# Card creation examples

## Stoked Assault — vigor gate, free action

```csharp
// Vigor gate only (no spend). IsFreeAction. Upgrade +1 damage.
CanPlay = (em, card) => VigorService.GetPlayerVigorStacks(em) >= VigorRequired;
OnCantPlay = (em, card) => { /* CantPlayCardMessage: Requires 2 vigor! */ };
OnUpgrade = (em, card) => { Damage += DamageUpgrade; };
```

See: `ECS/Objects/Cards/StokedAssault.cs`

## Ember Harvest — scorched payment, might bonus

```csharp
// Cost ["Any"]. OnPlay: damage + conditional might from LastPaymentCache.
private static bool AnyScorchedPayment(IEnumerable<Entity> paymentCards) =>
    paymentCards?.Any(p => p?.GetComponent<Scorched>() != null) == true;

OnUpgrade = (em, card) =>
{
    Damage += DamageUpgrade;
    Block += BlockUpgrade;
    Text = $"If a scorched card was discarded to play this, gain {GetMightGained(IsUpgraded)} might.";
};
```

See: `ECS/Objects/Cards/EmberHarvest.cs`

## Reap — conditional damage from payment colors

Uses `GetConditionalDamage` (not inline bonus) so card face shows correct damage:

```csharp
GetConditionalDamage = (entityManager, card) =>
{
    // count red payment cards; return DamageBonus if count == 2 else 0
};
```

See: `ECS/Objects/Cards/Reap.cs`

## User spec -> implementation mapping

**Input:** `"If a scorched card was discarded to play this, gain 2 might." 1 any cost, 7 damage, 2 block; upgrade = +1 might, +1 block, +1 damage`

| Spec field | Code |
|------------|------|
| 1 any cost | `Cost = ["Any"]` |
| 7 damage / 2 block | `Damage = 7; Block = 2;` |
| scorched payment -> 2 might | `OnPlay` checks `LastPaymentCache`, `ApplyPassiveEvent` Might |
| upgrade +1 might | `GetMightGained`: 2 base, 3 upgraded |
| upgrade +1 block, +1 damage | `OnUpgrade`: `Block += 1; Damage += 1;` |

**Input:** `"You can't play this if you don't have 2 vigor." 4 damage, 3 block, isfreeaction = true; upgrade: +1 damage`

| Spec field | Code |
|------------|------|
| vigor gate | `CanPlay` + `OnCantPlay`, no vigor spend in `OnPlay` |
| 4/3, free action | `Damage = 4; Block = 3; IsFreeAction = true;` |
| upgrade +1 damage | `OnUpgrade`: `Damage += 1;` |
