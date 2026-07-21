using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;
using Xunit;

namespace ChurchSuffering.Tests;

public sealed class ClimbResourceAcquisitionDisplaySystemTests
{
	[Fact]
	public void EncounterRewardPublishesFullPayloadOnlyWhenReturningToClimb()
	{
		EventManager.Clear();
		try
		{
			ClimbResourceAcquisitionAnimationRequested published = null;
			EventManager.Subscribe<ClimbResourceAcquisitionAnimationRequested>(evt => published = evt);
			var state = new QuestRewardOverlayState
			{
				IsEncounterReward = true,
				DismissScene = SceneId.Climb,
				ClimbResources = new ClimbResourceSave { red = 2, white = 1, black = 1 },
			};

			Assert.True(RewardModalDisplaySystem.PublishClimbResourceAcquisitionIfNeeded(state));
			Assert.NotNull(published);
				Assert.Equal(2, published.Resources.red);
				Assert.Equal(1, published.Resources.white);
				Assert.Equal(1, published.Resources.black);
				Assert.True(published.DelayClimbTurnoverUntilComplete);

			published = null;
			state.DismissScene = SceneId.Battle;
			Assert.False(RewardModalDisplaySystem.PublishClimbResourceAcquisitionIfNeeded(state));
			Assert.Null(published);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void BuildGemSequence_usesLiteralRoundRobinColorOrder()
	{
		var sequence = ClimbResourceAcquisitionDisplaySystem.BuildGemSequence(
			new ClimbResourceSave { red = 3, white = 2, black = 1 });

		Assert.Equal(
			new[]
			{
				ClimbResourceType.Red,
				ClimbResourceType.White,
				ClimbResourceType.Black,
				ClimbResourceType.Red,
				ClimbResourceType.White,
				ClimbResourceType.Red,
			},
			sequence);
	}

	[Fact]
	public void BuildGemSequence_ignoresNegativeAndZeroAmounts()
	{
		var sequence = ClimbResourceAcquisitionDisplaySystem.BuildGemSequence(
			new ClimbResourceSave { red = -3, white = 0, black = 2 });

		Assert.Equal(2, sequence.Count);
		Assert.All(sequence, type => Assert.Equal(ClimbResourceType.Black, type));
	}

	[Theory]
	[InlineData(1, 0f)]
	[InlineData(2, 0.11f)]
	[InlineData(3, 0.11f)]
	[InlineData(6, 0.07f)]
	public void CalculateStaggerSeconds_capsTotalCatchSpread(int count, float expected)
	{
		Assert.Equal(expected, ClimbResourceAcquisitionDisplaySystem.CalculateStaggerSeconds(count), 3);
	}
}
