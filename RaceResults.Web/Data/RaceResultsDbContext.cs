using Microsoft.EntityFrameworkCore;
using RaceResults.Web.Models;

namespace RaceResults.Web.Data;

public class RaceResultsDbContext : DbContext
{
    public RaceResultsDbContext(DbContextOptions<RaceResultsDbContext> options) : base(options) { }

    public DbSet<RaceEvent> Events => Set<RaceEvent>();
    public DbSet<Runner> Runners => Set<Runner>();
    public DbSet<Entrant> Entrants => Set<Entrant>();
    public DbSet<FinishBibRecord> FinishBibRecords => Set<FinishBibRecord>();
    public DbSet<TimingRow> TimingRows => Set<TimingRow>();
    public DbSet<ChampionOfChampionsScore> ChampionOfChampionsScores => Set<ChampionOfChampionsScore>();
    public DbSet<PointsAuditLog> PointsAuditLogs => Set<PointsAuditLog>();
    public DbSet<CourseRecord> CourseRecords => Set<CourseRecord>();
    public DbSet<Volunteer> Volunteers => Set<Volunteer>();
    public DbSet<VolunteerRole> VolunteerRoles => Set<VolunteerRole>();
    public DbSet<VolunteerRoleEligibility> VolunteerRoleEligibilities => Set<VolunteerRoleEligibility>();
    public DbSet<VolunteerAssignment> VolunteerAssignments => Set<VolunteerAssignment>();
    public DbSet<AllocationCandidateRecord> AllocationCandidateRecords => Set<AllocationCandidateRecord>();
    public DbSet<NotDuplicatePair> NotDuplicatePairs => Set<NotDuplicatePair>();
    public DbSet<Club> Clubs => Set<Club>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RaceEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.EventName).IsRequired();
            e.Property(x => x.EventDate).IsRequired();
            e.Property(x => x.EventType).IsRequired();
            e.HasData(new RaceEvent
            {
                Id = 1,
                EventName = "Crown to Crown",
                // Good Friday 2026 - the real first race of the C2C series. Deliberately outside
                // the Champions May-September scoring window; the series spans the full year.
                EventDate = new DateTime(2026, 4, 3),
                EventType = EventType.CrownToCrown,
                IsCurrent = true
            });
        });

        modelBuilder.Entity<Runner>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.Gender).IsRequired();
        });

        modelBuilder.Entity<Entrant>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EventId, x.BibNumber }).IsUnique();
            e.Property(x => x.BibNumber).IsRequired();
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.Gender).IsRequired();
            e.Property(x => x.EventId).HasDefaultValue(1);
            e.Property(x => x.Status).HasDefaultValue(FinishStatus.Finished);
            // Deleting an event (and its entrants) must never delete the persistent runner (US15 AC7).
            e.HasOne(x => x.Runner).WithMany().HasForeignKey(x => x.RunnerId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FinishBibRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EventId, x.Position }).IsUnique();
            e.HasIndex(x => new { x.EventId, x.BibNumber }).IsUnique();
            e.Property(x => x.BibNumber).IsRequired();
            e.Property(x => x.EventId).HasDefaultValue(1);
        });

        modelBuilder.Entity<TimingRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EventId, x.Position }).IsUnique();
            e.Property(x => x.EventId).HasDefaultValue(1);
            e.Property(x => x.Time).IsRequired();
        });

        modelBuilder.Entity<ChampionOfChampionsScore>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SeasonYear, x.EntrantId, x.Category }).IsUnique();
            e.Property(x => x.SeasonYear).IsRequired();
            e.Property(x => x.Category).IsRequired();
            e.Property(x => x.TotalPoints).IsRequired();
            e.Property(x => x.RaceCount).IsRequired();
            e.Property(x => x.LastUpdated).IsRequired();
            e.HasOne(x => x.Entrant).WithMany().HasForeignKey(x => x.EntrantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CourseRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EventType, x.Category, x.IsCurrent });
            e.Property(x => x.Category).IsRequired();
            e.Property(x => x.RunnerName).IsRequired();

            // Existing Crown to Crown course records (US22 AC2). Bluebell 5 starts with none.
            e.HasData(
                new CourseRecord
                {
                    Id = 1,
                    EventType = EventType.CrownToCrown,
                    Category = "Male",
                    DurationTicks = 9_250_000_000, // 15:25
                    RunnerName = "Adam Hickey",
                    Club = string.Empty,
                    EventName = "Crown to Crown",
                    EventDate = new DateTime(2013, 8, 1),
                    SourceEventId = null,
                    IsCurrent = true,
                    CreatedAt = new DateTime(2024, 1, 1)
                },
                new CourseRecord
                {
                    Id = 2,
                    EventType = EventType.CrownToCrown,
                    Category = "Female",
                    DurationTicks = 10_810_000_000, // 18:01
                    RunnerName = "Jessica Judd",
                    Club = string.Empty,
                    EventName = "Crown to Crown",
                    EventDate = new DateTime(2015, 12, 1),
                    SourceEventId = null,
                    IsCurrent = true,
                    CreatedAt = new DateTime(2024, 1, 1)
                });
        });

        modelBuilder.Entity<PointsAuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SeasonYear, x.EntrantId, x.EventId }).IsUnique(false);
            e.Property(x => x.SeasonYear).IsRequired();
            e.Property(x => x.Category).IsRequired();
            e.Property(x => x.PointsAwarded).IsRequired();
            e.Property(x => x.Action).IsRequired();
            e.Property(x => x.AuditTimestamp).IsRequired();
            e.HasOne(x => x.Event).WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Entrant).WithMany().HasForeignKey(x => x.EntrantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Volunteer>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.Gender).IsRequired();
            // Deleting an event (and its assignments) must never delete the persistent volunteer (US28 AC10).
            e.HasOne(x => x.Runner).WithMany().HasForeignKey(x => x.RunnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.DefaultPreferredRole).WithMany().HasForeignKey(x => x.DefaultPreferredRoleId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AllocationCandidateRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EventId, x.VolunteerId }).IsUnique();
            // Grid memory is per-event scratch state: deleting the event or the volunteer clears it.
            e.HasOne(x => x.Event).WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Volunteer).WithMany().HasForeignKey(x => x.VolunteerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VolunteerRole>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EventType, x.Name }).IsUnique();
            e.Property(x => x.Name).IsRequired();
            e.HasOne(x => x.PrePlacedVolunteer).WithMany().HasForeignKey(x => x.PrePlacedVolunteerId).OnDelete(DeleteBehavior.SetNull);
            e.HasData(SeedC2CRoles());
            e.HasData(SeedBluebellRoles());
        });

        modelBuilder.Entity<VolunteerRoleEligibility>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.VolunteerRoleId, x.VolunteerId }).IsUnique();
            e.HasOne(x => x.VolunteerRole).WithMany().HasForeignKey(x => x.VolunteerRoleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Volunteer).WithMany().HasForeignKey(x => x.VolunteerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VolunteerAssignment>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EventId, x.VolunteerId });
            // Deleting the event cascades its assignments (US28 AC10). The volunteer & role are protected.
            e.HasOne(x => x.Event).WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Volunteer).WithMany().HasForeignKey(x => x.VolunteerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.VolunteerRole).WithMany().HasForeignKey(x => x.VolunteerRoleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PreferredRole).WithMany().HasForeignKey(x => x.PreferredRoleId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<NotDuplicatePair>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.RunnerAId, x.RunnerBId }).IsUnique();
            // Deleting a runner clears any pair-dismissals that reference it.
            e.HasOne<Runner>().WithMany().HasForeignKey(x => x.RunnerAId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Runner>().WithMany().HasForeignKey(x => x.RunnerBId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Club>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).IsRequired();
            e.HasData(SeedClubs());
        });
    }

    /// <summary>Seeds the canonical club list agreed with the organiser (US45). New clubs added via the
    /// admin page after this run persist alongside these seeded records; deactivating is preferred over
    /// deletion so historic entrants continue to match.</summary>
    private static Club[] SeedClubs()
    {
        var names = new[]
        {
            "Aberystwyth AC",
            "Bad Boy Running",
            "Barking Road Runners",
            "Barking Running Club",
            "Basildon Athletics Club",
            "Basildon CC",
            "Benfleet Running Club",
            "Billericay Striders",
            "Braintree & District Athletic Club",
            "Brentwood Beagles Athletics Club",
            "Brentwood Running Club",
            "Castle Point Joggers",
            "Castle Point Young Runners",
            "Chelmsford Athletics",
            "City of Southend On Sea AC",
            "Corringham Running Club",
            "Dagenham 88 Runners",
            "Daws Heath Harriers",
            "Dengie 100 Runners",
            "East Essex Triathlon Club",
            "East London Runners",
            "Fordy Runs Running Club",
            "Harold Wood Running Club",
            "Havering '90 Joggers",
            "Havering AC",
            "Havering Tri",
            "Hockley Trail Runners",
            "Hot Steppers",
            "Ilford AC",
            "JBR Run and Tri Club",
            "Kingswood Running Club",
            "Leigh-on-Sea Striders",
            "London Heathside",
            "Lonely Goat RC",
            "Maldon Soul Runners",
            "Mid Essex Casuals",
            "Nuclear Races Striders",
            "Pewsey Vale Running Club",
            "Phoenix Striders",
            "Pitsea Running Club",
            "Rayleigh Rat Runners",
            "RED Runners",
            "Rochford Running Club",
            "South Woodham Runners",
            "Springfield Striders RC",
            "SS Athletics",
            "St Edmund Pacers",
            "Thames Hare & Hounds",
            "Thurrock Harriers",
            "Thurrock Nomads",
            "Trail Running Association",
            "Vegan Runners UK",
            "Ware Joggers",
            "Witham Running Club",
            "Woman of Wickford",
        };
        var clubs = new Club[names.Length];
        for (var i = 0; i < names.Length; i++)
        {
            clubs[i] = new Club { Id = i + 1, Name = names[i], IsActive = true };
        }
        return clubs;
    }

    /// <summary>Seeds the 23 Crown to Crown volunteer roles (US28). Bluebell 5 is seeded by a later story.</summary>
    private static VolunteerRole[] SeedC2CRoles()
    {
        VolunteerRole R(int id, RoleCategory cat, string name, int def, int min, int max,
            bool optional = false, int runAfter = 0, bool firstAid = false, bool restricted = false,
            bool genericPreference = false, int sortOrder = 0) =>
            new()
            {
                Id = id,
                Name = name,
                Category = cat,
                EventType = EventType.CrownToCrown,
                DefaultCount = def,
                MinCount = min,
                MaxCount = max,
                IsOptional = optional,
                IsGenericPreference = genericPreference,
                RunAfterCapacity = runAfter,
                RequiresFirstAid = firstAid,
                HasEligibilityRestriction = restricted,
                PrePlacedVolunteerId = null,
                SortOrder = sortOrder == 0 ? id : sortOrder,
                IsActive = true
            };

        return new[]
        {
            R(1,  RoleCategory.Leadership, "Lead",                   1, 1, 1, restricted: true),
            R(2,  RoleCategory.Leadership, "Shadow Lead",            1, 0, 1, optional: true),
            R(3,  RoleCategory.Leadership, "Results",                1, 1, 1, restricted: true),
            R(4,  RoleCategory.FinishArea, "Timekeeping",            2, 2, 2),
            R(5,  RoleCategory.FinishArea, "Course Setup",           2, 2, 2),
            R(6,  RoleCategory.FinishArea, "Number Collection",      2, 1, 2, runAfter: 1),
            R(7,  RoleCategory.FinishArea, "On The Day Registration",4, 4, 4, runAfter: 2),
            R(8,  RoleCategory.FinishArea, "Finish Line Funnel",     1, 1, 1),
            R(9,  RoleCategory.FinishArea, "Finish Line Results",    2, 2, 2),
            R(10, RoleCategory.FinishArea, "First Aid and Prizes",   1, 1, 1, firstAid: true),
            R(11, RoleCategory.FinishArea, "Tail Runners",           2, 2, 2),
            R(12, RoleCategory.FinishArea, "Photographer",           1, 0, 1, optional: true),
            R(13, RoleCategory.FinishArea, "Water Table",            2, 2, 2),
            R(14, RoleCategory.Course,     "Marshal Point 1",        2, 2, 2),
            R(15, RoleCategory.Course,     "Marshal Point 2",        2, 2, 2),
            R(16, RoleCategory.Course,     "Marshal Point 3",        2, 2, 2),
            R(17, RoleCategory.Course,     "Marshal Point 4",        3, 3, 3),
            R(18, RoleCategory.Course,     "Marshal Point 5",        2, 2, 2),
            R(19, RoleCategory.Course,     "Marshal Point 5a",       2, 2, 2),
            R(20, RoleCategory.Course,     "Marshal Point 6",        2, 2, 2),
            R(21, RoleCategory.Course,     "Marshal Point 7",        2, 2, 2),
            R(22, RoleCategory.Course,     "Metal Gate",             1, 0, 1, optional: true),
            R(23, RoleCategory.Course,     "First Aid On Course",    1, 1, 1, firstAid: true),
            // Generic preference sentinel: appears in the preferred-role dropdown but has no physical slots.
            // The allocator places volunteers who select this into any open marshal point.
            R(39, RoleCategory.Course,     "Marshal (any point)",    0, 0, 0,
                genericPreference: true, sortOrder: 13),
        };
    }

    /// <summary>Seeds the 15 Bluebell 5 volunteer roles (US34). Lead and Results are restricted with an empty
    /// allow-list, matching the C2C approach — the organiser populates the eligible names via the roles UI.</summary>
    private static VolunteerRole[] SeedBluebellRoles()
    {
        VolunteerRole R(int id, RoleCategory cat, string name, int def, int min, int max,
            bool optional = false, int runAfter = 0, bool restricted = false) =>
            new()
            {
                Id = id,
                Name = name,
                Category = cat,
                EventType = EventType.Bluebell5,
                DefaultCount = def,
                MinCount = min,
                MaxCount = max,
                IsOptional = optional,
                IsGenericPreference = false,
                RunAfterCapacity = runAfter,
                RequiresFirstAid = false,
                HasEligibilityRestriction = restricted,
                PrePlacedVolunteerId = null,
                SortOrder = id,
                IsActive = true
            };

        return new[]
        {
            R(24, RoleCategory.RaceHq,     "Number Pick Up",          6, 4, 6, runAfter: 3),
            R(25, RoleCategory.RaceHq,     "On The Day Registration", 2, 1, 2, runAfter: 1),
            R(26, RoleCategory.RaceHq,     "Refreshments",            3, 2, 3),
            R(27, RoleCategory.RaceHq,     "Bag Drop",                1, 0, 1, optional: true),
            R(28, RoleCategory.RaceHq,     "Car Park Marshal",        4, 3, 4, runAfter: 2),
            R(29, RoleCategory.Leadership, "Lead",                    1, 1, 1, restricted: true),
            R(30, RoleCategory.Leadership, "Results",                 1, 1, 1, restricted: true),
            R(31, RoleCategory.FinishArea, "Timekeeping",             2, 2, 2),
            R(32, RoleCategory.FinishArea, "Finish Line Funnel",      2, 1, 2),
            R(33, RoleCategory.FinishArea, "Finish Line Results",     2, 2, 2),
            R(34, RoleCategory.FinishArea, "Tail Walker",             2, 2, 2),
            R(35, RoleCategory.FinishArea, "Water Table",             4, 4, 4),
            R(36, RoleCategory.FinishArea, "Photographer",            1, 0, 1, optional: true),
            R(37, RoleCategory.FinishArea, "Finish Help",             1, 1, 1),
            R(38, RoleCategory.Transport,  "Van Driver",              1, 1, 1)
        };
    }
}
