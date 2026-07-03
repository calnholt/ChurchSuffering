# Climb Scene UI Translation Plan

## Context

Source prototype: `webapp/index.html` and `webapp/src`.

Target: a real MonoGame Climb scene for `SceneId.Climb`, replacing the temporary `Location` scene compatibility draw path. Archetype: **D - Scene HUD**, with modal reuse for replacement selection, narrative events, reward overlays, and run abandon.

Virtual resolution: `Game1.VirtualWidth` x `Game1.VirtualHeight` = `1920x1080`.

## Files To Create

| File | Purpose |
| --- | --- |
| `ECS/Components/ClimbComponents.cs` | Central Climb preview state, slot presentation metadata, and entity markers for header, columns, slots, and loadout button. |
| `ECS/Events/ClimbEvents.cs` | UI intent events for shop slot select, encounter select, Climb event select, preview start/clear, and loadout open. |
| `ECS/Scenes/ClimbScene/ClimbSceneSystem.cs` | Parent scene system. Subscribes to `LoadSceneEvent`, adds/removes Climb child systems, sets map music, clears pending battle. |
| `ECS/Scenes/ClimbScene/ClimbBackgroundDisplaySystem.cs` | Draws `desert_background_location` full-screen cover plus dim tint. |
| `ECS/Scenes/ClimbScene/ClimbHeaderLayoutSystem.cs` | Owns header entity bounds, timeline/resource/loadout button `UIElement.Bounds`, and preview state clearing when needed. |
| `ECS/Scenes/ClimbScene/ClimbHeaderDisplaySystem.cs` | Draws header chrome, timeline, resource bar, and weapon-art button. |
| `ECS/Scenes/ClimbScene/ClimbColumnLayoutSystem.cs` | Owns column and slot entity creation/bounds. Hides Events column when no active Climb events exist. |
| `ECS/Scenes/ClimbScene/ClimbColumnDisplaySystem.cs` | Draws columns, compact shop/event/encounter cards, and primitive resource/time icons. |
| `ECS/Scenes/ClimbScene/ClimbToastDisplaySystem.cs` | Draws short feedback for unaffordable shop slots or unavailable timed events. |

## Files To Modify

| File | Change |
| --- | --- |
| `Game1.cs` | Register/draw `ClimbSceneSystem` for `SceneId.Climb`; remove the temporary Location draw fallback once the scene exists. |
| `ECS/Scenes/UIElementEventDelegateSystem.cs` | Route Climb button entities through `UIElementEventDelegateService` or existing delegate hooks, never `MouseState`. |
| `ECS/Data/Save/SaveFile.cs` | Use existing Climb save state as the display source; add only display-required state that cannot be derived. |
| `ECS/Services/ClimbRuleService.cs` | Expose pure helpers for timeline, slot expiration, preview projection, and active event filtering. |
| `ECS/Services/ClimbShopService.cs` | Expose slot view data for shop item title, label, cost, sold state, and replacement mode. |
| `ECS/Services/ClimbEncounterService.cs` | Expose encounter slot view data and reward summaries. |
| `ECS/Diagnostics/Snapshots/DisplaySnapshotRegistry.cs` | Register Climb snapshot fixtures. |
| `ECS/Diagnostics/Snapshots/Fixtures/ClimbSnapshotFixture.cs` | Add no-events, active-events, hover-preview, sold-shop-slot, reward-modal, and replacement-modal variants. |

## Implementation Steps

1. Create `ClimbSceneSystem` using `LocationSceneSystem` as the parent-scene pattern. It should add child systems once per Climb load, remove them on `DeleteCachesEvent`, and set music to `MusicTrack.Map`.
2. Add a `ClimbPreviewState` component on a singleton-like scene entity. Fields: `SourceSlotId`, `Amount`, `IsActive`, projected resources, projected used time, projected remaining time, and `HashSet<string> WouldVanishSlotIds`.
3. Add layout systems that create entities for every interactive object with `Transform` and `UIElement`: shop slots, encounter slots, active event slots, and the 67x67 weapon-art button.
4. Draw the full scene from back to front: background, header chrome, timeline, resource bar, columns, slot cards, hover/sold/expired overlays, toast, then global modals.
5. Wire hover through cursor/UIElement events into `ClimbPreviewStartedEvent` and `ClimbPreviewClearedEvent`. Draw systems read the preview component only.
6. During active preview, block clicks for non-source slots whose id is in `WouldVanishSlotIds`.
7. Wire shop slots to `ClimbShopService`; replacement slots open selectable `CardListModal` using eligible non-weapon deck cards.
8. Wire encounter slots to `ClimbEncounterService.TryQueueEncounter`; successful queueing routes to battle.
9. Wire event slots to the Climb event runtime; successful selection advances Climb time and opens `NarrativeEventModalDisplaySystem`.
10. Add snapshots and run `dotnet build`.

## DebugEditable Properties

All display systems with `Draw()` need `[DebugTab("Climb ...")]`.

| System | Property | Default | Step |
| --- | --- | ---: | ---: |
| Background | `BackgroundDimAlpha` | `0.22f` | `0.01f` |
| Header | `HeaderHeight` | `90` | `1` |
| Header | `HeaderPaddingX` | `32` | `1` |
| Header | `HeaderPaddingTop` | `10` | `1` |
| Header | `HeaderPaddingBottom` | `12` | `1` |
| Header | `HeaderGap` | `16` | `1` |
| Header | `WeaponButtonSize` | `67` | `1` |
| Header | `TimelinePaddingX` | `10` | `1` |
| Header | `TimelinePaddingY` | `8` | `1` |
| Header | `TimelineSlotGap` | `3` | `1` |
| Header | `TimelineLabelFontScale` | `0.07f` | `0.01f` |
| Header | `TimelineValueFontScale` | `0.10f` | `0.01f` |
| Header | `ResourceLabelFontScale` | `0.09f` | `0.01f` |
| Header | `ResourceAmountFontScale` | `0.14f` | `0.01f` |
| Columns | `ColumnsTop` | `114` | `1` |
| Columns | `ColumnsMaxWidth` | `1500` | `1` |
| Columns | `ColumnsGap` | `20` | `1` |
| Columns | `ColumnsPaddingX` | `32` | `1` |
| Columns | `ColumnPadding` | `16` | `1` |
| Columns | `ColumnRadius` | `4` | `1` |
| Columns | `ColumnTitleFontScale` | `0.15f` | `0.01f` |
| Columns | `ColumnSubtitleFontScale` | `0.09f` | `0.01f` |
| Slots | `SlotGap` | `6` | `1` |
| Slots | `SlotRingPadding` | `10` | `1` |
| Slots | `CompactPaddingX` | `10` | `1` |
| Slots | `CompactPaddingY` | `8` | `1` |
| Slots | `CompactRadius` | `3` | `1` |
| Slots | `CompactTitleFontScale` | `0.10f` | `0.01f` |
| Slots | `CompactBadgeFontScale` | `0.07f` | `0.01f` |
| Slots | `CompactMetaFontScale` | `0.08f` | `0.01f` |
| Slots | `EnemyPortraitHeight` | `148` | `1` |
| Slots | `MetaBlockMinHeight` | `36` | `1` |
| Icons | `ResourceIconSize` | `18` | `1` |
| Icons | `CompactResourceIconSize` | `12` | `1` |
| Icons | `TimelineHourglassWidth` | `8` | `1` |
| Icons | `TimelineHourglassHeight` | `11` | `1` |
| Icons | `CompactHourglassWidth` | `10` | `1` |
| Icons | `CompactHourglassHeight` | `14` | `1` |

## Color Palette

| CSS Source | MonoGame |
| --- | --- |
| `--black-0: #050505` | `new Color(5, 5, 5)` |
| `--black-1: #0a0a0a` | `new Color(10, 10, 10)` |
| `--black-2: #141414` | `new Color(20, 20, 20)` |
| `--black-3: #1e1e1e` | `new Color(30, 30, 30)` |
| `--black-4: #2a2a2a` | `new Color(42, 42, 42)` |
| `--white-1: #ffffff` | `new Color(255, 255, 255)` |
| `--white-2: #f0ece6` | `new Color(240, 236, 230)` |
| `--white-3: #c8c0b8` | `new Color(200, 192, 184)` |
| `--red-3: #c41e3a` | `new Color(196, 30, 58)` |
| `--red-2: #ff4d5e` | `new Color(255, 77, 94)` |
| `--red-dim: #a00000` | `new Color(160, 0, 0)` |
| `--red-glow: rgba(255, 77, 94, 0.65)` | `new Color(255, 77, 94) * 0.65f` |
| `--panel-border: rgba(255, 255, 255, 0.85)` | `Color.White * 0.85f` |
| `--shop-bg: rgba(255, 255, 255, 0.1)` | `Color.White * 0.10f` |
| `--shop-border: rgba(255, 255, 255, 0.55)` | `Color.White * 0.55f` |
| `--mystery-bg: rgba(0, 0, 0, 0.35)` | `Color.Black * 0.35f` |
| `--mystery-border: rgba(0, 0, 0, 0.75)` | `Color.Black * 0.75f` |
| `--enemy-bg: rgba(196, 30, 58, 0.1)` | `new Color(196, 30, 58) * 0.10f` |
| `--enemy-border: rgba(196, 30, 58, 0.45)` | `new Color(196, 30, 58) * 0.45f` |
| card fill `rgba(8, 8, 8, 0.92)` | `new Color(8, 8, 8) * 0.92f` |
| header fill `rgba(10, 10, 10, 0.82)` | `new Color(10, 10, 10) * 0.82f` |
| shadow `rgba(0, 0, 0, 0.45)` | `Color.Black * 0.45f` |

## Pixel Position Reference

At 1920x1080 with active Events column:

| Element | X | Y | W | H | Notes |
| --- | ---: | ---: | ---: | ---: | --- |
| Background image | `0` | `0` | `1920` | `1080` | Cover crop, center top. |
| Header | `0` | `0` | `1920` | `90` | Prototype CSS totals 90px: 67px content plus 22px vertical padding plus 1px border. |
| Header content | `32` | `10` | `1856` | `67` | Horizontal flex area. |
| Weapon button | `1821` | `10` | `67` | `67` | Top-right, circular crop of current weapon art. |
| Resource bar | `1660` | `10` | `145` | `67` | Width may grow with resource digits; reserve before weapon button. |
| Timeline | `32` | `10` | `1612` | `67` | Remaining header width after gaps. |
| Timeline labels row | `42` | `18` | `1592` | `20` | Used, preview delta, remaining. |
| Timeline track | `42` | `51` | `1592` | `16` | 40 equal grid slots, 3px gaps. |
| Columns wrapper | `210` | `114` | `1500` | `918` | Centered; top is header height plus 24px. |
| Shop column | `210` | `114` | `486` | dynamic | `(1500 - 2*20) / 3`. |
| Encounter column | `716` | `114` | `486` | dynamic | 20px gap. |
| Events column | `1224` | `114` | `486` | dynamic | Hidden when no active Climb events exist. |
| Column inner | `column.X + 16` | `130` | `454` | dynamic | 16px padding. |
| Column title | `inner.X` | `130` | `454` | `31` | Includes 8px bottom padding and 2px underline. |
| Column subtitle | `inner.X` | `167` | `454` | `14` | Uppercase. |
| Slots start | `inner.X` | `189` | `454` | dynamic | 6px vertical gap. |
| Shop compact slot | `inner.X` | varies | `454` | `58` | Title/badge row plus meta row. |
| Event compact slot | `inner.X` | varies | `454` | `52` | Glyph, title, time block. |
| Encounter compact slot | `inner.X` | varies | `454` | `210` | 148px portrait plus 60px details. |

If Events is hidden, keep shop and encounter columns at the same `486px` width and center the two-column group inside the 1500px cap: group width `992`, x `464`.

## Draw Order Pipeline

1. `ClimbBackgroundDisplaySystem`: background texture, dim overlay.
2. `ClimbHeaderDisplaySystem`: header shadow, header fill, bottom border.
3. Header timeline panel, labels, shop markers, hourglass icons, preview pulse overlay.
4. Resource bar fill, border, labels, red/white/black resources.
5. Weapon-art circular button, hover border.
6. `ClimbColumnDisplaySystem`: column gradient strips, borders, titles, subtitles.
7. Slot ring hover backgrounds.
8. Compact cards: fill, border, portrait/image area, text, resource/time meta blocks.
9. Slot state overlays: sold, expired, unavailable, preview source, would-vanish pulse.
10. Toast/reward feedback.
11. Existing global overlays: card list, narrative event modal, reward modal, quit/run abandon overlay.

## Helper Method Signatures

```csharp
private ClimbHeaderLayout ComputeHeaderLayout(int vw, int vh);
private ClimbColumnsLayout ComputeColumnsLayout(int vw, int vh, bool showEvents);
private IReadOnlyList<Rectangle> ComputeSlotRects(Rectangle columnInner, IReadOnlyList<ClimbSlotView> slots);
private void DrawBorder(Rectangle rect, Color color, int thickness);
private void DrawVerticalGradient(Rectangle rect, Color top, Color bottom, int strips = 16);
private void DrawResourceIcon(Vector2 position, ClimbResourceType type, int size, Color color);
private void DrawHourglassIcon(Rectangle rect, Color frame, Color sand, bool filled);
private void DrawShopMarkerIcon(Rectangle rect, Color color);
private bool WouldBlockClickDuringPreview(string slotId, ClimbPreviewState preview);
```

## Layout Modes

| Mode | Rule |
| --- | --- |
| No active events | Do not create event slot entities; hide Events column; center Shop and Encounters as a two-column group. |
| Active events | Create up to three event slot entities and draw Events column. |
| Hover preview | Source slot gets raised border/glow; timeline shows clamped delta; vanishing slots pulse and are click-blocked unless source. |
| Sold shop slot | Keep slot in place, dim card, show sold state, do not reroll visually until refresh. |
| Resources-only reward modal | Reward modal must show resource icons and allow dismissal without requiring a deck offer. |
| Replacement modal | Existing card-list modal opens selectable mode with eligible non-weapon deck cards only. |

## Input Contracts

- Do not use `MouseState` or `GamePad`.
- Every clickable surface is an entity with `Transform` and `UIElement`.
- Simple clicks should route through `UIElementEventDelegateService`.
- Draw methods read component state and must not mutate slot, save, or preview state.
- Layout/update systems own `UIElement.Bounds` and any entity creation/deactivation.

## Verification Checklist

Run:

```bash
dotnet run -- snapshot climb-no-events
dotnet run -- snapshot climb-active-events
dotnet run -- snapshot climb-hover-preview
dotnet run -- snapshot climb-sold-shop-slot
dotnet run -- snapshot climb-encounter-reward-modal
dotnet run -- snapshot climb-replacement-modal
dotnet build
```

Manual checks:

- Weapon-art button opens display-only loadout modal.
- Shop replacement opens selectable deck list; close/cancel does not spend or mutate.
- Hovering a time-advancing slot previews used and remaining time.
- Non-source vanishing slots cannot be clicked during preview.
- Event column disappears when no active event exists.
- Event selection advances time and opens the narrative event modal.
- Run abandon text says Run abandon / Abandon run, not Quest abandon.

## Fidelity Notes

- CSS letter spacing has no SpriteFont equivalent; keep `letter-spacing` as a note and tune text scale/position instead.
- Browser `backdrop-filter: blur(8px)` is not required; use the header fill plus shadow.
- CSS transitions can be instant state swaps in MonoGame. The vanish pulse may use a sine alpha in `Draw()`.
- Enemy portrait CSS uses `object-fit: cover` and top-center anchoring for compact cards. Use `SpriteBatch.Draw` source-rect cropping to preserve that behavior.
- Gradients can be 16 horizontal strips lerped between the listed top and bottom colors.
