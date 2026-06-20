# US34 — Bluebell 5 Volunteer Roster & Allocation — Implementation Summary

**Story:** [US34](../../../user-stories/US34-bluebell-volunteer-roster.md)
**Plan:** [US34-code-generation-plan.md](../plans/US34-code-generation-plan.md)
**Status:** ✅ Complete
**Build:** Success
**Tests:** Pass (221 unit + 26 integration = 247)

## What changed

### Schema
- `RoleCategory` enum gains `RaceHq = 3` and `Transport = 4`. `FinishArea` is reused for Bluebell Start/Finish roles; `Course` remains C2C-only.
- `VolunteerAssignment.WantsRaceHq` (bool, default false) — carries the Bluebell-only "Registration / Refreshments area" preference through to the persisted assignment alongside the existing C2C preference columns.
- Migration: `20260620_AddBluebellRosterSeed` adds the column and seeds the 15 Bluebell roles (IDs 24-38).

### Seed
- `RaceResultsDbContext.SeedBluebellRoles()` returns the Bluebell role catalogue: 5 Race HQ roles, 2 Leadership (restricted, empty allow-list), 7 Start/Finish, 1 Transport. Run-after capacity only on Number Pick Up (3), On The Day Registration (1), and Car Park Marshal (2).

### Allocator (`RosterAllocator`)
- **Cross-event season history pool.** Dropped the `EventType == raceEvent.EventType` filter on `seasonHistory`, so Bluebell + C2C history in the same calendar year forms one fairness pool (US34 AC6).
- **Match-by-name role mix-up.** Built `recentRoleNameByVolunteer` keyed off a normalised role name (trim + lower-case) and a `MostRecentInRole(volunteerId, role)` helper. Replaced the three `recentRoleByVolunteer ... TryGetValue(role.Id, ...)` call sites in restricted-roles, mix-up, and fill so cross-event duplicates like "Timekeeping" or "Water Table" rotate as one role (US34 AC7).
- **Race HQ preference.** New Step 4e — for each unplaced candidate with `WantsRaceHq`, pick the first open role with `RoleCategory.RaceHq` the candidate can fit.
- `WantsRaceHq` added to `AllocationCandidate` and `ProposedAssignment`; carried through `Place(...)` so it lands on the proposal and the persisted assignment.

### Applier + service
- `RosterDraftApplier.ApplyAsync` forwards `WantsRaceHq` on `VolunteerAssignmentInput`.
- `VolunteerRosterService.AddAssignmentAsync` / `UpdateAssignmentAsync` write the new column.

### UI
- `Views/VolunteerRoster/Allocate.cshtml` branches columns by event type:
  - C2C: Preferred role / Run after / Near finish / Can't walk far / Seated / Any role (unchanged).
  - Bluebell: Run after / Start-Finish (`WantsNearFinish`) / Race HQ (`WantsRaceHq`) / Any role. Preferred-role dropdown, Can't walk far, and Seated checkboxes hidden.
- The roster Index, role editor, and exports already key off `event.EventType` and `VolunteerRoleService.GetRoles(eventType)`, so they automatically show the Bluebell catalogue on Bluebell events — no view changes needed there. Copy-from-previous-event filters by event type, so a Bluebell roster copies from the most recent Bluebell event.

### Tests (`RosterAllocatorTests`)
Six new Bluebell scenarios:
- `Bluebell_WantsRaceHq_PlacedInRaceHqCategory` — preference routes to a Race HQ role.
- `Bluebell_WantsStartFinish_PlacedInFinishArea` — Start/Finish preference (reuses `WantsNearFinish`) routes to FinishArea.
- `Bluebell_RunAfter_OnlyHonouredInRaceHqRoles` — run-after lands on Number Pick Up / On The Day Registration / Car Park Marshal only.
- `Bluebell_RunAfterRotation_PoolsAcrossEventTypes` — a candidate who ran-after at C2C is rotated down at Bluebell.
- `Bluebell_RoleMixUp_MatchesByName_AcrossEventTypes` — Alice doing C2C Timekeeping steers her off Bluebell Timekeeping.
- `Bluebell_ApplyDraft_PersistsWantsRaceHq` — the new column round-trips through Apply.

`GetRole` helper now takes an optional `EventType` parameter so both C2C and Bluebell roles can be queried by name. New `CreateBluebellEvent(date)` helper.

## Story traceability

| AC | Where |
|----|-------|
| 1 Bluebell event uses same roster UI | (No change — `VolunteerRosterController.Index` and `VolunteerRoleService.GetRoles(eventType)` already event-type-aware) |
| 2 Separate Bluebell role catalogue seeded | `SeedBluebellRoles` + migration |
| 3 Lead/Results restricted, empty allow-list | `SeedBluebellRoles` (restricted: true) |
| 4 Volunteers drawn from shared register | (No change — single `Volunteer` table) |
| 5 Allocator runs Bluebell complement with Bluebell preferences | `RosterAllocator.Propose` Step 4e + Allocate view |
| 6 Run-after rotation + mix-up pool Bluebell + C2C history | `seasonHistory` query (event-type filter removed) |
| 7 Role mix-up matches by name across events | `recentRoleNameByVolunteer` + `MostRecentInRole` |
| 8 Manual edit / retrospective / copy / export | (No change — all event-type-scoped already) |
| 9 US29 stats include Bluebell | (No change — `VolunteerStatsService` already crosses event types) |
| 10 Allocator unit tests | `RosterAllocatorTests` (6 new) |

## Notes
- US34 deliberately ships as one combined story (per clarifying answer); no split into a data-only + allocator pair.
- The Bluebell Lead/Results allow-lists are seeded as empty (mirrors US28's C2C approach); the organiser populates them via the existing roles UI.
- The marshal gender-mix post-pass remains keyed off role names starting with "Marshal Point" — a no-op on Bluebell, where no role uses that prefix.
- Role IDs 24-38 are reserved for Bluebell. Future Bluebell roles should continue from 39.
