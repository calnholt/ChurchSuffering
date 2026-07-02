using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;

namespace Crusaders30XX.ECS.Objects.Enemies;

public abstract class EnemyBase : IDisposable
{


  public EnemyId Id { get; set; }
  public string Name { get; set; }
  public int MaxHealth { get; set; }
  public int CurrentHealth { get; set; }
  public const int ReferenceDeckCardCount = 20;
  public int HP { get; protected set; }
  protected int StartingHealthBelowMax { get; set; }
  public Action<EntityManager> OnStartOfBattle { get; protected set; }
  public EntityManager EntityManager { get; set; }
  public bool IsBoss { get; set; } = false;
  public bool IsTutorialOnly { get; protected set; }
  public int Phases { get; set; } = 1;
  public int CurrentPhase { get; set; } = 1;

  public EnemyBase() { }

  public void ApplyHealthFromDeckSize(int deckCardCount)
  {
    ApplyHealthFromDeckWeight(deckCardCount);
  }

  public void ApplyHealthFromDeckWeight(float deckWeight)
  {
    MaxHealth = (int)Math.Round(HP * Math.Max(0f, deckWeight) / ReferenceDeckCardCount);
    CurrentHealth = MaxHealth - StartingHealthBelowMax;
  }

  public virtual void Dispose()
  {
    Console.WriteLine($"[EnemyBase] Dispose: {Id.ToKey()}");
  }

  public abstract IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber);
}
