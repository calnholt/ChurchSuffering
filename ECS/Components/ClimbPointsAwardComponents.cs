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

public sealed class ClimbPointsAwardOverlayState : IComponent
{
	public Entity Owner { get; set; }
	public ClimbPointsAwardOverlayPhase Phase { get; set; } = ClimbPointsAwardOverlayPhase.Hidden;
	public bool IsAuthoritative { get; set; }
	public int TimeReached { get; set; }
	public bool Abandoned { get; set; }
	public bool CompletedFinalBoss { get; set; }
	public int PointsAwarded { get; set; }
	public float ElapsedSeconds { get; set; }
	public float ExitElapsedSeconds { get; set; }

	public bool IsOpen => Phase != ClimbPointsAwardOverlayPhase.Hidden;
	public bool CanDismiss => Phase == ClimbPointsAwardOverlayPhase.Ready;
}
