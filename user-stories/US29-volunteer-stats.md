# US29 - Volunteer Statistics

**Status:** ✅ Complete

## As a
Race organiser

## I want
Volunteer statistics per event and across the whole season, including each person's total volunteering count and London Marathon ballot entries

## So that
The club can recognise its volunteers' contributions (e.g. end-of-season awards), award the correct number of London Marathon ballot entries, and spot roles or events that are consistently short-handed

---

## Background

Builds directly on the volunteer roster (US28). Once assignments are recorded per event, per-event summaries and season aggregates fall out of the same data. This year the season count matters more than ever: **each volunteering instance earns one entry into the Pitsea RC London Marathon ballot**, so the per-volunteer total must be accurate and defensible.

## Per-Event Statistics

1. Total volunteers and assignments for the event
2. Breakdown by role (e.g. 8 marshals, 2 timekeepers)
3. Unfilled roles versus the event-type's usual complement (the C2C role complement defined in US28)

## Season Statistics

For a selected year, across **all events of all event types** (C2C, Bluebell 5, etc.):

1. **Total volunteering count per person** — how many times each person volunteered across all event types in the season. This is the headline figure and the basis for the ballot count.
2. **London Marathon ballot entries** — one entry per volunteering instance, **for Pitsea RC members only** (non-members are not eligible for the ballot). Per-volunteer entry counts and a season total, exportable for running the ballot.
3. **Most active volunteers** — ranked by events volunteered at; "ever-present volunteer" badge for those at every event.
4. **Total volunteering instances** and unique volunteers across the season.
5. **Per-volunteer season profile** — events attended, roles performed, run-after slots taken.
6. **Role coverage trends** — volunteers per event across the season (early warning when numbers decline).
7. **Run + volunteer combination** — where a volunteer is linked to a runner (US28/US15), show combined participation ("ran 3, volunteered 4, involved in all 7 events") — the strongest recognition stat for club awards.

## Acceptance Criteria

1. Each event's roster page shows the per-event statistics above.
2. A season volunteers page, selectable by year, shows the season statistics **aggregated across all event types**, not a single event type.
3. **Total volunteering count and ballot entries** are shown per volunteer; one ballot entry is counted per volunteering instance (each role at each event counts), **with no per-person cap**, counted **per season (year)**. **Ballot entries are counted for club members only** — non-members accrue a total volunteering count and full recognition but zero ballot entries.
4. Ever-present and most-active calculations count distinct events (multiple roles at one event count once for attendance, but all roles appear in the profile and each counts as a separate ballot entry).
5. Statistics respect volunteer deactivation: past contributions remain counted; deactivated volunteers simply stop appearing in assignment pickers.
6. Statistics reflect post-event roster edits (no-shows removed, additions included) per US28.
7. Calculations live in the service layer with unit test coverage (edge cases: volunteer with multiple roles in one event, single-event seasons, ties for most active, ballot-entry counting across mixed event types).
8. Season volunteer statistics — including the ballot entry list — are exportable (CSV at minimum, consistent with US18; Excel/PDF welcome alongside US28's exports).

## Notes

- **Depends on [[US28-volunteer-roster]]** — there are no statistics without assignments, and the ballot count depends on accurate, post-event-corrected rosters.
- Non-member volunteers (helpers who aren't Pitsea RC members — see US28) count equally in all volunteering counts, statistics, and recognition. The **one exception is the London Marathon ballot**, which is members-only: non-members earn no ballot entries. The member flag drives both this rule and the member/non-member breakdown, but never excludes anyone from recognition.
- Membership is renewed yearly each April, so a **single current member flag** on the volunteer is sufficient — there is no need to track membership status as at each event date.
- The combined run+volunteer stat additionally benefits from [[US15-runner-registry]] linkage.
- Natural companion to [[US24-season-statistics]] — a club "season review" could eventually combine both pages.
