using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapEditor.Instance;
using Microsoft.Xna.Framework;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Core;

namespace HaCreator.MapSimulator.Pools
{
    /// <summary>
    /// Mob spawn point data - tracks where mobs can spawn and their respawn state
    /// </summary>
    public class MobSpawnPoint
    {
        public int SpawnId { get; set; }
        public string MobId { get; set; }           // Mob type ID (string in MapleStory)
        public float X { get; set; }
        public float Y { get; set; }
        public int Rx0Shift { get; set; }
        public int Rx1Shift { get; set; }
        public bool Flip { get; set; }
        public int RespawnTimeMs { get; set; }
        public bool IsBoss { get; set; }

        // Runtime state
        public bool IsActive { get; set; }
        public int DeathTime { get; set; }
        public int NextSpawnTime { get; set; }
        public MobItem CurrentMob { get; set; }
    }

    /// <summary>
    /// Mob Pool System - Manages mob spawning, despawning, and lifecycle
    /// Based on CMobPool from MapleStory client
    /// </summary>
    public class MobPool
    {
        #region Constants
        private const int DEFAULT_RESPAWN_TIME = 7000;      // 7 seconds default respawn
        private const int BOSS_RESPAWN_TIME = 0;            // Bosses don't respawn by default
        private const int DEATH_ANIMATION_TIME = 2000;      // 2 seconds for death animation
        private const int BOSS_ANNOUNCE_DELAY = 500;        // Delay before boss announcement
        #endregion

        #region Collections
        private readonly List<MobItem> _activeMobs = new List<MobItem>();
        private readonly List<MobItem> _dyingMobs = new List<MobItem>();      // Mobs playing death animation
        private readonly List<MobItem> _deadMobs = new List<MobItem>();       // Mobs ready for cleanup/respawn
        private readonly Dictionary<int, MobItem> _mobById = new Dictionary<int, MobItem>();
        private readonly List<MobSpawnPoint> _spawnPoints = new List<MobSpawnPoint>();
        private readonly Queue<string> _bossAnnouncements = new Queue<string>();
        #endregion

        #region State
        private int _nextMobId = 1;
        private bool _respawnEnabled = true;
        private bool _bossSpawnEnabled = true;
        private int _lastUpdateTick = 0;
        private Action<MobItem> _onMobSpawned;
        private Action<MobItem> _onMobDied;
        private Action<MobItem> _onMobRemoved;
        private Action<string> _onBossAnnouncement;
        #endregion

        #region Public Properties
        public int ActiveMobCount => _activeMobs.Count;
        public int DyingMobCount => _dyingMobs.Count;
        public int TotalMobCount => _activeMobs.Count + _dyingMobs.Count;
        public int SpawnPointCount => _spawnPoints.Count;
        public bool RespawnEnabled { get => _respawnEnabled; set => _respawnEnabled = value; }
        public bool BossSpawnEnabled { get => _bossSpawnEnabled; set => _bossSpawnEnabled = value; }
        public IReadOnlyList<MobItem> ActiveMobs => _activeMobs;
        public IReadOnlyList<MobItem> DyingMobs => _dyingMobs;
        #endregion

        #region Events
        public void SetOnMobSpawned(Action<MobItem> callback) => _onMobSpawned = callback;
        public void SetOnMobDied(Action<MobItem> callback) => _onMobDied = callback;
        public void SetOnMobRemoved(Action<MobItem> callback) => _onMobRemoved = callback;
        public void SetOnBossAnnouncement(Action<string> callback) => _onBossAnnouncement = callback;
        #endregion

        #region Initialization
        /// <summary>
        /// Initialize the mob pool with existing mobs from map load
        /// </summary>
        public void Initialize(MobItem[] existingMobs)
        {
            Clear();

            if (existingMobs == null || existingMobs.Length == 0)
                return;

            foreach (var mob in existingMobs)
            {
                if (mob == null) continue;

                // Assign unique ID
                int mobId = _nextMobId++;
                mob.PoolId = mobId;
                _mobById[mobId] = mob;
                _activeMobs.Add(mob);

                // Create spawn point for respawning
                var spawnPoint = CreateSpawnPointFromMob(mob, mobId);
                _spawnPoints.Add(spawnPoint);
                spawnPoint.CurrentMob = mob;
                spawnPoint.IsActive = true;
            }
        }

        private MobSpawnPoint CreateSpawnPointFromMob(MobItem mob, int spawnId)
        {
            var instance = mob.MobInstance;
            var mobData = instance?.MobInfo?.MobData;

            return new MobSpawnPoint
            {
                SpawnId = spawnId,
                MobId = instance?.MobInfo?.ID ?? "0",
                X = mob.MovementInfo?.SpawnX ?? instance?.X ?? 0,
                Y = mob.MovementInfo?.SpawnY ?? instance?.Y ?? 0,
                Rx0Shift = instance?.rx0Shift ?? 0,
                Rx1Shift = instance?.rx1Shift ?? 0,
                Flip = instance?.Flip == true,
                RespawnTimeMs = instance?.MobTime ?? DEFAULT_RESPAWN_TIME,
                IsBoss = mobData?.IsBoss ?? false,
                IsActive = true
            };
        }

        public void Clear()
        {
            _activeMobs.Clear();
            _dyingMobs.Clear();
            _deadMobs.Clear();
            _mobById.Clear();
            _spawnPoints.Clear();
            _bossAnnouncements.Clear();
            _nextMobId = 1;
        }
        #endregion

        #region Mob Lookup
        /// <summary>
        /// Get mob by its pool ID (efficient O(1) lookup)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MobItem GetMob(int mobId)
        {
            return _mobById.TryGetValue(mobId, out var mob) ? mob : null;
        }

        /// <summary>
        /// Get all mobs of a specific type
        /// </summary>
        public IEnumerable<MobItem> GetMobsByType(string mobTypeId)
        {
            return _activeMobs.Where(m => m.MobInstance?.MobInfo?.ID == mobTypeId);
        }

        /// <summary>
        /// Get mobs within a radius of a position
        /// </summary>
        public IEnumerable<MobItem> GetMobsInRadius(float x, float y, float radius)
        {
            float radiusSq = radius * radius;
            return _activeMobs.Where(m =>
            {
                if (m.MovementInfo == null) return false;
                float dx = m.MovementInfo.X - x;
                float dy = m.MovementInfo.Y - y;
                return (dx * dx + dy * dy) <= radiusSq;
            });
        }

        /// <summary>
        /// Get the closest mob to a position
        /// </summary>
        public MobItem GetClosestMob(float x, float y, float maxRadius = float.MaxValue)
        {
            MobItem closest = null;
            float closestDistSq = maxRadius * maxRadius;

            foreach (var mob in _activeMobs)
            {
                if (mob.MovementInfo == null) continue;
                float dx = mob.MovementInfo.X - x;
                float dy = mob.MovementInfo.Y - y;
                float distSq = dx * dx + dy * dy;
                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closest = mob;
                }
            }

            return closest;
        }

        /// <summary>
        /// Check if any boss mobs are active
        /// </summary>
        public bool HasActiveBoss()
        {
            return _activeMobs.Any(m => m.AI?.IsBoss == true);
        }

        /// <summary>
        /// Get all active boss mobs
        /// </summary>
        public IEnumerable<MobItem> GetActiveBosses()
        {
            return _activeMobs.Where(m => m.AI?.IsBoss == true);
        }
        #endregion

        #region Mob Lifecycle
        /// <summary>
        /// Kill a mob - starts death animation and schedules for removal
        /// </summary>
        public void KillMob(MobItem mob, MobDeathType deathType = MobDeathType.Killed)
        {
            if (mob == null || !_activeMobs.Contains(mob))
                return;

            // Move to dying list
            _activeMobs.Remove(mob);
            _dyingMobs.Add(mob);

            // Trigger AI death state
            if (mob.AI != null)
            {
                mob.AI.Kill(_lastUpdateTick, deathType);
            }

            // Find spawn point and mark death time
            var spawnPoint = _spawnPoints.FirstOrDefault(sp => sp.CurrentMob == mob);
            if (spawnPoint != null)
            {
                spawnPoint.IsActive = false;
                spawnPoint.DeathTime = _lastUpdateTick;
                spawnPoint.NextSpawnTime = _lastUpdateTick + DEATH_ANIMATION_TIME + spawnPoint.RespawnTimeMs;
                spawnPoint.CurrentMob = null;
            }

            _onMobDied?.Invoke(mob);
        }

        /// <summary>
        /// Kill all mobs (map clear / GM command)
        /// </summary>
        public void KillAllMobs(MobDeathType deathType = MobDeathType.Killed)
        {
            var mobsToKill = _activeMobs.ToList();
            foreach (var mob in mobsToKill)
            {
                KillMob(mob, deathType);
            }
        }

        /// <summary>
        /// Remove a dead mob completely (after death animation)
        /// </summary>
        private void RemoveMob(MobItem mob)
        {
            _dyingMobs.Remove(mob);
            _deadMobs.Remove(mob);
            _mobById.Remove(mob.PoolId);

            // Notify listeners so they can clean up references (e.g., MapSimulator nulls array entry)
            _onMobRemoved?.Invoke(mob);
        }

        /// <summary>
        /// Spawn a mob at a spawn point
        /// </summary>
        private MobItem SpawnMobAtPoint(MobSpawnPoint spawnPoint, Func<MobSpawnPoint, MobItem> mobFactory)
        {
            if (spawnPoint.IsBoss && !_bossSpawnEnabled)
                return null;

            var newMob = mobFactory?.Invoke(spawnPoint);
            if (newMob == null)
                return null;

            // Assign pool ID
            int mobId = _nextMobId++;
            newMob.PoolId = mobId;
            _mobById[mobId] = newMob;
            _activeMobs.Add(newMob);

            spawnPoint.CurrentMob = newMob;
            spawnPoint.IsActive = true;

            // Boss announcement
            if (spawnPoint.IsBoss)
            {
                var mobName = newMob.MobInstance?.MobInfo?.Name ?? "Boss";
                _bossAnnouncements.Enqueue($"{mobName} has appeared!");
            }

            _onMobSpawned?.Invoke(newMob);
            return newMob;
        }
        #endregion

        #region Update
        /// <summary>
        /// Update the mob pool - handles death animations, cleanup, and respawns
        /// </summary>
        /// <param name="currentTick">Current game tick (Environment.TickCount)</param>
        /// <param name="mobFactory">Factory function to create new mobs at spawn points</param>
        public void Update(int currentTick, Func<MobSpawnPoint, MobItem> mobFactory = null)
        {
            _lastUpdateTick = currentTick;

            // Check for mobs that died from damage and move them to dying list
            for (int i = _activeMobs.Count - 1; i >= 0; i--)
            {
                var mob = _activeMobs[i];
                if (mob?.AI != null && mob.AI.IsDead)
                {
                    // Move to dying list
                    _activeMobs.RemoveAt(i);
                    _dyingMobs.Add(mob);

                    // Update spawn point
                    var spawnPoint = _spawnPoints.FirstOrDefault(sp => sp.CurrentMob == mob);
                    if (spawnPoint != null)
                    {
                        spawnPoint.IsActive = false;
                        spawnPoint.DeathTime = currentTick;
                        spawnPoint.NextSpawnTime = currentTick + DEATH_ANIMATION_TIME + spawnPoint.RespawnTimeMs;
                        spawnPoint.CurrentMob = null;
                    }

                    _onMobDied?.Invoke(mob);
                }
            }

            // Process dying mobs - check if death animation is complete
            for (int i = _dyingMobs.Count - 1; i >= 0; i--)
            {
                var mob = _dyingMobs[i];
                if (mob.AI == null)
                {
                    RemoveMob(mob);
                    continue;
                }

                // Check if death animation has finished playing
                if (mob.IsDeathAnimationComplete)
                {
                    System.Diagnostics.Debug.WriteLine($"[MobPool] Mob death animation complete, removing");
                    // Mark AI as removed so it's properly cleaned up
                    if (mob.AI.State != MobAIState.Removed)
                    {
                        mob.AI.SetState(MobAIState.Removed, currentTick);
                    }
                    RemoveMob(mob);
                }
            }

            // Process respawns
            if (_respawnEnabled && mobFactory != null)
            {
                foreach (var spawnPoint in _spawnPoints)
                {
                    if (spawnPoint.IsActive)
                        continue;

                    // Skip boss respawn if disabled
                    if (spawnPoint.IsBoss && !_bossSpawnEnabled)
                        continue;

                    // Skip if respawn time is 0 (no respawn)
                    if (spawnPoint.RespawnTimeMs <= 0)
                        continue;

                    // Check if ready to respawn
                    if (currentTick >= spawnPoint.NextSpawnTime)
                    {
                        SpawnMobAtPoint(spawnPoint, mobFactory);
                    }
                }
            }

            // Process boss announcements
            while (_bossAnnouncements.Count > 0)
            {
                string announcement = _bossAnnouncements.Dequeue();
                _onBossAnnouncement?.Invoke(announcement);
            }
        }

        /// <summary>
        /// Get all mobs that should be rendered (active + dying)
        /// </summary>
        public MobItem[] GetRenderableMobs()
        {
            var result = new MobItem[_activeMobs.Count + _dyingMobs.Count];
            _activeMobs.CopyTo(result, 0);
            _dyingMobs.CopyTo(result, _activeMobs.Count);
            return result;
        }
        #endregion

        #region Utility
        /// <summary>
        /// Force respawn all dead mobs immediately
        /// </summary>
        public void ForceRespawnAll(Func<MobSpawnPoint, MobItem> mobFactory)
        {
            if (mobFactory == null)
                return;

            foreach (var spawnPoint in _spawnPoints)
            {
                if (!spawnPoint.IsActive && spawnPoint.RespawnTimeMs > 0)
                {
                    SpawnMobAtPoint(spawnPoint, mobFactory);
                }
            }
        }

        /// <summary>
        /// Set respawn time for all spawn points
        /// </summary>
        public void SetGlobalRespawnTime(int respawnTimeMs)
        {
            foreach (var spawnPoint in _spawnPoints)
            {
                if (!spawnPoint.IsBoss) // Don't change boss respawn
                {
                    spawnPoint.RespawnTimeMs = respawnTimeMs;
                }
            }
        }

        /// <summary>
        /// Get statistics about the mob pool
        /// </summary>
        public MobPoolStats GetStats()
        {
            return new MobPoolStats
            {
                ActiveMobs = _activeMobs.Count,
                DyingMobs = _dyingMobs.Count,
                TotalSpawnPoints = _spawnPoints.Count,
                ActiveSpawnPoints = _spawnPoints.Count(sp => sp.IsActive),
                BossSpawnPoints = _spawnPoints.Count(sp => sp.IsBoss),
                ActiveBosses = _activeMobs.Count(m => m.AI?.IsBoss == true)
            };
        }
        #endregion

        #region Puppet/Summon Aggro System
        private readonly List<PuppetInfo> _activePuppets = new List<PuppetInfo>();

        /// <summary>
        /// Register a puppet/summon that can draw mob aggro (LetMobChasePuppet support)
        /// </summary>
        /// <param name="puppet">Puppet info to register</param>
        public void RegisterPuppet(PuppetInfo puppet)
        {
            if (puppet == null)
                return;

            // Remove existing puppet with same ID
            _activePuppets.RemoveAll(p => p.ObjectId == puppet.ObjectId);
            _activePuppets.Add(puppet);
        }

        /// <summary>
        /// Remove a puppet from the aggro system
        /// </summary>
        public void RemovePuppet(int objectId)
        {
            _activePuppets.RemoveAll(p => p.ObjectId == objectId);
        }

        /// <summary>
        /// Clear all registered puppets
        /// </summary>
        public void ClearPuppets()
        {
            _activePuppets.Clear();
        }

        /// <summary>
        /// Make mobs chase a puppet/summon (LetMobChasePuppet from CMobPool)
        /// Based on MapleStory client's aggro transfer to summons like Dark Servant
        /// </summary>
        /// <param name="puppetX">Puppet X position</param>
        /// <param name="puppetY">Puppet Y position</param>
        /// <param name="aggroRange">Range at which mobs will aggro to puppet</param>
        /// <param name="puppetId">Puppet object ID for tracking</param>
        /// <returns>Number of mobs now chasing the puppet</returns>
        public int LetMobChasePuppet(float puppetX, float puppetY, float aggroRange, int puppetId)
        {
            int count = 0;
            float aggroRangeSq = aggroRange * aggroRange;

            foreach (var mob in _activeMobs)
            {
                if (mob?.AI == null || mob.AI.IsDead)
                    continue;

                if (mob.MovementInfo == null)
                    continue;

                // Calculate distance to puppet
                float dx = mob.MovementInfo.X - puppetX;
                float dy = mob.MovementInfo.Y - puppetY;
                float distSq = dx * dx + dy * dy;

                if (distSq <= aggroRangeSq)
                {
                    // Update mob's target to puppet position
                    // The AI will now chase the puppet instead of the player
                    mob.AI.SetAggroRange((int)aggroRange);
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Update puppet states - remove expired puppets
        /// </summary>
        public void UpdatePuppets(int currentTick)
        {
            _activePuppets.RemoveAll(p => !p.IsActive || (p.ExpirationTime > 0 && currentTick >= p.ExpirationTime));
        }

        /// <summary>
        /// Get all active puppets
        /// </summary>
        public IReadOnlyList<PuppetInfo> ActivePuppets => _activePuppets;
        #endregion

        #region Trapezoid Hit Detection
        /// <summary>
        /// Check if a mob is within a trapezoid area (CheckMobInTrapezoid from CMobPool)
        /// Trapezoid is defined by a center point, direction, and dimensions
        /// </summary>
        /// <param name="mob">Mob to check</param>
        /// <param name="trapezoid">Trapezoid definition</param>
        /// <returns>True if mob is within trapezoid</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CheckMobInTrapezoid(MobItem mob, Trapezoid trapezoid)
        {
            if (mob?.MovementInfo == null)
                return false;

            return trapezoid.ContainsPoint(mob.MovementInfo.X, mob.MovementInfo.Y);
        }

        /// <summary>
        /// Find all mobs hit by a trapezoid attack (FindHitMobInTrapezoid_Plural from CMobPool)
        /// Used for skills like Assaulter, Band of Thieves, etc.
        /// </summary>
        /// <param name="trapezoid">Attack trapezoid area</param>
        /// <param name="maxTargets">Maximum number of targets</param>
        /// <returns>List of mobs in the trapezoid</returns>
        public List<MobItem> FindHitMobInTrapezoid_Plural(Trapezoid trapezoid, int maxTargets = int.MaxValue)
        {
            var results = new List<MobItem>();

            foreach (var mob in _activeMobs)
            {
                if (results.Count >= maxTargets)
                    break;

                if (mob?.AI == null || mob.AI.IsDead)
                    continue;

                if (CheckMobInTrapezoid(mob, trapezoid))
                {
                    results.Add(mob);
                }
            }

            return results;
        }

        /// <summary>
        /// Find mobs in a trapezoid, sorted by distance from origin
        /// </summary>
        public List<MobItem> FindHitMobInTrapezoidSorted(Trapezoid trapezoid, float originX, float originY, int maxTargets = int.MaxValue)
        {
            var results = new List<(MobItem mob, float distSq)>();

            foreach (var mob in _activeMobs)
            {
                if (mob?.AI == null || mob.AI.IsDead)
                    continue;

                if (CheckMobInTrapezoid(mob, trapezoid))
                {
                    float dx = mob.MovementInfo.X - originX;
                    float dy = mob.MovementInfo.Y - originY;
                    results.Add((mob, dx * dx + dy * dy));
                }
            }

            // Sort by distance and take max targets
            results.Sort((a, b) => a.distSq.CompareTo(b.distSq));

            return results.Take(maxTargets).Select(r => r.mob).ToList();
        }
        #endregion

        #region Chain Lightning Targeting
        /// <summary>
        /// Find hit mobs for chain lightning skill (FindHitMobByChainlightning from CMobPool)
        /// Chain lightning bounces from mob to mob within range
        /// </summary>
        /// <param name="startX">Starting X position (caster or previous mob)</param>
        /// <param name="startY">Starting Y position</param>
        /// <param name="bounceRange">Range for each bounce</param>
        /// <param name="maxBounces">Maximum number of bounces/targets</param>
        /// <param name="excludeMobs">Mobs to exclude (already hit)</param>
        /// <returns>Ordered list of mobs to chain to</returns>
        public List<MobItem> FindHitMobByChainlightning(
            float startX, float startY,
            float bounceRange,
            int maxBounces,
            HashSet<int> excludeMobs = null)
        {
            var results = new List<MobItem>();
            var hitMobIds = excludeMobs ?? new HashSet<int>();
            float currentX = startX;
            float currentY = startY;
            float bounceRangeSq = bounceRange * bounceRange;

            for (int bounce = 0; bounce < maxBounces; bounce++)
            {
                MobItem nearest = null;
                float nearestDistSq = float.MaxValue;

                foreach (var mob in _activeMobs)
                {
                    if (mob?.AI == null || mob.AI.IsDead)
                        continue;

                    if (hitMobIds.Contains(mob.PoolId))
                        continue;

                    if (mob.MovementInfo == null)
                        continue;

                    float dx = mob.MovementInfo.X - currentX;
                    float dy = mob.MovementInfo.Y - currentY;
                    float distSq = dx * dx + dy * dy;

                    if (distSq <= bounceRangeSq && distSq < nearestDistSq)
                    {
                        nearestDistSq = distSq;
                        nearest = mob;
                    }
                }

                if (nearest == null)
                    break; // No more mobs in range

                results.Add(nearest);
                hitMobIds.Add(nearest.PoolId);

                // Move chain origin to this mob
                currentX = nearest.MovementInfo.X;
                currentY = nearest.MovementInfo.Y;
            }

            return results;
        }
        #endregion

        #region Rectangle Hit Detection
        /// <summary>
        /// Get mobs within a rectangle area
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerable<MobItem> GetMobsInRect(Rectangle rect)
        {
            return _activeMobs.Where(m =>
            {
                if (m?.MovementInfo == null || m.AI?.IsDead == true)
                    return false;
                return rect.Contains((int)m.MovementInfo.X, (int)m.MovementInfo.Y);
            });
        }

        /// <summary>
        /// Get mobs within a rectangle defined by bounds
        /// </summary>
        public IEnumerable<MobItem> GetMobsInRect(int left, int top, int right, int bottom)
        {
            return GetMobsInRect(new Rectangle(left, top, right - left, bottom - top));
        }

        /// <summary>
        /// Find undead mobs in a rectangle (FindHitUndeadMobInRect from CMobPool)
        /// Used for Heal skill damage and Holy-type attacks
        /// </summary>
        /// <param name="rect">Search rectangle</param>
        /// <param name="maxTargets">Maximum targets to return</param>
        /// <returns>List of undead mobs in the area</returns>
        public List<MobItem> FindHitUndeadMobInRect(Rectangle rect, int maxTargets = int.MaxValue)
        {
            var results = new List<MobItem>();

            foreach (var mob in _activeMobs)
            {
                if (results.Count >= maxTargets)
                    break;

                if (mob?.AI == null || mob.AI.IsDead)
                    continue;

                // Check if undead
                if (!mob.AI.IsUndead)
                    continue;

                if (mob.MovementInfo == null)
                    continue;

                if (rect.Contains((int)mob.MovementInfo.X, (int)mob.MovementInfo.Y))
                {
                    results.Add(mob);
                }
            }

            return results;
        }

        /// <summary>
        /// Find dazzled (confused) mobs in a rectangle (FindHitDazzledMobInRect from CMobPool)
        /// Dazzled mobs may attack other mobs instead of players
        /// </summary>
        /// <param name="rect">Search rectangle</param>
        /// <param name="maxTargets">Maximum targets to return</param>
        /// <returns>List of dazzled mobs in the area</returns>
        public List<MobItem> FindHitDazzledMobInRect(Rectangle rect, int maxTargets = int.MaxValue)
        {
            var results = new List<MobItem>();

            foreach (var mob in _activeMobs)
            {
                if (results.Count >= maxTargets)
                    break;

                if (mob?.AI == null || mob.AI.IsDead)
                    continue;

                // Check if dazzled
                if (!mob.AI.IsDazzled)
                    continue;

                if (mob.MovementInfo == null)
                    continue;

                if (rect.Contains((int)mob.MovementInfo.X, (int)mob.MovementInfo.Y))
                {
                    results.Add(mob);
                }
            }

            return results;
        }
        #endregion

        #region Body Attack Detection
        /// <summary>
        /// Find a mob for body attack collision (FindBodyAttackMob from CMobPool)
        /// Used when player walks into mob for contact damage
        /// </summary>
        /// <param name="playerX">Player X position</param>
        /// <param name="playerY">Player Y position</param>
        /// <param name="playerWidth">Player hitbox width</param>
        /// <param name="playerHeight">Player hitbox height</param>
        /// <returns>Mob that would deal body attack damage, or null</returns>
        public MobItem FindBodyAttackMob(float playerX, float playerY, int playerWidth = 40, int playerHeight = 60)
        {
            // Create player hitbox centered on position
            var playerRect = new Rectangle(
                (int)(playerX - playerWidth / 2),
                (int)(playerY - playerHeight),
                playerWidth,
                playerHeight);

            MobItem closest = null;
            float closestDistSq = float.MaxValue;

            foreach (var mob in _activeMobs)
            {
                if (mob?.AI == null || mob.AI.IsDead)
                    continue;

                if (mob.MovementInfo == null)
                    continue;

                // Get mob hitbox (estimate based on current frame)
                var mobFrame = mob.GetCurrentFrame();
                int mobWidth = mobFrame?.Width ?? 40;
                int mobHeight = mobFrame?.Height ?? 40;

                var mobRect = new Rectangle(
                    (int)(mob.MovementInfo.X - mobWidth / 2),
                    (int)(mob.MovementInfo.Y - mobHeight),
                    mobWidth,
                    mobHeight);

                // Check intersection
                if (playerRect.Intersects(mobRect))
                {
                    float dx = mob.MovementInfo.X - playerX;
                    float dy = mob.MovementInfo.Y - playerY;
                    float distSq = dx * dx + dy * dy;

                    if (distSq < closestDistSq)
                    {
                        closestDistSq = distSq;
                        closest = mob;
                    }
                }
            }

            return closest;
        }

        /// <summary>
        /// Find all mobs touching the player hitbox
        /// </summary>
        public List<MobItem> FindAllBodyAttackMobs(float playerX, float playerY, int playerWidth = 40, int playerHeight = 60)
        {
            var results = new List<MobItem>();

            var playerRect = new Rectangle(
                (int)(playerX - playerWidth / 2),
                (int)(playerY - playerHeight),
                playerWidth,
                playerHeight);

            foreach (var mob in _activeMobs)
            {
                if (mob?.AI == null || mob.AI.IsDead)
                    continue;

                if (mob.MovementInfo == null)
                    continue;

                var mobFrame = mob.GetCurrentFrame();
                int mobWidth = mobFrame?.Width ?? 40;
                int mobHeight = mobFrame?.Height ?? 40;

                var mobRect = new Rectangle(
                    (int)(mob.MovementInfo.X - mobWidth / 2),
                    (int)(mob.MovementInfo.Y - mobHeight),
                    mobWidth,
                    mobHeight);

                if (playerRect.Intersects(mobRect))
                {
                    results.Add(mob);
                }
            }

            return results;
        }
        #endregion

        #region Boss Mob Finding
        /// <summary>
        /// Find the boss mob in the field (FindBossMob from CMobPool)
        /// Returns the first boss found, or the highest level boss if multiple
        /// </summary>
        /// <returns>Boss mob, or null if no bosses active</returns>
        public MobItem FindBossMob()
        {
            MobItem highestBoss = null;
            int highestLevel = -1;

            foreach (var mob in _activeMobs)
            {
                if (mob?.AI == null || mob.AI.IsDead)
                    continue;

                if (!mob.AI.IsBoss)
                    continue;

                if (mob.AI.Level > highestLevel)
                {
                    highestLevel = mob.AI.Level;
                    highestBoss = mob;
                }
            }

            return highestBoss;
        }

        /// <summary>
        /// Find all boss mobs in the field
        /// </summary>
        public List<MobItem> FindAllBossMobs()
        {
            return _activeMobs.Where(m => m?.AI?.IsBoss == true && !m.AI.IsDead).ToList();
        }

        /// <summary>
        /// Find boss mob nearest to a position
        /// </summary>
        public MobItem FindNearestBossMob(float x, float y)
        {
            MobItem nearest = null;
            float nearestDistSq = float.MaxValue;

            foreach (var mob in _activeMobs)
            {
                if (mob?.AI == null || mob.AI.IsDead)
                    continue;

                if (!mob.AI.IsBoss)
                    continue;

                if (mob.MovementInfo == null)
                    continue;

                float dx = mob.MovementInfo.X - x;
                float dy = mob.MovementInfo.Y - y;
                float distSq = dx * dx + dy * dy;

                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearest = mob;
                }
            }

            return nearest;
        }
        #endregion

        #region Nearest Mob Finding
        /// <summary>
        /// Find the nearest mob to a position (FindNearestMob from CMobPool)
        /// Extended version of GetClosestMob with more filtering options
        /// </summary>
        /// <param name="x">X position</param>
        /// <param name="y">Y position</param>
        /// <param name="maxRadius">Maximum search radius</param>
        /// <param name="includeBosses">Include boss mobs in search</param>
        /// <param name="excludeDead">Exclude dead/dying mobs</param>
        /// <returns>Nearest mob, or null if none found</returns>
        public MobItem FindNearestMob(float x, float y, float maxRadius = float.MaxValue, bool includeBosses = true, bool excludeDead = true)
        {
            MobItem closest = null;
            float closestDistSq = maxRadius * maxRadius;

            foreach (var mob in _activeMobs)
            {
                if (mob?.AI == null)
                    continue;

                if (excludeDead && mob.AI.IsDead)
                    continue;

                if (!includeBosses && mob.AI.IsBoss)
                    continue;

                if (mob.MovementInfo == null)
                    continue;

                float dx = mob.MovementInfo.X - x;
                float dy = mob.MovementInfo.Y - y;
                float distSq = dx * dx + dy * dy;

                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closest = mob;
                }
            }

            return closest;
        }

        /// <summary>
        /// Find multiple nearest mobs
        /// </summary>
        public List<MobItem> FindNearestMobs(float x, float y, int count, float maxRadius = float.MaxValue)
        {
            float maxRadiusSq = maxRadius * maxRadius;

            var mobsWithDist = _activeMobs
                .Where(m => m?.MovementInfo != null && m.AI != null && !m.AI.IsDead)
                .Select(m =>
                {
                    float dx = m.MovementInfo.X - x;
                    float dy = m.MovementInfo.Y - y;
                    return (mob: m, distSq: dx * dx + dy * dy);
                })
                .Where(m => m.distSq <= maxRadiusSq)
                .OrderBy(m => m.distSq)
                .Take(count)
                .Select(m => m.mob)
                .ToList();

            return mobsWithDist;
        }
        #endregion

        #region Mob Controller Management
        /// <summary>
        /// Set a mob as locally controlled (SetLocalMob from CMobPool)
        /// </summary>
        /// <param name="mobId">Mob pool ID</param>
        /// <param name="controllerId">Controller/player ID</param>
        public void SetLocalMob(int mobId, int controllerId)
        {
            var mob = GetMob(mobId);
            if (mob?.AI == null)
                return;

            mob.AI.SetLocalController(controllerId);
        }

        /// <summary>
        /// Set a mob as remotely controlled (SetRemoteMob from CMobPool)
        /// </summary>
        /// <param name="mobId">Mob pool ID</param>
        /// <param name="controllerId">Controller/player ID</param>
        public void SetRemoteMob(int mobId, int controllerId)
        {
            var mob = GetMob(mobId);
            if (mob?.AI == null)
                return;

            mob.AI.SetRemoteController(controllerId);
        }

        /// <summary>
        /// Change the controller of a mob (OnMobChangeController from CMobPool)
        /// </summary>
        /// <param name="mobId">Mob pool ID</param>
        /// <param name="newControllerType">New controller type</param>
        /// <param name="newControllerId">New controller ID</param>
        /// <param name="currentTick">Current tick</param>
        public void OnMobChangeController(int mobId, MobControllerType newControllerType, int newControllerId, int currentTick)
        {
            var mob = GetMob(mobId);
            if (mob?.AI == null)
                return;

            mob.AI.ChangeController(newControllerType, newControllerId, currentTick);
        }

        /// <summary>
        /// Get all locally controlled mobs
        /// </summary>
        public IEnumerable<MobItem> GetLocallyControlledMobs()
        {
            return _activeMobs.Where(m => m?.AI?.IsLocalController == true);
        }

        /// <summary>
        /// Get all mobs controlled by a specific player
        /// </summary>
        public IEnumerable<MobItem> GetMobsControlledBy(int controllerId)
        {
            return _activeMobs.Where(m => m?.AI?.ControllerId == controllerId);
        }
        #endregion

        #region Guided Arrow Targeting
        /// <summary>
        /// Reset guided arrow targeting for all mobs (ResetGuidedMob from CMobPool)
        /// Called when a guided arrow skill ends
        /// </summary>
        /// <param name="targetId">Targeting skill ID to reset, or 0 for all</param>
        public void ResetGuidedMob(int targetId = 0)
        {
            foreach (var mob in _activeMobs)
            {
                if (mob?.AI == null)
                    continue;

                if (targetId == 0 || mob.AI.IsGuidedBy(targetId))
                {
                    mob.AI.ResetGuided();
                }
            }
        }

        /// <summary>
        /// Set a mob as a guided arrow target
        /// </summary>
        /// <param name="mobId">Mob pool ID</param>
        /// <param name="targetId">Targeting skill/arrow ID</param>
        public void SetGuidedMob(int mobId, int targetId)
        {
            var mob = GetMob(mobId);
            if (mob?.AI == null)
                return;

            mob.AI.SetGuided(targetId);
        }

        /// <summary>
        /// Get the mob being targeted by a guided arrow
        /// </summary>
        public MobItem GetGuidedMob(int targetId)
        {
            return _activeMobs.FirstOrDefault(m => m?.AI?.IsGuidedBy(targetId) == true);
        }
        #endregion
    }

    /// <summary>
    /// Statistics about the mob pool
    /// </summary>
    public struct MobPoolStats
    {
        public int ActiveMobs;
        public int DyingMobs;
        public int TotalSpawnPoints;
        public int ActiveSpawnPoints;
        public int BossSpawnPoints;
        public int ActiveBosses;
    }
}
