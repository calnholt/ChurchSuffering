# Persist unresolved deck reward offers and bypass copy limits on exchange

## Status

Accepted (2026-06-16)

## Context

Quest rewards grant gold plus a deck reward offer. A player can reach the reward modal, leave the process before choosing a card, and later continue the same active run from save. The unresolved offer must survive that interruption so the run cannot lose, duplicate, or reroll the pending deck reward.

Deck construction copy limits still define starting-deck validity: at most one copy of a card identity and printed-color pairing, and at most two copies of the same card identity across all printed colors. Run rewards are different. Shops already allow intentional duplicate stacking during a run; deck reward offers should follow the same run-growth rule instead of silently filtering or blocking desirable reward choices.

This supersedes ADR 0003's assumption that quest rewards enforce copy limits.

## Decision

1. **Persist unresolved offers**: a quest reward creates a deck reward offer that remains part of active-run save state until resolved. Loading an active run with an unresolved offer resumes that offer rather than creating a new randomized reward.

2. **Exchange semantics**: resolving the offer chooses exactly one lane and replaces that lane's targeted loadout entry in place. "In place" preserves the ordered deck position, not run-entry identity: the outgoing entry ends and the incoming card receives a new entry ID.

3. **Copy-limit bypass**: deck reward exchanges ignore copy limits. Copy limits remain a starting-deck construction rule, not a cap on run-grown decks.

4. **Gold separation**: quest reward gold remains separate from medal bonus gold and other simultaneous grants. Persisting or resolving a deck reward offer does not change the modal's quest reward gold accounting.

## Consequences

- **Positive**: Reward interruption is recoverable without rerolling rewards, run-grown decks have one duplicate policy across shops and deck reward exchanges, and replacement cannot accidentally carry entry-owned restrictions to the incoming card.

- **Negative**: Run decks can exceed copy limits through more than one source, so UI and deck logic must treat copy-limit violations as valid active-run states.
