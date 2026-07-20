using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;

namespace Crusaders30XX.ECS.Objects.Enemies;

public enum ClimbEncounterPool
{
  None = 0,
  Early = 1,
  Late = 2,
  /// <summary>Eligible in both Early and Late climb encounter rolls.</summary>
  Throughout = 3,
}

public abstract class EnemyBase : IDisposable
{


  public EnemyId Id { get; set; }
  public string Name { get; set; }
  public int MaxHealth { get; set; }
  public int CurrentHealth { get; set; }
  public int HP { get; protected set; }
  protected int StartingHealthBelowMax { get; set; }
  public Action<EntityManager> OnStartOfBattle { get; protected set; }
  public EntityManager EntityManager { get; set; }
  public bool IsBoss { get; set; } = false;
  public bool IsTutorialOnly { get; protected set; }
  public ClimbEncounterPool ClimbPool { get; protected set; } = ClimbEncounterPool.None;
  public int Phases { get; set; } = 1;
  public int CurrentPhase { get; set; } = 1;

  public EnemyBase() { }

  public void ApplyBaseHealth()
  {
    MaxHealth = HP;
    CurrentHealth = MaxHealth - StartingHealthBelowMax;
  }

  public virtual void Dispose()
  {
    Console.WriteLine($"[EnemyBase] Dispose: {Id.ToKey()}");
  }

  public abstract IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber);
}
