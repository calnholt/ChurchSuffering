#nullable enable

using System;
using Crusaders30XX.ECS.Data.Ids;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Meta;

/// <summary>Deterministic three-encounter and five-event climb schedule characterization.</summary>
public static class ClimbScheduleRuntime
{
    public const int EncounterCount = 3;
    public const int EventCount = 5;

    private static readonly EnemyId[] EnemyPool =
    [
        EnemyId.Skeleton, EnemyId.Demon, EnemyId.Ogre, EnemyId.Mummy,
        EnemyId.Ninja, EnemyId.Spider, EnemyId.Thornreaver, EnemyId.SkeletalArcher,
    ];

    public static void Generate(
        uint seed,
        int time,
        Span<ClimbEncounterScheduleEntry> encounters,
        Span<ClimbEventScheduleEntry> events)
    {
        if (encounters.Length < EncounterCount)
            throw new ArgumentException("The encounter destination must hold three entries.", nameof(encounters));
        if (events.Length < EventCount)
            throw new ArgumentException("The event destination must hold five entries.", nameof(events));
        time = Math.Clamp(time, 0, ClimbShopRuntime.MaxTime);
        uint random = seed == 0 ? 0x9E3779B9u : seed;
        int enemyStart = (int)(Next(ref random) % (uint)EnemyPool.Length);
        int regionStart = (int)(Next(ref random) % 5u);
        for (var index = 0; index < EncounterCount; index++)
        {
            uint roll = Next(ref random);
            int timeCost = 1 + (int)(roll % 3u);
            int rewardColor = (int)((roll >> 8) % 3u);
            encounters[index] = new(
                index,
                EnemyPool[(enemyStart + index) % EnemyPool.Length],
                (ClimbEncounterRegion)((regionStart + index) % 5),
                time,
                2 + (int)((roll >> 4) % 4u),
                timeCost,
                rewardColor == 0 ? timeCost : 0,
                rewardColor == 1 ? timeCost : 0,
                rewardColor == 2 ? timeCost : 0,
                roll);
        }

        Span<int> kinds = stackalloc int[EventCount] { 0, 0, 0, 1, 1 };
        for (var index = EventCount - 1; index > 0; index--)
        {
            int swap = (int)(Next(ref random) % (uint)(index + 1));
            (kinds[index], kinds[swap]) = (kinds[swap], kinds[index]);
        }
        for (var position = 0; position < EventCount; position++)
        {
            uint roll = Next(ref random);
            (int start, int end) = GetEventAppearanceBand(position);
            int appearance = start + (int)(roll % (uint)(end - start + 1));
            bool hazard = kinds[position] == 0;
            int reward = hazard ? 1 + (int)((roll >> 12) % 2u) : 0;
            int rewardColor = (int)((roll >> 16) % 3u);
            events[position] = new(
                position,
                new(8201 + (int)((roll >> 20) % (hazard ? 4u : 2u))),
                hazard ? ClimbScheduledEventKind.Hazard : ClimbScheduledEventKind.Character,
                appearance,
                hazard ? 2 + (int)((roll >> 4) % 3u) : 3 + (int)((roll >> 4) % 3u),
                hazard ? 0 : 1,
                rewardColor == 0 ? reward : 0,
                rewardColor == 1 ? reward : 0,
                rewardColor == 2 ? reward : 0,
                -1,
                ClimbScheduledEventStatus.Scheduled,
                roll);
        }
    }

    public static (int Start, int End) GetEventAppearanceBand(int position)
    {
        if ((uint)position >= EventCount) throw new ArgumentOutOfRangeException(nameof(position));
        int start = position * ClimbShopRuntime.MaxTime / EventCount + 1;
        int end = (position + 1) * ClimbShopRuntime.MaxTime / EventCount;
        return (start, end);
    }

    public static bool UpdateEventLifecycle(Span<ClimbEventScheduleEntry> events, int time)
    {
        time = Math.Clamp(time, 0, ClimbShopRuntime.MaxTime);
        var changed = false;
        for (var index = 0; index < events.Length; index++)
        {
            ref ClimbEventScheduleEntry entry = ref events[index];
            if (time >= ClimbShopRuntime.MaxTime)
            {
                if (entry.Status is ClimbScheduledEventStatus.Pending or ClimbScheduledEventStatus.Resolved or ClimbScheduledEventStatus.Expired)
                    continue;
                entry.Status = ClimbScheduledEventStatus.Expired;
                changed = true;
                continue;
            }
            if (entry.Status == ClimbScheduledEventStatus.Scheduled && time >= entry.ScheduledAppearanceTime)
            {
                entry.Status = ClimbScheduledEventStatus.Active;
                entry.ActivatedAtTime = time;
                changed = true;
            }
            else if (entry.Status == ClimbScheduledEventStatus.Active &&
                     time >= entry.ActivatedAtTime + entry.Duration)
            {
                entry.Status = ClimbScheduledEventStatus.Expired;
                changed = true;
            }
        }
        return changed;
    }

    private static uint Next(ref uint value)
    {
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        return value;
    }
}
