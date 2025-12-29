using HaCreator.MapEditor.Instance.Shapes;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Objects.FieldObject
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
    /// </summary>
    public class MobMovementInfo
    {
        // Movement state
        public MobMoveDirection MoveDirection { get; set; } = MobMoveDirection.Left;
        public MobMoveType MoveType { get; set; } = MobMoveType.Move;

        // Position (dynamic, updates during simulation)
        public float X { get; set; }
        public float Y { get; set; }

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

        // Jump physics fields (from Map.wz/Physics.img - see MapPhysicsEditor.cs)
        // Official values: gravityAcc=2000 px/s², fallSpeed=670 px/s, jumpSpeed=555 px/s
        // Converted to per-tick at 60fps: /60 for velocity, /60/60 for acceleration
        public MobJumpState JumpState { get; set; } = MobJumpState.None;
        public float VelocityY { get; set; } = 0;           // Current vertical velocity (px/tick)
        public float GravityAcc { get; set; } = 0.556f;     // 2000 / 60 / 60 = 0.556 px/tick²
        public float JumpHeight { get; set; } = 9.25f;      // 555 / 60 = 9.25 px/tick (initial upward velocity)
        public float MaxFallSpeed { get; set; } = 11.17f;   // 670 / 60 = 11.17 px/tick (terminal velocity)
        private int _jumpCooldown = 0;                       // Time until next jump allowed
        private bool _isJumpingMob = false;                  // Whether this mob can jump

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
        public void Initialize(int x, int y, int rx0Shift, int rx1Shift, int yShift, bool isFlyingMob, bool isJumpingMob = false)
        {
            _spawnX = x;
            _spawnY = y;
            _yShift = yShift;
            X = x;
            Y = y;
            SrcY = y;
            _isFlyingMob = isFlyingMob;
            _isJumpingMob = isJumpingMob;

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

            // Random initial direction
            MoveDirection = _random.Next(2) == 0 ? MobMoveDirection.Left : MobMoveDirection.Right;
            FlipX = MoveDirection == MobMoveDirection.Right;

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

        /// <summary>
        /// Update mob movement based on elapsed time.
        /// Call this from MapSimulator.Update()
        /// </summary>
        /// <param name="deltaTimeMs">Time elapsed since last update in milliseconds</param>
        public void UpdateMovement(int deltaTimeMs)
        {
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
                FlipX = false;

                // Check left boundary
                if (X <= effectiveLeft)
                {
                    X = effectiveLeft;
                    MoveDirection = MobMoveDirection.Right;
                    FlipX = true;
                }
            }
            else if (MoveDirection == MobMoveDirection.Right)
            {
                X += flyMoveAmount;
                FlipX = true;

                // Check right boundary
                if (X >= effectiveRight)
                {
                    X = effectiveRight;
                    MoveDirection = MobMoveDirection.Left;
                    FlipX = false;
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
                    FlipX = false;
                }
                else if (MoveDirection == MobMoveDirection.Right)
                {
                    newX = X + airMoveAmount;
                    FlipX = true;
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
                FlipX = true;
            }
            else if (X > effectiveRight)
            {
                X = effectiveRight;
                MoveDirection = MobMoveDirection.Left;
                FlipX = false;
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
                    FlipX = MoveDirection == MobMoveDirection.Right;
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
                            FlipX = MoveDirection == MobMoveDirection.Right;
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
                        FlipX = true;
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
                        FlipX = false;
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
                    FlipX = true;
                }
                else
                {
                    if (x1 > x2)
                        direction = GetAngle256(x1, y1, x2, y2);
                    else
                        direction = GetAngle256(x2, y2, x1, y1);
                    FlipX = false;
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
                    FlipX = false;
                }
                else
                {
                    X += moveAmount;
                    FlipX = true;
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
                    FlipX = MoveDirection == MobMoveDirection.Right;
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
                FlipX = MoveDirection == MobMoveDirection.Right;
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
                    FlipX = MoveDirection == MobMoveDirection.Right;
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

            // Use RX bounds as primary boundaries
            int effectiveLeft = RX0;
            int effectiveRight = RX1;

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
                    FlipX = true;
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
                    FlipX = false;
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
                    FlipX = true;
                }
                else
                {
                    // Moving left: angle from right point to left point
                    if (x1 > x2)
                        direction = GetAngle256(x1, y1, x2, y2);
                    else
                        direction = GetAngle256(x2, y2, x1, y1);
                    FlipX = false;
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
                    FlipX = true;
                }
                else if (X >= effectiveRight)
                {
                    X = effectiveRight;
                    MoveDirection = MobMoveDirection.Left;
                    FlipX = false;
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
                            FlipX = true;
                        }
                        else
                        {
                            X = fhMaxX - 2;
                            MoveDirection = MobMoveDirection.Left;
                            FlipX = false;
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
                    FlipX = false;
                    if (X <= effectiveLeft)
                    {
                        X = effectiveLeft;
                        MoveDirection = MobMoveDirection.Right;
                        FlipX = true;
                    }
                }
                else
                {
                    X += moveAmount;
                    FlipX = true;
                    if (X >= effectiveRight)
                    {
                        X = effectiveRight;
                        MoveDirection = MobMoveDirection.Left;
                        FlipX = false;
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
