using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RaceResults.Web.Data;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.UnitTests;

public class DatabaseBackupServiceTests
{
    [Fact]
    public void GetBackupFileName_IncludesTimestamp()
    {
        using var fixture = new FileBackupFixture();
        var service = fixture.CreateService();

        var name = service.GetBackupFileName(new DateTime(2026, 6, 12, 14, 30, 0));

        Assert.Equal("raceresults-backup-2026-06-12-1430.db", name);
        Assert.Matches(@"^raceresults-backup-\d{4}-\d{2}-\d{2}-\d{4}\.db$", name);
    }

    [Fact]
    public void CreateBackup_ProducesValidSqliteFile()
    {
        using var fixture = new FileBackupFixture();
        var service = fixture.CreateService();

        var bytes = service.CreateBackup();

        // SQLite files begin with the literal header "SQLite format 3\0".
        var header = Encoding.ASCII.GetString(bytes, 0, 16);
        Assert.Equal("SQLite format 3\0", header);
    }

    [Fact]
    public void Restore_WithValidBackup_SwapsDataAndKeepsPreRestoreCopy()
    {
        using var fixture = new FileBackupFixture();
        var service = fixture.CreateService();

        // Snapshot the seeded state, then change the live DB.
        var backup = service.CreateBackup();
        using (var db = fixture.Factory.CreateDbContext())
        {
            db.Events.Add(new RaceEvent { EventName = "Added After Backup", EventDate = new DateTime(2026, 7, 1), EventType = EventType.Bluebell5 });
            db.SaveChanges();
        }

        var result = service.Restore(new MemoryStream(backup));

        Assert.True(result.Success);
        Assert.NotNull(result.PreRestoreFileName);
        Assert.Contains(Directory.GetFiles(fixture.Dir), f => Path.GetFileName(f).StartsWith("raceresults-prerestore-"));

        using (var db = fixture.Factory.CreateDbContext())
        {
            Assert.DoesNotContain(db.Events, e => e.EventName == "Added After Backup");
        }
    }

    [Fact]
    public void Restore_WithInvalidFile_FailsAndLeavesDataIntact()
    {
        using var fixture = new FileBackupFixture();
        var service = fixture.CreateService();

        int eventsBefore;
        using (var db = fixture.Factory.CreateDbContext())
        {
            eventsBefore = db.Events.Count();
        }

        var garbage = new MemoryStream(Encoding.ASCII.GetBytes("this is definitely not a sqlite database"));
        var result = service.Restore(garbage);

        Assert.False(result.Success);
        Assert.DoesNotContain(Directory.GetFiles(fixture.Dir), f => Path.GetFileName(f).StartsWith("raceresults-prerestore-"));

        using (var db = fixture.Factory.CreateDbContext())
        {
            Assert.Equal(eventsBefore, db.Events.Count());
        }
    }

    /// <summary>A real file-backed SQLite database (migrations applied, matching production) for backup/restore tests.</summary>
    private sealed class FileBackupFixture : IDisposable
    {
        public string Dir { get; }
        public string DbPath { get; }
        public IConfiguration Configuration { get; }
        public IDbContextFactory<RaceResultsDbContext> Factory { get; }
        private readonly ServiceProvider _provider;

        public FileBackupFixture()
        {
            Dir = Path.Combine(Path.GetTempPath(), "rr-backup-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Dir);
            DbPath = Path.Combine(Dir, "raceresults.db");

            Configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = $"Data Source={DbPath}"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddDbContextFactory<RaceResultsDbContext>(options => options.UseSqlite($"Data Source={DbPath}"));
            _provider = services.BuildServiceProvider();
            Factory = _provider.GetRequiredService<IDbContextFactory<RaceResultsDbContext>>();

            // Use Migrate (not EnsureCreated) so the post-restore migrate is a no-op, matching production.
            using var db = Factory.CreateDbContext();
            db.Database.Migrate();
        }

        public DatabaseBackupService CreateService() =>
            new(Configuration, Factory, NullLogger<DatabaseBackupService>.Instance);

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            _provider.Dispose();
            try
            {
                Directory.Delete(Dir, recursive: true);
            }
            catch (IOException)
            {
                // Temp dir cleanup is best-effort.
            }
        }
    }
}
