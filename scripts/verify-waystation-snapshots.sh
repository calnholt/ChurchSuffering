#!/usr/bin/env bash
set -euo pipefail

mode="${1:---verify}"
if [[ "$mode" != "--verify" && "$mode" != "--accept" ]]; then
	echo "usage: $0 [--verify|--accept]" >&2
	exit 2
fi

variants=(default modal-first-unlock modal-hammer modal-full)

for variant in "${variants[@]}"; do
	dotnet run --no-build -- snapshot waystation "$variant" "$mode"
done
