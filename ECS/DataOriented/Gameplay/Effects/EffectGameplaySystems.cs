#nullable enable

using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Effects;

public static class EffectGameplaySystemIds
{
    public static readonly SystemId AppliedPassives = new(4301);
    public static readonly SystemId Bleed = new(4302);
    public static readonly SystemId Brittle = new(4303);
    public static readonly SystemId EquipmentBlockInteraction = new(4304);
    public static readonly SystemId EquipmentManager = new(4305);
    public static readonly SystemId Intimidate = new(4306);
    public static readonly SystemId MedalManager = new(4307);
    public static readonly SystemId Poison = new(4308);
    public static readonly SystemId ReplacementEffect = new(4309);
    public static readonly SystemId Scorched = new(4310);
    public static readonly SystemId TemperanceManager = new(4311);
    public static readonly SystemId Vigor = new(4312);
}

/// <summary>Migration-ledger compatibility identity. These rows are never returned by EffectGameplayComposition.</summary>
public interface IUnscheduledEffectLedgerSystem { }

public abstract class EffectGameplaySystemBase : IGameSystem, IUnscheduledEffectLedgerSystem
{
    protected EffectGameplaySystemBase(
        SystemId id,
        string name,
        SystemPhase phase = SystemPhase.Rules,
        EventBarrier barrier = EventBarrier.AfterSystem,
        bool recordsStructuralCommands = false) =>
        Descriptor = new SystemDescriptor(
            id, name, phase, SceneGroup.Battle,
            recordsStructuralCommands: recordsStructuralCommands,
            eventBarrier: barrier);

    public SystemDescriptor Descriptor { get; }
    public virtual void Update(ref SystemContext context) { }
}

public sealed class AppliedPassivesManagementSystem() : EffectGameplaySystemBase(
    EffectGameplaySystemIds.AppliedPassives, nameof(AppliedPassivesManagementSystem), recordsStructuralCommands: true);
public sealed class BleedManagementSystem() : EffectGameplaySystemBase(
    EffectGameplaySystemIds.Bleed, nameof(BleedManagementSystem));
public sealed class BrittleManagementSystem() : EffectGameplaySystemBase(
    EffectGameplaySystemIds.Brittle, nameof(BrittleManagementSystem));
public sealed class EquipmentBlockInteractionSystem() : EffectGameplaySystemBase(
    EffectGameplaySystemIds.EquipmentBlockInteraction, nameof(EquipmentBlockInteractionSystem), SystemPhase.Interaction);
public sealed class EquipmentManagerSystem() : EffectGameplaySystemBase(
    EffectGameplaySystemIds.EquipmentManager, nameof(EquipmentManagerSystem));
public sealed class IntimidateManagementSystem() : EffectGameplaySystemBase(
    EffectGameplaySystemIds.Intimidate, nameof(IntimidateManagementSystem));
public sealed class MedalManagerSystem() : EffectGameplaySystemBase(
    EffectGameplaySystemIds.MedalManager, nameof(MedalManagerSystem));
public sealed class PoisonSystem() : EffectGameplaySystemBase(
    EffectGameplaySystemIds.Poison, nameof(PoisonSystem));
public sealed class ReplacementEffectSystem() : EffectGameplaySystemBase(
    EffectGameplaySystemIds.ReplacementEffect, nameof(ReplacementEffectSystem));
public sealed class ScorchedManagementSystem() : EffectGameplaySystemBase(
    EffectGameplaySystemIds.Scorched, nameof(ScorchedManagementSystem));
public sealed class TemperanceManagerSystem() : EffectGameplaySystemBase(
    EffectGameplaySystemIds.TemperanceManager, nameof(TemperanceManagerSystem));
public sealed class VigorManagementSystem() : EffectGameplaySystemBase(
    EffectGameplaySystemIds.Vigor, nameof(VigorManagementSystem));
