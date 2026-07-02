# US43 - Volunteer Register Grooming

**Status:** 📋 Planned

## As a
Race organiser

## I want
The volunteers page to show when each volunteer last volunteered and let me sort by name,
assignment count, or last-volunteered date

## So that
Deciding who to deactivate at season end — and spotting lapsed regulars worth a nudge — takes a
glance instead of opening stats pages per person

---

## Background

The register lists assignment counts but no recency: a volunteer with 40 assignments who stopped
coming two years ago looks more active than a newcomer with 5 this season. Deactivation decisions
need "when did I last see them?".

## Acceptance Criteria

1. The volunteers page gains a **Last volunteered** column: the date (and event name) of the most
   recent event they held a non-no-show assignment at; blank for never-assigned volunteers.
2. Column headers for Name, Assignments, and Last volunteered sort the table; default stays
   name A–Z. Sort is server-side via query string (survives refresh).
3. Assignment counts and last-volunteered both **exclude no-shows** (consistent with US42).
4. Inactive volunteers keep their values when shown via "Show inactive".

## Notes

- Builds on [[US28-volunteer-roster]] and [[US42-no-show-tracking]].
