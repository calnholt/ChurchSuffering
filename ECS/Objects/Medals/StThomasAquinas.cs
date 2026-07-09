using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Medals
{
    public class StThomasAquinas : MedalBase
    {
        private const int HpLoss = 10;
        private const int HandSizeIncrease = 1;

        public StThomasAquinas()
        {
            Id = "st_thomas_aquinas";
            Name = "St. Thomas Aquinas";
            Text = $"Lose {HpLoss} max HP when acquired. Your max hand size is increased by {HandSizeIncrease}.";
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EntityManager = entityManager;
            MedalEntity = medalEntity;
        }

        public override void OnAcquire()
        {
            var player = EntityManager.GetEntity("Player");
            EventManager.Publish(new IncreaseMaxHpEvent { Target = player, Delta = -HpLoss });
            var maxHandSize = player?.GetComponent<MaxHandSize>();
            if (maxHandSize != null)
                maxHandSize.Value += HandSizeIncrease;
        }
    }
}
