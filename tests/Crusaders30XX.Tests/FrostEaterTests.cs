using System;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Objects.Enemies;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public class FrostEaterTests : IDisposable
{
    public FrostEaterTests()
    {
        EventManager.Clear();
    }

    public void Dispose()
    {
        EventManager.Clear();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Requires_two_frozen_cards_in_hand_before_it_can_be_selected(int frozenCardCount)
    {
        var entityManager = new EntityManager();
        var deckEntity = entityManager.CreateEntity("Deck");
        var deck = new Deck();
        entityManager.AddComponent(deckEntity, deck);
        deck.Hand.Add(CreateHandCard(entityManager, "Card1", frozen: frozenCardCount >= 1));
        deck.Hand.Add(CreateHandCard(entityManager, "UnfrozenCard2", frozen: false));

        var iceDemon = new IceDemon();

        for (int i = 0; i < 20; i++)
        {
            Assert.DoesNotContain(EnemyAttackId.FrostEater, iceDemon.GetAttackIds(entityManager, turnNumber: 1));
        }
    }

    [Fact]
    public void Frozen_block_penalty_is_stable_across_recomputes_and_reverses_when_removed()
    {
        var entityManager = CreateCombat();
        var progressSystem = new EnemyAttackProgressManagementSystem(entityManager);
        var frozenBlocker = CreateAssignedBlocker(entityManager, "FrozenBlocker", blockAmount: 2, frozen: true);

        EventManager.Publish(new BlockAssignmentAdded
        {
            Card = frozenBlocker,
            DeltaBlock = 2,
            Colors = [CardData.CardColor.White],
        });

        for (int i = 0; i < 10; i++)
        {
            progressSystem.Update(new GameTime());
        }

        Assert.True(EnemyAttackFlowService.TryGetCurrentProgress(entityManager, out var progress));
        Assert.Equal(2, progress.AssignedBlockTotal);
        Assert.Equal(1, progress.EffectiveAssignedBlockTotal);
        Assert.Equal(8, progress.ActualDamage);
        Assert.Equal(1, progress.TotalPreventedDamage);

        entityManager.RemoveComponent<AssignedBlockCard>(frozenBlocker);
        EventManager.Publish(new BlockAssignmentRemoved
        {
            Card = frozenBlocker,
            DeltaBlock = -2,
            Colors = [CardData.CardColor.White],
        });

        Assert.Equal(0, progress.AssignedBlockTotal);
        Assert.Equal(0, progress.EffectiveAssignedBlockTotal);
        Assert.Equal(9, progress.ActualDamage);
        Assert.Equal(0, progress.TotalPreventedDamage);
    }

    [Fact]
    public void Frozen_block_penalty_cannot_reduce_effective_block_below_zero()
    {
        var entityManager = CreateCombat();
        var progressSystem = new EnemyAttackProgressManagementSystem(entityManager);
        var firstBlocker = CreateAssignedBlocker(entityManager, "FirstFrozenBlocker", blockAmount: 1, frozen: true);
        var secondBlocker = CreateAssignedBlocker(entityManager, "SecondFrozenBlocker", blockAmount: 1, frozen: true);

        EventManager.Publish(new BlockAssignmentAdded { Card = firstBlocker, DeltaBlock = 1, Colors = [CardData.CardColor.White] });
        EventManager.Publish(new BlockAssignmentAdded { Card = secondBlocker, DeltaBlock = 1, Colors = [CardData.CardColor.White] });
        progressSystem.Update(new GameTime());

        Assert.True(EnemyAttackFlowService.TryGetCurrentProgress(entityManager, out var progress));
        Assert.Equal(2, progress.AssignedBlockTotal);
        Assert.Equal(0, progress.EffectiveAssignedBlockTotal);
        Assert.Equal(9, progress.ActualDamage);
    }

    [Fact]
    public void Resolved_damage_uses_frozen_cards_reduced_block_value()
    {
        var entityManager = CreateCombat();
        var progressSystem = new EnemyAttackProgressManagementSystem(entityManager);
        _ = new AttackResolutionSystem(entityManager);
        _ = new EnemyDamageManagerSystem(entityManager);
        var frozenBlocker = CreateAssignedBlocker(entityManager, "FrozenBlocker", blockAmount: 2, frozen: true);
        EnemyDamageAppliedEvent damageApplied = null;
        EventManager.Subscribe<EnemyDamageAppliedEvent>(evt => damageApplied = evt);

        EventManager.Publish(new BlockAssignmentAdded
        {
            Card = frozenBlocker,
            DeltaBlock = 2,
            Colors = [CardData.CardColor.White],
        });
        progressSystem.Update(new GameTime());
        EventManager.Publish(new ResolveAttack());
        EventManager.Publish(new EnemyAttackImpactNow());

        Assert.NotNull(damageApplied);
        Assert.Equal(8, damageApplied.FinalDamage);
        Assert.Equal(9, damageApplied.TotalDamage);
    }

    private static EntityManager CreateCombat()
    {
        var entityManager = new EntityManager();
        var phase = entityManager.CreateEntity("PhaseState");
        entityManager.AddComponent(phase, new PhaseState { Sub = SubPhase.Block });

        var player = entityManager.CreateEntity("Player");
        entityManager.AddComponent(player, new Player());

        var attack = new FrostEater();
        var enemy = entityManager.CreateEntity("IceDemon");
        entityManager.AddComponent(enemy, new AttackIntent
        {
            ActiveAttackSequence = 1,
            Planned =
            [
                new PlannedAttack
                {
                    AttackId = attack.Id,
                    AttackDefinition = attack,
                },
            ],
        });

        return entityManager;
    }

    private static Entity CreateAssignedBlocker(EntityManager entityManager, string name, int blockAmount, bool frozen)
    {
        var card = entityManager.CreateEntity(name);
        entityManager.AddComponent(card, new CardData { Card = new CardBase() });
        entityManager.AddComponent(card, new AssignedBlockCard
        {
            BlockAmount = blockAmount,
        });
		entityManager.AddComponent(card, new AssignedBlockPresentation { Phase = AssignedBlockPresentation.PhaseState.Idle });
        if (frozen)
        {
            entityManager.AddComponent(card, new Frozen { Owner = card });
        }
        return card;
    }

    private static Entity CreateHandCard(EntityManager entityManager, string name, bool frozen)
    {
        var card = entityManager.CreateEntity(name);
        entityManager.AddComponent(card, new CardData { Card = new CardBase() });
        if (frozen)
        {
            entityManager.AddComponent(card, new Frozen { Owner = card });
        }
        return card;
    }
}
