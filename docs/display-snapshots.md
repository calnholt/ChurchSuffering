# Display snapshot commands

Canonical reference for headless display verification. The game renders a fixture for two frames, writes a PNG under `debug/snapshots/`, prints the full path to the console, and exits.

**Do not document snapshot commands elsewhere** — link to this file instead. When adding a new fixture, register it in `DisplaySnapshotRegistry` and add a section below.

## Prerequisites

```bash
dotnet build
```

All commands use `dotnet run --` so arguments are passed to the game, not MSBuild.

## General form

```bash
dotnet run -- snapshot <fixture-id> [fixture-args...]
```

Add `--verify` to compare against the approved image, or `--accept` to
explicitly replace it. These flags are mutually exclusive and are not passed
to the fixture.

Add `--render-scale 2` to capture the fixture at 3840x2160 while preserving
the normal 1920x1080 logical layout. High-resolution captures are debug-only,
use an `@2x` filename suffix, and cannot be combined with `--verify` or
`--accept`. The accepted baseline workflow always remains at scale `1`.

| Part | Description |
|------|-------------|
| `snapshot` | Required first argument (replaces the removed `card-debug` command) |
| `<fixture-id>` | Registered fixture name (see table below) |
| `[fixture-args...]` | Fixture-specific flags and positional args |
| `--render-scale <value>` | Global debug capture scale greater than `0` and no greater than `2` |

### Output

- **Directory:** `debug/snapshots/<fixture-id>/` (relative to repo root; parent `debug/` is gitignored)
- **File:** `<slug>.png` — slug is fixture-defined (see each fixture)
- **Console:** `[DisplaySnapshot] Saved: <absolute-path>`
- **Exit code:** `0` on success; `1` on unknown fixture, invalid args, or invalid card/reward id (fail fast)

### Behavior (all fixtures)

- Logical resolution 1920×1080; default captures are 1920×1080 and `--render-scale 2` captures are 3840×2160
- Card snapshots use the canonical card renderer
- Does not publish `LoadSceneEvent` — uses `SceneId.Snapshot` only
- Optional launch flag `no-shaders` (e.g. `dotnet run -- snapshot card no-shaders`) disables GPU screen effects; PNGs are not comparable to full-effect baselines

---

## Fixtures

| Fixture id | Display system | Purpose |
|------------|----------------|---------|
| `card` | Card display | Three color variants of one card on a green background |
| `brittle-card` | Brittle card shader | One brittle card on a patterned backdrop for shader debugging |
| `frozen-card` | Frozen card shader | One frozen card on a patterned backdrop, optionally composed with Brittle |
| `thorned-card` | Thorned card shader | One thorned card on a patterned backdrop, optionally composed with Frozen |
| `scorched-card` | Scorched card shader | One scorched card on a patterned backdrop |
| `cursed-card` | Cursed card shader | One cursed card on a patterned backdrop |
| `poison-card` | Poison card shader | One poisoned card on a patterned backdrop |
| `poison-scorched-card` | Poison + Scorched composition | One card with both Poisoned and Scorched on a patterned backdrop |
| `card-render-pipeline` | Card render pipeline | Ordered all-status and sheen composition variants |
| `colorless-card` | Card display | Colorless cards across all three printed colors and cost-pip colors |
| `dual-color-card` | Card display | Dual-color diagonal split pairings, including black secondary block bonuses |
| `quest-reward-modal` | Quest reward modal | Quest complete overlay with deck reward offer lanes |
| `booster-pack-opening` | Booster pack opening display | Phase-driven pack summon, rupture, reward travel, sheen, and ready states |
| `modular-fx` | Modular battle FX | Fixed battle anchors with one modular effect at a sampled animation time |
| `passive-application` | Passive application animation | Status seals at fixed player/enemy anchors, including stagger and concurrent attack variants |
| `waystation` | WayStation hub | Hub scene with the Waystation banner, Climb POI, and Achievement POI |
| `waystation-collection` | WayStation collection modal | Reliquary cards, saints, and equipment tabs |
| `player-hud` | Production player HUD systems | Player HUD geometry and state variants |
| `equipment-tooltip` | Equipment panel and tooltip | Active, passive, and used equipment states |
| `enemy-damage-meter` | Enemy damage meter | Initial, transitioning, settled, and absorb animation samples |
| `enemy-attack-banner` | Enemy attack banner | Anticipation, impact, settled, hover, and absorb presentation samples |
| `assigned-block-rail` | Assigned blocker rail | Card/equipment density, hover, entry impact, and return presentation samples |
| `enemy-defeat-burst` | Enemy defeat pixel burst | Assembled portrait pixels, peak jitter buildup, and released explosion |
| `guardian-angel` | Guardian Angel battle companion | Idle, speech, and reactive flight gesture samples |
| `pause-menu` | Pause menu | Persisted rumble toggle in enabled and disabled states |
| `hotkey-hints` | Shared hotkey glyph renderer | Keyboard, Xbox, and PlayStation hint treatments and placement |
| `battle-phase-transition` | Battle phase transition | Entry, hold, and exit samples across phase title treatments |
| `achievement-overview` | Achievement scene | Mixed discovery states, collection meter, and claim action |
| `achievement-detail` | Achievement scene | Hover detail panel for a visible achievement |
| `climb-no-events` | Climb scene | Shop + Encounters only (no active events column) |
| `climb-hazard-event` | Climb scene | Active Hazard card with visible resource gain |
| `climb-character-event` | Climb scene | Active Character card with portrait and visible reward |
| `climb-hazard-hover-preview` | Climb scene | Zero-time Hazard resource projection |
| `climb-character-hover-preview` | Climb scene | One-time Character timeline projection |
| `climb-hazard-confirmation` | Climb scene + narrative modal | Binding Hazard effect and gain confirmation |
| `climb-character-summary` | Climb scene + narrative modal | Character reward summary |
| `climb-character-dialog` | Climb background + dialogue | Background-only Character exchange |
| `climb-active-events` | Climb scene V2 | Fixed shop, encounter, and active-event regions at T6 |
| `climb-hover-preview` | Climb scene | Hover preview on first encounter slot |
| `climb-medal-tooltip-hover` | Climb scene | Medal shop hover with icon followed by text tooltip |
| `climb-card-tooltip-hover` | Climb scene V2 | Card shop hover with upgrade-preview tooltip |
| `climb-equipment-tooltip-hover` | Climb scene V2 | Equipment shop hover with equipment tooltip |
| `climb-sold-shop-slot` | Climb scene | Shop with one purchased slot hidden (3 visible items) |
| `climb-replacement-modal` | Climb scene + card list modal | Deck replacement picker |
| `climb-header` | Climb scene | Compact resources, preview badges, pulse, and Run Overview control |
| `climb-resource-acquisition` | Climb scene | Gem fall, pouch catch, and earned-resource pulse |
| `climb-v2-entrance` | Climb scene V2 | Fresh-climb staggered entrance sample |
| `climb-v2-ashes` | Climb scene V2 | Midpoint ashes/desaturation turnover sample |
| `climb-v2-purchase` | Climb scene V2 | Midpoint shop purchase split sample |
| `climb-points-award` | WayStation return overlay | Climb threshold ascent, total crest, zero, and abandoned variants |

---

## Climb scene V2

The V2 fixture matrix covers the fixed six-item shop/no-event layout, the full mockup-bible layout,
hover projections, the three shop tooltip routes, transition samples, the V2 header,
and the retained V1 resource-acquisition overlay. Run the complete matrix with:

```bash
./scripts/verify-climb-v2-snapshots.sh
./scripts/verify-climb-v2-snapshots.sh --accept
```

Approved images are stored under their matching directories in `tests/VisualBaselines/`.

---

## `climb-header`

Renders the production V2 Climb header with fixed resource cells and the Climb Overview control. The variants cover mixed-width resource values, simultaneous positive and negative preview badges, earned-resource pulse, and overview hover feedback.

```bash
dotnet run -- snapshot climb-header normal --verify
dotnet run -- snapshot climb-header preview-delta --verify
dotnet run -- snapshot climb-header pulse --verify
dotnet run -- snapshot climb-header overview-hover --verify
./scripts/verify-climb-header-snapshots.sh
```

Approved images are stored under `tests/VisualBaselines/climb-header/`.

---

## `climb-resource-acquisition`

Renders the production Climb resource acquisition overlay with a fixed reward of two red, one white, and one black gem. The variants sample the pouch entrance, weighted gem fall, first catch, and earned-color header pulse.

```bash
dotnet run -- snapshot climb-resource-acquisition entry --verify
dotnet run -- snapshot climb-resource-acquisition fall --verify
dotnet run -- snapshot climb-resource-acquisition catch --verify
dotnet run -- snapshot climb-resource-acquisition pulse --verify
./scripts/verify-climb-resource-acquisition-snapshots.sh
```

Approved images are stored under `tests/VisualBaselines/climb-resource-acquisition/`.

---

## `climb-points-award`

Renders the production Waystation return overlay at fixed animation samples and for every climb-point outcome represented by the source mockup.

```bash
dotnet run -- snapshot climb-points-award victory-ready --verify
./scripts/verify-climb-points-award-snapshots.sh
./scripts/verify-climb-points-award-snapshots.sh --accept
```

Approved images are stored under `tests/VisualBaselines/climb-points-award/`.

---

## `battle-phase-transition`

Renders the production battle phase transition over a fixed gothic battle background. The variants cover every phase-specific title lockup plus representative entry and exit motion.

```bash
dotnet run -- snapshot battle-phase-transition block-hold --verify
./scripts/verify-battle-phase-transition-snapshots.sh
```

Approved images are `start-hold.png`, `block-entry.png`, `block-hold.png`, `action-hold.png`, `action-exit.png`, `pledge-hold.png`, and `victory-hold.png` under `tests/VisualBaselines/battle-phase-transition/`.

---

## `enemy-damage-meter`

Renders the enemy damage meter against a fixed attack-banner anchor. The variants cover its initial damage/aegis state, a mid-transition block assignment, the settled result, and the EnemyAttack absorb exit.

```bash
dotnet run -- snapshot enemy-damage-meter initial --verify
dotnet run -- snapshot enemy-damage-meter transition --verify
dotnet run -- snapshot enemy-damage-meter settled --verify
dotnet run -- snapshot enemy-damage-meter absorb --verify
./scripts/verify-enemy-damage-meter-snapshots.sh
```

Approved images are `initial.png`, `transition.png`, `settled.png`, and `absorb.png` under `tests/VisualBaselines/enemy-damage-meter/`. Unknown variants or extra arguments exit with code `1` without producing a PNG.

---

## `enemy-attack-banner`

Renders the production enemy attack banner against the gothic battle background with a fixed damage-15 attack and deterministic attack sequence. Variants cover the entrance anticipation, impact burst, settled state, confirm hover feedback, repeating outline echo, and absorb exit.

```bash
dotnet run -- snapshot enemy-attack-banner anticipation --verify
dotnet run -- snapshot enemy-attack-banner impact --verify
dotnet run -- snapshot enemy-attack-banner settled --verify
dotnet run -- snapshot enemy-attack-banner hover --verify
dotnet run -- snapshot enemy-attack-banner pulse --verify
dotnet run -- snapshot enemy-attack-banner absorb --verify
./scripts/verify-enemy-attack-banner-snapshots.sh
```

Approved images are stored under `tests/VisualBaselines/enemy-attack-banner/`.

---

## `assigned-block-rail`

Renders the production gothic blocker rail attached to a fixed enemy attack banner. Variants cover a single card, mixed card/equipment assignments, the normal eight-item dense state, hover lift, entry impact, equipment return motion, and Chrono Slice blockers flying toward distinct draw and discard piles.

```bash
dotnet run -- snapshot assigned-block-rail single-card --verify
dotnet run -- snapshot assigned-block-rail chronoslice-flight --verify
./scripts/verify-assigned-block-rail-snapshots.sh
```

Approved images are stored under `tests/VisualBaselines/assigned-block-rail/`.

---

## `enemy-defeat-burst`

Renders the production enemy defeat pixel burst with a fixed Skeleton portrait and deterministic particle seed. The variants cover the reconstructed portrait, the end of the accelerating jitter buildup, and the released explosion.

```bash
dotnet run -- snapshot enemy-defeat-burst assembled --verify
dotnet run -- snapshot enemy-defeat-burst peak-jitter --verify
dotnet run -- snapshot enemy-defeat-burst exploding --verify
./scripts/verify-enemy-defeat-burst-snapshots.sh
```

Approved images are `assembled.png`, `peak-jitter.png`, and `exploding.png` under `tests/VisualBaselines/enemy-defeat-burst/`.

---

## `guardian-angel`

Renders the Guardian Angel at a fixed player-side anchor with deterministic idle placement and representative speech gestures.

```bash
dotnet run -- snapshot guardian-angel idle --verify
dotnet run -- snapshot guardian-angel message --verify
dotnet run -- snapshot guardian-angel card-hop --verify
dotnet run -- snapshot guardian-angel medal-loop --verify
dotnet run -- snapshot guardian-angel enemy-recoil --verify
./scripts/verify-guardian-angel-snapshots.sh
```

Approved images are stored under `tests/VisualBaselines/guardian-angel/`.

---

## `pause-menu`

Renders the production pause rail with the persisted rumble toggle enabled or disabled.

```bash
dotnet run -- snapshot pause-menu rumble-on --verify
dotnet run -- snapshot pause-menu rumble-off --verify
./scripts/verify-pause-menu-snapshots.sh
```

Approved images are `rumble-on.png` and `rumble-off.png` under `tests/VisualBaselines/pause-menu/`.

---

## `hotkey-hints`

Renders the shared keyboard, Xbox, and PlayStation hotkey glyph treatments plus top, right, left, and below placement around a sample action.

```bash
dotnet run -- snapshot hotkey-hints keyboard --verify
dotnet run -- snapshot hotkey-hints xbox --verify
dotnet run -- snapshot hotkey-hints playstation --verify
./scripts/verify-hotkey-hint-snapshots.sh
```

Approved images are `keyboard.png`, `xbox.png`, and `playstation.png` under `tests/VisualBaselines/hotkey-hints/`.

---

## `equipment-tooltip`

Renders an equipped item with its hover tooltip. The `active` and `used` variants use the activation-only Bulwark Plate to verify that zero-block equipment has no footer band or BLOCK chip; the `used` variant also verifies the transparent used-state treatment. The `passive` variant covers block-only equipment.

```bash
dotnet run -- snapshot equipment-tooltip active --verify
dotnet run -- snapshot equipment-tooltip passive --verify
dotnet run -- snapshot equipment-tooltip used --verify
./scripts/verify-equipment-tooltip-snapshots.sh
```

Approved images are `active.png`, `passive.png`, and `used.png` under `tests/VisualBaselines/equipment-tooltip/`.

---

## `achievement-overview` and `achievement-detail`

Render the production Achievement scene with deterministic collection progress. The overview covers hidden, visible, completed, and completed-unseen cells plus an enabled claim action; the detail variant opens the hover-driven achievement record panel.

```bash
dotnet run -- snapshot achievement-overview
dotnet run -- snapshot achievement-detail
./scripts/verify-achievement-snapshots.sh
```

Output files are stored under `debug/snapshots/<fixture-id>/<fixture-id>.png`.

---

## `booster-pack-opening`

Renders the production booster opening overlay at a deterministic timeline sample. `--time` accepts a finite value from `0.0` through `30.0`; `--seed` accepts any signed 32-bit integer.

The fixture always displays a fixed Card as a White/Red/Black fan, plus a Medal and Scarlet Vest reward, so card-fan layout and equipment-art changes are covered by the baseline; the seed controls presentation particles only.

```bash
dotnet run -- snapshot booster-pack-opening
dotnet run -- snapshot booster-pack-opening --time 3.10 --seed 1337
dotnet run -- snapshot booster-pack-opening --time 5.14 --seed 1337
dotnet run -- snapshot booster-pack-opening --time 4.70 --seed 1337 no-shaders
```

Captures are stored at `debug/snapshots/booster-pack-opening/time-<seconds>-seed-<seed>.png`.

Useful sequence samples are `0.45` (summon), `1.70` (charge), `2.40` (crack), `3.10` (rupture), `3.95` (reward travel), `4.70` (sheen), and `5.14` (ready).

---

## `card`

Renders **White**, **Red**, and **Black** copies of the same card side by side (same layout as the former `card-debug` flow).

### Commands

```bash
# Random non-weapon card (card id omitted)
dotnet run -- snapshot card

# Specific card by definition id
dotnet run -- snapshot card strike
dotnet run -- snapshot card fireball
```

### Output file

`debug/snapshots/card/<cardId>.png`

Example: `debug/snapshots/card/strike.png`

### Errors

- If `<cardId>` is provided but unknown: exit `1`, no PNG

---

## `brittle-card`

Renders one White card with the `Brittle` component attached on a high-contrast patterned backdrop.

### Commands

```bash
# Default card
dotnet run -- snapshot brittle-card

# Specific card by definition id
dotnet run -- snapshot brittle-card strike
dotnet run -- snapshot brittle-card fireball

# Transform-aware shader checks
dotnet run -- snapshot brittle-card strike --scale 0.6 --rotation -25
dotnet run -- snapshot brittle-card strike --scale 1.35 --rotation 30

# Include an attached card decoration outside the brittle source capture
dotnet run -- snapshot brittle-card strike --rotation 20 --pledge

# Disable shaders to compare against the normal card render
dotnet run -- snapshot brittle-card strike no-shaders
```

### Output file

`debug/snapshots/brittle-card/<cardId>.png` for the default transform. Transform variants append their scale, rotation, and optional pledge state to the filename.

Example: `debug/snapshots/brittle-card/strike.png`

### Errors

- If `<cardId>` is provided but unknown: exit `1`, no PNG

---

## `frozen-card`

Renders one White card with the `Frozen` component attached on a high-contrast patterned backdrop.

```bash
dotnet run -- snapshot frozen-card
dotnet run -- snapshot frozen-card strike --scale 0.6 --rotation -25
dotnet run -- snapshot frozen-card strike --rotation 20 --brittle
dotnet run -- snapshot frozen-card strike no-shaders
```

Transform variants append their scale, rotation, and optional Brittle state to files under `debug/snapshots/frozen-card/`.

---

## `thorned-card`

Renders one White card with the `Thorned` component attached on a high-contrast patterned backdrop.

```bash
dotnet run -- snapshot thorned-card
dotnet run -- snapshot thorned-card strike --scale 0.6 --rotation -25
dotnet run -- snapshot thorned-card strike --frozen
dotnet run -- snapshot thorned-card strike no-shaders
```

Transform variants append their scale, rotation, and optional Frozen state to files under `debug/snapshots/thorned-card/`.

---

## `poison-card`

Renders one White card with the `Poisoned` component attached on a high-contrast patterned backdrop.

```bash
dotnet run -- snapshot poison-card
dotnet run -- snapshot poison-card fireball
dotnet run -- snapshot poison-card no-shaders
dotnet run -- snapshot poison-card --verify
```

Output: `debug/snapshots/poison-card/<cardId>.png`

---

## `poison-scorched-card`

Renders one White card with both `Poisoned` and `Scorched` attached, locking overlay time so slime stays on the card face while fire overflow remains outside it.

```bash
dotnet run -- snapshot poison-scorched-card
dotnet run -- snapshot poison-scorched-card fireball
dotnet run -- snapshot poison-scorched-card no-shaders
dotnet run -- snapshot poison-scorched-card --verify
```

Output: `debug/snapshots/poison-scorched-card/<cardId>.png`

---

## `scorched-card` and `cursed-card`

Render one White card with the selected status shader on the shared patterned shader-debug backdrop.

```bash
dotnet run -- snapshot scorched-card
dotnet run -- snapshot scorched-card strike --scale 0.6 --rotation -25
dotnet run -- snapshot cursed-card
dotnet run -- snapshot cursed-card strike --scale 0.6 --rotation -25
```

Transform variants append their scale and rotation under `debug/snapshots/<fixture-id>/`.

---

## `card-render-pipeline`

Locks the production pass order and the unified sheen path. Variants are `all-statuses`, `sheen-only`, and `all-statuses-sheen`.

```bash
./scripts/verify-card-render-pipeline-snapshots.sh --verify
./scripts/verify-card-render-pipeline-snapshots.sh --accept
```

Approved images are stored under `tests/VisualBaselines/card-render-pipeline/`.

---

## `colorless-card`

Renders Colorless copies of a printed White, Red, and Black card with Red, White, Black, and Any cost pips.

```bash
dotnet run -- snapshot colorless-card
dotnet run -- snapshot colorless-card --verify
```

Output: `debug/snapshots/colorless-card/all-printed-colors.png`

---

## `dual-color-card`

Renders White/Black, Red/Black, and Black/White Strike cards with a hard top-left-to-bottom-right split. The black-secondary variants also lock the visible +1 block value on the printed-color half.

```bash
dotnet run -- snapshot dual-color-card
dotnet run -- snapshot dual-color-card --verify
```

Output: `debug/snapshots/dual-color-card/pairings.png`

---

## `quest-reward-modal`

Renders the cinematic `RewardModalDisplaySystem` deck offer with two exchange lanes and one upgrade lane.

### Commands

```bash
# Default: two exchanges and one upgrade
dotnet run -- snapshot quest-reward-modal

# Deterministic presentation samples
dotnet run -- snapshot quest-reward-modal --presentation entering
dotnet run -- snapshot quest-reward-modal --presentation claiming
dotnet run -- snapshot quest-reward-modal --presentation skipping

# Explicit structured offer
dotnet run -- snapshot quest-reward-modal --exchange 'strike|white' 'smite|red' --exchange 'reckoning|white' 'unburdened_strike|black' --upgrade 'smite|white'

# Verify or accept the visible, entering, claiming, and skipping matrix
./scripts/verify-quest-reward-modal-snapshots.sh
./scripts/verify-quest-reward-modal-snapshots.sh --accept
```

### Card key format

`cardId|color` or `cardId|color|Upgraded`:

| Color | Token |
|-------|--------|
| White | `white` |
| Red | `red` |
| Black | `black` |

Example: `'strike|white'`, `'smite|red|Upgraded'` (quote in shell so `|` is not piped)

### Offer args

| Arg | Values |
|-----|--------|
| `--exchange` | `outgoingCardKey incomingCardKey` |
| `--upgrade` | `cardKey` |
| `--presentation` | `entering`, `visible`, `claiming`, or `skipping` |

### Output files

| Run | Example path |
|-----|----------------|
| Defaults | `debug/snapshots/quest-reward-modal/deck-offer-smite-red-unburdened_strike-black-smite-white-upgraded.png` |
| Entering | `debug/snapshots/quest-reward-modal/deck-offer-smite-red-unburdened_strike-black-smite-white-upgraded-entering.png` |
| Claiming | `debug/snapshots/quest-reward-modal/deck-offer-smite-red-unburdened_strike-black-smite-white-upgraded-claiming.png` |
| Skipping | `debug/snapshots/quest-reward-modal/deck-offer-smite-red-unburdened_strike-black-smite-white-upgraded-skipping.png` |
| Explicit structured offer | `debug/snapshots/quest-reward-modal/deck-offer-...png` |

(Slugs are defined by `QuestRewardSnapshotVariant` at implementation time; adjust this table if slugs change.)

### Errors

- Invalid or unknown `cardId` in any card key: exit `1`, no PNG
- Malformed card key: exit `1`, no PNG

---

## `modular-fx`

Renders one modular battle visual effect at fixed player/enemy anchors.

```bash
dotnet run -- snapshot modular-fx heavy-hammer impact
dotnet run -- snapshot modular-fx holy-strike impact
dotnet run -- snapshot modular-fx enemy-rock-blast impact
dotnet run -- snapshot modular-fx enemy-bite impact

# Isolated module with deterministic organic variation and direction
dotnet run -- snapshot modular-fx --module cracks --sample impact --seed 1337 --direction right
dotnet run -- snapshot modular-fx --module slash-band --sample impact --seed 1337 --direction left
dotnet run -- snapshot modular-fx --module energy-bolt --sample impact --palette fire --direction right
dotnet run -- snapshot modular-fx --module seal-stamp --sample impact --palette arcane --target card
```

Preset tokens: `heavy-hammer`, `holy-strike`, `enemy-rock-blast`, `enemy-bite`, `enemy-slash`, `light-slash`.

Sample tokens: `start`, `impact`, `late`.

Isolated-module options:

| Option | Values | Default |
|--------|--------|---------|
| `--module` | Any `VisualEffectModule` name in kebab case | None; render the positional preset |
| `--sample` | `start`, `impact`, `late` | `impact` |
| `--seed` | Any signed 32-bit integer | `1337` |
| `--direction` | `right`, `left` | Derived from the recipe target |
| `--palette` | `physical`, `holy`, `blood`, `fire`, `ice`, `shadow`, `earth`, `poison`, `arcane` | Module debug-catalog palette |
| `--target` | `actor`, `card` | `actor` |

The seed controls cracks, debris, shards, smoke, rays, rock fragments, poison clouds, and other organic variation. The same seed reproduces the same geometry. Run `./scripts/verify-modular-fx-snapshots.sh --verify` to check the representative preset, palette, actor-target, and card-target matrix; use `--accept` only for intentional visual changes.

Output: `debug/snapshots/modular-fx/<preset>-<sample>.png` for preset commands, or `debug/snapshots/modular-fx/module-<module>-<sample>-<direction>-seed-<seed>[-<palette>][-card].png` for isolated modules.

---

## `passive-application`

Renders the production passive-application seal beside a fixed player or enemy portrait. The optional mode covers staggered status groups and overlap with modular attack FX.

```bash
dotnet run -- snapshot passive-application burn hold player single --verify
dotnet run -- snapshot passive-application aegis entry player single --verify
dotnet run -- snapshot passive-application fear hold enemy single --verify
dotnet run -- snapshot passive-application frostbite exit player single --verify
dotnet run -- snapshot passive-application burn hold enemy multi --verify
dotnet run -- snapshot passive-application wounded hold player attack --verify
./scripts/verify-passive-application-snapshots.sh
```

Arguments are `[passive-type] [entry|hold|exit] [player|enemy] [single|multi|attack]`. Passive names accept enum names or kebab-case slugs. Output is stored under `debug/snapshots/passive-application/` and approved baselines under `tests/VisualBaselines/passive-application/`.

---

## `narrative-event-modal`

Renders `NarrativeEventModalDisplaySystem` for a narrative event type and optional visible option count.

### Commands

```bash
# Default: icebound_tithe, 3 options
dotnet run -- snapshot narrative-event-modal

dotnet run -- snapshot narrative-event-modal --event pruned_vocation

dotnet run -- snapshot narrative-event-modal --event icebound_tithe --options 1
dotnet run -- snapshot narrative-event-modal --event icebound_tithe --options 2
```

### Output files

| Run | Example path |
|-----|----------------|
| Defaults | `debug/snapshots/narrative-event-modal/icebound-tithe-options-3.png` |
| `--event pruned_vocation` | `debug/snapshots/narrative-event-modal/pruned-vocation-options-3.png` |
| `--event icebound_tithe --options 1` | `debug/snapshots/narrative-event-modal/icebound-tithe-options-1.png` |
| `--event icebound_tithe --options 2` | `debug/snapshots/narrative-event-modal/icebound-tithe-options-2.png` |

### Errors

- Unknown `--event` id: exit `1`, no PNG
- Invalid `--options` (not 1, 2, or 3): exit `1`, no PNG
- Malformed / unknown CLI token: exit `1`, no PNG

---

## `waystation`

Renders the settled fullscreen Penance V2 modal. Sword, Dagger, and Hammer are unlocked through Penance XII; Hammer/Penance XII is selected. Pending rewards and dialogue are suppressed, animation state is pinned, and the cursor does not hover a control.

### Commands

```bash
dotnet run -- snapshot waystation penance-12
./scripts/verify-waystation-snapshots.sh
```

This feature fixture is a plain, non-baseline capture. Inspect the generated PNG directly. Do not pass `--accept` and do not add it under `tests/VisualBaselines`.

### Output file

`debug/snapshots/waystation/penance-12.png`

---

## `waystation-collection`

Captures the production Crusader's Reliquary modal with a deterministic partial
collection and all three weapons unlocked. This fixture intentionally has no visual
baseline. Do not use `--verify` or `--accept`; compare fresh captures manually with
`mockups/waystation-collection-overlay-v1.html`.

```bash
dotnet run -- snapshot waystation-collection cards
dotnet run -- snapshot waystation-collection cards-hover
dotnet run -- snapshot waystation-collection saints
dotnet run -- snapshot waystation-collection saints-hover
dotnet run -- snapshot waystation-collection equipment
dotnet run -- snapshot waystation-collection equipment-hover
./scripts/capture-waystation-collection-snapshots.sh
```

Outputs are written to `debug/snapshots/waystation-collection/`. Run the complete
six-variant script at most twice for one implementation verification: inspect all
six images after the first pass, make one correction if needed, then capture the
whole set once more.

---

## `player-hud`

Renders the production player HUD systems against a fixed portrait and solid
backdrop. Approved images are stored under
`tests/VisualBaselines/player-hud/`.

```bash
dotnet run -- snapshot player-hud default
dotnet run -- snapshot player-hud unavailable
dotnet run -- snapshot player-hud incoming-damage
dotnet run -- snapshot player-hud low-health
dotnet run -- snapshot player-hud expanded
dotnet run -- snapshot player-hud enemy-health

./scripts/verify-player-hud-snapshots.sh
./scripts/verify-player-hud-snapshots.sh --accept
```

The `enemy-health` variant renders the enemy full health region in isolation;
the player HUD is placed outside the capture to avoid cursor-driven parallax.
Passive-chip animation durations are disabled in this fixture so the approved
images always capture Chakra Petch labels in their settled layout.
The verification script is read-only by default. `--accept` explicitly
replaces all six approved baselines.

---

## Climb fixtures

Renders the Climb scene HUD at 1920x1080 with fixture-specific save state.
Output PNGs are written under `debug/snapshots/<fixture-id>/<fixture-id>.png`.

```bash
dotnet run -- snapshot climb-no-events
dotnet run -- snapshot climb-hazard-event
dotnet run -- snapshot climb-character-event
dotnet run -- snapshot climb-hazard-hover-preview
dotnet run -- snapshot climb-character-hover-preview
dotnet run -- snapshot climb-hazard-confirmation
dotnet run -- snapshot climb-character-summary
dotnet run -- snapshot climb-character-dialog intro
dotnet run -- snapshot climb-character-dialog settled
dotnet run -- snapshot climb-active-events
dotnet run -- snapshot climb-hover-preview
dotnet run -- snapshot climb-medal-tooltip-hover
dotnet run -- snapshot climb-sold-shop-slot
dotnet run -- snapshot climb-replacement-modal
dotnet run -- snapshot climb-inventory-overlay
dotnet run -- snapshot climb-inventory-equipment-tooltip
```

Modal variants draw the Climb scene plus the open modal overlay. The Character
dialogue variant draws only the undimmed desert background and dialogue overlay.
It accepts `intro` and `settled` samples, defaulting to `settled`; outputs are
`intro.png` and `settled.png`. Both samples validate that every authored catalog
line fits the production dialogue body area before rendering.

Approved dialogue images are under
`tests/VisualBaselines/climb-character-dialog/`. Verify both with:

```bash
./scripts/verify-climb-character-dialog-snapshots.sh
```

The inventory variants cover the Run Overview's equipment art and its shared rich
equipment tooltip. Approved images are under `tests/VisualBaselines/climb-inventory-overlay/`
and `tests/VisualBaselines/climb-inventory-equipment-tooltip/`. Verify both with:

```bash
./scripts/verify-climb-inventory-snapshots.sh
```

Card-list renderer changes should also verify the replacement picker and default
inventory overlay at the top, middle, and bottom of a deterministic 60-card list:

```bash
./scripts/verify-card-list-modal-snapshots.sh
```

---

## Removed commands

| Old command | Replacement |
|-------------|-------------|
| `dotnet run -- card-debug` | `dotnet run -- snapshot card` |
| `dotnet run -- card-debug strike` | `dotnet run -- snapshot card strike` |

---

## Adding or changing a fixture

1. Implement `IDisplaySnapshotFixture` under `ECS/Diagnostics/Snapshots/Fixtures/`.
2. Register it in `DisplaySnapshotRegistry`.
3. **Add or update a section in this document** with fixture id, command examples, output path, valid args, and error behavior.
4. Generate or replace the approved baseline intentionally:

```bash
dotnet build
dotnet run --no-build -- snapshot <fixture-id> [fixture-args...] --accept
```

5. Verify the accepted baseline passes:

```bash
dotnet run --no-build -- snapshot <fixture-id> [fixture-args...] --verify
```

6. Commit the approved baseline PNG under `tests/VisualBaselines/<fixture-id>/<slug>.png`.
7. If the fixture has multiple variants, add or update a verification script under `scripts/` so future agents can run the full set without remembering every command.

Remember:

- `debug/snapshots/` contains generated captures, actuals, and diffs. Do not commit it.
- `tests/VisualBaselines/` contains approved baselines. Commit intentional baseline changes.
- `--accept` is a mutating operation. Use it only when the visual change is intentional.
- `--verify` is the regression check.
