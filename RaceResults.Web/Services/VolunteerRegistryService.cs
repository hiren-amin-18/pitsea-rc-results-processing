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
        db.Volunteers.Add(volunteer);
        await db.SaveChangesAsync();
        _logger.LogInformation("Volunteer {Id} created: {Name}.", volunteer.Id, volunteer.Name);
        return OperationResult.Ok($"Volunteer '{volunteer.Name}' added.");
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

    private static List<string> Validate(VolunteerInput input)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(input.Name)) errors.Add("Name is required.");
        return errors;
    }

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
