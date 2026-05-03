using System.Globalization;
using CsvHelper;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RaceResults.Web.Data;
using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

public class RaceResultsService : IRaceResultsService
{
    private readonly IDbContextFactory<RaceResultsDbContext> _dbContextFactory;
    private readonly ILogger<RaceResultsService> _logger;

    public RaceResultsService(IDbContextFactory<RaceResultsDbContext> dbContextFactory, ILogger<RaceResultsService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public RaceStatusCounts GetStatusCounts()
    {
        using var db = _dbContextFactory.CreateDbContext();
        return new RaceStatusCounts(
            db.Entrants.Count(),
            db.FinishBibRecords.Count(),
            db.TimingRows.Count()
        );
    }

    public async Task<OperationResult> UploadEntrantsAsync(IEnumerable<IFormFile> files)
    {
        var uploaded = files?.Where(f => f.Length > 0).ToList() ?? new List<IFormFile>();
        if (uploaded.Count == 0)
        {
            return OperationResult.Fail(new[] { "Upload at least one entrant Excel file." });
        }

        var errors = new List<string>();
        var parsedEntrants = new List<Entrant>();

        foreach (var file in uploaded)
        {
            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{file.FileName}: only .xlsx files are supported for entrants.");
                continue;
            }

            await using var stream = file.OpenReadStream();
            ParseEntrantsWorkbook(stream, file.FileName, parsedEntrants, errors);
        }

        var duplicateBibs = parsedEntrants
            .GroupBy(e => e.BibNumber, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(x => x)
            .ToList();

        foreach (var duplicateBib in duplicateBibs)
        {
            errors.Add($"Duplicate bib number detected: {duplicateBib}");
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("UploadEntrants validation failed: {Errors}", string.Join("; ", errors));
            return OperationResult.Fail(errors);
        }

        var deduped = parsedEntrants
            .GroupBy(e => e.BibNumber, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(e => e.BibNumber)
            .ToList();

        await using var db = _dbContextFactory.CreateDbContext();
        await db.TimingRows.ExecuteDeleteAsync();
        await db.FinishBibRecords.ExecuteDeleteAsync();
        await db.Entrants.ExecuteDeleteAsync();
        db.Entrants.AddRange(deduped);
        await db.SaveChangesAsync();

        _logger.LogInformation("Entrants uploaded: {Count} entrants from {FileCount} file(s).", deduped.Count, uploaded.Count);
        return OperationResult.Ok($"Loaded {deduped.Count} entrants from {uploaded.Count} file(s).", "Finish bib and timing data were reset.");
    }

    public async Task<OperationResult> UploadFinishBibAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return OperationResult.Fail(new[] { "Upload a finish position and bib Excel file." });
        }

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult.Fail(new[] { "Finish bib upload only supports .xlsx." });
        }

        await using var checkDb = _dbContextFactory.CreateDbContext();
        if (!await checkDb.Entrants.AnyAsync())
        {
            return OperationResult.Fail(new[] { "Upload entrants before uploading finish positions." });
        }

        var errors = new List<string>();
        var warnings = new List<string>();
        var rows = new List<FinishBibRecord>();

        await using var stream = file.OpenReadStream();
        ParseFinishBibWorkbook(stream, file.FileName, rows, errors);

        var entrantBibSet = await checkDb.Entrants.Select(e => e.BibNumber).ToHashSetAsync(StringComparer.OrdinalIgnoreCase);
        var unmatched = rows
            .Where(r => !entrantBibSet.Contains(r.BibNumber))
            .Select(r => r.BibNumber)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        if (unmatched.Count > 0)
        {
            warnings.Add($"Unmatched bib numbers: {string.Join(", ", unmatched)}");
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("UploadFinishBib validation failed: {Errors}", string.Join("; ", errors));
            return OperationResult.Fail(errors);
        }

        await checkDb.TimingRows.ExecuteDeleteAsync();
        await checkDb.FinishBibRecords.ExecuteDeleteAsync();
        var ordered = rows.OrderBy(r => r.Position).ToList();
        checkDb.FinishBibRecords.AddRange(ordered);
        await checkDb.SaveChangesAsync();

        if (warnings.Count > 0)
        {
            _logger.LogWarning("UploadFinishBib completed with warnings: {Warnings}", string.Join("; ", warnings));
        }
        else
        {
            _logger.LogInformation("Finish bib uploaded: {Count} rows.", ordered.Count);
        }

        var result = OperationResult.Ok($"Loaded {ordered.Count} finish rows.", "Timing data was reset.");
        result.Warnings.AddRange(warnings);
        return result;
    }

    public async Task<OperationResult> UploadTimingsAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return OperationResult.Fail(new[] { "Upload a timing file (.csv or .xlsx)." });
        }

        await using var db = _dbContextFactory.CreateDbContext();
        if (!await db.FinishBibRecords.AnyAsync())
        {
            return OperationResult.Fail(new[] { "Upload finish position and bib data before timings." });
        }

        var errors = new List<string>();
        var rows = new Dictionary<int, string>();

        await using var stream = file.OpenReadStream();
        if (file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            ParseTimingsCsv(stream, file.FileName, rows, errors);
        }
        else if (file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            ParseTimingsWorkbook(stream, file.FileName, rows, errors);
        }
        else
        {
            errors.Add("Timing upload supports .csv or .xlsx only.");
        }

        if (errors.Count > 0)
        {
            return OperationResult.Fail(errors);
        }

        var finishPositions = await db.FinishBibRecords.Select(r => r.Position).OrderBy(x => x).ToListAsync();
        var timingPositions = rows.Keys.OrderBy(x => x).ToList();

        if (ShouldShiftTimingPositions(finishPositions, timingPositions))
        {
            rows = rows.ToDictionary(kvp => kvp.Key + 1, kvp => kvp.Value);
            timingPositions = rows.Keys.OrderBy(x => x).ToList();
        }

        var missing = finishPositions.Except(timingPositions).ToList();
        var extra = timingPositions.Except(finishPositions).ToList();

        if (missing.Count > 0)
        {
            errors.Add($"Timing file is missing positions: {string.Join(", ", missing)}");
        }

        if (extra.Count > 0)
        {
            errors.Add($"Timing file has unexpected positions: {string.Join(", ", extra)}");
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("UploadTimings validation failed: {Errors}", string.Join("; ", errors));
            return OperationResult.Fail(errors);
        }

        await db.TimingRows.ExecuteDeleteAsync();
        db.TimingRows.AddRange(rows.Select(kvp => new TimingRow { Position = kvp.Key, Time = kvp.Value }));
        await db.SaveChangesAsync();

        _logger.LogInformation("Timings uploaded: {Count} rows.", rows.Count);
        return OperationResult.Ok($"Loaded {rows.Count} timing rows.");
    }

    private static bool ShouldShiftTimingPositions(IReadOnlyCollection<int> finishPositions, IReadOnlyCollection<int> timingPositions)
    {
        if (!timingPositions.Contains(0))
        {
            return false;
        }

        var finishSet = finishPositions.ToHashSet();
        var timingSet = timingPositions.ToHashSet();

        if (finishSet.SetEquals(timingSet))
        {
            return false;
        }

        var shiftedTimingSet = timingSet.Select(p => p + 1).ToHashSet();
        return finishSet.SetEquals(shiftedTimingSet);
    }

    public IReadOnlyList<ResultRecord> GetCollatedResults()
    {
        using var db = _dbContextFactory.CreateDbContext();
        var entrantByBib = db.Entrants.ToDictionary(e => e.BibNumber, StringComparer.OrdinalIgnoreCase);
        var timings = db.TimingRows.ToDictionary(t => t.Position, t => t.Time);

        return db.FinishBibRecords
            .OrderBy(r => r.Position)
            .ToList()
            .Select(r => new ResultRecord
            {
                Position = r.Position,
                BibNumber = r.BibNumber,
                Time = timings.TryGetValue(r.Position, out var time) ? time : string.Empty,
                Entrant = entrantByBib.TryGetValue(r.BibNumber, out var entrant) ? entrant : null
            })
            .ToList();
    }

    public IReadOnlyList<Entrant> GetDnfEntrants()
    {
        using var db = _dbContextFactory.CreateDbContext();
        var finishedBibs = db.FinishBibRecords.Select(x => x.BibNumber);
        return db.Entrants
            .Where(e => !finishedBibs.Contains(e.BibNumber))
            .OrderBy(e => e.BibNumber)
            .ToList();
    }

    public RaceStats GetRaceStats()
    {
        using var db = _dbContextFactory.CreateDbContext();
        var entrants = db.Entrants.ToList();
        var males = entrants.Where(e => IsMale(e.Gender)).ToList();
        var females = entrants.Where(e => IsFemale(e.Gender)).ToList();

        return new RaceStats
        {
            TotalMales = males.Count,
            TotalFemales = females.Count,
            TotalMalesU18 = males.Count(e => e.IsU18),
            TotalFemalesU18 = females.Count(e => e.IsU18),
            TotalMalesUnaffiliatedExcludingU18 = males.Count(e => !e.IsU18 && e.IsUnaffiliated),
            TotalFemalesUnaffiliatedExcludingU18 = females.Count(e => !e.IsU18 && e.IsUnaffiliated)
        };
    }

    public IReadOnlyList<TopTenCategory> GetTopTenByCategory()
    {
        var collated = GetCollatedResults();

        return new List<TopTenCategory>
        {
            BuildTopTen("Male", collated, e => IsMale(e.Gender)),
            BuildTopTen("Female", collated, e => IsFemale(e.Gender)),
            BuildTopTen("Male U18", collated, e => IsMale(e.Gender) && e.IsU18),
            BuildTopTen("Female U18", collated, e => IsFemale(e.Gender) && e.IsU18)
        };
    }

    public bool TryGetEditableResult(int position, out EditResultInput editInput)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var row = db.FinishBibRecords.FirstOrDefault(r => r.Position == position);
        if (row is null)
        {
            editInput = new EditResultInput();
            return false;
        }

        var timing = db.TimingRows.Find(position);
        editInput = new EditResultInput
        {
            OriginalPosition = row.Position,
            NewPosition = row.Position,
            BibNumber = row.BibNumber,
            Time = timing?.Time ?? string.Empty
        };

        return true;
    }

    public OperationResult UpdateResult(EditResultInput editInput)
    {
        var errors = new List<string>();

        using var db = _dbContextFactory.CreateDbContext();
        var row = db.FinishBibRecords.FirstOrDefault(r => r.Position == editInput.OriginalPosition);
        if (row is null)
        {
            errors.Add("Result row not found.");
            return OperationResult.Fail(errors);
        }

        if (db.FinishBibRecords.Any(r => r.Position == editInput.NewPosition && r.Position != editInput.OriginalPosition))
        {
            errors.Add("The new position is already used by another row.");
        }

        if (!db.Entrants.Any(e => e.BibNumber.ToLower() == editInput.BibNumber.ToLower()))
        {
            errors.Add("Bib number does not match a registered entrant.");
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("UpdateResult validation failed for position {Position}: {Errors}", editInput.OriginalPosition, string.Join("; ", errors));
            return OperationResult.Fail(errors);
        }

        var oldPosition = row.Position;
        row.Position = editInput.NewPosition;
        row.BibNumber = editInput.BibNumber.Trim();

        var oldTiming = db.TimingRows.Find(oldPosition);

        if (!string.IsNullOrWhiteSpace(editInput.Time))
        {
            if (oldPosition != editInput.NewPosition && oldTiming is not null)
            {
                db.TimingRows.Remove(oldTiming);
            }
            var newTiming = db.TimingRows.Find(editInput.NewPosition);
            if (newTiming is null)
            {
                db.TimingRows.Add(new TimingRow { Position = editInput.NewPosition, Time = editInput.Time.Trim() });
            }
            else
            {
                newTiming.Time = editInput.Time.Trim();
            }
        }
        else if (oldTiming is not null && oldPosition != editInput.NewPosition)
        {
            db.TimingRows.Remove(oldTiming);
            db.TimingRows.Add(new TimingRow { Position = editInput.NewPosition, Time = oldTiming.Time });
        }

        db.SaveChanges();
        return OperationResult.Ok("Result row updated successfully.");
    }

    public byte[] GenerateResultsPdf()
    {
        var collated = GetCollatedResults();
        var dnfs = GetDnfEntrants();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(20);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header()
                    .Text("5K Race Results")
                    .SemiBold()
                    .FontSize(20)
                    .FontColor(Colors.Blue.Darken2);

                page.Content().Column(column =>
                {
                    column.Spacing(8);

                    column.Item().Text("Final Standings").SemiBold().FontSize(14);

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(def =>
                        {
                            def.ConstantColumn(30);
                            def.ConstantColumn(62);
                            def.ConstantColumn(45);
                            def.RelativeColumn(2);
                            def.RelativeColumn(2);
                            def.ConstantColumn(50);
                            def.ConstantColumn(35);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCellStyle).Text("Pos").SemiBold();
                            header.Cell().Element(HeaderCellStyle).Text("Time").SemiBold();
                            header.Cell().Element(HeaderCellStyle).Text("Bib").SemiBold();
                            header.Cell().Element(HeaderCellStyle).Text("Name").SemiBold();
                            header.Cell().Element(HeaderCellStyle).Text("Club").SemiBold();
                            header.Cell().Element(HeaderCellStyle).Text("Gender").SemiBold();
                            header.Cell().Element(HeaderCellStyle).Text("Age").SemiBold();
                        });

                        foreach (var row in collated)
                        {
                            table.Cell().Element(BodyCellStyle).Text(row.Position.ToString(CultureInfo.InvariantCulture));
                            table.Cell().Element(BodyCellStyle).Text(row.Time);
                            table.Cell().Element(BodyCellStyle).Text(row.BibNumber);
                            table.Cell().Element(BodyCellStyle).Text(row.Name);
                            table.Cell().Element(BodyCellStyle).Text(row.Club);
                            table.Cell().Element(BodyCellStyle).Text(row.Gender);
                            table.Cell().Element(BodyCellStyle).Text(row.Age?.ToString(CultureInfo.InvariantCulture) ?? "-");
                        }
                    });

                    column.Item().PaddingTop(8).Text("DNF Runners").SemiBold().FontSize(14);

                    if (dnfs.Count == 0)
                    {
                        column.Item().Text("None");
                    }
                    else
                    {
                        foreach (var dnf in dnfs)
                        {
                            column.Item().Text($"- {dnf.BibNumber}: {dnf.Name} ({(string.IsNullOrWhiteSpace(dnf.Club) ? "Unaffiliated" : dnf.Club)})");
                        }
                    }
                });
            });
        });

        return document.GeneratePdf();
    }

    private static IContainer HeaderCellStyle(IContainer container)
    {
        return container
            .Background(Colors.Grey.Lighten2)
            .Padding(4);
    }

    private static IContainer BodyCellStyle(IContainer container)
    {
        return container
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(4);
    }

    private static TopTenCategory BuildTopTen(string name, IReadOnlyList<ResultRecord> collated, Func<Entrant, bool> predicate)
    {
        return new TopTenCategory
        {
            Name = name,
            Results = collated
                .Where(r => r.Entrant is not null && predicate(r.Entrant))
                .Take(10)
                .ToList()
        };
    }

    private static bool IsMale(string gender)
    {
        return Normalize(gender).StartsWith("m", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFemale(string gender)
    {
        return Normalize(gender).StartsWith("f", StringComparison.OrdinalIgnoreCase);
    }

    private static void ParseEntrantsWorkbook(Stream stream, string fileName, List<Entrant> entrants, List<string> errors)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();

        var headerMap = GetHeaderMap(sheet.Row(1));
        var required = new[]
        {
            new[] { "bib", "bibnumber", "bibno", "bibnum", "number", "racenumber", "raceno", "runnernumber" },
            new[] { "name", "fullname", "runnername" },
            new[] { "gender", "sex", "mf" }
        };

        foreach (var requiredSet in required)
        {
            if (FindColumnIndex(headerMap, requiredSet) is null)
            {
                errors.Add($"{fileName} row 1: missing required column ({requiredSet[0]}).");
            }
        }

        if (errors.Count > 0)
        {
            return;
        }

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = sheet.Row(rowNumber);
            if (row.IsEmpty())
            {
                continue;
            }

            var bib = ReadCell(row, headerMap, new[] { "bib", "bibnumber", "bibno", "bibnum", "number", "racenumber", "raceno", "runnernumber" });
            var name = ReadCell(row, headerMap, new[] { "name", "fullname", "runnername" });
            var club = ReadCell(row, headerMap, new[] { "club", "team", "clubname" });
            var gender = ReadCell(row, headerMap, new[] { "gender", "sex", "mf" });
            var ageRaw = ReadCell(row, headerMap, new[] { "age" });

            if (string.IsNullOrWhiteSpace(bib) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(gender))
            {
                errors.Add($"{fileName} row {rowNumber}: bib, name, and gender are required.");
                continue;
            }

            int? age = null;
            if (!string.IsNullOrWhiteSpace(ageRaw))
            {
                if (!int.TryParse(ageRaw, out var parsedAge))
                {
                    errors.Add($"{fileName} row {rowNumber}: age is invalid ({ageRaw}).");
                    continue;
                }
                age = parsedAge;
            }

            entrants.Add(new Entrant
            {
                BibNumber = bib.Trim(),
                Name = name.Trim(),
                Club = club.Trim(),
                Gender = NormalizeGender(gender),
                Age = age
            });
        }
    }

    private static void ParseFinishBibWorkbook(Stream stream, string fileName, List<FinishBibRecord> rows, List<string> errors)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();
        var headerMap = GetHeaderMap(sheet.Row(1));

        if (FindColumnIndex(headerMap, new[] { "position", "finishposition", "place" }) is null)
        {
            errors.Add($"{fileName} row 1: missing required column (position).");
        }

        if (FindColumnIndex(headerMap, new[] { "bib", "bibnumber", "bibno", "bibnum", "number", "racenumber", "raceno", "runnernumber" }) is null)
        {
            errors.Add($"{fileName} row 1: missing required column (bib).");
        }

        if (errors.Count > 0)
        {
            return;
        }

        var firstPositionRow = new Dictionary<int, int>();
        var firstBibRow = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = sheet.Row(rowNumber);
            if (row.IsEmpty())
            {
                continue;
            }

            var positionRaw = ReadCell(row, headerMap, new[] { "position", "finishposition", "place" });
            var bib = ReadCell(row, headerMap, new[] { "bib", "bibnumber", "bibno", "bibnum", "number", "racenumber", "raceno", "runnernumber" });

            if (!int.TryParse(positionRaw, out var position) || position < 1)
            {
                errors.Add($"{fileName} row {rowNumber}: invalid position ({positionRaw}).");
                continue;
            }

            if (string.IsNullOrWhiteSpace(bib))
            {
                errors.Add($"{fileName} row {rowNumber}: bib is required.");
                continue;
            }

            if (firstPositionRow.TryGetValue(position, out var firstPositionSeenAt))
            {
                errors.Add($"{fileName} row {rowNumber}: duplicate position ({position}), first seen at row {firstPositionSeenAt}.");
                continue;
            }

            var trimmedBib = bib.Trim();
            if (firstBibRow.TryGetValue(trimmedBib, out var firstBibSeenAt))
            {
                errors.Add($"{fileName} row {rowNumber}: duplicate bib ({trimmedBib}), first seen at row {firstBibSeenAt}.");
                continue;
            }

            firstPositionRow[position] = rowNumber;
            firstBibRow[trimmedBib] = rowNumber;

            rows.Add(new FinishBibRecord
            {
                Position = position,
                BibNumber = trimmedBib
            });
        }
    }

    private static void ParseTimingsWorkbook(Stream stream, string fileName, Dictionary<int, string> rows, List<string> errors)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();
        var headerMap = GetHeaderMap(sheet.Row(1));

        if (FindColumnIndex(headerMap, new[] { "position", "finishposition", "place" }) is null)
        {
            errors.Add($"{fileName} row 1: missing required column (position).");
        }

        if (FindColumnIndex(headerMap, new[] { "time", "timing", "finishtime" }) is null)
        {
            errors.Add($"{fileName} row 1: missing required column (time).");
        }

        if (errors.Count > 0)
        {
            return;
        }

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = sheet.Row(rowNumber);
            if (row.IsEmpty())
            {
                continue;
            }

            var positionRaw = ReadCell(row, headerMap, new[] { "position", "finishposition", "place" });
            var time = ReadCell(row, headerMap, new[] { "time", "timing", "finishtime" });

            if (!int.TryParse(positionRaw, out var position))
            {
                errors.Add($"{fileName} row {rowNumber}: invalid position ({positionRaw}).");
                continue;
            }

            if (string.IsNullOrWhiteSpace(time))
            {
                errors.Add($"{fileName} row {rowNumber}: time is required.");
                continue;
            }

            if (!rows.TryAdd(position, time.Trim()))
            {
                errors.Add($"{fileName} row {rowNumber}: duplicate timing position ({position}).");
            }
        }
    }

    private static void ParseTimingsCsv(Stream stream, string fileName, Dictionary<int, string> rows, List<string> errors)
    {
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        var rowNumber = 0;
        while (csv.Read())
        {
            rowNumber++;
            var col0 = csv.GetField(0)?.Trim();
            if (string.IsNullOrWhiteSpace(col0))
            {
                continue;
            }

            if (col0.Equals("STARTOFEVENT", StringComparison.OrdinalIgnoreCase) || col0.Equals("ENDOFEVENT", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var time = csv.GetField(2)?.Trim();

            if (!int.TryParse(col0, out var position))
            {
                errors.Add($"{fileName} row {rowNumber}: invalid position ({col0}).");
                continue;
            }

            if (string.IsNullOrWhiteSpace(time))
            {
                errors.Add($"{fileName} row {rowNumber}: time is required.");
                continue;
            }

            if (!rows.TryAdd(position, time))
            {
                errors.Add($"{fileName} row {rowNumber}: duplicate timing position ({position}).");
            }
        }
    }

    private static Dictionary<string, int> GetHeaderMap(IXLRow row)
    {
        return row.CellsUsed()
            .ToDictionary(c => Normalize(c.GetString()), c => c.Address.ColumnNumber);
    }

    private static int? FindColumnIndex(Dictionary<string, int> map, IEnumerable<string> aliases)
    {
        foreach (var alias in aliases)
        {
            var key = Normalize(alias);
            if (map.TryGetValue(key, out var index))
            {
                return index;
            }
        }

        return null;
    }

    private static string ReadCell(IXLRow row, Dictionary<string, int> map, IEnumerable<string> aliases)
    {
        var index = FindColumnIndex(map, aliases);
        if (index is null)
        {
            return string.Empty;
        }

        return row.Cell(index.Value).GetString();
    }

    private static string NormalizeGender(string value)
    {
        var normalized = Normalize(value);
        if (normalized.StartsWith("m", StringComparison.OrdinalIgnoreCase))
        {
            return "Male";
        }

        if (normalized.StartsWith("f", StringComparison.OrdinalIgnoreCase))
        {
            return "Female";
        }

        return value.Trim();
    }

    private static string Normalize(string value)
    {
        return new string(value
            .Where(ch => char.IsLetterOrDigit(ch))
            .ToArray())
            .ToLowerInvariant();
    }
}
