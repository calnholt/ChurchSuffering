#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT_DIR/Crusaders30XX.csproj"
VERSION_FILE="$ROOT_DIR/VERSION"
ITCH_USER="calnholt"
ITCH_GAME="church-suffering"
ARTIFACTS_DIR="$(mktemp -d "${TMPDIR:-/tmp}/crusaders30xx-publish.XXXXXX")"

if [[ ! -f "$VERSION_FILE" ]]; then
  echo "VERSION file not found: $VERSION_FILE" >&2
  exit 1
fi

VERSION="$(tr -d '[:space:]' < "$VERSION_FILE")"
if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "VERSION must contain a semantic version such as 1.2.3; found: $VERSION" >&2
  exit 1
fi

for command_name in dotnet butler curl; do
  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "Required command is not installed or not on PATH: $command_name" >&2
    exit 1
  fi
done

if [[ -z "${BUTLER_API_KEY:-}" ]]; then
  read -r -s -p "itch.io API key: " BUTLER_API_KEY
  echo
  export BUTLER_API_KEY
fi

if [[ -z "$BUTLER_API_KEY" ]]; then
  echo "BUTLER_API_KEY cannot be empty." >&2
  exit 1
fi

echo "Publishing Crusaders30XX version $VERSION"
echo "Temporary build directory: $ARTIFACTS_DIR"

cd "$ROOT_DIR"

echo "Restoring packages..."
dotnet restore "$PROJECT"

echo "Building MonoGame content..."
dotnet build "$PROJECT" -c Release --no-restore /p:SkipMonoGameContentPipeline=false

publish_build() {
  local runtime="$1"
  local output="$2"

  echo "Publishing $runtime..."
  dotnet publish "$PROJECT" \
    -c Release \
    -r "$runtime" \
    --self-contained true \
    --no-restore \
    -o "$output" \
    -p:SkipMonoGameContentPipeline=true
}

publish_build win-x64 "$ARTIFACTS_DIR/windows"
publish_build osx-arm64 "$ARTIFACTS_DIR/mac-apple-silicon"
chmod +x "$ARTIFACTS_DIR/mac-apple-silicon/Crusaders30XX"
publish_build osx-x64 "$ARTIFACTS_DIR/mac-intel"
chmod +x "$ARTIFACTS_DIR/mac-intel/Crusaders30XX"

push_build() {
  local directory="$1"
  local channel="$2"

  echo "Uploading $channel version $VERSION..."
  butler push "$directory" "$ITCH_USER/$ITCH_GAME:$channel" --userversion "$VERSION"
}

push_build "$ARTIFACTS_DIR/windows" windows
push_build "$ARTIFACTS_DIR/mac-apple-silicon" mac-apple-silicon
push_build "$ARTIFACTS_DIR/mac-intel" mac-intel

verify_channel() {
  local channel="$1"
  local target="$ITCH_USER/$ITCH_GAME:$channel"
  local wharf_url="https://api.itch.io/wharf/latest?target=$ITCH_USER/$ITCH_GAME&channel_name=$channel"
  local response_file="$ARTIFACTS_DIR/wharf-$channel.json"
  local attempt http_code latest

  echo "Butler status for $channel:"
  butler status "$target"
  echo "Polling $channel for version $VERSION (up to 30 minutes)..."

  for attempt in $(seq 1 60); do
    http_code="$(curl -sS -o "$response_file" -w '%{http_code}' "$wharf_url" || true)"
    latest=""
    if [[ "$http_code" == "200" ]]; then
      latest="$(sed -n 's/.*"latest"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$response_file" | head -1)"
      echo "Attempt $attempt/60 for $channel: latest=${latest:-<none>}"
      if [[ "$latest" == "$VERSION" ]]; then
        echo "$channel is live with version $VERSION."
        return 0
      fi
    else
      echo "Attempt $attempt/60 for $channel: HTTP ${http_code:-<none>}"
    fi
    sleep 30
  done

  echo "$channel did not report version $VERSION within 30 minutes." >&2
  butler status "$target" || true
  return 1
}

if verify_channel windows && \
   verify_channel mac-apple-silicon && \
   verify_channel mac-intel; then
  rm -rf "$ARTIFACTS_DIR"
  echo "All three builds are verified. Deleted local builds from $ARTIFACTS_DIR."
else
  echo "Verification failed. Local builds were preserved at $ARTIFACTS_DIR." >&2
  exit 1
fi
