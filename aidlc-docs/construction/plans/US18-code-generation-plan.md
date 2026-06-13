# US18 — Export Results to CSV — Code Generation Plan

**Story:** [US18](../../../user-stories/US18-export-results-csv.md)
**Type:** Brownfield (modify existing files)
**Scope:** Serialisation + two controller actions + two view buttons. No schema changes.

## Unit context

- Reuses existing `GetCollatedResults()`, `GetDnfEntrants()` (results) and `GetLeaderboardAsync()` (leaderboard).
- `CsvHelper` is already referenced by `RaceResults.Web`.

## Steps

- [ ] **Step 1 — Business logic (results CSV).** Add `GenerateResultsCsv()` and `GetResultsCsvFileName()` to `IRaceResultsService` / `RaceResultsService`. UTF-8 **with BOM** for Excel; columns `Position, Time, Bib, Name, Club, Gender, Age, Status`; finishers rows then DNF rows (`Status` = `Finished` / `Unmatched` / `DNF`). CsvHelper handles comma/quote escaping (AC2, AC3, AC7). Descriptive filename `{event-slug}-{yyyy-MM-dd}-results.csv` (AC6).
- [ ] **Step 2 — Business logic (leaderboard CSV).** Add private `GenerateLeaderboardCsv(...)` to `ChampionsController` (mirrors existing private `GenerateLeaderboardPdf`). Columns `Category, Rank, Name, Club, Races, Points, Tied` (AC4).
- [ ] **Step 3 — API layer.** Add `RaceController.ExportCsv` and `ChampionsController.ExportCsv(int? eventId, int? year)`; the leaderboard export honours the same `year`/`eventId` scoping as the on-screen view and PDF (AC5). Filename `champions-of-champions-{year}.csv` (AC6).
- [ ] **Step 4 — Frontend.** Add an "Export CSV" button next to "Export PDF" on `Views/Race/Results.cshtml` and `Views/Champions/Leaderboard.cshtml` (AC1, AC4).
- [ ] **Step 5 — Unit tests.** Add CSV tests: results CSV header/rows/DNF section/escaping; filename format; leaderboard CSV columns.
- [ ] **Step 6 — Build & test.** `dotnet build` + full test suite green.
- [ ] **Step 7 — Docs.** Update README features/workflow to mention CSV export.

## Story traceability

| AC | Covered by |
|----|------------|
| 1 Results page button | Step 4 |
| 2 Results columns | Step 1 |
| 3 DNF separation | Step 1 (Status column) |
| 4 Leaderboard CSV + button | Steps 2, 4 |
| 5 Scoped as on-screen | Step 3 |
| 6 Descriptive filenames | Steps 1, 3 |
| 7 Excel-friendly UTF-8 + escaping | Step 1 (BOM + CsvHelper) |
