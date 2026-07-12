using System;
using System.Text.Json.Nodes;
using System.Linq;
using System.Collections.Generic;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// Displays enemy attack damage breakdown as parallelogram segments.
	/// Order: Damage (red, elevated) | Block (black) | Aegis (white) | Condition (green).
	/// Block is prioritized over aegis when displaying prevention.
	/// Segments animate smoothly when values change.
	/// Scales with the enemy attack banner during absorb animation.
	/// </summary>
	[DebugTab("Enemy Damage Meter")]
	public class EnemyDamageMeterDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly SpriteFont _font = FontSingleton.ContentFont;
		private readonly Texture2D _pixel;
		private readonly BasicEffect _basicEffect;

		// Tooltip UIElement entities per segment type
		private readonly Dictionary<string, Entity> _segmentUiEntities = new();

		// Segment types for iteration - ordered: Damage, Block, Aegis, Condition
		private enum SegmentType { Damage, Block, Aegis, Condition }

		// Per-segment presentation state. Gameplay values remain instantaneous.
		private readonly EnemyDamageMeterSegmentAnimation _damageAnimation = new();
		private readonly EnemyDamageMeterSegmentAnimation _blockAnimation = new();
		private readonly EnemyDamageMeterSegmentAnimation _aegisAnimation = new();
		private readonly EnemyDamageMeterSegmentAnimation _conditionAnimation = new();
		private readonly EnemyDamageMeterSegmentAnimation _overflowAnimation = new();
		private bool _hasTargets;
		private int _lastAttackSequence = -1;

		// Absorb animation state (mirrors EnemyAttackDisplaySystem)
		private float _absorbElapsedSeconds;
		private SubPhase? _lastPhase;

		// Logging state — track previous values to avoid per-frame noise
		private bool _lastDrawGuardPassed;
		private float _lastLoggedPanelScale = -1f;

		#region Debug-Editable Fields

		[DebugEditable(DisplayName = "Spring Response", Step = 1f, Min = 1f, Max = 50f)]
		public float AnimationSpeed { get; set; } = 15f;

		[DebugEditable(DisplayName = "Segment Enter/Exit (s)", Step = 0.02f, Min = 0.05f, Max = 1f)]
		public float SegmentPresenceDurationSeconds { get; set; } = 0.18f;

		[DebugEditable(DisplayName = "Change Pulse Duration (s)", Step = 0.02f, Min = 0f, Max = 1f)]
		public float ChangePulseDurationSeconds { get; set; } = 0.2f;

		[DebugEditable(DisplayName = "Change Pulse Scale", Step = 0.01f, Min = 0f, Max = 0.25f)]
		public float ChangePulseScale { get; set; } = 0.06f;

		[DebugEditable(DisplayName = "Change Highlight Strength", Step = 0.01f, Min = 0f, Max = 1f)]
		public float ChangeHighlightStrength { get; set; } = 0.18f;

		[DebugEditable(DisplayName = "Absorb Duration (s)", Step = 0.02f, Min = 0.05f, Max = 3f)]
		public float AbsorbDurationSeconds { get; set; } = 0.4f;

		[DebugEditable(DisplayName = "Total Meter Width", Step = 5, Min = 50, Max = 600)]
		public int TotalMeterWidth { get; set; } = 300;

		[DebugEditable(DisplayName = "Min Segment Width", Step = 2, Min = 10, Max = 100)]
		public int MinSegmentWidth { get; set; } = 40;

		[DebugEditable(DisplayName = "Segment Height", Step = 2, Min = 10, Max = 100)]
		public int SegmentHeight { get; set; } = 40;

		[DebugEditable(DisplayName = "Segment Gap", Step = 1, Min = -20, Max = 20)]
		public int SegmentGap { get; set; } = -8;

		[DebugEditable(DisplayName = "Parallelogram Slant", Step = 2, Min = 0, Max = 40)]
		public int ParallelogramSlant { get; set; } = 18;

		[DebugEditable(DisplayName = "Damage Y Offset", Step = 2, Min = -50, Max = 50)]
		public int DamageYOffset { get; set; } = -8;

		[DebugEditable(DisplayName = "Offset Y from Banner Top", Step = 2, Min = -100, Max = 200)]
		public int OffsetYFromBannerTop { get; set; } = 90;

		[DebugEditable(DisplayName = "Font Scale", Step = 0.02f, Min = 0.05f, Max = 1f)]
		public float FontScale { get; set; } = 0.2f;

		[DebugEditable(DisplayName = "Damage Color R", Step = 5, Min = 0, Max = 255)]
		public int DamageColorR { get; set; } = 200;

		[DebugEditable(DisplayName = "Damage Color G", Step = 5, Min = 0, Max = 255)]
		public int DamageColorG { get; set; } = 40;

		[DebugEditable(DisplayName = "Damage Color B", Step = 5, Min = 0, Max = 255)]
		public int DamageColorB { get; set; } = 40;

		[DebugEditable(DisplayName = "Aegis Color R", Step = 5, Min = 0, Max = 255)]
		public int AegisColorR { get; set; } = 255;

		[DebugEditable(DisplayName = "Aegis Color G", Step = 5, Min = 0, Max = 255)]
		public int AegisColorG { get; set; } = 255;

		[DebugEditable(DisplayName = "Aegis Color B", Step = 5, Min = 0, Max = 255)]
		public int AegisColorB { get; set; } = 255;

		[DebugEditable(DisplayName = "Block Color R", Step = 5, Min = 0, Max = 255)]
		public int BlockColorR { get; set; } = 0;

		[DebugEditable(DisplayName = "Block Color G", Step = 5, Min = 0, Max = 255)]
		public int BlockColorG { get; set; } = 0;

		[DebugEditable(DisplayName = "Block Color B", Step = 5, Min = 0, Max = 255)]
		public int BlockColorB { get; set; } = 0;

		[DebugEditable(DisplayName = "Condition Color R", Step = 5, Min = 0, Max = 255)]
		public int ConditionColorR { get; set; } = 50;

		[DebugEditable(DisplayName = "Condition Color G", Step = 5, Min = 0, Max = 255)]
		public int ConditionColorG { get; set; } = 180;

		[DebugEditable(DisplayName = "Condition Color B", Step = 5, Min = 0, Max = 255)]
		public int ConditionColorB { get; set; } = 50;

		#endregion

		public EnemyDamageMeterDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_pixel = new Texture2D(graphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });

			// Setup BasicEffect for drawing parallelograms
			_basicEffect = new BasicEffect(graphicsDevice)
			{
				VertexColorEnabled = true,
				TextureEnabled = false
			};

			// Subscribe to phase changes to reset absorb timer
			EventManager.Subscribe<ChangeBattlePhaseEvent>(evt =>
			{
				LoggingService.Append("EnemyDamageMeterDisplaySystem.OnChangeBattlePhaseEvent", new JsonObject {
					{ "Current", evt.Current.ToString() },
					{ "Previous", evt.Previous.ToString() }
				});
				if (evt.Current == SubPhase.Block && evt.Previous != SubPhase.Block)
				{
					_absorbElapsedSeconds = 0f;
				}
			});
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<EnemyAttackProgress>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (!IsActive) return;

			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

			// Track phase transitions
			var phaseEntity = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault();
			var currentPhase = phaseEntity?.GetComponent<PhaseState>()?.Sub;

			// Reset absorb timer when entering Block phase
			if (currentPhase == SubPhase.Block && _lastPhase != SubPhase.Block)
			{
				_absorbElapsedSeconds = 0f;
			}

			// Update absorb timer during EnemyAttack phase
			if (currentPhase.HasValue && currentPhase.Value == SubPhase.EnemyAttack)
			{
				_absorbElapsedSeconds += dt;
			}

			_lastPhase = currentPhase;

			if (BattleInputGate.ShouldSuppressEnemyAttackDisplay(EntityManager))
			{
				ResetAnimatedValues();
				return;
			}

			// Get current progress and calculate target values
			var progress = GetCurrentProgress();
			if (progress == null)
			{
				ResetAnimatedValues();
				return;
			}
			if (_lastAttackSequence != progress.AttackSequence)
			{
				ResetAnimatedValues();
				_lastAttackSequence = progress.AttackSequence;
			}

			// Calculate target values (block prioritized over aegis)
			int baseDamage = progress.DamageBeforePrevention;
			int assignedBlock = DamagePredictionService.GetEffectiveAssignedBlockTotal(progress);
			// When attack ignores aegis, don't show aegis in the damage meter
			int totalAegis = progress.IgnoresAegis ? 0 : Math.Max(0, progress.AegisTotal);
			int conditionVal = Math.Max(0, progress.PreventedDamageFromBlockCondition);

			int effectiveBlock = Math.Min(assignedBlock, baseDamage);
			int overflowBlock = Math.Max(0, assignedBlock - baseDamage);
			int damageAfterBlock = Math.Max(0, baseDamage - assignedBlock);
			int effectiveAegis = Math.Min(totalAegis, damageAfterBlock);
			int damageVal = Math.Max(0, progress.ActualDamage);

			bool emphasize = _hasTargets;
			bool overflowChanged = overflowBlock != _overflowAnimation.Target;
			_damageAnimation.Retarget(damageVal, damageVal > 0, ChangePulseDurationSeconds, emphasize);
			_blockAnimation.Retarget(effectiveBlock, effectiveBlock > 0 || overflowBlock > 0, ChangePulseDurationSeconds, emphasize);
			if (emphasize && overflowChanged)
				_blockAnimation.Emphasize(ChangePulseDurationSeconds);
			_aegisAnimation.Retarget(effectiveAegis, effectiveAegis > 0, ChangePulseDurationSeconds, emphasize);
			_conditionAnimation.Retarget(conditionVal, conditionVal > 0, ChangePulseDurationSeconds, emphasize);
			_overflowAnimation.Retarget(overflowBlock, overflowBlock > 0, ChangePulseDurationSeconds, false);
			_hasTargets = true;

			_damageAnimation.Advance(dt, AnimationSpeed, SegmentPresenceDurationSeconds);
			_blockAnimation.Advance(dt, AnimationSpeed, SegmentPresenceDurationSeconds);
			_aegisAnimation.Advance(dt, AnimationSpeed, SegmentPresenceDurationSeconds);
			_conditionAnimation.Advance(dt, AnimationSpeed, SegmentPresenceDurationSeconds);
			_overflowAnimation.Advance(dt, AnimationSpeed, SegmentPresenceDurationSeconds);
		}

		public void Draw()
		{
			// Only render during Block / EnemyAttack phases
			var phaseEntity = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault();
			if (phaseEntity == null) return;
			var phase = phaseEntity.GetComponent<PhaseState>();
			if (phase == null || (phase.Sub != SubPhase.Block && phase.Sub != SubPhase.EnemyAttack)) return;

			if (BattleInputGate.ShouldSuppressEnemyAttackDisplay(EntityManager))
			{
				CleanupTooltips(new HashSet<string>());
				return;
			}

			// Get banner anchor bounds
			var anchorEntity = EntityManager.GetEntitiesWithComponent<EnemyAttackBannerAnchor>().FirstOrDefault();
			if (anchorEntity == null)
			{
				if (_lastDrawGuardPassed)
				{
					LoggingService.Append("EnemyDamageMeterDisplaySystem.DrawGuardFailed", new JsonObject {
						{ "Reason", "AnchorEntityNull" },
						{ "Phase", phase.Sub.ToString() }
					});
					_lastDrawGuardPassed = false;
				}
				return;
			}
			var anchorUi = anchorEntity.GetComponent<UIElement>();
			bool boundsUninitialized = anchorUi == null
				|| anchorUi.Bounds.Width < 2
				|| anchorUi.Bounds.Height < 2
				|| (anchorUi.Bounds.X == 0 && anchorUi.Bounds.Y == 0);
			if (boundsUninitialized)
			{
				if (_lastDrawGuardPassed)
				{
					LoggingService.Append("EnemyDamageMeterDisplaySystem.DrawGuardFailed", new JsonObject {
						{ "Reason", anchorUi == null ? "UIElementNull" : "UninitializedBounds" },
						{ "Bounds", anchorUi == null ? "null" : $"({anchorUi.Bounds.X},{anchorUi.Bounds.Y} {anchorUi.Bounds.Width}x{anchorUi.Bounds.Height})" },
						{ "Phase", phase.Sub.ToString() }
					});
					_lastDrawGuardPassed = false;
				}
				return;
			}

			if (!_lastDrawGuardPassed)
			{
				LoggingService.Append("EnemyDamageMeterDisplaySystem.DrawGuardPassed", new JsonObject {
					{ "Bounds", $"({anchorUi.Bounds.X},{anchorUi.Bounds.Y} {anchorUi.Bounds.Width}x{anchorUi.Bounds.Height})" },
					{ "Phase", phase.Sub.ToString() }
				});
				_lastDrawGuardPassed = true;
			}

			var bannerBounds = anchorUi.Bounds;

			// Log when bounds position looks uninitialized (likely cause of top-left flicker)
			if (bannerBounds.X == 0 && bannerBounds.Y == 0)
			{
				LoggingService.Append("EnemyDamageMeterDisplaySystem.DrawingAtOriginBounds", new JsonObject {
					{ "BannerBounds", $"({bannerBounds.X},{bannerBounds.Y} {bannerBounds.Width}x{bannerBounds.Height})" },
					{ "Phase", phase.Sub.ToString() }
				});
			}

			// Get current progress (for display text showing target values)
			var progress = GetCurrentProgress();
			if (progress == null)
			{
				CleanupTooltips(new HashSet<string>());
				return;
			}

			// Calculate panel scale (mirrors EnemyAttackDisplaySystem absorb animation)
			float panelScale = 1f;
			if (phase.Sub == SubPhase.EnemyAttack)
			{
				var dur = Math.Max(0.05f, AbsorbDurationSeconds);
				float tTween = MathHelper.Clamp(_absorbElapsedSeconds / dur, 0f, 1f);
				float ease = 1f - (float)Math.Pow(1f - tTween, 3); // easeOutCubic
				panelScale = MathHelper.Lerp(1f, 0f, ease);
			}

			// Don't render if fully scaled down
			if (panelScale < 0.01f)
			{
				if (_lastLoggedPanelScale >= 0.01f)
				{
					LoggingService.Append("EnemyDamageMeterDisplaySystem.PanelScaleHidden", new JsonObject {
						{ "PanelScale", panelScale },
						{ "Phase", phase.Sub.ToString() },
						{ "AbsorbElapsed", _absorbElapsedSeconds }
					});
				}
				_lastLoggedPanelScale = panelScale;
				CleanupTooltips(new HashSet<string>());
				return;
			}

			// Log meaningful panel scale transitions (1.0 → animating → hidden)
			float roundedScale = (float)Math.Round(panelScale, 2);
			float roundedLast  = (float)Math.Round(_lastLoggedPanelScale, 2);
			if (Math.Abs(roundedScale - roundedLast) >= 0.05f || (_lastLoggedPanelScale < 0))
			{
				LoggingService.Append("EnemyDamageMeterDisplaySystem.PanelScaleChanged", new JsonObject {
					{ "PanelScale", roundedScale },
					{ "Phase", phase.Sub.ToString() },
					{ "AbsorbElapsed", _absorbElapsedSeconds },
					{ "BannerBounds", $"({bannerBounds.X},{bannerBounds.Y} {bannerBounds.Width}x{bannerBounds.Height})" }
				});
				_lastLoggedPanelScale = panelScale;
			}

			// Keep entering and exiting segments in the layout until their presence envelope completes.
			const float visibilityThreshold = 0.001f;
			var segments = new List<(SegmentType type, EnemyDamageMeterSegmentAnimation animation, Color color, string label)>();

			if (_damageAnimation.Presence > visibilityThreshold)
				segments.Add((SegmentType.Damage, _damageAnimation, new Color(DamageColorR, DamageColorG, DamageColorB), "Damage"));
			if (_blockAnimation.Presence > visibilityThreshold)
				segments.Add((SegmentType.Block, _blockAnimation, new Color(BlockColorR, BlockColorG, BlockColorB), "Block"));
			if (_aegisAnimation.Presence > visibilityThreshold)
				segments.Add((SegmentType.Aegis, _aegisAnimation, new Color(AegisColorR, AegisColorG, AegisColorB), "Aegis"));
			if (_conditionAnimation.Presence > visibilityThreshold)
				segments.Add((SegmentType.Condition, _conditionAnimation, new Color(ConditionColorR, ConditionColorG, ConditionColorB), "Condition"));

			if (segments.Count == 0)
			{
				CleanupTooltips(new HashSet<string>());
				return;
			}

			// Calculate proportional widths using animated values
			float totalAnimatedValue = 0f;
			foreach (var seg in segments)
				totalAnimatedValue += Math.Max(0.01f, seg.animation.Value) * seg.animation.Presence;

			var segmentWidths = new List<float>();
			float baseMeterWidth = TotalMeterWidth;
			float scaledMeterWidth = baseMeterWidth * panelScale;
			float scaledMinSegmentWidth = MinSegmentWidth * panelScale;
			float scaledSegmentGap = SegmentGap * panelScale;

			var segmentGaps = new List<float>();
			float totalPositiveGap = 0f;
			for (int i = 0; i < segments.Count - 1; i++)
			{
				float gapPresence = Math.Min(segments[i].animation.Presence, segments[i + 1].animation.Presence);
				float gap = scaledSegmentGap * gapPresence;
				segmentGaps.Add(gap);
				totalPositiveGap += Math.Max(0f, gap);
			}

			float availableWidth = scaledMeterWidth - totalPositiveGap;
			float usedWidth = 0f;
			for (int i = 0; i < segments.Count; i++)
			{
				float weightedValue = Math.Max(0.01f, segments[i].animation.Value) * segments[i].animation.Presence;
				float proportion = weightedValue / Math.Max(0.01f, totalAnimatedValue);
				float minW = scaledMinSegmentWidth * EnemyDamageMeterAnimationService.EasePresence(segments[i].animation.Presence);
				float segW = Math.Max(minW, availableWidth * proportion);
				if (i == segments.Count - 1)
					segW = Math.Max(minW, availableWidth - usedWidth);
				segmentWidths.Add(segW);
				usedWidth += segW;
			}

			// Scaled dimensions (calculate before centering since we need slant for total width)
			float scaledHeight = SegmentHeight * panelScale;
			float scaledSlant = ParallelogramSlant * panelScale;

			// Calculate the effective slant of the rightmost segment for proper centering
			// The parallelogram's visual width is (width + slant), so total visual width includes the last segment's slant
			float lastSegmentSlant = 0f;
			if (segments.Count > 0 && segmentWidths.Count > 0)
			{
				float lastSegWidth = segmentWidths[segmentWidths.Count - 1];
				float lastSlantRatio = Math.Min(1f, lastSegWidth / Math.Max(1f, scaledMinSegmentWidth));
				lastSegmentSlant = scaledSlant * lastSlantRatio;
			}

			// Calculate total visual width and center position (using banner center)
			// Total visual width = segment widths + gaps + rightmost slant extension
			float totalGapWidth = 0f;
			foreach (float gap in segmentGaps) totalGapWidth += gap;
			float totalWidth = usedWidth + totalGapWidth + lastSegmentSlant;
			float startX = bannerBounds.Center.X - totalWidth / 2f;

			// Scale the Y offset and position relative to banner
			float scaledOffsetY = OffsetYFromBannerTop * panelScale;
			float baseY = bannerBounds.Top + scaledOffsetY;
			float scaledDamageYOffset = DamageYOffset * panelScale;
			float scaledFontScale = FontScale * panelScale;

			// End SpriteBatch to draw parallelograms with BasicEffect
			_spriteBatch.End();

			// Setup BasicEffect matrices
			_basicEffect.World = Matrix.Identity;
			_basicEffect.View = Matrix.Identity;
			_basicEffect.Projection = Matrix.CreateOrthographicOffCenter(
				0, Game1.VirtualWidth,
				Game1.VirtualHeight, 0,
				0, 1);

			var presentKeys = new HashSet<string>();
			float currentX = startX;

			// Draw each segment as parallelogram
			for (int i = 0; i < segments.Count; i++)
			{
				var (type, animation, color, label) = segments[i];
				float segWidth = segmentWidths[i];

				// Skip drawing segments with negligible width
				if (segWidth < 1f) continue;

				// Damage segment is elevated
				float yOffset = (type == SegmentType.Damage) ? scaledDamageYOffset : 0;
				float drawY = baseY + yOffset;
				float presence = EnemyDamageMeterAnimationService.EasePresence(animation.Presence);
				float emphasis = EnemyDamageMeterAnimationService.GetEmphasisAmount(animation);
				float pulseScale = 1f + emphasis * ChangePulseScale;
				float drawWidth = segWidth * pulseScale;
				float drawHeight = scaledHeight * presence * pulseScale;
				float pulseX = currentX - (drawWidth - segWidth) / 2f;
				float pulseY = drawY + (scaledHeight - drawHeight) / 2f;

				// Scale slant proportionally to segment width to avoid sliver artifacts
				// When width is at MinSegmentWidth, use full slant; when smaller, reduce slant proportionally
				float slantRatio = Math.Min(1f, drawWidth / Math.Max(1f, scaledMinSegmentWidth));
				float effectiveSlant = scaledSlant * slantRatio * pulseScale;
				Color drawColor = Color.Lerp(color, Color.White, emphasis * ChangeHighlightStrength) * presence;

				// Draw the parallelogram
				DrawParallelogram(pulseX, pulseY, drawWidth, drawHeight, effectiveSlant, drawColor);

				currentX += segWidth + (i < segmentGaps.Count ? segmentGaps[i] : 0f);
			}

			// Restart SpriteBatch for text rendering
			_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

			// Draw text on segments (using display values for text)
			currentX = startX;
			for (int i = 0; i < segments.Count; i++)
			{
				var (type, animation, color, label) = segments[i];
				float segWidth = segmentWidths[i];

				// Skip segments with negligible width
				if (segWidth < 1f) continue;

				float yOffset = (type == SegmentType.Damage) ? scaledDamageYOffset : 0;
				float drawY = baseY + yOffset;
				float presence = EnemyDamageMeterAnimationService.EasePresence(animation.Presence);
				float emphasis = EnemyDamageMeterAnimationService.GetEmphasisAmount(animation);
				float pulseScale = 1f + emphasis * ChangePulseScale;
				float drawWidth = segWidth * pulseScale;
				float drawHeight = scaledHeight * presence * pulseScale;
				float pulseX = currentX - (drawWidth - segWidth) / 2f;
				float pulseY = drawY + (scaledHeight - drawHeight) / 2f;

				// Calculate effective slant (same as drawing)
				float slantRatio = Math.Min(1f, drawWidth / Math.Max(1f, scaledMinSegmentWidth));
				float effectiveSlant = scaledSlant * slantRatio * pulseScale;
				int displayValue = EnemyDamageMeterAnimationService.GetDisplayedValue(animation.Value, animation.Target);
				int overflow = type == SegmentType.Block
					? EnemyDamageMeterAnimationService.GetDisplayedValue(_overflowAnimation.Value, _overflowAnimation.Target)
					: 0;
				float segmentFontScale = scaledFontScale * presence * pulseScale;

				// Draw number centered (accounting for parallelogram slant)
				if (_font != null && (displayValue > 0 || overflow > 0) && segmentFontScale > 0.02f && drawWidth > scaledMinSegmentWidth * 0.5f)
				{
					// For block segment with overflow, show as "value (+overflow)"
					string numText = (type == SegmentType.Block && overflow > 0)
						? $"{displayValue} (+{overflow})"
						: displayValue.ToString();

					var textSize = _font.MeasureString(numText) * segmentFontScale;

					// Text color: white for dark backgrounds, black for light
					Color textColor = ((type == SegmentType.Aegis) ? Color.Black : Color.White) * presence;

					// Center text in parallelogram (shift right by half slant)
					var textPos = new Vector2(
						pulseX + drawWidth / 2f + effectiveSlant / 2f - textSize.X / 2f,
						pulseY + drawHeight / 2f - textSize.Y / 2f
					);
					_spriteBatch.DrawString(_font, numText, textPos, textColor, 0f, Vector2.Zero, segmentFontScale, SpriteEffects.None, 0f);
				}

				// Update tooltip (only when scale is reasonable and segment is large enough)
				if (panelScale > 0.5f && presence > 0.8f && drawWidth > scaledMinSegmentWidth * 0.5f)
				{
					string key = $"DamageMeter_{type}";
					presentKeys.Add(key);
					var segmentRect = new Rectangle((int)pulseX, (int)pulseY, (int)(drawWidth + effectiveSlant), (int)drawHeight);
					string tooltipText = (type == SegmentType.Block && _overflowAnimation.Target > 0)
						? $"{label}: {animation.Target} (+{_overflowAnimation.Target} overflow)"
						: $"{label}: {animation.Target}";
					UpdateSegmentTooltipUi(key, segmentRect, tooltipText);
				}

				currentX += segWidth + (i < segmentGaps.Count ? segmentGaps[i] : 0f);
			}

			// Cleanup tooltips for segments no longer present
			CleanupTooltips(presentKeys);
		}

		private void DrawParallelogram(float x, float y, float width, float height, float slant, Color color)
		{
			// Parallelogram vertices (slanted to the right):
			// Top-left is shifted right by 'slant'
			//
			//     TL------TR
			//    /        /
			//   BL------BR
			//
			// TL = (x + slant, y)
			// TR = (x + slant + width, y)
			// BR = (x + width, y + height)
			// BL = (x, y + height)

			var vertices = new VertexPositionColor[4];
			vertices[0] = new VertexPositionColor(new Vector3(x + slant, y, 0), color);           // TL
			vertices[1] = new VertexPositionColor(new Vector3(x + slant + width, y, 0), color);   // TR
			vertices[2] = new VertexPositionColor(new Vector3(x + width, y + height, 0), color);  // BR
			vertices[3] = new VertexPositionColor(new Vector3(x, y + height, 0), color);          // BL

			// Draw as triangle strip: TL, TR, BL, BR
			var indices = new short[] { 0, 1, 3, 1, 2, 3 };

			foreach (var pass in _basicEffect.CurrentTechnique.Passes)
			{
				pass.Apply();
				_graphicsDevice.DrawUserIndexedPrimitives(
					PrimitiveType.TriangleList,
					vertices, 0, 4,
					indices, 0, 2);
			}
		}

		private void ResetAnimatedValues()
		{
			_damageAnimation.Reset();
			_blockAnimation.Reset();
			_aegisAnimation.Reset();
			_conditionAnimation.Reset();
			_overflowAnimation.Reset();
			_hasTargets = false;
			_lastAttackSequence = -1;
		}

		private EnemyAttackProgress GetCurrentProgress()
		{
			return EnemyAttackFlowService.TryGetCurrentProgress(EntityManager, out var progress)
				? progress
				: null;
		}

		private void UpdateSegmentTooltipUi(string key, Rectangle rect, string tooltipText)
		{
			if (!_segmentUiEntities.TryGetValue(key, out var uiEntity) || uiEntity == null)
			{
				uiEntity = EntityManager.CreateEntity($"UI_DamageMeter_{key}");
				EntityManager.AddComponent(uiEntity, new Transform
				{
					Position = new Vector2(rect.X, rect.Y),
					ZOrder = 10000
				});
				EntityManager.AddComponent(uiEntity, new UIElement
				{
					Bounds = rect,
					IsInteractable = false,
					Tooltip = tooltipText,
					TooltipPosition = TooltipPosition.Below,
					TooltipOffsetPx = 8
				});
				EntityManager.AddComponent(uiEntity, ParallaxLayer.GetUIParallaxLayer());
				_segmentUiEntities[key] = uiEntity;
			}
			else
			{
				var tr = uiEntity.GetComponent<Transform>();
				if (tr != null)
				{
					tr.Position = new Vector2(rect.X, rect.Y);
					tr.ZOrder = 10000;
				}
				var ui = uiEntity.GetComponent<UIElement>();
				if (ui != null)
				{
					ui.Bounds = rect;
					ui.Tooltip = tooltipText;
					ui.TooltipPosition = TooltipPosition.Below;
					ui.TooltipOffsetPx = 8;
					ui.IsInteractable = false;
				}
			}
		}

		private void CleanupTooltips(HashSet<string> presentKeys)
		{
			var toRemove = new List<string>();
			foreach (var kvp in _segmentUiEntities)
			{
				if (!presentKeys.Contains(kvp.Key))
				{
					if (kvp.Value != null)
					{
						EntityManager.DestroyEntity(kvp.Value.Id);
					}
					toRemove.Add(kvp.Key);
				}
			}
			foreach (var k in toRemove)
			{
				_segmentUiEntities.Remove(k);
			}
		}
	}
}
