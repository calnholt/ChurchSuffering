#nullable enable

using System;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Gameplay.Effects;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Storage;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Meta;

public readonly record struct AchievementDefinition(AchievementId Id, StringId Name, int Target, int Trigger);
public readonly record struct MetaObjectDefinition(StringId Name, byte Kind, int Target);

/// <summary>Generated-shape catalog replacing nineteen achievement objects and three event helpers.</summary>
public static class GeneratedMetaObjectCatalog
{
    private static readonly MetaObjectDefinition[] definitions =
    [
        new(new(7001), 0, 7), new(new(7002), 0, 100), new(new(7003), 0, 100),
        new(new(7004), 0, 20), new(new(7005), 0, 1), new(new(7006), 0, 1),
        new(new(7007), 0, 12), new(new(7008), 0, 1), new(new(7009), 0, 25),
        new(new(7010), 0, 25), new(new(7011), 0, 1), new(new(7012), 0, 10),
        new(new(7013), 0, 12), new(new(7014), 0, 10), new(new(7015), 0, 25),
        new(new(7016), 0, 10), new(new(7017), 0, 50), new(new(7018), 0, 100),
        new(new(7019), 0, 1),
        new(new(7020), 1, 0), // EventBase catalog/helper
        new(new(7021), 2, 1), // IceboundTithe
        new(new(7022), 2, 1), // PrunedVocation
    ];

    public static ReadOnlySpan<MetaObjectDefinition> Definitions => definitions;

    public static AchievementDefinition GetAchievement(AchievementId id)
    {
        int index = (int)id;
        if ((uint)index >= 19u) throw new ArgumentOutOfRangeException(nameof(id));
        MetaObjectDefinition definition = definitions[index];
        return new AchievementDefinition(id, definition.Name, definition.Target, index + 1);
    }
}

public static class DeterministicClimbGenerator
{
    public static int RequiredSlotCount(int columns, int rows)
    {
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));
        return checked(columns * rows);
    }

    public static void Generate(uint seed, int columns, int rows, Span<ClimbSlotEntry> destination)
    {
        int required = RequiredSlotCount(columns, rows);
        if (destination.Length < required) throw new ArgumentException("The climb slot destination is too small.", nameof(destination));
        uint state = seed == 0 ? 0x9E3779B9u : seed;
        for (var column = 0; column < columns; column++)
        for (var row = 0; row < rows; row++)
        {
            state = Next(state);
            ClimbSlotKind kind = row == rows - 1
                ? ClimbSlotKind.Encounter
                : (ClimbSlotKind)(state % 4u);
            int price = kind == ClimbSlotKind.Shop ? 15 + (int)(state % 4u) * 5 : 0;
            destination[column * rows + row] = new ClimbSlotEntry(
                column, row, kind, new StringId(8000 + (int)(state % 256u)), price, state);
        }
    }

    private static uint Next(uint value)
    {
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        return value;
    }
}

public sealed class MetaSaveDto
{
    public const int CurrentVersion = 1;
    public int Version { get; init; } = CurrentVersion;
    public uint ClimbSeed { get; init; }
    public int CurrentColumn { get; init; }
    public int ClimbTime { get; init; }
    public int RedResources { get; init; }
    public int WhiteResources { get; init; }
    public int BlackResources { get; init; }
    public int Gold { get; init; }
    public RunCardSaveDto[] Cards { get; init; } = [];
    public EquipmentSaveDto[] Equipment { get; init; } = [];
    public MedalSaveDto[] Medals { get; init; } = [];
    public AchievementSaveDto[] Achievements { get; init; } = [];
    public ShownShopItemSaveDto[] ShownShopItems { get; init; } = [];
    public ClimbShopOfferSaveDto[] ShopOffers { get; init; } = [];

    public static MetaSaveDto Fresh(uint seed = 1) => new()
    {
        ClimbSeed = seed,
        RedResources = 1,
        WhiteResources = 1,
        BlackResources = 1,
        Gold = 25,
        Cards =
        [
            new(CardId.Hammer, false, 0),
            new(CardId.Strike, false, 1),
            new(CardId.Mantlet, false, 2),
        ],
        Equipment = [new(EquipmentId.BulwarkPlate, true)],
        Medals = [],
        Achievements = [],
    };
}

public readonly record struct RunCardSaveDto(CardId Card, bool Upgraded, int Order);
public readonly record struct EquipmentSaveDto(EquipmentId Equipment, bool Active);
public readonly record struct MedalSaveDto(MedalId Medal, bool Active);
public readonly record struct AchievementSaveDto(AchievementId Achievement, int Progress, bool Seen);
public readonly record struct ShownShopItemSaveDto(ClimbShopOfferKind Kind, ushort Definition);
public readonly record struct ClimbShopOfferSaveDto(
    ClimbShopOfferKind Kind,
    CardId Card,
    EquipmentId Equipment,
    MedalId Medal,
    int SlotIndex,
    int TargetOrder,
    int RedCost,
    int WhiteCost,
    int BlackCost,
    int TimeCost,
    bool Purchased);
public readonly record struct MetaRuntimeEntities(EntityId Run, EntityId Climb, EntityId WayStation);

/// <summary>Fresh-version save boundary. It deliberately contains no legacy-version migration path.</summary>
public static class MetaSaveAdapter
{
    public static MetaRuntimeEntities Spawn(World world, MetaSaveDto save, int climbColumns = 5, int climbRows = 3)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(save);
        if (save.Version != MetaSaveDto.CurrentVersion)
            throw new NotSupportedException("Only fresh data-oriented save version 1 is accepted.");

        EntityId run = world.Create(default);
        EntityId climb = CreateClimb(world, save, climbColumns, climbRows);
        EntityId wayStation = CreateWayStation(world);

        for (var index = 0; index < save.Cards.Length; index++)
        {
            RunCardSaveDto value = save.Cards[index];
            var bundle = new SpawnBundle(1);
            var card = new RunDeckCard { Definition = value.Card, Upgraded = value.Upgraded ? (byte)1 : (byte)0, Order = value.Order };
            bundle.Add(in card);
            world.Create(in bundle);
        }

        for (var index = 0; index < save.Equipment.Length; index++)
        {
            EquipmentSaveDto value = save.Equipment[index];
            var bundle = new SpawnBundle(1);
            var equipment = new EquippedEquipment { Owner = run, Definition = value.Equipment, Active = value.Active ? (byte)1 : (byte)0 };
            bundle.Add(in equipment);
            world.Create(in bundle);
        }

        for (var index = 0; index < save.Medals.Length; index++)
        {
            MedalSaveDto value = save.Medals[index];
            var bundle = new SpawnBundle(1);
            var medal = new EquippedMedal { Owner = run, Definition = value.Medal, Active = value.Active ? (byte)1 : (byte)0 };
            bundle.Add(in medal);
            world.Create(in bundle);
        }

        for (var index = 0; index < 19; index++)
        {
            AchievementId id = (AchievementId)index;
            AchievementDefinition definition = GeneratedMetaObjectCatalog.GetAchievement(id);
            int progress = 0;
            byte seen = 0;
            for (var savedIndex = 0; savedIndex < save.Achievements.Length; savedIndex++)
            {
                AchievementSaveDto value = save.Achievements[savedIndex];
                if (value.Achievement != id) continue;
                progress = value.Progress;
                seen = value.Seen ? (byte)1 : (byte)0;
                break;
            }
            var bundle = new SpawnBundle(1);
            var achievement = new AchievementGridItem
            {
                Achievement = id,
                Progress = progress,
                Target = definition.Target,
                Completed = progress >= definition.Target ? (byte)1 : (byte)0,
                Seen = seen,
            };
            bundle.Add(in achievement);
            world.Create(in bundle);
        }

        if (save.ShopOffers.Length == 0) ClimbShopRuntime.Refresh(world, run, climb);
        else ClimbShopRuntime.Restore(world, climb, save.ShopOffers);

        return new MetaRuntimeEntities(run, climb, wayStation);
    }

    public static MetaSaveDto Extract(World world, uint climbSeed, int currentColumn, int gold)
    {
        ArgumentNullException.ThrowIfNull(world);
        RunCardSaveDto[] cards = ExtractCards(world);
        EquipmentSaveDto[] equipment = ExtractEquipment(world);
        MedalSaveDto[] medals = ExtractMedals(world);
        AchievementSaveDto[] achievements = ExtractAchievements(world);
        ClimbColumnTransitionState? climb = FindClimb(world);
        return new MetaSaveDto
        {
            ClimbSeed = climbSeed,
            CurrentColumn = currentColumn,
            ClimbTime = climb?.Time ?? 0,
            RedResources = climb?.Red ?? 0,
            WhiteResources = climb?.White ?? 0,
            BlackResources = climb?.Black ?? 0,
            Gold = gold,
            Cards = cards,
            Equipment = equipment,
            Medals = medals,
            Achievements = achievements,
            ShownShopItems = ExtractShownShopItems(world, climb),
            ShopOffers = ExtractShopOffers(world),
        };
    }

    public static MetaSaveDto Extract(World world, int gold = 25)
    {
        ClimbColumnTransitionState climb = FindClimb(world) ??
            throw new InvalidOperationException("No climb root is available for save extraction.");
        return Extract(world, climb.Seed, climb.CurrentColumn, gold);
    }

    private static EntityId CreateClimb(World world, MetaSaveDto save, int columns, int rows)
    {
        EntityId entity = world.Create(default);
        DynamicBufferHandle<ClimbSlotEntry> slotsHandle = world.CreateDynamicBuffer<ClimbSlotEntry>(entity, columns * rows);
        DynamicBufferHandle<ClimbTransitionKeyframe> keysHandle = world.CreateDynamicBuffer<ClimbTransitionKeyframe>(entity, 3);
        DynamicBufferHandle<ShownShopItemEntry> shownHandle = world.CreateDynamicBuffer<ShownShopItemEntry>(entity, Math.Max(8, save.ShownShopItems.Length));
        DynamicBufferHandle<ClimbEncounterScheduleEntry> encountersHandle =
            world.CreateDynamicBuffer<ClimbEncounterScheduleEntry>(entity, ClimbScheduleRuntime.EncounterCount);
        DynamicBufferHandle<ClimbEventScheduleEntry> eventsHandle =
            world.CreateDynamicBuffer<ClimbEventScheduleEntry>(entity, ClimbScheduleRuntime.EventCount);
        DynamicBuffer<ClimbSlotEntry> slots = world.GetDynamicBuffer(slotsHandle);
        int count = DeterministicClimbGenerator.RequiredSlotCount(columns, rows);
        for (var index = 0; index < count; index++) slots.Add(default);
        DeterministicClimbGenerator.Generate(save.ClimbSeed, columns, rows, slots.AsSpan());
        DynamicBuffer<ClimbTransitionKeyframe> keys = world.GetDynamicBuffer(keysHandle);
        keys.Add(new(0f, 0f));
        keys.Add(new(0.5f, -0.25f));
        keys.Add(new(1f, -1f));
        DynamicBuffer<ShownShopItemEntry> shown = world.GetDynamicBuffer(shownHandle);
        for (var index = 0; index < save.ShownShopItems.Length; index++)
            shown.Add(new(save.ShownShopItems[index].Kind, save.ShownShopItems[index].Definition));
        DynamicBuffer<ClimbEncounterScheduleEntry> encounters = world.GetDynamicBuffer(encountersHandle);
        DynamicBuffer<ClimbEventScheduleEntry> events = world.GetDynamicBuffer(eventsHandle);
        for (var index = 0; index < ClimbScheduleRuntime.EncounterCount; index++) encounters.Add(default);
        for (var index = 0; index < ClimbScheduleRuntime.EventCount; index++) events.Add(default);
        ClimbScheduleRuntime.Generate(save.ClimbSeed, save.ClimbTime, encounters.AsSpan(), events.AsSpan());
        var state = new ClimbColumnTransitionState
        {
            Slots = slotsHandle,
            Keyframes = keysHandle,
            ShownShopItems = shownHandle,
            Encounters = encountersHandle,
            Events = eventsHandle,
            Seed = save.ClimbSeed,
            CurrentColumn = save.CurrentColumn,
            SelectedSlot = -1,
            Time = Math.Clamp(save.ClimbTime, 0, ClimbShopRuntime.MaxTime),
            Red = Math.Max(0, save.RedResources),
            White = Math.Max(0, save.WhiteResources),
            Black = Math.Max(0, save.BlackResources),
        };
        world.Add(entity, in state);
        world.AddTag<ClimbSceneRoot>(entity);
        return entity;
    }

    private static EntityId CreateWayStation(World world)
    {
        var bundle = new SpawnBundle(1);
        var state = new WayStationArrivalContextState { Location = new StringId(9001), Visit = 1 };
        bundle.Add(in state);
        return world.Create(in bundle);
    }

    private static RunCardSaveDto[] ExtractCards(World world)
    {
        Query<RunDeckCard> query = world.Query<RunDeckCard>();
        var values = new RunCardSaveDto[Count(query)];
        var next = 0;
        foreach (QueryChunk<RunDeckCard> chunk in query)
        foreach (int row in chunk.Rows)
        {
            RunDeckCard card = chunk.Component1[row];
            values[next++] = new(card.Definition, card.Upgraded != 0, card.Order);
        }
        InsertionSort(values);
        return values;
    }

    private static EquipmentSaveDto[] ExtractEquipment(World world)
    {
        Query<EquippedEquipment> query = world.Query<EquippedEquipment>();
        var values = new EquipmentSaveDto[Count(query)];
        var next = 0;
        foreach (QueryChunk<EquippedEquipment> chunk in query)
        foreach (int row in chunk.Rows)
            values[next++] = new(chunk.Component1[row].Definition, chunk.Component1[row].Active != 0);
        return values;
    }

    private static MedalSaveDto[] ExtractMedals(World world)
    {
        Query<EquippedMedal> query = world.Query<EquippedMedal>();
        var values = new MedalSaveDto[Count(query)];
        var next = 0;
        foreach (QueryChunk<EquippedMedal> chunk in query)
        foreach (int row in chunk.Rows)
            values[next++] = new(chunk.Component1[row].Definition, chunk.Component1[row].Active != 0);
        return values;
    }

    private static AchievementSaveDto[] ExtractAchievements(World world)
    {
        Query<AchievementGridItem> query = world.Query<AchievementGridItem>();
        var values = new AchievementSaveDto[Count(query)];
        var next = 0;
        foreach (QueryChunk<AchievementGridItem> chunk in query)
        foreach (int row in chunk.Rows)
        {
            AchievementGridItem value = chunk.Component1[row];
            values[next++] = new(value.Achievement, value.Progress, value.Seen != 0);
        }
        return values;
    }

    private static ClimbColumnTransitionState? FindClimb(World world)
    {
        foreach (QueryChunk<ClimbColumnTransitionState> chunk in world.Query<ClimbColumnTransitionState>())
        foreach (int row in chunk.Rows)
            return chunk.Component1[row];
        return null;
    }

    private static ShownShopItemSaveDto[] ExtractShownShopItems(
        World world,
        ClimbColumnTransitionState? climb)
    {
        if (!climb.HasValue || climb.Value.ShownShopItems.IsNull) return [];
        DynamicBuffer<ShownShopItemEntry> shown = world.GetDynamicBuffer(climb.Value.ShownShopItems);
        var values = new ShownShopItemSaveDto[shown.Count];
        for (var index = 0; index < shown.Count; index++)
            values[index] = new(shown[index].Kind, shown[index].Definition);
        return values;
    }

    private static ClimbShopOfferSaveDto[] ExtractShopOffers(World world)
    {
        Query<ClimbShopSlotAction> query = world.Query<ClimbShopSlotAction>();
        var values = new ClimbShopOfferSaveDto[Count(query)];
        var next = 0;
        foreach (QueryChunk<ClimbShopSlotAction> chunk in query)
        foreach (int row in chunk.Rows)
        {
            ClimbShopSlotAction action = chunk.Component1[row];
            values[next++] = new(
                action.Kind, action.Card, action.Equipment, action.Medal, action.SlotIndex,
                action.TargetOrder, action.RedCost, action.WhiteCost, action.BlackCost,
                action.TimeCost, action.Purchased != 0);
        }
        Array.Sort(values, static (left, right) => left.SlotIndex.CompareTo(right.SlotIndex));
        return values;
    }

    private static int Count<T>(Query<T> query) where T : unmanaged, IComponent
    {
        var count = 0;
        foreach (QueryChunk<T> chunk in query) count += chunk.Count;
        return count;
    }

    private static void InsertionSort(Span<RunCardSaveDto> values)
    {
        for (var index = 1; index < values.Length; index++)
        {
            RunCardSaveDto value = values[index];
            var destination = index;
            while (destination > 0 && values[destination - 1].Order > value.Order)
            {
                values[destination] = values[destination - 1];
                destination--;
            }
            values[destination] = value;
        }
    }
}
