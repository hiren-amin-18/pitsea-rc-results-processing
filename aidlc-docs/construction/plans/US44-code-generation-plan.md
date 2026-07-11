# US44 — Champions of Champions Detailed Per-Event Breakdown — Code Generation Plan

**Story:** [US44](../../../user-stories/US44-champions-detailed-per-event-breakdown.md)
**Type:** Brownfield, presentation only. **No schema change** — the per-event points already live in `PointsAuditLog`.

## Design

- **Service projection.** New `IChampionsOfChampionsService.GetLeaderboardDetailAsync(seasonYear, asOfEventId)` returning a `ChampionsDetail`:
  - `Columns` — the in-season (May–Sept) Crown to Crown events **that awarded points**, up to and including the as-of event, ordered by date, each with a 1-based `Round` and label `"Round {n} – {Month}"` (plus event name/date for a tooltip).
  - `Rows` — the same ranked `ChampionsLeaderboardEntry` list the summary uses (via the existing `AggregateAudits` + `RankAndReturn`), each paired with a `PointsByEventId` map so the view can look up the points a runner scored in each column.
  - Rows reuse the summary ranking verbatim, so order, ties, and top-3 highlighting are identical to the summary; per-event cells therefore reconcile to the row's `TotalPoints` (AC7).
  - Blank cells are "no entry in the map" — the view renders nothing / a dash (AC5).
- **Toggle = server round-trip.** A `detail` flag on the actions (`?detail=1`), mirroring the existing year selector. The controller renders summary or detail; export links carry the flag so exports follow the on-screen view (AC10).
- **Exports.** Detailed PDF switches to **A4 landscape** (summary stays portrait) with one column per round; detailed CSV inserts one column per round between Club and the aggregates. UTF-8-with-BOM preserved.
- **Both surfaces.** Admin (`Views/Champions/Leaderboard.cshtml`) and public (`Views/Public/Champions.cshtml`) both get the toggle + detail table; only the admin surface has exports.

## Steps

- [ ] **Step 1 — Service.** Add `ChampionsDetail`/`ChampionsDetailColumn`/`ChampionsDetailRow` and `GetLeaderboardDetailAsync` (reusing `AggregateAudits`/`RankAndReturn`); wire into the interface.
- [ ] **Step 2 — Admin controller + VM.** `Leaderboard`, `ExportPdf`, `ExportCsv` take `bool detail`; `ChampionsLeaderboardViewModel` gains `ShowDetail` + `Detail`.
- [ ] **Step 3 — Admin view.** Summary/Details toggle; detail table per category (Rank, Name, Club, round columns, Events, Points) with top-3 highlighting and † marker; export links carry `detail`.
- [ ] **Step 4 — Exports.** Detailed CSV (round columns) and landscape detailed PDF.
- [ ] **Step 5 — Public.** `PublicController.Champions` + `PublicChampionsViewModel` take/expose the flag; toggle + detail table on the public page.
- [ ] **Step 6 — Tests.** Per-event matrix reconciles to summary totals; as-of scopes the columns; blank where no points.
- [ ] **Step 7 — Build + docs.** Full suite green; `docs/champions.md`, user-stories index, implementation summary; US44 → ✅.

## Story traceability

| AC | Covered by |
|----|------------|
| 1 toggle, both surfaces, default summary | Steps 2, 3, 5 |
| 2 detail table shape (rows/cols/cells) | Steps 1, 3, 5 |
| 3 columns = scored events up to as-of | Step 1 |
| 4 "Round n – Month" labels | Step 1 |
| 5 blank/– empty cells | Steps 3, 4, 5 |
| 6 keep totals, grouping, top-3, † | Steps 3, 4, 5 |
| 7 rows reconcile to summary total | Steps 1, 6 |
| 8 detailed PDF (landscape) | Step 4 |
| 9 detailed CSV | Step 4 |
| 10 export follows the view | Steps 2, 3, 4 |
| 11 empty/early-season graceful | Steps 1, 3, 5 |
| 12 no scoring change | Step 1 (projection only) |
