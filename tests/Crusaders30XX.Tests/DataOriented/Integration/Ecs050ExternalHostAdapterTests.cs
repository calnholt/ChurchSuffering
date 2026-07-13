#nullable enable

using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Global;
using Crusaders30XX.ECS.DataOriented.Gameplay.Meta;
using Crusaders30XX.ECS.DataOriented.Gameplay.Presentation;
using Crusaders30XX.ECS.DataOriented.Integration;
using Crusaders30XX.ECS.DataOriented.Integration.Host;
using Crusaders30XX.ECS.DataOriented.Rendering;
using Crusaders30XX.ECS.DataOriented.Resources;
using Microsoft.Xna.Framework;
using Xunit;
using LegacyButton = Crusaders30XX.ECS.Input.PlayerButton;
using LegacyDevice = Crusaders30XX.ECS.Input.PlayerInputDevice;
using LegacyFrame = Crusaders30XX.ECS.Input.PlayerInputFrame;
using LegacyMask = Crusaders30XX.ECS.Input.PlayerButtonMask;

namespace Crusaders30XX.Tests.DataOriented.Integration;

public sealed class Ecs050ExternalHostAdapterTests
{
    [Fact]
    public void Centrally_captured_input_maps_edges_modifiers_device_and_letterbox_coordinates()
    {
        LegacyMask down = Mask(LegacyButton.Primary, LegacyButton.LeftStick, LegacyButton.F11,
            LegacyButton.DebugMenu, LegacyButton.Shift, LegacyButton.Quit);
        LegacyMask pressed = Mask(LegacyButton.F11, LegacyButton.DebugMenu, LegacyButton.Quit);
        LegacyMask released = Mask(LegacyButton.Secondary);
        var frame = new LegacyFrame(
            17, true, true, LegacyDevice.Gamepad, LegacyDevice.KeyboardMouse,
            Crusaders30XX.ECS.Input.GamepadGlyphStyle.Xbox,
            new Vector2(400, 300), new Vector2(20, -10), -1f,
            new Vector2(0.25f), new Vector2(-0.5f), 0.2f, 0.8f,
            down, pressed, released);
        var adapter = new CentralInputFrameAdapter();
        var destination = new Rectangle(100, 50, 960, 540);

        var snapshot = adapter.CreateSnapshot(in frame, destination, 1920, 1080);
        var submission = adapter.Convert(in frame, destination, 1920, 1080);
        Crusaders30XX.ECS.DataOriented.Components.PlayerInputFrame mapped = submission.PlayerInput.Frame;

        Assert.Equal(new Vector2(300, 200), snapshot.ScreenPointer);
        Assert.Equal(new Vector2(290, 205), snapshot.PreviousScreenPointer);
        Assert.Equal(new Vector2(400, 300), mapped.PointerPosition);
        Assert.Equal(new Vector2(20, -10), mapped.PointerDelta);
        Assert.Equal(PlayerInputDevice.Gamepad, mapped.Device);
        Assert.True(mapped.IsDown(PlayerInputButton.Primary));
        Assert.True(mapped.IsDown(PlayerInputButton.ShowHint));
        Assert.True(mapped.IsDown(PlayerInputButton.Modifier));
        Assert.True(mapped.WasPressed(PlayerInputButton.ToggleFullScreen));
        Assert.True(mapped.WasPressed(PlayerInputButton.ToggleDebugMenu));
        Assert.True(mapped.WasPressed(PlayerInputButton.Quit));
        Assert.True(mapped.WasReleased(PlayerInputButton.Secondary));
        Assert.Equal(-1f, mapped.ScrollDelta);
        Assert.True(submission.CursorInput.PrimaryDown);
    }

    [Fact]
    public void Inactive_captured_input_clears_buttons_and_edges()
    {
        LegacyMask buttons = Mask(LegacyButton.Primary, LegacyButton.F11);
        var frame = new LegacyFrame(
            1, false, false, LegacyDevice.KeyboardMouse, LegacyDevice.KeyboardMouse,
            Crusaders30XX.ECS.Input.GamepadGlyphStyle.Xbox,
            Vector2.Zero, Vector2.Zero, 0, Vector2.Zero, Vector2.Zero, 0, 0,
            buttons, buttons, LegacyMask.None);
        var submission = new CentralInputFrameAdapter().Convert(
            in frame, new Rectangle(0, 0, 1920, 1080), 1920, 1080);

        Assert.Equal(0UL, submission.PlayerInput.Frame.DownButtons);
        Assert.Equal(0UL, submission.PlayerInput.Frame.PressedButtons);
        Assert.False(submission.CursorInput.PrimaryDown);
    }

    [Fact]
    public void Host_commands_dispatch_in_order_and_snapshot_mode_suppresses_everything_except_quit()
    {
        HostCommandRequestQueue normalQueue = QueueCommands(
            PlayerCommand.ToggleFullScreen,
            PlayerCommand.ToggleDebugMenu,
            PlayerCommand.ToggleEntityList,
            PlayerCommand.DealDebugDamage,
            PlayerCommand.ToggleProfiler,
            PlayerCommand.QuitApplication);
        var normalTarget = new CommandTarget();
        int normalCount = new HostCommandDispatcher().Drain(normalQueue, normalTarget);

        Assert.Equal(6, normalCount);
        Assert.Equal(new[] { "fullscreen", "debug", "entities", "damage", "profiler", "quit" }, normalTarget.Calls);
        Assert.Equal(0, normalQueue.Count);

        HostCommandRequestQueue snapshotQueue = QueueCommands(
            PlayerCommand.ToggleFullScreen,
            PlayerCommand.QuitApplication,
            PlayerCommand.ToggleProfiler);
        var snapshotTarget = new CommandTarget();
        int snapshotCount = new HostCommandDispatcher(snapshotMode: true).Drain(snapshotQueue, snapshotTarget);

        Assert.Equal(1, snapshotCount);
        Assert.Equal(new[] { "quit" }, snapshotTarget.Calls);
        Assert.Equal(0, snapshotQueue.Count);
    }

    [Fact]
    public void Render_adapter_resolves_assets_switches_passes_and_does_not_mutate_packets()
    {
        var packets = new RenderPacketStore(4);
        packets.BeginExtraction();
        packets.Add(Packet(1, 1, RenderPacketFlags.None));
        packets.Add(Packet(2, 2, RenderPacketFlags.Additive));
        packets.Add(Packet(3, 3, RenderPacketFlags.Additive));
        packets.Add(Packet(4, 4, RenderPacketFlags.None, TextureAssetId.Null));
        packets.EndExtraction();
        long version = packets.ExtractionVersion;
        int countBefore = packets.Count;
        var resolver = new TextureResolver();
        var device = new RenderDevice();

        int drawn = new RenderPacketHostAdapter<string>().Draw(packets, resolver, device);

        Assert.Equal(4, drawn);
        Assert.Equal(new[]
        {
            "begin:Alpha", "draw:1:tex-1", "end",
            "begin:Additive", "draw:2:tex-2", "draw:3:tex-3", "end",
            "begin:Alpha", "draw:4:null", "end",
        }, device.Calls);
        Assert.Equal(version, packets.ExtractionVersion);
        Assert.Equal(countBefore, packets.Count);
        Assert.Equal(3, resolver.ResolveCount);
    }

    [Fact]
    public void Audio_shader_and_rumble_requests_drain_once_in_deterministic_queue_order()
    {
        var requests = new PresentationRequestQueues(4);
        requests.Request(new AudioPlaybackRequest(AudioRequestKind.PlaySound, new SoundId(2), 1, 0, 1));
        requests.Request(new AudioPlaybackRequest(AudioRequestKind.StopSound, new SoundId(3), 0, 0, 2));
        requests.Request(new ShaderEffectRequest(ShaderRequestKind.Shockwave, default, default,
            Vector2.Zero, Vector2.One, 0.5f, 3));
        requests.Request(new ShaderEffectRequest(ShaderRequestKind.Poison, default, default,
            Vector2.One, Vector2.One, 0.75f, 4));
        requests.Request(new RumblePlaybackRequest(
            RumbleRequestKind.PlaySegment,
            RumbleRequestGroup.Gameplay,
            new RumbleMotorRequest(0.4f, 0.2f),
            RumbleMotorRequest.Zero,
            0.1f,
            0f,
            1f,
            5));
        var sink = new RequestSink();
        var adapter = new PresentationRequestDrainAdapter();

        Assert.Equal(5, adapter.Drain(5, requests, sink, sink, sink));
        Assert.Equal(0, adapter.Drain(5, requests, sink, sink, sink));
        Assert.Equal(new[] { "audio:1", "audio:2", "shader:3", "shader:4", "rumble:5" }, sink.Calls);
        Assert.Throws<InvalidOperationException>(() => adapter.Drain(4, requests, sink, sink, sink));

        requests.BeginFrame();
        requests.Request(new AudioPlaybackRequest(AudioRequestKind.ChangeMusic, new SoundId(5), 1, 0, 5));
        Assert.Equal(1, adapter.Drain(6, requests, sink, sink, sink));
        Assert.Equal("audio:5", sink.Calls[^1]);
    }

    [Fact]
    public void Lifecycle_diagnostics_and_save_seams_are_ordered_and_read_only()
    {
        using DataOrientedGameRuntime runtime = DataOrientedGameRuntime.Create();
        int entities = runtime.World.EntityCount;
        long moves = runtime.World.StructuralMoveCount;
        var lifecycleSink = new LifecycleSink();
        var lifecycle = new HostRuntimeLifecycleAdapter();

        lifecycle.Start(runtime, lifecycleSink);
        lifecycle.CompleteFrame(runtime, 1, lifecycleSink);
        lifecycle.Stop(runtime, 1, lifecycleSink);

        Assert.Equal(new[] { "start:0", "frame:1", "stop:1" }, lifecycleSink.Calls);
        Assert.Equal(entities, runtime.World.EntityCount);
        Assert.Equal(moves, runtime.World.StructuralMoveCount);
        Assert.Throws<InvalidOperationException>(() => lifecycle.CompleteFrame(runtime, 2, lifecycleSink));

        var saveStore = new SaveStore();
        var saves = new DataOrientedSaveHostAdapter();
        Assert.Equal((uint)99, saves.LoadOrFresh(saveStore, 99).ClimbSeed);
        var coordinates = new HostSaveCoordinates(7, 2, 41);
        MetaSaveDto saved = saves.ExtractAndSave(runtime, saveStore, in coordinates);
        Assert.Same(saved, saveStore.Saved);
        Assert.Equal(7u, saved.ClimbSeed);
        Assert.Equal(2, saved.CurrentColumn);
        Assert.Equal(41, saved.Gold);
    }

    private static LegacyMask Mask(params LegacyButton[] buttons)
    {
        LegacyMask mask = LegacyMask.None;
        for (var index = 0; index < buttons.Length; index++) mask |= LegacyFrame.Mask(buttons[index]);
        return mask;
    }

    private static HostCommandRequestQueue QueueCommands(params PlayerCommand[] commands)
    {
        var queue = new HostCommandRequestQueue();
        var stream = new EventStream<PlayerCommandEvent>();
        var route = new EventRoute<PlayerCommandEvent>(99050, "host-command-test", stream,
            new EventConsumerRegistration<PlayerCommandEvent>(0, "host-queue", queue));
        var runtime = new EventRuntime(new EventRoutingEndpoint([route]));
        for (var index = 0; index < commands.Length; index++)
            stream.Publish(new PlayerCommandEvent(commands[index], PlayerInputDevice.KeyboardMouse));
        runtime.DrainBarrier();
        return queue;
    }

    private static RenderPacket Packet(
        int entity,
        int z,
        RenderPacketFlags flags,
        TextureAssetId? texture = null) => new(
        new EntityId(entity, 1), texture ?? new TextureAssetId(entity), default,
        Vector2.Zero, Vector2.Zero, Vector2.One, Color.White, 0, 0, 0,
        z, entity, RenderLayer.World, RenderPacketKind.Sprite, flags);

    private sealed class CommandTarget : IHostCommandTarget
    {
        public List<string> Calls { get; } = [];
        public void QuitApplication() => Calls.Add("quit");
        public void ToggleFullScreen() => Calls.Add("fullscreen");
        public void ToggleDebugMenu() => Calls.Add("debug");
        public void ToggleEntityList() => Calls.Add("entities");
        public void DealDebugDamage() => Calls.Add("damage");
        public void ToggleProfiler() => Calls.Add("profiler");
    }

    private sealed class TextureResolver : IHostTextureResolver<string>
    {
        public int ResolveCount { get; private set; }
        public bool TryResolve(TextureAssetId id, out string? texture)
        {
            ResolveCount++;
            texture = $"tex-{id.Value}";
            return true;
        }
    }

    private sealed class RenderDevice : IHostRenderDevice<string>
    {
        public List<string> Calls { get; } = [];
        public void Begin(HostRenderPass pass) => Calls.Add($"begin:{pass}");
        public void Draw(in RenderPacket packet, string? texture) =>
            Calls.Add($"draw:{packet.Entity.Index}:{texture ?? "null"}");
        public void End() => Calls.Add("end");
    }

    private sealed class RequestSink : IHostAudioRequestSink, IHostShaderRequestSink, IHostRumbleRequestSink
    {
        public List<string> Calls { get; } = [];
        public void Dispatch(in AudioPlaybackRequest request) => Calls.Add($"audio:{request.Sequence}");
        public void Dispatch(in ShaderEffectRequest request) => Calls.Add($"shader:{request.Sequence}");
        public void Dispatch(in RumblePlaybackRequest request) => Calls.Add($"rumble:{request.Sequence}");
    }

    private sealed class LifecycleSink : IHostRuntimeLifecycleSink
    {
        public List<string> Calls { get; } = [];
        public void Started(in HostRuntimeDiagnostics diagnostics) => Calls.Add($"start:{diagnostics.Frame}");
        public void FrameCompleted(in HostRuntimeDiagnostics diagnostics) => Calls.Add($"frame:{diagnostics.Frame}");
        public void Stopping(in HostRuntimeDiagnostics diagnostics) => Calls.Add($"stop:{diagnostics.Frame}");
    }

    private sealed class SaveStore : IDataOrientedSaveStore
    {
        public MetaSaveDto? Saved { get; private set; }
        public bool TryLoad(out MetaSaveDto? save) { save = null; return false; }
        public void Save(MetaSaveDto save) => Saved = save;
    }
}
