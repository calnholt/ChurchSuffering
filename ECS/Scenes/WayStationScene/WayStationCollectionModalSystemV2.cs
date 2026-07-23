using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Input;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using static ChurchSuffering.ECS.Components.CardData;

namespace ChurchSuffering.ECS.Systems
{
	internal static class WayStationCollectionInputLayers
	{
		public const int Root = 5000;
		public const int ScrollBlocker = 5002;
		public const int Control = 5010;
		public const int CloseButton = 5020;
	}

	[DebugTab("WayStation Collection")]
	public sealed class WayStationCollectionModalSystemV2 : Core.System
	{
		private const int SaintListTopPadding = 16;

		[DebugEditable(DisplayName = "Mouse Scroll Step", Step = 4, Min = 8, Max = 240)]
		public int MouseScrollStep { get; set; } = 84;

		[DebugEditable(DisplayName = "Gamepad Scroll Speed", Step = 50, Min = 100, Max = 6000)]
		public float GamepadScrollSpeed { get; set; } = 1300f;

		private bool _catalogReady;

		public WayStationCollectionModalSystemV2(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<LoadSceneEvent>(OnLoadScene);
			EventManager.Subscribe<OpenWayStationCollectionModalEvent>(_ => Open());
		}

		protected override IEnumerable<Entity> GetRelevantEntities() =>
			EntityManager.GetEntitiesWithComponent<SceneState>();

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var scene = entity.GetComponent<SceneState>();
			bool supportedScene = scene?.Current is SceneId.WayStation or SceneId.Snapshot;
			if (!supportedScene)
			{
				Close(immediate: true);
				SetAllInteraction(false);
				return;
			}

			Entity root = EnsureRoot();
			var state = root.GetComponent<WayStationCollectionModalState>();
			var layout = root.GetComponent<WayStationCollectionModalLayout>();
			var animation = root.GetComponent<ModalAnimation>();
			ComputeLayout(layout);

			bool open = animation.RequestedVisible || animation.Phase != ModalAnimationPhase.Hidden;
			bool interactive = animation.Phase == ModalAnimationPhase.Visible;
			InputContextService.EnsureContext(
				EntityManager,
				root,
				WayStationSceneConstants.CollectionModalContextId,
				100,
				open);

			if (_catalogReady)
			{
				LayoutPresentations(root, state, layout);
				ClampScrollOffsets(root, state, layout);
				LayoutPresentations(root, state, layout);
			}

			var render = ModalAnimationRenderState.From(animation, layout.Shell);
			SyncInteraction(render, state, interactive);
			UpdateCardUpgradePreviews(state, interactive);
			if (!interactive) return;

			if (WasClicked(WayStationSceneConstants.CollectionModalCloseButtonName))
			{
				Close();
				return;
			}

			HandleClicks(root, state);
			UpdateScroll(root, state, layout, gameTime);
		}

		public void Open()
		{
			if (!IsSupportedScene()) return;
			Entity root = EnsureRoot();
			var catalog = WayStationCollectionCatalogService.Build(
				SaveCache.GetCollection(),
				SaveCache.GetWayStationMeta());
			root.GetComponent<WayStationCollectionCatalogComponent>().Catalog = catalog;
			var state = root.GetComponent<WayStationCollectionModalState>();
			WayStationCollectionModalLogic.Reset(state, catalog);
			ReconcileContent(root, catalog);
			foreach (var stack in EntityManager.GetEntitiesWithComponent<WayStationCollectionCardStackPresentation>())
			{
				var presentation = stack.GetComponent<WayStationCollectionCardStackPresentation>();
				presentation.FrontColor = CardColor.White;
				presentation.PendingFrontColor = null;
				presentation.ColorSwitchProgress = 1f;
				presentation.ShowUpgradePreview = false;
			}
			ResetMotion();
			_catalogReady = true;
			var animation = root.GetComponent<ModalAnimation>();
			animation.RequestedVisible = true;
			InputContextService.EnsureContext(
				EntityManager,
				root,
				WayStationSceneConstants.CollectionModalContextId,
				100,
				true);
		}

		public void Close(bool immediate = false)
		{
			var root = EntityManager.GetEntity(WayStationSceneConstants.CollectionModalRootName);
			var animation = root?.GetComponent<ModalAnimation>();
			if (animation == null) return;
			animation.RequestedVisible = false;
			if (immediate)
			{
				animation.Phase = ModalAnimationPhase.Hidden;
				animation.ElapsedSeconds = 0f;
			}
			if (root?.GetComponent<InputContext>() is InputContext context)
				context.IsActive = !immediate && animation.Phase != ModalAnimationPhase.Hidden;
		}

		private void OnLoadScene(LoadSceneEvent e)
		{
			if (e.Scene != SceneId.WayStation && e.Scene != SceneId.Snapshot)
			{
				Close(immediate: true);
				DestroyModalContent();
				_catalogReady = false;
			}
		}

		private Entity EnsureRoot()
		{
			var root = EntityManager.GetEntity(WayStationSceneConstants.CollectionModalRootName);
			if (root == null)
			{
				root = EntityManager.CreateEntity(WayStationSceneConstants.CollectionModalRootName);
				EntityManager.AddComponent(root, new Transform { ZOrder = WayStationCollectionInputLayers.Root });
				EntityManager.AddComponent(root, new UIElement
				{
					Bounds = new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight),
					TooltipType = TooltipType.None,
					IsInteractable = false,
					ShowHoverHighlight = false,
				});
				EntityManager.AddComponent(root, new ModalAnimation
				{
					InputContextId = WayStationSceneConstants.CollectionModalContextId,
				});
				EntityManager.AddComponent(root, new WayStationCollectionModalRoot());
				EntityManager.AddComponent(root, new WayStationCollectionModalState());
				EntityManager.AddComponent(root, new WayStationCollectionModalLayout());
				EntityManager.AddComponent(root, new WayStationCollectionCatalogComponent());
				EntityManager.AddComponent(root, new WayStationCollectionMotion());
			}
			root.GetComponent<Transform>().ZOrder = WayStationCollectionInputLayers.Root;
			InputContextService.EnsureContext(
				EntityManager,
				root,
				WayStationSceneConstants.CollectionModalContextId,
				100,
				false);
			InputContextService.EnsureMember(EntityManager, root, WayStationSceneConstants.CollectionModalContextId);

			EnsureSimpleControl<WayStationCollectionModalShell>(
				WayStationSceneConstants.CollectionModalShellName,
				interactable: false);
			var close = EnsureSimpleControl<WayStationCollectionModalCloseButton>(
				WayStationSceneConstants.CollectionModalCloseButtonName,
				interactable: true);
			close.GetComponent<Transform>().ZOrder = WayStationCollectionInputLayers.CloseButton;
			if (close.GetComponent<HotKey>() == null)
			{
				EntityManager.AddComponent(close, WayStationCollectionModalLogic.CreateCloseHotKey());
			}
			EnsureMotion(close);

			foreach (var tab in Enum.GetValues<WayStationCollectionTab>())
			{
				string name = WayStationSceneConstants.CollectionTabPrefix + tab;
				var tabEntity = EntityManager.GetEntity(name) ?? CreateUiEntity(name);
				if (tabEntity.GetComponent<WayStationCollectionTabPresentation>() == null)
					EntityManager.AddComponent(tabEntity, new WayStationCollectionTabPresentation { Tab = tab });
				EnsureMotion(tabEntity);
			}

			foreach (var filter in Enum.GetValues<WayStationCollectionCardFilter>())
			{
				string name = WayStationSceneConstants.CollectionFilterPrefix + filter;
				var filterEntity = EntityManager.GetEntity(name) ?? CreateUiEntity(name);
				if (filterEntity.GetComponent<WayStationCollectionFilterPresentation>() == null)
					EntityManager.AddComponent(filterEntity, new WayStationCollectionFilterPresentation { Filter = filter });
				EnsureMotion(filterEntity);
			}

			EnsureScrollBlocker(WayStationSceneConstants.CollectionCardScrollName);
			EnsureScrollBlocker(WayStationSceneConstants.CollectionSaintListScrollName);
			EnsureScrollBlocker(WayStationSceneConstants.CollectionSaintDetailScrollName);
			EnsureScrollBlocker(WayStationSceneConstants.CollectionEquipmentScrollName);
			return root;
		}

		private Entity EnsureSimpleControl<TEntity>(string name, bool interactable)
			where TEntity : class, IComponent, new()
		{
			var entity = EntityManager.GetEntity(name) ?? CreateUiEntity(name);
			if (entity.GetComponent<TEntity>() == null) EntityManager.AddComponent(entity, new TEntity());
			InputContextService.EnsureMember(EntityManager, entity, WayStationSceneConstants.CollectionModalContextId);
			entity.GetComponent<UIElement>().BaseInteractable = interactable;
			return entity;
		}

		private Entity CreateUiEntity(string name)
		{
			var entity = EntityManager.CreateEntity(name);
			EntityManager.AddComponent(entity, new Transform { ZOrder = WayStationCollectionInputLayers.Control });
			EntityManager.AddComponent(entity, new UIElement
			{
				TooltipType = TooltipType.None,
				ShowHoverHighlight = false,
				LayerType = UILayerType.Overlay,
			});
			InputContextService.EnsureMember(EntityManager, entity, WayStationSceneConstants.CollectionModalContextId);
			return entity;
		}

		private void EnsureScrollBlocker(string name)
		{
			var entity = EntityManager.GetEntity(name) ?? CreateUiEntity(name);
			if (entity.GetComponent<WayStationCollectionScrollBlocker>() == null)
				EntityManager.AddComponent(entity, new WayStationCollectionScrollBlocker());
			entity.GetComponent<Transform>().ZOrder = WayStationCollectionInputLayers.ScrollBlocker;
		}

		private void ReconcileContent(Entity root, WayStationCollectionCatalog catalog)
		{
			ReconcileCards(catalog.Cards);
			ReconcileSaints(catalog.Saints);
			ReconcileEquipment(catalog.Equipment);
		}

		private void ReconcileCards(IReadOnlyList<WayStationCollectionCardEntry> cards)
		{
			var wanted = new HashSet<string>(cards.Select(item => item.Id), StringComparer.OrdinalIgnoreCase);
			foreach (var entity in EntityManager.GetEntitiesWithComponent<WayStationCollectionCardStackPresentation>().ToList())
			{
				var stack = entity.GetComponent<WayStationCollectionCardStackPresentation>();
				if (wanted.Contains(stack.CardId)) continue;
				foreach (var preview in stack.PreviewCards.Where(item => item != null).ToList())
					EntityManager.DestroyEntity(preview.Id);
				EntityManager.DestroyEntity(entity.Id);
			}

			int index = 0;
			foreach (var item in cards)
			{
				string name = WayStationSceneConstants.CollectionCardStackPrefix + item.Id;
				var entity = EntityManager.GetEntity(name);
				bool isWeapon = item.Card.IsWeapon;
				if (entity == null)
				{
					entity = CreateUiEntity(name);
					var stack = new WayStationCollectionCardStackPresentation
					{
						CardId = item.Id,
						IsWeapon = isWeapon,
						WhiteCard = CreatePreviewCard(item.Id, CardColor.White, index),
						RedCard = isWeapon ? null : CreatePreviewCard(item.Id, CardColor.Red, index),
						BlackCard = isWeapon ? null : CreatePreviewCard(item.Id, CardColor.Black, index),
					};
					EntityManager.AddComponent(entity, stack);
					EnsureMotion(entity);
				}
				else
				{
					var stack = entity.GetComponent<WayStationCollectionCardStackPresentation>();
					stack.IsWeapon = isWeapon;
					if (isWeapon)
					{
						DestroyPreview(stack.RedCard);
						DestroyPreview(stack.BlackCard);
						DestroyPreview(stack.UpgradedRedCard);
						DestroyPreview(stack.UpgradedBlackCard);
						stack.RedCard = null;
						stack.BlackCard = null;
						stack.UpgradedRedCard = null;
						stack.UpgradedBlackCard = null;
					}
				}
				index++;
			}
		}

		private void DestroyPreview(Entity preview)
		{
			if (preview != null) EntityManager.DestroyEntity(preview.Id);
		}

		private Entity CreatePreviewCard(
			string cardId,
			CardColor color,
			int index,
			bool isUpgraded = false)
		{
			var card = EntityFactory.CreateCardFromDefinition(
				EntityManager,
				cardId,
				color,
				allowWeapons: true,
				index: 7000 + index * 6 + (int)color + (isUpgraded ? 3 : 0),
				suppressStatDeltaDisplay: true,
				isUpgraded: isUpgraded);
			if (card == null) return null;
			string upgradeSuffix = isUpgraded ? "_Upgraded" : string.Empty;
			card.Name = $"{WayStationSceneConstants.CollectionCardStackPrefix}Preview_{cardId}_{color}{upgradeSuffix}";
			var ui = card.GetComponent<UIElement>();
			if (ui != null)
			{
				ui.IsInteractable = false;
				ui.IsHidden = true;
				ui.TooltipType = TooltipType.None;
			}
			if (card.GetComponent<InputContextMember>() != null)
				EntityManager.RemoveComponent<InputContextMember>(card);
			EntityManager.AddComponent(card, new WayStationCollectionPreviewCard
			{
				CardId = cardId,
				Color = color,
				IsUpgraded = isUpgraded,
			});
			return card;
		}

		private void UpdateCardUpgradePreviews(
			WayStationCollectionModalState state,
			bool interactive)
		{
			bool modifierHeld = interactive
				&& state.ActiveTab == WayStationCollectionTab.Cards
				&& WayStationCollectionModalLogic.IsUpgradePreviewModifierHeld(
					PlayerInputService.GetFrame(EntityManager));
			foreach (var entity in EntityManager.GetEntitiesWithComponent<WayStationCollectionCardStackPresentation>())
			{
				var stack = entity.GetComponent<WayStationCollectionCardStackPresentation>();
				bool show = modifierHeld && entity.GetComponent<UIElement>()?.IsHovered == true;
				if (show) EnsureUpgradedPreviewCards(stack, entity.Id);
				stack.ShowUpgradePreview = show;
			}
		}

		private void EnsureUpgradedPreviewCards(
			WayStationCollectionCardStackPresentation stack,
			int previewIndex)
		{
			stack.UpgradedWhiteCard ??= CreatePreviewCard(
				stack.CardId,
				CardColor.White,
				previewIndex,
				isUpgraded: true);
			if (stack.IsWeapon) return;
			stack.UpgradedRedCard ??= CreatePreviewCard(
				stack.CardId,
				CardColor.Red,
				previewIndex,
				isUpgraded: true);
			stack.UpgradedBlackCard ??= CreatePreviewCard(
				stack.CardId,
				CardColor.Black,
				previewIndex,
				isUpgraded: true);
		}

		private void ReconcileSaints(IReadOnlyList<WayStationCollectionSaintEntry> saints)
		{
			var wanted = new HashSet<string>(saints.Select(item => item.Id), StringComparer.OrdinalIgnoreCase);
			DestroyStale<WayStationCollectionSaintTilePresentation>(
				entity => wanted.Contains(entity.GetComponent<WayStationCollectionSaintTilePresentation>().MedalId));
			foreach (var item in saints)
			{
				string name = WayStationSceneConstants.CollectionSaintTilePrefix + item.Id;
				var entity = EntityManager.GetEntity(name) ?? CreateUiEntity(name);
				if (entity.GetComponent<WayStationCollectionSaintTilePresentation>() == null)
					EntityManager.AddComponent(entity, new WayStationCollectionSaintTilePresentation { MedalId = item.Id });
				EnsureMotion(entity);
			}
		}

		private void ReconcileEquipment(IReadOnlyList<WayStationCollectionEquipmentEntry> equipment)
		{
			var wanted = new HashSet<string>(equipment.Select(item => item.Id), StringComparer.OrdinalIgnoreCase);
			DestroyStale<WayStationCollectionEquipmentTilePresentation>(
				entity => wanted.Contains(entity.GetComponent<WayStationCollectionEquipmentTilePresentation>().EquipmentId));
			foreach (var item in equipment)
			{
				string name = WayStationSceneConstants.CollectionEquipmentTilePrefix + item.Id;
				var entity = EntityManager.GetEntity(name) ?? CreateUiEntity(name);
				var presentation = entity.GetComponent<WayStationCollectionEquipmentTilePresentation>();
				if (presentation == null)
				{
					presentation = new WayStationCollectionEquipmentTilePresentation { EquipmentId = item.Id };
					EntityManager.AddComponent(entity, presentation);
				}
				presentation.Slot = item.Equipment.Slot;
				EnsureMotion(entity);
			}
		}

		private void DestroyStale<T>(Func<Entity, bool> keep) where T : class, IComponent
		{
			foreach (var entity in EntityManager.GetEntitiesWithComponent<T>().ToList())
				if (!keep(entity)) EntityManager.DestroyEntity(entity.Id);
		}

		private void EnsureMotion(Entity entity)
		{
			if (entity.GetComponent<WayStationCollectionMotion>() == null)
				EntityManager.AddComponent(entity, new WayStationCollectionMotion());
		}

		private static void ComputeLayout(WayStationCollectionModalLayout layout)
		{
			layout.Shell = new Rectangle(40, 40, 1840, 1000);
			layout.Header = new Rectangle(42, 42, 1836, 105);
			layout.TabRow = new Rectangle(42, 147, 1836, 88);
			layout.Body = new Rectangle(42, 235, 1836, 729);
			layout.Footer = new Rectangle(42, 964, 1836, 74);
			layout.CloseButton = new Rectangle(1808, 68, 44, 44);
			layout.ActivePanel = layout.Body;
			layout.CardFilterRow = new Rectangle(74, 251, 1772, 55);
			layout.CardGridClip = new Rectangle(74, 306, 1772, 638);
			int saintWallWidth = (int)Math.Round(layout.Body.Width * 0.46f);
			layout.SaintWall = new Rectangle(layout.Body.X, layout.Body.Y, saintWallWidth, layout.Body.Height);
			layout.SaintToolbar = new Rectangle(layout.SaintWall.X, layout.SaintWall.Y, layout.SaintWall.Width, 56);
			layout.SaintListClip = new Rectangle(layout.SaintWall.X + 28, layout.SaintToolbar.Bottom + 10, layout.SaintWall.Width - 56, layout.SaintWall.Height - 84);
			layout.SaintDetail = new Rectangle(layout.SaintWall.Right, layout.Body.Y, layout.Body.Right - layout.SaintWall.Right, layout.Body.Height);
			layout.SaintDetailClip = new Rectangle(layout.SaintDetail.X + 42, layout.SaintDetail.Y + 28, layout.SaintDetail.Width - 84, layout.SaintDetail.Height - 56);
			layout.EquipmentHall = layout.Body;
			layout.EquipmentHeader = new Rectangle(layout.Body.X, layout.Body.Y, layout.Body.Width, 72);
			layout.EquipmentContentClip = new Rectangle(layout.Body.X + 18, layout.EquipmentHeader.Bottom, layout.Body.Width - 36, layout.Body.Height - 88);
			layout.FooterMeter = new Rectangle(layout.Footer.Center.X - 230, layout.Footer.Center.Y - 5, 460, 10);
			layout.FooterLabelAnchor = new Vector2(layout.Footer.X + 32, layout.Footer.Center.Y);
			layout.FooterCountAnchor = new Vector2(layout.Footer.Right - 32, layout.Footer.Center.Y);
		}

		private void LayoutPresentations(Entity root, WayStationCollectionModalState state, WayStationCollectionModalLayout layout)
		{
			var catalog = root.GetComponent<WayStationCollectionCatalogComponent>().Catalog;
			LayoutTabs(layout);
			LayoutFilters(layout);
			LayoutCards(state, layout, catalog);
			LayoutSaints(state, layout, catalog);
			LayoutEquipment(state, layout, catalog);
		}

		private void LayoutTabs(WayStationCollectionModalLayout layout)
		{
			const int width = 222;
			const int gap = 14;
			int startX = layout.TabRow.Center.X - (width * 3 + gap * 2) / 2;
			int index = 0;
			foreach (var entity in EntityManager.GetEntitiesWithComponent<WayStationCollectionTabPresentation>()
				.OrderBy(item => item.GetComponent<WayStationCollectionTabPresentation>().Tab))
			{
				entity.GetComponent<WayStationCollectionTabPresentation>().Bounds =
					new Rectangle(startX + index * (width + gap), layout.TabRow.Y + 16, width, 72);
				index++;
			}
		}

		private void LayoutFilters(WayStationCollectionModalLayout layout)
		{
			int[] widths = [84, 116, 132, 124, 128];
			const int gap = 10;
			int total = widths.Sum() + gap * (widths.Length - 1);
			int x = layout.CardFilterRow.Center.X - total / 2;
			int index = 0;
			foreach (var entity in EntityManager.GetEntitiesWithComponent<WayStationCollectionFilterPresentation>()
				.OrderBy(item => item.GetComponent<WayStationCollectionFilterPresentation>().Filter))
			{
				entity.GetComponent<WayStationCollectionFilterPresentation>().Bounds =
					new Rectangle(x, layout.CardFilterRow.Center.Y - 15, widths[index], 30);
				x += widths[index] + gap;
				index++;
			}
		}

		private void LayoutCards(
			WayStationCollectionModalState state,
			WayStationCollectionModalLayout layout,
			WayStationCollectionCatalog catalog)
		{
			const int targetWidth = 259;
			const int targetHeight = 365;
			const int gapX = 34;
			const int gapY = 30;
			int columns = Math.Max(1, (layout.CardGridClip.Width + gapX) / (targetWidth + gapX));
			var visible = WayStationCollectionCatalogService.FilterCards(catalog, state.ActiveCardFilter);
			int totalWidth = columns * targetWidth + (columns - 1) * gapX;
			int startX = layout.CardGridClip.Center.X - totalWidth / 2;
			var indexById = visible.Select((item, index) => (item.Id, index))
				.ToDictionary(item => item.Id, item => item.index, StringComparer.OrdinalIgnoreCase);
			foreach (var entity in EntityManager.GetEntitiesWithComponent<WayStationCollectionCardStackPresentation>())
			{
				var stack = entity.GetComponent<WayStationCollectionCardStackPresentation>();
				if (!indexById.TryGetValue(stack.CardId, out int index))
				{
					stack.Bounds = Rectangle.Empty;
					continue;
				}
				int col = index % columns;
				int row = index / columns;
				stack.Bounds = new Rectangle(
					startX + col * (targetWidth + gapX),
					layout.CardGridClip.Y + 12 + row * (targetHeight + gapY) - state.CardScrollOffset,
					targetWidth,
					targetHeight);
			}
			var settings = CardGeometryService.GetSettings(EntityManager);
			layout.CardScale = targetWidth / (float)Math.Max(1, settings?.CardWidth ?? CardGeometrySettings.DefaultWidth);
		}

		private void LayoutSaints(
			WayStationCollectionModalState state,
			WayStationCollectionModalLayout layout,
			WayStationCollectionCatalog catalog)
		{
			const int columns = 5;
			const int cellHeight = 126;
			const int gapX = 12;
			const int gapY = 18;
			int cellWidth = Math.Max(1, (layout.SaintListClip.Width - (columns - 1) * gapX) / columns);
			int totalWidth = columns * cellWidth + (columns - 1) * gapX;
			int startX = layout.SaintListClip.Center.X - totalWidth / 2;
			var indexById = catalog.Saints.Select((item, index) => (item.Id, index))
				.ToDictionary(item => item.Id, item => item.index, StringComparer.OrdinalIgnoreCase);
			foreach (var entity in EntityManager.GetEntitiesWithComponent<WayStationCollectionSaintTilePresentation>())
			{
				var tile = entity.GetComponent<WayStationCollectionSaintTilePresentation>();
				if (!indexById.TryGetValue(tile.MedalId, out int index))
				{
					tile.Bounds = Rectangle.Empty;
					continue;
				}
				int col = index % columns;
				int row = index / columns;
				tile.Bounds = new Rectangle(
					startX + col * (cellWidth + gapX),
					layout.SaintListClip.Y + SaintListTopPadding + row * (cellHeight + gapY) - state.SaintListScrollOffset,
					cellWidth,
					cellHeight);
				tile.IsSelected = string.Equals(tile.MedalId, state.SelectedMedalId, StringComparison.OrdinalIgnoreCase);
			}
		}

		private void LayoutEquipment(
			WayStationCollectionModalState state,
			WayStationCollectionModalLayout layout,
			WayStationCollectionCatalog catalog)
		{
			EquipmentSlot[] slots = [EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Arms, EquipmentSlot.Legs];
			int columnWidth = layout.EquipmentContentClip.Width / slots.Length;
			foreach (var slot in slots)
			{
				int slotIndex = Array.IndexOf(slots, slot);
				int x = layout.EquipmentContentClip.X + slotIndex * columnWidth + 10;
				int y = layout.EquipmentContentClip.Y + 12 - state.EquipmentScrollOffset;
				foreach (var item in catalog.Equipment.Where(item => item.Equipment.Slot == slot))
				{
					var tile = EntityManager.GetEntity(WayStationSceneConstants.CollectionEquipmentTilePrefix + item.Id)
						?.GetComponent<WayStationCollectionEquipmentTilePresentation>();
					if (tile == null) continue;
					int height = Math.Max(160, EstimateEquipmentHeight(item.Equipment.Text, item.Equipment.FlavorText));
					tile.Bounds = new Rectangle(x, y, columnWidth - 20, height);
					tile.ArtBounds = new Rectangle(x + 16, y + 16, 128, 128);
					tile.ContentHeight = height;
					y += height + 14;
				}
			}
		}

		private static int EstimateEquipmentHeight(string text, string flavor) =>
			160 + (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(flavor) ? 0 : 42);

		private void ClampScrollOffsets(Entity root, WayStationCollectionModalState state, WayStationCollectionModalLayout layout)
		{
			var catalog = root.GetComponent<WayStationCollectionCatalogComponent>().Catalog;
			int cardCount = WayStationCollectionCatalogService.FilterCards(catalog, state.ActiveCardFilter).Count;
			int cardColumns = Math.Max(1, (layout.CardGridClip.Width + 34) / (259 + 34));
			int cardRows = (int)Math.Ceiling(cardCount / (float)cardColumns);
			int cardHeight = cardRows == 0 ? 0 : cardRows * 365 + (cardRows - 1) * 30 + 24;
			state.CardScrollOffset = Math.Clamp(state.CardScrollOffset, 0, Math.Max(0, cardHeight - layout.CardGridClip.Height));

			int saintRows = (int)Math.Ceiling(catalog.Saints.Count / 5f);
			int saintHeight = saintRows == 0
				? 0
				: SaintListTopPadding + saintRows * 126 + (saintRows - 1) * 18;
			state.SaintListScrollOffset = Math.Clamp(
				state.SaintListScrollOffset,
				0,
				Math.Max(0, saintHeight - layout.SaintListClip.Height));
			state.SaintDetailScrollOffset = Math.Clamp(
				state.SaintDetailScrollOffset,
				0,
				Math.Max(0, EstimateSaintDetailHeight(catalog, state.SelectedMedalId) - layout.SaintDetailClip.Height));

			int equipmentHeight = 0;
			foreach (var slot in Enum.GetValues<EquipmentSlot>())
			{
				int height = catalog.Equipment
					.Where(item => item.Equipment.Slot == slot)
					.Sum(item => EstimateEquipmentHeight(item.Equipment.Text, item.Equipment.FlavorText) + 14);
				equipmentHeight = Math.Max(equipmentHeight, height);
			}
			state.EquipmentScrollOffset = Math.Clamp(
				state.EquipmentScrollOffset,
				0,
				Math.Max(0, equipmentHeight - layout.EquipmentContentClip.Height));
		}

		private static int EstimateSaintDetailHeight(
			WayStationCollectionCatalog catalog,
			string selectedId)
		{
			var saint = catalog.Saints.FirstOrDefault(item =>
				string.Equals(item.Id, selectedId, StringComparison.OrdinalIgnoreCase));
			if (saint == null) return 0;
			int paragraphs = saint.Saint?.bioParagraphs?.Count ?? 0;
			return 430 + paragraphs * 135
				+ (saint.Medal.Text?.Length ?? 0) / 3
				+ (saint.Saint?.patronages?.Length ?? 0) / 3
				+ (saint.Saint?.prayerText?.Length ?? 0) / 3;
		}

		private void SyncInteraction(
			ModalAnimationRenderState render,
			WayStationCollectionModalState state,
			bool interactive)
		{
			SyncUi(WayStationSceneConstants.CollectionModalShellName, render.Transform(GetLayout().Shell), false, !render.ShouldDraw);
			SyncUi(WayStationSceneConstants.CollectionModalCloseButtonName, render.Transform(GetLayout().CloseButton), interactive, !render.ShouldDraw);

			foreach (var entity in EntityManager.GetEntitiesWithComponent<WayStationCollectionTabPresentation>())
				SyncUi(entity, render.Transform(entity.GetComponent<WayStationCollectionTabPresentation>().Bounds), interactive, !render.ShouldDraw);
			foreach (var entity in EntityManager.GetEntitiesWithComponent<WayStationCollectionFilterPresentation>())
			{
				bool shown = state.ActiveTab == WayStationCollectionTab.Cards;
				SyncUi(entity, shown ? render.Transform(entity.GetComponent<WayStationCollectionFilterPresentation>().Bounds) : Rectangle.Empty, interactive && shown, !shown);
			}
			foreach (var entity in EntityManager.GetEntitiesWithComponent<WayStationCollectionCardStackPresentation>())
			{
				var bounds = entity.GetComponent<WayStationCollectionCardStackPresentation>().Bounds;
				var visibleBounds = Rectangle.Intersect(bounds, GetLayout().CardGridClip);
				bool shown = state.ActiveTab == WayStationCollectionTab.Cards && visibleBounds != Rectangle.Empty;
				SyncUi(entity, shown ? render.Transform(visibleBounds) : Rectangle.Empty, interactive && shown, !shown);
			}
			foreach (var entity in EntityManager.GetEntitiesWithComponent<WayStationCollectionSaintTilePresentation>())
			{
				var bounds = entity.GetComponent<WayStationCollectionSaintTilePresentation>().Bounds;
				var visibleBounds = Rectangle.Intersect(bounds, GetLayout().SaintListClip);
				bool shown = state.ActiveTab == WayStationCollectionTab.Saints && visibleBounds != Rectangle.Empty;
				SyncUi(entity, shown ? render.Transform(visibleBounds) : Rectangle.Empty, interactive && shown, !shown);
			}
			foreach (var entity in EntityManager.GetEntitiesWithComponent<WayStationCollectionEquipmentTilePresentation>())
			{
				var bounds = entity.GetComponent<WayStationCollectionEquipmentTilePresentation>().Bounds;
				var visibleBounds = Rectangle.Intersect(bounds, GetLayout().EquipmentContentClip);
				bool shown = state.ActiveTab == WayStationCollectionTab.Equipment && visibleBounds != Rectangle.Empty;
				SyncUi(entity, shown ? render.Transform(visibleBounds) : Rectangle.Empty, interactive && shown, !shown);
			}

			SyncScrollUi(WayStationSceneConstants.CollectionCardScrollName, GetLayout().CardGridClip, state.ActiveTab == WayStationCollectionTab.Cards, render, interactive);
			SyncScrollUi(WayStationSceneConstants.CollectionSaintListScrollName, GetLayout().SaintListClip, state.ActiveTab == WayStationCollectionTab.Saints, render, interactive);
			SyncScrollUi(WayStationSceneConstants.CollectionSaintDetailScrollName, GetLayout().SaintDetailClip, state.ActiveTab == WayStationCollectionTab.Saints, render, interactive);
			SyncScrollUi(WayStationSceneConstants.CollectionEquipmentScrollName, GetLayout().EquipmentContentClip, state.ActiveTab == WayStationCollectionTab.Equipment, render, interactive);
		}

		private void SyncScrollUi(string name, Rectangle bounds, bool shown, ModalAnimationRenderState render, bool interactive) =>
			SyncUi(name, shown ? render.Transform(bounds) : Rectangle.Empty, shown && interactive, !shown);

		private void SyncUi(string name, Rectangle bounds, bool interactable, bool hidden)
		{
			var entity = EntityManager.GetEntity(name);
			if (entity != null) SyncUi(entity, bounds, interactable, hidden);
		}

		private static void SyncUi(Entity entity, Rectangle bounds, bool interactable, bool hidden)
		{
			var ui = entity.GetComponent<UIElement>();
			if (ui == null) return;
			ui.Bounds = bounds;
			ui.IsInteractable = interactable;
			ui.IsHidden = hidden;
			if (!interactable)
			{
				ui.IsHovered = false;
				ui.IsClicked = false;
			}
			var transform = entity.GetComponent<Transform>();
			if (transform != null) transform.Position = new Vector2(bounds.X, bounds.Y);
		}

		private void HandleClicks(Entity root, WayStationCollectionModalState state)
		{
			foreach (var entity in EntityManager.GetEntitiesWithComponent<WayStationCollectionTabPresentation>())
			{
				if (entity.GetComponent<UIElement>()?.IsClicked != true) continue;
				state.ActiveTab = entity.GetComponent<WayStationCollectionTabPresentation>().Tab;
				ClampScrollOffsets(root, state, GetLayout());
				return;
			}
			foreach (var entity in EntityManager.GetEntitiesWithComponent<WayStationCollectionFilterPresentation>())
			{
				if (entity.GetComponent<UIElement>()?.IsClicked != true) continue;
				state.ActiveCardFilter = entity.GetComponent<WayStationCollectionFilterPresentation>().Filter;
				state.CardScrollOffset = 0;
				ClampScrollOffsets(root, state, GetLayout());
				return;
			}
			foreach (var entity in EntityManager.GetEntitiesWithComponent<WayStationCollectionCardStackPresentation>())
			{
				if (entity.GetComponent<UIElement>()?.IsClicked != true) continue;
				var stack = entity.GetComponent<WayStationCollectionCardStackPresentation>();
				if (!stack.IsWeapon && !stack.PendingFrontColor.HasValue)
				{
					stack.PendingFrontColor = WayStationCollectionModalLogic.NextColor(stack.FrontColor);
					stack.ColorSwitchProgress = 0f;
				}
				return;
			}
			foreach (var entity in EntityManager.GetEntitiesWithComponent<WayStationCollectionSaintTilePresentation>())
			{
				if (entity.GetComponent<UIElement>()?.IsClicked != true) continue;
				string next = entity.GetComponent<WayStationCollectionSaintTilePresentation>().MedalId;
				if (!string.Equals(next, state.SelectedMedalId, StringComparison.OrdinalIgnoreCase))
				{
					state.SelectedMedalId = next;
					state.SaintDetailScrollOffset = 0;
					EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.SaintInfo, Volume = 0.5f });
				}
				return;
			}
		}

		private void UpdateScroll(
			Entity root,
			WayStationCollectionModalState state,
			WayStationCollectionModalLayout layout,
			GameTime gameTime)
		{
			PlayerInputFrame input = PlayerInputService.GetFrame(EntityManager);
			bool wheel = Math.Abs(input.ScrollDelta) > 0.001f;
			bool stick = MathF.Abs(input.RightStick.Y) > 0.15f;
			if (!wheel && !stick) return;
			int delta = 0;
			if (wheel) delta -= (int)Math.Round(input.ScrollDelta) * MouseScrollStep;
			if (stick)
				delta += (int)Math.Round(-Math.Sign(input.RightStick.Y) * GamepadScrollSpeed * gameTime.ElapsedGameTime.TotalSeconds);

			switch (state.ActiveTab)
			{
				case WayStationCollectionTab.Cards:
					state.CardScrollOffset += delta;
					break;
				case WayStationCollectionTab.Equipment:
					state.EquipmentScrollOffset += delta;
					break;
				case WayStationCollectionTab.Saints:
					bool inList = layout.SaintListClip.Contains(input.PointerPosition);
					bool inDetail = layout.SaintDetailClip.Contains(input.PointerPosition);
					if (inList && !inDetail) state.SaintListScrollOffset += delta;
					else if (!string.IsNullOrWhiteSpace(state.SelectedMedalId)) state.SaintDetailScrollOffset += delta;
					break;
			}
			ClampScrollOffsets(root, state, layout);
		}

		private void ResetMotion()
		{
			foreach (var entity in EntityManager.GetEntitiesWithComponent<WayStationCollectionMotion>())
			{
				var motion = entity.GetComponent<WayStationCollectionMotion>();
				motion.Hover = motion.TargetHover = 0f;
				motion.Scale = motion.TargetScale = 1f;
				motion.FanAngle = motion.TargetFanAngle = 0f;
				motion.Glow = motion.TargetGlow = 0f;
				motion.MeterProgress = motion.TargetMeterProgress = 0f;
			}
		}

		private void SetAllInteraction(bool interactable)
		{
			foreach (var entity in EntityManager.GetEntitiesWithComponent<InputContextMember>())
			{
				if (!string.Equals(entity.GetComponent<InputContextMember>()?.ContextId, WayStationSceneConstants.CollectionModalContextId, StringComparison.Ordinal))
					continue;
				if (entity.GetComponent<UIElement>() is UIElement ui)
				{
					ui.IsInteractable = interactable;
					if (!interactable)
					{
						ui.IsHovered = false;
						ui.IsClicked = false;
					}
				}
			}
		}

		private void DestroyModalContent()
		{
			var previews = EntityManager.GetEntitiesWithComponent<WayStationCollectionPreviewCard>().ToList();
			foreach (var preview in previews) EntityManager.DestroyEntity(preview.Id);
			var stacks = EntityManager.GetEntitiesWithComponent<WayStationCollectionCardStackPresentation>().ToList();
			foreach (var stack in stacks) EntityManager.DestroyEntity(stack.Id);
			var saints = EntityManager.GetEntitiesWithComponent<WayStationCollectionSaintTilePresentation>().ToList();
			foreach (var saint in saints) EntityManager.DestroyEntity(saint.Id);
			var equipment = EntityManager.GetEntitiesWithComponent<WayStationCollectionEquipmentTilePresentation>().ToList();
			foreach (var item in equipment) EntityManager.DestroyEntity(item.Id);
		}

		private bool IsSupportedScene() =>
			EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault()
				?.GetComponent<SceneState>()?.Current is SceneId.WayStation or SceneId.Snapshot;

		private bool WasClicked(string name) =>
			EntityManager.GetEntity(name)?.GetComponent<UIElement>()?.IsClicked == true;

		private WayStationCollectionModalLayout GetLayout() =>
			EntityManager.GetEntity(WayStationSceneConstants.CollectionModalRootName)
				?.GetComponent<WayStationCollectionModalLayout>();
	}
}
