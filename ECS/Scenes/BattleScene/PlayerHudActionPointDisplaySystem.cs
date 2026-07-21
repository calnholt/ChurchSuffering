using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
	[DebugTab("Player HUD Action Points")]
	public class PlayerHudActionPointDisplaySystem : Core.System
	{
		private const string Label = "AP";
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;

		public PlayerHudActionPointDisplaySystem(
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
				.Where(entity => entity.GetComponent<PlayerHudRegion>()?.Type == PlayerHudRegionType.ActionPoint);
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var ui = entity.GetComponent<UIElement>();
			if (ui == null) return;

			int amount = EntityManager.GetEntitiesWithComponent<Player>()
				.FirstOrDefault()
				?.GetComponent<ActionPoints>()
				?.Current ?? 0;
			string noun = amount == 1 ? "Action Point" : "Action Points";
			ui.Tooltip = $"{amount} {noun}\n\nSpend Action Points to play cards during the Action phase.";
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
			DrawGlow(drawState);

			float skewPercent = drawState.Bounds.Width <= 0
				? 0f
				: drawState.Slant * 100f / drawState.Bounds.Width;
			var chipMask = PrimitiveTextureFactory.GetParallelogramMask(
				_graphicsDevice,
				drawState.Bounds.Width,
				drawState.Bounds.Height,
				skewPercent);
			_spriteBatch.Draw(chipMask, drawState.Bounds, drawState.BackgroundColor);

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
				PlayerHudRegionType.ActionPoint,
				out var anchor,
				out var region,
				out var regionBounds,
				out var feedback,
				out var player))
			{
				return null;
			}

			var actionPoints = player.GetComponent<ActionPoints>();
			if (actionPoints == null) return null;

			float pulseScale = Math.Max(0.01f, feedback?.Scale ?? 1f);
			return new PlayerHudResourceRenderState(
				PlayerHudResourceDisplayHelper.ScaleAroundCenter(regionBounds, pulseScale),
				pulseScale,
				Math.Max(0, (int)Math.Round(anchor.Slant * pulseScale)),
				Label,
				actionPoints.Current.ToString(),
				anchor.HudWhite,
				anchor.HudBlack,
				anchor.HudBlack,
				anchor.ActionPointPaddingLeft,
				anchor.ActionPointPaddingRight,
				anchor.ContentGap,
				anchor.LabelLetterSpacing,
				anchor.LabelFontScale,
				anchor.ValueFontScale,
				Math.Max(0, (int)Math.Round(anchor.ActionPointGlowRadius * pulseScale)),
				Color.FromNonPremultiplied(
					anchor.HudRed.R,
					anchor.HudRed.G,
					anchor.HudRed.B,
					anchor.ActionPointGlowAlpha));
		}

		private void DrawGlow(PlayerHudResourceRenderState state)
		{
			if (state.EffectSize <= 0) return;

			const int layers = 5;
			for (int layer = layers; layer >= 1; layer--)
			{
				float fraction = layer / (float)layers;
				int spread = Math.Max(1, (int)Math.Round(state.EffectSize * fraction));
				var bounds = new Rectangle(
					state.Bounds.X - spread,
					state.Bounds.Y - spread,
					state.Bounds.Width + spread * 2,
					state.Bounds.Height + spread * 2);
				float skewPercent = bounds.Width <= 0
					? 0f
					: (state.Slant + spread) * 100f / bounds.Width;
				var glowMask = PrimitiveTextureFactory.GetParallelogramMask(
					_graphicsDevice,
					bounds.Width,
					bounds.Height,
					skewPercent);
				var color = state.EffectColor * ((1f - fraction * 0.55f) / layers);
				_spriteBatch.Draw(glowMask, bounds, color);
			}
		}
	}
}
