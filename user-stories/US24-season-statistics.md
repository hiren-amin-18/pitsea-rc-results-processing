# US24 - Season Statistics and Runner Season Profiles

**Status:** 📋 Planned

## As a
Race organiser

## I want
Season-level statistics across all events in a year — both per runner and club-wide

## So that
I can recognise consistent runners and clubs at the end of the season, and give individual runners a view of their own year

---

## Background

Once runners are persistent people (US15), results stop being isolated per race and become a season of data per person. This unlocks two views: an individual "my season" profile per runner, and a season dashboard of aggregate awards and trends. The season is the calendar year and spans all event types (Crown to Crown and Bluebell 5), unlike the Champions of Champions competition which is C2C May–September only.

## Runner Season Profile

For a selected runner and season:

1. **Races completed** — count and list (event, date, position, time), out of the events held that year.
2. **Season-best time per event type** — times are only comparable on the same course, so bests are per event type, never mixed.
3. **Average finish position** — overall and within their category.
4. **Improvement curve** — chart of their times per event type across the season; first race vs season best.
5. **Champions of Champions points progression** — cumulative points per race for the season (C2C events only).
6. **Streak** — current run of consecutive events attended.

## Season Dashboard

For a selected season, across all events:

1. **Most attended runner(s)** — runners with the highest event count; "ever-present" badge for those who attended every event.
2. **Most attended club** — clubs ranked by total entries across the season's races (and by unique runners, as a second view).
3. **Fastest in each category** — fastest single time of the season for Male, Female, Male U18, Female U18, shown **per event type**.
4. **Most improved runner** — biggest improvement between first race and season best on the same event type (minimum two races of that type).
5. **Participation trends** — entrants and finishers per event across the season; total unique runners; first-time runners per event.
6. **Season DNF rate** — DNFs as a percentage of starts across the season.
7. **Club championship table** *(optional, club to confirm scoring)* — clubs ranked by accumulated finishing points across the season.

## Acceptance Criteria

1. A season statistics page exists, selectable by year, showing the season dashboard items above.
2. Each runner has a season profile page reachable from the season dashboard and from results views.
3. All cross-event aggregation keys on the runner registry (`RunnerId`), not name matching.
4. Time-based statistics (season bests, fastest in category, most improved) compare only within the same event type and use typed durations; unparseable legacy times are excluded with a visible count.
5. Categories follow the existing scheme (Male, Female, Male U18, Female U18); a runner who turns 18 mid-season is categorised per race by their age at that race.
6. The season dashboard updates automatically as events are completed; no manual recalculation step.
7. Statistics pages are read-only and respect event archiving (US20) — archived events still contribute.
8. Calculations live in the service layer with unit test coverage, including edge cases: single-event seasons, runners with one race, ties for most attended.

## Notes

- **Depends on US15 (runner registry)** — without stable runner identity, every cross-event figure here degrades to name matching.
- **Depends on US17 (typed times)** for items 2–4 of the profile and 3–4 of the dashboard; attendance-based stats (most attended, streaks, trends) need only US15.
- Age-based statistics remain out of scope: ages are only recorded for U18 participants (blank age = adult by convention).
- Pairs naturally with US21 (public results page): runner profiles and the season dashboard are strong candidates for public sharing.
- Tie-breaking conventions (e.g. two ever-present runners) can simply show joint winners; no forced ranking needed.
