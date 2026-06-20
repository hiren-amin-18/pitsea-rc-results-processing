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

    [Fact]
    public async Task SimilarClusters_GroupSameNameDifferentClub()
    {
        await UploadEntrants(("1", "Alice Smith", "Club A", "Female"), ("2", "Bob", "Club B", "Male"));
        await StartNewEvent("Event 2", new DateTime(2026, 6, 10));
        await UploadEntrants(("7", "Alice Smith", "Club Z", "Female"));

        var clusters = _registry.GetSimilarRunnerClusters();

        var cluster = Assert.Single(clusters);
        Assert.Equal(SimilarityReason.SameNameDifferentClub, cluster.Reason);
        Assert.Equal(2, cluster.Runners.Count);
        Assert.All(cluster.Runners, r => Assert.Equal("Alice Smith", r.Runner.Name));
    }

    [Fact]
    public async Task SimilarClusters_GroupFuzzyNameMatch()
    {
        // "Jon Smith" vs "John Smith" — Levenshtein distance 1.
        await UploadEntrants(("1", "Jon Smith", "Club A", "Male"), ("2", "Carol", "Club C", "Female"));
        await StartNewEvent("Event 2", new DateTime(2026, 6, 10));
        await UploadEntrants(("7", "John Smith", "Club A", "Male"));

        var clusters = _registry.GetSimilarRunnerClusters();

        var cluster = Assert.Single(clusters);
        Assert.Equal(SimilarityReason.FuzzyNameMatch, cluster.Reason);
        Assert.Equal(2, cluster.Runners.Count);
    }

    [Fact]
    public async Task SimilarClusters_IgnoreDifferentGender()
    {
        await UploadEntrants(("1", "Alex Jones", "Club A", "Female"));
        await StartNewEvent("Event 2", new DateTime(2026, 6, 10));
        await UploadEntrants(("7", "Alex Jones", "Club B", "Male"));

        Assert.Empty(_registry.GetSimilarRunnerClusters());
    }

    [Fact]
    public async Task MergeBatch_AppliesAllSelectedMerges()
    {
        await UploadEntrants(
            ("1", "Jon Smith", "Club A", "Male"),
            ("2", "John Smith", "Club A", "Male"),
            ("3", "Alice Jones", "Club X", "Female"));
        await StartNewEvent("Event 2", new DateTime(2026, 6, 10));
        await UploadEntrants(("7", "Alice Jones", "Club Y", "Female"));

        await using (var db = _factory.CreateDbContext())
        {
            Assert.Equal(4, db.Runners.Count());
        }

        var jonId = await GetRunnerIdByName("Jon Smith");
        var johnId = await GetRunnerIdByName("John Smith");
        var aliceXId = await GetRunnerIdByName("Alice Jones", "Club X");
        var aliceYId = await GetRunnerIdByName("Alice Jones", "Club Y");

        var result = await _registry.MergeRunnersBatchAsync(new[]
        {
            new ClusterMergeInput { TargetId = johnId, SourceIds = new List<int> { jonId } },
            new ClusterMergeInput { TargetId = aliceYId, SourceIds = new List<int> { aliceXId } },
        });

        Assert.True(result.Success);
        await using var db2 = _factory.CreateDbContext();
        Assert.Equal(2, db2.Runners.Count());
    }

    [Fact]
    public async Task MergeBatch_SkipsClustersWithNoSources()
    {
        await UploadEntrants(("1", "Alice", "Club A", "Female"), ("2", "Bob", "Club B", "Male"));
        var aliceId = await GetRunnerIdByName("Alice");
        var bobId = await GetRunnerIdByName("Bob");

        // Both clusters have a target but no checked sources — should be a no-op success.
        var result = await _registry.MergeRunnersBatchAsync(new[]
        {
            new ClusterMergeInput { TargetId = aliceId, SourceIds = new List<int>() },
            new ClusterMergeInput { TargetId = bobId, SourceIds = new List<int>() },
        });

        Assert.True(result.Success);
        await using var db = _factory.CreateDbContext();
        Assert.Equal(2, db.Runners.Count());
    }

    [Fact]
    public async Task MergeBatch_StopsAtFirstFailure()
    {
        await UploadEntrants(("1", "Alice", "Club A", "Female"), ("2", "Bob", "Club B", "Male"));
        var aliceId = await GetRunnerIdByName("Alice");
        var bobId = await GetRunnerIdByName("Bob");

        // First cluster's source equals its target → MergeRunnersAsync fails;
        // the second valid cluster must not be applied.
        var result = await _registry.MergeRunnersBatchAsync(new[]
        {
            new ClusterMergeInput { TargetId = 99999, SourceIds = new List<int> { aliceId } },
            new ClusterMergeInput { TargetId = bobId, SourceIds = new List<int> { aliceId } },
        });

        Assert.False(result.Success);
        await using var db = _factory.CreateDbContext();
        Assert.Equal(2, db.Runners.Count()); // nothing merged
    }

    [Fact]
    public async Task MergeBatch_RejectsSourcesWithoutTarget()
    {
        await UploadEntrants(("1", "Alice", "Club A", "Female"));
        var aliceId = await GetRunnerIdByName("Alice");

        var result = await _registry.MergeRunnersBatchAsync(new[]
        {
            new ClusterMergeInput { TargetId = 0, SourceIds = new List<int> { aliceId } },
        });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("select a runner to keep"));
    }

    [Fact]
    public async Task DismissPair_HidesPairFromClusters()
    {
        await UploadEntrants(("1", "Alex Jones", "Club A", "Female"));
        await StartNewEvent("Event 2", new DateTime(2026, 6, 10));
        await UploadEntrants(("7", "Alex Jones", "Club B", "Female"));

        Assert.Single(_registry.GetSimilarRunnerClusters());

        var aId = await GetRunnerIdByName("Alex Jones", "Club A");
        var bId = await GetRunnerIdByName("Alex Jones", "Club B");
        var result = await _registry.DismissPairAsync(aId, bId);

        Assert.True(result.Success);
        Assert.Empty(_registry.GetSimilarRunnerClusters());
        Assert.Equal(1, _registry.CountDismissedPairs());
    }

    [Fact]
    public async Task DismissPair_IsOrderInsensitive()
    {
        await UploadEntrants(("1", "Sam Hill", "Club A", "Male"));
        await StartNewEvent("Event 2", new DateTime(2026, 6, 10));
        await UploadEntrants(("7", "Sam Hill", "Club B", "Male"));

        var aId = await GetRunnerIdByName("Sam Hill", "Club A");
        var bId = await GetRunnerIdByName("Sam Hill", "Club B");

        await _registry.DismissPairAsync(bId, aId);
        var second = await _registry.DismissPairAsync(aId, bId);

        Assert.True(second.Success);
        Assert.Equal(1, _registry.CountDismissedPairs()); // not duplicated
    }

    [Fact]
    public async Task ClearDismissedPairs_RestoresClusters()
    {
        await UploadEntrants(("1", "Pat Lee", "Club A", "Female"));
        await StartNewEvent("Event 2", new DateTime(2026, 6, 10));
        await UploadEntrants(("7", "Pat Lee", "Club B", "Female"));

        var aId = await GetRunnerIdByName("Pat Lee", "Club A");
        var bId = await GetRunnerIdByName("Pat Lee", "Club B");
        await _registry.DismissPairAsync(aId, bId);
        Assert.Empty(_registry.GetSimilarRunnerClusters());

        await _registry.ClearDismissedPairsAsync();

        Assert.Equal(0, _registry.CountDismissedPairs());
        Assert.Single(_registry.GetSimilarRunnerClusters());
    }

    private async Task<int> GetRunnerIdByName(string name, string? club = null)
    {
        await using var db = _factory.CreateDbContext();
        var query = db.Runners.Where(r => r.Name == name);
        if (club is not null) query = query.Where(r => r.Club == club);
        return query.Select(r => r.Id).Single();
    }

    [Fact]
    public async Task SimilarClusters_EmptyWhenNoDuplicates()
    {
        await UploadEntrants(("1", "Alice", "Club A", "Female"), ("2", "Bob", "Club B", "Male"));

        Assert.Empty(_registry.GetSimilarRunnerClusters());
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
