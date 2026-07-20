using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using System;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// Handles clicking equipped equipment during Block or Action sub-phases.
	/// In Block: assigns as block. In Action: activates if ability exists.
	/// </summary>
	public class EquipmentBlockInteractionSystem : Core.System
	{
		public EquipmentBlockInteractionSystem(EntityManager entityManager) : base(entityManager) { }

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<EquippedEquipment>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (BattleInputGate.IsBattleInputFrozen(EntityManager)) return;
			// Only in Block or Action phase
			var phase = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault()?.GetComponent<PhaseState>();
			if (phase == null) return;
			bool isBlockPhase = phase.Sub == SubPhase.Block;
			bool isActionPhase = phase.Sub == SubPhase.Action;
			if (!isBlockPhase && !isActionPhase) return;
			// Need current context during Block phase
			var enemy = EntityManager.GetEntitiesWithComponent<AttackIntent>().FirstOrDefault();
			var plannedAttack = enemy?.GetComponent<AttackIntent>()?.Planned?.FirstOrDefault();
			// Clicks now come from UIElement.IsClicked on equipment UI elements

			// Iterate equipment visible in panel (Default zone), topmost first (Z desc just in case)
			var equipEntities = EntityManager.GetEntitiesWithComponent<EquippedEquipment>()
				.Where(e => (e.GetComponent<EquipmentZone>()?.Zone ?? EquipmentZoneType.Default) == EquipmentZoneType.Default)
				.OrderByDescending(e => e.GetComponent<Transform>()?.ZOrder ?? 0)
				.ToList();
			foreach (var eqEntity in equipEntities)
			{
				var ui = eqEntity.GetComponent<UIElement>();
				var comp = eqEntity.GetComponent<EquippedEquipment>();
				if (ui == null || comp == null) continue;
				if (!ui.IsInteractable || !ui.IsClicked) continue;
				ui.IsClicked = false;

				if (isBlockPhase)
				{
					if (!comp.Equipment.IsAvailable)
					{
						PublishInvalidClick("This equipment has already been used this battle!");
						break;
					}
					if (comp.Equipment.Block <= 0)
					{
						PublishInvalidClick("This equipment cannot block!");
						break;
					}
					if (plannedAttack == null)
					{
						PublishInvalidClick("There is no attack to block!");
						break;
					}
					// Existing assign as block behavior
					// Lookup block value and color from definition
					int blockVal = 0;
					string color = "White";
					try
					{
						if (comp.Equipment.Block > 0)
						{
							blockVal = System.Math.Max(0, comp.Equipment.Block);
							color = comp.Equipment.Color.ToString();
						}
					}
					catch { }
					if (blockVal <= 0)
					{
						PublishInvalidClick("This equipment cannot block!");
						break;
					}
					if (!EnemyBlockerEligibilityService.IsEligibleEquipmentBlocker(EntityManager, eqEntity, plannedAttack))
					{
						var restriction = plannedAttack?.AttackDefinition?.BlockingRestrictionType ?? BlockingRestrictionType.None;
						var message = restriction == BlockingRestrictionType.None
							? "This equipment cannot block this attack!"
							: EnemyAttackTextHelper.GetBlockingRestrictionText(restriction);
						if (message.EndsWith(".")) message = message.Substring(0, message.Length - 1) + "!";
						PublishInvalidClick(message);
						break;
					}

					var t = eqEntity.GetComponent<Transform>();
					var uiElem = eqEntity.GetComponent<UIElement>();
					var panelBounds = uiElem == null
						? Rectangle.Empty
						: TransformResolverService.ResolveUIBounds(EntityManager, eqEntity, uiElem);
					var resolvedPanelCenter = panelBounds.Width > 0 && panelBounds.Height > 0
						? new Vector2(panelBounds.Center.X, panelBounds.Center.Y)
						: TransformResolverService.ResolveWorldPosition(EntityManager, eqEntity);
					var zone = eqEntity.GetComponent<EquipmentZone>();
					if (zone == null)
					{
						zone = new EquipmentZone { Zone = EquipmentZoneType.AssignedBlock };
						EntityManager.AddComponent(eqEntity, zone);
					}
					else
					{
						zone.Zone = EquipmentZoneType.AssignedBlock;
					}
					var panelCenter = zone.LastPanelCenter != Vector2.Zero
						? zone.LastPanelCenter
						: resolvedPanelCenter;
					if (zone.LastPanelCenter == Vector2.Zero && panelCenter != Vector2.Zero)
					{
						zone.LastPanelCenter = panelCenter;
					}
					if (eqEntity.HasComponent<ParentTransform>())
					{
						EntityManager.RemoveComponent<ParentTransform>(eqEntity);
					}
					if (uiElem != null && panelBounds != Rectangle.Empty)
					{
						uiElem.Bounds = panelBounds;
					}
					if (t == null)
					{
						t = new Transform();
						EntityManager.AddComponent(eqEntity, t);
					}
					t.Position = panelCenter;
					t.Scale = Vector2.One;
					// Create AssignedBlockCard animation state
					var abc = eqEntity.GetComponent<AssignedBlockCard>();
					if (abc == null)
					{
						var equipZone = eqEntity.GetComponent<EquipmentZone>();
						var returnPos = (equipZone != null && equipZone.LastPanelCenter != Vector2.Zero) ? equipZone.LastPanelCenter : panelCenter;
						abc = new AssignedBlockCard { BlockAmount = blockVal, AssignedAtTicks = System.DateTime.UtcNow.Ticks, IsEquipment = true, ColorKeys = [comp.Equipment.Color], Tooltip = BuildEquipmentTooltip(comp), DisplayBgColor = ResolveEquipmentBgColor(color), DisplayFgColor = ResolveFgForBg(ResolveEquipmentBgColor(color)), ReturnTargetPos = returnPos, EquipmentType = comp.Equipment.Slot.ToString() };
						EntityManager.AddComponent(eqEntity, abc);
					}
					else
					{
						var equipZone = eqEntity.GetComponent<EquipmentZone>();
						var returnPos = (equipZone != null && equipZone.LastPanelCenter != Vector2.Zero) ? equipZone.LastPanelCenter : panelCenter;
						abc.BlockAmount = blockVal; abc.AssignedAtTicks = System.DateTime.UtcNow.Ticks; abc.IsEquipment = true; abc.ColorKeys = [comp.Equipment.Color]; abc.Tooltip = BuildEquipmentTooltip(comp); abc.DisplayBgColor = ResolveEquipmentBgColor(color); abc.DisplayFgColor = ResolveFgForBg(abc.DisplayBgColor); abc.ReturnTargetPos = returnPos; abc.EquipmentType = comp.Equipment.Slot.ToString();
					}
					var assignedPresentation = eqEntity.GetComponent<AssignedBlockPresentation>();
					if (assignedPresentation == null)
					{
						EntityManager.AddComponent(eqEntity, new AssignedBlockPresentation
						{
							StartPos = t.Position,
							CurrentPos = t.Position,
							TargetPos = t.Position,
							StartScale = t.Scale.X,
							CurrentScale = t.Scale.X,
							Phase = AssignedBlockPresentation.PhaseState.Pullback,
						});
					}
					else
					{
						assignedPresentation.StartPos = t.Position;
						assignedPresentation.CurrentPos = t.Position;
						assignedPresentation.TargetPos = t.Position;
						assignedPresentation.StartScale = t.Scale.X;
						assignedPresentation.CurrentScale = t.Scale.X;
						assignedPresentation.Phase = AssignedBlockPresentation.PhaseState.Pullback;
						assignedPresentation.Elapsed = 0f;
						assignedPresentation.ReturnCompletionPublished = false;
					}
					EventManager.Publish(new BlockAssignmentAdded { Card = eqEntity, DeltaBlock = blockVal, Colors = [comp.Equipment.Color] });
					EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.Equip, Volume = 0.5f });
					// Mark the assigned equipment to unassign via delegate when clicked on its assigned tile
					{
						var uiAssigned = eqEntity.GetComponent<UIElement>();
						if (uiAssigned == null)
						{
							uiAssigned = new UIElement { IsInteractable = true };
							EntityManager.AddComponent(eqEntity, uiAssigned);
						}
						uiAssigned.EventType = UIElementEventType.UnassignCardAsBlock;
						uiAssigned.IsInteractable = true;
						// Ensure no lingering hotkey when initially assigning equipment (will be given to newest idle later)
						var hk = eqEntity.GetComponent<HotKey>();
						if (hk != null) { EntityManager.RemoveComponent<HotKey>(eqEntity); }
					}
					break;
				}

				if (isActionPhase)
				{
					if (!comp.Equipment.CanActivateDuringActionPhase)
					{
						PublishInvalidClick("This equipment cannot be activated during the Action phase!");
						break;
					}
					if (!comp.Equipment.IsAvailable)
					{
						PublishInvalidClick("This equipment has already been used this battle!");
						break;
					}
					if (!comp.Equipment.CanActivate())
					{
						comp.Equipment.CantActivateMessage();
						break;
					}
					comp.Equipment.EmitActivateEvent();
					break;
				}
			}

			// No raw mouse state tracking needed anymore
		}

		private static void PublishInvalidClick(string message)
		{
			EventManager.Publish(new CantPlayCardMessage { Message = message });
		}
		private static Microsoft.Xna.Framework.Color ResolveEquipmentBgColor(string c)
		{
			switch ((c ?? "").Trim().ToLowerInvariant())
			{
				case "red": return Microsoft.Xna.Framework.Color.DarkRed;
				case "black": return Microsoft.Xna.Framework.Color.Black;
				case "white": return Microsoft.Xna.Framework.Color.White;
				default: return Microsoft.Xna.Framework.Color.Gray;
			}
		}

		private static Microsoft.Xna.Framework.Color ResolveFgForBg(Microsoft.Xna.Framework.Color bg)
		{
			return (bg == Microsoft.Xna.Framework.Color.White) ? Microsoft.Xna.Framework.Color.Black : Microsoft.Xna.Framework.Color.White;
		}

		private static string BuildEquipmentTooltip(EquippedEquipment item)
		{
			try
			{
				if (item.Equipment != null)
				{
					string name = string.IsNullOrWhiteSpace(item.Equipment.Name) ? item.Equipment.Id : item.Equipment.Name;
					return name;
				}
			}
			catch { }
			return item.Equipment.Id ?? string.Empty;
		}
	}
}
