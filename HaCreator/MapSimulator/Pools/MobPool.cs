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
using MapleLib.WzLib.WzStructure.Data;

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
        public int YShift { get; set; }
        public bool Flip { get; set; }
        public string LimitedName { get; set; }
        public bool Hide { get; set; }
        public int? Info { get; set; }
        public int? Team { get; set; }
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
        private const int BOSS_RESPAWN_TIME = 0;            // Bosses don't respawn by default
        private const int DEFAULT_CREATE_MOB_INTERVAL = 9000;
        private const int DEATH_ANIMATION_TIME = 2000;      // 2 seconds for death animation
        private const int INITIAL_RESPAWN_DELAY = 1000;
        private const double MAP_UNIT_SIZE_FACTOR = 0.0000078125d;
        private const int MIN_MONSTER_CAPACITY = 1;
        private const int MAX_MONSTER_CAPACITY = 40;
        private const int SPAWN_HEIGHT_REDUCTION = 450;
        private const int MIN_SPAWN_WIDTH = 1024;
        private const int MIN_SPAWN_HEIGHT = 768;
        #endregion

        #region Collections
        private readonly List<MobItem> _activeMobs = new List<MobItem>();
        private readonly List<MobItem> _dyingMobs = new List<MobItem>();      // Mobs playing death animation
        private readonly List<MobItem> _deadMobs = new List<MobItem>();       // Mobs ready for cleanup/respawn
        private readonly Dictionary<int, MobItem> _mobById = new Dictionary<int, MobItem>();
        private readonly List<MobSpawnPoint> _spawnPoints = new List<MobSpawnPoint>();
        private readonly Dictionary<MobItem, MobSpawnPoint> _spawnPointByMob = new Dictionary<MobItem, MobSpawnPoint>();
        private readonly Queue<string> _bossAnnouncements = new Queue<string>();
        private readonly List<MobSpawnPoint> _respawnCandidates = new List<MobSpawnPoint>();
        #endregion

        #region State
        private int _nextMobId = 1;
        private bool _respawnEnabled = true;
        private bool _bossSpawnEnabled = true;
        private int _lastUpdateTick = 0;
        private int _globalRespawnIntervalMs = DEFAULT_CREATE_MOB_INTERVAL;
        private int _nextRespawnTime = -1;
        private int _minRegularSpawnAtOnce = MIN_MONSTER_CAPACITY;
        private int _maxRegularSpawnAtOnce = MIN_MONSTER_CAPACITY * 2;
        private int _simulatedCharacterCount = 1;
        private bool _noMonsterCapacityLimit = false;
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
        public int MinRegularSpawnAtOnce => _minRegularSpawnAtOnce;
        public int MaxRegularSpawnAtOnce => _maxRegularSpawnAtOnce;
        public int CurrentSpawnTarget => CalculateTargetMobCount();
        public int GlobalRespawnIntervalMs => _globalRespawnIntervalMs;
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
                AssignMobToSpawnPoint(spawnPoint, mob);
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
                YShift = instance?.yShift ?? 0,
                Flip = instance?.Flip == true,
                LimitedName = instance?.LimitedName,
                Hide = instance?.Hide == true,
                Info = instance?.Info,
                Team = instance?.Team,
                RespawnTimeMs = SpecialMobInteractionRules.ShouldDisableAutoRespawn(mobData)
                    ? -1
                    : NormalizeRespawnTime(instance?.MobTime, mobData?.IsBoss ?? false),
                IsBoss = mobData?.IsBoss ?? false,
                IsActive = true
            };
        }

        private static int NormalizeRespawnTime(int? mobTime, bool isBoss)
        {
            if (!mobTime.HasValue)
            {
                return isBoss ? BOSS_RESPAWN_TIME : 0;
            }

            if (mobTime.Value <= 0)
            {
                return mobTime.Value;
            }

            return checked(mobTime.Value * 1000);
        }

        public void Clear()
        {
            _activeMobs.Clear();
            _dyingMobs.Clear();
            _deadMobs.Clear();
            _mobById.Clear();
            _spawnPoints.Clear();
            _spawnPointByMob.Clear();
            _bossAnnouncements.Clear();
            _respawnCandidates.Clear();
            _nextMobId = 1;
            _nextRespawnTime = -1;
            _globalRespawnIntervalMs = DEFAULT_CREATE_MOB_INTERVAL;
            _minRegularSpawnAtOnce = MIN_MONSTER_CAPACITY;
            _maxRegularSpawnAtOnce = MIN_MONSTER_CAPACITY * 2;
            _simulatedCharacterCount = 1;
            _noMonsterCapacityLimit = false;
        }

        public void ConfigureSpawnModel(int mapWidth, int mapHeight, float mobRate, int? createMobIntervalMs, long fieldLimit, int simulatedCharacterCount = 1)
        {
            int spawnWidth = Math.Max(MIN_SPAWN_WIDTH, mapWidth);
            int spawnHeight = Math.Max(MIN_SPAWN_HEIGHT, mapHeight - SPAWN_HEIGHT_REDUCTION);
            int capacity = CalculateMonsterCapacity(spawnWidth, spawnHeight, mobRate);

            _minRegularSpawnAtOnce = capacity;
            _maxRegularSpawnAtOnce = capacity * 2;
            _globalRespawnIntervalMs = createMobIntervalMs.GetValueOrDefault(DEFAULT_CREATE_MOB_INTERVAL);
            _simulatedCharacterCount = Math.Max(1, simulatedCharacterCount);
            _noMonsterCapacityLimit = FieldLimitType.No_Monster_Capacity_Limit.Check(fieldLimit);
            _nextRespawnTime = -1;
        }

        public void TrimInitialPopulation()
        {
            int remainingBudget = Math.Max(0, CalculateTargetMobCount());

            foreach (var spawnPoint in _spawnPoints.Where(sp => sp.IsBoss))
            {
                if (spawnPoint.CurrentMob == null)
                {
                    continue;
                }

                if (remainingBudget > 0)
                {
                    remainingBudget--;
                    continue;
                }

                DeactivateSpawnPoint(spawnPoint, scheduleImmediateRespawn: true);
            }

            foreach (var spawnPoint in _spawnPoints.Where(sp => !sp.IsBoss))
            {
                if (spawnPoint.CurrentMob == null)
                {
                    continue;
                }

                if (remainingBudget > 0)
                {
                    remainingBudget--;
                    continue;
                }

                DeactivateSpawnPoint(spawnPoint, scheduleImmediateRespawn: true);
            }
        }

        private static int CalculateMonsterCapacity(int spawnWidth, int spawnHeight, float mobRate)
        {
            double rawCapacity = spawnWidth * (double)spawnHeight * mobRate * MAP_UNIT_SIZE_FACTOR;
            int capacity = (int)rawCapacity;
            capacity = Math.Max(MIN_MONSTER_CAPACITY, capacity);
            capacity = Math.Min(MAX_MONSTER_CAPACITY, capacity);
            return capacity;
        }

        private int CalculateTargetMobCount()
        {
            if (_noMonsterCapacityLimit)
            {
                return _spawnPoints.Count(sp => !sp.IsBoss);
            }

            int target = _minRegularSpawnAtOnce;
            if (_minRegularSpawnAtOnce <= 0)
            {
                return 0;
            }

            if (_simulatedCharacterCount > _maxRegularSpawnAtOnce / 2)
            {
                target += (_maxRegularSpawnAtOnce - _minRegularSpawnAtOnce)
                    * (2 * _simulatedCharacterCount - _minRegularSpawnAtOnce)
                    / (3 * _minRegularSpawnAtOnce);
            }

            return Math.Min(target, _maxRegularSpawnAtOnce);
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
            if (_spawnPointByMob.TryGetValue(mob, out MobSpawnPoint spawnPoint))
            {
                MarkSpawnPointInactive(spawnPoint, _lastUpdateTick);
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
            _spawnPointByMob.Remove(mob);

            // Notify listeners so they can clean up references (e.g., MapSimulator nulls array entry)
            _onMobRemoved?.Invoke(mob);
        }

        private void DeactivateSpawnPoint(MobSpawnPoint spawnPoint, bool scheduleImmediateRespawn)
        {
            var mob = spawnPoint?.CurrentMob;
            if (spawnPoint == null || mob == null)
            {
                return;
            }

            _activeMobs.Remove(mob);
            _dyingMobs.Remove(mob);
            _deadMobs.Remove(mob);
            _mobById.Remove(mob.PoolId);
            _spawnPointByMob.Remove(mob);

            spawnPoint.CurrentMob = null;
            spawnPoint.IsActive = false;
            spawnPoint.DeathTime = 0;
            spawnPoint.NextSpawnTime = scheduleImmediateRespawn ? 0 : int.MaxValue;

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

            AssignMobToSpawnPoint(spawnPoint, newMob);
            newMob.StartSpawnFadeIn(_lastUpdateTick);

            // Boss announcement
            if (spawnPoint.IsBoss)
            {
                var mobName = newMob.MobInstance?.MobInfo?.Name ?? "Boss";
                _bossAnnouncements.Enqueue($"{mobName} has appeared!");
            }

            _onMobSpawned?.Invoke(newMob);
            return newMob;
        }

        public MobItem AddTemporaryMob(MobItem mob, int currentTick)
        {
            if (mob == null)
            {
                return null;
            }

            _lastUpdateTick = currentTick;

            int mobId = _nextMobId++;
            mob.PoolId = mobId;
            _mobById[mobId] = mob;
            _activeMobs.Add(mob);
            mob.StartSpawnFadeIn(currentTick);

            _onMobSpawned?.Invoke(mob);
            return mob;
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
                    if (_spawnPointByMob.TryGetValue(mob, out MobSpawnPoint spawnPoint))
                    {
                        MarkSpawnPointInactive(spawnPoint, currentTick);
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

            // Process respawns using the field-level spawn cadence and population cap.
            if (_respawnEnabled && mobFactory != null)
            {
                if (_nextRespawnTime < 0)
                {
                    _nextRespawnTime = currentTick + INITIAL_RESPAWN_DELAY;
                }

                if (currentTick >= _nextRespawnTime)
                {
                    RespawnToTargetPopulation(currentTick, mobFactory);
                    _nextRespawnTime = currentTick + _globalRespawnIntervalMs;
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
                if (!spawnPoint.IsActive && spawnPoint.RespawnTimeMs >= 0)
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
                ActiveBosses = _activeMobs.Count(m => m.AI?.IsBoss == true),
                MinRegularSpawnAtOnce = _minRegularSpawnAtOnce,
                MaxRegularSpawnAtOnce = _maxRegularSpawnAtOnce,
                CurrentSpawnTarget = CalculateTargetMobCount(),
                GlobalRespawnIntervalMs = _globalRespawnIntervalMs
            };
        }

        private void RespawnToTargetPopulation(int currentTick, Func<MobSpawnPoint, MobItem> mobFactory)
        {
            int numShouldSpawn = CalculateTargetMobCount() - _activeMobs.Count;
            if (numShouldSpawn <= 0)
            {
                return;
            }

            foreach (var spawnPoint in _spawnPoints.Where(sp => sp.IsBoss))
            {
                if (numShouldSpawn <= 0)
                {
                    return;
                }

                if (!IsSpawnPointReady(spawnPoint, currentTick))
                {
                    continue;
                }

                if (SpawnMobAtPoint(spawnPoint, mobFactory) != null)
                {
                    numShouldSpawn--;
                }
            }

            _respawnCandidates.Clear();
            for (int i = 0; i < _spawnPoints.Count; i++)
            {
                MobSpawnPoint spawnPoint = _spawnPoints[i];
                if (!spawnPoint.IsBoss && IsSpawnPointReady(spawnPoint, currentTick))
                {
                    _respawnCandidates.Add(spawnPoint);
                }
            }

            ShuffleSpawnPoints(_respawnCandidates);

            for (int i = 0; i < _respawnCandidates.Count; i++)
            {
                if (numShouldSpawn <= 0)
                {
                    break;
                }

                MobSpawnPoint spawnPoint = _respawnCandidates[i];
                if (SpawnMobAtPoint(spawnPoint, mobFactory) != null)
                {
                    numShouldSpawn--;
                }
            }
        }

        private bool IsSpawnPointReady(MobSpawnPoint spawnPoint, int currentTick)
        {
            if (spawnPoint == null || spawnPoint.IsActive)
            {
                return false;
            }

            if (spawnPoint.IsBoss && !_bossSpawnEnabled)
            {
                return false;
            }

            if (spawnPoint.RespawnTimeMs < 0)
            {
                return false;
            }

            return currentTick >= spawnPoint.NextSpawnTime;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssignMobToSpawnPoint(MobSpawnPoint spawnPoint, MobItem mob)
        {
            if (spawnPoint == null)
            {
                return;
            }

            MobItem previousMob = spawnPoint.CurrentMob;
            if (previousMob != null)
            {
                _spawnPointByMob.Remove(previousMob);
            }

            spawnPoint.CurrentMob = mob;
            spawnPoint.IsActive = mob != null;
            spawnPoint.DeathTime = 0;
            if (mob != null)
            {
                _spawnPointByMob[mob] = spawnPoint;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MarkSpawnPointInactive(MobSpawnPoint spawnPoint, int currentTick)
        {
            if (spawnPoint == null)
            {
                return;
            }

            MobItem currentMob = spawnPoint.CurrentMob;
            if (currentMob != null)
            {
                _spawnPointByMob.Remove(currentMob);
            }

            spawnPoint.IsActive = false;
            spawnPoint.DeathTime = currentTick;
            spawnPoint.NextSpawnTime = spawnPoint.RespawnTimeMs < 0
                ? int.MaxValue
                : currentTick + DEATH_ANIMATION_TIME + spawnPoint.RespawnTimeMs;
            spawnPoint.CurrentMob = null;
        }

        private static void ShuffleSpawnPoints(List<MobSpawnPoint> spawnPoints)
        {
            for (int i = spawnPoints.Count - 1; i > 0; i--)
            {
                int j = Random.Shared.Next(i + 1);
                (spawnPoints[i], spawnPoints[j]) = (spawnPoints[j], spawnPoints[i]);
            }
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

            foreach (var mob in _activeMobs)
            {
                if (mob?.AI == null || mob.AI.IsDead)
                    continue;

                if (mob.MovementInfo == null)
                    continue;

                PuppetInfo preferredPuppet = ResolvePreferredPuppetForMob(mob, _lastUpdateTick, puppetId, puppetX, puppetY, aggroRange);
                if (preferredPuppet != null && preferredPuppet.ObjectId == puppetId)
                {
                    mob.AI.ForceAggro(
                        preferredPuppet.X,
                        preferredPuppet.Y,
                        _lastUpdateTick,
                        preferredPuppet.ObjectId,
                        MobTargetType.Summoned,
                        MobExternalTargetSource.Summoned);
                    mob.AI.SetAggroRange((int)MathF.Round(Math.Max(1f, preferredPuppet.AggroRange)));
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

        public void SyncPuppetTargets(int currentTick)
        {
            if (_activeMobs.Count == 0 || _activePuppets.Count == 0)
            {
                return;
            }

            foreach (MobItem mob in _activeMobs)
            {
                if (mob?.AI == null || !mob.AI.IsTargetingSummoned)
                {
                    continue;
                }

                PuppetInfo preferredPuppet = ResolvePreferredPuppetForMob(mob, currentTick);
                if (preferredPuppet == null)
                {
                    mob.AI.ClearExternalTarget(currentTick, MobExternalTargetSource.Summoned);
                    continue;
                }

                if (mob.AI.Target.TargetId != preferredPuppet.ObjectId)
                {
                    mob.AI.ForceAggro(
                        preferredPuppet.X,
                        preferredPuppet.Y,
                        currentTick,
                        preferredPuppet.ObjectId,
                        MobTargetType.Summoned,
                        MobExternalTargetSource.Summoned);
                    mob.AI.SetAggroRange((int)MathF.Round(Math.Max(1f, preferredPuppet.AggroRange)));
                    continue;
                }

                mob.AI.UpdateExternalTargetPosition(
                    preferredPuppet.ObjectId,
                    MobTargetType.Summoned,
                    preferredPuppet.X,
                    preferredPuppet.Y,
                    currentTick,
                    MobExternalTargetSource.Summoned);
            }
        }

        private PuppetInfo ResolvePreferredPuppetForMob(
            MobItem mob,
            int currentTick,
            int preferredPuppetId = 0,
            float fallbackPuppetX = 0f,
            float fallbackPuppetY = 0f,
            float fallbackAggroRange = 0f)
        {
            if (mob?.AI == null || mob.MovementInfo == null)
            {
                return null;
            }

            PuppetInfo bestPuppet = null;
            float bestDistanceSq = float.MaxValue;
            int bestAggroValue = int.MinValue;
            int currentTargetId = mob.AI.IsTargetingSummoned ? mob.AI.Target.TargetId : 0;

            foreach (PuppetInfo puppet in EnumerateActivePuppets(currentTick, preferredPuppetId, fallbackPuppetX, fallbackPuppetY, fallbackAggroRange))
            {
                float range = Math.Max(0f, puppet.AggroRange);
                if (range <= 0f)
                {
                    continue;
                }

                float dx = mob.MovementInfo.X - puppet.X;
                float dy = mob.MovementInfo.Y - puppet.Y;
                float distanceSq = dx * dx + dy * dy;
                if (distanceSq > range * range)
                {
                    continue;
                }

                if (IsPreferredPuppetCandidate(
                        puppet,
                        distanceSq,
                        bestPuppet,
                        bestDistanceSq,
                        bestAggroValue,
                        currentTargetId))
                {
                    bestPuppet = puppet;
                    bestDistanceSq = distanceSq;
                    bestAggroValue = puppet.AggroValue;
                }
            }

            return bestPuppet;
        }

        private IEnumerable<PuppetInfo> EnumerateActivePuppets(
            int currentTick,
            int fallbackPuppetId,
            float fallbackPuppetX,
            float fallbackPuppetY,
            float fallbackAggroRange)
        {
            foreach (PuppetInfo puppet in _activePuppets)
            {
                if (IsPuppetActive(puppet, currentTick))
                {
                    yield return puppet;
                }
            }

            if (fallbackPuppetId > 0
                && _activePuppets.All(puppet => puppet.ObjectId != fallbackPuppetId)
                && fallbackAggroRange > 0f)
            {
                yield return new PuppetInfo
                {
                    ObjectId = fallbackPuppetId,
                    X = fallbackPuppetX,
                    Y = fallbackPuppetY,
                    AggroValue = 1,
                    AggroRange = fallbackAggroRange,
                    IsActive = true
                };
            }
        }

        private static bool IsPuppetActive(PuppetInfo puppet, int currentTick)
        {
            return puppet != null
                && puppet.IsActive
                && (puppet.ExpirationTime <= 0 || currentTick < puppet.ExpirationTime);
        }

        private static bool IsPreferredPuppetCandidate(
            PuppetInfo candidate,
            float candidateDistanceSq,
            PuppetInfo currentBest,
            float currentBestDistanceSq,
            int currentBestAggroValue,
            int currentTargetId)
        {
            if (candidate == null)
            {
                return false;
            }

            if (currentBest == null)
            {
                return true;
            }

            if (candidate.AggroValue != currentBestAggroValue)
            {
                return candidate.AggroValue > currentBestAggroValue;
            }

            bool candidateIsCurrentTarget = candidate.ObjectId == currentTargetId;
            bool currentBestIsCurrentTarget = currentBest.ObjectId == currentTargetId;
            if (candidateIsCurrentTarget != currentBestIsCurrentTarget)
            {
                return candidateIsCurrentTarget;
            }

            if (MathF.Abs(candidateDistanceSq - currentBestDistanceSq) > 0.5f)
            {
                return candidateDistanceSq < currentBestDistanceSq;
            }

            if (candidate.OwnerId != currentBest.OwnerId)
            {
                return candidate.OwnerId < currentBest.OwnerId;
            }

            if (candidate.SummonSlotIndex != currentBest.SummonSlotIndex)
            {
                if (candidate.SummonSlotIndex < 0)
                {
                    return false;
                }

                if (currentBest.SummonSlotIndex < 0)
                {
                    return true;
                }

                return candidate.SummonSlotIndex < currentBest.SummonSlotIndex;
            }

            return candidate.ObjectId < currentBest.ObjectId;
        }

        public void SyncHypnotizedTargets(int currentTick)
        {
            if (_activeMobs.Count < 2)
            {
                foreach (MobItem mob in _activeMobs)
                {
                    mob?.AI?.ClearExternalTarget(currentTick, MobExternalTargetSource.Hypnotize);
                }

                return;
            }

            foreach (MobItem mob in _activeMobs)
            {
                if (mob?.AI == null || mob.AI.IsDead)
                {
                    continue;
                }

                if (!mob.AI.IsHypnotized)
                {
                    mob.AI.ClearExternalTarget(currentTick, MobExternalTargetSource.Hypnotize);
                    continue;
                }

                MobItem target = HypnotizeTargetResolver.ResolveTarget(mob, _activeMobs);
                if (target == null)
                {
                    mob.AI.ClearExternalTarget(currentTick, MobExternalTargetSource.Hypnotize);
                    continue;
                }

                if (mob.AI.IsTargetingMob &&
                    mob.AI.ExternalTargetSource == MobExternalTargetSource.Hypnotize &&
                    mob.AI.Target.TargetId == target.PoolId)
                {
                    mob.AI.UpdateExternalTargetPosition(
                        target.PoolId,
                        MobTargetType.Mob,
                        target.CurrentX,
                        target.CurrentY,
                        currentTick,
                        MobExternalTargetSource.Hypnotize);
                    continue;
                }

                mob.AI.ForceAggro(
                    target.CurrentX,
                    target.CurrentY,
                    currentTick,
                    target.PoolId,
                    MobTargetType.Mob,
                    MobExternalTargetSource.Hypnotize);
            }
        }

        public void SyncEncounterTargets(int currentTick)
        {
            if (_activeMobs.Count < 2)
            {
                return;
            }

            bool hasEncounterObjectives = _activeMobs.Any(IsEncounterObjective);
            if (!hasEncounterObjectives)
            {
                ClearStaleEncounterTargets(currentTick);
                return;
            }

            foreach (MobItem mob in _activeMobs)
            {
                if (mob?.AI == null || mob.AI.IsDead)
                {
                    continue;
                }

                MobItem target = ResolveEncounterTarget(mob);
                if (target == null)
                {
                    if (mob.AI.IsTargetingMob)
                    {
                        mob.AI.ClearExternalTarget(currentTick, MobExternalTargetSource.Encounter);
                    }

                    continue;
                }

                if (mob.AI.IsTargetingMob && mob.AI.Target.TargetId == target.PoolId)
                {
                    mob.AI.UpdateExternalTargetPosition(
                        target.PoolId,
                        MobTargetType.Mob,
                        target.CurrentX,
                        target.CurrentY,
                        currentTick,
                        MobExternalTargetSource.Encounter);
                    continue;
                }

                mob.AI.ForceAggro(
                    target.CurrentX,
                    target.CurrentY,
                    currentTick,
                    target.PoolId,
                    MobTargetType.Mob,
                    MobExternalTargetSource.Encounter);
            }
        }

        private MobItem FindNearestMobTarget(MobItem source)
        {
            return FindNearestMobTarget(source, _ => true, float.MaxValue);
        }

        private MobItem ResolveEncounterTarget(MobItem source)
        {
            if (source?.AI == null || source.AI.IsDead)
            {
                return null;
            }

            if (source.IsProtectedFromPlayerDamage)
            {
                return FindNearestMobTarget(source, candidate => !candidate.UsesMobCombatLane, Math.Max(320f, source.AI.AggroRange));
            }

            if ((source.MobData?.Escort ?? 0) > 0)
            {
                return null;
            }

            return FindNearestMobTarget(source, candidate => candidate.UsesMobCombatLane, Math.Max(320f, source.AI.AggroRange));
        }

        private MobItem FindNearestMobTarget(MobItem source, System.Predicate<MobItem> predicate, float maxDistance)
        {
            if (source == null)
            {
                return null;
            }

            MobItem nearest = null;
            float nearestDistanceSq = maxDistance * maxDistance;

            for (int i = 0; i < _activeMobs.Count; i++)
            {
                MobItem candidate = _activeMobs[i];
                if (candidate == null ||
                    candidate == source ||
                    candidate.AI == null ||
                    candidate.AI.IsDead)
                {
                    continue;
                }

                if (predicate != null && !predicate(candidate))
                {
                    continue;
                }

                float dx = candidate.CurrentX - source.CurrentX;
                float dy = candidate.CurrentY - source.CurrentY;
                float distanceSq = dx * dx + dy * dy;
                if (distanceSq >= nearestDistanceSq)
                {
                    continue;
                }

                nearestDistanceSq = distanceSq;
                nearest = candidate;
            }

            return nearest;
        }

        private void ClearStaleEncounterTargets(int currentTick)
        {
            foreach (MobItem mob in _activeMobs)
            {
                if (mob?.AI?.IsTargetingMob == true)
                {
                    mob.AI.ClearExternalTarget(currentTick, MobExternalTargetSource.Encounter);
                }
            }
        }

        private static bool IsEncounterObjective(MobItem mob)
        {
            return mob?.UsesMobCombatLane == true;
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

                var mobRect = mob.GetBodyHitbox();

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

                var mobRect = mob.GetBodyHitbox();

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
        public int MinRegularSpawnAtOnce;
        public int MaxRegularSpawnAtOnce;
        public int CurrentSpawnTarget;
        public int GlobalRespawnIntervalMs;
    }
}
