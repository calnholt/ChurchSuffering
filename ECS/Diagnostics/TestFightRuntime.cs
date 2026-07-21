using System;

namespace ChurchSuffering.Diagnostics
{
	public static class TestFightRuntime
	{
		private static Func<int> _deckSeedProvider = () => Random.Shared.Next();

		public static TestFightLaunchOptions Options { get; private set; }
		public static bool IsActive => Options != null;
		public static int HpDelta { get; private set; }
		public static int BaselineMaxHp { get; private set; }
		public static int CurrentMaxHp { get; private set; }
		public static int BattleNumber { get; private set; }
		public static int LastDeckSeed { get; private set; }

		public static void Configure(TestFightLaunchOptions options)
		{
			Options = options;
			HpDelta = 0;
			BaselineMaxHp = 0;
			CurrentMaxHp = 0;
			BattleNumber = 0;
			LastDeckSeed = 0;
		}

		public static int BeginBattle()
		{
			if (!IsActive) return 0;
			BattleNumber++;
			LastDeckSeed = _deckSeedProvider();
			return LastDeckSeed;
		}

		public static int ApplyHpDelta(int baselineMaxHp)
		{
			if (!IsActive) return Math.Max(1, baselineMaxHp);
			BaselineMaxHp = Math.Max(1, baselineMaxHp);
			CurrentMaxHp = Math.Max(1, BaselineMaxHp + HpDelta);
			return CurrentMaxHp;
		}

		public static void RecordVictory()
		{
			if (!IsActive) return;
			HpDelta++;
			CurrentMaxHp = Math.Max(1, BaselineMaxHp + HpDelta);
		}

		public static void RecordDefeat()
		{
			if (!IsActive) return;
			if (BaselineMaxHp + HpDelta > 1)
			{
				HpDelta--;
			}
			CurrentMaxHp = Math.Max(1, BaselineMaxHp + HpDelta);
		}

		public static void Reset()
		{
			Options = null;
			HpDelta = 0;
			BaselineMaxHp = 0;
			CurrentMaxHp = 0;
			BattleNumber = 0;
			LastDeckSeed = 0;
			_deckSeedProvider = () => Random.Shared.Next();
		}

		internal static void SetDeckSeedProviderForTests(Func<int> provider)
		{
			_deckSeedProvider = provider ?? (() => Random.Shared.Next());
		}
	}
}
