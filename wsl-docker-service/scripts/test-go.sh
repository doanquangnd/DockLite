#!/usr/bin/env bash
# Chạy go vet và go test trên toàn bộ module (gói nằm trong cmd/, internal/ — không có .go ở gốc).
# Test tích hợp + Docker: bash scripts/test-integration.sh
# Nên gọi: bash scripts/test-go.sh
set -eu
set -o pipefail
_SCRIPT="${BASH_SOURCE[0]%$'\r'}"
ROOT="$(cd "$(dirname "$_SCRIPT")/.." && pwd)"
ROOT="${ROOT%$'\r'}"
cd "$ROOT"
go vet ./...
go test ./...
