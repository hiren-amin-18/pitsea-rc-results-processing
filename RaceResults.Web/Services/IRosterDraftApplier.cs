using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

/// <summary>Applies an allocator draft as real <see cref="VolunteerAssignment"/>s via the existing roster service (US32 AC4).</summary>
public interface IRosterDraftApplier
{
    Task<OperationResult> ApplyAsync(AllocationDraft draft);
}
