using System.Collections.Generic;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Objects.Equipment;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using static ChurchSuffering.ECS.Components.CardData;

namespace ChurchSuffering.ECS.Components
{
	public enum WayStationCollectionTab
	{
		Cards,
		Saints,
		Equipment,
	}

	public enum WayStationCollectionCardFilter
	{
		All,
		Attack,
		Block,
		Prayer,
		Weapon,
	}

	public class WayStationCollectionModalState : IComponent
	{
		public Entity Owner { get; set; }
		public WayStationCollectionTab ActiveTab { get; set; } = WayStationCollectionTab.Cards;
		public WayStationCollectionCardFilter ActiveCardFilter { get; set; } = WayStationCollectionCardFilter.All;
		public int CardScrollOffset { get; set; }
		public int SaintListScrollOffset { get; set; }
		public int SaintDetailScrollOffset { get; set; }
		public int EquipmentScrollOffset { get; set; }
		public string SelectedMedalId { get; set; } = string.Empty;
	}

	public class WayStationCollectionCatalogComponent : IComponent
	{
		public Entity Owner { get; set; }
		public WayStationCollectionCatalog Catalog { get; set; } = WayStationCollectionCatalog.Empty;
	}

	public class WayStationCollectionModalLayout : IComponent
	{
		public Entity Owner { get; set; }
		public Rectangle Shell { get; set; }
		public Rectangle Header { get; set; }
		public Rectangle TabRow { get; set; }
		public Rectangle Body { get; set; }
		public Rectangle Footer { get; set; }
		public Rectangle CloseButton { get; set; }
		public Rectangle ActivePanel { get; set; }
		public Rectangle CardFilterRow { get; set; }
		public Rectangle CardGridClip { get; set; }
		public Rectangle SaintWall { get; set; }
		public Rectangle SaintToolbar { get; set; }
		public Rectangle SaintListClip { get; set; }
		public Rectangle SaintDetail { get; set; }
		public Rectangle SaintDetailClip { get; set; }
		public Rectangle EquipmentHall { get; set; }
		public Rectangle EquipmentHeader { get; set; }
		public Rectangle EquipmentContentClip { get; set; }
		public Rectangle FooterMeter { get; set; }
		public Vector2 FooterLabelAnchor { get; set; }
		public Vector2 FooterCountAnchor { get; set; }
		public float CardScale { get; set; } = 1f;
	}

	public class WayStationCollectionModalRoot : IComponent
	{
		public Entity Owner { get; set; }
	}

	public class WayStationCollectionModalShell : IComponent
	{
		public Entity Owner { get; set; }
	}

	public class WayStationCollectionModalCloseButton : IComponent
	{
		public Entity Owner { get; set; }
	}

	public class WayStationCollectionScrollBlocker : IComponent
	{
		public Entity Owner { get; set; }
	}

	public class WayStationCollectionTabPresentation : IComponent
	{
		public Entity Owner { get; set; }
		public WayStationCollectionTab Tab { get; set; }
		public Rectangle Bounds { get; set; }
	}

	public class WayStationCollectionFilterPresentation : IComponent
	{
		public Entity Owner { get; set; }
		public WayStationCollectionCardFilter Filter { get; set; }
		public Rectangle Bounds { get; set; }
	}

	public class WayStationCollectionCardStackPresentation : IComponent
	{
		public Entity Owner { get; set; }
		public string CardId { get; set; } = string.Empty;
		public bool IsWeapon { get; set; }
		public Rectangle Bounds { get; set; }
		public CardColor FrontColor { get; set; } = CardColor.White;
		public CardColor? PendingFrontColor { get; set; }
		public float ColorSwitchProgress { get; set; } = 1f;
		public bool ShowUpgradePreview { get; set; }
		public Entity WhiteCard { get; set; }
		public Entity RedCard { get; set; }
		public Entity BlackCard { get; set; }
		public Entity UpgradedWhiteCard { get; set; }
		public Entity UpgradedRedCard { get; set; }
		public Entity UpgradedBlackCard { get; set; }

		public IEnumerable<Entity> PreviewCards
		{
			get
			{
				yield return WhiteCard;
				yield return RedCard;
				yield return BlackCard;
				yield return UpgradedWhiteCard;
				yield return UpgradedRedCard;
				yield return UpgradedBlackCard;
			}
		}
	}

	public class WayStationCollectionPreviewCard : IComponent
	{
		public Entity Owner { get; set; }
		public string CardId { get; set; } = string.Empty;
		public CardColor Color { get; set; }
		public bool IsUpgraded { get; set; }
	}

	public class WayStationCollectionSaintTilePresentation : IComponent
	{
		public Entity Owner { get; set; }
		public string MedalId { get; set; } = string.Empty;
		public Rectangle Bounds { get; set; }
		public bool IsSelected { get; set; }
	}

	public class WayStationCollectionEquipmentTilePresentation : IComponent
	{
		public Entity Owner { get; set; }
		public string EquipmentId { get; set; } = string.Empty;
		public EquipmentSlot Slot { get; set; }
		public Rectangle Bounds { get; set; }
		public Rectangle ArtBounds { get; set; }
		public int ContentHeight { get; set; }
	}

	public class WayStationCollectionMotion : IComponent
	{
		public Entity Owner { get; set; }
		public float Hover { get; set; }
		public float TargetHover { get; set; }
		public float Scale { get; set; } = 1f;
		public float TargetScale { get; set; } = 1f;
		public float FanAngle { get; set; }
		public float TargetFanAngle { get; set; }
		public float Glow { get; set; }
		public float TargetGlow { get; set; }
		public float MeterProgress { get; set; }
		public float TargetMeterProgress { get; set; }
	}
}
