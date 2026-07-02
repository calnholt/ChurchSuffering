# Medal creation examples

## St. Elijah — scorched pledge, once per battle, enemy burn

Combines `StLonginus` (status pledge), `StPaulMiki` (once-per-battle), `StSimonOfCyrene` (enemy passive).

```csharp
// Constructor
MaxCount = 1;
Text = $"The first time each battle you pledge a scorched card, the enemy gains {BurnAmount} burn.";

// OnAcquire + StartBattle reset
public override void OnAcquire() => CurrentCount = MaxCount;
private void OnChangeBattlePhaseEvent(ChangeBattlePhaseEvent evt)
{
    if (evt.Current != SubPhase.StartBattle) return;
    CurrentCount = MaxCount;
}

// Trigger
private void OnPledgeAdded(PledgeAddedEvent evt)
{
    if (CurrentCount <= 0) return;
    if (evt?.Card?.GetComponent<Scorched>() == null) return;
    CurrentCount = 0;
    EmitActivateEvent();
}

// Effect
EventManager.Publish(new ApplyPassiveEvent
{
    Target = EntityManager.GetEntity("Enemy"),
    Type = AppliedPassiveType.Burn,
    Delta = BurnAmount
});
```

See: `ECS/Objects/Medals/StElijah.cs`

## St. Benedict — pledge counter every 3

```csharp
MaxCount = 3;
Text = $"Whenever you pledge {MaxCount} cards, gain 1 vigor.";

private void OnPledgeAdded(PledgeAddedEvent evt)
{
    CurrentCount++;
    if (CurrentCount >= MaxCount)
    {
        CurrentCount = 0;
        EmitActivateEvent();
    }
}
```

See: `ECS/Objects/Medals/StBenedict.cs`

## St. Longinus — thorned pledge adds Kunai

```csharp
private void OnPledgeAdded(PledgeAddedEvent evt)
{
    if (evt?.Card?.GetComponent<Thorned>() == null) return;
    EmitActivateEvent();
}

public override void Activate()
{
    var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
    var kunai = EntityFactory.CreateCardFromDefinition(EntityManager, "kunai", CardColor.White, false, 1);
    EventManager.Publish(new CardMoveRequested
    {
        Card = kunai,
        Deck = deckEntity,
        Destination = CardZoneType.Hand,
        Reason = Id
    });
}
```

See: `ECS/Objects/Medals/StLonginus.cs`

## St. Lawrence — stat modifier (no events)

```csharp
public class StLawrence : MedalBase, ICardStatModifierProvider
{
    public IEnumerable<CardStatModifier> GetStatModifiers(CardStatQuery query)
    {
        if (query?.Kind != CardStatKind.Damage) yield break;
        if (query.Mode != CardStatQueryMode.Resolution) yield break;
        if (query.Card?.GetComponent<Scorched>() == null) yield break;

        int paymentCount = query.PaymentCards?.Count ?? 0;
        yield return new CardStatModifier
        {
            Delta = paymentCount,
            Reason = Id,
            SourceId = Id,
            SourceType = "Medal",
        };
    }
}
```

See: `ECS/Objects/Medals/StLawrence.cs`

## User spec -> implementation mapping

**Input:** `"The first time each battle you pledge a scorched card, the enemy gains 1 burn."` — saint: St. Elijah

| Spec field | Code |
|------------|------|
| Saint | `Id = "st_elijah"`, `Name = "St. Elijah"`, class `StElijah` |
| First time each battle | `MaxCount = 1`, `OnAcquire` + `StartBattle` reset |
| Pledge scorched card | `PledgeAddedEvent` + `Scorched` component check |
| Enemy gains 1 burn | `Activate()` -> `ApplyPassiveEvent` `Burn` on `"Enemy"` |

**Input:** `"Whenever you pledge 3 cards, gain 1 vigor."` — saint: St. Benedict

| Spec field | Code |
|------------|------|
| Every 3 pledges | `MaxCount = 3`, increment on `PledgeAddedEvent` |
| Gain vigor | `Activate()` -> `ApplyPassiveEvent` `Vigor` on Player |

**Input:** `"Your brittle cards have +1 block."` — saint: St. Christopher

| Spec field | Code |
|------------|------|
| Passive stat | `ICardStatModifierProvider`, `CardStatKind.Block` |
| Brittle filter | `query.Card?.GetComponent<Brittle>() != null` |
| No events | empty `Initialize` (only set EntityManager/MedalEntity) |

## Test templates

**Trigger emits activate:**

```csharp
var activateCount = 0;
EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);
// publish trigger event
Assert.Equal(1, activateCount);
```

**Activate publishes effect:**

```csharp
entityManager.CreateEntity("Enemy");
ApplyPassiveEvent appliedEvent = null;
EventManager.Subscribe<ApplyPassiveEvent>(evt => appliedEvent = evt);
medal.Activate();
Assert.Equal(AppliedPassiveType.Burn, appliedEvent.Type);
Assert.Equal(1, appliedEvent.Delta);
```
