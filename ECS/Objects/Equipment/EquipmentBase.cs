using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.VisualEffects;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Equipment
{
  public abstract class EquipmentBase : IDisposable
  {
    public string Id { get; set; }
    public string Name { get; set; }
    public string Text { get; set; }
    public string FlavorText { get; set; }
    public bool CanActivateDuringActionPhase { get; set; }
    public EntityManager EntityManager { get; set; }
    public int Block { get; set; }
    public bool IsUsed { get; private set; }
    public CardData.CardColor Color { get; set; }
    public EquipmentSlot Slot { get; set; }
    public Entity EquipmentEntity { get; set; }
    public VisualEffectRecipe ActivationEffectRecipe { get; protected set; }

    public virtual void Initialize(EntityManager entityManager, Entity equipmentEntity) {
      EntityManager = entityManager;
      EquipmentEntity = equipmentEntity;
      RefreshForBattle();
    }


    public void EmitActivateEvent(){
      EventManager.Publish(new EquipmentActivateEvent { EquipmentEntity = EquipmentEntity });
    }

    public virtual void CantActivateMessage()
    {
      EventManager.Publish(new CantPlayCardMessage { Message = "This equipment has already been used this battle!" });
    }

    public virtual void Dispose()
    {
      Console.WriteLine($"[EquipmentBase] Dispose: {Id}");
    }

    public bool IsAvailable { get => !IsUsed; }

    public void MarkUsed()
    {
      IsUsed = true;
    }

    public void RefreshForBattle()
    {
      IsUsed = false;
    }

    public Action<EntityManager, Entity> OnActivate { get; protected set; } = (entityManager, entity) => { };
    public Func<bool> CanActivate { get; protected set; } = () => true;

    protected static VisualEffectRecipe DefensiveGuardEffect()
    {
      return VisualEffectPresets.DefensiveGuard();
    }

  }


}
