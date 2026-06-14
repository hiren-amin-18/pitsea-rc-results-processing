# US25 - Application Installer

**Status:** ✅ Complete

## As a
Race organiser (non-technical user)

## I want
To install and run the application like a normal desktop program, without needing the .NET SDK or command line

## So that
Any committee member can set the app up on their machine without developer help

---

## Background

Running the app currently requires cloning the repository, installing the .NET 10 SDK, and using `dotnet run`. That limits who can operate it on race day.

## Acceptance Criteria

1. A Windows installer (or self-contained published build) is produced that runs without a separately installed .NET SDK/runtime.
2. Installation creates a Start Menu shortcut; launching it starts the web app and opens the browser at the app URL.
3. The database is created in a sensible per-user location by default (e.g. `%LOCALAPPDATA%\PitseaRaceResults\raceresults.db`) — explicitly **not** a cloud-synced folder (see the SQLite storage caution in the README) and not the install directory.
4. Upgrading to a new version preserves the existing database (migrations run on first start, as today).
5. Uninstalling leaves the database behind by default, with a clear option to remove data.
6. A versioned release artefact can be produced repeatably (e.g. `dotnet publish` profile plus installer script committed to the repository).

## Notes

- Candidate approaches: self-contained `dotnet publish` + Inno Setup / MSIX, or a simple zip-and-shortcut distribution as a first step.
- This story targets local, single-machine use, which is the club's chosen deployment model; cloud hosting was considered and dropped as unnecessary.
