#nullable enable

using System.Runtime.InteropServices;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using Crusaders30XX.ECS.DataOriented.Storage;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Effects;

public enum PassiveLifetime : byte
{
    Phase = 0,
    Battle = 1,
    Quest = 2,
    Climb = 3,
    Permanent = 4,
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct PassiveEntry
{
    public EffectId Effect;
    public EntityId Source;
    public int Stacks;
    public int BattleEpoch;
    public int PhaseEpoch;
    public int TimerMilliseconds;
    public PassiveLifetime Lifetime;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct AppliedPassives : IComponent
{
    public DynamicBufferHandle<PassiveEntry> Entries;
}

public enum EquipmentZoneKind : byte
{
    Inventory = 0,
    Equipped = 1,
    AssignedBlock = 2,
    Destroyed = 3,
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct EquipmentZone : IComponent
{
    public EntityId Owner;
    public EntityId AssignedAttack;
    public EquipmentZoneKind Kind;
    public int SlotIndex;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct EquippedEquipment : IComponent
{
    public EntityId Owner;
    public EquipmentId Definition;
    public EquipmentUsageState Usage;
    public RuleRandomState Random;
    public byte Active;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct EquippedMedal : IComponent
{
    public EntityId Owner;
    public MedalId Definition;
    public MedalRuntimeState State;
    public RuleRandomState Random;
    public byte Active;
}

public enum TemperanceAbilityId : byte
{
    None = 0,
    AngelicAura = 1,
    FlingFling = 2,
    IronResolve = 3,
    MeasuredBreath = 4,
    Radiance = 5,
    StaticSurge = 6,
    Unsheath = 7,
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct EquippedTemperanceAbility : IComponent
{
    public TemperanceAbilityId Definition;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct PassiveMeterComponent : IComponent
{
    public EffectId Effect;
    public int DisplayedStacks;
    public float Fill;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct EquipmentTooltipSource : IComponent
{
    public EntityId Equipment;
    public EquipmentId Definition;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct EquipmentTooltipState : IComponent
{
    public EntityId Source;
    public float Opacity;
    public byte Visible;
}

public struct TemperanceTooltipAnchor : ITag { }
public struct EquipmentDisplayRoot : ITag { }

public enum EffectTrackingLifetime : byte
{
    Phase = 0,
    Battle = 1,
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct EffectTrackingEntry(
    EntityId Source,
    TriggerId Trigger,
    int Epoch,
    EffectTrackingLifetime Lifetime);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct EffectTriggerTracking : IComponent
{
    public DynamicBufferHandle<EffectTrackingEntry> Entries;
}
