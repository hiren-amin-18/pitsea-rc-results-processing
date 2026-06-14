using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestPDF.Infrastructure;
using RaceResults.UnitTests.Helpers;
using RaceResults.Web.Data;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.UnitTests;

public class SeasonReviewTests : IDisposable
{
    static SeasonReviewTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static readonly string[] EntrantHeader = ["Bib", "Name", "Club", "Gender", "Age"];

    private readonly SqliteConnection _connection;
    private readonly IDbContextFactory<RaceResultsDbContext> _factory;
    private readonly RaceResultsService _raceService;
    private readonly ChampionsOfChampionsService _championsService;
    private readonly SeasonStatisticsService _seasonStats;
    private readonly SeasonReviewService _review;

    public SeasonReviewTests()
    {
        var (factory, connection) = DbContextHelpers.CreateFactory();
        _factory = factory;
        _connection = connection;
        _raceService = new RaceResultsService(factory, NullLogger<RaceResultsService>.Instance);
        _championsService = new ChampionsOfChampionsService(factory, _raceService);
        _seasonStats = new SeasonStatisticsService(factory, _raceService);
        _review = new SeasonReviewService(factory, _seasonStats, _championsService);
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void Build_NoEventsInYear_ReturnsZeroHeadlinesAndNoComparison()
    {
        var review = _review.Build(2025);
        Assert.Equal(0, review.Headlines.EventsHeld);
        Assert.Null(review.Comparison);
        Assert.False(review.HasVolunteerData); // graceful degradation: US29 unimplemented
    }

    [Fact]
    public async Task Build_WithRace_AggregatesHeadlinesAndAwards()
    {
        // The seeded event is Crown to Crown, 3 April 2026 — outside the Champions scoring window.
        // Move it into season so Champions points are recorded.
        using (var db = _factory.CreateDbContext())
        {
            db.Events.First(e => e.Id == 1).EventDate = new DateTime(2026, 6, 10);
            db.SaveChanges();
        }
        await SeedTwoFinishers();
        await _championsService.CalculateAndSaveEventPointsAsync(1);

        var review = _review.Build(2026);

        Assert.Equal(1, review.Headlines.EventsHeld);
        Assert.Equal(2, review.Headlines.TotalEntrants);
        Assert.Equal(2, review.Headlines.TotalFinishers);
        Assert.Equal(100.0, review.Headlines.CompletionRatePercent);

        // Awards reflect the same data as the Champions service.
        Assert.NotEmpty(review.Awards.ChampionsWinners);
        var female = Assert.Single(review.Awards.ChampionsWinners, a => a.Title.Contains("Female"));
        Assert.Equal("Alice", female.Winner);

        // Ever-present: 1 event, both runners attended it.
        Assert.Equal(2, review.Awards.EverPresentRunners.Count);
    }

    [Fact]
    public async Task GeneratePdf_ReturnsPdfBytes()
    {
        await SeedTwoFinishers();
        var bytes = _review.GeneratePdf(2026);
        Assert.Equal(0x25, bytes[0]); // %PDF
        Assert.True(bytes.Length > 1000);
    }

    private async Task SeedTwoFinishers()
    {
        await _raceService.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx",
        [
            EntrantHeader,
            ["1", "Alice", "Club A", "Female", ""],
            ["2", "Bob", "Club B", "Male", ""],
        ])]);
        await _raceService.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx",
            [["Position", "Bib"], ["1", "1"], ["2", "2"]]));
        await _raceService.UploadTimingsAsync(FormFileHelpers.CreateCsv("t.csv",
            "STARTOFEVENT,x,x\n1,x,00:20:00\n2,x,00:21:00\n"));
    }
}
