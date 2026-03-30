#!/usr/bin/env bash
# Cố gắng dừng tiến trình docklite-wsl đang chạy (cùng tên binary) rồi chạy lại run-server.sh.
# Có thể không tìm đúng PID trên mọi máy; nếu lỗi, dừng tay trong WSL (Ctrl+C) rồi bash scripts/run-server.sh
set -eu
set -o pipefail
_SCRIPT="${BASH_SOURCE[0]%$'\r'}"
ROOT="$(cd "$(dirname "$_SCRIPT")/.." && pwd)"
ROOT="${ROOT%$'\r'}"
cd "$ROOT"
if command -v pgrep >/dev/null 2>&1; then
  if pgrep -f "bin/docklite-wsl" >/dev/null 2>&1; then
    pkill -f "bin/docklite-wsl" 2>/dev/null || true
    sleep 1
  fi
fi
exec bash scripts/run-server.sh
