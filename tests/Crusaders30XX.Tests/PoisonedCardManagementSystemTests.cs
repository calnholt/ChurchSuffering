using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class PoisonedCardManagementSystemTests : System.IDisposable
{
    public PoisonedCardManagementSystemTests() => EventManager.Clear();
    public void Dispose() => EventManager.Clear();

    [Fact]
    public void Preblock_marks_one_card_and_resolved_block_costs_one_hp_then_cleans_up()
    {
        var em = new EntityManager();
        var player = em.CreateEntity("Player");
        em.AddComponent(player, new Player());
        em.AddComponent(player, new AppliedPassives { Passives = { [AppliedPassiveType.Poison] = 3 } });
        var deckEntity = em.CreateEntity("Deck"); var deck = new Deck(); em.AddComponent(deckEntity, deck);
        for (int i = 0; i < 3; i++) { var card=em.CreateEntity($"Card_{i}"); em.AddComponent(card,new CardData { Card=new CardBase { Block=2 } }); deck.Hand.Add(card); }
        _ = new PoisonedCardManagementSystem(em);
        EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.PreBlock });
        var poisoned = em.GetEntitiesWithComponent<Poisoned>().Single();
        ModifyHpRequestEvent damage = null;
        EventManager.Subscribe<ModifyHpRequestEvent>(evt => damage = evt);
        EventManager.Publish(new CardMoved { Card=poisoned, From=CardZoneType.AssignedBlock, To=CardZoneType.DiscardPile });
        Assert.Equal(-1, damage.Delta);
        EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.EnemyEnd });
        Assert.Empty(em.GetEntitiesWithComponent<Poisoned>());
    }

    [Fact]
    public void Poison_is_run_long_and_decrements_at_player_end()
    {
        Assert.Contains(AppliedPassiveType.Poison, AppliedPassivesManagementSystem.GetRunLongPassives());
        Assert.Contains(AppliedPassiveType.Poison, AppliedPassivesManagementSystem.GetTurnPassivesToDecrement());
    }
}
