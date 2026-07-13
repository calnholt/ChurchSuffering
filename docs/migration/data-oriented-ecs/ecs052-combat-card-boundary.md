# ECS-052 combat/card boundary parity

## Outcome

The converted combat runtime now has a battle-scoped command/fact boundary for card state. Combat
does not query card buffers directly and does not receive a card system. The card domain owns the
implementation and all deck mutations.

The boundary closes these deleted-legacy-suite cases:

- `EntombTests` and `FrozenClawTests`: a successful damage-threshold effect applies brittle/frozen
  to the top draw card only. It does not search deeper when the top card is ineligible.
- `StrangeForceConditionTests`: reveal draws one card; a hit mills one only when two distinct
  blocking colors were not supplied.
- `FrostEaterTests`: enemy planning receives the current frozen-card count from the bound hand.
- `EnemyAttackMustBlockRequirementServiceTests`: an impossible minimum/exact blocker requirement
  is removed at reveal. Eligibility includes usable hand colors and active equipped equipment and
  honors only/exclude-color restrictions.
- `FallenShepherdAttackTests`: Have No Mercy selects one hand card at reveal, discards it only when
  its normal or damage-threshold condition succeeds, and always clears the selection afterward.

## Contracts and ownership

`ICombatCardBoundary` is the root-composable seam. It exposes a compact `CombatCardFacts` snapshot,
bounded candidate copying, and unmanaged `CombatCardCommand` values. Combat owns when commands are
issued; `CombatCardBoundary` owns draw, mill, zone movement, and card-effect structural changes.

The command set is intentionally small: apply/remove a card effect, draw, mill, and resolve a marked
discard. Existing card movement notifications continue through `CardGameplayEventHub`.

## Root composition

Bind the boundary before the combat session's first process call so the first planned intent sees
the correct hand facts:

```csharp
var cardBoundary = new CombatCardBoundary(world, cardGameplayEventHub);
combatSession.BindCardBoundary(deck, cardBoundary);
```

No additional global event IDs or generated IDs are required. The card event hub's ordinary routes
must already be part of the root event endpoint because draw, mill, and move notifications use them.

## Verification

`Ecs052CombatCardBoundaryParityTests` contains eight deterministic integrated tests. The focused
combat/card regression selection passes 33 tests:

```text
dotnet test tests/Crusaders30XX.Tests/Crusaders30XX.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~Ecs052CombatCardBoundaryParityTests|FullyQualifiedName~Ecs052CombatAttackParityTests|FullyQualifiedName~CardIntegratedOutcomeParityTests|FullyQualifiedName~CardGameplayRuntimeTests|FullyQualifiedName~CardBehaviorTraceTests" \
  --maxcpucount:1
```

## Remaining scope

This lane deliberately does not bind the boundary in `Game1` or `DataOrientedGameRuntime`; root
composition owns the live deck/session relationship. Other converted enemy attacks that require
broader hand/draw candidate routing remain separate parity work unless covered by the commands above.
