using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Physics;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Entities
{
    /// <summary>
    /// Movement direction for mobs
    /// </summary>
    public enum MobMoveDirection
    {
        Left,
        Right,
        None
    }

    /// <summary>
    /// Movement type for mobs (based on MapleNecrocer Mob.cs)
    /// </summary>
    public enum MobMoveType
    {
        Stand,  // Stationary mobs with no horizontal movement
        Move,   // Walking along footholds with pathfinding
        Jump,   // Jumping between platforms
        Fly     // Floating without foothold constraints
    }

    /// <summary>
    /// Jump state for mobs (based on MapleNecrocer)
    /// </summary>
    public enum MobJumpState
    {
        None,       // Not jumping, on ground
        Jumping,    // Ascending
        Falling     // Descending
    }

    /// <summary>
    /// Animation action for mobs
    /// </summary>
    public enum MobAction
    {
        Stand,
        Move,
        Jump,
        Fly,
        Hit1,
        Die1,
        Attack1
    }

    /// <summary>
    /// Stores movement state and physics for a mob in the MapSimulator.
    /// Based on MapleNecrocer's Mob.cs implementation.
    ///
    /// <para><b>═══════════════════════════════════════════════════════════════</b></para>
    /// <para><b>MOVEMENT SYSTEM OVERVIEW</b></para>
    /// <para><b>═══════════════════════════════════════════════════════════════</b></para>
    ///
    /// <para><b>1. Movement Types:</b></para>
    /// <list type="bullet">
    ///   <item><b>Stand</b>: Stationary mob, no movement updates</item>
    ///   <item><b>Move</b>: Ground-based walking along footholds (most common)</item>
    ///   <item><b>Jump</b>: Ground-based with periodic jumps using gravity physics</item>
    ///   <item><b>Fly</b>: Airborne floating with vertical bobbing (sine wave)</item>
    /// </list>
    ///
    /// <para><b>═══════════════════════════════════════════════════════════════</b></para>
    /// <para><b>BOUNDARY CHECKING SYSTEM</b></para>
    /// <para><b>═══════════════════════════════════════════════════════════════</b></para>
    ///
    /// <para><b>2. Boundary Hierarchy (from highest to lowest priority):</b></para>
    /// <code>
    ///   ┌─────────────────────────────────────────────────────────────┐
    ///   │                     MAP BOUNDS (VR)                         │
    ///   │   MapLeft, MapRight, MapTop, MapBottom                      │
    ///   │   - Absolute limits from map's Viewing Range                │
    ///   │   - Mobs cannot exceed these coordinates                    │
    ///   │                                                             │
    ///   │   ┌─────────────────────────────────────────────────────┐   │
    ///   │   │              RX BOUNDS (Spawn Range)                │   │
    ///   │   │   RX0 (left), RX1 (right)                           │   │
    ///   │   │   - Horizontal patrol range from mob spawn data     │   │
    ///   │   │   - Calculated: RX0 = spawnX - rx0Shift             │   │
    ///   │   │                 RX1 = spawnX + rx1Shift             │   │
    ///   │   │                                                     │   │
    ///   │   │   ┌─────────────────────────────────────────────┐   │   │
    ///   │   │   │        PLATFORM BOUNDS (Foothold)           │   │   │
    ///   │   │   │   PlatformLeft, PlatformRight               │   │   │
    ///   │   │   │   - Calculated by traversing connected      │   │   │
    ///   │   │   │     footholds at similar Y level            │   │   │
    ///   │   │   │   - Used for walking mobs to detect edges   │   │   │
    ///   │   │   └─────────────────────────────────────────────┘   │   │
    ///   │   └─────────────────────────────────────────────────────┘   │
    ///   └─────────────────────────────────────────────────────────────┘
    /// </code>
    ///
    /// <para><b>3. Effective Boundary Calculation:</b></para>
    /// <code>
    ///   effectiveLeft  = max(RX0, MapLeft + margin)
    ///   effectiveRight = min(RX1, MapRight - margin)
    ///   margin = 30-50 pixels (prevents edge clipping)
    /// </code>
    ///
    /// <para><b>4. Boundary Check Behavior by Movement Type:</b></para>
    /// <list type="bullet">
    ///   <item><b>Flying:</b> Horizontal bounce at effectiveLeft/effectiveRight,
    ///         vertical bobbing around SrcY (spawn Y)</item>
    ///   <item><b>Walking:</b> Turn around at RX bounds, platform edges, or walls</item>
    ///   <item><b>Jumping:</b> Same as walking + jump physics with gravity,
    ///         reset to spawn if fall off map</item>
    /// </list>
    ///
    /// <para><b>═══════════════════════════════════════════════════════════════</b></para>
    /// <para><b>FOOTHOLD NAVIGATION SYSTEM</b></para>
    /// <para><b>═══════════════════════════════════════════════════════════════</b></para>
    ///
    /// <para><b>5. Foothold Types:</b></para>
    /// <list type="bullet">
    ///   <item><b>Normal foothold:</b> Walkable platform segment (not vertical)</item>
    ///   <item><b>Wall (IsWall=true):</b> Vertical segment blocking horizontal movement</item>
    /// </list>
    ///
    /// <para><b>6. Foothold Search Methods:</b></para>
    /// <list type="bullet">
    ///   <item><b>FindBelow(x, y):</b> Find foothold directly below position (for landing)</item>
    ///   <item><b>FindWallL(x, y):</b> Find wall to the left (collision check)</item>
    ///   <item><b>FindWallR(x, y):</b> Find wall to the right (collision check)</item>
    ///   <item><b>FindNextConnectedFoothold(dir):</b> Find connected foothold via anchor links</item>
    /// </list>
    ///
    /// <para><b>7. Y Position Calculation on Slopes:</b></para>
    /// <code>
    ///   // Linear interpolation between foothold endpoints
    ///   t = (x - x1) / (x2 - x1)    // 0.0 to 1.0 along foothold
    ///   y = y1 + t * (y2 - y1)       // Interpolated Y position
    /// </code>
    ///
    /// <para><b>8. Slope Movement (256-unit angle system):</b></para>
    /// <code>
    ///   direction = GetAngle256(x1, y1, x2, y2)  // Angle in 0-255 units
    ///   deltaX = Cos256(direction) * moveSpeed
    ///   deltaY = Sin256(direction) * moveSpeed
    ///   // Mob follows the foothold slope naturally
    /// </code>
    ///
    /// <para><b>═══════════════════════════════════════════════════════════════</b></para>
    /// <para><b>JUMP PHYSICS SYSTEM</b></para>
    /// <para><b>═══════════════════════════════════════════════════════════════</b></para>
    ///
    /// <para><b>9. Physics Constants (from Map.wz/Physics.img):</b></para>
    /// <code>
    ///   gravityAcc  = 2000 px/s² → 0.556 px/tick² (at 60fps)
    ///   jumpSpeed   = 555 px/s  → 9.25 px/tick (initial upward velocity)
    ///   fallSpeed   = 670 px/s  → 11.17 px/tick (terminal velocity)
    /// </code>
    ///
    /// <para><b>10. Jump State Machine:</b></para>
    /// <code>
    ///   None → [TriggerJump] → Jumping (VelocityY = -JumpHeight)
    ///                              ↓
    ///                         VelocityY += GravityAcc
    ///                              ↓
    ///                         VelocityY >= 0 → Falling
    ///                              ↓
    ///                         Y >= footholdY → None (landed)
    /// </code>
    ///
    /// <para><b>═══════════════════════════════════════════════════════════════</b></para>
    /// <para><b>FLYING MOVEMENT SYSTEM</b></para>
    /// <para><b>═══════════════════════════════════════════════════════════════</b></para>
    ///
    /// <para><b>11. Flying Vertical Bobbing:</b></para>
    /// <code>
    ///   CosY += 7 * speedFactor       // Advance phase (0-255 cycle)
    ///   Y = SrcY - Cos256(CosY) * 16  // ±16 pixel oscillation around spawn Y
    /// </code>
    ///
    /// <para><b>12. Flying Horizontal Movement:</b></para>
    /// <code>
    ///   moveAmount = 1.5f * FlySpeed * speedFactor
    ///   X += moveAmount (or -= for left)
    ///   // Bounce at effectiveLeft/effectiveRight boundaries
    /// </code>
    /// </summary>
    public class MobMovementInfo
    {
        // Movement state
        public MobMoveDirection MoveDirection { get; set; } = MobMoveDirection.Left;
        public MobMoveType MoveType { get; set; } = MobMoveType.Move;

        // Position (dynamic, updates during simulation)
        public float X { get; set; }
        public float Y { get; set; }

        // Spawn position (for respawning and boundary calculations)
        public int SpawnX => _spawnX;
        public int SpawnY => _spawnY;

        // Movement boundaries (rx0 and rx1 from mob spawn data)
        public int RX0 { get; set; }  // Left boundary
        public int RX1 { get; set; }  // Right boundary

        // Platform boundaries (calculated from foothold)
        public int PlatformLeft { get; set; }   // Left edge of current platform
        public int PlatformRight { get; set; }  // Right edge of current platform

        // Movement parameters (default 2 as per MapleNecrocer)
        public float MoveSpeed { get; set; } = 2.0f;
        public float FlySpeed { get; set; } = 2.0f;

        // Foothold reference for ground-based movement
        public FootholdLine CurrentFoothold { get; set; }
        public int FootholdDirection { get; set; }  // Direction angle for diagonal footholds (0-255)

        // All footholds reference for navigation
        private IEnumerable<FootholdLine> _allFootholds;

        // Flying behavior
        public float CosY { get; set; } = 0;  // For vertical bobbing animation
        public float SrcY { get; set; }       // Original Y position for flying mobs

        // Animation state
        public MobAction CurrentAction { get; set; } = MobAction.Stand;
        public int Frame { get; set; } = 0;
        public int AnimTime { get; set; } = 0;

        // Flip state
        public bool FlipX { get; set; } = false;

        // NoFlip - when true, mob cannot change facing direction (from info/noFlip WZ property)
        public bool NoFlip { get; set; } = false;

        /// <summary>
        /// When true, mob ignores RX0/RX1 spawn boundaries and can move across the entire
        /// connected platform. Used for boss monsters that should patrol larger areas.
        /// </summary>
        public bool UsePlatformBounds { get; set; } = false;

        // Random movement timing
        private Random _random = new Random();
        private int _nextDirectionChangeTime = 0;
        private int _directionChangeCooldown = 0;  // Prevents rapid direction flipping

        // Spawn position (for reference)
        private int _spawnX;
        private int _spawnY;

        // Flying mob fields
        private bool _isFlyingMob = false;
        private int _flyingYDirection = 1;  // 1 = moving down, -1 = moving up
        private bool _flyingInitialized = false;  // One-time Y position adjustment

        // Jump physics fields - uses CVecCtrl constants for consistency with client
        // Official values from Map.wz/Physics.img (see CVecCtrl class)
        public MobJumpState JumpState { get; set; } = MobJumpState.None;
        public float VelocityX { get; set; } = 0;           // Current horizontal velocity (px/tick)
        public float VelocityY { get; set; } = 0;           // Current vertical velocity (px/tick)
        public float GravityAcc { get; set; } = CVecCtrl.GravityAcceleration;   // From CVecCtrl
        public float JumpHeight { get; set; } = CVecCtrl.JumpVelocity;          // From CVecCtrl
        public float MaxFallSpeed { get; set; } = CVecCtrl.TerminalVelocity;    // From CVecCtrl
        private int _jumpCooldown = 0;                       // Time until next jump allowed
        private bool _isJumpingMob = false;                  // Whether this mob can jump

        // Knockback/Impact state (based on CVecCtrl.SetImpactNext)
        private float _impactVelocityX = 0;
        private float _impactVelocityY = 0;
        private bool _hasPendingImpact = false;
        private int _knockbackRecoveryTime = 0;  // Time until knockback recovery

        // Map boundaries (VR bounds)
        public int MapLeft { get; set; } = int.MinValue;
        public int MapRight { get; set; } = int.MaxValue;
        public int MapTop { get; set; } = int.MinValue;
        public int MapBottom { get; set; } = int.MaxValue;

        // Y shift from foothold (cy - y from WZ data)
        // If yShift > 0, mob spawns above the foothold
        private int _yShift = 0;

        /// <summary>
        /// Initialize movement info from mob instance data
        /// </summary>
        /// <param name="x">Spawn X position</param>
        /// <param name="y">Spawn Y position (cy from WZ - actual mob position)</param>
        /// <param name="rx0Shift">Left boundary shift from spawn point</param>
        /// <param name="rx1Shift">Right boundary shift from spawn point</param>
        /// <param name="yShift">Y shift from foothold (cy - y). Foothold Y = y - yShift</param>
        /// <param name="isFlyingMob">Whether this mob can fly</param>
        /// <param name="isJumpingMob">Whether this mob can jump</param>
        /// <param name="noFlip">Whether this mob cannot change facing direction (info/noFlip = 1)</param>
        public void Initialize(int x, int y, int rx0Shift, int rx1Shift, int yShift, bool isFlyingMob, bool isJumpingMob = false, bool noFlip = false)
        {
            _spawnX = x;
            _spawnY = y;
            _yShift = yShift;
            X = x;
            Y = y;
            SrcY = y;
            _isFlyingMob = isFlyingMob;
            _isJumpingMob = isJumpingMob;
            NoFlip = noFlip;

            // Calculate RX0 and RX1 from shifts
            // From MapLoader: rx0Shift = x - rx0, rx1Shift = rx1 - x
            // So: rx0 = x - rx0Shift, rx1 = x + rx1Shift
            RX0 = x - rx0Shift;
            RX1 = x + rx1Shift;

            // Ensure boundaries are valid
            if (RX0 > RX1)
            {
                int temp = RX0;
                RX0 = RX1;
                RX1 = temp;
            }

            // If boundaries are too small or zero, set a reasonable default range
            if (RX1 - RX0 < 50)
            {
                RX0 = x - 100;
                RX1 = x + 100;
            }

            // Initialize platform boundaries to RX boundaries (will be updated when foothold is found)
            PlatformLeft = RX0;
            PlatformRight = RX1;

            // Set movement type based on mob properties (priority: Fly > Jump > Move)
            if (isFlyingMob)
                MoveType = MobMoveType.Fly;
            else if (isJumpingMob)
                MoveType = MobMoveType.Jump;
            else
                MoveType = MobMoveType.Move;

            // Random initial direction (only if noFlip is false)
            if (!noFlip)
            {
                MoveDirection = _random.Next(2) == 0 ? MobMoveDirection.Left : MobMoveDirection.Right;
                FlipX = MoveDirection == MobMoveDirection.Right;
            }
            else
            {
                // NoFlip mobs keep their initial facing direction
                MoveDirection = MobMoveDirection.Left;  // Default direction
                // FlipX remains at initial value (set by caller if needed)
            }

            // Set initial direction change time
            _nextDirectionChangeTime = _random.Next(2000, 5000);

            // Initialize flying Y direction
            if (isFlyingMob)
            {
                _flyingYDirection = _random.Next(2) == 0 ? -1 : 1;
            }

            // Initialize jump cooldown
            if (isJumpingMob)
            {
                _jumpCooldown = _random.Next(1000, 3000);
            }
        }

        #region Knockback System (based on CVecCtrl.SetImpactNext)

        /// <summary>
        /// Apply knockback/impact to the mob.
        /// Based on CVecCtrl::SetImpactNext from the client.
        /// Knockback velocities accumulate with clamping.
        /// </summary>
        /// <param name="vx">Horizontal knockback velocity (positive = right)</param>
        /// <param name="vy">Vertical knockback velocity (negative = up)</param>
        public void ApplyKnockback(float vx, float vy)
        {
            if (!_hasPendingImpact)
            {
                _impactVelocityX = 0;
                _impactVelocityY = 0;
            }

            _hasPendingImpact = true;

            // Accumulate horizontal impact with clamping (matching CVecCtrl logic)
            if (vx < 0 && vx < _impactVelocityX)
            {
                float combined = vx + _impactVelocityX;
                _impactVelocityX = combined < vx ? vx : combined;
            }
            else if (vx > 0 && vx > _impactVelocityX)
            {
                float combined = vx + _impactVelocityX;
                _impactVelocityX = combined > vx ? vx : combined;
            }

            // Accumulate vertical impact with clamping
            if (vy < 0 && vy < _impactVelocityY)
            {
                float combined = vy + _impactVelocityY;
                _impactVelocityY = combined < vy ? vy : combined;
            }
            else if (vy > 0 && vy > _impactVelocityY)
            {
                float combined = vy + _impactVelocityY;
                _impactVelocityY = combined > vy ? vy : combined;
            }

            // Set recovery time
            _knockbackRecoveryTime = 500; // 500ms knockback stun
        }

        /// <summary>
        /// Apply a simple knockback in a direction
        /// </summary>
        /// <param name="force">Knockback force</param>
        /// <param name="knockbackRight">True = knock right, False = knock left</param>
        public void ApplyKnockback(float force, bool knockbackRight)
        {
            float vx = knockbackRight ? force : -force;
            float vy = -force * 0.5f; // Slight upward component
            ApplyKnockback(vx, vy);
        }

        /// <summary>
        /// Apply immediate impact (like CVecCtrl::Impact).
        /// Sets velocity directly instead of queuing.
        /// Use for strong knockbacks like boss attacks.
        /// </summary>
        /// <param name="vx">Horizontal impact velocity</param>
        /// <param name="vy">Vertical impact velocity (negative = up)</param>
        public void ApplyImpact(float vx, float vy)
        {
            // Apply immediately to velocity
            VelocityX = vx;
            VelocityY = vy;

            // Clear any pending queued impact
            _hasPendingImpact = false;
            _impactVelocityX = 0;
            _impactVelocityY = 0;

            // Set recovery time
            _knockbackRecoveryTime = 500; // 500ms knockback stun

            // Detach from foothold if vertical impact (mob goes airborne)
            if (vy < 0)
            {
                CurrentFoothold = null;
            }
        }

        /// <summary>
        /// Apply immediate impact in a direction.
        /// </summary>
        /// <param name="force">Impact force (px/s)</param>
        /// <param name="knockRight">True = knock right, False = knock left</param>
        /// <param name="verticalForce">Vertical force (negative = up)</param>
        public void ApplyImpact(float force, bool knockRight, float verticalForce = -50f)
        {
            float vx = knockRight ? force : -force;
            ApplyImpact(vx, verticalForce);
        }

        /// <summary>
        /// Apply impact away from a source position.
        /// </summary>
        /// <param name="sourceX">X position of impact source</param>
        /// <param name="force">Impact force (px/s)</param>
        /// <param name="verticalForce">Vertical force (negative = up)</param>
        public void ApplyImpactFrom(float sourceX, float force, float verticalForce = -50f)
        {
            bool knockRight = sourceX < X;
            ApplyImpact(force, knockRight, verticalForce);
        }

        /// <summary>
        /// Check if mob is currently in knockback state
        /// </summary>
        public bool IsInKnockback => _knockbackRecoveryTime > 0;

        /// <summary>
        /// Speed multiplier for AI-controlled chase behavior
        /// </summary>
        private float _speedMultiplier = 1.0f;

        /// <summary>
        /// Whether movement is forced to stop
        /// </summary>
        private bool _isStopped = false;

        /// <summary>
        /// Pending direction change (waits for animation cycle to complete)
        /// </summary>
        private MobMoveDirection _pendingDirection = MobMoveDirection.None;

        /// <summary>
        /// Number of frames shown since last direction change
        /// </summary>
        private int _framesSinceDirectionChange = 0;

        /// <summary>
        /// Last frame index seen (to count unique frames)
        /// </summary>
        private int _lastFrameIndex = -1;

        /// <summary>
        /// Minimum number of unique frames that must be shown before direction can change
        /// This ensures the mob completes most of its movement animation before turning
        /// </summary>
        private const int MIN_FRAMES_BEFORE_TURN = 4;

        /// <summary>
        /// Force the mob to move in a specific direction (for AI chase behavior)
        /// Waits for current movement animation to show enough frames before changing direction
        /// </summary>
        /// <param name="direction">Target direction</param>
        /// <param name="currentFrameIndex">Current animation frame index (0 = start of cycle)</param>
        /// <param name="frameCount">Total frames in animation</param>
        public void ForceDirection(MobMoveDirection direction, int currentFrameIndex = 0, int frameCount = 1)
        {
            if (direction == MobMoveDirection.None)
                return;

            // Ensure mob is in Move action
            if (CurrentAction == MobAction.Stand)
            {
                CurrentAction = MobAction.Move;
            }

            // If same direction, just keep moving and reset pending
            if (MoveDirection == direction)
            {
                _pendingDirection = MobMoveDirection.None;
                return;
            }

            // If not currently moving (just started), apply immediately
            if (MoveDirection == MobMoveDirection.None)
            {
                ApplyDirection(direction);
                _framesSinceDirectionChange = 0;
                _lastFrameIndex = currentFrameIndex;
                return;
            }

            // Queue direction change - will be applied when enough frames have been shown
            _pendingDirection = direction;
        }

        /// <summary>
        /// Update pending direction changes based on animation frame
        /// Call this from UpdateMovement with current frame info
        /// </summary>
        public void UpdatePendingDirection(int currentFrameIndex, int frameCount)
        {
            // Count unique frames shown (frame index changed)
            if (currentFrameIndex != _lastFrameIndex)
            {
                _framesSinceDirectionChange++;
                _lastFrameIndex = currentFrameIndex;
            }

            // Check if we have a pending direction change and enough frames have been shown
            if (_pendingDirection == MobMoveDirection.None)
                return;

            // Calculate minimum frames needed (at least MIN_FRAMES_BEFORE_TURN, or full cycle if fewer frames)
            int minFrames = Math.Min(MIN_FRAMES_BEFORE_TURN, Math.Max(frameCount - 1, 2));

            if (_framesSinceDirectionChange >= minFrames)
            {
                ApplyDirection(_pendingDirection);
                _pendingDirection = MobMoveDirection.None;
                _framesSinceDirectionChange = 0;
            }
        }

        /// <summary>
        /// Apply direction change immediately
        /// </summary>
        private void ApplyDirection(MobMoveDirection direction)
        {
            MoveDirection = direction;
            _framesSinceDirectionChange = 0;

            // Also update flip
            if (!NoFlip)
            {
                FlipX = (direction == MobMoveDirection.Right);
            }
        }

        /// <summary>
        /// Set speed multiplier for chase behavior
        /// </summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            _speedMultiplier = Math.Max(0.1f, multiplier);
        }

        /// <summary>
        /// Get the effective move speed (base speed * multiplier)
        /// </summary>
        public float EffectiveMoveSpeed => MoveSpeed * _speedMultiplier;

        /// <summary>
        /// Stop all movement
        /// </summary>
        public void Stop()
        {
            _isStopped = true;
            VelocityX = 0;
            VelocityY = 0;
            CurrentAction = MobAction.Stand;
        }

        /// <summary>
        /// Resume movement after stop
        /// </summary>
        public void Resume()
        {
            _isStopped = false;
        }

        /// <summary>
        /// Check if movement is stopped
        /// </summary>
        public bool IsStopped => _isStopped;

        /// <summary>
        /// Process pending knockback - called at start of UpdateMovement
        /// </summary>
        private void ProcessPendingKnockback(float speedFactor)
        {
            if (!_hasPendingImpact)
                return;

            // Apply impact velocity
            VelocityX = _impactVelocityX;
            VelocityY = _impactVelocityY;

            // Leave foothold if knocked back
            if (CurrentFoothold != null && Math.Abs(_impactVelocityY) > 0.5f)
            {
                JumpState = MobJumpState.Falling;
            }

            // Clear pending impact
            _impactVelocityX = 0;
            _impactVelocityY = 0;
            _hasPendingImpact = false;

            // Set action to hit
            CurrentAction = MobAction.Hit1;
        }

        /// <summary>
        /// Update physics during knockback state
        /// </summary>
        private void UpdateKnockbackPhysics(int deltaTimeMs, float speedFactor)
        {
            // Apply gravity
            VelocityY += GravityAcc * speedFactor;
            if (VelocityY > MaxFallSpeed)
                VelocityY = MaxFallSpeed;

            // Apply air drag to horizontal velocity
            VelocityX *= (float)Math.Pow(CVecCtrl.AirDrag, speedFactor);

            // Update position
            X += VelocityX * speedFactor;
            Y += VelocityY * speedFactor;

            // Check boundaries
            int effectiveLeft = RX0;
            int effectiveRight = RX1;
            if (MapLeft != int.MinValue)
                effectiveLeft = Math.Max(effectiveLeft, MapLeft + 30);
            if (MapRight != int.MaxValue)
                effectiveRight = Math.Min(effectiveRight, MapRight - 30);

            if (X < effectiveLeft)
            {
                X = effectiveLeft;
                VelocityX = -VelocityX * 0.5f; // Bounce
            }
            else if (X > effectiveRight)
            {
                X = effectiveRight;
                VelocityX = -VelocityX * 0.5f; // Bounce
            }

            // Check for landing
            if (JumpState == MobJumpState.Falling || VelocityY > 0)
            {
                FootholdLine belowFH = FindBelow(X, Y - VelocityY - 2);
                if (belowFH != null)
                {
                    float fhY = CalculateYOnFoothold(belowFH, X);
                    if (Y >= fhY - 3)
                    {
                        // Land
                        Y = fhY;
                        JumpState = MobJumpState.None;
                        VelocityY = 0;
                        VelocityX = 0;
                        CurrentFoothold = belowFH;
                    }
                }
            }

            // Reset to spawn if fell off map
            if (MapBottom != int.MaxValue && Y > MapBottom + 100)
            {
                X = _spawnX;
                Y = _spawnY;
                JumpState = MobJumpState.None;
                VelocityX = 0;
                VelocityY = 0;
                _knockbackRecoveryTime = 0;
                FindCurrentFoothold(_allFootholds);
            }
        }

        #endregion

        /// <summary>
        /// Update mob movement based on elapsed time.
        /// Call this from MapSimulator.Update()
        /// </summary>
        /// <param name="deltaTimeMs">Time elapsed since last update in milliseconds</param>
        public void UpdateMovement(int deltaTimeMs)
        {
            // Update knockback recovery timer
            if (_knockbackRecoveryTime > 0)
            {
                _knockbackRecoveryTime -= deltaTimeMs;
            }

            // Process any pending knockback
            float speedFactor = deltaTimeMs / 16.67f;
            ProcessPendingKnockback(speedFactor);

            // During knockback, apply physics but skip normal AI
            if (_knockbackRecoveryTime > 0)
            {
                UpdateKnockbackPhysics(deltaTimeMs, speedFactor);
                return;
            }

            if (MoveType == MobMoveType.Stand)
                return;

            // For ground-based mobs (Move/Jump), wait until foothold is assigned
            // This prevents mobs from falling through the map on initial load
            if ((MoveType == MobMoveType.Move || MoveType == MobMoveType.Jump) &&
                CurrentFoothold == null && JumpState == MobJumpState.None)
            {
                return;  // Wait for FindCurrentFoothold to be called
            }

            AnimTime += deltaTimeMs;

            if (MoveType == MobMoveType.Fly)
            {
                UpdateFlyingMovement(deltaTimeMs);
            }
            else if (MoveType == MobMoveType.Jump)
            {
                UpdateJumpingMovement(deltaTimeMs);
            }
            else if (MoveType == MobMoveType.Move)
            {
                UpdateWalkingMovement(deltaTimeMs);
            }
        }

        /// <summary>
        /// Update flying mob movement with vertical movement across map
        /// </summary>
        private void UpdateFlyingMovement(int deltaTimeMs)
        {
            // Flying mobs always use "fly" action
            CurrentAction = MobAction.Fly;

            float speedFactor = deltaTimeMs / 16.67f;  // Normalize to ~60fps

            // Calculate effective horizontal boundaries (check for valid map bounds first)
            int effectiveLeft = RX0;
            int effectiveRight = RX1;

            if (MapLeft != int.MinValue)
                effectiveLeft = Math.Max(effectiveLeft, MapLeft + 50);
            if (MapRight != int.MaxValue)
                effectiveRight = Math.Min(effectiveRight, MapRight - 50);

            // Safety check - if boundaries are invalid, use spawn position
            if (effectiveLeft >= effectiveRight)
            {
                effectiveLeft = _spawnX - 100;
                effectiveRight = _spawnX + 100;
            }

            // Horizontal movement (MapleNecrocer uses 1.5f * FlySpeed)
            float flyMoveAmount = 1.5f * FlySpeed * speedFactor;

            if (MoveDirection == MobMoveDirection.Left)
            {
                X -= flyMoveAmount;
                if (!NoFlip) FlipX = false;

                // Check left boundary
                if (X <= effectiveLeft)
                {
                    X = effectiveLeft;
                    MoveDirection = MobMoveDirection.Right;
                    if (!NoFlip) FlipX = true;
                }
            }
            else if (MoveDirection == MobMoveDirection.Right)
            {
                X += flyMoveAmount;
                if (!NoFlip) FlipX = true;

                // Check right boundary
                if (X >= effectiveRight)
                {
                    X = effectiveRight;
                    MoveDirection = MobMoveDirection.Left;
                    if (!NoFlip) FlipX = false;
                }
            }

            // Vertical bobbing using cosine wave (MapleNecrocer: CosY += 7; Y = SrcY - Cos256(CosY) * 16)
            // Uses SrcY as base position with ±16 pixel oscillation
            CosY += 7 * speedFactor;
            if (CosY >= 256)
                CosY -= 256;

            // Cos256 uses 256-unit circle: Cos256(angle) = Cos(angle * 2PI / 256)
            float newY = SrcY - (float)(Cos256(CosY) * 16);

            // Flying mobs don't need Y clamping - they fly at their spawn height
            Y = newY;
        }

        /// <summary>
        /// Update jumping mob movement with gravity physics (based on MapleNecrocer)
        /// </summary>
        private void UpdateJumpingMovement(int deltaTimeMs)
        {
            float speedFactor = deltaTimeMs / 16.67f;  // Normalize to ~60fps

            // Update jump cooldown
            if (_jumpCooldown > 0)
                _jumpCooldown -= deltaTimeMs;

            // Handle jump physics when in air
            if (JumpState != MobJumpState.None)
            {
                CurrentAction = MobAction.Jump;

                // Apply gravity - reduce upward velocity or increase downward velocity
                VelocityY += GravityAcc * speedFactor;

                // Clamp to max fall speed
                if (VelocityY > MaxFallSpeed)
                    VelocityY = MaxFallSpeed;

                // Transition from jumping to falling
                if (JumpState == MobJumpState.Jumping && VelocityY >= 0)
                {
                    JumpState = MobJumpState.Falling;
                }

                // Apply vertical movement
                Y += VelocityY * speedFactor;

                // Horizontal movement while in air
                float airMoveAmount = MoveSpeed * 0.8f * speedFactor;
                float newX = X;

                if (MoveDirection == MobMoveDirection.Left)
                {
                    newX = X - airMoveAmount;
                    if (!NoFlip) FlipX = false;
                }
                else if (MoveDirection == MobMoveDirection.Right)
                {
                    newX = X + airMoveAmount;
                    if (!NoFlip) FlipX = true;
                }

                // Check if there's a foothold below at the new X position
                bool hasFootholdBelow = HasFootholdBelow(newX, Y);

                if (hasFootholdBelow)
                {
                    // There's a platform below - allow horizontal movement
                    X = newX;
                }
                else
                {
                    // No platform below - stop at edge to prevent jumping off map
                    // But still allow small movements within current foothold range
                    if (CurrentFoothold != null)
                    {
                        int fhMinX = Math.Min(CurrentFoothold.FirstDot.X, CurrentFoothold.SecondDot.X);
                        int fhMaxX = Math.Max(CurrentFoothold.FirstDot.X, CurrentFoothold.SecondDot.X);

                        // Allow movement within foothold bounds
                        if (newX >= fhMinX && newX <= fhMaxX)
                        {
                            X = newX;
                        }
                        // Otherwise clamp to foothold edge
                        else if (MoveDirection == MobMoveDirection.Left)
                        {
                            X = fhMinX;
                        }
                        else
                        {
                            X = fhMaxX;
                        }
                    }
                }

                // Check for landing
                if (JumpState == MobJumpState.Falling)
                {
                    CheckLanding();
                }
            }
            else
            {
                // On ground - use walking movement but with random jumps
                UpdateWalkingMovementForJumper(deltaTimeMs, speedFactor);
            }

            // Clamp X to boundaries
            int effectiveLeft = RX0;
            int effectiveRight = RX1;
            if (MapLeft != int.MinValue)
                effectiveLeft = Math.Max(effectiveLeft, MapLeft + 30);
            if (MapRight != int.MaxValue)
                effectiveRight = Math.Min(effectiveRight, MapRight - 30);

            if (X < effectiveLeft)
            {
                X = effectiveLeft;
                MoveDirection = MobMoveDirection.Right;
                if (!NoFlip) FlipX = true;
            }
            else if (X > effectiveRight)
            {
                X = effectiveRight;
                MoveDirection = MobMoveDirection.Left;
                if (!NoFlip) FlipX = false;
            }
        }

        /// <summary>
        /// Walking movement for jumping mobs (triggers random jumps)
        /// </summary>
        private void UpdateWalkingMovementForJumper(int deltaTimeMs, float speedFactor)
        {
            // Update cooldowns
            if (_directionChangeCooldown > 0)
                _directionChangeCooldown -= deltaTimeMs;

            // Random direction changes
            _nextDirectionChangeTime -= deltaTimeMs;
            if (_nextDirectionChangeTime <= 0)
            {
                int randomAction = _random.Next(100);
                if (randomAction < 30)
                {
                    // Trigger a jump
                    TriggerJump();
                }
                else if (randomAction < 50 && _directionChangeCooldown <= 0)
                {
                    // Switch direction (only if cooldown expired)
                    MoveDirection = MoveDirection == MobMoveDirection.Left
                        ? MobMoveDirection.Right
                        : MobMoveDirection.Left;
                    if (!NoFlip) FlipX = MoveDirection == MobMoveDirection.Right;
                    _directionChangeCooldown = 500;  // 500ms cooldown
                }
                _nextDirectionChangeTime = _random.Next(800, 2000);
            }

            // Check if near platform edge before moving (only if direction change allowed)
            if (CurrentFoothold != null && _directionChangeCooldown <= 0)
            {
                int fhMinX = Math.Min(CurrentFoothold.FirstDot.X, CurrentFoothold.SecondDot.X);
                int fhMaxX = Math.Max(CurrentFoothold.FirstDot.X, CurrentFoothold.SecondDot.X);
                int fhWidth = fhMaxX - fhMinX;

                // Only do edge detection if foothold is wide enough (> 60 pixels)
                if (fhWidth > 60)
                {
                    bool nearLeftEdge = X <= fhMinX + 15 && MoveDirection == MobMoveDirection.Left;
                    bool nearRightEdge = X >= fhMaxX - 15 && MoveDirection == MobMoveDirection.Right;

                    if (nearLeftEdge || nearRightEdge)
                    {
                        // At edge - either jump or turn around
                        if (_jumpCooldown <= 0 && _random.Next(100) < 60)
                        {
                            TriggerJump();
                            return;
                        }
                        else
                        {
                            // Turn around at edge
                            MoveDirection = MoveDirection == MobMoveDirection.Left
                                ? MobMoveDirection.Right
                                : MobMoveDirection.Left;
                            if (!NoFlip) FlipX = MoveDirection == MobMoveDirection.Right;
                            _directionChangeCooldown = 500;
                            return;
                        }
                    }
                }
            }

            // Move on ground
            CurrentAction = MobAction.Move;
            float moveAmount = MoveSpeed * speedFactor;
            MobMoveDirection moveDir = MoveDirection;

            // Check for wall collision first
            if (MoveDirection == MobMoveDirection.Left)
            {
                FootholdLine wallL = FindWallL(X - 4, Y - 4);
                if (wallL != null && X - moveAmount <= wallL.FirstDot.X)
                {
                    X = wallL.FirstDot.X + 1;
                    if (_jumpCooldown <= 0 && _random.Next(100) < 40)
                    {
                        TriggerJump();
                    }
                    else
                    {
                        MoveDirection = MobMoveDirection.Right;
                        if (!NoFlip) FlipX = true;
                        _directionChangeCooldown = 500;
                    }
                    return;
                }
            }
            else if (MoveDirection == MobMoveDirection.Right)
            {
                FootholdLine wallR = FindWallR(X + 4, Y - 4);
                if (wallR != null && X + moveAmount >= wallR.FirstDot.X)
                {
                    X = wallR.FirstDot.X - 1;
                    if (_jumpCooldown <= 0 && _random.Next(100) < 40)
                    {
                        TriggerJump();
                    }
                    else
                    {
                        MoveDirection = MobMoveDirection.Left;
                        if (!NoFlip) FlipX = false;
                        _directionChangeCooldown = 500;
                    }
                    return;
                }
            }

            // Angle-based slope movement (MapleNecrocer style)
            bool hasFoothold;
            if (CurrentFoothold != null && !CurrentFoothold.IsWall)
            {
                int x1 = CurrentFoothold.FirstDot.X;
                int y1 = CurrentFoothold.FirstDot.Y;
                int x2 = CurrentFoothold.SecondDot.X;
                int y2 = CurrentFoothold.SecondDot.Y;

                // Calculate direction angle along the foothold
                int direction;
                if (MoveDirection == MobMoveDirection.Right)
                {
                    if (x1 < x2)
                        direction = GetAngle256(x1, y1, x2, y2);
                    else
                        direction = GetAngle256(x2, y2, x1, y1);
                    if (!NoFlip) FlipX = true;
                }
                else
                {
                    if (x1 > x2)
                        direction = GetAngle256(x1, y1, x2, y2);
                    else
                        direction = GetAngle256(x2, y2, x1, y1);
                    if (!NoFlip) FlipX = false;
                }

                // Move along the slope
                float deltaX = (float)(Cos256(direction) * moveAmount);
                float deltaY = (float)(Sin256(direction) * moveAmount);

                X += deltaX;
                Y += deltaY;

                // Check if still on foothold
                int fhMinX = Math.Min(x1, x2);
                int fhMaxX = Math.Max(x1, x2);
                if (X >= fhMinX && X <= fhMaxX)
                {
                    hasFoothold = true;
                }
                else
                {
                    // Try anchor-based navigation first, then fallback to search
                    hasFoothold = TryTransitionFoothold(moveDir) || UpdateYPosition();
                }
            }
            else
            {
                // Flat movement fallback
                if (MoveDirection == MobMoveDirection.Left)
                {
                    X -= moveAmount;
                    if (!NoFlip) FlipX = false;
                }
                else
                {
                    X += moveAmount;
                    if (!NoFlip) FlipX = true;
                }
                hasFoothold = UpdateYPosition();
            }

            // If walked off edge (no foothold at new position), handle it
            if (!hasFoothold && CurrentFoothold != null && _directionChangeCooldown <= 0)
            {
                // Revert position and either jump or turn around
                int fhMinX = Math.Min(CurrentFoothold.FirstDot.X, CurrentFoothold.SecondDot.X);
                int fhMaxX = Math.Max(CurrentFoothold.FirstDot.X, CurrentFoothold.SecondDot.X);

                if (moveDir == MobMoveDirection.Left)
                {
                    X = fhMinX + 2;
                }
                else
                {
                    X = fhMaxX - 2;
                }

                // Update Y for corrected position
                Y = CalculateYOnFoothold(CurrentFoothold, X);

                // Either jump or turn around
                if (_jumpCooldown <= 0 && _random.Next(100) < 50)
                {
                    TriggerJump();
                }
                else
                {
                    MoveDirection = MoveDirection == MobMoveDirection.Left
                        ? MobMoveDirection.Right
                        : MobMoveDirection.Left;
                    if (!NoFlip) FlipX = MoveDirection == MobMoveDirection.Right;
                    _directionChangeCooldown = 500;
                }
            }
        }

        /// <summary>
        /// Trigger a jump
        /// </summary>
        private void TriggerJump()
        {
            if (JumpState != MobJumpState.None || _jumpCooldown > 0)
                return;

            // Don't jump if too close to map boundaries (would jump off map)
            int effectiveLeft = RX0;
            int effectiveRight = RX1;
            if (MapLeft != int.MinValue)
                effectiveLeft = Math.Max(effectiveLeft, MapLeft + 50);
            if (MapRight != int.MaxValue)
                effectiveRight = Math.Min(effectiveRight, MapRight - 50);

            bool tooCloseToLeftEdge = X <= effectiveLeft + 30 && MoveDirection == MobMoveDirection.Left;
            bool tooCloseToRightEdge = X >= effectiveRight - 30 && MoveDirection == MobMoveDirection.Right;

            if (tooCloseToLeftEdge || tooCloseToRightEdge)
            {
                // Turn around instead of jumping off map
                MoveDirection = MoveDirection == MobMoveDirection.Left ? MobMoveDirection.Right : MobMoveDirection.Left;
                if (!NoFlip) FlipX = MoveDirection == MobMoveDirection.Right;
                _jumpCooldown = _random.Next(500, 1500);
                return;
            }

            JumpState = MobJumpState.Jumping;
            VelocityY = -JumpHeight;  // Negative = upward
            _jumpCooldown = _random.Next(1500, 3500);
            CurrentAction = MobAction.Jump;
        }

        /// <summary>
        /// Check if the jumping mob has landed on a foothold (MapleNecrocer style)
        /// </summary>
        private void CheckLanding()
        {
            if (_allFootholds == null || JumpState != MobJumpState.Falling)
                return;

            // Use FindBelow to check for landing - look ahead based on velocity
            // MapleNecrocer: FindBelow(new Vector2(X, Y - VelocityY - 2), ref BelowFH)
            FootholdLine belowFH = FindBelow(X, Y - VelocityY - 2);

            if (belowFH != null)
            {
                float fhY = CalculateYOnFoothold(belowFH, X);

                // MapleNecrocer: if (Y >= Below.Y - 3)
                if (Y >= fhY - 3)
                {
                    // Land on this foothold
                    Y = fhY;
                    JumpState = MobJumpState.None;
                    VelocityY = 0;
                    CurrentFoothold = belowFH;
                    CurrentAction = MobAction.Stand;
                    return;
                }
            }

            // Calculate effective boundaries
            int effectiveLeft = RX0;
            int effectiveRight = RX1;
            if (MapLeft != int.MinValue)
                effectiveLeft = Math.Max(effectiveLeft, MapLeft + 30);
            if (MapRight != int.MaxValue)
                effectiveRight = Math.Min(effectiveRight, MapRight - 30);

            // If fell below map bottom or at edge with no foothold, reset to spawn
            bool atLeftEdge = X <= effectiveLeft + 5;
            bool atRightEdge = X >= effectiveRight - 5;
            bool fellTooFar = MapBottom != int.MaxValue && Y > MapBottom + 100;

            if (fellTooFar || ((atLeftEdge || atRightEdge) && belowFH == null && Y > _spawnY + 50))
            {
                X = _spawnX;
                Y = _spawnY;
                JumpState = MobJumpState.None;
                VelocityY = 0;
                FindCurrentFoothold(_allFootholds);
            }
        }

        /// <summary>
        /// Check if there's a foothold below the given position that the mob could land on
        /// </summary>
        /// <param name="x">X position to check</param>
        /// <param name="y">Current Y position</param>
        /// <returns>True if a foothold exists below this position</returns>
        private bool HasFootholdBelow(float x, float y)
        {
            // Use FindBelow to check - consistent with MapleNecrocer
            return FindBelow(x, y) != null;
        }

        /// <summary>
        /// Update walking mob movement along footholds
        /// </summary>
        private void UpdateWalkingMovement(int deltaTimeMs)
        {
            float speedFactor = deltaTimeMs / 16.67f;  // Normalize to ~60fps

            // Random direction changes and action switches
            _nextDirectionChangeTime -= deltaTimeMs;
            if (_nextDirectionChangeTime <= 0)
            {
                // Random chance to change direction or pause
                int randomAction = _random.Next(100);
                if (randomAction < 20)
                {
                    // Switch direction and keep moving
                    MoveDirection = MoveDirection == MobMoveDirection.Left
                        ? MobMoveDirection.Right
                        : MobMoveDirection.Left;
                    if (!NoFlip) FlipX = MoveDirection == MobMoveDirection.Right;
                    CurrentAction = MobAction.Move;
                }
                else if (randomAction < 40)
                {
                    // Pause - stand still
                    CurrentAction = MobAction.Stand;
                    _nextDirectionChangeTime = _random.Next(1000, 2500);  // Stand for a while
                }
                else
                {
                    // Start or continue moving
                    CurrentAction = MobAction.Move;
                    _nextDirectionChangeTime = _random.Next(2000, 5000);  // Move for a while
                }

                if (CurrentAction == MobAction.Move)
                {
                    _nextDirectionChangeTime = _random.Next(1500, 4000);
                }
            }

            // Only move if in "move" or "walk" action
            if (CurrentAction != MobAction.Move)
                return;

            // Determine movement boundaries
            // Boss monsters (UsePlatformBounds=true) use the entire connected platform
            // Regular mobs use RX0/RX1 spawn boundaries
            int effectiveLeft;
            int effectiveRight;

            if (UsePlatformBounds)
            {
                // Boss: use platform bounds (entire connected foothold chain)
                effectiveLeft = PlatformLeft;
                effectiveRight = PlatformRight;
            }
            else
            {
                // Regular mob: use RX spawn bounds
                effectiveLeft = RX0;
                effectiveRight = RX1;
            }

            // Also constrain to map boundaries if set
            if (MapLeft != int.MinValue)
                effectiveLeft = Math.Max(effectiveLeft, MapLeft + 30);
            if (MapRight != int.MaxValue)
                effectiveRight = Math.Min(effectiveRight, MapRight - 30);

            // Safety check - if boundaries are invalid, use spawn position
            if (effectiveLeft >= effectiveRight)
            {
                effectiveLeft = _spawnX - 100;
                effectiveRight = _spawnX + 100;
            }

            MobMoveDirection moveDir = MoveDirection; // Track original direction
            float moveAmount = MoveSpeed * speedFactor;

            // Check for wall collision first
            if (MoveDirection == MobMoveDirection.Left)
            {
                FootholdLine wallL = FindWallL(X - 4, Y - 4);
                if (wallL != null && X - moveAmount <= wallL.FirstDot.X)
                {
                    X = wallL.FirstDot.X + 1;
                    MoveDirection = MobMoveDirection.Right;
                    if (!NoFlip) FlipX = true;
                    return;
                }
            }
            else if (MoveDirection == MobMoveDirection.Right)
            {
                FootholdLine wallR = FindWallR(X + 4, Y - 4);
                if (wallR != null && X + moveAmount >= wallR.FirstDot.X)
                {
                    X = wallR.FirstDot.X - 1;
                    MoveDirection = MobMoveDirection.Left;
                    if (!NoFlip) FlipX = false;
                    return;
                }
            }

            // Angle-based slope movement (MapleNecrocer style)
            // Uses GetAngle256 to get foothold direction, then Sin256/Cos256 for movement
            if (CurrentFoothold != null && !CurrentFoothold.IsWall)
            {
                int x1 = CurrentFoothold.FirstDot.X;
                int y1 = CurrentFoothold.FirstDot.Y;
                int x2 = CurrentFoothold.SecondDot.X;
                int y2 = CurrentFoothold.SecondDot.Y;

                // Calculate direction angle along the foothold
                int direction;
                if (MoveDirection == MobMoveDirection.Right)
                {
                    // Moving right: angle from left point to right point
                    if (x1 < x2)
                        direction = GetAngle256(x1, y1, x2, y2);
                    else
                        direction = GetAngle256(x2, y2, x1, y1);
                    if (!NoFlip) FlipX = true;
                }
                else
                {
                    // Moving left: angle from right point to left point
                    if (x1 > x2)
                        direction = GetAngle256(x1, y1, x2, y2);
                    else
                        direction = GetAngle256(x2, y2, x1, y1);
                    if (!NoFlip) FlipX = false;
                }

                // Move along the slope using angle decomposition
                float deltaX = (float)(Cos256(direction) * moveAmount);
                float deltaY = (float)(Sin256(direction) * moveAmount);

                X += deltaX;
                Y += deltaY;

                // Check boundaries
                if (X <= effectiveLeft)
                {
                    X = effectiveLeft;
                    MoveDirection = MobMoveDirection.Right;
                    if (!NoFlip) FlipX = true;
                }
                else if (X >= effectiveRight)
                {
                    X = effectiveRight;
                    MoveDirection = MobMoveDirection.Left;
                    if (!NoFlip) FlipX = false;
                }

                // Check if we walked off the foothold edge
                int fhMinX = Math.Min(x1, x2);
                int fhMaxX = Math.Max(x1, x2);

                if (X < fhMinX || X > fhMaxX)
                {
                    // Try to find connected foothold via anchor navigation (efficient linked traversal)
                    bool foundNewFH = TryTransitionFoothold(moveDir);

                    // Fallback: try UpdateYPosition if anchor navigation failed
                    if (!foundNewFH)
                        foundNewFH = UpdateYPosition();

                    if (!foundNewFH)
                    {
                        // No connected foothold - turn around at platform edge
                        if (moveDir == MobMoveDirection.Left)
                        {
                            X = fhMinX + 2;
                            MoveDirection = MobMoveDirection.Right;
                            if (!NoFlip) FlipX = true;
                        }
                        else
                        {
                            X = fhMaxX - 2;
                            MoveDirection = MobMoveDirection.Left;
                            if (!NoFlip) FlipX = false;
                        }
                        Y = CalculateYOnFoothold(CurrentFoothold, X);
                    }
                }
            }
            else
            {
                // No foothold - use flat movement (fallback)
                if (MoveDirection == MobMoveDirection.Left)
                {
                    X -= moveAmount;
                    if (!NoFlip) FlipX = false;
                    if (X <= effectiveLeft)
                    {
                        X = effectiveLeft;
                        MoveDirection = MobMoveDirection.Right;
                        if (!NoFlip) FlipX = true;
                    }
                }
                else
                {
                    X += moveAmount;
                    if (!NoFlip) FlipX = true;
                    if (X >= effectiveRight)
                    {
                        X = effectiveRight;
                        MoveDirection = MobMoveDirection.Left;
                        if (!NoFlip) FlipX = false;
                    }
                }

                // Try to find a foothold
                UpdateYPosition();
            }
        }

        /// <summary>
        /// Update Y position by finding the foothold under current X position
        /// </summary>
        /// <returns>True if a valid foothold was found at current X, false otherwise</returns>
        private bool UpdateYPosition()
        {
            if (_allFootholds == null)
            {
                // No footholds - keep current Y
                return CurrentFoothold != null;
            }

            // PRIORITY 1: Stay on current foothold if X is still within its range
            // This prevents dropping off curvy/sloped platforms
            if (CurrentFoothold != null && !CurrentFoothold.IsWall)
            {
                int currentFhMinX = Math.Min(CurrentFoothold.FirstDot.X, CurrentFoothold.SecondDot.X);
                int currentFhMaxX = Math.Max(CurrentFoothold.FirstDot.X, CurrentFoothold.SecondDot.X);

                if (X >= currentFhMinX && X <= currentFhMaxX)
                {
                    // Still on current foothold - just update Y for the slope
                    Y = CalculateYOnFoothold(CurrentFoothold, X);
                    return true;
                }
            }

            // PRIORITY 2: Look for connected footholds or nearby footholds
            // Only search when we've moved outside current foothold's X range
            FootholdLine bestFh = null;
            float bestYDistance = float.MaxValue;

            // First pass: exact X match
            foreach (var fh in _allFootholds)
            {
                if (fh.IsWall)
                    continue;

                int fhMinX = Math.Min(fh.FirstDot.X, fh.SecondDot.X);
                int fhMaxX = Math.Max(fh.FirstDot.X, fh.SecondDot.X);

                // Check if X is within this foothold's range
                if (X >= fhMinX && X <= fhMaxX)
                {
                    float fhY = CalculateYOnFoothold(fh, X);
                    float yDistance = Math.Abs(fhY - Y);

                    // Use larger tolerance (50 pixels) for finding new footholds on slopes
                    if (fhY >= Y - 50 && yDistance < bestYDistance)
                    {
                        bestYDistance = yDistance;
                        bestFh = fh;
                    }
                }
            }

            // Second pass: if no exact match, try with small X tolerance for imperfect connections
            if (bestFh == null)
            {
                const int X_TOLERANCE = 5;
                foreach (var fh in _allFootholds)
                {
                    if (fh.IsWall)
                        continue;

                    int fhMinX = Math.Min(fh.FirstDot.X, fh.SecondDot.X);
                    int fhMaxX = Math.Max(fh.FirstDot.X, fh.SecondDot.X);

                    // Check if X is within foothold's range with tolerance
                    if (X >= fhMinX - X_TOLERANCE && X <= fhMaxX + X_TOLERANCE)
                    {
                        // Clamp X to foothold range for Y calculation
                        float clampedX = Math.Max(fhMinX, Math.Min(fhMaxX, X));
                        float fhY = CalculateYOnFoothold(fh, clampedX);
                        float yDistance = Math.Abs(fhY - Y);

                        // Use larger Y tolerance for edge transitions
                        if (fhY >= Y - 80 && yDistance < bestYDistance)
                        {
                            bestYDistance = yDistance;
                            bestFh = fh;
                        }
                    }
                }
            }

            if (bestFh != null)
            {
                CurrentFoothold = bestFh;
                // Clamp X to foothold bounds if needed
                int newFhMinX = Math.Min(bestFh.FirstDot.X, bestFh.SecondDot.X);
                int newFhMaxX = Math.Max(bestFh.FirstDot.X, bestFh.SecondDot.X);
                if (X < newFhMinX) X = newFhMinX;
                if (X > newFhMaxX) X = newFhMaxX;
                Y = CalculateYOnFoothold(bestFh, X);
                return true;
            }

            // No foothold found at current X
            return false;
        }

        /// <summary>
        /// Calculate Y position on a foothold at a given X
        /// </summary>
        private float CalculateYOnFoothold(FootholdLine fh, float x)
        {
            int x1 = fh.FirstDot.X;
            int y1 = fh.FirstDot.Y;
            int x2 = fh.SecondDot.X;
            int y2 = fh.SecondDot.Y;

            if (x1 > x2)
            {
                int tempX = x1; int tempY = y1;
                x1 = x2; y1 = y2;
                x2 = tempX; y2 = tempY;
            }

            if (x2 == x1)
                return y1;

            float t = (x - x1) / (float)(x2 - x1);
            t = Math.Max(0, Math.Min(1, t));
            return y1 + t * (y2 - y1);
        }

        /// <summary>
        /// Update platform boundaries by traversing all connected footholds at similar height
        /// </summary>
        private void UpdatePlatformBoundaries()
        {
            if (CurrentFoothold == null)
                return;

            // Get reference Y level from current foothold
            float referenceY = (CurrentFoothold.FirstDot.Y + CurrentFoothold.SecondDot.Y) / 2f;
            const int MAX_Y_DIFFERENCE = 100; // Only include footholds within this Y range

            // Use a set to track visited footholds to avoid infinite loops
            HashSet<FootholdLine> visited = new HashSet<FootholdLine>();
            Queue<FootholdLine> toVisit = new Queue<FootholdLine>();

            toVisit.Enqueue(CurrentFoothold);

            int leftMost = int.MaxValue;
            int rightMost = int.MinValue;

            while (toVisit.Count > 0)
            {
                FootholdLine fh = toVisit.Dequeue();

                if (visited.Contains(fh) || fh.IsWall)
                    continue;

                // Check if this foothold is at a similar Y level
                float fhY = (fh.FirstDot.Y + fh.SecondDot.Y) / 2f;
                if (Math.Abs(fhY - referenceY) > MAX_Y_DIFFERENCE)
                    continue; // Skip footholds on different platforms

                visited.Add(fh);

                // Expand boundaries
                int fhMinX = Math.Min(fh.FirstDot.X, fh.SecondDot.X);
                int fhMaxX = Math.Max(fh.FirstDot.X, fh.SecondDot.X);
                leftMost = Math.Min(leftMost, fhMinX);
                rightMost = Math.Max(rightMost, fhMaxX);

                // Add connected footholds through FirstDot
                if (fh.FirstDot is HaCreator.MapEditor.Instance.Shapes.FootholdAnchor anchor1)
                {
                    foreach (var line in anchor1.connectedLines)
                    {
                        if (line is FootholdLine connectedFh && !visited.Contains(connectedFh) && !connectedFh.IsWall)
                        {
                            toVisit.Enqueue(connectedFh);
                        }
                    }
                }

                // Add connected footholds through SecondDot
                if (fh.SecondDot is HaCreator.MapEditor.Instance.Shapes.FootholdAnchor anchor2)
                {
                    foreach (var line in anchor2.connectedLines)
                    {
                        if (line is FootholdLine connectedFh && !visited.Contains(connectedFh) && !connectedFh.IsWall)
                        {
                            toVisit.Enqueue(connectedFh);
                        }
                    }
                }
            }

            // Apply boundaries (with fallback if nothing was found)
            if (leftMost != int.MaxValue && rightMost != int.MinValue)
            {
                PlatformLeft = leftMost;
                PlatformRight = rightMost;
            }
        }

        /// <summary>
        /// Find the foothold below a given position (based on MapleNecrocer FindBelow)
        /// </summary>
        /// <param name="x">X position to search from</param>
        /// <param name="y">Y position to search from</param>
        /// <returns>The foothold below, or null if none found</returns>
        private FootholdLine FindBelow(float x, float y)
        {
            if (_allFootholds == null)
                return null;

            bool first = true;
            float maxY = 0;
            FootholdLine result = null;

            foreach (var fh in _allFootholds)
            {
                if (fh.IsWall)
                    continue;

                int x1 = fh.FirstDot.X;
                int y1 = fh.FirstDot.Y;
                int x2 = fh.SecondDot.X;
                int y2 = fh.SecondDot.Y;

                // Check if X is within foothold range (handles both directions)
                if ((x >= x1 && x <= x2) || (x >= x2 && x <= x1))
                {
                    // Skip vertical footholds (walls)
                    if (x1 == x2)
                        continue;

                    // Calculate Y on this foothold using linear interpolation
                    float fhY = (float)(y1 - y2) / (x1 - x2) * (x - x1) + y1;

                    if (first)
                    {
                        maxY = fhY;
                        result = fh;
                        if (maxY >= y)  // Foothold is at or below mob
                            first = false;
                    }
                    else
                    {
                        // Find closest foothold that is at or below mob position
                        if (fhY < maxY && fhY >= y)
                        {
                            result = fh;
                            maxY = fhY;
                        }
                    }
                }
            }

            return first ? null : result;
        }

        /// <summary>
        /// Find wall to the right of position (based on MapleNecrocer FindWallR)
        /// Used to detect collision when moving right
        /// </summary>
        /// <param name="x">X position to search from</param>
        /// <param name="y">Y position to search from</param>
        /// <returns>The wall foothold, or null if none found</returns>
        private FootholdLine FindWallR(float x, float y)
        {
            if (_allFootholds == null)
                return null;

            FootholdLine result = null;
            bool first = true;
            float maxX = 0;

            foreach (var fh in _allFootholds)
            {
                if (!fh.IsWall)
                    continue;

                int x1 = fh.FirstDot.X;
                int y1 = fh.FirstDot.Y;
                int y2 = fh.SecondDot.Y;

                // Ensure y1 <= y2 for range check
                if (y1 > y2)
                {
                    int temp = y1;
                    y1 = y2;
                    y2 = temp;
                }

                // Wall must be at or to the right of position, and Y must be within wall's vertical range
                if (x1 >= x && y >= y1 && y <= y2)
                {
                    if (first)
                    {
                        maxX = x1;
                        result = fh;
                        first = false;
                    }
                    else
                    {
                        // Find closest wall to the right
                        if (x1 < maxX)
                        {
                            maxX = x1;
                            result = fh;
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Find wall to the left of position (based on MapleNecrocer FindWallL)
        /// Used to detect collision when moving left
        /// </summary>
        /// <param name="x">X position to search from</param>
        /// <param name="y">Y position to search from</param>
        /// <returns>The wall foothold, or null if none found</returns>
        private FootholdLine FindWallL(float x, float y)
        {
            if (_allFootholds == null)
                return null;

            FootholdLine result = null;
            bool first = true;
            float maxX = 0;

            foreach (var fh in _allFootholds)
            {
                if (!fh.IsWall)
                    continue;

                int x1 = fh.FirstDot.X;
                int y1 = fh.FirstDot.Y;
                int y2 = fh.SecondDot.Y;

                // Ensure y1 <= y2 for range check
                if (y1 > y2)
                {
                    int temp = y1;
                    y1 = y2;
                    y2 = temp;
                }

                // Wall must be at or to the left of position, and Y must be within wall's vertical range
                if (x1 <= x && y >= y1 && y <= y2)
                {
                    if (first)
                    {
                        maxX = x1;
                        result = fh;
                        first = false;
                    }
                    else
                    {
                        // Find closest wall to the left
                        if (x1 > maxX)
                        {
                            maxX = x1;
                            result = fh;
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Find and set the foothold that contains the current position
        /// </summary>
        /// <param name="footholds">List of all footholds on the map</param>
        public void FindCurrentFoothold(IEnumerable<FootholdLine> footholds)
        {
            _allFootholds = footholds;

            // Calculate the expected foothold Y position
            // In WZ data: y = foothold Y, cy = mob display Y, yShift = cy - y
            // So: foothold Y = _spawnY - _yShift
            float expectedFootholdY = _spawnY - _yShift;

            // Use FindBelow to locate foothold beneath the expected position
            // Search from slightly above the expected foothold Y
            FootholdLine belowFH = FindBelow(X, expectedFootholdY - 5);

            if (belowFH != null)
            {
                CurrentFoothold = belowFH;
                // Snap mob Y to foothold (ground-based mobs stand on foothold)
                Y = CalculateYOnFoothold(belowFH, X);
                UpdatePlatformBoundaries();
                return;
            }

            // Fallback: search for any foothold within RX range at expected Y level
            float bestYDist = float.MaxValue;
            FootholdLine bestFH = null;

            foreach (var fh in footholds)
            {
                if (fh.IsWall)
                    continue;

                int minX = Math.Min(fh.FirstDot.X, fh.SecondDot.X);
                int maxX = Math.Max(fh.FirstDot.X, fh.SecondDot.X);

                // Check if foothold overlaps with mob's X position or RX range
                if (X >= minX && X <= maxX)
                {
                    float fhY = CalculateYOnFoothold(fh, X);
                    float yDist = Math.Abs(fhY - expectedFootholdY);

                    // Find foothold closest to expected Y position (within 100 pixel tolerance)
                    if (yDist < 100 && yDist < bestYDist)
                    {
                        bestYDist = yDist;
                        bestFH = fh;
                    }
                }
                // Also check if foothold is within RX range (wider search)
                else if (maxX >= RX0 && minX <= RX1)
                {
                    float clampedX = Math.Max(minX, Math.Min(maxX, X));
                    float fhY = CalculateYOnFoothold(fh, clampedX);
                    float yDist = Math.Abs(fhY - expectedFootholdY);

                    // Use larger tolerance for RX-range search
                    if (yDist < 150 && yDist < bestYDist)
                    {
                        bestYDist = yDist;
                        bestFH = fh;
                    }
                }
            }

            if (bestFH != null)
            {
                CurrentFoothold = bestFH;
                int minX = Math.Min(bestFH.FirstDot.X, bestFH.SecondDot.X);
                int maxX = Math.Max(bestFH.FirstDot.X, bestFH.SecondDot.X);
                X = Math.Max(minX, Math.Min(maxX, X));
                Y = CalculateYOnFoothold(bestFH, X);
                UpdatePlatformBoundaries();
            }
            else
            {
                // No foothold found - use spawn position
                CurrentFoothold = null;
                PlatformLeft = RX0;
                PlatformRight = RX1;
                X = _spawnX;
                Y = _spawnY;
            }
        }

        #region Foothold Navigation (MapleNecrocer Prev/Next style)

        /// <summary>
        /// Find the next connected foothold when reaching the edge of current foothold.
        /// Uses anchor connections for efficient linked navigation (MapleNecrocer style).
        /// </summary>
        /// <param name="direction">Movement direction (Left or Right)</param>
        /// <returns>The connected foothold, or null if at platform edge</returns>
        private FootholdLine FindNextConnectedFoothold(MobMoveDirection direction)
        {
            if (CurrentFoothold == null)
                return null;

            // Determine which end of the foothold we're at based on direction
            int x1 = CurrentFoothold.FirstDot.X;
            int x2 = CurrentFoothold.SecondDot.X;

            // Get the anchor at the edge we're walking towards
            HaCreator.MapEditor.Instance.Shapes.FootholdAnchor edgeAnchor;
            if (direction == MobMoveDirection.Left)
            {
                // Walking left - get the left-most anchor
                edgeAnchor = (x1 < x2)
                    ? CurrentFoothold.FirstDot as HaCreator.MapEditor.Instance.Shapes.FootholdAnchor
                    : CurrentFoothold.SecondDot as HaCreator.MapEditor.Instance.Shapes.FootholdAnchor;
            }
            else
            {
                // Walking right - get the right-most anchor
                edgeAnchor = (x1 > x2)
                    ? CurrentFoothold.FirstDot as HaCreator.MapEditor.Instance.Shapes.FootholdAnchor
                    : CurrentFoothold.SecondDot as HaCreator.MapEditor.Instance.Shapes.FootholdAnchor;
            }

            if (edgeAnchor == null)
                return null;

            // Search connected lines for a suitable continuation
            FootholdLine bestFh = null;
            float bestYDiff = float.MaxValue;

            foreach (var line in edgeAnchor.connectedLines)
            {
                if (line is FootholdLine connectedFh && connectedFh != CurrentFoothold && !connectedFh.IsWall)
                {
                    // Check if this foothold continues in our direction
                    int connX1 = connectedFh.FirstDot.X;
                    int connX2 = connectedFh.SecondDot.X;
                    int connMinX = Math.Min(connX1, connX2);
                    int connMaxX = Math.Max(connX1, connX2);

                    bool validDirection = (direction == MobMoveDirection.Left && connMinX < X) ||
                                         (direction == MobMoveDirection.Right && connMaxX > X);

                    if (validDirection)
                    {
                        // Calculate Y at connection point
                        float connY = CalculateYOnFoothold(connectedFh, X);
                        float yDiff = Math.Abs(connY - Y);

                        // Prefer footholds at similar height (within 50 pixels)
                        if (yDiff < 50 && yDiff < bestYDiff)
                        {
                            bestYDiff = yDiff;
                            bestFh = connectedFh;
                        }
                    }
                }
            }

            return bestFh;
        }

        /// <summary>
        /// Try to transition to a connected foothold. Returns true if successful.
        /// </summary>
        /// <param name="direction">Movement direction</param>
        /// <returns>True if transitioned to a new foothold, false if at platform edge</returns>
        private bool TryTransitionFoothold(MobMoveDirection direction)
        {
            FootholdLine nextFh = FindNextConnectedFoothold(direction);
            if (nextFh != null)
            {
                CurrentFoothold = nextFh;

                // Clamp X to new foothold bounds
                int fhMinX = Math.Min(nextFh.FirstDot.X, nextFh.SecondDot.X);
                int fhMaxX = Math.Max(nextFh.FirstDot.X, nextFh.SecondDot.X);
                if (X < fhMinX) X = fhMinX;
                if (X > fhMaxX) X = fhMaxX;

                // Update Y to match new foothold
                Y = CalculateYOnFoothold(nextFh, X);
                return true;
            }
            return false;
        }

        #endregion

        #region Angle Helper Methods (MapleNecrocer 256-unit circle)

        /// <summary>
        /// Cosine function using 256-unit circle (MapleNecrocer style)
        /// Angle 0 = right, 64 = up, 128 = left, 192 = down
        /// </summary>
        /// <param name="angle">Angle in 256-unit format (0-255)</param>
        /// <returns>Cosine value (-1.0 to 1.0)</returns>
        private static double Cos256(float angle)
        {
            return Math.Cos(angle * 2 * Math.PI / 256);
        }

        /// <summary>
        /// Sine function using 256-unit circle (MapleNecrocer style)
        /// </summary>
        /// <param name="angle">Angle in 256-unit format (0-255)</param>
        /// <returns>Sine value (-1.0 to 1.0)</returns>
        private static double Sin256(float angle)
        {
            return Math.Sin(angle * 2 * Math.PI / 256);
        }

        /// <summary>
        /// Calculate angle between two points in 256-unit format (MapleNecrocer GetAngle256)
        /// Used for calculating direction on slopes
        /// </summary>
        /// <param name="x1">Start X</param>
        /// <param name="y1">Start Y</param>
        /// <param name="x2">End X</param>
        /// <param name="y2">End Y</param>
        /// <returns>Angle in 256-unit format (0-255)</returns>
        private static int GetAngle256(float x1, float y1, float x2, float y2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;

            if (dx == 0 && dy == 0)
                return 0;

            // atan2 returns radians, convert to 256-unit
            double radians = Math.Atan2(dy, dx);
            int angle = (int)(radians * 256 / (2 * Math.PI));

            // Normalize to 0-255 range
            if (angle < 0)
                angle += 256;

            return angle % 256;
        }

        #endregion
    }
}
