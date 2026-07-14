using Crusaders30XX.ECS.Objects.EnemyAttacks;

public static class EnemyAttackTextHelper
  {
    public static string GetText(EnemyAttackTextType type, int amount = 0, ConditionType conditionType = ConditionType.None, int percentage = 100, string customText = "")
    {
      var conditionText = GetConditionText(conditionType);
      var enemyAttackText = GetEnemyAttackText(type, amount, customText);
      var percentageText = percentage == 100 ? "" : $" ({percentage}% chance)";
      return $"{conditionText}{enemyAttackText}{percentageText}";
    }

    public static string GetBlockThresholdText(int damageDelta, string effectText)
    {
      return $"If this attack deals {damageDelta + 1} or more damage - {effectText}";
    }

    public static string GetDamageThresholdText(int minimumDamage, string effectText)
    {
      return $"If this attack deals {minimumDamage} or more damage - {effectText}";
    }

    public static string GetConditionText(ConditionType conditionType)
    {
        switch (conditionType)
        {
          case ConditionType.OnHit:
            return "On hit - ";
          case ConditionType.OnBlockedByAtLeast1Card:
            return "If not blocked by at least 1 card - ";
          case ConditionType.OnBlockedByAtLeast2Cards:
            return "If not blocked by at least 2 cards - ";
          case ConditionType.OnBlockedByAtLeast2DifferentColors:
            return "If not blocked by 2 different colored cards - ";
          default:
            return "";
        }
    }

    public static string GetEnemyAttackText(EnemyAttackTextType type, int amount = 0, string customText = "")
    {
      switch (type)
      {
        case EnemyAttackTextType.Intimidate:
          return $"Intimidates {amount} card{(amount > 1 ? "s" : "")} from your hand (can't block with intimidated cards for the rest of the turn).";
        case EnemyAttackTextType.MustBeBlockedByAtLeast:
          return $"This attack must be blocked with at least {amount} card{(amount > 1 ? "s" : "")}/equipment.";
        case EnemyAttackTextType.MustBeBlockedExactly:
          return $"This attack must be blocked with exactly {amount} card{(amount > 1 ? "s" : "")}/equipment.";
        case EnemyAttackTextType.Burn:
          return $"Gain {amount} burn.";
		case EnemyAttackTextType.Poison:
		  return $"Gain {amount} poison.";
        case EnemyAttackTextType.Scar:
          return $"Gain {amount} scar{(amount > 1 ? "s" : "")}.";
        case EnemyAttackTextType.Armor:
          return $"The enemy gains {amount} armor.";
        case EnemyAttackTextType.Guard:
          return $"The enemy gains {amount} guard.";
        case EnemyAttackTextType.Corrode:
          return $"Each card used to block this attack reduces the block value by {amount} for the rest of the quest.";
        case EnemyAttackTextType.Recoil:
          return $"On reveal - a random card in your hand gains Recoil {amount}. If you don't block with it this turn, take {amount} damage.";
        case EnemyAttackTextType.Slow:
          return $"Gain {amount} slow.";
        case EnemyAttackTextType.Fear:
          return $"Gain {amount} fear.";
        case EnemyAttackTextType.Aggression:
          return $"The enemy gains {amount} aggression.";
        case EnemyAttackTextType.GlassCannon:
          return $"If this is blocked with exactly {amount} cards, this attack deals no damage and the blocking cards are exhausted.";
        case EnemyAttackTextType.Wounded:
          return $"Gain {amount} wounded.";
        case EnemyAttackTextType.Frozen:
          return $"Freeze {amount} random cards from your hand/draw pile.";
        case EnemyAttackTextType.Brittle:
          return $"Apply brittle to {amount} random card{(amount > 1 ? "s" : "")} in your deck/discard pile.";
        case EnemyAttackTextType.Frostbite:
          return $"Gain {amount} frostbite.";
        case EnemyAttackTextType.Custom:
          return customText;
        default:
          return string.Empty;
      }
    }

    public static string GetBlockingRestrictionText(BlockingRestrictionType blockingRestrictionType)
    {
      switch (blockingRestrictionType)
      {
        case BlockingRestrictionType.OnlyRed:
          return "Only red cards can block this attack.";
        case BlockingRestrictionType.OnlyBlack:
          return "Only black cards can block this attack.";
        case BlockingRestrictionType.OnlyWhite:
          return "Only white cards can block this attack.";
        case BlockingRestrictionType.NotRed:
          return "Only non-red cards can block this attack.";
        case BlockingRestrictionType.NotBlack:
          return "Only non-black cards can block this attack.";
        case BlockingRestrictionType.NotWhite:
          return "Only non-white cards can block this attack.";
        default:
          return "";
      }
    }
  }


  public enum EnemyAttackTextType
  {
    GlassCannon,
    Slow,
    Fear,
    Intimidate,
    Wounded,
    Scar,
    Aggression,
    Stealth,
    Power,
    Poison,
    MustBeBlockedByAtLeast,
    MustBeBlockedExactly,
    Burn,
    Armor,
    Guard,
    Corrode,
    Recoil,
    Frozen,
    Brittle,
    Frostbite,
    Custom
  }
