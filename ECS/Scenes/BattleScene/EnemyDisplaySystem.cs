using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Enemy Display")]
	public class EnemyDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ImageAssetService _imageAssets;
		private Texture2D _enemyTexture;
		private string _loadedAssetName;
		private float _pulseTimerSeconds;
		private readonly float _pulseDurationSeconds = 0.25f;

		[DebugEditable(DisplayName = "Screen Height Coverage", Step = 0.02f, Min = 0.05f, Max = 1f)]
		public float ScreenHeightCoverage { get; set; } = 0.44f;
		[DebugEditable(DisplayName = "Center Offset X (% of width)", Step = 0.01f, Min = -1.0f, Max = 1.0f)]
		public float CenterOffsetXPct { get; set; } = 0.3f; // positive = right, negative = left
		[DebugEditable(DisplayName = "Center Offset Y (% of height)", Step = 0.01f, Min = -1.0f, Max = 1.0f)]
		public float CenterOffsetYPct { get; set; } = -0.09f; // positive = down, negative = up
		public EnemyDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ImageAssetService imageAssets)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_imageAssets = imageAssets;
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<Enemy>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			if (_pulseTimerSeconds > 0f)
			{
				_pulseTimerSeconds = System.Math.Max(0f, _pulseTimerSeconds - (float)gameTime.ElapsedGameTime.TotalSeconds);
			}

			// Write base position so parallax system can adjust it before Draw
			var t = entity.GetComponent<Transform>();
			if (t != null)
			{
				int viewportW = Game1.VirtualWidth;
				int viewportH = Game1.VirtualHeight;
				t.Position = new Vector2(
					viewportW * (0.5f + CenterOffsetXPct),
					viewportH * (0.5f + CenterOffsetYPct)
				);
			}
		}

		public void Draw()
		{
			foreach (var e in GetRelevantEntities())
			{
				var enemy = e.GetComponent<Enemy>();
				var t = e.GetComponent<Transform>();
				if (enemy == null || t == null) continue;
				if (e.HasComponent<SuppressPortraitRender>()) continue;
				Texture2D tex = GetTextureFor();
				if (tex == null) continue;
				int viewportW = Game1.VirtualWidth;
				int viewportH = Game1.VirtualHeight;
				float desiredHeight = ScreenHeightCoverage * viewportH;
				float scale = desiredHeight / tex.Height;
				if (_pulseTimerSeconds > 0f)
				{
					float tp = 1f - (_pulseTimerSeconds / _pulseDurationSeconds);
					float bump = 1f + 0.15f * (float)System.Math.Sin(tp * System.Math.PI);
					scale *= bump;
				}
				var animState = e.GetComponent<ActorPresentationState>();
				Vector2 scaleVec = new Vector2(scale, scale);
				if (animState != null)
				{
					scaleVec *= animState.ScaleMultiplier;
				}
				var origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
				// Share scale and texture dims for accurate HP positioning if needed
				var info = e.GetComponent<PortraitInfo>();
				if (info == null)
				{
					return;
				}
				info.TextureWidth = tex.Width;
				info.TextureHeight = tex.Height;
				info.CurrentScale = scale;
				info.BaseScale = desiredHeight / tex.Height;
				// t.Position is parallax-adjusted (written in Update, offset by ParallaxLayerSystem)
				var drawPos = t.Position + (animState?.DrawOffset ?? Vector2.Zero);
				var battleTransform = EntityManager.GetEntity("BattlePresentationTransform")?.GetComponent<BattlePresentationTransform>();
				if (battleTransform != null)
				{
					drawPos += battleTransform.Offset;
					scaleVec *= battleTransform.Scale;
				}
				info.LastDrawCenter = drawPos;
				info.LastDrawTopLeft = drawPos - origin * scaleVec;
				info.LastDrawScale = scaleVec;
				// Update UI bounds so hover/tooltip works over the enemy portrait
				var ui = e.GetComponent<UIElement>();
				if (ui != null)
				{
					int wPx = (int)System.Math.Round(tex.Width * scaleVec.X);
					int hPx = (int)System.Math.Round(tex.Height * scaleVec.Y);
					int x0 = (int)System.Math.Round(drawPos.X - wPx / 2f);
					int y0 = (int)System.Math.Round(drawPos.Y - hPx / 2f);
					ui.Bounds = new Rectangle(x0, y0, wPx, hPx);
				}
				_spriteBatch.Draw(tex, position: drawPos, sourceRectangle: null, color: animState?.TintColor ?? Color.White, rotation: 0f, origin: origin, scale: scaleVec, effects: SpriteEffects.None, layerDepth: 0f);
			}
		}

		private Texture2D GetTextureFor()
		{
			var queuedEntity = EntityManager.GetEntity("QueuedEvents");
			var queued = queuedEntity?.GetComponent<QueuedEvents>();
			if (queued?.Events == null || queued.CurrentIndex < 0 || queued.CurrentIndex >= queued.Events.Count)
				return null;

			string enemyId = queued.Events[queued.CurrentIndex].EventId;
			string assetName = EnemyPortraitContent.ToAssetName(enemyId);
			if (_enemyTexture != null && _loadedAssetName == assetName) return _enemyTexture;

			_loadedAssetName = assetName;
			_enemyTexture = _imageAssets.GetTextureOrFallback(assetName, "Enemies/Skeleton");
			return _enemyTexture;
		}
	}
}
