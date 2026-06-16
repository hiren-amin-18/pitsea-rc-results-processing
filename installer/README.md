# Pitsea RC Race Results — Installer (US25)

This folder packages the app as a self-contained Windows release so non-technical
committee members can install and run it without the .NET SDK.

## What ships

| File | Purpose |
|---|---|
| `build-installer.ps1` | Publishes the self-contained build and produces the installer (or a zip fallback) |
| `PitseaRaceResults.iss` | Inno Setup script — Start Menu shortcut, uninstall with optional data removal |
| `PitseaRaceResults.cmd` | Launcher that starts the published app and opens the browser at `http://localhost:5200` |

## Building a release

From the repository root:

```powershell
.\installer\build-installer.ps1 -Version 2026.06.14
```

The script publishes `RaceResults.Web` with the `win-x64-installer` profile
(self-contained, single-file, ready-to-run), then either compiles the Inno
Setup installer or produces a zip with the launcher next to the exe. Both
artefacts land in `dist/`.

To compile the installer, install [Inno Setup](https://jrsoftware.org/isdl.php)
(v6 or v7). The script finds `ISCC.exe` on PATH, or by probing the standard
install locations (`%ProgramFiles%\Inno Setup 6|7\` and the `(x86)` variants),
so no PATH edit is needed. If `ISCC.exe` isn't found anywhere, the script
produces the zip fallback only.

## Installing (non-technical user)

1. Run `PitseaRaceResults-Setup-<version>.exe` (or unzip the fallback and run
   `PitseaRaceResults.cmd`).
2. The installer adds a **Pitsea RC Race Results** Start Menu shortcut.
3. Launching it starts the app and opens your browser at `http://localhost:5200`.

## Where the database lives

On first launch the app creates `raceresults.db` under
`%LOCALAPPDATA%\PitseaRaceResults\` — a per-user folder that is **not**
cloud-synced (OneDrive / Drive holds write locks on SQLite files and can
corrupt them; the Settings page offers Download Backup / Restore for sharing
copies).

Power users can override the path by setting
`ConnectionStrings__DefaultConnection=Data Source=…` before launching.

## Upgrading

Run the new installer over the old one. The publish folder is replaced, but
the database in `%LOCALAPPDATA%\PitseaRaceResults\` is untouched — pending
EF migrations run on first start, exactly as in dev.

## Uninstalling

Uninstall from **Apps & features** in Windows. By default the database is
preserved. To wipe it as well, tick **Also delete saved race data** during
uninstall.
