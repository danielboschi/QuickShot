#define AppName "QuickShot"
#define AppVersion "1.0.1"
#define AppPublisher "QuickShot"
#define AppExeName "QuickShot.exe"

[Setup]
AppId={{C0A3E8A1-9D11-4A77-B468-7A92B91F4A32}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\QuickShot
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=QuickShot-Setup-{#AppVersion}-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "Criar um atalho na área de trabalho"; GroupDescription: "Atalhos adicionais:"; Flags: unchecked
Name: "startupicon"; Description: "Iniciar o QuickShot automaticamente ao entrar no Windows"; GroupDescription: "Atalhos adicionais:";

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Excludes: "*.pdb,*.log,*.log.old"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\QuickShot"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Desinstalar QuickShot"; Filename: "{uninstallexe}"
Name: "{autodesktop}\QuickShot"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon
Name: "{userstartup}\QuickShot"; Filename: "{app}\{#AppExeName}"; Tasks: startupicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Iniciar QuickShot"; Flags: nowait postinstall skipifsilent
