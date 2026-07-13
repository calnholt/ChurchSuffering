#nullable enable

using Crusaders30XX.ECS.DataOriented.Resources;

namespace Crusaders30XX.ECS.DataOriented.Definitions;

/// <summary>Stable stat semantics used by definition tables, facts, and rule commands.</summary>
public static class RuleStatIds
{
    public static readonly StatId Courage = new(1);
    public static readonly StatId Temperance = new(2);
    public static readonly StatId ActionPoints = new(3);
    public static readonly StatId Threat = new(4);
    public static readonly StatId Intellect = new(5);
    public static readonly StatId MaxHandSize = new(6);
    public static readonly StatId CurrentHealth = new(7);
    public static readonly StatId MaxHealth = new(8);
    public static readonly StatId UnscarredMaxHealth = new(9);
    public static readonly StatId Block = new(10);
    public static readonly StatId CardBlock = new(11);
    public static readonly StatId CardCost = new(12);
    public static readonly StatId CardSeals = new(13);
    public static readonly StatId EffectiveAssignedBlock = new(14);
    public static readonly StatId AttackDamage = new(15);
    public static readonly StatId AttackAdditionalDamage = new(16);
    public static readonly StatId EnemyPhase = new(17);
    public static readonly StatId Gold = new(18);
    public static readonly StatId MetaProgress = new(19);
}

/// <summary>
/// Stable effect semantics. Values 1-41 preserve the declaration order of the legacy
/// passive enum; card-instance restrictions append after that frozen range.
/// </summary>
public static class RuleEffectIds
{
    public static readonly EffectId Burn = new(1);
    public static readonly EffectId Power = new(2);
    public static readonly EffectId DowseWithHolyWater = new(3);
    public static readonly EffectId Slow = new(4);
    public static readonly EffectId Aegis = new(5);
    public static readonly EffectId Stun = new(6);
    public static readonly EffectId Armor = new(7);
    public static readonly EffectId Wounded = new(8);
    public static readonly EffectId Webbing = new(9);
    public static readonly EffectId Inferno = new(10);
    public static readonly EffectId Aggression = new(11);
    public static readonly EffectId Stealth = new(12);
    public static readonly EffectId Poison = new(13);
    public static readonly EffectId Shield = new(14);
    public static readonly EffectId Guard = new(15);
    public static readonly EffectId Fear = new(16);
    public static readonly EffectId Siphon = new(17);
    public static readonly EffectId Thorns = new(18);
    public static readonly EffectId Bleed = new(19);
    public static readonly EffectId Rage = new(20);
    public static readonly EffectId Intellect = new(21);
    public static readonly EffectId Intimidated = new(22);
    public static readonly EffectId MindFog = new(23);
    public static readonly EffectId Scar = new(24);
    public static readonly EffectId Channel = new(25);
    public static readonly EffectId Frostbite = new(26);
    public static readonly EffectId Frozen = new(27);
    public static readonly EffectId Windchill = new(28);
    public static readonly EffectId SubZero = new(29);
    public static readonly EffectId Enflamed = new(30);
    public static readonly EffectId Shackled = new(31);
    public static readonly EffectId Anathema = new(32);
    public static readonly EffectId Silenced = new(33);
    public static readonly EffectId Sealed = new(34);
    public static readonly EffectId Plunder = new(35);
    public static readonly EffectId Sharpen = new(36);
    public static readonly EffectId Might = new(37);
    public static readonly EffectId Vigor = new(38);
    public static readonly EffectId CarpeDiem = new(39);
    public static readonly EffectId Galvanize = new(40);
    public static readonly EffectId SwordIntoShield = new(41);
    public static readonly EffectId CardFrozen = new(42);
    public static readonly EffectId Brittle = new(43);
    public static readonly EffectId Scorched = new(44);
    public static readonly EffectId Thorned = new(45);
    public static readonly EffectId Colorless = new(46);
    public static readonly EffectId Cursed = new(47);
    public static readonly EffectId Recoil = new(48);
    public static readonly EffectId MarkedForSpecificDiscard = new(49);
    public static readonly EffectId Pledged = new(50);
    public static readonly EffectId CannotBlockCurrentAttack = new(51);
}

public static class RuleConditionIds
{
    public static readonly ConditionId Always = ConditionId.Null;
    public static readonly ConditionId OnHit = new(1);
    public static readonly ConditionId NotBlockedByAtLeastOneCard = new(2);
    public static readonly ConditionId NotBlockedByAtLeastTwoCards = new(3);
    public static readonly ConditionId NotBlockedByAtLeastTwoColors = new(4);
    public static readonly ConditionId MustBlockWithAtLeastOneCard = new(5);
    public static readonly ConditionId MustBlockWithAtLeastTwoCards = new(6);
    public static readonly ConditionId MustBlockWithExactlyOneCard = new(7);
    public static readonly ConditionId MustBlockWithExactlyTwoCards = new(8);
    public static readonly ConditionId DamageThreshold = new(9);
    public static readonly ConditionId HasPledge = new(10);
    public static readonly ConditionId IsUpgraded = new(11);
    public static readonly ConditionId StatThreshold = new(12);
    public static readonly ConditionId RandomChance = new(13);
    public static readonly ConditionId FinalBattle = new(14);
    public static readonly ConditionId CardColorMatches = new(15);
    public static readonly ConditionId HasEffect = new(16);
    public static readonly ConditionId EnemyPhase = new(17);
}

/// <summary>Stable lifecycle and behavior stages passed to domain handler contexts.</summary>
public static class RuleTriggerIds
{
    public static readonly TriggerId CardValidate = new(1);
    public static readonly TriggerId CardResolvePlay = new(2);
    public static readonly TriggerId CardResolveBlock = new(3);
    public static readonly TriggerId CardDiscardedForCost = new(4);
    public static readonly TriggerId CardPledged = new(5);
    public static readonly TriggerId CardConditionalDamage = new(6);
    public static readonly TriggerId CardReactive = new(7);
    public static readonly TriggerId CardLifecycle = new(8);
    public static readonly TriggerId BattleStart = new(9);
    public static readonly TriggerId BattleEnd = new(10);
    public static readonly TriggerId BlockPhaseStart = new(11);
    public static readonly TriggerId ActionPhaseStart = new(12);
    public static readonly TriggerId ActionPhaseEnd = new(13);
    public static readonly TriggerId EnemyTurnStart = new(14);
    public static readonly TriggerId EnemyTurnEnd = new(15);
    public static readonly TriggerId EnemyAttackChannelApplied = new(16);
    public static readonly TriggerId EnemyAttackReveal = new(17);
    public static readonly TriggerId EnemyAttackBlockAssigned = new(18);
    public static readonly TriggerId EnemyAttackBlockProcessed = new(19);
    public static readonly TriggerId EnemyAttackBlocksConfirmed = new(20);
    public static readonly TriggerId EnemyAttackHit = new(21);
    public static readonly TriggerId EnemyAttackDamageThresholdMet = new(22);
    public static readonly TriggerId EnemyAttackProgressOverride = new(23);
    public static readonly TriggerId EquipmentActivated = new(24);
    public static readonly TriggerId EquipmentBlocked = new(25);
    public static readonly TriggerId EquipmentReactive = new(26);
    public static readonly TriggerId MedalAcquired = new(27);
    public static readonly TriggerId MedalReactive = new(28);
    public static readonly TriggerId TemperanceTriggered = new(29);
    public static readonly TriggerId DefinitionLifecycle = new(30);
}

/// <summary>Stable scalar inputs supplied to rule handlers in ascending-ID fact spans.</summary>
public static class RuleFactIds
{
    public static readonly RuleFactId Phase = new(1);
    public static readonly RuleFactId Turn = new(2);
    public static readonly RuleFactId TutorialSection = new(3);
    public static readonly RuleFactId TutorialTurn = new(4);
    public static readonly RuleFactId Channel = new(5);
    public static readonly RuleFactId DamageDealt = new(6);
    public static readonly RuleFactId AssignedBlockTotal = new(7);
    public static readonly RuleFactId EffectiveBlockTotal = new(8);
    public static readonly RuleFactId AssignedBlockerCount = new(9);
    public static readonly RuleFactId AssignedBlockColorCount = new(10);
    public static readonly RuleFactId IsFinalBattle = new(11);
    public static readonly RuleFactId IsUpgraded = new(12);
    public static readonly RuleFactId IsPledged = new(13);
    public static readonly RuleFactId SourceColor = new(14);
    public static readonly RuleFactId SelectedColor = new(15);
    public static readonly RuleFactId HandCount = new(16);
    public static readonly RuleFactId DrawPileCount = new(17);
    public static readonly RuleFactId DiscardPileCount = new(18);
    public static readonly RuleFactId FrozenInHand = new(19);
    public static readonly RuleFactId SealedInHand = new(20);
    public static readonly RuleFactId CourageLostThisBattle = new(21);
    public static readonly RuleFactId DamageTakenThisTurn = new(22);
    public static readonly RuleFactId WeaponAttackedThisTurn = new(23);
    public static readonly RuleFactId PlanningCounter0 = new(24);
    public static readonly RuleFactId PlanningCounter1 = new(25);
    public static readonly RuleFactId PlanningFlags = new(26);
    public static readonly RuleFactId ResultValue = new(27);
    public static readonly RuleFactId PassiveStacks = new(28);
    public static readonly RuleFactId CandidateCount = new(29);
}

/// <summary>Frozen exceptional handler routes. Arbitrary numeric handler IDs are rejected.</summary>
public static class RuleHandlerIds
{
    public static readonly RuleHandlerId ResolveCardPlay = new(1);
    public static readonly RuleHandlerId ResolveCardBlock = new(2);
    public static readonly RuleHandlerId ResolveCardPledge = new(3);
    public static readonly RuleHandlerId ResolveCardReactive = new(4);
    public static readonly RuleHandlerId ResolveCardSelection = new(5);
    public static readonly RuleHandlerId ResolveEnemyPlanning = new(6);
    public static readonly RuleHandlerId ResolveEnemyAttack = new(7);
    public static readonly RuleHandlerId ResolveEnemyLifecycle = new(8);
    public static readonly RuleHandlerId ResolveEquipmentTrigger = new(9);
    public static readonly RuleHandlerId ResolveMedalTrigger = new(10);
    public static readonly RuleHandlerId ResolveTemperanceTrigger = new(11);
    public static readonly RuleHandlerId ResolveReplacementEffect = new(12);
    public static readonly RuleHandlerId ResolveDelayedRule = new(13);
    public static readonly RuleHandlerId ResolveEndTurnRequest = new(14);

    // Frozen solely for the pre-ECS-023 characterization fixture. New content must use a semantic route above.
    public static readonly RuleHandlerId LegacyCharacterization = new(99);

    public static bool IsKnown(RuleHandlerId id) => id.Value is >= 1 and <= 14 or 99;
}

public static class RuleMetaResourceIds
{
    public static readonly MetaResourceId Gold = new(1);
    public static readonly MetaResourceId CollectionProgress = new(2);
    public static readonly MetaResourceId UnlockProgress = new(3);
    public static readonly MetaResourceId RewardRerolls = new(4);
    public static readonly MetaResourceId RedClimbResource = new(5);
    public static readonly MetaResourceId WhiteClimbResource = new(6);
    public static readonly MetaResourceId BlackClimbResource = new(7);

    public static bool IsKnown(MetaResourceId id) => id.Value is >= 1 and <= 7;
}

/// <summary>Stable keys for the legacy modular visual-effect recipes used by content.</summary>
public static class RuleVisualEffectRecipeIds
{
    public static readonly VisualEffectRecipeId None = VisualEffectRecipeId.Null;
    public static readonly VisualEffectRecipeId PlayerAttack = new(1);
    public static readonly VisualEffectRecipeId PlayerBuff = new(2);
    public static readonly VisualEffectRecipeId LightSlash = new(3);
    public static readonly VisualEffectRecipeId HeavyHammer = new(4);
    public static readonly VisualEffectRecipeId HolyStrike = new(5);
    public static readonly VisualEffectRecipeId HolySupport = new(6);
    public static readonly VisualEffectRecipeId DefensiveGuard = new(7);
    public static readonly VisualEffectRecipeId EnemySlash = new(8);
    public static readonly VisualEffectRecipeId EnemyClawSlash = new(9);
    public static readonly VisualEffectRecipeId EnemyBite = new(10);
    public static readonly VisualEffectRecipeId EnemyRockBlast = new(11);
    public static readonly VisualEffectRecipeId BlockedAttack = new(12);
    public static readonly VisualEffectRecipeId ArrowVolley = new(13);
    public static readonly VisualEffectRecipeId FireImpact = new(14);
    public static readonly VisualEffectRecipeId FrostImpact = new(15);
    public static readonly VisualEffectRecipeId KunaiVolley = new(16);
    public static readonly VisualEffectRecipeId LifeDrain = new(17);
    public static readonly VisualEffectRecipeId PoisonImpact = new(18);
    public static readonly VisualEffectRecipeId ShadowHex = new(19);
    public static readonly VisualEffectRecipeId ShieldBreak = new(20);
    public static readonly VisualEffectRecipeId ShieldGain = new(21);
    public static readonly VisualEffectRecipeId Whirlwind = new(22);
}
