using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using RaceResults.Web.Data;
using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

/// <summary>
/// Turns the two registration-platform CSVs (adults + U18) into the club's Online Registration
/// spreadsheet (US45). Parsing → preview → organiser resolves clubs → .xlsx generation.
/// </summary>
public class OnlineRegistrationGenerator : IOnlineRegistrationGenerator
{
    private const string U18MarkerColumn = "u18isdependantofuser";

    private readonly IDbContextFactory<RaceResultsDbContext> _factory;
    private readonly IClubService _clubs;
    private readonly ILogger<OnlineRegistrationGenerator> _logger;

    public OnlineRegistrationGenerator(
        IDbContextFactory<RaceResultsDbContext> factory,
        IClubService clubs,
        ILogger<OnlineRegistrationGenerator> logger)
    {
        _factory = factory;
        _clubs = clubs;
        _logger = logger;
    }

    // ----- Preview -----

    public OnlineRegistrationPreview BuildPreview(int eventId, Stream? adultsCsv, Stream? u18Csv)
    {
        var preview = new OnlineRegistrationPreview { EventId = eventId };

        using (var db = _factory.CreateDbContext())
        {
            var raceEvent = db.Events.FirstOrDefault(e => e.Id == eventId);
            if (raceEvent is null)
            {
                preview.Errors.Add("Event not found.");
                return preview;
            }
            if (raceEvent.EventType != EventType.CrownToCrown)
            {
                preview.Errors.Add("Online Registration generation is only available for Crown to Crown events.");
                return preview;
            }
            preview.EventName = raceEvent.EventName;
            preview.EventDate = raceEvent.EventDate;
        }

        if (adultsCsv is null && u18Csv is null)
        {
            preview.Errors.Add("Upload the adults CSV and the U18 CSV.");
            return preview;
        }
        if (adultsCsv is null) preview.Errors.Add("Missing adults CSV.");
        if (u18Csv is null) preview.Errors.Add("Missing U18 CSV.");

        var adultsIsU18 = false;
        var u18FileIsU18Flag = false;
        var parsedAdults = adultsCsv is not null ? ParseCsv(adultsCsv, "adults CSV", preview.Errors, out adultsIsU18) : new List<OnlineRegistrationRow>();
        var adultsFileIsU18 = adultsCsv is not null && adultsIsU18;

        var parsedU18 = u18Csv is not null ? ParseCsv(u18Csv, "U18 CSV", preview.Errors, out u18FileIsU18Flag) : new List<OnlineRegistrationRow>();
        var u18FileIsU18 = u18Csv is not null && u18FileIsU18Flag;

        if (adultsCsv is not null && u18Csv is not null)
        {
            if (adultsFileIsU18 && u18FileIsU18)
                preview.Errors.Add("Both files look like the U18 CSV (contain the 'U18IsDependantOfUser' column). Upload one of each.");
            else if (!adultsFileIsU18 && !u18FileIsU18)
                preview.Errors.Add("Neither file has the 'U18IsDependantOfUser' column — one of them must be the U18 CSV.");
            else if (adultsFileIsU18 && !u18FileIsU18)
                // User has them the wrong way round — swap silently, treating them by their detected type.
                (parsedAdults, parsedU18) = (parsedU18, parsedAdults);
        }

        if (preview.Errors.Count > 0) return preview;

        var allRows = parsedAdults.Concat(parsedU18).ToList();
        preview.Rows = allRows;
        preview.AdultCount = allRows.Count(r => !r.IsU18);
        preview.U18Count = allRows.Count(r => r.IsU18);

        preview.UnrecognisedGenders = allRows
            .Where(r => r.NormalisedGender != "Male" && r.NormalisedGender != "Female")
            .Select(r => $"row {r.SourceRowNumber} of {r.SourceFile}: gender '{r.RawGender}'")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        // Duplicate detection: same (normalised name + gender) across all rows.
        var byKey = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var r in allRows)
        {
            var key = $"{r.Forename.Trim().ToLowerInvariant()}|{r.Surname.Trim().ToLowerInvariant()}|{r.NormalisedGender}";
            byKey[key] = byKey.GetValueOrDefault(key) + 1;
        }
        preview.Duplicates = byKey
            .Where(kv => kv.Value > 1)
            .Select(kv =>
            {
                var parts = kv.Key.Split('|');
                return $"{Excel.Proper(parts[0])} {Excel.Proper(parts[1])} ({parts[2]}) — appears {kv.Value} times";
            })
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        // Fuzzy match every distinct club and split into confident / unresolved.
        var canonicalClubs = _clubs.GetClubs(includeInactive: false);
        var distinctClubs = allRows
            .Select(r => r.RawClub?.Trim() ?? string.Empty)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var occurrences = allRows
            .Where(r => !string.IsNullOrWhiteSpace(r.RawClub))
            .GroupBy(r => r.RawClub.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var raw in distinctClubs.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
        {
            var match = ClubMatcher.Match(raw, canonicalClubs);
            if (match.IsConfident)
            {
                preview.ConfidentlyMatchedClubs.Add($"{raw} → {match.ConfidentMatch!.Name}");
            }
            else
            {
                preview.UnresolvedClubs.Add(new UnresolvedClub
                {
                    Raw = raw,
                    OccurrenceCount = occurrences[raw],
                    Suggestions = match.Candidates.Select(c => new ClubSuggestion
                    {
                        ClubId = c.Club.Id,
                        Name = c.Club.Name,
                        Score = c.Score
                    }).ToList()
                });
            }
        }

        return preview;
    }

    // ----- Generate -----

    public OnlineRegistrationGenerateResult Generate(OnlineRegistrationGenerateInput input)
    {
        var result = new OnlineRegistrationGenerateResult();
        OnlineRegistrationPreview? preview;
        try
        {
            preview = System.Text.Json.JsonSerializer.Deserialize<OnlineRegistrationPreview>(input.PreviewJson);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Could not read preview: {ex.Message}");
            return result;
        }
        if (preview is null || preview.EventId != input.EventId)
        {
            result.Errors.Add("Preview data is missing or doesn't match the target event.");
            return result;
        }
        if (!preview.CanGenerate)
        {
            result.Errors.Add("Preview has unresolved errors; cannot generate.");
            return result;
        }

        // Resolve every unresolved club. The organiser must have supplied a resolution for each,
        // in one of three forms: "club:{id}", "add:{name}", or "leave".
        var canonical = _clubs.GetClubs(includeInactive: true).ToDictionary(c => c.Id);
        var resolvedByRaw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Pre-load confidently-matched clubs so their raw string resolves to the canonical name.
        foreach (var line in preview.ConfidentlyMatchedClubs)
        {
            var arrow = line.IndexOf(" → ", StringComparison.Ordinal);
            if (arrow > 0) resolvedByRaw[line[..arrow]] = line[(arrow + 3)..];
        }

        foreach (var unresolved in preview.UnresolvedClubs)
        {
            if (!input.ClubResolutions.TryGetValue(unresolved.Raw, out var choice) || string.IsNullOrWhiteSpace(choice))
            {
                result.Errors.Add($"No resolution picked for club '{unresolved.Raw}'.");
                continue;
            }
            if (choice == "leave")
            {
                resolvedByRaw[unresolved.Raw] = unresolved.Raw;
            }
            else if (choice.StartsWith("club:", StringComparison.Ordinal)
                && int.TryParse(choice[5..], out var clubId)
                && canonical.TryGetValue(clubId, out var club))
            {
                resolvedByRaw[unresolved.Raw] = club.Name;
            }
            else if (choice.StartsWith("add:", StringComparison.Ordinal))
            {
                var newName = choice[4..].Trim();
                if (string.IsNullOrWhiteSpace(newName))
                {
                    result.Errors.Add($"'Add as new club' for '{unresolved.Raw}' needs a name.");
                    continue;
                }
                // Persist so a subsequent run confidently matches without prompting (AC10).
                var addResult = _clubs.Add(newName);
                if (!addResult.Success)
                {
                    // If it was already there (added under a different resolution above, or race), reuse it.
                    var existing = _clubs.GetClubs().FirstOrDefault(c =>
                        string.Equals(c.Name, newName, StringComparison.OrdinalIgnoreCase));
                    if (existing is null)
                    {
                        result.Errors.AddRange(addResult.Errors);
                        continue;
                    }
                    resolvedByRaw[unresolved.Raw] = existing.Name;
                }
                else
                {
                    resolvedByRaw[unresolved.Raw] = newName;
                }
            }
            else
            {
                result.Errors.Add($"Unknown resolution '{choice}' for club '{unresolved.Raw}'.");
            }
        }

        if (result.Errors.Count > 0) return result;

        // Build the output rows: PROPER-cased name, resolved club, alphabetical sort.
        var outputRows = preview.Rows
            .Select(r => new
            {
                Name = $"{Excel.Proper(r.Forename)} {Excel.Proper(r.Surname)}".Trim(),
                Surname = Excel.Proper(r.Surname),
                Forename = Excel.Proper(r.Forename),
                Club = string.IsNullOrWhiteSpace(r.RawClub)
                    ? string.Empty
                    : (resolvedByRaw.TryGetValue(r.RawClub.Trim(), out var resolved) ? resolved : r.RawClub.Trim()),
                Gender = r.NormalisedGender,
                Age = r.IsU18 ? r.Age : null,
                r.IsU18,
            })
            .OrderBy(r => r.Surname, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Forename, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Write the .xlsx to bytes.
        using var wb = new XLWorkbook();
        var sheet = wb.Worksheets.Add("Online Registration");

        sheet.Cell(1, 1).Value = "Race #";
        sheet.Cell(1, 2).Value = "Name";
        sheet.Cell(1, 3).Value = "Club Name";
        sheet.Cell(1, 4).Value = "M/F";
        sheet.Cell(1, 5).Value = "Age";
        sheet.Cell(1, 6).Value = "Comments";
        var header = sheet.Range(1, 1, 1, 6);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.Black;
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        header.Style.Alignment.WrapText = true;
        sheet.Row(1).Height = 24;

        for (var i = 0; i < outputRows.Count; i++)
        {
            var row = i + 2;
            var r = outputRows[i];
            // Race # (A) intentionally blank.
            sheet.Cell(row, 2).Value = r.Name;
            sheet.Cell(row, 3).Value = r.Club;
            sheet.Cell(row, 4).Value = r.Gender;

            var ageCell = sheet.Cell(row, 5);
            if (r.IsU18 && r.Age.HasValue)
            {
                ageCell.Value = r.Age.Value;
                var fill = r.Gender == "Female"
                    ? "#F2DCDB"  // Office Accent 2 (C0504D) @ 80% tint — light pink
                    : "#DCE6F2"; // Office Accent 1 (4F81BD) @ 80% tint — light blue
                ageCell.Style.Fill.BackgroundColor = XLColor.FromHtml(fill);
                ageCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
            // Age blank for adults (leave cell untouched).

            // Comments (F) intentionally blank.
        }

        // Thin cell borders on the whole populated range (header + data), so blank Race # / Age /
        // Comments cells still render as visible grid squares.
        if (outputRows.Count > 0)
        {
            var body = sheet.Range(1, 1, outputRows.Count + 1, 6);
            body.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            body.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Column(1).Width = 10;
        sheet.Column(2).Width = 26;
        sheet.Column(3).Width = 32;
        sheet.Column(4).Width = 10;
        sheet.Column(5).Width = 8;
        sheet.Column(6).Width = 24;
        sheet.Column(4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Column(5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Column(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        result.Bytes = ms.ToArray();
        result.FileName = $"{EventSlug(preview.EventName, preview.EventDate)}-online-registration.xlsx";
        result.Success = true;

        _logger.LogInformation(
            "US45 online-registration generation: event {EventId} produced {Rows} row(s) ({Adults} adult + {U18} U18).",
            preview.EventId, outputRows.Count, preview.AdultCount, preview.U18Count);
        return result;
    }

    // ----- CSV parsing -----

    /// <summary>Parse a registration-platform CSV. Sets <paramref name="isU18File"/> based on whether
    /// the <c>U18IsDependantOfUser</c> column is present in the header row.</summary>
    private static List<OnlineRegistrationRow> ParseCsv(Stream csvStream, string label, List<string> errors, out bool isU18File)
    {
        var rows = new List<OnlineRegistrationRow>();
        isU18File = false;

        // The registration platform exports UTF-8 with a BOM; StreamReader detects it automatically.
        using var reader = new StreamReader(csvStream);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true, MissingFieldFound = null, HeaderValidated = null };
        using var csv = new CsvReader(reader, config);

        if (!csv.Read())
        {
            errors.Add($"{label}: file is empty.");
            return rows;
        }
        if (!csv.ReadHeader())
        {
            errors.Add($"{label}: could not read the header row.");
            return rows;
        }

        var headerMap = BuildHeaderMap(csv.HeaderRecord ?? Array.Empty<string>());
        isU18File = headerMap.ContainsKey(U18MarkerColumn);

        var required = new[] { "forename", "surname", "gender", "club" };
        foreach (var col in required)
        {
            if (!headerMap.ContainsKey(col))
            {
                errors.Add($"{label}: missing required column '{col}'.");
            }
        }
        if (isU18File && !headerMap.ContainsKey("age_on_race_day"))
        {
            errors.Add($"{label}: U18 file is missing the 'Age_on_Race_Day' column.");
        }
        if (errors.Any(e => e.StartsWith(label + ":"))) return rows;

        var rowNumber = 1; // header is row 1
        while (csv.Read())
        {
            rowNumber++;
            var forename = TryGet(csv, headerMap, "forename").Trim();
            var surname = TryGet(csv, headerMap, "surname").Trim();
            var gender = TryGet(csv, headerMap, "gender").Trim();
            var club = TryGet(csv, headerMap, "club").Trim();
            var ageRaw = isU18File ? TryGet(csv, headerMap, "age_on_race_day").Trim() : string.Empty;

            if (string.IsNullOrWhiteSpace(forename) && string.IsNullOrWhiteSpace(surname))
                continue;

            int? age = null;
            if (isU18File && !string.IsNullOrWhiteSpace(ageRaw))
            {
                if (int.TryParse(ageRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                    age = parsed;
                else
                    errors.Add($"{label} row {rowNumber}: age '{ageRaw}' isn't a whole number.");
            }

            rows.Add(new OnlineRegistrationRow
            {
                SourceFile = label,
                SourceRowNumber = rowNumber,
                IsU18 = isU18File,
                Forename = forename,
                Surname = surname,
                RawGender = gender,
                NormalisedGender = NormaliseGender(gender),
                RawClub = club,
                Age = age,
            });
        }

        return rows;
    }

    private static Dictionary<string, int> BuildHeaderMap(string[] headers)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < headers.Length; i++)
        {
            var key = NormaliseHeader(headers[i]);
            if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key)) map[key] = i;
        }
        return map;
    }

    private static string NormaliseHeader(string header)
    {
        if (string.IsNullOrWhiteSpace(header)) return string.Empty;
        var sb = new StringBuilder(header.Length);
        foreach (var c in header.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (c == '_') sb.Append('_'); // preserve for Age_on_Race_Day → age_on_race_day
        }
        return sb.ToString();
    }

    private static string TryGet(CsvReader csv, Dictionary<string, int> map, string key)
    {
        if (!map.TryGetValue(key, out var idx)) return string.Empty;
        return csv.GetField(idx) ?? string.Empty;
    }

    private static string NormaliseGender(string raw)
    {
        var trimmed = (raw ?? string.Empty).Trim();
        if (trimmed.StartsWith("f", StringComparison.OrdinalIgnoreCase)) return "Female";
        if (trimmed.StartsWith("m", StringComparison.OrdinalIgnoreCase)) return "Male";
        return trimmed;
    }

    private static string EventSlug(string eventName, DateTime eventDate)
    {
        var sb = new StringBuilder();
        foreach (var c in eventName.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        var name = sb.ToString().Trim('-');
        return string.IsNullOrEmpty(name)
            ? $"event-{eventDate:yyyy-MM-dd}"
            : $"{name}-{eventDate:yyyy-MM-dd}";
    }
}

/// <summary>Excel-compatible helpers used by the Online Registration generator (US45).</summary>
public static class Excel
{
    /// <summary>Reproduces Excel's PROPER function: uppercase the first letter of every word, where a
    /// "word" starts after any non-letter character (space, apostrophe, hyphen). Rest is lowercase.</summary>
    public static string Proper(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        var boundary = true;
        foreach (var c in s)
        {
            if (char.IsLetter(c))
            {
                sb.Append(boundary ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
                boundary = false;
            }
            else
            {
                sb.Append(c);
                boundary = true;
            }
        }
        return sb.ToString();
    }
}
