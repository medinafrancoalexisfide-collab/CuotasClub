; Instalador de CuotasClub (Inno Setup 6).
; Se genera con crear-instalador.bat, que primero publica el programa en ..\publicado.
; No pide permisos de administrador: se instala para el usuario que lo ejecuta.
; Los datos (cuotasclub.db) viven en %LOCALAPPDATA%\CuotasClub, así que reinstalar o actualizar no los toca.

#define MyAppName "CuotasClub"
#define MyAppVersion "1.0.0"
#define MyAppExe "CuotasClub.exe"
#ifndef Publicado
  #define Publicado "..\publicado"
#endif

[Setup]
; No cambiar AppId: es lo que hace que una versión nueva actualice a la anterior.
AppId={{9D4E33DD-2607-4E0C-A118-E40500BFCA31}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher=CuotasClub
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\salida
OutputBaseFilename=Instalar-CuotasClub-{#MyAppVersion}
SetupIconFile=..\src\CuotasClub\Recursos\CuotasClub.ico
UninstallDisplayIcon={app}\{#MyAppExe}
UninstallDisplayName={#MyAppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "escritorio"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"

[Files]
Source: "{#Publicado}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: escritorio

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "Abrir CuotasClub ahora"; Flags: nowait postinstall skipifsilent
