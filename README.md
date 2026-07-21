# Crusaders30XX

A .NET 8.0 MonoGame DesktopGL deckbuilder built around an Entity Component System (ECS). Runs start at the WayStation, climb under Penance pressure, and fight in block-then-act combat.

## Play

Download builds from itch.io: [calnholt.itch.io/church-suffering](https://calnholt.itch.io/church-suffering).

The in-repo release number lives in [`VERSION`](VERSION).

## Gameplay

- **Block, then act.** Enemies attack first each turn; your hand blocks incoming attacks and deals damage on your action phase.
- **WayStation → Climb.** Depart from the WayStation into a timed Climb with shops, encounters, and run-scoped loadouts.
- **Penance.** Scale difficulty per weapon (Sword, Dagger, Hammer) with accumulating constraints.
- **Build identity.** Cards, equipment, medals, and meta unlocks shape each run.

Full rules: [`docs/GAME_RULES.md`](docs/GAME_RULES.md).

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- MonoGame DesktopGL (pulled via the project)

On macOS/Linux, ordinary builds use pre-built content; rebuilding shaders/audio needs extra tooling — see [`docs/build-run.md`](docs/build-run.md).

## Build and run

```bash
dotnet build
dotnet run
dotnet run -- new   # wipe saves and start fresh
dotnet test tests/Crusaders30XX.Tests/Crusaders30XX.Tests.csproj
```

Handy developer flags (details in [`docs/build-run.md`](docs/build-run.md)): `unlock`, `skip-tutorials`, `test-fight`, `no-shaders`.

## Repository layout

```
Crusaders30XX/
├── ECS/           # Components, systems, scenes, objects, services, events, data
├── Content/       # Assets and Content.mgcb
├── Game1.cs       # World init, system registration, game loop
├── Program.cs     # Entry and launch flags
├── tests/         # Unit tests and visual baselines
├── docs/          # Architecture, rules, build, ADRs, plans
└── scripts/       # Verification and content tooling
```

## Documentation

| Topic | Doc |
|-------|-----|
| Build, run, verification | [`docs/build-run.md`](docs/build-run.md) |
| ECS architecture | [`docs/architecture.md`](docs/architecture.md) |
| Coding standards | [`docs/coding-standards.md`](docs/coding-standards.md) |
| Snapshot fixtures | [`docs/display-snapshots.md`](docs/display-snapshots.md) |
| Game rules | [`docs/GAME_RULES.md`](docs/GAME_RULES.md) |
| Passive keywords | [`docs/PASSIVE_KEYWORDS.md`](docs/PASSIVE_KEYWORDS.md) |
| Dialogue / effects | [`docs/DialogEffects.md`](docs/DialogEffects.md) |
| Architectural decisions | [`docs/adr/`](docs/adr/) |
| Implementation plans | [`docs/plans/`](docs/plans/) |

## Agents and contributors

Coding agents and contributors should start with [`AGENTS.md`](AGENTS.md) and follow [`docs/coding-standards.md`](docs/coding-standards.md). Prefer those docs over duplicating project rules here.

## License

Proprietary. All rights reserved.
