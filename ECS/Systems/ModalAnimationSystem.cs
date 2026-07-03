using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
	public class ModalAnimationSystem : Core.System
	{
		public ModalAnimationSystem(EntityManager entityManager) : base(entityManager)
		{
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<ModalAnimation>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var animation = entity.GetComponent<ModalAnimation>();
			if (animation == null) return;

			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			ApplyRequestedPhase(animation);
			Advance(animation, dt);
			SyncInputContext(entity, animation);
			SyncRootInputBlocker(entity, animation);
			SyncInputSuppression(animation);
		}

		private static void ApplyRequestedPhase(ModalAnimation animation)
		{
			if (animation.RequestedVisible)
			{
				if (animation.Phase == ModalAnimationPhase.Hidden || animation.Phase == ModalAnimationPhase.Exiting)
				{
					animation.Phase = ModalAnimationPhase.Entering;
					animation.ElapsedSeconds = 0f;
				}
				return;
			}

			if (animation.Phase == ModalAnimationPhase.Visible || animation.Phase == ModalAnimationPhase.Entering)
			{
				animation.Phase = ModalAnimationPhase.Exiting;
				animation.ElapsedSeconds = 0f;
				animation.ExitSequence++;
			}
		}

		private static void Advance(ModalAnimation animation, float dt)
		{
			switch (animation.Phase)
			{
				case ModalAnimationPhase.Entering:
				{
					animation.ElapsedSeconds += dt;
					if (animation.ElapsedSeconds >= System.Math.Max(0.001f, animation.EnterDurationSeconds))
					{
						animation.ElapsedSeconds = 0f;
						animation.Phase = ModalAnimationPhase.Visible;
					}
					break;
				}
				case ModalAnimationPhase.Exiting:
				{
					animation.ElapsedSeconds += dt;
					if (animation.ElapsedSeconds >= System.Math.Max(0.001f, animation.ExitDurationSeconds))
					{
						animation.ElapsedSeconds = 0f;
						animation.Phase = ModalAnimationPhase.Hidden;
						animation.CompletedExitSequence = animation.ExitSequence;
					}
					break;
				}
			}
		}

		private static void SyncInputContext(Entity entity, ModalAnimation animation)
		{
			var context = entity.GetComponent<InputContext>();
			if (context == null) return;
			context.IsActive = BlocksInput(animation);
		}

		private static void SyncRootInputBlocker(Entity entity, ModalAnimation animation)
		{
			var ui = entity.GetComponent<UIElement>();
			if (ui == null) return;

			if (BlocksInput(animation))
			{
				ui.Bounds = new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight);
				ui.IsInteractable = true;
				ui.IsHidden = false;
				ui.LayerType = UILayerType.Overlay;
				return;
			}

			ui.Bounds = Rectangle.Empty;
			ui.IsInteractable = false;
			ui.IsHidden = true;
			ui.IsHovered = false;
			ui.IsClicked = false;
			ui.LayerType = UILayerType.Default;
		}

		private static bool BlocksInput(ModalAnimation animation)
		{
			return animation.Phase != ModalAnimationPhase.Hidden || animation.RequestedVisible;
		}

		private void SyncInputSuppression(ModalAnimation animation)
		{
			string contextId = animation.InputContextId ?? string.Empty;
			if (string.IsNullOrWhiteSpace(contextId)) return;

			bool shouldSuppress = animation.Phase == ModalAnimationPhase.Entering
				|| animation.Phase == ModalAnimationPhase.Exiting;

			if (shouldSuppress)
			{
				foreach (Entity entity in EntityManager.GetEntitiesWithComponent<UIElement>().ToList())
				{
					var member = entity.GetComponent<InputContextMember>();
					if (member?.ContextId != contextId) continue;

					var suppression = entity.GetComponent<ModalInputSuppression>();
					if (suppression != null) continue;

					entity.GetComponent<UIElement>()?.Suppress();
					EntityManager.AddComponent(entity, new ModalInputSuppression { ContextId = contextId });
				}
				return;
			}

			foreach (Entity entity in EntityManager.GetEntitiesWithComponent<ModalInputSuppression>().ToList())
			{
				var suppression = entity.GetComponent<ModalInputSuppression>();
				if (suppression?.ContextId != contextId) continue;

				entity.GetComponent<UIElement>()?.Restore();
				EntityManager.RemoveComponent<ModalInputSuppression>(entity);
			}
		}
	}
}
