using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Objects.Medals
{
    public class StChristopher : MedalBase, ICardStatModifierProvider
    {
        public const string MedalId = "st_christopher";
        private const int BrittleBlockBonus = 1;

        public StChristopher()
        {
            Id = MedalId;
            Name = "St. Christopher";
            Text = $"Your brittle cards have +{BrittleBlockBonus} block.";
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EntityManager = entityManager;
            MedalEntity = medalEntity;
        }

        public IEnumerable<CardStatModifier> GetStatModifiers(CardStatQuery query)
        {
            if (query?.Kind != CardStatKind.Block) yield break;
            if (query.Card?.GetComponent<Brittle>() == null) yield break;

            yield return new CardStatModifier
            {
                Delta = BrittleBlockBonus,
                Reason = Id,
                SourceId = Id,
                SourceType = "Medal",
            };
        }
    }
}
