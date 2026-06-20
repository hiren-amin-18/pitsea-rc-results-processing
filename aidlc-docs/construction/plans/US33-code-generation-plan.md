# US33 — Bluebell 5 Results Processing — Code Generation Plan

**Story:** [US33](../../../user-stories/US33-bluebell-results-processing.md)
**Type:** Brownfield. Existing schema gains one column on `Entrant` (`IsVet`). Parsing, Top10 categorisation, and PDF winners block branch by `RaceEvent.EventType`. Crown to Crown behaviour is untouched.

## Design

### Domain change
- Add `bool IsVet` to `Entrant`. Populated by the upload parser; not user-editable in this story.
- Vet eligibility derivation runs at parse time so it survives results regeneration. For Bluebell entrants:
  - `Age` column value `Male U40` → male Senior, `IsVet = false`.
  - `Age` column value `Female U35` → female Senior, `IsVet = false`.
  - Blank → `IsVet = true` (gender determines threshold, M40+ / F35+).
  - Anything else (e.g. `Male U18`, `Female U18`, freeform text) → row-level upload error.
- For Crown to Crown entrants `IsVet` is always `false` (legacy `Age` int handling untouched).

### Parser dispatch
- `UploadEntrantsAsync` resolves the current event and picks one of two private parser methods:
  - `ParseEntrantsWorkbookCrownToCrown` (today's behaviour, renamed from `ParseEntrantsWorkbook`).
  - `ParseEntrantsWorkbookBluebell` (new). Same required columns (`Race No`/`bib`, `Name`, `M/F`/`Gender`) plus an `Age` column whose values must be in {`Male U40`, `Female U35`, blank}.
- Header aliases already cover `raceno` and `mf`, so no new aliases are needed.

### Top 10 (extending US12)
- `BuildTopTenFromCollated` becomes event-type-aware. Signature changes to take the event type from the calling method.
- For `EventType.Bluebell5`: return `Male`, `Female`, `Vet Male`, `Vet Female` — Vet lists filtered by `IsVet` only (no skip-top-3 rule per AC 10).
- For `EventType.CrownToCrown`: unchanged (`Male`, `Female`, `Male U18`, `Female U18`).

### PDF (extending US09)
- `GenerateResultsPdf(int eventId)` already loads `currentEvent`. Branch on `currentEvent.EventType`:
  - **Crown to Crown:** existing winners block (4 names, 2-column).
  - **Bluebell 5:** new winners block — 2 columns, 4 rows:
    1. `1st Male = X    |    1st Female = X`
    2. `2nd Male = X    |    2nd Female = X`
    3. `3rd Male = X    |    3rd Female = X`
    4. `1st Vet Male = X    |    1st Vet Female = X`
- Winner selection for Bluebell:
  - Top 3 male / female = first three finishers by chip time of each gender (`Status == Finished`, DNF/DSQ/DNS excluded — already true of `GetCollatedResults`).
  - 1st Vet Male = first male finisher with `IsVet == true` who is **not** in the male top 3.
  - 1st Vet Female = first female finisher with `IsVet == true` who is **not** in the female top 3.
  - Empty slots render as `… = -` (existing `WinnerText` helper already handles null).
- Results table columns unchanged (Position, Time, Race No, Name, Gender, Club Name). No Age/Vet column added.
- Course-records line continues to render if records exist for the current event type (per user confirmation, Bluebell course records are future-proofed).

### Edit UI (extending US10)
- `EditResult.cshtml` shows a small read-only Vet badge next to the Gender field when the current event is Bluebell and the entrant has `IsVet`. No new form fields; vet status cannot be edited inline — re-upload is required.
- `EditResultInput` gains a read-only `bool IsVet` (server-populated, not bound from the form).

### Tests
- `UploadEntrantsTests` — new fixture(s) for Bluebell parser path: valid M U40 / F U35 / blank rows; U18 row rejected with row-specific error; unknown value rejected; gender required.
- `StatsAndTopTenTests` — Bluebell event returns Male / Female / Vet Male / Vet Female categories; Vet lists ignore the skip-top-3 rule.
- `PdfGenerationTests` — Bluebell PDF contains 1st/2nd/3rd M & F plus 1st Vet M/F; vet prize skips top 3 (vet finishing 2nd does not also win the vet prize).

## Steps

- [x] **Step 1 — Schema.** Add `IsVet` to `Entrant`, create EF migration `AddEntrantIsVet`, update model snapshot.
- [x] **Step 2 — Parser dispatch.** Rename existing parser to `ParseEntrantsWorkbookCrownToCrown`; add `ParseEntrantsWorkbookBluebell`; `UploadEntrantsAsync` selects based on `GetCurrentEvent().EventType`.
- [x] **Step 3 — Top 10.** Make `BuildTopTenFromCollated` event-type-aware.
- [x] **Step 4 — Winners + PDF.** Branch `GenerateResultsPdf` by event type; add `BuildPdfWinnersBlockBluebell`; implement skip-top-3 vet selection.
- [x] **Step 5 — Edit UI.** Surface read-only Vet badge in `EditResult.cshtml`; populate `EditResultInput.IsVet` in `TryGetEditableResult`.
- [x] **Step 6 — Tests.** New cases in `UploadEntrantsTests`, `StatsAndTopTenTests`, `PdfGenerationTests`.
- [x] **Step 7 — Build + finalise.** `dotnet build` + `dotnet test` green; update README; mark US33 ✅ Complete; write implementation summary; append audit entry.

## Story traceability

| AC | Covered by |
|----|------------|
| 1 Bluebell upload reads Age column | Step 2 |
| 2 Senior vs Vet classification | Steps 1, 2 |
| 3 IsVet persisted | Step 1 |
| 4 U18 / unknown value → upload error | Step 2 |
| 5 M/F column required | Step 2 (existing required-column check retained) |
| 6 Winners with skip-top-3 vet rule | Step 4 |
| 7 DNF/DSQ/DNS excluded from winners | Step 4 (uses existing `GetCollatedResults`) |
| 8 Empty vet slot renders `-` | Step 4 (existing `WinnerText`) |
| 9 Top 10 Vet categories | Step 3 |
| 10 Vet Top 10 not subject to skip-top-3 | Step 3 |
| 11 C2C PDF template reused | Step 4 |
| 12 8-name 2-column winners block | Step 4 |
| 13 Empty slots show `-` | Step 4 |
| 14 No Age/Vet column in table | Step 4 (no table change) |
| 15 Title from current event | Step 4 (existing `BuildPdfEventTitle`) |
| 16 Behaviour scoped to event type | Steps 2, 3, 4 |
