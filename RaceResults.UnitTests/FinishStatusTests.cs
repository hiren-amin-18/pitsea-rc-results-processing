using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RaceResults.UnitTests.Helpers;
using RaceResults.Web.Data;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.UnitTests;

public class FinishStatusTests : IDisposable
{
    private static readonly string[] EntrantHeader = ["Bib", "Name", "Club", "Gender", "Age"];

    private readonly SqliteConnection _connection;
    private readonly IDbContextFactory<RaceResultsDbContext> _factory;
    private readonly RaceResultsService _service;
    private readonly ChampionsOfChampionsService _champions;

    public FinishStatusTests()
    {
        var (factory, connection) = DbContextHelpers.CreateFactory();
        _factory = factory;
        _connection = connection;
        _service = new RaceResultsService(factory, NullLogger<RaceResultsService>.Instance);
        _champions = new ChampionsOfChampionsService(factory, _service);

        // Move the seeded event into the Champions season window for the scoring test.
        using var db = _factory.CreateDbContext();
        db.Events.First(e => e.Id == 1).EventDate = new DateTime(2026, 6, 10);
        db.SaveChanges();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Disqualify_RemovesFromResultsAndClosesPositions()
    {
        await SeedThreeFinishers();

        var result = _service.DisqualifyResult(1, "Course cut");

        Assert.True(result.Success);
        var collated = _service.GetCollatedResults();
        Assert.DoesNotContain(collated, r => r.Position == 1);
        // Remaining finishers renumber from 1 for display, but keep their stored positions.
        Assert.Equal(new[] { 1, 2 }, collated.Select(r => r.DisplayPosition).ToArray());
        Assert.Equal(new[] { 2, 3 }, collated.Select(r => r.Position).ToArray());

        var dsq = Assert.Single(_service.GetDsqResults());
        Assert.Equal("Course cut", dsq.StatusReason);
    }

    [Fact]
    public async Task Disqualify_RequiresReason()
    {
        await SeedThreeFinishers();

        var result = _service.DisqualifyResult(1, "   ");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("reason is required"));
    }

    [Fact]
    public async Task Reinstate_RestoresFinisher()
    {
        await SeedThreeFinishers();
        _service.DisqualifyResult(1, "Mistake");

        var result = _service.ReinstateResult(1);

        Assert.True(result.Success);
        Assert.Contains(_service.GetCollatedResults(), r => r.Position == 1);
        Assert.Empty(_service.GetDsqResults());
    }

    [Fact]
    public async Task MarkDns_ExcludesFromDnfListAndStats()
    {
        // Two entrants, only one finishes; the other is a non-finisher (DNF by default).
        await _service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx",
        [
            EntrantHeader,
            ["1", "Alice", "Club A", "Female", ""],
            ["2", "Bob", "Club B", "Male", ""],
        ])]);
        await _service.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx", [["Position", "Bib"], ["1", "1"]]));
        await _service.UploadTimingsAsync(FormFileHelpers.CreateCsv("t.csv", "STARTOFEVENT,x,x\n1,x,00:20:00\n"));

        Assert.Contains(_service.GetDnfEntrants(), e => e.BibNumber == "2");
        var statsBefore = _service.GetRaceStats();
        Assert.Equal(1, statsBefore.TotalMales);

        var result = _service.SetNonFinisherStatus("2", FinishStatus.DidNotStart);

        Assert.True(result.Success);
        Assert.DoesNotContain(_service.GetDnfEntrants(), e => e.BibNumber == "2");
        Assert.Contains(_service.GetDnsEntrants(), e => e.BibNumber == "2");
        Assert.Equal(0, _service.GetRaceStats().TotalMales); // DNS excluded from stats
    }

    [Fact]
    public async Task DisqualifiedFinisher_EarnsNoChampionsPoints()
    {
        await SeedThreeFinishers();
        await _champions.CalculateAndSaveEventPointsAsync(1);

        // Bib 1 (Alice) is the female winner; confirm she scored, then disqualify her.
        var before = await _champions.GetCurrentSeasonLeaderboardAsync();
        Assert.Contains(before, e => e.Entrant.BibNumber == "1");

        _service.DisqualifyResult(1, "Course cut");
        await _champions.VoidDisqualifiedAndRecalculateAsync(1);

        var after = await _champions.GetCurrentSeasonLeaderboardAsync();
        Assert.DoesNotContain(after, e => e.Entrant.BibNumber == "1");
    }

    private async Task SeedThreeFinishers()
    {
        await _service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx",
        [
            EntrantHeader,
            ["1", "Alice", "Club A", "Female", ""],
            ["2", "Bob", "Club B", "Male", ""],
            ["3", "Cara", "Club C", "Female", ""],
        ])]);
        await _service.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx",
            [["Position", "Bib"], ["1", "1"], ["2", "2"], ["3", "3"]]));
        await _service.UploadTimingsAsync(FormFileHelpers.CreateCsv("t.csv",
            "STARTOFEVENT,x,x\n1,x,00:20:00\n2,x,00:21:00\n3,x,00:22:00\n"));
    }
}
