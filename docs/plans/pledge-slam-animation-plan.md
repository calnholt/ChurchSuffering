# Pledge Slam Animation Plan

## Context

- **Mockup:** `mockups/pledge-slam-animations-v1.html` — "Gravity Drop" variant (~550ms icon slam, impact FX at ~380ms)
- **Target system:** `ECS/Scenes/BattleScene/PledgeDisplaySystem.cs` (Archetype **A — Card-local**; draws via existing `CardRender*` event subscriptions)
- **Trigger:** `PledgeAddedEvent` published from `PledgeManagementSystem.AddPledgeToCard` when `Pledge` is added
- **Out of scope:** `card-shake`, `.card-pledge-glow` / red border glow — no card transform or card-border FX
- **After settle:** static pledge icon draw path unchanged (`IconScale` 0.09, `IconOffsetY` -190)

Reference pattern: `IntimidateDisplaySystem.cs` — per-entity `_animByEntityId`, `UpdateEntity` ticks elapsed time, render state sampled during card draw callbacks.

## Files Modified

| File | Change |
| --- | --- |
| `ECS/Scenes/BattleScene/PledgeDisplaySystem.cs` | Animation state, `PledgeAddedEvent` subscription, impact FX draw helpers, keyframe sampling, `UpdateEntity` tick/cleanup |

## Implementation Summary

1. `_animByEntityId` tracks `Elapsed` per card; started on `PledgeAddedEvent`, cleared after total duration or pledge removal.
2. `ComputeIconLandTransform` shared by static and animated draw paths.
3. Icon drop uses CSS keyframe stops with cubic-bezier `(0.22, 1, 0.36, 1)` per segment over `DropDurationSeconds` (0.55s default).
4. Impact FX at `ImpactStartDelaySeconds` (0.38s): dust puff, flash ellipse, expanding ring.
5. Card render handlers branch: active anim → `DrawPledgeSlam`, else `DrawPledgeIcon`.

## DebugEditable Properties

All on `PledgeDisplaySystem`, `Step = 0.01f` for floats unless noted.

| Group | Property | Default |
| --- | --- | --- |
| Icon | `IconScale` | 0.09 |
| Icon | `IconOffsetY` | -190 |
| Drop | `DropDurationSeconds` | 0.55 |
| Drop | `DropStartYOffset` | -280 |
| Drop | `DropOvershootYOffset` | 8 |
| Drop | `DropReboundYOffset` | -4 |
| Drop | `DropSettleYOffset` | 2 |
| Drop | `DropStartScale` | 1.15 |
| Drop | `DropImpactScaleX/Y` | 1.08 / 0.88 |
| Drop | `DropStartRotationDeg` | -8 |
| Shadow | `ShadowOffsetX/Y` | 0 / 4 |
| Shadow | `ShadowAlpha` | 0.45 |
| Impact timing | `ImpactStartDelaySeconds` | 0.38 |
| Ring | `RingBaseDiameterPx` | 20 |
| Ring | `RingStartScale` | 0.3 |
| Ring | `RingEndScale` | 3.5 |
| Ring | `RingDurationSeconds` | 0.45 |
| Ring | `RingStartBorderPx` | 3 |
| Flash | `FlashWidthPx` | 80 |
| Flash | `FlashHeightPx` | 40 |
| Flash | `FlashDurationSeconds` | 0.2 |
| Dust | `DustWidthPx` | 60 |
| Dust | `DustHeightPx` | 20 |
| Dust | `DustOffsetYPx` | 20 |
| Dust | `DustDurationSeconds` | 0.35 |

## Color Palette

| Role | CSS | MonoGame |
| --- | --- | --- |
| HUD red (ring) | `#c41e3a` | `new Color(196, 30, 58)` |
| Impact flash core | `rgba(255, 220, 200, 0.9)` | `new Color(255, 220, 200) * (alpha * 0.9f)` |
| Dust puff | `rgba(220, 215, 206, 0.5)` | `new Color(220, 215, 206) * (alpha * 0.5f)` |
| Icon shadow | `rgba(0, 0, 0, 0.45)` | `Color.Black * 0.45f` |

## Pixel Position Reference (card-local, default 268x377 card, cardScale = 1)

| Element | X | Y | W | H | Notes |
| --- | --- | --- | --- | --- | --- |
| Icon land point | center | center - 190 | — | — | `IconOffsetY` |
| Icon visual | landX | landY | ~72 | ~72 | `pledge.png` x `IconScale 0.09` |
| Impact ring | landX | landY | 20 | 20 | scales 0.3→3.5 |
| Impact flash | landX | landY | 80 | 40 | ellipse |
| Dust puff | landX | landY + 20 | 60 | 20 | ellipse |

## Draw Order Pipeline

1. Dust puff (if impact active)
2. Impact flash
3. Expanding impact ring
4. Pledge icon drop shadow
5. Pledge icon texture

## Fidelity Notes

- CSS `filter: drop-shadow` approximated with offset alpha duplicate (no blur pass)
- Radial gradients approximated via tinted stretched AA circles
- Mockup card 240x337 vs game 268x377 — motion distances scale with `cardScale`

## Verification Checklist

- [ ] Pledge hand card → slam plays once
- [ ] Enemy-applied pledge also animates
- [ ] No animation when pledge removed mid-flight
- [ ] Icon remains after animation at same placement as before
- [ ] Works for rotated hand cards
- [ ] Debug tab tunables adjust timing/scale live
- [ ] `dotnet build` succeeds
