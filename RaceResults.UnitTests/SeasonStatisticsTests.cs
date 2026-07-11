using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RaceResults.UnitTests.Helpers;
using RaceResults.Web.Data;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.UnitTests;

public class SeasonStatisticsTests : IDisposable
{
    private static readonly string[] EntrantHeader = ["Bib", "Name", "Club", "Gender", "Age"];

    private readonly SqliteConnection _connection;
    private readonly IDbContextFactory<RaceResultsDbContext> _factory;
    private readonly RaceResultsService _raceService;
    private readonly SeasonStatisticsService _season;

    public SeasonStatisticsTests()
    {
        var (factory, connection) = DbContextHelpers.CreateFactory();
        _factory = factory;
        _connection = connection;
        _raceService = new RaceResultsService(factory, NullLogger<RaceResultsService>.Instance);
        _season = new SeasonStatisticsService(factory, _raceService);
    }

    public void Dispose() => _connection.Dispose();

    // Two-event 2026 season: Alice & Bob run both; Carol runs only the second.
    private async Task SeedTwoEventSeason()
    {
        // Event 1 — the seeded default event (Crown to Crown, 3 April 2026).
        await Upload(("1", "Alice", "Club A", "Female", "00:20:00"), ("2", "Bob", "Club B", "Male", "00:22:00"));

        // Event 2 — June 2026, Crown to Crown.
        _raceService.CreateEvent(new CreateEventInput { EventName = "June Race", EventDate = new DateTime(2026, 6, 10), EventType = EventType.CrownToCrown });
        _raceService.SetCurrentEvent(_raceService.GetEvents().First(e => e.EventName == "June Race").Id);
        await Upload(("1", "Alice", "Club A", "Female", "00:19:00"), ("2", "Bob", "Club B", "Male", "00:23:00"), ("3", "Carol", "Club A", "Female", "00:25:00"));
    }

    private async Task Upload(params (string Bib, string Name, string Club, string Gender, string Time)[] rows)
    {
        var entrantRows = new List<string[]> { EntrantHeader };
        var finishRows = new List<string[]> { new[] { "Position", "Bib" } };
        var timing = "STARTOFEVENT,x,x\n";
        for (var i = 0; i < rows.Length; i++)
        {
            entrantRows.Add([rows[i].Bib, rows[i].Name, rows[i].Club, rows[i].Gender, ""]);
            finishRows.Add([(i + 1).ToString(), rows[i].Bib]);
            timing += $"{i + 1},x,{rows[i].Time}\n";
        }

        await _raceService.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx", entrantRows.ToArray())]);
        await _raceService.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx", finishRows.ToArray()));
        await _raceService.UploadTimingsAsync(FormFileHelpers.CreateCsv("t.csv", timing));
    }

    [Fact]
    public async Task Dashboard_MostAttended_FlagsEverPresentRunners()
    {
        await SeedTwoEventSeason();

        var dashboard = _season.GetSeasonDashboard(2026);

        Assert.Equal(2, dashboard.EventCount);
        Assert.Equal(3, dashboard.TotalUniqueRunners);
        // Alice and Bob attended both events (joint most attended, ever-present).
        Assert.Equal(2, dashboard.MostAttendedRunners.Count);
        Assert.All(dashboard.MostAttendedRunners, r => Assert.True(r.EverPresent));
        Assert.All(dashboard.MostAttendedRunners, r => Assert.Equal(2, r.EventsAttended));
    }

    [Fact]
    public async Task Dashboard_FastestByCategory_PicksBestAcrossSameType()
    {
        await SeedTwoEventSeason();

        var dashboard = _season.GetSeasonDashboard(2026);

        var fastestFemale = dashboard.FastestByCategory.Single(f => f.EventType == EventType.CrownToCrown && f.Category == "Female");
        Assert.Equal("19:00", fastestFemale.Time); // Alice's June time
        Assert.Equal("Alice", fastestFemale.RunnerName);

        var fastestMale = dashboard.FastestByCategory.Single(f => f.EventType == EventType.CrownToCrown && f.Category == "Male");
        Assert.Equal("22:00", fastestMale.Time); // Bob's April time
    }

    [Fact]
    public async Task Dashboard_MostImproved_PicksBiggestSameTypeImprovement()
    {
        await SeedTwoEventSeason();

        var dashboard = _season.GetSeasonDashboard(2026);

        var improved = Assert.Single(dashboard.MostImprovedByType, m => m.EventType == EventType.CrownToCrown);
        Assert.Equal("Alice", improved.RunnerName); // 20:00 → 19:00
        Assert.Equal("-01:00", improved.Improvement);
    }

    [Fact]
    public async Task Dashboard_Participation_CountsFirstTimersPerEvent()
    {
        await SeedTwoEventSeason();

        var dashboard = _season.GetSeasonDashboard(2026);

        var april = dashboard.Participation.Single(p => p.EventDate.Month == 4);
        var june = dashboard.Participation.Single(p => p.EventDate.Month == 6);
        Assert.Equal(2, april.FirstTimers); // Alice + Bob debut
        Assert.Equal(1, june.FirstTimers);  // only Carol is new
        Assert.Equal(3, june.Finishers);
        Assert.Equal(0, dashboard.SeasonDnfRatePercent);
    }

    [Fact]
    public async Task C2CSeasonStats_BuildsSeasonToDateTrend()
    {
        await SeedTwoEventSeason();

        var stats = _season.GetC2CSeasonStats(2026);

        Assert.True(stats.HasData);
        Assert.Equal(2, stats.EventsRun);
        Assert.Equal(2, stats.EventsScheduled);
        Assert.Equal(3, stats.UniqueRunners);
        Assert.Equal(2, stats.EverPresentCount);       // Alice & Bob at both events
        Assert.Equal(2, stats.Events.Count);

        var april = stats.Events.Single(e => e.EventDate.Month == 4);
        var june = stats.Events.Single(e => e.EventDate.Month == 6);
        Assert.Equal(2, april.FirstTimers);            // Alice + Bob debut
        Assert.Equal(0, april.ReturningRunners);
        Assert.Equal(1, june.FirstTimers);             // only Carol is new
        Assert.Equal(2, june.ReturningRunners);        // Alice + Bob return
        Assert.Equal(2, april.Finishers);
        Assert.Equal(3, june.Finishers);
        Assert.Equal(3, june.CumulativeUniqueRunners);
        Assert.Equal(1200, april.WinnerSeconds);       // Alice 20:00
        Assert.Equal(1140, june.WinnerSeconds);        // Alice 19:00
        Assert.False(stats.HasPointsData);             // no audit-log points seeded
    }

    [Fact]
    public async Task C2CSeasonStats_AnchorsToCurrentEvent_SeasonToDate()
    {
        await SeedTwoEventSeason();
        // Rewind the "current" event to the April race: June should drop out of the season-to-date view.
        var aprilId = _raceService.GetEvents().First(e => e.EventDate.Month == 4).Id;
        _raceService.SetCurrentEvent(aprilId);

        var stats = _season.GetC2CSeasonStats(2026);

        Assert.Equal(1, stats.EventsRun);
        Assert.Equal(2, stats.EventsScheduled);        // June is scheduled but not yet run
        Assert.Single(stats.Events);
        Assert.Equal(2, stats.UniqueRunners);          // only Alice & Bob so far
    }

    [Fact]
    public async Task C2CSeasonStats_ExcludesBluebell_AndBuildsAttendanceDistribution()
    {
        await SeedTwoEventSeason();
        // A Bluebell event (different type) with a new runner must not leak into C2C season stats.
        _raceService.CreateEvent(new CreateEventInput { EventName = "Bluebell", EventDate = new DateTime(2026, 7, 1), EventType = EventType.Bluebell5 });
        _raceService.SetCurrentEvent(_raceService.GetEvents().First(e => e.EventName == "Bluebell").Id);
        await Upload(("9", "Dave", "Club C", "Male", "00:30:00"));

        var stats = _season.GetC2CSeasonStats(2026);

        Assert.Equal(2, stats.EventsScheduled);        // C2C only
        Assert.Equal(2, stats.EventsRun);              // April + June (both before the July cutoff)
        Assert.Equal(3, stats.UniqueRunners);          // Dave (Bluebell) excluded
        Assert.DoesNotContain(stats.Events, e => e.EventName == "Bluebell");

        var oneEvent = stats.AttendanceDistribution.Single(b => b.Events == 1);
        var twoEvents = stats.AttendanceDistribution.Single(b => b.Events == 2);
        Assert.Equal(1, oneEvent.Runners);             // Carol
        Assert.Equal(2, twoEvents.Runners);            // Alice, Bob
    }

    [Fact]
    public async Task RunnerProfile_AggregatesRacesBestPositionAndStreak()
    {
        await SeedTwoEventSeason();
        var aliceId = _season.GetSeasonDashboard(2026).MostAttendedRunners.First(r => r.RunnerName == "Alice").RunnerId;

        var profile = _season.GetRunnerSeasonProfile(aliceId, 2026)!;

        Assert.Equal(2, profile.Races.Count);
        Assert.Equal(1.0, profile.AverageFinishPosition); // won both
        Assert.Equal(2, profile.CurrentStreak);
        var best = Assert.Single(profile.SeasonBests, b => b.EventType == EventType.CrownToCrown);
        Assert.Equal("19:00", best.Time);
    }

    [Fact]
    public async Task RunnerProfile_SingleRaceRunner_HasStreakOne()
    {
        await SeedTwoEventSeason();
        var carolId = _season.GetSeasonDashboard(2026).Participation.Count > 0
            ? _factory.CreateDbContext().Runners.First(r => r.Name == "Carol").Id
            : 0;

        var profile = _season.GetRunnerSeasonProfile(carolId, 2026)!;

        Assert.Single(profile.Races);
        Assert.Equal(1, profile.CurrentStreak);
        Assert.Equal(3.0, profile.AverageFinishPosition);
    }
}
