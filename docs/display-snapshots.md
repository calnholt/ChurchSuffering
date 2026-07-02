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

| Part | Description |
|------|-------------|
| `snapshot` | Required first argument (replaces the removed `card-debug` command) |
| `<fixture-id>` | Registered fixture name (see table below) |
| `[fixture-args...]` | Fixture-specific flags and positional args |

### Output

- **Directory:** `debug/snapshots/<fixture-id>/` (relative to repo root; parent `debug/` is gitignored)
- **File:** `<slug>.png` — slug is fixture-defined (see each fixture)
- **Console:** `[DisplaySnapshot] Saved: <absolute-path>`
- **Exit code:** `0` on success; `1` on unknown fixture, invalid args, or invalid card/reward id (fail fast)

### Behavior (all fixtures)

- Virtual resolution 1920×1080; no profiler, tooltips, debug menu, or cursor in the PNG
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
| `colorless-card` | Card display | Colorless cards across all three printed colors and cost-pip colors |
| `quest-reward-modal` | Quest reward modal | Quest complete overlay with deck reward offer lanes |
| `modular-fx` | Modular battle FX | Fixed battle anchors with one modular effect at a sampled animation time |
| `waystation` | WayStation run setup | Run setup scene with default Sword/Easy selections |
| `player-hud` | Production player HUD systems | Player HUD geometry and state variants |
| `climb-no-events` | Climb scene | Shop + Encounters only (no active events column) |
| `climb-hazard-event` | Climb scene | Active Hazard card with visible resource gain |
| `climb-character-event` | Climb scene | Active Character card with portrait and visible reward |
| `climb-hazard-hover-preview` | Climb scene | Zero-time Hazard resource projection |
| `climb-character-hover-preview` | Climb scene | One-time Character timeline projection |
| `climb-hazard-confirmation` | Climb scene + narrative modal | Binding Hazard effect and gain confirmation |
| `climb-character-summary` | Climb scene + narrative modal | Character reward summary |
| `climb-character-dialog` | Climb background + dialogue | Background-only Character exchange |
| `climb-active-events` | Climb scene | Three columns with active event slots at T5 |
| `climb-hover-preview` | Climb scene | Hover preview on first encounter slot |
| `climb-sold-shop-slot` | Climb scene | Shop with one purchased slot hidden (3 visible items) |
| `climb-encounter-reward-modal` | Climb scene + reward modal | Encounter reward overlay |
| `climb-replacement-modal` | Climb scene + card list modal | Deck replacement picker |

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

## `colorless-card`

Renders Colorless copies of a printed White, Red, and Black card with Red, White, Black, and Any cost pips.

```bash
dotnet run -- snapshot colorless-card
dotnet run -- snapshot colorless-card --verify
```

Output: `debug/snapshots/colorless-card/all-printed-colors.png`

---

## `quest-reward-modal`

Renders `RewardModalDisplaySystem` in quest deck-offer mode: exchange lanes and upgrade lanes with optional gold in the masthead.

### Commands

```bash
# Default: gold 500 + two exchanges and one upgrade
dotnet run -- snapshot quest-reward-modal

# Explicit structured offer
dotnet run -- snapshot quest-reward-modal --gold 500 --exchange 'strike|white' 'smite|red' --exchange 'reckoning|white' 'unburdened_strike|black' --upgrade 'smite|white'

# Compatibility shortcut: creates exchange lanes using default outgoing cards
dotnet run -- snapshot quest-reward-modal --card 'strike|white'
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
| `--gold` | non-negative integer |

### Output files

| Run | Example path |
|-----|----------------|
| Defaults | `debug/snapshots/quest-reward-modal/gold-500-deck-offer-smite-red-unburdened_strike-black-smite-white-upgraded.png` |
| Explicit structured offer | `debug/snapshots/quest-reward-modal/gold-500-deck-offer-...png` |

(Slugs are defined by `QuestRewardSnapshotVariant` at implementation time; adjust this table if slugs change.)

### Errors

- Invalid or unknown `cardId` in any card key: exit `1`, no PNG
- Malformed card key: exit `1`, no PNG
- Invalid `--gold` (non-integer): exit `1`, no PNG

---

## `modular-fx`

Renders one modular battle visual effect at fixed player/enemy anchors.

```bash
dotnet run -- snapshot modular-fx heavy-hammer impact
dotnet run -- snapshot modular-fx holy-strike impact
dotnet run -- snapshot modular-fx enemy-rock-blast impact
dotnet run -- snapshot modular-fx enemy-bite impact
```

Preset tokens: `heavy-hammer`, `holy-strike`, `enemy-rock-blast`, `enemy-bite`, `enemy-slash`, `light-slash`.

Sample tokens: `start`, `impact`, `late`.

Output: `debug/snapshots/modular-fx/<preset>-<sample>.png`

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

Renders the WayStation run setup scene with default Sword/Easy selections.

### Commands

```bash
dotnet run -- snapshot waystation
```

### Output file

`debug/snapshots/waystation/default.png`

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
dotnet run -- snapshot climb-character-dialog
dotnet run -- snapshot climb-active-events
dotnet run -- snapshot climb-hover-preview
dotnet run -- snapshot climb-sold-shop-slot
dotnet run -- snapshot climb-encounter-reward-modal
dotnet run -- snapshot climb-replacement-modal
```

Modal variants draw the Climb scene plus the open modal overlay. The Character
dialogue variant draws only the undimmed desert background and dialogue overlay.

---

## Removed commands

| Old command | Replacement |
|-------------|-------------|
| `dotnet run -- card-debug` | `dotnet run -- snapshot card` |
| `dotnet run -- card-debug strike` | `dotnet run -- snapshot card strike` |

---

## Adding a new fixture

1. Implement `IDisplaySnapshotFixture` under `ECS/Diagnostics/Snapshots/Fixtures/`
2. Register in `DisplaySnapshotRegistry`
3. **Add a section to this document** with id, commands, output paths, and errors
4. Add a verification line to the implementation plan / PR test notes
