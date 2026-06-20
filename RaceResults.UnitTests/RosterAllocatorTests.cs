using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RaceResults.UnitTests.Helpers;
using RaceResults.Web.Data;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.UnitTests;

public class RosterAllocatorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IDbContextFactory<RaceResultsDbContext> _factory;
    private readonly VolunteerRegistryService _registry;
    private readonly VolunteerRoleService _roleService;
    private readonly VolunteerRosterService _rosterService;
    private readonly VolunteerStatsService _stats;
    private readonly RosterAllocator _allocator;
    private readonly RosterDraftApplier _applier;

    public RosterAllocatorTests()
    {
        (var factory, _connection) = DbContextHelpers.CreateFactory();
        _factory = factory;
        _registry = new VolunteerRegistryService(factory, NullLogger<VolunteerRegistryService>.Instance);
        _roleService = new VolunteerRoleService(factory, NullLogger<VolunteerRoleService>.Instance);
        _stats = new VolunteerStatsService(factory);
        _rosterService = new VolunteerRosterService(factory, NullLogger<VolunteerRosterService>.Instance, _stats);
        _allocator = new RosterAllocator(factory);
        _applier = new RosterDraftApplier(_rosterService);
    }

    public void Dispose() => _connection.Dispose();

    // ---------- Pre-place wins ----------

    [Fact]
    public async Task PrePlacedVolunteer_PlacedAtPrePlacedRole_EvenWithOtherPreference()
    {
        var ian = await CreateVolunteer("Ian", "Male", member: false);
        await PrePlaceAsync("Marshal Point 7", ian.Id);

        var draft = _allocator.Propose(1, new[] {
            new AllocationCandidate { VolunteerId = ian.Id, PreferredRoleId = (await GetRole("Water Table")).Id }
        });

        var prop = Assert.Single(draft.Proposals);
        Assert.Equal("Marshal Point 7", prop.RoleName);
        Assert.Equal(AllocationReason.PrePlaced, prop.Reason);
    }

    // ---------- Eligibility wins over preference ----------

    [Fact]
    public async Task RestrictedRole_RejectsNonAllowListed_EvenIfPreferred()
    {
        var dave = await CreateVolunteer("Dave", "Male");
        var lead = await GetRole("Lead");

        var draft = _allocator.Propose(1, new[] {
            new AllocationCandidate { VolunteerId = dave.Id, PreferredRoleId = lead.Id }
        });

        Assert.DoesNotContain(draft.Proposals, p => p.VolunteerRoleId == lead.Id);
    }

    [Fact]
    public async Task RestrictedRole_PicksAllowListedCandidate()
    {
        var hiren = await CreateVolunteer("Hiren", "Male");
        var lead = await GetRole("Lead");
        await AllowListAsync(lead.Id, hiren.Id);

        var draft = _allocator.Propose(1, new[] {
            new AllocationCandidate { VolunteerId = hiren.Id }
        });

        Assert.Contains(draft.Proposals, p => p.VolunteerRoleId == lead.Id && p.VolunteerId == hiren.Id);
    }

    // ---------- Run-after rotation ----------

    [Fact]
    public async Task RunAfterRotation_PrefersCandidateWithFewerRunAfters()
    {
        // Two volunteers, both want run-after for the next event; Alice already did run-after at event-1.
        var alice = await CreateVolunteer("Alice", "Female");
        var bob = await CreateVolunteer("Bob", "Male");
        var numCol = await GetRole("Number Collection");

        // History at event 1: Alice was run-after.
        await SeedHistoryRunAfter(1, alice.Id, numCol.Id);

        // Now allocate event 2 (later same year).
        var event2 = await CreateExtraEvent(new DateTime(2026, 5, 13));

        var draft = _allocator.Propose(event2, new[] {
            new AllocationCandidate { VolunteerId = alice.Id, WantsToRunAfter = true },
            new AllocationCandidate { VolunteerId = bob.Id, WantsToRunAfter = true }
        });

        // Bob (zero prior run-after) should get the run-after slot first.
        var runAfter = draft.Proposals.Single(p => p.VolunteerRoleId == numCol.Id && p.WillRunAfter);
        Assert.Equal(bob.Id, runAfter.VolunteerId);
        Assert.Equal(AllocationReason.RunAfterRotation, runAfter.Reason);
    }

    // ---------- Preferences ----------

    [Fact]
    public async Task CantWalkFar_PlacedAtMarshal1Or2()
    {
        var alice = await CreateVolunteer("Alice", "Female");
        var draft = _allocator.Propose(1, new[] {
            new AllocationCandidate { VolunteerId = alice.Id, CantWalkFar = true }
        });
        var prop = Assert.Single(draft.Proposals);
        Assert.Contains(prop.RoleName, new[] { "Marshal Point 1", "Marshal Point 2" });
    }

    [Fact]
    public async Task WantsSeated_PlacedAtNumberCollectionOrOTDRegistration()
    {
        var alice = await CreateVolunteer("Alice", "Female");
        var draft = _allocator.Propose(1, new[] {
            new AllocationCandidate { VolunteerId = alice.Id, WantsSeated = true }
        });
        var prop = Assert.Single(draft.Proposals);
        Assert.Contains(prop.RoleName, new[] { "Number Collection", "On The Day Registration" });
    }

    // ---------- First-aid enforcement ----------

    [Fact]
    public async Task NonFirstAidVolunteer_NotProposedForFirstAidRole_EvenIfPreferred()
    {
        var bob = await CreateVolunteer("Bob", "Male", firstAid: false);
        var firstAidPrizes = await GetRole("First Aid and Prizes");

        var draft = _allocator.Propose(1, new[] {
            new AllocationCandidate { VolunteerId = bob.Id, PreferredRoleId = firstAidPrizes.Id }
        });

        Assert.DoesNotContain(draft.Proposals, p => p.VolunteerRoleId == firstAidPrizes.Id);
        // Bob will be placed elsewhere or end up unplaced — both are acceptable.
    }

    // ---------- Mix across season ----------

    [Fact]
    public async Task MixUp_PrefersRoleNotRecentlyDoneByCandidate()
    {
        var alice = await CreateVolunteer("Alice", "Female");
        var bob = await CreateVolunteer("Bob", "Male");

        var waterTable = await GetRole("Water Table");

        // History: Alice did Water Table at event 1; Bob did not.
        await SeedHistory(1, alice.Id, waterTable.Id);

        // Allocate event 2.
        var event2 = await CreateExtraEvent(new DateTime(2026, 5, 13));
        var draft = _allocator.Propose(event2, new[] {
            new AllocationCandidate { VolunteerId = alice.Id },
            new AllocationCandidate { VolunteerId = bob.Id }
        });

        var waterTableProps = draft.Proposals.Where(p => p.VolunteerRoleId == waterTable.Id).ToList();
        // If Water Table appears in the draft for one of them, prefer the one who didn't do it last time.
        if (waterTableProps.Count == 1)
        {
            Assert.NotEqual(alice.Id, waterTableProps[0].VolunteerId);
        }
    }

    // ---------- Marshal gender mix ----------

    [Fact]
    public async Task MarshalGenderMix_MixedPairProducedWhenPossible()
    {
        // Three candidates: 2F + 1M. Marshal Point 3 takes 2; expect at least one M and one F.
        var f1 = await CreateVolunteer("F1", "Female");
        var f2 = await CreateVolunteer("F2", "Female");
        var m1 = await CreateVolunteer("M1", "Male");

        var draft = _allocator.Propose(1, new[] {
            new AllocationCandidate { VolunteerId = f1.Id, PreferredRoleId = (await GetRole("Marshal Point 3")).Id },
            new AllocationCandidate { VolunteerId = f2.Id, PreferredRoleId = (await GetRole("Marshal Point 3")).Id },
            new AllocationCandidate { VolunteerId = m1.Id, AnyRole = true }
        });

        var mp3 = (await GetRole("Marshal Point 3")).Id;
        var mp3Assignees = draft.Proposals.Where(p => p.VolunteerRoleId == mp3).Select(p => p.VolunteerId).ToList();
        if (mp3Assignees.Count >= 2)
        {
            await using var db = _factory.CreateDbContext();
            var genders = db.Volunteers.Where(v => mp3Assignees.Contains(v.Id)).Select(v => v.Gender).ToList();
            Assert.True(genders.Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1,
                $"Expected mixed genders at Marshal Point 3 when possible; got {string.Join(",", genders)}.");
        }
    }

    // ---------- Apply step ----------

    [Fact]
    public async Task ApplyDraft_PersistsAssignments()
    {
        var alice = await CreateVolunteer("Alice", "Female");
        var draft = _allocator.Propose(1, new[] {
            new AllocationCandidate { VolunteerId = alice.Id, WantsSeated = true }
        });
        Assert.NotEmpty(draft.Proposals);

        var result = await _applier.ApplyAsync(draft);
        Assert.True(result.Success);

        var roster = _rosterService.GetRoster(1);
        Assert.Equal(draft.Proposals.Count, roster.TotalAssigned);
    }

    // ---------- Bluebell 5 (US34) ----------

    [Fact]
    public async Task Bluebell_WantsRaceHq_PlacedInRaceHqCategory()
    {
        var alice = await CreateVolunteer("Alice", "Female");
        var bluebell = await CreateBluebellEvent(new DateTime(2026, 5, 17));

        var draft = _allocator.Propose(bluebell, new[] {
            new AllocationCandidate { VolunteerId = alice.Id, WantsRaceHq = true }
        });

        var prop = Assert.Single(draft.Proposals);
        Assert.Equal(RoleCategory.RaceHq, prop.Category);
        Assert.Equal(AllocationReason.Preference, prop.Reason);
    }

    [Fact]
    public async Task Bluebell_WantsStartFinish_PlacedInFinishArea()
    {
        // Bluebell reuses WantsNearFinish for the Start/Finish preference.
        var alice = await CreateVolunteer("Alice", "Female");
        var bluebell = await CreateBluebellEvent(new DateTime(2026, 5, 17));

        var draft = _allocator.Propose(bluebell, new[] {
            new AllocationCandidate { VolunteerId = alice.Id, WantsNearFinish = true }
        });

        var prop = Assert.Single(draft.Proposals);
        Assert.Equal(RoleCategory.FinishArea, prop.Category);
    }

    [Fact]
    public async Task Bluebell_RunAfter_OnlyHonouredInRaceHqRoles()
    {
        // Only Number Pick Up, On The Day Registration, Car Park Marshal carry RunAfterCapacity at Bluebell.
        // A candidate who wants to run after should land in one of those, not Timekeeping/Water Table/etc.
        var alice = await CreateVolunteer("Alice", "Female");
        var bluebell = await CreateBluebellEvent(new DateTime(2026, 5, 17));

        var draft = _allocator.Propose(bluebell, new[] {
            new AllocationCandidate { VolunteerId = alice.Id, WantsToRunAfter = true }
        });

        var prop = Assert.Single(draft.Proposals);
        Assert.True(prop.WillRunAfter, "Expected run-after to be honoured.");
        Assert.Contains(prop.RoleName, new[] { "Number Pick Up", "On The Day Registration", "Car Park Marshal" });
    }

    [Fact]
    public async Task Bluebell_RunAfterRotation_PoolsAcrossEventTypes()
    {
        // Alice ran-after at C2C event 1; Bob did not. At Bluebell, Bob should get the run-after slot.
        var alice = await CreateVolunteer("Alice", "Female");
        var bob = await CreateVolunteer("Bob", "Male");
        var c2cNumberCollection = await GetRole("Number Collection");
        await SeedHistoryRunAfter(1, alice.Id, c2cNumberCollection.Id);

        var bluebell = await CreateBluebellEvent(new DateTime(2026, 5, 17));

        var draft = _allocator.Propose(bluebell, new[] {
            new AllocationCandidate { VolunteerId = alice.Id, WantsToRunAfter = true },
            new AllocationCandidate { VolunteerId = bob.Id, WantsToRunAfter = true }
        });

        var firstRunAfter = draft.Proposals.First(p => p.WillRunAfter);
        Assert.Equal(bob.Id, firstRunAfter.VolunteerId);
    }

    [Fact]
    public async Task Bluebell_RoleMixUp_MatchesByName_AcrossEventTypes()
    {
        // Alice did "Timekeeping" at C2C event 1; both timekeeping roles share the name. At Bluebell
        // the allocator should prefer Bob for Timekeeping and steer Alice elsewhere.
        var alice = await CreateVolunteer("Alice", "Female");
        var bob = await CreateVolunteer("Bob", "Male");
        var c2cTimekeeping = await GetRole("Timekeeping");
        await SeedHistory(1, alice.Id, c2cTimekeeping.Id);

        var bluebell = await CreateBluebellEvent(new DateTime(2026, 5, 17));
        var bluebellTimekeeping = await GetRole("Timekeeping", EventType.Bluebell5);

        var draft = _allocator.Propose(bluebell, new[] {
            new AllocationCandidate { VolunteerId = alice.Id },
            new AllocationCandidate { VolunteerId = bob.Id }
        });

        var tkProps = draft.Proposals.Where(p => p.VolunteerRoleId == bluebellTimekeeping.Id).ToList();
        if (tkProps.Count == 1)
        {
            Assert.NotEqual(alice.Id, tkProps[0].VolunteerId);
        }
    }

    [Fact]
    public async Task Bluebell_ApplyDraft_PersistsWantsRaceHq()
    {
        var alice = await CreateVolunteer("Alice", "Female");
        var bluebell = await CreateBluebellEvent(new DateTime(2026, 5, 17));

        var draft = _allocator.Propose(bluebell, new[] {
            new AllocationCandidate { VolunteerId = alice.Id, WantsRaceHq = true }
        });

        var result = await _applier.ApplyAsync(draft);
        Assert.True(result.Success);

        await using var db = _factory.CreateDbContext();
        var persisted = db.VolunteerAssignments.Single(a => a.EventId == bluebell && a.VolunteerId == alice.Id);
        Assert.True(persisted.WantsRaceHq);
    }

    // ---------- Helpers ----------

    private async Task<Volunteer> CreateVolunteer(string name, string gender, bool firstAid = false, bool member = true)
    {
        await _registry.CreateAsync(new VolunteerInput
        {
            Name = name, Gender = gender, IsClubMember = member, IsFirstAidTrained = firstAid
        });
        await using var db = _factory.CreateDbContext();
        return db.Volunteers.Single(v => v.Name == name);
    }

    private async Task<VolunteerRole> GetRole(string name, EventType eventType = EventType.CrownToCrown)
    {
        await using var db = _factory.CreateDbContext();
        return db.VolunteerRoles.Single(r => r.Name == name && r.EventType == eventType);
    }

    private async Task<int> CreateBluebellEvent(DateTime date)
    {
        await using var db = _factory.CreateDbContext();
        var ev = new RaceEvent
        {
            EventName = $"Bluebell 5 - {date:MMM yyyy}",
            EventDate = date,
            EventType = EventType.Bluebell5
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return ev.Id;
    }

    private async Task PrePlaceAsync(string roleName, int volunteerId)
    {
        await using var db = _factory.CreateDbContext();
        var role = db.VolunteerRoles.Single(r => r.Name == roleName && r.EventType == EventType.CrownToCrown);
        role.PrePlacedVolunteerId = volunteerId;
        await db.SaveChangesAsync();
    }

    private async Task AllowListAsync(int roleId, int volunteerId)
    {
        await using var db = _factory.CreateDbContext();
        db.VolunteerRoleEligibilities.Add(new VolunteerRoleEligibility { VolunteerRoleId = roleId, VolunteerId = volunteerId });
        await db.SaveChangesAsync();
    }

    private async Task SeedHistory(int eventId, int volunteerId, int roleId)
    {
        await using var db = _factory.CreateDbContext();
        db.VolunteerAssignments.Add(new VolunteerAssignment
        {
            EventId = eventId, VolunteerId = volunteerId, VolunteerRoleId = roleId
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedHistoryRunAfter(int eventId, int volunteerId, int roleId)
    {
        await using var db = _factory.CreateDbContext();
        db.VolunteerAssignments.Add(new VolunteerAssignment
        {
            EventId = eventId, VolunteerId = volunteerId, VolunteerRoleId = roleId, WillRunAfter = true
        });
        await db.SaveChangesAsync();
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
