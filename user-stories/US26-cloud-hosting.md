# US26 - Cloud Hosting

**Status:** 📋 Planned

## As a
Race organiser

## I want
The application hosted in the cloud so it is reachable from anywhere

## So that
Multiple officials can use it without sharing one machine, results survive any single laptop, and public results pages (US21) become reachable by participants

---

## Background

The app currently runs locally on one machine with a local SQLite file. Cloud hosting changes the operating assumptions: multiple concurrent users, an untrusted network, and managed storage.

## Acceptance Criteria

1. The app runs on a chosen cloud host (candidates: Azure App Service, Fly.io, Railway, or a club VPS) with HTTPS.
2. **Authentication is added before anything is exposed**: organiser accounts protect all upload, edit, and event-management functions. Public pages (US21), if enabled, remain anonymous and read-only.
3. Data storage strategy is decided and implemented:
   - either SQLite on a persistent volume with the US19 backup mechanism, or
   - migration to a hosted database (e.g. PostgreSQL), keeping EF Core migrations as the schema mechanism.
4. Configuration (connection string, environment, data paths) comes from environment variables, with no secrets committed to the repository.
5. Automated backups run on a schedule (extends US19).
6. A deployment pipeline exists (e.g. GitHub Actions) so deploying a new version is a single, repeatable action.
7. Local development continues to work unchanged with local SQLite.

## Notes

- **Authentication is the critical prerequisite** — today the app has no login at all, which is only safe because it is local.
- Concurrency: `RaceResultsService` is a singleton using a DbContext factory, which is sound, but upload flows assume one operator; review for two officials uploading to the same event simultaneously.
- This story and [[US25-app-installer]] are alternative deployment paths; the club may keep both (local on race day, cloud for publishing).
- Strongly pairs with [[US21-public-results-page]] (hosting is what makes public links useful) and [[US19-database-backup-restore]].
