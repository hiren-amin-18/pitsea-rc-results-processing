# US40 - Persistent Volunteer Preferences and Allocate Form Memory

**Status:** 📋 Planned

## As a
Race organiser

## I want
Each volunteer's usual preferences (preferred role, can't walk far, wants to run after, near
finish, seated, Race HQ, any role) stored on their record and pre-filled into the allocate form —
and the allocate form to remember, per event, who I ticked and any per-event overrides

## So that
Setting up the allocator for each event takes seconds ("tick who's coming, generate") instead of
re-entering facts that rarely change, and Back from the draft page no longer wipes the grid

---

## Background

Preferences live only on `VolunteerAssignment`, captured per event from a blank grid. Pam's
inability to walk far is a fact about Pam, not about one Tuesday in June. Worse, the allocate form
is stateless: navigating Back from the draft returns a blank form, so tweaking one preference means
redoing every tick.

## Model

- **Volunteer defaults** — new nullable/boolean columns on `Volunteer` mirroring the seven
  assignment preferences: `DefaultPreferredRoleId`, `DefaultWantsToRunAfter`,
  `DefaultWantsNearFinish`, `DefaultCantWalkFar`, `DefaultWantsSeated`, `DefaultWantsRaceHq`,
  `DefaultAnyRole`.
- **Allocation candidate memory** — new table `AllocationCandidateRecord` (event + volunteer +
  the seven preference fields) storing the last state of the allocate grid for that event.

## Acceptance Criteria

1. Volunteer create/edit captures the seven default preferences (a collapsed "Usual preferences"
   section; all default off/empty).
2. Opening the allocate form for an event with **no saved grid** pre-fills each volunteer's row
   from their defaults, with nobody ticked.
3. Generating a draft **saves the grid** (ticks + per-event preference values) for that event;
   reopening the allocate form (including Back from the draft) restores it exactly.
4. Per-event edits on the grid override defaults for that event only; the volunteer record is
   untouched.
5. Saved grids are per event and removed when their event is deleted.
6. The manual Add-assignment form's collapsed preference section also pre-fills from the selected
   volunteer's defaults.

## Notes

- Builds on [[US32-roster-auto-allocation]]. The allocator itself is unchanged — it still receives
  a candidate list; only where that list comes from improves.
- Preference fields on `VolunteerAssignment` stay as-is: they remain the per-event record that
  US32 reads for history.
