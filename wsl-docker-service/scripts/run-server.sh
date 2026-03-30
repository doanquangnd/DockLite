#!/usr/bin/env bash
# Chạy service DockLite trong WSL (build nếu cần).
# Nên gọi: bash scripts/run-server.sh (không cần quyền thực thi file .sh trên một số mount).
# Nếu Git/editor vẫn lưu CRLF: sed -i 's/\r$//' scripts/run-server.sh
# Hoặc: find . -name '*.sh' -exec sed -i 's/\r$//' {} +
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
exec ./bin/docklite-wsl
