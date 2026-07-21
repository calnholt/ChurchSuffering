using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
	[DebugTab("Player HUD Root")]
	public class PlayerHudRootDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;

		public PlayerHudRootDisplaySystem(
			EntityManager entityManager,
			GraphicsDevice graphicsDevice,
			SpriteBatch spriteBatch) : base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return Array.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public void Draw()
		{
			var root = EntityManager.GetEntitiesWithComponent<PlayerHudAnchor>().FirstOrDefault();
			var anchor = root?.GetComponent<PlayerHudAnchor>();
			if (anchor == null || anchor.Bounds.Width <= 0 || anchor.Bounds.Height <= 0) return;

			var regions = GetShadowRegionBounds();

			int blur = Math.Max(0, anchor.ShadowBlurRadius);
			const int layers = 5;
			for (int layer = layers; layer >= 1; layer--)
			{
				float fraction = layer / (float)layers;
				int spread = (int)Math.Round(blur * fraction);
				byte alpha = (byte)Math.Round(anchor.ShadowAlpha * (1f - fraction * 0.72f) / layers);
				foreach (var bounds in regions)
				{
					DrawRegionShadow(bounds, anchor, spread, alpha);
				}
			}
		}

		internal IReadOnlyList<Rectangle> GetShadowRegionBounds()
		{
			return EntityManager.GetEntitiesWithComponent<PlayerHudRegion>()
				.Select(entity => new
				{
					Region = entity.GetComponent<PlayerHudRegion>(),
					Bounds = TransformResolverService.ResolveLocalBounds(
						EntityManager,
						entity,
						entity.GetComponent<PlayerHudRegion>()?.Bounds ?? Rectangle.Empty),
				})
				.Where(item => item.Region != null
					&& item.Region.Type != PlayerHudRegionType.Root
					&& item.Region.IsVisible
					&& item.Bounds.Width > 0
					&& item.Bounds.Height > 0)
				.Select(item => item.Bounds)
				.ToList();
		}

		private void DrawRegionShadow(Rectangle bounds, PlayerHudAnchor anchor, int spread, byte alpha)
		{
			var destination = new Rectangle(
				bounds.X - spread,
				bounds.Y + anchor.ShadowOffsetY - spread,
				bounds.Width + spread * 2,
				bounds.Height + spread * 2);
			float skewPercent = destination.Width <= 0
				? 0f
				: anchor.Slant * 100f / destination.Width;
			var mask = PrimitiveTextureFactory.GetParallelogramMask(
				_graphicsDevice,
				destination.Width,
				destination.Height,
				skewPercent);
			_spriteBatch.Draw(mask, destination, Color.FromNonPremultiplied(0, 0, 0, alpha));
		}
	}
}
