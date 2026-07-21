using System;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Input;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Singletons;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.Diagnostics.Snapshots.Fixtures
{
	public sealed class HotKeySnapshotFixture : IDisplaySnapshotFixture
	{
		private const int Radius = 28;
		private const float TextScale = 0.18f;
		private string _variant = "keyboard";
		private HotKeyGlyphRenderer _renderer;
		private HotKeySystem _hotKeys;
		private Texture2D _pixel;

		public string Id => "hotkey-hints";
		public int WarmupFrames => 1;
		public string OutputFileName => _variant;

		public void Setup(DisplaySnapshotContext ctx, string[] args)
		{
			_variant = ParseVariant(args ?? Array.Empty<string>());
			_renderer = new HotKeyGlyphRenderer(ctx.GraphicsDevice, ctx.SpriteBatch, FontSingleton.ChakraPetchFont);
			_hotKeys = ctx.World.GetSystem<HotKeySystem>()
				?? throw new DisplaySnapshotSetupException("Hotkey system was not registered.");
			_pixel = new Texture2D(ctx.GraphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });
		}

		public void Draw(DisplaySnapshotContext ctx)
		{
			ctx.SpriteBatch.Draw(_pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), new Color(18, 20, 26));
			DrawTitle(ctx.SpriteBatch);
			if (_variant == "keyboard") DrawKeyboardGallery();
			else DrawGamepadGallery(_variant == "playstation" ? GamepadGlyphStyle.PlayStation : GamepadGlyphStyle.Xbox);
			DrawPositionSamples(ctx.SpriteBatch);
		}

		private void DrawTitle(SpriteBatch spriteBatch)
		{
			string title = _variant switch
			{
				"keyboard" => "Keyboard hotkey hints",
				"xbox" => "Xbox hotkey hints",
				_ => "PlayStation hotkey hints",
			};
			spriteBatch.DrawString(FontSingleton.TitleFont, title, new Vector2(120, 90), Color.White, 0f, Vector2.Zero, 0.35f, SpriteEffects.None, 0f);
		}

		private void DrawKeyboardGallery()
		{
			PlayerButton[] keys = { PlayerButton.Escape, PlayerButton.Enter, PlayerButton.Space };
			for (int i = 0; i < keys.Length; i++)
				_renderer.DrawKeyboard(keys[i], 520 + i * 440, 350, Radius, TextScale);
		}

		private void DrawGamepadGallery(GamepadGlyphStyle style)
		{
			FaceButton[] buttons =
			{
				FaceButton.B, FaceButton.X, FaceButton.Y, FaceButton.View,
				FaceButton.Start, FaceButton.LB, FaceButton.RB,
			};
			for (int i = 0; i < buttons.Length; i++)
			{
				int x = 300 + (i % 4) * 440;
				int y = 320 + (i / 4) * 150;
				_renderer.DrawGamepad(buttons[i], style, x, y, Radius, TextScale);
			}
		}

		private void DrawPositionSamples(SpriteBatch spriteBatch)
		{
			Rectangle button = new(810, 720, 300, 90);
			spriteBatch.Draw(_pixel, button, new Color(54, 58, 70));
			DrawBorder(spriteBatch, button, new Color(150, 158, 180));
			const string label = "Sample action";
			Vector2 size = FontSingleton.ChakraPetchFont.MeasureString(label) * 0.16f;
			spriteBatch.DrawString(FontSingleton.ChakraPetchFont, label, new Vector2(button.Center.X - size.X / 2f, button.Center.Y - size.Y / 2f), Color.White, 0f, Vector2.Zero, 0.16f, SpriteEffects.None, 0f);

			foreach (HotKeyPosition position in Enum.GetValues<HotKeyPosition>())
			{
				Point hintSize;
				if (_variant == "keyboard")
				{
					hintSize = _renderer.MeasureKeyboard(PlayerButton.Enter, Radius, TextScale);
					var (cx, cy) = _hotKeys.CalculateHintPosition(button, position, hintSize, 18, 18);
					_renderer.DrawKeyboard(PlayerButton.Enter, cx, cy, Radius, TextScale);
				}
				else
				{
					GamepadGlyphStyle style = _variant == "playstation" ? GamepadGlyphStyle.PlayStation : GamepadGlyphStyle.Xbox;
					hintSize = _renderer.MeasureGamepad(FaceButton.Start, Radius);
					var (cx, cy) = _hotKeys.CalculateHintPosition(button, position, hintSize, 18, 18);
					_renderer.DrawGamepad(FaceButton.Start, style, cx, cy, Radius, TextScale);
				}
			}
		}

		private void DrawBorder(SpriteBatch spriteBatch, Rectangle bounds, Color color)
		{
			spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), color);
			spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2), color);
			spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, 2, bounds.Height), color);
			spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - 2, bounds.Y, 2, bounds.Height), color);
		}

		private static string ParseVariant(string[] args)
		{
			if (args.Length == 1 && args[0] is "keyboard" or "xbox" or "playstation") return args[0];
			throw new DisplaySnapshotSetupException("hotkey-hints expects one variant: keyboard, xbox, or playstation");
		}
	}
}
