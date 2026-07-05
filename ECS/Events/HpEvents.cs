using System;
using Crusaders30XX.ECS.Core;

namespace Crusaders30XX.ECS.Events
{
    /// <summary>
    /// Adjust HP by Delta (negative to lose HP, positive to heal).
    /// If Target is null, applies to the first Player entity with an HP component.
    /// </summary>
    public class ModifyHpRequestEvent
    {
        public Entity Target { get; set; }
        public Entity Source { get; set; }
        public int Delta { get; set; }
        public ModifyTypeEnum DamageType { get; set; } = ModifyTypeEnum.Attack;
        public bool IgnoresAegis { get; set; } = false;
        /// <summary>Player card entity dealing attack damage, for weapon-only passives (Sharpen).</summary>
        public Entity AttackCard { get; set; }
    }
    public enum ModifyTypeEnum
    {
        Attack,
        Effect,
        Heal,
    }

    public class ModifyHpEvent
    {
        public Entity Target { get; set; }
        public Entity Source { get; set; }
        public int Delta { get; set; }
        public ModifyTypeEnum DamageType { get; set; } = ModifyTypeEnum.Attack;
        public Guid PresentationId { get; set; }
    }

    /// <summary>
    /// Set current HP to a specified value (clamped to [0, Max]).
    /// If Target is null, applies to the first Player entity with an HP component.
    /// </summary>
    public class SetHpEvent
    {
        public Entity Target { get; set; }
        public int Value { get; set; }
    }

    public class IncreaseMaxHpEvent
    {
        public Entity Target { get; set; }
        public int Delta { get; set; }
    }

    public class ApplyBattleMaxHpEvent
    {
        public Entity Target { get; set; }
        public int ScarPenalty { get; set; }
    }

    public class FullyHealEvent
    {
        public Entity Target { get; set; }
    }

    /// <summary>
    /// Emitted when the player entity's HP reaches 0 for the first time.
    /// </summary>
    public class PlayerDied
    {
        public Entity Player { get; set; }
    }

    public enum RunEndCause
    {
        Death,
        Abandon,
    }

    /// <summary>
    /// Starts the same game-over overlay sequence as PlayerDied (e.g. run abandon).
    /// </summary>
    public class RunEndSequenceRequested
    {
        public RunEndCause Cause { get; set; } = RunEndCause.Abandon;
    }
    public class HealEvent
    {
        public Entity Target { get; set; }
        public int Delta { get; set; }
    }
}
