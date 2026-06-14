; US25 — Pitsea RC Race Results installer (Inno Setup).
; Build the publish output first:
;   dotnet publish RaceResults.Web/RaceResults.Web.csproj -c Release -p:PublishProfile=win-x64-installer
; Then compile:
;   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DAppVersion=1.0.0 installer\PitseaRaceResults.iss

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{C2A4E3F2-7C2B-4B7B-9F45-2A4F5C8D3E1B}
AppName=Pitsea RC Race Results
AppVersion={#AppVersion}
AppPublisher=Pitsea Running Club
DefaultDirName={autopf}\PitseaRaceResults
DefaultGroupName=Pitsea RC Race Results
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\RaceResults.Web.exe
OutputDir=..\dist
OutputBaseFilename=PitseaRaceResults-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Self-contained publish output produced by the win-x64-installer profile.
Source: "..\RaceResults.Web\bin\publish\win-x64\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs
Source: "PitseaRaceResults.cmd"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Pitsea RC Race Results"; Filename: "{app}\PitseaRaceResults.cmd"; WorkingDir: "{app}"; IconFilename: "{app}\RaceResults.Web.exe"
Name: "{group}\Uninstall"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\PitseaRaceResults.cmd"; Description: "Start Pitsea RC Race Results"; Flags: postinstall nowait skipifsilent

[Tasks]
; AC5: data is kept by default. The user must explicitly opt in to deleting it during uninstall.
Name: "deletedata"; Description: "Also delete saved race data (the database in %LOCALAPPDATA%\PitseaRaceResults)."; GroupDescription: "Uninstall options:"; Flags: unchecked

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\PitseaRaceResults"; Tasks: deletedata
