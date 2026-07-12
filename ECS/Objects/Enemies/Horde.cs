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

public class Horde : EnemyBase
{
  public Horde()
  {
    Id = EnemyId.Horde;
    Name = "Horde";
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
    Console.WriteLine("[Horde] GetAttackIds: turnNumber=" + turnNumber);
    return [EnemyAttackId.Pounce];
  }
}

public class TutorialHordeStrike : EnemyAttackBase
{
  public TutorialHordeStrike()
  {
    Id = EnemyAttackId.TutorialHordeStrike;
    Name = "Pounce";
    Damage = 9;
    GuardConversionChance = 0f;
  }
}

public class TutorialHordeStrike3 : EnemyAttackBase
{
  public TutorialHordeStrike3()
  {
    Id = EnemyAttackId.TutorialHordeStrike3;
    Name = "Pounce";
    Damage = 3;
    GuardConversionChance = 0f;
  }
}

public class TutorialHordeStrike5 : EnemyAttackBase
{
  public TutorialHordeStrike5()
  {
    Id = EnemyAttackId.TutorialHordeStrike5;
    Name = "Pounce";
    Damage = 5;
    GuardConversionChance = 0f;
  }
}

public class TutorialHordeStrike6 : EnemyAttackBase
{
  public TutorialHordeStrike6()
  {
    Id = EnemyAttackId.TutorialHordeStrike6;
    Name = "Pounce";
    Damage = 6;
    GuardConversionChance = 0f;
  }
}

public class TutorialHordeStrike8 : EnemyAttackBase
{
  public TutorialHordeStrike8()
  {
    Id = EnemyAttackId.TutorialHordeStrike8;
    Name = "Pounce";
    Damage = 8;
    GuardConversionChance = 0f;
  }
}

public class TutorialHordeStrike9 : EnemyAttackBase
{
  public TutorialHordeStrike9()
  {
    Id = EnemyAttackId.TutorialHordeStrike9;
    Name = "Pounce";
    Damage = 9;
    GuardConversionChance = 0f;
  }
}
public class TutorialHordeStrike7 : EnemyAttackBase
{
  public TutorialHordeStrike7()
  {
    Id = EnemyAttackId.TutorialHordeStrike7;
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
