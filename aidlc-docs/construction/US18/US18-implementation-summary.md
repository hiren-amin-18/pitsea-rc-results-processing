# US18 — Export Results to CSV — Implementation Summary

**Status:** ✅ Complete — build green, 99/99 tests passing (78 unit + 21 integration).

## Files changed

**Modified**
- `RaceResults.Web/Services/IRaceResultsService.cs` — added `GenerateResultsCsv()` and `GetResultsCsvFileName()`.
- `RaceResults.Web/Services/RaceResultsService.cs` — CSV serialisation (UTF-8 BOM, CsvHelper escaping), `BuildEventSlug` helper; new `System.Text` / `System.Text.RegularExpressions` usings.
- `RaceResults.Web/Controllers/RaceController.cs` — `ExportCsv` action.
- `RaceResults.Web/Controllers/ChampionsController.cs` — `ExportCsv` action + private `GenerateLeaderboardCsv` (mirrors existing `GenerateLeaderboardPdf`); CsvHelper usings.
- `RaceResults.Web/Views/Race/Results.cshtml` — "Export CSV" button beside "Export PDF".
- `RaceResults.Web/Views/Champions/Leaderboard.cshtml` — "Export to CSV" button beside PDF export.
- `README.md` — features, workflow, story tables, routes, test counts.
- `user-stories/US18-export-results-csv.md` — Status → ✅ Complete.

**Created**
- `RaceResults.UnitTests/CsvExportTests.cs` — 4 tests (header/finishers/DNF section, comma escaping, UTF-8 BOM, filename format).

## Decisions

- **DNF separation (AC3):** used a `Status` column (`Finished` / `Unmatched` / `DNF`) — the story explicitly permits this over a trailing section, and it keeps the file a single clean table for spreadsheets. DNF rows leave Position/Time blank.
- **Excel-friendliness (AC7):** UTF-8 **with BOM** + CsvHelper's automatic quoting of fields containing commas/quotes.
- **Scoping (AC5):** results CSV = current event; leaderboard CSV reuses the same `year`/`eventId` query parameters as the on-screen view and PDF export.
- **Leaderboard CSV location:** placed in `ChampionsController` to mirror the existing leaderboard-PDF pattern rather than expanding the service interface.

## Acceptance criteria — all met (1–7).
