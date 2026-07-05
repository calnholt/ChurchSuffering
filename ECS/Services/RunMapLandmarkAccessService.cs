using System.Collections.Generic;
using Crusaders30XX.ECS.Data.Locations;

namespace Crusaders30XX.ECS.Services
{
	public static class RunMapLandmarkAccessService
	{
		public static bool IsWithinCompletedQuestFog(
			float landmarkX,
			float landmarkY,
			IReadOnlyList<RunMapNode> nodes)
		{
			if (nodes == null || nodes.Count == 0) return false;

			float revealRadius = LocationMapConstants.DefaultRevealRadius;

			for (int i = 0; i < nodes.Count; i++)
			{
				var node = nodes[i];
				if (node == null || !node.isCompleted) continue;

				if (RunMapRevealService.IsWithinRevealRadius(
					landmarkX, landmarkY, node.worldX, node.worldY, revealRadius))
				{
					return true;
				}
			}

			return false;
		}
	}
}
