; XOutputRedux Inno Setup Script
; Requires Inno Setup 6.x

#define MyAppName "XOutputRedux"
#define MyAppPublisher "XOutputRedux"
#define MyAppURL "https://github.com/d-b-c-e/xoutputredux"
#define MyAppExeName "XOutputRedux.exe"

; Version is passed in via command line: /DMyAppVersion=0.7.0-alpha
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif

[Setup]
AppId={{8A9E1F2D-4B3C-5D6E-7F8A-9B0C1D2E3F4A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\LICENSE
OutputDir=..\dist
OutputBaseFilename=XOutputRedux-{#MyAppVersion}-Setup
SetupIconFile=..\src\XOutputRedux.App\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
; Close running app before upgrade
CloseApplications=force
CloseApplicationsFilter=*.exe
RestartApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "addtopath"; Description: "Add to system PATH (enables CLI from any terminal)"; GroupDescription: "System Integration:"; Flags: unchecked
Name: "startwithwindows"; Description: "Start XOutputRedux when Windows starts"; GroupDescription: "System Integration:"; Flags: unchecked
Name: "runasadmin"; Description: "Always run as administrator (recommended for device access)"; GroupDescription: "System Integration:"; Flags: unchecked

[Files]
; Main application files from publish directory
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; shellexec + runasoriginaluser: Launch as the non-elevated user who started the installer
; Skip auto-launch if "run as admin" was selected (causes elevation conflict)
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent shellexec runasoriginaluser; Check: CanAutoLaunch

[Registry]
; Add to PATH if selected
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}"; Tasks: addtopath; Check: NeedsAddPath('{app}')
; Start with Windows if selected (requires both Run and StartupApproved entries)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "XOutputRedux"; ValueData: """{app}\{#MyAppExeName}"" --minimized"; Tasks: startwithwindows; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"; ValueType: binary; ValueName: "XOutputRedux"; ValueData: "02 00 00 00 00 00 00 00 00 00 00 00"; Tasks: startwithwindows; Flags: uninsdeletevalue
; Always run as administrator if selected (sets Windows compatibility layer)
Root: HKCU; Subkey: "Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"; ValueType: string; ValueName: "{app}\{#MyAppExeName}"; ValueData: "RUNASADMIN"; Tasks: runasadmin; Flags: uninsdeletevalue

[UninstallDelete]
; Clean up app data on uninstall (optional - commented out to preserve user settings)
; Type: filesandordirs; Name: "{userappdata}\XOutputRedux"

[Code]
// Check if "run as admin" task was NOT selected (for safe auto-launch)
function CanAutoLaunch(): boolean;
begin
  Result := not WizardIsTaskSelected('runasadmin');
end;

// Check if path already contains the directory
function NeedsAddPath(Param: string): boolean;
var
  OrigPath: string;
begin
  if not RegQueryStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', OrigPath) then
  begin
    Result := True;
    exit;
  end;
  // Look for the path with leading and trailing semicolons
  Result := Pos(';' + Param + ';', ';' + OrigPath + ';') = 0;
end;

// Remove from PATH on uninstall
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  OrigPath, NewPath: string;
  AppDir: string;
  P: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    AppDir := ExpandConstant('{app}');
    if RegQueryStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', OrigPath) then
    begin
      NewPath := OrigPath;
      // Remove the path entry
      P := Pos(';' + AppDir, NewPath);
      if P > 0 then
        Delete(NewPath, P, Length(AppDir) + 1)
      else
      begin
        P := Pos(AppDir + ';', NewPath);
        if P > 0 then
          Delete(NewPath, P, Length(AppDir) + 1)
        else if NewPath = AppDir then
          NewPath := '';
      end;
      // Update registry
      if NewPath <> OrigPath then
        RegWriteStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', NewPath);
    end;
  end;
end;
