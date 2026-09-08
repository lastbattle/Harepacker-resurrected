using HaCreator.MapSimulator.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Pools
{
    /// <summary>
    /// Generic object pool for reducing GC allocations.
    /// Based on MapleStory's ZRecyclable pattern.
    /// Thread-safe using ConcurrentBag.
    /// </summary>
    /// <typeparam name="T">Type of object to pool</typeparam>
    public class ObjectPool<T> : IObjectPool<T> where T : class, new()
    {
        private readonly ConcurrentBag<T> _pool = new ConcurrentBag<T>();
        private readonly Func<T> _factory;
        private readonly Action<T> _resetAction;
        private readonly int _maxSize;

        // Statistics
        private int _totalCreated = 0;
        private int _totalReused = 0;

        /// <summary>
        /// Creates an object pool with default factory
        /// </summary>
        /// <param name="initialSize">Number of objects to pre-allocate</param>
        /// <param name="maxSize">Maximum pool size (0 = unlimited)</param>
        /// <param name="resetAction">Action to reset object state when returning to pool</param>
        public ObjectPool(int initialSize = 0, int maxSize = 100, Action<T> resetAction = null)
            : this(() => new T(), initialSize, maxSize, resetAction)
        {
        }

        /// <summary>
        /// Creates an object pool with custom factory
        /// </summary>
        /// <param name="factory">Factory function to create new objects</param>
        /// <param name="initialSize">Number of objects to pre-allocate</param>
        /// <param name="maxSize">Maximum pool size (0 = unlimited)</param>
        /// <param name="resetAction">Action to reset object state when returning to pool</param>
        public ObjectPool(Func<T> factory, int initialSize = 0, int maxSize = 100, Action<T> resetAction = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _maxSize = maxSize;
            _resetAction = resetAction;

            // Pre-allocate initial objects
            for (int i = 0; i < initialSize; i++)
            {
                _pool.Add(_factory());
                _totalCreated++;
            }
        }

        /// <summary>
        /// Get an object from the pool, or create a new one if pool is empty
        /// </summary>
        public T Get()
        {
            if (_pool.TryTake(out T item))
            {
                _totalReused++;
                return item;
            }

            _totalCreated++;
            return _factory();
        }

        /// <summary>
        /// Return an object to the pool
        /// </summary>
        public void Return(T item)
        {
            if (item == null) return;

            // Don't exceed max size
            if (_maxSize > 0 && _pool.Count >= _maxSize)
                return;

            // Reset object state if reset action provided
            _resetAction?.Invoke(item);

            _pool.Add(item);
        }

        /// <summary>
        /// Current number of objects in the pool
        /// </summary>
        public int Count => _pool.Count;

        /// <summary>
        /// Get pool statistics
        /// </summary>
        public (int Created, int Reused, int InPool, float ReuseRate) GetStatistics()
        {
            int total = _totalCreated + _totalReused;
            float reuseRate = total > 0 ? (float)_totalReused / total : 0;
            return (_totalCreated, _totalReused, _pool.Count, reuseRate);
        }

        /// <summary>
        /// Gets a formatted string with pool statistics for debugging
        /// </summary>
        public string GetStatisticsString()
        {
            var stats = GetStatistics();
            return $"ObjectPool<{typeof(T).Name}>: InPool={stats.InPool}, Created={stats.Created}, Reused={stats.Reused}, ReuseRate={stats.ReuseRate:P1}";
        }

        /// <summary>
        /// Clear all objects from the pool
        /// </summary>
        public void Clear()
        {
            while (_pool.TryTake(out _)) { }
        }
    }

    /// <summary>
    /// Pool for List instances to avoid repeated allocations
    /// </summary>
    /// <typeparam name="T">Element type of the list</typeparam>
    public class ListPool<T>
    {
        private static readonly ObjectPool<List<T>> _pool = new ObjectPool<List<T>>(
            factory: () => new List<T>(),
            initialSize: 10,
            maxSize: 50,
            resetAction: list => list.Clear()
        );

        /// <summary>
        /// Get a list from the pool
        /// </summary>
        public static List<T> Get() => _pool.Get();

        /// <summary>
        /// Return a list to the pool
        /// </summary>
        public static void Return(List<T> list) => _pool.Return(list);

        /// <summary>
        /// Get a list, use it, and automatically return it
        /// </summary>
        public static void Use(Action<List<T>> action)
        {
            var list = Get();
            try
            {
                action(list);
            }
            finally
            {
                Return(list);
            }
        }
    }

    /// <summary>
    /// Pool for HashSet instances
    /// </summary>
    /// <typeparam name="T">Element type of the hashset</typeparam>
    public class HashSetPool<T>
    {
        private static readonly ObjectPool<HashSet<T>> _pool = new ObjectPool<HashSet<T>>(
            factory: () => new HashSet<T>(),
            initialSize: 5,
            maxSize: 20,
            resetAction: set => set.Clear()
        );

        public static HashSet<T> Get() => _pool.Get();
        public static void Return(HashSet<T> set) => _pool.Return(set);
    }

    /// <summary>
    /// Pool for StringBuilder instances
    /// </summary>
    public static class StringBuilderPool
    {
        private static readonly ObjectPool<System.Text.StringBuilder> _pool = new ObjectPool<System.Text.StringBuilder>(
            factory: () => new System.Text.StringBuilder(256),
            initialSize: 5,
            maxSize: 20,
            resetAction: sb => sb.Clear()
        );

        public static System.Text.StringBuilder Get() => _pool.Get();
        public static void Return(System.Text.StringBuilder sb) => _pool.Return(sb);

        /// <summary>
        /// Get a StringBuilder, build a string, and return the builder to the pool
        /// </summary>
        public static string Build(Action<System.Text.StringBuilder> builder)
        {
            var sb = Get();
            try
            {
                builder(sb);
                return sb.ToString();
            }
            finally
            {
                Return(sb);
            }
        }
    }

    /// <summary>
    /// Pooled wrapper that auto-returns to pool on dispose
    /// </summary>
    public struct PooledObject<T> : IDisposable where T : class, new()
    {
        private readonly ObjectPool<T> _pool;
        private T _value;
        private bool _disposed;

        public PooledObject(ObjectPool<T> pool)
        {
            _pool = pool;
            _value = pool.Get();
            _disposed = false;
        }

        public T Value => _value;

        public void Dispose()
        {
            if (!_disposed && _value != null)
            {
                _pool.Return(_value);
                _value = null;
                _disposed = true;
            }
        }
    }
}
