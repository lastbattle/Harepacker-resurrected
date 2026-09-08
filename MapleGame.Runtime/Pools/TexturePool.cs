using HaCreator.MapSimulator.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using MapleLib.WzLib.Spine;
using MapleLib.WzLib.WzProperties;
using HaSharedLibrary.Util;

namespace HaCreator.MapSimulator.Pools
{
    /// <summary>
    /// Pool of shared textures with TTL-based cleanup.
    /// Textures are automatically disposed when not accessed for a specified duration.
    /// </summary>
    public class TexturePool : ICache<string, Texture2D>, IDisposable
    {
        /// <summary>
        /// Entry storing texture and last access time for TTL tracking
        /// </summary>
        private class TextureEntry
        {
            public Texture2D Texture { get; set; }
            public long LastAccessTicks { get; set; }
            public bool IsDisposed { get; set; }

            public TextureEntry(Texture2D texture)
            {
                Texture = texture;
                LastAccessTicks = DateTime.UtcNow.Ticks;
                IsDisposed = false;
            }

            public void Touch()
            {
                LastAccessTicks = DateTime.UtcNow.Ticks;
            }
        }

        private readonly Dictionary<string, TextureEntry> _texturePool = new Dictionary<string, TextureEntry>();
        private readonly object _lock = new object();
        private readonly List<WzSpineObject> _pendingSpineObjects = new();
        private readonly HashSet<IDisposable> _ownedGraphicsResources = new();
        private readonly Dictionary<WzCanvasProperty, Texture2D> _canvasTextures = new(ReferenceEqualityComparer.Instance);

        // A full WZ path can recur in different source generations. Canvas identity
        // distinguishes those assets and keeps active drawable textures alive.
        public Texture2D GetCanvasTexture(WzCanvasProperty canvas)
        {
            lock (_lock)
                return _canvasTextures.TryGetValue(canvas, out var texture) && !texture.IsDisposed ? texture : null;
        }

        public Texture2D LoadCanvasTexture(WzCanvasProperty canvas, GraphicsDevice device)
        {
            if (canvas == null) return null;
            Texture2D cached = GetCanvasTexture(canvas);
            if (cached != null) return cached;
            using var bitmap = canvas.GetLinkedWzCanvasBitmap();
            if (bitmap == null) return null;
            Texture2D texture = bitmap.ToTexture2D(device);
            AddCanvasTexture(canvas, texture);
            return texture;
        }

        public void AddCanvasTexture(WzCanvasProperty canvas, Texture2D texture)
        {
            lock (_lock)
            {
                if (_canvasTextures.TryGetValue(canvas, out var previous) && !ReferenceEquals(previous, texture))
                {
                    _totalMemoryEstimate -= (long)previous.Width * previous.Height * 4;
                    previous.Dispose();
                }
                else if (ReferenceEquals(previous, texture))
                    return;
                _canvasTextures[canvas] = texture;
                _totalTexturesLoaded++;
                _totalMemoryEstimate += (long)texture.Width * texture.Height * 4;
            }
        }

        /// <summary>Registers a new session-owned animation without tagging the source WZ tree.</summary>
        public void RegisterSpine(WzSpineObject animation)
        {
            if (animation == null) return;
            lock (_lock)
            {
                _pendingSpineObjects.Add(animation);
                _ownedGraphicsResources.Add(animation.spineAnimationItem);
            }
        }

        public void OwnAnimationResource(IDisposable resource)
        {
            OwnGraphicsResource(resource);
        }

        /// <summary>
        /// Owns a graphics resource that must live for the lifetime of this pool,
        /// including resources registered before they receive a cache key.
        /// </summary>
        public void OwnGraphicsResource(IDisposable resource)
        {
            if (resource == null) return;
            lock (_lock) _ownedGraphicsResources.Add(resource);
        }

        public WzSpineObject[] TakeNewSpineObjects()
        {
            lock (_lock)
            {
                var animations = _pendingSpineObjects.ToArray();
                _pendingSpineObjects.Clear();
                return animations;
            }
        }

        // TTL settings
        private readonly long _ttlTicks;
        private long _lastCleanupTicks;
        private readonly long _cleanupIntervalTicks;

        // Statistics
        private int _totalTexturesLoaded = 0;
        private int _totalTexturesEvicted = 0;
        private long _totalMemoryEstimate = 0;

        /// <summary>
        /// Default TTL: 5 minutes
        /// </summary>
        private const int DEFAULT_TTL_SECONDS = 300;

        /// <summary>
        /// Cleanup interval: 30 seconds
        /// </summary>
        private const int CLEANUP_INTERVAL_SECONDS = 30;

        /// <summary>
        /// Maximum textures before forced cleanup
        /// </summary>
        private const int MAX_TEXTURES_BEFORE_CLEANUP = 500;

        public TexturePool() : this(DEFAULT_TTL_SECONDS)
        {
        }

        /// <summary>
        /// Create a texture pool with specified TTL
        /// </summary>
        /// <param name="ttlSeconds">Time-to-live in seconds for unused textures</param>
        public TexturePool(int ttlSeconds)
        {
            _ttlTicks = TimeSpan.FromSeconds(ttlSeconds).Ticks;
            _cleanupIntervalTicks = TimeSpan.FromSeconds(CLEANUP_INTERVAL_SECONDS).Ticks;
            _lastCleanupTicks = DateTime.UtcNow.Ticks;
        }

        /// <summary>
        /// Get the previously loaded texture from the pool.
        /// Updates the last access time for TTL tracking.
        /// </summary>
        /// <param name="wzpath">WZ path key</param>
        /// <returns>The texture if found and not disposed, null otherwise</returns>
        public Texture2D GetTexture(string wzpath)
        {
            lock (_lock)
            {
                if (_texturePool.TryGetValue(wzpath, out TextureEntry entry))
                {
                    if (!entry.IsDisposed && entry.Texture != null && !entry.Texture.IsDisposed)
                    {
                        entry.Touch();
                        return entry.Texture;
                    }
                    // Entry exists but texture is disposed - remove it
                    _texturePool.Remove(wzpath);
                }
            }
            return null;
        }

        /// <summary>
        /// Adds the loaded texture to the cache pool.
        /// </summary>
        /// <param name="wzpath">WZ path key</param>
        /// <param name="texture">Texture to cache</param>
        public void AddTextureToPool(string wzpath, Texture2D texture)
        {
            if (texture == null || texture.IsDisposed)
                return;

            lock (_lock)
            {
                if (!_texturePool.ContainsKey(wzpath))
                {
                    _texturePool[wzpath] = new TextureEntry(texture);
                    _totalTexturesLoaded++;

                    // Estimate memory usage (width * height * 4 bytes per pixel)
                    _totalMemoryEstimate += texture.Width * texture.Height * 4;

                    // Check if cleanup is needed
                    CheckAndPerformCleanup();
                }
            }
        }

        /// <summary>
        /// Performs cleanup if enough time has passed or pool is too large
        /// </summary>
        private void CheckAndPerformCleanup()
        {
            long now = DateTime.UtcNow.Ticks;

            bool shouldCleanup = (now - _lastCleanupTicks) >= _cleanupIntervalTicks ||
                                 _texturePool.Count >= MAX_TEXTURES_BEFORE_CLEANUP;

            if (shouldCleanup)
            {
                PerformTTLCleanup(now);
                _lastCleanupTicks = now;
            }
        }

        /// <summary>
        /// Removes and disposes textures that haven't been accessed within the TTL period
        /// </summary>
        private void PerformTTLCleanup(long now)
        {
            List<string> keysToRemove = new List<string>();

            foreach (var kvp in _texturePool)
            {
                if (kvp.Value.IsDisposed || kvp.Value.Texture == null || kvp.Value.Texture.IsDisposed)
                {
                    keysToRemove.Add(kvp.Key);
                }
                else if ((now - kvp.Value.LastAccessTicks) > _ttlTicks)
                {
                    // Texture hasn't been accessed within TTL - mark for removal
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (string key in keysToRemove)
            {
                if (_texturePool.TryGetValue(key, out TextureEntry entry))
                {
                    if (entry.Texture != null && !entry.Texture.IsDisposed)
                    {
                        // Update memory estimate
                        _totalMemoryEstimate -= entry.Texture.Width * entry.Texture.Height * 4;
                        if (_totalMemoryEstimate < 0) _totalMemoryEstimate = 0;

                        entry.Texture.Dispose();
                        entry.IsDisposed = true;
                        _totalTexturesEvicted++;
                    }
                    _texturePool.Remove(key);
                }
            }
        }

        /// <summary>
        /// Force an immediate cleanup of expired textures
        /// </summary>
        public void ForceCleanup()
        {
            lock (_lock)
            {
                PerformTTLCleanup(DateTime.UtcNow.Ticks);
            }
        }

        /// <summary>
        /// Gets the current number of cached textures
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _texturePool.Count + _canvasTextures.Count;
                }
            }
        }

        /// <summary>
        /// Gets statistics about the texture pool
        /// </summary>
        /// <returns>Tuple of (current count, total loaded, total evicted, estimated memory in MB)</returns>
        public (int CurrentCount, int TotalLoaded, int TotalEvicted, float MemoryMB) GetStatistics()
        {
            lock (_lock)
            {
                return (_texturePool.Count + _canvasTextures.Count, _totalTexturesLoaded, _totalTexturesEvicted,
                        _totalMemoryEstimate / (1024f * 1024f));
            }
        }

        /// <summary>
        /// Gets a formatted string with pool statistics for debugging
        /// </summary>
        public string GetStatisticsString()
        {
            var stats = GetStatistics();
            return $"TexturePool: Count={stats.CurrentCount}, Loaded={stats.TotalLoaded}, Evicted={stats.TotalEvicted}, Memory={stats.MemoryMB:F2}MB";
        }

        /// <summary>
        /// Checks if a texture exists in the pool
        /// </summary>
        public bool ContainsKey(string wzpath)
        {
            lock (_lock)
            {
                if (_texturePool.TryGetValue(wzpath, out TextureEntry entry))
                {
                    return !entry.IsDisposed && entry.Texture != null && !entry.Texture.IsDisposed;
                }
                return false;
            }
        }

        #region ICache<string, Texture2D> Implementation

        /// <summary>
        /// Gets a texture from the cache (ICache interface)
        /// </summary>
        Texture2D ICache<string, Texture2D>.Get(string key) => GetTexture(key);

        /// <summary>
        /// Adds a texture to the cache (ICache interface)
        /// </summary>
        void ICache<string, Texture2D>.Add(string key, Texture2D value) => AddTextureToPool(key, value);

        /// <summary>
        /// Clears all textures from the pool (IClearablePool interface)
        /// </summary>
        void IClearablePool.Clear() => DisposeAll();

        #endregion

        public void Dispose()
        {
            DisposeAll();
        }

        /// <summary>
        /// Disposes all textures in the pool and clears the pool.
        /// Used for seamless map transitions.
        /// </summary>
        public void DisposeAll()
        {
            lock (_lock)
            {
                foreach (var entry in _texturePool.Values)
                {
                    if (entry.Texture != null && !entry.Texture.IsDisposed)
                    {
                        entry.Texture.Dispose();
                        entry.IsDisposed = true;
                    }
                }
                _texturePool.Clear();
                foreach (Texture2D texture in _canvasTextures.Values) texture.Dispose();
                _canvasTextures.Clear();
                _pendingSpineObjects.Clear();
                foreach (IDisposable resource in _ownedGraphicsResources) resource.Dispose();
                _ownedGraphicsResources.Clear();
                _totalMemoryEstimate = 0;
            }
        }
    }
}
