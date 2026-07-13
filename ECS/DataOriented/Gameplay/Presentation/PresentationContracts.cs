#nullable enable

using System;
using System.Runtime.InteropServices;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Storage;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Presentation;

[Flags]
public enum PresentationFlags : byte
{
    None = 0,
    Visible = 1 << 0,
    Active = 1 << 1,
    Completed = 1 << 2,
    Looping = 1 << 3,
}

public enum PlayerHudRegionKind : byte { Health, ActionPoints, Courage, Temperance, Pledge }
public enum AudioRequestKind : byte { PlaySound, StopSound, ChangeMusic, StopMusic }
public enum ShaderRequestKind : byte { FullScreen, Sprite, RectangularShockwave, Shockwave, Poison }

[Flags]
public enum AudioRequestFlags : byte
{
    None = 0,
    Loop = 1 << 0,
}

public enum RumbleRequestKind : byte { PlaySegment, ClearGroup, ClearAll, SetEnabled }
public enum RumbleRequestGroup : byte { Default, UiHover, HotKeyHold, Gameplay, Achievement }

// The nine ECS-045 component-ledger replacements.
public struct PauseMenuOverlay : IComponent { public float Opacity; public PresentationFlags Flags; }
public struct PauseMenuSlider : IComponent { public float Minimum; public float Maximum; public float Value; public byte Selected; }
public struct PauseMenuToggle : IComponent { public byte IsOn; public byte Selected; }
public struct PlayerHudAnchor : IComponent { public Vector2 Position; public Vector2 Size; }
public struct PlayerHudFeedbackState : IComponent { public float Pulse; public float Delta; public int Sequence; }
public struct PlayerHudRegion : IComponent { public PlayerHudRegionKind Kind; public int Order; }
public struct POITitleTooltipSource : IComponent { public StringId Title; public StringId Detail; }

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct PointOfInterestAction(StringId Label, int Action, byte Enabled);

public struct PointOfInterest : IComponent
{
    public StringId Title;
    public TextureAssetId Texture;
    public DynamicBufferHandle<PointOfInterestAction> Actions;
    public PresentationFlags Flags;
}

public struct ActiveVisualEffect : IComponent
{
    public VisualEffectRecipeId Recipe;
    public EntityId Source;
    public EntityId Target;
    public float ElapsedSeconds;
    public float DurationSeconds;
    public int Sequence;
    public PresentationFlags Flags;
}

// ECS-045-owned hot presentation state and request contracts.
public struct ParallaxPresentationState : IComponent
{
    public Vector2 Anchor;
    public Vector2 Offset;
    public Vector2 LastWrittenPosition;
    public byte Initialized;
}

public struct JigglePulseState : IComponent
{
    public float ElapsedSeconds;
    public float DurationSeconds;
    public float Magnitude;
    public int Sequence;
}

public struct ShaderRequest : IComponent
{
    public ShaderRequestKind Kind;
    public VisualEffectRecipeId Recipe;
    public EntityId Target;
    public Vector2 Position;
    public Vector2 Size;
    public float Progress;
    public int Sequence;
}

public struct AudioRequest : IComponent
{
    public AudioRequestKind Kind;
    public SoundId Sound;
    public float Volume;
    public float Pitch;
    public int Sequence;
}

// ECS-045 event-ledger replacements. Payloads are compact and unmanaged.
public readonly record struct JigglePulseConfig(float DurationSeconds, float Magnitude);
public readonly record struct JigglePulseEvent(EntityId Entity, JigglePulseConfig Config, int Sequence);
public readonly record struct AudioSettingsChangedEvent(float MusicVolume, float SfxVolume);
public readonly record struct BattlePhaseAnimationCompleteEvent(EntityId Battle, int Sequence);
public readonly record struct ShowStartOfBattleAnimationEvent(EntityId Battle, int Sequence);
public readonly record struct ShowVictoryAnimationEvent(EntityId Battle, int Sequence);
public readonly record struct ShuffleDeckAnimationCompleted(EntityId Deck, int Sequence);
public readonly record struct VictoryAnimationCompleteEvent(EntityId Battle, int Sequence);
public readonly record struct CardBaseRenderCompletedEvent(EntityId Card, int Sequence);
public readonly record struct CardBaseRenderStartedEvent(EntityId Card, int Sequence);
public readonly record struct CardHighlightRenderEvent(EntityId Card, float Intensity);
public readonly record struct CardRenderEvent(EntityId Card, Vector2 Position);
public readonly record struct CardRenderScaledEvent(EntityId Card, Vector2 Position, Vector2 Scale);
public readonly record struct CardRenderScaledRotatedEvent(EntityId Card, Vector2 Position, Vector2 Scale, float Rotation);
public readonly record struct ChangeMusicTrack(
    SoundId Track,
    float FadeSeconds,
    float Volume = 0.5f,
    AudioRequestFlags Flags = AudioRequestFlags.Loop);
public readonly record struct EquipmentHighlightRenderEvent(EntityId Equipment, float Intensity);
public readonly record struct HighlightRenderEvent(EntityId Entity, float Intensity);
public readonly record struct PlaySfxEvent(
    SoundId Sound,
    float Volume,
    float Pitch,
    float Pan = 0f,
    AudioRequestFlags Flags = AudioRequestFlags.None);
public readonly record struct StopMusic(float FadeSeconds);
public readonly record struct StopSfxEvent(SoundId Sound);
public readonly record struct DebuffAnimationComplete(EntityId Target, int Sequence);
public readonly record struct ShowTransition(int Transition, float DurationSeconds);
public readonly record struct StartDebuffAnimation(EntityId Target, VisualEffectRecipeId Recipe, int Sequence);
public readonly record struct TransitionCompleteEvent(int Transition, int Sequence);
public readonly record struct HideLocationNameEvent(int Sequence);
public readonly record struct PlunderRescueAnimationCompleted(EntityId Card, int Sequence);
public readonly record struct PlunderSnatchAnimationCompleted(EntityId Card, int Sequence);
public readonly record struct RumbleGroupCleared(int Group);
public readonly record struct RumbleSettingsChangedEvent(byte Enabled, float Strength);
public readonly record struct RectangularShockwaveEvent(Vector2 Position, Vector2 Size, float DurationSeconds, int Sequence);
public readonly record struct ShockwaveEvent(Vector2 Position, float Radius, float DurationSeconds, int Sequence);
public readonly record struct UpdateLocationNameEvent(StringId Name, float DurationSeconds);
public readonly record struct BattlePresentationCompleted(EntityId Battle, int Sequence);
public readonly record struct BattlePresentationStarted(EntityId Battle, int Sequence);
public readonly record struct BeginDefeatPresentationEvent(EntityId Enemy, int Sequence);
public readonly record struct PixelBurstAnimationCompleted(EntityId Entity, int Sequence);
public readonly record struct VisualEffectCompleted(EntityId Entity, VisualEffectRecipeId Recipe, int Sequence);
public readonly record struct VisualEffectImpactReached(EntityId Entity, VisualEffectRecipeId Recipe, int Sequence);

public readonly record struct VisualEffectRequest(
    EntityId Source,
    EntityId Target,
    VisualEffectRecipeId Recipe,
    float DurationSeconds,
    int Sequence);

public readonly record struct ShaderEffectRequest(
    ShaderRequestKind Kind,
    VisualEffectRecipeId Recipe,
    EntityId Target,
    Vector2 Position,
    Vector2 Size,
    float DurationSeconds,
    int Sequence);

public readonly record struct AudioPlaybackRequest(
    AudioRequestKind Kind,
    SoundId Sound,
    float Volume,
    float Pitch,
    int Sequence,
    float Pan = 0f,
    float FadeSeconds = 0f,
    AudioRequestFlags Flags = AudioRequestFlags.None);

/// <summary>Four-motor host value. This is compact data, never a hardware API object.</summary>
public readonly record struct RumbleMotorRequest(
    float LowFrequency,
    float HighFrequency,
    float LeftTrigger = 0f,
    float RightTrigger = 0f)
{
    public static RumbleMotorRequest Zero => default;
}

/// <summary>
/// One deterministic host instruction. Multi-beat patterns are represented by ordered delayed
/// segments so no managed pattern graph crosses the ECS/host boundary.
/// </summary>
public readonly record struct RumblePlaybackRequest(
    RumbleRequestKind Kind,
    RumbleRequestGroup Group,
    RumbleMotorRequest Start,
    RumbleMotorRequest End,
    float DurationSeconds,
    float DelaySeconds,
    float Strength,
    int Sequence,
    byte Enabled = 1);
