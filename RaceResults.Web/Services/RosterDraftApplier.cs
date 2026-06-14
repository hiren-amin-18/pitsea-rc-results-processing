using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

public class RosterDraftApplier : IRosterDraftApplier
{
    private readonly IVolunteerRosterService _roster;

    public RosterDraftApplier(IVolunteerRosterService roster)
    {
        _roster = roster;
    }

    public async Task<OperationResult> ApplyAsync(AllocationDraft draft)
    {
        var applied = 0;
        var warnings = new List<string>();
        var errors = new List<string>();

        foreach (var p in draft.Proposals)
        {
            var result = await _roster.AddAssignmentAsync(new VolunteerAssignmentInput
            {
                EventId = draft.EventId,
                VolunteerId = p.VolunteerId,
                VolunteerRoleId = p.VolunteerRoleId,
                WillRunAfter = p.WillRunAfter,
                PreferredRoleId = p.PreferredRoleId,
                WantsToRunAfter = p.WantsToRunAfter,
                WantsNearFinish = p.WantsNearFinish,
                CantWalkFar = p.CantWalkFar,
                WantsSeated = p.WantsSeated,
                AnyRole = p.AnyRole
            });
            if (result.Success) applied++;
            warnings.AddRange(result.Warnings.Select(w => $"{p.VolunteerName} @ {p.RoleName}: {w}"));
            errors.AddRange(result.Errors.Select(e => $"{p.VolunteerName} @ {p.RoleName}: {e}"));
        }

        var summary = OperationResult.Ok($"Applied {applied} of {draft.Proposals.Count} proposed assignment(s).");
        foreach (var w in warnings) summary.Warnings.Add(w);
        foreach (var e in errors) summary.Errors.Add(e);
        if (errors.Count > 0) { summary.Success = applied > 0; }
        return summary;
    }
}
