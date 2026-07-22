using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
	public class AboundingGrace : CardBase
	{
		private int GraceGained = 2;
		private int GraceGainedUpgrade = 1;

		public AboundingGrace()
		{
			CardId = CardIds.AboundingGrace.ToKey();
			Name = "Abounding Grace";
			Target = "Player";
			Text = $"Gain {GetGraceGained(IsUpgraded)} grace.";
			Cost = [];
			IsFreeAction = true;
			VisualEffectRecipe = HolySupportEffect();
			Type = CardType.Prayer;
			Block = 2;

			OnPlay = (entityManager, card) =>
			{
				EventManager.Publish(new ApplyPassiveEvent
				{
					Target = entityManager.GetEntity("Player"),
					Type = AppliedPassiveType.Grace,
					Delta = GetGraceGained(IsUpgraded)
				});
			};

			OnUpgrade = (entityManager, card) =>
			{
				Text = $"Gain {GetGraceGained(IsUpgraded)} grace.";
			};
		}

		private int GetGraceGained(bool isUpgraded)
		{
			return isUpgraded ? GraceGained + GraceGainedUpgrade : GraceGained;
		}
	}
}
