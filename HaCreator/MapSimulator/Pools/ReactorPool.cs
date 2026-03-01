using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Info;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HaSharedLibrary.Render.DX;

namespace HaCreator.MapSimulator.Pools
{
    /// <summary>
    /// Reactor state enumeration
    /// </summary>
    public enum ReactorState
    {
        Idle,           // Initial state, waiting for interaction
        Activated,      // Player touched or hit, playing activation animation
        Active,         // In active/triggered state
        Deactivating,   // Returning to idle
        Destroyed,      // Permanently destroyed (one-time reactors)
        Respawning      // Waiting to respawn
    }

    /// <summary>
    /// Reactor activation type
    /// </summary>
    public enum ReactorActivationType
    {
        Touch,          // Activated by player collision
        Hit,            // Activated by attacking
        Skill,          // Activated by specific skill
        Quest,          // Activated when quest conditions met
        Time,           // Activated after time delay
        Item            // Activated by item use
    }

    /// <summary>
    /// Reactor spawn point for respawning
    /// </summary>
    public class ReactorSpawnPoint
    {
        public int SpawnId { get; set; }
        public string ReactorId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public bool Flip { get; set; }
        public string Name { get; set; }
        public int RespawnTimeMs { get; set; }

        // Runtime state
        public bool IsActive { get; set; }
        public int DestroyTime { get; set; }
        public int NextSpawnTime { get; set; }
        public ReactorItem CurrentReactor { get; set; }
    }

    /// <summary>
    /// Runtime reactor data for tracking state
    /// </summary>
    public class ReactorRuntimeData
    {
        public int PoolId { get; set; }
        public ReactorState State { get; set; }
        public int StateFrame { get; set; }
        public int StateStartTime { get; set; }
        public int HitCount { get; set; }
        public int RequiredHits { get; set; }
        public float Alpha { get; set; } = 1f;
        public ReactorActivationType ActivationType { get; set; }
        public int ActivatingPlayerId { get; set; }
        public bool CanRespawn { get; set; } = true;
    }

    /// <summary>
    /// Reactor Pool System - Manages reactors, spawning, and touch/skill detection
    /// Based on CReactorPool from MapleStory client
    /// </summary>
    public class ReactorPool
    {
        #region Constants
        private const int DEFAULT_RESPAWN_TIME = 60000;      // 1 minute default respawn
        private const float TOUCH_DETECTION_RANGE = 30f;     // Range for touch activation
        private const float SKILL_DETECTION_RANGE = 100f;    // Range for skill activation
        private const int ACTIVATION_ANIMATION_TIME = 500;   // Animation time for activation
        #endregion

        #region Collections
        private ReactorItem[] _reactors;
        private readonly Dictionary<int, ReactorRuntimeData> _reactorData = new Dictionary<int, ReactorRuntimeData>();
        private readonly Dictionary<string, List<int>> _reactorsByName = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<ReactorSpawnPoint> _spawnPoints = new List<ReactorSpawnPoint>();
        #endregion

        #region State
        private int _nextPoolId = 1;
        private int _lastUpdateTick = 0;
        private bool _respawnEnabled = true;

        // Callbacks
        private Action<ReactorItem, int> _onReactorActivated;
        private Action<ReactorItem, int> _onReactorDestroyed;
        private Action<ReactorItem> _onReactorSpawned;
        private Action<ReactorItem, int> _onReactorTouched;  // (reactor, playerId)
        private Action<ReactorItem, int> _onReactorHit;      // (reactor, playerId)

        // Factory for creating new reactor items
        private Func<ReactorSpawnPoint, GraphicsDevice, ReactorItem> _reactorFactory;
        private GraphicsDevice _graphicsDevice;
        #endregion

        #region Public Properties
        public int ReactorCount => _reactors?.Length ?? 0;
        public int ActiveReactorCount => _reactorData.Count(kvp => kvp.Value.State != ReactorState.Destroyed && kvp.Value.State != ReactorState.Respawning);
        public IReadOnlyList<ReactorItem> Reactors => _reactors;
        public bool RespawnEnabled { get => _respawnEnabled; set => _respawnEnabled = value; }
        #endregion

        #region Events
        public void SetOnReactorActivated(Action<ReactorItem, int> callback) => _onReactorActivated = callback;
        public void SetOnReactorDestroyed(Action<ReactorItem, int> callback) => _onReactorDestroyed = callback;
        public void SetOnReactorSpawned(Action<ReactorItem> callback) => _onReactorSpawned = callback;
        public void SetOnReactorTouched(Action<ReactorItem, int> callback) => _onReactorTouched = callback;
        public void SetOnReactorHit(Action<ReactorItem, int> callback) => _onReactorHit = callback;
        #endregion

        #region Initialization
        /// <summary>
        /// Initialize the reactor pool with existing reactors from map load
        /// </summary>
        public void Initialize(ReactorItem[] reactors, GraphicsDevice graphicsDevice = null)
        {
            Clear();

            _graphicsDevice = graphicsDevice;

            if (reactors == null || reactors.Length == 0)
            {
                _reactors = Array.Empty<ReactorItem>();
                return;
            }

            _reactors = reactors;

            // Build runtime data and spawn points
            for (int i = 0; i < _reactors.Length; i++)
            {
                var reactor = _reactors[i];
                if (reactor?.ReactorInstance == null)
                    continue;

                int poolId = _nextPoolId++;
                var instance = reactor.ReactorInstance;

                // Create runtime data
                var data = new ReactorRuntimeData
                {
                    PoolId = poolId,
                    State = ReactorState.Idle,
                    StateFrame = 0,
                    StateStartTime = 0,
                    HitCount = 0,
                    RequiredHits = 1, // Default, could be loaded from reactor info
                    Alpha = 1f,
                    ActivationType = ReactorActivationType.Touch, // Default
                    CanRespawn = true
                };
                _reactorData[i] = data;

                // Add to name lookup
                if (!string.IsNullOrEmpty(instance.Name))
                {
                    if (!_reactorsByName.TryGetValue(instance.Name, out var indices))
                    {
                        indices = new List<int>();
                        _reactorsByName[instance.Name] = indices;
                    }
                    indices.Add(i);
                }

                // Create spawn point
                var spawnPoint = new ReactorSpawnPoint
                {
                    SpawnId = i,
                    ReactorId = instance.ReactorInfo?.ID ?? "0",
                    X = instance.X,
                    Y = instance.Y,
                    Flip = instance.Flip,
                    Name = instance.Name,
                    RespawnTimeMs = instance.ReactorTime > 0 ? instance.ReactorTime : DEFAULT_RESPAWN_TIME,
                    IsActive = true,
                    CurrentReactor = reactor
                };
                _spawnPoints.Add(spawnPoint);
            }
        }

        /// <summary>
        /// Set the reactor factory for spawning new reactors
        /// </summary>
        public void SetReactorFactory(Func<ReactorSpawnPoint, GraphicsDevice, ReactorItem> factory)
        {
            _reactorFactory = factory;
        }

        public void Clear()
        {
            _reactors = null;
            _reactorData.Clear();
            _reactorsByName.Clear();
            _spawnPoints.Clear();
            _nextPoolId = 1;
        }
        #endregion

        #region Reactor Lookup
        /// <summary>
        /// Get reactor by index
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReactorItem GetReactor(int index)
        {
            if (_reactors == null || index < 0 || index >= _reactors.Length)
                return null;
            return _reactors[index];
        }

        /// <summary>
        /// Get reactors by name
        /// </summary>
        public IEnumerable<ReactorItem> GetReactorsByName(string name)
        {
            if (string.IsNullOrEmpty(name) || !_reactorsByName.TryGetValue(name, out var indices))
                yield break;

            foreach (var index in indices)
            {
                var reactor = GetReactor(index);
                if (reactor != null)
                    yield return reactor;
            }
        }

        /// <summary>
        /// Get reactor runtime data
        /// </summary>
        public ReactorRuntimeData GetReactorData(int index)
        {
            return _reactorData.TryGetValue(index, out var data) ? data : null;
        }

        /// <summary>
        /// Find reactor at position
        /// </summary>
        public (ReactorItem reactor, int index)? FindReactorAtPosition(float x, float y, float range = 40f)
        {
            if (_reactors == null)
                return null;

            float rangeSq = range * range;

            for (int i = 0; i < _reactors.Length; i++)
            {
                var reactor = _reactors[i];
                if (reactor?.ReactorInstance == null)
                    continue;

                // Check if reactor is active
                var data = GetReactorData(i);
                if (data?.State == ReactorState.Destroyed || data?.State == ReactorState.Respawning)
                    continue;

                float dx = reactor.ReactorInstance.X - x;
                float dy = reactor.ReactorInstance.Y - y;

                if (dx * dx + dy * dy <= rangeSq)
                    return (reactor, i);
            }

            return null;
        }
        #endregion

        #region Touch Detection (CReactorPool functions)
        /// <summary>
        /// Find reactors that can be touched/activated by player.
        /// Based on CReactorPool::FindTouchReactorAroundLocalUser from MapleStory client.
        /// </summary>
        /// <param name="playerX">Player X position</param>
        /// <param name="playerY">Player Y position</param>
        /// <param name="playerWidth">Player width (default 40)</param>
        /// <param name="playerHeight">Player height (default 60)</param>
        /// <returns>List of touch-able reactors in range</returns>
        public List<(ReactorItem reactor, int index)> FindTouchReactorAroundLocalUser(
            float playerX, float playerY,
            int playerWidth = 40, int playerHeight = 60)
        {
            var results = new List<(ReactorItem reactor, int index)>();

            if (_reactors == null)
                return results;

            // Create player hitbox (centered on X, Y at feet)
            var playerRect = new Rectangle(
                (int)(playerX - playerWidth / 2),
                (int)(playerY - playerHeight),
                playerWidth,
                playerHeight);

            for (int i = 0; i < _reactors.Length; i++)
            {
                var reactor = _reactors[i];
                if (reactor?.ReactorInstance == null)
                    continue;

                // Check if reactor is in touchable state
                var data = GetReactorData(i);
                if (data == null || data.State != ReactorState.Idle)
                    continue;

                // Only touch-type reactors
                if (data.ActivationType != ReactorActivationType.Touch)
                    continue;

                // Get reactor hitbox
                var instance = reactor.ReactorInstance;
                int reactorWidth = instance.Width;
                int reactorHeight = instance.Height;

                var reactorRect = new Rectangle(
                    instance.X - instance.Origin.X,
                    instance.Y - instance.Origin.Y,
                    reactorWidth,
                    reactorHeight);

                // Check intersection
                if (playerRect.Intersects(reactorRect))
                {
                    results.Add((reactor, i));
                }
            }

            return results;
        }

        /// <summary>
        /// Find skill-activated reactors in range.
        /// Based on CReactorPool::FindSkillReactor from MapleStory client.
        /// </summary>
        /// <param name="skillX">Skill X position</param>
        /// <param name="skillY">Skill Y position</param>
        /// <param name="skillRange">Skill effect range</param>
        /// <param name="skillId">Skill ID (for skill-specific reactors)</param>
        /// <returns>List of skill-activatable reactors in range</returns>
        public List<(ReactorItem reactor, int index)> FindSkillReactor(
            float skillX, float skillY,
            float skillRange,
            int skillId = 0)
        {
            var results = new List<(ReactorItem reactor, int index)>();

            if (_reactors == null)
                return results;

            float rangeSq = skillRange * skillRange;

            for (int i = 0; i < _reactors.Length; i++)
            {
                var reactor = _reactors[i];
                if (reactor?.ReactorInstance == null)
                    continue;

                // Check if reactor is in hittable state
                var data = GetReactorData(i);
                if (data == null)
                    continue;

                // Skip destroyed/respawning
                if (data.State == ReactorState.Destroyed || data.State == ReactorState.Respawning)
                    continue;

                // Check if this reactor is skill-activated or hit-activated
                if (data.ActivationType != ReactorActivationType.Skill &&
                    data.ActivationType != ReactorActivationType.Hit)
                    continue;

                var instance = reactor.ReactorInstance;
                float dx = instance.X - skillX;
                float dy = instance.Y - skillY;

                if (dx * dx + dy * dy <= rangeSq)
                {
                    results.Add((reactor, i));
                }
            }

            return results;
        }
        #endregion

        #region Reactor State Management
        /// <summary>
        /// Activate a reactor (touch or hit)
        /// </summary>
        /// <param name="index">Reactor index</param>
        /// <param name="playerId">Player who activated</param>
        /// <param name="currentTick">Current game tick</param>
        /// <param name="activationType">How it was activated</param>
        public void ActivateReactor(int index, int playerId, int currentTick, ReactorActivationType activationType = ReactorActivationType.Touch)
        {
            var reactor = GetReactor(index);
            var data = GetReactorData(index);

            if (reactor == null || data == null)
                return;

            if (data.State != ReactorState.Idle)
                return;

            data.State = ReactorState.Activated;
            data.StateStartTime = currentTick;
            data.StateFrame = 0;
            data.ActivatingPlayerId = playerId;

            // Invoke appropriate callback
            if (activationType == ReactorActivationType.Touch)
                _onReactorTouched?.Invoke(reactor, playerId);
            else
                _onReactorHit?.Invoke(reactor, playerId);

            _onReactorActivated?.Invoke(reactor, playerId);
        }

        /// <summary>
        /// Hit a reactor (for hit-type reactors that need multiple hits)
        /// </summary>
        public bool HitReactor(int index, int playerId, int currentTick, int damage = 1)
        {
            var reactor = GetReactor(index);
            var data = GetReactorData(index);

            if (reactor == null || data == null)
                return false;

            // Only hit reactors in idle or active state
            if (data.State != ReactorState.Idle && data.State != ReactorState.Active)
                return false;

            data.HitCount += damage;
            _onReactorHit?.Invoke(reactor, playerId);

            // Check if enough hits to activate/progress
            if (data.HitCount >= data.RequiredHits)
            {
                data.HitCount = 0;

                if (data.State == ReactorState.Idle)
                {
                    ActivateReactor(index, playerId, currentTick, ReactorActivationType.Hit);
                }
                else
                {
                    // Progress to next state or destroy
                    DestroyReactor(index, playerId, currentTick);
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// Destroy a reactor
        /// </summary>
        public void DestroyReactor(int index, int playerId, int currentTick)
        {
            var reactor = GetReactor(index);
            var data = GetReactorData(index);

            if (reactor == null || data == null)
                return;

            data.State = ReactorState.Destroyed;
            data.StateStartTime = currentTick;

            // Update spawn point
            if (index < _spawnPoints.Count)
            {
                var spawnPoint = _spawnPoints[index];
                spawnPoint.IsActive = false;
                spawnPoint.DestroyTime = currentTick;
                spawnPoint.NextSpawnTime = currentTick + spawnPoint.RespawnTimeMs;
                spawnPoint.CurrentReactor = null;
            }

            _onReactorDestroyed?.Invoke(reactor, playerId);

            // Schedule respawn if enabled
            if (_respawnEnabled && data.CanRespawn)
            {
                data.State = ReactorState.Respawning;
            }
        }

        /// <summary>
        /// Reset a reactor to idle state
        /// </summary>
        public void ResetReactor(int index, int currentTick)
        {
            var data = GetReactorData(index);
            if (data == null)
                return;

            data.State = ReactorState.Idle;
            data.StateStartTime = currentTick;
            data.StateFrame = 0;
            data.HitCount = 0;
            data.Alpha = 1f;
        }
        #endregion

        #region Reactor Spawning
        /// <summary>
        /// Spawn a reactor at a spawn point
        /// </summary>
        public ReactorItem SpawnReactor(int spawnIndex, int currentTick)
        {
            if (spawnIndex < 0 || spawnIndex >= _spawnPoints.Count)
                return null;

            var spawnPoint = _spawnPoints[spawnIndex];
            if (spawnPoint.IsActive)
                return null;

            // Use factory if available
            ReactorItem newReactor = null;
            if (_reactorFactory != null && _graphicsDevice != null)
            {
                newReactor = _reactorFactory(spawnPoint, _graphicsDevice);
            }

            if (newReactor != null)
            {
                // Update arrays (this is simplified - in practice you'd resize the array)
                spawnPoint.CurrentReactor = newReactor;
                spawnPoint.IsActive = true;

                // Reset runtime data
                if (_reactorData.TryGetValue(spawnIndex, out var data))
                {
                    data.State = ReactorState.Idle;
                    data.StateStartTime = currentTick;
                    data.StateFrame = 0;
                    data.HitCount = 0;
                    data.Alpha = 1f;
                }

                _onReactorSpawned?.Invoke(newReactor);
            }

            return newReactor;
        }

        /// <summary>
        /// Spawn reactors at specified positions (for external use)
        /// </summary>
        /// <param name="reactorId">Reactor ID to spawn</param>
        /// <param name="positions">List of (x, y) positions</param>
        /// <param name="currentTick">Current game tick</param>
        /// <returns>List of spawned reactor indices</returns>
        public List<int> SpawnReactorsAtPositions(string reactorId, List<(float x, float y)> positions, int currentTick)
        {
            var spawnedIndices = new List<int>();

            if (positions == null || positions.Count == 0)
                return spawnedIndices;

            foreach (var (x, y) in positions)
            {
                // Create new spawn point
                var spawnPoint = new ReactorSpawnPoint
                {
                    SpawnId = _spawnPoints.Count,
                    ReactorId = reactorId,
                    X = x,
                    Y = y,
                    Flip = false,
                    Name = $"spawned_{reactorId}_{_spawnPoints.Count}",
                    RespawnTimeMs = DEFAULT_RESPAWN_TIME,
                    IsActive = false
                };

                _spawnPoints.Add(spawnPoint);

                // Create runtime data
                var data = new ReactorRuntimeData
                {
                    PoolId = _nextPoolId++,
                    State = ReactorState.Respawning,
                    StateFrame = 0,
                    StateStartTime = currentTick,
                    HitCount = 0,
                    RequiredHits = 1,
                    Alpha = 1f,
                    ActivationType = ReactorActivationType.Touch
                };
                _reactorData[spawnPoint.SpawnId] = data;

                // Schedule spawn
                spawnPoint.NextSpawnTime = currentTick; // Spawn immediately

                spawnedIndices.Add(spawnPoint.SpawnId);
            }

            return spawnedIndices;
        }
        #endregion

        #region Update
        /// <summary>
        /// Update the reactor pool
        /// </summary>
        public void Update(int currentTick, float deltaTime)
        {
            _lastUpdateTick = currentTick;

            // Update reactor states
            foreach (var kvp in _reactorData)
            {
                int index = kvp.Key;
                var data = kvp.Value;

                switch (data.State)
                {
                    case ReactorState.Activated:
                        // Check if activation animation is complete
                        if (currentTick - data.StateStartTime >= ACTIVATION_ANIMATION_TIME)
                        {
                            data.State = ReactorState.Active;
                            data.StateStartTime = currentTick;
                        }
                        break;

                    case ReactorState.Deactivating:
                        // Fade out
                        data.Alpha = Math.Max(0, data.Alpha - deltaTime * 2);
                        if (data.Alpha <= 0)
                        {
                            data.State = ReactorState.Destroyed;
                            data.StateStartTime = currentTick;
                        }
                        break;
                }
            }

            // Process respawns
            if (_respawnEnabled)
            {
                for (int i = 0; i < _spawnPoints.Count; i++)
                {
                    var spawnPoint = _spawnPoints[i];
                    if (spawnPoint.IsActive)
                        continue;

                    // Check if ready to respawn
                    if (currentTick >= spawnPoint.NextSpawnTime)
                    {
                        // Get or reset reactor data
                        if (_reactorData.TryGetValue(i, out var data) && data.CanRespawn)
                        {
                            data.State = ReactorState.Idle;
                            data.StateStartTime = currentTick;
                            data.StateFrame = 0;
                            data.HitCount = 0;
                            data.Alpha = 1f;
                            spawnPoint.IsActive = true;
                        }
                    }
                }
            }
        }
        #endregion

        #region Render Helpers
        /// <summary>
        /// Get reactors that should be rendered
        /// </summary>
        public IEnumerable<(ReactorItem reactor, int index, float alpha)> GetRenderableReactors()
        {
            if (_reactors == null)
                yield break;

            for (int i = 0; i < _reactors.Length; i++)
            {
                var reactor = _reactors[i];
                if (reactor == null)
                    continue;

                var data = GetReactorData(i);
                if (data == null)
                {
                    yield return (reactor, i, 1f);
                    continue;
                }

                // Skip destroyed/respawning
                if (data.State == ReactorState.Destroyed && data.Alpha <= 0)
                    continue;
                if (data.State == ReactorState.Respawning)
                    continue;

                yield return (reactor, i, data.Alpha);
            }
        }

        /// <summary>
        /// Get current animation state for a reactor
        /// </summary>
        public (int state, int frame) GetReactorAnimationState(int index)
        {
            var data = GetReactorData(index);
            if (data == null)
                return (0, 0);

            int state = data.State switch
            {
                ReactorState.Idle => 0,
                ReactorState.Activated => 1,
                ReactorState.Active => 2,
                ReactorState.Deactivating => 3,
                ReactorState.Destroyed => 4,
                _ => 0
            };

            return (state, data.StateFrame);
        }
        #endregion

        #region Statistics
        public ReactorPoolStats GetStats()
        {
            int idleCount = 0;
            int activeCount = 0;
            int destroyedCount = 0;
            int respawningCount = 0;

            foreach (var data in _reactorData.Values)
            {
                switch (data.State)
                {
                    case ReactorState.Idle:
                        idleCount++;
                        break;
                    case ReactorState.Activated:
                    case ReactorState.Active:
                        activeCount++;
                        break;
                    case ReactorState.Destroyed:
                        destroyedCount++;
                        break;
                    case ReactorState.Respawning:
                        respawningCount++;
                        break;
                }
            }

            return new ReactorPoolStats
            {
                TotalReactors = _reactors?.Length ?? 0,
                TotalSpawnPoints = _spawnPoints.Count,
                IdleReactors = idleCount,
                ActiveReactors = activeCount,
                DestroyedReactors = destroyedCount,
                RespawningReactors = respawningCount
            };
        }
        #endregion
    }

    /// <summary>
    /// Statistics about the reactor pool
    /// </summary>
    public struct ReactorPoolStats
    {
        public int TotalReactors;
        public int TotalSpawnPoints;
        public int IdleReactors;
        public int ActiveReactors;
        public int DestroyedReactors;
        public int RespawningReactors;
    }
}
