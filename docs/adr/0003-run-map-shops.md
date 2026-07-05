# Run-map shops are off-tree vendors unlocked by completed-quest fog

## Status

Superseded by [ADR 0009](0009-time-based-climb.md) (2026-06-20); originally accepted (2026-05-30)

## Context

The desert run map is a procedural tree of 20 combat nodes. We want three card shops per run: visible landmarks from the start, enterable only after the player has cleared nearby battles, selling fixed non-starter listings at 30 gold with no separate collection layer.

Quest nodes use radius reveal when a completed quest's fog circle reaches them; only **completed** quests clear map fog. Shops need different rules: always visible on the map (drawn after fog composite), not in the battle tree, and enterable only when inside **completed** quest cleared fog.

Deck building normally enforces copy limits (one per identity+color, two per identity). Shop design allows stacking duplicates the player could not get from quest rewards.

## Decision

1. **Separate save list** `runMapShops` (ids `shop_0`..), each with `worldX`/`worldY` and three `RunMapShopItem` listings. The 20-node `runMapNodes` list is unchanged.

2. **Generation order**: build the battle tree, then scatter three shops. Placement keeps clearance from battle nodes; each shop lies within `DefaultRevealRadius` of at least one battle node so a completion can unlock it.

3. **Enterable rule**: a shop is enterable when its position lies inside cleared map fog from any **completed** quest (`RevealRadius`, center-to-center). During the post-battle reveal cutscene, the expanding fog on the just-completed quest also counts. Revealed-but-incomplete quests do not unlock shops.

4. **Map UX**: map-only scene capture, then fog composite, then shop icons on top (always visible landmarks); minimap dot always shown; no hover tooltip or hold-X until enterable; shops do not clear fog.

5. **Purchases**: spend gold, append the exact `cardId|color` listing to `loadout_1`, mark the shop slot `isPurchased`. Shop purchases **ignore** deck copy limits; quest rewards still enforce them.

6. **Scope**: shops only on **new** run map generation. No migration of in-progress saves. Remove the legacy JSON `LocationDefinitionCache` shop inventory path.

## Consequences

- **Positive**: Keeps 20 combat encounters; shops do not steal nodes or alter tree reveal logic; inventory is deterministic from `runMapSeed`; glossary in `CONTEXT.md` stays the player-facing source of truth.

- **Negative**: Saves created before this feature have no shops until a new run. Enterability requires a runtime distance check against completed nodes (or cached refresh on quest complete). Allowing shop dupes can produce decks that break assumptions in UI that expect copy limits.

- **Superseded**: The standalone run-map shop scene was removed when Climb became the active run progression scene. Climb shop slots now own the current shop flow.
