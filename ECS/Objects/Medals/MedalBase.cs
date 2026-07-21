using System;
using System.Collections.Generic;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.VisualEffects;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Medals
{
    public abstract class MedalBase : IDisposable
    {
        public string Id { get; set; }
        public string Name { get; set; } = "";
        public string Text { get; set; } = "";
        public EntityManager EntityManager { get; set; }
        public Entity MedalEntity { get; set; }
        public VisualEffectRecipe ActivationEffectRecipe { get; protected set; }

        public int CurrentCount { get; set; } = 0;
        public int MaxCount { get; set; } = 0;
        public bool Activated { get; set; } = false;
        public abstract void Initialize(EntityManager entityManager, Entity medalEntity);

        protected void EmitActivateEvent(){
            EventManager.Publish(new MedalActivateEvent { MedalEntity = MedalEntity });
        }

        public virtual void Activate(){
            Console.WriteLine($"[MedalBase] Activate: {Id}");
        }

        public virtual void OnAcquire()
        {
            Console.WriteLine($"[MedalBase] OnAcquire: {Id}");
        }

        protected static VisualEffectRecipe HolySupportEffect()
        {
            return VisualEffectPresets.HolySupport();
        }

        public virtual void Dispose()
        {
            Console.WriteLine($"[MedalBase] Dispose: {Id}");
        }
    }
}
