using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
	internal readonly record struct PlayerHudTemperanceRenderState(
		Rectangle Bounds,
		float PulseScale,
		int Slant,
		int Threshold,
		int FilledChunks,
		int ChunkWidth,
		int ChunkHeight,
		int ChunkGap,
		Color BackgroundColor,
		Color FilledColor,
		Color EmptyColor,
		Color LabelColor,
		float LabelFontScale,
		int LabelLetterSpacing,
		int PaddingLeft,
		int PaddingRight,
		int ContentGap);

	internal static class PlayerHudTemperanceRendering
	{
		public static (int Threshold, int FilledChunks) BuildChunkState(int amount, int threshold)
		{
			int visibleThreshold = Math.Max(1, threshold);
			return (visibleThreshold, Math.Clamp(amount, 0, visibleThreshold));
		}
	}

	[DebugTab("Player HUD Temperance")]
	public class PlayerHudTemperanceDisplaySystem : Core.System
	{
		private const string Label = "TEMP";
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;

		public PlayerHudTemperanceDisplaySystem(
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
				.Where(entity => entity.GetComponent<PlayerHudRegion>()?.Type == PlayerHudRegionType.Temperance);
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var ui = entity.GetComponent<UIElement>();
			if (ui == null) return;

			var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
			ui.Tooltip = BuildTooltip(player);
			ui.TooltipType = TooltipType.Text;
			ui.TooltipPosition = TooltipPosition.Above;
			ui.ShowHoverHighlight = false;
		}

		public void Draw()
		{
			var state = GetRenderState();
			var font = FontSingleton.ChakraPetchFont;
			if (state == null || font == null || _graphicsDevice == null || _spriteBatch == null) return;

			var drawState = state.Value;
			DrawParallelogram(drawState.Bounds, drawState.Slant, drawState.BackgroundColor);

			float labelScale = Math.Max(0.01f, drawState.LabelFontScale * drawState.PulseScale);
			float letterSpacing = Math.Max(0f, drawState.LabelLetterSpacing * drawState.PulseScale);
			Vector2 labelSize = MeasureSpacedString(font, Label, labelScale, letterSpacing);
			int chunkWidth = Math.Max(1, (int)Math.Round(drawState.ChunkWidth * drawState.PulseScale));
			int chunkHeight = Math.Max(1, (int)Math.Round(drawState.ChunkHeight * drawState.PulseScale));
			int chunkGap = (int)Math.Round(drawState.ChunkGap * drawState.PulseScale);
			int chunksWidth = drawState.Threshold * chunkWidth
				+ Math.Max(0, drawState.Threshold - 1) * chunkGap;
			float gap = Math.Max(0f, drawState.ContentGap * drawState.PulseScale);
			float contentWidth = labelSize.X + gap + chunksWidth;
			float left = drawState.Bounds.X + drawState.PaddingLeft * drawState.PulseScale;
			float right = drawState.Bounds.Right - drawState.PaddingRight * drawState.PulseScale;
			float x = (left + right - contentWidth) / 2f;
			float centerY = drawState.Bounds.Center.Y;

			DrawSpacedString(
				font,
				Label,
				new Vector2(x, centerY - labelSize.Y / 2f),
				drawState.LabelColor,
				labelScale,
				letterSpacing);

			int chunkX = (int)Math.Round(x + labelSize.X + gap);
			int chunkY = (int)Math.Round(centerY - chunkHeight / 2f);
			int chunkSlant = Math.Min(
				chunkWidth,
				Math.Max(0, (int)Math.Round(drawState.Slant * chunkHeight / (float)Math.Max(1, drawState.Bounds.Height))));
			for (int index = 0; index < drawState.Threshold; index++)
			{
				var bounds = new Rectangle(
					chunkX + index * (chunkWidth + chunkGap),
					chunkY,
					chunkWidth,
					chunkHeight);
				DrawParallelogram(
					bounds,
					chunkSlant,
					index < drawState.FilledChunks ? drawState.FilledColor : drawState.EmptyColor);
			}
		}

		internal PlayerHudTemperanceRenderState? GetRenderState()
		{
			if (!PlayerHudResourceDisplayHelper.TryGetContext(
				EntityManager,
				PlayerHudRegionType.Temperance,
				out var anchor,
				out var region,
				out var regionBounds,
				out var feedback,
				out var player))
			{
				return null;
			}

			var temperance = player.GetComponent<Temperance>();
			if (temperance == null) return null;

			var chunks = PlayerHudTemperanceRendering.BuildChunkState(
				temperance.Amount,
				ResolveThreshold(player));
			float pulseScale = Math.Max(0.01f, feedback?.Scale ?? 1f);
			return new PlayerHudTemperanceRenderState(
				PlayerHudResourceDisplayHelper.ScaleAroundCenter(regionBounds, pulseScale),
				pulseScale,
				Math.Max(0, (int)Math.Round(anchor.Slant * pulseScale)),
				chunks.Threshold,
				chunks.FilledChunks,
				anchor.TemperanceChunkWidth,
				anchor.TemperanceChunkHeight,
				anchor.TemperanceChunkGap,
				anchor.HudBlack,
				anchor.HudWhite,
				Color.FromNonPremultiplied(255, 255, 255, 36),
				anchor.HudWhite,
				anchor.LabelFontScale,
				anchor.LabelLetterSpacing,
				anchor.TemperancePaddingLeft,
				anchor.TemperancePaddingRight,
				anchor.ContentGap);
		}

		internal static int ResolveThreshold(Entity player)
		{
			string abilityId = player?.GetComponent<EquippedTemperanceAbility>()?.AbilityId;
			var ability = string.IsNullOrEmpty(abilityId) ? null : TemperanceFactory.Create(abilityId);
			return Math.Max(1, ability?.Threshold ?? 1);
		}

		internal static string BuildTooltip(Entity player)
		{
			const string blockingRule = "Blocking with white cards gains temperance.";
			string abilityId = player?.GetComponent<EquippedTemperanceAbility>()?.AbilityId;
			var ability = string.IsNullOrEmpty(abilityId) ? null : TemperanceFactory.Create(abilityId);
			string name = ability?.Name ?? string.Empty;
			string abilityText = ability?.Text ?? string.Empty;
			string abilityLine = !string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(abilityText)
				? $"{name} - {abilityText}"
				: !string.IsNullOrEmpty(name) ? name : abilityText;
			return string.IsNullOrEmpty(abilityLine)
				? $"Temperance\n\n\n\n{blockingRule}"
				: $"Temperance\n\n{abilityLine}\n\n{blockingRule}";
		}

		private void DrawParallelogram(Rectangle bounds, int slant, Color color)
		{
			if (bounds.Width <= 0 || bounds.Height <= 0) return;
			float skewPercent = Math.Min(bounds.Width, Math.Max(0, slant)) * 100f / bounds.Width;
			var mask = PrimitiveTextureFactory.GetParallelogramMask(
				_graphicsDevice,
				bounds.Width,
				bounds.Height,
				skewPercent);
			_spriteBatch.Draw(mask, bounds, color);
		}

		private static Vector2 MeasureSpacedString(
			SpriteFont font,
			string text,
			float scale,
			float letterSpacing)
		{
			float width = 0f;
			float height = 0f;
			foreach (char character in text)
			{
				Vector2 size = font.MeasureString(character.ToString()) * scale;
				width += size.X;
				height = Math.Max(height, size.Y);
			}

			width += Math.Max(0, text.Length - 1) * letterSpacing;
			return new Vector2(width, height);
		}

		private void DrawSpacedString(
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
				x += font.MeasureString(glyph).X * scale + letterSpacing;
			}
		}
	}
}
