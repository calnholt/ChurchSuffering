# Architecture Notes for Agents

Church Suffering is a deckbuilder card game built with an Entity Component System (ECS) architecture.

## Core ECS (`ECS/Core/`)

- **Entity**: Container holding components (`ID`, `Name`, `IsActive`, component dictionary)
- **IComponent**: Base interface for all data containers
- **World**: Main ECS entry point aggregating `EntityManager` and `SystemManager`
- **System**: Abstract base class with `GetRelevantEntities()` and `UpdateEntity()` methods
- **EventManager**: Static pub-sub system for loose coupling between systems
- **EventQueue/EventQueueBridge**: Hybrid queue system separating rules-driven events from triggered events

## Key directories

| Directory | Purpose |
|-----------|---------|
| `ECS/Components/` | Data containers (`CardComponents`, `CombatComponents`, etc.) |
| `ECS/Systems/` | Game logic |
| `ECS/Scenes/` | Scene-specific systems (`BattleScene`, `WorldMapScene`, `ShopScene`, etc.) |
| `ECS/Factories/` | Entity creation (`CardFactory`, `EnemyFactory`, etc.) |
| `ECS/Events/` | Event definitions for pub-sub communication |
| `ECS/Objects/` | Game entity definitions (`Cards/`, `Enemies/`, `Equipment/`, `Medals/`) |
| `ECS/Data/` | Data models, JSON loaders, save system |
| `ECS/Services/` | Business logic and calculations |
| `ECS/Singletons/` | Shared state managers (`StateSingleton`, `FontSingleton`) |
| `Content/Data/` | JSON game data (`locations`, `decks`, `enemies`) |

## Event queue system

The game uses a hybrid event queue with two queues:

- **Rules Queue**: Mandatory events from core systems, such as phases and timers
- **Trigger Queue**: Reactive events from abilities and conditions

Events have states: Pending -> Resolving -> Waiting -> Complete. This keeps execution deterministic and supports multi-frame operations like animations.

## Game flow

`Game1.cs` initializes the `World`, registers systems, and runs the game loop. Systems are updated each frame through `World.Update()`.

## Parallax system

`ParallaxLayerSystem` is agnostic. External systems cooperate with it only through `Transform.Position`, never by reading or writing `ParallaxLayer` fields directly.

Layout systems may write `Position` every frame to assert the entity's base. The parallax system detects external writes, derives the anchor, and applies its offset.

Do not reference `ParallaxLayer.Anchor`, `AnchorInitialized`, `LastWrittenPosition`, or other internal parallax state from other systems.

## Display resolution

Display and input coordinates use a fixed 1920x1080 logical canvas. `DisplayMetrics`
maps that canvas to a native internal render target matching the letterboxed 16:9
content area, capped at 3840x2160. Layout systems must use `Game1.VirtualWidth` and
`Game1.VirtualHeight`; physical viewport dimensions are reserved for render-target
allocation and fullscreen compositing.

## Related domain docs

- `docs/GAME_RULES.md` - current gameplay rules
- `docs/PASSIVE_KEYWORDS.md` - passive keyword behavior
- `docs/DialogEffects.md` - dialogue/effect reference
- `event_queue_system.md` - deeper event queue notes
- `docs/adr/` - architectural decisions
