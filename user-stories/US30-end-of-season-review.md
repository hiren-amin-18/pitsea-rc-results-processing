# US30 - End of Season Review

**Status:** ✅ Complete (degraded — volunteer sections omitted until US29 lands)

## As a
Race organiser

## I want
A consolidated end-of-season review — one page and one exportable document covering the whole year's racing and volunteering

## So that
The club has a single artefact for the AGM and awards night, instead of assembling figures by hand from several pages

---

## Background

US24 (runner season statistics), US29 (volunteer statistics), and US14 (Champions of Champions) each produce part of the year's story, on separate pages. The end-of-season review is the capstone that combines them into one view and one branded PDF.

## Season Definition

The review covers the club's **full racing year**, which is wider than the Champions scoring window:

- **Crown to Crown series** runs Good Friday (11:00) through Boxing Day (11:00):
  - First event: Good Friday (March/April, moves with Easter)
  - May–August: second Wednesday of each month, 19:30
  - September: first **or** second Wednesday (decided per year), 19:00
  - Final event: Boxing Day, 26 December
- **Champions of Champions** scores only the May–September window — the Good Friday and Boxing Day races are part of the series but deliberately earn no points
- **Bluebell 5** runs once a year, around April/May (date varies)

The review therefore reports on all events in the calendar year, while clearly distinguishing the Champions-scoring races from the rest.

## Review Content

### Headlines
1. Events held, total entrants, total finishers, total unique runners (US15), completion rate for the year
2. Year-on-year comparison with the previous season ("entries up 12%") where prior-year data exists

### Champions of Champions
3. Final standings per category with gold/silver/bronze highlighting — the season's headline competition result
4. Number of distinct points scorers and races contributing

### Runner recognition (from US24)
5. Most attended runner(s) and ever-present badges — attendance counted across the **whole series** including Good Friday and Boxing Day, not just scoring races
6. Fastest in each category per event type; most improved runner
7. Most attended club

### Volunteer recognition (from US29)
8. Most active volunteers and ever-present volunteer badges
9. Combined run+volunteer participation ("involved in all events this year")

### Records and notables
10. Course records set during the year (US22), DNF/DSQ summary (US16) where those stories are implemented — the review degrades gracefully, omitting sections whose source feature is absent

## Awards List

11. A dedicated "awards" view listing, per award, the winner(s) ready for trophy engraving / certificates:
    - Champions of Champions winner per category (4 awards)
    - Ever-present runner(s)
    - Ever-present volunteer(s)
    - Most improved runner
    - Club to confirm the definitive award list — the set above is configurable, not hard-coded

## Acceptance Criteria

1. A season review page exists, selectable by year, combining the sections above.
2. The review is exportable as a single branded PDF in the club's existing style, suitable for the AGM and for publishing.
3. Attendance-based figures count the full series year (Good Friday through Boxing Day plus Bluebell 5); Champions figures use only the May–September scoring window. The distinction is labelled wherever both appear.
4. Sections degrade gracefully: where a dependency (US16/US22 etc.) is not yet implemented, that section is omitted rather than shown empty.
5. Year-on-year deltas appear only when the prior season has data.
6. The review is read-only and reflects archived events (US20) identically to active ones.
7. Awards list winners match the underlying statistics pages exactly (single source of calculation in the service layer, covered by unit tests).

## Notes

- **Depends on:** [[US24-season-statistics]] and [[US29-volunteer-stats]] (which themselves require US15 and US28). Benefits from US16, US17, US22 where present. This is the capstone of the statistics roadmap — likely the last story in that chain to build.
- A natural follow-up (not in scope here): a **season calendar generator** that pre-creates the year's C2C events from the date rules above (Good Friday calculation, second-Wednesday logic, per-year September choice). Events currently store only a date; the series' start times (11:00 / 19:30 / 19:00) would need an optional event time field.
- Pairs with [[US21-public-results-page]]: the review PDF (or page) is a strong candidate for public sharing at season end.
