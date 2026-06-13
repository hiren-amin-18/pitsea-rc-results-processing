# US22 - Course Records Management

**Status:** ✅ Complete

## As a
Race organiser

## I want
Course records stored per event type and checked automatically against each race's results

## So that
The results PDF always shows accurate records for both Crown to Crown and Bluebell 5, and a new record is celebrated the day it happens

---

## Background

The results PDF currently hard-codes one line of Crown to Crown course records ("15:25 Adam Hickey (August 2013), 18:01 Jessica Judd (December 2015)") in `RaceResultsService`. Bluebell 5 events have no records at all, and a broken record would require a code change to update.

## Course Record Model

Records are held per **event type** and per **record category**:

- Male, Female, Male U18, Female U18 (matching the existing category scheme)
- Each record stores: time, runner name, club, event name, and event date

## Acceptance Criteria

1. Course records are stored in the database per event type and category, with a management UI (Settings or Events area) to view and edit them.
2. Existing Crown to Crown records (Adam Hickey 15:25, Jessica Judd 18:01) are seeded by migration; Bluebell 5 starts empty.
3. The results PDF renders the records line from stored data for the current event's type; if no records exist for that type, the line is omitted (current Bluebell 5 behaviour is preserved).
4. After timings are uploaded or a result is edited, each category winner's time is compared against the stored record for that event type:
   - A faster time raises a prominent notification to the organiser ("New course record: ...").
   - The organiser confirms before the stored record is updated (guarding against data-entry errors and short-course situations).
5. A confirmed new record appears on the results PDF flagged as "NEW COURSE RECORD" for that event.
6. Record history is retained (previous record holders are not overwritten silently — superseded records remain queryable).
7. Record comparison uses typed durations, not string comparison.

## Notes

- **Depends on US17** (time validation and typed durations); string times cannot be compared reliably.
- U18 records may not exist historically; the UI should allow a category to have no record yet.
- Out of scope: age-group records beyond the four existing categories.
