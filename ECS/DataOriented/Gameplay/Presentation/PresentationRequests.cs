#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Meta;
using Crusaders30XX.ECS.DataOriented.Resources;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Presentation;

/// <summary>Established-capacity output queues consumed by external GPU/audio adapters.</summary>
public sealed class PresentationRequestQueues
{
    private VisualEffectRequest[] visualEffects;
    private ShaderEffectRequest[] shaders;
    private AudioPlaybackRequest[] audio;
    private RumblePlaybackRequest[] rumble;
    private int visualEffectCount;
    private int shaderCount;
    private int audioCount;
    private int rumbleCount;

    public PresentationRequestQueues(int initialCapacity = 16)
    {
        if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        visualEffects = new VisualEffectRequest[initialCapacity];
        shaders = new ShaderEffectRequest[initialCapacity];
        audio = new AudioPlaybackRequest[initialCapacity];
        rumble = new RumblePlaybackRequest[initialCapacity];
    }

    public ReadOnlySpan<VisualEffectRequest> VisualEffects => visualEffects.AsSpan(0, visualEffectCount);
    public ReadOnlySpan<ShaderEffectRequest> Shaders => shaders.AsSpan(0, shaderCount);
    public ReadOnlySpan<AudioPlaybackRequest> Audio => audio.AsSpan(0, audioCount);
    public ReadOnlySpan<RumblePlaybackRequest> Rumble => rumble.AsSpan(0, rumbleCount);

    public void BeginFrame()
    {
        visualEffectCount = 0;
        shaderCount = 0;
        audioCount = 0;
        rumbleCount = 0;
    }

    public void Request(in VisualEffectRequest request)
    {
        Ensure(ref visualEffects, visualEffectCount + 1);
        visualEffects[visualEffectCount++] = request;
    }

    public void Request(in ShaderEffectRequest request)
    {
        Ensure(ref shaders, shaderCount + 1);
        shaders[shaderCount++] = request;
    }

    public void Request(in AudioPlaybackRequest request)
    {
        Ensure(ref audio, audioCount + 1);
        audio[audioCount++] = request;
    }

    public void Request(in RumblePlaybackRequest request)
    {
        Ensure(ref rumble, rumbleCount + 1);
        rumble[rumbleCount++] = request;
    }

    private static void Ensure<T>(ref T[] values, int required)
    {
        if (required <= values.Length) return;
        Array.Resize(ref values, Math.Max(required, Math.Max(4, values.Length * 2)));
    }
}

public sealed class PresentationRequestConsumer :
    IEventConsumer<PlaySfxEvent>,
    IEventConsumer<StopSfxEvent>,
    IEventConsumer<ChangeMusicTrack>,
    IEventConsumer<StopMusic>,
    IEventConsumer<StartDebuffAnimation>,
    IEventConsumer<ShockwaveEvent>,
    IEventConsumer<RectangularShockwaveEvent>,
    IEventConsumer<RumbleGroupCleared>,
    IEventConsumer<RumbleSettingsChangedEvent>,
    IEventConsumer<UIHoverChangedEvent>,
    IEventConsumer<RumbleRequested>,
    IEventConsumer<AchievementCompletedEvent>
{
    private readonly PresentationRequestQueues requests;
    private int sequence;

    public PresentationRequestConsumer(PresentationRequestQueues requests) =>
        this.requests = requests ?? throw new ArgumentNullException(nameof(requests));

    public void Consume(in PlaySfxEvent value, ref EventDispatchContext context)
    {
        if (value.Sound.IsNull) return;
        requests.Request(new AudioPlaybackRequest(
            AudioRequestKind.PlaySound,
            value.Sound,
            Math.Clamp(value.Volume, 0f, 1f),
            Math.Clamp(value.Pitch, -1f, 1f),
            ++sequence,
            Math.Clamp(value.Pan, -1f, 1f),
            Flags: value.Flags));
    }

    public void Consume(in StopSfxEvent value, ref EventDispatchContext context) =>
        requests.Request(new AudioPlaybackRequest(AudioRequestKind.StopSound, value.Sound, 0f, 0f, ++sequence));

    public void Consume(in ChangeMusicTrack value, ref EventDispatchContext context) =>
        requests.Request(new AudioPlaybackRequest(
            AudioRequestKind.ChangeMusic,
            value.Track,
            Math.Clamp(value.Volume, 0f, 1f),
            0f,
            ++sequence,
            FadeSeconds: Math.Max(0f, value.FadeSeconds),
            Flags: value.Flags));

    public void Consume(in StopMusic value, ref EventDispatchContext context) =>
        requests.Request(new AudioPlaybackRequest(
            AudioRequestKind.StopMusic,
            SoundId.Null,
            0f,
            0f,
            ++sequence,
            FadeSeconds: Math.Max(0f, value.FadeSeconds)));

    public void Consume(in StartDebuffAnimation value, ref EventDispatchContext context) =>
        requests.Request(new VisualEffectRequest(value.Target, value.Target, value.Recipe, 1f, value.Sequence));

    public void Consume(in ShockwaveEvent value, ref EventDispatchContext context) =>
        requests.Request(new ShaderEffectRequest(ShaderRequestKind.Shockwave, VisualEffectRecipeId.Null,
            EntityId.Null, value.Position, new Microsoft.Xna.Framework.Vector2(value.Radius), value.DurationSeconds, value.Sequence));

    public void Consume(in RectangularShockwaveEvent value, ref EventDispatchContext context) =>
        requests.Request(new ShaderEffectRequest(ShaderRequestKind.RectangularShockwave, VisualEffectRecipeId.Null,
            EntityId.Null, value.Position, value.Size, value.DurationSeconds, value.Sequence));

    public void Consume(in RumbleGroupCleared value, ref EventDispatchContext context) =>
        RequestRumble(RumbleRequestKind.ClearGroup, ToGroup(value.Group));

    public void Consume(in RumbleSettingsChangedEvent value, ref EventDispatchContext context) =>
        requests.Request(new RumblePlaybackRequest(
            RumbleRequestKind.SetEnabled,
            RumbleRequestGroup.Default,
            default,
            default,
            0f,
            0f,
            Math.Clamp(value.Strength, 0f, 1f),
            ++sequence,
            value.Enabled));

    public void Consume(in UIHoverChangedEvent value, ref EventDispatchContext context)
    {
        if (value.Source != Components.PlayerInputDevice.Gamepad || value.Current.IsNull) return;
        RequestSegment(
            RumbleRequestGroup.UiHover,
            new RumbleMotorRequest(0.30f, 0.20f),
            RumbleMotorRequest.Zero,
            0.04f);
    }

    public void Consume(in RumbleRequested value, ref EventDispatchContext context)
    {
        float strength = Math.Clamp(value.Strength, 0f, 1f);
        RequestSegment(
            RumbleRequestGroup.Gameplay,
            new RumbleMotorRequest(strength, strength, strength * 0.5f, strength * 0.5f),
            RumbleMotorRequest.Zero,
            Math.Max(0f, value.DurationSeconds));
    }

    public void Consume(in AchievementCompletedEvent value, ref EventDispatchContext context)
    {
        RequestSegment(
            RumbleRequestGroup.Achievement,
            new RumbleMotorRequest(0f, 0.38f, 0.20f, 0.20f),
            RumbleMotorRequest.Zero,
            0.07f);
        RequestSegment(
            RumbleRequestGroup.Achievement,
            new RumbleMotorRequest(0.38f, 0.24f, 0.12f, 0.12f),
            RumbleMotorRequest.Zero,
            0.14f,
            0.12f);
    }

    private void RequestRumble(RumbleRequestKind kind, RumbleRequestGroup group) =>
        requests.Request(new RumblePlaybackRequest(
            kind, group, default, default, 0f, 0f, 1f, ++sequence));

    private void RequestSegment(
        RumbleRequestGroup group,
        in RumbleMotorRequest start,
        in RumbleMotorRequest end,
        float durationSeconds,
        float delaySeconds = 0f)
    {
        if (durationSeconds <= 0f) return;
        requests.Request(new RumblePlaybackRequest(
            RumbleRequestKind.PlaySegment,
            group,
            start,
            end,
            durationSeconds,
            Math.Max(0f, delaySeconds),
            1f,
            ++sequence));
    }

    private static RumbleRequestGroup ToGroup(int group) => group switch
    {
        1 => RumbleRequestGroup.UiHover,
        2 => RumbleRequestGroup.HotKeyHold,
        3 => RumbleRequestGroup.Gameplay,
        4 => RumbleRequestGroup.Achievement,
        _ => RumbleRequestGroup.Default,
    };
}

/// <summary>Typed event surface for all 38 ECS-045-owned event contracts.</summary>
public sealed class PresentationEventHub
{
    public EventStream<JigglePulseConfig> JigglePulseConfig { get; } = new();
    public EventStream<JigglePulseEvent> JigglePulse { get; } = new();
    public EventStream<AudioSettingsChangedEvent> AudioSettingsChanged { get; } = new();
    public EventStream<BattlePhaseAnimationCompleteEvent> BattlePhaseAnimationComplete { get; } = new();
    public EventStream<ShowStartOfBattleAnimationEvent> ShowStartOfBattleAnimation { get; } = new();
    public EventStream<ShowVictoryAnimationEvent> ShowVictoryAnimation { get; } = new();
    public EventStream<ShuffleDeckAnimationCompleted> ShuffleDeckAnimationCompleted { get; } = new();
    public EventStream<VictoryAnimationCompleteEvent> VictoryAnimationComplete { get; } = new();
    public EventStream<CardBaseRenderCompletedEvent> CardBaseRenderCompleted { get; } = new();
    public EventStream<CardBaseRenderStartedEvent> CardBaseRenderStarted { get; } = new();
    public EventStream<CardHighlightRenderEvent> CardHighlightRender { get; } = new();
    public EventStream<CardRenderEvent> CardRender { get; } = new();
    public EventStream<CardRenderScaledEvent> CardRenderScaled { get; } = new();
    public EventStream<CardRenderScaledRotatedEvent> CardRenderScaledRotated { get; } = new();
    public EventStream<ChangeMusicTrack> ChangeMusicTrack { get; } = new();
    public EventStream<EquipmentHighlightRenderEvent> EquipmentHighlightRender { get; } = new();
    public EventStream<HighlightRenderEvent> HighlightRender { get; } = new();
    public EventStream<PlaySfxEvent> PlaySfx { get; } = new();
    public EventStream<StopMusic> StopMusic { get; } = new();
    public EventStream<StopSfxEvent> StopSfx { get; } = new();
    public EventStream<DebuffAnimationComplete> DebuffAnimationComplete { get; } = new();
    public EventStream<ShowTransition> ShowTransition { get; } = new();
    public EventStream<StartDebuffAnimation> StartDebuffAnimation { get; } = new();
    public EventStream<TransitionCompleteEvent> TransitionComplete { get; } = new();
    public EventStream<HideLocationNameEvent> HideLocationName { get; } = new();
    public EventStream<PlunderRescueAnimationCompleted> PlunderRescueAnimationCompleted { get; } = new();
    public EventStream<PlunderSnatchAnimationCompleted> PlunderSnatchAnimationCompleted { get; } = new();
    public EventStream<RumbleGroupCleared> RumbleGroupCleared { get; } = new();
    public EventStream<RumbleSettingsChangedEvent> RumbleSettingsChanged { get; } = new();
    public EventStream<RectangularShockwaveEvent> RectangularShockwave { get; } = new();
    public EventStream<ShockwaveEvent> Shockwave { get; } = new();
    public EventStream<UpdateLocationNameEvent> UpdateLocationName { get; } = new();
    public EventStream<BattlePresentationCompleted> BattlePresentationCompleted { get; } = new();
    public EventStream<BattlePresentationStarted> BattlePresentationStarted { get; } = new();
    public EventStream<BeginDefeatPresentationEvent> BeginDefeatPresentation { get; } = new();
    public EventStream<PixelBurstAnimationCompleted> PixelBurstAnimationCompleted { get; } = new();
    public EventStream<VisualEffectCompleted> VisualEffectCompleted { get; } = new();
    public EventStream<VisualEffectImpactReached> VisualEffectImpactReached { get; } = new();

    /// <summary>Builds the complete presentation route set for root endpoint composition.</summary>
    public IEventRoute[] BuildRoutes(
        PresentationRequestConsumer? requests = null,
        JigglePulsePresentationSystem? jiggle = null) =>
    [
        Route(45001, nameof(JigglePulseConfig), JigglePulseConfig),
        Route(45002, nameof(JigglePulseEvent), JigglePulse, Consumer(jiggle)),
        Route(45003, nameof(AudioSettingsChangedEvent), AudioSettingsChanged),
        Route(45004, nameof(BattlePhaseAnimationCompleteEvent), BattlePhaseAnimationComplete),
        Route(45005, nameof(ShowStartOfBattleAnimationEvent), ShowStartOfBattleAnimation),
        Route(45006, nameof(ShowVictoryAnimationEvent), ShowVictoryAnimation),
        Route(45007, nameof(ShuffleDeckAnimationCompleted), ShuffleDeckAnimationCompleted),
        Route(45008, nameof(VictoryAnimationCompleteEvent), VictoryAnimationComplete),
        Route(45009, nameof(CardBaseRenderCompletedEvent), CardBaseRenderCompleted),
        Route(45010, nameof(CardBaseRenderStartedEvent), CardBaseRenderStarted),
        Route(45011, nameof(CardHighlightRenderEvent), CardHighlightRender),
        Route(45012, nameof(CardRenderEvent), CardRender),
        Route(45013, nameof(CardRenderScaledEvent), CardRenderScaled),
        Route(45014, nameof(CardRenderScaledRotatedEvent), CardRenderScaledRotated),
        Route(45015, nameof(ChangeMusicTrack), ChangeMusicTrack, Consumer<ChangeMusicTrack>(requests)),
        Route(45016, nameof(EquipmentHighlightRenderEvent), EquipmentHighlightRender),
        Route(45017, nameof(HighlightRenderEvent), HighlightRender),
        Route(45018, nameof(PlaySfxEvent), PlaySfx, Consumer<PlaySfxEvent>(requests)),
        Route(45019, nameof(StopMusic), StopMusic, Consumer<StopMusic>(requests)),
        Route(45020, nameof(StopSfxEvent), StopSfx, Consumer<StopSfxEvent>(requests)),
        Route(45021, nameof(DebuffAnimationComplete), DebuffAnimationComplete),
        Route(45022, nameof(ShowTransition), ShowTransition),
        Route(45023, nameof(StartDebuffAnimation), StartDebuffAnimation, Consumer<StartDebuffAnimation>(requests)),
        Route(45024, nameof(TransitionCompleteEvent), TransitionComplete),
        Route(45025, nameof(HideLocationNameEvent), HideLocationName),
        Route(45026, nameof(PlunderRescueAnimationCompleted), PlunderRescueAnimationCompleted),
        Route(45027, nameof(PlunderSnatchAnimationCompleted), PlunderSnatchAnimationCompleted),
        Route(45028, nameof(RumbleGroupCleared), RumbleGroupCleared, Consumer<RumbleGroupCleared>(requests)),
        Route(45029, nameof(RumbleSettingsChangedEvent), RumbleSettingsChanged, Consumer<RumbleSettingsChangedEvent>(requests)),
        Route(45030, nameof(RectangularShockwaveEvent), RectangularShockwave, Consumer<RectangularShockwaveEvent>(requests)),
        Route(45031, nameof(ShockwaveEvent), Shockwave, Consumer<ShockwaveEvent>(requests)),
        Route(45032, nameof(UpdateLocationNameEvent), UpdateLocationName),
        Route(45033, nameof(BattlePresentationCompleted), BattlePresentationCompleted),
        Route(45034, nameof(BattlePresentationStarted), BattlePresentationStarted),
        Route(45035, nameof(BeginDefeatPresentationEvent), BeginDefeatPresentation),
        Route(45036, nameof(PixelBurstAnimationCompleted), PixelBurstAnimationCompleted),
        Route(45037, nameof(VisualEffectCompleted), VisualEffectCompleted),
        Route(45038, nameof(VisualEffectImpactReached), VisualEffectImpactReached),
    ];

    private static EventRoute<T> Route<T>(int id, string name, EventStream<T> stream,
        EventConsumerRegistration<T>[]? consumers = null) where T : unmanaged =>
        new(id, name, stream, consumers ?? Array.Empty<EventConsumerRegistration<T>>());

    private static EventConsumerRegistration<T>[] Consumer<T>(IEventConsumer<T>? consumer) where T : unmanaged =>
        consumer is null ? Array.Empty<EventConsumerRegistration<T>>() : [new(0, consumer.GetType().Name, consumer)];
}
