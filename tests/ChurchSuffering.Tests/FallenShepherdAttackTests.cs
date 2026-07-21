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
using ChurchSuffering.ECS.Objects.Enemies;
using ChurchSuffering.ECS.Systems;
using Xunit;

namespace ChurchSuffering.Tests;

public class FallenShepherdAttackTests : IDisposable
{
    private static readonly HashSet<EnemyAttackId> Phase1SmallPool =
    [
        EnemyAttackId.FallenShepherdCrooksScar,
        EnemyAttackId.FallenShepherdBreakFaith,
        EnemyAttackId.FallenShepherdBloodletting,
        EnemyAttackId.FallenShepherdCowTheFlock,
    ];

    private static readonly HashSet<EnemyAttackId> Phase2SmallPool =
    [
        EnemyAttackId.FallenShepherdShepherdsVigil,
        EnemyAttackId.FallenShepherdHush,
        EnemyAttackId.FallenShepherdCrooksScar,
        EnemyAttackId.FallenShepherdCowTheFlock,
    ];

    private static readonly HashSet<EnemyAttackId> Phase3Pool =
    [
        EnemyAttackId.FallenShepherdPurgeTheHeretic,
        EnemyAttackId.FallenShepherdFearTheShepherd,
        EnemyAttackId.FallenShepherdFinalSermon,
        EnemyAttackId.FallenShepherdPhase3,
    ];

    public FallenShepherdAttackTests()
    {
        EventManager.Clear();
    }

    public void Dispose()
    {
        EventManager.Clear();
    }

    [Fact]
    public void Phase_one_alternates_heavy_with_three_distinct_small_attacks()
    {
        var shepherd = new FallenShepherd();

        Assert.Equal(new[] { EnemyAttackId.FallenShepherdPhase1 }, shepherd.GetAttackIds(null, 1));
        Assert.Equal(new[] { EnemyAttackId.FallenShepherdPhase1 }, shepherd.GetAttackIds(null, 0));

        var selected = shepherd.GetAttackIds(null, 2).ToList();
        Assert.Equal(3, selected.Count);
        Assert.Equal(3, selected.Distinct().Count());
        Assert.All(selected, attackId => Assert.Contains(attackId, Phase1SmallPool));
    }

    [Fact]
    public void Phase_two_keeps_global_turn_parity_and_reuses_shared_attacks()
    {
        var shepherd = new FallenShepherd { CurrentPhase = 2 };

        Assert.Equal(new[] { EnemyAttackId.FallenShepherdPhase2 }, shepherd.GetAttackIds(null, 7));

        var selected = shepherd.GetAttackIds(null, 8).ToList();
        Assert.Equal(3, selected.Count);
        Assert.Equal(3, selected.Distinct().Count());
        Assert.All(selected, attackId => Assert.Contains(attackId, Phase2SmallPool));
    }

    [Fact]
    public void Phase_three_selects_one_registered_attack()
    {
        var shepherd = new FallenShepherd { CurrentPhase = 3 };

        for (int turn = 1; turn <= 20; turn++)
        {
            EnemyAttackId attackId = Assert.Single(shepherd.GetAttackIds(null, turn));
            Assert.Contains(attackId, Phase3Pool);
            Assert.NotNull(EnemyAttackFactory.Create(attackId));
        }
    }

    [Fact]
    public void Every_fallen_shepherd_attack_is_registered_in_both_factory_paths()
    {
        var attackIds = Phase1SmallPool
            .Concat(Phase2SmallPool)
            .Concat(Phase3Pool)
            .Append(EnemyAttackId.FallenShepherdPhase1)
            .Append(EnemyAttackId.FallenShepherdPhase2)
            .Distinct()
            .ToList();
        var allAttacks = EnemyAttackFactory.GetAllAttacks();

        Assert.All(attackIds, attackId =>
        {
            Assert.NotNull(EnemyAttackFactory.Create(attackId));
            Assert.True(allAttacks.ContainsKey(attackId), $"Missing attack registration: {attackId.ToKey()}");
        });
    }

    [Fact]
    public void Cast_out_requires_at_least_one_blocker_and_makes_only_card_blockers_colorless()
    {
        var entityManager = new EntityManager();
        var card = entityManager.CreateEntity("Card");
        entityManager.AddComponent(card, new CardData());
        entityManager.AddComponent(card, new AssignedBlockCard { IsEquipment = false });
        var equipment = entityManager.CreateEntity("Equipment");
        entityManager.AddComponent(equipment, new CardData());
        entityManager.AddComponent(equipment, new AssignedBlockCard { IsEquipment = true });
        var attack = new FallenShepherdPhase1();
        MustBeBlockedEvent requirement = null;
        EventManager.Subscribe<MustBeBlockedEvent>(evt => requirement = evt);

        attack.OnAttackReveal(entityManager);
        attack.OnBlockProcessed(entityManager, card);
        attack.OnBlockProcessed(entityManager, card);
        attack.OnBlockProcessed(entityManager, equipment);

        Assert.NotNull(requirement);
        Assert.Equal(1, requirement.Threshold);
        Assert.Equal(MustBeBlockedSystem.MustBeBlockedByType.AtLeast, requirement.Type);
        Assert.True(card.HasComponent<Colorless>());
        Assert.False(equipment.HasComponent<Colorless>());
        Assert.Single(card.GetAllComponents().OfType<Colorless>());
    }

    [Fact]
    public void Break_faith_makes_only_card_blockers_brittle()
    {
        var entityManager = new EntityManager();
        var card = entityManager.CreateEntity("Card");
        entityManager.AddComponent(card, new CardData());
        var equipment = entityManager.CreateEntity("Equipment");
        entityManager.AddComponent(equipment, new CardData());
        entityManager.AddComponent(equipment, new AssignedBlockCard { IsEquipment = true });
        var attack = new FallenShepherdBreakFaith();

        attack.OnBlockProcessed(entityManager, card);
        attack.OnBlockProcessed(entityManager, card);
        attack.OnBlockProcessed(entityManager, equipment);

        Assert.True(card.HasComponent<Brittle>());
        Assert.False(equipment.HasComponent<Brittle>());
        Assert.Single(card.GetAllComponents().OfType<Brittle>());
    }

    [Fact]
    public void On_hit_attacks_publish_expected_passives_to_expected_targets()
    {
        var entityManager = CreateOwners(out var player, out var enemy);
        var applied = new List<ApplyPassiveEvent>();
        EventManager.Subscribe<ApplyPassiveEvent>(evt => applied.Add(evt));

        new FallenShepherdCrooksScar().OnAttackHit(entityManager);
        new FallenShepherdBloodletting().OnAttackHit(entityManager);
        new FallenShepherdShepherdsVigil().OnAttackHit(entityManager);
        new FallenShepherdHush().OnAttackHit(entityManager);

        Assert.Collection(applied,
            evt => AssertPassive(evt, player, AppliedPassiveType.Scar, 1),
            evt => AssertPassive(evt, player, AppliedPassiveType.Bleed, 3),
            evt => AssertPassive(evt, enemy, AppliedPassiveType.Guard, 3),
            evt => AssertPassive(evt, player, AppliedPassiveType.Silenced, 1));
    }

    [Fact]
    public void Phase_three_reveal_attacks_publish_expected_player_passives()
    {
        var entityManager = CreateOwners(out var player, out _);
        var applied = new List<ApplyPassiveEvent>();
        EventManager.Subscribe<ApplyPassiveEvent>(evt => applied.Add(evt));

        new FallenShepherdPurgeTheHeretic().OnAttackReveal(entityManager);
        new FallenShepherdFearTheShepherd().OnAttackReveal(entityManager);
        new FallenShepherdFinalSermon().OnAttackReveal(entityManager);

        Assert.Collection(applied,
            evt => AssertPassive(evt, player, AppliedPassiveType.Burn, 1),
            evt => AssertPassive(evt, player, AppliedPassiveType.Fear, 1),
            evt => AssertPassive(evt, player, AppliedPassiveType.Silenced, 1));
    }

    [Fact]
    public void Cow_the_flock_intimidates_one_card_on_reveal()
    {
        var entityManager = new EntityManager();
        IntimidateEvent intimidated = null;
        EventManager.Subscribe<IntimidateEvent>(evt => intimidated = evt);

        new FallenShepherdCowTheFlock().OnAttackReveal(entityManager);

        Assert.NotNull(intimidated);
        Assert.Equal(1, intimidated.Amount);
    }

    // [Theory]
    // [InlineData(5, 2)]
    // [InlineData(6, 0)]
    // public void Binding_sermon_uses_six_block_threshold(int assignedBlock, int expectedShackled)
    // {
    //     var attack = new BindingSermon();
    //     var entityManager = CreateThresholdCombat(attack, assignedBlock, includeHandCard: false, out _, out var player);
    //     int shackled = 0;
    //     EventManager.Subscribe<ApplyPassiveEvent>(evt =>
    //     {
    //         if (evt.Target == player && evt.Type == AppliedPassiveType.Shackled) shackled += evt.Delta;
    //     });
    // 
    //     ResolveThresholdAttack(entityManager);
    // 
    //     Assert.Equal(expectedShackled, shackled);
    // }

    [Theory]
    [InlineData(2)]
    public void Have_no_mercy_discards_selected_card_only_below_threshold(int assignedBlock)
    {
        var attack = new FallenShepherdPhase3();
        var entityManager = CreateThresholdCombat(attack, assignedBlock, includeHandCard: true, out var handCard, out _);
        _ = new MarkedForSpecificDiscardSystem(entityManager);
        var discardRequests = new List<CardMoveRequested>();
        EventManager.Subscribe<CardMoveRequested>(evt =>
        {
            if (evt.Reason == "DiscardSpecificCard") discardRequests.Add(evt);
        });

        attack.OnAttackReveal(entityManager);
        Assert.NotNull(handCard.GetComponent<MarkedForSpecificDiscard>());

        ResolveThresholdAttack(entityManager);

        int blockRequired = attack.BlockRequiredToPreventEffect!.Value;
        int expected = assignedBlock < blockRequired ? 1 : 0;
        Assert.Equal(expected, discardRequests.Count);
        if (expected == 1) Assert.Same(handCard, discardRequests[0].Card);
        Assert.False(handCard.HasComponent<MarkedForSpecificDiscard>());
    }

    [Fact]
    public void Have_no_mercy_discards_at_exactly_one_below_threshold()
    {
        var attack = new FallenShepherdPhase3();
        int blockRequired = attack.BlockRequiredToPreventEffect!.Value;
        var entityManager = CreateThresholdCombat(attack, blockRequired - 1, includeHandCard: true, out var handCard, out _);
        _ = new MarkedForSpecificDiscardSystem(entityManager);
        var discardRequests = new List<CardMoveRequested>();
        EventManager.Subscribe<CardMoveRequested>(evt =>
        {
            if (evt.Reason == "DiscardSpecificCard") discardRequests.Add(evt);
        });

        attack.OnAttackReveal(entityManager);
        ResolveThresholdAttack(entityManager);

        Assert.Single(discardRequests);
        Assert.Same(handCard, discardRequests[0].Card);
    }

    [Fact]
    public void Have_no_mercy_spares_card_at_threshold_block()
    {
        var attack = new FallenShepherdPhase3();
        int blockRequired = attack.BlockRequiredToPreventEffect!.Value;
        var entityManager = CreateThresholdCombat(attack, blockRequired, includeHandCard: true, out _, out _);
        _ = new MarkedForSpecificDiscardSystem(entityManager);
        int discardRequests = 0;
        EventManager.Subscribe<CardMoveRequested>(evt =>
        {
            if (evt.Reason == "DiscardSpecificCard") discardRequests++;
        });

        attack.OnAttackReveal(entityManager);
        ResolveThresholdAttack(entityManager);

        Assert.Equal(0, discardRequests);
    }

    [Fact]
    public void Have_no_mercy_handles_an_empty_hand()
    {
        var attack = new FallenShepherdPhase3();
        var entityManager = CreateThresholdCombat(attack, assignedBlock: 0, includeHandCard: false, out _, out _);
        _ = new MarkedForSpecificDiscardSystem(entityManager);
        int discardRequests = 0;
        EventManager.Subscribe<CardMoveRequested>(evt =>
        {
            if (evt.Reason == "DiscardSpecificCard") discardRequests++;
        });

        attack.OnAttackReveal(entityManager);
        ResolveThresholdAttack(entityManager);

        Assert.Equal(0, discardRequests);
        Assert.Empty(entityManager.GetEntitiesWithComponent<MarkedForSpecificDiscard>());
    }

    private static EntityManager CreateOwners(out Entity player, out Entity enemy)
    {
        var entityManager = new EntityManager();
        player = entityManager.CreateEntity("Player");
        entityManager.AddComponent(player, new Player());
        enemy = entityManager.CreateEntity("Enemy");
        return entityManager;
    }

    private static EntityManager CreateThresholdCombat(
        EnemyAttackBase attack,
        int assignedBlock,
        bool includeHandCard,
        out Entity handCard,
        out Entity player)
    {
        var entityManager = CreateOwners(out player, out var enemy);
        var deckEntity = entityManager.CreateEntity("Deck");
        var deck = new Deck();
        entityManager.AddComponent(deckEntity, deck);
        handCard = null;
        if (includeHandCard)
        {
            handCard = entityManager.CreateEntity("SelectedCard");
            entityManager.AddComponent(handCard, new CardData
            {
                Card = new CardBase { Name = "Selected Card" },
            });
            deck.Hand.Add(handCard);
        }

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
        var progressEntity = entityManager.CreateEntity("EnemyAttackProgress[1]");
        entityManager.AddComponent(progressEntity, new EnemyAttackProgress
        {
            Enemy = enemy,
            AttackId = attack.Id,
            AttackSequence = 1,
            AssignedBlockTotal = assignedBlock,
            BaseDamage = attack.Damage,
        });

        _ = new AttackResolutionSystem(entityManager);
        _ = new EnemyDamageManagerSystem(entityManager);
        return entityManager;
    }

    private static void ResolveThresholdAttack(EntityManager entityManager)
    {
        EventManager.Publish(new ResolveAttack());
        EventManager.Publish(new EnemyAttackImpactNow());
    }

    private static void AssertPassive(
        ApplyPassiveEvent evt,
        Entity target,
        AppliedPassiveType type,
        int amount)
    {
        Assert.Same(target, evt.Target);
        Assert.Equal(type, evt.Type);
        Assert.Equal(amount, evt.Delta);
    }
}
