using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChurchSuffering.Diagnostics;

namespace ChurchSuffering.ECS.Scenes.BattleScene
{
	[DebugTab("Passive Meter")]
	public class PassiveMeterRenderSystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private Texture2D _whitePixel;

		[DebugEditable(DisplayName = "Meter Width", Step = 1, Min = 1, Max = 400)]
		public int MeterWidth { get; set; } = 100;

		[DebugEditable(DisplayName = "Meter Height", Step = 1, Min = 1, Max = 50)]
		public int MeterHeight { get; set; } = 4;

		[DebugEditable(DisplayName = "Offset X", Step = 1, Min = -500, Max = 500)]
		public int OffsetX { get; set; } = 0;

		[DebugEditable(DisplayName = "Offset Y", Step = 1, Min = -500, Max = 500)]
		public int OffsetY { get; set; } = 4;

		[DebugEditable(DisplayName = "Background Alpha", Step = 1, Min = 0, Max = 255)]
		public int BgA { get; set; } = 160;

		[DebugEditable(DisplayName = "Fill R", Step = 1, Min = 0, Max = 255)]
		public int FillR { get; set; } = 220;
		[DebugEditable(DisplayName = "Fill G", Step = 1, Min = 0, Max = 255)]
		public int FillG { get; set; } = 40;
		[DebugEditable(DisplayName = "Fill B", Step = 1, Min = 0, Max = 255)]
		public int FillB { get; set; } = 40;
		[DebugEditable(DisplayName = "Fill A", Step = 1, Min = 0, Max = 255)]
		public int FillA { get; set; } = 220;

		[DebugEditable(DisplayName = "Z-Order", Step = 1, Min = 0, Max = 20000)]
		public int ZOrder { get; set; } = 10002;

		public PassiveMeterRenderSystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch) : base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<PassiveMeterComponent>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			// No-op; rendering is done in Draw()
		}

		public void Draw()
		{
			EnsureWhitePixel();

			foreach (var entity in GetRelevantEntities())
			{
				var meter = entity.GetComponent<PassiveMeterComponent>();
				if (meter == null || !meter.IsActive) continue;

				var ui = entity.GetComponent<UIElement>();
				if (ui == null) continue;

				// Position meter centered under the anchor chip
				int x = ui.Bounds.Center.X - (MeterWidth / 2) + OffsetX;
				int y = ui.Bounds.Bottom + OffsetY;

				// Background (black with BgA alpha)
				var bgRect = new Rectangle(x, y, MeterWidth, MeterHeight);
				var bgColor = Color.FromNonPremultiplied(0, 0, 0, (byte)MathHelper.Clamp(BgA, 0, 255));
				_spriteBatch.Draw(_whitePixel, bgRect, bgColor);

				// Calculate fill percentage based on direction
				float pct;
				if (meter.Direction == PassiveMeterDirection.Countdown)
				{
					// Countdown: meter depletes as value decreases
					float clampedValue = MathHelper.Clamp(meter.CurrentValue, 0f, MathHelper.Max(0.0001f, meter.MaxValue));
					pct = MathHelper.Clamp(clampedValue / MathHelper.Max(0.0001f, meter.MaxValue), 0f, 1f);
				}
				else
				{
					// FillUp: meter fills as value increases
					float clampedValue = MathHelper.Clamp(meter.CurrentValue, 0f, MathHelper.Max(0.0001f, meter.MaxValue));
					pct = MathHelper.Clamp(clampedValue / MathHelper.Max(0.0001f, meter.MaxValue), 0f, 1f);
				}

				int fillW = (int)System.Math.Round(MeterWidth * pct);
				if (fillW > 0)
				{
					var fillRect = new Rectangle(x, y, fillW, MeterHeight);
					var fillColor = Color.FromNonPremultiplied(
						(int)MathHelper.Clamp(FillR, 0, 255),
						(int)MathHelper.Clamp(FillG, 0, 255),
						(int)MathHelper.Clamp(FillB, 0, 255),
						(byte)MathHelper.Clamp(FillA, 0, 255)
					);
					_spriteBatch.Draw(_whitePixel, fillRect, fillColor);
				}
			}
		}

		private void EnsureWhitePixel()
		{
			if (_whitePixel != null && !_whitePixel.IsDisposed) return;
			_whitePixel = new Texture2D(_graphicsDevice, 1, 1);
			_whitePixel.SetData(new[] { Color.White });
		}
	}
}
