using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RaceResults.UnitTests.Helpers;
using RaceResults.Web.Data;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.UnitTests;

public class VolunteerStatsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IDbContextFactory<RaceResultsDbContext> _factory;
    private readonly VolunteerRegistryService _registry;
    private readonly VolunteerRosterService _rosterService;
    private readonly VolunteerStatsService _stats;

    public VolunteerStatsTests()
    {
        (var factory, _connection) = DbContextHelpers.CreateFactory();
        _factory = factory;
        _registry = new VolunteerRegistryService(factory, NullLogger<VolunteerRegistryService>.Instance);
        _stats = new VolunteerStatsService(factory);
        _rosterService = new VolunteerRosterService(factory, NullLogger<VolunteerRosterService>.Instance, _stats);
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void EmptySeason_ReturnsEmptyStats()
    {
        var result = _stats.GetSeasonStats(2026);
        Assert.Equal(0, result.TotalInstances);
        Assert.Equal(0, result.UniqueVolunteers);
        Assert.Equal(0, result.TotalBallotEntries);
    }

    [Fact]
    public async Task SingleEventSeason_EverPresentCollapsesToAttendedThatEvent()
    {
        var alice = await CreateVolunteer("Alice", member: true);
        await AssignAsync(1, alice.Id, "Timekeeping");

        var result = _stats.GetSeasonStats(2026);
        var profile = Assert.Single(result.VolunteerProfiles);
        Assert.True(profile.IsEverPresent);
        Assert.Equal(1, profile.EventsAttended);
    }

    [Fact]
    public async Task MultiEvent_OnlyVolunteerAtEveryEventIsEverPresent()
    {
        var alice = await CreateVolunteer("Alice");
        var bob = await CreateVolunteer("Bob");
        var event2 = await CreateExtraEvent(new DateTime(2026, 5, 13));

        await AssignAsync(1, alice.Id, "Timekeeping");
        await AssignAsync(event2, alice.Id, "Timekeeping");
        await AssignAsync(1, bob.Id, "Course Setup");

        var result = _stats.GetSeasonStats(2026);
        Assert.True(result.VolunteerProfiles.Single(p => p.Name == "Alice").IsEverPresent);
        Assert.False(result.VolunteerProfiles.Single(p => p.Name == "Bob").IsEverPresent);
    }

    [Fact]
    public async Task NonMemberVolunteer_EarnsAssignmentsButZeroBallotEntries()
    {
        var ian = await CreateVolunteer("Ian", member: false);
        await AssignAsync(1, ian.Id, "Marshal Point 7");

        var result = _stats.GetSeasonStats(2026);
        var profile = Assert.Single(result.VolunteerProfiles);
        Assert.Equal(1, profile.Assignments);
        Assert.Equal(0, profile.BallotEntries);
        Assert.Equal(0, result.TotalBallotEntries);
    }

    [Fact]
    public async Task VolunteerWithMultipleRolesAtOneEvent_CountsOnceForEventsAndBallot()
    {
        var alice = await CreateVolunteer("Alice", firstAid: true);
        await AssignAsync(1, alice.Id, "Number Collection");
        await AssignAsync(1, alice.Id, "Finish Line Funnel");

        var result = _stats.GetSeasonStats(2026);
        var profile = Assert.Single(result.VolunteerProfiles);
        Assert.Equal(1, profile.EventsAttended);
        Assert.Equal(2, profile.Assignments);
        Assert.Equal(1, profile.BallotEntries);
    }

    [Fact]
    public async Task TiesForMostActive_BreakByAssignmentsThenName()
    {
        var alice = await CreateVolunteer("Alice");
        var zoe = await CreateVolunteer("Zoe");
        var event2 = await CreateExtraEvent(new DateTime(2026, 5, 13));

        // Both attended both events; Zoe has 3 assignments, Alice has 2.
        await AssignAsync(1, alice.Id, "Timekeeping");
        await AssignAsync(event2, alice.Id, "Course Setup");
        await AssignAsync(1, zoe.Id, "Marshal Point 1");
        await AssignAsync(1, zoe.Id, "Water Table");
        await AssignAsync(event2, zoe.Id, "Marshal Point 1");

        var result = _stats.GetSeasonStats(2026);
        Assert.Equal("Zoe", result.MostActive[0].Name);
        Assert.Equal("Alice", result.MostActive[1].Name);
    }

    [Fact]
    public async Task PerEventStats_ReturnsCorrectBreakdown()
    {
        var alice = await CreateVolunteer("Alice");
        await AssignAsync(1, alice.Id, "Timekeeping");

        var stats = _stats.GetEventStats(1);
        Assert.Equal(1, stats.TotalAssignments);
        Assert.Equal(1, stats.DistinctVolunteers);
        var tk = stats.RoleBreakdown.Single(r => r.RoleName == "Timekeeping");
        Assert.Equal(1, tk.AssignedCount);
        Assert.Equal(2, tk.DefaultCount);
        Assert.Equal(1, tk.Shortfall);
        Assert.True(stats.UnfilledRoleCount > 0);
    }

    [Fact]
    public async Task RoleCoverageTrend_OrdersByEventDate()
    {
        var alice = await CreateVolunteer("Alice");
        var event2 = await CreateExtraEvent(new DateTime(2026, 5, 13));
        var event3 = await CreateExtraEvent(new DateTime(2026, 7, 8));

        await AssignAsync(1, alice.Id, "Timekeeping");
        await AssignAsync(event3, alice.Id, "Timekeeping");

        var result = _stats.GetSeasonStats(2026);
        var dates = result.RoleCoverageTrend.Select(t => t.EventDate).ToList();
        Assert.Equal(dates.OrderBy(d => d).ToList(), dates);
        Assert.Equal(3, result.RoleCoverageTrend.Count);
        Assert.Equal(0, result.RoleCoverageTrend.Single(t => t.EventId == event2).TotalAssignments);
    }

    // ---------- Helpers ----------

    private async Task<Volunteer> CreateVolunteer(string name, bool member = true, bool firstAid = false)
    {
        await _registry.CreateAsync(new VolunteerInput
        {
            Name = name, Gender = "Female", IsClubMember = member, IsFirstAidTrained = firstAid
        });
        await using var db = _factory.CreateDbContext();
        return db.Volunteers.Single(v => v.Name == name);
    }

    private async Task AssignAsync(int eventId, int volunteerId, string roleName)
    {
        await using var db = _factory.CreateDbContext();
        var role = db.VolunteerRoles.Single(r => r.Name == roleName && r.EventType == EventType.CrownToCrown);
        var add = await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        {
            EventId = eventId, VolunteerId = volunteerId, VolunteerRoleId = role.Id
        });
        Assert.True(add.Success, string.Join("; ", add.Errors));
    }

    private async Task<int> CreateExtraEvent(DateTime date)
    {
        await using var db = _factory.CreateDbContext();
        var ev = new RaceEvent
        {
            EventName = $"Crown to Crown - {date:MMM yyyy}",
            EventDate = date,
            EventType = EventType.CrownToCrown
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return ev.Id;
    }
}
