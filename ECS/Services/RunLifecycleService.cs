using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Achievements;
using ChurchSuffering.ECS.Data.Save;

namespace ChurchSuffering.ECS.Services
{
	public static class RunLifecycleService
	{
		/// <summary>
		/// Persists meta progress, destroys run entities, and leaves the save without an active run.
		/// </summary>
		public static void EndCurrentRun(EntityManager entityManager = null)
		{
			CardUsageTelemetryRuntime.EndCurrentRun();
			AchievementManager.SaveProgress();
			if (entityManager != null)
			{
				RunScopedStateService.ClearRunCardRestrictionComponents(entityManager);
				RunDeckService.DestroyRunDeck(entityManager);
			}
			SaveCache.ClearRunScopedState();
			if (entityManager != null)
			{
				RunPlayerService.DestroyRunPlayer(entityManager);
				var queuedEvents = entityManager.GetEntity("QueuedEvents");
				if (queuedEvents != null)
					entityManager.DestroyEntity(queuedEvents.Id);
			}
			SaveCache.MarkRunInactive();
		}
	}
}
