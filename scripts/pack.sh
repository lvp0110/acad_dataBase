#!/usr/bin/env bash
# Pack ready-to-install ZIP (expects DLLs already built into bundle).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BUNDLE_NAME="AcadDwgBrowser.bundle"
BUNDLE="$ROOT/bundle/$BUNDLE_NAME"
ASSETS="$ROOT/scripts/dist-assets"
DIST="$ROOT/dist"
ROOT_INSTALL="$ROOT/Install.bat"

VERSION="${1:-}"
if [[ -z "$VERSION" ]]; then
  VERSION="$(sed -n 's/.*AppVersion="\([^"]*\)".*/\1/p' "$BUNDLE/PackageContents.xml" | head -1)"
  VERSION="${VERSION:-0.0.0}"
fi

if [[ ! -f "$BUNDLE/Contents/2024/AcadDwgBrowser.dll" && ! -f "$BUNDLE/Contents/2025/AcadDwgBrowser.dll" ]]; then
  echo "ERROR: нет AcadDwgBrowser.dll в bundle. Сначала соберите плагин." >&2
  exit 1
fi

PAYLOAD_NAME="AcadDwgBrowser-$VERSION"
STAGE="$(mktemp -d)"
PAYLOAD="$STAGE/$PAYLOAD_NAME"
mkdir -p "$PAYLOAD/$BUNDLE_NAME" "$DIST"

# Keep helper scripts in bundle
cp "$ASSETS/Install.ps1" "$BUNDLE/Install.ps1"
cp "$ASSETS/README-INSTALL.txt" "$BUNDLE/README-INSTALL.txt"
# Simple xcopy installer at bundle root + package root
cp "$ROOT_INSTALL" "$BUNDLE/Install.bat"

cp -R "$BUNDLE/." "$PAYLOAD/$BUNDLE_NAME/"
cp "$ROOT_INSTALL" "$PAYLOAD/Install.bat"
cp "$ASSETS/Install.ps1" "$PAYLOAD/Install.ps1"
cp "$ROOT/ПРОСТАЯ-УСТАНОВКА.txt" "$PAYLOAD/README.txt" 2>/dev/null || cp "$ASSETS/README-INSTALL.txt" "$PAYLOAD/README.txt"

ZIP="$DIST/$PAYLOAD_NAME.zip"
rm -f "$ZIP"
(
  cd "$STAGE"
  zip -r "$ZIP" "$PAYLOAD_NAME" \
    -x "**/.DS_Store" "**/.gitkeep" "**/*.pdb"
)

echo "Packed: $ZIP"
unzip -l "$ZIP" | grep -E 'dll$|Install.bat|PackageContents' || true

rm -rf "$STAGE"

if ! unzip -l "$ZIP" | grep -F "AcadDwgBrowser.dll" >/dev/null; then
  echo "ERROR: AcadDwgBrowser.dll missing from zip" >&2
  exit 1
fi
if ! unzip -l "$ZIP" | grep -F "Install.bat" >/dev/null; then
  echo "ERROR: Install.bat missing from zip" >&2
  exit 1
fi

echo "OK: ZIP ready for Windows — unzip and run Install.bat"
