using System.Collections.Generic;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
	[DebugTab("Test Fight HP Display")]
	public class TestFightHpDisplaySystem : Core.System
	{
		private readonly SpriteBatch _spriteBatch;
		private readonly SpriteFont _font = FontSingleton.TitleFont;

		[DebugEditable(DisplayName = "Offset X", Step = 2, Min = -2000, Max = 2000)]
		public int OffsetX { get; set; } = -16;

		[DebugEditable(DisplayName = "Offset Y", Step = 2, Min = -2000, Max = 2000)]
		public int OffsetY { get; set; } = 54;

		[DebugEditable(DisplayName = "Font Scale", Step = 0.01f, Min = 0.05f, Max = 1f)]
		public float FontScale { get; set; } = 0.16f;

		[DebugEditable(DisplayName = "Text Color R", Step = 1, Min = 0, Max = 255)]
		public int TextColorR { get; set; } = 255;

		[DebugEditable(DisplayName = "Text Color G", Step = 1, Min = 0, Max = 255)]
		public int TextColorG { get; set; } = 220;

		[DebugEditable(DisplayName = "Text Color B", Step = 1, Min = 0, Max = 255)]
		public int TextColorB { get; set; } = 120;

		public TestFightHpDisplaySystem(
			EntityManager entityManager,
			GraphicsDevice graphicsDevice,
			SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_spriteBatch = spriteBatch;
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return System.Array.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public void Draw()
		{
			if (!TestFightRuntime.IsActive || _font == null || TestFightRuntime.CurrentMaxHp <= 0) return;

			string text = BuildText(TestFightRuntime.CurrentMaxHp, TestFightRuntime.HpDelta);
			var size = _font.MeasureString(text) * FontScale;
			var position = new Vector2(
				Game1.VirtualWidth + OffsetX - size.X,
				OffsetY);
			var color = new Color(
				System.Math.Clamp(TextColorR, 0, 255),
				System.Math.Clamp(TextColorG, 0, 255),
				System.Math.Clamp(TextColorB, 0, 255));

			_spriteBatch.DrawString(
				_font,
				text,
				position + new Vector2(1, 1),
				Color.Black * 0.6f,
				0f,
				Vector2.Zero,
				FontScale,
				SpriteEffects.None,
				0f);
			_spriteBatch.DrawString(
				_font,
				text,
				position,
				color,
				0f,
				Vector2.Zero,
				FontScale,
				SpriteEffects.None,
				0f);
		}

		internal static string BuildText(int maxHp, int delta)
		{
			string formattedDelta = delta >= 0 ? $"+{delta}" : delta.ToString();
			return $"Max HP: {maxHp}, Delta: {formattedDelta}";
		}
	}
}
