[Preprocessor]
#ifndef ReleaseDir
  #define ReleaseDir "release"
#endif

[Setup]
AppName=EME Core
AppVersion=2.23.0.0
AppPublisher=E.M.E Core
DefaultDirName={autopf}\EMECore
DefaultGroupName=EME Core
OutputDir=..\installer
OutputBaseFilename=EMECore_v2.23.0.0_Setup
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
Name: "desktopicon"; Description: "Criar atalho na area de trabalho"; GroupDescription: "Atalhos:"
Name: "startmenuicon"; Description: "Criar atalho no Menu Iniciar"; GroupDescription: "Atalhos:"; Flags: checkedonce

[Files]
Source: "{#ReleaseDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

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
  Result := Exec('dotnet', '--list-runtimes', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function IsWebView2Installed: Boolean;
var
  S: String;
begin
  Result := RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BFB-1B2A3AB2C151}', 'pv', S) or
            RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BFB-1B2A3AB2C151}', 'pv', S) or
            RegQueryStringValue(HKCU, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BFB-1B2A3AB2C151}', 'pv', S);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  if not IsDotNet8Installed then
  begin
    if MsgBox('NET 8 Desktop Runtime nao foi encontrado. Deseja baixar agora?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/8.0/runtime', '', '', SW_SHOW, ewNoWait, ResultCode);
    end;
  end;
  if not IsWebView2Installed then
  begin
    if MsgBox('WebView2 Runtime nao foi encontrado. Deseja baixar agora?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://go.microsoft.com/fwlink/p/?LinkId=2124703', '', '', SW_SHOW, ewNoWait, ResultCode);
    end;
  end;
end;
