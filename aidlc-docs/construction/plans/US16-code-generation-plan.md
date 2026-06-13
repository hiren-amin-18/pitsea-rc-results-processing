# US16 — Finish Status (DNS / DNF / DSQ) — Code Generation Plan

**Story:** [US16](../../../user-stories/US16-finish-status-dns-dnf-dsq.md)
**Type:** Brownfield + schema change. Consumes the existing `AuditAction.Voided`.

## Status model

`FinishStatus { Finished, DidNotStart, DidNotFinish, Disqualified }` stored on `Entrant` (+ `StatusReason`, `StatusUpdatedAt`). Effective status derives with the finish row:
- has finish row → `Finished`, unless `Disqualified` (DSQ)
- no finish row → DNF by default, or `DidNotStart` (DNS)

DSQ is presentation-only: stored finish bib rows are never rewritten; display positions close up (AC3 note).

## Steps

- [ ] **Step 1 — Models.** `FinishStatus` enum; `Entrant.Status/StatusReason/StatusUpdatedAt`; `ResultRecord.DisplayPosition/Status/StatusReason`; `ResultsPageViewModel.DnsEntrants/DsqResults`; `DisqualifyInput`.
- [ ] **Step 2 — DbContext + migration** (`AddFinishStatus`).
- [ ] **Step 3 — Service.** Collation excludes DSQ and assigns sequential `DisplayPosition`; `GetDsqResults`, `GetDnsEntrants`; `GetDnfEntrants` excludes DNS; `GetRaceStats` excludes DNS; `DisqualifyResult(position, reason)`, `ReinstateResult(position)`, `SetNonFinisherStatus(bib, status)`.
- [ ] **Step 4 — Champions (AC5).** `VoidDisqualifiedAndRecalculateAsync(eventId)`: append `Voided` audit entries for DSQ entrants' latest awards, then season recalc (collation already excludes DSQ from top-ten).
- [ ] **Step 5 — Controller.** `Results` populates new sections; `Disqualify` GET/POST (reason required) → void+recalc when C2C; `Reinstate` POST → recalc; `SetStatus` POST (DNS/DNF toggle).
- [ ] **Step 6 — Results view.** Display positions; DSQ section (reason + Reinstate); DNS section; DNF/DNS toggle buttons; `Disqualify` form view.
- [ ] **Step 7 — PDF + CSV (AC6).** PDF gains DNF and DSQ sections; CSV includes DSQ and DNS rows (Status column) so exports stay faithful.
- [ ] **Step 8 — Tests + build + docs.** Status transitions, DSQ removal + reinstatement, DNS exclusion from DNF/stats, Champions void+recalc; README; US16 → ✅.

## Story traceability

| AC | Covered by |
|----|------------|
| 1 status, default DNF, settable DNS | Steps 1, 3 |
| 2 DSQ from Results, reason required | Steps 5, 6 |
| 3 DSQ removes + renumbers, reversible | Steps 3, 5 |
| 4 DNS excluded from DNF/stats/PDF | Steps 3, 7 |
| 5 DSQ voids Champions points + recalc | Step 4 |
| 6 PDF DNF + DSQ sections | Step 7 |
| 7 status changes recorded (reason + timestamp) | Steps 1, 3 |
