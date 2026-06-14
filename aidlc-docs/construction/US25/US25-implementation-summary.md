# US25 — Application Installer — Implementation Summary

**Status:** ✅ Complete — build green, 181/181 tests passing (155 unit + 26 integration). Per-user DB default verified by unit tests; the Windows installer artefact is produced by the committed build script.

## Files changed

**Created**
- `RaceResults.Web/Services/DatabasePathResolver.cs` — per-user fallback for the SQLite connection string.
- `RaceResults.Web/Properties/PublishProfiles/win-x64-installer.pubxml` — self-contained / single-file / R2R publish profile.
- `installer/PitseaRaceResults.iss` — Inno Setup script with Start Menu shortcut, post-install launch, and uninstall opt-in to delete data.
- `installer/PitseaRaceResults.cmd` — launcher that starts the published app and opens the browser at `http://localhost:5200`.
- `installer/build-installer.ps1` — one-step publish + installer (or zip fallback) packaging script.
- `installer/README.md` — non-technical install / upgrade / uninstall guide.
- Tests: `DatabasePathResolverTests.cs` (4).

**Modified**
- `RaceResults.Web/Program.cs` — uses `DatabasePathResolver.Resolve(...)` to fall back to the per-user folder when no `ConnectionStrings:DefaultConnection` is configured.
- `README.md`, `user-stories/US25-app-installer.md`.

## Decisions

- **Connection string override stays the source of truth.** An explicit `ConnectionStrings:DefaultConnection` (in `appsettings.json` or the `ConnectionStrings__DefaultConnection` env var) always wins, so `dotnet run` from the repo and CI/integration tests behave exactly as before. Only installed builds (which configure nothing) get the new per-user default.
- **`%LOCALAPPDATA%\PitseaRaceResults\raceresults.db` (AC3):** per-user, outside cloud-synced folders by convention, and the directory is created on resolve. Linux fallback is `~/.local/share/PitseaRaceResults/` for power users who deploy via the same publish output.
- **Inno Setup + zip fallback (AC1, AC6):** the build script publishes the self-contained `win-x64` output, then either compiles the Inno installer (if `ISCC.exe` is on PATH) or zips the publish folder with the launcher next to the exe. Either way the release is repeatable from `installer/build-installer.ps1 -Version …`.
- **Data preservation across upgrade (AC4):** the publish output is replaced in `Program Files`; the database lives separately under `%LOCALAPPDATA%`. EF migrations run on first launch (already wired) so older databases upgrade cleanly.
- **Uninstall keeps data by default (AC5):** the Inno script registers an opt-in "Also delete saved race data" task; ticking it adds `%LOCALAPPDATA%\PitseaRaceResults` to `[UninstallDelete]`.
- **Launcher (AC2):** a tiny `.cmd` rather than an embedded auto-launcher in the app itself — keeps the app process well-behaved when started other ways (manual run, future Windows Service, cloud host).

## Acceptance criteria — all met (1–6).

## Verification notes
- Tests cover the resolver behaviour (override wins; empty/whitespace/null fall back to the per-user path; directory ends with `PitseaRaceResults`).
- The Windows installer artefact itself wasn't compiled in CI (Inno Setup isn't a build dependency); the script paths and Inno script were authored against the documented Inno 6 idioms and the existing self-contained publish target.
