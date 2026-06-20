# US34 - Bluebell 5 Volunteer Roster & Allocation

**Status:** ✅ Complete

## As a
Race organiser

## I want
The same volunteer roster and rules-based draft allocation we use for Crown to Crown, extended to Bluebell 5 with its own role complement and a smaller preference set

## So that
Bluebell 5 gets the same fair, mixed-up volunteer planning C2C does — without forcing C2C's larger role catalogue or preference set onto a much smaller event.

---

## Background

[[US28-volunteer-roster]] and [[US32-roster-auto-allocation]] built the volunteer register, role catalogue, manual roster, and the rules-based draft allocator for **C2C**. Bluebell 5 has fewer roles, a different layout (Race HQ + Start/Finish, no marshal points along the course), and volunteers there ask for less.

This story is a thin extension, not a rebuild: reuse the volunteer register, the Assignment model, the manual roster UI, the export, and the US32 allocator engine. What's new is a **separate Bluebell role list** and a **smaller preference set**. Bluebell and C2C share the **same season fairness pool** so a volunteer's run-after slots and role mix are balanced across both events together — the same picture US29 already reports and the London Marathon ballot already counts.

Because Bluebell's surface is small and the allocator engine is reused, the data seed and the allocator extension ship together as one story rather than being split like C2C's US28/US32.

## Bluebell 5 Role Complement (default seed)

A **separate Bluebell role list**, distinct from C2C's. Roles with the same name (e.g. Timekeeping, Photographer, Water Table) are deliberately separate Role records so each event's complement, run-after capacity, and optional flag can evolve independently. Volunteers are drawn from the **same shared register**.

### Race HQ
| Role | People | Notes |
|---|---|---|
| Number Pick Up | 6 | Some can run after |
| On The Day Registration | 1–2 | Some can run after; may double up on Refreshments once registration closes |
| Refreshments | 2–3 | |
| Bag Drop | 1 | Optional |
| Car Park Marshal | 3–4 | Some can run after |

### Start/Finish Area
| Role | People | Notes |
|---|---|---|
| Lead | 1 | Same eligibility as C2C: Hiren Amin or Michael Groombridge |
| Results | 1 | Same eligibility as C2C: Hiren Amin |
| Timekeeping | 2 | |
| Finish Line Funnel | 1–2 | |
| Finish Line Results | 2 | |
| Tail Walker | 2 | |
| Water Table | 4 | |
| Photographer | 1 | Optional |
| Finish Help | 1 | |

### Transport
| Role | People | Notes |
|---|---|---|
| Van Driver | 1 | |

**Run-after capacity** applies only to the **Race HQ** roles — Number Pick Up, On The Day Registration, and Car Park Marshal. Start/Finish and Transport roles are not run-after eligible.

**Eligibility constraints**: Lead and Results carry the same eligibility constraints as their C2C equivalents (editable as people change). No first-aid-required roles at Bluebell. No pre-placed volunteers seeded by default.

No course/marshal-point roles — Bluebell 5 doesn't use them.

## Volunteer Preferences (Bluebell-specific subset)

Volunteers signing up for a Bluebell event may request one of:

- **Run after** — wants to volunteer then run; can only be honoured in a role with run-after capacity (i.e. one of the three Race HQ roles above).
- **Start/Finish area** — prefers a Start/Finish role.
- **Registration / Refreshments area** — prefers a Race HQ desk role (Number Pick Up, On The Day Registration, or Refreshments).
- **Any role** — no preference; resolved last. May still be combined with "run after".

No "specific role", "near finish" vs "can't walk far" vs "wants to be seated" distinctions — Bluebell's layout doesn't make those meaningful.

## Acceptance Criteria

1. Bluebell 5 events get a roster page using the same UI and Assignment model as C2C, scoped to the event.
2. A **separate Bluebell role catalogue** is seeded from the complement above — categories Race HQ / Start-Finish / Transport, default counts, min/max overrides, optional flags, and run-after capacity on the three Race HQ roles only.
3. Lead and Results carry the same eligibility constraints as their C2C counterparts; constraints remain editable per-role.
4. Volunteers are drawn from the **existing shared register** — no separate Bluebell register, no duplicate Volunteer records.
5. The auto-allocator (US32 engine) runs against the Bluebell complement using the **Bluebell preference set only**; the rule priority order from US32 (pre-placed → eligibility → run-after rotation → preferences → season mix-up → fill) is unchanged.
6. Run-after rotation and season role mix-up consider **Bluebell + C2C history together as one season fairness pool**, so a volunteer who has done run-after at C2C is rotated down at Bluebell.
7. For season role mix-up, roles are matched **by name** across event types — Timekeeping, Water Table, Photographer, Tail Walker, Finish Line Funnel, and Finish Line Results are treated as the same role whether worked at C2C or Bluebell, so a volunteer who timed at C2C last weekend is rotated off Timekeeping at Bluebell. The underlying Role records stay separate so each event's complement, run-after capacity, and optional flag can diverge.
8. Manual roster editing, retrospective entry, post-event corrections, double-booking warnings, copy-from-previous-event (most recent **Bluebell** event), and Excel / PDF / print export all work for Bluebell exactly as for C2C.
9. [[US29-volunteer-stats]] includes Bluebell assignments in the per-volunteer totals, member/non-member breakdowns, and London Marathon ballot count (one entry per volunteering instance, irrespective of event).
10. Allocator unit tests cover the Bluebell-specific edges: a sign-up with no preferences at all; more "run after" requests than the three Race HQ roles can absorb; a Bluebell event run in the same season as several C2C events (history pool exercised); Lead/Results eligible person absent.

## Notes

- **Depends on [[US28-volunteer-roster]]** and **[[US32-roster-auto-allocation]]** — reuses the register, Assignment model, roster UI, export, and allocator engine. Only adds the Bluebell role seed and the smaller preference set.
- **Feeds [[US29-volunteer-stats]]** — Bluebell assignments flow into the same totals and ballot count as C2C.
- Out of scope: volunteer self-service sign-up (consistent with US28); Bluebell-specific course/marshal roles (Bluebell doesn't use them).
