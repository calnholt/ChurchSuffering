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
		private readonly Texture2D _pixel;
		private Texture2D _rounded;
		private int _cachedW, _cachedH, _cachedR;

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


		private class FadeState { public float Alpha01; public bool TargetVisible; public Rectangle Rect; public string Text; }
		private readonly Dictionary<int, FadeState> _fadeByEntityId = new();

		public TooltipTextDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_font = FontSingleton.ChakraPetchFont;
			_pixel = new Texture2D(graphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });
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
						x.E.GetComponent<Frozen>() != null ||
						x.E.GetComponent<Brittle>() != null ||
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
				string text = TooltipTextService.BuildCardTooltip(top.E, top.UI.Tooltip, EntityManager);
				Rectangle anchorBounds = TransformResolverService.ResolveUIBounds(
					EntityManager,
					top.E,
					top.UI);

				// Wrap text based on MaxWidth
				var wrappedLines = TextUtils.WrapText(_font, text, TextScale, MaxWidth);
				text = string.Join("\n", wrappedLines);

				int pad = System.Math.Max(0, Padding);
				var size = _font.MeasureString(text) * TextScale;
				int w = (int)System.Math.Ceiling(size.X) + pad * 2;
				int h = (int)System.Math.Ceiling(size.Y) + pad * 2;

				// Position based on UI.TooltipPosition
				int rx = anchorBounds.X;
				int ry = anchorBounds.Y;
				int gap = System.Math.Max(0, top.UI.TooltipOffsetPx);
				switch (top.UI.TooltipPosition)
				{
					case TooltipPosition.Above:
						rx = anchorBounds.X + (anchorBounds.Width - w) / 2;
						ry = anchorBounds.Y - h - gap;
						break;
					case TooltipPosition.Below:
						rx = anchorBounds.X + (anchorBounds.Width - w) / 2;
						ry = anchorBounds.Bottom + gap;
						break;
					case TooltipPosition.Right:
						rx = anchorBounds.Right + gap;
						ry = anchorBounds.Y + (anchorBounds.Height - h) / 2;
						break;
					case TooltipPosition.Left:
						rx = anchorBounds.X - w - gap;
						ry = anchorBounds.Y + (anchorBounds.Height - h) / 2;
						break;
				}
				var rect = new Rectangle(rx, ry, w, h);
				// Screen clamp
				rect.X = System.Math.Max(0, System.Math.Min(rect.X, Game1.VirtualWidth - rect.Width));
				rect.Y = System.Math.Max(0, System.Math.Min(rect.Y, Game1.VirtualHeight - rect.Height));

				var id = top.E.Id;
				if (!_fadeByEntityId.TryGetValue(id, out var fs))
				{
					fs = new FadeState { Alpha01 = 0f, TargetVisible = true, Rect = rect, Text = text };
					_fadeByEntityId[id] = fs;
				}
				fs.TargetVisible = true;
				fs.Rect = rect;
				fs.Text = text;
				_fadeByEntityId[id] = fs;
			}

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

				int w = fs.Rect.Width;
				int h = fs.Rect.Height;
				int r = System.Math.Max(0, System.Math.Min(CornerRadius, System.Math.Min(w, h) / 2));
				bool rebuild = _rounded == null || _cachedW != w || _cachedH != h || _cachedR != r;
				if (rebuild)
				{
					_rounded?.Dispose();
					_rounded = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, w, h, r);
					_cachedW = w; _cachedH = h; _cachedR = r;
				}

				int alpha = (int)System.Math.Round(MaxAlpha * fs.Alpha01);
				var backColor = new Color(0, 0, 0, System.Math.Clamp(alpha, 0, 255));
				_spriteBatch.Draw(_rounded, fs.Rect, null, backColor, 0f, Vector2.Zero, SpriteEffects.None, 0.999f);
				// DrawBorder(fs.Rect, Color.White, 2);
				int pad = System.Math.Max(0, Padding);
				var textPos = new Vector2(fs.Rect.X + pad, fs.Rect.Y + pad);
				var textColor = new Color(TextColorR, TextColorG, TextColorB, 255) * fs.Alpha01;
				_spriteBatch.DrawString(_font, fs.Text, textPos, textColor, 0f, Vector2.Zero, TextScale, SpriteEffects.None, 1.0f);
			}
		}

	}
}

