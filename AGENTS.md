# AGENTS.md

This file provides guidance to coding agents when working with code in this repository. `CLAUDE.md` is a symlink to this file; edit only `AGENTS.md` and keep the symlink intact.

## Build & Run

```bash
# Build the project
dotnet build

# Run the game
dotnet run

# Delete existing save (primary and legacy paths) then start fresh
dotnet run -- new

# Run without GPU screen effects (weak GPU / dev performance)
dotnet run -- no-shaders
dotnet run --launch-profile no-shaders

# Run without in-battle tutorials (dev / testing)
dotnet run -- skip-tutorials

# Repeated isolated balance fight; tutorials and persistence are disabled
dotnet run -- test-fight hammer skeleton hard

# Display snapshots (headless PNG capture for visual verification)
# See docs/display-snapshots.md for all commands
dotnet run -- snapshot card strike
dotnet run -- snapshot quest-reward-modal --gold 500 --exchange 'strike|white' 'smite|red' --upgrade 'smite|white'

# Publish for distribution
dotnet publish -c Release
```

This is a .NET 8.0 MonoGame DesktopGL project. Content assets are compiled via the MonoGame Content Builder pipeline (`Content/Content.mgcb`).

### Saves

Do not migrate, reconcile, or preserve backward compatibility for existing save files. When save shape or rules change, assume a fresh run (`dotnet run -- new`). Do not add one-off sanitizers for in-progress saves unless explicitly requested.

### macOS / Linux: shader compilation (MGFXC)

`.fx` shaders compile through Wine. One-time setup:

```bash
chmod +x scripts/setup-mgfxc-wine.sh
./scripts/setup-mgfxc-wine.sh
```

Requires Wine 8+ and `7z` (`brew install --cask wine-stable p7zip`). Creates `~/.winemonogame`. `Directory.Build.targets` sets `MGFXC_WINE_PATH` for `dotnet build` on macOS and Linux.

### After executing a plan

When you finish implementing an attached or approved plan, **always** run `dotnet build` from the repo root before marking work complete. Fix any compile errors before handing off.

## Architecture

Crusaders30XX is a deckbuilder card game built with an Entity Component System (ECS) architecture.

### Core ECS (`ECS/Core/`)

- **Entity**: Container holding components (ID, Name, IsActive, component dictionary)
- **IComponent**: Base interface for all data containers
- **World**: Main ECS entry point aggregating EntityManager and SystemManager
- **System**: Abstract base class with `GetRelevantEntities()` and `UpdateEntity()` methods
- **EventManager**: Static pub-sub system for loose coupling between systems
- **EventQueue/EventQueueBridge**: Hybrid queue system separating rules-driven events from triggered events

### Key Directories


| Directory         | Purpose                                                              |
| ----------------- | -------------------------------------------------------------------- |
| `ECS/Components/` | Data containers (CardComponents, CombatComponents, etc.)             |
| `ECS/Systems/`    | Game logic                                                           |
| `ECS/Scenes/`     | Scene-specific systems (BattleScene, WorldMapScene, ShopScene, etc.) |
| `ECS/Factories/`  | Entity creation (CardFactory, EnemyFactory, etc.)                    |
| `ECS/Events/`     | Event definitions for pub-sub communication                          |
| `ECS/Objects/`    | Game entity definitions (Cards/, Enemies/, Equipment/, Medals/)      |
| `ECS/Data/`       | Data models, JSON loaders, save system                               |
| `ECS/Services/`   | Business logic and calculations                                      |
| `ECS/Singletons/` | Shared state managers (StateSingleton, FontSingleton)                |
| `Content/Data/`   | JSON game data (locations, decks, enemies)                           |


### Event Queue System

The game uses a hybrid event queue with two queues:

- **Rules Queue**: Mandatory events from core systems (phases, timers)
- **Trigger Queue**: Reactive events from abilities and conditions

Events have states: Pending → Resolving → Waiting → Complete. This ensures deterministic execution and supports multi-frame operations like animations.

### Game Flow

`Game1.cs` initializes the World, registers all systems, and runs the game loop. Systems are updated each frame via `World.Update()`.

### Parallax System

The `ParallaxLayerSystem` is fully agnostic — external systems cooperate with it only through `Transform.Position`, never by reading or writing `ParallaxLayer` fields directly. Layout systems freely write `Position` every frame to assert the entity's base; the parallax system detects external writes, derives the anchor, and applies its offset. No system should reference `ParallaxLayer.Anchor`, `AnchorInitialized`, `LastWrittenPosition`, or any other internal parallax state.

## Coding Standards

- Plans list only required work — never optional, nice-to-have, or "if time permits" items
- Add `DebugEditable` and `DebugTab` attributes to systems with Draw functions
- Use imports, not fully-qualified names (e.g., avoid `Crusaders30XX.ECS.Data.Cards`)
- Prioritize readability over complexity
- Structure draw code to be as readable as possible using comments and good naming to convey behavior, breaking down logical sections into well named functions
- Draw() functions never manage state - strictly for rendering
- Performance is important - cache when logical; use `DeleteCachesEvent` to clear
- NEVER use MouseState or GamePad state - use CursorEvents
- When presented multiple different approaches, never take the easy way out - prefer the hard but comprehensive approach
  - NO SLOP; STAY DRY
- Systems own their outputs exclusively — never duplicate another system's logic or neutralize it (e.g., overwriting its output each frame to suppress it). Fix ordering or initialization issues at the source instead
- NEVER pass another `System` as a constructor parameter to a system. Systems must not hold direct references to other systems. Cross-system behavior goes through events (`EventManager`, `EventQueue`/`EventQueueBridge`) or shared component state the owning system writes
- Keep systems self-contained: encode state on components (fields the owning system writes), not public static snapshots (`HashSet`, etc.) that other code must query
- Services are read-only helpers/calculators. Do not mutate ECS components, publish/enqueue events, or change singleton state from services; route game-state writes through systems via events.

## Display Systems

- Always create as many `DebugEditable` as possible for maximum visual control, never hardcode magic numbers.
- `FontSingleton.ContentFont` is a large font (~100px+ native). Small UI text requires `FontScale` in the `0.05–0.15` range; default to low values and tune up.
- Text display fields (e.g. `FontScale`) always use `Step = 0.01f` in `DebugEditable`.
- Strings drawn with `SpriteFont` (`DrawString`, `MeasureString`, including debug/profiler overlays) must be **ASCII only** (letters, digits, and basic punctuation such as `, . : / - ( )`). Do not use typographic symbols (middle dot, bullet, em dash, arrows, etc.); missing glyphs throw `ArgumentException` at runtime. Prefer separators like `,` or `|` instead of `·` or `•`.

## Entities

- ALWAYS create entities for objects that have functionality or bounds
- When creating buttons, ALWAYS create an entity with `Transform` and `UIElement` components

## Events

- Create events to drive behavior across different systems
- Prefer events over direct system-to-system calls. If one system needs another to act, define an event and let the target system subscribe or enqueue it — do not inject the target system via constructor
- Prioritize leveraging `UIElementEventDelegateService` for simple events rather than `IsClicked`

### New System

1. Inherit from `ECS/Core/System`
2. Override `GetRelevantEntities()` and `UpdateEntity()`
3. Register with `world.AddSystem()` in `Game1.cs`
4. Add `DebugEditable`/`DebugTab` attributes if it has a Draw function
5. Constructor parameters may include scene resources (`GraphicsDevice`, `SpriteBatch`, `ContentManager`, etc.) but never other systems

### New Component

1. Create class implementing `IComponent`
