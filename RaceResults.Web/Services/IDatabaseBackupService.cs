namespace RaceResults.Web.Services;

/// <summary>Backup and restore of the SQLite race database (US19).</summary>
public interface IDatabaseBackupService
{
    /// <summary>Absolute path of the live SQLite database file.</summary>
    string GetDatabasePath();

    /// <summary>Produce a consistent snapshot of the live database via the SQLite backup API (AC1).</summary>
    byte[] CreateBackup();

    /// <summary>Timestamped download filename, e.g. <c>raceresults-backup-2026-06-12-1430.db</c> (AC2).</summary>
    string GetBackupFileName(DateTime localNow);

    /// <summary>Restore the live database from an uploaded backup. Validates the file, keeps the
    /// pre-restore database aside, replaces the live file, then applies pending migrations (AC3, AC5).</summary>
    BackupRestoreResult Restore(Stream uploadedBackup);
}

/// <summary>Outcome of a restore attempt.</summary>
public class BackupRestoreResult
{
    public bool Success { get; private init; }
    public string Message { get; private init; } = string.Empty;

    /// <summary>Filename of the saved pre-restore database, when a restore succeeded.</summary>
    public string? PreRestoreFileName { get; private init; }

    public static BackupRestoreResult Failed(string message) => new() { Success = false, Message = message };

    public static BackupRestoreResult Restored(string preRestoreFileName) => new()
    {
        Success = true,
        PreRestoreFileName = preRestoreFileName,
        Message = $"Database restored. The previous database was saved as '{preRestoreFileName}' so this restore can be undone."
    };
}
