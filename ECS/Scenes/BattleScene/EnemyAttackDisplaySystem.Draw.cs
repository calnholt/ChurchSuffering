using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	public partial class EnemyAttackDisplaySystem
	{
		private struct DrawContext
		{
			public Rectangle Rect;
			public float PanelScale, SquashX, SquashY, ContentScale;
			public Vector2 Shake, ApproachPos;
			public int Padding, BgAlpha, DrawW, DrawH;
			public SubPhase PhaseNow;
			public Entity Enemy, AnchorEntity;
			public PlannedAttack PlannedAttack;
			public EnemyAttackBase Def;
			public EnemyAttackProgress Progress;
			public List<(string text, float scale, Color color, bool centerTitle)> WrappedLines;
		}

		public void Draw()
		{
			var ctx = BuildDrawContext();
			if (ctx == null) return;
			var c = ctx.Value;

			DrawPanelBackground(c);
			DrawAttackDecorations(c.Rect, c.PanelScale, c.SquashX, c.SquashY);
			UpdateAnchorEntity(c);
			DrawImpactFlash(c);
			DrawCrater(c);
			DrawDebris(c);
			DrawTextContent(c);
			DrawConfirmButton(c);
			UpdateBannerAnchorTransform(c);
		}

		private DrawContext? BuildDrawContext()
		{
			if (BattleInputGate.ShouldSuppressEnemyAttackDisplay(EntityManager)) return null;

			var enemy = GetRelevantEntities().FirstOrDefault();
			var intent = enemy?.GetComponent<AttackIntent>();
			if (intent == null || intent.Planned.Count == 0 || _contentFont == null || _bodyFont == null) return null;

			var phaseNow = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault().GetComponent<PhaseState>().Sub;
			if (phaseNow != SubPhase.Block && phaseNow != SubPhase.EnemyAttack) return null;
			if (!_showBanner) return null;

			// Gate display during ambush intro
			var ambushState = EntityManager.GetEntitiesWithComponent<AmbushState>().FirstOrDefault()?.GetComponent<AmbushState>();
			if (ambushState != null && ambushState.IsActive && ambushState.IntroActive) return null;
			if (_absorbCompleteFired) return null;

			var pa = intent.Planned[0];
			var def = pa.AttackDefinition;
			if (def == null) return null;

			// Build text lines
			var lines = new List<(string text, float scale, Color color)>();
			lines.Add((def.Name, TitleScale, Color.White));
			var progress = FindEnemyAttackProgress();
			if (progress != null)
			{
				bool conditionMet = GuidedTutorialService.IsActive(EntityManager)
					? BattleInputGate.IsTutorialActionAllowed(EntityManager, TutorialAction.ConfirmBlocks)
					: progress.IsConditionMet;
				lines.Add(($"{def.Text}", TextScale, GetConditionTextColor(conditionMet)));
			}

			// Measure panel
			int pad = Math.Max(0, PanelPadding);
			int vx = Game1.VirtualWidth;
			int vy = Game1.VirtualHeight;
			float percent = Math.Clamp(PanelMaxWidthPercent, 0.1f, 1f);
			int maxPanelWidthPx = (int)Math.Round(vx * percent);
			float minPercent = Math.Clamp(PanelMinWidthPercent, 0f, 1f);
			int minPanelWidthPx = (int)Math.Round(vx * minPercent);
			int contentWidthLimitPx = Math.Max(50, maxPanelWidthPx - pad * 2);

			// Wrap text lines
			var wrappedLines = new List<(string text, float scale, Color color, bool centerTitle)>();
			for (int i = 0; i < lines.Count; i++)
			{
				var (text, lineScale, color) = lines[i];
				bool centerTitle = (i == 0);
				var font = centerTitle ? _contentFont : _bodyFont;
				var parts = TextUtils.WrapText(font, text, lineScale, contentWidthLimitPx);
				foreach (var p in parts)
				{
					wrappedLines.Add((p, lineScale, color, centerTitle));
				}
			}

			// Calculate panel dimensions
			float maxW = 0f;
			float totalH = 0f;
			bool isFirstTitleForHeight = true;
			foreach (var (text, lineScale, _, centerTitle) in wrappedLines)
			{
				var font = centerTitle ? _contentFont : _bodyFont;
				var sz = font.MeasureString(text);
				maxW = Math.Max(maxW, sz.X * lineScale);
				float spacing = (isFirstTitleForHeight && centerTitle) ? TitleSpacingExtra : LineSpacingExtra;
				totalH += sz.Y * lineScale + spacing;
				if (centerTitle) isFirstTitleForHeight = false;
			}
			int w = (int)Math.Ceiling(Math.Min(maxW + pad * 2, maxPanelWidthPx));
			w = Math.Max(w, minPanelWidthPx);
			int h = (int)Math.Ceiling(totalH) + pad * 2;

			var anchorEntity = EntityManager.GetEntitiesWithComponent<EnemyAttackBannerAnchor>().FirstOrDefault();
			if (anchorEntity == null) return null; // Anchor not yet created by Update
			var anchorTransform = anchorEntity.GetComponent<Transform>();
			// Read parallax-adjusted center from the anchor entity
			var center = anchorTransform?.Position ?? new Vector2(vx / 2f + OffsetX, vy / 2f + OffsetY);

			// Absorb tween
			Vector2 approachPos = center;
			float panelScale = 1f;
			if (phaseNow == SubPhase.EnemyAttack)
			{
				var enemyT = enemy?.GetComponent<Transform>();
				(panelScale, approachPos) = EnemyAttackAnimationService.ComputeAbsorbTween(
					center, enemyT.Position, AbsorbTargetYOffset, _absorbElapsedSeconds, AbsorbDurationSeconds);
			}

			// Impact squash/stretch + shake
			Vector2 shake = Vector2.Zero;
			float squashX = 1f;
			float squashY = 1f;
			float contentScale = 1f;
			if (_impactActive)
			{
				(squashX, squashY, contentScale) = EnemyAttackAnimationService.ComputeImpactSquash(
					_squashElapsedSeconds, SquashDurationSeconds, SquashXFactor, SquashYFactor, OvershootIntensity);
				shake = EnemyAttackAnimationService.ComputeShake(
					_shakeElapsedSeconds, ShakeDurationSeconds, ShakeAmplitudePx, _rand);
			}

			int bgAlpha = Math.Clamp(BackgroundAlpha, 0, 255);
			int drawW = (int)Math.Round(w * panelScale * squashX);
			int drawH = (int)Math.Round(h * panelScale * squashY);
			var rect = new Rectangle(
				(int)(approachPos.X - drawW / 2f + shake.X),
				(int)(approachPos.Y + shake.Y),
				drawW, drawH);

			return new DrawContext
			{
				Rect = rect,
				PanelScale = panelScale,
				SquashX = squashX,
				SquashY = squashY,
				ContentScale = contentScale,
				Shake = shake,
				ApproachPos = approachPos,
				Padding = pad,
				BgAlpha = bgAlpha,
				DrawW = drawW,
				DrawH = drawH,
				PhaseNow = phaseNow,
				Enemy = enemy,
				AnchorEntity = anchorEntity,
				PlannedAttack = pa,
				Def = def,
				Progress = progress,
				WrappedLines = wrappedLines,
			};
		}

		// --- Sub-draw methods ---

		private void DrawPanelBackground(DrawContext ctx)
		{
			_spriteBatch.Draw(_pixel, ctx.Rect, new Color(20, 20, 20, ctx.BgAlpha));
			DrawRect(ctx.Rect, Color.White, Math.Max(0, BorderThickness));
		}

		private void DrawImpactFlash(DrawContext ctx)
		{
			if (!_impactActive || _flashElapsedSeconds >= FlashDurationSeconds || FlashMaxAlpha <= 0) return;
			float ft = 1f - Math.Clamp(_flashElapsedSeconds / Math.Max(0.0001f, FlashDurationSeconds), 0f, 1f);
			int fa = (int)(FlashMaxAlpha * ft);
			_spriteBatch.Draw(_pixel, ctx.Rect, new Color(255, 255, 255, Math.Clamp(fa, 0, 255)));
		}

		private void DrawCrater(DrawContext ctx)
		{
			if (!_impactActive || _craterElapsedSeconds >= CraterDurationSeconds || CraterMaxAlpha <= 0) return;
			float ct = Math.Clamp(_craterElapsedSeconds / Math.Max(0.0001f, CraterDurationSeconds), 0f, 1f);
			int cexp = (int)Math.Round(CraterMaxExpandPx * ct);
			int ca = (int)Math.Round(CraterMaxAlpha * (1f - ct));
			var craterRect = new Rectangle(
				ctx.Rect.X - cexp, ctx.Rect.Y - cexp,
				ctx.Rect.Width + cexp * 2, ctx.Rect.Height + cexp * 2);
			_spriteBatch.Draw(_pixel, craterRect, new Color(10, 10, 10, Math.Clamp(ca, 0, 255)));
		}

		private void DrawDebris(DrawContext ctx)
		{
			if (!_impactActive || _debris.Count <= 0) return;
			var debrisBase = new Vector2(ctx.Rect.X + ctx.Rect.Width / 2f, ctx.Rect.Y + ctx.Rect.Height / 2f);
			for (int i = 0; i < _debris.Count; i++)
			{
				var d = _debris[i];
				if (d.Age <= d.Lifetime)
				{
					int ds = (int)Math.Max(1, d.Size);
					var p = new Rectangle(
						(int)(debrisBase.X + d.Position.X + ctx.Shake.X),
						(int)(debrisBase.Y + d.Position.Y + ctx.Shake.Y),
						ds, ds);
					_spriteBatch.Draw(_pixel, p, d.Color);
				}
			}
		}

		private void DrawTextContent(DrawContext ctx)
		{
			float y = ctx.Rect.Y + ctx.Padding * ctx.PanelScale * ctx.ContentScale;

			bool isFirstTitleLine = true;

			// Track bounds of def.Text lines (non-title lines) for tooltip
			float defTextMinX = float.MaxValue;
			float defTextMinY = float.MaxValue;
			float defTextMaxX = float.MinValue;
			float defTextMaxY = float.MinValue;
			bool hasDefTextLines = false;

			foreach (var (text, baseScale, color, centerTitle) in ctx.WrappedLines)
			{
				float s = baseScale * ctx.PanelScale * ctx.ContentScale;
				var font = centerTitle ? _contentFont : _bodyFont;
				var sz = font.MeasureString(text);
				float textWidth = sz.X * s;
				float textHeight = sz.Y * s;
				float x = centerTitle
					? ctx.Rect.X + (ctx.Rect.Width - textWidth) / 2f
					: ctx.Rect.X + ctx.Padding * ctx.PanelScale * ctx.ContentScale;
				_spriteBatch.DrawString(font, text, new Vector2(x, y), color, 0f, Vector2.Zero, s, SpriteEffects.None, 0f);

				// Track bounds for non-title lines (these are the def.Text lines)
				if (!centerTitle)
				{
					defTextMinX = Math.Min(defTextMinX, x);
					defTextMinY = Math.Min(defTextMinY, y);
					defTextMaxX = Math.Max(defTextMaxX, x + textWidth);
					defTextMaxY = Math.Max(defTextMaxY, y + textHeight);
					hasDefTextLines = true;
				}

				float extraSpacing = (isFirstTitleLine && centerTitle)
					? TitleSpacingExtra
					: LineSpacingExtra;
				y += sz.Y * s + extraSpacing * ctx.PanelScale * ctx.ContentScale;
				if (centerTitle) isFirstTitleLine = false;
			}

			// Create or update tooltip entity for def.Text if keywords are present
			var keywordBlocks = TooltipTextService.GetKeywordTooltipBlocks(ctx.Def.Text);
			if (keywordBlocks.Count > 0 && hasDefTextLines)
			{
				var defTextBounds = new Rectangle(
					(int)Math.Floor(defTextMinX),
					(int)Math.Floor(defTextMinY),
					(int)Math.Ceiling(defTextMaxX - defTextMinX),
					(int)Math.Ceiling(defTextMaxY - defTextMinY)
				);

				if (_attackTextTooltipEntity == null)
				{
					_attackTextTooltipEntity = EntityManager.CreateEntity("UI_EnemyAttackTextTooltip");
					EntityManager.AddComponent(_attackTextTooltipEntity, new Transform { Position = new Vector2(defTextBounds.X, defTextBounds.Y), ZOrder = 10001 });
					EntityManager.AddComponent(_attackTextTooltipEntity, new UIElement
					{
						Bounds = defTextBounds,
						IsInteractable = false,
						Tooltip = string.Empty,
						TooltipKeywordSource = ctx.Def.Text ?? string.Empty,
						TooltipType = TooltipType.Text,
						TooltipPosition = TooltipPosition.Below
					});
				}
				else
				{
					var tr = _attackTextTooltipEntity.GetComponent<Transform>();
					if (tr != null)
					{
						tr.Position = new Vector2(defTextBounds.X, defTextBounds.Y);
						tr.ZOrder = 10001;
					}
					var ui = _attackTextTooltipEntity.GetComponent<UIElement>();
					if (ui != null)
					{
						ui.Bounds = defTextBounds;
						ui.Tooltip = string.Empty;
						ui.TooltipKeywordSource = ctx.Def.Text ?? string.Empty;
						ui.TooltipType = TooltipType.Text;
						ui.TooltipPosition = TooltipPosition.Below;
						ui.IsInteractable = false;
					}
				}
			}
			else
			{
				// No keywords found or no def.Text lines - cleanup tooltip entity if it exists
				if (_attackTextTooltipEntity != null)
				{
					EntityManager.DestroyEntity(_attackTextTooltipEntity.Id);
					_attackTextTooltipEntity = null;
				}
			}
		}

		private void DrawConfirmButton(DrawContext ctx)
		{
			Entity primaryBtn = EntityManager.GetEntity("UIButton_ConfirmEnemyAttack");
			var ui = primaryBtn?.GetComponent<UIElement>();
			var isInteractable = ui?.IsInteractable ?? false;
			bool showConfirm = isInteractable
				&& EnemyAttackConfirmAvailabilityService.CanRequestCurrentAttackConfirm(
					EntityManager,
					_confirmedAttackSequences);

			if (showConfirm)
			{
				// Read parallax-adjusted button position (written in Update, offset by parallax)
				var btnTr = primaryBtn.GetComponent<Transform>();
				var btnPos = btnTr?.Position ?? new Vector2(ctx.Rect.X + ctx.Rect.Width / 2f - ConfirmButtonWidth / 2f, ctx.Rect.Bottom + ConfirmButtonOffsetY);
				var btnRect = new Rectangle(
					(int)btnPos.X,
					(int)btnPos.Y,
					ConfirmButtonWidth,
					ConfirmButtonHeight
				);
				// Ensure cached confirm button texture
				string label = "Confirm";
				if (_cachedConfirmTexture == null || _cachedConfirmText != label)
				{
					_cachedConfirmTexture?.Dispose();
					_cachedConfirmTexture = ButtonTextureFactory.Create(
						_graphicsDevice, label, Color.White, Color.DarkRed);
					_cachedConfirmText = label;
				}
				_spriteBatch.Draw(_cachedConfirmTexture,
					new Rectangle(btnRect.X, btnRect.Y, btnRect.Width, btnRect.Height),
					Color.White);

				if (ui != null) { ui.Bounds = btnRect; }
			}
		}

		internal static Color GetConditionTextColor(bool conditionMet) =>
			conditionMet ? Color.White : new Color(255, 150, 150, 255);

		private void UpdateAnchorEntity(DrawContext ctx)
		{
			var anchorUi = ctx.AnchorEntity.GetComponent<UIElement>();
			if (anchorUi == null)
			{
				anchorUi = new UIElement { Bounds = ctx.Rect, IsInteractable = false };
				EntityManager.AddComponent(ctx.AnchorEntity, anchorUi);
			}
			else
			{
				anchorUi.Bounds = ctx.Rect;
				anchorUi.IsInteractable = false;
			}
		}

		private void UpdateBannerAnchorTransform(DrawContext ctx)
		{
			var anchorTransform = ctx.AnchorEntity.GetComponent<Transform>();
			if (anchorTransform != null)
			{
				anchorTransform.Scale = Vector2.One;
				anchorTransform.Rotation = 0f;
			}
		}

		private Rectangle CalculateBannerRect(Entity enemy, SubPhase phaseNow, EnemyAttackBase def)
		{
			int pad = Math.Max(0, PanelPadding);
			int vx = Game1.VirtualWidth;
			int vy = Game1.VirtualHeight;
			float percent = Math.Clamp(PanelMaxWidthPercent, 0.1f, 1f);
			int maxPanelWidthPx = (int)Math.Round(vx * percent);
			float minPercent = Math.Clamp(PanelMinWidthPercent, 0f, 1f);
			int minPanelWidthPx = (int)Math.Round(vx * minPercent);
			int contentWidthLimitPx = Math.Max(50, maxPanelWidthPx - pad * 2);

			var lines = new List<(string text, float scale, SpriteFont font)>
			{
				(def.Name, TitleScale, _contentFont),
				(def.Text, TextScale, _bodyFont),
			};

			float maxW = 0f;
			float totalH = 0f;
			bool isFirstTitle = true;
			foreach (var (text, lineScale, font) in lines)
			{
				var parts = TextUtils.WrapText(font, text, lineScale, contentWidthLimitPx);
				foreach (var p in parts)
				{
					var sz = font.MeasureString(p);
					maxW = Math.Max(maxW, sz.X * lineScale);
					float spacing = isFirstTitle ? TitleSpacingExtra : LineSpacingExtra;
					totalH += sz.Y * lineScale + spacing;
					if (isFirstTitle) isFirstTitle = false;
				}
			}

			int w = (int)Math.Ceiling(Math.Min(maxW + pad * 2, maxPanelWidthPx));
			w = Math.Max(w, minPanelWidthPx);
			int h = (int)Math.Ceiling(totalH) + pad * 2;

			// Calculate animation effects via shared service
			float panelScale = 1f;
			float squashX = 1f;
			float squashY = 1f;

			if (phaseNow == SubPhase.EnemyAttack)
			{
				(panelScale, _) = EnemyAttackAnimationService.ComputeAbsorbTween(
					Vector2.Zero, Vector2.Zero, 0, _absorbElapsedSeconds, AbsorbDurationSeconds);
			}

			if (_impactActive)
			{
				(squashX, squashY, _) = EnemyAttackAnimationService.ComputeImpactSquash(
					_squashElapsedSeconds, SquashDurationSeconds, SquashXFactor, SquashYFactor, OvershootIntensity);
			}

			int drawW = (int)Math.Round(w * panelScale * squashX);
			int drawH = (int)Math.Round(h * panelScale * squashY);

			// Calculate position
			var centerBase = new Vector2(vx / 2f + OffsetX, vy / 2f + OffsetY);
			var center = centerBase;
			Vector2 approachPos = center;

			if (phaseNow == SubPhase.EnemyAttack)
			{
				var enemyT = enemy?.GetComponent<Transform>();
				if (enemyT != null)
				{
					(_, approachPos) = EnemyAttackAnimationService.ComputeAbsorbTween(
						center, enemyT.Position, AbsorbTargetYOffset, _absorbElapsedSeconds, AbsorbDurationSeconds);
				}
			}

			return new Rectangle(
				(int)(approachPos.X - drawW / 2f),
				(int)(approachPos.Y),
				drawW,
				drawH
			);
		}

		private void UpdateAnchorBounds(Rectangle bannerRect)
		{
			var anchorEntity = EntityManager.GetEntitiesWithComponent<EnemyAttackBannerAnchor>().FirstOrDefault();
			if (anchorEntity == null) return;

			var anchorUi = anchorEntity.GetComponent<UIElement>();
			if (anchorUi == null)
			{
				anchorUi = new UIElement { Bounds = bannerRect, IsInteractable = false };
				EntityManager.AddComponent(anchorEntity, anchorUi);
			}
			else
			{
				anchorUi.Bounds = bannerRect;
				anchorUi.IsInteractable = false;
			}
		}

		private void DrawAttackDecorations(Rectangle rect, float panelScale, float panelSquashX, float panelSquashY)
		{
			if (rect.Width <= 0 || rect.Height <= 0) return;

			float scaleXFactor = Math.Max(0f, panelScale * panelSquashX);
			float scaleYFactor = Math.Max(0f, panelScale * panelSquashY);

			float cornerScale = Math.Max(0.01f, CornerOrnamentScale);
			if (_enemyAttackCornerBlTexture != null)
			{
				var originBl = new Vector2(0f, _enemyAttackCornerBlTexture.Height);
				var posBl = new Vector2(rect.Left + CornerLeftOffsetX, rect.Bottom + CornerLeftOffsetY);
				var scaleBl = new Vector2(cornerScale * scaleXFactor, cornerScale * scaleYFactor);
				_spriteBatch.Draw(_enemyAttackCornerBlTexture, posBl, null, Color.White, 0f, originBl, scaleBl, SpriteEffects.None, 0f);
			}

			if (_enemyAttackCornerBrTexture != null)
			{
				var originBr = new Vector2(_enemyAttackCornerBrTexture.Width, _enemyAttackCornerBrTexture.Height);
				var posBr = new Vector2(rect.Right + CornerRightOffsetX, rect.Bottom + CornerRightOffsetY);
				var scaleBr = new Vector2(cornerScale * scaleXFactor, cornerScale * scaleYFactor);
				_spriteBatch.Draw(_enemyAttackCornerBrTexture, posBr, null, Color.White, 0f, originBr, scaleBr, SpriteEffects.None, 0f);
			}

			float topScale = Math.Max(0.01f, TopOrnamentScale);
			if (_enemyAttackTopTexture != null)
			{
				var originTop = new Vector2(_enemyAttackTopTexture.Width / 2f, _enemyAttackTopTexture.Height);
				var posTop = new Vector2(rect.Left + rect.Width / 2f, rect.Top + TopOrnamentOffsetY);
				var topScaleVec = new Vector2(topScale * scaleXFactor, topScale * scaleYFactor);
				_spriteBatch.Draw(_enemyAttackTopTexture, posTop, null, Color.White, 0f, originTop, topScaleVec, SpriteEffects.None, 0f);
			}

			float skullScale = Math.Max(0.01f, SkullScale);
			if (_enemyAttackSkullTexture != null)
			{
				var originSkull = new Vector2(_enemyAttackSkullTexture.Width / 2f, _enemyAttackSkullTexture.Height);
				var skullPos = new Vector2(rect.Left + rect.Width / 2f, rect.Top + SkullVerticalOffset);
				var skullScaleVec = new Vector2(skullScale * scaleXFactor, skullScale * scaleYFactor);
				_spriteBatch.Draw(_enemyAttackSkullTexture, skullPos, null, Color.White, 0f, originSkull, skullScaleVec, SpriteEffects.None, 0f);
			}
		}

		private void DrawRect(Rectangle rect, Color color, int thickness)
		{
			_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
		}
	}
}
