# US44 — Champions of Champions Detailed Per-Event Breakdown — Implementation Summary

**Status:** ✅ Complete — build green, 266/266 unit tests passing (incl. 2 new). Presentation-only; **no schema change**.

## Files changed

**Created**
- `Services/ChampionsDetail.cs` — `ChampionsDetail` (columns + rows), `ChampionsDetailColumn` (event id, round, `"Round n – Month"` label, name/date for tooltip), `ChampionsDetailRow` (summary entry + `PointsByEventId` map + `PointsFor(eventId)`).
- `aidlc-docs/construction/plans/US44-code-generation-plan.md`.
- Tests in `ChampionsOfChampionsServiceTests.cs`: reconciliation/columns and as-of scoping (+ `SeedTwoEventSeasonAsync` helper).

**Modified**
- `Services/IChampionsOfChampionsService.cs` + `ChampionsOfChampionsService.cs` — `GetLeaderboardDetailAsync(seasonYear, asOfEventId?)`; reuses the private `AggregateAudits`/`RankAndReturn` so detail rows equal the summary rows.
- `Controllers/ChampionsController.cs` — `Leaderboard`/`ExportPdf`/`ExportCsv` take `bool detail`; new `GenerateDetailedLeaderboardCsv` and `GenerateDetailedLeaderboardPdf` (A4 landscape).
- `Models/ChampionsLeaderboardViewModel.cs` — `ShowDetail` + `Detail`.
- `Views/Champions/Leaderboard.cshtml` — Summary/Details toggle; detail table; export links carry `detail`.
- `Controllers/PublicController.cs` + `Models/PublicViewModels.cs` + `Views/Public/Champions.cshtml` — same toggle + detail table on the public page.
- `docs/champions.md`, `docs/user-stories.md`, `user-stories/US44-…md`.

## Decisions

- **Data source (AC12, no schema change):** the per-event points already exist in `PointsAuditLog`. The new projection takes the latest non-voided batch per event (mirroring `AggregateAudits`) and pivots it into a runner×event matrix keyed by the same runner identity the summary uses — so per-event cells always sum to the row's total (AC7, asserted in tests).
- **Columns (AC3/AC4):** only in-season (May–Sept) events that actually awarded points, ordered by date, numbered `Round n`, capped at the as-of event's date. Non-scoring Good Friday / Boxing Day fixtures never appear.
- **Empty cells (AC5):** an event missing from a runner's map renders `–` on screen / blank in exports — no ran-vs-absent distinction.
- **Toggle (AC1/AC10):** server round-trip via `?detail=1`, consistent with the existing year selector; export links propagate the flag so exports match the on-screen view.
- **Exports (AC6/AC8/AC9):** detailed CSV inserts round columns between Club and the aggregates; detailed PDF switches to A4 landscape, keeping category grouping, Events/Points aggregates, top-3 gold/silver/bronze highlighting and the † marker. Summary exports unchanged (still portrait). UTF-8-with-BOM preserved.
- **Both surfaces (AC1):** admin and public share the projection and table shape; only admin has exports.

## Acceptance criteria — all met (1–12).
