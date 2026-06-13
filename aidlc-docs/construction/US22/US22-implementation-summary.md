# US22 — Course Records Management — Implementation Summary

**Status:** ✅ Complete — build green, 143/143 tests passing (121 unit + 22 integration). Depends on US17.

## Files changed

**Created**
- `Models/CourseRecord.cs`, `Models/CourseRecordModels.cs` (`CourseRecordSlot`, `EditCourseRecordInput`, `PendingCourseRecord`).
- `Services/ICourseRecordService.cs` + `CourseRecordService.cs`.
- `Controllers/CourseRecordsController.cs`; `Views/CourseRecords/Index.cshtml`, `Edit.cshtml`.
- `Migrations/…_AddCourseRecords.*` — `CourseRecords` table + seeded Crown to Crown records.
- Tests: `CourseRecordTests.cs` (5).

**Modified**
- `Data/RaceResultsDbContext.cs` — `CourseRecords` set, index, and `HasData` seed (Adam Hickey 15:25 / Jessica Judd 18:01).
- `Models/ResultsPageViewModel.cs` — `PendingCourseRecords`.
- `Services/RaceResultsService.cs` — PDF reads stored records (`LoadCurrentCourseRecords`), renders the line per event type, omits when none, flags "NEW COURSE RECORD" for records set at the current event.
- `Controllers/RaceController.cs` — injects `ICourseRecordService`; `Results` populates pending records; `ConfirmCourseRecord` POST.
- `Views/Race/Results.cshtml` — pending-record banner with per-category Confirm.
- `Views/Shared/_Layout.cshtml` — Records nav link.
- `Program.cs` — registered `ICourseRecordService`.
- `README.md`, `user-stories/US22-course-records-management.md`.

## Decisions

- **Typed comparison (AC7):** `DurationTicks` stored; comparisons and pending detection use `TimeSpan`.
- **History (AC6):** confirming a new record sets the old one `IsCurrent = false` and inserts a new current row with `SourceEventId`; superseded records remain queryable. Manual edits correct the current record in place (no spurious history).
- **Confirmation flow (AC4):** detection is surfaced as a banner on the Results page (after upload/edit the organiser lands there); confirming re-derives the winner server-side and re-checks it still beats the record, guarding against stale/short-course confirmations.
- **PDF dependency (AC3, AC5):** the singleton `RaceResultsService` reads `CourseRecords` directly via its DbContext factory rather than depending on the scoped `CourseRecordService`; records line omitted when empty (preserves prior Bluebell 5 behaviour).
- **Lifetimes:** `CourseRecordService` is scoped and uses `IRaceResultsService` for category winners.

## Acceptance criteria — all met (1–7).
