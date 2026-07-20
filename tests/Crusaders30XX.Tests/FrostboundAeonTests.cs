using System;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Objects.Enemies;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class FrostboundAeonTests : IDisposable
{
	public FrostboundAeonTests()
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
	public void Factory_registers_enemy_attacks_and_turn_boundary()
	{
		var enemy = Assert.IsType<FrostboundAeon>(EnemyFactory.Create(EnemyId.FrostboundAeon));

		Assert.Equal(32, enemy.HP);
		Assert.Equal("frostbound_aeon", enemy.Id.ToKey());
		Assert.Equal([EnemyAttackId.ChronoSlice], enemy.GetAttackIds(new EntityManager(), 1));
		Assert.Equal([EnemyAttackId.ChronoSlice], enemy.GetAttackIds(new EntityManager(), 5));
		Assert.Equal([EnemyAttackId.AeonWard], enemy.GetAttackIds(new EntityManager(), 6));
		Assert.Equal([EnemyAttackId.AeonWard], enemy.GetAttackIds(new EntityManager(), 7));
		Assert.Equal(10, Assert.IsType<ChronoSlice>(EnemyAttackFactory.Create(EnemyAttackId.ChronoSlice)).Damage);
		Assert.Equal(5, Assert.IsType<AeonWard>(EnemyAttackFactory.Create(EnemyAttackId.AeonWard)).Damage);
		Assert.True(GuardianAngelMessageService.HasEnemyAttackMessages(EnemyAttackId.ChronoSlice));
		Assert.True(GuardianAngelMessageService.HasEnemyAttackMessages(EnemyAttackId.AeonWard));
		Assert.Contains("frostbound_aeon", EnemyPortraitContent.GetClimbEncounterEnemyPool());
	}

	[Fact]
	public void Aeon_ward_gains_three_guard_on_reveal()
	{
		var entityManager = new EntityManager();
		var enemy = entityManager.CreateEntity("Enemy");
		ApplyPassiveEvent applied = null;
		EventManager.Subscribe<ApplyPassiveEvent>(evt => applied = evt);

		new AeonWard().OnAttackReveal(entityManager);

		Assert.NotNull(applied);
		Assert.Same(enemy, applied.Target);
		Assert.Equal(AppliedPassiveType.Guard, applied.Type);
		Assert.Equal(3, applied.Delta);
	}

	[Fact]
	public void Chrono_slice_marks_earliest_card_and_ignores_earlier_equipment()
	{
		var entityManager = new EntityManager();
		var equipment = AddBlocker(entityManager, "Equipment", assignedAt: 1, isEquipment: true);
		var earliestCard = AddBlocker(entityManager, "EarliestCard", assignedAt: 2);
		var laterCard = AddBlocker(entityManager, "LaterCard", assignedAt: 3);

		new ChronoSlice().OnBlocksConfirmed(entityManager);

		Assert.Null(equipment.GetComponent<AssignedBlockDestinationOverride>());
		Assert.Equal(CardZoneType.DrawPile, earliestCard.GetComponent<AssignedBlockDestinationOverride>()?.Destination);
		Assert.Null(laterCard.GetComponent<AssignedBlockDestinationOverride>());
	}

	[Fact]
	public void Chrono_slice_does_nothing_when_only_equipment_is_assigned()
	{
		var entityManager = new EntityManager();
		var equipment = AddBlocker(entityManager, "Equipment", assignedAt: 1, isEquipment: true);

		new ChronoSlice().OnBlocksConfirmed(entityManager);

		Assert.Null(equipment.GetComponent<AssignedBlockDestinationOverride>());
	}

	[Fact]
	public void Immediate_resolution_puts_redirected_card_on_deck_bottom_and_still_grants_resources()
	{
		var entityManager = new EntityManager();
		var player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		entityManager.AddComponent(player, new Courage());
		entityManager.AddComponent(player, new Temperance());
		var deckEntity = entityManager.CreateEntity("Deck");
		var deck = new Deck();
		entityManager.AddComponent(deckEntity, deck);
		var existingBottom = AddCard(entityManager, "ExistingBottom", CardData.CardColor.Black);
		deck.DrawPile.Add(existingBottom);
		var redirected = AddBlocker(entityManager, "Redirected", assignedAt: 1, color: CardData.CardColor.White);
		var discarded = AddBlocker(entityManager, "Discarded", assignedAt: 2, color: CardData.CardColor.Red);
		entityManager.AddComponent(redirected, new ExhaustOnBlock { Owner = redirected });
		_ = new CardZoneSystem(entityManager);
		_ = new CourageManagerSystem(entityManager);
		_ = new TemperanceManagerSystem(entityManager);

		new ChronoSlice().OnBlocksConfirmed(entityManager);
		QueuedDiscardAssignedBlocksEvent.ResolveImmediately(entityManager, discardSpentBlocks: true);

		Assert.Equal([existingBottom, redirected], deck.DrawPile);
		Assert.Contains(discarded, deck.DiscardPile);
		Assert.DoesNotContain(redirected, deck.ExhaustPile);
		Assert.Equal(1, player.GetComponent<Courage>().Amount);
		Assert.Equal(1, player.GetComponent<Temperance>().Amount);
		Assert.False(redirected.HasComponent<AssignedBlockDestinationOverride>());
		Assert.False(redirected.HasComponent<ExhaustOnBlock>());
	}

	[Fact]
	public void Animated_resolution_targets_draw_pile_root_for_redirected_card()
	{
		var entityManager = new EntityManager();
		var phase = entityManager.CreateEntity("PhaseState");
		entityManager.AddComponent(phase, new PhaseState { Sub = SubPhase.EnemyAttack });
		var deckEntity = entityManager.CreateEntity("Deck");
		var deck = new Deck();
		entityManager.AddComponent(deckEntity, deck);
		var enemy = entityManager.CreateEntity("Enemy");
		entityManager.AddComponent(enemy, new AttackIntent
		{
			ActiveAttackSequence = 1,
			Planned =
			[
				new PlannedAttack
				{
					AttackId = EnemyAttackId.ChronoSlice,
					AttackDefinition = new ChronoSlice(),
				},
			],
		});
		var drawPilePosition = new Vector2(1700f, 900f);
		var drawPileRoot = entityManager.CreateEntity("UI_DrawPileRoot");
		entityManager.AddComponent(drawPileRoot, new Transform { Position = drawPilePosition });
		var blocker = AddBlocker(entityManager, "Redirected", assignedAt: 1);
		entityManager.AddComponent(blocker, new Transform { Position = new Vector2(900f, 500f) });
		entityManager.AddComponent(blocker, new AssignedBlockPresentation { RenderPos = new Vector2(900f, 500f) });
		entityManager.AddComponent(blocker, new AssignedBlockDestinationOverride
		{
			Owner = blocker,
			Destination = CardZoneType.DrawPile,
		});
		_ = new CardZoneSystem(entityManager);
		var system = new AssignedBlocksToDiscardSystem(entityManager, null)
		{
			StartDelayBetweenCardsSeconds = 0f,
			FlightDurationSeconds = 0.05f,
		};

		EventManager.Publish(new DebugCommandEvent { Command = "AnimateAssignedBlocksToDiscard" });

		var flight = blocker.GetComponent<CardToDiscardFlight>();
		Assert.NotNull(flight);
		Assert.Equal(CardZoneType.DrawPile, flight.Destination);
		Assert.Equal(drawPilePosition, flight.TargetPos);

		system.Update(new GameTime(TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(0.1)));
		system.Update(new GameTime(TimeSpan.FromSeconds(0.2), TimeSpan.FromSeconds(0.1)));

		Assert.Contains(blocker, deck.DrawPile);
		Assert.False(blocker.HasComponent<CardToDiscardFlight>());
		Assert.False(blocker.HasComponent<AssignedBlockDestinationOverride>());
	}

	[Fact]
	public void Animated_resolution_preserves_distinct_flight_positions_through_presentation()
	{
		var entityManager = new EntityManager();
		var phase = entityManager.CreateEntity("PhaseState");
		entityManager.AddComponent(phase, new PhaseState { Sub = SubPhase.EnemyAttack });
		var deckEntity = entityManager.CreateEntity("Deck");
		var deck = new Deck();
		entityManager.AddComponent(deckEntity, deck);
		var enemy = entityManager.CreateEntity("Enemy");
		entityManager.AddComponent(enemy, new AttackIntent
		{
			ActiveAttackSequence = 1,
			Planned =
			[
				new PlannedAttack
				{
					AttackId = EnemyAttackId.ChronoSlice,
					AttackDefinition = new ChronoSlice(),
				},
			],
		});

		var drawPilePosition = new Vector2(1700f, 900f);
		var discardPilePosition = new Vector2(120f, 900f);
		var drawPileRoot = entityManager.CreateEntity("UI_DrawPileRoot");
		entityManager.AddComponent(drawPileRoot, new Transform { Position = drawPilePosition });
		var discardPileRoot = entityManager.CreateEntity("UI_DiscardPileRoot");
		entityManager.AddComponent(discardPileRoot, new Transform { Position = discardPilePosition });

		var anchor = entityManager.CreateEntity("EnemyAttackBannerAnchor");
		entityManager.AddComponent(anchor, new EnemyAttackBannerAnchor());
		entityManager.AddComponent(anchor, new Transform { Position = new Vector2(1000f, 400f) });
		entityManager.AddComponent(anchor, new EnemyAttackBannerPresentation
		{
			LogicalWidth = 620,
			RenderBounds = new Rectangle(690, 400, 620, 220),
		});
		entityManager.AddComponent(anchor, new AssignedBlockRailPresentation
		{
			LogicalAnchorPos = new Vector2(900f, 350f),
		});

		var redirected = AddPresentedBlocker(entityManager, "Redirected", 1, new Vector2(860f, 500f));
		var discarded = AddPresentedBlocker(entityManager, "Discarded", 2, new Vector2(1060f, 500f));
		_ = new CardZoneSystem(entityManager);
		var flightSystem = new AssignedBlocksToDiscardSystem(entityManager, null)
		{
			StartDelayBetweenCardsSeconds = 0f,
			FlightDurationSeconds = 1f,
			ArcHeightPx = 0,
		};
		var railAnimationSystem = new AssignedBlockAnimationSystem(entityManager);
		var lateLayoutSystem = new AssignedBlockLateLayoutSystem(entityManager);

		new ChronoSlice().OnBlocksConfirmed(entityManager);
		EventManager.Publish(new DebugCommandEvent { Command = "AnimateAssignedBlocksToDiscard" });

		var redirectedFlight = Assert.IsType<CardToDiscardFlight>(redirected.GetComponent<CardToDiscardFlight>());
		var discardedFlight = Assert.IsType<CardToDiscardFlight>(discarded.GetComponent<CardToDiscardFlight>());
		Assert.Equal(CardZoneType.DrawPile, redirectedFlight.Destination);
		Assert.Equal(drawPilePosition, redirectedFlight.TargetPos);
		Assert.Equal(CardZoneType.DiscardPile, discardedFlight.Destination);
		Assert.Equal(discardPilePosition, discardedFlight.TargetPos);

		redirectedFlight.Started = true;
		discardedFlight.Started = true;
		var quarterSecond = new GameTime(TimeSpan.FromSeconds(0.25), TimeSpan.FromSeconds(0.25));
		flightSystem.Update(quarterSecond);
		var redirectedFlightPosition = redirected.GetComponent<Transform>().Position;
		var discardedFlightPosition = discarded.GetComponent<Transform>().Position;

		railAnimationSystem.Update(quarterSecond);
		lateLayoutSystem.Update(quarterSecond);

		Assert.Equal(redirectedFlightPosition, redirected.GetComponent<AssignedBlockPresentation>().CurrentPos);
		Assert.Equal(redirectedFlightPosition, redirected.GetComponent<AssignedBlockPresentation>().RenderPos);
		Assert.Equal(discardedFlightPosition, discarded.GetComponent<AssignedBlockPresentation>().CurrentPos);
		Assert.Equal(discardedFlightPosition, discarded.GetComponent<AssignedBlockPresentation>().RenderPos);
		Assert.False(redirected.GetComponent<UIElement>().IsInteractable);
		Assert.False(discarded.GetComponent<UIElement>().IsInteractable);

		flightSystem.Update(new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(0.75)));

		Assert.Equal([redirected], deck.DrawPile);
		Assert.Equal([discarded], deck.DiscardPile);
	}

	[Fact]
	public void Card_blocked_event_runs_current_attack_block_processing()
	{
		var entityManager = new EntityManager();
		var attack = new BlockProcessingAttack();
		var enemy = entityManager.CreateEntity("Enemy");
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
		var blocker = AddCard(entityManager, "Blocker", CardData.CardColor.Black);
		_ = new CardZoneSystem(entityManager);

		EventManager.Publish(new CardBlockedEvent { Card = blocker });

		Assert.Equal(1, attack.ProcessedBlockCount);
	}

	[Fact]
	public void Resolver_confirms_blocks_before_queuing_cleanup()
	{
		var entityManager = new EntityManager();
		var phase = entityManager.CreateEntity("PhaseState");
		entityManager.AddComponent(phase, new PhaseState { Sub = SubPhase.Block });
		var enemy = entityManager.CreateEntity("Enemy");
		entityManager.AddComponent(enemy, new AttackIntent
		{
			ActiveAttackSequence = 1,
			Planned =
			[
				new PlannedAttack
				{
					AttackId = EnemyAttackId.ChronoSlice,
					AttackDefinition = new ChronoSlice(),
				},
			],
		});
		var blocker = AddBlocker(entityManager, "Blocker", assignedAt: 1);
		var resolver = new EnemyAttackResolver(entityManager, new ImmediateAttackPresentationGate());

		resolver.ResolveCurrentAttack();

		Assert.Equal(CardZoneType.DrawPile, blocker.GetComponent<AssignedBlockDestinationOverride>()?.Destination);
	}

	private static Entity AddBlocker(
		EntityManager entityManager,
		string name,
		long assignedAt,
		bool isEquipment = false,
		CardData.CardColor color = CardData.CardColor.Black)
	{
		var entity = isEquipment
			? entityManager.CreateEntity(name)
			: AddCard(entityManager, name, color);
		entityManager.AddComponent(entity, new AssignedBlockCard
		{
			AssignedAtTicks = assignedAt,
			IsEquipment = isEquipment,
		});
		return entity;
	}

	private static Entity AddPresentedBlocker(
		EntityManager entityManager,
		string name,
		long assignedAt,
		Vector2 position)
	{
		var entity = AddBlocker(entityManager, name, assignedAt);
		entityManager.AddComponent(entity, new Transform
		{
			Position = position,
			Scale = new Vector2(0.24f),
		});
		entityManager.AddComponent(entity, new AssignedBlockPresentation
		{
			StartPos = position,
			CurrentPos = position,
			TargetPos = position,
			RenderPos = position,
			StartScale = 0.24f,
			CurrentScale = 0.24f,
			Phase = AssignedBlockPresentation.PhaseState.Idle,
		});
		entityManager.AddComponent(entity, new UIElement { IsInteractable = true });
		return entity;
	}

	private static Entity AddCard(EntityManager entityManager, string name, CardData.CardColor color)
	{
		var entity = entityManager.CreateEntity(name);
		entityManager.AddComponent(entity, new CardData
		{
			Card = new CardBase { CardId = name.ToLowerInvariant(), Name = name },
			Color = color,
		});
		return entity;
	}

	private sealed class BlockProcessingAttack : EnemyAttackBase
	{
		public int ProcessedBlockCount { get; private set; }

		public BlockProcessingAttack()
		{
			Id = EnemyAttackId.ChronoSlice;
			OnBlockProcessed = (_, _) => ProcessedBlockCount++;
		}
	}
}
