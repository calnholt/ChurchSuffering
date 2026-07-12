#!/usr/bin/env bash
set -euo pipefail

mode="${1:---verify}"
if [[ "$mode" != "--verify" && "$mode" != "--accept" ]]; then
  echo "usage: $0 [--verify|--accept]" >&2
  exit 2
fi

for variant in intro settled; do
  dotnet run --no-build -- snapshot climb-character-dialog "$variant" "$mode"
done
