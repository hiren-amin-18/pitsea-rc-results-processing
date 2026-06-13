using Microsoft.EntityFrameworkCore;
using RaceResults.Web.Data;
using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

public class RunnerRegistryService : IRunnerRegistryService
{
    private readonly IDbContextFactory<RaceResultsDbContext> _dbContextFactory;
    private readonly IChampionsOfChampionsService _championsService;
    private readonly ILogger<RunnerRegistryService> _logger;

    public RunnerRegistryService(
        IDbContextFactory<RaceResultsDbContext> dbContextFactory,
        IChampionsOfChampionsService championsService,
        ILogger<RunnerRegistryService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _championsService = championsService;
        _logger = logger;
    }

    public IReadOnlyList<RunnerListItem> GetRunners()
    {
        using var db = _dbContextFactory.CreateDbContext();

        var raceCounts = db.Entrants
            .Where(e => e.RunnerId != null)
            .GroupBy(e => e.RunnerId!.Value)
            .Select(g => new { RunnerId = g.Key, Count = g.Count() })
            .ToDictionary(x => x.RunnerId, x => x.Count);

        return db.Runners
            .OrderBy(r => r.Name)
            .ToList()
            .Select(r => new RunnerListItem
            {
                Runner = r,
                RaceCount = raceCounts.TryGetValue(r.Id, out var count) ? count : 0
            })
            .ToList();
    }

    public bool TryGetRunnerForEdit(int id, out EditRunnerInput input)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var runner = db.Runners.FirstOrDefault(r => r.Id == id);
        if (runner is null)
        {
            input = new EditRunnerInput();
            return false;
        }

        input = new EditRunnerInput
        {
            Id = runner.Id,
            Name = runner.Name,
            Club = runner.Club,
            Gender = runner.Gender,
            Age = runner.Age,
            ExternalReference = runner.ExternalReference
        };
        return true;
    }

    public async Task<OperationResult> UpdateRunnerAsync(EditRunnerInput input)
    {
        var name = input.Name?.Trim() ?? string.Empty;
        var gender = input.Gender?.Trim() ?? string.Empty;
        var club = input.Club?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            return OperationResult.Fail(new[] { "Name is required." });
        }
        if (string.IsNullOrWhiteSpace(gender))
        {
            return OperationResult.Fail(new[] { "Gender is required." });
        }

        await using var db = _dbContextFactory.CreateDbContext();
        var runner = await db.Runners.FirstOrDefaultAsync(r => r.Id == input.Id);
        if (runner is null)
        {
            return OperationResult.Fail(new[] { "Runner not found." });
        }

        runner.Name = name;
        runner.Club = club;
        runner.Gender = gender;
        runner.Age = input.Age;
        runner.ExternalReference = string.IsNullOrWhiteSpace(input.ExternalReference) ? null : input.ExternalReference!.Trim();

        // Keep the per-event entrant snapshots in step with the canonical runner so display and
        // category-based scoring stay consistent (bib numbers remain per-event).
        var entrants = await db.Entrants.Where(e => e.RunnerId == runner.Id).ToListAsync();
        foreach (var entrant in entrants)
        {
            entrant.Name = name;
            entrant.Club = club;
            entrant.Gender = gender;
            entrant.Age = input.Age;
        }

        await db.SaveChangesAsync();

        await RecalculateSeasonsForRunnersAsync(db, new[] { runner.Id });
        _logger.LogInformation("Runner {RunnerId} updated; {Count} entrant row(s) synced.", runner.Id, entrants.Count);
        return OperationResult.Ok($"Runner '{runner.Name}' updated.");
    }

    public async Task<OperationResult> MergeRunnersAsync(int sourceId, int targetId)
    {
        if (sourceId == targetId)
        {
            return OperationResult.Fail(new[] { "Choose two different runners to merge." });
        }

        await using var db = _dbContextFactory.CreateDbContext();
        var source = await db.Runners.FirstOrDefaultAsync(r => r.Id == sourceId);
        var target = await db.Runners.FirstOrDefaultAsync(r => r.Id == targetId);
        if (source is null || target is null)
        {
            return OperationResult.Fail(new[] { "One or both runners were not found." });
        }

        var sourceEntrants = await db.Entrants.Where(e => e.RunnerId == sourceId).ToListAsync();
        foreach (var entrant in sourceEntrants)
        {
            entrant.RunnerId = targetId;
        }

        await db.SaveChangesAsync();

        // Source now has no entrants referencing it (FK is Restrict), so it can be removed.
        db.Runners.Remove(source);
        await db.SaveChangesAsync();

        await RecalculateSeasonsForRunnersAsync(db, new[] { targetId });
        _logger.LogInformation(
            "Merged runner {SourceId} into {TargetId}; reassigned {Count} entrant(s).", sourceId, targetId, sourceEntrants.Count);
        return OperationResult.Ok($"Merged '{source.Name}' into '{target.Name}'. {sourceEntrants.Count} race entry(ies) moved.");
    }

    /// <summary>Recalculate Champions points for every season the given runners have entrants in (US15 AC6).</summary>
    private async Task RecalculateSeasonsForRunnersAsync(RaceResultsDbContext db, IEnumerable<int> runnerIds)
    {
        var ids = runnerIds.ToHashSet();
        var years = await db.Entrants
            .Where(e => e.RunnerId != null && ids.Contains(e.RunnerId.Value))
            .Join(db.Events, e => e.EventId, ev => ev.Id, (e, ev) => ev.EventDate.Year)
            .Distinct()
            .ToListAsync();

        foreach (var year in years)
        {
            await _championsService.RecalculateSeasonPointsAsync(year);
        }
    }
}
