# US16 — Finish Status (DNS / DNF / DSQ) — Implementation Summary

**Status:** ✅ Complete — build green, 138/138 tests passing (116 unit + 22 integration).

## Files changed

**Created**
- `Models/FinishStatus.cs` (enum), `Models/DisqualifyInput.cs`.
- `Views/Race/Disqualify.cshtml`.
- `Migrations/…_AddFinishStatus.*` — `Entrant.Status` (default Finished) + `StatusReason` + `StatusUpdatedAt`.
- Tests: `FinishStatusTests.cs` (5).

**Modified**
- `Models/Entrant.cs` — `Status`/`StatusReason`/`StatusUpdatedAt`.
- `Models/ResultRecord.cs` — `DisplayPosition`, `Status`, `StatusReason`.
- `Models/ResultsPageViewModel.cs` — `DnsEntrants`, `DsqResults`.
- `Data/RaceResultsDbContext.cs` — `Status` default.
- `Services/RaceResultsService.cs` — collation excludes DSQ + assigns `DisplayPosition`; `GetDsqResults`, `GetDnsEntrants`; `GetDnfEntrants`/`GetRaceStats` exclude DNS; `DisqualifyResult`/`ReinstateResult`/`SetNonFinisherStatus`; PDF gains DNF + DSQ sections; CSV gains DSQ + DNS rows and uses `DisplayPosition`.
- `Services/IRaceResultsService.cs` — new members.
- `Services/IChampionsOfChampionsService.cs` + `ChampionsOfChampionsService.cs` — `VoidDisqualifiedAndRecalculateAsync` (consumes `AuditAction.Voided`).
- `Controllers/RaceController.cs` — `Disqualify` GET/POST, `Reinstate`, `SetStatus`; `Results` populates new sections.
- `Views/Race/Results.cshtml` — display positions, DSQ button, DSQ/DNS sections, DNF↔DNS toggles.
- `README.md`, `user-stories/US16-finish-status-dns-dnf-dsq.md`.

## Decisions

- **Status model:** a single `FinishStatus` enum on `Entrant`. Effective status derives with the finish row — a non-finisher is DNF unless explicitly DNS; a finisher is Finished unless DSQ. Statuses are set post-upload and are reset by the existing destructive re-upload semantics (acceptable).
- **DSQ is presentation-only (AC3):** stored finish bib rows are untouched; collation filters DSQ and assigns a sequential `DisplayPosition`, while `Position` keeps the stored value so Edit/DSQ links remain stable. PDF/CSV use `DisplayPosition`.
- **Champions voiding (AC5):** because aggregation uses the latest batch per event, removing one runner needs a full season recalc (collation already excludes DSQ). `VoidDisqualifiedAndRecalculateAsync` additionally appends explicit `Voided` audit entries for the DSQ runner's prior awards as the audit trail the AC requires, then recalculates.
- **Orchestration:** the singleton `RaceResultsService` records the status; the `RaceController` (which already holds the scoped Champions service) triggers void/recalc — same pattern as upload/edit.

## Acceptance criteria — all met (1–7).
