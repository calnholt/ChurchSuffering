#nullable enable

using System;
using System.Runtime.InteropServices;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;

namespace Crusaders30XX.ECS.DataOriented.Rules;

public enum ProviderLifetime : byte
{
    WhileEquipped = 0,
    Battle = 1,
    Quest = 2,
    Climb = 3,
}

public enum ProviderResolutionPolicy : byte
{
    FirstApplicableByStableEntityId = 0,
    AccumulateByStableEntityId = 1,
}

public static class EquipmentMedalProviderPolicies
{
    public const ProviderResolutionPolicy AlternatePlay = ProviderResolutionPolicy.FirstApplicableByStableEntityId;
    public const ProviderResolutionPolicy Replacement = ProviderResolutionPolicy.FirstApplicableByStableEntityId;
    public const ProviderResolutionPolicy CardStatModifiers = ProviderResolutionPolicy.AccumulateByStableEntityId;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ProviderSource(
    EntityId Entity,
    EntityId EquippedOwner,
    MedalId Definition,
    ProviderLifetime Lifetime,
    byte IsActive)
{
    public bool Active => IsActive != 0;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ProviderCandidateResult(
    ProviderSource Source,
    byte Applies)
{
    public bool IsApplicable => Applies != 0;
}

public static class ProviderPrecedence
{
    public static bool TrySelectFirst(
        ReadOnlySpan<ProviderCandidateResult> candidates,
        EntityId queryOwner,
        out ProviderCandidateResult selected)
    {
        selected = default;
        var found = false;
        for (var index = 0; index < candidates.Length; index++)
        {
            ProviderCandidateResult candidate = candidates[index];
            if (!IsEligible(in candidate, queryOwner))
                continue;
            if (!found || Compare(candidate.Source.Entity, selected.Source.Entity) < 0)
            {
                selected = candidate;
                found = true;
            }
        }
        return found;
    }

    public static int CollectApplicableInOrder(
        ReadOnlySpan<ProviderCandidateResult> candidates,
        EntityId queryOwner,
        Span<ProviderCandidateResult> destination)
    {
        var count = 0;
        for (var index = 0; index < candidates.Length; index++)
        {
            ProviderCandidateResult candidate = candidates[index];
            if (!IsEligible(in candidate, queryOwner))
                continue;
            if (count >= destination.Length)
                return -1;

            var insertion = count;
            while (insertion > 0 &&
                   Compare(candidate.Source.Entity, destination[insertion - 1].Source.Entity) < 0)
            {
                destination[insertion] = destination[insertion - 1];
                insertion--;
            }
            destination[insertion] = candidate;
            count++;
        }
        return count;
    }

    private static bool IsEligible(in ProviderCandidateResult candidate, EntityId queryOwner) =>
        candidate.IsApplicable && candidate.Source.Active && candidate.Source.EquippedOwner == queryOwner;

    private static int Compare(EntityId left, EntityId right)
    {
        int byIndex = left.Index.CompareTo(right.Index);
        return byIndex != 0 ? byIndex : left.Generation.CompareTo(right.Generation);
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct AlternatePlayQuery(
    EntityId Owner,
    EntityId Card,
    CardId Definition,
    RulePhase Phase,
    RuleCardTraits Traits);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct AlternatePlayResult(
    ProviderSource Source,
    byte Applies,
    byte AllowsPlay,
    byte IsFreeAction,
    byte TreatsAsAttack,
    int AttackDamage)
{
    public bool IsApplicable => Applies != 0;
    public bool Allowed => AllowsPlay != 0;
    public bool FreeAction => IsFreeAction != 0;
    public bool AsAttack => TreatsAsAttack != 0;
}

public enum CardStatKind : byte
{
    Damage = 0,
    Block = 1,
    OutgoingAttackDamage = 2,
}

public enum CardStatQueryMode : byte
{
    Preview = 0,
    Resolution = 1,
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CardStatQuery(
    EntityId Owner,
    EntityId Card,
    EntityId Source,
    EntityId Target,
    CardId Definition,
    CardStatKind Kind,
    CardStatQueryMode Mode,
    RuleCardTraits Traits,
    int BaseValue,
    int PaymentCardCount);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CardStatModifierResult(
    ProviderSource Source,
    StatId Stat,
    int Delta,
    byte Applies)
{
    public bool IsApplicable => Applies != 0;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ReplacementQuery(
    RuleReplacementKind Kind,
    EntityId OriginalSource,
    EntityId OriginalTarget,
    EntityId PrimaryEnemy,
    EffectId Effect,
    RuleDamageKind DamageKind,
    int OriginalDelta,
    byte PrimaryEnemyHasRequiredState)
{
    public bool HasEligiblePrimaryEnemy => PrimaryEnemyHasRequiredState != 0 && !PrimaryEnemy.IsNull;
}

public enum ReplacementActionKind : byte
{
    None = 0,
    ModifyHp = 1,
    ModifyStat = 2,
    ApplyEffect = 3,
    RemoveEffect = 4,
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ReplacementAction(
    ReplacementActionKind Kind,
    EntityId Source,
    EntityId Target,
    StatId Stat,
    EffectId Effect,
    RuleDamageKind DamageKind,
    int Amount,
    RuleValueFlags Flags);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ReplacementPlan(
    byte Handled,
    ProviderSource HandlingProvider,
    int ActionCount)
{
    public bool IsHandled => Handled != 0;
}

public ref struct ReplacementPlanWriter
{
    private readonly Span<ReplacementAction> actions;
    private ProviderSource handlingProvider;
    private int actionCount;
    private byte handled;

    public ReplacementPlanWriter(Span<ReplacementAction> actions)
    {
        this.actions = actions;
        handlingProvider = default;
        actionCount = 0;
        handled = 0;
    }

    public readonly int Capacity => actions.Length;
    public readonly int Count => actionCount;
    public readonly bool IsHandled => handled != 0;
    public readonly ReadOnlySpan<ReplacementAction> Actions => actions[..actionCount];

    public void MarkHandled(ProviderSource provider)
    {
        handlingProvider = provider;
        handled = 1;
    }

    public bool TryAppend(in ReplacementAction action)
    {
        if (!IsHandled || action.Kind == ReplacementActionKind.None || actionCount >= actions.Length)
            return false;
        actions[actionCount++] = action;
        return true;
    }

    public readonly ReplacementPlan BuildPlan() => new(handled, handlingProvider, actionCount);
}
