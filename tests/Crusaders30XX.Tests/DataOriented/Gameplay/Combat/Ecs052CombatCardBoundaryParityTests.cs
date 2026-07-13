#nullable enable

using System;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Cards;
using Crusaders30XX.ECS.DataOriented.Gameplay.Combat;
using Crusaders30XX.ECS.DataOriented.Gameplay.Effects;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Rules;
using Crusaders30XX.ECS.DataOriented.Storage;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Gameplay.Combat;

public sealed class Ecs052CombatCardBoundaryParityTests
{
    [Theory]
    [InlineData(EnemyId.Mummy, EnemyAttackId.Entomb, true)]
    [InlineData(EnemyId.IceDemon, EnemyAttackId.FrozenClaw, false)]
    public void Damage_threshold_applies_the_expected_effect_to_only_the_top_draw_card(
        EnemyId enemy,
        EnemyAttackId attack,
        bool brittle)
    {
        BoundaryFixture fixture = Create(enemy);
        EntityId top = CardGameplayFactory.CreateCard(fixture.World, fixture.Deck, CardId.Strike, CardZone.DrawPile);
        EntityId next = CardGameplayFactory.CreateCard(fixture.World, fixture.Deck, CardId.Fervor, CardZone.DrawPile);
        StartAttack(fixture.Session, attack);
        int required = fixture.World.Get<EnemyAttackProgress>(fixture.Session.Battle).RequiredAmount;
        Assert.True(fixture.Session.AssignBlock(CreateBlocker(fixture.World), required - 1, RuleCardColor.White));

        ResolveCurrentAttack(fixture.Session);

        Assert.Equal(brittle, fixture.World.Has<Brittle>(top));
        Assert.Equal(!brittle, fixture.World.Has<Frozen>(top));
        Assert.False(fixture.World.Has<Brittle>(next));
        Assert.False(fixture.World.Has<Frozen>(next));
    }

    [Fact]
    public void Strange_force_draws_on_reveal_and_mills_on_hit_when_two_color_condition_fails()
    {
        BoundaryFixture fixture = Create(EnemyId.Sorcerer);
        EntityId drawn = CardGameplayFactory.CreateCard(fixture.World, fixture.Deck, CardId.Strike, CardZone.DrawPile);
        EntityId milled = CardGameplayFactory.CreateCard(fixture.World, fixture.Deck, CardId.Fervor, CardZone.DrawPile);

        fixture.Session.Process();
        Assert.Equal(EnemyAttackId.StrangeForce, fixture.World.Get<EnemyAttackProgress>(fixture.Session.Battle).Attack);

        Assert.Equal(CardZone.Hand, fixture.World.Get<CardZoneLocation>(drawn).Zone);
        ResolveCurrentAttack(fixture.Session);
        Assert.Equal(CardZone.DiscardPile, fixture.World.Get<CardZoneLocation>(milled).Zone);
        Assert.Equal(1, fixture.World.Get<Deck>(fixture.Deck).CardsMilled);
    }

    [Fact]
    public void Strange_force_does_not_mill_when_blocked_by_two_distinct_colors()
    {
        BoundaryFixture fixture = Create(EnemyId.Sorcerer);
        _ = CardGameplayFactory.CreateCard(fixture.World, fixture.Deck, CardId.Strike, CardZone.DrawPile);
        EntityId spared = CardGameplayFactory.CreateCard(fixture.World, fixture.Deck, CardId.Fervor, CardZone.DrawPile);
        fixture.Session.Process();
        Assert.Equal(EnemyAttackId.StrangeForce, fixture.World.Get<EnemyAttackProgress>(fixture.Session.Battle).Attack);
        Assert.True(fixture.Session.AssignBlock(CreateBlocker(fixture.World), 1, RuleCardColor.Red));
        Assert.True(fixture.Session.AssignBlock(CreateBlocker(fixture.World), 1, RuleCardColor.White));

        ResolveCurrentAttack(fixture.Session);

        Assert.Equal(CardZone.DrawPile, fixture.World.Get<CardZoneLocation>(spared).Zone);
        Assert.Equal(0, fixture.World.Get<Deck>(fixture.Deck).CardsMilled);
    }

    [Fact]
    public void Frost_eater_planning_observes_frozen_cards_in_the_bound_hand()
    {
        bool selectedWithTwo = false;
        for (ulong seed = 1; seed <= 32 && !selectedWithTwo; seed++)
        {
            BoundaryFixture fixture = Create(EnemyId.IceDemon, seed);
            EntityId first = CardGameplayFactory.CreateCard(fixture.World, fixture.Deck, CardId.Strike, CardZone.Hand);
            EntityId second = CardGameplayFactory.CreateCard(fixture.World, fixture.Deck, CardId.Fervor, CardZone.Hand);
            var commands = new CommandBuffer();
            commands.AddTag<Frozen>(first);
            commands.AddTag<Frozen>(second);
            commands.Playback(fixture.World);

            fixture.Session.Process();

            ReadOnlySpan<EnemyAttackId> intent = fixture.World.GetDynamicBuffer(
                fixture.World.Get<AttackIntent>(fixture.Session.Battle).Attacks).AsReadOnlySpan();
            for (var index = 0; index < intent.Length; index++)
                if (intent[index] == EnemyAttackId.FrostEater) selectedWithTwo = true;
        }

        Assert.True(selectedWithTwo);
    }

    [Fact]
    public void Impossible_must_block_is_removed_but_hand_cards_and_active_equipment_keep_it_active()
    {
        BoundaryFixture empty = Create(EnemyId.GlacialGuardian);
        StartAttack(empty.Session, EnemyAttackId.GlacialStrike);
        Assert.Equal(RequirementKind.None, empty.World.Get<EnemyAttackProgress>(empty.Session.Battle).Requirement);

        BoundaryFixture hand = Create(EnemyId.GlacialGuardian);
        _ = CardGameplayFactory.CreateCard(hand.World, hand.Deck, CardId.Strike, CardZone.Hand, color: RuleCardColor.White);
        StartAttack(hand.Session, EnemyAttackId.GlacialStrike);
        Assert.Equal(RequirementKind.MinimumBlockers, hand.World.Get<EnemyAttackProgress>(hand.Session.Battle).Requirement);

        BoundaryFixture equipment = Create(EnemyId.GlacialGuardian);
        var equipmentBundle = new SpawnBundle(2);
        equipmentBundle.Add(new EquippedEquipment { Owner = equipment.Session.Player, Definition = EquipmentId.IvoryCoif, Active = 1 });
        equipmentBundle.Add(new EquipmentZone { Owner = equipment.Session.Player, Kind = EquipmentZoneKind.Equipped });
        equipment.World.Create(in equipmentBundle);
        StartAttack(equipment.Session, EnemyAttackId.GlacialStrike);
        Assert.Equal(RequirementKind.MinimumBlockers, equipment.World.Get<EnemyAttackProgress>(equipment.Session.Battle).Requirement);
    }

    [Fact]
    public void Have_no_mercy_discards_the_selected_card_only_when_unblocked()
    {
        BoundaryFixture unblocked = Create(EnemyId.Ninja);
        EntityId discarded = CardGameplayFactory.CreateCard(unblocked.World, unblocked.Deck, CardId.Strike, CardZone.Hand);
        StartAttack(unblocked.Session, EnemyAttackId.HaveNoMercy);
        Assert.True(unblocked.World.Has<MarkedForSpecificDiscard>(discarded));
        ResolveCurrentAttack(unblocked.Session);
        Assert.False(unblocked.World.Has<MarkedForSpecificDiscard>(discarded));
        Assert.Equal(CardZone.DiscardPile, unblocked.World.Get<CardZoneLocation>(discarded).Zone);

        BoundaryFixture blocked = Create(EnemyId.Ninja);
        EntityId spared = CardGameplayFactory.CreateCard(blocked.World, blocked.Deck, CardId.Strike, CardZone.Hand);
        StartAttack(blocked.Session, EnemyAttackId.HaveNoMercy);
        Assert.True(blocked.Session.AssignBlock(CreateBlocker(blocked.World), 0, RuleCardColor.White));
        ResolveCurrentAttack(blocked.Session);
        Assert.False(blocked.World.Has<MarkedForSpecificDiscard>(spared));
        Assert.Equal(CardZone.Hand, blocked.World.Get<CardZoneLocation>(spared).Zone);
    }

    [Fact]
    public void Fallen_shepherd_have_no_mercy_discards_below_the_dynamic_threshold()
    {
        BoundaryFixture fixture = Create(EnemyId.FallenShepherd);
        EntityId selected = CardGameplayFactory.CreateCard(fixture.World, fixture.Deck, CardId.Strike, CardZone.Hand);
        StartAttack(fixture.Session, EnemyAttackId.FallenShepherdPhase3);
        int required = fixture.World.Get<EnemyAttackProgress>(fixture.Session.Battle).RequiredAmount;
        Assert.True(fixture.Session.AssignBlock(CreateBlocker(fixture.World), required - 1, RuleCardColor.White));

        ResolveCurrentAttack(fixture.Session);

        Assert.Equal(CardZone.DiscardPile, fixture.World.Get<CardZoneLocation>(selected).Zone);
        Assert.False(fixture.World.Has<MarkedForSpecificDiscard>(selected));
    }

    private static BoundaryFixture Create(EnemyId enemy, ulong seed = 1)
    {
        var world = new World(GeneratedComponentRegistry.Create());
        var combatHub = new CombatEventHub();
        var cardHub = new CardGameplayEventHub();
        var owned = new CombatOwnedEventConsumers(world);
        IEventRoute[] combatRoutes = combatHub.BuildRoutes(owned.RegisterRoutes());
        IEventRoute[] cardRoutes = cardHub.BuildRoutes();
        var routes = new IEventRoute[combatRoutes.Length + cardRoutes.Length];
        combatRoutes.CopyTo(routes, 0);
        cardRoutes.CopyTo(routes, combatRoutes.Length);
        world.AttachEventRuntime(new EventRuntime(new EventRoutingEndpoint(routes)));
        CombatSession session = CombatSession.Create(world, combatHub, enemy, seed: seed);
        owned.Bind(session);
        EntityId deck = CardGameplayFactory.CreateDeck(world, session.Player, seed + 1000);
        session.BindCardBoundary(deck, new CombatCardBoundary(world, cardHub));
        return new BoundaryFixture(world, session, deck);
    }

    private static void StartAttack(CombatSession session, EnemyAttackId attack)
    {
        session.Process();
        ref AttackIntent intent = ref session.World.Get<AttackIntent>(session.Battle);
        DynamicBuffer<EnemyAttackId> attacks = session.World.GetDynamicBuffer(intent.Attacks);
        attacks.Clear();
        attacks.Add(attack);
        intent.CurrentIndex = -1;
        intent.Current = default;
        session.World.Get<BattleStateInfo>(session.Battle).Flags &= ~CombatFlags.AwaitingBlockConfirmation;
        session.EnqueueMandatory(CombatRuleKind.BeginEnemyAttack);
        session.Process();
        Assert.Equal(attack, session.World.Get<EnemyAttackProgress>(session.Battle).Attack);
    }

    private static void ResolveCurrentAttack(CombatSession session)
    {
        session.ConfirmBlocks();
        Assert.True(session.Process().IsWaiting);
        session.Process();
    }

    private static EntityId CreateBlocker(World world)
    {
        var bundle = new SpawnBundle(1);
        bundle.Add(new ModifiedBlock { Base = 1 });
        return world.Create(in bundle);
    }

    private readonly record struct BoundaryFixture(World World, CombatSession Session, EntityId Deck);
}
