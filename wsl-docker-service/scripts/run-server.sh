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
if [ ! -f "$BIN" ]; then
  echo "Chưa có binary. Chạy: bash scripts/build-server.sh" >&2
  exit 1
fi
chmod +x "$BIN" 2>/dev/null || true
exec "$BIN"
