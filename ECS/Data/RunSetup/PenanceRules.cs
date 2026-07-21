using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Crusaders30XX.ECS.Data.RunSetup
{
	public enum StartingWeapon
	{
		Sword,
		Dagger,
		Hammer,
	}

	public enum PenanceType
	{
		Fasting,
		Reparation,
		Abstinence,
		Mortification,
		PenitentialPilgrimage,
	}

	public readonly record struct PenanceResourceCounts(int Red, int White, int Black);

	public readonly record struct PenanceCalculation(
		int Level,
		int FastingStacks,
		int ReparationStacks,
		int AbstinenceStacks,
		int MortificationStacks,
		int PenitentialPilgrimageStacks,
		int PlayerMaximumHp,
		float EnemyHealthModifier,
		PenanceResourceCounts InitialResources,
		int ShopRefreshInterval)
	{
		public int GetStackCount(PenanceType type) => type switch
		{
			PenanceType.Fasting => FastingStacks,
			PenanceType.Reparation => ReparationStacks,
			PenanceType.Abstinence => AbstinenceStacks,
			PenanceType.Mortification => MortificationStacks,
			PenanceType.PenitentialPilgrimage => PenitentialPilgrimageStacks,
			_ => 0,
		};
	}

	public static class PenanceRules
	{
		public const int MaxLevel = 24;
		public const int BasePlayerMaximumHp = 25;
		public const float BaseEnemyHealthModifier = 0.70f;
		public const int BaseShopRefreshInterval = 8;

		private static readonly ReadOnlyCollection<PenanceType> FixedOrder = Array.AsReadOnly(new[]
		{
			PenanceType.Fasting,
			PenanceType.Reparation,
			PenanceType.Abstinence,
			PenanceType.Mortification,
			PenanceType.Fasting,
			PenanceType.Reparation,
			PenanceType.Abstinence,
			PenanceType.Mortification,
			PenanceType.Reparation,
			PenanceType.PenitentialPilgrimage,
			PenanceType.Fasting,
			PenanceType.Mortification,
			PenanceType.Reparation,
			PenanceType.Fasting,
			PenanceType.Reparation,
			PenanceType.Mortification,
			PenanceType.Abstinence,
			PenanceType.Reparation,
			PenanceType.PenitentialPilgrimage,
			PenanceType.Mortification,
			PenanceType.Reparation,
			PenanceType.Fasting,
			PenanceType.Reparation,
			PenanceType.Mortification,
		});

		public static IReadOnlyList<PenanceType> Order => FixedOrder;

		public static int ClampLevel(int level) => Math.Clamp(level, 0, MaxLevel);

		public static PenanceCalculation Calculate(int level)
		{
			int clamped = ClampLevel(level);
			var active = FixedOrder.Take(clamped);
			int fasting = active.Count(type => type == PenanceType.Fasting);
			int reparation = active.Count(type => type == PenanceType.Reparation);
			int abstinence = active.Count(type => type == PenanceType.Abstinence);
			int mortification = active.Count(type => type == PenanceType.Mortification);
			int pilgrimage = active.Count(type => type == PenanceType.PenitentialPilgrimage);

			return new PenanceCalculation(
				clamped,
				fasting,
				reparation,
				abstinence,
				mortification,
				pilgrimage,
				BasePlayerMaximumHp - fasting,
				BaseEnemyHealthModifier + mortification * 0.05f,
				new PenanceResourceCounts(
					abstinence < 3 ? 1 : 0,
					abstinence < 2 ? 1 : 0,
					abstinence < 1 ? 1 : 0),
				BaseShopRefreshInterval + pilgrimage);
		}

		public static string GetWeaponId(StartingWeapon weapon) => weapon switch
		{
			StartingWeapon.Sword => "sword",
			StartingWeapon.Dagger => "dagger",
			StartingWeapon.Hammer => "hammer",
			_ => "sword",
		};

		public static StartingWeapon ParseWeapon(string weaponId) => weaponId?.Trim().ToLowerInvariant() switch
		{
			"dagger" => StartingWeapon.Dagger,
			"hammer" => StartingWeapon.Hammer,
			_ => StartingWeapon.Sword,
		};
	}
}
