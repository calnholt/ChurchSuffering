using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Temperance
{
    public class StaticSurge : TemperanceBase
    {
        public StaticSurge()
        {
            Id = "static_surge";
            Name = "Static Surge";
            Target = "Player";
            Text = "Gain galvanize.";
            Threshold = 3;
        }

        public override void Activate(EntityManager entityManager)
        {
            PublishTrigger(entityManager);
            var player = GetPlayer(entityManager);
            EventManager.Publish(new ApplyPassiveEvent
            {
                Target = player,
                Type = AppliedPassiveType.Galvanize,
                Delta = 1
            });
        }
    }
}
