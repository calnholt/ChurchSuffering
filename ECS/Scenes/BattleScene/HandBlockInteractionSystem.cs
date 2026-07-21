using System.Linq;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Events;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Objects.EnemyAttacks;

namespace ChurchSuffering.ECS.Systems
{
	[ChurchSuffering.Diagnostics.DebugTab("Combat Debug")]
	public class HandBlockInteractionSystem : Core.System
	{
		public HandBlockInteractionSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<AssignCardAsBlockRequested>(OnAssignCardAsBlockRequested);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			// Event-driven system; assignment requests are handled synchronously.
			return System.Array.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
		}

		private void OnAssignCardAsBlockRequested(AssignCardAsBlockRequested evt)
		{
			var card = evt?.Card;
			if (card == null) return;
			if (BattleInputGate.IsBattleInputFrozen(EntityManager)) return;
			if (!BattleInputGate.TryAllowTutorialAction(EntityManager, TutorialAction.AssignBlock, card)) return;
			// Only during Block phase
			var phaseState = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault();
			if (phaseState == null) return;
			var phase = phaseState.GetComponent<PhaseState>();
			if (phase.Sub != SubPhase.Block) return;
			// Need a current intent context
			var enemy = EntityManager.GetEntitiesWithComponent<AttackIntent>().FirstOrDefault();
			var pa = enemy?.GetComponent<AttackIntent>()?.Planned?.FirstOrDefault();
			if (pa == null) return;

			// Hit-test hand cards
			var deck = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
			if (deck?.Hand == null || !deck.Hand.Contains(card)) return;

			var ui = card.GetComponent<UIElement>();
			var data = card.GetComponent<CardData>();
			if (ui == null || data?.Card == null) return;
			if (card.GetComponent<AssignedBlockCard>() != null) return;

			string id = data.Card.CardId ?? string.Empty;
			// Weapons and tokens cannot be assigned as block.
			try
			{
				if (!string.IsNullOrEmpty(id))
				{
					if (data.Card.IsWeapon || data.Card.IsToken) return;
				}
			}
			catch { }

			if (card.GetComponent<Intimidated>() != null)
			{
				EventManager.Publish(new CantPlayCardMessage { Message = "Can't block with intimidated cards!" });
				return;
			}

			if (card.GetComponent<Pledge>() != null)
			{
				EventManager.Publish(new CantPlayCardMessage { Message = "Can't block with pledged card!" });
				return;
			}

			if (card.GetComponent<CannotBlockThisAttack>() is CannotBlockThisAttack cannotBlock)
			{
				EventManager.Publish(cannotBlock.Reason);
				return;
			}

			if (data.Card.Type == CardType.Block && !data.Card.CanPlay(EntityManager, card))
			{
				data.Card.OnCantPlay?.Invoke(EntityManager, card);
				return;
			}

			if (card.GetComponent<Shackle>() != null)
			{
				var allShackled = deck.Hand.Where(c => c.GetComponent<Shackle>() != null).ToList();
				foreach (var shackledCard in allShackled)
				{
					var shackledData = shackledCard.GetComponent<CardData>();
					if (shackledData?.Card != null
						&& shackledData.Card.Type == CardType.Block
						&& !shackledData.Card.CanPlay(EntityManager, shackledCard))
					{
						EventManager.Publish(new CantPlayCardMessage { Message = "All shackled cards must be playable!" });
						return;
					}
				}
			}

			var enemyAttack = GetComponentHelper.GetPlannedAttack(EntityManager);
			if (enemyAttack != null && enemyAttack.BlockingRestrictionType != BlockingRestrictionType.None)
			{
				var message = EnemyAttackTextHelper.GetBlockingRestrictionText(enemyAttack.BlockingRestrictionType);
				if (message.EndsWith(".")) message = message.Substring(0, message.Length - 1) + "!";
				bool canPlay = CardColorQualificationService.MeetsBlockingRestriction(
					card,
					enemyAttack.BlockingRestrictionType);
				if (!canPlay)
				{
					EventManager.Publish(new CantPlayCardMessage { Message = message });
					return;
				}
			}

			int blockValue = BlockValueService.GetTotalBlockValue(card);
			var colors = CardColorQualificationService.GetQualifiedColors(card);
			var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
			var transform = card.GetComponent<Transform>();
			if (deckEntity != null && transform != null)
			{
				var startPosition = transform.Position;
				EventManager.Publish(new CardMoveRequested
				{
					Card = card,
					Deck = deckEntity,
					Destination = CardZoneType.AssignedBlock,
					Reason = "AssignBlock"
				});
				var assignedBlock = card.GetComponent<AssignedBlockCard>();
				if (assignedBlock != null)
				{
					assignedBlock.ReturnTargetPos = startPosition;
				}
			}

			EventManager.Publish(new BlockAssignmentAdded
			{
				Card = card,
				Colors = colors,
				DeltaBlock = blockValue,
			});
		}
	}
}
