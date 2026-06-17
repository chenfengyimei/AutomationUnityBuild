#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
URLS="${1:-http://127.0.0.1:5090}"

cd "$REPO_ROOT"
dotnet run --project ./LinuxGateway/LinuxGateway.csproj --urls "$URLS"
