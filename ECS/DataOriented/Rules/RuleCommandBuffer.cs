#nullable enable

using System;

namespace Crusaders30XX.ECS.DataOriented.Rules;

public readonly record struct RuleCommandIndex(int Index, int Version);

public sealed class RuleCommandBuffer
{
    private RuleCommand[] commands;
    private int version = 1;

    public RuleCommandBuffer(int initialCapacity = 32)
    {
        if (initialCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        }

        commands = new RuleCommand[Math.Max(1, initialCapacity)];
    }

    public int Count { get; private set; }

    public int Version => version;

    public RuleCommandWriter Writer => new(this);

    public ref readonly RuleCommand this[int index]
    {
        get
        {
            if ((uint)index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return ref commands[index];
        }
    }

    public ReadOnlySpan<RuleCommand> AsReadOnlySpan() => commands.AsSpan(0, Count);

    public void Clear()
    {
        commands.AsSpan(0, Count).Clear();
        Count = 0;
        version = version == int.MaxValue ? 1 : version + 1;
    }

    internal RuleCommandIndex Append(in RuleCommand command)
    {
        if (command.Kind == RuleCommandKind.None)
        {
            throw new ArgumentException("A rule command must have a concrete kind.", nameof(command));
        }

        EnsureCapacity(Count + 1);
        int index = Count++;
        commands[index] = command.WithSequence(index);
        return new RuleCommandIndex(index, version);
    }

    internal void AppendRange(ReadOnlySpan<RuleCommand> values)
    {
        EnsureCapacity(checked(Count + values.Length));
        for (var index = 0; index < values.Length; index++)
        {
            Append(in values[index]);
        }
    }

    private void EnsureCapacity(int required)
    {
        if (commands.Length >= required)
        {
            return;
        }

        Array.Resize(ref commands, Math.Max(required, commands.Length * 2));
    }
}

public readonly struct RuleCommandWriter
{
    private readonly RuleCommandBuffer? buffer;

    internal RuleCommandWriter(RuleCommandBuffer buffer)
    {
        this.buffer = buffer;
    }

    public int Count => RequiredBuffer.Count;

    public RuleCommandIndex Append(in RuleCommand command) => RequiredBuffer.Append(in command);

    public void AppendRange(ReadOnlySpan<RuleCommand> commands) => RequiredBuffer.AppendRange(commands);

    private RuleCommandBuffer RequiredBuffer => buffer ??
        throw new InvalidOperationException("A default rule command writer cannot append commands.");
}
