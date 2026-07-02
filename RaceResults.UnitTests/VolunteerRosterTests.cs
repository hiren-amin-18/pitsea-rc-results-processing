using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RaceResults.UnitTests.Helpers;
using RaceResults.Web.Data;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.UnitTests;

public class VolunteerRosterTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IDbContextFactory<RaceResultsDbContext> _factory;
    private readonly VolunteerRegistryService _registry;
    private readonly VolunteerRoleService _roleService;
    private readonly VolunteerRosterService _rosterService;
    private readonly VolunteerRosterExportService _exportService;

    public VolunteerRosterTests()
    {
        (var factory, _connection) = DbContextHelpers.CreateFactory();
        _factory = factory;
        _registry = new VolunteerRegistryService(factory, NullLogger<VolunteerRegistryService>.Instance);
        _roleService = new VolunteerRoleService(factory, NullLogger<VolunteerRoleService>.Instance);
        _rosterService = new VolunteerRosterService(factory, NullLogger<VolunteerRosterService>.Instance);
        _exportService = new VolunteerRosterExportService(_rosterService);
    }

    public void Dispose() => _connection.Dispose();

    // ---------- Registry ----------

    [Fact]
    public async Task CreateVolunteer_PersistsAndIsListed()
    {
        var result = await _registry.CreateAsync(new VolunteerInput
        {
            Name = "Alice",
            Gender = "Female",
            IsClubMember = true,
            IsFirstAidTrained = true
        });
        Assert.True(result.Success);
        var list = _registry.GetVolunteers();
        var item = Assert.Single(list);
        Assert.Equal("Alice", item.Volunteer.Name);
        Assert.True(item.Volunteer.IsFirstAidTrained);
    }

    [Fact]
    public async Task CreateVolunteer_NameRequired()
    {
        var result = await _registry.CreateAsync(new VolunteerInput { Name = "  ", Gender = "Female" });
        Assert.False(result.Success);
        Assert.Contains("Name is required.", result.Errors);
    }

    [Fact]
    public async Task DeactivatedVolunteer_PreservedButHiddenByDefault()
    {
        var create = await _registry.CreateAsync(new VolunteerInput { Name = "Bob", Gender = "Male" });
        Assert.True(create.Success);
        var bob = _registry.GetVolunteers().Single().Volunteer;

        await _registry.SetActiveAsync(bob.Id, false);
        Assert.Empty(_registry.GetVolunteers());
        Assert.Single(_registry.GetVolunteers(includeInactive: true));
    }

    [Fact]
    public async Task DeleteIfUnused_RemovesVolunteer_WhenNoAssignments()
    {
        await _registry.CreateAsync(new VolunteerInput { Name = "Carol", Gender = "Female" });
        var carol = _registry.GetVolunteers().Single().Volunteer;

        var result = await _registry.DeleteIfUnusedAsync(carol.Id);
        Assert.True(result.Success);
        Assert.Empty(_registry.GetVolunteers(includeInactive: true));
    }

    [Fact]
    public async Task DeleteIfUnused_RefusesIfVolunteerHasAssignments()
    {
        await _registry.CreateAsync(new VolunteerInput { Name = "Dan", Gender = "Male" });
        var dan = _registry.GetVolunteers().Single().Volunteer;
        var eventId = SeedEvent();
        await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        {
            EventId = eventId,
            VolunteerId = dan.Id,
            VolunteerRoleId = _roleService.GetRoles(EventType.CrownToCrown).First(r => r.Name == "Timekeeping").Id
        });

        var result = await _registry.DeleteIfUnusedAsync(dan.Id);
        Assert.False(result.Success);
        Assert.Single(_registry.GetVolunteers());
    }

    [Fact]
    public async Task DeleteAllUnused_OnlyRemovesVolunteersWithoutAssignments()
    {
        await _registry.CreateAsync(new VolunteerInput { Name = "Eve", Gender = "Female" });   // unused
        await _registry.CreateAsync(new VolunteerInput { Name = "Frank", Gender = "Male" });   // will be assigned
        await _registry.CreateAsync(new VolunteerInput { Name = "Gina", Gender = "Female" });  // unused
        var frank = _registry.GetVolunteers().Single(v => v.Volunteer.Name == "Frank").Volunteer;
        var eventId = SeedEvent();
        await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        {
            EventId = eventId,
            VolunteerId = frank.Id,
            VolunteerRoleId = _roleService.GetRoles(EventType.CrownToCrown).First(r => r.Name == "Timekeeping").Id
        });

        var result = await _registry.DeleteAllUnusedAsync();
        Assert.True(result.Success);
        var remaining = _registry.GetVolunteers(includeInactive: true).Select(v => v.Volunteer.Name).ToList();
        Assert.Equal(new[] { "Frank" }, remaining);
    }

    private int SeedEvent()
    {
        using var db = _factory.CreateDbContext();
        var ev = new RaceEvent { EventName = "Test Event", EventDate = new DateTime(2026, 4, 3), EventType = EventType.CrownToCrown };
        db.Events.Add(ev);
        db.SaveChanges();
        return ev.Id;
    }

    // ---------- Duplicate guard and merge (US39) ----------

    [Fact]
    public async Task CreateVolunteer_SameName_WarnsButAllows()
    {
        await _registry.CreateAsync(new VolunteerInput { Name = "Dave Smith", Gender = "Male" });
        var second = await _registry.CreateAsync(new VolunteerInput { Name = "dave smith", Gender = "Male" });

        Assert.True(second.Success);
        Assert.Contains(second.Warnings, w => w.Contains("already exists"));
        Assert.Equal(2, _registry.GetVolunteers().Count);
    }

    [Fact]
    public async Task Merge_MovesAssignments_DropsCollisions_AdoptsDetails()
    {
        var eventId = SeedEvent();
        await _registry.CreateAsync(new VolunteerInput { Name = "Dave Smith", Gender = "Male", Email = "dave@x.com" });
        await _registry.CreateAsync(new VolunteerInput { Name = "Dave Smith", Gender = "Male", Phone = "07000", IsFirstAidTrained = true });
        var volunteers = _registry.GetVolunteers();
        var survivor = volunteers.First().Volunteer;
        var duplicate = volunteers.Last().Volunteer;
        Assert.NotEqual(survivor.Id, duplicate.Id);

        var tk = _roleService.GetRoles(EventType.CrownToCrown).First(r => r.Name == "Timekeeping").Id;
        var setup = _roleService.GetRoles(EventType.CrownToCrown).First(r => r.Name == "Course Setup").Id;
        // Survivor and duplicate share Timekeeping (collision — dropped); duplicate also has Course Setup (moved).
        await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput { EventId = eventId, VolunteerId = survivor.Id, VolunteerRoleId = tk });
        await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput { EventId = eventId, VolunteerId = duplicate.Id, VolunteerRoleId = tk });
        await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput { EventId = eventId, VolunteerId = duplicate.Id, VolunteerRoleId = setup });

        var result = await _registry.MergeAsync(survivor.Id, duplicate.Id);
        Assert.True(result.Success);

        await using var db = _factory.CreateDbContext();
        Assert.Null(db.Volunteers.FirstOrDefault(v => v.Id == duplicate.Id));
        var survivorAssignments = db.VolunteerAssignments.Where(a => a.VolunteerId == survivor.Id).ToList();
        Assert.Equal(2, survivorAssignments.Count); // tk (own) + setup (moved); colliding tk dropped
        var merged = db.Volunteers.Single(v => v.Id == survivor.Id);
        Assert.Equal("dave@x.com", merged.Email);   // survivor's own kept
        Assert.Equal("07000", merged.Phone);        // adopted from duplicate
        Assert.True(merged.IsFirstAidTrained);      // OR of the two
    }

    [Fact]
    public async Task Merge_SameVolunteerTwice_Fails()
    {
        await _registry.CreateAsync(new VolunteerInput { Name = "Solo", Gender = "Female" });
        var solo = _registry.GetVolunteers().Single().Volunteer;
        var result = await _registry.MergeAsync(solo.Id, solo.Id);
        Assert.False(result.Success);
    }

    // ---------- Role catalogue (seed verified) ----------

    [Fact]
    public void Seed_Has24C2CRoles_AcrossThreeCategories()
    {
        var roles = _roleService.GetRoles(EventType.CrownToCrown);
        Assert.Equal(24, roles.Count);
        Assert.Equal(3, roles.Count(r => r.Category == RoleCategory.Leadership));
        Assert.Equal(10, roles.Count(r => r.Category == RoleCategory.FinishArea));
        Assert.Equal(11, roles.Count(r => r.Category == RoleCategory.Course));
    }

    [Fact]
    public void Seed_LeadAndResultsAreRestricted_FirstAidRolesMarked()
    {
        var roles = _roleService.GetRoles(EventType.CrownToCrown);
        Assert.True(roles.Single(r => r.Name == "Lead").HasEligibilityRestriction);
        Assert.True(roles.Single(r => r.Name == "Results").HasEligibilityRestriction);
        Assert.True(roles.Single(r => r.Name == "First Aid and Prizes").RequiresFirstAid);
        Assert.True(roles.Single(r => r.Name == "First Aid On Course").RequiresFirstAid);

        var numberCollection = roles.Single(r => r.Name == "Number Collection");
        Assert.Equal(1, numberCollection.RunAfterCapacity);
        Assert.Equal(1, numberCollection.MinCount);
        Assert.Equal(2, numberCollection.MaxCount);

        var otd = roles.Single(r => r.Name == "On The Day Registration");
        Assert.Equal(2, otd.RunAfterCapacity);
        Assert.Equal(4, otd.DefaultCount);
    }

    [Fact]
    public async Task RoleValidation_RejectsBadCounts()
    {
        var result = await _roleService.CreateAsync(new VolunteerRoleInput
        {
            Name = "Bad", Category = RoleCategory.Course, EventType = EventType.CrownToCrown,
            DefaultCount = 3, MinCount = 5, MaxCount = 5
        });
        Assert.False(result.Success);
    }

    // ---------- Roster building ----------

    [Fact]
    public async Task AddAssignment_AddsToRoster_AndCountsAgainstComplement()
    {
        var alice = await CreateVolunteerAsync("Alice", "Female");
        var timekeeping = (await GetRoleAsync("Timekeeping")).Id;

        var add = await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        {
            EventId = 1, VolunteerId = alice.Id, VolunteerRoleId = timekeeping
        });
        Assert.True(add.Success);

        var roster = _rosterService.GetRoster(1);
        var tk = roster.ByCategory[RoleCategory.FinishArea].Single(r => r.Role.Name == "Timekeeping");
        Assert.Single(tk.Assignments);
        Assert.True(tk.IsUnderComplement); // default = 2
    }

    [Fact]
    public async Task DoubleBooking_AllowedButWarns()
    {
        var alice = await CreateVolunteerAsync("Alice", "Female");
        var numColl = (await GetRoleAsync("Number Collection")).Id;
        var funnel = (await GetRoleAsync("Finish Line Funnel")).Id;

        await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        { EventId = 1, VolunteerId = alice.Id, VolunteerRoleId = numColl });

        var second = await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        { EventId = 1, VolunteerId = alice.Id, VolunteerRoleId = funnel });

        Assert.True(second.Success);
        Assert.Contains(second.Warnings, w => w.Contains("Number Collection"));

        var roster = _rosterService.GetRoster(1);
        Assert.Contains(alice.Id, roster.DoubleBookedVolunteerIds);
    }

    [Fact]
    public async Task FirstAidRole_RejectsNonFirstAidVolunteer()
    {
        var bob = await CreateVolunteerAsync("Bob", "Male", firstAid: false);
        var firstAidPrizes = (await GetRoleAsync("First Aid and Prizes")).Id;

        var result = await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        { EventId = 1, VolunteerId = bob.Id, VolunteerRoleId = firstAidPrizes });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("first-aid-trained"));
    }

    [Fact]
    public async Task FirstAidRole_AcceptsFirstAidVolunteer()
    {
        var carol = await CreateVolunteerAsync("Carol", "Female", firstAid: true);
        var firstAidPrizes = (await GetRoleAsync("First Aid and Prizes")).Id;

        var result = await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        { EventId = 1, VolunteerId = carol.Id, VolunteerRoleId = firstAidPrizes });

        Assert.True(result.Success);
    }

    [Fact]
    public async Task RestrictedRole_RejectsVolunteerNotOnAllowList()
    {
        var dave = await CreateVolunteerAsync("Dave", "Male");
        var leadId = (await GetRoleAsync("Lead")).Id;

        var result = await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        { EventId = 1, VolunteerId = dave.Id, VolunteerRoleId = leadId });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("restricted"));
    }

    [Fact]
    public async Task RestrictedRole_AcceptsVolunteerOnAllowList()
    {
        var hiren = await CreateVolunteerAsync("Hiren", "Male");
        var lead = await GetRoleAsync("Lead");

        // Add Hiren to the Lead allow-list.
        var leadInput = new VolunteerRoleInput();
        Assert.True(_roleService.TryGetRoleForEdit(lead.Id, out leadInput));
        leadInput.EligibleVolunteerIds.Add(hiren.Id);
        await _roleService.UpdateAsync(leadInput);

        var result = await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        { EventId = 1, VolunteerId = hiren.Id, VolunteerRoleId = lead.Id });

        Assert.True(result.Success);
    }

    [Fact]
    public async Task MaxCount_RejectsBeyondLimit()
    {
        var photographer = await GetRoleAsync("Photographer"); // Max = 1
        var v1 = await CreateVolunteerAsync("A", "Female");
        var v2 = await CreateVolunteerAsync("B", "Male");

        var first = await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        { EventId = 1, VolunteerId = v1.Id, VolunteerRoleId = photographer.Id });
        var second = await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        { EventId = 1, VolunteerId = v2.Id, VolunteerRoleId = photographer.Id });

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Contains(second.Errors, e => e.Contains("maximum"));
    }

    [Fact]
    public async Task RunAfterCapacity_RejectsBeyondLimit()
    {
        var numCol = await GetRoleAsync("Number Collection"); // RunAfterCapacity = 1
        var a = await CreateVolunteerAsync("A", "Female");
        var b = await CreateVolunteerAsync("B", "Male");

        await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        { EventId = 1, VolunteerId = a.Id, VolunteerRoleId = numCol.Id, WillRunAfter = true });

        var second = await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        { EventId = 1, VolunteerId = b.Id, VolunteerRoleId = numCol.Id, WillRunAfter = true });

        Assert.False(second.Success);
        Assert.Contains(second.Errors, e => e.Contains("run-after"));
    }

    // ---------- Edit assignment (US36) ----------

    [Fact]
    public async Task UpdateAssignment_ChangesRoleAndNote_PreservesPreferences()
    {
        var alice = await CreateVolunteerAsync("Alice", "Female");
        var tk = (await GetRoleAsync("Timekeeping")).Id;
        var courseSetup = (await GetRoleAsync("Course Setup")).Id;
        await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        { EventId = 1, VolunteerId = alice.Id, VolunteerRoleId = tk, Note = "old", CantWalkFar = true });

        var assignmentId = _rosterService.GetRoster(1).ByCategory[RoleCategory.FinishArea]
            .Single(r => r.Role.Name == "Timekeeping").Assignments.Single().Assignment.Id;

        var result = await _rosterService.UpdateAssignmentAsync(new VolunteerAssignmentInput
        { Id = assignmentId, VolunteerRoleId = courseSetup, Note = "new" });

        Assert.True(result.Success);
        await using var db = _factory.CreateDbContext();
        var updated = db.VolunteerAssignments.Single(a => a.Id == assignmentId);
        Assert.Equal(courseSetup, updated.VolunteerRoleId);
        Assert.Equal("new", updated.Note);
        Assert.True(updated.CantWalkFar); // preference preserved (US36 AC4)
        Assert.Equal(alice.Id, updated.VolunteerId);
    }

    [Fact]
    public async Task UpdateAssignment_SavingUnchanged_DoesNotTripCapacityCheck()
    {
        var photographer = (await GetRoleAsync("Photographer")).Id; // Max = 1
        var a = await CreateVolunteerAsync("A", "Female");
        await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        { EventId = 1, VolunteerId = a.Id, VolunteerRoleId = photographer });

        var assignmentId = _rosterService.GetRoster(1).ByCategory[RoleCategory.FinishArea]
            .Single(r => r.Role.Name == "Photographer").Assignments.Single().Assignment.Id;

        var result = await _rosterService.UpdateAssignmentAsync(new VolunteerAssignmentInput
        { Id = assignmentId, VolunteerRoleId = photographer });

        Assert.True(result.Success);
    }

    [Fact]
    public async Task UpdateAssignment_MovingToFullRole_Fails()
    {
        var photographer = (await GetRoleAsync("Photographer")).Id; // Max = 1
        var tk = (await GetRoleAsync("Timekeeping")).Id;
        var a = await CreateVolunteerAsync("A", "Female");
        var b = await CreateVolunteerAsync("B", "Male");
        await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        { EventId = 1, VolunteerId = a.Id, VolunteerRoleId = photographer });
        await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        { EventId = 1, VolunteerId = b.Id, VolunteerRoleId = tk });

        var bAssignment = _rosterService.GetRoster(1).ByCategory[RoleCategory.FinishArea]
            .Single(r => r.Role.Name == "Timekeeping").Assignments.Single().Assignment.Id;

        var result = await _rosterService.UpdateAssignmentAsync(new VolunteerAssignmentInput
        { Id = bAssignment, VolunteerRoleId = photographer });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("maximum"));
    }

    [Fact]
    public async Task GenericMarshalSentinel_FillsMostUnderStaffedMarshalPoint()
    {
        var alice = await CreateVolunteerAsync("Alice", "Female");
        var bob = await CreateVolunteerAsync("Bob", "Male");
        var sentinel = (await GetRoleAsync("Marshal (any point)")).Id;

        // Pre-fill Marshal Point 1 (default 2) with one person so Marshal Point 4 (default 3) is the most
        // under-staffed. Point 4 has gap 3, points 2/3/5/5a/6/7 have gap 2, point 1 now has gap 1.
        var mp1 = (await GetRoleAsync("Marshal Point 1")).Id;
        await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        { EventId = 1, VolunteerId = alice.Id, VolunteerRoleId = mp1 });

        var result = await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        { EventId = 1, VolunteerId = bob.Id, VolunteerRoleId = sentinel });

        Assert.True(result.Success);
        Assert.Contains("Marshal Point 4", result.Messages.Single());

        var roster = _rosterService.GetRoster(1);
        var mp4 = roster.ByCategory[RoleCategory.Course].Single(r => r.Role.Name == "Marshal Point 4");
        Assert.Contains(mp4.Assignments, a => a.Volunteer.Id == bob.Id);
    }

    [Fact]
    public async Task GenericMarshalSentinel_ErrorsWhenAllMarshalPointsFull()
    {
        var sentinel = (await GetRoleAsync("Marshal (any point)")).Id;
        var marshalPoints = _roleService.GetRoles(EventType.CrownToCrown)
            .Where(r => r.Name.StartsWith("Marshal Point")).ToList();
        var fillCount = marshalPoints.Sum(r => r.DefaultCount);

        // Fill every marshal point to its default count with unique volunteers.
        int i = 0;
        foreach (var mp in marshalPoints)
        {
            for (int slot = 0; slot < mp.DefaultCount; slot++)
            {
                var v = await CreateVolunteerAsync($"Filler{++i}", "Female");
                await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
                { EventId = 1, VolunteerId = v.Id, VolunteerRoleId = mp.Id });
            }
        }

        var extra = await CreateVolunteerAsync("Extra", "Male");
        var result = await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        { EventId = 1, VolunteerId = extra.Id, VolunteerRoleId = sentinel });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("No marshal point has an available spot"));
    }

    [Fact]
    public async Task GenericMarshalSentinel_RejectsWillRunAfter()
    {
        var alice = await CreateVolunteerAsync("Alice", "Female");
        var sentinel = (await GetRoleAsync("Marshal (any point)")).Id;

        var result = await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        { EventId = 1, VolunteerId = alice.Id, VolunteerRoleId = sentinel, WillRunAfter = true });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("cannot also run after"));
    }

    [Fact]
    public async Task RemoveAssignment_RemovesFromRoster()
    {
        var alice = await CreateVolunteerAsync("Alice", "Female");
        var timekeeping = (await GetRoleAsync("Timekeeping")).Id;
        await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        { EventId = 1, VolunteerId = alice.Id, VolunteerRoleId = timekeeping });

        var rosterBefore = _rosterService.GetRoster(1);
        var assignmentId = rosterBefore.ByCategory[RoleCategory.FinishArea]
            .Single(r => r.Role.Name == "Timekeeping").Assignments.Single().Assignment.Id;

        var result = await _rosterService.RemoveAssignmentAsync(assignmentId);
        Assert.True(result.Success);

        var rosterAfter = _rosterService.GetRoster(1);
        Assert.Empty(rosterAfter.ByCategory[RoleCategory.FinishArea].Single(r => r.Role.Name == "Timekeeping").Assignments);
    }

    // ---------- Copy from previous event ----------

    [Fact]
    public async Task CopyFromPreviousEvent_CopiesAssignments_SkipsInactive()
    {
        // Set up: event 1 (seeded), event 2 (new), assignments on event 1, then copy to event 2.
        var alice = await CreateVolunteerAsync("Alice", "Female");
        var bob = await CreateVolunteerAsync("Bob", "Male");

        var tk = (await GetRoleAsync("Timekeeping")).Id;
        await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        { EventId = 1, VolunteerId = alice.Id, VolunteerRoleId = tk });
        await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        { EventId = 1, VolunteerId = bob.Id, VolunteerRoleId = tk });

        // Create event 2 after event 1.
        int newEventId;
        await using (var db = _factory.CreateDbContext())
        {
            db.Events.Add(new RaceEvent
            {
                EventName = "Crown to Crown - May",
                EventDate = new DateTime(2026, 5, 13),
                EventType = EventType.CrownToCrown
            });
            await db.SaveChangesAsync();
            newEventId = db.Events.OrderByDescending(e => e.Id).First().Id;
        }

        // Bob goes inactive — should be skipped on copy.
        await _registry.SetActiveAsync(bob.Id, false);

        var result = await _rosterService.CopyFromPreviousEventAsync(newEventId);
        Assert.True(result.Success);
        Assert.Equal(1, result.CopiedCount);
        Assert.Contains(result.SkippedItems, s => s.Contains("Bob") && s.Contains("inactive"));

        var roster = _rosterService.GetRoster(newEventId);
        var tkRow = roster.ByCategory[RoleCategory.FinishArea].Single(r => r.Role.Name == "Timekeeping");
        Assert.Single(tkRow.Assignments);
        Assert.Equal("Alice", tkRow.Assignments.Single().Volunteer.Name);
    }

    [Fact]
    public async Task CopyFromPreviousEvent_NoEarlierEvent_Fails()
    {
        var result = await _rosterService.CopyFromPreviousEventAsync(1);
        Assert.False(result.Success);
        Assert.Contains("earlier event", result.ErrorMessage ?? "");
    }

    // ---------- Exports ----------

    [Fact]
    public async Task ExportExcel_ProducesValidXlsx_WithRows()
    {
        var alice = await CreateVolunteerAsync("Alice", "Female");
        var tk = (await GetRoleAsync("Timekeeping")).Id;
        await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        { EventId = 1, VolunteerId = alice.Id, VolunteerRoleId = tk });

        var bytes = _exportService.ExportExcel(1);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 1000);
        // XLSX is a zip — first two bytes are PK
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    [Fact]
    public void ExportExcel_EmptyRoster_StillProduces()
    {
        var bytes = _exportService.ExportExcel(1);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 500);
    }

    [Fact]
    public async Task ExportPdf_ProducesValidPdf()
    {
        var alice = await CreateVolunteerAsync("Alice", "Female");
        var tk = (await GetRoleAsync("Timekeeping")).Id;
        await _rosterService.AddAssignmentAsync(new VolunteerAssignmentInput
        { EventId = 1, VolunteerId = alice.Id, VolunteerRoleId = tk });

        var bytes = _exportService.ExportPdf(1);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 500);
        // PDF magic: %PDF
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
    }

    // ---------- Helpers ----------

    private async Task<Volunteer> CreateVolunteerAsync(string name, string gender, bool firstAid = false, bool clubMember = true)
    {
        var result = await _registry.CreateAsync(new VolunteerInput
        {
            Name = name, Gender = gender, IsClubMember = clubMember, IsFirstAidTrained = firstAid
        });
        Assert.True(result.Success);
        await using var db = _factory.CreateDbContext();
        return db.Volunteers.Single(v => v.Name == name);
    }

    private async Task<VolunteerRole> GetRoleAsync(string name)
    {
        await using var db = _factory.CreateDbContext();
        return db.VolunteerRoles.Single(r => r.Name == name && r.EventType == EventType.CrownToCrown);
    }
}
