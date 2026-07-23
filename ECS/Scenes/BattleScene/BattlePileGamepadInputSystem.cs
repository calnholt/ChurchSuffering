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
	public sealed class BattlePileGamepadInputSystem : Core.System
	{
		public BattlePileGamepadInputSystem(EntityManager entityManager)
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
			bool lb = frame.WasPressed(PlayerButton.LeftShoulder);
			bool rb = frame.WasPressed(PlayerButton.RightShoulder);
			if (!lb && !rb) return;

			if (lb)
				HandleShoulder(wantsDiscard: true);
			else
				HandleShoulder(wantsDiscard: false);
		}

		private bool CanProcessInput()
		{
			if (!Game1.WindowIsActive) return false;
			if (StateSingleton.IsActive) return false;
			if (!IsBattleScene()) return false;
			if (RewardModalDisplaySystem.ShouldSuppressBattleSceneDisplay(EntityManager)) return false;

			PlayerInputFrame frame = PlayerInputService.GetFrame(EntityManager);
			if (!frame.IsGamepadConnected) return false;
			if (GameOverOverlayDisplaySystem.IsOverlayActive(EntityManager)) return false;
			if (IsPauseMenuActive()) return false;
			if (BattleInputGate.IsBattleInputFrozen(EntityManager)) return false;

			return true;
		}

		private bool IsBattleScene()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>()
				.FirstOrDefault()
				?.GetComponent<SceneState>()
				?.Current == SceneId.Battle;
		}

		private bool IsPauseMenuActive()
		{
			var pause = EntityManager.GetEntitiesWithComponent<PauseMenuOverlay>().FirstOrDefault()?.GetComponent<PauseMenuOverlay>();
			return pause != null && pause.Phase != PauseMenuPhase.Hidden;
		}

		private void HandleShoulder(bool wantsDiscard)
		{
			bool targetVisible = wantsDiscard
				? PileDisplayVisibilityService.IsDiscardPileVisible(EntityManager)
				: PileDisplayVisibilityService.IsDrawPileVisible(EntityManager);
			if (!targetVisible) return;

			if (!PileViewService.TryGetOpenPileView(EntityManager, out bool openIsDraw))
			{
				if (PileViewService.IsUnrelatedModalOpen(EntityManager)) return;
				OpenPile(wantsDiscard);
				return;
			}

			bool openIsDiscard = !openIsDraw;
			if (openIsDiscard == wantsDiscard)
				PileViewService.ClosePileView(EntityManager);
			else
				SwitchTo(wantsDiscard);
		}

		private void OpenPile(bool wantsDiscard)
		{
			if (wantsDiscard)
				PileViewService.OpenDiscardPile(EntityManager);
			else
				PileViewService.OpenDrawPile(EntityManager);
		}

		private void SwitchTo(bool wantsDiscard)
		{
			PileViewService.ClosePileView(EntityManager);
			OpenPile(wantsDiscard);
		}
	}
}
