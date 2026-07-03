using Crusaders30XX.ECS.Components;

namespace Crusaders30XX.ECS.Events
{
	public class ChangeBattlePhaseEvent
	{
		public SubPhase Current { get; set; }
		public SubPhase Previous { get; set; }

	}

	// New: Proceed-to-next-phase intent; a coordinator will transition using PhaseState2
	public class ProceedToNextPhase { }

	public class ShowConfirmButtonEvent { }

	public class BattlePhaseAnimationCompleteEvent 
	{
		public SubPhase SubPhase;
	}

	public class ShowStartOfBattleAnimationEvent { }

	public class ShowVictoryAnimationEvent { }

	public class VictoryAnimationCompleteEvent { }

	public class ShuffleDeckAnimationRequested
	{
		public System.Guid RequestId { get; set; }
		public string Reason { get; set; } = string.Empty;
		public string TargetEntityName { get; set; } = "UI_DrawPileRoot";
	}

	public class ShuffleDeckAnimationCompleted
	{
		public System.Guid RequestId { get; set; }
	}
}
