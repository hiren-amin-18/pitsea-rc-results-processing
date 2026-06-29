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

- **Specific role** — asks to do a named role, including the generic **Marshal (any point)** sentinel (see below).
- **Run after** — wants to volunteer then run; can only be honoured in a role with run-after capacity (US28).
- **Near the finish area** — prefers a Finish Area role.
- **Can't walk far** — marshal only at Points 1 or 2 (Point 6 if necessary).
- **Wants to be seated** — Number Collection or On The Day Registration.
- **Any role** — no preference; resolved last, once specific requests are placed.

## Role Flags

- **Optional** — Shadow Lead, Photographer, Metal Gate. These are desirable extras but must not be filled until all mandatory roles reach their minimum counts. The allocator defers them to a final pass and notes in the report if they are skipped.
- **Generic preference sentinel** — "Marshal (any point)" is a special role with zero physical slots. A volunteer who selects it is placed in whichever open Marshal Point needs filling most (least-recently-done first), after all specific marshal-point preferences have been honoured. The sentinel never appears in the unfilled-roles report.

## Auto-Allocation Rules

Given the volunteers signed up for an event and their preferences, the system produces a **rough allocation** by applying, in priority order:

1. **Pre-placed fixtures** — Ian is always pre-placed at Marshal Point 7 (with his dog Shane) before allocation begins; if signed up, he fills one of that point's slots automatically and is excluded from the rest of the allocation.
2. **Eligibility constraints** — only eligible volunteers for restricted roles (Lead, Results, first-aid roles), per the role definitions in US28.
3. **Rotate the run-after roles above all else** — roles where a volunteer can run after are the easy option, so spread them across the season first: prefer people who have had fewest run-after slots this year.
4. **Honour specific preferences** — specific role, run-after, near finish, can't-walk-far, seated — subject to the rules above.
   - **4a₂. Generic marshal preference** — volunteers who selected "Marshal (any point)" are placed into the open Marshal Point they've done least recently, after all specific marshal-point preferences are resolved.
5. **Mix up roles across the season** — balance each volunteer's roles across the **whole season to date**, not just versus the previous event; avoid repeating someone's recent role and spread roles so no one dominates (the main directive).
6. **Male/female mix at marshal points** — where possible, pair a mix of genders at each marshal point.
7. **Fill remaining roles** — place "any role" volunteers and anyone left into the unfilled complement.
8. **Finish line dual assignment (C2C only)** — take up to 3 non-runners (WantsToRunAfter = false) from proposals already placed at On The Day Registration or Number Collection and additionally assign them to Finish Line Funnel and Finish Line Results. These slots are **reserved** and excluded from passes 1–7 so they are always covered by OTD/NC non-runners. The minimum is 3 (1 Funnel + 2 Results); if fewer non-runners are available, the report notes the shortfall. Dual-assigned volunteers appear twice in the draft with the second row marked "Finish line (also OTD/NC)".
9. **Optional roles** — once all mandatory minimums are met, any remaining unplaced candidates are placed into Shadow Lead, Photographer, and Metal Gate. If mandatory minimums are not yet met, optional slots are skipped and a report note explains why.

The allocator is **best-effort**: it flags unfilled roles and over-/under-complement roles rather than refusing to produce a roster, and it never invents constraints — if there aren't enough eligible people, it leaves the role short and says so.

## Acceptance Criteria

1. Before an event, volunteers can be signed up with their **preferences** for that event (the preference set above).
2. The system generates a **rough auto-allocation** honouring all the allocation rules in priority order, drawing on the season's assignment history (US28) to drive rotation and mix-up.
3. Pre-placed fixtures (Ian at Marshal 7) and eligibility constraints are applied first; then rotation of the **run-after roles** takes precedence over the general role mix-up, which in turn takes precedence over the gender mix at marshal points. Role mix-up balances across the **whole season to date**.
4. The generated allocation is produced as a **draft handed to US28's roster**, where it is fully editable by the organiser; the allocator never locks an assignment.
5. The allocator **reports** unfilled roles, over-/under-complement roles, any preferences it could not honour, and notes for optional-role deferral and finish-line dual-assignment shortfalls, so the organiser knows what to fix by hand.
6. Re-running the allocation for an event is safe and does not overwrite manual edits without the organiser confirming.
7. The allocation logic lives in the **service layer with thorough unit test coverage** — edge cases: more demand than run-after capacity, an event with no history (first of the season), restricted roles with no eligible volunteer present, all-"any role" sign-ups, marshal points where a gender mix isn't achievable, generic-preference sentinel candidates, and events with too few OTD/NC non-runners to cover finish line.
8. **Optional roles are deferred**: Shadow Lead, Photographer, and Metal Gate are only filled after all mandatory roles reach their MinCount. The draft report notes when optional roles are skipped.
9. **Finish line dual assignment** produces a minimum of 3 dual-assigned records (1 Funnel + 2 Results) from OTD/NC non-runners; the draft shows these with the "Finish line (also OTD/NC)" reason and a distinct highlight.
10. The **"Marshal (any point)"** role appears in the preference drop-down but never as a physical assignment in the draft; volunteers who select it are placed at a real Marshal Point.

## Notes

- **Depends on [[US28-volunteer-roster]]** for the volunteer register, role complement, run-after capacity, eligibility constraints, and the editable roster the draft is handed to.
- Season history that drives rotation/mix-up is the same data [[US29-volunteer-stats]] reports on.
- Bluebell 5 allocation is a separate story.
