# Enemy Design Philosophy

This document contains the detailed rationale behind enemy design principles. For quick reference and workflows, see `CLAUDE.md`.

---

## Core Design Philosophy

These principles apply to ALL mechanic design, not just enemies.

### Restrict, Don't Remove

A partially disabled resource is more interesting than a deleted one. If you're designing a debuff that affects cards, ask: can the player still DO something with the afflicted card? A card that can't be played but CAN block creates decisions. A card that's simply removed creates nothing.

### The Cure Lives Inside Normal Play

The best debuffs are cleared by doing what you already want to do. Playing cards, blocking, being active - these should chip away at curses. Avoid mechanics that require dedicated "undo" actions or sacrifice costs. The player should feel like engaging with the game is progress, not like they're paying a tax.

### Persistence With Visible Progress

Long-lasting effects are memorable. Effects that span multiple battles feel significant. But they MUST have visible, inevitable progress toward resolution. The player should never feel permanently damaged - only temporarily cursed with a clear path out.

### Location and Timing Create Texture

Where something is matters. A debuff in your hand behaves differently than one buried in your draw pile. When you draw into a problem, when you're forced to discard it, when it shuffles away - these create stories. Design mechanics that care about position.

### The Afflicted Participates In Its Own Rescue

The thing that's cursed should be part of the cure. Using a frozen card to block clears the freeze. A shackled card blocking frees itself. This is both thematically elegant and creates interesting decisions about when to deploy the restricted resource.

### Layer, Don't Override

New mechanics should work alongside existing systems, not create exception bubbles. Can this interact with Frozen? With Shackled? With Pledge? If the answer is "no, it's special," reconsider. Complexity should emerge from interaction, not from isolation.

### Decisions Emerge From State

Never add explicit choice prompts ("Choose: A or B"). The interesting decisions should come from evaluating your hand, the board, and the rules - then discovering what you should do. The player finds the decision; they're not asked it.

### Ask These Questions

When designing a new mechanic:
1. What can the player still do with an afflicted resource?
2. How does normal play contribute to resolution?
3. What happens when this interacts with existing mechanics?
4. Where does this create a decision the player didn't have before?
5. Can the player see their progress toward escaping?

---

## Damage and Effect Relationships

When designing enemy attacks, always consider how the base damage interacts with any special effects:

- **On Hit effects** require enough base damage that the player faces a meaningful blocking decision. If the damage is too low, the attack will always be fully blocked and the effect will never trigger.

- **Conditional effects based on blocking behavior** (e.g., "if blocked by 2+ cards") must make sense given the base damage. Ask: would a player ever realistically meet this condition? A 1-damage attack will never be blocked by multiple cards.

- **Think through the player's decision tree** - what choices does the attack actually present? Effects that the player can trivially avoid aren't interesting.

- **Minimum damage**: All enemy attacks must deal at least 1 damage. Zero-damage attacks feel anticlimactic and don't engage the blocking system.

---

## Burn and Player Health

The player has **20 HP** and burn deals **X damage per turn** (where X is burn stacks). This creates important design constraints:

- **5 burn = tight clock**: At 5 burn, the player takes 5 damage per turn and will die in 4 turns without healing. This is significant pressure.
- **Burn stacks quickly**: Multiple small burn applications (1-2) add up fast. An enemy that applies 2 burn across two attacks has already put the player on a 4-damage-per-turn clock.
- **Burn as primary threat**: For burn-focused enemies, the burn itself can be the main danger. The attack damage can be lower since burn provides sustained pressure.
- **Avoid burn overload**: Be cautious with attacks that apply 4+ burn at once - this can feel unfair without clear counterplay.

---

## Passives Must Change Decisions

A passive is only worth adding if it changes **what the player does**, not just how much damage they take.

**Bad passive design**: "Ember - converts to burn after 2 turns." This is just delayed burn. The player's strategy is identical - block burn attacks, find healing. There's no decision point where having Ember changes your play.

**Bad passive design**: "Kindling - increases burn taken by 1." This is burn with bigger numbers later. The player still does the same things, the stakes are just higher.

**Good passive design**: The passive creates a new decision point or changes how you evaluate existing decisions:
- **Burn** works because it creates ongoing pressure affecting *future* turn decisions (heal vs. deal damage?)
- **Must be blocked** forces specific block allocation *now*
- **Vulnerable** changes how you prioritize which attacks to block
- **Petrify** works because it restricts a card but lets it contribute to its own cure through blocking

Ask: "If the player has this passive, do they do something *different*? Or do they just take more damage while playing the same way?"

**The best mechanics have multiple escape routes.** Petrify can be cleared by blocking OR by playing cards. This means different hands find different paths out. One player blocks aggressively to free their card. Another plays their whole hand to crack it. Both are valid. Mechanics with only one exit become solved puzzles.

---

## Avoid Cost Modification

Don't design enemy effects that modify card costs. The cost system is too complex to manipulate cleanly:
- Costs are paid by discarding other cards, not spending a resource pool
- Some cards require discarding specific colors
- Increasing costs creates cascading complexity (what if they can't pay? what about color requirements?)

This design space isn't worth the cognitive overhead.

---

## No Hand Adjacency Mechanics

Never design mechanics based on card position or adjacency in the player's hand. Effects like "spreads to adjacent cards" or "affects neighboring cards" don't work with the game's design:
- Hand order isn't a meaningful player decision

If you want cascading or spreading effects, use other vectors: card color, card type, or random targeting.

---

## Force Hand Re-evaluation

Effects that make the player look at their hand differently are valuable. The best designs create tradeoffs where the "right" play isn't obvious:

- **Conditional penalties**: "If you play 3+ cards this turn, gain 2 burn" makes the player reconsider a big combo turn
- **Block-based effects**: Effects that trigger based on how much block is used force re-evaluation of which cards to spend blocking
- **Color pressure**: Effects that punish or reward playing specific colors make hand composition matter more

The goal is tension: the player has a plan, then sees the enemy's attack and has to ask "is my plan still good?" Sometimes yes, sometimes no - that's interesting.

---

## Favor Simplicity

When conveying an attack concept, prefer simpler mechanics:

- `OnAttackReveal` for guaranteed effects (e.g., deal damage when the attack is revealed) is often cleaner than complex damage modification systems
- Avoid mechanics that bypass core systems (like block) entirely - they remove player agency without adding interesting decisions
- **No explicit choices on enemy attacks** (e.g., "Choose: take 5 damage OR gain 2 burn"). This adds UI overhead, breaks combat flow, and the interesting decisions should emerge from blocking/card play, not from modal prompts

---

## Health & Damage Reference

The player has **20 HP**. All enemy stats should be understood relative to this.

### Health Ranges

| Tier | HP Range | Examples | Design Role |
|------|----------|----------|-------------|
| Fragile | 40-60 | Gleeber (50), Skeletal Archer (48) | Quick fights, tutorial enemies |
| Standard | 70-90 | Spider (80), Ninja (80), Demon (90) | Core encounter length |
| Tough | 95-120 | Sorcerer (120), Glacial Guardian (110) | Extended battles, scaling threats |
| Boss-tier | 120+ | Shadow (150) | Marathon fights, attrition-based |

Enemy max HP scales with **deck size**, not difficulty: set `HP` (max HP at a 20-card reference deck) on the enemy; at spawn, `MaxHealth = Round(HP * deckWeight / 20)`. Difficulty affects spawn HP via `EnemyHealthModifier` and attacks/passives where coded.

### Attack Damage Ranges

| Tier | Damage | Role | Notes |
|------|--------|------|-------|
| Chip | 1-4 | Setup, condition delivery | Often paired with other attacks |
| Standard | 5-7 | Core pressure | Forces blocking decisions |
| Heavy | 8-11 | Ender attacks, punishes | Should feel threatening |
| Devastating | 12+ | Boss moves, conditional | Needs counterplay or warning |

**Rule of thumb**: An unblocked heavy attack (8-11) takes ~half the player's health. Two unblocked heavies should kill.

---

## Attack Pattern Archetypes

### Single Attack Enemies

One attack per turn, but each attack matters.

**Examples**: Demon, Sorcerer, Berserker, Ice Demon

**When to use**: When the enemy's identity comes from *what* the attack does, not attack volume. Good for:
- Scaling threats (Sorcerer's Channel makes each attack worse)
- High-impact moments (Berserker's 11-damage hit)
- Complex single-attack decisions (Ice Demon's frostbite conditionals)

### Linker + Ender Pattern

2-3 attacks per turn: small "linker" attacks set up a larger "ender."

**Examples**: Spider, Ogre, Succubus

**Structure**:
- Linkers deal 1-4 damage, apply conditions or restrict blocking
- Enders deal 6-11 damage, punish if linker conditions weren't handled

**When to use**: When you want the player to manage multiple threats simultaneously. The linker creates context for evaluating the ender.

### Multi-Jab Pattern

3-7 small attacks per turn, each with its own conditional effect.

**Examples**: Ninja (4-7 attacks), Skeleton (3+ attacks)

**When to use**: When the enemy's threat comes from *accumulated* pressure rather than any single hit. Works well with:
- Per-card effects (Corrode on each blocking card)
- Synergy bonuses (Ninja's Nightveil Guillotine if Slice + Dice both hit)
- Resource exhaustion (too many attacks to block everything)

### Alternating Pattern

Predictable rotation between attack types across turns.

**Examples**: Sand Golem (A/B/A/B), Shadow (attack/rest/attack)

**When to use**: When you want the player to plan ahead. The predictability is a feature—it rewards learning the pattern and preparing the right cards.

---

## Enemy Identity Motifs

### Burn Enemies

**Core fantasy**: Sustained pressure, ticking clock

**Examples**: Demon, Fire Skeleton, Cinderbolt Demon

**Design notes**:
- Attack damage can be lower since burn provides ongoing threat
- 2-3 burn per attack is meaningful; 4+ feels punishing without counterplay
- Works best when player must choose between blocking damage *now* vs. managing burn *later*

### Blocking-Punish Enemies

**Core fantasy**: Your blocking decisions have consequences

**Examples**: Spider (ambush), Mummy (exact block requirements), Ninja (per-card penalties)

**Design notes**:
- High ambush chance (75%) means on-hit effects trigger even when blocked
- "Must block with exactly X cards" creates puzzle-like decisions
- Effects that trigger per blocking card make over-blocking costly

### Armor/Resilience Enemies

**Core fantasy**: Can't be burst down, requires sustained offense

**Examples**: Skeleton (builds armor), Skeletal Archer (starts with armor), Cactus (thorns)

**Design notes**:
- 3-4 starting armor is noticeable but not oppressive
- Building armor each turn creates urgency to kill quickly
- Thorns/reflection punishes reckless attacking

### Control Enemies

**Core fantasy**: Your cards don't work the way you want

**Examples**: Glacial Guardian (freezes hand), Sorcerer (mills deck), Shadow (silences), Medusa (petrifies)

**Design notes**:
- Restrict, don't remove - afflicted cards should still be able to do SOMETHING
- The player should be able to work toward freedom through normal play
- Freeze: can't play, CAN block, cleared by blocking
- Petrify: can't play/pledge, CAN block, cleared by activity while in hand
- Intimidate/silence: temporary restrictions with automatic expiration
- Mill is the exception - it removes cards entirely. Use sparingly and with clear counterplay.

### Scaling Enemies

**Core fantasy**: The longer you wait, the worse it gets

**Examples**: Sorcerer (Channel stacks), Dust Wuurm (gains Power)

**Design notes**:
- Creates time pressure without requiring complex mechanics
- Player learns to prioritize killing these quickly
- Scaling should be visible (player can see stacks increasing)

### Passive-Driven Enemies

**Core fantasy**: The enemy's rules change how you play, not just their attacks

**Examples**: Succubus (heals when you lose courage), Cactus (thorns), Shadow (Anathema punishes pledges)

**Design notes**:
- Attacks can be simpler because the passive carries complexity
- The passive should create a clear "don't do X" or "must do Y" rule
- Best when the rule interacts with normal player behavior

---

## Common Conditions Reference

### Applied to Player

| Condition | Frequency | Design Role |
|-----------|-----------|-------------|
| Burn | Very common | Sustained damage pressure |
| Intimidate | Common | Restricts card plays |
| Wounded | Moderate | Reduces card effectiveness |
| Frozen | Moderate | Temporarily disables cards |
| Slow/Fear | Rare | Tempo disruption |
| Corrode | Rare | Degrades blocking cards |

### Applied to Enemy (Self-Buffs)

| Condition | Examples | Design Role |
|-----------|----------|-------------|
| Armor | Skeleton, Skeletal Archer | Damage reduction |
| Channel | Sorcerer | Attack scaling |
| Power | Dust Wuurm | Damage scaling |
| Thorns | Cactus | Reflection damage |
| Aggression | Various | Offensive boost |
