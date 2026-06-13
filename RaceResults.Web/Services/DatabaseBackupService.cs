using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaceResults.Web.Data;

namespace RaceResults.Web.Services;

public class DatabaseBackupService : IDatabaseBackupService
{
    // Tables every valid race-results database must contain.
    private static readonly string[] RequiredTables =
        ["Events", "Entrants", "FinishBibRecords", "TimingRows"];

    private readonly IConfiguration _configuration;
    private readonly IDbContextFactory<RaceResultsDbContext> _dbContextFactory;
    private readonly ILogger<DatabaseBackupService> _logger;

    public DatabaseBackupService(
        IConfiguration configuration,
        IDbContextFactory<RaceResultsDbContext> dbContextFactory,
        ILogger<DatabaseBackupService> logger)
    {
        _configuration = configuration;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public string GetDatabasePath()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=raceresults.db";
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        return Path.GetFullPath(dataSource);
    }

    public string GetBackupFileName(DateTime localNow) =>
        $"raceresults-backup-{localNow:yyyy-MM-dd-HHmm}.db";

    public byte[] CreateBackup()
    {
        var dbPath = GetDatabasePath();
        var tempPath = Path.Combine(Path.GetTempPath(), $"raceresults-backup-{Guid.NewGuid():N}.db");

        try
        {
            // Online backup API copies a transactionally consistent snapshot, even mid-write (AC1).
            using (var source = new SqliteConnection($"Data Source={dbPath}"))
            using (var destination = new SqliteConnection($"Data Source={tempPath}"))
            {
                source.Open();
                destination.Open();
                source.BackupDatabase(destination);
            }

            // Release the handle on the temp file before reading it.
            SqliteConnection.ClearAllPools();

            var bytes = File.ReadAllBytes(tempPath);
            _logger.LogInformation("Created database backup snapshot ({Bytes} bytes) from {DbPath}", bytes.Length, dbPath);
            return bytes;
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    public BackupRestoreResult Restore(Stream uploadedBackup)
    {
        var dbPath = GetDatabasePath();
        var tempUploadPath = Path.Combine(Path.GetTempPath(), $"raceresults-upload-{Guid.NewGuid():N}.db");

        try
        {
            using (var fileStream = File.Create(tempUploadPath))
            {
                uploadedBackup.CopyTo(fileStream);
            }

            if (!IsValidDatabase(tempUploadPath, out var validationError))
            {
                _logger.LogWarning("Rejected database restore: {Error}", validationError);
                return BackupRestoreResult.Failed(validationError);
            }

            // Drop pooled connections so the live file is no longer held open.
            SqliteConnection.ClearAllPools();

            // Keep the current database aside so a bad restore can be undone (AC3).
            var directory = Path.GetDirectoryName(dbPath) ?? Directory.GetCurrentDirectory();
            var preRestoreName = $"raceresults-prerestore-{DateTime.Now:yyyy-MM-dd-HHmmss}.db";
            var preRestorePath = Path.Combine(directory, preRestoreName);
            if (File.Exists(dbPath))
            {
                File.Copy(dbPath, preRestorePath, overwrite: true);
            }

            File.Copy(tempUploadPath, dbPath, overwrite: true);
            SqliteConnection.ClearAllPools();

            // Bring an older backup up to the current schema (AC5).
            using (var db = _dbContextFactory.CreateDbContext())
            {
                db.Database.Migrate();
            }

            _logger.LogInformation(
                "Restored database from uploaded backup. Previous database saved to {PreRestorePath}", preRestorePath);
            return BackupRestoreResult.Restored(preRestoreName);
        }
        finally
        {
            TryDelete(tempUploadPath);
        }
    }

    private static bool IsValidDatabase(string filePath, out string error)
    {
        error = string.Empty;
        try
        {
            using var connection = new SqliteConnection($"Data Source={filePath};Mode=ReadOnly");
            connection.Open();

            foreach (var table in RequiredTables)
            {
                using var command = connection.CreateCommand();
                command.CommandText =
                    "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
                command.Parameters.AddWithValue("$name", table);
                var found = Convert.ToInt64(command.ExecuteScalar());
                if (found == 0)
                {
                    error = $"The uploaded file is not a valid race-results backup (missing the '{table}' table).";
                    return false;
                }
            }

            return true;
        }
        catch (SqliteException)
        {
            error = "The uploaded file is not a valid SQLite database.";
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup of a temp file; nothing actionable if it fails.
        }
    }
}
