#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Cards;

/// <summary>
/// Scheduler-safe card-domain composition. Only systems with meaningful per-frame work appear in
/// <see cref="Systems"/>. The 26 migration responsibility names remain available for audit and
/// direct API ownership through <see cref="CompatibilitySystems"/>, but are never an implicit
/// scheduler registration list.
/// </summary>
public sealed class CardGameplayComposition
{
    private readonly IGameSystem[] systems;
    private readonly IGameSystem[] compatibilitySystems;
    private readonly IEventRoute[] routes;

    private CardGameplayComposition(
        IGameSystem[] systems,
        IGameSystem[] compatibilitySystems,
        IEventRoute[] routes)
    {
        this.systems = systems;
        this.compatibilitySystems = compatibilitySystems;
        this.routes = routes;
    }

    /// <summary>Explicit allowlist safe to register with the root scheduler.</summary>
    public ReadOnlySpan<IGameSystem> Systems => systems;

    /// <summary>Unscheduled compatibility/API owners covering the 26-row ECS-041 system ledger.</summary>
    public ReadOnlySpan<IGameSystem> CompatibilitySystems => compatibilitySystems;

    /// <summary>All 58 root-composable card route fragments.</summary>
    public ReadOnlySpan<IEventRoute> Routes => routes;

    public IEventRoute[] GetRoutes() => (IEventRoute[])routes.Clone();

    public static CardGameplayComposition Create(
        World world,
        CardGameplayEventHub events,
        CardGameplayRouteConsumers? rootConsumers = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(events);

        var actionPoints = new ActionPointManagementSystem(world);
        var deck = new DeckManagementSystem(world, events);
        var cardList = new CardListModalSystem(world);
        var pileInput = new BattlePileInputSystem(world, events, cardList.Modal);
        IGameSystem[] compatibilitySystems =
        [
            actionPoints,
            new AssignedBlocksToDiscardSystem(),
            new DrawHandSystem(),
            new CanPlayCardHighlightSystem(),
            new CantPlayCardMessageSystem(),
            new CardApplicationManagementSystem(),
            new CardHoverDetectionSystem(),
            new CardPlaySystem(world, events),
            new CardZoneSystem(world, events),
            new CursedManagementSystem(world),
            new DeckEmptyDeathCheckSystem(),
            deck,
            new DiscardSpecificCardHighlightSystem(),
            new HandBlockInteractionSystem(),
            new HandCardBoundsLateSystem(),
            new MarkedForExhaustSystem(world, events),
            new MarkedForSpecificDiscardSystem(),
            new MillCardSystem(world, events),
            new PledgeManagementSystem(world, events),
            new PlunderManagementSystem(world),
            new RecoilManagementSystem(world),
            new SealManagementSystem(world),
            new ShackleManagementSystem(world),
            cardList,
            new CardShaderCompositorSystem(),
            new CardUsageTrackingSystem(),
        ];

        var localConsumers = new CardGameplayRouteConsumers()
            .Add<ModifyActionPointsEvent>(actionPoints, priority: 100)
            .Add<SetActionPointsEvent>(actionPoints, priority: 100)
            .Add<DeckShuffleDrawEvent>(deck, priority: 100)
            .Add<DeckShuffleEvent>(deck, priority: 100)
            .Add<DiscardAllCardsEvent>(deck, priority: 100)
            .Add<DrawRandomCardFromDiscardEvent>(deck, priority: 100)
            .Add<RedrawHandEvent>(deck, priority: 100)
            .Add<ResetDeckEvent>(deck, priority: 100)
            .Add<ShuffleRandomCardsFromDiscardToDrawPileEvent>(deck, priority: 100)
            .Add<OpenCardListModalEvent>(cardList, priority: 100)
            .Add<CloseCardListModalEvent>(cardList, priority: 100);

        return new CardGameplayComposition(
            systems: [deck, pileInput],
            compatibilitySystems,
            events.BuildRoutes(localConsumers, rootConsumers));
    }
}
