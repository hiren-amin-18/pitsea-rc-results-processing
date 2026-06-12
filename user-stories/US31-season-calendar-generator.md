# US31 - Season Calendar Generator

**Status:** 📋 Planned

## As a
Race organiser

## I want
The year's Crown to Crown events generated automatically from the club's fixed date rules

## So that
Setting up a new season is one action instead of creating eight events by hand, and fixture dates are always rule-correct

---

## Background

The Crown to Crown series follows fixed date rules each year:

| Fixture | Rule | Start time |
|---|---|---|
| First event | Good Friday (moves with Easter) | 11:00 |
| May–August | Second Wednesday of each month | 19:30 |
| September | First **or** second Wednesday — decided per year | 19:00 |
| Final event | Boxing Day (26 December) | 11:00 |

Bluebell 5 has no fixed rule (around April/May, varies per year) and remains manually created.

Events are currently created one at a time via the Events page, and store only a date — start times are not modelled.

## Acceptance Criteria

1. A "Generate season" action on the Events page takes a year and creates the season's Crown to Crown events:
   - Good Friday (computed for that year — e.g. via the standard Easter algorithm)
   - Second Wednesday of May, June, July, and August
   - September event per the organiser's choice (a first-vs-second Wednesday prompt during generation)
   - Boxing Day
2. Generated events follow a consistent naming convention (e.g. "Crown to Crown – May 2027"), editable afterwards like any event.
3. An optional start time field is added to events, populated by the generator (11:00 / 19:30 / 19:00) and editable; existing events default to no time. Where present, the time appears on the Events page and event displays, but the results PDF title format is unchanged.
4. Generation is idempotent and safe: it previews the fixtures before creating, skips dates where a C2C event already exists (warning rather than duplicate), and never modifies existing events.
5. Generated events behave identically to manually created events everywhere (uploads, results, Champions scoring window rules, archiving).
6. The current-event selection is unchanged by generation (no auto-switch).
7. The Good Friday and second-Wednesday calculations are unit tested across multiple years, including edge years where Easter falls in March.

## Notes

- The Champions May–September scoring window is untouched: generated Good Friday and Boxing Day events are real fixtures that earn no Champions points, as today.
- Easter calculation should use a well-known algorithm (e.g. Anonymous Gregorian/Meeus) rather than a lookup table, so any year works.
- Pairs with [[US20-archive-completed-events]] (archive last season, generate the next) and feeds [[US30-end-of-season-review]]'s season definition.
- Bluebell 5 is intentionally out of scope for generation; the existing manual creation flow covers it.
