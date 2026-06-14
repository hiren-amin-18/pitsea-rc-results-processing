# US25 — Application Installer — Code Generation Plan

**Story:** [US25](../../../user-stories/US25-app-installer.md)
**Type:** Brownfield. Code change: per-user DB location. Deliverables: publish profile, installer script, packaging script, launcher, docs.

## Design

- **DB location (AC3, AC4):** when no `ConnectionStrings:DefaultConnection` is set, default to `%LOCALAPPDATA%\PitseaRaceResults\raceresults.db` (Linux fallback: `~/.local/share/PitseaRaceResults/raceresults.db`) and create the directory if missing. An explicit connection string still wins, so `dotnet run` from the repo behaves the same as today.
- **Publish profile** (AC1, AC6): `RaceResults.Web/Properties/PublishProfiles/win-x64-installer.pubxml` — `win-x64`, self-contained, single-file, `IncludeNativeLibrariesForSelfExtract`, ready-to-run, Release.
- **Inno Setup script** (AC1, AC2, AC5): `installer/PitseaRaceResults.iss` with Start Menu shortcut + uninstall option to also remove `%LOCALAPPDATA%\PitseaRaceResults`. Inno is the standard Windows installer for .NET apps and is free.
- **Zip-and-shortcut fallback** (AC1, AC6): a one-step PowerShell script `installer/build-installer.ps1` that publishes, optionally compiles the Inno script if Inno is on PATH, and otherwise produces a versioned `.zip` — so the release is repeatable without forcing a tool dependency.
- **Launcher (AC2):** a small `PitseaRaceResults.cmd` next to the exe that starts the published app and opens the default browser at the listening URL.
- **Docs:** README installer section + `installer/README.md` with non-technical install/upgrade/uninstall steps.

## Steps

- [ ] **Step 1 — Per-user DB default.** `Program.cs`: resolve a sensible per-user path when no connection string is configured; create the directory.
- [ ] **Step 2 — Publish profile** (win-x64 self-contained, single-file, R2R).
- [ ] **Step 3 — Inno Setup script** + uninstall data-removal option.
- [ ] **Step 4 — Build script.** PowerShell wrapper: publish → installer (if ISCC present) or zip fallback; takes `-Version`.
- [ ] **Step 5 — Launcher .cmd** that starts the exe and opens `http://localhost:5200` in the default browser.
- [ ] **Step 6 — Tests.** Unit test that the per-user default resolves to a non-empty path under `LOCALAPPDATA` on Windows / `XDG_DATA_HOME`-style fallback elsewhere.
- [ ] **Step 7 — Docs + verification.** README + `installer/README.md`; verify `dotnet publish` against the profile actually produces a self-contained build.

## Story traceability

| AC | Covered by |
|----|------------|
| 1 self-contained installer/zip | Steps 2, 3, 4 |
| 2 Start Menu + auto-open browser | Steps 3, 5 |
| 3 per-user DB outside cloud-synced folder | Step 1 |
| 4 upgrade preserves DB | Steps 1, 3 (migrations + per-user path unchanged across versions) |
| 5 uninstall keeps data with explicit removal option | Step 3 |
| 6 repeatable versioned artefact | Steps 2, 4 |
