#!/usr/bin/env bash
set -euo pipefail

dotnet run -- snapshot guardian-angel idle --verify
dotnet run -- snapshot guardian-angel message --verify
dotnet run -- snapshot guardian-angel card-hop --verify
dotnet run -- snapshot guardian-angel medal-loop --verify
dotnet run -- snapshot guardian-angel enemy-recoil --verify
