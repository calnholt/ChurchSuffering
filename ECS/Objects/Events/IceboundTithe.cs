using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.ECS.Objects.Events
{
	public class IceboundTithe : EventBase
	{
		private int GoldAmount1 = 10;
		private int GoldAmount2 = 30;
		private int GoldAmount3 = 60;
		public IceboundTithe()
		{
			Id = "icebound_tithe";
			Title = "The Icebound Tithe";
			EventText = "A frost-bound reliquary hums at the roadside. It offers coin if you surrender cards to the cold.";
		}

		public override string Option1Text => $"Offer one card to the frost for {GoldAmount1} gold";
		public override string Option2Text => $"Surrender two cards to the cold for {GoldAmount2} gold";
		public override string Option3Text => $"Yield three cards to the rime for {GoldAmount3} gold";

		public override void OnOption1(EntityManager entityManager) => Resolve(entityManager, 1, GoldAmount1);
		public override void OnOption2(EntityManager entityManager) => Resolve(entityManager, 2, GoldAmount2);
		public override void OnOption3(EntityManager entityManager) => Resolve(entityManager, 3, GoldAmount3);

		private void Resolve(EntityManager entityManager, int freezeCount, int goldAmount)
		{
			RunDeckService.EnsureRunDeck(entityManager);
			EventManager.Publish(new ApplyCardApplicationEvent
			{
				Amount = freezeCount,
				Type = CardApplicationType.Frozen,
				Target = CardApplicationTarget.Deck,
			});
			EventManager.Publish(new ModifyGoldRequestEvent { Delta = goldAmount, Reason = Id });
		}
	}
}
