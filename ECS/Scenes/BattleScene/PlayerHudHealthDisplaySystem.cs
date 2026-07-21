using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
	internal readonly record struct PlayerHudHealthRenderState(
		int Current,
		int Max,
		float TargetPercent,
		float DisplayedPercent,
		int IncomingDamage,
		bool IsLowHealth)
	{
		public string FractionText => $"{Current}/{Max}";
	}

	internal static class PlayerHudHealthRendering
	{
		public static PlayerHudHealthRenderState BuildRenderState(
			int current,
			int max,
			float displayedPercent,
			int incomingDamage,
			float lowHealthThresholdPercent)
		{
			int visibleCurrent = Math.Max(0, current);
			int visibleMax = Math.Max(0, max);
			float targetPercent = visibleMax > 0
				? MathHelper.Clamp(visibleCurrent / (float)visibleMax, 0f, 1f)
				: 0f;
			int clampedIncoming = Math.Clamp(incomingDamage, 0, visibleCurrent);
			bool isLowHealth = targetPercent <= MathHelper.Clamp(lowHealthThresholdPercent, 0f, 1f);

			return new PlayerHudHealthRenderState(
				visibleCurrent,
				visibleMax,
				targetPercent,
				MathHelper.Clamp(displayedPercent, 0f, 1f),
				clampedIncoming,
				isLowHealth);
		}

		public static int CalculateTotalIncomingDamage(EntityManager entityManager, int currentHp)
		{
			var activeSequences = entityManager.GetEntitiesWithComponent<AttackIntent>()
				.Select(entity => entity.GetComponent<AttackIntent>())
				.Where(intent => intent?.Planned != null && intent.Planned.Count > 0)
				.Select(intent => intent.ActiveAttackSequence)
				.ToHashSet();

			if (activeSequences.Count == 0 || currentHp <= 0) return 0;

			long total = 0;
			foreach (var entity in entityManager.GetEntitiesWithComponent<EnemyAttackProgress>())
			{
				var progress = entity.GetComponent<EnemyAttackProgress>();
				if (progress == null
					|| progress.ActualDamage <= 0
					|| !activeSequences.Contains(progress.AttackSequence))
				{
					continue;
				}

				total += progress.ActualDamage;
				if (total >= currentHp) return currentHp;
			}

			return (int)Math.Clamp(total, 0L, currentHp);
		}

		public static Rectangle CalculateTrackBounds(
			Rectangle regionBounds,
			PlayerHudAnchor anchor,
			float labelWidth)
		{
			int contentLeft = regionBounds.X + anchor.HealthPaddingLeft;
			int contentRight = regionBounds.Right - anchor.HealthPaddingRight;
			int contentTop = regionBounds.Y + anchor.HealthPaddingVertical;
			int contentHeight = Math.Max(
				0,
				regionBounds.Height - anchor.HealthPaddingVertical * 2);
			int trackHeight = Math.Min(anchor.HealthTrackHeight, contentHeight);
			int trackX = contentLeft + (int)Math.Ceiling(Math.Max(0f, labelWidth)) + anchor.ContentGap;
			int trackY = contentTop + Math.Max(0, (contentHeight - trackHeight) / 2);

			return new Rectangle(
				trackX,
				trackY,
				Math.Max(0, contentRight - trackX),
				Math.Max(0, trackHeight));
		}

		public static Rectangle CalculateEnemyRegionBounds(
			Rectangle playerHealthBounds,
			Vector2 portraitCenter,
			int portraitTextureHeight,
			float portraitBaseScale,
			int offsetX,
			int offsetY)
		{
			if (playerHealthBounds.Width <= 0
				|| playerHealthBounds.Height <= 0
				|| portraitTextureHeight <= 0)
			{
				return Rectangle.Empty;
			}

			float portraitHalfHeight = portraitTextureHeight * Math.Max(0f, portraitBaseScale) / 2f;
			return new Rectangle(
				(int)Math.Round(portraitCenter.X + offsetX - playerHealthBounds.Width / 2f),
				(int)Math.Round(portraitCenter.Y + portraitHalfHeight + offsetY),
				playerHealthBounds.Width,
				playerHealthBounds.Height);
		}

		public static float CalculatePulseAlpha(
			float elapsedSeconds,
			float frequencyHz,
			float minAlpha,
			float maxAlpha)
		{
			float phase = (float)Math.Sin(
				MathHelper.TwoPi * Math.Max(0.01f, frequencyHz) * Math.Max(0f, elapsedSeconds));
			float normalized = 0.5f * (phase + 1f);
			return MathHelper.Lerp(
				MathHelper.Clamp(minAlpha, 0f, 1f),
				MathHelper.Clamp(maxAlpha, 0f, 1f),
				normalized);
		}

		public static float EaseInOutPow(float t, float power)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			float exponent = Math.Max(1f, power);
			if (t < 0.5f)
			{
				return 0.5f * (float)Math.Pow(t * 2f, exponent);
			}

			return 1f - 0.5f * (float)Math.Pow((1f - t) * 2f, exponent);
		}
	}

	[DebugTab("HUD Health")]
	public class PlayerHudHealthDisplaySystem : Core.System
	{
		private sealed class FillAnimationState
		{
			public float Start;
			public float End;
			public float Displayed;
			public float Elapsed;
			public float Duration;
		}

		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly Dictionary<int, FillAnimationState> _fillAnimations = new();
		private float _elapsedSeconds;

		[DebugEditable(DisplayName = "Fill Tween Seconds", Step = 0.05f, Min = 0.05f, Max = 5f)]
		public float FillTweenSeconds { get; set; } = 0.2f;

		[DebugEditable(DisplayName = "Fill Ease Exponent", Step = 0.1f, Min = 1f, Max = 5f)]
		public float FillEaseExponent { get; set; } = 2.5f;

		[DebugEditable(DisplayName = "Low Health Flash Enabled")]
		public bool LowHealthFlashEnabled { get; set; } = true;

		[DebugEditable(DisplayName = "Low Health Threshold %", Step = 0.05f, Min = 0f, Max = 1f)]
		public float LowHealthThresholdPercent { get; set; } = 0.20f;

		[DebugEditable(DisplayName = "Low Health Flash Hz", Step = 0.1f, Min = 0.1f, Max = 10f)]
		public float LowHealthFlashFrequencyHz { get; set; } = 2.0f;

		[DebugEditable(DisplayName = "Low Health Alpha Min", Step = 0.05f, Min = 0f, Max = 1f)]
		public float LowHealthAlphaMin { get; set; } = 0.35f;

		[DebugEditable(DisplayName = "Low Health Alpha Max", Step = 0.05f, Min = 0f, Max = 1f)]
		public float LowHealthAlphaMax { get; set; } = 1.0f;

		[DebugEditable(DisplayName = "Incoming Damage Flash Enabled")]
		public bool IncomingDamageFlashEnabled { get; set; } = true;

		[DebugEditable(DisplayName = "Incoming Damage Flash Hz", Step = 0.1f, Min = 0.1f, Max = 10f)]
		public float IncomingDamageFlashFrequencyHz { get; set; } = 0.4f;

		[DebugEditable(DisplayName = "Incoming Damage Alpha Min", Step = 0.05f, Min = 0f, Max = 1f)]
		public float IncomingDamageAlphaMin { get; set; } = 0.10f;

		[DebugEditable(DisplayName = "Incoming Damage Alpha Max", Step = 0.05f, Min = 0f, Max = 1f)]
		public float IncomingDamageAlphaMax { get; set; } = 0.45f;

		[DebugEditable(DisplayName = "Enemy Region Offset X", Step = 1, Min = -1000, Max = 1000)]
		public int EnemyRegionOffsetX { get; set; } = 0;

		[DebugEditable(DisplayName = "Enemy Region Offset Y", Step = 1, Min = -1000, Max = 1000)]
		public int EnemyRegionOffsetY { get; set; } = 4;

		public PlayerHudHealthDisplaySystem(
			EntityManager entityManager,
			GraphicsDevice graphicsDevice,
			SpriteBatch spriteBatch) : base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<HP>()
				.Where(entity => entity.HasComponent<Player>() || entity.HasComponent<Enemy>());
		}

		public override void Update(GameTime gameTime)
		{
			_elapsedSeconds += Math.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
			var relevantEntityIds = GetRelevantEntities()
				.Select(entity => entity.Id)
				.ToHashSet();
			foreach (int staleEntityId in _fillAnimations.Keys
				.Where(entityId => !relevantEntityIds.Contains(entityId))
				.ToList())
			{
				_fillAnimations.Remove(staleEntityId);
			}
			base.Update(gameTime);
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			if (entity.HasComponent<SuppressPortraitRender>())
			{
				_fillAnimations.Remove(entity.Id);
				PublishTrackAnchor(entity);
				return;
			}

			var hp = entity.GetComponent<HP>();
			if (hp == null) return;

			float targetPercent = hp.Max > 0
				? MathHelper.Clamp(hp.Current / (float)hp.Max, 0f, 1f)
				: 0f;
			var animation = GetOrCreateAnimation(entity.Id, targetPercent);
			if (Math.Abs(animation.End - targetPercent) > 0.0001f)
			{
				animation.Start = animation.Displayed;
				animation.End = targetPercent;
				animation.Elapsed = 0f;
				animation.Duration = Math.Max(0.001f, FillTweenSeconds);
			}

			animation.Elapsed += Math.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
			float progress = animation.Duration <= 0f
				? 1f
				: MathHelper.Clamp(animation.Elapsed / animation.Duration, 0f, 1f);
			animation.Displayed = MathHelper.Lerp(
				animation.Start,
				animation.End,
				PlayerHudHealthRendering.EaseInOutPow(progress, FillEaseExponent));

			PublishTrackAnchor(entity);
		}

		public void Draw()
		{
			var root = EntityManager.GetEntitiesWithComponent<PlayerHudAnchor>().FirstOrDefault();
			var anchor = root?.GetComponent<PlayerHudAnchor>();
			var healthRegionEntity = GetHealthRegionEntity();
			var healthRegion = healthRegionEntity?.GetComponent<PlayerHudRegion>();
			Rectangle healthBounds = healthRegionEntity == null || healthRegion == null
				? Rectangle.Empty
				: TransformResolverService.ResolveLocalBounds(EntityManager, healthRegionEntity, healthRegion.Bounds);
			var font = FontSingleton.ChakraPetchFont;
			if (anchor == null
				|| healthRegion == null
				|| font == null
				|| !healthRegion.IsVisible
				|| healthBounds.Width <= 0
				|| healthBounds.Height <= 0)
			{
				return;
			}

			var player = EntityManager.GetEntitiesWithComponent<Player>()
				.FirstOrDefault(entity => entity.HasComponent<HP>());
			if (player != null && !player.HasComponent<SuppressPortraitRender>())
			{
				DrawHealthRegion(player, healthBounds, anchor, font, includeIncomingDamage: true);
			}

			foreach (var enemy in EntityManager.GetEntitiesWithComponent<Enemy>()
				.Where(entity => entity.HasComponent<HP>() && !entity.HasComponent<SuppressPortraitRender>()))
			{
				Rectangle enemyBounds = CalculateHealthRegionBounds(enemy, healthBounds);
				DrawHealthRegion(enemy, enemyBounds, anchor, font, includeIncomingDamage: false);
			}
		}

		private void DrawHealthRegion(
			Entity entity,
			Rectangle regionBounds,
			PlayerHudAnchor anchor,
			SpriteFont font,
			bool includeIncomingDamage)
		{
			var hp = entity.GetComponent<HP>();
			if (hp == null || regionBounds.Width <= 0 || regionBounds.Height <= 0) return;

			float displayedPercent = _fillAnimations.TryGetValue(entity.Id, out var animation)
				? animation.Displayed
				: hp.Max > 0
					? MathHelper.Clamp(hp.Current / (float)hp.Max, 0f, 1f)
					: 0f;
			int incomingDamage = includeIncomingDamage
				? PlayerHudHealthRendering.CalculateTotalIncomingDamage(EntityManager, Math.Max(0, hp.Current))
				: 0;
			var renderState = PlayerHudHealthRendering.BuildRenderState(
				hp.Current,
				hp.Max,
				displayedPercent,
				incomingDamage,
				LowHealthThresholdPercent);

			DrawParallelogram(regionBounds, anchor.Slant, anchor.HudBlack);

			float labelWidth = MeasureSpacedText(font, "HP", anchor.LabelFontScale, anchor.LabelLetterSpacing).X;
			var trackBounds = PlayerHudHealthRendering.CalculateTrackBounds(
				regionBounds,
				anchor,
				labelWidth);
			if (trackBounds.Width <= 0 || trackBounds.Height <= 0) return;

			DrawHealthLabel(font, regionBounds, anchor);
			DrawTrack(trackBounds, anchor, renderState);
			DrawFraction(font, trackBounds, anchor, renderState.FractionText);
		}

		private FillAnimationState GetOrCreateAnimation(int entityId, float targetPercent)
		{
			if (_fillAnimations.TryGetValue(entityId, out var animation)) return animation;

			animation = new FillAnimationState
			{
				Start = targetPercent,
				End = targetPercent,
				Displayed = targetPercent,
				Duration = Math.Max(0.001f, FillTweenSeconds),
			};
			_fillAnimations[entityId] = animation;
			return animation;
		}

		private void PublishTrackAnchor(Entity healthEntity)
		{
			var anchor = EntityManager.GetEntitiesWithComponent<PlayerHudAnchor>()
				.FirstOrDefault()
				?.GetComponent<PlayerHudAnchor>();
			var healthRegionEntity = GetHealthRegionEntity();
			var healthRegion = healthRegionEntity?.GetComponent<PlayerHudRegion>();
			Rectangle healthBounds = healthRegionEntity == null || healthRegion == null
				? Rectangle.Empty
				: TransformResolverService.ResolveLocalBounds(EntityManager, healthRegionEntity, healthRegion.Bounds);
			var hpBarAnchor = healthEntity.GetComponent<HPBarAnchor>();
			if (hpBarAnchor == null)
			{
				hpBarAnchor = new HPBarAnchor();
				EntityManager.AddComponent(healthEntity, hpBarAnchor);
			}

			if (anchor == null
				|| healthRegion == null
				|| !healthRegion.IsVisible
				|| healthBounds.Width <= 0
				|| healthBounds.Height <= 0)
			{
				hpBarAnchor.Rect = Rectangle.Empty;
				return;
			}

			var font = FontSingleton.ChakraPetchFont;
			if (font == null)
			{
				hpBarAnchor.Rect = Rectangle.Empty;
				return;
			}

			healthBounds = CalculateHealthRegionBounds(healthEntity, healthBounds);
			if (healthBounds.Width <= 0 || healthBounds.Height <= 0)
			{
				hpBarAnchor.Rect = Rectangle.Empty;
				return;
			}

			float labelWidth = MeasureSpacedText(font, "HP", anchor.LabelFontScale, anchor.LabelLetterSpacing).X;
			hpBarAnchor.Rect = PlayerHudHealthRendering.CalculateTrackBounds(
				healthBounds,
				anchor,
				labelWidth);
		}

		private Rectangle CalculateHealthRegionBounds(Entity healthEntity, Rectangle playerHealthBounds)
		{
			if (healthEntity.HasComponent<SuppressPortraitRender>()) return Rectangle.Empty;
			if (healthEntity.HasComponent<Player>()) return playerHealthBounds;

			var transform = healthEntity.GetComponent<Transform>();
			var portrait = healthEntity.GetComponent<PortraitInfo>();
			if (transform == null || portrait == null) return Rectangle.Empty;

			return PlayerHudHealthRendering.CalculateEnemyRegionBounds(
				playerHealthBounds,
				transform.Position,
				portrait.TextureHeight,
				portrait.BaseScale,
				EnemyRegionOffsetX,
				EnemyRegionOffsetY);
		}

		private Entity GetHealthRegionEntity()
		{
			return EntityManager.GetEntitiesWithComponent<PlayerHudRegion>()
				.FirstOrDefault(entity => entity.GetComponent<PlayerHudRegion>()?.Type == PlayerHudRegionType.Health);
		}

		private void DrawHealthLabel(
			SpriteFont font,
			Rectangle regionBounds,
			PlayerHudAnchor anchor)
		{
			if (font == null) return;

			var labelSize = MeasureSpacedText(
				font,
				"HP",
				anchor.LabelFontScale,
				anchor.LabelLetterSpacing);
			var position = new Vector2(
				regionBounds.X + anchor.HealthPaddingLeft,
				regionBounds.Center.Y - labelSize.Y / 2f);
			DrawSpacedText(
				font,
				"HP",
				position,
				anchor.HudWhite,
				anchor.LabelFontScale,
				anchor.LabelLetterSpacing);
		}

		private void DrawTrack(
			Rectangle trackBounds,
			PlayerHudAnchor anchor,
			PlayerHudHealthRenderState state)
		{
			int trackSlant = CalculateTrackSlant(trackBounds.Height, anchor.Slant);
			DrawParallelogram(trackBounds, trackSlant, anchor.HudRed);

			int border = Math.Min(
				Math.Max(0, anchor.HealthTrackBorderThickness),
				Math.Min(trackBounds.Width, trackBounds.Height) / 2);
			var innerBounds = new Rectangle(
				trackBounds.X + border,
				trackBounds.Y + border,
				Math.Max(0, trackBounds.Width - border * 2),
				Math.Max(0, trackBounds.Height - border * 2));
			if (innerBounds.Width <= 0 || innerBounds.Height <= 0) return;

			int innerSlant = CalculateTrackSlant(innerBounds.Height, anchor.Slant);
			DrawParallelogram(innerBounds, innerSlant, anchor.HudBlack);

			int fillWidth = (int)Math.Round(innerBounds.Width * state.DisplayedPercent);
			if (fillWidth > 0)
			{
				float fillAlpha = LowHealthFlashEnabled && state.IsLowHealth
					? PlayerHudHealthRendering.CalculatePulseAlpha(
						_elapsedSeconds,
						LowHealthFlashFrequencyHz,
						LowHealthAlphaMin,
						LowHealthAlphaMax)
					: 1f;
				var fillBounds = new Rectangle(
					innerBounds.X,
					innerBounds.Y,
					Math.Min(innerBounds.Width, fillWidth),
					innerBounds.Height);
				DrawParallelogram(
					fillBounds,
					Math.Min(innerSlant, fillBounds.Width),
					WithAlpha(anchor.HudRed, fillAlpha));
			}

			if (!IncomingDamageFlashEnabled || state.IncomingDamage <= 0 || state.Max <= 0) return;

			int currentFillWidth = (int)Math.Round(innerBounds.Width * state.TargetPercent);
			int incomingWidth = (int)Math.Round(
				innerBounds.Width * state.IncomingDamage / (float)state.Max);
			int overlayWidth = Math.Min(currentFillWidth, incomingWidth);
			if (overlayWidth <= 0) return;

			var overlayBounds = new Rectangle(
				innerBounds.X + currentFillWidth - overlayWidth,
				innerBounds.Y,
				overlayWidth,
				innerBounds.Height);
			float incomingAlpha = PlayerHudHealthRendering.CalculatePulseAlpha(
				_elapsedSeconds,
				IncomingDamageFlashFrequencyHz,
				IncomingDamageAlphaMin,
				IncomingDamageAlphaMax);
			DrawParallelogram(
				overlayBounds,
				Math.Min(innerSlant, overlayBounds.Width),
				WithAlpha(anchor.HudWhite, incomingAlpha));
		}

		private void DrawFraction(
			SpriteFont font,
			Rectangle trackBounds,
			PlayerHudAnchor anchor,
			string fractionText)
		{
			if (font == null) return;

			var textSize = font.MeasureString(fractionText) * anchor.LabelFontScale;
			var position = new Vector2(
				trackBounds.Center.X - textSize.X / 2f,
				trackBounds.Center.Y - textSize.Y / 2f);
			_spriteBatch.DrawString(
				font,
				fractionText,
				position,
				anchor.HudWhite,
				0f,
				Vector2.Zero,
				anchor.LabelFontScale,
				SpriteEffects.None,
				0f);
		}

		private void DrawParallelogram(Rectangle bounds, int slantPixels, Color color)
		{
			if (bounds.Width <= 0 || bounds.Height <= 0) return;

			int slant = Math.Clamp(slantPixels, 0, bounds.Width);
			float skewPercent = slant * 100f / bounds.Width;
			var mask = PrimitiveTextureFactory.GetParallelogramMask(
				_graphicsDevice,
				bounds.Width,
				bounds.Height,
				skewPercent);
			_spriteBatch.Draw(mask, bounds, color);
		}

		private void DrawSpacedText(
			SpriteFont font,
			string text,
			Vector2 position,
			Color color,
			float scale,
			int spacing)
		{
			float x = position.X;
			foreach (char character in text)
			{
				string glyph = character.ToString();
				_spriteBatch.DrawString(
					font,
					glyph,
					new Vector2(x, position.Y),
					color,
					0f,
					Vector2.Zero,
					scale,
					SpriteEffects.None,
					0f);
				x += font.MeasureString(glyph).X * scale + spacing;
			}
		}

		private static Vector2 MeasureSpacedText(
			SpriteFont font,
			string text,
			float scale,
			int spacing)
		{
			if (font == null || string.IsNullOrEmpty(text)) return Vector2.Zero;

			float width = 0f;
			float height = 0f;
			for (int index = 0; index < text.Length; index++)
			{
				var glyphSize = font.MeasureString(text[index].ToString()) * scale;
				width += glyphSize.X;
				height = Math.Max(height, glyphSize.Y);
				if (index < text.Length - 1) width += Math.Max(0, spacing);
			}
			return new Vector2(width, height);
		}

		private static int CalculateTrackSlant(int height, int slantDegrees)
		{
			float radians = MathHelper.ToRadians(Math.Clamp(slantDegrees, 0, 80));
			return Math.Max(0, (int)Math.Round(Math.Tan(radians) * Math.Max(0, height)));
		}

		private static Color WithAlpha(Color color, float alpha)
		{
			return Color.FromNonPremultiplied(
				color.R,
				color.G,
				color.B,
				(int)Math.Round(MathHelper.Clamp(alpha, 0f, 1f) * 255f));
		}
	}
}
