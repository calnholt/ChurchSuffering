using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.RunSetup;

namespace ChurchSuffering.ECS.Services
{
	public static class WayStationRunSetupService
	{
		private const string RunSetupEntityName = "WayStationRunSetup";

		public static RunSetup GetRunSetup(EntityManager entityManager)
		{
			if (entityManager == null) return null;
			var entity = entityManager.GetEntity(RunSetupEntityName);
			if (entity == null)
			{
				entity = entityManager.CreateEntity(RunSetupEntityName);
				entityManager.AddComponent(entity, new RunSetup());
				entityManager.AddComponent(entity, new DontDestroyOnLoad());
			}

			var setup = entity.GetComponent<RunSetup>();
			if (setup == null)
			{
				setup = new RunSetup();
				entityManager.AddComponent(entity, setup);
			}
			if (entity.GetComponent<DontDestroyOnLoad>() == null)
			{
				entityManager.AddComponent(entity, new DontDestroyOnLoad());
			}
			return setup;
		}

		public static void Depart(World world)
		{
			if (world == null) return;
			var setup = GetRunSetup(world.EntityManager);
			if (setup == null) return;

			RunDeckService.DestroyRunDeck(world.EntityManager);
			RunPlayerService.DestroyRunPlayer(world.EntityManager);
			SaveCache.StartWayStationClimbAttempt();
			SaveCache.ConfigurePrimaryRunSetup(
				setup.WeaponId,
				StartingDeckGeneratorService.GetDefaultTemperanceId(setup.SelectedWeapon),
				setup.SelectedPenanceLevel);
			PrepareRunEntities(world);

			EventManager.Publish(new ShowTransition { Scene = SceneId.Climb, SkipHold = true });
		}

		private static void PrepareRunEntities(World world)
		{
			var deckEntity = RunDeckService.EnsureRunDeck(world.EntityManager);
			var player = RunPlayerService.EnsureRunPlayer(world);
			var playerComponent = player?.GetComponent<Player>();
			if (playerComponent != null)
			{
				playerComponent.DeckEntity = deckEntity;
			}
		}

		public static void ApplyPersistedPlayerHp(Entity player)
		{
			var hp = player?.GetComponent<HP>();
			if (hp == null) return;

			hp.Max = PenanceRules.Calculate(SaveCache.GetClimbState()?.penanceLevel ?? 0).PlayerMaximumHp;
			hp.UnscarredMax = hp.Max;
			hp.Current = hp.Max;
		}
	}
}
