using System.Linq;
using System.Text.Json.Nodes;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Medal Display")]
	public class MedalDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ImageAssetService _imageAssets;
		private readonly SpriteFont _font = FontSingleton.ContentFont;

		// Layout/debug controls
		[DebugEditable(DisplayName = "Left Margin", Step = 2, Min = 0, Max = 2000)]
		public int LeftMargin { get; set; } = 30;
		[DebugEditable(DisplayName = "Top Margin", Step = 2, Min = 0, Max = 2000)]
		public int TopMargin { get; set; } = 20;
		[DebugEditable(DisplayName = "Icon Size", Step = 1, Min = 8, Max = 512)]
		public int IconSize { get; set; } = 119;
		[DebugEditable(DisplayName = "Icon Soften Strength", Step = 0.01f, Min = 0f, Max = 4f)]
		public float IconSoftenStrength { get; set; } = 0.4f;
		[DebugEditable(DisplayName = "Spacing X", Step = 1, Min = 0, Max = 256)]
		public int SpacingX { get; set; } = 12;
		[DebugEditable(DisplayName = "Background Corner Radius", Step = 1, Min = 0, Max = 64)]
		public int BgCornerRadius { get; set; } = 16;
		[DebugEditable(DisplayName = "Background Padding", Step = 1, Min = 0, Max = 64)]
		public int BgPadding { get; set; } = 0;
		[DebugEditable(DisplayName = "Background Opacity", Step = 0.05f, Min = 0f, Max = 1f)]
		public float BgOpacity { get; set; } = 0f;

		// Counter debug controls
		[DebugEditable(DisplayName = "Counter Text Scale", Step = 0.05f, Min = 0.1f, Max = 2.0f)]
		public float CounterTextScale { get; set; } = 0.15f;
		[DebugEditable(DisplayName = "Counter X Offset", Step = 1f, Min = -100f, Max = 100f)]
		public float CounterXOffset { get; set; } = -3f;
		[DebugEditable(DisplayName = "Counter Y Offset", Step = 1f, Min = -100f, Max = 100f)]
		public float CounterYOffset { get; set; } = 62f;
		[DebugEditable(DisplayName = "Counter Trap Width", Step = 1f, Min = 10f, Max = 200f)]
		public float CounterTrapezoidWidth { get; set; } = 39f;
		[DebugEditable(DisplayName = "Counter Trap Height", Step = 1f, Min = 10f, Max = 100f)]
		public float CounterTrapezoidHeight { get; set; } = 21f;
		[DebugEditable(DisplayName = "Counter Trap Left Offset", Step = 1f, Min = -50f, Max = 50f)]
		public float CounterTrapezoidLeftOffset { get; set; } = -1f;
		[DebugEditable(DisplayName = "Counter Trap Top Angle", Step = 1f, Min = -45f, Max = 45f)]
		public float CounterTrapezoidTopAngle { get; set; } = 2f;
		[DebugEditable(DisplayName = "Counter Trap Right Angle", Step = 1f, Min = -45f, Max = 45f)]
		public float CounterTrapezoidRightAngle { get; set; } = -29f;
		[DebugEditable(DisplayName = "Counter Trap Bottom Angle", Step = 1f, Min = -45f, Max = 45f)]
		public float CounterTrapezoidBottomAngle { get; set; } = -11f;
		[DebugEditable(DisplayName = "Counter Trap Left Angle", Step = 1f, Min = -45f, Max = 45f)]
		public float CounterTrapezoidLeftAngle { get; set; } = 12f;
		[DebugEditable(DisplayName = "Counter Trap X Offset", Step = 1f, Min = -100f, Max = 100f)]
		public float CounterTrapXOffset { get; set; } = -15f;
		[DebugEditable(DisplayName = "Counter Trap Y Offset", Step = 1f, Min = -100f, Max = 100f)]
		public float CounterTrapYOffset { get; set; } = -7f;

		// Pulse/jiggle animation tuning
		[DebugEditable(DisplayName = "Pulse Duration (s)", Step = 0.05f, Min = 0.1f, Max = 2f)]
		public float PulseDurationSeconds { get; set; } = 0.5f;
		[DebugEditable(DisplayName = "Pulse Scale Amp", Step = 0.01f, Min = 0f, Max = 0.6f)]
		public float PulseScaleAmplitude { get; set; } = 0.44f;
		[DebugEditable(DisplayName = "Jiggle Degrees", Step = 0.5f, Min = 0f, Max = 45f)]
		public float JiggleDegrees { get; set; } = 5f;
		[DebugEditable(DisplayName = "Pulse Frequency (Hz)", Step = 0.1f, Min = 0.5f, Max = 8f)]
		public float PulseFrequencyHz { get; set; } = 1.7f;

		public MedalDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ImageAssetService imageAssets)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_imageAssets = imageAssets;
			EventManager.Subscribe<MedalTriggered>(OnMedalTriggered);
		}

        private void OnMedalTriggered(MedalTriggered evt)
        {
            LoggingService.Append("MedalDisplaySystem.OnMedalTriggered", new JsonObject {
                { "MedalId", evt.MedalId }
            });
            if (evt?.MedalEntity == null) return;
            EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.MedalActivated, Volume = 0.5f });
            var cfg = new JigglePulseConfig
            {
                PulseDurationSeconds = PulseDurationSeconds,
                PulseScaleAmplitude = PulseScaleAmplitude,
                JiggleDegrees = JiggleDegrees,
                PulseFrequencyHz = PulseFrequencyHz
            };
            EventManager.Publish(new JigglePulseEvent { Target = evt.MedalEntity, Config = cfg });
        }

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<Player>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            UpdateMedalPositions();
        }

        private void UpdateMedalPositions()
        {
            var player = GetRelevantEntities().FirstOrDefault();
            if (player == null) return;
            var medals = EntityManager.GetEntitiesWithComponent<EquippedMedal>()
                .Where(e => e.GetComponent<EquippedMedal>().EquippedOwner == player)
                .Select(e => e.GetComponent<EquippedMedal>())
                .ToList();
            if (medals.Count == 0) return;

            int x = LeftMargin;
            int y = TopMargin;
            int bgW = IconSize + BgPadding * 2;
            foreach (var m in medals)
            {
                var t = m.Owner.GetComponent<Transform>();
                if (t != null) t.Position = new Vector2(x, y);
                x += bgW + SpacingX;
            }
        }

		public void Draw()
		{
			var player = GetRelevantEntities().FirstOrDefault();
			if (player == null) return;
			var medals = EntityManager.GetEntitiesWithComponent<EquippedMedal>()
				.Where(e => e.GetComponent<EquippedMedal>().EquippedOwner == player)
				.Select(e => e.GetComponent<EquippedMedal>())
				.ToList();
			if (medals.Count == 0) return;

			foreach (var m in medals)
			{
				int bgW = IconSize + BgPadding * 2;
				int bgH = IconSize + BgPadding * 2;
				var t = m.Owner.GetComponent<Transform>();
				var cur = t != null ? t.Position : Vector2.Zero;
				var rect = new Rectangle((int)System.Math.Round(cur.X), (int)System.Math.Round(cur.Y), bgW, bgH);
				// Backgrounds removed: draw medals without black rounded panels
				UpdateTooltipForMedal(m, rect);
                float rot = t?.Rotation ?? 0f;
                float scalePulse = t?.Scale.X ?? 1f;
                float centerX = rect.X + BgPadding + IconSize / 2f;
                float centerY = rect.Y + BgPadding + IconSize / 2f;
                var drawnRect = MedalIconRenderService.DrawMedalIcon(
                    _spriteBatch,
                    _graphicsDevice,
                    _font,
                    new Vector2(centerX, centerY),
                    IconSize,
                    m.Medal.Id,
                    _imageAssets,
                    scalePulse,
                    rot,
                    IconSoftenStrength);

				if (m.Medal.MaxCount > 0)
				{
					DrawMedalCounter(m, rect);
				}

			}
		}

		private void DrawMedalCounter(EquippedMedal m, Rectangle bgRect)
		{
			string counterText = $"{m.Medal.CurrentCount}/{m.Medal.MaxCount}";
			Vector2 textSize = _font.MeasureString(counterText) * CounterTextScale;

			float centerX = bgRect.X + bgRect.Width / 2f + CounterXOffset;
			float centerY = bgRect.Y + bgRect.Height / 2f + CounterYOffset;

			// Draw black trapezoid background
			Texture2D trapTex = PrimitiveTextureFactory.GetAntialiasedTrapezoid(
				_graphicsDevice,
				CounterTrapezoidWidth,
				CounterTrapezoidHeight,
				CounterTrapezoidLeftOffset,
				CounterTrapezoidTopAngle,
				CounterTrapezoidRightAngle,
				CounterTrapezoidBottomAngle,
				CounterTrapezoidLeftAngle
			);

			Vector2 trapOrigin = new Vector2(CounterTrapezoidWidth / 2f, CounterTrapezoidHeight / 2f);
			_spriteBatch.Draw(trapTex, new Vector2(centerX + CounterTrapXOffset, centerY + CounterTrapYOffset), null, Color.White, 0f, trapOrigin, 1f, SpriteEffects.None, 0f);

			// Draw white text centered on trapezoid position (before trap offset)
			Vector2 textPos = new Vector2(centerX - textSize.X / 2f, centerY - textSize.Y / 2f);
			_spriteBatch.DrawString(_font, counterText, textPos, Color.White, 0f, Vector2.Zero, CounterTextScale, SpriteEffects.None, 0f);
		}

		private void UpdateTooltipForMedal(EquippedMedal medal, Rectangle rect)
		{
			var ui = medal.Owner.GetComponent<UIElement>();
			if (ui == null)
			{
				ui = new UIElement { IsInteractable = true };
				EntityManager.AddComponent(medal.Owner, ui);
			}
			ui.Bounds = rect;
			ui.IsInteractable = false;
			ui.TooltipPosition = TooltipPosition.Below; // show tooltip below and centered when possible
			ui.Tooltip = BuildMedalTooltip(medal);
		}

		private string BuildMedalTooltip(EquippedMedal medal)
		{
			return $"{medal.Medal.Name}\n\n{medal.Medal.Text}";
		}


    [DebugAction("Animation Test")]
    public void debug_animation() 
    {
      EventManager.Publish(new MedalTriggered { MedalEntity = EntityManager.GetEntity("Medal_StLuke"), MedalId = "st_luke" });
    }
  }
}

