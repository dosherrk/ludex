#!/usr/bin/env bash
# Сборка macOS .dmg на Linux (без macOS и без подписи).
#
#   ./tools/make-dmg.sh <папка-с-Ludex.app> <выходной.dmg> "<имя тома>" [размер_образа_MB]
#
# Нужны три утилиты (собираются из исходников, в репозиторий не входят):
#   NEWFS_HFS — mkfs.hfsplus из hfsprogs (diskdev_cmds)
#   HFSPLUS   — утилита из libdmg-hfsplus (команды addall/chmod/symlink/ls)
#   DMG       — утилита dmg из libdmg-hfsplus (команда dmg: HFS+ образ -> UDIF/UDZO)
set -euo pipefail

SRC=${1:?папка с Ludex.app}
OUT=${2:?выходной .dmg}
VOL=${3:-Ludex}
SIZE_MB=${4:-96}

NEWFS_HFS=${NEWFS_HFS:-newfs_hfs}
HFSPLUS=${HFSPLUS:-hfsplus}
DMG=${DMG:-dmg}

WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT

# 1. образ HFS+ нужного размера
dd if=/dev/zero of="$WORK/disk.img" bs=1M count="$SIZE_MB" status=none
"$NEWFS_HFS" -v "$VOL" "$WORK/disk.img" >/dev/null

# 2. файлы: симлинк на /Applications кладём отдельно (addall его не переваривает)
mkdir -p "$WORK/stage"
cp -a "$SRC"/. "$WORK/stage"/
"$HFSPLUS" "$WORK/disk.img" addall "$WORK/stage" / >/dev/null
"$HFSPLUS" "$WORK/disk.img" symlink /Applications /Applications

# 3. права: addall ставит файлам 0644 — исполняемым возвращаем 0755
while IFS= read -r f; do
  "$HFSPLUS" "$WORK/disk.img" chmod 755 "$f"
done < <(cd "$WORK/stage" && find . -type f -perm -u+x \
           | sed 's|^\.||' | sed 's| .*||' | sed 's|/|/|g')

# 4. HFS+ -> UDIF (zlib), как это делает hdiutil
"$DMG" dmg "$WORK/disk.img" "$OUT"
echo "готово: $OUT ($(du -h "$OUT" | cut -f1))"
