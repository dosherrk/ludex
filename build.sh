#!/usr/bin/env bash
# Ludex: сборка дистрибутивов под Windows, Linux и macOS из одной машины (Linux).
# Требуется: .NET SDK 8, makensis (apt install nsis), imagemagick, dpkg-deb, zip.
#
#   ./build.sh            собрать всё
#   ./build.sh linux      только Linux (tar.gz + .deb)
#   ./build.sh mac        только macOS (osx-arm64 и osx-x64)
#   ./build.sh windows    только Windows (установщик + portable + zip)
set -euo pipefail
cd "$(dirname "$0")"

VER=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' src/Ludex/Ludex.csproj | head -1)
[ -n "$VER" ] || { echo "не нашёл версию в csproj"; exit 1; }
WHAT="${1:-all}"

PUB="dotnet publish src/Ludex -c Release --self-contained true
  -p:PublishSingleFile=true
  -p:IncludeNativeLibrariesForSelfExtract=true
  -p:EnableCompressionInSingleFile=true
  -p:PublishReadyToRun=false
  -p:DebugType=none"

make_icon() { # $1 = png, $2 = выходной каталог
  local src="$1" out="$2"
  mkdir -p "$out"
  for s in 16 32 48 64 128 256 512; do
    convert "$src" -resize "${s}x${s}" "$out/ludex-${s}.png"
  done
}

# ------------------------------------------------------------------ Windows
build_windows() {
  echo "==> Windows $VER"
  rm -rf release/app
  $PUB -r win-x64 -o release/app
  ( cd installer && makensis -DAPPVERSION="$VER" Ludex.nsi )
  cp release/app/Ludex.exe "release/Ludex-${VER}-portable.exe"
  ( cd release && zip -q "Ludex-${VER}-windows.zip" "Ludex-${VER}-portable.exe" )
  ls -lh release/Ludex-Setup-${VER}.exe "release/Ludex-${VER}-portable.exe" "release/Ludex-${VER}-windows.zip"
}

# -------------------------------------------------------------------- Linux
build_linux() {
  echo "==> Linux $VER"
  local rid=linux-x64 stage=build/linux
  rm -rf release/$rid "$stage"
  $PUB -r $rid -o release/$rid
  mkdir -p "$stage"

  # portable-архив
  cp release/$rid/Ludex "$stage/Ludex"
  chmod +x "$stage/Ludex"
  make_icon assets/logo-ludex.png build/icons
  cp build/icons/ludex-256.png "$stage/ludex.png"
  cat > "$stage/ludex.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=Ludex
Comment=Local game library with automatic artwork
Comment[ru]=Локальная библиотека игр с автоматическими обложками
Exec=ludex
Icon=ludex
Terminal=false
Categories=Game;Utility;
StartupWMClass=Ludex
DESKTOP
  cat > "$stage/КАК-ЗАПУСТИТЬ.txt" <<'TXT'
Ludex для Linux
================

1. Распакуйте архив в любую папку, например ~/Applications/Ludex:

       mkdir -p ~/Applications/Ludex
       tar -xzf Ludex-*.tar.gz -C ~/Applications/Ludex

2. Запустите:

       ~/Applications/Ludex/Ludex

3. Чтобы приложение появилось в меню приложений, скопируйте ярлык и иконку:

       cp ludex.desktop ~/.local/share/applications/
       cp ludex.png ~/.local/share/icons/hicolor/256x256/apps/   # при необходимости создайте папку

   и поправьте в ludex.desktop строку Exec= на полный путь к Ludex,
   например Exec=/home/пользователь/Applications/Ludex/Ludex

Требования: 64-битный Linux с X11 или Wayland (XWayland), glibc 2.31+.
Библиотеки для интерфейса: libX11, libXrandr, libXi, libXcursor, libXext,
libXrender, libICE, libSM, libGL, libfontconfig, libfreetype, libxkbcommon.

Данные (настройки, библиотека, обложки) хранятся в ~/.local/share/Ludex.
Установщик .deb делает то же самое автоматически: sudo apt install ./Ludex-*.deb
TXT
  tar -czf "release/Ludex-${VER}-linux-x64.tar.gz" -C "$stage" .

  # .deb
  local deb=build/deb
  rm -rf "$deb"
  mkdir -p "$deb/DEBIAN" "$deb/opt/ludex" "$deb/usr/bin" \
           "$deb/usr/share/applications" "$deb/usr/share/icons/hicolor/256x256/apps"
  cp release/$rid/Ludex "$deb/opt/ludex/Ludex"
  chmod 755 "$deb/opt/ludex/Ludex"
  ln -s /opt/ludex/Ludex "$deb/usr/bin/ludex"
  ln -s /opt/ludex/Ludex "$deb/usr/bin/Ludex"
  cp "$stage/ludex.desktop" "$deb/usr/share/applications/ludex.desktop"
  cp build/icons/ludex-256.png "$deb/usr/share/icons/hicolor/256x256/apps/ludex.png"
  cat > "$deb/DEBIAN/control" <<CONTROL
Package: ludex
Version: ${VER}
Section: games
Priority: optional
Architecture: amd64
Maintainer: strxkilla <dosherrk@users.noreply.github.com>
Installed-Size: $(du -k "$deb/opt/ludex/Ludex" | cut -f1)
Depends: libx11-6, libice6, libsm6, libfontconfig1, libxrandr2, libxi6, libxcursor1, libxext6, libxrender1, libgl1, libglib2.0-0
Description: Ludex - local game library with automatic artwork
 Сканирует указанные папки, находит игры (AppImage, нативные бинарники),
 определяет названия и подтягивает обложки из Steam. Запуск в один клик,
 свои темы и обои.
CONTROL
  dpkg-deb --root-owner-group --build "$deb" "release/Ludex-${VER}-linux-x64.deb" >/dev/null
  ls -lh "release/Ludex-${VER}-linux-x64.tar.gz" "release/Ludex-${VER}-linux-x64.deb"
}

# -------------------------------------------------------------------- macOS
build_mac() {
  echo "==> macOS $VER"
  make_icon assets/logo-ludex.png build/icons
  png2icns build/AppIcon.icns $(for s in 16 32 64 128 256 512; do echo build/icons/ludex-$s.png; done) 2>/dev/null \
    || echo "png2icns не сработал — иконка в бандле будет отсутствовать"

  for rid in osx-arm64 osx-x64; do
    local arch="${rid#osx-}"
    local app="build/Ludex-${arch}/Ludex.app"
    rm -rf "build/Ludex-${arch}" release/$rid
    $PUB -r $rid -o release/$rid

    mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
    cp -a release/$rid/. "$app/Contents/MacOS/"
    chmod 755 "$app/Contents/MacOS/"*
    [ -f build/AppIcon.icns ] && cp build/AppIcon.icns "$app/Contents/Resources/AppIcon.icns"

    cat > "$app/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key><string>Ludex</string>
    <key>CFBundleDisplayName</key><string>Ludex</string>
    <key>CFBundleIdentifier</key><string>app.ludex.desktop</string>
    <key>CFBundleVersion</key><string>${VER}</string>
    <key>CFBundleShortVersionString</key><string>${VER}</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleExecutable</key><string>Ludex</string>
    <key>CFBundleIconFile</key><string>AppIcon</string>
    <key>LSMinimumSystemVersion</key><string>11.0</string>
    <key>NSHighResolutionCapable</key><true/>
    <key>LSApplicationCategoryType</key><string>public.app-category.utilities</string>
</dict>
</plist>
PLIST

    cat > "build/Ludex-${arch}/КАК-ЗАПУСТИТЬ.txt" <<'TXT'
Ludex для macOS
================

1. Распакуйте архив — получится Ludex.app. Перетащите его в «Программы» (Applications).

2. Первый запуск: приложение не подписано сертификатом Apple, поэтому Gatekeeper
   скажет «Не удаётся открыть, так как разработчик не может быть проверен».
   Варианты:
     • правый клик по Ludex.app → «Открыть» → «Открыть» (один раз, дальше обычный запуск);
     • либо в Терминале:
           xattr -dr com.apple.quarantine /Applications/Ludex.app

Требования: macOS 11 Big Sur или новее.
  Ludex-1.5.0-macos-arm64.zip — для Apple Silicon (M1/M2/M3/M4);
  Ludex-1.5.0-macos-x64.zip   — для Mac на Intel.

Данные (настройки, библиотека, обложки) хранятся в ~/Library/Application Support/Ludex.

Что работает: сканирование папок, поиск игр (.app-бандлы и исполняемые файлы),
обложки из Steam, темы, обои, запуск игр. Автозапуск и «запуск от администратора» —
функции Windows, в macOS их нет.
TXT
    ( cd "build/Ludex-${arch}" && zip -qry "../../release/Ludex-${VER}-macos-${arch}.zip" Ludex.app "КАК-ЗАПУСТИТЬ.txt" )
    echo "    $rid: $(file -b release/$rid/Ludex | cut -c1-40) → $(du -h "release/Ludex-${VER}-macos-${arch}.zip" | cut -f1)"
  done
}

case "$WHAT" in
  windows) build_windows ;;
  linux)   build_linux ;;
  mac)     build_mac ;;
  all)     build_windows; build_linux; build_mac ;;
  *) echo "использование: $0 [all|windows|linux|mac]"; exit 1 ;;
esac

echo "==> готово, release/:"
ls -lh release/*.exe release/*.zip release/*.tar.gz release/*.deb 2>/dev/null || true
