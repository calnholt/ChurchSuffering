using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Data.RunSetup;
using ChurchSuffering.ECS.Data.Save;

namespace ChurchSuffering.ECS.Services
{
	public static class ClimbUnlockProgressionRules
	{
		public static bool ShouldShowSettingsModal(WayStationMetaSave meta)
		{
			return IsWeaponUnlocked(meta, StartingWeapon.Dagger);
		}

		public static bool IsWeaponUnlocked(WayStationMetaSave meta, StartingWeapon weapon)
		{
			string id = PenanceRules.GetWeaponId(weapon);
			return weapon == StartingWeapon.Sword
				|| meta?.highestPenanceByWeapon?.ContainsKey(id) == true;
		}

		public static int GetHighestUnlockedPenance(WayStationMetaSave meta, StartingWeapon weapon)
		{
			if (!IsWeaponUnlocked(meta, weapon)) return 0;
			string id = PenanceRules.GetWeaponId(weapon);
			return meta?.highestPenanceByWeapon != null
				&& meta.highestPenanceByWeapon.TryGetValue(id, out int level)
					? PenanceRules.ClampLevel(level)
					: 0;
		}

		public static bool IsPenanceUnlocked(WayStationMetaSave meta, StartingWeapon weapon, int level)
		{
			return IsWeaponUnlocked(meta, weapon)
				&& PenanceRules.ClampLevel(level) == level
				&& level <= GetHighestUnlockedPenance(meta, weapon);
		}

		public static IReadOnlyList<StartingWeapon> GetUnlockedWeapons(WayStationMetaSave meta)
		{
			return Enum.GetValues<StartingWeapon>()
				.Where(weapon => IsWeaponUnlocked(meta, weapon))
				.ToArray();
		}
	}
}
