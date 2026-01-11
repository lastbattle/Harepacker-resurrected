using System;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.Core;
using HaCreator.MapSimulator.Physics;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;

namespace HaCreator.MapSimulator.Character
{
    /// <summary>
    /// Player state for the state machine
    /// </summary>
    public enum PlayerState
    {
        Standing,
        Walking,
        Jumping,
        Falling,
        Ladder,
        Rope,
        Sitting,
        Prone,
        Swimming,
        Flying,      // Flying mount/skill (not GM fly mode)
        Attacking,
        Hit,
        Dead
    }

    /// <summary>
    /// Attack type for different weapon animations
    /// </summary>
    public enum AttackType
    {
        None,
        Stab,       // One-handed stab
        Swing,      // Swing attack
        Shoot,      // Ranged attack
        ProneStab   // Prone stab
    }

    /// <summary>
    /// Player Character Controller - Handles physics, input, and animation
    /// Equivalent to CUserLocal in the MapleStory client
    /// </summary>
    public class PlayerCharacter
    {
        #region Constants

        // ============================================================
        // Physics constants loaded from Map.wz/Physics.img at runtime
        // See PhysicsConstants.cs for the loader
        //
        // Official formulas from CVecCtrl::CalcWalk, AccSpeed, DecSpeed:
        //   maxSpeed = shoeWalkSpeed * physicsWalkSpeed * footholdWalk
        //   v += (force / mass) * tSec
        //   pos += v * tSec
        // ============================================================

        // GM Fly Mode - not in Physics.img, hardcoded for convenience
        private const float GM_FLY_SPEED = 400f; // pixels per second - fast flying for map exploration

        // Hitboxes - Y position is now at feet, so hitbox extends upward from feet
        private const int HITBOX_WIDTH = 30;
        private const int HITBOX_HEIGHT = 60;
        private const int HITBOX_OFFSET_Y = -60; // Hitbox top is 60 pixels above feet

        // Attack cooldowns
        private const int MIN_ATTACK_DELAY = 300;

        // Hit state duration (knockback stun)
        private const int HIT_STUN_DURATION = 400; // 400ms stun when hit by monster

        #endregion

        #region Properties

        public CharacterBuild Build { get; private set; }
        public CharacterAssembler Assembler { get; private set; }
        public CVecCtrl Physics { get; private set; }

        // State
        public PlayerState State { get; private set; } = PlayerState.Standing;
        public CharacterAction CurrentAction { get; private set; } = CharacterAction.Stand1;
        public bool FacingRight { get; set; } = true;
        public bool IsAlive => State != PlayerState.Dead;
        public bool CanMove => State != PlayerState.Dead && State != PlayerState.Hit && State != PlayerState.Attacking;
        public bool CanAttack => State != PlayerState.Dead && State != PlayerState.Hit &&
                                  State != PlayerState.Ladder && State != PlayerState.Rope &&
                                  State != PlayerState.Swimming; // Can't attack while swimming (official behavior)

        /// <summary>
        /// GM Fly Mode - allows free flying around the map ignoring physics
        /// Toggle with G key
        /// </summary>
        public bool GmFlyMode { get; private set; }

        /// <summary>
        /// God Mode - prevents all damage from monsters
        /// </summary>
        public bool GodMode { get; set; }

        // Position shortcuts
        public float X => (float)Physics.X;
        public float Y => (float)Physics.Y;
        public Vector2 Position => new Vector2(X, Y);

        // Stats (from build) - use defaults for placeholder player
        public int HP { get => Build?.HP ?? 100; set { if (Build != null) Build.HP = Math.Clamp(value, 0, Build.MaxHP); } }
        public int MP { get => Build?.MP ?? 100; set { if (Build != null) Build.MP = Math.Clamp(value, 0, Build.MaxMP); } }
        public int MaxHP => Build?.MaxHP ?? 100;
        public int MaxMP => Build?.MaxMP ?? 100;
        public int Level => Build?.Level ?? 1;

        // Animation
        private int _animationStartTime;
        private int _lastAttackTime;
        private AttackType _currentAttackType;
        private int _attackFrame;
        private int _attackFrameTimer;

        // Hit state tracking
        private int _hitStateStartTime;

        // Input state
        private bool _inputLeft;
        private bool _inputRight;
        private bool _inputUp;
        private bool _inputDown;
        private bool _inputJump;
        private bool _inputAttack;
        private bool _inputPickup;
        private bool _inputGmFlyToggle;

        // Debug
        private bool _physicsDebugLogged;
        private bool _wasInSwimMode;

        // Callbacks
        public Action<PlayerCharacter, Rectangle> OnAttackHitbox;
        public Action<PlayerCharacter> OnDeath;
        public Action<PlayerCharacter, int> OnDamaged;

        // Sound callbacks
        private Action _onJumpSound;

        // Foothold system reference
        private Func<float, float, float, FootholdLine> _findFoothold;
        private Func<float, float, float, (int x, int top, int bottom, bool isLadder)?> _findLadder;
        private Func<float, float, float, bool> _checkSwimArea;

        #endregion

        #region Initialization

        public PlayerCharacter(CharacterBuild build)
        {
            Build = build ?? throw new ArgumentNullException(nameof(build));
            Assembler = new CharacterAssembler(build);
            Physics = new CVecCtrl();

            // Preload common animations
            Assembler.PreloadStandardAnimations();
        }

        /// <summary>
        /// Create a placeholder player without character graphics (for testing)
        /// </summary>
        public PlayerCharacter(GraphicsDevice device, TexturePool texturePool, CharacterBuild build)
        {
            Build = build; // Can be null for placeholder
            Physics = new CVecCtrl();

            if (build != null)
            {
                Assembler = new CharacterAssembler(build);
                Assembler.PreloadStandardAnimations();
            }
            // If build is null, we're a placeholder - just track position
        }

        public void SetPosition(float x, float y)
        {
            Physics.SetPosition(x, y);
        }

        public void SetFootholdLookup(Func<float, float, float, FootholdLine> findFoothold)
        {
            _findFoothold = findFoothold;
        }

        public void SetLadderLookup(Func<float, float, float, (int x, int top, int bottom, bool isLadder)?> findLadder)
        {
            _findLadder = findLadder;
        }

        public void SetSwimAreaCheck(Func<float, float, float, bool> checkSwimArea)
        {
            _checkSwimArea = checkSwimArea;
        }

        /// <summary>
        /// Set jump sound callback (called when player jumps)
        /// </summary>
        public void SetJumpSoundCallback(Action onJump)
        {
            _onJumpSound = onJump;
        }

        #endregion

        #region Input

        /// <summary>
        /// Set input state for this frame
        /// </summary>
        public void SetInput(bool left, bool right, bool up, bool down, bool jump, bool attack, bool pickup)
        {
            _inputLeft = left;
            _inputRight = right;
            _inputUp = up;
            _inputDown = down;
            _inputJump = jump;
            _inputAttack = attack;
            _inputPickup = pickup;
        }

        /// <summary>
        /// Clear all input
        /// </summary>
        public void ClearInput()
        {
            _inputLeft = false;
            _inputRight = false;
            _inputUp = false;
            _inputDown = false;
            _inputJump = false;
            _inputAttack = false;
            _inputPickup = false;
            _inputGmFlyToggle = false;
        }

        /// <summary>
        /// Set GM fly toggle input (should be called with key press detection)
        /// </summary>
        public void SetGmFlyToggle(bool pressed)
        {
            _inputGmFlyToggle = pressed;
        }

        /// <summary>
        /// Toggle GM fly mode
        /// </summary>
        public void ToggleGmFlyMode()
        {
            GmFlyMode = !GmFlyMode;
            if (GmFlyMode)
            {
                // Clear foothold state when entering fly mode
                Physics.CurrentFoothold = null;
                Physics.FallStartFoothold = null;
                Physics.VelocityY = 0;
                Physics.ClearKnockback();
                State = PlayerState.Jumping; // Use jump animation while flying
            }
        }

        /// <summary>
        /// Toggle God mode (invincibility)
        /// </summary>
        public void ToggleGodMode()
        {
            GodMode = !GodMode;
            System.Diagnostics.Debug.WriteLine($"[PlayerCharacter] God Mode: {(GodMode ? "ON" : "OFF")}");
        }

        #endregion

        #region Update

        /// <summary>
        /// Update player state, physics, and animation
        /// </summary>
        public void Update(int currentTime, float deltaTime)
        {
            if (!IsAlive) return;

            // Handle GM fly mode toggle
            if (_inputGmFlyToggle)
            {
                ToggleGmFlyMode();
                _inputGmFlyToggle = false;
            }

            // GM Fly Mode - free movement ignoring physics
            if (GmFlyMode)
            {
                UpdateGmFlyMode(deltaTime);
                UpdateAnimation(currentTime);
                return;
            }

            // Process input and update state (pass deltaTime for proper acceleration scaling)
            ProcessInput(currentTime, deltaTime);

            // Update physics - skip if swimming/flying (handled in ProcessFloatMovement)
            bool inFloatMode = (Physics.IsInSwimArea || Physics.IsUserFlying()) && !Physics.IsOnFoothold();
            if (!inFloatMode)
            {
                Physics.Update(deltaTime);
            }

            // Check foothold transitions
            if (Physics.IsAirborne())
            {
                // Falling - check for landing
                CheckFootholdLanding();
            }
            else if (Physics.IsOnFoothold())
            {
                // Walking - check for foothold transitions or walking off edge
                CheckFootholdTransition();
            }

            // Check swim area
            if (_checkSwimArea != null)
            {
                Physics.IsInSwimArea = _checkSwimArea(X, Y, 0);
            }

            // Update state machine
            UpdateStateMachine(currentTime);

            // Update animation
            UpdateAnimation(currentTime);
        }

        /// <summary>
        /// Update when in GM fly mode - free movement with no physics
        /// </summary>
        private void UpdateGmFlyMode(float deltaTime)
        {
            // deltaTime is in seconds, multiply by speed (px/s) to get distance
            float distance = GM_FLY_SPEED * deltaTime;

            // Direct position updates
            if (_inputLeft)
            {
                Physics.X -= distance;
                FacingRight = false;
                Physics.FacingRight = false;
            }
            if (_inputRight)
            {
                Physics.X += distance;
                FacingRight = true;
                Physics.FacingRight = true;
            }
            if (_inputUp)
            {
                Physics.Y -= distance;
            }
            if (_inputDown)
            {
                Physics.Y += distance;
            }

            // Keep jump animation while flying
            State = PlayerState.Jumping;
            Physics.VelocityX = 0;
            Physics.VelocityY = 0;
        }

        private void ProcessInput(int currentTime, float deltaTime)
        {
            if (!CanMove && State != PlayerState.Attacking)
                return;

            // deltaTime is already in seconds (same as tSec in official client)
            float tSec = deltaTime;

            // Attack input
            if (_inputAttack && CanAttack && currentTime - _lastAttackTime >= GetAttackCooldown())
            {
                StartAttack(currentTime);
                return; // Attack takes priority
            }

            // Ladder/rope interaction - UP to grab from below
            if (_inputUp && State != PlayerState.Ladder && State != PlayerState.Rope)
            {
                TryGrabLadder();
            }

            // Ladder/rope interaction - DOWN to grab from above (while on platform)
            if (_inputDown && State != PlayerState.Ladder && State != PlayerState.Rope && Physics.IsOnFoothold())
            {
                TryGrabLadderDown();
            }

            // Jump input
            if (_inputJump)
            {
                TryJump();
            }

            // Movement input - using official physics formula from Physics.img
            // maxSpeed = shoeWalkSpeed * physicsWalkSpeed * footholdWalk
            float maxSpeed = GetMoveSpeed();
            float force = CVecCtrl.WalkAcceleration; // WalkForce / DefaultMass
            float drag = CVecCtrl.WalkDeceleration;  // WalkDrag / DefaultMass
            float mass = 1.0f;                       // Already divided by mass above

            if (State == PlayerState.Ladder || State == PlayerState.Rope)
            {
                // Vertical climbing - use character's actual speed stat, not raw Physics.img value
                // GetMoveSpeed() returns characterSpeed * 0.6f for ladder/rope state
                float climbSpeed = GetMoveSpeed();
                if (_inputUp)
                {
                    // Check if at top of ladder/rope - exit onto platform
                    if (Physics.Y <= Physics.LadderTop)
                    {
                        Physics.ReleaseLadder();
                        // Move up slightly to land on platform above
                        Physics.Y = Physics.LadderTop - 5;
                        State = PlayerState.Falling;
                    }
                    else
                    {
                        Physics.VelocityY = -climbSpeed;
                    }
                }
                else if (_inputDown)
                {
                    // Check if at bottom of ladder/rope - drop down
                    if (Physics.Y >= Physics.LadderBottom)
                    {
                        Physics.ReleaseLadder();
                        State = PlayerState.Falling;
                    }
                    else
                    {
                        Physics.VelocityY = climbSpeed;
                    }
                }
                else
                {
                    Physics.VelocityY = 0;
                }

                // Jump off ladder/rope - requires jump + left/right direction
                // Official behavior: reduced jump (50%) + horizontal force (130% walk speed)
                if (_inputJump && (_inputLeft || _inputRight))
                {
                    Physics.ReleaseLadder();
                    Physics.Jump();

                    // Apply reduced vertical jump (50% of normal)
                    float jumpPower = (Build?.JumpPower ?? 100) / 100f;
                    Physics.VelocityY = -CVecCtrl.JumpVelocity * jumpPower * 0.5f;

                    // Apply horizontal force: walkSpeed * 1.3 in jump direction
                    // Character's Speed stat is the walk speed in px/s (100 = 100 px/s)
                    float direction = _inputRight ? 1f : -1f;
                    float walkSpeed = Build?.Speed ?? 100f;  // Speed stat = walk speed in px/s
                    Physics.VelocityX = walkSpeed * direction * 1.3f;

                    // Set facing direction
                    FacingRight = _inputRight;

                    State = PlayerState.Jumping;
                    return; // Skip the facing direction code below
                }

                // Left/right just changes facing direction while on rope
                if (_inputLeft)
                {
                    FacingRight = false;
                }
                else if (_inputRight)
                {
                    FacingRight = true;
                }
            }
            else if ((Physics.IsInSwimArea || Physics.IsUserFlying()) && !Physics.IsOnFoothold())
            {
                // Swimming/Flying movement - when not on foothold
                ProcessFloatMovement(tSec);
            }
            else if ((Physics.IsInSwimArea || Physics.IsUserFlying()) && Physics.IsOnFoothold() && (_inputUp || _inputJump))
            {
                // On foothold in swim area, pressing up/jump - start swimming
                Physics.DetachFromFoothold();
                Physics.VelocityY = -100; // Upward push to leave ground
                _wasInSwimMode = false; // Reset so entry clamp doesn't trigger
                ProcessFloatMovement(tSec);
            }
            else
            {
                // Normal ground movement (or on foothold in swim area not pressing up)
                // Check for swim mode exit - reduce velocity to prevent gliding
                if (_wasInSwimMode)
                {
                    Physics.VelocityX *= 0.3f;
                    _wasInSwimMode = false;
                }
                // Horizontal movement - using official CVecCtrl::CalcWalk physics
                // AccSpeed: v += (force / mass) * tSec, clamped to maxSpeed
                // DecSpeed: v -= (drag / mass) * tSec, clamped to 0 or target
                //
                // In the official client, force = walkForce * footholdDrag * characterWalkAcc * fieldWalk
                // We use WalkForce directly since PhysicsConstants already applies base multipliers

                double vx = Physics.VelocityX;
                double walkForce = PhysicsConstants.Instance.WalkForce;
                double walkDrag = PhysicsConstants.Instance.WalkDrag;
                double entityMass = PhysicsConstants.Instance.DefaultMass;

                // Debug output - one time per second
                if (!_physicsDebugLogged && (_inputLeft || _inputRight))
                {
                    _physicsDebugLogged = true;
                    System.Diagnostics.Debug.WriteLine($"[PlayerCharacter Physics] maxSpeed={maxSpeed:F1} px/s, walkForce={walkForce:F1}, walkDrag={walkDrag:F1}, mass={entityMass}, tSec={tSec:F4}");
                    System.Diagnostics.Debug.WriteLine($"[PlayerCharacter Physics] accel={walkForce/entityMass:F1} px/s², decel={walkDrag/entityMass:F1} px/s²");
                }

                if (_inputLeft && !_inputRight)
                {
                    // Accelerate left using AccSpeed (negative force)
                    CVecCtrl.AccSpeed(ref vx, -walkForce, entityMass, maxSpeed, tSec);
                    Physics.VelocityX = vx;
                    FacingRight = false;
                    Physics.FacingRight = false;
                }
                else if (_inputRight && !_inputLeft)
                {
                    // Accelerate right using AccSpeed (positive force)
                    CVecCtrl.AccSpeed(ref vx, walkForce, entityMass, maxSpeed, tSec);
                    Physics.VelocityX = vx;
                    FacingRight = true;
                    Physics.FacingRight = true;
                }
                else if (!Physics.IsAirborne())
                {
                    // Decelerate using DecSpeed (target speed = 0)
                    CVecCtrl.DecSpeed(ref vx, walkDrag, entityMass, 0.0, tSec);
                    Physics.VelocityX = vx;
                }
            }

            // Prone - only allow if nearly stationary (velocity in px/s)
            if (_inputDown && Physics.IsOnFoothold() && State != PlayerState.Prone)
            {
                if (Math.Abs(Physics.VelocityX) < 10) // ~10 px/s threshold
                {
                    State = PlayerState.Prone;
                }
            }
            else if (State == PlayerState.Prone && (_inputUp || _inputLeft || _inputRight || _inputJump))
            {
                State = PlayerState.Standing;
            }
        }

        /// <summary>
        /// Process swimming/flying movement using CVecCtrl::CalcFloat physics.
        /// Allows free movement in all directions with drag and reduced/no gravity.
        /// Based on official client physics for swim areas and flying maps.
        /// </summary>
        /// <param name="tSec">Time step in seconds</param>
        private void ProcessFloatMovement(float tSec)
        {
            // Determine input directions
            int inputX = 0;
            int inputY = 0;

            if (_inputLeft && !_inputRight) inputX = -1;
            else if (_inputRight && !_inputLeft) inputX = 1;

            if (_inputUp && !_inputDown) inputY = -1;
            else if (_inputDown && !_inputUp) inputY = 1;

            // Update facing direction
            if (inputX < 0)
            {
                FacingRight = false;
                Physics.FacingRight = false;
            }
            else if (inputX > 0)
            {
                FacingRight = true;
                Physics.FacingRight = true;
            }

            // Get physics parameters based on mode
            double maxSpeed, floatForce, floatDrag, gravityFactor;
            double entityMass = PhysicsConstants.Instance.DefaultMass;

            if (Physics.IsUserFlying())
            {
                // Flying mode - no gravity, high speed
                maxSpeed = PhysicsConstants.Instance.FlySpeed;
                floatForce = PhysicsConstants.Instance.FlyForce;
                floatDrag = PhysicsConstants.Instance.FloatDrag1;
                gravityFactor = 0.0; // No gravity when flying

                State = PlayerState.Flying;
                Physics.CurrentAction = MoveAction.Fly;
            }
            else // Swimming
            {
                // Swimming mode - official client physics
                // Max speed, force, drag from Physics.img
                maxSpeed = PhysicsConstants.Instance.SwimSpeed;
                floatForce = PhysicsConstants.Instance.SwimForce;
                floatDrag = PhysicsConstants.Instance.FloatDrag2;
                // Gravity factor not used for swimming - official uses force/mass/maxSpeed formula
                gravityFactor = 0.0;

                State = PlayerState.Swimming;
                Physics.CurrentAction = MoveAction.Swim;

                // Apply swim speed modifier from equipment/buffs
                float swimSpeedMod = (Build?.Speed ?? 100) / 100f * (float)PhysicsConstants.Instance.SwimSpeedDec;
                maxSpeed *= swimSpeedMod;
            }

            // Apply CalcFloat physics
            Physics.CalcFloat(inputX, inputY, maxSpeed, floatForce, floatDrag, entityMass, gravityFactor, tSec);

            // Debug output (once)
            if (!_physicsDebugLogged && (inputX != 0 || inputY != 0))
            {
                _physicsDebugLogged = true;
                string mode = Physics.IsUserFlying() ? "Flying" : "Swimming";
                System.Diagnostics.Debug.WriteLine($"[PlayerCharacter {mode}] maxSpeed={maxSpeed:F1}, force={floatForce:F1}, drag={floatDrag:F1}, gravity={gravityFactor:F2}");
            }

            // Detach from foothold when entering water/flying
            if (Physics.CurrentFoothold != null)
            {
                Physics.DetachFromFoothold();
            }

            // Handle swim mode entry - clamp velocity
            if (!_wasInSwimMode)
            {
                // Just entered swim mode - clamp velocity to prevent jump momentum
                double maxUpVelocity = maxSpeed * 0.3;
                double maxDownVelocity = maxSpeed * 1.5;

                if (Physics.VelocityY < -maxUpVelocity)
                {
                    Physics.VelocityY = (float)(-maxUpVelocity);
                }
                else if (Physics.VelocityY > maxDownVelocity)
                {
                    Physics.VelocityY = (float)maxDownVelocity;
                }
            }
            _wasInSwimMode = true;
        }

        private void TryJump()
        {
            // Check for jump down first (Down + Jump while on foothold)
            // This works even while prone - character gets up and falls through
            if (_inputDown && Physics.IsOnFoothold())
            {
                if (State == PlayerState.Prone)
                {
                    State = PlayerState.Standing;
                }
                TryJumpDown();
                return;
            }

            if (State == PlayerState.Prone)
            {
                State = PlayerState.Standing;
                return;
            }

            // Don't allow jump from TryJump when on rope - rope jump is handled separately
            // and requires pressing a direction key
            if (Physics.IsOnFoothold() && !Physics.IsOnLadderOrRope)
            {
                // Use Physics.Jump() to properly clear knockback state
                Physics.Jump();

                // Apply jump power modifier
                float jumpPower = (Build?.JumpPower ?? 100) / 100f;
                Physics.VelocityY = -CVecCtrl.JumpVelocity * jumpPower;

                State = PlayerState.Jumping;

                // Play jump sound
                _onJumpSound?.Invoke();
            }
        }

        /// <summary>
        /// Try to jump down through the current platform.
        /// Called when player presses Down + Jump.
        /// </summary>
        private void TryJumpDown()
        {
            if (!Physics.IsOnFoothold())
                return;

            var currentFh = Physics.CurrentFoothold;
            if (currentFh == null)
                return;

            // Check if this foothold allows jumping through
            // CantThrough = true means you cannot jump down through this platform
            if (currentFh.CantThrough == MapleLib.WzLib.WzStructure.MapleBool.True)
            {
                // Cannot jump down through this platform
                return;
            }

            // Initiate jump down
            if (Physics.JumpDown())
            {
                State = PlayerState.Falling;

                // Play jump sound
                _onJumpSound?.Invoke();

                System.Diagnostics.Debug.WriteLine($"[TryJumpDown] Jump down initiated from foothold at Y={currentFh.FirstDot.Y}");
            }
        }

        /// <summary>
        /// Try to grab a ladder/rope when pressing UP
        /// </summary>
        private void TryGrabLadder()
        {
            if (_findLadder == null) return;

            var ladder = _findLadder(X, Y, 50);
            if (ladder.HasValue)
            {
                Physics.GrabLadder(ladder.Value.x, ladder.Value.top, ladder.Value.bottom, ladder.Value.isLadder);
                State = ladder.Value.isLadder ? PlayerState.Ladder : PlayerState.Rope;
            }
        }

        /// <summary>
        /// Try to grab a ladder/rope when pressing DOWN while on a platform.
        /// Official behavior: can grab rope if character Y > rope top (y1)
        /// </summary>
        private void TryGrabLadderDown()
        {
            if (_findLadder == null) return;
            if (!Physics.IsOnFoothold()) return;

            // Search slightly below the player's current position to find ropes that start at/below their feet
            // The player stands on a platform, and the rope typically starts at or just below the platform
            var ladder = _findLadder(X, Y + 15, 15);
            if (ladder.HasValue)
            {
                // Can grab if character is at or above the rope's top (standing on platform above rope)
                if (Y <= ladder.Value.top + 10)
                {
                    Physics.GrabLadder(ladder.Value.x, ladder.Value.top, ladder.Value.bottom, ladder.Value.isLadder);
                    State = ladder.Value.isLadder ? PlayerState.Ladder : PlayerState.Rope;
                }
            }
        }

        private void CheckFootholdLanding()
        {
            if (_findFoothold == null || Physics.VelocityY <= 0) return;

            // Search for foothold at current position
            // Use velocity-based search range: faster falling = larger search range
            // This prevents passing through footholds at high speeds
            float searchRange = Math.Max(20, (float)Physics.VelocityY * 0.05f);
            var fh = _findFoothold(X, Y, searchRange);

            if (fh != null)
            {
                // Check if we're falling through this foothold
                if (Physics.FallStartFoothold != fh)
                {
                    // Landing on a different foothold - always allowed
                    Physics.LandOnFoothold(fh);
                    State = PlayerState.Standing;
                }
                else if (Physics.IsJumpingDown)
                {
                    // Jump-down: must fall below the foothold before landing on it again
                    // Calculate Y on the foothold at our current X position
                    var fallStartFh = Physics.FallStartFoothold;
                    float fhY = (float)CalculateYOnFoothold(fallStartFh, X);

                    // Require at least 30 pixels below the foothold to land on the same one
                    const float MIN_FALL_DISTANCE = 30f;
                    if (Physics.Y >= fhY + MIN_FALL_DISTANCE)
                    {
                        Physics.LandOnFoothold(fh);
                        State = PlayerState.Standing;
                    }
                }
                else
                {
                    // Normal jump: can land on same foothold once we're at or below it
                    // Calculate actual Y on the sloped foothold at current X position
                    float fhY = Physics.FallStartFoothold != null
                        ? (float)CalculateYOnFoothold(Physics.FallStartFoothold, X)
                        : Y;
                    if (Physics.Y >= fhY)
                    {
                        Physics.LandOnFoothold(fh);
                        State = PlayerState.Standing;
                    }
                }
            }
            // No aggressive safety check - let player fall naturally
            // Map boundaries will handle death zones
        }

        /// <summary>
        /// Check if player has walked past the current foothold and needs to transition.
        /// Official client behavior:
        /// - If there's a platform below to land on, allow walking off the edge
        /// - If no platform below (would fall off map), clamp to edge
        /// </summary>
        private void CheckFootholdTransition()
        {
            if (_findFoothold == null || Physics.CurrentFoothold == null) return;

            var currentFh = Physics.CurrentFoothold;
            float fhMinX = Math.Min(currentFh.FirstDot.X, currentFh.SecondDot.X);
            float fhMaxX = Math.Max(currentFh.FirstDot.X, currentFh.SecondDot.X);

            // Check if still within current foothold bounds
            if (X >= fhMinX && X <= fhMaxX)
                return; // Still on current foothold

            // Walked past foothold bounds - search for adjacent foothold at same level
            var newFh = _findFoothold(X, Y + 5, 30);

            if (newFh != null && newFh != currentFh)
            {
                // Transition to adjacent foothold at same level
                Physics.LandOnFoothold(newFh);
            }
            else if (newFh == null)
            {
                // No adjacent foothold at same level
                // Check if there's a platform below to land on (search up to 500px below)
                var footholdBelow = _findFoothold(X, Y + 50, 500);

                if (footholdBelow != null)
                {
                    // There's a platform below - allow falling off the edge
                    Physics.FallStartFoothold = currentFh;
                    Physics.CurrentFoothold = null;
                    Physics.CurrentJumpState = JumpState.Falling;
                    State = PlayerState.Falling;
                }
                else
                {
                    // No platform below - would fall off map, clamp to edge
                    if (X < fhMinX)
                    {
                        Physics.X = fhMinX;
                        Physics.VelocityX = 0;
                    }
                    else if (X > fhMaxX)
                    {
                        Physics.X = fhMaxX;
                        Physics.VelocityX = 0;
                    }
                    // Update Y to match foothold at clamped position
                    Physics.Y = CalculateYOnFoothold(currentFh, Physics.X);
                }
            }
        }

        /// <summary>
        /// Calculate Y position on a foothold at a given X coordinate.
        /// Used for slope handling and edge clamping.
        /// </summary>
        private double CalculateYOnFoothold(FootholdLine fh, double x)
        {
            if (fh == null) return Physics.Y;

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

        private void UpdateStateMachine(int currentTime)
        {
            // Handle Hit state recovery (knockback stun)
            if (State == PlayerState.Hit)
            {
                if (currentTime - _hitStateStartTime >= HIT_STUN_DURATION)
                {
                    // Hit stun is over - return to appropriate state based on physics
                    if (Physics.IsOnLadder())
                        State = PlayerState.Ladder;
                    else if (Physics.IsOnRope())
                        State = PlayerState.Rope;
                    else if (Physics.IsAirborne())
                        State = Physics.VelocityY < 0 ? PlayerState.Jumping : PlayerState.Falling;
                    else
                        State = PlayerState.Standing;
                }
                return; // Don't process other state changes while in hit stun
            }

            // Determine state based on physics
            if (State == PlayerState.Attacking)
            {
                // Check if attack animation is complete
                var anim = Assembler?.GetAnimation(CurrentAction);
                if (anim != null && anim.Length > 0)
                {
                    int attackDuration = 0;
                    foreach (var frame in anim)
                        attackDuration += frame.Duration;

                    if (currentTime - _animationStartTime >= attackDuration)
                    {
                        State = Physics.IsOnFoothold() ? PlayerState.Standing : PlayerState.Falling;
                        System.Diagnostics.Debug.WriteLine($"[UpdateStateMachine] Attack complete, returning to {State}");
                    }
                }
                else
                {
                    // No animation found - return to standing after short delay
                    if (currentTime - _animationStartTime >= 300)
                    {
                        State = Physics.IsOnFoothold() ? PlayerState.Standing : PlayerState.Falling;
                        System.Diagnostics.Debug.WriteLine($"[UpdateStateMachine] No attack anim found for {CurrentAction}, returning to {State}");
                    }
                }
            }
            else if (Physics.IsOnLadder())
            {
                State = PlayerState.Ladder;
            }
            else if (Physics.IsOnRope())
            {
                State = PlayerState.Rope;
            }
            else if (Physics.IsAirborne())
            {
                State = Physics.VelocityY < 0 ? PlayerState.Jumping : PlayerState.Falling;
            }
            else if (Physics.IsSwimming())
            {
                State = PlayerState.Swimming;
            }
            else if (State != PlayerState.Prone && State != PlayerState.Sitting)
            {
                // Show walking animation if:
                // 1. Actually moving (velocity > threshold), OR
                // 2. Pressing movement keys while on ground (e.g., pushing against platform edge)
                bool isPressingMovement = (_inputLeft || _inputRight) && Physics.IsOnFoothold();
                if (Math.Abs(Physics.VelocityX) > 5 || isPressingMovement)
                {
                    State = PlayerState.Walking;
                }
                else
                {
                    State = PlayerState.Standing;
                }
            }
        }

        private void UpdateAnimation(int currentTime)
        {
            CharacterAction newAction = State switch
            {
                PlayerState.Standing => CharacterAction.Stand1,
                PlayerState.Walking => CharacterAction.Walk1,
                PlayerState.Jumping or PlayerState.Falling => CharacterAction.Jump,
                PlayerState.Ladder => CharacterAction.Ladder,
                PlayerState.Rope => CharacterAction.Rope,
                PlayerState.Sitting => CharacterAction.Sit,
                PlayerState.Prone => CharacterAction.Prone,
                PlayerState.Swimming => CharacterAction.Swim,
                PlayerState.Flying => CharacterAction.Fly,
                PlayerState.Attacking => GetAttackAction(),
                PlayerState.Hit => CharacterAction.Stand1,
                PlayerState.Dead => CharacterAction.Dead,
                _ => CharacterAction.Stand1
            };

            if (newAction != CurrentAction)
            {
                CurrentAction = newAction;
                _animationStartTime = currentTime;
            }
        }

        #endregion

        #region Combat

        private void StartAttack(int currentTime)
        {
            State = PlayerState.Attacking;
            _lastAttackTime = currentTime;
            _animationStartTime = currentTime;
            _attackFrame = 0;

            // Determine attack type based on weapon
            var weapon = Build?.GetWeapon();
            if (State == PlayerState.Prone)
            {
                _currentAttackType = AttackType.ProneStab;
            }
            else if (weapon != null)
            {
                // Weapon type determines attack animation
                _currentAttackType = weapon.WeaponType switch
                {
                    "bow" or "crossbow" or "gun" => AttackType.Shoot,
                    "dagger" or "claw" => AttackType.Stab,
                    _ => AttackType.Swing
                };
            }
            else
            {
                _currentAttackType = AttackType.Swing;
            }

            CurrentAction = GetAttackAction();

            // Trigger hitbox callback on attack frame
            var hitbox = GetAttackHitbox();
            OnAttackHitbox?.Invoke(this, hitbox);
        }

        private CharacterAction GetAttackAction()
        {
            return _currentAttackType switch
            {
                AttackType.Stab => CharacterAction.StabO1,
                AttackType.Swing => CharacterAction.SwingO1,
                AttackType.Shoot => CharacterAction.Shoot1,
                AttackType.ProneStab => CharacterAction.ProneStab,
                _ => CharacterAction.SwingO1
            };
        }

        private int GetAttackCooldown()
        {
            var weapon = Build?.GetWeapon();
            if (weapon == null) return 500;

            // Attack speed: 2 (fast) to 9 (slow)
            int speed = weapon.AttackSpeed;
            return MIN_ATTACK_DELAY + (speed - 2) * 50;
        }

        private Rectangle GetAttackHitbox()
        {
            if (Assembler == null)
                return new Rectangle((int)X - 50, (int)Y - 50, 100, 50);
            return Assembler.GetAttackHitbox(CurrentAction, _attackFrame, FacingRight);
        }

        /// <summary>
        /// Trigger a specific skill animation (called by SkillManager)
        /// </summary>
        public void TriggerSkillAnimation(string actionName)
        {
            // Map action name to CharacterAction
            State = PlayerState.Attacking;

            if (string.IsNullOrEmpty(actionName))
                actionName = "attack1";

            CurrentAction = actionName.ToLower() switch
            {
                "attack1" or "stabo1" => CharacterAction.StabO1,
                "attack2" or "stabo2" => CharacterAction.StabO2,
                "swingo1" => CharacterAction.SwingO1,
                "swingo2" => CharacterAction.SwingO2,
                "swingo3" => CharacterAction.SwingO3,
                "swingof" => CharacterAction.SwingOF,
                "shoot1" => CharacterAction.Shoot1,
                "shoot2" => CharacterAction.Shoot2,
                "pronestab" => CharacterAction.ProneStab,
                _ => CharacterAction.SwingO1
            };

            _attackFrame = 0;
            _attackFrameTimer = 0;
            _animationStartTime = Environment.TickCount; // Set animation start time for completion check

            System.Diagnostics.Debug.WriteLine($"[TriggerSkillAnimation] actionName={actionName}, CurrentAction={CurrentAction}, State={State}");
        }

        /// <summary>
        /// Apply damage to player with knockback
        /// </summary>
        /// <param name="damage">Amount of damage</param>
        /// <param name="knockbackX">Horizontal knockback velocity (px/s)</param>
        /// <param name="knockbackY">Vertical knockback velocity (px/s, negative = up)</param>
        /// <param name="immediate">If true, use Impact (immediate). If false, use SetImpactNext (queued).</param>
        public void TakeDamage(int damage, float knockbackX = 0, float knockbackY = 0, bool immediate = true)
        {
            if (!IsAlive) return;
            if (GodMode) return; // No damage in god mode

            HP -= damage;
            OnDamaged?.Invoke(this, damage);

            if (HP <= 0)
            {
                Die();
                return;
            }

            // Enter hit state (knockback stun)
            State = PlayerState.Hit;
            _hitStateStartTime = Environment.TickCount;
            Physics.CurrentAction = MoveAction.Hit;

            // Apply knockback velocity
            if (knockbackX != 0 || knockbackY != 0)
            {
                if (immediate)
                {
                    // Use Impact for immediate knockback (boss attacks, strong hits)
                    Physics.Impact(knockbackX, knockbackY);
                }
                else
                {
                    // Use SetImpactNext for queued knockback (accumulated damage)
                    Physics.SetImpactNext(knockbackX, knockbackY);
                }
            }
        }

        /// <summary>
        /// Apply knockback without damage (e.g., from skills, traps, environmental hazards).
        /// Uses CVecCtrl.Impact for immediate effect.
        /// </summary>
        /// <param name="knockbackX">Horizontal knockback velocity (px/s)</param>
        /// <param name="knockbackY">Vertical knockback velocity (px/s, negative = up)</param>
        public void ApplyKnockback(float knockbackX, float knockbackY)
        {
            if (!IsAlive) return;

            // Enter hit state for knockback animation
            State = PlayerState.Hit;
            _hitStateStartTime = Environment.TickCount;
            Physics.CurrentAction = MoveAction.Hit;

            // Use Impact for immediate knockback
            Physics.Impact(knockbackX, knockbackY);
        }

        /// <summary>
        /// Apply knockback in a direction (convenience method).
        /// </summary>
        /// <param name="force">Knockback force (px/s)</param>
        /// <param name="knockRight">True = knock right, False = knock left</param>
        /// <param name="verticalForce">Optional vertical force (negative = up)</param>
        public void ApplyKnockback(float force, bool knockRight, float verticalForce = -100f)
        {
            float vx = knockRight ? force : -force;
            ApplyKnockback(vx, verticalForce);
        }

        /// <summary>
        /// Apply knockback away from a source position.
        /// </summary>
        /// <param name="sourceX">X position of knockback source</param>
        /// <param name="force">Knockback force (px/s)</param>
        /// <param name="verticalForce">Optional vertical force (negative = up)</param>
        public void ApplyKnockbackFrom(float sourceX, float force, float verticalForce = -100f)
        {
            bool knockRight = sourceX < X;
            ApplyKnockback(force, knockRight, verticalForce);
        }

        /// <summary>
        /// Kill the player
        /// </summary>
        public void Die()
        {
            if (!IsAlive) return;

            HP = 0;
            State = PlayerState.Dead;
            CurrentAction = CharacterAction.Dead;

            // Completely stop all physics - velocity, knockback, and movement state
            Physics.VelocityX = 0;
            Physics.VelocityY = 0;
            Physics.ClearKnockback();
            Physics.CurrentAction = MoveAction.Stand;

            // Store death position for tombstone
            DeathX = X;
            DeathY = Y;

            OnDeath?.Invoke(this);
        }

        /// <summary>
        /// Death position X coordinate (for tombstone display)
        /// </summary>
        public float DeathX { get; private set; }

        /// <summary>
        /// Death position Y coordinate (for tombstone display)
        /// </summary>
        public float DeathY { get; private set; }

        /// <summary>
        /// Respawn at position
        /// </summary>
        public void Respawn(float x, float y)
        {
            HP = MaxHP;
            MP = MaxMP;
            State = PlayerState.Standing;
            CurrentAction = CharacterAction.Stand1;
            Physics.Reset();
            Physics.SetPosition(x, y);
        }

        /// <summary>
        /// Force the player to standing state and clear all movement.
        /// Used when entering portals, interacting with objects, etc.
        /// </summary>
        public void ForceStand()
        {
            if (State == PlayerState.Dead)
                return;

            State = PlayerState.Standing;
            CurrentAction = CharacterAction.Stand1;
            Physics.VelocityX = 0;

            // Clear input states to prevent resuming movement
            _inputLeft = false;
            _inputRight = false;
            _inputUp = false;
            _inputDown = false;
            _inputJump = false;
        }

        #endregion

        #region Draw

        /// <summary>
        /// Draw the player
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer,
            int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime)
        {
            int screenX = (int)X - mapShiftX + centerX;
            int screenY = (int)Y - mapShiftY + centerY;

            // If no assembler (placeholder player), skip drawing - debug box handles it
            if (Assembler == null)
                return;

            // Get current frame
            int animTime = currentTime - _animationStartTime;

            // When on rope/ladder and not moving, use still frame (frame 0)
            if ((State == PlayerState.Rope || State == PlayerState.Ladder) && Math.Abs(Physics.VelocityY) < 0.001)
            {
                animTime = 0;
            }

            var frame = Assembler.GetFrameAtTime(CurrentAction, animTime);

            if (frame != null)
            {
                // Apply hit flash effect
                Color tint = Color.White;
                if (State == PlayerState.Hit)
                {
                    float flash = (float)Math.Sin(currentTime * 0.02) * 0.5f + 0.5f;
                    tint = Color.Lerp(Color.White, Color.Red, flash);
                }

                // MapleStory sprites face LEFT by default, flip when facing right
                frame.Draw(spriteBatch, skeletonRenderer, screenX, screenY, FacingRight, tint);
            }
            else
            {
                // Fallback: draw simple rectangle
                var rect = new Rectangle(screenX - 15, screenY - 60, 30, 60);
                spriteBatch.Draw(GetPixelTexture(spriteBatch.GraphicsDevice), rect, Color.Blue * 0.5f);
            }
        }

        /// <summary>
        /// Draw HP/MP bars
        /// </summary>
        public void DrawStatusBars(SpriteBatch spriteBatch, int screenX, int screenY)
        {
            var pixel = GetPixelTexture(spriteBatch.GraphicsDevice);

            // HP bar
            int barWidth = 40;
            int barHeight = 4;
            int hpBarX = screenX - barWidth / 2;
            int hpBarY = screenY - 70;

            // Background
            spriteBatch.Draw(pixel, new Rectangle(hpBarX - 1, hpBarY - 1, barWidth + 2, barHeight + 2), Color.Black);
            // HP fill
            float hpRatio = (float)HP / MaxHP;
            spriteBatch.Draw(pixel, new Rectangle(hpBarX, hpBarY, (int)(barWidth * hpRatio), barHeight), Color.Red);

            // MP bar
            int mpBarY = hpBarY + barHeight + 2;
            spriteBatch.Draw(pixel, new Rectangle(hpBarX - 1, mpBarY - 1, barWidth + 2, barHeight + 2), Color.Black);
            float mpRatio = (float)MP / MaxMP;
            spriteBatch.Draw(pixel, new Rectangle(hpBarX, mpBarY, (int)(barWidth * mpRatio), barHeight), Color.Blue);
        }

        private static Texture2D _pixelTexture;
        private static Texture2D GetPixelTexture(GraphicsDevice device)
        {
            if (_pixelTexture == null || _pixelTexture.IsDisposed)
            {
                _pixelTexture = new Texture2D(device, 1, 1);
                _pixelTexture.SetData(new[] { Color.White });
            }
            return _pixelTexture;
        }

        #endregion

        #region Utility

        /// <summary>
        /// Get player hitbox for collision
        /// </summary>
        public Rectangle GetHitbox()
        {
            return new Rectangle(
                (int)X - HITBOX_WIDTH / 2,
                (int)Y + HITBOX_OFFSET_Y,
                HITBOX_WIDTH,
                HITBOX_HEIGHT);
        }

        /// <summary>
        /// Get movement speed based on current state (in pixels per second)
        /// Official formula: maxSpeed = shoeWalkSpeed * physicsWalkSpeed * footholdWalk
        /// Values loaded from Map.wz/Physics.img
        /// </summary>
        private float GetMoveSpeed()
        {
            // Get character Speed stat (default 100)
            float characterSpeed = Build?.Speed ?? 100f;

            // Official client formula:
            //   maxSpeed = CAttrShoe::walkSpeed * (dWalkSpeed * footholdDrag)
            //   walkSpeed = characterSpeed / dWalkSpeed, so formula simplifies to:
            //   maxSpeed = (characterSpeed / 1250) * 1250 * footholdDrag = characterSpeed * footholdDrag
            //
            // Official client: Speed 100 = 100 px/s (with footholdDrag = 1.0)
            // MapleNecrocer uses 2.5 px/frame at 60fps = 150 px/s (1.5x faster than official)
            const float WalkSpeedScale = 1.0f;  // Official client formula

            if (State == PlayerState.Swimming)
            {
                // Swimming uses swimSpeedDec multiplier (from Physics.img)
                return characterSpeed * WalkSpeedScale * CVecCtrl.SwimSpeedFactor;
            }
            else if (State == PlayerState.Ladder || State == PlayerState.Rope)
            {
                // Ladder/rope climbing is approximately 60% of walk speed
                // MapleNecrocer uses 1.5 px/frame for ladder = ~45 px/s at 30fps
                return characterSpeed * WalkSpeedScale * 0.6f;
            }
            else
            {
                // Normal walking: Speed 100 = 100 px/s (official client formula)
                return characterSpeed * WalkSpeedScale;
            }
        }

        /// <summary>
        /// Get distance to another position
        /// </summary>
        public float DistanceTo(float x, float y)
        {
            float dx = X - x;
            float dy = Y - y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Check if player is within range of position
        /// </summary>
        public bool IsInRange(float x, float y, float range)
        {
            return DistanceTo(x, y) <= range;
        }

        #endregion
    }
}
