using System;
using System.Collections.Generic;
using System.Globalization;
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

    internal enum PacketReactorAnimationPhase
    {
        Idle = 0,
        AwaitingHitStart = 1,
        AwaitingLayerCompletion = 2,
        AwaitingAutoHitLayerCompletion = 3,
        AwaitingLeaveCompletion = 4
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
        public int HitOption { get; set; } = -1;
        public int ActivatingPlayerId { get; set; }
        public bool CanRespawn { get; set; } = true;
        public bool IsPacketOwned { get; set; }
        public bool PacketLeavePending { get; set; }
        public int PacketHitStartTime { get; set; }
        public int PacketStateEndTime { get; set; }
        public int PacketAnimationEndTime { get; set; }
        public int PacketAnimationSourceState { get; set; } = -1;
        public int PacketHitAnimationState { get; set; } = -1;
        public int PacketPendingVisualState { get; set; } = -1;
        public int PacketProperEventIndex { get; set; } = -1;
        internal PacketReactorAnimationPhase PacketAnimationPhase { get; set; } = PacketReactorAnimationPhase.Idle;
        public int PacketMoveStartTime { get; set; }
        public int PacketMoveEndTime { get; set; }
        public int PacketMoveStartX { get; set; }
        public int PacketMoveStartY { get; set; }
        public int PacketMoveTargetX { get; set; }
        public int PacketMoveTargetY { get; set; }
        public bool PacketMoveUsesDefaultRelMove { get; set; }
        public bool PacketMoveExplicitlyRequested { get; set; }
        public float PacketObservedMovePixelsPerMs { get; set; }
        public int PacketEnterFadeStartTime { get; set; }
        public int PacketEnterFadeEndTime { get; set; }
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
        public int HitOption { get; init; } = -1;
        public ReactorActivationType ActivationType { get; init; } = ReactorActivationType.Touch;
        public ReactorActivationTypeMask SupportedActivationTypes { get; init; } = ReactorActivationTypeMask.Touch;
        public int? RequiredItemId { get; init; }
        public int? RequiredQuestId { get; init; }
        public QuestStateType? RequiredQuestState { get; init; }
        public IReadOnlyList<string> ScriptNames { get; init; } = Array.Empty<string>();
    }

    public readonly record struct ReactorTouchStateChange(
        ReactorItem Reactor,
        int Index,
        int ObjectId,
        bool UsesPacketObjectId,
        bool IsTouching);

    internal readonly record struct LocalTouchOwnershipPollResult(
        int Index,
        int ObjectId,
        bool IsTouchingNow);

    internal readonly record struct LocalTouchOwnershipDelta(
        int Index,
        int ObjectId,
        bool IsTouching);

    public readonly record struct ReactorFootholdPlacement(int Page, int ZMass);

    internal readonly record struct PacketEnterAuthoredReactorCandidate(
        int Index,
        int AuthoredOrder,
        bool IsPacketNamePresent,
        bool IsLocallyTouched,
        bool ContainsCurrentLocalUserPosition,
        bool MatchesPacketName,
        bool HasExactNameMatch,
        int VisualState);

    internal enum PacketEnterAuthoredReactorSelectionReason
    {
        None = 0,
        ClientSignal = 1,
        WzAuthoredOrderFallback = 2
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
        private const int PACKET_ENTER_FADE_DURATION_MS = 800;
        private const int PACKET_RELMOVE_FALLBACK_DURATION_MS = 800; // Best-effort vtMissing RelMove duration until the vector default is recovered.
        private const int PACKET_RELMOVE_MIN_DURATION_MS = 120;
        private const int PACKET_RELMOVE_MAX_DURATION_MS = 2000;
        #endregion

        #region Collections
        private ReactorItem[] _reactors;
        private readonly Dictionary<int, ReactorRuntimeData> _reactorData = new Dictionary<int, ReactorRuntimeData>();
        private readonly Dictionary<string, List<int>> _reactorsByName = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, int> _reactorIndicesByPacketObjectId = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _reactorsOnLocalUser = new Dictionary<int, int>();
        private readonly Queue<ReactorTouchStateChange> _pendingPacketTouchStateChanges = new Queue<ReactorTouchStateChange>();
        private readonly Queue<int> _pendingPacketTouchRequestRemovalObjectIds = new Queue<int>();
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
        private Action<string> _onReactorLayerSoundRequested;
        private Func<int, int, ReactorFootholdPlacement?> _reactorFootholdPlacementResolver;

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
        public void SetOnReactorLayerSoundRequested(Action<string> callback) => _onReactorLayerSoundRequested = callback;
        public void SetReactorFootholdPlacementResolver(Func<int, int, ReactorFootholdPlacement?> callback) => _reactorFootholdPlacementResolver = callback;

        public void RefreshReactorLayerPlacements()
        {
            if (_reactors == null)
            {
                return;
            }

            for (int i = 0; i < _reactors.Length; i++)
            {
                RefreshReactorLayerPlacement(_reactors[i]);
            }
        }
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
                    HitOption = interactionMetadata.HitOption,
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
                    PacketHitAnimationState = -1,
                    PacketPendingVisualState = -1,
                    PacketProperEventIndex = -1,
                    PacketAnimationPhase = PacketReactorAnimationPhase.Idle,
                    PacketMoveStartTime = 0,
                    PacketMoveEndTime = 0,
                    PacketMoveStartX = instance.X,
                    PacketMoveStartY = instance.Y,
                    PacketMoveTargetX = instance.X,
                    PacketMoveTargetY = instance.Y,
                    PacketMoveUsesDefaultRelMove = false,
                    PacketMoveExplicitlyRequested = false,
                    PacketObservedMovePixelsPerMs = 0f,
                    PacketEnterFadeStartTime = 0,
                    PacketEnterFadeEndTime = 0,
                    RequiredItemId = interactionMetadata.RequiredItemId,
                    RequiredQuestId = interactionMetadata.RequiredQuestId,
                    RequiredQuestState = interactionMetadata.RequiredQuestState,
                    ScriptNames = interactionMetadata.ScriptNames,
                    ScriptStatePublished = false,
                    PreferredAuthoredEventOrder = -1,
                    PreferredAuthoredActivationType = ReactorActivationType.None
                };
                _reactorData[i] = data;
                RefreshReactorLayerPlacement(reactor);

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
            _reactorsOnLocalUser.Clear();
            _pendingPacketTouchStateChanges.Clear();
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

        public bool TryGetReactorStatesByName(string name, out int state, out int visualState)
        {
            state = 0;
            visualState = 0;
            if (string.IsNullOrWhiteSpace(name) || !_reactorsByName.TryGetValue(name, out List<int> indices))
            {
                return false;
            }

            for (int i = 0; i < indices.Count; i++)
            {
                ReactorRuntimeData data = GetReactorData(indices[i]);
                if (data == null)
                {
                    continue;
                }

                state = (int)data.State;
                visualState = data.VisualState;
                return true;
            }

            return false;
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
        /// <param name="playerWidth">Unused. The client checks the local vec-ctrl position, not the avatar rectangle.</param>
        /// <param name="playerHeight">Unused. The client checks the local vec-ctrl position, not the avatar rectangle.</param>
        /// <returns>List of touch-able reactors containing the local-user position</returns>
        public List<(ReactorItem reactor, int index)> FindTouchReactorAroundLocalUser(
            float playerX, float playerY,
            int playerWidth = 40, int playerHeight = 60,
            int? currentTick = null)
        {
            var results = new List<(ReactorItem reactor, int index)>();
            _ = playerWidth;
            _ = playerHeight;

            if (GetReactorCount() == 0)
                return results;

            int resolvedTick = currentTick ?? _lastUpdateTick;

            for (int i = 0; i < GetReactorCount(); i++)
            {
                var reactor = GetReactor(i);
                if (reactor?.ReactorInstance == null)
                    continue;

                var data = GetReactorData(i);
                if (!CanPollLocalUserTouchReactor(data))
                {
                    continue;
                }

                // Only touch-type reactors
                if (!CanPollLocalUserTouchOwnershipCandidate(data, SupportsActivationType(data, ReactorActivationType.Touch)))
                {
                    continue;
                }

                // Get reactor hitbox
                Rectangle reactorRect = reactor.GetCurrentBounds(resolvedTick);

                if (DoesClientTouchBoundsContainPosition(reactorRect, playerX, playerY))
                {
                    results.Add((reactor, i));
                }
            }

            return results;
        }

        /// <summary>
        /// Refreshes the local touch-owner set the same way CUserLocal::CheckReactor_Collision
        /// drives CReactorPool::FindTouchReactorAroundLocalUser in the client.
        /// </summary>
        public List<ReactorTouchStateChange> RefreshTouchReactorsAroundLocalUser(
            float playerX,
            float playerY,
            int? currentTick = null)
        {
            int resolvedTick = currentTick ?? _lastUpdateTick;
            var pollResults = new List<LocalTouchOwnershipPollResult>();

            for (int i = 0; i < GetReactorCount(); i++)
            {
                ReactorItem reactor = GetReactor(i);
                ReactorRuntimeData data = GetReactorData(i);
                if (reactor?.ReactorInstance == null || data == null)
                {
                    continue;
                }

                int objectId = ResolveLocalTouchObjectId(data);
                if (objectId == 0)
                {
                    continue;
                }

                if (!ShouldIncludeClientTouchOwnershipPollResult(data, SupportsActivationType(data, ReactorActivationType.Touch)))
                {
                    continue;
                }

                Rectangle bounds = reactor.GetCurrentBounds(resolvedTick);
                bool isTouchingNow = ResolveClientTouchOwnershipPollContainment(
                    bounds,
                    playerX,
                    playerY,
                    _reactorsOnLocalUser.ContainsKey(objectId));

                pollResults.Add(new LocalTouchOwnershipPollResult(i, objectId, isTouchingNow));
            }

            IReadOnlyList<LocalTouchOwnershipDelta> ownershipDeltas = ApplyClientOrderedLocalTouchOwnershipDiffs(
                pollResults,
                _reactorsOnLocalUser);
            List<ReactorTouchStateChange> changes = new();

            foreach (LocalTouchOwnershipDelta delta in ownershipDeltas)
            {
                ReactorRuntimeData data = GetReactorData(delta.Index);
                changes.Add(new ReactorTouchStateChange(
                    GetReactor(delta.Index),
                    delta.Index,
                    delta.ObjectId,
                    UsesPacketObjectIdForLocalTouch(data, delta.ObjectId),
                    IsTouching: delta.IsTouching));
            }

            return changes;
        }

        internal static IReadOnlyList<LocalTouchOwnershipDelta> ApplyClientOrderedLocalTouchOwnershipDiffs(
            IReadOnlyList<LocalTouchOwnershipPollResult> pollResults,
            Dictionary<int, int> reactorsOnLocalUser)
        {
            if (reactorsOnLocalUser == null)
            {
                return Array.Empty<LocalTouchOwnershipDelta>();
            }

            List<LocalTouchOwnershipDelta> deltas = new();
            HashSet<int> seenObjectIds = new();

            if (pollResults != null)
            {
                foreach (LocalTouchOwnershipPollResult pollResult in pollResults)
                {
                    if (pollResult.ObjectId == 0)
                    {
                        continue;
                    }

                    seenObjectIds.Add(pollResult.ObjectId);
                    bool wasTouched = reactorsOnLocalUser.TryGetValue(pollResult.ObjectId, out int previousIndex);
                    if (pollResult.IsTouchingNow)
                    {
                        reactorsOnLocalUser[pollResult.ObjectId] = pollResult.Index;
                        if (!wasTouched)
                        {
                            deltas.Add(new LocalTouchOwnershipDelta(
                                pollResult.Index,
                                pollResult.ObjectId,
                                IsTouching: true));
                        }

                        continue;
                    }

                    if (!wasTouched)
                    {
                        continue;
                    }

                    reactorsOnLocalUser.Remove(pollResult.ObjectId);
                    deltas.Add(new LocalTouchOwnershipDelta(
                        previousIndex,
                        pollResult.ObjectId,
                        IsTouching: false));
                }
            }

            foreach ((int objectId, int index) in reactorsOnLocalUser
                .OrderBy(static entry => entry.Value)
                .ThenBy(static entry => entry.Key)
                .ToArray())
            {
                if (seenObjectIds.Contains(objectId))
                {
                    continue;
                }

                reactorsOnLocalUser.Remove(objectId);
                deltas.Add(new LocalTouchOwnershipDelta(
                    index,
                    objectId,
                    IsTouching: false));
            }

            return deltas;
        }

        public List<ReactorTouchStateChange> DrainPendingPacketTouchStateChanges()
        {
            if (_pendingPacketTouchStateChanges.Count == 0)
            {
                return new List<ReactorTouchStateChange>();
            }

            var changes = new List<ReactorTouchStateChange>(_pendingPacketTouchStateChanges.Count);
            while (_pendingPacketTouchStateChanges.Count > 0)
            {
                changes.Add(_pendingPacketTouchStateChanges.Dequeue());
            }

            return changes;
        }

        public List<int> DrainPendingPacketTouchRequestRemovalObjectIds()
        {
            if (_pendingPacketTouchRequestRemovalObjectIds.Count == 0)
            {
                return new List<int>();
            }

            HashSet<int> objectIds = new();
            List<int> orderedObjectIds = new();
            while (_pendingPacketTouchRequestRemovalObjectIds.Count > 0)
            {
                int objectId = _pendingPacketTouchRequestRemovalObjectIds.Dequeue();
                if (objectId > 0 && objectIds.Add(objectId))
                {
                    orderedObjectIds.Add(objectId);
                }
            }

            return orderedObjectIds;
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
        /// Find hit-activatable reactors intersecting a world-space basic-attack area.
        /// Mirrors CReactorPool::FindHitReactor used by CField_GuildBoss::BasicActionAttack.
        /// </summary>
        public List<(ReactorItem reactor, int index)> FindHitReactorsInBounds(Rectangle attackBounds, int? currentTick = null)
        {
            var results = new List<(ReactorItem reactor, int index)>();

            if (GetReactorCount() == 0 || attackBounds.Width <= 0 || attackBounds.Height <= 0)
            {
                return results;
            }

            int resolvedTick = currentTick ?? _lastUpdateTick;

            for (int i = 0; i < GetReactorCount(); i++)
            {
                var reactor = GetReactor(i);
                if (reactor?.ReactorInstance == null)
                {
                    continue;
                }

                ReactorRuntimeData data = GetReactorData(i);
                if (!CanParticipateInHitReactorSearch(data) || !MeetsQuestRequirement(data))
                {
                    continue;
                }

                Rectangle reactorBounds = reactor.GetCurrentBounds(resolvedTick);
                if (!IsAttackDirectionValid(data.ReactorType, attackBounds, reactorBounds))
                {
                    continue;
                }

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
            int previousVisualState = data.VisualState;
            if (!reactor.CanActivateFromState(data.VisualState, request))
            {
                return false;
            }

            if (TryResolveNextVisualState(reactor, data, request, out int nextVisualState, out int selectedAuthoredOrder))
            {
                data.VisualState = nextVisualState;
                SyncReactorVisualState(reactor, data, currentTick);
                UpdatePreferredAuthoredOrder(data, activationType, selectedAuthoredOrder);
                RefreshReactorLayerPlacement(reactor);
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

            if (activationType == ReactorActivationType.Hit)
            {
                TryPlayReactorHitSound(reactor, previousVisualState);
            }

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
                        SyncReactorVisualState(reactor, data, currentTick);
                        data.State = ReactorState.Activated;
                        data.StateStartTime = currentTick;
                        data.StateFrame = 0;
                        data.ActivatingPlayerId = playerId;
                        UpdatePreferredAuthoredOrder(data, ReactorActivationType.Hit, selectedAuthoredOrder);
                        RefreshReactorLayerPlacement(reactor);
                        TryPlayReactorHitSound(reactor, data.PacketHitAnimationState >= 0 ? data.PacketHitAnimationState : data.VisualState);
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
            data.PacketAnimationPhase = PacketReactorAnimationPhase.Idle;
            QueuePacketTouchRequestRemoval(data);
            ClearLocalTouchOwnership(index, data);
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
            SyncReactorVisualState(reactor, data, currentTick);
            data.HitCount = 0;
            data.Alpha = 1f;
            data.ActivationType = data.PrimaryActivationType;
            data.ActivationValue = 0;
            data.PacketLeavePending = false;
            data.PacketHitStartTime = 0;
            data.PacketStateEndTime = 0;
            data.PacketAnimationEndTime = 0;
            data.PacketAnimationSourceState = -1;
            data.PacketHitAnimationState = -1;
            data.PacketPendingVisualState = -1;
            data.PacketProperEventIndex = -1;
            data.PacketAnimationPhase = PacketReactorAnimationPhase.Idle;
            data.PacketMoveStartTime = 0;
            data.PacketMoveEndTime = 0;
            data.PacketMoveStartX = reactor?.ReactorInstance?.X ?? 0;
            data.PacketMoveStartY = reactor?.ReactorInstance?.Y ?? 0;
            data.PacketMoveTargetX = reactor?.ReactorInstance?.X ?? 0;
            data.PacketMoveTargetY = reactor?.ReactorInstance?.Y ?? 0;
            data.PacketMoveUsesDefaultRelMove = false;
            data.PacketMoveExplicitlyRequested = false;
            data.PacketObservedMovePixelsPerMs = 0f;
            data.PacketEnterFadeStartTime = 0;
            data.PacketEnterFadeEndTime = 0;
            data.PreferredAuthoredEventOrder = -1;
            data.PreferredAuthoredActivationType = ReactorActivationType.None;
            PublishScriptState(reactor, data, isEnabled: false, currentTick);
            RefreshReactorLayerPlacement(reactor);
        }

        public void RefreshQuestReactors(int currentTick)
        {
            if (_questStateProvider == null || _reactors == null)
                return;

            foreach (var kvp in _reactorData)
            {
                int index = kvp.Key;
                ReactorRuntimeData data = kvp.Value;
                ReactorItem reactor = GetReactor(index);
                if (data?.RequiredQuestId is not int)
                    continue;

                bool matchesQuest = MeetsQuestRequirement(data);
                if (SupportsActivationType(data, ReactorActivationType.Quest))
                {
                    if (matchesQuest)
                    {
                        if (data.State == ReactorState.Idle
                            && ShouldAutoActivateQuestRefresh(
                                data,
                                reactor?.GetAuthoredEventTypes(data.VisualState)))
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
                ?? TryInferDirectionalReactorTypeFromStateEvents(authoredEventTypes)
                ?? ReactorType.UNKNOWN;
            int hitOption = TryGetOptionalInt(infoProperty?["reactorType"])
                ?? TryGetOptionalInt(infoProperty?["type"])
                ?? TryResolveHitOptionFromReactorType(reactorType)
                ?? -1;

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
                HitOption = hitOption,
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

        internal static ReactorType? TryInferDirectionalReactorTypeFromStateEvents(IReadOnlySet<int> authoredEventTypes)
        {
            if (authoredEventTypes == null)
            {
                return null;
            }

            bool hasLeftHitOnlyEvent = authoredEventTypes.Contains(3);
            bool hasRightHitOnlyEvent = authoredEventTypes.Contains(4);
            if (hasLeftHitOnlyEvent && !hasRightHitOnlyEvent)
            {
                return ReactorType.ActivatedLeftHit;
            }

            if (hasRightHitOnlyEvent && !hasLeftHitOnlyEvent)
            {
                return ReactorType.ActivatedRightHit;
            }

            if (hasLeftHitOnlyEvent && hasRightHitOnlyEvent)
            {
                return ReactorType.ActivatedByAnyHit;
            }

            return null;
        }

        internal static int? TryResolveHitOptionFromReactorType(ReactorType reactorType)
        {
            return reactorType switch
            {
                ReactorType.ActivatedLeftHit => 0,
                ReactorType.ActivatedRightHit => 1,
                ReactorType.ActivatedByAnyHit => 0,
                _ => null
            };
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

            if (eventTypes.Any(eventType => eventType is 1 or 2 or 3 or 4 or 8))
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
                    ClearPacketVisualOwnershipState(data);
                    data.PacketPendingVisualState = -1;
                    data.PacketProperEventIndex = -1;
                    data.PacketAnimationPhase = PacketReactorAnimationPhase.Idle;
                    data.PacketMoveStartTime = 0;
                    data.PacketMoveEndTime = 0;
                    data.PacketMoveStartX = newReactor.ReactorInstance?.X ?? 0;
                    data.PacketMoveStartY = newReactor.ReactorInstance?.Y ?? 0;
                    data.PacketMoveTargetX = newReactor.ReactorInstance?.X ?? 0;
                    data.PacketMoveTargetY = newReactor.ReactorInstance?.Y ?? 0;
                    data.PacketMoveUsesDefaultRelMove = false;
                    data.PacketMoveExplicitlyRequested = false;
                    data.PacketObservedMovePixelsPerMs = 0f;
                    data.PacketEnterFadeStartTime = 0;
                    data.PacketEnterFadeEndTime = 0;
                    data.ScriptStatePublished = false;
                    data.PreferredAuthoredEventOrder = -1;
                    data.PreferredAuthoredActivationType = ReactorActivationType.None;
                    RefreshReactorLayerPlacement(newReactor);
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
                    PacketProperEventIndex = -1,
                    PacketAnimationPhase = PacketReactorAnimationPhase.Idle
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
            float? localPlayerX,
            float? localPlayerY,
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
                ReactorRuntimeData existingTouchData = GetReactorData(existingIndex);
                bool promoteExistingTouch = ShouldPromotePacketEnterLocalTouch(
                    IsLocallyTouchedReactor(existingIndex, existingTouchData));
                ApplyPacketOwnershipToReactor(existingIndex, packetObjectId, canRespawn: false, promoteLocalUserTouch: promoteExistingTouch);
                ApplyPacketReactorState(existingIndex, initialState, x, y, flip, currentTick);
                ReactorRuntimeData existingData = GetReactorData(existingIndex);
                if (existingData != null)
                {
                    existingData.State = ReactorState.Idle;
                }

                BeginPacketEnterFade(GetReactorData(existingIndex), currentTick);
                SyncPacketScriptPublication(GetReactor(existingIndex), GetReactorData(existingIndex), currentTick);
                reactorIndex = existingIndex;
                message = $"Updated packet-owned reactor {packetObjectId} as template {reactorId} at ({x}, {y}).";
                return true;
            }

            if (TryFindAuthoredReactorForPacketEnter(
                reactorId,
                initialState,
                x,
                y,
                flip,
                name,
                currentTick,
                localPlayerX,
                localPlayerY,
                out PacketEnterAuthoredReactorSelectionReason selectionReason,
                out int authoredIndex))
            {
                ReactorRuntimeData authoredTouchData = GetReactorData(authoredIndex);
                bool promoteAuthoredTouch = ShouldPromotePacketEnterLocalTouch(
                    IsLocallyTouchedReactor(authoredIndex, authoredTouchData));
                ApplyPacketOwnershipToReactor(authoredIndex, packetObjectId, canRespawn: false, promoteLocalUserTouch: promoteAuthoredTouch);
                ApplyPacketReactorState(authoredIndex, initialState, x, y, flip, currentTick);
                ReactorRuntimeData authoredData = GetReactorData(authoredIndex);
                if (authoredData != null)
                {
                    authoredData.State = ReactorState.Idle;
                }

                BeginPacketEnterFade(authoredData, currentTick);
                SyncPacketScriptPublication(GetReactor(authoredIndex), authoredData, currentTick);
                reactorIndex = authoredIndex;
                string fallbackSuffix = selectionReason == PacketEnterAuthoredReactorSelectionReason.WzAuthoredOrderFallback
                    ? " using WZ authored reactor order fallback."
                    : ".";
                message = $"Bound packet-owned reactor {packetObjectId} to authored template {reactorId} at ({x}, {y}){fallbackSuffix}";
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
                PacketHitAnimationState = -1,
                PacketPendingVisualState = -1,
                PacketProperEventIndex = -1,
                PacketAnimationPhase = PacketReactorAnimationPhase.Idle,
                PacketMoveStartTime = 0,
                PacketMoveEndTime = 0,
                PacketMoveStartX = x,
                PacketMoveStartY = y,
                PacketMoveTargetX = x,
                PacketMoveTargetY = y,
                PacketMoveUsesDefaultRelMove = false,
                PacketMoveExplicitlyRequested = false,
                PacketObservedMovePixelsPerMs = 0f,
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

            ReactorRuntimeData runtimeData = GetReactorData(spawnPoint.SpawnId);
            if (runtimeData != null)
            {
                ReactorInteractionMetadata interactionMetadata = ResolveInteractionMetadata(reactor.ReactorInstance);
                runtimeData.PrimaryActivationType = interactionMetadata.ActivationType;
                runtimeData.ReactorType = interactionMetadata.ReactorType;
                runtimeData.HitOption = interactionMetadata.HitOption;
                runtimeData.ActivationType = interactionMetadata.ActivationType;
                runtimeData.SupportedActivationTypes = interactionMetadata.SupportedActivationTypes;
                runtimeData.RequiredItemId = interactionMetadata.RequiredItemId;
                runtimeData.RequiredQuestId = interactionMetadata.RequiredQuestId;
                runtimeData.RequiredQuestState = interactionMetadata.RequiredQuestState;
                runtimeData.ScriptNames = interactionMetadata.ScriptNames;
            }

            ApplyPacketReactorState(spawnPoint.SpawnId, initialState, x, y, flip, currentTick);
            ReactorRuntimeData enteredData = GetReactorData(spawnPoint.SpawnId);
            if (enteredData != null)
            {
                enteredData.State = ReactorState.Idle;
            }

            BeginPacketEnterFade(GetReactorData(spawnPoint.SpawnId), currentTick);
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

            ResolvePacketMutationFallbackVisualOwnershipSourceStates(
                reactor.GetActiveAnimationState(),
                reactor.TransientHitLayerSourceState,
                data.VisualState,
                out int fallbackAnimationOwnerState,
                out int fallbackHitOwnerState);
            ResolvePacketVisualOwnershipSourceStatesForMutation(
                data,
                fallbackAnimationOwnerState,
                fallbackHitOwnerState,
                out int packetAnimationSourceState,
                out int packetHitAnimationState);
            bool wasAnimationClockRunning = IsPacketAnimationClockRunning(data);
            bool previousLeavePending = data.PacketLeavePending;
            int previousHitStartTime = data.PacketHitStartTime;
            int previousAnimationEndTime = data.PacketAnimationEndTime;
            PacketReactorAnimationPhase previousAnimationPhase = data.PacketAnimationPhase;
            int currentX = reactor.CurrentWorldX;
            int currentY = reactor.CurrentWorldY;
            bool shouldRelMoveToStateTarget = reactor.TemplateMoveEnabled
                && stateEndDelayTicks > 0
                && (currentX != x || currentY != y);
            ApplyPacketReactorState(
                index,
                state,
                shouldRelMoveToStateTarget ? currentX : x,
                shouldRelMoveToStateTarget ? currentY : y,
                reactor.ReactorInstance?.Flip ?? false,
                currentTick,
                applyAnimationState: false);
            if (!wasAnimationClockRunning)
            {
                data.PacketHitStartTime = ResolvePacketClientHitStartTime(currentTick, hitStartDelayMs);
                data.PacketAnimationPhase = PacketReactorAnimationPhase.AwaitingHitStart;
            }
            else
            {
                data.PacketLeavePending = previousLeavePending;
                data.PacketHitStartTime = previousHitStartTime;
                data.PacketAnimationEndTime = previousAnimationEndTime;
                data.PacketAnimationPhase = previousAnimationPhase;
            }

            ApplyPacketProperEventIndexPreference(data, properEventIndex);
            data.PacketStateEndTime = ResolvePacketStateEndTime(currentTick, stateEndDelayTicks);
            data.PacketAnimationSourceState = packetAnimationSourceState;
            data.PacketHitAnimationState = packetHitAnimationState;
            data.PacketPendingVisualState = state;
            ConfigurePacketStateMovement(
                reactor,
                data,
                currentX,
                currentY,
                x,
                y,
                data.PacketStateEndTime);
            if (index < _spawnPoints.Count)
            {
                _spawnPoints[index].X = x;
                _spawnPoints[index].Y = y;
            }

            data.State = ReactorState.Activated;
            data.StateStartTime = currentTick;
            SyncPacketScriptPublication(reactor, data, currentTick);
            RefreshReactorLayerPlacement(reactor);
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

            int currentX = reactor.CurrentWorldX;
            int currentY = reactor.CurrentWorldY;
            int moveEndTime = ResolvePacketMovePacketEndTime(
                currentX,
                currentY,
                x,
                y,
                currentTick,
                data.PacketStateEndTime,
                data.PacketObservedMovePixelsPerMs);
            bool usesDefaultRelMove = moveEndTime > currentTick;

            ConfigurePacketStateMovement(
                reactor,
                data,
                currentX,
                currentY,
                x,
                y,
                moveEndTime,
                allowDefaultRelMove: moveEndTime > currentTick);
            data.PacketMoveUsesDefaultRelMove = usesDefaultRelMove;
            data.PacketMoveExplicitlyRequested = data.PacketMoveEndTime > currentTick;

            if (data.PacketMoveEndTime > currentTick)
            {
                StartPacketStateMovement(reactor, data, currentTick);
            }
            else
            {
                reactor.SetWorldPosition(x, y);
                ClearPacketStateMovement(reactor, data, snapToTarget: false);
            }

            if (index < _spawnPoints.Count)
            {
                _spawnPoints[index].X = x;
                _spawnPoints[index].Y = y;
            }

            data.StateStartTime = currentTick;
            RefreshReactorLayerPlacement(reactor);
            message = moveEndTime > currentTick
                ? $"Moved packet-owned reactor {packetObjectId} to ({x}, {y}) with fallback RelMove timing."
                : $"Moved packet-owned reactor {packetObjectId} to ({x}, {y}).";
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

            ResolvePacketMutationFallbackVisualOwnershipSourceStates(
                reactor.GetActiveAnimationState(),
                reactor.TransientHitLayerSourceState,
                data.VisualState,
                out int fallbackAnimationOwnerState,
                out int fallbackHitOwnerState);
            ResolvePacketVisualOwnershipSourceStatesForMutation(
                data,
                fallbackAnimationOwnerState,
                fallbackHitOwnerState,
                out int packetAnimationSourceState,
                out int packetHitAnimationState);
            int remainingCurrentAnimationDuration = reactor.GetRemainingStoppedAnimationDuration(currentTick);
            ApplyPacketReactorState(index, state, x, y, reactor.ReactorInstance?.Flip ?? false, currentTick, applyAnimationState: false);
            data.PacketLeavePending = true;
            QueuePacketTouchRequestRemoval(data);
            ClearLocalTouchOwnership(index, data);
            data.PacketProperEventIndex = -2;
            data.PacketAnimationSourceState = packetAnimationSourceState;
            data.PacketHitAnimationState = packetHitAnimationState;
            bool hasAuthoredStateEvents = reactor.HasAuthoredEventInfo(state);
            data.PacketPendingVisualState = hasAuthoredStateEvents ? -1 : state;
            data.PacketHitStartTime = ResolvePacketLeaveHitStartTime(currentTick, hasAuthoredStateEvents);
            data.PacketAnimationEndTime = data.PacketHitStartTime == 0
                ? ResolvePacketAnimationEndTime(currentTick, remainingCurrentAnimationDuration)
                : 0;
            data.PacketAnimationPhase = PacketReactorAnimationPhase.AwaitingLeaveCompletion;
            data.PacketStateEndTime = 0;
            data.CanRespawn = false;
            RefreshReactorLayerPlacement(reactor);
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
                    if (data.PacketEnterFadeEndTime > 0)
                    {
                        data.Alpha = ResolvePacketEnterFadeAlpha(
                            data.PacketEnterFadeStartTime,
                            data.PacketEnterFadeEndTime,
                            currentTick);
                        if (currentTick >= data.PacketEnterFadeEndTime)
                        {
                            data.PacketEnterFadeStartTime = 0;
                            data.PacketEnterFadeEndTime = 0;
                            data.Alpha = 1f;
                        }
                    }

                    if (data.PacketLeavePending)
                    {
                        if (data.PacketHitStartTime > 0 && currentTick >= data.PacketHitStartTime)
                        {
                            data.PacketHitStartTime = 0;
                            int hitAnimationDuration = StartPacketHitAnimation(reactor, data, currentTick, _onReactorLayerSoundRequested);
                            data.PacketAnimationPhase = hitAnimationDuration > 0
                                ? PacketReactorAnimationPhase.AwaitingLeaveCompletion
                                : PacketReactorAnimationPhase.Idle;
                            if (hitAnimationDuration > 0)
                            {
                                data.PacketAnimationEndTime = ResolvePacketAnimationEndTime(currentTick, hitAnimationDuration);
                            }
                            else
                            {
                                data.PacketAnimationEndTime = ResolvePacketLeaveNoHitLayerAnimationEndTime(
                                    currentTick,
                                    data.PacketProperEventIndex,
                                    data.PacketAnimationEndTime,
                                    reactor?.GetRemainingStoppedAnimationDuration(currentTick) ?? 0);
                            }
                        }

                        if (data.PacketHitStartTime == 0
                            && (data.PacketAnimationEndTime <= 0 || currentTick >= data.PacketAnimationEndTime))
                        {
                            reactor?.ClearTransientAnimation();
                            ClearPacketStateMovement(reactor, data, snapToTarget: true);
                            data.PacketAnimationPhase = PacketReactorAnimationPhase.Idle;
                            DestroyReactor(index, playerId: 0, currentTick);
                        }

                        continue;
                    }

                    if (data.State == ReactorState.Activated
                        && data.PacketHitStartTime > 0
                        && currentTick > data.PacketHitStartTime)
                    {
                        data.PacketHitStartTime = 0;
                        int hitAnimationDuration = StartPacketHitAnimation(reactor, data, currentTick, _onReactorLayerSoundRequested);
                        if (hitAnimationDuration > 0)
                        {
                            data.PacketAnimationPhase = ResolvePacketAnimationPhaseAfterLoad(data.PacketProperEventIndex, loadedHitLayer: true);
                            data.PacketAnimationEndTime = ResolvePacketAnimationEndTime(currentTick, hitAnimationDuration);
                        }
                        else if (ShouldRefuseUnmatchedPacketAutoHitLayer(data, hitAnimationDuration))
                        {
                            RefuseUnmatchedPacketAutoHitLayer(reactor, data, currentTick);
                        }
                        else
                        {
                            int remainingCurrentAnimationDuration = reactor?.GetRemainingStoppedAnimationDuration(currentTick) ?? 0;
                            data.PacketAnimationPhase = ResolvePacketAnimationPhaseAfterNoHitLayer(data.PacketProperEventIndex);
                            data.PacketAnimationEndTime = ResolvePacketChangeStateNoHitLayerAnimationEndTime(
                                currentTick,
                                data.PacketProperEventIndex,
                                data.PacketAnimationEndTime,
                                remainingCurrentAnimationDuration);
                        }
                    }

                    if (data.State == ReactorState.Activated
                        && data.PacketAnimationEndTime > 0
                        && currentTick >= data.PacketAnimationEndTime)
                    {
                        data.PacketAnimationEndTime = 0;
                        reactor?.ClearTransientAnimation();
                        data.PacketAnimationPhase = data.PacketAnimationPhase == PacketReactorAnimationPhase.AwaitingAutoHitLayerCompletion
                            && data.PacketProperEventIndex == -2
                                ? PacketReactorAnimationPhase.AwaitingAutoHitLayerCompletion
                                : PacketReactorAnimationPhase.AwaitingLayerCompletion;

                        if (ShouldDelayActivatedPacketHandoffUntilStateEnd(data, currentTick))
                        {
                            data.StateStartTime = currentTick;
                        }
                        else
                        {
                            ApplyPendingPacketVisualState(reactor, data, currentTick);
                            data.State = ReactorState.Active;
                            data.StateStartTime = currentTick;
                            data.PacketAnimationPhase = PacketReactorAnimationPhase.Idle;
                            StartPacketStateMovement(reactor, data, currentTick);
                        }
                    }

                    if (ShouldApplyPendingPacketVisualStateOnStateEnd(data, currentTick))
                    {
                        ApplyPendingPacketVisualState(reactor, data, currentTick);
                        data.State = ReactorState.Active;
                        data.StateStartTime = currentTick;
                        data.PacketAnimationPhase = PacketReactorAnimationPhase.Idle;
                        StartPacketStateMovement(reactor, data, currentTick);
                    }

                    if (ShouldCommitPacketVisualOwnershipOnStateEnd(data, currentTick))
                    {
                        CommitPacketVisualOwnership(reactor, data, currentTick);
                        StartPacketStateMovement(reactor, data, currentTick);
                    }

                    UpdatePacketStateMovement(reactor, data, currentTick);

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

            return (ResolveRenderableReactorState(data), data.StateFrame);
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
            data.HitOption = interactionMetadata.HitOption;
            data.PrimaryActivationType = interactionMetadata.ActivationType;
            data.ActivationType = interactionMetadata.ActivationType;
            data.SupportedActivationTypes = interactionMetadata.SupportedActivationTypes;
            data.RequiredItemId = interactionMetadata.RequiredItemId;
            data.RequiredQuestId = interactionMetadata.RequiredQuestId;
            data.RequiredQuestState = interactionMetadata.RequiredQuestState;
            data.ScriptNames = interactionMetadata.ScriptNames;
        }

        private void ApplyPacketReactorState(int index, int state, int x, int y, bool flip, int currentTick, bool applyAnimationState = true)
        {
            ReactorRuntimeData data = GetReactorData(index);
            ReactorItem reactor = GetReactor(index);
            if (data == null || reactor == null)
            {
                return;
            }

            reactor.SetWorldPosition(x, y);
            reactor.SetFlipState(flip);
            if (applyAnimationState)
            {
                reactor.SetAnimationState(state, currentTick, restartIfSameState: true);
            }

            data.VisualState = state;
            data.StateFrame = 0;
            data.StateStartTime = currentTick;
            data.Alpha = 1f;
            data.PacketLeavePending = false;
            data.PacketHitStartTime = 0;
            data.PacketStateEndTime = 0;
            data.PacketAnimationEndTime = 0;
            ClearPacketVisualOwnershipState(data);
            data.PacketPendingVisualState = applyAnimationState ? -1 : state;
            data.PacketAnimationPhase = applyAnimationState
                ? PacketReactorAnimationPhase.Idle
                : data.PacketAnimationPhase;
            data.PacketMoveStartTime = 0;
            data.PacketMoveEndTime = 0;
            data.PacketMoveStartX = x;
            data.PacketMoveStartY = y;
            data.PacketMoveTargetX = x;
            data.PacketMoveTargetY = y;
            data.PacketMoveUsesDefaultRelMove = false;
            data.PacketMoveExplicitlyRequested = false;
            data.PacketObservedMovePixelsPerMs = 0f;
            data.PacketEnterFadeStartTime = 0;
            data.PacketEnterFadeEndTime = 0;
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

            RefreshReactorLayerPlacement(reactor);
        }

        private static bool IsPacketAnimationClockRunning(ReactorRuntimeData data)
        {
            return data != null
                && (data.PacketLeavePending
                    || data.PacketHitStartTime > 0
                    || data.PacketAnimationEndTime > 0);
        }

        private void ApplyPendingPacketVisualState(ReactorItem reactor, ReactorRuntimeData data, int currentTick)
        {
            if (reactor == null || data == null || data.PacketPendingVisualState < 0)
            {
                return;
            }

            reactor.ClearTransientAnimation();
            reactor.SetAnimationState(data.PacketPendingVisualState, currentTick, restartIfSameState: true);
            CommitPacketLayerSourceOwnership(data, data.PacketPendingVisualState);
            data.PacketPendingVisualState = -1;
            RefreshReactorLayerPlacement(reactor);
        }

        private static void ConfigurePacketStateMovement(
            ReactorItem reactor,
            ReactorRuntimeData data,
            int startX,
            int startY,
            int targetX,
            int targetY,
            int moveEndTime,
            bool allowDefaultRelMove = false)
        {
            if (data == null)
            {
                return;
            }

            data.PacketMoveStartTime = 0;
            data.PacketMoveEndTime = 0;
            data.PacketMoveStartX = startX;
            data.PacketMoveStartY = startY;
            data.PacketMoveTargetX = targetX;
            data.PacketMoveTargetY = targetY;
            data.PacketMoveUsesDefaultRelMove = false;
            data.PacketMoveExplicitlyRequested = false;

            if (reactor == null
                || (!reactor.TemplateMoveEnabled && !allowDefaultRelMove)
                || moveEndTime <= 0
                || (startX == targetX && startY == targetY))
            {
                return;
            }

            reactor.SetLogicalWorldPosition(targetX, targetY);
            data.PacketMoveEndTime = moveEndTime;
        }

        internal static int ResolvePacketStandaloneMoveEndTime(
            int startX,
            int startY,
            int targetX,
            int targetY,
            int currentTick,
            float observedMovePixelsPerMs = 0f)
        {
            if (startX == targetX && startY == targetY)
            {
                return 0;
            }

            int resolvedDuration = PACKET_RELMOVE_FALLBACK_DURATION_MS;
            if (observedMovePixelsPerMs > 0f)
            {
                double dx = targetX - startX;
                double dy = targetY - startY;
                double distance = Math.Sqrt((dx * dx) + (dy * dy));
                if (distance > 0d)
                {
                    int inferredDuration = (int)Math.Round(distance / observedMovePixelsPerMs, MidpointRounding.AwayFromZero);
                    resolvedDuration = Math.Clamp(
                        inferredDuration,
                        PACKET_RELMOVE_MIN_DURATION_MS,
                        PACKET_RELMOVE_MAX_DURATION_MS);
                }
            }

            return currentTick + resolvedDuration;
        }

        internal static int ResolvePacketMovePacketEndTime(
            int startX,
            int startY,
            int targetX,
            int targetY,
            int currentTick,
            int packetStateEndTime,
            float observedMovePixelsPerMs = 0f)
        {
            // `CReactorPool::OnReactorMove` passes vtMissing for both RelMove timing
            // variants; tStateEnd is only consumed by `LoadReactorLayer` for moving
            // state reloads.
            _ = packetStateEndTime;
            return ResolvePacketStandaloneMoveEndTime(
                startX,
                startY,
                targetX,
                targetY,
                currentTick,
                observedMovePixelsPerMs);
        }

        internal static float ResolvePacketMoveProgress(
            int moveStartTime,
            int moveEndTime,
            int currentTick,
            bool usesDefaultRelMove)
        {
            int duration = Math.Max(1, moveEndTime - moveStartTime);
            float progress = MathHelper.Clamp((currentTick - moveStartTime) / (float)duration, 0f, 1f);
            if (usesDefaultRelMove)
            {
                progress = progress * progress * (3f - (2f * progress));
            }

            return progress;
        }

        internal static bool CanAnimatePacketStateMovement(
            bool templateMoveEnabled,
            bool usesDefaultRelMove,
            bool explicitlyRequested)
        {
            return templateMoveEnabled
                || usesDefaultRelMove
                || explicitlyRequested;
        }

        private static void StartPacketStateMovement(ReactorItem reactor, ReactorRuntimeData data, int currentTick)
        {
            if (reactor == null
                || data == null
                || !CanAnimatePacketStateMovement(
                    reactor.TemplateMoveEnabled,
                    data.PacketMoveUsesDefaultRelMove,
                    data.PacketMoveExplicitlyRequested)
                || data.PacketMoveEndTime <= currentTick
                || data.PacketMoveStartTime > 0
                || (data.PacketMoveStartX == data.PacketMoveTargetX && data.PacketMoveStartY == data.PacketMoveTargetY))
            {
                return;
            }

            data.PacketMoveStartX = reactor.CurrentWorldX;
            data.PacketMoveStartY = reactor.CurrentWorldY;
            data.PacketMoveStartTime = currentTick;
            data.PacketObservedMovePixelsPerMs = ResolveObservedPacketMovePixelsPerMs(
                data.PacketMoveStartX,
                data.PacketMoveStartY,
                data.PacketMoveTargetX,
                data.PacketMoveTargetY,
                data.PacketMoveStartTime,
                data.PacketMoveEndTime);
        }

        private void UpdatePacketStateMovement(ReactorItem reactor, ReactorRuntimeData data, int currentTick)
        {
            if (reactor == null
                || data == null
                || !CanAnimatePacketStateMovement(
                    reactor.TemplateMoveEnabled,
                    data.PacketMoveUsesDefaultRelMove,
                    data.PacketMoveExplicitlyRequested)
                || data.PacketMoveStartTime <= 0
                || data.PacketMoveEndTime <= 0)
            {
                return;
            }

            if (currentTick >= data.PacketMoveEndTime)
            {
                reactor.SetWorldPosition(data.PacketMoveTargetX, data.PacketMoveTargetY);
                ClearPacketStateMovement(reactor, data, snapToTarget: true);
                RefreshReactorLayerPlacement(reactor);
                return;
            }

            float progress = ResolvePacketMoveProgress(
                data.PacketMoveStartTime,
                data.PacketMoveEndTime,
                currentTick,
                data.PacketMoveUsesDefaultRelMove);
            int x = (int)Math.Round(MathHelper.Lerp(data.PacketMoveStartX, data.PacketMoveTargetX, progress));
            int y = (int)Math.Round(MathHelper.Lerp(data.PacketMoveStartY, data.PacketMoveTargetY, progress));
            if (reactor.CurrentWorldX != x || reactor.CurrentWorldY != y)
            {
                reactor.SetWorldPosition(x, y, persistInstance: false);
                RefreshReactorLayerPlacement(reactor);
            }
        }

        private static void ClearPacketStateMovement(ReactorItem reactor, ReactorRuntimeData data, bool snapToTarget)
        {
            if (data == null)
            {
                return;
            }

            if (snapToTarget && reactor != null)
            {
                reactor.SetWorldPosition(data.PacketMoveTargetX, data.PacketMoveTargetY);
            }

            int resolvedX = reactor?.CurrentWorldX ?? data.PacketMoveTargetX;
            int resolvedY = reactor?.CurrentWorldY ?? data.PacketMoveTargetY;
            data.PacketMoveStartTime = 0;
            data.PacketMoveEndTime = 0;
            data.PacketMoveStartX = resolvedX;
            data.PacketMoveStartY = resolvedY;
            data.PacketMoveTargetX = resolvedX;
            data.PacketMoveTargetY = resolvedY;
            data.PacketMoveUsesDefaultRelMove = false;
            data.PacketMoveExplicitlyRequested = false;
        }

        internal static float ResolveObservedPacketMovePixelsPerMs(
            int startX,
            int startY,
            int targetX,
            int targetY,
            int moveStartTime,
            int moveEndTime)
        {
            int duration = moveEndTime - moveStartTime;
            if (duration <= 0)
            {
                return 0f;
            }

            double dx = targetX - startX;
            double dy = targetY - startY;
            double distance = Math.Sqrt((dx * dx) + (dy * dy));
            if (distance <= 0d)
            {
                return 0f;
            }

            return (float)(distance / duration);
        }

        private static void SyncReactorVisualState(ReactorItem reactor, ReactorRuntimeData data, int currentTick)
        {
            if (reactor == null || data == null)
            {
                return;
            }

            reactor.SetAnimationState(data.VisualState, currentTick);
            data.StateFrame = reactor.GetCurrentFrameIndex(currentTick);
        }

        private void RefreshReactorLayerPlacement(ReactorItem reactor)
        {
            if (reactor?.ReactorInstance == null)
            {
                return;
            }

            int x = reactor.CurrentWorldX;
            int y = reactor.CurrentWorldY;
            ReactorFootholdPlacement placement = _reactorFootholdPlacementResolver?.Invoke(x, y)
                ?? new ReactorFootholdPlacement(0, 0);
            reactor.RenderSortKey = ResolveReactorRenderSortKey(
                placement.Page,
                placement.ZMass,
                reactor.TemplateLayerMode);
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

        private bool TryFindAuthoredReactorForPacketEnter(
            string reactorId,
            int initialState,
            int x,
            int y,
            bool flip,
            string name,
            int currentTick,
            float? localPlayerX,
            float? localPlayerY,
            out PacketEnterAuthoredReactorSelectionReason selectionReason,
            out int index)
        {
            index = -1;
            selectionReason = PacketEnterAuthoredReactorSelectionReason.None;
            var candidates = new List<PacketEnterAuthoredReactorCandidate>();

            for (int i = 0; i < GetReactorCount(); i++)
            {
                ReactorItem reactor = GetReactor(i);
                ReactorRuntimeData data = GetReactorData(i);
                if (reactor?.ReactorInstance == null
                    || data == null
                    || !CanAdoptPacketEnterOntoAuthoredReactor(data))
                {
                    continue;
                }

                if (!IsSameReactorTemplate(reactor.ReactorInstance.ReactorInfo?.ID, reactorId)
                    || reactor.CurrentWorldX != x
                    || reactor.CurrentWorldY != y
                    || reactor.ReactorInstance.Flip != flip)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(name)
                    && !string.IsNullOrWhiteSpace(reactor.ReactorInstance.Name)
                    && !string.Equals(reactor.ReactorInstance.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool hasPacketName = !string.IsNullOrWhiteSpace(name);
                bool hasAuthoredName = !string.IsNullOrWhiteSpace(reactor.ReactorInstance.Name);
                bool hasExactNameMatch = hasPacketName
                    && hasAuthoredName
                    && string.Equals(reactor.ReactorInstance.Name, name, StringComparison.OrdinalIgnoreCase);
                bool matchesPacketNameWhenPresent = !hasPacketName || hasExactNameMatch;
                candidates.Add(new PacketEnterAuthoredReactorCandidate(
                    i,
                    ResolveAuthoredReactorOrderForPacketEnterCandidate(i),
                    hasPacketName,
                    IsLocallyTouchedReactor(index: i, data),
                    IsImmediateLocalUserTouchCandidate(reactor, data, currentTick, localPlayerX, localPlayerY),
                    matchesPacketNameWhenPresent,
                    hasExactNameMatch,
                    data.VisualState));
            }

            return TrySelectAuthoredReactorCandidateForPacketEnter(candidates, initialState, out index, out selectionReason);
        }

        private int ResolveAuthoredReactorOrderForPacketEnterCandidate(int index)
        {
            if (index >= 0 && index < _spawnPoints.Count)
            {
                int spawnId = _spawnPoints[index]?.SpawnId ?? -1;
                if (spawnId >= 0)
                {
                    return spawnId;
                }
            }

            return index;
        }

        internal static bool CanAdoptPacketEnterOntoAuthoredReactor(ReactorRuntimeData data)
        {
            return data != null
                && !data.PacketObjectId.HasValue
                && !data.IsPacketOwned
                && data.State != ReactorState.Destroyed
                && data.State != ReactorState.Respawning
                && !data.PacketLeavePending;
        }

        internal static bool TrySelectAuthoredReactorCandidateForPacketEnter(
            IReadOnlyList<PacketEnterAuthoredReactorCandidate> candidates,
            int initialState,
            out int index,
            out PacketEnterAuthoredReactorSelectionReason selectionReason)
        {
            index = -1;
            selectionReason = PacketEnterAuthoredReactorSelectionReason.None;
            if (candidates == null || candidates.Count == 0)
            {
                return false;
            }

            bool packetNamePresent = candidates.Any(static candidate => candidate.IsPacketNamePresent);
            bool hasPacketNameMatch = candidates.Any(static candidate => candidate.MatchesPacketName);
            bool unresolvedPacketName = packetNamePresent && !hasPacketNameMatch;
            if (candidates.Count == 1)
            {
                if (unresolvedPacketName)
                {
                    // Narrow unresolved packet-name adoption only when template/position/flip
                    // resolution leaves exactly one authored candidate with no conflicting name.
                    index = candidates[0].Index;
                    selectionReason = PacketEnterAuthoredReactorSelectionReason.ClientSignal;
                    return true;
                }

                index = candidates[0].Index;
                selectionReason = PacketEnterAuthoredReactorSelectionReason.ClientSignal;
                return true;
            }

            if (unresolvedPacketName)
            {
                // Packet enters always carry a name field, but authored map reactors can leave
                // name empty. When template/position/flip has already narrowed to this scope and
                // no conflicting authored name survived candidate filtering, keep using the same
                // client-signal and authored-order discriminators instead of hard-refusing.
            }

            static bool TrySelectUniqueCandidate(
                IReadOnlyList<PacketEnterAuthoredReactorCandidate> scope,
                out int selectedIndex)
            {
                selectedIndex = -1;
                if (scope == null || scope.Count != 1)
                {
                    return false;
                }

                selectedIndex = scope[0].Index;
                return true;
            }

            static List<PacketEnterAuthoredReactorCandidate> PreferTrueCandidates(
                IReadOnlyList<PacketEnterAuthoredReactorCandidate> scope,
                Func<PacketEnterAuthoredReactorCandidate, bool> selector)
            {
                if (scope == null || scope.Count == 0 || selector == null)
                {
                    return scope?.ToList() ?? new List<PacketEnterAuthoredReactorCandidate>();
                }

                List<PacketEnterAuthoredReactorCandidate> preferred = scope
                    .Where(selector)
                    .ToList();
                return preferred.Count > 0
                    ? preferred
                    : scope.ToList();
            }

            List<PacketEnterAuthoredReactorCandidate> scope = candidates.ToList();
            Func<PacketEnterAuthoredReactorCandidate, bool>[] identityFilters =
            {
                static candidate => candidate.HasExactNameMatch,
                static candidate => candidate.MatchesPacketName,
                static candidate => candidate.ContainsCurrentLocalUserPosition
            };

            foreach (Func<PacketEnterAuthoredReactorCandidate, bool> filter in identityFilters)
            {
                scope = PreferTrueCandidates(scope, filter);
                if (TrySelectUniqueCandidate(scope, out index))
                {
                    selectionReason = PacketEnterAuthoredReactorSelectionReason.ClientSignal;
                    return true;
                }
            }

            if (HasDisjointCandidateSignalPair(
                    scope,
                    static candidate => candidate.IsLocallyTouched,
                    candidate => candidate.VisualState == initialState)
                || HasDisjointCandidateSignalPair(
                    scope,
                    static candidate => candidate.IsLocallyTouched,
                    static candidate => candidate.ContainsCurrentLocalUserPosition)
                || HasDisjointCandidateSignalPair(
                    scope,
                    static candidate => candidate.ContainsCurrentLocalUserPosition,
                    candidate => candidate.VisualState == initialState))
            {
                if (TrySelectUniqueStrongestStateSignalCandidate(scope, initialState, out index))
                {
                    selectionReason = PacketEnterAuthoredReactorSelectionReason.ClientSignal;
                    return true;
                }

                if (TrySelectNarrowedWzAuthoredOrderCandidateForDisjointSignals(
                        scope,
                        initialState,
                        out index))
                {
                    selectionReason = PacketEnterAuthoredReactorSelectionReason.WzAuthoredOrderFallback;
                    return true;
                }

                return false;
            }

            Func<PacketEnterAuthoredReactorCandidate, bool>[] statefulFilters =
            {
                static candidate => candidate.IsLocallyTouched,
                candidate => candidate.VisualState == initialState
            };

            foreach (Func<PacketEnterAuthoredReactorCandidate, bool> filter in statefulFilters)
            {
                scope = PreferTrueCandidates(scope, filter);
                if (TrySelectUniqueCandidate(scope, out index))
                {
                    selectionReason = PacketEnterAuthoredReactorSelectionReason.ClientSignal;
                    return true;
                }
            }

            if (TrySelectFullyAmbiguousWzAuthoredOrderCandidate(scope, initialState, out index))
            {
                selectionReason = PacketEnterAuthoredReactorSelectionReason.WzAuthoredOrderFallback;
                return true;
            }

            return false;
        }

        internal static bool ShouldPromotePacketEnterLocalTouch(bool isAlreadyLocallyTouched)
        {
            // CReactorPool::OnReactorEnterField only adds a live reactor. Re-key the
            // touched set immediately only when this packet recovers the id for a
            // reactor the local poll owner had already marked as touched.
            return isAlreadyLocallyTouched;
        }

        private static bool HasDisjointCandidateSignalPair(
            IReadOnlyList<PacketEnterAuthoredReactorCandidate> candidates,
            Func<PacketEnterAuthoredReactorCandidate, bool> firstSignal,
            Func<PacketEnterAuthoredReactorCandidate, bool> secondSignal)
        {
            if (candidates == null || candidates.Count <= 1)
            {
                return false;
            }

            if (firstSignal == null || secondSignal == null)
            {
                return false;
            }

            bool hasFirstSignalCandidate = false;
            bool hasSecondSignalCandidate = false;
            bool hasCandidateWithBothSignals = false;

            foreach (PacketEnterAuthoredReactorCandidate candidate in candidates)
            {
                bool matchesFirstSignal = firstSignal(candidate);
                bool matchesSecondSignal = secondSignal(candidate);
                hasFirstSignalCandidate |= matchesFirstSignal;
                hasSecondSignalCandidate |= matchesSecondSignal;
                hasCandidateWithBothSignals |= matchesFirstSignal && matchesSecondSignal;
            }

            return hasFirstSignalCandidate
                && hasSecondSignalCandidate
                && !hasCandidateWithBothSignals;
        }

        private static bool TrySelectFullyAmbiguousWzAuthoredOrderCandidate(
            IReadOnlyList<PacketEnterAuthoredReactorCandidate> candidates,
            int initialState,
            out int index)
        {
            index = -1;
            if (candidates == null || candidates.Count <= 1)
            {
                return false;
            }

            PacketEnterAuthoredReactorCandidate first = candidates[0];
            bool firstStateMatches = first.VisualState == initialState;

            for (int i = 1; i < candidates.Count; i++)
            {
                PacketEnterAuthoredReactorCandidate candidate = candidates[i];
                if (candidate.IsPacketNamePresent != first.IsPacketNamePresent
                    || candidate.MatchesPacketName != first.MatchesPacketName
                    || candidate.HasExactNameMatch != first.HasExactNameMatch
                    || candidate.ContainsCurrentLocalUserPosition != first.ContainsCurrentLocalUserPosition
                    || candidate.IsLocallyTouched != first.IsLocallyTouched
                    || candidate.VisualState != first.VisualState
                    || (candidate.VisualState == initialState) != firstStateMatches)
                {
                    return false;
                }
            }

            index = candidates
                .OrderBy(static candidate => candidate.AuthoredOrder)
                .ThenBy(static candidate => candidate.Index)
                .Select(static candidate => candidate.Index)
                .FirstOrDefault();
            return index >= 0;
        }

        private static bool TrySelectUniqueStrongestStateSignalCandidate(
            IReadOnlyList<PacketEnterAuthoredReactorCandidate> candidates,
            int initialState,
            out int index)
        {
            index = -1;
            if (candidates == null || candidates.Count == 0)
            {
                return false;
            }

            int bestIndex = -1;
            int bestScore = int.MinValue;
            bool hasScoreTie = false;

            for (int i = 0; i < candidates.Count; i++)
            {
                PacketEnterAuthoredReactorCandidate candidate = candidates[i];
                int score = 0;
                if (candidate.IsLocallyTouched)
                {
                    score++;
                }

                if (candidate.ContainsCurrentLocalUserPosition)
                {
                    score++;
                }

                if (candidate.VisualState == initialState)
                {
                    score++;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = candidate.Index;
                    hasScoreTie = false;
                    continue;
                }

                if (score == bestScore)
                {
                    hasScoreTie = true;
                }
            }

            if (bestScore < 2 || hasScoreTie)
            {
                return false;
            }

            index = bestIndex;
            return index >= 0;
        }

        private static bool TrySelectNarrowedWzAuthoredOrderCandidateForDisjointSignals(
            IReadOnlyList<PacketEnterAuthoredReactorCandidate> candidates,
            int initialState,
            out int index)
        {
            index = -1;
            if (candidates == null
                || candidates.Count <= 1)
            {
                return false;
            }

            int highestSignalScore = int.MinValue;
            bool hasSignalScoreTie = false;
            for (int i = 0; i < candidates.Count; i++)
            {
                PacketEnterAuthoredReactorCandidate candidate = candidates[i];
                int signalScore = 0;
                if (candidate.IsLocallyTouched)
                {
                    signalScore++;
                }

                if (candidate.ContainsCurrentLocalUserPosition)
                {
                    signalScore++;
                }

                if (candidate.VisualState == initialState)
                {
                    signalScore++;
                }

                if (signalScore > highestSignalScore)
                {
                    highestSignalScore = signalScore;
                    hasSignalScoreTie = false;
                    continue;
                }

                if (signalScore == highestSignalScore)
                {
                    hasSignalScoreTie = true;
                }
            }

            if (!hasSignalScoreTie || highestSignalScore <= 0)
            {
                return false;
            }

            List<PacketEnterAuthoredReactorCandidate> strongestCandidates = candidates
                .Where(candidate =>
                {
                    int score = 0;
                    if (candidate.IsLocallyTouched)
                    {
                        score++;
                    }

                    if (candidate.ContainsCurrentLocalUserPosition)
                    {
                        score++;
                    }

                    if (candidate.VisualState == initialState)
                    {
                        score++;
                    }

                    return score == highestSignalScore;
                })
                .ToList();
            if (strongestCandidates.Count <= 1)
            {
                return false;
            }

            index = strongestCandidates
                .OrderBy(static candidate => candidate.AuthoredOrder)
                .ThenBy(static candidate => candidate.Index)
                .Select(static candidate => candidate.Index)
                .FirstOrDefault();
            return index >= 0;
        }

        private void ApplyPacketOwnershipToReactor(int index, int packetObjectId, bool canRespawn, bool promoteLocalUserTouch = false)
        {
            if (packetObjectId <= 0)
            {
                return;
            }

            ReactorRuntimeData data = GetReactorData(index);
            if (data == null)
            {
                return;
            }

            int? previousPacketObjectId = data.PacketObjectId;
            int previousTouchObjectId = ResolveLocalTouchObjectId(data);
            bool wasLocallyTouched = previousTouchObjectId != 0
                && _reactorsOnLocalUser.TryGetValue(previousTouchObjectId, out int touchedIndex)
                && touchedIndex == index;
            bool wasPacketTouched = _reactorsOnLocalUser.TryGetValue(packetObjectId, out int packetTouchedIndex)
                && packetTouchedIndex == index;
            UntrackPacketObjectId(data.PacketObjectId);
            data.PacketObjectId = packetObjectId;
            data.IsPacketOwned = true;
            data.CanRespawn = canRespawn;
            TrackPacketObjectId(index, packetObjectId);

            if (index >= 0 && index < _spawnPoints.Count)
            {
                ReactorSpawnPoint spawnPoint = _spawnPoints[index];
                spawnPoint.PacketObjectId = packetObjectId;
                spawnPoint.IsPacketOwned = true;
                spawnPoint.CanRespawn = canRespawn;
            }

            if (previousPacketObjectId.HasValue
                && previousPacketObjectId.Value > 0
                && previousPacketObjectId.Value != packetObjectId)
            {
                _pendingPacketTouchRequestRemovalObjectIds.Enqueue(previousPacketObjectId.Value);
            }

            if ((previousTouchObjectId != packetObjectId || promoteLocalUserTouch)
                && (wasLocallyTouched || promoteLocalUserTouch)
                && !wasPacketTouched)
            {
                if (wasLocallyTouched)
                {
                    _reactorsOnLocalUser.Remove(previousTouchObjectId);
                }

                _reactorsOnLocalUser[packetObjectId] = index;
                ReactorItem reactor = GetReactor(index);
                if (reactor != null)
                {
                    _pendingPacketTouchStateChanges.Enqueue(new ReactorTouchStateChange(
                        reactor,
                        index,
                        packetObjectId,
                        UsesPacketObjectIdForLocalTouch(data, packetObjectId),
                        IsTouching: true));
                }
            }
        }

        private void ClearLocalTouchOwnership(int index, ReactorRuntimeData data)
        {
            if (data == null)
            {
                return;
            }

            int touchObjectId = ResolveLocalTouchObjectId(data);
            if (touchObjectId != 0
                && _reactorsOnLocalUser.TryGetValue(touchObjectId, out int touchedIndex)
                && touchedIndex == index)
            {
                _reactorsOnLocalUser.Remove(touchObjectId);
            }
        }

        private void QueuePacketTouchRequestRemoval(ReactorRuntimeData data)
        {
            int objectId = ResolveRemovedPacketTouchRequestObjectId(data);
            if (objectId > 0)
            {
                _pendingPacketTouchRequestRemovalObjectIds.Enqueue(objectId);
            }
        }

        internal static int ResolveRemovedPacketTouchRequestObjectId(ReactorRuntimeData data)
        {
            return data?.PacketObjectId is int objectId && objectId > 0
                ? objectId
                : 0;
        }

        internal static bool UsesPacketObjectIdForLocalTouch(ReactorRuntimeData data, int objectId)
        {
            return IsTransportableLocalTouchObjectId(objectId)
                && data?.PacketObjectId == objectId;
        }

        internal static bool IsTransportableLocalTouchObjectId(int objectId)
        {
            return objectId > 0;
        }

        private bool IsLocallyTouchedReactor(int index, ReactorRuntimeData data)
        {
            if (data == null)
            {
                return false;
            }

            int touchObjectId = ResolveLocalTouchObjectId(data);
            return touchObjectId != 0
                && _reactorsOnLocalUser.TryGetValue(touchObjectId, out int touchedIndex)
                && touchedIndex == index;
        }

        private bool IsImmediateLocalUserTouchCandidate(
            ReactorItem reactor,
            ReactorRuntimeData data,
            int currentTick,
            float? localPlayerX,
            float? localPlayerY)
        {
            if (reactor == null
                || data == null
                || !localPlayerX.HasValue
                || !localPlayerY.HasValue
                || !CanPollLocalUserTouchOwnershipCandidate(data, SupportsActivationType(data, ReactorActivationType.Touch)))
            {
                return false;
            }

            Rectangle reactorRect = reactor.GetCurrentBounds(currentTick);
            return DoesClientTouchBoundsContainPosition(reactorRect, localPlayerX.Value, localPlayerY.Value);
        }

        private static bool IsSameReactorTemplate(string left, string right)
        {
            if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return int.TryParse(left, NumberStyles.Integer, CultureInfo.InvariantCulture, out int leftId)
                && int.TryParse(right, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rightId)
                && leftId == rightId;
        }

        internal static bool DoesClientTouchBoundsContainPosition(Rectangle bounds, float playerX, float playerY)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return false;
            }

            return bounds.Contains(
                (int)playerX,
                (int)playerY);
        }

        internal static bool ShouldSkipClientTouchOwnershipBoundsUpdate(Rectangle bounds)
        {
            return bounds.Width <= 0 || bounds.Height <= 0;
        }

        internal static bool ResolveClientTouchOwnershipPollContainment(
            Rectangle bounds,
            float playerX,
            float playerY,
            bool wasTouching)
        {
            if (ShouldSkipClientTouchOwnershipBoundsUpdate(bounds))
            {
                // CReactorPool::FindTouchReactorAroundLocalUser skips ownership mutation
                // when the live layer returns empty bounds before the point test.
                return wasTouching;
            }

            return DoesClientTouchBoundsContainPosition(bounds, playerX, playerY);
        }

        internal static int ResolveLocalTouchObjectId(ReactorRuntimeData data)
        {
            if (data == null)
            {
                return 0;
            }

            if (data.PacketObjectId is int packetObjectId && packetObjectId > 0)
            {
                return packetObjectId;
            }

            return data.PoolId > 0 ? -data.PoolId : 0;
        }

        internal static bool CanPollLocalUserTouchReactor(ReactorRuntimeData data)
        {
            return data != null
                && data.State != ReactorState.Destroyed
                && data.State != ReactorState.Respawning
                && !data.PacketLeavePending;
        }

        internal static bool CanPollLocalUserTouchOwnershipCandidate(ReactorRuntimeData data, bool supportsTouchActivation)
        {
            return supportsTouchActivation
                && CanPollLocalUserTouchReactor(data);
        }

        internal static bool ShouldIncludeClientTouchOwnershipPollResult(ReactorRuntimeData data, bool supportsTouchActivation)
        {
            return CanPollLocalUserTouchOwnershipCandidate(data, supportsTouchActivation);
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

        internal static bool CanParticipateInHitReactorSearch(ReactorRuntimeData data)
        {
            return data != null
                && data.State != ReactorState.Destroyed
                && data.State != ReactorState.Respawning
                && SupportsActivationType(data, ReactorActivationType.Hit);
        }

        internal static bool ShouldAutoActivateQuestRefresh(
            ReactorRuntimeData data,
            IEnumerable<int> currentStateEventTypes)
        {
            if (!SupportsActivationType(data, ReactorActivationType.Quest))
            {
                return false;
            }

            ReactorActivationTypeMask currentStateMask = ReactorActivationTypeMask.None;
            if (currentStateEventTypes != null)
            {
                foreach (int eventType in currentStateEventTypes)
                {
                    currentStateMask |= EventTypeToActivationMask(eventType);
                }
            }

            ReactorActivationTypeMask blockingInteractiveTypes =
                ReactorActivationTypeMask.Touch
                | ReactorActivationTypeMask.Hit
                | ReactorActivationTypeMask.Skill
                | ReactorActivationTypeMask.Item;

            if (currentStateMask != ReactorActivationTypeMask.None)
            {
                return (currentStateMask & ReactorActivationTypeMask.Quest) != 0
                    && (currentStateMask & blockingInteractiveTypes) == ReactorActivationTypeMask.None;
            }

            if (data?.PrimaryActivationType != ReactorActivationType.Quest)
            {
                return false;
            }

            ReactorActivationTypeMask supportedTypes = data.SupportedActivationTypes == ReactorActivationTypeMask.None
                ? ToActivationMask(data.ActivationType)
                : data.SupportedActivationTypes;
            return (supportedTypes & blockingInteractiveTypes) == ReactorActivationTypeMask.None;
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
            SyncReactorVisualState(reactor, data, currentTick);
            data.State = ReactorState.Activated;
            data.StateStartTime = currentTick;
            data.StateFrame = 0;
            UpdatePreferredAuthoredOrder(data, ReactorActivationType.Time, selectedAuthoredOrder);
            RefreshReactorLayerPlacement(reactor);
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
            SyncReactorVisualState(reactor, data, currentTick);
            data.ActivationType = ReactorActivationType.Time;
            data.ActivationValue = 0;
            data.State = ReactorState.Activated;
            data.StateStartTime = currentTick;
            data.StateFrame = 0;
            data.ActivatingPlayerId = 0;
            UpdatePreferredAuthoredOrder(data, ReactorActivationType.Time, selectedAuthoredOrder);
            PublishScriptState(reactor, data, isEnabled: true, currentTick);
            RefreshReactorLayerPlacement(reactor);
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
            SyncReactorVisualState(reactor, data, currentTick);
            data.State = ReactorState.Activated;
            data.StateStartTime = currentTick;
            data.StateFrame = 0;
            UpdatePreferredAuthoredOrder(data, activationType, selectedAuthoredOrder);
            RefreshReactorLayerPlacement(reactor);
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
            int hitStartTime = currentTick + Math.Max(0, hitStartDelayMs);
            return hitStartTime == 0 ? 1 : hitStartTime;
        }

        internal static int ResolvePacketLeaveHitStartTime(int currentTick, bool hasAuthoredStateEvents)
        {
            if (hasAuthoredStateEvents)
            {
                return 0;
            }

            int hitStartTime = currentTick + 400;
            return hitStartTime == 0 ? 1 : hitStartTime;
        }

        internal static int ResolvePacketStateEndTime(int currentTick, int stateEndDelayTicks)
        {
            return currentTick + (Math.Max(0, stateEndDelayTicks) * 100);
        }

        internal static int ResolvePacketAnimationEndTime(int currentTick, int animationDurationMs)
        {
            return animationDurationMs > 0
                ? currentTick + animationDurationMs
                : 0;
        }

        internal static PacketReactorAnimationPhase ResolvePacketAnimationPhaseAfterLoad(
            int packetProperEventIndex,
            bool loadedHitLayer)
        {
            if (!loadedHitLayer)
            {
                return PacketReactorAnimationPhase.Idle;
            }

            return packetProperEventIndex == -2
                ? PacketReactorAnimationPhase.AwaitingAutoHitLayerCompletion
                : PacketReactorAnimationPhase.AwaitingLayerCompletion;
        }

        internal static PacketReactorAnimationPhase ResolvePacketAnimationPhaseAfterNoHitLayer(int packetProperEventIndex)
        {
            return packetProperEventIndex == -2
                ? PacketReactorAnimationPhase.Idle
                : PacketReactorAnimationPhase.AwaitingLayerCompletion;
        }

        internal static int ResolvePacketChangeStateNoHitLayerAnimationEndTime(
            int currentTick,
            int packetProperEventIndex,
            int currentAnimationEndTime,
            int remainingCurrentAnimationDuration)
        {
            if (packetProperEventIndex == -2)
            {
                return 0;
            }

            if (currentAnimationEndTime > currentTick)
            {
                return currentAnimationEndTime;
            }

            return ResolvePacketAnimationEndTime(currentTick, remainingCurrentAnimationDuration);
        }

        internal static int ResolvePacketLeaveNoHitLayerAnimationEndTime(
            int currentTick,
            int packetProperEventIndex,
            int currentAnimationEndTime,
            int remainingCurrentAnimationDuration)
        {
            if (packetProperEventIndex == -2)
            {
                return 0;
            }

            if (currentAnimationEndTime > currentTick)
            {
                return currentAnimationEndTime;
            }

            return ResolvePacketAnimationEndTime(currentTick, remainingCurrentAnimationDuration);
        }

        internal static float ResolvePacketEnterFadeAlpha(int fadeStartTime, int fadeEndTime, int currentTick)
        {
            if (fadeEndTime <= fadeStartTime)
            {
                return 1f;
            }

            if (currentTick <= fadeStartTime)
            {
                return 0f;
            }

            if (currentTick >= fadeEndTime)
            {
                return 1f;
            }

            return MathHelper.Clamp(
                (currentTick - fadeStartTime) / (float)(fadeEndTime - fadeStartTime),
                0f,
                1f);
        }

        private static int StartPacketHitAnimation(ReactorItem reactor, ReactorRuntimeData data, int currentTick, Action<string> playHitSound)
        {
            if (reactor == null || data == null)
            {
                return 0;
            }

            ResolvePacketMutationFallbackVisualOwnershipSourceStates(
                reactor.GetActiveAnimationState(),
                reactor.TransientHitLayerSourceState,
                data.VisualState,
                out int fallbackAnimationOwnerState,
                out _);
            int sourceState = ResolvePacketHitAnimationState(
                data,
                fallbackAnimationOwnerState);
            IReadOnlyList<int> sourceEventTypes = reactor.GetExactAuthoredEventTypes(sourceState);
            int sourceStateBeforeFallback = sourceState;
            sourceState = ResolvePacketHitAnimationLoadSourceState(
                sourceState,
                fallbackAnimationOwnerState,
                data,
                sourceEventTypes,
                data.HitOption,
                data.ReactorType,
                reactor.GetExactAuthoredEventTypes);
            if (sourceState != sourceStateBeforeFallback)
            {
                sourceEventTypes = reactor.GetExactAuthoredEventTypes(sourceState);
            }
            int packetProperEventIndex = data.PacketProperEventIndex;
            if (!TryResolvePacketLoadLayerProperEventIndex(
                    packetProperEventIndex,
                    sourceEventTypes,
                    data.HitOption,
                    data.ReactorType,
                    out int properEventIndex,
                    out bool shouldLoadHitLayer))
            {
                return 0;
            }

            if (!shouldLoadHitLayer)
            {
                return 0;
            }

            int persistedProperEventIndex = ResolvePacketLoadLayerPersistedProperEventIndex(
                packetProperEventIndex,
                properEventIndex);
            if (persistedProperEventIndex != packetProperEventIndex)
            {
                data.PacketProperEventIndex = persistedProperEventIndex;
            }

            if (persistedProperEventIndex >= 0)
            {
                UpdatePreferredAuthoredOrder(data, ReactorActivationType.Hit, persistedProperEventIndex);
            }

            if (!reactor.TryStartHitAnimation(sourceState, persistedProperEventIndex, currentTick, out int duration))
            {
                return 0;
            }

            ApplyPacketLoadedHitLayerOwnership(data, sourceState);

            string descriptor = BuildReactorHitSoundDescriptor(reactor, sourceState);
            if (!string.IsNullOrWhiteSpace(descriptor))
            {
                playHitSound?.Invoke(descriptor);
            }

            return duration;
        }

        internal static bool TryResolvePacketLoadLayerProperEventIndex(
            int packetProperEventIndex,
            IEnumerable<int> sourceEventTypes,
            int hitOption,
            ReactorType reactorType,
            out int properEventIndex,
            out bool shouldLoadHitLayer)
        {
            properEventIndex = packetProperEventIndex;
            shouldLoadHitLayer = false;
            if (packetProperEventIndex != -2)
            {
                shouldLoadHitLayer = true;
                return true;
            }

            if (!ReactorItem.TryResolveClientHitEventIndex(sourceEventTypes, hitOption, reactorType, out properEventIndex))
            {
                properEventIndex = -1;
                shouldLoadHitLayer = false;
                return true;
            }

            shouldLoadHitLayer = properEventIndex >= 0;
            return true;
        }

        internal static int ResolvePacketLoadLayerPersistedProperEventIndex(
            int packetProperEventIndex,
            int resolvedProperEventIndex)
        {
            return packetProperEventIndex == -2 && resolvedProperEventIndex >= 0
                ? resolvedProperEventIndex
                : packetProperEventIndex;
        }

        internal static int ResolvePacketHitAnimationState(ReactorRuntimeData data)
        {
            return ResolvePacketHitAnimationState(
                data,
                data?.VisualState ?? 0);
        }

        internal static int ResolvePacketHitAnimationState(
            ReactorRuntimeData data,
            int fallbackAnimationOwnerState)
        {
            if (data == null)
            {
                return 0;
            }

            return data.PacketHitAnimationState >= 0
                ? data.PacketHitAnimationState
                : data.PacketAnimationSourceState >= 0
                    ? data.PacketAnimationSourceState
                    : fallbackAnimationOwnerState;
        }

        internal static int ResolvePacketHitAnimationLoadSourceState(
            int sourceState,
            int fallbackAnimationOwnerState,
            ReactorRuntimeData data,
            IReadOnlyList<int> sourceEventTypes,
            int hitOption,
            ReactorType reactorType,
            Func<int, IReadOnlyList<int>> getExactAuthoredEventTypes)
        {
            if (data == null
                || data.PacketProperEventIndex != -2
                || getExactAuthoredEventTypes == null)
            {
                return sourceState;
            }

            if (HasResolvableAutoHitEventType(sourceEventTypes, hitOption, reactorType))
            {
                return sourceState;
            }

            int packetAnimationSourceState = data.PacketAnimationSourceState;
            if (packetAnimationSourceState >= 0)
            {
                IReadOnlyList<int> animationSourceEventTypes = getExactAuthoredEventTypes(packetAnimationSourceState);
                if (HasResolvableAutoHitEventType(animationSourceEventTypes, hitOption, reactorType))
                {
                    return packetAnimationSourceState;
                }
            }

            if (fallbackAnimationOwnerState >= 0
                && fallbackAnimationOwnerState != sourceState
                && fallbackAnimationOwnerState != packetAnimationSourceState)
            {
                IReadOnlyList<int> continuityFallbackEventTypes = getExactAuthoredEventTypes(fallbackAnimationOwnerState);
                if (HasResolvableAutoHitEventType(continuityFallbackEventTypes, hitOption, reactorType))
                {
                    return fallbackAnimationOwnerState;
                }
            }

            return sourceState;
        }

        private static bool HasResolvableAutoHitEventType(
            IReadOnlyList<int> eventTypes,
            int hitOption,
            ReactorType reactorType)
        {
            return eventTypes is { Count: > 0 }
                && ReactorItem.TryResolveClientHitEventIndex(eventTypes, hitOption, reactorType, out _);
        }

        internal static int ResolvePacketMutationFallbackAnimationOwnerState(
            int activeAnimationState,
            int transientHitSourceState)
        {
            return activeAnimationState >= 0
                ? activeAnimationState
                : transientHitSourceState;
        }

        internal static void ResolvePacketMutationFallbackVisualOwnershipSourceStates(
            int activeAnimationState,
            int transientHitSourceState,
            int visualState,
            out int fallbackAnimationOwnerState,
            out int fallbackHitOwnerState)
        {
            fallbackAnimationOwnerState = activeAnimationState >= 0
                ? activeAnimationState
                : transientHitSourceState >= 0
                    ? transientHitSourceState
                    : visualState;
            fallbackHitOwnerState = transientHitSourceState >= 0
                && transientHitSourceState != fallbackAnimationOwnerState
                    ? transientHitSourceState
                    : -1;
        }

        internal static void CommitPacketLayerSourceOwnership(ReactorRuntimeData data, int visualState)
        {
            if (data == null)
            {
                return;
            }

            data.PacketAnimationSourceState = visualState;
            data.PacketHitAnimationState = -1;
        }

        internal static void ApplyPacketLoadedHitLayerOwnership(ReactorRuntimeData data, int loadedHitSourceState)
        {
            if (data == null)
            {
                return;
            }

            if (loadedHitSourceState < 0)
            {
                data.PacketHitAnimationState = -1;
                return;
            }

            if (data.PacketAnimationSourceState < 0)
            {
                data.PacketAnimationSourceState = loadedHitSourceState;
                data.PacketHitAnimationState = -1;
                return;
            }

            data.PacketHitAnimationState = loadedHitSourceState != data.PacketAnimationSourceState
                ? loadedHitSourceState
                : -1;
        }

        internal static void ClearPacketVisualOwnershipState(ReactorRuntimeData data)
        {
            if (data == null)
            {
                return;
            }

            data.PacketAnimationSourceState = -1;
            data.PacketHitAnimationState = -1;
        }

        private static string BuildReactorHitSoundDescriptor(ReactorItem reactor, int sourceState)
        {
            string templateId = reactor?.ReactorInstance?.ReactorInfo?.ID;
            if (string.IsNullOrWhiteSpace(templateId))
            {
                return null;
            }

            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                0x0849,
                "Sound/Reactor.img/{0}/{1}",
                2,
                out bool _);
            return string.Format(
                CultureInfo.InvariantCulture,
                format,
                templateId,
                sourceState.ToString(CultureInfo.InvariantCulture));
        }

        internal static int ResolveRenderableReactorState(ReactorRuntimeData data)
        {
            if (data == null)
            {
                return 0;
            }

            return ShouldPreservePacketAnimationSourceState(data) && data.PacketAnimationSourceState >= 0
                ? data.PacketAnimationSourceState
                : data.VisualState;
        }

        internal static int ResolvePacketVisualOwnershipSourceState(ReactorRuntimeData data)
        {
            return ResolvePacketVisualOwnershipSourceState(
                data,
                data?.VisualState ?? 0);
        }

        internal static int ResolvePacketVisualOwnershipSourceState(
            ReactorRuntimeData data,
            int fallbackAnimationOwnerState)
        {
            if (data == null)
            {
                return 0;
            }

            if (data.PacketHitAnimationState >= 0)
            {
                return data.PacketHitAnimationState;
            }

            if (data.PacketAnimationSourceState >= 0)
            {
                return data.PacketAnimationSourceState;
            }

            return fallbackAnimationOwnerState;
        }

        internal static void ResolvePacketVisualOwnershipSourceStatesForMutation(
            ReactorRuntimeData data,
            out int packetAnimationSourceState,
            out int packetHitAnimationState)
        {
            ResolvePacketVisualOwnershipSourceStatesForMutation(
                data,
                data?.VisualState ?? 0,
                out packetAnimationSourceState,
                out packetHitAnimationState);
        }

        internal static void ResolvePacketVisualOwnershipSourceStatesForMutation(
            ReactorRuntimeData data,
            int fallbackAnimationOwnerState,
            out int packetAnimationSourceState,
            out int packetHitAnimationState)
        {
            ResolvePacketVisualOwnershipSourceStatesForMutation(
                data,
                fallbackAnimationOwnerState,
                fallbackHitOwnerState: -1,
                out packetAnimationSourceState,
                out packetHitAnimationState);
        }

        internal static void ResolvePacketVisualOwnershipSourceStatesForMutation(
            ReactorRuntimeData data,
            int fallbackAnimationOwnerState,
            int fallbackHitOwnerState,
            out int packetAnimationSourceState,
            out int packetHitAnimationState)
        {
            packetAnimationSourceState = fallbackAnimationOwnerState >= 0
                ? fallbackAnimationOwnerState
                : 0;
            packetHitAnimationState = -1;
            if (data == null)
            {
                if (fallbackHitOwnerState >= 0
                    && fallbackHitOwnerState != packetAnimationSourceState)
                {
                    packetHitAnimationState = fallbackHitOwnerState;
                }
                return;
            }

            if (data.PacketHitAnimationState >= 0)
            {
                packetAnimationSourceState = data.PacketAnimationSourceState >= 0
                    ? data.PacketAnimationSourceState
                    : fallbackAnimationOwnerState >= 0
                        ? fallbackAnimationOwnerState
                        : data.PacketHitAnimationState;
                packetHitAnimationState = data.PacketHitAnimationState;
                return;
            }

            if (data.PacketAnimationSourceState >= 0)
            {
                packetAnimationSourceState = data.PacketAnimationSourceState;
                if (fallbackHitOwnerState >= 0
                    && fallbackHitOwnerState != packetAnimationSourceState)
                {
                    packetHitAnimationState = fallbackHitOwnerState;
                }
                return;
            }

            packetAnimationSourceState = fallbackAnimationOwnerState;
            if (fallbackHitOwnerState >= 0
                && fallbackHitOwnerState != packetAnimationSourceState)
            {
                packetHitAnimationState = fallbackHitOwnerState;
            }
        }

        internal static bool ShouldPreservePacketAnimationSourceState(ReactorRuntimeData data)
        {
            if (data == null || !data.IsPacketOwned || data.PacketAnimationSourceState < 0)
            {
                return false;
            }

            if (data.PacketLeavePending)
            {
                return true;
            }

            return data.PacketPendingVisualState >= 0
                || ShouldPreservePacketSourceUntilStateEnd(data)
                || ShouldPreservePacketSourceAfterAutoHitLayerRefusal(data);
        }

        internal static bool ShouldApplyPendingPacketVisualStateOnStateEnd(ReactorRuntimeData data, int currentTick)
        {
            return data != null
                && data.PacketPendingVisualState >= 0
                && data.PacketStateEndTime > 0
                && currentTick >= data.PacketStateEndTime
                && data.State == ReactorState.Activated
                && data.PacketHitStartTime <= 0
                && data.PacketAnimationEndTime <= 0;
        }

        internal static bool ShouldCommitPacketVisualOwnershipOnStateEnd(ReactorRuntimeData data, int currentTick)
        {
            return data != null
                && data.IsPacketOwned
                && data.State == ReactorState.Activated
                && data.PacketPendingVisualState < 0
                && data.PacketAnimationSourceState >= 0
                && data.PacketStateEndTime > 0
                && currentTick >= data.PacketStateEndTime
                && data.PacketHitStartTime <= 0
                && data.PacketAnimationEndTime <= 0
                && !data.PacketLeavePending;
        }

        internal static bool ShouldDelayActivatedPacketHandoffUntilStateEnd(ReactorRuntimeData data, int currentTick)
        {
            return data != null
                && data.State == ReactorState.Activated
                && data.PacketPendingVisualState < 0
                && data.PacketStateEndTime > currentTick;
        }

        private static bool ShouldPreservePacketSourceUntilStateEnd(ReactorRuntimeData data)
        {
            return data != null
                && data.State == ReactorState.Activated
                && data.PacketStateEndTime > 0
                && data.PacketHitStartTime <= 0
                && data.PacketAnimationEndTime <= 0
                && !data.PacketLeavePending;
        }

        internal static bool ShouldRefuseUnmatchedPacketAutoHitLayer(ReactorRuntimeData data, int hitAnimationDuration)
        {
            return data != null
                && data.PacketProperEventIndex == -2
                && hitAnimationDuration <= 0;
        }

        internal static bool ShouldPreservePacketSourceAfterAutoHitLayerRefusal(ReactorRuntimeData data)
        {
            return data != null
                && data.IsPacketOwned
                && data.PacketProperEventIndex == -2
                && data.PacketPendingVisualState < 0
                && data.PacketAnimationSourceState >= 0
                && data.PacketHitStartTime <= 0
                && data.PacketAnimationEndTime <= 0
                && !data.PacketLeavePending;
        }

        internal static void RefuseUnmatchedPacketAutoHitLayer(ReactorItem reactor, ReactorRuntimeData data, int currentTick)
        {
            if (data == null)
            {
                return;
            }

            reactor?.ClearTransientAnimation();
            data.PacketHitAnimationState = -1;
            data.PacketPendingVisualState = -1;
            data.PacketAnimationEndTime = 0;
            data.PacketAnimationPhase = PacketReactorAnimationPhase.Idle;
            data.State = ReactorState.Activated;
            data.StateStartTime = currentTick;
        }

        private static void CommitPacketVisualOwnership(ReactorItem reactor, ReactorRuntimeData data, int currentTick)
        {
            if (data == null)
            {
                return;
            }

            if (reactor != null)
            {
                reactor.ClearTransientAnimation();
                reactor.SetAnimationState(data.VisualState, currentTick, restartIfSameState: true);
            }

            ClearPacketVisualOwnershipState(data);
            data.PacketStateEndTime = 0;
            data.PacketAnimationPhase = PacketReactorAnimationPhase.Idle;
            data.State = ReactorState.Active;
            data.StateStartTime = currentTick;
        }

        internal static int ResolveReactorRenderSortKey(int page, int zMass, int templateLayerMode)
        {
            return templateLayerMode switch
            {
                1 => (30000 * page) - 1073739824,
                2 => -1073471624,
                _ => (10 * ((3000 * page) - zMass)) - 1073711834
            };
        }

        private static void BeginPacketEnterFade(ReactorRuntimeData data, int currentTick)
        {
            if (data == null)
            {
                return;
            }

            data.PacketEnterFadeStartTime = currentTick;
            data.PacketEnterFadeEndTime = currentTick + PACKET_ENTER_FADE_DURATION_MS;
            data.Alpha = 0f;
        }

        private void TryPlayReactorHitSound(ReactorItem reactor, int sourceState)
        {
            string descriptor = BuildReactorHitSoundDescriptor(reactor, sourceState);
            if (!string.IsNullOrWhiteSpace(descriptor))
            {
                _onReactorLayerSoundRequested?.Invoke(descriptor);
            }
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
                : ResolveLocalPreferredAuthoredOrder(reactor, data, request);
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

        internal static int ResolveClientPreferredHitAuthoredOrder(
            IEnumerable<int> authoredEventTypes,
            int hitOption,
            ReactorType reactorType)
        {
            return ReactorItem.TryResolveClientHitEventIndex(authoredEventTypes, hitOption, reactorType, out int eventIndex)
                ? eventIndex
                : -1;
        }

        private static int ResolveLocalPreferredAuthoredOrder(
            ReactorItem reactor,
            ReactorRuntimeData data,
            ReactorTransitionRequest request)
        {
            if (reactor == null
                || data == null
                || request.ActivationType != ReactorActivationType.Hit)
            {
                return -1;
            }

            return ResolveClientPreferredHitAuthoredOrder(
                reactor.GetAuthoredEventTypes(data.VisualState),
                data.HitOption,
                data.ReactorType);
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
                1 or 2 or 3 or 4 or 8 => ReactorActivationTypeMask.Hit,
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
