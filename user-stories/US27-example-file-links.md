# US27 - Example Upload File Links

**Status:** ✅ Complete

## As a
Race organiser

## I want
Download links to example upload files on the Uploads page

## So that
Anyone preparing entrant, finish bib, or timing files can see exactly the expected format without digging through the repository

---

## Background

Correctly formatted example files already exist in `user-stories/example-files/` (online registration, on-the-day entrants, bib-position, timings CSV), but they are only reachable through the source tree. New volunteers preparing files have no in-app reference.

## Acceptance Criteria

1. Each upload section on the Uploads page (entrants, finish bib, timings) offers a "Download example file" link for its format.
2. The example files are served by the app (copied into `wwwroot` or embedded), not referenced from the `user-stories/` folder, so they work in published/installed builds (US25/US26).
3. Examples match the documented accepted header names in the README and stay representative: multiple entrant files, a realistic timing CSV including `STARTOFEVENT`/`ENDOFEVENT` rows.
4. A short "expected columns" summary is shown next to each upload form (required vs optional columns), consistent with the README's Upload File Formats section.

## Notes

- Deliberately tiny story — mostly static assets plus view changes.
- Keep the canonical copies in `user-stories/example-files/` for documentation, and copy at build time (or document that both locations must be updated together).
