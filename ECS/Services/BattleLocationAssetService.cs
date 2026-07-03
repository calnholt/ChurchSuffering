using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Services
{
	public static class BattleLocationAssetService
	{
		public static readonly BattleLocation[] ClimbEncounterLocations =
		{
			BattleLocation.Desert,
			BattleLocation.Tundra,
			BattleLocation.Jungle,
			BattleLocation.Volcano,
			BattleLocation.Gothic,
		};

		public const BattleLocation FinalEncounterLocation = BattleLocation.TheGate;

		public static BattleLocation RollClimbEncounterLocation(Random rng)
		{
			return RollClimbEncounterLocation(rng, excludedLocations: null);
		}

		public static BattleLocation RollClimbEncounterLocation(Random rng, IEnumerable<BattleLocation> excludedLocations)
		{
			rng ??= Random.Shared;
			var pool = ClimbEncounterLocations.ToList();
			if (pool.Count == 0) return BattleLocation.Desert;

			var excluded = excludedLocations?.ToHashSet() ?? new HashSet<BattleLocation>();
			if (excluded.Count > 0 && pool.Count > excluded.Count)
			{
				pool = pool.Where(location => !excluded.Contains(location)).ToList();
			}

			return pool[rng.Next(pool.Count)];
		}

		public static string GetBackgroundAsset(BattleLocation location)
		{
			return location switch
			{
				BattleLocation.Desert => "Battle_Backgrounds/desert-battle-background",
				BattleLocation.Tundra => "Battle_Backgrounds/tundra-battle-background",
				BattleLocation.Jungle => "Battle_Backgrounds/jungle-battle-background",
				BattleLocation.Volcano => "Battle_Backgrounds/volcano-battle-background",
				BattleLocation.TheGate => "Battle_Backgrounds/the-gate-battle-background",
				BattleLocation.Gothic => "Battle_Backgrounds/gothic-battle-background",
				BattleLocation.Forest => "forest-background",
				BattleLocation.Cathedral => "cathedral-background",
				_ => string.Empty,
			};
		}

		public static string GetClimbBackgroundAsset(BattleLocation location)
		{
			return location switch
			{
				BattleLocation.Desert => "desert_background_location",
				BattleLocation.Tundra => "tundra_background_location",
				BattleLocation.Jungle => "jungle_background_location",
				BattleLocation.Volcano => "volcano_background_location",
				BattleLocation.Gothic => "gothic_background_location",
				_ => "desert_background_location",
			};
		}

		public static MusicTrack GetMusicTrack(BattleLocation location)
		{
			return location switch
			{
				BattleLocation.Desert => MusicTrack.DesertBattle,
				BattleLocation.Tundra => MusicTrack.TundraBattle,
				BattleLocation.Jungle => MusicTrack.JungleBattle,
				BattleLocation.Volcano => MusicTrack.VolcanoBattle,
				BattleLocation.TheGate => MusicTrack.TheGateBattle,
				BattleLocation.Gothic => MusicTrack.GothicBattle,
				_ => MusicTrack.DesertBattle,
			};
		}
	}
}
