#!/usr/bin/env bash
# Dừng tiến trình docklite-wsl (cùng logic phần đầu của restart-server.sh; không chạy lại run-server).
# Gọi: bash scripts/stop-server.sh (không cần chmod +x nếu gọi qua bash).
# Nếu không tìm đúng PID trên mọi máy: dừng tay trong WSL (Ctrl+C) hoặc pkill -f docklite-wsl
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
