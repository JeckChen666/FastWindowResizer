#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\dist\FastWindowResizer"
#endif
#ifndef InstallerOutputDir
  #define InstallerOutputDir "..\dist\installer"
#endif

[Setup]
AppId={{FA2A415F-A0A4-41C7-B591-5C841BC61451}
AppName=FastWindowResizer
AppVersion={#AppVersion}
AppPublisher=JeckChen666
AppPublisherURL=https://github.com/JeckChen666/FastWindowResizer
AppSupportURL=https://github.com/JeckChen666/FastWindowResizer/issues
AppUpdatesURL=https://github.com/JeckChen666/FastWindowResizer/releases
DefaultDirName={localappdata}\Programs\FastWindowResizer
DefaultGroupName=FastWindowResizer
DisableProgramGroupPage=yes
DisableDirPage=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#InstallerOutputDir}
OutputBaseFilename=FastWindowResizer-v{#AppVersion}-win-x64-setup
SetupIconFile=..\Assets\app-generated-v2.ico
UninstallDisplayIcon={app}\FastWindowResizer.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
AppMutex=Local\FastWindowResizer
SetupMutex=Local\FastWindowResizer.Setup
CloseApplications=yes
RestartApplications=no
Uninstallable=yes

[Languages]
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\FastWindowResizer"; Filename: "{app}\FastWindowResizer.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\FastWindowResizer"; Filename: "{app}\FastWindowResizer.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\FastWindowResizer.exe"; Description: "{cm:LaunchProgram,FastWindowResizer}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command, InstalledExe: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    InstalledExe := ExpandConstant('{app}\FastWindowResizer.exe');
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run',
      'FastWindowResizer', Command) then
    begin
      { Remove only this installation's autostart, not a different portable copy. }
      if (CompareText(Trim(Command), '"' + InstalledExe + '"') = 0) or
         (CompareText(Trim(Command), InstalledExe) = 0) then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'FastWindowResizer');
    end;
  end;
end;
