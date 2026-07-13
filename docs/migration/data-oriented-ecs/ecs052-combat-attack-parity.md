# ECS-052 combat attack parity

Status: the highest-risk combat-owned attack stages now have deterministic end-to-end
characterization. Four conversion regressions were repaired. Card-zone effects and blocker
availability normalization remain cross-domain blockers and are called out explicitly below.

## Legacy source boundary inspected

The audit read these deleted suites from `git show HEAD:<path>` and compared them with
`CombatRuntimeTests`, `EnemyBehaviorParityTests`, generated enemy/attack definitions,
`EnemyContentBehavior`, and `CombatSession`:

| Deleted suite | Declarations | Legacy responsibility |
| --- | ---: | --- |
| `FallenShepherdAttackTests` | 13 | Three-phase planning, attack registration, card-only blocker restrictions, reveal/hit passives, and selected-card threshold discard. |
| `FrostEaterTests` | 4 | Frozen-card planning eligibility, stable/reversible effective-block override, clamping, and resolved damage. |
| `InfernalExecutionTests` | 1 | Two-blocker condition. |
| `EntombTests` | 1 | Dynamic damage threshold and top-draw brittle application. |
| `FrozenClawTests` | 1 | Fixed threshold and top-draw freeze application. |
| `StrangeForceConditionTests` | 3 | Distinct-color condition and conditional mill. |
| `EnemyDamageThresholdTests` | 8 | Block threshold versus final prevented damage, simultaneous hit/threshold hooks, and display predicates/text. |
| `EnemyAttackProgressOverrideServiceTests` | 2 | Exact-block-count full prevention and card-blocker exhaustion. |
| `EnemyAttackMustBlockRequirementServiceTests` | 6 | Impossible-requirement downgrade using eligible hand/equipment blockers and color restrictions. |

This boundary contains 39 declarations. The earlier `EnemyBehaviorParityTests` already replaces
the Fallen Shepherd phase-planning portion and freezes all enemy planners; this pass concentrates
on attack reveal, blocker, confirm, progress, hit, and threshold stages.

## New deterministic end-to-end coverage

`Ecs052CombatAttackParityTests` adds six tests through `CombatSession`, its mandatory rule queue,
generated attack dispatch, structural command playback, event barriers, and final world state:

1. Sand Slam exact-two confirmation fully prevents damage even when the two blockers provide zero
   block, marks the ordinary card `ExhaustOnBlock`, and does not mark equipment.
2. Fallen Shepherd Cast Out and Break Faith add Colorless/Brittle exactly once to ordinary card
   blockers and skip equipment blockers.
3. Frost Eater subtracts one effective block per frozen blocker, reverses on removal, reapplies on
   reassignment, and resolves the expected eight damage from a two-block frozen card.
4. Mummify's threshold hook runs below its rolled threshold when final damage is positive, does
   not run at the threshold, and does not run when Aegis reduces final damage to zero.
5. Infernal Execution with three Channel applies four Burn on reveal, retains its minimum-two
   blocker requirement, rejects one blocker, and accepts two.
6. Fallen Shepherd Purge the Heretic applies Burn to the player on reveal, while Crook's Scar
   applies one Scar to the player (not the enemy) after a damaging hit.

Together with `EnemyBehaviorParityTests`, this covers every Fallen Shepherd planning phase,
generated registration/arsenal membership, the card-only Cast Out/Break Faith hooks, one phase-1
hit hook, one phase-3 reveal hook, exact-block prevention, Frost Eater progress override, Infernal
Execution's requirement/channel payload, and final-damage threshold gating.

## Proven conversion regressions repaired

### Exact-block override was only a confirmation gate

The converted exact requirement checked blocker count but then used ordinary block arithmetic.
Two zero-block cards therefore allowed confirmation but still took full damage, and no blocker was
marked for exhaust. `EnemyAttackProgress.FullyPreventedBySpecial` now records the special outcome;
confirm playback marks non-equipment blockers with `ExhaustOnBlock`, and impact damage is zero.

### Damage thresholds ignored final prevention

The converted threshold stage checked only assigned block against the rolled threshold. It still
ran when Armor/Guard/Aegis reduced final damage to zero, contrary to the deleted threshold suite.
Threshold dispatch now also requires positive `DamageDealt` after prevention.

### Fallen Shepherd card restrictions were discarded

Cast Out and Break Faith emitted typed `ApplyEffect` commands, but the combat command executor
ignored Colorless/Brittle effects for non-player entities. It also lacked the legacy card-versus-
equipment distinction. The executor now materializes the corresponding card tags, and blocker
dispatch skips these two card-only hooks for equipment assignments.

### Confirmation discarded Frost Eater's progress override

Assignment/removal invoked the generated progress hook, but confirmation performed one final raw
block recomputation without reapplying it. Frost Eater therefore displayed the reduced effective
block and then resolved damage using the unreduced total. Confirmation now reapplies the progress
override immediately after its canonical recomputation.

## Exact cases still uncovered or blocked

- **Entomb and Frozen Claw top-draw applications:** attack content emits card-effect commands, but
  combat has no deck handle/candidate span and `RandomCardZone` is ignored by the combat executor.
  The current target span contains assigned blockers, not the draw pile. Closing this requires a
  root-composed card-command output/event; silently applying the effect to a blocker would be
  incorrect.
- **Strange Force draw/mill:** the distinct-color predicate exists, but reveal draw and conditional
  mill have the same missing combat-to-card command boundary. No end-to-end card-zone assertion is
  currently possible.
- **Frost Eater planning eligibility:** `BuildPlanningFacts` does not receive `FrozenInHand`, so
  the runtime cannot select Frost Eater based on two frozen hand cards. The progress/damage stage
  is covered after deterministic attack injection; planning needs a root/card fact provider.
- **Impossible must-block downgrade:** `CombatSession` has no deck/equipment availability snapshot,
  so it cannot reproduce legacy normalization for zero eligible blockers or color-restricted
  hands. This is a gameplay deadlock risk and requires a card/equipment eligibility fact at reveal.
- **Fallen Shepherd Have No Mercy selected-card discard:** marking/removal and the actual discard
  depend on hand selection and the card-zone executor. Phase-3 planning and other reveal/hit hooks
  are covered, but this exact threshold flow is not.
- **Threshold presentation helpers:** rules text and display-condition formatting belonged to the
  deleted presentation model. Runtime threshold execution is covered; pixel/text parity remains
  with the snapshot/presentation lane.
- **Remaining generated attack hooks:** the audit did not claim exhaustive end-to-end coverage of
  all 91 attacks. Weathering Shot, Basilisk Glare, sealed-card modifiers, selected-color attacks,
  and the remaining random hand effects need a generated attack-stage trace matrix plus the same
  card-command boundary.

## Focused verification

```bash
dotnet test tests/Crusaders30XX.Tests/Crusaders30XX.Tests.csproj --no-restore \
  --filter FullyQualifiedName~Ecs052CombatAttackParityTests --maxcpucount:1
```

Result: 6 passed, 0 failed. An initial attempt was blocked by unrelated concurrent meta/card test
compilation, then the focused suite ran cleanly after those lanes settled.
