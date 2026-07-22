using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Data.Loadouts;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
	/// <summary>
	/// Plays a climb-map card upgrade cutscene: enter from left, jiggle pulse swap, hold, exit right.
	/// </summary>
	[DebugTab("Climb Upgrade Anim")]
	public class ClimbCardUpgradeDisplaySystem : Core.System
	{
		private enum ClimbCardAnimMode
		{
			Upgrade,
			Mutation,
			Boon,
		}

		private class CardAnimRequest
		{
			public ClimbCardAnimMode Mode;
			public string BaseCardKey = string.Empty;
			public string UpgradedCardKey = string.Empty;
			public string DeckEntryId = string.Empty;
			public string MutationCardKey = string.Empty;
			public string RestrictionName = string.Empty;
			public List<string> CurrentRestrictionNames = new List<string>();
			public bool TransitionToBattleOnComplete;
			public bool ReleasesClimbTurnover;
			public List<string> RestrictionNames = new List<string>();
			public List<CardBoonSave> BeforeBoons = new List<CardBoonSave>();
			public List<CardBoonSave> AfterBoons = new List<CardBoonSave>();
			public string BeforeSecondaryColor = string.Empty;
			public string AfterSecondaryColor = string.Empty;
		}

		private class ActiveClimbAnimation
		{
			public ClimbCardAnimMode Mode;
			public Entity BaseCard;
			public Entity FinalCard;
			public bool MutationPersisted;
			public string DeckEntryId = string.Empty;
			public string RestrictionName = string.Empty;
			public bool TransitionToBattleOnComplete;
			public bool ReleasesClimbTurnover;
			public CardRestrictionMutationAnimator.MutationAnimation Animation;
		}

		private const string AnchorEntityName = "Climb_UpgradeAnimAnchor";

		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly CardRestrictionMutationAnimator _animator = new CardRestrictionMutationAnimator();
		private readonly Queue<CardAnimRequest> _queue = new Queue<CardAnimRequest>();
		private ActiveClimbAnimation _active;
		private Entity _anchor;
		private bool _inputBlockedByThisSystem;

		public ClimbCardUpgradeDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			EventManager.Subscribe<ClimbCardUpgradeAnimationRequested>(OnAnimationRequested);
			EventManager.Subscribe<ClimbCardMutationAnimationRequested>(OnMutationAnimationRequested);
			EventManager.Subscribe<ClimbCardBoonAnimationRequested>(OnBoonAnimationRequested);
			EventManager.Subscribe<DeleteCachesEvent>(_ => ClearAll());
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			if (!IsClimbScene() || _active == null) return;

			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			if (_animator.Update(dt, EntityManager, _anchor))
			{
				CompleteActiveAnimation();
			}
		}

		public void Draw()
		{
			if (!IsClimbScene() || _active == null) return;

			_animator.Draw(
				EntityManager,
				_anchor,
				ResolveLeftMiddle(),
				ResolveCenter(),
				ResolveRightMiddle());
		}

		private void OnAnimationRequested(ClimbCardUpgradeAnimationRequested evt)
		{
			if (!IsClimbScene()) return;
			if (evt == null
				|| string.IsNullOrWhiteSpace(evt.BaseCardKey)
				|| string.IsNullOrWhiteSpace(evt.UpgradedCardKey))
			{
				return;
			}

			_queue.Enqueue(new CardAnimRequest
			{
				Mode = ClimbCardAnimMode.Upgrade,
				DeckEntryId = evt.DeckEntryId,
				BaseCardKey = evt.BaseCardKey,
				UpgradedCardKey = evt.UpgradedCardKey,
				BeforeSecondaryColor = evt.BeforeSecondaryColor ?? string.Empty,
				AfterSecondaryColor = evt.AfterSecondaryColor ?? string.Empty,
				ReleasesClimbTurnover = evt.DelayClimbTurnoverUntilComplete,
			});
			if (_active == null) TryStartNext();
		}

		private void OnMutationAnimationRequested(ClimbCardMutationAnimationRequested evt)
		{
			if (!IsClimbScene()) return;
			if (evt == null
				|| string.IsNullOrWhiteSpace(evt.DeckEntryId)
				|| string.IsNullOrWhiteSpace(evt.CardKey)
				|| string.IsNullOrWhiteSpace(evt.RestrictionName))
			{
				return;
			}

			_queue.Enqueue(new CardAnimRequest
			{
				Mode = ClimbCardAnimMode.Mutation,
				DeckEntryId = evt.DeckEntryId,
				MutationCardKey = evt.CardKey,
				RestrictionName = evt.RestrictionName,
				CurrentRestrictionNames = evt.CurrentRestrictionNames ?? new List<string>(),
				TransitionToBattleOnComplete = evt.TransitionToBattleOnComplete,
			});
			if (_active == null) TryStartNext();
		}

		private void OnBoonAnimationRequested(ClimbCardBoonAnimationRequested evt)
		{
			if (!IsClimbScene()) return;
			if (evt == null
				|| string.IsNullOrWhiteSpace(evt.DeckEntryId)
				|| string.IsNullOrWhiteSpace(evt.CardKey))
			{
				return;
			}

			_queue.Enqueue(new CardAnimRequest
			{
				Mode = ClimbCardAnimMode.Boon,
				DeckEntryId = evt.DeckEntryId,
				MutationCardKey = evt.CardKey,
				RestrictionNames = evt.RestrictionNames ?? new List<string>(),
				BeforeBoons = evt.BeforeBoons ?? new List<CardBoonSave>(),
				AfterBoons = evt.AfterBoons ?? new List<CardBoonSave>(),
				BeforeSecondaryColor = evt.BeforeSecondaryColor ?? string.Empty,
				AfterSecondaryColor = evt.AfterSecondaryColor ?? string.Empty,
				ReleasesClimbTurnover = evt.DelayClimbTurnoverUntilComplete,
			});
			if (_active == null) TryStartNext();
		}

		private void TryStartNext()
		{
			if (_active != null || _queue.Count == 0) return;

			var request = _queue.Dequeue();
			Entity baseCard;
			Entity finalCard;
			CardData.CardColor? secondaryColor = ResolveSecondaryColor(request.DeckEntryId);
			if (request.Mode == ClimbCardAnimMode.Mutation)
			{
				var boons = ResolveBoons(request.DeckEntryId);
				(baseCard, finalCard) = CardRestrictionMutationDisplayFactory.CreateDisplayPairFromKeys(
					EntityManager,
					request.MutationCardKey,
					request.CurrentRestrictionNames,
					request.RestrictionName,
					secondaryColor);
				CardBoonApplicator.Synchronize(EntityManager, baseCard, boons);
				CardBoonApplicator.Synchronize(EntityManager, finalCard, boons);
			}
			else if (request.Mode == ClimbCardAnimMode.Boon)
			{
				baseCard = CardRestrictionMutationDisplayFactory.CreateDisplayCard(
					EntityManager,
					request.MutationCardKey,
					request.RestrictionNames,
					ParseSecondaryColor(request.BeforeSecondaryColor),
					request.BeforeBoons);
				finalCard = CardRestrictionMutationDisplayFactory.CreateDisplayCard(
					EntityManager,
					request.MutationCardKey,
					request.RestrictionNames,
					ParseSecondaryColor(request.AfterSecondaryColor),
					request.AfterBoons);
			}
			else
			{
				var boons = ResolveBoons(request.DeckEntryId);
				CardData.CardColor? beforeSecondary = !string.IsNullOrWhiteSpace(request.BeforeSecondaryColor)
					|| !string.IsNullOrWhiteSpace(request.AfterSecondaryColor)
					? ParseSecondaryColor(request.BeforeSecondaryColor)
					: secondaryColor;
				CardData.CardColor? afterSecondary = !string.IsNullOrWhiteSpace(request.BeforeSecondaryColor)
					|| !string.IsNullOrWhiteSpace(request.AfterSecondaryColor)
					? ParseSecondaryColor(request.AfterSecondaryColor)
					: secondaryColor;
				baseCard = CardRestrictionMutationDisplayFactory.CreateDisplayCard(EntityManager, request.BaseCardKey, secondaryColor: beforeSecondary, boons: boons);
				finalCard = CardRestrictionMutationDisplayFactory.CreateDisplayCard(EntityManager, request.UpgradedCardKey, secondaryColor: afterSecondary, boons: boons);
			}

			if (baseCard == null || finalCard == null)
			{
				CardRestrictionMutationDisplayFactory.DestroyDisplayCard(EntityManager, baseCard);
				CardRestrictionMutationDisplayFactory.DestroyDisplayCard(EntityManager, finalCard);
				PublishTurnoverRelease(request.ReleasesClimbTurnover);
				TryStartNext();
				return;
			}

			EnsureAnchor();
			ResetAnchorTransform();

			var animation = new CardRestrictionMutationAnimator.MutationAnimation
			{
				BaseCard = baseCard,
				FinalCard = finalCard,
				PlayUpgradeSfx = request.Mode is ClimbCardAnimMode.Upgrade or ClimbCardAnimMode.Boon,
				SfxRestrictionName = request.RestrictionName,
				OnSwap = () => PersistMutationIfNeeded(request),
			};

			_active = new ActiveClimbAnimation
			{
				Mode = request.Mode,
				BaseCard = baseCard,
				FinalCard = finalCard,
				DeckEntryId = request.DeckEntryId,
				RestrictionName = request.RestrictionName,
				TransitionToBattleOnComplete = request.TransitionToBattleOnComplete,
				ReleasesClimbTurnover = request.ReleasesClimbTurnover,
				Animation = animation,
			};

			_animator.Start(animation);
			BlockInput();
		}

		private static CardData.CardColor? ResolveSecondaryColor(string entryId)
		{
			if (string.IsNullOrWhiteSpace(entryId)) return null;
			var entry = SaveCache.GetRunDeckEntry(RunDeckService.PrimaryLoadoutId, entryId);
			return Enum.TryParse(entry?.secondaryColor, true, out CardData.CardColor color)
				&& CardColorQualificationService.IsPlayableColor(color)
				? color
				: null;
		}

		private static CardData.CardColor? ParseSecondaryColor(string value)
		{
			return Enum.TryParse(value, true, out CardData.CardColor color)
				&& CardColorQualificationService.IsPlayableColor(color)
				? color
				: null;
		}

		private static List<CardBoonSave> ResolveBoons(string entryId)
		{
			if (string.IsNullOrWhiteSpace(entryId)) return new List<CardBoonSave>();
			return SaveCache.GetRunDeckEntry(RunDeckService.PrimaryLoadoutId, entryId)?.boons
				?? new List<CardBoonSave>();
		}

		private void CompleteActiveAnimation()
		{
			bool transitionToBattle = false;
			bool releasesClimbTurnover = false;
			if (_active != null)
			{
				transitionToBattle = _active.TransitionToBattleOnComplete;
				releasesClimbTurnover = _active.ReleasesClimbTurnover;
				CardRestrictionMutationDisplayFactory.DestroyDisplayCard(EntityManager, _active.BaseCard);
				CardRestrictionMutationDisplayFactory.DestroyDisplayCard(EntityManager, _active.FinalCard);
				_active = null;
			}
			PublishTurnoverRelease(releasesClimbTurnover);

			if (_queue.Count > 0)
			{
				TryStartNext();
				return;
			}

			UnblockInput();
			if (transitionToBattle)
			{
				EventManager.Publish(new ShowTransition { Scene = SceneId.Battle });
			}
		}

		private void ClearAll()
		{
			foreach (var request in _queue) PublishTurnoverRelease(request.ReleasesClimbTurnover);
			_queue.Clear();
			if (_active != null)
			{
				PublishTurnoverRelease(_active.ReleasesClimbTurnover);
				CardRestrictionMutationDisplayFactory.DestroyDisplayCard(EntityManager, _active.BaseCard);
				CardRestrictionMutationDisplayFactory.DestroyDisplayCard(EntityManager, _active.FinalCard);
				_active = null;
			}

			_animator.Cancel();
			UnblockInput();
		}

		private static void PublishTurnoverRelease(bool releasesClimbTurnover)
		{
			if (releasesClimbTurnover)
				EventManager.Publish(new ClimbCardUpgradeAnimationCompleted { ReleasesClimbTurnover = true });
		}

		private void BlockInput()
		{
			if (_inputBlockedByThisSystem) return;
			EventManager.Publish(new SetPlayerInputEnabledEvent { Enabled = false });
			_inputBlockedByThisSystem = true;
		}

		private void UnblockInput()
		{
			if (!_inputBlockedByThisSystem) return;
			EventManager.Publish(new SetPlayerInputEnabledEvent { Enabled = true });
			_inputBlockedByThisSystem = false;
		}

		private void PersistMutationIfNeeded(CardAnimRequest request)
		{
			if (request == null || request.Mode != ClimbCardAnimMode.Mutation || _active == null || _active.MutationPersisted) return;
			_active.MutationPersisted = true;
			if (SaveCache.AddRunDeckEntryRestriction(
				RunDeckService.PrimaryLoadoutId,
				request.DeckEntryId,
				request.RestrictionName))
			{
				RunDeckService.EnsureRunDeck(EntityManager);
			}
		}

		private void EnsureAnchor()
		{
			_anchor = EntityManager.GetEntity(AnchorEntityName);
			if (_anchor != null) return;

			_anchor = EntityManager.CreateEntity(AnchorEntityName);
			EntityManager.AddComponent(_anchor, new Transform { Scale = Vector2.One, ZOrder = 10003 });
			EntityManager.AddComponent(_anchor, new OwnedByScene { Scene = SceneId.Climb });
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

		private bool IsClimbScene()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>()
				.FirstOrDefault()
				?.GetComponent<SceneState>()
				?.Current == SceneId.Climb;
		}

		[DebugAction("Play Test Upgrade Animation")]
		private void Debug_PlayTestUpgradeAnimation()
		{
			const string baseKey = "strike|White";
			string upgradedKey = RunDeckService.BuildUpgradedCardKey(baseKey);
			if (string.IsNullOrWhiteSpace(upgradedKey)) return;

			EventManager.Publish(new ClimbCardUpgradeAnimationRequested
			{
				BaseCardKey = baseKey,
				UpgradedCardKey = upgradedKey,
			});
		}
	}
}
