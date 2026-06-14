# AI-DLC Audit Log

## US30 — End of Season Review (volunteer section wired up to US29)
**Timestamp**: 2026-06-14
**Stage**: Construction (Code Generation + Build & Test)
**Plan**: incremental change — no new plan; tracked here.
**Build Status**: Success
**Test Status**: Pass (195 unit + 26 integration = 221)
**Notes**: Replaced the placeholder `MostActiveVolunteers` collection on `SeasonReview` with a real `VolunteerRecognition` block (total instances + ballot count + most-active list + ever-present list + ran-and-volunteered list). `SeasonReviewService` now optionally takes `IVolunteerStatsService`; when injected and the year has assignments, the review page and PDF render the volunteer section and the awards table includes "Volunteer of the season" + "Ever-present volunteer" entries. Degrades gracefully when no assignments exist. US30 status flipped to ✅ Complete (no remaining degraded sections).

---

## US32 — Automated Roster Allocation
**Timestamp**: 2026-06-14
**Stage**: Construction (Code Generation + Build & Test)
**Plan**: `aidlc-docs/construction/plans/US32-code-generation-plan.md`
**Summary**: `aidlc-docs/construction/US32/US32-implementation-summary.md`
**Build Status**: Success
**Test Status**: Pass (193 unit + 26 integration = 219)
**Notes**: Brownfield, no schema change — pure rules engine over US28 assignments and US29 history. Allocator runs the seven-step priority pipeline (pre-place → eligibility → run-after rotation → preferences → mix-up → gender mix → fill); Apply step re-validates via the roster service so the database can never end up invalid. JSON-round-tripped draft (no draft table). All planned user stories now complete.

---

## US29 — Volunteer Statistics
**Timestamp**: 2026-06-14
**Stage**: Construction (Code Generation + Build & Test)
**Plan**: `aidlc-docs/construction/plans/US29-code-generation-plan.md`
**Summary**: `aidlc-docs/construction/US29/US29-implementation-summary.md`
**Build Status**: Success
**Test Status**: Pass (183 unit + 26 integration = 209)
**Notes**: Brownfield, no schema change — pure aggregation over US28 assignments. New per-event panel on the roster page, season volunteer-stats page (cards + most-active + per-volunteer table + role-coverage trend) with CSV export. Members-only London Marathon ballot baked into aggregation. US30 (End of Season Review) is now eligible to be un-degraded.

---

## US28 — Volunteer Roster Builder
**Timestamp**: 2026-06-14
**Stage**: Construction (Code Generation + Build & Test)
**Plan**: `aidlc-docs/construction/plans/US28-code-generation-plan.md`
**Summary**: `aidlc-docs/construction/US28/US28-implementation-summary.md`
**Build Status**: Success
**Test Status**: Pass (175 unit + 26 integration = 201)
**Notes**: Brownfield + new schema (4 entities, 1 migration, 23-row C2C role seed). New manage pages for Volunteers / Volunteer Roles and a per-event Roster page with PDF + Excel export (ClosedXML / QuestPDF — both already on csproj). Restricted-role allow-lists seeded empty; organiser populates Lead / Results / Marshal 7 pre-place via the UI. Sets the data foundation for US29 and US32.

---

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
**Notes**: Brownfield + schema change. Pure SeasonCalendar (Anonymous Gregorian Easter, second/first Wednesday), SeasonCalendarService preview/generate with idempotent skip, RaceEvent.StartTime optional, Generate Season UI on Events page; current event untouched. Reviewed and committed (0fb2b75).

---

## US30 — End of Season Review (degraded)
**Timestamp**: 2026-06-14
**Stage**: Construction (Code Generation + Build & Test)
**Plan**: `aidlc-docs/construction/plans/US30-code-generation-plan.md`
**Summary**: `aidlc-docs/construction/US30/US30-implementation-summary.md`
**Build Status**: Success (no schema change)
**Test Status**: Pass (150 unit + 26 integration = 176)
**Notes**: Brownfield capstone, no schema change. SeasonReviewService composes US24 dashboard + US14 Champions + US22 records + US16 DNF/DSQ into a single page + branded PDF; awards list derived from the same data (single source of calc). YoY only when prior year has data; series-vs-scoring-window labelled. Volunteer sections deliberately empty until US28/US29 land. Reviewed and committed (7b890b6).

---

## US25 — Application Installer
**Timestamp**: 2026-06-14
**Stage**: Construction (Code Generation + Build & Test)
**Plan**: `aidlc-docs/construction/plans/US25-code-generation-plan.md`
**Summary**: `aidlc-docs/construction/US25/US25-implementation-summary.md`
**Build Status**: Success (no schema change)
**Test Status**: Pass (155 unit + 26 integration = 181)
**Notes**: Brownfield deployment story. Added DatabasePathResolver (per-user %LOCALAPPDATA%\PitseaRaceResults default when no connection string configured), win-x64 self-contained publish profile, Inno Setup installer with opt-in uninstall data removal, launcher .cmd, build script with zip fallback, and installer README. Awaiting user review.

---
