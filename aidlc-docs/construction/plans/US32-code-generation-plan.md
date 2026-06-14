# US32 — Automated Roster Allocation — Code Generation Plan

**Story:** [US32](../../../user-stories/US32-roster-auto-allocation.md)
**Type:** Brownfield. No schema change — assignments and preferences already live on `VolunteerAssignment` (US28). The allocator reads the season-to-date history from existing assignments.

## Design

### Input model (organiser flow)

The organiser opens **Allocate Draft** on the roster page. The page shows the volunteer register; the organiser ticks who will be at the event and, for each, ticks their preferences (specific role, run-after, near-finish, can't-walk-far, seated, any-role). They click **Generate draft** and the allocator returns a proposal.

Two new model types:

- `AllocationCandidate` — `VolunteerId`, `PreferredRoleId?`, `WantsToRunAfter`, `WantsNearFinish`, `CantWalkFar`, `WantsSeated`, `AnyRole`.
- `AllocationDraft` — `EventId`, `Proposals: ProposedAssignment[]`, `Report: AllocationReport`.
  - `ProposedAssignment` — `VolunteerId`, `VolunteerRoleId`, `WillRunAfter`, `ReasonCode` (`PrePlaced` | `Eligibility` | `RunAfterRotation` | `Preference` | `Mix` | `Fill`), plus the preference fields so they're preserved when applied.
  - `AllocationReport` — `UnfilledRoles`, `UnplacedCandidates`, `PreferencesNotHonoured`, `Notes`.

### Service

- **`IRosterAllocator` / `RosterAllocator`** — pure stateless function:
  - `Propose(int eventId, IReadOnlyList<AllocationCandidate> candidates) → AllocationDraft`.
  - Reads: roles for the event type, eligibility allow-lists, pre-placed volunteer per role, the season-to-date assignment history (events of this type with `EventDate.Year == target.EventDate.Year` and `EventDate < target.EventDate`).
  - Uses `Volunteer.Gender` and `IsFirstAidTrained` for the constraints.
- **`IRosterDraftApplier` / `RosterDraftApplier`** — persists an approved draft as real `VolunteerAssignment`s via the existing `IVolunteerRosterService.AddAssignmentAsync` (so validation still runs and the warnings surface). Returns an `OperationResult` summary.

### Algorithm (greedy with explicit priority)

For each candidate, compute a "rotation score" = number of times they have done a run-after role this season; a "role recency map" = role → most recent event date for this person.

Then, in this order:

1. **Pre-placements.** For each role with `PrePlacedVolunteerId` set, if that person is among the candidates, assign them. Reduce that role's remaining slots by 1. Reason = `PrePlaced`.
2. **Restricted-role volunteers, restricted-role first.** For each role with `HasEligibilityRestriction = true`, pick candidates from the allow-list, prefer those whose preferences also match. Reason = `Eligibility`.
3. **Run-after rotation.** Iterate roles with `RunAfterCapacity > 0`. For each available run-after slot, pick the candidate who **wants to run after** and has done the fewest run-after slots so far this season (lowest rotation score; tie-break by name). Reason = `RunAfterRotation`.
4. **Honour preferences** (in this sub-order, each step uses any remaining slot in eligible roles):
   - **Specific role** (`PreferredRoleId`).
   - **Can't walk far** → Marshal Point 1 or 2 (fallback Point 6 if needed).
   - **Wants seated** → Number Collection / On The Day Registration.
   - **Wants near finish** → any Finish Area role.
   - First-aid-required roles only accept first-aid-trained volunteers.
   - Reason = `Preference`.
5. **Mix-up across the season.** For remaining candidates without a placement, score each open (role × candidate) pair by avoid-recent-role: candidates whose most recent event in that role is the oldest (or never) score highest. Greedy-fill. Reason = `Mix`.
6. **Marshal gender mix.** When placing in a Marshal Point role with one slot left and only candidates of one gender available, that's fine (best-effort, not a hard rule). When two slots remain at a marshal point and both genders are available among unplaced candidates, prefer a mixed pair. Implemented as a post-pass that swaps marshal-point assignments where doing so improves the mix without breaking other constraints.
7. **Fill remainder.** Candidates still unplaced (typically `AnyRole = true`) drop into any open slot, respecting all hard constraints. Reason = `Fill`.

Anyone who still cannot be placed appears in `UnplacedCandidates` with a reason ("no compatible role left", "ineligible for remaining restricted roles", etc.). Roles still below `DefaultCount` appear in `UnfilledRoles`. Honoured-but-broken preferences appear in `PreferencesNotHonoured`.

### Apply

The Apply step iterates `ProposedAssignment`s and calls `AddAssignmentAsync` for each. Warnings from the roster service (e.g. double-bookings — only possible if the draft and existing assignments overlap) bubble up via `OperationResult.Warnings`. The draft does *not* clear existing assignments; the organiser is expected to remove anything they want replaced manually first.

### Controller + View

- Extend `VolunteerRosterController`:
  - `GET Allocate` — page with volunteer list (multi-select + per-row preference checkboxes) + Generate button.
  - `POST Allocate` — call `IRosterAllocator.Propose`, render draft preview.
  - `POST Apply` — persist via `IRosterDraftApplier`, return to the roster index with a feedback banner.
- Views: `Views/VolunteerRoster/Allocate.cshtml` (form), `Views/VolunteerRoster/Draft.cshtml` (preview + Apply).

### Tests

`RosterAllocatorTests.cs` exercising:

- Pre-place wins: Ian configured at Marshal 7, candidate Ian → placed there even with other preferences.
- Eligibility wins over preference: Lead restricted to Hiren; non-Hiren candidate asking for Lead doesn't get it.
- Run-after rotation: two candidates wanting run-after, one already did one this season → the other gets it.
- Preference honouring: "can't walk far" → Marshal 1/2 (or 6 fallback when 1/2 full).
- First-aid enforcement: non-first-aid candidate never proposed for first-aid role even if asking.
- Mix across season: candidate did Water Table at the previous event; another didn't → other gets it this time.
- Marshal gender mix: when both genders available, a mixed pair is produced.
- Unplaced candidates surfaced when all open roles are incompatible.
- Apply step calls the roster service for each proposal and rolls up the results.

## Steps

- [ ] **Step 1 — Models.** `Models/AllocationModels.cs` with the types above + a `ReasonCode` enum.
- [ ] **Step 2 — Allocator.** `IRosterAllocator` + `RosterAllocator` (read-only — takes a DbContextFactory but only queries).
- [ ] **Step 3 — Applier.** `IRosterDraftApplier` + `RosterDraftApplier`, wrapping the existing `IVolunteerRosterService.AddAssignmentAsync`.
- [ ] **Step 4 — Tests.** `RosterAllocatorTests.cs` covering all eight scenarios above.
- [ ] **Step 5 — Controller + Views.** Extend `VolunteerRosterController` (3 new actions, 2 views), add an "Allocate draft" button on the roster index.
- [ ] **Step 6 — DI.** Register both services as scoped.
- [ ] **Step 7 — Build + docs.** Full suite green; README → all stories implemented; US32 → ✅; audit; implementation summary.

## Story traceability

| AC | Covered by |
|----|------------|
| 1 capture preferences per event | Step 5 (UI), Step 1 (model) |
| 2 honours all rules in priority order | Steps 2, 4 |
| 3 run-after > role mix-up > gender mix; mix-up across whole season | Step 2 |
| 4 draft handed to US28's roster; fully editable | Step 3, 5 |
| 5 reports unfilled / over- / preferences-not-honoured | Step 2, 5 |
| 6 re-run is safe; doesn't overwrite manual edits | Step 3 (Add never deletes) |
| 7 service-layer + unit tests | Steps 2, 4 |
