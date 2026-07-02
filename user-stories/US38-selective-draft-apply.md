# US38 - Selective Draft Apply

**Status:** ✅ Complete

## As a
Race organiser

## I want
To untick individual proposals on the draft roster page before applying it

## So that
When the allocator gets 28 of 30 placements right, I apply the 28 and leave the 2 I disagree with,
instead of applying everything and hunting the wrong ones down on the roster afterwards

---

## Background

The US32 draft page is read-only: Apply takes the whole draft JSON or nothing. The applier
(`RosterDraftApplier`) already validates per proposal, so partial application is natural — the
missing piece is per-row selection in the UI.

## Acceptance Criteria

1. Each proposed assignment row on the draft page has a checkbox, ticked by default.
2. The Apply button applies only ticked proposals, and its count updates live as rows are toggled.
3. Finish-line dual assignments (reason "Finish line") can be unticked independently of the primary
   OTD/NC assignment they derive from.
4. Unticking every row disables Apply.
5. The applied-count feedback reflects what was actually submitted, e.g. "Applied 26 of 26
   proposed assignment(s)" when 4 of 30 were unticked.

## Notes

- Builds on [[US32-roster-auto-allocation]]. No allocator changes — selection happens between
  propose and apply.
