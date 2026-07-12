# Explicit Visual Effect Choreography Plan

## Summary

Replace text-based visual-effect inference with a hybrid authoring model:

- Explicit, composable choreography for cards and enemy attacks.
- Runtime visuals driven by confirmed conditional gameplay outcomes.
- Conservative defaults based only on structured fields.
- No parsing of display names or rules text.

`VisualEffectSequenceAuthoring` currently attempts to infer semantics from `Name`, `Text`, costs, values, and attack identifiers. This is inherently unreliable because display text contains conditions, negation, thresholds, percentages, and references to outcomes that may not occur. It also means wording-only changes can silently alter animation timing and composition.

The replacement model treats gameplay rules as authoritative and makes visual intent explicit. Shared builders provide consistent choreography without requiring every definition to repeat timing and module details.

## Key Changes

- Replace fuzzy text parsing and hash-based variation with reusable sequence and beat builders for attack style, support style, target, weight, timing, and sound.
- Let card and enemy-attack constructors explicitly assign their primary choreography.
- Preserve a basic fallback derived only from structured fields such as card `Type`, `Target`, and `Damage`, or enemy attack `Damage`.
- Restore meaningful explicit styles currently represented by helpers such as `HeavyHammerEffect`, `HolyStrikeEffect`, `EnemyBiteEffect`, and `EnemyRockBlastEffect`.
- Keep conditional outcomes out of static sequences. Trigger healing, courage, status, and similar secondary visuals only after the corresponding gameplay event confirms that the outcome occurred.
- Retain `DrivesGameplayImpact` on exactly one enemy-attack beat so gameplay resolution remains synchronized with presentation.
- Remove the requirement that every definition have an artificially unique visual signature. Definitions may intentionally share choreography.
- Do not add save migration or backward compatibility; visual-effect authoring is definition and transient presentation state.

## Public Interfaces and Types

Introduce typed visual-authoring options or equivalent strongly typed parameters for:

- Primary attack style, including light slash, heavy impact, holy strike, bite, claw, and rock blast.
- Support style, including general benefit, holy support, defensive guard, and ritual effects.
- Visual weight or intensity tier.
- Target role.
- Optional secondary beats that are guaranteed to occur whenever the definition resolves.

Provide a small factory or builder API that returns `VisualEffectSequence` and owns shared timing, module composition, particles, and sound defaults. Card and enemy-attack definitions should select a style and apply only intentional overrides rather than construct low-level beats repeatedly.

Continue using the existing runtime types:

- `VisualEffectSequence` as the complete choreography.
- `VisualEffectBeat` as one timed visual moment.
- `VisualEffectRequested` as the request consumed by the battle presentation systems.

Replace the current fuzzy fallback with a structured fallback that never reads `Name` or `Text`. The fallback should provide a safe basic attack or support effect, not attempt to make the definition bespoke.

Conditional visuals should use existing authoritative gameplay events where available. Add a narrowly scoped outcome event only when no existing event communicates that the effect successfully occurred. The presentation layer must react to the confirmed result rather than predict it from static definition text.

## Migration Approach

1. Introduce the typed builders and conservative structured fallback.
2. Convert the existing card effect helper methods from empty placeholder recipes into explicit sequence builders or replace their call sites with the new typed API.
3. Migrate existing card recipe selections and enemy attack styles into explicit sequences.
4. Audit compound definitions and author multiple static beats only when every beat always occurs.
5. Route conditional secondary effects through successful gameplay outcomes.
6. Update debug previews to display the explicitly authored sequences.
7. Remove semantic keyword lists, number extraction, text/name parsing, stable hashes, and hash-driven timing variation after all definitions use the new path.
8. Remove or consolidate obsolete `VisualEffectRecipe` card/enemy plumbing only after confirming equipment and medal consumers still require it.

Equipment and medal behavior is otherwise out of scope. Any changes to those definitions should be limited to mechanical compatibility with shared visual-effect interfaces.

## Test Plan

- Every registered card and enemy attack produces a non-empty valid sequence.
- Every enemy attack contains exactly one gameplay-driving beat.
- Explicit heavy, holy, bite, claw, slash, rock, defensive, and support styles produce their intended modules and targets.
- `Seize` never produces a courage-gain visual merely because its text mentions lost courage.
- Negated and conditional rules text cannot alter generated choreography.
- Editing a definition's `Name` or `Text` does not change its sequence.
- Conditional visuals appear only when the corresponding gameplay outcome succeeds.
- Conditional visuals do not appear when the relevant condition fails or the effect is prevented.
- Definitions may intentionally share sequence signatures.
- Debug previews continue to enumerate and render card and enemy-attack sequences.
- Existing visual-effect request and timing tests continue to pass.
- Run focused visual-effect tests, then run `dotnet build` from the repository root.

## Acceptance Criteria

- No runtime visual-effect authoring logic parses card or enemy-attack display text or names.
- Adding or editing player-facing wording cannot change choreography.
- Every definition can select an intentional visual style with a concise constructor assignment.
- Definitions without explicit choreography receive a predictable structured fallback.
- Conditional outcome visuals are emitted only from confirmed gameplay results.
- Enemy attacks preserve exactly one authoritative gameplay-impact moment.
- The complete project builds successfully.

## Assumptions

- Explicit choreography remains alongside each card or enemy-attack definition.
- Shared builders own common timing and module composition.
- Gameplay rules remain authoritative for whether conditional secondary effects occur.
- Identical choreography across multiple definitions is valid when intentionally selected.
- Existing saves do not require migration; validation may use `dotnet run -- new`.
