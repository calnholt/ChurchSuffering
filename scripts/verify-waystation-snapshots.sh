#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 0 ]]; then
	echo "usage: $0" >&2
	exit 2
fi

dotnet run --no-build -- snapshot waystation penance-12
