using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.Core;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Physics
{
    /// <summary>
    /// Vector Controller - Physics engine for entity movement.
    /// Based on MapleStory client's CVecCtrl class from IDA Pro analysis.
    ///
    /// Handles:
    /// - Foothold-based ground movement (CalcWalk, CollisionDetectWalk)
    /// - Ladder/rope climbing (GetLadderOrRope, IsOnLadder, IsOnRope)
    /// - Swimming physics (CalcFloat with swim mode)
    /// - Flying physics (CalcFloat with fly mode, IsUserFlying)
    /// - Knockback/impact (SetImpactNext, Impact)
    /// - Jump and fall physics (Jump, JustJump, FallDown)
    /// - Movement path generation (MakeMovePath, MakeContinuousMovePath)
    /// - State preservation (SaveFloatState*, BoundPosMapRange)
    /// </summary>
    public class CVecCtrl
    {
        #region Physics Constants (from Map.wz/Physics.img)

        // ============================================================
        // Physics values are loaded at runtime from Map.wz/Physics.img
        // See PhysicsConstants.cs for the loader
        // Access via PhysicsConstants.Instance
        // ============================================================

        /// <summary>
        /// Get gravity acceleration from Physics.img (px/s²)
        /// </summary>
        public static float GravityAcceleration => (float)PhysicsConstants.Instance.GravityAcc;

        /// <summary>
        /// Get jump velocity from Physics.img (px/s)
        /// </summary>
        public static float JumpVelocity => (float)PhysicsConstants.Instance.JumpSpeed;

        /// <summary>
        /// Get terminal velocity from Physics.img (px/s)
        /// </summary>
        public static float TerminalVelocity => (float)PhysicsConstants.Instance.FallSpeed;

        /// <summary>
        /// Get default walk speed from Physics.img (px/s)
        /// </summary>
        public static float DefaultWalkSpeed => (float)PhysicsConstants.Instance.WalkSpeed;

        /// <summary>
        /// Get walk acceleration from Physics.img (px/s²)
        /// Calculated as WalkForce / DefaultMass
        /// </summary>
        public static float WalkAcceleration => (float)PhysicsConstants.Instance.WalkAcceleration;

        /// <summary>
        /// Get walk deceleration from Physics.img (px/s²)
        /// Calculated as WalkDrag / DefaultMass
        /// </summary>
        public static float WalkDeceleration => (float)PhysicsConstants.Instance.WalkDeceleration;

        /// <summary>
        /// Get swim speed multiplier from Physics.img
        /// </summary>
        public static float SwimSpeedFactor => (float)PhysicsConstants.Instance.SwimSpeedMultiplier;

        /// <summary>
        /// Ladder/rope climbing speed (px/s)
        /// Approximately 60% of walk speed
        /// </summary>
        public static float ClimbSpeed => DefaultWalkSpeed * 0.6f;

        /// <summary>
        /// Flying speed multiplier for airborne mobs
        /// </summary>
        public const float FlySpeedMultiplier = 1.5f;

        /// <summary>
        /// Air drag deceleration (px/s²)
        /// Based on floatDrag1 / DefaultMass
        /// </summary>
        public static float AirDragDeceleration => (float)(PhysicsConstants.Instance.FloatDrag1 / PhysicsConstants.Instance.DefaultMass);

        // Backward-compatible constants for mob physics (per-tick multipliers at 60fps)
        // These will be deprecated when mob physics is updated to use time-based physics

        /// <summary>
        /// Air drag factor per tick (backward compatibility for mob physics)
        /// </summary>
        public const float AirDrag = 0.98f;

        /// <summary>
        /// Ground friction factor per tick (backward compatibility for mob physics)
        /// </summary>
        public const float GroundFriction = 0.85f;

        #endregion

        #region Official Client Physics Functions (AccSpeed/DecSpeed)

        /// <summary>
        /// Accelerate towards maximum speed (official client formula).
        /// Formula: v += (force / mass) * tSec, clamped to vMax
        /// </summary>
        /// <param name="v">Current velocity (ref, will be modified)</param>
        /// <param name="force">Acceleration force (walkForce * multipliers)</param>
        /// <param name="mass">Entity mass (usually 100.0)</param>
        /// <param name="vMax">Maximum speed (absolute value)</param>
        /// <param name="tSec">Time in seconds</param>
        public static void AccSpeed(ref double v, double force, double mass, double vMax, double tSec)
        {
            if (vMax < 0.0) return;

            if (force > 0.0 && vMax > v)
            {
                // Accelerating right
                v += (force / mass) * tSec;
                if (v > vMax) v = vMax;
            }
            else if (force <= 0.0 && -vMax < v)
            {
                // Accelerating left (force is negative)
                v += (force / mass) * tSec;
                if (v < -vMax) v = -vMax;
            }
        }

        /// <summary>
        /// Decelerate towards zero or target speed (official client formula).
        /// Formula: v -= (drag / mass) * tSec towards vMax (or 0 if vMax is 0)
        /// </summary>
        /// <param name="v">Current velocity (ref, will be modified)</param>
        /// <param name="drag">Deceleration force (walkDrag * multipliers)</param>
        /// <param name="mass">Entity mass (usually 100.0)</param>
        /// <param name="vMax">Target speed to decelerate to (usually 0)</param>
        /// <param name="tSec">Time in seconds</param>
        public static void DecSpeed(ref double v, double drag, double mass, double vMax, double tSec)
        {
            if (vMax < 0.0) return;

            if (vMax < v)
            {
                // Moving right faster than target - slow down
                v -= (drag / mass) * tSec;
                if (v < vMax) v = vMax;
            }
            else if (-vMax > v)
            {
                // Moving left faster than target - slow down (add towards 0)
                v += (drag / mass) * tSec;
                if (v > -vMax) v = -vMax;
            }
        }

        #endregion

        #region Position and Velocity (m_ap)

        /// <summary>
        /// Current X position
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// Current Y position
        /// </summary>
        public double Y { get; set; }

        /// <summary>
        /// Horizontal velocity
        /// </summary>
        public double VelocityX { get; set; }

        /// <summary>
        /// Vertical velocity (positive = down)
        /// </summary>
        public double VelocityY { get; set; }

        #endregion

        #region Foothold State (m_pfh, m_pfhFallStart)

        /// <summary>
        /// Current foothold the entity is standing on (null if airborne)
        /// </summary>
        public FootholdLine CurrentFoothold { get; set; }

        /// <summary>
        /// Foothold where the entity started falling from
        /// </summary>
        public FootholdLine FallStartFoothold { get; set; }

        #endregion

        #region Ladder/Rope State (m_pLadderOrRope)

        /// <summary>
        /// Whether currently on a ladder or rope
        /// </summary>
        public bool IsOnLadderOrRope { get; set; }

        /// <summary>
        /// True = ladder, False = rope
        /// </summary>
        public bool IsLadder { get; set; }

        /// <summary>
        /// Ladder/rope X position (center)
        /// </summary>
        public int LadderX { get; set; }

        /// <summary>
        /// Ladder/rope Y range (top)
        /// </summary>
        public int LadderTop { get; set; }

        /// <summary>
        /// Ladder/rope Y range (bottom)
        /// </summary>
        public int LadderBottom { get; set; }

        #endregion

        #region Movement State (m_nMoveAction)

        /// <summary>
        /// Current movement action/state
        /// </summary>
        public MoveAction CurrentAction { get; set; } = MoveAction.Stand;

        /// <summary>
        /// Direction facing (true = right, false = left)
        /// </summary>
        public bool FacingRight { get; set; } = true;

        /// <summary>
        /// Whether the entity can flip direction
        /// </summary>
        public bool NoFlip { get; set; } = false;

        #endregion

        #region Impact/Knockback State (m_impactNext)

        /// <summary>
        /// Pending knockback X velocity
        /// </summary>
        public double ImpactVelocityX { get; private set; }

        /// <summary>
        /// Pending knockback Y velocity
        /// </summary>
        public double ImpactVelocityY { get; private set; }

        /// <summary>
        /// Whether there is pending knockback
        /// </summary>
        public bool HasPendingImpact { get; private set; }

        /// <summary>
        /// Whether currently in knockback state (prevents falling off platform)
        /// </summary>
        public bool IsInKnockback { get; private set; }

        /// <summary>
        /// Minimum X position during knockback (foothold left edge)
        /// </summary>
        public double KnockbackMinX { get; private set; }

        /// <summary>
        /// Maximum X position during knockback (foothold right edge)
        /// </summary>
        public double KnockbackMaxX { get; private set; }

        /// <summary>
        /// Duration of knockback state in seconds (auto-clears after this)
        /// </summary>
        private float _knockbackTimeRemaining;

        /// <summary>
        /// Maximum knockback duration in seconds
        /// </summary>
        private const float MaxKnockbackTime = 0.5f;

        #endregion

        #region Special States

        /// <summary>
        /// Whether currently in swimming area
        /// </summary>
        public bool IsInSwimArea { get; set; }

        /// <summary>
        /// Whether wings effect is active (float down slowly)
        /// </summary>
        public bool WingsActive { get; set; }

        /// <summary>
        /// Jump state for animation control
        /// </summary>
        public JumpState CurrentJumpState { get; set; } = JumpState.None;

        /// <summary>
        /// Whether currently in a jump-down (falling through platform).
        /// Used to distinguish from normal jumps for landing detection.
        /// </summary>
        public bool IsJumpingDown { get; set; }

        #endregion

        #region Flying State (m_bFlying, IsUserFlying)

        /// <summary>
        /// Whether currently in flying mode (flying mount, skill, or flying map)
        /// </summary>
        public bool IsFlying { get; set; }

        /// <summary>
        /// Flying map flag (some maps allow free flying)
        /// </summary>
        public bool IsFlyingMap { get; set; }

        /// <summary>
        /// Flying skill/mount active
        /// </summary>
        public bool HasFlyingAbility { get; set; }

        #endregion

        #region Float State Preservation (SaveFloatState*)

        /// <summary>
        /// Saved X position before collision detection
        /// </summary>
        private double _savedX;

        /// <summary>
        /// Saved Y position before collision detection
        /// </summary>
        private double _savedY;

        /// <summary>
        /// Saved X velocity before collision detection
        /// </summary>
        private double _savedVelocityX;

        /// <summary>
        /// Saved Y velocity before collision detection
        /// </summary>
        private double _savedVelocityY;

        /// <summary>
        /// Whether float state has been saved
        /// </summary>
        private bool _floatStateSaved;

        #endregion

        #region Map Bounds (for BoundPosMapRange)

        /// <summary>
        /// Map left boundary
        /// </summary>
        public int MapLeft { get; set; } = int.MinValue;

        /// <summary>
        /// Map right boundary
        /// </summary>
        public int MapRight { get; set; } = int.MaxValue;

        /// <summary>
        /// Map top boundary
        /// </summary>
        public int MapTop { get; set; } = int.MinValue;

        /// <summary>
        /// Map bottom boundary
        /// </summary>
        public int MapBottom { get; set; } = int.MaxValue;

        #endregion

        #region Movement Path (CMovePath integration)

        /// <summary>
        /// Movement path elements for network sync
        /// </summary>
        private readonly List<MovePathElement> _movePath = new List<MovePathElement>();

        /// <summary>
        /// Whether movement path recording is active
        /// </summary>
        public bool IsRecordingPath { get; set; }

        /// <summary>
        /// Last time path was flushed (for network sync timing)
        /// </summary>
        private int _lastPathFlushTime;

        /// <summary>
        /// Path flush interval in milliseconds
        /// </summary>
        private const int PathFlushInterval = 100;

        #endregion

        #region State Query Methods (matching client)

        /// <summary>
        /// Checks if entity is standing on a foothold.
        /// Client: return this->m_pfh != 0;
        /// </summary>
        public bool IsOnFoothold()
        {
            return CurrentFoothold != null;
        }

        /// <summary>
        /// Checks if entity is on a ladder.
        /// Client checks m_pLadderOrRope && bLadder flag
        /// </summary>
        public bool IsOnLadder()
        {
            return IsOnLadderOrRope && IsLadder;
        }

        /// <summary>
        /// Checks if entity is on a rope.
        /// </summary>
        public bool IsOnRope()
        {
            return IsOnLadderOrRope && !IsLadder;
        }

        /// <summary>
        /// Checks if entity has stopped moving.
        /// Client: return vx == 0 && vy == 0;
        /// </summary>
        public bool IsStopped()
        {
            return Math.Abs(VelocityX) < 0.001 && Math.Abs(VelocityY) < 0.001;
        }

        /// <summary>
        /// Checks if entity is swimming.
        /// Client checks map type or swim area inclusion.
        /// </summary>
        public bool IsSwimming()
        {
            return IsInSwimArea;
        }

        /// <summary>
        /// Checks if entity is airborne (jumping or falling)
        /// </summary>
        public bool IsAirborne()
        {
            return !IsOnFoothold() && !IsOnLadderOrRope;
        }

        /// <summary>
        /// Checks if entity is falling (moving downward while airborne).
        /// Client: return m_pfh == 0 && vy > 0;
        /// </summary>
        public bool IsFalling()
        {
            return !IsOnFoothold() && VelocityY > 0;
        }

        /// <summary>
        /// Checks if entity is free falling (no control, not on ladder).
        /// Used by client for fall damage calculation.
        /// </summary>
        public bool IsFreeFalling()
        {
            return !IsOnFoothold() && !IsOnLadderOrRope && !IsFlying && VelocityY > 0;
        }

        /// <summary>
        /// Checks if user is flying (flying mount, skill, or flying map).
        /// Client: CVecCtrlUser::IsUserFlying
        /// </summary>
        public bool IsUserFlying()
        {
            return IsFlying || IsFlyingMap || HasFlyingAbility;
        }

        /// <summary>
        /// Get the current foothold the entity is standing on.
        /// Client: CVecCtrl::GetFoothold
        /// </summary>
        public FootholdLine GetFoothold()
        {
            return CurrentFoothold;
        }

        /// <summary>
        /// Get ladder or rope at the specified position.
        /// Client: CVecCtrl::GetLadderOrRope
        /// This is a stub - actual implementation needs foothold/rope data.
        /// </summary>
        /// <param name="x">X position</param>
        /// <param name="y">Y position</param>
        /// <param name="isLadder">Output: true if ladder, false if rope</param>
        /// <returns>True if ladder/rope found at position</returns>
        public bool GetLadderOrRope(double x, double y, out bool isLadder)
        {
            isLadder = false;
            // Stub - would need rope/ladder data from map
            return false;
        }

        #endregion

        #region Movement Methods

        /// <summary>
        /// Apply knockback/impact velocity.
        /// Based on CVecCtrl::SetImpactNext - accumulates impact with clamping.
        /// </summary>
        /// <param name="vx">Horizontal knockback velocity</param>
        /// <param name="vy">Vertical knockback velocity</param>
        public void SetImpactNext(double vx, double vy)
        {
            WingsActive = false; // Disable wings on impact

            if (!HasPendingImpact)
            {
                ImpactVelocityX = 0;
                ImpactVelocityY = 0;
            }

            HasPendingImpact = true;

            // Accumulate horizontal impact with clamping (from client)
            if (vx < 0 && vx < ImpactVelocityX)
            {
                double combined = vx + ImpactVelocityX;
                ImpactVelocityX = combined < vx ? vx : combined;
            }
            else if (vx > 0 && vx > ImpactVelocityX)
            {
                double combined = vx + ImpactVelocityX;
                ImpactVelocityX = combined > vx ? vx : combined;
            }

            // Accumulate vertical impact with clamping
            if (vy < 0 && vy < ImpactVelocityY)
            {
                double combined = vy + ImpactVelocityY;
                ImpactVelocityY = combined < vy ? vy : combined;
            }
            else if (vy > 0 && vy > ImpactVelocityY)
            {
                double combined = vy + ImpactVelocityY;
                ImpactVelocityY = combined > vy ? vy : combined;
            }
        }

        /// <summary>
        /// Apply pending impact to velocity and clear it
        /// </summary>
        public void ApplyPendingImpact()
        {
            if (HasPendingImpact)
            {
                VelocityX += ImpactVelocityX;
                VelocityY += ImpactVelocityY;

                // Set up knockback bounds from current foothold to prevent falling off
                if (CurrentFoothold != null)
                {
                    // Record the foothold bounds for knockback clamping
                    KnockbackMinX = Math.Min(CurrentFoothold.FirstDot.X, CurrentFoothold.SecondDot.X);
                    KnockbackMaxX = Math.Max(CurrentFoothold.FirstDot.X, CurrentFoothold.SecondDot.X);
                    IsInKnockback = true;
                    _knockbackTimeRemaining = MaxKnockbackTime;
                }

                ImpactVelocityX = 0;
                ImpactVelocityY = 0;
                HasPendingImpact = false;

                // Leave foothold if knocked back vertically
                if (CurrentFoothold != null && Math.Abs(VelocityY) > 0.1)
                {
                    FallStartFoothold = CurrentFoothold;
                    CurrentFoothold = null;
                    CurrentJumpState = JumpState.Falling;
                }
            }
        }

        /// <summary>
        /// Initiate a jump
        /// </summary>
        public void Jump()
        {
            if (IsOnFoothold() || IsOnLadderOrRope)
            {
                VelocityY = -JumpVelocity;
                FallStartFoothold = CurrentFoothold;
                CurrentFoothold = null;
                IsOnLadderOrRope = false;
                CurrentJumpState = JumpState.Jumping;
                CurrentAction = MoveAction.Jump;

                // Clear knockback state on voluntary jump (player can move freely)
                IsInKnockback = false;
                _knockbackTimeRemaining = 0;
            }
        }

        /// <summary>
        /// Initiate a jump down (fall through platform).
        /// Player presses Down + Jump to fall through a platform.
        /// Based on CVecCtrl behavior in official client.
        /// </summary>
        /// <returns>True if jump down was initiated, false if not possible</returns>
        public bool JumpDown()
        {
            if (!IsOnFoothold())
                return false;

            // Record the foothold we're jumping down from
            // The landing detection will skip this foothold until we pass it
            FallStartFoothold = CurrentFoothold;
            CurrentFoothold = null;

            // Mark as jump-down so landing detection knows to fall through
            IsJumpingDown = true;

            // Small downward velocity to start falling (not a full jump)
            VelocityY = 50f; // Small initial downward velocity (50 px/s)

            CurrentJumpState = JumpState.Falling;
            CurrentAction = MoveAction.Fall;

            // Clear knockback state since this is a voluntary action
            IsInKnockback = false;
            _knockbackTimeRemaining = 0;

            return true;
        }

        /// <summary>
        /// Update physics for one tick
        /// </summary>
        /// <param name="deltaTime">Time since last update in seconds</param>
        public void Update(float deltaTime)
        {
            // deltaTime is in seconds - same as tSec in official client
            // Official formula: pos += (v_old + v_new) * 0.5 * tSec

            // Apply pending knockback
            ApplyPendingImpact();

            if (IsOnLadderOrRope)
            {
                UpdateLadderMovement(deltaTime);
            }
            else if (IsOnFoothold())
            {
                UpdateGroundMovement(deltaTime);
            }
            else
            {
                UpdateAirMovement(deltaTime);
            }
        }

        private void UpdateGroundMovement(float deltaTime)
        {
            // Note: Ground friction is now handled by PlayerCharacter using DecSpeed formula
            // VelocityX is set directly by the player input processing

            // Move along foothold using velocity (px/s) * time (s)
            X += VelocityX * deltaTime;

            // Apply knockback bounds clamping on ground
            if (IsInKnockback && CurrentFoothold != null)
            {
                // Clamp X position to foothold bounds during knockback
                if (X < KnockbackMinX)
                {
                    X = KnockbackMinX;
                    VelocityX = 0;
                }
                else if (X > KnockbackMaxX)
                {
                    X = KnockbackMaxX;
                    VelocityX = 0;
                }

                // Decrement knockback timer based on time
                _knockbackTimeRemaining -= deltaTime;
                if (_knockbackTimeRemaining <= 0)
                {
                    IsInKnockback = false;
                }
            }
            // Note: Normal movement does NOT clamp to foothold bounds here.
            // The foothold lookup callback in PlayerCharacter handles finding
            // adjacent footholds when walking. Footholds are connected and
            // players transition between them naturally.

            // Update Y to follow foothold slope
            if (CurrentFoothold != null)
            {
                Y = CalculateYOnFoothold(CurrentFoothold, X);
            }

            CurrentAction = Math.Abs(VelocityX) > 5 ? MoveAction.Walk : MoveAction.Stand; // 5 px/s threshold
            CurrentJumpState = JumpState.None;
        }

        private void UpdateAirMovement(float deltaTime)
        {
            // Check for special float modes (flying/swimming)
            if (IsUserFlying() || IsInSwimArea)
            {
                UpdateFloatMovement(deltaTime);
                return;
            }

            // Apply gravity: v += g * t (official formula)
            float gravity = GravityAcceleration;
            if (WingsActive)
                gravity *= 0.3f; // Reduced gravity with wings

            VelocityY += gravity * deltaTime;

            // Clamp to terminal velocity (px/s)
            if (VelocityY > TerminalVelocity)
                VelocityY = TerminalVelocity;

            // Apply air drag deceleration to horizontal movement
            // DecSpeed formula: v -= (drag / mass) * t
            if (VelocityX > 0)
            {
                VelocityX -= AirDragDeceleration * deltaTime;
                if (VelocityX < 0) VelocityX = 0;
            }
            else if (VelocityX < 0)
            {
                VelocityX += AirDragDeceleration * deltaTime;
                if (VelocityX > 0) VelocityX = 0;
            }

            // Update position: pos += v * t
            X += VelocityX * deltaTime;
            Y += VelocityY * deltaTime;

            // Apply knockback bounds clamping to prevent falling off platform
            if (IsInKnockback)
            {
                // Clamp X position to foothold bounds
                if (X < KnockbackMinX)
                {
                    X = KnockbackMinX;
                    VelocityX = 0; // Stop horizontal movement at edge
                }
                else if (X > KnockbackMaxX)
                {
                    X = KnockbackMaxX;
                    VelocityX = 0; // Stop horizontal movement at edge
                }

                // Decrement knockback timer
                _knockbackTimeRemaining -= deltaTime;
                if (_knockbackTimeRemaining <= 0)
                {
                    IsInKnockback = false;
                }
            }

            // Update jump state
            if (VelocityY < 0)
                CurrentJumpState = JumpState.Jumping;
            else
                CurrentJumpState = JumpState.Falling;
        }

        private void UpdateLadderMovement(float deltaTime)
        {
            // Clamp to ladder X
            X = LadderX;

            // Move vertically: pos += v * t
            Y += VelocityY * deltaTime;

            // Clamp to ladder bounds
            if (Y < LadderTop)
            {
                Y = LadderTop;
                VelocityY = 0;
            }
            else if (Y > LadderBottom)
            {
                Y = LadderBottom;
                VelocityY = 0;
            }

            CurrentAction = Math.Abs(VelocityY) > 5 ? MoveAction.Ladder : MoveAction.Stand; // 5 px/s threshold
        }

        /// <summary>
        /// Update float movement (swimming/flying).
        /// Uses CalcFloat with appropriate parameters.
        /// </summary>
        private void UpdateFloatMovement(float deltaTime)
        {
            // Determine float mode
            FloatMode mode = FloatMode.Normal;
            double gravityFactor = 1.0;
            double maxSpeed, force, drag;

            if (IsUserFlying())
            {
                mode = FloatMode.Flying;
                gravityFactor = 0.0; // No gravity when flying
                maxSpeed = (float)PhysicsConstants.Instance.FlySpeed;
                force = PhysicsConstants.Instance.FlyForce;
                drag = PhysicsConstants.Instance.FloatDrag1;
            }
            else if (IsInSwimArea)
            {
                mode = FloatMode.Swimming;
                gravityFactor = 0.3; // Reduced gravity in water
                maxSpeed = (float)PhysicsConstants.Instance.SwimSpeed;
                force = PhysicsConstants.Instance.SwimForce;
                drag = PhysicsConstants.Instance.FloatDrag2;
            }
            else if (WingsActive)
            {
                mode = FloatMode.Wings;
                gravityFactor = 0.3; // Slow fall with wings
                maxSpeed = DefaultWalkSpeed;
                force = PhysicsConstants.Instance.WalkForce;
                drag = PhysicsConstants.Instance.FloatDrag1;
            }
            else
            {
                // Normal air - use standard physics
                maxSpeed = DefaultWalkSpeed;
                force = PhysicsConstants.Instance.WalkForce;
                drag = PhysicsConstants.Instance.FloatDrag1;
            }

            double mass = PhysicsConstants.Instance.DefaultMass;

            // No input - this is for passive float physics (mobs, NPCs)
            // Player input is handled separately in PlayerCharacter.ProcessFloatMovement
            int inputX = 0;
            int inputY = 0;

            // Save state before collision
            SaveFloatStateBeforeCollision();

            // Apply float physics
            CalcFloat(inputX, inputY, maxSpeed, force, drag, mass, gravityFactor, deltaTime);

            // Update position
            double newX = X + VelocityX * deltaTime;
            double newY = Y + VelocityY * deltaTime;

            // Collision detection
            if (CollisionDetectFloat(newX, newY, out double collidedX, out double collidedY))
            {
                X = collidedX;
                Y = collidedY;
                // Zero velocity on collision
                if (collidedX != newX) VelocityX = 0;
                if (collidedY != newY) VelocityY = 0;
            }
            else
            {
                X = newX;
                Y = newY;
            }

            // Save state after collision
            SaveFloatStateAfterCollision();

            // Bound to map
            BoundPosMapRange();

            // Update action state
            if (mode == FloatMode.Flying)
                CurrentAction = MoveAction.Fly;
            else if (mode == FloatMode.Swimming)
                CurrentAction = MoveAction.Swim;
            else
                CurrentAction = VelocityY < 0 ? MoveAction.Jump : MoveAction.Fall;

            // Update jump state
            CurrentJumpState = VelocityY < 0 ? JumpState.Jumping : JumpState.Falling;
        }

        /// <summary>
        /// Calculate Y position on a foothold given X
        /// </summary>
        private double CalculateYOnFoothold(FootholdLine fh, double x)
        {
            if (fh == null) return Y;

            // Linear interpolation along foothold
            double x1 = fh.FirstDot.X;
            double y1 = fh.FirstDot.Y;
            double x2 = fh.SecondDot.X;
            double y2 = fh.SecondDot.Y;

            // Handle vertical footholds
            if (Math.Abs(x2 - x1) < 0.001)
                return y1;

            double t = (x - x1) / (x2 - x1);
            t = Math.Max(0, Math.Min(1, t)); // Clamp to [0,1]

            return y1 + t * (y2 - y1);
        }

        /// <summary>
        /// Land on a foothold
        /// </summary>
        /// <param name="fh">Foothold to land on</param>
        public void LandOnFoothold(FootholdLine fh)
        {
            CurrentFoothold = fh;
            FallStartFoothold = null;
            VelocityY = 0;
            CurrentJumpState = JumpState.None;
            IsOnLadderOrRope = false;

            // Clear jump-down state when landing
            IsJumpingDown = false;

            // Clear knockback state when landing
            IsInKnockback = false;
            _knockbackTimeRemaining = 0;

            if (fh != null)
            {
                Y = CalculateYOnFoothold(fh, X);
            }
        }

        /// <summary>
        /// Start climbing a ladder or rope
        /// </summary>
        public void GrabLadder(int x, int top, int bottom, bool isLadder)
        {
            IsOnLadderOrRope = true;
            IsLadder = isLadder;
            LadderX = x;
            LadderTop = top;
            LadderBottom = bottom;
            CurrentFoothold = null;
            VelocityX = 0;
            CurrentAction = MoveAction.Ladder;

            // Clear knockback state when grabbing ladder
            IsInKnockback = false;
            _knockbackTimeRemaining = 0;
        }

        /// <summary>
        /// Clear knockback state (e.g., when action is interrupted)
        /// </summary>
        public void ClearKnockback()
        {
            IsInKnockback = false;
            KnockbackMinX = 0;
            KnockbackMaxX = 0;
            _knockbackTimeRemaining = 0;
        }

        /// <summary>
        /// Release from ladder/rope
        /// </summary>
        public void ReleaseLadder()
        {
            IsOnLadderOrRope = false;
            CurrentJumpState = JumpState.Falling;
        }

        /// <summary>
        /// Detach from current foothold (knockback/forced fall).
        /// Client: CVecCtrl::DetachFromFoothold
        /// </summary>
        public void DetachFromFoothold()
        {
            if (CurrentFoothold != null)
            {
                FallStartFoothold = CurrentFoothold;
                CurrentFoothold = null;
                CurrentJumpState = JumpState.Falling;
                CurrentAction = MoveAction.Fall;
            }
        }

        /// <summary>
        /// Force fall down from current position.
        /// Client: CVecCtrl::FallDown
        /// </summary>
        /// <param name="initialVelocityY">Optional initial downward velocity</param>
        public void FallDown(double initialVelocityY = 0)
        {
            DetachFromFoothold();
            if (initialVelocityY > 0)
            {
                VelocityY = initialVelocityY;
            }
        }

        /// <summary>
        /// Apply impact force (knockback from damage).
        /// Client: CVecCtrl::Impact - applies immediate velocity change.
        /// </summary>
        /// <param name="vx">Horizontal impact velocity</param>
        /// <param name="vy">Vertical impact velocity</param>
        public void Impact(double vx, double vy)
        {
            // Impact applies immediately (unlike SetImpactNext which queues)
            VelocityX = vx;
            VelocityY = vy;

            WingsActive = false; // Disable wings on impact

            // Detach from foothold if vertical impact
            if (vy < 0 && CurrentFoothold != null)
            {
                DetachFromFoothold();
            }

            // Set up knockback bounds
            if (CurrentFoothold != null)
            {
                KnockbackMinX = Math.Min(CurrentFoothold.FirstDot.X, CurrentFoothold.SecondDot.X);
                KnockbackMaxX = Math.Max(CurrentFoothold.FirstDot.X, CurrentFoothold.SecondDot.X);
                IsInKnockback = true;
                _knockbackTimeRemaining = MaxKnockbackTime;
            }
        }

        #endregion

        #region Official Client Physics Formulas (CalcWalk, CalcFloat)

        /// <summary>
        /// Calculate walk physics using official client formula.
        /// Client: CVecCtrl::CalcWalk
        ///
        /// Formula:
        ///   If input direction matches velocity: AccSpeed (accelerate)
        ///   If no input: DecSpeed (decelerate to 0)
        ///   If input opposite to velocity: DecSpeed then AccSpeed
        /// </summary>
        /// <param name="inputDirection">-1 = left, 0 = none, 1 = right</param>
        /// <param name="maxSpeed">Maximum walk speed (from character stats)</param>
        /// <param name="force">Walk force (from Physics.img * shoe attr)</param>
        /// <param name="drag">Walk drag (from Physics.img * shoe attr)</param>
        /// <param name="mass">Entity mass (usually 100)</param>
        /// <param name="tSec">Time in seconds</param>
        public void CalcWalk(int inputDirection, double maxSpeed, double force, double drag, double mass, double tSec)
        {
            double v = VelocityX;

            if (inputDirection == 0)
            {
                // No input - decelerate to 0
                DecSpeed(ref v, drag, mass, 0, tSec);
            }
            else if (inputDirection > 0)
            {
                // Moving right
                if (v >= 0)
                {
                    // Already moving right or stopped - accelerate right
                    AccSpeed(ref v, force, mass, maxSpeed, tSec);
                }
                else
                {
                    // Moving left - decelerate first, then accelerate right
                    DecSpeed(ref v, drag, mass, 0, tSec);
                    if (v >= 0)
                    {
                        AccSpeed(ref v, force, mass, maxSpeed, tSec);
                    }
                }
            }
            else
            {
                // Moving left (inputDirection < 0)
                if (v <= 0)
                {
                    // Already moving left or stopped - accelerate left
                    AccSpeed(ref v, -force, mass, maxSpeed, tSec);
                }
                else
                {
                    // Moving right - decelerate first, then accelerate left
                    DecSpeed(ref v, drag, mass, 0, tSec);
                    if (v <= 0)
                    {
                        AccSpeed(ref v, -force, mass, maxSpeed, tSec);
                    }
                }
            }

            VelocityX = v;
        }

        /// <summary>
        /// Calculate float physics (swimming/flying) using official client formula.
        /// Client: CVecCtrl::CalcFloat
        ///
        /// Handles both X and Y movement in fluid/air with drag.
        /// </summary>
        /// <param name="inputX">-1 = left, 0 = none, 1 = right</param>
        /// <param name="inputY">-1 = up, 0 = none, 1 = down</param>
        /// <param name="maxSpeed">Maximum float speed</param>
        /// <param name="force">Float force</param>
        /// <param name="drag">Float drag</param>
        /// <param name="mass">Entity mass</param>
        /// <param name="gravityFactor">Gravity multiplier (0 for flying, 0.5 for swimming)</param>
        /// <param name="tSec">Time in seconds</param>
        public void CalcFloat(int inputX, int inputY, double maxSpeed, double force, double drag,
            double mass, double gravityFactor, double tSec)
        {
            double vx = VelocityX;
            double vy = VelocityY;

            // Horizontal movement
            if (inputX == 0)
            {
                DecSpeed(ref vx, drag, mass, 0, tSec);
            }
            else if (inputX > 0)
            {
                if (vx >= 0)
                    AccSpeed(ref vx, force, mass, maxSpeed, tSec);
                else
                {
                    DecSpeed(ref vx, drag, mass, 0, tSec);
                    if (vx >= 0) AccSpeed(ref vx, force, mass, maxSpeed, tSec);
                }
            }
            else
            {
                if (vx <= 0)
                    AccSpeed(ref vx, -force, mass, maxSpeed, tSec);
                else
                {
                    DecSpeed(ref vx, drag, mass, 0, tSec);
                    if (vx <= 0) AccSpeed(ref vx, -force, mass, maxSpeed, tSec);
                }
            }

            // Vertical movement for swimming - simple and direct
            // Max speeds: 30% up, 150% down
            double maxUpSpeed = maxSpeed * 0.3;   // ~132 for swim
            double maxDownSpeed = maxSpeed * 1.5; // ~660 for swim
            double accel = maxSpeed * 3.0 * tSec; // Acceleration per frame

            if (inputY == 0)
            {
                // No input - sink due to gravity, apply drag
                vy += accel * 0.3; // Gentle sink acceleration
                // Apply drag to slow down
                if (vy > 0) vy *= (1.0 - tSec * 2.0);
                if (vy < 0) vy *= (1.0 - tSec * 2.0);
                // Clamp sink speed
                if (vy > maxSpeed * 0.3) vy = maxSpeed * 0.3;
            }
            else if (inputY > 0)
            {
                // Pressing DOWN - accelerate downward
                vy += accel;
                if (vy > maxDownSpeed) vy = maxDownSpeed;
            }
            else
            {
                // Pressing UP - accelerate upward
                vy -= accel;
                if (vy < -maxUpSpeed) vy = -maxUpSpeed;
            }

            VelocityX = vx;
            VelocityY = vy;
        }

        /// <summary>
        /// Collision detection for walking on footholds.
        /// Client: CVecCtrl::CollisionDetectWalk
        /// Returns true if collision occurred.
        /// </summary>
        /// <param name="newX">New X position after movement</param>
        /// <param name="newY">New Y position after movement</param>
        /// <param name="collidedX">Output: adjusted X after collision</param>
        /// <param name="collidedY">Output: adjusted Y after collision</param>
        /// <returns>True if collision occurred</returns>
        public bool CollisionDetectWalk(double newX, double newY, out double collidedX, out double collidedY)
        {
            collidedX = newX;
            collidedY = newY;

            if (CurrentFoothold == null)
                return false;

            // Clamp to foothold X bounds (for knockback)
            if (IsInKnockback)
            {
                if (newX < KnockbackMinX)
                {
                    collidedX = KnockbackMinX;
                    return true;
                }
                if (newX > KnockbackMaxX)
                {
                    collidedX = KnockbackMaxX;
                    return true;
                }
            }

            // Calculate Y on foothold
            collidedY = CalculateYOnFoothold(CurrentFoothold, collidedX);
            return false;
        }

        /// <summary>
        /// Collision detection for floating (swimming/flying).
        /// Client: CVecCtrl::CollisionDetectFloat
        /// Returns true if collision occurred.
        /// </summary>
        /// <param name="newX">New X position after movement</param>
        /// <param name="newY">New Y position after movement</param>
        /// <param name="collidedX">Output: adjusted X after collision</param>
        /// <param name="collidedY">Output: adjusted Y after collision</param>
        /// <returns>True if collision occurred</returns>
        public bool CollisionDetectFloat(double newX, double newY, out double collidedX, out double collidedY)
        {
            collidedX = newX;
            collidedY = newY;

            bool collided = false;

            // Check for foothold landing when moving downward
            // TODO: Implement foothold lookup for floating collision
            // This requires a foothold lookup delegate to be passed in
            /*
            if (VelocityY > 0 && footholdLookup != null)
            {
                var foothold = footholdLookup((float)newX, (float)newY, 10);
                if (foothold != null)
                {
                    double fhY = CalculateYOnFoothold(foothold, newX);
                    // If we're at or below the foothold, land on it
                    if (newY >= fhY - 5)
                    {
                        collidedY = fhY;
                        CurrentFoothold = foothold;
                        VelocityY = 0;
                        VelocityX *= 0.5f; // Reduce horizontal velocity on landing
                        collided = true;
                    }
                }
            }
            */

            // Bound to map range
            if (newX < MapLeft)
            {
                collidedX = MapLeft;
                collided = true;
            }
            else if (newX > MapRight)
            {
                collidedX = MapRight;
                collided = true;
            }

            if (newY < MapTop)
            {
                collidedY = MapTop;
                collided = true;
            }
            else if (newY > MapBottom)
            {
                collidedY = MapBottom;
                collided = true;
            }

            return collided;
        }

        #endregion

        #region Float State Preservation (SaveFloatState*)

        /// <summary>
        /// Save float state before collision detection.
        /// Client: CVecCtrl::SaveFloatStateBeforeCollision
        /// </summary>
        public void SaveFloatStateBeforeCollision()
        {
            _savedX = X;
            _savedY = Y;
            _savedVelocityX = VelocityX;
            _savedVelocityY = VelocityY;
            _floatStateSaved = true;
        }

        /// <summary>
        /// Save float state after collision detection.
        /// Client: CVecCtrl::SaveFloatStateAfterCollision
        /// Updates saved state with post-collision values.
        /// </summary>
        public void SaveFloatStateAfterCollision()
        {
            if (_floatStateSaved)
            {
                // After collision, update saved velocity to current
                // Position stays at pre-collision for interpolation
                _savedVelocityX = VelocityX;
                _savedVelocityY = VelocityY;
            }
        }

        /// <summary>
        /// Restore float state (undo collision).
        /// </summary>
        public void RestoreFloatState()
        {
            if (_floatStateSaved)
            {
                X = _savedX;
                Y = _savedY;
                VelocityX = _savedVelocityX;
                VelocityY = _savedVelocityY;
                _floatStateSaved = false;
            }
        }

        /// <summary>
        /// Bound position to map range.
        /// Client: CVecCtrl::BoundPosMapRange
        /// </summary>
        public void BoundPosMapRange()
        {
            if (X < MapLeft)
            {
                X = MapLeft;
                if (VelocityX < 0) VelocityX = 0;
            }
            else if (X > MapRight)
            {
                X = MapRight;
                if (VelocityX > 0) VelocityX = 0;
            }

            if (Y < MapTop)
            {
                Y = MapTop;
                if (VelocityY < 0) VelocityY = 0;
            }
            else if (Y > MapBottom)
            {
                Y = MapBottom;
                if (VelocityY > 0) VelocityY = 0;
            }
        }

        /// <summary>
        /// Set map boundaries for BoundPosMapRange.
        /// </summary>
        public void SetMapBounds(int left, int right, int top, int bottom)
        {
            MapLeft = left;
            MapRight = right;
            MapTop = top;
            MapBottom = bottom;
        }

        #endregion

        #region Movement Path (CMovePath integration)

        /// <summary>
        /// Make a new movement path element.
        /// Client: CVecCtrl::MakeNewMovePathElem
        /// </summary>
        public MovePathElement MakeNewMovePathElem()
        {
            return new MovePathElement
            {
                X = (int)X,
                Y = (int)Y,
                VelocityX = (short)VelocityX,
                VelocityY = (short)VelocityY,
                Action = CurrentAction,
                FootholdId = CurrentFoothold?.num ?? 0,
                TimeStamp = Environment.TickCount
            };
        }

        /// <summary>
        /// Make movement path from current state.
        /// Client: CMovePath::MakeMovePath
        /// </summary>
        /// <returns>List of movement path elements</returns>
        public List<MovePathElement> MakeMovePath()
        {
            var path = new List<MovePathElement>();
            path.Add(MakeNewMovePathElem());
            return path;
        }

        /// <summary>
        /// Make continuous movement path (for smooth network sync).
        /// Client: CVecCtrl::MakeContinuousMovePath
        /// </summary>
        /// <param name="currentTimeMs">Current game time in milliseconds</param>
        public void MakeContinuousMovePath(int currentTimeMs)
        {
            if (!IsRecordingPath)
                return;

            // Add path element at intervals
            if (currentTimeMs - _lastPathFlushTime >= PathFlushInterval)
            {
                _movePath.Add(MakeNewMovePathElem());
                _lastPathFlushTime = currentTimeMs;

                // Limit path size
                while (_movePath.Count > 50)
                {
                    _movePath.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Get and clear the current movement path.
        /// Client: CMovePath::Flush
        /// </summary>
        public List<MovePathElement> FlushMovePath()
        {
            var path = new List<MovePathElement>(_movePath);
            _movePath.Clear();
            return path;
        }

        /// <summary>
        /// Check if it's time to flush the movement path.
        /// Client: CMovePath::IsTimeForFlush
        /// </summary>
        public bool IsTimeForFlush(int currentTimeMs)
        {
            return currentTimeMs - _lastPathFlushTime >= PathFlushInterval;
        }

        /// <summary>
        /// Start recording movement path.
        /// </summary>
        public void StartPathRecording()
        {
            IsRecordingPath = true;
            _movePath.Clear();
            _lastPathFlushTime = Environment.TickCount;
        }

        /// <summary>
        /// Stop recording movement path.
        /// </summary>
        public void StopPathRecording()
        {
            IsRecordingPath = false;
            _movePath.Clear();
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Set position directly
        /// </summary>
        public void SetPosition(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// Set velocity directly
        /// </summary>
        public void SetVelocity(double vx, double vy)
        {
            VelocityX = vx;
            VelocityY = vy;
        }

        /// <summary>
        /// Get position as Vector2
        /// </summary>
        public Vector2 GetPosition()
        {
            return new Vector2((float)X, (float)Y);
        }

        /// <summary>
        /// Reset all state
        /// </summary>
        public void Reset()
        {
            X = 0;
            Y = 0;
            VelocityX = 0;
            VelocityY = 0;
            CurrentFoothold = null;
            FallStartFoothold = null;
            IsOnLadderOrRope = false;
            HasPendingImpact = false;
            ImpactVelocityX = 0;
            ImpactVelocityY = 0;
            IsInKnockback = false;
            KnockbackMinX = 0;
            KnockbackMaxX = 0;
            _knockbackTimeRemaining = 0;
            WingsActive = false;
            IsInSwimArea = false;
            IsJumpingDown = false;
            CurrentAction = MoveAction.Stand;
            CurrentJumpState = JumpState.None;
            FacingRight = true;

            // Flying state
            IsFlying = false;
            IsFlyingMap = false;
            HasFlyingAbility = false;

            // Float state preservation
            _savedX = 0;
            _savedY = 0;
            _savedVelocityX = 0;
            _savedVelocityY = 0;
            _floatStateSaved = false;

            // Movement path
            _movePath.Clear();
            IsRecordingPath = false;
            _lastPathFlushTime = 0;
        }

        #endregion
    }

    /// <summary>
    /// Movement action states
    /// </summary>
    public enum MoveAction
    {
        Stand = 0,
        Walk = 1,
        Jump = 2,
        Fall = 3,
        Ladder = 4,
        Rope = 5,
        Swim = 6,
        Fly = 7,
        Attack = 8,
        Hit = 9,
        Die = 10
    }

    /// <summary>
    /// Jump state for animation control
    /// </summary>
    public enum JumpState
    {
        None = 0,
        Jumping = 1,  // Moving upward
        Falling = 2   // Moving downward
    }

    /// <summary>
    /// Movement path element for network synchronization.
    /// Based on CMovePath element structure from client.
    /// </summary>
    public struct MovePathElement
    {
        /// <summary>
        /// X position
        /// </summary>
        public int X;

        /// <summary>
        /// Y position
        /// </summary>
        public int Y;

        /// <summary>
        /// Horizontal velocity
        /// </summary>
        public short VelocityX;

        /// <summary>
        /// Vertical velocity
        /// </summary>
        public short VelocityY;

        /// <summary>
        /// Current action state
        /// </summary>
        public MoveAction Action;

        /// <summary>
        /// Foothold ID (0 if airborne)
        /// </summary>
        public int FootholdId;

        /// <summary>
        /// Timestamp when this element was created
        /// </summary>
        public int TimeStamp;

        /// <summary>
        /// Duration until next element (for interpolation)
        /// </summary>
        public short Duration;

        /// <summary>
        /// Whether facing right
        /// </summary>
        public bool FacingRight;

        /// <summary>
        /// Stat changed flag (for server validation)
        /// </summary>
        public bool StatChanged;
    }

    /// <summary>
    /// Float mode for CalcFloat physics.
    /// </summary>
    public enum FloatMode
    {
        /// <summary>
        /// Normal air physics (gravity applies fully)
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Swimming physics (reduced gravity, water drag)
        /// </summary>
        Swimming = 1,

        /// <summary>
        /// Flying physics (no gravity, free movement)
        /// </summary>
        Flying = 2,

        /// <summary>
        /// Wings effect (reduced gravity, slow fall)
        /// </summary>
        Wings = 3
    }
}
