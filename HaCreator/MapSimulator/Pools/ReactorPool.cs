using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Info;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HaCreator.MapSimulator.Interaction;

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
        None,           // Animation-only or otherwise non-interactive reactor
        Touch,          // Activated by player collision
        Hit,            // Activated by attacking
        Skill,          // Activated by specific skill
        Quest,          // Activated when quest conditions met
        Time,           // Activated after time delay
        Item            // Activated by item use
    }

    [Flags]
    internal enum ReactorActivationTypeMask
    {
        None = 0,
        Touch = 1 << 0,
        Hit = 1 << 1,
        Skill = 1 << 2,
        Quest = 1 << 3,
        Time = 1 << 4,
        Item = 1 << 5
    }

    /// <summary>
    /// Reactor spawn point for respawning
    /// </summary>
    public class ReactorSpawnPoint
    {
        public int SpawnId { get; set; }
        public int? PacketObjectId { get; set; }
        public string ReactorId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public bool Flip { get; set; }
        public string Name { get; set; }
        public int RespawnTimeMs { get; set; }
        public ReactorActivationType ActivationTypeOverride { get; set; }
        public bool CanRespawn { get; set; } = true;
        public bool IsPacketOwned { get; set; }

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
        public int? PacketObjectId { get; set; }
        public ReactorState State { get; set; }
        public int StateFrame { get; set; }
        public int StateStartTime { get; set; }
        public int VisualState { get; set; }
        public int HitCount { get; set; }
        public int RequiredHits { get; set; }
        public float Alpha { get; set; } = 1f;
        public ReactorActivationType PrimaryActivationType { get; set; }
        public ReactorActivationType ActivationType { get; set; }
        public int ActivationValue { get; set; }
        public ReactorType ReactorType { get; set; } = ReactorType.UNKNOWN;
        public int ActivatingPlayerId { get; set; }
        public bool CanRespawn { get; set; } = true;
        public bool IsPacketOwned { get; set; }
        public bool PacketLeavePending { get; set; }
        public int PacketHitStartTime { get; set; }
        public int PacketStateEndTime { get; set; }
        public int PacketAnimationEndTime { get; set; }
        public int PacketAnimationSourceState { get; set; } = -1;
        public int PacketProperEventIndex { get; set; } = -1;
        internal ReactorActivationTypeMask SupportedActivationTypes { get; set; } = ReactorActivationTypeMask.None;
        public int? RequiredItemId { get; set; }
        public int? RequiredQuestId { get; set; }
        public QuestStateType? RequiredQuestState { get; set; }
        public IReadOnlyList<string> ScriptNames { get; set; } = Array.Empty<string>();
        public bool ScriptStatePublished { get; set; }
        public int PreferredAuthoredEventOrder { get; set; } = -1;
        public ReactorActivationType PreferredAuthoredActivationType { get; set; } = ReactorActivationType.None;
    }

    internal sealed class ReactorInteractionMetadata
    {
        public ReactorType ReactorType { get; init; } = ReactorType.UNKNOWN;
        public ReactorActivationType ActivationType { get; init; } = ReactorActivationType.Touch;
        public ReactorActivationTypeMask SupportedActivationTypes { get; init; } = ReactorActivationTypeMask.Touch;
        public int? RequiredItemId { get; init; }
        public int? RequiredQuestId { get; init; }
        public QuestStateType? RequiredQuestState { get; init; }
        public IReadOnlyList<string> ScriptNames { get; init; } = Array.Empty<string>();
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
        private readonly Dictionary<int, int> _reactorIndicesByPacketObjectId = new Dictionary<int, int>();
        private readonly List<ReactorSpawnPoint> _spawnPoints = new List<ReactorSpawnPoint>();
        #endregion

        #region State
        private int _nextPoolId = 1;
        private int _lastUpdateTick = 0;
        private bool _respawnEnabled = true;
        private Func<int, QuestStateType> _questStateProvider;

        // Callbacks
        private Action<ReactorItem, int> _onReactorActivated;
        private Action<ReactorItem, int> _onReactorDestroyed;
        private Action<ReactorItem> _onReactorSpawned;
        private Action<ReactorItem, int> _onReactorTouched;  // (reactor, playerId)
        private Action<ReactorItem, int> _onReactorHit;      // (reactor, playerId)
        private Action<ReactorItem, IReadOnlyList<string>, bool, int> _onReactorScriptStateChanged;

        // Factory for creating new reactor items
        private Func<ReactorSpawnPoint, GraphicsDevice, ReactorItem> _reactorFactory;
        private GraphicsDevice _graphicsDevice;
        #endregion

        #region Public Properties
        public int ReactorCount => GetReactorCount();
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
        public void SetOnReactorScriptStateChanged(Action<ReactorItem, IReadOnlyList<string>, bool, int> callback) => _onReactorScriptStateChanged = callback;
        #endregion

        #region Initialization
        /// <summary>
        /// Initialize the reactor pool with existing reactors from map load
        /// </summary>
        public void Initialize(ReactorItem[] reactors, GraphicsDevice graphicsDevice = null, int currentTick = 0)
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
                ReactorInteractionMetadata interactionMetadata = ResolveInteractionMetadata(instance);

                // Create runtime data
                var data = new ReactorRuntimeData
                {
                    PoolId = poolId,
                    PacketObjectId = null,
                    State = ReactorState.Idle,
                    StateFrame = 0,
                    StateStartTime = currentTick,
                    VisualState = reactor.GetInitialState(),
                    HitCount = 0,
                    RequiredHits = 1, // Default, could be loaded from reactor info
                    Alpha = 1f,
                    PrimaryActivationType = interactionMetadata.ActivationType,
                    ReactorType = interactionMetadata.ReactorType,
                    ActivationType = interactionMetadata.ActivationType,
                    ActivationValue = 0,
                    SupportedActivationTypes = interactionMetadata.SupportedActivationTypes,
                    CanRespawn = true,
                    IsPacketOwned = false,
                    PacketLeavePending = false,
                    PacketHitStartTime = 0,
                    PacketStateEndTime = 0,
                    PacketAnimationEndTime = 0,
                    PacketAnimationSourceState = -1,
                    PacketProperEventIndex = -1,
                    RequiredItemId = interactionMetadata.RequiredItemId,
                    RequiredQuestId = interactionMetadata.RequiredQuestId,
                    RequiredQuestState = interactionMetadata.RequiredQuestState,
                    ScriptNames = interactionMetadata.ScriptNames,
                    ScriptStatePublished = false,
                    PreferredAuthoredEventOrder = -1,
                    PreferredAuthoredActivationType = ReactorActivationType.None
                };
                _reactorData[i] = data;

                // Add to name lookup
                AddReactorNameLookup(instance.Name, i);

                // Create spawn point
                var spawnPoint = new ReactorSpawnPoint
                {
                    SpawnId = i,
                    PacketObjectId = null,
                    ReactorId = instance.ReactorInfo?.ID ?? "0",
                    X = instance.X,
                    Y = instance.Y,
                    Flip = instance.Flip,
                    Name = instance.Name,
                    RespawnTimeMs = instance.ReactorTime > 0 ? instance.ReactorTime : DEFAULT_RESPAWN_TIME,
                    IsActive = true,
                    IsPacketOwned = false,
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

        public void SetQuestStateProvider(Func<int, QuestStateType> questStateProvider)
        {
            _questStateProvider = questStateProvider;
        }

        public void Clear()
        {
            _reactors = null;
            _reactorData.Clear();
            _reactorsByName.Clear();
            _reactorIndicesByPacketObjectId.Clear();
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
            if (index < 0)
                return null;

            if (_reactors != null && index < _reactors.Length && _reactors[index] != null)
                return _reactors[index];

            if (index < _spawnPoints.Count)
                return _spawnPoints[index].CurrentReactor;

            return null;
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

        public bool TryGetReactorIndexByPacketObjectId(int packetObjectId, out int index)
        {
            return _reactorIndicesByPacketObjectId.TryGetValue(packetObjectId, out index);
        }

        /// <summary>
        /// Find reactor at position
        /// </summary>
        public (ReactorItem reactor, int index)? FindReactorAtPosition(float x, float y, float range = 40f, int? currentTick = null)
        {
            if (GetReactorCount() == 0)
                return null;

            float rangeSq = range * range;
            int resolvedTick = currentTick ?? _lastUpdateTick;

            for (int i = 0; i < _reactors.Length; i++)
            {
                var reactor = _reactors[i];
                if (reactor?.ReactorInstance == null)
                    continue;

                // Check if reactor is active
                var data = GetReactorData(i);
                if (data?.State == ReactorState.Destroyed || data?.State == ReactorState.Respawning)
                    continue;

                Rectangle reactorBounds = reactor.GetCurrentBounds(resolvedTick);
                float dx = reactorBounds.Center.X - x;
                float dy = reactorBounds.Center.Y - y;

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
            int playerWidth = 40, int playerHeight = 60,
            int? currentTick = null)
        {
            var results = new List<(ReactorItem reactor, int index)>();

            if (GetReactorCount() == 0)
                return results;

            int resolvedTick = currentTick ?? _lastUpdateTick;

            // Create player hitbox (centered on X, Y at feet)
            var playerRect = new Rectangle(
                (int)(playerX - playerWidth / 2),
                (int)(playerY - playerHeight),
                playerWidth,
                playerHeight);

            for (int i = 0; i < GetReactorCount(); i++)
            {
                var reactor = GetReactor(i);
                if (reactor?.ReactorInstance == null)
                    continue;

                // Check if reactor is in touchable state
                var data = GetReactorData(i);
                if (data == null || data.State != ReactorState.Idle)
                    continue;

                // Only touch-type reactors
                if (!CanActivateWith(reactor, data, ReactorActivationType.Touch))
                    continue;

                if (!MeetsQuestRequirement(data))
                    continue;

                // Get reactor hitbox
                Rectangle reactorRect = reactor.GetCurrentBounds(resolvedTick);

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
            int skillId = 0,
            int? currentTick = null)
        {
            var results = new List<(ReactorItem reactor, int index)>();

            if (GetReactorCount() == 0)
                return results;

            float rangeSq = skillRange * skillRange;
            int resolvedTick = currentTick ?? _lastUpdateTick;

            for (int i = 0; i < GetReactorCount(); i++)
            {
                var reactor = GetReactor(i);
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
                bool canSkillActivate = data.State == ReactorState.Idle
                    && CanActivateWith(reactor, data, ReactorActivationType.Skill, skillId);
                bool canHitActivate = SupportsActivationType(data, ReactorActivationType.Hit);
                if (!canSkillActivate && !canHitActivate)
                    continue;

                if (!MeetsQuestRequirement(data))
                    continue;

                Rectangle reactorBounds = reactor.GetCurrentBounds(resolvedTick);
                float dx = reactorBounds.Center.X - skillX;
                float dy = reactorBounds.Center.Y - skillY;

                if (dx * dx + dy * dy <= rangeSq)
                {
                    results.Add((reactor, i));
                }
            }

            return results;
        }

        /// <summary>
        /// Find skill or hit reactors intersecting a world-space attack area.
        /// </summary>
        public List<(ReactorItem reactor, int index)> FindSkillReactorsInBounds(Rectangle attackBounds, int skillId = 0, int? currentTick = null)
        {
            var results = new List<(ReactorItem reactor, int index)>();

            if (GetReactorCount() == 0 || attackBounds.Width <= 0 || attackBounds.Height <= 0)
                return results;

            int resolvedTick = currentTick ?? _lastUpdateTick;

            for (int i = 0; i < GetReactorCount(); i++)
            {
                var reactor = GetReactor(i);
                if (reactor?.ReactorInstance == null)
                    continue;

                ReactorRuntimeData data = GetReactorData(i);
                if (data == null
                    || data.State == ReactorState.Destroyed
                    || data.State == ReactorState.Respawning)
                {
                    continue;
                }

                bool canSkillActivate = data.State == ReactorState.Idle
                    && CanActivateWith(reactor, data, ReactorActivationType.Skill, skillId);
                bool canHitActivate = SupportsActivationType(data, ReactorActivationType.Hit);
                if (!canSkillActivate && !canHitActivate)
                {
                    continue;
                }

                if (!MeetsQuestRequirement(data))
                    continue;

                Rectangle reactorBounds = reactor.GetCurrentBounds(resolvedTick);
                if (!IsAttackDirectionValid(data.ReactorType, attackBounds, reactorBounds))
                    continue;

                if (reactorBounds.Intersects(attackBounds))
                {
                    results.Add((reactor, i));
                }
            }

            return results;
        }

        /// <summary>
        /// Trigger reactors that react to skill or attack interaction inside the supplied range.
        /// </summary>
        public List<ReactorItem> TriggerSkillReactors(
            float skillX,
            float skillY,
            float skillRange,
            int playerId,
            int currentTick,
            int skillId = 0,
            int damage = 1)
        {
            var triggeredReactors = new List<ReactorItem>();

            foreach (var (reactor, index) in FindSkillReactor(skillX, skillY, skillRange, skillId, currentTick))
            {
                ReactorRuntimeData data = GetReactorData(index);
                if (data == null)
                    continue;

                bool triggered = false;
                if (data.State == ReactorState.Idle
                    && CanActivateWith(reactor, data, ReactorActivationType.Skill, skillId))
                {
                    triggered = ActivateReactor(index, playerId, currentTick, ReactorActivationType.Skill, skillId);
                }

                if (!triggered
                    && SupportsActivationType(data, ReactorActivationType.Hit)
                    && HitReactor(index, playerId, currentTick, damage))
                {
                    triggered = true;
                }

                if (triggered)
                {
                    triggeredReactors.Add(reactor);
                }
            }

            return triggeredReactors;
        }

        /// <summary>
        /// Trigger reactors that react to skill or attack interaction inside the supplied world bounds.
        /// </summary>
        public List<ReactorItem> TriggerSkillReactorsInBounds(
            Rectangle attackBounds,
            int playerId,
            int currentTick,
            int skillId = 0,
            int damage = 1)
        {
            var triggeredReactors = new List<ReactorItem>();

            foreach (var (reactor, index) in FindSkillReactorsInBounds(attackBounds, skillId, currentTick))
            {
                ReactorRuntimeData data = GetReactorData(index);
                if (data == null)
                    continue;

                bool triggered = false;
                if (data.State == ReactorState.Idle
                    && CanActivateWith(reactor, data, ReactorActivationType.Skill, skillId))
                {
                    triggered = ActivateReactor(index, playerId, currentTick, ReactorActivationType.Skill, skillId);
                }

                if (!triggered
                    && SupportsActivationType(data, ReactorActivationType.Hit)
                    && HitReactor(index, playerId, currentTick, damage))
                {
                    triggered = true;
                }

                if (triggered)
                {
                    triggeredReactors.Add(reactor);
                }
            }

            return triggeredReactors;
        }

        public List<(ReactorItem reactor, int index)> FindItemReactors(int itemId)
        {
            var results = new List<(ReactorItem reactor, int index)>();

            if (GetReactorCount() == 0 || itemId <= 0)
                return results;

            for (int i = 0; i < GetReactorCount(); i++)
            {
                ReactorItem reactor = GetReactor(i);
                if (reactor?.ReactorInstance == null)
                    continue;

                ReactorRuntimeData data = GetReactorData(i);
                if (data == null
                    || data.State != ReactorState.Idle
                    || !CanActivateWith(reactor, data, ReactorActivationType.Item, itemId)
                    || !MatchesRequiredItem(data, itemId)
                    || !MeetsQuestRequirement(data))
                {
                    continue;
                }

                results.Add((reactor, i));
            }

            return results;
        }

        public List<(ReactorItem reactor, int index)> FindItemReactorsAroundLocalUser(
            float playerX,
            float playerY,
            int itemId,
            int playerWidth = 40,
            int playerHeight = 60,
            int? currentTick = null)
        {
            var results = new List<(ReactorItem reactor, int index)>();

            if (GetReactorCount() == 0 || itemId <= 0)
                return results;

            int resolvedTick = currentTick ?? _lastUpdateTick;

            var playerRect = new Rectangle(
                (int)(playerX - playerWidth / 2f),
                (int)(playerY - playerHeight),
                playerWidth,
                playerHeight);

            for (int i = 0; i < GetReactorCount(); i++)
            {
                ReactorItem reactor = GetReactor(i);
                if (reactor?.ReactorInstance == null)
                    continue;

                ReactorRuntimeData data = GetReactorData(i);
                if (data == null
                    || data.State != ReactorState.Idle
                    || !CanActivateWith(reactor, data, ReactorActivationType.Item, itemId)
                    || !MatchesRequiredItem(data, itemId)
                    || !MeetsQuestRequirement(data))
                {
                    continue;
                }

                if (!reactor.GetCurrentBounds(resolvedTick).Intersects(playerRect))
                    continue;

                results.Add((reactor, i));
            }

            return results;
        }

        public List<ReactorItem> TriggerItemReactors(int itemId, int playerId, int currentTick)
        {
            var triggeredReactors = new List<ReactorItem>();

            foreach (var (reactor, index) in FindItemReactors(itemId))
            {
                if (ActivateReactor(index, playerId, currentTick, ReactorActivationType.Item, itemId))
                {
                    triggeredReactors.Add(reactor);
                }
            }

            return triggeredReactors;
        }

        public List<ReactorItem> TriggerItemReactorsAroundLocalUser(
            float playerX,
            float playerY,
            int itemId,
            int playerId,
            int currentTick)
        {
            var triggeredReactors = new List<ReactorItem>();

            foreach (var (reactor, index) in FindItemReactorsAroundLocalUser(playerX, playerY, itemId, currentTick: currentTick))
            {
                if (ActivateReactor(index, playerId, currentTick, ReactorActivationType.Item, itemId))
                {
                    triggeredReactors.Add(reactor);
                }
            }

            return triggeredReactors;
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
        public bool ActivateReactor(
            int index,
            int playerId,
            int currentTick,
            ReactorActivationType activationType = ReactorActivationType.Touch,
            int activationValue = 0)
        {
            var reactor = GetReactor(index);
            var data = GetReactorData(index);

            if (reactor == null || data == null)
                return false;

            if (data.State != ReactorState.Idle)
                return false;

            var request = new ReactorTransitionRequest(activationType, data.ReactorType, activationValue);
            if (!reactor.CanActivateFromState(data.VisualState, request))
            {
                return false;
            }

            if (TryResolveNextVisualState(reactor, data, request, out int nextVisualState, out int selectedAuthoredOrder))
            {
                data.VisualState = nextVisualState;
                UpdatePreferredAuthoredOrder(data, activationType, selectedAuthoredOrder);
            }
            else
            {
                ClearPreferredAuthoredOrder(data);
            }

            data.ActivationType = activationType;
            data.ActivationValue = activationValue;
            data.State = ReactorState.Activated;
            data.StateStartTime = currentTick;
            data.StateFrame = 0;
            data.ActivatingPlayerId = playerId;
            PublishScriptState(reactor, data, isEnabled: true, currentTick);

            // Invoke appropriate callback
            if (activationType == ReactorActivationType.Touch)
                _onReactorTouched?.Invoke(reactor, playerId);
            else if (activationType == ReactorActivationType.Hit || activationType == ReactorActivationType.Skill)
                _onReactorHit?.Invoke(reactor, playerId);

            _onReactorActivated?.Invoke(reactor, playerId);
            return true;
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
                    if (TryResolveNextVisualState(
                        reactor,
                        data,
                        new ReactorTransitionRequest(ReactorActivationType.Hit, data.ReactorType, data.ActivationValue),
                        out int nextVisualState,
                        out int selectedAuthoredOrder))
                    {
                        data.ActivationType = ReactorActivationType.Hit;
                        data.ActivationValue = 0;
                        data.VisualState = nextVisualState;
                        data.State = ReactorState.Activated;
                        data.StateStartTime = currentTick;
                        data.StateFrame = 0;
                        data.ActivatingPlayerId = playerId;
                        UpdatePreferredAuthoredOrder(data, ReactorActivationType.Hit, selectedAuthoredOrder);
                    }
                    else
                    {
                        ClearPreferredAuthoredOrder(data);
                        // Progress to terminal destroyed state when WZ does not expose another branch.
                        DestroyReactor(index, playerId, currentTick);
                    }
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
            data.PacketLeavePending = false;
            ClearPreferredAuthoredOrder(data);
            PublishScriptState(reactor, data, isEnabled: false, currentTick);

            // Update spawn point
            if (index < _spawnPoints.Count)
            {
                var spawnPoint = _spawnPoints[index];
                spawnPoint.IsActive = false;
                spawnPoint.DestroyTime = currentTick;
                spawnPoint.NextSpawnTime = currentTick + spawnPoint.RespawnTimeMs;
                spawnPoint.CurrentReactor = null;
            }

            UntrackPacketObjectId(data.PacketObjectId);
            RemoveReactorNameLookup(reactor?.ReactorInstance?.Name, index);

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
            ReactorItem reactor = GetReactor(index);
            var data = GetReactorData(index);
            if (data == null)
                return;

            data.State = ReactorState.Idle;
            data.StateStartTime = currentTick;
            data.StateFrame = 0;
            data.VisualState = reactor?.GetInitialState() ?? 0;
            data.HitCount = 0;
            data.Alpha = 1f;
            data.ActivationType = data.PrimaryActivationType;
            data.ActivationValue = 0;
            data.PacketLeavePending = false;
            data.PacketHitStartTime = 0;
            data.PacketStateEndTime = 0;
            data.PacketAnimationEndTime = 0;
            data.PacketAnimationSourceState = -1;
            data.PacketProperEventIndex = -1;
            data.PreferredAuthoredEventOrder = -1;
            data.PreferredAuthoredActivationType = ReactorActivationType.None;
            PublishScriptState(reactor, data, isEnabled: false, currentTick);
        }

        public void RefreshQuestReactors(int currentTick)
        {
            if (_questStateProvider == null || _reactors == null)
                return;

            foreach (var kvp in _reactorData)
            {
                int index = kvp.Key;
                ReactorRuntimeData data = kvp.Value;
                if (data?.RequiredQuestId is not int)
                    continue;

                bool matchesQuest = MeetsQuestRequirement(data);
                if (SupportsActivationType(data, ReactorActivationType.Quest))
                {
                    if (matchesQuest)
                    {
                        if (data.State == ReactorState.Idle)
                        {
                            ActivateReactor(index, playerId: 0, currentTick, ReactorActivationType.Quest, data.RequiredQuestId.Value);
                        }
                    }
                    else if (data.State == ReactorState.Active || data.State == ReactorState.Activated)
                    {
                        ResetReactor(index, currentTick);
                    }
                }
            }
        }

        private ReactorInteractionMetadata ResolveInteractionMetadata(ReactorInstance instance)
        {
            ReactorInfo reactorInfo = instance?.BaseInfo as ReactorInfo;
            WzSubProperty infoProperty = reactorInfo?.LinkedWzImage?["info"] as WzSubProperty;

            int? requiredItemId = TryGetOptionalInt(infoProperty?["itemID"])
                ?? TryGetOptionalInt(infoProperty?["itemid"]);
            int? requiredQuestId = TryGetOptionalInt(infoProperty?["quest"])
                ?? TryGetOptionalInt(infoProperty?["reqQuest"])
                ?? TryInferRequiredQuestIdFromStateEvents(reactorInfo?.LinkedWzImage);
            HashSet<int> authoredEventTypes = GetStateEventTypes(reactorInfo?.LinkedWzImage);
            QuestStateType? requiredQuestState = TryGetOptionalQuestState(infoProperty?["state"])
                ?? TryGetOptionalQuestState(infoProperty?["questState"])
                ?? TryResolveDefaultQuestState(requiredQuestId, authoredEventTypes);
            IReadOnlyList<string> scriptNames = QuestRuntimeManager.ParseScriptNames(infoProperty?["script"]);
            ReactorType reactorType = TryGetOptionalReactorType(infoProperty?["reactorType"])
                ?? TryGetOptionalReactorType(infoProperty?["type"])
                ?? ReactorType.UNKNOWN;

            bool activateByTouch = TryGetOptionalInt(infoProperty?["activateByTouch"]).GetValueOrDefault() != 0;

            ReactorActivationType activationType = requiredItemId.HasValue
                ? ReactorActivationType.Item
                : requiredQuestId.HasValue
                    ? ReactorActivationType.Quest
                    : activateByTouch
                        ? ReactorActivationType.Touch
                        : TryInferActivationTypeFromStateEvents(reactorInfo?.LinkedWzImage)
                            ?? reactorType switch
                            {
                                ReactorType.ActivatedByAnyHit => ReactorActivationType.Hit,
                                ReactorType.ActivatedLeftHit => ReactorActivationType.Hit,
                                ReactorType.ActivatedRightHit => ReactorActivationType.Hit,
                                ReactorType.ActivatedByHarvesting => ReactorActivationType.Hit,
                                ReactorType.ActivatedBySkill => ReactorActivationType.Skill,
                                ReactorType.ActivatedByTouch => ReactorActivationType.Touch,
                                ReactorType.ActivatedbyItem => ReactorActivationType.Item,
                                ReactorType.AnimationOnly => ReactorActivationType.None,
                                _ => HasHitAnimation(reactorInfo?.LinkedWzImage) ? ReactorActivationType.Hit : ReactorActivationType.Touch
                            };

            ReactorActivationTypeMask supportedActivationTypes = ResolveSupportedActivationTypes(
                activationType,
                requiredItemId,
                requiredQuestId,
                authoredEventTypes);

            return new ReactorInteractionMetadata
            {
                ReactorType = reactorType,
                ActivationType = activationType,
                SupportedActivationTypes = supportedActivationTypes,
                RequiredItemId = requiredItemId,
                RequiredQuestId = requiredQuestId,
                RequiredQuestState = requiredQuestState,
                ScriptNames = scriptNames
            };
        }

        private bool MeetsQuestRequirement(ReactorRuntimeData data)
        {
            if (data?.RequiredQuestId is not int questId)
            {
                return true;
            }

            if (_questStateProvider == null)
            {
                return false;
            }

            QuestStateType currentState = _questStateProvider(questId);
            return !data.RequiredQuestState.HasValue || currentState == data.RequiredQuestState.Value;
        }

        private static bool MatchesRequiredItem(ReactorRuntimeData data, int itemId)
        {
            return data?.RequiredItemId is not int requiredItemId || requiredItemId == itemId;
        }

        private static int? TryGetOptionalInt(WzImageProperty property)
        {
            return property switch
            {
                WzIntProperty intProperty => intProperty.Value,
                WzShortProperty shortProperty => shortProperty.Value,
                WzLongProperty longProperty => (int)longProperty.Value,
                WzStringProperty stringProperty when int.TryParse(stringProperty.Value, out int value) => value,
                _ => null
            };
        }

        private static string TryGetOptionalString(WzImageProperty property)
        {
            return (property as WzStringProperty)?.Value;
        }

        private static QuestStateType? TryGetOptionalQuestState(WzImageProperty property)
        {
            int? value = TryGetOptionalInt(property);
            return value.HasValue && Enum.IsDefined(typeof(QuestStateType), value.Value)
                ? (QuestStateType)value.Value
                : null;
        }

        private static ReactorType? TryGetOptionalReactorType(WzImageProperty property)
        {
            int? value = TryGetOptionalInt(property);
            return value.HasValue && Enum.IsDefined(typeof(ReactorType), value.Value)
                ? (ReactorType)value.Value
                : null;
        }

        private static ReactorActivationType? TryInferActivationTypeFromStateEvents(WzImage linkedReactorImage)
        {
            if (linkedReactorImage?.WzProperties == null)
                return null;

            var eventTypes = new HashSet<int>();
            foreach (WzImageProperty property in linkedReactorImage.WzProperties)
            {
                if (!int.TryParse(property?.Name, out _))
                    continue;

                if (WzInfoTools.GetRealProperty(property?["event"]) is not WzSubProperty eventProperty)
                    continue;

                foreach (WzSubProperty eventNode in eventProperty.WzProperties.OfType<WzSubProperty>())
                {
                    int? eventType = TryGetOptionalInt(WzInfoTools.GetRealProperty(eventNode["type"]));
                    if (eventType.HasValue)
                    {
                        eventTypes.Add(eventType.Value);
                    }
                }
            }

            if (eventTypes.Contains(5))
                return ReactorActivationType.Skill;

            if (eventTypes.Contains(9))
                return ReactorActivationType.Item;

            if (eventTypes.Any(eventType => eventType is 1 or 2 or 8))
                return ReactorActivationType.Hit;

            if (eventTypes.Contains(100))
                return ReactorActivationType.Quest;

            if (eventTypes.Any(eventType => eventType is 0 or 6))
                return ReactorActivationType.Touch;

            if (eventTypes.Any(eventType => eventType is 7 or 101))
                return ReactorActivationType.Time;

            return null;
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
            if (_reactorFactory != null)
            {
                newReactor = _reactorFactory(spawnPoint, _graphicsDevice);
            }

            if (newReactor != null)
            {
                // Update arrays (this is simplified - in practice you'd resize the array)
                spawnPoint.CurrentReactor = newReactor;
                spawnPoint.IsActive = true;
                newReactor.SetWorldPosition((int)Math.Round(spawnPoint.X), (int)Math.Round(spawnPoint.Y));
                newReactor.SetFlipState(spawnPoint.Flip);

                // Reset runtime data
                if (_reactorData.TryGetValue(spawnIndex, out var data))
                {
                    ApplyInteractionMetadata(newReactor?.ReactorInstance, data);
                    if (spawnPoint.ActivationTypeOverride != ReactorActivationType.None)
                    {
                        data.ActivationType = spawnPoint.ActivationTypeOverride;
                        data.SupportedActivationTypes = AddSupportedActivationType(
                            data.SupportedActivationTypes,
                            spawnPoint.ActivationTypeOverride);
                    }

                    data.State = ReactorState.Idle;
                    data.StateStartTime = currentTick;
                    data.StateFrame = 0;
                    data.VisualState = newReactor.GetInitialState();
                    data.HitCount = 0;
                    data.Alpha = 1f;
                    data.ActivationValue = 0;
                    data.CanRespawn = spawnPoint.CanRespawn;
                    data.PacketObjectId = spawnPoint.PacketObjectId;
                    data.IsPacketOwned = spawnPoint.IsPacketOwned;
                    data.PacketLeavePending = false;
                    data.PacketHitStartTime = 0;
                    data.PacketStateEndTime = 0;
                    data.PacketAnimationEndTime = 0;
                    data.PacketAnimationSourceState = -1;
                    data.PacketProperEventIndex = -1;
                    data.ScriptStatePublished = false;
                    data.PreferredAuthoredEventOrder = -1;
                    data.PreferredAuthoredActivationType = ReactorActivationType.None;
                }

                AddReactorNameLookup(newReactor?.ReactorInstance?.Name, spawnIndex);
                TrackPacketObjectId(spawnIndex, spawnPoint.PacketObjectId);

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
        public List<int> SpawnReactorsAtPositions(
            string reactorId,
            List<(float x, float y)> positions,
            int currentTick,
            ReactorActivationType activationTypeOverride = ReactorActivationType.None,
            bool canRespawn = true,
            string namePrefix = null)
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
                    PacketObjectId = null,
                    ReactorId = reactorId,
                    X = x,
                    Y = y,
                    Flip = false,
                    Name = string.IsNullOrWhiteSpace(namePrefix) ? $"spawned_{reactorId}_{_spawnPoints.Count}" : $"{namePrefix}_{_spawnPoints.Count}",
                    RespawnTimeMs = DEFAULT_RESPAWN_TIME,
                    IsActive = false,
                    ActivationTypeOverride = activationTypeOverride,
                    CanRespawn = canRespawn,
                    IsPacketOwned = false
                };

                _spawnPoints.Add(spawnPoint);

                // Create runtime data
                var data = new ReactorRuntimeData
                {
                    PoolId = _nextPoolId++,
                    PacketObjectId = null,
                    State = ReactorState.Respawning,
                    StateFrame = 0,
                    StateStartTime = currentTick,
                    VisualState = 0,
                    HitCount = 0,
                    RequiredHits = 1,
                    Alpha = 1f,
                    PrimaryActivationType = activationTypeOverride == ReactorActivationType.None ? ReactorActivationType.Touch : activationTypeOverride,
                    ActivationType = activationTypeOverride == ReactorActivationType.None ? ReactorActivationType.Touch : activationTypeOverride,
                    ActivationValue = 0,
                    SupportedActivationTypes = ToActivationMask(
                        activationTypeOverride == ReactorActivationType.None ? ReactorActivationType.Touch : activationTypeOverride),
                    CanRespawn = canRespawn,
                    IsPacketOwned = false,
                    PacketLeavePending = false,
                    PacketHitStartTime = 0,
                    PacketStateEndTime = 0,
                    PacketProperEventIndex = -1
                };
                _reactorData[spawnPoint.SpawnId] = data;

                spawnPoint.NextSpawnTime = currentTick;
                SpawnReactor(spawnPoint.SpawnId, currentTick);

                spawnedIndices.Add(spawnPoint.SpawnId);
            }

            return spawnedIndices;
        }

        public bool TryEnterPacketOwnedReactor(
            int packetObjectId,
            string reactorId,
            int initialState,
            int x,
            int y,
            bool flip,
            string name,
            int currentTick,
            out int reactorIndex,
            out string message)
        {
            reactorIndex = -1;

            if (packetObjectId <= 0)
            {
                message = "Packet-owned reactor enter is missing a valid reactor object id.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(reactorId))
            {
                message = $"Packet-owned reactor {packetObjectId} enter is missing a valid template id.";
                return false;
            }

            if (TryGetReactorIndexByPacketObjectId(packetObjectId, out int existingIndex))
            {
                ApplyPacketReactorState(existingIndex, initialState, x, y, flip, currentTick);
                SyncPacketScriptPublication(GetReactor(existingIndex), GetReactorData(existingIndex), currentTick);
                reactorIndex = existingIndex;
                message = $"Updated packet-owned reactor {packetObjectId} as template {reactorId} at ({x}, {y}).";
                return true;
            }

            var spawnPoint = new ReactorSpawnPoint
            {
                SpawnId = _spawnPoints.Count,
                PacketObjectId = packetObjectId,
                ReactorId = reactorId,
                X = x,
                Y = y,
                Flip = flip,
                Name = string.IsNullOrWhiteSpace(name) ? $"packet_{packetObjectId}" : name,
                RespawnTimeMs = 0,
                ActivationTypeOverride = ReactorActivationType.None,
                CanRespawn = false,
                IsPacketOwned = true,
                IsActive = false
            };

            _spawnPoints.Add(spawnPoint);
            _reactorData[spawnPoint.SpawnId] = new ReactorRuntimeData
            {
                PoolId = _nextPoolId++,
                PacketObjectId = packetObjectId,
                State = ReactorState.Respawning,
                StateStartTime = currentTick,
                VisualState = initialState,
                RequiredHits = 1,
                Alpha = 1f,
                PrimaryActivationType = ReactorActivationType.None,
                ActivationType = ReactorActivationType.None,
                CanRespawn = false,
                IsPacketOwned = true,
                PacketLeavePending = false,
                PacketHitStartTime = 0,
                PacketStateEndTime = currentTick + 800,
                PacketAnimationEndTime = 0,
                PacketAnimationSourceState = -1,
                PacketProperEventIndex = -1,
                PreferredAuthoredEventOrder = -1,
                PreferredAuthoredActivationType = ReactorActivationType.None
            };

            ReactorItem reactor = SpawnReactor(spawnPoint.SpawnId, currentTick);
            if (reactor == null)
            {
                _reactorData.Remove(spawnPoint.SpawnId);
                _spawnPoints.RemoveAt(_spawnPoints.Count - 1);
                message = $"Failed to spawn packet-owned reactor template {reactorId} for object {packetObjectId}.";
                return false;
            }

            ApplyPacketReactorState(spawnPoint.SpawnId, initialState, x, y, flip, currentTick);
            SyncPacketScriptPublication(reactor, GetReactorData(spawnPoint.SpawnId), currentTick);
            TrackPacketObjectId(spawnPoint.SpawnId, packetObjectId);
            reactorIndex = spawnPoint.SpawnId;
            message = $"Spawned packet-owned reactor {packetObjectId} as template {reactorId} at ({x}, {y}).";
            return true;
        }

        public bool TryChangePacketOwnedReactorState(
            int packetObjectId,
            int state,
            int x,
            int y,
            int hitStartDelayMs,
            int properEventIndex,
            int stateEndDelayTicks,
            int currentTick,
            out string message)
        {
            if (!TryGetReactorIndexByPacketObjectId(packetObjectId, out int index))
            {
                message = $"Packet-owned reactor {packetObjectId} is not active in the current pool.";
                return false;
            }

            ReactorRuntimeData data = GetReactorData(index);
            ReactorItem reactor = GetReactor(index);
            if (data == null || reactor == null)
            {
                message = $"Packet-owned reactor {packetObjectId} could not be resolved in the current pool.";
                return false;
            }

            int previousVisualState = data.VisualState;
            ApplyPacketReactorState(index, state, x, y, reactor.ReactorInstance?.Flip ?? false, currentTick);
            data.PacketHitStartTime = ResolvePacketClientHitStartTime(currentTick, hitStartDelayMs);
            ApplyPacketProperEventIndexPreference(data, properEventIndex);
            data.PacketStateEndTime = ResolvePacketStateEndTime(currentTick, stateEndDelayTicks);
            data.PacketAnimationEndTime = 0;
            data.PacketAnimationSourceState = previousVisualState;
            data.State = ReactorState.Activated;
            data.StateStartTime = currentTick;
            SyncPacketScriptPublication(reactor, data, currentTick);
            message = $"Applied packet-owned reactor state {state} to object {packetObjectId} at ({x}, {y}).";
            return true;
        }

        public bool TryMovePacketOwnedReactor(int packetObjectId, int x, int y, int currentTick, out string message)
        {
            if (!TryGetReactorIndexByPacketObjectId(packetObjectId, out int index))
            {
                message = $"Packet-owned reactor {packetObjectId} is not active in the current pool.";
                return false;
            }

            ReactorRuntimeData data = GetReactorData(index);
            ReactorItem reactor = GetReactor(index);
            if (data == null || reactor == null)
            {
                message = $"Packet-owned reactor {packetObjectId} could not be resolved in the current pool.";
                return false;
            }

            reactor.SetWorldPosition(x, y);
            if (index < _spawnPoints.Count)
            {
                _spawnPoints[index].X = x;
                _spawnPoints[index].Y = y;
            }

            data.StateStartTime = currentTick;
            message = $"Moved packet-owned reactor {packetObjectId} to ({x}, {y}).";
            return true;
        }

        public bool TryLeavePacketOwnedReactor(int packetObjectId, int state, int x, int y, int currentTick, out string message)
        {
            if (!TryGetReactorIndexByPacketObjectId(packetObjectId, out int index))
            {
                message = $"Packet-owned reactor {packetObjectId} is not active in the current pool.";
                return false;
            }

            ReactorRuntimeData data = GetReactorData(index);
            ReactorItem reactor = GetReactor(index);
            if (data == null || reactor == null)
            {
                message = $"Packet-owned reactor {packetObjectId} could not be resolved in the current pool.";
                return false;
            }

            int previousVisualState = data.VisualState;
            int remainingCurrentAnimationDuration = reactor.GetRemainingAnimationDuration(currentTick);
            ApplyPacketReactorState(index, state, x, y, reactor.ReactorInstance?.Flip ?? false, currentTick);
            data.PacketLeavePending = true;
            data.PacketProperEventIndex = -2;
            data.PacketAnimationSourceState = previousVisualState;
            data.PacketHitStartTime = ResolvePacketLeaveHitStartTime(currentTick, reactor.HasAuthoredEventInfo(state));
            data.PacketAnimationEndTime = data.PacketHitStartTime == 0
                ? ResolvePacketAnimationEndTime(currentTick, remainingCurrentAnimationDuration)
                : 0;
            data.PacketStateEndTime = 0;
            data.CanRespawn = false;
            message = $"Queued packet-owned reactor {packetObjectId} for leave-field removal from ({x}, {y}).";
            return true;
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
                ReactorItem reactor = GetReactor(index);

                if (reactor != null)
                {
                    data.StateFrame = reactor.GetCurrentFrameIndex(currentTick);
                }

                if (data.IsPacketOwned)
                {
                    if (data.PacketLeavePending)
                    {
                        if (data.PacketHitStartTime > 0 && currentTick >= data.PacketHitStartTime)
                        {
                            data.PacketHitStartTime = 0;
                            data.PacketAnimationEndTime = ResolvePacketAnimationEndTime(
                                currentTick,
                                ResolvePacketHitAnimationDuration(reactor, data));
                        }

                        if (data.PacketHitStartTime == 0
                            && (data.PacketAnimationEndTime <= 0 || currentTick >= data.PacketAnimationEndTime))
                        {
                            DestroyReactor(index, playerId: 0, currentTick);
                        }

                        continue;
                    }

                    if (data.State == ReactorState.Activated
                        && data.PacketHitStartTime > 0
                        && currentTick >= data.PacketHitStartTime)
                    {
                        data.PacketHitStartTime = 0;
                        data.PacketAnimationEndTime = ResolvePacketAnimationEndTime(
                            currentTick,
                            ResolvePacketHitAnimationDuration(reactor, data));

                        if (data.PacketAnimationEndTime <= 0)
                        {
                            data.State = ReactorState.Active;
                            data.StateStartTime = currentTick;
                        }
                    }

                    if (data.State == ReactorState.Activated
                        && data.PacketAnimationEndTime > 0
                        && currentTick >= data.PacketAnimationEndTime)
                    {
                        data.PacketAnimationEndTime = 0;
                        data.State = ReactorState.Active;
                        data.StateStartTime = currentTick;
                    }

                    if (data.PacketStateEndTime > 0
                        && currentTick >= data.PacketStateEndTime
                        && data.State == ReactorState.Activated
                        && data.PacketHitStartTime <= 0
                        && data.PacketAnimationEndTime <= 0)
                    {
                        data.State = ReactorState.Active;
                        data.StateStartTime = currentTick;
                    }

                    continue;
                }

                switch (data.State)
                {
                    case ReactorState.Idle:
                        if (data.PrimaryActivationType == ReactorActivationType.Time
                            && currentTick - data.StateStartTime >= GetActivationDuration(index)
                            && TryActivateTimedIdleReactor(index, reactor, data, currentTick))
                        {
                            break;
                        }
                        break;

                    case ReactorState.Activated:
                        if (currentTick - data.StateStartTime >= GetActivationDuration(index))
                        {
                            if (ShouldAutoChainStates(data)
                                && TryAdvanceToNextVisualState(reactor, data, currentTick, data.ActivationType))
                            {
                                break;
                            }

                            data.State = ReactorState.Active;
                            data.StateStartTime = currentTick;
                            data.StateFrame = 0;
                        }
                        break;

                    case ReactorState.Active:
                        if (data.ActivationType != ReactorActivationType.Hit
                            && data.ActivationType != ReactorActivationType.None
                            && currentTick - data.StateStartTime >= GetActivationDuration(index)
                            && TryApplyTimedStateTransition(index, reactor, data, currentTick))
                        {
                            break;
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
                        if (_reactorData.TryGetValue(i, out var data) && data.CanRespawn)
                        {
                            SpawnReactor(i, currentTick);
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
            if (GetReactorCount() == 0)
                yield break;

            for (int i = 0; i < GetReactorCount(); i++)
            {
                var reactor = GetReactor(i);
                if (reactor == null)
                    continue;

                var data = GetReactorData(i);
                if (data == null)
                {
                    yield return (reactor, i, 1f);
                    continue;
                }

                // Skip destroyed/respawning
                if (data.State == ReactorState.Destroyed)
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

            return (data.VisualState, data.StateFrame);
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
                TotalReactors = GetReactorCount(),
                TotalSpawnPoints = _spawnPoints.Count,
                IdleReactors = idleCount,
                ActiveReactors = activeCount,
                DestroyedReactors = destroyedCount,
                RespawningReactors = respawningCount
            };
        }

        internal static ReactorActivationType ResolveActivationType(ReactorInstance reactorInstance)
        {
            return ResolveReactorType(reactorInstance) switch
            {
                ReactorType.ActivatedByAnyHit => ReactorActivationType.Hit,
                ReactorType.ActivatedLeftHit => ReactorActivationType.Hit,
                ReactorType.ActivatedRightHit => ReactorActivationType.Hit,
                ReactorType.ActivatedByHarvesting => ReactorActivationType.Hit,
                ReactorType.ActivatedBySkill => ReactorActivationType.Skill,
                ReactorType.ActivatedByTouch => ReactorActivationType.Touch,
                ReactorType.ActivatedbyItem => ReactorActivationType.Item,
                ReactorType.AnimationOnly => ReactorActivationType.None,
                _ => HasHitAnimation(reactorInstance?.ReactorInfo?.LinkedWzImage) ? ReactorActivationType.Hit : ReactorActivationType.Touch
            };
        }

        internal static ReactorType ResolveReactorType(ReactorInstance reactorInstance)
        {
            WzImage linkedReactorImage = reactorInstance?.ReactorInfo?.LinkedWzImage;
            if (linkedReactorImage == null)
                return ReactorType.UNKNOWN;

            WzImageProperty infoProperty = WzInfoTools.GetRealProperty(linkedReactorImage["info"]);
            if (infoProperty == null)
                return ReactorType.UNKNOWN;

            int? reactorTypeValue =
                TryReadIntProperty(WzInfoTools.GetRealProperty(infoProperty["reactorType"])) ??
                TryReadIntProperty(WzInfoTools.GetRealProperty(infoProperty["type"]));

            if (!reactorTypeValue.HasValue)
                return ReactorType.UNKNOWN;

            return Enum.IsDefined(typeof(ReactorType), reactorTypeValue.Value)
                ? (ReactorType)reactorTypeValue.Value
                : ReactorType.UNKNOWN;
        }

        private static bool IsAttackDirectionValid(ReactorType reactorType, Rectangle attackBounds, Rectangle reactorBounds)
        {
            if (attackBounds.Width <= 0 || reactorBounds.Width <= 0)
                return false;

            float reactorCenterX = reactorBounds.Center.X;
            float attackCenterX = attackBounds.Center.X;

            return reactorType switch
            {
                ReactorType.ActivatedLeftHit => attackCenterX <= reactorCenterX,
                ReactorType.ActivatedRightHit => attackCenterX >= reactorCenterX,
                _ => true
            };
        }

        private static int? TryReadIntProperty(WzImageProperty property)
        {
            if (property is WzIntProperty intProperty)
                return intProperty.Value;

            object value = property?.WzValue;
            if (value == null)
                return null;

            if (value is int intValue)
                return intValue;

            return int.TryParse(value.ToString(), out int parsedValue) ? parsedValue : null;
        }

        private static bool HasHitAnimation(WzImage linkedReactorImage)
        {
            if (linkedReactorImage?.WzProperties == null)
                return false;

            foreach (WzImageProperty property in linkedReactorImage.WzProperties)
            {
                if (!int.TryParse(property?.Name, out _))
                    continue;

                if (WzInfoTools.GetRealProperty(property?["hit"]) != null)
                    return true;
            }

            return false;
        }

        private void ApplyInteractionMetadata(ReactorInstance reactorInstance, ReactorRuntimeData data)
        {
            if (reactorInstance == null || data == null)
                return;

            ReactorInteractionMetadata interactionMetadata = ResolveInteractionMetadata(reactorInstance);
            data.ReactorType = interactionMetadata.ReactorType;
            data.PrimaryActivationType = interactionMetadata.ActivationType;
            data.ActivationType = interactionMetadata.ActivationType;
            data.SupportedActivationTypes = interactionMetadata.SupportedActivationTypes;
            data.RequiredItemId = interactionMetadata.RequiredItemId;
            data.RequiredQuestId = interactionMetadata.RequiredQuestId;
            data.RequiredQuestState = interactionMetadata.RequiredQuestState;
            data.ScriptNames = interactionMetadata.ScriptNames;
        }

        private void ApplyPacketReactorState(int index, int state, int x, int y, bool flip, int currentTick)
        {
            ReactorRuntimeData data = GetReactorData(index);
            ReactorItem reactor = GetReactor(index);
            if (data == null || reactor == null)
            {
                return;
            }

            reactor.SetWorldPosition(x, y);
            reactor.SetFlipState(flip);
            reactor.SetAnimationState(state, currentTick);
            data.VisualState = state;
            data.StateFrame = 0;
            data.StateStartTime = currentTick;
            data.Alpha = 1f;
            data.PacketLeavePending = false;
            data.PacketHitStartTime = 0;
            data.PacketStateEndTime = 0;
            data.PacketAnimationEndTime = 0;
            data.PacketAnimationSourceState = -1;
            ClearPreferredAuthoredOrder(data);

            if (index < _spawnPoints.Count)
            {
                ReactorSpawnPoint spawnPoint = _spawnPoints[index];
                spawnPoint.X = x;
                spawnPoint.Y = y;
                spawnPoint.Flip = flip;
                spawnPoint.IsActive = true;
                spawnPoint.CurrentReactor = reactor;
            }
        }

        private void SyncPacketScriptPublication(ReactorItem reactor, ReactorRuntimeData data, int currentTick)
        {
            if (reactor == null || data == null)
            {
                return;
            }

            bool shouldPublish = data.VisualState != reactor.GetInitialState();
            PublishScriptState(reactor, data, shouldPublish, currentTick);
        }

        private void TrackPacketObjectId(int index, int? packetObjectId)
        {
            if (packetObjectId is int objectId && objectId > 0)
            {
                _reactorIndicesByPacketObjectId[objectId] = index;
            }
        }

        private void UntrackPacketObjectId(int? packetObjectId)
        {
            if (packetObjectId is int objectId && objectId > 0)
            {
                _reactorIndicesByPacketObjectId.Remove(objectId);
            }
        }

        private void AddReactorNameLookup(string name, int index)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            if (!_reactorsByName.TryGetValue(name, out List<int> indices))
            {
                indices = new List<int>();
                _reactorsByName[name] = indices;
            }

            if (!indices.Contains(index))
            {
                indices.Add(index);
            }
        }

        private void RemoveReactorNameLookup(string name, int index)
        {
            if (string.IsNullOrEmpty(name) || !_reactorsByName.TryGetValue(name, out List<int> indices))
            {
                return;
            }

            indices.Remove(index);
            if (indices.Count == 0)
            {
                _reactorsByName.Remove(name);
            }
        }

        private static bool SupportsActivationType(ReactorRuntimeData data, ReactorActivationType activationType)
        {
            if (data == null || activationType == ReactorActivationType.None)
            {
                return false;
            }

            ReactorActivationTypeMask supportedTypes = data.SupportedActivationTypes == ReactorActivationTypeMask.None
                ? ToActivationMask(data.ActivationType)
                : data.SupportedActivationTypes;
            return (supportedTypes & ToActivationMask(activationType)) != 0;
        }

        private static bool CanActivateWith(
            ReactorItem reactor,
            ReactorRuntimeData data,
            ReactorActivationType activationType,
            int activationValue = 0)
        {
            if (reactor == null
                || data == null
                || data.State != ReactorState.Idle
                || !SupportsActivationType(data, activationType))
            {
                return false;
            }

            return reactor.CanActivateFromState(
                data.VisualState,
                new ReactorTransitionRequest(activationType, data.ReactorType, activationValue));
        }

        private int GetReactorCount()
        {
            return Math.Max(_reactors?.Length ?? 0, _spawnPoints.Count);
        }

        private int GetActivationDuration(int index)
        {
            ReactorItem reactor = GetReactor(index);
            ReactorRuntimeData data = GetReactorData(index);
            if (reactor == null || data == null)
                return ACTIVATION_ANIMATION_TIME;

            int stateDuration = reactor.GetStateDuration(data.VisualState);
            return stateDuration > 0 ? stateDuration : ACTIVATION_ANIMATION_TIME;
        }

        private static bool ShouldAutoChainStates(ReactorRuntimeData data)
        {
            return data != null
                && data.ActivationType != ReactorActivationType.Hit
                && data.ActivationType != ReactorActivationType.None;
        }

        private static ReactorActivationTypeMask ResolveSupportedActivationTypes(
            ReactorActivationType primaryActivationType,
            int? requiredItemId,
            int? requiredQuestId,
            HashSet<int> authoredEventTypes = null)
        {
            ReactorActivationTypeMask supportedTypes = ToActivationMask(primaryActivationType);
            authoredEventTypes ??= new HashSet<int>();

            foreach (int eventType in authoredEventTypes)
            {
                supportedTypes |= EventTypeToActivationMask(eventType);
            }

            if (requiredItemId.HasValue)
            {
                supportedTypes |= ReactorActivationTypeMask.Item;
            }

            if (authoredEventTypes.Contains(100))
            {
                supportedTypes |= ReactorActivationTypeMask.Quest;
            }

            return supportedTypes == ReactorActivationTypeMask.None
                ? ReactorActivationTypeMask.Touch
                : supportedTypes;
        }

        private bool TryApplyTimedStateTransition(int index, ReactorItem reactor, ReactorRuntimeData data, int currentTick)
        {
            if (reactor == null
                || data == null
                || !TryResolveNextVisualState(
                    reactor,
                    data,
                    new ReactorTransitionRequest(ReactorActivationType.Time, data.ReactorType, data.ActivationValue),
                    out int nextVisualState,
                    out int selectedAuthoredOrder,
                    allowNumericFallback: false))
            {
                return false;
            }

            if (nextVisualState == reactor.GetInitialState())
            {
                ResetReactor(index, currentTick);
                return true;
            }

            data.VisualState = nextVisualState;
            data.State = ReactorState.Activated;
            data.StateStartTime = currentTick;
            data.StateFrame = 0;
            UpdatePreferredAuthoredOrder(data, ReactorActivationType.Time, selectedAuthoredOrder);
            return true;
        }

        private bool TryActivateTimedIdleReactor(int index, ReactorItem reactor, ReactorRuntimeData data, int currentTick)
        {
            if (reactor == null
                || data == null
                || !TryResolveNextVisualState(
                    reactor,
                    data,
                    new ReactorTransitionRequest(ReactorActivationType.Time, data.ReactorType, data.ActivationValue),
                    out int nextVisualState,
                    out int selectedAuthoredOrder,
                    allowNumericFallback: false))
            {
                return false;
            }

            data.VisualState = nextVisualState;
            data.ActivationType = ReactorActivationType.Time;
            data.ActivationValue = 0;
            data.State = ReactorState.Activated;
            data.StateStartTime = currentTick;
            data.StateFrame = 0;
            data.ActivatingPlayerId = 0;
            UpdatePreferredAuthoredOrder(data, ReactorActivationType.Time, selectedAuthoredOrder);
            PublishScriptState(reactor, data, isEnabled: true, currentTick);
            _onReactorActivated?.Invoke(reactor, 0);
            return true;
        }

        private bool TryAdvanceToNextVisualState(ReactorItem reactor, ReactorRuntimeData data, int currentTick, ReactorActivationType activationType)
        {
            if (reactor == null
                || data == null
                || !TryResolveNextVisualState(
                    reactor,
                    data,
                    new ReactorTransitionRequest(activationType, data.ReactorType, data.ActivationValue),
                    out int nextVisualState,
                    out int selectedAuthoredOrder))
            {
                return false;
            }

            data.VisualState = nextVisualState;
            data.State = ReactorState.Activated;
            data.StateStartTime = currentTick;
            data.StateFrame = 0;
            UpdatePreferredAuthoredOrder(data, activationType, selectedAuthoredOrder);
            return true;
        }

        private static void UpdatePreferredAuthoredOrder(
            ReactorRuntimeData data,
            ReactorActivationType activationType,
            int selectedAuthoredOrder)
        {
            if (data == null)
            {
                return;
            }

            if (selectedAuthoredOrder < 0)
            {
                ClearPreferredAuthoredOrder(data);
                return;
            }

            data.PreferredAuthoredActivationType = activationType;
            data.PreferredAuthoredEventOrder = selectedAuthoredOrder;
        }

        internal static void ApplyPacketProperEventIndexPreference(ReactorRuntimeData data, int properEventIndex)
        {
            if (data == null)
            {
                return;
            }

            data.PacketProperEventIndex = properEventIndex;
            if (properEventIndex < 0)
            {
                ClearPreferredAuthoredOrder(data);
                return;
            }

            ReactorActivationType activationType = data.ActivationType != ReactorActivationType.None
                ? data.ActivationType
                : data.PrimaryActivationType;
            if (activationType == ReactorActivationType.None)
            {
                ClearPreferredAuthoredOrder(data);
                return;
            }

            UpdatePreferredAuthoredOrder(data, activationType, properEventIndex);
        }

        private static void ClearPreferredAuthoredOrder(ReactorRuntimeData data)
        {
            if (data == null)
            {
                return;
            }

            data.PreferredAuthoredActivationType = ReactorActivationType.None;
            data.PreferredAuthoredEventOrder = -1;
        }

        internal static int ResolvePacketClientHitStartTime(int currentTick, int hitStartDelayMs)
        {
            return currentTick + Math.Max(1, Math.Max(0, hitStartDelayMs));
        }

        internal static int ResolvePacketLeaveHitStartTime(int currentTick, bool hasAuthoredStateEvents)
        {
            return hasAuthoredStateEvents
                ? 0
                : currentTick + 400;
        }

        internal static int ResolvePacketStateEndTime(int currentTick, int stateEndDelayTicks)
        {
            return stateEndDelayTicks > 0
                ? currentTick + (stateEndDelayTicks * 100)
                : 0;
        }

        internal static int ResolvePacketAnimationEndTime(int currentTick, int animationDurationMs)
        {
            return animationDurationMs > 0
                ? currentTick + animationDurationMs
                : 0;
        }

        private static int ResolvePacketHitAnimationDuration(ReactorItem reactor, ReactorRuntimeData data)
        {
            if (reactor == null || data == null)
            {
                return 0;
            }

            int sourceState = data.PacketAnimationSourceState >= 0
                ? data.PacketAnimationSourceState
                : data.VisualState;
            return reactor.GetHitAnimationDuration(sourceState);
        }

        private static bool TryResolveNextVisualState(
            ReactorItem reactor,
            ReactorRuntimeData data,
            ReactorTransitionRequest request,
            out int nextVisualState,
            out int selectedAuthoredOrder,
            bool allowNumericFallback = true)
        {
            nextVisualState = data?.VisualState ?? 0;
            selectedAuthoredOrder = -1;
            if (reactor == null || data == null)
            {
                return false;
            }

            int preferredOrder = data.PreferredAuthoredActivationType == request.ActivationType
                ? data.PreferredAuthoredEventOrder
                : -1;
            if (!reactor.TryResolveNextState(
                data.VisualState,
                request,
                out ReactorItem.TransitionSelection selection,
                includeNumericFallback: allowNumericFallback,
                preferredAuthoredOrder: preferredOrder))
            {
                return false;
            }

            nextVisualState = selection.TargetState;
            selectedAuthoredOrder = selection.IsAuthored ? selection.AuthoredOrder : -1;
            return true;
        }

        private static ReactorActivationTypeMask AddSupportedActivationType(
            ReactorActivationTypeMask supportedTypes,
            ReactorActivationType activationType)
        {
            return supportedTypes | ToActivationMask(activationType);
        }

        private static ReactorActivationTypeMask ToActivationMask(ReactorActivationType activationType)
        {
            return activationType switch
            {
                ReactorActivationType.Touch => ReactorActivationTypeMask.Touch,
                ReactorActivationType.Hit => ReactorActivationTypeMask.Hit,
                ReactorActivationType.Skill => ReactorActivationTypeMask.Skill,
                ReactorActivationType.Quest => ReactorActivationTypeMask.Quest,
                ReactorActivationType.Time => ReactorActivationTypeMask.Time,
                ReactorActivationType.Item => ReactorActivationTypeMask.Item,
                _ => ReactorActivationTypeMask.None
            };
        }

        private static ReactorActivationTypeMask EventTypeToActivationMask(int eventType)
        {
            return eventType switch
            {
                0 or 6 => ReactorActivationTypeMask.Touch,
                100 => ReactorActivationTypeMask.Quest,
                1 or 2 or 8 => ReactorActivationTypeMask.Hit,
                5 => ReactorActivationTypeMask.Skill,
                7 or 101 => ReactorActivationTypeMask.Time,
                9 => ReactorActivationTypeMask.Item,
                _ => ReactorActivationTypeMask.None
            };
        }

        private static HashSet<int> GetStateEventTypes(WzImage linkedReactorImage)
        {
            var eventTypes = new HashSet<int>();
            if (linkedReactorImage?.WzProperties == null)
            {
                return eventTypes;
            }

            foreach (WzImageProperty property in linkedReactorImage.WzProperties)
            {
                if (!int.TryParse(property?.Name, out _))
                    continue;

                if (WzInfoTools.GetRealProperty(property?["event"]) is not WzSubProperty eventProperty)
                    continue;

                foreach (WzSubProperty eventNode in eventProperty.WzProperties.OfType<WzSubProperty>())
                {
                    int? eventType = TryGetOptionalInt(WzInfoTools.GetRealProperty(eventNode["type"]));
                    if (eventType.HasValue)
                    {
                        eventTypes.Add(eventType.Value);
                    }
                }
            }

            return eventTypes;
        }

        private static QuestStateType? TryResolveDefaultQuestState(
            int? requiredQuestId,
            HashSet<int> authoredEventTypes)
        {
            if (!requiredQuestId.HasValue)
            {
                return null;
            }

            if (authoredEventTypes != null && authoredEventTypes.Contains(100))
            {
                // Sparse quest reactors often omit info/state while still behaving like
                // active-progress perform-state reactors through their authored quest event.
                return QuestStateType.Started;
            }

            return null;
        }

        private static int? TryInferRequiredQuestIdFromStateEvents(WzImage linkedReactorImage)
        {
            if (linkedReactorImage?.WzProperties == null)
            {
                return null;
            }

            HashSet<int> authoredQuestIds = new HashSet<int>();
            foreach (WzImageProperty property in linkedReactorImage.WzProperties)
            {
                if (!int.TryParse(property?.Name, out _))
                    continue;

                if (WzInfoTools.GetRealProperty(property?["event"]) is not WzSubProperty eventProperty)
                    continue;

                foreach (WzSubProperty eventNode in eventProperty.WzProperties.OfType<WzSubProperty>())
                {
                    int? eventType = TryGetOptionalInt(WzInfoTools.GetRealProperty(eventNode["type"]));
                    if (eventType != 100)
                        continue;

                    foreach (int questId in EnumerateQuestSelectorValues(eventNode))
                    {
                        authoredQuestIds.Add(questId);
                        if (authoredQuestIds.Count > 1)
                        {
                            return null;
                        }
                    }
                }
            }

            return authoredQuestIds.Count == 1 ? authoredQuestIds.First() : null;
        }

        private static IEnumerable<int> EnumerateQuestSelectorValues(WzSubProperty eventNode)
        {
            int?[] selectorCandidates =
            {
                TryGetOptionalInt(WzInfoTools.GetRealProperty(eventNode?["quest"])),
                TryGetOptionalInt(WzInfoTools.GetRealProperty(eventNode?["questID"])),
                TryGetOptionalInt(WzInfoTools.GetRealProperty(eventNode?["questid"])),
                TryGetOptionalInt(WzInfoTools.GetRealProperty(eventNode?["reqQuest"])),
                TryGetOptionalInt(WzInfoTools.GetRealProperty(eventNode?["id"]))
            };

            foreach (int? selectorCandidate in selectorCandidates)
            {
                if (selectorCandidate.GetValueOrDefault() > 0)
                {
                    yield return selectorCandidate.Value;
                }
            }
        }

        private void PublishScriptState(ReactorItem reactor, ReactorRuntimeData data, bool isEnabled, int currentTick)
        {
            if (reactor == null
                || data == null
                || data.ScriptNames == null
                || data.ScriptNames.Count == 0)
            {
                return;
            }

            if (data.ScriptStatePublished == isEnabled)
            {
                return;
            }

            data.ScriptStatePublished = isEnabled;
            _onReactorScriptStateChanged?.Invoke(reactor, data.ScriptNames, isEnabled, currentTick);
        }

        private static Rectangle GetReactorBounds(ReactorInstance instance)
        {
            int width = Math.Max(1, instance?.Width ?? 1);
            int height = Math.Max(1, instance?.Height ?? 1);
            System.Drawing.Point origin = instance?.Origin ?? System.Drawing.Point.Empty;

            return new Rectangle(
                (instance?.X ?? 0) - origin.X,
                (instance?.Y ?? 0) - origin.Y,
                width,
                height);
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
