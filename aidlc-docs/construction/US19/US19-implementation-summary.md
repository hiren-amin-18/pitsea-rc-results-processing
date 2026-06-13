# US19 — Database Backup and Restore — Implementation Summary

**Status:** ✅ Complete — build green, 103/103 tests passing (82 unit + 21 integration).

## Files changed

**Created**
- `RaceResults.Web/Services/IDatabaseBackupService.cs` — interface + `BackupRestoreResult`.
- `RaceResults.Web/Services/DatabaseBackupService.cs` — backup snapshot, validation, restore (pre-restore copy + migrate).
- `RaceResults.UnitTests/DatabaseBackupServiceTests.cs` — 4 tests against a real temp-file SQLite DB.

**Modified**
- `RaceResults.Web/Program.cs` — registered `IDatabaseBackupService` (scoped).
- `RaceResults.Web/Controllers/HomeController.cs` — `DownloadBackup` (GET) + `RestoreBackup` (POST, multipart + confirm).
- `RaceResults.Web/Views/Home/Settings.cshtml` — "Data backup" card (download, validated restore with explicit confirmation + warning, storage guidance).
- `RaceResults.Web/Views/Events/Index.cshtml` — backup reminder + stronger delete confirm (AC4).
- `RaceResults.Web/Views/Race/Uploads.cshtml` — entrant re-upload reminder shown when data exists (AC4).
- `README.md` — features, data-persistence backup section, DI note, story tables, test counts.
- `user-stories/US19-database-backup-restore.md` — Status → ✅ Complete.

## Decisions

- **Consistent snapshot (AC1):** `SqliteConnection.BackupDatabase` (SQLite online backup API) into a temp file → transactionally consistent even mid-write; never copies a half-written file.
- **Restore safety (AC3):** upload is written to temp and **schema-validated** (expected tables present, opens as SQLite) before any change; `ClearAllPools` then live DB copied aside to `raceresults-prerestore-{timestamp}.db`; live file overwritten; pools cleared again.
- **Older backups (AC5):** restore calls `Database.Migrate()` immediately (and startup already migrates), so a backup from an older schema upgrades cleanly. Tests use `Migrate()` (not `EnsureCreated`) so the post-restore migrate is a no-op, matching production.
- **AC4 reminders:** lightweight, non-blocking UI prompts on event delete and entrant re-upload, linking to Settings.
- **Testing approach:** unit tests use a file-backed SQLite DB + in-memory `IConfiguration`; verify SQLite header on backup, data swap + pre-restore copy on restore, and that an invalid upload is rejected without touching live data.

## Acceptance criteria — all met (1–6).

## Notes / follow-ups
- Restore replaces the live file under running connections (mitigated by `ClearAllPools`); for a busy multi-user deployment a restart-based restore would be safer. Fine for the single-organiser local model. Scheduled backups remain out of scope per the story.
