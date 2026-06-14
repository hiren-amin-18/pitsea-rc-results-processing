# US21 — Public Results Page — Implementation Summary

**Status:** ✅ Complete — build green, 162/162 tests passing (136 unit + 26 integration). Pairs with US20.

## Files changed

**Created**
- `Migrations/…_AddPublicResults.*` — `RaceEvent.IsPublished` + `PublicToken`.
- `Models/PublicViewModels.cs`.
- `Controllers/PublicController.cs` (`/public/results/{token}`, `/public/champions/{token}`).
- `Views/Public/_PublicLayout.cshtml`, `Results.cshtml`, `Champions.cshtml`.
- Tests: `PublicControllerTests.cs` (4).

**Modified**
- `Models/RaceEvent.cs` — `IsPublished`, `PublicToken`.
- `Services/IRaceResultsService.cs` + `RaceResultsService.cs` — `PublishEvent`/`UnpublishEvent`/`GetPublishedEventByToken`.
- `Controllers/EventsController.cs` — `Publish`/`Unpublish` actions.
- `Views/Events/Index.cshtml` — Publish/Unpublish + copy-link row.
- `README.md`, `user-stories/US21-public-results-page.md`.

## Decisions

- **Token (AC5):** `Guid.NewGuid().ToString("N")` — 128-bit, generated on first publish, retained across unpublish/republish so links keep working.
- **404 semantics (AC6):** `GetPublishedEventByToken` returns null for unknown tokens *and* tokens belonging to unpublished events; the controller returns `NotFound()` in both cases — readers can't enumerate unpublished events.
- **Minimal public layout:** standalone `_PublicLayout` (no admin nav, no theme toggle, no Settings link) so the public pages can't surface any management affordance (AC1).
- **Neutral unmatched (AC8):** the public view shows `Unknown runner` instead of the internal `Unmatched` badge; the integration test asserts both the positive label and the absence of the warning copy.
- **Champions reuse:** the public Champions page uses the existing leaderboard service unchanged, just with the public layout and top-3 highlighting inline (matches the internal styling per AC4).

## Acceptance criteria — all met (1–8).
