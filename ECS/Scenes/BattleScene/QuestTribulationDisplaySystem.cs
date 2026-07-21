using System;
using System.Linq;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Systems
{
	/// <summary>
	/// Displays the chalice icon to the right of the CourageDisplay when tribulations are active.
	/// Shows tooltip with tribulation text on hover. Includes pulse animation similar to medals.
	/// </summary>
	[DebugTab("Tribulation Display")]
	public class QuestTribulationDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ImageAssetService _imageAssets;
		private Texture2D _chaliceTexture;
        private Entity _chaliceEntity;

		// Layout/debug controls
		[DebugEditable(DisplayName = "Chalice Spacing", Step = 2, Min = 0, Max = 200)]
		public int ChaliceSpacing { get; set; } = 50;

		[DebugEditable(DisplayName = "Chalice Scale", Step = 0.1f, Min = 0.1f, Max = 5f)]
		public float ChaliceScale { get; set; } = .18f;

		[DebugEditable(DisplayName = "Chalice Offset X", Step = 1, Min = -200, Max = 200)]
		public int ChaliceOffsetX { get; set; } = 0;

		[DebugEditable(DisplayName = "Chalice Offset Y", Step = 1, Min = -200, Max = 200)]
		public int ChaliceOffsetY { get; set; } = 0;

		// Pulse animation debug fields (matching MedalDisplaySystem)
		[DebugEditable(DisplayName = "Pulse Duration (s)", Step = 0.05f, Min = 0.1f, Max = 2f)]
		public float PulseDurationSeconds { get; set; } = 0.5f;

		[DebugEditable(DisplayName = "Pulse Scale Amp", Step = 0.01f, Min = 0f, Max = 0.6f)]
		public float PulseScaleAmplitude { get; set; } = 0.44f;

		[DebugEditable(DisplayName = "Jiggle Degrees", Step = 0.5f, Min = 0f, Max = 45f)]
		public float JiggleDegrees { get; set; } = 5f;

		[DebugEditable(DisplayName = "Pulse Frequency (Hz)", Step = 0.1f, Min = 0.5f, Max = 8f)]
		public float PulseFrequencyHz { get; set; } = 1.7f;

		public QuestTribulationDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ImageAssetService imageAssets)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_imageAssets = imageAssets;
			TryLoadAssets();
			EventManager.Subscribe<TribulationTriggered>(OnTribulationTriggered);
		}

        private void OnTribulationTriggered(TribulationTriggered evt)
        {
            if (evt == null) return;
            LoggingService.Append("QuestTribulationDisplaySystem.OnTribulationTriggered", new System.Text.Json.Nodes.JsonObject
            {
                ["questId"] = evt.QuestId
            });
            if (_chaliceEntity != null)
            {
                var cfg = new JigglePulseConfig
                {
                    PulseDurationSeconds = PulseDurationSeconds,
                    PulseScaleAmplitude = PulseScaleAmplitude,
                    JiggleDegrees = JiggleDegrees,
                    PulseFrequencyHz = PulseFrequencyHz
                };
                EventManager.Publish(new JigglePulseEvent { Target = _chaliceEntity, Config = cfg });
            }
        }

		private void TryLoadAssets()
		{
			_chaliceTexture = _imageAssets?.TryGetTexture("chalice");
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<Player>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }

		public void Draw()
		{
			var playerEntity = GetRelevantEntities().FirstOrDefault();
			if (playerEntity == null) return;

			// Check if player has any tribulations
			var tribulations = EntityManager.GetEntitiesWithComponent<Tribulation>()
				.Where(e => e.GetComponent<Tribulation>()?.PlayerOwner == playerEntity)
				.Select(e => e.GetComponent<Tribulation>())
				.Where(t => t != null)
				.ToList();

			if (tribulations.Count == 0)
			{
				// Hide chalice if no tribulations
				if (_chaliceEntity != null)
				{
					var ui = _chaliceEntity.GetComponent<UIElement>();
					if (ui != null) ui.Bounds = Rectangle.Empty;
				}
				return;
			}

			// Anchor to the layout-owned courage region.
			var courage = playerEntity.GetComponent<Courage>();
			if (courage == null) return;

			Vector2? courageCenterValue = ResolveCourageAnchor(playerEntity);
			if (!courageCenterValue.HasValue) return;
			Vector2 courageCenter = courageCenterValue.Value;

			// Position chalice to the right of courage display
			Vector2 chalicePosition = new Vector2(
				courageCenter.X + ChaliceSpacing + ChaliceOffsetX,
				courageCenter.Y + ChaliceOffsetY
			);

			// Ensure chalice entity exists and is valid
			const string ChaliceEntityName = "TribulationChalice";
			var existingChalice = EntityManager.GetEntity(ChaliceEntityName);
			if (existingChalice != null && existingChalice.IsActive)
			{
				_chaliceEntity = existingChalice;
			}
			else
			{
				_chaliceEntity = EntityManager.CreateEntity(ChaliceEntityName);
				EntityManager.AddComponent(_chaliceEntity, new Transform { Position = chalicePosition, ZOrder = 10000 });
				EntityManager.AddComponent(_chaliceEntity, ParallaxLayer.GetUIParallaxLayer());
			}

			// Draw chalice icon
			if (_chaliceTexture != null)
			{
                var t = _chaliceEntity.GetComponent<Transform>();
                float rot = t?.Rotation ?? 0f;
                float scalePulse = t?.Scale.X ?? 1f;
                float finalScale = ChaliceScale * scalePulse;
				var origin = new Vector2(_chaliceTexture.Width / 2f, _chaliceTexture.Height / 2f);
				_spriteBatch.Draw(
					_chaliceTexture,
					chalicePosition,
					null,
					Color.White,
                    rot,
					origin,
					finalScale,
					SpriteEffects.None,
					0f
				);

				// Update UI element bounds for tooltip
				int iconSize = (int)(Math.Max(_chaliceTexture.Width, _chaliceTexture.Height) * finalScale);
				var hitRect = new Rectangle(
					(int)(chalicePosition.X - iconSize / 2f),
					(int)(chalicePosition.Y - iconSize / 2f),
					iconSize,
					iconSize
				);

				var ui = _chaliceEntity.GetComponent<UIElement>();
				if (ui == null)
				{
					ui = new UIElement { IsInteractable = false };
					EntityManager.AddComponent(_chaliceEntity, ui);
				}
				// Always update tooltip and bounds to ensure they're current after tribulations are recreated
				ui.Bounds = hitRect;
				ui.TooltipType = TooltipType.Text;
				ui.Tooltip = BuildTribulationTooltip(tribulations);
				ui.TooltipPosition = TooltipPosition.Above;
				ui.IsInteractable = false;

				var transform = _chaliceEntity.GetComponent<Transform>();
				if (transform != null)
				{
					transform.Position = chalicePosition;
				}
			}
		}

		private string BuildTribulationTooltip(List<Tribulation> tribulations)
		{
			if (tribulations == null || tribulations.Count == 0) return string.Empty;
			return string.Join("\n\n", tribulations.Select(t => t.Text ?? string.Empty));
		}

		internal Vector2? ResolveCourageAnchor(Entity player)
		{
			var courageRegionEntity = EntityManager.GetEntitiesWithComponent<PlayerHudRegion>()
				.FirstOrDefault(entity => entity.GetComponent<PlayerHudRegion>()?.Type == PlayerHudRegionType.Courage);
			var courageRegion = courageRegionEntity?.GetComponent<PlayerHudRegion>();
			Rectangle bounds = courageRegionEntity == null || courageRegion == null
				? Rectangle.Empty
				: TransformResolverService.ResolveLocalBounds(EntityManager, courageRegionEntity, courageRegion.Bounds);
			if (courageRegion != null
				&& bounds.Width > 0
				&& bounds.Height > 0)
			{
				return new Vector2(
					bounds.Right,
					bounds.Center.Y);
			}

			return player?.GetComponent<Transform>()?.Position;
		}
	}
}
