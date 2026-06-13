# US19 — Database Backup and Restore — Code Generation Plan

**Story:** [US19](../../../user-stories/US19-database-backup-restore.md)
**Type:** Brownfield. New service + Settings UI + destructive-action reminders.

## Design

- New `IDatabaseBackupService` (scoped), injected `IConfiguration` (to resolve the SQLite `Data Source`), `IDbContextFactory<RaceResultsDbContext>` (post-restore migrate + schema validation), `ILogger`.
- **Backup (AC1, AC2):** use the SQLite **online backup API** (`SqliteConnection.BackupDatabase`) into a temp file → consistent snapshot even mid-write; return bytes. Filename `raceresults-backup-yyyy-MM-dd-HHmm.db`.
- **Restore (AC3):** persist upload to temp; **validate** it opens as SQLite and has the expected tables before touching the live DB; copy the live DB aside to `raceresults-prerestore-{timestamp}.db`; `ClearAllPools` + overwrite live file; then `Database.Migrate()` so older backups upgrade (AC5).
- **AC5:** startup already runs `db.Database.Migrate()`; restore also migrates immediately.
- **AC6:** log backup + restore (with pre-restore path).

## Steps

- [ ] **Step 1 — Business logic.** Add `RaceResults.Web/Services/IDatabaseBackupService.cs` + `DatabaseBackupService.cs` (resolve path, `CreateBackup`, `GetBackupFileName`, `Validate`, `Restore`).
- [ ] **Step 2 — DI.** Register the service in `Program.cs`.
- [ ] **Step 3 — API layer.** `HomeController.DownloadBackup` (GET) and `HomeController.RestoreBackup` (POST, multipart + confirm checkbox), with `TempData` feedback.
- [ ] **Step 4 — Frontend.** `Views/Home/Settings.cshtml`: "Data backup" card — Download button, Restore form (file + explicit confirmation + warning), feedback alert, and storage-location guidance.
- [ ] **Step 5 — Destructive-action reminders (AC4).** Events delete confirm text + tip linking to Settings backup; Uploads entrants section reminder shown when data already exists.
- [ ] **Step 6 — Unit tests.** `DatabaseBackupServiceTests` against a real temp-file SQLite DB: backup produces a valid SQLite file; validation accepts good / rejects bad; restore swaps data and leaves a pre-restore file; filename format.
- [ ] **Step 7 — Build & test + docs.** Full suite green; README features/data-persistence/settings + storage guidance; mark US19 ✅.

## Story traceability

| AC | Covered by |
|----|------------|
| 1 consistent snapshot download | Steps 1, 3, 4 |
| 2 timestamped filename | Step 1 |
| 3 validated restore w/ confirm + pre-restore aside | Steps 1, 3, 4 |
| 4 destructive-op backup reminder | Step 5 |
| 5 migrations on restored DB | Step 1 (+ existing startup migrate) |
| 6 logging | Step 1 |
