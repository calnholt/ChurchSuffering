using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;

namespace Crusaders30XX.ECS.Events
{
	public class BlockAssignmentAdded
	{
		public Entity Card;
		public int DeltaBlock;
		public IReadOnlyList<CardData.CardColor> Colors = Array.Empty<CardData.CardColor>();
	}

	public class BlockAssignmentRemoved
	{
		public Entity Card;
		public int DeltaBlock;
		public IReadOnlyList<CardData.CardColor> Colors = Array.Empty<CardData.CardColor>();
	}

	public class AssignedBlockReturnCompleted
	{
		public Entity Card;
	}

	public class ReserveAssignedBlockReturnRequested
	{
		public Entity Card;
		public Entity Deck;
	}
}
