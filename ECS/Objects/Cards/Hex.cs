using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards;

public sealed class Hex : CardBase
{
    public const string CardIdValue = "hex";

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
