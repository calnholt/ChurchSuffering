using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.VisualEffects;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
	[DebugTab("Passive Application Animation")]
	public sealed class PassiveApplicationAnimationDisplaySystem : Core.System
	{
		private sealed class AnimationInstance
		{
			public Entity Target;
			public AppliedPassiveType Type;
			public PassiveApplicationRecipe Recipe;
			public float CreatedSeconds;
			public float StartSeconds;
			public float Strength = 1f;
			public int CombinedDelta;
			public bool AudioPublished;
		}

		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly Texture2D _pixel;
		private readonly List<AnimationInstance> _animations = new();
		private readonly Dictionary<int, float> _nextStartByTarget = new();
		private readonly Dictionary<int, float> _nextAudioByTarget = new();
		private float _clockSeconds;

		[DebugEditable(DisplayName = "Duration Scale", Step = 0.05f, Min = 0.25f, Max = 3f)]
		public float DurationScale { get; set; } = 1f;

		[DebugEditable(DisplayName = "Seal Scale", Step = 0.05f, Min = 0.25f, Max = 3f)]
		public float SealScale { get; set; } = 1f;

		[DebugEditable(DisplayName = "Alpha", Step = 0.05f, Min = 0f, Max = 2f)]
		public float SealAlpha { get; set; } = 0.92f;

		[DebugEditable(DisplayName = "HP Bar Edge Gap", Step = 1f, Min = 0f, Max = 400f)]
		public float OutwardOffset { get; set; } = 36f;

		[DebugEditable(DisplayName = "Vertical Offset", Step = 1f, Min = -300f, Max = 300f)]
		public float VerticalOffset { get; set; } = 8f;

		[DebugEditable(DisplayName = "Fan Spacing", Step = 1f, Min = 0f, Max = 180f)]
		public float FanSpacing { get; set; } = 78f;

		[DebugEditable(DisplayName = "Status Stagger (s)", Step = 0.01f, Min = 0f, Max = 1f)]
		public float StatusStaggerSeconds { get; set; } = 0.10f;

		[DebugEditable(DisplayName = "Coalesce Window (s)", Step = 0.01f, Min = 0f, Max = 1f)]
		public float CoalesceWindowSeconds { get; set; } = 0.12f;

		[DebugEditable(DisplayName = "Audio Cooldown (s)", Step = 0.01f, Min = 0f, Max = 2f)]
		public float AudioCooldownSeconds { get; set; } = 0.25f;

		[DebugEditable(DisplayName = "Audio Volume", Step = 0.01f, Min = 0f, Max = 1f)]
		public float AudioVolume { get; set; } = 0.18f;

		[DebugEditable(DisplayName = "Max Per Target", Step = 1f, Min = 1f, Max = 16f)]
		public int MaxPerTarget { get; set; } = 6;

		[DebugEditable(DisplayName = "Max Global", Step = 1f, Min = 1f, Max = 32f)]
		public int MaxGlobal { get; set; } = 12;

		public PassiveApplicationAnimationDisplaySystem(
			EntityManager entityManager,
			GraphicsDevice graphicsDevice,
			SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			if (graphicsDevice != null)
			{
				_pixel = new Texture2D(graphicsDevice, 1, 1);
				_pixel.SetData(new[] { Color.White });
			}
			EventManager.Subscribe<ApplyPassiveEvent>(OnApplyPassive);
			EventManager.Subscribe<DeleteCachesEvent>(_ => Clear());
			EventManager.Subscribe<LoadSceneEvent>(_ => Clear());
		}

		protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();
		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			if (!IsActive) return;
			float dt = Math.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
			_clockSeconds += dt;

			foreach (var animation in _animations)
			{
				if (animation.AudioPublished || animation.StartSeconds > _clockSeconds) continue;
				animation.AudioPublished = true;
				int targetId = animation.Target?.Id ?? -1;
				float nextAudio = _nextAudioByTarget.TryGetValue(targetId, out float value) ? value : 0f;
				if (_clockSeconds < nextAudio || AudioVolume <= 0f) continue;
				EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.ApplyCard, Volume = AudioVolume });
				_nextAudioByTarget[targetId] = _clockSeconds + Math.Max(0f, AudioCooldownSeconds);
			}

			_animations.RemoveAll(animation =>
				_clockSeconds >= animation.StartSeconds + AnimationDuration(animation));
			PruneTargetState();
		}

		private void OnApplyPassive(ApplyPassiveEvent evt)
		{
			if (evt?.Target == null || evt.Delta <= 0) return;

			var coalesced = _animations
				.LastOrDefault(animation =>
					animation.Target?.Id == evt.Target.Id
					&& animation.Type == evt.Type
					&& _clockSeconds - animation.CreatedSeconds <= Math.Max(0f, CoalesceWindowSeconds));
			if (coalesced != null)
			{
				coalesced.CombinedDelta += evt.Delta;
				coalesced.Strength = Math.Min(1.35f, coalesced.Strength + 0.10f);
				if (coalesced.StartSeconds <= _clockSeconds)
					coalesced.StartSeconds = _clockSeconds;
				return;
			}

			ReserveCapacity(evt.Target.Id);
			float nextStart = _nextStartByTarget.TryGetValue(evt.Target.Id, out float scheduled)
				? Math.Max(_clockSeconds, scheduled)
				: _clockSeconds;
			_animations.Add(new AnimationInstance
			{
				Target = evt.Target,
				Type = evt.Type,
				Recipe = PassiveApplicationRecipeCatalog.Get(evt.Type),
				CreatedSeconds = _clockSeconds,
				StartSeconds = nextStart,
				CombinedDelta = evt.Delta,
			});
			_nextStartByTarget[evt.Target.Id] = nextStart + Math.Max(0f, StatusStaggerSeconds);
		}

		private void ReserveCapacity(int targetId)
		{
			while (_animations.Count(animation => animation.Target?.Id == targetId) >= Math.Max(1, MaxPerTarget))
				RemoveOldest(animation => animation.Target?.Id == targetId);
			while (_animations.Count >= Math.Max(1, MaxGlobal))
				RemoveOldest(_ => true);
		}

		private void RemoveOldest(Func<AnimationInstance, bool> predicate)
		{
			var oldest = _animations.Where(predicate).OrderBy(animation => animation.CreatedSeconds).FirstOrDefault();
			if (oldest != null) _animations.Remove(oldest);
		}

		private void PruneTargetState()
		{
			foreach (int targetId in _nextStartByTarget.Keys.Where(id => _animations.All(a => a.Target?.Id != id)).ToList())
				_nextStartByTarget.Remove(targetId);
			foreach (int targetId in _nextAudioByTarget.Keys.Where(id => _clockSeconds >= _nextAudioByTarget[id] && _animations.All(a => a.Target?.Id != id)).ToList())
				_nextAudioByTarget.Remove(targetId);
		}

		private void Clear()
		{
			_animations.Clear();
			_nextStartByTarget.Clear();
			_nextAudioByTarget.Clear();
			_clockSeconds = 0f;
		}

		public void Draw()
		{
			if (_spriteBatch == null || _graphicsDevice == null || _pixel == null) return;
			var started = _animations
				.Where(animation => animation.StartSeconds <= _clockSeconds)
				.GroupBy(animation => animation.Target?.Id ?? -1);
			foreach (var group in started)
			{
				var targetAnimations = group.OrderBy(animation => animation.StartSeconds).ThenBy(animation => animation.Type).ToList();
				for (int i = 0; i < targetAnimations.Count; i++)
				{
					var animation = targetAnimations[i];
					float fanOffset = (i - (targetAnimations.Count - 1) * 0.5f) * FanSpacing;
					DrawAnimation(animation, ResolveAnchor(animation.Target) + new Vector2(0f, fanOffset));
				}
			}
		}

		private Vector2 ResolveAnchor(Entity target)
		{
			var hpBarAnchor = target?.GetComponent<HPBarAnchor>();
			if (hpBarAnchor != null && hpBarAnchor.Rect.Width > 0 && hpBarAnchor.Rect.Height > 0)
			{
				float sealHalfExtent = 58f * Math.Max(.25f, SealScale);
				float x = target.HasComponent<Player>()
					? hpBarAnchor.Rect.Left - Math.Max(0f, OutwardOffset) - sealHalfExtent
					: hpBarAnchor.Rect.Right + Math.Max(0f, OutwardOffset) + sealHalfExtent;
				return new Vector2(x, hpBarAnchor.Rect.Center.Y + VerticalOffset);
			}

			var portrait = target?.GetComponent<PortraitInfo>();
			var transform = target?.GetComponent<Transform>();
			Vector2 center = portrait?.LastDrawCenter != null && portrait.LastDrawCenter != Vector2.Zero
				? portrait.LastDrawCenter
				: transform?.Position ?? Vector2.Zero;
			float baseScale = portrait?.BaseScale > 0f ? portrait.BaseScale : 1f;
			float halfWidth = portrait?.TextureWidth > 0 ? portrait.TextureWidth * baseScale * 0.5f : 110f;
			float halfHeight = portrait?.TextureHeight > 0 ? portrait.TextureHeight * baseScale * 0.5f : 140f;
			float outward = center.X < Game1.VirtualWidth * 0.5f ? -1f : 1f;
			return center + new Vector2(outward * (halfWidth * 0.72f + OutwardOffset), halfHeight * 0.08f + VerticalOffset);
		}

		private void DrawAnimation(AnimationInstance animation, Vector2 center)
		{
			float duration = AnimationDuration(animation);
			float progress = MathHelper.Clamp((_clockSeconds - animation.StartSeconds) / Math.Max(.001f, duration), 0f, 1f);
			float enter = MathHelper.Clamp(progress / .36f, 0f, 1f);
			float exit = MathHelper.Clamp((progress - .62f) / .38f, 0f, 1f);
			float alpha = SealAlpha * VisualEffectDisplayMath.EaseOutCubic(enter) * (1f - exit * exit);
			if (alpha <= 0f) return;

			float eased = VisualEffectDisplayMath.EaseOutCubic(enter);
			float scale = MotionScale(animation.Recipe.Motion, eased, progress) * SealScale * animation.Strength;
			float rotation = MathHelper.ToRadians(animation.Recipe.RotationDegrees + MotionRotation(animation.Recipe.Motion, progress));
			var colors = VisualEffectPaletteResolver.Resolve(animation.Recipe.Palette);
			DrawSoftGlow(center, 112f * scale, colors.Glow, alpha * .16f);
			DrawMotif(animation.Recipe, center, scale, rotation, progress, alpha, colors);
		}

		private static float MotionScale(PassiveApplicationMotion motion, float enter, float progress)
		{
			return motion switch
			{
				PassiveApplicationMotion.Constrict => MathHelper.Lerp(1.55f, 1f, enter),
				PassiveApplicationMotion.Stamp => MathHelper.Lerp(1.8f, 1f, VisualEffectDisplayMath.EaseOutBack(enter)),
				PassiveApplicationMotion.Pulse => 1f + MathF.Sin(progress * MathHelper.TwoPi * 2f) * .10f * (1f - progress),
				_ => MathHelper.Lerp(.45f, 1f, enter),
			};
		}

		private static float MotionRotation(PassiveApplicationMotion motion, float progress)
		{
			return motion == PassiveApplicationMotion.Orbit ? progress * 150f
				: motion == PassiveApplicationMotion.Assemble ? (1f - progress) * -35f
				: 0f;
		}

		private void DrawMotif(
			PassiveApplicationRecipe recipe,
			Vector2 center,
			float scale,
			float rotation,
			float progress,
			float alpha,
			VisualEffectColors colors)
		{
			switch (recipe.Motif)
			{
				case PassiveApplicationMotif.Ring:
				case PassiveApplicationMotif.Ward:
				case PassiveApplicationMotif.Iris:
				case PassiveApplicationMotif.HeatRing:
				case PassiveApplicationMotif.BrokenHalo:
				case PassiveApplicationMotif.Stamp:
					DrawRingFamily(recipe, center, scale, rotation, progress, alpha, colors);
					break;
				case PassiveApplicationMotif.FlameCrown:
				case PassiveApplicationMotif.Sparks:
				case PassiveApplicationMotif.Threads:
				case PassiveApplicationMotif.Thorns:
				case PassiveApplicationMotif.JaggedAura:
				case PassiveApplicationMotif.IceNeedles:
				case PassiveApplicationMotif.FrostSeal:
				case PassiveApplicationMotif.ElectricChain:
					DrawRadialFamily(recipe, center, scale, rotation, progress, alpha, colors);
					break;
				case PassiveApplicationMotif.Bubbles:
				case PassiveApplicationMotif.Motes:
				case PassiveApplicationMotif.Nodes:
				case PassiveApplicationMotif.Wisps:
				case PassiveApplicationMotif.CoinOrbit:
					DrawOrbitFamily(recipe, center, scale, rotation, progress, alpha, colors);
					break;
				case PassiveApplicationMotif.Chevrons:
				case PassiveApplicationMotif.TrailingArcs:
				case PassiveApplicationMotif.SplitMark:
				case PassiveApplicationMotif.PressureBars:
				case PassiveApplicationMotif.Scar:
				case PassiveApplicationMotif.Stream:
				case PassiveApplicationMotif.Ribbons:
				case PassiveApplicationMotif.MuteBar:
				case PassiveApplicationMotif.BladeGlint:
				case PassiveApplicationMotif.Pillars:
					DrawLinearFamily(recipe, center, scale, rotation, progress, alpha, colors);
					break;
				default:
					DrawStructuredFamily(recipe, center, scale, rotation, progress, alpha, colors);
					break;
			}
		}

		private void DrawRingFamily(PassiveApplicationRecipe recipe, Vector2 center, float scale, float rotation, float progress, float alpha, VisualEffectColors colors)
		{
			float aspect = Math.Max(.35f, recipe.Aspect);
			var ring = PrimitiveTextureFactory.GetAntialiasedRingMask(_graphicsDevice, 112, 112, recipe.Motif == PassiveApplicationMotif.Iris ? 8f : 5f);
			DrawMask(ring, center, colors.Primary * alpha, rotation, new Vector2(scale * aspect, scale));
			if (recipe.Motif is PassiveApplicationMotif.Ward or PassiveApplicationMotif.Stamp)
			{
				DrawMask(ring, center, colors.Highlight * (alpha * .62f), -rotation * .65f, new Vector2(scale * .68f));
				DrawRadialLines(center, 6, 25f * scale, 47f * scale, -rotation, colors.Highlight * (alpha * .72f), 3f * scale);
			}
			else if (recipe.Motif == PassiveApplicationMotif.HeatRing)
			{
				DrawMask(ring, center, colors.Glow * (alpha * .54f), rotation, new Vector2(scale * (.72f + .08f * MathF.Sin(progress * 18f))));
			}
			else if (recipe.Motif == PassiveApplicationMotif.BrokenHalo)
			{
				DrawLine(center + Rotate(new Vector2(-45f, -8f) * scale, rotation), center + Rotate(new Vector2(-8f, 6f) * scale, rotation), colors.Shadow * alpha, 7f * scale);
				DrawLine(center + Rotate(new Vector2(10f, -5f) * scale, rotation), center + Rotate(new Vector2(46f, 10f) * scale, rotation), colors.Highlight * alpha, 7f * scale);
			}
		}

		private void DrawRadialFamily(PassiveApplicationRecipe recipe, Vector2 center, float scale, float rotation, float progress, float alpha, VisualEffectColors colors)
		{
			int count = Math.Max(1, recipe.ElementCount);
			float inner = recipe.Motif == PassiveApplicationMotif.Threads ? 4f : 28f;
			float outer = recipe.Motif is PassiveApplicationMotif.FlameCrown or PassiveApplicationMotif.JaggedAura ? 62f : 54f;
			float thickness = recipe.Motif == PassiveApplicationMotif.Threads ? 2f : 5f;
			for (int i = 0; i < count; i++)
			{
				float angle = rotation + MathHelper.TwoPi * i / count;
				float wave = (i % 2 == 0 ? 1f : .78f) * (1f + .05f * MathF.Sin(progress * 16f + i));
				var start = center + Axis(angle) * inner * scale;
				var end = center + Axis(angle) * outer * wave * scale;
				DrawLine(start, end, (i % 2 == 0 ? colors.Primary : colors.Highlight) * alpha, thickness * scale);
				if (recipe.Motif is PassiveApplicationMotif.Sparks or PassiveApplicationMotif.ElectricChain)
				{
					var elbow = Vector2.Lerp(start, end, .55f) + Perp(angle) * (i % 2 == 0 ? 8f : -8f) * scale;
					DrawLine(start, elbow, colors.Glow * alpha, 3f * scale);
					DrawLine(elbow, end, colors.Highlight * alpha, 3f * scale);
				}
			}
			if (recipe.Motif == PassiveApplicationMotif.FrostSeal)
				DrawRing(center, 38f * scale, colors.Highlight * (alpha * .7f), 3f * scale);
		}

		private void DrawOrbitFamily(PassiveApplicationRecipe recipe, Vector2 center, float scale, float rotation, float progress, float alpha, VisualEffectColors colors)
		{
			int count = Math.Max(1, recipe.ElementCount);
			for (int i = 0; i < count; i++)
			{
				float angle = rotation + MathHelper.TwoPi * i / count;
				float radius = (38f + (i % 2) * 13f) * scale;
				var pos = center + new Vector2(MathF.Cos(angle) * radius * recipe.Aspect, MathF.Sin(angle) * radius);
				float size = recipe.Motif == PassiveApplicationMotif.CoinOrbit ? 13f : recipe.Motif == PassiveApplicationMotif.Wisps ? 9f : 11f;
				DrawDot(pos, size * scale, (i % 2 == 0 ? colors.Primary : colors.Highlight) * alpha);
				if (recipe.Motif == PassiveApplicationMotif.Wisps)
					DrawLine(pos, pos + Rotate(new Vector2(-22f, 8f) * scale, angle), colors.Smoke * (alpha * .7f), 4f * scale);
			}
			if (recipe.Motif == PassiveApplicationMotif.Nodes)
				DrawRing(center, 43f * scale, colors.Glow * (alpha * .55f), 2f * scale);
		}

		private void DrawLinearFamily(PassiveApplicationRecipe recipe, Vector2 center, float scale, float rotation, float progress, float alpha, VisualEffectColors colors)
		{
			int count = Math.Max(1, recipe.ElementCount);
			if (recipe.Motif == PassiveApplicationMotif.Chevrons)
			{
				for (int i = 0; i < count; i++)
				{
					float y = (i - (count - 1) * .5f) * 17f;
					var tip = center + Rotate(new Vector2(22f, y) * scale, rotation);
					DrawLine(center + Rotate(new Vector2(-22f, y - 12f) * scale, rotation), tip, colors.Primary * alpha, 5f * scale);
					DrawLine(center + Rotate(new Vector2(-22f, y + 12f) * scale, rotation), tip, colors.Highlight * alpha, 5f * scale);
				}
				return;
			}

			if (recipe.Motif is PassiveApplicationMotif.SplitMark or PassiveApplicationMotif.Scar or PassiveApplicationMotif.BladeGlint)
			{
				DrawLine(center + Rotate(new Vector2(-52f, 0f) * scale, rotation), center + Rotate(new Vector2(52f, 0f) * scale, rotation), colors.Primary * alpha, (recipe.Motif == PassiveApplicationMotif.Scar ? 8f : 5f) * scale);
				if (recipe.Motif == PassiveApplicationMotif.SplitMark)
					DrawLine(center + Rotate(new Vector2(-32f, -15f) * scale, -rotation), center + Rotate(new Vector2(34f, 18f) * scale, -rotation), colors.Highlight * alpha, 4f * scale);
				else if (recipe.Motif == PassiveApplicationMotif.BladeGlint)
					DrawRadialLines(center + Axis(rotation) * MathHelper.Lerp(-42f, 42f, progress) * scale, 4, 2f, 18f * scale, rotation, colors.Highlight * alpha, 3f * scale);
				return;
			}

			if (recipe.Motif == PassiveApplicationMotif.MuteBar)
			{
				DrawLine(center + new Vector2(-48f, 0f) * scale, center + new Vector2(48f, 0f) * scale, colors.Primary * alpha, 9f * scale);
				DrawLine(center + new Vector2(-58f, -30f) * scale, center + new Vector2(-58f, 30f) * scale, colors.Highlight * alpha, 5f * scale);
				DrawLine(center + new Vector2(58f, -30f) * scale, center + new Vector2(58f, 30f) * scale, colors.Highlight * alpha, 5f * scale);
				return;
			}

			for (int i = 0; i < count; i++)
			{
				float offset = (i - (count - 1) * .5f) * 18f;
				var a = center + Rotate(new Vector2(-48f * recipe.Aspect, offset) * scale, rotation);
				var b = center + Rotate(new Vector2(48f * recipe.Aspect, offset + (i % 2 == 0 ? -6f : 6f)) * scale, rotation);
				DrawLine(a, b, (i % 2 == 0 ? colors.Primary : colors.Highlight) * alpha, (recipe.Motif == PassiveApplicationMotif.PressureBars ? 8f : 4f) * scale);
			}
		}

		private void DrawStructuredFamily(PassiveApplicationRecipe recipe, Vector2 center, float scale, float rotation, float progress, float alpha, VisualEffectColors colors)
		{
			switch (recipe.Motif)
			{
				case PassiveApplicationMotif.Droplets:
				case PassiveApplicationMotif.BloodDrops:
					for (int i = 0; i < recipe.ElementCount; i++)
					{
						float x = (i - (recipe.ElementCount - 1) * .5f) * 18f * scale;
						float y = ((progress * 90f + i * 17f) % 82f - 41f) * scale;
						DrawDot(center + new Vector2(x, y), (recipe.Motif == PassiveApplicationMotif.BloodDrops ? 12f : 9f) * scale, colors.Primary * alpha);
						DrawLine(center + new Vector2(x, y - 13f * scale), center + new Vector2(x, y), colors.Highlight * alpha, 3f * scale);
					}
					break;
				case PassiveApplicationMotif.Plates:
				case PassiveApplicationMotif.Braces:
					for (int i = 0; i < 4; i++)
					{
						float angle = rotation + MathHelper.PiOver2 * i;
						var pos = center + Axis(angle) * 46f * scale;
						DrawRect(pos, new Vector2(38f, recipe.Motif == PassiveApplicationMotif.Plates ? 18f : 8f) * scale, colors.Primary * alpha, angle + MathHelper.PiOver2);
					}
					break;
				case PassiveApplicationMotif.Crescents:
					for (int i = 0; i < 3; i++)
						DrawArc(center, (34f + i * 11f) * scale, rotation + i * .45f, 2.8f, colors.Primary * (alpha * (1f - i * .18f)), 4f * scale);
					break;
				case PassiveApplicationMotif.Eye:
					DrawMask(PrimitiveTextureFactory.GetAntialiasedRingMask(_graphicsDevice, 124, 62, 5f), center, colors.Primary * alpha, rotation, new Vector2(scale * recipe.Aspect, scale));
					DrawDot(center, 20f * scale, colors.Highlight * alpha);
					DrawDot(center, 8f * scale, colors.Shadow * alpha);
					break;
				case PassiveApplicationMotif.Lattice:
					DrawRadialLines(center, 6, 0f, 55f * scale, rotation, colors.Primary * alpha, 4f * scale);
					DrawRing(center, 34f * scale, colors.Highlight * alpha, 3f * scale);
					break;
				case PassiveApplicationMotif.ChainLinks:
					var link = PrimitiveTextureFactory.GetAntialiasedRingMask(_graphicsDevice, 74, 46, 7f);
					DrawMask(link, center + Rotate(new Vector2(-24f, 0f) * scale, rotation), colors.Primary * alpha, rotation, new Vector2(scale));
					DrawMask(link, center + Rotate(new Vector2(24f, 0f) * scale, rotation), colors.Highlight * alpha, rotation + MathHelper.PiOver2, new Vector2(scale));
					break;
				case PassiveApplicationMotif.HeartDiamond:
					DrawDiamond(center, 55f * scale, colors.Primary * alpha, rotation, 5f * scale);
					DrawDiamond(center, 31f * scale, colors.Highlight * alpha, -rotation, 3f * scale);
					break;
				case PassiveApplicationMotif.Clock:
					DrawRing(center, 49f * scale, colors.Primary * alpha, 4f * scale);
					DrawRadialLines(center, recipe.ElementCount, 35f * scale, 48f * scale, rotation, colors.Highlight * alpha, 3f * scale);
					DrawLine(center, center + Axis(rotation + progress * MathHelper.TwoPi) * 35f * scale, colors.Highlight * alpha, 4f * scale);
					break;
				default:
					DrawRing(center, 48f * scale, colors.Primary * alpha, 4f * scale);
					break;
			}
		}

		private float AnimationDuration(AnimationInstance animation) =>
			Math.Max(.05f, animation.Recipe.DurationSeconds * Math.Max(.05f, DurationScale));

		private void DrawSoftGlow(Vector2 center, float diameter, Color color, float alpha)
		{
			var texture = PrimitiveTextureFactory.GetSoftRadialCircle(_graphicsDevice, Math.Max(1, (int)MathF.Round(diameter)), 0f, .9f);
			DrawMask(texture, center, color * alpha, 0f, Vector2.One);
		}

		private void DrawRing(Vector2 center, float radius, Color color, float thickness)
		{
			int diameter = Math.Max(2, (int)MathF.Round(radius * 2f));
			DrawMask(PrimitiveTextureFactory.GetAntialiasedRingMask(_graphicsDevice, diameter, diameter, thickness), center, color, 0f, Vector2.One);
		}

		private void DrawRadialLines(Vector2 center, int count, float inner, float outer, float rotation, Color color, float thickness)
		{
			for (int i = 0; i < Math.Max(1, count); i++)
			{
				float angle = rotation + MathHelper.TwoPi * i / Math.Max(1, count);
				DrawLine(center + Axis(angle) * inner, center + Axis(angle) * outer, color, thickness);
			}
		}

		private void DrawArc(Vector2 center, float radius, float start, float sweep, Color color, float thickness)
		{
			const int segments = 14;
			for (int i = 0; i < segments; i++)
			{
				float a = start + sweep * i / segments;
				float b = start + sweep * (i + 1) / segments;
				DrawLine(center + Axis(a) * radius, center + Axis(b) * radius, color, thickness);
			}
		}

		private void DrawDiamond(Vector2 center, float diameter, Color color, float rotation, float thickness)
		{
			var points = new[]
			{
				center + Rotate(new Vector2(0f, -diameter * .5f), rotation),
				center + Rotate(new Vector2(diameter * .5f, 0f), rotation),
				center + Rotate(new Vector2(0f, diameter * .5f), rotation),
				center + Rotate(new Vector2(-diameter * .5f, 0f), rotation),
			};
			for (int i = 0; i < points.Length; i++) DrawLine(points[i], points[(i + 1) % points.Length], color, thickness);
		}

		private void DrawDot(Vector2 center, float diameter, Color color)
		{
			var dot = PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, Math.Max(1, (int)MathF.Round(diameter * .5f)));
			DrawMask(dot, center, color, 0f, Vector2.One);
		}

		private void DrawMask(Texture2D texture, Vector2 center, Color color, float rotation, Vector2 scale)
		{
			_spriteBatch.Draw(texture, center, null, color, rotation, new Vector2(texture.Width * .5f, texture.Height * .5f), scale, SpriteEffects.None, 0f);
		}

		private void DrawRect(Vector2 center, Vector2 size, Color color, float rotation)
		{
			_spriteBatch.Draw(_pixel, center, null, color, rotation, new Vector2(.5f), new Vector2(Math.Max(1f, size.X), Math.Max(1f, size.Y)), SpriteEffects.None, 0f);
		}

		private void DrawLine(Vector2 start, Vector2 end, Color color, float thickness)
		{
			var delta = end - start;
			float length = delta.Length();
			if (length <= .001f) return;
			_spriteBatch.Draw(_pixel, start, null, color, MathF.Atan2(delta.Y, delta.X), new Vector2(0f, .5f), new Vector2(length, Math.Max(1f, thickness)), SpriteEffects.None, 0f);
		}

		private static Vector2 Axis(float rotation) => new(MathF.Cos(rotation), MathF.Sin(rotation));
		private static Vector2 Perp(float rotation) => new(-MathF.Sin(rotation), MathF.Cos(rotation));
		private static Vector2 Rotate(Vector2 value, float rotation) => new(
			value.X * MathF.Cos(rotation) - value.Y * MathF.Sin(rotation),
			value.X * MathF.Sin(rotation) + value.Y * MathF.Cos(rotation));

		internal int AnimationCount => _animations.Count;

		internal bool TryGetAnimation(Entity target, AppliedPassiveType type, out float startSeconds, out float strength, out int combinedDelta)
		{
			var animation = _animations.LastOrDefault(item => item.Target?.Id == target?.Id && item.Type == type);
			startSeconds = animation?.StartSeconds ?? 0f;
			strength = animation?.Strength ?? 0f;
			combinedDelta = animation?.CombinedDelta ?? 0;
			return animation != null;
		}
	}
}
