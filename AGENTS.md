# AGENTS.md

Root guidance for coding agents in this repository. Keep this file slim and use it as a pointer map; detailed guidance belongs in `docs/`.

`CLAUDE.md` is a symlink to this file. Edit only `AGENTS.md` and keep the symlink intact.

## Project snapshot

Church Suffering is a .NET 8.0 MonoGame DesktopGL deckbuilder built around an ECS architecture. `Game1.cs` initializes the world, registers systems, and runs the loop. Content assets are built through `Content/Content.mgcb`.

## Read the relevant docs before editing

- Build/run/checks/content pipeline: `docs/build-run.md`
- ECS architecture, project map, event queue, parallax: `docs/architecture.md`
- Coding standards, system/component/event/UI rules: `docs/coding-standards.md`
- Snapshot fixtures and visual verification: `docs/display-snapshots.md`
- Game rules: `docs/GAME_RULES.md`
- Passive keywords: `docs/PASSIVE_KEYWORDS.md`
- Dialogue/effects: `docs/DialogEffects.md`
- Architectural decisions: `docs/adr/`
- Existing implementation plans: `docs/plans/`

Prefer pointers to authoritative files over copying long instructions into this file.

## Build, run, and verification

Use `docs/build-run.md` as the canonical command list. It includes ordinary run commands, platform/content pipeline setup, and verification by change type.

When you finish implementing an attached or approved plan, always run `dotnet build` from the repo root before marking work complete. Fix compile errors before handing off.

## Snapshot fixture changes

`docs/display-snapshots.md` is the canonical snapshot reference. When adding or changing a snapshot fixture, update that doc, accept intentional baselines into `tests/VisualBaselines/`, verify with `--verify`, and add/update a script under `scripts/` when a fixture has multiple variants. When verifying snapshots, fix and re-run at most two passes to catch obvious rendering, scale, or display issues — do not loop beyond that.

Do not run baseline generations if not instructed to do so.

## Universal project rules

- Do not migrate, reconcile, or preserve backward compatibility for existing save files unless explicitly requested. Assume a fresh run with `dotnet run -- new`.
- Never pass another `System` as a constructor parameter to a system. Cross-system behavior goes through events or shared component state owned by a system.
- Draw functions render only; they do not manage state.
- Never use `MouseState` or `GamePad` state directly; use `CursorEvents`.
- Services are read-only helpers/calculators; game-state writes belong in systems via events.
