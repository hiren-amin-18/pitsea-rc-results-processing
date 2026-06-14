# US31 — Season Calendar Generator — Implementation Summary

**Status:** ✅ Complete — build green, 173/173 tests passing (147 unit + 26 integration).

## Files changed

**Created**
- `Services/SeasonCalendar.cs` — pure helpers: Easter (Anonymous Gregorian), Good Friday, first/second Wednesday, `BuildFixtures(year, septemberOption)` → 7 fixtures.
- `Services/ISeasonCalendarService.cs` + `SeasonCalendarService.cs` — preview/generate.
- `Controllers/EventsController.cs` — `GenerateSeason` GET + POST.
- `Views/Events/GenerateSeason.cshtml`.
- `Migrations/…_AddEventStartTime.*` — `RaceEvent.StartTime`.
- Tests: `SeasonCalendarTests.cs` (6).

**Modified**
- `Models/RaceEvent.cs` — `StartTime` (nullable `TimeSpan`).
- `Views/Events/Index.cshtml` — "Generate Season" button + start-time display.
- `Program.cs` — registered `ISeasonCalendarService`.
- `README.md`, `user-stories/US31-season-calendar-generator.md`.

## Decisions

- **Easter (AC7):** Anonymous Gregorian (Meeus) — pure, no lookup table. Tested across edge years (2024 early, 2027 March-Easter, 2038 latest).
- **Seven fixtures**, matching the story rules: Good Friday + May/Jun/Jul/Aug second Wednesdays + Sep (first or second per choice) + Boxing Day. Names are `Crown to Crown – Month YYYY` (AC2) and remain editable like any event.
- **Idempotent (AC4):** preview flags any date that already has a C2C event in that year; generate skips those dates entirely and never modifies existing events.
- **Current event untouched (AC6):** generated events are always created with `IsCurrent = false`; the existing current selection stays.
- **Start time (AC3):** stored on the event, populated by the generator (11:00 / 19:30 / 19:00); displayed beside the date on the Events page; PDF title format intentionally untouched.

## Acceptance criteria — all met (1–7).
