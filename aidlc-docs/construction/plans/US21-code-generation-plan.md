# US21 — Public Results Page — Code Generation Plan

**Story:** [US21](../../../user-stories/US21-public-results-page.md)
**Type:** Brownfield + schema change. Reuses the `eventId` read-only accessors added in US20.

## Design

- `RaceEvent.IsPublished` + `PublicToken` (unguessable `Guid.N`). Public URLs key on the token, never the id (AC5).
- New `PublicController` (no auth, no management affordances) on `/public/*`, rendered with a minimal `_PublicLayout` (no admin nav).
- Unpublished/unknown token → 404 (AC6).

## Steps

- [ ] **Step 1 — Schema.** `RaceEvent.IsPublished`/`PublicToken`; migration `AddPublicResults`.
- [ ] **Step 2 — Service.** `PublishEvent`/`UnpublishEvent` (generate token on first publish); `GetPublishedEventByToken(token)`.
- [ ] **Step 3 — Models.** `PublicResultsViewModel`, `PublicChampionsViewModel`.
- [ ] **Step 4 — Public controller + views.** `/public/results/{token}` (event header, collated results, category winners, DNF; mobile-friendly; client-side name/club/bib filter; unmatched shown as "Unknown runner" — AC8) and `/public/champions/{token}` (season leaderboard with gold/silver/bronze). Minimal `_PublicLayout`.
- [ ] **Step 5 — Events page.** Publish/Unpublish actions + copy-link field.
- [ ] **Step 6 — Tests.** Published token returns results; unpublished/unknown token 404; unmatched neutral label; publish/unpublish toggles.
- [ ] **Step 7 — Build + docs.** Full suite green; README; US21 → ✅.

## Story traceability

| AC | Covered by |
|----|------------|
| 1 token URL, no admin affordances | Steps 1, 4 |
| 2 results + winners + DNF | Step 4 |
| 3 mobile + client filter | Step 4 |
| 4 public Champions page | Step 4 |
| 5 unguessable token | Steps 1, 2 |
| 6 publish/unpublish, 404 | Steps 2, 5 |
| 7 copy link | Step 5 |
| 8 neutral unmatched label | Step 4 |
