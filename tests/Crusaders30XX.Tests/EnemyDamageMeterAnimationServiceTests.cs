using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public class EnemyDamageMeterAnimationServiceTests
{
	[Theory]
	[InlineData(30)]
	[InlineData(60)]
	[InlineData(144)]
	public void CriticalSpringSettlesExactlyAtTargetAcrossFrameRates(int framesPerSecond)
	{
		float value = 0f;
		float velocity = 0f;
		float dt = 1f / framesPerSecond;

		for (int i = 0; i < framesPerSecond * 2; i++)
		{
			EnemyDamageMeterAnimationService.AdvanceCriticalSpring(
				ref value,
				ref velocity,
				12f,
				dt,
				15f);
		}

		Assert.Equal(12f, value);
		Assert.Equal(0f, velocity);
	}

	[Fact]
	public void InterruptedSpringRetargetsContinuouslyAndNeverBecomesNegative()
	{
		float value = 0f;
		float velocity = 0f;

		for (int i = 0; i < 8; i++)
		{
			EnemyDamageMeterAnimationService.AdvanceCriticalSpring(
				ref value,
				ref velocity,
				20f,
				1f / 60f,
				15f);
		}

		float valueAtRetarget = value;
		EnemyDamageMeterAnimationService.AdvanceCriticalSpring(
			ref value,
			ref velocity,
			0f,
			1f / 60f,
			15f);

		Assert.True(value > 0f);
		Assert.True(value < valueAtRetarget + 2f);

		for (int i = 0; i < 180; i++)
		{
			EnemyDamageMeterAnimationService.AdvanceCriticalSpring(
				ref value,
				ref velocity,
				0f,
				1f / 60f,
				15f);
			Assert.True(value >= 0f);
		}

		Assert.Equal(0f, value);
	}

	[Fact]
	public void SegmentPresenceReversesWithoutPopping()
	{
		var animation = new EnemyDamageMeterSegmentAnimation();
		animation.Retarget(8, true, 0.2f, false);
		animation.Advance(0.09f, 15f, 0.18f);

		Assert.InRange(animation.Presence, 0.49f, 0.51f);
		float presenceAtRetarget = animation.Presence;

		animation.Retarget(0, false, 0.2f, true);
		animation.Advance(0.045f, 15f, 0.18f);

		Assert.True(animation.Presence > 0f);
		Assert.True(animation.Presence < presenceAtRetarget);
		Assert.True(EnemyDamageMeterAnimationService.GetEmphasisAmount(animation) > 0f);
	}

	[Fact]
	public void DisplayedValueRollsAndThenUsesExactTarget()
	{
		Assert.Equal(4, EnemyDamageMeterAnimationService.GetDisplayedValue(3.6f, 10));
		Assert.Equal(10, EnemyDamageMeterAnimationService.GetDisplayedValue(9.995f, 10));
		Assert.Equal(0, EnemyDamageMeterAnimationService.GetDisplayedValue(-0.4f, 0));
	}

	[Fact]
	public void ExplicitEmphasisSupportsOverflowOnlyChanges()
	{
		var animation = new EnemyDamageMeterSegmentAnimation();
		animation.Emphasize(0.2f);
		animation.Advance(0.1f, 15f, 0.18f);

		Assert.InRange(EnemyDamageMeterAnimationService.GetEmphasisAmount(animation), 0.99f, 1f);
	}
}
