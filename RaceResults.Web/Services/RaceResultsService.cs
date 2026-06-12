using System.Globalization;
using System.IO;
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

    public RaceEvent GetCurrentEvent()
    {
        using var db = _dbContextFactory.CreateDbContext();
        return EnsureCurrentEvent(db);
    }

    public IReadOnlyList<RaceEvent> GetEvents()
    {
        using var db = _dbContextFactory.CreateDbContext();
        return db.Events
            .OrderByDescending(e => e.EventDate)
            .ThenBy(e => e.EventName)
            .ToList();
    }

    public OperationResult CreateEvent(CreateEventInput input)
    {
        var name = input.EventName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return OperationResult.Fail(new[] { "Event name is required." });
        }

        using var db = _dbContextFactory.CreateDbContext();
        var existingCurrent = EnsureCurrentEvent(db);
        existingCurrent.IsCurrent = false;

        var entity = new RaceEvent
        {
            EventName = name,
            EventDate = input.EventDate.Date,
            EventType = input.EventType,
            IsCurrent = true
        };

        db.Events.Add(entity);
        db.SaveChanges();

        return OperationResult.Ok($"Created event '{entity.EventName}' and set as current.");
    }

    public OperationResult UpdateEvent(EditEventInput input)
    {
        var name = input.EventName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return OperationResult.Fail(new[] { "Event name is required." });
        }

        using var db = _dbContextFactory.CreateDbContext();
        var entity = db.Events.FirstOrDefault(e => e.Id == input.Id);
        if (entity is null)
        {
            return OperationResult.Fail(new[] { "Event not found." });
        }

        entity.EventName = name;
        entity.EventDate = input.EventDate.Date;
        entity.EventType = input.EventType;
        db.SaveChanges();

        return OperationResult.Ok("Event updated.");
    }

    public OperationResult SetCurrentEvent(int eventId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var target = db.Events.FirstOrDefault(e => e.Id == eventId);
        if (target is null)
        {
            return OperationResult.Fail(new[] { "Event not found." });
        }

        foreach (var raceEvent in db.Events.Where(e => e.IsCurrent && e.Id != eventId))
        {
            raceEvent.IsCurrent = false;
        }

        target.IsCurrent = true;
        db.SaveChanges();
        return OperationResult.Ok($"Current event set to '{target.EventName}'.");
    }

    public OperationResult DeleteEvent(int eventId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var target = db.Events.FirstOrDefault(e => e.Id == eventId);
        if (target is null)
        {
            return OperationResult.Fail(new[] { "Event not found." });
        }

        db.Entrants.Where(e => e.EventId == eventId).ExecuteDelete();
        db.FinishBibRecords.Where(r => r.EventId == eventId).ExecuteDelete();
        db.TimingRows.Where(t => t.EventId == eventId).ExecuteDelete();
        db.Events.Remove(target);
        db.SaveChanges();

        if (!db.Events.Any(e => e.IsCurrent))
        {
            var next = db.Events.OrderByDescending(e => e.EventDate).FirstOrDefault();
            if (next is null)
            {
                EnsureCurrentEvent(db);
            }
            else
            {
                next.IsCurrent = true;
                db.SaveChanges();
            }
        }

        return OperationResult.Ok("Event deleted and data reset for that event.");
    }

    public RaceStatusCounts GetStatusCounts()
    {
        using var db = _dbContextFactory.CreateDbContext();
        var currentEventId = EnsureCurrentEvent(db).Id;
        return new RaceStatusCounts(
            db.Entrants.Count(e => e.EventId == currentEventId),
            db.FinishBibRecords.Count(r => r.EventId == currentEventId),
            db.TimingRows.Count(t => t.EventId == currentEventId)
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

        // Duplicate names are not necessarily an error (two people can share a name), but they are
        // a common data-entry mistake, so surface them as a warning for the organizer to review.
        var duplicateNames = deduped
            .Where(e => !string.IsNullOrWhiteSpace(e.Name))
            .GroupBy(e => e.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(x => x)
            .ToList();

        await using var db = _dbContextFactory.CreateDbContext();
        var currentEvent = await EnsureCurrentEventAsync(db);
        await db.TimingRows.Where(t => t.EventId == currentEvent.Id).ExecuteDeleteAsync();
        await db.FinishBibRecords.Where(r => r.EventId == currentEvent.Id).ExecuteDeleteAsync();
        await db.Entrants.Where(e => e.EventId == currentEvent.Id).ExecuteDeleteAsync();

        foreach (var entrant in deduped)
        {
            entrant.EventId = currentEvent.Id;
        }

        db.Entrants.AddRange(deduped);
        await db.SaveChangesAsync();

        _logger.LogInformation("Entrants uploaded: {Count} entrants from {FileCount} file(s).", deduped.Count, uploaded.Count);
        var result = OperationResult.Ok($"Loaded {deduped.Count} entrants from {uploaded.Count} file(s).", "Finish bib and timing data were reset.");
        if (duplicateNames.Count > 0)
        {
            result.Warnings.Add($"Duplicate entrant names detected (please verify): {string.Join(", ", duplicateNames)}");
        }
        return result;
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
        var currentEvent = await EnsureCurrentEventAsync(checkDb);
        if (!await checkDb.Entrants.AnyAsync(e => e.EventId == currentEvent.Id))
        {
            return OperationResult.Fail(new[] { "Upload entrants before uploading finish positions." });
        }

        var errors = new List<string>();
        var warnings = new List<string>();
        var rows = new List<FinishBibRecord>();

        await using var stream = file.OpenReadStream();
        ParseFinishBibWorkbook(stream, file.FileName, rows, errors);

        var entrantBibSet = await checkDb.Entrants
            .Where(e => e.EventId == currentEvent.Id)
            .Select(e => e.BibNumber)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase);
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

        await checkDb.TimingRows.Where(t => t.EventId == currentEvent.Id).ExecuteDeleteAsync();
        await checkDb.FinishBibRecords.Where(r => r.EventId == currentEvent.Id).ExecuteDeleteAsync();
        var ordered = rows.OrderBy(r => r.Position).ToList();
        foreach (var finishRow in ordered)
        {
            finishRow.EventId = currentEvent.Id;
        }
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
        var currentEvent = await EnsureCurrentEventAsync(db);
        if (!await db.FinishBibRecords.AnyAsync(r => r.EventId == currentEvent.Id))
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

        var finishPositions = await db.FinishBibRecords
            .Where(r => r.EventId == currentEvent.Id)
            .Select(r => r.Position)
            .OrderBy(x => x)
            .ToListAsync();
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

        await db.TimingRows.Where(t => t.EventId == currentEvent.Id).ExecuteDeleteAsync();
        db.TimingRows.AddRange(rows.Select(kvp => new TimingRow
        {
            EventId = currentEvent.Id,
            Position = kvp.Key,
            Time = kvp.Value
        }));
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
        var currentEventId = EnsureCurrentEvent(db).Id;
        return GetCollatedResultsForEvent(db, currentEventId);
    }

    public IReadOnlyList<ResultRecord> GetCollatedResults(int eventId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        return GetCollatedResultsForEvent(db, eventId);
    }

    private static IReadOnlyList<ResultRecord> GetCollatedResultsForEvent(RaceResultsDbContext db, int eventId)
    {
        var entrantByBib = db.Entrants
            .Where(e => e.EventId == eventId)
            .ToDictionary(e => e.BibNumber, StringComparer.OrdinalIgnoreCase);
        var timings = db.TimingRows
            .Where(t => t.EventId == eventId)
            .ToDictionary(t => t.Position, t => t.Time);

        return db.FinishBibRecords
            .Where(r => r.EventId == eventId)
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
        var currentEventId = EnsureCurrentEvent(db).Id;
        var finishedBibs = db.FinishBibRecords
            .Where(x => x.EventId == currentEventId)
            .Select(x => x.BibNumber);
        return db.Entrants
            .Where(e => e.EventId == currentEventId)
            .Where(e => !finishedBibs.Contains(e.BibNumber))
            .OrderBy(e => e.BibNumber)
            .ToList();
    }

    public RaceStats GetRaceStats()
    {
        using var db = _dbContextFactory.CreateDbContext();
        var currentEventId = EnsureCurrentEvent(db).Id;
        var entrants = db.Entrants.Where(e => e.EventId == currentEventId).ToList();
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
        return BuildTopTenFromCollated(collated);
    }

    public IReadOnlyList<TopTenCategory> GetTopTenByCategory(int eventId)
    {
        var collated = GetCollatedResults(eventId);
        return BuildTopTenFromCollated(collated);
    }

    private static IReadOnlyList<TopTenCategory> BuildTopTenFromCollated(IReadOnlyList<ResultRecord> collated)
    {
        return new List<TopTenCategory>
        {
            BuildTopTen("Male", collated, e => IsMale(e.Gender) && !e.IsU18),
            BuildTopTen("Female", collated, e => IsFemale(e.Gender) && !e.IsU18),
            BuildTopTen("Male U18", collated, e => IsMale(e.Gender) && e.IsU18),
            BuildTopTen("Female U18", collated, e => IsFemale(e.Gender) && e.IsU18)
        };
    }

    public bool TryGetEditableResult(int position, out EditResultInput editInput)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var currentEventId = EnsureCurrentEvent(db).Id;
        var row = db.FinishBibRecords.FirstOrDefault(r => r.EventId == currentEventId && r.Position == position);
        if (row is null)
        {
            editInput = new EditResultInput();
            return false;
        }

        var timing = db.TimingRows.FirstOrDefault(t => t.EventId == currentEventId && t.Position == position);
        var entrant = db.Entrants.FirstOrDefault(e => e.EventId == currentEventId && e.BibNumber.ToLower() == row.BibNumber.ToLower());
        editInput = new EditResultInput
        {
            OriginalPosition = row.Position,
            NewPosition = row.Position,
            BibNumber = row.BibNumber,
            Name = entrant?.Name ?? string.Empty,
            Club = entrant?.Club,
            Gender = entrant?.Gender ?? string.Empty,
            Age = entrant?.Age,
            Time = timing?.Time ?? string.Empty
        };

        return true;
    }

    public OperationResult UpdateResult(EditResultInput editInput)
    {
        var errors = new List<string>();

        var trimmedBib = editInput.BibNumber.Trim();
        var trimmedName = editInput.Name.Trim();
        var trimmedGender = editInput.Gender.Trim();
        var trimmedClub = editInput.Club?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmedBib))
        {
            errors.Add("Bib number is required.");
        }

        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            errors.Add("Name is required.");
        }

        if (string.IsNullOrWhiteSpace(trimmedGender))
        {
            errors.Add("Gender is required.");
        }

        using var db = _dbContextFactory.CreateDbContext();
        var currentEventId = EnsureCurrentEvent(db).Id;
        var row = db.FinishBibRecords.FirstOrDefault(r => r.EventId == currentEventId && r.Position == editInput.OriginalPosition);
        if (row is null)
        {
            errors.Add("Result row not found.");
            return OperationResult.Fail(errors);
        }

        if (db.FinishBibRecords.Any(r => r.EventId == currentEventId && r.Position == editInput.NewPosition && r.Position != editInput.OriginalPosition))
        {
            errors.Add("The new position is already used by another row.");
        }

        if (db.FinishBibRecords.Any(r => r.EventId == currentEventId && r.Position != editInput.OriginalPosition && r.BibNumber.ToLower() == trimmedBib.ToLower()))
        {
            errors.Add("Bib number is already used by another result row.");
        }

        var oldBib = row.BibNumber.Trim();
        var oldBibLower = oldBib.ToLower();
        var trimmedBibLower = trimmedBib.ToLower();

        var entrantByOldBib = db.Entrants.FirstOrDefault(e => e.EventId == currentEventId && e.BibNumber.ToLower() == oldBibLower);
        var entrantByNewBib = db.Entrants.FirstOrDefault(e => e.EventId == currentEventId && e.BibNumber.ToLower() == trimmedBibLower);

        if (entrantByOldBib is not null && entrantByNewBib is not null && entrantByOldBib.Id != entrantByNewBib.Id)
        {
            errors.Add("Bib number is already used by another entrant.");
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("UpdateResult validation failed for position {Position}: {Errors}", editInput.OriginalPosition, string.Join("; ", errors));
            return OperationResult.Fail(errors);
        }

        var oldPosition = row.Position;
        row.Position = editInput.NewPosition;
        row.BibNumber = trimmedBib;

        var entrantToUpdate = entrantByOldBib ?? entrantByNewBib;
        if (entrantToUpdate is null)
        {
            entrantToUpdate = new Entrant();
            entrantToUpdate.EventId = currentEventId;
            db.Entrants.Add(entrantToUpdate);
        }

        entrantToUpdate.BibNumber = trimmedBib;
        entrantToUpdate.Name = trimmedName;
        entrantToUpdate.Club = trimmedClub;
        entrantToUpdate.Gender = trimmedGender;
        entrantToUpdate.Age = editInput.Age;

        var oldTiming = db.TimingRows.FirstOrDefault(t => t.EventId == currentEventId && t.Position == oldPosition);

        if (!string.IsNullOrWhiteSpace(editInput.Time))
        {
            if (oldPosition != editInput.NewPosition && oldTiming is not null)
            {
                db.TimingRows.Remove(oldTiming);
            }
            var newTiming = db.TimingRows.FirstOrDefault(t => t.EventId == currentEventId && t.Position == editInput.NewPosition);
            if (newTiming is null)
            {
                db.TimingRows.Add(new TimingRow { EventId = currentEventId, Position = editInput.NewPosition, Time = editInput.Time.Trim() });
            }
            else
            {
                newTiming.Time = editInput.Time.Trim();
            }
        }
        else if (oldTiming is not null && oldPosition != editInput.NewPosition)
        {
            db.TimingRows.Remove(oldTiming);
            db.TimingRows.Add(new TimingRow { EventId = currentEventId, Position = editInput.NewPosition, Time = oldTiming.Time });
        }

        db.SaveChanges();
        return OperationResult.Ok("Result row updated successfully.");
    }

    public byte[] GenerateResultsPdf()
    {
        var collated = GetCollatedResults();
        var currentEvent = GetCurrentEvent();
        var logoBytes = TryLoadPdfLogo();

        var maleWinner = FindWinner(collated, e => IsMale(e.Gender) && !e.IsU18);
        var femaleWinner = FindWinner(collated, e => IsFemale(e.Gender) && !e.IsU18);
        var maleYouthWinner = FindWinner(collated, e => IsMale(e.Gender) && e.IsU18);
        var femaleYouthWinner = FindWinner(collated, e => IsFemale(e.Gender) && e.IsU18);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(20);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(x => BuildPdfHeader(x, logoBytes, currentEvent));

                page.Content().Column(column =>
                {
                    column.Spacing(6);

                    column.Item().ShowOnce().Element(x => BuildPdfWinnersBlock(
                        x,
                        maleWinner,
                        femaleWinner,
                        maleYouthWinner,
                        femaleYouthWinner,
                        currentEvent.EventType == EventType.CrownToCrown));

                    column.Item().Border(1).BorderColor(Colors.Black).Table(table =>
                    {
                        table.ColumnsDefinition(def =>
                        {
                            def.ConstantColumn(55);
                            def.ConstantColumn(62);
                            def.ConstantColumn(45);
                            def.RelativeColumn(2);
                            def.ConstantColumn(50);
                            def.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCellStyle).AlignCenter().Text("Position").SemiBold().FontColor(Colors.White);
                            header.Cell().Element(HeaderCellStyle).AlignCenter().Text("Time").SemiBold().FontColor(Colors.White);
                            header.Cell().Element(HeaderCellStyle).AlignCenter().Text("Race No").SemiBold().FontColor(Colors.White);
                            header.Cell().Element(HeaderCellStyle).Text("Name").SemiBold().FontColor(Colors.White);
                            header.Cell().Element(HeaderCellStyle).AlignCenter().Text("Gender").SemiBold().FontColor(Colors.White);
                            header.Cell().Element(HeaderCellStyle).Text("Club Name").SemiBold().FontColor(Colors.White);
                        });

                        for (var i = 0; i < collated.Count; i++)
                        {
                            var row = collated[i];
                            Func<IContainer, IContainer> bodyStyle = BodyCellStyle;

                            table.Cell().Element(bodyStyle).AlignCenter().Text(row.Position.ToString(CultureInfo.InvariantCulture));
                            table.Cell().Element(bodyStyle).AlignCenter().Text(row.Time);
                            table.Cell().Element(bodyStyle).AlignCenter().Text(row.BibNumber);
                            table.Cell().Element(bodyStyle).Text(ToPdfCellText(row.Name));
                            table.Cell().Element(bodyStyle).AlignCenter().Text(ToPdfCellText(row.Gender));
                            table.Cell().Element(bodyStyle).Text(ToPdfCellText(row.Club));
                        }
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private static ResultRecord? FindWinner(IEnumerable<ResultRecord> results, Func<Entrant, bool> predicate)
    {
        return results.FirstOrDefault(r => r.Entrant is not null && predicate(r.Entrant));
    }

    private static string WinnerText(ResultRecord? winner)
    {
        return winner?.Name ?? "-";
    }

    private static string ToPdfCellText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "\u00A0" : value;
    }

    private static byte[]? TryLoadPdfLogo()
    {
        var candidatePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "wwwroot", "images", "pitsea-logo-white.png"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "RaceResults.Web", "wwwroot", "images", "pitsea-logo-white.png")
        };

        foreach (var candidatePath in candidatePaths)
        {
            var fullPath = Path.GetFullPath(candidatePath);
            if (File.Exists(fullPath))
            {
                return File.ReadAllBytes(fullPath);
            }
        }

        return null;
    }

    private static void BuildPdfHeader(IContainer container, byte[]? logoBytes, RaceEvent raceEvent)
    {
        container.PaddingBottom(10).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.ConstantItem(80).AlignLeft().Element(x => RenderPdfLogo(x, logoBytes));

                row.RelativeItem().AlignCenter().Column(center =>
                {
                    center.Item().AlignCenter().Text("PITSEA RUNNING CLUB")
                        .SemiBold()
                        .FontSize(18);

                    center.Item().AlignCenter().Text(BuildPdfEventTitle(raceEvent))
                        .FontSize(14);
                });

                row.ConstantItem(80).AlignRight().Element(x => RenderPdfLogo(x, logoBytes));
            });
        });
    }

    private static void RenderPdfLogo(IContainer container, byte[]? logoBytes)
    {
        if (logoBytes is null)
        {
            container.Height(56);
            return;
        }

        container.Height(56).Width(56).Image(logoBytes).FitArea();
    }

    private static string BuildPdfEventTitle(RaceEvent raceEvent)
    {
        var date = raceEvent.EventDate;
        var formattedDate = $"{date.Day}{GetDaySuffix(date.Day)} {date:MMMM yyyy}".ToUpper();

        return $"{raceEvent.EventName.ToUpper()} RESULTS {formattedDate}";
    }

    private static string GetDaySuffix(int day)
    {
        if (day % 100 is >= 11 and <= 13)
        {
            return "th";
        }

        return (day % 10) switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th"
        };
    }

    private static void BuildPdfWinnersBlock(
        IContainer container,
        ResultRecord? maleWinner,
        ResultRecord? femaleWinner,
        ResultRecord? maleYouthWinner,
        ResultRecord? femaleYouthWinner,
        bool showCourseRecords)
    {
        container.PaddingTop(8).PaddingBottom(8).Column(column =>
        {
            column.Spacing(3);

            column.Item().Row(row =>
            {
                row.RelativeItem().Text($"1st Male = {WinnerText(maleWinner)}").FontSize(11);
                row.RelativeItem().AlignRight().Text($"1st Male Youth = {WinnerText(maleYouthWinner)}").FontSize(11);
            });

            column.Item().Row(row =>
            {
                row.RelativeItem().Text($"1st Female = {WinnerText(femaleWinner)}").FontSize(11);
                row.RelativeItem().AlignRight().Text($"1st Female Youth = {WinnerText(femaleYouthWinner)}").FontSize(11);
            });

            // Course records are specific to the Crown to Crown course; omit them for other events.
            if (showCourseRecords)
            {
                column.Item().PaddingTop(3).Text("Course records - 15:25 Adam Hickey (August 2013) 18:01 Jessica Judd (December 2015)")
                    .SemiBold()
                    .FontSize(11);
            }
        });
    }

    private static IContainer HeaderCellStyle(IContainer container)
    {
        return container
            .Background(Colors.Black)
            .Border(1)
            .BorderColor(Colors.White)
            .PaddingVertical(4)
            .PaddingHorizontal(6);
    }

    private static IContainer BodyCellStyle(IContainer container)
    {
        return container
            .Border(1)
            .BorderColor(Colors.Black)
            .Background(Colors.White)
            .PaddingVertical(2)
            .PaddingHorizontal(6);
    }

    private static async Task<RaceEvent> EnsureCurrentEventAsync(RaceResultsDbContext db)
    {
        var current = await db.Events.FirstOrDefaultAsync(e => e.IsCurrent);
        if (current is not null)
        {
            return current;
        }

        var existing = await db.Events.OrderByDescending(e => e.EventDate).FirstOrDefaultAsync();
        if (existing is not null)
        {
            existing.IsCurrent = true;
            await db.SaveChangesAsync();
            return existing;
        }

        var created = new RaceEvent
        {
            EventName = "Crown to Crown",
            EventDate = new DateTime(2026, 4, 3), // Good Friday 2026, the series' first race
            EventType = EventType.CrownToCrown,
            IsCurrent = true
        };

        db.Events.Add(created);
        await db.SaveChangesAsync();
        return created;
    }

    private static RaceEvent EnsureCurrentEvent(RaceResultsDbContext db)
    {
        var current = db.Events.FirstOrDefault(e => e.IsCurrent);
        if (current is not null)
        {
            return current;
        }

        var existing = db.Events.OrderByDescending(e => e.EventDate).FirstOrDefault();
        if (existing is not null)
        {
            existing.IsCurrent = true;
            db.SaveChanges();
            return existing;
        }

        var created = new RaceEvent
        {
            EventName = "Crown to Crown",
            EventDate = new DateTime(2026, 4, 3), // Good Friday 2026, the series' first race
            EventType = EventType.CrownToCrown,
            IsCurrent = true
        };

        db.Events.Add(created);
        db.SaveChanges();
        return created;
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
