using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.VisualEffects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Modular FX Screen")]
	public sealed class ModularEffectScreenDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly Texture2D _pixel;

		[DebugEditable(DisplayName = "White Wash Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float WhiteWashAlpha { get; set; } = 0.28f;

		[DebugEditable(DisplayName = "Red Vignette Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float RedVignetteAlpha { get; set; } = 0.32f;

		[DebugEditable(DisplayName = "Slash Band Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float SlashBandAlpha { get; set; } = 0.38f;

		[DebugEditable(DisplayName = "Shake Pixels", Step = 1f, Min = 0f, Max = 40f)]
		public float ShakePixels { get; set; } = 8f;

		[DebugEditable(DisplayName = "Punch Zoom Amount", Step = 0.01f, Min = 0f, Max = 0.4f)]
		public float PunchZoomAmount { get; set; } = 0.08f;

		public ModularEffectScreenDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch) : base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_pixel = new Texture2D(graphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });
		}

		protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();
		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			if (!IsActive) return;
			var transform = EnsureBattlePresentationTransform();
			transform.Offset = Vector2.Zero;
			transform.Scale = Vector2.One;

			foreach (var effect in GetActiveEffects())
			{
				float impact = ImpactProgress(effect);
				if (effect.Recipe.Modules.Contains(VisualEffectModule.Shake))
				{
					float amp = ShakePixels * effect.Recipe.Intensity * (1f - impact);
					transform.Offset += new Vector2(
						(float)Math.Sin(effect.ElapsedSeconds * 92f) * amp,
						(float)Math.Cos(effect.ElapsedSeconds * 71f) * amp * 0.6f);
				}
				if (effect.Recipe.Modules.Contains(VisualEffectModule.PunchZoom))
				{
					float pulse = (float)Math.Sin(MathHelper.Clamp(impact, 0f, 1f) * Math.PI) * PunchZoomAmount * effect.Recipe.Intensity;
					transform.Scale = new Vector2(Math.Max(transform.Scale.X, 1f + pulse), Math.Max(transform.Scale.Y, 1f + pulse));
				}
			}
		}

		public void Draw()
		{
			foreach (var effect in GetActiveEffects())
			{
				float progress = Progress(effect);
				float impact = ImpactProgress(effect);
				float fade = 1f - progress;
				if (effect.Recipe.Modules.Contains(VisualEffectModule.WhiteWash))
				{
					DrawFilled(new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.White * (WhiteWashAlpha * fade * effect.Recipe.Intensity));
				}
				if (effect.Recipe.Modules.Contains(VisualEffectModule.RedVignette))
				{
					DrawFilled(new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), new Color(130, 0, 0) * (RedVignetteAlpha * fade * effect.Recipe.Intensity));
				}
				if (effect.Recipe.Modules.Contains(VisualEffectModule.SlashBand))
				{
					var center = effect.ImpactAnchor;
					float alpha = SlashBandAlpha * (1f - impact) * effect.Recipe.Intensity;
					DrawLine(center + new Vector2(-260f * effect.DirectionSign, 130f), center + new Vector2(260f * effect.DirectionSign, -130f), Color.White * alpha, 42f);
				}
				if (effect.Recipe.Modules.Contains(VisualEffectModule.SmokeScreen))
				{
					for (int i = 0; i < 5; i++)
					{
						float x = effect.ImpactAnchor.X + (i - 2) * 42f;
						float y = effect.ImpactAnchor.Y - impact * 70f + (i % 2) * 18f;
						DrawFilled(new Rectangle((int)x - 42, (int)y - 22, 84, 44), Color.Black * (0.16f * fade));
					}
				}
			}
		}

		private BattlePresentationTransform EnsureBattlePresentationTransform()
		{
			var entity = EntityManager.GetEntity("BattlePresentationTransform");
			if (entity == null)
			{
				entity = EntityManager.CreateEntity("BattlePresentationTransform");
			}
			var transform = entity.GetComponent<BattlePresentationTransform>();
			if (transform == null)
			{
				transform = new BattlePresentationTransform();
				EntityManager.AddComponent(entity, transform);
			}
			return transform;
		}

		private IEnumerable<ActiveVisualEffect> GetActiveEffects()
		{
			return EntityManager.GetEntitiesWithComponent<ActiveVisualEffect>()
				.Select(e => e.GetComponent<ActiveVisualEffect>())
				.Where(e => e != null);
		}

		private static float Progress(ActiveVisualEffect effect)
		{
			return MathHelper.Clamp(effect.ElapsedSeconds / Math.Max(0.0001f, effect.Timing.DurationSeconds), 0f, 1f);
		}

		private static float ImpactProgress(ActiveVisualEffect effect)
		{
			return MathHelper.Clamp((effect.ElapsedSeconds - effect.Timing.ImpactTimeSeconds) / Math.Max(0.0001f, effect.Timing.DurationSeconds - effect.Timing.ImpactTimeSeconds), 0f, 1f);
		}

		private void DrawFilled(Rectangle rect, Color color)
		{
			_spriteBatch.Draw(_pixel, rect, color);
		}

		private void DrawLine(Vector2 start, Vector2 end, Color color, float thickness)
		{
			var delta = end - start;
			float length = delta.Length();
			if (length <= 0.001f) return;
			float rotation = (float)Math.Atan2(delta.Y, delta.X);
			_spriteBatch.Draw(_pixel, start, null, color, rotation, new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
		}
	}
}
