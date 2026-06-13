# US27 — Example Upload File Links — Implementation Summary

**Status:** ✅ Complete — build green, 99/99 tests passing. Publish verified the assets ship.

## Files changed

**Created**
- `RaceResults.Web/wwwroot/example-files/{online-registration,on-the-day-1,on-the-day-2,bib-position}.xlsx`, `timings.csv` — in-app copies of the canonical examples.

**Modified**
- `RaceResults.Web/Views/Race/Uploads.cshtml` — each section now shows an "Expected columns" summary (required marked with `*`) and "Download example file" links; added a `* marks a required column` legend.
- `README.md` — feature row, mirror-maintenance note, story tables, intro line.
- `user-stories/US27-example-file-links.md` — Status → ✅ Complete.

## Decisions

- **Serving mechanism (AC2):** physically copied the examples into `wwwroot/example-files/` and committed them, rather than a build-time copy target. The app uses .NET 10 `MapStaticAssets`, whose manifest is built from files present in `wwwroot` at build time; committed files are deterministic and verified to ship (a `dotnet publish` placed them — pre-compressed — under `wwwroot/example-files/`). The dual-maintenance cost is documented in the README, as the story sanctions.
- **No new tests:** static assets + a Razor view only; the existing `UploadControllerTests` GET of `/Race/Uploads` still passes, exercising the changed view.

## Acceptance criteria — all met (1–4).
