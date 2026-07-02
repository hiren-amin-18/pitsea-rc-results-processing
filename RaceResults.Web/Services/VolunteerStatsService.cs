using Microsoft.EntityFrameworkCore;
using RaceResults.Web.Data;
using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

public class VolunteerStatsService : IVolunteerStatsService
{
    private readonly IDbContextFactory<RaceResultsDbContext> _dbContextFactory;

    public VolunteerStatsService(IDbContextFactory<RaceResultsDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public EventVolunteerStats GetEventStats(int eventId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var raceEvent = db.Events.FirstOrDefault(e => e.Id == eventId);
        if (raceEvent is null) return new EventVolunteerStats { EventId = eventId };

        var assignments = db.VolunteerAssignments
            .Where(a => a.EventId == eventId && !a.IsNoShow)
            .Include(a => a.VolunteerRole)
            .ToList();

        var activeRoles = db.VolunteerRoles
            .Where(r => r.EventType == raceEvent.EventType && r.IsActive)
            .OrderBy(r => r.Category).ThenBy(r => r.SortOrder)
            .ToList();

        var breakdown = activeRoles.Select(r => new RoleBreakdownRow
        {
            RoleName = r.Name,
            Category = r.Category,
            AssignedCount = assignments.Count(a => a.VolunteerRoleId == r.Id),
            DefaultCount = r.DefaultCount,
            MinCount = r.MinCount
        }).ToList();

        return new EventVolunteerStats
        {
            EventId = eventId,
            TotalAssignments = assignments.Count,
            DistinctVolunteers = assignments.Select(a => a.VolunteerId).Distinct().Count(),
            UnfilledRoleCount = breakdown.Count(b => b.Shortfall > 0),
            RoleBreakdown = breakdown
        };
    }

    public SeasonVolunteerStats GetSeasonStats(int year)
    {
        using var db = _dbContextFactory.CreateDbContext();

        var eventsInYear = db.Events.Where(e => e.EventDate.Year == year).ToList();
        var eventIds = eventsInYear.Select(e => e.Id).ToHashSet();
        var totalEvents = eventsInYear.Count;

        // No-shows (US42) earn nothing: no ballot entry, no attendance, no role history.
        var assignments = db.VolunteerAssignments
            .Where(a => eventIds.Contains(a.EventId) && !a.IsNoShow)
            .Include(a => a.Volunteer)
            .Include(a => a.VolunteerRole)
            .Include(a => a.Event)
            .ToList();

        var profiles = assignments
            .GroupBy(a => a.VolunteerId)
            .Select(g =>
            {
                var v = g.First().Volunteer!;
                var eventsAttended = g.Select(a => a.EventId).Distinct().Count();
                var ballot = v.IsClubMember ? eventsAttended : 0;
                return new VolunteerSeasonProfile
                {
                    VolunteerId = v.Id,
                    Name = v.Name,
                    IsClubMember = v.IsClubMember,
                    EventsAttended = eventsAttended,
                    Assignments = g.Count(),
                    BallotEntries = ballot,
                    RunAfterCount = g.Count(a => a.WillRunAfter),
                    IsEverPresent = totalEvents > 0 && eventsAttended == totalEvents,
                    RolesPerformed = g.GroupBy(a => a.VolunteerRole!.Name)
                        .Select(rg => new RolePerformedRow { RoleName = rg.Key, Times = rg.Count() })
                        .OrderByDescending(r => r.Times).ThenBy(r => r.RoleName).ToList(),
                    RunAndVolunteer = BuildRunAndVolunteer(db, v, year, eventsInYear)
                };
            })
            .OrderByDescending(p => p.BallotEntries).ThenByDescending(p => p.EventsAttended).ThenBy(p => p.Name)
            .ToList();

        var roleCoverage = eventsInYear
            .OrderBy(e => e.EventDate)
            .Select(e => new RoleCoverageTrendItem
            {
                EventId = e.Id,
                EventName = e.EventName,
                EventDate = e.EventDate,
                TotalAssignments = assignments.Count(a => a.EventId == e.Id),
                DistinctVolunteers = assignments.Where(a => a.EventId == e.Id).Select(a => a.VolunteerId).Distinct().Count()
            })
            .ToList();

        var mostActive = profiles
            .OrderByDescending(p => p.EventsAttended).ThenByDescending(p => p.Assignments).ThenBy(p => p.Name)
            .Take(10)
            .ToList();

        return new SeasonVolunteerStats
        {
            Year = year,
            TotalInstances = assignments.Count,
            UniqueVolunteers = profiles.Count,
            TotalBallotEntries = profiles.Sum(p => p.BallotEntries),
            EventsCovered = roleCoverage.Count(t => t.TotalAssignments > 0),
            VolunteerProfiles = profiles,
            MostActive = mostActive,
            RoleCoverageTrend = roleCoverage,
            AvailableYears = GetAvailableYears().ToList()
        };
    }

    public IReadOnlyList<int> GetAvailableYears()
    {
        using var db = _dbContextFactory.CreateDbContext();
        return db.VolunteerAssignments
            .Join(db.Events, a => a.EventId, e => e.Id, (_, e) => e.EventDate.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToList();
    }

    private static RunAndVolunteerSummary? BuildRunAndVolunteer(
        RaceResultsDbContext db, Volunteer v, int year, IReadOnlyList<RaceEvent> eventsInYear)
    {
        if (v.RunnerId is null) return null;

        var runEvents = db.Entrants
            .Where(e => e.RunnerId == v.RunnerId.Value)
            .Join(db.Events.Where(ev => ev.EventDate.Year == year), e => e.EventId, ev => ev.Id, (e, _) => e.EventId)
            .Distinct()
            .ToList();

        var volunteeredEvents = db.VolunteerAssignments
            .Where(a => a.VolunteerId == v.Id && !a.IsNoShow)
            .Join(db.Events.Where(ev => ev.EventDate.Year == year), a => a.EventId, ev => ev.Id, (a, _) => a.EventId)
            .Distinct()
            .ToList();

        var union = runEvents.Concat(volunteeredEvents).Distinct().Count();

        return new RunAndVolunteerSummary
        {
            RunCount = runEvents.Count,
            VolunteerCount = volunteeredEvents.Count,
            EventsInvolvedIn = union
        };
    }
}
