using ChurchSuffering.ECS.Core;

namespace ChurchSuffering.ECS.Events
{
	public class SetShopTitle
	{
		public string Title { get; set; } = "Shop";
		public string ShopId { get; set; } = string.Empty;
		public string BackgroundAsset { get; set; } = string.Empty;
	}

	public class OpenRunShopRequested
	{
		public Entity Entity { get; set; }
		public string ShopId { get; set; } = string.Empty;
	}
}


