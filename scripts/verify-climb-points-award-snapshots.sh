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

variants=(
  intro
  partial-route
  victory-route
  victory-impact
  time0-ready
  partial-ready
  mid-ready
  deep-ready
  victory-ready
  abandoned-ready
)

for variant in "${variants[@]}"; do
  dotnet run --no-build -- snapshot climb-points-award "$variant" "$mode"
done
