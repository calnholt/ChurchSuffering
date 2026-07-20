using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Singletons;

namespace Crusaders30XX.ECS.Services
{
	public static class WayStationRunSetupService
	{
		public static void Depart(World world)
		{
			if (world == null) return;

			RunDeckService.DestroyRunDeck(world.EntityManager);
			RunPlayerService.DestroyRunPlayer(world.EntityManager);
			SaveCache.StartWayStationClimbAttempt();
			SaveCache.ConfigurePrimaryRunSetup(
				WayStationRunSetupSingleton.WeaponId,
				GetSelectedTemperanceId(),
				WayStationRunSetupSingleton.SelectedDifficulty);
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

		public static void ApplySelectedPlayerHp(Entity player)
		{
			var hp = player?.GetComponent<HP>();
			if (hp == null) return;

			hp.Max = WayStationRunSetupSingleton.PlayerMaxHp;
			hp.UnscarredMax = hp.Max;
			hp.Current = hp.Max;
		}

		private static string GetSelectedTemperanceId()
		{
			return StartingDeckGeneratorService.GetDefaultTemperanceId(WayStationRunSetupSingleton.SelectedWeapon);
		}
	}
}
