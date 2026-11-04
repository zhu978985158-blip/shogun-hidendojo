#!/bin/bash
set -euo pipefail
if [ "$#" -gt 1 ]; then echo "用法：bash install.sh [HidenDojo.dll路径]" >&2; exit 1; fi
SCRIPT_ROOT="$(cd "$(dirname "$0")" && pwd)"
GAME_ROOT="${STEAM_SHOGUN_ROOT:-$HOME/Library/Application Support/Steam/steamapps/common/Shogun Showdown}"
SOURCE_DLL="${1:-$SCRIPT_ROOT/bin/Release/net472/HidenDojo.dll}"
PLUGIN_DIR="$GAME_ROOT/BepInEx/plugins"
TARGET_DLL="$PLUGIN_DIR/HidenDojo.dll"
BACKUP_ROOT="$GAME_ROOT/HidenDojo-backups"
if pgrep -x ShogunShowdown >/dev/null; then
  echo "请先退出 Shogun Showdown。" >&2; exit 1
else
  PROCESS_STATUS=$?
  if [ "$PROCESS_STATUS" -ne 1 ]; then echo "无法检查游戏进程，已停止。" >&2; exit 1; fi
fi
if [ ! -d "$GAME_ROOT/ShogunShowdown.app/Contents/Resources/Data/Managed" ]; then echo "找不到 macOS 游戏目录。请设置 STEAM_SHOGUN_ROOT。" >&2; exit 1; fi
if [ ! -f "$GAME_ROOT/BepInEx/core/BepInEx.dll" ] || [ ! -f "$GAME_ROOT/BepInEx/core/0Harmony.dll" ]; then echo "请先自行安装并验证 BepInEx 5 Mono 加载器。" >&2; exit 1; fi
if [ ! -f "$SOURCE_DLL" ] || [ ! -s "$SOURCE_DLL" ]; then echo "找不到非空的插件文件：$SOURCE_DLL" >&2; exit 1; fi
if [ -L "$TARGET_DLL" ] || [ -L "$BACKUP_ROOT" ]; then echo "插件或备份路径是符号链接，已停止。" >&2; exit 1; fi
if [ -e "$TARGET_DLL" ] && [ ! -f "$TARGET_DLL" ]; then echo "插件目标不是普通文件，已停止。" >&2; exit 1; fi
if [ -f "$TARGET_DLL" ]; then
  mkdir -p "$BACKUP_ROOT"
  BACKUP_DIR="$(mktemp -d "$BACKUP_ROOT/$(date +%Y%m%d-%H%M%S)-XXXXXX")"
  cp "$TARGET_DLL" "$BACKUP_DIR/HidenDojo.dll"
  echo "上一版本已备份：$BACKUP_DIR/HidenDojo.dll"
fi
mkdir -p "$PLUGIN_DIR"
STAGED_DLL="$(mktemp "$PLUGIN_DIR/.HidenDojo.XXXXXX")"
trap 'rm -f "$STAGED_DLL"' EXIT
cp "$SOURCE_DLL" "$STAGED_DLL"
mv -f "$STAGED_DLL" "$TARGET_DLL"
echo "已安装秘传道场：$TARGET_DLL"
echo "保留了现有配置、其他 Mod 和 BepInEx 加载器。"
