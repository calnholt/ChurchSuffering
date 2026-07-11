#!/usr/bin/env bash
set -euo pipefail

mode="${1:---verify}"
if [[ "$mode" != "--verify" && "$mode" != "--accept" ]]; then
  echo "usage: $0 [--verify|--accept]" >&2
  exit 2
fi

variants=(
  active
  passive
  used
)

for variant in "${variants[@]}"; do
  dotnet run --no-build -- snapshot equipment-tooltip "$variant" "$mode"
done
