#!/usr/bin/env bash
set -euo pipefail

mode="${1:---verify}"
if [[ "$mode" != "--verify" && "$mode" != "--accept" ]]; then
  echo "usage: $0 [--verify|--accept]" >&2
  exit 2
fi

dotnet run --no-build -- snapshot climb-inventory-overlay "$mode"
dotnet run --no-build -- snapshot climb-inventory-equipment-tooltip "$mode"
