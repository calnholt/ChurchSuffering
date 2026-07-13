#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Effects;
using Crusaders30XX.ECS.DataOriented.Gameplay.Meta;
using Crusaders30XX.ECS.DataOriented.Generated;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Gameplay.Meta;

public sealed class Ecs052ClimbShopParityTests
{
    [Fact]
    public void Encounter_and_event_schedules_are_repeatable_diverse_and_match_exact_bands()
    {
        var firstEncounters = new ClimbEncounterScheduleEntry[3];
        var secondEncounters = new ClimbEncounterScheduleEntry[3];
        var firstEvents = new ClimbEventScheduleEntry[5];
        var secondEvents = new ClimbEventScheduleEntry[5];

        ClimbScheduleRuntime.Generate(123, 0, firstEncounters, firstEvents);
        ClimbScheduleRuntime.Generate(123, 0, secondEncounters, secondEvents);

        Assert.Equal(firstEncounters, secondEncounters);
        Assert.Equal(firstEvents, secondEvents);
        Assert.Equal(3, firstEncounters.Select(value => value.Enemy).Distinct().Count());
        Assert.Equal(3, firstEncounters.Select(value => value.Region).Distinct().Count());
        Assert.All(firstEncounters, value =>
        {
            Assert.InRange(value.Duration, 2, 5);
            Assert.InRange(value.TimeCost, 1, 3);
            Assert.Equal(value.TimeCost, value.RewardRed + value.RewardWhite + value.RewardBlack);
        });
        Assert.Equal(3, firstEvents.Count(value => value.Kind == ClimbScheduledEventKind.Hazard));
        Assert.Equal(2, firstEvents.Count(value => value.Kind == ClimbScheduledEventKind.Character));
        Assert.Equal([(1, 6), (7, 12), (13, 19), (20, 25), (26, 32)],
            Enumerable.Range(0, 5).Select(ClimbScheduleRuntime.GetEventAppearanceBand));
        foreach (ClimbEventScheduleEntry value in firstEvents)
        {
            (int start, int end) = ClimbScheduleRuntime.GetEventAppearanceBand(value.Position);
            Assert.InRange(value.ScheduledAppearanceTime, start, end);
            Assert.Equal(ClimbScheduledEventStatus.Scheduled, value.Status);
            Assert.Equal(-1, value.ActivatedAtTime);
            if (value.Kind == ClimbScheduledEventKind.Hazard)
            {
                Assert.Equal(0, value.TimeCost);
                Assert.InRange(value.Duration, 2, 4);
                Assert.InRange(value.RewardRed + value.RewardWhite + value.RewardBlack, 1, 2);
            }
            else
            {
                Assert.Equal(1, value.TimeCost);
                Assert.InRange(value.Duration, 3, 5);
                Assert.Equal(0, value.RewardRed + value.RewardWhite + value.RewardBlack);
            }
        }
    }

    [Fact]
    public void Event_lifecycle_activates_crossed_entries_expires_end_exclusive_and_preserves_pending_at_final_time()
    {
        ClimbEventScheduleEntry[] events =
        [
            new(0, new(1), ClimbScheduledEventKind.Hazard, 4, 2, 0, 1, 0, 0, -1, ClimbScheduledEventStatus.Scheduled, 1),
            new(1, new(2), ClimbScheduledEventKind.Character, 9, 5, 1, 0, 0, 0, -1, ClimbScheduledEventStatus.Scheduled, 2),
        ];

        Assert.True(ClimbScheduleRuntime.UpdateEventLifecycle(events, 12));
        Assert.All(events, value => Assert.Equal(ClimbScheduledEventStatus.Active, value.Status));
        Assert.All(events, value => Assert.Equal(12, value.ActivatedAtTime));
        Assert.False(ClimbScheduleRuntime.UpdateEventLifecycle(events, 13));
        Assert.True(ClimbScheduleRuntime.UpdateEventLifecycle(events, 14));
        Assert.Equal(ClimbScheduledEventStatus.Expired, events[0].Status);
        Assert.Equal(ClimbScheduledEventStatus.Active, events[1].Status);
        events[1].Status = ClimbScheduledEventStatus.Pending;
        Assert.False(ClimbScheduleRuntime.UpdateEventLifecycle(events.AsSpan(1, 1), 32));
        Assert.Equal(ClimbScheduledEventStatus.Pending, events[1].Status);
    }

    [Fact]
    public void Fresh_shop_has_fixed_order_distinct_medals_and_inventory_exclusions()
    {
        World world = CreateWorld();
        MetaRuntimeEntities roots = MetaSaveAdapter.Spawn(world, MetaSaveDto.Fresh(123));
        ClimbShopSlotAction[] offers = Offers(world);

        Assert.Equal(ClimbShopRefreshResult.SlotCount, offers.Length);
        Assert.Equal(
            [ClimbShopOfferKind.Medal, ClimbShopOfferKind.Medal, ClimbShopOfferKind.Equipment,
             ClimbShopOfferKind.Upgrade, ClimbShopOfferKind.Replacement],
            offers.Select(value => value.Kind));
        Assert.NotEqual(offers[0].Medal, offers[1].Medal);
        Assert.NotEqual(EquipmentId.BulwarkPlate, offers[2].Equipment);
        Assert.All(offers, offer => Assert.Equal(roots.Climb, offer.Run));
        Assert.All(offers, offer => Assert.InRange(offer.TimeCost, 0, 2));
        Assert.All(offers, offer => Assert.True(
            offer.Kind == ClimbShopOfferKind.Empty ||
            offer.RedCost + offer.WhiteCost + offer.BlackCost == 1 + offer.TimeCost));
    }

    [Fact]
    public void Shop_generation_is_repeatable_for_seed_time_deck_and_inventory()
    {
        ClimbShopSlotAction[] first = OffersFor(MetaSaveDto.Fresh(0xC0FFEE));
        ClimbShopSlotAction[] second = OffersFor(MetaSaveDto.Fresh(0xC0FFEE));

        Assert.Equal(first.Select(Comparable), second.Select(Comparable));
    }

    [Fact]
    public void Refresh_excludes_every_previously_shown_medal_and_equipment()
    {
        World world = CreateWorld();
        MetaRuntimeEntities roots = MetaSaveAdapter.Spawn(world, MetaSaveDto.Fresh(77));
        ClimbShopSlotAction[] first = Offers(world);

        ClimbShopRuntime.Refresh(world, roots.Run, roots.Climb);
        ClimbShopSlotAction[] second = Offers(world);

        Assert.DoesNotContain(second.Take(2), value => value.Medal == first[0].Medal || value.Medal == first[1].Medal);
        Assert.NotEqual(first[2].Equipment, second[2].Equipment);
        ClimbColumnTransitionState economy = world.Get<ClimbColumnTransitionState>(roots.Climb);
        Assert.Equal(6, world.GetDynamicBuffer(economy.ShownShopItems).Count);
    }

    [Fact]
    public void Unaffordable_purchase_is_atomic_and_does_not_mark_slot_sold()
    {
        World world = CreateWorld();
        MetaRuntimeEntities roots = MetaSaveAdapter.Spawn(world, MetaSaveDto.Fresh(42));
        EntityId slot = FindSlot(world, ClimbShopOfferKind.Medal);
        ClimbColumnTransitionState before = world.Get<ClimbColumnTransitionState>(roots.Climb);
        before.Red = before.White = before.Black = 0;
        world.Set(roots.Climb, in before);

        Assert.False(ClimbShopRuntime.TryPurchase(world, slot, roots.Run));

        ClimbColumnTransitionState after = world.Get<ClimbColumnTransitionState>(roots.Climb);
        Assert.Equal(before, after);
        Assert.Equal((byte)0, world.Get<ClimbShopSlotAction>(slot).Purchased);
        Assert.Equal((byte)0, world.Get<ClimbShopTooltipSource>(slot).Sold);
        Assert.Equal(0, Count(world.Query<EquippedMedal>()));
    }

    [Fact]
    public void Routed_medal_purchase_spends_once_equips_once_and_marks_both_states_sold()
    {
        World world = CreateWorld();
        MetaRuntimeEntities roots = MetaSaveAdapter.Spawn(world, MetaSaveDto.Fresh(91));
        FillWallet(world, roots.Climb);
        EntityId slot = FindSlot(world, ClimbShopOfferKind.Medal);
        ClimbShopSlotAction offer = world.Get<ClimbShopSlotAction>(slot);
        ClimbColumnTransitionState before = world.Get<ClimbColumnTransitionState>(roots.Climb);
        var events = new MetaGameEventHub();
        MetaGameComposition composition = MetaGameComposition.Create(world, events);
        world.AttachEventRuntime(new EventRuntime(new EventRoutingEndpoint(composition.GetRoutes())));

        events.ClimbShopSlotSelected.Publish(new(slot, roots.Run));
        events.ClimbShopSlotSelected.Publish(new(slot, roots.Run));
        world.Events.DrainBarrier();

        ClimbColumnTransitionState after = world.Get<ClimbColumnTransitionState>(roots.Climb);
        Assert.Equal(before.Red - offer.RedCost, after.Red);
        Assert.Equal(before.White - offer.WhiteCost, after.White);
        Assert.Equal(before.Black - offer.BlackCost, after.Black);
        Assert.Equal(before.Time + offer.TimeCost, after.Time);
        Assert.Equal((byte)1, world.Get<ClimbShopSlotAction>(slot).Purchased);
        Assert.Equal((byte)1, world.Get<ClimbShopTooltipSource>(slot).Sold);
        EquippedMedal medal = Assert.Single(Components(world.Query<EquippedMedal>()));
        Assert.Equal(offer.Medal, medal.Definition);
        Assert.Equal(roots.Run, medal.Owner);
    }

    [Fact]
    public void Upgrade_preserves_card_entity_and_replacement_preserves_order_with_new_identity()
    {
        World upgradeWorld = CreateWorld();
        MetaRuntimeEntities upgradeRoots = MetaSaveAdapter.Spawn(upgradeWorld, MetaSaveDto.Fresh(8));
        FillWallet(upgradeWorld, upgradeRoots.Climb);
        EntityId upgradeSlot = FindSlot(upgradeWorld, ClimbShopOfferKind.Upgrade);
        ClimbShopSlotAction upgrade = upgradeWorld.Get<ClimbShopSlotAction>(upgradeSlot);

        Assert.True(ClimbShopRuntime.TryPurchase(upgradeWorld, upgradeSlot, upgradeRoots.Run));
        Assert.True(upgradeWorld.IsAlive(upgrade.TargetCard));
        Assert.Equal((byte)1, upgradeWorld.Get<RunDeckCard>(upgrade.TargetCard).Upgraded);
        Assert.Equal(upgrade.TargetOrder, upgradeWorld.Get<RunDeckCard>(upgrade.TargetCard).Order);

        World replacementWorld = CreateWorld();
        MetaRuntimeEntities replacementRoots = MetaSaveAdapter.Spawn(replacementWorld, MetaSaveDto.Fresh(8));
        FillWallet(replacementWorld, replacementRoots.Climb);
        EntityId replacementSlot = FindSlot(replacementWorld, ClimbShopOfferKind.Replacement);
        ClimbShopSlotAction replacement = replacementWorld.Get<ClimbShopSlotAction>(replacementSlot);

        Assert.True(ClimbShopRuntime.TryPurchase(replacementWorld, replacementSlot, replacementRoots.Run));
        Assert.False(replacementWorld.IsAlive(replacement.TargetCard));
        (EntityId entity, RunDeckCard card) = FindCard(replacementWorld, replacement.TargetOrder);
        Assert.NotEqual(replacement.TargetCard, entity);
        Assert.Equal(replacement.Card, card.Definition);
        Assert.Equal((byte)0, card.Upgraded);
    }

    [Fact]
    public void Crossing_eight_time_boundary_refreshes_all_five_offers_but_final_time_does_not()
    {
        World world = CreateWorld();
        MetaRuntimeEntities roots = MetaSaveAdapter.Spawn(world, MetaSaveDto.Fresh(19));
        FillWallet(world, roots.Climb);
        EntityId purchased = FindSlot(world, ClimbShopOfferKind.Medal);
        ClimbShopSlotAction action = world.Get<ClimbShopSlotAction>(purchased);
        action.RedCost = action.WhiteCost = action.BlackCost = 0;
        action.TimeCost = 1;
        world.Set(purchased, in action);
        ref ClimbColumnTransitionState economy = ref world.Get<ClimbColumnTransitionState>(roots.Climb);
        economy.Time = 7;

        Assert.True(ClimbShopRuntime.TryPurchase(world, purchased, roots.Run));

        Assert.False(world.IsAlive(purchased));
        Assert.Equal(8, world.Get<ClimbColumnTransitionState>(roots.Climb).Time);
        Assert.Equal(5, Offers(world).Length);
        Assert.True(ClimbShopRuntime.CrossedRefreshBoundary(7, 8));
        Assert.False(ClimbShopRuntime.CrossedRefreshBoundary(31, 32));
    }

    [Fact]
    public void Purchased_reward_economy_inventory_and_exact_shop_round_trip_through_fresh_save_boundary()
    {
        World first = CreateWorld();
        MetaRuntimeEntities roots = MetaSaveAdapter.Spawn(first, MetaSaveDto.Fresh(5150));
        FillWallet(first, roots.Climb);
        EntityId medalSlot = FindSlot(first, ClimbShopOfferKind.Medal);
        EntityId equipmentSlot = FindSlot(first, ClimbShopOfferKind.Equipment);
        Assert.True(ClimbShopRuntime.TryPurchase(first, medalSlot, roots.Run));
        Assert.True(ClimbShopRuntime.TryPurchase(first, equipmentSlot, roots.Run));

        MetaSaveDto saved = MetaSaveAdapter.Extract(first, gold: 33);
        World second = CreateWorld();
        MetaRuntimeEntities restoredRoots = MetaSaveAdapter.Spawn(second, saved);
        MetaSaveDto roundTrip = MetaSaveAdapter.Extract(second, gold: 33);

        Assert.Equal(saved.ClimbSeed, roundTrip.ClimbSeed);
        Assert.Equal(saved.ClimbTime, roundTrip.ClimbTime);
        Assert.Equal(saved.RedResources, roundTrip.RedResources);
        Assert.Equal(saved.WhiteResources, roundTrip.WhiteResources);
        Assert.Equal(saved.BlackResources, roundTrip.BlackResources);
        Assert.Equal(saved.Cards, roundTrip.Cards);
        Assert.Equal(saved.Equipment, roundTrip.Equipment);
        Assert.Equal(saved.Medals, roundTrip.Medals);
        Assert.Equal(saved.ShownShopItems, roundTrip.ShownShopItems);
        Assert.Equal(saved.ShopOffers, roundTrip.ShopOffers);
        Assert.Equal(roots.Run.Index, restoredRoots.Run.Index);
    }

    private static ClimbShopSlotAction[] OffersFor(MetaSaveDto save)
    {
        World world = CreateWorld();
        MetaSaveAdapter.Spawn(world, save);
        return Offers(world);
    }

    private static ClimbShopSlotAction[] Offers(World world)
    {
        var values = new List<ClimbShopSlotAction>(5);
        foreach (QueryChunk<ClimbShopSlotAction> chunk in world.Query<ClimbShopSlotAction>())
        foreach (int row in chunk.Rows)
            values.Add(chunk.Component1[row]);
        return values.OrderBy(value => value.SlotIndex).ToArray();
    }

    private static object Comparable(ClimbShopSlotAction value) => new
    {
        value.Card, value.Equipment, value.Medal, value.SlotIndex, value.TargetOrder,
        value.RedCost, value.WhiteCost, value.BlackCost, value.TimeCost, value.Kind, value.Purchased,
    };

    private static EntityId FindSlot(World world, ClimbShopOfferKind kind)
    {
        foreach (QueryChunk<ClimbShopSlotAction> chunk in world.Query<ClimbShopSlotAction>())
        foreach (int row in chunk.Rows)
            if (chunk.Component1[row].Kind == kind) return chunk.Entities[row];
        throw new InvalidOperationException($"No {kind} shop slot was authored.");
    }

    private static (EntityId Entity, RunDeckCard Card) FindCard(World world, int order)
    {
        foreach (QueryChunk<RunDeckCard> chunk in world.Query<RunDeckCard>())
        foreach (int row in chunk.Rows)
            if (chunk.Component1[row].Order == order) return (chunk.Entities[row], chunk.Component1[row]);
        throw new InvalidOperationException($"No run card has order {order}.");
    }

    private static void FillWallet(World world, EntityId climb)
    {
        ref ClimbColumnTransitionState economy = ref world.Get<ClimbColumnTransitionState>(climb);
        economy.Red = economy.White = economy.Black = 20;
    }

    private static int Count<T>(Query<T> query) where T : unmanaged, IComponent
    {
        var count = 0;
        foreach (QueryChunk<T> chunk in query) count += chunk.Count;
        return count;
    }

    private static T[] Components<T>(Query<T> query) where T : unmanaged, IComponent
    {
        var values = new List<T>();
        foreach (QueryChunk<T> chunk in query)
        foreach (int row in chunk.Rows)
            values.Add(chunk.Component1[row]);
        return values.ToArray();
    }

    private static World CreateWorld() => new(GeneratedComponentRegistry.Create());
}
