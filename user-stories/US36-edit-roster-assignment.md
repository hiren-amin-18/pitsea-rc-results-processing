# US36 - Edit Roster Assignments In Place

**Status:** ✅ Complete

## As a
Race organiser

## I want
To edit an existing roster assignment — change its role, toggle "running after", or amend the note — without removing and re-adding it

## So that
Fixing a typo'd note or moving someone from Marshal Point 3 to Marshal Point 5 is one quick action instead of a delete + full re-entry that loses the captured preferences

---

## Background

US28 built the roster with Add and Remove only. The plumbing for update was anticipated
(`VolunteerAssignmentInput.Id` exists; `ValidateAssignmentAsync` takes an `existingId` that is always
passed `null`) but never wired up. Today any correction destroys and recreates the assignment,
re-entering the note and losing the per-event preferences stored on it.

## Acceptance Criteria

1. Each assignment on the roster page has an Edit action opening a small form pre-filled with the
   assignment's current role, "will run after" flag, and note.
2. Saving applies the same validation as Add (role active, event type, first aid, eligibility,
   max count, run-after capacity) but **excludes the assignment being edited** from the capacity
   counts, so re-saving without changes never fails.
3. The volunteer on an assignment cannot be changed — that is a remove + add (deliberate: it is a
   different fact).
4. Per-event preferences captured on the assignment (preferred role, can't walk far, etc.) are
   preserved untouched by an edit.
5. Double-booking warnings still appear when an edit moves someone into a second role they already
   hold elsewhere in the event.

## Notes

- Builds on [[US28-volunteer-roster]]. The `existingId` parameter in
  `VolunteerRosterService.ValidateAssignmentAsync` finally earns its keep.
