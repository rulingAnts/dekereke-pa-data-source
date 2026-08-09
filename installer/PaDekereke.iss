; -----------------------------------------------------------------------------
; Inno Setup script for the PaDekereke add-on.
;
; What it must do:
;   1. Find the Phonology Assistant installation (PA installs per-machine,
;      ALLUSERS=1, so this installer requests elevation).
;   2. Copy PaDekereke.dll + DekerekeToPa.dll (and any netstandard support DLLs
;      from the build output) into <PA>\AddOns\.
;   3. Uninstall = remove those files (and AddOns if empty).
;
; PA detection, in order:
;   a. MSI: PA's UpgradeCode is fixed across versions:
;      {5E57E4D4-580A-4cc1-9E0C-7EF8D3F81BBD}   (Installer/Product.wxs:9 in the
;      sillsdev/phonology-assistant repo). MsiEnumRelatedProducts +
;      MsiGetProductInfo(INSTALLPROPERTY_INSTALLLOCATION) yields the folder.
;      TODO(cloud/VM): implement via DLL imports from msi.dll below - stubbed
;      for now because it needs testing against a real installed PA.
;   b. Fallback: default path checked in InitializeSetup.
;   c. Last resort: the user browses to the folder (DisableDirPage=no).
;
; Build (Windows): iscc installer\PaDekereke.iss
;   after: dotnet build src\PaDekereke -c Release
; -----------------------------------------------------------------------------

#define AppName "Dekereke Data Sources for Phonology Assistant"
#define AppVersion "0.1.0"
#define AddOnBuildDir "..\src\PaDekereke\bin\Release\net48"

[Setup]
AppId={{B7A2F1C4-8D3E-4A57-9B12-DE0C6A55F2A1}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Seth Johnston
DefaultDirName={code:GetPaDir}\AddOns
DisableDirPage=no
DirExistsWarning=no
AppendDefaultDirName=no
PrivilegesRequired=admin
OutputBaseFilename=PaDekereke-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
UninstallFilesDir={app}
ArchitecturesInstallIn64BitMode=

[Files]
Source: "{#AddOnBuildDir}\PaDekereke.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AddOnBuildDir}\DekerekeToPa.dll"; DestDir: "{app}"; Flags: ignoreversion
; netstandard2.0 facade shims, if the build output contains any:
Source: "{#AddOnBuildDir}\netstandard.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Messages]
SelectDirDesc=Setup could not locate Phonology Assistant automatically.%nPlease select Phonology Assistant's AddOns folder (inside the folder containing Pa.exe).

[Code]
const
  DefaultPaDir = 'C:\Program Files (x86)\SIL\Phonology Assistant';

var
  FoundPaDir: string;

// TODO(cloud/VM): replace this path probe with proper MSI lookup:
//   function MsiEnumRelatedProducts(lpUpgradeCode: string; dwReserved, iProductIndex: DWORD;
//     lpProductBuf: string): UINT; external 'MsiEnumRelatedProductsW@msi.dll stdcall';
//   function MsiGetProductInfo(szProduct, szAttribute: string; lpValueBuf: string;
//     var pcchValueBuf: DWORD): UINT; external 'MsiGetProductInfoW@msi.dll stdcall';
// UpgradeCode: {5E57E4D4-580A-4cc1-9E0C-7EF8D3F81BBD}
// Attribute:   'InstallLocation'
function DetectPaDir: string;
begin
  Result := '';
  if FileExists(DefaultPaDir + '\Pa.exe') then
    Result := DefaultPaDir;
end;

function GetPaDir(Param: string): string;
begin
  if FoundPaDir <> '' then
    Result := FoundPaDir
  else
    Result := DefaultPaDir;
end;

function InitializeSetup: Boolean;
begin
  FoundPaDir := DetectPaDir;
  Result := True;
  if FoundPaDir = '' then
    MsgBox('Phonology Assistant was not found in its default location.'#13#10 +
           'You can still continue and select its AddOns folder manually,'#13#10 +
           'or cancel, install Phonology Assistant first, and run this setup again.',
           mbInformation, MB_OK);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  // Refuse to install into a folder that is not next to Pa.exe.
  if CurPageID = wpSelectDir then
  begin
    if not FileExists(ExtractFilePath(RemoveBackslash(WizardDirValue)) + '\Pa.exe') and
       not FileExists(AddBackslash(RemoveBackslash(WizardDirValue)) + '..\Pa.exe') then
    begin
      MsgBox('That folder does not appear to be Phonology Assistant''s AddOns folder ' +
             '(Pa.exe was not found beside it). Please choose the AddOns folder inside ' +
             'the Phonology Assistant program folder.', mbError, MB_OK);
      Result := False;
    end;
  end;
end;
