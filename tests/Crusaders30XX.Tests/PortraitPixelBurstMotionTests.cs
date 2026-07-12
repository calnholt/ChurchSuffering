using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public class PortraitPixelBurstMotionTests
{
	[Fact]
	public void Jitter_starts_at_the_sampled_portrait_position()
	{
		var offset = PortraitPixelBurstMotion.ComputeJitterOffset(
			0f, 0.2f, 6f, 2f, 0.4f, 1.2f, 14f, 24f);

		Assert.Equal(Vector2.Zero, offset);
	}

	[Fact]
	public void Jitter_envelope_increases_toward_release()
	{
		var early = PortraitPixelBurstMotion.ComputeJitterOffset(
			0.05f, 0.2f, 6f, 2f, MathHelper.PiOver2, MathHelper.PiOver2, 0f, 0f);
		var late = PortraitPixelBurstMotion.ComputeJitterOffset(
			0.15f, 0.2f, 6f, 2f, MathHelper.PiOver2, MathHelper.PiOver2, 0f, 0f);

		Assert.True(late.Length() > early.Length());
		Assert.InRange(late.Length(), 0f, 6f);
	}

	[Fact]
	public void Jitter_is_deterministic_for_stable_particle_parameters()
	{
		var first = PortraitPixelBurstMotion.ComputeJitterOffset(
			0.18f, 0.2f, 6f, 2f, 0.4f, 1.2f, 14f, 24f);
		var second = PortraitPixelBurstMotion.ComputeJitterOffset(
			0.18f, 0.2f, 6f, 2f, 0.4f, 1.2f, 14f, 24f);

		Assert.Equal(first, second);
	}

	[Fact]
	public void Flight_delta_excludes_buildup_and_uses_only_release_frame_remainder()
	{
		Assert.Equal(0f, PortraitPixelBurstMotion.ResolveFlightDelta(0.10f, 0.15f, 0.20f), 4);
		Assert.Equal(0.03f, PortraitPixelBurstMotion.ResolveFlightDelta(0.18f, 0.23f, 0.20f), 4);
		Assert.Equal(0.05f, PortraitPixelBurstMotion.ResolveFlightDelta(0.23f, 0.28f, 0.20f), 4);
	}

	[Fact]
	public void Flight_age_begins_at_zero_when_buildup_releases()
	{
		Assert.Equal(0f, PortraitPixelBurstMotion.ResolveFlightAge(0.20f, 0.20f), 4);
		Assert.Equal(0.12f, PortraitPixelBurstMotion.ResolveFlightAge(0.32f, 0.20f), 4);
	}
}
