using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Singletons;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// Resolves assigned-block render and input bounds after position tween and parallax.
	/// </summary>
	[DebugTab("Assigned Block Rail")]
	public sealed class AssignedBlockLateLayoutSystem : Core.System
	{
		[DebugEditable(DisplayName = "Equipment Width", Step = 2, Min = 20, Max = 200)]
		public int EquipmentWidth { get; set; } = 76;
		[DebugEditable(DisplayName = "Equipment Height", Step = 2, Min = 20, Max = 240)]
		public int EquipmentHeight { get; set; } = 96;
		[DebugEditable(DisplayName = "Rail Height", Step = 2, Min = 20, Max = 120)]
		public int RailHeight { get; set; } = 54;
		[DebugEditable(DisplayName = "Rail Padding X", Step = 2, Min = 0, Max = 100)]
		public int RailPaddingX { get; set; } = 22;

		public AssignedBlockLateLayoutSystem(EntityManager entityManager) : base(entityManager) { }

		protected override IEnumerable<Entity> GetRelevantEntities() =>
			EntityManager.GetEntitiesWithComponent<AssignedBlockCard>();

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			var anchor = EntityManager.GetEntitiesWithComponent<EnemyAttackBannerAnchor>().FirstOrDefault();
			var anchorTransform = anchor?.GetComponent<Transform>();
			var banner = anchor?.GetComponent<EnemyAttackBannerPresentation>();
			var rail = anchor?.GetComponent<AssignedBlockRailPresentation>();
			Vector2 parallaxDelta = anchorTransform != null && rail != null
				? anchorTransform.Position - rail.LogicalAnchorPos
				: Vector2.Zero;

			var activeBounds = new List<Rectangle>();
			foreach (var entity in GetRelevantEntities()
				.OrderBy(item => item.GetComponent<AssignedBlockCard>().AssignedAtTicks)
				.ToList())
			{
				var assignment = entity.GetComponent<AssignedBlockCard>();
				var presentation = entity.GetComponent<AssignedBlockPresentation>();
				if (assignment == null || presentation == null) continue;

				bool inFlight = entity.GetComponent<CardToDiscardFlight>() != null;
				bool returning = presentation.Phase == AssignedBlockPresentation.PhaseState.Returning;
				presentation.RenderPos = returning || inFlight
					? presentation.CurrentPos
					: presentation.CurrentPos + parallaxDelta;
				presentation.RenderBounds = GetBounds(entity, assignment, presentation.RenderPos, presentation.CurrentScale);
				if (!inFlight && (!returning || assignment.IsEquipment)) activeBounds.Add(presentation.RenderBounds);

				var ui = entity.GetComponent<UIElement>();
				if (ui == null) continue;
				ui.Bounds = presentation.RenderBounds;
				bool settled = !inFlight
					&& presentation.Phase is (AssignedBlockPresentation.PhaseState.Idle
						or AssignedBlockPresentation.PhaseState.Impact);
				ui.IsInteractable = settled
					&& !StateSingleton.IsActive
					&& !BattleInputGate.IsBattleInputFrozen(EntityManager);
				if (!settled) ui.IsHovered = false;
			}

			if (rail == null) return;
			if (activeBounds.Count == 0 || banner?.RenderBounds == Rectangle.Empty)
			{
				rail.Bounds = Rectangle.Empty;
				return;
			}

			int left = activeBounds.Min(bounds => bounds.Left) - RailPaddingX;
			int right = activeBounds.Max(bounds => bounds.Right) + RailPaddingX;
			int maxWidth = Math.Max(1, banner.RenderBounds.Width);
			int width = Math.Clamp(right - left, Math.Min(160, maxWidth), maxWidth);
			rail.Bounds = new Rectangle(
				banner.RenderBounds.Center.X - width / 2,
				banner.RenderBounds.Top - RailHeight + 8 + (int)Math.Round(rail.VerticalOffset),
				width,
				RailHeight);
		}

		private Rectangle GetBounds(Entity entity, AssignedBlockCard assignment, Vector2 position, float scale)
		{
			if (!assignment.IsEquipment && entity.GetComponent<CardData>() != null)
				return CardGeometryService.GetVisualRect(EntityManager, position, scale);

			int width = Math.Max(1, (int)Math.Round(EquipmentWidth * scale / 0.43f));
			int height = Math.Max(1, (int)Math.Round(EquipmentHeight * scale / 0.43f));
			return new Rectangle(
				(int)Math.Round(position.X - width * 0.5f),
				(int)Math.Round(position.Y - height * 0.5f),
				width,
				height);
		}
	}
}
