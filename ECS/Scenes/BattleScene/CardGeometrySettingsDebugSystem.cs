using System;
using System.Linq;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.Diagnostics;
using System.Collections.Generic;

namespace ChurchSuffering.ECS.Systems
{
	/// <summary>
	/// Debug-only system that exposes card geometry fields for live editing via the debug menu.
	/// </summary>
	[DebugTab("Card Geometry")]
	public class CardGeometrySettingsDebugSystem : Core.System
	{
		public CardGeometrySettingsDebugSystem(EntityManager entityManager) : base(entityManager) { }

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return Array.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, Microsoft.Xna.Framework.GameTime gameTime) { }

		private CardGeometrySettings EnsureSettings()
		{
			var e = EntityManager.GetEntitiesWithComponent<CardGeometrySettings>().FirstOrDefault();
			if (e == null)
			{
				e = EntityManager.CreateEntity("CardGeometrySettings");
				var s = new CardGeometrySettings
				{
					CardWidth = CardGeometrySettings.DefaultWidth,
					CardHeight = CardGeometrySettings.DefaultHeight,
					CardOffsetYExtra = CardGeometrySettings.DefaultOffsetYExtra,
					CardGap = CardGeometrySettings.DefaultGap,
					CardCornerRadius = CardGeometrySettings.DefaultCornerRadius,
					HighlightBorderThickness = CardGeometrySettings.DefaultHighlightBorderThickness
				};
				EntityManager.AddComponent(e, s);
				return s;
			}
			return e.GetComponent<CardGeometrySettings>();
		}

		[DebugEditable(DisplayName = "Card Width", Step = 1, Min = 10, Max = 2000)]
		public int CardWidth { get => EnsureSettings().CardWidth; set => EnsureSettings().CardWidth = Math.Max(10, value); }

		[DebugEditable(DisplayName = "Card Height", Step = 1, Min = 10, Max = 2000)]
		public int CardHeight { get => EnsureSettings().CardHeight; set => EnsureSettings().CardHeight = Math.Max(10, value); }

		[DebugEditable(DisplayName = "Card Offset Y Extra", Step = 1, Min = -500, Max = 500)]
		public int CardOffsetYExtra { get => EnsureSettings().CardOffsetYExtra; set => EnsureSettings().CardOffsetYExtra = value; }

		[DebugEditable(DisplayName = "Card Gap", Step = 1, Min = -500, Max = 500)]
		public int CardGap { get => EnsureSettings().CardGap; set => EnsureSettings().CardGap = value; }

		[DebugEditable(DisplayName = "Corner Radius", Step = 1, Min = 0, Max = 64)]
		public int CornerRadius { get => EnsureSettings().CardCornerRadius; set => EnsureSettings().CardCornerRadius = Math.Max(0, value); }

		[DebugEditable(DisplayName = "Highlight Border Thickness", Step = 1, Min = 0, Max = 64)]
		public int HighlightBorderThickness { get => EnsureSettings().HighlightBorderThickness; set => EnsureSettings().HighlightBorderThickness = Math.Max(0, value); }

		[DebugAction("Apply Defaults")]
		public void ApplyDefaults()
		{
			var s = EnsureSettings();
			s.CardWidth = CardGeometrySettings.DefaultWidth;
			s.CardHeight = CardGeometrySettings.DefaultHeight;
			s.CardOffsetYExtra = CardGeometrySettings.DefaultOffsetYExtra;
			s.CardGap = CardGeometrySettings.DefaultGap;
			s.CardCornerRadius = CardGeometrySettings.DefaultCornerRadius;
			s.HighlightBorderThickness = CardGeometrySettings.DefaultHighlightBorderThickness;
		}
	}
}

