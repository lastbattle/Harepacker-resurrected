using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Pools;

namespace HaCreator.MapSimulator.Core
{
    /// <summary>
    /// Captures the state of a single mob for persistence across map transitions.
    /// </summary>
    public struct MobState
    {
        // Identification
        public int SpawnIndex;           // Index in the original mob array (for matching on restore)
        public string MobId;             // Mob type ID

        // Position
        public float X;
        public float Y;

        // Movement state
        public MobMoveDirection MoveDirection;
        public MobMoveType MoveType;
        public bool FlipX;

        // Velocities
        public float VelocityX;
        public float VelocityY;

        // Jump state
        public MobJumpState JumpState;

        // Flying mob parameters
        public float CosY;
        public float SrcY;

        // Combat state
        public int CurrentHp;
        public int MaxHp;
        public MobAIState AIState;
        public bool IsDead;
        public bool IsRemoved;

        // Timing
        public int StateStartTime;
        public int LastDamageTime;
    }

    /// <summary>
    /// Stores the complete state of a map for persistence.
    /// </summary>
    public class MapState
    {
        public int MapId { get; set; }
        public int SaveTime { get; set; }
        public List<MobState> MobStates { get; set; } = new List<MobState>();

        // Optional: could add NPC, reactor, and other entity states here
    }

    /// <summary>
    /// Caches map states to maintain entity positions across map transitions.
    /// When leaving a map, the state is saved. When returning, it's restored.
    /// </summary>
    public class MapStateCache
    {
        private readonly Dictionary<int, MapState> _cachedStates = new Dictionary<int, MapState>();

        /// <summary>
        /// Maximum number of maps to keep in cache (LRU eviction)
        /// </summary>
        public int MaxCachedMaps { get; set; } = 10;

        /// <summary>
        /// How long cached states remain valid (in milliseconds). 0 = no expiry.
        /// Default: 5 minutes
        /// </summary>
        public int StateExpiryMs { get; set; } = 300000;

        /// <summary>
        /// Whether to preserve dead mobs (if false, dead mobs respawn fresh)
        /// </summary>
        public bool PreserveDeadMobs { get; set; } = false;

        private readonly Queue<int> _accessOrder = new Queue<int>();

        /// <summary>
        /// Save the current state of a map before leaving.
        /// </summary>
        /// <param name="mapId">Map ID to save</param>
        /// <param name="mobs">Array of mobs currently on the map</param>
        /// <param name="currentTick">Current game tick for timing</param>
        public void SaveMapState(int mapId, MobItem[] mobs, int currentTick)
        {
            if (mobs == null || mobs.Length == 0)
                return;

            var mapState = new MapState
            {
                MapId = mapId,
                SaveTime = currentTick
            };

            for (int i = 0; i < mobs.Length; i++)
            {
                var mob = mobs[i];
                if (mob == null)
                    continue;

                // Skip removed mobs unless preserving dead mobs
                if (mob.AI?.State == MobAIState.Removed)
                    continue;

                // Skip dead mobs if not preserving them
                if (!PreserveDeadMobs && mob.AI?.IsDead == true)
                    continue;

                var mobState = CaptureMobState(mob, i);
                mapState.MobStates.Add(mobState);
            }

            // Only cache if we have mobs to save
            if (mapState.MobStates.Count > 0)
            {
                _cachedStates[mapId] = mapState;
                UpdateAccessOrder(mapId);
                EnforceCacheLimit();
            }
        }

        /// <summary>
        /// Capture the current state of a mob.
        /// </summary>
        private MobState CaptureMobState(MobItem mob, int spawnIndex)
        {
            var movement = mob.MovementInfo;
            var ai = mob.AI;

            return new MobState
            {
                SpawnIndex = spawnIndex,
                MobId = mob.MobInstance?.MobInfo?.ID ?? "0",

                // Position
                X = movement?.X ?? mob.MobInstance?.X ?? 0,
                Y = movement?.Y ?? mob.MobInstance?.Y ?? 0,

                // Movement
                MoveDirection = movement?.MoveDirection ?? MobMoveDirection.Left,
                MoveType = movement?.MoveType ?? MobMoveType.Stand,
                FlipX = movement?.FlipX ?? false,

                // Velocities
                VelocityX = movement?.VelocityX ?? 0,
                VelocityY = movement?.VelocityY ?? 0,

                // Jump state
                JumpState = movement?.JumpState ?? MobJumpState.None,

                // Flying
                CosY = movement?.CosY ?? 0,
                SrcY = movement?.SrcY ?? 0,

                // Combat
                CurrentHp = ai?.CurrentHp ?? 0,
                MaxHp = ai?.MaxHp ?? 1,
                AIState = ai?.State ?? MobAIState.Idle,
                IsDead = ai?.IsDead ?? false,
                IsRemoved = ai?.State == MobAIState.Removed
            };
        }

        /// <summary>
        /// Check if a map has cached state available.
        /// </summary>
        /// <param name="mapId">Map ID to check</param>
        /// <param name="currentTick">Current tick for expiry check</param>
        /// <returns>True if valid cached state exists</returns>
        public bool HasCachedState(int mapId, int currentTick)
        {
            if (!_cachedStates.TryGetValue(mapId, out var state))
                return false;

            // Check expiry
            if (StateExpiryMs > 0 && (currentTick - state.SaveTime) > StateExpiryMs)
            {
                _cachedStates.Remove(mapId);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Restore mob states after loading a map.
        /// </summary>
        /// <param name="mapId">Map ID being loaded</param>
        /// <param name="mobs">Freshly loaded mob array</param>
        /// <param name="currentTick">Current game tick</param>
        /// <returns>True if state was restored</returns>
        public bool RestoreMapState(int mapId, MobItem[] mobs, int currentTick)
        {
            if (!HasCachedState(mapId, currentTick))
                return false;

            var mapState = _cachedStates[mapId];
            int restoredCount = 0;

            foreach (var savedState in mapState.MobStates)
            {
                // Find matching mob by spawn index and ID
                if (savedState.SpawnIndex >= 0 && savedState.SpawnIndex < mobs.Length)
                {
                    var mob = mobs[savedState.SpawnIndex];
                    if (mob != null && mob.MobInstance?.MobInfo?.ID == savedState.MobId)
                    {
                        RestoreMobState(mob, savedState, currentTick);
                        restoredCount++;
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[MapStateCache] Restored {restoredCount}/{mapState.MobStates.Count} mobs for map {mapId}");

            // Update access order
            UpdateAccessOrder(mapId);

            return restoredCount > 0;
        }

        /// <summary>
        /// Restore a single mob's state from saved data.
        /// </summary>
        private void RestoreMobState(MobItem mob, MobState savedState, int currentTick)
        {
            var movement = mob.MovementInfo;
            var ai = mob.AI;

            if (movement != null)
            {
                // Restore position
                movement.X = savedState.X;
                movement.Y = savedState.Y;

                // Restore movement state
                movement.MoveDirection = savedState.MoveDirection;
                movement.FlipX = savedState.FlipX;

                // Restore velocities
                movement.VelocityX = savedState.VelocityX;
                movement.VelocityY = savedState.VelocityY;

                // Restore jump state
                movement.JumpState = savedState.JumpState;

                // Restore flying parameters
                movement.CosY = savedState.CosY;
                movement.SrcY = savedState.SrcY;
            }

            if (ai != null)
            {
                // Restore HP
                ai.RestoreHp(savedState.CurrentHp);

                // Handle dead mobs
                if (savedState.IsDead && PreserveDeadMobs)
                {
                    ai.SetState(MobAIState.Death, currentTick);
                }
            }
        }

        /// <summary>
        /// Clear cached state for a specific map.
        /// </summary>
        public void ClearMapState(int mapId)
        {
            _cachedStates.Remove(mapId);
        }

        /// <summary>
        /// Clear all cached states.
        /// </summary>
        public void ClearAll()
        {
            _cachedStates.Clear();
            _accessOrder.Clear();
        }

        /// <summary>
        /// Get the number of maps currently cached.
        /// </summary>
        public int CachedMapCount => _cachedStates.Count;

        private void UpdateAccessOrder(int mapId)
        {
            // Simple LRU tracking - add to end
            // Note: This is O(n) but cache size is small (10 maps)
            var newQueue = new Queue<int>();
            while (_accessOrder.Count > 0)
            {
                var id = _accessOrder.Dequeue();
                if (id != mapId)
                    newQueue.Enqueue(id);
            }
            newQueue.Enqueue(mapId);
            while (newQueue.Count > 0)
                _accessOrder.Enqueue(newQueue.Dequeue());
        }

        private void EnforceCacheLimit()
        {
            while (_cachedStates.Count > MaxCachedMaps && _accessOrder.Count > 0)
            {
                var oldestMapId = _accessOrder.Dequeue();
                _cachedStates.Remove(oldestMapId);
            }
        }
    }
}
