using System;
using System.Collections.Generic;

namespace ChurchSuffering.ECS.Rendering;

internal sealed class WeightedLruCache<TKey, TValue> : IDisposable where TKey : notnull
{
    private readonly long _capacity;
    private readonly Action<TValue> _dispose;
    private readonly Dictionary<TKey, LinkedListNode<Entry>> _entries = new();
    private readonly LinkedList<Entry> _lru = new();
    private long _weight;

    public WeightedLruCache(long capacity, Action<TValue> dispose)
    {
        _capacity = Math.Max(1, capacity);
        _dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));
    }

    public int Count => _entries.Count;
    public long Weight => _weight;

    public bool TryGet(TKey key, out TValue value)
    {
        if (!_entries.TryGetValue(key, out LinkedListNode<Entry> node))
        {
            value = default;
            return false;
        }

        _lru.Remove(node);
        _lru.AddFirst(node);
        value = node.Value.Value;
        return true;
    }

    public void Add(TKey key, TValue value, long weight)
    {
        Remove(key);
        long safeWeight = Math.Max(1, weight);
        if (safeWeight > _capacity)
        {
            _dispose(value);
            return;
        }

        var node = new LinkedListNode<Entry>(new Entry(key, value, safeWeight));
        _lru.AddFirst(node);
        _entries[key] = node;
        _weight += safeWeight;
        while (_weight > _capacity && _lru.Last != null)
        {
            Remove(_lru.Last.Value.Key);
        }
    }

    public void Clear()
    {
        foreach (Entry entry in _lru) _dispose(entry.Value);
        _lru.Clear();
        _entries.Clear();
        _weight = 0;
    }

    public void Dispose() => Clear();

    private void Remove(TKey key)
    {
        if (!_entries.Remove(key, out LinkedListNode<Entry> node)) return;
        _lru.Remove(node);
        _weight -= node.Value.Weight;
        _dispose(node.Value.Value);
    }

    private readonly record struct Entry(TKey Key, TValue Value, long Weight);
}
