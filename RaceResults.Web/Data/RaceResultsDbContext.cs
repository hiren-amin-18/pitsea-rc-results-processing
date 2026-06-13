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
    }
}
