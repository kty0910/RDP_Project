#define MyAppName "Remote Monitor"



#ifdef NoBridgeToken
#define MyOutputDir "..\installer-output-notoken"
#define MyPublishDir "..\publish-notoken"
#else
#define MyOutputDir "..\installer-output"
#define MyPublishDir "..\publish"
#endif

#define MyAppVersion "1.1.0"

#define MyReleaseDate "2026-07-29"



#define MyAppPublisher "RemoteMonitor"



[Setup]



AppId={{D6DEDF3D-393A-48F0-B563-57FCA0F3214F}



AppName={#MyAppName}



AppVersion={#MyAppVersion}



AppPublisher={#MyAppPublisher}



DefaultDirName={code:GetInstallRootDir}



DisableDirPage=yes



DefaultGroupName=Remote Monitor



DisableProgramGroupPage=yes



OutputDir={#MyOutputDir}



OutputBaseFilename=RemoteMonitor_Setup_v{#MyAppVersion}



Compression=lzma2



SolidCompression=yes



WizardStyle=modern



PrivilegesRequired=admin



ArchitecturesAllowed=x64compatible



ArchitecturesInstallIn64BitMode=x64compatible



UninstallDisplayName={#MyAppName} v{#MyAppVersion}



SetupIconFile=..\Assets\installer-r.ico
WizardSmallImageFile=..\Assets\installer-r-small.bmp
WizardImageFile=..\Assets\installer-r-large.bmp



[Languages]



Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"



[Types]



Name: "custom"; Description: "사용자 지정"; Flags: iscustom



[Components]



Name: "client"; Description: "Client 설치"; Types: custom; Flags: checkablealone



Name: "server"; Description: "Server 설치"; Types: custom; Flags: checkablealone



[Tasks]



Name: "clientdesktopicon"; Description: "Client 바탕화면 아이콘 생성"; GroupDescription: "Client 추가 작업:"; Components: client; Flags: unchecked



Name: "clientstartmenu"; Description: "Client 시작 메뉴 바로가기 등록"; GroupDescription: "Client 추가 작업:"; Components: client



Name: "clientstartup"; Description: "Windows 로그인 시 자동 실행"; GroupDescription: "Client 추가 작업:"; Components: client; Flags: unchecked



Name: "serverdesktopicon"; Description: "Server 바탕화면 아이콘 생성"; GroupDescription: "Server 추가 작업:"; Components: server; Flags: unchecked



Name: "serverstartmenu"; Description: "Server 시작 메뉴 바로가기 등록"; GroupDescription: "Server 추가 작업:"; Components: server






Name: "serverstartup"; Description: "Windows 부팅 시 자동 실행"; GroupDescription: "Server 추가 작업:"; Components: server



[Files]



Source: "{#MyPublishDir}\client\*"; DestDir: "{code:GetClientDir}"; Components: client; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,Data\remote_pc_list.dat"



Source: "{#MyPublishDir}\server\*"; DestDir: "{code:GetServerDir}"; Components: server; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,bridge_settings.json,Logs\*"



Source: "{#MyPublishDir}\server-service\*"; DestDir: "{code:GetServerDir}"; Components: server; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,bridge_settings.json,Logs\*"



[Dirs]



Name: "{code:GetClientDir}\Data"; Components: client



Name: "{code:GetServerDir}\Logs"; Components: server



[Icons]



Name: "{commondesktop}\Remote Monitor Client"; Filename: "{code:GetClientDir}\RemoteMonitor.Client.exe"; Components: client; Tasks: clientdesktopicon



Name: "{commondesktop}\Remote Monitor Server"; Filename: "{code:GetServerDir}\RemoteMonitor.Server.exe"; Components: server; Tasks: serverdesktopicon



Name: "{group}\Remote Monitor Client"; Filename: "{code:GetClientDir}\RemoteMonitor.Client.exe"; Components: client; Tasks: clientstartmenu



Name: "{group}\Remote Monitor Server"; Filename: "{code:GetServerDir}\RemoteMonitor.Server.exe"; Components: server; Tasks: serverstartmenu



[Registry]



Root: HKLM; Subkey: "Software\RemoteMonitor"; ValueType: string; ValueName: "ClientDir"; ValueData: "{code:GetClientDir}"; Components: client



Root: HKLM; Subkey: "Software\RemoteMonitor"; ValueType: string; ValueName: "ServerDir"; ValueData: "{code:GetServerDir}"; Components: server



Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "RemoteMonitorClient"; ValueData: """{code:GetClientDir}\RemoteMonitor.Client.exe"" --tray"; Components: client; Tasks: clientstartup; Flags: uninsdeletevalue



Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "RemoteMonitorServerUI"; ValueData: """{code:GetServerDir}\RemoteMonitor.Server.exe"" --tray"; Components: server; Tasks: serverstartup



[Run]



Filename: "{sys}\cmd.exe"; Parameters: "/C reg delete HKLM\Software\Microsoft\Windows\CurrentVersion\Run /v RemoteMonitorClient /f >NUL 2>&1 & exit /B 0"; Flags: runhidden waituntilterminated; Components: client; Tasks: not clientstartup



Filename: "{sys}\cmd.exe"; Parameters: "/C sc stop RemoteMonitor.Server.Service >NUL 2>&1 & exit /B 0"; Flags: runhidden waituntilterminated; Components: server; Tasks: serverstartup



Filename: "{sys}\cmd.exe"; Parameters: "/C sc delete RemoteMonitor.Server.Service >NUL 2>&1 & exit /B 0"; Flags: runhidden waituntilterminated; Components: server; Tasks: serverstartup



Filename: "{sys}\sc.exe"; Parameters: "create RemoteMonitor.Server.Service binPath= ""{code:GetServerDir}\RemoteMonitor.Server.Service.exe"" start= auto DisplayName= ""Remote Monitor Server Service"""; Flags: runhidden waituntilterminated; Components: server; Tasks: serverstartup



Filename: "{sys}\cmd.exe"; Parameters: "/C sc stop RemoteMonitor.Server.Service >NUL 2>&1 & exit /B 0"; Flags: runhidden waituntilterminated; Components: server; Tasks: not serverstartup



Filename: "{sys}\cmd.exe"; Parameters: "/C sc delete RemoteMonitor.Server.Service >NUL 2>&1 & exit /B 0"; Flags: runhidden waituntilterminated; Components: server; Tasks: not serverstartup



Filename: "{sys}\cmd.exe"; Parameters: "/C reg delete HKLM\Software\Microsoft\Windows\CurrentVersion\Run /v RemoteMonitorServerUI /f >NUL 2>&1 & exit /B 0"; Flags: runhidden waituntilterminated; Components: server; Tasks: not serverstartup









Filename: "{code:GetClientDir}\RemoteMonitor.Client.exe"; Description: "Client 실행"; Components: client; Flags: nowait postinstall skipifsilent unchecked



Filename: "{code:GetServerDir}\RemoteMonitor.Server.exe"; Description: "Server UI 실행"; Components: server; Flags: nowait postinstall skipifsilent unchecked



[UninstallRun]



Filename: "{sys}\cmd.exe"; Parameters: "/C reg delete HKLM\Software\Microsoft\Windows\CurrentVersion\Run /v RemoteMonitorClient /f >NUL 2>&1 & exit /B 0"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteClientRun"



Filename: "{sys}\cmd.exe"; Parameters: "/C sc stop RemoteMonitor.Server.Service >NUL 2>&1 & exit /B 0"; Flags: runhidden waituntilterminated; RunOnceId: "StopServerService"



Filename: "{sys}\cmd.exe"; Parameters: "/C sc delete RemoteMonitor.Server.Service >NUL 2>&1 & exit /B 0"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteServerService"



Filename: "{sys}\cmd.exe"; Parameters: "/C reg delete HKLM\Software\Microsoft\Windows\CurrentVersion\Run /v RemoteMonitorServerUI /f >NUL 2>&1 & exit /B 0"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteServerUiRun"












[Code]



var



  ModePage: TWizardPage;



  RemoveComponentPage: TWizardPage;
  RemoveProgressPage: TWizardPage;



  InstallModeRadio: TNewRadioButton;



  RemoveModeRadio: TNewRadioButton;
  RemoveClientCheckBox: TNewCheckBox;
  RemoveServerCheckBox: TNewCheckBox;



  InstallDirPage: TInputDirWizardPage;



  RemoveStatusLabel: TNewStaticText;



  RemoveProgressBar: TNewProgressBar;



  ProductInfoLabel: TNewStaticText;



  RemoveCompleted: Boolean;
  ComponentsInitialized: Boolean;



procedure ExitProcess(exitCode: Integer); external 'ExitProcess@kernel32.dll stdcall';

function SelectedComponentsText(): String;



begin



  Result := '';



  if WizardIsComponentSelected('client') then



    Result := Result + 'Client, ';



  if WizardIsComponentSelected('server') then



    Result := Result + 'Server, ';



  if Result <> '' then



    Delete(Result, Length(Result) - 1, 2);



end;



function IsRemoveMode(): Boolean;



begin



  Result := (RemoveModeRadio <> nil) and RemoveModeRadio.Checked;



end;




function IsRemoveClientSelected(): Boolean;
begin
  Result := IsRemoveMode() and (RemoveClientCheckBox <> nil) and RemoveClientCheckBox.Checked;
end;

function IsRemoveServerSelected(): Boolean;
begin
  Result := IsRemoveMode() and (RemoveServerCheckBox <> nil) and RemoveServerCheckBox.Checked;
end;
function RemoveTrailingBackslash(Path: String): String;



begin



  Result := Path;



  while (Length(Result) > 3) and (Copy(Result, Length(Result), 1) = '\') do



    Delete(Result, Length(Result), 1);



end;



function EndsWithText(Value: String; Suffix: String): Boolean;



begin



  Result := CompareText(Copy(Value, Length(Value) - Length(Suffix) + 1, Length(Suffix)), Suffix) = 0;



end;



function IsDriveRoot(Path: String): Boolean;



begin



  Result := (Length(Path) = 3) and (Copy(Path, 2, 2) = ':\');



end;



function NormalizeComponentDir(Path: String; ComponentDir: String): String;



var



  Base: String;



begin



  Base := RemoveTrailingBackslash(Path);



  if Base = '' then



    Base := 'C:\RDP';



  if EndsWithText(Base, '\RDP\' + ComponentDir) then



  begin



    Result := Base;



    Exit;



  end;



  if EndsWithText(Base, '\RDP') then



  begin



    Result := AddBackslash(Base) + ComponentDir;



    Exit;



  end;



  if IsDriveRoot(Base) then



  begin



    Result := AddBackslash(Base) + 'RDP\' + ComponentDir;



    Exit;



  end;



  Result := AddBackslash(Base) + 'RDP\' + ComponentDir;



end;



function GetClientDir(Param: String): String;



begin



  if (InstallDirPage <> nil) and WizardIsComponentSelected('client') then



    Result := NormalizeComponentDir(InstallDirPage.Values[0], 'Client')



  else



    Result := 'C:\RDP\Client';



end;



function GetServerDir(Param: String): String;



begin



  if (InstallDirPage <> nil) and WizardIsComponentSelected('server') then



  begin



    if WizardIsComponentSelected('client') then



      Result := NormalizeComponentDir(InstallDirPage.Values[1], 'Server')



    else



      Result := NormalizeComponentDir(InstallDirPage.Values[0], 'Server');



  end



  else



  begin



    Result := 'C:\RDP\Server';



  end;



end;



function GetInstallRootDir(Param: String): String;



begin



  if (InstallDirPage <> nil) and WizardIsComponentSelected('client') then



  begin



    Result := ExtractFileDir(GetClientDir(''));



    Exit;



  end;



  if (InstallDirPage <> nil) and WizardIsComponentSelected('server') then



  begin



    Result := ExtractFileDir(GetServerDir(''));



    Exit;



  end;



  Result := 'C:\RDP';



end;



function GetInstalledClientDir(): String;



begin



  if not RegQueryStringValue(HKLM, 'Software\RemoteMonitor', 'ClientDir', Result) then



    Result := 'C:\RDP\Client';



end;



function GetInstalledServerDir(): String;



begin



  if not RegQueryStringValue(HKLM, 'Software\RemoteMonitor', 'ServerDir', Result) then



    Result := 'C:\RDP\Server';



end;



function CheckSelectedProcesses(): Boolean;



var



  ResultCode: Integer;



  Command: String;



begin



  Command := '-NoProfile -ExecutionPolicy Bypass -Command "$names=@();';



  if IsRemoveClientSelected() or ((not IsRemoveMode()) and WizardIsComponentSelected('client')) then



    Command := Command + '$names+= ''RemoteMonitor.Client'';';



  if IsRemoveServerSelected() or ((not IsRemoveMode()) and WizardIsComponentSelected('server')) then



  begin



    Command := Command + '$names+= ''RemoteMonitor.Server'';';



    Command := Command + '$names+= ''RemoteMonitor.Server.Service'';';



  end;



  Command := Command +



    '$running=@(); foreach($n in $names){ if(Get-Process -Name $n -ErrorAction SilentlyContinue){ $running += $n } };' +



    'if($running.Count -gt 0){ exit 1 } else { exit 0 }"';



  if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Command, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then



  begin



    Result := True;



    Exit;



  end;



  Result := ResultCode = 0;



end;

function IsProcessRunning(ProcessName: String): Boolean;
var
  ResultCode: Integer;
  Command: String;
begin
  Command := '-NoProfile -ExecutionPolicy Bypass -Command "' +
    'if(Get-Process -Name ''' + ProcessName + ''' -ErrorAction SilentlyContinue){ exit 1 } else { exit 0 }"';

  if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Command, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := False;
    Exit;
  end;

  Result := ResultCode <> 0;
end;

function WaitForServerProcessesToStop(): Boolean;
var
  Attempt: Integer;
begin
  Result := False;
  for Attempt := 1 to 20 do
  begin
    if (not IsProcessRunning('RemoteMonitor.Server')) and
       (not IsProcessRunning('RemoteMonitor.Server.Service')) then
    begin
      Result := True;
      Exit;
    end;

    Sleep(500);
  end;
end;

function WaitForClientProcessToStop(): Boolean;
var
  Attempt: Integer;
begin
  Result := False;
  for Attempt := 1 to 20 do
  begin
    if not IsProcessRunning('RemoteMonitor.Client') then
    begin
      Result := True;
      Exit;
    end;

    Sleep(500);
  end;
end;

function StopClientForInstall(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;

  if not IsProcessRunning('RemoteMonitor.Client') then
    Exit;

  if MsgBox(
    'RDP Client가 실행 중입니다.' + #13#10#13#10 +
    '설치를 진행하려면 Client를 종료해야 합니다.' + #13#10 +
    '종료한 뒤 설치를 진행할까요?',
    mbConfirmation,
    MB_YESNO) <> IDYES then
  begin
    ExitProcess(0);
    Exit;
  end;

  Exec(ExpandConstant('{sys}\cmd.exe'), '/C taskkill /IM RemoteMonitor.Client.exe /F >NUL 2>&1 & exit /B 0', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  Result := WaitForClientProcessToStop();
  if not Result then
    MsgBox('RDP Client를 종료하지 못했습니다. 작업 관리자에서 종료한 뒤 다시 설치해 주세요.', mbError, MB_OK);
end;

function StopServerForInstall(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;

  if (not IsProcessRunning('RemoteMonitor.Server')) and
     (not IsProcessRunning('RemoteMonitor.Server.Service')) then
    Exit;

  if MsgBox(
    'RDP Server가 실행 중입니다.' + #13#10#13#10 +
    '설치를 진행하려면 Server UI와 Server Service를 종료해야 합니다.' + #13#10 +
    '종료한 뒤 설치를 진행할까요?',
    mbConfirmation,
    MB_YESNO) <> IDYES then
  begin
    ExitProcess(0);
    Exit;
  end;

  Exec(ExpandConstant('{sys}\cmd.exe'), '/C taskkill /IM RemoteMonitor.Server.exe /F >NUL 2>&1 & exit /B 0', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop RemoteMonitor.Server.Service', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  Result := WaitForServerProcessesToStop();
  if not Result then
    MsgBox('RDP Server Service를 종료하지 못했습니다. 작업 관리자 또는 서비스 관리에서 종료한 뒤 다시 설치해 주세요.', mbError, MB_OK);
end;



#ifdef FrameworkDependent



function HasDotNetDesktopRuntime(): Boolean;



var



  ResultCode: Integer;



begin



  Result := Exec(



    ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),



    '-NoProfile -ExecutionPolicy Bypass -Command "try { $r = & dotnet --list-runtimes 2>$null; if ($r -match ''Microsoft\.WindowsDesktop\.App 8\.'') { exit 0 } else { exit 1 } } catch { exit 1 }"',



    '',



    SW_HIDE,



    ewWaitUntilTerminated,



    ResultCode);



  Result := Result and (ResultCode = 0);



end;



#endif



function PrepareToInstall(var NeedsRestart: Boolean): String;



var



  Button: Integer;



begin



  Result := '';



  if IsRemoveMode() then



    Exit;



#ifdef FrameworkDependent



  if not HasDotNetDesktopRuntime() then



  begin



    Result := '.NET 8 Desktop Runtime x64가 설치되어 있지 않습니다. 런타임 설치 후 다시 실행해 주세요.';



    Exit;



  end;



#endif



  if WizardIsComponentSelected('client') and (not StopClientForInstall()) then
  begin
    Result := 'RDP Client를 종료하지 못해 설치를 진행할 수 없습니다.';
    Exit;
  end;

  if WizardIsComponentSelected('server') and (not StopServerForInstall()) then
  begin
    Result := 'RDP Server를 종료하지 못해 설치를 진행할 수 없습니다.';
    Exit;
  end;



  while not CheckSelectedProcesses() do



  begin



    Button := MsgBox(



      '설치하려는 구성요소 중 실행 중인 프로그램이 있습니다.' + #13#10#13#10 +



      '실행 중인 ' + SelectedComponentsText() + '를 종료한 뒤 [다시 시도]를 클릭해 주세요.',



      mbError,



      MB_RETRYCANCEL);



    if Button = IDCANCEL then



    begin



      Result := '실행 중인 프로그램이 있어 설치를 취소했습니다.';



      Exit;



    end;



  end;



end;




function RemoveSelectedComponentsText(): String;
begin
  Result := '';
  if IsRemoveClientSelected() then
    Result := Result + 'Client, ';
  if IsRemoveServerSelected() then
    Result := Result + 'Server, ';
  if Result <> '' then
    Delete(Result, Length(Result) - 1, 2);
end;
function NextButtonClick(CurPageID: Integer): Boolean;



begin



  Result := True;



  if (RemoveProgressPage <> nil) and (CurPageID = RemoveProgressPage.ID) then
  begin
    if RemoveCompleted then
    begin
      ExitProcess(0);
      Result := False;
      Exit;
    end;

    Result := False;
    Exit;
  end;

  if (RemoveComponentPage <> nil) and (CurPageID = RemoveComponentPage.ID) then
  begin
    if (not IsRemoveClientSelected()) and (not IsRemoveServerSelected()) then
    begin
      MsgBox('삭제할 구성요소를 하나 이상 선택해 주세요.', mbInformation, MB_OK);
      Result := False;
      Exit;
    end;

    if MsgBox(
      '선택한 Remote Monitor 구성요소를 삭제합니다.' + #13#10#13#10 +
      '선택 항목: ' + RemoveSelectedComponentsText() + #13#10#13#10 +
      '계속할까요?',
      mbConfirmation,
      MB_YESNO) <> IDYES then
    begin
      Result := False;
      Exit;
    end;
  end;



  if CurPageID = wpSelectComponents then



  begin



    if SelectedComponentsText() = '' then



    begin



      MsgBox('설치할 구성요소를 하나 이상 선택해 주세요.', mbInformation, MB_OK);



      Result := False;



    end;



  end;



  if (InstallDirPage <> nil) and (CurPageID = InstallDirPage.ID) then



  begin



    if WizardIsComponentSelected('client') then



      InstallDirPage.Values[0] := GetClientDir('');



    if WizardIsComponentSelected('server') then



    begin



      if WizardIsComponentSelected('client') then



        InstallDirPage.Values[1] := GetServerDir('')



      else



        InstallDirPage.Values[0] := GetServerDir('');



    end;



    WizardForm.DirEdit.Text := GetInstallRootDir('');



  end;



end;



function ShouldSkipPage(PageID: Integer): Boolean;



begin



  Result := False;



  if IsRemoveMode() then



  begin



    if (PageID = wpSelectComponents) or (PageID = wpSelectTasks) or (PageID = wpReady) or (PageID = wpPreparing) or (PageID = wpInstalling) or (PageID = wpFinished) then



      Result := True;



  end;



  if (RemoveComponentPage <> nil) and (PageID = RemoveComponentPage.ID) then
    Result := not IsRemoveMode();

  if (RemoveProgressPage <> nil) and (PageID = RemoveProgressPage.ID) then



    Result := not IsRemoveMode();



  if (InstallDirPage <> nil) and (PageID = InstallDirPage.ID) then



    Result := IsRemoveMode() or (not WizardIsComponentSelected('client') and not WizardIsComponentSelected('server'));



end;



procedure SetRemoveProgress(Position: Integer; StatusText: String);



begin



  RemoveProgressBar.Position := Position;



  RemoveStatusLabel.Caption := StatusText;



  WizardForm.Refresh;



end;



procedure PerformRemoval;



var



  ResultCode: Integer;
  ClientRoot: String;
  ServerRoot: String;



begin



  WizardForm.BackButton.Enabled := False;



  WizardForm.NextButton.Enabled := False;



  WizardForm.CancelButton.Enabled := False;



  SetRemoveProgress(5, '실행 중인 프로그램을 종료하는 중...');



  if IsRemoveClientSelected() then
    Exec(ExpandConstant('{sys}\cmd.exe'), '/C taskkill /IM RemoteMonitor.Client.exe /F >NUL 2>&1 & exit /B 0', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);



  if IsRemoveServerSelected() then
    Exec(ExpandConstant('{sys}\cmd.exe'), '/C taskkill /IM RemoteMonitor.Server.exe /F >NUL 2>&1 & exit /B 0', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);



  SetRemoveProgress(20, 'Windows Service를 중지하는 중...');



  if IsRemoveServerSelected() then
    Exec(ExpandConstant('{sys}\cmd.exe'), '/C sc stop RemoteMonitor.Server.Service >NUL 2>&1 & exit /B 0', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  if IsRemoveServerSelected() and (not WaitForServerProcessesToStop()) then
  begin
    Exec(ExpandConstant('{sys}\cmd.exe'), '/C taskkill /IM RemoteMonitor.Server.Service.exe /F >NUL 2>&1 & exit /B 0', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    WaitForServerProcessesToStop();
  end;



  SetRemoveProgress(35, 'Windows Service 등록을 삭제하는 중...');



  if IsRemoveServerSelected() then
    Exec(ExpandConstant('{sys}\cmd.exe'), '/C sc delete RemoteMonitor.Server.Service >NUL 2>&1 & exit /B 0', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);



  if IsRemoveClientSelected() then
    Exec(ExpandConstant('{sys}\cmd.exe'), '/C reg delete HKLM\Software\Microsoft\Windows\CurrentVersion\Run /v RemoteMonitorClient /f >NUL 2>&1 & exit /B 0', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  if IsRemoveServerSelected() then
    Exec(ExpandConstant('{sys}\cmd.exe'), '/C reg delete HKLM\Software\Microsoft\Windows\CurrentVersion\Run /v RemoteMonitorServerUI /f >NUL 2>&1 & exit /B 0', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);















  SetRemoveProgress(68, '설치 파일을 삭제하는 중...');

  ClientRoot := ExtractFileDir(GetInstalledClientDir());
  ServerRoot := ExtractFileDir(GetInstalledServerDir());



  if IsRemoveClientSelected() then
    DelTree(GetInstalledClientDir(), True, True, True);



  if IsRemoveServerSelected() then
    DelTree(GetInstalledServerDir(), True, True, True);



  SetRemoveProgress(82, '바로가기를 삭제하는 중...');



  if IsRemoveClientSelected() then
    DeleteFile(ExpandConstant('{commondesktop}\Remote Monitor Client.lnk'));



  if IsRemoveServerSelected() then
    DeleteFile(ExpandConstant('{commondesktop}\Remote Monitor Server.lnk'));



  if IsRemoveClientSelected() then
    DeleteFile(ExpandConstant('{commonprograms}\Remote Monitor\Remote Monitor Client.lnk'));



  if IsRemoveServerSelected() then
    DeleteFile(ExpandConstant('{commonprograms}\Remote Monitor\Remote Monitor Server.lnk'));



  RemoveDir(ExpandConstant('{commonprograms}\Remote Monitor'));



  SetRemoveProgress(94, '설치 정보를 정리하는 중...');



  if IsRemoveClientSelected() then
    RegDeleteValue(HKLM, 'Software\RemoteMonitor', 'ClientDir');



  if IsRemoveServerSelected() then
    RegDeleteValue(HKLM, 'Software\RemoteMonitor', 'ServerDir');



  RegDeleteKeyIfEmpty(HKLM, 'Software\RemoteMonitor');

  if (not DirExists(GetInstalledClientDir())) and (not DirExists(GetInstalledServerDir())) then
  begin
    RegDeleteKeyIncludingSubkeys(HKLM, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{D6DEDF3D-393A-48F0-B563-57FCA0F3214F}_is1');
    Exec(ExpandConstant('{sys}\cmd.exe'), '/C del /Q "' + AddBackslash(ClientRoot) + 'unins*.exe" "' + AddBackslash(ClientRoot) + 'unins*.dat" >NUL 2>&1 & exit /B 0', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    if CompareText(ClientRoot, ServerRoot) <> 0 then
      Exec(ExpandConstant('{sys}\cmd.exe'), '/C del /Q "' + AddBackslash(ServerRoot) + 'unins*.exe" "' + AddBackslash(ServerRoot) + 'unins*.dat" >NUL 2>&1 & exit /B 0', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    RemoveDir(ClientRoot);
    if CompareText(ClientRoot, ServerRoot) <> 0 then
      RemoveDir(ServerRoot);
  end;



  SetRemoveProgress(100, '삭제가 완료되었습니다.');



  RemoveCompleted := True;



  WizardForm.NextButton.Caption := '종료';
  WizardForm.NextButton.Enabled := True;
  WizardForm.BackButton.Visible := False;
  WizardForm.CancelButton.Visible := False;



end;



procedure UpdateInstallDirPage;



begin



  if InstallDirPage = nil then



    Exit;



  InstallDirPage.PromptLabels[0].Visible := True;



  InstallDirPage.Edits[0].Visible := True;



  InstallDirPage.Buttons[0].Visible := True;



  InstallDirPage.PromptLabels[1].Visible := False;



  InstallDirPage.Edits[1].Visible := False;



  InstallDirPage.Buttons[1].Visible := False;



  if WizardIsComponentSelected('client') and WizardIsComponentSelected('server') then



  begin



    InstallDirPage.PromptLabels[0].Caption := 'Client 설치 경로:';



    InstallDirPage.PromptLabels[1].Caption := 'Server 설치 경로:';



    InstallDirPage.Values[0] := 'C:\RDP\Client';



    InstallDirPage.Values[1] := 'C:\RDP\Server';



    InstallDirPage.PromptLabels[1].Visible := True;



    InstallDirPage.Edits[1].Visible := True;



    InstallDirPage.Buttons[1].Visible := True;



    Exit;



  end;



  if WizardIsComponentSelected('client') then



  begin



    InstallDirPage.PromptLabels[0].Caption := 'Client 설치 경로:';



    InstallDirPage.Values[0] := 'C:\RDP\Client';



    Exit;



  end;



  if WizardIsComponentSelected('server') then



  begin



    InstallDirPage.PromptLabels[0].Caption := 'Server 설치 경로:';



    InstallDirPage.Values[0] := 'C:\RDP\Server';



  end;



end;



procedure CurPageChanged(CurPageID: Integer);



begin

  if (CurPageID = wpSelectComponents) and (not ComponentsInitialized) then
  begin
    WizardSelectComponents('');
    ComponentsInitialized := True;
  end;



  if (InstallDirPage <> nil) and (CurPageID = InstallDirPage.ID) then



    UpdateInstallDirPage();



  if (RemoveProgressPage <> nil) and (CurPageID = RemoveProgressPage.ID) then



  begin



    RemoveCompleted := False;



    WizardForm.NextButton.Caption := '다음(&N)';



    PerformRemoval();



  end;



end;



procedure InitializeWizard;



begin



  RemoveCompleted := False;
  ComponentsInitialized := False;



  ModePage := CreateCustomPage(



    wpWelcome,



    '작업 선택',



    '설치 또는 삭제 작업을 선택해 주세요.');



  InstallModeRadio := TNewRadioButton.Create(ModePage);



  InstallModeRadio.Parent := ModePage.Surface;



  InstallModeRadio.Left := 0;



  InstallModeRadio.Top := 16;



  InstallModeRadio.Width := ModePage.SurfaceWidth;



  InstallModeRadio.Caption := '설치';



  InstallModeRadio.Checked := True;



  RemoveModeRadio := TNewRadioButton.Create(ModePage);



  RemoveModeRadio.Parent := ModePage.Surface;



  RemoveModeRadio.Left := 0;



  RemoveModeRadio.Top := 46;



  RemoveModeRadio.Width := ModePage.SurfaceWidth;



  RemoveModeRadio.Caption := '삭제';



  ProductInfoLabel := TNewStaticText.Create(ModePage);



  ProductInfoLabel.Parent := ModePage.Surface;



  ProductInfoLabel.Left := 0;



  ProductInfoLabel.Top := ModePage.SurfaceHeight - 98;



  ProductInfoLabel.Width := ModePage.SurfaceWidth;



  ProductInfoLabel.Height := 94;



  ProductInfoLabel.Caption :=



    '제작자 : 김태영 사원 (센싱SW 전장파트)' + #13#10 +



    '문의처 : bigzero3949@partron.co.kr' + #13#10 +



    '배포일 : {#MyReleaseDate}' + #13#10 +

    '프로그램 버전' + #13#10 +



    'Client : {#MyAppVersion}    Server : {#MyAppVersion}';




  RemoveComponentPage := CreateCustomPage(
    ModePage.ID,
    '삭제 구성요소 선택',
    '삭제할 구성요소를 선택해 주세요.');

  RemoveClientCheckBox := TNewCheckBox.Create(RemoveComponentPage);
  RemoveClientCheckBox.Parent := RemoveComponentPage.Surface;
  RemoveClientCheckBox.Left := 0;
  RemoveClientCheckBox.Top := 20;
  RemoveClientCheckBox.Width := RemoveComponentPage.SurfaceWidth;
  RemoveClientCheckBox.Caption := 'Client 삭제';
  RemoveClientCheckBox.Checked := DirExists(GetInstalledClientDir());

  RemoveServerCheckBox := TNewCheckBox.Create(RemoveComponentPage);
  RemoveServerCheckBox.Parent := RemoveComponentPage.Surface;
  RemoveServerCheckBox.Left := 0;
  RemoveServerCheckBox.Top := 52;
  RemoveServerCheckBox.Width := RemoveComponentPage.SurfaceWidth;
  RemoveServerCheckBox.Caption := 'Server 삭제';
  RemoveServerCheckBox.Checked := DirExists(GetInstalledServerDir());
  RemoveProgressPage := CreateCustomPage(



    RemoveComponentPage.ID,



    '삭제 진행 중',



    'Remote Monitor를 삭제하고 있습니다.');



  RemoveStatusLabel := TNewStaticText.Create(RemoveProgressPage);



  RemoveStatusLabel.Parent := RemoveProgressPage.Surface;



  RemoveStatusLabel.Left := 0;



  RemoveStatusLabel.Top := 24;



  RemoveStatusLabel.Width := RemoveProgressPage.SurfaceWidth;



  RemoveStatusLabel.Caption := '삭제를 준비하는 중...';



  RemoveProgressBar := TNewProgressBar.Create(RemoveProgressPage);



  RemoveProgressBar.Parent := RemoveProgressPage.Surface;



  RemoveProgressBar.Left := 0;



  RemoveProgressBar.Top := 58;



  RemoveProgressBar.Width := RemoveProgressPage.SurfaceWidth;



  RemoveProgressBar.Height := 18;



  RemoveProgressBar.Min := 0;



  RemoveProgressBar.Max := 100;



  RemoveProgressBar.Position := 0;



  InstallDirPage := CreateInputDirPage(



    wpSelectComponents,



    '설치 경로 선택',



    '설치할 경로를 선택해 주세요.',



    '기본 경로를 사용하거나 필요한 경우 경로를 수정할 수 있습니다.',



    False,



    '');



  InstallDirPage.Add('Client 설치 경로:');



  InstallDirPage.Add('Server 설치 경로:');



  InstallDirPage.Values[0] := 'C:\RDP\Client';



  InstallDirPage.Values[1] := 'C:\RDP\Server';



end;



