# US28 - Volunteer Roster Builder

**Status:** 📋 Planned

## As a
Race organiser

## I want
To build a volunteer roster for each event, assigning people to roles

## So that
Every race has its marshal points, timekeeping, registration, and funnel positions covered, with a roster I can share before race day

---

## Background

Races depend on volunteers as much as runners, but the app currently models only runners. Organisers track volunteer assignments separately (spreadsheets, messages); bringing the roster into the app puts it alongside the event it belongs to and unlocks volunteer recognition stats (US29).

## Roster Model

- **Volunteer** — a persistent person: name, optional contact details, optional link to a runner record (many volunteers also run; see US15).
  - **Volunteers are not necessarily club members.** Family members and friends of the club regularly help (a member's wife or son, or Ian — and his dog Shane). The model must not require club membership, a runner record, or any registration data: a name alone is enough to put someone on the roster. An optional "club member" flag supports member/non-member breakdowns in US29.
- **Role** — club-defined list, e.g. Race Director, Marshal, Timekeeper, Funnel, Registration, First Aid, Tail Runner. Roles are configurable, not hard-coded.
- **Assignment** — volunteer + role + event, with an optional note (e.g. marshal point location "Gate 3").

## Acceptance Criteria

1. A volunteer register exists: add, edit, and deactivate volunteers (deactivate rather than delete, to preserve history).
2. Roles are manageable by the organiser (add/rename/retire), seeded with a sensible default set.
3. Each event has a roster page: assign volunteers to roles, multiple volunteers per role, optional note per assignment.
4. The roster warns about double-booking (same volunteer assigned twice in one event) but allows it deliberately (e.g. registration then funnel).
5. The roster is printable/exportable (PDF or print-friendly page) for race-day briefing, including role, name, and notes.
6. Copy-from-previous-event: a new event's roster can be pre-filled from the most recent event of the same type, then adjusted.
7. Roster data is scoped to its event and survives event archiving (US20); deleting an event removes its assignments but never the volunteers themselves.

## Notes

- A volunteer optionally linking to a runner record anticipates [[US15-runner-registry]]; until US15 lands, the link can be by name or simply omitted.
- Provides the data foundation for [[US29-volunteer-stats]].
- Out of scope: volunteer self-service sign-up (would require US26 cloud hosting + accounts); this story is organiser-managed only.
