# US20 — Archive Completed Events — Code Generation Plan

**Story:** [US20](../../../user-stories/US20-archive-completed-events.md)
**Type:** Brownfield + schema change.

## Design

- `RaceEvent.IsArchived`. Archived events are read-only and can never be current.
- Mutations on an archived event are rejected with: *"This event is archived. Unarchive it to make changes."*
- Because the Results/Stats/Top 10/exports are current-event scoped and an archived event can't be current, AC3 viewing is satisfied by adding optional `eventId` (read-only) viewing to those pages and exports.

## Steps

- [ ] **Step 1 — Schema.** `RaceEvent.IsArchived`; migration `AddEventArchiving`.
- [ ] **Step 2 — Service guards + archive ops.** `ArchiveEvent`/`UnarchiveEvent`; archiving the current event promotes another non-archived event (or creates one); `EnsureCurrentEvent` prefers non-archived. Reject mutations when the current/target event is archived: entrant/finish/timing uploads, `UpdateResult`, DSQ/Reinstate/SetStatus, `UpdateEvent`. `SetCurrentEvent` rejects archived; `DeleteEvent` rejects archived (AC8).
- [ ] **Step 3 — Read-only viewing (AC3).** Add `eventId` overloads for `GetDnfEntrants`/`GetDnsEntrants`/`GetDsqResults`/`GetRaceStats`/`GetRaceStatisticsSummary`/`GenerateResultsPdf`/`GenerateResultsCsv`/`GetResultsCsvFileName`; controllers accept optional `eventId` (default = current).
- [ ] **Step 4 — Views.** Events page: Archive/Unarchive (confirm), archived badge, Set-Current disabled, Delete guarded, "View" link; Results/Stats show a read-only banner when viewing an archived event and hide Edit/DSQ; archived badge in current-context (Home).
- [ ] **Step 5 — Tests.** Archive blocks uploads/edits/detail-edits/delete/set-current; archived event still scores Champions (no recalc); unarchive restores; archiving current promotes another; read-only viewing works.
- [ ] **Step 6 — Build + docs.** Full suite green; README; US20 → ✅.

## Story traceability

| AC | Covered by |
|----|------------|
| 1 archive w/ confirm | Steps 2, 4 |
| 2 reject mutations w/ message | Step 2 |
| 3 still viewable | Steps 3, 4 |
| 4 still scores, no recalc | Step 2 (archiving doesn't touch Champions) |
| 5 cannot be current | Step 2 |
| 6 unarchive w/ confirm | Steps 2, 4 |
| 7 badge visible | Step 4 |
| 8 delete needs unarchive | Step 2 |
