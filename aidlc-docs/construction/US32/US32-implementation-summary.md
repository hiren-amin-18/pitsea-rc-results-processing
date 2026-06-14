# US32 — Automated Roster Allocation — Implementation Summary

**Status:** ✅ Complete — build green, 193 unit + 26 integration = 219/219 tests passing.

## Files changed

**Created**
- `Models/AllocationModels.cs` — `AllocationCandidate`, `ProposedAssignment`, `AllocationReport`, `UnfilledRole`, `UnplacedCandidate`, `AllocationDraft`, `AllocationFormInput`, and the `AllocationReason` enum (`PrePlaced` | `Eligibility` | `RunAfterRotation` | `Preference` | `Mix` | `Fill`).
- `Services/IRosterAllocator.cs` + `RosterAllocator.cs` — stateless greedy rules engine implementing the seven priority steps:
  1. Pre-placements (Ian @ Marshal 7).
  2. Restricted-role allow-list picks.
  3. Run-after rotation — sorted by season-to-date run-after count, fewest first.
  4. Preferences (specific role → can't-walk-far → seated → near-finish).
  5. Mix-up across the season — pick the role each candidate has done least recently (or never) this season.
  6. Marshal gender mix — post-pass swap that frees a slot and replaces with an opposite-gender candidate when one is available.
  7. Fill remainder for "any role" / leftovers.
- `Services/IRosterDraftApplier.cs` + `RosterDraftApplier.cs` — applies an approved draft by calling `IVolunteerRosterService.AddAssignmentAsync` so all the US28 validation (eligibility, first-aid, capacity, run-after capacity, double-booking warning) still runs, and rolls warnings/errors back into a single `OperationResult`.
- `Views/VolunteerRoster/Allocate.cshtml` — picker page (one row per volunteer, preference checkboxes).
- `Views/VolunteerRoster/Draft.cshtml` — proposal preview with reason chips, unfilled-roles and unplaced-candidates reports, and a single Apply button (round-trips the draft as JSON in a hidden field — no extra "draft" table).
- `RaceResults.UnitTests/RosterAllocatorTests.cs` — 10 tests: pre-place wins, restricted-role enforcement (positive + negative), run-after rotation by season-to-date count, can't-walk-far + seated preference placement, first-aid enforcement, mix-up across season, marshal gender mix produces a mixed pair when possible, and Apply persists.

**Modified**
- `Controllers/VolunteerRosterController.cs` — three new actions: `GET Allocate`, `POST Allocate`, `POST Apply`. Uses `System.Text.Json` to round-trip the draft so we don't need a new table.
- `Views/VolunteerRoster/Index.cshtml` — added an "Allocate draft (US32)" button alongside the existing toolbar.
- `Program.cs` — registered `IRosterAllocator` and `IRosterDraftApplier` as scoped.
- `user-stories/US32-roster-auto-allocation.md` — status flipped to ✅.
- `README.md` — moved US32 from Planned to Implemented; updated intro line; planned table now empty.

## Decisions

- **No new schema.** The allocator reads from existing `VolunteerAssignment` + `VolunteerRole` + `VolunteerRoleEligibility` tables. Drafts live only in memory; the Apply step writes real assignments.
- **Single greedy pass with explicit phases** (vs. constraint solver). Priorities are short, rules are testable in isolation, and a club-volume dataset (≤ ~30 candidates) doesn't warrant a SAT solver.
- **Apply re-validates via the roster service.** Means any rule the allocator missed (or any concurrent change since the draft was generated) is still enforced — Apply cannot put the database in an invalid state.
- **JSON round-trip for the draft** instead of a draft table. Lower complexity; the draft is throwaway state and the organiser is expected to review, then Apply or discard.
- **Season window** for rotation/mix-up is `EventDate.Year == target.EventDate.Year && EventDate < target.EventDate` (the season-to-date, your decision). Matches the C2C Good-Friday-to-Boxing-Day window where calendar year ≈ season.
- **Gender mix is best-effort.** It only swaps in a marshal point that has finished allocation as same-gender *and* an opposite-gender unplaced candidate is available who is eligible (constraint-wise) for that role. Avoids cascading swaps that would unwind other rules.

## Acceptance criteria — all met (1–7).
