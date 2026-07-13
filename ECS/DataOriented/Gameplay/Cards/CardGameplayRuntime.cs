#nullable enable

using System;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Content.Cards;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Rules;
using Crusaders30XX.ECS.DataOriented.Storage;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Cards;

public struct PendingCardSpawn : IComponent
{
    public EntityId Deck;
    public CardZone Destination;
    public int DestinationIndex;
}

public struct CardApplications : IComponent
{
    public DynamicBufferHandle<CardApplicationEntry> Entries;
}

public static class CardGameplayFactory
{
    public static EntityId CreateDeck(World world, EntityId owner, ulong seed, int maximumHandSize = 4)
    {
        ArgumentNullException.ThrowIfNull(world);
        var bundle = new SpawnBundle(1);
        bundle.Add(new Deck { Owner = owner, Random = RuleRandomState.FromSeed(seed), MaximumHandSize = maximumHandSize });
        EntityId entity = world.Create(in bundle);
        ref Deck deck = ref world.Get<Deck>(entity);
        deck.Cards = world.CreateDynamicBuffer<MasterDeckCard>(entity, 16);
        deck.DrawPile = world.CreateDynamicBuffer<DrawPileCard>(entity, 16);
        deck.Hand = world.CreateDynamicBuffer<HandCard>(entity, maximumHandSize);
        deck.DiscardPile = world.CreateDynamicBuffer<DiscardPileCard>(entity, 16);
        deck.ExhaustPile = world.CreateDynamicBuffer<ExhaustPileCard>(entity, 8);
        deck.AssignedBlocks = world.CreateDynamicBuffer<AssignedBlockCard>(entity, 8);
        return entity;
    }

    public static EntityId CreateCard(
        World world,
        EntityId deck,
        CardId definition,
        CardZone destination,
        bool upgraded = false,
        int destinationIndex = -1,
        RuleCardColor color = RuleCardColor.None)
    {
        SpawnBundle bundle = BuildCardBundle(deck, definition, destination, upgraded, destinationIndex, color, pending: false);
        EntityId card = world.Create(in bundle);
        CardZoneOperations.AddToMasterDeck(world, deck, card);
        CardZoneOperations.Insert(world, deck, destination, card, destinationIndex);
        CardZoneOperations.RefreshIndices(world, deck, destination);
        return card;
    }

    public static DeferredEntity RecordCardSpawn(
        CommandBuffer commands,
        EntityId deck,
        CardId definition,
        CardZone destination,
        bool upgraded = false,
        int destinationIndex = -1,
        RuleCardColor color = RuleCardColor.None)
    {
        ArgumentNullException.ThrowIfNull(commands);
        SpawnBundle bundle = BuildCardBundle(deck, definition, destination, upgraded, destinationIndex, color, pending: true);
        return commands.Create(in bundle);
    }

    public static CardData BuildCardData(
        EntityId deck,
        CardId definition,
        bool upgraded,
        RuleCardColor color = RuleCardColor.None)
    {
        ref readonly CardDefinitionData printed = ref GeneratedCardCatalog.GetDefinition(definition);
        ref readonly CardUpgradeDelta upgrade = ref GeneratedCardCatalog.GetUpgrade(definition);
        int damage = printed.Damage + (upgraded ? upgrade.DamageDelta : 0);
        int block = printed.Block + (upgraded ? upgrade.BlockDelta : 0);
        CardCost cost = upgraded && upgrade.ReplacementCost is not null ? upgrade.ReplacementCost : printed.Cost;
        CardDefinitionType type = upgraded && upgrade.ReplacementType.HasValue ? upgrade.ReplacementType.Value : printed.Type;
        CardDefinitionFlags flags = printed.Flags;
        if (upgraded)
            flags = (flags | upgrade.AddFlags) & ~upgrade.RemoveFlags;

        CardRuntimeFlags runtimeFlags = upgraded ? CardRuntimeFlags.Upgraded : CardRuntimeFlags.None;
        if ((flags & CardDefinitionFlags.FreeAction) != 0) runtimeFlags |= CardRuntimeFlags.FreeAction;
        if ((flags & CardDefinitionFlags.ExhaustsOnEndTurn) != 0) runtimeFlags |= CardRuntimeFlags.ExhaustsOnEndTurn;
        if ((flags & CardDefinitionFlags.Weapon) != 0) runtimeFlags |= CardRuntimeFlags.Weapon;
        if ((flags & CardDefinitionFlags.Token) != 0) runtimeFlags |= CardRuntimeFlags.Token;
        if ((flags & CardDefinitionFlags.Starter) != 0) runtimeFlags |= CardRuntimeFlags.Starter;
        if ((flags & CardDefinitionFlags.CanAddToLoadout) != 0) runtimeFlags |= CardRuntimeFlags.CanAddToLoadout;

        RuleCardColor instanceColor = ResolveColor(printed.PrintedColors, color);
        return new CardData
        {
            Definition = definition,
            PrintedDefinition = definition,
            Deck = deck,
            PrintedColor = instanceColor,
            RuntimeColor = instanceColor,
            Type = ToRuleType(type),
            Flags = runtimeFlags,
            Damage = damage,
            Block = block,
            CostCount = checked((byte)cost.Count),
            Cost0 = CostAt(cost, 0),
            Cost1 = CostAt(cost, 1),
            Cost2 = CostAt(cost, 2),
            Cost3 = CostAt(cost, 3),
        };
    }

    private static SpawnBundle BuildCardBundle(
        EntityId deck,
        CardId definition,
        CardZone destination,
        bool upgraded,
        int destinationIndex,
        RuleCardColor color,
        bool pending)
    {
        var bundle = new SpawnBundle(pending ? 3 : 2, 128);
        CardData data = BuildCardData(deck, definition, upgraded, color);
        bundle.Add(in data);
        bundle.Add(new CardZoneLocation { Deck = deck, Zone = pending ? CardZone.None : destination, Index = pending ? -1 : destinationIndex });
        if (pending)
            bundle.Add(new PendingCardSpawn { Deck = deck, Destination = destination, DestinationIndex = destinationIndex });
        return bundle;
    }

    private static CardCostColor CostAt(CardCost cost, int index) => index < cost.Count ? cost[index] : CardCostColor.Any;
    private static RuleCardColor ToRuleColor(CardPrintedColors colors) => colors switch
    {
        CardPrintedColors.Red => RuleCardColor.Red,
        CardPrintedColors.White => RuleCardColor.White,
        CardPrintedColors.Black => RuleCardColor.Black,
        _ => RuleCardColor.None,
    };

    private static RuleCardColor ResolveColor(CardPrintedColors allowed, RuleCardColor requested)
    {
        if (requested == RuleCardColor.None) return ToRuleColor(allowed);
        CardPrintedColors flag = requested switch
        {
            RuleCardColor.Red => CardPrintedColors.Red,
            RuleCardColor.White => CardPrintedColors.White,
            RuleCardColor.Black => CardPrintedColors.Black,
            _ => CardPrintedColors.None,
        };
        if (flag == CardPrintedColors.None || (allowed & flag) == 0)
            throw new ArgumentOutOfRangeException(nameof(requested), requested, "The card definition cannot be printed in this color.");
        return requested;
    }
    private static RuleCardType ToRuleType(CardDefinitionType type) => type switch
    {
        CardDefinitionType.Attack => RuleCardType.Attack,
        CardDefinitionType.Prayer => RuleCardType.Prayer,
        CardDefinitionType.Block => RuleCardType.Block,
        CardDefinitionType.Relic => RuleCardType.Relic,
        _ => RuleCardType.None,
    };
}

public static class CardZoneOperations
{
    public static int Count(World world, EntityId deckEntity, CardZone zone)
    {
        ref Deck deck = ref world.Get<Deck>(deckEntity);
        return zone switch
        {
            CardZone.MasterDeck => world.GetDynamicBuffer<MasterDeckCard>(deck.Cards).Count,
            CardZone.DrawPile => world.GetDynamicBuffer<DrawPileCard>(deck.DrawPile).Count,
            CardZone.Hand => world.GetDynamicBuffer<HandCard>(deck.Hand).Count,
            CardZone.DiscardPile => world.GetDynamicBuffer<DiscardPileCard>(deck.DiscardPile).Count,
            CardZone.ExhaustPile => world.GetDynamicBuffer<ExhaustPileCard>(deck.ExhaustPile).Count,
            _ => 0,
        };
    }

    public static EntityId At(World world, EntityId deckEntity, CardZone zone, int index)
    {
        ref Deck deck = ref world.Get<Deck>(deckEntity);
        return zone switch
        {
            CardZone.MasterDeck => world.GetDynamicBuffer<MasterDeckCard>(deck.Cards)[index].Card,
            CardZone.DrawPile => world.GetDynamicBuffer<DrawPileCard>(deck.DrawPile)[index].Card,
            CardZone.Hand => world.GetDynamicBuffer<HandCard>(deck.Hand)[index].Card,
            CardZone.DiscardPile => world.GetDynamicBuffer<DiscardPileCard>(deck.DiscardPile)[index].Card,
            CardZone.ExhaustPile => world.GetDynamicBuffer<ExhaustPileCard>(deck.ExhaustPile)[index].Card,
            _ => EntityId.Null,
        };
    }

    public static bool Move(
        World world,
        EntityId deckEntity,
        EntityId card,
        CardZone source,
        CardZone destination,
        int destinationIndex,
        CardMoveReason reason,
        CardGameplayEventHub? events = null)
    {
        if (!world.IsAlive(card) || !world.TryGet(card, out CardZoneLocation location) || location.Deck != deckEntity)
            return false;
        CardZone actualSource = source == CardZone.None ? location.Zone : source;
        int sourceIndex = IndexOf(world, deckEntity, actualSource, card);
        if (sourceIndex < 0 || !RemoveAt(world, deckEntity, actualSource, sourceIndex))
            return false;
        int insertedIndex = Insert(world, deckEntity, destination, card, destinationIndex);
        ref CardZoneLocation mutable = ref world.Get<CardZoneLocation>(card);
        mutable.Zone = destination;
        mutable.Index = insertedIndex;
        RefreshIndices(world, deckEntity, actualSource);
        RefreshIndices(world, deckEntity, destination);
        events?.CardMoves.Publish(new CardMoved(card, deckEntity, actualSource, destination, sourceIndex, insertedIndex, reason));
        return true;
    }

    public static void AddToMasterDeck(World world, EntityId deckEntity, EntityId card)
    {
        ref Deck deck = ref world.Get<Deck>(deckEntity);
        DynamicBuffer<MasterDeckCard> cards = world.GetDynamicBuffer<MasterDeckCard>(deck.Cards);
        if (IndexOf(cards.AsReadOnlySpan(), card) < 0)
            cards.Add(new MasterDeckCard(card));
    }

    public static int Insert(World world, EntityId deckEntity, CardZone zone, EntityId card, int index = -1)
    {
        ref Deck deck = ref world.Get<Deck>(deckEntity);
        switch (zone)
        {
            case CardZone.DrawPile:
                return Insert(world.GetDynamicBuffer<DrawPileCard>(deck.DrawPile), new DrawPileCard(card), index);
            case CardZone.Hand:
                return Insert(world.GetDynamicBuffer<HandCard>(deck.Hand), new HandCard(card), index);
            case CardZone.DiscardPile:
                return Insert(world.GetDynamicBuffer<DiscardPileCard>(deck.DiscardPile), new DiscardPileCard(card), index);
            case CardZone.ExhaustPile:
                return Insert(world.GetDynamicBuffer<ExhaustPileCard>(deck.ExhaustPile), new ExhaustPileCard(card), index);
            case CardZone.Removed:
            case CardZone.None:
                return -1;
            default:
                throw new ArgumentOutOfRangeException(nameof(zone), zone, "MasterDeck membership is not an exclusive card zone.");
        }
    }

    public static int IndexOf(World world, EntityId deckEntity, CardZone zone, EntityId card)
    {
        ref Deck deck = ref world.Get<Deck>(deckEntity);
        return zone switch
        {
            CardZone.MasterDeck => IndexOf(world.GetDynamicBuffer<MasterDeckCard>(deck.Cards).AsReadOnlySpan(), card),
            CardZone.DrawPile => IndexOf(world.GetDynamicBuffer<DrawPileCard>(deck.DrawPile).AsReadOnlySpan(), card),
            CardZone.Hand => IndexOf(world.GetDynamicBuffer<HandCard>(deck.Hand).AsReadOnlySpan(), card),
            CardZone.DiscardPile => IndexOf(world.GetDynamicBuffer<DiscardPileCard>(deck.DiscardPile).AsReadOnlySpan(), card),
            CardZone.ExhaustPile => IndexOf(world.GetDynamicBuffer<ExhaustPileCard>(deck.ExhaustPile).AsReadOnlySpan(), card),
            _ => -1,
        };
    }

    public static void RefreshIndices(World world, EntityId deckEntity, CardZone zone)
    {
        int count = Count(world, deckEntity, zone);
        for (var index = 0; index < count; index++)
        {
            EntityId card = At(world, deckEntity, zone, index);
            if (!world.IsAlive(card) || !world.Has<CardZoneLocation>(card))
                continue;
            ref CardZoneLocation location = ref world.Get<CardZoneLocation>(card);
            if (location.Zone == zone)
                location.Index = index;
        }
    }

    public static void ShuffleDrawPile(World world, EntityId deckEntity)
    {
        ref Deck deck = ref world.Get<Deck>(deckEntity);
        DynamicBuffer<DrawPileCard> draw = world.GetDynamicBuffer<DrawPileCard>(deck.DrawPile);
        var random = new DeterministicRuleRandom(ref deck.Random);
        random.Shuffle(draw.AsSpan());
        RefreshIndices(world, deckEntity, CardZone.DrawPile);
    }

    private static bool RemoveAt(World world, EntityId deckEntity, CardZone zone, int index)
    {
        ref Deck deck = ref world.Get<Deck>(deckEntity);
        switch (zone)
        {
            case CardZone.DrawPile: world.GetDynamicBuffer<DrawPileCard>(deck.DrawPile).RemoveAt(index); return true;
            case CardZone.Hand: world.GetDynamicBuffer<HandCard>(deck.Hand).RemoveAt(index); return true;
            case CardZone.DiscardPile: world.GetDynamicBuffer<DiscardPileCard>(deck.DiscardPile).RemoveAt(index); return true;
            case CardZone.ExhaustPile: world.GetDynamicBuffer<ExhaustPileCard>(deck.ExhaustPile).RemoveAt(index); return true;
            default: return false;
        }
    }

    private static int Insert<T>(DynamicBuffer<T> buffer, in T value, int index) where T : unmanaged
    {
        int destination = index < 0 || index > buffer.Count ? buffer.Count : index;
        buffer.Insert(destination, in value);
        return destination;
    }

    private static int IndexOf(ReadOnlySpan<MasterDeckCard> values, EntityId card) { for (var i = 0; i < values.Length; i++) if (values[i].Card == card) return i; return -1; }
    private static int IndexOf(ReadOnlySpan<DrawPileCard> values, EntityId card) { for (var i = 0; i < values.Length; i++) if (values[i].Card == card) return i; return -1; }
    private static int IndexOf(ReadOnlySpan<HandCard> values, EntityId card) { for (var i = 0; i < values.Length; i++) if (values[i].Card == card) return i; return -1; }
    private static int IndexOf(ReadOnlySpan<DiscardPileCard> values, EntityId card) { for (var i = 0; i < values.Length; i++) if (values[i].Card == card) return i; return -1; }
    private static int IndexOf(ReadOnlySpan<ExhaustPileCard> values, EntityId card) { for (var i = 0; i < values.Length; i++) if (values[i].Card == card) return i; return -1; }
}
