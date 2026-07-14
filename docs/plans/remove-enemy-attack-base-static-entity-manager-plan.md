# Remove `EnemyAttackBase` Static `EntityManager` Plan

## Document Status

- **Status:** Draft design, ready for implementation review.
- **Repository:** `Crusaders30XX`
- **Runtime:** .NET 8.0, MonoGame DesktopGL, ECS architecture.
- **RFC sequence:** #3 — land **first** among the Deep-Module refactors (~6 lines, 4 files).
- **Required final verification after implementation:** `dotnet build` from the repository root.
- **Save compatibility:** None required. Start fresh with `dotnet run -- new` when validating.

This plan removes the process-global `EnemyAttackBase.EntityManager` static field. Attack callbacks already receive `EntityManager` as their first parameter; a handful of sites accidentally read the static instead. Deleting the field makes per-battle world isolation real and removes one of two blockers to relaxing test parallelization.

**Out of scope:** `EnemyBase.EntityManager` (per-instance field, already isolated). Forward-looking `AttackContext` struct widening is deferred to candidate RFC #6 (injectable per-battle event bus).

---

## 1. Objective

Eliminate the only shared mutable `EntityManager` slot on the enemy attack layer so that:

- Two `EntityManager` instances can coexist without racing (second battle, or parallel test cases).
- Attack callbacks use only the `entityManager` parameter they are handed.
- Stale-reference hazard from a static that outlives any battle is removed.

**Chosen shape:** Route through the existing callback parameter and delete the field. Rejected alternatives: instance-field-only (two sources of truth), `AsyncLocal` (re-hides world as spooky state).

---

## 2. Problem

`EnemyAttackBase.EntityManager` is `public static` (`ECS/Objects/Enemies/EnemyAttackBase.cs:29`) — a process-global mutable pointer to whichever battle's `EntityManager` last ran `Initialize` (`:90`), read back at `:91` (`IsLastBattleOfQuest`) and `:92` (`GetAppliedPassives`).

- **No two battles can coexist.** Every attack instance resolves the static to one shared slot; two `EntityManager`s race on `:90` and the loser reads a foreign world.
- **Stale-reference hazard.** The static outlives any battle; nothing clears it.
- **Forces serial tests** (one of two reasons for `TestAssembly.cs:3` `DisableTestParallelization`).

### 2.1 True blast radius (much smaller than assumed)

Repo-wide grep for `EnemyAttackBase.EntityManager`: zero hits (only read as the bare inherited `EntityManager` inside subclasses). Every callback delegate already takes `EntityManager` as its first parameter (`EnemyAttackBase.cs:79-86`), and every invocation site already passes the correct per-battle manager:

- `EnemyIntentPlanningSystem.cs:134`
- `AttackResolutionSystem.cs:93/101`
- `CardZoneSystem.cs:50`
- `EnemyAttackProgressManagementSystem.cs:169`
- `EnemyAttackBase.cs:94`

Only **4 callback bodies in 3 files** read the static instead of the parameter — all in `OnAttackReveal`, all calling `PlayerHandColorService.GetRandomCardColorInPlayerHand(EntityManager)`:

- `ECS/Objects/Enemies/CinderboltDemon.cs:47` (Cinderbolt), `:78` (InsidiousBolt)
- `ECS/Objects/Enemies/EarthDemon.cs:70` (StoneBarrage)
- `ECS/Objects/Enemies/Thornreaver.cs:43` (SawtoothRend)

Each class's own `OnBlockProcessed` right below correctly uses its `entityManager` parameter — the static read is accidental capture of the inherited field. Plus `Initialize` reads the field twice (`:91`, `:92`), holding the same value as its `entityManager` parameter.

**True radius = 1 field + 6 read sites across 4 files.**

---

## 3. Proposed Interface

Delegate signatures stay identical; the static field is deleted; the 4 stragglers + `Initialize` use the `EntityManager` parameter they already receive. Zero authoring-ergonomics change.

```csharp
// EnemyAttackBase.cs:29 — static field DELETED

public void Initialize(EntityManager entityManager)
{
    IsOneBattleOrLastBattle = GetComponentHelper.IsLastBattleOfQuest(entityManager); // was: EntityManager
    GetComponentHelper.GetAppliedPassives(entityManager, "Enemy").Passives
        .TryGetValue(AppliedPassiveType.Channel, out int count); // was: EntityManager
    Channel = count;
    OnChannelApplied?.Invoke(entityManager);
}
```

Before/after — `Cinderbolt.OnAttackReveal` (`CinderboltDemon.cs:45-51`):

```csharp
// BEFORE — ignores its parameter, reads the global static
OnAttackReveal = (entityManager) => {
    Color = PlayerHandColorService.GetRandomCardColorInPlayerHand(EntityManager);
    ...
};

// AFTER — uses the per-battle EntityManager it was handed
OnAttackReveal = (entityManager) => {
    Color = PlayerHandColorService.GetRandomCardColorInPlayerHand(entityManager);
    ...
};
```

Hides nothing new — it *removes* hidden global state; the battle boundary becomes the only source of the world reference.

---

## 4. Dependency Strategy

In-process. Per-battle isolation is achieved by eliminating the only shared mutable slot on the attack layer: after this change an attack instance holds no `EntityManager` at all — it receives the battle's manager per-call from systems that each own their own manager.

Land ahead of RFC #1 (Enemy Attack Resolver); orthogonal to `AttackResolutionSystem.cs`.

---

## 5. Testing Strategy

### 5.1 New tests

Two coexisting `EntityManager`s with independently-behaving `Cinderbolt`/`StoneBarrage`/`SawtoothRend` reveals (impossible today). Targeted regression: two `Cinderbolt` instances bound to two managers whose player hands differ in color; assert each `Color`/`Text` reflects its own manager.

### 5.2 Existing tests

`EnemyAttackFlowTests` and `FallenShepherdAttackTests` need no change (they build fresh managers and don't touch the static).

### 5.3 `DisableTestParallelization`

Cannot be relaxed yet — `EventManager`/`EventQueue` are still static (candidate RFC #6). This RFC removes *one of two* blockers; keep the attribute, note the progress.

---

## 6. Implementation Steps

1. Delete `EnemyAttackBase.cs:29` (static field).
2. Repoint `Initialize` lines `:91/:92` to use the `entityManager` parameter.
3. Change the sole argument `EntityManager` → `entityManager` in the 4 `OnAttackReveal` bodies across `CinderboltDemon.cs`, `EarthDemon.cs`, `Thornreaver.cs`.
4. Grep-guard: `EnemyAttackBase\.EntityManager` and bare-`EntityManager` reads inside subclasses must both return zero; `EnemyBase.EntityManager` untouched.
5. Add the two-battle color-isolation test.
6. Run `dotnet build` and `dotnet test tests/Crusaders30XX.Tests`.

**Effort:** trivial. **Risk:** low, behavior-preserving within a single battle (`Initialize` set the static to the same manager later passed to callbacks).

---

## 7. Critical Files

- `ECS/Objects/Enemies/EnemyAttackBase.cs`
- `ECS/Objects/Enemies/CinderboltDemon.cs`
- `ECS/Objects/Enemies/EarthDemon.cs`
- `ECS/Objects/Enemies/Thornreaver.cs`
- `ECS/Scenes/BattleScene/EnemyIntentPlanningSystem.cs` (reference only — already passes correct manager)

---

## 8. Verification Checklist

- [ ] `dotnet build` succeeds from repo root.
- [ ] `dotnet test tests/Crusaders30XX.Tests` green.
- [ ] New two-battle color-isolation test passes (would have been nondeterministic before).
- [ ] Grep-guard returns zero bare-`EntityManager` reads in `EnemyAttackBase` subclasses.
