using System;
using System.Collections.Generic;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.VisualEffects;
using ChurchSuffering.ECS.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
	[DebugTab("Modular FX Screen")]
	public sealed class ModularEffectScreenDisplaySystem : Core.System
	{
		private static readonly Color Cream = new(255, 245, 223);
		private static readonly Color Red = new(199, 34, 50);
		private static readonly Color RedBright = new(239, 52, 72);
		private static readonly Color SmokeCore = new(55, 47, 50);
		private static readonly Color BlackSmoke = new(12, 10, 12);

		private readonly SpriteBatch _spriteBatch;
		private readonly ModularEffectRenderResources _resources;
		private readonly Texture2D _pixel;

		[DebugEditable(DisplayName = "White Wash Alpha", Step = 0.01f, Min = 0f, Max = 2f)]
		public float WhiteWashAlpha { get; set; } = 0.58f;

		[DebugEditable(DisplayName = "Red Vignette Alpha", Step = 0.01f, Min = 0f, Max = 2f)]
		public float RedVignetteAlpha { get; set; } = 0.72f;

		[DebugEditable(DisplayName = "Slash Band Alpha", Step = 0.01f, Min = 0f, Max = 2f)]
		public float SlashBandAlpha { get; set; } = 1.15f;

		[DebugEditable(DisplayName = "Slash Band Length", Step = 10f, Min = 200f, Max = 1600f)]
		public float SlashBandLength { get; set; } = 760f;

		[DebugEditable(DisplayName = "Slash Band Thickness", Step = 2f, Min = 10f, Max = 220f)]
		public float SlashBandThickness { get; set; } = 86f;

		[DebugEditable(DisplayName = "Smoke Screen Alpha", Step = 0.01f, Min = 0f, Max = 2f)]
		public float SmokeScreenAlpha { get; set; } = 0.78f;

		[DebugEditable(DisplayName = "Shockwave Ring Alpha", Step = 0.01f, Min = 0f, Max = 2f)]
		public float ShockwaveRingAlpha { get; set; } = 0.92f;

		[DebugEditable(DisplayName = "Shake Pixels", Step = 1f, Min = 0f, Max = 80f)]
		public float ShakePixels { get; set; } = 18f;

		[DebugEditable(DisplayName = "Punch Zoom Amount", Step = 0.01f, Min = 0f, Max = 0.4f)]
		public float PunchZoomAmount { get; set; } = 0.075f;

		public ModularEffectScreenDisplaySystem(EntityManager entityManager, SpriteBatch spriteBatch, ModularEffectRenderResources resources) : base(entityManager)
		{
			_spriteBatch = spriteBatch;
			_resources = resources ?? throw new ArgumentNullException(nameof(resources));
			_pixel = resources.Pixel;
		}

		protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();
		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			if (!IsActive) return;
			var transform = EnsureBattlePresentationTransform();
			transform.Offset = Vector2.Zero;
			transform.Scale = Vector2.One;

			foreach (var entity in EntityManager.GetEntitiesWithComponent<ActiveVisualEffect>())
			{
				var effect = entity.GetComponent<ActiveVisualEffect>();
				if (effect == null || effect.ElapsedSeconds < 0f) continue;
				if (effect.Recipe.HasModule(VisualEffectModule.Shake))
				{
					transform.Offset += ComputeImpactShake(effect) * ShakePixels * Math.Max(0f, effect.Recipe.Intensity);
				}
				if (effect.Recipe.HasModule(VisualEffectModule.PunchZoom))
				{
					float pulse = ComputePunchScale(effect, Math.Max(0f, effect.Recipe.Intensity));
					transform.Scale = new Vector2(Math.Max(transform.Scale.X, pulse), Math.Max(transform.Scale.Y, pulse));
				}
			}
		}

		public void Draw()
		{
			foreach (var entity in EntityManager.GetEntitiesWithComponent<ActiveVisualEffect>())
			{
				var effect = entity.GetComponent<ActiveVisualEffect>();
				if (effect == null || effect.ElapsedSeconds < 0f) continue;
				var recipe = effect.Recipe;
				if (recipe.HasModule(VisualEffectModule.WhiteWash)) DrawWhiteWash(effect);
				if (recipe.HasModule(VisualEffectModule.RedVignette)) DrawRedVignette(effect);
				if (recipe.HasModule(VisualEffectModule.Shockwave)) DrawShockwave(effect);
				if (recipe.HasModule(VisualEffectModule.SlashBand)) DrawSlashBand(effect);
				if (recipe.HasModule(VisualEffectModule.SmokeScreen)) DrawSmokeScreen(effect);
			}
		}

		private void DrawWhiteWash(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float p = VisualEffectDisplayMath.Progress(effect);
			float approach = VisualEffectDisplayMath.ApproachProgress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float pulse = recovery <= 0f ? approach : 1f - VisualEffectDisplayMath.EaseOutCubic(recovery);
			float alpha = WhiteWashAlpha * effect.Recipe.Intensity * pulse;
			if (alpha <= 0f) return;
			float scale = MathHelper.Lerp(0.35f, 1.55f, VisualEffectDisplayMath.EaseOutCubic(p));
			DrawSoftCircle(effect.ImpactAnchor, 1040f * scale, colors.Primary, alpha * 0.82f, 0f, 0.52f);
			DrawSoftCircle(effect.ImpactAnchor, 480f * scale, Color.White, alpha * 0.62f, 0f, 0.34f);
		}

		private void DrawRedVignette(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float approach = VisualEffectDisplayMath.ApproachProgress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float window = recovery <= 0f ? approach * approach : 1f - recovery;
			float alpha = RedVignetteAlpha * effect.Recipe.Intensity * window;
			if (alpha <= 0f) return;

			_spriteBatch.Draw(_pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), colors.Primary * (alpha * 0.28f));
			_spriteBatch.Draw(_pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.Black * (alpha * 0.22f));
			DrawSoftCircle(effect.ImpactAnchor, 860f, colors.Glow, alpha * 0.22f, 0.08f, 1f);
		}

		private void DrawShockwave(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = ShockwaveRingAlpha * effect.Recipe.Intensity * (1f - VisualEffectDisplayMath.EaseOutCubic(recovery));
			if (alpha <= 0f) return;
			float scale = MathHelper.Lerp(0.32f, 12.5f, VisualEffectDisplayMath.EaseOutCubic(recovery));
			var ring = _resources.ShockwaveRingMask;
			DrawMask(ring, effect.ImpactAnchor, Color.White * (alpha * 0.88f), 0f, new Vector2(scale));
			DrawMask(ring, effect.ImpactAnchor, colors.Glow * (alpha * 0.34f), 0f, new Vector2(scale * 1.38f));
		}

		private void DrawSlashBand(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float approach = VisualEffectDisplayMath.ApproachProgress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alphaShape = recovery <= 0f ? VisualEffectDisplayMath.EaseOutCubic(approach) : 1f - VisualEffectDisplayMath.EaseOutCubic(recovery);
			float alpha = SlashBandAlpha * effect.Recipe.Intensity * alphaShape;
			if (alpha <= 0f) return;

			float sign = Math.Sign(effect.DirectionSign == 0 ? 1 : effect.DirectionSign);
			var targetDirection = VisualEffectDisplayMath.DirectionToTarget(effect);
			float angle = MathF.Atan2(targetDirection.Y, targetDirection.X) + MathHelper.ToRadians(-28f * sign);
			float travel = recovery <= 0f
				? MathHelper.Lerp(-SlashBandLength * 0.46f, 0f, VisualEffectDisplayMath.EaseOutCubic(approach))
				: MathHelper.Lerp(0f, SlashBandLength * 0.36f, VisualEffectDisplayMath.EaseOutCubic(recovery));
			float length = SlashBandLength * MathHelper.Lerp(0.42f, 1f, VisualEffectDisplayMath.EaseOutCubic(approach));
			var center = effect.ImpactAnchor + Axis(angle) * travel;
			DrawSlashGradient(center, angle, length, SlashBandThickness, alpha, colors);
			DrawLine(center - Axis(angle) * length * 0.43f, center + Axis(angle) * length * 0.43f, Color.White * (alpha * 0.72f), SlashBandThickness * 0.13f);
		}

		private void DrawSmokeScreen(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float p = VisualEffectDisplayMath.Progress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = Math.Min(0.68f, SmokeScreenAlpha * effect.Recipe.Intensity) * (recovery <= 0f ? VisualEffectDisplayMath.ApproachProgress(effect) : 1f - recovery);
			if (alpha <= 0f) return;
			float lift = MathHelper.Lerp(14f, -12f, VisualEffectDisplayMath.EaseOutCubic(p));
			float scale = MathHelper.Lerp(0.88f, 1.16f, p);
			var center = effect.ImpactAnchor + new Vector2(0f, lift);
			DrawSoftCircle(center, 620f * scale, colors.Smoke, alpha * 0.66f, 0f, 0.52f);
			DrawSoftCircle(center + new Vector2(140f * effect.DirectionSign, -112f), 480f * scale, colors.Primary, alpha * 0.34f, 0f, 0.56f);
			DrawSoftCircle(center + new Vector2(-110f * effect.DirectionSign, 132f), 440f * scale, colors.Shadow, alpha * 0.44f, 0f, 0.52f);
			DrawSoftCircle(center + new Vector2(0f, -80f), 370f * scale, colors.Glow, alpha * 0.13f, 0.22f, 0.82f);
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

		private static Vector2 ComputeImpactShake(ActiveVisualEffect effect)
		{
			float approach = VisualEffectDisplayMath.ApproachProgress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			if (recovery <= 0f) return approach < 0.82f ? Vector2.Zero : new Vector2(-0.22f, 0.12f) * ((approach - 0.82f) / 0.18f);
			float envelope = MathF.Pow(1f - recovery, 1.7f);
			float x = MathF.Sin(recovery * MathHelper.Pi * 13f);
			float y = MathF.Cos(recovery * MathHelper.Pi * 17f) * 0.68f;
			return new Vector2(x, y) * envelope;
		}

		private float ComputePunchScale(ActiveVisualEffect effect, float intensity)
		{
			float approach = VisualEffectDisplayMath.ApproachProgress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float shape;
			if (recovery <= 0f) shape = MathHelper.Lerp(-0.18f, 1f, MathF.Pow(approach, 3f));
			else if (recovery < 0.22f) shape = MathHelper.Lerp(1f, -0.42f, VisualEffectDisplayMath.EaseOutCubic(recovery / 0.22f));
			else shape = MathHelper.Lerp(-0.42f, 0f, VisualEffectDisplayMath.EaseInOutQuad((recovery - 0.22f) / 0.78f));
			return 1f + shape * PunchZoomAmount * intensity;
		}

		private void DrawSlashGradient(Vector2 center, float rotation, float length, float thickness, float alpha, VisualEffectColors colors)
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
				Color color = mid > 0.62f ? colors.Primary : colors.Highlight;
				var start = center + axis * ((t0 - 0.5f) * length);
				var end = center + axis * ((t1 - 0.5f) * length);
				DrawLine(start, end, color * (alpha * stripAlpha), thickness);
			}
		}

		private void DrawSoftCircle(Vector2 center, float diameter, Color color, float alpha, float innerStop, float outerStop)
		{
			var texture = _resources.GetRadialMask(innerStop, outerStop);
			float scale = Math.Max(1f, diameter) / texture.Width;
			DrawMask(texture, center, color * alpha, 0f, new Vector2(scale));
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

		private static VisualEffectColors Colors(ActiveVisualEffect effect)
		{
			return VisualEffectPaletteResolver.Resolve(effect?.Recipe?.Palette ?? VisualEffectPalette.Physical);
		}
	}
}
