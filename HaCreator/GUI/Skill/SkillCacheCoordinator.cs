using System;
using System.Collections.Generic;

namespace HaCreator.GUI.Skill;

public sealed class SkillCacheCoordinator<TKey, TValue> : IDisposable where TValue : class
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<(TKey key, TValue value)>> _entries = new();
    private readonly LinkedList<(TKey key, TValue value)> _lru = new();
    public SkillCacheCoordinator(int capacity) => _capacity = Math.Max(1, capacity);
    public int Count => _entries.Count;
    public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
    {
        if (_entries.TryGetValue(key, out var existing)) { _lru.Remove(existing); _lru.AddFirst(existing); return existing.Value.value; }
        TValue value = factory(key); if (value == null) return null;
        var node = _lru.AddFirst((key, value)); _entries[key] = node;
        while (_entries.Count > _capacity) Remove(_lru.Last);
        return value;
    }
    public void Remove(TKey key) { if (_entries.TryGetValue(key, out var node)) Remove(node); }
    public void Clear() { while (_lru.Last != null) Remove(_lru.Last); }
    private void Remove(LinkedListNode<(TKey key, TValue value)> node)
    {
        _lru.Remove(node); _entries.Remove(node.Value.key); if (node.Value.value is IDisposable disposable) disposable.Dispose();
    }
    public void Dispose() => Clear();
}
