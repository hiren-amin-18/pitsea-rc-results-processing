# AI-DLC Audit Log

## US18 — Export Results to CSV
**Timestamp**: 2026-06-13
**Stage**: Construction (Code Generation + Build & Test)
**Plan**: `aidlc-docs/construction/plans/US18-code-generation-plan.md`
**Summary**: `aidlc-docs/construction/US18/US18-implementation-summary.md`
**Build Status**: Success
**Test Status**: Pass (78 unit + 21 integration = 99)
**Notes**: Brownfield modification. Added CSV export for collated results and Champions leaderboard. Reviewed and committed (1399e85).

---

## US27 — Example Upload File Links
**Timestamp**: 2026-06-13
**Stage**: Construction (Code Generation + Build & Test)
**Plan**: `aidlc-docs/construction/plans/US27-code-generation-plan.md`
**Summary**: `aidlc-docs/construction/US27/US27-implementation-summary.md`
**Build Status**: Success (publish verified assets ship)
**Test Status**: Pass (78 unit + 21 integration = 99)
**Notes**: Brownfield. Mirrored example files into wwwroot; added download links + expected-columns summaries to the Uploads page. Reviewed and committed (4a30c94).

---

## US19 — Database Backup and Restore
**Timestamp**: 2026-06-13
**Stage**: Construction (Code Generation + Build & Test)
**Plan**: `aidlc-docs/construction/plans/US19-code-generation-plan.md`
**Summary**: `aidlc-docs/construction/US19/US19-implementation-summary.md`
**Build Status**: Success
**Test Status**: Pass (82 unit + 21 integration = 103)
**Notes**: Brownfield. New DatabaseBackupService (SQLite backup API snapshot, validated restore with pre-restore copy + migrate), Settings UI, destructive-action reminders. Reviewed and committed (fbb42df).

---

## US17 — Time Validation and Race Analytics
**Timestamp**: 2026-06-13
**Stage**: Construction (Code Generation + Build & Test)
**Plan**: `aidlc-docs/construction/plans/US17-code-generation-plan.md`
**Summary**: `aidlc-docs/construction/US17/US17-implementation-summary.md`
**Build Status**: Success (new EF migration AddTimingDuration)
**Test Status**: Pass (104 unit + 21 integration = 125)
**Notes**: Brownfield + schema change. RaceTime helper, typed TimingRow.DurationTicks, validated parsing, out-of-order warning, gap-to-winner column, edit validation, typed stats, legacy backfill at startup. Foundational for US22/US23/US24. Reviewed and committed (09b62e1).

---

## US15 — Runner Registry
**Timestamp**: 2026-06-13
**Stage**: Construction (Code Generation + Build & Test)
**Plan**: `aidlc-docs/construction/plans/US15-code-generation-plan.md`
**Summary**: `aidlc-docs/construction/US15/US15-implementation-summary.md`
**Build Status**: Success (new EF migration AddRunnerRegistry)
**Test Status**: Pass (111 unit + 22 integration = 133)
**Notes**: Brownfield + schema change. New Runner entity + Entrant.RunnerId FK, upload matching with near-match warnings, RunnerRegistryService (list/edit/merge) with affected-season recalc, Champions keying on RunnerId, event-delete keeps runners (inactive flag), startup data backfill. Structural prerequisite for US24. Reviewed and committed (00e9e0a).

---

## US16 — Finish Status (DNS / DNF / DSQ)
**Timestamp**: 2026-06-13
**Stage**: Construction (Code Generation + Build & Test)
**Plan**: `aidlc-docs/construction/plans/US16-code-generation-plan.md`
**Summary**: `aidlc-docs/construction/US16/US16-implementation-summary.md`
**Build Status**: Success (new EF migration AddFinishStatus)
**Test Status**: Pass (116 unit + 22 integration = 138)
**Notes**: Brownfield + schema change. FinishStatus on Entrant; DSQ removes finishers (display positions close up) + voids Champions points via the Voided audit action + recalc; DNS excluded from DNF/stats/PDF; PDF/CSV gain DNF/DSQ sections; reversible. Reviewed and committed (0d76310).

---

## US22 — Course Records Management
**Timestamp**: 2026-06-13
**Stage**: Construction (Code Generation + Build & Test)
**Plan**: `aidlc-docs/construction/plans/US22-code-generation-plan.md`
**Summary**: `aidlc-docs/construction/US22/US22-implementation-summary.md`
**Build Status**: Success (new EF migration AddCourseRecords with seed)
**Test Status**: Pass (121 unit + 22 integration = 143)
**Notes**: Brownfield + schema change. CourseRecord entity per event type/category with seeded C2C records; data-driven PDF records line with NEW COURSE RECORD flag; automatic detection + organiser confirmation; record history retained; typed-duration comparison; management UI. Depends on US17. Reviewed and committed (bdfb7be).

---

## US23 — Enhanced Race Statistics
**Timestamp**: 2026-06-13
**Stage**: Construction (Code Generation + Build & Test)
**Plan**: `aidlc-docs/construction/plans/US23-code-generation-plan.md`
**Summary**: `aidlc-docs/construction/US23/US23-implementation-summary.md`
**Build Status**: Success (no schema change)
**Test Status**: Pass (126 unit + 22 integration = 148)
**Notes**: Brownfield. GetRaceStatisticsSummary: completion rate, gender-split %, affiliation chart, finish-time summary (winner/median/average/percentiles/spread), busiest window; current-event scoped; typed durations; DNS excluded from completion. Reviewed and committed (2413d53).

---

## US24 — Season Statistics & Runner Season Profiles
**Timestamp**: 2026-06-13
**Stage**: Construction (Code Generation + Build & Test)
**Plan**: `aidlc-docs/construction/plans/US24-code-generation-plan.md`
**Summary**: `aidlc-docs/construction/US24/US24-implementation-summary.md`
**Build Status**: Success (no schema change)
**Test Status**: Pass (132 unit + 22 integration = 154)
**Notes**: Brownfield. SeasonStatisticsService: per-year dashboard (attendance/ever-present, clubs, fastest per category per type, most improved, participation, DNF rate) + runner season profile (races, season bests per type, average position, streak, Champions progression). Keyed on RunnerId; typed same-type time stats; derived/read-only. Depends on US15 + US17. Reviewed and committed (296bb8a).

---

## US20 — Archive Completed Events
**Timestamp**: 2026-06-13
**Stage**: Construction (Code Generation + Build & Test)
**Plan**: `aidlc-docs/construction/plans/US20-code-generation-plan.md`
**Summary**: `aidlc-docs/construction/US20/US20-implementation-summary.md`
**Build Status**: Success (new EF migration AddEventArchiving)
**Test Status**: Pass (136 unit + 22 integration = 158)
**Notes**: Brownfield + schema change. RaceEvent.IsArchived; archived events reject uploads/edits/detail-edits/set-current/delete; archiving the current event promotes another; read-only eventId viewing added across Results/Stats/Top10/exports; Champions untouched. Reviewed and committed (6290d52).

---

## US21 — Public Results Page
**Timestamp**: 2026-06-14
**Stage**: Construction (Code Generation + Build & Test)
**Plan**: `aidlc-docs/construction/plans/US21-code-generation-plan.md`
**Summary**: `aidlc-docs/construction/US21/US21-implementation-summary.md`
**Build Status**: Success (new EF migration AddPublicResults)
**Test Status**: Pass (136 unit + 26 integration = 162)
**Notes**: Brownfield + schema change. RaceEvent.IsPublished + 128-bit PublicToken; /public/results/{token} + /public/champions/{token} with a minimal layout (no admin nav); unknown/unpublished tokens 404; unmatched bibs shown as "Unknown runner"; Publish/Unpublish + copy-link on Events page. Pairs with US20. Reviewed and committed (cae0548).

---

## US31 — Season Calendar Generator
**Timestamp**: 2026-06-14
**Stage**: Construction (Code Generation + Build & Test)
**Plan**: `aidlc-docs/construction/plans/US31-code-generation-plan.md`
**Summary**: `aidlc-docs/construction/US31/US31-implementation-summary.md`
**Build Status**: Success (new EF migration AddEventStartTime)
**Test Status**: Pass (147 unit + 26 integration = 173)
**Notes**: Brownfield + schema change. Pure SeasonCalendar (Anonymous Gregorian Easter, second/first Wednesday), SeasonCalendarService preview/generate with idempotent skip, RaceEvent.StartTime optional, Generate Season UI on Events page; current event untouched. Awaiting user review.

---
