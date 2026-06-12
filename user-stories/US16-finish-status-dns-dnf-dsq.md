# US16 - Finish Status (DNS / DNF / DSQ)

**Status:** 📋 Planned

## As a
Race official

## I want
To mark entrants as Did Not Start (DNS), Did Not Finish (DNF), or Disqualified (DSQ)

## So that
Published results distinguish no-shows from genuine non-finishers, and disqualified runners are removed from standings correctly

---

## Background

The current DNF list (US08) is inferred: any registered entrant without a finish row is shown as DNF. This conflates runners who started but dropped out with runners who never turned up. There is also no way to disqualify a finisher after results are uploaded.

## Statuses

| Status | Meaning | Effect |
|---|---|---|
| Finished | Has a finish position | Appears in results (default) |
| DNS | Registered but never started | Excluded from DNF list, stats, and results |
| DNF | Started but did not finish | Listed in the DNF section as today |
| DSQ | Finished but disqualified | Removed from results; positions below close up |

## Acceptance Criteria

1. Each entrant has a finish status; entrants without a finish row default to DNF (current behaviour) but can be set to DNS.
2. A finisher can be marked DSQ from the Results page, with a required reason recorded.
3. Marking a finisher DSQ removes them from collated results, recomputes subsequent positions for display, and is reversible.
4. DNS entrants are excluded from the DNF list, race statistics totals, and the PDF.
5. DSQ runners earn no Champions of Champions points; existing points for that event are voided using the `Voided` audit action and the leaderboard recalculates.
6. The results PDF shows DNF and DSQ sections consistent with the on-screen view.
7. Status changes are recorded (who/when/why is captured in the reason text and timestamp).

## Notes

- The `AuditAction.Voided` enum value already exists in `PointsAuditLog` and is currently unused; this story is its natural consumer.
- Display of re-computed positions after a DSQ should not rewrite the stored finish bib rows; it is a presentation-level adjustment so the original timing data remains intact.
