# Guided Tutorial is outside the run lifecycle

The Guided Tutorial is a one-time meta-progression sequence that runs before any run exists. It is not a quest node, queued encounter, procedural map battle, or resumable run battle.

The tutorial owns temporary `GuidedTutorial`, `StockHand`, player, deck, and queued battle entities. Its two enemies are explicitly classified as tutorial-only and cannot enter procedural enemy pools. Authored stock hands replace ordinary deck construction while retaining only the pledged Fervor called out by the script.

Tutorial combat uses normal battle systems and presentation, but it does not publish kill or quest-completion flow, grant rewards, update achievements, record card usage or run telemetry, or enter run-failure and abandon flow. Completion is persisted as meta, temporary entities are destroyed, and the game transitions to WayStation with no active run. Interruption leaves completion unset, so title routing restarts battle one and its opening dialogue.

`skip-tutorials` records tutorial completion and all contextual tutorial keys covered by the guided sequence before routing to WayStation. WayStation Depart remains the only operation that creates a run.
