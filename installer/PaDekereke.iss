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
;      MsiGetProductInfo('InstallLocation') yields the folder. Implemented in
;      [Code] below. UNVERIFIED: written by inspection against the msi.dll API
;      docs; it has not yet been compiled by iscc or run against a real
;      installed PA - verify on Windows before shipping. Note also that
;      InstallLocation is only populated if PA's MSI set ARPINSTALLLOCATION,
;      which could not be checked offline - hence the fallbacks stay.
;   b. Fallback: probe {pf32}\SIL\Phonology Assistant (and the literal default
;      path) for Pa.exe.
;   c. Last resort: the user browses to the folder (DisableDirPage=no).
;
; Build (Windows): iscc installer\PaDekereke.iss
;   after: dotnet build src\PaDekereke -c Release
; -----------------------------------------------------------------------------

#define AppName "Dekereke Data Sources for Phonology Assistant"
; Overridable from CI: iscc /DAppVersion=1.2.3 installer\PaDekereke.iss
#ifndef AppVersion
  #define AppVersion "0.1.0-dev"
#endif
#define AppPublisherName "Seth Johnston"
#define AppUrl "https://github.com/rulingAnts/dekereke-pa-data-source"
#define AddOnBuildDir "..\src\PaDekereke\bin\Release\net48"

[Setup]
AppId={{B7A2F1C4-8D3E-4A57-9B12-DE0C6A55F2A1}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisherName}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
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

  // Fixed across all PA versions - Installer/Product.wxs:9 in the
  // sillsdev/phonology-assistant repo.
  PaUpgradeCode = '{5E57E4D4-580A-4cc1-9E0C-7EF8D3F81BBD}';

  ERROR_SUCCESS = 0;

var
  FoundPaDir: string;

// UNVERIFIED (written by inspection; needs a Windows machine with PA
// installed): enumerate MSI products sharing PA's UpgradeCode and read each
// one's InstallLocation. Unicode Inno Setup passes String parameters to
// external functions as null-terminated wide-char pointers; out-buffers are
// Strings pre-sized with SetLength, per the Inno Setup "external" docs.
//
// MsiEnumRelatedProducts: returns ERROR_SUCCESS per product,
//   ERROR_NO_MORE_ITEMS (259) when done; lpProductBuf receives a 38-char
//   product code GUID and must hold 39 chars including the terminator.
// MsiGetProductInfoW: pcchValueBuf is in/out - in: buffer size in chars,
//   out: chars copied excluding the terminator.
function MsiEnumRelatedProducts(lpUpgradeCode: string; dwReserved: Cardinal;
  iProductIndex: Cardinal; lpProductBuf: string): Cardinal;
  external 'MsiEnumRelatedProductsW@msi.dll stdcall';

function MsiGetProductInfo(szProduct: string; szAttribute: string;
  lpValueBuf: string; var pcchValueBuf: Cardinal): Cardinal;
  external 'MsiGetProductInfoW@msi.dll stdcall';

// The install folder of the MSI-registered PA, or '' when PA is not
// installed (or its MSI never set ARPINSTALLLOCATION, so InstallLocation is
// empty - the path-probe fallback below still applies).
function MsiLocatePaDir: string;
var
  Index: Cardinal;
  ProductCode, Location: string;
  Len: Cardinal;
  Dir: string;
begin
  Result := '';
  Index := 0;
  repeat
    SetLength(ProductCode, 39);
    if MsiEnumRelatedProducts(PaUpgradeCode, 0, Index, ProductCode) <> ERROR_SUCCESS then
      Break;
    // Trim at the terminator the API wrote into the buffer.
    if Pos(#0, ProductCode) > 0 then
      SetLength(ProductCode, Pos(#0, ProductCode) - 1);

    Len := 512;
    SetLength(Location, Len);
    if MsiGetProductInfo(ProductCode, 'InstallLocation', Location, Len) = ERROR_SUCCESS then
    begin
      SetLength(Location, Len);
      Dir := RemoveBackslashUnlessRoot(Trim(Location));
      if (Dir <> '') and FileExists(Dir + '\Pa.exe') then
      begin
        Result := Dir;
        Exit;
      end;
    end;

    Index := Index + 1;
  until Index > 32; // paranoia cap; UpgradeCode families are tiny
end;

function DetectPaDir: string;
var
  Dir: string;
begin
  Result := MsiLocatePaDir;
  if Result <> '' then
    Exit;

  // Path-probe fallback: {pf32} is Program Files (x86) on 64-bit Windows and
  // Program Files on 32-bit (PA is an x86 app).
  Dir := ExpandConstant('{pf32}') + '\SIL\Phonology Assistant';
  if FileExists(Dir + '\Pa.exe') then
  begin
    Result := Dir;
    Exit;
  end;

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
