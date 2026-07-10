# Coding Standards for Agents

Use this for implementation work. Prefer matching nearby code over inventing a new local style.

## General standards

- Plans list only required work. Do not add optional, nice-to-have, or "if time permits" items.
- Use imports, not fully-qualified names. Avoid `Crusaders30XX.ECS.Data.Cards` when a `using` will do.
- Prioritize readability over cleverness.
- Keep code DRY. When presented with multiple approaches, prefer the hard comprehensive fix over an easy local patch.
- Performance matters. Cache when logical, and clear caches with `DeleteCachesEvent`.
- Systems own their outputs exclusively. Do not duplicate another system's logic or overwrite its output every frame to suppress it; fix ordering or initialization at the source.
- Keep systems self-contained. Encode state on components that the owning system writes, not public static snapshots that other code must query.
- Services are read-only helpers/calculators. They must not mutate ECS components, publish/enqueue events, or change singleton state. Route game-state writes through systems via events.

## Display systems

- Add `DebugEditable` and `DebugTab` attributes to systems with `Draw` functions.
- Create as many `DebugEditable` values as reasonable for visual control; avoid hardcoded magic numbers.
- `Draw()` functions never manage state. They are strictly for rendering.
- Structure draw code for readability with named helper methods and comments around logical sections.
- `FontSingleton.ContentFont` is large (~100px+ native). Small UI text usually needs `FontScale` in the `0.05-0.15` range.
- Text display fields such as `FontScale` should use `Step = 0.01f` in `DebugEditable`.
- Strings drawn with `SpriteFont` (`DrawString`, `MeasureString`, debug/profiler overlays) must be ASCII only. Missing glyphs throw `ArgumentException` at runtime. Prefer separators like `,` or `|` instead of bullets, middle dots, arrows, or em dashes.

## Entities and UI

- Create entities for objects that have functionality or bounds.
- Buttons must be entities with `Transform` and `UIElement` components.
- Never use `MouseState` or `GamePad` state directly. Use `CursorEvents`.
- Prefer `UIElementEventDelegateService` for simple UI events rather than polling `IsClicked`.

## Events and system boundaries

- Create events to drive behavior across systems.
- Prefer events over direct system-to-system calls.
- Never pass another `System` as a constructor parameter to a system.
- Systems must not hold direct references to other systems.
- Cross-system behavior goes through events (`EventManager`, `EventQueue`/`EventQueueBridge`) or shared component state written by the owning system.

## New system checklist

1. Inherit from `ECS/Core/System`.
2. Override `GetRelevantEntities()` and `UpdateEntity()`.
3. Register with `world.AddSystem()` in `Game1.cs`.
4. Add `DebugEditable`/`DebugTab` attributes if it has a `Draw` function.
5. Constructor parameters may include scene resources (`GraphicsDevice`, `SpriteBatch`, `ContentManager`, etc.) but never other systems.

## New component checklist

1. Create a class implementing `IComponent`.
2. Keep it as a data container; put behavior in systems/services as appropriate.
