#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
CURRENT_DIR="$(pwd)"

TARGETS=(
  "$ROOT_DIR"
  "$ROOT_DIR/publish"
  "$ROOT_DIR/publish/osx-arm64"
  "$ROOT_DIR/publish/osx-x64"
  "$ROOT_DIR/publish/osx-arm64/AutomationUnityBuildIOS"
  "$ROOT_DIR/publish/osx-x64/AutomationUnityBuildIOS"
  "$ROOT_DIR/scripts/build-ios.sh"
  "$CURRENT_DIR"
  "$CURRENT_DIR/osx-arm64"
  "$CURRENT_DIR/osx-x64"
  "$CURRENT_DIR/AutomationUnityBuildIOS"
  "$CURRENT_DIR/osx-arm64/AutomationUnityBuildIOS"
  "$CURRENT_DIR/osx-x64/AutomationUnityBuildIOS"
)

echo "Fixing macOS Gatekeeper quarantine flags..."

for target in "${TARGETS[@]}"; do
  if [[ ! -e "$target" ]]; then
    continue
  fi

  if command -v xattr >/dev/null 2>&1; then
    xattr -cr "$target" 2>/dev/null || true
    xattr -dr com.apple.quarantine "$target" 2>/dev/null || true
  fi

  if [[ -f "$target" ]]; then
    chmod +x "$target" || true
  fi

  if [[ -f "$target" && "$(basename "$target")" == "AutomationUnityBuildIOS" ]] && command -v codesign >/dev/null 2>&1; then
    codesign --force --deep --sign - "$target" >/dev/null 2>&1 || true
  fi

  echo "OK: $target"
done

echo
echo "If macOS still blocks it, run the exact commands below from your current folder:"
echo "  xattr -cr ."
echo "  chmod +x ./osx-arm64/AutomationUnityBuildIOS"
echo "  codesign --force --deep --sign - ./osx-arm64/AutomationUnityBuildIOS"
echo "  ./osx-arm64/AutomationUnityBuildIOS 00"
echo
echo "Done. Try running:"
echo "  ./publish/osx-arm64/AutomationUnityBuildIOS 00"
echo "or:"
echo "  ./scripts/build-ios.sh ./configs/build-ios.dev.json"
