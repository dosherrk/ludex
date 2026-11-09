; ============================================================================
;  Ludex - installer script (NSIS 3)
;  Build:  makensis installer/Ludex.nsi      (run from the repo root)
;  Result: release/Ludex-Setup-<версия>.exe  (with built-in uninstaller)
; ============================================================================

Unicode true

!include "MUI2.nsh"
!include "FileFunc.nsh"
!include "LogicLib.nsh"
!include "WinVer.nsh"

!define APPNAME       "Ludex"
!define APPEXE        "Ludex.exe"
!ifndef APPVERSION
  !define APPVERSION  "1.5.0"
!endif
!define PUBLISHER     "Ludex"
!define APPID         "Ludex"
!define APPURL        "https://steamgriddb.com"
!define REGKEY        "Software\${APPID}"
!define UNINSTKEY     "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPID}"

Name "${APPNAME}"
BrandingText "${APPNAME} ${APPVERSION}"
OutFile "../release/Ludex-Setup-${APPVERSION}.exe"

; per-user install: no administrator rights needed
InstallDir "$LOCALAPPDATA\Programs\${APPNAME}"
InstallDirRegKey HKCU "${REGKEY}" "InstallDir"
RequestExecutionLevel user

SetCompressor /SOLID lzma
SetCompressorDictSize 64

Icon "setup.ico"
UninstallIcon "setup.ico"

VIProductVersion "${APPVERSION}.0"
VIAddVersionKey "ProductName"     "${APPNAME}"
VIAddVersionKey "FileDescription" "${APPNAME} Setup"
VIAddVersionKey "FileVersion"     "${APPVERSION}.0"
VIAddVersionKey "ProductVersion"  "${APPVERSION}"
VIAddVersionKey "CompanyName"     "${PUBLISHER}"
VIAddVersionKey "LegalCopyright"  "${PUBLISHER}"

; ------------------------------------------------------------------ interface
!define MUI_ABORTWARNING
!define MUI_ICON   "setup.ico"
!define MUI_UNICON "setup.ico"
!define MUI_LANGDLL_ALLLANGUAGES

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "license_ru.txt"
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES

!define MUI_FINISHPAGE_RUN "$INSTDIR\${APPEXE}"
!define MUI_FINISHPAGE_RUN_TEXT "$(FinishRun)"
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "Russian"
!insertmacro MUI_LANGUAGE "English"

; custom strings
LangString FinishRun      ${LANG_RUSSIAN} "Запустить ${APPNAME}"
LangString FinishRun      ${LANG_ENGLISH} "Launch ${APPNAME}"
LangString SecMain        ${LANG_RUSSIAN} "${APPNAME} (обязательно)"
LangString SecMain        ${LANG_ENGLISH} "${APPNAME} (required)"
LangString SecDesktop     ${LANG_RUSSIAN} "Ярлык на рабочем столе"
LangString SecDesktop     ${LANG_ENGLISH} "Desktop shortcut"
LangString SecUninstall    ${LANG_RUSSIAN} "Удалить ${APPNAME}"
LangString SecUninstall    ${LANG_ENGLISH} "Uninstall ${APPNAME}"
LangString AskWipeData    ${LANG_RUSSIAN} "Удалить также настройки и кэш обложек Ludex?$\r$\n$\r$\nПапка: $LOCALAPPDATA\${APPNAME}$\r$\n(сами игры на диске не затрагиваются)"
LangString AskWipeData    ${LANG_ENGLISH} "Also delete Ludex settings and artwork cache?$\r$\n$\r$\nFolder: $LOCALAPPDATA\${APPNAME}$\r$\n(The game files on your disks are not touched)"

; ------------------------------------------------------------------- helpers
Function .onInit
  !insertmacro MUI_LANGDLL_DISPLAY
FunctionEnd
Function un.onInit
  !insertmacro MUI_LANGDLL_DISPLAY
FunctionEnd

; ------------------------------------------------------------------- sections
Section "$(SecMain)" SecMain
  SectionIn RO
  SetShellVarContext current

  SetOutPath "$INSTDIR"
  SetOverwrite on
  File "../release/app/Ludex.exe"
  WriteUninstaller "$INSTDIR\uninstall.exe"

  ; start menu
  CreateDirectory "$SMPROGRAMS\${APPNAME}"
  CreateShortCut  "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk" "$INSTDIR\${APPEXE}" "" "$INSTDIR\${APPEXE}" 0
  CreateShortCut  "$SMPROGRAMS\${APPNAME}\$(SecUninstall).lnk" "$INSTDIR\uninstall.exe"

  ; registry: install path + add/remove programs entry
  WriteRegStr HKCU "${REGKEY}" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "${REGKEY}" "Version" "${APPVERSION}"

  WriteRegStr   HKCU "${UNINSTKEY}" "DisplayName"     "${APPNAME}"
  WriteRegStr   HKCU "${UNINSTKEY}" "DisplayVersion"  "${APPVERSION}"
  WriteRegStr   HKCU "${UNINSTKEY}" "DisplayIcon"     "$INSTDIR\${APPEXE},0"
  WriteRegStr   HKCU "${UNINSTKEY}" "Publisher"       "${PUBLISHER}"
  WriteRegStr   HKCU "${UNINSTKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr   HKCU "${UNINSTKEY}" "UninstallString" '"$INSTDIR\uninstall.exe"'
  WriteRegStr   HKCU "${UNINSTKEY}" "QuietUninstallString" '"$INSTDIR\uninstall.exe" /S'
  WriteRegStr   HKCU "${UNINSTKEY}" "URLInfoAbout"    "${APPURL}"
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoRepair" 1

  ; переезд с прежнего имени (GameVault → Ludex): старые ярлыки и папку установки убираем,
  ; настройки и кэш обложек в $LOCALAPPDATA\GameVault не трогаем — приложение перенесёт их само
  Delete "$SMPROGRAMS\GameVault\*.lnk"
  RMDir  "$SMPROGRAMS\GameVault"
  Delete "$DESKTOP\GameVault.lnk"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\GameVault"
  DeleteRegKey HKCU "Software\GameVault"
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "GameVault"
  RMDir /r "$LOCALAPPDATA\Programs\GameVault"

  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKCU "${UNINSTKEY}" "EstimatedSize" "$0"
SectionEnd

Section "$(SecDesktop)" SecDesktop
  SetShellVarContext current
  CreateShortCut "$DESKTOP\${APPNAME}.lnk" "$INSTDIR\${APPEXE}" "" "$INSTDIR\${APPEXE}" 0
SectionEnd

; ------------------------------------------------------------------ uninstall
Section "Uninstall"
  SetShellVarContext current

  Delete "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk"
  Delete "$SMPROGRAMS\${APPNAME}\$(SecUninstall).lnk"
  RMDir  "$SMPROGRAMS\${APPNAME}"
  Delete "$DESKTOP\${APPNAME}.lnk"

  DeleteRegKey HKCU "${UNINSTKEY}"
  DeleteRegKey HKCU "${REGKEY}"

  Delete "$INSTDIR\Ludex.exe"
  Delete "$INSTDIR\uninstall.exe"
  RMDir "$INSTDIR"

  ; library database + cached covers (optional)
  IfSilent wipe
    MessageBox MB_YESNO|MB_ICONQUESTION "$(AskWipeData)" /SD IDNO IDNO keep
  wipe:
    RMDir /r "$LOCALAPPDATA\${APPNAME}"
  keep:
SectionEnd
