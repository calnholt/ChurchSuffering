#nullable enable

using System;
using System.Linq;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Content.Equipment;
using Crusaders30XX.ECS.DataOriented.Content.Medals;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Effects;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Rules;
using Crusaders30XX.ECS.DataOriented.Storage;
using Crusaders30XX.ECS.DataOriented.Systems;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Gameplay.Effects;

public sealed class EffectGameplayRuntimeTests
{
    [Fact]
    public void Passive_buffer_is_sorted_stacks_and_resets_by_lifetime_epoch()
    {
        using var store = new DynamicBufferStore();
        DynamicBuffer<PassiveEntry> passives = store.Get(store.Create<PassiveEntry>(new EntityId(1, 1), 8));
        EntityId source = new(2, 1);

        PassiveRuntime.Apply(passives, source, RuleEffectIds.Vigor, 2, PassiveLifetime.Battle, 1, 1);
        PassiveRuntime.Apply(passives, source, RuleEffectIds.Aegis, 3, PassiveLifetime.Phase, 1, 1);
        PassiveRuntime.Apply(passives, source, RuleEffectIds.Vigor, -1, PassiveLifetime.Battle, 1, 1);

        Assert.Equal([RuleEffectIds.Aegis, RuleEffectIds.Vigor], passives.AsReadOnlySpan().ToArray().Select(value => value.Effect));
        Assert.Equal(1, PassiveRuntime.GetStacks(passives, RuleEffectIds.Vigor));
        Assert.Equal(1, PassiveRuntime.ResetPhase(passives, phaseEpoch: 2));
        Assert.Equal(1, passives.Count);
        Assert.Equal(1, PassiveRuntime.ResetBattle(passives, battleEpoch: 2));
        Assert.Empty(passives.AsReadOnlySpan().ToArray());
    }

    [Fact]
    public void Once_tracking_is_scoped_independently_to_phase_and_battle_epochs()
    {
        using var store = new DynamicBufferStore();
        DynamicBuffer<EffectTrackingEntry> tracking = store.Get(
            store.Create<EffectTrackingEntry>(new EntityId(1, 1), 4));
        EntityId source = new(3, 1);

        Assert.True(EffectTrackingRuntime.TryMark(tracking, source, RuleTriggerIds.CardReactive, EffectTrackingLifetime.Phase, 4));
        Assert.False(EffectTrackingRuntime.TryMark(tracking, source, RuleTriggerIds.CardReactive, EffectTrackingLifetime.Phase, 4));
        Assert.True(EffectTrackingRuntime.TryMark(tracking, source, RuleTriggerIds.CardReactive, EffectTrackingLifetime.Battle, 4));
        Assert.True(EffectTrackingRuntime.TryMark(tracking, source, RuleTriggerIds.CardReactive, EffectTrackingLifetime.Phase, 5));
    }

    [Fact]
    public void Alternate_and_replacement_providers_choose_lowest_equipped_entity()
    {
        EntityId owner = new(20, 1);
        EquippedMedalProvider[] alternateProviders =
        [
            Provider(9, owner, MedalId.StGeorge),
            Provider(2, owner, MedalId.StGeorge),
        ];
        var alternateQuery = new AlternatePlayQuery(
            owner, new EntityId(30, 1), CardId.ShieldOfFaith,
            RulePhase.Action, RuleCardTraits.Block);

        Assert.True(EquipmentMedalProviderRuntime.TryResolveAlternate(alternateProviders, in alternateQuery, out AlternatePlayResult alternate));
        Assert.Equal(2, alternate.Source.Entity.Index);
        Assert.True(alternate.FreeAction);
        Assert.Equal(3, alternate.AttackDamage);

        EquippedMedalProvider[] replacementProviders =
        [
            Provider(8, owner, MedalId.StOlaf),
            Provider(3, owner, MedalId.StOlaf),
        ];
        var replacementQuery = new ReplacementQuery(
            RuleReplacementKind.EffectThresholdDamage, owner, owner, new EntityId(50, 1),
            RuleEffectIds.Frostbite, RuleDamageKind.Effect, -3, 1);
        Span<ReplacementAction> actions = stackalloc ReplacementAction[4];
        ReplacementPlan plan = EquipmentMedalProviderRuntime.ResolveReplacement(
            replacementProviders, owner, in replacementQuery, actions);

        Assert.True(plan.IsHandled);
        Assert.Equal(3, plan.HandlingProvider.Entity.Index);
        Assert.Equal(1, plan.ActionCount);
        Assert.Equal(new EntityId(50, 1), actions[0].Target);
    }

    [Fact]
    public void Olaf_suppresses_original_frostbite_when_no_eligible_enemy_exists()
    {
        EntityId owner = new(20, 1);
        EquippedMedalProvider[] providers = [Provider(3, owner, MedalId.StOlaf)];
        var query = new ReplacementQuery(
            RuleReplacementKind.EffectThresholdDamage, owner, owner, EntityId.Null,
            RuleEffectIds.Frostbite, RuleDamageKind.Effect, -3, 0);

        ReplacementPlan plan = EquipmentMedalProviderRuntime.ResolveReplacement(
            providers, owner, in query, Span<ReplacementAction>.Empty);

        Assert.True(plan.IsHandled);
        Assert.Equal(0, plan.ActionCount);
    }

    [Fact]
    public void Stat_modifiers_accumulate_all_active_owner_providers()
    {
        EntityId owner = new(20, 1);
        EquippedMedalProvider[] providers =
        [
            Provider(7, owner, MedalId.StChristopher),
            Provider(2, owner, MedalId.StChristopher),
            Provider(1, new EntityId(99, 1), MedalId.StChristopher),
        ];
        var query = new CardStatQuery(
            owner, new EntityId(30, 1), owner, EntityId.Null, CardId.ShieldOfFaith,
            CardStatKind.Block, CardStatQueryMode.Preview,
            RuleCardTraits.Block | RuleCardTraits.Brittle, 3, 0);

        Assert.Equal(5, EquipmentMedalProviderRuntime.AggregateCardStat(providers, in query, out int count));
        Assert.Equal(2, count);
    }

    [Theory]
    [InlineData(TemperanceAbilityId.AngelicAura, 2, 5, 3)]
    [InlineData(TemperanceAbilityId.FlingFling, 3, 0, 2)]
    [InlineData(TemperanceAbilityId.IronResolve, 3, 38, 1)]
    [InlineData(TemperanceAbilityId.MeasuredBreath, 3, 0, 1)]
    [InlineData(TemperanceAbilityId.Radiance, 4, 6, 1)]
    [InlineData(TemperanceAbilityId.StaticSurge, 3, 40, 1)]
    [InlineData(TemperanceAbilityId.Unsheath, 3, 36, 5)]
    public void All_folded_temperance_abilities_preserve_threshold_and_payload(
        TemperanceAbilityId id,
        int threshold,
        ushort effect,
        int amount)
    {
        ref readonly TemperanceAbilityDefinition definition = ref TemperanceAbilityCatalog.GetDefinition(id);
        Assert.Equal(threshold, definition.Threshold);
        if (effect != 0)
        {
            Assert.Equal(new EffectId(effect), definition.Effect);
            Assert.Equal(amount, definition.EffectStacks);
        }
        else if (id == TemperanceAbilityId.FlingFling)
            Assert.Equal(amount, definition.SpawnCount);
        else
            Assert.Equal(amount, definition.DrawCount);

        int temperance = threshold;
        var commands = new RuleCommandBuffer(2);
        Assert.True(TemperanceRuntime.TryResolve(
            ref temperance, id, new EntityId(1, 1), new EntityId(2, 1), new EntityId(3, 1),
            commands.Writer, out int draws));
        Assert.Equal(0, temperance);
        Assert.Equal(definition.DrawCount, draws);
    }

    [Fact]
    public void Every_generated_equipment_and_medal_initializes_and_resets_from_static_catalogs()
    {
        foreach (EquipmentId id in Enum.GetValues<EquipmentId>())
        {
            var equipped = new EquippedEquipment { Definition = id, Active = 1 };
            EquipmentActivationRuntime.RefreshForBattle(ref equipped, 10);
            Assert.True(equipped.Usage.IsInitialized);
            Assert.Equal(1, equipped.Active);
        }
        foreach (MedalId id in Enum.GetValues<MedalId>())
        {
            var equipped = new EquippedMedal { Definition = id, Active = 1 };
            MedalActivationRuntime.RefreshForBattle(ref equipped, 10);
            Assert.True(equipped.State.IsInitialized);
            Assert.Equal(GeneratedMedalCatalog.GetDefinition(id).Trigger.Activation.Counter.InitialCount, equipped.State.Count);
        }
    }

    [Fact]
    public void Passive_mechanics_match_bleed_brittle_vigor_and_poison_boundaries()
    {
        Assert.Equal(2, PassiveMechanicRules.ResolveBleedTriggers(3, [2, 1, 4]));
        Assert.True(PassiveMechanicRules.ShouldMillForBrittle(1));
        Assert.False(PassiveMechanicRules.ShouldMillForBrittle(2));
        Assert.Equal(2, PassiveMechanicRules.ConsumeVigor(3, 2));

        int poison = 60_000;
        Assert.False(PassiveMechanicRules.TickPoison(ref poison, 30_000, active: true, tutorialPaused: false));
        Assert.True(PassiveMechanicRules.TickPoison(ref poison, 30_000, active: true, tutorialPaused: false));
        Assert.Equal(60_000, poison);
        Assert.False(PassiveMechanicRules.TickPoison(ref poison, 100, active: true, tutorialPaused: true));
    }

    [Fact]
    public void All_twelve_system_rows_have_unique_battle_descriptors()
    {
        IGameSystem[] systems =
        [
            new AppliedPassivesManagementSystem(), new BleedManagementSystem(), new BrittleManagementSystem(),
            new EquipmentBlockInteractionSystem(), new EquipmentManagerSystem(), new IntimidateManagementSystem(),
            new MedalManagerSystem(), new PoisonSystem(), new ReplacementEffectSystem(),
            new ScorchedManagementSystem(), new TemperanceManagerSystem(), new VigorManagementSystem(),
        ];

        Assert.Equal(12, systems.Length);
        Assert.Equal(12, systems.Select(value => value.Descriptor.Id).Distinct().Count());
        Assert.All(systems, value => Assert.Equal(SceneGroup.Battle, value.Descriptor.SceneGroup));
        Assert.All(systems, value => Assert.IsAssignableFrom<IUnscheduledEffectLedgerSystem>(value));
    }

    [Fact]
    public void Route_owned_composition_consumes_events_without_scheduling_noop_systems()
    {
        World world = new(GeneratedComponentRegistry.Create());
        using var buffers = new DynamicBufferStore();
        world.RegisterEntityDestructionListener(buffers);
        var events = new EffectGameplayEventHub();
        EffectGameplayComposition composition = EffectGameplayComposition.Create(world, buffers, events);
        Assert.Empty(composition.Systems.ToArray());

        EntityId owner = CreateOwner(world, temperance: 3, TemperanceAbilityId.IronResolve);
        EntityId equipment = CreateEquipment(world, owner, EquipmentId.WhetstoneGauntlets);
        var endpoint = new EventRoutingEndpoint(composition.Routes.ToArray());
        var runtime = new EventRuntime(endpoint);
        world.AttachEventRuntime(runtime);

        events.ApplyPassive.Publish(new(owner, owner, RuleEffectIds.Might, 2, PassiveLifetime.Battle));
        events.EquipmentActivate.Publish(new(equipment, owner, RulePhase.Action, 1));
        events.TriggerTemperance.Publish(new(owner, new EntityId(90, 1), new EntityId(91, 1)));
        runtime.DrainBarrier();

        AppliedPassives applied = world.Get<AppliedPassives>(owner);
        Assert.Equal(2, PassiveRuntime.GetStacks(buffers.Get(applied.Entries), RuleEffectIds.Might));
        Assert.Equal(1, world.Get<EquippedEquipment>(equipment).Usage.Uses);
        Assert.Equal(0, world.Get<Temperance>(owner).Amount);
        Assert.True(runtime.LastBarrierWaveCount >= 2);
    }

    [Fact]
    public void Operational_replacement_consumer_selects_lowest_olaf_deterministically()
    {
        World world = new(GeneratedComponentRegistry.Create());
        var events = new EffectGameplayEventHub();
        var replacement = new MedalEffectRuntimeSystem(world, events);
        var capture = new ReplacementCapture();
        var consumers = new EffectGameplayRouteConsumers()
            .Add<FrostbiteTriggered>(replacement)
            .Add<ReplacementEffectResolved>(capture);
        var runtime = new EventRuntime(new EventRoutingEndpoint(events.BuildRoutes(consumers)));
        world.AttachEventRuntime(runtime);
        EntityId owner = CreateOwner(world, 0, TemperanceAbilityId.AngelicAura);
        EntityId lowest = CreateMedal(world, owner, MedalId.StOlaf);
        CreateMedal(world, owner, MedalId.StOlaf);

        events.Frostbite.Publish(new(owner, owner, new EntityId(90, 1), 1));
        runtime.DrainBarrier();

        Assert.True(capture.Value.Plan.IsHandled);
        Assert.Equal(1, capture.Value.Plan.ActionCount);
        Assert.Equal(new EntityId(90, 1), capture.Value.Action0.Target);
        Assert.Equal(lowest, capture.Value.Plan.HandlingProvider.Entity);
    }

    [Fact]
    public void Twenty_ledger_routes_compose_into_the_root_endpoint_with_other_domains()
    {
        var hub = new EffectGameplayEventHub();
        IEventRoute[] owned = hub.BuildLedgerRoutes();
        Assert.Equal(20, owned.Length);
        Assert.Equal(20, owned.Select(route => route.EventTypeId).Distinct().Count());

        IEventRoute[] allEffects = hub.BuildRoutes();
        var rootRoutes = new IEventRoute[allEffects.Length + 1];
        allEffects.CopyTo(rootRoutes, 0);
        rootRoutes[^1] = new EventRoute<ExternalProbeEvent>(49999, nameof(ExternalProbeEvent), new EventStream<ExternalProbeEvent>());
        var endpoint = new EventRoutingEndpoint(rootRoutes);

        Assert.Equal(23, endpoint.RouteCount);
    }

    [Fact]
    public void Warmed_provider_stat_aggregation_allocates_zero_bytes()
    {
        EntityId owner = new(20, 1);
        EquippedMedalProvider[] providers =
        [
            Provider(1, owner, MedalId.StChristopher),
            Provider(2, owner, MedalId.StChristopher),
        ];
        var query = new CardStatQuery(
            owner, new EntityId(30, 1), owner, EntityId.Null, CardId.ShieldOfFaith,
            CardStatKind.Block, CardStatQueryMode.Preview,
            RuleCardTraits.Block | RuleCardTraits.Brittle, 3, 0);
        EquipmentMedalProviderRuntime.AggregateCardStat(providers, in query, out _);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++)
            EquipmentMedalProviderRuntime.AggregateCardStat(providers, in query, out _);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    private static EquippedMedalProvider Provider(int index, EntityId owner, MedalId definition) =>
        new(new EntityId(index, 1), new EquippedMedal
        {
            Owner = owner,
            Definition = definition,
            Active = 1,
            Random = RuleRandomState.FromSeed((ulong)index),
        });

    private static EntityId CreateOwner(World world, int temperance, TemperanceAbilityId ability)
    {
        var bundle = new SpawnBundle(2);
        bundle.Add(new Temperance { Amount = temperance });
        bundle.Add(new EquippedTemperanceAbility { Definition = ability });
        return world.Create(in bundle);
    }

    private static EntityId CreateEquipment(World world, EntityId owner, EquipmentId definition)
    {
        var bundle = new SpawnBundle(2);
        bundle.Add(new EquippedEquipment
        {
            Owner = owner,
            Definition = definition,
            Active = 1,
            Random = RuleRandomState.FromSeed(1),
        });
        bundle.Add(new EquipmentZone { Owner = owner, Kind = EquipmentZoneKind.Equipped });
        return world.Create(in bundle);
    }

    private static EntityId CreateMedal(World world, EntityId owner, MedalId definition)
    {
        var bundle = new SpawnBundle(1);
        bundle.Add(new EquippedMedal
        {
            Owner = owner,
            Definition = definition,
            Active = 1,
            Random = RuleRandomState.FromSeed(1),
        });
        return world.Create(in bundle);
    }

    private sealed class ReplacementCapture : IEventConsumer<ReplacementEffectResolved>
    {
        public ReplacementEffectResolved Value;
        public void Consume(in ReplacementEffectResolved value, ref EventDispatchContext context) => Value = value;
    }

    private readonly record struct ExternalProbeEvent(int Value);
}
