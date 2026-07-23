using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Input;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Singletons;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
	public sealed class RewardModalGamepadInputSystem : Core.System
	{
		public RewardModalGamepadInputSystem(EntityManager entityManager)
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
			if (!IsSupportedScene()) return false;
			if (!RewardModalDisplaySystem.IsInteractiveOverlayOpen(EntityManager)) return false;

			PlayerInputFrame frame = PlayerInputService.GetFrame(EntityManager);
			if (!frame.IsGamepadConnected) return false;
			if (IsPauseMenuActive()) return false;

			return true;
		}

		private bool IsSupportedScene()
		{
			SceneId scene = EntityManager.GetEntitiesWithComponent<SceneState>()
				.FirstOrDefault()
				?.GetComponent<SceneState>()
				?.Current ?? SceneId.None;
			return scene is SceneId.Climb or SceneId.Battle;
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

			if (ClimbOverviewViewService.IsUnrelatedModalOpen(EntityManager)) return;

			ClimbOverviewViewService.Open(EntityManager);
		}
	}
}
