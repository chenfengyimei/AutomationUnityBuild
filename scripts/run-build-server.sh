#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://127.0.0.1:5088}"
export BUILD_SERVER_DATA_ROOT="${BUILD_SERVER_DATA_ROOT:-$REPO_ROOT/buildserver-data}"

dotnet run --project "$REPO_ROOT/BuildServer/BuildServer.csproj"
