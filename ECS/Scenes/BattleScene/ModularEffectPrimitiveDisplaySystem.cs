using System;
using System.Collections.Generic;
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

		private readonly SpriteBatch _spriteBatch;
		private readonly ModularEffectRenderResources _resources;
		private readonly Texture2D _pixel;
		private Texture2D _editableRingMask;
		private float _ringThickness = 5f;

		[DebugEditable(DisplayName = "Primitive Alpha", Step = 0.01f, Min = 0f, Max = 2f)]
		public float PrimitiveAlpha { get; set; } = 1.0f;

		[DebugEditable(DisplayName = "Glow Alpha", Step = 0.01f, Min = 0f, Max = 2f)]
		public float GlowAlpha { get; set; } = 0.72f;

		[DebugEditable(DisplayName = "Glow Passes", Step = 1f, Min = 0f, Max = 8f)]
		public int GlowPasses { get; set; } = 3;

		[DebugEditable(DisplayName = "Slash Thickness Scale", Step = 0.01f, Min = 0.25f, Max = 4f)]
		public float SlashThicknessScale { get; set; } = 1.0f;

		[DebugEditable(DisplayName = "Ring Thickness", Step = 1f, Min = 1f, Max = 40f)]
		public float RingThickness
		{
			get => _ringThickness;
			set
			{
				_ringThickness = Math.Max(1f, value);
				if (_resources != null)
				{
					_editableRingMask = _resources.GetEditableRingMask(_ringThickness);
				}
			}
		}

		[DebugEditable(DisplayName = "Beam Alpha", Step = 0.01f, Min = 0f, Max = 2f)]
		public float BeamAlpha { get; set; } = 0.74f;

		[DebugEditable(DisplayName = "Crack Alpha", Step = 0.01f, Min = 0f, Max = 2f)]
		public float CrackAlpha { get; set; } = 0.94f;

		[DebugEditable(DisplayName = "Crack Reach", Step = 5f, Min = 40f, Max = 500f)]
		public float CrackReach { get; set; } = 255f;

		[DebugEditable(DisplayName = "Crack Trunks", Step = 1f, Min = 3f, Max = 14f)]
		public int CrackTrunkCount { get; set; } = 7;

		[DebugEditable(DisplayName = "Hammer Scale", Step = 0.05f, Min = 0.5f, Max = 2f)]
		public float HammerScale { get; set; } = 1.25f;

		[DebugEditable(DisplayName = "Hammer Wind-up Degrees", Step = 1f, Min = 60f, Max = 160f)]
		public float HammerWindupDegrees { get; set; } = 112f;

		[DebugEditable(DisplayName = "Hammer Acceleration", Step = 0.1f, Min = 1f, Max = 5f)]
		public float HammerAccelerationPower { get; set; } = 3f;

		[DebugEditable(DisplayName = "Hammer Follow-through Degrees", Step = 1f, Min = 0f, Max = 45f)]
		public float HammerFollowThroughDegrees { get; set; } = 18f;

		public ModularEffectPrimitiveDisplaySystem(EntityManager entityManager, SpriteBatch spriteBatch, ModularEffectRenderResources resources) : base(entityManager)
		{
			_spriteBatch = spriteBatch;
			_resources = resources ?? throw new ArgumentNullException(nameof(resources));
			_pixel = resources.Pixel;
			_editableRingMask = resources.DefaultRingMask;
		}

		protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();
		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public void Draw()
		{
			foreach (var entity in EntityManager.GetEntitiesWithComponent<ActiveVisualEffect>())
			{
				var effect = entity.GetComponent<ActiveVisualEffect>();
				if (effect == null || effect.ElapsedSeconds < 0f) continue;
				var recipe = effect.Recipe;
				if (recipe.HasModule(VisualEffectModule.SwordArc)) DrawSwordArc(effect);
				if (recipe.HasModule(VisualEffectModule.CrossSlash)) DrawCrossSlash(effect);
				if (recipe.HasModule(VisualEffectModule.ClawSlash)) DrawClawSlash(effect);
				if (recipe.HasModule(VisualEffectModule.Bite)) DrawBite(effect);
				if (recipe.HasModule(VisualEffectModule.RockBlast)) DrawRockBlast(effect);
				if (recipe.HasModule(VisualEffectModule.HammerArc)) DrawHammerArc(effect);
				if (recipe.HasModule(VisualEffectModule.CrossBloom)) DrawCrossBloom(effect);
				if (recipe.HasModule(VisualEffectModule.Ring)) DrawRing(effect);
				if (recipe.HasModule(VisualEffectModule.Halo)) DrawHalo(effect);
				if (recipe.HasModule(VisualEffectModule.Beam)) DrawBeam(effect);
				if (recipe.HasModule(VisualEffectModule.Rays)) DrawRays(effect);
				if (recipe.HasModule(VisualEffectModule.Cracks)) DrawCracks(effect);
				if (recipe.HasModule(VisualEffectModule.HitFlash)) DrawHitFlash(effect);
				if (recipe.HasModule(VisualEffectModule.ArrowShot)) DrawArrowShot(effect);
				if (recipe.HasModule(VisualEffectModule.ThrownBladeVolley)) DrawThrownBladeVolley(effect);
				if (recipe.HasModule(VisualEffectModule.EnergyBolt)) DrawEnergyBolt(effect);
				if (recipe.HasModule(VisualEffectModule.SpinSlash)) DrawSpinSlash(effect);
				if (recipe.HasModule(VisualEffectModule.FlameBurst)) DrawFlameBurst(effect);
				if (recipe.HasModule(VisualEffectModule.FrostBurst)) DrawFrostBurst(effect);
				if (recipe.HasModule(VisualEffectModule.ShadowTendrils)) DrawShadowTendrils(effect);
				if (recipe.HasModule(VisualEffectModule.PoisonCloud)) DrawPoisonCloud(effect);
				if (recipe.HasModule(VisualEffectModule.ShieldWard)) DrawShieldWard(effect);
				if (recipe.HasModule(VisualEffectModule.ShieldShatter)) DrawShieldShatter(effect);
				if (recipe.HasModule(VisualEffectModule.SoulSiphon)) DrawSoulSiphon(effect);
				if (recipe.HasModule(VisualEffectModule.ResourceMotes)) DrawResourceMotes(effect);
				if (recipe.HasModule(VisualEffectModule.SealStamp)) DrawSealStamp(effect);
				if (recipe.HasModule(VisualEffectModule.FrostBind)) DrawFrostBind(effect);
				if (recipe.HasModule(VisualEffectModule.BrittleFracture)) DrawBrittleFracture(effect);
				if (recipe.HasModule(VisualEffectModule.ColorDrain)) DrawColorDrain(effect);
			}
		}

		private void DrawSwordArc(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float approach = VisualEffectDisplayMath.ApproachProgress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * (recovery <= 0f ? VisualEffectDisplayMath.EaseOutCubic(approach) : 1f - VisualEffectDisplayMath.EaseOutCubic(recovery));
			if (alpha <= 0f) return;

			float travel = recovery <= 0f ? MathHelper.Lerp(-210f, 0f, VisualEffectDisplayMath.EaseOutCubic(approach)) : MathHelper.Lerp(0f, 120f, VisualEffectDisplayMath.EaseOutCubic(recovery));
			float scaleX = MathHelper.Lerp(0.28f, 1.25f, VisualEffectDisplayMath.EaseOutCubic(approach));
			float rotation = MathHelper.ToRadians(-28f * Math.Sign(effect.DirectionSign == 0 ? 1 : effect.DirectionSign));
			var axis = new Vector2(MathF.Cos(rotation), MathF.Sin(rotation));
			var center = effect.ImpactAnchor + axis * travel;
			var mask = _resources.SwordArcMask;

			DrawMask(mask, center, colors.Glow * (alpha * GlowAlpha), rotation, new Vector2(scaleX * 1.24f, 2.2f));
			DrawMask(mask, center, colors.Highlight * alpha, rotation, new Vector2(scaleX, 1.28f));
			DrawGlowLine(
				center + Axis(rotation) * -72f + Perp(rotation) * 14f,
				center + Axis(rotation) * 84f - Perp(rotation) * 12f,
				colors.Primary,
				8f * SlashThicknessScale,
				alpha * 0.65f);
		}

		private void DrawCrossSlash(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float approach = VisualEffectDisplayMath.ApproachProgress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * (recovery <= 0f ? approach : 1f - VisualEffectDisplayMath.EaseOutCubic(recovery));
			if (alpha <= 0f) return;

			float sign = effect.DirectionSign == 0 ? 1f : effect.DirectionSign;
			float second = MathHelper.Clamp((approach - 0.20f) / 0.80f, 0f, 1f);
			float firstTravel = recovery <= 0f ? MathHelper.Lerp(-150f, 0f, VisualEffectDisplayMath.EaseOutCubic(approach)) : MathHelper.Lerp(0f, 85f, recovery);
			float secondTravel = recovery <= 0f ? MathHelper.Lerp(150f, 0f, VisualEffectDisplayMath.EaseOutCubic(second)) : MathHelper.Lerp(0f, -85f, recovery);
			float firstAngle = 38f * sign;
			float secondAngle = -38f * sign;
			DrawGlowSlash(effect.ImpactAnchor + Axis(MathHelper.ToRadians(firstAngle)) * firstTravel, firstAngle, 390f, 24f * SlashThicknessScale, alpha, colors);
			DrawGlowSlash(effect.ImpactAnchor + Axis(MathHelper.ToRadians(secondAngle)) * secondTravel, secondAngle, 390f, 24f * SlashThicknessScale, alpha * MathHelper.Clamp(second * 1.8f, 0f, 1f), colors);
		}

		private void DrawClawSlash(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float approach = VisualEffectDisplayMath.ApproachProgress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float groupAlpha = PrimitiveAlpha * effect.Recipe.Intensity * (recovery <= 0f ? approach : 1f - VisualEffectDisplayMath.EaseOutCubic(recovery));
			if (groupAlpha <= 0f) return;

			var rows = new[]
			{
				(y: -58f, angle: 22f, delay: 0f),
				(y: 0f, angle: 16f, delay: 0.10f),
				(y: 58f, angle: 10f, delay: 0.20f)
			};

			foreach (var row in rows)
			{
				float stroke = MathHelper.Clamp((approach - row.delay) / (1f - row.delay), 0f, 1f);
				float alpha = groupAlpha * MathHelper.Clamp(stroke * 2f, 0f, 1f);
				if (alpha <= 0f) continue;
				float travel = MathHelper.Lerp(170f, recovery <= 0f ? 0f : -90f, VisualEffectDisplayMath.EaseOutCubic(recovery <= 0f ? stroke : recovery)) * -effect.DirectionSign;
				var center = effect.ImpactAnchor + new Vector2(travel, row.y);
				DrawGlowSlash(center, row.angle * effect.DirectionSign, 310f, 20f * SlashThicknessScale, alpha, colors);
			}
		}

		private void DrawBite(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float approach = VisualEffectDisplayMath.ApproachProgress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * (recovery <= 0f ? approach : 1f - recovery);
			if (alpha <= 0f) return;

			float scale = recovery <= 0f ? MathHelper.Lerp(1.48f, 1f, VisualEffectDisplayMath.EaseOutCubic(approach)) : MathHelper.Lerp(1f, 1.24f, recovery);
			var ring = _resources.BiteRingMask;
			DrawMask(ring, effect.ImpactAnchor, colors.Primary * (alpha * 0.44f), 0f, new Vector2(scale));
			DrawMask(ring, effect.ImpactAnchor, Color.Black * (alpha * 0.26f), 0f, new Vector2(scale * 0.9f));

			float jawOpen = recovery <= 0f ? 1f - VisualEffectDisplayMath.EaseOutCubic(approach) : VisualEffectDisplayMath.EaseOutCubic(recovery);
			float gap = MathHelper.Lerp(34f, 138f, jawOpen);
			DrawToothRow(effect.ImpactAnchor + new Vector2(-10f, -gap), true, alpha, colors);
			DrawToothRow(effect.ImpactAnchor + new Vector2(12f, gap), false, alpha, colors);
		}

		private void DrawRockBlast(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float approach = VisualEffectDisplayMath.ApproachProgress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * (recovery <= 0f ? approach : 1f - VisualEffectDisplayMath.EaseOutCubic(recovery));
			if (alpha <= 0f) return;

			float scale = recovery <= 0f ? MathHelper.Lerp(0.16f, 1.12f, VisualEffectDisplayMath.EaseOutBack(approach)) : MathHelper.Lerp(1.12f, 2.5f, recovery);
			DrawSoftCircle(effect.ImpactAnchor, 270f * scale, colors.Shadow, alpha * 0.72f, 0.0f, 0.86f);
			DrawSoftCircle(effect.ImpactAnchor, 220f * scale, colors.Primary, alpha * 0.68f, 0.0f, 0.64f);
			DrawSoftCircle(effect.ImpactAnchor, 130f * scale, colors.Highlight, alpha * 0.88f, 0.0f, 0.42f);

			var chunk = _resources.RockChunkMask;
			var variation = new VisualEffectVariation(effect);
			float chunkT = 0.20f + VisualEffectDisplayMath.EaseOutCubic(recovery) * 0.80f;
			for (int i = 0; i < 5; i++)
			{
				float angle = variation.Range(100 + i * 3, -165f, 165f);
				float distance = variation.Range(101 + i * 3, 130f, 330f) * chunkT;
				var offset = Axis(MathHelper.ToRadians(angle)) * distance;
				float chunkScale = variation.Range(102 + i * 3, 0.82f, 1.75f);
				DrawMask(chunk, effect.ImpactAnchor + offset, colors.Glow * (alpha * 0.28f), MathHelper.ToRadians(angle + 240f * chunkT), new Vector2(chunkScale * 1.28f));
				DrawMask(chunk, effect.ImpactAnchor + offset, colors.Shadow * alpha, MathHelper.ToRadians(angle + 240f * chunkT), new Vector2(chunkScale));
			}
		}

		private void DrawHammerArc(ActiveVisualEffect effect)
		{
			float p = VisualEffectDisplayMath.Progress(effect);
			float duration = Math.Max(0.0001f, effect.Timing.DurationSeconds);
			float contactSeconds = effect.Timing.HitStopDurationSeconds > 0f
				? effect.Timing.HitStopStartSeconds
				: effect.Timing.ImpactTimeSeconds;
			float contactProgress = MathHelper.Clamp(contactSeconds / duration, 0.05f, 0.85f);
			float followThroughEnd = Math.Min(0.62f, contactProgress + 0.28f);
			float fadeEnd = Math.Min(1f, followThroughEnd + 0.18f);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * VisualEffectDisplayMath.Window(p, 0f, 0.06f, followThroughEnd, fadeEnd);
			if (alpha <= 0f) return;

			int directionSign = effect.DirectionSign;
			if (directionSign == 0)
			{
				directionSign = effect.ImpactAnchor.X >= effect.SourceAnchor.X ? 1 : -1;
			}

			float baseAngle = MathF.Atan2(effect.ImpactAnchor.Y - effect.SourceAnchor.Y, effect.ImpactAnchor.X - effect.SourceAnchor.X);
			float contactRotation = baseAngle;
			float windupRotation = contactRotation - MathHelper.ToRadians(HammerWindupDegrees * directionSign);
			float rotation;
			if (p <= contactProgress)
			{
				float swingProgress = MathHelper.Clamp(p / contactProgress, 0f, 1f);
				float acceleratedSwing = MathF.Pow(swingProgress, HammerAccelerationPower);
				rotation = MathHelper.Lerp(windupRotation, contactRotation, acceleratedSwing);
			}
			else
			{
				float followThroughProgress = MathHelper.Clamp((p - contactProgress) / Math.Max(0.0001f, followThroughEnd - contactProgress), 0f, 1f);
				float followThrough = VisualEffectDisplayMath.EaseOutCubic(followThroughProgress);
				rotation = contactRotation + MathHelper.ToRadians(HammerFollowThroughDegrees * directionSign) * followThrough;
			}

			const float headCenterDistance = 270f;
			const float headHeight = 180f;
			float scale = HammerScale;
			var contactPoint = new Vector2(headCenterDistance, directionSign * headHeight * 0.5f) * scale;
			var pivot = effect.ImpactAnchor - Rotate(contactPoint, contactRotation);
			DrawHammer(pivot, rotation, scale, alpha);
		}

		private void DrawCrossBloom(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float approach = VisualEffectDisplayMath.ApproachProgress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * (recovery <= 0f ? approach : 1f - VisualEffectDisplayMath.EaseOutCubic(recovery));
			if (alpha <= 0f) return;
			float scale = recovery <= 0f ? MathHelper.Lerp(0.15f, 1.15f, VisualEffectDisplayMath.EaseOutBack(approach)) : MathHelper.Lerp(1.15f, 2.1f, recovery);
			var center = effect.ImpactAnchor;
			DrawGlowRect(center, new Vector2(30f, 230f) * scale, colors.Primary, alpha, 0f);
			DrawGlowRect(center, new Vector2(190f, 28f) * scale, colors.Primary, alpha, 0f);
			DrawGlowRect(center, new Vector2(12f, 330f) * scale, Color.White, alpha * 0.62f, 0f);
			DrawSoftCircle(center, 150f * scale, colors.Glow, alpha * 0.42f, 0f, 0.78f);
		}

		private void DrawRing(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float approach = VisualEffectDisplayMath.ApproachProgress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * (recovery <= 0f ? approach : 1f - VisualEffectDisplayMath.EaseOutCubic(recovery));
			if (alpha <= 0f) return;
			float scale = recovery <= 0f ? MathHelper.Lerp(0.18f, 1.1f, VisualEffectDisplayMath.EaseOutCubic(approach)) : MathHelper.Lerp(1.1f, 5.8f, VisualEffectDisplayMath.EaseOutCubic(recovery));
			var ring = _editableRingMask;
			DrawMask(ring, effect.ImpactAnchor, Color.White * alpha, 0f, new Vector2(scale));
			DrawMask(ring, effect.ImpactAnchor, colors.Primary * (alpha * 0.42f), 0f, new Vector2(scale * 1.18f));
		}

		private void DrawHalo(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float approach = VisualEffectDisplayMath.ApproachProgress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * (recovery <= 0f ? approach : 1f - recovery);
			if (alpha <= 0f) return;
			float y = MathHelper.Lerp(30f, -70f, VisualEffectDisplayMath.EaseOutCubic(VisualEffectDisplayMath.Progress(effect)));
			float scale = MathHelper.Lerp(0.65f, 1.4f, VisualEffectDisplayMath.EaseOutBack(approach));
			var halo = _resources.HaloRingMask;
			var center = effect.ImpactAnchor + new Vector2(0f, -190f + y);
			DrawMask(halo, center, colors.Glow * (alpha * 0.38f), 0f, new Vector2(scale * 1.22f));
			DrawMask(halo, center, colors.Highlight * alpha, 0f, new Vector2(scale));
		}

		private void DrawBeam(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float approach = VisualEffectDisplayMath.ApproachProgress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = BeamAlpha * effect.Recipe.Intensity * (recovery <= 0f ? approach : 1f - VisualEffectDisplayMath.EaseOutCubic(recovery));
			if (alpha <= 0f) return;
			float scaleY = MathHelper.Lerp(0.20f, 1.12f, VisualEffectDisplayMath.EaseOutCubic(approach));
			DrawHorizontalGradientBand(effect.ImpactAnchor.X - 120f, effect.ImpactAnchor.Y - 470f * scaleY, 240f, 820f * scaleY, colors.Primary, alpha, 28);
			DrawHorizontalGradientBand(effect.ImpactAnchor.X - 38f, effect.ImpactAnchor.Y - 520f * scaleY, 76f, 920f * scaleY, Color.White, alpha * 0.82f, 18);
		}

		private void DrawRays(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float approach = VisualEffectDisplayMath.ApproachProgress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * (recovery <= 0f ? approach : 1f - VisualEffectDisplayMath.EaseOutCubic(recovery)) * 0.82f;
			if (alpha <= 0f) return;
			float scale = MathHelper.Lerp(0.3f, 2.05f, VisualEffectDisplayMath.EaseOutCubic(VisualEffectDisplayMath.Progress(effect)));
			var variation = new VisualEffectVariation(effect);
			float spin = MathHelper.ToRadians(MathHelper.Lerp(-28f, 52f, VisualEffectDisplayMath.Progress(effect)) + variation.Range(8, -12f, 12f));
			int rayCount = variation.Range(9, 14, 19);
			for (int i = 0; i < rayCount; i++)
			{
				float angle = spin + i * MathHelper.TwoPi / rayCount + MathHelper.ToRadians(variation.Range(20 + i, -5f, 5f));
				var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
				float length = variation.Range(60 + i, 125f, 245f) * scale;
				float width = variation.Range(90 + i, 6f, 14f);
				DrawGlowLine(effect.ImpactAnchor + dir * 22f, effect.ImpactAnchor + dir * length, i % 4 == 0 ? colors.Primary : colors.Highlight, width, alpha * (i % 4 == 0 ? 0.55f : 0.82f));
			}
		}

		private void DrawCracks(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = CrackAlpha * effect.Recipe.Intensity * (1f - MathF.Pow(recovery, 1.5f));
			if (alpha <= 0f) return;
			float approach = VisualEffectDisplayMath.ApproachProgress(effect);
			float grow = recovery <= 0f
				? VisualEffectDisplayMath.EaseOutCubic(MathHelper.Clamp((approach - 0.72f) / 0.28f, 0f, 1f))
				: 1f;
			var variation = new VisualEffectVariation(effect);
			int trunks = Math.Max(3, CrackTrunkCount);
			for (int i = 0; i < trunks; i++)
			{
				float angle = i * 360f / trunks + variation.Range(200 + i, -18f, 18f);
				float reach = CrackReach * variation.Range(220 + i, 0.68f, 1.22f);
				int segments = variation.Range(240 + i, 3, 6);
				var start = effect.ImpactAnchor + new Vector2(0f, 8f);
				for (int segment = 0; segment < segments; segment++)
				{
					float segmentStart = segment / (float)segments;
					float segmentGrow = MathHelper.Clamp((grow - segmentStart) * segments, 0f, 1f);
					if (segmentGrow <= 0f) break;
					float jitter = variation.Range(300 + i * 10 + segment, -16f, 16f);
					float length = reach / segments * variation.Range(360 + i * 10 + segment, 0.78f, 1.18f);
					var end = start + Axis(MathHelper.ToRadians(angle + jitter)) * length * segmentGrow;
					DrawGlowLine(start, end, colors.Glow, MathHelper.Lerp(9f, 4f, segmentStart), alpha * (1f - segmentStart * 0.28f));
					if (segment > 0 && variation.Range(420 + i * 10 + segment, 0f, 1f) > 0.42f)
					{
						float branchAngle = angle + jitter + variation.Range(480 + i * 10 + segment, -58f, 58f);
						var branchEnd = start + Axis(MathHelper.ToRadians(branchAngle)) * length * 0.58f * segmentGrow;
						DrawGlowLine(start, branchEnd, colors.Primary, 3f, alpha * 0.64f);
					}
					start = end;
				}
			}
		}

		private void DrawHitFlash(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float approach = VisualEffectDisplayMath.ApproachProgress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * (recovery <= 0f ? MathF.Pow(approach, 4f) : 1f - VisualEffectDisplayMath.EaseOutCubic(recovery)) * 1.15f;
			if (alpha <= 0f) return;
			float scale = recovery <= 0f ? MathHelper.Lerp(0.18f, 1.15f, VisualEffectDisplayMath.EaseOutCubic(approach)) : MathHelper.Lerp(1.15f, 3.2f, recovery);
			DrawSoftCircle(effect.ImpactAnchor, 170f * scale, Color.White, alpha, 0f, 0.68f);
			DrawSoftCircle(effect.ImpactAnchor, 210f * scale, colors.Glow, alpha * 0.48f, 0.16f, 1f);
		}

		private void DrawArrowShot(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float approach = VisualEffectDisplayMath.ApproachProgress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * (recovery <= 0f ? 1f : 1f - recovery);
			var direction = VisualEffectDisplayMath.DirectionToTarget(effect);
			float travel = VisualEffectDisplayMath.EaseOutCubic(MathHelper.Clamp(approach * 1.18f, 0f, 1f));
			var tip = Vector2.Lerp(effect.SourceAnchor, effect.ImpactAnchor, travel);
			float rotation = MathF.Atan2(direction.Y, direction.X);
			var tail = tip - direction * 150f;
			DrawGlowLine(tail, tip, colors.Primary, 8f, alpha * 0.72f);
			DrawLine(tail, tip, colors.Highlight * alpha, 4f);
			DrawLine(tip, tip - Rotate(direction, 0.62f) * 34f, colors.Highlight * alpha, 7f);
			DrawLine(tip, tip - Rotate(direction, -0.62f) * 34f, colors.Highlight * alpha, 7f);
			DrawLine(tail, tail + Perp(rotation) * 28f - direction * 18f, colors.Primary * alpha, 5f);
			DrawLine(tail, tail - Perp(rotation) * 28f - direction * 18f, colors.Primary * alpha, 5f);
		}

		private void DrawThrownBladeVolley(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float approach = VisualEffectDisplayMath.ApproachProgress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			var blade = _resources.ThrownBladeMask;
			for (int i = 0; i < 5; i++)
			{
				float local = MathHelper.Clamp((approach - i * 0.075f) / 0.70f, 0f, 1f);
				float alpha = PrimitiveAlpha * effect.Recipe.Intensity * MathHelper.Clamp(local * 3f, 0f, 1f) * (1f - recovery);
				var pos = Vector2.Lerp(effect.SourceAnchor, effect.ImpactAnchor, VisualEffectDisplayMath.EaseOutCubic(local));
				pos += new Vector2(0f, (i - 2f) * 38f);
				float rotation = MathF.Atan2(effect.ImpactAnchor.Y - effect.SourceAnchor.Y, effect.ImpactAnchor.X - effect.SourceAnchor.X) + local * MathHelper.TwoPi * 2f;
				DrawMask(blade, pos, colors.Glow * (alpha * 0.35f), rotation, new Vector2(1.28f));
				DrawMask(blade, pos, colors.Highlight * alpha, rotation, Vector2.One);
			}
		}

		private void DrawEnergyBolt(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float approach = VisualEffectDisplayMath.ApproachProgress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float travel = VisualEffectDisplayMath.EaseOutCubic(MathHelper.Clamp(approach * 1.12f, 0f, 1f));
			var pos = Vector2.Lerp(effect.SourceAnchor, effect.ImpactAnchor, travel);
			var direction = VisualEffectDisplayMath.DirectionToTarget(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * (1f - recovery);
			DrawGlowLine(pos - direction * 210f, pos, colors.Primary, 22f, alpha * 0.42f);
			DrawSoftCircle(pos, 118f, colors.Glow, alpha * 0.55f, 0f, 0.82f);
			DrawSoftCircle(pos, 54f, colors.Highlight, alpha, 0f, 0.62f);
		}

		private void DrawSpinSlash(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float p = VisualEffectDisplayMath.Progress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * (1f - recovery);
			for (int i = 0; i < 4; i++)
			{
				float angle = p * MathHelper.TwoPi * 1.8f + i * MathHelper.PiOver2;
				var center = effect.ImpactAnchor + new Vector2(MathF.Cos(angle) * 92f, MathF.Sin(angle) * 52f);
				var tangent = new Vector2(-MathF.Sin(angle), MathF.Cos(angle));
				DrawGlowLine(center - tangent * 150f, center + tangent * 150f, colors.Primary, 18f, alpha * (1f - i * 0.13f));
				DrawLine(center - tangent * 112f, center + tangent * 112f, colors.Highlight * alpha, 5f);
			}
		}

		private void DrawFlameBurst(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * (1f - recovery);
			float scale = MathHelper.Lerp(0.35f, 1.9f, VisualEffectDisplayMath.EaseOutCubic(recovery));
			DrawSoftCircle(effect.ImpactAnchor, 230f * scale, colors.Primary, alpha * 0.48f, 0f, 0.72f);
			for (int i = 0; i < 9; i++)
			{
				float angle = -MathHelper.PiOver2 + (i - 4) * 0.28f;
				float length = 110f + (i % 3) * 42f;
				var start = effect.ImpactAnchor + Axis(angle) * 24f;
				var end = start + Axis(angle) * length * scale;
				DrawGlowLine(start, end, i % 2 == 0 ? colors.Highlight : colors.Primary, 18f, alpha * 0.75f);
			}
		}

		private void DrawFrostBurst(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * (1f - recovery);
			float grow = MathHelper.Lerp(0.35f, 1.35f, VisualEffectDisplayMath.EaseOutCubic(Math.Max(VisualEffectDisplayMath.ApproachProgress(effect), recovery)));
			for (int i = 0; i < 12; i++)
			{
				float angle = i * MathHelper.TwoPi / 12f;
				float length = (i % 2 == 0 ? 185f : 125f) * grow;
				DrawGlowLine(effect.ImpactAnchor + Axis(angle) * 28f, effect.ImpactAnchor + Axis(angle) * length, colors.Primary, i % 2 == 0 ? 15f : 9f, alpha);
			}
			DrawSoftCircle(effect.ImpactAnchor, 145f * grow, colors.Highlight, alpha * 0.62f, 0f, 0.72f);
		}

		private void DrawShadowTendrils(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float p = VisualEffectDisplayMath.Progress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * (1f - recovery);
			for (int i = 0; i < 7; i++)
			{
				float angle = i * MathHelper.TwoPi / 7f + p * 1.4f;
				Vector2 previous = effect.ImpactAnchor + Axis(angle) * 26f;
				for (int segment = 1; segment <= 5; segment++)
				{
					float reach = segment * 42f * (0.55f + p);
					float curl = MathF.Sin(p * 8f + i + segment) * 0.24f;
					var next = effect.ImpactAnchor + Axis(angle + curl) * reach;
					DrawGlowLine(previous, next, colors.Primary, MathHelper.Lerp(16f, 4f, segment / 5f), alpha * 0.7f);
					previous = next;
				}
			}
		}

		private void DrawPoisonCloud(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float p = VisualEffectDisplayMath.Progress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * (1f - recovery) * 0.72f;
			var variation = new VisualEffectVariation(effect);
			for (int i = 0; i < 10; i++)
			{
				float angle = variation.Range(900 + i, 0f, MathHelper.TwoPi);
				float distance = variation.Range(930 + i, 30f, 175f) * (0.55f + p * 0.45f);
				var center = effect.ImpactAnchor + Axis(angle) * distance + new Vector2(0f, -p * 55f);
				DrawSoftCircle(center, variation.Range(960 + i, 70f, 150f), i % 3 == 0 ? colors.Highlight : colors.Smoke, alpha, 0f, 0.82f);
			}
		}

		private void DrawShieldWard(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float approach = VisualEffectDisplayMath.ApproachProgress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * (recovery <= 0f ? approach : 1f - recovery);
			float scale = MathHelper.Lerp(0.55f, 1.18f, VisualEffectDisplayMath.EaseOutBack(approach));
			var shield = _resources.ShieldMask;
			DrawMask(shield, effect.ImpactAnchor, colors.Glow * (alpha * 0.20f), 0f, new Vector2(scale * 1.16f));
			DrawMask(shield, effect.ImpactAnchor, colors.Primary * (alpha * 0.34f), 0f, new Vector2(scale));
			DrawMask(shield, effect.ImpactAnchor, colors.Highlight * (alpha * 0.22f), 0f, new Vector2(scale * 0.82f));
		}

		private void DrawShieldShatter(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * (1f - recovery);
			var fragment = _resources.ShieldFragmentMask;
			for (int i = 0; i < 11; i++)
			{
				float angle = i * MathHelper.TwoPi / 11f + 0.2f;
				float distance = MathHelper.Lerp(24f, 250f + (i % 3) * 32f, VisualEffectDisplayMath.EaseOutCubic(recovery));
				DrawMask(fragment, effect.ImpactAnchor + Axis(angle) * distance, colors.Primary * alpha, angle + recovery * 5f, new Vector2(0.8f + (i % 3) * 0.2f));
			}
		}

		private void DrawSoulSiphon(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float p = VisualEffectDisplayMath.Progress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * (1f - recovery);
			Vector2 previous = effect.ImpactAnchor;
			for (int i = 1; i <= 18; i++)
			{
				float t = i / 18f;
				var point = Vector2.Lerp(effect.ImpactAnchor, effect.SourceAnchor, t);
				point.Y += MathF.Sin(t * MathHelper.TwoPi * 2f + p * 8f) * 42f * MathF.Sin(t * MathHelper.Pi);
				DrawGlowLine(previous, point, colors.Primary, MathHelper.Lerp(18f, 5f, t), alpha * 0.62f);
				previous = point;
			}
			DrawSoftCircle(Vector2.Lerp(effect.ImpactAnchor, effect.SourceAnchor, MathHelper.Clamp(p, 0f, 1f)), 52f, colors.Highlight, alpha, 0f, 0.72f);
		}

		private void DrawResourceMotes(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float p = VisualEffectDisplayMath.Progress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * (1f - recovery);
			for (int i = 0; i < 10; i++)
			{
				float angle = i * MathHelper.TwoPi / 10f + p * 3f;
				float radius = MathHelper.Lerp(180f, 48f, VisualEffectDisplayMath.EaseOutCubic(p));
				var pos = effect.ImpactAnchor + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius * 0.55f - p * 65f);
				DrawSoftCircle(pos, 24f + (i % 3) * 7f, i % 2 == 0 ? colors.Highlight : colors.Primary, alpha, 0f, 0.7f);
			}
		}

		private void DrawSealStamp(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float approach = VisualEffectDisplayMath.ApproachProgress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * (1f - recovery);
			float scale = MathHelper.Lerp(2.2f, 1f, VisualEffectDisplayMath.EaseOutBack(approach));
			var ring = _resources.SealRingMask;
			DrawMask(ring, effect.ImpactAnchor, colors.Primary * alpha, 0f, new Vector2(scale));
			for (int i = 0; i < 4; i++)
			{
				float angle = i * MathHelper.PiOver2 + MathHelper.PiOver4;
				DrawLine(effect.ImpactAnchor + Axis(angle) * 38f * scale, effect.ImpactAnchor + Axis(angle) * 88f * scale, colors.Highlight * alpha, 10f);
			}
		}

		private void DrawFrostBind(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float p = VisualEffectDisplayMath.Progress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * (1f - recovery);
			for (int i = -3; i <= 3; i++)
			{
				float x = i * 42f;
				float rise = MathHelper.Lerp(180f, 310f, VisualEffectDisplayMath.EaseOutCubic(p));
				DrawGlowLine(effect.ImpactAnchor + new Vector2(x, 150f), effect.ImpactAnchor + new Vector2(x * 0.55f, 150f - rise), colors.Primary, 9f, alpha);
			}
		}

		private void DrawBrittleFracture(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float p = VisualEffectDisplayMath.Progress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * (1f - recovery);
			for (int i = 0; i < 8; i++)
			{
				float angle = i * MathHelper.TwoPi / 8f + (i % 2) * 0.18f;
				var mid = effect.ImpactAnchor + Axis(angle) * 70f * p;
				var end = mid + Axis(angle + (i % 2 == 0 ? 0.22f : -0.2f)) * 90f * p;
				DrawGlowLine(effect.ImpactAnchor, mid, colors.Primary, 7f, alpha);
				DrawGlowLine(mid, end, colors.Highlight, 4f, alpha);
			}
		}

		private void DrawColorDrain(ActiveVisualEffect effect)
		{
			var colors = Colors(effect);
			float p = VisualEffectDisplayMath.Progress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float alpha = PrimitiveAlpha * effect.Recipe.Intensity * (1f - recovery);
			for (int i = 0; i < 4; i++)
			{
				float scale = MathHelper.Lerp(2.6f + i * 0.34f, 0.45f + i * 0.12f, VisualEffectDisplayMath.EaseOutCubic(p));
				var ring = _resources.ColorDrainRingMask;
				DrawMask(ring, effect.ImpactAnchor, (i % 2 == 0 ? colors.Primary : colors.Shadow) * (alpha * (1f - i * 0.14f)), 0f, new Vector2(scale));
			}
			DrawSoftCircle(effect.ImpactAnchor, 310f, colors.Shadow, alpha * p * 0.28f, 0f, 0.82f);
		}

		private void DrawTravelingSlash(Vector2 center, float angleDegrees, float startTravel, float endTravel, float progress, float alpha)
		{
			if (alpha <= 0f) return;
			float rotation = MathHelper.ToRadians(angleDegrees);
			float travel = MathHelper.Lerp(startTravel, endTravel, VisualEffectDisplayMath.EaseOutCubic(progress));
			DrawGlowSlash(center + Axis(rotation) * travel, angleDegrees, 260f, 18f * SlashThicknessScale, alpha, VisualEffectPaletteResolver.Resolve(VisualEffectPalette.Physical));
		}

		private void DrawGlowSlash(Vector2 center, float angleDegrees, float length, float thickness, float alpha, VisualEffectColors colors)
		{
			float rotation = MathHelper.ToRadians(angleDegrees);
			var half = Axis(rotation) * (length * 0.5f);
			DrawGlowLine(center - half, center + half, colors.Glow, thickness * 1.9f, alpha * GlowAlpha);
			DrawGlowLine(center - half * 0.86f, center + half * 0.86f, colors.Highlight, thickness, alpha);
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

		private void DrawToothRow(Vector2 center, bool top, float alpha, VisualEffectColors colors)
		{
			var tooth = top ? _resources.ToothTopMask : _resources.ToothBottomMask;
			for (int i = 0; i < 8; i++)
			{
				float x = (i - 3.5f) * 31f;
				float y = MathF.Abs(i - 3.5f) * 4f * (top ? -1f : 1f);
				float toothScale = 0.78f + (i % 3) * 0.13f;
				float rotation = MathHelper.ToRadians((i - 3.5f) * (top ? -2.6f : 2.6f));
				DrawMask(tooth, center + new Vector2(x, y), colors.Glow * (alpha * 0.26f), rotation, new Vector2(toothScale * 1.26f));
				DrawMask(tooth, center + new Vector2(x, y), colors.Highlight * alpha, rotation, new Vector2(toothScale));
			}
		}

		private void DrawHammer(Vector2 pivot, float rotation, float scale, float alpha)
		{
			var handleCenter = pivot + Rotate(new Vector2(130f, 0f) * scale, rotation);
			var handleSize = new Vector2(280f, 24f) * scale;
			DrawGlowRect(handleCenter, handleSize + new Vector2(6f * scale), Cream, alpha * 0.16f, rotation);
			DrawRotatedRect(handleCenter, handleSize, RockDark * alpha, rotation);
			var handleHighlight = handleCenter + Rotate(new Vector2(0f, -5f) * scale, rotation);
			DrawRotatedRect(handleHighlight, new Vector2(266f, 3f) * scale, Cream * (alpha * 0.28f), rotation);

			var gripCenter = pivot + Rotate(new Vector2(42f, 0f) * scale, rotation);
			DrawRotatedRect(gripCenter, new Vector2(96f, 34f) * scale, RockDark * alpha, rotation);
			for (int i = -2; i <= 2; i++)
			{
				var wrapCenter = gripCenter + Rotate(new Vector2(i * 18f, 0f) * scale, rotation);
				DrawRotatedRect(wrapCenter, new Vector2(5f, 36f) * scale, Gold * (alpha * 0.55f), rotation);
			}

			var pommelCenter = pivot + Rotate(new Vector2(-18f, 0f) * scale, rotation);
			DrawGlowRect(pommelCenter, new Vector2(34f, 44f) * scale, Gold, alpha * 0.12f, rotation);
			DrawRotatedRect(pommelCenter, new Vector2(30f, 40f) * scale, RockDark * alpha, rotation);

			var headCenter = pivot + Rotate(new Vector2(270f, 0f) * scale, rotation);
			var headMask = _resources.HammerHeadMask;
			DrawMask(headMask, headCenter, Cream * (alpha * 0.18f), rotation, new Vector2(scale * 1.08f));
			DrawMask(headMask, headCenter, RockDark * alpha, rotation, new Vector2(scale));
			DrawMask(headMask, headCenter, Steel * (alpha * 0.70f), rotation, new Vector2(scale * 0.84f));

			DrawRotatedRect(headCenter, new Vector2(48f, 62f) * scale, Gold * (alpha * 0.72f), rotation);
			DrawRotatedRect(headCenter, new Vector2(34f, 48f) * scale, RockDark * (alpha * 0.88f), rotation);
			var upperFaceCenter = headCenter + Rotate(new Vector2(0f, -82f) * scale, rotation);
			var lowerFaceCenter = headCenter + Rotate(new Vector2(0f, 82f) * scale, rotation);
			DrawRotatedRect(upperFaceCenter, new Vector2(68f, 10f) * scale, Cream * (alpha * 0.72f), rotation);
			DrawRotatedRect(lowerFaceCenter, new Vector2(68f, 10f) * scale, Cream * (alpha * 0.72f), rotation);
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
			var texture = _resources.GetRadialMask(innerStop, outerStop);
			float scale = Math.Max(1f, diameter) / texture.Width;
			DrawMask(texture, center, color * alpha, 0f, new Vector2(scale));
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

		private static VisualEffectColors Colors(ActiveVisualEffect effect)
		{
			return VisualEffectPaletteResolver.Resolve(effect?.Recipe?.Palette ?? VisualEffectPalette.Physical);
		}
	}
}
