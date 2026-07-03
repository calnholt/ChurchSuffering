# Climb replaces run map terminology and owns mid-run routing

## Status

Accepted (2026-06-18)

## Context

The current implementation still contains names from the older run-map and quest-node model. Those names blur three separate concepts:

1. The active run lifecycle.
2. The mid-run progression space between WayStation and final victory or failure.
3. Individual fightable encounters and non-combat events inside that progression space.

The game needs stable language before systems are moved. The new term is **Climb**. A Climb is the mid-run progression layer for an active run: it presents encounter choices, shops, Treasure Chests, and narrative events, and it routes the player into battle or other run activities.

## Decision

1. **Scene contract**: `SceneId.Climb` is the canonical scene id for the mid-run progression layer. Task 1 only adds the route target; it does not implement the scene or retarget existing systems.

2. **Routing contract**: after WayStation creates an active run and completes any required root/start battle flow, mid-run navigation should route to `SceneId.Climb`. Battle completion, shop exit, narrative-event exit, and reward dismissal should return to Climb when the run remains active. Run failure, run victory, and run abandon route back to WayStation after persisting an inactive run.

3. **Terminology**:
   - Use **Climb** for the mid-run progression layer.
   - Use **Climb encounter** for a fightable combat stop inside the Climb.
   - Use **Queued encounter** only for one enemy fight inside a multi-fight battle queue.
   - Use **Climb event** for a non-combat choice landmark inside the Climb.
   - Use **Encounter reward** for rewards granted by completing a Climb encounter.
   - Use **Final encounter** for The Gate / Fallen Shepherd endpoint.
   - Use **Run abandon** for voluntary quit that ends the active run.

4. **Legacy scope**: existing code symbols may keep `Quest`, `RunMap`, or `Location` names until the owning systems are migrated. New docs and user-facing text should use the Climb terms above unless referring to a legacy implementation detail.

## Consequences

- **Positive**: Future scene work can target `SceneId.Climb` without re-litigating terminology, and docs distinguish run lifecycle from mid-run progression and individual fights.

- **Negative**: During migration, code will temporarily contain legacy names while documentation uses Climb terminology. Reviewers should treat this as intentional until the implementation tasks rename or replace those systems.
