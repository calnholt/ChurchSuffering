using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
	public class PlayerHudFeedbackSystem : Core.System
	{
		public PlayerHudFeedbackSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<ModifyCourageEvent>(_ => Trigger(PlayerHudRegionType.Courage));
			EventManager.Subscribe<SetCourageEvent>(_ => Trigger(PlayerHudRegionType.Courage));
			EventManager.Subscribe<ModifyTemperanceEvent>(_ => Trigger(PlayerHudRegionType.Temperance));
			EventManager.Subscribe<SetTemperanceEvent>(_ => Trigger(PlayerHudRegionType.Temperance));
			EventManager.Subscribe<ModifyActionPointsEvent>(_ => Trigger(PlayerHudRegionType.ActionPoint));
			EventManager.Subscribe<SetActionPointsEvent>(_ => Trigger(PlayerHudRegionType.ActionPoint));
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<PlayerHudFeedbackState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var feedback = entity.GetComponent<PlayerHudFeedbackState>();
			if (feedback == null || !feedback.IsPulsing) return;

			feedback.ElapsedSeconds += Math.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
			float duration = Math.Max(0.01f, feedback.DurationSeconds);
			float progress = MathHelper.Clamp(feedback.ElapsedSeconds / duration, 0f, 1f);
			float maxScale = GetAnchor()?.ResourcePulseMaxScale ?? 1.12f;
			feedback.Scale = 1f + (Math.Max(1f, maxScale) - 1f) * (float)Math.Sin(MathHelper.Pi * progress);
			if (progress >= 1f)
			{
				feedback.IsPulsing = false;
				feedback.Scale = 1f;
			}
		}

		private void Trigger(PlayerHudRegionType type)
		{
			var entity = EntityManager.GetEntitiesWithComponent<PlayerHudRegion>()
				.FirstOrDefault(candidate => candidate.GetComponent<PlayerHudRegion>()?.Type == type);
			var feedback = entity?.GetComponent<PlayerHudFeedbackState>();
			if (feedback == null) return;

			feedback.IsPulsing = true;
			feedback.ElapsedSeconds = 0f;
			feedback.DurationSeconds = GetAnchor()?.ResourcePulseDurationSeconds ?? 0.30f;
			feedback.Scale = 1f;
		}

		private PlayerHudAnchor GetAnchor()
		{
			return EntityManager.GetEntitiesWithComponent<PlayerHudAnchor>()
				.FirstOrDefault()
				?.GetComponent<PlayerHudAnchor>();
		}
	}
}
