# Shared Draw-Primitive Consolidation → `UiDraw` Plan

## Document Status

- **Status:** Draft design, ready for implementation review.
- **Repository:** `ChurchSuffering`
- **Runtime:** .NET 8.0, MonoGame DesktopGL, ECS architecture.
- **RFC sequence:** #5 — independent, wide-but-mechanical; can start any time. Land incrementally per-primitive, snapshot-gated.
- **Required final verification after implementation:** `dotnet build` from the repository root.
- **Save compatibility:** None required.

This plan consolidates re-rolled core draw primitives into a static `UiDraw` facade beside `ModalOverlayChrome`, delegating all caching to the injected `ImageAssetService`. Independent of the other Deep-Module refactors; lowest logic risk, widest file touch count.

---

## 1. Objective

One answer to "how are borders / rounded rects / pixels / lines / gradients drawn?" Glyph/text policy stays in `TextUtils`.

**Chosen shape:** Static `UiDraw` facade delegating to injected `ImageAssetService`, never creating textures itself. Rejected: injected instance service (redundant + adds ctor param to ~40 systems); `SpriteBatch` extension methods (no repo precedent).

Resolves the AGENTS.md tension (services = static read-only helpers, but `ImageAssetService` is deliberately an injected stateful cache): `UiDraw` is the stateless static that renders only and borrows the stateful cache.

---

## 2. Problem

Core draw primitives are re-rolled across renderers though a caching home exists (`ImageAssetService`, instantiated `Game1.cs:191`, injected into most systems).

### 2.1 Border (4-edge outline)

Canonical `ModalOverlayChrome.DrawBorder` (`ECS/Rendering/ModalOverlayChrome.cs:257`); ~16 byte-identical reimplementations across production systems, per-scene helpers, and snapshot fixtures.

*Correction:* `ProfilerSystem.cs:327` and `PassiveApplicationAnimationDisplaySystem.cs:530` are filled/rotated rects, not outlines — reclassify.

### 2.2 Rounded-rect

Canonical cached home `ImageAssetService.GetRoundedRect`/`GetRoundedRectPerCorner` (`:123`/`:136`); only `CardDisplaySystem.cs:372/377` uses it correctly. ~18 sites bypass via `RoundedRectTextureFactory.CreateRoundedRect`, and **8 re-implement the identical `(w,h,r)→Texture2D` cache** `ImageAssetService` already owns.

### 2.3 Pixel (1×1 solid)

`ImageAssetService.GetPixel` (`:113`) caches per color, yet **59 files** hand-roll `new Texture2D(gd,1,1); SetData(...)`. Exclude per-frame noise/mask writers (`DrippingBloodOverlay`/`IncenseOverlay`/`CircularMaskOverlay`).

### 2.4 Filled-rect/line

Duplicated rotated-pixel line (`atan2`) in `ProfilerSystem.cs:332`, `PassiveApplicationAnimationDisplaySystem.cs:535`.

### 2.5 Gradient

Two divergent impls — `ModalOverlayChrome.HorizontalGradientRuleCache:295`, `ClimbSceneDrawHelpers.DrawVerticalGradient:89` (lowest priority).

### 2.6 Glyph filter (live defect)

Canonical font-aware `TextUtils.FilterUnsupportedGlyphs` (`ECS/Utils/TextUtils.cs:10`) vs two divergent private `ToAscii` — `ClimbSceneDrawHelpers.cs:436` (non-ASCII→`?`) and `AchievementSceneDrawHelpers.cs:68` (non-ASCII→space). Same input → three different outputs. `TextUtils.WrapText` (`:35`) also reimplemented twice.

**Why it hurts:** 16 answers to "how are borders drawn"; glyph filtering already diverges in production; a fix/AA tweak applied to one copy silently misses the rest.

---

## 3. Proposed Interface

```csharp
public static class UiDraw
{
    static void Border(SpriteBatch sb, ImageAssetService img, Rectangle r, Color color, int thickness = 1);
    static void FillRect(SpriteBatch sb, ImageAssetService img, Rectangle r, Color color);
    static void Line(SpriteBatch sb, ImageAssetService img, Vector2 a, Vector2 b, Color color, float thickness = 1f);
    static void VerticalGradient(SpriteBatch sb, ImageAssetService img, Rectangle r, Color top, Color bottom, int strips = 16);
    static void RoundedRect(SpriteBatch sb, ImageAssetService img, Rectangle r, int radius, Color color);
    static void RoundedRectPerCorner(SpriteBatch sb, ImageAssetService img, Rectangle r, int tl, int tr, int br, int bl, Color color);
}
```

No `Pixel` method (`ImageAssetService.GetPixel` covers it — call sites stop making their own).

**Text migration:** two `ToAscii` callers → `FilterUnsupportedGlyphs`; two private `WrapText`s → `TextUtils.WrapText`.

### 3.1 What `UiDraw` hides

Per-color/per-shape caching, rounded-rect AA, radius clamping, degenerate-size/`thickness<=0` guards, premultiplied white-pixel tint semantics.

Fold `AchievementSceneDrawHelpers.DrawBorder:30`'s `t = Min(thickness, Min(w,h))` clamp into `UiDraw.Border`.

---

## 4. Dependency Strategy

In-process. `UiDraw` is a static class in the existing assembly next to `ModalOverlayChrome`; consumes the already-injected `ImageAssetService` + existing factories.

Snapshot host exposes `DisplaySnapshotContext.ImageAssets` (`DisplaySnapshotHost.cs:16`), so fixtures call `UiDraw` with zero plumbing.

---

## 5. Testing Strategy

### 5.1 New

- `UiPrimitivesSnapshotFixture` — grid (Border thickness 1/2/4, FillRect, RoundedRect radius small/large/over-clamp, RoundedRectPerCorner, Line, VerticalGradient) → one baseline PNG.
- xUnit tests for `FilterUnsupportedGlyphs` substitutions (the one real behavioral divergence).

### 5.2 Must stay pixel-identical

Existing per-system fixtures (`HotKeySnapshotFixture`, `EnemyDamageMeterSnapshotFixture`, `AssignedBlockRailSnapshotFixture`, `EquipmentTooltipSnapshotFixture`, `PauseMenuSnapshotFixture`, card fixtures) — draw white pixel tinted by `color` (multiply), keep radius clamping = `ClampRadius`, preserve `thickness = Math.Max(1,…)` + `rect.Width<=0` guards.

### 5.3 Intentional visual changes

Glyph migration for the two `ToAscii` scenes — recapture `ClimbSnapshotFixture`/`AchievementSnapshotFixture` baselines as intentional.

### 5.4 Delete

~16 private border methods; 8 private `_roundedRectCache` dicts + helpers + `DeleteCachesEvent` subscriptions; ~59 solid pixel-field creations; two `ToAscii` + two private `WrapText`; `ClimbSceneDrawHelpers.DrawVerticalGradient`. Leave per-scene DrawHelpers holding only palette constants + composite helpers.

---

## 6. Implementation Steps

Sequence **per primitive** (border → rounded-rect+caches → pixel → line/fill → gradient → glyph), each its own snapshot-gated change.

### 6.1 Migration pattern (mechanical, per system)

1. Replace private `DrawBorder`/`DrawRect`/`GetRoundedRectTexture` call sites with `UiDraw.*`.
2. Delete the private method + any `_roundedRectCache`/`DeleteCachesEvent`.
3. Delete the `_pixel = new Texture2D(...)` field (switch raw fills to `UiDraw.FillRect` or retained `_imageAssets.GetPixel`).
4. Build + run the owning snapshot fixture; confirm baseline unchanged.

### 6.2 First targets (small, snapshot-covered)

- `WayStationDialogueSystem`
- `HotKeySnapshotFixture`
- `UIElementBorderDebugSystem`
- `DialogDisplaySystem`

**Effort:** wide (~60 files) but individually trivial. **Risk:** low. Rounded-rect cache removal is highest-value (kills 8 caches); glyph is the only non-mechanical slice — isolate it.

---

## 7. Critical Files

- `ECS/Services/ImageAssetService.cs`
- `ECS/Rendering/ModalOverlayChrome.cs`
- `ECS/Utils/TextUtils.cs`
- `ECS/Scenes/ClimbScene/ClimbSceneDrawHelpers.cs`
- `ECS/Scenes/CardDisplaySystem.cs`

---

## 8. Verification Checklist

- [ ] `dotnet build` succeeds from repo root.
- [ ] Per-primitive: owning snapshot fixture baseline unchanged after migration (border/rounded-rect/pixel).
- [ ] New `UiPrimitivesSnapshotFixture` baseline captured.
- [ ] `FilterUnsupportedGlyphs` unit tests pass.
- [ ] Climb/Achievement baselines recaptured as intentional glyph changes.
