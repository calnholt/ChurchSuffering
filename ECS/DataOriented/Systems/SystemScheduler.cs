#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;

namespace Crusaders30XX.ECS.DataOriented.Systems;

public sealed class SystemScheduler
{
    private const int PhaseCount = 7;
    private const int SceneCount = 7;

    private readonly World world;
    private readonly EventRuntime events;
    private readonly bool profilingEnabled;
    private readonly List<ScheduledSystem> registrations = [];
    private readonly ScheduledSystem[][] executionCaches = new ScheduledSystem[PhaseCount * SceneCount][];
    private SceneGroup activeScene = SceneGroup.TitleMenu;
    private bool cachesDirty = true;
    private long frameIndex;

    public SystemScheduler(World world, EventRuntime events, bool? profilingEnabled = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(events);
        world.AttachEventRuntime(events);
        this.world = world;
        this.events = events;
        this.profilingEnabled = profilingEnabled ?? DefaultProfilingEnabled;
    }

    public SceneGroup ActiveScene
    {
        get => activeScene;
        set
        {
            ValidateActiveScene(value);
            activeScene = value;
        }
    }

    public long FrameIndex => frameIndex;

    public int Count => registrations.Count;

    public void Register(IGameSystem system)
    {
        ArgumentNullException.ThrowIfNull(system);
        SystemDescriptor descriptor = system.Descriptor ??
            throw new ArgumentException("A game system must expose a descriptor.", nameof(system));
        for (var index = 0; index < registrations.Count; index++)
        {
            if (registrations[index].Descriptor.Id == descriptor.Id)
            {
                throw new InvalidOperationException(
                    $"System ID {descriptor.Id.Value} is already registered by '{registrations[index].Descriptor.Name}'; " +
                    $"'{descriptor.Name}' cannot reuse it.");
            }
        }

        registrations.Add(new ScheduledSystem(system, descriptor));
        cachesDirty = true;
    }

    public void Build()
    {
        var indexesById = new Dictionary<SystemId, int>(registrations.Count);
        for (var index = 0; index < registrations.Count; index++)
        {
            indexesById.Add(registrations[index].Descriptor.Id, index);
        }

        var edges = new bool[registrations.Count, registrations.Count];
        for (var index = 0; index < registrations.Count; index++)
        {
            SystemDescriptor descriptor = registrations[index].Descriptor;
            AddDependencies(index, descriptor.RunsBefore, before: true, indexesById, edges);
            AddDependencies(index, descriptor.RunsAfter, before: false, indexesById, edges);
        }

        ValidateAccessConflicts(edges);
        int[] sorted = TopologicalSort(edges);
        BuildExecutionCaches(sorted);
        cachesDirty = false;
    }

    public void Update(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }

        EnsureBuilt();
        for (var phaseValue = 0; phaseValue < PhaseCount; phaseValue++)
        {
            var phase = (SystemPhase)phaseValue;
            ScheduledSystem[] systems = executionCaches[CacheIndex(activeScene, phase)];
            var drainAfterPhase = false;
            for (var index = 0; index < systems.Length; index++)
            {
                ScheduledSystem scheduled = systems[index];
                SystemDescriptor descriptor = scheduled.Descriptor;
                long allocatedBefore = profilingEnabled ? GC.GetAllocatedBytesForCurrentThread() : 0;
                long started = profilingEnabled ? Stopwatch.GetTimestamp() : 0;
                try
                {
                    var context = new SystemContext(
                        world,
                        scheduled.Commands,
                        events,
                        frameIndex,
                        elapsed,
                        activeScene);
                    scheduled.System.Update(ref context);
                    if (!descriptor.RecordsStructuralCommands && scheduled.Commands.Count != 0)
                    {
                        throw new InvalidOperationException(
                            $"System '{descriptor.Name}' recorded {scheduled.Commands.Count} structural command(s) " +
                            "but its descriptor declares RecordsStructuralCommands=false.");
                    }
                    scheduled.Commands.Playback(world);

                    if (descriptor.EventBarrier == EventBarrier.AfterSystem)
                    {
                        events.DrainBarrier();
                    }
                    else if (descriptor.EventBarrier == EventBarrier.AfterPhase)
                    {
                        drainAfterPhase = true;
                    }
                }
                catch
                {
                    scheduled.Commands.Clear();
                    throw;
                }
                finally
                {
                    if (profilingEnabled)
                    {
                        scheduled.Profile.Record(
                            Stopwatch.GetTimestamp() - started,
                            GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
                    }
                }
            }

            if (drainAfterPhase)
            {
                events.DrainBarrier();
            }
        }

        frameIndex++;
    }

    public bool TryGetProfile(SystemId systemId, out SystemProfileSnapshot profile)
    {
        for (var index = 0; index < registrations.Count; index++)
        {
            ScheduledSystem scheduled = registrations[index];
            if (scheduled.Descriptor.Id == systemId)
            {
                profile = scheduled.Profile.Snapshot(scheduled.Descriptor);
                return true;
            }
        }

        profile = default;
        return false;
    }

    public SystemProfileSnapshot[] GetProfileSnapshot()
    {
        var result = new SystemProfileSnapshot[registrations.Count];
        for (var index = 0; index < registrations.Count; index++)
        {
            ScheduledSystem scheduled = registrations[index];
            result[index] = scheduled.Profile.Snapshot(scheduled.Descriptor);
        }

        return result;
    }

    public string[] GetExecutionOrder(SceneGroup scene, SystemPhase phase)
    {
        ValidateActiveScene(scene);
        if (!Enum.IsDefined(phase))
        {
            throw new ArgumentOutOfRangeException(nameof(phase), phase, null);
        }
        EnsureBuilt();
        ScheduledSystem[] systems = executionCaches[CacheIndex(scene, phase)];
        var result = new string[systems.Length];
        for (var index = 0; index < systems.Length; index++)
        {
            result[index] = systems[index].Descriptor.Name;
        }

        return result;
    }

    private void AddDependencies(
        int ownerIndex,
        ReadOnlySpan<SystemId> dependencies,
        bool before,
        Dictionary<SystemId, int> indexesById,
        bool[,] edges)
    {
        SystemDescriptor owner = registrations[ownerIndex].Descriptor;
        for (var dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
        {
            SystemId dependencyId = dependencies[dependencyIndex];
            if (!indexesById.TryGetValue(dependencyId, out int targetIndex))
            {
                throw new InvalidOperationException(
                    $"System '{owner.Name}' references missing dependency ID {dependencyId.Value}.");
            }

            SystemDescriptor target = registrations[targetIndex].Descriptor;
            int from = before ? ownerIndex : targetIndex;
            int to = before ? targetIndex : ownerIndex;
            SystemDescriptor fromDescriptor = registrations[from].Descriptor;
            SystemDescriptor toDescriptor = registrations[to].Descriptor;
            if (fromDescriptor.Phase > toDescriptor.Phase)
            {
                throw new InvalidOperationException(
                    $"Dependency '{fromDescriptor.Name}' -> '{toDescriptor.Name}' contradicts frozen phase order " +
                    $"({fromDescriptor.Phase} runs after {toDescriptor.Phase}).");
            }

            if (fromDescriptor.Phase == toDescriptor.Phase)
            {
                edges[from, to] = true;
            }
        }
    }

    private void ValidateAccessConflicts(bool[,] edges)
    {
        for (var first = 0; first < registrations.Count; first++)
        {
            SystemDescriptor left = registrations[first].Descriptor;
            for (var second = first + 1; second < registrations.Count; second++)
            {
                SystemDescriptor right = registrations[second].Descriptor;
                if (left.Phase != right.Phase || !CanRunTogether(left.SceneGroup, right.SceneGroup) ||
                    !HasAccessConflict(left, right))
                {
                    continue;
                }

                if (!HasPath(first, second, edges) && !HasPath(second, first, edges))
                {
                    throw new InvalidOperationException(
                        $"Systems '{left.Name}' and '{right.Name}' have conflicting declared access in phase " +
                        $"{left.Phase} but no explicit before/after dependency orders them.");
                }
            }
        }
    }

    private int[] TopologicalSort(bool[,] edges)
    {
        int count = registrations.Count;
        var indegree = new int[count];
        for (var from = 0; from < count; from++)
        {
            for (var to = 0; to < count; to++)
            {
                if (edges[from, to]) indegree[to]++;
            }
        }

        var sorted = new int[count];
        var emitted = new bool[count];
        for (var output = 0; output < count; output++)
        {
            int next = -1;
            for (var candidate = 0; candidate < count; candidate++)
            {
                if (!emitted[candidate] && indegree[candidate] == 0)
                {
                    next = candidate;
                    break;
                }
            }

            if (next < 0)
            {
                throw BuildCycleException(edges);
            }

            sorted[output] = next;
            emitted[next] = true;
            for (var target = 0; target < count; target++)
            {
                if (edges[next, target]) indegree[target]--;
            }
        }

        return sorted;
    }

    private InvalidOperationException BuildCycleException(bool[,] edges)
    {
        int count = registrations.Count;
        var states = new byte[count];
        var stack = new int[count];
        var stackCount = 0;
        for (var index = 0; index < count; index++)
        {
            if (states[index] == 0 && TryFindCycle(index, edges, states, stack, ref stackCount, out string? path))
            {
                return new InvalidOperationException($"System dependency cycle detected: {path}");
            }
        }

        return new InvalidOperationException("System dependency cycle detected.");
    }

    private bool TryFindCycle(
        int current,
        bool[,] edges,
        byte[] states,
        int[] stack,
        ref int stackCount,
        out string? path)
    {
        states[current] = 1;
        stack[stackCount++] = current;
        for (var target = 0; target < registrations.Count; target++)
        {
            if (!edges[current, target]) continue;
            if (states[target] == 0 && TryFindCycle(target, edges, states, stack, ref stackCount, out path))
            {
                return true;
            }

            if (states[target] == 1)
            {
                int start = 0;
                while (stack[start] != target) start++;
                var names = new string[stackCount - start + 1];
                for (var index = start; index < stackCount; index++)
                {
                    names[index - start] = registrations[stack[index]].Descriptor.Name;
                }
                names[^1] = registrations[target].Descriptor.Name;
                path = string.Join(" -> ", names);
                return true;
            }
        }

        stackCount--;
        states[current] = 2;
        path = null;
        return false;
    }

    private void BuildExecutionCaches(int[] sorted)
    {
        for (var sceneValue = 1; sceneValue < SceneCount; sceneValue++)
        {
            var scene = (SceneGroup)sceneValue;
            for (var phaseValue = 0; phaseValue < PhaseCount; phaseValue++)
            {
                var phase = (SystemPhase)phaseValue;
                var count = 0;
                for (var index = 0; index < sorted.Length; index++)
                {
                    SystemDescriptor descriptor = registrations[sorted[index]].Descriptor;
                    if (descriptor.Phase == phase &&
                        (descriptor.SceneGroup == SceneGroup.Global || descriptor.SceneGroup == scene))
                    {
                        count++;
                    }
                }

                var cache = new ScheduledSystem[count];
                var written = 0;
                for (var index = 0; index < sorted.Length; index++)
                {
                    ScheduledSystem scheduled = registrations[sorted[index]];
                    if (scheduled.Descriptor.Phase == phase &&
                        (scheduled.Descriptor.SceneGroup == SceneGroup.Global ||
                         scheduled.Descriptor.SceneGroup == scene))
                    {
                        cache[written++] = scheduled;
                    }
                }

                executionCaches[CacheIndex(scene, phase)] = cache;
            }
        }
    }

    private static bool HasAccessConflict(SystemDescriptor left, SystemDescriptor right)
    {
        if (left.RequiresExclusiveWorldAccess || right.RequiresExclusiveWorldAccess)
        {
            return true;
        }

        ComponentSignature leftAffected = left.ReadComponents | left.WriteComponents;
        ComponentSignature rightAffected = right.ReadComponents | right.WriteComponents;
        if (left.WriteComponents.Intersects(rightAffected) || right.WriteComponents.Intersects(leftAffected))
        {
            return true;
        }

        if (BufferAccessConflicts(left, right) || EventAccessConflicts(left, right))
        {
            return true;
        }

        return false;
    }

    private static bool BufferAccessConflicts(SystemDescriptor left, SystemDescriptor right) =>
        Intersects(left.WriteDynamicBufferTypes, right.ReadDynamicBufferTypes) ||
        Intersects(left.WriteDynamicBufferTypes, right.WriteDynamicBufferTypes) ||
        Intersects(right.WriteDynamicBufferTypes, left.ReadDynamicBufferTypes);

    private static bool EventAccessConflicts(SystemDescriptor left, SystemDescriptor right) =>
        Intersects(left.EmittedEventTypeIds, right.ConsumedEventTypeIds) ||
        Intersects(right.EmittedEventTypeIds, left.ConsumedEventTypeIds);

    private static bool Intersects<T>(ReadOnlySpan<T> first, ReadOnlySpan<T> second)
    {
        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        for (var left = 0; left < first.Length; left++)
        {
            for (var right = 0; right < second.Length; right++)
            {
                if (comparer.Equals(first[left], second[right])) return true;
            }
        }

        return false;
    }

    private bool HasPath(int from, int to, bool[,] edges)
    {
        var visited = new bool[registrations.Count];
        return Visit(from, to, edges, visited);
    }

    private bool Visit(int current, int target, bool[,] edges, bool[] visited)
    {
        if (current == target) return true;
        if (visited[current]) return false;
        visited[current] = true;
        for (var next = 0; next < registrations.Count; next++)
        {
            if (edges[current, next] && Visit(next, target, edges, visited)) return true;
        }

        return false;
    }

    private void EnsureBuilt()
    {
        if (cachesDirty) Build();
    }

    private static int CacheIndex(SceneGroup scene, SystemPhase phase) =>
        ((int)scene * PhaseCount) + (int)phase;

    private static bool CanRunTogether(SceneGroup left, SceneGroup right) =>
        left == SceneGroup.Global || right == SceneGroup.Global || left == right;

    private static void ValidateActiveScene(SceneGroup scene)
    {
        if (!Enum.IsDefined(scene) || scene == SceneGroup.Global)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scene),
                scene,
                "The active scene must be one frozen non-global scene group.");
        }
    }

    private static bool DefaultProfilingEnabled
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }

    private sealed class ScheduledSystem
    {
        public ScheduledSystem(IGameSystem system, SystemDescriptor descriptor)
        {
            System = system;
            Descriptor = descriptor;
            Commands = new CommandBuffer();
            Profile = new SystemProfile();
        }

        public IGameSystem System { get; }
        public SystemDescriptor Descriptor { get; }
        public CommandBuffer Commands { get; }
        public SystemProfile Profile { get; }
    }
}
