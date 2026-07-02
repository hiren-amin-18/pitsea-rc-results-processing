# US41 - Per-Role Quick Assign

**Status:** 📋 Planned

## As a
Race organiser

## I want
A "+" action on each role row of the roster that opens a picker listing only the volunteers who are
eligible for that role and not yet assigned at this event, letting me tick several and add them in
one submit

## So that
Filling gaps role by role — the natural way to finish a roster — takes one click and a few ticks
per role instead of a two-dropdown round-trip per person

---

## Background

Hand-building a ~30-person roster through the single Add form means ~30 post/redirect cycles, each
re-scanning two long dropdowns. When the organiser works role-first ("who else can do Timekeeping?"),
the form fights them: it is volunteer-first and forgets everything between submits.

## Acceptance Criteria

1. Each role row on the roster page has a "+" action (hidden when the role is at MaxCount).
2. The picker lists only volunteers who are: active, **not already assigned at this event**,
   first-aid trained if the role requires it, and on the allow-list if the role is restricted.
3. Multiple volunteers can be ticked and added in one submit; the number accepted is capped at the
   role's remaining MaxCount headroom, with anything beyond reported.
4. Each added assignment goes through the same validation as single Add; per-row failures are
   reported without blocking the rest.
5. The generic-preference sentinel row shows no "+" (it has no physical slots).

## Notes

- Builds on [[US28-volunteer-roster]] and [[US37-roster-form-fill-awareness]].
- "Not already assigned" is a filter, not a rule change — deliberate double-booking stays possible
  through the main Add form.
