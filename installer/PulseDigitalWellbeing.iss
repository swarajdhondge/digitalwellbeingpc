[Setup]
AppName=Pulse Digital Wellbeing
AppVersion=0.1.0
AppPublisher=Pulse Digital Wellbeing Team
DefaultDirName={autopf}\Pulse Digital Wellbeing
DisableProgramGroupPage=yes
OutputBaseFilename=Pulse-Digital-Wellbeing-v0.1.0-Setup
Compression=lzma
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
; Uncomment and point to a valid .ico file for a custom installer icon
;SetupIconFile=icon.ico
UninstallDisplayIcon={app}\DigitalWellbeing.exe
OutputDir=.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\digital-wellbeing-app\bin\Release\net9.0-windows10.0.26100.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Pulse Digital Wellbeing"; Filename: "{app}\DigitalWellbeing.exe"
Name: "{autodesktop}\Pulse Digital Wellbeing"; Filename: "{app}\DigitalWellbeing.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\DigitalWellbeing.exe"; Description: "{cm:LaunchProgram,Pulse Digital Wellbeing}"; Flags: nowait postinstall skipifsilent
