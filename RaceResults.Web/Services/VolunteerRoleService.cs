using Microsoft.EntityFrameworkCore;
using RaceResults.Web.Data;
using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

public class VolunteerRoleService : IVolunteerRoleService
{
    private readonly IDbContextFactory<RaceResultsDbContext> _dbContextFactory;
    private readonly ILogger<VolunteerRoleService> _logger;

    public VolunteerRoleService(
        IDbContextFactory<RaceResultsDbContext> dbContextFactory,
        ILogger<VolunteerRoleService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public IReadOnlyList<VolunteerRole> GetRoles(EventType eventType, bool includeInactive = false)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var query = db.VolunteerRoles.AsQueryable().Where(r => r.EventType == eventType);
        if (!includeInactive) query = query.Where(r => r.IsActive);
        return query
            .Include(r => r.PrePlacedVolunteer)
            .OrderBy(r => r.Category)
            .ThenBy(r => r.SortOrder)
            .ToList();
    }

    public VolunteerRole? Get(int id)
    {
        using var db = _dbContextFactory.CreateDbContext();
        return db.VolunteerRoles.Include(r => r.PrePlacedVolunteer).FirstOrDefault(r => r.Id == id);
    }

    public bool TryGetRoleForEdit(int id, out VolunteerRoleInput input)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var role = db.VolunteerRoles.FirstOrDefault(r => r.Id == id);
        if (role is null) { input = new VolunteerRoleInput(); return false; }

        var eligibleIds = db.VolunteerRoleEligibilities
            .Where(x => x.VolunteerRoleId == id)
            .Select(x => x.VolunteerId)
            .ToList();

        input = new VolunteerRoleInput
        {
            Id = role.Id,
            Name = role.Name,
            Category = role.Category,
            EventType = role.EventType,
            DefaultCount = role.DefaultCount,
            MinCount = role.MinCount,
            MaxCount = role.MaxCount,
            IsOptional = role.IsOptional,
            IsGenericPreference = role.IsGenericPreference,
            RunAfterCapacity = role.RunAfterCapacity,
            RequiresFirstAid = role.RequiresFirstAid,
            HasEligibilityRestriction = role.HasEligibilityRestriction,
            PrePlacedVolunteerId = role.PrePlacedVolunteerId,
            SortOrder = role.SortOrder,
            IsActive = role.IsActive,
            EligibleVolunteerIds = eligibleIds
        };
        return true;
    }

    public IReadOnlyList<int> GetEligibleVolunteerIds(int roleId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        return db.VolunteerRoleEligibilities
            .Where(x => x.VolunteerRoleId == roleId)
            .Select(x => x.VolunteerId)
            .ToList();
    }

    public async Task<OperationResult> CreateAsync(VolunteerRoleInput input)
    {
        var errors = Validate(input);
        if (errors.Count > 0) return OperationResult.Fail(errors);

        await using var db = _dbContextFactory.CreateDbContext();
        if (await db.VolunteerRoles.AnyAsync(r => r.EventType == input.EventType && r.Name == input.Name.Trim()))
        {
            return OperationResult.Fail(new[] { $"A role named '{input.Name.Trim()}' already exists for this event type." });
        }

        var role = new VolunteerRole
        {
            Name = input.Name.Trim(),
            Category = input.Category,
            EventType = input.EventType,
            DefaultCount = input.DefaultCount,
            MinCount = input.MinCount,
            MaxCount = input.MaxCount,
            IsOptional = input.IsOptional,
            IsGenericPreference = input.IsGenericPreference,
            RunAfterCapacity = input.RunAfterCapacity,
            RequiresFirstAid = input.RequiresFirstAid,
            HasEligibilityRestriction = input.HasEligibilityRestriction,
            PrePlacedVolunteerId = input.PrePlacedVolunteerId,
            SortOrder = input.SortOrder == 0 ? 999 : input.SortOrder,
            IsActive = true
        };
        db.VolunteerRoles.Add(role);
        await db.SaveChangesAsync();

        await SyncEligibility(db, role.Id, input.EligibleVolunteerIds);
        await db.SaveChangesAsync();
        _logger.LogInformation("Volunteer role {Id} created: {Name}.", role.Id, role.Name);
        return OperationResult.Ok($"Role '{role.Name}' added.");
    }

    public async Task<OperationResult> UpdateAsync(VolunteerRoleInput input)
    {
        var errors = Validate(input);
        if (errors.Count > 0) return OperationResult.Fail(errors);

        await using var db = _dbContextFactory.CreateDbContext();
        var role = await db.VolunteerRoles.FirstOrDefaultAsync(r => r.Id == input.Id);
        if (role is null) return OperationResult.Fail(new[] { "Role not found." });

        role.Name = input.Name.Trim();
        role.Category = input.Category;
        role.DefaultCount = input.DefaultCount;
        role.MinCount = input.MinCount;
        role.MaxCount = input.MaxCount;
        role.IsOptional = input.IsOptional;
        role.IsGenericPreference = input.IsGenericPreference;
        role.RunAfterCapacity = input.RunAfterCapacity;
        role.RequiresFirstAid = input.RequiresFirstAid;
        role.HasEligibilityRestriction = input.HasEligibilityRestriction;
        role.PrePlacedVolunteerId = input.PrePlacedVolunteerId;
        role.SortOrder = input.SortOrder;
        role.IsActive = input.IsActive;
        await db.SaveChangesAsync();

        await SyncEligibility(db, role.Id, input.EligibleVolunteerIds);
        await db.SaveChangesAsync();
        _logger.LogInformation("Volunteer role {Id} updated.", role.Id);
        return OperationResult.Ok($"Role '{role.Name}' updated.");
    }

    public async Task<OperationResult> SetActiveAsync(int id, bool isActive)
    {
        await using var db = _dbContextFactory.CreateDbContext();
        var role = await db.VolunteerRoles.FirstOrDefaultAsync(r => r.Id == id);
        if (role is null) return OperationResult.Fail(new[] { "Role not found." });
        role.IsActive = isActive;
        await db.SaveChangesAsync();
        return OperationResult.Ok($"Role '{role.Name}' {(isActive ? "reactivated" : "retired")}.");
    }

    private static List<string> Validate(VolunteerRoleInput input)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(input.Name)) errors.Add("Name is required.");
        if (input.MinCount < 0) errors.Add("Min count cannot be negative.");
        if (input.MaxCount < input.MinCount) errors.Add("Max count must be >= min count.");
        if (input.DefaultCount < input.MinCount || input.DefaultCount > input.MaxCount)
            errors.Add("Default count must lie within [Min, Max].");
        if (input.RunAfterCapacity > input.MaxCount)
            errors.Add("Run-after capacity cannot exceed max count.");
        return errors;
    }

    private static async Task SyncEligibility(RaceResultsDbContext db, int roleId, List<int> volunteerIds)
    {
        var existing = await db.VolunteerRoleEligibilities.Where(e => e.VolunteerRoleId == roleId).ToListAsync();
        var desired = volunteerIds.Distinct().ToHashSet();
        var existingSet = existing.Select(e => e.VolunteerId).ToHashSet();

        foreach (var entry in existing.Where(e => !desired.Contains(e.VolunteerId)))
        {
            db.VolunteerRoleEligibilities.Remove(entry);
        }
        foreach (var vid in desired.Where(v => !existingSet.Contains(v)))
        {
            db.VolunteerRoleEligibilities.Add(new VolunteerRoleEligibility { VolunteerRoleId = roleId, VolunteerId = vid });
        }
    }
}
