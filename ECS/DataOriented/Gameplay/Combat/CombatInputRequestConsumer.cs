#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Cards;
using Crusaders30XX.ECS.DataOriented.Gameplay.Meta;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Combat;

/// <summary>Combat-owned execution endpoint for battle input requests.</summary>
public sealed class CombatInputRequestConsumer :
    IEventConsumer<AssignCardAsBlockRequested>,
    IEventConsumer<UnassignCardAsBlockRequested>,
    IEventConsumer<ConfirmBlocksRequested>,
    IEventConsumer<EndTurnRequested>
{
    private readonly CombatSessionSlot sessions;

    public CombatInputRequestConsumer(CombatSessionSlot sessions) =>
        this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));

    public void Consume(in AssignCardAsBlockRequested value, ref EventDispatchContext context)
    {
        CombatSession? session = Match(value.Attack);
        if (session is null || !session.World.TryGet(value.Card, out CardData card)) return;
        session.AssignBlock(
            value.Card,
            Math.Max(0, card.Block),
            card.RuntimeColor,
            frozen: session.World.Has<Frozen>(value.Card),
            sealedCard: session.World.Has<Sealed>(value.Card));
    }

    public void Consume(in UnassignCardAsBlockRequested value, ref EventDispatchContext context) =>
        Match(value.Attack)?.RemoveBlock(value.Card);

    public void Consume(in ConfirmBlocksRequested value, ref EventDispatchContext context) =>
        Match(value.Battle)?.ConfirmBlocks();

    public void Consume(in EndTurnRequested value, ref EventDispatchContext context) =>
        Match(value.Battle)?.EndActionPhase();

    private CombatSession? Match(EntityId battle)
    {
        CombatSession? session = sessions.Current;
        return session is not null && session.Battle == battle ? session : null;
    }
}
