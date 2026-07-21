using System;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Objects.Equipment;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Services;

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

	public static Rectangle GetContainedBounds(Texture2D texture, Rectangle container)
	{
		if (texture == null || container.Width <= 0 || container.Height <= 0)
		{
			return Rectangle.Empty;
		}

		float scale = Math.Min(
			container.Width / (float)Math.Max(1, texture.Width),
			container.Height / (float)Math.Max(1, texture.Height));
		int width = Math.Max(1, (int)Math.Round(texture.Width * scale));
		int height = Math.Max(1, (int)Math.Round(texture.Height * scale));
		return new Rectangle(
			container.Center.X - width / 2,
			container.Center.Y - height / 2,
			width,
			height);
	}
}
