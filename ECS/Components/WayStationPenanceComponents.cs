using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.RunSetup;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Components
{
	public enum WayStationPenanceModalPhase { Hidden, Entering, Visible, Exiting }
	public enum WayStationPenanceMotionRole { Root, Masthead, Rule, Close, Weapon, TrackLabel, Track, Node, Fill, Tally, Footer }
	public enum WayStationPenanceTransitionKind { None, WeaponFlash, Ignite, Extinguish, CountBump, EnterTally, ExitTally, FooterSwap, NodeRipple, FillSweep }

	public sealed class WayStationPenanceModalState : IComponent
	{
		public Entity Owner { get; set; }
		public bool RequestedVisible { get; set; }
		public WayStationPenanceModalPhase Phase { get; set; } = WayStationPenanceModalPhase.Hidden;
		public float ElapsedSeconds { get; set; }
		public bool InteractionEnabled { get; set; }
	}

	public sealed class WayStationPenanceMastheadPresentation : IComponent
	{
		public Entity Owner { get; set; }
		public Rectangle Bounds { get; set; }
	}

	public sealed class WayStationPenanceWeaponPresentation : IComponent
	{
		public Entity Owner { get; set; }
		public StartingWeapon Weapon { get; set; }
		public int HighestUnlockedLevel { get; set; }
		public bool IsUnlocked { get; set; }
		public bool IsSelected { get; set; }
	}

	public sealed class WayStationPenanceTrackPresentation : IComponent
	{
		public Entity Owner { get; set; }
		public Rectangle LabelBounds { get; set; }
		public Rectangle FrameBounds { get; set; }
		public float FillWidth { get; set; }
	}

	public sealed class WayStationPenanceNodePresentation : IComponent
	{
		public Entity Owner { get; set; }
		public int Level { get; set; }
		public PenanceType Type { get; set; }
		public bool IsUnlocked { get; set; }
		public bool IsActive { get; set; }
		public bool IsCurrent { get; set; }
	}

	public sealed class WayStationPenanceTallyPresentation : IComponent
	{
		public Entity Owner { get; set; }
		public PenanceType Type { get; set; }
		public int CurrentCount { get; set; }
		public int DisplayedCount { get; set; }
		public bool IsActive { get; set; }
		public Rectangle Bounds { get; set; }
	}

	public sealed class WayStationPenanceFooterPresentation : IComponent
	{
		public Entity Owner { get; set; }
		public Rectangle DepartBounds { get; set; }
		public Rectangle SummaryBounds { get; set; }
		public Rectangle CloseBounds { get; set; }
		public string Summary { get; set; } = string.Empty;
	}

	public sealed class WayStationPenanceMotion : IComponent
	{
		public Entity Owner { get; set; }
		public WayStationPenanceMotionRole Role { get; set; }
		public int Index { get; set; }
		public float Opacity { get; set; } = 1f;
		public Vector2 Offset { get; set; }
		public float Scale { get; set; } = 1f;
		public float WidthProgress { get; set; } = 1f;
		public float Glow { get; set; }
		public WayStationPenanceTransitionKind TransitionKind { get; set; }
		public float TransitionElapsed { get; set; }
		public float TransitionDelay { get; set; }
		public float TransitionDuration { get; set; }
		public float StartValue { get; set; }
		public float TargetValue { get; set; }
		public float AnimatedValue { get; set; } = -1f;
	}
}
