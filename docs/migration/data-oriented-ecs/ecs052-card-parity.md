# ECS-052 card outcome parity

## Covered integrated outcomes

The focused `CardIntegratedOutcomeParityTests` suite adds six end-to-end card-domain cases:

- deterministic autopay in hand order, including backtracking when an `Any` slot would consume
  the only card that can satisfy a later colored slot;
- exclusion of the played card, weapons, pledged cards, cards outside the hand, cards owned by a
  different deck, and repeated references to the same entity;
- atomic rejection: an invalid multi-card payment leaves the played card, every payment card,
  action points, rule commands, and structural commands unchanged;
- propagation of the actual selected cards into `CardHandlerInput.Payment`, demonstrated by the
  scorched-payment branch of Ember Harvest and the two-red upgraded Reap branch;
- exact identity for duplicate definitions through payment and generated spawn paths, including
  preservation of the requested red/white/black instance color;
- pledge creation, current-turn lock, unlock, play, removal, draw-count exclusion, and persistence
  of the once-per-action-phase opportunity after the pledged card leaves hand.

The implementation fixed three conversion regressions found while adding these traces:

1. Payment validation accepted duplicate references, discard-pile cards, and cards from another
   deck, then continued playing after one or more payment moves failed.
2. `CardPlaySystem` dispatched the caller's stale payment snapshot instead of deriving it from the
   cards actually discarded.
3. Card materialization and `SpawnCard` command execution discarded the requested instance color,
   leaving definitions printable in all colors with `RuleCardColor.None`.

## Explicitly uncovered

This lane does not claim replacement for all 191 deleted card declarations. Remaining gaps include:

- pay-cost overlay selection, cancellation, return animation, and `SelectedForPayment` lifecycle;
- full preview/resolution stat-provider parity outside the payment snapshot branches above;
- assigned-block interaction and post-attack return/discard behavior;
- start-of-turn deck-empty death, duplicate-death suppression, and medal rescue ordering;
- persisted run-deck entry IDs and restriction hydration/removal across a save round trip;
- quest reward selection, replacement and upgrade policies;
- card usage telemetry and achievement reconciliation;
- exhaustive integrated play/block/reactive/lifecycle sequences for all 69 definitions.

The card factory now accepts an explicit instance color, but multi-color definitions intentionally
remain `RuleCardColor.None` when a caller omits it. Combat/save authoring must supply the persisted
or generated instance color; choosing an arbitrary fallback in the card domain would change deck
identity and payment semantics.

## Focused verification

```bash
dotnet test tests/Crusaders30XX.Tests/Crusaders30XX.Tests.csproj --no-restore \
  --filter FullyQualifiedName~CardIntegratedOutcomeParityTests --maxcpucount:1
```

Result: 6 passed, 0 failed. A combined run with `CardGameplayRuntimeTests` and
`CardBehaviorTraceTests` passed 19 tests with 0 failures.
