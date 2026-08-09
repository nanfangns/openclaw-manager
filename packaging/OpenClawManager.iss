#define MyAppName "OpenClaw Manager"
#define MyAppVersion "0.1.3"
#define MyAppPublisher "nanfangns"
#define MyAppExeName "OpenClawManager.exe"
#define PublishDir AddBackslash(SourcePath) + "publish"

[Setup]
AppId={{7E37DBD0-4A47-4D35-A2F3-7F0B8B4A6A8D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\OpenClaw Manager
DefaultGroupName={#MyAppName}
UninstallDisplayName={#MyAppName}
OutputDir=output
OutputBaseFilename=OpenClawManagerSetup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\OpenClawManager\Assets\OpenClawManager.ico
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
DisableProgramGroupPage=yes
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
