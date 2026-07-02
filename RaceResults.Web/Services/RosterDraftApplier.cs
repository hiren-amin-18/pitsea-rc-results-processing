using Microsoft.EntityFrameworkCore;
using RaceResults.Web.Data;
using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

public class RosterDraftApplier : IRosterDraftApplier
{
    private readonly IDbContextFactory<RaceResultsDbContext> _dbContextFactory;

    public RosterDraftApplier(IDbContextFactory<RaceResultsDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<OperationResult> ApplyAsync(AllocationDraft draft)
    {
        await using var db = _dbContextFactory.CreateDbContext();

        var raceEvent = await db.Events.FirstOrDefaultAsync(e => e.Id == draft.EventId);
        if (raceEvent is null)
        {
            return OperationResult.Fail(new[] { "Event not found." });
        }

        var volunteerIds = draft.Proposals.Select(p => p.VolunteerId).Distinct().ToList();
        var roleIds = draft.Proposals.Select(p => p.VolunteerRoleId).Distinct().ToList();

        var volunteers = await db.Volunteers
            .Where(v => volunteerIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id);

        var roles = await db.VolunteerRoles
            .Where(r => roleIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id);

        var allowLists = await db.VolunteerRoleEligibilities
            .Where(e => roleIds.Contains(e.VolunteerRoleId))
            .GroupBy(e => e.VolunteerRoleId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.VolunteerId).ToHashSet());

        var existing = await db.VolunteerAssignments
            .Where(a => a.EventId == draft.EventId)
            .ToListAsync();
        // No-shows (US42) don't occupy slots for capacity purposes.
        var roleCounts = existing.Where(a => !a.IsNoShow)
            .GroupBy(a => a.VolunteerRoleId).ToDictionary(g => g.Key, g => g.Count());
        var roleRunAfterCounts = existing.Where(a => a.WillRunAfter && !a.IsNoShow)
            .GroupBy(a => a.VolunteerRoleId)
            .ToDictionary(g => g.Key, g => g.Count());

        var warnings = new List<string>();
        var errors = new List<string>();
        var applied = 0;

        foreach (var p in draft.Proposals)
        {
            var prefix = $"{p.VolunteerName} @ {p.RoleName}";

            if (!volunteers.TryGetValue(p.VolunteerId, out var volunteer))
            {
                errors.Add($"{prefix}: volunteer no longer exists.");
                continue;
            }
            if (!volunteer.IsActive)
            {
                errors.Add($"{prefix}: volunteer is deactivated.");
                continue;
            }
            if (!roles.TryGetValue(p.VolunteerRoleId, out var role))
            {
                errors.Add($"{prefix}: role no longer exists.");
                continue;
            }
            if (!role.IsActive)
            {
                errors.Add($"{prefix}: role is retired.");
                continue;
            }
            if (role.EventType != raceEvent.EventType)
            {
                errors.Add($"{prefix}: role does not apply to this event type.");
                continue;
            }
            if (role.RequiresFirstAid && !volunteer.IsFirstAidTrained)
            {
                errors.Add($"{prefix}: role requires a first-aid-trained volunteer.");
                continue;
            }
            if (role.HasEligibilityRestriction
                && (!allowLists.TryGetValue(role.Id, out var list) || !list.Contains(volunteer.Id)))
            {
                errors.Add($"{prefix}: role is restricted and volunteer is not on the allow-list.");
                continue;
            }

            var currentCount = roleCounts.GetValueOrDefault(role.Id);
            if (currentCount + 1 > role.MaxCount)
            {
                errors.Add($"{prefix}: role is at its maximum of {role.MaxCount}.");
                continue;
            }
            if (p.WillRunAfter)
            {
                var currentRunAfter = roleRunAfterCounts.GetValueOrDefault(role.Id);
                if (currentRunAfter + 1 > role.RunAfterCapacity)
                {
                    errors.Add($"{prefix}: role run-after capacity {role.RunAfterCapacity} would be exceeded.");
                    continue;
                }
                roleRunAfterCounts[role.Id] = currentRunAfter + 1;
            }

            var assignment = new VolunteerAssignment
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
                WantsRaceHq = p.WantsRaceHq,
                AnyRole = p.AnyRole
            };
            db.VolunteerAssignments.Add(assignment);
            roleCounts[role.Id] = currentCount + 1;
            applied++;

            if (existing.Any(a => a.VolunteerId == p.VolunteerId)
                || draft.Proposals.Take(draft.Proposals.IndexOf(p)).Any(q => q.VolunteerId == p.VolunteerId))
            {
                warnings.Add($"{prefix}: volunteer is also assigned to another role at this event.");
            }
        }

        if (applied > 0)
        {
            await db.SaveChangesAsync();
        }

        var summary = OperationResult.Ok($"Applied {applied} of {draft.Proposals.Count} proposed assignment(s).");
        foreach (var w in warnings) summary.Warnings.Add(w);
        foreach (var e in errors) summary.Errors.Add(e);
        if (errors.Count > 0) summary.Success = applied > 0;
        return summary;
    }
}
