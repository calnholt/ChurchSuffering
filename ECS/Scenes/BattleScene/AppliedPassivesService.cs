using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;

namespace Crusaders30XX.ECS.Systems
{
    internal static class AppliedPassivesService
    {

      public static int GetGalvanizeBonus(int preGalvanizeDamage)
      {
        if (preGalvanizeDamage <= 0) return 0;
        return (int)Math.Ceiling(preGalvanizeDamage * TooltipTextService.GalvanizeBonusFraction);
      }

      public static int GetGuardAbsorption(Entity target, int rawAttackDamage)
      {
        if (rawAttackDamage <= 0 || target == null) return 0;
        var passives = target.GetComponent<AppliedPassives>()?.Passives;
        if (passives == null) return 0;
        if (!passives.TryGetValue(AppliedPassiveType.Guard, out int guardStacks) || guardStacks <= 0) return 0;
        return Math.Min(guardStacks, rawAttackDamage);
      }

      public static int GetPreviewAttackDamage(ModifyHpRequestEvent e, int rawDamage, bool ReadOnly = true)
      {
        e.Delta = -rawDamage;
        if (e.DamageType == ModifyTypeEnum.Attack && rawDamage > 0)
        {
          int guardAbsorbed = GetGuardAbsorption(e.Target, rawDamage);
          if (guardAbsorbed > 0)
          {
            e.Delta += guardAbsorbed;
            if (e.Delta >= 0) return 0;
          }
        }

        int passiveDelta = GetPassiveDelta(e, ReadOnly);
        int newDelta = e.Delta + passiveDelta;
        if (e.DamageType == ModifyTypeEnum.Attack && newDelta > 0) return 0;
        return Math.Max(0, -newDelta);
      }

      public static int GetPassiveDelta(ModifyHpRequestEvent e, bool ReadOnly = false)
      {
        if (e.DamageType == ModifyTypeEnum.Heal)
        {
          return 0;
        }
        var delta = 0;
        var isEnemy = e.Source?.HasComponent<Enemy>() == true;
        var targetPassives = e.Target?.GetComponent<AppliedPassives>()?.Passives
          ?? new Dictionary<AppliedPassiveType, int>();
        if (targetPassives.ContainsKey(AppliedPassiveType.Armor) && e.DamageType == ModifyTypeEnum.Attack)
        {
          targetPassives.TryGetValue(AppliedPassiveType.Armor, out var amount);
          delta -= amount;
        }
        if (targetPassives.ContainsKey(AppliedPassiveType.Wounded))
        {
          targetPassives.TryGetValue(AppliedPassiveType.Wounded, out var amount);
          delta += amount;
        }
        if (e.DamageType == ModifyTypeEnum.Attack && !isEnemy)
        {
          var outgoing = CardStatModifierService.GetOutgoingAttackDamage(new CardStatQuery
          {
            Kind = CardStatKind.OutgoingAttackDamage,
            Mode = ReadOnly ? CardStatQueryMode.Preview : CardStatQueryMode.Resolution,
            Source = e.Source,
            Owner = e.Source,
            Target = e.Target,
            Card = e.AttackCard,
            BaseValue = Math.Abs(e.Delta),
          });
          delta += outgoing.TotalDelta;
          if (!ReadOnly)
          {
            foreach (var consumption in outgoing.PassiveConsumptions)
            {
              EventManager.Publish(new RemovePassive
              {
                Owner = consumption.Owner,
                Type = consumption.Type,
              });
            }
          }
        }
        return -delta;
      }
    }
}
