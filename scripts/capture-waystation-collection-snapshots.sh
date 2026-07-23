#!/usr/bin/env bash
set -euo pipefail

for variant in cards cards-hover saints saints-hover equipment equipment-hover; do
  dotnet run --no-build -- snapshot waystation-collection "$variant"
done
