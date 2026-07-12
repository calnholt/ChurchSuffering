using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class EnemyAttackAnimationServiceTests
{
	[Fact]
	public void ImpactIntensity_is_clamped_and_monotonic()
	{
		float negative = EnemyAttackAnimationService.ComputeImpactIntensity(-5);
		float zero = EnemyAttackAnimationService.ComputeImpactIntensity(0);
		float medium = EnemyAttackAnimationService.ComputeImpactIntensity(10);
		float heavy = EnemyAttackAnimationService.ComputeImpactIntensity(20);
		float extreme = EnemyAttackAnimationService.ComputeImpactIntensity(100);

		Assert.Equal(0.25f, negative, 3);
		Assert.Equal(negative, zero);
		Assert.True(medium > zero);
		Assert.True(heavy > medium);
		Assert.Equal(1f, extreme, 3);
	}

	[Fact]
	public void Entrance_reaches_stable_final_values()
	{
		var initial = EnemyAttackAnimationService.ComputeEntrance(0f, 0.75f);
		var impact = EnemyAttackAnimationService.ComputeEntrance(EnemyAttackAnimationService.ImpactMomentSeconds, 0.75f);
		var settled = EnemyAttackAnimationService.ComputeEntrance(EnemyAttackAnimationService.PresentationCompleteSeconds, 0.75f);

		Assert.True(initial.PanelScaleX < 0.25f);
		Assert.Equal(0f, initial.TextAlpha, 3);
		Assert.True(impact.FlashAlpha > 0.9f);
		Assert.Equal(1f, settled.PanelScaleX, 3);
		Assert.Equal(1f, settled.PanelScaleY, 3);
		Assert.Equal(1f, settled.TextAlpha, 3);
		Assert.Equal(0f, settled.TextOffsetY, 3);
		Assert.Equal(0f, settled.FlashAlpha, 3);
		Assert.Equal(1f, settled.RingOneProgress, 3);
		Assert.Equal(1f, settled.RingTwoProgress, 3);
	}

	[Fact]
	public void DeterministicRecoil_is_repeatable_and_returns_to_zero()
	{
		float elapsed = EnemyAttackAnimationService.ImpactMomentSeconds + 0.06f;
		Vector2 first = EnemyAttackAnimationService.ComputeDeterministicRecoil(elapsed, 0.25f, 9f);
		Vector2 second = EnemyAttackAnimationService.ComputeDeterministicRecoil(elapsed, 0.25f, 9f);

		Assert.Equal(first, second);
		Assert.NotEqual(Vector2.Zero, first);
		Assert.Equal(Vector2.Zero, EnemyAttackAnimationService.ComputeDeterministicRecoil(2f, 0.25f, 9f));
		Assert.Equal(Vector2.Zero, EnemyAttackAnimationService.ComputeDeterministicRecoil(elapsed, 0f, 9f));
	}

	[Fact]
	public void AbsorbTween_handles_zero_duration_and_reaches_target()
	{
		var start = new Vector2(10f, 20f);
		var enemy = new Vector2(100f, 80f);
		var (earlyScale, earlyPosition) = EnemyAttackAnimationService.ComputeAbsorbTween(start, enemy, -10, 0f, 0f);
		var (finalScale, finalPosition) = EnemyAttackAnimationService.ComputeAbsorbTween(start, enemy, -10, 5f, 0f);

		Assert.Equal(1f, earlyScale, 3);
		Assert.Equal(start, earlyPosition);
		Assert.Equal(0f, finalScale, 3);
		Assert.Equal(new Vector2(100f, 70f), finalPosition);
	}
}
