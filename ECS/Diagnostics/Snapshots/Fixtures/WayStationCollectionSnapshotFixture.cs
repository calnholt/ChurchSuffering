using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.Diagnostics.Snapshots.Fixtures
{
	public sealed class WayStationCollectionSnapshotFixture : IDisplaySnapshotFixture
	{
		public string Id => "waystation-collection";
		public int WarmupFrames => 3;
		public string OutputFileName => _variant.FileSlug();

		private WayStationCollectionSnapshotVariant _variant;
		private WayStationBackgroundDisplaySystem _background;
		private IncenseDisplaySystem _incense;
		private WayStationCollectionModalSystemV2 _controller;
		private WayStationCollectionMotionSystem _motion;
		private WayStationCollectionChromeDisplaySystem _chrome;
		private WayStationCollectionCardsDisplaySystem _cards;
		private WayStationCollectionSaintsDisplaySystem _saints;
		private WayStationCollectionEquipmentDisplaySystem _equipment;

		public void Setup(DisplaySnapshotContext ctx, string[] args)
		{
			_variant = WayStationCollectionSnapshotVariantParser.Parse(args);
			ctx.SceneEntity.GetComponent<SceneState>().Current = SceneId.Snapshot;
			SaveCache.GetAll().collection = new PlayerCollectionSave
			{
				cardIds =
				[
					"absolution", "consecrate", "divine_protection", "fury", "hold_the_line",
					"impale", "mantlet", "reap", "smite", "stalwart", "strike", "tempest",
				],
				medalIds = ["st_augustine", "st_joan_of_arc", "st_michael", "st_rita", "st_sebastian", "st_thomas_aquinas"],
				equipmentIds =
				[
					"knightly_helm", "ivory_coif", "helm_of_seeing",
					"knightly_chest", "bulwark_plate", "heartforge_cuirass",
					"knightly_gauntlets", "kunai_sheath", "whetstone_gauntlets",
					"knightly_grieves", "fleetfoot_greaves", "sunderstep_treads",
				],
			};
			SaveCache.GetAll().waystation = new WayStationMetaSave
			{
				climbCompletions = 3,
				highestPenanceByWeapon = new Dictionary<string, int>
				{
					["sword"] = 24,
					["dagger"] = 24,
					["hammer"] = 24,
				},
			};
			SaveCache.GetAll().collection.pendingClimbPoints = 0;
			SaveCache.GetAll().pendingDeckRewardOffer = null;

			_background = new WayStationBackgroundDisplaySystem(ctx.World.EntityManager, ctx.SpriteBatch, ctx.ImageAssets);
			_incense = new IncenseDisplaySystem(ctx.World.EntityManager, ctx.GraphicsDevice, ctx.SpriteBatch, ctx.Content);
			_controller = new WayStationCollectionModalSystemV2(ctx.World.EntityManager);
			_motion = new WayStationCollectionMotionSystem(ctx.World.EntityManager);
			_chrome = new WayStationCollectionChromeDisplaySystem(ctx.World.EntityManager, ctx.GraphicsDevice, ctx.SpriteBatch, ctx.ImageAssets);
			_cards = new WayStationCollectionCardsDisplaySystem(ctx.World.EntityManager, ctx.GraphicsDevice, ctx.SpriteBatch, ctx.ImageAssets);
			_saints = new WayStationCollectionSaintsDisplaySystem(ctx.World.EntityManager, ctx.GraphicsDevice, ctx.SpriteBatch, ctx.ImageAssets);
			_equipment = new WayStationCollectionEquipmentDisplaySystem(ctx.World.EntityManager, ctx.GraphicsDevice, ctx.SpriteBatch, ctx.ImageAssets);

			ctx.World.AddSystem(_background);
			ctx.World.AddSystem(_incense);
			ctx.World.AddSystem(_controller);
			ctx.World.AddSystem(_motion);
			ctx.World.AddSystem(_chrome);
			ctx.World.AddSystem(_cards);
			ctx.World.AddSystem(_saints);
			ctx.World.AddSystem(_equipment);

			EventManager.Publish(new OpenWayStationCollectionModalEvent());
			PinSettled(ctx);
		}

		public void Draw(DisplaySnapshotContext ctx)
		{
			PinSettled(ctx);
			_background.Draw();
			_incense.Draw();
			_chrome.Draw();
			_cards.Draw();
			_saints.Draw();
			_equipment.Draw();
		}

		private void PinSettled(DisplaySnapshotContext ctx)
		{
			Entity root = ctx.World.EntityManager.GetEntity(WayStationSceneConstants.CollectionModalRootName);
			var animation = root?.GetComponent<ModalAnimation>();
			var state = root?.GetComponent<WayStationCollectionModalState>();
			if (animation == null || state == null)
				throw new DisplaySnapshotSetupException("Waystation Collection modal did not open.");
			animation.RequestedVisible = true;
			animation.Phase = ModalAnimationPhase.Visible;
			animation.ElapsedSeconds = 0f;

			state.ActiveTab = _variant switch
			{
				WayStationCollectionSnapshotVariant.Saints or WayStationCollectionSnapshotVariant.SaintsHover
					=> WayStationCollectionTab.Saints,
				WayStationCollectionSnapshotVariant.Equipment or WayStationCollectionSnapshotVariant.EquipmentHover
					=> WayStationCollectionTab.Equipment,
				_ => WayStationCollectionTab.Cards,
			};
			state.ActiveCardFilter = WayStationCollectionCardFilter.All;
			state.CardScrollOffset = 0;
			state.SaintListScrollOffset = 0;
			state.SaintDetailScrollOffset = 0;
			state.EquipmentScrollOffset = 0;

			foreach (var entity in ctx.World.EntityManager.GetEntitiesWithComponent<WayStationCollectionMotion>())
			{
				var motion = entity.GetComponent<WayStationCollectionMotion>();
				motion.Hover = motion.TargetHover = 0f;
				motion.Scale = motion.TargetScale = 1f;
				motion.FanAngle = motion.TargetFanAngle = 0f;
				motion.Glow = motion.TargetGlow = 0f;
				motion.MeterProgress = motion.TargetMeterProgress = GetMeterTarget(root, state);
				if (entity.GetComponent<UIElement>() is UIElement ui) ui.IsHovered = false;
			}

			if (_variant == WayStationCollectionSnapshotVariant.CardsHover)
			{
				SetFirstHovered<WayStationCollectionCardStackPresentation>(ctx, motion =>
				{
					motion.Hover = motion.TargetHover = 1f;
					motion.FanAngle = motion.TargetFanAngle = 1f;
					motion.Glow = motion.TargetGlow = 1f;
				});
			}
			else if (_variant == WayStationCollectionSnapshotVariant.SaintsHover)
			{
				string selected = state.SelectedMedalId;
				var entity = ctx.World.EntityManager.GetEntitiesWithComponent<WayStationCollectionSaintTilePresentation>()
					.FirstOrDefault(item => item.GetComponent<WayStationCollectionSaintTilePresentation>().MedalId != selected);
				SetHovered(entity);
			}
			else if (_variant == WayStationCollectionSnapshotVariant.EquipmentHover)
			{
				SetFirstHovered<WayStationCollectionEquipmentTilePresentation>(ctx, motion =>
				{
					motion.Hover = motion.TargetHover = 1f;
					motion.Scale = motion.TargetScale = 1.1f;
					motion.Glow = motion.TargetGlow = 1f;
				});
			}
		}

		private static float GetMeterTarget(Entity root, WayStationCollectionModalState state)
		{
			var catalog = root.GetComponent<WayStationCollectionCatalogComponent>()?.Catalog;
			(int unlocked, int total) = state.ActiveTab switch
			{
				WayStationCollectionTab.Saints => (catalog?.Saints.Count ?? 0, catalog?.SaintTotal ?? 0),
				WayStationCollectionTab.Equipment => (catalog?.Equipment.Count ?? 0, catalog?.EquipmentTotal ?? 0),
				_ => (catalog?.Cards.Count ?? 0, catalog?.CardTotal ?? 0),
			};
			return total == 0 ? 0f : unlocked / (float)total;
		}

		private static void SetFirstHovered<T>(
			DisplaySnapshotContext ctx,
			System.Action<WayStationCollectionMotion> configure)
			where T : class, IComponent
		{
			var entity = ctx.World.EntityManager.GetEntitiesWithComponent<T>().FirstOrDefault();
			if (entity == null) return;
			if (entity.GetComponent<UIElement>() is UIElement ui) ui.IsHovered = true;
			configure(entity.GetComponent<WayStationCollectionMotion>());
		}

		private static void SetHovered(Entity entity)
		{
			if (entity == null) return;
			if (entity.GetComponent<UIElement>() is UIElement ui) ui.IsHovered = true;
			var motion = entity.GetComponent<WayStationCollectionMotion>();
			if (motion == null) return;
			motion.Hover = motion.TargetHover = 1f;
			motion.Scale = motion.TargetScale = 1.1f;
			motion.Glow = motion.TargetGlow = 1f;
		}
	}
}
