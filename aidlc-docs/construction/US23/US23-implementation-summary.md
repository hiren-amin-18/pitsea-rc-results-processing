# US23 — Enhanced Race Statistics — Implementation Summary

**Status:** ✅ Complete — build green, 148/148 tests passing (126 unit + 22 integration). No schema change.

## Files changed

**Created**
- `Models/RaceStatisticsSummary.cs`.
- Tests: `RaceStatisticsSummaryTests.cs` (5).

**Modified**
- `Models/RaceStatsDashboardViewModel.cs` — `Summary`.
- `Services/IRaceResultsService.cs` + `RaceResultsService.cs` — `GetRaceStatisticsSummary()` + nearest-rank `Percentile` helper.
- `Controllers/RaceController.cs` — populates `Summary`.
- `Views/Race/Stats.cshtml` — completion card, gender-% card, finish-time card (winner/median/average/percentiles/spread), affiliation doughnut chart, busiest-window line, exclusion caveat.
- `README.md`, `user-stories/US23-enhanced-race-statistics.md`.

## Decisions

- **Service-layer calculation (AC8):** all figures in `GetRaceStatisticsSummary()`, current-event scoped (AC7), with per-statistic tests including empty-race and DNF/DNS edges.
- **Denominators:** completion rate = finishers ÷ starters, where starters exclude DNS (US16). DNF count also excludes DNS. The DNS runner is neither a starter nor a DNF.
- **Times (AC4/AC5):** medians/percentiles/average use typed durations (US17); rows without a parseable duration are counted and surfaced as a caveat rather than dropped silently — effectively 0 now that US17 validates at upload.
- **Affiliation (AC3):** derived from the existing `RaceStats` convention (unaffiliated counts exclude U18); affiliated = starters − unaffiliated.
- **Busiest window (AC6):** computed from the same per-minute duration buckets the chart uses, so the headline matches the graph.
- **Age-based stats deliberately excluded** per the club convention (ages only recorded for U18).

## Acceptance criteria — all met (1–8).
