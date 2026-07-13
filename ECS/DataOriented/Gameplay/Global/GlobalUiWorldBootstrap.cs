#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Global;

/// <summary>Creates the unique global entities before the scheduler starts.</summary>
public static class GlobalUiWorldBootstrap
{
    public static GlobalUiGlobals Create(World world, SceneGroup initialScene = SceneGroup.TitleMenu)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (initialScene == SceneGroup.Global)
        {
            throw new ArgumentOutOfRangeException(nameof(initialScene));
        }

        EntityId scene = CreateSceneGlobal(world, initialScene);
        EntityId input = CreateInputGlobal(world);
        EntityId interaction = CreateInteractionGlobal(world);
        EntityId highlight = CreateHighlightGlobal(world);
        return new GlobalUiGlobals(scene, input, interaction, highlight);
    }

    private static EntityId CreateSceneGlobal(World world, SceneGroup initialScene)
    {
        var bundle = new SpawnBundle(7, 160);
        bundle.Add(new SceneState { Current = initialScene });
        bundle.Add(new SceneTransitionState
        {
            From = initialScene,
            To = initialScene,
            Phase = SceneTransitionPhase.Idle,
        });
        bundle.Add(new ScenePreparationState
        {
            TargetScene = initialScene,
            Status = ScenePreparationStatus.Idle,
        });
        bundle.AddTag<SceneStateSingleton>();
        bundle.AddTag<ScenePreparationSingleton>();
        bundle.AddTag<DontDestroyOnLoad>();
        bundle.AddTag<DontDestroyOnReload>();
        EntityId entity = world.Create(in bundle);
        world.RegisterUnique<SceneStateSingleton>(entity);
        world.RegisterUnique<ScenePreparationSingleton>(entity);
        return entity;
    }

    private static EntityId CreateInputGlobal(World world)
    {
        var bundle = new SpawnBundle(3, 160);
        bundle.Add(new PlayerInputState
        {
            Flags = PlayerInputFlags.InputEnabled,
        });
        bundle.AddTag<PlayerInputSingleton>();
        bundle.AddTag<DontDestroyOnLoad>();
        EntityId entity = world.Create(in bundle);
        world.RegisterUnique<PlayerInputSingleton>(entity);
        return entity;
    }

    private static EntityId CreateInteractionGlobal(World world)
    {
        var bundle = new SpawnBundle(3, 64);
        bundle.Add(new UIInteractionSettings());
        bundle.AddTag<UIInteractionSettingsSingleton>();
        bundle.AddTag<DontDestroyOnLoad>();
        EntityId entity = world.Create(in bundle);
        world.RegisterUnique<UIInteractionSettingsSingleton>(entity);
        return entity;
    }

    private static EntityId CreateHighlightGlobal(World world)
    {
        var bundle = new SpawnBundle(3, 128);
        bundle.Add(new EquipmentHighlightSettings
        {
            GlowLayers = 12,
            GlowSpread = 0.012f,
            GlowSpreadSpeed = 2f,
            GlowSpreadAmplitude = 0.15f,
            MaxAlpha = 0.8f,
            GlowPulseSpeed = 2.4f,
            GlowEasingPower = 1f,
            GlowMinIntensity = 0.35f,
            GlowMaxIntensity = 1f,
            CornerRadius = 8,
            HighlightBorderThickness = 5,
            GlowColorR = 255,
            GlowColorG = 255,
            GlowColorB = 255,
        });
        bundle.AddTag<HighlightSettingsSingleton>();
        bundle.AddTag<DontDestroyOnLoad>();
        EntityId entity = world.Create(in bundle);
        world.RegisterUnique<HighlightSettingsSingleton>(entity);
        return entity;
    }
}

public readonly record struct GlobalUiGlobals(
    EntityId Scene,
    EntityId PlayerInput,
    EntityId InteractionSettings,
    EntityId HighlightSettings);
