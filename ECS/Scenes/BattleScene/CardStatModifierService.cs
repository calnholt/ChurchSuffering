using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Objects.Medals;

namespace Crusaders30XX.ECS.Systems
{
    public enum CardStatKind
    {
        Damage,
        Block,
        OutgoingAttackDamage,
    }

    public enum CardStatQueryMode
    {
        Preview,
        Resolution,
    }

    public sealed class CardStatQuery
    {
        public EntityManager EntityManager { get; set; }
        public CardStatKind Kind { get; set; }
        public CardStatQueryMode Mode { get; set; } = CardStatQueryMode.Preview;
        public Entity Owner { get; set; }
        public Entity Card { get; set; }
        public Entity Source { get; set; }
        public Entity Target { get; set; }
        public int BaseValue { get; set; }
        public List<Entity> PaymentCards { get; set; } = new();
    }

    public sealed class CardStatModifier
    {
        public int Delta { get; set; }
        public string Reason { get; set; } = "";
        public string SourceId { get; set; } = "";
        public string SourceType { get; set; } = "";
    }

    public sealed class CardStatPassiveConsumption
    {
        public Entity Owner { get; set; }
        public AppliedPassiveType Type { get; set; }
    }

    public sealed class CardStatResult
    {
        public int BaseValue { get; set; }
        public List<CardStatModifier> Modifiers { get; set; } = new();
        public List<CardStatPassiveConsumption> PassiveConsumptions { get; set; } = new();
        public int TotalValue => Math.Max(0, BaseValue + Modifiers.Sum(m => m.Delta));
        public int TotalDelta => Modifiers.Sum(m => m.Delta);
    }

    public interface ICardStatModifierProvider
    {
        IEnumerable<CardStatModifier> GetStatModifiers(CardStatQuery query);
    }

    internal static class CardStatModifierService
    {
        private const string BlackCardReason = "Black card";

        public static CardStatResult GetCardDamage(EntityManager entityManager, Entity card, CardStatQueryMode mode = CardStatQueryMode.Preview)
        {
            var data = card?.GetComponent<CardData>();
            var cardBase = data?.Card;
            var query = new CardStatQuery
            {
                EntityManager = entityManager ?? cardBase?.EntityManager,
                Kind = CardStatKind.Damage,
                Mode = mode,
                Owner = ResolvePlayer(entityManager ?? cardBase?.EntityManager),
                Card = card,
                Source = ResolvePlayer(entityManager ?? cardBase?.EntityManager),
                BaseValue = cardBase?.Damage ?? 0,
                PaymentCards = GetPaymentCards(card),
            };

            var result = BuildCardStatResult(query);
            if (cardBase?.GetConditionalDamage != null)
            {
                int conditional = cardBase.GetConditionalDamage(query.EntityManager, card);
                if (conditional != 0)
                {
                    result.Modifiers.Add(new CardStatModifier
                    {
                        Delta = conditional,
                        Reason = "Conditional",
                        SourceType = "Card",
                        SourceId = cardBase.CardId ?? string.Empty,
                    });
                }
            }

            return result;
        }

        public static CardStatResult GetCardBlock(EntityManager entityManager, Entity card, CardStatQueryMode mode = CardStatQueryMode.Preview)
        {
            var data = card?.GetComponent<CardData>();
            var cardBase = data?.Card;
            return BuildCardStatResult(new CardStatQuery
            {
                EntityManager = entityManager ?? cardBase?.EntityManager,
                Kind = CardStatKind.Block,
                Mode = mode,
                Owner = ResolvePlayer(entityManager ?? cardBase?.EntityManager),
                Card = card,
                Source = ResolvePlayer(entityManager ?? cardBase?.EntityManager),
                BaseValue = cardBase?.Block ?? 0,
                PaymentCards = GetPaymentCards(card),
            });
        }

        public static CardStatResult GetOutgoingAttackDamage(CardStatQuery query)
        {
            query.Kind = CardStatKind.OutgoingAttackDamage;
            return BuildCardStatResult(query);
        }

        private static CardStatResult BuildCardStatResult(CardStatQuery query)
        {
            var result = new CardStatResult { BaseValue = query.BaseValue };
            AddStoredCardModifiers(query, result);
            AddSourcePassiveModifiers(query, result);
            AddProviderModifiers(query, result);
            return result;
        }

        private static void AddStoredCardModifiers(CardStatQuery query, CardStatResult result)
        {
            if (query.Card == null) return;

            if (query.Kind == CardStatKind.Damage)
            {
                var modifiedDamage = query.Card.GetComponent<ModifiedDamage>();
                if (modifiedDamage?.Modifications == null) return;
                foreach (var mod in modifiedDamage.Modifications)
                {
                    result.Modifiers.Add(FromStoredModification(mod));
                }
                return;
            }

            if (query.Kind == CardStatKind.Block)
            {
                var modifiedBlock = query.Card.GetComponent<ModifiedBlock>();
                if (modifiedBlock?.Modifications == null) return;
                foreach (var mod in modifiedBlock.Modifications)
                {
                    if (query.Card.HasComponent<Colorless>() && mod.Reason == BlackCardReason) continue;
                    result.Modifiers.Add(FromStoredModification(mod));
                }
            }
        }

        private static CardStatModifier FromStoredModification(Modification mod) =>
            new()
            {
                Delta = mod.Delta,
                Reason = mod.Reason ?? string.Empty,
                SourceType = "Stored",
                SourceId = mod.Reason ?? string.Empty,
            };

        private static void AddSourcePassiveModifiers(CardStatQuery query, CardStatResult result)
        {
            if (query.Kind != CardStatKind.OutgoingAttackDamage) return;
            if (query.Source?.HasComponent<Enemy>() == true) return;

            var sourcePassives = query.Source?.GetComponent<AppliedPassives>()?.Passives
                ?? new Dictionary<AppliedPassiveType, int>();
            bool isWeaponAttack = query.Card?.GetComponent<CardData>()?.Card?.IsWeapon == true;
            bool isNonWeaponAttackCard = !isWeaponAttack
                && query.Card?.GetComponent<CardData>()?.Card?.Type == CardType.Attack;
            int flatSourceBonus = 0;

            AddPassiveFlatModifier(query, result, sourcePassives, AppliedPassiveType.Power, ref flatSourceBonus);
            AddPassiveFlatModifier(query, result, sourcePassives, AppliedPassiveType.Might, ref flatSourceBonus);

            if (isNonWeaponAttackCard
                && sourcePassives.TryGetValue(AppliedPassiveType.SwordIntoShield, out int swordIntoShield)
                && swordIntoShield > 0)
            {
                AddPassiveModifier(result, AppliedPassiveType.SwordIntoShield, swordIntoShield);
                flatSourceBonus += swordIntoShield;
            }

            if (!isWeaponAttack && sourcePassives.TryGetValue(AppliedPassiveType.Aggression, out int aggression) && aggression > 0)
            {
                AddPassiveModifier(result, AppliedPassiveType.Aggression, aggression);
                flatSourceBonus += aggression;
                AddResolutionConsumption(query, result, AppliedPassiveType.Aggression);
            }

            if (!isWeaponAttack && sourcePassives.TryGetValue(AppliedPassiveType.Galvanize, out int galvanize) && galvanize > 0)
            {
                int galvanizeBonus = AppliedPassivesService.GetGalvanizeBonus(query.BaseValue + flatSourceBonus);
                if (galvanizeBonus > 0)
                {
                    AddPassiveModifier(result, AppliedPassiveType.Galvanize, galvanizeBonus);
                }
                AddResolutionConsumption(query, result, AppliedPassiveType.Galvanize);
            }

            if (isWeaponAttack && sourcePassives.TryGetValue(AppliedPassiveType.Sharpen, out int sharpen) && sharpen > 0)
            {
                AddPassiveModifier(result, AppliedPassiveType.Sharpen, sharpen);
                AddResolutionConsumption(query, result, AppliedPassiveType.Sharpen);
            }
        }

        private static void AddPassiveFlatModifier(
            CardStatQuery query,
            CardStatResult result,
            Dictionary<AppliedPassiveType, int> sourcePassives,
            AppliedPassiveType type,
            ref int flatSourceBonus)
        {
            if (sourcePassives.TryGetValue(type, out int amount) && amount > 0)
            {
                AddPassiveModifier(result, type, amount);
                flatSourceBonus += amount;
            }
        }

        private static void AddPassiveModifier(CardStatResult result, AppliedPassiveType type, int amount)
        {
            result.Modifiers.Add(new CardStatModifier
            {
                Delta = amount,
                Reason = type.ToString(),
                SourceId = type.ToString(),
                SourceType = "Passive",
            });
        }

        private static void AddResolutionConsumption(CardStatQuery query, CardStatResult result, AppliedPassiveType type)
        {
            if (query.Mode != CardStatQueryMode.Resolution || query.Source == null) return;
            result.PassiveConsumptions.Add(new CardStatPassiveConsumption
            {
                Owner = query.Source,
                Type = type,
            });
        }

        private static void AddProviderModifiers(CardStatQuery query, CardStatResult result)
        {
            if (query.EntityManager == null || query.Owner == null) return;

            var medalEntities = query.EntityManager.GetEntitiesWithComponent<EquippedMedal>()
                .OrderBy(entity => entity.Id)
                .ToList();

            foreach (var medalEntity in medalEntities)
            {
                var equipped = medalEntity.GetComponent<EquippedMedal>();
                if (equipped?.EquippedOwner != query.Owner) continue;
                if (equipped.Medal is not ICardStatModifierProvider provider) continue;

                foreach (var modifier in provider.GetStatModifiers(query) ?? Array.Empty<CardStatModifier>())
                {
                    if (modifier == null || modifier.Delta == 0) continue;
                    result.Modifiers.Add(modifier);
                }
            }
        }

        private static Entity ResolvePlayer(EntityManager entityManager) =>
            entityManager?.GetEntitiesWithComponent<Player>().FirstOrDefault();

        private static List<Entity> GetPaymentCards(Entity card)
        {
            var context = card?.GetComponent<CardPlayStatContext>();
            return context?.PaymentCards != null
                ? new List<Entity>(context.PaymentCards)
                : new List<Entity>();
        }
    }
}
