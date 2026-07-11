using Microsoft.EntityFrameworkCore;
using RaceResults.Web.Data;
using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

public class ChampionsOfChampionsService : IChampionsOfChampionsService
{
    // The Champions of Champions season spans Crown to Crown races from May to September inclusive.
    private const int SeasonStartMonth = 5;  // May
    private const int SeasonEndMonth = 9;     // September

    private readonly IDbContextFactory<RaceResultsDbContext> _dbContextFactory;
    private readonly IRaceResultsService _raceResultsService;

    public ChampionsOfChampionsService(
        IDbContextFactory<RaceResultsDbContext> dbContextFactory,
        IRaceResultsService raceResultsService)
    {
        _dbContextFactory = dbContextFactory;
        _raceResultsService = raceResultsService;
    }

    public async Task CalculateAndSaveEventPointsAsync(int eventId)
    {
        using var db = _dbContextFactory.CreateDbContext();

        var raceEvent = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId);
        if (raceEvent == null || raceEvent.EventType != EventType.CrownToCrown)
            throw new InvalidOperationException($"Event {eventId} is not a Crown to Crown race");

        if (!IsInSeasonWindow(raceEvent.EventDate))
            throw new InvalidOperationException(
                $"Event {eventId} is outside the Champions of Champions season (May–September) and is not scored.");

        var seasonYear = raceEvent.EventDate.Year;

        // Get top 10 finishers per category for this specific event
        var topTenByCategory = _raceResultsService.GetTopTenByCategory(eventId);

        // The audit log is append-only: previously recorded awards are preserved for history.
        // If this event already has awards, this pass is a recalculation; otherwise it is the initial award.
        var isRecalculation = await db.PointsAuditLogs
            .AnyAsync(a => a.EventId == eventId && a.SeasonYear == seasonYear && a.Action != AuditAction.Voided);
        var action = isRecalculation ? AuditAction.Recalculated : AuditAction.Initial;

        AddAuditLogsForEvent(db, eventId, seasonYear, topTenByCategory, action);
        await db.SaveChangesAsync();

        // Now aggregate into the cumulative leaderboard
        await AggregateLeaderboardAsync(db, seasonYear);
    }

    public async Task RecalculateSeasonPointsAsync(int seasonYear)
    {
        using var db = _dbContextFactory.CreateDbContext();

        // Get all Crown to Crown events for this season that fall within the May–September window
        var seasonEvents = await db.Events
            .Where(e => e.EventType == EventType.CrownToCrown
                     && e.EventDate.Year == seasonYear
                     && e.EventDate.Month >= SeasonStartMonth
                     && e.EventDate.Month <= SeasonEndMonth)
            .OrderBy(e => e.EventDate)
            .ToListAsync();

        // The audit log is append-only. We do not delete prior awards; instead we append a fresh
        // "Recalculated" batch for each event. Aggregation always uses each event's latest batch,
        // so prior batches remain as an audit trail of when points were awarded vs recalculated.
        foreach (var raceEvent in seasonEvents)
        {
            var topTenByCategory = _raceResultsService.GetTopTenByCategory(raceEvent.Id);
            AddAuditLogsForEvent(db, raceEvent.Id, seasonYear, topTenByCategory, AuditAction.Recalculated);
        }

        await db.SaveChangesAsync();

        // Reaggregate the leaderboard
        await AggregateLeaderboardAsync(db, seasonYear);
    }

    public async Task<IReadOnlyList<ChampionsLeaderboardEntry>> GetLeaderboardAsync(int seasonYear, int? asOfEventId = null)
    {
        using var db = _dbContextFactory.CreateDbContext();

        if (asOfEventId.HasValue)
        {
            // Get the target event date to filter audit logs
            var targetEvent = await db.Events.FirstOrDefaultAsync(e => e.Id == asOfEventId);
            if (targetEvent != null)
            {
                // Get all in-season event IDs up to and including this event (by date)
                var eligibleEventIds = await db.Events
                    .Where(e => e.EventType == EventType.CrownToCrown
                             && e.EventDate.Year == seasonYear
                             && e.EventDate.Month >= SeasonStartMonth
                             && e.EventDate.Month <= SeasonEndMonth
                             && e.EventDate <= targetEvent.EventDate)
                    .Select(e => e.Id)
                    .ToListAsync();

                var audits = await db.PointsAuditLogs
                    .Where(a => a.SeasonYear == seasonYear
                             && eligibleEventIds.Contains(a.EventId)
                             && a.Action != AuditAction.Voided)
                    .Include(a => a.Entrant)
                    .ToListAsync();

                return RankAndReturn(AggregateAudits(audits));
            }
        }

        // Default: return from the aggregate scores table
        var allScores = await db.ChampionOfChampionsScores
            .Where(s => s.SeasonYear == seasonYear)
            .Include(s => s.Entrant)
            .AsNoTracking()
            .ToListAsync();

        var leaderboardEntries = allScores
            .Where(s => s.Entrant != null)
            .Select(s => new ChampionsLeaderboardEntry
            {
                Entrant = s.Entrant!,
                Category = s.Category,
                TotalPoints = s.TotalPoints,
                RaceCount = s.RaceCount
            })
            .ToList();

        return RankAndReturn(leaderboardEntries);
    }

    public async Task<IReadOnlyList<ChampionsLeaderboardEntry>> GetCurrentSeasonLeaderboardAsync(int? asOfEventId = null)
    {
        // The season is determined by the current event's date, not wall-clock time, so the
        // leaderboard stays correct when viewing historical data or across a calendar boundary.
        var currentEvent = _raceResultsService.GetCurrentEvent();
        if (currentEvent is null)
        {
            return Array.Empty<ChampionsLeaderboardEntry>();
        }
        var currentSeasonYear = currentEvent.EventDate.Year;
        return await GetLeaderboardAsync(currentSeasonYear, asOfEventId);
    }

    public async Task<ChampionsDetail> GetLeaderboardDetailAsync(int seasonYear, int? asOfEventId = null)
    {
        using var db = _dbContextFactory.CreateDbContext();

        // The in-season Crown to Crown events that can carry points, in date order, optionally
        // capped at the "as of" event's date so the columns match the summary's as-of scope.
        var eventsQuery = db.Events
            .Where(e => e.EventType == EventType.CrownToCrown
                     && e.EventDate.Year == seasonYear
                     && e.EventDate.Month >= SeasonStartMonth
                     && e.EventDate.Month <= SeasonEndMonth);

        if (asOfEventId.HasValue)
        {
            var targetEvent = await db.Events.FirstOrDefaultAsync(e => e.Id == asOfEventId);
            if (targetEvent != null)
            {
                eventsQuery = eventsQuery.Where(e => e.EventDate <= targetEvent.EventDate);
            }
        }

        var seasonEvents = await eventsQuery.OrderBy(e => e.EventDate).ToListAsync();
        var eligibleEventIds = seasonEvents.Select(e => e.Id).ToList();

        var audits = await db.PointsAuditLogs
            .Where(a => a.SeasonYear == seasonYear
                     && eligibleEventIds.Contains(a.EventId)
                     && a.Action != AuditAction.Voided)
            .Include(a => a.Entrant)
            .ToListAsync();

        // The latest batch per event only (mirrors AggregateAudits) so a re-scored event isn't double counted.
        var liveAudits = audits
            .Where(a => a.Entrant != null)
            .GroupBy(a => a.EventId)
            .SelectMany(g =>
            {
                var latestBatch = g.Max(x => x.AuditTimestamp);
                return g.Where(x => x.AuditTimestamp == latestBatch);
            })
            .ToList();

        // Columns are only the events that actually awarded points, numbered in date order.
        var scoredEventIds = liveAudits.Select(a => a.EventId).ToHashSet();
        var columns = seasonEvents
            .Where(e => scoredEventIds.Contains(e.Id))
            .Select((e, i) => new ChampionsDetailColumn
            {
                EventId = e.Id,
                Round = i + 1,
                Label = $"Round {i + 1} – {e.EventDate:MMMM}",
                EventName = e.EventName,
                EventDate = e.EventDate
            })
            .ToList();

        // Per runner+category, the points scored in each event. Keyed by the same runner identity
        // the summary aggregation uses, so the two always line up.
        var pointsByRunner = liveAudits
            .GroupBy(a => $"{RunnerKey(a.Entrant!)}|{a.Category}")
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<int, int>)g
                    .GroupBy(x => x.EventId)
                    .ToDictionary(x => x.Key, x => x.Sum(y => y.PointsAwarded)));

        // Reuse the summary ranking verbatim so rows, order, ties and highlighting match exactly.
        var rankedEntries = RankAndReturn(AggregateAudits(audits));

        var rows = rankedEntries
            .Select(entry => new ChampionsDetailRow
            {
                Entry = entry,
                PointsByEventId = pointsByRunner.TryGetValue($"{RunnerKey(entry.Entrant)}|{entry.Category}", out var map)
                    ? map
                    : new Dictionary<int, int>()
            })
            .ToList();

        return new ChampionsDetail { Columns = columns, Rows = rows };
    }

    public async Task<bool> IsEligibleForPointsAsync(int entrantId, int eventId, string category)
    {
        using var db = _dbContextFactory.CreateDbContext();

        // Check if this runner is in top 10 of their category for this event
        var topTenAudit = await db.PointsAuditLogs
            .FirstOrDefaultAsync(a =>
                a.EntrantId == entrantId &&
                a.EventId == eventId &&
                a.Category == category &&
                a.Action != AuditAction.Voided &&
                a.PointsAwarded > 0);

        return topTenAudit != null;
    }

    public async Task VoidDisqualifiedAndRecalculateAsync(int eventId)
    {
        using var db = _dbContextFactory.CreateDbContext();

        var raceEvent = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId);
        if (raceEvent is null || raceEvent.EventType != EventType.CrownToCrown || !IsInSeasonWindow(raceEvent.EventDate))
        {
            return; // Nothing scored for non-C2C / out-of-season events.
        }

        var seasonYear = raceEvent.EventDate.Year;

        var dsqEntrantIds = await db.Entrants
            .Where(e => e.EventId == eventId && e.Status == FinishStatus.Disqualified)
            .Select(e => e.Id)
            .ToListAsync();

        if (dsqEntrantIds.Count > 0)
        {
            // Record an explicit Voided audit entry for each award the disqualified runners currently hold,
            // preserving why those points were removed. The subsequent recalculation supersedes them.
            var currentAwards = await db.PointsAuditLogs
                .Where(a => a.EventId == eventId
                         && a.SeasonYear == seasonYear
                         && a.Action != AuditAction.Voided
                         && a.PointsAwarded > 0
                         && dsqEntrantIds.Contains(a.EntrantId))
                .ToListAsync();

            var batchTimestamp = DateTime.UtcNow;
            foreach (var award in currentAwards)
            {
                db.PointsAuditLogs.Add(new PointsAuditLog
                {
                    SeasonYear = seasonYear,
                    EventId = eventId,
                    EntrantId = award.EntrantId,
                    Category = award.Category,
                    PointsAwarded = 0,
                    Action = AuditAction.Voided,
                    AuditTimestamp = batchTimestamp,
                    Reason = "Voided: disqualified"
                });
            }

            await db.SaveChangesAsync();
        }

        // Re-derive the season from current results, which now exclude disqualified finishers.
        await RecalculateSeasonPointsAsync(seasonYear);
    }

    private static bool IsInSeasonWindow(DateTime date) =>
        date.Month >= SeasonStartMonth && date.Month <= SeasonEndMonth;

    private static void AddAuditLogsForEvent(
        RaceResultsDbContext db,
        int eventId,
        int seasonYear,
        IReadOnlyList<TopTenCategory> topTenByCategory,
        AuditAction action)
    {
        // A single timestamp identifies this write as one batch for the event.
        var batchTimestamp = DateTime.UtcNow;
        var reasonPrefix = action == AuditAction.Recalculated ? "Recalculated: " : string.Empty;

        foreach (var categoryGroup in topTenByCategory)
        {
            var categoryName = categoryGroup.Name;
            var categoryResults = categoryGroup.Results;

            for (int position = 0; position < categoryResults.Count && position < 10; position++)
            {
                var result = categoryResults[position];
                if (result.Entrant == null) continue;

                int pointsAwarded = 10 - position; // 1st=10, 2nd=9, ..., 10th=1

                db.PointsAuditLogs.Add(new PointsAuditLog
                {
                    SeasonYear = seasonYear,
                    EventId = eventId,
                    EntrantId = result.Entrant.Id,
                    Category = categoryName,
                    PointsAwarded = pointsAwarded,
                    Action = action,
                    AuditTimestamp = batchTimestamp,
                    Reason = $"{reasonPrefix}Position {result.Position} in {categoryName}"
                });
            }
        }
    }

    /// <summary>
    /// Aggregates audit logs into one leaderboard entry per runner+category.
    /// Runners are identified by name and club (bib numbers differ between races), so a runner's
    /// points accumulate across every event in the season. For each event only the most recent batch
    /// of awards is counted, so re-scoring an event supersedes its earlier awards without double counting.
    /// </summary>
    private static List<ChampionsLeaderboardEntry> AggregateAudits(IEnumerable<PointsAuditLog> audits)
    {
        var liveAudits = audits
            .Where(a => a.Entrant != null && a.Action != AuditAction.Voided)
            .GroupBy(a => a.EventId)
            .SelectMany(eventGroup =>
            {
                var latestBatch = eventGroup.Max(x => x.AuditTimestamp);
                return eventGroup.Where(x => x.AuditTimestamp == latestBatch);
            });

        return liveAudits
            .GroupBy(a => new { Runner = RunnerKey(a.Entrant!), a.Category })
            .Select(g => new ChampionsLeaderboardEntry
            {
                // Use the most recent event's entrant row for display (latest club/name on record).
                Entrant = g.OrderByDescending(x => x.EventId).First().Entrant!,
                Category = g.Key.Category,
                TotalPoints = g.Sum(x => x.PointsAwarded),
                RaceCount = g.Select(x => x.EventId).Distinct().Count()
            })
            .ToList();
    }

    // A runner is identified across events by their persistent RunnerId (US15 AC5). Entrants predating
    // the runner registry (no RunnerId) fall back to a per-entrant key so they are never merged by accident.
    private static string RunnerKey(Entrant entrant) =>
        entrant.RunnerId.HasValue ? $"r{entrant.RunnerId.Value}" : $"e{entrant.Id}";

    // The fixed order categories are shown in across the leaderboard and every export.
    private static readonly string[] CategoryDisplayOrder = { "Male", "Female", "Male U18", "Female U18" };

    /// <summary>Sort index for a category in the canonical display order; unknown categories sort last.</summary>
    public static int CategoryDisplayRank(string category)
    {
        var index = Array.IndexOf(CategoryDisplayOrder, category);
        return index < 0 ? int.MaxValue : index;
    }

    private static IReadOnlyList<ChampionsLeaderboardEntry> RankAndReturn(
        List<ChampionsLeaderboardEntry> entries)
    {
        // Group by category (in canonical display order), rank within each category
        var rankedByCategory = entries
            .GroupBy(e => e.Category)
            .OrderBy(g => CategoryDisplayRank(g.Key))
            .ThenBy(g => g.Key)
            .SelectMany(g =>
            {
                var sorted = g
                    .OrderByDescending(e => e.TotalPoints)
                    .ThenByDescending(e => e.RaceCount)
                    .ToList();

                // Assign ranks and detect genuine ties (equal on both points AND race count,
                // i.e. the tie-breaker did not separate them).
                for (int i = 0; i < sorted.Count; i++)
                {
                    sorted[i].Rank = i + 1;

                    if (i < sorted.Count - 1
                        && sorted[i].TotalPoints == sorted[i + 1].TotalPoints
                        && sorted[i].RaceCount == sorted[i + 1].RaceCount)
                    {
                        sorted[i].IsPointsTied = true;
                        sorted[i + 1].IsPointsTied = true;
                    }
                }

                return sorted;
            })
            .ToList();

        return rankedByCategory;
    }

    private async Task AggregateLeaderboardAsync(RaceResultsDbContext db, int seasonYear)
    {
        // The scores table is a derived cache; rebuild it from the audit log each time.
        var existingScores = await db.ChampionOfChampionsScores
            .Where(s => s.SeasonYear == seasonYear)
            .ToListAsync();
        db.ChampionOfChampionsScores.RemoveRange(existingScores);

        // Only count awards from Crown to Crown events within the May–September window.
        var inSeasonEventIds = await db.Events
            .Where(e => e.EventType == EventType.CrownToCrown
                     && e.EventDate.Year == seasonYear
                     && e.EventDate.Month >= SeasonStartMonth
                     && e.EventDate.Month <= SeasonEndMonth)
            .Select(e => e.Id)
            .ToListAsync();

        var audits = await db.PointsAuditLogs
            .Where(a => a.SeasonYear == seasonYear
                     && a.Action != AuditAction.Voided
                     && inSeasonEventIds.Contains(a.EventId))
            .Include(a => a.Entrant)
            .ToListAsync();

        foreach (var entry in AggregateAudits(audits))
        {
            db.ChampionOfChampionsScores.Add(new ChampionOfChampionsScore
            {
                SeasonYear = seasonYear,
                EntrantId = entry.Entrant.Id,
                Category = entry.Category,
                TotalPoints = entry.TotalPoints,
                RaceCount = entry.RaceCount,
                LastUpdated = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }
}
