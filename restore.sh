#!/bin/bash
set -euo pipefail
if [ "$#" -ne 0 ]; then echo "用法：bash restore.sh" >&2; exit 1; fi
GAME_ROOT="${STEAM_SHOGUN_ROOT:-$HOME/Library/Application Support/Steam/steamapps/common/Shogun Showdown}"
TARGET_DLL="$GAME_ROOT/BepInEx/plugins/HidenDojo.dll"
BACKUP_ROOT="$GAME_ROOT/HidenDojo-backups"
if pgrep -x ShogunShowdown >/dev/null; then
  echo "请先退出 Shogun Showdown。" >&2; exit 1
else
  PROCESS_STATUS=$?
  if [ "$PROCESS_STATUS" -ne 1 ]; then echo "无法检查游戏进程，已停止。" >&2; exit 1; fi
fi
if [ ! -d "$GAME_ROOT/ShogunShowdown.app/Contents/Resources/Data/Managed" ]; then echo "找不到 macOS 游戏目录。请设置 STEAM_SHOGUN_ROOT。" >&2; exit 1; fi
if [ -L "$TARGET_DLL" ] || [ -L "$BACKUP_ROOT" ]; then echo "插件或备份路径是符号链接，已停止。" >&2; exit 1; fi
if [ ! -e "$TARGET_DLL" ]; then echo "没有已安装的 HidenDojo.dll；无需移除。"; exit 0; fi
if [ ! -f "$TARGET_DLL" ]; then echo "插件目标不是普通文件，已停止。" >&2; exit 1; fi
mkdir -p "$BACKUP_ROOT"
BACKUP_DIR="$(mktemp -d "$BACKUP_ROOT/$(date +%Y%m%d-%H%M%S)-XXXXXX")"
mv "$TARGET_DLL" "$BACKUP_DIR/HidenDojo.dll"
echo "已停用秘传道场，插件保留在：$BACKUP_DIR/HidenDojo.dll"
echo "保留了配置、其他 Mod 和 BepInEx；没有恢复或覆盖游戏程序集。"
