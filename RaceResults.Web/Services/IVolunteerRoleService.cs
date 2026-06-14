using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

/// <summary>Volunteer role catalogue CRUD + eligibility allow-list + pre-placed volunteer (US28).</summary>
public interface IVolunteerRoleService
{
    IReadOnlyList<VolunteerRole> GetRoles(EventType eventType, bool includeInactive = false);
    VolunteerRole? Get(int id);
    bool TryGetRoleForEdit(int id, out VolunteerRoleInput input);
    IReadOnlyList<int> GetEligibleVolunteerIds(int roleId);
    Task<OperationResult> CreateAsync(VolunteerRoleInput input);
    Task<OperationResult> UpdateAsync(VolunteerRoleInput input);
    Task<OperationResult> SetActiveAsync(int id, bool isActive);
}
