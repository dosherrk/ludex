#!/usr/bin/env bash
# Ludex: сборка дистрибутивов под Windows и Linux из одной машины (Linux).
# Требуется: .NET SDK 8, makensis (apt install nsis), imagemagick, dpkg-deb, zip.
#
#   ./build.sh            собрать всё
#   ./build.sh linux      только Linux (tar.gz + .deb)
#   ./build.sh windows    только Windows (установщик + portable + zip)
set -euo pipefail
cd "$(dirname "$0")"

VER=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' src/Ludex/Ludex.csproj | head -1)
[ -n "$VER" ] || { echo "не нашёл версию в csproj"; exit 1; }
WHAT="${1:-all}"

# Публикация набором файлов, а не одним exe: single-file с сжатием распаковывает
# ~150 МБ в temp при каждом запуске, из-за чего старт занимает лишние секунды.
PUB="dotnet publish src/Ludex -c Release --self-contained true -m:1 --disable-build-servers
  -p:PublishSingleFile=false
  -p:IncludeNativeLibrariesForSelfExtract=false
  -p:EnableCompressionInSingleFile=false
  -p:PublishReadyToRun=false
  -p:DebugType=none
  -p:SatelliteResourceLanguages=en"

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

  # портативная версия — папка с файлами в архиве (файлы должны лежать рядом с Ludex.exe)
  local stage=build/windows
  rm -rf "$stage"
  mkdir -p "$stage/Ludex-${VER}"
  cp -a release/app/. "$stage/Ludex-${VER}/"
  cat > "$stage/Ludex-${VER}/КАК-ЗАПУСТИТЬ.txt" <<'TXT'
Ludex для Windows — портативная версия
======================================

1. Распакуйте архив целиком в любую папку (например, D:\Ludex).
2. Запускайте Ludex.exe.

Важно: Ludex.exe не работает в одиночку — рядом должны оставаться все файлы
из архива (Ludex.dll, папка runtimes и остальные). Поэтому не перетаскивайте
один exe на рабочий стол: сделайте ярлык на Ludex.exe.

Данные (настройки, библиотека, обложки) хранятся в %LOCALAPPDATA%\Ludex.

Если хочется обычной установки с ярлыками и деинсталлятором — скачайте
Ludex-Setup-*.exe.
TXT
  ( cd "$stage" && zip -qry -UN=UTF8 "../../release/Ludex-${VER}-windows.zip" "Ludex-${VER}" )
  ls -lh release/Ludex-Setup-${VER}.exe "release/Ludex-${VER}-windows.zip"
}

# -------------------------------------------------------------------- Linux
build_linux() {
  echo "==> Linux $VER"
  local rid=linux-x64 stage=build/linux
  rm -rf release/$rid "$stage"
  $PUB -r $rid -o release/$rid
  mkdir -p "$stage"

  # portable-архив: вся папка публикации, а не один бинарник
  mkdir -p "$stage/Ludex"
  cp -a release/$rid/. "$stage/Ludex/"
  chmod +x "$stage/Ludex/Ludex"
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

1. Распакуйте архив в любую папку, например ~/Applications:

       mkdir -p ~/Applications
       tar -xzf Ludex-*.tar.gz -C ~/Applications

   Появится папка Ludex — все файлы должны оставаться вместе.

2. Запустите:

       ~/Applications/Ludex/Ludex/Ludex

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
  cp -a release/$rid/. "$deb/opt/ludex/"
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
Installed-Size: $(du -sk "$deb/opt/ludex" | cut -f1)
Depends: libx11-6, libice6, libsm6, libfontconfig1, libxrandr2, libxi6, libxcursor1, libxext6, libxrender1, libgl1, libglib2.0-0
Description: Ludex - local game library with automatic artwork
 Сканирует указанные папки, находит игры (AppImage, нативные бинарники),
 определяет названия и подтягивает обложки из Steam. Запуск в один клик,
 свои темы и обои.
CONTROL
  dpkg-deb --root-owner-group --build "$deb" "release/Ludex-${VER}-linux-x64.deb" >/dev/null
  ls -lh "release/Ludex-${VER}-linux-x64.tar.gz" "release/Ludex-${VER}-linux-x64.deb"
}

case "$WHAT" in
  windows) build_windows ;;
  linux)   build_linux ;;
  all)     build_windows; build_linux ;;
  *) echo "использование: $0 [all|windows|linux]"; exit 1 ;;
esac

echo "==> готово, release/:"
ls -lh release/*.exe release/*.zip release/*.tar.gz release/*.deb 2>/dev/null || true
