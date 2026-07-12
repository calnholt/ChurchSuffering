#!/usr/bin/env bash
set -euo pipefail

dotnet run -- snapshot climb-resource-acquisition entry --verify
dotnet run -- snapshot climb-resource-acquisition fall --verify
dotnet run -- snapshot climb-resource-acquisition catch --verify
dotnet run -- snapshot climb-resource-acquisition pulse --verify
