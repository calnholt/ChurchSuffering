using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Dialog;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("WayStation Dialogue")]
	public class WayStationDialogueSystem : Core.System
	{
		private const int ZOrder = 1220;
		private readonly SpriteBatch _spriteBatch;
		private readonly Texture2D _dialoguePoiTexture;
		private readonly Texture2D _pixel;
		private readonly Random _rng = new();

		private WayStationArrivalKind _arrivalKind = WayStationArrivalKind.Initial;
		private bool _visitEvaluated;
		private Guid _pendingRequestId = Guid.Empty;
		private string _pendingOfferId = string.Empty;
		private string _pendingCharacterId = string.Empty;
		private string _pendingSegmentId = string.Empty;

		[DebugEditable(DisplayName = "Keeper POI Screen X", Step = 2, Min = 0, Max = 1920)]
		public float KeeperPoiScreenX { get; set; } = 832f;
		[DebugEditable(DisplayName = "Keeper POI Screen Y", Step = 2, Min = 0, Max = 1080)]
		public float KeeperPoiScreenY { get; set; } = 338f;
		[DebugEditable(DisplayName = "Rook Tutorial POI Screen X", Step = 2, Min = 0, Max = 1920)]
		public float RookTutorialPoiScreenX { get; set; } = 1632f;
		[DebugEditable(DisplayName = "Rook Tutorial POI Screen Y", Step = 2, Min = 0, Max = 1080)]
		public float RookTutorialPoiScreenY { get; set; } = 546f;
		[DebugEditable(DisplayName = "POI Icon Size", Step = 2, Min = 24, Max = 220)]
		public int PoiIconSize { get; set; } = 92;
		[DebugEditable(DisplayName = "POI Hover Scale", Step = 0.01f, Min = 1f, Max = 2f)]
		public float PoiHoverScale { get; set; } = 1.14f;
		[DebugEditable(DisplayName = "Show NPC Placement Area")]
		public bool ShowNpcPlacementArea { get; set; } = false;
		[DebugEditable(DisplayName = "NPC Area Left Padding", Step = 2, Min = 0, Max = 1800)]
		public int NpcAreaLeftPadding { get; set; } = 274;
		[DebugEditable(DisplayName = "NPC Area Right Padding", Step = 2, Min = 0, Max = 1800)]
		public int NpcAreaRightPadding { get; set; } = 496;
		[DebugEditable(DisplayName = "NPC Area Top Padding", Step = 2, Min = 0, Max = 1000)]
		public int NpcAreaTopPadding { get; set; } = 492;
		[DebugEditable(DisplayName = "NPC Area Bottom Padding", Step = 2, Min = 0, Max = 1000)]
		public int NpcAreaBottomPadding { get; set; } = 82;

		public WayStationDialogueSystem(
			EntityManager entityManager,
			SpriteBatch spriteBatch,
			ImageAssetService imageAssets)
			: base(entityManager)
		{
			_spriteBatch = spriteBatch;
			_dialoguePoiTexture = imageAssets.GetRequiredTexture("waystation/dialogue-poi");
			_pixel = imageAssets.GetPixel(Color.White);
			EventManager.Subscribe<LoadSceneEvent>(OnLoadScene);
			EventManager.Subscribe<TransitionCompleteEvent>(OnTransitionComplete);
			EventManager.Subscribe<WayStationDialoguePoiSelectedEvent>(OnDialoguePoiSelected);
			EventManager.Subscribe<DialogueSequenceCompleted>(OnDialogueSequenceCompleted);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var scene = entity.GetComponent<SceneState>();
			if (!IsWayStationScene(scene))
			{
				HidePoi(WayStationSceneConstants.KeeperDialoguePoiName);
				HidePoi(WayStationSceneConstants.NpcDialoguePoiName);
				HidePoi(WayStationSceneConstants.RookTutorialDialoguePoiName);
				return;
			}

			if (!_visitEvaluated && _pendingRequestId == Guid.Empty)
			{
				EnsureVisitEvaluated();
			}

			SyncVisitPois();
		}

		public void Draw()
		{
			var scene = EntityManager.GetEntitiesWithComponent<SceneState>()
				.FirstOrDefault()
				?.GetComponent<SceneState>();
			if (!IsWayStationScene(scene)) return;

			DrawNpcPlacementArea();
			DrawPoi(WayStationSceneConstants.KeeperDialoguePoiName);
			DrawPoi(WayStationSceneConstants.RookTutorialDialoguePoiName);
			DrawPoi(WayStationSceneConstants.NpcDialoguePoiName);
		}

		private void OnLoadScene(LoadSceneEvent evt)
		{
			if (evt?.Scene != SceneId.WayStation) return;
			_arrivalKind = WayStationArrivalContextService.Consume(EntityManager);
			_visitEvaluated = false;
			_pendingRequestId = Guid.Empty;
			_pendingOfferId = string.Empty;
			_pendingCharacterId = string.Empty;
			_pendingSegmentId = string.Empty;
		}

		private void OnTransitionComplete(TransitionCompleteEvent evt)
		{
			if (evt?.Scene != SceneId.WayStation) return;
			var auto = WayStationDialoguePlanner.TryGetAutoDialogue(SaveCache.GetWayStationMeta());
			if (auto != null)
			{
				RequestDialogue(auto.OfferId, auto.CharacterId, auto.DefinitionId, auto.SegmentId);
				return;
			}

			EnsureVisitEvaluated();
		}

		private void OnDialoguePoiSelected(WayStationDialoguePoiSelectedEvent evt)
		{
			if (evt == null || string.IsNullOrWhiteSpace(evt.OfferId)) return;
			if (!IsWayStationActive() || IsDialogueActive()) return;

			var visit = SaveCache.GetWayStationVisit();
			var offer = visit?.offers?.FirstOrDefault(candidate =>
				candidate != null
				&& candidate.visible
				&& string.Equals(candidate.offerId, evt.OfferId, StringComparison.OrdinalIgnoreCase));
			if (offer == null) return;

			RequestDialogue(offer.offerId, offer.characterId, offer.definitionId, offer.segmentId);
		}

		private void OnDialogueSequenceCompleted(DialogueSequenceCompleted evt)
		{
			if (evt == null || _pendingRequestId == Guid.Empty || evt.RequestId != _pendingRequestId) return;

			SaveCache.MarkWayStationDialogueSegmentSeen(_pendingCharacterId, _pendingSegmentId);

			if (!string.Equals(_pendingOfferId, "keeper_auto", StringComparison.OrdinalIgnoreCase))
			{
				var visit = SaveCache.GetWayStationVisit();
				if (visit?.offers != null)
				{
					foreach (var offer in visit.offers)
					{
						if (offer == null) continue;
						if (string.Equals(offer.offerId, _pendingOfferId, StringComparison.OrdinalIgnoreCase))
						{
							offer.visible = false;
						}
					}
					SaveCache.SaveWayStationVisit(visit);
				}
			}
			else
			{
				_visitEvaluated = false;
				EnsureVisitEvaluated();
			}

			_pendingRequestId = Guid.Empty;
			_pendingOfferId = string.Empty;
			_pendingCharacterId = string.Empty;
			_pendingSegmentId = string.Empty;
		}

		private void RequestDialogue(string offerId, string characterId, string definitionId, string segmentId)
		{
			if (string.IsNullOrWhiteSpace(definitionId) || string.IsNullOrWhiteSpace(segmentId)) return;
			_pendingRequestId = Guid.NewGuid();
			_pendingOfferId = offerId ?? string.Empty;
			_pendingCharacterId = characterId ?? string.Empty;
			_pendingSegmentId = segmentId ?? string.Empty;
			EventManager.Publish(new DialogueSequenceRequested
			{
				DefinitionId = definitionId,
				SegmentId = segmentId,
				RequestId = _pendingRequestId,
				BackgroundOnly = false,
			});
		}

		private void EnsureVisitEvaluated()
		{
			if (_visitEvaluated) return;

			var visit = SaveCache.GetWayStationVisit();
			if (IsNewVisitArrival(_arrivalKind))
			{
				visit = BuildNewVisit(_arrivalKind);
				SaveCache.SaveWayStationVisit(visit);
			}
			else if (visit?.initialized != true)
			{
				visit = BuildNewVisit(_arrivalKind);
				SaveCache.SaveWayStationVisit(visit);
			}

			_visitEvaluated = true;
		}

		private WayStationVisitSave BuildNewVisit(WayStationArrivalKind arrivalKind)
		{
			var visit = new WayStationVisitSave { initialized = true };
			var offers = WayStationDialoguePlanner.BuildVisit(SaveCache.GetWayStationMeta(), arrivalKind, _rng);
			bool queuedNpcOffer = false;
			foreach (var offer in offers)
			{
				var position = GetOfferPosition(offer.OfferId);
				visit.offers.Add(new WayStationDialogueOfferSave
				{
					offerId = offer.OfferId,
					characterId = offer.CharacterId,
					definitionId = offer.DefinitionId,
					segmentId = offer.SegmentId,
					screenX = position.X,
					screenY = position.Y,
					visible = true,
				});
				if (string.Equals(offer.OfferId, WayStationDialoguePlanner.NpcOfferId, StringComparison.OrdinalIgnoreCase))
				{
					queuedNpcOffer = true;
				}
			}

			if (queuedNpcOffer)
			{
				SaveCache.MarkWayStationNpcDialogueOfferQueued();
			}

			return visit;
		}

		private Vector2 GetOfferPosition(string offerId)
		{
			if (string.Equals(offerId, WayStationDialoguePlanner.KeeperOfferId, StringComparison.OrdinalIgnoreCase))
			{
				return new Vector2(KeeperPoiScreenX, KeeperPoiScreenY);
			}

			if (string.Equals(offerId, WayStationDialoguePlanner.RookTutorialOfferId, StringComparison.OrdinalIgnoreCase))
			{
				return new Vector2(RookTutorialPoiScreenX, RookTutorialPoiScreenY);
			}

			var area = GetNpcPlacementArea();
			float minX = area.Left;
			float maxX = area.Right;
			float minY = area.Top;
			float maxY = area.Bottom;
			return new Vector2(
				Lerp(minX, maxX, (float)_rng.NextDouble()),
				Lerp(minY, maxY, (float)_rng.NextDouble()));
		}

		private void SyncVisitPois()
		{
			var visit = SaveCache.GetWayStationVisit();
			var offers = visit?.offers ?? new List<WayStationDialogueOfferSave>();
			SyncOfferPoi(
				WayStationSceneConstants.KeeperDialoguePoiName,
				offers.FirstOrDefault(offer => string.Equals(offer?.offerId, WayStationDialoguePlanner.KeeperOfferId, StringComparison.OrdinalIgnoreCase)));
			SyncOfferPoi(
				WayStationSceneConstants.RookTutorialDialoguePoiName,
				offers.FirstOrDefault(offer => string.Equals(offer?.offerId, WayStationDialoguePlanner.RookTutorialOfferId, StringComparison.OrdinalIgnoreCase)));
			SyncOfferPoi(
				WayStationSceneConstants.NpcDialoguePoiName,
				offers.FirstOrDefault(offer => string.Equals(offer?.offerId, WayStationDialoguePlanner.NpcOfferId, StringComparison.OrdinalIgnoreCase)));
		}

		private void SyncOfferPoi(string entityName, WayStationDialogueOfferSave offer)
		{
			bool visible = offer?.visible == true && !IsClimbModalOpen() && !IsDialogueActive();
			if (offer == null)
			{
				HidePoi(entityName);
				return;
			}

			var center = GetOfferCenter(offer);
			var rect = GetPoiBounds(entityName, center.X, center.Y);
			var entity = EntityManager.GetEntity(entityName);
			if (entity == null)
			{
				entity = EntityManager.CreateEntity(entityName);
				EntityManager.AddComponent(entity, new Transform());
				EntityManager.AddComponent(entity, new UIElement
				{
					TooltipType = TooltipType.None,
					EventType = UIElementEventType.WayStationDialoguePoiSelect,
				});
				EntityManager.AddComponent(entity, new PointOfInterest { Type = PointOfInterestType.Event });
				EntityManager.AddComponent(entity, new POITitleTooltipSource());
				EntityManager.AddComponent(entity, new WayStationDialoguePoiAction());
				EntityManager.AddComponent(entity, ParallaxLayer.GetUIParallaxLayer());
			}

			var transform = entity.GetComponent<Transform>();
			transform.Position = new Vector2(rect.X, rect.Y);
			transform.ZOrder = ZOrder;

			var ui = entity.GetComponent<UIElement>();
			ui.Bounds = rect;
			ui.IsInteractable = visible;
			ui.IsHidden = !visible;
			ui.LayerType = UILayerType.Default;
			ui.TooltipType = TooltipType.None;
			ui.EventType = UIElementEventType.WayStationDialoguePoiSelect;

			var action = entity.GetComponent<WayStationDialoguePoiAction>();
			action.OfferId = offer.offerId ?? string.Empty;

			var poi = entity.GetComponent<PointOfInterest>();
			poi.Id = offer.offerId ?? string.Empty;
			poi.WorldPosition = center;
			poi.DisplayRadius = rect.Width / 2f;

			var tooltip = entity.GetComponent<POITitleTooltipSource>();
			tooltip.Title = WayStationDialogueCatalog.TryGetDisplayName(offer.characterId, out string displayName)
				? displayName
				: "Dialogue";
		}

		private void HidePoi(string entityName)
		{
			var ui = EntityManager.GetEntity(entityName)?.GetComponent<UIElement>();
			if (ui == null) return;
			ui.IsInteractable = false;
			ui.IsHidden = true;
		}

		private void DrawPoi(string entityName)
		{
			var ui = EntityManager.GetEntity(entityName)?.GetComponent<UIElement>();
			if (ui == null || ui.IsHidden) return;
			Color tint = ui.IsHovered ? Color.White : Color.White * 0.88f;
			_spriteBatch.Draw(_dialoguePoiTexture, ui.Bounds, tint);
		}

		private void DrawNpcPlacementArea()
		{
			if (!ShowNpcPlacementArea) return;
			var area = GetNpcPlacementArea();
			_spriteBatch.Draw(_pixel, area, Color.ForestGreen * 0.16f);
			DrawBorder(area, Color.LimeGreen * 0.65f, 2);
		}

		private void DrawBorder(Rectangle rect, Color color, int thickness)
		{
			int t = Math.Max(1, thickness);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, t), color);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - t, rect.Width, t), color);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, t, rect.Height), color);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.Right - t, rect.Y, t, rect.Height), color);
		}

		private Vector2 GetOfferCenter(WayStationDialogueOfferSave offer)
		{
			if (string.Equals(offer?.offerId, WayStationDialoguePlanner.KeeperOfferId, StringComparison.OrdinalIgnoreCase))
			{
				return new Vector2(KeeperPoiScreenX, KeeperPoiScreenY);
			}

			if (string.Equals(offer?.offerId, WayStationDialoguePlanner.RookTutorialOfferId, StringComparison.OrdinalIgnoreCase))
			{
				return new Vector2(RookTutorialPoiScreenX, RookTutorialPoiScreenY);
			}

			return new Vector2(offer?.screenX ?? 0f, offer?.screenY ?? 0f);
		}

		private Rectangle GetPoiBounds(string entityName, float centerX, float centerY)
		{
			float scale = IsHovered(entityName) ? PoiHoverScale : 1f;
			int size = Math.Max(1, (int)Math.Round(PoiIconSize * scale));
			return new Rectangle(
				(int)Math.Round(centerX - size / 2f),
				(int)Math.Round(centerY - size / 2f),
				size,
				size);
		}

		private Rectangle GetNpcPlacementArea()
		{
			int minZoneSize = Math.Max(1, PoiIconSize);
			int left = ClampPadding(NpcAreaLeftPadding, Game1.VirtualWidth, minZoneSize);
			int right = ClampPadding(NpcAreaRightPadding, Game1.VirtualWidth - left, minZoneSize);
			int top = ClampPadding(NpcAreaTopPadding, Game1.VirtualHeight, minZoneSize);
			int bottom = ClampPadding(NpcAreaBottomPadding, Game1.VirtualHeight - top, minZoneSize);

			int width = Math.Max(1, Game1.VirtualWidth - left - right);
			int height = Math.Max(1, Game1.VirtualHeight - top - bottom);
			return new Rectangle(left, top, width, height);
		}

		private static int ClampPadding(int requested, int dimension, int minZoneSize)
		{
			return Math.Clamp(requested, 0, Math.Max(0, dimension - minZoneSize));
		}

		private bool IsWayStationActive()
		{
			var scene = EntityManager.GetEntitiesWithComponent<SceneState>()
				.FirstOrDefault()
				?.GetComponent<SceneState>();
			return IsWayStationScene(scene);
		}

		private bool IsClimbModalOpen()
		{
			var animation = EntityManager.GetEntity(WayStationSceneConstants.ModalRootName)
				?.GetComponent<ModalAnimation>();
			return animation != null && (animation.RequestedVisible || animation.Phase != ModalAnimationPhase.Hidden);
		}

		private bool IsDialogueActive()
		{
			return EntityManager.GetEntity("DialogOverlay")?.GetComponent<DialogOverlayState>()?.IsActive == true;
		}

		private bool IsHovered(string name)
		{
			return EntityManager.GetEntity(name)?.GetComponent<UIElement>()?.IsHovered == true;
		}

		private static bool IsNewVisitArrival(WayStationArrivalKind arrivalKind)
		{
			return arrivalKind == WayStationArrivalKind.ReturnedFromFailedClimb
				|| arrivalKind == WayStationArrivalKind.ReturnedFromAbandonedClimb
				|| arrivalKind == WayStationArrivalKind.ReturnedFromCompletedClimb;
		}

		private static bool IsWayStationScene(SceneState scene)
		{
			return scene != null && (scene.Current == SceneId.WayStation || scene.Current == SceneId.Snapshot);
		}

		private static float Lerp(float a, float b, float amount)
		{
			return a + (b - a) * amount;
		}
	}
}
