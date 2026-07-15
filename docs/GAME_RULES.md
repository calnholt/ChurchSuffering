# Game Rules

## Overview

Crusaders30XX is a deckbuilder card game where players battle enemies using a hand of cards for both blocking attacks and taking offensive actions.

## Turn Structure

### 1. Start of Battle Phase

- Some enemy passives may trigger at battle start
- Player draws cards up to their **max hand size (4)**

### 2. Block Phase

- **Enemy attacks first** with one or more attacks
- Player can see the **number of attacks queued**, but not what the attacks are
- Player may assign cards from hand to block incoming attacks
- Cards have block values, typically **2-4**
- Some enemy attacks have additional effects that trigger when they deal damage or have special blocking conditions
- Player decides how to block (or whether to block at all)
- All attacks resolve
- If you have Poison, one block-capable card in hand becomes poisoned. Blocking with it costs 1 HP.

### 3. Action Phase

- Player begins the turn with **1 Action Point (AP)**
- Player may play:
  - Cards that cost an action point while AP remains
  - **Any number of Free Action cards** in any order
  - Equipment abilities marked **Free Action**, without spending AP
- Equipment activation marks the item used for the rest of the battle and still requires any resources listed by the ability
- Card effects may grant additional AP
- Some cards have **discard costs** requiring the player to discard cards of a specific or any color
- The player may pledge one eligible card from hand when **Pledge available**:
  - Pledging is enabled
  - The player has not pledged during this Action phase
  - No card is already pledged
  - The hand contains an eligible card
- Sealed cards, weapons, block cards, relics, tokens, and cards already pledged cannot be pledged; sealed cards lose one seal when used to block and are freed at zero seals
- **Hex** temporarily covers an eligible hand card with a free 5-damage, 3-block attack. Hovering Hex previews the covered card. Playing Hex turns that card into a persistent Curse; any unplayed Hex is removed at the end of the player turn.
- A pledged card does not count towards max hand size
- A pledged card cannot be played during the Action phase in which it was pledged
- A pledged card must be played as an action; it cannot block or pay another card's cost
- Player can end their turn at any time

### 4. Draw Phase

- Player draws up to their max hand size

### 5. Enemy Turn

- Enemy takes their turn
- Loop returns to Block Phase

## Win/Lose Conditions

- **Victory:** Enemy HP reaches 0
- **Defeat:** Player HP reaches 0
- **No reshuffle:** Your deck does not reshuffle when it empties

## Climb Unlock Progression

- New profiles begin with Sword on Easy; Start Climb launches this configuration directly until Dagger is unlocked.
- The first completed climb unlocks Dagger on Easy and Sword on Normal.
- Completing Sword on Normal unlocks Sword on Hard.
- Completing Dagger on Easy unlocks Hammer on Easy and Dagger on Normal.
- Completing Dagger on Normal unlocks Dagger on Hard.
- Completing Hammer on Easy unlocks Hammer on Normal; completing Hammer on Normal unlocks Hammer on Hard.
- Once Dagger is unlocked, Start Climb opens the settings modal and only shows unlocked choices.

## Quest Structure

- A **quest** consists of one or more battles
- **HP fully recovers** after each battle within a quest
- Each equipment item can be used once per battle, shared between blocking and activating its ability
- Equipment refreshes when the battle ends
- This design makes each encounter a true fight for survival rather than an exercise in HP preservation

## Equipment

- Equipment remains visible and inspectable after it has been used
- During the Block phase, unused equipment with block greater than zero may be assigned as block
- During the Action phase, equipment marked **Free Action** may activate without spending AP
- Used equipment cannot block or activate

## Card Colors

There are three card colors, each with distinct blocking mechanics:

| Color | Block Bonus | Resource Gained |
|-------|-------------|-----------------|
| **Red** | Standard | Courage |
| **White** | Standard | Temperance |
| **Black** | +1 block | None |

### Red Cards & Courage

- Blocking with red cards grants **Courage**
- Courage is a resource that cards can spend or check thresholds against

### White Cards & Temperance

- Blocking with white cards grants **Temperance**
- When you reach your **Temperance threshold**, your equipped temperance ability auto-triggers

### Black Cards

- Block for **+1 compared to red/white counterparts**
- Do not grant any resource when blocking

### Dual Color Modification

- Dual Color is a permanent, run-scoped card modification that adds a second Red, White, or Black color
- A Dual Color card qualifies as either of its colors for costs, blocking restrictions, color counts, achievements, medals, enemy effects, and other color-triggered effects
- One Dual Color card still pays only one discard-cost slot; it cannot satisfy two costs by itself
- Blocking counts the physical card and its block value once, while applying each color's blocking benefit once
- A card with Black as either color receives the intrinsic +1 block bonus
- Colorless temporarily suppresses both colors and their benefits without removing the Dual Color modification

### Colorless Restriction

- Colorless is a run-long restriction, not a fourth card color
- A card keeps its printed Red, White, or Black identity for deck construction, rewards, and save keys
- During combat, a Colorless card qualifies as no color
- It can pay an **Any** color cost, but cannot pay a specific Red, White, or Black cost
- It does not generate Courage or Temperance from blocking
- A printed-Black Colorless card loses the intrinsic +1 block bonus
- It does not count for positive color checks, color counts, selections, achievements, medals, enemy effects, or battle UI color totals
- It fails Only Red/White/Black restrictions and passes Not Red/White/Black restrictions
- Removing Colorless immediately restores normal printed-color behavior

## Deck Building Rules

- Each card exists in all three colors (red, white, black variants)
- Maximum of **2 copies** of the same card name in your deck

## Core Strategy

The central puzzle each turn is **maximizing value from your hand**:

- You don't want to carry cards to the next turn (unless pledged) because you draw to max hand size
- Balance blocking vs. saving cards for actions
- Consider resource generation (Courage/Temperance) when choosing which cards to block with
- Manage your pledge slot for cards you want to guarantee playing
