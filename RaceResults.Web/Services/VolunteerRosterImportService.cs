using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RaceResults.Web.Data;
using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

public class VolunteerRosterImportService : IVolunteerRosterImportService
{
    private readonly IDbContextFactory<RaceResultsDbContext> _dbContextFactory;
    private readonly ILogger<VolunteerRosterImportService> _logger;

    // C2C: "Marshal (5a)" → "Marshal Point 5a". Captures one or more digits, optional trailing letters.
    private static readonly Regex MarshalAlias =
        new(@"^marshal\s*\(\s*(\d+[a-z]?)\s*\)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public VolunteerRosterImportService(
        IDbContextFactory<RaceResultsDbContext> dbContextFactory,
        ILogger<VolunteerRosterImportService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public RosterImportPreview BuildPreview(int eventId, Stream xlsxStream)
    {
        var preview = new RosterImportPreview { EventId = eventId };

        using var db = _dbContextFactory.CreateDbContext();
        var raceEvent = db.Events.FirstOrDefault(e => e.Id == eventId);
        if (raceEvent is null)
        {
            preview.Errors.Add("Event not found.");
            return preview;
        }
        preview.EventName = raceEvent.EventName;
        preview.EventType = raceEvent.EventType;
        preview.RosterAlreadyHasAssignments = db.VolunteerAssignments.Any(a => a.EventId == eventId);

        List<RosterImportParsedAssignment> parsed;
        try
        {
            parsed = ParseWorkbook(xlsxStream);
        }
        catch (Exception ex)
        {
            preview.Errors.Add($"Could not read spreadsheet: {ex.Message}");
            return preview;
        }
        if (parsed.Count == 0)
        {
            preview.Errors.Add("The spreadsheet has no volunteer rows.");
            return preview;
        }

        // GroupBy + First tolerates duplicate names already in the register (e.g. two Michael Gould records);
        // the import doesn't try to disambiguate, it just reuses whichever record it sees first.
        var existingVolunteers = db.Volunteers
            .ToList()
            .GroupBy(v => NormalizeName(v.Name), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var roles = db.VolunteerRoles
            .Where(r => r.EventType == raceEvent.EventType && r.IsActive)
            .ToList();
        var rolesByName = roles
            .GroupBy(r => NormalizeRoleName(r.Name), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var newByKey = new Dictionary<string, RosterImportNewVolunteer>(StringComparer.Ordinal);
        var matchedByKey = new Dictionary<string, RosterImportMatchedVolunteer>(StringComparer.Ordinal);
        var unmatchedRoles = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in parsed)
        {
            var resolvedRoleName = ResolveRoleAlias(row.RawRoleName);
            var note = row.Note;

            // "First Aid and Prizes" + "(course)" lists the on-course first aider on the same row
            // as the finish first aider; reroute them to "First Aid On Course" and drop the now-redundant note.
            // "(finish)" just confirms the default role for that row, so we drop the note too.
            if (raceEvent.EventType == EventType.CrownToCrown
                && resolvedRoleName.Equals("First Aid and Prizes", StringComparison.OrdinalIgnoreCase)
                && note is not null)
            {
                if (note.Equals("course", StringComparison.OrdinalIgnoreCase))
                {
                    resolvedRoleName = "First Aid On Course";
                    note = null;
                }
                else if (note.Equals("finish", StringComparison.OrdinalIgnoreCase))
                {
                    note = null;
                }
            }

            var roleKey = NormalizeRoleName(resolvedRoleName);
            if (!rolesByName.TryGetValue(roleKey, out var role))
            {
                unmatchedRoles.Add(row.RawRoleName.Trim());
                continue;
            }

            var displayName = CollapseWhitespace(row.VolunteerName);
            if (string.IsNullOrWhiteSpace(displayName)) continue;
            var nameKey = NormalizeName(displayName);

            if (existingVolunteers.TryGetValue(nameKey, out var existing))
            {
                if (!matchedByKey.ContainsKey(nameKey))
                {
                    matchedByKey[nameKey] = new RosterImportMatchedVolunteer { VolunteerId = existing.Id, Name = existing.Name };
                }
            }
            else if (!newByKey.ContainsKey(nameKey))
            {
                newByKey[nameKey] = new RosterImportNewVolunteer { NameKey = nameKey, Name = displayName };
            }

            preview.Assignments.Add(new RosterImportAssignmentPlan
            {
                VolunteerNameKey = nameKey,
                VolunteerDisplayName = displayName,
                RoleId = role.Id,
                RoleName = role.Name,
                WillRunAfter = row.WillRunAfter,
                Note = note
            });
        }

        preview.NewVolunteers = newByKey.Values.OrderBy(v => v.Name).ToList();
        preview.MatchedVolunteers = matchedByKey.Values.OrderBy(v => v.Name).ToList();
        preview.UnmatchedRoles = unmatchedRoles.OrderBy(r => r).ToList();
        return preview;
    }

    public async Task<OperationResult> CommitAsync(RosterImportCommitInput input)
    {
        RosterImportPreview? preview;
        try
        {
            preview = System.Text.Json.JsonSerializer.Deserialize<RosterImportPreview>(input.PreviewJson);
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(new[] { $"Could not read import preview: {ex.Message}" });
        }
        if (preview is null || preview.EventId != input.EventId)
            return OperationResult.Fail(new[] { "Import preview is missing or doesn't match the target event." });

        await using var db = _dbContextFactory.CreateDbContext();

        if (db.VolunteerAssignments.Any(a => a.EventId == input.EventId))
            return OperationResult.Fail(new[] { "The event already has roster assignments. Clear the roster first." });

        var existing = db.Volunteers
            .ToList()
            .GroupBy(v => NormalizeName(v.Name), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var idByKey = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var m in preview.MatchedVolunteers) idByKey[NormalizeName(m.Name)] = m.VolunteerId;

        var createdCount = 0;
        foreach (var n in preview.NewVolunteers)
        {
            if (existing.TryGetValue(n.NameKey, out var alreadyThere))
            {
                idByKey[n.NameKey] = alreadyThere.Id;
                continue;
            }
            var gender = input.NewVolunteerGenders.TryGetValue(n.NameKey, out var g) ? (g ?? string.Empty).Trim() : string.Empty;
            var volunteer = new Volunteer
            {
                Name = n.Name,
                Gender = gender,
                IsClubMember = true,
                IsFirstAidTrained = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Volunteers.Add(volunteer);
            await db.SaveChangesAsync();
            idByKey[n.NameKey] = volunteer.Id;
            createdCount++;
        }

        var assignmentCount = 0;
        foreach (var a in preview.Assignments)
        {
            if (!idByKey.TryGetValue(a.VolunteerNameKey, out var volunteerId)) continue;
            db.VolunteerAssignments.Add(new VolunteerAssignment
            {
                EventId = input.EventId,
                VolunteerId = volunteerId,
                VolunteerRoleId = a.RoleId,
                WillRunAfter = a.WillRunAfter,
                Note = string.IsNullOrWhiteSpace(a.Note) ? null : a.Note
            });
            assignmentCount++;
        }
        await db.SaveChangesAsync();

        _logger.LogInformation(
            "US35 import: event {EventId} got {Created} new volunteer(s), {Reused} reused, {Assignments} assignment(s).",
            input.EventId, createdCount, preview.MatchedVolunteers.Count, assignmentCount);

        var result = OperationResult.Ok(
            $"Imported {assignmentCount} assignment(s): {createdCount} new volunteer(s), {preview.MatchedVolunteers.Count} reused.");
        if (preview.UnmatchedRoles.Count > 0)
            result.Warnings.Add($"Unmatched role(s) skipped: {string.Join(", ", preview.UnmatchedRoles)}.");
        return result;
    }

    // ----- Parsing -----

    private static List<RosterImportParsedAssignment> ParseWorkbook(Stream xlsxStream)
    {
        var rows = new List<RosterImportParsedAssignment>();
        using var workbook = new XLWorkbook(xlsxStream);
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException("The workbook has no sheets.");

        var range = sheet.RangeUsed();
        if (range is null) return rows;

        // Detect header: if the first row is "Role" / "Volunteer(s)" (case-insensitive), skip it.
        var firstRow = range.FirstRow();
        var startRowNum = range.FirstRow().RowNumber();
        var roleHeader = firstRow.Cell(1).GetString().Trim();
        if (roleHeader.Equals("Role", StringComparison.OrdinalIgnoreCase))
            startRowNum++;

        for (int r = startRowNum; r <= range.LastRow().RowNumber(); r++)
        {
            var roleCell = sheet.Cell(r, 1).GetString();
            var volunteerCell = sheet.Cell(r, 2).GetString();
            if (string.IsNullOrWhiteSpace(roleCell)) continue;
            if (string.IsNullOrWhiteSpace(volunteerCell)) continue;

            // Multi-name cell: split on any newline variant.
            var names = volunteerCell.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawName in names)
            {
                var (name, willRun, note) = ExtractAnnotations(rawName);
                if (string.IsNullOrWhiteSpace(name)) continue;
                rows.Add(new RosterImportParsedAssignment
                {
                    RawRoleName = roleCell.Trim(),
                    RawVolunteerText = rawName.Trim(),
                    VolunteerName = name,
                    WillRunAfter = willRun,
                    Note = note
                });
            }
        }
        return rows;
    }

    /// <summary>
    /// Peel parenthesised annotations off the end of a cell line.
    /// "Sue Allen (to run)" → ("Sue Allen", willRun=true, note=null)
    /// "Nadine Baldwin (finish)" → ("Nadine Baldwin", willRun=false, note="finish")
    /// "Katie Darby (to run) (course)" → ("Katie Darby", willRun=true, note="course")
    /// </summary>
    public static (string name, bool willRunAfter, string? note) ExtractAnnotations(string raw)
    {
        var text = (raw ?? string.Empty).Trim();
        bool willRun = false;
        var notes = new List<string>();

        // Repeatedly strip a trailing (...).
        while (true)
        {
            if (!text.EndsWith(")")) break;
            int open = text.LastIndexOf('(');
            if (open <= 0) break;
            var inside = text.Substring(open + 1, text.Length - open - 2).Trim();
            if (inside.Equals("to run", StringComparison.OrdinalIgnoreCase))
                willRun = true;
            else if (!string.IsNullOrWhiteSpace(inside))
                notes.Insert(0, inside); // preserve original left-to-right order
            text = text.Substring(0, open).TrimEnd();
        }

        var note = notes.Count == 0 ? null : string.Join(", ", notes);
        return (CollapseWhitespace(text), willRun, note);
    }

    // ----- Role / name normalisation -----

    private static string ResolveRoleAlias(string raw)
    {
        var trimmed = (raw ?? string.Empty).Trim();
        var match = MarshalAlias.Match(trimmed);
        if (match.Success) return $"Marshal Point {match.Groups[1].Value.ToLowerInvariant()}";
        return trimmed;
    }

    private static string NormalizeRoleName(string s) => CollapseWhitespace(s).ToLowerInvariant();

    public static string NormalizeName(string s) => CollapseWhitespace(s).ToLowerInvariant();

    private static string CollapseWhitespace(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        bool inWs = false;
        foreach (var c in s.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                if (!inWs) { sb.Append(' '); inWs = true; }
            }
            else
            {
                sb.Append(c);
                inWs = false;
            }
        }
        return sb.ToString();
    }
}
