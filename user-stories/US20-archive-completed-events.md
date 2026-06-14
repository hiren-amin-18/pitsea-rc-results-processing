# US20 - Archive Completed Events

**Status:** ✅ Complete

## As a
Race organiser

## I want
To mark an event as completed/archived so its results become read-only

## So that
Finalised, published results cannot be accidentally changed by later uploads, edits, or someone forgetting which event is current

---

## Background

Every event is permanently editable. Because uploads always target the *current* event, the most likely accident is uploading a new race's entrant file while last month's event is still selected — which silently wipes that event's finish and timing data.

## Acceptance Criteria

1. An event can be marked **Archived** from the Events page; archiving requires confirmation.
2. Archived events reject all mutating operations: entrant/finish/timing uploads, result edits, and event detail edits. Attempts produce a clear message ("This event is archived. Unarchive it to make changes.").
3. Archived events remain fully viewable: collated results, stats, top 10, DNF list, and PDF/CSV exports all continue to work.
4. Archived events continue to count toward the Champions of Champions leaderboard; archiving does not trigger any recalculation.
5. An archived event cannot be the current event:
   - Archiving the current event prompts the organiser to select (or create) another current event.
   - The "set current" action is disabled for archived events.
6. Unarchiving requires confirmation and restores normal editing.
7. Archived state is visible at a glance on the Events page (badge) and anywhere the event name is shown as current context.
8. Deleting an archived event requires unarchiving first, as an extra guard on the most destructive action.

## Notes

- This pairs with US19: archive when the race is published, back up after archiving.
- A natural workflow nudge (optional): after Champions points are calculated for an event, suggest archiving it.
