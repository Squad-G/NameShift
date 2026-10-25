; NameShift installer (Inno Setup 6), per user only: no admin rights and no UAC prompt.
; Build it with installer/build-installer.sh, which stages the files and passes AppVersion and StageDir to ISCC.

#ifndef AppVersion
  ; A default for editing only: build-installer.sh always passes /DAppVersion=<x.y.z>, read from NameShift.exe.
  #define AppVersion "0.0.0"
#endif
#ifndef StageDir
  #define StageDir "Output\stage"
#endif

[Setup]
; AppId: generated once for NameShift (E7168693-3863-4A40-9072-736D2750DC75). Never change it: an upgrade finds the
; installed version by it. The doubled opening brace is Inno's escape for a literal brace.
AppId={{E7168693-3863-4A40-9072-736D2750DC75}
AppName=NameShift
AppVersion={#AppVersion}
AppVerName=NameShift {#AppVersion}
AppPublisher=Squad-G
AppPublisherURL=https://github.com/Squad-G/NameShift
AppSupportURL=https://github.com/Squad-G/NameShift
VersionInfoVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\NameShift
DefaultGroupName=NameShift
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=Output
OutputBaseFilename=NameShift-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
LicenseFile=..\LICENSE
ChangesEnvironment=yes
CloseApplications=yes
RestartApplications=no
UninstallDisplayName=NameShift
UninstallDisplayIcon={app}\NameShift.exe
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked
Name: "addtopath"; Description: "Add NameShift to PATH"; Flags: unchecked
Name: "explorermenu"; Description: "Add ""Bulk Rename Here"" to the Explorer right-click menu"

[Files]
Source: "{#StageDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{autoprograms}\NameShift"; Filename: "{app}\NameShift.exe"
Name: "{autodesktop}\NameShift"; Filename: "{app}\NameShift.exe"; Tasks: desktopicon

[Registry]
; The Explorer entry "Bulk Rename Here" (NS-49 criterion 1), for the current user only. The * key is a literal key name.
; A quote inside an Inno string is doubled: """{app}\NameShift.exe""" writes "<install folder>\NameShift.exe".
Root: HKCU; Subkey: "Software\Classes\Directory\shell\NameShift"; ValueType: string; ValueName: ""; ValueData: "Bulk Rename Here"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\NameShift"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\NameShift.exe"""; Tasks: explorermenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\NameShift\command"; ValueType: string; ValueName: ""; ValueData: """{app}\NameShift.exe"" ""%1"""; Tasks: explorermenu
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\NameShift"; ValueType: string; ValueName: ""; ValueData: "Bulk Rename Here"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\NameShift"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\NameShift.exe"""; Tasks: explorermenu
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\NameShift\command"; ValueType: string; ValueName: ""; ValueData: """{app}\NameShift.exe"" ""%V"""; Tasks: explorermenu
Root: HKCU; Subkey: "Software\Classes\*\shell\NameShift"; ValueType: string; ValueName: ""; ValueData: "Bulk Rename Here"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKCU; Subkey: "Software\Classes\*\shell\NameShift"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\NameShift.exe"""; Tasks: explorermenu
Root: HKCU; Subkey: "Software\Classes\*\shell\NameShift\command"; ValueType: string; ValueName: ""; ValueData: """{app}\NameShift.exe"" ""%1"""; Tasks: explorermenu
; Task unchecked on a reinstall: remove the entry an earlier install wrote (only these three keys; nothing if absent).
Root: HKCU; Subkey: "Software\Classes\Directory\shell\NameShift"; ValueType: none; Flags: deletekey; Tasks: not explorermenu
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\NameShift"; ValueType: none; Flags: deletekey; Tasks: not explorermenu
Root: HKCU; Subkey: "Software\Classes\*\shell\NameShift"; ValueType: none; Flags: deletekey; Tasks: not explorermenu

[Code]
const
  // .NET Framework 4.8 or later: the Release value of this key is at least 528040.
  NetFxKey = 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full';
  NetFx48Release = 528040;

  EnvironmentKey = 'Environment';
  PathValue = 'Path';

  // Raw Win32 values for the advapi32 calls below. Setup is always a 32-bit process, so a handle fits a Cardinal.
  HKEY_CURRENT_USER_HANDLE = $80000001;
  KEY_QUERY_VALUE = $0001;
  ERROR_SUCCESS = 0;
  ERROR_FILE_NOT_FOUND = 2;
  REG_SZ = 1;
  REG_EXPAND_SZ = 2;

  // What PathKind finds.
  PathAbsent = 0;
  PathPresent = 1;
  PathUnreadable = 2;

function RegOpenKeyExW(hKey: Cardinal; lpSubKey: String; ulOptions: Cardinal; samDesired: Cardinal;
  var phkResult: Cardinal): Integer;
  external 'RegOpenKeyExW@advapi32.dll stdcall';

// lpData and lpcbData are passed as 0 (NULL): only the type is read.
function RegQueryValueExW(hKey: Cardinal; lpValueName: String; lpReserved: Cardinal; var lpType: Cardinal;
  lpData: Cardinal; lpcbData: Cardinal): Integer;
  external 'RegQueryValueExW@advapi32.dll stdcall';

function RegCloseKey(hKey: Cardinal): Integer;
  external 'RegCloseKey@advapi32.dll stdcall';

// ---- .NET Framework 4.8 ----

function HasNetFx48: Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, NetFxKey, 'Release', Release) and (Release >= NetFx48Release);
  if (not Result) and IsWin64 then
    Result := RegQueryDWordValue(HKLM64, NetFxKey, 'Release', Release) and (Release >= NetFx48Release);
end;

function InitializeSetup: Boolean;
begin
  Result := HasNetFx48;
  if not Result then
    SuppressibleMsgBox('NameShift needs .NET Framework 4.8, which is not installed on this computer.' + #13#10#13#10 +
      'Install .NET Framework 4.8 from Microsoft (or update Windows), then run this setup again.',
      mbCriticalError, MB_OK, IDOK);
end;

// ---- The data folders: never an install folder ----

function NormalizedDir(Dir: String): String;
begin
  Result := AnsiLowercase(AddBackslash(ExpandFileName(Dir)));
end;

// True when Dir is %LocalAppData%\NameShift or %AppData%\NameShift, or a folder inside one. The trailing backslash of
// both sides makes "...\NameShiftOld" no match, and the default "%LocalAppData%\Programs\NameShift" is no match either.
function IsDataFolder(Dir: String): Boolean;
var
  D, LocalData, RoamingData: String;
begin
  D := NormalizedDir(Dir);
  LocalData := NormalizedDir(ExpandConstant('{localappdata}\NameShift'));
  RoamingData := NormalizedDir(ExpandConstant('{userappdata}\NameShift'));
  Result := (Copy(D, 1, Length(LocalData)) = LocalData) or (Copy(D, 1, Length(RoamingData)) = RoamingData);
end;

function DataFolderMessage(Dir: String): String;
begin
  Result := 'NameShift cannot be installed in "' + Dir + '": that folder holds your NameShift settings, presets ' +
    'and history, which uninstall may delete. Choose another folder.';
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = wpSelectDir) and IsDataFolder(WizardDirValue) then
  begin
    SuppressibleMsgBox(DataFolderMessage(WizardDirValue), mbError, MB_OK, IDOK);
    Result := False;
  end;
end;

// Also refuses a data folder given with /DIR= to a silent setup, which never shows the folder page.
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if IsDataFolder(WizardDirValue) then
    Result := DataFolderMessage(WizardDirValue);
end;

// ---- PATH: HKCU\Environment\Path, one entry, its kind kept ----

// Reads only the kind of the value (REG_SZ or REG_EXPAND_SZ) through advapi32, because RegQueryStringValue does not
// tell it. Never guesses the kind from the text.
function PathKind(var Kind: Cardinal): Integer;
var
  Key: Cardinal;
  Error: Integer;
begin
  Kind := 0;
  Key := 0;
  Error := RegOpenKeyExW(HKEY_CURRENT_USER_HANDLE, EnvironmentKey, 0, KEY_QUERY_VALUE, Key);
  if Error = ERROR_FILE_NOT_FOUND then
  begin
    Result := PathAbsent;
    Exit;
  end;
  if Error <> ERROR_SUCCESS then
  begin
    Result := PathUnreadable;
    Exit;
  end;
  try
    Error := RegQueryValueExW(Key, PathValue, 0, Kind, 0, 0);
    if Error = ERROR_SUCCESS then
      Result := PathPresent
    else if Error = ERROR_FILE_NOT_FOUND then
      Result := PathAbsent
    else
      Result := PathUnreadable;
  finally
    RegCloseKey(Key);
  end;
end;

// Entries compare without case and without a trailing backslash (and surrounding spaces).
function SamePathEntry(Entry, Dir: String): Boolean;
var
  A, B: String;
begin
  A := AnsiLowercase(Trim(Entry));
  B := AnsiLowercase(Trim(Dir));
  while (Length(A) > 0) and (A[Length(A)] = '\') do
    A := Copy(A, 1, Length(A) - 1);
  while (Length(B) > 0) and (B[Length(B)] = '\') do
    B := Copy(B, 1, Length(B) - 1);
  Result := (A <> '') and (A = B);
end;

function PathHasEntry(Path, Dir: String): Boolean;
var
  Rest, Entry: String;
  P: Integer;
begin
  Result := False;
  Rest := Path;
  while not Result do
  begin
    P := Pos(';', Rest);
    if P = 0 then
    begin
      Result := SamePathEntry(Rest, Dir);
      Exit;
    end;
    Entry := Copy(Rest, 1, P - 1);
    Rest := Copy(Rest, P + 1, Length(Rest));
    Result := SamePathEntry(Entry, Dir);
  end;
end;

// Path without the entries equal to Dir; every other entry (empty ones too) is kept as it was, in order.
function PathWithoutEntry(Path, Dir: String): String;
var
  Rest, Entry: String;
  P: Integer;
  First, Last: Boolean;
begin
  Result := '';
  First := True;
  Rest := Path;
  Last := False;
  while not Last do
  begin
    P := Pos(';', Rest);
    if P = 0 then
    begin
      Entry := Rest;
      Last := True;
    end
    else
    begin
      Entry := Copy(Rest, 1, P - 1);
      Rest := Copy(Rest, P + 1, Length(Rest));
    end;
    if not SamePathEntry(Entry, Dir) then
    begin
      if First then
        Result := Entry
      else
        Result := Result + ';' + Entry;
      First := False;
    end;
  end;
end;

function WritePath(Kind: Cardinal; Value: String): Boolean;
begin
  if Kind = REG_EXPAND_SZ then
    Result := RegWriteExpandStringValue(HKCU, EnvironmentKey, PathValue, Value)
  else
    Result := RegWriteStringValue(HKCU, EnvironmentKey, PathValue, Value);
end;

procedure AddAppToPath;
var
  App, OldPath, NewPath: String;
  Kind: Cardinal;
  Found: Integer;
begin
  App := ExpandConstant('{app}');
  Found := PathKind(Kind);
  if Found = PathAbsent then
  begin
    if not RegWriteExpandStringValue(HKCU, EnvironmentKey, PathValue, App) then
      Log('PATH: could not create the Path value.');
    Exit;
  end;
  if (Found <> PathPresent) or ((Kind <> REG_SZ) and (Kind <> REG_EXPAND_SZ)) then
  begin
    Log('PATH: the Path value cannot be read as text; left unchanged.');
    Exit;
  end;
  if not RegQueryStringValue(HKCU, EnvironmentKey, PathValue, OldPath) then
  begin
    Log('PATH: the Path value cannot be read; left unchanged.');
    Exit;
  end;
  if PathHasEntry(OldPath, App) then
    Exit;
  if OldPath = '' then
    NewPath := App
  else if OldPath[Length(OldPath)] = ';' then
    NewPath := OldPath + App
  else
    NewPath := OldPath + ';' + App;
  if not WritePath(Kind, NewPath) then
    Log('PATH: could not write the Path value.');
end;

procedure RemoveAppFromPath;
var
  App, OldPath, NewPath: String;
  Kind: Cardinal;
begin
  App := ExpandConstant('{app}');
  if PathKind(Kind) <> PathPresent then
    Exit;
  if (Kind <> REG_SZ) and (Kind <> REG_EXPAND_SZ) then
    Exit;
  if not RegQueryStringValue(HKCU, EnvironmentKey, PathValue, OldPath) then
    Exit;
  if not PathHasEntry(OldPath, App) then
    Exit;
  NewPath := PathWithoutEntry(OldPath, App);
  if not WritePath(Kind, NewPath) then
    Log('PATH: could not write the Path value.');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if WizardIsTaskSelected('addtopath') then
      AddAppToPath
    else
      // Task unchecked on a reinstall: take out the entry an earlier install added (nothing if absent).
      RemoveAppFromPath;
  end;
end;

// ---- Uninstall ----

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RemoveAppFromPath;

  // The user's data is kept unless they answer Yes; a silent uninstall never asks and keeps it.
  if (CurUninstallStep = usPostUninstall) and (not UninstallSilent) then
  begin
    if MsgBox('Also delete your NameShift settings, presets and history?' + #13#10#13#10 +
      'If you delete the history, Revert of past renames is no longer possible.',
      mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
    begin
      DelTree(ExpandConstant('{localappdata}\NameShift'), True, True, True);
      DelTree(ExpandConstant('{userappdata}\NameShift'), True, True, True);
    end;
  end;
end;
