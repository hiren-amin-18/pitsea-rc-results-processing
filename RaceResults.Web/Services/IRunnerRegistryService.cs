using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

/// <summary>Runner registry management: list, edit, and merge persistent runners (US15).</summary>
public interface IRunnerRegistryService
{
    IReadOnlyList<RunnerListItem> GetRunners();
    bool TryGetRunnerForEdit(int id, out EditRunnerInput input);

    /// <summary>Update a runner's details (propagated to their entrant rows) and recalculate affected Champions seasons.</summary>
    Task<OperationResult> UpdateRunnerAsync(EditRunnerInput input);

    /// <summary>Merge the source runner into the target: reassign the source's entrants, remove the source,
    /// and recalculate affected Champions seasons.</summary>
    Task<OperationResult> MergeRunnersAsync(int sourceId, int targetId);
}
