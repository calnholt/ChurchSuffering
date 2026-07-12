using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// Plays the card restriction mutation cutscene during battle when restrictions are applied.
	/// </summary>
	[DebugTab("Battle Mutation Anim")]
	public class BattleCardMutationDisplaySystem : Core.System
	{
		private const string AnchorEntityName = "Battle_MutationAnimAnchor";

		private sealed class MutationRequest
		{
			public Entity TargetCard;
			public CardApplicationType Type;
		}

		private sealed class ActiveMutation
		{
			public Entity TargetCard;
			public CardApplicationType Type;
			public Entity BaseDisplayCard;
			public Entity FinalDisplayCard;
			public CardRestrictionMutationAnimator.MutationAnimation Animation;
		}

		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly CardRestrictionMutationAnimator _animator = new CardRestrictionMutationAnimator();
		private readonly Queue<MutationRequest> _queue = new Queue<MutationRequest>();
		private ActiveMutation _active;
		private Entity _anchor;

		public bool IsBusy => _active != null || _queue.Count > 0;

		public BattleCardMutationDisplaySystem(
			EntityManager entityManager,
			GraphicsDevice graphicsDevice,
			SpriteBatch spriteBatch) : base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			EventManager.Subscribe<CardRestrictionMutationAnimationRequested>(OnAnimationRequested);
			EventManager.Subscribe<DeleteCachesEvent>(_ => ClearAll());
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			if (!IsBattleScene()) return;

			SyncInputGate();
			if (_active == null)
			{
				TryStartNext();
			}

			if (_active == null) return;

			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			if (_animator.Update(dt, EntityManager, _anchor))
			{
				CompleteActiveAnimation();
			}
		}

		public void Draw()
		{
			if (!IsBattleScene() || _active == null) return;

			_animator.Draw(
				EntityManager,
				_anchor,
				ResolveLeftMiddle(),
				ResolveCenter(),
				ResolveRightMiddle());
		}

		private void OnAnimationRequested(CardRestrictionMutationAnimationRequested evt)
		{
			if (!IsBattleScene() || evt?.TargetCard == null) return;

			_queue.Enqueue(new MutationRequest
			{
				TargetCard = evt.TargetCard,
				Type = evt.Type,
			});

			SyncInputGate();
			if (_active == null) TryStartNext();
		}

		private void TryStartNext()
		{
			if (_active != null || _queue.Count == 0) return;

			while (_queue.Count > 0)
			{
				var request = _queue.Dequeue();
				if (!TryStart(request)) continue;
				return;
			}

			SyncInputGate();
		}

		private bool TryStart(MutationRequest request)
		{
			var target = request?.TargetCard;
			if (target == null || !target.IsActive) return false;
			if (CardApplicationService.IsApplied(target, request.Type)) return false;

			var (baseCard, finalCard) = CardRestrictionMutationDisplayFactory.CreateDisplayPairFromBattleCard(
				EntityManager,
				target,
				request.Type);
			if (baseCard == null || finalCard == null)
			{
				CardRestrictionMutationDisplayFactory.DestroyDisplayCard(EntityManager, baseCard);
				CardRestrictionMutationDisplayFactory.DestroyDisplayCard(EntityManager, finalCard);
				CardApplicationService.ApplyRestriction(EntityManager, target, request.Type);
				return false;
			}

			EnsureAnchor();
			ResetAnchorTransform();
			if (!target.HasComponent<SuppressCardZoneRender>())
			{
				EntityManager.AddComponent(target, new SuppressCardZoneRender { Owner = target });
			}

			var animation = new CardRestrictionMutationAnimator.MutationAnimation
			{
				BaseCard = baseCard,
				FinalCard = finalCard,
				SfxRestrictionName = CardRestrictionMutationDisplayFactory.ToRestrictionName(request.Type),
				OnSwap = () => CardApplicationService.ApplyRestriction(EntityManager, target, request.Type),
			};

			_active = new ActiveMutation
			{
				TargetCard = target,
				Type = request.Type,
				BaseDisplayCard = baseCard,
				FinalDisplayCard = finalCard,
				Animation = animation,
			};

			_animator.Start(animation);
			SyncInputGate();
			return true;
		}

		private void CompleteActiveAnimation()
		{
			if (_active != null)
			{
				var target = _active.TargetCard;
				if (target != null && target.HasComponent<SuppressCardZoneRender>())
				{
					EntityManager.RemoveComponent<SuppressCardZoneRender>(target);
				}

				CardRestrictionMutationDisplayFactory.DestroyDisplayCard(EntityManager, _active.BaseDisplayCard);
				CardRestrictionMutationDisplayFactory.DestroyDisplayCard(EntityManager, _active.FinalDisplayCard);
				_active = null;
			}

			if (_queue.Count > 0)
			{
				TryStartNext();
				return;
			}

			SyncInputGate();
		}

		private void ClearAll()
		{
			_queue.Clear();
			if (_active != null)
			{
				var target = _active.TargetCard;
				if (target != null && target.IsActive && target.HasComponent<SuppressCardZoneRender>())
				{
					EntityManager.RemoveComponent<SuppressCardZoneRender>(target);
				}

				CardRestrictionMutationDisplayFactory.DestroyDisplayCard(EntityManager, _active.BaseDisplayCard);
				CardRestrictionMutationDisplayFactory.DestroyDisplayCard(EntityManager, _active.FinalDisplayCard);
				_active = null;
			}

			_animator.Cancel();
			SyncInputGate();
		}

		private void SyncInputGate()
		{
			bool busy = IsBusy || _animator.IsActive;
			StateSingleton.PreventClicking = busy;
			SetBattleAnimationActive(busy);
		}

		private void SetBattleAnimationActive(bool active)
		{
			var phase = EntityManager.GetEntitiesWithComponent<PhaseState>()
				.FirstOrDefault()
				?.GetComponent<PhaseState>();
			if (phase != null) phase.BattleAnimationActive = active;
		}

		private void EnsureAnchor()
		{
			_anchor = EntityManager.GetEntity(AnchorEntityName);
			if (_anchor != null) return;

			_anchor = EntityManager.CreateEntity(AnchorEntityName);
			EntityManager.AddComponent(_anchor, new Transform { Scale = Vector2.One, ZOrder = 10003 });
			EntityManager.AddComponent(_anchor, new OwnedByScene { Scene = SceneId.Battle });
		}

		private void ResetAnchorTransform()
		{
			var transform = _anchor?.GetComponent<Transform>();
			if (transform == null) return;
			transform.Scale = Vector2.One;
			transform.Rotation = 0f;
		}

		private Vector2 ResolveLeftMiddle()
		{
			int cardWidth = CardGeometryService.GetSettings(EntityManager)?.CardWidth ?? CardGeometrySettings.DefaultWidth;
			float vh = Game1.VirtualHeight;
			return new Vector2(
				-cardWidth * 0.5f + CardRestrictionMutationSettings.LeftExitOffsetX,
				vh * 0.5f + CardRestrictionMutationSettings.VerticalCenterOffsetY);
		}

		private Vector2 ResolveRightMiddle()
		{
			int cardWidth = CardGeometryService.GetSettings(EntityManager)?.CardWidth ?? CardGeometrySettings.DefaultWidth;
			float vw = Game1.VirtualWidth;
			float vh = Game1.VirtualHeight;
			return new Vector2(
				vw + cardWidth * 0.5f + CardRestrictionMutationSettings.RightExitOffsetX,
				vh * 0.5f + CardRestrictionMutationSettings.VerticalCenterOffsetY);
		}

		private Vector2 ResolveCenter()
		{
			float vw = Game1.VirtualWidth;
			float vh = Game1.VirtualHeight;
			return new Vector2(
				vw * 0.5f + CardRestrictionMutationSettings.CenterOffsetX,
				vh * 0.5f + CardRestrictionMutationSettings.CenterOffsetY + CardRestrictionMutationSettings.VerticalCenterOffsetY);
		}

		private bool IsBattleScene()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>()
				.FirstOrDefault()
				?.GetComponent<SceneState>()
				?.Current == SceneId.Battle;
		}
	}
}
