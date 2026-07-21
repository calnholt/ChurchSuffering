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

- **Range:** ~14 - 53 (`HP` on each enemy; authored base max HP)
- **Max HP at spawn:** `ApplyBaseHealth`, then Mortification, then climb-time bonus in `EntityFactory.CreateEnemyFromId`
- **Mortification:** multiplier is `0.70 + 0.05 * stacks`, applied first (0 through 6 stacks yields 0.70 through 1.00)
- **Climb time:** after Mortification, `+10%` of post-Penance HP per fixed eight climb time; Penitential Pilgrimage changes shop refreshes but not this cadence
- Example: Skeleton 26 at time 0 is 18 at Penance 0 and 26 with all six Mortification stacks; at climb time 8 the latter becomes 29

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
6. Set `ClimbPool` to `ClimbEncounterPool.Early` or `Late` for first/second half of climb time, or `Throughout` to appear in both halves (equal odds with that half's pool). Leave `None` to exclude from climb rolls. Melee skeletons use `Throughout`; encounter boards cap melee skeletons at 1 of 3 slots (Skeletal Archer is Early-only and does not count toward that cap).

---

## Design Checklist

1. **What's the identity?** (one sentence)
2. **What decision does the player make?** (beyond "block the big attack")
3. **Attack pattern?** (single/linker+ender/multi-jab/alternating)
4. **HP tier?** (fragile ~14-17 / standard ~22-30 / tough ~31-42 / boss-tier ~43+)
5. **Scaling?** (does the enemy get worse if fight drags?)
6. **Conditions?** (1-2 max, reinforce identity)
7. **Counterplay?** (smart play should mitigate)
