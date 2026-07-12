using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Scenes.BattleScene;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class DialogPresentationMathTests
{
	[Theory]
	[InlineData(DialogPhase.Intro, 0.4f)]
	[InlineData(DialogPhase.Active, 1f)]
	[InlineData(DialogPhase.Outro, 0.2f)]
	public void Stage_translation_preserves_text_inset(
		DialogPhase phase,
		float elapsedSeconds)
	{
		var animation = DialogPresentationMath.CalculateAnimation(phase, elapsedSeconds, 0.52f);
		var stage = new Rectangle(760, 760, 960, 248);
		var bodyBase = new Vector2(760, 824);
		int drawX = stage.X + (int)animation.StageTranslateX;

		var translated = DialogPresentationMath.TranslateStagePosition(
			bodyBase,
			stage,
			drawX,
			stage.Y);

		Assert.Equal(bodyBase.X - stage.X, translated.X - drawX);
		Assert.Equal(bodyBase.Y - stage.Y, translated.Y - stage.Y);
	}

	[Fact]
	public void Active_animation_is_fully_settled()
	{
		var animation = DialogPresentationMath.CalculateAnimation(DialogPhase.Active, 0f, 0.52f);

		Assert.Equal(1f, animation.RailProgress);
		Assert.Equal(1f, animation.RailAccentProgress);
		Assert.Equal(1f, animation.PortraitOpacity);
		Assert.Equal(1f, animation.StageOpacity);
		Assert.Equal(0f, animation.StageTranslateX);
		Assert.Equal(1f, animation.BottomBarProgress);
		Assert.Equal(1f, animation.SpeakerDashProgress);
		Assert.Equal(1f, animation.SkipButtonOpacity);
		Assert.Equal(0f, animation.SkipButtonSlideY);
	}

	[Fact]
	public void Body_layout_key_changes_for_every_layout_input()
	{
		var baseline = new DialogBodyLayoutKey("Line", 0.2f, 900, 760, 824);

		Assert.NotEqual(baseline, baseline with { Text = "Other" });
		Assert.NotEqual(baseline, baseline with { Scale = 0.25f });
		Assert.NotEqual(baseline, baseline with { MaxWidth = 800 });
		Assert.NotEqual(baseline, baseline with { StartX = 780 });
		Assert.NotEqual(baseline, baseline with { StartY = 840 });
	}
}
