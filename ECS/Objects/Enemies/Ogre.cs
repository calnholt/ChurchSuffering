using System;
using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.Enemies;

namespace ChurchSuffering.ECS.Objects.EnemyAttacks
{
  public class Ogre : EnemyBase
  {
    private int PummelIntoSubmissionCount = 0;
    public Ogre()
    {
      Id = EnemyId.Ogre;
      Name = "Ogre";
      HP = 31;
      ClimbPool = ClimbEncounterPool.Early;
    }

    public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
    {
      int random = Random.Shared.Next(0, 100);
      if (random <= 20)
      {
        return [EnemyAttackId.SlamTrunk, EnemyAttackId.FakeOut];
      }
      if (random <= 40)
      {
        return [EnemyAttackId.SlamTrunk, EnemyAttackId.Thud];
      }
      if (random <= 60)
      {
        return [EnemyAttackId.TreeStomp];
      }
      if (random <= 80 && PummelIntoSubmissionCount < 2)
      {
        PummelIntoSubmissionCount++;
        return [EnemyAttackId.PummelIntoSubmission];
      }
      return [EnemyAttackId.SlamTrunk, EnemyAttackId.HaveNoMercy];
    }
  }
  public class PummelIntoSubmission : EnemyAttackBase
  {
    private int Scar = 1;
    public PummelIntoSubmission()
    {
      Id = EnemyAttackId.PummelIntoSubmission;
      Name = "Pummel Into Submission";
      Damage = 6;
      ConditionType = ConditionType.OnBlockedByAtLeast2Cards;
      Text = $"{EnemyAttackTextHelper.GetText(EnemyAttackTextType.Intimidate, 1)}\n\n{EnemyAttackTextHelper.GetText(EnemyAttackTextType.Scar, Scar, ConditionType)}";
      OnAttackReveal = (entityManager) =>
      {
        EventManager.Publish(new IntimidateEvent { Amount = Scar });
      };
      OnAttackHit = (entityManager) =>
      {
        EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Player"), Type = AppliedPassiveType.Scar, Delta = Scar });
      };
    }
  }
  public class TreeStomp : EnemyAttackBase
  {
    private int IntimidateAmount = 2;
    public TreeStomp()
    {
      Id = EnemyAttackId.TreeStomp;
      Name = "Tree Stomp";
      Damage = 9;
      Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Intimidate, 2);
      OnAttackReveal = (entityManager) =>
      {
        EventManager.Publish(new IntimidateEvent { Amount = IntimidateAmount });
      };
    }
  }

  public class SlamTrunk : EnemyAttackBase
  {
    private int IntimidateAmount = 1;
    public SlamTrunk()
    {
      Id = EnemyAttackId.SlamTrunk;
      Name = "Slam Trunk";
      Damage = 4;
      Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Intimidate, 1);
      OnAttackReveal = (entityManager) =>
      {
        EventManager.Publish(new IntimidateEvent { Amount = IntimidateAmount });
      };
    }
  }

  public class FakeOut : EnemyAttackBase
  {
    private int IntimidateAmount = 2;
    public FakeOut()
    {
      Id = EnemyAttackId.FakeOut;
      Name = "Fake Out";
      Damage = 3;
      Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Intimidate, 2);
      OnAttackReveal = (entityManager) =>
      {
        EventManager.Publish(new IntimidateEvent { Amount = IntimidateAmount });
      };
    }
  }
  public class Thud : EnemyAttackBase
  {
    private int WoundedAmount = 1;
    public Thud()
    {
      Id = EnemyAttackId.Thud;
      Name = "Thud";
      Damage = 3;
      Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Wounded, 1);
      ConditionType = ConditionType.OnHit;
      OnAttackHit = (entityManager) =>
      {
        EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Player"), Type = AppliedPassiveType.Wounded, Delta = WoundedAmount });
      };
    }
  }
}