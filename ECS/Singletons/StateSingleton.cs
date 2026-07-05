using System;

namespace Crusaders30XX.ECS.Systems
{
	public static class StateSingleton
	{
		public static bool IsActive { get; set; } = false;
		public static bool HasPendingLocationPoiReveal { get; set; } = false;
		public static string PendingPoiId { get; set; } = null;
		public static bool PreventClicking { get; set; } = false;
		public static bool IsTutorialActive { get; set; } = false;
		public static bool IsPledgeEnabled { get; set; } = true;
		public static float LocationMapZoom { get; set; } = 0.75f;
	}
}

