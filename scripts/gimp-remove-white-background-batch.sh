#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage:
  scripts/gimp-remove-white-background-batch.sh INPUT OUTPUT [options]

INPUT can be a single image file or a directory.
OUTPUT can be a PNG file when INPUT is one file, or a directory otherwise.

Options:
  --threshold N       Near-white tolerance, 0-255. Default: 35
  --feather N         Selection feather radius in pixels. Default: 0.5
  --edge-samples N    Sample points per edge. Default: 9
  --no-recursive      Only process files directly inside INPUT when INPUT is a directory
  --gimp PATH         GIMP executable path
  --fail-fast         Stop at the first failed image
  -h, --help          Show this help

Examples:
  scripts/gimp-remove-white-background-batch.sh ~/Downloads/cards ~/Downloads/cards-png
  scripts/gimp-remove-white-background-batch.sh image.jpg image-cutout.png --threshold 45
USAGE
}

if [[ $# -lt 1 ]]; then
  usage
  exit 2
fi

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  usage
  exit 0
fi

if [[ $# -lt 2 ]]; then
  usage
  exit 2
fi

INPUT=$1
OUTPUT=$2
shift 2

THRESHOLD=${GIMP_BG_THRESHOLD:-35}
FEATHER=${GIMP_BG_FEATHER:-0.5}
EDGE_SAMPLES=${GIMP_BG_EDGE_SAMPLES:-9}
RECURSIVE=${GIMP_BG_RECURSIVE:-1}
FAIL_FAST=${GIMP_BG_FAIL_FAST:-0}
GIMP_BIN=${GIMP_BIN:-}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --threshold)
      THRESHOLD=$2
      shift 2
      ;;
    --feather)
      FEATHER=$2
      shift 2
      ;;
    --edge-samples)
      EDGE_SAMPLES=$2
      shift 2
      ;;
    --no-recursive)
      RECURSIVE=0
      shift
      ;;
    --gimp)
      GIMP_BIN=$2
      shift 2
      ;;
    --fail-fast)
      FAIL_FAST=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [[ -z "$GIMP_BIN" ]]; then
  if command -v gimp >/dev/null 2>&1; then
    GIMP_BIN=$(command -v gimp)
  elif [[ -x /Applications/GIMP.app/Contents/MacOS/gimp ]]; then
    GIMP_BIN=/Applications/GIMP.app/Contents/MacOS/gimp
  else
    echo "Could not find GIMP. Pass --gimp /path/to/gimp or set GIMP_BIN." >&2
    exit 1
  fi
fi

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
PY_SCRIPT="$SCRIPT_DIR/gimp-remove-white-background.py"
PY_SCRIPT_ESCAPED=${PY_SCRIPT//\\/\\\\}
PY_SCRIPT_ESCAPED=${PY_SCRIPT_ESCAPED//\'/\\\'}
BATCH_CODE="exec(compile(open('${PY_SCRIPT_ESCAPED}').read(), '${PY_SCRIPT_ESCAPED}', 'exec'))"

GIMP_BG_INPUT=$INPUT \
GIMP_BG_OUTPUT=$OUTPUT \
GIMP_BG_THRESHOLD=$THRESHOLD \
GIMP_BG_FEATHER=$FEATHER \
GIMP_BG_EDGE_SAMPLES=$EDGE_SAMPLES \
GIMP_BG_RECURSIVE=$RECURSIVE \
GIMP_BG_FAIL_FAST=$FAIL_FAST \
"$GIMP_BIN" \
  -i \
  -d \
  -f \
  --batch-interpreter=python-fu-eval \
  -b "$BATCH_CODE" \
  --quit
