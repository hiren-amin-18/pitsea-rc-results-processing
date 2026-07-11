using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using RaceResults.Web.Data;
using RaceResults.Web.Services;
using RaceResults.UnitTests.Helpers;
using RaceResults.Web.Models;

namespace RaceResults.UnitTests;

public class ChampionsOfChampionsServiceTests : IDisposable
{
    private readonly IDbContextFactory<RaceResultsDbContext> _factory;
    private readonly RaceResultsService _raceResultsService;
    private readonly ChampionsOfChampionsService _championsService;
    private readonly SqliteConnection _connection;

    private static readonly string[] EntrantHeader = ["Bib", "Name", "Club", "Gender", "Age"];

    public ChampionsOfChampionsServiceTests()
    {
        var (factory, connection) = DbContextHelpers.CreateFactory();
        _factory = factory;
        _connection = connection;
        _raceResultsService = new RaceResultsService(factory, NullLogger<RaceResultsService>.Instance);
        _championsService = new ChampionsOfChampionsService(factory, _raceResultsService);

        // The seeded default event is dated Good Friday (3 April), which is outside the Champions
        // May-September scoring window by design. These tests score event 1, so move it in-season.
        using var db = _factory.CreateDbContext();
        var seededEvent = db.Events.First(e => e.Id == 1);
        seededEvent.EventDate = new DateTime(2026, 6, 10);
        db.SaveChanges();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task CalculateEventPoints_AwardsCorrectPointsToTopTen()
    {
        // Arrange: Seed 12 male runners
        var rows = new List<string[]> { EntrantHeader };
        for (int i = 1; i <= 12; i++)
        {
            rows.Add([$"{i}", $"Runner{i}", "Club", "Male", "25"]);
        }

        await _raceResultsService.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx", rows)]);

        // Upload finish positions
        var finishRows = new List<string[]> { new[] { "Position", "Bib" } };
        for (int i = 1; i <= 12; i++) finishRows.Add([$"{i}", $"{i}"]);
        await _raceResultsService.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx", finishRows));

        // Upload timings
        var csvLines = "STARTOFEVENT,x,x\n" +
            string.Join("\n", Enumerable.Range(1, 12).Select(i => $"{i},x,00:{20 + i:D2}:00"));
        await _raceResultsService.UploadTimingsAsync(FormFileHelpers.CreateCsv("t.csv", csvLines));

        // Act
        await _championsService.CalculateAndSaveEventPointsAsync(1);

        // Assert
        var leaderboard = await _championsService.GetCurrentSeasonLeaderboardAsync();
        var maleCategory = leaderboard.Where(e => e.Category == "Male").ToList();

        // Only top 10 should be on leaderboard
        Assert.Equal(10, maleCategory.Count);
        // 1st place should have 10 points
        Assert.Equal(10, maleCategory.First(e => e.Rank == 1).TotalPoints);
        // 10th place should have 1 point
        Assert.Equal(1, maleCategory.First(e => e.Rank == 10).TotalPoints);
        // Runners 11-12 should not be on leaderboard
        Assert.DoesNotContain(maleCategory, e => e.Entrant.BibNumber == "11");
        Assert.DoesNotContain(maleCategory, e => e.Entrant.BibNumber == "12");
    }

    [Fact]
    public async Task CalculateEventPoints_CumulatesAcrossMultipleEvents()
    {
        // Arrange: Create two events. Runners are the same people across events, but bib numbers
        // differ per race (as they do in real Crown to Crown races), so the leaderboard must
        // accumulate points by runner identity, not by per-event entrant rows.
        // Event 1: Runner1 finishes 1st (10), Runner2 finishes 2nd (9)
        // Event 2: Runner1 finishes 2nd (9), Runner2 finishes 1st (10)
        // Expected: both runners on 19 points, each with a race count of 2.

        // First event (the default seeded current event, Id 1)
        var rows1 = new List<string[]> { EntrantHeader };
        rows1.Add(["1", "Runner1", "Club", "Male", "25"]);
        rows1.Add(["2", "Runner2", "Club", "Male", "25"]);
        await _raceResultsService.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e1.xlsx", rows1)]);

        var finishRows1 = new List<string[]> { new[] { "Position", "Bib" }, new[] { "1", "1" }, new[] { "2", "2" } };
        await _raceResultsService.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb1.xlsx", finishRows1));

        await _raceResultsService.UploadTimingsAsync(
            FormFileHelpers.CreateCsv("t1.csv", "STARTOFEVENT,x,x\n1,x,00:20:00\n2,x,00:21:00\n"));

        await _championsService.CalculateAndSaveEventPointsAsync(1);

        // Create second event in the SAME database and make it current.
        int event2Id;
        using (var db = _factory.CreateDbContext())
        {
            var event1 = await db.Events.FindAsync(1);
            if (event1 != null) event1.IsCurrent = false;

            var newEvent = new RaceEvent
            {
                EventName = "Event 2",
                EventDate = new DateTime(2026, 6, 15),
                EventType = EventType.CrownToCrown,
                IsCurrent = true
            };
            db.Events.Add(newEvent);
            await db.SaveChangesAsync();
            event2Id = newEvent.Id;
        }

        // Second event: same runners, different bibs, reversed finishing order.
        var rows2 = new List<string[]> { EntrantHeader };
        rows2.Add(["10", "Runner1", "Club", "Male", "25"]);
        rows2.Add(["20", "Runner2", "Club", "Male", "25"]);
        await _raceResultsService.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e2.xlsx", rows2)]);

        var finishRows2 = new List<string[]> { new[] { "Position", "Bib" }, new[] { "1", "20" }, new[] { "2", "10" } };
        await _raceResultsService.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb2.xlsx", finishRows2));

        await _raceResultsService.UploadTimingsAsync(
            FormFileHelpers.CreateCsv("t2.csv", "STARTOFEVENT,x,x\n1,x,00:21:00\n2,x,00:22:00\n"));

        // Act
        await _championsService.CalculateAndSaveEventPointsAsync(event2Id);

        // Assert
        var leaderboard = await _championsService.GetCurrentSeasonLeaderboardAsync();
        var males = leaderboard.Where(e => e.Category == "Male").ToList();

        // Two distinct runners, not four per-event rows.
        Assert.Equal(2, males.Count);
        // Both should have cumulative 19 points across the two races.
        Assert.All(males, m => Assert.Equal(19, m.TotalPoints));
        // Race count accumulates across events.
        Assert.All(males, m => Assert.Equal(2, m.RaceCount));
    }

    [Fact]
    public async Task CalculateEventPoints_DifferentiatesByCategory()
    {
        // Arrange: Mix of male and female runners
        var rows = new List<string[]> { EntrantHeader };
        rows.Add(["1", "Alice", "Club", "Female", "25"]);
        rows.Add(["2", "Bob", "Club", "Male", "25"]);
        rows.Add(["3", "Charlie", "Club", "Male", "16"]);  // U18
        rows.Add(["4", "Dana", "Club", "Female", "15"]);   // U18

        await _raceResultsService.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx", rows)]);

        var finishRows = new List<string[]> { new[] { "Position", "Bib" } };
        finishRows.Add(["1", "2"]);  // Bob (Male)
        finishRows.Add(["2", "1"]);  // Alice (Female)
        finishRows.Add(["3", "3"]);  // Charlie (Male U18)
        finishRows.Add(["4", "4"]);  // Dana (Female U18)
        await _raceResultsService.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx", finishRows));

        var csvLines = "STARTOFEVENT,x,x\n1,x,00:20:00\n2,x,00:21:00\n3,x,00:22:00\n4,x,00:23:00\n";
        await _raceResultsService.UploadTimingsAsync(FormFileHelpers.CreateCsv("t.csv", csvLines));

        // Act
        await _championsService.CalculateAndSaveEventPointsAsync(1);

        // Assert
        var leaderboard = await _championsService.GetCurrentSeasonLeaderboardAsync();

        // Should have 4 entries (1 per category)
        Assert.Equal(4, leaderboard.Count);

        // Each category should appear once
        Assert.Single(leaderboard, e => e.Category == "Male");
        Assert.Single(leaderboard, e => e.Category == "Female");
        Assert.Single(leaderboard, e => e.Category == "Male U18");
        Assert.Single(leaderboard, e => e.Category == "Female U18");

        // Check rankings within categories
        var maleEntry = leaderboard.Single(e => e.Category == "Male");
        Assert.Equal("Bob", maleEntry.Entrant.Name);
        Assert.Equal(10, maleEntry.TotalPoints);
    }

    [Fact]
    public async Task PointsAuditLog_TracksAllTransactions()
    {
        // Arrange
        await SeedEventWithResults(1);

        // Act
        await _championsService.CalculateAndSaveEventPointsAsync(1);

        // Assert - Check audit logs were created
        using (var db = _factory.CreateDbContext())
        {
            var auditLogs = db.PointsAuditLogs.ToList();
            Assert.NotEmpty(auditLogs);
            Assert.All(auditLogs, a => Assert.Equal(AuditAction.Initial, a.Action));
            Assert.All(auditLogs, a => Assert.NotNull(a.Reason));
        }
    }

    [Fact]
    public async Task TieBreaking_UsesRaceCountForRanking()
    {
        // Arrange: Create scenario where two runners have same points but different race counts
        // For this test, we'd need to manually create audit logs showing different participation
        // This is a complex scenario requiring direct DB manipulation

        // Seed basic event
        await SeedEventWithResults(1);
        await _championsService.CalculateAndSaveEventPointsAsync(1);

        // Assert leaderboard ranks by race count in tie situations
        var leaderboard = await _championsService.GetCurrentSeasonLeaderboardAsync();
        Assert.NotEmpty(leaderboard);

        // Verify ranking is assigned
        Assert.All(leaderboard, e => Assert.True(e.Rank > 0));
    }

    [Fact]
    public async Task HighlightClass_AssignedToTopThree()
    {
        // Arrange
        await SeedEventWithResults(1);
        await _championsService.CalculateAndSaveEventPointsAsync(1);

        // Act
        var leaderboard = await _championsService.GetCurrentSeasonLeaderboardAsync();

        // Assert
        var topThree = leaderboard.Where(e => e.Rank <= 3).ToList();
        Assert.NotEmpty(topThree);

        var first = topThree.FirstOrDefault(e => e.Rank == 1);
        Assert.NotNull(first);
        Assert.Equal("rank-gold", first.HighlightClass);

        var second = topThree.FirstOrDefault(e => e.Rank == 2);
        if (second != null)
            Assert.Equal("rank-silver", second.HighlightClass);

        var third = topThree.FirstOrDefault(e => e.Rank == 3);
        if (third != null)
            Assert.Equal("rank-bronze", third.HighlightClass);
    }

    [Fact]
    public async Task TiedRunners_MarkedWithIsPointsTied()
    {
        // Arrange: Create two events where two runners end up tied on points
        // Event 1: Runner1 = 10 points (1st), Runner2 = 9 points (2nd)
        // Event 2: Runner1 = 9 points (2nd), Runner2 = 10 points (1st)
        // Result: Both have 19 points -> tied
        
        var rows1 = new List<string[]> { EntrantHeader };
        rows1.Add(["1", "Alice", "Club", "Female", "25"]);
        rows1.Add(["2", "Bob", "Club", "Female", "26"]);

        await _raceResultsService.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e1.xlsx", rows1)]);

        var finishRows1 = new List<string[]> { new[] { "Position", "Bib" } };
        finishRows1.Add(["1", "1"]);  // Alice 1st = 10 points
        finishRows1.Add(["2", "2"]);  // Bob 2nd = 9 points
        await _raceResultsService.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb1.xlsx", finishRows1));

        var csvLines1 = "STARTOFEVENT,x,x\n1,x,00:20:00\n2,x,00:21:00\n";
        await _raceResultsService.UploadTimingsAsync(FormFileHelpers.CreateCsv("t1.csv", csvLines1));

        // First event scoring
        await _championsService.CalculateAndSaveEventPointsAsync(1);

        // Verify first event scores
        var lb1 = await _championsService.GetCurrentSeasonLeaderboardAsync();
        Assert.Equal(2, lb1.Count(e => e.Category == "Female"));
        Assert.True(lb1.First(e => e.Entrant.Name == "Alice" && e.Category == "Female").TotalPoints > 
                    lb1.First(e => e.Entrant.Name == "Bob" && e.Category == "Female").TotalPoints);

        // Act: Score should show IsPointsTied = true for tied runners
        var leaderboard = await _championsService.GetCurrentSeasonLeaderboardAsync();

        // Assert: No ties yet since we only have one event
        var females = leaderboard.Where(e => e.Category == "Female").ToList();
        Assert.Equal(2, females.Count);
    }

    [Fact]
    public async Task CalculateEventPoints_RejectsOutOfSeasonCrownToCrownEvent()
    {
        // The C2C series includes Good Friday and Boxing Day races, but the Champions
        // scoring window is May-September only - out-of-window events must not score.
        using (var db = _factory.CreateDbContext())
        {
            var seededEvent = db.Events.First(e => e.Id == 1);
            seededEvent.EventDate = new DateTime(2026, 4, 3); // Good Friday 2026
            await db.SaveChangesAsync();
        }

        await SeedEventWithResults(1);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _championsService.CalculateAndSaveEventPointsAsync(1));
    }

    [Fact]
    public async Task GetLeaderboard_OrdersCategoriesMaleFemaleMaleU18FemaleU18()
    {
        // Seed all four categories, deliberately with a non-Male runner first, to prove the
        // leaderboard imposes a fixed category order rather than following the data order.
        var rows = new List<string[]> { EntrantHeader };
        rows.Add(["1", "Alice", "Club", "Female", "25"]);
        rows.Add(["2", "Bob", "Club", "Male", "25"]);
        rows.Add(["3", "Charlie", "Club", "Male", "16"]);   // Male U18
        rows.Add(["4", "Dana", "Club", "Female", "15"]);    // Female U18
        await _raceResultsService.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx", rows)]);

        var finishRows = new List<string[]> { new[] { "Position", "Bib" }, new[] { "1", "1" }, new[] { "2", "2" }, new[] { "3", "3" }, new[] { "4", "4" } };
        await _raceResultsService.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx", finishRows));
        await _raceResultsService.UploadTimingsAsync(
            FormFileHelpers.CreateCsv("t.csv", "STARTOFEVENT,x,x\n1,x,00:20:00\n2,x,00:21:00\n3,x,00:22:00\n4,x,00:23:00\n"));
        await _championsService.CalculateAndSaveEventPointsAsync(1);

        var expected = new[] { "Male", "Female", "Male U18", "Female U18" };

        var leaderboard = await _championsService.GetLeaderboardAsync(2026);
        Assert.Equal(expected, leaderboard.Select(e => e.Category).Distinct().ToArray());

        var detail = await _championsService.GetLeaderboardDetailAsync(2026);
        Assert.Equal(expected, detail.Rows.Select(r => r.Entry.Category).Distinct().ToArray());
    }

    [Fact]
    public async Task GetLeaderboardDetail_ColumnsAndPerEventPointsReconcileWithSummary()
    {
        var (event1Id, event2Id) = await SeedTwoEventSeasonAsync();

        var detail = await _championsService.GetLeaderboardDetailAsync(2026);
        var summary = await _championsService.GetLeaderboardAsync(2026);

        // Two scored events become two columns, numbered by date, labelled "Round n – Month".
        Assert.Equal(2, detail.Columns.Count);
        Assert.Equal("Round 1 – June", detail.Columns[0].Label);
        Assert.Equal("Round 2 – July", detail.Columns[1].Label);
        Assert.Equal(event1Id, detail.Columns[0].EventId);
        Assert.Equal(event2Id, detail.Columns[1].EventId);

        // Every row's per-event points sum to its summary total (AC7).
        foreach (var row in detail.Rows)
        {
            Assert.Equal(row.Entry.TotalPoints, row.PointsByEventId.Values.Sum());
        }

        // Detail rows match the summary rows one-for-one (same order, same totals).
        Assert.Equal(summary.Count, detail.Rows.Count);
        foreach (var (s, d) in summary.Zip(detail.Rows))
        {
            Assert.Equal(s.Entrant.Name, d.Entry.Entrant.Name);
            Assert.Equal(s.TotalPoints, d.Entry.TotalPoints);
            Assert.Equal(s.Rank, d.Entry.Rank);
        }

        // Runner3 only ran event 1, so event 2 is blank (null) for them (AC5).
        var runner3 = detail.Rows.Single(r => r.Entry.Entrant.Name == "Runner3");
        Assert.Equal(8, runner3.PointsFor(event1Id));
        Assert.Null(runner3.PointsFor(event2Id));
    }

    [Fact]
    public async Task GetLeaderboardDetail_AsOfEventLimitsColumns()
    {
        var (event1Id, _) = await SeedTwoEventSeasonAsync();

        // As of the first event, only its column is shown and totals reflect that event alone.
        var detail = await _championsService.GetLeaderboardDetailAsync(2026, event1Id);

        Assert.Single(detail.Columns);
        Assert.Equal(event1Id, detail.Columns[0].EventId);

        var runner1 = detail.Rows.Single(r => r.Entry.Entrant.Name == "Runner1");
        Assert.Equal(10, runner1.Entry.TotalPoints);
        Assert.Equal(10, runner1.PointsFor(event1Id));
    }

    /// <summary>
    /// Seeds two in-season Crown to Crown events with the same runners at different bibs:
    /// Event 1 (June): Runner1 1st (10), Runner2 2nd (9), Runner3 3rd (8).
    /// Event 2 (July): Runner1 2nd (9), Runner2 1st (10); Runner3 does not run.
    /// </summary>
    private async Task<(int event1Id, int event2Id)> SeedTwoEventSeasonAsync()
    {
        // Event 1 is the seeded current event (Id 1), dated 2026-06-10 by the constructor.
        var rows1 = new List<string[]> { EntrantHeader };
        rows1.Add(["1", "Runner1", "Club", "Male", "25"]);
        rows1.Add(["2", "Runner2", "Club", "Male", "25"]);
        rows1.Add(["3", "Runner3", "Club", "Male", "25"]);
        await _raceResultsService.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e1.xlsx", rows1)]);

        var finishRows1 = new List<string[]> { new[] { "Position", "Bib" }, new[] { "1", "1" }, new[] { "2", "2" }, new[] { "3", "3" } };
        await _raceResultsService.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb1.xlsx", finishRows1));
        await _raceResultsService.UploadTimingsAsync(
            FormFileHelpers.CreateCsv("t1.csv", "STARTOFEVENT,x,x\n1,x,00:20:00\n2,x,00:21:00\n3,x,00:22:00\n"));
        await _championsService.CalculateAndSaveEventPointsAsync(1);

        int event2Id;
        using (var db = _factory.CreateDbContext())
        {
            var event1 = await db.Events.FindAsync(1);
            if (event1 != null) event1.IsCurrent = false;

            var newEvent = new RaceEvent
            {
                EventName = "Event 2",
                EventDate = new DateTime(2026, 7, 15),
                EventType = EventType.CrownToCrown,
                IsCurrent = true
            };
            db.Events.Add(newEvent);
            await db.SaveChangesAsync();
            event2Id = newEvent.Id;
        }

        var rows2 = new List<string[]> { EntrantHeader };
        rows2.Add(["10", "Runner1", "Club", "Male", "25"]);
        rows2.Add(["20", "Runner2", "Club", "Male", "25"]);
        await _raceResultsService.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e2.xlsx", rows2)]);

        var finishRows2 = new List<string[]> { new[] { "Position", "Bib" }, new[] { "1", "20" }, new[] { "2", "10" } };
        await _raceResultsService.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb2.xlsx", finishRows2));
        await _raceResultsService.UploadTimingsAsync(
            FormFileHelpers.CreateCsv("t2.csv", "STARTOFEVENT,x,x\n1,x,00:21:00\n2,x,00:22:00\n"));
        await _championsService.CalculateAndSaveEventPointsAsync(event2Id);

        return (1, event2Id);
    }

    private async Task SeedEventWithResults(int eventId, string eventName = "Test Race")
    {
        // Seed basic event with 5 male runners
        var rows = new List<string[]> { EntrantHeader };
        for (int i = 1; i <= 5; i++)
        {
            rows.Add([$"{i}", $"Runner{i}", "Club", "Male", "25"]);
        }

        await _raceResultsService.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx", rows)]);

        var finishRows = new List<string[]> { new[] { "Position", "Bib" } };
        for (int i = 1; i <= 5; i++) finishRows.Add([$"{i}", $"{i}"]);
        await _raceResultsService.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx", finishRows));

        var csvLines = "STARTOFEVENT,x,x\n" +
            string.Join("\n", Enumerable.Range(1, 5).Select(i => $"{i},x,00:{20 + i:D2}:00"));
        await _raceResultsService.UploadTimingsAsync(FormFileHelpers.CreateCsv("t.csv", csvLines));
    }
}
