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
    public int MaxUses { get; protected set; } = 3;
    public int RemainingUses { get; private set; } = 3;
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
      string message = RemainingUses <= 0
        ? "This equipment has no uses remaining!"
        : "This equipment has already been used this battle!";
      EventManager.Publish(new CantPlayCardMessage { Message = message });
    }

    public virtual void Dispose()
    {
      Console.WriteLine($"[EquipmentBase] Dispose: {Id}");
    }

    public bool IsAvailable { get => RemainingUses > 0 && !IsUsed; }

    public void MarkUsed()
    {
      if (RemainingUses <= 0) return;
      RemainingUses--;
      IsUsed = true;
      EventManager.Publish(new EquipmentRemainingUsesChanged
      {
        EquipmentId = Id,
        Slot = Slot,
        RemainingUses = RemainingUses,
      });
    }

    public void SetRemainingUses(int remainingUses)
    {
      RemainingUses = Math.Clamp(remainingUses, 0, Math.Max(0, MaxUses));
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
