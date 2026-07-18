---
name: create-card
description: Create new Crusaders30XX cards (CardBase classes in ECS/Objects/Cards). Use when the user asks to create, add, or implement a new card, describes card text/stats/upgrades, or references card factory registration.
---

# Create Card

Add a new card to Crusaders30XX: one `CardBase` subclass + factory registration + `dotnet build`.

**Read first:** [ECS/Objects/Cards/CLAUDE.md](../../../ECS/Objects/Cards/CLAUDE.md)

## Workflow

```
- [ ] 1. Parse spec (name, stats, text, cost, upgrade, conditionals)
- [ ] 2. Ask only if blocked (name, spend vs gate, ambiguous condition)
- [ ] 3. Find 1-2 similar cards in ECS/Objects/Cards/ and match their pattern
- [ ] 4. Create ECS/Objects/Cards/{PascalName}.cs
- [ ] 5. Register snake_case id in CardFactory.Create() and GetAllCards() (alphabetical)
- [ ] 6. dotnet build — fix compile errors before done
```

Do **not** edit deck JSON, shop pools, or reward tables unless the user asks.

## Naming

| Item | Convention | Example |
|------|------------|---------|
| Class / file | PascalCase | `EmberHarvest.cs` |
| `CardId` / factory key | snake_case | `ember_harvest` |
| Display `Name` | Title Case | `Ember Harvest` |

## Spec defaults (when omitted)

| Field | Default |
|-------|---------|
| `Type` | `CardType.Attack` |
| `Target` | `"Enemy"` for attacks, `"Player"` for prayers |
| `IsFreeAction` | `false` |
| `Cost` | `[]` |
| `VisualEffectRecipe` | `PlayerAttackEffect()` for attacks, `HolySupportEffect()` for prayers |
| `Rarity` | omit unless user specifies |
| `EligibleWeapons` | `[EligibleWeapon.All]` (set e.g. `[EligibleWeapon.Hammer]` to gate shop/encounter rewards) |

## Archetype picker

| User intent | Pattern cards | Key hooks |
|-------------|---------------|-----------|
| Vanilla attack | `Smite`, `Fervor` | `OnPlay` -> `ModifyHpRequestEvent` + `GetDerivedDamage` |
| Attack + block | `Impale`, `StokedAssault` | set `Damage`/`Block`; damage in `OnPlay` only |
| Free action attack | `Impale`, `Stab` | `IsFreeAction = true` |
| Courage **spend** on play | `Stab`, `Impale` | spend in `OnPlay`; `CanPlay`/`OnCantPlay`; text: "As an additional cost, lose {N} courage." |
| Courage **gate** only | rare — confirm with user | `CanPlay` only, no spend in `OnPlay` |
| Vigor **gate** only | `StokedAssault` | `VigorService.GetPlayerVigorStacks`; no vigor delta in `OnPlay` |
| Payment conditional | `EmberHarvest`, `Reap`, `BatteringBlow` | read `LastPaymentCache.PaymentCards` in `OnPlay` or `GetConditionalDamage` |
| Scorched payment check | `EmberHarvest` | `paymentCard.GetComponent<Scorched>() != null` |
| Red payment conditional | `Reap` | `CardColorQualificationService.QualifiesAs(paymentCard, CardColor.Red)` |
| No payment bonus | `UnburdenedStrike`, `BatteringBlow` | `paymentCards == null \|\| paymentCards.Count == 0` |
| Might on condition | `DowseWithHolyWater`, `EmberHarvest` | `ApplyPassiveEvent` + `GetX(IsUpgraded)` helper |
| Prayer / buff | `IncreaseFaith`, `LitanyOfWrath` | `Type = CardType.Prayer`, `Target = "Player"` |

## Implementation rules

1. **Damage:** always `GetDerivedDamage(entityManager, card)` in `OnPlay` — never raw `Damage`.
2. **Block:** set `Block` property; play pipeline applies it (do not duplicate in `OnPlay` unless card has custom `OnBlock`).
3. **Constants:** private `const` or fields; interpolate into `Text` via helpers like `GetMightGained(IsUpgraded)`.
4. **CanPlay:** pure bool, no side effects.
5. **OnCantPlay:** publish `CantPlayCardMessage` with ASCII-only text (e.g. `"Requires 2 vigor!"`).
6. **OnUpgrade:** stat bumps (`Damage +=`, `Block +=`); refresh `Text` when numbers in text change. Guard `card == null` only for one-time meta effects (see CLAUDE.md).
7. **Payment cache:** populated before `OnPlay` — safe to read in `OnPlay`:

```csharp
var cacheEntity = entityManager.GetEntitiesWithComponent<LastPaymentCache>().FirstOrDefault();
var paymentCards = cacheEntity?.GetComponent<LastPaymentCache>()?.PaymentCards;
```

8. **Strings:** ASCII only in anything drawn with `SpriteFont` (card text, `CantPlayCardMessage`).

## Minimal attack template

```csharp
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class MyCard : CardBase
    {
        public MyCard()
        {
            CardId = "my_card";
            Name = "My Card";
            Target = "Enemy";
            Cost = ["Any"];
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 7;
            Block = 2;

            OnPlay = (entityManager, card) =>
            {
                EventManager.Publish(new ModifyHpRequestEvent
                {
                    Source = entityManager.GetEntity("Player"),
                    Target = entityManager.GetEntity(Target),
                    Delta = -GetDerivedDamage(entityManager, card),
                    AttackCard = card,
                    DamageType = ModifyTypeEnum.Attack
                });
            };
        }
    }
}
```

## Factory registration

In [ECS/Factories/CardFactory.cs](../../../ECS/Factories/CardFactory.cs), add **both**:

```csharp
"my_card" => new MyCard(),
// ...
{ "my_card", new MyCard() },
```

Keep entries **alphabetical** by `CardId`.

## Clarify before implementing

Ask only when the spec is ambiguous:

1. **Card name** — if not given, offer 2-3 thematic options (avoid reusing overused words like "Strike" unless requested).
2. **Spend vs gate** — courage/vigor "can't play without X" may mean gate-only (keep stacks) or spend-on-play (like `Stab`). Default: match wording; ask if unclear.
3. **Payment condition** — "a scorched card" = at least one payment card has `Scorched`.

## Verification

- [ ] `CardId` matches factory key exactly
- [ ] Both factory sites updated
- [ ] `dotnet build` passes with 0 errors
- [ ] No tests added unless user requested

## Reference implementations

| Card | Why |
|------|-----|
| `StokedAssault` | Vigor gate, free action, +damage upgrade |
| `EmberHarvest` | Payment scorched check, might reward, multi-stat upgrade |
| `Impale` | Courage spend, free action, damage+block |
| `Reap` | `GetConditionalDamage` from payment colors |
| `UnburdenedStrike` | Bonus when zero payment cards |
| `DowseWithHolyWater` | Conditional might + `GetMight(IsUpgraded)` |

More examples: [examples.md](examples.md)
