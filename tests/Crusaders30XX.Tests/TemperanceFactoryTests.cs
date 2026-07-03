using Crusaders30XX.ECS.Factories;
using Xunit;

namespace Crusaders30XX.Tests;

public class TemperanceFactoryTests
{
    [Theory]
    [InlineData("angelic_aura", "Angelic Aura", "Player", "Gain 3 aegis.", 2)]
    [InlineData("fling_fling", "Fling Fling", "Player", "Add 2 Kunai cards to your hand.", 3)]
    [InlineData("iron_resolve", "Iron Resolve", "Player", "Gain 1 vigor.", 3)]
    [InlineData("radiance", "Radiance", "Enemy", "Stun the enemy.", 4)]
    [InlineData("static_surge", "Static Surge", "Player", "Gain galvanize.", 3)]
    [InlineData("unsheath", "Unsheath", "Player", "Gain 5 sharpen.", 3)]
    public void Create_returns_temperance_metadata(string id, string name, string target, string text, int threshold)
    {
        var ability = TemperanceFactory.Create(id);

        Assert.NotNull(ability);
        Assert.Equal(id, ability.Id);
        Assert.Equal(name, ability.Name);
        Assert.Equal(target, ability.Target);
        Assert.Equal(text, ability.Text);
        Assert.Equal(threshold, ability.Threshold);
    }

    [Fact]
    public void Create_returns_null_for_unknown_id()
    {
        Assert.Null(TemperanceFactory.Create("missing_temperance"));
    }

    [Fact]
    public void GetAllTemperanceAbilities_returns_all_current_abilities()
    {
        var abilities = TemperanceFactory.GetAllTemperanceAbilities();

        Assert.Equal(7, abilities.Count);
        Assert.Contains("angelic_aura", abilities.Keys);
        Assert.Contains("fling_fling", abilities.Keys);
        Assert.Contains("iron_resolve", abilities.Keys);
        Assert.Contains("measured_breath", abilities.Keys);
        Assert.Contains("radiance", abilities.Keys);
        Assert.Contains("static_surge", abilities.Keys);
        Assert.Contains("unsheath", abilities.Keys);
    }
}
