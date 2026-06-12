# US29 - Volunteer Statistics

**Status:** 📋 Planned

## As a
Race organiser

## I want
Volunteer statistics per event and across the season

## So that
The club can recognise its volunteers' contributions (e.g. end-of-season awards) and spot roles or events that are consistently short-handed

---

## Background

Builds directly on the volunteer roster (US28). Once assignments are recorded per event, both per-event summaries and season aggregates fall out of the same data.

## Per-Event Statistics

1. Total volunteers and assignments for the event
2. Breakdown by role (e.g. 8 marshals, 2 timekeepers)
3. Unfilled roles versus the event-type's usual complement (based on the previous event of the same type)

## Season Statistics

For a selected year, across all events:

1. **Most active volunteers** — ranked by events volunteered at; "ever-present volunteer" badge for those at every event
2. **Total volunteering instances** and unique volunteers across the season
3. **Per-volunteer season profile** — events attended, roles performed
4. **Role coverage trends** — volunteers per event across the season (early warning when numbers decline)
5. **Run + volunteer combination** — where a volunteer is linked to a runner (US28/US15), show combined participation ("ran 3, volunteered 4, involved in all 7 events") — the strongest recognition stat for club awards

## Acceptance Criteria

1. Each event's roster page shows the per-event statistics above.
2. A season volunteers page, selectable by year, shows the season statistics.
3. Ever-present and most-active calculations count distinct events (multiple roles at one event count once for attendance, but all roles appear in the profile).
4. Statistics respect volunteer deactivation: past contributions remain counted; deactivated volunteers simply stop appearing in assignment pickers.
5. Calculations live in the service layer with unit test coverage (edge cases: volunteer with multiple roles in one event, single-event seasons, ties for most active).
6. Season volunteer statistics are exportable (CSV at minimum, consistent with US18).

## Notes

- **Depends on [[US28-volunteer-roster]]** — there are no statistics without assignments.
- The combined run+volunteer stat additionally benefits from [[US15-runner-registry]] linkage.
- Natural companion to [[US24-season-statistics]] — a club "season review" could eventually combine both pages.
