#!/usr/bin/env bash
# Ludex: сборка Windows-дистрибутива из Linux/macOS.
# Требуется: .NET SDK 8, makensis (apt install nsis).
set -euo pipefail
cd "$(dirname "$0")"

echo "==> 1/3 publish (win-x64, self-contained, single file)"
dotnet publish src/Ludex -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:PublishReadyToRun=false \
  -p:DebugType=none \
  -o release/app

echo "==> 2/3 build installer (NSIS)"
makensis installer/Ludex.nsi

echo "==> 3/3 done"
ls -la release/
