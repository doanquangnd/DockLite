#!/usr/bin/env bash
# Restart service bằng cơ chế PID file (TERM, timeout 5s rồi KILL).
set -eu
set -o pipefail
_SCRIPT="${BASH_SOURCE[0]%$'\r'}"
ROOT="$(cd "$(dirname "$_SCRIPT")/.." && pwd)"
ROOT="${ROOT%$'\r'}"
cd "$ROOT"
bash scripts/stop-server.sh || true
exec bash scripts/run-server.sh
