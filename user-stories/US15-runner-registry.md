# US15 - Runner Registry

**Status:** ✅ Complete

## As a
Race organiser

## I want
Runners to be stored as persistent people that per-event entrants link to

## So that
A runner's results are tracked reliably across events and seasons, instead of being inferred by matching names between events

---

## Background

Entrants are currently stored per event and recreated on every upload. Cross-event features (Champions of Champions) identify the same person by a normalised name + club string match. This is fragile:

- A typo in one race ("Jon Smith" vs "John Smith") silently splits a runner's season points.
- A mid-season club change splits a runner into two leaderboard entries.
- There is no way to view one person's history across races.

## Acceptance Criteria

1. A `Runner` entity exists with: name, club, gender, date of birth or age band, and optional external reference (e.g. EA number).
2. Each `Entrant` row links to a `Runner` (`RunnerId` foreign key); bib numbers remain per-event.
3. During entrant upload, each parsed entrant is matched against existing runners:
   - Exact match on normalised name + club links automatically.
   - Near matches (e.g. same name, different club; small edit distance) are reported as warnings for review.
   - No match creates a new runner.
4. A runner management UI lists all runners with their race count and supports:
   - Editing a runner's details.
   - Merging two runners into one (for typos/duplicates), with confirmation.
5. Champions of Champions scoring and aggregation key on `RunnerId` instead of normalised name + club.
6. Merging or editing runners automatically recalculates the Champions leaderboard for affected seasons.
7. Deleting an event does not delete runners; a runner with no remaining entrants may be flagged as inactive rather than removed.
8. Existing data is migrated: one runner is created per distinct normalised name + club across all events, and entrants are linked accordingly.

## Notes

- This story is the structural prerequisite for per-runner history pages, personal bests, and reliable multi-season Champions comparisons.
- Matching should be assistive, never silently destructive: ambiguous matches go to the organiser for a decision.
