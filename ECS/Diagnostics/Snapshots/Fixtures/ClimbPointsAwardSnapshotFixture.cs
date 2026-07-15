using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures;

public sealed class ClimbPointsAwardSnapshotFixture : IDisplaySnapshotFixture
{
	public string Id => "climb-points-award";
	public int WarmupFrames => 2;
	public string OutputFileName => $"{_variant}.png";

	private string _variant = "victory-ready";
	private ClimbPointsAwardDisplaySystem _display;

	public void Setup(DisplaySnapshotContext ctx, string[] args)
	{
		_variant = ParseVariant(args);
		ctx.SceneEntity.GetComponent<SceneState>().Current = SceneId.Snapshot;
		_display = new ClimbPointsAwardDisplaySystem(
			ctx.World.EntityManager,
			ctx.GraphicsDevice,
			ctx.SpriteBatch,
			ctx.ImageAssets);

		var sample = GetSample(_variant);
		if (!_display.OpenForSnapshot(
			sample.TimeReached,
			sample.CompletedFinalBoss,
			sample.Abandoned,
			sample.ElapsedSeconds))
		{
			throw new DisplaySnapshotSetupException("Climb points award overlay did not open.");
		}
	}

	public void Draw(DisplaySnapshotContext ctx) => _display.Draw();

	private static string ParseVariant(string[] args)
	{
		string variant = args?.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
			? args[0].Trim().ToLowerInvariant()
			: "victory-ready";
		_ = GetSample(variant);
		return variant;
	}

	private static Sample GetSample(string variant)
	{
		return variant switch
		{
			"intro" => new Sample(12, false, false, 0.35f),
			"partial-route" => new Sample(12, false, false, 0.85f),
			"victory-route" => new Sample(32, true, false, 2.05f),
			"victory-impact" => new Sample(32, true, false, 2.67f),
			"time0-ready" => Ready(0, false, false),
			"partial-ready" => Ready(12, false, false),
			"mid-ready" => Ready(20, false, false),
			"deep-ready" => Ready(26, false, false),
			"victory-ready" => Ready(32, true, false),
			"abandoned-ready" => Ready(18, false, true),
			_ => throw new DisplaySnapshotSetupException(
				$"Unknown climb-points-award variant '{variant}'. Expected intro, partial-route, victory-route, victory-impact, time0-ready, partial-ready, mid-ready, deep-ready, victory-ready, or abandoned-ready."),
		};
	}

	private static Sample Ready(int timeReached, bool completedFinalBoss, bool abandoned)
	{
		var scenario = ClimbPointsAwardAnimationService.CreateScenario(timeReached, completedFinalBoss, abandoned);
		int earned = ClimbPointsAwardAnimationService.GetEarnedTierCount(scenario);
		return new Sample(
			timeReached,
			completedFinalBoss,
			abandoned,
			ClimbPointsAwardAnimationService.GetReadySeconds(earned) + 0.40f);
	}

	private readonly record struct Sample(
		int TimeReached,
		bool CompletedFinalBoss,
		bool Abandoned,
		float ElapsedSeconds);
}
