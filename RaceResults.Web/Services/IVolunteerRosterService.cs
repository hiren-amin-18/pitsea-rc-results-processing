using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

/// <summary>Per-event volunteer roster building, editing, and copy-forward (US28).</summary>
public interface IVolunteerRosterService
{
    RosterViewModel GetRoster(int eventId);
    Task<OperationResult> AddAssignmentAsync(VolunteerAssignmentInput input);
    Task<OperationResult> UpdateAssignmentAsync(VolunteerAssignmentInput input);
    Task<OperationResult> RemoveAssignmentAsync(int assignmentId);
    Task<CopyRosterResult> CopyFromPreviousEventAsync(int targetEventId);
}
