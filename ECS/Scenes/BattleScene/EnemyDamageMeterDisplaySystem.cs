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

		// Animated values for smooth transitions
		private float _animatedDamage;
		private float _animatedBlock;
		private float _animatedAegis;
		private float _animatedCondition;
		private float _animatedOverflow;

		// Target values (what we're animating toward)
		private int _targetDamage;
		private int _targetBlock;
		private int _targetAegis;
		private int _targetCondition;
		private int _targetOverflow;

		// Absorb animation state (mirrors EnemyAttackDisplaySystem)
		private float _absorbElapsedSeconds;
		private SubPhase? _lastPhase;

		// Logging state — track previous values to avoid per-frame noise
		private bool _lastDrawGuardPassed;
		private float _lastLoggedPanelScale = -1f;

		#region Debug-Editable Fields

		[DebugEditable(DisplayName = "Animation Speed", Step = 1f, Min = 1f, Max = 50f)]
		public float AnimationSpeed { get; set; } = 15f;

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

			// Calculate target values (block prioritized over aegis)
			int baseDamage = progress.DamageBeforePrevention;
			int assignedBlock = Math.Max(0, progress.AssignedBlockTotal);
			// When attack ignores aegis, don't show aegis in the damage meter
			int totalAegis = progress.IgnoresAegis ? 0 : Math.Max(0, progress.AegisTotal);
			int conditionVal = Math.Max(0, progress.PreventedDamageFromBlockCondition);

			int effectiveBlock = Math.Min(assignedBlock, baseDamage);
			int overflowBlock = Math.Max(0, assignedBlock - baseDamage);
			int damageAfterBlock = Math.Max(0, baseDamage - assignedBlock);
			int effectiveAegis = Math.Min(totalAegis, damageAfterBlock);
			int damageVal = Math.Max(0, progress.ActualDamage);

			_targetDamage = damageVal;
			_targetBlock = effectiveBlock;
			_targetAegis = effectiveAegis;
			_targetCondition = conditionVal;
			_targetOverflow = overflowBlock;

			// Lerp animated values toward targets
			float lerpFactor = 1f - (float)Math.Exp(-AnimationSpeed * dt);
			_animatedDamage = MathHelper.Lerp(_animatedDamage, _targetDamage, lerpFactor);
			_animatedBlock = MathHelper.Lerp(_animatedBlock, _targetBlock, lerpFactor);
			_animatedAegis = MathHelper.Lerp(_animatedAegis, _targetAegis, lerpFactor);
			_animatedCondition = MathHelper.Lerp(_animatedCondition, _targetCondition, lerpFactor);
			_animatedOverflow = MathHelper.Lerp(_animatedOverflow, _targetOverflow, lerpFactor);

			// Snap to target if very close (avoid floating point drift)
			const float snapThreshold = 0.01f;
			if (Math.Abs(_animatedDamage - _targetDamage) < snapThreshold) _animatedDamage = _targetDamage;
			if (Math.Abs(_animatedBlock - _targetBlock) < snapThreshold) _animatedBlock = _targetBlock;
			if (Math.Abs(_animatedAegis - _targetAegis) < snapThreshold) _animatedAegis = _targetAegis;
			if (Math.Abs(_animatedCondition - _targetCondition) < snapThreshold) _animatedCondition = _targetCondition;
			if (Math.Abs(_animatedOverflow - _targetOverflow) < snapThreshold) _animatedOverflow = _targetOverflow;
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

			// Build segments list using animated values for width calculation
			// but target values for text display
			// Only include segments that have meaningful animated values (threshold prevents sliver artifacts)
			const float visibilityThreshold = 0.5f;
			var segments = new List<(SegmentType type, float animatedValue, int displayValue, Color color, string label, int overflow)>();

			if (_animatedDamage >= visibilityThreshold)
				segments.Add((SegmentType.Damage, _animatedDamage, _targetDamage, new Color(DamageColorR, DamageColorG, DamageColorB), "Damage", 0));
			if (_animatedBlock >= visibilityThreshold || (_targetOverflow > 0 && _animatedBlock >= 0.01f))
				segments.Add((SegmentType.Block, Math.Max(visibilityThreshold, _animatedBlock), _targetBlock, new Color(BlockColorR, BlockColorG, BlockColorB), "Block", _targetOverflow));
			if (_animatedAegis >= visibilityThreshold)
				segments.Add((SegmentType.Aegis, _animatedAegis, _targetAegis, new Color(AegisColorR, AegisColorG, AegisColorB), "Aegis", 0));
			if (_animatedCondition >= visibilityThreshold)
				segments.Add((SegmentType.Condition, _animatedCondition, _targetCondition, new Color(ConditionColorR, ConditionColorG, ConditionColorB), "Condition", 0));

			if (segments.Count == 0)
			{
				CleanupTooltips(new HashSet<string>());
				return;
			}

			// Calculate proportional widths using animated values
			float totalAnimatedValue = 0f;
			foreach (var seg in segments)
				totalAnimatedValue += Math.Max(0.01f, seg.animatedValue);

			var segmentWidths = new List<float>();
			float baseMeterWidth = TotalMeterWidth;
			float scaledMeterWidth = baseMeterWidth * panelScale;
			float scaledMinSegmentWidth = MinSegmentWidth * panelScale;
			float scaledSegmentGap = SegmentGap * panelScale;

			float availableWidth = scaledMeterWidth - (segments.Count - 1) * Math.Max(0, scaledSegmentGap);
			float usedWidth = 0f;
			for (int i = 0; i < segments.Count; i++)
			{
				float proportion = Math.Max(0.01f, segments[i].animatedValue) / Math.Max(0.01f, totalAnimatedValue);
				// Use minimum width only if this segment has a non-zero target, otherwise let it shrink freely
				bool hasTarget = segments[i].type switch
				{
					SegmentType.Damage => _targetDamage > 0,
					SegmentType.Block => _targetBlock > 0 || _targetOverflow > 0,
					SegmentType.Aegis => _targetAegis > 0,
					SegmentType.Condition => _targetCondition > 0,
					_ => false
				};
				float minW = hasTarget ? scaledMinSegmentWidth : 0f;
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
			float totalWidth = usedWidth + (segments.Count - 1) * scaledSegmentGap + lastSegmentSlant;
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
				var (type, animatedValue, displayValue, color, label, overflow) = segments[i];
				float segWidth = segmentWidths[i];

				// Skip drawing segments with negligible width
				if (segWidth < 1f) continue;

				// Damage segment is elevated
				float yOffset = (type == SegmentType.Damage) ? scaledDamageYOffset : 0;
				float drawY = baseY + yOffset;

				// Scale slant proportionally to segment width to avoid sliver artifacts
				// When width is at MinSegmentWidth, use full slant; when smaller, reduce slant proportionally
				float slantRatio = Math.Min(1f, segWidth / Math.Max(1f, scaledMinSegmentWidth));
				float effectiveSlant = scaledSlant * slantRatio;

				// Draw the parallelogram
				DrawParallelogram(currentX, drawY, segWidth, scaledHeight, effectiveSlant, color);

				currentX += segWidth + scaledSegmentGap;
			}

			// Restart SpriteBatch for text rendering
			_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

			// Draw text on segments (using display values for text)
			currentX = startX;
			for (int i = 0; i < segments.Count; i++)
			{
				var (type, animatedValue, displayValue, color, label, overflow) = segments[i];
				float segWidth = segmentWidths[i];

				// Skip segments with negligible width
				if (segWidth < 1f) continue;

				float yOffset = (type == SegmentType.Damage) ? scaledDamageYOffset : 0;
				float drawY = baseY + yOffset;

				// Calculate effective slant (same as drawing)
				float slantRatio = Math.Min(1f, segWidth / Math.Max(1f, scaledMinSegmentWidth));
				float effectiveSlant = scaledSlant * slantRatio;

				// Draw number centered (accounting for parallelogram slant)
				if (_font != null && (displayValue > 0 || overflow > 0) && scaledFontScale > 0.02f && segWidth > scaledMinSegmentWidth * 0.5f)
				{
					// For block segment with overflow, show as "value (+overflow)"
					string numText = (type == SegmentType.Block && overflow > 0)
						? $"{displayValue} (+{overflow})"
						: displayValue.ToString();

					var textSize = _font.MeasureString(numText) * scaledFontScale;

					// Text color: white for dark backgrounds, black for light
					Color textColor = (type == SegmentType.Aegis) ? Color.Black : Color.White;

					// Center text in parallelogram (shift right by half slant)
					var textPos = new Vector2(
						currentX + segWidth / 2f + effectiveSlant / 2f - textSize.X / 2f,
						drawY + scaledHeight / 2f - textSize.Y / 2f
					);
					_spriteBatch.DrawString(_font, numText, textPos, textColor, 0f, Vector2.Zero, scaledFontScale, SpriteEffects.None, 0f);
				}

				// Update tooltip (only when scale is reasonable and segment is large enough)
				if (panelScale > 0.5f && segWidth > scaledMinSegmentWidth * 0.5f)
				{
					string key = $"DamageMeter_{type}";
					presentKeys.Add(key);
					var segmentRect = new Rectangle((int)currentX, (int)drawY, (int)(segWidth + effectiveSlant), (int)scaledHeight);
					string tooltipText = (type == SegmentType.Block && overflow > 0)
						? $"{label}: {displayValue} (+{overflow} overflow)"
						: $"{label}: {displayValue}";
					UpdateSegmentTooltipUi(key, segmentRect, tooltipText);
				}

				currentX += segWidth + scaledSegmentGap;
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
			_animatedDamage = 0f;
			_animatedBlock = 0f;
			_animatedAegis = 0f;
			_animatedCondition = 0f;
			_animatedOverflow = 0f;
			_targetDamage = 0;
			_targetBlock = 0;
			_targetAegis = 0;
			_targetCondition = 0;
			_targetOverflow = 0;
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
