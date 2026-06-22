using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

/// <summary>Uploads a previous-event volunteer spreadsheet and pre-populates an event's roster (US35).
/// Creates new volunteers it doesn't recognise (member=true, first-aid=false, active=true, gender chosen
/// in the preview); reuses existing volunteers with no field changed; refuses if the target event already
/// has any assignments.</summary>
public interface IVolunteerRosterImportService
{
    /// <summary>Parses the file and resolves it against the current volunteer register and role catalogue.
    /// Does not write anything; returns the plan + any new-volunteer slots needing a gender pick.</summary>
    RosterImportPreview BuildPreview(int eventId, Stream xlsxStream);

    /// <summary>Applies a previously-built preview to the target event.</summary>
    Task<OperationResult> CommitAsync(RosterImportCommitInput input);
}
