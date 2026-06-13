# US17 — Time Validation and Race Analytics — Implementation Summary

**Status:** ✅ Complete — build green, 125/125 tests passing (104 unit + 21 integration). Foundational for US22/US23/US24.

## Files changed

**Created**
- `RaceResults.Web/Services/RaceTime.cs` — tolerant `TryParse`, canonical `Format`, `FormatGap`.
- `RaceResults.Web/Migrations/20260613162723_AddTimingDuration.*` — adds nullable `DurationTicks` to `TimingRows`.
- `RaceResults.UnitTests/RaceTimeTests.cs` — 17 parse/format cases.

**Modified**
- `Models/TimingRow.cs` — `DurationTicks` + `[NotMapped] Duration`; `Time` retained as raw audit text.
- `Models/ResultRecord.cs` — typed `Duration`; `Time` now canonical display.
- `Services/RaceResultsService.cs` — parsers validate each time (`ParsedTiming` record), reject malformed with row+value, persist `DurationTicks`; out-of-order monotonic warning (`FindOutOfOrderPositions`); collation emits typed `Duration` + canonical `Time`; `UpdateResult` validates time + stores `DurationTicks`.
- `Controllers/RaceController.cs` — finishers-per-minute derives from `Duration` (no silent drops).
- `Views/Race/Results.cshtml` — "Gap" column (`+m:ss`, winner shows —).
- `Views/Race/EditResult.cshtml` — feedback alert (so service validation errors show) + accepted-format hint.
- `Program.cs` — `BackfillTimingDurations` converts legacy string times at startup; logs unparseable ones.
- `RaceResults.UnitTests/UploadTimingsTests.cs` — canonical-format assertions updated; 3 new tests (malformed rejection, out-of-order warning, typed storage).
- `RaceResults.IntegrationTests/ResultsControllerTests.cs` — direct seed sets `DurationTicks` (as real uploads now do).
- `README.md`, `user-stories/US17-...md`.

## Decisions

- **Storage (AC3):** `long? DurationTicks` (numeric, sortable, SQLite-friendly) + retained raw `Time`. Nullable only for legacy/unparseable rows.
- **Canonical display (AC2):** `mm:ss` under an hour, `h:mm:ss` over. This intentionally changes stored display from `00:20:00` → `20:00`; existing collation assertions updated accordingly. `mm:ss` tolerates minutes ≥ 60 (e.g. `75:30`); `h:mm:ss` enforces minutes < 60.
- **Backfill/report (AC8):** startup converts parseable legacy times in place and logs a clear warning listing any that don't parse (surfaced, not silent). Skipped in the Testing environment. Chosen over a written file to avoid placing reports in a possibly cloud-synced working directory.
- **Edit error visibility (AC6):** added a feedback alert to the edit view — previously service-level edit errors weren't rendered there at all.

## Acceptance criteria — all met (1–8).
