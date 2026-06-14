# US29 — Volunteer Statistics — Implementation Summary

**Status:** ✅ Complete — build green, 183 unit + 26 integration = 209/209 tests passing.

## Files changed

**Created**
- `Models/VolunteerStatsModels.cs` — `EventVolunteerStats`, `SeasonVolunteerStats`, `VolunteerSeasonProfile`, `RoleBreakdownRow`, `RolePerformedRow`, `RunAndVolunteerSummary`, `RoleCoverageTrendItem`.
- `Services/IVolunteerStatsService.cs` + `VolunteerStatsService.cs` — per-event + season aggregation (pure EF queries).
- `Controllers/VolunteerStatsController.cs` — `Index?year=YYYY` and `Csv?year=YYYY` (CsvHelper, already on csproj).
- `Views/VolunteerStats/Index.cshtml` — year picker, summary cards, most-active table, full per-volunteer table, role coverage trend.
- `RaceResults.UnitTests/VolunteerStatsTests.cs` — 8 tests: empty season, single-event ever-present collapse, multi-event ever-present, non-member zero ballot, multi-role-one-event counting, ties for most active, per-event breakdown, role coverage trend ordering.

**Modified**
- `Models/VolunteerInputs.cs` — added `PerEventStats` to `RosterViewModel`.
- `Services/VolunteerRosterService.cs` — optional `IVolunteerStatsService` dependency; `GetRoster` populates `PerEventStats` when injected.
- `Program.cs` — registered `IVolunteerStatsService` as scoped (before the roster service).
- `Views/Shared/_Layout.cshtml` — Manage dropdown now includes Volunteer Stats.
- `Views/VolunteerRoster/Index.cshtml` — per-event stats panel (Assignments / Volunteers / Unfilled / link to season stats).
- `user-stories/US29-volunteer-stats.md` — status flipped to ✅.
- `README.md` — moved US29 from Planned to Implemented; updated intro counts.

## Decisions

- **Season year = calendar year** (`EventDate.Year`), matching the C2C Good-Friday-to-Boxing-Day window and the convention used by US14 / US24 / US30. No new "season" concept needed.
- **Ever-present** collapses to "attended every event of that year". For single-event seasons, it trivially marks the only attendee — preferred over hiding the badge, which would surprise users in a year where only one event has run so far.
- **Members-only ballot** baked into `BallotEntries` at the aggregation site, not at the UI. Non-members appear in every other stat unchanged.
- **`RosterViewModel.PerEventStats`** is optional (nullable). The roster service still works without the stats service injected — useful for unit tests that don't need the per-event panel and for any future deserialisation paths.
- **CSV export** mirrors the US18 pattern (UTF-8 with BOM, CsvHelper invariant culture) so it opens cleanly in Excel.

## Acceptance criteria — all met (1–8).
