using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class EnemyAttackResolverTests : IDisposable
{
	public EnemyAttackResolverTests()
	{
		EventManager.Clear();
		EventQueue.Clear();
	}

	public void Dispose()
	{
		EventManager.Clear();
		EventQueue.Clear();
	}

	[Fact]
	public void Unblocked_attack_applies_damage_and_resolves_once()
	{
		var attack = new TrackingAttack(damage: 5);
		var entityManager = CreateSingleAttackCombat(attack, assignedBlock: 0, out var player);
		RegisterCombatSystems(entityManager);
		int resolvedCount = 0;
		AttackResolved resolved = null;
		EventManager.Subscribe<AttackResolved>(evt =>
		{
			resolved = evt;
			resolvedCount++;
		});

		ResolveAndPump(entityManager);

		Assert.Equal(25, player.GetComponent<HP>().Current);
		Assert.Equal(1, attack.OnHitCount);
		Assert.Equal(1, resolvedCount);
		Assert.NotNull(resolved);
		Assert.False(resolved.WasConditionMet);
	}

	[Fact]
	public void Fully_blocked_attack_preserves_hp_and_does_not_run_on_hit()
	{
		var attack = new TrackingAttack(damage: 5);
		var entityManager = CreateSingleAttackCombat(attack, assignedBlock: 5, out var player);
		RegisterCombatSystems(entityManager);
		AttackResolved resolved = null;
		EventManager.Subscribe<AttackResolved>(evt => resolved = evt);

		ResolveAndPump(entityManager);

		Assert.Equal(30, player.GetComponent<HP>().Current);
		Assert.Equal(0, attack.OnHitCount);
		Assert.NotNull(resolved);
		Assert.True(resolved.WasConditionMet);
	}

	[Fact]
	public void Threshold_effect_runs_for_insufficient_block_and_positive_final_damage()
	{
		var attack = new TrackingAttack(damage: 5, blockRequired: 3);
		var entityManager = CreateSingleAttackCombat(attack, assignedBlock: 2, out var player);
		RegisterCombatSystems(entityManager);

		ResolveAndPump(entityManager);

		Assert.Equal(27, player.GetComponent<HP>().Current);
		Assert.Equal(1, attack.ThresholdCount);
	}

	[Fact]
	public void Immediate_discard_runs_card_block_effects_in_the_resolver_pipeline()
	{
		var attack = new TrackingAttack(damage: 5);
		var entityManager = CreateSingleAttackCombat(attack, assignedBlock: 2, out _);
		var deckEntity = entityManager.CreateEntity("Deck");
		var deck = new Deck();
		entityManager.AddComponent(deckEntity, deck);
		var cardDefinition = new BlockTrackingCard();
		var blocker = entityManager.CreateEntity("AssignedBlocker");
		entityManager.AddComponent(blocker, new CardData
		{
			Card = cardDefinition,
			Color = CardData.CardColor.Red
		});
		entityManager.AddComponent(blocker, new AssignedBlockCard
		{
			BlockAmount = 2,
			AssignedAtTicks = 1
		});
		entityManager.AddComponent(blocker, new AssignedBlockPresentation
		{
			Phase = AssignedBlockPresentation.PhaseState.Idle
		});
		_ = new CardZoneSystem(entityManager);
		RegisterCombatSystems(entityManager);
		int blockedEvents = 0;
		EventManager.Subscribe<CardBlockedEvent>(_ => blockedEvents++);

		ResolveAndPump(entityManager);

		Assert.Equal(1, cardDefinition.OnBlockCount);
		Assert.Equal(1, blockedEvents);
		Assert.Contains(blocker, deck.DiscardPile);
		Assert.False(blocker.HasComponent<AssignedBlockCard>());
	}

	[Fact]
	public void Resolve_step_always_precedes_gameplay_impact()
	{
		var attack = new TrackingAttack(damage: 5);
		var entityManager = CreateSingleAttackCombat(attack, assignedBlock: 0, out _);
		RegisterCombatSystems(entityManager);
		var observed = new List<string>();
		EventManager.Subscribe<ResolveAttack>(_ => observed.Add("resolve"));
		EventManager.Subscribe<EnemyAttackImpactNow>(_ => observed.Add("impact"));

		ResolveAndPump(entityManager);

		Assert.Equal(["resolve", "impact"], observed);
	}

	[Fact]
	public void Clearing_queue_during_impact_cancels_attack_advancement()
	{
		var attack = new TrackingAttack(damage: 5);
		var entityManager = CreateSingleAttackCombat(attack, assignedBlock: 0, out _);
		RegisterCombatSystems(entityManager);
		EventManager.Subscribe<EnemyAttackImpactNow>(_ => EventQueue.Clear(), priority: 100);

		ResolveAndPump(entityManager);

		var intent = entityManager.GetEntitiesWithComponent<AttackIntent>()
			.Single()
			.GetComponent<AttackIntent>();
		Assert.Single(intent.Planned);
		Assert.Equal(1, intent.ActiveAttackSequence);
		Assert.True(EventQueue.IsIdle);
	}

	[Fact]
	public void Graphics_gate_builds_visual_starts_followed_by_the_driving_impact_wait()
	{
		var attack = new TrackingAttack(damage: 5);
		var entityManager = CreateSingleAttackCombat(attack, assignedBlock: 0, out _);
		var enemy = entityManager.GetEntitiesWithComponent<AttackIntent>().Single();
		var gate = new GraphicsAttackPresentationGate();

		var steps = gate.BuildImpactSteps(entityManager, enemy, attack, attackSequence: 1);

		Assert.NotEmpty(steps);
		Assert.All(steps.Take(steps.Count - 1), step => Assert.IsType<QueuedStartVisualEffect>(step));
		Assert.IsType<QueuedWaitVisualEffectImpact>(steps[^1]);
	}

	[Fact]
	public void Graphics_gate_falls_back_to_an_immediate_gameplay_impact()
	{
		var entityManager = new EntityManager();
		var gate = new GraphicsAttackPresentationGate();
		int impacts = 0;
		EventManager.Subscribe<EnemyAttackImpactNow>(_ => impacts++);

		var steps = gate.BuildImpactSteps(entityManager, enemy: null, attack: null, attackSequence: -1);
		var fallback = Assert.IsType<EventQueueBridge.QueuedPublish<EnemyAttackImpactNow>>(Assert.Single(steps));
		fallback.StartResolving();

		Assert.Equal(1, impacts);
		Assert.Equal(EventQueue.EventState.Complete, fallback.State);
	}

	private static EntityManager CreateSingleAttackCombat(
		TrackingAttack attack,
		int assignedBlock,
		out Entity player)
	{
		var entityManager = new EntityManager();
		var phaseEntity = entityManager.CreateEntity("PhaseState");
		entityManager.AddComponent(phaseEntity, new PhaseState
		{
			Main = MainPhase.EnemyTurn,
			Sub = SubPhase.Block,
			TurnNumber = 1
		});

		player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		entityManager.AddComponent(player, new HP { Max = 30, Current = 30 });
		entityManager.AddComponent(player, new AppliedPassives());

		var enemy = entityManager.CreateEntity("Enemy");
		entityManager.AddComponent(enemy, new Enemy());
		entityManager.AddComponent(enemy, new AttackIntent
		{
			ActiveAttackSequence = 1,
			Planned =
			[
				new PlannedAttack
				{
					AttackId = attack.Id,
					AttackDefinition = attack
				}
			]
		});

		var progressEntity = entityManager.CreateEntity("EnemyAttackProgress[1]");
		entityManager.AddComponent(progressEntity, new EnemyAttackProgress
		{
			Enemy = enemy,
			AttackId = attack.Id,
			AttackSequence = 1,
			AssignedBlockTotal = assignedBlock,
			BaseDamage = attack.Damage
		});
		return entityManager;
	}

	private static void RegisterCombatSystems(EntityManager entityManager)
	{
		_ = new AttackResolutionSystem(entityManager);
		_ = new EnemyDamageManagerSystem(entityManager);
		_ = new HpManagementSystem(entityManager);
		_ = new PhaseCoordinatorSystem(entityManager);
	}

	private static void ResolveAndPump(EntityManager entityManager)
	{
		var resolver = new EnemyAttackResolver(entityManager, new ImmediateAttackPresentationGate());
		resolver.ResolveCurrentAttack();
		for (int i = 0; i < 100 && !EventQueue.IsIdle; i++)
		{
			EventQueue.Update(1f);
		}
		Assert.True(EventQueue.IsIdle);
	}

	private sealed class TrackingAttack : EnemyAttackBase
	{
		public int OnHitCount { get; private set; }
		public int ThresholdCount { get; private set; }

		public TrackingAttack(int damage, int? blockRequired = null)
		{
			Id = EnemyAttackId.Cinderbolt;
			Name = "Resolver Test Attack";
			Damage = damage;
			ConditionType = ConditionType.OnHit;
			BlockRequiredToPreventEffect = blockRequired;
			OnAttackHit = _ => OnHitCount++;
			OnDamageThresholdMet = _ => ThresholdCount++;
		}
	}

	private sealed class BlockTrackingCard : CardBase
	{
		public int OnBlockCount { get; private set; }

		public BlockTrackingCard()
		{
			CardId = "resolver_test_blocker";
			Name = "Resolver Test Blocker";
			OnBlock = (_, _) => OnBlockCount++;
		}
	}
}
