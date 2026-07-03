using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Medals
{
    public class StNicholas : MedalBase
    {
        public int HpIncrease { get; set; } = 2;
        public int FrozenCards { get; set; } = 4;
        public StNicholas()
        {
            Id = "st_nicholas";
            Name = "St. Nicholas the Bishop";
            Text = $"When this is acquired, increase your max HP by {HpIncrease} and {FrozenCards} random cards from your deck become frozen.";
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EntityManager = entityManager;
            MedalEntity = medalEntity;
        }

        public override void OnAcquire()
        {
            EventManager.Publish(new IncreaseMaxHpEvent { Target = EntityManager.GetEntity("Player"), Delta = HpIncrease });
            EventManager.Publish(new ApplyCardApplicationEvent
            {
                Amount = FrozenCards,
                Type = CardApplicationType.Frozen,
                Target = CardApplicationTarget.Deck,
            });
        }
    }
}
