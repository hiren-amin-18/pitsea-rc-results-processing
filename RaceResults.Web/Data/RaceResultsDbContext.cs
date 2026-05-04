using Microsoft.EntityFrameworkCore;
using RaceResults.Web.Models;

namespace RaceResults.Web.Data;

public class RaceResultsDbContext : DbContext
{
    public RaceResultsDbContext(DbContextOptions<RaceResultsDbContext> options) : base(options) { }

    public DbSet<RaceEvent> Events => Set<RaceEvent>();
    public DbSet<Entrant> Entrants => Set<Entrant>();
    public DbSet<FinishBibRecord> FinishBibRecords => Set<FinishBibRecord>();
    public DbSet<TimingRow> TimingRows => Set<TimingRow>();

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
                EventDate = new DateTime(2026, 4, 3),
                EventType = EventType.CrownToCrown,
                IsCurrent = true
            });
        });

        modelBuilder.Entity<Entrant>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EventId, x.BibNumber }).IsUnique();
            e.Property(x => x.BibNumber).IsRequired();
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.Gender).IsRequired();
            e.Property(x => x.EventId).HasDefaultValue(1);
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
    }
}
