#!/usr/bin/env bash
# Chỉ build binary (go mod tidy + go build). Dùng khi đổi mã hoặc lần đầu trước khi chạy run-server.sh.
# Nên gọi: bash scripts/build-server.sh
set -eu
set -o pipefail
_SCRIPT="${BASH_SOURCE[0]%$'\r'}"
ROOT="$(cd "$(dirname "$_SCRIPT")/.." && pwd)"
ROOT="${ROOT%$'\r'}"
cd "$ROOT"
mkdir -p bin
go mod tidy
go build -o bin/docklite-wsl ./cmd/server
chmod +x bin/docklite-wsl 2>/dev/null || true
echo "OK: $ROOT/bin/docklite-wsl"
