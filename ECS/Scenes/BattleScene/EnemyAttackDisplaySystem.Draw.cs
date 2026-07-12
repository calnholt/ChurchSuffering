using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	public partial class EnemyAttackDisplaySystem
	{
		private readonly List<(string Text, float Scale, Color Color, bool IsTitle)> _wrappedLines = new();
		private string _layoutCacheKey = string.Empty;
		private EnemyAttackBase _displayDefinition;

		public void Draw()
		{
			var presentation = GetPresentation();
			if (presentation == null || !presentation.IsVisible || presentation.RenderBounds == Rectangle.Empty) return;

			DrawAbsorbAfterimages(presentation);
			DrawImpactRings(presentation);
			DrawPanelBackground(presentation);
			DrawImpactFlash(presentation);
			DrawFrameDecorations(presentation);
			DrawImpactParticles(presentation);
			DrawSkull(presentation);
			DrawTextContent(presentation);
			DrawAbsorbEmbers(presentation);
			DrawConfirmButton(presentation);
		}

		private EnemyAttackBannerPresentation GetPresentation()
		{
			return EntityManager.GetEntitiesWithComponent<EnemyAttackBannerPresentation>()
				.FirstOrDefault()?.GetComponent<EnemyAttackBannerPresentation>();
		}

		private void UpdatePresentationState(Entity enemy, AttackIntent intent, SubPhase phaseNow)
		{
			var anchor = EnsureBannerAnchor();
			var presentation = anchor.GetComponent<EnemyAttackBannerPresentation>();
			var anchorTransform = anchor.GetComponent<Transform>();
			var planned = intent.Planned.FirstOrDefault();
			var definition = planned?.AttackDefinition;
			bool suppressed = BattleInputGate.ShouldSuppressEnemyAttackDisplay(EntityManager);
			var ambush = EntityManager.GetEntitiesWithComponent<AmbushState>().FirstOrDefault()?.GetComponent<AmbushState>();
			bool visible = !suppressed
				&& _showBanner
				&& !_absorbCompleteFired
				&& definition != null
				&& (phaseNow == SubPhase.Block || phaseNow == SubPhase.EnemyAttack)
				&& !(ambush?.IsActive == true && ambush.IntroActive);

			presentation.IsVisible = visible;
			if (!visible)
			{
				SyncAttackTextTooltip(null, Rectangle.Empty);
				return;
			}

			_displayDefinition = definition;
			BuildCachedLayout(definition);
			presentation.LogicalWidth = Math.Max(1, _bannerRect.Width);
			presentation.LogicalHeight = Math.Max(1, _bannerRect.Height);
			presentation.ImpactIntensity = _impactIntensity;

			var sample = _impactActive
				? _entranceSample
				: EnemyAttackAnimationService.ComputeEntrance(EnemyAttackAnimationService.PresentationCompleteSeconds, _impactIntensity);
			float absorbProgress = phaseNow == SubPhase.EnemyAttack
				? MathHelper.Clamp(_absorbElapsedSeconds / Math.Max(0.05f, AbsorbDurationSeconds), 0f, 1f)
				: 0f;
			float absorbEase = 1f - MathF.Pow(1f - absorbProgress, 3f);
			float absorbScale = 1f - absorbEase;
			var baseCenter = new Vector2(Game1.VirtualWidth / 2f + OffsetX, Game1.VirtualHeight / 2f + OffsetY);
			var enemyTransform = enemy.GetComponent<Transform>();
			var absorbTarget = (enemyTransform?.Position ?? baseCenter) + new Vector2(0f, AbsorbTargetYOffset);
			var currentCenter = Vector2.Lerp(baseCenter, absorbTarget, absorbEase);

			presentation.PanelScaleX = sample.PanelScaleX * absorbScale;
			presentation.PanelScaleY = sample.PanelScaleY * absorbScale;
			presentation.ContentScale = phaseNow == SubPhase.EnemyAttack ? absorbScale : 1f;
			presentation.Alpha = 1f - absorbProgress * absorbProgress;
			presentation.OrnamentProgress = sample.OrnamentProgress;
			presentation.SkullScale = sample.SkullScale;
			presentation.SkullTint = sample.SkullTint;
			presentation.TextAlpha = sample.TextAlpha;
			presentation.TextOffsetY = sample.TextOffsetY;
			presentation.FlashAlpha = sample.FlashAlpha;
			presentation.RingOneProgress = sample.RingOneProgress;
			presentation.RingTwoProgress = sample.RingTwoProgress;
			presentation.AbsorbProgress = absorbProgress;
			presentation.AbsorbStart = baseCenter;
			presentation.AbsorbTarget = absorbTarget;
			presentation.RecoilOffset = _impactActive
				? EnemyAttackAnimationService.ComputeDeterministicRecoil(
					_impactElapsedSeconds,
					ShakeDurationSeconds,
					ShakeAmplitudePx * _impactIntensity)
				: Vector2.Zero;
			presentation.ShowConfirm = EntityManager.GetEntity("UIButton_ConfirmEnemyAttack")?.GetComponent<UIElement>()?.IsInteractable == true;
			presentation.ConfirmWidth = ConfirmButtonWidth;
			presentation.ConfirmHeight = ConfirmButtonHeight;
			presentation.ConfirmOffsetY = ConfirmButtonOffsetY;
			presentation.HasKeywordTooltip = TooltipTextService.GetKeywordTooltipBlocks(definition.Text).Count > 0;

			anchorTransform.Position = currentCenter;
			anchorTransform.Scale = Vector2.One;
			anchorTransform.Rotation = 0f;
			SyncAttackTextTooltip(presentation.HasKeywordTooltip ? definition.Text : null, presentation.LocalTextBounds);
			EnsureConfirmTexture();
		}

		private Entity EnsureBannerAnchor()
		{
			var anchor = EntityManager.GetEntitiesWithComponent<EnemyAttackBannerAnchor>().FirstOrDefault();
			if (anchor != null)
			{
				if (anchor.GetComponent<EnemyAttackBannerPresentation>() == null)
					EntityManager.AddComponent(anchor, new EnemyAttackBannerPresentation());
				return anchor;
			}

			anchor = EntityManager.CreateEntity("EnemyAttackBannerAnchor");
			EntityManager.AddComponent(anchor, new EnemyAttackBannerAnchor());
			EntityManager.AddComponent(anchor, new EnemyAttackBannerPresentation());
			EntityManager.AddComponent(anchor, new Transform());
			var parallax = ParallaxLayer.GetUIParallaxLayer();
			parallax.MultiplierX = 0.045f;
			parallax.MultiplierY = 0.045f;
			EntityManager.AddComponent(anchor, parallax);
			EntityManager.AddComponent(anchor, new UIElement { Bounds = new Rectangle(0, 0, 1, 1), IsInteractable = false });
			return anchor;
		}

		private void BuildCachedLayout(EnemyAttackBase definition)
		{
			int padding = Math.Max(0, PanelPadding);
			int maxWidth = (int)MathF.Round(Game1.VirtualWidth * Math.Clamp(PanelMaxWidthPercent, 0.1f, 1f));
			int minWidth = (int)MathF.Round(Game1.VirtualWidth * Math.Clamp(PanelMinWidthPercent, 0f, 1f));
			int contentLimit = Math.Max(50, maxWidth - padding * 2);
			var progress = FindEnemyAttackProgress();
			bool conditionMet = progress == null || (GuidedTutorialService.IsActive(EntityManager)
				? BattleInputGate.IsTutorialActionAllowed(EntityManager, TutorialAction.ConfirmBlocks)
				: progress.IsConditionMet);
			string cacheKey = string.Join('|', definition.Name, definition.Text, TitleScale, TextScale, padding, maxWidth, minWidth, LineSpacingExtra, TitleSpacingExtra, conditionMet);
			if (_layoutCacheKey == cacheKey) return;

			_layoutCacheKey = cacheKey;
			_wrappedLines.Clear();
			foreach (string part in TextUtils.WrapText(_contentFont, definition.Name, TitleScale, contentLimit))
				_wrappedLines.Add((part, TitleScale, Color.White, true));
			foreach (string part in TextUtils.WrapText(_bodyFont, definition.Text, TextScale, contentLimit))
				_wrappedLines.Add((part, TextScale, GetConditionTextColor(conditionMet), false));

			float maxTextWidth = 0f;
			float totalHeight = 0f;
			bool firstTitle = true;
			float bodyTop = 0f;
			float bodyBottom = 0f;
			foreach (var line in _wrappedLines)
			{
				var font = line.IsTitle ? _contentFont : _bodyFont;
				var measured = font.MeasureString(line.Text) * line.Scale;
				maxTextWidth = Math.Max(maxTextWidth, measured.X);
				if (!line.IsTitle && bodyTop == 0f) bodyTop = totalHeight;
				totalHeight += measured.Y + (firstTitle && line.IsTitle ? TitleSpacingExtra : LineSpacingExtra);
				if (!line.IsTitle) bodyBottom = totalHeight;
				if (line.IsTitle) firstTitle = false;
			}

			int width = Math.Max(minWidth, (int)MathF.Ceiling(Math.Min(maxTextWidth + padding * 2, maxWidth)));
			int height = (int)MathF.Ceiling(totalHeight) + padding * 2;
			_bannerRect = new Rectangle(0, 0, width, height);
			var presentation = GetPresentation();
			if (presentation != null)
			{
				int textWidth = Math.Max(1, width - padding * 2);
				presentation.LocalTextBounds = bodyBottom <= bodyTop
					? Rectangle.Empty
					: new Rectangle(padding, padding + (int)bodyTop, textWidth, Math.Max(1, (int)MathF.Ceiling(bodyBottom - bodyTop)));
			}
		}

		private void SyncAttackTextTooltip(string keywordSource, Rectangle localBounds)
		{
			if (string.IsNullOrEmpty(keywordSource) || localBounds == Rectangle.Empty)
			{
				if (_attackTextTooltipEntity != null)
				{
					EntityManager.DestroyEntity(_attackTextTooltipEntity.Id);
					_attackTextTooltipEntity = null;
				}
				return;
			}

			if (_attackTextTooltipEntity == null)
			{
				_attackTextTooltipEntity = EntityManager.CreateEntity("UI_EnemyAttackTextTooltip");
				EntityManager.AddComponent(_attackTextTooltipEntity, new Transform { ZOrder = 10001 });
				EntityManager.AddComponent(_attackTextTooltipEntity, new UIElement { IsInteractable = false });
			}

			var ui = _attackTextTooltipEntity.GetComponent<UIElement>();
			ui.Tooltip = string.Empty;
			ui.TooltipKeywordSource = keywordSource;
			ui.TooltipType = TooltipType.Text;
			ui.TooltipPosition = TooltipPosition.Below;
			ui.IsInteractable = false;
		}

		private void DrawAbsorbAfterimages(EnemyAttackBannerPresentation p)
		{
			if (p.AbsorbProgress <= 0f || AbsorbAfterimageCount <= 0) return;
			for (int i = AbsorbAfterimageCount; i >= 1; i--)
			{
				float lag = i * 0.04f / Math.Max(0.05f, AbsorbDurationSeconds);
				float t = MathHelper.Clamp(p.AbsorbProgress - lag, 0f, 1f);
				float ease = 1f - MathF.Pow(1f - t, 3f);
				var center = Vector2.Lerp(p.AbsorbStart, p.AbsorbTarget, ease);
				float scale = 1f - ease;
				var rect = ScaleRectAroundTopCenter(p.LogicalWidth, p.LogicalHeight, center, scale, scale);
				DrawRect(rect, new Color(155, 18, 30) * (0.055f * (AbsorbAfterimageCount - i + 1)), 2);
			}
		}

		private void DrawImpactRings(EnemyAttackBannerPresentation p)
		{
			DrawRing(p.RenderBounds, p.RingOneProgress, new Color(145, 15, 28), 4, 56f * p.ImpactIntensity);
			DrawRing(p.RenderBounds, p.RingTwoProgress, new Color(28, 22, 24), 3, 82f * p.ImpactIntensity);
		}

		private void DrawRing(Rectangle bounds, float progress, Color color, int thickness, float expansion)
		{
			if (progress <= 0f || progress >= 1f) return;
			float eased = 1f - MathF.Pow(1f - progress, 3f);
			int amount = (int)MathF.Round(expansion * eased);
			var expanded = new Rectangle(bounds.X - amount, bounds.Y - amount, bounds.Width + amount * 2, bounds.Height + amount * 2);
			DrawRect(expanded, color * ((1f - progress) * 0.8f), thickness);
		}

		private void DrawPanelBackground(EnemyAttackBannerPresentation p)
		{
			var rect = p.RenderBounds;
			int aura = 42 + (int)(30f * p.ImpactIntensity);
			_spriteBatch.Draw(_panelAuraTexture,
				new Rectangle(rect.X - aura, rect.Y - aura, rect.Width + aura * 2, rect.Height + aura * 2),
				new Color(135, 8, 24) * (PanelAuraAlpha * p.Alpha));
			const int bands = 8;
			for (int i = 0; i < bands; i++)
			{
				int y0 = rect.Y + rect.Height * i / bands;
				int y1 = rect.Y + rect.Height * (i + 1) / bands;
				float shade = i / (float)(bands - 1);
				var color = Color.Lerp(new Color(31, 25, 27), new Color(10, 9, 11), shade) * (BackgroundAlpha / 255f * p.Alpha);
				_spriteBatch.Draw(_pixel, new Rectangle(rect.X, y0, rect.Width, Math.Max(1, y1 - y0)), color);
			}
			DrawOutlineEcho(rect, p.Alpha);
			DrawRect(rect, new Color(143, 22, 31) * (0.72f * p.Alpha), Math.Max(1, BorderThickness));
			DrawRect(new Rectangle(rect.X + 3, rect.Y + 3, Math.Max(1, rect.Width - 6), Math.Max(1, rect.Height - 6)), new Color(215, 50, 56) * (0.18f * p.Alpha), 1);
		}

		private void DrawOutlineEcho(Rectangle bounds, float panelAlpha)
		{
			var sample = EnemyAttackAnimationService.ComputeOutlineEcho(
				_outlineEchoElapsedSeconds,
				OutlineEchoIntervalSeconds,
				OutlineEchoDurationSeconds);
			if (!sample.IsActive || OutlineEchoExpansionPx <= 0 || OutlineEchoAlpha <= 0f) return;

			int expansion = (int)MathF.Round(OutlineEchoExpansionPx * sample.ExpansionProgress);
			var echoBounds = new Rectangle(
				bounds.X - expansion,
				bounds.Y - expansion,
				bounds.Width + expansion * 2,
				bounds.Height + expansion * 2);
			float alpha = MathHelper.Clamp(OutlineEchoAlpha * sample.Alpha * panelAlpha, 0f, 1f);
			DrawRect(echoBounds, new Color(143, 22, 31) * alpha, Math.Max(1, BorderThickness));
		}

		private void DrawImpactFlash(EnemyAttackBannerPresentation p)
		{
			if (p.FlashAlpha <= 0f || FlashMaxAlpha <= 0) return;
			_spriteBatch.Draw(_pixel, p.RenderBounds, Color.White * (p.FlashAlpha * FlashMaxAlpha / 255f));
		}

		private void DrawFrameDecorations(EnemyAttackBannerPresentation p)
		{
			var rect = p.RenderBounds;
			float progress = p.OrnamentProgress;
			float alpha = MathHelper.Clamp(progress * p.Alpha, 0f, 1f);
			float slide = OrnamentSlidePx * (1f - progress);
			if (_enemyAttackCornerBlTexture != null)
			{
				var position = new Vector2(rect.Left + CornerLeftOffsetX + slide, rect.Bottom + CornerLeftOffsetY);
				_spriteBatch.Draw(_enemyAttackCornerBlTexture, position, null, Color.White * alpha, 0f,
					new Vector2(0f, _enemyAttackCornerBlTexture.Height), CornerOrnamentScale, SpriteEffects.None, 0f);
			}
			if (_enemyAttackCornerBrTexture != null)
			{
				var position = new Vector2(rect.Right + CornerRightOffsetX - slide, rect.Bottom + CornerRightOffsetY);
				_spriteBatch.Draw(_enemyAttackCornerBrTexture, position, null, Color.White * alpha, 0f,
					new Vector2(_enemyAttackCornerBrTexture.Width, _enemyAttackCornerBrTexture.Height), CornerOrnamentScale, SpriteEffects.None, 0f);
			}
			if (_enemyAttackTopTexture != null)
			{
				var position = new Vector2(rect.Center.X, rect.Top + TopOrnamentOffsetY - slide * 0.4f);
				_spriteBatch.Draw(_enemyAttackTopTexture, position, null, Color.White * alpha, 0f,
					new Vector2(_enemyAttackTopTexture.Width * 0.5f, _enemyAttackTopTexture.Height), TopOrnamentScale, SpriteEffects.None, 0f);
			}
		}

		private void DrawSkull(EnemyAttackBannerPresentation p)
		{
			if (_enemyAttackSkullTexture == null) return;
			bool confirmAvailable = p.ShowConfirm;
			float idlePulse = !_impactActive && confirmAvailable ? EnemyAttackAnimationService.ComputeIdlePulse(_idleElapsedSeconds) : 1f;
			float scale = SkullScale * p.SkullScale * idlePulse * Math.Max(0.01f, p.ContentScale);
			var impactTint = Color.Lerp(Color.White, new Color(255, 73, 65), p.SkullTint * 0.55f);
			_spriteBatch.Draw(_enemyAttackSkullTexture,
				new Vector2(p.RenderBounds.Center.X, p.RenderBounds.Top + SkullVerticalOffset * p.ContentScale),
				null, impactTint * p.Alpha, 0f,
				new Vector2(_enemyAttackSkullTexture.Width * 0.5f, _enemyAttackSkullTexture.Height),
				scale, SpriteEffects.None, 0f);
		}

		private void DrawImpactParticles(EnemyAttackBannerPresentation p)
		{
			var center = new Vector2(p.RenderBounds.Center.X, p.RenderBounds.Center.Y);
			foreach (var particle in _particles)
			{
				float t = MathHelper.Clamp(particle.Age / Math.Max(0.0001f, particle.Lifetime), 0f, 1f);
				float alpha = MathF.Pow(1f - t, 0.75f) * p.Alpha;
				var size = particle.Kind == BannerParticleKind.FrameShard
					? new Vector2(particle.Size * 2.4f, particle.Size * 0.65f)
					: new Vector2(particle.Size);
				_spriteBatch.Draw(_pixel, center + particle.Position, null, particle.Color * alpha,
					particle.Rotation, new Vector2(0.5f), size, SpriteEffects.None, 0f);
			}
		}

		private void DrawTextContent(EnemyAttackBannerPresentation p)
		{
			float contentScale = Math.Max(0.01f, p.ContentScale);
			float y = p.RenderBounds.Y + PanelPadding * contentScale + p.TextOffsetY * contentScale;
			bool firstTitle = true;
			foreach (var line in _wrappedLines)
			{
				var font = line.IsTitle ? _contentFont : _bodyFont;
				float scale = line.Scale * contentScale;
				var measured = font.MeasureString(line.Text) * scale;
				float x = line.IsTitle
					? p.RenderBounds.Center.X - measured.X * 0.5f
					: p.RenderBounds.X + PanelPadding * contentScale;
				var titleTint = Color.Lerp(new Color(255, 104, 91), line.Color, 1f - p.SkullTint * 0.45f);
				var color = (line.IsTitle ? titleTint : line.Color) * (p.TextAlpha * p.Alpha);
				_spriteBatch.DrawString(font, line.Text, new Vector2(x, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
				y += measured.Y + (firstTitle && line.IsTitle ? TitleSpacingExtra : LineSpacingExtra) * contentScale;
				if (line.IsTitle) firstTitle = false;
			}
		}

		private void DrawAbsorbEmbers(EnemyAttackBannerPresentation p)
		{
			if (p.AbsorbProgress <= 0f) return;
			float elapsed = _absorbElapsedSeconds;
			foreach (var ember in _absorbEmbers)
			{
				float t = MathHelper.Clamp((elapsed - ember.Delay) / Math.Max(0.01f, ember.Lifetime), 0f, 1f);
				if (t <= 0f || t >= 1f) continue;
				float eased = 1f - MathF.Pow(1f - t, 3f);
				var position = Vector2.Lerp(ember.Start, ember.Target, eased) + ember.Arc * MathF.Sin(t * MathF.PI) * (1f - t);
				_spriteBatch.Draw(_pixel, position, null, new Color(236, 42, 31) * ((1f - t) * 0.8f), 0f, new Vector2(0.5f), 3f, SpriteEffects.None, 0f);
			}
		}

		private void DrawConfirmButton(EnemyAttackBannerPresentation p)
		{
			var button = EntityManager.GetEntity("UIButton_ConfirmEnemyAttack");
			var ui = button?.GetComponent<UIElement>();
			if (ui?.IsInteractable != true || p.ConfirmBounds == Rectangle.Empty || _cachedConfirmTexture == null) return;
			bool hovered = ui.IsHovered;
			var rect = hovered ? ScaleRectAroundCenter(p.ConfirmBounds, ConfirmHoverScale) : p.ConfirmBounds;
			if (hovered)
			{
				var glow = new Rectangle(rect.X - 8, rect.Y - 6, rect.Width + 16, rect.Height + 12);
				_spriteBatch.Draw(_pixel, glow, new Color(190, 22, 35) * 0.35f);
			}
			_spriteBatch.Draw(_cachedConfirmTexture, rect, hovered ? Color.White : Color.White * 0.92f);
		}

		internal static Color GetConditionTextColor(bool conditionMet) =>
			conditionMet ? Color.White : new Color(255, 150, 150, 255);

		private static Rectangle ScaleRectAroundCenter(Rectangle rect, float scale)
		{
			int width = Math.Max(1, (int)MathF.Round(rect.Width * scale));
			int height = Math.Max(1, (int)MathF.Round(rect.Height * scale));
			return new Rectangle(rect.Center.X - width / 2, rect.Center.Y - height / 2, width, height);
		}

		private static Rectangle ScaleRectAroundTopCenter(int width, int height, Vector2 centerTop, float scaleX, float scaleY)
		{
			int drawWidth = Math.Max(1, (int)MathF.Round(width * scaleX));
			int drawHeight = Math.Max(1, (int)MathF.Round(height * scaleY));
			return new Rectangle((int)MathF.Round(centerTop.X - drawWidth * 0.5f), (int)MathF.Round(centerTop.Y), drawWidth, drawHeight);
		}

		private void DrawRect(Rectangle rect, Color color, int thickness)
		{
			if (rect.Width <= 0 || rect.Height <= 0 || thickness <= 0 || color.A == 0) return;
			_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
		}
	}
}
