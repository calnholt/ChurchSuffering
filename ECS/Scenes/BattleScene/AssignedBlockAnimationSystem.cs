using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Assigned Block Rail")]
	public sealed class AssignedBlockAnimationSystem : Core.System
	{
		[DebugEditable(DisplayName = "Card Scale %", Step = 1f, Min = 10f, Max = 100f)]
		public float CardScalePercent { get; set; } = 24f;
		[DebugEditable(DisplayName = "Equipment Scale", Step = 0.02f, Min = 0.1f, Max = 1f)]
		public float EquipmentScale { get; set; } = 0.43f;
		[DebugEditable(DisplayName = "Slot Spacing", Step = 2, Min = 44, Max = 120)]
		public float SlotSpacing { get; set; } = 84f;
		[DebugEditable(DisplayName = "Minimum Spacing", Step = 2, Min = 20, Max = 100)]
		public float MinimumSpacing { get; set; } = 44f;
		[DebugEditable(DisplayName = "Rail Side Padding", Step = 2, Min = 0, Max = 100)]
		public float RailSidePadding { get; set; } = 48f;
		[DebugEditable(DisplayName = "Rail Offset Y", Step = 1, Min = -200, Max = 200)]
		public float RailOffsetY { get; set; } = -60f;
		[DebugEditable(DisplayName = "Card Bottom Gap", Step = 1, Min = 0, Max = 100)]
		public float CardBottomGap { get; set; } = 10f;
		[DebugEditable(DisplayName = "Pullback Seconds", Step = 0.01f, Min = 0f, Max = 1f)]
		public float PullbackSeconds { get; set; } = 0.04f;
		[DebugEditable(DisplayName = "Launch Seconds", Step = 0.01f, Min = 0f, Max = 1f)]
		public float LaunchSeconds { get; set; } = 0.20f;
		[DebugEditable(DisplayName = "Impact Seconds", Step = 0.01f, Min = 0f, Max = 1f)]
		public float ImpactSeconds { get; set; } = 0.08f;
		[DebugEditable(DisplayName = "Return Seconds", Step = 0.01f, Min = 0f, Max = 1f)]
		public float ReturnSeconds { get; set; } = 0.18f;
		public AssignedBlockAnimationSystem(EntityManager entityManager) : base(entityManager) { }

		protected override IEnumerable<Entity> GetRelevantEntities() =>
			EntityManager.GetEntitiesWithComponent<AssignedBlockCard>();

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			var entities = GetRelevantEntities()
				.Where(entity => entity.GetComponent<AssignedBlockPresentation>() != null)
				.Where(entity => entity.GetComponent<CardToDiscardFlight>() == null)
				.OrderBy(entity => entity.GetComponent<AssignedBlockCard>().AssignedAtTicks)
				.ToList();
			if (entities.Count == 0) return;

			var anchor = EntityManager.GetEntitiesWithComponent<EnemyAttackBannerAnchor>().FirstOrDefault();
			var anchorTransform = anchor?.GetComponent<Transform>();
			var banner = anchor?.GetComponent<EnemyAttackBannerPresentation>();
			Vector2 anchorTop = anchorTransform?.Position ?? new Vector2(Game1.VirtualWidth * 0.5f, Game1.VirtualHeight * 0.5f);
			var rail = anchor?.GetComponent<AssignedBlockRailPresentation>();
			if (anchor != null && rail == null)
			{
				rail = new AssignedBlockRailPresentation();
				EntityManager.AddComponent(anchor, rail);
			}
			if (rail != null)
			{
				rail.LogicalAnchorPos = anchorTop;
				rail.VerticalOffset = RailOffsetY;
				rail.Flash = entities.Max(entity => entity.GetComponent<AssignedBlockPresentation>()?.RailFlash ?? 0f);
			}
			float availableWidth = Math.Max(MinimumSpacing, (banner?.LogicalWidth ?? 620f) - RailSidePadding * 2f);
			float spacing = entities.Count <= 1
				? 0f
				: MathHelper.Clamp(availableWidth / (entities.Count - 1), MinimumSpacing, SlotSpacing);
			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

			for (int index = 0; index < entities.Count; index++)
			{
				var entity = entities[index];
				var assignment = entity.GetComponent<AssignedBlockCard>();
				var presentation = entity.GetComponent<AssignedBlockPresentation>();
				if (assignment == null || presentation == null) continue;

				float targetScale = assignment.IsEquipment
					? MathHelper.Clamp(EquipmentScale, 0.1f, 1f)
					: MathHelper.Clamp(CardScalePercent, 10f, 100f) * 0.01f;
				float bottomOffset = GetVisualBottomOffset(assignment, targetScale);
				float offsetIndex = index - (entities.Count - 1) * 0.5f;
				presentation.TargetPos = new Vector2(
					anchorTop.X + offsetIndex * spacing,
					anchorTop.Y + RailOffsetY - CardBottomGap - bottomOffset);
				Advance(entity, assignment, presentation, targetScale, dt);
			}
		}

		private void Advance(
			Entity entity,
			AssignedBlockCard assignment,
			AssignedBlockPresentation presentation,
			float targetScale,
			float dt)
		{
			presentation.Elapsed += dt;
			switch (presentation.Phase)
			{
				case AssignedBlockPresentation.PhaseState.Pullback:
				{
					float p = Progress(presentation.Elapsed, PullbackSeconds);
					presentation.CurrentPos = Vector2.Lerp(presentation.StartPos, presentation.StartPos + new Vector2(-24f, -14f), p);
					presentation.CurrentScale = MathHelper.Lerp(presentation.StartScale, targetScale * 0.82f, p);
					presentation.CurrentRotation = 0f;
					if (p >= 1f)
					{
						presentation.StartPos = presentation.CurrentPos;
						presentation.StartScale = presentation.CurrentScale;
						presentation.Phase = AssignedBlockPresentation.PhaseState.Launch;
						presentation.Elapsed = 0f;
					}
					break;
				}
				case AssignedBlockPresentation.PhaseState.Launch:
				{
					float p = Progress(presentation.Elapsed, LaunchSeconds);
					presentation.CurrentPos = AssignedBlockAnimationService.LerpPosition(presentation.StartPos, presentation.TargetPos, p);
					presentation.CurrentScale = AssignedBlockAnimationService.LerpScale(presentation.StartScale, targetScale, p);
					presentation.CurrentRotation = 0f;
					if (p >= 1f)
					{
						presentation.Phase = AssignedBlockPresentation.PhaseState.Impact;
						presentation.Elapsed = 0f;
					}
					break;
				}
				case AssignedBlockPresentation.PhaseState.Impact:
				{
					float p = Progress(presentation.Elapsed, ImpactSeconds);
					float pulse = (float)Math.Sin(p * Math.PI);
					presentation.CurrentPos = presentation.TargetPos + new Vector2(0f, 5f * (1f - p));
					presentation.CurrentScale = targetScale * (1f + 0.08f * pulse);
					presentation.RailFlash = 1f - p;
					if (p >= 1f)
					{
						presentation.CurrentPos = presentation.TargetPos;
						presentation.CurrentScale = targetScale;
						presentation.Phase = AssignedBlockPresentation.PhaseState.Idle;
						presentation.Elapsed = 0f;
					}
					break;
				}
				case AssignedBlockPresentation.PhaseState.Idle:
				{
					float slide = 1f - (float)Math.Exp(-10f * dt);
					presentation.CurrentPos = Vector2.Lerp(presentation.CurrentPos, presentation.TargetPos, slide);
					presentation.CurrentScale = MathHelper.Lerp(presentation.CurrentScale, targetScale, slide);
					presentation.CurrentRotation = 0f;
					presentation.RailFlash = Math.Max(0f, presentation.RailFlash - dt * 5f);
					break;
				}
				case AssignedBlockPresentation.PhaseState.Returning:
				{
					float p = Progress(presentation.Elapsed, ReturnSeconds);
					var handPose = ResolveReturnPose(entity, assignment);
					presentation.CurrentPos = AssignedBlockAnimationService.LerpPosition(presentation.StartPos, handPose.Position, p);
					presentation.CurrentScale = AssignedBlockAnimationService.LerpScale(presentation.StartScale, handPose.Scale, p);
					presentation.CurrentRotation = MathHelper.Lerp(presentation.StartRotation, handPose.Rotation, AssignedBlockAnimationService.CubicOut(p));
					SyncTransform(entity, presentation, handPose.ZOrder);
					SyncPositionTween(entity, presentation.CurrentPos);
					if (p >= 1f && !presentation.ReturnCompletionPublished)
					{
						presentation.ReturnCompletionPublished = true;
						EventManager.Publish(new AssignedBlockReturnCompleted { Card = entity });
					}
					return;
				}
			}

			SyncTransform(entity, presentation, null);
		}

		private float GetVisualBottomOffset(AssignedBlockCard assignment, float scale)
		{
			if (assignment.IsEquipment) return 130f * scale * 0.5f;
			return CardGeometryService.GetVisualRect(EntityManager, Vector2.Zero, scale).Bottom;
		}

		private (Vector2 Position, float Scale, float Rotation, int ZOrder) ResolveReturnPose(Entity entity, AssignedBlockCard assignment)
		{
			var transform = entity.GetComponent<Transform>();
			Vector2 position = assignment.ReturnTargetPos != Vector2.Zero
				? assignment.ReturnTargetPos
				: transform?.Position ?? Vector2.Zero;
			float scale = transform?.Scale.X ?? 1f;
			float rotation = transform?.Rotation ?? 0f;
			int zOrder = transform?.ZOrder ?? 0;

			if (!assignment.IsEquipment)
			{
				var deck = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
				if (deck?.Hand.Contains(entity) == true)
				{
					var tween = entity.GetComponent<PositionTween>();
					if (tween != null && tween.Target != Vector2.Zero) position = tween.Target;
				}
			}
			return (position, scale, rotation, zOrder);
		}

		private void SyncTransform(Entity entity, AssignedBlockPresentation presentation, int? zOrder)
		{
			var transform = entity.GetComponent<Transform>();
			if (transform == null)
			{
				transform = new Transform();
				EntityManager.AddComponent(entity, transform);
			}
			transform.Position = presentation.CurrentPos;
			transform.Scale = new Vector2(presentation.CurrentScale);
			transform.Rotation = presentation.CurrentRotation;
			if (zOrder.HasValue) transform.ZOrder = zOrder.Value;
		}

		private static void SyncPositionTween(Entity entity, Vector2 position)
		{
			var tween = entity.GetComponent<PositionTween>();
			if (tween == null) return;
			tween.Current = position;
			tween.Target = position;
			tween.Initialized = true;
		}

		private static float Progress(float elapsed, float duration) =>
			duration <= 0f ? 1f : MathHelper.Clamp(elapsed / duration, 0f, 1f);
	}
}
