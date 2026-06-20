# US33 — Bluebell 5 Results Processing — Implementation Summary

**Story:** [US33](../../../user-stories/US33-bluebell-results-processing.md)
**Plan:** [US33-code-generation-plan.md](../plans/US33-code-generation-plan.md)
**Status:** ✅ Complete
**Build:** Success
**Tests:** Pass (215 unit + 26 integration = 241)

## What changed

### Schema
- `Entrant.IsVet` (bool) — new column populated at parse time.
- Migration: `20260620160119_AddEntrantIsVet`.

### Services
- `RaceResultsService.UploadEntrantsAsync` resolves the current event up-front and dispatches to one of two parsers:
  - `ParseEntrantsWorkbookCrownToCrown` (existing behaviour, renamed).
  - `ParseEntrantsWorkbookBluebell` (new) — reads the `Age` column, accepts `Male U40` / `Female U35` / blank, rejects U18 or any other value with a row-specific error, and rejects gender/age mismatches.
- `RaceResultsService.GetTopTenByCategory(int)` now resolves the event type and switches categories:
  - **Bluebell 5:** Male, Female, Vet Male, Vet Female (Vet filter only — no skip-top-3 here, per AC 10).
  - **Crown to Crown:** unchanged (Male, Female, Male U18, Female U18).
- `RaceResultsService.GenerateResultsPdf` branches by event type; new helper `BuildPdfWinnersBlockBluebell` renders the 2-column, 4-row winners block.
- New `BluebellWinnerSelection.Select(IReadOnlyList<ResultRecord>)` returns the eight Bluebell winners with the skip-top-3 vet rule, factored out so it's unit-testable independently of PDF rendering.
- `RaceResultsService.TryGetEditableResult` now populates `EditResultInput.IsVet` and `EditResultInput.IsBluebell`.

### Views
- `Views/Race/EditResult.cshtml` swaps the Age input for a read-only Senior/Vet badge when editing a Bluebell entrant, with a hint telling the organiser to re-upload to change vet status.

### Models
- `EditResultInput` gains `IsVet` and `IsBluebell` (display-only flags).

### Tests
- New `BluebellResultsTests`:
  - Bluebell parser derives IsVet from the Age column.
  - U18 row rejected.
  - Age category mismatched to gender rejected.
  - Top 10 swaps U18 for Vet categories on Bluebell events.
  - Vet prize skips overall top 3 (M and F).
  - Empty vet slot returns null winner.
  - Bluebell PDF generates valid PDF bytes.
- `EventManagementTests.StatusCounts_AreScopedToCurrentEvent` updated: the post-switch upload now uses Bluebell-format Age values (parser dispatches by event type).

## Story traceability

| AC | Where |
|----|-------|
| 1 Bluebell upload reads Age column | `ParseEntrantsWorkbookBluebell` |
| 2 Senior vs Vet classification | `ParseEntrantsWorkbookBluebell` |
| 3 IsVet persisted | `Entrant.IsVet` + migration |
| 4 U18 / unknown value rejected | `ParseEntrantsWorkbookBluebell` |
| 5 M/F column required | `ParseEntrantsWorkbookBluebell` (existing required-column check retained) |
| 6 Winners with skip-top-3 vet rule | `BluebellWinnerSelection.Select` |
| 7 DNF/DSQ/DNS excluded from winners | Uses `GetCollatedResults` (existing semantics) |
| 8 Empty vet slot renders `-` | `WinnerText` returns `-` for null |
| 9 Top 10 Vet categories on Bluebell | `BuildTopTenFromCollated(_, EventType)` |
| 10 Vet Top 10 not subject to skip-top-3 | Top 10 uses `IsVet` filter only |
| 11 C2C PDF template reused | `GenerateResultsPdf` shares header, table, footer with C2C path |
| 12 8-name 2-column winners block | `BuildPdfWinnersBlockBluebell` |
| 13 Empty slots show `-` | `MalePlace`/`FemalePlace` helpers, `WinnerText` |
| 14 No Age/Vet column in table | Table column set unchanged |
| 15 Title from current event | Existing `BuildPdfEventTitle` |
| 16 Behaviour scoped to event type | Parser dispatch, Top 10 switch, PDF branch |

## Notes
- Course records continue to render on the Bluebell PDF if any are stored for `EventType.Bluebell5` (user confirmed); the reference 2026 PDF shows none because no records are seeded.
- Vet status is not editable in this story by design (per clarifying answer). Re-upload is the supported route to fix mis-categorisations.
- `BluebellWinnerSelection` was extracted from `RaceResultsService` to keep the prize-selection rules independently testable without spinning up a PDF.
