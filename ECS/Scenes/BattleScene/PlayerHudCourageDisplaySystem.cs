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
	[DebugTab("Player HUD Courage")]
	public class PlayerHudCourageDisplaySystem : Core.System
	{
		private const string Label = "COUR";
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;

		public PlayerHudCourageDisplaySystem(
			EntityManager entityManager,
			GraphicsDevice graphicsDevice,
			SpriteBatch spriteBatch) : base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<PlayerHudRegion>()
				.Where(entity => entity.GetComponent<PlayerHudRegion>()?.Type == PlayerHudRegionType.Courage);
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var ui = entity.GetComponent<UIElement>();
			if (ui == null) return;

			int amount = EntityManager.GetEntitiesWithComponent<Player>()
				.FirstOrDefault()
				?.GetComponent<Courage>()
				?.Amount ?? 0;
			ui.Tooltip = $"{amount} Courage\n\nBlocking with red cards increases your courage by 1.";
			ui.TooltipType = TooltipType.Text;
			ui.TooltipPosition = TooltipPosition.Above;
			ui.ShowHoverHighlight = false;
		}

		public void Draw()
		{
			var state = GetRenderState();
			var labelFont = FontSingleton.ChakraPetchFont;
			var valueFont = FontSingleton.TitleFont;
			if (state == null || _graphicsDevice == null || _spriteBatch == null
				|| labelFont == null || valueFont == null)
			{
				return;
			}

			var drawState = state.Value;
			float skewPercent = drawState.Bounds.Width <= 0
				? 0f
				: drawState.Slant * 100f / drawState.Bounds.Width;
			var chipMask = PrimitiveTextureFactory.GetParallelogramMask(
				_graphicsDevice,
				drawState.Bounds.Width,
				drawState.Bounds.Height,
				skewPercent);

			_spriteBatch.Draw(chipMask, drawState.Bounds, drawState.BackgroundColor);

			if (drawState.EffectSize > 0)
			{
				int shadowHeight = Math.Min(drawState.EffectSize, drawState.Bounds.Height);
				var source = new Rectangle(
					0,
					Math.Max(0, chipMask.Height - shadowHeight),
					chipMask.Width,
					shadowHeight);
				var destination = new Rectangle(
					drawState.Bounds.X,
					drawState.Bounds.Bottom - shadowHeight,
					drawState.Bounds.Width,
					shadowHeight);
				_spriteBatch.Draw(chipMask, destination, source, drawState.EffectColor);
			}

			PlayerHudResourceDisplayHelper.DrawSplitText(
				_spriteBatch,
				labelFont,
				valueFont,
				drawState);
		}

		internal PlayerHudResourceRenderState? GetRenderState()
		{
			if (!PlayerHudResourceDisplayHelper.TryGetContext(
				EntityManager,
				PlayerHudRegionType.Courage,
				out var anchor,
				out var region,
				out var regionBounds,
				out var feedback,
				out var player))
			{
				return null;
			}

			var courage = player.GetComponent<Courage>();
			if (courage == null) return null;

			float pulseScale = Math.Max(0.01f, feedback?.Scale ?? 1f);
			return new PlayerHudResourceRenderState(
				PlayerHudResourceDisplayHelper.ScaleAroundCenter(regionBounds, pulseScale),
				pulseScale,
				Math.Max(0, (int)Math.Round(anchor.Slant * pulseScale)),
				Label,
				courage.Amount.ToString(),
				anchor.HudRed,
				anchor.HudWhite,
				anchor.HudWhite,
				anchor.CouragePaddingLeft,
				anchor.CouragePaddingRight,
				anchor.ContentGap,
				anchor.LabelLetterSpacing,
				anchor.LabelFontScale,
				anchor.ValueFontScale,
				Math.Max(0, (int)Math.Round(anchor.CourageInsetShadowHeight * pulseScale)),
				Color.FromNonPremultiplied(0, 0, 0, anchor.CourageInsetShadowAlpha));
		}
	}

	internal readonly record struct PlayerHudResourceRenderState(
		Rectangle Bounds,
		float PulseScale,
		int Slant,
		string Label,
		string Value,
		Color BackgroundColor,
		Color LabelColor,
		Color ValueColor,
		int PaddingLeft,
		int PaddingRight,
		int ContentGap,
		int LabelLetterSpacing,
		float LabelFontScale,
		float ValueFontScale,
		int EffectSize,
		Color EffectColor);

	internal static class PlayerHudResourceDisplayHelper
	{
		public static bool TryGetContext(
			EntityManager entityManager,
			PlayerHudRegionType type,
			out PlayerHudAnchor anchor,
			out PlayerHudRegion region,
			out Rectangle worldBounds,
			out PlayerHudFeedbackState feedback,
			out Entity player)
		{
			anchor = entityManager.GetEntitiesWithComponent<PlayerHudAnchor>()
				.FirstOrDefault()
				?.GetComponent<PlayerHudAnchor>();
			var regionEntity = entityManager.GetEntitiesWithComponent<PlayerHudRegion>()
				.FirstOrDefault(entity => entity.GetComponent<PlayerHudRegion>()?.Type == type);
			region = regionEntity?.GetComponent<PlayerHudRegion>();
			worldBounds = regionEntity == null || region == null
				? Rectangle.Empty
				: TransformResolverService.ResolveLocalBounds(
					entityManager,
					regionEntity,
					region.Bounds);
			feedback = regionEntity?.GetComponent<PlayerHudFeedbackState>();
			player = entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();

			return anchor != null
				&& region != null
				&& region.IsVisible
				&& worldBounds.Width > 0
				&& worldBounds.Height > 0
				&& player != null;
		}

		public static Rectangle ScaleAroundCenter(Rectangle bounds, float scale)
		{
			int width = Math.Max(1, (int)Math.Round(bounds.Width * scale));
			int height = Math.Max(1, (int)Math.Round(bounds.Height * scale));
			return new Rectangle(
				bounds.Center.X - width / 2,
				bounds.Center.Y - height / 2,
				width,
				height);
		}

		public static void DrawSplitText(
			SpriteBatch spriteBatch,
			SpriteFont labelFont,
			SpriteFont valueFont,
			PlayerHudResourceRenderState state)
		{
			float labelScale = Math.Max(0.01f, state.LabelFontScale * state.PulseScale);
			float valueScale = Math.Max(0.01f, state.ValueFontScale * state.PulseScale);
			float letterSpacing = Math.Max(0f, state.LabelLetterSpacing * state.PulseScale);
			float gap = Math.Max(0f, state.ContentGap * state.PulseScale);
			Vector2 labelSize = MeasureSpacedString(labelFont, state.Label, labelScale, letterSpacing);
			Vector2 valueSize = valueFont.MeasureString(state.Value) * valueScale;
			float contentWidth = labelSize.X + gap + valueSize.X;
			float left = state.Bounds.X + state.PaddingLeft * state.PulseScale;
			float right = state.Bounds.Right - state.PaddingRight * state.PulseScale;
			float contentCenterX = (left + right) / 2f;
			float centerY = state.Bounds.Center.Y;
			var labelPosition = new Vector2(
				contentCenterX - contentWidth / 2f,
				centerY - labelSize.Y / 2f);

			DrawSpacedString(
				spriteBatch,
				labelFont,
				state.Label,
				labelPosition,
				state.LabelColor,
				labelScale,
				letterSpacing);

			var valuePosition = new Vector2(
				labelPosition.X + labelSize.X + gap,
				centerY - valueSize.Y / 2f);
			spriteBatch.DrawString(
				valueFont,
				state.Value,
				valuePosition,
				state.ValueColor,
				0f,
				Vector2.Zero,
				valueScale,
				SpriteEffects.None,
				0f);
		}

		private static Vector2 MeasureSpacedString(
			SpriteFont font,
			string text,
			float scale,
			float letterSpacing)
		{
			if (string.IsNullOrEmpty(text)) return Vector2.Zero;

			float width = 0f;
			float height = 0f;
			for (int index = 0; index < text.Length; index++)
			{
				Vector2 characterSize = font.MeasureString(text[index].ToString()) * scale;
				width += characterSize.X;
				height = Math.Max(height, characterSize.Y);
			}

			width += Math.Max(0, text.Length - 1) * letterSpacing;
			return new Vector2(width, height);
		}

		private static void DrawSpacedString(
			SpriteBatch spriteBatch,
			SpriteFont font,
			string text,
			Vector2 position,
			Color color,
			float scale,
			float letterSpacing)
		{
			float x = position.X;
			foreach (char character in text)
			{
				string glyph = character.ToString();
				spriteBatch.DrawString(
					font,
					glyph,
					new Vector2(x, position.Y),
					color,
					0f,
					Vector2.Zero,
					scale,
					SpriteEffects.None,
					0f);
				x += font.MeasureString(glyph).X * scale + letterSpacing;
			}
		}
	}
}
