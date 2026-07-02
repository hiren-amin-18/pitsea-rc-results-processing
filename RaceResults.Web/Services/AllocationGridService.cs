using Microsoft.EntityFrameworkCore;
using RaceResults.Web.Data;
using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

/// <summary>Loads and saves the allocate-form grid per event (US40): who is ticked and their per-event
/// preference values. A fresh grid starts from each volunteer's usual preferences.</summary>
public interface IAllocationGridService
{
    IReadOnlyList<AllocationGridRow> GetGrid(int eventId);
    Task SaveGridAsync(int eventId, IReadOnlyList<AllocationCandidate> tickedCandidates);
}

public class AllocationGridService : IAllocationGridService
{
    private readonly IDbContextFactory<RaceResultsDbContext> _dbContextFactory;

    public AllocationGridService(IDbContextFactory<RaceResultsDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public IReadOnlyList<AllocationGridRow> GetGrid(int eventId)
    {
        using var db = _dbContextFactory.CreateDbContext();

        var volunteers = db.Volunteers
            .Where(v => v.IsActive)
            .OrderBy(v => v.Name)
            .ToList();

        var saved = db.AllocationCandidateRecords
            .Where(r => r.EventId == eventId)
            .ToDictionary(r => r.VolunteerId);

        return volunteers.Select(v =>
        {
            if (saved.TryGetValue(v.Id, out var record))
            {
                return new AllocationGridRow
                {
                    Volunteer = v,
                    IsSelected = true,
                    Candidate = new AllocationCandidate
                    {
                        VolunteerId = v.Id,
                        PreferredRoleId = record.PreferredRoleId,
                        WantsToRunAfter = record.WantsToRunAfter,
                        WantsNearFinish = record.WantsNearFinish,
                        CantWalkFar = record.CantWalkFar,
                        WantsSeated = record.WantsSeated,
                        WantsRaceHq = record.WantsRaceHq,
                        AnyRole = record.AnyRole
                    }
                };
            }
            return new AllocationGridRow
            {
                Volunteer = v,
                IsSelected = false,
                Candidate = new AllocationCandidate
                {
                    VolunteerId = v.Id,
                    PreferredRoleId = v.DefaultPreferredRoleId,
                    WantsToRunAfter = v.DefaultWantsToRunAfter,
                    WantsNearFinish = v.DefaultWantsNearFinish,
                    CantWalkFar = v.DefaultCantWalkFar,
                    WantsSeated = v.DefaultWantsSeated,
                    WantsRaceHq = v.DefaultWantsRaceHq,
                    AnyRole = v.DefaultAnyRole
                }
            };
        }).ToList();
    }

    public async Task SaveGridAsync(int eventId, IReadOnlyList<AllocationCandidate> tickedCandidates)
    {
        await using var db = _dbContextFactory.CreateDbContext();

        var existing = await db.AllocationCandidateRecords
            .Where(r => r.EventId == eventId)
            .ToListAsync();
        db.AllocationCandidateRecords.RemoveRange(existing);

        foreach (var c in tickedCandidates.DistinctBy(c => c.VolunteerId))
        {
            db.AllocationCandidateRecords.Add(new AllocationCandidateRecord
            {
                EventId = eventId,
                VolunteerId = c.VolunteerId,
                PreferredRoleId = c.PreferredRoleId,
                WantsToRunAfter = c.WantsToRunAfter,
                WantsNearFinish = c.WantsNearFinish,
                CantWalkFar = c.CantWalkFar,
                WantsSeated = c.WantsSeated,
                WantsRaceHq = c.WantsRaceHq,
                AnyRole = c.AnyRole
            });
        }
        await db.SaveChangesAsync();
    }
}
