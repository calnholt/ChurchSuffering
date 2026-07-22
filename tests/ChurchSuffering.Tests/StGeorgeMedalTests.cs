using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using ChurchSuffering.ECS.Objects.Medals;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace ChurchSuffering.Tests;

public sealed class StGeorgeMedalTests : IDisposable
{
    public StGeorgeMedalTests()
    {
        EventManager.Clear();
        EventQueue.Clear();
        StateSingleton.IsActive = false;
    }

    public void Dispose()
    {
        EventManager.Clear();
        EventQueue.Clear();
        StateSingleton.IsActive = false;
    }

    [Fact]
    public void MedalFactory_includes_st_george()
    {
        Assert.IsType<StGeorge>(MedalFactory.Create("st_george"));
        Assert.Contains(MedalId.StGeorge, MedalFactory.GetAllMedals().Keys);
    }

    [Fact]
    public void Block_card_cannot_be_played_without_st_george()
    {
        var entityManager = BuildActionBattle(1, out var player, out var deck);
        var block = AddBlockCard(entityManager, deck, "mantlet");
        var enemy = entityManager.GetEntity("Enemy");
        entityManager.AddComponent(enemy, new HP { Current = 10, Max = 10 });
        _ = new CardPlaySystem(entityManager);
        _ = new HpManagementSystem(entityManager);
        string message = null;
        EventManager.Subscribe<CantPlayCardMessage>(evt => message = evt.Message);

        EventManager.Publish(new PlayCardRequested { Card = block });

        Assert.Equal("Block cards can only be used to block!", message);
        Assert.Equal(10, enemy.GetComponent<HP>().Current);
        Assert.Equal(1, player.GetComponent<ActionPoints>().Current);
        Assert.Contains(block, deck.Hand);
    }

    [Fact]
    public void Block_card_can_be_played_as_free_attack_with_st_george()
    {
        var entityManager = BuildActionBattle(1, out var player, out var deck);
        EquipMedal(entityManager, player, new StGeorge());
        var block = AddBlockCard(entityManager, deck, "mantlet");
        var enemy = entityManager.GetEntity("Enemy");
        entityManager.AddComponent(enemy, new HP { Current = 10, Max = 10 });
        _ = new CardPlaySystem(entityManager);
        _ = new HpManagementSystem(entityManager);
        _ = new CardZoneSystem(entityManager);

        int cardPlayedCount = 0;
        int medalTriggeredCount = 0;
        CardMoveRequested moveRequest = null;
        EventManager.Subscribe<CardPlayedEvent>(_ => cardPlayedCount++);
        EventManager.Subscribe<MedalTriggered>(evt =>
        {
            medalTriggeredCount++;
            Assert.Equal(StGeorge.MedalIdValue, evt.MedalId);
        });
        EventManager.Subscribe<CardMoveRequested>(evt =>
        {
            if (evt.Destination == CardZoneType.DiscardPile)
                moveRequest = evt;
        });

        EventManager.Publish(new PlayCardRequested { Card = block });

        Assert.Equal(7, enemy.GetComponent<HP>().Current);
        Assert.Equal(1, player.GetComponent<ActionPoints>().Current);
        Assert.Equal(1, cardPlayedCount);
        Assert.Equal(1, medalTriggeredCount);
        Assert.NotNull(moveRequest);
        Assert.Same(block, moveRequest.Card);
        Assert.True(
            !deck.Hand.Contains(block) || block.GetComponent<AnimatingHandToZone>() != null,
            "Played card should leave hand or begin discard animation.");
    }

    [Fact]
    public void Alternate_block_attack_uses_normal_attack_modifiers()
    {
        var entityManager = BuildActionBattle(1, out var player, out var deck);
        EquipMedal(entityManager, player, new StGeorge());
        var block = AddBlockCard(entityManager, deck, "mantlet");
        var enemy = entityManager.GetEntity("Enemy");
        entityManager.AddComponent(enemy, new HP { Current = 20, Max = 20 });
        player.GetComponent<AppliedPassives>().Passives[AppliedPassiveType.Power] = 2;
        _ = new CardPlaySystem(entityManager);
        _ = new HpManagementSystem(entityManager);

        EventManager.Publish(new PlayCardRequested { Card = block });

        Assert.Equal(15, enemy.GetComponent<HP>().Current);
    }

    [Fact]
    public void Alternate_block_attack_does_not_fire_on_block()
    {
        var entityManager = BuildActionBattle(1, out var player, out var deck);
        EquipMedal(entityManager, player, new StGeorge());
        var block = AddBlockCard(entityManager, deck, "hold_the_line");
        _ = new CardPlaySystem(entityManager);
        int courageChanges = 0;
        EventManager.Subscribe<ModifyCourageRequestEvent>(_ => courageChanges++);

        EventManager.Publish(new PlayCardRequested { Card = block });

        Assert.Equal(0, courageChanges);
    }

    [Fact]
    public void Block_phase_assignment_still_uses_normal_block_behavior()
    {
        var entityManager = new EntityManager();
        var phaseEntity = entityManager.CreateEntity("PhaseState");
        entityManager.AddComponent(phaseEntity, new PhaseState
        {
            Main = MainPhase.EnemyTurn,
            Sub = SubPhase.Block,
        });
        var deckEntity = entityManager.CreateEntity("Deck");
        var deck = new Deck();
        entityManager.AddComponent(deckEntity, deck);
        var player = entityManager.CreateEntity("Player");
        entityManager.AddComponent(player, new Player { DeckEntity = deckEntity });
        EquipMedal(entityManager, player, new StGeorge());

        var block = AddBlockCard(entityManager, deck, "hold_the_line");
        entityManager.AddComponent(block, new UIElement { IsInteractable = true });
        entityManager.AddComponent(block, new Transform { Position = new Vector2(100, 200) });

        var enemy = entityManager.CreateEntity("Enemy");
        entityManager.AddComponent(enemy, new HP { Current = 10, Max = 10 });
        entityManager.AddComponent(enemy, new AttackIntent
        {
            ActiveAttackSequence = 1,
            Planned = [new PlannedAttack { AttackDefinition = new EnemyAttackBase() }],
        });

        _ = new HandBlockInteractionSystem(entityManager);
        _ = new CardPlaySystem(entityManager);
        _ = new HpManagementSystem(entityManager);

        int assignments = 0;
        int attackDamage = 0;
        EventManager.Subscribe<BlockAssignmentAdded>(_ => assignments++);
        EventManager.Subscribe<ModifyHpRequestEvent>(evt =>
        {
            if (evt.DamageType == ModifyTypeEnum.Attack)
                attackDamage += Math.Abs(evt.Delta);
        });

        EventManager.Publish(new AssignCardAsBlockRequested { Card = block });

        Assert.Equal(1, assignments);
        Assert.Equal(0, attackDamage);
        Assert.Equal(10, enemy.GetComponent<HP>().Current);
    }

    [Fact]
    public void Resolver_allows_st_george_alternate_block_play()
    {
        var entityManager = BuildActionBattle(1, out var player, out var deck);
        var block = AddBlockCard(entityManager, deck, "mantlet");

        Assert.Null(AlternateCardPlayService.GetProfile(entityManager, block, SubPhase.Action));
        Assert.False(ResolveActionPlayability(entityManager, block, deck).IsPlayable);

        EquipMedal(entityManager, player, new StGeorge());

        var profile = AlternateCardPlayService.GetProfile(entityManager, block, SubPhase.Action);
        Assert.NotNull(profile);
        Assert.True(profile.AllowsPlay);
        var plan = ResolveActionPlayability(entityManager, block, deck);
        Assert.True(plan.IsPlayable);
        Assert.True(plan.IsFreeAction);
        Assert.Equal(CardPlayMode.AlternateAttack, plan.Mode);
    }

    private static CardPlayPlan ResolveActionPlayability(
        EntityManager entityManager,
        Entity cardEntity,
        Deck deck)
    {
        var data = cardEntity.GetComponent<CardData>();
        var card = data?.Card;
        var alternateProfile = AlternateCardPlayService.GetProfile(entityManager, cardEntity, SubPhase.Action);
        var player = entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
        var pledge = cardEntity.GetComponent<Pledge>();
        var appliedPassives = player?.GetComponent<AppliedPassives>();
        bool isSilenced = appliedPassives != null
            && appliedPassives.Passives.TryGetValue(AppliedPassiveType.Silenced, out int silencedStacks)
            && silencedStacks > 0;
        return CardPlayResolver.Resolve(new CardPlayContext(
            cardEntity,
            card,
            SubPhase.Action,
            player?.GetComponent<ActionPoints>()?.Current ?? 0,
            VigorService.GetPlayerVigorStacks(entityManager),
            false,
            pledge != null,
            pledge?.CanPlay ?? true,
            isSilenced,
            card.CanPlay?.Invoke(entityManager, cardEntity) ?? true,
            alternateProfile,
            deck.Hand));
    }

    private static EntityManager BuildActionBattle(int actionPoints, out Entity player, out Deck deck)
    {
        var entityManager = new EntityManager();
        var phaseEntity = entityManager.CreateEntity("PhaseState");
        entityManager.AddComponent(phaseEntity, new PhaseState
        {
            Main = MainPhase.PlayerTurn,
            Sub = SubPhase.Action,
        });

        var deckEntity = entityManager.CreateEntity("Deck");
        deck = new Deck();
        entityManager.AddComponent(deckEntity, deck);

        player = entityManager.CreateEntity("Player");
        entityManager.AddComponent(player, new Player { DeckEntity = deckEntity });
        entityManager.AddComponent(player, new ActionPoints { Current = actionPoints });
        entityManager.AddComponent(player, new Courage());
        entityManager.AddComponent(player, new AppliedPassives());

        var enemy = entityManager.CreateEntity("Enemy");
        entityManager.AddComponent(enemy, new Enemy());
        entityManager.AddComponent(enemy, new AppliedPassives());

        var tutorial = entityManager.CreateEntity("GuidedTutorial");
        entityManager.AddComponent(tutorial, new GuidedTutorial { Section = 1 });

        return entityManager;
    }

    private static Entity AddBlockCard(EntityManager entityManager, Deck deck, string cardId)
    {
        var card = EntityFactory.CreateCardFromDefinition(entityManager, cardId, CardData.CardColor.White);
        Assert.NotNull(card);
        deck.Cards.Add(card);
        deck.Hand.Add(card);
        return card;
    }

    private static void EquipMedal(EntityManager entityManager, Entity player, MedalBase medal)
    {
        var medalEntity = entityManager.CreateEntity($"Medal_{medal.Id}");
        entityManager.AddComponent(medalEntity, new EquippedMedal
        {
            EquippedOwner = player,
            Medal = medal,
        });
        medal.Initialize(entityManager, medalEntity);
    }
}
