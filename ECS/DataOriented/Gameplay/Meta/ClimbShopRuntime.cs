#nullable enable

using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Gameplay.Effects;
using Crusaders30XX.ECS.DataOriented.Rules;
using Crusaders30XX.ECS.DataOriented.Storage;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Meta;

public readonly record struct ClimbShopRefreshResult(
    EntityId Slot0,
    EntityId Slot1,
    EntityId Slot2,
    EntityId Slot3,
    EntityId Slot4)
{
    public const int SlotCount = 5;

    public EntityId this[int index] => index switch
    {
        0 => Slot0,
        1 => Slot1,
        2 => Slot2,
        3 => Slot3,
        4 => Slot4,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
}

/// <summary>Deterministic live climb-shop economy and inventory mutation.</summary>
public static class ClimbShopRuntime
{
    public const int MaxTime = 32;
    public const int RefreshInterval = 8;

    private static readonly MedalId[] MedalPool = Enum.GetValues<MedalId>();
    private static readonly EquipmentId[] EquipmentPool = Enum.GetValues<EquipmentId>();
    private static readonly CardId[] ReplacementPool =
    [
        CardId.Strike, CardId.Fervor, CardId.Mantlet, CardId.Reckoning,
        CardId.LitanyOfWrath, CardId.Absolution, CardId.Courageous,
    ];

    public static ClimbShopRefreshResult Refresh(World world, EntityId run, EntityId climb)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.IsAlive(run)) throw new ArgumentException("The shop run owner is not alive.", nameof(run));
        if (!world.TryGet(climb, out ClimbColumnTransitionState state))
            throw new ArgumentException("The shop climb root has no climb state.", nameof(climb));

        DestroyExistingSlots(world, climb);
        DynamicBuffer<ShownShopItemEntry> shown = world.GetDynamicBuffer(state.ShownShopItems);
        uint random = Mix(state.Seed, (uint)state.Time, 0x51A0B11u);

        MedalId firstMedal = SelectMedal(world, shown, ref random, excluded: null);
        MedalId secondMedal = SelectMedal(world, shown, ref random, firstMedal);
        EquipmentId equipment = SelectEquipment(world, shown, ref random);
        EntityId upgradeTarget = SelectUpgradeTarget(world, ref random);
        EntityId replacementTarget = SelectReplacementTarget(world, ref random);
        CardId replacement = SelectReplacement(world, replacementTarget, ref random);

        EntityId slot0 = CreateSlot(world, climb, 0, ClimbShopOfferKind.Medal, ref random, medal: firstMedal);
        EntityId slot1 = CreateSlot(world, climb, 1, ClimbShopOfferKind.Medal, ref random, medal: secondMedal);
        EntityId slot2 = CreateSlot(world, climb, 2, ClimbShopOfferKind.Equipment, ref random, equipment: equipment);
        EntityId slot3 = upgradeTarget.IsNull
            ? CreateSlot(world, climb, 3, ClimbShopOfferKind.Empty, ref random)
            : CreateSlot(world, climb, 3, ClimbShopOfferKind.Upgrade, ref random, target: upgradeTarget,
                card: world.Get<RunDeckCard>(upgradeTarget).Definition);
        EntityId slot4 = replacementTarget.IsNull
            ? CreateSlot(world, climb, 4, ClimbShopOfferKind.Empty, ref random)
            : CreateSlot(world, climb, 4, ClimbShopOfferKind.Replacement, ref random, target: replacementTarget, card: replacement);

        AddShown(shown, ClimbShopOfferKind.Medal, (ushort)firstMedal);
        AddShown(shown, ClimbShopOfferKind.Medal, (ushort)secondMedal);
        AddShown(shown, ClimbShopOfferKind.Equipment, (ushort)equipment);
        return new(slot0, slot1, slot2, slot3, slot4);
    }

    public static ClimbShopRefreshResult Restore(
        World world,
        EntityId climb,
        ReadOnlySpan<ClimbShopOfferSaveDto> offers)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.Has<ClimbColumnTransitionState>(climb))
            throw new ArgumentException("The shop climb root has no climb state.", nameof(climb));
        if (offers.Length != ClimbShopRefreshResult.SlotCount)
            throw new ArgumentException("A persisted shop must contain exactly five ordered offers.", nameof(offers));

        DestroyExistingSlots(world, climb);
        Span<EntityId> slots = stackalloc EntityId[ClimbShopRefreshResult.SlotCount];
        Span<byte> seen = stackalloc byte[ClimbShopRefreshResult.SlotCount];
        for (var index = 0; index < offers.Length; index++)
        {
            ref readonly ClimbShopOfferSaveDto saved = ref offers[index];
            if ((uint)saved.SlotIndex >= ClimbShopRefreshResult.SlotCount || seen[saved.SlotIndex] != 0)
                throw new ArgumentException("Persisted shop slot indices must be unique values from zero through four.", nameof(offers));
            seen[saved.SlotIndex] = 1;
            EntityId target = FindCardByOrder(world, saved.TargetOrder);
            var action = new ClimbShopSlotAction
            {
                Run = climb,
                TargetCard = target,
                Card = saved.Card,
                Equipment = saved.Equipment,
                Medal = saved.Medal,
                SlotIndex = saved.SlotIndex,
                TargetOrder = saved.TargetOrder,
                RedCost = saved.RedCost,
                WhiteCost = saved.WhiteCost,
                BlackCost = saved.BlackCost,
                TimeCost = saved.TimeCost,
                Kind = saved.Kind,
                Purchased = saved.Purchased ? (byte)1 : (byte)0,
            };
            var bundle = new SpawnBundle(2);
            bundle.Add(in action);
            var source = new ClimbShopTooltipSource
            {
                SlotIndex = saved.SlotIndex,
                Price = saved.RedCost + saved.WhiteCost + saved.BlackCost,
                Sold = action.Purchased,
            };
            bundle.Add(in source);
            slots[saved.SlotIndex] = world.Create(in bundle);
        }
        return new(slots[0], slots[1], slots[2], slots[3], slots[4]);
    }

    public static bool TryPurchase(World world, EntityId slot, EntityId buyer)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.TryGet(slot, out ClimbShopSlotAction action) || action.Purchased != 0 ||
            action.Kind == ClimbShopOfferKind.Empty) return false;
        if (!world.TryGet(action.Run, out ClimbColumnTransitionState economy)) return false;
        if (economy.Time >= MaxTime || !CanAfford(in economy, in action)) return false;
        if (!CanApply(world, in action, buyer)) return false;

        int previousTime = economy.Time;
        Apply(world, in action, buyer);
        economy.Red -= action.RedCost;
        economy.White -= action.WhiteCost;
        economy.Black -= action.BlackCost;
        economy.Time = Math.Min(MaxTime, economy.Time + action.TimeCost);
        world.Set(action.Run, in economy);
        ClimbScheduleRuntime.UpdateEventLifecycle(
            world.GetDynamicBuffer(economy.Events).AsSpan(),
            economy.Time);
        action.Purchased = 1;
        world.Set(slot, in action);
        if (world.TryGet(slot, out ClimbShopTooltipSource tooltip))
        {
            tooltip.Sold = 1;
            world.Set(slot, in tooltip);
        }

        if (CrossedRefreshBoundary(previousTime, economy.Time) && economy.Time < MaxTime)
            Refresh(world, buyer, action.Run);
        return true;
    }

    public static bool CrossedRefreshBoundary(int previousTime, int currentTime)
    {
        previousTime = Math.Clamp(previousTime, 0, MaxTime);
        currentTime = Math.Clamp(currentTime, 0, MaxTime);
        if (currentTime <= previousTime) return false;
        int next = (previousTime / RefreshInterval + 1) * RefreshInterval;
        return next < MaxTime && currentTime >= next;
    }

    private static bool CanAfford(in ClimbColumnTransitionState economy, in ClimbShopSlotAction action) =>
        economy.Red >= action.RedCost && economy.White >= action.WhiteCost && economy.Black >= action.BlackCost;

    private static bool CanApply(World world, in ClimbShopSlotAction action, EntityId buyer) => action.Kind switch
    {
        ClimbShopOfferKind.Medal or ClimbShopOfferKind.Equipment => world.IsAlive(buyer),
        ClimbShopOfferKind.Upgrade => world.TryGet(action.TargetCard, out RunDeckCard upgrade) && upgrade.Upgraded == 0,
        ClimbShopOfferKind.Replacement => world.Has<RunDeckCard>(action.TargetCard),
        _ => false,
    };

    private static void Apply(World world, in ClimbShopSlotAction action, EntityId buyer)
    {
        switch (action.Kind)
        {
            case ClimbShopOfferKind.Medal:
            {
                var bundle = new SpawnBundle(1);
                var medal = new EquippedMedal
                {
                    Owner = buyer,
                    Definition = action.Medal,
                    Active = 1,
                    Random = RuleRandomState.FromSeed((ulong)(action.SlotIndex + 1)),
                };
                bundle.Add(in medal);
                world.Create(in bundle);
                break;
            }
            case ClimbShopOfferKind.Equipment:
            {
                var bundle = new SpawnBundle(1);
                var equipment = new EquippedEquipment
                {
                    Owner = buyer,
                    Definition = action.Equipment,
                    Active = 1,
                    Random = RuleRandomState.FromSeed((ulong)(action.SlotIndex + 11)),
                };
                bundle.Add(in equipment);
                world.Create(in bundle);
                break;
            }
            case ClimbShopOfferKind.Upgrade:
            {
                RunDeckCard card = world.Get<RunDeckCard>(action.TargetCard);
                card.Upgraded = 1;
                world.Set(action.TargetCard, in card);
                break;
            }
            case ClimbShopOfferKind.Replacement:
            {
                RunDeckCard old = world.Get<RunDeckCard>(action.TargetCard);
                world.Destroy(action.TargetCard);
                var bundle = new SpawnBundle(1);
                var replacement = new RunDeckCard { Definition = action.Card, Order = old.Order, Upgraded = 0 };
                bundle.Add(in replacement);
                world.Create(in bundle);
                break;
            }
        }
    }

    private static EntityId CreateSlot(
        World world,
        EntityId climb,
        int index,
        ClimbShopOfferKind kind,
        ref uint random,
        EntityId target = default,
        CardId card = default,
        EquipmentId equipment = default,
        MedalId medal = default)
    {
        random = Next(random);
        int timeCost = kind == ClimbShopOfferKind.Empty ? 0 : (int)(random % 3u);
        int amount = kind == ClimbShopOfferKind.Empty ? 0 : 1 + timeCost;
        var action = new ClimbShopSlotAction
        {
            Run = climb,
            TargetCard = target,
            Card = card,
            Equipment = equipment,
            Medal = medal,
            SlotIndex = index,
            TargetOrder = target.IsNull ? -1 : world.Get<RunDeckCard>(target).Order,
            RedCost = kind is ClimbShopOfferKind.Upgrade or ClimbShopOfferKind.Replacement ? amount : 0,
            WhiteCost = kind == ClimbShopOfferKind.Equipment ? amount : 0,
            BlackCost = kind == ClimbShopOfferKind.Medal ? amount : 0,
            TimeCost = timeCost,
            Kind = kind,
        };
        var bundle = new SpawnBundle(2);
        bundle.Add(in action);
        var source = new ClimbShopTooltipSource { SlotIndex = index, Price = amount, Sold = 0 };
        bundle.Add(in source);
        return world.Create(in bundle);
    }

    private static MedalId SelectMedal(
        World world,
        DynamicBuffer<ShownShopItemEntry> shown,
        ref uint random,
        MedalId? excluded)
    {
        for (var offset = 0; offset < MedalPool.Length; offset++)
        {
            random = Next(random);
            MedalId candidate = MedalPool[(int)(random % (uint)MedalPool.Length + (uint)offset) % MedalPool.Length];
            if (excluded.HasValue && candidate == excluded.Value) continue;
            if (WasShown(shown, ClimbShopOfferKind.Medal, (ushort)candidate) || HasMedal(world, candidate)) continue;
            return candidate;
        }
        return FirstAvailableMedal(world, shown, excluded);
    }

    private static MedalId FirstAvailableMedal(World world, DynamicBuffer<ShownShopItemEntry> shown, MedalId? excluded)
    {
        foreach (MedalId candidate in MedalPool)
            if ((!excluded.HasValue || candidate != excluded.Value) &&
                !WasShown(shown, ClimbShopOfferKind.Medal, (ushort)candidate) && !HasMedal(world, candidate)) return candidate;
        return excluded.HasValue ? excluded.Value : MedalPool[0];
    }

    private static EquipmentId SelectEquipment(World world, DynamicBuffer<ShownShopItemEntry> shown, ref uint random)
    {
        for (var offset = 0; offset < EquipmentPool.Length; offset++)
        {
            random = Next(random);
            EquipmentId candidate = EquipmentPool[(int)(random % (uint)EquipmentPool.Length + (uint)offset) % EquipmentPool.Length];
            if (!WasShown(shown, ClimbShopOfferKind.Equipment, (ushort)candidate) && !HasEquipment(world, candidate)) return candidate;
        }
        foreach (EquipmentId candidate in EquipmentPool)
            if (!WasShown(shown, ClimbShopOfferKind.Equipment, (ushort)candidate) && !HasEquipment(world, candidate)) return candidate;
        return EquipmentPool[0];
    }

    private static EntityId SelectUpgradeTarget(World world, ref uint random)
    {
        var candidates = new List<EntityId>(16);
        foreach (QueryChunk<RunDeckCard> chunk in world.Query<RunDeckCard>())
        foreach (int row in chunk.Rows)
            if (chunk.Component1[row].Upgraded == 0 && !IsWeapon(chunk.Component1[row].Definition)) candidates.Add(chunk.Entities[row]);
        if (candidates.Count == 0) return EntityId.Null;
        random = Next(random);
        return candidates[(int)(random % (uint)candidates.Count)];
    }

    private static EntityId FindCardByOrder(World world, int order)
    {
        if (order < 0) return EntityId.Null;
        foreach (QueryChunk<RunDeckCard> chunk in world.Query<RunDeckCard>())
        foreach (int row in chunk.Rows)
            if (chunk.Component1[row].Order == order) return chunk.Entities[row];
        return EntityId.Null;
    }

    private static EntityId SelectReplacementTarget(World world, ref uint random)
    {
        var candidates = new List<EntityId>(16);
        foreach (QueryChunk<RunDeckCard> chunk in world.Query<RunDeckCard>())
        foreach (int row in chunk.Rows)
            if (!IsWeapon(chunk.Component1[row].Definition)) candidates.Add(chunk.Entities[row]);
        if (candidates.Count == 0) return EntityId.Null;
        random = Next(random);
        return candidates[(int)(random % (uint)candidates.Count)];
    }

    private static CardId SelectReplacement(World world, EntityId target, ref uint random)
    {
        CardId current = target.IsNull ? default : world.Get<RunDeckCard>(target).Definition;
        random = Next(random);
        for (var offset = 0; offset < ReplacementPool.Length; offset++)
        {
            CardId candidate = ReplacementPool[((int)(random % (uint)ReplacementPool.Length) + offset) % ReplacementPool.Length];
            if (candidate != current) return candidate;
        }
        return ReplacementPool[0];
    }

    private static bool HasMedal(World world, MedalId definition)
    {
        foreach (QueryChunk<EquippedMedal> chunk in world.Query<EquippedMedal>())
        foreach (int row in chunk.Rows)
            if (chunk.Component1[row].Definition == definition) return true;
        return false;
    }

    private static bool HasEquipment(World world, EquipmentId definition)
    {
        foreach (QueryChunk<EquippedEquipment> chunk in world.Query<EquippedEquipment>())
        foreach (int row in chunk.Rows)
            if (chunk.Component1[row].Definition == definition) return true;
        return false;
    }

    private static bool WasShown(DynamicBuffer<ShownShopItemEntry> shown, ClimbShopOfferKind kind, ushort definition)
    {
        for (var index = 0; index < shown.Count; index++)
            if (shown[index].Kind == kind && shown[index].Definition == definition) return true;
        return false;
    }

    private static void AddShown(DynamicBuffer<ShownShopItemEntry> shown, ClimbShopOfferKind kind, ushort definition)
    {
        if (!WasShown(shown, kind, definition)) shown.Add(new(kind, definition));
    }

    private static void DestroyExistingSlots(World world, EntityId climb)
    {
        var existing = new List<EntityId>(ClimbShopRefreshResult.SlotCount);
        foreach (QueryChunk<ClimbShopSlotAction> chunk in world.Query<ClimbShopSlotAction>())
        foreach (int row in chunk.Rows)
            if (chunk.Component1[row].Run == climb) existing.Add(chunk.Entities[row]);
        foreach (EntityId entity in existing) world.Destroy(entity);
    }

    private static bool IsWeapon(CardId card) => card is CardId.Sword or CardId.Hammer or CardId.Dagger;

    private static uint Mix(uint seed, uint time, uint salt)
    {
        uint value = seed == 0 ? 0x9E3779B9u : seed;
        value ^= time * 0x85EBCA6Bu;
        value ^= salt;
        return Next(value);
    }

    private static uint Next(uint value)
    {
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        return value;
    }
}
