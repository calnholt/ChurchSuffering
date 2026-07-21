namespace ChurchSuffering.ECS.Input
{
	public enum RumbleProfile
	{
		None,
		UiHover,
		HotKeyTick25,
		HotKeyTick50,
		HotKeyTick75,
		HotKeyComplete,
		AchievementUnlock,
		EnemyIntentImpact,
		Soft,
		Guard,
		LightImpact,
		MediumImpact,
		HeavyImpact,
		EpicImpact,
	}

	public static class RumblePatternCatalog
	{
		private static readonly RumbleMotorState Zero = RumbleMotorState.Zero;

		public static RumblePattern Resolve(RumbleProfile profile) => profile switch
		{
			RumbleProfile.UiHover => Decay(new(0.30f, 0.20f), 0.04f),
			RumbleProfile.HotKeyTick25 => Decay(new(0.00f, 0.10f, 0.06f, 0.06f), 0.025f),
			RumbleProfile.HotKeyTick50 => Decay(new(0.04f, 0.15f, 0.08f, 0.08f), 0.03f),
			RumbleProfile.HotKeyTick75 => Decay(new(0.08f, 0.22f, 0.12f, 0.12f), 0.035f),
			RumbleProfile.HotKeyComplete => Decay(new(0.18f, 0.32f, 0.18f, 0.18f), 0.06f),
			RumbleProfile.AchievementUnlock => new RumblePattern(
				new RumbleSegment(new(0.00f, 0.38f, 0.20f, 0.20f), Zero, 0.07f),
				new RumbleSegment(Zero, Zero, 0.05f),
				new RumbleSegment(new(0.38f, 0.24f, 0.12f, 0.12f), Zero, 0.14f)),
			RumbleProfile.EnemyIntentImpact => Decay(new(0.42f, 0.24f, 0.10f, 0.10f), 0.10f),
			RumbleProfile.Soft => Decay(new(0.08f, 0.18f, 0.12f, 0.12f), 0.08f),
			RumbleProfile.Guard => Decay(new(0.30f, 0.16f, 0.28f, 0.28f), 0.10f),
			RumbleProfile.LightImpact => Decay(new(0.12f, 0.30f, 0.18f, 0.18f), 0.06f),
			RumbleProfile.MediumImpact => Decay(new(0.30f, 0.34f, 0.20f, 0.20f), 0.08f),
			RumbleProfile.HeavyImpact => Decay(new(0.58f, 0.38f, 0.25f, 0.25f), 0.12f),
			RumbleProfile.EpicImpact => new RumblePattern(
				new RumbleSegment(new(0.68f, 0.44f, 0.30f, 0.30f), Zero, 0.10f),
				new RumbleSegment(Zero, Zero, 0.035f),
				new RumbleSegment(new(0.34f, 0.25f, 0.18f, 0.18f), Zero, 0.09f)),
			_ => new RumblePattern(),
		};

		private static RumblePattern Decay(RumbleMotorState start, float duration) =>
			new(new RumbleSegment(start, Zero, duration));
	}
}
