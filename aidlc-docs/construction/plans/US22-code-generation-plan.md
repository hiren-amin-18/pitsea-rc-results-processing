# US22 — Course Records Management — Code Generation Plan

**Story:** [US22](../../../user-stories/US22-course-records-management.md)
**Type:** Brownfield + schema change. Depends on US17 typed durations.

## Design

- **`CourseRecord`** entity: `EventType`, `Category`, `DurationTicks` (typed — AC7), `RunnerName`, `Club`, `EventName`, `EventDate`, `SourceEventId` (the app event that set it; null for seeded), `IsCurrent`, `CreatedAt`. History retained by keeping superseded rows with `IsCurrent = false` (AC6).
- **`CourseRecordService`** (scoped) — slots/upsert/pending/confirm; depends on `IRaceResultsService` for category winners.
- PDF reads current records directly via the DbContext factory (keeps the singleton `RaceResultsService` free of a scoped dependency).

## Steps

- [ ] **Step 1 — Models.** `CourseRecord`; `CourseRecordSlot`; `EditCourseRecordInput`; `PendingCourseRecord`; add `PendingCourseRecords` to `ResultsPageViewModel`.
- [ ] **Step 2 — DbContext + seed + migration.** `DbSet<CourseRecord>`; seed Crown to Crown Male 15:25 Adam Hickey (Aug 2013) + Female 18:01 Jessica Judd (Dec 2015); Bluebell 5 empty (AC2). Migration `AddCourseRecords`.
- [ ] **Step 3 — Service.** `GetCurrentRecordSlots`, `TryGetRecordForEdit`, `UpsertRecord` (manual correction, in place), `GetPendingRecords(eventId)` (category winner faster than current, typed compare), `ConfirmRecord(eventId, category)` (supersede + history + `SourceEventId`).
- [ ] **Step 4 — Management UI (AC1).** `CourseRecordsController` Index (8 slots) + Edit; nav link.
- [ ] **Step 5 — Results page (AC4).** Pending-record banner with per-category Confirm buttons; `RaceController.Results` populates it; `ConfirmCourseRecord` POST.
- [ ] **Step 6 — PDF (AC3, AC5).** Render the records line from stored current records for the event type (omit if none); flag records set at the current event as "NEW COURSE RECORD".
- [ ] **Step 7 — Tests + build + docs.** Pending detection (typed), confirm + history, seed present, no-record omission; README; US22 → ✅.

## Story traceability

| AC | Covered by |
|----|------------|
| 1 stored per type/category + UI | Steps 1, 2, 4 |
| 2 seeded C2C records, Bluebell empty | Step 2 |
| 3 PDF from data, omit if none | Step 6 |
| 4 detect + confirm | Steps 3, 5 |
| 5 PDF "NEW COURSE RECORD" | Step 6 |
| 6 history retained | Steps 1, 3 |
| 7 typed comparison | Steps 1, 3 |
