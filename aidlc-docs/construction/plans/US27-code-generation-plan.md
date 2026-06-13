# US27 — Example Upload File Links — Code Generation Plan

**Story:** [US27](../../../user-stories/US27-example-file-links.md)
**Type:** Brownfield. Mostly static assets + one view.

## Steps

- [ ] **Step 1 — Static assets.** Copy the canonical examples from `user-stories/example-files/` into `RaceResults.Web/wwwroot/example-files/` (the four formats: `online-registration.xlsx`, `on-the-day-1.xlsx`, `on-the-day-2.xlsx`, `bib-position.xlsx`, `timings.csv`). Committed so they ship in published builds (AC2). `MapStaticAssets` serves them at `/example-files/...`.
- [ ] **Step 2 — Frontend.** On `Views/Race/Uploads.cshtml`, add to each section (entrants, finish bib, timings): a "Download example file" link (or links) and a short "expected columns" summary (required vs optional) consistent with the README (AC1, AC3, AC4).
- [ ] **Step 3 — Docs.** README: note that example files are mirrored into `wwwroot/example-files/` for in-app download and that both locations must be updated together (AC2 maintenance note). Mark US27 complete; move story tables; bump nothing test-wise (no new tests — static assets + view only).
- [ ] **Step 4 — Build & verify.** `dotnet build` green; confirm the files land in the build output and the links resolve.

## Story traceability

| AC | Covered by |
|----|------------|
| 1 download link per section | Step 2 |
| 2 served by app / published builds | Step 1 |
| 3 examples match README headers, realistic | Step 1 (existing canonical files) |
| 4 expected-columns summary | Step 2 |
