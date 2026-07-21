using ChurchSuffering.ECS.Core;

namespace ChurchSuffering.ECS.Services
{
	public static class PileDisplayVisibilityService
	{
		public static bool IsDrawPileVisible(EntityManager entityManager)
		{
			if (!GuidedTutorialService.IsActive(entityManager)) return true;
			var guidedState = GuidedTutorialService.GetState(entityManager);
			return guidedState?.Section == 8;
		}

		public static bool IsDiscardPileVisible(EntityManager entityManager)
		{
			return !GuidedTutorialService.IsActive(entityManager);
		}
	}
}
