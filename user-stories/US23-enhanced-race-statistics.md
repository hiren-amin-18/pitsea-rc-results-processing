# US23 - Enhanced Race Statistics

**Status:** 📋 Planned

## As a
Race organiser

## I want
The race statistics page to include completion, gender split, and finish-time summary figures

## So that
I can quote the headline numbers a race report needs (completion rate, splits, median time) without working them out by hand

---

## Background

The Stats page currently shows category totals (Male/Female, U18, unaffiliated) and two charts (club breakdown, finishers per minute). It never relates entrants to finishers, shows no percentages, and says nothing about finish times. All of the figures below are derivable from data already in the database — no schema changes.

## New Statistics

### Completion summary
- Entrants, finishers, and DNF as counts
- Completion rate as a percentage (e.g. "142 of 156 entrants finished — 91%")

### Gender split percentages
- Male / Female as percentages of finishers, alongside the existing counts
- Affiliated vs unaffiliated split shown as a visual breakdown (counts already exist in `RaceStats` but are not charted)

### Finish time summary
- Winner's time, median time, and average time
- Winner-to-median spread (a rough course-difficulty indicator)
- Time percentiles: the cut-off times for the top 25%, 50%, and 75% of finishers

### Busiest finish window
- A one-line summary above the existing finishers-per-minute chart, e.g. "Peak: 14 finishers between 24:00 and 25:00" (useful for planning funnel staffing)

## Acceptance Criteria

1. The Stats page shows a completion summary card: entrant count, finisher count, DNF count, and completion rate percentage.
2. Gender cards show percentage of finishers alongside the existing counts.
3. An affiliated vs unaffiliated chart is added using the existing `RaceStats` figures.
4. A finish time summary card shows winner, median, and average times plus 25th/50th/75th percentile cut-offs.
5. Times that cannot be parsed are excluded from time-based statistics, and the page states how many rows were excluded (rather than excluding them silently). This caveat disappears once US17 lands.
6. The busiest finish window summary appears with the finishers-per-minute chart and matches its data.
7. All new figures are scoped to the current event, consistent with the rest of the Stats page.
8. New calculations live in the service layer with unit test coverage (per-statistic tests including empty-race and DNF-heavy edge cases).

## Notes

- **Age-based statistics are deliberately excluded.** The club only records ages for under-18 participants; a blank age means "adult" by convention. Age histograms, veteran categories (V40/V50/V60), and age-grading are therefore not possible from current data and are out of scope unless the entry form starts capturing adult age bands.
- Median/percentile calculations should use the typed-duration parsing that US17 will formalise; until then, `TimeSpan.TryParse` with exclusion counting (AC5) is acceptable.
- DNS runners (US16, when implemented) should be excluded from the completion-rate denominator; until then, all entrants count.
