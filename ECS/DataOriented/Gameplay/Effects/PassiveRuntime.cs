#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Storage;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Effects;

/// <summary>Sorted, allocation-free operations over an actor's applied-passive buffer.</summary>
public static class PassiveRuntime
{
    public static int GetStacks(DynamicBuffer<PassiveEntry> passives, EffectId effect)
    {
        int index = Find(passives.AsReadOnlySpan(), effect);
        return index >= 0 ? passives[index].Stacks : 0;
    }

    public static int Apply(
        DynamicBuffer<PassiveEntry> passives,
        EntityId source,
        EffectId effect,
        int delta,
        PassiveLifetime lifetime,
        int battleEpoch,
        int phaseEpoch)
    {
        if (effect.IsNull || delta == 0)
            return GetStacks(passives, effect);

        int index = Find(passives.AsReadOnlySpan(), effect);
        if (index >= 0)
        {
            ref PassiveEntry existing = ref passives[index];
            existing.Stacks += delta;
            existing.Source = source;
            existing.Lifetime = Longer(existing.Lifetime, lifetime);
            existing.BattleEpoch = battleEpoch;
            existing.PhaseEpoch = phaseEpoch;
            if (existing.Stacks <= 0)
            {
                passives.RemoveAt(index);
                return 0;
            }
            return existing.Stacks;
        }

        if (delta < 0)
            return 0;

        int insertion = ~index;
        var entry = new PassiveEntry
        {
            Effect = effect,
            Source = source,
            Stacks = delta,
            Lifetime = lifetime,
            BattleEpoch = battleEpoch,
            PhaseEpoch = phaseEpoch,
        };
        passives.Insert(insertion, in entry);
        return delta;
    }

    public static int Set(
        DynamicBuffer<PassiveEntry> passives,
        EntityId source,
        EffectId effect,
        int stacks,
        PassiveLifetime lifetime,
        int battleEpoch,
        int phaseEpoch)
    {
        int current = GetStacks(passives, effect);
        return Apply(passives, source, effect, stacks - current, lifetime, battleEpoch, phaseEpoch);
    }

    public static int Remove(DynamicBuffer<PassiveEntry> passives, EffectId effect, int stacks = int.MaxValue)
    {
        int index = Find(passives.AsReadOnlySpan(), effect);
        if (index < 0 || stacks <= 0)
            return 0;

        ref PassiveEntry existing = ref passives[index];
        if (stacks >= existing.Stacks)
        {
            passives.RemoveAt(index);
            return 0;
        }

        existing.Stacks -= stacks;
        return existing.Stacks;
    }

    public static int ResetPhase(DynamicBuffer<PassiveEntry> passives, int phaseEpoch) =>
        RemoveExpired(passives, PassiveLifetime.Phase, phaseEpoch, isBattleReset: false);

    public static int ResetBattle(DynamicBuffer<PassiveEntry> passives, int battleEpoch) =>
        RemoveExpired(passives, PassiveLifetime.Battle, battleEpoch, isBattleReset: true);

    public static int RemoveThroughLifetime(DynamicBuffer<PassiveEntry> passives, PassiveLifetime maximum)
    {
        int removed = 0;
        for (int index = passives.Count - 1; index >= 0; index--)
        {
            if (passives[index].Lifetime <= maximum)
            {
                passives.RemoveAt(index);
                removed++;
            }
        }
        return removed;
    }

    public static PassiveLifetime DefaultLifetime(EffectId effect) =>
        effect == RuleEffectIds.Scar || effect == RuleEffectIds.Cursed
            ? PassiveLifetime.Quest
            : effect == RuleEffectIds.Intellect
                ? PassiveLifetime.Climb
                : PassiveLifetime.Battle;

    private static int RemoveExpired(
        DynamicBuffer<PassiveEntry> passives,
        PassiveLifetime lifetime,
        int epoch,
        bool isBattleReset)
    {
        int removed = 0;
        for (int index = passives.Count - 1; index >= 0; index--)
        {
            ref PassiveEntry entry = ref passives[index];
            int entryEpoch = isBattleReset ? entry.BattleEpoch : entry.PhaseEpoch;
            if (entry.Lifetime == lifetime && entryEpoch != epoch)
            {
                passives.RemoveAt(index);
                removed++;
            }
        }
        return removed;
    }

    private static int Find(ReadOnlySpan<PassiveEntry> passives, EffectId effect)
    {
        int low = 0;
        int high = passives.Length - 1;
        while (low <= high)
        {
            int middle = low + ((high - low) >> 1);
            int comparison = passives[middle].Effect.Value.CompareTo(effect.Value);
            if (comparison == 0)
                return middle;
            if (comparison < 0)
                low = middle + 1;
            else
                high = middle - 1;
        }
        return ~low;
    }

    private static PassiveLifetime Longer(PassiveLifetime left, PassiveLifetime right) =>
        left >= right ? left : right;
}

public static class EffectTrackingRuntime
{
    public static bool TryMark(
        DynamicBuffer<EffectTrackingEntry> tracking,
        EntityId source,
        TriggerId trigger,
        EffectTrackingLifetime lifetime,
        int epoch)
    {
        for (var index = 0; index < tracking.Count; index++)
        {
            ref EffectTrackingEntry entry = ref tracking[index];
            if (entry.Source != source || entry.Trigger != trigger || entry.Lifetime != lifetime)
                continue;
            if (entry.Epoch == epoch)
                return false;
            entry = entry with { Epoch = epoch };
            return true;
        }

        var created = new EffectTrackingEntry(source, trigger, epoch, lifetime);
        tracking.Add(in created);
        return true;
    }

    public static int PurgeBefore(DynamicBuffer<EffectTrackingEntry> tracking, EffectTrackingLifetime lifetime, int epoch)
    {
        int removed = 0;
        for (int index = tracking.Count - 1; index >= 0; index--)
        {
            EffectTrackingEntry entry = tracking[index];
            if (entry.Lifetime == lifetime && entry.Epoch != epoch)
            {
                tracking.RemoveAt(index);
                removed++;
            }
        }
        return removed;
    }
}
