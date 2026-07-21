using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Temperance
{
    public class AngelicAura : TemperanceBase
    {
        public int AegisAmount = 3;
        public AngelicAura()
        {
            Id = "angelic_aura";
            Name = "Angelic Aura";
            Target = "Player";
            Text = $"Gain {AegisAmount} aegis.";
            Threshold = 2;
        }

        public override void Activate(EntityManager entityManager)
        {
            PublishTrigger(entityManager);
            var player = GetPlayer(entityManager);
            EventManager.Publish(new ApplyPassiveEvent { Target = player, Type = AppliedPassiveType.Aegis, Delta = AegisAmount });
        }
    }
}
