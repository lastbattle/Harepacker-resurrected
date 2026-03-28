using System;
using System.Collections.Generic;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.Core;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Physics;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System.IO;
using System.Linq;

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
        private sealed class PlayerSkillBlockingStatusState
        {
            public int ExpireTime { get; set; }
        }

        private sealed class SkillAvatarTransformState
        {
            public int SkillId { get; init; }
            public int SourceId { get; init; }
            public CharacterPart AvatarPart { get; init; }
            public IReadOnlyList<string> StandActionNames { get; init; }
            public IReadOnlyList<string> WalkActionNames { get; init; }
            public IReadOnlyList<string> JumpActionNames { get; init; }
            public IReadOnlyList<string> ProneActionNames { get; init; }
            public IReadOnlyList<string> AttackActionNames { get; init; }
            public IReadOnlyList<string> ClimbActionNames { get; init; }
            public IReadOnlyList<string> FloatActionNames { get; init; }
            public IReadOnlyList<string> HitActionNames { get; init; }
            public string ExitActionName { get; init; }
            public bool LocksMovement { get; init; }
        }

        private enum SkillAvatarEffectMode
        {
            Ground,
            LadderOrRope
        }

        private enum SkillAvatarEffectPlane
        {
            BehindCharacter,
            UnderFace,
            OverCharacter
        }

        private sealed class SkillAvatarEffectState
        {
            public int SkillId { get; init; }
            public SkillAnimation GroundOverlayAnimation { get; init; }
            public SkillAnimation GroundOverlaySecondaryAnimation { get; init; }
            public SkillAnimation GroundUnderFaceAnimation { get; init; }
            public SkillAnimation GroundUnderFaceSecondaryAnimation { get; init; }
            public SkillAnimation LadderOverlayAnimation { get; init; }
            public SkillAnimation GroundOverlayFinishAnimation { get; init; }
            public SkillAnimation GroundUnderFaceFinishAnimation { get; init; }
            public SkillAnimation LadderOverlayFinishAnimation { get; init; }
            public bool HideOnLadderOrRope { get; init; }
            public int AnimationStartTime { get; set; }
            public bool IsFinishing { get; set; }
            public SkillAvatarEffectMode Mode { get; set; }

            public bool HasLoopAnimation =>
                GroundOverlayAnimation != null
                || GroundOverlaySecondaryAnimation != null
                || GroundUnderFaceAnimation != null
                || GroundUnderFaceSecondaryAnimation != null
                || LadderOverlayAnimation != null;

            public bool HasFinishAnimation =>
                GroundOverlayFinishAnimation != null
                || GroundUnderFaceFinishAnimation != null
                || LadderOverlayFinishAnimation != null;
        }

        private sealed class TransientSkillAvatarEffectState
        {
            public int SkillId { get; init; }
            public SkillAnimation Animation { get; init; }
            public SkillAnimation SecondaryAnimation { get; init; }
            public int AnimationStartTime { get; set; }
            public SkillAvatarEffectPlane Plane { get; init; }
            public SkillAvatarEffectPlane SecondaryPlane { get; init; }
        }

        private sealed class MeleeAfterImageState
        {
            public int SkillId { get; init; }
            public string ActionName { get; init; }
            public MeleeAfterImageAction AfterImageAction { get; init; }
            public int AnimationStartTime { get; set; }
            public bool FacingRight { get; init; }
            public int ActionDuration { get; init; }
            public int FadeDuration { get; init; }
            public int FadeStartTime { get; set; } = -1;
            public int LastFrameIndex { get; set; } = -1;
        }

        private sealed class ShadowPartnerState
        {
            public int SkillId { get; init; }
            public IReadOnlyDictionary<string, SkillAnimation> ActionAnimations { get; init; }
            public int HorizontalOffsetPx { get; init; }
            public string CurrentActionName { get; set; }
            public int CurrentActionStartTime { get; set; }
            public bool CurrentFacingRight { get; set; }
            public string ObservedPlayerActionName { get; set; }
            public string PendingActionName { get; set; }
            public int PendingActionReadyTime { get; set; }
            public bool PendingFacingRight { get; set; }
            public string QueuedActionName { get; set; }
            public bool QueuedFacingRight { get; set; }
            public bool ObservedPlayerFloatingState { get; set; }
        }

        private readonly struct AvatarEffectRenderable
        {
            public AvatarEffectRenderable(SkillFrame frame, SkillAvatarEffectPlane plane)
            {
                Frame = frame;
                Plane = plane;
            }

            public SkillFrame Frame { get; }
            public SkillAvatarEffectPlane Plane { get; }
        }

        #region Constants

        private const int MinimumMeleeAfterImageFadeDurationMs = 60;
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
        private const int MechanicTamingMobItemId = 1932016;

        // Hitboxes - Y position is now at feet, so hitbox extends upward from feet
        private const int HITBOX_WIDTH = 30;
        private const int HITBOX_HEIGHT = 60;
        private const int HITBOX_OFFSET_Y = -60; // Hitbox top is 60 pixels above feet

        // Attack cooldowns
        private const int MIN_ATTACK_DELAY = 300;
        private const int FLOAT_JUMP_COOLDOWN_MS = 300;

        // Hit state duration (knockback stun)
        private const int HIT_STUN_DURATION = 400; // 400ms stun when hit by monster
        private const int FACE_HIT_EXPRESSION_DURATION_MS = 450;
        private const int FACE_BLINK_DURATION_MS = 120;
        private const int FACE_BLINK_MIN_INTERVAL_MS = 2500;
        private const int FACE_BLINK_MAX_INTERVAL_MS = 4500;
        private const int PORTABLE_CHAIR_RECOVERY_INTERVAL_MS = 10000;
        private const int ShadowPartnerAttackDelayMs = 90;

        // Float idle should ignore the tiny passive sink applied by swim physics.
        private const float FLOAT_ANIMATION_MOVEMENT_THRESHOLD = 20f;
        // CActionMan action metadata uses 150 as the default alpha for composed character pieces.
        private static readonly Color ShadowPartnerTint = new(255, 255, 255, 150);

        #endregion

        #region Properties

        public CharacterBuild Build { get; private set; }
        public CharacterAssembler Assembler { get; private set; }
        public CVecCtrl Physics { get; private set; }

        // State
        public PlayerState State { get; private set; } = PlayerState.Standing;
        public CharacterAction CurrentAction { get; private set; } = CharacterAction.Stand1;
        public string CurrentActionName { get; private set; } = CharacterPart.GetActionString(CharacterAction.Stand1);
        public string CurrentFaceExpressionName { get; private set; } = "default";
        public bool FacingRight { get; set; } = true;
        public bool IsAlive => State != PlayerState.Dead;
        public bool CanMove => !IsMovementLockedBySkillTransform
                               && State != PlayerState.Dead
                               && State != PlayerState.Hit
                               && State != PlayerState.Attacking;
        public bool CanAttack => State != PlayerState.Dead && State != PlayerState.Hit &&
                                  State != PlayerState.Ladder && State != PlayerState.Rope &&
                                  State != PlayerState.Swimming; // Can't attack while swimming (official behavior)
        public bool IsRecordingMovementPath => Physics?.IsRecordingPath == true;
        public bool IsMovementLockedBySkillTransform => GetActiveAvatarTransform()?.LocksMovement == true;
        public bool HasActiveMorphTransform => GetActiveAvatarTransform()?.AvatarPart?.Type == CharacterPartType.Morph;

        /// <summary>
        /// GM Fly Mode - allows free flying around the map ignoring physics
        /// Toggle with G key
        /// </summary>
        public bool GmFlyMode { get; private set; }

        /// <summary>
        /// God Mode - prevents all damage from monsters
        /// </summary>
        public bool GodMode { get; set; }

        public bool HasActiveSkillBlockingStatus(int currentTime)
        {
            ClearExpiredSkillBlockingStatuses(currentTime);
            return _activeSkillBlockingStatuses.Count > 0;
        }

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
        private string _forcedActionName;
        private bool _sustainedSkillAnimation;
        private SkillAvatarTransformState _activeSkillAvatarTransform;
        private SkillAvatarTransformState _activeExternalAvatarTransform;
        private int _activeExternalAvatarTransformExpiresAt = int.MaxValue;
        private readonly System.Collections.Generic.List<SkillAvatarEffectState> _activeSkillAvatarEffects = new();
        private readonly System.Collections.Generic.List<TransientSkillAvatarEffectState> _transientSkillAvatarEffects = new();
        private MeleeAfterImageState _activeMeleeAfterImage;
        private readonly HashSet<int> _skillAvatarEffectRenderSuppressionSkillIds = new();
        private readonly Random _faceExpressionRandom = new(Environment.TickCount);
        private int _nextBlinkTime = Environment.TickCount + FACE_BLINK_MIN_INTERVAL_MS;
        private int _blinkExpressionEndTime;
        private int _hitExpressionEndTime;
        private int _nextPortableChairRecoveryTime = int.MaxValue;
        private CharacterPart _observedTamingMobPart;
        private bool _observedClientOwnedTamingMobActive;
        private CharacterPart _transitionTamingMobOverridePart;
        private CharacterPart _stateDrivenTamingMobOverridePart;
        private CharacterPart _sharedMechanicTamingMobPart;
        private CharacterPart _clientOwnedVehicleTamingMobPart;
        private bool _clientOwnedVehicleTamingMobActive;
        private bool _suppressAutomaticTamingMobTransition;
        private readonly Dictionary<PlayerSkillBlockingStatus, PlayerSkillBlockingStatusState> _activeSkillBlockingStatuses = new();

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
        private bool _isFloatAnimationMoving;
        private bool _wasJumpHeldLastFrame;
        private bool _mobUndeadRecoveryActive;
        private int _mobHpRecoveryCapPercent = 100;
        private int _mobMpRecoveryCapPercent = 100;
        private int _mobHpRecoveryDamagePercent = 100;
        private bool _jumpPressedThisFrame;
        private int _lastFloatJumpTime = int.MinValue;
        private float _externalMoveSpeedMultiplier = 1f;

        // Callbacks
        public Action<PlayerCharacter, Rectangle> OnAttackHitbox;
        public Action<PlayerCharacter> OnDeath;
        public Action<PlayerCharacter, int> OnDamaged;

        // Sound callbacks
        private Action _onJumpSound;
        private Func<string> _jumpRestrictionMessageProvider;
        private Action<string> _onJumpRestricted;
        private Func<float, float> _moveSpeedCapResolver;

        // Foothold system reference
        private Func<float, float, float, FootholdLine> _findFoothold;
        private Func<float, float, float, (int x, int top, int bottom, bool isLadder)?> _findLadder;
        private Func<float, float, float, bool> _checkSwimArea;
        private Func<int, CharacterPart> _tamingMobLoader;
        private Func<int, CharacterPart> _portableChairTamingMobLoader;
        private Func<int, CharacterPart> _skillMorphLoader;
        private CharacterPart _portableChairPreviousMount;
        private bool _portableChairAppliedMount;
        private CharacterAssembler _portableChairPairAssembler;
        private Point _portableChairPairOffset;
        private bool _portableChairPairFacingRight;
        private string _portableChairPairActionName;
        private bool _portableChairExternalPairRequested;
        private bool _portableChairHasExternalPair;
        private Vector2 _portableChairExternalPairPosition;
        private bool _portableChairExternalPairFacingRight;
        private ShadowPartnerState _activeShadowPartner;

        // Preserve ladder context across hit knockback so holding UP can immediately re-grab it.
        private bool _pendingLadderRegrab;
        private int _pendingLadderX;
        private int _pendingLadderTop;
        private int _pendingLadderBottom;
        private bool _pendingLadderIsLadder;

        #endregion

        #region Initialization

        public PlayerCharacter(CharacterBuild build)
        {
            Build = build ?? throw new ArgumentNullException(nameof(build));
            Assembler = new CharacterAssembler(build);
            Physics = new CVecCtrl();
            _observedTamingMobPart = GetEquippedTamingMobPart();
            UpdateAssemblerAvatarOverride();

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
                UpdateAssemblerAvatarOverride();
                Assembler.PreloadStandardAnimations();
                _observedTamingMobPart = GetEquippedTamingMobPart();
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
            Physics.SetLadderOrRopeLookup(findLadder == null
                ? null
                : (x, y, range) =>
                {
                    var ladder = findLadder(x, y, range);
                    return ladder.HasValue
                        ? new LadderOrRopeInfo(ladder.Value.x, ladder.Value.top, ladder.Value.bottom, ladder.Value.isLadder)
                        : null;
                });
        }

        public void SetSwimAreaCheck(Func<float, float, float, bool> checkSwimArea)
        {
            _checkSwimArea = checkSwimArea;
        }

        public void SetPortableChairTamingMobLoader(Func<int, CharacterPart> loader)
        {
            _portableChairTamingMobLoader = loader;
        }

        public void SetTamingMobLoader(Func<int, CharacterPart> loader)
        {
            _tamingMobLoader = loader;
            _sharedMechanicTamingMobPart = null;
        }

        public void SetSkillMorphLoader(Func<int, CharacterPart> loader)
        {
            _skillMorphLoader = loader;
        }

        /// <summary>
        /// Set jump sound callback (called when player jumps)
        /// </summary>
        public void SetJumpSoundCallback(Action onJump)
        {
            _onJumpSound = onJump;
        }

        public void SetJumpRestrictionHandler(Func<string> getRestrictionMessage, Action<string> onJumpRestricted)
        {
            _jumpRestrictionMessageProvider = getRestrictionMessage;
            _onJumpRestricted = onJumpRestricted;
        }

        public void SetMoveSpeedCapResolver(Func<float, float> moveSpeedCapResolver)
        {
            _moveSpeedCapResolver = moveSpeedCapResolver;
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

        public bool TryActivatePortableChair(PortableChair chair)
        {
            if (chair == null || Build == null || !IsAlive || !Physics.IsOnFoothold())
            {
                return false;
            }

            ClearPortableChairMountState();
            Build.ActivePortableChair = chair;
            ApplyPortableChairMount(chair);
            Physics.VelocityX = 0;
            Physics.VelocityY = 0;
            Physics.CurrentAction = MoveAction.Stand;
            ClearForcedActionName();
            State = PlayerState.Sitting;
            CurrentAction = CharacterAction.Sit;
            CurrentActionName = GetPortableChairActionName(chair);
            _animationStartTime = Environment.TickCount;
            _nextPortableChairRecoveryTime = Environment.TickCount + PORTABLE_CHAIR_RECOVERY_INTERVAL_MS;
            ConfigurePortableChairPairPreview(chair);
            return true;
        }

        public void ClearPortableChair(bool standUp = true)
        {
            if (Build?.ActivePortableChair == null)
            {
                return;
            }

            Build.ActivePortableChair = null;
            ClearPortableChairPairPreview();
            SetPortableChairPairRequestActive(false);
            ClearPortableChairMountState();
            _nextPortableChairRecoveryTime = int.MaxValue;
            if (standUp && State == PlayerState.Sitting)
            {
                State = Physics.IsOnFoothold() ? PlayerState.Standing : PlayerState.Falling;
                CurrentAction = CharacterAction.Stand1;
                CurrentActionName = CharacterPart.GetActionString(CharacterAction.Stand1);
                _animationStartTime = Environment.TickCount;
            }
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
            _jumpPressedThisFrame = false;
            _wasJumpHeldLastFrame = false;
        }

        public void ApplySkillBlockingStatus(PlayerSkillBlockingStatus status, int durationMs, int currentTime)
        {
            if (durationMs <= 0)
            {
                return;
            }

            if (_activeSkillBlockingStatuses.TryGetValue(status, out PlayerSkillBlockingStatusState existingState))
            {
                existingState.ExpireTime = Math.Max(existingState.ExpireTime, currentTime + durationMs);
                return;
            }

            _activeSkillBlockingStatuses[status] = new PlayerSkillBlockingStatusState
            {
                ExpireTime = currentTime + durationMs
            };
        }

        public string GetSkillBlockingRestrictionMessage(int currentTime)
        {
            ClearExpiredSkillBlockingStatuses(currentTime);

            if (_activeSkillBlockingStatuses.ContainsKey(PlayerSkillBlockingStatus.Stun))
            {
                return PlayerSkillBlockingStatusMapper.GetRestrictionMessage(PlayerSkillBlockingStatus.Stun);
            }

            if (_activeSkillBlockingStatuses.ContainsKey(PlayerSkillBlockingStatus.Freeze))
            {
                return PlayerSkillBlockingStatusMapper.GetRestrictionMessage(PlayerSkillBlockingStatus.Freeze);
            }

            if (_activeSkillBlockingStatuses.ContainsKey(PlayerSkillBlockingStatus.Seal))
            {
                return PlayerSkillBlockingStatusMapper.GetRestrictionMessage(PlayerSkillBlockingStatus.Seal);
            }

            if (_activeSkillBlockingStatuses.ContainsKey(PlayerSkillBlockingStatus.Attract))
            {
                return PlayerSkillBlockingStatusMapper.GetRestrictionMessage(PlayerSkillBlockingStatus.Attract);
            }

            return null;
        }

        public void ClearSkillBlockingStatuses()
        {
            _activeSkillBlockingStatuses.Clear();
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

        public void SetExternalMoveSpeedMultiplier(float multiplier)
        {
            _externalMoveSpeedMultiplier = Math.Clamp(multiplier, 0.1f, 3f);
        }

        #endregion

        #region Update

        /// <summary>
        /// Update player state, physics, and animation
        /// </summary>
        public void Update(int currentTime, float deltaTime)
        {
            if (!IsAlive) return;

            ExpireExternalAvatarTransformIfNeeded(currentTime);
            _jumpPressedThisFrame = _inputJump && !_wasJumpHeldLastFrame;

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
                UpdateOwnedTamingMobRenderState();
                UpdateShadowPartnerRenderState(currentTime);
                UpdateAvatarEffects(currentTime);
                UpdateFaceExpression(currentTime);
                RecordMovementSync(currentTime);
                return;
            }

            RefreshSwimAreaState();
            UpdateAutomaticTamingMobTransition();
            TryRegrabLadderWhileHoldingUp();

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

            RefreshSwimAreaState();

            // Update state machine
            UpdateStateMachine(currentTime);

            // Update animation
            UpdateAnimation(currentTime);
            UpdateOwnedTamingMobRenderState();
            UpdateShadowPartnerRenderState(currentTime);
            UpdateAvatarEffects(currentTime);
            UpdateFaceExpression(currentTime);
            ApplyPortableChairRecovery(currentTime);
            RecordMovementSync(currentTime);

            _wasJumpHeldLastFrame = _inputJump;
        }

        private void ApplyPortableChairRecovery(int currentTime)
        {
            PortableChair chair = Build?.ActivePortableChair;
            if (chair == null || State != PlayerState.Sitting)
            {
                _nextPortableChairRecoveryTime = int.MaxValue;
                return;
            }

            if (_nextPortableChairRecoveryTime == int.MaxValue)
            {
                _nextPortableChairRecoveryTime = currentTime + PORTABLE_CHAIR_RECOVERY_INTERVAL_MS;
                return;
            }

            if (currentTime < _nextPortableChairRecoveryTime)
            {
                return;
            }

            if (chair.RecoveryHp > 0)
            {
                Recover(chair.RecoveryHp, 0);
            }

            if (chair.RecoveryMp > 0)
            {
                Recover(0, chair.RecoveryMp);
            }

            do
            {
                _nextPortableChairRecoveryTime += PORTABLE_CHAIR_RECOVERY_INTERVAL_MS;
            }
            while (_nextPortableChairRecoveryTime <= currentTime);
        }

        private void RefreshSwimAreaState()
        {
            if (_checkSwimArea != null)
            {
                Physics.IsInSwimArea = _checkSwimArea(X, Y, 0);
            }
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
            bool movementLockedBySkillTransform = IsMovementLockedBySkillTransform;
            if (!CanMove && State != PlayerState.Attacking && !movementLockedBySkillTransform)
                return;

            if (Build?.ActivePortableChair != null && ShouldCancelPortableChairFromInput())
            {
                ClearPortableChair();
            }

            // deltaTime is already in seconds (same as tSec in official client)
            float tSec = deltaTime;

            // Attack input
            if (_inputAttack && CanAttack && currentTime - _lastAttackTime >= GetAttackCooldown())
            {
                StartAttack(currentTime);
                return; // Attack takes priority
            }

            if (movementLockedBySkillTransform)
            {
                Physics.VelocityX = 0;
                return;
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
                TryJump(currentTime);
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
                        ExitLadderAtTop();
                        return;
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
                        Physics.ReleaseLadder(yOverride: Physics.LadderBottom + 1);
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
                    float jumpPower = (Build?.JumpPower ?? 100) / 100f;
                    double jumpVelocityY = -CVecCtrl.JumpVelocity * jumpPower * 0.5f;

                    // Apply horizontal force: walkSpeed * 1.3 in jump direction
                    // Character's Speed stat is the walk speed in px/s (100 = 100 px/s)
                    float direction = _inputRight ? 1f : -1f;
                    double walkSpeed = Build?.Speed ?? 100f;  // Speed stat = walk speed in px/s
                    double jumpVelocityX = walkSpeed * direction * 1.3f;
                    Physics.JumpOffLadder(jumpVelocityX, jumpVelocityY);

                    // Set facing direction
                    FacingRight = _inputRight;
                    Physics.FacingRight = FacingRight;

                    State = PlayerState.Jumping;
                    _onJumpSound?.Invoke();
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
                // On foothold in a swim/fly map, up/jump transitions into float control.
                Physics.DetachFromFoothold();
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
                    System.Diagnostics.Debug.WriteLine($"[PlayerCharacter Physics] accel={walkForce/entityMass:F1} px/sﾂｲ, decel={walkDrag/entityMass:F1} px/sﾂｲ");
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
            if (Physics.CurrentFoothold != null)
            {
                Physics.DetachFromFoothold();
            }

            // Determine input directions
            int inputX = 0;
            int inputY = 0;

            if (_inputLeft && !_inputRight) inputX = -1;
            else if (_inputRight && !_inputLeft) inputX = 1;

            if (_inputUp && !_inputDown)
            {
                inputY = -1;
            }
            else if (_inputDown && !_inputUp)
            {
                inputY = 1;
            }

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
                // Swimming mode - use the raw swim vecctrl values from Physics.img.
                maxSpeed = PhysicsConstants.Instance.SwimSpeed;
                floatForce = PhysicsConstants.Instance.SwimForce;
                floatDrag = PhysicsConstants.Instance.FloatDrag2;
                gravityFactor = 0.3;

                State = PlayerState.Swimming;
                Physics.CurrentAction = MoveAction.Swim;
            }

            // Debug output (once)
            if (!_physicsDebugLogged && (inputX != 0 || inputY != 0))
            {
                _physicsDebugLogged = true;
                string mode = Physics.IsUserFlying() ? "Flying" : "Swimming";
                System.Diagnostics.Debug.WriteLine($"[PlayerCharacter {mode}] maxSpeed={maxSpeed:F1}, force={floatForce:F1}, drag={floatDrag:F1}, gravity={gravityFactor:F2}");
            }

            // Handle swim mode entry - clamp velocity
            if (!_wasInSwimMode && Physics.CurrentJumpState != JumpState.Jumping)
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

            Physics.StepFloatMovement(
                inputX,
                inputY,
                maxSpeed,
                floatForce,
                floatDrag,
                entityMass,
                gravityFactor,
                tSec,
                Physics.IsUserFlying() ? FloatMode.Flying : FloatMode.Swimming);

            _wasInSwimMode = true;
        }

        private void TryJump(int currentTime)
        {
            string jumpRestrictionMessage = _jumpRestrictionMessageProvider?.Invoke();
            if (!string.IsNullOrWhiteSpace(jumpRestrictionMessage))
            {
                _onJumpRestricted?.Invoke(jumpRestrictionMessage);
                return;
            }

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

            if ((Physics.IsInSwimArea || Physics.IsUserFlying()) && !Physics.IsOnLadderOrRope)
            {
                if (Physics.IsOnFoothold())
                {
                    if (_jumpPressedThisFrame && CanTriggerFloatJump(currentTime))
                    {
                        Physics.JumpFromFloatFoothold();
                        _lastFloatJumpTime = currentTime;
                        State = Physics.IsUserFlying() ? PlayerState.Flying : PlayerState.Swimming;
                        _onJumpSound?.Invoke();
                    }
                    else
                    {
                        Physics.DetachFromFoothold();
                    }
                }
                else if (_jumpPressedThisFrame && CanTriggerFloatJump(currentTime) && Physics.IsSwimming())
                {
                    Physics.ApplySwimJumpImpulse();
                    _lastFloatJumpTime = currentTime;
                    State = PlayerState.Swimming;
                    _onJumpSound?.Invoke();
                }

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

        private bool CanTriggerFloatJump(int currentTime)
        {
            if (_lastFloatJumpTime == int.MinValue)
            {
                return true;
            }

            return currentTime - _lastFloatJumpTime >= FLOAT_JUMP_COOLDOWN_MS;
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
            if (Physics.TryGetLadderOrRope(X, Y, 50f, out LadderOrRopeInfo ladder))
            {
                Physics.GrabLadder(ladder.X, ladder.Top, ladder.Bottom, ladder.IsLadder);
                State = ladder.IsLadder ? PlayerState.Ladder : PlayerState.Rope;
            }
        }

        /// <summary>
        /// Try to grab a ladder/rope when pressing DOWN while on a platform.
        /// Official behavior: can grab rope if character Y > rope top (y1)
        /// </summary>
        private void TryGrabLadderDown()
        {
            if (!Physics.IsOnFoothold()) return;

            // Search slightly below the player's current position to find ropes that start at/below their feet
            // The player stands on a platform, and the rope typically starts at or just below the platform
            if (Physics.TryGetLadderOrRope(X, Y + 15, 15f, out LadderOrRopeInfo ladder))
            {
                // Can grab if character is at or above the rope's top (standing on platform above rope)
                if (Y <= ladder.Top + 10)
                {
                    Physics.GrabLadder(ladder.X, ladder.Top, ladder.Bottom, ladder.IsLadder);
                    State = ladder.IsLadder ? PlayerState.Ladder : PlayerState.Rope;
                }
            }
        }

        private void ExitLadderAtTop()
        {
            double exitY = Physics.LadderTop - 4;
            FootholdLine exitFoothold = _findFoothold?.Invoke(Physics.LadderX, (float)exitY, 24f);

            Physics.ReleaseLadder(yOverride: exitY);

            if (exitFoothold != null)
            {
                Physics.X = Physics.LadderX;
                Physics.LandOnFoothold(exitFoothold);
                Physics.VelocityX = 0;
                Physics.CurrentAction = MoveAction.Stand;
                State = PlayerState.Standing;
                return;
            }

            State = PlayerState.Falling;
        }

        private void CheckFootholdLanding()
        {
            if (_findFoothold == null || Physics.VelocityY <= 0) return;

            float previousY = Physics.HasSavedFloatState ? (float)Physics.SavedFloatY : Y;
            float downwardTravel = Math.Max(0f, Y - previousY);
            float searchRange = Math.Max(20f, downwardTravel + 8f);
            float probeY = Physics.HasSavedFloatState ? previousY : Y;
            var fh = _findFoothold(X, probeY, searchRange);

            if (fh != null)
            {
                float fhYAtX = (float)CalculateYOnFoothold(fh, X);
                bool crossedFoothold = !Physics.HasSavedFloatState || (previousY <= fhYAtX + 1f && Y >= fhYAtX - 1f);

                if (!crossedFoothold)
                {
                    return;
                }

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
                        : fhYAtX;
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
                    else if (Physics.IsUserFlying() && !Physics.IsOnFoothold())
                        State = PlayerState.Flying;
                    else if (Physics.IsSwimming() && !Physics.IsOnFoothold())
                        State = PlayerState.Swimming;
                    else if (Physics.IsAirborne())
                        State = Physics.VelocityY < 0 ? PlayerState.Jumping : PlayerState.Falling;
                    else
                        State = PlayerState.Standing;
                }
                return; // Don't process other state changes while in hit stun
            }

            ClearPendingLadderRegrab();

            // Determine state based on physics
            if (State == PlayerState.Attacking)
            {
                if (_sustainedSkillAnimation)
                {
                    return;
                }

                // Check if attack animation is complete
                var anim = Assembler?.GetAnimation(CurrentActionName);
                if (anim != null && anim.Length > 0)
                {
                    int attackDuration = 0;
                    foreach (var frame in anim)
                        attackDuration += frame.Duration;

                    if (currentTime - _animationStartTime >= attackDuration)
                    {
                        BeginMeleeAfterImageFade(currentTime);
                        ClearForcedActionName();
                        State = Physics.IsOnFoothold() ? PlayerState.Standing : PlayerState.Falling;
                        System.Diagnostics.Debug.WriteLine($"[UpdateStateMachine] Attack complete, returning to {State}");
                    }
                }
                else
                {
                    // No animation found - return to standing after short delay
                    if (currentTime - _animationStartTime >= 300)
                    {
                        ClearForcedActionName();
                        State = Physics.IsOnFoothold() ? PlayerState.Standing : PlayerState.Falling;
                        System.Diagnostics.Debug.WriteLine($"[UpdateStateMachine] No attack anim found for {CurrentActionName}, returning to {State}");
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
            else if (Physics.IsUserFlying() && !Physics.IsOnFoothold())
            {
                State = PlayerState.Flying;
            }
            else if (Physics.IsSwimming() && !Physics.IsOnFoothold())
            {
                State = PlayerState.Swimming;
            }
            else if (Physics.IsAirborne())
            {
                State = Physics.VelocityY < 0 ? PlayerState.Jumping : PlayerState.Falling;
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

            string newActionName;
            if (State == PlayerState.Attacking && !string.IsNullOrWhiteSpace(_forcedActionName))
            {
                newActionName = _forcedActionName;
            }
            else if (State == PlayerState.Sitting)
            {
                newActionName = GetPortableChairActionName(Build?.ActivePortableChair);
            }
            else
            {
                newActionName = GetSkillTransformActionName(State) ?? CharacterPart.GetActionString(newAction);
            }

            bool isFloatAction = IsFloatAnimationAction(newAction);
            bool isFloatMoving = isFloatAction && ShouldAnimateFloatAction();

            if (newAction != CurrentAction || !string.Equals(newActionName, CurrentActionName, StringComparison.Ordinal) || (isFloatAction && isFloatMoving != _isFloatAnimationMoving))
            {
                _animationStartTime = currentTime;
            }

            CurrentAction = newAction;
            CurrentActionName = newActionName;
            _isFloatAnimationMoving = isFloatMoving;
        }

        private bool IsFloatAnimationAction(CharacterAction action)
        {
            return action == CharacterAction.Swim || action == CharacterAction.Fly;
        }

        private bool ShouldAnimateFloatAction()
        {
            if (Physics.IsOnFoothold() || Physics.IsOnLadderOrRope)
            {
                return false;
            }

            bool hasDirectionalInput = _inputLeft || _inputRight || _inputUp || _inputDown || _inputJump;
            return hasDirectionalInput ||
                   Math.Abs(Physics.VelocityX) > FLOAT_ANIMATION_MOVEMENT_THRESHOLD ||
                   Math.Abs(Physics.VelocityY) > FLOAT_ANIMATION_MOVEMENT_THRESHOLD;
        }

        private int GetRenderAnimationTime(int currentTime)
        {
            if ((State == PlayerState.Rope || State == PlayerState.Ladder) && Math.Abs(Physics.VelocityY) < 0.001)
            {
                return 0;
            }

            if (IsFloatAnimationAction(CurrentAction) && !_isFloatAnimationMoving)
            {
                return 0;
            }

            return currentTime - _animationStartTime;
        }

        private void UpdateFaceExpression(int currentTime)
        {
            string expressionName = "default";

            if (State == PlayerState.Hit || currentTime < _hitExpressionEndTime)
            {
                expressionName = "hit";
            }
            else if (State != PlayerState.Dead)
            {
                if (_blinkExpressionEndTime > 0 && currentTime >= _blinkExpressionEndTime)
                {
                    _blinkExpressionEndTime = 0;
                    ScheduleNextBlink(currentTime);
                }

                if (_blinkExpressionEndTime == 0 && currentTime >= _nextBlinkTime)
                {
                    _blinkExpressionEndTime = currentTime + FACE_BLINK_DURATION_MS;
                }

                if (_blinkExpressionEndTime > 0 && currentTime < _blinkExpressionEndTime)
                {
                    expressionName = "blink";
                }
            }

            CurrentFaceExpressionName = expressionName;
            if (Assembler != null)
            {
                Assembler.FaceExpressionName = expressionName;
            }
        }

        private void ScheduleNextBlink(int currentTime)
        {
            _nextBlinkTime = currentTime + _faceExpressionRandom.Next(FACE_BLINK_MIN_INTERVAL_MS, FACE_BLINK_MAX_INTERVAL_MS + 1);
        }

        private void RecordMovementSync(int currentTime)
        {
            if (!Physics.IsRecordingPath)
            {
                Physics.StartPathRecording(currentTime);
                return;
            }

            Physics.MakeContinuousMovePath(currentTime);
        }

        public PlayerMovementSyncSnapshot GetMovementSyncSnapshot(int currentTime, bool flushPath = true)
        {
            RecordMovementSync(currentTime);

            PassivePositionSnapshot passivePosition = Physics.MakePassivePositionSnapshot(currentTime);
            var movePath = flushPath
                ? Physics.FlushMovePath(currentTime)
                : Physics.GetMovePathSnapshot(currentTime);

            if (movePath.Count == 0)
            {
                movePath = Physics.MakeMovePath(currentTime);
            }

            if (flushPath && Physics.IsRecordingPath)
            {
                Physics.StartPathRecording(currentTime);
            }

            return new PlayerMovementSyncSnapshot(passivePosition, movePath);
        }

        #endregion

        #region Combat

        private void StartAttack(int currentTime)
        {
            ClearPortableChair(standUp: false);
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
            CurrentActionName = CharacterPart.GetActionString(CurrentAction);
            _forcedActionName = null;

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
            ClearPortableChair(standUp: false);
            State = PlayerState.Attacking;
            _sustainedSkillAnimation = false;

            actionName = ResolveActiveSkillSpecificActionName(actionName);

            if (string.IsNullOrEmpty(actionName))
                actionName = "attack1";

            _forcedActionName = actionName;
            CurrentAction = GetCharacterActionForActionName(actionName);
            CurrentActionName = actionName;
            _activeMeleeAfterImage = null;

            _attackFrame = 0;
            _attackFrameTimer = 0;
            _animationStartTime = Environment.TickCount; // Set animation start time for completion check

            System.Diagnostics.Debug.WriteLine($"[TriggerSkillAnimation] actionName={actionName}, CurrentAction={CurrentActionName}, State={State}");
        }

        public void BeginSustainedSkillAnimation(string actionName)
        {
            if (string.IsNullOrEmpty(actionName))
                actionName = "attack1";

            ClearPortableChair(standUp: false);

            bool isSameAction = _sustainedSkillAnimation
                && State == PlayerState.Attacking
                && string.Equals(_forcedActionName, actionName, StringComparison.OrdinalIgnoreCase);

            State = PlayerState.Attacking;
            _sustainedSkillAnimation = true;
            _forcedActionName = actionName;
            CurrentAction = GetCharacterActionForActionName(actionName);
            CurrentActionName = actionName;
            _activeMeleeAfterImage = null;

            if (!isSameAction)
            {
                _attackFrame = 0;
                _attackFrameTimer = 0;
                _animationStartTime = Environment.TickCount;
            }
        }

        public void EndSustainedSkillAnimation()
        {
            _sustainedSkillAnimation = false;
        }

        public void ApplyMeleeAfterImage(int skillId, string actionName, MeleeAfterImageAction afterImageAction, int currentTime)
        {
            if (afterImageAction == null
                || string.IsNullOrWhiteSpace(actionName)
                || ((afterImageAction.FrameSets == null || afterImageAction.FrameSets.Count == 0) && !afterImageAction.HasRange))
            {
                _activeMeleeAfterImage = null;
                return;
            }

            _activeMeleeAfterImage = new MeleeAfterImageState
            {
                SkillId = skillId,
                ActionName = actionName,
                AfterImageAction = afterImageAction,
                AnimationStartTime = currentTime,
                FacingRight = FacingRight,
                ActionDuration = GetMeleeAfterImageActionDuration(actionName),
                FadeDuration = GetMeleeAfterImageFadeDuration(actionName)
            };
        }

        public void ClearMeleeAfterImage()
        {
            _activeMeleeAfterImage = null;
        }

        private void BeginMeleeAfterImageFade(int currentTime)
        {
            if (_activeMeleeAfterImage == null || _activeMeleeAfterImage.FadeStartTime >= 0)
            {
                return;
            }

            int animationTime = Math.Max(0, currentTime - _activeMeleeAfterImage.AnimationStartTime);
            int frameIndex = Assembler?.GetFrameIndexAtTime(_activeMeleeAfterImage.ActionName, animationTime) ?? -1;
            if (frameIndex >= 0)
            {
                _activeMeleeAfterImage.LastFrameIndex = frameIndex;
            }

            _activeMeleeAfterImage.FadeStartTime = currentTime;
        }

        private int GetMeleeAfterImageActionDuration(string actionName)
        {
            AssembledFrame[] animation = Assembler?.GetAnimation(actionName);
            if (animation == null || animation.Length == 0)
            {
                return 0;
            }

            int duration = 0;
            foreach (AssembledFrame frame in animation)
            {
                duration += Math.Max(0, frame?.Duration ?? 0);
            }

            return duration;
        }

        private int GetMeleeAfterImageFadeDuration(string actionName)
        {
            return Math.Max(MinimumMeleeAfterImageFadeDurationMs, GetMeleeAfterImageActionDuration(actionName) / 4);
        }

        public bool ApplySkillAvatarTransform(int skillId, string actionName, int morphTemplateId = 0)
        {
            if (!TryCreateSkillAvatarTransform(skillId, actionName, morphTemplateId, out SkillAvatarTransformState transform))
            {
                return false;
            }

            _activeSkillAvatarTransform = transform;
            UpdateAssemblerAvatarOverride();
            return true;
        }

        public bool ApplyExternalAvatarTransform(int sourceId, string actionName, int morphTemplateId = 0, int expirationTime = int.MaxValue)
        {
            if (!TryCreateExternalAvatarTransform(sourceId, actionName, morphTemplateId, out SkillAvatarTransformState transform))
            {
                return false;
            }

            _activeExternalAvatarTransform = transform;
            _activeExternalAvatarTransformExpiresAt = expirationTime > 0 ? expirationTime : int.MaxValue;
            UpdateAssemblerAvatarOverride();
            return true;
        }

        public void ClearSkillAvatarTransform()
        {
            _activeSkillAvatarTransform = null;
            UpdateAssemblerAvatarOverride();
        }

        public void ClearExternalAvatarTransform()
        {
            _activeExternalAvatarTransform = null;
            _activeExternalAvatarTransformExpiresAt = int.MaxValue;
            UpdateAssemblerAvatarOverride();
        }

        public void ClearSkillAvatarTransformAndPlayExitAction()
        {
            if (_activeSkillAvatarTransform == null)
            {
                return;
            }

            string exitActionName = _activeSkillAvatarTransform.ExitActionName;
            ClearSkillAvatarTransform();

            if (!string.IsNullOrWhiteSpace(exitActionName) && IsAlive)
            {
                TriggerSkillAnimation(exitActionName);
            }
        }

        public void ClearSkillAvatarTransform(int skillId)
        {
            if (_activeSkillAvatarTransform != null && _activeSkillAvatarTransform.SkillId == skillId)
            {
                ClearSkillAvatarTransformAndPlayExitAction();
            }
        }

        public void ClearExternalAvatarTransform(int sourceId)
        {
            if (_activeExternalAvatarTransform != null && _activeExternalAvatarTransform.SourceId == sourceId)
            {
                ClearExternalAvatarTransform();
            }
        }

        public bool HasSkillAvatarTransform(int skillId)
        {
            return _activeSkillAvatarTransform != null && _activeSkillAvatarTransform.SkillId == skillId;
        }

        public bool HasExternalAvatarTransform(int sourceId)
        {
            return _activeExternalAvatarTransform != null && _activeExternalAvatarTransform.SourceId == sourceId;
        }

        private void ExpireExternalAvatarTransformIfNeeded(int currentTime)
        {
            if (_activeExternalAvatarTransform == null ||
                _activeExternalAvatarTransformExpiresAt == int.MaxValue ||
                currentTime < _activeExternalAvatarTransformExpiresAt)
            {
                return;
            }

            ClearExternalAvatarTransform();
        }

        public bool ApplyShadowPartner(int skillId, SkillData skill, int currentTime)
        {
            if (skillId <= 0 || skill?.HasShadowPartnerActionAnimations != true)
            {
                return false;
            }

            string resolvedActionName = ResolveShadowPartnerActionName(skill.ShadowPartnerActionAnimations, CurrentActionName, null);
            string spawnActionName = ResolveShadowPartnerCreateActionName(skill.ShadowPartnerActionAnimations);
            bool useSpawnAction = !string.IsNullOrWhiteSpace(spawnActionName);

            _activeShadowPartner = new ShadowPartnerState
            {
                SkillId = skillId,
                ActionAnimations = skill.ShadowPartnerActionAnimations,
                HorizontalOffsetPx = skill.ShadowPartnerHorizontalOffsetPx,
                CurrentActionName = useSpawnAction ? spawnActionName : resolvedActionName,
                CurrentActionStartTime = currentTime,
                CurrentFacingRight = FacingRight,
                ObservedPlayerActionName = CurrentActionName,
                QueuedActionName = useSpawnAction ? resolvedActionName : null,
                QueuedFacingRight = FacingRight
            };

            return !string.IsNullOrWhiteSpace(_activeShadowPartner.CurrentActionName);
        }

        public void ClearShadowPartner(int skillId)
        {
            if (_activeShadowPartner != null && _activeShadowPartner.SkillId == skillId)
            {
                _activeShadowPartner = null;
            }
        }

        public bool ApplySkillAvatarEffect(int skillId, SkillData skill, int currentTime)
        {
            if (!TryCreateSkillAvatarEffect(skillId, skill, out SkillAvatarEffectState effectState))
            {
                return false;
            }

            ClearSkillAvatarEffect(skillId, currentTime, playFinish: false);

            effectState.Mode = GetCurrentSkillAvatarEffectMode();
            effectState.AnimationStartTime = currentTime;
            _activeSkillAvatarEffects.Add(effectState);
            return true;
        }

        public void ClearSkillAvatarEffect(int skillId, int currentTime)
        {
            ClearSkillAvatarEffect(skillId, currentTime, playFinish: true);
        }

        public bool HasSkillAvatarEffect(int skillId)
        {
            return _activeSkillAvatarEffects.Exists(effectState => effectState.SkillId == skillId);
        }

        public bool ApplyTransientSkillAvatarEffect(int skillId, SkillAnimation animation, SkillAnimation secondaryAnimation, int currentTime)
        {
            bool hasPrimaryAnimation = animation?.Frames != null && animation.Frames.Count > 0;
            bool hasSecondaryAnimation = secondaryAnimation?.Frames != null && secondaryAnimation.Frames.Count > 0;
            if (skillId <= 0 || (!hasPrimaryAnimation && !hasSecondaryAnimation))
            {
                return false;
            }

            ClearTransientSkillAvatarEffect(skillId);
            _transientSkillAvatarEffects.Add(new TransientSkillAvatarEffectState
            {
                SkillId = skillId,
                Animation = animation,
                SecondaryAnimation = secondaryAnimation,
                AnimationStartTime = currentTime,
                Plane = ResolveTransientSkillAvatarEffectPlane(animation),
                SecondaryPlane = ResolveTransientSkillAvatarEffectPlane(secondaryAnimation)
            });
            return true;
        }

        public bool HasTransientSkillAvatarEffect(int skillId)
        {
            return _transientSkillAvatarEffects.Exists(effectState => effectState.SkillId == skillId);
        }

        public void SetSkillAvatarEffectRenderSuppressed(int skillId, bool suppressed)
        {
            if (skillId <= 0)
            {
                return;
            }

            if (suppressed)
            {
                _skillAvatarEffectRenderSuppressionSkillIds.Add(skillId);
            }
            else
            {
                _skillAvatarEffectRenderSuppressionSkillIds.Remove(skillId);
            }
        }

        public void NotifyTamingMobOwnershipHandledExternally()
        {
            _suppressAutomaticTamingMobTransition = true;
            _observedTamingMobPart = GetEquippedTamingMobPart();
            _observedClientOwnedTamingMobActive = IsClientOwnedVehicleTamingMobStateActive(_observedTamingMobPart);
            SetTransientTamingMobOverride(null);
        }

        public void SetClientOwnedVehicleTamingMobState(CharacterPart mountPart, bool isActive)
        {
            _clientOwnedVehicleTamingMobPart = mountPart?.Slot == EquipSlot.TamingMob ? mountPart : null;
            _clientOwnedVehicleTamingMobActive = isActive && _clientOwnedVehicleTamingMobPart != null;
        }

        public void ClearAllSkillAvatarEffects(bool playFinish, int currentTime)
        {
            for (int i = _activeSkillAvatarEffects.Count - 1; i >= 0; i--)
            {
                SkillAvatarEffectState effectState = _activeSkillAvatarEffects[i];
                if (playFinish && TryBeginSkillAvatarEffectFinish(effectState, currentTime))
                {
                    continue;
                }

                _activeSkillAvatarEffects.RemoveAt(i);
            }
        }

        public void ClearSkillAvatarEffect(int skillId, int currentTime, bool playFinish)
        {
            for (int i = _activeSkillAvatarEffects.Count - 1; i >= 0; i--)
            {
                SkillAvatarEffectState effectState = _activeSkillAvatarEffects[i];
                if (effectState.SkillId != skillId)
                {
                    continue;
                }

                if (playFinish && TryBeginSkillAvatarEffectFinish(effectState, currentTime))
                {
                    continue;
                }

                _activeSkillAvatarEffects.RemoveAt(i);
            }
        }

        private void UpdateAvatarEffects(int currentTime)
        {
            for (int i = _transientSkillAvatarEffects.Count - 1; i >= 0; i--)
            {
                TransientSkillAvatarEffectState transientEffect = _transientSkillAvatarEffects[i];
                if (transientEffect == null)
                {
                    _transientSkillAvatarEffects.RemoveAt(i);
                    continue;
                }

                int elapsedTime = Math.Max(0, currentTime - transientEffect.AnimationStartTime);
                bool primaryComplete = transientEffect.Animation == null || transientEffect.Animation.IsComplete(elapsedTime);
                bool secondaryComplete = transientEffect.SecondaryAnimation == null || transientEffect.SecondaryAnimation.IsComplete(elapsedTime);
                if (primaryComplete && secondaryComplete)
                {
                    _transientSkillAvatarEffects.RemoveAt(i);
                }
            }

            if (_activeSkillAvatarEffects.Count == 0)
            {
                return;
            }

            SkillAvatarEffectMode currentMode = GetCurrentSkillAvatarEffectMode();
            for (int i = _activeSkillAvatarEffects.Count - 1; i >= 0; i--)
            {
                SkillAvatarEffectState effectState = _activeSkillAvatarEffects[i];
                if (effectState.IsFinishing)
                {
                    if (IsSkillAvatarEffectAnimationComplete(effectState, currentTime))
                    {
                        _activeSkillAvatarEffects.RemoveAt(i);
                    }

                    continue;
                }

                if (effectState.Mode == currentMode)
                {
                    continue;
                }

                bool hasCurrentModeAnimation = HasSkillAvatarEffectAnimationForMode(effectState, currentMode);
                bool hasPreviousModeAnimation = HasSkillAvatarEffectAnimationForMode(effectState, effectState.Mode);
                effectState.Mode = currentMode;

                if (hasCurrentModeAnimation || hasPreviousModeAnimation)
                {
                    effectState.AnimationStartTime = currentTime;
                }
            }
        }

        private SkillAvatarEffectMode GetCurrentSkillAvatarEffectMode()
        {
            return Physics.IsOnLadderOrRope || State == PlayerState.Ladder || State == PlayerState.Rope
                ? SkillAvatarEffectMode.LadderOrRope
                : SkillAvatarEffectMode.Ground;
        }

        private static bool HasSkillAvatarEffectAnimationForMode(SkillAvatarEffectState effectState, SkillAvatarEffectMode mode)
        {
            if (effectState == null)
            {
                return false;
            }

            if (mode == SkillAvatarEffectMode.LadderOrRope)
            {
                if (effectState.HideOnLadderOrRope)
                {
                    return false;
                }

                return effectState.LadderOverlayAnimation != null
                       || effectState.GroundOverlayAnimation != null
                       || effectState.GroundOverlaySecondaryAnimation != null
                       || effectState.GroundUnderFaceAnimation != null
                       || effectState.GroundUnderFaceSecondaryAnimation != null;
            }

            return effectState.GroundOverlayAnimation != null
                   || effectState.GroundOverlaySecondaryAnimation != null
                   || effectState.GroundUnderFaceAnimation != null
                   || effectState.GroundUnderFaceSecondaryAnimation != null;
        }

        private static bool TryCreateSkillAvatarEffect(int skillId, SkillData skill, out SkillAvatarEffectState effectState)
        {
            effectState = null;
            if (skill?.HasPersistentAvatarEffect != true)
            {
                return false;
            }

            effectState = new SkillAvatarEffectState
            {
                SkillId = skillId,
                GroundOverlayAnimation = skill.AvatarOverlayEffect,
                GroundOverlaySecondaryAnimation = skill.AvatarOverlaySecondaryEffect,
                GroundUnderFaceAnimation = skill.AvatarUnderFaceEffect,
                GroundUnderFaceSecondaryAnimation = skill.AvatarUnderFaceSecondaryEffect,
                LadderOverlayAnimation = skill.AvatarLadderEffect,
                GroundOverlayFinishAnimation = skill.AvatarOverlayFinishEffect,
                GroundUnderFaceFinishAnimation = skill.AvatarUnderFaceFinishEffect,
                LadderOverlayFinishAnimation = skill.AvatarLadderFinishEffect,
                HideOnLadderOrRope = skill.HideAvatarEffectOnLadderOrRope
            };

            return effectState.HasLoopAnimation || effectState.HasFinishAnimation;
        }

        private bool TryBeginSkillAvatarEffectFinish(SkillAvatarEffectState effectState, int currentTime)
        {
            if (effectState == null || effectState.IsFinishing)
            {
                return false;
            }

            SkillAvatarEffectMode finishMode = GetCurrentSkillAvatarEffectMode();
            if (!HasSkillAvatarEffectFinishForMode(effectState, finishMode))
            {
                finishMode = effectState.Mode;
            }

            if (!HasSkillAvatarEffectFinishForMode(effectState, finishMode))
            {
                return false;
            }

            effectState.Mode = finishMode;
            effectState.IsFinishing = true;
            effectState.AnimationStartTime = currentTime;
            return true;
        }

        private static bool HasSkillAvatarEffectFinishForMode(SkillAvatarEffectState effectState, SkillAvatarEffectMode mode)
        {
            if (effectState == null)
            {
                return false;
            }

            if (mode == SkillAvatarEffectMode.LadderOrRope)
            {
                if (effectState.HideOnLadderOrRope)
                {
                    return false;
                }

                return effectState.LadderOverlayFinishAnimation != null
                       || effectState.GroundOverlayFinishAnimation != null
                       || effectState.GroundUnderFaceFinishAnimation != null;
            }

            return effectState.GroundOverlayFinishAnimation != null
                   || effectState.GroundUnderFaceFinishAnimation != null;
        }

        private static bool IsSkillAvatarEffectAnimationComplete(SkillAvatarEffectState effectState, int currentTime)
        {
            if (effectState == null || !effectState.IsFinishing)
            {
                return true;
            }

            int elapsedTime = Math.Max(0, currentTime - effectState.AnimationStartTime);
            bool anyAnimation = false;

            foreach (SkillAnimation animation in GetSkillAvatarEffectAnimations(effectState))
            {
                if (animation == null)
                {
                    continue;
                }

                anyAnimation = true;
                if (!animation.IsComplete(elapsedTime))
                {
                    return false;
                }
            }

            return anyAnimation;
        }

        private static IEnumerable<SkillAnimation> GetSkillAvatarEffectAnimations(SkillAvatarEffectState effectState)
        {
            if (effectState == null)
            {
                yield break;
            }

            if (effectState.IsFinishing)
            {
                if (effectState.Mode == SkillAvatarEffectMode.LadderOrRope)
                {
                    if (effectState.LadderOverlayFinishAnimation != null)
                    {
                        yield return effectState.LadderOverlayFinishAnimation;
                        yield break;
                    }
                }

                if (effectState.GroundOverlayFinishAnimation != null)
                {
                    yield return effectState.GroundOverlayFinishAnimation;
                }

                if (effectState.Mode != SkillAvatarEffectMode.LadderOrRope
                    && effectState.GroundUnderFaceFinishAnimation != null)
                {
                    yield return effectState.GroundUnderFaceFinishAnimation;
                }

                yield break;
            }

            if (effectState.Mode == SkillAvatarEffectMode.LadderOrRope && effectState.LadderOverlayAnimation != null)
            {
                yield return effectState.LadderOverlayAnimation;
                yield break;
            }

            if (effectState.Mode == SkillAvatarEffectMode.LadderOrRope && effectState.HideOnLadderOrRope)
            {
                yield break;
            }

            if (effectState.GroundOverlayAnimation != null)
            {
                yield return effectState.GroundOverlayAnimation;
            }

            if (effectState.Mode != SkillAvatarEffectMode.LadderOrRope
                && effectState.GroundUnderFaceAnimation != null)
            {
                yield return effectState.GroundUnderFaceAnimation;
            }
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
            ClearPortableChair(standUp: false);
            State = PlayerState.Hit;
            _hitStateStartTime = Environment.TickCount;
            _hitExpressionEndTime = _hitStateStartTime + FACE_HIT_EXPRESSION_DURATION_MS;
            Physics.CurrentAction = MoveAction.Hit;
            CacheLadderStateForRegrab();

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
            ClearPortableChair(standUp: false);
            State = PlayerState.Hit;
            _hitStateStartTime = Environment.TickCount;
            _hitExpressionEndTime = _hitStateStartTime + FACE_HIT_EXPRESSION_DURATION_MS;
            Physics.CurrentAction = MoveAction.Hit;
            CacheLadderStateForRegrab();

            // Use Impact for immediate knockback
            Physics.Impact(knockbackX, knockbackY);
        }

        private void CacheLadderStateForRegrab()
        {
            if (!Physics.IsOnLadderOrRope)
            {
                ClearPendingLadderRegrab();
                return;
            }

            _pendingLadderRegrab = true;
            _pendingLadderX = Physics.LadderX;
            _pendingLadderTop = Physics.LadderTop;
            _pendingLadderBottom = Physics.LadderBottom;
            _pendingLadderIsLadder = Physics.IsLadder;
        }

        private void ClearPendingLadderRegrab()
        {
            _pendingLadderRegrab = false;
            _pendingLadderX = 0;
            _pendingLadderTop = 0;
            _pendingLadderBottom = 0;
            _pendingLadderIsLadder = false;
        }

        private bool TryRegrabLadderWhileHoldingUp()
        {
            if (State != PlayerState.Hit || !_pendingLadderRegrab || !_inputUp)
            {
                return false;
            }

            const float regrabHorizontalTolerance = 18f;
            const float regrabVerticalTolerance = 6f;

            bool foundLadder = Physics.TryGetLadderOrRope(X, Y, regrabHorizontalTolerance, out LadderOrRopeInfo ladder);
            if (!foundLadder &&
                Math.Abs(X - _pendingLadderX) <= regrabHorizontalTolerance &&
                Y >= _pendingLadderTop - regrabVerticalTolerance &&
                Y <= _pendingLadderBottom + regrabVerticalTolerance)
            {
                ladder = new LadderOrRopeInfo(_pendingLadderX, _pendingLadderTop, _pendingLadderBottom, _pendingLadderIsLadder);
                foundLadder = true;
            }

            if (!foundLadder ||
                Y < ladder.Top - regrabVerticalTolerance ||
                Y > ladder.Bottom + regrabVerticalTolerance)
            {
                return false;
            }

            Physics.GrabLadder(ladder.X, ladder.Top, ladder.Bottom, ladder.IsLadder);
            State = ladder.IsLadder ? PlayerState.Ladder : PlayerState.Rope;
            ClearPendingLadderRegrab();
            return true;
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

            ClearPortableChair(standUp: false);
            ClearSkillBlockingStatuses();
            HP = 0;
            State = PlayerState.Dead;
            CurrentAction = CharacterAction.Dead;
            CurrentActionName = CharacterPart.GetActionString(CharacterAction.Dead);
            ClearForcedActionName();
            ClearSkillAvatarTransform();
            ClearAllSkillAvatarEffects(playFinish: false, Environment.TickCount);
            ClearAllTransientSkillAvatarEffects();
            _blinkExpressionEndTime = 0;
            _hitExpressionEndTime = 0;
            CurrentFaceExpressionName = "default";

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
            ClearPortableChair(standUp: false);
            ClearSkillBlockingStatuses();
            HP = MaxHP;
            MP = MaxMP;
            State = PlayerState.Standing;
            CurrentAction = CharacterAction.Stand1;
            CurrentActionName = CharacterPart.GetActionString(CharacterAction.Stand1);
            ClearForcedActionName();
            ClearSkillAvatarTransform();
            ClearAllSkillAvatarEffects(playFinish: false, Environment.TickCount);
            ClearAllTransientSkillAvatarEffects();
            _blinkExpressionEndTime = 0;
            _hitExpressionEndTime = 0;
            CurrentFaceExpressionName = "default";
            ScheduleNextBlink(Environment.TickCount);
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

            ClearPortableChair(standUp: false);
            State = PlayerState.Standing;
            CurrentAction = CharacterAction.Stand1;
            CurrentActionName = CharacterPart.GetActionString(CharacterAction.Stand1);
            ClearForcedActionName();
            Physics.VelocityX = 0;

            // Clear input states to prevent resuming movement
            _inputLeft = false;
            _inputRight = false;
            _inputUp = false;
            _inputDown = false;
            _inputJump = false;
        }

        private void ClearExpiredSkillBlockingStatuses(int currentTime)
        {
            if (_activeSkillBlockingStatuses.Count == 0)
            {
                return;
            }

            List<PlayerSkillBlockingStatus> expiredStatuses = null;
            foreach (KeyValuePair<PlayerSkillBlockingStatus, PlayerSkillBlockingStatusState> entry in _activeSkillBlockingStatuses)
            {
                if (currentTime < entry.Value.ExpireTime)
                {
                    continue;
                }

                expiredStatuses ??= new List<PlayerSkillBlockingStatus>();
                expiredStatuses.Add(entry.Key);
            }

            if (expiredStatuses == null)
            {
                return;
            }

            foreach (PlayerSkillBlockingStatus status in expiredStatuses)
            {
                _activeSkillBlockingStatuses.Remove(status);
            }
        }

        #endregion

        #region Draw

        /// <summary>
        /// Draw the player
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer,
            int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime,
            Action drawUnderFaceOverlay = null)
        {
            int screenX = (int)X - mapShiftX + centerX;
            int screenY = (int)Y - mapShiftY + centerY;

            // If no assembler (placeholder player), skip drawing - debug box handles it
            if (Assembler == null)
                return;

            // Get current frame
            int animTime = GetRenderAnimationTime(currentTime);

            var frame = Assembler.GetFrameAtTime(CurrentActionName, animTime);

            if (frame != null)
            {
                DrawPortableChairCoupleMidpointEffects(
                    spriteBatch,
                    skeletonRenderer,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    currentTime,
                    drawFrontLayers: false);

                DrawPortableChairPairPreview(
                    spriteBatch,
                    skeletonRenderer,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    currentTime);

                // Apply hit flash effect
                Color tint = Color.White;
                if (State == PlayerState.Hit)
                {
                    float flash = (float)Math.Sin(currentTime * 0.02) * 0.5f + 0.5f;
                    tint = Color.Lerp(Color.White, Color.Red, flash);
                }

                DrawFrameWithAvatarEffects(
                    spriteBatch,
                    skeletonRenderer,
                    frame,
                    screenX,
                    screenY,
                    tint,
                    currentTime,
                    drawUnderFaceOverlay);

                DrawPortableChairCoupleMidpointEffects(
                    spriteBatch,
                    skeletonRenderer,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    currentTime,
                    drawFrontLayers: true);
            }
            else
            {
                // Fallback: draw simple rectangle
                var rect = new Rectangle(screenX - 15, screenY - 60, 30, 60);
                spriteBatch.Draw(GetPixelTexture(spriteBatch.GraphicsDevice), rect, Color.Blue * 0.5f);
            }
        }

        public void TakeStatusDamage(int damage)
        {
            if (!IsAlive || GodMode || damage <= 0)
            {
                return;
            }

            HP -= damage;
            OnDamaged?.Invoke(this, damage);

            if (HP <= 0)
            {
                Die();
            }
        }

        public void ApplyMobRecoveryModifiers(bool hpRecoveryReversed, int maxHpPercentCap, int maxMpPercentCap, int hpRecoveryDamagePercent = 100)
        {
            _mobUndeadRecoveryActive = hpRecoveryReversed;
            _mobHpRecoveryCapPercent = Math.Clamp(maxHpPercentCap, 1, 100);
            _mobMpRecoveryCapPercent = Math.Clamp(maxMpPercentCap, 1, 100);
            _mobHpRecoveryDamagePercent = Math.Clamp(hpRecoveryDamagePercent, 1, 100);

            int hpCap = Math.Max(1, MaxHP * _mobHpRecoveryCapPercent / 100);
            int mpCap = Math.Max(0, MaxMP * _mobMpRecoveryCapPercent / 100);
            if (HP > hpCap)
            {
                HP = hpCap;
            }

            if (MP > mpCap)
            {
                MP = mpCap;
            }
        }

        public void Recover(int hp, int mp)
        {
            if (!IsAlive)
            {
                return;
            }

            if (hp > 0)
            {
                if (_mobUndeadRecoveryActive)
                {
                    int reflectedDamage = Math.Max(1, (int)Math.Ceiling(hp * (_mobHpRecoveryDamagePercent / 100f)));
                    TakeStatusDamage(reflectedDamage);
                }
                else
                {
                    int hpCap = Math.Max(1, MaxHP * _mobHpRecoveryCapPercent / 100);
                    HP = Math.Min(hpCap, HP + hp);
                }
            }

            if (mp > 0)
            {
                int mpCap = Math.Max(0, MaxMP * _mobMpRecoveryCapPercent / 100);
                MP = Math.Min(mpCap, MP + mp);
            }
        }

        private void DrawPortableChairPairPreview(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int currentTime)
        {
            if (_portableChairPairAssembler == null
                || string.IsNullOrWhiteSpace(_portableChairPairActionName)
                || Build?.ActivePortableChair?.IsCoupleChair != true
                || _portableChairExternalPairRequested
                || !ShouldDrawPortableChairPairPreview())
            {
                return;
            }

            int partnerScreenX = (int)(X + _portableChairPairOffset.X) - mapShiftX + centerX;
            int partnerScreenY = (int)(Y + _portableChairPairOffset.Y) - mapShiftY + centerY;
            AssembledFrame partnerFrame = _portableChairPairAssembler.GetFrameAtTime(_portableChairPairActionName, GetRenderAnimationTime(currentTime));
            if (partnerFrame == null)
            {
                return;
            }

            int adjustedY = partnerScreenY - partnerFrame.FeetOffset;
            for (int i = 0; i < partnerFrame.Parts.Count; i++)
            {
                DrawAssembledPart(spriteBatch, skeletonRenderer, partnerFrame.Parts[i], partnerScreenX, adjustedY, _portableChairPairFacingRight, Color.White);
            }
        }

        internal bool TryResolvePortableChairExternalPairLayerState(
            bool partnerFacingRight,
            float partnerX,
            float partnerY,
            out PortableChair chair)
        {
            chair = Build?.ActivePortableChair;
            if (chair?.IsCoupleChair != true
                || !_portableChairExternalPairRequested
                || !_portableChairHasExternalPair)
            {
                chair = null;
                return false;
            }

            return IsPortableChairActualPairActive(
                chair,
                FacingRight,
                X,
                Y,
                partnerFacingRight,
                partnerX,
                partnerY);
        }

        private void DrawPortableChairCoupleMidpointEffects(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int currentTime,
            bool drawFrontLayers)
        {
            PortableChair chair = Build?.ActivePortableChair;
            if (chair?.IsCoupleChair != true
                || chair.CoupleMidpointLayers == null
                || chair.CoupleMidpointLayers.Count == 0)
            {
                return;
            }

            float partnerX;
            float partnerY;
            if (_portableChairExternalPairRequested)
            {
                if (!_portableChairHasExternalPair)
                {
                    return;
                }

                partnerX = _portableChairExternalPairPosition.X;
                partnerY = _portableChairExternalPairPosition.Y;
            }
            else
            {
                if (_portableChairPairAssembler == null || !ShouldDrawPortableChairPairPreview())
                {
                    return;
                }

                partnerX = X + _portableChairPairOffset.X;
                partnerY = Y + _portableChairPairOffset.Y;
            }

            int midpointScreenX = (int)Math.Round((X + partnerX) * 0.5f) - mapShiftX + centerX;
            int midpointScreenY = (int)Math.Round((Y + partnerY) * 0.5f) - mapShiftY + centerY;
            int animationTime = GetRenderAnimationTime(currentTime);

            for (int i = 0; i < chair.CoupleMidpointLayers.Count; i++)
            {
                PortableChairLayer layer = chair.CoupleMidpointLayers[i];
                if ((layer.RelativeZ > 0) != drawFrontLayers)
                {
                    continue;
                }

                CharacterFrame frame = GetPortableChairLayerFrameAtTime(layer, animationTime);
                DrawPortableChairLayerFrame(spriteBatch, skeletonRenderer, frame, midpointScreenX, midpointScreenY, FacingRight);
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

        private void DrawFrameWithAvatarEffects(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            AssembledFrame frame,
            int screenX,
            int screenY,
            Color tint,
            int currentTime,
            Action drawUnderFaceOverlay)
        {
            if (frame == null)
            {
                return;
            }

            UpdateShadowPartnerRenderState(currentTime);
            List<AvatarEffectRenderable> avatarEffects = GetCurrentAvatarEffectRenderables(currentTime);
            int adjustedY = screenY - frame.FeetOffset;

            DrawShadowPartner(spriteBatch, skeletonRenderer, screenX, screenY, currentTime);
            DrawAvatarEffectPlane(spriteBatch, skeletonRenderer, avatarEffects, SkillAvatarEffectPlane.BehindCharacter, screenX, screenY, tint);
            DrawMeleeAfterImage(spriteBatch, skeletonRenderer, screenX, screenY, tint, currentTime);

            int underFaceInsertionIndex = GetUnderFaceInsertionIndex(frame.Parts);
            bool underFaceDrawn = avatarEffects.Count == 0;

            for (int i = 0; i < frame.Parts.Count; i++)
            {
                if (!underFaceDrawn && i == underFaceInsertionIndex)
                {
                    drawUnderFaceOverlay?.Invoke();
                    DrawAvatarEffectPlane(spriteBatch, skeletonRenderer, avatarEffects, SkillAvatarEffectPlane.UnderFace, screenX, screenY, tint);
                    underFaceDrawn = true;
                }

                DrawAssembledPart(spriteBatch, skeletonRenderer, frame.Parts[i], screenX, adjustedY, FacingRight, tint);
            }

            if (!underFaceDrawn)
            {
                drawUnderFaceOverlay?.Invoke();
                DrawAvatarEffectPlane(spriteBatch, skeletonRenderer, avatarEffects, SkillAvatarEffectPlane.UnderFace, screenX, screenY, tint);
            }

            DrawAvatarEffectPlane(spriteBatch, skeletonRenderer, avatarEffects, SkillAvatarEffectPlane.OverCharacter, screenX, screenY, tint);
        }

        private void DrawMeleeAfterImage(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            int screenX,
            int screenY,
            Color tint,
            int currentTime)
        {
            if (_activeMeleeAfterImage?.AfterImageAction?.FrameSets == null
                || _activeMeleeAfterImage.AfterImageAction.FrameSets.Count == 0)
            {
                return;
            }

            bool activeAction = _activeMeleeAfterImage.FadeStartTime < 0
                && State == PlayerState.Attacking
                && string.Equals(CurrentActionName, _activeMeleeAfterImage.ActionName, StringComparison.OrdinalIgnoreCase);
            if (!activeAction)
            {
                BeginMeleeAfterImageFade(currentTime);
            }
            else if (_activeMeleeAfterImage.ActionDuration > 0
                     && currentTime - _activeMeleeAfterImage.AnimationStartTime >= _activeMeleeAfterImage.ActionDuration)
            {
                BeginMeleeAfterImageFade(currentTime);
                activeAction = false;
            }

            int frameIndex = _activeMeleeAfterImage.LastFrameIndex;
            float alpha = 1f;
            if (activeAction)
            {
                int animationTime = Math.Max(0, currentTime - _activeMeleeAfterImage.AnimationStartTime);
                frameIndex = Assembler?.GetFrameIndexAtTime(_activeMeleeAfterImage.ActionName, animationTime) ?? -1;
                if (frameIndex >= 0)
                {
                    _activeMeleeAfterImage.LastFrameIndex = frameIndex;
                }
            }
            else if (_activeMeleeAfterImage.FadeStartTime >= 0)
            {
                int fadeElapsed = Math.Max(0, currentTime - _activeMeleeAfterImage.FadeStartTime);
                if (fadeElapsed >= _activeMeleeAfterImage.FadeDuration)
                {
                    _activeMeleeAfterImage = null;
                    return;
                }

                alpha = 1f - (fadeElapsed / (float)Math.Max(1, _activeMeleeAfterImage.FadeDuration));
            }

            if (frameIndex < 0
                || !_activeMeleeAfterImage.AfterImageAction.FrameSets.TryGetValue(frameIndex, out MeleeAfterImageFrameSet frameSet)
                || frameSet?.Frames == null)
            {
                return;
            }

            Color frameTint = tint * MathHelper.Clamp(alpha, 0f, 1f);
            foreach (SkillFrame frame in frameSet.Frames)
            {
                if (frame?.Texture == null)
                {
                    continue;
                }

                bool shouldFlip = _activeMeleeAfterImage.FacingRight ^ frame.Flip;
                int drawX = shouldFlip
                    ? screenX - (frame.Texture.Width - frame.Origin.X)
                    : screenX - frame.Origin.X;
                int drawY = screenY - frame.Origin.Y;
                frame.Texture.DrawBackground(spriteBatch, skeletonRenderer, null, drawX, drawY, frameTint, shouldFlip, null);
            }
        }

        private static void DrawAssembledPart(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            AssembledPart part,
            int screenX,
            int adjustedY,
            bool flip,
            Color tint)
        {
            if (part?.Texture == null || !part.IsVisible)
            {
                return;
            }

            int partX;
            int partY = adjustedY + part.OffsetY;
            partX = flip
                ? screenX - part.OffsetX - part.Texture.Width
                : screenX + part.OffsetX;

            Color partColor = part.Tint != Color.White ? part.Tint : tint;
            part.Texture.DrawBackground(spriteBatch, skeletonRenderer, null, partX, partY, partColor, flip, null);
        }

        internal static CharacterFrame GetPortableChairLayerFrameAtTime(PortableChairLayer layer, int timeMs)
        {
            if (layer?.Animation == null)
            {
                return null;
            }

            return layer.Animation.GetFrameAtTime(timeMs, out _);
        }

        internal static void DrawPortableChairLayers(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            IEnumerable<PortableChairLayer> layers,
            int screenX,
            int screenY,
            bool flip,
            int timeMs,
            bool drawFrontLayers)
        {
            if (layers == null)
            {
                return;
            }

            foreach (PortableChairLayer layer in layers)
            {
                if (layer == null || (layer.RelativeZ > 0) != drawFrontLayers)
                {
                    continue;
                }

                CharacterFrame frame = GetPortableChairLayerFrameAtTime(layer, timeMs);
                DrawPortableChairLayerFrame(spriteBatch, skeletonRenderer, frame, screenX, screenY, flip);
            }
        }

        internal static void DrawPortableChairLayerFrame(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            CharacterFrame frame,
            int screenX,
            int screenY,
            bool flip)
        {
            if (frame?.Texture == null)
            {
                return;
            }

            int drawX = flip
                ? screenX + frame.Origin.X - frame.Texture.Width
                : screenX - frame.Origin.X;
            int drawY = screenY - frame.Origin.Y;
            frame.Texture.DrawBackground(spriteBatch, skeletonRenderer, null, drawX, drawY, Color.White, flip, null);
        }

        private void DrawShadowPartner(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            int screenX,
            int screenY,
            int currentTime)
        {
            if (_activeShadowPartner?.ActionAnimations == null)
            {
                return;
            }

            if (!TryGetShadowPartnerAnimation(currentTime, out SkillAnimation animation, out int animationTime, out bool facingRight))
            {
                return;
            }

            SkillFrame frame = animation.GetFrameAtTime(animationTime);
            if (frame?.Texture == null)
            {
                return;
            }

            bool flip = facingRight ^ frame.Flip;
            int horizontalOffsetPx = ResolveShadowPartnerHorizontalOffsetPx(animation);
            int drawX = screenX + (facingRight ? -horizontalOffsetPx : horizontalOffsetPx);
            drawX = flip
                ? drawX - (frame.Texture.Width - frame.Origin.X)
                : drawX - frame.Origin.X;

            int drawY = screenY - frame.Origin.Y;
            frame.Texture.DrawBackground(spriteBatch, skeletonRenderer, null, drawX, drawY, ShadowPartnerTint, flip, null);
        }

        private bool TryGetShadowPartnerAnimation(int currentTime, out SkillAnimation animation, out int animationTime, out bool facingRight)
        {
            animation = null;
            animationTime = 0;
            facingRight = FacingRight;

            if (_activeShadowPartner?.ActionAnimations == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(_activeShadowPartner.CurrentActionName)
                || !_activeShadowPartner.ActionAnimations.TryGetValue(_activeShadowPartner.CurrentActionName, out animation)
                || animation?.Frames == null
                || animation.Frames.Count == 0)
            {
                return false;
            }

            animationTime = Math.Max(0, currentTime - _activeShadowPartner.CurrentActionStartTime);
            facingRight = _activeShadowPartner.CurrentFacingRight;
            return true;
        }

        private int ResolveShadowPartnerHorizontalOffsetPx(SkillAnimation currentAnimation)
        {
            int baselineOffsetPx = Math.Max(0, _activeShadowPartner?.HorizontalOffsetPx ?? 26);
            if (currentAnimation?.Frames == null || currentAnimation.Frames.Count == 0)
            {
                return baselineOffsetPx;
            }

            // Shadow Partner special actions author slightly different first-frame origins per branch.
            // Preserve that authored horizontal cadence instead of pinning every action to the idle spacing.
            int baselineOriginX = baselineOffsetPx + 2;
            int actionOriginX = currentAnimation.Frames[0]?.Origin.X ?? baselineOriginX;
            return Math.Max(0, baselineOffsetPx + (actionOriginX - baselineOriginX));
        }

        private void DrawAvatarEffectPlane(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            List<AvatarEffectRenderable> avatarEffects,
            SkillAvatarEffectPlane plane,
            int screenX,
            int screenY,
            Color tint)
        {
            if (avatarEffects == null || avatarEffects.Count == 0)
            {
                return;
            }

            for (int i = 0; i < avatarEffects.Count; i++)
            {
                AvatarEffectRenderable effect = avatarEffects[i];
                if (effect.Plane != plane || effect.Frame?.Texture == null)
                {
                    continue;
                }

                bool shouldFlip = FacingRight ^ effect.Frame.Flip;
                int drawX = shouldFlip
                    ? screenX - (effect.Frame.Texture.Width - effect.Frame.Origin.X)
                    : screenX - effect.Frame.Origin.X;
                int drawY = screenY - effect.Frame.Origin.Y;

                effect.Frame.Texture.DrawBackground(spriteBatch, skeletonRenderer, null, drawX, drawY, tint, shouldFlip, null);
            }
        }

        private List<AvatarEffectRenderable> GetCurrentAvatarEffectRenderables(int currentTime)
        {
            var renderables = new List<AvatarEffectRenderable>();
            if (_activeSkillAvatarEffects.Count == 0 && _transientSkillAvatarEffects.Count == 0)
            {
                return renderables;
            }

            if (ShouldSuppressSkillAvatarEffectRendering())
            {
                return renderables;
            }

            int elapsedTime;
            for (int i = 0; i < _activeSkillAvatarEffects.Count; i++)
            {
                SkillAvatarEffectState effectState = _activeSkillAvatarEffects[i];
                elapsedTime = Math.Max(0, currentTime - effectState.AnimationStartTime);

                if (effectState.IsFinishing)
                {
                    SkillAnimation finishOverlay = effectState.Mode == SkillAvatarEffectMode.LadderOrRope
                        ? effectState.LadderOverlayFinishAnimation ?? effectState.GroundOverlayFinishAnimation
                        : effectState.GroundOverlayFinishAnimation;
                    SkillAnimation finishUnderFace = effectState.Mode == SkillAvatarEffectMode.LadderOrRope
                        ? null
                        : effectState.GroundUnderFaceFinishAnimation;

                    AddAvatarEffectRenderable(renderables, finishOverlay, SkillAvatarEffectPlane.OverCharacter, elapsedTime);
                    AddAvatarEffectRenderable(renderables, finishUnderFace, SkillAvatarEffectPlane.UnderFace, elapsedTime);
                    continue;
                }

                SkillAnimation overlayAnimation = effectState.Mode == SkillAvatarEffectMode.LadderOrRope
                    ? effectState.LadderOverlayAnimation ?? effectState.GroundOverlayAnimation
                    : effectState.GroundOverlayAnimation;
                SkillAnimation overlaySecondaryAnimation = effectState.Mode == SkillAvatarEffectMode.LadderOrRope
                    ? null
                    : effectState.GroundOverlaySecondaryAnimation;
                SkillAnimation underFaceAnimation = effectState.Mode == SkillAvatarEffectMode.LadderOrRope
                    ? null
                    : effectState.GroundUnderFaceAnimation;
                SkillAnimation underFaceSecondaryAnimation = effectState.Mode == SkillAvatarEffectMode.LadderOrRope
                    ? null
                    : effectState.GroundUnderFaceSecondaryAnimation;
                SkillAvatarEffectPlane overlayPlane = effectState.Mode == SkillAvatarEffectMode.LadderOrRope
                    && effectState.LadderOverlayAnimation != null
                    ? SkillAvatarEffectPlane.BehindCharacter
                    : SkillAvatarEffectPlane.OverCharacter;

                AddAvatarEffectRenderable(renderables, overlayAnimation, overlayPlane, elapsedTime);
                AddAvatarEffectRenderable(renderables, overlaySecondaryAnimation, overlayPlane, elapsedTime);
                AddAvatarEffectRenderable(renderables, underFaceAnimation, SkillAvatarEffectPlane.UnderFace, elapsedTime);
                AddAvatarEffectRenderable(renderables, underFaceSecondaryAnimation, SkillAvatarEffectPlane.UnderFace, elapsedTime);
            }

            for (int i = 0; i < _transientSkillAvatarEffects.Count; i++)
            {
                TransientSkillAvatarEffectState effectState = _transientSkillAvatarEffects[i];
                if (effectState == null)
                {
                    continue;
                }

                elapsedTime = Math.Max(0, currentTime - effectState.AnimationStartTime);
                AddAvatarEffectRenderable(renderables, effectState.Animation, effectState.Plane, elapsedTime);
                AddAvatarEffectRenderable(renderables, effectState.SecondaryAnimation, effectState.SecondaryPlane, elapsedTime);
            }

            return renderables;
        }

        private void UpdateShadowPartnerRenderState(int currentTime)
        {
            if (_activeShadowPartner?.ActionAnimations == null || _activeShadowPartner.ActionAnimations.Count == 0)
            {
                return;
            }

            if (TryAdvanceShadowPartnerQueuedAction(currentTime))
            {
                return;
            }

            string playerActionName = CurrentActionName;
            bool isFloatingState = State is PlayerState.Swimming or PlayerState.Flying;
            if (!string.Equals(playerActionName, _activeShadowPartner.ObservedPlayerActionName, StringComparison.OrdinalIgnoreCase)
                || isFloatingState != _activeShadowPartner.ObservedPlayerFloatingState)
            {
                _activeShadowPartner.ObservedPlayerActionName = playerActionName;
                _activeShadowPartner.ObservedPlayerFloatingState = isFloatingState;
                if (IsShadowPartnerAttackAction(playerActionName))
                {
                    string delayedAttackAction = ResolveShadowPartnerActionName(playerActionName, _activeShadowPartner.CurrentActionName);
                    if (!string.IsNullOrWhiteSpace(delayedAttackAction))
                    {
                        _activeShadowPartner.PendingActionName = delayedAttackAction;
                        _activeShadowPartner.PendingActionReadyTime = currentTime + ResolveShadowPartnerAttackDelayMs(delayedAttackAction);
                        _activeShadowPartner.PendingFacingRight = FacingRight;
                    }
                }
                else
                {
                    string resolvedAction = ResolveShadowPartnerActionName(playerActionName, _activeShadowPartner.CurrentActionName);
                    if (ShouldHoldShadowPartnerCurrentAction(currentTime))
                    {
                        _activeShadowPartner.QueuedActionName = resolvedAction;
                        _activeShadowPartner.QueuedFacingRight = FacingRight;
                    }
                    else
                    {
                        SetShadowPartnerAction(resolvedAction, currentTime, FacingRight);
                    }

                    _activeShadowPartner.PendingActionName = null;
                }
            }

            if (!string.IsNullOrWhiteSpace(_activeShadowPartner.PendingActionName)
                && currentTime >= _activeShadowPartner.PendingActionReadyTime)
            {
                string pendingActionName = _activeShadowPartner.PendingActionName;
                bool pendingFacingRight = _activeShadowPartner.PendingFacingRight;
                _activeShadowPartner.PendingActionName = null;

                if (ShouldHoldShadowPartnerCurrentAction(currentTime))
                {
                    _activeShadowPartner.QueuedActionName = pendingActionName;
                    _activeShadowPartner.QueuedFacingRight = pendingFacingRight;
                }
                else
                {
                    SetShadowPartnerAction(pendingActionName, currentTime, pendingFacingRight);
                }
            }

            if (string.IsNullOrWhiteSpace(_activeShadowPartner.CurrentActionName))
            {
                SetShadowPartnerAction(ResolveShadowPartnerFallbackAction(), currentTime, FacingRight);
            }
        }

        private int ResolveShadowPartnerAttackDelayMs(string actionName)
        {
            if (_activeShadowPartner?.ActionAnimations != null
                && !string.IsNullOrWhiteSpace(actionName)
                && _activeShadowPartner.ActionAnimations.TryGetValue(actionName, out SkillAnimation animation)
                && animation?.Frames != null
                && animation.Frames.Count > 0)
            {
                int frameDelay = animation.Frames[0]?.Delay ?? 0;
                if (frameDelay > 0)
                {
                    return frameDelay;
                }
            }

            return ShadowPartnerAttackDelayMs;
        }

        private void SetShadowPartnerAction(string actionName, int currentTime, bool facingRight)
        {
            if (_activeShadowPartner == null || string.IsNullOrWhiteSpace(actionName))
            {
                return;
            }

            if (!_activeShadowPartner.ActionAnimations.ContainsKey(actionName))
            {
                return;
            }

            if (string.Equals(_activeShadowPartner.CurrentActionName, actionName, StringComparison.OrdinalIgnoreCase)
                && _activeShadowPartner.CurrentFacingRight == facingRight)
            {
                return;
            }

            _activeShadowPartner.CurrentActionName = actionName;
            _activeShadowPartner.CurrentActionStartTime = currentTime;
            _activeShadowPartner.CurrentFacingRight = facingRight;
        }

        private string ResolveShadowPartnerActionName(string playerActionName, string fallbackActionName)
        {
            return ResolveShadowPartnerActionName(_activeShadowPartner?.ActionAnimations, playerActionName, fallbackActionName);
        }

        private string ResolveShadowPartnerActionName(
            IReadOnlyDictionary<string, SkillAnimation> actionAnimations,
            string playerActionName,
            string fallbackActionName)
        {
            if (actionAnimations == null || actionAnimations.Count == 0)
            {
                return null;
            }

            foreach (string candidate in EnumerateShadowPartnerClientMappedCandidates(playerActionName, fallbackActionName))
            {
                if (actionAnimations.ContainsKey(candidate))
                {
                    return candidate;
                }
            }

            foreach (string candidate in EnumerateShadowPartnerActionCandidates(playerActionName, fallbackActionName))
            {
                if (actionAnimations.ContainsKey(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private IEnumerable<string> EnumerateShadowPartnerClientMappedCandidates(string playerActionName, string fallbackActionName)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string candidate in GetShadowPartnerClientMappedCandidates(playerActionName, fallbackActionName))
            {
                if (!string.IsNullOrWhiteSpace(candidate) && yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        private IEnumerable<string> GetShadowPartnerClientMappedCandidates(string playerActionName, string fallbackActionName)
        {
            if (string.IsNullOrWhiteSpace(playerActionName))
            {
                yield break;
            }

            switch (playerActionName.ToLowerInvariant())
            {
                case "ghostwalk":
                    yield return "walk1";
                    yield return "walk2";
                    yield break;
                case "ghoststand":
                    yield return "stand1";
                    yield return "stand2";
                    yield break;
                case "ghostjump":
                    yield return "jump";
                    yield break;
                case "ghostladder":
                    yield return "ladder";
                    yield break;
                case "ghostrope":
                    yield return "rope";
                    yield break;
                case "ghostprone":
                    yield return "prone";
                    yield break;
                case "ghostpronestab":
                    yield return "proneStab";
                    yield return "prone";
                    yield break;
                case "ghostfly":
                    // Client `MoveAction2RawAction` collapses ghost float/swim move actions
                    // back onto the stand-family raw action before `load_character_action`.
                    yield return "stand1";
                    yield return "stand2";
                    yield return "fly";
                    yield return "jump";
                    yield break;
                case "ghostsit":
                    yield return "sit";
                    yield break;
            }

            if (State is PlayerState.Swimming or PlayerState.Flying)
            {
                yield return "stand1";
                yield return "stand2";
                yield return "fly";
                yield return "jump";
            }
            else if (State == PlayerState.Ladder)
            {
                yield return "ladder";
            }
            else if (State == PlayerState.Rope)
            {
                yield return "rope";
            }
            else if (State == PlayerState.Prone)
            {
                yield return "prone";
                yield return "proneStab";
            }
            else if (State == PlayerState.Sitting)
            {
                yield return "sit";
            }
            else if (State == PlayerState.Walking)
            {
                yield return "walk1";
                yield return "walk2";
            }
            else if (State == PlayerState.Standing)
            {
                yield return "stand1";
                yield return "stand2";
            }

            if (!string.IsNullOrWhiteSpace(fallbackActionName))
            {
                yield return fallbackActionName;
            }
        }

        private string ResolveShadowPartnerCreateActionName(IReadOnlyDictionary<string, SkillAnimation> actionAnimations)
        {
            if (actionAnimations == null || actionAnimations.Count == 0)
            {
                return null;
            }

            foreach (string candidate in EnumerateShadowPartnerCreateCandidates())
            {
                if (actionAnimations.ContainsKey(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private IEnumerable<string> EnumerateShadowPartnerCreateCandidates()
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool airborne = State is PlayerState.Jumping or PlayerState.Falling or PlayerState.Swimming or PlayerState.Flying;
            bool stationary = State is PlayerState.Standing or PlayerState.Walking or PlayerState.Sitting or PlayerState.Prone;

            foreach (string candidate in new[] { "create2", "create3", "create4" })
            {
                string stateVariant = airborne
                    ? candidate + "_f"
                    : stationary
                        ? candidate + "_s"
                        : null;

                if (!string.IsNullOrWhiteSpace(stateVariant) && yielded.Add(stateVariant))
                {
                    yield return stateVariant;
                }

                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }

                string alternateVariant = airborne ? candidate + "_s" : candidate + "_f";
                if (yielded.Add(alternateVariant))
                {
                    yield return alternateVariant;
                }
            }
        }

        private IEnumerable<string> EnumerateShadowPartnerActionCandidates(string playerActionName, string fallbackActionName)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(playerActionName))
            {
                foreach (string candidate in CharacterPart.GetActionLookupStrings(playerActionName))
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }

                foreach (string candidate in EnumerateShadowPartnerClientActionAliases(playerActionName))
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }

                if (string.Equals(playerActionName, "attack1", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string candidate in new[] { "stabO1", "stabO2", "stabOF" })
                    {
                        if (yielded.Add(candidate))
                        {
                            yield return candidate;
                        }
                    }
                }
                else if (string.Equals(playerActionName, "attack2", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string candidate in new[] { "swingO1", "swingO2", "swingO3", "swingOF" })
                    {
                        if (yielded.Add(candidate))
                        {
                            yield return candidate;
                        }
                    }
                }
                else if (string.Equals(playerActionName, "hit", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string candidate in new[] { "alert", "stand1" })
                    {
                        if (yielded.Add(candidate))
                        {
                            yield return candidate;
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(fallbackActionName) && yielded.Add(fallbackActionName))
            {
                yield return fallbackActionName;
            }

            string stateFallback = ResolveShadowPartnerFallbackAction();
            if (!string.IsNullOrWhiteSpace(stateFallback) && yielded.Add(stateFallback))
            {
                yield return stateFallback;
            }

            foreach (string candidate in new[] { "stand1", "stand2", "alert", "walk1", "sit" })
            {
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        private static IEnumerable<string> EnumerateShadowPartnerClientActionAliases(string playerActionName)
        {
            if (string.IsNullOrWhiteSpace(playerActionName))
            {
                yield break;
            }

            if (playerActionName.StartsWith("alert", StringComparison.OrdinalIgnoreCase))
            {
                yield return "alert";
            }
            else if (string.Equals(playerActionName, "ladder2", StringComparison.OrdinalIgnoreCase))
            {
                yield return "ladder";
            }
            else if (string.Equals(playerActionName, "rope2", StringComparison.OrdinalIgnoreCase))
            {
                yield return "rope";
            }

            switch (playerActionName.ToLowerInvariant())
            {
                case "ghoststand":
                    yield return "stand1";
                    yield return "stand2";
                    break;
                case "ghostwalk":
                    yield return "walk1";
                    yield return "walk2";
                    break;
                case "ghostjump":
                    yield return "jump";
                    break;
                case "ghostpronestab":
                    yield return "proneStab";
                    yield return "prone";
                    break;
                case "ghostladder":
                    yield return "ladder";
                    break;
                case "ghostrope":
                    yield return "rope";
                    break;
                case "ghostfly":
                    yield return "fly";
                    break;
                case "ghostsit":
                    yield return "sit";
                    break;
            }
        }

        private string ResolveShadowPartnerFallbackAction()
        {
            return State switch
            {
                PlayerState.Walking => "walk1",
                PlayerState.Jumping or PlayerState.Falling => "jump",
                PlayerState.Ladder => "ladder",
                PlayerState.Rope => "rope",
                PlayerState.Swimming or PlayerState.Flying => "fly",
                PlayerState.Sitting => "sit",
                PlayerState.Prone => "prone",
                PlayerState.Dead => "dead",
                PlayerState.Attacking => CurrentActionName,
                _ => "stand1"
            };
        }

        private bool TryAdvanceShadowPartnerQueuedAction(int currentTime)
        {
            if (_activeShadowPartner == null || string.IsNullOrWhiteSpace(_activeShadowPartner.QueuedActionName))
            {
                return false;
            }

            if (ShouldHoldShadowPartnerCurrentAction(currentTime))
            {
                return false;
            }

            string queuedActionName = _activeShadowPartner.QueuedActionName;
            bool queuedFacingRight = _activeShadowPartner.QueuedFacingRight;
            _activeShadowPartner.QueuedActionName = null;
            SetShadowPartnerAction(queuedActionName, currentTime, queuedFacingRight);
            return true;
        }

        private bool ShouldHoldShadowPartnerCurrentAction(int currentTime)
        {
            if (_activeShadowPartner?.ActionAnimations == null
                || string.IsNullOrWhiteSpace(_activeShadowPartner.CurrentActionName)
                || !_activeShadowPartner.ActionAnimations.TryGetValue(_activeShadowPartner.CurrentActionName, out SkillAnimation currentAnimation)
                || currentAnimation?.Frames == null
                || currentAnimation.Frames.Count == 0)
            {
                return false;
            }

            if (!IsShadowPartnerBlockingAction(_activeShadowPartner.CurrentActionName))
            {
                return false;
            }

            int elapsedTime = Math.Max(0, currentTime - _activeShadowPartner.CurrentActionStartTime);
            return !currentAnimation.IsComplete(elapsedTime);
        }

        private static bool IsShadowPartnerBlockingAction(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                   && (IsShadowPartnerAttackAction(actionName)
                       || actionName.StartsWith("create", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsShadowPartnerAttackAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return actionName.StartsWith("swing", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("stab", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("shoot", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "attack1", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "attack2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "proneStab", StringComparison.OrdinalIgnoreCase);
        }

        private bool ShouldSuppressSkillAvatarEffectRendering()
        {
            return _skillAvatarEffectRenderSuppressionSkillIds.Count > 0
                   || CharacterAssembler.ShouldSuppressBaseAvatarForState(Build, CurrentActionName);
        }

        private bool ShouldCancelPortableChairFromInput()
        {
            return _inputLeft || _inputRight || _inputUp || _inputJump || _inputAttack;
        }

        private void ConfigurePortableChairPairPreview(PortableChair chair)
        {
            ClearPortableChairPairPreview();

            if (chair?.IsCoupleChair != true || Build == null)
            {
                return;
            }

            CharacterBuild pairBuild = Build.Clone();
            pairBuild.ActivePortableChair = chair;
            _portableChairPairAssembler = new CharacterAssembler(pairBuild);
            _portableChairPairOffset = ResolvePortableChairPairOffset(chair, FacingRight);
            _portableChairPairFacingRight = ResolvePortableChairPairFacingRight(chair, FacingRight);
            _portableChairPairActionName = GetPortableChairActionName(chair);
        }

        private bool ShouldDrawPortableChairPairPreview()
        {
            return IsPortableChairPairPlacementValid(Build?.ActivePortableChair, FacingRight, X, Y, _findFoothold);
        }

        public void SetPortableChairPairRequestActive(bool requested)
        {
            _portableChairExternalPairRequested = requested;
            if (!requested)
            {
                ClearPortableChairExternalPair();
            }
        }

        public void SetPortableChairExternalPair(Vector2 position, bool facingRight)
        {
            _portableChairExternalPairRequested = true;
            _portableChairHasExternalPair = true;
            _portableChairExternalPairPosition = position;
            _portableChairExternalPairFacingRight = facingRight;
        }

        public void ClearPortableChairExternalPair()
        {
            _portableChairHasExternalPair = false;
            _portableChairExternalPairPosition = Vector2.Zero;
            _portableChairExternalPairFacingRight = false;
        }

        private void ClearPortableChairPairPreview()
        {
            _portableChairPairAssembler = null;
            _portableChairPairOffset = Point.Zero;
            _portableChairPairFacingRight = false;
            _portableChairPairActionName = null;
            ClearPortableChairExternalPair();
        }

        internal static Point ResolvePortableChairPairOffset(PortableChair chair, bool facingRight)
        {
            if (chair?.IsCoupleChair != true)
            {
                return Point.Zero;
            }

            int distanceX = Math.Abs(chair.CoupleDistanceX ?? 0);
            int distanceY = chair.CoupleDistanceY ?? 0;
            int directionSign = ResolvePortableChairPairDirectionSign(chair, facingRight);
            return new Point(directionSign * distanceX, distanceY);
        }

        internal static bool IsPortableChairPairPlacementValid(
            PortableChair chair,
            bool facingRight,
            float originX,
            float originY,
            Func<float, float, float, FootholdLine> findFoothold)
        {
            if (chair?.IsCoupleChair != true)
            {
                return false;
            }

            if (chair.CoupleMaxDiff is not int maxDiff || maxDiff < 0 || findFoothold == null)
            {
                return true;
            }

            Point offset = ResolvePortableChairPairOffset(chair, facingRight);
            float partnerX = originX + offset.X;
            float partnerY = originY + offset.Y;
            float searchRange = Math.Max(8f, Math.Abs(offset.Y) + maxDiff + 4f);
            FootholdLine foothold = findFoothold(partnerX, partnerY, searchRange);
            if (foothold == null)
            {
                return false;
            }

            float footholdY = CalculateYOnFoothold(foothold, partnerX);
            return Math.Abs(footholdY - partnerY) <= maxDiff;
        }

        internal static bool ResolvePortableChairPairFacingRight(bool facingRight)
        {
            return ResolvePortableChairPairFacingRight(null, facingRight);
        }

        internal static bool ResolvePortableChairPairFacingRight(PortableChair chair, bool facingRight)
        {
            if (chair?.IsCoupleChair != true)
            {
                return !facingRight;
            }

            ResolvePortableChairPairPreviewLayout(chair, facingRight, out _, out bool partnerFacingRight);
            return partnerFacingRight;
        }

        internal static int ResolvePortableChairPairDirectionSign(PortableChair chair, bool facingRight)
        {
            if (chair?.IsCoupleChair != true)
            {
                return facingRight ? 1 : -1;
            }

            ResolvePortableChairPairPreviewLayout(chair, facingRight, out int directionSign, out _);
            return directionSign;
        }

        internal static bool IsPortableChairActualPairActive(
            PortableChair chair,
            bool ownerFacingRight,
            float ownerX,
            float ownerY,
            bool partnerFacingRight,
            float partnerX,
            float partnerY)
        {
            if (chair?.IsCoupleChair != true)
            {
                return false;
            }

            int distanceX = Math.Abs(chair.CoupleDistanceX ?? 0);
            int distanceY = Math.Abs(chair.CoupleDistanceY ?? 0);
            int tolerance = Math.Max(0, chair.CoupleMaxDiff ?? 0);
            int actualDistanceX = (int)Math.Round(Math.Abs(ownerX - partnerX));
            int actualDistanceY = (int)Math.Round(Math.Abs(ownerY - partnerY));
            if (Math.Abs(actualDistanceX - distanceX) > tolerance
                || Math.Abs(actualDistanceY - distanceY) > tolerance)
            {
                return false;
            }

            bool ownerIsLeft = ownerX <= partnerX;
            bool leftFacingRight = ownerIsLeft ? ownerFacingRight : partnerFacingRight;
            bool rightFacingRight = ownerIsLeft ? partnerFacingRight : ownerFacingRight;

            return (chair.CoupleDirection ?? 0) switch
            {
                0 => true,
                3 => leftFacingRight == rightFacingRight,
                11 => !leftFacingRight && !rightFacingRight,
                12 => !leftFacingRight && rightFacingRight,
                21 => leftFacingRight && !rightFacingRight,
                22 => leftFacingRight && rightFacingRight,
                _ => false
            };
        }

        private static void ResolvePortableChairPairPreviewLayout(
            PortableChair chair,
            bool ownerFacingRight,
            out int directionSign,
            out bool partnerFacingRight)
        {
            switch (chair?.CoupleDirection ?? 0)
            {
                case 3:
                    directionSign = ownerFacingRight ? 1 : -1;
                    partnerFacingRight = ownerFacingRight;
                    return;
                case 11:
                    directionSign = ownerFacingRight ? -1 : 1;
                    partnerFacingRight = false;
                    return;
                case 12:
                    directionSign = ownerFacingRight ? -1 : 1;
                    partnerFacingRight = !ownerFacingRight;
                    return;
                case 21:
                    directionSign = ownerFacingRight ? 1 : -1;
                    partnerFacingRight = !ownerFacingRight;
                    return;
                case 22:
                    directionSign = ownerFacingRight ? 1 : -1;
                    partnerFacingRight = true;
                    return;
                default:
                    directionSign = ownerFacingRight ? 1 : -1;
                    partnerFacingRight = !ownerFacingRight;
                    return;
            }
        }

        private static float CalculateYOnFoothold(FootholdLine foothold, float x)
        {
            if (foothold == null)
            {
                return float.NaN;
            }

            float x1 = foothold.FirstDot.X;
            float y1 = foothold.FirstDot.Y;
            float x2 = foothold.SecondDot.X;
            float y2 = foothold.SecondDot.Y;
            if (Math.Abs(x2 - x1) < 0.001f)
            {
                return Math.Min(y1, y2);
            }

            float t = (x - x1) / (x2 - x1);
            return y1 + ((y2 - y1) * t);
        }

        private static string GetPortableChairActionName(PortableChair chair)
        {
            if (chair?.SitActionId is int sitActionId && sitActionId >= 0)
            {
                return $"sit{sitActionId}";
            }

            return CharacterPart.GetActionString(CharacterAction.Sit);
        }

        private void ApplyPortableChairMount(PortableChair chair)
        {
            if (Build == null
                || chair?.TamingMobItemId is not int tamingMobItemId
                || tamingMobItemId <= 0
                || _portableChairTamingMobLoader == null)
            {
                return;
            }

            CharacterPart mountPart = _portableChairTamingMobLoader(tamingMobItemId);
            if (mountPart?.Slot != EquipSlot.TamingMob)
            {
                return;
            }

            Build.Equipment.TryGetValue(EquipSlot.TamingMob, out _portableChairPreviousMount);
            Build.Equip(mountPart);
            _portableChairAppliedMount = true;
            NotifyTamingMobOwnershipHandledExternally();
        }

        private void ClearPortableChairMountState()
        {
            if (Build == null || !_portableChairAppliedMount)
            {
                return;
            }

            if (_portableChairPreviousMount != null)
            {
                Build.Equip(_portableChairPreviousMount);
            }
            else
            {
                Build.Unequip(EquipSlot.TamingMob);
            }

            _portableChairPreviousMount = null;
            _portableChairAppliedMount = false;
            NotifyTamingMobOwnershipHandledExternally();
        }

        private void ClearTransientSkillAvatarEffect(int skillId)
        {
            for (int i = _transientSkillAvatarEffects.Count - 1; i >= 0; i--)
            {
                if (_transientSkillAvatarEffects[i].SkillId == skillId)
                {
                    _transientSkillAvatarEffects.RemoveAt(i);
                }
            }
        }

        public void ClearAllTransientSkillAvatarEffects()
        {
            _transientSkillAvatarEffects.Clear();
        }

        private static SkillAvatarEffectPlane ResolveTransientSkillAvatarEffectPlane(SkillAnimation animation)
        {
            return animation?.ZOrder < 0
                ? SkillAvatarEffectPlane.UnderFace
                : SkillAvatarEffectPlane.OverCharacter;
        }

        private static void AddAvatarEffectRenderable(
            List<AvatarEffectRenderable> renderables,
            SkillAnimation animation,
            SkillAvatarEffectPlane plane,
            int elapsedTime)
        {
            if (renderables == null || animation == null)
            {
                return;
            }

            SkillFrame frame = animation.GetFrameAtTime(elapsedTime);
            if (frame?.Texture == null)
            {
                return;
            }

            renderables.Add(new AvatarEffectRenderable(frame, plane));
        }

        private static int GetUnderFaceInsertionIndex(List<AssembledPart> parts)
        {
            if (parts == null || parts.Count == 0)
            {
                return 0;
            }

            int fallbackIndex = parts.Count;
            for (int i = 0; i < parts.Count; i++)
            {
                CharacterPartType partType = parts[i].PartType;
                if (partType == CharacterPartType.Head)
                {
                    fallbackIndex = i + 1;
                    continue;
                }

                if (partType == CharacterPartType.Face
                    || partType == CharacterPartType.Hair
                    || partType == CharacterPartType.Cap
                    || partType == CharacterPartType.CapOverHair
                    || partType == CharacterPartType.CapBelowAccessory
                    || partType == CharacterPartType.Accessory
                    || partType == CharacterPartType.AccessoryOverHair
                    || partType == CharacterPartType.Face_Accessory
                    || partType == CharacterPartType.Eye_Accessory
                    || partType == CharacterPartType.Earrings)
                {
                    return i;
                }
            }

            return fallbackIndex;
        }

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

        public Point? TryGetCurrentBodyOrigin(int currentTime)
        {
            if (Assembler == null)
            {
                return null;
            }

            AssembledFrame frame = Assembler.GetFrameAtTime(CurrentActionName, GetRenderAnimationTime(currentTime));
            return frame == null
                ? null
                : new Point((int)X, (int)Y - frame.FeetOffset);
        }

        public Point? TryGetCurrentBodyMapPoint(string mapPointName, int currentTime)
        {
            if (Assembler == null || string.IsNullOrWhiteSpace(mapPointName))
            {
                return null;
            }

            AssembledFrame frame = Assembler.GetFrameAtTime(CurrentActionName, GetRenderAnimationTime(currentTime));
            if (frame == null || !frame.MapPoints.TryGetValue(mapPointName, out Point localPoint))
            {
                return null;
            }

            int worldX = (int)X + (FacingRight ? localPoint.X : -localPoint.X);
            int worldY = (int)Y - frame.FeetOffset + localPoint.Y;
            return new Point(worldX, worldY);
        }

        public Rectangle? TryGetCurrentFrameBounds(int currentTime)
        {
            if (Assembler == null)
            {
                return null;
            }

            AssembledFrame frame = Assembler.GetFrameAtTime(CurrentActionName, GetRenderAnimationTime(currentTime));
            return frame == null || frame.Bounds.IsEmpty
                ? null
                : frame.Bounds;
        }

        private void ClearForcedActionName()
        {
            _sustainedSkillAnimation = false;
            _forcedActionName = null;
            SetTransientTamingMobOverride(null);
        }

        private void UpdateAutomaticTamingMobTransition()
        {
            CharacterPart equippedMount = GetEquippedTamingMobPart();
            CharacterPart activeMount = ResolveAutomaticTamingMobTransitionMount(equippedMount);
            bool clientOwnedMountActive = IsClientOwnedVehicleTamingMobStateActive(activeMount);

            if (_suppressAutomaticTamingMobTransition)
            {
                _observedTamingMobPart = activeMount;
                _observedClientOwnedTamingMobActive = clientOwnedMountActive;
                _suppressAutomaticTamingMobTransition = false;
                return;
            }

            if (SameTamingMob(_observedTamingMobPart, activeMount)
                && _observedClientOwnedTamingMobActive == clientOwnedMountActive)
            {
                return;
            }

            if (_portableChairAppliedMount || Build?.ActivePortableChair != null)
            {
                _observedTamingMobPart = activeMount;
                _observedClientOwnedTamingMobActive = clientOwnedMountActive;
                return;
            }

            if (State == PlayerState.Attacking && !IsAutomaticTamingMobTransitionAction(CurrentActionName))
            {
                _observedTamingMobPart = activeMount;
                _observedClientOwnedTamingMobActive = clientOwnedMountActive;
                return;
            }

            bool shouldPlayRideTransition = activeMount?.Slot == EquipSlot.TamingMob
                && (clientOwnedMountActive || !SameTamingMob(_observedTamingMobPart, activeMount));
            bool shouldPlayGetOffTransition = _observedTamingMobPart?.Slot == EquipSlot.TamingMob
                && (_observedClientOwnedTamingMobActive || activeMount == null);

            if (shouldPlayRideTransition
                && SupportsTamingMobTransitionAction(activeMount, "ride2"))
            {
                TriggerAutomaticTamingMobTransition(activeMount, "ride2", preserveUnmountedMount: false);
            }
            else if (shouldPlayGetOffTransition
                     && SupportsTamingMobTransitionAction(_observedTamingMobPart, "getoff2"))
            {
                TriggerAutomaticTamingMobTransition(_observedTamingMobPart, "getoff2", preserveUnmountedMount: true);
            }
            else
            {
                SetTransientTamingMobOverride(null);
            }

            _observedTamingMobPart = activeMount;
            _observedClientOwnedTamingMobActive = clientOwnedMountActive;
        }

        private void TriggerAutomaticTamingMobTransition(CharacterPart mountPart, string actionName, bool preserveUnmountedMount)
        {
            if (mountPart?.Slot != EquipSlot.TamingMob || string.IsNullOrWhiteSpace(actionName))
            {
                return;
            }

            SetTransientTamingMobOverride(preserveUnmountedMount ? mountPart : null);
            TriggerSkillAnimation(actionName);
        }

        private CharacterPart GetEquippedTamingMobPart()
        {
            return Build?.Equipment != null
                && Build.Equipment.TryGetValue(EquipSlot.TamingMob, out CharacterPart mountPart)
                ? mountPart
                : null;
        }

        private void SetTransientTamingMobOverride(CharacterPart mountPart)
        {
            _transitionTamingMobOverridePart = mountPart?.Slot == EquipSlot.TamingMob ? mountPart : null;
            UpdateAssemblerTamingMobOverride();
        }

        private void SetStateDrivenTamingMobOverride(CharacterPart mountPart)
        {
            _stateDrivenTamingMobOverridePart = mountPart?.Slot == EquipSlot.TamingMob ? mountPart : null;
            UpdateAssemblerTamingMobOverride();
        }

        private void UpdateAssemblerTamingMobOverride()
        {
            if (Assembler == null)
            {
                return;
            }

            CharacterPart overridePart = _transitionTamingMobOverridePart?.Slot == EquipSlot.TamingMob
                ? _transitionTamingMobOverridePart
                : _stateDrivenTamingMobOverridePart?.Slot == EquipSlot.TamingMob
                    ? _stateDrivenTamingMobOverridePart
                    : null;
            Assembler.OverrideTamingMobPart = overridePart;
        }

        private void UpdateOwnedTamingMobRenderState()
        {
            if (Assembler == null)
            {
                return;
            }

            if (_portableChairAppliedMount || Build?.ActivePortableChair != null)
            {
                SetStateDrivenTamingMobOverride(null);
                return;
            }

            CharacterPart clientOwnedVehicleMount = GetClientOwnedVehicleTamingMobPart();
            if (CharacterAssembler.SupportsTamingMobAction(clientOwnedVehicleMount, CurrentActionName))
            {
                SetStateDrivenTamingMobOverride(clientOwnedVehicleMount);
                return;
            }

            CharacterPart equippedMount = GetEquippedTamingMobPart();
            if (equippedMount?.Slot == EquipSlot.TamingMob)
            {
                SetStateDrivenTamingMobOverride(null);
                return;
            }

            if (!IsStateDrivenMechanicVehicleAction(CurrentActionName))
            {
                SetStateDrivenTamingMobOverride(null);
                return;
            }

            CharacterPart mechanicMountPart = ResolveMechanicVehicleTamingMobPart();
            if (CharacterAssembler.IsTamingMobRenderOwnershipAction(mechanicMountPart, CurrentActionName))
            {
                SetStateDrivenTamingMobOverride(mechanicMountPart);
                return;
            }

            SetStateDrivenTamingMobOverride(null);
        }

        private CharacterPart ResolveMechanicVehicleTamingMobPart()
        {
            CharacterPart equippedMount = GetEquippedTamingMobPart();
            if (IsMechanicTamingMobPart(equippedMount))
            {
                return equippedMount;
            }

            if (IsMechanicTamingMobPart(_transitionTamingMobOverridePart))
            {
                return _transitionTamingMobOverridePart;
            }

            if (IsMechanicTamingMobPart(_observedTamingMobPart))
            {
                return _observedTamingMobPart;
            }

            if (IsMechanicTamingMobPart(_sharedMechanicTamingMobPart))
            {
                return _sharedMechanicTamingMobPart;
            }

            CharacterPart loadedMount = _tamingMobLoader?.Invoke(MechanicTamingMobItemId);
            if (IsMechanicTamingMobPart(loadedMount))
            {
                _sharedMechanicTamingMobPart = loadedMount;
                return loadedMount;
            }

            return null;
        }

        private CharacterPart ResolveAutomaticTamingMobTransitionMount(CharacterPart equippedMount)
        {
            CharacterPart clientOwnedVehicleMount = GetClientOwnedVehicleTamingMobPart();
            return clientOwnedVehicleMount?.Slot == EquipSlot.TamingMob
                ? clientOwnedVehicleMount
                : equippedMount;
        }

        private CharacterPart GetClientOwnedVehicleTamingMobPart()
        {
            return _clientOwnedVehicleTamingMobActive
                   && _clientOwnedVehicleTamingMobPart?.Slot == EquipSlot.TamingMob
                ? _clientOwnedVehicleTamingMobPart
                : null;
        }

        private bool IsClientOwnedVehicleTamingMobStateActive(CharacterPart mountPart)
        {
            return _clientOwnedVehicleTamingMobActive
                   && SameTamingMob(_clientOwnedVehicleTamingMobPart, mountPart);
        }

        private static bool SupportsTamingMobTransitionAction(CharacterPart mountPart, string actionName)
        {
            return mountPart?.Slot == EquipSlot.TamingMob
                   && mountPart.GetAnimation(actionName) != null;
        }

        private static bool IsAutomaticTamingMobTransitionAction(string actionName)
        {
            return string.Equals(actionName, "ride2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "getoff2", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMechanicTamingMobPart(CharacterPart mountPart)
        {
            return mountPart?.Slot == EquipSlot.TamingMob
                   && mountPart.ItemId == MechanicTamingMobItemId;
        }

        private static bool IsStateDrivenMechanicVehicleAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return actionName.StartsWith("tank_", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("siege_", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("flamethrower", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("rbooster", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("gatlingshot", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("drillrush", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("earthslug", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("rpunch", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("mbooster", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("msummon", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("mRush", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "alert3", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "herbalism_mechanic", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "mining_mechanic", StringComparison.OrdinalIgnoreCase);
        }

        private static bool SameTamingMob(CharacterPart left, CharacterPart right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            return left.Slot == right.Slot && left.ItemId == right.ItemId;
        }

        private static CharacterAction GetCharacterActionForActionName(string actionName)
        {
            return actionName?.ToLower() switch
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
        }

        private string GetSkillTransformActionName(PlayerState state)
        {
            SkillAvatarTransformState activeTransform = GetActiveAvatarTransform();
            if (activeTransform == null)
            {
                return null;
            }

            return state switch
            {
                PlayerState.Walking => ResolveSkillTransformActionName(activeTransform.WalkActionNames, activeTransform.StandActionNames),
                PlayerState.Jumping or PlayerState.Falling => ResolveSkillTransformActionName(activeTransform.JumpActionNames, activeTransform.StandActionNames),
                PlayerState.Prone => ResolveSkillTransformActionName(activeTransform.ProneActionNames, activeTransform.StandActionNames),
                PlayerState.Ladder or PlayerState.Rope => ResolveSkillTransformActionName(activeTransform.ClimbActionNames, activeTransform.StandActionNames),
                PlayerState.Swimming or PlayerState.Flying => ResolveSkillTransformActionName(activeTransform.FloatActionNames, activeTransform.StandActionNames),
                PlayerState.Attacking => ResolveSkillTransformActionName(activeTransform.AttackActionNames, activeTransform.StandActionNames),
                PlayerState.Hit => ResolveSkillTransformActionName(activeTransform.HitActionNames, activeTransform.StandActionNames),
                PlayerState.Dead => CharacterPart.GetActionString(CharacterAction.Dead),
                _ => ResolveSkillTransformActionName(activeTransform.StandActionNames)
            };
        }

        private string ResolveSkillTransformActionName(IReadOnlyList<string> preferredActionNames, IReadOnlyList<string> fallbackActionNames = null)
        {
            foreach (string actionName in EnumerateTransformActionNames(preferredActionNames, fallbackActionNames))
            {
                if (HasAvatarAction(actionName))
                {
                    return actionName;
                }
            }

            foreach (string actionName in EnumerateTransformActionNames(preferredActionNames, fallbackActionNames))
            {
                return actionName;
            }

            return null;
        }

        private string ResolveActiveSkillSpecificActionName(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName)
                || actionName.StartsWith("tank_", StringComparison.OrdinalIgnoreCase)
                || actionName.StartsWith("siege_", StringComparison.OrdinalIgnoreCase)
                || actionName.StartsWith("tank_siege", StringComparison.OrdinalIgnoreCase)
                || GetActiveAvatarTransform() == null)
            {
                return actionName;
            }

            string[] transformPrefixes = GetActiveSkillTransformPrefixes();
            if (transformPrefixes.Length == 0)
            {
                return actionName;
            }

            foreach (string prefix in transformPrefixes)
            {
                string prefixedActionName = $"{prefix}{actionName}";
                if (HasAvatarAction(prefixedActionName) || HasMountedAction(prefixedActionName))
                {
                    return prefixedActionName;
                }
            }

            return actionName;
        }

        private string[] GetActiveSkillTransformPrefixes()
        {
            foreach (string actionName in EnumerateTransformActionNames(GetActiveAvatarTransform()?.StandActionNames))
            {
                if (actionName.StartsWith("tank_siege", StringComparison.OrdinalIgnoreCase))
                {
                    return new[] { "tank_siege", "tank_" };
                }

                if (actionName.StartsWith("siege_", StringComparison.OrdinalIgnoreCase))
                {
                    return new[] { "siege_" };
                }

                if (actionName.StartsWith("tank_", StringComparison.OrdinalIgnoreCase))
                {
                    return new[] { "tank_" };
                }
            }

            return Array.Empty<string>();
        }

        private IEnumerable<string> EnumerateTransformActionNames(params IReadOnlyList<string>[] actionGroups)
        {
            if (actionGroups == null)
            {
                yield break;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (IReadOnlyList<string> actionGroup in actionGroups)
            {
                if (actionGroup == null)
                {
                    continue;
                }

                foreach (string actionName in actionGroup)
                {
                    if (string.IsNullOrWhiteSpace(actionName) || !seen.Add(actionName))
                    {
                        continue;
                    }

                    yield return actionName;
                }
            }
        }

        private bool HasAvatarAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            CharacterPart avatarPart = GetActiveAvatarTransform()?.AvatarPart ?? Build?.Body;
            return avatarPart?.Animations?.ContainsKey(actionName) == true
                   || avatarPart?.GetAnimation(actionName) != null;
        }

        private bool HasMountedAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName)
                || Build?.Equipment == null
                || !Build.Equipment.TryGetValue(EquipSlot.TamingMob, out CharacterPart mountPart))
            {
                return false;
            }

            return mountPart.GetAnimation(actionName) != null;
        }

        public bool CanRenderAction(string actionName)
        {
            return HasAvatarAction(actionName) || HasMountedAction(actionName);
        }

        private bool TryCreateSkillAvatarTransform(int skillId, string actionName, int morphTemplateId, out SkillAvatarTransformState transform)
        {
            if (TryCreateMorphAvatarTransform(skillId, morphTemplateId, actionName, out transform))
            {
                return true;
            }

            return TryCreateBuiltInSkillAvatarTransform(skillId, actionName, out transform);
        }

        private bool TryCreateExternalAvatarTransform(int sourceId, string actionName, int morphTemplateId, out SkillAvatarTransformState transform)
        {
            transform = null;
            if (!TryCreateMorphAvatarTransform(sourceId, morphTemplateId, actionName, out transform))
            {
                return false;
            }

            transform = CloneTransform(transform, sourceId);
            return true;
        }

        private bool TryCreateMorphAvatarTransform(int skillId, int morphTemplateId, string actionName, out SkillAvatarTransformState transform)
        {
            transform = null;
            if (morphTemplateId <= 0 || _skillMorphLoader == null)
            {
                return false;
            }

            CharacterPart morphPart = _skillMorphLoader(morphTemplateId);
            if (morphPart?.Type != CharacterPartType.Morph || morphPart.Animations.Count == 0)
            {
                return false;
            }

            transform = CreateMorphTransform(skillId, morphPart, actionName);
            return true;
        }

        private static bool TryCreateBuiltInSkillAvatarTransform(int skillId, string actionName, out SkillAvatarTransformState transform)
        {
            transform = null;
            string normalizedAction = actionName?.Trim();

            switch (skillId)
            {
                case 22121000:
                    transform = CreateSingleActionTransform(skillId, "icebreathe_prepare", "dragonIceBreathe");
                    return true;
                case 22151001:
                    transform = CreateSingleActionTransform(skillId, "breathe_prepare", "dragonBreathe");
                    return true;
                case 32121003:
                    transform = CreateSingleActionTransform(skillId, "cyclone", "cyclone_after");
                    return true;
                case 33101005:
                    transform = CreatePreparedSingleActionTransform(skillId, normalizedAction, "swallow_pre", "swallow_loop", "swallow");
                    return true;
                case 33121006:
                    transform = CreateSingleActionTransform(skillId, "wildbeast", exitActionName: null);
                    return true;
                case 4341002:
                    transform = CreateSingleActionTransform(skillId, "finalCutPrepare", "finalCut");
                    return true;
                case 4341003:
                    transform = CreateSingleActionTransform(skillId, "monsterBombPrepare", "monsterBombThrow");
                    return true;
                case 4001003:
                case 14001003:
                    transform = CreateDarkSightTransform(skillId);
                    return true;
                case 23121000:
                    transform = CreatePreparedSingleActionTransform(skillId, normalizedAction, "dualVulcanPrep", "dualVulcanLoop", "dualVulcanEnd");
                    return true;
                case 14111006:
                    transform = CreatePreparedSingleActionTransform(skillId, normalizedAction, "dash", "darkTornado", "darkTornado_after");
                    return true;
                case 5311002:
                    transform = CreateSingleActionTransform(skillId, "noiseWave_ing", "noiseWave");
                    return true;
                case 5221004:
                case 5721001:
                    transform = CreateSingleActionTransform(skillId, "rapidfire", exitActionName: null);
                    return true;
                case 35001001:
                    transform = CreatePreparedMechanicTransform(skillId, normalizedAction, "flamethrower_pre", "flamethrower", "flamethrower_after");
                    return true;
                case 35101009:
                    transform = CreatePreparedMechanicTransform(skillId, normalizedAction, "flamethrower_pre2", "flamethrower2", "flamethrower_after2");
                    return true;
                case 35121005:
                    transform = CreateMechanicTransform(skillId, "tank_stand", "tank_walk", "tank", "tank_prone", "tank_after");
                    return true;
                case 35111004:
                    transform = CreateMechanicTransform(skillId, "siege_stand", "siege_stand", "siege", "siege_stand", "siege_after", locksMovement: true);
                    return true;
                case 35121013:
                    transform = CreateMechanicTransform(skillId, "tank_siegestand", "tank_siegestand", "tank_siegeattack", "tank_siegestand", "tank_siegeafter", locksMovement: true);
                    return true;
                case 35101004:
                    transform = CreateRocketBoosterTransform(skillId, normalizedAction);
                    return true;
            }

            if (string.Equals(normalizedAction, "flamethrower", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreateMechanicTransform(skillId, "flamethrower", "flamethrower", "flamethrower", "flamethrower", "flamethrower_after");
                return true;
            }

            if (string.Equals(normalizedAction, "icebreathe_prepare", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreateSingleActionTransform(skillId, "icebreathe_prepare", "dragonIceBreathe");
                return true;
            }

            if (string.Equals(normalizedAction, "breathe_prepare", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreateSingleActionTransform(skillId, "breathe_prepare", "dragonBreathe");
                return true;
            }

            if (string.Equals(normalizedAction, "cyclone_pre", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreateSingleActionTransform(skillId, "cyclone", "cyclone_after");
                return true;
            }

            if (string.Equals(normalizedAction, "swallow_loop", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreateSingleActionTransform(skillId, "swallow_loop", "swallow");
                return true;
            }

            if (string.Equals(normalizedAction, "wildbeast", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreateSingleActionTransform(skillId, "wildbeast", exitActionName: null);
                return true;
            }

            if (string.Equals(normalizedAction, "finalCutPrepare", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreateSingleActionTransform(skillId, "finalCutPrepare", "finalCut");
                return true;
            }

            if (string.Equals(normalizedAction, "monsterBombPrepare", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreateSingleActionTransform(skillId, "monsterBombPrepare", "monsterBombThrow");
                return true;
            }

            if (string.Equals(normalizedAction, "darksight", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreateDarkSightTransform(skillId);
                return true;
            }

            if (string.Equals(normalizedAction, "dualVulcanPrep", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "dualVulcanLoop", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreateSingleActionTransform(skillId, "dualVulcanLoop", "dualVulcanEnd");
                return true;
            }

            if (string.Equals(normalizedAction, "darkTornado_pre", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "darkTornado", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreateSingleActionTransform(skillId, "darkTornado", "darkTornado_after");
                return true;
            }

            if (string.Equals(normalizedAction, "noiseWave_pre", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "noiseWave_ing", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreateSingleActionTransform(skillId, "noiseWave_ing", "noiseWave");
                return true;
            }

            if (string.Equals(normalizedAction, "rapidfire", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreateSingleActionTransform(skillId, "rapidfire", exitActionName: null);
                return true;
            }

            if (string.Equals(normalizedAction, "flamethrower2", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreateMechanicTransform(skillId, "flamethrower2", "flamethrower2", "flamethrower2", "flamethrower2", "flamethrower_after2");
                return true;
            }

            if (string.Equals(normalizedAction, "tank_pre", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreateMechanicTransform(skillId, "tank_stand", "tank_walk", "tank", "tank_prone", "tank_after");
                return true;
            }

            if (string.Equals(normalizedAction, "siege_pre", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreateMechanicTransform(skillId, "siege_stand", "siege_stand", "siege", "siege_stand", "siege_after", locksMovement: true);
                return true;
            }

            if (string.Equals(normalizedAction, "tank_siegepre", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreateMechanicTransform(skillId, "tank_siegestand", "tank_siegestand", "tank_siegeattack", "tank_siegestand", "tank_siegeafter", locksMovement: true);
                return true;
            }

            if (string.Equals(normalizedAction, "rbooster", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "rbooster_pre", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "tank_rbooster_pre", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreateRocketBoosterTransform(skillId, normalizedAction);
                return true;
            }

            return false;
        }

        private static SkillAvatarTransformState CreateMorphTransform(int skillId, CharacterPart morphPart, string actionName)
        {
            string normalizedAction = actionName?.Trim();
            return new SkillAvatarTransformState
            {
                SkillId = skillId,
                SourceId = skillId,
                AvatarPart = morphPart,
                StandActionNames = CreateMorphActionVariants(morphPart, normalizedAction, "stand", "stand1", "stand2"),
                WalkActionNames = CreateMorphActionVariants(morphPart, "walk", "move", "walk1", "walk2", "stand"),
                JumpActionNames = CreateMorphActionVariants(morphPart, "jump", "fly", "stand"),
                ProneActionNames = CreateMorphActionVariants(morphPart, "prone", "stand"),
                AttackActionNames = CreateMorphActionVariants(morphPart, normalizedAction, "attack", "attack1", "walk", "stand"),
                ClimbActionNames = CreateMorphActionVariants(morphPart, "ladder", "rope", "stand"),
                FloatActionNames = CreateMorphActionVariants(morphPart, "fly", "swim", "jump", "stand"),
                HitActionNames = CreateMorphActionVariants(morphPart, "hit", "stand"),
                ExitActionName = null
            };
        }

        private static SkillAvatarTransformState CreateMechanicTransform(int skillId, string standActionName, string walkActionName, string attackActionName, string proneActionName, string exitActionName, bool locksMovement = false)
        {
            return new SkillAvatarTransformState
            {
                SkillId = skillId,
                SourceId = skillId,
                StandActionNames = CreateActionVariants(standActionName),
                WalkActionNames = CreateActionVariants(walkActionName, standActionName),
                JumpActionNames = CreateActionVariants(GetActionFamilyVariant(standActionName, "jump"), standActionName),
                ProneActionNames = CreateActionVariants(proneActionName, standActionName),
                AttackActionNames = CreateActionVariants(attackActionName, standActionName),
                ClimbActionNames = CreateActionVariants(GetActionFamilyVariant(standActionName, "ladder"), GetActionFamilyVariant(standActionName, "rope"), standActionName),
                FloatActionNames = CreateActionVariants(GetActionFamilyVariant(standActionName, "fly"), GetActionFamilyVariant(standActionName, "swim"), standActionName),
                HitActionNames = CreateActionVariants(GetActionFamilyVariant(standActionName, "hit"), standActionName),
                ExitActionName = exitActionName,
                LocksMovement = locksMovement
            };
        }

        private static SkillAvatarTransformState CreateSingleActionTransform(int skillId, string actionName, string exitActionName)
        {
            return new SkillAvatarTransformState
            {
                SkillId = skillId,
                SourceId = skillId,
                StandActionNames = CreateActionVariants(actionName),
                WalkActionNames = CreateActionVariants(actionName),
                JumpActionNames = CreateActionVariants(actionName),
                ProneActionNames = CreateActionVariants(actionName),
                AttackActionNames = CreateActionVariants(actionName),
                ClimbActionNames = CreateActionVariants(actionName),
                FloatActionNames = CreateActionVariants(actionName),
                HitActionNames = CreateActionVariants(actionName),
                ExitActionName = exitActionName
            };
        }

        private static SkillAvatarTransformState CreatePreparedSingleActionTransform(
            int skillId,
            string currentActionName,
            string prepareActionName,
            string holdActionName,
            string exitActionName)
        {
            bool usePrepareAction = string.Equals(currentActionName, prepareActionName, StringComparison.OrdinalIgnoreCase);
            return CreateSingleActionTransform(
                skillId,
                usePrepareAction ? prepareActionName : holdActionName,
                usePrepareAction ? null : exitActionName);
        }

        private static SkillAvatarTransformState CreatePreparedMechanicTransform(
            int skillId,
            string currentActionName,
            string prepareActionName,
            string holdActionName,
            string exitActionName)
        {
            bool usePrepareAction = string.Equals(currentActionName, prepareActionName, StringComparison.OrdinalIgnoreCase);
            string activeActionName = usePrepareAction ? prepareActionName : holdActionName;
            return CreateMechanicTransform(
                skillId,
                activeActionName,
                activeActionName,
                activeActionName,
                activeActionName,
                usePrepareAction ? null : exitActionName);
        }

        private static SkillAvatarTransformState CreateRocketBoosterTransform(int skillId, string actionName)
        {
            string normalizedActionName = actionName?.Trim();
            string transformActionName = string.Equals(normalizedActionName, "tank_rbooster_pre", StringComparison.OrdinalIgnoreCase)
                ? "tank_rbooster_pre"
                : string.Equals(normalizedActionName, "rbooster_pre", StringComparison.OrdinalIgnoreCase)
                    ? "rbooster_pre"
                    : "rbooster";
            string exitActionName = string.Equals(transformActionName, "tank_rbooster_pre", StringComparison.OrdinalIgnoreCase)
                ? "tank_rbooster_after"
                : "rbooster_after";

            return CreateSingleActionTransform(skillId, transformActionName, exitActionName);
        }

        private static SkillAvatarTransformState CreateDarkSightTransform(int skillId)
        {
            return new SkillAvatarTransformState
            {
                SkillId = skillId,
                SourceId = skillId,
                StandActionNames = CreateActionVariants("ghoststand", "darksight"),
                WalkActionNames = CreateActionVariants("ghostwalk", "ghoststand", "darksight"),
                JumpActionNames = CreateActionVariants("ghostjump", "ghostfly", "ghoststand", "darksight"),
                ProneActionNames = CreateActionVariants("ghostproneStab", "ghoststand", "darksight"),
                AttackActionNames = CreateActionVariants("ghoststand", "darksight"),
                ClimbActionNames = CreateActionVariants("ghostladder", "ghostrope", "ghoststand", "darksight"),
                FloatActionNames = CreateActionVariants("ghostfly", "ghostjump", "ghoststand", "darksight"),
                HitActionNames = CreateActionVariants("ghoststand", "darksight"),
                ExitActionName = null
            };
        }

        private static SkillAvatarTransformState CloneTransform(SkillAvatarTransformState transform, int sourceId)
        {
            if (transform == null)
            {
                return null;
            }

            return new SkillAvatarTransformState
            {
                SkillId = transform.SkillId,
                SourceId = sourceId,
                AvatarPart = transform.AvatarPart,
                StandActionNames = transform.StandActionNames,
                WalkActionNames = transform.WalkActionNames,
                JumpActionNames = transform.JumpActionNames,
                ProneActionNames = transform.ProneActionNames,
                AttackActionNames = transform.AttackActionNames,
                ClimbActionNames = transform.ClimbActionNames,
                FloatActionNames = transform.FloatActionNames,
                HitActionNames = transform.HitActionNames,
                ExitActionName = transform.ExitActionName,
                LocksMovement = transform.LocksMovement
            };
        }

        private SkillAvatarTransformState GetActiveAvatarTransform()
        {
            return _activeExternalAvatarTransform ?? _activeSkillAvatarTransform;
        }

        private static IReadOnlyList<string> CreateActionVariants(params string[] actionNames)
        {
            var actions = new List<string>();
            if (actionNames == null)
            {
                return actions;
            }

            foreach (string actionName in actionNames)
            {
                if (string.IsNullOrWhiteSpace(actionName))
                {
                    continue;
                }

                bool alreadyAdded = false;
                foreach (string existingAction in actions)
                {
                    if (string.Equals(existingAction, actionName, StringComparison.OrdinalIgnoreCase))
                    {
                        alreadyAdded = true;
                        break;
                    }
                }

                if (!alreadyAdded)
                {
                    actions.Add(actionName);
                }
            }

            return actions;
        }

        private static IReadOnlyList<string> CreateMorphActionVariants(CharacterPart morphPart, params string[] preferredActions)
        {
            var actions = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(string actionName)
            {
                if (string.IsNullOrWhiteSpace(actionName))
                {
                    return;
                }

                foreach (string candidate in CharacterPart.GetActionLookupStrings(actionName))
                {
                    if (!string.IsNullOrWhiteSpace(candidate) && seen.Add(candidate))
                    {
                        actions.Add(candidate);
                    }
                }
            }

            if (preferredActions != null)
            {
                foreach (string actionName in preferredActions)
                {
                    Add(actionName);
                }
            }

            if (morphPart?.Animations != null)
            {
                foreach (string actionName in morphPart.Animations.Keys)
                {
                    Add(actionName);
                }
            }

            return actions;
        }

        private void UpdateAssemblerAvatarOverride()
        {
            if (Assembler != null)
            {
                Assembler.OverrideAvatarPart = GetActiveAvatarTransform()?.AvatarPart;
            }
        }

        private static string GetActionFamilyVariant(string standActionName, string variantSuffix)
        {
            if (string.IsNullOrWhiteSpace(standActionName) || string.IsNullOrWhiteSpace(variantSuffix))
            {
                return null;
            }

            return standActionName switch
            {
                "tank_stand" => $"tank_{variantSuffix}",
                "siege_stand" => $"siege_{variantSuffix}",
                "tank_siegestand" => $"tank_siege{variantSuffix}",
                _ => null
            };
        }

        /// <summary>
        /// Get movement speed based on current state (in pixels per second)
        /// Official formula: maxSpeed = shoeWalkSpeed * physicsWalkSpeed * footholdWalk
        /// Values loaded from Map.wz/Physics.img
        /// </summary>
        private float GetMoveSpeed()
        {
            // Get character Speed stat (default 100)
            float characterSpeed = (Build?.Speed ?? 100f) * _externalMoveSpeedMultiplier;
            characterSpeed = _moveSpeedCapResolver?.Invoke(characterSpeed) ?? characterSpeed;

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
                return (float)PhysicsConstants.Instance.SwimSpeed;
            }
            else if (State == PlayerState.Flying)
            {
                return (float)PhysicsConstants.Instance.FlySpeed;
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

    public sealed class PlayerMovementSyncSnapshot
    {
        public PlayerMovementSyncSnapshot(PassivePositionSnapshot passivePosition, System.Collections.Generic.List<MovePathElement> movePath)
        {
            PassivePosition = passivePosition;
            MovePath = movePath ?? throw new ArgumentNullException(nameof(movePath));
        }

        public PassivePositionSnapshot PassivePosition { get; }
        public System.Collections.Generic.List<MovePathElement> MovePath { get; }

        public byte[] Encode()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write((byte)1);
            WriteSnapshot(writer, PassivePosition);
            writer.Write(MovePath.Count);

            for (int i = 0; i < MovePath.Count; i++)
            {
                WriteElement(writer, MovePath[i]);
            }

            writer.Flush();
            return stream.ToArray();
        }

        public static PlayerMovementSyncSnapshot Decode(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            using var stream = new MemoryStream(data, writable: false);
            using var reader = new BinaryReader(stream);

            byte version = reader.ReadByte();
            if (version != 1)
            {
                throw new InvalidDataException($"Unsupported movement snapshot version: {version}");
            }

            PassivePositionSnapshot passivePosition = ReadSnapshot(reader);
            int count = reader.ReadInt32();
            var movePath = new System.Collections.Generic.List<MovePathElement>(count);

            for (int i = 0; i < count; i++)
            {
                movePath.Add(ReadElement(reader));
            }

            return new PlayerMovementSyncSnapshot(passivePosition, movePath);
        }

        public PassivePositionSnapshot SampleAtTime(int currentTime)
        {
            if (MovePath.Count == 0 || currentTime >= PassivePosition.TimeStamp)
            {
                return PassivePosition;
            }

            if (currentTime <= MovePath[0].TimeStamp)
            {
                return ToPassivePosition(MovePath[0]);
            }

            for (int i = 0; i < MovePath.Count; i++)
            {
                MovePathElement start = MovePath[i];
                int segmentStart = start.TimeStamp;
                int segmentEnd = i + 1 < MovePath.Count
                    ? MovePath[i + 1].TimeStamp
                    : Math.Max(start.TimeStamp + start.Duration, PassivePosition.TimeStamp);

                if (currentTime > segmentEnd)
                {
                    continue;
                }

                if (i + 1 >= MovePath.Count || segmentEnd <= segmentStart)
                {
                    return currentTime >= PassivePosition.TimeStamp
                        ? PassivePosition
                        : ToPassivePosition(start);
                }

                MovePathElement end = MovePath[i + 1];
                float t = (float)(currentTime - segmentStart) / (segmentEnd - segmentStart);
                t = Math.Clamp(t, 0f, 1f);

                return new PassivePositionSnapshot
                {
                    X = LerpInt(start.X, end.X, t),
                    Y = LerpInt(start.Y, end.Y, t),
                    VelocityX = LerpShort(start.VelocityX, end.VelocityX, t),
                    VelocityY = LerpShort(start.VelocityY, end.VelocityY, t),
                    Action = t < 1f ? start.Action : end.Action,
                    FootholdId = t < 1f ? start.FootholdId : end.FootholdId,
                    TimeStamp = currentTime,
                    FacingRight = t < 0.5f ? start.FacingRight : end.FacingRight
                };
            }

            return PassivePosition;
        }

        private static void WriteSnapshot(BinaryWriter writer, PassivePositionSnapshot snapshot)
        {
            writer.Write(snapshot.X);
            writer.Write(snapshot.Y);
            writer.Write(snapshot.VelocityX);
            writer.Write(snapshot.VelocityY);
            writer.Write((byte)snapshot.Action);
            writer.Write(snapshot.FootholdId);
            writer.Write(snapshot.TimeStamp);
            writer.Write(snapshot.FacingRight);
        }

        private static PassivePositionSnapshot ReadSnapshot(BinaryReader reader)
        {
            return new PassivePositionSnapshot
            {
                X = reader.ReadInt32(),
                Y = reader.ReadInt32(),
                VelocityX = reader.ReadInt16(),
                VelocityY = reader.ReadInt16(),
                Action = (MoveAction)reader.ReadByte(),
                FootholdId = reader.ReadInt32(),
                TimeStamp = reader.ReadInt32(),
                FacingRight = reader.ReadBoolean()
            };
        }

        private static void WriteElement(BinaryWriter writer, MovePathElement element)
        {
            writer.Write(element.X);
            writer.Write(element.Y);
            writer.Write(element.VelocityX);
            writer.Write(element.VelocityY);
            writer.Write((byte)element.Action);
            writer.Write(element.FootholdId);
            writer.Write(element.TimeStamp);
            writer.Write(element.Duration);
            writer.Write(element.FacingRight);
            writer.Write(element.StatChanged);
        }

        private static MovePathElement ReadElement(BinaryReader reader)
        {
            return new MovePathElement
            {
                X = reader.ReadInt32(),
                Y = reader.ReadInt32(),
                VelocityX = reader.ReadInt16(),
                VelocityY = reader.ReadInt16(),
                Action = (MoveAction)reader.ReadByte(),
                FootholdId = reader.ReadInt32(),
                TimeStamp = reader.ReadInt32(),
                Duration = reader.ReadInt16(),
                FacingRight = reader.ReadBoolean(),
                StatChanged = reader.ReadBoolean()
            };
        }

        private static PassivePositionSnapshot ToPassivePosition(MovePathElement element)
        {
            return new PassivePositionSnapshot
            {
                X = element.X,
                Y = element.Y,
                VelocityX = element.VelocityX,
                VelocityY = element.VelocityY,
                Action = element.Action,
                FootholdId = element.FootholdId,
                TimeStamp = element.TimeStamp,
                FacingRight = element.FacingRight
            };
        }

        private static int LerpInt(int start, int end, float t)
        {
            return (int)Math.Round(start + ((end - start) * t));
        }

        private static short LerpShort(short start, short end, float t)
        {
            return (short)Math.Round(start + ((end - start) * t));
        }
    }
}
