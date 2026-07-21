using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
	public class MustBeBlockedSystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
        private int blockCount = 0;
        
        public enum MustBeBlockedByType { None, AtLeast, Exactly }
        private MustBeBlockedByType requirementType = MustBeBlockedByType.None;
        private int mustBeBlockedThreshold = 0;
        private int mustBeBlockedAttackSequence = -1;
        private EnemyAttackBase mustBeBlockedAttackDefinition;
        
        // Cache previous state to prevent flickering UI updates
        private bool? previousFulfilledState = null;
        private int previousPlayedCardsCount = -1;

        public MustBeBlockedSystem(EntityManager em, GraphicsDevice gd, SpriteBatch sb) : base(em)
		{
			_graphicsDevice = gd;
			_spriteBatch = sb;
            
            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
            EventManager.Subscribe<AmbushTimerExpired>(OnAmbushTimerExpired);
            LoggingService.Append("MustBeBlockedSystem.ctor", new System.Text.Json.Nodes.JsonObject { ["message"] = "initialized" });
            EventManager.Subscribe<MustBeBlockedEvent>(OnMustBeBlockedEvent);
		}

        private void OnMustBeBlockedEvent(MustBeBlockedEvent evt)
        {
            InitializeBlockRequirement(evt);
        }

        private bool IsAtLeastRequirementMet(int blockCount)
        {
            return blockCount >= mustBeBlockedThreshold;
        }

        private bool IsExactlyRequirementMet(int blockCount)
        {
            return blockCount == mustBeBlockedThreshold;
        }

        private bool IsRequirementFulfilled(int blockCount)
        {
            return requirementType switch
            {
                MustBeBlockedByType.AtLeast => IsAtLeastRequirementMet(blockCount),
                MustBeBlockedByType.Exactly => IsExactlyRequirementMet(blockCount),
                MustBeBlockedByType.None => false,
                _ => false
            };
        }

        private void ResetBlockRequirement()
        {
            requirementType = MustBeBlockedByType.None;
            mustBeBlockedThreshold = 0;
            mustBeBlockedAttackSequence = -1;
            mustBeBlockedAttackDefinition = null;
            previousFulfilledState = null;
            previousPlayedCardsCount = -1;
        }

        private void InitializeBlockRequirement(MustBeBlockedEvent evt)
        {
            var enemy = EntityManager.GetEntity("Enemy");
            var intent = enemy?.GetComponent<AttackIntent>();
            var plannedAttack = intent?.Planned?.FirstOrDefault();
            mustBeBlockedAttackSequence = intent?.ActiveAttackSequence ?? -1;
            mustBeBlockedAttackDefinition = plannedAttack?.AttackDefinition;
            if (mustBeBlockedAttackDefinition == null
                || !EnemyAttackMustBlockRequirementService.TryGetRequirement(
                    mustBeBlockedAttackDefinition.ConditionType,
                    out var activeRequirement)
                || activeRequirement.Threshold != evt.Threshold
                || !RequirementTypesMatch(activeRequirement.Type, evt.Type))
            {
                ResetBlockRequirement();
                return;
            }

            mustBeBlockedThreshold = evt.Threshold;
            requirementType = evt.Type;
            LoggingService.Append("MustBeBlockedSystem.InitializeBlockRequirement", new System.Text.Json.Nodes.JsonObject { ["attackSequence"] = mustBeBlockedAttackSequence, ["attackName"] = mustBeBlockedAttackDefinition?.Name, ["threshold"] = mustBeBlockedThreshold, ["requirementType"] = requirementType.ToString() });
        }

        private static bool RequirementTypesMatch(
            EnemyAttackMustBlockRequirementService.RequirementType activeRequirementType,
            MustBeBlockedByType eventRequirementType)
        {
            return activeRequirementType switch
            {
                EnemyAttackMustBlockRequirementService.RequirementType.AtLeast => eventRequirementType == MustBeBlockedByType.AtLeast,
                EnemyAttackMustBlockRequirementService.RequirementType.Exactly => eventRequirementType == MustBeBlockedByType.Exactly,
                _ => false
            };
        }

        private List<Entity> GetEligibleBlockCards(Deck deck)
        {
            var eligible = new List<Entity>();
            
            if (deck == null || deck.Hand == null || deck.Hand.Count == 0)
            {
                return eligible;
            }

            foreach (var card in deck.Hand)
            {
                var data = card.GetComponent<CardData>();
                if (data == null)
                {
                    continue;
                }

                string id = data.Card.CardId ?? string.Empty;

                // Skip invalid or special types
                if (string.IsNullOrEmpty(id) || data.Card == null)
                {
                    continue;
                }
                if (data.Card.IsWeapon || data.Card.IsToken)
                {
                    continue;
                }

                // Skip intimidated cards
                if (card.GetComponent<Intimidated>() != null)
                {
                    continue;
                }

                // If it's a block card with extra cost, ensure it can be paid
                if (data.Card.Type == CardType.Block && !data.Card.CanPlay(EntityManager, card))
                {
                    continue;
                }

                var plannedAttack = GetComponentHelper.GetPlannedAttack(EntityManager);
                if (plannedAttack != null
                    && !CardColorQualificationService.MeetsBlockingRestriction(
                        card,
                        plannedAttack.BlockingRestrictionType))
                {
                    continue;
                }

                // If shackled, ensure ALL shackled partners are also playable
                if (card.GetComponent<Shackle>() != null)
                {
                    var allShackled = deck.Hand.Where(c => c.GetComponent<Shackle>() != null).ToList();
                    bool allCanPlay = allShackled.All(s => {
                        var sData = s.GetComponent<CardData>();
                        return sData == null || sData.Card.Type != CardType.Block || sData.Card.CanPlay(EntityManager, s);
                    });
                    if (!allCanPlay) continue;
                }

                eligible.Add(card);
            }

            return eligible;
        }

        private void OnChangeBattlePhaseEvent(ChangeBattlePhaseEvent evt)
        {
            if (evt.Current == SubPhase.EnemyAttack)
            {
                ResetBlockRequirement();
                return;
            }
            if (evt.Current != SubPhase.PreBlock) return;
            LoggingService.Append("MustBeBlockedSystem.OnChangeBattlePhaseEvent", new System.Text.Json.Nodes.JsonObject { ["phase"] = evt.Current.ToString() });
            blockCount = 0;
            
            // Initialize the block requirement (checks for both MustBeBlocked and MustBeBlockedExactly)
            
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (requirementType == MustBeBlockedByType.None) return;
            if (mustBeBlockedAttackSequence < 0) return;

            var progress = EntityManager.GetEntitiesWithComponent<EnemyAttackProgress>()
                .FirstOrDefault(e => e.GetComponent<EnemyAttackProgress>()?.AttackSequence == mustBeBlockedAttackSequence);
            if (progress == null) return;
            var progressComponent = progress.GetComponent<EnemyAttackProgress>();
            if (progressComponent == null) return;
            
            blockCount = progressComponent.PlayedCards;
            
            // Only process if the card count has actually changed (prevents redundant evaluations)
            if (blockCount == previousPlayedCardsCount)
            {
                return;
            }
            
            previousPlayedCardsCount = blockCount;
            var isFullfilled = IsRequirementFulfilled(blockCount);
            
            // Only update UI if the fulfillment state has changed (prevents flickering)
            if (previousFulfilledState.HasValue && previousFulfilledState.Value == isFullfilled)
            {
                return;
            }
            
            previousFulfilledState = isFullfilled;
            
            if (mustBeBlockedAttackDefinition != null)
            {
                // mustBeBlockedAttackDefinition.isTextConditionFulfilled = isFullfilled;
            }
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }
        private void OnAmbushTimerExpired(AmbushTimerExpired evt)
        {
            try
            {
                if (requirementType == MustBeBlockedByType.None || mustBeBlockedThreshold <= 0)
                {
                    return;
                }

                // Need current planned ambush attack and matching context
                var enemy = EntityManager.GetEntity("Enemy");
                var intent = enemy?.GetComponent<AttackIntent>();
                var pa = intent?.Planned?.FirstOrDefault();
                if (pa == null || !pa.IsAmbush)
                {
                    return;
                }
                if (intent.ActiveAttackSequence != evt.AttackSequence)
                {
                    return;
                }

                // Already satisfied?
                int needed = mustBeBlockedThreshold - blockCount;
                if (needed <= 0)
                {
                    return;
                }

                var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
                var deck = deckEntity?.GetComponent<Deck>();
                if (deck == null)
                {
                    return;
                }

                // Get eligible cards using extracted helper
                var eligible = GetEligibleBlockCards(deck);
                if (eligible.Count == 0)
                {
                    LoggingService.Append("MustBeBlockedSystem.OnAmbushTimerExpired", new System.Text.Json.Nodes.JsonObject { ["message"] = "no eligible cards to auto-assign" });
                    return;
                }

                int toAssign = Math.Min(needed, eligible.Count);
                // Randomize order and take up to needed
                var rng = Random.Shared;
                var randomized = eligible
                    .OrderBy(_ => rng.Next())
                    .Take(toAssign)
                    .ToList();

                LoggingService.Append("MustBeBlockedSystem.OnAmbushTimerExpired", new System.Text.Json.Nodes.JsonObject { ["assigningCount"] = randomized.Count, ["needed"] = needed, ["currentCount"] = blockCount, ["threshold"] = mustBeBlockedThreshold });

                foreach (var card in randomized)
                {
                    var t = card.GetComponent<Transform>();
                    if (deckEntity != null && t != null)
                    {
                        var startPos = t.Position;
                        EventManager.Publish(new CardMoveRequested
                        {
                            Card = card,
                            Deck = deckEntity,
                            Destination = CardZoneType.AssignedBlock,
                            Reason = "AssignBlockAutoAmbush"
                        });
                        var abc = card.GetComponent<AssignedBlockCard>();
                        if (abc != null)
                        {
                            abc.ReturnTargetPos = startPos;
                        }
                    }

                    var data = card.GetComponent<CardData>();
                    if (data == null)
                    {
                        continue;
                    }

                    int blockVal = BlockValueService.GetTotalBlockValue(card);
					var colors = CardColorQualificationService.GetQualifiedColors(card);
                    EventManager.Publish(new BlockAssignmentAdded
                    {
                        Card = card,
						Colors = colors,
                        DeltaBlock = blockVal
                    });
                }
            }
            catch (Exception ex)
            {
                LoggingService.Append("MustBeBlockedSystem.OnAmbushTimerExpired.error", new System.Text.Json.Nodes.JsonObject { ["exception"] = ex.ToString() });
            }
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return Array.Empty<Entity>();
        }

    }

}
