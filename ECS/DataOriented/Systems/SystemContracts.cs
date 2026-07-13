#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;

namespace Crusaders30XX.ECS.DataOriented.Systems;

public readonly record struct SystemId(int Value)
{
    public static SystemId Invalid => default;
    public bool IsValid => Value > 0;
    public override string ToString() => Value.ToString();
}

public enum SystemPhase : byte
{
    Input = 0,
    Interaction = 1,
    Rules = 2,
    Gameplay = 3,
    Presentation = 4,
    LatePresentation = 5,
    RenderExtraction = 6,
}

public enum SceneGroup : byte
{
    Global = 0,
    TitleMenu = 1,
    WayStation = 2,
    Climb = 3,
    Battle = 4,
    Achievement = 5,
    Snapshot = 6,
}

public enum EventBarrier : byte
{
    None = 0,
    AfterSystem = 1,
    AfterPhase = 2,
}

public interface IGameSystem
{
    SystemDescriptor Descriptor { get; }

    void Update(ref SystemContext context);
}

public readonly struct SystemContext
{
    internal SystemContext(
        World world,
        CommandBuffer commands,
        EventRuntime events,
        long frameIndex,
        TimeSpan elapsed,
        SceneGroup activeScene)
    {
        World = world;
        Commands = commands;
        Events = events;
        FrameIndex = frameIndex;
        Elapsed = elapsed;
        ActiveScene = activeScene;
    }

    public World World { get; }

    public CommandBuffer Commands { get; }

    public EventRuntime Events { get; }

    public long FrameIndex { get; }

    public TimeSpan Elapsed { get; }

    public SceneGroup ActiveScene { get; }
}
