using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Input;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Singletons;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
	public sealed class ClimbOverviewGamepadInputSystem : Core.System
	{
		public ClimbOverviewGamepadInputSystem(EntityManager entityManager)
			: base(entityManager)
		{
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return Enumerable.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (!CanProcessInput()) return;

			PlayerInputFrame frame = PlayerInputService.GetFrame(EntityManager);
			if (!frame.WasPressed(PlayerButton.RightShoulder)) return;

			HandleRightShoulder();
		}

		private bool CanProcessInput()
		{
			if (!Game1.WindowIsActive) return false;
			if (StateSingleton.IsActive) return false;
			if (!IsClimbScene()) return false;

			PlayerInputFrame frame = PlayerInputService.GetFrame(EntityManager);
			if (!frame.IsGamepadConnected) return false;
			if (IsPauseMenuActive()) return false;

			return true;
		}

		private bool IsClimbScene()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>()
				.FirstOrDefault()
				?.GetComponent<SceneState>()
				?.Current == SceneId.Climb;
		}

		private bool IsPauseMenuActive()
		{
			var pause = EntityManager.GetEntitiesWithComponent<PauseMenuOverlay>().FirstOrDefault()?.GetComponent<PauseMenuOverlay>();
			return pause != null && pause.Phase != PauseMenuPhase.Hidden;
		}

		private void HandleRightShoulder()
		{
			if (ClimbOverviewViewService.IsOverviewOpen(EntityManager))
			{
				ClimbOverviewViewService.Close(EntityManager);
				return;
			}

			if (StateSingleton.PreventClicking) return;
			if (ClimbOverviewViewService.IsUnrelatedModalOpen(EntityManager)) return;

			ClimbOverviewViewService.Open(EntityManager);
		}
	}
}
