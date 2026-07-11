using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.VisualEffects;
using Crusaders30XX.ECS.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Modular FX Screen")]
	public sealed class ModularEffectScreenDisplaySystem : Core.System
	{
		private static readonly Color Cream = new(255, 245, 223);
		private static readonly Color Red = new(199, 34, 50);
		private static readonly Color RedBright = new(239, 52, 72);
		private static readonly Color SmokeCore = new(55, 47, 50);
		private static readonly Color BlackSmoke = new(12, 10, 12);

		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly Texture2D _pixel;

		[DebugEditable(DisplayName = "White Wash Alpha", Step = 0.01f, Min = 0f, Max = 2f)]
		public float WhiteWashAlpha { get; set; } = 0.58f;

		[DebugEditable(DisplayName = "Red Vignette Alpha", Step = 0.01f, Min = 0f, Max = 2f)]
		public float RedVignetteAlpha { get; set; } = 0.72f;

		[DebugEditable(DisplayName = "Slash Band Alpha", Step = 0.01f, Min = 0f, Max = 2f)]
		public float SlashBandAlpha { get; set; } = 0.95f;

		[DebugEditable(DisplayName = "Smoke Screen Alpha", Step = 0.01f, Min = 0f, Max = 2f)]
		public float SmokeScreenAlpha { get; set; } = 0.78f;

		[DebugEditable(DisplayName = "Shockwave Ring Alpha", Step = 0.01f, Min = 0f, Max = 2f)]
		public float ShockwaveRingAlpha { get; set; } = 0.92f;

		[DebugEditable(DisplayName = "Shake Pixels", Step = 1f, Min = 0f, Max = 80f)]
		public float ShakePixels { get; set; } = 9f;

		[DebugEditable(DisplayName = "Punch Zoom Amount", Step = 0.01f, Min = 0f, Max = 0.4f)]
		public float PunchZoomAmount { get; set; } = 0.035f;

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
				float p = VisualEffectDisplayMath.Progress(effect);
				if (effect.Recipe.Modules.Contains(VisualEffectModule.Shake))
				{
					transform.Offset += ComputeStepShake(p) * ShakePixels * Math.Max(0f, effect.Recipe.Intensity);
				}
				if (effect.Recipe.Modules.Contains(VisualEffectModule.PunchZoom))
				{
					float pulse = ComputePunchScale(p, Math.Max(0f, effect.Recipe.Intensity));
					transform.Scale = new Vector2(Math.Max(transform.Scale.X, pulse), Math.Max(transform.Scale.Y, pulse));
				}
			}
		}

		public void Draw()
		{
			foreach (var effect in GetActiveEffects())
			{
				var modules = effect.Recipe.Modules;
				if (modules.Contains(VisualEffectModule.WhiteWash)) DrawWhiteWash(effect);
				if (modules.Contains(VisualEffectModule.RedVignette)) DrawRedVignette(effect);
				if (modules.Contains(VisualEffectModule.Shockwave)) DrawShockwave(effect);
				if (modules.Contains(VisualEffectModule.SlashBand)) DrawSlashBand(effect);
				if (modules.Contains(VisualEffectModule.SmokeScreen)) DrawSmokeScreen(effect);
			}
		}

		private void DrawWhiteWash(ActiveVisualEffect effect)
		{
			float p = VisualEffectDisplayMath.Progress(effect);
			float alpha = WhiteWashAlpha * effect.Recipe.Intensity * VisualEffectDisplayMath.Window(p, 0.08f, 0.22f, 0.22f, 1f);
			if (alpha <= 0f) return;
			float scale = MathHelper.Lerp(0.45f, 1.3f, VisualEffectDisplayMath.EaseOutCubic(p));
			DrawSoftCircle(effect.ImpactAnchor, 920f * scale, Cream, alpha, 0f, 0.44f);
			DrawSoftCircle(effect.ImpactAnchor, 520f * scale, Color.White, alpha * 0.36f, 0f, 0.28f);
		}

		private void DrawRedVignette(ActiveVisualEffect effect)
		{
			float p = VisualEffectDisplayMath.Progress(effect);
			float window = p <= 0.16f
				? p / 0.16f
				: p <= 0.30f
					? MathHelper.Lerp(1f, 0.53f, (p - 0.16f) / 0.14f)
					: p <= 0.42f
						? MathHelper.Lerp(0.53f, 1f, (p - 0.30f) / 0.12f)
						: MathHelper.Clamp(1f - (p - 0.42f) / 0.58f, 0f, 1f);
			float alpha = RedVignetteAlpha * effect.Recipe.Intensity * window;
			if (alpha <= 0f) return;

			_spriteBatch.Draw(_pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Red * (alpha * 0.18f));
			_spriteBatch.Draw(_pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.Black * (alpha * 0.18f));
			DrawSoftCircle(effect.ImpactAnchor, 720f, RedBright, alpha * 0.12f, 0.12f, 1f);
		}

		private void DrawShockwave(ActiveVisualEffect effect)
		{
			float p = VisualEffectDisplayMath.Progress(effect);
			float alpha = ShockwaveRingAlpha * effect.Recipe.Intensity * VisualEffectDisplayMath.Window(p, 0.14f, 0.24f, 0.24f, 1f);
			if (alpha <= 0f) return;
			float scale = MathHelper.Lerp(0.2f, 9.5f, VisualEffectDisplayMath.EaseOutCubic(p));
			var ring = PrimitiveTextureFactory.GetAntialiasedRingMask(_graphicsDevice, 48, 48, 3f);
			DrawMask(ring, effect.ImpactAnchor, Cream * (alpha * 0.72f), 0f, new Vector2(scale));
			DrawMask(ring, effect.ImpactAnchor, RedBright * (alpha * 0.18f), 0f, new Vector2(scale * 1.34f));
		}

		private void DrawSlashBand(ActiveVisualEffect effect)
		{
			float p = VisualEffectDisplayMath.Progress(effect);
			float alpha = SlashBandAlpha * effect.Recipe.Intensity * VisualEffectDisplayMath.Window(p, 0f, 0.26f, 0.26f, 1f);
			if (alpha <= 0f) return;

			float sign = Math.Sign(effect.DirectionSign == 0 ? 1 : effect.DirectionSign);
			float angle = MathHelper.ToRadians(-23f * sign);
			float start = -0.42f * Game1.VirtualWidth * sign;
			float end = 0.38f * Game1.VirtualWidth * sign;
			float travel = MathHelper.Lerp(start, end, VisualEffectDisplayMath.EaseOutCubic(p));
			float scaleX = MathHelper.Lerp(0.28f, 1f, VisualEffectDisplayMath.EaseOutCubic(p));
			var center = new Vector2(Game1.VirtualWidth * 0.5f, effect.ImpactAnchor.Y) + Axis(angle) * travel;
			DrawSlashGradient(center, angle, Game1.VirtualWidth * 1.12f * scaleX, 58f, alpha);
		}

		private void DrawSmokeScreen(ActiveVisualEffect effect)
		{
			float p = VisualEffectDisplayMath.Progress(effect);
			float alpha = SmokeScreenAlpha * effect.Recipe.Intensity * VisualEffectDisplayMath.Window(p, 0f, 0.22f, 0.68f, 1f);
			if (alpha <= 0f) return;
			float lift = MathHelper.Lerp(14f, -12f, VisualEffectDisplayMath.EaseOutCubic(p));
			float scale = MathHelper.Lerp(0.88f, 1.16f, p);
			var center = effect.ImpactAnchor + new Vector2(0f, lift);
			DrawSoftCircle(center, 520f * scale, SmokeCore, alpha * 0.86f, 0f, 0.35f);
			DrawSoftCircle(center + new Vector2(110f * effect.DirectionSign, -92f), 410f * scale, new Color(85, 16, 28), alpha * 0.52f, 0f, 0.38f);
			DrawSoftCircle(center + new Vector2(-85f * effect.DirectionSign, 118f), 380f * scale, BlackSmoke, alpha * 0.54f, 0f, 0.34f);
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
				.Where(e => e != null && e.ElapsedSeconds >= 0f);
		}

		private static Vector2 ComputeStepShake(float progress)
		{
			if (progress >= 0.40f) return Vector2.Zero;
			float local = MathHelper.Clamp(progress / 0.40f, 0f, 1f);
			if (local < 0.12f) return Vector2.Zero;
			if (local < 0.24f) return new Vector2(1f, -0.56f);
			if (local < 0.36f) return new Vector2(-0.78f, 0.44f);
			if (local < 0.48f) return new Vector2(0.56f, 0.78f);
			if (local < 0.60f) return new Vector2(-0.44f, -0.56f);
			if (local < 0.84f) return new Vector2(0.78f, 0.33f);
			return Vector2.Zero;
		}

		private float ComputePunchScale(float progress, float intensity)
		{
			if (progress >= 0.52f) return 1f;
			float t = MathHelper.Clamp(progress / 0.52f, 0f, 1f);
			float shape = t < 0.20f
				? MathHelper.Lerp(0f, 1f, t / 0.20f)
				: t < 0.50f
					? MathHelper.Lerp(1f, -0.34f, (t - 0.20f) / 0.30f)
					: MathHelper.Lerp(-0.34f, 0f, (t - 0.50f) / 0.50f);
			return 1f + shape * PunchZoomAmount * intensity;
		}

		private void DrawSlashGradient(Vector2 center, float rotation, float length, float thickness, float alpha)
		{
			int strips = 24;
			var axis = Axis(rotation);
			for (int i = 0; i < strips; i++)
			{
				float t0 = i / (float)strips;
				float t1 = (i + 1) / (float)strips;
				float mid = (t0 + t1) * 0.5f;
				float stripAlpha = mid < 0.5f ? mid / 0.5f : (1f - mid) / 0.5f;
				stripAlpha *= stripAlpha;
				Color color = mid > 0.62f ? RedBright : Cream;
				var start = center + axis * ((t0 - 0.5f) * length);
				var end = center + axis * ((t1 - 0.5f) * length);
				DrawLine(start, end, color * (alpha * stripAlpha), thickness);
			}
		}

		private void DrawSoftCircle(Vector2 center, float diameter, Color color, float alpha, float innerStop, float outerStop)
		{
			int size = Math.Max(1, (int)MathF.Round(diameter));
			var texture = PrimitiveTextureFactory.GetSoftRadialCircle(_graphicsDevice, size, innerStop, outerStop);
			DrawMask(texture, center, color * alpha, 0f, Vector2.One);
		}

		private void DrawMask(Texture2D texture, Vector2 center, Color color, float rotation, Vector2 scale)
		{
			_spriteBatch.Draw(texture, center, null, color, rotation, new Vector2(texture.Width * 0.5f, texture.Height * 0.5f), scale, SpriteEffects.None, 0f);
		}

		private void DrawLine(Vector2 start, Vector2 end, Color color, float thickness)
		{
			var delta = end - start;
			float length = delta.Length();
			if (length <= 0.001f) return;
			float rotation = MathF.Atan2(delta.Y, delta.X);
			_spriteBatch.Draw(_pixel, start, null, color, rotation, new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
		}

		private static Vector2 Axis(float rotation)
		{
			return new Vector2(MathF.Cos(rotation), MathF.Sin(rotation));
		}
	}
}
