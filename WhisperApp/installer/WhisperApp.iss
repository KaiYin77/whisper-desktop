#define MyAppName "WhisperApp"
#define MyAppVersion "1.0"
#define MyAppPublisher "Upbeat Tech"
#define MyAppExeName "WhisperApp.exe"
#define MyAppIcon "..\app-icon.ico"

[Setup]
AppId={{A3F2C1D4-8B7E-4F5A-9C2D-1E6B0A3F7D8C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\dist
OutputBaseFilename=WhisperApp-Setup
SetupIconFile={#MyAppIcon}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "建立桌面捷徑"; GroupDescription: "額外圖示："
Name: "installwhisper"; Description: "安裝 Whisper 相依套件 (Python openai-whisper + ffmpeg)"; GroupDescription: "相依套件："; Flags: unchecked

[Files]
Source: "..\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\scripts\install-whisper.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\解除安裝 {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -NonInteractive -WindowStyle Normal -File ""{app}\scripts\install-whisper.ps1"""; \
  StatusMsg: "安裝 Whisper 相依套件中，請稍候..."; \
  Tasks: installwhisper; \
  Flags: waituntilterminated

Filename: "{app}\{#MyAppExeName}"; \
  Description: "啟動 WhisperApp"; \
  Flags: nowait postinstall skipifsilent
