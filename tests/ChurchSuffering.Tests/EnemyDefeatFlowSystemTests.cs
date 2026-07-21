using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Data.Loadouts;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Objects.Enemies;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Systems;
using System.Linq;
using Xunit;

namespace ChurchSuffering.Tests;

public class EnemyDefeatFlowSystemTests
{
	[Fact]
	public void Real_defeat_completion_keeps_portrait_suppressed()
	{
		EventManager.Clear();
		EventQueue.Clear();

		try
		{
			SaveCache.DeleteSaveFilesIfPresent();
			SaveCache.StartNewRun();
			var climb = SaveCache.GetClimbState();
			climb.encounterSlots[0].enemyId = "skeleton";
			SaveCache.SaveClimbState(climb);

			var world = BuildWorld(out var phaseState, out var enemy);
			var queued = world.EntityManager.GetEntity("QueuedEvents").GetComponent<QueuedEvents>();
			queued.IsClimbEncounter = true;
			queued.ClimbEncounterSlotId = climb.encounterSlots[0].id;
			_ = new EnemyDefeatFlowSystem(world.EntityManager, imageAssets: null);

			int enemyKilledCount = 0;
			int questRewardCount = 0;
			EventManager.Subscribe<EnemyKilledEvent>(_ => enemyKilledCount++);
			EventManager.Subscribe<ShowQuestRewardOverlay>(_ => questRewardCount++);

			EventManager.Publish(new BeginDefeatPresentationEvent { Enemy = enemy, IsPreview = false });

			Assert.True(enemy.HasComponent<SuppressPortraitRender>());
			Assert.False(phaseState.DefeatPresentationActive);
			Assert.Equal(1, enemyKilledCount);
			Assert.Equal(0, questRewardCount);

			CompleteVictoryAnimation();

			Assert.Equal(1, questRewardCount);
		}
		finally
		{
			EventManager.Clear();
			EventQueue.Clear();
		}
	}

	[Fact]
	public void Preview_defeat_completion_restores_portrait()
	{
		EventManager.Clear();
		EventQueue.Clear();

		try
		{
			var world = BuildWorld(out var phaseState, out var enemy);
			_ = new EnemyDefeatFlowSystem(world.EntityManager, imageAssets: null);

			EventManager.Publish(new BeginDefeatPresentationEvent { Enemy = enemy, IsPreview = true });

			Assert.False(enemy.HasComponent<SuppressPortraitRender>());
			Assert.False(phaseState.DefeatPresentationActive);
		}
		finally
		{
			EventManager.Clear();
			EventQueue.Clear();
		}
	}

	[Fact]
	public void Final_climb_encounter_victory_uses_run_victory_transition_without_reward()
	{
		EventManager.Clear();
		EventQueue.Clear();

		try
		{
			SaveCache.DeleteSaveFilesIfPresent();
			SaveCache.StartNewRun();
			var climb = SaveCache.GetClimbState();
			climb.startingWeaponId = "hammer";
			climb.penanceLevel = 24;
			SaveCache.SaveClimbState(climb);

			var world = BuildWorld(out var phaseState, out var enemy);
			var queued = world.EntityManager.GetEntity("QueuedEvents").GetComponent<QueuedEvents>();
			queued.IsClimbEncounter = true;
			queued.ClimbEncounterSlotId = "final";
			queued.Events[0].EventId = "fallen_shepherd";
			var enemyComponent = enemy.GetComponent<Enemy>();
			enemyComponent.Id = EnemyId.FallenShepherd;
			enemyComponent.Name = "Fallen Shepherd";
			enemyComponent.EnemyBase = EnemyFactory.Create(EnemyId.FallenShepherd);
			_ = new EnemyDefeatFlowSystem(world.EntityManager, imageAssets: null);

			ShowTransition transition = null;
			int rewardCount = 0;
			int musicCount = 0;
			ClimbCompletedEvent climbCompleted = null;
			EventManager.Subscribe<ShowTransition>(evt => transition = evt);
			EventManager.Subscribe<ShowQuestRewardOverlay>(_ => rewardCount++);
			EventManager.Subscribe<ClimbCompletedEvent>(evt => climbCompleted = evt);
			EventManager.Subscribe<ChangeMusicTrack>(evt =>
			{
				if (evt.Track == MusicTrack.QuestComplete) musicCount++;
			});

			EventManager.Publish(new BeginDefeatPresentationEvent { Enemy = enemy, IsPreview = false });

			Assert.False(phaseState.DefeatPresentationActive);
			Assert.Equal(0, rewardCount);
			Assert.Equal(0, musicCount);
			Assert.NotNull(climbCompleted);
			Assert.Equal("hammer", climbCompleted.StartingWeaponId);
			Assert.Equal(24, climbCompleted.PenanceLevel);
			Assert.Null(transition);

			CompleteVictoryAnimation();

			Assert.Equal(0, rewardCount);
			Assert.NotNull(transition);
			Assert.Equal(SceneId.WayStation, transition.Scene);
			Assert.True(transition.EndRunOnLoad);
		}
		finally
		{
			EventManager.Clear();
			EventQueue.Clear();
			SaveCache.DeleteSaveFilesIfPresent();
		}
	}

	[Fact]
	public void Final_time_climb_encounter_reward_dismisses_to_battle()
	{
		EventManager.Clear();
		EventQueue.Clear();

		try
		{
			SaveCache.DeleteSaveFilesIfPresent();
			SaveCache.StartNewRun();
			var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
			loadout.cards = new System.Collections.Generic.List<LoadoutCardEntry>
			{
				new() { entryId = "test_entry_0", cardKey = "smite|White", isStarter = true },
			};
			loadout.weaponId = "sword";
			loadout.medalIds = new System.Collections.Generic.List<string>();
			SaveCache.SaveLoadout(loadout);

			var climb = SaveCache.GetClimbState();
			climb.time = ClimbRuleService.BaseMaxTime - 1;
			climb.encounterSlots[0].timeCost = 1;
			SaveCache.SaveClimbState(climb);

			var world = new World();
			var queuedEntity = world.CreateEntity("QueuedEvents");
			var queued = new QueuedEvents
			{
				Events =
				{
					new QueuedEvent
					{
						EventId = "skeleton",
						EventType = QueuedEventType.Enemy,
					},
				},
				CurrentIndex = 0,
				IsClimbEncounter = true,
				ClimbEncounterSlotId = "encounter_0",
				LocationId = "climb",
			};
			world.AddComponent(queuedEntity, queued);

			var phaseEntity = world.CreateEntity("PhaseState");
			world.AddComponent(phaseEntity, new PhaseState
			{
				Main = MainPhase.PlayerTurn,
				Sub = SubPhase.Action,
				TurnNumber = 1,
			});

			var enemy = world.CreateEntity("Enemy");
			world.AddComponent(enemy, new Enemy { Id = EnemyId.Skeleton, Name = "Skeleton" });
			world.AddComponent(enemy, new Transform());

			_ = new EnemyDefeatFlowSystem(world.EntityManager, imageAssets: null);

			ShowQuestRewardOverlay reward = null;
			int musicCount = 0;
			EventManager.Subscribe<ShowQuestRewardOverlay>(evt => reward = evt);
			EventManager.Subscribe<ChangeMusicTrack>(evt =>
			{
				if (evt.Track == MusicTrack.QuestComplete) musicCount++;
			});

			EventManager.Publish(new BeginDefeatPresentationEvent { Enemy = enemy, IsPreview = false });

			Assert.Equal(1, musicCount);
			Assert.Null(reward);

			CompleteVictoryAnimation();

			Assert.NotNull(reward);
			Assert.True(reward.IsEncounterReward);
			Assert.Equal(SceneId.Battle, reward.DismissScene);
		}
		finally
		{
			EventManager.Clear();
			EventQueue.Clear();
		}
	}

	[Fact]
	public void Last_queue_battle_starts_quest_complete_music_at_zero_hp()
	{
		EventManager.Clear();
		EventQueue.Clear();

		try
		{
			var world = BuildWorld(out _, out var enemy);
			_ = new EnemyDefeatFlowSystem(world.EntityManager, imageAssets: null);

			int musicCount = 0;
			EventManager.Subscribe<ChangeMusicTrack>(evt =>
			{
				if (evt.Track == MusicTrack.QuestComplete) musicCount++;
			});

			EventManager.Publish(new BeginDefeatPresentationEvent { Enemy = enemy, IsPreview = false });

			Assert.Equal(1, musicCount);
		}
		finally
		{
			EventManager.Clear();
			EventQueue.Clear();
		}
	}

	[Fact]
	public void Mid_queue_battle_does_not_start_quest_complete_music_at_zero_hp()
	{
		EventManager.Clear();
		EventQueue.Clear();

		try
		{
			var world = BuildWorld(out _, out var enemy);
			var queued = world.EntityManager.GetEntity("QueuedEvents").GetComponent<QueuedEvents>();
			queued.Events.Add(new QueuedEvent { EventId = "skeleton" });
			queued.CurrentIndex = 0;

			_ = new EnemyDefeatFlowSystem(world.EntityManager, imageAssets: null);

			int musicCount = 0;
			EventManager.Subscribe<ChangeMusicTrack>(evt =>
			{
				if (evt.Track == MusicTrack.QuestComplete) musicCount++;
			});

			EventManager.Publish(new BeginDefeatPresentationEvent { Enemy = enemy, IsPreview = false });

			Assert.Equal(0, musicCount);
		}
		finally
		{
			EventManager.Clear();
			EventQueue.Clear();
		}
	}

	private static void CompleteVictoryAnimation()
	{
		EventManager.Publish(new VictoryAnimationCompleteEvent());
	}

	private static World BuildWorld(out PhaseState phaseState, out Entity enemy)
	{
		var world = new World();
		var phaseEntity = world.CreateEntity("PhaseState");
		phaseState = new PhaseState
		{
			Main = MainPhase.PlayerTurn,
			Sub = SubPhase.Action,
			TurnNumber = 1,
		};
		world.AddComponent(phaseEntity, phaseState);

		var queuedEntity = world.CreateEntity("QueuedEvents");
		var queued = new QueuedEvents
		{
			Events = { new QueuedEvent { EventId = "skeleton" } },
			CurrentIndex = 0,
		};
		world.AddComponent(queuedEntity, queued);

		enemy = world.CreateEntity("Enemy");
		world.AddComponent(enemy, new Enemy { Id = EnemyId.Skeleton, Name = "Skeleton" });
		world.AddComponent(enemy, new Transform());
		return world;
	}
}
