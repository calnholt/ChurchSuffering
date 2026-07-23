using System;
using System.Collections.Generic;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
	[DebugTab("WayStation Collection Motion")]
	public sealed class WayStationCollectionMotionSystem : Core.System
	{
		[DebugEditable(DisplayName = "Tab Duration", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float TabDuration { get; set; } = 0.20f;

		[DebugEditable(DisplayName = "Icon Duration", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float IconDuration { get; set; } = 0.22f;

		[DebugEditable(DisplayName = "Card Fan Duration", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float CardDuration { get; set; } = 0.28f;

		[DebugEditable(DisplayName = "Card Switch Duration", Step = 0.01f, Min = 0.05f, Max = 1f)]
		public float CardSwitchDuration { get; set; } = 0.28f;

		[DebugEditable(DisplayName = "Meter Duration", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float MeterDuration { get; set; } = 0.45f;

		public WayStationCollectionMotionSystem(EntityManager entityManager) : base(entityManager) { }

		protected override IEnumerable<Entity> GetRelevantEntities() =>
			EntityManager.GetEntitiesWithComponent<WayStationCollectionMotion>();

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var motion = entity.GetComponent<WayStationCollectionMotion>();
			float dt = Math.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
			bool hovered = entity.GetComponent<UIElement>()?.IsHovered == true;
			motion.TargetHover = hovered ? 1f : 0f;
			motion.TargetGlow = hovered ? 1f : 0f;

			float duration = TabDuration;
			var cardStack = entity.GetComponent<WayStationCollectionCardStackPresentation>();
			if (cardStack != null)
			{
				duration = CardDuration;
				motion.TargetFanAngle = hovered ? 1f : 0f;
				AdvanceCardSwitch(cardStack, dt);
			}
			else if (entity.GetComponent<WayStationCollectionSaintTilePresentation>() != null
				|| entity.GetComponent<WayStationCollectionEquipmentTilePresentation>() != null)
			{
				duration = IconDuration;
				motion.TargetScale = hovered ? 1.1f : 1f;
			}
			else
			{
				motion.TargetScale = 1f;
			}

			if (entity.GetComponent<WayStationCollectionModalRoot>() != null)
			{
				var catalog = entity.GetComponent<WayStationCollectionCatalogComponent>()?.Catalog;
				var state = entity.GetComponent<WayStationCollectionModalState>();
				(int unlocked, int total) = state?.ActiveTab switch
				{
					WayStationCollectionTab.Saints => (catalog?.Saints.Count ?? 0, catalog?.SaintTotal ?? 0),
					WayStationCollectionTab.Equipment => (catalog?.Equipment.Count ?? 0, catalog?.EquipmentTotal ?? 0),
					_ => (catalog?.Cards.Count ?? 0, catalog?.CardTotal ?? 0),
				};
				motion.TargetMeterProgress = total <= 0 ? 0f : MathHelper.Clamp(unlocked / (float)total, 0f, 1f);
			}

			motion.Hover = WayStationCollectionModalLogic.Approach(motion.Hover, motion.TargetHover, duration, dt);
			motion.Scale = WayStationCollectionModalLogic.Approach(motion.Scale, motion.TargetScale, duration, dt);
			motion.FanAngle = WayStationCollectionModalLogic.Approach(motion.FanAngle, motion.TargetFanAngle, CardDuration, dt);
			motion.Glow = WayStationCollectionModalLogic.Approach(motion.Glow, motion.TargetGlow, duration, dt);
			motion.MeterProgress = WayStationCollectionModalLogic.Approach(
				motion.MeterProgress,
				motion.TargetMeterProgress,
				MeterDuration,
				dt);
		}

		private void AdvanceCardSwitch(
			WayStationCollectionCardStackPresentation stack,
			float elapsedSeconds)
		{
			if (!stack.PendingFrontColor.HasValue)
			{
				stack.ColorSwitchProgress = 1f;
				return;
			}

			float duration = Math.Max(0.001f, CardSwitchDuration);
			stack.ColorSwitchProgress = MathHelper.Clamp(
				stack.ColorSwitchProgress + elapsedSeconds / duration,
				0f,
				1f);
			if (stack.ColorSwitchProgress < 1f) return;

			stack.FrontColor = stack.PendingFrontColor.Value;
			stack.PendingFrontColor = null;
		}
	}
}
