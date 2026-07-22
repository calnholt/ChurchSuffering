# Waystation Collection Modal V2 Implementation Plan

## Document status

- **Status:** Approved design, ready for implementation.
- **Repository:** `ChurchSuffering`.
- **Runtime:** .NET 8.0, MonoGame DesktopGL, 1920x1080 virtual canvas.
- **Authoritative visual reference:** `mockups/waystation-collection-overlay-v1.html` as it exists in the working tree when implementation begins.
- **Replaces:** `ECS/Scenes/WayStationScene/WayStationSaintsMedalsModalSystem.cs` and its Saints-only ECS surface.
- **Save compatibility:** No migration or save-shape change is required.
- **Visual verification constraint:** Perform at most two complete snapshot verification passes. Each pass means capturing all required variants and manually comparing them with the HTML mockup. Ignore `tests/VisualBaselines/` entirely: do not read, compare, generate, update, accept, or verify baselines.
- **Implementation constraint:** Preserve all unrelated working-tree changes, including concurrent card, medal, and factory additions.

## Goal

Replace the Waystation Saints Medals modal with a new V2 collection modal named **The Crusader's Reliquary**. The new modal presents the player's unlocked cards, saints/medals, and equipment in three tabs while matching the HTML mockup as closely as MonoGame permits.

The completed feature must:

- Reproduce the mockup's fullscreen reliquary shell, typography, spacing, gradients, rounded controls, hover animations, scrolling regions, and collection meter.
- Use the canonical in-game card renderer for card previews rather than recreating the HTML card approximation.
- Read unlocks from the persistent player collection and Waystation weapon progression.
- Remain a read-only collection viewer; it must not alter decks, loadouts, equipped medals, or equipped equipment.
- Split controller, state, motion, and rendering responsibilities across focused ECS components and systems.
- Replace the legacy modal completely rather than leaving a dormant compatibility implementation.

## Fixed product decisions

These decisions are final and should not be revisited during implementation:

1. Only unlocked entries are displayed in all three tabs. There are no question-mark tiles or other locked placeholders.
2. Tab counts and the footer meter remain `unlocked / total`, where total means every canonical collectible in that category.
3. The Cards tab includes unlocked Sword, Dagger, and Hammer cards. Weapon ownership comes from `ClimbUnlockProgressionRules`, not `PlayerCollectionSave.cardIds`.
4. The existing bottom-right medal POI is repurposed as the Collection POI. Its current art is retained, its tooltip becomes `Collection`, and it opens the V2 modal.
5. The Collection POI is visible whenever at least one valid collection item or Waystation weapon is unlocked.
6. Every open resets to the mockup defaults: Cards tab, All filter, all scroll offsets at zero, White at the front of every card stack, and the first unlocked saint selected.
7. The top-right close control remains a clickable square `X` button.
8. The close entity receives `HotKey { Button = FaceButton.B, IsKeyboardMouseEnabled = false }`, matching the existing Waystation Penance close pattern. Escape is not bound and no keyboard glyph is shown.
9. Clicking the dimmed margin outside the modal does not close it.
10. Equipment tiles are informational only. Clicking them has no action.
11. No new art assets are required. Existing card, medal, equipment, slot-icon, POI, and font assets are reused.
12. The current HTML mockup is a design input and is not modified as part of this implementation.

## Collection rules

### Canonical catalog

Add a read-only collection catalog service or equivalent pure calculation surface. It must build the modal's data from the live factories each time the modal opens, so newly registered content appears without maintaining a second content list.

The catalog returns three ordered collections plus unlocked and total counts for each tab. It must not mutate saves, components, factories, or singleton state.

#### Cards

- The canonical total consists of all `CardFactory.GetAllCards()` entries whose card is non-null, `CanAddToLoadout` is true, and `IsToken` is false.
- This total includes the three weapon cards.
- An ordinary card is unlocked when its canonical ID occurs in `PlayerCollectionSave.cardIds`, case-insensitively.
- A weapon is unlocked when `ClimbUnlockProgressionRules.IsWeaponUnlocked(meta, weapon)` returns true.
- Do not infer Dagger or Hammer ownership from the card collection list.
- Ignore unknown or stale save IDs rather than displaying fallback cards or increasing the unlocked count.
- De-duplicate IDs case-insensitively.
- Sort displayed entries by card display name, case-insensitively, with canonical ID as a deterministic tie-breaker.

#### Saints

- The canonical total is every non-null entry returned by `MedalFactory.GetAllMedals()`.
- A saint is unlocked when its canonical medal ID occurs in `PlayerCollectionSave.medalIds`, case-insensitively.
- Do not use `waystation.purchasedMedalIds`; the new modal represents meta collection unlocks, not medals purchased during a run.
- Resolve biography content through `SaintBlurbDefinitionCache` using the canonical medal ID.
- Ignore unknown save IDs and de-duplicate case-insensitively.
- Sort by medal display name, case-insensitively, with canonical ID as a deterministic tie-breaker.

#### Equipment

- The canonical total is every non-null entry returned by `EquipmentFactory.GetAllEquipment()`.
- An item is unlocked when its canonical ID occurs in `PlayerCollectionSave.equipmentIds`, case-insensitively.
- Ignore unknown save IDs and de-duplicate case-insensitively.
- Group display entries into Head, Chest, Arms, and Legs columns in that order.
- For existing equipment, reproduce the order declared by the mockup within each slot. Any future equipment not present in that declared order is appended alphabetically by display name and then ID.

### Counts and empty states

- Each tab label displays `unlocked / total` beneath its title.
- The footer label is `Cards Unlocked`, `Communion of Saints`, or `Equipment Unlocked` according to the active tab.
- The footer meter fills to `unlocked / total`; when total is zero, its fill width is zero and the count is `0 / 0`.
- An empty active tab shows a centered, muted explanatory message in the content region rather than leaving a blank screen.
- The Collection POI visibility check uses the same catalog rules as the modal so the access gate cannot disagree with displayed content.

## ECS design

### Events and constants

Add `OpenWayStationCollectionModalEvent` and replace all publication/subscription of `OpenWayStationSaintsMedalsModalEvent`.

Replace the Saints-specific constants with collection names for:

- Modal root and input context.
- Shell/panel and close button.
- Three tabs.
- Five card filters.
- Card stack, saint tile, and equipment tile entity prefixes.
- Card, saint-list, saint-detail, and equipment scroll regions.

Use one modal input context with the same priority as the legacy modal. All modal controls and scroll blockers must be explicit context members.

### Components

Move the new state into a focused `WayStationCollectionModalComponents` source rather than adding more modal types to the general `Scenes.cs` file.

The component model must cover the following information without embedding behavior:

- `WayStationCollectionModalState`
  - Requested/animated visibility remains represented by the shared `ModalAnimation` component.
  - Active tab.
  - Active card filter.
  - Card, saint-list, saint-detail, and equipment scroll offsets.
  - Selected medal ID.
- `WayStationCollectionModalLayout`
  - Authoritative shell, header, tab row, body, footer, close, and active-panel rectangles.
  - Card filter/grid clip rectangles.
  - Saint wall/list/detail rectangles.
  - Equipment hall/header/content rectangles.
  - Footer meter and text anchors.
- Tab and filter action/presentation components containing their enum value and resolved bounds.
- Card-stack presentation containing card ID, stack bounds, current front color, and references or stable names for its three render-only card entities.
- Saint-tile presentation containing medal ID, bounds, and selected state.
- Equipment-tile presentation containing equipment ID, slot, bounds, and content measurements needed by drawing.
- Reusable motion/presentation state containing current and target hover progress, scale, fan angle, glow, or meter progress as appropriate.
- Marker components for the root, shell, close button, and scroll blockers where entity queries need explicit ownership.

Use enums for the three tabs and five card filters rather than string comparisons in frame logic.

### Controller: `WayStationCollectionModalSystemV2`

The controller owns state and interaction but performs no visual drawing.

Responsibilities:

- Subscribe to `LoadSceneEvent` and `OpenWayStationCollectionModalEvent`.
- Create the modal root with `ModalAnimation`, `InputContext`, `UIElement`, and collection state/layout components.
- Create/reconcile tabs, filters, close control, scroll blockers, and content-entry UI entities.
- Build a fresh catalog on each open and reconcile entities against it.
- Reset all state to the fixed open defaults before requesting visibility.
- Compute the authoritative 1920x1080 layout used by every display system.
- Route clicks from `UIElement.IsClicked`:
  - Switch tabs.
  - Change card filters.
  - Cycle a card's front color White to Red to Black to White.
  - Select unlocked saints and reset saint-detail scroll to zero.
  - Close from the X button/B hotkey.
- Apply scroll input only while the shared modal animation is fully visible and interactive.
- Use pointer location to choose the saint list versus detail region. On the other tabs, scroll their single content region.
- Support mouse wheel and right-stick scrolling with debug-editable step/speed values.
- Clamp offsets after every catalog, filter, tab, or layout change so a smaller result set cannot leave content offscreen.
- Synchronize child `UIElement` bounds after modal-animation transforms and disable all child interaction during enter/exit motion.
- Close immediately on leaving Waystation. Snapshot mode may keep the modal active for fixture rendering.
- Never pass or retain references to another `System`.

The controller must reconcile instead of blindly creating entities every frame. When an unlocked entry disappears from a malformed/replaced save, destroy or deactivate its presentation and render-preview entities so stale content is not retained.

### Motion system

Add a non-drawing motion system responsible for time-based presentation state.

- Hover values ease toward 1 while hovered and 0 otherwise.
- Tab lift/glow uses the mockup's 0.20-second timing.
- Medal and equipment icon scale uses the mockup's 0.22-second timing.
- Card fan and front-shadow response uses the mockup's 0.28-second cubic-out timing.
- Footer meter width animates over 0.45 seconds with the mockup's cubic-bezier character.
- Motion helpers must be deterministic across differing frame steps and unit-testable without a graphics device.
- Card color cycling may change render order immediately, but each layer's rotation/opacity settles through its motion component rather than being mutated during draw.

### Display-system split

Register separate debug-editable display systems and invoke them in a deterministic order from the Waystation draw path.

1. `WayStationCollectionChromeDisplaySystem`
   - Draws dim, shell shadow/fill/outline, header, title/subtitle, tabs, body background, footer, progress meter, and X control.
2. `WayStationCollectionCardsDisplaySystem`
   - Draws filter pills and publishes canonical render requests for visible card layers.
3. `WayStationCollectionSaintsDisplaySystem`
   - Draws the medal wall, saint tiles, selected detail, and saint empty state.
4. `WayStationCollectionEquipmentDisplaySystem`
   - Draws slot headers, equipment tiles, art, stats, and text.

Only the active content system draws content, while the chrome system always draws the shared shell. Wrap their ordered calls in one aggregate `WayStationCollectionModalV2.Draw` profiler scope and optionally retain child scopes for diagnosis.

Every display system must:

- Have `DebugTab` and reasonable `DebugEditable` values for layout-sensitive visual constants.
- Read resolved layout and presentation state from components.
- Keep `Draw()` state-free.
- Cache generated rounded masks/textures by dimensions and radius.
- Clear and dispose generated GPU resources in response to `DeleteCachesEvent` or normal disposal.
- Restore SpriteBatch/scissor state after clipped regions.

## Visual specification

The HTML mockup is authoritative when exact values below conflict with subjective interpretation.

### Shared shell

- Virtual resolution: 1920x1080.
- Shell inset: 40px on every side, producing a 1840x1000 modal.
- Background dim: black at 0.72 opacity.
- Shell fill: top-centered pale radial highlight over a near-black vertical gradient from approximately `(16,15,14)` to `(8,8,8)`, both near 0.97 opacity.
- Main border: 2px white at 0.50 opacity.
- Outer outline: 1px white at 0.15 opacity, offset 5px outside the main border.
- Shadow: black, approximately 40px below with a broad 90px falloff. Use layered cached masks/rectangles to approximate the CSS shadow without per-pixel work each frame.
- The existing shared `ModalAnimation` controls entrance/exit scale, alpha, dim, and input suppression. All content must transform consistently with the shell.

### Header and close

- Header top padding: 26px; horizontal padding: 32px.
- Title: `The Crusader's Reliquary`, centered, Title/New Rocker font, approximately 46px visual height, 3px tracking, and a soft white glow.
- Subtitle: `WAYSTATION COLLECTION`, centered below the title, Chakra Petch, approximately 12px, 6px tracking, muted warm gray.
- Close control: 44x44 at 26px from the header's top/right inset, black 0.55 fill, 2px white 0.50 border.
- Close hover: dark red fill, bright red border, and 16px-equivalent red glow over 0.20 seconds.
- The X remains centered in the control; the global HotKey system draws the B glyph according to the existing hotkey placement rules.

### Tabs

- Center Cards, Saints, and Equipment in one row with 14px gaps and 20px top margin.
- Each tab uses a top-arched/pill silhouette with rounded top corners and square bottom corners.
- Title font: Title/New Rocker, approximately 20px, 2px tracking.
- Count font: Chakra Petch, approximately 10px, 3px tracking, placed beneath the title.
- Default fill is a subtle top-to-bottom white gradient, with a 1px white 0.30 border and no bottom border.
- Hovered inactive tabs lift 3px and brighten.
- Active tabs use the stronger white gradient and upward white glow from the mockup.
- The body begins at the tab baseline behind a 1px white 0.35 divider.

### Cards tab

- Filter row is centered above the card grid with the `TYPE` label followed by All, Attack, Block, Prayer, and Weapon pills.
- Pills use a fully rounded radius, muted translucent fill, 1px border, and the mockup's active white-fill/dark-text treatment.
- Cards are sorted alphabetically after filtering.
- The grid is centered and wraps using approximately 34px horizontal and 30px vertical gaps.
- Size canonical cards to approximately 259x365, matching the mockup's `240x340` card at scale `1.08`; derive the scale from the live `CardGeometrySettings` rather than assuming its defaults never change.
- Each stack owns render-only White, Red, and Black card entities built through `EntityFactory.CreateCardFromDefinition(..., allowWeapons: true)`.
- Disable the preview cards' own UI interaction. The stack's dedicated UI entity owns hover and click bounds.
- Render back-left, back-right, then front through `CardRenderScaledEvent` with the content clip rectangle and `PreferCachedBase = true`.
- Resting back-card rotations are approximately -6 and +6 degrees. Hover rotations are approximately -11 and +11 degrees.
- Back cards use the mockup's dimmed treatment, approximately 0.60 alpha/brightness, while the front card remains full strength.
- Hover adds a dark front-card shadow and raises the stack's local draw priority so neighboring stacks do not paint over the fan.
- Only publish render events for stacks whose rotated conservative bounds intersect the card clip region.
- Card filters classify weapons before ordinary `CardType`; non-weapon Attack, Block, and Prayer entries go to their matching type. All includes every displayed entry.

### Saints tab

- Split body width 46% medal wall / 54% saint detail.
- Add a 1px white 0.16 divider between the two regions.
- Wall toolbar reads `MEDALS ACQUIRED` with the mockup's 18/28/10 padding and 4px tracking.
- Render a centered five-column medal grid with approximately 18px row and 12px column gaps.
- Use existing medal art at an 88px target diameter with a subtle border.
- Hover scales the icon to 1.10, brightens it, and adds a white ring/glow.
- The selected icon uses the mockup's white selection ring and glow; hover strengthens but does not replace it.
- Auto-select the first alphabetically sorted unlocked saint when the modal opens.
- Preserve the previous modal's detail content:
  - Medal/saint name.
  - Medal effect text.
  - Keyword explanation blocks.
  - Years on Earth.
  - Biography paragraphs.
  - Common patronages.
  - Associated prayer.
- Match the mockup's rounded 6px medal-effect panel, section labels, warm-gray body hierarchy, red prayer rule, italic years/prayer text, wrapping, and spacing.
- Keep saint detail independently scrollable from the medal wall.
- Publish the existing saint-information SFX only when the selected medal actually changes.

### Equipment tab

- Render four equal-width Head, Chest, Arms, and Legs columns with 1px white 0.12 dividers.
- Column headers remain fixed while the shared equipment content scrolls beneath them.
- Each header uses the existing slot icon at 40x40 and an uppercase 11px label with 4px tracking.
- Tile minimum height is 160px with 14px bottom gap, 16px padding, 16px art/body gap, and 6px corner radius.
- Art occupies a 128x128 contained region and uses `EquipmentArtService` for asset/fallback resolution.
- Render the equipment name in the Title/New Rocker font and a 10px color gem.
- Reuse `CardPalette` block-label/value colors and the same stacked BLOCK chip proportions as `EquipmentTooltipDisplaySystem`: 38px wide, 13px label slab, 25px value slab, 3px radius.
- Draw `EquipmentBase.MaxUses` pips, all filled because the collection view is not showing run consumption.
- Show `Text` when present; otherwise show `FlavorText` in the italic/muted treatment.
- Highlight registered keywords inline with the shared gold keyword color.
- Hover over 0.20/0.22 seconds: brighter border/fill, inset and outer glow, and art scale from 1.00 to 1.08.

### Keyword text runs

Extend `TooltipTextService` with an internal read-only API returning safe text runs marked as keyword/non-keyword.

- Reuse the existing private keyword registry, alias order, and word-boundary logic.
- Do not duplicate keyword lists in either display system.
- Preserve original visible casing and punctuation.
- Resolve overlaps deterministically using existing registry order and earliest match position.
- Filter unsupported glyphs before measuring/drawing with SpriteFont.
- Continue using `GetKeywordTooltipBlocks` for the explanatory saint keyword blocks.

## Input and scrolling

- The modal root blocks underlying Waystation interaction whenever requested visible or animating.
- Child controls are interactive only in the fully visible phase.
- Mouse/virtual-cursor Primary activates tabs, filters, card stacks, saint tiles, and the X button.
- Gamepad B activates the close entity through `HotKeySystem`.
- No explicit LB/RB tab shortcuts are added; gamepad users operate tabs through the existing virtual cursor.
- Mouse wheel scroll applies to the region under the pointer.
- Right-stick vertical input scrolls the active content; in Saints, use the detail by default when the pointer is in neither scroll region and a saint is selected.
- Scrolling uses scissor clips expressed in logical coordinates and converted through `Game1.Display.LogicalToRender`.
- Hidden tabs retain no interaction bounds.
- Scroll offsets reset on open and remain independent while the modal stays open.

## Legacy replacement and integration

Remove or replace all Saints-modal-specific surfaces:

- `WayStationSaintsMedalsModalSystem` source and registration.
- Legacy root/panel/close/tile components.
- Legacy constants and input context ID.
- `OpenWayStationSaintsMedalsModalEvent`.
- Old draw/profiler labels.
- Location-name suppression references.

Update integration points:

- `Game1` fields, construction, system registration, Waystation draw ordering, and profiling.
- `WayStationPoiDisplaySystem` tooltip, visibility predicate, and published open event.
- `LocationNameDisplaySystem` modal-root check.
- Snapshot fixture registration and documentation.

Do not change:

- Save-file structure or version.
- Collection unlock/progression rules.
- Card, medal, or equipment factory registrations except to consume their current output.
- Loadout, run inventory, equipment activation, medal purchase, or deck-edit behavior.
- The HTML mockup.

## Performance and lifecycle requirements

- Build/reconcile the catalog on modal open, not every draw call.
- Reconcile only when opening or when an explicit collection-changing event/scene refresh requires it; frame updates should consume cached presentation components.
- Create three preview card entities only for displayed unlocked card IDs, then reuse them across filtering and scrolling while the modal is open.
- Cull all offscreen card render events and avoid drawing scrolled-out medal/equipment tiles.
- Request cached canonical card bases for static preview cards.
- Keep all generated card preview entities outside gameplay zones and non-interactable so they cannot participate in deck, tooltip, hotkey, or combat queries.
- Mark modal-owned entities for Waystation cleanup or destroy them explicitly on scene exit; do not leak preview cards across scene reloads.
- Generated rounded textures must be cached by dimensions/radius and disposed on cache deletion.
- Avoid per-pixel texture creation, factory enumeration, or save cloning inside any `Draw()` path.

## Test plan

### Unit tests

Add focused tests for:

1. Catalog construction
   - Valid unlocked ordinary cards appear.
   - Unknown, duplicate, token, and non-loadout card IDs do not appear or affect counts.
   - Sword is present on a fresh profile.
   - Dagger and Hammer appear only when their Waystation progression keys are unlocked.
   - Medal unlocks come from `collection.medalIds`, not purchased medal IDs.
   - Equipment is grouped into the four slot columns.
   - Totals use canonical factory eligibility and remain greater than unlocked counts for partial collections.
   - Ordering is deterministic.
2. Card filtering
   - All returns every displayed card.
   - Weapon contains only unlocked weapon cards.
   - Attack excludes weapons.
   - Block and Prayer match canonical `CardType`.
3. Modal state
   - Opening resets tab, filter, selection, front colors, and scroll offsets.
   - Card clicks cycle White, Red, Black, White.
   - Filter/catalog changes clamp scroll offsets.
   - No unlocked saints produces the saint empty state without a selected ID.
4. Input setup
   - Close entity has `FaceButton.B` and `IsKeyboardMouseEnabled == false`.
   - Hidden/animating child controls are non-interactable.
   - POI visibility and modal catalog use the same unlock predicate.
5. Motion math
   - Hover values reach the same result within tolerance at 30, 60, and 120 FPS.
   - Card fan, icon scale, tab lift, glow, and meter animations hit their exact settled targets.
6. Keyword runs
   - Alias boundaries, casing preservation, overlap ordering, punctuation, and no-match input.

### Snapshot fixture

Add a dedicated `waystation-collection` fixture rather than overloading the existing Penance-only `waystation` fixture. This fixture is verified only through fresh captures compared manually with the HTML mockup; existing visual-baseline infrastructure is outside this feature's workflow.

Use a deterministic representative partial collection and unlock all three weapons. Pin the modal's shared animation and collection motion to settled values. Suppress pending rewards/dialogue and keep the cursor neutral except in explicit hover variants.

Required variants:

- `cards` — Cards/All at the top of the partial collection.
- `cards-hover` — A visible stack fully fanned at hover completion.
- `saints` — First unlocked saint selected with the top of its detail visible.
- `saints-hover` — A non-selected visible medal at hover completion while the selected detail remains visible.
- `equipment` — Four columns with representative unlocked items and no hover.
- `equipment-hover` — One visible equipment tile at completed hover scale/glow.

Add a script under `scripts/` that captures all six variants because this fixture has multiple variants. Document commands, output paths, the two-pass maximum, and the manual mockup-comparison workflow in `docs/display-snapshots.md`.

Ignore `tests/VisualBaselines/` completely. Do not inspect it for expected images, add files beneath it, compare captures against it, or use `--accept`/`--verify` for this fixture.

### Verification commands

Run from the repository root:

```bash
dotnet build
dotnet test tests/ChurchSuffering.Tests/ChurchSuffering.Tests.csproj
./scripts/capture-waystation-collection-snapshots.sh
```

Run at most two complete snapshot verification passes. One pass consists of running the capture script once for all six variants and manually inspecting every generated PNG against the HTML mockup. If the first pass exposes issues, make one correction and run the full script one final time; do not run a third pass. Visual-baseline state is ignored in both passes.

## Acceptance criteria

The implementation is complete when:

- The old Saints-only modal and all obsolete integration surfaces are removed.
- The existing Waystation POI opens the Collection modal for a fresh valid profile.
- Cards, Saints, and Equipment show only valid unlocked entries with correct unlocked/total counts.
- Unlocked weapon cards reflect Waystation progression and appear in the Weapon filter.
- Cards use the canonical in-game renderer in functional three-color stacks.
- All three tabs, filters, scrolling regions, selection, color cycling, hover motion, and B close behavior work without affecting underlying Waystation UI.
- The visual shell and every active tab closely match the HTML mockup, including rounded tabs/tiles, spacing, colors, typography, shadows, and hover endpoints.
- No display `Draw()` method mutates ECS state.
- No system holds another system reference.
- No save, loadout, deck, equipped medal, or equipped equipment state changes while browsing.
- Build, tests, and all six plain snapshot captures succeed.
- No unrelated working-tree changes are overwritten or reformatted.
