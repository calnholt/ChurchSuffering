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
	[DebugTab("Modular FX Primitives")]
	public sealed class ModularEffectPrimitiveDisplaySystem : Core.System
	{
		private static readonly Color Cream = new(255, 245, 223);
		private static readonly Color Red = new(199, 34, 50);
		private static readonly Color RedBright = new(239, 52, 72);
		private static readonly Color Gold = new(217, 182, 99);
		private static readonly Color Steel = new(143, 150, 160);
		private static readonly Color RockDark = new(43, 36, 33);

		private static readonly Vector2[] SwordArcMask =
		{
			new(0.00f, 0.48f),
			new(0.10f, 0.30f),
			new(0.48f, 0.40f),
			new(0.94f, 0.00f),
			new(1.00f, 0.14f),
			new(0.58f, 0.62f),
			new(0.12f, 0.72f)
		};

		private static readonly Vector2[] JaggedShardMask =
		{
			new(0.50f, 0.00f),
			new(1.00f, 0.76f),
			new(0.62f, 1.00f),
			new(0.00f, 0.42f)
		};

		private static readonly Vector2[] CrackMask =
		{
			new(0.00f, 0.50f),
			new(0.10f, 0.14f),
			new(0.24f, 0.54f),
			new(0.38f, 0.20f),
			new(0.52f, 0.62f),
			new(0.66f, 0.22f),
			new(0.82f, 0.58f),
			new(1.00f, 0.34f),
			new(0.94f, 0.78f),
			new(0.76f, 0.54f),
			new(0.62f, 0.88f),
			new(0.44f, 0.58f),
			new(0.30f, 0.92f),
			new(0.16f, 0.60f),
			new(0.00f, 0.82f)
		};

		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly Texture2D _pixel;

		[DebugEditable(DisplayName = "Primitive Alpha", Step = 0.01f, Min = 0f, Max = 2f)]
		public float PrimitiveAlpha { get; set; } = 1.0f;

		[DebugEditable(DisplayName = "Glow Alpha", Step = 0.01f, Min = 0f, Max = 2f)]
		public float GlowAlpha { get; set; } = 0.72f;

		[DebugEditable(DisplayName = "Glow Passes", Step = 1f, Min = 0f, Max = 8f)]
		public int GlowPasses { get; set; } = 3;

		[DebugEditable(DisplayName = "Slash Thickness Scale", Step = 0.01f, Min = 0.25f, Max = 4f)]
		public float SlashThicknessScale { get; set; } = 1.0f;

		[DebugEditable(DisplayName = "Ring Thickness", Step = 1f, Min = 1f, Max = 40f)]
		public float RingThickness { get; set; } = 5f;

		[DebugEditable(DisplayName = "Beam Alpha", Step = 0.01f, Min = 0f, Max = 2f)]
		public float BeamAlpha { get; set; } = 0.74f;

		[DebugEditable(DisplayName = "Crack Alpha", Step = 0.01f, Min = 0f, Max = 2f)]
		public float CrackAlpha { get; set; } = 0.94f;

		public ModularEffectPrimitiveDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch) : base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_pixel = new Texture2D(graphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });
		}

		protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();
		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public void Draw()
		{
			foreach (var effect in GetActiveEffects())
			{
				var modules = effect.Recipe.Modules;
				if (modules.Contains(VisualEffectModule.SwordArc)) DrawSwordArc(effect);
				if (modules.Contains(VisualEffectModule.CrossSlash)) DrawCrossSlash(effect);
				if (modules.Contains(VisualEffectModule.ClawSlash)) DrawClawSlash(effect);
				if (modules.Contains(VisualEffectModule.Bite)) DrawBite(effect);
				if (modules.Contains(VisualEffectModule.RockBlast)) DrawRockBlast(effect);
				if (modules.Contains(VisualEffectModule.HammerArc)) DrawHammerArc(effect);
				if (modules.Contains(VisualEffectModule.CrossBloom)) DrawCrossBloom(effect);
				if (modules.Contains(VisualEffectModule.Ring)) DrawRing(effect);
				if (modules.Contains(VisualEffectModule.Halo)) DrawHalo(effect);
				if (modules.Contains(VisualEffectModule.Beam)) DrawBeam(effect);
				if (modules.Contains(VisualEffectModule.Rays)) DrawRays(effect);
				if (modules.Contains(VisualEffectModule.Cracks)) DrawCracks(effect);
				if (modules.Contains(VisualEffectModule.HitFlash)) DrawHitFlash(effect);
			}
		}

		private IEnumerable<ActiveVisualEffect> GetActiveEffects()
		{
			return EntityManager.GetEntitiesWithComponent<ActiveVisualEffect>()
				.Select(e => e.GetComponent<ActiveVisualEffect>())
				.Where(e => e != null);
		}

		private void DrawSwordArc(ActiveVisualEffect effect)
		{
			float p = VisualEffectDisplayMath.Progress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * VisualEffectDisplayMath.Window(p, 0.05f, 0.18f, 0.46f, 1f);
			if (alpha <= 0f) return;

			float eased = VisualEffectDisplayMath.EaseOutCubic(p);
			float travel = MathHelper.Lerp(-118f, 86f, eased);
			float scaleX = p < 0.46f ? MathHelper.Lerp(0.2f, 1f, MathHelper.Clamp(p / 0.34f, 0f, 1f)) : MathHelper.Lerp(1f, 0.92f, MathHelper.Clamp((p - 0.46f) / 0.54f, 0f, 1f));
			float rotation = MathHelper.ToRadians(-28f * Math.Sign(effect.DirectionSign == 0 ? 1 : effect.DirectionSign));
			var axis = new Vector2(MathF.Cos(rotation), MathF.Sin(rotation));
			var center = effect.ImpactAnchor + axis * travel;
			var mask = PrimitiveTextureFactory.GetAntialiasedPolygonMask(_graphicsDevice, 280, 64, "modular_fx_sword_arc", SwordArcMask);

			DrawMask(mask, center, RedBright * (alpha * GlowAlpha), rotation, new Vector2(scaleX * 1.16f, 1.55f));
			DrawMask(mask, center, Cream * alpha, rotation, new Vector2(scaleX, 1f));
			DrawGlowLine(
				center + Axis(rotation) * -72f + Perp(rotation) * 14f,
				center + Axis(rotation) * 84f - Perp(rotation) * 12f,
				RedBright,
				8f * SlashThicknessScale,
				alpha * 0.65f);
		}

		private void DrawCrossSlash(ActiveVisualEffect effect)
		{
			float p = VisualEffectDisplayMath.Progress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * VisualEffectDisplayMath.Window(p, 0.06f, 0.16f, 0.56f, 1f);
			if (alpha <= 0f) return;

			float aAlpha = alpha * VisualEffectDisplayMath.Window(p, 0.06f, 0.30f, 0.52f, 1f);
			float bAlpha = alpha * VisualEffectDisplayMath.Window(p, 0.18f, 0.42f, 0.56f, 1f);
			DrawTravelingSlash(effect.ImpactAnchor, 34f, -70f, 44f, p, aAlpha);
			DrawTravelingSlash(effect.ImpactAnchor, -34f, 70f, -44f, p, bAlpha);
		}

		private void DrawClawSlash(ActiveVisualEffect effect)
		{
			float p = VisualEffectDisplayMath.Progress(effect);
			float groupAlpha = PrimitiveAlpha * effect.Recipe.Intensity * VisualEffectDisplayMath.Window(p, 0.08f, 0.18f, 0.62f, 1f);
			if (groupAlpha <= 0f) return;

			var rows = new[]
			{
				(y: -42f, angle: 24f, start: 0.08f, peak: 0.30f, end: 0.54f),
				(y: 0f, angle: 18f, start: 0.14f, peak: 0.36f, end: 0.58f),
				(y: 42f, angle: 12f, start: 0.20f, peak: 0.42f, end: 0.62f)
			};

			foreach (var row in rows)
			{
				float alpha = groupAlpha * VisualEffectDisplayMath.Window(p, row.start, row.peak, row.end, 1f);
				if (alpha <= 0f) continue;
				float travel = MathHelper.Lerp(86f, -52f, VisualEffectDisplayMath.EaseOutCubic(p)) * -effect.DirectionSign;
				var center = effect.ImpactAnchor + new Vector2(travel, row.y);
				DrawGlowSlash(center, row.angle * effect.DirectionSign, 190f, 16f * SlashThicknessScale, alpha);
			}
		}

		private void DrawBite(ActiveVisualEffect effect)
		{
			float p = VisualEffectDisplayMath.Progress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * VisualEffectDisplayMath.Window(p, 0.08f, 0.24f, 0.58f, 1f);
			if (alpha <= 0f) return;

			float scale = p < 0.24f ? MathHelper.Lerp(1.32f, 0.98f, p / 0.24f) : MathHelper.Lerp(0.98f, 1.08f, MathHelper.Clamp((p - 0.58f) / 0.42f, 0f, 1f));
			var ring = PrimitiveTextureFactory.GetAntialiasedRingMask(_graphicsDevice, 210, 170, 4f);
			DrawMask(ring, effect.ImpactAnchor, RedBright * (alpha * 0.58f), 0f, new Vector2(scale));
			DrawMask(ring, effect.ImpactAnchor, Color.Black * (alpha * 0.26f), 0f, new Vector2(scale * 0.9f));

			float close = VisualEffectDisplayMath.EaseOutCubic(MathHelper.Clamp((p - 0.08f) / 0.16f, 0f, 1f));
			float topY = MathHelper.Lerp(-48f, 0f, close);
			float bottomY = MathHelper.Lerp(48f, 0f, close);
			DrawToothRow(effect.ImpactAnchor + new Vector2(0f, -40f + topY), true, alpha);
			DrawToothRow(effect.ImpactAnchor + new Vector2(0f, 40f + bottomY), false, alpha);
		}

		private void DrawRockBlast(ActiveVisualEffect effect)
		{
			float p = VisualEffectDisplayMath.Progress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * VisualEffectDisplayMath.Window(p, 0.10f, 0.24f, 0.48f, 1f);
			if (alpha <= 0f) return;

			float scale = p < 0.24f ? MathHelper.Lerp(0.22f, 1.02f, VisualEffectDisplayMath.EaseOutBack(p / 0.24f)) : MathHelper.Lerp(1.02f, 1.9f, MathHelper.Clamp((p - 0.48f) / 0.52f, 0f, 1f));
			DrawSoftCircle(effect.ImpactAnchor, 190f * scale, Cream, alpha * 0.72f, 0.0f, 0.20f);
			DrawSoftCircle(effect.ImpactAnchor, 190f * scale, Gold, alpha * 0.50f, 0.12f, 0.46f);
			DrawSoftCircle(effect.ImpactAnchor, 190f * scale, RockDark, alpha * 0.44f, 0.26f, 0.80f);

			var chunk = PrimitiveTextureFactory.GetAntialiasedPolygonMask(_graphicsDevice, 42, 38, "modular_fx_rock_chunk", JaggedShardMask);
			float chunkT = VisualEffectDisplayMath.EaseOutCubic(MathHelper.Clamp((p - 0.14f) / 0.86f, 0f, 1f));
			DrawMask(chunk, effect.ImpactAnchor + new Vector2(-76f, -54f) * chunkT, RockDark * alpha, MathHelper.ToRadians(-96f * chunkT), new Vector2(MathHelper.Lerp(0.5f, 1.08f, chunkT)));
			DrawMask(chunk, effect.ImpactAnchor + new Vector2(88f, 62f) * chunkT, RockDark * alpha, MathHelper.ToRadians(118f * chunkT), new Vector2(MathHelper.Lerp(0.5f, 1.15f, chunkT)));
		}

		private void DrawHammerArc(ActiveVisualEffect effect)
		{
			float p = VisualEffectDisplayMath.Progress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * VisualEffectDisplayMath.Window(p, 0f, 0.10f, 0.84f, 1f);
			if (alpha <= 0f) return;

			float swing = MathHelper.Lerp(-110f * effect.DirectionSign, 3f * effect.DirectionSign, VisualEffectDisplayMath.EaseOutCubic(MathHelper.Clamp(p / 0.84f, 0f, 1f)));
			float baseAngle = MathF.Atan2(effect.ImpactAnchor.Y - effect.SourceAnchor.Y, effect.ImpactAnchor.X - effect.SourceAnchor.X);
			float rotation = baseAngle + MathHelper.ToRadians(swing);
			var pivot = effect.SourceAnchor + (effect.ImpactAnchor - effect.SourceAnchor) * 0.55f;
			DrawHammerPart(pivot, rotation, new Vector2(66f, 0f), new Vector2(132f, 14f), alpha * 0.95f, 8f);
			DrawHammerPart(pivot, rotation, new Vector2(172f, 0f), new Vector2(112f, 48f), alpha, 3f);
		}

		private void DrawCrossBloom(ActiveVisualEffect effect)
		{
			float p = VisualEffectDisplayMath.Progress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * VisualEffectDisplayMath.Window(p, 0.12f, 0.38f, 0.38f, 1f);
			if (alpha <= 0f) return;
			float scale = p < 0.38f ? MathHelper.Lerp(0.22f, 1f, p / 0.38f) : MathHelper.Lerp(1f, 1.65f, MathHelper.Clamp((p - 0.38f) / 0.62f, 0f, 1f));
			var center = effect.SourceAnchor + (effect.TargetAnchor - effect.SourceAnchor) * 0.16f;
			DrawGlowRect(center, new Vector2(18f, 112f) * scale, Cream, alpha, 0f);
			DrawGlowRect(center, new Vector2(94f, 16f) * scale, Cream, alpha, 0f);
		}

		private void DrawRing(ActiveVisualEffect effect)
		{
			float p = VisualEffectDisplayMath.Progress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * VisualEffectDisplayMath.Window(p, 0.08f, 0.20f, 0.20f, 1f);
			if (alpha <= 0f) return;
			float scale = MathHelper.Lerp(0.24f, 4.3f, VisualEffectDisplayMath.EaseOutCubic(p));
			var ring = PrimitiveTextureFactory.GetAntialiasedRingMask(_graphicsDevice, 80, 80, RingThickness);
			DrawMask(ring, effect.ImpactAnchor, Cream * alpha, 0f, new Vector2(scale));
			DrawMask(ring, effect.ImpactAnchor, Red * (alpha * 0.18f), 0f, new Vector2(scale * 1.12f));
		}

		private void DrawHalo(ActiveVisualEffect effect)
		{
			float p = VisualEffectDisplayMath.Progress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * VisualEffectDisplayMath.Window(p, 0f, 0.24f, 0.56f, 1f) * 0.95f;
			if (alpha <= 0f) return;
			float y = MathHelper.Lerp(48f, -58f, VisualEffectDisplayMath.EaseOutCubic(p));
			float scale = MathHelper.Lerp(0.58f, 1.16f, p);
			var halo = PrimitiveTextureFactory.GetAntialiasedRingMask(_graphicsDevice, 160, 44, 5f);
			DrawMask(halo, effect.SourceAnchor + new Vector2(0f, -120f + y), Cream * alpha, 0f, new Vector2(scale));
		}

		private void DrawBeam(ActiveVisualEffect effect)
		{
			float p = VisualEffectDisplayMath.Progress(effect);
			float alpha = BeamAlpha * effect.Recipe.Intensity * VisualEffectDisplayMath.Window(p, 0f, 0.28f, 0.60f, 1f);
			if (alpha <= 0f) return;
			float scaleY = MathHelper.Lerp(0.35f, 1.02f, VisualEffectDisplayMath.EaseOutCubic(p));
			DrawHorizontalGradientBand(effect.ImpactAnchor.X - 60f, effect.ImpactAnchor.Y - 340f * scaleY, 120f, 580f * scaleY, Cream, alpha, 18);
		}

		private void DrawRays(ActiveVisualEffect effect)
		{
			float p = VisualEffectDisplayMath.Progress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * VisualEffectDisplayMath.Window(p, 0f, 0.28f, 0.60f, 1f) * 0.58f;
			if (alpha <= 0f) return;
			float scale = MathHelper.Lerp(0.4f, 1.5f, VisualEffectDisplayMath.EaseOutCubic(p));
			float spin = MathHelper.ToRadians(MathHelper.Lerp(-18f, 36f, p));
			for (int i = 0; i < 12; i++)
			{
				float angle = spin + i * MathHelper.TwoPi / 12f;
				var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
				float length = 155f * scale * (i % 3 == 0 ? 1f : 0.68f);
				DrawGlowLine(effect.ImpactAnchor + dir * 18f, effect.ImpactAnchor + dir * length, i % 4 == 0 ? RedBright : Cream, 8f, alpha * (i % 4 == 0 ? 0.45f : 0.72f));
			}
		}

		private void DrawCracks(ActiveVisualEffect effect)
		{
			float p = VisualEffectDisplayMath.Progress(effect);
			float alpha = CrackAlpha * effect.Recipe.Intensity * VisualEffectDisplayMath.Window(p, 0.14f, 0.26f, 0.66f, 1f);
			if (alpha <= 0f) return;
			float grow = VisualEffectDisplayMath.EaseOutCubic(MathHelper.Clamp((p - 0.14f) / 0.12f, 0f, 1f));
			float[] baseAngles = { -46f, -22f, 4f, 28f, 54f };
			var crack = PrimitiveTextureFactory.GetAntialiasedPolygonMask(_graphicsDevice, 138, 8, "modular_fx_crack_segment", CrackMask);
			for (int i = 0; i < baseAngles.Length; i++)
			{
				float angle = (effect.DirectionSign < 0 ? 180f - baseAngles[i] : baseAngles[i]) + (i - 2) * 3f;
				float lenScale = MathHelper.Lerp(0.52f, 1f, i / 4f) * grow;
				var center = effect.ImpactAnchor + new Vector2(0f, 8f) + Axis(MathHelper.ToRadians(angle)) * (34f * lenScale);
				DrawMask(crack, center, RedBright * alpha, MathHelper.ToRadians(angle), new Vector2(lenScale, 1f));
				if (i > 0)
				{
					float branchAngle = angle + (i % 2 == 0 ? 34f : -34f);
					var branchCenter = center + Axis(MathHelper.ToRadians(angle)) * 18f;
					DrawMask(crack, branchCenter, RedBright * (alpha * 0.72f), MathHelper.ToRadians(branchAngle), new Vector2(lenScale * 0.42f, 0.68f));
				}
			}
		}

		private void DrawHitFlash(ActiveVisualEffect effect)
		{
			float p = VisualEffectDisplayMath.Progress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * VisualEffectDisplayMath.Window(p, 0.14f, 0.22f, 0.22f, 1f) * 0.94f;
			if (alpha <= 0f) return;
			float scale = MathHelper.Lerp(0.35f, 1.9f, VisualEffectDisplayMath.EaseOutCubic(p));
			DrawSoftCircle(effect.ImpactAnchor, 130f * scale, Color.White, alpha, 0f, 0.62f);
			DrawSoftCircle(effect.ImpactAnchor, 130f * scale, Cream, alpha * 0.34f, 0.22f, 1f);
		}

		private void DrawTravelingSlash(Vector2 center, float angleDegrees, float startTravel, float endTravel, float progress, float alpha)
		{
			if (alpha <= 0f) return;
			float rotation = MathHelper.ToRadians(angleDegrees);
			float travel = MathHelper.Lerp(startTravel, endTravel, VisualEffectDisplayMath.EaseOutCubic(progress));
			DrawGlowSlash(center + Axis(rotation) * travel, angleDegrees, 260f, 18f * SlashThicknessScale, alpha);
		}

		private void DrawGlowSlash(Vector2 center, float angleDegrees, float length, float thickness, float alpha)
		{
			float rotation = MathHelper.ToRadians(angleDegrees);
			var half = Axis(rotation) * (length * 0.5f);
			DrawGlowLine(center - half, center + half, RedBright, thickness * 1.9f, alpha * GlowAlpha);
			DrawGlowLine(center - half * 0.86f, center + half * 0.86f, Cream, thickness, alpha);
		}

		private void DrawGlowLine(Vector2 start, Vector2 end, Color color, float thickness, float alpha)
		{
			if (alpha <= 0f) return;
			int passes = Math.Max(0, GlowPasses);
			for (int i = passes; i >= 1; i--)
			{
				float pass = i / (float)Math.Max(1, passes);
				DrawLine(start, end, color * (alpha * 0.16f * pass), thickness + i * 9f);
			}
			DrawLine(start, end, color * alpha, thickness);
		}

		private void DrawToothRow(Vector2 center, bool top, float alpha)
		{
			var tooth = PrimitiveTextureFactory.GetAntialiasedPolygonMask(
				_graphicsDevice,
				18,
				36,
				top ? "modular_fx_tooth_top" : "modular_fx_tooth_bottom",
				top
					? new[] { new Vector2(0.10f, 0f), new Vector2(0.90f, 0f), new Vector2(0.50f, 1f) }
					: new[] { new Vector2(0.50f, 0f), new Vector2(0.90f, 1f), new Vector2(0.10f, 1f) });
			for (int i = 0; i < 6; i++)
			{
				float x = (i - 2.5f) * 23f;
				DrawMask(tooth, center + new Vector2(x, 0f), Cream * (alpha * 0.95f), 0f, Vector2.One);
			}
		}

		private void DrawHammerPart(Vector2 pivot, float rotation, Vector2 localCenter, Vector2 size, float alpha, float radius)
		{
			var center = pivot + Rotate(localCenter, rotation);
			DrawGlowRect(center, size + new Vector2(6f), Cream, alpha * 0.16f, rotation);
			DrawRotatedRect(center, size, RockDark * alpha, rotation);
			DrawRotatedRect(center, new Vector2(size.X + radius, 2f), Cream * (alpha * 0.12f), rotation);
		}

		private void DrawGlowRect(Vector2 center, Vector2 size, Color color, float alpha, float rotation)
		{
			for (int i = Math.Max(1, GlowPasses); i >= 1; i--)
			{
				DrawRotatedRect(center, size + new Vector2(i * 10f), color * (alpha * 0.10f), rotation);
			}
			DrawRotatedRect(center, size, color * alpha, rotation);
		}

		private void DrawHorizontalGradientBand(float x, float y, float width, float height, Color color, float alpha, int strips)
		{
			strips = Math.Max(1, strips);
			for (int i = 0; i < strips; i++)
			{
				float t0 = i / (float)strips;
				float t1 = (i + 1) / (float)strips;
				float center = (t0 + t1) * 0.5f;
				float stripAlpha = 1f - Math.Abs(center - 0.5f) / 0.5f;
				stripAlpha *= stripAlpha;
				var rect = new Rectangle(
					(int)MathF.Round(x + t0 * width),
					(int)MathF.Round(y),
					Math.Max(1, (int)MathF.Ceiling(width / strips) + 1),
					Math.Max(1, (int)MathF.Round(height)));
				_spriteBatch.Draw(_pixel, rect, color * (alpha * stripAlpha));
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

		private void DrawRotatedRect(Vector2 center, Vector2 size, Color color, float rotation)
		{
			_spriteBatch.Draw(_pixel, center, null, color, rotation, new Vector2(0.5f, 0.5f), new Vector2(Math.Max(1f, size.X), Math.Max(1f, size.Y)), SpriteEffects.None, 0f);
		}

		private void DrawLine(Vector2 start, Vector2 end, Color color, float thickness)
		{
			var delta = end - start;
			float length = delta.Length();
			if (length <= 0.001f) return;
			float rotation = MathF.Atan2(delta.Y, delta.X);
			_spriteBatch.Draw(_pixel, start, null, color, rotation, new Vector2(0f, 0.5f), new Vector2(length, Math.Max(1f, thickness)), SpriteEffects.None, 0f);
		}

		private static Vector2 Axis(float rotation)
		{
			return new Vector2(MathF.Cos(rotation), MathF.Sin(rotation));
		}

		private static Vector2 Perp(float rotation)
		{
			return new Vector2(-MathF.Sin(rotation), MathF.Cos(rotation));
		}

		private static Vector2 Rotate(Vector2 value, float rotation)
		{
			float c = MathF.Cos(rotation);
			float s = MathF.Sin(rotation);
			return new Vector2(value.X * c - value.Y * s, value.X * s + value.Y * c);
		}
	}
}
