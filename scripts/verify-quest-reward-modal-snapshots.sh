#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

mode="${1:---verify}"
if [[ "$mode" != "--verify" && "$mode" != "--accept" ]]; then
  echo "usage: $0 [--verify|--accept]" >&2
  exit 2
fi

presentations=(
  visible
  entering
  claiming
  skipping
)

for presentation in "${presentations[@]}"; do
  dotnet run --no-build -- snapshot quest-reward-modal --presentation "$presentation" "$mode"
done
