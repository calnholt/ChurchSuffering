# Block Cards As Free Action Attacks Medal Implementation Plan

## Document Status

- **Status:** Draft design, ready for implementation.
- **Repository:** `Crusaders30XX`
- **Runtime:** .NET 8.0, MonoGame DesktopGL, ECS architecture.
- **Primary scene:** `SceneId.Battle`.
- **Required final verification after implementation:** `dotnet build` from the repository root.
- **Save compatibility:** None required. Medal IDs are runtime/run data, and existing saves do not need migration.

This plan adds a medal with text:

```text
Your block cards can be played as 3 damage free action attacks.
```

The recommended implementation is an ongoing play-rule override, not a replacement effect. Replacement effects in the current code intercept specific declared replaceable events, such as frostbite damage. This medal instead changes the legal action-phase play profile of a class of cards.

---

## 1. Objective

Add one medal that lets the player use block cards in two ways:

- During Block phase, block cards continue to work as block cards with their normal `OnBlock` behavior.
- During Action phase, block cards may be clicked and played as free action attacks that deal 3 normal attack damage to the enemy.

The Action phase version must feel like a real card play:

- It publishes `CardPlayedEvent`.
- It moves the played card out of hand to the normal played-card destination.
- It participates in existing play tracking.
- It applies Frozen, Sealed, Pledge cleanup, and other accepted-play side effects already owned by `CardPlaySystem`.
- It does not consume AP.

The Action phase version must not behave like block assignment:

- It must not publish `CardBlockedEvent`.
- It must not add `AssignedBlockCard`.
- It must not call `CardBase.OnBlock`.
- It must not satisfy block-phase conditions.

---

## 2. Canonical Product Decisions

### 2.1 Effect category

This medal is an ongoing passive rule override.

Do not implement it with `IReplacementEffectProvider` or `ReplaceableEffectRequest`, because no existing effect is being replaced at resolution time. The card is allowed to enter a different legal play mode before normal card-play validation resolves.

### 2.2 Medal identity

Use:

```text
Name: St. George
Id: st_george
Enum: MedalId.StGeorge
Class: StGeorge
```

Add the medal to:

- `ECS/Data/Ids/GameIds.cs`
- `ECS/Factories/MedalFactory.cs`
- `ECS/Objects/Medals/StGeorge.cs`

The medal text must be ASCII:

```text
Your block cards can be played as 3 damage free action attacks.
```

### 2.3 Damage semantics

The 3 damage is normal attack damage, not fixed effect damage.

Publish damage through:

```csharp
EventManager.Publish(new ModifyHpRequestEvent
{
    Source = player,
    Target = enemy,
    Delta = -3,
    DamageType = ModifyTypeEnum.Attack,
    AttackCard = cardEntity,
});
```

This intentionally allows existing outgoing attack modifiers in `AppliedPassivesService` to apply, including Power, Might, Aggression, Galvanize, Guard interaction, target defense passives, and enemy defeat handling.

### 2.4 Free action semantics

The alternate play consumes no AP.

Do not mutate the underlying `CardBase.IsFreeAction` flag. The card is only free while being played through this medal's alternate profile. Mutating card definitions would leak display and behavior into other contexts.

### 2.5 Block card identity

Do not change `CardBase.Type` from `CardType.Block`.

The card remains a block card for:

- card type icon and type label
- pledge restrictions
- block assignment in Block phase
- deck/card usage telemetry type
- any future effects that check card type

Action-phase behavior is provided by an alternate play profile layered over the card, not by transforming the card into an attack.

---

## 3. Target Design

### 3.1 Add alternate play profile contracts

Add a small query model in the battle/card-play domain, near `CardStatModifierService` or in a dedicated service file under `ECS/Scenes/BattleScene`.

Recommended names:

```csharp
public sealed class AlternateCardPlayQuery
{
    public EntityManager EntityManager { get; set; }
    public Entity Owner { get; set; }
    public Entity Card { get; set; }
    public SubPhase Phase { get; set; }
}

public sealed class AlternateCardPlayProfile
{
    public string SourceId { get; set; } = "";
    public string SourceType { get; set; } = "";
    public Entity SourceEntity { get; set; }
    public bool AllowsPlay { get; set; }
    public bool IsFreeAction { get; set; }
    public bool TreatsAsAttack { get; set; }
    public int AttackDamage { get; set; }
}

public interface IAlternateCardPlayProvider
{
    AlternateCardPlayProfile GetAlternatePlayProfile(AlternateCardPlayQuery query);
}
```

Add a read-only resolver service:

```csharp
internal static class AlternateCardPlayService
{
    public static AlternateCardPlayProfile GetProfile(
        EntityManager entityManager,
        Entity card,
        SubPhase phase)
}
```

The service should:

- Resolve the player as the owner, matching `CardStatModifierService`.
- Iterate equipped medals owned by the player in stable entity ID order.
- Ask medals implementing `IAlternateCardPlayProvider`.
- Return the first non-null profile with `AllowsPlay == true`.
- Return `null` when no alternate play applies.

This mirrors the existing provider pattern used by `ICardStatModifierProvider` without overloading stat modifiers with play legality.

### 3.2 Implement `StGeorge`

`StGeorge` implements `IAlternateCardPlayProvider`.

It returns a profile only when:

- `query.Phase == SubPhase.Action`
- `query.Card` has `CardData`
- `query.Card.GetComponent<CardData>().Card.Type == CardType.Block`
- the card is not a weapon or token

Profile:

```text
AllowsPlay: true
IsFreeAction: true
TreatsAsAttack: true
AttackDamage: 3
SourceType: Medal
SourceId: st_george
SourceEntity: MedalEntity
```

`Initialize` only stores `EntityManager` and `MedalEntity`; no event subscriptions are needed. This is a passive continuous effect, so no `EmitActivateEvent` is required on every preview query.

During actual accepted play, publish `MedalTriggered` once for the play if the profile source is a medal entity. This preserves feedback in `MedalDisplaySystem` without firing repeatedly from preview/highlight queries.

### 3.3 Update `CardPlaySystem`

In `OnPlayCardRequested`:

1. Resolve phase and card data as today.
2. Resolve `alternateProfile = AlternateCardPlayService.GetProfile(EntityManager, evt.Card, phase.Sub)`.
3. Replace the hard block-card rejection with:

```csharp
if (card.Type == CardType.Block && alternateProfile?.AllowsPlay != true)
{
    EventManager.Publish(new CantPlayCardMessage { Message = "Block cards can only be used to block!" });
    return;
}
```

4. AP gate uses alternate free-action state:

```csharp
bool isFree = card.IsFreeAction || alternateProfile?.IsFreeAction == true;
```

Use the same value again when deciding whether to consume AP in `ResolveAcceptedCardPlay`.

5. Card-specific `CanPlay` must not reject block cards solely because their `CanPlay` only allows Block phase. For alternate profile plays:

- Skip `card.CanPlay` for `CardType.Block`.
- Continue normal validation for non-block cards.

This prevents cards like `Stalwart` from failing because its `CanPlay` requires `SubPhase.Block`. Its block-only courage cost should not apply to the Action phase attack version.

6. Cost handling:

- Current block cards have no discard costs for play.
- If a future block card gets a `Cost`, the alternate play should still use normal effective cost handling unless the alternate profile explicitly says otherwise.
- Do not invent cost-bypass behavior beyond AP-free.

7. Resolution:

Update `ResolveAcceptedCardPlay` to accept the alternate profile or recompute it before execution. Passing the accepted profile is preferred so the play mode cannot change between validation and resolution.

Before or instead of `card.OnPlay`, handle alternate attack profiles:

```csharp
if (alternateProfile?.TreatsAsAttack == true)
{
    EventManager.Publish(new MedalTriggered
    {
        MedalEntity = alternateProfile.SourceEntity,
        MedalId = alternateProfile.SourceId,
    });

    EventManager.Publish(new ModifyHpRequestEvent
    {
        Source = player,
        Target = enemy,
        Delta = -alternateProfile.AttackDamage,
        DamageType = ModifyTypeEnum.Attack,
        AttackCard = cardEntity,
    });
}
else
{
    card.OnPlay?.Invoke(EntityManager, cardEntity);
}
```

Do not call `OnBlock`.

8. Keep the existing accepted-play cleanup after the alternate effect:

- `CardPlayedEvent`
- tracking
- Sealed HP cost/removal
- Pledge removal
- AP consumption check using alternate free-action state
- Frozen frostbite application
- destination handling and card movement

### 3.4 Update `CanPlayCardHighlightSystem`

Action-phase playability must use the same alternate profile rule as `CardPlaySystem`.

Modify `IsPlayableInAction`:

- Resolve `alternateProfile`.
- Continue rejecting relics.
- Allow block cards only when alternate profile allows play.
- Use `card.IsFreeAction || alternateProfile.IsFreeAction` for AP checks.
- Skip block card `CanPlay` when alternate profile allows play.
- Keep pledge, silence, and cost checks.

This keeps highlights aligned with actual click behavior.

### 3.5 Update card stat display

`CardDisplaySystem.DrawStatChips` currently shows attack chips only for `CardType.Attack`, and AP/FREE chips only for non-block/non-relic cards.

Modify display to resolve an alternate profile in the current phase:

- In Action phase, if `alternateProfile.TreatsAsAttack == true`, show a `DAMAGE` chip with `AttackDamage` previewed through `AppliedPassivesService.GetPreviewAttackDamage`.
- Show `FREE` / `0` AP for the alternate profile.
- Continue showing the block chip from `BlockValueService.GetTotalBlockValue`.
- Keep the card type label and type icon as block.

Do not show the alternate damage/AP chips in Block phase. During Block phase the card should present primarily as a blocker.

### 3.6 Optional visual effect

No new visual effect recipe is required for v1.

The resulting `ModifyHpRequestEvent` will still produce existing damage presentation through `ModifyHpEvent` and HP display systems. If a card-specific lunge is desired later, that should be added as a separate presentation plan.

---

## 4. Edge Cases And Rules

### 4.1 Pledged block cards

If a block card is pledged and cannot be played this turn, it remains unplayable as an alternate attack. Reuse existing pledge checks.

### 4.2 Silenced pledged cards

The existing silence plus pledge restriction still applies. Do not add a bypass.

### 4.3 Frozen block cards

Playing a Frozen block card as an alternate attack should apply frostbite exactly like any played Frozen card, because `CardPlaySystem` already applies Frostbite after accepted play.

### 4.4 Sealed block cards

Playing a Sealed block card as an alternate attack should apply the sealed HP cost and remove the seal, because it is a real card play.

### 4.5 Block-specific `CanPlay`

Block-specific play restrictions, such as Stalwart's courage requirement during Block phase, do not apply to the alternate attack. The alternate effect is not blocking and should not spend block-only costs.

### 4.6 On-block effects

Cards such as `HoldTheLine`, `HiddenKunai`, and `Stalwart` must not fire `OnBlock` when played as attacks. Those effects remain tied to assignment/resolution as blocks.

### 4.7 Attack passives

Because the damage is `ModifyTypeEnum.Attack`, attack modifiers should apply normally. The base value passed into `ModifyHpRequestEvent` is 3. The original card's `Damage` field should not be used, since block cards often have zero printed damage.

### 4.8 Multiple alternate play providers

For v1, first provider wins in equipped medal entity ID order. This matches the deterministic iteration style used by `CardStatModifierService` and avoids introducing conflict resolution before there is more than one provider.

### 4.9 Medal trigger presentation

Publish `MedalTriggered` only during actual accepted alternate play, not during preview, highlighting, or card rendering. Rendering and highlighting can query every frame and must remain side-effect free.

---

## 5. Tests

Add tests in a new focused test file, recommended:

```text
tests/Crusaders30XX.Tests/StGeorgeMedalTests.cs
```

Use the existing patterns from:

- `CardStatModifierServiceTests`
- `FrostbiteReplacementEffectTests`
- `CardPlayUpgradeTests`
- `CardInputRoutingTests`

Required test cases:

1. `MedalFactory_includes_st_george`
   - `MedalFactory.Create("st_george")` returns `StGeorge`.
   - `MedalFactory.GetAllMedals()` includes `MedalId.StGeorge`.

2. `Block_card_cannot_be_played_without_st_george`
   - Build an Action phase battle.
   - Put a block card in hand.
   - Publish `PlayCardRequested`.
   - Assert enemy HP unchanged, AP unchanged, card remains in hand.

3. `Block_card_can_be_played_as_free_attack_with_st_george`
   - Equip `StGeorge`.
   - Use a block card in hand.
   - Publish `PlayCardRequested`.
   - Assert enemy HP is reduced by 3 before passives.
   - Assert AP is unchanged.
   - Assert card moved out of hand to discard.
   - Assert `CardPlayedEvent` fires once.
   - Assert `MedalTriggered` fires once for `st_george`.

4. `Alternate_block_attack_uses_normal_attack_modifiers`
   - Equip `StGeorge`.
   - Add `AppliedPassiveType.Power` or `Might` to the player.
   - Play a block card as an alternate attack.
   - Assert enemy loses `3 + modifier` damage.

5. `Alternate_block_attack_does_not_fire_on_block`
   - Use a custom block card or existing block card with observable `OnBlock`.
   - Play it during Action phase through `StGeorge`.
   - Assert `OnBlock` side effect did not occur.

6. `Block_phase_assignment_still_uses_normal_block_behavior`
   - Equip `StGeorge`.
   - In Block phase, assign a block card.
   - Assert it adds assigned block and does not deal alternate attack damage.

7. `Can_play_highlight_matches_alternate_play_rule`
   - In Action phase without `StGeorge`, a block card is not considered playable.
   - With `StGeorge`, the same card is considered playable when other restrictions pass.

If direct display assertions are difficult, cover the display-facing query through `AlternateCardPlayService` and keep pixel/snapshot validation optional for this plan.

After implementation:

```bash
dotnet test
dotnet build
```

---

## 6. Acceptance Criteria

- The medal appears in the normal medal factory/pool.
- Equipped `StGeorge` lets Action phase block cards be played from hand.
- The played block card deals 3 base normal attack damage to the enemy.
- Attack passives and enemy HP handling use the same path as attack cards.
- The play costs 0 AP.
- The card still moves through normal played-card cleanup.
- Block phase behavior of block cards is unchanged.
- Preview/highlight/render queries are side-effect free.
- The implementation does not use replacement effects for this medal.
- The implementation does not mutate `CardBase.Type`, `CardBase.Damage`, or `CardBase.IsFreeAction` to fake the effect.

---

## 7. Files Expected To Change

Likely implementation files:

- `ECS/Objects/Medals/StGeorge.cs`
- `ECS/Data/Ids/GameIds.cs`
- `ECS/Factories/MedalFactory.cs`
- `ECS/Scenes/BattleScene/CardPlaySystem.cs`
- `ECS/Scenes/BattleScene/CanPlayCardHighlightSystem.cs`
- `ECS/Scenes/CardDisplaySystem.cs`
- a new alternate play service/provider contract file under `ECS/Scenes/BattleScene`
- `tests/Crusaders30XX.Tests/StGeorgeMedalTests.cs`

Avoid touching save migration code. Existing save compatibility is explicitly out of scope.
