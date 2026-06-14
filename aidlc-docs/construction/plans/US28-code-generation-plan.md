# US28 — Volunteer Roster Builder — Code Generation Plan

**Story:** [US28](../../../user-stories/US28-volunteer-roster.md)
**Type:** Brownfield + schema additions (three new entities) + new pages + Excel/PDF export.

## Design

### Entities (new)

- **`Volunteer`** — persistent person.
  - `Id`, `Name` (required), `Email?`, `Phone?`.
  - `Gender` (string: `Male`/`Female`/`""`) — drives marshal mix in US32.
  - `IsClubMember` (bool, default `true`) — drives ballot eligibility in US29.
  - `IsFirstAidTrained` (bool, default `false`).
  - `RunnerId?` (FK to `Runner`, nullable, `OnDelete.Restrict` — same pattern as `Entrant.RunnerId`).
  - `IsActive` (bool, default `true`) — deactivate, never delete (AC1).
  - `CreatedAt`.
- **`VolunteerRole`** — configurable role catalogue.
  - `Id`, `Name` (required, unique).
  - `Category` (enum `RoleCategory`: `Leadership` | `FinishArea` | `Course`).
  - `EventType` (existing enum, scoped to `CrownToCrown` for now; Bluebell 5 uses its own seed in a later story).
  - `DefaultCount` (int), `MinCount` (int), `MaxCount` (int).
  - `IsOptional` (bool).
  - `RunAfterCapacity` (int) — how many people in this role may run after.
  - `RequiresFirstAid` (bool).
  - `HasEligibilityRestriction` (bool) — when true, only explicitly allowed volunteers can be assigned (via `VolunteerRoleEligibility`).
  - `PrePlacedVolunteerId?` (FK to `Volunteer`, nullable) — e.g. Ian at Marshal 7.
  - `SortOrder` (int) — controls roster display order within category.
  - `IsActive` (bool) — retire without deleting history.
- **`VolunteerRoleEligibility`** — join table for restricted roles. `(VolunteerRoleId, VolunteerId)` unique. Empty by default for newly-seeded restricted roles (the organiser fills the allow-list once Hiren / Michael are added).
- **`VolunteerAssignment`** — single entity carrying both intent and allocation, per the chosen sign-up model.
  - `Id`, `EventId` (FK, cascade-delete with event), `VolunteerId` (FK, restrict — deleting an event must never delete the volunteer), `VolunteerRoleId` (FK, restrict).
  - `WillRunAfter` (bool).
  - `Note` (string, optional — e.g. "Gate 3").
  - **Preference fields** (organiser captures these on add; used by US32 later):
    - `PreferredRoleId?` (FK to role, nullable).
    - `WantsToRunAfter` (bool).
    - `WantsNearFinish` (bool).
    - `CantWalkFar` (bool).
    - `WantsSeated` (bool).
    - `AnyRole` (bool).
  - Indexed on `(EventId, VolunteerId)` — not unique (double-booking is allowed but warned, AC7).

### Services

- **`IVolunteerRegistryService` / `VolunteerRegistryService`** — CRUD over `Volunteer`, with `Deactivate(int id)` / `Reactivate(int id)`. Pickers exclude inactive volunteers.
- **`IVolunteerRoleService` / `VolunteerRoleService`** — CRUD over `VolunteerRole` + manage `VolunteerRoleEligibility` allow-lists + set `PrePlacedVolunteerId`.
- **`IVolunteerRosterService` / `VolunteerRosterService`** — per-event roster:
  - `GetRoster(eventId)` → grouped by `RoleCategory`, with `DefaultCount` / `MinCount` / `MaxCount` and assigned volunteers.
  - `AddAssignment(eventId, volunteerId, roleId, preferences, willRunAfter, note)` — returns assignment + `DoubleBookingWarning` flag (AC7).
  - `UpdateAssignment(id, ...)`, `RemoveAssignment(id)`.
  - `CopyFromPreviousEvent(targetEventId)` — copies assignments from the most recent C2C event chronologically before this one (AC9). Inactive volunteers and people no longer eligible for restricted roles are skipped, listed as `SkippedItems` in the result.
- **`IVolunteerRosterExportService` / `VolunteerRosterExportService`** — PDF (QuestPDF, mirroring the US09 `RaceResultsPdf` layout) and Excel (ClosedXML — one sheet, columns: Category, Role, Volunteer, Member?, First Aid?, Running After?, Note).

### Controllers / Views

- **`VolunteersController`** (`/Volunteers`):
  - `Index` — register list (active + inactive toggle), add/edit form, deactivate/reactivate.
  - `Edit/Create` POST.
- **`VolunteerRolesController`** (`/VolunteerRoles`):
  - `Index` — list roles by category with edit links + reorder.
  - `Edit/Create` POST including eligibility allow-list and `PrePlacedVolunteerId`.
- **`VolunteerRosterController`** (`/Events/{eventId}/Roster`):
  - `Index` — roster view grouped by category, with assigned/unfilled chips against complement, edit-in-place modal for each assignment.
  - `Add/Update/Delete` POSTs.
  - `CopyPrevious` POST.
  - `ExportPdf` GET, `ExportExcel` GET.
- Navigation:
  - Add **Manage → Volunteers** and **Manage → Roles** links to the existing Manage dropdown.
  - Add a **Roster** link to each event row on `/Events/Index`.

### Seed (C2C role catalogue)

Seed via `modelBuilder.Entity<VolunteerRole>().HasData(...)` in `RaceResultsDbContext.OnModelCreating`. 22 roles in three categories with the counts/flags from the US28 table:

| # | Category | Name | Default | Min | Max | Optional | RunAfter | FirstAid | Restricted |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Leadership | Lead | 1 | 1 | 1 | false | 0 | false | true |
| 2 | Leadership | Shadow Lead | 1 | 0 | 1 | true | 0 | false | false |
| 3 | Leadership | Results | 1 | 1 | 1 | false | 0 | false | true |
| 4 | FinishArea | Timekeeping | 2 | 2 | 2 | false | 0 | false | false |
| 5 | FinishArea | Course Setup | 2 | 2 | 2 | false | 0 | false | false |
| 6 | FinishArea | Number Collection | 2 | 1 | 2 | false | 1 | false | false |
| 7 | FinishArea | On The Day Registration | 4 | 4 | 4 | false | 2 | false | false |
| 8 | FinishArea | Finish Line Funnel | 1 | 1 | 1 | false | 0 | false | false |
| 9 | FinishArea | Finish Line Results | 2 | 2 | 2 | false | 0 | false | false |
| 10 | FinishArea | First Aid and Prizes | 1 | 1 | 1 | false | 0 | true | false |
| 11 | FinishArea | Tail Runners | 2 | 2 | 2 | false | 0 | false | false |
| 12 | FinishArea | Photographer | 1 | 0 | 1 | true | 0 | false | false |
| 13 | FinishArea | Water Table | 2 | 2 | 2 | false | 0 | false | false |
| 14 | Course | Marshal Point 1 | 2 | 2 | 2 | false | 0 | false | false |
| 15 | Course | Marshal Point 2 | 2 | 2 | 2 | false | 0 | false | false |
| 16 | Course | Marshal Point 3 | 2 | 2 | 2 | false | 0 | false | false |
| 17 | Course | Marshal Point 4 | 3 | 3 | 3 | false | 0 | false | false |
| 18 | Course | Marshal Point 5 | 2 | 2 | 2 | false | 0 | false | false |
| 19 | Course | Marshal Point 5a | 2 | 2 | 2 | false | 0 | false | false |
| 20 | Course | Marshal Point 6 | 2 | 2 | 2 | false | 0 | false | false |
| 21 | Course | Marshal Point 7 | 2 | 2 | 2 | false | 0 | false | false |
| 22 | Course | Metal Gate | 1 | 0 | 1 | true | 0 | false | false |
| 23 | Course | First Aid On Course | 1 | 1 | 1 | false | 0 | true | false |

All seeded with `IsActive=true`, `PrePlacedVolunteerId=null`. Empty `VolunteerRoleEligibility` allow-lists for the three restricted roles — the organiser populates them on first run (and pre-places Ian at Marshal Point 7) via the new UI.

Sources-from-other-roles (Finish Line Funnel / Results drawn from Number Collection / OTD Registration) is **not modelled in the schema** — it's an allocation hint relevant to US32 and a comment on the seed row's note; the organiser can assign anyone there in US28's manual flow.

## Steps

- [ ] **Step 1 — Models.** Add `Volunteer.cs`, `VolunteerRole.cs`, `RoleCategory.cs`, `VolunteerRoleEligibility.cs`, `VolunteerAssignment.cs` in `Models/`.
- [ ] **Step 2 — DbContext.** Register all four DbSets in `RaceResultsDbContext`; configure keys, indexes, FKs, delete behaviours, and `HasData` seed for the 23 roles.
- [ ] **Step 3 — Migration.** `dotnet ef migrations add AddVolunteerRoster`.
- [ ] **Step 4 — Registry service.** `IVolunteerRegistryService` + impl + unit tests (add, edit, deactivate preserves history; runner-link round trip; case-insensitive name search).
- [ ] **Step 5 — Role service.** `IVolunteerRoleService` + impl + unit tests (CRUD; allow-list add/remove; pre-place set/clear; min/max validation).
- [ ] **Step 6 — Roster service.** `IVolunteerRosterService` + impl + unit tests:
  - Get, add, update, remove.
  - Double-booking warning fires when same volunteer is added twice to one event.
  - Restricted role rejects a volunteer not on the allow-list.
  - First-aid role rejects a non-first-aid volunteer.
  - Min/max overrides respected; assigning beyond `MaxCount` returns a clear error.
  - `CopyFromPreviousEvent` picks the chronologically previous C2C event, skips inactive volunteers and now-ineligible restricted-role volunteers, returns skip list.
- [ ] **Step 7 — Export service.** PDF (QuestPDF, A4 portrait, one table grouped by category with role complement vs assigned counts) + Excel (ClosedXML); unit tests cover non-empty/empty roster and the "(running after)" suffix.
- [ ] **Step 8 — Controllers + Views.** Volunteers / Roles / Roster pages + navbar entries. Use existing styling (Bootstrap, monospace panels) consistent with the rest of the app.
- [ ] **Step 9 — DI.** Register the four services in `Program.cs` as scoped.
- [ ] **Step 10 — Integration tests.** Round-trip through the controllers: create volunteer → create role override → build roster → export both formats → copy to next event.
- [ ] **Step 11 — Build + docs.** Full suite green; README updated (new entities, new pages, manage menu); US28 → ✅; audit entry; implementation summary.

## Story traceability

| AC | Covered by |
|----|------------|
| 1 register with all flags | Steps 1, 2, 4, 8 |
| 2 role catalogue + override + pre-placed Ian | Steps 1, 2, 5, 8 |
| 3 manual assignment, multi per role, notes, run-after | Steps 1, 6, 8 |
| 4 fully editable, min/max overrides | Steps 6, 8 |
| 5 retrospective entry (past events) | Steps 6, 8 (no date guard on event in roster controller) |
| 6 post-event editing | Steps 6, 8 |
| 7 double-booking warning | Step 6 |
| 8 Excel + PDF + print-friendly export | Step 7, 8 |
| 9 copy from previous event | Step 6 |
| 10 survives archiving; event delete cascades assignments only | Step 2 (delete behaviours) |
| 11 accepts draft from US32 | Schema is shared; US32 plan will write into the same `VolunteerAssignment` table |
