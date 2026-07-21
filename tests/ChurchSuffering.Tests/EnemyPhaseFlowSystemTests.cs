using System;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Dialog;
using ChurchSuffering.ECS.Data.VisualEffects;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.Enemies;
using ChurchSuffering.ECS.Systems;
using Xunit;

namespace ChurchSuffering.Tests;

public class EnemyPhaseFlowSystemTests
{
	[Fact]
	public void Correlated_dialogue_controls_intermediate_resets_and_final_defeat()
	{
		EventManager.Clear();
		EventQueue.Clear();
		TimerScheduler.Clear();
		DialogDefinitionCache.Reload();

		try
		{
			var world = BuildWorld(out var phaseState, out var enemy, out var definition);
			_ = new EnemyPhaseFlowSystem(world.EntityManager);

			DialogueSequenceRequested request = null;
			int resetCount = 0;
			int defeatPresentationCount = 0;
			int enemyKilledCount = 0;
			int rewardCount = 0;
			int startBattleCount = 0;
			EventManager.Subscribe<DialogueSequenceRequested>(evt => request = evt);
			EventManager.Subscribe<EnemyPhaseResetEvent>(_ => resetCount++);
			EventManager.Subscribe<BeginDefeatPresentationEvent>(_ => defeatPresentationCount++);
			EventManager.Subscribe<EnemyKilledEvent>(_ => enemyKilledCount++);
			EventManager.Subscribe<ShowQuestRewardOverlay>(_ => rewardCount++);
			EventManager.Subscribe<StartBattleRequested>(_ => startBattleCount++);

			EventManager.Publish(new EnemyPhaseLethalEvent { Enemy = enemy });

			Assert.Equal("phase_1_end", request.SegmentId);
			Assert.Equal(1, definition.CurrentPhase);
			Assert.True(phaseState.DefeatPresentationActive);

			EventManager.Publish(new DialogueSequenceCompleted
			{
				DefinitionId = request.DefinitionId,
				SegmentId = request.SegmentId,
				RequestId = Guid.NewGuid(),
			});
			Assert.Equal(1, definition.CurrentPhase);

			Complete(request);
			Assert.Equal(2, definition.CurrentPhase);
			Assert.Equal(1, resetCount);
			Assert.False(phaseState.DefeatPresentationActive);
			Assert.Equal(9, phaseState.TurnNumber);

			request = null;
			EventManager.Publish(new EnemyPhaseLethalEvent { Enemy = enemy });
			Assert.Equal("phase_2_end", request.SegmentId);
			Complete(request);
			Assert.Equal(3, definition.CurrentPhase);
			Assert.Equal(2, resetCount);

			request = null;
			EventManager.Publish(new EnemyPhaseLethalEvent { Enemy = enemy });
			Assert.Equal("victory", request.SegmentId);
			Complete(request);

			Assert.Equal(0, defeatPresentationCount);
			Assert.True(phaseState.DefeatPresentationActive);
			TimerScheduler.Update(0.11f);
			Assert.Equal(1, defeatPresentationCount);
			Assert.Equal(0, enemyKilledCount);
			Assert.Equal(0, rewardCount);
			Assert.Equal(0, startBattleCount);
		}
		finally
		{
			EventManager.Clear();
			EventQueue.Clear();
			TimerScheduler.Clear();
		}
	}

	[Fact]
	public void Phase_dialogue_waits_for_active_card_visual_effect_completion()
	{
		EventManager.Clear();
		EventQueue.Clear();
		TimerScheduler.Clear();
		DialogDefinitionCache.Reload();

		try
		{
			var world = BuildWorld(out var phaseState, out var enemy, out var definition);
			_ = new EnemyPhaseFlowSystem(world.EntityManager);
			var player = world.EntityManager.GetEntity("Player");
			var effectId = Guid.NewGuid();
			var effectEntity = world.CreateEntity("ActiveCardEffect");
			world.AddComponent(effectEntity, new ActiveVisualEffect
			{
				RequestId = effectId,
				Source = player,
				Target = enemy,
				SourceKind = VisualEffectSourceKind.Card,
				IsPreview = false,
			});

			DialogueSequenceRequested request = null;
			EventManager.Subscribe<DialogueSequenceRequested>(evt => request = evt);

			EventManager.Publish(new EnemyPhaseLethalEvent { Enemy = enemy });

			Assert.Null(request);
			Assert.True(phaseState.DefeatPresentationActive);
			Assert.Equal(1, definition.CurrentPhase);

			EventManager.Publish(new VisualEffectCompleted { RequestId = Guid.NewGuid(), IsPreview = false });
			Assert.Null(request);

			EventManager.Publish(new VisualEffectCompleted { RequestId = effectId, IsPreview = false });
			Assert.NotNull(request);
			Assert.Equal("phase_1_end", request.SegmentId);
		}
		finally
		{
			EventManager.Clear();
			EventQueue.Clear();
			TimerScheduler.Clear();
		}
	}

	[Fact]
	public void Phase_dialogue_waits_for_damage_number_presentation()
	{
		EventManager.Clear();
		EventQueue.Clear();
		TimerScheduler.Clear();
		DialogDefinitionCache.Reload();

		try
		{
			var world = BuildWorld(out _, out var enemy, out _);
			_ = new EnemyPhaseFlowSystem(world.EntityManager);
			var presentationId = Guid.NewGuid();
			DialogueSequenceRequested request = null;
			EventManager.Subscribe<DialogueSequenceRequested>(evt => request = evt);

			EventManager.Publish(new BattlePresentationStarted
			{
				PresentationId = presentationId,
				Target = enemy,
				Kind = BattlePresentationKind.DamageNumber,
			});

			EventManager.Publish(new EnemyPhaseLethalEvent
			{
				Enemy = enemy,
				DamagePresentationId = presentationId,
				DamageType = ModifyTypeEnum.Effect,
			});

			Assert.Null(request);

			EventManager.Publish(new BattlePresentationCompleted
			{
				PresentationId = Guid.NewGuid(),
				Target = enemy,
				Kind = BattlePresentationKind.DamageNumber,
			});
			Assert.Null(request);

			EventManager.Publish(new BattlePresentationCompleted
			{
				PresentationId = presentationId,
				Target = enemy,
				Kind = BattlePresentationKind.DamageNumber,
			});
			Assert.NotNull(request);
			Assert.Equal("phase_1_end", request.SegmentId);
		}
		finally
		{
			EventManager.Clear();
			EventQueue.Clear();
			TimerScheduler.Clear();
		}
	}

	[Fact]
	public void Final_phase_victory_dialogue_waits_for_damage_presentation_before_defeat_presentation()
	{
		EventManager.Clear();
		EventQueue.Clear();
		TimerScheduler.Clear();
		DialogDefinitionCache.Reload();

		try
		{
			var world = BuildWorld(out _, out var enemy, out var definition);
			definition.CurrentPhase = 3;
			_ = new EnemyPhaseFlowSystem(world.EntityManager);
			var presentationId = Guid.NewGuid();
			DialogueSequenceRequested request = null;
			int defeatPresentationCount = 0;
			EventManager.Subscribe<DialogueSequenceRequested>(evt => request = evt);
			EventManager.Subscribe<BeginDefeatPresentationEvent>(_ => defeatPresentationCount++);

			EventManager.Publish(new BattlePresentationStarted
			{
				PresentationId = presentationId,
				Target = enemy,
				Kind = BattlePresentationKind.DamageNumber,
			});
			EventManager.Publish(new EnemyPhaseLethalEvent
			{
				Enemy = enemy,
				DamagePresentationId = presentationId,
				DamageType = ModifyTypeEnum.Effect,
			});

			Assert.Null(request);
			Assert.Equal(0, defeatPresentationCount);

			EventManager.Publish(new BattlePresentationCompleted
			{
				PresentationId = presentationId,
				Target = enemy,
				Kind = BattlePresentationKind.DamageNumber,
			});

			Assert.NotNull(request);
			Assert.Equal("victory", request.SegmentId);
			Complete(request);
			Assert.Equal(0, defeatPresentationCount);
			TimerScheduler.Update(0.11f);
			Assert.Equal(1, defeatPresentationCount);
		}
		finally
		{
			EventManager.Clear();
			EventQueue.Clear();
			TimerScheduler.Clear();
		}
	}

	private static void Complete(DialogueSequenceRequested request)
	{
		EventManager.Publish(new DialogueSequenceCompleted
		{
			DefinitionId = request.DefinitionId,
			SegmentId = request.SegmentId,
			RequestId = request.RequestId,
		});
	}

	private static World BuildWorld(
		out PhaseState phaseState,
		out Entity enemy,
		out FallenShepherd definition)
	{
		var world = new World();
		var phaseEntity = world.CreateEntity("PhaseState");
		phaseState = new PhaseState
		{
			Main = MainPhase.PlayerTurn,
			Sub = SubPhase.Action,
			TurnNumber = 9,
		};
		world.AddComponent(phaseEntity, phaseState);

		var player = world.CreateEntity("Player");
		world.AddComponent(player, new Player());
		world.AddComponent(player, new HP { Max = 25, Current = 25 });
		world.AddComponent(player, new AppliedPassives());
		world.AddComponent(player, new ActionPoints());

		var deckEntity = world.CreateEntity("Deck");
		var deck = new Deck();
		world.AddComponent(deckEntity, deck);
		var card = world.CreateEntity("Card");
		world.AddComponent(card, new CardData());
		deck.Cards.Add(card);
		deck.DrawPile.Add(card);

		definition = new FallenShepherd
		{
			MaxHealth = 30,
			CurrentHealth = 0,
		};
		enemy = world.CreateEntity("Enemy");
		world.AddComponent(enemy, new Enemy
		{
			Id = definition.Id,
			Name = definition.Name,
			MaxHealth = 30,
			CurrentHealth = 0,
			EnemyBase = definition,
		});
		world.AddComponent(enemy, new HP { Max = 30, Current = 0 });
		world.AddComponent(enemy, new EnemyArsenal());
		world.AddComponent(enemy, new AttackIntent());
		world.AddComponent(enemy, new NextTurnAttackIntent());
		world.AddComponent(enemy, new AppliedPassives());
		return world;
	}
}
