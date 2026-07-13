#!/usr/bin/env bash
set -euo pipefail

mode="${1:---verify}"
if [[ "$mode" != "--verify" && "$mode" != "--accept" ]]; then
  echo "usage: $0 [--verify|--accept]" >&2
  exit 2
fi

dotnet run --no-build -- snapshot card-list-modal-top "$mode" no-shaders
dotnet run --no-build -- snapshot card-list-modal-middle "$mode" no-shaders
dotnet run --no-build -- snapshot card-list-modal-bottom "$mode" no-shaders
