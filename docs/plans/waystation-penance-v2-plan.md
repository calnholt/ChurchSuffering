# Way Station Penance V2 Implementation Plan

## Document status

- **Status:** Approved design, ready for implementation.
- **Repository:** `ChurchSuffering`.
- **Runtime:** .NET 8.0, MonoGame DesktopGL, 1920x1080 virtual canvas.
- **Authoritative visual reference:** `mockups/waystation-climb-penance-v1.html`.
- **Save compatibility:** None required. Development and verification assume a fresh save created with `dotnet run -- new`.
- **Visual verification constraint:** Do not generate or accept snapshot baselines. Capture and inspect one settled Penance XII screenshot exactly twice.

## Goal

Replace the Easy/Normal/Hard climb setup with a 24-level, per-weapon Penance system and redesign the Way Station climb modal as the fullscreen experience in the HTML mockup.

The finished work must cover all of the following as one coherent feature:

- The fullscreen modal layout, typography, colors, gradients, shadows, border radii, glow, hover states, and animation choreography match the rendered mockup.
- Sword, Dagger, and Hammer each own independent Penance progression.
- Completing climbs unlocks weapons and Penance levels sequentially.
- The selected Penance is persisted into the active climb and all five accumulated effects are applied deterministically.
- The old tier model and its process-global run-setup singleton are removed rather than retained as a compatibility layer.
- UI responsibilities are split into individual ECS presentation/display systems with debug-editable visual controls.
- The Way Station scene receives cursor-driven parallax and the mockup's animated saturation, contrast, zoom, dim, vignette, and grain treatment.

## Fixed product decisions

These decisions are final and should not be revisited during implementation:

1. Switching weapons always selects that weapon's highest unlocked Penance. It does not restore a remembered lower choice from the mockup's JavaScript state.
2. Sword begins unlocked at Penance 0. The first Sword clear unlocks Dagger at 0; the first Dagger clear unlocks Hammer at 0.
3. Clearing a weapon at its current highest unlocked level unlocks the next level, capped at 24. Clearing a lower level does not advance progression.
4. Abstinence removes initial resources in fixed Black, White, Red order.
5. Reparation draws replacement cards only from the unlocked collection, excludes Starter-rarity cards and cards present in the generated starting deck, and uses only Thorned, Scorched, Cursed, Frozen, or Brittle as its negative modification.
6. Difficulty achievements become five Penance milestones per weapon at levels 0, 6, 12, 18, and 24, using generic systematic names.
7. `test-fight` accepts a numeric Penance from 0 through 24. Easy/Normal/Hard aliases are not retained.
8. The one feature snapshot has all three weapons unlocked through Penance XII, with Hammer selected at XII.

## Penance definitions and accumulated rules

### Fixed unlock order

The order is shared by every weapon and must remain centralized in one authoritative rules type:

1. Fasting
2. Reparation
3. Abstinence
4. Mortification
5. Fasting
6. Reparation
7. Abstinence
8. Mortification
9. Reparation
10. Penitential Pilgrimage
11. Fasting
12. Mortification
13. Reparation
14. Fasting
15. Reparation
16. Mortification
17. Abstinence
18. Reparation
19. Penitential Pilgrimage
20. Mortification
21. Reparation
22. Fasting
23. Reparation
24. Mortification

At Penance level `N`, the first `N` entries are active. This yields the following maximum stack counts:

- Fasting: 5
- Mortification: 6
- Abstinence: 3
- Penitential Pilgrimage: 2
- Reparation: 8

### Fasting

- The unburdened player maximum is 25 HP.
- Each stack removes 1 maximum HP: `25 - fastingStacks`.
- Five stacks therefore produce 20 maximum HP.
- Apply the resolved maximum when the run player is created. Normal battle-to-battle recovery still restores the player to that reduced maximum.
- Persist only the selected Penance level; derive the HP value from the rules table instead of storing redundant effect totals.

### Mortification

- The unburdened enemy-health modifier remains 0.70.
- Each stack adds 0.05: `0.70 + 0.05 * mortificationStacks`.
- Six stacks therefore produce a 1.00 modifier.
- Continue using the existing integer rounding behavior for enemy maximum/current HP.
- Apply Mortification before the existing climb-time health bonus.
- The climb-time bonus keeps its existing eight-time cadence. It must not inherit the longer shop interval from Penitential Pilgrimage, because Pilgrimage changes the shop cycle and total climb length, not enemy health scaling.

### Abstinence

- The unburdened climb starts with one Red, one White, and one Black resource.
- Stack 1 removes Black, stack 2 removes White, and stack 3 removes Red.
- This modifies the initial run-level `ClimbResourceSave`; it is not reapplied at the start of every battle.

### Penitential Pilgrimage

- The base shop refresh interval is 8 climb time.
- Each stack adds one: `8 + pilgrimageStacks`.
- A climb always contains four shop cycles, so its total length is `shop refresh interval * 4`: 32 pips at zero stacks, 36 at one stack, and 40 at two stacks.
- Update shop refresh decisions, next-refresh projections, Climb Point refresh thresholds, header/timeline markers, and relevant Climb previews to use the run-specific interval.
- Keep a separate base cadence for enemy climb-time HP scaling so the Penance does not accidentally grant a compensating benefit.

### Reparation

For each active stack, replace one distinct entry in the generated 20-card starting deck.

Replacement target rules:

- Select distinct loadout entries marked as starters.
- Preserve each selected entry's stable `entryId`.
- Set the replacement entry's `isStarter` to false and leave `countsAsTraded` false.
- Do not replace the equipped weapon or create/remove deck slots.

Incoming-card rules:

- The base card ID must be present in `PlayerCollectionSave.cardIds`.
- The card must exist, be loadout-eligible, be eligible for the selected weapon, and be neither a weapon nor a token.
- Its rarity must not be `Rarity.Starter`.
- Its base card ID must not occur anywhere in the originally generated starting deck. Evaluate this exclusion against the original deck before applying any Reparation replacements.
- Respect the deck's maximum-two-copy rule across all replacement results.
- Choose a valid printed color using the same Red/White/Black card-key conventions as the existing starting-deck and reward generators.

Negative-modification rules:

- Apply exactly one of Thorned, Scorched, Cursed, Frozen, or Brittle to every replacement.
- Persist the chosen restriction name directly on the replacement `LoadoutCardEntry` so normal run-deck hydration recreates it.
- Do not include Colorless, Sealed, Hex, or any other restriction in this roll.

Determinism and failure behavior:

- Use a dedicated, stable Reparation salt combined with the run seed. Do not use `Random.Shared`.
- Identical weapon, seed, collection, and Penance inputs must produce identical targets, incoming card keys, and restrictions.
- Choose targets and incoming candidates using stable ordering before RNG indexing so dictionary/enumeration order cannot change results.
- If malformed external data leaves too few valid cards, apply as many legal replacements as possible without violating deck rules; never insert an invalid card or throw during departure.

## Save and public model changes

### Shared types

- Keep `StartingWeapon`, but move it out of the deleted singleton file into a shared run-setup/domain location.
- Add `PenanceType` with the five named modifications.
- Add a pure Penance rules/calculation surface that exposes:
  - `MaxLevel = 24`.
  - The fixed order.
  - Level clamping.
  - Per-type stack counts.
  - Player maximum HP.
  - Enemy-health modifier.
  - Initial resources.
  - Shop refresh interval.
  - Reparation count.
- Prefer one immutable calculation/result value for consumers rather than letting each consumer recount the order independently.

### Way Station meta progression

Replace difficulty completion tuples with a dictionary on `WayStationMetaSave`:

```text
highestPenanceByWeapon: Dictionary<string, int>
```

Rules for this dictionary:

- Keys use canonical lower-case IDs: `sword`, `dagger`, and `hammer`.
- Presence of a key means the weapon is unlocked.
- Sword is ensured at value 0 for a fresh profile.
- Values are clamped to `0..24` whenever meta state is cloned or written.
- Dagger is added at 0 after a Sword completion.
- Hammer is added at 0 after a Dagger completion.
- Completing the current highest level increments that weapon's value by one when below 24.
- Continue retaining `climbAttempts` and `climbCompletions` for generic progression/dialogue behavior.
- Remove `CompletedClimbSave` and `completedClimbs`; do not maintain a shadow copy of the old tier records.

### Active climb state

- Replace `ClimbSaveState.difficulty` with `ClimbSaveState.penanceLevel`.
- Clamp saved active-run levels to `0..24`.
- Persist the level before creating run entities so player, enemies, shops, UI, and reload paths all derive from the same authoritative run state.
- Clone `penanceLevel` in every `SaveCache` snapshot/clone path.
- Increment `SaveFile.CURRENT_VERSION`; do not add a migration or sanitizer for old saves.

### Run setup component

Delete `WayStationRunSetupSingleton` and add a `DontDestroyOnLoad` run-setup entity/component containing:

- `SelectedWeapon`, defaulting to Sword.
- `SelectedPenanceLevel`, defaulting to 0.
- A derived canonical weapon ID if useful to callers.

The modal controller is the normal writer. Departure reads the component and passes explicit values into save/run construction. Battle and reload consumers read the persisted `ClimbSaveState`, not the transient component, so restarting the application cannot lose active-run effects.

### Events, diagnostics, and achievements

- Replace `ClimbCompletedEvent.Difficulty` with `PenanceLevel`.
- Update final-boss completion publishing and meta progression recording to use the completed climb's weapon and level.
- Change `TestFightLaunchOptions.Difficulty` to `PenanceLevel` and parse exactly one integer from 0 through 24.
- Update usage/errors and `docs/build-run.md` examples to `dotnet run -- test-fight hammer skeleton 24` or another numeric level.
- Apply all five effects in test-fight setup, including deterministic Reparation on regenerated test decks.
- Update `unlock-run-setup` so all three dictionary entries are present at 24.

Create fifteen weapon milestones using IDs and display names in this pattern:

```text
sword_penance_0   / Sword Penance 0
sword_penance_6   / Sword Penance VI
sword_penance_12  / Sword Penance XII
sword_penance_18  / Sword Penance XVIII
sword_penance_24  / Sword Penance XXIV
```

Repeat the pattern for Dagger and Hammer. Descriptions say to complete a climb with that weapon at the named Penance. A completion at or above the required level qualifies. Keep `first_ascent` and `veteran_climber` unchanged as generic completion achievements.

Grid positions:

- Sword: row 0, columns 3 through 7.
- Dagger: row 1, columns 5 through 9.
- Hammer: row 2, columns 3 through 7.

## ECS ownership and system split

### Controller

Retain `WayStationClimbSettingsModalSystem` as the modal's controller, layout authority, and draw coordinator. It should no longer contain all rendering code.

Controller responsibilities:

- Open/close/depart requests and modal input context.
- Create/reconcile the root, close, weapon, Penance-node, tally, and depart entities.
- Read meta progression and normalize the `RunSetup` selection.
- Compute authoritative 1920x1080 base bounds matching the mockup.
- Route clicks through entity `UIElement` state.
- Publish explicit selection/motion events when weapon or level changes.
- Gate interaction until entrance motion reaches the settled state.
- Call child display systems in a deterministic draw order.
- Continue fast-departing Sword/Penance 0 when Dagger has not yet unlocked.

The controller must not poll raw mouse or gamepad state. Use existing `CursorEvents`/`UIElement` interaction infrastructure.

### Presentation components

Create focused data-only components for:

- Modal root/state.
- Masthead/title/rule.
- Weapon choice, including weapon identity and highest unlocked level.
- Penance meter track/fill.
- Penance node, including level, unlocked/active/current state, and tooltip text.
- Tally chip, including Penance type, current count, displayed count, and tooltip text.
- Footer/depart/close presentation.
- Per-element motion state: phase, elapsed time, delay, opacity, offset, scale, width progress, glow, and count-bump progress as applicable.

Do not use public static snapshots for any of this state. Each stateful output must have one owning system.

### Motion system

Add `WayStationPenanceMotionSystem` as the sole writer of animation state.

- Advance all timers in `Update`; `Draw` methods remain state-free.
- Consume selection-change requests/events and initialize element transitions.
- Reuse `ModalAnimation` only for high-level Hidden/Entering/Visible/Exiting lifecycle if useful; do not use the generic shell-scale renderer because the V2 mockup has independent choreography.
- Add debug-editable timings, offsets, stagger delays, scale peaks, glow intensities, and easing controls under a dedicated debug tab.
- Implement CSS-compatible cubic-bezier evaluation for:
  - `ease-rise = cubic-bezier(0.16, 1, 0.3, 1)`.
  - `ease-slam = cubic-bezier(0.2, 1.35, 0.4, 1)`.
- Preserve overshoot from `ease-slam`; do not clamp the eased result to 0–1 before applying scale.

### Display systems and draw order

Create and register these focused display systems:

1. `WayStationPenanceBackdropDisplaySystem`
2. `WayStationPenanceMastheadDisplaySystem`
3. `WayStationPenanceWeaponDisplaySystem`
4. `WayStationPenanceTrackDisplaySystem`
5. `WayStationPenanceNodeDisplaySystem`
6. `WayStationPenanceTallyDisplaySystem`
7. `WayStationPenanceFooterDisplaySystem`

The modal coordinator draws them in that order. Standard text tooltips draw later through the existing global tooltip system.

Every display system must:

- Have a readable `DebugTab`.
- Expose practical `DebugEditable` geometry, font scale, color-channel, radius, shadow, glow, opacity, and hover controls rather than embedding unexplained magic numbers.
- Use `Step = 0.01f` for font scales and ordinary float controls.
- Read presentation/motion data without mutating it.
- Render only when the Way Station/Snapshot scene and modal lifecycle permit it.

## Visual specification

### Layout

Translate the shipped portion of the HTML mockup directly to the 1920x1080 virtual canvas. Page header, replay buttons, viewport scaler, and other browser-only chrome are not part of the game.

The modal uses the mockup's fullscreen four-row grid:

- Outer padding: 26 top, 90 horizontal, 24 bottom.
- Rows: 150 masthead, 380 weapons, flexible track/tally area, 108 footer.
- Close button: 46x46 at top 26, right 30.
- Weapon cards: 250x330, centered, 30 gap.
- Weapon art: 216 high with 14 top margin and contain scaling.
- Track label/frame/tally vertical gap: 30.
- Track frame: 30 vertical and 34 horizontal padding.
- Nodes: 30x30 diamonds with 17 gap, giving a 47-pixel pitch.
- Track fill begins at the first node center and ends at the selected node center.
- Roman milestone ticks appear below levels 6, 12, 18, and 24.
- Footer contains the 64-pixel-high Depart button and the selection summary line.

### Typography and text

- New Rocker: title, weapon names, Depart, and close X.
- Chakra Petch: labels, records, summary text, tally names, and tooltips.
- Grenze: Roman/Arabic node numerals, best-Penance values, and tally counts. Expose Grenze through `FontSingleton` or load it once through the display composition root.
- Match the mockup's font sizes through native SpriteFont scale defaults and expose those scales for debug tuning.
- Follow the repository ASCII-only SpriteFont constraint:
  - Use `-` instead of the em dash for an empty record.
  - Use `xN` instead of the multiplication glyph in tally counts/tooltips.
  - Use ordinary hyphens in locked-node titles and descriptions.

### Colors, borders, radii, and shadows

Use the mockup's exact default palette:

- Ink `#050606`
- Charcoal `#101112`
- Iron `#242628`
- Bone `#eee9df`
- Dim bone `#a8a399`
- Ash `#6f6b64`
- Blood `#c41e3a`
- Dim blood `#7c1226`
- Dark blood `#3a0812`
- Gold `#e8c45d`

Specific treatments:

- Weapon cards, close, and Depart remain square-cornered like the HTML.
- Tally chips use a fully pill-shaped radius and animated width.
- Tooltips use the shared 8-pixel rounded black container.
- Selected weapons use a blood border, one-pixel outer ring, 36-pixel red glow, and bottom radial blood wash.
- Current nodes use the four-pixel outer red ring and stronger 20-pixel glow.
- Depart retains its lower black shadow, red ornamental side rules, and blood hover background/glow.
- Use layered translucent masks/rectangles for soft glows and shadows; cache rounded masks through `ImageAssetService`.

### Tooltips

Use `TooltipType.Text`, the existing rounded tooltip renderer, and screen-clamped placement.

Unlocked node:

```text
Penance XII - Mortification
Enemies have 5% more HP.
```

Locked node:

```text
Penance XIII - Locked
Conquer Penance XII with the Hammer to unlock.
```

Tally chip:

```text
Mortification x3
Enemies have 5% more HP. (applied 3 times)
```

Use the exact effect descriptions from the mockup, adjusted only for the agreed Abstinence wording and ASCII constraints.

## Animation specification

### Modal entrance

The complete interactive entrance lasts 1.45 seconds. Elements are non-interactable until it settles.

- 0ms: scene filter/dim begins fading over 500ms; scene zooms from 1.02 to 1.04 over 800ms.
- 60ms: title settles from scale 1.35 and eight-pixel blur over 550ms using `ease-slam`.
- 160ms: first weapon rises from +46px over 600ms using `ease-rise`; subsequent weapons begin 90ms apart.
- 260ms: masthead rule unfurls horizontally from zero over 500ms.
- 420ms: track label fades upward over 450ms.
- 480ms: track frame unfurls from horizontal scale 0.12 over 550ms.
- 560ms: nodes arrive from scale 0.2 and +10px; each starts 18ms after the previous and lasts 400ms.
- 650ms: red fill begins its 500ms sweep from zero to the selected level.
- 820ms: active tally chips enter, staggered 70ms apart; the unburdened line uses the same phase when level 0 is selected.
- 940ms: footer rises over 500ms.
- 950ms: close X fades in over 300ms.

Approximate title blur by drawing controlled translucent offset samples if applying a text-only blur pass would complicate SpriteBatch ownership. The settled appearance and timing remain authoritative.

### Modal exit

The exit lasts 470ms and disables interaction immediately.

- Masthead travels -30px with a four-pixel blur over 300ms.
- Close fades over 220ms.
- Weapon cards drop +36px over 320ms with a 45ms index stagger.
- Track zone drops/fades over 300ms after an 80ms delay.
- Footer drops/fades over 260ms after a 40ms delay.
- Scene filter, zoom, and dim release toward their base values concurrently.
- Hide/deactivate UI bounds only after exit completion.

### Weapon selection

- Ignore a click on the already selected weapon.
- Set the selected level to the new weapon's highest unlocked level.
- Flash the new card from the three-pixel/70-pixel burst to its settled selected glow over 500ms.
- Pop its art from 1.0 to 1.08 and back over 450ms using `ease-slam`.
- Ripple node locked/unlocked visuals left-to-right at 10ms per node.
- Recompute active/current nodes, fill target, tally counts, tooltips, and footer summary from the new level.
- Hide any tooltip whose source changed during the switch.

### Penance increase/decrease

Increasing:

- Nodes in `(oldLevel, newLevel]` ignite left-to-right, 45ms apart.
- Each node scales 1.0 -> 1.42 -> 1.0 over 500ms using `ease-slam` while fill/border/glow transition in.
- The fill width sweeps to the new current node over 500ms.

Decreasing:

- Nodes in `(newLevel, oldLevel]` extinguish right-to-left, 28ms apart.
- Each node scales 1.0 -> 0.8 -> 1.0 over 320ms.
- The fill width retracts over 500ms.

Tally/footer response:

- A newly active type enters from scale 0.85 over 450ms.
- A removed type collapses toward scale 0.92 while fading/shrinking away.
- Chip width/padding/margins animate over 420ms and opacity over 340ms.
- Recompute the total row width every update so neighboring chips visibly shift and the collection stays centered.
- Changed counts bump 1.0 -> 1.55 -> 1.0 with the pink flash/glow over 400ms.
- Footer selection text performs the 340ms upward text swap.
- The rendered mockup contains no separate level-title line; do not add one from the prose comment.

## Backdrop shader and parallax

Add a scene-local SpriteBatch-compatible post-process effect for the Way Station content already rendered beneath the modal.

The effect must:

- Sample the current Way Station scene through an intermediate render target; never read from and write to the same target.
- Apply animated UV zoom from 1.02 to 1.04.
- Apply cursor-driven UV offset using the same center-minus-cursor direction as the Climb V2 encounter portrait parallax.
- Apply saturation from 0.90 to 0.62 and contrast from 1.02 to 1.05.
- Apply the mockup's radial vignette and vertical dim gradient, controlled by the modal lifecycle alpha.
- Apply stable animated grain at approximately 0.12 opacity behind the modal chrome.
- Expose useful shader controls as debug-editable display-system properties.
- Use the repository `SpriteDrawing` technique, `MatrixTransform`, source texture sampler, and shader-model 3.0 conventions.
- Be registered in `Content/Content.mgcb` with `EffectImporter`, `EffectProcessor`, and `DebugMode=Auto`.

The wrapper/display integration must:

- Capture and restore render targets, blend, sampler, depth, rasterizer, and scissor state.
- Own only the post-process pass; the ordinary background/POI/dialogue systems continue owning their underlying draws.
- Run after Way Station background/incense/POI/dialogue and before all Penance UI systems.
- Lazy-load the effect, log a load failure once, and fall back to primitive dim/vignette rendering.
- Honor `ShaderRuntimeOptions.ShadersEnabled` and remain safe under `no-shaders`.
- Subscribe to `CursorStateEvent`; never read `MouseState` or `GamePad` directly.
- Update parallax smoothing and animation time in `Update`, not `Draw`.

## Snapshot fixture

Refocus modal snapshot coverage on one settled V2 state named `penance-12`:

- Sword, Dagger, and Hammer are all unlocked.
- Each weapon's highest unlocked Penance is 12.
- Hammer is selected.
- Selected Penance is XII.
- The modal is pinned to the settled Visible state.
- Entrance/idle transition timers are pinned deterministically.
- Cursor is placed where no control is hovered.
- Pending Way Station rewards/dialogue do not obscure the modal.

Update `docs/display-snapshots.md` to document the single feature capture command. Remove obsolete modal-variant references from the Way Station snapshot script/catalog, but do not generate or accept a new baseline.

Required visual workflow during implementation:

1. Run the plain `penance-12` snapshot command and inspect the generated PNG.
2. Make at most one correction pass for obvious geometry, scale, layering, font, or shader issues.
3. Run the same command a second and final time and inspect the same output path.
4. Do not run `--accept`, do not create/update a file under `tests/VisualBaselines`, and do not capture any other Penance variant.

## Documentation updates

Update authoritative documentation alongside implementation:

- `docs/GAME_RULES.md`: replace tier progression with weapon-specific Penance progression and define the five accumulated effects.
- `docs/build-run.md`: replace test-fight difficulty examples/usage with numeric Penance and document the updated unlock flag behavior.
- `docs/display-snapshots.md`: document the single `penance-12` feature fixture and its non-baseline workflow.
- Enemy implementation notes/design philosophy: replace the tier multiplier table with the Mortification formula and clarify that climb-time scaling remains on the base cadence.
- `docs/plans/waystation-run-setup-component-plan.md`: mark the earlier difficulty-based draft superseded by this plan rather than implementing both models.

## Testing strategy

### Penance rules

- Level clamp behavior at negative, 0, 1, 12, 24, and greater-than-24 inputs.
- Exact order entries and total length 24.
- Stack counts at representative levels and final counts 5/6/3/2/8.
- Player HP formula from 25 through 20.
- Enemy multiplier from 0.70 through 1.00.
- Abstinence resources at stacks 0, 1, 2, and 3.
- Shop interval at Pilgrimage stacks 0, 1, and 2.

### Progression and persistence

- Fresh meta contains Sword at 0 only.
- Sword completion unlocks Dagger at 0.
- Dagger completion unlocks Hammer at 0.
- Clearing the highest level increments exactly once.
- Clearing below the highest level does not increment.
- Level 24 never increments.
- Weapon switching selects that weapon's highest value.
- Clicking the current node steps down one; clicking another unlocked node selects it directly; locked clicks are ignored.
- Save cloning/round-trip retains per-weapon dictionary and active level.
- No code/test references remain to `RunDifficulty`, difficulty choice components/buttons, `CompletedClimbSave`, or `WayStationRunSetupSingleton`.

### Run effect integration

- Departure persists weapon and Penance before constructing run entities.
- Player creation/resume receives the Fasting maximum.
- Enemy creation applies Mortification before climb-time scaling with existing rounding.
- Abstinence affects only initial climb resources.
- Shop refresh, header markers, timeline markers, and previews agree on the dynamic interval.
- Enemy climb-time scaling still uses an eight-time cadence under Pilgrimage.
- Test-fight parsing accepts 0 and 24, rejects -1, 25, non-numeric values, and the removed tier names.

### Reparation

- Same seed/input produces identical targets, card keys, colors, and restrictions.
- Different seeds can produce different legal results.
- Exactly one distinct starter target is replaced per stack when sufficient candidates exist.
- Incoming cards are unlocked, weapon-eligible, non-Starter, non-weapon, non-token, and absent from the original generated deck.
- Results never exceed two copies of a base card.
- Restriction is always one of the agreed five.
- Replacement restrictions persist into `RunDeckService` hydration.
- Eight stacks produce eight replacements on a normal fresh collection.
- A deliberately constrained/malformed pool degrades safely without invalid cards or exceptions.

### Completion events and achievements

- Completion event carries weapon and selected level.
- Generic climb-completion achievements still advance.
- Each weapon milestone ignores other weapons.
- A completion below the milestone does not qualify; a completion at or above it does.
- All fifteen IDs, names, descriptions, and grid positions are unique.

### Motion and UI state

Use deterministic `GameTime` samples rather than screenshots for most animation coverage:

- Entrance visibility/offset/scale at every major delay boundary.
- Interaction remains disabled until 1.45 seconds.
- Exit elements follow their independent delays and become hidden after 470ms.
- Weapon flash/art pop and 10ms node ripple.
- Node ignite and extinguish ordering/durations.
- Track fill expands/retracts to exact station centers.
- Tally enter/leave widths cause centered neighboring-chip shifts.
- Count bump reaches the configured peak and settles.
- Footer text swap and tooltip refresh on state changes.
- Display systems do not mutate presentation or motion components during `Draw`.

## Implementation sequence

1. Add the Penance domain types/calculator and unit tests for the fixed order and formulas.
2. Replace save/event/achievement/test-fight tier fields with Penance fields and update clone/progression paths.
3. Replace the run-setup singleton with the world-scoped component and explicit departure flow.
4. Apply Fasting, Mortification, Abstinence, and dynamic shop refresh to active-run consumers.
5. Add deterministic Reparation loadout transformation and persistence tests.
6. Build the modal ECS presentation entities, controller interactions, and motion system without rendering.
7. Add the individual display systems and reproduce the settled HTML layout.
8. Add entrance, exit, weapon, node, fill, tally, and footer animation choreography.
9. Add the backdrop post-process/parallax effect, content registration, and `no-shaders` fallback.
10. Update the single snapshot fixture and authoritative documentation.
11. Run the full verification sequence and the two permitted screenshot passes.

## Final verification

Run from the repository root:

```bash
dotnet build
dotnet test tests/ChurchSuffering.Tests/ChurchSuffering.Tests.csproj
dotnet build /p:SkipMonoGameContentPipeline=false
dotnet run -- test-fight hammer skeleton 24
```

Also perform short shader-enabled and `no-shaders` launches when the environment supports a game window. Confirm that the post-process loads without a logged failure, both paths remain alive through multiple frames, and modal open/close does not corrupt SpriteBatch state.

Finish with:

```bash
git diff --check
git status --short
```

Then perform the two plain, non-baseline `penance-12` snapshot captures described above. Do not run baseline generation or acceptance.

## Completion criteria

The feature is complete only when:

- No Easy/Normal/Hard run-setup path remains.
- Per-weapon progression and all five Penance effects are persisted and deterministic.
- The settled modal and its full motion choreography match the HTML mockup at the 1920x1080 virtual resolution.
- Weapon, meter, node, tally, and footer visuals are owned by separate display systems with debug-editable controls.
- Cursor-driven parallax and the filtered/dimmed backdrop work with a safe shader-disabled fallback.
- Tests, builds, diagnostic test fight, documentation, and the two permitted visual inspection passes are complete.
- No new or updated visual baseline has been produced.
