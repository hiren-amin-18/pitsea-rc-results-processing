<#
.SYNOPSIS
    Builds a versioned, self-contained Windows release (US25).

.DESCRIPTION
    1. Publishes RaceResults.Web with the win-x64-installer profile (self-contained,
       single-file, ready-to-run) so end users don't need the .NET SDK.
    2. If Inno Setup's ISCC.exe is on PATH, compiles the installer.
    3. Otherwise produces a zip-and-shortcut fallback that can be unzipped anywhere.

    Run from the repository root.

.PARAMETER Version
    Semantic version stamped on the artefact (default: 1.0.0).

.EXAMPLE
    .\installer\build-installer.ps1 -Version 2026.06.14
#>

param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$publishDir = Join-Path $repoRoot "RaceResults.Web\bin\publish\win-x64"
$distDir = Join-Path $repoRoot "dist"
$null = New-Item -ItemType Directory -Force $distDir

Write-Host "==> Publishing self-contained win-x64 build..."
& dotnet publish (Join-Path $repoRoot "RaceResults.Web\RaceResults.Web.csproj") `
    -c Release `
    -p:PublishProfile=win-x64-installer `
    -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

# Copy the launcher into the publish output so a zip fallback is self-sufficient.
Copy-Item (Join-Path $PSScriptRoot "PitseaRaceResults.cmd") -Destination $publishDir -Force

$iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue)
if ($iscc) {
    Write-Host "==> Inno Setup found at $($iscc.Source); compiling installer..."
    & $iscc.Source "/DAppVersion=$Version" (Join-Path $PSScriptRoot "PitseaRaceResults.iss")
    if ($LASTEXITCODE -ne 0) { throw "ISCC failed." }
    Write-Host "==> Installer written to $distDir"
} else {
    $zipPath = Join-Path $distDir "PitseaRaceResults-$Version-win-x64.zip"
    Write-Host "==> Inno Setup not on PATH - producing zip fallback at $zipPath"
    if (Test-Path $zipPath) { Remove-Item $zipPath }
    Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath
    Write-Host "==> Zip written. To install: unzip, then run PitseaRaceResults.cmd."
}
