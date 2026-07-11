#!/usr/bin/env bash
set -euo pipefail

mode="${1:---verify}"
if [[ "$mode" != "--verify" && "$mode" != "--accept" ]]; then
  echo "usage: $0 [--verify|--accept]" >&2
  exit 2
fi

run_snapshot() {
  dotnet run --no-build -- snapshot modular-fx "$@" "$mode"
}

run_snapshot heavy-hammer start
run_snapshot heavy-hammer impact
run_snapshot heavy-hammer late
run_snapshot holy-strike impact
run_snapshot enemy-rock-blast impact
run_snapshot enemy-bite impact
run_snapshot enemy-slash impact
run_snapshot light-slash impact

run_snapshot --module actor-lunge --sample start --seed 1337 --direction right
run_snapshot --module actor-squash-stretch --sample impact --seed 1337 --direction left
run_snapshot --module smoke-screen --sample impact --seed 1337 --direction right
run_snapshot --module claw-slash --sample impact --seed 1337 --direction left
run_snapshot --module halo --sample impact --seed 1337 --direction left
run_snapshot --module shards --sample late --seed 1337 --direction right
run_snapshot --module cracks --sample impact --seed 1337 --direction right
run_snapshot --module cracks --sample impact --seed 7331 --direction right
run_snapshot --module slash-band --sample impact --seed 1337 --direction left
run_snapshot --module slash-band --sample impact --seed 1337 --direction right
