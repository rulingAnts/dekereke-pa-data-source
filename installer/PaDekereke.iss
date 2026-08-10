; -----------------------------------------------------------------------------
; Inno Setup script for the PaDekereke add-on.
;
; What it must do:
;   1. Find the Phonology Assistant installation (PA installs per-machine,
;      ALLUSERS=1, so this installer requests elevation).
;   2. Copy PaDekereke.dll + DekerekeToPa.dll (and any netstandard support DLLs
;      from the build output) into <PA>\AddOns\.
;   3. Keep everything of its OWN out of PA's tree: the uninstaller lives in
;      {pf32}\PaDekereke, not in AddOns. Only the two DLLs go into PA's
;      folder, and the chosen AddOns path is remembered in the registry so
;      uninstall can remove them (and AddOns itself, if then empty) even
;      though they live outside {app}.
;   4. Uninstall = remove the DLLs, drop AddOns if empty, remove {app}.
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
;   c. Last resort: a dedicated wizard page (shown only when detection
;      fails) asks for the folder containing Pa.exe.
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
; {app} is the add-on's own folder and holds only the uninstaller. PA's
; AddOns folder is a deployment TARGET, resolved in [Code] - putting the
; uninstall data inside another product's tree would orphan our uninstall
; entry if PA were ever removed or reinstalled first.
DefaultDirName={autopf}\PaDekereke
; An earlier build used <PA>\AddOns as {app}; never inherit that from a
; previous install's registry entry.
UsePreviousAppDir=no
DisableDirPage=yes
PrivilegesRequired=admin
OutputBaseFilename=PaDekereke-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=

[Files]
; Installed-file paths are recorded absolutely in the uninstall log, so these
; are removed on uninstall even though they are outside {app}.
Source: "{#AddOnBuildDir}\PaDekereke.dll"; DestDir: "{code:GetAddOnsDir}"; Flags: ignoreversion
Source: "{#AddOnBuildDir}\DekerekeToPa.dll"; DestDir: "{code:GetAddOnsDir}"; Flags: ignoreversion
; netstandard2.0 facade shims, if the build output contains any:
Source: "{#AddOnBuildDir}\netstandard.dll"; DestDir: "{code:GetAddOnsDir}"; Flags: ignoreversion skipifsourcedoesntexist

[Registry]
; Where the DLLs went, for uninstall-time cleanup ({code:...} state is gone by
; then). uninsdeletekey removes the whole key with the add-on.
Root: HKLM; Subkey: "Software\PaDekereke"; ValueType: string; ValueName: "AddOnsDir"; ValueData: "{code:GetAddOnsDir}"; Flags: uninsdeletekey

[UninstallDelete]
; Drop the AddOns folder if removing our DLLs left it empty; harmless no-op
; otherwise. Falls back to {app} (removed anyway) if the registry value is gone.
Type: dirifempty; Name: "{reg:HKLM\Software\PaDekereke,AddOnsDir|{app}}"

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

var
  PaDirPage: TInputDirWizardPage;

// The PA program folder: detected, or whatever the user picked on the page.
function GetPaDir: string;
begin
  if FoundPaDir <> '' then
    Result := FoundPaDir
  else
    Result := RemoveBackslashUnlessRoot(Trim(PaDirPage.Values[0]));
end;

// Where the DLLs go. Used by [Files] and remembered in [Registry]; the DLLs
// deliberately do NOT go under {app} - see the note in [Setup].
function GetAddOnsDir(Param: string): string;
begin
  Result := AddBackslash(GetPaDir) + 'AddOns';
end;

function InitializeSetup: Boolean;
begin
  FoundPaDir := DetectPaDir;
  Result := True;
  if FoundPaDir = '' then
    MsgBox('Phonology Assistant was not found on this computer.'#13#10#13#10 +
           'You can still continue and point Setup at its program folder ' +
           '(the folder containing Pa.exe), or cancel, install Phonology ' +
           'Assistant first, and run this setup again.',
           mbInformation, MB_OK);
end;

procedure InitializeWizard;
begin
  PaDirPage := CreateInputDirPage(wpWelcome,
    'Locate Phonology Assistant',
    'Setup could not find Phonology Assistant automatically.',
    'Select the folder that contains Pa.exe, then click Next.',
    False, '');
  PaDirPage.Add('&Phonology Assistant program folder:');
  PaDirPage.Values[0] := DefaultPaDir;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  // The locate page exists only for the detection-failed case.
  Result := (PageID = PaDirPage.ID) and (FoundPaDir <> '');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = PaDirPage.ID then
  begin
    if not FileExists(AddBackslash(Trim(PaDirPage.Values[0])) + 'Pa.exe') then
    begin
      MsgBox('Pa.exe was not found in that folder. Please select the ' +
             'Phonology Assistant program folder itself - normally'#13#10 +
             DefaultPaDir, mbError, MB_OK);
      Result := False;
    end;
  end;
end;
