#!/usr/bin/env bash
set -euo pipefail

mode="${1:---verify}"
if [[ "$mode" != "--verify" && "$mode" != "--accept" ]]; then
  echo "usage: $0 [--verify|--accept]" >&2
  exit 2
fi

variants=(
  start-hold
  block-entry
  block-hold
  action-hold
  action-exit
  pledge-hold
  victory-hold
)

for variant in "${variants[@]}"; do
  dotnet run --no-build -- snapshot battle-phase-transition "$variant" "$mode"
done
