[Setup]
AppName=GrinVideoEncoder
AppVersion={#AppVersion}
AppPublisher=GrinwaldFlo
AppPublisherURL=https://github.com/GrinwaldFlo/GrinVideoEncoder
DefaultDirName={autopf}\GrinVideoEncoder
DefaultGroupName=GrinVideoEncoder
OutputDir=output
OutputBaseFilename=GrinVideoEncoder-{#AppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
SetupIconFile=setup-icon.ico
UninstallDisplayIcon={app}\GrinVideoEncoder.exe
PrivilegesRequired=lowest

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\GrinVideoEncoder"; Filename: "{app}\GrinVideoEncoder.exe"
Name: "{group}\Uninstall GrinVideoEncoder"; Filename: "{uninstallexe}"
Name: "{autodesktop}\GrinVideoEncoder"; Filename: "{app}\GrinVideoEncoder.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\GrinVideoEncoder.exe"; Description: "Launch GrinVideoEncoder"; Flags: nowait postinstall skipifsilent
