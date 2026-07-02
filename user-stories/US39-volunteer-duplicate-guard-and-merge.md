# US39 - Volunteer Duplicate Guard and Merge

**Status:** 📋 Planned

## As a
Race organiser

## I want
A warning when I create a volunteer whose name matches an existing record, and a merge tool that
combines two volunteer records into one

## So that
"Dave Smith" doesn't silently exist twice with his season stats and London Marathon ballot entries
split between the two records — and when it happens anyway, I can repair it without SQL

---

## Background

`VolunteerRegistryService.CreateAsync` has no same-name check (the role service does). Duplicates
are invisible until stats look wrong, and today the only fix is manual database surgery because
assignments, eligibility rows, and pre-placements all point at volunteer IDs.

## Acceptance Criteria

1. Creating a volunteer whose trimmed, case-insensitive name matches an existing record (active or
   inactive) **succeeds with a warning** naming the existing record(s). Genuine namesakes remain
   possible.
2. A merge action lets the organiser pick a **survivor** and a **duplicate**; the duplicate's roster
   assignments, role-eligibility entries, and any role pre-placements are re-pointed at the
   survivor, then the duplicate record is deleted.
3. If merging would create the same volunteer twice in one role at one event, the colliding
   duplicate assignment is dropped (not doubled) and reported in the result summary.
4. Survivor's own fields (contact, flags, runner link) are kept; where the survivor's field is
   empty and the duplicate's is not (email, phone, gender, runner link), the duplicate's value is
   adopted. First-aid flag becomes the OR of the two.
5. Merge is confirmed with a clear irreversibility prompt and reports what moved:
   "Moved 12 assignment(s), 1 eligibility entr(ies); dropped 1 duplicate assignment."

## Notes

- Builds on [[US28-volunteer-roster]]; protects [[US29-volunteer-stats]] ballot counts.
