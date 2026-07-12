using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Rendering;
using System.Collections.Generic;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Utils;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// Draws simple black-background, white-text tooltips for hovered UI elements that have a Tooltip component.
	/// </summary>
	[DebugTab("Tooltips")]
	public class TooltipTextDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly SpriteFont _font;
		private readonly Dictionary<(int Width, int Height, int Radius), Texture2D> _roundedCache = new();
		private CardGeometrySettings _cardTooltipSettings;

		[DebugEditable(DisplayName = "Padding", Step = 1, Min = 0, Max = 40)]
		public int Padding { get; set; } = 8;

		[DebugEditable(DisplayName = "Corner Radius", Step = 1, Min = 0, Max = 64)]
		public int CornerRadius { get; set; } = 8;

		[DebugEditable(DisplayName = "Fade Seconds", Step = 0.05f, Min = 0.05f, Max = 1.5f)]
		public float FadeSeconds { get; set; } = 0.1f;

		[DebugEditable(DisplayName = "Max Alpha", Step = 5, Min = 0, Max = 255)]
		public int MaxAlpha { get; set; } = 220;

		[DebugEditable(DisplayName = "Text Scale", Step = 0.1f, Min = 0.5f, Max = 2.0f)]
		public float TextScale { get; set; } = 0.125f;

		[DebugEditable(DisplayName = "Text Color R", Step = 1, Min = 0, Max = 255)]
		public int TextColorR { get; set; } = 255;

		[DebugEditable(DisplayName = "Text Color G", Step = 1, Min = 0, Max = 255)]
		public int TextColorG { get; set; } = 255;

		[DebugEditable(DisplayName = "Text Color B", Step = 1, Min = 0, Max = 255)]
		public int TextColorB { get; set; } = 255;

		[DebugEditable(DisplayName = "Max Width", Step = 10, Min = 50, Max = 1000)]
		public int MaxWidth { get; set; } = 400;

		[DebugEditable(DisplayName = "Stack Gap", Step = 1, Min = 0, Max = 40)]
		public int StackGap { get; set; } = 6;

		[DebugEditable(DisplayName = "Card Text Gap", Step = 1, Min = 0, Max = 120)]
		public int CardTooltipTextGap { get; set; } = 12;

		private sealed class TooltipRenderBlock
		{
			public Rectangle Rect;
			public string Text;
		}

		private sealed class FadeState
		{
			public float Alpha01;
			public bool TargetVisible;
			public Rectangle Rect;
			public List<TooltipRenderBlock> Blocks = new();
		}

		private readonly Dictionary<int, FadeState> _fadeByEntityId = new();

		public TooltipTextDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_font = FontSingleton.ChakraPetchFont;
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<UIElement>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public void Draw()
		{
			if (_font == null) return;
			if (StateSingleton.IsTutorialActive) return;

			// Determine top-most hovered UI with tooltip
			var hoverables = GetRelevantEntities()
				.Select(e => new { E = e, UI = e.GetComponent<UIElement>(), T = e.GetComponent<Transform>() })
				.Where(x => x.UI?.TooltipType == TooltipType.Text
					&& x.UI.IsHovered
					&& !x.UI.IsHidden
					&& (
						!string.IsNullOrWhiteSpace(x.UI.Tooltip) || 
						!string.IsNullOrWhiteSpace(x.UI.TooltipKeywordSource) ||
						x.E.GetComponent<Frozen>() != null ||
						x.E.GetComponent<Brittle>() != null ||
						x.E.GetComponent<Scorched>() != null ||
						x.E.GetComponent<Thorned>() != null ||
						x.E.GetComponent<Colorless>() != null ||
						x.E.GetComponent<Intimidated>() != null || 
						x.E.GetComponent<Shackle>() != null ||
						x.E.GetComponent<Pledge>() != null ||
						x.E.GetComponent<PledgePreview>() != null ||
						x.E.GetComponent<Sealed>() != null ||
						x.E.GetComponent<Recoil>() != null
					))
				.OrderByDescending(x => x.T?.ZOrder ?? 0)
				.ToList();
			var top = hoverables.FirstOrDefault();

			// Set all states to fade out by default
			foreach (var key in _fadeByEntityId.Keys.ToList())
			{
				_fadeByEntityId[key].TargetVisible = false;
			}

			if (top != null)
			{
				var blocks = TooltipTextService.BuildTooltipBlocks(
					top.E,
					top.UI.Tooltip,
					EntityManager,
					top.UI.TooltipKeywordSource);
				if (blocks.Count > 0)
				{
					Rectangle anchorBounds = TransformResolverService.ResolveUIBounds(
						EntityManager,
						top.E,
						top.UI);

					var measured = MeasureBlocks(blocks);

					// Position based on UI.TooltipPosition
					int rx = anchorBounds.X;
					int ry = anchorBounds.Y;
					int gap = System.Math.Max(0, top.UI.TooltipOffsetPx);
					switch (top.UI.TooltipPosition)
					{
						case TooltipPosition.Above:
							rx = anchorBounds.X + (anchorBounds.Width - measured.Size.X) / 2;
							ry = anchorBounds.Y - measured.Size.Y - gap;
							break;
						case TooltipPosition.Below:
							rx = anchorBounds.X + (anchorBounds.Width - measured.Size.X) / 2;
							ry = anchorBounds.Bottom + gap;
							break;
						case TooltipPosition.Right:
							rx = anchorBounds.Right + gap;
							ry = anchorBounds.Y + (anchorBounds.Height - measured.Size.Y) / 2;
							break;
						case TooltipPosition.Left:
							rx = anchorBounds.X - measured.Size.X - gap;
							ry = anchorBounds.Y + (anchorBounds.Height - measured.Size.Y) / 2;
							break;
					}
					var rect = new Rectangle(rx, ry, measured.Size.X, measured.Size.Y);
					// Screen clamp
					rect.X = System.Math.Max(0, System.Math.Min(rect.X, Game1.VirtualWidth - rect.Width));
					rect.Y = System.Math.Max(0, System.Math.Min(rect.Y, Game1.VirtualHeight - rect.Height));
					var renderBlocks = BuildRenderBlocks(measured.Blocks, rect.Location);

					SetVisibleFadeState(top.E.Id, rect, renderBlocks);
				}
			}

			UpdateCardTooltipTextFadeState();

			// Update and draw all fade states
			foreach (var kv in _fadeByEntityId.ToList())
			{
				var id = kv.Key;
				var fs = kv.Value;
				float step = (FadeSeconds <= 0f) ? 1f : (1f / (FadeSeconds * 60f));
				fs.Alpha01 = MathHelper.Clamp(fs.Alpha01 + (fs.TargetVisible ? step : -step), 0f, 1f);
				_fadeByEntityId[id] = fs;
				if (fs.Alpha01 <= 0f && !fs.TargetVisible)
				{
					_fadeByEntityId.Remove(id);
					continue;
				}

				int alpha = (int)System.Math.Round(MaxAlpha * fs.Alpha01);
				var backColor = new Color(0, 0, 0, System.Math.Clamp(alpha, 0, 255));
				int pad = System.Math.Max(0, Padding);
				var textColor = new Color(TextColorR, TextColorG, TextColorB, 255) * fs.Alpha01;
				foreach (var block in fs.Blocks)
				{
					var texture = GetRoundedTexture(block.Rect);
					_spriteBatch.Draw(texture, block.Rect, null, backColor, 0f, Vector2.Zero, SpriteEffects.None, 0.999f);
					var textPos = new Vector2(block.Rect.X + pad, block.Rect.Y + pad);
					_spriteBatch.DrawString(_font, block.Text, textPos, textColor, 0f, Vector2.Zero, TextScale, SpriteEffects.None, 1.0f);
				}
			}
		}

		private void UpdateCardTooltipTextFadeState()
		{
			if (_cardTooltipSettings == null)
			{
				_cardTooltipSettings = CardGeometryService.GetSettings(EntityManager);
				if (_cardTooltipSettings == null) return;
			}

			if (!CardTooltipLayoutService.TryGetTopHoveredLayout(
				EntityManager,
				_cardTooltipSettings,
				Game1.VirtualWidth,
				Game1.VirtualHeight,
				gapOverride: 0,
				screenPadding: 8,
				out var layout))
			{
				return;
			}

			var blocks = TooltipTextService.BuildTooltipBlocks(
				layout.Entity,
				string.Empty,
				EntityManager,
				layout.UI.TooltipKeywordSource);
			if (blocks.Count == 0) return;

			int gap = System.Math.Max(0, CardTooltipTextGap);
			bool placeLeft = layout.TooltipRect.X < layout.AnchorBounds.X;
			int availableWidth = placeLeft
				? layout.TooltipRect.X - gap
				: Game1.VirtualWidth - layout.TooltipRect.Right - gap;
			var measured = MeasureBlocks(blocks, System.Math.Max(50, System.Math.Min(MaxWidth, availableWidth)));

			int rx = placeLeft
				? layout.TooltipRect.X - measured.Size.X - gap
				: layout.TooltipRect.Right + gap;
			int ry = layout.TooltipRect.Y;
			var rect = new Rectangle(rx, ry, measured.Size.X, measured.Size.Y);
			rect.X = System.Math.Max(0, System.Math.Min(rect.X, Game1.VirtualWidth - rect.Width));
			rect.Y = System.Math.Max(0, System.Math.Min(rect.Y, Game1.VirtualHeight - rect.Height));
			var renderBlocks = BuildRenderBlocks(measured.Blocks, rect.Location);
			SetVisibleFadeState(layout.Entity.Id, rect, renderBlocks);
		}

		private void SetVisibleFadeState(int entityId, Rectangle rect, List<TooltipRenderBlock> renderBlocks)
		{
			if (!_fadeByEntityId.TryGetValue(entityId, out var fs))
			{
				fs = new FadeState { Alpha01 = 0f, TargetVisible = true, Rect = rect, Blocks = renderBlocks };
				_fadeByEntityId[entityId] = fs;
			}
			fs.TargetVisible = true;
			fs.Rect = rect;
			fs.Blocks = renderBlocks;
			_fadeByEntityId[entityId] = fs;
		}

		private (List<TooltipRenderBlock> Blocks, Point Size) MeasureBlocks(
			IReadOnlyList<TooltipTextService.TooltipTextBlock> sourceBlocks,
			int maxWidth = -1)
		{
			int pad = System.Math.Max(0, Padding);
			int stackGap = System.Math.Max(0, StackGap);
			int wrapWidth = maxWidth > 0 ? maxWidth : MaxWidth;
			var measured = new List<TooltipRenderBlock>();
			int stackWidth = 0;
			int stackHeight = 0;

			foreach (var block in sourceBlocks)
			{
				string text = string.Join("\n", TextUtils.WrapText(_font, block.Text, TextScale, wrapWidth));
				var size = _font.MeasureString(text) * TextScale;
				int width = (int)System.Math.Ceiling(size.X) + pad * 2;
				int height = (int)System.Math.Ceiling(size.Y) + pad * 2;
				measured.Add(new TooltipRenderBlock
				{
					Rect = new Rectangle(0, 0, width, height),
					Text = text,
				});
				stackWidth = System.Math.Max(stackWidth, width);
				stackHeight += height;
			}

			if (measured.Count > 1)
				stackHeight += stackGap * (measured.Count - 1);

			return (measured, new Point(stackWidth, stackHeight));
		}

		private List<TooltipRenderBlock> BuildRenderBlocks(List<TooltipRenderBlock> measuredBlocks, Point stackLocation)
		{
			int y = stackLocation.Y;
			int stackGap = System.Math.Max(0, StackGap);
			int width = measuredBlocks.Count == 0 ? 0 : measuredBlocks.Max(block => block.Rect.Width);
			var renderBlocks = new List<TooltipRenderBlock>(measuredBlocks.Count);

			foreach (var block in measuredBlocks)
			{
				var rect = new Rectangle(stackLocation.X, y, width, block.Rect.Height);
				renderBlocks.Add(new TooltipRenderBlock { Rect = rect, Text = block.Text });
				y += block.Rect.Height + stackGap;
			}

			return renderBlocks;
		}

		private Texture2D GetRoundedTexture(Rectangle rect)
		{
			int r = System.Math.Max(0, System.Math.Min(CornerRadius, System.Math.Min(rect.Width, rect.Height) / 2));
			var key = (rect.Width, rect.Height, r);
			if (!_roundedCache.TryGetValue(key, out var texture))
			{
				texture = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, rect.Width, rect.Height, r);
				_roundedCache[key] = texture;
			}

			return texture;
		}

	}
}
