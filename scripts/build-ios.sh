#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
CONFIG_PATH="${1:-$ROOT_DIR/build-ios.json}"

fix_macos_file() {
  local target="$1"
  [[ -e "$target" ]] || return 0
  chmod +x "$target" || true
  if command -v xattr >/dev/null 2>&1; then
    xattr -dr com.apple.quarantine "$target" 2>/dev/null || true
  fi
}

if [[ -x "$ROOT_DIR/publish/osx-arm64/AutomationUnityBuildIOS" ]]; then
  fix_macos_file "$ROOT_DIR/publish/osx-arm64/AutomationUnityBuildIOS"
  "$ROOT_DIR/publish/osx-arm64/AutomationUnityBuildIOS" run --config "$CONFIG_PATH"
elif [[ -x "$ROOT_DIR/publish/osx-x64/AutomationUnityBuildIOS" ]]; then
  fix_macos_file "$ROOT_DIR/publish/osx-x64/AutomationUnityBuildIOS"
  "$ROOT_DIR/publish/osx-x64/AutomationUnityBuildIOS" run --config "$CONFIG_PATH"
else
  dotnet run --project "$ROOT_DIR/AutomationUnityBuildIOS.csproj" -- run --config "$CONFIG_PATH"
fi
