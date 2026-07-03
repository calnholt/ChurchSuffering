using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Data.Tutorials;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Objects.Enemies;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public class EnemyIntentPlanningSystemTests
{
	[Fact]
	public void EnemyStart_replans_after_mid_turn_intents_cleared()
	{
		EventManager.Clear();

		try
		{
			var world = BuildWorld(out var phaseState, out var enemy, out var definition, out var intent);
			_ = new EnemyIntentPlanningSystem(world.EntityManager);

			phaseState.Sub = SubPhase.PlayerEnd;
			phaseState.TurnNumber = 4;
			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.EnemyStart });

			Assert.Single(intent.Planned);
			Assert.Equal(EnemyAttackId.FallenShepherdPhase1, intent.Planned[0].AttackId);

			phaseState.Sub = SubPhase.Block;
			phaseState.TurnNumber = 5;
			intent.Planned.Clear();
			enemy.GetComponent<NextTurnAttackIntent>().Planned.Clear();
			definition.CurrentPhase = 2;
			enemy.GetComponent<EnemyArsenal>().AttackIds = definition
				.GetAttackIds(world.EntityManager, phaseState.TurnNumber)
				.ToList();

			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.EnemyStart });

			Assert.Single(intent.Planned);
			Assert.Equal(EnemyAttackId.FallenShepherdPhase2, intent.Planned[0].AttackId);
		}
		finally
		{
			EventManager.Clear();
		}
	}

		[Fact]
		public void Guided_intent_plans_attacks_for_tutorial_section()
		{
			EventManager.Clear();

			try
			{
				var world = new World();
				var phaseEntity = world.CreateEntity("PhaseState");
				world.AddComponent(phaseEntity, new PhaseState
				{
					Main = MainPhase.PlayerTurn,
					Sub = SubPhase.PlayerEnd,
					TurnNumber = 2,
				});
				var tutorialEntity = world.CreateEntity("GuidedTutorial");
				world.AddComponent(tutorialEntity, new GuidedTutorial
				{
					Section = 8,
					TurnWithinSection = 2,
				});
				var player = world.CreateEntity("Player");
				world.AddComponent(player, new Player());
				world.AddComponent(player, new AppliedPassives());

				var definition = new Horde();
				var enemy = world.CreateEntity("Enemy");
				world.AddComponent(enemy, new Enemy
				{
					Id = definition.Id,
					Name = definition.Name,
					EnemyBase = definition,
				});
				world.AddComponent(enemy, new EnemyArsenal
				{
					AttackIds = definition.GetAttackIds(world.EntityManager, 3).ToList(),
				});
				world.AddComponent(enemy, new AppliedPassives());
				var intent = new AttackIntent();
				world.AddComponent(enemy, intent);
				world.AddComponent(enemy, new NextTurnAttackIntent());
				_ = new EnemyIntentPlanningSystem(world.EntityManager);

				EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.EnemyStart });

				Assert.Single(intent.Planned);
				Assert.Equal(EnemyAttackId.TutorialHordeStrike6, intent.Planned[0].AttackId);
			}
			finally
			{
				EventManager.Clear();
			}
		}

	private static World BuildWorld(
		out PhaseState phaseState,
		out Entity enemy,
		out FallenShepherd definition,
		out AttackIntent intent)
	{
		var world = new World();
		var phaseEntity = world.CreateEntity("PhaseState");
		phaseState = new PhaseState
		{
			Main = MainPhase.EnemyTurn,
			Sub = SubPhase.Block,
			TurnNumber = 5,
		};
		world.AddComponent(phaseEntity, phaseState);

		var player = world.CreateEntity("Player");
		world.AddComponent(player, new Player());
		world.AddComponent(player, new AppliedPassives());

		definition = new FallenShepherd();
		enemy = world.CreateEntity("Enemy");
		world.AddComponent(enemy, new Enemy
		{
			Id = definition.Id,
			Name = definition.Name,
			EnemyBase = definition,
		});
		world.AddComponent(enemy, new EnemyArsenal { AttackIds = new() { EnemyAttackId.FallenShepherdPhase1 } });
		world.AddComponent(enemy, new AppliedPassives());
		intent = new AttackIntent();
		world.AddComponent(enemy, intent);
		world.AddComponent(enemy, new NextTurnAttackIntent());
		return world;
	}
}
