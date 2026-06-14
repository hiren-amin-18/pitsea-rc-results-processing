# US24 — Season Statistics & Runner Season Profiles — Implementation Summary

**Status:** ✅ Complete — build green, 154/154 tests passing (132 unit + 22 integration). No schema change. Depends on US15 + US17.

## Files changed

**Created**
- `Models/SeasonStatisticsModels.cs` (dashboard + profile DTOs).
- `Services/ISeasonStatisticsService.cs` + `SeasonStatisticsService.cs`.
- `Controllers/SeasonController.cs`; `Views/Season/Dashboard.cshtml`, `Views/Season/Runner.cshtml`.
- Tests: `SeasonStatisticsTests.cs` (6).

**Modified**
- `Program.cs` — registered `ISeasonStatisticsService`.
- `Views/Shared/_Layout.cshtml` — Season nav link.
- `Views/Runners/Index.cshtml` — per-runner "Season" profile link.
- `README.md`, `user-stories/US24-season-statistics.md`.

## Scope delivered

- **Dashboard (per calendar year, all event types):** most-attended runner(s) + ever-present; top clubs by entries and by unique runners; fastest per category **per event type**; most-improved per type (≥2 races); participation per event (entrants/finishers/first-timers) + total unique runners; season DNF rate; excluded-time count.
- **Runner profile:** races completed (event/date/type/position/category/time); season best per event type; average finish position overall and within category; current attendance streak; Champions points progression (C2C May–Sept only, cumulative).
- *Optional club championship table omitted* — the story marks its scoring as "club to confirm".

## Decisions

- **Keyed on `RunnerId` (AC3):** all cross-event aggregation groups by the runner registry, not names.
- **Same-type, typed times (AC4):** season bests, fastest-in-category, and most-improved compare only within an event type, using typed durations; unparseable rows are counted and surfaced.
- **Per-race category (AC5):** category is derived from each race's entrant (gender + age-at-race), so a runner who turns 18 mid-season is categorised per race.
- **Derived/live (AC6, AC7):** read-only and computed on demand from current data, so no recalculation step and archived events still contribute.
- **Lifetimes:** scoped service reusing `IRaceResultsService.GetCollatedResults(eventId)` for per-event finisher data (excludes DSQ, carries `RunnerId`/`Duration`/category).
- **Streak:** consecutive attended events ending at the runner's most recently attended event.

## Acceptance criteria — all met (1–8).
