#!/usr/bin/env bash
# Chỉ chạy binary đã build (nhanh, không go mod tidy mỗi lần). Build: bash scripts/build-server.sh
# Nên gọi: bash scripts/run-server.sh (không cần chmod +x nếu gọi qua bash).
# Nếu Git/editor vẫn lưu CRLF: sed -i 's/\r$//' scripts/run-server.sh
set -eu
set -o pipefail
_SCRIPT="${BASH_SOURCE[0]%$'\r'}"
ROOT="$(cd "$(dirname "$_SCRIPT")/.." && pwd)"
ROOT="${ROOT%$'\r'}"
cd "$ROOT"
BIN="$ROOT/bin/docklite-wsl"
RUNTIME_DIR="${HOME}/.docklite/run"
PID_FILE="${RUNTIME_DIR}/docklite-wsl.pid"
if [ ! -f "$BIN" ]; then
  echo "Chưa có binary. Chạy: bash scripts/build-server.sh" >&2
  exit 1
fi
chmod +x "$BIN" 2>/dev/null || true
mkdir -p "$RUNTIME_DIR"

if [ -f "$PID_FILE" ]; then
  old_pid="$(cat "$PID_FILE" 2>/dev/null || true)"
  if [ -n "$old_pid" ] && kill -0 "$old_pid" 2>/dev/null; then
    echo "docklite-wsl đang chạy với pid=$old_pid (PID file: $PID_FILE)." >&2
    echo "Dùng: bash scripts/stop-server.sh hoặc bash scripts/restart-server.sh" >&2
    exit 1
  fi
  rm -f "$PID_FILE"
fi
SHA256_VALUE=""
if command -v sha256sum >/dev/null 2>&1; then
  SHA256_VALUE="$(sha256sum "$BIN" | awk '{print $1}')"
elif command -v shasum >/dev/null 2>&1; then
  SHA256_VALUE="$(shasum -a 256 "$BIN" | awk '{print $1}')"
fi

GIT_HEAD="unknown"
GIT_DIRTY="unknown"
if command -v git >/dev/null 2>&1 && [ -d "$ROOT/.git" ]; then
  if git -C "$ROOT" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    GIT_HEAD="$(git -C "$ROOT" rev-parse --short=12 HEAD 2>/dev/null || echo unknown)"
    if [ -z "$(git -C "$ROOT" status --porcelain 2>/dev/null)" ]; then
      GIT_DIRTY="clean"
    else
      GIT_DIRTY="dirty"
    fi
  fi
fi

echo "docklite-wsl run metadata:"
echo "  root=$ROOT"
echo "  bin=$BIN"
if [ -n "$SHA256_VALUE" ]; then
  echo "  sha256=$SHA256_VALUE"
else
  echo "  sha256=unavailable"
fi
echo "  git_head=$GIT_HEAD"
echo "  git_tree=$GIT_DIRTY"
echo "  pid_file=$PID_FILE"

"$BIN" &
pid="$!"
echo "$pid" > "$PID_FILE"

cleanup() {
  if [ -f "$PID_FILE" ] && [ "$(cat "$PID_FILE" 2>/dev/null || true)" = "$pid" ]; then
    rm -f "$PID_FILE"
  fi
}
trap cleanup EXIT
wait "$pid"
