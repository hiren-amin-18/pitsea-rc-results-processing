# US29 — Volunteer Statistics — Code Generation Plan

**Story:** [US29](../../../user-stories/US29-volunteer-stats.md)
**Type:** Brownfield. No schema change — all stats are derived from US28's `VolunteerAssignment` table.

## Design

### Services

- **`IVolunteerStatsService` / `VolunteerStatsService`** — pure aggregation on top of `IDbContextFactory`.
  - **Per-event stats** (`GetEventStats(eventId)`):
    - `TotalAssignments`, `DistinctVolunteers`.
    - `RoleBreakdown` — for each active role: `AssignedCount`, `DefaultCount`, `MinCount`, `Shortfall` (max(0, Min−Assigned)).
    - Already partly surfaced by `IVolunteerRosterService.GetRoster` — the new method returns just the numbers (no full roster object), keeping the roster page query small.
  - **Season stats** (`GetSeasonStats(year)`):
    - `VolunteerProfiles` — one row per volunteer with at least one assignment in the year:
      - `Volunteer`, `EventsAttended` (distinct events), `Assignments` (every role-event pair), `RolesPerformed` (distinct role names with count), `RunAfterCount`, `BallotEntries` (= total assignments for club members, 0 for non-members).
      - `IsEverPresent` (= attended every event of the year — see "Edge cases" below).
      - `RunAndVolunteer` block when `Volunteer.RunnerId` is set: `RunCount` (entrants in the year for this runner), `EventsInvolvedIn` (union of ran/volunteered).
    - `TotalInstances`, `UniqueVolunteers`, `TotalBallotEntries` (members only).
    - `RoleCoverageTrend` — `[ { Event, TotalAssignments } ]` ordered by event date (early warning when numbers fall).
    - `MostActive` — top N by `EventsAttended`, ties broken by `Assignments`.
  - Year scoping uses **calendar year** (`EventDate.Year`) — established convention (Champions, US24, US30) and matches C2C's Good Friday → Boxing Day window where season ≈ calendar year.

- **Per-event stats are also embedded into the existing roster view** (`RosterViewModel.PerEventStats`) so the roster page shows the AC1 numbers without an extra round-trip — `VolunteerRosterService.GetRoster` calls into `IVolunteerStatsService` to fill it.

### Controller + View

- **`VolunteerStatsController`** (`/VolunteerStats`):
  - `Index(int? year)` — defaults to today's calendar year. Picks available years from `VolunteerAssignments`.
  - `Csv(int year)` — emits the per-volunteer season profile as CSV (member, total assignments, ballot entries, events attended, roles performed, run-after count, runner-link summary if linked). Encoded with `CsvHelper` (already on csproj for US18).
- **View** `Views/VolunteerStats/Index.cshtml` — year picker, four sections:
  1. **Season summary** (cards): total instances, unique volunteers, total ballot entries, events covered.
  2. **Most active volunteers** (top 10) + ever-present badge.
  3. **Per-volunteer table** sorted by ballot entries desc (CSV export link).
  4. **Role coverage trend** — small table per event in the year.
- **Manage menu**: add "Volunteer Stats" link.

### Per-event stats on roster page

Extend `RosterViewModel` with a `PerEventStats` block (`TotalAssignments`, `DistinctVolunteers`, `UnfilledRoles` count); render a small summary panel above the category tables.

### Edge cases (documented and tested)

- **Ever-present** = volunteer attended every event of the year *that they could have attended* (every event with at least one assignment that year). If only one event ran in a year, ever-present collapses to "attended that one event". Avoids a confusing "every event = 1 trivially" badge for single-event seasons.
- **Members-only ballot**: non-members earn ballot entries = 0 even if they have assignments. AC3 explicitly requires this.
- **Multi-role at one event**: each assignment is one ballot entry (per the story); but the volunteer is counted once for `EventsAttended`.
- **Ties for most active**: ordered by `EventsAttended` desc, then `Assignments` desc, then `Name` asc.

## Steps

- [ ] **Step 1 — Models.** `Models/VolunteerStatsModels.cs` — `EventVolunteerStats`, `SeasonVolunteerStats`, `VolunteerSeasonProfile`, `RoleBreakdownRow`, `RoleCoverageTrendItem`, `RunAndVolunteerSummary`.
- [ ] **Step 2 — Service.** `IVolunteerStatsService` + `VolunteerStatsService`, with carefully-bounded EF queries (no in-memory blow-ups on `VolunteerAssignments`).
- [ ] **Step 3 — Roster integration.** Extend `RosterViewModel` with `PerEventStats`; `VolunteerRosterService.GetRoster` populates it.
- [ ] **Step 4 — Unit tests.** `VolunteerStatsTests.cs`:
  - Empty season → empty stats (no crash).
  - Single event → ever-present collapses sensibly.
  - Multi-event season with two volunteers, one ever-present.
  - Non-member earns assignments but zero ballot entries.
  - Volunteer with multiple roles at one event → counted once for EventsAttended, two for Assignments / BallotEntries.
  - Runner-linked volunteer's `RunAndVolunteer` aggregates `Entrant` count for the year and unions involved events.
  - Ties for most active break by Assignments then Name.
- [ ] **Step 5 — Controller + View.** `VolunteerStatsController` + `Views/VolunteerStats/Index.cshtml`; navbar link; per-event panel on the roster page.
- [ ] **Step 6 — DI.** Register the stats service as scoped.
- [ ] **Step 7 — Build + docs.** Full suite green; README; US29 → ✅; audit; implementation summary.

## Story traceability

| AC | Covered by |
|----|------------|
| 1 per-event stats on roster page | Steps 2, 3, 5 |
| 2 season stats page (year picker, all event types) | Steps 2, 5 |
| 3 total volunteering count + ballot entries (members only, no cap, per year) | Steps 2, 4 |
| 4 ever-present + most-active counting distinct events | Steps 2, 4 |
| 5 respects deactivation (past stays counted) | Step 2 (queries don't filter by IsActive for historic rows) |
| 6 reflects post-event roster edits | Step 2 (live query against current assignments) |
| 7 service-layer + unit tests inc. edge cases | Steps 2, 4 |
| 8 CSV export | Step 5 |
