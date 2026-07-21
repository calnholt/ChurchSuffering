using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ChurchSuffering.ECS.Core;

namespace ChurchSuffering.ECS.Systems
{
	/// <summary>
	/// Ticks the global TimerScheduler each frame.
	/// </summary>
	public class TimerSchedulerSystem : Core.System
	{
		public TimerSchedulerSystem(EntityManager entityManager) : base(entityManager) { }

		protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();
		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			TimerScheduler.Update(dt);
		}
	}
}


