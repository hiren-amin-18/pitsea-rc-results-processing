# US34 — Bluebell 5 Volunteer Roster & Allocation — Code Generation Plan

**Story:** [US34](../../../user-stories/US34-bluebell-volunteer-roster.md)
**Type:** Brownfield. Reuses US28/US32 plumbing end-to-end. Schema gains two new `RoleCategory` enum values + one new `VolunteerAssignment.WantsRaceHq` column + one new `AllocationCandidate.WantsRaceHq` field. New seed data for Bluebell roles. Allocator updated to (a) pool season history across both event types and (b) match roles by name for season role-mix-up.

## Design

### Schema

- `RoleCategory` enum gains `RaceHq = 3` and `Transport = 4`. Existing `FinishArea` is reused for Bluebell Start/Finish roles. `Course` remains C2C-only.
- `VolunteerAssignment.WantsRaceHq` (bool) added so the Bluebell preference is preserved on apply, mirroring the existing C2C preference columns. Default `false` for the migration.
- New EF migration: `AddBluebellRosterSeed`. Seeds 14 Bluebell roles with IDs 24-37 (continuing from the C2C seed at 23).

### Bluebell role seed (`SeedBluebellRoles`)

Single new method on `RaceResultsDbContext`, called from the existing `VolunteerRole` `HasData` block alongside `SeedC2CRoles()`.

| Id | Category | Name | Default | Min | Max | Optional | RunAfter | Restricted |
|---|---|---|---|---|---|---|---|---|
| 24 | RaceHq | Number Pick Up | 6 | 4 | 6 | | 3 | |
| 25 | RaceHq | On The Day Registration | 2 | 1 | 2 | | 1 | |
| 26 | RaceHq | Refreshments | 3 | 2 | 3 | | 0 | |
| 27 | RaceHq | Bag Drop | 1 | 0 | 1 | ✓ | 0 | |
| 28 | RaceHq | Car Park Marshal | 4 | 3 | 4 | | 2 | |
| 29 | Leadership | Lead | 1 | 1 | 1 | | 0 | ✓ |
| 30 | Leadership | Results | 1 | 1 | 1 | | 0 | ✓ |
| 31 | FinishArea | Timekeeping | 2 | 2 | 2 | | 0 | |
| 32 | FinishArea | Finish Line Funnel | 2 | 1 | 2 | | 0 | |
| 33 | FinishArea | Finish Line Results | 2 | 2 | 2 | | 0 | |
| 34 | FinishArea | Tail Walker | 2 | 2 | 2 | | 0 | |
| 35 | FinishArea | Water Table | 4 | 4 | 4 | | 0 | |
| 36 | FinishArea | Photographer | 1 | 0 | 1 | ✓ | 0 | |
| 37 | FinishArea | Finish Help | 1 | 1 | 1 | | 0 | |
| 38 | Transport | Van Driver | 1 | 1 | 1 | | 0 | |

Lead and Results restricted with no eligibility entries seeded (matches C2C behaviour from US28; organiser populates via the existing roles UI).

### Allocator changes (`RosterAllocator`)

Two functional changes:

1. **Cross-event season history.** Drop the `x.EventType == raceEvent.EventType` filter from the `seasonHistory` query so a volunteer's run-after and role activity at *any* event in the calendar year is considered. Run-after rotation already keys off the existing `WillRunAfter` flag — no logic change beyond removing that filter.
2. **Role mix-up by name.** Build a second map alongside `recentRoleByVolunteer`:
   - `recentRoleNameByVolunteer: Dictionary<VolunteerId, Dictionary<string, DateTime>>` keyed by **role name** (lower-cased + trimmed), so "Timekeeping" at C2C and "Timekeeping" at Bluebell collapse to one bucket.
   - The mix-up score in Steps 5 and 7 of the allocator switches from "most recent date this `RoleId`" to "most recent date this **role name**". `roles` is the Bluebell catalogue (already event-type-scoped), so the names looked up are Bluebell names; the lookup finds matches from C2C history with the same name.
3. **Bluebell preference handling.** Add `WantsRaceHq` to `AllocationCandidate` and `ProposedAssignment`. New Step 4e in `Propose`: for each unplaced candidate with `WantsRaceHq`, pick the first open role in `RoleCategory.RaceHq` they can fit (run-after honoured if requested). Reason = `Preference`. Existing C2C-only preference branches (specific role, can't-walk-far, seated, near-finish) remain unchanged — Bluebell forms simply don't set them.

The marshal gender-mix pass (Step 6) is keyed off role names starting with "Marshal Point" — a no-op on Bluebell. Left in place.

### Applier (`RosterDraftApplier`)

Carry the new `WantsRaceHq` flag through `VolunteerAssignmentInput` so it lands on the persisted `VolunteerAssignment`.

### Allocate UI (`Views/VolunteerRoster/Allocate.cshtml`)

One form, conditional on `raceEvent.EventType`:

- **Crown to Crown** (today): Preferred role / Run after / Near finish / Can't walk far / Seated / Any role.
- **Bluebell 5**: Run after / Start-Finish / Race HQ / Any role. "Preferred role" dropdown, "Can't walk far", "Seated" hidden. "Near finish" checkbox repurposed as "Start-Finish" (same underlying `WantsNearFinish` field). New "Race HQ" checkbox binds `WantsRaceHq`.

### Tests (`RosterAllocatorTests.cs`)

New scenarios (also creates a Bluebell event in the in-memory DB):

- Bluebell run-after rotation prefers a candidate with no run-after history this season — including history from a *Crown to Crown* event in the same year (pool-together check).
- Bluebell role mix-up matches by name: candidate did Timekeeping at C2C → at Bluebell, allocator prefers a different role for them when both are open.
- `WantsRaceHq` routes the candidate to a Race HQ role; `WantsNearFinish` (start-finish) routes to a FinishArea role.
- Run-after capacity at Bluebell is honoured only on Race HQ roles (Number Pick Up / On The Day Registration / Car Park Marshal), never on Start/Finish or Transport.
- `Apply` persists `WantsRaceHq` to `VolunteerAssignment`.
- Smoke: `_allocator.Propose` against a Bluebell event with the full role seed fills every required role given enough candidates.

### Test helpers

`GetRole(string)` currently filters by `EventType.CrownToCrown`. Add `GetRole(string, EventType)` overload (default `CrownToCrown`) so existing tests stay green and new Bluebell tests can target Bluebell roles by name.

### Out of scope

- The roster `Index` page and exports already key off `roster.Event.EventType` and `VolunteerRoleService.GetRoles(eventType)`, so they automatically show the Bluebell role list when viewing a Bluebell event — no view changes required there.
- US29 stats already sum across all assignments regardless of event type — no change.
- Copy-from-previous-event already filters by `EventType`, so a Bluebell event copies the most recent Bluebell roster automatically — no change.

## Steps

- [x] **Step 1 — Schema.** Add `RaceHq` and `Transport` to `RoleCategory`. Add `WantsRaceHq` to `VolunteerAssignment` and `VolunteerAssignmentInput`. Add `SeedBluebellRoles()` to `RaceResultsDbContext` and call it alongside `SeedC2CRoles()` in the `VolunteerRole` `HasData`. Create EF migration `AddBluebellRosterSeed`.
- [x] **Step 2 — Allocator.** Add `WantsRaceHq` to `AllocationCandidate` and `ProposedAssignment`. Update `RosterAllocator.Propose` to: drop the event-type filter on `seasonHistory`; build a `recentRoleNameByVolunteer` map; switch Steps 5/7 mix-up to that map; add Step 4e Race-HQ preference branch.
- [x] **Step 3 — Applier.** Carry `WantsRaceHq` through `RosterDraftApplier.ApplyAsync` so the persisted `VolunteerAssignment` records it.
- [x] **Step 4 — UI.** Update `Views/VolunteerRoster/Allocate.cshtml` to switch column set by event type. Keep the C2C layout untouched.
- [x] **Step 5 — Tests.** Add Bluebell scenarios to `RosterAllocatorTests.cs`. Update the `GetRole` helper.
- [x] **Step 6 — Build + docs.** `dotnet build` + `dotnet test` green. Update README. Mark US34 ✅ Complete. Write implementation summary at `aidlc-docs/construction/US34/US34-implementation-summary.md`. Append audit entry.

## Story traceability

| AC | Covered by |
|----|------------|
| 1 Bluebell event uses same roster UI | (No change — existing roster page is event-type aware) |
| 2 Separate Bluebell role catalogue seeded with the listed complement | Step 1 (`SeedBluebellRoles`) |
| 3 Lead/Results restricted same as C2C | Step 1 (restricted=true, empty allow-list) |
| 4 Volunteers drawn from shared register | (No change — single `Volunteer` table) |
| 5 Allocator runs against Bluebell complement with Bluebell preference set | Steps 2, 4 |
| 6 Run-after rotation + mix-up pool Bluebell + C2C history | Step 2 (drop event-type filter on history query) |
| 7 Role mix-up matches by name across events | Step 2 (`recentRoleNameByVolunteer` map) |
| 8 Manual edit / retrospective / copy / export work as for C2C | (No change — all event-type-scoped already) |
| 9 US29 includes Bluebell | (No change — stats already cross event types) |
| 10 Allocator unit tests | Step 5 |
