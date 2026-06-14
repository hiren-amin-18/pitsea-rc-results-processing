using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RaceResults.Web.Data;
using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

public class SeasonReviewService : ISeasonReviewService
{
    private readonly IDbContextFactory<RaceResultsDbContext> _dbContextFactory;
    private readonly ISeasonStatisticsService _seasonStats;
    private readonly IChampionsOfChampionsService _championsService;

    public SeasonReviewService(
        IDbContextFactory<RaceResultsDbContext> dbContextFactory,
        ISeasonStatisticsService seasonStats,
        IChampionsOfChampionsService championsService)
    {
        _dbContextFactory = dbContextFactory;
        _seasonStats = seasonStats;
        _championsService = championsService;
    }

    public SeasonReview Build(int year)
    {
        var availableYears = _seasonStats.GetAvailableSeasons();
        var dashboard = _seasonStats.GetSeasonDashboard(year);
        var headlines = ComputeHeadlines(dashboard);
        var champions = _championsService.GetLeaderboardAsync(year).GetAwaiter().GetResult();
        var (newRecords, totalDnf, totalDsq, scoringEventCount, distinctScorers) = ReadYearScopedFigures(year);

        var comparison = TryBuildComparison(year - 1, headlines);
        var awards = BuildAwardsList(dashboard, champions);

        return new SeasonReview
        {
            Year = year,
            AvailableYears = availableYears,
            Headlines = headlines,
            Comparison = comparison,
            ChampionsLeaderboard = champions,
            ChampionsDistinctScorers = distinctScorers,
            ChampionsScoringEventCount = scoringEventCount,
            SeasonDashboard = dashboard,
            NewCourseRecordsThisYear = newRecords,
            TotalDnf = totalDnf,
            TotalDsq = totalDsq,
            Awards = awards
        };
    }

    private static SeasonHeadlines ComputeHeadlines(SeasonDashboard dashboard)
    {
        var entrants = dashboard.Participation.Sum(p => p.Entrants);
        var finishers = dashboard.Participation.Sum(p => p.Finishers);
        return new SeasonHeadlines
        {
            EventsHeld = dashboard.EventCount,
            TotalEntrants = entrants,
            TotalFinishers = finishers,
            TotalUniqueRunners = dashboard.TotalUniqueRunners,
            CompletionRatePercent = entrants == 0 ? 0 : Math.Round(100.0 * finishers / entrants, 1)
        };
    }

    private SeasonComparison? TryBuildComparison(int previousYear, SeasonHeadlines current)
    {
        var prevDashboard = _seasonStats.GetSeasonDashboard(previousYear);
        if (prevDashboard.EventCount == 0)
        {
            return null;
        }

        var prev = ComputeHeadlines(prevDashboard);
        return new SeasonComparison
        {
            Previous = prev,
            EntrantDelta = current.TotalEntrants - prev.TotalEntrants,
            EntrantPercentChange = prev.TotalEntrants == 0 ? 0 : Math.Round(100.0 * (current.TotalEntrants - prev.TotalEntrants) / prev.TotalEntrants, 1),
            UniqueRunnerDelta = current.TotalUniqueRunners - prev.TotalUniqueRunners,
            EventsHeldDelta = current.EventsHeld - prev.EventsHeld
        };
    }

    private (IReadOnlyList<CourseRecord>, int dnf, int dsq, int scoringEventCount, int distinctScorers)
        ReadYearScopedFigures(int year)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var eventIds = db.Events.Where(e => e.EventDate.Year == year).Select(e => e.Id).ToList();

        var newRecords = db.CourseRecords
            .Where(r => r.SourceEventId != null && eventIds.Contains(r.SourceEventId.Value))
            .OrderBy(r => r.EventType).ThenBy(r => r.Category)
            .ToList();

        // DNF derived consistently with the season dashboard (already excludes DNS).
        var totalDnf = db.Entrants
            .Where(e => eventIds.Contains(e.EventId) && e.Status == FinishStatus.DidNotFinish)
            .Count();
        var totalDsq = db.Entrants
            .Where(e => eventIds.Contains(e.EventId) && e.Status == FinishStatus.Disqualified)
            .Count();

        var scoringEvents = db.PointsAuditLogs
            .Where(a => a.SeasonYear == year && a.Action != AuditAction.Voided)
            .Select(a => a.EventId).Distinct().Count();
        var distinctScorers = db.PointsAuditLogs
            .Where(a => a.SeasonYear == year && a.Action != AuditAction.Voided && a.PointsAwarded > 0)
            .Select(a => a.EntrantId).Distinct().Count();

        return (newRecords, totalDnf, totalDsq, scoringEvents, distinctScorers);
    }

    private static AwardsList BuildAwardsList(SeasonDashboard dashboard, IReadOnlyList<ChampionsLeaderboardEntry> champions)
    {
        var championsWinners = champions
            .Where(e => e.Rank == 1)
            .OrderBy(e => e.Category)
            .Select(e => new AwardEntry
            {
                Title = $"Champion of Champions — {e.Category}",
                Winner = e.Entrant.Name,
                Detail = $"{e.TotalPoints} pts over {e.RaceCount} race(s)"
            })
            .ToList();

        var everPresentRunners = dashboard.MostAttendedRunners
            .Where(r => r.EverPresent)
            .Select(r => new AwardEntry
            {
                Title = "Ever-present runner",
                Winner = r.RunnerName,
                Detail = $"{r.EventsAttended} events"
            })
            .ToList();

        AwardEntry? mostImproved = null;
        if (dashboard.MostImprovedByType.Count > 0)
        {
            var top = dashboard.MostImprovedByType
                .OrderBy(m => m.EventType)
                .First();
            mostImproved = new AwardEntry
            {
                Title = "Most improved runner",
                Winner = top.RunnerName,
                Detail = $"{top.FirstTime} → {top.BestTime} ({top.Improvement})"
            };
        }

        return new AwardsList
        {
            ChampionsWinners = championsWinners,
            EverPresentRunners = everPresentRunners,
            MostImprovedRunner = mostImproved
        };
    }

    public byte[] GeneratePdf(int year)
    {
        var review = Build(year);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(28);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text("PITSEA RUNNING CLUB").Bold().FontSize(18);
                    col.Item().AlignCenter().Text($"End of Season Review — {review.Year}").FontSize(14);
                    col.Item().PaddingBottom(8);
                });

                page.Content().Column(column =>
                {
                    column.Spacing(8);

                    column.Item().Text("Season at a glance (full racing year)").Bold().FontSize(12);
                    column.Item().Text(t =>
                    {
                        t.Span($"{review.Headlines.EventsHeld} events · ");
                        t.Span($"{review.Headlines.TotalEntrants} entrants · ");
                        t.Span($"{review.Headlines.TotalFinishers} finishers · ");
                        t.Span($"{review.Headlines.TotalUniqueRunners} unique runners · ");
                        t.Span($"{review.Headlines.CompletionRatePercent}% completion");
                    });

                    if (review.Comparison is { } cmp)
                    {
                        column.Item().Text($"vs {year - 1}: entrants {SignedDelta(cmp.EntrantDelta)} ({Signed(cmp.EntrantPercentChange)}%), " +
                            $"unique runners {SignedDelta(cmp.UniqueRunnerDelta)}, events {SignedDelta(cmp.EventsHeldDelta)}.");
                    }

                    column.Item().PaddingTop(6).Text("Champions of Champions (May–September scoring window)").Bold().FontSize(12);
                    if (review.ChampionsLeaderboard.Count == 0)
                    {
                        column.Item().Text("No Champions scoring this season.").Italic();
                    }
                    else
                    {
                        foreach (var category in review.ChampionsLeaderboard.GroupBy(e => e.Category).OrderBy(g => g.Key))
                        {
                            column.Item().Text(category.Key).Bold();
                            foreach (var entry in category.OrderBy(e => e.Rank).Take(3))
                            {
                                column.Item().Text($"  {entry.Rank}. {entry.Entrant.Name} — {entry.TotalPoints} pts ({entry.RaceCount} races)");
                            }
                        }
                        column.Item().PaddingTop(2).Text($"{review.ChampionsDistinctScorers} distinct point scorers across {review.ChampionsScoringEventCount} scoring race(s).").Italic();
                    }

                    column.Item().PaddingTop(6).Text("Runner recognition (full series)").Bold().FontSize(12);
                    var attended = review.SeasonDashboard.MostAttendedRunners;
                    if (attended.Count > 0)
                    {
                        column.Item().Text($"Most attended: {string.Join(", ", attended.Select(a => $"{a.RunnerName} ({a.EventsAttended} events{(a.EverPresent ? " — ever-present" : string.Empty)})"))}");
                    }

                    if (review.NewCourseRecordsThisYear.Count > 0)
                    {
                        column.Item().PaddingTop(6).Text("New course records").Bold().FontSize(12);
                        foreach (var record in review.NewCourseRecordsThisYear)
                        {
                            column.Item().Text($"  {record.EventType} {record.Category}: {RaceTime.Format(record.Duration)} — {record.RunnerName}");
                        }
                    }

                    column.Item().PaddingTop(6).Text("DNF / DSQ summary").Bold().FontSize(12);
                    column.Item().Text($"DNF: {review.TotalDnf} · DSQ: {review.TotalDsq}");

                    column.Item().PaddingTop(6).Text("Awards").Bold().FontSize(12);
                    foreach (var award in review.Awards.ChampionsWinners
                        .Concat(review.Awards.EverPresentRunners)
                        .Concat(review.Awards.MostImprovedRunner is null ? Array.Empty<AwardEntry>() : new[] { review.Awards.MostImprovedRunner }))
                    {
                        column.Item().Text($"  {award.Title}: {award.Winner}" + (string.IsNullOrEmpty(award.Detail) ? "" : $" ({award.Detail})"));
                    }
                });
            });
        });

        return document.GeneratePdf();
    }

    private static string Signed(double value) => value >= 0 ? $"+{value}" : value.ToString();
    private static string SignedDelta(int value) => value >= 0 ? $"+{value}" : value.ToString();
}
