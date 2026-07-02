using Microsoft.EntityFrameworkCore;
using RaceResults.Web.Data;
using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

public class VolunteerRegistryService : IVolunteerRegistryService
{
    private readonly IDbContextFactory<RaceResultsDbContext> _dbContextFactory;
    private readonly ILogger<VolunteerRegistryService> _logger;

    public VolunteerRegistryService(
        IDbContextFactory<RaceResultsDbContext> dbContextFactory,
        ILogger<VolunteerRegistryService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public IReadOnlyList<VolunteerListItem> GetVolunteers(bool includeInactive = false)
    {
        using var db = _dbContextFactory.CreateDbContext();

        var assignmentCounts = db.VolunteerAssignments
            .GroupBy(a => a.VolunteerId)
            .Select(g => new { VolunteerId = g.Key, Count = g.Count() })
            .ToDictionary(x => x.VolunteerId, x => x.Count);

        var query = db.Volunteers.Include(v => v.Runner).AsQueryable();
        if (!includeInactive) query = query.Where(v => v.IsActive);

        return query
            .OrderBy(v => v.Name)
            .ToList()
            .Select(v => new VolunteerListItem
            {
                Volunteer = v,
                RunnerName = v.Runner?.Name,
                AssignmentCount = assignmentCounts.TryGetValue(v.Id, out var c) ? c : 0
            })
            .ToList();
    }

    public Volunteer? Get(int id)
    {
        using var db = _dbContextFactory.CreateDbContext();
        return db.Volunteers.Include(v => v.Runner).FirstOrDefault(v => v.Id == id);
    }

    public bool TryGetVolunteerForEdit(int id, out VolunteerInput input)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var v = db.Volunteers.FirstOrDefault(x => x.Id == id);
        if (v is null) { input = new VolunteerInput(); return false; }
        input = new VolunteerInput
        {
            Id = v.Id,
            Name = v.Name,
            Email = v.Email,
            Phone = v.Phone,
            Gender = v.Gender,
            IsClubMember = v.IsClubMember,
            IsFirstAidTrained = v.IsFirstAidTrained,
            RunnerId = v.RunnerId
        };
        return true;
    }

    public async Task<OperationResult> CreateAsync(VolunteerInput input)
    {
        var errors = Validate(input);
        if (errors.Count > 0) return OperationResult.Fail(errors);

        await using var db = _dbContextFactory.CreateDbContext();
        var volunteer = new Volunteer
        {
            Name = input.Name.Trim(),
            Email = NullIfBlank(input.Email),
            Phone = NullIfBlank(input.Phone),
            Gender = input.Gender?.Trim() ?? string.Empty,
            IsClubMember = input.IsClubMember,
            IsFirstAidTrained = input.IsFirstAidTrained,
            RunnerId = input.RunnerId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        // Duplicate-name guard (US39): warn but allow — genuine namesakes are possible.
        var trimmedName = input.Name.Trim();
        var namesakes = await db.Volunteers
            .Where(v => v.Name.ToLower() == trimmedName.ToLower())
            .Select(v => new { v.Id, v.IsActive })
            .ToListAsync();

        db.Volunteers.Add(volunteer);
        await db.SaveChangesAsync();
        _logger.LogInformation("Volunteer {Id} created: {Name}.", volunteer.Id, volunteer.Name);
        var result = OperationResult.Ok($"Volunteer '{volunteer.Name}' added.");
        if (namesakes.Count > 0)
        {
            var detail = string.Join(", ", namesakes.Select(n => $"#{n.Id}{(n.IsActive ? "" : " (inactive)")}"));
            result.Warnings.Add(
                $"A volunteer named '{volunteer.Name}' already exists ({detail}). If this is the same person, merge the records from the Volunteers page.");
        }
        return result;
    }

    public async Task<OperationResult> UpdateAsync(VolunteerInput input)
    {
        var errors = Validate(input);
        if (errors.Count > 0) return OperationResult.Fail(errors);

        await using var db = _dbContextFactory.CreateDbContext();
        var volunteer = await db.Volunteers.FirstOrDefaultAsync(v => v.Id == input.Id);
        if (volunteer is null) return OperationResult.Fail(new[] { "Volunteer not found." });

        volunteer.Name = input.Name.Trim();
        volunteer.Email = NullIfBlank(input.Email);
        volunteer.Phone = NullIfBlank(input.Phone);
        volunteer.Gender = input.Gender?.Trim() ?? string.Empty;
        volunteer.IsClubMember = input.IsClubMember;
        volunteer.IsFirstAidTrained = input.IsFirstAidTrained;
        volunteer.RunnerId = input.RunnerId;
        await db.SaveChangesAsync();
        _logger.LogInformation("Volunteer {Id} updated.", volunteer.Id);
        return OperationResult.Ok($"Volunteer '{volunteer.Name}' updated.");
    }

    public async Task<OperationResult> SetActiveAsync(int id, bool isActive)
    {
        await using var db = _dbContextFactory.CreateDbContext();
        var volunteer = await db.Volunteers.FirstOrDefaultAsync(v => v.Id == id);
        if (volunteer is null) return OperationResult.Fail(new[] { "Volunteer not found." });
        volunteer.IsActive = isActive;
        await db.SaveChangesAsync();
        _logger.LogInformation("Volunteer {Id} {State}.", id, isActive ? "reactivated" : "deactivated");
        return OperationResult.Ok($"Volunteer '{volunteer.Name}' {(isActive ? "reactivated" : "deactivated")}.");
    }

    public async Task<OperationResult> DeleteIfUnusedAsync(int id)
    {
        await using var db = _dbContextFactory.CreateDbContext();
        var volunteer = await db.Volunteers.FirstOrDefaultAsync(v => v.Id == id);
        if (volunteer is null) return OperationResult.Fail(new[] { "Volunteer not found." });
        if (db.VolunteerAssignments.Any(a => a.VolunteerId == id))
            return OperationResult.Fail(new[] { $"'{volunteer.Name}' has roster assignments and cannot be deleted - deactivate instead." });

        // Also clear any eligibility allow-list entries so we don't leave dangling rows.
        var eligibility = db.VolunteerRoleEligibilities.Where(e => e.VolunteerId == id);
        db.VolunteerRoleEligibilities.RemoveRange(eligibility);
        db.Volunteers.Remove(volunteer);
        await db.SaveChangesAsync();
        _logger.LogInformation("Volunteer {Id} deleted: {Name}.", id, volunteer.Name);
        return OperationResult.Ok($"Volunteer '{volunteer.Name}' deleted.");
    }

    public async Task<OperationResult> DeleteAllUnusedAsync()
    {
        await using var db = _dbContextFactory.CreateDbContext();
        var usedIds = await db.VolunteerAssignments.Select(a => a.VolunteerId).Distinct().ToListAsync();
        var unused = await db.Volunteers.Where(v => !usedIds.Contains(v.Id)).ToListAsync();
        if (unused.Count == 0) return OperationResult.Ok("No unused volunteers to delete.");

        var unusedIds = unused.Select(v => v.Id).ToList();
        var eligibility = db.VolunteerRoleEligibilities.Where(e => unusedIds.Contains(e.VolunteerId));
        db.VolunteerRoleEligibilities.RemoveRange(eligibility);
        db.Volunteers.RemoveRange(unused);
        await db.SaveChangesAsync();
        _logger.LogInformation("Bulk-deleted {Count} unused volunteer(s).", unused.Count);
        return OperationResult.Ok($"Deleted {unused.Count} volunteer(s) with no assignments.");
    }

    public async Task<OperationResult> MergeAsync(int survivorId, int duplicateId)
    {
        if (survivorId == duplicateId)
            return OperationResult.Fail(new[] { "Pick two different volunteers to merge." });

        await using var db = _dbContextFactory.CreateDbContext();
        var survivor = await db.Volunteers.FirstOrDefaultAsync(v => v.Id == survivorId);
        if (survivor is null) return OperationResult.Fail(new[] { "Survivor volunteer not found." });
        var duplicate = await db.Volunteers.FirstOrDefaultAsync(v => v.Id == duplicateId);
        if (duplicate is null) return OperationResult.Fail(new[] { "Duplicate volunteer not found." });

        // Re-point assignments; drop any that would duplicate a survivor assignment (same event + role).
        var survivorKeys = (await db.VolunteerAssignments
                .Where(a => a.VolunteerId == survivorId)
                .Select(a => new { a.EventId, a.VolunteerRoleId })
                .ToListAsync())
            .Select(x => (x.EventId, x.VolunteerRoleId))
            .ToHashSet();
        var duplicateAssignments = await db.VolunteerAssignments
            .Where(a => a.VolunteerId == duplicateId)
            .ToListAsync();
        var moved = 0;
        var dropped = 0;
        foreach (var assignment in duplicateAssignments)
        {
            if (survivorKeys.Contains((assignment.EventId, assignment.VolunteerRoleId)))
            {
                db.VolunteerAssignments.Remove(assignment);
                dropped++;
            }
            else
            {
                assignment.VolunteerId = survivorId;
                survivorKeys.Add((assignment.EventId, assignment.VolunteerRoleId));
                moved++;
            }
        }

        // Re-point eligibility allow-list entries; drop collisions (unique index on role + volunteer).
        var survivorEligibleRoles = (await db.VolunteerRoleEligibilities
                .Where(e => e.VolunteerId == survivorId)
                .Select(e => e.VolunteerRoleId)
                .ToListAsync())
            .ToHashSet();
        var duplicateEligibility = await db.VolunteerRoleEligibilities
            .Where(e => e.VolunteerId == duplicateId)
            .ToListAsync();
        var movedEligibility = 0;
        foreach (var entry in duplicateEligibility)
        {
            if (survivorEligibleRoles.Contains(entry.VolunteerRoleId))
            {
                db.VolunteerRoleEligibilities.Remove(entry);
            }
            else
            {
                entry.VolunteerId = survivorId;
                survivorEligibleRoles.Add(entry.VolunteerRoleId);
                movedEligibility++;
            }
        }

        // Re-point role pre-placements.
        var prePlacements = await db.VolunteerRoles
            .Where(r => r.PrePlacedVolunteerId == duplicateId)
            .ToListAsync();
        foreach (var role in prePlacements) role.PrePlacedVolunteerId = survivorId;

        // Adopt the duplicate's details where the survivor's are empty; first aid is the OR of the two.
        survivor.Email ??= duplicate.Email;
        survivor.Phone ??= duplicate.Phone;
        if (string.IsNullOrWhiteSpace(survivor.Gender)) survivor.Gender = duplicate.Gender;
        survivor.RunnerId ??= duplicate.RunnerId;
        survivor.IsFirstAidTrained |= duplicate.IsFirstAidTrained;

        db.Volunteers.Remove(duplicate);
        await db.SaveChangesAsync();

        _logger.LogInformation("Merged volunteer {DuplicateId} ({DuplicateName}) into {SurvivorId} ({SurvivorName}): {Moved} assignment(s) moved, {Dropped} dropped.",
            duplicateId, duplicate.Name, survivorId, survivor.Name, moved, dropped);

        var summary = $"Merged '{duplicate.Name}' into '{survivor.Name}': moved {moved} assignment(s), {movedEligibility} eligibility entr(ies)";
        if (prePlacements.Count > 0) summary += $", {prePlacements.Count} pre-placement(s)";
        summary += dropped > 0 ? $"; dropped {dropped} duplicate assignment(s)." : ".";
        return OperationResult.Ok(summary);
    }

    private static List<string> Validate(VolunteerInput input)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(input.Name)) errors.Add("Name is required.");
        return errors;
    }

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
