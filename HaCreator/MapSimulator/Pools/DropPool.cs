using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator.Pools
{
    /// <summary>
    /// Represents a pet's target drop for chasing
    /// </summary>
    public class PetDropTarget
    {
        public int PetId { get; set; }
        public int DropId { get; set; }
        public float TargetX { get; set; }
        public float TargetY { get; set; }
        public int StartTime { get; set; }
        public bool IsChasing { get; set; }
        public float ChaseSpeed { get; set; } = 150f;  // Pixels per second
    }

    /// <summary>
    /// Record of a recently picked up item
    /// </summary>
    public class RecentPickupRecord
    {
        public int DropId { get; set; }
        public DropType Type { get; set; }
        public string ItemId { get; set; }
        public int MesoAmount { get; set; }
        public int Quantity { get; set; }
        public int PickupTime { get; set; }
        public int PickerId { get; set; }       // Player or pet ID
        public bool PickedByPet { get; set; }
        public DropPickupActorKind ActorKind { get; set; } = DropPickupActorKind.Other;
        public string ActorName { get; set; }
    }

    /// <summary>
    /// Drop exception entry for filtering
    /// </summary>
    public class DropExceptionEntry
    {
        public string ItemId { get; set; }
        public DropType? Type { get; set; }
        public int? MinMesoAmount { get; set; }
        public int? MaxMesoAmount { get; set; }
        public bool BlockPickup { get; set; }   // true = block, false = allow only this
    }
    /// <summary>
    /// Drop type enumeration
    /// </summary>
    public enum DropType
    {
        Meso,           // Currency drop
        Item,           // Equipment/use/etc item
        QuestItem,      // Quest-specific item
        InstallItem     // Installation item (chair, etc)
    }

    public enum DropOwnershipType : byte
    {
        Character = 0,
        Party = 1,
        None = 2,
        Explosive = 3
    }

    public enum PacketDropLeaveReason : byte
    {
        Remove = 0,
        PlayerPickup = 2,
        MobPickup = 3,
        Explode = 4,
        PetPickup = 5
    }

    public enum DropPickupFailureReason
    {
        None = 0,
        NoDropInRange,
        OwnershipRestricted,
        InventoryFull,
        PetPickupBlocked,
        FieldRestricted,
        Unavailable
    }

    public enum DropPickupActorKind
    {
        Player = 0,
        Pet,
        Mob,
        Other
    }

    public sealed class DropPickupAttemptResult
    {
        public DropItem Drop { get; init; }
        public DropItem ContextDrop { get; init; }
        public DropPickupFailureReason FailureReason { get; init; }
        public RecentPickupRecord RecentPickup { get; init; }

        public bool Success => Drop != null && FailureReason == DropPickupFailureReason.None;
    }

    /// <summary>
    /// Drop state for animation
    /// </summary>
    public enum DropState
    {
        Spawning,       // Initial spawn animation (floating down from mob)
        Falling,        // Falling with gravity
        Bouncing,       // Bouncing on ground
        Idle,           // Sitting on ground, can be picked up
        PickingUp,      // Being picked up animation
        Expired,        // Faded out / disappeared
        Removed         // Ready for removal
    }

    /// <summary>
    /// Single dropped item with physics and animation
    /// </summary>
    public class DropItem
    {
        #region Properties
        public int PoolId { get; set; }
        public DropType Type { get; set; }
        public string ItemId { get; set; }              // Item ID string (for lookup)
        public int Quantity { get; set; } = 1;          // Stack count (for mesos/etc items)
        public int MesoAmount { get; set; }             // If meso drop

        // Position and physics
        public float X { get; set; }
        public float Y { get; set; }
        public float VelocityX { get; set; }
        public float VelocityY { get; set; }
        public float SpawnX { get; set; }               // Where mob died
        public float SpawnY { get; set; }
        public float GroundY { get; set; }              // Ground level to land on

        // Animation state
        public DropState State { get; set; } = DropState.Spawning;
        public int SpawnTime { get; set; }
        public int BounceCount { get; set; }
        public float Alpha { get; set; } = 1.0f;
        public float Scale { get; set; } = 1.0f;
        public float Rotation { get; set; }             // For item icons
        public int LastPickupFailureTime { get; set; } = int.MinValue;
        public DropPickupFailureReason LastPickupFailureReason { get; set; } = DropPickupFailureReason.None;
        public int LastStateChangeTime { get; set; }

        // Visual
        public IDXObject Icon { get; set; }             // Item icon to display
        public List<IDXObject> AnimFrames { get; set; } // For animated drops
        public int CurrentFrame { get; set; }
        public int LastFrameTime { get; set; }
        public bool IsRare { get; set; }                // Glow effect for rare items
        public Color GlowColor { get; set; } = Color.White;
        public bool UseLayeredMesoAnimation { get; set; }
        public int MesoAnimationLayerCount { get; set; }
        public int MesoAnimationIconType { get; set; }
        public bool DrawOnElevatedLayer { get; set; }

        // Pickup state
        public bool CanPickup { get; set; } = true;
        public int OwnerId { get; set; } = 0;           // Player ID with pickup priority (0 = anyone)
        public int OwnerExpireTime { get; set; }        // When ownership expires
        public int ExpireTime { get; set; }             // When drop disappears
        public DropOwnershipType OwnershipType { get; set; } = DropOwnershipType.None;
        public int SourceId { get; set; }
        public bool IsReal { get; set; } = true;
        public bool AllowPetPickup { get; set; } = true;
        public bool IsPacketControlled { get; set; }
        public byte PacketEnterType { get; set; }
        public int CreateDelayMs { get; set; }
        public int ScheduledRemovalTime { get; set; }
        public float HoverAmplitude { get; set; }
        public float HoverFrequency { get; set; } = 1f;
        public float HoverPhase { get; set; }
        public float TargetX { get; set; }
        public float TargetY { get; set; }
        public int MotionElapsedMs { get; set; }
        public int MotionLastUpdateTime { get; set; }
        public int ParabolicDurationMs { get; set; }
        public float PickupStartX { get; set; }
        public float PickupStartY { get; set; }
        public float PickupTargetX { get; set; }
        public float PickupTargetY { get; set; }
        public int PickupDurationMs { get; set; }
        public bool UsePickupAbsorbMotion { get; set; }
        public Func<Vector2?> PickupTargetPositionResolver { get; set; }
        public int RemovalFadeDurationMs { get; set; }
        public bool TriggerPacketExplodeEffectOnRemove { get; set; }
        public float RemovalStartScale { get; set; } = 1f;
        public float RemovalTargetScale { get; set; } = 1f;
        public int PacketEnterAlphaRampStartTime { get; set; }
        public int PacketEnterAlphaRampDurationMs { get; set; }
        public bool FreezeAnimationDuringRemovalFade { get; set; }
        #endregion

        #region Constants
        // Physics constants - tuned for snappy drop feel
        // Higher gravity = faster falling after the initial pop
        public const float GRAVITY = 1200f;             // Gravity acceleration (px/s²)
        public const float BOUNCE_DAMPENING = 0.5f;
        public const float MIN_BOUNCE_VELOCITY = 30f;
        public const int MAX_BOUNCES = 3;
        public const int SPAWN_FLOAT_DURATION = 200;    // Float phase before physics kicks in
        public const int EXPIRE_FADE_DURATION = 1000;   // Fade out duration before expire
        public const int PICKUP_DURATION = 200;         // Pickup animation duration
        public const float SPAWN_FLOAT_HEIGHT = 50f;    // Initial offset (client uses parabolic arc)
        public const int PACKET_REMOVE_DURATION = 1000;
        public const int PACKET_ABSORB_DURATION = 220;
        public const int PACKET_ENTER_TYPE3_ALPHA_RAMP_DURATION = 1000;
        public const int PACKET_MOTION_STEP_MS = 30;
        #endregion

        public bool IsExpired => State == DropState.Expired || State == DropState.Removed;

        public void Update(int currentTime, float deltaTime)
        {
            switch (State)
            {
                case DropState.Spawning:
                    UpdateSpawning(currentTime);
                    break;
                case DropState.Falling:
                    UpdateFalling(currentTime, deltaTime);
                    break;
                case DropState.Bouncing:
                    UpdateBouncing(currentTime, deltaTime);
                    break;
                case DropState.Idle:
                    UpdateIdle(currentTime);
                    break;
                case DropState.PickingUp:
                    UpdatePickingUp(currentTime);
                    break;
                case DropState.Expired:
                    UpdateExpired(currentTime);
                    break;
            }

            ApplyPacketEnterAlphaRamp(currentTime);

            // Update animation frames
            UpdateAnimation(currentTime);
        }

        private void UpdateSpawning(int currentTime)
        {
            if (ShouldHoldPacketEnterAlphaAtZero(currentTime))
            {
                Alpha = 0f;
            }

            if (IsPacketCreateDelayPending(currentTime))
            {
                return;
            }

            State = DropState.Falling;
            LastStateChangeTime = currentTime;
            MotionElapsedMs = 0;
            MotionLastUpdateTime = currentTime;
            CurrentFrame = 0;
            LastFrameTime = currentTime;
            if (IsPacketEnterType3AlphaRampActive)
            {
                PacketEnterAlphaRampStartTime = currentTime;
                Alpha = 0f;
            }
        }

        private void UpdateFalling(int currentTime, float deltaTime)
        {
            if (UsesClientPacketMotion())
            {
                UpdatePacketParabolicMotion(currentTime);
                return;
            }

            VelocityY += GRAVITY * deltaTime;
            Y += VelocityY * deltaTime;
            X += VelocityX * deltaTime;

            // Check ground collision
            if (Y >= GroundY)
            {
                Y = GroundY;

                if (MathF.Abs(VelocityY) > MIN_BOUNCE_VELOCITY && BounceCount < MAX_BOUNCES)
                {
                    // Bounce
                    VelocityY = -VelocityY * BOUNCE_DAMPENING;
                    VelocityX *= BOUNCE_DAMPENING;
                    BounceCount++;
                    State = DropState.Bouncing;
                }
                else
                {
                    // Stop bouncing, go idle
                    VelocityX = 0;
                    VelocityY = 0;
                    State = DropState.Idle;
                    LastStateChangeTime = currentTime;
                }
            }
        }

        private void UpdateBouncing(int currentTime, float deltaTime)
        {
            if (UsesClientPacketMotion())
            {
                UpdatePacketSettleMotion(currentTime);
                return;
            }

            // Same as falling but tracks bounce count
            UpdateFalling(currentTime, deltaTime);
        }

        private void UpdateIdle(int currentTime)
        {
            // Check expiration
            if (ExpireTime > 0 && currentTime >= ExpireTime - EXPIRE_FADE_DURATION)
            {
                // Start fading
                int fadeRemaining = ExpireTime - currentTime;
                if (fadeRemaining <= 0)
                {
                    State = DropState.Expired;
                    Alpha = 0;
                }
                else
                {
                    Alpha = (float)fadeRemaining / EXPIRE_FADE_DURATION;
                }
            }

            if (HoverAmplitude > 0f)
            {
                float elapsedSeconds = Math.Max(0, currentTime - SpawnTime) / 1000f;
                Y = GroundY + HoverAmplitude * MathF.Sin(HoverPhase + elapsedSeconds * HoverFrequency * MathF.PI * 2f);
            }
            else
            {
                Y = GroundY;
            }

            // Gentle hover animation for rare items
            if (IsRare)
            {
                float hoverT = (currentTime % 1000) / 1000f;
                Scale = 1.0f + 0.05f * MathF.Sin(hoverT * MathF.PI * 2);
            }
        }

        private void UpdatePickingUp(int currentTime)
        {
            int durationMs = Math.Max(1, PickupDurationMs > 0 ? PickupDurationMs : PICKUP_DURATION);
            int elapsed = currentTime - LastStateChangeTime;
            float t = (float)elapsed / durationMs;

            if (t >= 1.0f)
            {
                State = DropState.Removed;
                Alpha = 0;
                return;
            }

            if (UsePickupAbsorbMotion)
            {
                if (PickupTargetPositionResolver?.Invoke() is Vector2 liveTargetPosition)
                {
                    PickupTargetX = liveTargetPosition.X;
                    PickupTargetY = liveTargetPosition.Y;
                }

                float easedT = 1f - MathF.Pow(1f - t, 3f);
                X = MathHelper.Lerp(PickupStartX, PickupTargetX, easedT);
                Y = MathHelper.Lerp(PickupStartY, PickupTargetY, easedT) - (18f * MathF.Sin(t * MathF.PI));
                Alpha = 1f - t;
                Scale = 1f - (0.35f * t);
                return;
            }

            Y = GroundY - (30f * t);
            Alpha = 1f - t;
            Scale = 1f + (0.3f * t);
        }

        private void UpdateExpired(int currentTime)
        {
            if (RemovalFadeDurationMs > 0)
            {
                int elapsed = currentTime - LastStateChangeTime;
                float t = Math.Clamp((float)elapsed / RemovalFadeDurationMs, 0f, 1f);
                Alpha = 1f - t;
                Scale = MathHelper.Lerp(RemovalStartScale, RemovalTargetScale, t);
                if (t >= 1f)
                {
                    State = DropState.Removed;
                    Alpha = 0f;
                }

                return;
            }

            if (ScheduledRemovalTime <= 0 || currentTime >= ScheduledRemovalTime)
            {
                State = DropState.Removed;
            }
        }

        private bool UsesClientPacketMotion()
        {
            return IsPacketControlled && PacketEnterType != 2 && ParabolicDurationMs > 0;
        }

        private void UpdatePacketParabolicMotion(int currentTime)
        {
            MotionLastUpdateTime = currentTime;
            MotionElapsedMs += PACKET_MOTION_STEP_MS;

            float horizontalHalfDistance = (TargetX - SpawnX) * 0.5f;
            float progressA = MathF.Min(MotionElapsedMs / 500f, 1f);
            float progressB = MotionElapsedMs <= 500
                ? 0f
                : MathF.Min((MotionElapsedMs - 500f) / MathF.Max(1f, ParabolicDurationMs - 500f), 1f);
            float arcTimeSeconds = MotionElapsedMs / 1000f;
            if (PacketEnterType == 4)
            {
                arcTimeSeconds /= 3f;
            }

            float launchVelocity = OwnershipType == DropOwnershipType.Explosive ? 720f : 400f;
            X = SpawnX + (progressA + progressB) * horizontalHalfDistance;
            Y = SpawnY - (launchVelocity * arcTimeSeconds) + (400f * arcTimeSeconds * arcTimeSeconds);

            if (MotionElapsedMs < ParabolicDurationMs)
            {
                return;
            }

            if (SpawnY < TargetY)
            {
                State = DropState.Bouncing;
                MotionElapsedMs = 0;
                MotionLastUpdateTime = currentTime;
                return;
            }

            SnapToPacketIdle(currentTime);
        }

        private void UpdatePacketSettleMotion(int currentTime)
        {
            MotionLastUpdateTime = currentTime;
            MotionElapsedMs += PACKET_MOTION_STEP_MS;

            float settleTimeSeconds = MotionElapsedMs / 1000f;
            float launchVelocity = OwnershipType == DropOwnershipType.Explosive ? 720f : 400f;
            float settledY = SpawnY + (settleTimeSeconds * launchVelocity);

            X = SpawnY < TargetY ? TargetX : SpawnX;
            Y = settledY;
            if (settledY >= TargetY)
            {
                SnapToPacketIdle(currentTime);
            }
        }

        private void SnapToPacketIdle(int currentTime)
        {
            X = TargetX;
            Y = TargetY;
            VelocityX = 0f;
            VelocityY = 0f;
            State = DropState.Idle;
            LastStateChangeTime = currentTime;
            MotionElapsedMs = 0;
            MotionLastUpdateTime = currentTime;
        }

        private bool IsPacketEnterType3AlphaRampActive =>
            IsPacketControlled
            && PacketEnterType == 3
            && PacketEnterAlphaRampDurationMs > 0;

        private bool ShouldHoldPacketEnterAlphaAtZero(int currentTime)
        {
            return IsPacketEnterType3AlphaRampActive
                && IsPacketCreateDelayPending(currentTime);
        }

        private bool IsPacketCreateDelayPending(int currentTime)
        {
            if (!IsPacketControlled || CreateDelayMs <= 0)
            {
                return false;
            }

            return currentTime - (SpawnTime + CreateDelayMs) <= 0;
        }

        private void ApplyPacketEnterAlphaRamp(int currentTime)
        {
            if (!IsPacketEnterType3AlphaRampActive
                || State == DropState.PickingUp
                || State == DropState.Expired
                || State == DropState.Removed)
            {
                return;
            }

            if (ShouldHoldPacketEnterAlphaAtZero(currentTime))
            {
                Alpha = 0f;
                return;
            }

            int elapsed = currentTime - PacketEnterAlphaRampStartTime;
            if (elapsed <= 0)
            {
                Alpha = 0f;
                return;
            }

            Alpha = Math.Min(Alpha, 1f);
            Alpha = Math.Max(Alpha, Math.Clamp(elapsed / (float)PacketEnterAlphaRampDurationMs, 0f, 1f));
        }

        private void UpdateAnimation(int currentTime)
        {
            if (AnimFrames == null || AnimFrames.Count <= 1)
                return;

            if (FreezeAnimationDuringRemovalFade)
            {
                return;
            }

            if (State == DropState.Spawning
                && IsPacketCreateDelayPending(currentTime))
            {
                return;
            }

            var frame = AnimFrames[CurrentFrame];
            int delay = frame?.Delay ?? 100;

            if (currentTime - LastFrameTime >= delay)
            {
                CurrentFrame = (CurrentFrame + 1) % AnimFrames.Count;
                LastFrameTime = currentTime;
            }
        }

        /// <summary>
        /// Start pickup animation
        /// </summary>
        public void StartPickup(
            int currentTime,
            Vector2? targetPosition = null,
            int durationMs = PICKUP_DURATION,
            Func<Vector2?> targetPositionResolver = null,
            Vector2? startPositionOverride = null)
        {
            if (State == DropState.Removed || State == DropState.Expired)
                return;

            State = DropState.PickingUp;
            LastStateChangeTime = currentTime;
            ScheduledRemovalTime = 0;
            Vector2 startPosition = startPositionOverride ?? new Vector2(X, Y);
            PickupStartX = startPosition.X;
            PickupStartY = startPosition.Y;
            X = startPosition.X;
            Y = startPosition.Y;
            PickupDurationMs = Math.Max(1, durationMs);
            UsePickupAbsorbMotion = targetPosition.HasValue;
            PickupTargetX = targetPosition?.X ?? X;
            PickupTargetY = targetPosition?.Y ?? GroundY - 30f;
            PickupTargetPositionResolver = targetPositionResolver;
            RemovalFadeDurationMs = 0;
        }

        public void ScheduleRemoval(int currentTime, int delayMs, bool fadeOut)
        {
            CanPickup = false;
            ScheduledRemovalTime = currentTime + Math.Max(0, delayMs);
            State = DropState.Expired;
            LastStateChangeTime = currentTime;
            RemovalFadeDurationMs = 0;
            if (fadeOut && delayMs <= 0)
            {
                StartRemoveFade(currentTime, PACKET_REMOVE_DURATION);
            }
        }

        public void StartRemoveFade(int currentTime, int durationMs, float targetScale = 1f)
        {
            CanPickup = false;
            State = DropState.Expired;
            LastStateChangeTime = currentTime;
            ScheduledRemovalTime = 0;
            RemovalFadeDurationMs = Math.Max(1, durationMs);
            RemovalStartScale = Scale;
            RemovalTargetScale = targetScale;
            Alpha = Math.Max(Alpha, 0f);

            if (!UseLayeredMesoAnimation && AnimFrames != null && AnimFrames.Count > 1)
            {
                FreezeAnimationDuringRemovalFade = true;
            }
        }

        public void SnapToTargetPosition()
        {
            X = TargetX;
            Y = TargetY;
            VelocityX = 0f;
            VelocityY = 0f;
            MotionElapsedMs = 0;
            MotionLastUpdateTime = LastStateChangeTime;
        }

        /// <summary>
        /// Check if a position is close enough to pick up this drop
        /// </summary>
        public bool IsInPickupRange(float playerX, float playerY, float range = 40f)
        {
            if (State != DropState.Idle)
                return false;

            float dx = X - playerX;
            float dy = Y - playerY;
            return (dx * dx + dy * dy) <= range * range;
        }
    }

    /// <summary>
    /// Drop Pool System - Manages item drops, physics, and pickup
    /// Based on CDropPool from MapleStory client
    /// </summary>
    public class DropPool
    {
        #region Constants
        private const int DEFAULT_DROP_LIFETIME = 120000;       // 2 minutes
        private const int OWNER_PRIORITY_DURATION = 15000;      // 15 seconds owner priority
        private const float DROP_SPREAD = 30f;                  // Horizontal spread of multiple drops
        private const float DROP_INITIAL_VELOCITY_Y = -200f;    // Initial upward velocity (tuned for snappy feel)
        private const int MAX_DROPS = 200;

        // Pet pickup constants
        private const float PET_PICKUP_RANGE = 80f;             // Range at which pet detects drops
        private const float PET_LOOT_RANGE = 300f;              // Max range pet will travel to loot
        private const float PET_CHASE_SPEED = 150f;             // Pet movement speed when chasing drops
        private const int PET_PICKUP_COOLDOWN = 200;            // Cooldown between pet pickups (ms)
        private const int PICKUP_FAILURE_REPORT_COOLDOWN = 1500;

        // Mob pickup constants
        private const float MOB_PICKUP_RANGE = 30f;             // Range for mob pickup detection

        // Meso explosion constants
        private const float MESO_EXPLOSION_RANGE = 150f;        // Default meso explosion range
        private const int MAX_MESO_EXPLOSION_DROPS = 10;        // Max mesos in single explosion
        private const int PACKET_ENTER_SOUND_COOLDOWN = 300;
        private const int PACKET_EXPLODE_SOUND_COOLDOWN = 90;

        // Recent pickup history
        private const int MAX_RECENT_PICKUPS = 50;              // Max pickup history entries
        private const int RECENT_PICKUP_LIFETIME = 30000;       // How long to keep pickup records (30s)
        #endregion

        #region Collections
        private readonly List<DropItem> _activeDrops = new List<DropItem>();
        private readonly Dictionary<int, DropItem> _dropById = new Dictionary<int, DropItem>();
        private readonly Queue<DropItem> _dropPool = new Queue<DropItem>();
        private readonly Random _random = new Random();

        // Pet chasing system
        private readonly Dictionary<int, PetDropTarget> _petTargets = new Dictionary<int, PetDropTarget>();
        private readonly Dictionary<int, int> _petLastPickupTime = new Dictionary<int, int>();

        // Mob pickup tracking
        private readonly HashSet<int> _mobsWithPickupAbility = new HashSet<int>();

        // Exception list for filtering
        private readonly List<DropExceptionEntry> _exceptionList = new List<DropExceptionEntry>();
        private bool _useWhitelist = false;  // false = blocklist mode, true = whitelist mode

        // Recent pickup history
        private readonly Queue<RecentPickupRecord> _recentPickups = new Queue<RecentPickupRecord>();

        // Booby trap drops tracking
        private readonly Dictionary<int, int> _boobyTrapDrops = new Dictionary<int, int>(); // dropId -> trapOwnerId
        #endregion

        #region State
        private int _nextDropId = 1;
        private int _lastUpdateTick = 0;
        private int _lastPacketEnterSoundTime = int.MinValue;
        private int _lastPacketExplodeSoundTime = int.MinValue;
        private Action<DropItem> _onDropSpawned;
        private Action<DropItem> _onDropPickedUp;
        private Action<DropItem> _onDropExpired;
        private Action<DropItem, int, bool> _onPickupResolved;
        private Action<DropPickupAttemptResult, int, bool> _onPickupFailed;
        private Func<DropItem, DropPickupFailureReason> _pickupAvailabilityEvaluator;
        private Func<DropItem, DropPickupFailureReason> _petPickupAvailabilityEvaluator;
        private Func<DropPickupActorKind, int, bool, string> _pickupActorNameResolver;
        private Action<DropItem, int, string> _onRemotePlayerPickedUp;
        private Action<DropItem, int, string> _onRemotePetPickedUp;
        private Action<DropItem, int, string> _onRemoteOtherPickedUp;
        private Func<int, int, bool> _partyPickupMembershipEvaluator;
        private Func<string, IReadOnlyList<IDXObject>> _packetItemVisualResolver;
        private Action<DropItem, int> _onPacketEnterSoundRequested;
        private Action<DropItem, int> _onPacketExploded;
        private Func<DateTime> _packetExpireTimeUtcResolver = () => DateTime.UtcNow;

        // Ground level lookup function
        private Func<float, float, float> _getGroundY;
        private Func<int, Vector2?> _sourcePositionResolver;
        #endregion

        #region Resources
        private IDXObject _mesoIcon;
        private readonly List<IDXObject>[] _mesoAnimationFrames = new List<IDXObject>[] { new(), new(), new(), new() };
        private Dictionary<string, IDXObject> _itemIcons = new Dictionary<string, IDXObject>();
        private readonly Dictionary<string, List<IDXObject>> _packetItemVisualFrames = new Dictionary<string, List<IDXObject>>(StringComparer.Ordinal);
        #endregion

        #region Public Properties
        public int ActiveDropCount => _activeDrops.Count;
        public IReadOnlyList<DropItem> ActiveDrops => _activeDrops;
        #endregion

        #region Events
        public void SetOnDropSpawned(Action<DropItem> callback) => _onDropSpawned = callback;
        public void SetOnDropPickedUp(Action<DropItem> callback) => _onDropPickedUp = callback;
        public void SetOnDropExpired(Action<DropItem> callback) => _onDropExpired = callback;
        public void SetOnPickupResolved(Action<DropItem, int, bool> callback) => _onPickupResolved = callback;
        public void SetOnPickupFailed(Action<DropPickupAttemptResult, int, bool> callback) => _onPickupFailed = callback;
        public void SetPickupAvailabilityEvaluator(Func<DropItem, DropPickupFailureReason> callback) => _pickupAvailabilityEvaluator = callback;
        public void SetPetPickupAvailabilityEvaluator(Func<DropItem, DropPickupFailureReason> callback) => _petPickupAvailabilityEvaluator = callback;
        public void SetPickupActorNameResolver(Func<DropPickupActorKind, int, bool, string> callback) => _pickupActorNameResolver = callback;
        public void SetGroundLevelLookup(Func<float, float, float> getGroundY) => _getGroundY = getGroundY;
        public void SetSourcePositionResolver(Func<int, Vector2?> resolver) => _sourcePositionResolver = resolver;
        public void SetPartyPickupMembershipEvaluator(Func<int, int, bool> evaluator) => _partyPickupMembershipEvaluator = evaluator;
        public void SetPacketItemVisualResolver(Func<string, IReadOnlyList<IDXObject>> resolver) => _packetItemVisualResolver = resolver;
        public void SetPacketExpireTimeUtcResolver(Func<DateTime> resolver) => _packetExpireTimeUtcResolver = resolver ?? (() => DateTime.UtcNow);
        public void SetOnRemotePlayerPickedUp(Action<DropItem, int, string> callback) => _onRemotePlayerPickedUp = callback;
        public void SetOnRemotePetPickedUp(Action<DropItem, int, string> callback) => _onRemotePetPickedUp = callback;
        public void SetOnRemoteOtherPickedUp(Action<DropItem, int, string> callback) => _onRemoteOtherPickedUp = callback;
        public void SetOnPacketEnterSoundRequested(Action<DropItem, int> callback) => _onPacketEnterSoundRequested = callback;
        public void SetOnPacketExploded(Action<DropItem, int> callback) => _onPacketExploded = callback;

        // Pet pickup events
        private Action<DropItem, int> _onPetPickedUp;          // (drop, petId)
        private Action<int, DropItem> _onPetStartChasing;      // (petId, drop)
        public void SetOnPetPickedUp(Action<DropItem, int> callback) => _onPetPickedUp = callback;
        public void SetOnPetStartChasing(Action<int, DropItem> callback) => _onPetStartChasing = callback;

        // Mob pickup events
        private Action<DropItem, int> _onMobPickedUp;          // (drop, mobId)
        public void SetOnMobPickedUp(Action<DropItem, int> callback) => _onMobPickedUp = callback;

        // Booby trap events
        private Action<DropItem, int> _onBoobyTrapTriggered;   // (drop, trapOwnerId)
        public void SetOnBoobyTrapTriggered(Action<DropItem, int> callback) => _onBoobyTrapTriggered = callback;
        #endregion

        #region Initialization
        public void Initialize(IDXObject mesoIcon = null)
        {
            _mesoIcon = mesoIcon;
            Clear();
        }

        public void SetMesoIcon(IDXObject icon)
        {
            _mesoIcon = icon;
        }

        public void SetMesoAnimationFrames(int iconType, List<IDXObject> frames)
        {
            if ((uint)iconType >= _mesoAnimationFrames.Length)
            {
                return;
            }

            _mesoAnimationFrames[iconType].Clear();
            if (frames == null)
            {
                return;
            }

            _mesoAnimationFrames[iconType].AddRange(frames.Where(frame => frame != null));
        }

        public void SetItemIcon(string itemId, IDXObject icon)
        {
            _itemIcons[itemId] = icon;
        }

        public void Clear()
        {
            foreach (var drop in _activeDrops)
            {
                _dropPool.Enqueue(drop);
            }
            _activeDrops.Clear();
            _dropById.Clear();
            _nextDropId = 1;
            _lastPacketEnterSoundTime = int.MinValue;
            _lastPacketExplodeSoundTime = int.MinValue;

            // Clear new collections
            _petTargets.Clear();
            _petLastPickupTime.Clear();
            _mobsWithPickupAbility.Clear();
            _recentPickups.Clear();
            _boobyTrapDrops.Clear();
            // Note: Exception list is preserved across clear (user preference)
        }

        public void ClearPacketDrops()
        {
            for (int i = _activeDrops.Count - 1; i >= 0; i--)
            {
                DropItem drop = _activeDrops[i];
                if (!drop.IsPacketControlled)
                {
                    continue;
                }

                RemoveDrop(drop);
            }
        }
        #endregion

        #region Drop Spawning
        /// <summary>
        /// Spawn a meso drop
        /// </summary>
        public DropItem SpawnMesoDrop(float x, float y, int amount, int currentTime, int ownerId = 0)
        {
            var drop = GetOrCreateDrop();
            InitializeDrop(drop, DropType.Meso, null, x, y, currentTime, ownerId);
            drop.MesoAmount = amount;
            drop.Icon = _mesoIcon;
            ApplyMesoVisuals(drop, packetControlled: false);

            // Meso glow based on amount
            if (amount >= 10000)
            {
                drop.IsRare = true;
                drop.GlowColor = new Color(255, 215, 0); // Gold
            }
            else if (amount >= 1000)
            {
                drop.GlowColor = new Color(192, 192, 192); // Silver
            }

            _activeDrops.Add(drop);
            _dropById[drop.PoolId] = drop;
            _onDropSpawned?.Invoke(drop);

            return drop;
        }

        /// <summary>
        /// Spawn an item drop
        /// </summary>
        public DropItem SpawnItemDrop(float x, float y, string itemId, int quantity, int currentTime, int ownerId = 0, bool isRare = false)
        {
            var drop = GetOrCreateDrop();
            InitializeDrop(drop, DropType.Item, itemId, x, y, currentTime, ownerId);
            drop.Quantity = quantity;
            drop.IsRare = isRare;

            // Try to get item icon
            if (_itemIcons.TryGetValue(itemId, out var icon))
            {
                drop.Icon = icon;
            }

            if (isRare)
            {
                drop.GlowColor = new Color(200, 150, 255); // Purple glow for rare
            }

            _activeDrops.Add(drop);
            _dropById[drop.PoolId] = drop;
            _onDropSpawned?.Invoke(drop);

            return drop;
        }

        /// <summary>
        /// Spawn multiple drops from a mob death
        /// </summary>
        public List<DropItem> SpawnDropsFromMob(float mobX, float mobY, int currentTime, int ownerId = 0,
            int mesoAmount = 0, List<(string itemId, int quantity, bool isRare)> items = null)
        {
            var spawned = new List<DropItem>();
            int dropIndex = 0;

            // Spawn meso if any
            if (mesoAmount > 0)
            {
                float offsetX = GetDropOffset(dropIndex++, 1 + (items?.Count ?? 0));
                var mesoDrop = SpawnMesoDrop(mobX + offsetX, mobY, mesoAmount, currentTime, ownerId);
                spawned.Add(mesoDrop);
            }

            // Spawn items
            if (items != null)
            {
                int totalDrops = (mesoAmount > 0 ? 1 : 0) + items.Count;
                foreach (var (itemId, quantity, isRare) in items)
                {
                    float offsetX = GetDropOffset(dropIndex++, totalDrops);
                    var itemDrop = SpawnItemDrop(mobX + offsetX, mobY, itemId, quantity, currentTime, ownerId, isRare);
                    spawned.Add(itemDrop);
                }
            }

            return spawned;
        }

        private float GetDropOffset(int index, int total)
        {
            if (total <= 1)
                return 0;

            // Spread drops in an arc
            float totalWidth = (total - 1) * DROP_SPREAD;
            return -totalWidth / 2f + index * DROP_SPREAD;
        }

        private DropItem GetOrCreateDrop()
        {
            if (_dropPool.Count > 0)
                return _dropPool.Dequeue();

            // Remove oldest if at capacity
            if (_activeDrops.Count >= MAX_DROPS)
            {
                var oldest = _activeDrops[0];
                RemoveDrop(oldest);
            }

            return new DropItem();
        }

        private void InitializeDrop(DropItem drop, DropType type, string itemId, float x, float y, int currentTime, int ownerId)
        {
            drop.PoolId = _nextDropId++;
            drop.Type = type;
            drop.ItemId = itemId;
            drop.X = x;
            drop.SpawnX = x;
            drop.SpawnY = y;
            drop.TargetX = x;
            drop.TargetY = y;
            // Start above spawn point so the arc animation is visible
            // The drop will arc up slightly then fall to ground
            drop.Y = y - 60;  // Start 60px above mob position
            drop.VelocityX = (float)(_random.NextDouble() * 80 - 40); // Random horizontal spread
            drop.VelocityY = DROP_INITIAL_VELOCITY_Y + (float)(_random.NextDouble() * 40 - 20);  // Upward arc
            // Ground is at mob's feet position (or foothold below)
            drop.GroundY = _getGroundY?.Invoke(x, y) ?? y;
            drop.State = DropState.Spawning;
            drop.SpawnTime = currentTime;
            drop.BounceCount = 0;
            drop.Alpha = 1.0f;  // Visible immediately (no fade-in animation)
            drop.Scale = 1.0f;
            drop.Rotation = 0;
            drop.CurrentFrame = 0;
            drop.LastFrameTime = currentTime;
            drop.LastStateChangeTime = currentTime;
            drop.OwnerId = ownerId;
            drop.OwnerExpireTime = ownerId > 0 ? currentTime + OWNER_PRIORITY_DURATION : 0;
            drop.ExpireTime = currentTime + DEFAULT_DROP_LIFETIME;
            drop.OwnershipType = ownerId > 0 ? DropOwnershipType.Character : DropOwnershipType.None;
            drop.SourceId = 0;
            drop.IsReal = true;
            drop.AllowPetPickup = true;
            drop.IsPacketControlled = false;
            drop.PacketEnterType = 0;
            drop.CreateDelayMs = 0;
            drop.ScheduledRemovalTime = 0;
            drop.HoverAmplitude = 0f;
            drop.HoverFrequency = 1f;
            drop.HoverPhase = 0f;
            drop.MotionElapsedMs = 0;
            drop.MotionLastUpdateTime = currentTime;
            drop.ParabolicDurationMs = 0;
            drop.PickupStartX = 0f;
            drop.PickupStartY = 0f;
            drop.PickupTargetX = 0f;
            drop.PickupTargetY = 0f;
            drop.PickupDurationMs = 0;
            drop.UsePickupAbsorbMotion = false;
            drop.PickupTargetPositionResolver = null;
            drop.RemovalFadeDurationMs = 0;
            drop.TriggerPacketExplodeEffectOnRemove = false;
            drop.RemovalStartScale = 1f;
            drop.RemovalTargetScale = 1f;
            drop.PacketEnterAlphaRampStartTime = 0;
            drop.PacketEnterAlphaRampDurationMs = 0;
            drop.FreezeAnimationDuringRemovalFade = false;
            drop.CanPickup = true;
            drop.IsRare = false;
            drop.GlowColor = Color.White;
            drop.UseLayeredMesoAnimation = false;
            drop.MesoAnimationLayerCount = 0;
            drop.MesoAnimationIconType = 0;
            drop.DrawOnElevatedLayer = false;
            drop.AnimFrames = null;
            drop.Icon = null;
            drop.Quantity = 1;
            drop.MesoAmount = 0;
            drop.LastPickupFailureTime = int.MinValue;
            drop.LastPickupFailureReason = DropPickupFailureReason.None;
        }

        public static int GetMoneyIconTypeForAmount(int amount)
        {
            if (amount < 50)
            {
                return 0;
            }

            if (amount < 100)
            {
                return 1;
            }

            return amount < 1000 ? 2 : 3;
        }
        #endregion

        #region Drop Lookup
        /// <summary>
        /// Get drop by ID
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DropItem GetDrop(int dropId)
        {
            return _dropById.TryGetValue(dropId, out var drop) ? drop : null;
        }

        /// <summary>
        /// Get drops within pickup range of a position
        /// </summary>
        public IEnumerable<DropItem> GetDropsInRange(float x, float y, float range = 40f)
        {
            foreach (var drop in _activeDrops)
            {
                if (drop.IsInPickupRange(x, y, range))
                    yield return drop;
            }
        }

        /// <summary>
        /// Find closest pickupable drop
        /// </summary>
        public DropItem GetClosestDrop(float x, float y, float maxRange = 40f, int playerId = 0)
        {
            DropItem closest = null;
            float closestDist = maxRange * maxRange;
            int currentTime = _lastUpdateTick;

            foreach (var drop in _activeDrops)
            {
                if (drop.State != DropState.Idle || !drop.CanPickup)
                    continue;

                if (!CanPlayerPickup(drop, playerId, currentTime))
                    continue;

                float dx = drop.X - x;
                float dy = drop.Y - y;
                float distSq = dx * dx + dy * dy;

                if (distSq < closestDist)
                {
                    closestDist = distSq;
                    closest = drop;
                }
            }

            return closest;
        }
        #endregion

        #region Pickup
        /// <summary>
        /// Attempt to pick up a drop
        /// </summary>
        public bool TryPickup(DropItem drop, int playerId, int currentTime)
        {
            if (drop == null || drop.State != DropState.Idle || !drop.CanPickup)
                return false;

            if (!CanPlayerPickup(drop, playerId, currentTime))
                return false;

            return CompletePickup(drop, playerId, pickedByPet: false, currentTime, DropPickupActorKind.Player, notifyLocalPickup: true);
        }

        public bool ResolveRemotePickup(
            DropItem drop,
            int actorId,
            int currentTime,
            DropPickupActorKind actorKind,
            string actorName = null,
            bool pickedByPet = false,
            Vector2? pickupTargetPosition = null,
            Func<Vector2?> pickupTargetPositionResolver = null,
            Vector2? pickupStartPositionOverride = null,
            bool bypassStateValidation = false)
        {
            if (actorKind == DropPickupActorKind.Player)
            {
                return CompletePickup(
                    drop,
                    actorId,
                    pickedByPet: false,
                    currentTime,
                    actorKind,
                    notifyLocalPickup: false,
                    actorName,
                    pickupTargetPosition,
                    pickupTargetPositionResolver: pickupTargetPositionResolver,
                    pickupStartPositionOverride: pickupStartPositionOverride,
                    bypassStateValidation: bypassStateValidation);
            }

            if (actorKind == DropPickupActorKind.Pet)
            {
                return CompletePickup(
                    drop,
                    actorId,
                    pickedByPet: true,
                    currentTime,
                    actorKind,
                    notifyLocalPickup: false,
                    actorName,
                    pickupTargetPosition,
                    pickupTargetPositionResolver: pickupTargetPositionResolver,
                    pickupStartPositionOverride: pickupStartPositionOverride,
                    bypassStateValidation: bypassStateValidation);
            }

            if (actorKind == DropPickupActorKind.Mob)
            {
                return CompletePickup(
                    drop,
                    actorId,
                    pickedByPet: false,
                    currentTime,
                    actorKind,
                    notifyLocalPickup: false,
                    actorName,
                    pickupTargetPosition,
                    pickupTargetPositionResolver: pickupTargetPositionResolver,
                    pickupStartPositionOverride: pickupStartPositionOverride,
                    bypassStateValidation: bypassStateValidation);
            }

            if (actorKind == DropPickupActorKind.Other)
            {
                return CompletePickup(
                    drop,
                    actorId,
                    pickedByPet: false,
                    currentTime,
                    actorKind,
                    notifyLocalPickup: false,
                    actorName,
                    pickupTargetPosition,
                    pickupTargetPositionResolver: pickupTargetPositionResolver,
                    pickupStartPositionOverride: pickupStartPositionOverride,
                    bypassStateValidation: bypassStateValidation);
            }

            return false;
        }

        public DropItem TryPickUpDropByRemotePlayer(
            int playerId,
            float playerX,
            float playerY,
            int currentTime,
            string playerName = null,
            float pickupRange = 40f)
        {
            if (playerId <= 0)
            {
                return null;
            }

            float rangeSq = pickupRange * pickupRange;
            DropItem closestDrop = null;
            float closestDistSq = float.MaxValue;

            foreach (var drop in _activeDrops)
            {
                if (drop.State != DropState.Idle || !drop.CanPickup)
                {
                    continue;
                }

                if (!CanPlayerPickup(drop, playerId, currentTime))
                {
                    continue;
                }

                float dx = drop.X - playerX;
                float dy = drop.Y - playerY;
                float distSq = dx * dx + dy * dy;
                if (distSq > rangeSq)
                {
                    continue;
                }

                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closestDrop = drop;
                }
            }

            if (closestDrop == null)
            {
                return null;
            }

            return ResolveRemotePickup(
                closestDrop,
                playerId,
                currentTime,
                DropPickupActorKind.Player,
                playerName)
                ? closestDrop
                : null;
        }

        public DropItem TryPickUpDropByRemotePet(
            int petId,
            int ownerId,
            float petX,
            float petY,
            int currentTime,
            string petName = null,
            float petPickupRange = 0,
            Func<DropItem, DropPickupFailureReason> pickupValidator = null)
        {
            if (petPickupRange <= 0)
            {
                petPickupRange = PET_PICKUP_RANGE;
            }

            float rangeSq = petPickupRange * petPickupRange;
            DropItem closestDrop = null;
            float closestDistSq = float.MaxValue;

            foreach (var drop in _activeDrops)
            {
                if (drop.State != DropState.Idle || !drop.CanPickup)
                {
                    continue;
                }

                float dx = drop.X - petX;
                float dy = drop.Y - petY;
                float distSq = dx * dx + dy * dy;
                if (distSq > rangeSq)
                {
                    continue;
                }

                if (!CanPetPickup(drop, ownerId, currentTime))
                {
                    continue;
                }

                if ((pickupValidator?.Invoke(drop) ?? DropPickupFailureReason.None) != DropPickupFailureReason.None)
                {
                    continue;
                }

                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closestDrop = drop;
                }
            }

            return ResolveRemotePickup(
                closestDrop,
                petId,
                currentTime,
                DropPickupActorKind.Pet,
                petName,
                pickedByPet: true)
                ? closestDrop
                : null;
        }

        /// <summary>
        /// Pick up closest drop in range
        /// </summary>
        public DropItem TryPickupClosest(float x, float y, int playerId, int currentTime, float range = 40f)
        {
            return TryPickupClosestDetailed(x, y, playerId, currentTime, range).Drop;
        }

        public DropPickupAttemptResult TryPickupClosestDetailed(
            float x,
            float y,
            int playerId,
            int currentTime,
            float range = 40f,
            Func<DropItem, DropPickupFailureReason> pickupValidator = null)
        {
            pickupValidator ??= _pickupAvailabilityEvaluator;
            float rangeSq = range * range;
            DropItem closestAvailable = null;
            float closestAvailableDistSq = float.MaxValue;
            DropItem closestFailureDrop = null;
            DropPickupFailureReason closestFailureReason = DropPickupFailureReason.NoDropInRange;
            float closestFailureDistSq = float.MaxValue;

            foreach (var drop in _activeDrops)
            {
                if (drop.State != DropState.Idle || !drop.CanPickup)
                {
                    continue;
                }

                float dx = drop.X - x;
                float dy = drop.Y - y;
                float distSq = dx * dx + dy * dy;
                if (distSq > rangeSq)
                {
                    continue;
                }

                if (!CanPlayerPickup(drop, playerId, currentTime))
                {
                    if (distSq < closestFailureDistSq)
                    {
                        closestFailureDrop = drop;
                        closestFailureDistSq = distSq;
                        closestFailureReason = DropPickupFailureReason.OwnershipRestricted;
                    }

                    continue;
                }

                DropPickupFailureReason validatorReason = pickupValidator?.Invoke(drop) ?? DropPickupFailureReason.None;
                if (validatorReason != DropPickupFailureReason.None)
                {
                    if (distSq < closestFailureDistSq)
                    {
                        closestFailureDrop = drop;
                        closestFailureDistSq = distSq;
                        closestFailureReason = validatorReason;
                    }

                    continue;
                }

                if (distSq < closestAvailableDistSq)
                {
                    closestAvailableDistSq = distSq;
                    closestAvailable = drop;
                }
            }

            if (closestAvailable != null && TryPickup(closestAvailable, playerId, currentTime))
            {
                return new DropPickupAttemptResult
                {
                    Drop = closestAvailable,
                    FailureReason = DropPickupFailureReason.None
                };
            }

            if (closestAvailable != null)
            {
                return new DropPickupAttemptResult
                {
                    Drop = null,
                    ContextDrop = closestAvailable,
                    FailureReason = DropPickupFailureReason.Unavailable,
                    RecentPickup = FindRecentPickup(closestAvailable.PoolId, currentTime)
                };
            }

            return new DropPickupAttemptResult
            {
                Drop = null,
                ContextDrop = closestFailureDrop,
                FailureReason = closestFailureReason
            };
        }
        #endregion

        #region Update
        /// <summary>
        /// Update all drops with frame-rate independent timing
        /// </summary>
        /// <param name="currentTime">Current tick count</param>
        /// <param name="deltaTime">Time since last frame in seconds</param>
        public void Update(int currentTime, float deltaTime)
        {
            _lastUpdateTick = currentTime;

            for (int i = _activeDrops.Count - 1; i >= 0; i--)
            {
                var drop = _activeDrops[i];
                DropState previousState = drop.State;
                drop.Update(currentTime, deltaTime);

                if (previousState == DropState.Spawning
                    && drop.State == DropState.Falling
                    && drop.IsPacketControlled
                    && drop.PacketEnterType == 1
                    && currentTime - _lastPacketEnterSoundTime >= PACKET_ENTER_SOUND_COOLDOWN)
                {
                    _lastPacketEnterSoundTime = currentTime;
                    _onPacketEnterSoundRequested?.Invoke(drop, currentTime);
                }

                if (ShouldRemoveClientFakeDrop(drop, currentTime))
                {
                    RemoveDrop(drop);
                    continue;
                }

                if (drop.State == DropState.Removed)
                {
                    if (drop.TriggerPacketExplodeEffectOnRemove
                        && currentTime - _lastPacketExplodeSoundTime >= PACKET_EXPLODE_SOUND_COOLDOWN)
                    {
                        _lastPacketExplodeSoundTime = currentTime;
                        _onPacketExploded?.Invoke(drop, currentTime);
                    }

                    if (drop.Alpha <= 0)
                        _onDropExpired?.Invoke(drop);
                    RemoveDrop(drop);
                }
            }
        }

        private static bool ShouldRemoveClientFakeDrop(DropItem drop, int currentTime)
        {
            if (drop == null || !drop.IsPacketControlled || drop.IsReal)
            {
                return false;
            }

            if (drop.State == DropState.Idle)
            {
                return true;
            }

            int createTime = drop.SpawnTime + Math.Max(0, drop.CreateDelayMs);
            return currentTime - createTime > 3000;
        }

        private void RemoveDrop(DropItem drop)
        {
            _activeDrops.Remove(drop);
            _dropById.Remove(drop.PoolId);
            _dropPool.Enqueue(drop);
        }
        #endregion

        #region Draw Helper
        /// <summary>
        /// Get all drops that should be rendered (with screen culling)
        /// </summary>
        public void GetRenderableDrops(
            List<DropItem> destination,
            int screenLeft,
            int screenRight,
            int screenTop,
            int screenBottom,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            bool elevatedOnly = false)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();

            for (int i = 0; i < _activeDrops.Count; i++)
            {
                DropItem drop = _activeDrops[i];
                if (drop.Alpha <= 0)
                {
                    continue;
                }

                if (drop.DrawOnElevatedLayer != elevatedOnly)
                {
                    continue;
                }

                int screenX = (int)drop.X - mapShiftX + centerX;
                int screenY = (int)drop.Y - mapShiftY + centerY;

                // Simple bounds check with padding for icons
                if (screenX >= screenLeft - 50 && screenX <= screenRight + 50 &&
                    screenY >= screenTop - 50 && screenY <= screenBottom + 50)
                {
                    destination.Add(drop);
                }
            }
        }
        #endregion

        #region Stats
        public DropPoolStats GetStats()
        {
            int idleCount = 0;
            int fallingCount = 0;
            int mesoTotal = 0;

            foreach (var drop in _activeDrops)
            {
                if (drop.State == DropState.Idle) idleCount++;
                else if (drop.State == DropState.Falling || drop.State == DropState.Bouncing) fallingCount++;
                if (drop.Type == DropType.Meso) mesoTotal += drop.MesoAmount;
            }

            return new DropPoolStats
            {
                TotalDrops = _activeDrops.Count,
                IdleDrops = idleCount,
                FallingDrops = fallingCount,
                TotalMesos = mesoTotal
            };
        }
        #endregion

        #region Pet Pickup System

        /// <summary>
        /// Attempt to pick up a drop by a pet.
        /// Pet loot system based on CDropPool::TryPickUpDropByPet from MapleStory client.
        /// </summary>
        /// <param name="petId">The pet's unique ID</param>
        /// <param name="petX">Pet's X position</param>
        /// <param name="petY">Pet's Y position</param>
        /// <param name="playerId">Owner player's ID</param>
        /// <param name="currentTime">Current game tick</param>
        /// <param name="petPickupRange">Override pickup range (default: PET_PICKUP_RANGE)</param>
        /// <returns>The picked up drop, or null if nothing picked up</returns>
        public DropItem TryPickUpDropByPet(int petId, float petX, float petY, int playerId, int currentTime, float petPickupRange = 0)
        {
            return TryPickUpDropByPetDetailed(petId, petX, petY, playerId, currentTime, petPickupRange).Drop;
        }

        public DropPickupAttemptResult TryPickUpDropByPetDetailed(int petId, float petX, float petY, int playerId, int currentTime, float petPickupRange = 0)
        {
            if (petPickupRange <= 0)
                petPickupRange = PET_PICKUP_RANGE;

            // Check cooldown
            if (_petLastPickupTime.TryGetValue(petId, out int lastPickup))
            {
                if (currentTime - lastPickup < PET_PICKUP_COOLDOWN)
                {
                    return new DropPickupAttemptResult
                    {
                        Drop = null,
                        FailureReason = DropPickupFailureReason.NoDropInRange
                    };
                }
            }

            float rangeSq = petPickupRange * petPickupRange;
            DropItem closestDrop = null;
            float closestDistSq = float.MaxValue;
            DropItem closestFailureDrop = null;
            float closestFailureDistSq = float.MaxValue;
            DropPickupFailureReason closestFailureReason = DropPickupFailureReason.NoDropInRange;

            foreach (var drop in _activeDrops)
            {
                if (drop.State != DropState.Idle || !drop.CanPickup)
                    continue;

                float dx = drop.X - petX;
                float dy = drop.Y - petY;
                float distSq = dx * dx + dy * dy;
                if (distSq > rangeSq)
                    continue;

                if (!CanPetPickup(drop, playerId, currentTime))
                {
                    if (distSq < closestFailureDistSq)
                    {
                        closestFailureDrop = drop;
                        closestFailureDistSq = distSq;
                        closestFailureReason = DropPickupFailureReason.OwnershipRestricted;
                    }

                    continue;
                }

                // Check exception list
                if (IsInExceptionList(drop))
                    continue;

                DropPickupFailureReason validatorReason =
                    _petPickupAvailabilityEvaluator?.Invoke(drop)
                    ?? _pickupAvailabilityEvaluator?.Invoke(drop)
                    ?? DropPickupFailureReason.None;
                if (validatorReason != DropPickupFailureReason.None)
                {
                    if (distSq < closestFailureDistSq)
                    {
                        closestFailureDrop = drop;
                        closestFailureDistSq = distSq;
                        closestFailureReason = validatorReason;
                    }

                    continue;
                }

                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closestDrop = drop;
                }
            }

            if (closestDrop != null)
            {
                // Clear chase target if this was it
                if (_petTargets.TryGetValue(petId, out var target) && target.DropId == closestDrop.PoolId)
                {
                    _petTargets.Remove(petId);
                }

                _petLastPickupTime[petId] = currentTime;
                CompletePickup(closestDrop, petId, pickedByPet: true, currentTime, DropPickupActorKind.Pet, notifyLocalPickup: true);

                return new DropPickupAttemptResult
                {
                    Drop = closestDrop,
                    ContextDrop = closestDrop,
                    FailureReason = DropPickupFailureReason.None
                };
            }

            ReportPickupFailure(closestFailureDrop, closestFailureReason, petId, true, currentTime);
            return new DropPickupAttemptResult
            {
                Drop = null,
                ContextDrop = closestFailureDrop,
                FailureReason = closestFailureReason
            };
        }

        private void ReportPickupFailure(DropItem drop, DropPickupFailureReason reason, int pickerId, bool pickedByPet, int currentTime)
        {
            if (reason == DropPickupFailureReason.None || reason == DropPickupFailureReason.NoDropInRange)
            {
                return;
            }

            if (drop != null)
            {
                if (drop.LastPickupFailureReason == reason
                    && currentTime - drop.LastPickupFailureTime < PICKUP_FAILURE_REPORT_COOLDOWN)
                {
                    return;
                }

                drop.LastPickupFailureReason = reason;
                drop.LastPickupFailureTime = currentTime;
            }

            _onPickupFailed?.Invoke(new DropPickupAttemptResult
            {
                Drop = null,
                ContextDrop = drop,
                FailureReason = reason,
                RecentPickup = reason == DropPickupFailureReason.Unavailable && drop != null
                    ? FindRecentPickup(drop.PoolId, currentTime)
                    : null
            }, pickerId, pickedByPet);
        }

        /// <summary>
        /// Update pet chasing behavior for drops.
        /// Based on CDropPool::UpdateChasingDropForPet from MapleStory client.
        /// </summary>
        /// <param name="petId">The pet's unique ID</param>
        /// <param name="petX">Current pet X position</param>
        /// <param name="petY">Current pet Y position</param>
        /// <param name="playerId">Owner player's ID</param>
        /// <param name="playerX">Owner player's X position</param>
        /// <param name="playerY">Owner player's Y position</param>
        /// <param name="currentTime">Current game tick</param>
        /// <param name="deltaTime">Time since last update</param>
        /// <returns>The target drop the pet should chase, or null if none</returns>
        public PetDropTarget UpdateChasingDropForPet(int petId, float petX, float petY, int playerId,
            float playerX, float playerY, int currentTime, float deltaTime)
        {
            // Check if pet already has a target
            if (_petTargets.TryGetValue(petId, out var existingTarget))
            {
                var targetDrop = GetDrop(existingTarget.DropId);

                // Validate target still exists and is pickupable
                if (targetDrop != null && targetDrop.State == DropState.Idle && targetDrop.CanPickup)
                {
                    // Update target position
                    existingTarget.TargetX = targetDrop.X;
                    existingTarget.TargetY = targetDrop.Y;

                    // Check if close enough to pickup
                    float dx = targetDrop.X - petX;
                    float dy = targetDrop.Y - petY;
                    float distSq = dx * dx + dy * dy;

                    if (distSq <= PET_PICKUP_RANGE * PET_PICKUP_RANGE)
                    {
                        existingTarget.IsChasing = false;
                    }
                    else
                    {
                        existingTarget.IsChasing = true;
                    }

                    return existingTarget;
                }
                else
                {
                    // Target no longer valid, clear it
                    _petTargets.Remove(petId);
                }
            }

            // Find a new target drop
            DropItem bestDrop = null;
            float bestDistSq = PET_LOOT_RANGE * PET_LOOT_RANGE;

            foreach (var drop in _activeDrops)
            {
                if (drop.State != DropState.Idle || !drop.CanPickup)
                    continue;

                if (!CanPetPickup(drop, playerId, currentTime))
                    continue;

                // Check exception list
                if (IsInExceptionList(drop))
                    continue;

                // Check if another pet is already targeting this drop
                bool alreadyTargeted = false;
                foreach (var otherTarget in _petTargets.Values)
                {
                    if (otherTarget.PetId != petId && otherTarget.DropId == drop.PoolId)
                    {
                        alreadyTargeted = true;
                        break;
                    }
                }
                if (alreadyTargeted)
                    continue;

                // Prefer drops closer to both pet and player
                float dxPet = drop.X - petX;
                float dyPet = drop.Y - petY;
                float distToPetSq = dxPet * dxPet + dyPet * dyPet;

                float dxPlayer = drop.X - playerX;
                float dyPlayer = drop.Y - playerY;
                float distToPlayerSq = dxPlayer * dxPlayer + dyPlayer * dyPlayer;

                // Weighted distance: prioritize drops near player
                float weightedDistSq = distToPetSq + distToPlayerSq * 0.5f;

                if (weightedDistSq < bestDistSq)
                {
                    bestDistSq = weightedDistSq;
                    bestDrop = drop;
                }
            }

            if (bestDrop != null)
            {
                var newTarget = new PetDropTarget
                {
                    PetId = petId,
                    DropId = bestDrop.PoolId,
                    TargetX = bestDrop.X,
                    TargetY = bestDrop.Y,
                    StartTime = currentTime,
                    IsChasing = true,
                    ChaseSpeed = PET_CHASE_SPEED
                };

                _petTargets[petId] = newTarget;
                _onPetStartChasing?.Invoke(petId, bestDrop);

                return newTarget;
            }

            return null;
        }

        /// <summary>
        /// Clear a pet's chase target
        /// </summary>
        public void ClearPetTarget(int petId)
        {
            _petTargets.Remove(petId);
        }

        /// <summary>
        /// Get a pet's current chase target
        /// </summary>
        public PetDropTarget GetPetTarget(int petId)
        {
            return _petTargets.TryGetValue(petId, out var target) ? target : null;
        }

        #endregion

        #region Mob Pickup System

        /// <summary>
        /// Register a mob as having pickup ability (like Thief mobs)
        /// </summary>
        public void RegisterMobWithPickupAbility(int mobId)
        {
            _mobsWithPickupAbility.Add(mobId);
        }

        /// <summary>
        /// Unregister a mob's pickup ability
        /// </summary>
        public void UnregisterMobWithPickupAbility(int mobId)
        {
            _mobsWithPickupAbility.Remove(mobId);
        }

        /// <summary>
        /// Attempt to pick up a drop by a mob.
        /// Based on CDropPool::TryPickUpDropByMob from MapleStory client.
        /// Some mobs in MapleStory can pick up dropped items (like Thief Crows).
        /// </summary>
        /// <param name="mobId">The mob's unique ID</param>
        /// <param name="mobX">Mob's X position</param>
        /// <param name="mobY">Mob's Y position</param>
        /// <param name="currentTime">Current game tick</param>
        /// <returns>The picked up drop, or null if nothing picked up</returns>
        public DropItem TryPickUpDropByMob(int mobId, float mobX, float mobY, int currentTime)
        {
            // Check if this mob has pickup ability
            if (!_mobsWithPickupAbility.Contains(mobId))
                return null;

            // Find closest drop within range
            DropItem closestDrop = null;
            float closestDistSq = MOB_PICKUP_RANGE * MOB_PICKUP_RANGE;

            foreach (var drop in _activeDrops)
            {
                if (drop.State != DropState.Idle || !drop.CanPickup)
                    continue;

                if (!CanMobPickup(drop))
                    continue;

                float dx = drop.X - mobX;
                float dy = drop.Y - mobY;
                float distSq = dx * dx + dy * dy;

                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closestDrop = drop;
                }
            }

            if (closestDrop != null)
            {
                return ResolveRemotePickup(closestDrop, mobId, currentTime, DropPickupActorKind.Mob)
                    ? closestDrop
                    : null;
            }

            return null;
        }

        #endregion

        #region Meso Explosion (Shadower Skill)

        /// <summary>
        /// Get all meso drops within a rectangular area for Meso Explosion skill.
        /// Based on CDropPool::GetExplosiveDropInRect from MapleStory client.
        /// </summary>
        /// <param name="centerX">Center X of explosion</param>
        /// <param name="centerY">Center Y of explosion</param>
        /// <param name="width">Width of explosion area</param>
        /// <param name="height">Height of explosion area</param>
        /// <param name="playerId">Player using the skill</param>
        /// <param name="currentTime">Current game tick</param>
        /// <param name="maxCount">Maximum mesos to explode</param>
        /// <returns>List of meso drops that will explode</returns>
        public List<DropItem> GetExplosiveDropInRect(float centerX, float centerY, float width, float height,
            int playerId, int currentTime, int maxCount = 0, bool enforceOwnership = true)
        {
            if (maxCount <= 0)
                maxCount = MAX_MESO_EXPLOSION_DROPS;

            var explosiveDrops = new List<DropItem>();

            float halfWidth = width / 2f;
            float halfHeight = height / 2f;
            float left = centerX - halfWidth;
            float right = centerX + halfWidth;
            float top = centerY - halfHeight;
            float bottom = centerY + halfHeight;

            foreach (var drop in _activeDrops)
            {
                if (drop.State != DropState.Idle || !drop.CanPickup)
                    continue;

                // Only meso drops can explode
                if (drop.Type != DropType.Meso)
                    continue;

                if (enforceOwnership
                    && drop.OwnerId > 0
                    && drop.OwnerId != playerId
                    && currentTime < drop.OwnerExpireTime)
                    continue;

                // Check if in rect
                if (drop.X >= left && drop.X <= right && drop.Y >= top && drop.Y <= bottom)
                {
                    explosiveDrops.Add(drop);

                    if (explosiveDrops.Count >= maxCount)
                        break;
                }
            }

            return explosiveDrops;
        }

        /// <summary>
        /// Get meso drops within circular range for Meso Explosion.
        /// Convenience overload using radius instead of rectangle.
        /// </summary>
        public List<DropItem> GetExplosiveDropInRange(float centerX, float centerY, float radius,
            int playerId, int currentTime, int maxCount = 0, bool enforceOwnership = true)
        {
            if (maxCount <= 0)
                maxCount = MAX_MESO_EXPLOSION_DROPS;

            var explosiveDrops = new List<DropItem>();
            float radiusSq = radius * radius;

            foreach (var drop in _activeDrops)
            {
                if (drop.State != DropState.Idle || !drop.CanPickup)
                    continue;

                if (drop.Type != DropType.Meso)
                    continue;

                if (enforceOwnership
                    && drop.OwnerId > 0
                    && drop.OwnerId != playerId
                    && currentTime < drop.OwnerExpireTime)
                    continue;

                float dx = drop.X - centerX;
                float dy = drop.Y - centerY;
                if (dx * dx + dy * dy <= radiusSq)
                {
                    explosiveDrops.Add(drop);

                    if (explosiveDrops.Count >= maxCount)
                        break;
                }
            }

            return explosiveDrops;
        }

        /// <summary>
        /// Consume meso drops for Meso Explosion, returning total meso amount.
        /// </summary>
        public int ConsumeMesosForExplosion(List<DropItem> mesosToExplode, int currentTime)
        {
            int totalMesos = 0;

            foreach (var drop in mesosToExplode)
            {
                if (drop.State != DropState.Idle)
                    continue;

                totalMesos += drop.MesoAmount;
                drop.State = DropState.PickingUp;
                drop.LastStateChangeTime = currentTime;
            }

            return totalMesos;
        }

        #endregion

        #region Booby Trap System

        /// <summary>
        /// Register a drop as a booby trap.
        /// When picked up, triggers trap effect on the player who picks it up.
        /// </summary>
        public void RegisterBoobyTrapDrop(int dropId, int trapOwnerId)
        {
            _boobyTrapDrops[dropId] = trapOwnerId;
        }

        /// <summary>
        /// Check if a drop is a booby trap and handle pickup.
        /// Based on CDropPool::BoobyTrapCheckPickupItem from MapleStory client.
        /// </summary>
        /// <param name="drop">The drop being picked up</param>
        /// <param name="pickerId">Player attempting pickup</param>
        /// <returns>True if drop was a booby trap (caller should apply trap effect)</returns>
        public bool BoobyTrapCheckPickupItem(DropItem drop, int pickerId)
        {
            if (drop == null)
                return false;

            if (_boobyTrapDrops.TryGetValue(drop.PoolId, out int trapOwnerId))
            {
                // Don't trigger trap on the owner
                if (pickerId != trapOwnerId)
                {
                    _onBoobyTrapTriggered?.Invoke(drop, trapOwnerId);
                    _boobyTrapDrops.Remove(drop.PoolId);
                    return true;
                }

                // Owner picked up their own trap - just remove it
                _boobyTrapDrops.Remove(drop.PoolId);
            }

            return false;
        }

        /// <summary>
        /// Check if a drop is a booby trap
        /// </summary>
        public bool IsBoobyTrapDrop(int dropId)
        {
            return _boobyTrapDrops.ContainsKey(dropId);
        }

        /// <summary>
        /// Clear booby trap registration when drop expires
        /// </summary>
        public void ClearBoobyTrap(int dropId)
        {
            _boobyTrapDrops.Remove(dropId);
        }

        #endregion

        #region Recent Pickup History

        /// <summary>
        /// Record a recently picked up item for history tracking.
        /// Based on CDropPool::RecordRecentPickupItem from MapleStory client.
        /// </summary>
        public RecentPickupRecord RecordRecentPickupItem(
            DropItem drop,
            int pickerId,
            bool pickedByPet,
            int currentTime,
            DropPickupActorKind actorKind = DropPickupActorKind.Other,
            string actorName = null)
        {
            if (drop == null)
                return null;

            PruneRecentPickupHistory(currentTime);

            // Enforce max size
            while (_recentPickups.Count >= MAX_RECENT_PICKUPS)
            {
                _recentPickups.Dequeue();
            }

            // Add new record
            var record = new RecentPickupRecord
            {
                DropId = drop.PoolId,
                Type = drop.Type,
                ItemId = drop.ItemId,
                MesoAmount = drop.MesoAmount,
                Quantity = drop.Quantity,
                PickupTime = currentTime,
                PickerId = pickerId,
                PickedByPet = pickedByPet,
                ActorKind = actorKind,
                ActorName = actorName
            };

            _recentPickups.Enqueue(record);
            return record;
        }

        public RecentPickupRecord FindRecentPickup(int dropId, int currentTime)
        {
            PruneRecentPickupHistory(currentTime);

            RecentPickupRecord latestRecord = null;
            foreach (RecentPickupRecord record in _recentPickups)
            {
                if (record.DropId == dropId)
                {
                    latestRecord = record;
                }
            }

            return latestRecord;
        }

        /// <summary>
        /// Get recent pickup history
        /// </summary>
        public IReadOnlyCollection<RecentPickupRecord> GetRecentPickups()
        {
            return _recentPickups;
        }

        /// <summary>
        /// Get recent pickups filtered by type
        /// </summary>
        public IEnumerable<RecentPickupRecord> GetRecentPickups(DropType type)
        {
            return _recentPickups.Where(r => r.Type == type);
        }

        /// <summary>
        /// Get total mesos picked up recently
        /// </summary>
        public int GetRecentMesoTotal()
        {
            return _recentPickups.Where(r => r.Type == DropType.Meso).Sum(r => r.MesoAmount);
        }

        /// <summary>
        /// Clear all pickup history
        /// </summary>
        public void ClearRecentPickups()
        {
            _recentPickups.Clear();
        }

        private void PruneRecentPickupHistory(int currentTime)
        {
            while (_recentPickups.Count > 0)
            {
                RecentPickupRecord oldest = _recentPickups.Peek();
                if (currentTime - oldest.PickupTime > RECENT_PICKUP_LIFETIME)
                {
                    _recentPickups.Dequeue();
                    continue;
                }

                break;
            }
        }

        private bool CompletePickup(
            DropItem drop,
            int pickerId,
            bool pickedByPet,
            int currentTime,
            DropPickupActorKind actorKind,
            bool notifyLocalPickup,
            string actorName = null,
            Vector2? pickupTargetPosition = null,
            int pickupDurationMs = DropItem.PACKET_ABSORB_DURATION,
            Func<Vector2?> pickupTargetPositionResolver = null,
            Vector2? pickupStartPositionOverride = null,
            bool bypassStateValidation = false)
        {
            if (drop == null || drop.State == DropState.Removed || drop.State == DropState.Expired)
            {
                return false;
            }

            if (!bypassStateValidation && (drop.State != DropState.Idle || !drop.CanPickup))
            {
                return false;
            }

            actorName = string.IsNullOrWhiteSpace(actorName)
                ? _pickupActorNameResolver?.Invoke(actorKind, pickerId, pickedByPet)
                : actorName;
            drop.StartPickup(
                currentTime,
                pickupTargetPosition,
                pickupDurationMs,
                pickupTargetPositionResolver,
                pickupStartPositionOverride);
            RecordRecentPickupItem(drop, pickerId, pickedByPet, currentTime, actorKind, actorName);
            _onDropPickedUp?.Invoke(drop);

            if (notifyLocalPickup)
            {
                _onPickupResolved?.Invoke(drop, pickerId, pickedByPet);
                if (pickedByPet)
                {
                    _onPetPickedUp?.Invoke(drop, pickerId);
                }

                return true;
            }

            switch (actorKind)
            {
                case DropPickupActorKind.Player:
                    _onRemotePlayerPickedUp?.Invoke(drop, pickerId, actorName);
                    break;
                case DropPickupActorKind.Pet:
                    _onRemotePetPickedUp?.Invoke(drop, pickerId, actorName);
                    break;
                case DropPickupActorKind.Mob:
                    _onMobPickedUp?.Invoke(drop, pickerId);
                    break;
                case DropPickupActorKind.Other:
                    _onRemoteOtherPickedUp?.Invoke(drop, pickerId, actorName);
                    break;
            }

            return true;
        }

        private static Vector2 ResolvePacketLeaveOrigin(DropItem drop)
        {
            if (drop == null)
            {
                return Vector2.Zero;
            }

            return drop.IsPacketControlled
                ? new Vector2(drop.TargetX, drop.TargetY)
                : new Vector2(drop.X, drop.Y);
        }

        public bool ApplyPacketEnter(RemoteDropEnterPacket packet, int currentTime)
        {
            if (packet.DropId <= 0)
            {
                return false;
            }

            if (_dropById.TryGetValue(packet.DropId, out DropItem existingDrop))
            {
                if ((packet.EnterType == 1 || packet.EnterType == 2) && !existingDrop.IsReal)
                {
                    existingDrop.IsReal = true;
                    existingDrop.PacketEnterType = packet.EnterType;
                }

                return true;
            }

            string itemId = packet.IsMoney ? null : packet.Info.ToString("D8");
            DropItem drop = GetOrCreateDrop();
            InitializeDrop(drop, packet.IsMoney ? DropType.Meso : DropType.Item, itemId, packet.TargetX, packet.TargetY, currentTime, packet.OwnerId);

            drop.PoolId = packet.DropId;
            _nextDropId = Math.Max(_nextDropId, packet.DropId + 1);
            drop.OwnershipType = packet.OwnershipType;
            drop.SourceId = packet.SourceId;
            drop.IsReal = packet.EnterType == 1 || packet.EnterType == 2;
            drop.AllowPetPickup = packet.AllowPetPickup;
            drop.IsPacketControlled = true;
            drop.PacketEnterType = packet.EnterType;
            drop.CreateDelayMs = packet.DelayMs;
            drop.OwnerExpireTime = packet.OwnerId > 0 ? currentTime + OWNER_PRIORITY_DURATION : 0;
            drop.ExpireTime = ResolvePacketExpireTime(
                currentTime,
                packet.IsMoney,
                packet.ExpireRaw,
                _packetExpireTimeUtcResolver?.Invoke() ?? DateTime.UtcNow);
            drop.HoverAmplitude = packet.IsMoney ? 3f : 2f;
            drop.HoverFrequency = packet.EnterType == 4 ? 0.6f : 1f;
            drop.HoverPhase = drop.PoolId * 0.31f;

            Vector2 startPosition = ResolvePacketDropStartPosition(packet);
            drop.SpawnX = startPosition.X;
            drop.SpawnY = startPosition.Y;
            drop.TargetX = packet.TargetX;
            drop.X = startPosition.X;
            drop.Y = startPosition.Y;
            drop.GroundY = _getGroundY?.Invoke(packet.TargetX, packet.TargetY) ?? packet.TargetY;
            drop.TargetY = drop.GroundY;
            drop.VelocityX = 0f;
            drop.VelocityY = packet.EnterType == 4 ? DROP_INITIAL_VELOCITY_Y * 0.45f : DROP_INITIAL_VELOCITY_Y;
            drop.MotionElapsedMs = 0;
            drop.MotionLastUpdateTime = currentTime;
            drop.ParabolicDurationMs = CalculateParabolicMotionDuration(
                drop.SpawnY,
                drop.TargetY,
                drop.OwnershipType == DropOwnershipType.Explosive,
                packet.EnterType == 4);

            if (packet.IsMoney)
            {
                drop.MesoAmount = Math.Max(0, packet.Info);
                drop.Icon = _mesoIcon;
                ApplyMesoVisuals(drop, packetControlled: true);
            }
            else if (!TryApplyPacketItemVisuals(drop, itemId)
                     && _itemIcons.TryGetValue(itemId, out IDXObject icon))
            {
                drop.Icon = icon;
            }

            drop.DrawOnElevatedLayer = packet.ElevateLayer;
            drop.PacketEnterAlphaRampDurationMs = packet.EnterType == 3
                ? DropItem.PACKET_ENTER_TYPE3_ALPHA_RAMP_DURATION
                : 0;
            drop.PacketEnterAlphaRampStartTime = currentTime + Math.Max(0, (int)packet.DelayMs);

            if (packet.EnterType == 2)
            {
                SnapDropToPacketIdle(drop, currentTime);
            }
            else
            {
                drop.State = DropState.Spawning;
                if (packet.EnterType == 3)
                {
                    drop.Alpha = 0f;
                }
            }

            _activeDrops.Add(drop);
            _dropById[drop.PoolId] = drop;
            _onDropSpawned?.Invoke(drop);
            return true;
        }

        public bool ApplyPacketLeave(
            RemoteDropLeavePacket packet,
            int currentTime,
            int localCharacterId,
            Func<PacketDropLeaveReason, RemoteDropLeavePacket, string> actorNameResolver = null,
            Func<PacketDropLeaveReason, RemoteDropLeavePacket, Vector2?> actorPositionResolver = null,
            Func<RemoteDropLeavePacket, int> petPickupActorIdResolver = null,
            Action<RemoteDropLeavePacket> beforeLocalPetPickup = null)
        {
            if (!_dropById.TryGetValue(packet.DropId, out DropItem drop))
            {
                return false;
            }

            switch (packet.Reason)
            {
                case PacketDropLeaveReason.Remove:
                    if (drop.IsPacketControlled)
                    {
                        drop.SnapToTargetPosition();
                    }

                    drop.ScheduleRemoval(currentTime, 0, fadeOut: true);
                    return true;

                case PacketDropLeaveReason.Explode:
                    drop.TriggerPacketExplodeEffectOnRemove = true;
                    drop.ScheduleRemoval(currentTime, packet.DelayMs, fadeOut: false);
                    return true;

                case PacketDropLeaveReason.PlayerPickup:
                    if (packet.ActorId == localCharacterId)
                    {
                        return CompletePickup(
                            drop,
                            packet.ActorId,
                            pickedByPet: false,
                            currentTime,
                            DropPickupActorKind.Player,
                            notifyLocalPickup: true,
                            pickupTargetPosition: actorPositionResolver?.Invoke(packet.Reason, packet),
                            pickupStartPositionOverride: ResolvePacketLeaveOrigin(drop),
                            bypassStateValidation: drop.IsPacketControlled,
                            pickupTargetPositionResolver: actorPositionResolver == null
                                ? null
                                : () => actorPositionResolver(packet.Reason, packet));
                    }

                    return ResolveRemotePickup(
                        drop,
                        packet.ActorId,
                        currentTime,
                        DropPickupActorKind.Player,
                        actorNameResolver?.Invoke(packet.Reason, packet),
                        pickupTargetPosition: actorPositionResolver?.Invoke(packet.Reason, packet),
                        pickupStartPositionOverride: ResolvePacketLeaveOrigin(drop),
                        bypassStateValidation: drop.IsPacketControlled,
                        pickupTargetPositionResolver: actorPositionResolver == null
                            ? null
                            : () => actorPositionResolver(packet.Reason, packet));

                case PacketDropLeaveReason.MobPickup:
                    return ResolveRemotePickup(
                        drop,
                        packet.ActorId,
                        currentTime,
                        DropPickupActorKind.Mob,
                        actorNameResolver?.Invoke(packet.Reason, packet),
                        pickupTargetPosition: actorPositionResolver?.Invoke(packet.Reason, packet),
                        pickupStartPositionOverride: ResolvePacketLeaveOrigin(drop),
                        bypassStateValidation: drop.IsPacketControlled,
                        pickupTargetPositionResolver: actorPositionResolver == null
                            ? null
                            : () => actorPositionResolver(packet.Reason, packet));

                case PacketDropLeaveReason.PetPickup:
                    if (packet.ActorId == localCharacterId)
                    {
                        int localPetId = petPickupActorIdResolver?.Invoke(packet) ?? packet.ActorId;
                        beforeLocalPetPickup?.Invoke(packet);
                        return CompletePickup(
                            drop,
                            localPetId,
                            pickedByPet: true,
                            currentTime,
                            DropPickupActorKind.Pet,
                            notifyLocalPickup: true,
                            pickupTargetPosition: actorPositionResolver?.Invoke(packet.Reason, packet),
                            pickupStartPositionOverride: ResolvePacketLeaveOrigin(drop),
                            bypassStateValidation: drop.IsPacketControlled,
                            pickupTargetPositionResolver: actorPositionResolver == null
                                ? null
                                : () => actorPositionResolver(packet.Reason, packet));
                    }

                    int remotePetActorId = petPickupActorIdResolver?.Invoke(packet) ?? packet.ActorId;
                    return ResolveRemotePickup(
                        drop,
                        remotePetActorId,
                        currentTime,
                        DropPickupActorKind.Pet,
                        actorNameResolver?.Invoke(packet.Reason, packet),
                        pickedByPet: true,
                        pickupTargetPosition: actorPositionResolver?.Invoke(packet.Reason, packet),
                        pickupStartPositionOverride: ResolvePacketLeaveOrigin(drop),
                        bypassStateValidation: drop.IsPacketControlled,
                        pickupTargetPositionResolver: actorPositionResolver == null
                            ? null
                            : () => actorPositionResolver(packet.Reason, packet));

                default:
                    return false;
            }
        }

        private Vector2 ResolvePacketDropStartPosition(RemoteDropEnterPacket packet)
        {
            if (packet.SourceId > 0 && _sourcePositionResolver?.Invoke(packet.SourceId) is Vector2 sourcePosition)
            {
                return sourcePosition;
            }

            if (packet.HasStartPosition)
            {
                return new Vector2(packet.StartX, packet.StartY);
            }

            return new Vector2(packet.TargetX, packet.TargetY - 40f);
        }

        private static bool IsOwnershipWindowActive(DropItem drop, int currentTime)
        {
            if (drop.OwnerId <= 0 || currentTime >= drop.OwnerExpireTime)
            {
                return false;
            }

            return !drop.IsPacketControlled || drop.SourceId != 0;
        }

        internal static int ResolvePacketExpireTime(int currentTime, bool isMoney, long expireRaw)
        {
            return ResolvePacketExpireTime(currentTime, isMoney, expireRaw, DateTime.UtcNow);
        }

        internal static int ResolvePacketExpireTime(int currentTime, bool isMoney, long expireRaw, DateTime referenceUtc)
        {
            if (isMoney)
            {
                return currentTime + DEFAULT_DROP_LIFETIME;
            }

            if (expireRaw <= 0 || expireRaw == long.MaxValue)
            {
                return currentTime + DEFAULT_DROP_LIFETIME;
            }

            try
            {
                DateTime expireUtc = DateTime.FromFileTimeUtc(expireRaw);
                if (referenceUtc.Kind == DateTimeKind.Local)
                {
                    referenceUtc = referenceUtc.ToUniversalTime();
                }
                else if (referenceUtc.Kind == DateTimeKind.Unspecified)
                {
                    referenceUtc = DateTime.SpecifyKind(referenceUtc, DateTimeKind.Utc);
                }

                double remainingMs = (expireUtc - referenceUtc).TotalMilliseconds;
                if (remainingMs <= 0)
                {
                    return currentTime;
                }

                if (remainingMs >= int.MaxValue - currentTime)
                {
                    return int.MaxValue;
                }

                return currentTime + (int)Math.Ceiling(remainingMs);
            }
            catch (ArgumentOutOfRangeException)
            {
                return currentTime + DEFAULT_DROP_LIFETIME;
            }
        }

        private bool IsPlayerOwnershipBlocked(DropItem drop, int actorId, int currentTime)
        {
            if (!IsOwnershipWindowActive(drop, currentTime))
            {
                return false;
            }

            return drop.OwnershipType switch
            {
                DropOwnershipType.Character => drop.OwnerId != actorId,
                DropOwnershipType.Party => !AreActorsPartyLinked(drop.OwnerId, actorId),
                _ => false
            };
        }

        private bool CanPlayerPickup(DropItem drop, int actorId, int currentTime)
        {
            return drop != null
                && drop.IsReal
                && !IsPlayerOwnershipBlocked(drop, actorId, currentTime);
        }

        private bool CanPetPickup(DropItem drop, int ownerId, int currentTime)
        {
            return drop != null
                && drop.IsReal
                && drop.AllowPetPickup
                && !IsPlayerOwnershipBlocked(drop, ownerId, currentTime);
        }

        private static bool CanMobPickup(DropItem drop)
        {
            return drop != null && drop.IsReal;
        }

        private bool AreActorsPartyLinked(int ownerId, int actorId)
        {
            if (ownerId <= 0 || actorId <= 0)
            {
                return false;
            }

            if (ownerId == actorId)
            {
                return true;
            }

            return _partyPickupMembershipEvaluator?.Invoke(ownerId, actorId) == true;
        }

        private static int CalculateParabolicMotionDuration(float startY, float targetY, bool explosiveOwnership, bool elongatedEnter)
        {
            float launchVelocity = explosiveOwnership ? 720f : 400f;
            float verticalDistance = Math.Max(0f, targetY - startY);
            float discriminant = (launchVelocity * launchVelocity) + (1600f * verticalDistance);
            float durationSeconds = (launchVelocity + MathF.Sqrt(discriminant)) / 800f;
            int durationMs = Math.Max(500, (int)MathF.Ceiling(durationSeconds * 1000f));
            return elongatedEnter ? durationMs * 2 : durationMs;
        }

        private static void SnapDropToPacketIdle(DropItem drop, int currentTime)
        {
            drop.State = DropState.Idle;
            drop.X = drop.TargetX;
            drop.Y = drop.TargetY;
            drop.VelocityX = 0f;
            drop.VelocityY = 0f;
            drop.LastStateChangeTime = currentTime;
            drop.MotionElapsedMs = 0;
            drop.MotionLastUpdateTime = currentTime;
        }

        private void ApplyMesoVisuals(DropItem drop, bool packetControlled)
        {
            if (drop == null || drop.Type != DropType.Meso)
            {
                return;
            }

            int iconType = GetMoneyIconTypeForAmount(drop.MesoAmount);
            List<IDXObject> frames = _mesoAnimationFrames[iconType];
            if (frames != null && frames.Count > 0)
            {
                drop.AnimFrames = frames;
                drop.Icon = frames[0];
            }

            drop.MesoAnimationIconType = iconType;
            drop.UseLayeredMesoAnimation = packetControlled && frames != null && frames.Count > 1;
            drop.MesoAnimationLayerCount = drop.UseLayeredMesoAnimation
                ? PacketOwnedMesoAnimationPresentation.ResolveLayerCount(iconType, frames.Count)
                : 0;
        }

        private bool TryApplyPacketItemVisuals(DropItem drop, string itemId)
        {
            if (drop == null || string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            if (!_packetItemVisualFrames.TryGetValue(itemId, out List<IDXObject> frames))
            {
                frames = _packetItemVisualResolver?.Invoke(itemId)?
                    .Where(frame => frame != null)
                    .ToList();
                _packetItemVisualFrames[itemId] = frames;
            }

            if (frames == null || frames.Count == 0)
            {
                return false;
            }

            drop.AnimFrames = frames;
            drop.Icon = frames[0];
            return true;
        }

        #endregion

        #region Exception List (Drop Filtering)

        /// <summary>
        /// Check if a drop is in the exception list.
        /// Based on CDropPool::IsInExceptionList from MapleStory client.
        /// Used for pet auto-loot filtering.
        /// </summary>
        /// <param name="drop">The drop to check</param>
        /// <returns>True if drop should be filtered out</returns>
        public bool IsInExceptionList(DropItem drop)
        {
            if (drop == null || _exceptionList.Count == 0)
                return false;

            bool matchFound = false;

            foreach (var entry in _exceptionList)
            {
                bool matches = true;

                // Check item ID
                if (!string.IsNullOrEmpty(entry.ItemId))
                {
                    if (drop.ItemId != entry.ItemId)
                        matches = false;
                }

                // Check drop type
                if (entry.Type.HasValue)
                {
                    if (drop.Type != entry.Type.Value)
                        matches = false;
                }

                // Check meso range
                if (entry.MinMesoAmount.HasValue || entry.MaxMesoAmount.HasValue)
                {
                    if (drop.Type != DropType.Meso)
                    {
                        matches = false;
                    }
                    else
                    {
                        if (entry.MinMesoAmount.HasValue && drop.MesoAmount < entry.MinMesoAmount.Value)
                            matches = false;
                        if (entry.MaxMesoAmount.HasValue && drop.MesoAmount > entry.MaxMesoAmount.Value)
                            matches = false;
                    }
                }

                if (matches)
                {
                    matchFound = true;

                    // In blocklist mode: if matched and BlockPickup is true, filter it out
                    // In whitelist mode: if matched, allow it
                    if (!_useWhitelist)
                    {
                        return entry.BlockPickup;
                    }
                    else
                    {
                        // Whitelist mode: found match means allowed
                        return false;
                    }
                }
            }

            // In whitelist mode: no match means filter out
            if (_useWhitelist && !matchFound)
                return true;

            return false;
        }

        /// <summary>
        /// Add an exception entry to the filter list
        /// </summary>
        public void AddExceptionEntry(DropExceptionEntry entry)
        {
            if (entry != null)
                _exceptionList.Add(entry);
        }

        /// <summary>
        /// Add item to blocklist (won't be auto-picked by pets)
        /// </summary>
        public void AddToBlocklist(string itemId)
        {
            _exceptionList.Add(new DropExceptionEntry
            {
                ItemId = itemId,
                BlockPickup = true
            });
        }

        /// <summary>
        /// Add item type to blocklist
        /// </summary>
        public void AddTypeToBlocklist(DropType type)
        {
            _exceptionList.Add(new DropExceptionEntry
            {
                Type = type,
                BlockPickup = true
            });
        }

        /// <summary>
        /// Block mesos under a certain amount
        /// </summary>
        public void BlockMesosUnder(int amount)
        {
            _exceptionList.Add(new DropExceptionEntry
            {
                Type = DropType.Meso,
                MaxMesoAmount = amount - 1,
                BlockPickup = true
            });
        }

        /// <summary>
        /// Remove an item from the exception list
        /// </summary>
        public void RemoveFromExceptionList(string itemId)
        {
            _exceptionList.RemoveAll(e => e.ItemId == itemId);
        }

        /// <summary>
        /// Clear all exception entries
        /// </summary>
        public void ClearExceptionList()
        {
            _exceptionList.Clear();
        }

        /// <summary>
        /// Set exception list mode (blocklist or whitelist)
        /// </summary>
        /// <param name="useWhitelist">True for whitelist mode, false for blocklist mode</param>
        public void SetExceptionListMode(bool useWhitelist)
        {
            _useWhitelist = useWhitelist;
        }

        /// <summary>
        /// Get current exception list mode
        /// </summary>
        public bool IsWhitelistMode => _useWhitelist;

        /// <summary>
        /// Get all exception entries
        /// </summary>
        public IReadOnlyList<DropExceptionEntry> GetExceptionList()
        {
            return _exceptionList;
        }

        #endregion
    }

    /// <summary>
    /// Statistics about the drop pool
    /// </summary>
    public struct DropPoolStats
    {
        public int TotalDrops;
        public int IdleDrops;
        public int FallingDrops;
        public int TotalMesos;
    }
}
