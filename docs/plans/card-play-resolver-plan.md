# Card Play + Cost Resolution → `CardPlayResolver` Plan

## Document Status

- **Status:** Draft design, ready for implementation review.
- **Repository:** `Crusaders30XX`
- **Runtime:** .NET 8.0, MonoGame DesktopGL, ECS architecture.
- **RFC sequence:** #2 — land **third**, after RFC #1 (Enemy Attack Resolver).
- **Required final verification after implementation:** `dotnet build` from the repository root.
- **Save compatibility:** None required. Start fresh with `dotnet run -- new` when validating.

This plan extracts "can I play this card, and what do I pay?" into a pure static `CardPlayResolver` over a state snapshot, returning an immutable `CardPlayPlan`. Central battle interaction; behavior-preserving extraction that kills triplicated rules and a latent solver bug.

---

## 1. Objective

Single source of truth for:

- Action-phase playability gates.
- Effective-cost computation (via `VigorService`).
- Payment eligibility filtering.
- Recursive cost-solver (count + first solution).

**Chosen shape:** Pure static resolver over a state snapshot, returning immutable `CardPlayPlan`. Rejected: resolver holding `EntityManager` (re-couples tests to full-world setup); fluent builder (ceremony).

Sits beside graphics-free siblings `AlternateCardPlayService`/`VigorService`; `internal` (test assembly already sees internals).

---

## 2. Problem

"Can I play this card, and what do I pay?" is implemented three-plus times, each shallow and coupled to its host system.

### 2.1 Cost-solver buried in an event handler

`CardPlaySystem.CountDistinctSolutions` (`ECS/Scenes/BattleScene/CardPlaySystem.cs:38`) and `FindFirstSolution` (`:101`) are private recursive multiset solvers reachable only by publishing `PlayCardRequested` and observing follow-on events. No way to unit-test "these 4 cards, this cost → N solutions."

### 2.2 Playability rule duplicated verbatim

The Action-phase gate in `OnPlayCardRequested` (`:196-262`: relic → block-without-alternate → pledge → silenced → AP → `CanPlay`) is re-derived in `CanPlayCardHighlightSystem.IsPlayableInAction` (`CanPlayCardHighlightSystem.cs:147-190`) — a **graphics system** (`GraphicsDevice`/`SpriteBatch` ctor `:36`). A **third** copy is in tests: `StGeorgeMedalTests.EvaluateActionPlayability` (`:198-227`), and `Can_play_highlight_matches_alternate_play_rule` (`:181`) exists only to assert the copies agree.

### 2.3 Cost feasibility triplicated with two algorithms

- `CardPlaySystem.CanSatisfy` (greedy, `:300-331`)
- `CanPlayCardHighlightSystem.CanSatisfyCosts` (greedy, `:216-248`, comment says *"mirrors CardPlaySystem.CanSatisfy"*)
- `PayCostOverlaySystem.TryConsumeCostForCard` (incremental greedy, `:440-463`)
- Plus the recursive `CountDistinctSolutions`

### 2.4 Payment eligibility filtering triplicated

Exclude Yellow / non-`CanDiscardForCost` / pledged:

- `CardPlaySystem.cs:282-296`
- `CanPlayCardHighlightSystem:200-213`
- `PayCostOverlaySystem:918-937`

### 2.5 Latent bug

`CountDistinctSolutions` empty-cost branch returns `validCards.Count == 0 ? 1 : 0` (`:47-48`) — an empty cost should always have exactly one trivial solution; dead today only because the sole caller gates on `requiredCosts.Count > 0` (`:277`). Any future caller inherits it.

---

## 3. Proposed Interface

```csharp
public enum CardPlayMode { Normal, AlternateAttack }

public enum CardPlayRejection
{
    None, WrongPhase, IsRelic, BlockWithoutAlternate, Pledged,
    Silenced, NoActionPoints, CanPlayFalse, CostUnsatisfiable
}

public sealed class CardPlayContext
{
    public Entity Card;
    public CardBase Def;
    public SubPhase Phase;
    public int ActionPoints;
    public int VigorStacks;
    public bool HasPledge;
    public bool PledgeBlocksPlay;
    public bool IsSilenced;
    public AlternateCardPlayProfile AlternateProfile; // from AlternateCardPlayService
    public Func<EntityManager, Entity, bool> EvaluateCanPlay; // wraps Def.CanPlay(em, Card)
    public IReadOnlyList<Entity> PaymentPool; // hand minus self, UNFILTERED
}

public sealed class CardPlayPlan
{
    public bool IsPlayable;
    public CardPlayRejection Rejection;
    public IReadOnlyList<string> EffectiveCosts; // post-Vigor
    public int SolutionCount;                      // 0, 1, 2 (=many, capped)
    public IReadOnlyList<Entity> AutoPayment;      // set iff SolutionCount==1 && costs>0
    public bool RequiresOverlay;
    public bool IsFreeAction;
    public CardPlayMode Mode;
}

internal static class CardPlayResolver
{
    public static CardPlayPlan Resolve(CardPlayContext ctx);
    public static int CountCostSolutions(IReadOnlyList<string> costs, IReadOnlyList<Entity> pool, int cap = 2);
    public static IReadOnlyList<Entity> FirstCostSolution(IReadOnlyList<string> costs, IReadOnlyList<Entity> pool);
    public static List<Entity> FilterPayable(IReadOnlyList<Entity> pool); // one canonical eligibility filter
}
```

### 3.1 Caller migration

`CardPlaySystem.OnPlayCardRequested` (replaces `:196-405`; `BattleInputGate`/tutorial checks `:169-170` and all event publishing stay) becomes:

1. Build context → `Resolve`
2. If `!IsPlayable` emit `CantPlayCardMessage`/`OnCantPlay`
3. Else if `RequiresOverlay` open pay-cost overlay
4. Else auto-pay over `plan.AutoPayment`
5. `ResolveAcceptedCardPlay` switches on `plan.Mode` instead of re-reading `alternateProfile.TreatsAsAttack` (`:448`)

`CanPlayCardHighlightSystem` replaces `IsPlayableInAction`/`BuildCostEligibleCards`/`CanSatisfyCosts` (`:147-248`) with `CardPlayResolver.Resolve(ctx).IsPlayable` — same context, same rule; `cap=2` early-exit keeps per-frame cost equal to today's greedy.

### 3.2 What the resolver hides

Recursion + canonical dedup-by-sorted-entity-id (`:62`), greedy/recursive divergence (collapsed to one algorithm), Yellow/weapon/token/pledge exclusion, post-Vigor cost reduction, alternate-profile interpretation; **fixes** empty-cost semantics to "1 trivial solution".

---

## 4. Dependency Strategy

In-process, merge directly. New file `ECS/Scenes/BattleScene/CardPlayResolver.cs`.

**Graphics-free:** no `GraphicsDevice`/`SpriteBatch`/`Texture2D`/`Microsoft.Xna.Framework.Graphics`; depends only on `CardBase`/`CardData`/`Entity`/`CardColorQualificationService`/`VigorService`/`AlternateCardPlayProfile`. No `EntityManager` queries, no event publishing — the caller gathers the snapshot and owns all side effects.

---

## 5. Testing Strategy

### 5.1 New `CardPlayResolverTests`

Bare `EntityManager` + `EntityFactory.CreateCardFromDefinition`, no systems/graphics:

**Solver:** 0/1/many (capped) solutions, empty costs→1 (locks the `:47-48` fix), Yellow/weapon/token/pledged excluded, `"Any"` matching, specific-color via `CardColorQualificationService`, duplicate `["Red","Red"]`, mixed `["Red","Any"]` (guards multiset dedup).

**Playability:** relic/block-without-alternate/block-with-StGeorge (free, `AlternateAttack`)/AP-zero/free-action/`CanPlayFalse`/pledged/silenced/cost-unsatisfiable/`RequiresOverlay`/`AutoPayment`.

### 5.2 Delete/replace

- `StGeorgeMedalTests.EvaluateActionPlayability` (`:198-227`)
- Rewrite `Can_play_highlight_matches_alternate_play_rule` (`:181-196`) to assert on `Resolve(...).IsPlayable` directly

### 5.3 Keep as integration guards (must pass unchanged)

- `CardPlayUpgradeTests` (`:30-60`, overlay-vs-auto-pay)
- `StGeorgeMedalTests` play-flow (`:60-115`)

---

## 6. Implementation Steps

1. Add `CardPlayResolver.cs` + types; no wiring yet.
2. `CardPlaySystem` builds context + switches on plan; delete `CountDistinctSolutions`/`FindFirstSolution`/local `CanSatisfy` (greedy pre-check `:333` becomes redundant — `SolutionCount==0` means unsatisfiable; `CannotSatisfyCost`/`NoSolutionForCost` `:341/:357` collapse to one).
3. `CanPlayCardHighlightSystem` → `Resolve(...).IsPlayable`; leave block-phase path + rendering intact.
4. Optional: route `PayCostOverlaySystem` eligibility through `FilterPayable`.
5. Add tests; simplify `StGeorgeMedalTests`.
6. Pin recursive solver as single source of truth with tests **before** deleting greedy paths (feasibility-equivalent because every specific-color-eligible card is also `"Any"`-eligible).
7. Run `dotnet build` and `dotnet test tests/Crusaders30XX.Tests`.

**Effort:** medium (~1 file added, 2 systems slimmed, ~120 lines deleted). **Risk:** medium — central interaction, but behavior-preserving and fenced by existing integration tests.

### 6.1 Does NOT own (stays in caller)

`EntityManager` queries + context assembly; `BattleInputGate`/tutorial checks; auto-pay discard loop + `LastPaymentCache`; `card.OnPlay?.Invoke` (`:472`); all event publishing; zone-move logic; PayCostOverlay UI.

---

## 7. Critical Files

- `ECS/Scenes/BattleScene/CardPlaySystem.cs`
- `ECS/Scenes/BattleScene/CanPlayCardHighlightSystem.cs`
- `ECS/Scenes/BattleScene/AlternateCardPlayService.cs`
- `ECS/Scenes/BattleScene/VigorService.cs`
- `tests/Crusaders30XX.Tests/StGeorgeMedalTests.cs`

---

## 8. Verification Checklist

- [ ] `dotnet build` succeeds from repo root.
- [ ] New `CardPlayResolverTests` pass (esp. empty-cost = 1 and many-solutions overlay branch).
- [ ] `CardPlayUpgradeTests` / `StGeorgeMedalTests` pass unchanged.
- [ ] In-app: play card with 0/1/many payment options (auto-pay vs pay-cost overlay) and StGeorge alternate-play block card.
