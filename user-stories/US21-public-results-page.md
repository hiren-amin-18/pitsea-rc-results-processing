# US21 - Public Results Page

**Status:** ✅ Complete

## As a
Club member or race participant

## I want
To view race results via a shareable read-only link

## So that
Results can be published online immediately without giving anyone access to editing or upload functions

---

## Background

Results currently live only inside the organiser's app; publication means exporting a PDF and posting it elsewhere. A read-only web view gives participants live, searchable results on their phones and removes the temptation to share access to the full application.

## Acceptance Criteria

1. Each event has a public results URL (e.g. `/public/results/{token}`) that requires no login and exposes no edit, upload, or event-management affordances.
2. The public page shows: event name and date, collated results (position, time, bib, name, club, gender), category winners, and the DNF list — consistent with the PDF content.
3. The page is mobile-friendly and supports quick name/club/bib filtering client-side.
4. A public Champions of Champions leaderboard page exists for the season, with the same gold/silver/bronze top-3 highlighting as the internal view.
5. Public URLs use an unguessable token per event (not the sequential database id), so unpublished events cannot be enumerated.
6. The organiser explicitly publishes/unpublishes an event from the Events page; unpublished events return 404 on their public URL.
7. The organiser can copy the public link from the Events page.
8. Unmatched-bib placeholder rows are presented neutrally (e.g. name shown as "Unknown runner") rather than with internal warning language.

## Notes

- Depends on the app being hosted somewhere reachable by participants; for purely local use, the page still works on the local network.
- Personal data consideration: the public page should show only what the club already publishes in its PDF (no ages if the PDF omits them — align the two).
- Pairs naturally with US20: publishing an archived event guarantees the public view never changes underneath readers.
