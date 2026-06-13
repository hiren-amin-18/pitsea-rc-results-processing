using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RaceResults.UnitTests.Helpers;
using RaceResults.Web.Data;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.UnitTests;

public class RunnerRegistryTests : IDisposable
{
    private static readonly string[] EntrantHeader = ["Bib", "Name", "Club", "Gender", "Age"];

    private readonly SqliteConnection _connection;
    private readonly IDbContextFactory<RaceResultsDbContext> _factory;
    private readonly RaceResultsService _raceService;
    private readonly RunnerRegistryService _registry;

    public RunnerRegistryTests()
    {
        var (factory, connection) = DbContextHelpers.CreateFactory();
        _factory = factory;
        _connection = connection;
        _raceService = new RaceResultsService(factory, NullLogger<RaceResultsService>.Instance);
        var champions = new ChampionsOfChampionsService(factory, _raceService);
        _registry = new RunnerRegistryService(factory, champions, NullLogger<RunnerRegistryService>.Instance);
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Upload_CreatesAndLinksRunners()
    {
        await UploadEntrants(("1", "Alice", "Club A", "Female"), ("2", "Bob", "Club B", "Male"));

        await using var db = _factory.CreateDbContext();
        Assert.Equal(2, db.Runners.Count());
        Assert.True(db.Entrants.All(e => e.RunnerId != null));
    }

    [Fact]
    public async Task ReuploadSameEntrants_DoesNotDuplicateRunners()
    {
        await UploadEntrants(("1", "Alice", "Club A", "Female"), ("2", "Bob", "Club B", "Male"));
        await UploadEntrants(("1", "Alice", "Club A", "Female"), ("2", "Bob", "Club B", "Male"));

        await using var db = _factory.CreateDbContext();
        Assert.Equal(2, db.Runners.Count());
    }

    [Fact]
    public async Task SameRunnerAcrossEvents_SharesOneRunner()
    {
        await UploadEntrants(("1", "Alice", "Club A", "Female"));
        await StartNewEvent("Event 2", new DateTime(2026, 6, 10));
        await UploadEntrants(("7", "Alice", "Club A", "Female")); // different bib, same person

        await using var db = _factory.CreateDbContext();
        Assert.Equal(1, db.Runners.Count());

        var item = Assert.Single(_registry.GetRunners());
        Assert.Equal(2, item.RaceCount);
    }

    [Fact]
    public async Task SameNameDifferentClub_WarnsAndCreatesSeparateRunner()
    {
        await UploadEntrants(("1", "Alice Smith", "Club A", "Female"));
        await StartNewEvent("Event 2", new DateTime(2026, 6, 10));
        var result = await UploadEntrants(("1", "Alice Smith", "Club B", "Female"));

        Assert.Contains(result.Warnings, w => w.Contains("looks similar"));
        await using var db = _factory.CreateDbContext();
        Assert.Equal(2, db.Runners.Count());
    }

    [Fact]
    public async Task MergeRunners_ReassignsEntrantsAndRemovesSource()
    {
        await UploadEntrants(("1", "Jon Smith", "Club A", "Male"));
        await StartNewEvent("Event 2", new DateTime(2026, 6, 10));
        await UploadEntrants(("1", "John Smith", "Club A", "Male")); // near match → separate runner

        var runners = _registry.GetRunners();
        var source = runners.First(r => r.Runner.Name == "Jon Smith").Runner;
        var target = runners.First(r => r.Runner.Name == "John Smith").Runner;

        var result = await _registry.MergeRunnersAsync(source.Id, target.Id);

        Assert.True(result.Success);
        var remaining = Assert.Single(_registry.GetRunners());
        Assert.Equal(target.Id, remaining.Runner.Id);
        Assert.Equal(2, remaining.RaceCount);
    }

    [Fact]
    public async Task UpdateRunner_PropagatesToEntrants()
    {
        await UploadEntrants(("1", "Alice", "Club A", "Female"));
        var runnerId = _registry.GetRunners().Single().Runner.Id;

        var result = await _registry.UpdateRunnerAsync(new EditRunnerInput
        {
            Id = runnerId,
            Name = "Alice Jones",
            Club = "New Club",
            Gender = "Female",
            Age = null,
            ExternalReference = "EA12345"
        });

        Assert.True(result.Success);
        await using var db = _factory.CreateDbContext();
        var entrant = db.Entrants.Single(e => e.RunnerId == runnerId);
        Assert.Equal("Alice Jones", entrant.Name);
        Assert.Equal("New Club", entrant.Club);
    }

    [Fact]
    public async Task DeleteEvent_KeepsRunnersAndFlagsInactive()
    {
        await UploadEntrants(("1", "Alice", "Club A", "Female"));
        var currentEventId = _raceService.GetCurrentEvent().Id;

        _raceService.DeleteEvent(currentEventId);

        await using var db = _factory.CreateDbContext();
        var runner = Assert.Single(db.Runners);
        Assert.False(runner.IsActive); // kept, but no remaining entrants
    }

    private async Task<OperationResult> UploadEntrants(params (string Bib, string Name, string Club, string Gender)[] rows)
    {
        var data = new List<string[]> { EntrantHeader };
        data.AddRange(rows.Select(r => new[] { r.Bib, r.Name, r.Club, r.Gender, "" }));
        return await _raceService.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx", data.ToArray())]);
    }

    private async Task StartNewEvent(string name, DateTime date)
    {
        _raceService.CreateEvent(new CreateEventInput { EventName = name, EventDate = date, EventType = EventType.CrownToCrown });
        var created = _raceService.GetEvents().First(e => e.EventName == name);
        _raceService.SetCurrentEvent(created.Id);
        await Task.CompletedTask;
    }
}
