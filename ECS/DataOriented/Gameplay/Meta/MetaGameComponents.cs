#nullable enable

using System.Runtime.InteropServices;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Storage;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Meta;

public enum ClimbSlotKind : byte { Encounter, Event, Shop, Church }
public enum ClimbDifficulty : byte { Easy, Normal, Hard }
public enum ClimbShopOfferKind : byte { Empty, Medal, Equipment, Upgrade, Replacement }
public enum ClimbEncounterRegion : byte { Desert, Tundra, Jungle, Volcano, Gothic }
public enum ClimbScheduledEventKind : byte { Hazard, Character }
public enum ClimbScheduledEventStatus : byte { Scheduled, Active, Pending, Resolved, Expired }
public enum MetaModalKind : byte { None, ClimbSettings, SaintsMedals, QuestReward, BoosterPack, CardList, Narrative }
public enum DialogueState : byte { Hidden, Playing, AwaitingChoice, Interrupted, Complete }
public enum TutorialState : byte { Inactive, Running, Skipped, Complete }
public enum AchievementId : byte
{
    Archangel, BoldInvestment, CardPlayer, FadedSpectrum, FirstVictory,
    FrozenButUnbroken, HexedHoard, JustGettingStarted, Kenosis, KunaiStorm,
    LivingOnTheEdge, MassRevival, MasterArtificer, QuestMaster, RedCardApprentice,
    Relentless, SkeletonSlayer, Slayer, Unshackled,
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ClimbSlotEntry(int Column, int Row, ClimbSlotKind Kind, StringId Content, int Price, uint Roll);
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ClimbTransitionKeyframe(float Time, float Offset);
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ClimbPreviewEntry(int Column, int Row, StringId Content);
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct QuestTooltipLine(StringId Text, byte Style);
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct TutorialStepEntry(StringId Instruction, int RequiredAction, byte Completed);
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct BoosterRewardEntry(CardId Card, EquipmentId Equipment, MedalId Medal, byte Kind);
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct DialogueLineEntry(StringId Speaker, StringId Text, int NextLine, byte IsChoice);
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct QuestRewardEntry(CardId Card, int Gold, byte Upgraded);
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ShownShopItemEntry(ClimbShopOfferKind Kind, ushort Definition);
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ClimbEncounterScheduleEntry(
    int SlotIndex, EnemyId Enemy, ClimbEncounterRegion Region, int GeneratedAtTime,
    int Duration, int TimeCost, int RewardRed, int RewardWhite, int RewardBlack, uint Roll);
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public record struct ClimbEventScheduleEntry(
    int Position, StringId Definition, ClimbScheduledEventKind Kind,
    int ScheduledAppearanceTime, int Duration, int TimeCost,
    int RewardRed, int RewardWhite, int RewardBlack,
    int ActivatedAtTime, ClimbScheduledEventStatus Status, uint Roll);

// The following fifty component/tag declarations map one-for-one to the ECS-044 component ledger.
public struct AchievementBackButton : ITag { }
public struct AchievementButton : ITag { }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct AchievementGridItem : IComponent { public AchievementId Achievement; public int Progress; public int Target; public byte Completed; public byte Seen; }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct AchievementSceneState : IComponent { public int SelectedIndex; public int PendingReveals; public float RevealProgress; }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct ClimbShopSlotAction : IComponent
{
    public EntityId Run;
    public EntityId TargetCard;
    public CardId Card;
    public EquipmentId Equipment;
    public MedalId Medal;
    public int SlotIndex;
    public int TargetOrder;
    public int RedCost;
    public int WhiteCost;
    public int BlackCost;
    public int TimeCost;
    public ClimbShopOfferKind Kind;
    public byte Purchased;
}
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct QuestTooltip : IComponent { public DynamicBufferHandle<QuestTooltipLine> Lines; public StringId Quest; }
public struct TutorialInteractionPermitted : ITag { }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct ClimbColumnPresentation : IComponent { public int Column; public float Offset; public float Opacity; }
public struct ClimbColumnTransitionInputSuppression : ITag { }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct ClimbColumnTransitionState : IComponent
{
    public DynamicBufferHandle<ClimbSlotEntry> Slots;
    public DynamicBufferHandle<ClimbTransitionKeyframe> Keyframes;
    public DynamicBufferHandle<ShownShopItemEntry> ShownShopItems;
    public DynamicBufferHandle<ClimbEncounterScheduleEntry> Encounters;
    public DynamicBufferHandle<ClimbEventScheduleEntry> Events;
    public uint Seed;
    public int CurrentColumn;
    public int SelectedSlot;
    public int Time;
    public int Red;
    public int White;
    public int Black;
    public float Progress;
}
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct ClimbEncounterSlotAction : IComponent { public EntityId Run; public int SlotIndex; public StringId Encounter; }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct ClimbEventSlotAction : IComponent { public EntityId Run; public int SlotIndex; public StringId Event; }
public struct ClimbHeaderElement : ITag { }
public struct ClimbLoadoutButton : ITag { }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct ClimbMedalTooltipAnchor : IComponent { public EntityId Medal; }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct ClimbMedalTooltipSource : IComponent { public MedalId Medal; public EntityId RuntimeEntity; }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct ClimbPreviewState : IComponent { public DynamicBufferHandle<ClimbPreviewEntry> Entries; public int HoveredSlot; public byte Active; }
public struct ClimbResourceBarElement : ITag { }
public struct ClimbSceneRoot : ITag { }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct ClimbShopTooltipSource : IComponent { public int SlotIndex; public int Price; public byte Sold; }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct ClimbSlotPresentation : IComponent { public int Column; public int Row; public float Highlight; public byte Enabled; }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct ClimbSlotRefreshTransitionState : IComponent { public DynamicBufferHandle<ClimbTransitionKeyframe> Keyframes; public float Progress; public int Sequence; }
public struct ClimbTimelineElement : ITag { }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct GuidedTutorial : IComponent { public DynamicBufferHandle<TutorialStepEntry> Steps; public int TutorialId; public int Section; public int CurrentStep; public TutorialState State; }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct StockHand : IComponent { public DynamicBufferHandle<CardId> Cards; public int RestoreSequence; }
public struct TutorialEnemy : ITag { }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct QuestScopedCardModificationCleanup : IComponent { public EntityId Card; public int QuestSequence; public byte Pending; }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct RunDeckCard : IComponent { public CardId Definition; public int Order; public byte Upgraded; }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct BoosterPackOpeningOverlayState : IComponent { public DynamicBufferHandle<BoosterRewardEntry> Rewards; public int RevealedCount; public MetaModalKind Modal; public byte Open; }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct DialogOverlayState : IComponent { public DynamicBufferHandle<DialogueLineEntry> Lines; public int CurrentLine; public DialogueState State; public MetaModalKind InterruptedBy; }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct PendingQuestDialog : IComponent { public StringId Sequence; public int QuestIndex; public byte Pending; }
public struct QuestArrowLeft : ITag { }
public struct QuestArrowRight : ITag { }
public struct QuestBackButton : ITag { }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct QuestRewardOverlayState : IComponent { public DynamicBufferHandle<QuestRewardEntry> Rewards; public int Selected; public MetaModalKind Modal; public byte Open; }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct QuestSelectState : IComponent { public StringId Quest; public int Index; public int Count; public byte Confirmed; }
public struct QuestStartArea : ITag { }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct WayStationArrivalContextState : IComponent { public StringId Location; public int Visit; public int ModalDepth; public byte DialoguePending; }
public struct WayStationClimbModalCloseButton : ITag { }
public struct WayStationClimbModalDepartButton : ITag { }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct WayStationClimbModalDifficultyChoice : IComponent { public ClimbDifficulty Difficulty; public byte Selected; }
public struct WayStationClimbModalPanel : ITag { }
public struct WayStationClimbModalRoot : ITag { }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct WayStationClimbModalWeaponChoice : IComponent { public CardId Weapon; public byte Selected; }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct WayStationDialoguePoiAction : IComponent { public StringId Sequence; public int PoiIndex; }
public struct WayStationSaintsMedalsModalCloseButton : ITag { }
public struct WayStationSaintsMedalsModalPanel : ITag { }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct WayStationSaintsMedalsModalRoot : IComponent { public int SelectedIndex; public MetaModalKind Modal; public byte Open; }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct WayStationSaintsMedalsModalTile : IComponent { public MedalId Medal; public int Index; public byte Unlocked; public byte Selected; }
[StructLayout(LayoutKind.Sequential, Pack = 4)] public struct ForSaleItem : IComponent { public StringId Item; public int Price; public byte Kind; public byte Sold; }
