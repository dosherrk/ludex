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
  !define APPVERSION  "1.21.7"
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

!define MUI_FINISHPAGE_RUN
!define MUI_FINISHPAGE_RUN_TEXT "$(FinishRun)"
!define MUI_FINISHPAGE_RUN_FUNCTION LaunchApp
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
LangString LeftFiles      ${LANG_RUSSIAN} "Часть файлов удалить не удалось: $INSTDIR$\r$\n$\r$\nСкорее всего, Ludex ещё запущен. Закройте его и удалите папку вручную."
LangString LeftFiles      ${LANG_ENGLISH} "Some files could not be removed: $INSTDIR$\r$\n$\r$\nLudex is probably still running. Close it and delete the folder manually."
LangString AskCloseRunning ${LANG_RUSSIAN} "Ludex уже запущен.$\r$\n$\r$\nЗакрыть его, чтобы установить новую версию?"
LangString AskCloseRunning ${LANG_ENGLISH} "Ludex is already running.$\r$\n$\r$\nClose it to install the new version?"
LangString InstallCancelled ${LANG_RUSSIAN} "Установка отменена: сначала закройте Ludex."
LangString InstallCancelled ${LANG_ENGLISH} "Installation cancelled: close Ludex first."
LangString AskWipeData    ${LANG_ENGLISH} "Also delete Ludex settings and artwork cache?$\r$\n$\r$\nFolder: $LOCALAPPDATA\${APPNAME}$\r$\n(The game files on your disks are not touched)"

; ------------------------------------------------------------------- helpers
; Запуск Ludex с галочки «Запустить Ludex».
; Обычный Exec из установщика отдаёт приложению фокус установщика: Ludex стартует
; позади окна установщика, и кажется, что он не запустился. Поэтому запускаем так же,
; как это делает ярлык — через оболочку Windows (ShellExecute).
Function LaunchApp
  ${If} ${FileExists} "$INSTDIR\${APPEXE}"
    ; SYNCHRONIZE = 0x100000. Открываем мьютекс, который держит приложение.
    System::Call 'kernel32::OpenMutex(i 0x100000, i 0, t "Global\Ludex.SingleInstance") p .r1 ?e'
    Pop $2 ; код ошибки GetLastError
    StrCmp $1 "0" 0 ludex_running
    ; ERROR_ACCESS_DENIED (5): мьютекс существует, но открыт с другими правами —
    ; так бывает, когда Ludex запущен от администратора. Это тоже «запущен».
    StrCmp $2 "5" ludex_running
    Goto ludex_not_running
    ludex_running:
      StrCmp $1 "0" +2
        System::Call 'kernel32::CloseHandle(p r1)'
      MessageBox MB_YESNO|MB_ICONQUESTION "$(AskCloseRunning)" IDYES killcopies
      Abort "$(InstallCancelled)"
      killcopies:
        nsExec::ExecToLog 'taskkill /IM ${APPEXE} /F'
        Sleep 700
  ${EndIf}
  ludex_not_running:

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
  ; публикация идёт набором файлов (быстрее старт) — ставим всё содержимое папки
  File /r "../release/app\*.*"
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

  ; приложение ставится набором файлов: exe рядом с ним лежат Ludex.dll, папка runtimes,
  ; файлы локализации и нативные библиотеки. Удаляем папку целиком, иначе остаются dll.
  ; Перед этим закрываем запущенный Ludex — иначе Windows не даёт удалить занятые файлы.
  nsExec::ExecToLog 'taskkill /IM ${APPEXE} /F'
  Pop $0
  Sleep 700

  ; данные обновления (если есть) — отдельная папка рядом
  RMDir /r "$INSTDIR\runtimes"
  Delete "$INSTDIR\*.dll"
  Delete "$INSTDIR\*.so"
  Delete "$INSTDIR\*.json"
  Delete "$INSTDIR\*.pdb"

  Delete "$INSTDIR\${APPEXE}"
  Delete "$INSTDIR\uninstall.exe"
  RMDir /r "$INSTDIR"

  ; если что-то осталось (файл занят), пробуем ещё раз после паузы и сообщаем путь
  IfFileExists "$INSTDIR\*.*" 0 done
    Sleep 1500
    RMDir /r "$INSTDIR"
  done:
  IfFileExists "$INSTDIR\*.*" 0 cleaned
    IfSilent +2
    MessageBox MB_OK|MB_ICONEXCLAMATION "$(LeftFiles)"
  cleaned:

  ; library database + cached covers (optional)
  IfSilent wipe
    MessageBox MB_YESNO|MB_ICONQUESTION "$(AskWipeData)" /SD IDNO IDNO keep
  wipe:
    RMDir /r "$LOCALAPPDATA\${APPNAME}"
  keep:
SectionEnd
