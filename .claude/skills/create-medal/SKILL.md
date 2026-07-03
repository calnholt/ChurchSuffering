---
name: create-medal
description: Create new Crusaders30XX medals (MedalBase subclasses in ECS/Objects/Medals). Use when the user asks to create, add, or implement a new medal, describes medal text/triggers/effects, or references medal factory registration.
---

# Create Medal

Add a new medal: one `MedalBase` subclass + factory registration + tests + `dotnet build`.

**Do not** create or edit `MedalBase.cs` — it already exists at [ECS/Objects/Medals/MedalBase.cs](../../../ECS/Objects/Medals/MedalBase.cs).

## Workflow

```
- [ ] 1. Parse spec (saint name, trigger, limit, effect, target)
- [ ] 2. Ask only if blocked (saint name/id, ambiguous once-per scope)
- [ ] 3. Find 1-2 similar medals in ECS/Objects/Medals/ and match their pattern
- [ ] 4. Create ECS/Objects/Medals/{ClassName}.cs
- [ ] 5. Register snake_case id in MedalFactory.Create() and GetAllMedals() (alphabetical)
- [ ] 6. Add tests in tests/Crusaders30XX.Tests/MedalCounterTests.cs
- [ ] 7. dotnet build — fix compile errors before done
```

Do **not** edit shop JSON, treasure pools, or reward tables unless the user asks — pools pull from `MedalFactory.GetAllMedals()`.

## Naming

| Item | Convention | Example |
|------|------------|---------|
| Class / file | `St{Name}` PascalCase | `StElijah.cs` |
| `Id` / factory key | `st_{snake_name}` | `st_elijah` |
| Display `Name` | `St. {Saint Name}` | `St. Elijah` |

If the user does not specify a saint, offer 2-3 thematic options (fire -> St. Elijah, pledge -> St. Benedict, etc.).

## Activation pipeline

Medals never call `Activate()` directly from gameplay code. The flow is:

1. **Trigger handler** (event subscription in `Initialize`) checks conditions.
2. **`EmitActivateEvent()`** publishes `MedalActivateEvent`.
3. **`MedalManagerSystem`** receives it, optionally plays `ActivationEffectRecipe`, then calls `medal.Activate()`.
4. **`Activate()`** publishes the actual game effect (`ApplyPassiveEvent`, `CardMoveRequested`, etc.).

Put **condition checks** in the trigger handler; put **effects** in `Activate()`.

## Archetype picker

| User intent | Pattern medals | Key hooks |
|-------------|----------------|-----------|
| Start of battle buff (player) | `StLuke` | `ChangeBattlePhaseEvent` + `SubPhase.StartBattle`; `ActivationEffectRecipe = HolySupportEffect()` optional |
| Start of battle debuff (enemy) | `StSimonOfCyrene` | same phase hook; `ApplyPassiveEvent` targeting `"Enemy"` |
| Pledge counter (every N pledges) | `StBenedict` | `PledgeAddedEvent`; `CurrentCount++`; reset at `MaxCount` |
| Pledge with card status | `StLonginus`, `StElijah` | `evt.Card?.GetComponent<Thorned/Scorched>() != null` |
| Block counter (every N blocks) | `StPeter` | `CardBlockedEvent`; color via `CardColorQualificationService` |
| First time per battle | `StPaulMiki`, `StElijah` | `MaxCount = 1`; `OnAcquire` + `StartBattle` reset `CurrentCount`; gate with `CurrentCount <= 0` |
| On card play | `StRita`, `StJoanOfArc` | `CardPlayedEvent`; filter by `CardId` or `IsWeapon` |
| React to passive gain | `StJerome` | subscribe `ApplyPassiveEvent`; filter `evt.Type` and `evt.Target` |
| On enemy killed | `StSebastian` | `EnemyKilledEvent`; check player HP |
| Meta / climb reward | `StHomobonus` | `ShowQuestRewardOverlay`; may mutate `SaveCache` in handler |
| Passive stat modifier | `StLawrence`, `StChristopher` | implement `ICardStatModifierProvider`; no event subscriptions |
| Add token card to hand | `StLonginus` | `EntityFactory.CreateCardFromDefinition` + `CardMoveRequested` |
| Resurrect from discard | `StRita`, `StPeter` | `DrawRandomCardFromDiscardEvent` |

## Implementation rules

1. **Initialize:** set `EntityManager`, `MedalEntity`, subscribe events.
2. **Dispose:** unsubscribe every event subscribed in `Initialize`.
3. **Constants:** private `const` or fields; interpolate into `Text`.
4. **Strings:** ASCII only in `Text` (SpriteFont constraint).
5. **Targets:** `EntityManager.GetEntity("Player")` or `"Enemy"` for battle entities.
6. **Card status checks:** `evt.Card?.GetComponent<Scorched>()`, `Thorned`, `Brittle`, etc.
7. **Once-per-battle:** `MaxCount = 1`, override `OnAcquire()` to set `CurrentCount = MaxCount`, reset on `ChangeBattlePhaseEvent` when `evt.Current == SubPhase.StartBattle`.
8. **Counter medals:** increment `CurrentCount` in handler; when `>= MaxCount`, set `CurrentCount = 0` and `EmitActivateEvent()`.
9. **Stat modifiers:** implement `ICardStatModifierProvider.GetStatModifiers`; filter `CardStatKind` and card components; yield `CardStatModifier` with `SourceType = "Medal"`.
10. **Visual effect:** set `ActivationEffectRecipe = HolySupportEffect()` in constructor for holy player buffs (`StLuke` pattern). Enemy debuffs usually omit it.

## Minimal event-triggered template

```csharp
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Medals
{
    public class StExample : MedalBase
    {
        private const int EffectAmount = 1;

        public StExample()
        {
            Id = "st_example";
            Name = "St. Example";
            Text = $"At the start of battle, gain {EffectAmount} aegis.";
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EntityManager = entityManager;
            MedalEntity = medalEntity;
            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
        }

        private void OnChangeBattlePhaseEvent(ChangeBattlePhaseEvent evt)
        {
            if (evt.Current != SubPhase.StartBattle) return;
            EmitActivateEvent();
        }

        public override void Activate()
        {
            EventManager.Publish(new ApplyPassiveEvent
            {
                Target = EntityManager.GetEntity("Player"),
                Type = AppliedPassiveType.Aegis,
                Delta = EffectAmount
            });
        }

        public override void Dispose()
        {
            EventManager.Unsubscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
        }
    }
}
```

## Factory registration

In [ECS/Factories/MedalFactory.cs](../../../ECS/Factories/MedalFactory.cs), add **both**:

```csharp
"st_example" => new StExample(),
// ...
{ "st_example", new StExample() },
```

Keep entries **alphabetical** by `Id`.

## Tests

Add to [tests/Crusaders30XX.Tests/MedalCounterTests.cs](../../../tests/Crusaders30XX.Tests/MedalCounterTests.cs):

| Medal type | Tests to add |
|------------|--------------|
| Event trigger | emits `MedalActivateEvent` on valid trigger |
| Filtered trigger | does **not** trigger on invalid input |
| Once-per-battle | fires once, ignores repeat, resets on `StartBattle` |
| Counter | `CurrentCount` increments/resets correctly |
| `Activate()` effect | subscribes to output event; assert target/type/delta |
| Factory | `MedalFactory.Create(id)` and `GetAllMedals().Keys` |

Test pattern:

```csharp
EventManager.Clear();
try
{
    var entityManager = new EntityManager();
    var medal = new StExample();
    medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));
    // publish trigger events, assert
}
finally
{
    EventManager.Clear();
}
```

For `Activate()` tests that need `"Enemy"` / `"Player"`, create those entities first (`entityManager.CreateEntity("Enemy")`).

## Clarify before implementing

Ask only when blocked:

1. **Saint name / id** — all medals use `st_{name}` + `St. {Name}` unless user specifies.
2. **Once-per scope** — "first time each battle" = reset on `StartBattle`; "each quest" = no battle reset (see `StPeter`).
3. **Target** — "gain burn" on enemy vs player; confirm if ambiguous.

## Verification

- [ ] `Id` matches factory key exactly
- [ ] Both factory sites updated (alphabetical)
- [ ] All event subscriptions unsubscribed in `Dispose`
- [ ] Tests added in `MedalCounterTests.cs`
- [ ] `dotnet build` passes with 0 errors

## Reference implementations

| Medal | Why |
|-------|-----|
| `StElijah` | Scorched pledge + once-per-battle + enemy burn |
| `StPaulMiki` | Once-per-battle gate with `CurrentCount` |
| `StBenedict` | Pledge counter every N |
| `StLonginus` | Status-filtered pledge + add card to hand |
| `StLawrence` | `ICardStatModifierProvider` for scorched damage |
| `StJerome` | React to `ApplyPassiveEvent` |
| `StHomobonus` | Meta/climb counter outside battle |

More examples: [examples.md](examples.md)
