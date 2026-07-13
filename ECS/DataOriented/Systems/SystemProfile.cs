#nullable enable

using System;

namespace Crusaders30XX.ECS.DataOriented.Systems;

public readonly record struct SystemProfileSnapshot(
    SystemId SystemId,
    string SystemName,
    long InvocationCount,
    long LastElapsedTicks,
    long MaximumElapsedTicks,
    long TotalElapsedTicks,
    long LastAllocatedBytes,
    long MaximumAllocatedBytes,
    long TotalAllocatedBytes);

internal sealed class SystemProfile
{
    public long InvocationCount { get; private set; }
    public long LastElapsedTicks { get; private set; }
    public long MaximumElapsedTicks { get; private set; }
    public long TotalElapsedTicks { get; private set; }
    public long LastAllocatedBytes { get; private set; }
    public long MaximumAllocatedBytes { get; private set; }
    public long TotalAllocatedBytes { get; private set; }

    public void Record(long elapsedTicks, long allocatedBytes)
    {
        InvocationCount++;
        LastElapsedTicks = elapsedTicks;
        MaximumElapsedTicks = Math.Max(MaximumElapsedTicks, elapsedTicks);
        TotalElapsedTicks += elapsedTicks;
        LastAllocatedBytes = allocatedBytes;
        MaximumAllocatedBytes = Math.Max(MaximumAllocatedBytes, allocatedBytes);
        TotalAllocatedBytes += allocatedBytes;
    }

    public SystemProfileSnapshot Snapshot(SystemDescriptor descriptor) => new(
        descriptor.Id,
        descriptor.Name,
        InvocationCount,
        LastElapsedTicks,
        MaximumElapsedTicks,
        TotalElapsedTicks,
        LastAllocatedBytes,
        MaximumAllocatedBytes,
        TotalAllocatedBytes);
}
