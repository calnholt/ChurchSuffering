#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Gameplay.Meta;
using Crusaders30XX.ECS.DataOriented.Integration;
using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Integration.Host;

public readonly record struct HostRuntimeDiagnostics(
    long Frame,
    SceneGroup Scene,
    int EntityCount,
    int ArchetypeCount,
    long StructuralMoveCount,
    int PendingEventCount,
    int RenderPacketCount,
    long RenderExtractionVersion);

public interface IHostDiagnosticsSink
{
    void Report(in HostRuntimeDiagnostics diagnostics);
}

public sealed class HostRuntimeDiagnosticsAdapter
{
    public HostRuntimeDiagnostics Capture(DataOrientedGameRuntime runtime, long frame)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        if (frame < 0) throw new ArgumentOutOfRangeException(nameof(frame));
        SceneGroup scene = runtime.World.Get<SceneState>(runtime.Globals.Scene).Current;
        return new HostRuntimeDiagnostics(
            frame,
            scene,
            runtime.World.EntityCount,
            runtime.World.ArchetypeCount,
            runtime.World.StructuralMoveCount,
            runtime.Events.PendingEventCount,
            runtime.Packets.Count,
            runtime.Packets.ExtractionVersion);
    }

    public void Report(DataOrientedGameRuntime runtime, long frame, IHostDiagnosticsSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        HostRuntimeDiagnostics diagnostics = Capture(runtime, frame);
        sink.Report(in diagnostics);
    }
}

public interface IHostRuntimeLifecycleSink
{
    void Started(in HostRuntimeDiagnostics diagnostics);
    void FrameCompleted(in HostRuntimeDiagnostics diagnostics);
    void Stopping(in HostRuntimeDiagnostics diagnostics);
}

/// <summary>Ordered lifecycle notifications carrying read-only runtime diagnostics.</summary>
public sealed class HostRuntimeLifecycleAdapter
{
    private readonly HostRuntimeDiagnosticsAdapter diagnostics = new();
    private bool started;
    private bool stopped;

    public void Start(DataOrientedGameRuntime runtime, IHostRuntimeLifecycleSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        if (started) throw new InvalidOperationException("The data-oriented host lifecycle has already started.");
        HostRuntimeDiagnostics snapshot = diagnostics.Capture(runtime, 0);
        started = true;
        sink.Started(in snapshot);
    }

    public void CompleteFrame(DataOrientedGameRuntime runtime, long frame, IHostRuntimeLifecycleSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        EnsureRunning();
        HostRuntimeDiagnostics snapshot = diagnostics.Capture(runtime, frame);
        sink.FrameCompleted(in snapshot);
    }

    public void Stop(DataOrientedGameRuntime runtime, long frame, IHostRuntimeLifecycleSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        EnsureRunning();
        HostRuntimeDiagnostics snapshot = diagnostics.Capture(runtime, frame);
        stopped = true;
        sink.Stopping(in snapshot);
    }

    private void EnsureRunning()
    {
        if (!started) throw new InvalidOperationException("Start the data-oriented host lifecycle first.");
        if (stopped) throw new InvalidOperationException("The data-oriented host lifecycle has stopped.");
    }
}

public interface IDataOrientedSaveStore
{
    bool TryLoad(out MetaSaveDto? save);
    void Save(MetaSaveDto save);
}

public readonly record struct HostSaveCoordinates(uint ClimbSeed, int CurrentColumn, int Gold);

/// <summary>External persistence seam for the fresh-version data-oriented save DTO.</summary>
public sealed class DataOrientedSaveHostAdapter
{
    public MetaSaveDto LoadOrFresh(IDataOrientedSaveStore store, uint freshSeed = 1)
    {
        ArgumentNullException.ThrowIfNull(store);
        return store.TryLoad(out MetaSaveDto? save) && save is not null
            ? save
            : MetaSaveDto.Fresh(freshSeed);
    }

    public MetaSaveDto ExtractAndSave(
        DataOrientedGameRuntime runtime,
        IDataOrientedSaveStore store,
        in HostSaveCoordinates coordinates)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(store);
        MetaSaveDto save = runtime.ExtractSave(
            coordinates.ClimbSeed,
            coordinates.CurrentColumn,
            coordinates.Gold);
        store.Save(save);
        return save;
    }
}
