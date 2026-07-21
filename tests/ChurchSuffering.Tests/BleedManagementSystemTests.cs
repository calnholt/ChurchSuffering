using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Systems;
using Xunit;

namespace ChurchSuffering.Tests;

public class BleedManagementSystemTests
{
    [Fact]
    public void GetQualifyingSameColorCount_returns_zero_for_single_card_per_color()
    {
        var progress = new EnemyAttackProgress { PlayedRed = 1, PlayedWhite = 1, PlayedBlack = 1 };
        Assert.Equal(0, BleedManagementSystem.GetQualifyingSameColorCount(progress));
    }

    [Fact]
    public void GetQualifyingSameColorCount_returns_zero_for_no_cards()
    {
        var progress = new EnemyAttackProgress();
        Assert.Equal(0, BleedManagementSystem.GetQualifyingSameColorCount(progress));
        Assert.Equal(0, BleedManagementSystem.GetQualifyingSameColorCount(null));
    }

    [Fact]
    public void GetQualifyingSameColorCount_returns_one_for_two_reds()
    {
        var progress = new EnemyAttackProgress { PlayedRed = 2 };
        Assert.Equal(1, BleedManagementSystem.GetQualifyingSameColorCount(progress));
    }

    [Fact]
    public void GetQualifyingSameColorCount_returns_two_for_red_red_white_white()
    {
        var progress = new EnemyAttackProgress { PlayedRed = 2, PlayedWhite = 2 };
        Assert.Equal(2, BleedManagementSystem.GetQualifyingSameColorCount(progress));
    }

    [Fact]
    public void GetQualifyingSameColorCount_counts_three_colors_when_each_has_two_or_more()
    {
        var progress = new EnemyAttackProgress { PlayedRed = 2, PlayedWhite = 3, PlayedBlack = 2 };
        Assert.Equal(3, BleedManagementSystem.GetQualifyingSameColorCount(progress));
    }
}
