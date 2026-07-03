using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Currency Display")]
	public class CurrencyDisplaySystem
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly SpriteFont _font = FontSingleton.ContentFont;
		private Texture2D _goldTexture;
		private int _currentGold;
		private readonly System.Action<GoldChanged> _onGoldChangedHandler;

		// Layout controls
		[DebugEditable(DisplayName = "Margin X", Step = 1f, Min = 0f, Max = 200f)]
		public float MarginX { get; set; } = 20f;

		[DebugEditable(DisplayName = "Margin Y", Step = 1f, Min = 0f, Max = 200f)]
		public float MarginY { get; set; } = 24f;

		[DebugEditable(DisplayName = "Padding X", Step = 1f, Min = 0f, Max = 200f)]
		public float PaddingX { get; set; } = 16f;

		[DebugEditable(DisplayName = "Padding Y", Step = 1f, Min = 0f, Max = 200f)]
		public float PaddingY { get; set; } = 10f;

		[DebugEditable(DisplayName = "Text Scale", Step = 0.05f, Min = 0.2f, Max = 2f)]
		public float TextScale { get; set; } = 0.275f;

		[DebugEditable(DisplayName = "Coin Size", Step = 1f, Min = 8f, Max = 256f)]
		public float CoinSize { get; set; } = 40f;

		[DebugEditable(DisplayName = "Gap Text -> Icon", Step = 1f, Min = 0f, Max = 100f)]
		public float GapTextToIcon { get; set; } = 5f;

		// Trapezoid parameters (background)
		[DebugEditable(DisplayName = "Trapezoid Width", Step = 10f, Min = 80f, Max = 1200f)]
		public float TrapezoidWidth { get; set; } = 110f;

		[DebugEditable(DisplayName = "Trapezoid Height", Step = 5f, Min = 24f, Max = 300f)]
		public float TrapezoidHeight { get; set; } = 51f;

		[DebugEditable(DisplayName = "Left Side Offset", Step = 1f, Min = 0f, Max = 100f)]
		public float LeftSideOffset { get; set; } = 0f;

		[DebugEditable(DisplayName = "Top Edge Angle (deg)", Step = 1f, Min = -45f, Max = 45f)]
		public float TopEdgeAngleDegrees { get; set; } = 3f;

		[DebugEditable(DisplayName = "Right Edge Angle (deg)", Step = 1f, Min = -45f, Max = 45f)]
		public float RightEdgeAngleDegrees { get; set; } = -22f;

		[DebugEditable(DisplayName = "Bottom Edge Angle (deg)", Step = 1f, Min = -45f, Max = 45f)]
		public float BottomEdgeAngleDegrees { get; set; } = 3f;

		[DebugEditable(DisplayName = "Left Edge Angle (deg)", Step = 1f, Min = -45f, Max = 45f)]
		public float LeftEdgeAngleDegrees { get; set; } = 8f;

		public CurrencyDisplaySystem(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ImageAssetService imageAssets)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_goldTexture = imageAssets.TryGetTexture("gold");

			// Seed current gold
			try { _currentGold = SaveCache.GetGold(); } catch { _currentGold = 0; }

			// Subscribe to gold change events
			_onGoldChangedHandler = OnGoldChanged;
			EventManager.Subscribe(_onGoldChangedHandler);
			EventManager.Subscribe<LoadSceneEvent>(OnLoadScene);
		}

		private void OnLoadScene(LoadSceneEvent evt)
		{
			if (evt.Scene == SceneId.Location)
			{
				_currentGold = SaveCache.GetGold();
			}
		}

		private void OnGoldChanged(GoldChanged evt)
		{
			_currentGold = evt?.NewGold ?? _currentGold;
		}

		public void Dispose()
		{
			try { EventManager.Unsubscribe(_onGoldChangedHandler); } catch { }
		}

		public void Draw()
		{
			if (_font == null) return;

			int viewportW = Game1.VirtualWidth;
			int viewportH = Game1.VirtualHeight;

			int gold = _currentGold;
			string text = gold.ToString();
			Vector2 baseTextSize = _font.MeasureString(text);
			Vector2 textSize = baseTextSize * TextScale;

			float coinW = CoinSize;
			float coinH = CoinSize;
			if (_goldTexture != null && _goldTexture.Width > 0)
			{
				float aspect = _goldTexture.Height / (float)_goldTexture.Width;
				coinH = coinW * aspect;
			}

			// Ensure the trapezoid is large enough to contain content + padding
			float minWidth = PaddingX + textSize.X + (coinW > 0 ? (GapTextToIcon + coinW) : 0f) + PaddingX;
			float bgWidth = System.MathF.Max(TrapezoidWidth, minWidth);
			float bgHeight = TrapezoidHeight;

			// Bottom-left anchor
			float bgX = MarginX;
			float bgY = viewportH - MarginY - bgHeight;

			// Trapezoid background (black)
			var trapezoid = PrimitiveTextureFactory.GetAntialiasedTrapezoid(
				_graphicsDevice,
				bgWidth,
				bgHeight,
				LeftSideOffset,
				TopEdgeAngleDegrees,
				RightEdgeAngleDegrees,
				BottomEdgeAngleDegrees,
				LeftEdgeAngleDegrees
			);
			if (trapezoid != null)
			{
				var dest = new Rectangle((int)System.Math.Round(bgX), (int)System.Math.Round(bgY), (int)System.Math.Round(bgWidth), (int)System.Math.Round(bgHeight));
				_spriteBatch.Draw(trapezoid, dest, Color.White);
			}

			// Text position (left-inside, vertically centered)
			Vector2 textPos = new Vector2(
				bgX + PaddingX,
				bgY + (bgHeight - textSize.Y) / 2f
			);
			_spriteBatch.DrawString(_font, text, textPos, Color.White, 0f, Vector2.Zero, TextScale, SpriteEffects.None, 0f);

			// Coin icon to the right of text
			if (_goldTexture != null)
			{
				float coinX = textPos.X + textSize.X + GapTextToIcon;
				float coinY = bgY + (bgHeight - coinH) / 2f;
				var coinRect = new Rectangle(
					(int)System.Math.Round(coinX),
					(int)System.Math.Round(coinY),
					(int)System.Math.Round(coinW),
					(int)System.Math.Round(coinH)
				);
				_spriteBatch.Draw(_goldTexture, coinRect, Color.White);
			}
		}
	}
}

