using Microsoft.EntityFrameworkCore;
using RaceResults.Web.Data;
using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

public class ChampionsOfChampionsService : IChampionsOfChampionsService
{
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

        var seasonYear = raceEvent.EventDate.Year;

        // Get top 10 finishers per category for this specific event
        var topTenByCategory = _raceResultsService.GetTopTenByCategory(eventId);

        // Clear any existing audit logs for this event (in case of recalculation)
        var existingAudits = await db.PointsAuditLogs
            .Where(a => a.EventId == eventId && a.SeasonYear == seasonYear)
            .ToListAsync();
        db.PointsAuditLogs.RemoveRange(existingAudits);

        // For each category, award points to top 10
        foreach (var categoryGroup in topTenByCategory)
        {
            var categoryName = categoryGroup.Name;
            var categoryResults = categoryGroup.Results;

            for (int position = 0; position < categoryResults.Count && position < 10; position++)
            {
                var result = categoryResults[position];
                if (result.Entrant == null) continue;

                int pointsAwarded = 10 - position; // 1st=10, 2nd=9, ..., 10th=1

                // Record audit log
                var auditLog = new PointsAuditLog
                {
                    SeasonYear = seasonYear,
                    EventId = eventId,
                    EntrantId = result.Entrant.Id,
                    Category = categoryName,
                    PointsAwarded = pointsAwarded,
                    Action = AuditAction.Initial,
                    AuditTimestamp = DateTime.UtcNow,
                    Reason = $"Position {result.Position} in {categoryName}"
                };
                db.PointsAuditLogs.Add(auditLog);
            }
        }

        await db.SaveChangesAsync();

        // Now aggregate into the cumulative leaderboard
        await AggregateLeaderboardAsync(db, seasonYear);
    }

    public async Task RecalculateSeasonPointsAsync(int seasonYear)
    {
        using var db = _dbContextFactory.CreateDbContext();

        // Get all Crown to Crown events for this season
        var seasonEvents = await db.Events
            .Where(e => e.EventType == EventType.CrownToCrown && e.EventDate.Year == seasonYear)
            .OrderBy(e => e.EventDate)
            .ToListAsync();

        // Clear all existing audit logs and scores for the season
        var existingAudits = await db.PointsAuditLogs
            .Where(a => a.SeasonYear == seasonYear)
            .ToListAsync();
        db.PointsAuditLogs.RemoveRange(existingAudits);

        var existingScores = await db.ChampionOfChampionsScores
            .Where(s => s.SeasonYear == seasonYear)
            .ToListAsync();
        db.ChampionOfChampionsScores.RemoveRange(existingScores);

        await db.SaveChangesAsync();

        // Re-score each event in the season
        foreach (var raceEvent in seasonEvents)
        {
            var topTenByCategory = _raceResultsService.GetTopTenByCategory(raceEvent.Id);

            foreach (var categoryGroup in topTenByCategory)
            {
                var categoryName = categoryGroup.Name;
                var categoryResults = categoryGroup.Results;

                for (int position = 0; position < categoryResults.Count && position < 10; position++)
                {
                    var result = categoryResults[position];
                    if (result.Entrant == null) continue;

                    int pointsAwarded = 10 - position;

                    var auditLog = new PointsAuditLog
                    {
                        SeasonYear = seasonYear,
                        EventId = raceEvent.Id,
                        EntrantId = result.Entrant.Id,
                        Category = categoryName,
                        PointsAwarded = pointsAwarded,
                        Action = AuditAction.Recalculated,
                        AuditTimestamp = DateTime.UtcNow,
                        Reason = $"Recalculated: Position {result.Position} in {categoryName}"
                    };
                    db.PointsAuditLogs.Add(auditLog);
                }
            }
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
                // Get all event IDs up to and including this event (by date)
                var eligibleEventIds = await db.Events
                    .Where(e => e.EventType == EventType.CrownToCrown
                             && e.EventDate.Year == seasonYear
                             && e.EventDate <= targetEvent.EventDate)
                    .Select(e => e.Id)
                    .ToListAsync();

                // Aggregate from audit logs for only those events
                var auditAggregates = await db.PointsAuditLogs
                    .Where(a => a.SeasonYear == seasonYear
                             && eligibleEventIds.Contains(a.EventId)
                             && a.Action != AuditAction.Voided)
                    .GroupBy(a => new { a.EntrantId, a.Category })
                    .Select(g => new
                    {
                        g.Key.EntrantId,
                        g.Key.Category,
                        TotalPoints = g.Sum(x => x.PointsAwarded),
                        RaceCount = g.Select(x => x.EventId).Distinct().Count()
                    })
                    .ToListAsync();

                // Load entrants for display
                var entrantIds = auditAggregates.Select(a => a.EntrantId).Distinct().ToList();
                var entrants = await db.Entrants
                    .Where(e => entrantIds.Contains(e.Id))
                    .ToDictionaryAsync(e => e.Id);

                var entries = new List<ChampionsLeaderboardEntry>();
                foreach (var agg in auditAggregates)
                {
                    if (!entrants.TryGetValue(agg.EntrantId, out var entrant)) continue;

                    entries.Add(new ChampionsLeaderboardEntry
                    {
                        Entrant = entrant,
                        Category = agg.Category,
                        TotalPoints = agg.TotalPoints,
                        RaceCount = agg.RaceCount
                    });
                }

                return RankAndReturn(entries);
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
        var currentYear = DateTime.Now.Year;
        return await GetLeaderboardAsync(currentYear, asOfEventId);
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
                a.PointsAwarded > 0);

        return topTenAudit != null;
    }

    private static IReadOnlyList<ChampionsLeaderboardEntry> RankAndReturn(
        List<ChampionsLeaderboardEntry> entries)
    {
        // Group by category, rank within each category
        var rankedByCategory = entries
            .GroupBy(e => e.Category)
            .SelectMany(g =>
            {
                var sorted = g
                    .OrderByDescending(e => e.TotalPoints)
                    .ThenByDescending(e => e.RaceCount)
                    .ToList();

                // Assign ranks and detect ties
                for (int i = 0; i < sorted.Count; i++)
                {
                    sorted[i].Rank = i + 1;
                    
                    // Check if this runner is tied on points with the next runner
                    if (i < sorted.Count - 1 && sorted[i].TotalPoints == sorted[i + 1].TotalPoints)
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
        // Clear existing scores for this season
        var existingScores = await db.ChampionOfChampionsScores
            .Where(s => s.SeasonYear == seasonYear)
            .ToListAsync();
        db.ChampionOfChampionsScores.RemoveRange(existingScores);

        // Aggregate points from audit log
        var aggregated = await db.PointsAuditLogs
            .Where(a => a.SeasonYear == seasonYear && a.Action != AuditAction.Voided)
            .GroupBy(a => new { a.EntrantId, a.Category })
            .Select(g => new
            {
                g.Key.EntrantId,
                g.Key.Category,
                TotalPoints = g.Sum(x => x.PointsAwarded),
                RaceCount = g.Select(x => x.EventId).Distinct().Count()
            })
            .ToListAsync();

        foreach (var agg in aggregated)
        {
            var score = new ChampionOfChampionsScore
            {
                SeasonYear = seasonYear,
                EntrantId = agg.EntrantId,
                Category = agg.Category,
                TotalPoints = agg.TotalPoints,
                RaceCount = agg.RaceCount,
                LastUpdated = DateTime.UtcNow
            };
            db.ChampionOfChampionsScores.Add(score);
        }

        await db.SaveChangesAsync();
    }
}
