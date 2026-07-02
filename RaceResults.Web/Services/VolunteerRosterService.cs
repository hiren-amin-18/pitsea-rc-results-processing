using Microsoft.EntityFrameworkCore;
using RaceResults.Web.Data;
using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

public class VolunteerRosterService : IVolunteerRosterService
{
    private readonly IDbContextFactory<RaceResultsDbContext> _dbContextFactory;
    private readonly ILogger<VolunteerRosterService> _logger;
    private readonly IVolunteerStatsService? _stats;

    public VolunteerRosterService(
        IDbContextFactory<RaceResultsDbContext> dbContextFactory,
        ILogger<VolunteerRosterService> logger,
        IVolunteerStatsService? stats = null)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _stats = stats;
    }

    public RosterViewModel GetRoster(int eventId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var raceEvent = db.Events.FirstOrDefault(e => e.Id == eventId)
            ?? throw new InvalidOperationException($"Event {eventId} not found.");

        var roles = db.VolunteerRoles
            .Where(r => r.EventType == raceEvent.EventType && r.IsActive)
            .Include(r => r.PrePlacedVolunteer)
            .OrderBy(r => r.Category).ThenBy(r => r.SortOrder)
            .ToList();

        var assignments = db.VolunteerAssignments
            .Where(a => a.EventId == eventId)
            .Include(a => a.Volunteer)
            .ToList();

        var byCategory = new Dictionary<RoleCategory, List<RosterRoleRow>>();
        foreach (var category in Enum.GetValues<RoleCategory>())
        {
            byCategory[category] = roles
                .Where(r => r.Category == category)
                .Select(r => new RosterRoleRow
                {
                    Role = r,
                    Assignments = assignments
                        .Where(a => a.VolunteerRoleId == r.Id)
                        .OrderBy(a => a.Volunteer!.Name)
                        .Select(a => new RosterAssignmentRow { Assignment = a, Volunteer = a.Volunteer! })
                        .ToList()
                })
                .ToList();
        }

        var doubleBooked = assignments
            .GroupBy(a => a.VolunteerId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        var copyable = db.Events
            .Where(e => e.EventType == raceEvent.EventType && e.Id != eventId)
            .OrderByDescending(e => e.EventDate)
            .Take(10)
            .ToList();

        return new RosterViewModel
        {
            Event = raceEvent,
            ByCategory = byCategory,
            TotalAssigned = assignments.Count,
            DistinctVolunteers = assignments.Select(a => a.VolunteerId).Distinct().Count(),
            DoubleBookedVolunteerIds = doubleBooked,
            CopyableEvents = copyable,
            PerEventStats = _stats?.GetEventStats(eventId)
        };
    }

    public async Task<OperationResult> AddAssignmentAsync(VolunteerAssignmentInput input)
    {
        await using var db = _dbContextFactory.CreateDbContext();

        // Generic-preference sentinel (e.g. "Marshal (any point)") has no physical slot — resolve it to
        // the most under-staffed Marshal Point the volunteer is eligible for, or error if all are full.
        string? autoFillMessage = null;
        var pickedRole = await db.VolunteerRoles.FirstOrDefaultAsync(r => r.Id == input.VolunteerRoleId);
        if (pickedRole is not null && pickedRole.IsGenericPreference)
        {
            if (input.WillRunAfter)
                return OperationResult.Fail(new[] { "Marshal roles cannot also run after." });

            var (resolvedId, resolvedName, error) = await ResolveGenericMarshalAsync(db, input);
            if (error is not null) return OperationResult.Fail(new[] { error });
            input.VolunteerRoleId = resolvedId!.Value;
            autoFillMessage = $"Placed at {resolvedName}.";
        }

        var validation = await ValidateAssignmentAsync(db, input, existingId: null);
        if (validation.Errors.Count > 0) return OperationResult.Fail(validation.Errors);

        var assignment = new VolunteerAssignment
        {
            EventId = input.EventId,
            VolunteerId = input.VolunteerId,
            VolunteerRoleId = input.VolunteerRoleId,
            WillRunAfter = input.WillRunAfter,
            Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(),
            PreferredRoleId = input.PreferredRoleId,
            WantsToRunAfter = input.WantsToRunAfter,
            WantsNearFinish = input.WantsNearFinish,
            CantWalkFar = input.CantWalkFar,
            WantsSeated = input.WantsSeated,
            WantsRaceHq = input.WantsRaceHq,
            AnyRole = input.AnyRole
        };
        db.VolunteerAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var result = OperationResult.Ok(autoFillMessage ?? "Assignment added.");
        foreach (var warning in validation.Warnings) result.Warnings.Add(warning);
        _logger.LogInformation("Assignment {Id} created for event {EventId} role {RoleId}.", assignment.Id, input.EventId, input.VolunteerRoleId);
        return result;
    }

    private async Task<(int? RoleId, string? RoleName, string? Error)> ResolveGenericMarshalAsync(
        RaceResultsDbContext db, VolunteerAssignmentInput input)
    {
        var raceEvent = await db.Events.FirstOrDefaultAsync(e => e.Id == input.EventId);
        if (raceEvent is null) return (null, null, "Event not found.");

        var volunteer = await db.Volunteers.FirstOrDefaultAsync(v => v.Id == input.VolunteerId);
        if (volunteer is null) return (null, null, "Volunteer not found.");
        if (!volunteer.IsActive) return (null, null, $"Volunteer '{volunteer.Name}' is deactivated.");

        var marshalPoints = await db.VolunteerRoles
            .Where(r => r.EventType == raceEvent.EventType
                        && r.IsActive
                        && r.Name.StartsWith("Marshal Point"))
            .ToListAsync();
        if (marshalPoints.Count == 0)
            return (null, null, "No marshal points exist for this event type.");

        var marshalIds = marshalPoints.Select(r => r.Id).ToList();

        var allowLists = await db.VolunteerRoleEligibilities
            .Where(e => marshalIds.Contains(e.VolunteerRoleId))
            .GroupBy(e => e.VolunteerRoleId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.VolunteerId).ToHashSet());

        var assignmentCounts = await db.VolunteerAssignments
            .Where(a => a.EventId == input.EventId && marshalIds.Contains(a.VolunteerRoleId))
            .GroupBy(a => a.VolunteerRoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count);

        bool IsEligible(VolunteerRole r)
        {
            if (r.RequiresFirstAid && !volunteer.IsFirstAidTrained) return false;
            if (r.HasEligibilityRestriction)
            {
                if (!allowLists.TryGetValue(r.Id, out var list) || !list.Contains(volunteer.Id))
                    return false;
            }
            return true;
        }

        var pick = marshalPoints
            .Where(IsEligible)
            .Select(r => new
            {
                Role = r,
                Assigned = assignmentCounts.TryGetValue(r.Id, out var c) ? c : 0
            })
            .Where(x => x.Assigned < x.Role.DefaultCount)
            .OrderByDescending(x => x.Role.DefaultCount - x.Assigned)
            .ThenBy(x => x.Role.SortOrder)
            .Select(x => x.Role)
            .FirstOrDefault();

        if (pick is null)
            return (null, null, "No marshal point has an available spot.");

        return (pick.Id, pick.Name, null);
    }

    public bool TryGetAssignmentForEdit(int assignmentId, out VolunteerAssignmentInput input, out string volunteerName)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var assignment = db.VolunteerAssignments
            .Include(a => a.Volunteer)
            .FirstOrDefault(a => a.Id == assignmentId);
        if (assignment is null)
        {
            input = new VolunteerAssignmentInput();
            volunteerName = string.Empty;
            return false;
        }
        input = new VolunteerAssignmentInput
        {
            Id = assignment.Id,
            EventId = assignment.EventId,
            VolunteerId = assignment.VolunteerId,
            VolunteerRoleId = assignment.VolunteerRoleId,
            WillRunAfter = assignment.WillRunAfter,
            Note = assignment.Note
        };
        volunteerName = assignment.Volunteer?.Name ?? "(unknown)";
        return true;
    }

    public async Task<OperationResult> UpdateAssignmentAsync(VolunteerAssignmentInput input)
    {
        await using var db = _dbContextFactory.CreateDbContext();
        var assignment = await db.VolunteerAssignments.FirstOrDefaultAsync(a => a.Id == input.Id);
        if (assignment is null) return OperationResult.Fail(new[] { "Assignment not found." });

        // The event and volunteer on an assignment are immutable — take them from the stored record.
        input.EventId = assignment.EventId;
        input.VolunteerId = assignment.VolunteerId;

        var newRole = await db.VolunteerRoles.FirstOrDefaultAsync(r => r.Id == input.VolunteerRoleId);
        string? autoFillMessage = null;
        if (newRole is not null && newRole.IsGenericPreference)
        {
            if (input.WillRunAfter)
                return OperationResult.Fail(new[] { "Marshal roles cannot also run after." });
            var (resolvedId, resolvedName, error) = await ResolveGenericMarshalAsync(db, input);
            if (error is not null) return OperationResult.Fail(new[] { error });
            input.VolunteerRoleId = resolvedId!.Value;
            autoFillMessage = $"Placed at {resolvedName}.";
        }

        var validation = await ValidateAssignmentAsync(db, input, existingId: assignment.Id);
        if (validation.Errors.Count > 0) return OperationResult.Fail(validation.Errors);

        assignment.VolunteerRoleId = input.VolunteerRoleId;
        assignment.WillRunAfter = input.WillRunAfter;
        assignment.Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        // Per-event preferences are deliberately untouched (US36 AC4).
        await db.SaveChangesAsync();

        var result = OperationResult.Ok(autoFillMessage ?? "Assignment updated.");
        foreach (var warning in validation.Warnings) result.Warnings.Add(warning);
        _logger.LogInformation("Assignment {Id} updated for event {EventId} role {RoleId}.", assignment.Id, assignment.EventId, assignment.VolunteerRoleId);
        return result;
    }

    public async Task<OperationResult> RemoveAssignmentAsync(int assignmentId)
    {
        await using var db = _dbContextFactory.CreateDbContext();
        var assignment = await db.VolunteerAssignments.FirstOrDefaultAsync(a => a.Id == assignmentId);
        if (assignment is null) return OperationResult.Fail(new[] { "Assignment not found." });
        db.VolunteerAssignments.Remove(assignment);
        await db.SaveChangesAsync();
        return OperationResult.Ok("Assignment removed.");
    }

    public async Task<CopyRosterResult> CopyFromPreviousEventAsync(int targetEventId)
    {
        await using var db = _dbContextFactory.CreateDbContext();
        var target = await db.Events.FirstOrDefaultAsync(e => e.Id == targetEventId);
        if (target is null) return new CopyRosterResult { Success = false, ErrorMessage = "Target event not found." };

        var source = await db.Events
            .Where(e => e.EventType == target.EventType
                && e.Id != target.Id
                && e.EventDate < target.EventDate)
            .OrderByDescending(e => e.EventDate)
            .FirstOrDefaultAsync();
        if (source is null)
            return new CopyRosterResult { Success = false, ErrorMessage = "No earlier event of this type to copy from." };

        var sourceAssignments = await db.VolunteerAssignments
            .Where(a => a.EventId == source.Id)
            .Include(a => a.Volunteer)
            .Include(a => a.VolunteerRole)
            .ToListAsync();

        if (sourceAssignments.Count == 0)
            return new CopyRosterResult { Success = false, ErrorMessage = $"Source event '{source.EventName}' has no roster to copy." };

        var existingForTarget = await db.VolunteerAssignments
            .Where(a => a.EventId == target.Id)
            .Select(a => new { a.VolunteerId, a.VolunteerRoleId })
            .ToListAsync();
        var existingSet = existingForTarget.Select(x => (x.VolunteerId, x.VolunteerRoleId)).ToHashSet();

        var restrictedAllowLists = await db.VolunteerRoleEligibilities
            .Where(e => sourceAssignments.Select(a => a.VolunteerRoleId).Contains(e.VolunteerRoleId))
            .GroupBy(e => e.VolunteerRoleId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.VolunteerId).ToHashSet());

        var result = new CopyRosterResult { Success = true };
        foreach (var src in sourceAssignments)
        {
            if (src.Volunteer is null || !src.Volunteer.IsActive)
            {
                result.SkippedItems.Add($"{src.Volunteer?.Name ?? "(unknown)"}: inactive volunteer.");
                continue;
            }
            if (src.VolunteerRole is null || !src.VolunteerRole.IsActive)
            {
                result.SkippedItems.Add($"{src.Volunteer.Name}: role no longer active.");
                continue;
            }
            if (src.VolunteerRole.RequiresFirstAid && !src.Volunteer.IsFirstAidTrained)
            {
                result.SkippedItems.Add($"{src.Volunteer.Name}: '{src.VolunteerRole.Name}' now requires first aid.");
                continue;
            }
            if (src.VolunteerRole.HasEligibilityRestriction)
            {
                if (!restrictedAllowLists.TryGetValue(src.VolunteerRoleId, out var allowList) || !allowList.Contains(src.VolunteerId))
                {
                    result.SkippedItems.Add($"{src.Volunteer.Name}: no longer eligible for '{src.VolunteerRole.Name}'.");
                    continue;
                }
            }
            if (existingSet.Contains((src.VolunteerId, src.VolunteerRoleId)))
            {
                result.SkippedItems.Add($"{src.Volunteer.Name} @ {src.VolunteerRole.Name}: already on the target roster.");
                continue;
            }

            db.VolunteerAssignments.Add(new VolunteerAssignment
            {
                EventId = target.Id,
                VolunteerId = src.VolunteerId,
                VolunteerRoleId = src.VolunteerRoleId,
                WillRunAfter = src.WillRunAfter,
                Note = src.Note
                // Preferences are NOT carried — they are per-event sentiments.
            });
            result.CopiedCount++;
        }
        await db.SaveChangesAsync();
        _logger.LogInformation("Copied {Count} assignment(s) from event {SourceId} to event {TargetId}; skipped {Skipped}.",
            result.CopiedCount, source.Id, target.Id, result.SkippedItems.Count);
        return result;
    }

    private record ValidationOutcome(List<string> Errors, List<string> Warnings);

    private async Task<ValidationOutcome> ValidateAssignmentAsync(RaceResultsDbContext db, VolunteerAssignmentInput input, int? existingId)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        var raceEvent = await db.Events.FirstOrDefaultAsync(e => e.Id == input.EventId);
        if (raceEvent is null) { errors.Add("Event not found."); return new ValidationOutcome(errors, warnings); }

        var volunteer = await db.Volunteers.FirstOrDefaultAsync(v => v.Id == input.VolunteerId);
        if (volunteer is null) { errors.Add("Volunteer not found."); return new ValidationOutcome(errors, warnings); }
        if (!volunteer.IsActive) errors.Add($"Volunteer '{volunteer.Name}' is deactivated.");

        var role = await db.VolunteerRoles.FirstOrDefaultAsync(r => r.Id == input.VolunteerRoleId);
        if (role is null) { errors.Add("Role not found."); return new ValidationOutcome(errors, warnings); }
        if (!role.IsActive) errors.Add($"Role '{role.Name}' is retired.");
        if (role.EventType != raceEvent.EventType) errors.Add($"Role '{role.Name}' does not apply to this event type.");

        if (role.RequiresFirstAid && !volunteer.IsFirstAidTrained)
            errors.Add($"'{role.Name}' requires a first-aid-trained volunteer.");

        if (role.HasEligibilityRestriction)
        {
            var allowed = await db.VolunteerRoleEligibilities
                .AnyAsync(e => e.VolunteerRoleId == role.Id && e.VolunteerId == volunteer.Id);
            if (!allowed) errors.Add($"'{role.Name}' is restricted and '{volunteer.Name}' is not on the allow-list.");
        }

        // Capacity check: count existing assignments excluding this one if updating.
        var existingForRole = await db.VolunteerAssignments
            .Where(a => a.EventId == input.EventId && a.VolunteerRoleId == role.Id && (existingId == null || a.Id != existingId.Value))
            .CountAsync();
        if (existingForRole + 1 > role.MaxCount)
            errors.Add($"'{role.Name}' is at its maximum of {role.MaxCount}.");

        // Run-after capacity.
        if (input.WillRunAfter)
        {
            var existingRunAfter = await db.VolunteerAssignments
                .Where(a => a.EventId == input.EventId && a.VolunteerRoleId == role.Id && a.WillRunAfter && (existingId == null || a.Id != existingId.Value))
                .CountAsync();
            if (existingRunAfter + 1 > role.RunAfterCapacity)
                errors.Add($"'{role.Name}' has run-after capacity {role.RunAfterCapacity} (would be exceeded).");
        }

        // Double-booking warning (not an error).
        var alreadyAssigned = await db.VolunteerAssignments
            .Where(a => a.EventId == input.EventId && a.VolunteerId == volunteer.Id && (existingId == null || a.Id != existingId.Value))
            .Select(a => a.VolunteerRole!.Name)
            .ToListAsync();
        if (alreadyAssigned.Count > 0)
        {
            warnings.Add($"'{volunteer.Name}' is also assigned to: {string.Join(", ", alreadyAssigned)} at this event.");
        }

        return new ValidationOutcome(errors, warnings);
    }
}
