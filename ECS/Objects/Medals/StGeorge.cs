using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Objects.Medals
{
    public class StGeorge : MedalBase, IAlternateCardPlayProvider
    {
        private const int AttackDamage = 3;
        public const string MedalIdValue = "st_george";

        public StGeorge()
        {
            Id = MedalIdValue;
            Name = "St. George";
            Text = "Your block cards can be played as 3 damage free action attacks.";
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EntityManager = entityManager;
            MedalEntity = medalEntity;
        }

        public AlternateCardPlayProfile GetAlternatePlayProfile(AlternateCardPlayQuery query)
        {
            if (query?.Phase != SubPhase.Action) return null;

            var data = query.Card?.GetComponent<CardData>();
            var card = data?.Card;
            if (card == null) return null;
            if (card.Type != CardType.Block) return null;
            if (card.IsWeapon || card.IsToken) return null;

            return new AlternateCardPlayProfile
            {
                SourceId = Id,
                SourceType = "Medal",
                SourceEntity = MedalEntity,
                AllowsPlay = true,
                IsFreeAction = true,
                TreatsAsAttack = true,
                AttackDamage = AttackDamage,
            };
        }
    }
}
