using System;

namespace HaCreator.MapSimulator.Core
{
    /// <summary>
    /// Interface for pool statistics tracking.
    /// Provides a common way to monitor pool usage across different pool implementations.
    /// </summary>
    public interface IPoolStatistics
    {
        /// <summary>
        /// Gets the current number of items in the pool
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Gets a formatted string with pool statistics for debugging
        /// </summary>
        string GetStatisticsString();
    }

    /// <summary>
    /// Interface for resource pools that can be cleared.
    /// </summary>
    public interface IClearablePool
    {
        /// <summary>
        /// Clears all items from the pool
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// Interface for object pools that provide Get/Return semantics.
    /// </summary>
    /// <typeparam name="T">Type of object being pooled</typeparam>
    public interface IObjectPool<T> : IPoolStatistics, IClearablePool where T : class
    {
        /// <summary>
        /// Gets an object from the pool, creating a new one if necessary
        /// </summary>
        T Get();

        /// <summary>
        /// Returns an object to the pool
        /// </summary>
        void Return(T item);
    }

    /// <summary>
    /// Interface for keyed caches with optional TTL support.
    /// </summary>
    /// <typeparam name="TKey">Type of cache key</typeparam>
    /// <typeparam name="TValue">Type of cached value</typeparam>
    public interface ICache<TKey, TValue> : IPoolStatistics, IClearablePool
    {
        /// <summary>
        /// Tries to get a cached value by key
        /// </summary>
        TValue Get(TKey key);

        /// <summary>
        /// Adds a value to the cache
        /// </summary>
        void Add(TKey key, TValue value);

        /// <summary>
        /// Checks if a key exists in the cache
        /// </summary>
        bool ContainsKey(TKey key);
    }
}
