# US45 — Online Registration Spreadsheet Generator — Implementation Summary

**Status:** ✅ Complete — build green, 288 unit + 26 integration = 314 tests passing (+24 new). One EF migration (`AddClubs`) seeded with the club's agreed canonical list.

## Files created

- `Models/Club.cs` — canonical club record (Id, Name, IsActive); no hard delete.
- `Models/OnlineRegistrationModels.cs` — `OnlineRegistrationRow`, `OnlineRegistrationPreview`, `UnresolvedClub`, `ClubSuggestion`, `OnlineRegistrationGenerateInput`.
- `Services/IClubService.cs` + `ClubService.cs` — thin CRUD over the Clubs DbSet (Add / Rename / SetActive) with duplicate-name guard.
- `Services/ClubMatcher.cs` — normaliser + token similarity + Levenshtein hybrid, plus a small running-club abbreviation table (`RC` → Running Club, `AC` → Athletics Club, `CC` → Cycling Club, `Tri` → Triathlon, `PRC` → Pitsea Running Club). Two thresholds: `ConfidentThreshold = 0.90` snaps automatically; `SuggestionThreshold = 0.55` populates the preview dropdown.
- `Services/IOnlineRegistrationGenerator.cs` + `OnlineRegistrationGenerator.cs` — the workhorse: parse two CSVs (CsvHelper), detect U18 by `U18IsDependantOfUser` header, fuzzy-match clubs, build preview; then on generate apply organiser resolutions, PROPER-case names, sort alphabetically by Surname/Forename, emit .xlsx via ClosedXML with header styling + frozen row + gender-coloured Age cells for U18s.
- `Controllers/ClubsController.cs` — admin CRUD.
- `Controllers/OnlineRegistrationController.cs` — event-scoped upload → preview → download flow, C2C-gated.
- `Views/Clubs/Index.cshtml` — Active + Inactive tables with inline add/rename/deactivate/reactivate forms.
- `Views/OnlineRegistration/Index.cshtml` — two-file upload form (adults + U18).
- `Views/OnlineRegistration/Preview.cshtml` — counts, unresolved-clubs table with pick/add/leave dropdown per row, duplicate + unrecognised-gender alerts, hidden PreviewJson posted to Generate.
- `Migrations/20260714144605_AddClubs.cs` + `.Designer.cs` — Clubs table + unique-name index + 55 seeded rows.
- `RaceResults.UnitTests/OnlineRegistrationGeneratorTests.cs` — 21 tests covering PROPER casing, fuzzy matcher (abbreviations, typos, unknown), preview happy path + Bluebell rejection + swapped files auto-correct + unresolved club + duplicate detection, generation with Age colour coding + alphabetical sort + persistent new-club addition + leave-as-typed + missing-resolution error + filename slug pattern.

## Files modified

- `Data/RaceResultsDbContext.cs` — added `DbSet<Club> Clubs`, entity config with unique-Name index, `SeedClubs()` seeding the 55-club canonical list from the story.
- `Program.cs` — registered `IClubService` + `IOnlineRegistrationGenerator`.
- `Views/Shared/_Layout.cshtml` — `Clubs` menu item under People; nav highlighting picks up the new controller.
- `Views/Events/Index.cshtml` — per-row **Registration** button (C2C only, non-archived) linking to the new controller.
- `README.md` — one-line feature mention in the intro paragraph; test counts (264 → 288 unit; 290 → 314 total).
- `docs/features.md` — two new rows: **Online Registration spreadsheet generator** and **Clubs registry**.
- `docs/user-stories.md` — US45 entry in the implemented table; range widened to `US27–US45`; dependency note explaining relationship to US01/US33.
- `docs/upload-formats.md` — new *Online-registration CSV inputs* section.
- `user-stories/US45-online-registration-spreadsheet-generator.md` — status flipped Draft → ✅ Complete.

## Key decisions

- **Clubs is a new, dedicated table** — not a projection of existing `Entrant.Club` values. Free-text `Club` on `Entrant`/`Runner` stays untouched, so rename in the Clubs table never rewrites history (AC9, explicitly out of scope). The fuzzy matcher reads from the Clubs table and only writes back when the organiser picks *Add as new*.
- **Fuzzy matcher is deterministic + testable** — token-Jaccard OR Levenshtein-ratio, whichever is higher; the abbreviation table expands `RC` / `AC` / `CC` / `Tri` / `PRC` so `Pitsea RC` normalises to the same key as `Pitsea Running Club` and hits Jaccard=1.0. Levenshtein catches single-letter typos (`Runnng`). Thresholds picked empirically: 0.90 snaps, 0.55–0.90 offers suggestions, below 0.55 = no match.
- **PROPER matches Excel's function exactly** — first letter after any non-letter is uppercased, rest lowercased. No bespoke `Mc…` / `O'…` list per story's explicit resolved decision; `Mcdonald` and `O'Brien` fall out for free.
- **Age colour coding is the exact Office 2007 Accent 1/2 @ 80% tint** — `#DCE6F2` (light blue, Male) and `#F2DCDB` (light pink, Female), matching the July 2026 reference file inspected earlier in the story-writing pass. Adult rows leave Age blank and unshaded.
- **Preview data survives round-trip via serialised JSON in a hidden form field**, mirroring the US35 volunteer-import pattern. Avoids re-parsing the CSVs across the request boundary and keeps the confirmed rows exactly the ones the organiser reviewed.
- **`add:{name}` writes to the Clubs table before generation**, so a second run against the same CSVs (AC10) has the club in the canonical list and no longer prompts.
- **C2C gating** lives in both the controller (`Index` redirects Bluebell events with a flash message) and the service (`BuildPreview` errors if the event's `EventType != CrownToCrown`). The Events-page button is hidden entirely for Bluebell rows, so the redirect only fires on a hand-typed URL.
- **Files uploaded the wrong way round are silently swapped** — if the "adults" slot contains the U18 file and vice versa, the service swaps them by their detected type rather than erroring. Erroring only when both slots look U18 (or neither does).

## Acceptance criteria — all met (1–11)

| AC | How covered |
|---|---|
| 1 — C2C-only event action | Events/Index.cshtml button `raceEvent.EventType == EventType.CrownToCrown` gate; controller `Index` redirect; service preview error |
| 2 — Identify CSVs by `U18IsDependantOfUser` | `ParseCsv` sets `isU18File`; both-U18 / both-adult surface as preview errors |
| 3 — Dry-run preview | Preview.cshtml renders row counts, unresolved-club table, duplicates alert, unrecognised-gender alert; button disabled until `CanGenerate` |
| 4 — Output matches template shape | `Excel` module: 6 columns with same headers, styled + frozen header, Race # blank on every row, Age blank for adults, U18 Age filled + colour-coded (unit test `Generate_ProducesXlsx…`) |
| 5 — PROPER-cased Forename + Surname | `Excel.Proper` unit tests |
| 6 — Fuzzy club match with organiser resolution | `ClubMatcher` unit tests + Preview flow |
| 7 — Alphabetical by Surname then Forename | `Generate_ProducesXlsx…` asserts order |
| 8 — `<event-slug>-online-registration.xlsx` filename | `EventSlug` + unit test `Generate_FilenameFollowsEventSlugPattern` |
| 9 — Age cell colour coding | `Generate_ProducesXlsx…` asserts `#DCE6F2` / `#F2DCDB` |
| 10 — Clubs admin (add / rename / deactivate) | `Views/Clubs/Index.cshtml` + `ClubsController` + `ClubService` |
| 11 — Second run matches without prompting | `Generate_AddNewClub_PersistsToClubsTable` asserts a follow-up preview has empty `UnresolvedClubs` |
| 11b — Does not create entrants | Controller returns `File(...)`; no `Entrant` writes in the code path — enforced by absence in the service |
