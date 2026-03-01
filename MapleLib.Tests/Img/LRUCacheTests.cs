/*  MapleLib.Tests - Unit tests for MapleLib
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using MapleLib.Img;
using Xunit;

namespace MapleLib.Tests.Img
{
    // Wrapper class for testing (since TValue must be a class)
    public class IntWrapper
    {
        public int Value { get; set; }
        public IntWrapper(int value) => Value = value;
    }

    public class LRUCacheTests : IDisposable
    {
        private LRUCache<string, IntWrapper>? _cache;

        public void Dispose()
        {
            _cache?.Dispose();
        }

        [Fact]
        public void Constructor_ValidCapacity_CreatesCache()
        {
            // Act
            _cache = new LRUCache<string, IntWrapper>(10);

            // Assert
            Assert.Equal(0, _cache.Count);
        }

        [Fact]
        public void Constructor_ZeroCapacity_UsesDefaultCapacity()
        {
            // Act - should use default capacity (100)
            _cache = new LRUCache<string, IntWrapper>(0);

            // Assert
            Assert.Equal(0, _cache.Count);
        }

        [Fact]
        public void Add_SingleItem_IncreasesCount()
        {
            // Arrange
            _cache = new LRUCache<string, IntWrapper>(10);

            // Act
            _cache.Add("key1", new IntWrapper(100));

            // Assert
            Assert.Equal(1, _cache.Count);
        }

        [Fact]
        public void Add_DuplicateKey_UpdatesValue()
        {
            // Arrange
            _cache = new LRUCache<string, IntWrapper>(10);
            _cache.Add("key1", new IntWrapper(100));

            // Act
            _cache.Add("key1", new IntWrapper(200));

            // Assert
            Assert.Equal(1, _cache.Count);
            Assert.True(_cache.TryGet("key1", out var value));
            Assert.Equal(200, value!.Value);
        }

        [Fact]
        public void TryGet_ExistingKey_ReturnsTrue()
        {
            // Arrange
            _cache = new LRUCache<string, IntWrapper>(10);
            _cache.Add("key1", new IntWrapper(100));

            // Act
            bool result = _cache.TryGet("key1", out var value);

            // Assert
            Assert.True(result);
            Assert.Equal(100, value!.Value);
        }

        [Fact]
        public void TryGet_NonExistingKey_ReturnsFalse()
        {
            // Arrange
            _cache = new LRUCache<string, IntWrapper>(10);

            // Act
            bool result = _cache.TryGet("nonexistent", out var value);

            // Assert
            Assert.False(result);
            Assert.Null(value);
        }

        [Fact]
        public void Add_ExceedsCapacity_EvictsLeastRecentlyUsed()
        {
            // Arrange
            _cache = new LRUCache<string, IntWrapper>(3);
            _cache.Add("key1", new IntWrapper(1));
            _cache.Add("key2", new IntWrapper(2));
            _cache.Add("key3", new IntWrapper(3));

            // Act - Add 4th item, should evict key1
            _cache.Add("key4", new IntWrapper(4));

            // Assert
            Assert.Equal(3, _cache.Count);
            Assert.False(_cache.TryGet("key1", out _)); // key1 should be evicted
            Assert.True(_cache.TryGet("key2", out _));
            Assert.True(_cache.TryGet("key3", out _));
            Assert.True(_cache.TryGet("key4", out _));
        }

        [Fact]
        public void TryGet_MovesItemToFront()
        {
            // Arrange
            _cache = new LRUCache<string, IntWrapper>(3);
            _cache.Add("key1", new IntWrapper(1));
            _cache.Add("key2", new IntWrapper(2));
            _cache.Add("key3", new IntWrapper(3));

            // Access key1 to move it to front
            _cache.TryGet("key1", out _);

            // Add new item - should evict key2 (now least recently used)
            _cache.Add("key4", new IntWrapper(4));

            // Assert
            Assert.True(_cache.TryGet("key1", out _)); // key1 should still be there
            Assert.False(_cache.TryGet("key2", out _)); // key2 should be evicted
        }

        [Fact]
        public void Remove_ExistingKey_RemovesAndReturnsTrue()
        {
            // Arrange
            _cache = new LRUCache<string, IntWrapper>(10);
            _cache.Add("key1", new IntWrapper(100));

            // Act
            bool result = _cache.Remove("key1");

            // Assert
            Assert.True(result);
            Assert.Equal(0, _cache.Count);
            Assert.False(_cache.TryGet("key1", out _));
        }

        [Fact]
        public void Remove_NonExistingKey_ReturnsFalse()
        {
            // Arrange
            _cache = new LRUCache<string, IntWrapper>(10);

            // Act
            bool result = _cache.Remove("nonexistent");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Clear_RemovesAllItems()
        {
            // Arrange
            _cache = new LRUCache<string, IntWrapper>(10);
            _cache.Add("key1", new IntWrapper(1));
            _cache.Add("key2", new IntWrapper(2));
            _cache.Add("key3", new IntWrapper(3));

            // Act
            _cache.Clear();

            // Assert
            Assert.Equal(0, _cache.Count);
        }

        [Fact]
        public void ContainsKey_ExistingKey_ReturnsTrue()
        {
            // Arrange
            _cache = new LRUCache<string, IntWrapper>(10);
            _cache.Add("key1", new IntWrapper(100));

            // Act & Assert
            Assert.True(_cache.ContainsKey("key1"));
        }

        [Fact]
        public void ContainsKey_NonExistingKey_ReturnsFalse()
        {
            // Arrange
            _cache = new LRUCache<string, IntWrapper>(10);

            // Act & Assert
            Assert.False(_cache.ContainsKey("nonexistent"));
        }

        [Fact]
        public void IsThreadSafe_ConcurrentAccess_DoesNotThrow()
        {
            // Arrange
            _cache = new LRUCache<string, IntWrapper>(100);
            var tasks = new List<Task>();

            // Act - Multiple threads accessing cache concurrently
            for (int t = 0; t < 10; t++)
            {
                int threadId = t;
                tasks.Add(Task.Run(() =>
                {
                    for (int i = 0; i < 100; i++)
                    {
                        _cache.Add($"key_{threadId}_{i}", new IntWrapper(i));
                        _cache.TryGet($"key_{threadId}_{i}", out _);
                    }
                }));
            }

            // Assert - Should complete without throwing
            Task.WaitAll(tasks.ToArray());
            Assert.True(_cache.Count <= 100); // Should respect capacity
        }

        [Fact]
        public void GetOrAdd_NonExistingKey_AddsValue()
        {
            // Arrange
            _cache = new LRUCache<string, IntWrapper>(10);

            // Act
            var value = _cache.GetOrAdd("key1", key => new IntWrapper(100));

            // Assert
            Assert.Equal(100, value!.Value);
            Assert.True(_cache.ContainsKey("key1"));
        }

        [Fact]
        public void GetOrAdd_ExistingKey_ReturnsExistingValue()
        {
            // Arrange
            _cache = new LRUCache<string, IntWrapper>(10);
            _cache.Add("key1", new IntWrapper(100));

            // Act
            var value = _cache.GetOrAdd("key1", key => new IntWrapper(999));

            // Assert
            Assert.Equal(100, value!.Value); // Should return existing value, not 999
        }

        [Fact]
        public void GetOrAdd_FactoryNotCalledForExisting()
        {
            // Arrange
            _cache = new LRUCache<string, IntWrapper>(10);
            _cache.Add("key1", new IntWrapper(100));
            bool factoryCalled = false;

            // Act
            _cache.GetOrAdd("key1", key =>
            {
                factoryCalled = true;
                return new IntWrapper(999);
            });

            // Assert
            Assert.False(factoryCalled);
        }

        [Fact]
        public void Statistics_TracksHitsAndMisses()
        {
            // Arrange
            _cache = new LRUCache<string, IntWrapper>(10);
            _cache.Add("key1", new IntWrapper(100));

            // Act
            _cache.TryGet("key1", out _); // Hit
            _cache.TryGet("key1", out _); // Hit
            _cache.TryGet("nonexistent", out _); // Miss

            // Assert
            Assert.Equal(2, _cache.HitCount);
            Assert.Equal(1, _cache.MissCount);
        }

        [Fact]
        public void HitRatio_CalculatesCorrectly()
        {
            // Arrange
            _cache = new LRUCache<string, IntWrapper>(10);
            _cache.Add("key1", new IntWrapper(100));

            // Act
            _cache.TryGet("key1", out _); // Hit
            _cache.TryGet("nonexistent", out _); // Miss

            // Assert
            Assert.Equal(0.5, _cache.HitRatio, 2);
        }

        [Fact]
        public void ResetStatistics_ClearsCounters()
        {
            // Arrange
            _cache = new LRUCache<string, IntWrapper>(10);
            _cache.Add("key1", new IntWrapper(100));
            _cache.TryGet("key1", out _);
            _cache.TryGet("nonexistent", out _);

            // Act
            _cache.ResetStatistics();

            // Assert
            Assert.Equal(0, _cache.HitCount);
            Assert.Equal(0, _cache.MissCount);
        }

        [Fact]
        public void GetStatistics_ReturnsStatisticsObject()
        {
            // Arrange
            _cache = new LRUCache<string, IntWrapper>(10);
            _cache.Add("key1", new IntWrapper(100));
            _cache.Add("key2", new IntWrapper(200));

            // Act
            var stats = _cache.GetStatistics();

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(2, stats.ItemCount);
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            // Arrange
            var cache = new LRUCache<string, IntWrapper>(10);
            cache.Add("key1", new IntWrapper(100));

            // Act & Assert (should not throw)
            cache.Dispose();
            cache.Dispose();
        }

        [Fact]
        public void SizeBasedEviction_EvictsWhenSizeExceeded()
        {
            // Arrange - Create cache with size-based eviction
            var cache = new LRUCache<string, IntWrapper>(
                maxSizeBytes: 100, // 100 bytes max
                sizeEstimator: wrapper => 30); // Each item is 30 bytes

            try
            {
                // Act - Add 4 items (30 * 4 = 120 bytes, exceeds 100)
                cache.Add("key1", new IntWrapper(1));
                cache.Add("key2", new IntWrapper(2));
                cache.Add("key3", new IntWrapper(3));
                cache.Add("key4", new IntWrapper(4));

                // Assert - Should have evicted at least one item
                Assert.True(cache.Count < 4);
            }
            finally
            {
                cache.Dispose();
            }
        }
    }
}
