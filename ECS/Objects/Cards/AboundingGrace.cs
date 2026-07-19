using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
	public class AboundingGrace : CardBase
	{
		private int GraceGained = 2;
		private int GraceGainedUpgrade = 1;

		public AboundingGrace()
		{
			CardId = "abounding_grace";
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
