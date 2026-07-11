using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Data.VisualEffects;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class VisualEffectDisplayMathTests
{
	[Fact]
	public void Contact_phases_use_hit_stop_contact_when_present()
	{
		var effect = CreateEffect(Guid.Empty, 0.20f);

		Assert.Equal(0.25f, VisualEffectDisplayMath.ContactProgress(effect), 5);
		Assert.Equal(1f, VisualEffectDisplayMath.ApproachProgress(effect), 5);
		Assert.Equal(0f, VisualEffectDisplayMath.RecoveryProgress(effect), 5);

		effect.ElapsedSeconds = 0.50f;
		Assert.True(VisualEffectDisplayMath.RecoveryProgress(effect) > 0f);
	}

	[Fact]
	public void Variation_is_repeatable_per_request_and_changes_between_requests()
	{
		var first = new VisualEffectVariation(Guid.Parse("11111111-2222-3333-4444-555555555555"));
		var same = new VisualEffectVariation(Guid.Parse("11111111-2222-3333-4444-555555555555"));
		var other = new VisualEffectVariation(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));

		Assert.Equal(first.Range(7, -20f, 20f), same.Range(7, -20f, 20f));
		Assert.NotEqual(first.Range(7, -20f, 20f), other.Range(7, -20f, 20f));
		Assert.InRange(first.Range(8, 3, 9), 3, 8);
	}

	private static ActiveVisualEffect CreateEffect(Guid requestId, float elapsed)
	{
		return new ActiveVisualEffect
		{
			RequestId = requestId,
			Recipe = new VisualEffectRecipe().WithModules(VisualEffectModule.HitStop),
			Timing = new VisualEffectTiming
			{
				DurationSeconds = 0.80f,
				ImpactTimeSeconds = 0.30f,
				HitStopStartSeconds = 0.20f,
				HitStopDurationSeconds = 0.10f
			},
			ElapsedSeconds = elapsed
		};
	}
}
