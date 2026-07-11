using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Objects.Equipment;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Services;

public static class EquipmentArtService
{
	public static string GetAssetName(EquipmentBase equipment)
	{
		if (equipment == null) return string.Empty;
		return GetAssetName(equipment.Id);
	}

	public static string GetAssetName(string equipmentId)
	{
		return string.IsNullOrWhiteSpace(equipmentId) ? string.Empty : "Equipment/" + equipmentId;
	}

	public static Texture2D GetTexture(ImageAssetService imageAssets, EquipmentBase equipment)
	{
		if (imageAssets == null || equipment == null) return null;
		return imageAssets.TryGetTexture(GetAssetName(equipment))
			?? imageAssets.TryGetTexture(equipment.Slot.ToString().ToLowerInvariant());
	}

	public static Texture2D GetTexture(ImageAssetService imageAssets, string equipmentId, EquipmentSlot fallbackSlot)
	{
		if (imageAssets == null) return null;
		return imageAssets.TryGetTexture(GetAssetName(equipmentId))
			?? imageAssets.TryGetTexture(fallbackSlot.ToString().ToLowerInvariant());
	}
}
