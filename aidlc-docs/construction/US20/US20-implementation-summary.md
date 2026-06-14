# US20 — Archive Completed Events — Implementation Summary

**Status:** ✅ Complete — build green, 158/158 tests passing (136 unit + 22 integration).

## Files changed

**Created**
- `Migrations/…_AddEventArchiving.*` — `RaceEvent.IsArchived`.
- Tests: `EventArchivingTests.cs` (5).

**Modified**
- `Models/RaceEvent.cs` — `IsArchived`.
- `Models/ResultsPageViewModel.cs` — `IsReadOnly`, `ViewedEventId`, `ViewedEventName`, `IsArchived`.
- `Services/IRaceResultsService.cs` + `RaceResultsService.cs` — `ArchiveEvent`/`UnarchiveEvent`; archived guards on uploads, `UpdateResult`, DSQ/Reinstate/SetStatus, `UpdateEvent`, `SetCurrentEvent`, `DeleteEvent`; `EnsureCurrentEvent` prefers non-archived; **read-only `eventId` overloads** for DNF/DNS/DSQ/stats/summary/PDF/CSV/filename.
- `Controllers/EventsController.cs` — `Archive`/`Unarchive` actions.
- `Controllers/RaceController.cs` — `Results`/`Stats`/`Top10`/`ExportPdf`/`ExportCsv` accept optional `eventId`.
- `Views/Events/Index.cshtml` — archived badge, Archive/Unarchive (confirm), Set-Current hidden for archived, Delete guarded, "View" link.
- `Views/Race/Results.cshtml` — read-only banner; Edit/DSQ/Reinstate/DNS/DNF actions hidden when viewing a non-current event; exports carry the viewed event id.
- `README.md`, `user-stories/US20-archive-completed-events.md`.

## Decisions

- **Read-only viewing (AC3):** since an archived event can't be current and the results pages are current-scoped, added optional `eventId` viewing across Results/Stats/Top 10/PDF/CSV. `IsReadOnly` = the viewed event isn't the current one, which naturally covers archived events and hides all mutating UI.
- **Current-event invariant (AC5):** archiving the current event promotes the most recent non-archived event (or creates a fresh default if none remain); `EnsureCurrentEvent` never promotes an archived event. The upload guard on an archived *current* event is therefore defensive (unreachable through normal flow).
- **Champions untouched (AC4):** archiving only flips a flag — no audit writes or recalculation; archived events keep contributing because scoring reads their results unchanged.
- **Delete guard (AC8):** archived events must be unarchived before deletion.

## Acceptance criteria — all met (1–8).
