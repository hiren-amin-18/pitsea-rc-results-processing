# US19 - Database Backup and Restore

**Status:** 📋 Planned

## As a
Race organiser

## I want
To download a backup of all race data and restore the application from a backup file

## So that
Results are never lost to machine failure, file corruption, or an accidental destructive action

---

## Background

All data lives in a single SQLite file (`raceresults.db`) in the application working directory. There is currently no backup mechanism, and several operations are destructive with no undo: deleting an event removes its data permanently, and re-uploading entrants wipes the current event's finish and timing data. Running the database inside a cloud-synced folder (e.g. OneDrive) adds a real corruption risk from file-lock conflicts.

## Acceptance Criteria

1. The Settings page offers "Download backup", producing a single file containing all data (a consistent snapshot of the SQLite database, e.g. via the SQLite backup API — not a copy of a potentially mid-write file).
2. Backup filenames include a timestamp, e.g. `raceresults-backup-2026-06-12-1430.db`.
3. The Settings page offers "Restore from backup" with a file upload:
   - The uploaded file is validated as a usable database with the expected schema before anything is replaced.
   - Restore requires explicit confirmation and states clearly that current data will be replaced.
   - The pre-restore database is kept aside automatically so a bad restore can be undone.
4. Destructive operations (event delete, entrant re-upload over existing data) prompt the organiser with a reminder/short-cut to take a backup first.
5. Pending EF migrations are applied to a restored database on next startup, so older backups restore safely into newer app versions.
6. Backup/restore actions are logged.

## Notes

- Documentation should recommend storing the live database outside cloud-synced folders, with backups (not the live file) synced to OneDrive/Drive.
- Out of scope: scheduled/automatic backups — a sensible follow-up once manual backup exists.
