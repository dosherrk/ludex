@echo off
rem Ludex: сборка Windows-дистрибутива на Windows.
rem Требуется: .NET 8 SDK (https://dot.net) и NSIS 3 (makensis.exe в PATH).
setlocal
cd /d "%~dp0"

echo === 1/3 publish win-x64 ===
dotnet publish src\Ludex -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:PublishReadyToRun=false ^
  -p:DebugType=none ^
  -o release\app
if errorlevel 1 goto :err

echo === 2/3 build installer ===
makensis installer\Ludex.nsi
if errorlevel 1 goto :err

echo === 3/3 done ===
dir release
goto :eof

:err
echo BUILD FAILED
exit /b 1
