; Installer fuer Speedy Monitor (Inno Setup 6.7+).
; Bauen: ..\build-installer.ps1 (veroeffentlicht die App und ruft ISCC auf).

#define AppName "Speedy Monitor"
#define AppExe "SpeedyMonitor.exe"
#define AppPublisher "breiti35"
; ISCC /DStandalone baut die Variante mit eingebauter .NET-Laufzeit (kein Runtime-Check noetig).
#ifdef Standalone
  #define PublishDir "..\publish-standalone"
  #define OutputSuffix "_Standalone"
#else
  #define PublishDir "..\publish"
  #define OutputSuffix ""
#endif
#define AppVersion GetVersionNumbersString(PublishDir + "\" + AppExe)
; Nur Major.Minor.Patch fuer Anzeige und Dateiname.
#define AppVersionShort Copy(AppVersion, 1, RPos(".", AppVersion) - 1)

[Setup]
AppId={{6E4B1F0C-8A57-4C2E-9D1B-5F3A7C2E9B41}
AppName={#AppName}
AppVersion={#AppVersionShort}
AppVerName={#AppName} {#AppVersionShort}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}
; Pro Benutzer, ohne Admin-Abfrage - passt zum Autostart unter HKCU.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
OutputDir=Output
OutputBaseFilename=SpeedyMonitor_Setup_{#AppVersionShort}{#OutputSuffix}
LicenseFile=..\LICENSE
SetupIconFile=..\src\NetSpeedMonitor\AppIcon.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern dynamic windows11
WizardSmallImageFile=assets\wizard-small-58.png,assets\wizard-small-87.png,assets\wizard-small-116.png
WizardSmallImageFileDynamicDark=assets\wizard-small-58.png,assets\wizard-small-87.png,assets\wizard-small-116.png
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
ShowLanguageDialog=auto

[Languages]
; Englisch zuerst: Inno waehlt die zur Windows-Sprache passende Sprache, sonst die erste.
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[CustomMessages]
english.AutostartTask=Start with Windows
english.OtherTasks=Other options:
english.RuntimeMissing=Speedy Monitor requires the .NET 8 Desktop Runtime (x64), which was not found on this PC.%n%nOpen the download page now? After installing the runtime, simply run this setup again.
english.DeleteUserData=Do you also want to delete your settings and the collected statistics?%n%n(If you choose "No", they are kept for a later reinstallation.)
german.AutostartTask=Mit Windows starten
german.OtherTasks=Weitere Optionen:
german.RuntimeMissing=Speedy Monitor benötigt die .NET 8 Desktop Runtime (x64), die auf diesem PC nicht gefunden wurde.%n%nSoll die Download-Seite jetzt geöffnet werden? Nach der Installation der Runtime einfach das Setup erneut starten.
german.DeleteUserData=Sollen auch deine Einstellungen und die gesammelte Statistik gelöscht werden?%n%n(Bei "Nein" bleiben sie für eine spätere Neuinstallation erhalten.)

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "autostart"; Description: "{cm:AutostartTask}"; GroupDescription: "{cm:OtherTasks}"

[Files]
Source: "{#PublishDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "SpeedyMonitor"; ValueData: """{app}\{#AppExe}"""; Tasks: autostart

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
function IsDesktopRuntime8Installed: Boolean;
var
  FindRec: TFindRec;
begin
  Result := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\8.*'), FindRec);
  if Result then
    FindClose(FindRec);
end;

function InitializeSetup: Boolean;
var
  ErrorCode: Integer;
begin
#ifdef Standalone
  Result := True;
#else
  Result := IsDesktopRuntime8Installed;
  if not Result then
    if SuppressibleMsgBox(CustomMessage('RuntimeMissing'), mbError, MB_YESNO, IDNO) = IDYES then
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/8.0', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
#endif
end;

// Laufende Instanz beenden, sonst ist die exe gesperrt (die App hat kein Hauptfenster,
// auf das der Restart Manager reagieren koennte).
procedure StopRunningApp;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(300);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningApp;
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    StopRunningApp;
    // Die App kann den Autostart-Eintrag auch selbst gesetzt haben - immer entfernen.
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'SpeedyMonitor');
  end;

  if CurUninstallStep = usPostUninstall then
    if SuppressibleMsgBox(CustomMessage('DeleteUserData'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2, IDNO) = IDYES then
      DelTree(ExpandConstant('{userappdata}\SpeedyMonitor'), True, True, True);
end;
