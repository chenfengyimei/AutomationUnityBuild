#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
CONFIG_PATH="${1:-$ROOT_DIR/build-ios.json}"

if [[ -x "$ROOT_DIR/publish/osx-arm64/AutomationUnityBuildIOS" ]]; then
  "$ROOT_DIR/publish/osx-arm64/AutomationUnityBuildIOS" run --config "$CONFIG_PATH"
elif [[ -x "$ROOT_DIR/publish/osx-x64/AutomationUnityBuildIOS" ]]; then
  "$ROOT_DIR/publish/osx-x64/AutomationUnityBuildIOS" run --config "$CONFIG_PATH"
else
  dotnet run --project "$ROOT_DIR/AutomationUnityBuildIOS.csproj" -- run --config "$CONFIG_PATH"
fi
