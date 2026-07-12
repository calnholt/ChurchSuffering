using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Crusaders30XX.ECS.Utils;

namespace Crusaders30XX.ECS.Objects.Enemies
{
  public class IceDemon : EnemyBase
  {
    public IceDemon()
    {
      Id = EnemyId.IceDemon;
      Name = "Ice Demon";
      HP = 33;
    }

    public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
    {
      var cardsInHandEntities = GetComponentHelper.GetHandOfCards(entityManager);
      int frozenCardsInHand = cardsInHandEntities?.Count(c => c.HasComponent<Frozen>()) ?? 0;
      if (frozenCardsInHand > 1 && Random.Shared.Next(0, 100) <= 75)
      {
        return [EnemyAttackId.FrostEater];
      }
      return ArrayUtils.TakeRandomWithReplacement(new List<EnemyAttackId> { EnemyAttackId.IcyBlade, EnemyAttackId.FrozenClaw }, 1);
    }
  }
}

public class IcyBlade : EnemyAttackBase
{
  private int Frostbite = 2;
  public IcyBlade()
  {
    Id = EnemyAttackId.IcyBlade;
    Name = "Icy Blade";
    Damage = 11;
    ConditionType = ConditionType.OnBlockedByAtLeast2Cards;
    Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Frostbite, Frostbite, ConditionType);

    OnAttackHit = (entityManager) =>
    {
      EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Player"), Type = AppliedPassiveType.Frostbite, Delta = Frostbite });
    };
  }
}

public class FrozenClaw : EnemyAttackBase
{
  public FrozenClaw()
  {
    Id = EnemyAttackId.FrozenClaw;
    Name = "Frozen Claw";
    Damage = 10;
    AttackEffectRecipe = EnemyClawSlashEffect();
    BlockRequiredToPreventEffect = 6;
    Text = $"On attack - Intimidate 1 card.\n\n{EnemyAttackTextHelper.GetBlockThresholdText(Damage - BlockRequiredToPreventEffect.Value, "Freeze the top card of your draw pile.")}";

    OnAttackReveal = (entityManager) =>
    {
      EventManager.Publish(new IntimidateEvent { Amount = 1 });
    };

    OnDamageThresholdMet = (entityManager) =>
    {
      EventManager.Publish(new ApplyCardApplicationEvent
      {
        Amount = 1,
        Type = CardApplicationType.Frozen,
        Target = CardApplicationTarget.TopXCards,
      });
    };
  }
}

// might not want here but made it so can be used elsewhere

public class FrostEater : EnemyAttackBase
{
  public FrostEater()
  {
    Id = EnemyAttackId.FrostEater;
    Name = "Frost Eater";
    Damage = 9;
    Text = "Frozen cards have -1 block value when blocking this attack.";

    ProgressOverride = (entityManager) =>
    {
      var p = entityManager.GetEntitiesWithComponent<EnemyAttackProgress>()
        .FirstOrDefault()?
        .GetComponent<EnemyAttackProgress>();
      if (p == null) return false;

      var assignedFrozenBlockCards = entityManager.GetEntitiesWithComponent<AssignedBlockCard>()
              .Where(e => !e.GetComponent<AssignedBlockCard>().IsEquipment && e.GetComponent<Frozen>() != null)
              .ToList();
      p.EffectiveAssignedBlockTotal = Math.Max(0, p.AssignedBlockTotal - assignedFrozenBlockCards.Count);
      return false;
    };
  }

}
