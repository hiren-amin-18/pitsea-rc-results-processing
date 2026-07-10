using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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
    private const string ArchivedMessage = "This event is archived. Unarchive it to make changes.";

    private readonly IDbContextFactory<RaceResultsDbContext> _dbContextFactory;
    private readonly ILogger<RaceResultsService> _logger;

    public RaceResultsService(IDbContextFactory<RaceResultsDbContext> dbContextFactory, ILogger<RaceResultsService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public RaceEvent? GetCurrentEvent()
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
        if (existingCurrent is not null)
        {
            existingCurrent.IsCurrent = false;
        }

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

        if (entity.IsArchived)
        {
            return OperationResult.Fail(new[] { ArchivedMessage });
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

        if (target.IsArchived)
        {
            return OperationResult.Fail(new[] { "Archived events cannot be set as current. Unarchive it first." });
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

        if (target.IsArchived)
        {
            return OperationResult.Fail(new[] { "Unarchive this event before deleting it." });
        }

        db.Entrants.Where(e => e.EventId == eventId).ExecuteDelete();
        db.FinishBibRecords.Where(r => r.EventId == eventId).ExecuteDelete();
        db.TimingRows.Where(t => t.EventId == eventId).ExecuteDelete();
        db.Events.Remove(target);
        db.SaveChanges();

        // Runners persist across events; mark those with no remaining entrants inactive (US15 AC7).
        RefreshRunnerActiveFlags(db);

        if (!db.Events.Any(e => e.IsCurrent))
        {
            // Promote the next-most-recent event if any remain; otherwise leave the DB empty
            // and let the app's empty-events state take over.
            var next = db.Events.Where(e => !e.IsArchived).OrderByDescending(e => e.EventDate).FirstOrDefault();
            if (next is not null)
            {
                next.IsCurrent = true;
                db.SaveChanges();
            }
        }

        return OperationResult.Ok("Event deleted and data reset for that event.");
    }

    public OperationResult ArchiveEvent(int eventId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var target = db.Events.FirstOrDefault(e => e.Id == eventId);
        if (target is null)
        {
            return OperationResult.Fail(new[] { "Event not found." });
        }

        if (target.IsArchived)
        {
            return OperationResult.Ok($"'{target.EventName}' is already archived.");
        }

        target.IsArchived = true;

        // An archived event cannot be current (US20 AC5): promote the most recent non-archived event.
        if (target.IsCurrent)
        {
            target.IsCurrent = false;
            var replacement = db.Events
                .Where(e => e.Id != eventId && !e.IsArchived)
                .OrderByDescending(e => e.EventDate)
                .FirstOrDefault();
            if (replacement is not null)
            {
                replacement.IsCurrent = true;
            }
        }

        db.SaveChanges();

        // If no non-archived event remained, leave the DB without a current event — the empty state covers it.

        _logger.LogInformation("Archived event {EventId} ('{Name}').", eventId, target.EventName);
        return OperationResult.Ok($"'{target.EventName}' archived and is now read-only.");
    }

    public OperationResult UnarchiveEvent(int eventId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var target = db.Events.FirstOrDefault(e => e.Id == eventId);
        if (target is null)
        {
            return OperationResult.Fail(new[] { "Event not found." });
        }

        if (!target.IsArchived)
        {
            return OperationResult.Ok($"'{target.EventName}' is not archived.");
        }

        target.IsArchived = false;
        db.SaveChanges();

        _logger.LogInformation("Unarchived event {EventId} ('{Name}').", eventId, target.EventName);
        return OperationResult.Ok($"'{target.EventName}' unarchived and editable again.");
    }

    public OperationResult PublishEvent(int eventId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var target = db.Events.FirstOrDefault(e => e.Id == eventId);
        if (target is null)
        {
            return OperationResult.Fail(new[] { "Event not found." });
        }

        target.IsPublished = true;
        if (string.IsNullOrEmpty(target.PublicToken))
        {
            target.PublicToken = Guid.NewGuid().ToString("N");
        }
        db.SaveChanges();

        _logger.LogInformation("Published event {EventId} ('{Name}').", eventId, target.EventName);
        return OperationResult.Ok($"'{target.EventName}' is now public.");
    }

    public OperationResult UnpublishEvent(int eventId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var target = db.Events.FirstOrDefault(e => e.Id == eventId);
        if (target is null)
        {
            return OperationResult.Fail(new[] { "Event not found." });
        }

        target.IsPublished = false;
        db.SaveChanges();

        _logger.LogInformation("Unpublished event {EventId} ('{Name}').", eventId, target.EventName);
        return OperationResult.Ok($"'{target.EventName}' is no longer public.");
    }

    public RaceEvent? GetPublishedEventByToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        using var db = _dbContextFactory.CreateDbContext();
        return db.Events.FirstOrDefault(e => e.IsPublished && e.PublicToken == token);
    }

    public RaceStatusCounts GetStatusCounts()
    {
        using var db = _dbContextFactory.CreateDbContext();
        var current = EnsureCurrentEvent(db);
        if (current is null)
        {
            return new RaceStatusCounts(0, 0, 0);
        }
        var currentEventId = current.Id;
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

        // Resolve current event up-front so the parser can dispatch on EventType (US33).
        await using (var lookupDb = await _dbContextFactory.CreateDbContextAsync())
        {
            var lookupEvent = await EnsureCurrentEventAsync(lookupDb);
            if (lookupEvent is null)
            {
                return OperationResult.Fail(new[] { "Create an event first before uploading entrants." });
            }
            if (lookupEvent.IsArchived)
            {
                return OperationResult.Fail(new[] { ArchivedMessage });
            }

            foreach (var file in uploaded)
            {
                if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"{file.FileName}: only .xlsx files are supported for entrants.");
                    continue;
                }

                await using var stream = file.OpenReadStream();
                if (lookupEvent.EventType == EventType.Bluebell5)
                {
                    ParseEntrantsWorkbookBluebell(stream, file.FileName, parsedEntrants, errors);
                }
                else
                {
                    ParseEntrantsWorkbookCrownToCrown(stream, file.FileName, parsedEntrants, errors);
                }
            }
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
        if (currentEvent is null)
        {
            return OperationResult.Fail(new[] { "Create an event first before uploading entrants." });
        }
        if (currentEvent.IsArchived)
        {
            return OperationResult.Fail(new[] { ArchivedMessage });
        }
        await db.TimingRows.Where(t => t.EventId == currentEvent.Id).ExecuteDeleteAsync();
        await db.FinishBibRecords.Where(r => r.EventId == currentEvent.Id).ExecuteDeleteAsync();
        await db.Entrants.Where(e => e.EventId == currentEvent.Id).ExecuteDeleteAsync();

        foreach (var entrant in deduped)
        {
            entrant.EventId = currentEvent.Id;
        }

        // Link each entrant to a persistent runner, creating runners as needed (US15).
        var runnerWarnings = await LinkEntrantsToRunnersAsync(db, deduped);

        db.Entrants.AddRange(deduped);
        await db.SaveChangesAsync();

        _logger.LogInformation("Entrants uploaded: {Count} entrants from {FileCount} file(s).", deduped.Count, uploaded.Count);
        var result = OperationResult.Ok($"Loaded {deduped.Count} entrants from {uploaded.Count} file(s).", "Finish bib and timing data were reset.");
        if (duplicateNames.Count > 0)
        {
            result.Warnings.Add($"Duplicate entrant names detected (please verify): {string.Join(", ", duplicateNames)}");
        }
        result.Warnings.AddRange(runnerWarnings);
        return result;
    }

    /// <summary>
    /// Links each entrant to a persistent runner (US15 AC3): exact normalised name+club matches link to the
    /// existing runner; otherwise a new runner is created. Likely duplicates (same name/different club, or a small
    /// typo) against runners that existed before this upload are returned as assistive warnings for the organiser.
    /// </summary>
    private static async Task<List<string>> LinkEntrantsToRunnersAsync(RaceResultsDbContext db, List<Entrant> entrants)
    {
        var warnings = new List<string>();

        // Runners that existed before this upload — only these are candidates for near-match warnings,
        // so runners created within this same batch don't warn against each other.
        var preExisting = await db.Runners.ToListAsync();
        var byKey = preExisting.ToDictionary(r => RunnerIdentity.NormalizeKey(r.Name, r.Club));

        foreach (var entrant in entrants)
        {
            var key = RunnerIdentity.NormalizeKey(entrant.Name, entrant.Club);
            if (byKey.TryGetValue(key, out var existing))
            {
                entrant.Runner = existing;
                continue;
            }

            var entrantName = RunnerIdentity.NormalizeName(entrant.Name);
            var near = preExisting.FirstOrDefault(r =>
            {
                var runnerName = RunnerIdentity.NormalizeName(r.Name);
                if (runnerName == entrantName)
                {
                    return true; // same name, different club
                }

                return entrantName.Length >= 4 && RunnerIdentity.Levenshtein(runnerName, entrantName) <= 2;
            });

            if (near is not null)
            {
                warnings.Add(
                    $"'{entrant.Name}' ({ClubLabel(entrant.Club)}) looks similar to existing runner " +
                    $"'{near.Name}' ({ClubLabel(near.Club)}) — created as a new runner; merge them under Runners if they are the same person.");
            }

            var runner = new Runner
            {
                Name = entrant.Name,
                Club = entrant.Club,
                Gender = entrant.Gender,
                Age = entrant.Age,
                IsActive = true
            };
            db.Runners.Add(runner);
            byKey[key] = runner;
            entrant.Runner = runner;
        }

        return warnings;
    }

    private static string ClubLabel(string? club) => string.IsNullOrWhiteSpace(club) ? "Unaffiliated" : club!;

    /// <summary>Returns a failure result if the given event is archived (read-only), otherwise null (US20).</summary>
    private static OperationResult? RejectIfArchived(RaceResultsDbContext db, int eventId)
    {
        var raceEvent = db.Events.FirstOrDefault(e => e.Id == eventId);
        return raceEvent is { IsArchived: true } ? OperationResult.Fail(new[] { ArchivedMessage }) : null;
    }

    /// <summary>Recomputes each runner's active flag: active iff at least one entrant still links to them (US15 AC7).</summary>
    private static void RefreshRunnerActiveFlags(RaceResultsDbContext db)
    {
        var activeRunnerIds = db.Entrants
            .Where(e => e.RunnerId != null)
            .Select(e => e.RunnerId!.Value)
            .Distinct()
            .ToHashSet();

        var changed = false;
        foreach (var runner in db.Runners.ToList())
        {
            var shouldBeActive = activeRunnerIds.Contains(runner.Id);
            if (runner.IsActive != shouldBeActive)
            {
                runner.IsActive = shouldBeActive;
                changed = true;
            }
        }

        if (changed)
        {
            db.SaveChanges();
        }
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
        if (currentEvent is null)
        {
            return OperationResult.Fail(new[] { "Create an event first before uploading finish positions." });
        }
        if (currentEvent.IsArchived)
        {
            return OperationResult.Fail(new[] { ArchivedMessage });
        }
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
        if (currentEvent is null)
        {
            return OperationResult.Fail(new[] { "Create an event first before uploading timings." });
        }
        if (currentEvent.IsArchived)
        {
            return OperationResult.Fail(new[] { ArchivedMessage });
        }
        if (!await db.FinishBibRecords.AnyAsync(r => r.EventId == currentEvent.Id))
        {
            return OperationResult.Fail(new[] { "Upload finish position and bib data before timings." });
        }

        var errors = new List<string>();
        var rows = new Dictionary<int, ParsedTiming>();

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
            Time = kvp.Value.Raw,
            DurationTicks = kvp.Value.Duration.Ticks
        }));
        await db.SaveChangesAsync();

        _logger.LogInformation("Timings uploaded: {Count} rows.", rows.Count);
        var result = OperationResult.Ok($"Loaded {rows.Count} timing rows.");

        // A finisher whose time is earlier than someone who placed ahead is almost always a
        // data-entry or chip error. Warn but don't block (US17 AC4).
        var outOfOrder = FindOutOfOrderPositions(rows);
        if (outOfOrder.Count > 0)
        {
            result.Warnings.Add(
                $"Times are not in finishing order at position(s): {string.Join(", ", outOfOrder)}. " +
                "A later finisher has an earlier time than someone ahead — please review.");
            _logger.LogWarning("UploadTimings: out-of-order times at positions {Positions}", string.Join(", ", outOfOrder));
        }

        return result;
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
        var current = EnsureCurrentEvent(db);
        return current is null ? Array.Empty<ResultRecord>() : GetCollatedResultsForEvent(db, current.Id);
    }

    public IReadOnlyList<ResultRecord> GetCollatedResults(int eventId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        return GetCollatedResultsForEvent(db, eventId);
    }

    private static IReadOnlyList<ResultRecord> GetCollatedResultsForEvent(RaceResultsDbContext db, int eventId)
    {
        // Disqualified finishers are removed and the remaining positions close up for display (US16 AC3).
        var finishers = BuildAllFinishRecords(db, eventId)
            .Where(r => r.Status != FinishStatus.Disqualified)
            .ToList();

        for (var i = 0; i < finishers.Count; i++)
        {
            finishers[i].DisplayPosition = i + 1;
        }

        return finishers;
    }

    private static List<ResultRecord> BuildAllFinishRecords(RaceResultsDbContext db, int eventId)
    {
        var entrantByBib = db.Entrants
            .Where(e => e.EventId == eventId)
            .ToDictionary(e => e.BibNumber, StringComparer.OrdinalIgnoreCase);
        var timings = db.TimingRows
            .Where(t => t.EventId == eventId)
            .ToList()
            .ToDictionary(t => t.Position);

        return db.FinishBibRecords
            .Where(r => r.EventId == eventId)
            .OrderBy(r => r.Position)
            .ToList()
            .Select(r =>
            {
                timings.TryGetValue(r.Position, out var timing);
                entrantByBib.TryGetValue(r.BibNumber, out var entrant);
                return new ResultRecord
                {
                    Position = r.Position,
                    BibNumber = r.BibNumber,
                    Duration = timing?.Duration,
                    // Canonical display when typed; fall back to raw text for unparseable legacy rows.
                    Time = timing is null
                        ? string.Empty
                        : timing.Duration is { } d ? RaceTime.Format(d) : timing.Time,
                    Entrant = entrant,
                    Status = entrant?.Status ?? FinishStatus.Finished,
                    StatusReason = entrant?.StatusReason
                };
            })
            .ToList();
    }

    public IReadOnlyList<ResultRecord> GetDsqResults()
    {
        var id = CurrentEventId();
        return id is null ? Array.Empty<ResultRecord>() : GetDsqResults(id.Value);
    }

    public IReadOnlyList<ResultRecord> GetDsqResults(int eventId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        return BuildAllFinishRecords(db, eventId)
            .Where(r => r.Status == FinishStatus.Disqualified)
            .ToList();
    }

    public IReadOnlyList<Entrant> GetDnfEntrants()
    {
        var id = CurrentEventId();
        return id is null ? Array.Empty<Entrant>() : GetDnfEntrants(id.Value);
    }

    public IReadOnlyList<Entrant> GetDnfEntrants(int eventId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var finishedBibs = db.FinishBibRecords.Where(x => x.EventId == eventId).Select(x => x.BibNumber);
        // Non-finishers default to DNF; those explicitly marked DNS are listed separately (US16).
        return db.Entrants
            .Where(e => e.EventId == eventId)
            .Where(e => !finishedBibs.Contains(e.BibNumber))
            .Where(e => e.Status != FinishStatus.DidNotStart)
            .OrderBy(e => e.BibNumber)
            .ToList();
    }

    public IReadOnlyList<Entrant> GetDnsEntrants()
    {
        var id = CurrentEventId();
        return id is null ? Array.Empty<Entrant>() : GetDnsEntrants(id.Value);
    }

    public IReadOnlyList<Entrant> GetDnsEntrants(int eventId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var finishedBibs = db.FinishBibRecords.Where(x => x.EventId == eventId).Select(x => x.BibNumber);
        return db.Entrants
            .Where(e => e.EventId == eventId)
            .Where(e => !finishedBibs.Contains(e.BibNumber))
            .Where(e => e.Status == FinishStatus.DidNotStart)
            .OrderBy(e => e.BibNumber)
            .ToList();
    }

    public RaceStats GetRaceStats()
    {
        var id = CurrentEventId();
        return id is null ? new RaceStats() : GetRaceStats(id.Value);
    }

    public RaceStats GetRaceStats(int eventId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        // DNS entrants never started, so they are excluded from race statistics totals (US16 AC4).
        var entrants = db.Entrants
            .Where(e => e.EventId == eventId && e.Status != FinishStatus.DidNotStart)
            .ToList();
        var males = entrants.Where(e => IsMale(e.Gender)).ToList();
        var females = entrants.Where(e => IsFemale(e.Gender)).ToList();

        return new RaceStats
        {
            TotalMales = males.Count,
            TotalFemales = females.Count,
            TotalMalesU18 = males.Count(e => e.IsU18),
            TotalFemalesU18 = females.Count(e => e.IsU18),
            TotalMalesUnaffiliatedExcludingU18 = males.Count(e => !e.IsU18 && e.IsUnaffiliated),
            TotalFemalesUnaffiliatedExcludingU18 = females.Count(e => !e.IsU18 && e.IsUnaffiliated),
            TotalMalesVet = males.Count(e => e.IsVet),
            TotalFemalesVet = females.Count(e => e.IsVet),
            TotalMalesNonVet = males.Count(e => !e.IsVet),
            TotalFemalesNonVet = females.Count(e => !e.IsVet)
        };
    }

    private int? CurrentEventId()
    {
        using var db = _dbContextFactory.CreateDbContext();
        return EnsureCurrentEvent(db)?.Id;
    }

    private RaceEvent? GetEventById(int eventId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        return db.Events.FirstOrDefault(e => e.Id == eventId) ?? EnsureCurrentEvent(db);
    }

    public RaceStatisticsSummary GetRaceStatisticsSummary()
    {
        var id = CurrentEventId();
        return id is null ? new RaceStatisticsSummary() : GetRaceStatisticsSummary(id.Value);
    }

    public RaceStatisticsSummary GetRaceStatisticsSummary(int eventId)
    {
        using var db = _dbContextFactory.CreateDbContext();

        var entrants = db.Entrants.Where(e => e.EventId == eventId).ToList();
        var dnsCount = entrants.Count(e => e.Status == FinishStatus.DidNotStart);
        var startedEntrants = entrants.Where(e => e.Status != FinishStatus.DidNotStart).ToList();

        var collated = GetCollatedResultsForEvent(db, eventId); // finishers, excludes DSQ
        var finisherCount = collated.Count;

        var finishedBibs = db.FinishBibRecords
            .Where(r => r.EventId == eventId)
            .Select(r => r.BibNumber)
            .ToList()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dnfCount = startedEntrants.Count(e => !finishedBibs.Contains(e.BibNumber));

        var entrantCount = startedEntrants.Count;
        var completionRate = entrantCount == 0 ? 0 : Math.Round(100.0 * finisherCount / entrantCount, 1);

        var maleFinishers = collated.Count(r => r.Entrant is not null && IsMale(r.Entrant.Gender));
        var femaleFinishers = collated.Count(r => r.Entrant is not null && IsFemale(r.Entrant.Gender));

        // Affiliation from the existing RaceStats convention (unaffiliated counts exclude U18).
        var males = startedEntrants.Where(e => IsMale(e.Gender)).ToList();
        var females = startedEntrants.Where(e => IsFemale(e.Gender)).ToList();
        var unaffiliated = males.Count(e => !e.IsU18 && e.IsUnaffiliated) + females.Count(e => !e.IsU18 && e.IsUnaffiliated);
        var affiliated = startedEntrants.Count - unaffiliated;

        var durations = collated
            .Where(r => r.Duration.HasValue)
            .Select(r => r.Duration!.Value)
            .OrderBy(d => d)
            .ToList();
        var excludedTimeRows = collated.Count(r => !r.Duration.HasValue);

        TimeSpan? winner = durations.Count > 0 ? durations[0] : null;
        TimeSpan? median = Percentile(durations, 50);
        TimeSpan? average = durations.Count > 0 ? TimeSpan.FromTicks((long)durations.Average(d => d.Ticks)) : null;
        TimeSpan? spread = winner.HasValue && median.HasValue ? median.Value - winner.Value : null;

        var perMinute = durations
            .GroupBy(d => (int)Math.Floor(d.TotalMinutes))
            .Select(g => new { Minute = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Minute)
            .ToList();
        var busiest = perMinute.FirstOrDefault();

        return new RaceStatisticsSummary
        {
            EntrantCount = entrantCount,
            FinisherCount = finisherCount,
            DnfCount = dnfCount,
            DnsCount = dnsCount,
            CompletionRatePercent = completionRate,
            MaleFinishers = maleFinishers,
            FemaleFinishers = femaleFinishers,
            MalePercent = finisherCount == 0 ? 0 : Math.Round(100.0 * maleFinishers / finisherCount, 1),
            FemalePercent = finisherCount == 0 ? 0 : Math.Round(100.0 * femaleFinishers / finisherCount, 1),
            AffiliatedCount = affiliated,
            UnaffiliatedCount = unaffiliated,
            HasTimes = durations.Count > 0,
            WinnerTime = winner,
            MedianTime = median,
            AverageTime = average,
            WinnerToMedianSpread = spread,
            Percentile25 = Percentile(durations, 25),
            Percentile50 = median,
            Percentile75 = Percentile(durations, 75),
            ExcludedTimeRowCount = excludedTimeRows,
            BusiestWindowMinute = busiest?.Minute,
            BusiestWindowCount = busiest?.Count ?? 0
        };
    }

    /// <summary>Nearest-rank percentile over an ascending-sorted list of durations.</summary>
    private static TimeSpan? Percentile(IReadOnlyList<TimeSpan> sortedAscending, double percentile)
    {
        if (sortedAscending.Count == 0)
        {
            return null;
        }

        var rank = (int)Math.Ceiling(percentile / 100.0 * sortedAscending.Count);
        rank = Math.Clamp(rank, 1, sortedAscending.Count);
        return sortedAscending[rank - 1];
    }

    public IReadOnlyList<TopTenCategory> GetTopTenByCategory()
    {
        var id = CurrentEventId();
        return id is null ? Array.Empty<TopTenCategory>() : GetTopTenByCategory(id.Value);
    }

    public IReadOnlyList<TopTenCategory> GetTopTenByCategory(int eventId)
    {
        var collated = GetCollatedResults(eventId);
        var eventType = GetEventById(eventId)?.EventType ?? EventType.CrownToCrown;
        return BuildTopTenFromCollated(collated, eventType);
    }

    private static IReadOnlyList<TopTenCategory> BuildTopTenFromCollated(IReadOnlyList<ResultRecord> collated, EventType eventType)
    {
        // Bluebell 5 has no U18 category; Vet (M40+, F35+) replaces Youth (US33 AC9).
        if (eventType == EventType.Bluebell5)
        {
            return new List<TopTenCategory>
            {
                BuildTopTen("Male", collated, e => IsMale(e.Gender)),
                BuildTopTen("Female", collated, e => IsFemale(e.Gender)),
                BuildTopTen("Vet Male", collated, e => IsMale(e.Gender) && e.IsVet),
                BuildTopTen("Vet Female", collated, e => IsFemale(e.Gender) && e.IsVet)
            };
        }

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
        var current = EnsureCurrentEvent(db);
        if (current is null)
        {
            editInput = new EditResultInput();
            return false;
        }
        var currentEventId = current.Id;
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
            Time = timing?.Time ?? string.Empty,
            IsVet = entrant?.IsVet ?? false,
            IsBluebell = current.EventType == EventType.Bluebell5
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

        // Validate the optional time with the same rules as upload (US17 AC6).
        TimeSpan? editDuration = null;
        if (!string.IsNullOrWhiteSpace(editInput.Time))
        {
            if (RaceTime.TryParse(editInput.Time, out var parsedEditTime))
            {
                editDuration = parsedEditTime;
            }
            else
            {
                errors.Add($"Time '{editInput.Time.Trim()}' is not valid. Use mm:ss or h:mm:ss.");
            }
        }

        using var db = _dbContextFactory.CreateDbContext();
        var currentForUpdate = EnsureCurrentEvent(db);
        if (currentForUpdate is null)
        {
            return OperationResult.Fail(new[] { "No current event." });
        }
        var currentEventId = currentForUpdate.Id;
        if (RejectIfArchived(db, currentEventId) is { } archived)
        {
            return archived;
        }
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
            var trimmedTime = editInput.Time.Trim();
            var durationTicks = editDuration?.Ticks;
            if (oldPosition != editInput.NewPosition && oldTiming is not null)
            {
                db.TimingRows.Remove(oldTiming);
            }
            var newTiming = db.TimingRows.FirstOrDefault(t => t.EventId == currentEventId && t.Position == editInput.NewPosition);
            if (newTiming is null)
            {
                db.TimingRows.Add(new TimingRow { EventId = currentEventId, Position = editInput.NewPosition, Time = trimmedTime, DurationTicks = durationTicks });
            }
            else
            {
                newTiming.Time = trimmedTime;
                newTiming.DurationTicks = durationTicks;
            }
        }
        else if (oldTiming is not null && oldPosition != editInput.NewPosition)
        {
            db.TimingRows.Remove(oldTiming);
            db.TimingRows.Add(new TimingRow { EventId = currentEventId, Position = editInput.NewPosition, Time = oldTiming.Time, DurationTicks = oldTiming.DurationTicks });
        }

        db.SaveChanges();
        return OperationResult.Ok("Result row updated successfully.");
    }

    public OperationResult DisqualifyResult(int position, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return OperationResult.Fail(new[] { "A reason is required to disqualify a finisher." });
        }

        using var db = _dbContextFactory.CreateDbContext();
        var currentForDsq = EnsureCurrentEvent(db);
        if (currentForDsq is null)
        {
            return OperationResult.Fail(new[] { "No current event." });
        }
        var currentEventId = currentForDsq.Id;
        if (RejectIfArchived(db, currentEventId) is { } archived)
        {
            return archived;
        }
        var row = db.FinishBibRecords.FirstOrDefault(r => r.EventId == currentEventId && r.Position == position);
        if (row is null)
        {
            return OperationResult.Fail(new[] { "Result row not found." });
        }

        var entrant = db.Entrants.FirstOrDefault(e => e.EventId == currentEventId && e.BibNumber.ToLower() == row.BibNumber.ToLower());
        if (entrant is null)
        {
            return OperationResult.Fail(new[] { "Cannot disqualify an unmatched bib — correct the entrant first." });
        }

        entrant.Status = FinishStatus.Disqualified;
        entrant.StatusReason = reason.Trim();
        entrant.StatusUpdatedAt = DateTime.UtcNow;
        db.SaveChanges();

        _logger.LogInformation("Disqualified position {Position} (bib {Bib}): {Reason}", position, row.BibNumber, reason.Trim());
        return OperationResult.Ok($"Disqualified {entrant.Name}. They have been removed from the results.");
    }

    public OperationResult ReinstateResult(int position)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var currentForReinstate = EnsureCurrentEvent(db);
        if (currentForReinstate is null)
        {
            return OperationResult.Fail(new[] { "No current event." });
        }
        var currentEventId = currentForReinstate.Id;
        if (RejectIfArchived(db, currentEventId) is { } archived)
        {
            return archived;
        }
        var row = db.FinishBibRecords.FirstOrDefault(r => r.EventId == currentEventId && r.Position == position);
        if (row is null)
        {
            return OperationResult.Fail(new[] { "Result row not found." });
        }

        var entrant = db.Entrants.FirstOrDefault(e => e.EventId == currentEventId && e.BibNumber.ToLower() == row.BibNumber.ToLower());
        if (entrant is null)
        {
            return OperationResult.Fail(new[] { "Entrant not found." });
        }

        entrant.Status = FinishStatus.Finished;
        entrant.StatusReason = null;
        entrant.StatusUpdatedAt = DateTime.UtcNow;
        db.SaveChanges();

        _logger.LogInformation("Reinstated position {Position} (bib {Bib}).", position, row.BibNumber);
        return OperationResult.Ok($"Reinstated {entrant.Name} to the results.");
    }

    public OperationResult SetNonFinisherStatus(string bibNumber, FinishStatus status)
    {
        if (status != FinishStatus.DidNotStart && status != FinishStatus.DidNotFinish)
        {
            return OperationResult.Fail(new[] { "Only DNS or DNF can be set for a non-finisher." });
        }

        using var db = _dbContextFactory.CreateDbContext();
        var currentForStatus = EnsureCurrentEvent(db);
        if (currentForStatus is null)
        {
            return OperationResult.Fail(new[] { "No current event." });
        }
        var currentEventId = currentForStatus.Id;
        if (RejectIfArchived(db, currentEventId) is { } archived)
        {
            return archived;
        }
        var bibLower = bibNumber.Trim().ToLower();
        var entrant = db.Entrants.FirstOrDefault(e => e.EventId == currentEventId && e.BibNumber.ToLower() == bibLower);
        if (entrant is null)
        {
            return OperationResult.Fail(new[] { "Entrant not found." });
        }

        if (db.FinishBibRecords.Any(r => r.EventId == currentEventId && r.BibNumber.ToLower() == bibLower))
        {
            return OperationResult.Fail(new[] { "This entrant has a finish position; use Disqualify instead." });
        }

        entrant.Status = status;
        entrant.StatusUpdatedAt = DateTime.UtcNow;
        db.SaveChanges();

        var label = status == FinishStatus.DidNotStart ? "DNS" : "DNF";
        _logger.LogInformation("Set entrant bib {Bib} to {Status}.", bibNumber, label);
        return OperationResult.Ok($"Marked {entrant.Name} as {label}.");
    }

    public byte[] GenerateResultsPdf() => GenerateResultsPdf(CurrentEventId() ?? throw new InvalidOperationException("No current event."));

    public byte[] GenerateResultsPdf(int eventId)
    {
        var collated = GetCollatedResults(eventId);
        var currentEvent = GetEventById(eventId) ?? throw new InvalidOperationException("Event not found.");
        var courseRecords = LoadCurrentCourseRecords(currentEvent.EventType);
        var logoBytes = TryLoadPdfLogo();

        var isBluebell = currentEvent.EventType == EventType.Bluebell5;

        // C2C winners (overall + youth). For Bluebell these slots are unused.
        var maleWinner = FindWinner(collated, e => IsMale(e.Gender) && !e.IsU18);
        var femaleWinner = FindWinner(collated, e => IsFemale(e.Gender) && !e.IsU18);
        var maleYouthWinner = FindWinner(collated, e => IsMale(e.Gender) && e.IsU18);
        var femaleYouthWinner = FindWinner(collated, e => IsFemale(e.Gender) && e.IsU18);

        // Bluebell winners — top 3 M/F by finish time, plus the first vet of each gender outside the top 3 (US33 AC6).
        var bluebellWinners = isBluebell
            ? BluebellWinnerSelection.Select(collated)
            : new BluebellWinners(Array.Empty<ResultRecord>(), Array.Empty<ResultRecord>(), null, null);

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

                    if (isBluebell)
                    {
                        column.Item().ShowOnce().Element(x => BuildPdfWinnersBlockBluebell(
                            x,
                            bluebellWinners.MaleTop3,
                            bluebellWinners.FemaleTop3,
                            bluebellWinners.VetMale,
                            bluebellWinners.VetFemale,
                            courseRecords,
                            currentEvent.Id));
                    }
                    else
                    {
                        column.Item().ShowOnce().Element(x => BuildPdfWinnersBlock(
                            x,
                            maleWinner,
                            femaleWinner,
                            maleYouthWinner,
                            femaleYouthWinner,
                            courseRecords,
                            currentEvent.Id));
                    }

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

                            table.Cell().Element(bodyStyle).AlignCenter().Text(row.DisplayPosition.ToString(CultureInfo.InvariantCulture));
                            table.Cell().Element(bodyStyle).AlignCenter().Text(row.Time);
                            table.Cell().Element(bodyStyle).AlignCenter().Text(row.BibNumber);
                            table.Cell().Element(bodyStyle).Text(ToPdfCellText(row.Name));
                            table.Cell().Element(bodyStyle).AlignCenter().Text(ToPdfCellText(row.Gender));
                            table.Cell().Element(bodyStyle).Text(ToPdfCellText(row.Club));
                        }
                    });

                    // Finishers only — DNF, DNS and DSQ entrants are intentionally omitted from the PDF.
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateResultsCsv() => GenerateResultsCsv(CurrentEventId() ?? throw new InvalidOperationException("No current event."));

    public byte[] GenerateResultsCsv(int eventId)
    {
        var collated = GetCollatedResults(eventId);

        using var memory = new MemoryStream();
        // UTF-8 with BOM so Excel opens accented names correctly (AC7).
        using (var writer = new StreamWriter(memory, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            foreach (var heading in new[] { "Position", "Time", "Bib", "Name", "Club", "Gender", "Age", "Status" })
            {
                csv.WriteField(heading);
            }
            csv.NextRecord();

            foreach (var row in collated)
            {
                csv.WriteField(row.DisplayPosition.ToString(CultureInfo.InvariantCulture));
                csv.WriteField(row.Time);
                csv.WriteField(row.BibNumber);
                csv.WriteField(row.Name);
                csv.WriteField(row.Club);
                csv.WriteField(row.Gender);
                csv.WriteField(row.Age?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                csv.WriteField(row.IsUnmatched ? "Unmatched" : "Finished");
                csv.NextRecord();
            }

            // Finishers only — DNF, DNS and DSQ entrants are intentionally omitted from the export.

            writer.Flush();
        }

        return memory.ToArray();
    }

    public string GetResultsCsvFileName() => $"{BuildEventSlug(GetCurrentEvent() ?? throw new InvalidOperationException("No current event."))}-results.csv";

    public string GetResultsCsvFileName(int eventId) => $"{BuildEventSlug(GetEventById(eventId) ?? throw new InvalidOperationException("Event not found."))}-results.csv";

    private static string BuildEventSlug(RaceEvent raceEvent)
    {
        var slug = Regex.Replace(raceEvent.EventName.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        if (string.IsNullOrEmpty(slug))
        {
            slug = "event";
        }

        return $"{slug}-{raceEvent.EventDate:yyyy-MM-dd}";
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

    private List<CourseRecord> LoadCurrentCourseRecords(EventType eventType)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var order = new[] { "Male", "Female", "Male U18", "Female U18" };
        return db.CourseRecords
            .Where(r => r.IsCurrent && r.EventType == eventType)
            .ToList()
            .OrderBy(r => Array.IndexOf(order, r.Category))
            .ToList();
    }

    private static void BuildPdfWinnersBlock(
        IContainer container,
        ResultRecord? maleWinner,
        ResultRecord? femaleWinner,
        ResultRecord? maleYouthWinner,
        ResultRecord? femaleYouthWinner,
        IReadOnlyList<CourseRecord> courseRecords,
        int currentEventId)
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

            // Course records are rendered from stored data for this event's type (US22); omit the line if none exist.
            if (courseRecords.Count > 0)
            {
                var parts = courseRecords.Select(r =>
                    $"{RaceTime.Format(r.Duration)} {r.RunnerName} ({r.EventDate:MMMM yyyy})");
                column.Item().PaddingTop(3).Text($"Course records - {string.Join("  ", parts)}")
                    .SemiBold()
                    .FontSize(11);

                // Records set at this event are celebrated (US22 AC5).
                var newRecords = courseRecords.Where(r => r.SourceEventId == currentEventId).ToList();
                if (newRecords.Count > 0)
                {
                    var newParts = newRecords.Select(r => $"{r.Category}: {RaceTime.Format(r.Duration)} {r.RunnerName}");
                    column.Item().PaddingTop(2).Text($"NEW COURSE RECORD — {string.Join("; ", newParts)}")
                        .Bold()
                        .FontSize(11)
                        .FontColor(Colors.Red.Darken2);
                }
            }
        });
    }

    /// <summary>Bluebell 5 winners block (US33 AC12) — 2 columns, 4 rows: 1st/2nd/3rd M&amp;F + 1st Vet M/F.</summary>
    private static void BuildPdfWinnersBlockBluebell(
        IContainer container,
        IReadOnlyList<ResultRecord> maleTop3,
        IReadOnlyList<ResultRecord> femaleTop3,
        ResultRecord? maleVetWinner,
        ResultRecord? femaleVetWinner,
        IReadOnlyList<CourseRecord> courseRecords,
        int currentEventId)
    {
        string MalePlace(int index) => index < maleTop3.Count ? WinnerText(maleTop3[index]) : "-";
        string FemalePlace(int index) => index < femaleTop3.Count ? WinnerText(femaleTop3[index]) : "-";

        container.PaddingTop(8).PaddingBottom(8).Column(column =>
        {
            column.Spacing(3);

            column.Item().Row(row =>
            {
                row.RelativeItem().Text($"1st Male = {MalePlace(0)}").FontSize(11);
                row.RelativeItem().AlignRight().Text($"1st Female = {FemalePlace(0)}").FontSize(11);
            });
            column.Item().Row(row =>
            {
                row.RelativeItem().Text($"2nd Male = {MalePlace(1)}").FontSize(11);
                row.RelativeItem().AlignRight().Text($"2nd Female = {FemalePlace(1)}").FontSize(11);
            });
            column.Item().Row(row =>
            {
                row.RelativeItem().Text($"3rd Male = {MalePlace(2)}").FontSize(11);
                row.RelativeItem().AlignRight().Text($"3rd Female = {FemalePlace(2)}").FontSize(11);
            });
            column.Item().Row(row =>
            {
                row.RelativeItem().Text($"1st Vet Male = {WinnerText(maleVetWinner)}").FontSize(11);
                row.RelativeItem().AlignRight().Text($"1st Vet Female = {WinnerText(femaleVetWinner)}").FontSize(11);
            });

            if (courseRecords.Count > 0)
            {
                var parts = courseRecords.Select(r =>
                    $"{RaceTime.Format(r.Duration)} {r.RunnerName} ({r.EventDate:MMMM yyyy})");
                column.Item().PaddingTop(3).Text($"Course records - {string.Join("  ", parts)}")
                    .SemiBold()
                    .FontSize(11);

                var newRecords = courseRecords.Where(r => r.SourceEventId == currentEventId).ToList();
                if (newRecords.Count > 0)
                {
                    var newParts = newRecords.Select(r => $"{r.Category}: {RaceTime.Format(r.Duration)} {r.RunnerName}");
                    column.Item().PaddingTop(2).Text($"NEW COURSE RECORD — {string.Join("; ", newParts)}")
                        .Bold()
                        .FontSize(11)
                        .FontColor(Colors.Red.Darken2);
                }
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

    private static async Task<RaceEvent?> EnsureCurrentEventAsync(RaceResultsDbContext db)
    {
        var current = await db.Events.FirstOrDefaultAsync(e => e.IsCurrent);
        if (current is not null)
        {
            return current;
        }

        // Promote-only: an archived event must never become current (US20); only a non-archived event can be promoted.
        // When no events exist at all, return null — the app's empty state takes over (no auto-seeding).
        var existing = await db.Events.Where(e => !e.IsArchived).OrderByDescending(e => e.EventDate).FirstOrDefaultAsync();
        if (existing is not null)
        {
            existing.IsCurrent = true;
            await db.SaveChangesAsync();
            return existing;
        }

        return null;
    }

    private static RaceEvent? EnsureCurrentEvent(RaceResultsDbContext db)
    {
        var current = db.Events.FirstOrDefault(e => e.IsCurrent);
        if (current is not null)
        {
            return current;
        }

        var existing = db.Events.Where(e => !e.IsArchived).OrderByDescending(e => e.EventDate).FirstOrDefault();
        if (existing is not null)
        {
            existing.IsCurrent = true;
            db.SaveChanges();
            return existing;
        }

        return null;
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

    private static void ParseEntrantsWorkbookCrownToCrown(Stream stream, string fileName, List<Entrant> entrants, List<string> errors)
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

    /// <summary>
    /// Bluebell 5 entry parser (US33). Expects an <c>Age</c> column whose value is either
    /// <c>Male U40</c>, <c>Female U35</c>, or blank. Blank means the runner is at/over the vet
    /// threshold for their gender (M40+, F35+) and is awarded <c>IsVet = true</c>. Any other value
    /// (e.g. <c>Male U18</c>) is rejected with a row-specific error.
    /// </summary>
    private static void ParseEntrantsWorkbookBluebell(Stream stream, string fileName, List<Entrant> entrants, List<string> errors)
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

            var normalisedGender = NormalizeGender(gender);
            var ageToken = (ageRaw ?? string.Empty).Trim();
            var ageKey = new string(ageToken.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();

            bool isVet;
            if (string.IsNullOrEmpty(ageKey))
            {
                // Blank = at/over the vet threshold for their gender.
                isVet = true;
            }
            else if (ageKey == "maleu40")
            {
                if (!IsMale(normalisedGender))
                {
                    errors.Add($"{fileName} row {rowNumber}: age '{ageRaw}' does not match gender '{gender}'.");
                    continue;
                }
                isVet = false;
            }
            else if (ageKey == "femaleu35")
            {
                if (!IsFemale(normalisedGender))
                {
                    errors.Add($"{fileName} row {rowNumber}: age '{ageRaw}' does not match gender '{gender}'.");
                    continue;
                }
                isVet = false;
            }
            else
            {
                // U18 or any other unsupported value: Bluebell does not have under-18 categories.
                errors.Add($"{fileName} row {rowNumber}: age '{ageRaw}' is not supported for Bluebell 5 (expected blank, 'Male U40', or 'Female U35').");
                continue;
            }

            entrants.Add(new Entrant
            {
                BibNumber = bib.Trim(),
                Name = name.Trim(),
                Club = club.Trim(),
                Gender = normalisedGender,
                Age = null,
                IsVet = isVet
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

    /// <summary>A parsed timing row: the original uploaded text plus its validated duration (US17).</summary>
    private sealed record ParsedTiming(string Raw, TimeSpan Duration);

    /// <summary>Positions whose duration is earlier than that of an earlier finishing position (US17 AC4).</summary>
    private static List<int> FindOutOfOrderPositions(Dictionary<int, ParsedTiming> rows)
    {
        var outOfOrder = new List<int>();
        TimeSpan? previous = null;
        foreach (var position in rows.Keys.OrderBy(p => p))
        {
            var duration = rows[position].Duration;
            if (previous.HasValue && duration < previous.Value)
            {
                outOfOrder.Add(position);
            }

            previous = duration;
        }

        return outOfOrder;
    }

    private static void ParseTimingsWorkbook(Stream stream, string fileName, Dictionary<int, ParsedTiming> rows, List<string> errors)
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

            if (!RaceTime.TryParse(time, out var duration))
            {
                errors.Add($"{fileName} row {rowNumber}: invalid time ({time.Trim()}).");
                continue;
            }

            if (!rows.TryAdd(position, new ParsedTiming(time.Trim(), duration)))
            {
                errors.Add($"{fileName} row {rowNumber}: duplicate timing position ({position}).");
            }
        }
    }

    private static void ParseTimingsCsv(Stream stream, string fileName, Dictionary<int, ParsedTiming> rows, List<string> errors)
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

            if (csv.Parser.Count < 3)
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

            if (!RaceTime.TryParse(time, out var duration))
            {
                errors.Add($"{fileName} row {rowNumber}: invalid time ({time}).");
                continue;
            }

            if (!rows.TryAdd(position, new ParsedTiming(time, duration)))
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
