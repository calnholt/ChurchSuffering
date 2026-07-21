using System.Collections.Generic;
using ChurchSuffering.ECS.Core;

namespace ChurchSuffering.ECS.Components
{
	public enum TutorialAction
	{
		AssignBlock,
		ConfirmBlocks,
		PlayCard,
		PledgeCard,
		PayCost,
		EndTurn,
	}

	public class GuidedTutorial : IComponent
	{
		public Entity Owner { get; set; }
		public int Section { get; set; } = 1;
		public int TurnWithinSection { get; set; } = 1;
		public int PlayerHp { get; set; } = 1;
		public bool StockHandPrepared { get; set; }
		public bool IsCompleted { get; set; }
		public bool IsRestart { get; set; }

		public int BaselineCourage { get; set; }
		public int BaselineTemperance { get; set; }

		public HashSet<string> SessionSeenTeaches { get; set; } = new();
		public List<string> BlockedCardIdsThisTurn { get; set; } = new();
		public int ConfirmedAttackCountThisTurn { get; set; }
	}

	public class StockHand : IComponent
	{
		public Entity Owner { get; set; }
		public int Section { get; set; }
		public int TurnWithinSection { get; set; }
	}

	public class TutorialEnemy : IComponent
	{
		public Entity Owner { get; set; }
	}
}
