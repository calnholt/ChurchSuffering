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
	[DebugTab("Modular FX Primitives")]
	public sealed class ModularEffectPrimitiveDisplaySystem : Core.System
	{
		private readonly SpriteBatch _spriteBatch;
		private readonly Texture2D _pixel;

		[DebugEditable(DisplayName = "Primitive Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float PrimitiveAlpha { get; set; } = 0.9f;

		[DebugEditable(DisplayName = "Line Thickness", Step = 1f, Min = 1f, Max = 60f)]
		public float LineThickness { get; set; } = 14f;

		public ModularEffectPrimitiveDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch) : base(entityManager)
		{
			_spriteBatch = spriteBatch;
			_pixel = new Texture2D(graphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });
		}

		protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();
		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public void Draw()
		{
			foreach (var effect in EntityManager.GetEntitiesWithComponent<ActiveVisualEffect>().Select(e => e.GetComponent<ActiveVisualEffect>()).Where(e => e != null))
			{
				float progress = MathHelper.Clamp(effect.ElapsedSeconds / Math.Max(0.0001f, effect.Timing.DurationSeconds), 0f, 1f);
				float impact = MathHelper.Clamp((effect.ElapsedSeconds - effect.Timing.ImpactTimeSeconds) / Math.Max(0.0001f, effect.Timing.DurationSeconds - effect.Timing.ImpactTimeSeconds), 0f, 1f);
				float alpha = PrimitiveAlpha * (1f - progress) * effect.Recipe.Intensity;
				var modules = effect.Recipe.Modules;
				if (modules.Contains(VisualEffectModule.SwordArc)) DrawLine(effect.SourceAnchor, effect.ImpactAnchor + new Vector2(34f * effect.DirectionSign, -32f), Color.White * alpha, LineThickness);
				if (modules.Contains(VisualEffectModule.HammerArc)) DrawLine(effect.SourceAnchor + new Vector2(-80f * effect.DirectionSign, -90f), effect.ImpactAnchor, Color.DarkGray * alpha, LineThickness * 2.2f);
				if (modules.Contains(VisualEffectModule.CrossSlash))
				{
					DrawLine(effect.ImpactAnchor + new Vector2(-90f, -60f), effect.ImpactAnchor + new Vector2(90f, 60f), Color.White * alpha, LineThickness);
					DrawLine(effect.ImpactAnchor + new Vector2(-90f, 60f), effect.ImpactAnchor + new Vector2(90f, -60f), Color.White * alpha, LineThickness);
				}
				if (modules.Contains(VisualEffectModule.ClawSlash))
				{
					for (int i = -1; i <= 1; i++)
					{
						DrawLine(effect.ImpactAnchor + new Vector2(-92f * effect.DirectionSign, -42f + i * 26f), effect.ImpactAnchor + new Vector2(78f * effect.DirectionSign, -4f + i * 26f), Color.IndianRed * alpha, LineThickness * 0.75f);
					}
				}
				if (modules.Contains(VisualEffectModule.Bite))
				{
					DrawLine(effect.ImpactAnchor + new Vector2(-70f, -70f + impact * 45f), effect.ImpactAnchor + new Vector2(0f, -12f), Color.White * alpha, LineThickness);
					DrawLine(effect.ImpactAnchor + new Vector2(70f, -70f + impact * 45f), effect.ImpactAnchor + new Vector2(0f, -12f), Color.White * alpha, LineThickness);
					DrawLine(effect.ImpactAnchor + new Vector2(-70f, 70f - impact * 45f), effect.ImpactAnchor + new Vector2(0f, 12f), Color.White * alpha, LineThickness);
					DrawLine(effect.ImpactAnchor + new Vector2(70f, 70f - impact * 45f), effect.ImpactAnchor + new Vector2(0f, 12f), Color.White * alpha, LineThickness);
				}
				if (modules.Contains(VisualEffectModule.RockBlast))
				{
					DrawFilled(Centered(effect.ImpactAnchor, 120f * (0.4f + impact), 70f * (0.4f + impact)), new Color(97, 79, 60) * alpha);
				}
				if (modules.Contains(VisualEffectModule.CrossBloom))
				{
					DrawLine(effect.ImpactAnchor + new Vector2(-70f, 0f), effect.ImpactAnchor + new Vector2(70f, 0f), Color.Gold * alpha, LineThickness);
					DrawLine(effect.ImpactAnchor + new Vector2(0f, -80f), effect.ImpactAnchor + new Vector2(0f, 80f), Color.Gold * alpha, LineThickness);
				}
				if (modules.Contains(VisualEffectModule.Ring)) DrawRing(effect.ImpactAnchor, 36f + impact * 120f, Color.White * alpha);
				if (modules.Contains(VisualEffectModule.Halo)) DrawRing(effect.TargetAnchor + new Vector2(0f, -120f - impact * 30f), 52f, Color.Gold * alpha);
				if (modules.Contains(VisualEffectModule.Beam)) DrawFilled(new Rectangle((int)effect.TargetAnchor.X - 32, 0, 64, Game1.VirtualHeight), Color.LightGoldenrodYellow * (alpha * 0.45f));
				if (modules.Contains(VisualEffectModule.Rays))
				{
					for (int i = 0; i < 8; i++)
					{
						float a = i * MathHelper.TwoPi / 8f + progress;
						var dir = new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
						DrawLine(effect.ImpactAnchor, effect.ImpactAnchor + dir * (80f + impact * 80f), Color.Gold * (alpha * 0.5f), 6f);
					}
				}
				if (modules.Contains(VisualEffectModule.Cracks))
				{
					for (int i = 0; i < 6; i++)
					{
						float a = i * MathHelper.TwoPi / 6f;
						var dir = new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
						DrawLine(effect.ImpactAnchor, effect.ImpactAnchor + dir * (40f + impact * 60f), Color.DarkRed * alpha, 5f);
					}
				}
				if (modules.Contains(VisualEffectModule.HitFlash)) DrawFilled(Centered(effect.ImpactAnchor, 130f * (1f - impact), 130f * (1f - impact)), Color.White * (alpha * (1f - impact)));
			}
		}

		private static Rectangle Centered(Vector2 center, float width, float height)
		{
			return new Rectangle((int)(center.X - width / 2f), (int)(center.Y - height / 2f), (int)Math.Max(1f, width), (int)Math.Max(1f, height));
		}

		private void DrawFilled(Rectangle rect, Color color) => _spriteBatch.Draw(_pixel, rect, color);

		private void DrawRing(Vector2 center, float radius, Color color)
		{
			const int segments = 32;
			Vector2 prev = center + new Vector2(radius, 0f);
			for (int i = 1; i <= segments; i++)
			{
				float a = i * MathHelper.TwoPi / segments;
				var next = center + new Vector2((float)Math.Cos(a), (float)Math.Sin(a)) * radius;
				DrawLine(prev, next, color, 5f);
				prev = next;
			}
		}

		private void DrawLine(Vector2 start, Vector2 end, Color color, float thickness)
		{
			var delta = end - start;
			float length = delta.Length();
			if (length <= 0.001f) return;
			float rotation = (float)Math.Atan2(delta.Y, delta.X);
			_spriteBatch.Draw(_pixel, start, null, color, rotation, new Vector2(0f, 0.5f), new Vector2(length, Math.Max(1f, thickness)), SpriteEffects.None, 0f);
		}
	}
}
