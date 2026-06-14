# US30 — End of Season Review — Code Generation Plan

**Story:** [US30](../../../user-stories/US30-end-of-season-review.md)
**Type:** Brownfield, no schema change. Capstone aggregation. Degraded: US29 (volunteer) sections omitted by configuration.

## Design

- `SeasonReview` model bundles all sections + a `Sections` enum of what's actually populated (so PDF/view degrade gracefully — AC4).
- `ISeasonReviewService` builds the review by composing existing services: `SeasonStatisticsService` (US24), `ChampionsOfChampionsService` (US14), `CourseRecordService` (US22), `RaceResultsService` for entrant/finish counts. **Single source of calculation** (AC7) — every figure goes through these.
- Year-on-year deltas (AC5): re-call the same composition with `year-1`, populate `Comparison` only when prior data exists.
- Archived events (AC6) contribute identically because the underlying services don't filter by archive state.
- US29 volunteer sections (`MostActiveVolunteers`, `EverPresentVolunteers`, `CombinedParticipation`) are part of the model but always empty for now; the view/PDF omits them — graceful degradation built in (AC4).

## Steps

- [ ] **Step 1 — Models.** `SeasonReview` + sub-DTOs (`SeasonHeadlines`, `SeasonComparison`, `AwardsList`).
- [ ] **Step 2 — Service.** `SeasonReviewService.Build(year)` composing the existing services; awards list derived from the same data (AC7).
- [ ] **Step 3 — Controller + view.** `SeasonController.Review(year)` + `Views/Season/Review.cshtml` (mobile-responsive, sections labelled "Full series" vs "Champions May–Sep" — AC3).
- [ ] **Step 4 — PDF export.** `SeasonController.ReviewPdf(year)` — branded QuestPDF with the same header style.
- [ ] **Step 5 — Tests.** Headlines + comparison (no prior year → no comparison); awards match underlying stats; archived events contribute; degraded volunteer sections absent.
- [ ] **Step 6 — Build + docs.** Full suite green; README; US30 → ✅.

## Story traceability

| AC | Covered by |
|----|------------|
| 1 review page | Steps 2, 3 |
| 2 PDF export | Step 4 |
| 3 full series vs scoring window labelled | Step 3 |
| 4 graceful degradation | Steps 1, 3, 4 |
| 5 YoY deltas when prior data | Step 2 |
| 6 archived events same | Step 2 |
| 7 awards match stats | Step 2 |
