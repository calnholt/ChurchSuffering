#!/usr/bin/env bash
set -euo pipefail

dotnet run -- snapshot pause-menu rumble-on --verify
dotnet run -- snapshot pause-menu rumble-off --verify
