using Crusaders30XX.ECS.Core;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Components
{
	public class EquipmentDisplayRoot : IComponent
	{
		public Entity Owner { get; set; }
	}

	public class EquipmentTooltipState : IComponent
	{
		public Entity Owner { get; set; }
		public Entity EquipmentEntity { get; set; }
		public Entity AnchorEntity { get; set; }
		public float Alpha01 { get; set; }
		public bool TargetVisible { get; set; }
		public Rectangle Bounds { get; set; }
	}

	/// <summary>
	/// Routes a UI hover region to an existing equipped item for a named equipment tooltip.
	/// </summary>
	public class EquipmentTooltipSource : IComponent
	{
		public Entity Owner { get; set; }
		public Entity EquipmentEntity { get; set; }
		public string TooltipEntityName { get; set; } = string.Empty;
	}
}
