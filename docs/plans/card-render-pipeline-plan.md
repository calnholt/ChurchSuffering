# Card Render Pipeline Plan

## Document Status

- **Status:** Implemented.
- **Runtime:** .NET 8.0, MonoGame DesktopGL, ECS architecture.
- **Save compatibility:** None required.
- **Verification:** See `docs/display-snapshots.md` for canonical snapshot commands.

## Objective

Render a card's base surface, ordered status shaders, and sheen through one synchronous module with explicit render-target and SpriteBatch ownership. The pass list must be visible in one registry instead of being distributed across event priorities.

## Implemented Design

`CardDisplaySystem` exclusively owns a plain `CardRenderPipeline`. It retains responsibility for normalizing the three public card-render events, temporary transforms, alpha, clipping, base-surface caching, UI bounds, and `HighlightRenderEvent`.

The pipeline accepts the normalized spatial request and a base-draw callback:

```csharp
internal readonly record struct CardRenderRequest(
    Entity Card,
    Vector2 Position,
    float Scale,
    float Rotation);

internal interface ICardOverlayPass
{
    string Name { get; }
    bool AppliesTo(Entity card);
    void Update(GameTime gameTime);
    void Render(CardOverlayPassContext context);
    void Reset();
}

internal sealed class CardRenderPipeline
{
    public void Update(GameTime gameTime);
    public void Reset();
    public void Render(in CardRenderRequest request, Action drawBase);
}
```

`CardOverlayPassCatalog` defines the only pass order: Brittle, Frozen, Thorned, Scorched, Cursed, Poison, Sheen. The former display systems are plain pass objects, not ECS systems, so no system holds or invokes another system.

The existing overlay wrappers, effect parameter caches, bounds service, and pooled shader surfaces remain in use. Passes retain their lazy loading, failure isolation, clocks, and debug-editable properties.

## Render Flow

1. `CardDisplaySystem` prepares the event-specific transform, clip, alpha, and cached/live base callback.
2. Cards without applicable effects draw directly. Disabled or suppressed shaders also bypass composition.
3. The pipeline captures the scene render target and SpriteBatch state, leases two bounded surfaces, and draws the base into the first surface.
4. Applicable passes run in catalog order through `CardOverlayPassContext`, which owns ping-pong swaps and unbinds sampled textures before reusing them as destinations.
5. The pipeline restores the caller's state and blits the final surface. `finally` paths restore state and release both leases after failures.

Sheen now always uses the final composite pass. Its former direct draw path and per-card de-duplication set were removed.

## Removed Seam

- `CardShaderCompositorSystem`
- `CardBaseRenderStartedEvent`
- `CardBaseRenderCompletedEvent`
- `CardShaderPassEvent`

The public `CardRenderEvent`, `CardRenderScaledEvent`, and `CardRenderScaledRotatedEvent` inputs are unchanged.

## Debugging and Verification

`IDebugInspectableChildren` lets `CardDisplaySystem` expose its owned passes to `DebugMenuSystem`, preserving the previous status and sheen tuning tabs without registering fake ECS systems.

The `card-render-pipeline` snapshot fixture fixes all overlay clocks and covers `all-statuses`, `sheen-only`, and `all-statuses-sheen`. Its multi-variant verification script and approved baselines are checked in. Bounds and pass selection/order are covered by unit tests.
