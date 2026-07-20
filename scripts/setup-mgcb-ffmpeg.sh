#!/usr/bin/env bash
# One-time setup for compiling MonoGame audio on macOS versions older than Sonoma.
# MonoGame 3.8.4+ ships ffprobe/ffmpeg built for macOS 14+, which fails on older
# systems with: Symbol not found: _AVCaptureDeviceTypeContinuityCamera
# See: https://github.com/MonoGame/MonoGame/discussions/9012

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TOOLS_DIR="$SCRIPT_DIR/tools"
FFMPEG_VERSION="6.1"
FFBIN_API="https://ffbinaries.com/api/v1/version/${FFMPEG_VERSION}"

if ! $([ "$(uname -s)" = "Darwin" ] || [ "$(uname -s)" = "Linux" ]); then
  echo "This script is only needed on macOS/Linux. Windows MGCB audio works natively."
  exit 0
fi

mkdir -p "$TOOLS_DIR"

download_osx_binaries() {
  local json url
  json="$(curl -fsSL "$FFBIN_API")"
  url="$(printf '%s' "$json" | python3 -c 'import json,sys; print(json.load(sys.stdin)["bin"]["osx-64"]["ffprobe"])')"
  curl -fsSL -o /tmp/mgcb-ffprobe.zip "$url"
  unzip -o /tmp/mgcb-ffprobe.zip -d "$TOOLS_DIR" >/dev/null
  url="$(printf '%s' "$json" | python3 -c 'import json,sys; print(json.load(sys.stdin)["bin"]["osx-64"]["ffmpeg"])')"
  curl -fsSL -o /tmp/mgcb-ffmpeg.zip "$url"
  unzip -o /tmp/mgcb-ffmpeg.zip -d "$TOOLS_DIR" >/dev/null
  chmod +x "$TOOLS_DIR/ffprobe" "$TOOLS_DIR/ffmpeg"
  xattr -d com.apple.quarantine "$TOOLS_DIR/ffprobe" "$TOOLS_DIR/ffmpeg" 2>/dev/null || true
}

download_linux_binaries() {
  local json url arch
  arch="$(uname -m)"
  case "$arch" in
    x86_64) arch="linux-64" ;;
    aarch64|arm64) arch="linux-arm64" ;;
    *)
      echo "error: unsupported Linux architecture: $arch"
      exit 1
      ;;
  esac

  json="$(curl -fsSL "$FFBIN_API")"
  url="$(printf '%s' "$json" | python3 -c "import json,sys; print(json.load(sys.stdin)['bin']['${arch}']['ffprobe'])")"
  curl -fsSL -o /tmp/mgcb-ffprobe.zip "$url"
  unzip -o /tmp/mgcb-ffprobe.zip -d "$TOOLS_DIR" >/dev/null
  url="$(printf '%s' "$json" | python3 -c "import json,sys; print(json.load(sys.stdin)['bin']['${arch}']['ffmpeg'])")"
  curl -fsSL -o /tmp/mgcb-ffmpeg.zip "$url"
  unzip -o /tmp/mgcb-ffmpeg.zip -d "$TOOLS_DIR" >/dev/null
  chmod +x "$TOOLS_DIR/ffprobe" "$TOOLS_DIR/ffmpeg"
}

if [ "$(uname -s)" = "Darwin" ]; then
  echo "Downloading macOS ffmpeg/ffprobe ${FFMPEG_VERSION} into scripts/tools..."
  download_osx_binaries
  PATCH_DIRS=(
    "$HOME/.nuget/packages/dotnet-mgcb/3.8.4.1/tools/net8.0/any/osx"
    "$HOME/.nuget/packages/dotnet-mgcb/3.8.4/tools/net8.0/any/osx"
    "$HOME/.nuget/packages/monogame.content.builder.task/3.8.4/build/dotnet-tools/.store/dotnet-mgcb/3.8.4/dotnet-mgcb/3.8.4/tools/net8.0/any/osx"
    "$HOME/.dotnet/tools/.store/dotnet-mgcb/3.8.4.1/dotnet-mgcb/3.8.4.1/tools/net8.0/any/osx"
  )
else
  echo "Downloading Linux ffmpeg/ffprobe ${FFMPEG_VERSION} into scripts/tools..."
  download_linux_binaries
  PATCH_DIRS=(
    "$HOME/.nuget/packages/dotnet-mgcb/3.8.4.1/tools/net8.0/any/linux-x64"
    "$HOME/.nuget/packages/dotnet-mgcb/3.8.4/tools/net8.0/any/linux-x64"
    "$HOME/.nuget/packages/monogame.content.builder.task/3.8.4/build/dotnet-tools/.store/dotnet-mgcb/3.8.4/dotnet-mgcb/3.8.4/tools/net8.0/any/linux-x64"
  )
fi

if ! "$TOOLS_DIR/ffprobe" -hide_banner -version >/dev/null 2>&1; then
  echo "error: downloaded ffprobe failed to run."
  exit 1
fi

patched=0
for dir in "${PATCH_DIRS[@]}"; do
  if [ -d "$dir" ]; then
    cp "$TOOLS_DIR/ffprobe" "$dir/ffprobe"
    cp "$TOOLS_DIR/ffmpeg" "$dir/ffmpeg"
    chmod +x "$dir/ffprobe" "$dir/ffmpeg"
    xattr -d com.apple.quarantine "$dir/ffprobe" "$dir/ffmpeg" 2>/dev/null || true
    echo "Patched $dir"
    patched=1
  fi
done

if [ "$patched" -eq 0 ]; then
  echo "warning: no MGCB osx/linux folders found yet. Run dotnet restore first, then re-run this script."
  echo "Binaries are ready in $TOOLS_DIR."
  exit 0
fi

echo ""
echo "MGCB audio tooling ready."
echo "Re-run this script after dotnet tool restore if MGCB packages are re-downloaded."
