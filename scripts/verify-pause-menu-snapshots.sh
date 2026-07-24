#!/usr/bin/env bash
set -euo pipefail

dotnet run -- snapshot pause-menu rumble-50 --verify
dotnet run -- snapshot pause-menu rumble-0 --verify
