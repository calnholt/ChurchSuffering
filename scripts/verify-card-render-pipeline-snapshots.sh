#!/usr/bin/env bash
set -euo pipefail

mode="${1:---verify}"
if [[ "$mode" != "--verify" && "$mode" != "--accept" ]]; then
  echo "usage: $0 [--verify|--accept]" >&2
  exit 2
fi

variants=(
  all-statuses
  sheen-only
  all-statuses-sheen
)

for variant in "${variants[@]}"; do
  dotnet run --no-build -- snapshot card-render-pipeline "$variant" "$mode"
done
