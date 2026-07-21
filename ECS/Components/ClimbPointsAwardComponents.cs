using System;
using Crusaders30XX.ECS.Core;

namespace Crusaders30XX.ECS.Components;

public enum ClimbPointsAwardOverlayPhase
{
	Hidden,
	AwaitingTransition,
	Playing,
	Ready,
	Exiting,
}

[Flags]
public enum ClimbPointsAwardRumbleFinaleFlags
{
	None = 0,
	CrestReveal = 1 << 0,
	CountUpComplete = 1 << 1,
}

public sealed class ClimbPointsAwardOverlayState : IComponent
{
	public Entity Owner { get; set; }
	public ClimbPointsAwardOverlayPhase Phase { get; set; } = ClimbPointsAwardOverlayPhase.Hidden;
	public bool IsAuthoritative { get; set; }
	public int TimeReached { get; set; }
	public int ShopRefreshInterval { get; set; } = 8;
	public bool Abandoned { get; set; }
	public bool CompletedFinalBoss { get; set; }
	public int PointsAwarded { get; set; }
	public float ElapsedSeconds { get; set; }
	public float PreviousElapsedSeconds { get; set; }
	public float ExitElapsedSeconds { get; set; }
	public ClimbPointsAwardRumbleFinaleFlags RumbleFinaleFlags { get; set; }

	public bool IsOpen => Phase != ClimbPointsAwardOverlayPhase.Hidden;
	public bool CanDismiss => Phase == ClimbPointsAwardOverlayPhase.Ready;
}
