# US17 - Time Validation and Race Analytics

**Status:** ✅ Complete

## As a
Race organiser

## I want
Uploaded finish times to be validated and stored as real durations rather than raw text

## So that
Results can be checked for ordering anomalies and enriched with gaps, pace, and reliable charts

---

## Background

`TimingRow.Time` is currently an unvalidated string. A malformed value (e.g. `00:2X:99`) is accepted at upload and only surfaces later as a blank in the finishers-per-minute chart, which silently drops unparseable times. Because times are not typed, the system cannot sort by time, compute gaps, or detect data-entry errors.

## Acceptance Criteria

1. Timing uploads (.csv and .xlsx) validate every time value; malformed times are rejected with the row number and offending value, consistent with existing upload error reporting.
2. Accepted formats are documented and tolerant of common variants (`h:mm:ss`, `mm:ss`, `hh:mm:ss.f`); values are normalised to a canonical display format.
3. Times are stored as durations (or a canonical sortable representation) while preserving the original text for audit.
4. On upload, a warning is raised if times are not monotonically non-decreasing with finish position (almost always a data-entry or chip error). The upload still succeeds; the affected rows are listed.
5. The Results view can show a "gap to winner" column (e.g. `+1:23`).
6. The edit-result form validates time input with the same rules as upload.
7. Race statistics derive from typed durations; the finishers-per-minute chart no longer silently drops rows (any excluded row is impossible by construction).
8. Existing stored string times are migrated: parseable values convert in place; unparseable values are surfaced to the organiser in a one-time report rather than silently kept.

## Notes

- This story is a prerequisite for US22 (course record detection) and for any pace or age-grading features.
- Comparison/sorting must use the typed value; the original raw text is for display/audit only.
