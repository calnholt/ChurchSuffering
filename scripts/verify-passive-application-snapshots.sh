#!/usr/bin/env bash
set -euo pipefail

mode="${1:---verify}"
if [[ "$mode" != "--verify" && "$mode" != "--accept" ]]; then
  echo "usage: $0 [--verify|--accept]" >&2
  exit 2
fi

run_snapshot() {
  dotnet run --no-build -- snapshot passive-application "$@" "$mode"
}

run_snapshot burn hold player single
run_snapshot aegis entry player single
run_snapshot fear hold enemy single
run_snapshot frostbite exit player single
run_snapshot burn hold enemy multi
run_snapshot wounded hold player attack
