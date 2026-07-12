using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.VisualEffects;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public enum CardType
    {
        Attack,
        Prayer,
        Block,
        Relic
    }

    public enum Rarity
    {
        Starter,
        Common,
        Uncommon,
        Rare,
    }
    public class CardBase : IDisposable
    {
        public EntityManager EntityManager { get; set; }
        public Entity CardEntity { get; set; }

        public string CardId { get; set; }
        public string Name { get; set; } = "";
        public int Damage { get; set; } = 0;
        public int Block { get; set; } = 0;
        public List<string> Cost { get; set; } = [];

        private int _multiHitCount = 1;
        private float _firstHitDelaySeconds;
        private float _hitIntervalSeconds = 0.5f;

        public int MultiHitCount
        {
            get => _multiHitCount;
            set
            {
                _multiHitCount = Math.Max(1, value);
                _visualEffectSequence = null;
            }
        }

        public float FirstHitDelaySeconds
        {
            get => _firstHitDelaySeconds;
            set
            {
                _firstHitDelaySeconds = Math.Max(0f, value);
                _visualEffectSequence = null;
            }
        }

        public float HitIntervalSeconds
        {
            get => _hitIntervalSeconds;
            set
            {
                _hitIntervalSeconds = Math.Max(0f, value);
                _visualEffectSequence = null;
            }
        }
        
        public string Text { get; set; } = "";
        public bool IsUpgraded { get; set; } = false;

        public string DisplayName => IsUpgraded && !string.IsNullOrWhiteSpace(Name) ? $"{Name}+" : Name;
        public string GetDisplayText()
        {
            return Text ?? string.Empty;
        }

        public int GetDerivedDamage(EntityManager entityManager, Entity card)
        {
            return CardStatModifierService.GetCardDamage(entityManager, card, CardStatQueryMode.Resolution).TotalValue;
        }
        
        public VisualEffectRecipe VisualEffectRecipe { get; protected set; }
        private VisualEffectSequence _visualEffectSequence;
        public VisualEffectSequence VisualEffectSequence
        {
            get => _visualEffectSequence ??= VisualEffectSequenceAuthoring.ForCard(this);
            protected set => _visualEffectSequence = value;
        }
        public CardType Type { get; set; } = CardType.Attack;
        public string Target { get; set; } = "";
        public bool IsFreeAction { get; set; } = false;
        public bool ExhaustsOnEndTurn { get; set; } = false;
        public bool CanAddToLoadout { get; set; } = true;
        public bool IsToken { get; set; } = false;
        public bool IsWeapon { get; set; } = false;
        public bool CanDiscardForCost => !IsWeapon && !IsToken;
        public bool IsStarter { get; set; } = false;
        public Rarity Rarity { get; set; } = Rarity.Common;
        private string _tooltip = "";
        public string Tooltip 
        {
            get => _tooltip;
            set
            {
                _tooltip = TooltipTextService.GetKeywordTooltip(value);
            }
        }
        public string CardTooltip { get; set; } = "";
        public string SpecialAction { get; set; } = "";
        public string OriginalText { get; set; } = "";


#nullable enable annotations
        public Action<EntityManager, Entity>? OnPlay { get; protected set; }
        public Action<EntityManager, Entity>? OnBlock { get; protected set; }
        public Action<EntityManager, Entity>? OnDraw {get; protected set; }
        public Action<EntityManager, Entity>? OnCreate {get; protected set; }
        public Action<EntityManager, Entity>? OnDiscardedForCost {get; protected set; }
        public Action<EntityManager, Entity>? OnPledged { get; protected set; }
        public Action<EntityManager, Entity>? OnUpgrade { get; protected set; }
        public Func<EntityManager, Entity, bool>? CanPlay { get; protected set; } = (a, b) => true;
        public Action<EntityManager, Entity>? OnCantPlay { get; protected set; }
        public Func<EntityManager, Entity, int>? GetConditionalDamage { get; protected set; } = (a, b) => 0;

        protected static VisualEffectRecipe PlayerAttackEffect()
        {
            return VisualEffectPresets.PlayerAttack();
        }

        protected static VisualEffectRecipe PlayerBuffEffect()
        {
            return VisualEffectPresets.PlayerBuff();
        }

        protected static VisualEffectRecipe LightSlashEffect()
        {
            return VisualEffectPresets.LightSlash();
        }

        protected static VisualEffectRecipe HeavyHammerEffect()
        {
            return VisualEffectPresets.HeavyHammer();
        }

        protected static VisualEffectRecipe HolyStrikeEffect()
        {
            return VisualEffectPresets.HolyStrike();
        }

        protected static VisualEffectRecipe HolySupportEffect()
        {
            return VisualEffectPresets.HolySupport();
        }

        protected static VisualEffectRecipe DefensiveGuardEffect()
        {
            return VisualEffectPresets.DefensiveGuard();
        }

        public virtual void Initialize(EntityManager entityManager, Entity cardEntity)
        {
            EntityManager = entityManager;
            CardEntity = cardEntity;
            OnCreate?.Invoke(entityManager, cardEntity);
            if (IsUpgraded)
                OnUpgrade?.Invoke(entityManager, cardEntity);
        }

        public virtual void Dispose()
        {
            Console.WriteLine($"[CardBase] Dispose: {CardId}");
        }

        public string GetCardHint(CardData.CardColor color)
        {
            StringBuilder sb = new StringBuilder();
            if (IsWeapon)
            {
                sb.Append("Weapons are a special type of card that can play once each action phase. They cannot be used to block enemy attacks, do not count towards your maximum hand size, and cannot be discarded to pay the costs of other cards in your hand.\n\n");
                return sb.ToString();
            }
            sb.Append("Cards have different uses! They can be used to play for their effect on your action phase, can be used to block enemy attacks, or be discarded to pay the costs of other cards in your hand.\n\n");
            switch (color)
            {
                case CardData.CardColor.White:
                    sb.Append("When a white card is used to block an enemy attack, you gain 1 temperance.\n\n");
                    break;
                case CardData.CardColor.Red:
                    sb.Append("When a red card is used to block an enemy attack, you gain 1 courage.\n\n");
                    break;
                case CardData.CardColor.Black:
                    sb.Append("Black cards have +1 block value from their red and white counterparts.\n\n");
                    break;
            }
            if (IsFreeAction)
            {
                sb.Append("Free action cards do not consume an action point.\n\n");
            }
            else
            {
                sb.Append("This card requires one action point to play.\n\n");
            }
            if (Cost.Count > 0)
            {
                sb.Append("This card requires discarding the following costs to play: ");
                foreach (string cost in Cost)
                {
                    sb.Append($"{cost.ToLower()} ");
                }
                sb.Append(".\n\n");
                if (Cost.Contains("Any"))
                {
                    sb.Append("(You can discard ANY color of card to satisfy an \"any\" cost.)\n\n");
                }
            }
            return sb.ToString();
        }
    }
}
