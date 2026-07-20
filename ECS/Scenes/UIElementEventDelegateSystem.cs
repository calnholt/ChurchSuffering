using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Data.Save;
using System;
using System.Linq;
using System.Collections.Generic;
using Crusaders30XX.ECS.Services;
namespace Crusaders30XX.ECS.Systems
{
    internal static class UIElementEventDelegateService
    {
        public static void HandleEvent(UIElementEventType type, Entity entity, EntityManager entityManager)
        {
            var activeDeck = entityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
            var handleLog = new System.Text.Json.Nodes.JsonObject
            {
                ["type"] = type.ToString(),
                ["entityId"] = entity.Id,
                ["entityName"] = entity.Name,
                ["entityType"] = entity.GetType().Name,
            };
            if (entity.HasComponent<CardData>())
            {
                handleLog["cardId"] = entity.GetComponent<CardData>()?.Card?.CardId ?? "unknown";
                handleLog["inDeckHand"] = activeDeck?.Hand.Contains(entity) ?? false;
                handleLog["countsForLayout"] = HandStateLoggingService.CountsForHandLayout(entity);
                handleLog["countsForDraw"] = HandStateLoggingService.CountsForDraw(entity);
                handleLog["card"] = HandStateLoggingService.BuildCardSnapshot(entity);
            }
            LoggingService.Append("UIElementEventDelegateService.HandleEvent", handleLog);
            switch(type)
            {
				case UIElementEventType.ToggleRumble:
				{
					SaveCache.SetRumbleEnabled(!SaveCache.GetRumbleEnabled());
					break;
				}
				case UIElementEventType.BoosterPackOpeningClose:
				{
					EventManager.Publish(new CloseBoosterPackOpeningOverlayEvent());
					break;
				}
                case UIElementEventType.ConfirmBlocks:
                {
                    EventManager.Publish(new ConfirmBlocksRequested());
                    break;
                }
                case UIElementEventType.UnassignCardAsBlock:
                {
                    EventManager.Publish(new UnassignCardAsBlockRequested { CardEntity = entity });
                    break;
                }
                case UIElementEventType.ActivateEquipment:
                {
                    EventManager.Publish(new ActivateEquipmentRequested { EquipmentEntity = entity });
                    break;
                }
                case UIElementEventType.CardListModalClose:
                {
                    EventManager.Publish(new CloseCardListModalEvent { });
                    break;
                }
                case UIElementEventType.ClimbShopSlotSelect:
                {
                    if (!IsClimbScene(entityManager)) break;
                    if (WouldBlockClickDuringPreview(entity, entityManager)) break;
                    HandleClimbShopSlotSelected(entity, entityManager);
                    break;
                }
                case UIElementEventType.ClimbEncounterSlotSelect:
                {
                    if (!IsClimbScene(entityManager)) break;
                    if (WouldBlockClickDuringPreview(entity, entityManager)) break;
                    var action = entity.GetComponent<ClimbEncounterSlotAction>();
                    if (action != null)
                    {
                        EventManager.Publish(new ClimbEncounterSlotSelectedEvent { SlotId = action.SlotId });
                    }
                    break;
                }
                case UIElementEventType.ClimbEventSlotSelect:
                {
                    if (!IsClimbScene(entityManager)) break;
                    if (WouldBlockClickDuringPreview(entity, entityManager)) break;
                    var action = entity.GetComponent<ClimbEventSlotAction>();
                    if (action != null)
                    {
                        EventManager.Publish(new ClimbEventSlotSelectedEvent { SlotId = action.SlotId });
                    }
                    break;
                }
                case UIElementEventType.QuestSelect:
                {
                    EventManager.Publish(new QuestSelectRequested { Entity = entity });
                    break;
                }
                case UIElementEventType.WayStationDialoguePoiSelect:
                {
                    var action = entity.GetComponent<WayStationDialoguePoiAction>();
                    if (action != null)
                    {
                        EventManager.Publish(new WayStationDialoguePoiSelectedEvent { OfferId = action.OfferId });
                    }
                    break;
                }
                case UIElementEventType.PayCostCancel:
                {
                    EventManager.Publish(new PayCostCancelRequested());
                    break;
                }
                case UIElementEventType.AbandonQuest:
                {
                    EventManager.Publish(new RunEndSequenceRequested { Cause = RunEndCause.Abandon });
                    break;
                }
                case UIElementEventType.SkipTutorial:
                {
                    if (GuidedTutorialService.IsActive(entityManager))
                        EventManager.Publish(new GuidedTutorialSkipRequested());
                    break;
                }
                case UIElementEventType.SkipDialog:
                {
                    EventManager.Publish(new DialogSkipRequested());
                    break;
                }
                case UIElementEventType.OpenLoadout:
                {
                    if (entity.GetComponent<ClimbLoadoutButton>() != null)
                    {
                        if (!IsClimbScene(entityManager)) break;
                        EventManager.Publish(new ClimbLoadoutOpenRequestedEvent());
                    }

                    var deckEntity = RunDeckService.EnsureRunDeck(entityManager);
                    var deck = deckEntity?.GetComponent<Deck>();
                    if (deck?.Cards != null)
                    {
                        bool isClimb = IsClimbScene(entityManager);
                        EventManager.Publish(new OpenCardListModalEvent
                        {
							Title = isClimb ? ClimbOverviewViewService.OverviewTitle : "Loadout",
                            Cards = deck.Cards.ToList(),
                            Mode = isClimb ? CardListModalMode.Inventory : CardListModalMode.CardList,
                        });
                    }
                    break;
                }
                case UIElementEventType.CardClicked:
                {
                    var cardListModal = entityManager.GetEntitiesWithComponent<CardListModal>()
                        .FirstOrDefault()
                        ?.GetComponent<CardListModal>();
                    if (cardListModal?.IsOpen == true && cardListModal.IsSelectable)
                    {
                        var cards = (cardListModal.Cards ?? new List<Entity>())
                            .Where(e => e != null && e.GetComponent<CardData>() != null)
                            .OrderBy(e => e.GetComponent<CardData>().Card.CardId)
                            .ToList();
                        EventManager.Publish(new CardListModalCardSelectedEvent
                        {
                            Card = entity,
                            CardIndex = cards.FindIndex(e => e == entity),
                            SelectionContext = cardListModal.SelectionContext ?? string.Empty,
                        });
                        EventManager.Publish(new CloseCardListModalEvent());
                        break;
                    }

                    var payStateEntity = entityManager.GetEntitiesWithComponent<PayCostOverlayState>().FirstOrDefault();
                    var payState = payStateEntity?.GetComponent<PayCostOverlayState>();
                    if (payState != null && payState.IsOpen)
                    {
                        LoggingService.Append("UIElementEventDelegateService.CardClicked", new System.Text.Json.Nodes.JsonObject
                        {
                            ["branch"] = "PayCostCandidateClicked",
                            ["entityId"] = entity.Id,
                            ["cardId"] = entity.GetComponent<CardData>()?.Card?.CardId ?? "unknown",
                            ["inDeckHand"] = activeDeck?.Hand.Contains(entity) ?? false,
                            ["card"] = HandStateLoggingService.BuildCardSnapshot(entity)
                        });
                        EventManager.Publish(new PayCostCandidateClicked { Card = entity });
                    }
                    else
                    {
                        var phase = entityManager.GetEntitiesWithComponent<PhaseState>()
                            .FirstOrDefault()
                            ?.GetComponent<PhaseState>();
                        bool isHandCard = activeDeck?.Hand.Contains(entity) == true;
                        if (isHandCard && phase?.Sub == SubPhase.Block)
                        {
                            LoggingService.Append("UIElementEventDelegateService.CardClicked", new System.Text.Json.Nodes.JsonObject
                            {
                                ["branch"] = "AssignCardAsBlockRequested",
                                ["entityId"] = entity.Id,
                                ["cardId"] = entity.GetComponent<CardData>()?.Card?.CardId ?? "unknown",
                                ["card"] = HandStateLoggingService.BuildCardSnapshot(entity)
                            });
                            EventManager.Publish(new AssignCardAsBlockRequested { Card = entity });
                            break;
                        }

                        if (phase?.Sub != SubPhase.Action)
                        {
                            break;
                        }

                        LoggingService.Append("UIElementEventDelegateService.CardClicked", new System.Text.Json.Nodes.JsonObject
                        {
                            ["branch"] = "PlayCardRequested",
                            ["entityId"] = entity.Id,
                            ["cardId"] = entity.GetComponent<CardData>()?.Card?.CardId ?? "unknown",
                            ["payStateExists"] = payStateEntity != null,
                            ["inDeckHand"] = activeDeck?.Hand.Contains(entity) ?? false,
                            ["card"] = HandStateLoggingService.BuildCardSnapshot(entity)
                        });
                        EventManager.Publish(new PlayCardRequested { Card = entity });
                    }
                    break;
                }
                case UIElementEventType.EndTurn:
                {
                    EventManager.Publish(new EndTurnRequested());
                    break;
                }
                case UIElementEventType.PledgeCard:
                {
                    var phase = entityManager.GetEntitiesWithComponent<PhaseState>()
                        .FirstOrDefault()
                        ?.GetComponent<PhaseState>();
                    if (phase?.Sub != SubPhase.Action || activeDeck?.Hand.Contains(entity) != true)
                    {
                        break;
                    }
                    EventManager.Publish(new PledgeCardRequested { Card = entity });
                    break;
                }
                case UIElementEventType.AssignCardAsBlock:
                {
                    EventManager.Publish(new AssignCardAsBlockRequested { Card = entity });
                    break;
                }
                case UIElementEventType.ViewDeck:
                {
                    PileViewService.OpenDrawPile(entityManager);
                    break;
                }
                case UIElementEventType.ViewDiscard:
                {
                    PileViewService.OpenDiscardPile(entityManager);
                    break;
                }
                default:
                {
                    if (type != UIElementEventType.None)
                    {
                        Console.WriteLine($"UIElementEventDelegateService: unhandled event type {type} on entity {entity.Id}");
                    }
                    break;
                }
            }
        }

        private static void HandleClimbShopSlotSelected(Entity entity, EntityManager entityManager)
        {
            var action = entity.GetComponent<ClimbShopSlotAction>();
            if (action == null || action.SlotIndex < 0) return;

            var climb = SaveCache.GetClimbState();
            var slot = climb?.shopSlots != null && action.SlotIndex < climb.shopSlots.Count
                ? climb.shopSlots[action.SlotIndex]
                : null;
            if (slot == null || slot.isSold || string.Equals(slot.kind, ClimbShopSlotKinds.Empty, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!string.Equals(slot.kind, ClimbShopSlotKinds.Replacement, StringComparison.OrdinalIgnoreCase))
            {
                // ClimbShopSlotSelectedEvent is published from TryPurchaseSlot on success only.
                ClimbShopService.TryPurchaseSlot(entityManager, action.SlotIndex);
                return;
            }

            if (!ClimbShopService.TryOpenReplacementOffer(action.SlotIndex)) return;

            var eligibleCards = BuildEligibleReplacementCards(entityManager);
            if (eligibleCards.Count == 0)
            {
                ClimbShopService.CancelReplacementOffer();
                return;
            }

            EventManager.Publish(new OpenCardListModalEvent
            {
                Title = "Replace a Card",
                Cards = eligibleCards,
                IsSelectable = true,
                SelectionContext = CardListSelectionContexts.ClimbReplacement,
                Mode = CardListModalMode.CardList,
            });
        }

        private static List<Entity> BuildEligibleReplacementCards(EntityManager entityManager)
        {
            ClearCardListSelectionMetadata(entityManager);

            var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
            var deckEntity = RunDeckService.EnsureRunDeck(entityManager);
            var deck = deckEntity?.GetComponent<Deck>();
			if (loadout?.cards == null || deck?.Cards == null) return new List<Entity>();

            var usedEntityIds = new HashSet<int>();
            var result = new List<Entity>();
			for (int i = 0; i < loadout.cards.Count; i++)
			{
				var entry = loadout.cards[i];
				if (entry == null) continue;
				string cardKey = entry.cardKey;
                if (!ClimbShopService.IsReplacementEligible(cardKey)) continue;

                var card = deck.Cards.FirstOrDefault(e =>
                    e != null
                    && e.IsActive
					&& !usedEntityIds.Contains(e.Id)
					&& string.Equals(e.GetComponent<RunDeckCard>()?.EntryId, entry.entryId, StringComparison.Ordinal));
                if (card == null) continue;

                usedEntityIds.Add(card.Id);
                entityManager.AddComponent(card, new CardListModalSelectionMetadata
				{
					SelectionContext = CardListSelectionContexts.ClimbReplacement,
					EntryId = entry.entryId,
					CardKey = cardKey,
                    SourceIndex = i,
                });
                result.Add(card);
            }

            return result;
        }

        private static void ClearCardListSelectionMetadata(EntityManager entityManager)
        {
            foreach (var entity in entityManager.GetEntitiesWithComponent<CardListModalSelectionMetadata>().ToList())
            {
                entityManager.RemoveComponent<CardListModalSelectionMetadata>(entity);
            }
        }

        private static bool WouldBlockClickDuringPreview(Entity entity, EntityManager entityManager)
        {
            var slot = entity.GetComponent<ClimbSlotPresentation>();
            if (slot == null || string.IsNullOrWhiteSpace(slot.SlotId)) return false;

            var preview = entityManager.GetEntity(ClimbHeaderLayoutSystem.RootName)?.GetComponent<ClimbPreviewState>();
            return preview?.IsActive == true
                && !string.Equals(preview.SourceSlotId, slot.SlotId, StringComparison.OrdinalIgnoreCase)
                && preview.WouldVanishSlotIds.Contains(slot.SlotId);
        }

        private static bool IsClimbScene(EntityManager entityManager)
        {
            return entityManager.GetEntitiesWithComponent<SceneState>()
                .FirstOrDefault()
                ?.GetComponent<SceneState>()
                ?.Current == SceneId.Climb;
        }
    }
}
