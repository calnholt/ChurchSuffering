using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.ECS.Systems
{
    /// <summary>
    /// Queued event that kicks off the assigned-blocks-to-discard animation for the current attack,
    /// then waits until all discard flights have completed.
    /// </summary>
    public class QueuedDiscardAssignedBlocksEvent : EventQueue.IQueuedEvent
    {
        public string Name { get; }
        public object Payload { get; }
        public EventQueue.EventState State { get; set; } = EventQueue.EventState.Pending;

        private readonly EntityManager _entityManager;

        public QueuedDiscardAssignedBlocksEvent(EntityManager entityManager)
        {
            _entityManager = entityManager;
            Name = "Rule.DiscardAssignedBlocks";
            Payload = null;
        }

        public void StartResolving()
        {
            if (!HasAssignedBlocks(_entityManager))
            {
                State = EventQueue.EventState.Complete;
                return;
            }

            bool animate = EnemyAttackFlowService.HasCurrentAttack(_entityManager)
                && !BattleInputGate.IsEnemyDefeated(_entityManager);

            if (animate)
            {
                EventManager.Publish(new DebugCommandEvent { Command = "AnimateAssignedBlocksToDiscard" });
                State = EventQueue.EventState.Waiting;
                return;
            }

            ResolveImmediately(_entityManager, ShouldDiscardSpentBlocks(_entityManager));
            State = EventQueue.EventState.Complete;
        }

        public void Update(float deltaSeconds)
        {
            if (State != EventQueue.EventState.Waiting) return;
            bool anyFlights = _entityManager.GetEntitiesWithComponent<CardToDiscardFlight>()
                .Any(e =>
                {
                    var f = e.GetComponent<CardToDiscardFlight>();
                    return f != null && !f.Completed;
                });
            if (!anyFlights)
            {
                State = EventQueue.EventState.Complete;
            }
        }

        public static bool HasAssignedBlocks(EntityManager entityManager)
        {
            return entityManager != null
                && entityManager.GetEntitiesWithComponent<AssignedBlockCard>().Any();
        }

        public static bool ShouldDiscardSpentBlocks(EntityManager entityManager)
        {
            var phase = entityManager?.GetEntitiesWithComponent<PhaseState>().FirstOrDefault()?.GetComponent<PhaseState>();
            return phase?.Sub == SubPhase.EnemyAttack;
        }

        public static void ResolveImmediately(EntityManager entityManager, bool discardSpentBlocks)
        {
            if (entityManager == null) return;

            var deckEntity = entityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            var assigned = entityManager.GetEntitiesWithComponent<AssignedBlockCard>()
                .OrderBy(e => e.GetComponent<AssignedBlockCard>().AssignedAtTicks)
                .ToList();

            foreach (var entity in assigned)
            {
                if (entity.GetComponent<CardToDiscardFlight>() != null)
                {
                    entityManager.RemoveComponent<CardToDiscardFlight>(entity);
                }

                var abc = entity.GetComponent<AssignedBlockCard>();
                if (abc == null) continue;

                if (abc.IsEquipment || entity.GetComponent<EquippedEquipment>() != null)
                {
                    ResolveEquipmentImmediately(entityManager, entity);
                    continue;
                }

                if (discardSpentBlocks)
                {
                    ResolveSpentCardImmediately(entityManager, deckEntity, entity);
                }
                else
                {
                    ResolveCardToHandImmediately(entityManager, deckEntity, entity);
                }
            }
        }

        private static void ResolveEquipmentImmediately(EntityManager entityManager, Entity entity)
        {
            var zone = entity.GetComponent<EquipmentZone>();
            if (zone == null)
            {
                zone = new EquipmentZone();
                entityManager.AddComponent(entity, zone);
            }
            zone.Zone = EquipmentZoneType.Default;

            var eqComp = entity.GetComponent<EquippedEquipment>();
            if (eqComp != null && !string.IsNullOrEmpty(eqComp.Equipment.Id))
            {
                eqComp.Equipment.MarkUsed();
            }

            try
            {
                if (eqComp != null)
                {
                    if (eqComp.Equipment.Color == CardData.CardColor.Red)
                    {
                        EventManager.Publish(new ModifyCourageRequestEvent { Delta = 1, Type = ModifyCourageType.Gain });
                    }
                    else if (eqComp.Equipment.Color == CardData.CardColor.White)
                    {
                        EventManager.Publish(new ModifyTemperanceEvent { Delta = 1 });
                    }
                }
            }
            catch { }

            entityManager.RemoveComponent<AssignedBlockCard>(entity);
			entityManager.RemoveComponent<AssignedBlockPresentation>(entity);
            CardTransientStateService.ClearAssignedBlockHotKey(entityManager, entity);
            var ui = entity.GetComponent<UIElement>();
            if (ui != null)
            {
                ui.IsHovered = false;
                ui.IsInteractable = true;
                ui.Tooltip = string.Empty;
                ui.EventType = UIElementEventType.None;
            }
        }

        private static void ResolveSpentCardImmediately(
            EntityManager entityManager,
            Entity deckEntity,
            Entity entity)
        {
            var destination = AssignedBlockDestinationService.Resolve(entity);
            entityManager.RemoveComponent<AssignedBlockDestinationOverride>(entity);
            entityManager.RemoveComponent<ExhaustOnBlock>(entity);

            var cardData = entity.GetComponent<CardData>();
            cardData?.Card?.OnBlock?.Invoke(entityManager, entity);
            EventManager.Publish(new CardBlockedEvent { Card = entity });

            CardTransientStateService.ClearAssignedBlockHotKey(entityManager, entity);
			var presentation = entity.GetComponent<AssignedBlockPresentation>();
			if (presentation != null) presentation.Phase = AssignedBlockPresentation.PhaseState.Returning;
            EventManager.Publish(new CardMoveRequested
            {
                Card = entity,
                Deck = deckEntity,
                Destination = destination,
                Reason = AssignedBlockDestinationService.GetMoveReason(destination)
            });
            entityManager.RemoveComponent<AssignedBlockCard>(entity);
			entityManager.RemoveComponent<AssignedBlockPresentation>(entity);
            RestoreTooltipFromBackup(entityManager, entity);
        }

        private static void ResolveCardToHandImmediately(EntityManager entityManager, Entity deckEntity, Entity entity)
        {
            entityManager.RemoveComponent<AssignedBlockDestinationOverride>(entity);
            CardTransientStateService.ClearAssignedBlockHotKey(entityManager, entity);
            EventManager.Publish(new CardMoveRequested
            {
                Card = entity,
                Deck = deckEntity,
                Destination = CardZoneType.Hand,
                Reason = "ReturnAfterAssignment"
            });
        }

        private static void RestoreTooltipFromBackup(EntityManager entityManager, Entity entity)
        {
            var backup = entity.GetComponent<TooltipOverrideBackup>();
            var ui = entity.GetComponent<UIElement>();
            if (backup == null || ui == null) return;

            ui.TooltipType = backup.OriginalType;
            ui.TooltipPosition = backup.OriginalPosition;
            ui.TooltipOffsetPx = backup.OriginalOffsetPx;
            var ct = entity.GetComponent<CardTooltip>();
            if (backup.HadCardTooltip)
            {
                if (ct == null)
                {
                    entityManager.AddComponent(entity, new CardTooltip { CardId = backup.OriginalCardTooltipId ?? string.Empty });
                }
                else
                {
                    ct.CardId = backup.OriginalCardTooltipId ?? string.Empty;
                }
            }
            else if (ct != null)
            {
                entityManager.RemoveComponent<CardTooltip>(entity);
            }
            entityManager.RemoveComponent<TooltipOverrideBackup>(entity);

            if (ui != null)
            {
                ui.Tooltip = string.Empty;
                ui.IsHovered = false;
            }
        }
    }
}
