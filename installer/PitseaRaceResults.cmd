@echo off
REM Launcher shipped by the US25 installer. Starts the published app in a hidden window
REM and opens the default browser at the listening URL once it's ready.

setlocal
set "APP_URL=http://localhost:5200"
set "APP_DIR=%~dp0"
set "APP_EXE=%APP_DIR%RaceResults.Web.exe"

if not exist "%APP_EXE%" (
    echo Could not find %APP_EXE%. Reinstall the application.
    pause
    exit /b 1
)

REM Run the app in the background so closing the browser doesn't kill it.
start "" /B "%APP_EXE%" --urls=%APP_URL%

REM Give Kestrel a moment to come up before opening the browser.
timeout /t 2 /nobreak >nul
start "" "%APP_URL%"

endlocal
