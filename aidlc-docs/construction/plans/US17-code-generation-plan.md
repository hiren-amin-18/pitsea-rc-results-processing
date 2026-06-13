# US17 — Time Validation and Race Analytics — Code Generation Plan

**Story:** [US17](../../../user-stories/US17-time-validation-and-analytics.md)
**Type:** Brownfield. Schema change (new column + migration) + parsing/validation + analytics. Foundational for US22/US23/US24.

## Design

- **`RaceTime` helper** (`Services/RaceTime.cs`): tolerant `TryParse` (`mm:ss`, `h:mm:ss`, `hh:mm:ss.f`; rejects non-numeric, seconds/minutes ≥ 60 where positional), canonical `Format` (`mm:ss` under an hour, `h:mm:ss` over), and `FormatGap` (`+m:ss`). Pure, fully unit-tested.
- **`TimingRow`**: keep `Time` (raw text, audit — AC3) and add `long? DurationTicks` (canonical sortable; null = unparseable legacy) + `[NotMapped] TimeSpan? Duration`.
- **`ResultRecord`**: add `TimeSpan? Duration`; collation sets `Time` to canonical display when parseable, else raw.

## Steps

- [ ] **Step 1 — `RaceTime` helper + unit tests.**
- [ ] **Step 2 — Model.** `TimingRow.DurationTicks`/`Duration`; `ResultRecord.Duration`.
- [ ] **Step 3 — Migration.** `dotnet ef migrations add AddTimingDuration` (adds nullable `DurationTicks`).
- [ ] **Step 4 — Upload parsing/validation (AC1, AC2).** Parsers validate each time via `RaceTime.TryParse`, reporting `row N: invalid time (value)`; capture parsed duration (switch the parse dictionary value to a `ParsedTiming(raw, duration)` record). Persist raw + `DurationTicks`.
- [ ] **Step 5 — Monotonic warning (AC4).** After position checks, warn (non-blocking) when a finisher's time is earlier than someone ahead; list affected positions.
- [ ] **Step 6 — Collation + stats (AC7).** Collation emits typed `Duration` + canonical `Time`; `RaceController.Stats` finishers-per-minute uses `Duration` (no silent drops).
- [ ] **Step 7 — Gap to winner (AC5).** Add a "Gap" column to `Results.cshtml` (`+m:ss`, winner shows —).
- [ ] **Step 8 — Edit validation (AC6).** `UpdateResult` validates `Time` with `RaceTime.TryParse` and stores `DurationTicks`; `EditResult.cshtml` shows the accepted-format hint.
- [ ] **Step 9 — Legacy backfill (AC8).** Startup routine (skipped in Testing) converts parseable string times in place; unparseable rows are logged as a clear warning report (surfaced, not silently kept).
- [ ] **Step 10 — Tests + build + docs.** Service tests (malformed rejection, monotonic warning, typed storage); full suite green; README (upload formats, features, conventions, counts); US17 → ✅.

## Story traceability

| AC | Covered by |
|----|------------|
| 1 reject malformed w/ row+value | Step 4 |
| 2 tolerant formats + canonical | Steps 1, 4 |
| 3 typed storage + raw retained | Steps 2, 4 |
| 4 monotonic warning | Step 5 |
| 5 gap-to-winner column | Step 7 |
| 6 edit validates same rules | Step 8 |
| 7 stats from durations, no drops | Step 6 |
| 8 legacy migration + report | Steps 3, 9 |
