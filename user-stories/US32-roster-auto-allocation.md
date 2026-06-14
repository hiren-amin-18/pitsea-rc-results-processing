# US32 - Automated Roster Allocation

**Status:** ✅ Complete

## As a
Race organiser

## I want
The system to capture each volunteer's preferences for an event and generate a rough role allocation against the club's rules

## So that (main directive)
The volunteering is **mixed up across the year** — no one person does the same role over and over, and no one dominates the easy "volunteer then run after" roles — and I start from a sensible draft instead of a blank roster.

---

## Background

US28 gives us the volunteer register, the role catalogue with its complement, and a roster I can build and edit by hand. This story adds the intelligence on top: take the people signed up for an event, the preferences they've expressed, and the season's history so far, and propose a fair allocation that the organiser then refines.

This is the heaviest, most rule-driven part of the volunteering feature, which is why it's split out. It depends entirely on [[US28-volunteer-roster]] for its data and hands its output back to US28's editable roster.

This story covers **Crown to Crown (C2C)**. Bluebell 5 will need an equivalent allocator with its own complement; separate story, out of scope here.

## Volunteer Preferences (captured per event sign-up)

Volunteers may request things when they sign up for an event. These are recorded per volunteer per event and fed into the allocation:

- **Specific role** — asks to do a named role.
- **Run after** — wants to volunteer then run; can only be honoured in a role with run-after capacity (US28).
- **Near the finish area** — prefers a Finish Area role.
- **Can't walk far** — marshal only at Points 1 or 2 (Point 6 if necessary).
- **Wants to be seated** — Number Collection or On The Day Registration.
- **Any role** — no preference; resolved last, once specific requests are placed.

## Auto-Allocation Rules

Given the volunteers signed up for an event and their preferences, the system produces a **rough allocation** by applying, in priority order:

1. **Pre-placed fixtures** — Ian is always pre-placed at Marshal Point 7 (with his dog Shane) before allocation begins; if signed up, he fills one of that point's slots automatically and is excluded from the rest of the allocation.
2. **Eligibility constraints** — only eligible volunteers for restricted roles (Lead, Results, first-aid roles), per the role definitions in US28.
3. **Rotate the run-after roles above all else** — roles where a volunteer can run after are the easy option, so spread them across the season first: prefer people who have had fewest run-after slots this year.
4. **Honour specific preferences** — specific role, run-after, near finish, can't-walk-far, seated — subject to the rules above.
5. **Mix up roles across the season** — balance each volunteer's roles across the **whole season to date**, not just versus the previous event; avoid repeating someone's recent role and spread roles so no one dominates (the main directive).
6. **Male/female mix at marshal points** — where possible, pair a mix of genders at each marshal point.
7. **Fill remaining roles** — place "any role" volunteers and anyone left into the unfilled complement.

The allocator is **best-effort**: it flags unfilled roles and over-/under-complement roles rather than refusing to produce a roster, and it never invents constraints — if there aren't enough eligible people, it leaves the role short and says so.

## Acceptance Criteria

1. Before an event, volunteers can be signed up with their **preferences** for that event (the preference set above).
2. The system generates a **rough auto-allocation** honouring all the allocation rules in priority order, drawing on the season's assignment history (US28) to drive rotation and mix-up.
3. Pre-placed fixtures (Ian at Marshal 7) and eligibility constraints are applied first; then rotation of the **run-after roles** takes precedence over the general role mix-up, which in turn takes precedence over the gender mix at marshal points. Role mix-up balances across the **whole season to date**.
4. The generated allocation is produced as a **draft handed to US28's roster**, where it is fully editable by the organiser; the allocator never locks an assignment.
5. The allocator **reports** unfilled roles, over-/under-complement roles, and any preferences it could not honour, so the organiser knows what to fix by hand.
6. Re-running the allocation for an event is safe and does not overwrite manual edits without the organiser confirming.
7. The allocation logic lives in the **service layer with thorough unit test coverage** — edge cases: more demand than run-after capacity, an event with no history (first of the season), restricted roles with no eligible volunteer present, all-"any role" sign-ups, and marshal points where a gender mix isn't achievable.

## Notes

- **Depends on [[US28-volunteer-roster]]** for the volunteer register, role complement, run-after capacity, eligibility constraints, and the editable roster the draft is handed to.
- Season history that drives rotation/mix-up is the same data [[US29-volunteer-stats]] reports on.
- Bluebell 5 allocation is a separate story.
