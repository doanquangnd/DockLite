#!/usr/bin/env bash
# Test tích hợp (build tag integration): cần Docker Engine (socket mặc định hoặc DOCKER_HOST).
# Nên gọi: bash scripts/test-integration.sh
set -eu
set -o pipefail
_SCRIPT="${BASH_SOURCE[0]%$'\r'}"
ROOT="$(cd "$(dirname "$_SCRIPT")/.." && pwd)"
ROOT="${ROOT%$'\r'}"
cd "$ROOT"
if ! docker info >/dev/null 2>&1; then
  echo "Cần Docker daemon (docker info thất bại)." >&2
  exit 1
fi
go test -tags=integration -count=1 -v ./integration/...
