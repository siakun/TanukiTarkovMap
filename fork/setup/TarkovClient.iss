; Inno Setup Script for TarkovClient
; This script creates a professional installer for TarkovClient
; Copyright (c) 2025 TarkovClient Project

#define MyAppName "Tarkov Client"
#define MyAppVersion "0.1.1"
#define MyAppPublisher "crows_gear_byeong_il"
#define MyAppURL "https://github.com/"
#define MyAppExeName "TarkovClient.exe"
#define MyAppDescription "Tarkov Market Client with integrated map features"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
AppId={{E8F6A7B2-3C4D-4E5F-9A8B-1C2D3E4F5A6B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
AppComments={#MyAppDescription}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=
OutputDir=Output
SourceDir=..
OutputBaseFilename=TarkovClientSetup
SetupIconFile=Resources\korea.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

; Windows version requirements
MinVersion=10.0.17763
; Windows 10 version 1809 (October 2018) or later

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
Source: "publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\TarkovClient.exe.WebView2"
Type: filesandordirs; Name: "{userappdata}\TarkovClient"
Type: filesandordirs; Name: "{localappdata}\Temp\TarkovClient"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

procedure InitializeWizard();
begin
  WizardForm.LicenseAcceptedRadio.Checked := True;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  
  // Check if WebView2 is installed
  if not RegKeyExists(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}') and
     not RegKeyExists(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}') then
  begin
    if MsgBox('WebView2 Runtime이 설치되어 있지 않습니다.' + #13#10 + 
              'TarkovClient가 정상적으로 작동하려면 WebView2가 필요합니다.' + #13#10 + #13#10 +
              '설치를 계속하시겠습니까?' + #13#10 +
              '(나중에 Microsoft 웹사이트에서 WebView2를 다운로드할 수 있습니다)', 
              mbConfirmation, MB_YESNO) = IDNO then
      Result := 'WebView2 Runtime 설치가 필요합니다.';
  end;
end;