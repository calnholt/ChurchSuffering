#nullable enable

using System;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Rules;
using Crusaders30XX.ECS.DataOriented.Storage;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Meta;

[Flags]
public enum GuidedTutorialSectionFlags : byte
{
    None = 0,
    TeachesRules = 1 << 0,
    ShowsDrawPile = 1 << 1,
}

public enum GuidedTutorialPhase : byte { Block, Action }

public enum GuidedTutorialAction : int
{
    AcknowledgeWin = 1,
    AcknowledgeLoss = 2,
    InspectEnemyAttack = 3,
    InspectActionPoints = 4,
    PlayFreeAction = 5,
    DiscardForReckoning = 6,
    AssignBlackBlock = 7,
    PlayWeapon = 8,
    AssignRedBlock = 9,
    InspectCourage = 10,
    AssignWhiteBlock = 11,
    InspectTemperance = 12,
    InspectIntent = 13,
    PledgeCard = 14,
}

public enum GuidedTutorialMessageId : byte
{
    TeachWin,
    TeachLoss,
    TeachEnemyAttack,
    TeachBlackBlock,
    TeachWeapon,
    TeachRedCourage,
    TeachCourageHud,
    TeachWhiteTemperance,
    TeachTemperanceHud,
    TeachIntentPips,
    TeachPledge,
    TeachActionPoints,
    TeachFreeActions,
    TeachReckoningDiscard,
}

public readonly record struct GuidedTutorialCardDefinition(
    CardId Card,
    RuleCardColor Color,
    byte Colorless);

public readonly record struct GuidedTutorialTurnDefinition(
    int CardOffset,
    int CardCount,
    int AttackOffset,
    int AttackCount);

public readonly record struct GuidedTutorialSectionDefinition(
    int Section,
    int EnemyHp,
    EnemyAttackId EnemyAttack,
    int PlayerHp,
    GuidedTutorialSectionFlags Flags,
    int TurnOffset,
    int TurnCount,
    StringId PendingDialogue);

public readonly record struct GuidedTutorialMessageDefinition(
    GuidedTutorialMessageId Id,
    StringId Instruction,
    GuidedTutorialAction RequiredAction,
    string Key,
    string Text,
    string TargetType,
    string TargetId,
    string BubbleOrientation,
    string Condition);

public readonly record struct GuidedTutorialMessageScheduleEntry(
    int Section,
    int Turn,
    GuidedTutorialPhase Phase,
    GuidedTutorialMessageId Message);

/// <summary>
/// Immutable exact-definition catalog for the eight characterized guided-tutorial sections.
/// Managed text stays here; authored ECS state contains only compact IDs and a dynamic step buffer.
/// </summary>
public static class GuidedTutorialCatalog
{
    private const int InstructionIdBase = 73001;

    private static readonly GuidedTutorialCardDefinition[] Cards =
    [
        C(CardId.Smite), C(CardId.Smite),
        C(CardId.Smite), C(CardId.LitanyOfWrath), C(CardId.Smite),
        C(CardId.Smite), C(CardId.LitanyOfWrath), C(CardId.Smite), C(CardId.Reckoning),
        C(CardId.Absolution), C(CardId.LitanyOfWrath), C(CardId.Smite), C(CardId.Reckoning),
        B(CardId.Smite), B(CardId.Smite), B(CardId.Smite), B(CardId.Smite),
        B(CardId.Stab), R(CardId.Smite), W(CardId.Smite), W(CardId.Smite),
        W(CardId.Smite), W(CardId.Smite), B(CardId.Smite), B(CardId.Smite),
        B(CardId.Courageous), B(CardId.Smite), B(CardId.Smite), B(CardId.Fervor),
        R(CardId.LitanyOfWrath), R(CardId.Absolution), B(CardId.Reckoning), R(CardId.Smite),
    ];

    private static readonly EnemyAttackId[] Attacks =
    [
        EnemyAttackId.TutorialHordeStrike3,
        EnemyAttackId.TutorialHordeStrike3,
        EnemyAttackId.TutorialHordeStrike3,
        EnemyAttackId.TutorialHordeStrike8,
        EnemyAttackId.TutorialHordeStrike8,
        EnemyAttackId.TutorialHordeStrike6,
        EnemyAttackId.TutorialHordeStrike6,
        EnemyAttackId.TutorialHordeStrike8,
        EnemyAttackId.TutorialHordeStrike6,
    ];

    private static readonly GuidedTutorialTurnDefinition[] Turns =
    [
        new(0, 2, 0, 1), new(2, 3, 1, 1), new(5, 4, 2, 1), new(9, 4, 3, 1),
        new(13, 4, 4, 1), new(17, 4, 5, 1), new(21, 4, 6, 1),
        new(25, 4, 7, 1), new(29, 4, 8, 1),
    ];

    private static readonly GuidedTutorialSectionDefinition[] Sections =
    [
        new(1, 3, EnemyAttackId.TutorialHordeStrike3, 1, GuidedTutorialSectionFlags.TeachesRules, 0, 1, default),
        new(2, 6, EnemyAttackId.TutorialHordeStrike3, 1, GuidedTutorialSectionFlags.TeachesRules, 1, 1, default),
        new(3, 8, EnemyAttackId.TutorialHordeStrike3, 1, GuidedTutorialSectionFlags.TeachesRules, 2, 1, new(73101)),
        new(4, 10, EnemyAttackId.TutorialHordeStrike8, 9, GuidedTutorialSectionFlags.None, 3, 1, new(73102)),
        new(5, 5, EnemyAttackId.TutorialHordeStrike8, 1, GuidedTutorialSectionFlags.TeachesRules, 4, 1, default),
        new(6, 8, EnemyAttackId.TutorialHordeStrike6, 1, GuidedTutorialSectionFlags.TeachesRules, 5, 1, default),
        new(7, 10, EnemyAttackId.TutorialHordeStrike6, 1, GuidedTutorialSectionFlags.TeachesRules, 6, 1, default),
        new(8, 12, EnemyAttackId.TutorialHordeStrike8, 1,
            GuidedTutorialSectionFlags.TeachesRules | GuidedTutorialSectionFlags.ShowsDrawPile,
            7, 2, new(73103)),
    ];

    private static readonly GuidedTutorialMessageDefinition[] Messages =
    [
        M(GuidedTutorialMessageId.TeachWin, GuidedTutorialAction.AcknowledgeWin,
            "teach_win", "Reduce the enemy's HP to zero to win the battle.", "entity_name", "Enemy", "bottom"),
        M(GuidedTutorialMessageId.TeachLoss, GuidedTutorialAction.AcknowledgeLoss,
            "teach_loss", "If your HP reaches zero, you lose.", "entity_name", "UI_PlayerHudHealth", "right"),
        M(GuidedTutorialMessageId.TeachEnemyAttack, GuidedTutorialAction.InspectEnemyAttack,
            "teach_enemy_attack", "Attacks show their DAMAGE value. Block incoming damage by assigning cards.",
            "ui_region", "enemy_attack_display", "top"),
        M(GuidedTutorialMessageId.TeachBlackBlock, GuidedTutorialAction.AssignBlackBlock,
            "teach_black_block", "Black cards receive one additional BLOCK.", "ui_region", "first_black_card", "top"),
        M(GuidedTutorialMessageId.TeachWeapon, GuidedTutorialAction.PlayWeapon,
            "teach_weapon", "Your weapon can be played once each turn.", "entity_name", "Weapon", "top"),
        M(GuidedTutorialMessageId.TeachRedCourage, GuidedTutorialAction.AssignRedBlock,
            "teach_red_courage", "Blocking with a red card grants 1 courage.", "ui_region", "first_red_card", "top"),
        M(GuidedTutorialMessageId.TeachCourageHud, GuidedTutorialAction.InspectCourage,
            "teach_courage_hud", "Your current courage is displayed here.", "entity_name", "UI_PlayerHudCourage", "right"),
        M(GuidedTutorialMessageId.TeachWhiteTemperance, GuidedTutorialAction.AssignWhiteBlock,
            "teach_white_temperance", "Blocking with a white card grants 1 temperance.", "ui_region", "first_white_card", "top"),
        M(GuidedTutorialMessageId.TeachTemperanceHud, GuidedTutorialAction.InspectTemperance,
            "teach_temperance_hud", "Your temperance meter is shown here. Hover over it to see your ability details.",
            "entity_name", "UI_PlayerHudTemperance", "right"),
        M(GuidedTutorialMessageId.TeachIntentPips, GuidedTutorialAction.InspectIntent,
            "teach_intent_pips", "Intent pips show the number of incoming enemy attacks for this turn, and the next turn.",
            "entity_name", "EnemyIntentPips", "bottom"),
        M(GuidedTutorialMessageId.TeachPledge, GuidedTutorialAction.PledgeCard,
            "teach_pledge", "Pledge one card to keep it for a later turn. Pledged cards cannot block or pay costs.",
            "entity_name", "UI_PlayerHudPledge", "right"),
        M(GuidedTutorialMessageId.TeachActionPoints, GuidedTutorialAction.InspectActionPoints,
            "teach_action_points", "You get one Action Point to spend during your Action phase. Cards that cost AP show their cost.",
            "entity_name", "UI_PlayerHudActionPoint", "right", "has_non_free_card"),
        M(GuidedTutorialMessageId.TeachFreeActions, GuidedTutorialAction.PlayFreeAction,
            "teach_free_actions", "FREE ACTIONS do not consume your action point.",
            "ui_region", "litany_of_wrath", "top", "has_litany_of_wrath_in_hand"),
        M(GuidedTutorialMessageId.TeachReckoningDiscard, GuidedTutorialAction.DiscardForReckoning,
            "teach_reckoning_discard", "This card requires you to DISCARD two other cards from your hand to play.",
            "ui_region", "reckoning", "top", "has_reckoning_in_hand"),
    ];

    private static readonly GuidedTutorialMessageScheduleEntry[] Schedule =
    [
        S(1, GuidedTutorialPhase.Block, GuidedTutorialMessageId.TeachWin),
        S(1, GuidedTutorialPhase.Block, GuidedTutorialMessageId.TeachLoss),
        S(1, GuidedTutorialPhase.Block, GuidedTutorialMessageId.TeachEnemyAttack),
        S(1, GuidedTutorialPhase.Action, GuidedTutorialMessageId.TeachActionPoints),
        S(2, GuidedTutorialPhase.Action, GuidedTutorialMessageId.TeachFreeActions),
        S(3, GuidedTutorialPhase.Action, GuidedTutorialMessageId.TeachReckoningDiscard),
        S(5, GuidedTutorialPhase.Block, GuidedTutorialMessageId.TeachBlackBlock),
        S(5, GuidedTutorialPhase.Action, GuidedTutorialMessageId.TeachWeapon),
        S(6, GuidedTutorialPhase.Block, GuidedTutorialMessageId.TeachRedCourage),
        S(6, GuidedTutorialPhase.Block, GuidedTutorialMessageId.TeachCourageHud),
        S(7, GuidedTutorialPhase.Block, GuidedTutorialMessageId.TeachWhiteTemperance),
        S(7, GuidedTutorialPhase.Block, GuidedTutorialMessageId.TeachTemperanceHud),
        S(8, GuidedTutorialPhase.Block, GuidedTutorialMessageId.TeachIntentPips),
        S(8, GuidedTutorialPhase.Action, GuidedTutorialMessageId.TeachPledge),
    ];

    public static ReadOnlySpan<GuidedTutorialSectionDefinition> RegisteredSections => Sections;
    public static ReadOnlySpan<GuidedTutorialMessageDefinition> RegisteredMessages => Messages;
    public static ReadOnlySpan<GuidedTutorialMessageScheduleEntry> RegisteredSchedule => Schedule;

    public static GuidedTutorialSectionDefinition GetSection(int section) =>
        Sections[Math.Clamp(section - 1, 0, Sections.Length - 1)];

    public static GuidedTutorialTurnDefinition GetTurn(int section, int turn)
    {
        GuidedTutorialSectionDefinition definition = GetSection(section);
        int local = Math.Clamp(turn - 1, 0, definition.TurnCount - 1);
        return Turns[definition.TurnOffset + local];
    }

    public static ReadOnlySpan<GuidedTutorialCardDefinition> GetCards(int section, int turn)
    {
        GuidedTutorialTurnDefinition definition = GetTurn(section, turn);
        return Cards.AsSpan(definition.CardOffset, definition.CardCount);
    }

    public static ReadOnlySpan<EnemyAttackId> GetAttacks(int section, int turn)
    {
        GuidedTutorialTurnDefinition definition = GetTurn(section, turn);
        return Attacks.AsSpan(definition.AttackOffset, definition.AttackCount);
    }

    public static int CopyMessages(
        int section,
        int turn,
        GuidedTutorialPhase phase,
        Span<GuidedTutorialMessageId> destination)
    {
        var count = 0;
        for (var index = 0; index < Schedule.Length; index++)
        {
            GuidedTutorialMessageScheduleEntry entry = Schedule[index];
            if (entry.Section != section || entry.Turn != turn || entry.Phase != phase) continue;
            if (count >= destination.Length)
                throw new ArgumentException("The tutorial message destination is too small.", nameof(destination));
            destination[count++] = entry.Message;
        }
        return count;
    }

    public static GuidedTutorialMessageDefinition GetMessage(GuidedTutorialMessageId id)
    {
        int index = (int)id;
        if ((uint)index >= (uint)Messages.Length) throw new ArgumentOutOfRangeException(nameof(id));
        return Messages[index];
    }

    public static bool TryGetMessage(StringId instruction, out GuidedTutorialMessageDefinition definition)
    {
        int index = instruction.Value - InstructionIdBase;
        if ((uint)index < (uint)Messages.Length)
        {
            definition = Messages[index];
            return true;
        }
        definition = default;
        return false;
    }

    public static EntityId Materialize(World world, int section)
    {
        ArgumentNullException.ThrowIfNull(world);
        GuidedTutorialSectionDefinition definition = GetSection(section);
        EntityId entity = world.Create(default);
        int stepCount = CountSectionMessages(definition.Section);
        DynamicBufferHandle<TutorialStepEntry> steps = world.CreateDynamicBuffer<TutorialStepEntry>(entity, stepCount);
        DynamicBuffer<TutorialStepEntry> buffer = world.GetDynamicBuffer(steps);
        for (var index = 0; index < Schedule.Length; index++)
        {
            GuidedTutorialMessageScheduleEntry scheduled = Schedule[index];
            if (scheduled.Section != definition.Section) continue;
            GuidedTutorialMessageDefinition message = GetMessage(scheduled.Message);
            buffer.Add(new(message.Instruction, (int)message.RequiredAction, 0));
        }
        var tutorial = new GuidedTutorial
        {
            Steps = steps,
            TutorialId = definition.Section,
            Section = definition.Section,
            CurrentStep = 0,
            State = TutorialState.Inactive,
        };
        world.Add(entity, in tutorial);
        return entity;
    }

    private static int CountSectionMessages(int section)
    {
        var count = 0;
        for (var index = 0; index < Schedule.Length; index++)
            if (Schedule[index].Section == section) count++;
        return count;
    }

    private static GuidedTutorialCardDefinition C(CardId id) => new(id, RuleCardColor.Black, 1);
    private static GuidedTutorialCardDefinition B(CardId id) => new(id, RuleCardColor.Black, 0);
    private static GuidedTutorialCardDefinition R(CardId id) => new(id, RuleCardColor.Red, 0);
    private static GuidedTutorialCardDefinition W(CardId id) => new(id, RuleCardColor.White, 0);

    private static GuidedTutorialMessageDefinition M(
        GuidedTutorialMessageId id,
        GuidedTutorialAction action,
        string key,
        string text,
        string targetType,
        string targetId,
        string orientation,
        string condition = "") =>
        new(id, new StringId(InstructionIdBase + (int)id), action, key, text, targetType, targetId, orientation, condition);

    private static GuidedTutorialMessageScheduleEntry S(
        int section,
        GuidedTutorialPhase phase,
        GuidedTutorialMessageId message) => new(section, 1, phase, message);
}
