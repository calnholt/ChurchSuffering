using System;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Enemies;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Xunit;

namespace Crusaders30XX.Tests;

public class EntombTests : IDisposable
{
    public EntombTests()
    {
        EventManager.Clear();
    }

    public void Dispose()
    {
        EventManager.Clear();
    }

    [Fact]
    public void Uses_damage_threshold_to_apply_brittle_to_top_draw_pile_card()
    {
        var attack = new Entomb();
        ApplyCardApplicationEvent publishedEvent = null;
        EventManager.Subscribe<ApplyCardApplicationEvent>(evt => publishedEvent = evt);

        attack.OnAttackReveal(new EntityManager());
        string expectedText = EnemyAttackTextHelper.GetBlockThresholdText(
            attack.Damage - attack.BlockRequiredToPreventEffect!.Value,
            "Apply brittle to the top card of your draw pile.");
        attack.OnDamageThresholdMet(new EntityManager());

        Assert.Equal(10, attack.Damage);
        Assert.True(attack.BlockRequiredToPreventEffect is 6 or 7);
        Assert.Equal(ConditionType.None, attack.ConditionType);
        Assert.Null(attack.OnAttackHit);
        Assert.Equal(expectedText, attack.Text);
        Assert.NotNull(publishedEvent);
        Assert.Equal(1, publishedEvent.Amount);
        Assert.Equal(CardApplicationType.Brittle, publishedEvent.Type);
        Assert.Equal(CardApplicationTarget.TopXCards, publishedEvent.Target);
    }
}
