# Enemy Design Guidelines

For detailed design philosophy and rationale, see `DESIGN_PHILOSOPHY.md`.

## Core Principles

- **Restrict, don't remove** - Afflicted cards should still do something
- **Cure through normal play** - No dedicated "undo" actions required
- **The afflicted participates in rescue** - Frozen card blocking clears freeze
- **Layer, don't override** - New mechanics interact with existing ones
- **Decisions emerge from state** - No explicit "Choose A or B" prompts
- **Passives must change what the player DOES** - Not just damage numbers

## Quick Reference

### HP

- **Range:** ~14 - 53 (`HP` on each enemy; max HP at a 20-card reference deck)
- **Max HP at spawn:** `Round(HP * deckWeight / 20)` via `EnemyBase.ApplyHealthFromDeckWeight` in `EntityFactory.CreateEnemyFromId`
- **deckWeight:** card count plus climb-time bonus from `RunDeckService.CalculateEnemyHealthDeckWeight`
- **Difficulty** does not affect max HP directly; `WayStationRunSetupSingleton.EnemyHealthModifier` scales spawn HP (e.g. Skeleton 26 at 20 cards -> 21 Easy / 23 Normal / 26 Hard)

### Damage Ranges

| Tier | Damage | Role |
|------|--------|------|
| Chip | 1-4 | Setup, condition delivery |
| Standard | 5-7 | Core pressure |
| Heavy | 8-11 | Enders, punishes |


### Pattern Examples

| Pattern | Use When | Examples |
|---------|----------|----------|
| Single attack | Identity from WHAT the attack does | `Demon.cs`, `Sorcerer.cs`, `Berserker.cs` |
| Linker + Ender | Multiple threats to manage | `Spider.cs`, `Ogre.cs`, `Succubus.cs` |
| Multi-jab | Accumulated pressure | `Ninja.cs`, `Skeleton.cs` |
| Alternating | Predictable planning | `SandGolem.cs`, `Shadow.cs` |

---

## Creating a New Enemy

1. Create `ECS/Objects/Enemies/YourEnemy.cs`
2. Inherit from `EnemyBase`, define attacks inheriting from `EnemyAttackBase`
3. Register enemy in `EnemyFactory.cs`
4. Register attacks in `EnemyAttackFactory.cs`
5. Use private fields for numbers, eg - `private int Armor = 1;`

---

## Design Checklist

1. **What's the identity?** (one sentence)
2. **What decision does the player make?** (beyond "block the big attack")
3. **Attack pattern?** (single/linker+ender/multi-jab/alternating)
4. **HP tier?** (fragile ~14-17 / standard ~22-30 / tough ~31-42 / boss-tier ~43+)
5. **Scaling?** (does the enemy get worse if fight drags?)
6. **Conditions?** (1-2 max, reinforce identity)
7. **Counterplay?** (smart play should mitigate)
