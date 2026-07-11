#!/usr/bin/env bash
set -euo pipefail

dotnet run -- snapshot achievement-overview --verify
dotnet run -- snapshot achievement-detail --verify
