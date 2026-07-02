# US37 - Roster Form Fill-State Awareness

**Status:** 📋 Planned

## As a
Race organiser

## I want
The roster's Add-assignment form to show each role's current fill ("Timekeeping (1/2)"), disable
roles already at their maximum, and mark volunteers already assigned at this event

## So that
I stop discovering "role is at its maximum" or accidental double-bookings only after a failed
submit and page round-trip

---

## Background

The Add form's two dropdowns are blind: the role list shows constraints (first aid, restricted) but
not how full each role already is, and the volunteer list gives no hint that someone is already on
the roster. Every mistake costs a full post/redirect cycle.

## Acceptance Criteria

1. The role dropdown shows current assigned count against default, e.g. `Timekeeping (1/2)`, using
   the same roster data the page already loads.
2. Roles at their **MaxCount** are disabled in the dropdown (visible, but not selectable).
3. The generic-preference sentinel ("Marshal (any point)") shows the total open marshal-point spots,
   e.g. `Marshal (any point) (13 spots open)`, and is disabled when no marshal point has space.
4. Volunteers already assigned at this event are marked in the volunteer dropdown, e.g.
   `Pam Smith · assigned`, but remain selectable (double-booking is legal and warned).
5. No behavioural change server-side — this is presentation only; validation stays authoritative.

## Notes

- Builds on [[US28-volunteer-roster]] and the generic marshal auto-fill added after
  [[US32-roster-auto-allocation]].
