using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.RunSetup;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
	[DebugTab("WayStation Penance Layout")]
	public sealed class WayStationClimbSettingsModalSystem : Core.System
	{
		private static readonly StartingWeapon[] Weapons = Enum.GetValues<StartingWeapon>();
		private static readonly PenanceType[] TallyOrder =
		{
			PenanceType.Fasting,
			PenanceType.Mortification,
			PenanceType.Abstinence,
			PenanceType.PenitentialPilgrimage,
			PenanceType.Reparation,
		};

		private readonly World _world;
		private bool _departInProgress;

		[DebugEditable(DisplayName = "Outer Padding X", Step = 1, Min = 0, Max = 240)] public int OuterPaddingX { get; set; } = 90;
		[DebugEditable(DisplayName = "Outer Padding Top", Step = 1, Min = 0, Max = 120)] public int OuterPaddingTop { get; set; } = 26;
		[DebugEditable(DisplayName = "Outer Padding Bottom", Step = 1, Min = 0, Max = 120)] public int OuterPaddingBottom { get; set; } = 24;
		[DebugEditable(DisplayName = "Masthead Height", Step = 1, Min = 80, Max = 240)] public int MastheadHeight { get; set; } = 150;
		[DebugEditable(DisplayName = "Weapons Row Height", Step = 1, Min = 240, Max = 500)] public int WeaponsRowHeight { get; set; } = 380;
		[DebugEditable(DisplayName = "Footer Height", Step = 1, Min = 70, Max = 180)] public int FooterHeight { get; set; } = 108;
		[DebugEditable(DisplayName = "Weapon Width", Step = 1, Min = 160, Max = 360)] public int WeaponWidth { get; set; } = 250;
		[DebugEditable(DisplayName = "Weapon Height", Step = 1, Min = 220, Max = 430)] public int WeaponHeight { get; set; } = 330;
		[DebugEditable(DisplayName = "Weapon Gap", Step = 1, Min = 0, Max = 100)] public int WeaponGap { get; set; } = 30;
		[DebugEditable(DisplayName = "Node Size", Step = 1, Min = 16, Max = 60)] public int NodeSize { get; set; } = 30;
		[DebugEditable(DisplayName = "Node Gap", Step = 1, Min = 0, Max = 40)] public int NodeGap { get; set; } = 17;
		[DebugEditable(DisplayName = "Track Padding X", Step = 1, Min = 0, Max = 80)] public int TrackPaddingX { get; set; } = 34;
		[DebugEditable(DisplayName = "Track Padding Y", Step = 1, Min = 0, Max = 80)] public int TrackPaddingY { get; set; } = 30;
		[DebugEditable(DisplayName = "Zone Gap", Step = 1, Min = 0, Max = 80)] public int TrackZoneGap { get; set; } = 30;

		private readonly record struct Layout(
			Rectangle Masthead,
			Rectangle Weapons,
			Rectangle TrackZone,
			Rectangle Footer,
			Rectangle TrackLabel,
			Rectangle TrackFrame,
			Rectangle Depart,
			Rectangle Summary,
			Rectangle Close);

		public WayStationClimbSettingsModalSystem(World world) : base(world.EntityManager)
		{
			_world = world;
			EventManager.Subscribe<LoadSceneEvent>(OnLoadScene);
			EventManager.Subscribe<OpenWayStationClimbSettingsModalEvent>(OnOpenModal);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var scene = entity.GetComponent<SceneState>();
			if (scene?.Current is not (SceneId.WayStation or SceneId.Snapshot))
			{
				CloseImmediate();
				return;
			}

			EnsureEntities();
			var state = GetModalState();
			var meta = SaveCache.GetWayStationMeta();
			NormalizeSelection(meta);
			Reconcile(meta, ComputeLayout());
			SyncInteraction(state);
			if (state?.InteractionEnabled != true) return;

			if (WasClicked(WayStationSceneConstants.CloseButtonName))
			{
				RequestClose();
				return;
			}

			foreach (var weapon in Weapons)
			{
				if (WasClicked(WeaponEntityName(weapon))) SelectWeapon(meta, weapon);
			}

			for (int level = 1; level <= PenanceRules.MaxLevel; level++)
			{
				if (WasClicked(NodeEntityName(level))) SelectLevel(meta, level);
			}

			if (!_departInProgress && WasClicked(WayStationSceneConstants.DepartButtonName))
			{
				NormalizeSelection(meta);
				_departInProgress = true;
				WayStationRunSetupService.Depart(_world);
			}
		}

		private void OnLoadScene(LoadSceneEvent evt)
		{
			if (evt.Scene != SceneId.WayStation) return;
			_departInProgress = false;
			CloseImmediate();
		}

		private void OnOpenModal(OpenWayStationClimbSettingsModalEvent evt)
		{
			if (!IsWayStationActive()) return;
			var meta = SaveCache.GetWayStationMeta();
			var setup = WayStationRunSetupService.GetRunSetup(EntityManager);
			if (!ClimbUnlockProgressionRules.ShouldShowSettingsModal(meta))
			{
				if (_departInProgress || setup == null) return;
				setup.SelectedWeapon = StartingWeapon.Sword;
				setup.SelectedPenanceLevel = 0;
				_departInProgress = true;
				WayStationRunSetupService.Depart(_world);
				return;
			}

			EnsureEntities();
			NormalizeSelection(meta);
			setup.SelectedPenanceLevel = ClimbUnlockProgressionRules.GetHighestUnlockedPenance(
				meta,
				setup.SelectedWeapon);
			Reconcile(meta, ComputeLayout());
			GetModalState().RequestedVisible = true;
			EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.ClimbMenuEnter, Volume = 0.5f });
		}

		private void SelectWeapon(WayStationMetaSave meta, StartingWeapon weapon)
		{
			var setup = WayStationRunSetupService.GetRunSetup(EntityManager);
			if (setup == null || setup.SelectedWeapon == weapon || !ClimbUnlockProgressionRules.IsWeaponUnlocked(meta, weapon)) return;
			int oldLevel = setup.SelectedPenanceLevel;
			setup.SelectedWeapon = weapon;
			setup.SelectedPenanceLevel = ClimbUnlockProgressionRules.GetHighestUnlockedPenance(meta, weapon);
			Reconcile(meta, ComputeLayout());
			ClearChangedTooltips();
			EventManager.Publish(new WayStationPenanceSelectionChangedEvent
			{
				OldLevel = oldLevel,
				NewLevel = setup.SelectedPenanceLevel,
				WeaponChanged = true,
			});
		}

		private void SelectLevel(WayStationMetaSave meta, int requestedLevel)
		{
			var setup = WayStationRunSetupService.GetRunSetup(EntityManager);
			if (setup == null || !ClimbUnlockProgressionRules.IsPenanceUnlocked(meta, setup.SelectedWeapon, requestedLevel)) return;
			int oldLevel = setup.SelectedPenanceLevel;
			int newLevel = requestedLevel == oldLevel ? Math.Max(0, oldLevel - 1) : requestedLevel;
			if (newLevel == oldLevel) return;
			setup.SelectedPenanceLevel = newLevel;
			Reconcile(meta, ComputeLayout());
			ClearChangedTooltips();
			EventManager.Publish(new WayStationPenanceSelectionChangedEvent
			{
				OldLevel = oldLevel,
				NewLevel = newLevel,
				WeaponChanged = false,
			});
		}

		private void NormalizeSelection(WayStationMetaSave meta)
		{
			var setup = WayStationRunSetupService.GetRunSetup(EntityManager);
			if (setup == null) return;
			if (!ClimbUnlockProgressionRules.IsWeaponUnlocked(meta, setup.SelectedWeapon))
			{
				setup.SelectedWeapon = StartingWeapon.Sword;
			}
			int highest = ClimbUnlockProgressionRules.GetHighestUnlockedPenance(meta, setup.SelectedWeapon);
			setup.SelectedPenanceLevel = Math.Clamp(setup.SelectedPenanceLevel, 0, highest);
		}

		private void EnsureEntities()
		{
			var root = EnsureEntity(WayStationSceneConstants.ModalRootName);
			EnsureComponent(root, new WayStationClimbModalRoot());
			EnsureComponent(root, new WayStationPenanceModalState());
			EnsureComponent(root, new WayStationPenanceMotion { Role = WayStationPenanceMotionRole.Root });
			// The root owns the active input context but is intentionally not a hit target.
			// A fullscreen UIElement here would win equal-Z hit tests and swallow every
			// child hover/click, making any click look like a backdrop dismissal.
			if (root.GetComponent<UIElement>() != null) EntityManager.RemoveComponent<UIElement>(root);
			InputContextService.EnsureContext(EntityManager, root, WayStationSceneConstants.ModalContextId, 100, false);

			var masthead = EnsureEntity(WayStationSceneConstants.MastheadName);
			EnsureComponent(masthead, new WayStationPenanceMastheadPresentation());
			EnsureComponent(masthead, new WayStationPenanceMotion { Role = WayStationPenanceMotionRole.Masthead });
			var rule = EnsureEntity(WayStationSceneConstants.MastheadRuleName);
			EnsureComponent(rule, new WayStationPenanceMotion { Role = WayStationPenanceMotionRole.Rule });

			for (int index = 0; index < Weapons.Length; index++)
			{
				var weapon = Weapons[index];
				var weaponEntity = EnsureEntity(WeaponEntityName(weapon));
				EnsureComponent(weaponEntity, new WayStationClimbModalWeaponChoice { Weapon = weapon });
				EnsureComponent(weaponEntity, new WayStationPenanceWeaponPresentation { Weapon = weapon });
				EnsureComponent(weaponEntity, new WayStationPenanceMotion { Role = WayStationPenanceMotionRole.Weapon, Index = index });
				EnsureUi(weaponEntity);
			}

			var track = EnsureEntity(WayStationSceneConstants.TrackName);
			EnsureComponent(track, new WayStationPenanceTrackPresentation());
			EnsureComponent(track, new WayStationPenanceMotion { Role = WayStationPenanceMotionRole.Track });
			EnsureComponent(EnsureEntity(WayStationSceneConstants.TrackLabelName), new WayStationPenanceMotion { Role = WayStationPenanceMotionRole.TrackLabel });
			EnsureComponent(EnsureEntity(WayStationSceneConstants.TrackFillName), new WayStationPenanceMotion { Role = WayStationPenanceMotionRole.Fill });

			for (int level = 1; level <= PenanceRules.MaxLevel; level++)
			{
				var node = EnsureEntity(NodeEntityName(level));
				EnsureComponent(node, new WayStationPenanceNodePresentation { Level = level, Type = PenanceRules.Order[level - 1] });
				EnsureComponent(node, new WayStationPenanceMotion { Role = WayStationPenanceMotionRole.Node, Index = level - 1 });
				EnsureUi(node);
			}

			for (int index = 0; index < TallyOrder.Length; index++)
			{
				var tally = EnsureEntity(TallyEntityName(TallyOrder[index]));
				EnsureComponent(tally, new WayStationPenanceTallyPresentation { Type = TallyOrder[index] });
				EnsureComponent(tally, new WayStationPenanceMotion { Role = WayStationPenanceMotionRole.Tally, Index = index });
				EnsureUi(tally);
			}

			var footer = EnsureEntity(WayStationSceneConstants.FooterName);
			EnsureComponent(footer, new WayStationPenanceFooterPresentation());
			EnsureComponent(footer, new WayStationPenanceMotion { Role = WayStationPenanceMotionRole.Footer });
			var depart = EnsureEntity(WayStationSceneConstants.DepartButtonName);
			EnsureComponent(depart, new WayStationClimbModalDepartButton());
			EnsureUi(depart);
			var close = EnsureEntity(WayStationSceneConstants.CloseButtonName);
			EnsureComponent(close, new WayStationClimbModalCloseButton());
			EnsureComponent(close, new WayStationPenanceMotion { Role = WayStationPenanceMotionRole.Close });
			EnsureUi(close);
		}

		private void Reconcile(WayStationMetaSave meta, Layout layout)
		{
			var setup = WayStationRunSetupService.GetRunSetup(EntityManager);
			if (setup == null) return;
			var calculation = PenanceRules.Calculate(setup.SelectedPenanceLevel);

			EntityManager.GetEntity(WayStationSceneConstants.MastheadName)
				.GetComponent<WayStationPenanceMastheadPresentation>().Bounds = layout.Masthead;

			var unlockedWeapons = Weapons.Where(weapon => ClimbUnlockProgressionRules.IsWeaponUnlocked(meta, weapon)).ToArray();
			int rowWidth = unlockedWeapons.Length * WeaponWidth + Math.Max(0, unlockedWeapons.Length - 1) * WeaponGap;
			int weaponX = layout.Weapons.Center.X - rowWidth / 2;
			for (int index = 0; index < Weapons.Length; index++)
			{
				var weapon = Weapons[index];
				var entity = EntityManager.GetEntity(WeaponEntityName(weapon));
				var presentation = entity.GetComponent<WayStationPenanceWeaponPresentation>();
				presentation.IsUnlocked = ClimbUnlockProgressionRules.IsWeaponUnlocked(meta, weapon);
				presentation.HighestUnlockedLevel = ClimbUnlockProgressionRules.GetHighestUnlockedPenance(meta, weapon);
				presentation.IsSelected = setup.SelectedWeapon == weapon;
				int unlockedIndex = Array.IndexOf(unlockedWeapons, weapon);
				var bounds = presentation.IsUnlocked
					? new Rectangle(weaponX + unlockedIndex * (WeaponWidth + WeaponGap), layout.Weapons.Center.Y - WeaponHeight / 2, WeaponWidth, WeaponHeight)
					: Rectangle.Empty;
				SyncUi(entity, bounds, presentation.IsUnlocked, string.Empty, TooltipType.None);
			}

			var track = EntityManager.GetEntity(WayStationSceneConstants.TrackName).GetComponent<WayStationPenanceTrackPresentation>();
			track.LabelBounds = layout.TrackLabel;
			track.FrameBounds = layout.TrackFrame;
			track.FillWidth = setup.SelectedPenanceLevel <= 0 ? 0f : (setup.SelectedPenanceLevel - 1) * (NodeSize + NodeGap);
			int nodeY = layout.TrackFrame.Center.Y - NodeSize / 2;
			for (int level = 1; level <= PenanceRules.MaxLevel; level++)
			{
				var entity = EntityManager.GetEntity(NodeEntityName(level));
				var node = entity.GetComponent<WayStationPenanceNodePresentation>();
				node.IsUnlocked = level <= ClimbUnlockProgressionRules.GetHighestUnlockedPenance(meta, setup.SelectedWeapon);
				node.IsActive = level <= setup.SelectedPenanceLevel;
				node.IsCurrent = level == setup.SelectedPenanceLevel && setup.SelectedPenanceLevel > 0;
				var bounds = new Rectangle(layout.TrackFrame.X + TrackPaddingX + (level - 1) * (NodeSize + NodeGap), nodeY, NodeSize, NodeSize);
				SyncUi(entity, bounds, node.IsUnlocked, BuildNodeTooltip(node, setup.SelectedWeapon), TooltipType.Text);
			}

			var activeTallies = new List<(Entity Entity, WayStationPenanceTallyPresentation Tally, WayStationPenanceMotion Motion, int FullWidth)>();
			for (int index = 0; index < TallyOrder.Length; index++)
			{
				var type = TallyOrder[index];
				var entity = EntityManager.GetEntity(TallyEntityName(type));
				var tally = entity.GetComponent<WayStationPenanceTallyPresentation>();
				var motion = entity.GetComponent<WayStationPenanceMotion>();
				tally.CurrentCount = calculation.GetStackCount(type);
				tally.IsActive = tally.CurrentCount > 0;
				int width = TallyWidth(type);
				if (tally.IsActive || tally.DisplayedCount > 0 || motion.WidthProgress > 0.001f)
					activeTallies.Add((entity, tally, motion, width));
			}

			int totalWidth = activeTallies.Sum(item => (int)MathF.Round(item.FullWidth * MathHelper.Clamp(item.Motion.WidthProgress, 0f, 1f)))
				+ Math.Max(0, activeTallies.Count - 1) * 10;
			int tallyX = layout.TrackZone.Center.X - totalWidth / 2;
			int tallyY = layout.TrackFrame.Bottom + TrackZoneGap;
			foreach (var item in activeTallies)
			{
				int width = Math.Max(0, (int)MathF.Round(item.FullWidth * MathHelper.Clamp(item.Motion.WidthProgress, 0f, 1f)));
				item.Tally.Bounds = new Rectangle(tallyX, tallyY, width, 34);
				SyncUi(item.Entity, item.Tally.Bounds, item.Tally.IsActive, BuildTallyTooltip(item.Tally), TooltipType.Text);
				tallyX += width + 10;
			}

			var footer = EntityManager.GetEntity(WayStationSceneConstants.FooterName).GetComponent<WayStationPenanceFooterPresentation>();
			footer.DepartBounds = layout.Depart;
			footer.SummaryBounds = layout.Summary;
			footer.CloseBounds = layout.Close;
			string weaponName = setup.SelectedWeapon.ToString();
			footer.Summary = setup.SelectedPenanceLevel == 0
				? $"DEPART WITH THE {weaponName.ToUpperInvariant()}, UNBURDENED"
				: $"DEPART WITH THE {weaponName.ToUpperInvariant()} UNDER PENANCE {ToRoman(setup.SelectedPenanceLevel)}";
			SyncUi(EntityManager.GetEntity(WayStationSceneConstants.DepartButtonName), layout.Depart, true, string.Empty, TooltipType.None);
			SyncUi(EntityManager.GetEntity(WayStationSceneConstants.CloseButtonName), layout.Close, true, string.Empty, TooltipType.None);
		}

		private Layout ComputeLayout()
		{
			int innerHeight = Game1.VirtualHeight - OuterPaddingTop - OuterPaddingBottom;
			var masthead = new Rectangle(OuterPaddingX, OuterPaddingTop, Game1.VirtualWidth - OuterPaddingX * 2, MastheadHeight);
			var weapons = new Rectangle(OuterPaddingX, masthead.Bottom, masthead.Width, WeaponsRowHeight);
			var footer = new Rectangle(OuterPaddingX, Game1.VirtualHeight - OuterPaddingBottom - FooterHeight, masthead.Width, FooterHeight);
			var trackZone = new Rectangle(OuterPaddingX, weapons.Bottom, masthead.Width, Math.Max(1, innerHeight - MastheadHeight - WeaponsRowHeight - FooterHeight));
			int frameWidth = PenanceRules.MaxLevel * NodeSize + (PenanceRules.MaxLevel - 1) * NodeGap + TrackPaddingX * 2;
			int frameHeight = NodeSize + TrackPaddingY * 2;
			int contentHeight = 16 + TrackZoneGap + frameHeight + TrackZoneGap + 34;
			int startY = trackZone.Center.Y - contentHeight / 2;
			var label = new Rectangle(trackZone.Center.X - 210, startY, 420, 16);
			var frame = new Rectangle(trackZone.Center.X - frameWidth / 2, label.Bottom + TrackZoneGap, frameWidth, frameHeight);
			var depart = new Rectangle(footer.Center.X - 130, footer.Y + 5, 260, 64);
			var summary = new Rectangle(footer.X, depart.Bottom + 10, footer.Width, 18);
			var close = new Rectangle(Game1.VirtualWidth - 30 - 46, 26, 46, 46);
			return new Layout(masthead, weapons, trackZone, footer, label, frame, depart, summary, close);
		}

		private void SyncInteraction(WayStationPenanceModalState state)
		{
			bool draw = state?.Phase != WayStationPenanceModalPhase.Hidden;
			bool interactive = state?.InteractionEnabled == true;
			var root = EntityManager.GetEntity(WayStationSceneConstants.ModalRootName);
			InputContextService.EnsureContext(EntityManager, root, WayStationSceneConstants.ModalContextId, 100, draw);
			foreach (var entity in InteractiveEntities())
			{
				var ui = entity.GetComponent<UIElement>();
				if (ui == null) continue;
				ui.IsInteractable = interactive && !ui.Bounds.IsEmpty;
				ui.IsHidden = !draw || ui.Bounds.IsEmpty;
			}
		}

		private IEnumerable<Entity> InteractiveEntities()
		{
			foreach (var weapon in Weapons) yield return EntityManager.GetEntity(WeaponEntityName(weapon));
			for (int level = 1; level <= PenanceRules.MaxLevel; level++) yield return EntityManager.GetEntity(NodeEntityName(level));
			foreach (var type in TallyOrder) yield return EntityManager.GetEntity(TallyEntityName(type));
			yield return EntityManager.GetEntity(WayStationSceneConstants.DepartButtonName);
			yield return EntityManager.GetEntity(WayStationSceneConstants.CloseButtonName);
		}

		private Entity EnsureEntity(string name)
		{
			var entity = EntityManager.GetEntity(name) ?? EntityManager.CreateEntity(name);
			EnsureComponent(entity, new Transform { ZOrder = 10000 });
			return entity;
		}

		private void EnsureUi(Entity entity)
		{
			EnsureComponent(entity, new UIElement
			{
				Bounds = Rectangle.Empty,
				IsInteractable = false,
				IsHidden = true,
				LayerType = UILayerType.Overlay,
				ShowHoverHighlight = false,
			});
			InputContextService.EnsureMember(EntityManager, entity, WayStationSceneConstants.ModalContextId);
		}

		private void EnsureComponent<T>(Entity entity, T component) where T : class, IComponent
		{
			if (entity.GetComponent<T>() == null) EntityManager.AddComponent(entity, component);
		}

		private static void SyncUi(Entity entity, Rectangle bounds, bool enabled, string tooltip, TooltipType tooltipType)
		{
			if (entity == null) return;
			var transform = entity.GetComponent<Transform>();
			if (transform != null) transform.Position = bounds.Location.ToVector2();
			var ui = entity.GetComponent<UIElement>();
			if (ui == null) return;
			ui.Bounds = bounds;
			ui.IsInteractable = enabled && !bounds.IsEmpty;
			ui.IsHidden = bounds.IsEmpty;
			ui.LayerType = UILayerType.Overlay;
			ui.Tooltip = tooltip ?? string.Empty;
			ui.TooltipType = tooltipType;
			ui.TooltipPosition = TooltipPosition.Above;
			ui.TooltipOffsetPx = 14;
			ui.ShowHoverHighlight = false;
		}

		private void RequestClose()
		{
			var state = GetModalState();
			if (state != null) state.RequestedVisible = false;
			ClearChangedTooltips();
		}

		private void CloseImmediate()
		{
			var state = GetModalState();
			if (state == null) return;
			state.RequestedVisible = false;
			state.Phase = WayStationPenanceModalPhase.Hidden;
			state.ElapsedSeconds = 0f;
			state.InteractionEnabled = false;
			SyncInteraction(state);
		}

		private void ClearChangedTooltips()
		{
			foreach (var entity in InteractiveEntities())
			{
				var ui = entity?.GetComponent<UIElement>();
				if (ui == null) continue;
				ui.IsHovered = false;
				ui.IsClicked = false;
			}
		}

		private WayStationPenanceModalState GetModalState()
		{
			return EntityManager.GetEntity(WayStationSceneConstants.ModalRootName)?.GetComponent<WayStationPenanceModalState>();
		}

		private bool WasClicked(string name) => EntityManager.GetEntity(name)?.GetComponent<UIElement>()?.IsClicked == true;

		private bool IsWayStationActive()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault()?.GetComponent<SceneState>()?.Current == SceneId.WayStation;
		}

		private static string WeaponEntityName(StartingWeapon weapon) => weapon switch
		{
			StartingWeapon.Dagger => WayStationSceneConstants.DaggerButtonName,
			StartingWeapon.Hammer => WayStationSceneConstants.HammerButtonName,
			_ => WayStationSceneConstants.SwordButtonName,
		};

		private static string NodeEntityName(int level) => $"{WayStationSceneConstants.NodePrefix}{level}";
		private static string TallyEntityName(PenanceType type) => $"{WayStationSceneConstants.TallyPrefix}{type}";

		private static int TallyWidth(PenanceType type) => type switch
		{
			PenanceType.Fasting => 112,
			PenanceType.Mortification => 150,
			PenanceType.Abstinence => 132,
			PenanceType.PenitentialPilgrimage => 224,
			PenanceType.Reparation => 138,
			_ => 120,
		};

		private static string BuildNodeTooltip(WayStationPenanceNodePresentation node, StartingWeapon weapon)
		{
			if (!node.IsUnlocked)
			{
				return $"Penance {ToRoman(node.Level)} - Locked\nConquer Penance {ToRoman(node.Level - 1)} with the {weapon} to unlock.";
			}
			return $"Penance {ToRoman(node.Level)} - {DisplayName(node.Type)}\n{Description(node.Type)}";
		}

		private static string BuildTallyTooltip(WayStationPenanceTallyPresentation tally)
		{
			return $"{DisplayName(tally.Type)} x{tally.CurrentCount}\n{Description(tally.Type)} (applied {tally.CurrentCount} times)";
		}

		public static string DisplayName(PenanceType type) => type switch
		{
			PenanceType.PenitentialPilgrimage => "Penitential Pilgrimage",
			_ => type.ToString(),
		};

		public static string Description(PenanceType type) => type switch
		{
			PenanceType.Fasting => "The Crusader begins the climb with 1 less max HP.",
			PenanceType.Mortification => "Enemies have 5% more HP.",
			PenanceType.Abstinence => "The climb begins with 1 fewer starting resource.",
			PenanceType.PenitentialPilgrimage => "The shop takes 1 additional climb to reset its wares.",
			PenanceType.Reparation => "1 starter card is replaced with a random card bearing a random negative modification.",
			_ => string.Empty,
		};

		public static string ToRoman(int value)
		{
			if (value <= 0) return "0";
			var numerals = new (int Value, string Text)[] { (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I") };
			string result = string.Empty;
			foreach (var numeral in numerals)
			{
				while (value >= numeral.Value)
				{
					result += numeral.Text;
					value -= numeral.Value;
				}
			}
			return result;
		}
	}
}
