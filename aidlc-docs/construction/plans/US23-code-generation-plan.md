# US23 — Enhanced Race Statistics — Code Generation Plan

**Story:** [US23](../../../user-stories/US23-enhanced-race-statistics.md)
**Type:** Brownfield, no schema change. Builds on US17 (typed durations) and US16 (DNS exclusion).

## Design

New `RaceStatisticsSummary` computed in the service (AC8). All figures scoped to the current event (AC7), all derived from existing data. Age-based stats deliberately excluded (club convention).

- **Completion:** entrants (started = excluding DNS), finishers, DNF, completion rate %.
- **Gender split:** male/female finisher counts + % of finishers.
- **Affiliation:** affiliated vs unaffiliated from the existing `RaceStats` figures (AC3).
- **Finish times** (typed durations): winner, median, average, winner→median spread, 25/50/75 percentiles; count of rows excluded for unparseable/missing time (AC5 — effectively 0 post-US17, but surfaced not silent).
- **Busiest window:** peak finishers-per-minute bucket, matching the chart (AC6).

## Steps

- [ ] **Step 1 — Model.** `RaceStatisticsSummary`; add `Summary` to `RaceStatsDashboardViewModel`.
- [ ] **Step 2 — Service.** `GetRaceStatisticsSummary()` on `IRaceResultsService`/`RaceResultsService` (percentile/median/average helpers; busiest-window).
- [ ] **Step 3 — Controller.** `RaceController.Stats` populates `Summary`.
- [ ] **Step 4 — View.** Completion card, gender %, affiliation chart, finish-time summary card, exclusion caveat, busiest-window line above the per-minute chart.
- [ ] **Step 5 — Tests.** Per-statistic unit tests incl. empty-race and DNF-heavy edges; DNS excluded from completion denominator.
- [ ] **Step 6 — Build + docs.** Full suite green; README; US23 → ✅.

## Story traceability

| AC | Covered by |
|----|------------|
| 1 completion summary | Steps 1–4 |
| 2 gender % | Steps 2, 4 |
| 3 affiliation chart | Steps 2, 4 |
| 4 time summary + percentiles | Steps 2, 4 |
| 5 excluded-time caveat | Steps 2, 4 |
| 6 busiest window | Steps 2, 4 |
| 7 current-event scope | Step 2 |
| 8 service-layer + tests | Steps 2, 5 |
