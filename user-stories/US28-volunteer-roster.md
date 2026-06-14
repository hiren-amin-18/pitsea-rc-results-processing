# US28 - Volunteer Roster Builder

**Status:** ✅ Complete

## As a
Race organiser

## I want
To register volunteers and build a volunteer roster for each event — assigning people to roles by hand, retrospectively for past events, and adjustable afterwards — then export it to share before race day

## So that
Every race has its marshal points, finish area, and course roles covered, and I have a roster I can export, share, and correct after the event

---

## Background

Races depend on volunteers as much as runners, but the app currently models only runners. Organisers track volunteer assignments separately (spreadsheets, messages); bringing the roster into the app puts it alongside the event it belongs to and unlocks volunteer recognition stats (US29) — which matter more than ever now that each volunteering instance earns a Pitsea RC London Marathon ballot entry.

This story is the **data foundation and manual workflow**: the volunteer register, the role catalogue and complement, and a roster the organiser builds and edits by hand. The rules-based **automatic allocation** that proposes a draft roster from volunteer preferences and season history is split into [[US32-roster-auto-allocation]].

This story covers **Crown to Crown (C2C)**. Bluebell 5 needs a similar system with its own role complement; that is defined in a separate user story and is out of scope here.

## Roster Model

- **Volunteer** — a persistent person with:
  - Name, optional contact details.
  - Optional link to a runner record (US15). Many volunteers also run; some are runners who **may never run again**; some never run at all. The link is always optional.
  - **Club member flag** — most volunteers are Pitsea RC members, but some are not (a member's wife or son, or Ian — and his dog Shane). The model must not require club membership, a runner record, or any registration data: a name alone is enough. The flag drives member/non-member breakdowns in US29 but never excludes anyone.
  - **First aid trained flag** — only a select number of volunteers are first aid trained. Stored against the record and used to enforce first-aid roles (see role complement).
  - **Gender** — recorded so the allocator (US32) can aim for a male/female mix at marshal points.
  - Active flag — deactivate rather than delete, to preserve history.
- **Role** — club-defined and configurable, not hard-coded. Each role carries:
  - Category (Leadership, Finish Area, Course).
  - Default complement (how many people) with optional **min/max override** — additional people are sometimes allowed for a role, but this is a deliberate override of the default.
  - Optional flag (role isn't always needed, e.g. Metal Gate, Photographer, Shadow Lead).
  - **"Can run after" capacity** — how many people in this role are permitted to run after their duty (e.g. Number Collection: 1 of 2; On The Day Registration: 2 of 4).
  - **First-aid-required flag** — role can only be filled by a first-aid-trained volunteer.
  - **Eligibility constraint** — optional restriction to specific volunteers (e.g. Lead = Hiren Amin or Michael Groombridge; Results = Hiren Amin). Constraints are editable as people change.
  - **Pre-placed volunteer** — optional standing assignment of a specific person to a role (e.g. Ian is always at Marshal Point 7 with his dog Shane). The allocator (US32) pins them automatically; editable as people change.
  - **Source-pool** — some roles are drawn from the volunteers already assigned to other roles (e.g. Finish Line Funnel and Finish Line Results come from the Number Collection / On The Day Registration people).
- **Assignment** — volunteer + role + event, with:
  - Optional note (e.g. marshal point location).
  - **"Running after" flag** — marks that this volunteer will run once their duty ends.

## C2C Role Complement (default seed)

The default set the allocator targets and that US29 measures "unfilled vs usual complement" against. Counts are defaults; the min/max override allows more where needed.

### Leadership
| Role | People | Notes |
|---|---|---|
| Lead | 1 | Currently only Hiren Amin or Michael Groombridge (editable) |
| Shadow Lead | 1 | Optional |
| Results | 1 | Currently only Hiren Amin (editable) |

### Finish Area
| Role | People | Notes |
|---|---|---|
| Timekeeping | 2 | |
| Course Setup | 2 | |
| Number Collection | 1–2 | If 2, one may run after |
| On The Day Registration | 4 | 2 of the 4 may run after |
| Finish Line Funnel | 1 | Drawn from Number Collection / On The Day Registration |
| Finish Line Results | 2 | Drawn from Number Collection / On The Day Registration |
| First Aid and Prizes | 1 | Must be first aid trained; also presents prizes |
| Tail Runners | 2 | |
| Photographer | 1 | Optional |
| Water Table | 2 | |

### Course
| Role | People | Notes |
|---|---|---|
| Marshal Point 1 | 2 | "Can't walk far" preference can be placed here |
| Marshal Point 2 | 2 | "Can't walk far" preference can be placed here |
| Marshal Point 3 | 2 | |
| Marshal Point 4 | 3 | |
| Marshal Point 5 | 2 | |
| Marshal Point 5a | 2 | |
| Marshal Point 6 | 2 | Fallback for "can't walk far" if necessary |
| Marshal Point 7 | 2 | One slot pre-placed to Ian (with his dog Shane); Ian is not a Pitsea RC member |
| Metal Gate | 1 | Optional, rarely needed |
| First Aid On Course | 1 | Must be first aid trained |

## Acceptance Criteria

1. A volunteer register exists: add, edit, and deactivate volunteers, capturing name, optional contact, optional runner link, **club member flag, first aid trained flag, and gender**.
2. Roles are manageable by the organiser (add/rename/retire/reorder), seeded with the C2C complement above, including each role's default count, min/max override, optional flag, run-after capacity, first-aid requirement, eligibility constraint, and any pre-placed volunteer (e.g. Ian at Marshal 7).
3. Each event has a roster page where the organiser can **assign volunteers to roles by hand**, with multiple volunteers per role and an optional note per assignment, marking any volunteer who is running after.
4. The roster is **fully editable** — reassign, add, or remove anyone — including overriding default complements within a role's min/max.
5. **Retrospective entry**: organisers can add volunteers and assignments manually to events that have already happened.
6. The roster can be **edited after the event** to record no-shows and last-minute additions, keeping US29 stats accurate.
7. The roster warns about double-booking (same volunteer assigned twice in one event) but allows it deliberately (e.g. Number Collection then Finish Line Funnel).
8. The roster is **exportable to Excel and PDF** (plus a print-friendly page) for race-day briefing, showing category, role, name, notes, and which volunteers are running after.
9. Copy-from-previous-event: a new event's roster can be pre-filled from the most recent C2C event, then adjusted.
10. Roster data is scoped to its event and survives event archiving (US20); deleting an event removes its assignments but never the volunteers themselves.
11. The roster accepts a draft allocation produced by [[US32-roster-auto-allocation]] and lets the organiser edit it freely; that automatic generation is defined in US32, not here.

## Notes

- A volunteer optionally linking to a runner record anticipates [[US15-runner-registry]]; until US15 lands, the link can be by name or omitted.
- Provides the data foundation for [[US29-volunteer-stats]] (including the **London Marathon ballot** count, one entry per volunteering instance) and for [[US32-roster-auto-allocation]] (the rules-based draft allocation).
- Bluebell 5 will need an equivalent roster with its own role complement — separate user story, out of scope here.
- Out of scope: volunteer self-service sign-up (would require hosting + accounts, which the club has decided against); this story is organiser-managed only.
