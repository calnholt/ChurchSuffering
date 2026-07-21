using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Xunit;

namespace ChurchSuffering.Tests;

public sealed class BoosterPackOpeningAnimationServiceTests
{
	private static readonly BoosterPackOpeningTiming Timing = BoosterPackOpeningTiming.Default;

	[Theory]
	[InlineData(0.00f, BoosterPackOpeningPhase.Summon)]
	[InlineData(0.76f, BoosterPackOpeningPhase.Idle)]
	[InlineData(1.28f, BoosterPackOpeningPhase.Charge)]
	[InlineData(2.13f, BoosterPackOpeningPhase.Crack)]
	[InlineData(2.78f, BoosterPackOpeningPhase.Rupture)]
	[InlineData(3.54f, BoosterPackOpeningPhase.Showcase)]
	[InlineData(5.14f, BoosterPackOpeningPhase.Ready)]
	[InlineData(20f, BoosterPackOpeningPhase.Ready)]
	public void GetPhase_uses_exact_derived_boundaries(
		float elapsedSeconds,
		BoosterPackOpeningPhase expected)
	{
		Assert.Equal(expected, BoosterPackOpeningAnimationService.GetPhase(elapsedSeconds, Timing));
	}

	[Fact]
	public void Negative_time_clamps_to_summon_start()
	{
		Assert.Equal(
			BoosterPackOpeningPhase.Summon,
			BoosterPackOpeningAnimationService.GetPhase(-12f, Timing));
		Assert.Equal(
			0f,
			BoosterPackOpeningAnimationService.GetPhaseProgress(
				-12f,
				BoosterPackOpeningPhase.Summon,
				Timing));
	}

	[Fact]
	public void Changing_a_phase_duration_shifts_every_later_boundary()
	{
		var shifted = Timing with { IdleDuration = Timing.IdleDuration + 0.40f };

		Assert.Equal(Timing.IdleStart, shifted.IdleStart, 5);
		Assert.Equal(Timing.ChargeStart + 0.40f, shifted.ChargeStart, 5);
		Assert.Equal(Timing.CrackStart + 0.40f, shifted.CrackStart, 5);
		Assert.Equal(Timing.RuptureStart + 0.40f, shifted.RuptureStart, 5);
		Assert.Equal(Timing.ShowcaseStart + 0.40f, shifted.ShowcaseStart, 5);
		Assert.Equal(Timing.ReadyStart + 0.40f, shifted.ReadyStart, 5);
	}

	[Fact]
	public void Large_update_returns_all_crossed_milestones_once_in_time_order()
	{
		var milestones = BoosterPackOpeningAnimationService
			.GetCrossedMilestones(1.20f, 3.70f, Timing)
			.ToList();

		Assert.Equal(
			new[]
			{
				BoosterPackOpeningMilestoneKind.ChargeStarted,
				BoosterPackOpeningMilestoneKind.ChargePulse,
				BoosterPackOpeningMilestoneKind.ChargePulse,
				BoosterPackOpeningMilestoneKind.ChargePulse,
				BoosterPackOpeningMilestoneKind.CrackStarted,
				BoosterPackOpeningMilestoneKind.RuptureStarted,
				BoosterPackOpeningMilestoneKind.ShowcaseStarted,
			},
			milestones.Select(milestone => milestone.Kind));
		Assert.True(milestones.Zip(milestones.Skip(1), (left, right) => left.Seconds < right.Seconds).All(value => value));
		Assert.Empty(BoosterPackOpeningAnimationService.GetCrossedMilestones(3.70f, 3.70f, Timing));
	}

	[Fact]
	public void Charge_pulses_stop_strictly_before_crack()
	{
		var pulses = BoosterPackOpeningAnimationService
			.GetCrossedMilestones(0f, Timing.CrackStart, Timing)
			.Where(milestone => milestone.Kind == BoosterPackOpeningMilestoneKind.ChargePulse)
			.ToList();

		Assert.NotEmpty(pulses);
		Assert.All(pulses, pulse => Assert.True(pulse.Seconds < Timing.CrackStart));
	}

	[Fact]
	public void Loot_reveal_is_indexed_and_finishes_at_authored_transform()
	{
		var start = new Vector2(960f, 500f);
		var end = new Vector2(530f, 540f);
		var beforeSecondSlot = BoosterPackOpeningAnimationService.SampleLoot(
			Timing.ShowcaseStart + Timing.RevealStagger - 0.001f,
			1,
			start,
			end,
			Timing,
			120f);
		Assert.Equal(0f, beforeSecondSlot.Progress);

		var finished = BoosterPackOpeningAnimationService.SampleLoot(
			Timing.ShowcaseStart + Timing.RevealStagger + Timing.RevealTravelDuration,
			1,
			start,
			end,
			Timing,
			120f);
		Assert.Equal(end, finished.Position);
		Assert.Equal(1f, finished.Scale, 5);
		Assert.Equal(0f, finished.Rotation, 5);
		Assert.Equal(1f, finished.Alpha, 5);
		Assert.True(finished.IsSettled);
	}

	[Fact]
	public void Loot_start_rotation_follows_horizontal_slot()
	{
		var start = new Vector2(960f, 500f);
		var end = new Vector2(960f, 540f);
		float left = BoosterPackOpeningAnimationService.SampleLoot(Timing.ShowcaseStart, 0, start, end, Timing, 120f).Rotation;
		float center = BoosterPackOpeningAnimationService.SampleLoot(Timing.ShowcaseStart + Timing.RevealStagger, 1, start, end, Timing, 120f).Rotation;
		float right = BoosterPackOpeningAnimationService.SampleLoot(Timing.ShowcaseStart + Timing.RevealStagger * 2f, 2, start, end, Timing, 120f).Rotation;

		Assert.True(left < 0f);
		Assert.True(center > 0f);
		Assert.True(right > center);
	}

	[Fact]
	public void Latest_sheen_starts_before_ready_and_remains_active_after_ready()
	{
		float latestSheenStart = Timing.ShowcaseStart
			+ Timing.RevealStagger * 2f
			+ Timing.SheenDelayFromReveal;
		Assert.False(BoosterPackOpeningAnimationService.HasSheenStarted(latestSheenStart - 0.001f, 2, Timing));
		Assert.True(BoosterPackOpeningAnimationService.HasSheenStarted(latestSheenStart, 2, Timing));
		Assert.True(BoosterPackOpeningAnimationService.HasSheenStarted(Timing.ReadyStart + 10f, 2, Timing));
		Assert.False(BoosterPackOpeningAnimationService.CanDismiss(5.139f, Timing));
		Assert.True(BoosterPackOpeningAnimationService.CanDismiss(5.14f, Timing));
	}

	[Fact]
	public void Rupture_shake_is_deterministic_and_stops_after_its_duration()
	{
		float sampleTime = Timing.RuptureStart + 0.21f;
		Vector2 first = BoosterPackOpeningAnimationService.SampleRuptureShake(sampleTime, Timing, 13f);
		Vector2 second = BoosterPackOpeningAnimationService.SampleRuptureShake(sampleTime, Timing, 13f);

		Assert.Equal(first, second);
		Assert.NotEqual(Vector2.Zero, first);
		Assert.Equal(
			Vector2.Zero,
			BoosterPackOpeningAnimationService.SampleRuptureShake(
				Timing.RuptureStart + Timing.RuptureShakeDuration,
				Timing,
				13f));
	}

	[Fact]
	public void Buildup_rumble_is_zero_before_charge_and_at_rupture()
	{
		var settings = BoosterPackRumbleSettings.Default;

		Assert.Equal(
			BoosterPackRumbleSample.Zero,
			BoosterPackOpeningAnimationService.SampleBuildupRumble(Timing.ChargeStart - 0.01f, Timing, settings));
		Assert.Equal(
			BoosterPackRumbleSample.Zero,
			BoosterPackOpeningAnimationService.SampleBuildupRumble(Timing.RuptureStart, Timing, settings));
	}

	[Fact]
	public void Buildup_rumble_increases_monotonically_during_charge_and_crack()
	{
		var settings = BoosterPackRumbleSettings.Default;
		float early = BoosterPackOpeningAnimationService.SampleBuildupRumble(
			Timing.ChargeStart + 0.20f,
			Timing,
			settings).LowFrequency;
		float late = BoosterPackOpeningAnimationService.SampleBuildupRumble(
			Timing.RuptureStart - 0.01f,
			Timing,
			settings).LowFrequency;

		Assert.True(early > 0f);
		Assert.True(late > early);
	}

	[Fact]
	public void Loot_reveal_rumble_pulses_at_each_slot_and_fades_out()
	{
		var settings = BoosterPackRumbleSettings.Default;
		float slot0Start = BoosterPackOpeningAnimationService.GetLootRevealStartSeconds(0, Timing);
		float slot1Start = BoosterPackOpeningAnimationService.GetLootRevealStartSeconds(1, Timing);

		Assert.Equal(
			BoosterPackRumbleSample.Zero,
			BoosterPackOpeningAnimationService.SampleLootRevealRumble(slot0Start - 0.01f, 3, Timing, settings));

		float slot0Peak = BoosterPackOpeningAnimationService.SampleLootRevealRumble(
			slot0Start,
			3,
			Timing,
			settings).LowFrequency;
		Assert.Equal(settings.LootPulseLow, slot0Peak, 5);

		Assert.Equal(
			BoosterPackRumbleSample.Zero,
			BoosterPackOpeningAnimationService.SampleLootRevealRumble(
				slot0Start + settings.LootPulseDurationSeconds,
				3,
				Timing,
				settings));

		float slot1Peak = BoosterPackOpeningAnimationService.SampleLootRevealRumble(
			slot1Start,
			3,
			Timing,
			settings).LowFrequency;
		Assert.Equal(settings.LootPulseLow, slot1Peak, 5);
	}

	[Fact]
	public void SampleRumble_uses_buildup_before_rupture_and_loot_pulses_after()
	{
		var settings = BoosterPackRumbleSettings.Default;
		float buildup = BoosterPackOpeningAnimationService.SampleRumble(
			Timing.CrackStart + 0.10f,
			3,
			Timing,
			settings).LowFrequency;
		float lootPulse = BoosterPackOpeningAnimationService.SampleRumble(
			Timing.ShowcaseStart,
			3,
			Timing,
			settings).LowFrequency;
		float ruptureSilence = BoosterPackOpeningAnimationService.SampleRumble(
			Timing.RuptureStart + 0.10f,
			3,
			Timing,
			settings).LowFrequency;

		Assert.True(buildup > 0f);
		Assert.Equal(settings.LootPulseLow, lootPulse, 5);
		Assert.Equal(0f, ruptureSilence);
	}
}
