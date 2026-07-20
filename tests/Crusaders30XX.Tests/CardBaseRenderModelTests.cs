using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Objects.Cards;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class CardBaseRenderModelTests
{
    [Fact]
    public void Printed_and_effective_visual_state_changes_cache_key()
    {
        CardBaseRenderModel baseline = Model();

        Assert.NotEqual(baseline, baseline with { DisplayText = "Changed" });
        Assert.NotEqual(baseline, baseline with { Costs = "Red|White" });
        Assert.NotEqual(baseline, baseline with { Color = CardData.CardColor.Red });
        Assert.NotEqual(baseline, baseline with { SecondaryColor = CardData.CardColor.Black });
        Assert.NotEqual(baseline, baseline with { IsUpgraded = true });
        Assert.NotEqual(baseline, baseline with { EffectiveDamage = 9 });
        Assert.NotEqual(baseline, baseline with { EffectiveBlock = 8 });
        Assert.NotEqual(baseline, baseline with { AlternateTreatsAsAttack = true });
        Assert.NotEqual(baseline, baseline with { AlternateAttackDamage = 7 });
        Assert.NotEqual(baseline, baseline with { AlternateIsFreeAction = true });
        Assert.NotEqual(baseline, baseline with { Phase = SubPhase.Block });
        Assert.NotEqual(baseline, baseline with { StyleFingerprint = 2 });
        Assert.NotEqual(baseline, baseline with { Scale = 1f });
        Assert.NotEqual(baseline, baseline with { Rotation = 0.1f });
        Assert.NotEqual(baseline, baseline with { PhysicalWidth = 300 });
    }

    private static CardBaseRenderModel Model() => new(
        CardId: "strike",
        DisplayName: "Strike",
        DisplayText: "Deal damage.",
        Costs: "Red",
        Color: CardData.CardColor.White,
        SecondaryColor: null,
        Type: CardType.Attack,
        PrintedDamage: 5,
        PrintedBlock: 0,
        EffectiveDamage: 5,
        EffectiveBlock: 0,
        IsFreeAction: false,
        IsWeapon: false,
        IsToken: false,
        IsUpgraded: false,
        IsColorless: false,
        SuppressStatDelta: false,
        AlternateTreatsAsAttack: false,
        AlternateAttackDamage: 0,
        AlternateIsFreeAction: false,
        Phase: SubPhase.Action,
        StyleFingerprint: 1,
        Scale: 0.85f,
        Rotation: 0f,
        PhysicalWidth: 228,
        PhysicalHeight: 321);
}
