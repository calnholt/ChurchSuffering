---
name: create-card
description: Create or update ChurchSuffering CardBase cards, including typed IDs, factory registration, combat behavior, upgrades, stat providers, VFX choreography, Guardian dialogue, and verification. Use when the user asks to add or implement a card, supplies card text/stats/upgrades, or mentions card registration.
---

# Create Card

Add a card using the repository's current typed-ID and authored-presentation pipeline.

**Read first:** [ECS/Objects/Cards/CLAUDE.md](../../../ECS/Objects/Cards/CLAUDE.md) and the relevant verification section in [docs/build-run.md](../../../docs/build-run.md).

## Workflow

1. Parse name, stats, text, cost, type, free-action status, upgrade, and conditions. Ask only when a missing choice changes behavior.
2. Inspect 1-2 similar cards and the services/components used by the requested effect.
3. Add `ECS/Objects/Cards/{PascalName}.cs`.
4. Register the card everywhere listed under **Registration**.
5. Update affected fixed-count/catalog tests.
6. Run required verification and fix regressions caused by the change.

Do not edit deck JSON, starter decks, shops, or reward tables unless requested. `CardFactory.GetAllCards()` already exposes collectible cards to shared pools.

## Naming and defaults

| Item | Convention / default |
|---|---|
| Class / file | PascalCase, e.g. `EmberHarvest` |
| `CardId` enum | PascalCase, e.g. `EmberHarvest` |
| Serialized `CardId` | snake_case, e.g. `ember_harvest` |
| Display `Name` | Title Case |
| Type / target | `Attack` / `"Enemy"`; prayers use `Prayer` / `"Player"` |
| Cost | `[]` |
| Free action | `false` |
| Rarity | default `Common`; omit unless different |
| Visual recipe | `PlayerAttackEffect()`; prayers use `HolySupportEffect()` |

## Common patterns

| Intent | References / implementation |
|---|---|
| Vanilla attack | `Smite`, `Fervor`; publish `ModifyHpRequestEvent` with `GetDerivedDamage` |
| Attack + block | `Impale`, `StokedAssault`; set `Block`, do not apply it in `OnPlay` |
| Courage spend | `Stab`, `Impale`; validate, spend in `OnPlay`, and publish `CantPlayCardMessage` |
| Vigor gate | `StokedAssault`; validate only, do not spend vigor |
| Payment conditional | `EmberHarvest`, `Reap`, `BatteringBlow`; inspect `LastPaymentCache` |
| Hand-based stat aura | `ShieldbearersVigil`; implement `ICardStatModifierProvider` and let `CardStatModifierService` discover hand providers |
| Prayer / passive | `IncreaseFaith`, `LitanyOfWrath`; publish events rather than mutate state |

## Implementation rules

- Damage must resolve through `GetDerivedDamage(entityManager, card)`, never raw `Damage`.
- Set printed block through `Block`; use `ICardStatModifierProvider` for live conditional damage/block bonuses instead of adding/removing stored modifiers on zone events.
- Keep `CanPlay` side-effect free. Put rejection text in `OnCantPlay`; all rendered strings must be ASCII.
- Use constants for effect values. Refresh `Text` only when an upgrade changes displayed numbers.
- `OnUpgrade` runs with a non-null card during upgraded-instance initialization and with null arguments for one-time meta application. Guard per-instance stat changes with `if (card != null)`.
- Event subscriptions belong in `Initialize`/`Dispose`; one-shot setup belongs in `OnCreate`.
- For payment effects, `LastPaymentCache.PaymentCards` is populated before `OnPlay`.

## Minimal attack

```csharp
public class MyCard : CardBase
{
    private const int DamageUpgrade = 1;

    public MyCard()
    {
        CardId = "my_card";
        Name = "My Card";
        Target = "Enemy";
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

        OnUpgrade = (_, card) =>
        {
            if (card != null) Damage += DamageUpgrade;
        };
    }
}
```

## Registration

Keep each catalog entry near its alphabetical neighbors:

1. Add the enum value and snake_case `ToKey` mapping in `ECS/Data/Ids/GameIds.cs`.
2. Add one constructor entry to `CardFactory.CardConstructors`; `Create()` and `GetAllCards()` derive from this registry.
3. Add explicit, unique choreography in `VisualEffectSequenceAuthoring`. Attack cards must use an attack-compatible `CardStyle`, even when their theme is defensive.
4. Add two distinct ASCII Guardian lines (maximum 80 characters) in `GuardianAngelMessageService`.

## Clarify only when needed

- Missing name: offer 2-3 thematic options.
- “Requires X courage/vigor”: confirm gate versus spend when wording is unclear.
- Payment wording: “a scorched card” means at least one payment card has `Scorched`.

## Verification

- Confirm the class ID, enum value, key mapping, and factory entry agree.
- Update the registered-card count in `VisualEffectSequenceAuthoringTests`.
- Run `dotnet build` from the repo root; zero errors are required.
- Run focused tests for changed services plus `VisualEffectSequenceAuthoringTests` and `GuardianAngelMessageServiceTests`.
- For card/combat changes, run the broader test command and `dotnet run -- test-fight hammer skeleton hard` from `docs/build-run.md`; report unrelated baseline failures separately.

More examples: [examples.md](examples.md)
