# US15 — Runner Registry — Code Generation Plan

**Story:** [US15](../../../user-stories/US15-runner-registry.md)
**Type:** Brownfield + schema change. Structural prerequisite for US24.

## Design

- **`Runner`** entity: `Name`, `Club`, `Gender`, `Age` (nullable — consistent with the club's "ages only for U18" convention), `ExternalReference` (EA number), `IsActive`.
- **`Entrant.RunnerId`** (nullable FK) + nav. Bibs stay per-event. FK `OnDelete(Restrict)` — deleting an event's entrants never deletes runners (AC7).
- **`RunnerIdentity`** (static): shared `NormalizeKey(name, club)` + `Levenshtein` for near-match detection — single source of truth reused by upload matching, the registry service, and the startup backfill.
- **Champions keying (AC5):** `AggregateAudits` groups by `Entrant.RunnerId` instead of name+club; display entrant = most recent.
- **Lifetimes:** upload matching stays inside the singleton `RaceResultsService` (uses the static helper + its own DbContext). Registry management (list/edit/merge) is a new scoped `IRunnerRegistryService` that depends on the scoped `IChampionsOfChampionsService` for recalculation.

## Steps

- [ ] **Step 1 — Models.** `Runner`, `Entrant.RunnerId`/nav, `EditRunnerInput`, `RunnerListItem`.
- [ ] **Step 2 — DbContext + migration.** `DbSet<Runner>`, FK config; `dotnet ef migrations add AddRunnerRegistry`.
- [ ] **Step 3 — `RunnerIdentity` helper** + unit tests (normalize, Levenshtein).
- [ ] **Step 4 — Upload matching (AC3).** During entrant upload: exact normalized name+club → link; no match → create runner; near match (same name/different club or edit-distance ≤ 2) → warning for review.
- [ ] **Step 5 — Champions keying (AC5).** Group aggregation by `RunnerId`.
- [ ] **Step 6 — Registry service + UI (AC4, AC6).** `RunnerRegistryService`: list with race count, edit, merge (reassign entrants, deactivate/remove source) — both recalc affected seasons. `RunnersController` + `Index`/`Edit` views + nav link.
- [ ] **Step 7 — Event delete (AC7).** Keep runners; flag runners with no remaining entrants inactive.
- [ ] **Step 8 — Startup backfill (AC8).** One runner per distinct normalized name+club across all entrants; link existing rows. Skipped in Testing.
- [ ] **Step 9 — Tests + build + docs.** Registry/upload/merge/Champions-keying tests; README (conventions, features, structure, counts); US15 → ✅.

## Story traceability

| AC | Covered by |
|----|------------|
| 1 Runner entity | Step 1 |
| 2 Entrant→Runner FK | Steps 1, 2 |
| 3 upload matching | Steps 3, 4 |
| 4 management UI (list/edit/merge) | Step 6 |
| 5 Champions key on RunnerId | Step 5 |
| 6 edit/merge recalc | Step 6 |
| 7 event delete keeps runners | Step 7 |
| 8 data migration | Step 8 |
