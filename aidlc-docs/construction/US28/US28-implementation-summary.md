# US28 — Volunteer Roster Builder — Implementation Summary

**Status:** ✅ Complete — build green, 175 unit + 26 integration = 201/201 tests passing.

## Files changed

**Created — Models**
- `Models/Volunteer.cs` — register entity with member / first-aid / gender / optional runner link.
- `Models/RoleCategory.cs` — enum (`Leadership` | `FinishArea` | `Course`).
- `Models/VolunteerRole.cs` — role catalogue with default/min/max counts, run-after capacity, first-aid + restriction + pre-place flags.
- `Models/VolunteerRoleEligibility.cs` — join entity for restricted-role allow-lists.
- `Models/VolunteerAssignment.cs` — single entity carrying both the allocation and the per-event preferences (chosen sign-up model).
- `Models/VolunteerInputs.cs` — DTOs (`VolunteerInput`, `VolunteerListItem`, `VolunteerRoleInput`, `VolunteerAssignmentInput`, `RosterViewModel`, `RosterRoleRow`, `RosterAssignmentRow`, `CopyRosterResult`).

**Created — Services**
- `Services/IVolunteerRegistryService.cs` + `VolunteerRegistryService.cs` — CRUD + deactivate.
- `Services/IVolunteerRoleService.cs` + `VolunteerRoleService.cs` — CRUD + allow-list sync + pre-place + validation (min/max/runAfter bounds).
- `Services/IVolunteerRosterService.cs` + `VolunteerRosterService.cs` — get / add / update / remove / copy-from-previous; full assignment validation (eligibility, first-aid, max-count, run-after capacity, double-booking warning).
- `Services/IVolunteerRosterExportService.cs` + `VolunteerRosterExportService.cs` — PDF (QuestPDF, A4) and Excel (ClosedXML) export grouped by category.

**Created — Controllers + Views**
- `Controllers/VolunteersController.cs`, `Controllers/VolunteerRolesController.cs`, `Controllers/VolunteerRosterController.cs` (routed `/Events/{eventId}/Roster`).
- `Views/Volunteers/{Index,Create,Edit,_Form}.cshtml`.
- `Views/VolunteerRoles/{Index,Create,Edit,_Form}.cshtml`.
- `Views/VolunteerRoster/Index.cshtml`.

**Created — Migration + Tests**
- `Migrations/20260614204948_AddVolunteerRoster.*` — new tables `Volunteers`, `VolunteerRoles` (seeded with 23 C2C roles), `VolunteerRoleEligibilities`, `VolunteerAssignments` + indexes + delete-behaviour wiring.
- `RaceResults.UnitTests/VolunteerRosterTests.cs` — 20 tests covering registry CRUD, the C2C seed (23 roles, restriction/first-aid flags, run-after capacities), allow-list enforcement, first-aid enforcement, capacity/run-after enforcement, double-booking warning, copy-from-previous with skip list, and PDF/Excel export validity.

**Modified**
- `Data/RaceResultsDbContext.cs` — four new `DbSet`s, four entity configurations, and the 23-row C2C role seed (`SeedC2CRoles()`).
- `Program.cs` — registered the four new scoped services.
- `Views/Shared/_Layout.cshtml` — Manage dropdown now includes Volunteers and Volunteer Roles.
- `Views/Events/Index.cshtml` — added a Roster link to each event row (works for current, future, and archived events).
- `user-stories/US28-volunteer-roster.md` — status flipped to ✅.
- `README.md` — moved US28 from Planned to Implemented; updated intro counts.

## Decisions

- **Single Assignment entity** (per the agreed sign-up model). Preferences live on the same row as the allocation; retrospective entry simply creates assignments with no preferences set.
- **Restricted-role allow-list seeded empty.** Lead and Results are marked `HasEligibilityRestriction=true` in the seed, but no people are baked into the migration — the organiser adds Hiren / Michael / Ian once and chooses them via the roles UI. Same for `PrePlacedVolunteerId` (Ian at Marshal 7 is configured by hand, not seeded).
- **Source-pool roles** (Funnel / Finish Line Results drawn from Number Collection / OTD Registration) are **not** a schema constraint in this story — the organiser can assign anyone manually. US32's allocator will treat it as a hint when it drafts.
- **Retrospective entry** (AC5) is implemented by *not* adding a "future events only" guard. Any event — past, current, future, or archived — gets a roster page.
- **Cascade behaviour** (AC10): deleting an event cascades its `VolunteerAssignments`; the volunteer and role are protected (`Restrict`). The volunteer's `Runner` link is `Restrict` for the same reason.
- **Double-booking** (AC7) is a warning, never an error — the index on `(EventId, VolunteerId)` is non-unique on purpose.
- **Excel export** uses ClosedXML 0.105.0 (already on csproj); PDF uses QuestPDF Community (already on csproj). No new package dependencies were added.

## Acceptance criteria — all met (1–11).
