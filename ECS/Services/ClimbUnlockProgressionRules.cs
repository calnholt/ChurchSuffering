using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Singletons;

namespace Crusaders30XX.ECS.Services
{
	public static class ClimbUnlockProgressionRules
	{
		public static bool ShouldShowSettingsModal(WayStationMetaSave meta)
		{
			return IsWeaponUnlocked(meta, StartingWeapon.Dagger);
		}

		public static bool IsWeaponUnlocked(WayStationMetaSave meta, StartingWeapon weapon)
		{
			return weapon switch
			{
				StartingWeapon.Sword => true,
				StartingWeapon.Dagger => (meta?.climbCompletions ?? 0) > 0,
				StartingWeapon.Hammer => HasCompleted(meta, StartingWeapon.Dagger, RunDifficulty.Easy),
				_ => false,
			};
		}

		public static bool IsDifficultyUnlocked(
			WayStationMetaSave meta,
			StartingWeapon weapon,
			RunDifficulty difficulty)
		{
			if (!IsWeaponUnlocked(meta, weapon)) return false;
			return difficulty switch
			{
				RunDifficulty.Easy => true,
				RunDifficulty.Normal => weapon switch
				{
					StartingWeapon.Sword => (meta?.climbCompletions ?? 0) > 0,
					StartingWeapon.Dagger => HasCompleted(meta, StartingWeapon.Dagger, RunDifficulty.Easy),
					StartingWeapon.Hammer => HasCompleted(meta, StartingWeapon.Hammer, RunDifficulty.Easy),
					_ => false,
				},
				RunDifficulty.Hard => weapon switch
				{
					StartingWeapon.Sword => HasCompleted(meta, StartingWeapon.Sword, RunDifficulty.Normal),
					StartingWeapon.Dagger => HasCompleted(meta, StartingWeapon.Dagger, RunDifficulty.Normal),
					StartingWeapon.Hammer => HasCompleted(meta, StartingWeapon.Hammer, RunDifficulty.Normal),
					_ => false,
				},
				_ => false,
			};
		}

		public static IReadOnlyList<StartingWeapon> GetUnlockedWeapons(WayStationMetaSave meta)
		{
			return Enum.GetValues<StartingWeapon>()
				.Where(weapon => IsWeaponUnlocked(meta, weapon))
				.ToArray();
		}

		public static IReadOnlyList<RunDifficulty> GetUnlockedDifficulties(
			WayStationMetaSave meta,
			StartingWeapon weapon)
		{
			return Enum.GetValues<RunDifficulty>()
				.Where(difficulty => IsDifficultyUnlocked(meta, weapon, difficulty))
				.ToArray();
		}

		private static bool HasCompleted(
			WayStationMetaSave meta,
			StartingWeapon weapon,
			RunDifficulty difficulty)
		{
			string weaponId = weapon.ToString().ToLowerInvariant();
			return meta?.completedClimbs?.Any(entry =>
				entry != null
				&& string.Equals(entry.startingWeaponId, weaponId, StringComparison.OrdinalIgnoreCase)
				&& entry.difficulty == difficulty) == true;
		}
	}
}
