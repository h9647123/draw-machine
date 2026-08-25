#define AppName "抽号机"
#define AppVersion "175.0.0"
#define AppVersionText "v175-2026-08-25"
#define AppPublisher "h9647123"
#ifndef SourceExe
  #define SourceExe "..\DrawMachineDesktop\bin\Release\publish-v175-single-exe\抽号机.exe"
#endif
#ifndef OutputDir
  #define OutputDir "..\DrawMachineDesktop\bin\Release\installer-v175-windows"
#endif

[Setup]
AppId={{57B91247-671B-4214-A30B-4C6638201573}
AppName={#AppName}
AppVersion={#AppVersionText}
AppVerName={#AppName} {#AppVersionText}
AppPublisher={#AppPublisher}
DefaultDirName={code:GetDefaultInstallDir}
DisableProgramGroupPage=yes
AllowNoIcons=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\DrawMachineDesktop\Assets\app-icon.ico
UninstallDisplayIcon={app}\抽号机.exe
OutputDir={#OutputDir}
OutputBaseFilename=抽号机安装器
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ChangesAssociations=no
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=抽号机 Setup
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
VersionInfoCopyright={#AppPublisher}

[Languages]
Name: "chinesesimp"; MessagesFile: "ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："; Flags: checkedonce
Name: "startmenuicon"; Description: "创建开始菜单快捷方式"; GroupDescription: "快捷方式："; Flags: checkedonce
Name: "startup"; Description: "开机自启到托盘"; GroupDescription: "启动选项："; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; DestName: "抽号机.exe"; Flags: ignoreversion

[Icons]
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\抽号机.exe"; WorkingDir: "{app}"; IconFilename: "{app}\抽号机.exe"; Tasks: desktopicon
Name: "{userprograms}\{#AppName}\{#AppName}"; Filename: "{app}\抽号机.exe"; WorkingDir: "{app}"; IconFilename: "{app}\抽号机.exe"; Tasks: startmenuicon
Name: "{userprograms}\{#AppName}\卸载{#AppName}"; Filename: "{uninstallexe}"; Tasks: startmenuicon

[Run]
Filename: "{app}\抽号机.exe"; Description: "安装完成后打开抽号机"; Flags: nowait postinstall skipifsilent unchecked

[UninstallDelete]
Type: dirifempty; Name: "{userprograms}\{#AppName}"

[Code]
const
  StartupRegistryPath = 'Software\Microsoft\Windows\CurrentVersion\Run';
  StartupRegistryName = 'DrawMachineDesktop';
  InstallerRegistryPath = 'Software\DrawMachineDesktop';
  InstallDirectoryRegistryValue = 'InstallDirectory';

procedure ConfigureStartup();
var
  StartupCommand: String;
begin
  if WizardIsTaskSelected('startup') then
  begin
    StartupCommand := '"' + ExpandConstant('{app}\抽号机.exe') + '" --silent-startup';
    RegWriteStringValue(HKCU, StartupRegistryPath, StartupRegistryName, StartupCommand);
  end
  else
  begin
    RegDeleteValue(HKCU, StartupRegistryPath, StartupRegistryName);
  end;
end;

procedure SaveInstallDirectory();
begin
  RegWriteStringValue(HKCU, InstallerRegistryPath, InstallDirectoryRegistryValue, ExpandConstant('{app}'));
end;

function GetDefaultInstallDir(Param: String): String;
var
  SavedDir: String;
begin
  if RegQueryStringValue(HKCU, InstallerRegistryPath, InstallDirectoryRegistryValue, SavedDir) and (SavedDir <> '') then
    Result := SavedDir
  else
    Result := ExpandConstant('{localappdata}\DrawMachineDesktop');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    ConfigureStartup();
    SaveInstallDirectory();
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RegDeleteValue(HKCU, StartupRegistryPath, StartupRegistryName);
  end;
end;
