#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 0 ]]; then
	echo "usage: $0" >&2
	exit 2
fi

for variant in incense penance-12; do
	dotnet run --no-build -- snapshot waystation "$variant"
done
