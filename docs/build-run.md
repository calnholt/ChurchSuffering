# Build, Run, and Verification

This project is a .NET 8.0 MonoGame DesktopGL game. Content assets are built through the MonoGame Content Builder pipeline (`Content/Content.mgcb`).

## Everyday commands

```bash
# Build the project
dotnet build

# Run the game
dotnet run

# Delete existing save files and start fresh
dotnet run -- new

# Run without GPU screen effects
dotnet run -- no-shaders
dotnet run --launch-profile no-shaders

# Run without in-battle tutorials
dotnet run -- skip-tutorials

# DEBUG only: asynchronous GPU command timing and rendering workload counters
dotnet run -- profile-gpu

# DEBUG only: deterministic 60-card, 4K card-list performance benchmark
dotnet run -- card-list-profile profile-gpu --render-scale 2

# Unlock every collectible card, medal, and equipment item in the current save
dotnet run -- unlock
dotnet run --launch-profile unlock

# Repeated isolated balance fight; tutorials and persistence are disabled
dotnet run -- test-fight hammer skeleton hard

# Publish for distribution
dotnet publish -c Release
```

`profile-gpu` is optional and stripped before ordinary command parsing. On DesktopGL
systems with OpenGL timestamp-query support it records delayed GPU milliseconds without
waiting for results. Unsupported systems still report MonoGame rendering workload
counters. Shift+Escape writes `logs/performance-report.txt`; pending queries are reported
at shutdown and are not synchronously drained.

## Verification by change type

Use the smallest check set that covers the subsystem you changed. When in doubt, run the broader check.

| Change type | Checks |
|-------------|--------|
| Any code change | `dotnet build` |
| Game logic, services, rules, event flow, data model | `dotnet test tests/Crusaders30XX.Tests/Crusaders30XX.Tests.csproj` |
| UI, display, rendering, layout | Relevant snapshot `--verify` commands from `docs/display-snapshots.md` |
| Player HUD display/layout | `./scripts/verify-player-hud-snapshots.sh` |
| Battle setup, cards, enemies, combat flow, balance-sensitive changes | `dotnet run -- test-fight hammer skeleton hard` |
| Content, SFX, music, shaders, `Content.mgcb` | `dotnet build /p:SkipMonoGameContentPipeline=false` |

For a broad local safety pass before handing off substantial work:

```bash
dotnet build
dotnet test tests/Crusaders30XX.Tests/Crusaders30XX.Tests.csproj
```

## Snapshot and visual checks

`docs/display-snapshots.md` is the canonical reference for snapshot fixture commands, fixture arguments, output paths, accepting baselines, and adding new fixtures. Do not duplicate the fixture command catalog elsewhere.

Short version:

- Generated captures and failure diffs go under `debug/snapshots/` and are not committed.
- Approved baselines live under `tests/VisualBaselines/<fixture-id>/` and are committed.
- Use `--verify` to compare against an approved baseline.
- Use `--accept` only when intentionally replacing an approved baseline.
- If a fixture has multiple variants, add or update a script under `scripts/` so future agents can verify the full set.

## Saves

Do not migrate, reconcile, or preserve backward compatibility for existing save files. When save shape or rules change, assume a fresh run:

```bash
dotnet run -- new
```

Do not add one-off sanitizers for in-progress saves unless explicitly requested.

## macOS / Linux content pipeline setup

On macOS/Linux, default `dotnet build` sets `SkipMonoGameContentPipeline=true` and uses pre-built `.xnb` files in `Content/bin/DesktopGL/Content`.

If you need to rebuild content locally, set up the tooling first:

```bash
chmod +x scripts/setup-mgfxc-wine.sh
./scripts/setup-mgfxc-wine.sh

chmod +x scripts/setup-mgcb-ffmpeg.sh
./scripts/setup-mgcb-ffmpeg.sh
```

Then run:

```bash
dotnet build /p:SkipMonoGameContentPipeline=false
```

Notes:

- `.fx` shaders compile through Wine. `scripts/setup-mgfxc-wine.sh` creates `~/.winemonogame`; `Directory.Build.targets` sets `MGFXC_WINE_PATH`.
- MonoGame 3.8.4+ bundles `ffmpeg`/`ffprobe` targeting newer macOS than Monterey. `scripts/setup-mgcb-ffmpeg.sh` patches older macOS setups.
- After adding new SFX or music to `Content.mgcb`, compile matching `.xnb` files locally or audio may fail silently at runtime.
- CI builds content on Windows and publishes the `monogame-content` artifact for release builds.
