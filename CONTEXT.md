# Church Suffering — domain glossary

Terms only. No implementation details.

## Performance diagnostics

### Render cadence

Wall-clock time between the starts of consecutive rendered frames. This includes CPU work, pacing, and driver stalls; it is distinct from fixed-step simulation time.

### CPU busy time

Wall-clock time spent from the first update iteration after a rendered frame through completion of the next frame's draw commands.

### GPU command time

Delayed device time for a bounded set of submitted rendering commands. It excludes swap and display synchronization time.

### Outside/pacing/driver time

The part of render cadence not covered by the preceding CPU busy interval. It may include framework pacing, operating-system scheduling, and graphics-driver waits outside instrumented work.

## Run

A single playthrough from WayStation Depart through the **Climb** until victory, failure, or abandon. A save may hold one active run or no active run.

## Fresh profile

A save containing only **meta** progress. It has no active run, Climb, deck, gold, loadout, inventory, or queued encounter. The **Guided Tutorial** may run from a fresh profile, but only WayStation Depart creates the first run.

## Guided Tutorial

A one-time, two-battle instructional sequence launched from the title screen before any run exists. It is meta progression and exists outside run, Climb encounter, and queued-encounter lifecycles. Interruption restarts the sequence from its opening dialogue and first battle. Completion transitions directly to WayStation.

## Tutorial battle

One of the two battles inside the **Guided Tutorial**. Tutorial battles use temporary player and deck entities, grant no rewards or progression, emit no run telemetry, and do not create or mutate a run.

## Stock hand

The exact authored set of card identities and printed colors created fresh for a Guided Tutorial turn. Stock cards use normal draw presentation. A pledged Fervor is the only card explicitly retained between stock hands.

## Tutorial enemy

An enemy classification reserved for the **Guided Tutorial**. Tutorial enemies can be spawned explicitly by the tutorial but are excluded from procedural maps and ordinary encounter pools.

## Active run

A run whose Climb state, deck, gold, inventory, and run-scoped state exist in the save. WayStation Depart creates and activates a run before applying the selected setup.

## Inactive run

The persisted state after run victory, run failure, or run abandon. Meta remains, but map data, gold, loadout, inventory, and other run-scoped data are cleared. Loading an inactive save does not generate a replacement run; only WayStation Depart creates the next run.

## Run-long applied passive

A debuff or status on the player that lasts for the whole **run**: it survives leaving battle, visiting the Climb, shopping, and starting other **Climb encounters**, until **run failure**, **run abandon**, or **new run start**. Examples: Frostbite, Scar, Bleed, Shackled (see implementation list `GetRunLongPassives`). Gaining Scar immediately lowers max HP by that amount. At battle start, one Scar stack is removed, but max HP is not restored in that battle; the next battle recalculates max HP from the remaining Scar stacks.

_Avoid_: Quest passive (when meaning run-long; use **run-long applied passive**)

## Encounter-scoped applied passive

A debuff or status on the player tied to the current **Climb encounter** attempt: it persists across **queued encounters** within that encounter, but is removed when the player leaves the battle scene to the Climb, shop, or other non-battle scene. It is not written to the save file.

Examples: Webbing, Fear, Enflamed (see implementation list `GetQuestPassives`).

_Avoid_: Quest passive (when meaning run-long; use **run-long applied passive**)

## Queued encounter

One enemy fight in the sequential battle queue for a **Climb encounter** (`QueuedEvents` advances one step per **queued encounter**). A Climb encounter may have several queued encounters before the player returns to the Climb.

**Temperance** built during one queued encounter carries to the next encounter in the same Climb encounter. **Temperance** resets to zero when the player leaves the battle scene (e.g. return to the Climb). Courage, HP, and action points reset at the start of each queued encounter.

When card or medal text says **win a battle**, that means winning a **queued encounter** (the enemy's **defeat presentation** has finished and the battle advances), not completing the whole **Climb encounter**. Reaching 0 HP starts **defeat presentation**; the encounter is not won until that presentation ends.

## Action Point

A battle resource spent to play cards during the **Action phase**. Playing a card that is not a **Free Action** costs one Action Point. The player begins each turn with one Action Point, and card effects may grant more.

## Free Action

A card play or equipment ability that can be activated during the **Action phase** without spending an Action Point. Activating equipment marked as a Free Action uses that item for the current battle.

## Equipment availability

Each equipment item has three persistent uses across a quest, shared by block and activation. Consuming a use marks the item used for the current battle (once per battle while charges remain). The per-battle lock clears at the next battle start or on enemy kill without restoring remaining uses. Remaining uses are stored on the run loadout and restored when equipment is created from the save. Exhausted equipment (zero uses left) cannot block or activate for the rest of the quest.

## Pledge available

The player may pledge during the **Action phase** when pledging is enabled, they have not pledged during that Action phase, no card is already pledged, and their hand contains an eligible card. Sealed cards, weapons, block cards, relics, tokens, and cards already pledged are not eligible.

## Defeat presentation

The on-screen sequence when a combatant is reduced to 0 HP before the battle advances. For enemies in v1, this is the pixel-burst animation: the enemy **portrait** is hidden and replaced by the burst; other enemy UI (HP bar, intents, passives) may remain visible until the presentation ends. When it completes, the game runs the normal post-kill flow (achievements, next queued encounter, or encounter completion). During defeat presentation the player cannot take battle actions (cards, end turn, equipment). Player defeat presentation is separate (game-over overlay).

_Avoid_: Death animation (ambiguous with attack animations or game-over)

_Avoid_: Battle node (ambiguous with **Climb encounter**)

## Run failure

Losing a battle. The current run ends after the game-over sequence finishes: run entities and run-scoped save data are cleared, an inactive run is persisted, and the player returns to WayStation. Meta earned during that fight is kept.

## Run abandon

Voluntarily ending the current run from the in-battle quit overlay (prompt: "Abandon run?"). Uses the same game-over sequence as run failure, persists an inactive run, and returns to WayStation.

## Meta

Progress that survives run failure and new-run creation: achievements, seen tutorials, and Guided Tutorial completion.

## Save

Persistent record of run lifecycle state plus meta. A fresh profile and an inactive profile contain meta only. An active run includes current Climb progress and opportunities, gold, loadouts, inventory, **run-long applied passives**, and **run-long card restrictions**. Starting a new run replaces run state only; meta is kept.

Saves do not migrate between incompatible versions; an incompatible save becomes a fresh meta-only profile.

## Loadout

The player's configured battle kit for the run: ordered deck card entries, weapon, Temperance ability, equipment, and medals. Shop card purchases append a deck card entry. Resolving a deck reward exchange replaces the selected entry at the same ordered position, ending the outgoing identity and creating a new identity for the incoming card. There is no separate owned-cards list.

## Deck

The player's deck for the current run: the ordered set of **deck card entries** on the loadout, plus how those cards are distributed in battle. Duplicate card keys are valid because each entry has its own run identity.

## Deck card entry

One stable, run-scoped card instance in the ordered loadout. Its card key identifies its content; its entry identity distinguishes it from every other instance, including entries with the same card key. Upgrading a card preserves its entry identity. Replacing or exchanging it ends that identity and creates a new entry at the same ordered position.

## Printed color

The immutable Red, White, or Black identity stored on a card entry. Printed color determines deck construction, rewards, save keys, and the card's normal combat color. Effects may change whether the card qualifies as that color in combat without changing its printed color.

## Colorless

A **run-long card restriction** that makes a card qualify as no color during combat while preserving its **printed color**. A Colorless card can pay an `Any` color cost but not a Red, White, or Black cost. It does not count for color predicates, color resources, or the printed-Black block bonus.

## Exhaust

Removing a card from the deck for the rest of the run. Exhausted cards do not return in later battles. Exhaustion removes that card entry from the save loadout and destroys the run deck entity.

## Inter-battle deck reset

At the start of each battle, all surviving cards (draw pile, hand, and discard) combine into one draw pile, which is then shuffled. Exhausted cards are not included.

## Run-long card restriction

A combat effect owned by one **deck card entry** that lasts for the whole **run** until that entry ends, **run failure**, **run abandon**, or **new run start**. Restrictions survive leaving battle and Climb visits. Upgrading preserves them with the entry; replacement, exchange, or exhaustion removes them with the outgoing entry.

The run-long **Shackled** player passive is not a card restriction. Battle-local **Shackle** markers link cards so they are assigned and unassigned as blockers together; those markers are not persisted.

## Encounter-scoped card modification

A permanent change to a deck card's combat numbers (e.g. +1 damage from an encounter-scoped effect) for the current **Climb encounter** attempt. It persists across **queued encounters** in that encounter and is removed on **encounter completion** when the player earns **encounter reward**. It does not carry to other Climb encounters or the Climb.

_Avoid_: Rest of the quest (ambiguous with whole **run**; prefer **Climb encounter** or **encounter-scoped card modification**)

## Starter pool

The fixed set of card identities used only when rolling a new run's starting deck. Cards earned or bought during a run do not add to the starter pool for future runs.

## Starting deck

The 20-card deck assigned at new-run creation by random selection from the starter pool, subject to color balance and copy limits. Persisted on the loadout until the player edits it.

## Copy limit

A deck may include at most one copy of a given card identity and color pairing, and at most two copies of the same card identity across all colors. Starting deck construction follows this limit.

Shop purchases and deck reward exchanges are exceptions: either may add a card entry even when it would exceed the normal copy limit. Each shop listing can still be bought only once per run.

## Climb

The capped time-and-slot progression layer for an active run between WayStation Depart and run victory, run failure, or run abandon. It presents generated Shop, Encounter, and Event columns.

_Avoid_: Run map, world map, location hub (when meaning the active mid-run progression layer)

## Climb time

A capped progression meter advanced by Climb choices. It represents the run's approach to the Final encounter, not real elapsed time. Reaching maximum Climb time triggers the **Final encounter**.

## Climb resource

One of the Red, White, or Black spendable resources used by the Climb economy.

## Climb resource pip

One unit of one **Climb resource** color.

## Climb encounter

One fightable opportunity in the active Climb's Encounter column. Selecting it advances Climb time by its shown cost; completing its battle may grant an **encounter reward**.

## New run start

WayStation Depart after first launch or after an inactive run. Depart creates and activates the run, applies the selected setup, then routes the player to the Climb. Combat starts only after the player selects a Climb encounter.

## Combat node

A legacy implementation term from the dormant `RunMap` and `Location` model. It does not define the active Climb; use **Climb encounter** for canonical behavior.

## Ordinary Climb encounter

A Climb encounter that is not the **Final encounter**. Completing it can grant an **encounter reward**.

## Final encounter

The run-ending Climb encounter triggered when Climb time reaches its maximum. It grants no encounter reward.

## Encounter completion

Winning all queued encounters belonging to one Climb encounter.

## Boss phase

One segment of a multi-phase enemy battle. `EnemyBase.Phases` is the total phase count and `CurrentPhase` is the active phase. Lethal damage before the final phase ends the current phase without publishing enemy-kill, battle-win, encounter-completion, or reward signals.

## Phase reset

The transition between boss phases after checkpoint dialogue. It advances `CurrentPhase`, rebuilds the enemy arsenal, fully heals player and enemy, merges and shuffles hand/draw/discard while leaving exhausted cards removed, clears pledges and transient card/attack interaction state, and removes only turn-duration passives from player and enemy.

Courage, Temperance, Guard, battle/encounter/run passives, card restrictions, card modifications, and cumulative turn number persist. A phase reset does not publish StartBattle, rerun start-of-battle abilities, or show the Start of Battle banner. It enters the next EnemyStart flow directly.

## Shop (Climb)

A generated opportunity in the active Climb's Shop column where Climb resources can be spent on its offered benefit.

## Climb event

A generated, time-scheduled, non-combat opportunity in the active Climb's Event column. It is either a **Hazard event** or a **Character event**.

## Hazard event

A Climb event that shows a Climb resource reward while hiding a binding harmful effect until selection. Confirming applies the harm and reward together without advancing Climb time.

## Character event

A Climb event that shows a beneficial reward, presents a character exchange after binding selection, and then applies the reward while advancing Climb time by one.

## Next-battle bonus

Saved Courage, Temperance, or Vigor granted once when the next Climb encounter begins. Bonuses from multiple sources add together.

## Next-battle penalty

Saved Burn or Fear granted once when the next Climb encounter begins. Penalties from multiple sources add together.

## Narrative event

A legacy UI and implementation term used by dormant `RunMap` and `Location` event code. It is not a canonical Climb event subtype; use **Hazard event** or **Character event** for the active Climb.

## Encounter reward

On first completion of an ordinary Climb encounter: reward gold and a deck reward offer. The Final encounter grants no encounter reward. No rewards on repeat attempts (replays disabled). The reward modal shows only this encounter reward gold; other gold grants (e.g. from medals) may apply at the same moment without changing the modal amount.

## Deck reward offer

A persisted unresolved offer created by an encounter reward. Resolving an exchange chooses one lane and replaces its targeted **deck card entry** at the same ordered position. The outgoing entry ends and the incoming card receives a new run identity. Deck reward exchanges ignore copy limits.

## Run victory

Defeating the Fallen Shepherd's final phase. Victory dialogue finishes first, then the normal enemy defeat presentation and enemy-kill signal run. No encounter completion or reward is granted. The run is ended, an inactive run is persisted, and the game transitions to WayStation.

## Might (applied passive)

A turn-long buff on the player: each of your attacks this turn deals extra damage equal to your Might stacks. Might does not carry to the next turn. Might stacks with **Power** on the same attack.

## Aggression (applied passive)

A turn-long buff on the player: your next **non-weapon** attack this turn deals extra damage equal to your Aggression stacks, then Aggression is removed. Weapon attacks do not consume Aggression. Multiple Aggression grants in the same turn add their stack counts together.

## Sharpen (applied passive)

A turn-long buff on the player: your next **weapon** attack this turn deals extra damage equal to your Sharpen stacks, then Sharpen is removed. Non-weapon attacks do not consume Sharpen. Multiple Sharpen grants in the same turn add their stack counts together.

Aggression and Sharpen are complementary (non-weapon vs weapon) and do not apply to the same attack.

## Bottom of deck

The end of the draw pile list (drawn last). The top of the draw pile is the card drawn next (`RemoveAt(0)` in battle).

## Medal bonus gold

Gold granted by a medal on **encounter completion**, separate from **encounter reward** gold shown in the completion modal. The player's total gold increases; the modal still displays the encounter reward amount only.

## Diagnostics

**Climb generator log**:
An append-only text log written in debug builds when a new Climb is created. Each line summarizes spread for one generation (seed, coverage stats). The log is cleared when the generator version integer is bumped in code.
_Avoid_: Spread log, map debug file

**Profiled game frame**:
One full game loop iteration: Update phase then Draw phase. Profiler stats for draw and update attach to the same frame id.
_Avoid_: Draw frame, profiler frame

**Performance report**:
A plain-text session summary written when the developer quits with Shift+Escape. Lists timing stats per instrumented scope plus frame-level totals.
_Avoid_: Profiler dump, perf log

**Unaccounted time**:
Wall-clock time in a profiled game frame not explained by leaf instrumented scopes (shader passes, present, uninstrumented code). Parent/inclusive scopes are listed separately and do not count toward this bucket.
_Avoid_: Mystery time, overhead
