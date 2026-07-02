using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

/// <summary>Volunteer register CRUD (US28). Deactivate, never delete: history must survive.</summary>
public interface IVolunteerRegistryService
{
    IReadOnlyList<VolunteerListItem> GetVolunteers(bool includeInactive = false);
    Volunteer? Get(int id);
    bool TryGetVolunteerForEdit(int id, out VolunteerInput input);
    Task<OperationResult> CreateAsync(VolunteerInput input);
    Task<OperationResult> UpdateAsync(VolunteerInput input);
    Task<OperationResult> SetActiveAsync(int id, bool isActive);

    /// <summary>Permanently remove a volunteer who has no assignments. Refuses if any assignment exists -
    /// US28's "deactivate, never delete" rule still stands for anyone who's worked an event.</summary>
    Task<OperationResult> DeleteIfUnusedAsync(int id);

    /// <summary>Permanently remove every volunteer who has no assignments. Returns how many were removed.</summary>
    Task<OperationResult> DeleteAllUnusedAsync();

    /// <summary>Merges the duplicate volunteer into the survivor (US39): re-points assignments, eligibility
    /// entries, and role pre-placements, adopts the duplicate's contact details where the survivor's are empty,
    /// then deletes the duplicate. Colliding duplicate assignments (same event + role) are dropped and reported.</summary>
    Task<OperationResult> MergeAsync(int survivorId, int duplicateId);
}
