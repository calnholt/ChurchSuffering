using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Services
{
	public static class PortraitPixelBurstRequestBuilder
	{
		public static bool TryBuild(
			EntityManager entityManager,
			ImageAssetService imageAssets,
			Entity enemy,
			bool isPreview,
			out PixelBurstAnimationRequested request)
		{
			request = null;
			if (enemy == null || imageAssets == null) return false;

			var transform = enemy.GetComponent<Transform>();
			if (transform == null) return false;

			var texture = ResolveEnemyTexture(entityManager, imageAssets);
			if (texture == null) return false;

			var portraitInfo = enemy.GetComponent<PortraitInfo>();
			var (center, topLeft, drawScale) = PortraitPixelBurstLayout.ResolveDrawFrame(
				portraitInfo,
				transform.Position,
				texture.Width,
				texture.Height,
				Game1.VirtualHeight);

			request = new PixelBurstAnimationRequested
			{
				Texture = texture,
				Center = center,
				DrawTopLeft = topLeft,
				DrawScale = drawScale,
				SourceEntityId = enemy.Id,
				BurstId = System.Guid.NewGuid(),
				IsPreview = isPreview
			};
			return true;
		}

		private static Texture2D ResolveEnemyTexture(EntityManager entityManager, ImageAssetService imageAssets)
		{
			var queuedEntity = entityManager.GetEntity("QueuedEvents");
			var queued = queuedEntity?.GetComponent<QueuedEvents>();
			if (queued?.Events == null || queued.Events.Count == 0 || queued.CurrentIndex < 0 || queued.CurrentIndex >= queued.Events.Count)
			{
				return imageAssets.TryGetTexture("Enemies/Skeleton");
			}

			string enemyId = queued.Events[queued.CurrentIndex].EventId;
			string assetName = EnemyPortraitContent.ToAssetName(enemyId);
			return imageAssets.GetTextureOrFallback(assetName, "Enemies/Skeleton");
		}
	}
}
