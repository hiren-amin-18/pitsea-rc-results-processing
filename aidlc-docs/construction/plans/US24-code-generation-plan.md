# US24 — Season Statistics & Runner Season Profiles — Code Generation Plan

**Story:** [US24](../../../user-stories/US24-season-statistics.md)
**Type:** Brownfield, no schema change. Read-only aggregation. Depends on US15 (RunnerId) and US17 (typed times).

## Design

New scoped `ISeasonStatisticsService` aggregating across all events in a calendar year (all event types). All cross-event keys on `RunnerId` (AC3); time stats compare only within the same event type and use typed durations, counting excluded rows (AC4); categories derived per race from the entrant's age at that race (AC5); read-only and derived live from current data, so archived events still contribute and no recalculation step is needed (AC6, AC7).

## Scope

- **Season dashboard:** most-attended runner(s) + ever-present; top clubs by entries and by unique runners; fastest per category **per event type**; most-improved runner (≥2 races of a type); participation per event (entrants/finishers/first-timers) + total unique runners; season DNF rate. *(Optional club championship table omitted — story marks it "club to confirm".)*
- **Runner season profile:** races completed (event/date/position/time); season-best per event type; average finish position overall and within category; improvement curve (times per type); Champions points progression (C2C only); current attendance streak.

## Steps

- [ ] **Step 1 — Models.** `SeasonStatisticsModels.cs` (dashboard + profile DTOs).
- [ ] **Step 2 — Service.** `ISeasonStatisticsService`/`SeasonStatisticsService`: `GetAvailableSeasons`, `GetSeasonDashboard(year)`, `GetRunnerSeasonProfile(runnerId, year)`. Loads events + per-event collated results once.
- [ ] **Step 3 — Controller + views.** `SeasonController` (Dashboard, RunnerProfile); `Season/Dashboard.cshtml`, `Season/RunnerProfile.cshtml`; nav link; profile links from the Runners list (AC2).
- [ ] **Step 4 — Tests.** Per-statistic incl. single-event season, single-race runner, ties for most attended.
- [ ] **Step 5 — Build + docs.** Full suite green; README; US24 → ✅.

## Story traceability

| AC | Covered by |
|----|------------|
| 1 season page by year | Steps 2, 3 |
| 2 runner profile reachable | Step 3 |
| 3 keys on RunnerId | Step 2 |
| 4 same-type typed time stats + excluded count | Step 2 |
| 5 per-race category | Step 2 |
| 6 auto-updates (derived) | Step 2 |
| 7 read-only, archived contribute | Steps 2, 3 |
| 8 service-layer + tests | Steps 2, 4 |
