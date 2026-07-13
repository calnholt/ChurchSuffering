#nullable enable

using System;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Effects;

public readonly record struct TemperanceAbilityDefinition(
    TemperanceAbilityId Id,
    int Threshold,
    EffectId Effect,
    int EffectStacks,
    CardId SpawnedCard,
    int SpawnCount,
    int DrawCount,
    byte TargetsEnemy);

/// <summary>Static replacement for the legacy TemperanceBase object hierarchy.</summary>
public static class TemperanceAbilityCatalog
{
    private static readonly TemperanceAbilityDefinition[] Definitions =
    [
        new(TemperanceAbilityId.AngelicAura, 2, RuleEffectIds.Aegis, 3, default, 0, 0, 0),
        new(TemperanceAbilityId.FlingFling, 3, default, 0, CardId.Kunai, 2, 0, 0),
        new(TemperanceAbilityId.IronResolve, 3, RuleEffectIds.Vigor, 1, default, 0, 0, 0),
        new(TemperanceAbilityId.MeasuredBreath, 3, default, 0, default, 0, 1, 0),
        new(TemperanceAbilityId.Radiance, 4, RuleEffectIds.Stun, 1, default, 0, 0, 1),
        new(TemperanceAbilityId.StaticSurge, 3, RuleEffectIds.Galvanize, 1, default, 0, 0, 0),
        new(TemperanceAbilityId.Unsheath, 3, RuleEffectIds.Sharpen, 5, default, 0, 0, 0),
    ];

    public const int Count = 7;

    public static ref readonly TemperanceAbilityDefinition GetDefinition(TemperanceAbilityId id)
    {
        int index = (int)id - 1;
        if ((uint)index >= Definitions.Length)
            throw new System.ArgumentOutOfRangeException(nameof(id));
        return ref Definitions[index];
    }
}

public static class TemperanceRuntime
{
    public static int Modify(int current, int delta) => System.Math.Max(0, checked(current + delta));

    public static int Set(int amount) => System.Math.Max(0, amount);

    public static bool TryResolve(
        ref int temperance,
        TemperanceAbilityId ability,
        EntityId owner,
        EntityId primaryEnemy,
        EntityId deck,
        RuleCommandWriter commands,
        out int drawCount)
    {
        ref readonly TemperanceAbilityDefinition definition = ref TemperanceAbilityCatalog.GetDefinition(ability);
        drawCount = 0;
        if (temperance < definition.Threshold)
            return false;

        temperance -= definition.Threshold;
        TargetHandle source = TargetHandle.ForEntity(owner);
        if (!definition.Effect.IsNull)
        {
            TargetHandle target = definition.TargetsEnemy != 0
                ? TargetHandle.ForEntity(primaryEnemy)
                : source;
            var effect = new EffectSpec(
                definition.Effect, definition.EffectStacks, 0, ConditionSpec.Always, RuleValueFlags.None);
            commands.Append(RuleCommand.ApplyEffect(source, target, in effect));
        }
        if (definition.SpawnCount > 0)
        {
            commands.Append(RuleCommand.SpawnCard(
                source, source, deck, definition.SpawnedCard, CardZone.Hand,
                RuleCardColor.White, count: definition.SpawnCount));
        }
        drawCount = definition.DrawCount;
        return true;
    }
}

public static class PassiveMechanicRules
{
    public static int ResolveBleedTriggers(int bleedStacks, ReadOnlySpan<byte> assignedBlockerCountsByColor) =>
        System.Math.Min(bleedStacks, CountAtLeastTwo(assignedBlockerCountsByColor));

    public static bool ShouldMillForBrittle(int playedBlockerCount) => playedBlockerCount == 1;

    public static int ConsumeVigor(int vigorStacks, int paidNonWeaponCards) =>
        System.Math.Min(System.Math.Max(0, vigorStacks), System.Math.Max(0, paidNonWeaponCards));

    public static int ConsumeIntimidate(int availableEligibleCards, int requested) =>
        System.Math.Min(System.Math.Max(0, availableEligibleCards), System.Math.Max(0, requested));

    public static bool TickPoison(ref int remainingMilliseconds, int elapsedMilliseconds, bool active, bool tutorialPaused)
    {
        const int period = 60_000;
        if (!active)
        {
            remainingMilliseconds = period;
            return false;
        }
        if (tutorialPaused || elapsedMilliseconds <= 0)
            return false;
        if (remainingMilliseconds <= 0)
            remainingMilliseconds = period;
        remainingMilliseconds -= elapsedMilliseconds;
        if (remainingMilliseconds > 0)
            return false;
        remainingMilliseconds += period;
        return true;
    }

    private static int CountAtLeastTwo(ReadOnlySpan<byte> counts)
    {
        int count = 0;
        for (var index = 0; index < counts.Length; index++)
        {
            if (counts[index] >= 2)
                count++;
        }
        return count;
    }
}
