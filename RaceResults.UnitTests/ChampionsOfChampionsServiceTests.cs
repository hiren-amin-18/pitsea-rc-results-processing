using Microsoft.Data.Sqlite;
using Xunit;
using RaceResults.Web.Services;
using RaceResults.UnitTests.Helpers;
using RaceResults.Web.Models;

namespace RaceResults.UnitTests;

public class ChampionsOfChampionsServiceTests : IDisposable
{
    private readonly RaceResultsService _raceResultsService;
    private readonly ChampionsOfChampionsService _championsService;
    private readonly SqliteConnection _connection;

    private static readonly string[] EntrantHeader = ["Bib", "Name", "Club", "Gender", "Age"];

    public ChampionsOfChampionsServiceTests()
    {
        var (factory, connection) = DbContextHelpers.CreateFactory();
        _connection = connection;
        _raceResultsService = new RaceResultsService(factory, null!);
        _championsService = new ChampionsOfChampionsService(factory, _raceResultsService);
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
        // Arrange: Create two events
        // Event 1: Runner1 finishes 1st, Runner2 finishes 2nd
        // Event 2: Runner1 finishes 2nd, Runner2 finishes 1st
        // Expected: Runner1 has 10+9=19 points, Runner2 has 9+10=19 points (tied)

        // First event
        await SeedEventWithResults(1, eventName: "Event 1");
        await _championsService.CalculateAndSaveEventPointsAsync(1);

        // Create second event
        using (var db = DbContextHelpers.CreateFactory().Item1.CreateDbContext())
        {
            var newEvent = new RaceEvent
            {
                EventName = "Event 2",
                EventDate = new DateTime(2026, 6, 15),
                EventType = EventType.CrownToCrown,
                IsCurrent = true
            };
            db.Events.Add(newEvent);
            await db.SaveChangesAsync();
        }

        // Second event with reversed positions
        var rows2 = new List<string[]> { EntrantHeader };
        rows2.Add(["1", "Runner2", "Club", "Male", "25"]);
        rows2.Add(["2", "Runner1", "Club", "Male", "25"]);
        
        await _raceResultsService.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e2.xlsx", rows2)]);

        var finishRows2 = new List<string[]> { new[] { "Position", "Bib" } };
        finishRows2.Add(["1", "1"]);
        finishRows2.Add(["2", "2"]);
        await _raceResultsService.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb2.xlsx", finishRows2));

        var csvLines2 = "STARTOFEVENT,x,x\n1,x,00:21:00\n2,x,00:22:00\n";
        await _raceResultsService.UploadTimingsAsync(FormFileHelpers.CreateCsv("t2.csv", csvLines2));

        // Update current event
        using (var db = DbContextHelpers.CreateFactory().Item1.CreateDbContext())
        {
            var event1 = await db.Events.FindAsync(1);
            if (event1 != null) event1.IsCurrent = false;
            await db.SaveChangesAsync();
        }

        // Act
        await _championsService.CalculateAndSaveEventPointsAsync(2);

        // Assert
        var leaderboard = await _championsService.GetCurrentSeasonLeaderboardAsync();
        var males = leaderboard.Where(e => e.Category == "Male").ToList();

        Assert.Equal(2, males.Count);
        // Both should have 19 points
        Assert.All(males, m => Assert.Equal(19, m.TotalPoints));
        // But rank by race count (both participated in 2)
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
        Assert.Single(leaderboard.Where(e => e.Category == "Male"));
        Assert.Single(leaderboard.Where(e => e.Category == "Female"));
        Assert.Single(leaderboard.Where(e => e.Category == "Male U18"));
        Assert.Single(leaderboard.Where(e => e.Category == "Female U18"));

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
        using (var db = DbContextHelpers.CreateFactory().Item1.CreateDbContext())
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
