# US30 — End of Season Review — Implementation Summary

**Status:** ✅ Complete (degraded) — build green, 176/176 tests passing (150 unit + 26 integration).

## Files changed

**Created**
- `Models/SeasonReview.cs` — capstone view model + `SeasonHeadlines`, `SeasonComparison`, `AwardsList`, `AwardEntry`.
- `Services/ISeasonReviewService.cs` + `SeasonReviewService.cs` — `Build(year)` and `GeneratePdf(year)`.
- `Views/Season/Review.cshtml`.
- Tests: `SeasonReviewTests.cs` (3).

**Modified**
- `Controllers/SeasonController.cs` — `Review` + `ReviewPdf` actions.
- `Views/Season/Dashboard.cshtml` — link to the review.
- `Program.cs` — registered `ISeasonReviewService`.
- `README.md`, `user-stories/US30-end-of-season-review.md`.

## Decisions

- **Capstone composition (AC1, AC7):** the review is built by composing the existing services — `SeasonStatisticsService` (US24) for dashboard, `ChampionsOfChampionsService` for leaderboard, `CourseRecord` / `Entrant.Status` reads for records and DNF/DSQ totals. Single source of calculation: the awards list is derived from the same data, so awards and the underlying stats can't diverge.
- **Series vs scoring window (AC3):** the page and PDF both label "full series" (attendance, awards) and "May–September scoring window" (Champions) explicitly.
- **Graceful degradation (AC4):** US29 volunteer sections (`MostActiveVolunteers`, combined participation) are part of the model but always empty; the view shows a small "omitted" note and the PDF skips the block. US16 sections appear if Status is set (the seeded default is fine) — they're integers that naturally read as zero.
- **YoY (AC5):** `TryBuildComparison` re-asks the season-stats service for `year - 1`; if that year has no events, `Comparison` stays null and the view/PDF skips it.
- **Archived events (AC6):** uses the same season-stats path, which never filters by archive flag — archived events contribute identically.
- **PDF (AC2):** QuestPDF in the existing branded style; downloads as `season-review-{year}.pdf`.

## Acceptance criteria — all met (1–7); volunteer-recognition section deliberately empty per the user's roadmap decision.
