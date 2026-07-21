using System;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Objects.Enemies;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using Xunit;

namespace ChurchSuffering.Tests;

public class FrozenClawTests : IDisposable
{
    public FrozenClawTests()
    {
        EventManager.Clear();
    }

    public void Dispose()
    {
        EventManager.Clear();
    }

    [Fact]
    public void Uses_damage_threshold_to_freeze_top_draw_pile_card()
    {
        var attack = new FrozenClaw();
        ApplyCardApplicationEvent publishedEvent = null;
        EventManager.Subscribe<ApplyCardApplicationEvent>(evt => publishedEvent = evt);

        attack.OnDamageThresholdMet(new EntityManager());

        Assert.Equal(10, attack.Damage);
        Assert.Equal(6, attack.BlockRequiredToPreventEffect);
        Assert.Equal(ConditionType.None, attack.ConditionType);
        Assert.Null(attack.OnAttackHit);
        Assert.Equal(
            $"On attack - Intimidate 1 card.\n\n{EnemyAttackTextHelper.GetBlockThresholdText(attack.Damage - attack.BlockRequiredToPreventEffect!.Value, "Freeze the top card of your draw pile.")}",
            attack.Text);
        Assert.NotNull(publishedEvent);
        Assert.Equal(1, publishedEvent.Amount);
        Assert.Equal(CardApplicationType.Frozen, publishedEvent.Type);
        Assert.Equal(CardApplicationTarget.TopXCards, publishedEvent.Target);
    }
}
