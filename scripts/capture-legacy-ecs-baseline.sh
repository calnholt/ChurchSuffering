#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output_path="${1:-$repo_root/debug/performance/legacy-ecs-baseline.json}"
if [[ "$output_path" != /* ]]; then
  output_path="$repo_root/$output_path"
fi

cd "$repo_root"
CRUSADERS_ECS_PERF_OUTPUT="$output_path" \
  dotnet test tests/Crusaders30XX.Tests/Crusaders30XX.Tests.csproj \
    -c Release \
    --filter FullyQualifiedName~LegacyEcsPerformanceFixtureTests \
    --logger "console;verbosity=normal"
