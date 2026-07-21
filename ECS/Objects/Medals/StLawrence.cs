using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Systems;

namespace ChurchSuffering.ECS.Objects.Medals
{
    public class StLawrence : MedalBase, ICardStatModifierProvider
    {
        public const string MedalId = "st_lawrence";

        public StLawrence()
        {
            Id = MedalId;
            Name = "St. Lawrence";
            Text = "Your scorched cards deal +X damage, where X is the number of cards discarded to play it.";
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EntityManager = entityManager;
            MedalEntity = medalEntity;
        }

        public IEnumerable<CardStatModifier> GetStatModifiers(CardStatQuery query)
        {
            if (query?.Kind != CardStatKind.Damage) yield break;
            if (query.Mode != CardStatQueryMode.Resolution) yield break;
            if (query.Card?.GetComponent<Scorched>() == null) yield break;

            int paymentCount = query.PaymentCards?.Count ?? 0;
            yield return new CardStatModifier
            {
                Delta = paymentCount,
                Reason = Id,
                SourceId = Id,
                SourceType = "Medal",
            };
        }
    }
}
