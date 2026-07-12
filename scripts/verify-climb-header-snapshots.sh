#!/usr/bin/env bash
set -euo pipefail

dotnet run -- snapshot climb-header normal --verify
dotnet run -- snapshot climb-header preview-delta --verify
dotnet run -- snapshot climb-header pulse --verify
dotnet run -- snapshot climb-header overview-hover --verify
