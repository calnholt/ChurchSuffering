using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using ChurchSuffering.ECS.Systems;
using Xunit;

namespace ChurchSuffering.Tests;

public class TestFightFlowSystemTests
{
	[Fact]
	public void Player_death_decrements_hp_and_requests_same_battle()
	{
		EventManager.Clear();
		EventQueue.Clear();
		TestFightRuntime.Configure(Options());
		TestFightRuntime.ApplyHpDelta(10);

		try
		{
			var world = new World();
			var queuedEntity = world.CreateEntity("QueuedEvents");
			var queued = new QueuedEvents
			{
				CurrentIndex = 0,
				Events = { new QueuedEvent { EventId = "skeleton" } },
			};
			world.AddComponent(queuedEntity, queued);
			_ = new TestFightFlowSystem(world.EntityManager);

			ShowTransition transition = null;
			EventManager.Subscribe<ShowTransition>(evt => transition = evt);

			EventManager.Publish(new PlayerDied());

			Assert.Equal(-1, TestFightRuntime.HpDelta);
			Assert.Equal(-1, queued.CurrentIndex);
			Assert.Equal(SceneId.Battle, transition.Scene);
			Assert.False(transition.SkipWipe);
		}
		finally
		{
			EventManager.Clear();
			EventQueue.Clear();
			TestFightRuntime.Reset();
		}
	}

	[Fact]
	public void Enemy_defeat_increments_hp_and_skips_rewards()
	{
		EventManager.Clear();
		EventQueue.Clear();
		TestFightRuntime.Configure(Options());
		TestFightRuntime.ApplyHpDelta(10);

		try
		{
			var world = new World();
			var phaseEntity = world.CreateEntity("PhaseState");
			world.AddComponent(phaseEntity, new PhaseState
			{
				Main = MainPhase.PlayerTurn,
				Sub = SubPhase.Action,
			});
			var queuedEntity = world.CreateEntity("QueuedEvents");
			var queued = new QueuedEvents
			{
				CurrentIndex = 0,
				Events = { new QueuedEvent { EventId = "skeleton" } },
			};
			world.AddComponent(queuedEntity, queued);

			var enemy = world.CreateEntity("Enemy");
			world.AddComponent(enemy, new Enemy
			{
				Id = EnemyId.Skeleton,
				EnemyBase = new Skeleton(),
			});
			world.AddComponent(enemy, new Transform());

			_ = new EnemyDefeatFlowSystem(world.EntityManager, imageAssets: null);

			int rewardCount = 0;
			ShowTransition transition = null;
			EventManager.Subscribe<ShowQuestRewardOverlay>(_ => rewardCount++);
			EventManager.Subscribe<ShowTransition>(evt => transition = evt);

			EventManager.Publish(new BeginDefeatPresentationEvent
			{
				Enemy = enemy,
				IsPreview = false,
			});

			Assert.Equal(1, TestFightRuntime.HpDelta);
			Assert.Equal(0, rewardCount);
			Assert.Equal(-1, queued.CurrentIndex);
			Assert.Null(transition);

			EventManager.Publish(new VictoryAnimationCompleteEvent());

			Assert.Equal(SceneId.Battle, transition.Scene);
		}
		finally
		{
			EventManager.Clear();
			EventQueue.Clear();
			TestFightRuntime.Reset();
		}
	}

	private static TestFightLaunchOptions Options()
	{
		return new TestFightLaunchOptions
		{
			WeaponId = "hammer",
			EnemyId = "skeleton",
			PenanceLevel = 24,
		};
	}
}
