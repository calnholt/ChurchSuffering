# Run deck cards have stable entry identity

## Status

Accepted (2026-06-20)

## Context

A card key identifies authored card content, including its printed color and upgrade state. It cannot also identify one card instance in a run because duplicate card keys are valid. Hazards and other effects must be able to target one specific card without affecting an identical copy.

Key-based restriction records and parallel provenance or restriction lists cannot safely represent this relationship. They either apply one-card effects to every matching key or depend on positional synchronization across independently mutated collections.

## Decision

1. **Structured entries**: every run-deck card is a structured loadout entry with a stable entry ID. The card key remains content identity; the entry ID is run-instance identity.

2. **Entry ownership**: provenance and run-long card restrictions belong to the entry they describe.

3. **Upgrade**: upgrading changes the card key while preserving the entry ID, provenance, ordered position, and restrictions.

4. **Replacement and exchange**: replacing or exchanging a card ends the outgoing entry and creates a new entry ID at the same ordered position. The new entry does not inherit restrictions.

5. **Removal and addition**: exhausting or removing a card ends that exact entry. A newly added card receives a new entry ID.

6. **Rejected alternatives**: card-key identity and parallel key, provenance, or restriction lists are rejected because duplicate keys and one-card effects make them ambiguous and unsafe.

## Consequences

- **Positive**: Identical cards remain independently targetable across mutation, battle creation, and save/load boundaries.

- **Negative**: Every deck mutation must explicitly preserve an existing identity or allocate a new one according to its semantics.
