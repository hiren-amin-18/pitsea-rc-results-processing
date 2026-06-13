# US15 — Runner Registry — Implementation Summary

**Status:** ✅ Complete — build green, 133/133 tests passing (111 unit + 22 integration). Structural prerequisite for US24.

## Files changed

**Created**
- `Models/Runner.cs`, `Models/EditRunnerInput.cs`, `Models/RunnerListItem.cs`.
- `Services/RunnerIdentity.cs` — shared normalised-key + Levenshtein.
- `Services/IRunnerRegistryService.cs` + `RunnerRegistryService.cs` — list/edit/merge with affected-season recalculation.
- `Controllers/RunnersController.cs`; `Views/Runners/Index.cshtml`, `Views/Runners/Edit.cshtml`.
- `Migrations/…_AddRunnerRegistry.*` — `Runners` table + `Entrant.RunnerId` FK (`Restrict`).
- Tests: `RunnerRegistryTests.cs` (7), `RunnersControllerTests.cs` (1).

**Modified**
- `Models/Entrant.cs` — `RunnerId` + nav.
- `Data/RaceResultsDbContext.cs` — `Runners` set + FK config.
- `Services/RaceResultsService.cs` — `LinkEntrantsToRunnersAsync` at upload (exact-match link / create / near-match warning); `RefreshRunnerActiveFlags` on event delete.
- `Services/ChampionsOfChampionsService.cs` — `RunnerKey` now keys on `RunnerId` (AC5).
- `Program.cs` — registered `IRunnerRegistryService`; `BackfillRunners` startup migration of legacy entrants.
- `Views/Shared/_Layout.cshtml` — Runners nav link.
- `RaceResults.IntegrationTests/ResultsControllerTests.cs` was already updated for US17; no change needed here.
- `README.md`, `user-stories/US15-runner-registry.md`.

## Decisions

- **Identity model:** `Runner` carries `Age` (nullable) consistent with the club's "ages only for U18" convention rather than a full DOB, plus `ExternalReference` (EA number). AC1's "DOB or age band" satisfied via the existing age convention.
- **Lifetimes:** upload matching stays in the singleton `RaceResultsService` (static `RunnerIdentity` + its own DbContext); management (which must recalc Champions) is a scoped `RunnerRegistryService` depending on the scoped `IChampionsOfChampionsService`.
- **Near-match (AC3):** compared only against runners that existed *before* the current upload (so a batch of brand-new runners doesn't warn against itself), via same-normalised-name or Levenshtein ≤ 2 (name length ≥ 4). Assistive warning only — never auto-merges.
- **Edit propagation:** editing a runner updates their entrant snapshots too, so category-based scoring and display stay consistent; both edit and merge recalc every season the runner has entrants in (AC6).
- **Event delete (AC7):** FK `Restrict` means deleting an event's entrants never deletes runners; runners left with no entrants are flagged `IsActive = false`.
- **Migration (AC8):** startup `BackfillRunners` creates one runner per distinct normalised name+club and links existing entrants; skipped in Testing.

## Acceptance criteria — all met (1–8).
