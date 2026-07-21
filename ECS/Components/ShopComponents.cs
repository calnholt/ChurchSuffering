using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Objects.Cards;

namespace ChurchSuffering.ECS.Components
{
	public enum ForSaleItemType
	{
		Card,
		Medal,
		Equipment,
		Weapon
	}

	public class ForSaleItem : IComponent
	{
		public Entity Owner { get; set; }

		public string Id { get; set; } = string.Empty;
		public ForSaleItemType ItemType { get; set; } = ForSaleItemType.Card;
		public int Price { get; set; } = 0;
		public bool IsPurchased { get; set; } = false;
		public string DisplayName { get; set; } = string.Empty;
		public string SourceShopName { get; set; } = string.Empty;
		public string ShopId { get; set; } = string.Empty;
		public int ShopSlotIndex { get; set; } = -1;
		public CardData.CardColor? CardColor { get; set; }
		public float DisplayRotationDeg { get; set; }
	}
}





