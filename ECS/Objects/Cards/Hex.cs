using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards;

public sealed class Hex : CardBase
{
    public static readonly string CardIdValue = CardIds.Hex.ToKey();

    public Hex()
    {
        CardId = CardIdValue;
        Name = "Hex";
        Target = "Enemy";
        Damage = 5;
        Block = 3;
        IsFreeAction = true;
        CanAddToLoadout = false;
        Text = "This card becomes cursed.\n\nAt the end of the turn, remove Hex from this card.";
        VisualEffectRecipe = PlayerAttackEffect();

        OnPlay = (entityManager, card) =>
        {
            EventManager.Publish(new ModifyHpRequestEvent
            {
                Source = entityManager.GetEntity("Player"),
                Target = entityManager.GetEntity(Target),
                Delta = -GetDerivedDamage(entityManager, card),
                AttackCard = card,
                DamageType = ModifyTypeEnum.Attack,
            });
        };
    }
}
