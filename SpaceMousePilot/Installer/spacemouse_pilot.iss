; SpaceMouse Pilot — Inno Setup script

#define AppName       "SpaceMouse Pilot"
#define AppVersion    GetEnv("SPACEMOUSE_VERSION")
#define AppVersionNum GetEnv("SPACEMOUSE_VERSION_NUMERIC")
#define AppPublisher  "SpaceMouse Pilot"
#define AppExe        "SpaceMousePilot.exe"
#define Root          SourcePath + "\.."
#define SourceDir     Root + "\dist\app"

[Setup]
AppId={{A3F2C1D4-7B8E-4F9A-B3C2-1D4E5F6A7B8C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputDir={#Root}\dist\installer
OutputBaseFilename=SpaceMousePilot_Setup_{#AppVersion}
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=lowest
UninstallDisplayName={#AppName}
VersionInfoVersion={#AppVersionNum}
CloseApplications=yes
CloseApplicationsFilter={#AppExe}
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";  Description: "Create a desktop shortcut";        GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "startupentry"; Description: "Start with Windows (system tray)"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";           Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#AppName}";     Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "{#AppName}"; \
  ValueData: """{app}\{#AppExe}"" --tray"; \
  Flags: uninsdeletevalue; Tasks: startupentry

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "taskkill.exe"; Parameters: "/f /im {#AppExe}"; Flags: runhidden; RunOnceId: "KillApp"

[Code]
function ViGEmBusInstalled: Boolean;
begin
  Result := RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\ViGEmBus');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
    Exec('taskkill.exe', '/f /im SpaceMousePilot.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  if CurStep = ssPostInstall then
    if not ViGEmBusInstalled then
      MsgBox(
        'ViGEmBus driver was not detected.' + #13#10 +
        'SpaceMouse Pilot requires it to create a virtual controller.' + #13#10#13#10 +
        'Download from: https://github.com/nefarius/ViGEmBus/releases/latest',
        mbInformation, MB_OK);
end;
