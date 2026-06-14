# US31 — Season Calendar Generator — Code Generation Plan

**Story:** [US31](../../../user-stories/US31-season-calendar-generator.md)
**Type:** Brownfield + schema change (optional start time).

## Design

- `RaceEvent.StartTime` (nullable `TimeSpan`), populated by the generator and editable; the PDF title format is unchanged.
- Pure static `SeasonCalendar`:
  - `ComputeEasterSunday(year)` — Anonymous Gregorian (Meeus). `GoodFriday(year)` = Easter − 2.
  - `SecondWednesday(year, month)`, `FirstWednesday(year, month)`.
  - `BuildFixtures(year, septemberOption)` → eight `SeasonFixture` rows (name + date + start time).
- `ISeasonCalendarService`/`SeasonCalendarService`:
  - `Preview(year, septemberOption)` → list of fixtures **with `AlreadyExists`** flagged against existing C2C events on the same date (no creation).
  - `Generate(year, septemberOption)` → creates only the missing fixtures; never modifies existing; doesn't change the current event.

## Steps

- [ ] **Step 1 — Schema.** `RaceEvent.StartTime`; migration `AddEventStartTime`.
- [ ] **Step 2 — `SeasonCalendar`** + unit tests across years (March-Easter edges) and Wednesday computations.
- [ ] **Step 3 — Service.** Preview/generate.
- [ ] **Step 4 — UI.** "Generate season" button → `Generate` form (year + September-choice + preview table) → POST creates and lands back on Events.
- [ ] **Step 5 — Display.** Show start time alongside date on the Events page when set.
- [ ] **Step 6 — Tests.** Generate creates 8; idempotent re-run is a no-op; existing event flagged as skip; current event unchanged.
- [ ] **Step 7 — Build + docs.** Full suite green; README; US31 → ✅.

## Story traceability

| AC | Covered by |
|----|------------|
| 1 generate action | Steps 2, 3, 4 |
| 2 consistent naming, editable | Steps 2, 5 |
| 3 optional StartTime, editable | Steps 1, 5 |
| 4 idempotent preview/skip | Step 3 |
| 5 generated == manual | Steps 1, 3 |
| 6 current event unchanged | Step 3 |
| 7 unit tests across years | Step 2 |
