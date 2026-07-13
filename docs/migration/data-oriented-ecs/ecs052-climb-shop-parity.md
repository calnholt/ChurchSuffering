# ECS-052 climb, shop, and reward parity

This note records the data-oriented replacement delivered for the highest-risk
climb/shop behavior from the deleted `ClimbRule`, `Shop`, `Encounter`, `Event`,
`Collection`, `BattleClimb`, and `WayStation` suites. It is intentionally a
bounded parity claim, not a claim that every deleted feature is restored.

## Implemented replacement

The climb root now owns its shop economy and deterministic schedule state:

- red, white, and black resources plus climb time;
- shown-shop-item history;
- three deterministic encounter schedule entries;
- five deterministic event schedule entries; and
- five shop offer entities in the fixed display order: two medals, equipment,
  card upgrade, then card replacement.

Shop generation is deterministic for the climb seed and time. The two medal
offers are distinct. Equipped and previously shown medal/equipment definitions
are excluded while candidates remain. Each offer stores its resource costs,
time cost, target-card order, and sold state.

A purchase is atomic: affordability, sold state, and card-target validity are
checked before any mutation. Successful purchases spend colored resources,
advance time up to 32, synchronize tooltip sold state, and perform the reward:

- medal and equipment purchases add the existing equipped-inventory
  components owned by the run;
- upgrade purchases retain the target card entity and stable deck order;
- replacement purchases replace the target entity while retaining stable deck
  order; and
- replaying a purchased slot is idempotently rejected.

The shop refreshes at crossed time boundaries 8, 16, and 24, and does not
refresh at the final time 32. Refresh replaces the five offer entities and
continues to honor shown-item exclusions.

Encounter generation produces three distinct enemy/region entries. Encounter
duration is 2-5, time cost is 1-3, and the total colored reward pips equal the
time cost. Event generation produces three hazards and two character events in
the exact appearance bands `(1, 6)`, `(7, 12)`, `(13, 19)`, `(20, 25)`, and
`(26, 32)`. Hazard duration is 2-4 with 1-2 reward pips and no time cost;
character duration is 3-5 with one time cost and no resource reward. Lifecycle
processing activates entries reached by a time landing, expires entries at
their end-exclusive boundary, and preempts scheduled/active entries at final
time while retaining pending, resolved, and already expired entries.

Fresh save version 1 now round-trips climb time, colored resources, equipped
inventory rewards, stable deck reward mutations, shown-item history, and exact
shop offers including costs, sold state, and target order. The legacy explicit
`Extract(world, climbSeed, currentColumn, gold)` overload still honors its
supplied host coordinates; the live-state overload reads seed and column from
the climb root.

## Proven regressions fixed

Before this replacement, selecting a climb shop slot only toggled its purchased
flag. It did not enforce affordability, spend an economy, grant a reward,
validate a target, preserve deck identity/order, synchronize the tooltip, or
block replay through a complete transaction.

The save boundary also rerolled the shop and lost sold/cost/target state on
reload. Its extraction API could not independently distinguish explicit host
coordinates from live climb-root coordinates. Both behaviors now have focused
coverage. The shop scheduler's write access to run deck cards is explicitly
ordered after run lifecycle initialization, eliminating the discovered access
conflict.

## Verification

`Ecs052ClimbShopParityTests` contains ten end-to-end tests covering deterministic
schedule construction and lifecycle, deterministic shop construction,
inventory/shown exclusions, atomic unaffordable purchases, routed and
idempotent reward purchase, upgrade/replacement identity, refresh boundaries,
and save round-trip fidelity.

Focused verification on 2026-07-13:

```text
Ecs052ClimbShopParityTests + Ecs050ExternalHostAdapterTests
Passed: 16, Failed: 0, Skipped: 0

MetaGameRuntimeTests + Ecs052GuidedTutorialParityTests +
Ecs052ClimbShopParityTests
Passed: 29, Failed: 0, Skipped: 0
```

Both runs used `--no-restore --maxcpucount:1`.

## Remaining blockers and deliberate gaps

- Offer generation defines a deterministic data-oriented parity contract, but
  does not reproduce the deleted rarity RNG, full card/collection pools, or
  item-specific weighting. A complete collection runtime is not present.
- Replacement auto-upgrade rules, off-weapon collection rules, and broader
  collection validation are not connected.
- Encounter enemy selection uses a compact static pool instead of the complete
  content/collection validation path. Battle-location mutation packages,
  queued battle launch, pending reward selection, and the final encounter are
  not implemented.
- Encounter/event schedules regenerate deterministically from seed and time;
  their individual lifecycle statuses are not persisted. Pending and resolved
  schedule state therefore cannot survive a reload.
- Event definitions are represented by compact stable string IDs. Hazard and
  character resolution, dialogue, and their full gameplay payloads are not
  implemented.
- Quest and booster reward overlays still lack the full event payload and
  accept/skip resolution. The persistence covered here is limited to rewards
  actually granted by shop purchases through the existing inventory and deck
  components.
- Colored-resource acquisition animations are not emitted by shop purchases.
  Reaching final time preempts schedules but does not queue a final encounter
  or another scene transition.

