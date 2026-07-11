# US44 - Champions of Champions Detailed Per-Event Breakdown

**Status:** ✅ Complete

## As a
Race organiser (and public viewer)

## I want
A "Show details" toggle on the Champions of Champions leaderboard that expands the summary into a per-event table — each scored round as a column, each runner as a row, showing the points scored in every event — with the exports able to render the same way

## So that
I can see *where* each runner's cumulative total came from (which rounds they scored in and how strongly), not just the season total, and share that breakdown in the PDF/CSV

---

## Background

Today the Champions of Champions leaderboard ([US14](US14-champions-of-champions-leaderboard.md)) shows one **summary** row per runner per category: Rank, Name, Club, Events (race count), Points. It appears on two surfaces — the admin leaderboard (`Views/Champions/Leaderboard.cshtml`) and the public shared page (`Views/Public/Champions.cshtml`) — and is exported as PDF and CSV from `ChampionsController`.

The per-event points that make up each runner's total are **already stored**: `PointsAuditLog` holds one row per runner per event per category with `PointsAwarded`, and `ChampionsOfChampionsService.AggregateAudits` already reduces those to the summary totals. So the detailed breakdown is derivable from existing data — **no schema change** is expected.

The Champions season is the May–September Crown to Crown window; the leaderboard already supports an "as of" selected event so it can be viewed at any point in the season.

## Acceptance Criteria

1. **Toggle.** Both the admin leaderboard and the public Champions page gain a "Show details" / "Show summary" toggle. The default view is the existing **summary**; toggling to details reveals the per-event breakdown table. Toggling back returns to the summary.

2. **Detail table shape.** In detail view, within each category (Male / Female / Male U18 / Female U18):
   - **Rows** are runners, **ordered by total points descending**, with the existing tie-break (more races ranks higher) — i.e. the same order and ranking as the summary.
   - **Columns** are the scored events, in date order, followed by the aggregate columns.
   - Each **cell** shows the points that runner scored in that event.

3. **Which columns.** Event columns are the **May–September scored Crown to Crown events up to and including the selected "as of" event** (respecting the existing as-of filter), in date order. Non-scoring fixtures (Good Friday, Boxing Day) are **not** shown as columns.

4. **Column labels.** Each event column is headed with its round number and month, e.g. "Round 1 – May", "Round 2 – June", following the season order. (The event's own name/date should remain discoverable, e.g. via a tooltip/title, without widening the header.)

5. **Empty cells.** An event in which a runner scored no Champions points shows **blank (or "–")**. (No distinction is drawn between "did not run" and "ran but finished outside the top 10".)

6. **Aggregates retained.** The detail table keeps the **Points total** and **Events (race count)** columns alongside the per-event columns, keeps **category grouping**, and keeps the **gold / silver / bronze top-3 highlighting** and the tied (†) marker exactly as the summary does.

7. **Row total consistency.** Each runner's per-event points across the visible columns sum to the Points total shown on their row (for the same as-of scope), so the breakdown always reconciles with the summary.

8. **PDF export.** The Champions PDF can render the detailed per-event layout — category sections, one column per scored round (labelled as in AC4), points per cell, blank where none, and the Points/Events aggregate columns with top-3 highlighting — matching the on-screen detail view. Wide seasons must remain legible on the page (e.g. sensible column sizing / orientation).

9. **CSV export.** The Champions CSV can render the detailed layout: one column per scored round (plus Category, Rank, Name, Club, and the Points/Events aggregates), one row per runner, blank cells where no points were scored. Existing UTF-8-with-BOM behaviour (accented names open correctly in Excel) is preserved.

10. **Export follows the view.** Whether an export produces the summary or the detailed layout follows the current on-screen toggle state (so a viewer exports what they are looking at); the existing summary exports remain available when the toggle is on summary.

11. **Empty / early-season.** With no scored events yet (or none up to the as-of event), the detail view degrades gracefully — the same "no data for this season yet" message as the summary, no empty column skeleton.

12. **No scoring change.** This is a presentation feature only: points, ranking, tie-breaks, the as-of behaviour, and the May–September window are unchanged. Editing a past result still recalculates as today, and the detail view reflects the recalculated per-event points.

## Notes

- Purely additive to [US14](US14-champions-of-champions-leaderboard.md); reuses the `PointsAuditLog` per-event awards and the existing `AggregateAudits` ranking. Expect a new per-event projection in `ChampionsOfChampionsService` (e.g. a matrix of runner × event points) rather than any new persisted table.
- Applies to both surfaces from [US21](US21-public-results-page.md) (public page) and the admin leaderboard; keep the two views visually consistent.
- Column count grows through the season (up to the full May–September set). Keep the wide table readable — horizontal scroll on screen and careful column sizing / landscape in the PDF are acceptable approaches.
- Unit coverage should assert the per-event matrix reconciles with the summary totals (AC7) and that as-of scoping limits the columns (AC3), building on `ChampionsOfChampionsServiceTests`.
- The [same-course convention](../docs/domain-conventions.md) and the [Champions scoring window](../docs/domain-conventions.md) are unaffected.
