# Card-Status Application Resolver Plan

> A pure selection/eligibility resolver behind one method. Kills a 3-file triple-dispatch and an 8-system copy of the "pick N random eligible cards" idiom whose eligibility predicates have silently diverged. Selection becomes headless-testable over a deck snapshot; systems keep event subscription + animation publishing.

## Document Status

- **Status:** Draft design, ready for implementation review.
- **Repository:** `Crusaders30XX`
- **Runtime:** .NET 8.0, MonoGame DesktopGL, ECS architecture.
- **RFC sequence:** #01 — in-process; effort/risk medium / medium.
- **Required final verification after implementation:** `dotnet build` from the repository root.
- **Save compatibility:** None required.

Pure `CardStatusSelector` reconciles divergent card-status eligibility predicates and centralizes random selection over deck snapshots.

---

## Context

**Why (the friction):** "Apply status X to N random eligible cards" is implemented at least ten times across the battle layer, each a hand-rolled LINQ chain with its *own* candidate source, its *own* eligibility predicate, its *own* count/ordering rule, and its *own* RNG. The canonical idiom — `ResolveCandidates(...).Where(IsEligibleForApplication).Where().Distinct().OrderBy(_=>Random.Shared.Next()).Take(evt.Amount)` — appears verbatim in three files (`CardApplicationManagementSystem.cs:33-39`, `CursedManagementSystem.cs:51-58`, `HexManagementSystem.cs:36-44`), and in *divergent* forms in five more (`PoisonedCardManagementSystem.cs:57-66`, `ShackleManagementSystem.cs:49 -62`, `IntimidateManagementSystem.cs: 70-88`, `RecoilManagementSystem.cs:41-47`, `MarkedForSpecificDiscardSystem.cs:42-46`), plus a sixth EM-coupled variant in `MustBeBlockedSystem.cs:113-178`. Nothing owns "which cards does a status land on." The predicates have drifted apart with no test asserting the intended rules, and the selection is impossible to test without standing up the full event+animation pipeline.

**What prompted it:** an `improve-codebase-architecture` deep-module exploration (2026-07-15) to find small interfaces hiding large implementations — deepen a shallow, duplicated cluster behind a real boundary and make it testable.

**Intended outcome:** one pure `CardStatusSelector` that takes a deck/hand snapshot + a declarative selection request and returns the exact selected entities + a rejection reason, with **one canonical eligibility filter** reconciling the divergent predicates. Systems keep their event subscriptions and their side-effect/animation publishing; the resolver owns *selection only*. Net effect: the divergences become explicit named options instead of copy-pasted loops; selection gets ~unit-level tests with an injected RNG; the accidental behavior differences get reconciled deliberately.

## Problem

### 1. Triple-dispatch across three files subscribed to the same event trio

`CardApplicationManagementSystem`, `CursedManagementSystem`, and `HexManagementSystem` all subscribe the **same three events** in their ctors — `ApplyCardApplicationEvent`, `RemoveCardApplication`, `RemoveCardApplications` (`CardApplicationManagementSystem.cs:17-19`, `CursedManagementSystem.cs:19-21`, `HexManagementSystem.cs:21-22`) — and each early-returns unless `evt.Type` is "theirs":
- `CardApplicationManagementSystem.OnApplyCardApplication:31` — `if (evt.Type == Cursed || evt.Type == Hex || evt.Amount <= 0) return;` (handles Frozen/Brittle/Scorched/Thorned/Colorless/Sealed).
- `CursedManagementSystem.OnApplyCardApplication:49` — `if (evt.Type != Cursed || evt.Amount <= 0) return;`
- `HexManagementSystem.OnApplyCardApplication:34` — `if (evt?.Type != Hex || evt.Amount <= 0) return;` The same negative-space guard is repeated on all three remove handlers (`CardApplicationManagementSystem.cs:75,:81`, `CursedManagementSystem.cs:93,:99`, `HexManagementSystem.cs:58`). This is hand-rolled type dispatch scattered across three files: adding a status means editing the negative guard in the "generic" system *and* remembering none of the special ones swallow it. A test exists purely to assert the generic system does not accidentally act on Cursed events (`CardApplicationManagementSystemTests.cs:326-366`, `Generic_system_ignores_cursed_events_without_throwing`) — an integration test that only exists because the dispatch is implicit.

### 2. The "pick N random eligible cards" idiom, duplicated with divergent predicates

Same shape, eight-plus times, with quietly different rules. Divergences (verified):

| System / method | Candidate source | Eligibility predicate | Not-already-applied | Count / ordering | RNG |
|---|---|---|---|---|---|
| **Frozen/Brittle/Scorched/Thorned/ Colorless/Sealed** — `CardApplicationManagementSystem.OnApplyCardApplication` (`:33-39`) | `ResolveCandidates(em, evt.Card, evt.Target)` — any pile per `Target`, honors `exactCard` | `IsEligibleForApplication` = non-weapon `&& !Pledge` (`CardApplicationTargetingService.cs:41-44`) | `evt.Type == Sealed \|\|!CardApplicationService.IsApplied(card, type)` (Sealed re-stacks) | `.Distinct().OrderBy(Random.Shared.Next()).Take(evt.Amount)` | `Random.Shared` |
| **Cursed** — `CursedManagementSystem.OnApplyCardApplication` (`:51-58`) | `ResolveCandidates` | `IsEligibleForApplication` + `!Cursed` + `!Hexed` | via `!Cursed` | `.Distinct().OrderBy(Random.Shared.Next()).Take(evt.Amount)` | `Random.Shared` |
| **Hex** — `HexManagementSystem.OnApplyCardApplication` (`:36-44`) | `ResolveCandidates` | `IsEligibleForApplication` + `!Cursed` + `!Hexed` + `CardId != Curse.CardIdValue` + `CardId != Hex.CardIdValue` | via `!Hexed` | `.Distinct().OrderBy(Random.Shared.Next()).Take(evt.Amount)` | `Random.Shared` |
| **Poisoned** — `PoisonedCardManagementSystem.ApplyOnePoisonedCard` (`:57-66`) | raw `deck.Hand` (hand only) | `!Pledge` + `!Poisoned` + `BlockValueService.GetTotalBlockValue(card) > 0` — **no weapon exclusion** | via `!Poisoned` | exactly **one**: `candidates[Random.Shared.Next(count)]` (no `Distinct`, no `Take(N)`) | `Random.Shared.Next(count)` |
| **Shackle** — `ShackleManagementSystem.ApplyShackleEffect` (`:49-62`) | `GetComponentHelper.GetHandOfCards` (pre-filters weapon + pledge, see `GetComponentHelper.cs:59`) | `!Intimidated` + `!Shackle` | via `!Shackle` | requires `> 1` candidate (`:56`); always `Take(2)` fixed | **`new Random()`** (fresh, time-seeded, unseedable) |
| **Intimidate** — `IntimidateManagementSystem.ApplyIntimidateEffects` (`:70-88`) | raw `deck.Hand` | `!Intimidated` + `!Pledge` + `!Shackle` + `GetTotalBlockValue > 0` — **no weapon exclusion** | via `!Intimidated` | **poisoned-last** bias: `!Poisoned` shuffled, concat `Poisoned` shuffled, then `Take(amount)` | **`new Random()`** |
| **Recoil** — `RecoilManagementSystem.OnApplyRecoil` (`:41-47`) | `GetHandOfCards` (weapon + pledge pre-filtered) | `!Recoil` | via `!Recoil` | exactly **one**: `.OrderBy(_random.Next()).First()` | instance `_random` |
| **MarkedForSpecificDiscard** — `MarkedForSpecificDiscardSystem.TryPreselectSpecificDiscards` (`:42-46`) | `GetHandOfCards` (weapon + pledge pre-filtered) | **none** (all hand cards) | n/a | `.OrderBy(_random.Next()).Take(Min(evt.Amount, count))` | instance `_random` |
| **MustBeBlocked (auto-assign)** — `MustBeBlockedSystem.GetEligibleBlockCards` (`:113-178`) + `OnAmbushTimerExpired` (`:277-283`) | raw `deck.Hand` | non-null `CardData`, id non-empty, `!IsWeapon && !IsToken`, `!Intimidated`, Block-type must `CanPlay(em, card)`, must meet `BlockingRestriction`, all shackle partners playable | n/a (assignment, not a status) | `.OrderBy(Random.Shared.Next()).Take(Min(needed, eligible.Count))` | `Random.Shared` |

The accidental divergences worth reconciling:
- **Candidate source drift.** Frozen-family/Cursed/Hex route through `ResolveCandidates` (pile-parameterized, `exactCard`-aware); the other five are hand-only, half via `GetHandOfCards` (which silently pre-excludes weapons + pledged, `GetComponentHelper.cs:59`) and half via raw `deck.Hand` (which does not). So Poison and Intimidate can theoretically land on a weapon-in-hand with block > 0, while Shackle/Recoil/Marked cannot — a difference nobody chose.
- **Pledge exclusion is inconsistent.** Frozen-family, Poison, Intimidate exclude pledged; Shackle/Recoil/Marked exclude it only as a side effect of `GetHandOfCards`; the remove paths do *not* (see below).
- **`Distinct()` only on the `ResolveCandidates` paths** (the pile targets can overlap; the hand-only paths assume uniqueness).
- **RNG is unseedable in two systems.** Shackle and Intimidate `new Random()` per call — time-seeded, so two selections in the same tick can correlate, and no test can pin the outcome. Everything else uses `Random.Shared` or an instance field, also unseedable from a test. This is the single biggest reason selection is not unit-tested today.
- **Remove-path asymmetry.** `CardApplicationManagementSystem.OnRemoveCardApplications:83-89` filters with `IsNonWeaponCard` (not `IsEligibleForApplication`), so it can remove applications from *pledged* cards — while apply cannot add to them. `CursedManagementSystem.OnRemoveCardApplications:101-107` is the same shape but keyed on `HasComponent()`.

### 3. Dependency-inversion smell: service → system callback

`CardApplicationService` is presented as a read-only application helper, yet its per-type dispatch dictionary calls **back up into two systems' static methods**:
- `CardApplicationService.cs:57` — `CardApplicationType.Cursed → CursedManagementSystem.ApplyCursedRuntime(em, card)` / remove → `CursedManagementSystem.RemoveCursedRuntime` (`:58`).
- `CardApplicationService.cs:61` — `CardApplicationType.Hex → HexManagementSystem.ApplyHexRuntime(em, card)` / remove → `HexManagementSystem.RemoveHexRuntime` (`:62`). So the call graph is **system → service → system**. It also violates `docs/coding-standards.md:14` ("Services are read-only helpers/calculators. They must not mutate ECS components ...") twice over: `CardApplicationService.ApplyRest riction:71-79` / `RemoveRestriction:81-89` themselves call `entityManager.AddComponent` / `RemoveComponent` (via the dictionary lambdas at `:22-54`) and `RunScopedStateService.SyncCardRestrictionsFromComponents`. `CardApplicationService` is not a read-only service; it is an un-labeled applier that reaches into system internals.

### 4. Control flow deferred into an animation closure

The actual component mutation for card-scoped restrictions is not performed by the management system — it is a lambda captured by the animation and fired mid-tween:
- `BattleCardMutationDisplaySystem.TryStart:155` — `OnSwap = () => CardApplicationService.ApplyRestri ction(EntityManager, target, request.Type, request.StacksPerCard);` The management system only publishes `CardRestrictionMutationAnimationRequested` (`CardApplicationManagementSystem.cs:56`, `CursedManagementSystem.cs:75`, `HexManagementSystem.cs:48`); the graphics system decides *when* the state write happens (and has a headless fallback that applies immediately if the display pair can't be built, `BattleCardMutationDisplaySystem.cs:139`). Selection, eligibility, apply-dispatch, and apply-timing are therefore smeared across a service, three management systems, a targeting service, and a graphics system — with the only ground truth being "run the whole pipeline and see which components appear." That is exactly why every existing test drives `BattleMutationTestSupport.CreateBattleMutationPipeline` + `CompleteMutations` (`BattleMutationTestSupport.cs:12-37`).

## Proposed Interface

A pure static `CardStatusSelector` in `ECS/Services/` (graphics-free, no `EntityManager`, no events). It consumes a caller-built `DeckSnapshot` + a declarative `CardSelectionRequest` and returns a `CardSelectionResult` — the exact selected entities, a rejection reason, and the full eligible set (for logging/tests). **One** canonical eligibility filter (`IsEligible`) replaces the eight scattered predicates; the divergences survive as explicit, defaulted request fields. RNG is injected as `Func` (default `Random.Shared.Next`) so tests are deterministic and production is byte-for-byte unchanged.

**Shape chosen: pure static selector over a value snapshot** (rejected: (a) a selector that also *applies* — re-couples to `EntityManager` mutation + the `CursedManagementSystem`/`HexManagementSystem` static callbacks, keeping the read-only-service violation and losing headless testability; (b) a per-status strategy-object registry — more ceremony than the ~six divergence knobs justify, and it fragments the one thing we want central: the eligibility rule).

```csharp
namespace Crusaders30XX.ECS.Services { //Per-cardfactsprojectedbythe caller.No EntityManager, no components —
puredata.//BlockValue is precomputed because BlockValueService is internal to ECS.Systems and EM-bound
//(BlockValueService.cs:24-28); the resolver mustnot reach for it.public readonly record struct CardFacts( Entity
Card, string CardId, bool IsWeapon, bool IsToken, bool HasPledge, int BlockValue, CardStatusFlags Statuses);
[System.Flags] public enum CardStatusFlags { None = 0, Frozen = 1 << 0, Brittle = 1 << 1, Scorched = 1 << 2, Thorned =
1 << 3, Colorless = 1 << 4, Sealed = 1 << 5, Cursed = 1 << 6, Hexed = 1 << 7, Poisoned = 1 << 8, Shackled = 1 << 9,
Intimidated = 1 << 10, Recoil = 1 << 11, } //Thedeckprojectionthe caller gathers ONCEfrom EntityManager (mirrors
CardApplicationTargetingService piles). public sealed class DeckSnapshot { public IReadOnlyList Hand; public
IReadOnlyList DrawPile; //orderpreserved for TopXCards public IReadOnlyList DiscardPile; public IReadOnlyList
ExhaustPile; //=deck.Cardsminustheother three, for Target.Deck public IReadOnlyList AllCards; //deck.Cards}
//Theonecanonicaleligibility filter, expressed as data.Every scattered predicate collapses tothis.public sealed class
CardEligibility { public bool ExcludeWeapons = true; //frozen-family/Cursed/Hex/MustBeBlocked public bool
ExcludeTokens = false; //onlyMustBeBlockedtoday public bool ExcludePledged = false; //frozen-family/Poison/Intimidate
public bool RequirePositiveBlock = false; //Poison/Intimidate public CardStatusFlags ExcludeIfAny =
CardStatusFlags.None; //e.g.Cursed|Hexed, orthe target status public IReadOnlyCollection ExcludeCardIds =
System.Array.Empty(); //Hex:{Curse.Id, Hex.Id} } public enum SelectionCount { UpToAmount, ExactlyOne, Fixed }
//FixedusesAmount as the exacttake public enum OrderingBias {UniformRandom, PoisonedLast } //Intimidate public enum
SelectionRejection { None, NoEligibleCandidates, BelowMinCandidates } public sealed class CardSelectionRequest {
public CardFacts? ExactCard; //non-nullshort-circuitspileresolution public CardApplicationTarget Target;
//reusesexisting enum (CardEvents.cs:719) public int Amount = 1; public SelectionCount Count =
SelectionCount.UpToAmount; public int MinCandidates = 0; //Shackle=2 public CardEligibility Eligibility = new();
public OrderingBias Ordering = OrderingBias.UniformRandom; public System.Func Rng = System.Random.Shared.Next;
//inject for deterministic tests } public sealed class CardSelectionResult {public IReadOnlyList Selected;
//exactchosenentities, in apply order public SelectionRejection Rejection; public IReadOnlyList EligibleCandidates;
//fulleligiblesetpre-Take(logging/tests) } public static class CardStatusSelector {public static CardSelectionResult
Select(DeckSnapshotdeck, CardSelectionRequest request); //Thesinglesourceoftruth for "maythis card receive an
application". public static bool IsEligible(in CardFactscard, CardEligibility rules); } }
```

**Usage — the common case is trivial.** `CardApplicationManagementSystem.OnApplyCardApplication` collapses to snapshot + one call + publish-per-selected (the animation/side-effect publishing stays in the system):

```csharp
private void OnApplyCardApplication(ApplyCardApplicationEventevt) { if (evt.Amount <= 0) return;
//typedispatchhandledbythe shared registry, notper-file guards var deck = DeckSnapshotBuilder.Build(EntityManager);
//callerownstheEMread var result = CardStatusSelector.Select(deck, new CardSelectionRequest { Target = evt.Target,
ExactCard = evt.Card == null ? (CardFacts?)null : DeckSnapshotBuilder.Project(EntityManager, evt.Card), Amount =
evt.Amount, Eligibility = new CardEligibility { ExcludePledged = true, ExcludeIfAny = evt.Type ==
CardApplicationType.Sealed ? CardStatusFlags.None //Sealedre-stacks:ToFlag(evt.Type), }, }); if (result.Rejection !=
SelectionRejection.None) {LoggingService.Append(...); return;} foreach (varcard in result.Selected)
EventManager.Publish(new CardRestrictionMutationAnimationR equested { TargetCard = card, StacksPerCard =
System.Math.Max(1, evt.StacksPerCard), Type = evt.Type }); }
```

Divergent callers become one request each: **Poison** = `{ Target=Hand, Count=ExactlyOne, Eligibility={ExcludePledged=true, RequirePositiveBlock=true, ExcludeIfAny=Poisoned } }`; **Shackle** = `{ Target=Hand, Count=Fixed, Amount=2, MinCandidates=2, Eligibility={ExcludeIfAny=Shackled|Intimidated} }`; **Intimidate** = `{Target=Hand, Amount=n, Ordering=PoisonedLast, Eligibility={ExcludePledged=true, RequirePositiveBlock=true, ExcludeIfAny=Intimidated|Shackled} }`; **Recoil** = `{ Target=Hand, Count=ExactlyOne, Eligibility={ExcludeIfAny=Recoil } }`; **MarkedForSpecificDiscard** = `{Target=Hand, Amount=n, Eligibility=new() }`.

**Hides:** the target→pile mapping (currently `CardApplicationTargetingService.ResolveCandidates:12-39`), the `Distinct().OrderBy(rng).Take(n)` mechanics, the count modes (up-to-N / exactly-one / fixed / min-candidates gate), the poisoned-last ordering, and — most importantly — the single eligibility rule. **Fixes (deliberately, guarded by new tests):** the candidate-source and pledge/weapon inconsistencies, and the unseedable RNG.

## Dependency Strategy

In-process. `CardStatusSelector` is a pure static class beside the other graphics-free selection helpers in `ECS/Services/` (next to `CardApplicationTargetingService`). It performs **no `EntityManager` queries and publishes/enqueues no events** — it operates only on the value `DeckSnapshot` and `CardSelectionRequest` handed in.
- **Snapshot gathering stays in the caller.** A small `DeckSnapshotBuilder` (or a static method on the selector's companion) does the single `EntityManager` read: locate the `Deck` (as `CardApplicationTargetingService.cs:22-24` does), and project each card into `CardFacts` — reading `CardData.Card.IsWeapon/IsToken/CardId`, `HasComponent()`, the status components, and `BlockValueService.GetTotalBlockValue(card)`. Block value must be projected here because `BlockValueService` is `internal` to `ECS.Systems` and pulls its `EntityManager` off the card (`BlockValueService.cs:26`) — the resolver never touches it.
- **No callback into systems.** The resolver replaces the *selection* half only. The service→system inversion (§3) is addressed as a companion cleanup, not by the resolver: relocate `ApplyCursedRuntime`/`RemoveCursedRuntime` and `ApplyHexRuntime`/`RemoveHexR untime` bodies (`CursedManagementSystem.cs:12 9-194`, `HexManagementSystem.cs:85-119`) so `CardApplicationService`'s dictionary (`CardApplicationService.cs:55-62`) stops calling *up* into systems. That is a mechanical move, sequenced after the selector lands.
- **Apply-timing stays put.** `BattleCardMutationDisplaySystem ` keeps the `OnSwap` closure and its immediate-apply fallback unchanged; the resolver has nothing to say about *when* the write happens.

## Testing Strategy

**New `CardStatusSelectorTests`** — bare, no `EntityManager`, no systems, no graphics. Build `DeckSnapshot` from literal `CardFacts` lists, inject a deterministic `Rng` (e.g. `_=>0` or a fixed permutation), call `Select`, assert `Selected` (which entities) + `Rejection` (why) + `EligibleCandidates` (the pre-Take set). One spec case per reconciled divergence:
- **Target → pile mapping** parity with the old `ResolveCandidates` (port the matrix from `CardApplicationManagementSystemTests.cs:17-60`, `Target_applies_only_to_cards_in_selected_zones`) — now asserted directly on the selector, not through the animation pipeline.
- **`ExactCard`** short-circuits pile resolution and still respects eligibility.
- **Canonical eligibility**: weapon excluded; token excluded (MustBeBlocked); pledged excluded when `ExcludePledged`; `RequirePositiveBlock` drops zero-block cards; `ExcludeIfAny` drops already-statused cards; `ExcludeCardIds` drops Curse/Hex ids (Hex case).
- **Not-already-applied**: Sealed re-selects an already-Sealed card (`ExcludeIfAny=None`); every other status does not (locks `CardApplicationManagementSystem.cs:35`).
- **Count modes**: `UpToAmount` returns `min(Amount, eligible)`; `ExactlyOne` returns one (Poison/Recoil); `Fixed`+`MinCandidates=2` returns 2, and returns `BelowMinCandidates` with 0 selected when only one candidate exists (locks `ShackleManagementSystem.cs:56 `).
- **Ordering**: `PoisonedLast` places non-poisoned before poisoned before `Take` (locks `IntimidateManagementSystem.cs: 82-88`); with a fixed RNG the exact order is asserted.
- **Determinism**: identical snapshot + identical `Rng` ⇒ identical `Selected` (the property `new Random()` in Shackle/Intimidate cannot satisfy today).
- **Remove selection**: `ExcludeIfAny`-inverse "must-have-status" path for `RemoveCardApplications` (pledged cards remain removable — locks the `IsNonWeaponCard` asymmetry at `CardApplicationManagementSystem.cs:84`). **Delete / replace (old pipeline-driven tests):** the selection assertions currently smuggled through the full event+animation pipeline move to the selector boundary. Concretely: `CardApplicationManagementSystemTests.Application_skips_ineligible_and_already_applied_cards` (`:290-324`) and `Target_applies_only_to_cards_in_selected_zones` (`:17-60`) → selector specs; the Cursed/Hex mutual-exclusion + Curse/Hex-id exclusion assertions in `CursedManagementSystemTests` / `HexManagementSystemTests` → selector specs; the selection portions of `PoisonedCardManagementSystemTests.cs:16-34`, `ShackleManagementSystemTests` (the `Assert.Equal(2, CountShackled(...))` count/gate assertions, `:23-52`), and `IntimidateManagementSystemTests` → selector specs with injected RNG. **Keep as thin integration guards (must pass unchanged):** one test per system proving the system still publishes `CardRestrictionMutationAnimationRequested` / adds the component for a trivial selection through `BattleMutationTestSupport` — `CardApplicationManagementSystemTests.Application_type_adds_the_corresponding_component` (`:62-90`), `Sealed_application_adds_requested_stacks_and_can_reapply` (`:92-122`), and `Generic_system_ignores_cursed_events_without_throwing` (`:326-366`) stays until the shared type-dispatch registry replaces the per-file guards. Persistence/hydration tests (`CardApplicationManagementSystemTests.cs:124-257`) are unaffected — they exercise apply, not selection.

**Test env:** none beyond the current xUnit setup (`tests/Crusaders30XX.Tests`). No `GraphicsDevice`/`SpriteBatch`, no `BattleMutationTestSupport` for the selector suite — that is the payoff. Serial execution (`[assembly: CollectionBehavior(DisableTestParallelization = true)]`) is irrelevant to `CardStatusSelectorTests` because it touches no static bus.

## Implementation Recommendations

**Owns:** target→pile resolution, the single eligibility filter, the `Distinct/OrderBy(rng)/Take` selection mechanics, count modes (up-to-N / exactly-one / fixed / min-candidates), ordering bias, and the rejection reason.

**Hides:** all of the above behind `Select(...)` + `IsEligible(...)`.

**Exposes:** two static methods plus the `DeckSnapshot`/`CardFacts`/`CardEligibility`/`CardSelectionRequest `/`CardSelectionResult` value types. **Does NOT own (stays in systems):** event subscription, `EntityManager` reads (the snapshot builder), publishing `CardRestrictionMutationAnimationRequested` / adding components, the `OnSwap` apply-timing closure, and the Cursed/Hex runtime replacement. Incremental, behavior-preserving migration: 1. Add `CardStatusSelector` + value types + `DeckSnapshotBuilder`, no wiring. Add `CardStatusSelectorTests` first, pinning the *reconciled* rules as the intended contract.
2. Repoint `CardApplicationManagementSystem` (Frozen-family), `CursedManagementSystem`, `HexManagementSystem` onto `Select` (apply + both remove paths). These three are the exact-idiom callers — a straight swap; delete their local LINQ chains. Verify against the retained integration guards.
3. Repoint `PoisonedCardManagementSystem `, `RecoilManagementSystem`, `IntimidateManagementSystem`, `ShackleManagementSystem`, `MarkedForSpecificDiscardSystem` — each becomes one `CardSelectionRequest`. This is where the correctness fix lands: choose one deliberate answer for weapon/pledge exclusion and candidate source across the hand-only statuses, encode it in the request, and lock it with the spec cases. Inject a `Random.Shared.Next` `Rng` in production (identical behavior) so Shackle/Intimidate stop using `new Random()`. 4. (Companion, optional this pass) Collapse the per-file type-dispatch (§1) into one shared registry so the negative guards disappear, and relocate the Cursed/Hex runtime out of the service dictionary (§3) so `CardApplicationService` no longer calls into systems.
5. `MustBeBlockedSystem` is the boundary case: its predicate needs EM-bound `CanPlay(em, card)` and `CardColorQualificationService.MeetsBlockingRestriction` (`MustBeBlockedSystem.cs:149-172`). Project those as booleans into `CardFacts` (e.g. `CanBlockNow`, `MeetsBlockRestriction`) so the resolver still owns count+ordering; treat as a stretch, not a blocker. **Systems that slim:** `CardApplicationManagementSystem`, `CursedManagementSystem`, `HexManagementSystem` lose their selection chains (each ~7–12 lines → one call); `PoisonedCardManagementSystem `/`ShackleManagementSystem`/`IntimidateManagementSystem`/`RecoilManagementSystem`/`MarkedForSpecificDiscardSystem` lose their ad-hoc predicates and RNG fields. `CardApplicationTargetingService.ResolveCandidates` is absorbed into the resolver's pile mapping (delete once callers migrate).

**Effort:** medium (1 file + snapshot builder added; 3 systems repointed cleanly, 5 more converted to a request; ~100 lines of duplicated LINQ deleted).

**Risk:** medium — the deliberate reconciliation of the divergent predicates is an intentional behavior change for the hand-only statuses (weapon/pledge/candidate-source), so it must ship behind the new spec tests and be spot-checked in-app; steps 1–2 (exact-idiom callers) are behavior-preserving and low risk. **The correctness fix (call-out):** today "which cards can a status hit" has ~eight subtly different answers and no test asserting any of them. Reconciling them into `CardEligibility` + `IsEligible` is the point of this RFC, not a side effect — the win is a single, tested definition of card-status eligibility plus deterministic selection.

## Critical files

- `ECS/Scenes/BattleScene/CardApplicationManagementSystem.cs` (`:17-19`, `:31-95`)
- `ECS/Scenes/BattleScene/CursedManagementSystem.cs` (`:47-113`, `:129-194`)
- `ECS/Scenes/BattleScene/HexManagementSystem.cs` (`:32-59`, `:85-119`)
- `ECS/Services/CardApplicationTargetingService.cs` (`ResolveCandidates:12`, `IsEligibleForApplication:41`, `IsNonWeaponCard:46`)
- `ECS/Services/CardApplicationService.cs` (per-type dictionary `:17-63`; service→system callbacks `:57`,`:61`)
- `ECS/Scenes/BattleScene/PoisonedCardManagementSystem.cs` (`:57-66`), `ShackleManagementSystem.cs` (`:49-62`), `IntimidateManagementSystem.cs` (`:70-88`), `RecoilManagementSystem.cs` (`:41-47`), `MarkedForSpecificDiscardSystem.cs` (`:42-46`), `MustBeBlockedSystem.cs` (`:113-178`)
- `ECS/Scenes/BattleScene/BattleCardMutationDisplaySystem.cs` (`OnSwap:155`, fallback `:139`)
- `ECS/Events/CardEvents.cs` (`ApplyCardApplicationEvent:667`, `CardRestrictionMutationAnimationRequested:677`, `RemoveCardApplication:684`, `RemoveCardApplications:690`, `CardApplicationType:697`, `CardApplicationTarget:719`)
- Tests: `tests/Crusaders30XX.Tests/CardApplicationManagementSystemTests.cs`, `CursedManagementSystemTests.cs`, `HexManagementSystemTests.cs`, `PoisonedCardManagementSystemTests.cs`, `ShackleManagementSystemTests.cs`, `IntimidateManagementSystemTests.cs`, `RecoilManagementSystemTests.cs `, `BattleMutationTestSupport.cs`

## Verification

- `dotnet build` from the repo root is clean (per `AGENTS.md:29`).
- `dotnet test tests/Crusaders30XX.Tests` is green (xUnit, serial).
- New `CardStatusSelectorTests` pass, including: the target→pile matrix, each eligibility rule, Sealed re-stacking, the three count modes + `MinCandidates` rejection, `PoisonedLast` ordering, and determinism under a fixed `Rng`.
- The retained per-system integration guards (component added / animation event published for a trivial selection) still pass through `BattleMutationTestSupport`.
- In-app (`dotnet run`, or `dotnet run --new` for a fresh save): trigger Frozen, Cursed, Poison, and Shackle applications during a block phase and confirm selection behavior is identical for the exact-idiom statuses (Frozen/Cursed) and matches the deliberately reconciled rules for the hand-only statuses (Poison picks one eligible block-bearing card; Shackle links exactly two non-intimidated cards, doing nothing with <2 candidates).

---

