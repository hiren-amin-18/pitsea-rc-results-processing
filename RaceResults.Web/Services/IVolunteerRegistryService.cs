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
}
