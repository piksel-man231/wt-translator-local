#!/usr/bin/env bash
# Build a self-contained macOS application bundle on macOS.
set -euo pipefail
cd "$(dirname "$0")/.."
RID="${1:-osx-arm64}"
VERSION="${2:-1.1.0}"
case "$RID" in osx-arm64|osx-x64) ;; *) echo "Usage: $0 [osx-arm64|osx-x64] [version]" >&2; exit 1 ;; esac
if [[ "$(uname -s)" != Darwin ]]; then
  echo "Run this script on macOS (codesign is required)." >&2
  exit 1
fi
if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Version must have the form 1.0.0." >&2
  exit 1
fi
PUBLISH="out/$RID"
APP="out/bundle-$RID/WtTranslator.app"
# These directories contain generated output only.
rm -rf "$PUBLISH" "out/bundle-$RID"
dotnet publish src/WtTranslator.App -c Release -r "$RID" --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:Version="$VERSION" -o "$PUBLISH"
mkdir -p "$APP/Contents/MacOS" dist
cp -R "$PUBLISH/." "$APP/Contents/MacOS/"
find "$APP" -name '*.pdb' -delete
sed "s/__VERSION__/$VERSION/g" packaging/macos/Info.plist > "$APP/Contents/Info.plist"
codesign --force --deep --sign - "$APP"
codesign --verify --deep --strict "$APP"
ditto -c -k --sequesterRsrc --keepParent "$APP" "dist/WtTranslator-$VERSION-$RID.zip"
echo "Application: $APP"
echo "Archive: dist/WtTranslator-$VERSION-$RID.zip"
