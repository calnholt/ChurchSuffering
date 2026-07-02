using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Enemies;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Data.Tutorials;
using Crusaders30XX.ECS.Components;

namespace Crusaders30XX.ECS.Objects.EnemyAttacks;

public class Gleeber : EnemyBase
{
  public Gleeber()
  {
    Id = EnemyId.Gleeber;
    Name = "Gleeber";
    IsTutorialOnly = true;
    HP = 14;
  }
  public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
  {
    if (GuidedTutorialService.IsActive(entityManager))
    {
      var state = GuidedTutorialService.GetState(entityManager);
      if (state != null)
        return GuidedTutorialDefinitions.GetTurn(state.Section, turnNumber).AttackIds
          .Select(id => GameIdExtensions.TryParseEnemyAttackId(id, out var parsed)
            ? parsed
            : EnemyAttackId.Pounce);
    }
    Console.WriteLine("[Gleeber] GetAttackIds: turnNumber=" + turnNumber);
    return [EnemyAttackId.Pounce];
  }
}

public class TutorialGleeberStrike : EnemyAttackBase
{
  public TutorialGleeberStrike()
  {
    Id = EnemyAttackId.TutorialGleeberStrike9;
    Name = "Pounce";
    Damage = 9;
    GuardConversionChance = 0f;
  }
}

public class TutorialGleeberStrike3 : EnemyAttackBase
{
  public TutorialGleeberStrike3()
  {
    Id = EnemyAttackId.TutorialGleeberStrike3;
    Name = "Pounce";
    Damage = 3;
    GuardConversionChance = 0f;
  }
}

public class TutorialGleeberStrike5 : EnemyAttackBase
{
  public TutorialGleeberStrike5()
  {
    Id = EnemyAttackId.TutorialGleeberStrike5;
    Name = "Pounce";
    Damage = 5;
    GuardConversionChance = 0f;
  }
}

public class TutorialGleeberStrike6 : EnemyAttackBase
{
  public TutorialGleeberStrike6()
  {
    Id = EnemyAttackId.TutorialGleeberStrike6;
    Name = "Pounce";
    Damage = 6;
    GuardConversionChance = 0f;
  }
}

public class TutorialGleeberStrike8 : EnemyAttackBase
{
  public TutorialGleeberStrike8()
  {
    Id = EnemyAttackId.TutorialGleeberStrike8;
    Name = "Pounce";
    Damage = 8;
    GuardConversionChance = 0f;
  }
}

public class TutorialGleeberStrike9 : EnemyAttackBase
{
  public TutorialGleeberStrike9()
  {
    Id = EnemyAttackId.TutorialGleeberStrike9;
    Name = "Pounce";
    Damage = 9;
    GuardConversionChance = 0f;
  }
}
public class TutorialGleeberStrike7 : EnemyAttackBase
{
  public TutorialGleeberStrike7()
  {
    Id = EnemyAttackId.TutorialGleeberStrike7;
    Name = "Pounce";
    Damage = 7;
    GuardConversionChance = 0f;
  }
}

public class Pounce : EnemyAttackBase
{
  public Pounce()
  {
    Id = EnemyAttackId.Pounce;
    Name = "Pounce";
    Damage = 5;
    GuardConversionChance = 0f;
  }
}
