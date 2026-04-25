#!/usr/bin/env bash
# Dừng tiến trình docklite-wsl theo PID file ~/.docklite/run/docklite-wsl.pid.
# Gọi: bash scripts/stop-server.sh (không cần chmod +x nếu gọi qua bash).
set -eu
set -o pipefail
_SCRIPT="${BASH_SOURCE[0]%$'\r'}"
ROOT="$(cd "$(dirname "$_SCRIPT")/.." && pwd)"
ROOT="${ROOT%$'\r'}"
cd "$ROOT"
RUNTIME_DIR="${HOME}/.docklite/run"
PID_FILE="${RUNTIME_DIR}/docklite-wsl.pid"

if [ ! -f "$PID_FILE" ]; then
  echo "Không có PID file: $PID_FILE (service có thể đã dừng)." >&2
  exit 0
fi

pid="$(cat "$PID_FILE" 2>/dev/null || true)"
if [ -z "$pid" ]; then
  echo "PID file rỗng: $PID_FILE" >&2
  rm -f "$PID_FILE"
  exit 0
fi

if ! kill -0 "$pid" 2>/dev/null; then
  echo "PID $pid không tồn tại. Xóa PID file cũ." >&2
  rm -f "$PID_FILE"
  exit 0
fi

kill -TERM "$pid" 2>/dev/null || true
for _ in 1 2 3 4 5; do
  if ! kill -0 "$pid" 2>/dev/null; then
    rm -f "$PID_FILE"
    exit 0
  fi
  sleep 1
done

kill -KILL "$pid" 2>/dev/null || true
rm -f "$PID_FILE"
