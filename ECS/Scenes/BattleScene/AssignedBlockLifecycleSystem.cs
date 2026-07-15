using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// Owns assigned-block interaction, UI lifecycle, hotkeys, and zone-return requests.
	/// Presentation systems own animation and rendering only.
	/// </summary>
	public sealed class AssignedBlockLifecycleSystem : Core.System
	{
		public AssignedBlockLifecycleSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<UnassignCardAsBlockRequested>(OnUnassignRequested);
			EventManager.Subscribe<BlockAssignmentAdded>(OnAssignmentAdded);
			EventManager.Subscribe<BlockAssignmentRemoved>(OnAssignmentRemoved);
			EventManager.Subscribe<AssignedBlockReturnCompleted>(OnReturnCompleted);
			EventManager.Subscribe<CardMoved>(OnCardMoved);
		}

		protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			ReconcileHotKey();
		}

		private void OnAssignmentAdded(BlockAssignmentAdded evt)
		{
			var card = evt?.Card;
			var assignment = card?.GetComponent<AssignedBlockCard>();
			if (card == null || assignment == null) return;

			EnsurePresentation(card);
			ConfigureAssignedUi(card, assignment);
			ReconcileHotKey();
		}

		private void OnUnassignRequested(UnassignCardAsBlockRequested evt)
		{
			var card = evt?.CardEntity;
			var assignment = card?.GetComponent<AssignedBlockCard>();
			var presentation = card?.GetComponent<AssignedBlockPresentation>();
			if (card == null || assignment == null || presentation == null) return;
			if (presentation.Phase == AssignedBlockPresentation.PhaseState.Returning) return;
			if (BattleInputGate.IsBattleInputFrozen(EntityManager)) return;

			LoggingService.Append(
				"AssignedBlockLifecycleSystem.OnUnassignRequested",
				new System.Text.Json.Nodes.JsonObject { ["entityId"] = card.Id });

			if (presentation.RenderPos != Vector2.Zero)
			{
				presentation.CurrentPos = presentation.RenderPos;
			}
			presentation.StartPos = presentation.CurrentPos;
			presentation.StartScale = presentation.CurrentScale;
			presentation.StartRotation = presentation.CurrentRotation;
			presentation.Phase = AssignedBlockPresentation.PhaseState.Returning;
			presentation.Elapsed = 0f;
			presentation.ReturnCompletionPublished = false;

			var ui = card.GetComponent<UIElement>();
			if (ui != null)
			{
				ui.IsInteractable = false;
				ui.IsHovered = false;
				ui.IsClicked = false;
			}

			if (!assignment.IsEquipment)
			{
				EventManager.Publish(new ReserveAssignedBlockReturnRequested
				{
					Card = card,
					Deck = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault(),
				});
			}

			ReconcileHotKey();
			EventManager.Publish(new BlockAssignmentRemoved
			{
				Card = card,
				DeltaBlock = -assignment.BlockAmount,
				Colors = assignment.ColorKeys,
			});
			EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.Equip, Volume = 0.5f, Pitch = -0.5f });
		}

		private void OnAssignmentRemoved(BlockAssignmentRemoved evt)
		{
			if (evt?.Card == null) return;
			RestoreTooltip(evt.Card);
			ReconcileHotKey();
		}

		private void OnReturnCompleted(AssignedBlockReturnCompleted evt)
		{
			var card = evt?.Card;
			var assignment = card?.GetComponent<AssignedBlockCard>();
			if (card == null || assignment == null) return;

			EntityManager.RemoveComponent<AssignedBlockPresentation>(card);
			if (assignment.IsEquipment)
			{
				EntityManager.RemoveComponent<AssignedBlockCard>(card);
				var zone = card.GetComponent<EquipmentZone>();
				if (zone == null)
				{
					zone = new EquipmentZone();
					EntityManager.AddComponent(card, zone);
				}
				zone.Zone = EquipmentZoneType.Default;
				ResetStableUi(card, UIElementEventType.None);
			}
			else
			{
				EventManager.Publish(new CardMoveRequested
				{
					Card = card,
					Deck = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault(),
					Destination = CardZoneType.Hand,
					Reason = "ReturnAfterAssignment",
				});
			}
			ReconcileHotKey();
		}

		private void OnCardMoved(CardMoved evt)
		{
			if (evt?.Card == null || evt.From != CardZoneType.AssignedBlock) return;
			if (evt.Card.GetComponent<AssignedBlockCard>() == null)
			{
				EntityManager.RemoveComponent<AssignedBlockPresentation>(evt.Card);
			}
			ReconcileHotKey();
		}

		private void EnsurePresentation(Entity card)
		{
			if (card.GetComponent<AssignedBlockPresentation>() != null) return;
			var transform = card.GetComponent<Transform>();
			var position = transform?.Position ?? Vector2.Zero;
			float scale = transform?.Scale.X ?? 1f;
			EntityManager.AddComponent(card, new AssignedBlockPresentation
			{
				StartPos = position,
				CurrentPos = position,
				TargetPos = position,
				StartScale = scale,
				CurrentScale = scale,
				StartRotation = transform?.Rotation ?? 0f,
				CurrentRotation = transform?.Rotation ?? 0f,
				Phase = AssignedBlockPresentation.PhaseState.Pullback,
			});
		}

		private void ConfigureAssignedUi(Entity card, AssignedBlockCard assignment)
		{
			var ui = card.GetComponent<UIElement>();
			if (ui == null)
			{
				ui = new UIElement();
				EntityManager.AddComponent(card, ui);
			}
			ui.IsInteractable = false;
			ui.IsHovered = false;
			ui.IsClicked = false;
			ui.EventType = UIElementEventType.UnassignCardAsBlock;
			ui.LayerType = UILayerType.Default;

			if (assignment.IsEquipment)
			{
				ui.Tooltip = string.Empty;
				ui.TooltipType = TooltipType.Equipment;
				ui.TooltipPosition = TooltipPosition.Above;
				ui.TooltipOffsetPx = 10;
				return;
			}

			var backup = card.GetComponent<TooltipOverrideBackup>();
			if (backup == null)
			{
				var existingTooltip = card.GetComponent<CardTooltip>();
				EntityManager.AddComponent(card, new TooltipOverrideBackup
				{
					OriginalType = ui.TooltipType,
					OriginalPosition = ui.TooltipPosition,
					OriginalOffsetPx = ui.TooltipOffsetPx,
					HadCardTooltip = existingTooltip != null,
					OriginalCardTooltipId = existingTooltip?.CardId ?? string.Empty,
				});
			}

			ui.Tooltip = string.Empty;
			ui.TooltipType = TooltipType.Card;
			ui.TooltipPosition = TooltipPosition.Below;
			ui.TooltipOffsetPx = 10;
			var cardId = card.GetComponent<CardData>()?.Card?.CardId ?? string.Empty;
			var cardTooltip = card.GetComponent<CardTooltip>();
			if (cardTooltip == null) EntityManager.AddComponent(card, new CardTooltip { CardId = cardId });
			else cardTooltip.CardId = cardId;
		}

		private void RestoreTooltip(Entity card)
		{
			var backup = card?.GetComponent<TooltipOverrideBackup>();
			var ui = card?.GetComponent<UIElement>();
			if (backup == null || ui == null) return;

			ui.TooltipType = backup.OriginalType;
			ui.TooltipPosition = backup.OriginalPosition;
			ui.TooltipOffsetPx = backup.OriginalOffsetPx;
			var cardTooltip = card.GetComponent<CardTooltip>();
			if (backup.HadCardTooltip)
			{
				if (cardTooltip == null)
					EntityManager.AddComponent(card, new CardTooltip { CardId = backup.OriginalCardTooltipId ?? string.Empty });
				else
					cardTooltip.CardId = backup.OriginalCardTooltipId ?? string.Empty;
			}
			else if (cardTooltip != null)
			{
				EntityManager.RemoveComponent<CardTooltip>(card);
			}
			EntityManager.RemoveComponent<TooltipOverrideBackup>(card);
		}

		private static void ResetStableUi(Entity card, UIElementEventType eventType)
		{
			var ui = card.GetComponent<UIElement>();
			if (ui == null) return;
			ui.IsInteractable = true;
			ui.IsHovered = false;
			ui.IsClicked = false;
			ui.EventType = eventType;
			ui.Tooltip = string.Empty;
		}

		private void ReconcileHotKey()
		{
			var candidates = (EnemyAttackFlowService.HasCurrentAttack(EntityManager)
					? EntityManager.GetEntitiesWithComponent<AssignedBlockCard>()
					: Enumerable.Empty<Entity>())
				.Where(entity => entity.GetComponent<AssignedBlockPresentation>()?.Phase == AssignedBlockPresentation.PhaseState.Idle)
				.OrderBy(entity => entity.GetComponent<AssignedBlockCard>().AssignedAtTicks)
				.ToList();
			var newest = candidates.LastOrDefault();

			foreach (var entity in EntityManager.GetEntitiesWithComponent<AssignedBlockCard>().ToList())
			{
				var hotKey = entity.GetComponent<HotKey>();
				if (entity == newest)
				{
					if (hotKey == null)
						EntityManager.AddComponent(entity, new HotKey
						{
							Button = FaceButton.B,
							Position = HotKeyPosition.Top,
							IsKeyboardMouseEnabled = false,
						});
					else
					{
						hotKey.Button = FaceButton.B;
						hotKey.Position = HotKeyPosition.Top;
						hotKey.IsKeyboardMouseEnabled = false;
					}
				}
				else if (hotKey?.Button == FaceButton.B)
				{
					EntityManager.RemoveComponent<HotKey>(entity);
				}
			}
		}
	}
}
