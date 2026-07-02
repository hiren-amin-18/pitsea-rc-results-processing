using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

/// <summary>Per-event volunteer roster building, editing, and copy-forward (US28).</summary>
public interface IVolunteerRosterService
{
    RosterViewModel GetRoster(int eventId);
    Task<OperationResult> AddAssignmentAsync(VolunteerAssignmentInput input);
    /// <summary>Loads an assignment for the edit form (US36). Volunteer name is display-only — the volunteer
    /// on an assignment cannot be changed.</summary>
    bool TryGetAssignmentForEdit(int assignmentId, out VolunteerAssignmentInput input, out string volunteerName);
    /// <summary>Updates role, run-after, and note on an existing assignment (US36). Preferences are preserved.</summary>
    Task<OperationResult> UpdateAssignmentAsync(VolunteerAssignmentInput input);
    Task<OperationResult> RemoveAssignmentAsync(int assignmentId);
    Task<CopyRosterResult> CopyFromPreviousEventAsync(int targetEventId);
}
