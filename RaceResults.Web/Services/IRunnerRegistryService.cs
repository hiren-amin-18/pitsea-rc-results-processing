using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

/// <summary>Runner registry management: list, edit, and merge persistent runners (US15).</summary>
public interface IRunnerRegistryService
{
    IReadOnlyList<RunnerListItem> GetRunners();

    /// <summary>Clusters of runners likely to be the same person (fuzzy name or same-name-different-club), to help spot merge candidates.</summary>
    IReadOnlyList<RunnerSimilarityCluster> GetSimilarRunnerClusters();
    bool TryGetRunnerForEdit(int id, out EditRunnerInput input);

    /// <summary>Update a runner's details (propagated to their entrant rows) and recalculate affected Champions seasons.</summary>
    Task<OperationResult> UpdateRunnerAsync(EditRunnerInput input);

    /// <summary>Merge the source runner into the target: reassign the source's entrants, remove the source,
    /// and recalculate affected Champions seasons.</summary>
    Task<OperationResult> MergeRunnersAsync(int sourceId, int targetId);

    /// <summary>Merge multiple sources into their target for each cluster. Clusters with no sources are skipped.
    /// Stops at the first failure; merges already applied remain.</summary>
    Task<OperationResult> MergeRunnersBatchAsync(IReadOnlyList<ClusterMergeInput> clusters);

    /// <summary>Record that two runners are NOT the same person, so the duplicates view stops surfacing the pair.</summary>
    Task<OperationResult> DismissPairAsync(int runnerAId, int runnerBId);

    /// <summary>Remove all "not a duplicate" dismissals so previously-hidden pairs surface again.</summary>
    Task<OperationResult> ClearDismissedPairsAsync();

    int CountDismissedPairs();
}
