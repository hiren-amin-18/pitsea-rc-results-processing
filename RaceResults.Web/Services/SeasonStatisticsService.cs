using Microsoft.EntityFrameworkCore;
using RaceResults.Web.Data;
using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

public class SeasonStatisticsService : ISeasonStatisticsService
{
    private const int ChampionsSeasonStartMonth = 5;
    private const int ChampionsSeasonEndMonth = 9;

    private readonly IDbContextFactory<RaceResultsDbContext> _dbContextFactory;
    private readonly IRaceResultsService _raceResultsService;

    public SeasonStatisticsService(
        IDbContextFactory<RaceResultsDbContext> dbContextFactory,
        IRaceResultsService raceResultsService)
    {
        _dbContextFactory = dbContextFactory;
        _raceResultsService = raceResultsService;
    }

    public IReadOnlyList<int> GetAvailableSeasons()
    {
        using var db = _dbContextFactory.CreateDbContext();
        return db.Events.Select(e => e.EventDate.Year).Distinct().OrderByDescending(y => y).ToList();
    }

    public SeasonDashboard GetSeasonDashboard(int year)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var events = db.Events.Where(e => e.EventDate.Year == year).OrderBy(e => e.EventDate).ToList();
        var availableYears = db.Events.Select(e => e.EventDate.Year).Distinct().OrderByDescending(y => y).ToList();

        if (events.Count == 0)
        {
            return new SeasonDashboard { Year = year, AvailableYears = availableYears };
        }

        var eventIds = events.Select(e => e.Id).ToHashSet();
        var entrants = db.Entrants.Where(e => eventIds.Contains(e.EventId)).ToList();
        var startedEntrants = entrants.Where(e => e.Status != FinishStatus.DidNotStart).ToList();
        var finishedKeys = db.FinishBibRecords
            .Where(r => eventIds.Contains(r.EventId))
            .Select(r => new { r.EventId, r.BibNumber })
            .ToList()
            .Select(r => (r.EventId, Bib: r.BibNumber.ToLowerInvariant()))
            .ToHashSet();

        // Per-event collated finishers (excludes DSQ; carries RunnerId, Duration, category).
        var collatedByEvent = events.ToDictionary(e => e.Id, e => _raceResultsService.GetCollatedResults(e.Id));

        // --- Attendance (keyed on RunnerId) ---
        var attendanceByRunner = entrants
            .Where(e => e.RunnerId.HasValue)
            .GroupBy(e => e.RunnerId!.Value)
            .Select(g => new
            {
                RunnerId = g.Key,
                Events = g.Select(x => x.EventId).Distinct().Count(),
                Latest = g.OrderByDescending(x => x.EventId).First()
            })
            .ToList();

        var maxAttendance = attendanceByRunner.Count > 0 ? attendanceByRunner.Max(a => a.Events) : 0;
        var mostAttended = attendanceByRunner
            .Where(a => a.Events == maxAttendance && maxAttendance > 0)
            .Select(a => new AttendanceItem
            {
                RunnerId = a.RunnerId,
                RunnerName = a.Latest.Name,
                Club = a.Latest.Club,
                EventsAttended = a.Events,
                EverPresent = a.Events == events.Count
            })
            .OrderBy(a => a.RunnerName)
            .ToList();

        // --- Clubs ---
        string ClubLabel(string? club) => string.IsNullOrWhiteSpace(club) ? "Unaffiliated" : club!;
        var topClubsByEntries = entrants
            .GroupBy(e => ClubLabel(e.Club), StringComparer.OrdinalIgnoreCase)
            .Select(g => new ClubCount { Club = g.Key, Count = g.Count() })
            .OrderByDescending(c => c.Count).ThenBy(c => c.Club).Take(10).ToList();
        var topClubsByRunners = entrants
            .Where(e => e.RunnerId.HasValue)
            .GroupBy(e => ClubLabel(e.Club), StringComparer.OrdinalIgnoreCase)
            .Select(g => new ClubCount { Club = g.Key, Count = g.Select(x => x.RunnerId!.Value).Distinct().Count() })
            .OrderByDescending(c => c.Count).ThenBy(c => c.Club).Take(10).ToList();

        // --- Time-based: fastest per category per type, most improved per type ---
        var typedFinishes = new List<(EventType Type, string Category, TimeSpan Duration, string Runner, string? Club, int? RunnerId, string EventName, DateTime Date)>();
        var excludedTimeRows = 0;
        foreach (var raceEvent in events)
        {
            foreach (var r in collatedByEvent[raceEvent.Id])
            {
                var category = CategoryOf(r);
                if (category is null)
                {
                    continue;
                }

                if (!r.Duration.HasValue)
                {
                    excludedTimeRows++;
                    continue;
                }

                typedFinishes.Add((raceEvent.EventType, category, r.Duration.Value, r.Name, r.Club, r.Entrant?.RunnerId, raceEvent.EventName, raceEvent.EventDate));
            }
        }

        var fastestByCategory = typedFinishes
            .GroupBy(f => new { f.Type, f.Category })
            .Select(g =>
            {
                var best = g.OrderBy(x => x.Duration).First();
                return new CategoryFastest
                {
                    EventType = g.Key.Type,
                    Category = g.Key.Category,
                    RunnerName = best.Runner,
                    Club = best.Club,
                    Time = RaceTime.Format(best.Duration),
                    EventName = best.EventName
                };
            })
            .OrderBy(c => c.EventType).ThenBy(c => c.Category)
            .ToList();

        var mostImproved = typedFinishes
            .Where(f => f.RunnerId.HasValue)
            .GroupBy(f => f.Type)
            .Select(typeGroup =>
            {
                MostImproved? best = null;
                TimeSpan bestDiff = TimeSpan.Zero;
                foreach (var runnerGroup in typeGroup.GroupBy(x => x.RunnerId!.Value))
                {
                    var ordered = runnerGroup.OrderBy(x => x.Date).ToList();
                    if (ordered.Count < 2)
                    {
                        continue;
                    }

                    var firstTime = ordered.First().Duration;
                    var bestTime = ordered.Min(x => x.Duration);
                    var diff = firstTime - bestTime;
                    if (diff > bestDiff)
                    {
                        bestDiff = diff;
                        best = new MostImproved
                        {
                            EventType = typeGroup.Key,
                            RunnerName = ordered.First().Runner,
                            Improvement = $"-{RaceTime.Format(diff)}",
                            FirstTime = RaceTime.Format(firstTime),
                            BestTime = RaceTime.Format(bestTime)
                        };
                    }
                }

                return best;
            })
            .Where(m => m is not null)
            .Select(m => m!)
            .ToList();

        // --- Participation per event + first-timers ---
        var firstEventByRunner = entrants
            .Where(e => e.RunnerId.HasValue)
            .GroupBy(e => e.RunnerId!.Value)
            .ToDictionary(g => g.Key, g => g.Min(x => events.First(ev => ev.Id == x.EventId).EventDate));

        var participation = new List<ParticipationRow>();
        var totalStarts = 0;
        var totalDnf = 0;
        foreach (var raceEvent in events)
        {
            var eventStarters = startedEntrants.Where(e => e.EventId == raceEvent.Id).ToList();
            var matchedFinishers = eventStarters.Count(e => finishedKeys.Contains((raceEvent.Id, e.BibNumber.ToLowerInvariant())));
            var dnf = eventStarters.Count - matchedFinishers;
            totalStarts += eventStarters.Count;
            totalDnf += dnf;

            var firstTimers = eventStarters.Count(e => e.RunnerId.HasValue
                && firstEventByRunner.TryGetValue(e.RunnerId.Value, out var firstDate)
                && firstDate == raceEvent.EventDate);

            participation.Add(new ParticipationRow
            {
                EventName = raceEvent.EventName,
                EventDate = raceEvent.EventDate,
                Entrants = eventStarters.Count,
                Finishers = collatedByEvent[raceEvent.Id].Count,
                FirstTimers = firstTimers
            });
        }

        return new SeasonDashboard
        {
            Year = year,
            AvailableYears = availableYears,
            Events = events,
            EventCount = events.Count,
            TotalUniqueRunners = entrants.Where(e => e.RunnerId.HasValue).Select(e => e.RunnerId!.Value).Distinct().Count(),
            MostAttendedRunners = mostAttended,
            TopClubsByEntries = topClubsByEntries,
            TopClubsByRunners = topClubsByRunners,
            FastestByCategory = fastestByCategory,
            MostImprovedByType = mostImproved,
            Participation = participation,
            SeasonDnfRatePercent = totalStarts == 0 ? 0 : Math.Round(100.0 * totalDnf / totalStarts, 1),
            ExcludedTimeRows = excludedTimeRows
        };
    }

    public C2CSeasonStats GetC2CSeasonStats(int year)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var availableYears = db.Events.Select(e => e.EventDate.Year).Distinct().OrderByDescending(y => y).ToList();

        // Every C2C event in the calendar year, chronological.
        var seasonC2C = db.Events
            .Where(e => e.EventType == EventType.CrownToCrown && e.EventDate.Year == year)
            .OrderBy(e => e.EventDate)
            .ToList();

        // Anchor to the current event: for the live season only include events up to and including
        // it; a past/complete season (no current event in this year) shows the whole series.
        var currentEvent = _raceResultsService.GetCurrentEvent();
        var cutoff = currentEvent is not null && currentEvent.EventDate.Year == year
            ? currentEvent.EventDate
            : (DateTime?)null;
        var events = cutoff.HasValue
            ? seasonC2C.Where(e => e.EventDate <= cutoff.Value).ToList()
            : seasonC2C;

        if (events.Count == 0)
        {
            return new C2CSeasonStats
            {
                Year = year,
                AvailableYears = availableYears,
                EventsScheduled = seasonC2C.Count,
                CurrentEventName = currentEvent?.EventName ?? string.Empty
            };
        }

        var eventIds = events.Select(e => e.Id).ToHashSet();
        var dateByEventId = events.ToDictionary(e => e.Id, e => e.EventDate);

        // Runner-attributed starters (excludes DNS and bib-only entrants without a registry link).
        var started = db.Entrants
            .Where(e => eventIds.Contains(e.EventId) && e.Status != FinishStatus.DidNotStart && e.RunnerId.HasValue)
            .ToList();
        var startedByEvent = started.GroupBy(e => e.EventId).ToDictionary(g => g.Key, g => g.ToList());
        var firstEventByRunner = started
            .GroupBy(e => e.RunnerId!.Value)
            .ToDictionary(g => g.Key, g => g.Min(x => dateByEventId[x.EventId]));

        // --- Per-event trend series (reuses the same per-event summary as the Race Stats page) ---
        var points = new List<SeasonEventPoint>();
        var seen = new HashSet<int>();
        var totalStarters = 0;
        var totalFinishers = 0;
        var totalDnf = 0;
        var excludedTimeRows = 0;

        foreach (var raceEvent in events)
        {
            var summary = _raceResultsService.GetRaceStatisticsSummary(raceEvent.Id);
            var attributed = startedByEvent.GetValueOrDefault(raceEvent.Id, new List<Entrant>())
                .Select(e => e.RunnerId!.Value).Distinct().ToList();
            var firstTimers = attributed.Count(rid => firstEventByRunner[rid] == raceEvent.EventDate);
            foreach (var rid in attributed)
            {
                seen.Add(rid);
            }

            totalStarters += summary.EntrantCount;
            totalFinishers += summary.FinisherCount;
            totalDnf += summary.DnfCount;
            excludedTimeRows += summary.ExcludedTimeRowCount;

            points.Add(new SeasonEventPoint
            {
                EventName = raceEvent.EventName,
                EventDate = raceEvent.EventDate,
                Entrants = summary.EntrantCount,
                Finishers = summary.FinisherCount,
                FirstTimers = firstTimers,
                ReturningRunners = attributed.Count - firstTimers,
                MaleFinishers = summary.MaleFinishers,
                FemaleFinishers = summary.FemaleFinishers,
                CumulativeUniqueRunners = seen.Count,
                DnfRatePercent = summary.EntrantCount == 0 ? 0 : Math.Round(100.0 * summary.DnfCount / summary.EntrantCount, 1),
                WinnerSeconds = summary.WinnerTime.HasValue ? (int)summary.WinnerTime.Value.TotalSeconds : null,
                MedianSeconds = summary.MedianTime.HasValue ? (int)summary.MedianTime.Value.TotalSeconds : null
            });
        }

        // --- Attendance distribution + ever-present ---
        var attendanceByRunner = started
            .GroupBy(e => e.RunnerId!.Value)
            .Select(g => g.Select(x => x.EventId).Distinct().Count())
            .ToList();
        var attendanceDistribution = attendanceByRunner
            .GroupBy(c => c)
            .Select(g => new AttendanceBucket { Events = g.Key, Runners = g.Count() })
            .OrderBy(b => b.Events)
            .ToList();

        // --- Top clubs by unique runners ---
        string ClubLabel(string? club) => string.IsNullOrWhiteSpace(club) ? "Unaffiliated" : club!;
        var topClubs = started
            .GroupBy(e => ClubLabel(e.Club), StringComparer.OrdinalIgnoreCase)
            .Select(g => new ClubCount { Club = g.Key, Count = g.Select(x => x.RunnerId!.Value).Distinct().Count() })
            .OrderByDescending(c => c.Count).ThenBy(c => c.Club).Take(10).ToList();

        var (pointsRaceEventNames, pointsRace) = BuildPointsRace(db, events, year);

        return new C2CSeasonStats
        {
            Year = year,
            AvailableYears = availableYears,
            HasData = true,
            CurrentEventName = currentEvent?.EventName ?? string.Empty,
            EventsRun = events.Count,
            EventsScheduled = seasonC2C.Count,
            UniqueRunners = attendanceByRunner.Count,
            TotalFinishers = totalFinishers,
            AverageFieldSize = Math.Round((double)totalStarters / events.Count, 1),
            SeasonDnfRatePercent = totalStarters == 0 ? 0 : Math.Round(100.0 * totalDnf / totalStarters, 1),
            EverPresentCount = attendanceByRunner.Count(c => c == events.Count),
            Events = points,
            AttendanceDistribution = attendanceDistribution,
            TopClubs = topClubs,
            HasPointsData = pointsRace.Any(r => r.Total > 0),
            PointsRaceEventNames = pointsRaceEventNames,
            PointsRace = pointsRace,
            ExcludedTimeRows = excludedTimeRows
        };
    }

    /// <summary>
    /// Cumulative Champions points for the top contenders across the in-window (May–Sept) C2C events
    /// of the season-to-date. Mirrors the per-runner progression logic but for the whole field.
    /// </summary>
    private (List<string> EventNames, List<PointsRaceRunner> Runners) BuildPointsRace(
        RaceResultsDbContext db, List<RaceEvent> events, int year)
    {
        var inWindow = events
            .Where(e => e.EventDate.Month >= ChampionsSeasonStartMonth && e.EventDate.Month <= ChampionsSeasonEndMonth)
            .OrderBy(e => e.EventDate)
            .ToList();
        if (inWindow.Count == 0)
        {
            return (new List<string>(), new List<PointsRaceRunner>());
        }

        // Entrant → runner mapping (and latest display name) for the in-window events.
        var windowEventIds = inWindow.Select(e => e.Id).ToHashSet();
        var windowEntrants = db.Entrants
            .Where(e => windowEventIds.Contains(e.EventId) && e.RunnerId.HasValue)
            .ToList();
        var runnerByEntrantId = windowEntrants
            .GroupBy(e => e.Id)
            .ToDictionary(g => g.Key, g => g.First().RunnerId!.Value);
        var nameByRunner = windowEntrants
            .GroupBy(e => e.RunnerId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.EventId).First().Name);

        var running = new Dictionary<int, int>();
        var series = new Dictionary<int, int[]>();
        var allRunnerIds = new HashSet<int>();

        for (var i = 0; i < inWindow.Count; i++)
        {
            var raceEvent = inWindow[i];
            var audits = db.PointsAuditLogs
                .Where(a => a.EventId == raceEvent.Id && a.SeasonYear == year && a.Action != AuditAction.Voided)
                .ToList();

            var pointsThisEvent = new Dictionary<int, int>();
            if (audits.Count > 0)
            {
                var latestBatch = audits.Max(a => a.AuditTimestamp);
                foreach (var a in audits.Where(a => a.AuditTimestamp == latestBatch))
                {
                    if (runnerByEntrantId.TryGetValue(a.EntrantId, out var runnerId))
                    {
                        pointsThisEvent[runnerId] = pointsThisEvent.GetValueOrDefault(runnerId) + a.PointsAwarded;
                        allRunnerIds.Add(runnerId);
                    }
                }
            }

            // Carry every known runner forward so each series stays aligned to the event axis.
            foreach (var runnerId in allRunnerIds)
            {
                running[runnerId] = running.GetValueOrDefault(runnerId) + pointsThisEvent.GetValueOrDefault(runnerId);
                if (!series.TryGetValue(runnerId, out var arr))
                {
                    arr = new int[inWindow.Count];
                    series[runnerId] = arr;
                }

                arr[i] = running[runnerId];
            }
        }

        var runners = series
            .Select(kv => new PointsRaceRunner
            {
                RunnerName = nameByRunner.GetValueOrDefault(kv.Key, "Unknown"),
                CumulativePoints = kv.Value,
                Total = kv.Value.Length > 0 ? kv.Value[^1] : 0
            })
            .OrderByDescending(r => r.Total).ThenBy(r => r.RunnerName)
            .Take(8)
            .ToList();

        return (inWindow.Select(e => e.EventName).ToList(), runners);
    }

    public RunnerSeasonProfile? GetRunnerSeasonProfile(int runnerId, int year)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var runner = db.Runners.FirstOrDefault(r => r.Id == runnerId);
        if (runner is null)
        {
            return null;
        }

        var events = db.Events.Where(e => e.EventDate.Year == year).OrderBy(e => e.EventDate).ToList();
        var collatedByEvent = events.ToDictionary(e => e.Id, e => _raceResultsService.GetCollatedResults(e.Id));

        var attended = events.Select(e => db.Entrants.Any(x => x.EventId == e.Id && x.RunnerId == runnerId)).ToArray();

        var races = new List<RunnerRaceLine>();
        var overallPositions = new List<int>();
        var categoryPositions = new List<int>();
        var excludedTimeRows = 0;

        for (var i = 0; i < events.Count; i++)
        {
            var raceEvent = events[i];
            var collated = collatedByEvent[raceEvent.Id];
            var mine = collated.FirstOrDefault(r => r.Entrant?.RunnerId == runnerId);
            if (mine is null)
            {
                continue; // attended but did not finish, or did not attend
            }

            var category = CategoryOf(mine) ?? "—";
            races.Add(new RunnerRaceLine
            {
                EventName = raceEvent.EventName,
                EventDate = raceEvent.EventDate,
                EventType = raceEvent.EventType,
                Position = mine.DisplayPosition,
                Time = mine.Duration.HasValue ? RaceTime.Format(mine.Duration.Value) : null,
                Category = category
            });

            overallPositions.Add(mine.DisplayPosition);
            if (!mine.Duration.HasValue)
            {
                excludedTimeRows++;
            }

            // Rank within the runner's category for this event.
            var inCategory = collated.Where(r => CategoryOf(r) == category).OrderBy(r => r.DisplayPosition).ToList();
            var rank = inCategory.FindIndex(r => r.Entrant?.RunnerId == runnerId) + 1;
            if (rank > 0)
            {
                categoryPositions.Add(rank);
            }
        }

        var seasonBests = races
            .Where(r => r.Time is not null)
            .GroupBy(r => r.EventType)
            .Select(g =>
            {
                var best = g.OrderBy(r => ParseOrMax(r.Time)).First();
                return new SeasonBest { EventType = g.Key, Time = best.Time!, EventName = best.EventName };
            })
            .OrderBy(b => b.EventType)
            .ToList();

        return new RunnerSeasonProfile
        {
            Runner = runner,
            Year = year,
            EventsHeld = events.Count,
            Races = races,
            SeasonBests = seasonBests,
            AverageFinishPosition = overallPositions.Count > 0 ? Math.Round(overallPositions.Average(), 1) : null,
            AverageCategoryPosition = categoryPositions.Count > 0 ? Math.Round(categoryPositions.Average(), 1) : null,
            ChampionsProgression = BuildChampionsProgression(db, events, year, runnerId),
            CurrentStreak = CurrentStreak(attended),
            ExcludedTimeRows = excludedTimeRows
        };
    }

    private static List<PointsProgressionPoint> BuildChampionsProgression(
        RaceResultsDbContext db, List<RaceEvent> events, int year, int runnerId)
    {
        var c2cEvents = events
            .Where(e => e.EventType == EventType.CrownToCrown
                     && e.EventDate.Month >= ChampionsSeasonStartMonth
                     && e.EventDate.Month <= ChampionsSeasonEndMonth)
            .OrderBy(e => e.EventDate)
            .ToList();
        if (c2cEvents.Count == 0)
        {
            return new List<PointsProgressionPoint>();
        }

        var runnerEntrantIds = db.Entrants
            .Where(e => e.RunnerId == runnerId)
            .Select(e => e.Id)
            .ToHashSet();

        var progression = new List<PointsProgressionPoint>();
        var cumulative = 0;
        foreach (var raceEvent in c2cEvents)
        {
            var audits = db.PointsAuditLogs
                .Where(a => a.EventId == raceEvent.Id && a.SeasonYear == year && a.Action != AuditAction.Voided)
                .ToList();
            var points = 0;
            if (audits.Count > 0)
            {
                var latestBatch = audits.Max(a => a.AuditTimestamp);
                points = audits
                    .Where(a => a.AuditTimestamp == latestBatch && runnerEntrantIds.Contains(a.EntrantId))
                    .Sum(a => a.PointsAwarded);
            }

            cumulative += points;
            progression.Add(new PointsProgressionPoint
            {
                EventName = raceEvent.EventName,
                EventDate = raceEvent.EventDate,
                PointsThisEvent = points,
                CumulativePoints = cumulative
            });
        }

        return progression;
    }

    /// <summary>Consecutive attended events ending at the runner's most recently attended event.</summary>
    private static int CurrentStreak(bool[] attended)
    {
        var last = Array.LastIndexOf(attended, true);
        if (last < 0)
        {
            return 0;
        }

        var streak = 0;
        for (var i = last; i >= 0 && attended[i]; i--)
        {
            streak++;
        }

        return streak;
    }

    /// <summary>Category for time/category stats, or null for non Male/Female genders (excluded).</summary>
    private static string? CategoryOf(ResultRecord record)
    {
        var gender = record.Gender;
        string? baseCategory = gender.StartsWith("M", StringComparison.OrdinalIgnoreCase) ? "Male"
            : gender.StartsWith("F", StringComparison.OrdinalIgnoreCase) ? "Female"
            : null;
        if (baseCategory is null)
        {
            return null;
        }

        return record.IsU18 ? $"{baseCategory} U18" : baseCategory;
    }

    private static TimeSpan ParseOrMax(string? time) =>
        RaceTime.TryParse(time, out var d) ? d : TimeSpan.MaxValue;
}
