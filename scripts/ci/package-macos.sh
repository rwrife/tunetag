#!/usr/bin/env bash
set -euo pipefail

RID="${1:-osx-x64}"
VERSION_RAW="${2:-0.1.0}"
ARTIFACT_DIR="${3:-artifacts/macos}"

normalize_version() {
  local raw="${1#v}"
  if [[ "$raw" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)(\.([0-9]+))? ]]; then
    local major="${BASH_REMATCH[1]}"
    local minor="${BASH_REMATCH[2]}"
    local patch="${BASH_REMATCH[3]}"
    local build="${BASH_REMATCH[5]:-0}"
    echo "${major}.${minor}.${patch}.${build}"
  else
    echo "0.1.0.0"
  fi
}

if [[ "$RID" == "osx-x64" ]]; then
  ARCH_LABEL="x64"
elif [[ "$RID" == "osx-arm64" ]]; then
  ARCH_LABEL="arm64"
else
  echo "Unsupported RID: $RID" >&2
  exit 1
fi

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

ARTIFACT_ROOT="$REPO_ROOT/$ARTIFACT_DIR"
PUBLISH_DIR="$ARTIFACT_ROOT/publish/$RID"
BUNDLE_DIR="$ARTIFACT_ROOT/staging/$RID/tunetag.app"
DMG_PATH="$ARTIFACT_ROOT/tunetag-macos-${ARCH_LABEL}.dmg"
CFBUNDLE_VERSION="$(normalize_version "$VERSION_RAW")"

rm -rf "$PUBLISH_DIR" "$ARTIFACT_ROOT/staging/$RID" "$DMG_PATH"
mkdir -p "$PUBLISH_DIR" "$BUNDLE_DIR/Contents/MacOS" "$BUNDLE_DIR/Contents/Resources"

dotnet publish src/TuneTag.App/TuneTag.App.csproj \
  -c Release \
  -r "$RID" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o "$PUBLISH_DIR"

cp -R "$PUBLISH_DIR"/. "$BUNDLE_DIR/Contents/MacOS/"

if [[ -f "$BUNDLE_DIR/Contents/MacOS/TuneTag.App" ]]; then
  mv "$BUNDLE_DIR/Contents/MacOS/TuneTag.App" "$BUNDLE_DIR/Contents/MacOS/tunetag"
fi
chmod +x "$BUNDLE_DIR/Contents/MacOS/tunetag"

cat > "$BUNDLE_DIR/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
  <dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleDisplayName</key>
    <string>TuneTag</string>
    <key>CFBundleExecutable</key>
    <string>tunetag</string>
    <key>CFBundleIdentifier</key>
    <string>com.rwrife.tunetag</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>TuneTag</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>${CFBUNDLE_VERSION}</string>
    <key>CFBundleVersion</key>
    <string>${CFBUNDLE_VERSION}</string>
    <key>LSMinimumSystemVersion</key>
    <string>12.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
  </dict>
</plist>
PLIST

hdiutil create \
  -volname "tunetag" \
  -srcfolder "$BUNDLE_DIR" \
  -ov \
  -format UDZO \
  "$DMG_PATH"

echo "Created artifact: $DMG_PATH"
