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
    public DbSet<NotDuplicatePair> NotDuplicatePairs => Set<NotDuplicatePair>();

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
        });

        modelBuilder.Entity<VolunteerRole>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EventType, x.Name }).IsUnique();
            e.Property(x => x.Name).IsRequired();
            e.HasOne(x => x.PrePlacedVolunteer).WithMany().HasForeignKey(x => x.PrePlacedVolunteerId).OnDelete(DeleteBehavior.SetNull);
            e.HasData(SeedC2CRoles());
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
    }

    /// <summary>Seeds the 23 Crown to Crown volunteer roles (US28). Bluebell 5 is seeded by a later story.</summary>
    private static VolunteerRole[] SeedC2CRoles()
    {
        VolunteerRole R(int id, RoleCategory cat, string name, int def, int min, int max,
            bool optional = false, int runAfter = 0, bool firstAid = false, bool restricted = false) =>
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
                RunAfterCapacity = runAfter,
                RequiresFirstAid = firstAid,
                HasEligibilityRestriction = restricted,
                PrePlacedVolunteerId = null,
                SortOrder = id,
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
            R(23, RoleCategory.Course,     "First Aid On Course",    1, 1, 1, firstAid: true)
        };
    }
}
