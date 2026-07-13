#nullable enable

using System;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Resources;

namespace Crusaders30XX.ECS.DataOriented.Content.Enemies;

[Flags]
public enum EnemyDefinitionFlags : byte
{
    None = 0,
    Boss = 1 << 0,
    TutorialOnly = 1 << 1,
}

public enum EnemyPlanningProfile : byte
{
    Fixed = 0,
    Weighted = 1,
    Alternating = 2,
    RandomPool = 3,
    Stateful = 4,
    Tutorial = 5,
    PhaseBoss = 6,
}

public sealed record EnemyPhaseDefinition(int Phase, EnemyAttackId[] Arsenal);

public sealed record EnemyDefinitionData(
    EnemyId Id,
    string Name,
    int BaseHealth,
    int StartingHealthBelowMax,
    EnemyDefinitionFlags Flags,
    EnemyPlanningProfile Planning,
    EnemyPhaseDefinition[] Phases)
{
    public bool IsBoss => (Flags & EnemyDefinitionFlags.Boss) != 0;
    public bool IsTutorialOnly => (Flags & EnemyDefinitionFlags.TutorialOnly) != 0;
}

public enum EnemyAttackCondition : byte
{
    None = 0,
    OnHit = 1,
    IfNotBlockedByAtLeastOneCard = 2,
    IfNotBlockedByAtLeastTwoCards = 3,
    IfNotBlockedByAtLeastTwoColors = 4,
    MustBlockWithAtLeastOneCard = 5,
    MustBlockWithAtLeastTwoCards = 6,
    MustBlockWithExactlyOneCard = 7,
    MustBlockWithExactlyTwoCards = 8,
    DamageThreshold = 9,
}

public enum EnemyAttackColorRestriction : byte
{
    None = 0,
    OnlyRed = 1,
    OnlyBlack = 2,
    OnlyWhite = 3,
    NotRed = 4,
    NotBlack = 5,
    NotWhite = 6,
}

[Flags]
public enum EnemyAttackFlags : ushort
{
    None = 0,
    IgnoresAegis = 1 << 0,
    DynamicDamage = 1 << 1,
    DynamicThreshold = 1 << 2,
    RevealHook = 1 << 3,
    HitHook = 1 << 4,
    ThresholdHook = 1 << 5,
    BlockerHook = 1 << 6,
    ConfirmHook = 1 << 7,
    ChannelHook = 1 << 8,
    ProgressHook = 1 << 9,
}

public sealed record EnemyAttackDefinitionData(
    EnemyAttackId Id,
    string Name,
    int MinimumDamage,
    int MaximumDamage,
    int AdditionalDamage,
    EnemyAttackCondition Condition,
    EnemyAttackColorRestriction ColorRestriction,
    int AmbushPercentage,
    int MinimumBlockThreshold,
    int MaximumBlockThreshold,
    int GuardConversionPercent,
    EnemyAttackFlags Flags,
    VisualEffectRecipeId VisualEffect,
    string RulesText);
