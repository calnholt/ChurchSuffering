using System.Collections.Generic;
using ChurchSuffering.ECS.Objects.Equipment;

namespace ChurchSuffering.ECS.Services
{
	public static class EquipmentService
	{
		public static string GetTooltipText(EquipmentBase equipment, EquipmentTooltipType type = EquipmentTooltipType.Battle)
		{
			if (equipment == null) return string.Empty;

			var sections = new List<string>();
			if (!string.IsNullOrWhiteSpace(equipment.Name))
			{
				sections.Add(equipment.Name);
			}
			if (!string.IsNullOrWhiteSpace(equipment.Text))
			{
				sections.Add(equipment.Text);
			}
			if (!string.IsNullOrWhiteSpace(equipment.FlavorText))
			{
				sections.Add(equipment.FlavorText);
			}
			if (type == EquipmentTooltipType.Shop && equipment.Block > 0)
			{
				sections.Add($"Block: {equipment.Block}");
			}
			if (equipment.CanActivateDuringActionPhase)
			{
				sections.Add("Free Action");
			}
			return string.Join("\n\n", sections);
		}
	}


  public enum EquipmentTooltipType
  {
    Battle,
    Shop,
  }
}
