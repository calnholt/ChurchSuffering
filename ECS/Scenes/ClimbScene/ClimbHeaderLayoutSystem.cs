using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
	public class ClimbHeaderLayoutSystem : Core.System
	{
		public const string RootName = "Climb_Root";
		public const string HeaderName = "Climb_Header";
		public const string TimelineName = "Climb_Timeline";
		public const string ResourceBarName = "Climb_ResourceBar";
		public const string LoadoutButtonName = "Climb_LoadoutButton";

		public ClimbHeaderLayoutSystem(EntityManager entityManager)
			: base(entityManager)
		{
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var scene = entity.GetComponent<SceneState>();
			if (scene?.Current != SceneId.Climb)
			{
				ClimbSceneSystem.DeactivateClimbUiEntities(EntityManager);
				return;
			}

			var preview = EnsurePreviewState();
			EnsureHeaderEntities();
			UpdateBounds();
			UpdatePreview(preview);
		}

		private ClimbPreviewState EnsurePreviewState()
		{
			var root = EnsureEntity(RootName, new ClimbSceneRoot(), e => e.GetComponent<ClimbSceneRoot>() != null);
			var preview = root.GetComponent<ClimbPreviewState>();
			if (preview == null)
			{
				preview = new ClimbPreviewState();
				EntityManager.AddComponent(root, preview);
			}
			return preview;
		}

		private void EnsureHeaderEntities()
		{
			EnsureEntity(HeaderName, new ClimbHeaderElement(), e => e.GetComponent<ClimbHeaderElement>() != null);
			EnsureEntity(TimelineName, new ClimbTimelineElement(), e => e.GetComponent<ClimbTimelineElement>() != null);
			EnsureEntity(ResourceBarName, new ClimbResourceBarElement(), e => e.GetComponent<ClimbResourceBarElement>() != null);
			var loadout = EnsureEntity(LoadoutButtonName, new ClimbLoadoutButton(), e => e.GetComponent<ClimbLoadoutButton>() != null);
			EnsureUi(loadout, UIElementEventType.OpenLoadout, isInteractable: true);
		}

		private void UpdateBounds()
		{
			var headerRect = new Rectangle(0, 0, Game1.VirtualWidth, ClimbHeaderDisplaySystem.HeaderHeightValue);
			SetTransformAndBounds(HeaderName, headerRect, zOrder: 2000, interactable: false);

			int contentX = ClimbHeaderDisplaySystem.HeaderPaddingXValue;
			int contentY = ClimbHeaderDisplaySystem.HeaderPaddingTopValue;
			int contentH = ClimbHeaderDisplaySystem.WeaponButtonSizeValue;
			int weaponSize = ClimbHeaderDisplaySystem.WeaponButtonSizeValue;
			int gap = ClimbHeaderDisplaySystem.HeaderGapValue;
			int weaponX = Game1.VirtualWidth - ClimbHeaderDisplaySystem.HeaderPaddingXValue - weaponSize;
			var loadoutRect = new Rectangle(weaponX, contentY, weaponSize, weaponSize);
			SetTransformAndBounds(LoadoutButtonName, loadoutRect, zOrder: 2100, interactable: true, UIElementEventType.OpenLoadout);

			int resourceW = ClimbHeaderDisplaySystem.ResourceBarWidthValue;
			var resourceRect = new Rectangle(loadoutRect.X - gap - resourceW, contentY, resourceW, contentH);
			SetTransformAndBounds(ResourceBarName, resourceRect, zOrder: 2050, interactable: false);

			int timelineW = resourceRect.X - gap - contentX;
			var timelineRect = new Rectangle(contentX, contentY, timelineW, contentH);
			SetTransformAndBounds(TimelineName, timelineRect, zOrder: 2050, interactable: false);
		}

		private void UpdatePreview(ClimbPreviewState preview)
		{
			var climb = SaveCache.GetClimbState();
			int currentTime = ClimbRuleService.ClampTime(climb?.time ?? 0);
			var hoveredSlot = EntityManager.GetEntitiesWithComponent<ClimbSlotPresentation>()
				.Select(e => new { Entity = e, Slot = e.GetComponent<ClimbSlotPresentation>(), UI = e.GetComponent<UIElement>() })
				.FirstOrDefault(x => x.UI?.IsHovered == true
					&& x.Slot != null
					&& (x.Slot.TimeCost > 0 || x.Slot.Kind == ClimbSlotKind.Event)
					&& !x.Slot.IsUnavailable
					&& !x.Slot.IsCompleted
					&& !x.Slot.IsSold
					&& (x.Slot.Kind != ClimbSlotKind.Shop || x.Slot.IsAffordable));

			if (hoveredSlot == null)
			{
				preview.Clear();
				preview.ProjectedUsedTime = currentTime;
				preview.ProjectedRemainingTime = ClimbRuleService.MaxTime - currentTime;
				preview.ProjectedResources = CloneResources(climb?.resources);
				FillAffordableShopIds(preview, climb);
				return;
			}

			int projected = ClimbRuleService.ClampTime(currentTime + hoveredSlot.Slot.TimeCost);
			preview.IsActive = projected > currentTime || hoveredSlot.Slot.Kind == ClimbSlotKind.Event;
			preview.SourceSlotId = hoveredSlot.Slot.SlotId;
			preview.Amount = projected - currentTime;
			preview.ProjectedUsedTime = projected;
			preview.ProjectedRemainingTime = ClimbRuleService.MaxTime - projected;
			preview.ProjectedResources = CloneResources(climb?.resources);
			if (hoveredSlot.Slot.Kind == ClimbSlotKind.Encounter)
			{
				ClimbRuleService.AddResources(preview.ProjectedResources, hoveredSlot.Slot.Reward);
			}
			else if (hoveredSlot.Slot.Kind == ClimbSlotKind.Event
				&& hoveredSlot.Slot.EventKind == ClimbEventKind.Hazard)
			{
				ClimbRuleService.AddResources(preview.ProjectedResources, hoveredSlot.Slot.Reward);
			}
			else if (hoveredSlot.Slot.Kind == ClimbSlotKind.Shop)
			{
				ClimbRuleService.TrySpend(preview.ProjectedResources, hoveredSlot.Slot.Cost);
			}

			preview.WouldVanishSlotIds.Clear();
			FillWouldVanish(preview, climb, projected);
			FillAffordableShopIds(preview, climb);
		}

		private static void FillWouldVanish(ClimbPreviewState preview, ClimbSaveState climb, int projectedTime)
		{
			if (!string.IsNullOrWhiteSpace(preview.SourceSlotId))
			{
				preview.WouldVanishSlotIds.Add(preview.SourceSlotId);
			}

			int current = ClimbRuleService.ClampTime(climb?.time ?? 0);
			int nextShopRefresh = ((current / ClimbRuleService.ShopRefreshInterval) + 1) * ClimbRuleService.ShopRefreshInterval;
			if (projectedTime >= nextShopRefresh)
			{
				foreach (var slot in climb?.shopSlots ?? new List<ClimbShopSlotSave>())
				{
					if (!string.IsNullOrWhiteSpace(slot?.id)) preview.WouldVanishSlotIds.Add(slot.id);
				}
			}

			foreach (var slot in climb?.eventSlots ?? new List<ClimbEventSlotSave>())
			{
				if (slot == null || slot.status != ClimbEventStatus.Active || slot.activatedAtTime < 0) continue;
				if (projectedTime >= slot.activatedAtTime + Math.Max(0, slot.duration) && !string.IsNullOrWhiteSpace(slot.id))
				{
					preview.WouldVanishSlotIds.Add(slot.id);
				}
			}

			foreach (var slot in climb?.encounterSlots ?? new List<ClimbEncounterSlotSave>())
			{
				if (slot == null || slot.isCompleted || slot.isFinal) continue;
				if (ClimbRuleService.IsEncounterExpired(slot, projectedTime) && !string.IsNullOrWhiteSpace(slot.id))
				{
					preview.WouldVanishSlotIds.Add(slot.id);
				}
			}
		}

		private static void FillAffordableShopIds(ClimbPreviewState preview, ClimbSaveState climb)
		{
			preview.AffordableShopSlotIds.Clear();
			foreach (var slot in climb?.shopSlots ?? new List<ClimbShopSlotSave>())
			{
				if (slot == null || slot.isSold || string.IsNullOrWhiteSpace(slot.id)) continue;
				if (ClimbRuleService.CanAfford(preview.ProjectedResources, slot.cost))
				{
					preview.AffordableShopSlotIds.Add(slot.id);
				}
			}
		}

		private Entity EnsureEntity<T>(string name, T marker, System.Func<Entity, bool> hasMarker)
			where T : class, IComponent
		{
			var entity = EntityManager.GetEntity(name);
			if (entity == null)
			{
				entity = EntityManager.CreateEntity(name);
				EntityManager.AddComponent(entity, new Transform());
				EntityManager.AddComponent(entity, new OwnedByScene { Scene = SceneId.Climb });
			}

			if (!hasMarker(entity))
			{
				EntityManager.AddComponent(entity, marker);
			}

			return entity;
		}

		private void EnsureUi(Entity entity, UIElementEventType eventType, bool isInteractable)
		{
			var ui = entity.GetComponent<UIElement>();
			if (ui == null)
			{
				EntityManager.AddComponent(entity, new UIElement { EventType = eventType, IsInteractable = isInteractable });
			}
			else
			{
				ui.EventType = eventType;
				ui.IsInteractable = isInteractable;
			}
		}

		private void SetTransformAndBounds(string entityName, Rectangle rect, int zOrder, bool interactable, UIElementEventType eventType = UIElementEventType.None)
		{
			var entity = EntityManager.GetEntity(entityName);
			if (entity == null) return;
			var transform = entity.GetComponent<Transform>();
			if (transform != null)
			{
				transform.Position = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
				transform.ZOrder = zOrder;
			}

			var ui = entity.GetComponent<UIElement>();
			if (ui == null)
			{
				EntityManager.AddComponent(entity, new UIElement { Bounds = rect, IsInteractable = interactable, EventType = eventType });
			}
			else
			{
				ui.Bounds = rect;
				ui.IsInteractable = interactable;
				ui.IsHidden = false;
				ui.EventType = eventType;
			}
		}

		private static ClimbResourceSave CloneResources(ClimbResourceSave resources)
		{
			return new ClimbResourceSave
			{
				red = resources?.red ?? 0,
				white = resources?.white ?? 0,
				black = resources?.black ?? 0,
			};
		}
	}
}
