using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Systems;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Utils;

namespace Crusaders30XX.ECS.Objects.Enemies;


public class EarthDemon : EnemyBase
{
    public EarthDemon()
    {
        Id = EnemyId.EarthDemon;
        Name = "Earth Demon";
        HP = 32;
    }

    public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
    {
        return ArrayUtils.TakeRandomWithoutReplacement(new List<EnemyAttackId> { EnemyAttackId.TremorStrike, EnemyAttackId.StoneBarrage, EnemyAttackId.EarthenWall }, 1);
    }
}


public class TremorStrike : EnemyAttackBase
{
    private int ShackledAmount = 2;

    public TremorStrike()
    {
        Id = EnemyAttackId.TremorStrike;
        Name = "Tremor Strike";
        Damage = 9;
        ConditionType = ConditionType.OnBlockedByAtLeast2Cards;
        Text = $"If not blocked by 2+ cards - Gain {ShackledAmount} shackled.";

        OnAttackHit = (entityManager) =>
        {
            EventManager.Publish(new ApplyPassiveEvent
            {
                Target = entityManager.GetEntity("Player"),
                Type = AppliedPassiveType.Shackled,
                Delta = ShackledAmount
            });
        };
    }
}

public class StoneBarrage : EnemyAttackBase
{
    private int BleedPerCard = 2;
    private CardData.CardColor? Color;

    public StoneBarrage()
    {
        Id = EnemyAttackId.StoneBarrage;
        Name = "Stone Barrage";
        Damage = 10;
        AttackEffectRecipe = EnemyRockBlastEffect();
        ConditionType = ConditionType.None;

        OnAttackReveal = (entityManager) =>
        {
            Color = PlayerHandColorService.GetRandomCardColorInPlayerHand(entityManager);
            Text = Color.HasValue
                ? $"Gain {BleedPerCard} bleed for each {Color.Value.ToString().ToLower()} card that blocks this."
                : $"Gain {BleedPerCard} bleed for each card of the selected color that blocks this. No color is selected.";
        };

        OnBlockProcessed = (entityManager, card) =>
        {
			if (Color.HasValue && CardColorQualificationService.QualifiesAs(card, Color.Value))
            {
                EventManager.Publish(new ApplyPassiveEvent
                {
                    Target = entityManager.GetEntity("Player"),
                    Type = AppliedPassiveType.Bleed,
                    Delta = BleedPerCard
                });
            }
        };
    }
}

public class EarthenWall : EnemyAttackBase
{
    private int GuardAmount = 4;

    public EarthenWall()
    {
        Id = EnemyAttackId.EarthenWall;
        Name = "Earthen Wall";
        Damage = 6;
        ConditionType = ConditionType.None;
        Text = $"On attack - Gain {GuardAmount} guard.";

        OnAttackReveal = (entityManager) =>
        {
            EventManager.Publish(new ApplyPassiveEvent
            {
                Target = entityManager.GetEntity("Enemy"),
                Type = AppliedPassiveType.Guard,
                Delta = GuardAmount
            });
        };
    }
}
