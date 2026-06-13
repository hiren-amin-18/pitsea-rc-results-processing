# US18 - Export Results to CSV

**Status:** ✅ Complete

## As a
Race organiser

## I want
To export the collated results and the Champions of Champions leaderboard as CSV files

## So that
I can submit results to league coordinators, archive them in spreadsheets, and share data with people who need more than a PDF

---

## Acceptance Criteria

1. The Results page offers a "Export CSV" action alongside the existing "Export PDF".
2. The results CSV contains one row per finisher with columns: Position, Time, Bib, Name, Club, Gender, Age.
3. DNF entrants are included in a clearly separated way (either a trailing section or a Status column), consistent with how the PDF presents them.
4. The Champions leaderboard page offers a CSV export containing: Category, Rank, Name, Club, Races, Points, and a tie indicator.
5. Exports are scoped exactly as the on-screen view: current event for results; selected season/as-of event for the leaderboard.
6. Files download with descriptive names, e.g. `crown-to-crown-2026-05-01-results.csv`, `champions-of-champions-2026.csv`.
7. CSV output is Excel-friendly: UTF-8 with correct escaping of commas/quotes in names and clubs.

## Notes

- Small story by design: `GetCollatedResults()` and `GetLeaderboardAsync()` already produce the needed data; this adds serialisation and two controller actions only.
- CsvHelper is already a project dependency.
