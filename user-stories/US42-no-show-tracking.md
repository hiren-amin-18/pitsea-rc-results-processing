# US42 - No-Show Tracking

**Status:** ✅ Complete

## As a
Race organiser

## I want
To mark a roster assignment as a no-show after the event instead of deleting it

## So that
Ballot entries and volunteer stats stay honest without erasing the history that the person was
rostered — and over a season I can see who reliably turns up

---

## Background

US28 AC6 says "edit after the event to record no-shows", which today means deleting the
assignment. That fixes the stats but destroys the record. A flag keeps both truths: they were
rostered, and they didn't come.

## Acceptance Criteria

1. `VolunteerAssignment` gains an `IsNoShow` flag, toggleable from the roster page after (or
   before) the event.
2. No-show assignments are **excluded from volunteer stats and London Marathon ballot entries**
   (US29): they don't count as an event attended, an assignment performed, a run-after, or
   ever-present.
3. No-show assignments stay visible on the roster page, clearly badged.
4. Exports (PDF/Excel) include no-shows struck through / marked "no show", so the printed record
   matches reality.
5. Role fill counts on the roster treat a no-show slot as **not filled** (the colour coding and
   unfilled-role stats reflect who actually stood there).
6. Copy-from-previous-event does not copy the no-show flag (a new event starts clean), but does
   still copy the person (one bad day isn't a ban).
7. The US32 allocator's season history ignores no-show assignments when computing "least recently
   did this role" and run-after fairness.

## Notes

- Builds on [[US28-volunteer-roster]]; corrects [[US29-volunteer-stats]] and feeds honest history
  to [[US32-roster-auto-allocation]].
