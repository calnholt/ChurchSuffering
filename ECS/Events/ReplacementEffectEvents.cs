using System.Collections.Generic;
using Crusaders30XX.ECS.Core;

namespace Crusaders30XX.ECS.Events
{
    public enum ReplaceableEffectKind
    {
        FrostbiteThresholdDamage
    }

    public enum ReplacementEffectActionType
    {
        ModifyHp
    }

    public interface IReplacementEffectProvider
    {
        bool TryReplace(ReplaceableEffectRequest request);
    }

    public class ReplaceableEffectRequest
    {
        public ReplaceableEffectKind Kind { get; set; }
        public Entity OriginalSource { get; set; }
        public Entity OriginalTarget { get; set; }
        public int OriginalDelta { get; set; }
        public ModifyTypeEnum DamageType { get; set; } = ModifyTypeEnum.Effect;
        public string PassiveType { get; set; } = string.Empty;
        public bool IsHandled { get; set; }
        public Entity HandlingMedalEntity { get; set; }
        public string HandlingMedalId { get; set; } = string.Empty;
        public List<ReplacementEffectAction> Actions { get; } = new();
    }

    public class ReplacementEffectAction
    {
        public ReplacementEffectActionType Type { get; set; }
        public Entity Source { get; set; }
        public Entity Target { get; set; }
        public int Delta { get; set; }
        public ModifyTypeEnum DamageType { get; set; } = ModifyTypeEnum.Effect;
    }
}
