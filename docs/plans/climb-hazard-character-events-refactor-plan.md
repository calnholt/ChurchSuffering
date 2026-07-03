# Climb Hazard and Character Events Refactor - Multi-Agent Implementation Plan

## Document Status

- **Status:** Approved design, ready for implementation.
- **Repository:** `Crusaders30XX`
- **Runtime:** .NET 8.0, MonoGame DesktopGL, ECS architecture.
- **Primary scene:** `SceneId.Climb`.
- **Required final verification:** `dotnet test` followed by `dotnet build` from the repository root.
- **Save compatibility:** None. Increment the save version and require a fresh run. Do not add migration or reconciliation code.

This document is intentionally exhaustive. It is written so multiple implementation agents can work from the same source of truth without making product or architecture decisions locally.

---

## 1. Objective

Replace the existing undifferentiated Climb event runtime with two canonical event types:

1. **Hazard events**
   - Three are generated per run.
   - Hazard definitions may repeat within a run.
   - They do not advance Climb time.
   - Each grants one or two Red, White, or Black Climb resource pips.
   - Each applies one harmful effect.
   - The resource reward is visible on the Climb card before selection.
   - The harmful effect is hidden until selection, and selection is binding.
   - A one-choice narrative modal reveals the exact effect and reward; `Confirm` resolves both atomically.

2. **Character events**
   - Two distinct characters are generated per run from a pool of four.
   - They grant no Red, White, or Black Climb resources.
   - Their exact reward is visible below the portrait before selection.
   - They run a two-line exchange between the character and the Crusader.
   - During dialogue, the only scene art visible behind the dialogue is the undimmed Climb background image.
   - A one-choice reward summary follows the dialogue.
   - `Proceed` applies the reward and advances Climb time by exactly one in one atomic resolution.

Exactly five event instances are generated at run creation. Expired, ignored, or Final-encounter-preempted events still count toward that total and are never replenished.

---

## 2. Canonical Product Decisions

These decisions are closed. Implementers must not reinterpret them.

### 2.1 Canonical Climb model

- The active Climb is the capped time-and-slot model represented by `SceneId.Climb` and the Shop, Encounter, and Event columns.
- Maximum Climb time is `ClimbRuleService.MaxTime`, currently `32`.
- Reaching maximum Climb time triggers the Final encounter.
- Spatial Climb topology, fog, event landmarks, map shops, and Treasure Chests are legacy concepts.
- The old Location/run-map event path remains compiled but dormant. Do not delete it in this change, and do not use it for the new Climb event runtime.
- `Narrative event` is a legacy/UI implementation term, not a canonical event subtype.

### 2.2 Canonical terminology

- **Climb event:** One generated, time-scheduled, non-combat opportunity in the active Climb.
- **Hazard event:** A Climb event that hides a binding harmful effect while showing its Climb resource reward.
- **Character event:** A Climb event that presents a character exchange and beneficial reward.
- **Climb resource:** A Red, White, or Black spendable unit used by the Climb economy.
- **Climb resource pip:** One unit of one Climb resource color.
- **Next-battle bonus:** Saved Courage, Temperance, or Vigor granted once when the next Climb encounter begins.
- **Next-battle penalty:** Saved Burn or Fear granted once when the next Climb encounter begins.
- **Deck card entry:** One stable, run-scoped card instance in the ordered loadout. Two entries may have the same card key while retaining different identities and restrictions.

### 2.3 Generation and duplication

- Generate three Hazard instances independently with replacement from the seven-definition pool.
- The same Hazard definition may therefore appear multiple times in one run.
- Generate two Character instances without replacement from the four-character pool.
- A character cannot appear twice in one run.
- All generation uses a deterministic RNG derived from the run seed and a dedicated Climb-event salt.
- Roll and persist all definition choices, amounts, rewards, durations, and scheduled appearance times at run creation. Never reroll them when loading or displaying the event.

### 2.4 Appearance and duration

- Scheduled appearance times are hidden from the player.
- Divide integer Climb times `1..MaxTime` into five equal chronological bands.
- For `MaxTime == 32`, the bands are exactly:
  - Event position 0: `1..6`
  - Event position 1: `7..12`
  - Event position 2: `13..19`
  - Event position 3: `20..25`
  - Event position 4: `26..32`
- Shuffle the five generated event instances before assigning one to each band.
- Roll one inclusive appearance time inside each event's assigned band.
- Hazard duration is an inclusive random roll from `2..4`.
- Character duration is an inclusive random roll from `3..5`.
- An event does not necessarily activate at its scheduled appearance time. It activates when Climb time first lands on or beyond that time.
- When activated, store the actual landing time as `activatedAtTime`.
- Give the event its full duration from that landing time.
- Visibility is end-exclusive:
  - Activated at time `10`, duration `2` -> visible at times `10` and `11`, expired at `12`.
- If one time advance crosses multiple scheduled appearance times, activate all crossed events at the same landing time.
- If a time advance reaches `MaxTime`, the Final encounter takes precedence. Do not activate newly crossed events, and expire all other unresolved events.
- Late events are allowed to be preempted by the Final encounter. Do not reserve an end-of-Climb buffer.

### 2.5 Binding and interruption behavior

- Hazard selection is binding. There is no cancel or back-out route after clicking the Hazard card.
- Hazard effects and resources apply only when the modal's `Confirm` button is selected.
- If the game is interrupted during Hazard confirmation, reload into the same confirmation without applying anything twice.
- Character selection is binding. There is no return to the Climb between the dialogue and reward summary.
- Character reward and one-time cost both apply only when `Proceed` is selected.
- If the game is interrupted during Character dialogue, restart the two-line exchange from line one.
- If interrupted during the Character reward summary, reopen the summary. No reward or time has been applied yet.
- Pending events are exempt from normal event expiration until they resolve.

### 2.6 Next-battle behavior

- Courage, Temperance, Vigor, Burn, and Fear values from multiple events add together.
- Consume the accumulated package once at the start of the first queued encounter in the next Climb encounter.
- Apply it after the normal start-of-battle resets so Courage is not immediately erased.
- After the one-time grant, every value follows its existing lifetime rules:
  - Courage follows queued-encounter Courage reset behavior.
  - Temperance follows existing Climb-encounter carry behavior.
  - Vigor follows its normal passive consumption/lifetime behavior.
  - Burn follows its normal battle passive behavior.
  - Fear follows its normal encounter-scoped behavior.
- Apply the package to the Final encounter if it is the next Climb encounter.

### 2.7 Stable deck-entry identity

- A one-card Hazard must affect one specific deck card entry, even when the deck contains identical card keys.
- Replace the value-only `cardIds` list with structured loadout entries.
- Store run-long card restrictions directly on each entry.
- Upgrade preserves the entry ID, provenance, and restrictions.
- Replacement or reward exchange removes the outgoing entry and creates a new entry ID at the same ordered position with no inherited restrictions.
- Exhaust removes the exact entry.
- Newly added cards receive new entry IDs.
- Duplicate identical card entries are valid and must create separate ECS card entities.

---

## 3. Explicit Non-Goals

- Do not migrate old saves.
- Do not preserve `cardIds` as a compatibility API or parallel list.
- Do not retain key-based card-restriction persistence.
- Do not add a second upgrade tier for the Smith.
- Do not compensate an ineligible Smith with resources or Vigor.
- Do not invent fallback penalties for restriction Hazards.
- Do not remove the dormant Location/run-map event code.
- Do not make future event times visible through cards or timeline markers.
- Do not allow canceling a selected Hazard or Character event.
- Do not add character-specific layout tuning fields when a shared portrait/slot field can serve all portraits.
- Do not inject one `System` into another `System`.
- Do not have draw functions mutate state.
- Do not use `MouseState` or `GamePad` state; all interaction continues through cursor/UI events and input contexts.

---

## 4. Target Public Data Contracts

Names below are normative unless an existing namespace conflict requires a mechanically equivalent name. If renamed, update this document's terminology in code comments and tests rather than changing behavior.

### 4.1 Structured loadout entry

Replace `LoadoutDefinition.cardIds` with:

```csharp
public class LoadoutCardEntry
{
    public string entryId { get; set; } = string.Empty;
    public string cardKey { get; set; } = string.Empty;
    public bool isStarter { get; set; }
    public bool countsAsTraded { get; set; }
    public List<string> restrictions { get; set; } = new();
}

public class LoadoutDefinition
{
    // Existing identity/equipment fields remain.
    public List<LoadoutCardEntry> cards { get; set; } = new();
}
```

Add to the root run save:

```csharp
public int nextRunDeckEntryId { get; set; }
```

Entry IDs use the stable format:

```text
run_card_0
run_card_1
run_card_2
...
```

Remove these root-save fields after all callers migrate:

```text
starterCardKeys
tradedCardKeys
runCardRestrictions
```

Update `RunDeckCard`:

```csharp
public class RunDeckCard : IComponent
{
    public Entity Owner { get; set; }
    public string EntryId { get; set; } = string.Empty;
    public string CardKey { get; set; } = string.Empty;
}
```

### 4.2 Entry provenance rules

| Mutation | New entry ID | `isStarter` | `countsAsTraded` | Preserve restrictions |
| --- | --- | --- | --- | --- |
| Starting deck creation | Yes | `true` | `false` | N/A |
| Direct card append/purchase | Yes | `false` | Preserve current purchase semantics; normally `false` | N/A |
| Encounter reward exchange | Yes | `false` | `true` | No |
| Climb replacement | Yes | `false` | `true` | No |
| Shop upgrade | No | Preserve | Preserve | Yes |
| Reward upgrade | No | Preserve | Preserve | Yes |
| Smith upgrade | No | Preserve | Preserve | Yes |
| Exhaust/removal | Entry ends | N/A | N/A | Entry and restrictions removed |

Any code that previously used a card key to identify one deck object must use `entryId`. Card keys remain content identity and continue encoding card ID, printed color, and upgraded state.

### 4.3 Climb event enums

```csharp
public enum ClimbEventKind
{
    Hazard,
    Character,
}

public enum ClimbHazardEffectType
{
    None,
    Colorless,
    Frozen,
    Brittle,
    Burn,
    Fear,
    Shackled,
    Scar,
}

public enum ClimbCharacterRewardType
{
    None,
    Temperance,
    Courage,
    Vigor,
    RandomCardUpgrade,
}

public enum ClimbEventStatus
{
    Scheduled,
    Active,
    Pending,
    Completed,
    Expired,
}

public enum ClimbEventFlowPhase
{
    None,
    HazardConfirmation,
    CharacterDialogue,
    CharacterSummary,
}
```

### 4.4 Saved event slot

Replace the current event slot shape with:

```csharp
public class ClimbEventSlotSave
{
    public string id { get; set; } = string.Empty;
    public string definitionId { get; set; } = string.Empty;
    public ClimbEventKind kind { get; set; }
    public ClimbHazardEffectType hazardEffect { get; set; }
    public ClimbCharacterRewardType characterReward { get; set; }
    public int scheduledAppearanceTime { get; set; }
    public int activatedAtTime { get; set; } = -1;
    public int duration { get; set; }
    public int timeCost { get; set; }
    public int effectAmount { get; set; }
    public ClimbResourceSave rewardResources { get; set; }
        = new() { red = 0, white = 0, black = 0 };
    public ClimbEventStatus status { get; set; } = ClimbEventStatus.Scheduled;
}
```

Invariants:

- Hazard `timeCost == 0`.
- Character `timeCost == 1`.
- Character `rewardResources` is always all zeroes.
- Character `hazardEffect == None`.
- Hazard `characterReward == None`.
- `activatedAtTime == -1` until activation.
- `Scheduled` slots are not rendered.
- `Pending` identifies the one slot referenced by `pendingEvent`.
- Completed and expired slots remain in the five-slot save list for auditability and deterministic counts but are not rendered.

### 4.5 Pending flow and next-battle state

```csharp
public class ClimbPendingEventSave
{
    public string eventSlotId { get; set; } = string.Empty;
    public ClimbEventFlowPhase phase { get; set; }
    public string dialogueRequestId { get; set; } = string.Empty;
}

public class ClimbNextBattleBonusSave
{
    public int courage { get; set; }
    public int temperance { get; set; }
    public int vigor { get; set; }
}

public class ClimbNextBattlePenaltySave
{
    public int burn { get; set; }
    public int fear { get; set; }
}
```

Add both next-battle objects to `ClimbSaveState`. Remove `shownEventTypeIds` and `nextEventSlotId`; the generated five-slot list is the complete event schedule.

### 4.6 Narrative modal request contract

Keep the existing legacy modal and factory fallback, but allow supplied content:

```csharp
public class NarrativeModalContent
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string ConfirmLabel { get; set; } = string.Empty;
}

public class ShowNarrativeEventOverlay
{
    // Existing legacy fields remain for dormant Location events.
    public string RunMapEventId { get; set; } = string.Empty;
    public string EventTypeId { get; set; } = string.Empty;

    // New supplied-content path.
    public string ResolutionContextId { get; set; } = string.Empty;
    public NarrativeModalContent Content { get; set; }
}

public class NarrativeModalChoiceRequested
{
    public string ResolutionContextId { get; set; } = string.Empty;
    public int ChoiceIndex { get; set; }
    public bool Handled { get; set; }
}
```

The modal closes supplied content only when the synchronous resolver sets `Handled = true`. Legacy `EventBase` options continue using their current path.

### 4.7 Generic dialogue sequence contract

Generalize the encounter-specific request/completion names so Climb Character dialogue is not mislabeled:

```csharp
public class DialogueSequenceRequested
{
    public string DefinitionId { get; set; } = string.Empty;
    public string SegmentId { get; set; } = string.Empty;
    public Guid RequestId { get; set; }
    public bool BackgroundOnly { get; set; }
}

public class DialogueSequenceCompleted
{
    public string DefinitionId { get; set; } = string.Empty;
    public string SegmentId { get; set; } = string.Empty;
    public Guid RequestId { get; set; }
}
```

Update existing guided tutorial and Fallen Shepherd callers. Untracked legacy `DialogEnded` behavior may remain for dialogues that do not require a correlated completion.

---

## 5. Hazard and Character Catalog

Create a dedicated `ClimbEventCatalog`. Do not add these definitions to the dormant `EventFactory` pool.

### 5.1 Hazard definitions

| Definition ID | Title | Effect | Amount |
| --- | --- | --- | --- |
| `bleached_standard` | `The Bleached Standard` | Colorless | 1 card |
| `winter_reliquary` | `The Winter Reliquary` | Frozen | 1 card |
| `glass_psalm` | `The Glass Psalm` | Brittle | 1 card |
| `cinders_in_the_censer` | `Cinders in the Censer` | Burn | 1 |
| `second_footsteps` | `The Second Footsteps` | Fear | 1-3 |
| `penitents_chain` | `The Penitent's Chain` | Shackled | 1-4 |
| `saint_of_old_wounds` | `The Saint of Old Wounds` | Scar | 1-2 |

Hazard narrative bodies, exactly as ASCII runtime strings:

#### The Bleached Standard

```text
A torn battle standard rises from the sand with no army beneath it. Its colors drain as you approach, and the blank cloth strains toward your deck like a hand seeking a name.
```

#### The Winter Reliquary

```text
An iron reliquary lies cold beneath the noon sun. When its seal breaks, a breath of winter coils around your deck and settles between the cards.
```

#### The Glass Psalm

```text
Wind uncovers a chapel made of glass-thin stone. A single hymn rings from its empty nave. One card answers, its fibers hardening until they sound like bone.
```

#### Cinders in the Censer

```text
A censer swings from a dead tree though the air is still. Its coals flare when you pass, searing a mark into your flesh before dropping bright offerings into the dust.
```

#### The Second Footsteps

```text
At dusk, a second set of footprints appears beside yours. They stop when you stop. They begin again one step before you move.
```

#### The Penitent's Chain

```text
Half-buried chains rattle beneath a weathered shrine. When you pull free the offering tangled among them, the loose links coil around your wrists and vanish beneath the skin.
```

#### The Saint of Old Wounds

```text
A cracked saint's effigy kneels beside a dry well. The stone bears wounds that match no martyr's tale. When you take the coins at its feet, the cuts open across your own skin.
```

### 5.2 Hazard modal mechanics block

Append two explicit paragraphs or lines beneath the narrative body:

```text
Effect: {effect summary}
Gain: {resource summary}
```

Use `Confirm` as the sole button label.

Effect summary rules:

| Effect | Summary |
| --- | --- |
| Colorless | `One random deck card becomes Colorless.` |
| Frozen | `One random deck card becomes Frozen.` |
| Brittle | `One random deck card becomes Brittle.` |
| Burn | `Start the next battle with 1 Burn.` |
| Fear | `Start the next battle with {amount} Fear.` |
| Shackled | `Gain {amount} Shackled.` |
| Scar, amount 1 | `Gain 1 Scar.` |
| Scar, amount >1 | `Gain {amount} Scars.` |
| Restriction with no eligible entry | `No deck card can be affected.` |

Resource summary formatting is ASCII and omits zeroes. Examples:

```text
1 Red
2 White
1 Red, 1 Black
```

### 5.3 Character definitions

| Definition ID | Actor | Portrait asset | Reward | Card GAIN text |
| --- | --- | --- | --- | --- |
| `nun_counsel` | Nun | `character/nun` | +2 Temperance next battle | `+2 TEMPERANCE` / `NEXT BATTLE` |
| `reverent_crusader_counsel` | Reverent Crusader | `character/reverent_crusader` | +2 Courage next battle | `+2 COURAGE` / `NEXT BATTLE` |
| `revered_crusader_training` | Revered Crusader | `character/revered_crusader` | +1 Vigor next battle | `+1 VIGOR` / `NEXT BATTLE` |
| `smith_forging` | Smith | `character/smith` | Random eligible card upgrade | `RANDOM CARD` / `UPGRADE` |

Every Character exchange is exactly two lines, one per speaker.

#### Nun exchange

```text
Nun: You carry every wound as if suffering were proof of purpose. Take two measured breaths before you draw steel.
Crusader: Pain is easier to trust than mercy. But I will take the breaths.
```

Summary title and body:

```text
Measured Grace

The nun's measured counsel steadies your spirit. Gain 2 Temperance in the next battle.
```

#### Reverent Crusader exchange

```text
Reverent Crusader: Your guard is sound, but your heart enters battle after your blade. Courage is command over doubt. Remember that.
Crusader: My blade has fewer doubts. I will teach my heart to follow.
```

Summary title and body:

```text
A Steadier Heart

The reverent crusader's conviction hardens your resolve. Gain 2 Courage in the next battle.
```

#### Revered Crusader exchange

```text
Revered Crusader: You waste strength fighting the weight of your own armor. Set your feet, loosen your shoulders, and let it serve you.
Crusader: Armor is meant to be carried. Show me how to carry it well.
```

Summary title and body:

```text
Strength Without Waste

The revered crusader strips wasted motion from your stance. Gain 1 Vigor in the next battle.
```

#### Smith exchange

```text
Smith: That card has seen hard use. I cannot mend it, but I can make it worthy of your hand.
Crusader: Then strike while the iron still fears you.
```

Eligible-target summary:

```text
The Smith's Work

The smith raises his hammer over your deck. A random eligible card gains an upgrade.
```

No-target summary:

```text
The Smith's Work

The smith studies every card, then lowers his hammer. Nothing remains for him to improve.
```

Use `Proceed` as the sole Character summary button. Keep the selected Smith target hidden before resolution; do not render the chosen card or name it.

---

## 6. Runtime State Machines

### 6.1 Hazard state machine

```text
Scheduled
  -> Climb time reaches/crosses appearance
Active
  -> player selects card
Pending: HazardConfirmation
  -> modal Confirm succeeds
Completed
```

Alternative terminal paths:

```text
Active -> duration boundary reached -> Expired
Scheduled/Active -> Climb reaches MaxTime -> Expired, Final encounter starts
Pending -> application restart -> reopen HazardConfirmation
```

Selection procedure:

1. Reject if not in `SceneId.Climb`, another event is pending, slot is not Active, or modal/dialog input context is active.
2. Set slot status to Pending.
3. Save `pendingEvent` with `HazardConfirmation`.
4. Persist before opening the modal.
5. Build dynamic mechanics lines against the current deck:
   - If a restriction has eligible entries, state the normal effect.
   - If none exists, state `No deck card can be affected.`
6. Publish supplied narrative-modal content.
7. Do not mutate resources, passives, restrictions, or time yet.

Confirmation transaction:

1. Reload and validate pending slot/context.
2. Apply Climb resources.
3. Resolve the effect.
4. Mark slot Completed.
5. Clear pending event.
6. Persist all save mutations once.
7. Synchronize live ECS entities from the committed save.
8. Mark the modal choice handled so the modal closes.

### 6.2 Character state machine

```text
Scheduled
  -> Climb time reaches/crosses appearance
Active
  -> player selects card
Pending: CharacterDialogue
  -> dialogue completes or is skipped
Pending: CharacterSummary
  -> modal Proceed succeeds
Completed
```

Alternative paths:

```text
Active -> duration boundary reached -> Expired
Scheduled/Active -> Climb reaches MaxTime -> Expired, Final encounter starts
CharacterDialogue -> application restart -> restart dialogue from line one
CharacterSummary -> application restart -> reopen summary
```

Selection procedure:

1. Validate the Active slot and input state.
2. Mark slot Pending with `CharacterDialogue`.
3. Generate and persist a new dialogue request ID.
4. Persist before publishing dialogue.
5. Publish `DialogueSequenceRequested` with `BackgroundOnly = true`.
6. Do not advance time or grant the reward.

Dialogue completion procedure:

1. Match the completion request ID to the pending event.
2. Change phase to `CharacterSummary`.
3. Persist.
4. Open the supplied summary modal.
5. Do not advance time or grant the reward.

Proceed transaction:

1. Reload and validate the pending Character slot/context.
2. Apply its reward:
   - Add bonus fields, or
   - Resolve the Smith upgrade.
3. Mark the selected slot Completed before lifecycle processing.
4. Clear pending state.
5. Advance Climb time by exactly one through the shared Climb-time rules.
6. Refresh shop slots when crossing a shop boundary.
7. Expire and activate other event slots at the new landing time.
8. Replenish/reroll encounter slots according to existing rules.
9. Persist all save mutations once.
10. Synchronize live entities and invoke card upgrade callbacks after the save commit.
11. Close the summary.
12. If time reached `MaxTime`, queue the Final encounter after the event has fully resolved.

### 6.3 Event lifecycle update ordering

Whenever Climb time changes and remains below maximum:

1. Leave the pending slot untouched.
2. Expire Active slots where:

```text
currentTime >= activatedAtTime + duration
```

3. Activate Scheduled slots where:

```text
scheduledAppearanceTime <= currentTime
```

4. Set every newly active slot's `activatedAtTime` to the current landing time.
5. Persist only when lifecycle state changed.

At `currentTime >= MaxTime`:

1. Complete the currently resolving event first if it caused the advance.
2. Mark every other Scheduled or Active event Expired.
3. Do not activate newly crossed schedules.
4. Persist.
5. Queue the Final encounter once.

### 6.4 Deterministic card targeting

Restriction Hazard and Smith targets are chosen uniformly from eligible structured entries, not unique card keys.

Eligibility:

- Restriction Hazard:
  - Entry has a valid non-weapon, non-token loadout card.
  - Entry does not already contain the same restriction.
- Smith:
  - Entry has a valid non-weapon, non-token loadout card.
  - Card key is not upgraded.
  - `BuildUpgradedCardKey` returns a valid key.

Sort eligible entries by `entryId`, then choose with a deterministic resolution RNG derived from run seed plus event slot ID. This makes tests and reload behavior stable without exposing the target before resolution.

If no restriction target exists, apply no restriction and still grant Hazard resources.

If no Smith target exists, apply no upgrade and still charge one Character-event time.

---

## 7. Stable Deck-Entry Migration

This is a prerequisite, not an optional cleanup. The new card-restriction effects are incorrect without it.

### 7.1 Save and loadout conversion

- Update `LoadoutDefinition` and every initializer/test fixture from strings to `LoadoutCardEntry` objects.
- Starting deck generation may continue returning card keys internally, but run creation must wrap those keys in newly allocated structured entries.
- Clone every entry and its restrictions in save snapshot/clone methods; never return shared restriction-list references from repository helpers.
- Save version mismatch discards old shape; do not deserialize or convert an old `cardIds` list.
- Ensure new default/fresh/inactive saves initialize the counter and empty structured lists.

### 7.2 RunDeckService conversion

- Replace key-indexed dictionaries with entry-ID dictionaries.
- `EnsureRunDeck` must create exactly one ECS card entity per structured entry, including identical keys.
- Reconcile by `EntryId`, not `CardKey`.
- When an existing entry's key changes because of an upgrade, update or recreate only that entry's ECS representation without conflating duplicates.
- Hydrate restrictions from the entry's restriction list.
- Preserve `CardBase.IsStarter` from `entry.isStarter`.
- Count upgraded/traded deck weight from structured entries.
- Any debug/test-fight conversion may generate temporary entry IDs local to the fixture, but active-run saves always use allocated run IDs.

### 7.3 Mutation API conversion

Provide repository-level atomic methods that operate by entry ID and ordered position:

- Add a new card entry.
- Remove/exhaust one entry.
- Replace/exchange one entry with a newly allocated entry at the same position.
- Upgrade an entry in place.
- Add/remove/set one entry's restrictions.
- Resolve a Hazard transaction.
- Resolve a Character transaction.

Mutation methods return enough result data for the owning system to publish ECS events after persistence. Save/repository code must not directly control display systems.

### 7.4 Offer and selection conversion

Convert all persisted and runtime selection targets that currently rely on key or index alone:

- Deck reward exchange target.
- Deck reward upgrade target.
- Climb shop upgrade target.
- Climb replacement selection.
- Card-list modal selection metadata.
- Exhaust/removal requests that represent a persistent run card.

An index may still be carried as presentation metadata, but persistence validation and mutation identity must use `entryId`.

### 7.5 Restriction lifecycle

- Keep the existing restriction names:
  - `Frozen`
  - `Sealed`
  - `Brittle`
  - `Colorless`
- Do not persist battle-local `Shackle` card markers.
- `RunScopedStateService` reads/writes entry restrictions.
- Updating or removing one duplicate entry must not alter restrictions on another identical key.
- Upgrade retains restrictions on the same entry.
- Replacement creates an unrestricted new entry.

---

## 8. Effect Resolution Details

### 8.1 Colorless, Frozen, and Brittle

- Each effect applies to exactly one eligible entry.
- Add the restriction name to the entry if absent.
- Do not apply duplicate markers.
- Hydrate the matching live ECS card by `EntryId` after commit.
- Existing display and gameplay components remain the combat representation:
  - `Colorless`
  - `Frozen`
  - `Brittle`
- A card may simultaneously have different restrictions.
- When no eligible entry exists, resolve as resources-only.

### 8.2 Burn and Fear

- Do not apply these components while still in the Climb.
- Add the rolled amount to saved next-battle penalties.
- Burn amount is always one per resolved Burn Hazard.
- Fear amount is the persisted 1-3 roll.
- Repeated Hazards add their values.

### 8.3 Shackled and Scar

- Apply immediately as run-long player passives.
- Use `AppliedPassiveType.Shackled` and `AppliedPassiveType.Scar`.
- Do not create a new `Shackles` passive and do not use card-level `Shackle` markers.
- Add to existing saved stacks.
- Hydrate the live persistent player after commit.
- Preserve existing Scar max-HP semantics, including the documented start-of-battle removal rule.

### 8.4 Character bonuses

- Nun adds two to saved next-battle Temperance.
- Reverent Crusader adds two to saved next-battle Courage.
- Revered Crusader adds one to saved next-battle Vigor.
- These values are not live battle components while in the Climb.
- Multiple future sources add rather than replace.

### 8.5 Smith upgrade

- Resolve only on `Proceed`.
- Choose one eligible entry uniformly and deterministically.
- Keep the target hidden in the summary.
- Upgrade the card key in place on the existing entry.
- Preserve entry ID, provenance, and restrictions.
- Update the live run-deck entity after commit.
- Invoke `CardUpgradeService.InvokeUpgradeConfirmed` exactly once for a successful upgrade.
- If no eligible entry exists, resolve successfully without an upgrade.

### 8.6 Applying next-battle state

In `BattleSceneSystem.InitBattle`, detect the first queued encounter before incrementing `QueuedEvents.CurrentIndex`:

```text
queued.IsClimbEncounter == true
queued.CurrentIndex == -1
```

After the player exists, managers are subscribed, and standard resets have run:

1. Publish Courage modification for the saved Courage bonus.
2. Publish Temperance modification for the saved Temperance bonus.
3. Publish `ApplyPassiveEvent` for Vigor.
4. Publish `ApplyPassiveEvent` for Burn.
5. Publish `ApplyPassiveEvent` for Fear.
6. Clear the saved next-battle package so later queued encounters do not regrant it.

Do not override each effect's established subsequent lifetime behavior.

---

## 9. Climb Presentation Specification

### 9.1 Shared event-card geometry

Hazard and Character cards use the same outer geometry as Encounter cards:

- Same slot height.
- Same top visual region height.
- Same border/fill structure.
- Same bottom meta row.
- Same GAIN block geometry.
- Same time/duration block geometry.
- Same hover ring and preview glow behavior.

Remove or stop using a separate compact `EventSlotHeight`. The Event column must lay out active event cards with the shared portrait-slot height.

### 9.2 Debug field consolidation

Rename encounter-specific fields to generic names and use them for Encounter, Hazard, and Character rendering:

| Existing concept | Target shared concept |
| --- | --- |
| `EncounterSlotHeight` | `PortraitSlotHeight` or another generic shared name |
| `EnemyPortraitHeight` | `PortraitRegionHeight` |
| `EncounterTimeBlockWidth` | `PortraitSlotTimeBlockWidth` |
| `EncounterTitleMaxLength` | `PortraitSlotTitleMaxLength` |

Add one shared `PortraitCropTopBias` with `DebugEditable`, `Step = 0.01f`. Pass it to `DrawPortraitCropped` for enemy and Character portraits.

Do not add separate Nun, Smith, Reverent, or Revered crop/scale settings unless visual snapshot evidence proves a shared field cannot render the supplied assets acceptably.

All text-scale `DebugEditable` fields use `Step = 0.01f`.

### 9.3 Hazard card

Upper region:

- Draw the existing event/question glyph treatment at encounter scale.
- Draw `Hazard` as the generic title.
- Do not show the authored narrative title.
- Do not show or hint at the harmful effect.

Bottom region:

- Left block label: `GAIN`.
- Draw the persisted Red/White/Black reward through the existing encounter `DrawRewardMetaBlock` and resource icon helpers.
- Right block shows `+0` Climb time and remaining active duration.

### 9.4 Character card

Upper region:

- Load and crop the supplied portrait through the same portrait renderer used by encounters.
- Preserve the shared radial portrait backdrop and bottom separator treatment.

Bottom region:

- Left block label remains `GAIN` by explicit product decision.
- Render two compact ASCII lines:
  - Mechanical reward.
  - `NEXT BATTLE`, except Smith uses `UPGRADE` as the second line.
- Right block shows `+1` Climb time and remaining active duration.

### 9.5 Event column and visibility

- Hide the Event column when no slot is Active or Pending.
- Render Active slots sorted by `activatedAtTime`, then stable slot ID.
- Pending slots are hidden behind their modal/dialog flow and must not remain clickable.
- Full-height cards fit because the five-band schedule and duration limits prevent more than adjacent-band overlap under current maximum action costs. Add a rule test for the expected concurrency bound; do not silently truncate active slots.
- If a future rules change violates the bound, fail a debug assertion or test rather than hiding active events.

### 9.6 Hover preview

Current preview discovery incorrectly requires `TimeCost > 0`. Change it so zero-time Hazard cards can be preview sources.

Hazard preview:

- `IsActive = true` while hovered.
- Projected time remains current time.
- Projected resources include the persisted Hazard reward.
- Source glow is shown.
- No unrelated slot expires from the zero-time preview.

Character preview:

- Project time by one.
- Do not project Red/White/Black resource changes.
- Show which active events, encounters, and shop state would vanish or change after the one-time advance.
- At time `MaxTime - 1`, show the Final-time projection according to existing timeline presentation.

### 9.7 Background-only Character dialogue

Add a background-only draw mode driven by `DialogOverlayState.BackgroundOnly` and the active scene.

When active in `SceneId.Climb`:

1. Draw `desert_background_location` as the normal full-screen cover.
2. Do not draw `BackgroundDimAlpha`; the image must be undimmed.
3. Do not draw Climb header, columns, slots, tooltips, or other Climb UI.
4. Skip global overlays including reward modal, narrative modal, card list, tutorials, tooltips, alerts, profiler, debug menu, entity list, hotkey overlays, and border debug.
5. Draw the dialogue overlay, its Skip control, and the cursor.
6. Do not affect normal Battle dialogue drawing.

Keep draw code state-free. Query shared overlay component state; do not pass `DialogDisplaySystem` into `ClimbSceneSystem`.

### 9.8 Content pipeline and portrait mapping

Add all four existing user-provided assets to `Content/Content.mgcb`:

```text
Content/character/nun.png
Content/character/reverent_crusader.png
Content/character/revered_crusader.png
Content/character/smith.png
```

Use asset names:

```text
character/nun
character/reverent_crusader
character/revered_crusader
character/smith
```

Extend dialogue portrait resolution:

| Actor string | Asset |
| --- | --- |
| `Nun` | `character/nun` |
| `Reverent Crusader` | `character/reverent_crusader` |
| `Revered Crusader` | `character/revered_crusader` |
| `Smith` | `character/smith` |
| `Crusader` | Existing `CrusaderPortraitAssets.DialogPortraitAsset` |

Preserve texture caching and cache deletion behavior.

---

## 10. System Ownership and Event Flow

### 10.1 ClimbEventSystem responsibilities

`ClimbEventSystem` owns:

- Lifecycle activation and expiration.
- Pending-flow resume on Climb load.
- `ClimbEventSlotSelectedEvent` handling.
- Narrative modal confirmation handling.
- Dialogue completion correlation.
- Atomic save resolution coordination.
- ECS hydration/publishing after committed mutations.
- Final encounter handoff after Character resolution reaches maximum time.

It does not draw.

### 10.2 ClimbEventRuleService responsibilities

Keep services read-only/pure:

- Build the five-event initial schedule from a seed.
- Calculate bands.
- Roll amounts/durations/resources.
- Determine visibility/expiration/activation candidates.
- Build resource and effect summary text.
- Return eligible entry lists or calculate deterministic target indices.
- Return proposed state transitions; do not persist, publish events, or mutate ECS components.

### 10.3 UI delegate responsibilities

For `UIElementEventType.ClimbEventSlotSelect`:

- Validate Climb scene and preview click blocking.
- Publish `ClimbEventSlotSelectedEvent` with slot ID.
- Do not call a mutating service.
- Remove the current direct call to `ClimbEventService.TryLaunchEvent`.

### 10.4 Narrative modal responsibilities

- Render legacy factory-backed content or supplied content.
- Own button entities and input context.
- Publish a resolution request.
- Close only after the resolver handles it.
- Never mutate Climb save, resources, deck entries, or passives.

### 10.5 Dialogue responsibilities

- Render and advance a requested sequence.
- Carry the `BackgroundOnly` presentation flag on overlay state.
- Publish correlated completion.
- Never apply Character rewards or Climb time.

### 10.6 Save repository responsibilities

- Perform locked, atomic mutations and one persistence write per event resolution.
- Validate pending slot and entry identity.
- Return resolution results for ECS synchronization.
- Never draw or directly control another system.

---

## 11. Multi-Agent Work Breakdown

### 11.1 Coordination rules

- Agents work in dependency waves, not all at once.
- Do not allow two agents to edit `SaveFile.cs`, `SaveCache.cs`, `Game1.cs`, or `CONTEXT.md` concurrently.
- Each agent must inspect current working-tree changes before editing and preserve unrelated user changes.
- `todo.txt` is user-owned and out of scope.
- `Content/character/` contains user-provided assets; use them but do not replace or modify the image files.
- Every handoff must include:
  - Files changed.
  - Public contracts introduced or changed.
  - Tests run.
  - Known compile failures that are intentionally waiting on a downstream wave.
- Intermediate agents run targeted tests. The final integration agent runs the full suite and build.

### 11.2 Dependency graph

```text
Wave 1: Agent A - Stable deck-entry foundation
               |
               v
Wave 2: Agent B - Event save model, catalog, generation, runtime core
               |
        +------+------------------+
        |                         |
        v                         v
Wave 3: Agent C - Modal/dialog flow     Agent D - Climb presentation/assets
        |                         |
        +-------------+-----------+
                      v
Wave 4: Agent E - Battle consumption and application integration
                      |
                      v
Wave 5: Agent F - Documentation, snapshots, full integration, verification
```

Agent C and Agent D may work in parallel only after Agent B's event contracts compile. Agent E starts after both the deck and runtime contracts stabilize. Agent F owns all final conflict resolution.

---

## 12. Agent A - Stable Deck-Entry Foundation

### Mission

Replace key-only run-deck persistence with stable structured entries and migrate every existing deck mutation path before event effects depend on it.

### Primary file ownership

- `ECS/Data/Loadouts/LoadoutDefinition.cs`
- `ECS/Components/RunDeckComponents.cs`
- `ECS/Data/Save/SaveFile.cs` for deck-entry/root-field changes.
- `ECS/Data/Save/SaveCache.cs` for entry allocation and mutation APIs.
- `ECS/Services/RunDeckService.cs`
- `ECS/Services/RunScopedStateService.cs`
- `ECS/Services/StartingDeckGeneratorService.cs`
- `ECS/Services/QuestCardRewardService.cs`
- `ECS/Services/ClimbShopService.cs`
- Related card-list/reward selection metadata and tests.

### Required implementation

1. Add `LoadoutCardEntry` and replace `cardIds` with `cards`.
2. Add root entry counter and remove key-based starter/traded/restriction fields.
3. Assign entry IDs at run creation.
4. Convert SaveCache clone/default/new-run logic.
5. Reconcile ECS deck entities by entry ID.
6. Support identical card keys without `ToDictionary` collisions.
7. Move restriction persistence onto entries.
8. Convert add, remove, exhaust, replacement, exchange, and upgrade operations.
9. Convert pending deck reward and Climb shop targets to entry IDs.
10. Preserve exact provenance behavior used by enemy health deck weighting.
11. Update all tests and fixtures that initialize loadouts.

### Agent A acceptance tests

- Two identical card keys produce two active run-deck entities with different entry IDs.
- Restricting one duplicate does not restrict the other.
- Save reload recreates both entities and their independent restrictions.
- Upgrade preserves entry ID and restrictions.
- Replacement creates a different entry ID at the same position and clears restrictions.
- Exhaust removes only the targeted entry.
- Starting/traded deck weight matches pre-refactor behavior for equivalent decks.
- Existing shop and deck reward tests pass after being rewritten for structured entries.

### Handoff contract

Agent A must publish the final names/signatures for:

- Entry allocation.
- Entry lookup.
- Entry upgrade.
- Entry replacement.
- Entry restriction mutation.
- ECS lookup by entry ID.

Agent B may not invent parallel mutation APIs.

---

## 13. Agent B - Event Model, Catalog, Generation, and Runtime Core

### Mission

Implement the five-event schedule, pure lifecycle rules, pending flow, effect transactions, and system-owned event orchestration.

### Dependency

Starts after Agent A's structured entry APIs compile.

### Primary file ownership

- `ECS/Data/Save/SaveFile.cs` for Climb event fields, after Agent A handoff.
- `ECS/Data/Save/SaveCache.cs` for atomic event transactions, after Agent A handoff.
- `ECS/Events/ClimbEvents.cs`
- `ECS/Services/ClimbRuleService.cs`
- New `ECS/Data/Climb/ClimbEventCatalog.cs` or equivalent data namespace.
- New `ECS/Systems/ClimbEventSystem.cs` or scene-appropriate system path.
- `ECS/Scenes/UIElementEventDelegateSystem.cs` event-selection branch only.
- Event generation/runtime unit tests.

### Required implementation

1. Add event enums and saved state.
2. Remove replenishing/shown-history behavior from canonical Climb event generation.
3. Generate exactly five instances at run creation.
4. Implement deterministic band allocation and rolls.
5. Implement landing activation and end-exclusive expiration.
6. Implement Final-time preemption.
7. Implement Hazard/Character selection validation and pending phases.
8. Implement Hazard confirmation transaction.
9. Implement Character Proceed transaction.
10. Implement deterministic entry targeting through Agent A APIs.
11. Implement next-battle bonus/penalty stacking.
12. Implement interruption resume signals on `LoadSceneEvent` for Climb.
13. Ensure save writes occur only when lifecycle or resolution state changes, not every frame.
14. Remove direct mutating service invocation from the UI delegate.

### Agent B acceptance tests

- Exactly three Hazards and two Characters are generated.
- Hazards may repeat; Characters do not.
- Band times remain in the five exact ranges for MaxTime 32.
- Same seed produces identical complete schedule.
- Different seeds can vary definitions, amounts, resources, durations, and times.
- Amount and duration ranges are exact.
- Hazard rewards contain one or two independently rolled pips.
- Landing activation grants full duration.
- Duration is end-exclusive.
- Multiple crossed events activate together.
- Final time expires unresolved events without activating newly crossed ones.
- Pending events do not expire.
- Repeated next-battle values add.
- Restriction no-target result grants resources only.
- Smith no-target result succeeds with no upgrade and still charges one time.
- No effect or reward applies before Confirm/Proceed.
- Confirm/Proceed is idempotent under repeated request delivery.

### Handoff contract

Agent B provides Agent C and D with:

- Catalog read APIs.
- Active slot view fields.
- Modal request context IDs.
- Pending phase behavior.
- Event status/visibility helpers.
- Character actor/portrait/reward strings.

---

## 14. Agent C - Narrative Modal and Dialogue Generalization

### Mission

Make the shared modal and dialogue systems support the new flows without taking ownership of Climb state.

### Dependency

Starts after Agent B's public event contracts compile.

### Primary file ownership

- `ECS/Events/SceneEvents.cs`
- `ECS/Events/CombatEvents.cs` for dialogue-event generalization.
- `ECS/Components/Scenes.cs`
- `ECS/Scenes/NarrativeEventModalDisplaySystem.cs`
- `ECS/Scenes/BattleScene/DialogDisplaySystem.cs`
- `ECS/Data/Dialog/DialogCatalog.cs`
- Existing guided tutorial/Fallen Shepherd dialogue callers needed for renamed generic events.
- Modal/dialog unit and snapshot-fixture support, excluding final registry edits owned by Agent F.

### Required implementation

1. Add supplied narrative content and correlated choice request.
2. Preserve legacy `EventFactory` behavior when supplied content is absent.
3. Close supplied modal only after resolution acknowledges success.
4. Preserve one-to-three legacy option support.
5. Generalize encounter dialogue request/completion names.
6. Carry `BackgroundOnly` on dialogue overlay state.
7. Add four two-line Character definitions.
8. Add actor portrait mappings.
9. Ensure dialogue Skip publishes the same correlated completion as normal line completion.
10. Ensure no unsupported glyph reaches a SpriteFont draw.
11. Do not apply Climb effects or time here.

### Agent C acceptance tests

- Legacy Icebound Tithe/Pruned Vocation modal snapshots still open through the factory path.
- Supplied Hazard content renders title, narrative, Effect, Gain, and one Confirm button.
- Unhandled supplied choice does not silently complete the modal.
- Handled supplied choice closes once.
- Character dialogue resolves exactly two lines and correlated completion.
- Skip produces correlated completion.
- BackgroundOnly state is active only for requested Character dialogue.
- Reload/resubmission can reopen dialogue from the first line.
- All authored strings are ASCII-safe.

---

## 15. Agent D - Climb Presentation and Content Assets

### Mission

Implement full-height Hazard and Character cards, shared encounter geometry, zero-time previews, and content compilation.

### Dependency

Starts after Agent B exposes final slot/catalog fields. May run in parallel with Agent C.

### Primary file ownership

- `ECS/Components/ClimbComponents.cs`
- `ECS/Scenes/ClimbScene/ClimbColumnLayoutSystem.cs`
- `ECS/Scenes/ClimbScene/ClimbColumnDisplaySystem.cs`
- `ECS/Scenes/ClimbScene/ClimbHeaderLayoutSystem.cs`
- `ECS/Scenes/ClimbScene/ClimbSceneDrawHelpers.cs`
- `ECS/Scenes/ClimbScene/ClimbBackgroundDisplaySystem.cs`
- `Content/Content.mgcb`
- Climb fixture setup code needed to produce renderable states, excluding registry/docs owned by Agent F.

### Required implementation

1. Replace compact event rows with shared encounter-style cards.
2. Generalize debug field names and static mirrored values.
3. Add shared portrait crop bias.
4. Render generic Hazard glyph and title without effect hints.
5. Reuse exact encounter GAIN renderer for Hazard resources.
6. Render Character portraits through shared crop helper.
7. Render Character reward text in a GAIN block.
8. Show correct `+0` or `+1` time and remaining duration.
9. Render only Active slots.
10. Support zero-time Hazard preview and resource projection.
11. Add character assets to MGCB without editing the PNG files.
12. Preserve texture caching and `DeleteCachesEvent` handling.
13. Add undimmed-background draw capability, but leave global draw gating to Agent F.

### Agent D acceptance tests

- Hazard and Character cards use identical outer and bottom-row geometry to Encounter cards.
- Changing shared portrait/debug fields affects enemies and Characters.
- Hazard reward icons match Encounter reward icons.
- Hazard hover changes projected resources but not time.
- Character hover changes projected time but not Climb resources.
- Character portraits load from all four supplied assets.
- Character reward text fits without unsupported glyphs or clipping.
- No separate per-character magic layout values are introduced.

---

## 16. Agent E - Battle Consumption and Runtime Application Integration

### Mission

Integrate next-battle packages and live ECS synchronization with existing battle/passive/resource lifecycles.

### Dependencies

- Agent A structured entries.
- Agent B next-battle save contract and resolution results.
- Agent C generic dialogue completion contract.

### Primary file ownership

- `ECS/Scenes/BattleScene/BattleSceneSystem.cs`
- Targeted changes to Courage/Temperance/passive event integration only when required.
- Run-player/run-deck hydration calls needed after event resolution.
- Integration tests for first queued encounter behavior.

### Required implementation

1. Detect first queued encounter of a Climb encounter.
2. Apply bonuses after normal reset ordering.
3. Apply penalties/passives through existing events.
4. Clear the saved package exactly once.
5. Do not reapply on later queued encounters.
6. Preserve normal lifetime behavior after grant.
7. Ensure Final encounter receives pending values.
8. Ensure immediate Shackled/Scar and card restrictions hydrate correctly before the next battle.

### Agent E acceptance tests

- Courage starts at reset value plus pending bonus.
- Temperance receives pending bonus once.
- Vigor, Burn, and Fear appear on the player once.
- Second queued encounter does not receive a second grant.
- Existing carry/reset rules apply afterward.
- Final encounter consumes pending state.
- Empty pending state changes nothing.

---

## 17. Agent F - Integration, Documentation, Snapshots, and Final Verification

### Mission

Own high-conflict registration/draw files, reconcile all workstreams, update domain documentation, add visual coverage, and deliver a clean build.

### Dependency

Starts after Agents A-E hand off compiling changes or clearly documented integration gaps.

### Primary file ownership

- `Game1.cs`
- `ECS/Diagnostics/Snapshots/DisplaySnapshotRegistry.cs`
- `ECS/Diagnostics/Snapshots/Fixtures/ClimbSnapshotFixture.cs`
- Narrative/dialog snapshot variants as needed.
- `docs/display-snapshots.md`
- `CONTEXT.md`
- `docs/adr/0003-run-map-shops.md` status update.
- New `docs/adr/0009-time-based-climb.md`.
- New `docs/adr/0010-stable-deck-entry-identity.md`.
- Clarification to `docs/adr/0007-persisted-deck-reward-offers.md`.
- Final test fixes that do not change approved behavior.

### Required integration

1. Register `ClimbEventSystem` once globally.
2. Update global draw gating for background-only Character dialogue.
3. Ensure Climb background draws undimmed in that mode.
4. Ensure normal Battle dialogue remains unchanged.
5. Resolve any public contract compile errors across workstreams.
6. Update snapshot fixtures from obsolete event shapes to five-slot state.
7. Add new fixtures/variants and commands.
8. Rewrite canonical docs and ADRs.
9. Run full automated and visual verification.

### Required snapshot fixtures

Add or revise fixtures so all of these can be captured headlessly:

```text
climb-no-events
climb-hazard-event
climb-character-event
climb-hazard-hover-preview
climb-character-hover-preview
climb-hazard-confirmation
climb-character-summary
climb-character-dialog
```

Snapshot expectations:

- `climb-hazard-event`: generic Hazard upper region, GAIN pips, +0 time, remaining duration.
- `climb-character-event`: supplied portrait, GAIN reward text, +1 time, remaining duration.
- `climb-hazard-hover-preview`: projected resources change, timeline does not.
- `climb-character-hover-preview`: timeline changes, resources do not.
- `climb-hazard-confirmation`: exact Effect and Gain lines, one Confirm button.
- `climb-character-summary`: present-tense narrative/effect, one Proceed button.
- `climb-character-dialog`: undimmed desert background, no Climb/global foreground, dialogue visible.

### Final verification commands

```bash
dotnet test
dotnet run -- snapshot climb-hazard-event
dotnet run -- snapshot climb-character-event
dotnet run -- snapshot climb-hazard-hover-preview
dotnet run -- snapshot climb-character-hover-preview
dotnet run -- snapshot climb-hazard-confirmation
dotnet run -- snapshot climb-character-summary
dotnet run -- snapshot climb-character-dialog
dotnet build
```

If shader compilation is unavailable on the host, use the repository's documented MGFXC Wine setup. Do not mark implementation complete while normal `dotnet build` has compile errors.

---

## 18. Documentation Plan

### 18.1 CONTEXT.md rewrite

Keep `CONTEXT.md` implementation-free and glossary-only.

Add or rewrite concise definitions for:

- Climb.
- Climb time as a capped progression meter, not real time.
- Climb resource and Climb resource pip.
- Climb encounter in the active slot model.
- Climb event.
- Hazard event.
- Character event.
- Next-battle bonus.
- Next-battle penalty.
- Final encounter triggered by maximum Climb time.
- Deck card entry with stable run identity.
- Run-long card restriction as entry-owned.

Remove canonical spatial definitions for:

- Root Climb encounter.
- Climb tree.
- Climb coverage.
- Encounter reveal.
- Map fog range.
- Map fog.
- Reveal cutscene.
- Spatial Shop reveal.
- Spatial Treasure Chest.
- Spatial Climb event reveal.

Retain short legacy terms only where necessary to explain dormant `RunMap`, `Location`, `Combat node`, or `Narrative event` code. Do not maintain a second full legacy model in the glossary.

### 18.2 ADR 0009

Create `docs/adr/0009-time-based-climb.md` recording:

- The active Climb uses capped time and generated Shop/Encounter/Event slots.
- It replaces spatial topology as the canonical model.
- Maximum time triggers the Final encounter.
- Dormant Location/run-map code remains temporarily compiled but does not define domain behavior.
- This supersedes the spatial assumptions of ADR 0003 and the landmark-specific portion of ADR 0008.

Mark ADR 0003 as superseded by ADR 0009 rather than deleting its history.

### 18.3 ADR 0010

Create `docs/adr/0010-stable-deck-entry-identity.md` recording:

- Card key is content identity, not run-instance identity.
- Structured entry ID is required because duplicate card keys are valid.
- Restrictions belong to entries.
- Upgrade preserves identity.
- Replacement/exchange creates new identity.
- The rejected key-based and parallel-list alternatives could not represent one-card effects safely.

### 18.4 ADR 0007 clarification

Clarify that "replaces the targeted loadout entry in place" means preserving ordered deck position, not preserving run-entry identity. The outgoing entry ends and the incoming card receives a new entry ID.

---

## 19. Detailed Test Matrix

### 19.1 Generation

| Scenario | Expected |
| --- | --- |
| New run | Exactly five saved event instances |
| Kind count | Exactly three Hazard, two Character |
| Hazard selection | Definitions may repeat |
| Character selection | Definition IDs distinct |
| Determinism | Same seed gives byte-equivalent event schedule fields |
| Band allocation | One appearance inside each exact band |
| Hazard duration | Every value in 2-4 |
| Character duration | Every value in 3-5 |
| Colorless/Frozen/Brittle/Burn amount | Exactly 1 |
| Fear amount | 1-3 |
| Shackled amount | 1-4 |
| Scar amount | 1-2 |
| Hazard reward total | 1-2 pips |
| Character reward resources | All zero |

### 19.2 Lifecycle

| Scenario | Expected |
| --- | --- |
| Time below appearance | Scheduled and hidden |
| Land exactly on appearance | Activate at landing |
| Jump past appearance | Activate at landing with full duration |
| Cross two schedules | Both activate at landing |
| Activated 10, duration 2, current 11 | Active |
| Activated 10, duration 2, current 12 | Expired |
| Pending event crosses nominal end | Remains Pending |
| Reach MaxTime | Other Scheduled/Active slots expire |
| Cross appearance while reaching MaxTime | Do not activate it |
| Expired/completed events | Never replenish |

### 19.3 Hazard flow

| Scenario | Expected |
| --- | --- |
| Select active Hazard | Pending confirmation; no mutation |
| Modal content | Narrative plus exact Effect/Gain lines |
| Cancel input | No cancel path |
| Restart pending confirmation | Same modal, no mutation |
| Confirm | Resources/effect apply once, event completes |
| Duplicate Confirm request | No duplicate reward/effect |
| Zero-time resolution | Climb time unchanged |
| Restriction target available | Exactly one eligible entry restricted |
| Duplicate identical cards | Only chosen entry restricted |
| No restriction target | Resources only |
| Burn/Fear | Added to pending penalties |
| Shackled/Scar | Added immediately to run-long stacks |

### 19.4 Character flow

| Scenario | Expected |
| --- | --- |
| Select active Character | Dialogue pending; no reward/time |
| Dialogue display | Undimmed background only behind dialogue |
| Normal completion | Opens summary; no reward/time |
| Skip | Opens same summary; no reward/time |
| Restart during dialogue | Starts line one |
| Restart during summary | Reopens summary |
| Proceed | Reward plus one time apply atomically |
| Duplicate Proceed request | No duplicate reward/time |
| Proceed reaches MaxTime | Event resolves, then Final queues |
| Smith eligible | One hidden random entry upgrades |
| Smith no eligible entry | No upgrade, still costs one time |

### 19.5 Next-battle state

| Scenario | Expected |
| --- | --- |
| Two Burn Hazards | Burn values add |
| Two Fear Hazards | Fear values add |
| Multiple bonus types | All coexist |
| Enter non-Climb/tutorial battle | Do not consume Climb package unless it is the next Climb encounter |
| First queued encounter | Apply full package once |
| Second queued encounter | Do not grant package again |
| Final is next | Apply package to Final |
| Leave first queued encounter | Existing lifetime rules govern retained effects |

### 19.6 Stable deck entries

| Scenario | Expected |
| --- | --- |
| Duplicate same card key | Separate IDs and ECS entities |
| Save/reload duplicates | Identity remains stable |
| Restrict first duplicate | Second unchanged |
| Upgrade restricted entry | Same ID and restrictions, upgraded key |
| Replace restricted entry | New ID, no restrictions |
| Reward exchange | New ID at same position |
| Exhaust one duplicate | Other remains |
| Card-list selection | Exact entry targeted |
| Starter upgrade | Starter provenance retained |
| Traded upgrade | Traded provenance retained |

### 19.7 Presentation

| Scenario | Expected |
| --- | --- |
| Hazard card | Generic face, GAIN pips, +0 time |
| Character card | Correct portrait/reward, +1 time |
| Shared tuning | Portrait/slot field changes both encounter and Character |
| Hazard hover | Resource projection only |
| Character hover | Time/expiration projection only |
| No active events | Event column hidden |
| Modal strings | No unsupported SpriteFont glyph exceptions |
| Dialogue | No dim overlay or foreground leak |

---

## 20. Likely File Inventory

This list helps agents locate impact; it is not permission to edit files outside an assigned workstream without coordination.

### Core data and save

- `ECS/Data/Loadouts/LoadoutDefinition.cs`
- `ECS/Data/Save/SaveFile.cs`
- `ECS/Data/Save/SaveCache.cs`
- `ECS/Data/Save/SaveRepository.cs`
- `ECS/Components/RunDeckComponents.cs`
- `ECS/Components/ClimbComponents.cs`
- `ECS/Components/Scenes.cs`

### Services and systems

- `ECS/Services/RunDeckService.cs`
- `ECS/Services/RunScopedStateService.cs`
- `ECS/Services/StartingDeckGeneratorService.cs`
- `ECS/Services/QuestCardRewardService.cs`
- `ECS/Services/ClimbRuleService.cs`
- `ECS/Services/ClimbShopService.cs`
- `ECS/Services/ClimbEncounterService.cs`
- `ECS/Services/CardUpgradeService.cs`
- `ECS/Services/ClimbEventService.cs` (retire canonical mutating behavior)
- New `ClimbEventSystem`
- `ECS/Scenes/UIElementEventDelegateSystem.cs`
- `ECS/Scenes/BattleScene/BattleSceneSystem.cs`
- `ECS/Scenes/BattleScene/DialogDisplaySystem.cs`
- `ECS/Scenes/NarrativeEventModalDisplaySystem.cs`

### Climb display

- `ECS/Scenes/ClimbScene/ClimbSceneSystem.cs`
- `ECS/Scenes/ClimbScene/ClimbBackgroundDisplaySystem.cs`
- `ECS/Scenes/ClimbScene/ClimbHeaderLayoutSystem.cs`
- `ECS/Scenes/ClimbScene/ClimbColumnLayoutSystem.cs`
- `ECS/Scenes/ClimbScene/ClimbColumnDisplaySystem.cs`
- `ECS/Scenes/ClimbScene/ClimbSceneDrawHelpers.cs`

### Events and content

- `ECS/Events/ClimbEvents.cs`
- `ECS/Events/SceneEvents.cs`
- `ECS/Events/CombatEvents.cs`
- `ECS/Data/Dialog/DialogCatalog.cs`
- New `ClimbEventCatalog`
- `Content/Content.mgcb`
- `Content/character/*.png`

### Registration and diagnostics

- `Game1.cs`
- `ECS/Diagnostics/Snapshots/DisplaySnapshotRegistry.cs`
- `ECS/Diagnostics/Snapshots/Fixtures/ClimbSnapshotFixture.cs`
- `ECS/Diagnostics/Snapshots/Fixtures/NarrativeEventModalSnapshotFixture.cs`
- `docs/display-snapshots.md`

### Existing tests likely requiring conversion

- `tests/Crusaders30XX.Tests/ClimbRuleServiceTests.cs`
- `tests/Crusaders30XX.Tests/ClimbEventServiceTests.cs` or replacement system tests.
- `tests/Crusaders30XX.Tests/ClimbShopServiceTests.cs`
- `tests/Crusaders30XX.Tests/ClimbEncounterServiceTests.cs`
- `tests/Crusaders30XX.Tests/QuestCardRewardServiceTests.cs`
- `tests/Crusaders30XX.Tests/StarterDeckSaveTests.cs`
- `tests/Crusaders30XX.Tests/DeckManagementSystemTests.cs`
- `tests/Crusaders30XX.Tests/ColorlessCardTests.cs`
- `tests/Crusaders30XX.Tests/TestFightSetupServiceTests.cs`
- New stable-entry, event-flow, and next-battle integration tests.

---

## 21. Integration Risks and Required Mitigations

### 21.1 Duplicate-key assumptions

Risk: Existing code frequently calls `ToDictionary` or `FirstOrDefault` by card key.

Mitigation:

- Search all `RunDeckCard`, `cardIds`, and card-key mutation call sites.
- Use entry ID for identity and card key only for content comparison.
- Add duplicate-key regression tests before event effects land.

### 21.2 Multi-file save mutation

Risk: Resource, passive, event, and deck mutations persisted separately can duplicate or lose part of a resolution after interruption.

Mitigation:

- Add locked repository transactions that validate pending state and persist once.
- Publish ECS synchronization only after committed persistence.
- Make duplicate Confirm/Proceed requests fail validation after the first commit.

### 21.3 EventManager subscription duplication

Risk: Recreating scene child systems that subscribe globally can process confirmations multiple times.

Mitigation:

- Register `ClimbEventSystem` globally once in `Game1`.
- Do not recreate it on each Climb load.
- Keep child display systems scene-owned.

### 21.4 Final encounter race

Risk: Character Proceed reaches MaxTime while summary/modal state is still active, causing transition before reward persistence.

Mitigation:

- Complete reward/time transaction first.
- Mark modal request handled and close it.
- Queue Final only after successful resolution.

### 21.5 Modal text clipping

Risk: Exact binding Hazard information could be clipped if placed on a long button.

Mitigation:

- Put mechanics in wrapped modal body lines.
- Keep sole button label `Confirm`.
- Snapshot longest plural/resource combinations.

### 21.6 Portrait crop quality

Risk: Tall transparent Character assets may crop poorly in a wide encounter region.

Mitigation:

- Use shared top-biased crop.
- Add one shared debug crop-bias field.
- Capture all four portraits during development even if only one fixture is committed.
- Add per-asset settings only if shared tuning demonstrably fails, and document why.

### 21.7 Global overlay leakage

Risk: Debug, tooltip, hotkey, reward, or narrative overlays remain visible during Character dialogue.

Mitigation:

- Centralize the background-only draw gate in `Game1` using shared dialogue state.
- Snapshot the exact global draw mode.

### 21.8 ASCII-only runtime fonts

Risk: Curly apostrophes or typographic punctuation crash SpriteFont drawing.

Mitigation:

- Store all new runtime strings with straight ASCII apostrophes and basic punctuation.
- Add a test that filters or validates all catalog/dialog strings.

---

## 22. Definition of Done

The feature is complete only when all statements below are true:

- A fresh run saves exactly three Hazard and two distinct Character event instances.
- Hazard effects may repeat.
- Every event has a deterministic hidden schedule and persisted roll data.
- Landing activation, full duration, end-exclusive expiration, and Final preemption behave exactly as specified.
- Hazard cards show only generic identity, GAIN resources, time zero, and duration.
- Character cards show the correct portrait, exact GAIN reward text, time one, and duration.
- Hazard selection is binding and resolves only on Confirm.
- Character reward/time resolves only on Proceed.
- Character dialogue is exactly two lines and restarts after interruption.
- Background-only Character dialogue shows an undimmed Climb background and no other scene/global foreground.
- Colorless, Frozen, and Brittle target one stable deck entry.
- Duplicate identical card entries remain independent through save/reload.
- Burn/Fear stack as next-battle penalties.
- Courage/Temperance/Vigor stack as next-battle bonuses.
- Shackled/Scar apply immediately as run-long passives.
- The next Climb encounter consumes bonuses/penalties exactly once.
- Smith upgrades one hidden eligible entry, or grants nothing when none exists.
- Upgrade preserves entry identity/restrictions; replacement creates a new identity.
- Legacy Location events still compile but are not used by the active Climb.
- `CONTEXT.md`, ADR 0009, ADR 0010, ADR 0003 status, ADR 0007 clarification, and snapshot docs are updated.
- All targeted and full tests pass.
- Required snapshots render without exceptions.
- `dotnet build` succeeds from the repository root.
