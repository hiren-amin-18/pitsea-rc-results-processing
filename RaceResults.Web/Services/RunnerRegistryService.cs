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

    public IReadOnlyList<RunnerSimilarityCluster> GetSimilarRunnerClusters()
    {
        var runners = GetRunners();
        var n = runners.Count;
        if (n < 2)
        {
            return Array.Empty<RunnerSimilarityCluster>();
        }

        HashSet<(int Low, int High)> dismissed;
        using (var db = _dbContextFactory.CreateDbContext())
        {
            dismissed = db.NotDuplicatePairs
                .Select(p => new { p.RunnerAId, p.RunnerBId })
                .AsEnumerable()
                .Select(p => NormalisePair(p.RunnerAId, p.RunnerBId))
                .ToHashSet();
        }

        var normalised = runners.Select(r => NormaliseName(r.Runner.Name)).ToArray();

        // Union-find groups runners transitively (so A~B and B~C cluster together).
        var parent = Enumerable.Range(0, n).ToArray();
        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        void Union(int a, int b) { var ra = Find(a); var rb = Find(b); if (ra != rb) parent[ra] = rb; }

        // Strongest reason per cluster wins; SameNameDifferentClub is more useful to surface than fuzzy.
        var clusterReason = new Dictionary<int, SimilarityReason>();

        for (var i = 0; i < n; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                if (!GendersCompatible(runners[i].Runner.Gender, runners[j].Runner.Gender))
                {
                    continue;
                }

                if (dismissed.Contains(NormalisePair(runners[i].Runner.Id, runners[j].Runner.Id)))
                {
                    continue;
                }

                var ni = normalised[i];
                var nj = normalised[j];
                if (ni.Length == 0 || nj.Length == 0)
                {
                    continue;
                }

                SimilarityReason? reason = null;
                if (ni == nj &&
                    !string.Equals(runners[i].Runner.Club?.Trim(), runners[j].Runner.Club?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    reason = SimilarityReason.SameNameDifferentClub;
                }
                else if (ni != nj && IsFuzzyMatch(ni, nj))
                {
                    reason = SimilarityReason.FuzzyNameMatch;
                }

                if (reason is null)
                {
                    continue;
                }

                Union(i, j);
                var root = Find(i);
                if (!clusterReason.TryGetValue(root, out var existing) || reason == SimilarityReason.SameNameDifferentClub)
                {
                    clusterReason[root] = reason.Value;
                }
            }
        }

        var groups = new Dictionary<int, List<int>>();
        for (var i = 0; i < n; i++)
        {
            var root = Find(i);
            if (!groups.TryGetValue(root, out var list))
            {
                list = new List<int>();
                groups[root] = list;
            }
            list.Add(i);
        }

        return groups.Values
            .Where(g => g.Count >= 2)
            .Select(g =>
            {
                var root = Find(g[0]);
                var reason = clusterReason.TryGetValue(root, out var r) ? r : SimilarityReason.FuzzyNameMatch;
                // Propagate the strongest reason if any pair in the cluster had it.
                foreach (var idx in g)
                {
                    var rt = Find(idx);
                    if (clusterReason.TryGetValue(rt, out var rr) && rr == SimilarityReason.SameNameDifferentClub)
                    {
                        reason = SimilarityReason.SameNameDifferentClub;
                        break;
                    }
                }
                return new RunnerSimilarityCluster
                {
                    Reason = reason,
                    Runners = g.Select(idx => runners[idx]).OrderBy(r => r.Runner.Name).ToList()
                };
            })
            .OrderBy(c => c.Runners[0].Runner.Name)
            .ToList();
    }

    public async Task<OperationResult> DismissPairAsync(int runnerAId, int runnerBId)
    {
        if (runnerAId == runnerBId)
        {
            return OperationResult.Fail(new[] { "Choose two different runners." });
        }

        var (low, high) = NormalisePair(runnerAId, runnerBId);

        await using var db = _dbContextFactory.CreateDbContext();
        var a = await db.Runners.FirstOrDefaultAsync(r => r.Id == low);
        var b = await db.Runners.FirstOrDefaultAsync(r => r.Id == high);
        if (a is null || b is null)
        {
            return OperationResult.Fail(new[] { "One or both runners were not found." });
        }

        var existing = await db.NotDuplicatePairs.FirstOrDefaultAsync(p => p.RunnerAId == low && p.RunnerBId == high);
        if (existing is not null)
        {
            return OperationResult.Ok($"'{a.Name}' and '{b.Name}' were already marked as not duplicates.");
        }

        db.NotDuplicatePairs.Add(new NotDuplicatePair { RunnerAId = low, RunnerBId = high, DismissedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        _logger.LogInformation("Pair dismissed: {LowId} <-> {HighId}", low, high);
        return OperationResult.Ok($"'{a.Name}' and '{b.Name}' won't be flagged as possible duplicates again.");
    }

    public async Task<OperationResult> ClearDismissedPairsAsync()
    {
        await using var db = _dbContextFactory.CreateDbContext();
        var rows = await db.NotDuplicatePairs.ToListAsync();
        if (rows.Count == 0)
        {
            return OperationResult.Ok("No dismissed pairs to clear.");
        }

        db.NotDuplicatePairs.RemoveRange(rows);
        await db.SaveChangesAsync();
        _logger.LogInformation("Cleared {Count} dismissed pair(s).", rows.Count);
        return OperationResult.Ok($"{rows.Count} dismissed pair(s) cleared.");
    }

    public int CountDismissedPairs()
    {
        using var db = _dbContextFactory.CreateDbContext();
        return db.NotDuplicatePairs.Count();
    }

    private static (int Low, int High) NormalisePair(int a, int b) => a < b ? (a, b) : (b, a);

    private static string NormaliseName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var sb = new System.Text.StringBuilder(name.Length);
        var prevSpace = true;
        foreach (var c in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) { sb.Append(c); prevSpace = false; }
            else if (!prevSpace) { sb.Append(' '); prevSpace = true; }
        }
        return sb.ToString().Trim();
    }

    private static bool GendersCompatible(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return true;
        return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFuzzyMatch(string a, string b)
    {
        var longer = Math.Max(a.Length, b.Length);
        if (longer < 4) return false;
        var threshold = longer >= 8 ? 2 : 1;
        return LevenshteinDistance(a, b, threshold + 1) <= threshold;
    }

    private static int LevenshteinDistance(string a, string b, int cutoff)
    {
        if (Math.Abs(a.Length - b.Length) >= cutoff) return cutoff;
        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            var rowMin = curr[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                if (curr[j] < rowMin) rowMin = curr[j];
            }
            if (rowMin >= cutoff) return cutoff;
            (prev, curr) = (curr, prev);
        }
        return prev[b.Length];
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

        // Volunteers also hold a Restrict FK to Runner (US28); re-point them or the delete below throws.
        var sourceVolunteers = await db.Volunteers.Where(v => v.RunnerId == sourceId).ToListAsync();
        foreach (var volunteer in sourceVolunteers)
        {
            volunteer.RunnerId = targetId;
        }

        await db.SaveChangesAsync();

        // Source now has no entrants or volunteers referencing it (FKs are Restrict), so it can be
        // removed. NotDuplicatePair rows cascade-delete with the runner.
        db.Runners.Remove(source);
        await db.SaveChangesAsync();

        await RecalculateSeasonsForRunnersAsync(db, new[] { targetId });
        _logger.LogInformation(
            "Merged runner {SourceId} into {TargetId}; reassigned {Count} entrant(s) and {VolunteerCount} volunteer(s).",
            sourceId, targetId, sourceEntrants.Count, sourceVolunteers.Count);
        return OperationResult.Ok($"Merged '{source.Name}' into '{target.Name}'. {sourceEntrants.Count} race entry(ies) moved.");
    }

    public async Task<OperationResult> MergeRunnersBatchAsync(IReadOnlyList<ClusterMergeInput> clusters)
    {
        // Pre-validate: a cluster with sources but no target is a user error — fail before doing anything.
        for (var i = 0; i < clusters.Count; i++)
        {
            var c = clusters[i];
            if (c.SourceIds.Count > 0 && c.TargetId == 0)
            {
                return OperationResult.Fail(new[] { $"Cluster {i + 1}: select a runner to keep before submitting." });
            }
        }

        var messages = new List<string>();
        var totalMerges = 0;

        for (var i = 0; i < clusters.Count; i++)
        {
            var c = clusters[i];
            // Ignore clusters with no sources ticked (user said: skip if nothing selected).
            var sources = c.SourceIds.Where(id => id != c.TargetId).Distinct().ToList();
            if (sources.Count == 0)
            {
                continue;
            }

            foreach (var sourceId in sources)
            {
                var result = await MergeRunnersAsync(sourceId, c.TargetId);
                if (!result.Success)
                {
                    var done = OperationResult.Fail(result.Errors.Select(e => $"Cluster {i + 1}: {e}"));
                    foreach (var m in messages) done.Messages.Add(m);
                    return done;
                }
                messages.AddRange(result.Messages);
                totalMerges++;
            }
        }

        if (totalMerges == 0)
        {
            return OperationResult.Ok("No merges to apply.");
        }

        var summary = OperationResult.Ok($"{totalMerges} merge(s) applied across {clusters.Count(c => c.SourceIds.Any(s => s != c.TargetId))} cluster(s).");
        foreach (var m in messages) summary.Messages.Add(m);
        return summary;
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
