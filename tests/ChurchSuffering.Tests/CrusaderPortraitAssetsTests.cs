using ChurchSuffering.ECS.Services;
using Xunit;

namespace ChurchSuffering.Tests;

public class CrusaderPortraitAssetsTests
{
    [Theory]
    [InlineData("sword", "crusader_sword")]
    [InlineData("hammer", "crusader_hammer")]
    [InlineData("dagger", "crusader_dagger")]
    [InlineData("", "crusader_sword")]
    [InlineData("unknown", "crusader_sword")]
    public void ResolveBattlePortraitAsset_MapsWeaponIds(string weaponId, string expected)
    {
        Assert.Equal(expected, CrusaderPortraitAssets.ResolveBattlePortraitAsset(weaponId));
    }

    [Fact]
    public void DialogPortraitAsset_IsSwordPortrait()
    {
        Assert.Equal("crusader_sword", CrusaderPortraitAssets.DialogPortraitAsset);
    }

    [Theory]
    [InlineData("sword", "CardArt/sword")]
    [InlineData("hammer", "CardArt/hammer")]
    [InlineData("dagger", "CardArt/dagger")]
    public void ResolveWeaponCardArtAsset_UsesCardArtPrefix(string weaponId, string expected)
    {
        Assert.Equal(expected, CrusaderPortraitAssets.ResolveWeaponCardArtAsset(weaponId));
    }
}
