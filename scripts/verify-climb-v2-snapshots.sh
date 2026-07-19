#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

mode="--verify"
if [[ "${1:-}" == "--accept" ]]; then
  mode="--accept"
elif [[ -n "${1:-}" ]]; then
  echo "Usage: $0 [--accept]" >&2
  exit 2
fi

fixtures=(
  climb-no-events
  climb-active-events
  climb-hover-preview
  climb-medal-tooltip-hover
  climb-card-tooltip-hover
  climb-equipment-tooltip-hover
  climb-v2-entrance
  climb-v2-ashes
  climb-v2-purchase
)

for fixture in "${fixtures[@]}"; do
  dotnet run --no-build -- snapshot "$fixture" "$mode"
done

for variant in normal preview-delta pulse overview-hover; do
  dotnet run --no-build -- snapshot climb-header "$variant" "$mode"
done

for variant in entry fall catch pulse; do
  dotnet run --no-build -- snapshot climb-resource-acquisition "$variant" "$mode"
done
