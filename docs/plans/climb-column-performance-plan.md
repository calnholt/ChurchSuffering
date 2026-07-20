# Climb Column Performance Plan

## Document Status

- **Status:** Ready for implementation after auditing all climb-state mutation paths listed below.
- **Primary goal:** Make unchanged Climb frames cheap by rebuilding presentation only when source state changes and by batching the plain UI render span.
- **Required final verification:** `dotnet build`, climb/save tests, all Climb snapshot variants, and a deterministic steady-state/transition performance capture.
- **Save compatibility:** None required.

## Evidence and Target

In Climb, `ClimbColumnLayoutSystem.Update` averaged 0.62 ms, `ClimbColumnDisplaySystem.Draw` 0.99 ms, and `ClimbHeaderDisplaySystem.Draw` 0.49 ms. The layout system currently deep-clones climb/loadout data, validates and sometimes saves gameplay state, resynchronizes every column/slot/tooltip, constructs factory objects, clones resource objects, captures presentation snapshots, builds string fingerprints, creates dictionaries, sorts lists, and scans shadow entities every frame. Outside Climb it repeatedly performs cleanup rather than only reacting to scene exit.

Acceptance in a deterministic, unchanged Climb scene:

- Reduce `ClimbColumnLayoutSystem.Update` average to 0.15 ms or less and P95 to 0.30 ms or less.
- Reduce the combined column/header draw-call count by at least 40% without changing visuals.
- Allocate no climb-state clones, enemy/card/medal/equipment definitions, snapshot dictionaries, or fingerprint strings on a steady frame with no transition.
- Preserve every current column transition, slot refresh animation, hover preview, tooltip, purchase, encounter, and event behavior.

## Implementation Plan

### 1. Add authoritative climb-state revision tracking

- Add a monotonic climb revision owned by `SaveCache`. Expose an atomic snapshot API returning the deep-cloned `ClimbSaveState` and its revision together; layout code must not separately fetch the state and revision.
- Increment the revision whenever climb state is created, replaced, reset, saved, restored after a failed transaction, or mutated successfully by a transactional `Try...` method.
- Audit every direct `_save.climb` assignment/mutation in `SaveCache`, plus `SaveClimbState` callers in climb shop/encounter systems, battle completion, medals, and snapshot fixtures. Tests must prove each production mutation path increments exactly once for a successful logical change and not for a failed/rolled-back transaction.
- Keep revision runtime-only; do not add it to the save schema.

### 2. Split static rebuilds from per-frame animation

- Give `ClimbColumnLayoutSystem` cached source state: last climb revision, a layout-settings signature, cached climb snapshot, cached loadout-derived metadata, and whether the previous frame was in Climb.
- On first entry, revision change, or layout-setting change, rebuild columns, slots, bounds, actions, tooltips, affordability, and typed visual snapshots once.
- On an unchanged frame, update only active column/slot transition time, animation offsets/opacities, and input suppression. If no animation is active, return after the scene/revision/signature checks.
- Run scene-exit cleanup only on the Climb-to-non-Climb edge. Do not call `DeactivateClimbUiEntities` and suppression restoration on every frame spent in another scene.
- Move `EnsureEncounterMutationTargets` and its possible save write out of the presentation/layout update. Run it from the climb gameplay/lifecycle owner when the run state is created or the Climb scene is entered, then persist through the existing state flow.

### 3. Remove rebuild-time duplication and allocation churn

- Pass the single cached climb snapshot/resources into every slot synchronization method; remove per-shop `SaveCache.GetClimbState()` calls.
- Resolve enemy names, portraits, shop item tooltip models, card display text, and event definitions once per rebuild. Store the display-ready values on `ClimbSlotPresentation`; `Draw` must never invoke factories or `SaveCache`.
- Replace string fingerprints and per-frame `ToDictionary`/`Distinct`/`OrderBy` comparison with typed immutable snapshot equality in fixed slot order. Build refresh jobs only when the climb revision changes, then animate the resulting jobs without re-diffing.
- Keep refresh-shadow entities in a cached list or fixed index map. Hide/reset them only when entering or leaving a refresh animation, not every idle frame.
- Update remaining-duration/display-time fields on presentation during the rebuild or when climb time changes; remove `SaveCache.GetClimbState()` calls from `ClimbColumnDisplaySystem.DrawSlot`.

### 4. Cache render order and batch the plain UI span

- Publish ordered column and slot entity IDs through a root presentation component owned by the layout system. `ClimbColumnDisplaySystem` reads that component instead of querying and sorting entities every draw.
- Cache ASCII-filtered strings and text measurements on rebuild for labels that do not animate. Dynamic remaining-time text may update only when its integer value changes.
- In `ClimbSceneSystem`, bracket the contiguous column/header sprite-only span in a `Deferred` SpriteBatch pass using the same render state and transform. Preserve exact draw order; shader backgrounds, clipped card/modal content, and effects that change graphics state remain in their existing isolated passes.
- Coordinate the pass helper with `battle-render-submission-performance-plan.md` if that plan lands first. Do not create a second incompatible state-restoration abstraction.

## Interfaces and Tests

- **New data API:** `SaveCache.GetClimbSnapshot()` returning state plus runtime revision.
- **New presentation data:** typed slot snapshot equality and ordered column/slot entity IDs on the Climb root.
- Add tests for climb revision semantics, unchanged-frame fast-path behavior, scene-exit edge behavior, slot diff/job generation, and cache invalidation when debug-editable layout values change.
- Add allocation assertions or benchmark counters for clone/factory/snapshot work on steady frames.
- Verify every fixture in the Climb section of `docs/display-snapshots.md`, including no-events, hazard/character events, hover previews, confirmations/dialogue, sold slots, tooltips, replacement modal, header, and resource acquisition.
- Capture performance for idle two-column, idle three-column, column enter/leave, slot refresh, hover preview, and tooltip-visible variants.

## Dependencies and Boundaries

- Services remain read-only calculators; gameplay-state mutation stays in systems through existing state/event flows.
- Draw functions render cached presentation only and do not update save, animation, tooltip, or cache state.
- `scene-module-composition-plan.md` may later replace scene lifecycle wiring, but this plan's revision/fast-path behavior must remain independent.
- `ui-draw-primitives-consolidation-plan.md` may replace helper calls but is not a substitute for revision gating or batching.

