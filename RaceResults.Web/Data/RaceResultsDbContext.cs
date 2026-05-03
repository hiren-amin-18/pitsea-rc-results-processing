using Microsoft.EntityFrameworkCore;
using RaceResults.Web.Models;

namespace RaceResults.Web.Data;

public class RaceResultsDbContext : DbContext
{
    public RaceResultsDbContext(DbContextOptions<RaceResultsDbContext> options) : base(options) { }

    public DbSet<Entrant> Entrants => Set<Entrant>();
    public DbSet<FinishBibRecord> FinishBibRecords => Set<FinishBibRecord>();
    public DbSet<TimingRow> TimingRows => Set<TimingRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Entrant>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.BibNumber).IsUnique();
            e.Property(x => x.BibNumber).IsRequired();
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.Gender).IsRequired();
        });

        modelBuilder.Entity<FinishBibRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Position).IsUnique();
            e.HasIndex(x => x.BibNumber).IsUnique();
            e.Property(x => x.BibNumber).IsRequired();
        });

        modelBuilder.Entity<TimingRow>(e =>
        {
            e.HasKey(x => x.Position);
            e.Property(x => x.Time).IsRequired();
        });
    }
}
