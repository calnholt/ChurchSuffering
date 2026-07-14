# Card Render Pipeline → `CardRenderPipeline` Plan

## Document Status

- **Status:** Draft design, ready for implementation review.
- **Repository:** `Crusaders30XX`
- **Runtime:** .NET 8.0, MonoGame DesktopGL, ECS architecture.
- **RFC sequence:** #4 — land **last** among the five Deep-Module refactors; highest visual-regression risk, gated by existing snapshot harness.
- **Required final verification after implementation:** `dotnet build` from the repository root.
- **Save compatibility:** None required.

This plan collapses rendering one status-affected card from ~10 files coordinated by fire-and-forget events into a single `CardRenderPipeline` with an ordered injected pass list. Overlays become `ICardOverlayPass` implementations; the event seam (`CardBaseRenderStartedEvent`/`CompletedEvent`/`CardShaderPassEvent`) is deleted.

---

## 1. Objective

One entry point for base card surface + ordered status overlays + sheen, with explicit RT/batch ownership. Pass list is knowable from the pipeline registry, not by grepping event subscribers.

**Chosen shape:** Pass-list pipeline object. Rejected: single static facade (hard-codes pass list — not open-closed); retained render-graph (over-engineering — no branching deps, just total order).

---

## 2. Problem

Rendering **one** status-affected card spans ~10 files with no single call site, coordinated by fire-and-forget events + priority-ordered subscribers.

### 2.1 The chase

`CardDisplaySystem.RenderCardWithLifecycle` (`ECS/Scenes/CardDisplaySystem.cs:468-515`) draws the base surface (`DrawCard`, `:502`) between `Publish(CardBaseRenderStartedEvent)` (`:478`) and `...CompletedEvent` (`:507`) — it doesn't know status effects exist.

`CardShaderCompositorSystem` subscribes to both (`ECS/Scenes/CardShaderCompositorSystem.cs:30-31`):

- `OnStarted` (`:38`) leases two pooled surfaces (`:63-64`), captures RT+batch state (`:57`), ends the caller's batch and re-`Begin`s a local one (`:62-77`).
- `OnCompleted` (`:105`) publishes `CardShaderPassEvent` (`:125`) then restores and blits (`:127-138`).

### 2.2 Shallow modules behind a wide event seam

**7** systems subscribe to `CardShaderPassEvent`, ordered only by scattered integer priority (higher=earlier, `EventManager.cs:29`): Brittle 100, Frozen 90, Thorned 80, Scorched 70, Cursed 60, Poison 55, Sheen 50.

Each re-derives the same guard (`ShouldRender`, e.g. `FrozenDisplaySystem.cs:285-291`) partly duplicating the compositor's `ShouldProcess` (`CardShaderCompositorSystem.cs:159-170`), then calls `context.Apply(...)` to ping-pong pooled surfaces (`CardShaderPassContext.cs:51-72`). The pass list is knowable only by grepping the event type.

### 2.3 Implicit Begin/End ownership split three ways

Compositor owns the batch during capture (`:62-77`); each overlay wrapper does its own `Begin/End` inside `Apply` (`FrozenOverlay.cs:125-144`); Sheen keeps a **second divergent path** (`CardSheenDisplaySystem.OnCardBaseRenderCompleted:118-135` → `DrawOverlay:180-231`) for the no-status case, guarded by a `_locallyCompositedCards` de-dupe set (`:30/:120/:161`).

### 2.4 Corrections

- **7** subscribers, not 9 (grep's other two hits are publisher + event definition).
- `CardShaderPassEvent`/`CardShaderPassContext` are already `internal` — lower migration risk.
- `CardDisplaySystem.GetRoundedRectTexture` (`:372-375`) is a one-line forwarding wrapper, not a duplicate impl.
- `ShouldProcess` gates 6 components; **Sheen is not gated there** — rides every card and self-gates on `CardSheen.IsActive`.

---

## 3. Proposed Interface

```csharp
public readonly record struct CardRenderRequest(
    Entity Card,
    Vector2 Position,
    float Scale,
    float Rotation,
    float Alpha = 1f,
    Rectangle? ClipRect = null,
    bool PreferCachedBase = false);

public sealed class CardRenderPipeline
{
    public CardRenderPipeline(
        EntityManager em,
        GraphicsDevice gd,
        SpriteBatch sb,
        IReadOnlyList<ICardOverlayPass> orderedPasses, // registry, pre-sorted by Order
        Func<Entity, RenderTarget2D> baseSurfaceProvider); // base draw stays in CardDisplaySystem

    // Renders base + ordered overlays + sheen, composited, into the bound scene RT.
    // Owns/restores the batch.
    public void RenderCard(in CardRenderRequest request);
}

internal interface ICardOverlayPass
{
    int Order { get; }                              // replaces scattered priority ints
    bool AppliesTo(Entity card);                      // replaces per-system ShouldRender/ShouldProcess
    void Configure(CardOverlayPassContext ctx);
    void Render(CardOverlayPassContext ctx);          // calls ctx.Apply(...) — ping-pong hidden
    // optional: Rectangle PaddingFor(Entity card) — co-locate bounds padding with the pass
}
```

`CardOverlayPassContext` = renamed `CardShaderPassContext`, handed only to passes, never published.

`CardDisplaySystem`'s three render handlers (`OnCardRenderEvent:388`, `OnCardRenderScaledEvent:398`, rotated variant) collapse to `_pipeline.RenderCard(new CardRenderRequest(...))`.

`CardBaseRenderStartedEvent`/`CompletedEvent`/`CardShaderPassEvent` are deleted (`CardEvents.cs:54-76`).

### 3.1 What the pipeline hides

Surface leasing (`CardShaderSurfacePool`), two-target ping-pong, scene RT capture/restore (`SpriteBatchRenderTargetCompositor.cs:24-77`), the End/Begin dance, pass priorities, `ShouldProcess`/`ShouldRender` duplication, Sheen's dual-path + de-dupe set, the final blit.

---

## 4. Dependency Strategy

In-process; no new assembly boundary. `GraphicsDevice`/`SpriteBatch`/`ContentManager` owned as today, injected by ctor.

**RT ownership becomes explicit and single:** `RenderCard` is the sole owner of the End/capture/drive/restore/blit bracket. Pooled surfaces (`CardShaderSurfacePool`) stay but are called only inside the pipeline. Per-pass Effects keep their own lazy `ContentManager.Load` + `_failed` latch so a missing shader degrades one pass, not the pipeline.

Boundary-tested through `DisplaySnapshotHost`. `Game1.DrawScene` battle case (`:623-633`, incl. additive trail `:627-632`) is unaffected (pipeline blits into the same bound scene RT).

---

## 5. Testing Strategy

### 5.1 Safety net exists

Per-status fixtures registered today (`DisplaySnapshotRegistry.cs:14-20`): Brittle/Frozen/Thorned/Scorched/Cursed/Poison/Colorless + plain card, each driving the real path via `CardRenderScaledEvent` — once `CardDisplaySystem` routes that into `RenderCard`, they exercise the new module unchanged.

### 5.2 New

- Combination fixtures (frozen+scorched, cursed+poison, all-statuses) to lock pass **ordering** + RT parity.
- Two Sheen fixtures (status + active sheen → sheen composites last order 50; sheen + no status → verifies old direct-draw path faithfully reproduced — subtlest migration).
- Non-snapshot unit test asserting `orderedPasses` sort Brittle→…→Sheen and `AppliesTo` matches old gates.

### 5.3 Delete

Assertions that `CardShaderPassEvent`/`CardBaseRender*Event` are published. No baseline PNGs deleted — re-verified against the new path.

### 5.4 Environment caveat

Headless baselines currently exist only for `colorless-card`/`poison-card`; shader fixtures fall back to no-shader path when `ShaderRuntimeOptions.ShadersEnabled` is false (`FrozenCardSnapshotFixture.cs:56-64`). Guarantee no-shader fallback stays green; accept new shader-path baselines on a pinned reference device.

---

## 6. Implementation Steps

### 6.1 Pipeline core

1. Add `CardRenderPipeline`, `ICardOverlayPass`, `CardRenderRequest`; rename context to `CardOverlayPassContext`.
2. Move compositor logic from `CardShaderCompositorSystem` into pipeline `RenderCard`.
3. Register ordered passes where display systems are constructed in `Game1`.

### 6.2 Overlay migration (mechanical, per file, 7 systems)

- `OnShaderPass` → `Render`
- `ConfigureOverlay` → `Configure`
- `ShouldRender` → `AppliesTo`
- `PassPriority` const → `Order`
- Drop ctor `EventManager.Subscribe`
- `*Overlay` wrappers, `EffectParameterCache`, `CardShaderSurfacePool` reused untouched
- Keep `[DebugTab]`/`[DebugEditable]` tuning attrs on the pass class

Consider `PaddingFor(card)` on `ICardOverlayPass` to close the "add a status = edit two files" gap (`CardRenderBoundsService` already special-cases per-status overflow at `:36-57`).

### 6.3 Caller migration

Rewire three handlers to `RenderCard`; keep base-surface caching (`GetOrCreateCachedBase:517`) behind injected `baseSurfaceProvider`; delete `CardShaderCompositorSystem` + the three events.

**Effort:** medium-large. **Risk:** visual regression, not logic. Highest-risk: exact pass order + RT swap parity, End/Begin restore bracket, scene-target restore on abort path (`CardShaderCompositorSystem.cs:147-157`), Sheen's dual path. Mitigation: land behind green fallback-path baselines first, then extend to shader path.

---

## 7. Critical Files

- `ECS/Scenes/CardShaderCompositorSystem.cs`
- `ECS/Rendering/CardShaderPassContext.cs`
- `ECS/Scenes/CardDisplaySystem.cs`
- `ECS/Scenes/CardSheenDisplaySystem.cs`
- `ECS/Rendering/CardRenderBoundsService.cs`

---

## 8. Verification Checklist

- [ ] `dotnet build` succeeds from repo root.
- [ ] All existing per-status card snapshots pass against new pipeline (no-shader fallback first).
- [ ] New combination + Sheen fixtures captured.
- [ ] In-app: render frozen/brittle/thorned/scorched/cursed/poison cards + combinations + sheen; confirm no visual regression.
