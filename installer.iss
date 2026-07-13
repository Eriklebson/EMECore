[Setup]
AppName=EME Core
AppVersion=2.18.0.0
AppPublisher=E.M.E Core
DefaultDirName={autopf}\EMECore
DefaultGroupName=EME Core
OutputDir=..\installer
OutputBaseFilename=EMECore_v2.18.0.0_Setup
Compression=lzma2/ultra64
SolidCompression=yes
SetupIconFile=src\EMECore.WinUI\Assets\Logo\logo.ico
UninstallDisplayIcon={app}\EMECore.WinUI.exe
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern

[Languages]
Name: "portuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Criaratalho na area de trabalho"; GroupDescription: "Atalhos:"
Name: "startmenuicon"; Description: "Criaratalho no Menu Iniciar"; GroupDescription: "Atalhos:"; Flags: checkedonce

[Files]
Source: "release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\EME Core"; Filename: "{app}\EMECore.WinUI.exe"; Comment: "Abrir E.M.E Core"
Name: "{group}\Desinstalar EME Core"; Filename: "{uninstallexe}"
Name: "{autodesktop}\EME Core"; Filename: "{app}\EMECore.WinUI.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\EMECore.WinUI.exe"; Description: "Abrir E.M.E Core agora"; Flags: nowait postinstall skipifsilent

[Code]
function IsDotNet8Installed: Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('dotnet', '--list-runtimes', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    if not IsDotNet8Installed then
    begin
      if MsgBox('.NET 8 Runtime no encontrado. Deseja baixar e instalar agora?', mbConfirmation, MB_YESNO) = IDYES then
      begin
        ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/8.0/runtime', '', '', SW_SHOW, ewNoWait, ResultCode);
      end;
    end;
  end;
end;
