using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems;

public sealed class ClimbV2MotionSystem : Core.System
{
	private const float AshesSeconds = 1.05f;
	private const float TurnoverGapSeconds = 0.08f;
	private const float PurchaseExitSeconds = 0.42f;

	public ClimbV2MotionSystem(EntityManager entityManager) : base(entityManager) { }

	protected override IEnumerable<Entity> GetRelevantEntities() =>
		EntityManager.GetEntitiesWithComponent<ClimbV2ChoiceMotion>();

	protected override void UpdateEntity(Entity entity, GameTime gameTime)
	{
		var motion = entity.GetComponent<ClimbV2ChoiceMotion>();
		if (motion == null) return;
		float dt = Math.Max(0f, (float)(gameTime?.ElapsedGameTime.TotalSeconds ?? 0d));
		if (motion.Phase == ClimbV2MotionPhase.Settled)
		{
			ResetVisual(motion);
			return;
		}

		motion.ElapsedSeconds += dt;
		float localSeconds = motion.ElapsedSeconds - Math.Max(0f, motion.DelaySeconds);
		if (localSeconds < 0f)
		{
			if (motion.Phase == ClimbV2MotionPhase.Entering)
			{
				motion.Opacity = 0f;
				motion.Offset = EntranceOffset(entity);
			}
			return;
		}

		if (motion.Phase == ClimbV2MotionPhase.AshesExiting)
		{
			ApplyAshes(motion, localSeconds);
			if (localSeconds < AshesSeconds) return;
			motion.Phase = ClimbV2MotionPhase.Entering;
			motion.ElapsedSeconds = -TurnoverGapSeconds;
			motion.DelaySeconds = 0f;
			return;
		}

		if (motion.Phase == ClimbV2MotionPhase.Purchasing)
		{
			ApplyPurchase(motion, localSeconds);
			if (localSeconds < PurchaseExitSeconds) return;
			motion.Phase = ClimbV2MotionPhase.AwaitingPurchaseReconciliation;
			motion.ElapsedSeconds = 0f;
			motion.DelaySeconds = 0f;
			motion.Offset = new Vector2(105f, 0f);
			motion.Opacity = 0f;
			motion.Brightness = 0.72f;
			motion.Blur = 3f;
			return;
		}
		if (motion.Phase == ClimbV2MotionPhase.AwaitingPurchaseReconciliation) return;

		float duration = entity.GetComponent<ClimbEncounterPresentation>() != null ? 0.72f : 0.62f;
		float progress = MathHelper.Clamp(localSeconds / duration, 0f, 1f);
		float eased = EaseOutCubic(progress);
		motion.Offset = Vector2.Lerp(EntranceOffset(entity), Vector2.Zero, eased);
		motion.Opacity = eased;
		motion.Brightness = MathHelper.Lerp(entity.GetComponent<ClimbEncounterPresentation>() != null ? 0.58f : 0.68f, 1f, eased);
		motion.Blur = MathHelper.Lerp(entity.GetComponent<ClimbEncounterPresentation>() != null ? 5f : 3f, 0f, eased);
		if (progress >= 1f)
		{
			motion.Phase = ClimbV2MotionPhase.Settled;
			ResetVisual(motion);
		}
	}

	public override void Update(GameTime gameTime)
	{
		base.Update(gameTime);
		bool active = GetRelevantEntities().Any(entity => entity.GetComponent<ClimbV2ChoiceMotion>()?.Phase != ClimbV2MotionPhase.Settled);
		SyncInputSuppression(active);
	}

	private static void ApplyAshes(ClimbV2ChoiceMotion motion, float seconds)
	{
		float progress = MathHelper.Clamp(seconds / AshesSeconds, 0f, 1f);
		motion.Offset = new Vector2(0f, -48f * EaseInCubic(progress));
		motion.Opacity = progress <= 0.30f ? 1f : 1f - MathHelper.Clamp((progress - 0.30f) / 0.70f, 0f, 1f);
		if (progress <= 0.30f)
		{
			float t = progress / 0.30f;
			motion.Grayscale = 0.3f * t;
			motion.Sepia = 0.7f * t;
			motion.Brightness = MathHelper.Lerp(1f, 1.25f, t);
			motion.Blur = 0f;
			return;
		}
		motion.Grayscale = MathHelper.Lerp(0.3f, 1f, (progress - 0.30f) / 0.70f);
		motion.Sepia = MathHelper.Lerp(0.7f, 1f, (progress - 0.30f) / 0.70f);
		motion.Brightness = progress < 0.68f
			? MathHelper.Lerp(1.25f, 0.75f, (progress - 0.30f) / 0.38f)
			: MathHelper.Lerp(0.75f, 0.35f, (progress - 0.68f) / 0.32f);
		motion.Blur = progress < 0.68f ? 2f * (progress - 0.30f) / 0.38f : MathHelper.Lerp(2f, 9f, (progress - 0.68f) / 0.32f);
	}

	private static void ApplyPurchase(ClimbV2ChoiceMotion motion, float seconds)
	{
		float progress = MathHelper.Clamp(seconds / PurchaseExitSeconds, 0f, 1f);
		float eased = EaseInCubic(progress);
		motion.Offset = new Vector2(105f * eased, 0f);
		motion.Opacity = 1f - eased;
		motion.Brightness = MathHelper.Lerp(1f, 0.72f, eased);
		motion.Blur = 3f * eased;
	}

	private void SyncInputSuppression(bool suppress)
	{
		foreach (var entity in EntityManager.GetAllEntities().Where(IsClimbAction).ToList())
		{
			var ui = entity.GetComponent<UIElement>();
			bool marked = entity.GetComponent<ClimbV2InputSuppression>() != null;
			if (suppress && !marked)
			{
				ui.Suppress();
				ui.IsClicked = false;
				EntityManager.AddComponent(entity, new ClimbV2InputSuppression());
			}
			else if (!suppress && marked)
			{
				ui.Restore();
				EntityManager.RemoveComponent<ClimbV2InputSuppression>(entity);
			}
		}
	}

	private static bool IsClimbAction(Entity entity)
	{
		var ui = entity?.GetComponent<UIElement>();
		if (ui == null || entity.GetComponent<OwnedByScene>()?.Scene != SceneId.Climb) return false;
		return entity.GetComponent<ClimbSlotPresentation>() != null || entity.GetComponent<ClimbOverviewButton>() != null;
	}

	private static Vector2 EntranceOffset(Entity entity)
	{
		if (entity.GetComponent<ClimbShopItemPresentation>() != null) return new Vector2(-105f, 0f);
		if (entity.GetComponent<ClimbEventPresentation>() != null) return new Vector2(120f, 0f);
		var slot = entity.GetComponent<ClimbSlotPresentation>();
		return new Vector2(0f, slot?.SlotIndex == 1 ? 220f : -220f);
	}

	private static void ResetVisual(ClimbV2ChoiceMotion motion)
	{
		motion.Offset = Vector2.Zero;
		motion.Opacity = 1f;
		motion.Brightness = 1f;
		motion.Grayscale = 0f;
		motion.Sepia = 0f;
		motion.Blur = 0f;
	}

	private static float EaseInCubic(float value)
	{
		value = MathHelper.Clamp(value, 0f, 1f);
		return value * value * value;
	}

	private static float EaseOutCubic(float value)
	{
		value = MathHelper.Clamp(value, 0f, 1f);
		float inverse = 1f - value;
		return 1f - inverse * inverse * inverse;
	}
}
