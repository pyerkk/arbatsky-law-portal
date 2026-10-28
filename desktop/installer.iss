[Setup]
AppId={{EE5E5C22-C58B-4CC1-9D64-98EC786BC716}
AppName=Правовой портал Арбатский
AppVersion=1.0.0
AppPublisher=Неофициальный игровой справочник
DefaultDirName={localappdata}\Programs\ArbatskyOverlay
DefaultGroupName=Правовой портал Арбатский
OutputDir=..\output
OutputBaseFilename=ArbatskyOverlay-Setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\ArbatskyOverlay.exe
WizardStyle=modern

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; Flags: unchecked

[Files]
Source: "..\output\ArbatskyOverlay.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Правовой портал Арбатский"; Filename: "{app}\ArbatskyOverlay.exe"
Name: "{autodesktop}\Правовой портал Арбатский"; Filename: "{app}\ArbatskyOverlay.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\ArbatskyOverlay.exe"; Description: "Запустить приложение"; Flags: nowait postinstall skipifsilent

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "ArbatskyOverlay"; Flags: uninsdeletevalue
