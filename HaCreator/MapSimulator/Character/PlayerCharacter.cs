using System;
using System.Collections.Generic;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.Core;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Physics;
using HaCreator.MapSimulator.Pools;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

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

    public readonly struct PlayerLandingInfo
    {
        public PlayerLandingInfo(float fallStartY, float landingY, float impactVelocityY)
        {
            FallStartY = fallStartY;
            LandingY = landingY;
            ImpactVelocityY = impactVelocityY;
        }

        public float FallStartY { get; }
        public float LandingY { get; }
        public float ImpactVelocityY { get; }
        public float FallDistance => Math.Max(0f, LandingY - FallStartY);
    }

    internal readonly record struct PacketOwnedEmotionState(
        int EmotionId,
        string EmotionName,
        int DurationMs,
        int AppliedAt,
        int ExpireTime,
        bool ByItemOption)
    {
        public bool HasFiniteDuration => DurationMs > 0 && ExpireTime > AppliedAt;

        public bool IsExpired(int currentTime)
        {
            return ExpireTime > 0 && currentTime >= ExpireTime;
        }
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
            public IReadOnlyList<string> SitActionNames { get; init; }
            public IReadOnlyList<string> ProneActionNames { get; init; }
            public IReadOnlyList<string> AttackActionNames { get; init; }
            public IReadOnlyList<string> LadderActionNames { get; init; }
            public IReadOnlyList<string> RopeActionNames { get; init; }
            public IReadOnlyList<string> FlyActionNames { get; init; }
            public IReadOnlyList<string> AirborneMoveActionNames { get; init; }
            public IReadOnlyList<string> AirborneAttackActionNames { get; init; }
            public IReadOnlyList<string> SwimActionNames { get; init; }
            public IReadOnlyList<string> HitActionNames { get; init; }
            public IReadOnlyList<string> DeadActionNames { get; init; }
            public string ExitActionName { get; init; }
            public bool LocksMovement { get; init; }
        }

        internal readonly record struct SkillAvatarTransformResolutionForTesting(
            IReadOnlyList<string> StandActionNames,
            IReadOnlyList<string> WalkActionNames,
            IReadOnlyList<string> JumpActionNames,
            IReadOnlyList<string> AttackActionNames,
            IReadOnlyList<string> LadderActionNames,
            IReadOnlyList<string> RopeActionNames,
            IReadOnlyList<string> FlyActionNames,
            IReadOnlyList<string> SwimActionNames,
            IReadOnlyList<string> HitActionNames,
            IReadOnlyList<string> ProneActionNames,
            string ExitActionName,
            bool LocksMovement);

        internal readonly record struct MorphAvatarTransformResolutionForTesting(
            IReadOnlyList<string> StandActionNames,
            IReadOnlyList<string> WalkActionNames,
            IReadOnlyList<string> JumpActionNames,
            IReadOnlyList<string> FlyActionNames,
            IReadOnlyList<string> AirborneMoveActionNames,
            IReadOnlyList<string> AirborneAttackActionNames,
            IReadOnlyList<string> LadderActionNames,
            IReadOnlyList<string> RopeActionNames,
            IReadOnlyList<string> SwimActionNames,
            IReadOnlyList<string> AttackActionNames,
            IReadOnlyList<string> HitActionNames,
            IReadOnlyList<string> DeadActionNames);

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
            public bool HideOnRotateAction { get; init; }
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
            public SkillAnimation FinishAnimation { get; init; }
            public SkillAnimation FinishSecondaryAnimation { get; init; }
            public int AnimationStartTime { get; set; }
            public SkillAvatarEffectPlane Plane { get; init; }
            public SkillAvatarEffectPlane SecondaryPlane { get; init; }
            public SkillAvatarEffectPlane FinishPlane { get; init; }
            public SkillAvatarEffectPlane FinishSecondaryPlane { get; init; }
            public bool IsFinishing { get; set; }

            public bool HasFinishAnimation =>
                FinishAnimation != null
                || FinishSecondaryAnimation != null;
        }

        private sealed class MeleeAfterImageState
        {
            public int SkillId { get; init; }
            public string ActionName { get; init; }
            public MeleeAfterImageAction AfterImageAction { get; init; }
            public int AnimationStartTime { get; set; }
            public int ActivationStartTime { get; init; }
            public bool FacingRight { get; init; }
            public int ActionDuration { get; init; }
            public int FadeStartTime { get; set; } = -1;
            public int LastFrameIndex { get; set; } = -1;
            public int LastFrameElapsedMs { get; set; }
            public IReadOnlyList<AfterimageRenderableLayer> LastResolvedLayers { get; set; } = Array.Empty<AfterimageRenderableLayer>();
        }

        private sealed class ShadowPartnerState
        {
            public int SkillId { get; init; }
            public IReadOnlyDictionary<string, SkillAnimation> ActionAnimations { get; init; }
            public IReadOnlySet<string> SupportedRawActionNames { get; init; }
            public int HorizontalOffsetPx { get; init; }
            public Point CurrentClientOffsetPx { get; set; }
            public Point ClientOffsetStartPx { get; set; }
            public Point ClientOffsetTargetPx { get; set; }
            public int ClientOffsetTransitionStartTime { get; set; }
            public string CurrentActionName { get; set; }
            public SkillAnimation CurrentPlaybackAnimation { get; set; }
            public int CurrentActionStartTime { get; set; }
            public bool CurrentFacingRight { get; set; }
            public string ObservedPlayerActionName { get; set; }
            public string PendingActionName { get; set; }
            public SkillAnimation PendingPlaybackAnimation { get; set; }
            public int PendingActionReadyTime { get; set; }
            public bool PendingFacingRight { get; set; }
            public bool PendingForceReplay { get; set; }
            public string QueuedActionName { get; set; }
            public SkillAnimation QueuedPlaybackAnimation { get; set; }
            public bool QueuedFacingRight { get; set; }
            public bool QueuedForceReplay { get; set; }
            public bool ObservedPlayerFacingRight { get; set; }
            public bool ObservedPlayerFloatingState { get; set; }
            public int ObservedPlayerActionTriggerTime { get; set; } = int.MinValue;
        }

        private sealed class MirrorImageState
        {
            public int SkillId { get; init; }
            public int StartTime { get; set; }
            public string ObservedPlayerActionName { get; set; }
            public bool ObservedPlayerFacingRight { get; set; }
            public PlayerState ObservedPlayerState { get; set; }
            public Point CurrentOffsetPx { get; set; }
            public bool Visible { get; set; }
            public string PreparedActionName { get; set; }
            public int PreparedFrameIndex { get; set; } = -1;
            public int PreparedFeetOffset { get; set; }
            public MirrorImagePreparedSourceLayer[] PreparedSourceLayers { get; set; } = CreateEmptyMirrorImagePreparedSourceLayers();
        }

        private sealed class MirrorImagePreparedSourceLayer
        {
            public AvatarRenderLayer RenderLayer { get; set; }
            public int SourceSignature { get; set; }
            public Rectangle Bounds { get; set; }
            public Rectangle TextureSourceBounds { get; set; }
            public Point Origin { get; set; }
            public int PreparedCurrentTime { get; set; } = int.MinValue;
            public int PreparedSourceLayerCurrentTime { get; set; } = int.MinValue;
            public bool PreparedFacingRight { get; set; }
            public AvatarRenderLayer OverlayTargetLayer { get; set; } = AvatarRenderLayer.UnderFace;
            public Texture2D ComposedTexture { get; set; }
            public IReadOnlyList<AssembledPart> Parts { get; set; } = Array.Empty<AssembledPart>();
        }

        private readonly struct MirrorImageRenderableSourceLayer
        {
            public MirrorImageRenderableSourceLayer(
                Texture2D preparedSnapshotTexture,
                Rectangle preparedSnapshotSourceBounds,
                Rectangle positionBounds,
                Point origin,
                bool facingRight,
                int transitionStartTime)
            {
                PreparedSnapshotTexture = preparedSnapshotTexture;
                PreparedSnapshotSourceBounds = preparedSnapshotSourceBounds;
                PositionBounds = positionBounds;
                Origin = origin;
                FacingRight = facingRight;
                TransitionStartTime = transitionStartTime;
            }

            public Texture2D PreparedSnapshotTexture { get; }
            public Rectangle PreparedSnapshotSourceBounds { get; }
            public Rectangle PositionBounds { get; }
            public Point Origin { get; }
            public bool FacingRight { get; }
            public int TransitionStartTime { get; }
        }

        private readonly struct AvatarEffectRenderable
        {
            public AvatarEffectRenderable(SkillFrame frame, SkillAvatarEffectPlane plane, int? positionCode)
            {
                Frame = frame;
                Plane = plane;
                PositionCode = positionCode;
            }

            public SkillFrame Frame { get; }
            public SkillAvatarEffectPlane Plane { get; }
            public int? PositionCode { get; }
        }

        internal readonly record struct PersistentSkillAvatarEffectRenderSelection(
            SkillAnimation OverlayAnimation,
            SkillAnimation OverlaySecondaryAnimation,
            SkillAnimation UnderFaceAnimation,
            SkillAnimation UnderFaceSecondaryAnimation,
            bool OverlayUsesBehindCharacterPlane);

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
        private const int BattleshipTamingMobItemId = 1932000;
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
        private const int ShadowPartnerClientSideOffsetPx = 30;
        private const int ShadowPartnerClientBackActionOffsetYPx = 50;
        private const int ShadowPartnerTransitionDurationMs = 200;
        private const int MirrorImageClientSideOffsetPx = 50;
        private const int MirrorImageClientBackActionOffsetYPx = 50;
        private const int MirrorImageTransitionDurationMs = 200;
        private const int PacketOwnedEmotionEffectSkillId = 2099000000;
        private const int BlinkEmotionId = 8;
        // Float idle should ignore the tiny passive sink applied by swim physics.
        private const float FLOAT_ANIMATION_MOVEMENT_THRESHOLD = 20f;
        // CActionMan action metadata uses 150 as the default alpha for composed character pieces.
        private static readonly Color ShadowPartnerTint = new(255, 255, 255, 150);
        private static readonly Color MirrorImageTint = new(128, 128, 128, 208);
        private static readonly HashSet<int> PacketOwnedEmotionSuppressedRawActionCodes = new()
        {
            41,
            42,
            57
        };
        private static readonly HashSet<string> PacketOwnedEmotionSuppressedActionNames =
            BuildPacketOwnedEmotionSuppressedActionNames();

        #endregion

        #region Properties

        public CharacterBuild Build { get; private set; }
        public string Name => Build?.Name ?? string.Empty;
        public CharacterAssembler Assembler { get; private set; }
        public CVecCtrl Physics { get; private set; }

        // State
        public PlayerState State { get; private set; } = PlayerState.Standing;
        public CharacterAction CurrentAction { get; private set; } = CharacterAction.Stand1;
        public string CurrentActionName { get; private set; } = CharacterPart.GetActionString(CharacterAction.Stand1);
        public int CurrentSkillAnimationSkillId { get; private set; }
        public int CurrentSkillAnimationStartTime { get; private set; } = int.MinValue;
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
        public bool IsPlayingClientOwnedOneTimeAction => State == PlayerState.Attacking
                                                         && !_sustainedSkillAnimation
                                                         && CurrentSkillAnimationStartTime != int.MinValue;
        public bool IsRecordingMovementPath => Physics?.IsRecordingPath == true;
        public bool IsMovementLockedBySkillTransform => GetActiveAvatarTransform()?.LocksMovement == true;
        public bool HasActiveMorphTransform => GetActiveAvatarTransform()?.AvatarPart?.Type == CharacterPartType.Morph;
        public int HorizontalInputDirection => _inputLeft == _inputRight ? 0 : (_inputRight ? 1 : -1);

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

        public bool HasSkillBlockingStatus(PlayerSkillBlockingStatus status, int currentTime)
        {
            ClearExpiredSkillBlockingStatuses(currentTime);
            return _activeSkillBlockingStatuses.ContainsKey(status);
        }

        public bool IsImmovableForPassiveTransferField(int currentTime)
        {
            ClearExpiredSkillBlockingStatuses(currentTime);
            return IsMovementLockedBySkillTransform
                   || State == PlayerState.Dead
                   || State == PlayerState.Hit
                   || State == PlayerState.Attacking
                   || State == PlayerState.Sitting
                   || State == PlayerState.Ladder
                   || State == PlayerState.Rope
                   || _activeSkillBlockingStatuses.ContainsKey(PlayerSkillBlockingStatus.StopMotion);
        }

        public bool IsAttractLockedForPassiveTransferField(int currentTime)
        {
            return HasSkillBlockingStatus(PlayerSkillBlockingStatus.Attract, currentTime);
        }

        public PacketOwnedUserSummonRegistry PacketOwnedSummons { get; } = new();

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
        private readonly Random _packetOwnedEmotionRandom = new(unchecked((Environment.TickCount * 397) ^ 0x232));
        private readonly Dictionary<string, SkillAnimation> _packetOwnedEmotionEffectCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly GraphicsDevice _graphicsDevice;
        private int _nextBlinkTime = Environment.TickCount + FACE_BLINK_MIN_INTERVAL_MS;
        private int _blinkExpressionEndTime;
        private int _hitExpressionEndTime;
        private int _packetOwnedEmotionId;
        private int _packetOwnedEmotionDurationMs;
        private int _packetOwnedEmotionAppliedAt;
        private int _packetOwnedEmotionEndTime;
        private int _nextPortableChairRecoveryTime = int.MaxValue;
        private string _packetOwnedEmotionName = "default";
        private PacketOwnedEmotionState? _lastPacketOwnedEmotionState;
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
        private int _hitStateDurationMs = HIT_STUN_DURATION;
        private bool _hitExpressionSuppressedByPacketOwnedItemOption;

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
        private bool _landingTrackingActive;
        private float _landingTrackingStartY;

        // Callbacks
        public Action<PlayerCharacter, Rectangle> OnAttackHitbox;
        public Action<PlayerCharacter> OnDeath;
        public Action<PlayerCharacter, int> OnDamaged;

        // Sound callbacks
        private Action _onJumpSound;
        private Action<string> _onWeaponSfxSound;
        private Func<string> _jumpRestrictionMessageProvider;
        private Func<string> _jumpDownRestrictionMessageProvider;
        private Action<string> _onJumpRestricted;
        private Func<float, float> _moveSpeedCapResolver;
        private Action<PlayerCharacter, PlayerLandingInfo> _onLanded;

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
        private bool _packetOwnedChairSitConfirmed;
        private bool _portableChairExternalPairRequested;
        private bool _portableChairHasExternalPair;
        private Vector2 _portableChairExternalPairPosition;
        private bool _portableChairExternalPairFacingRight;
        private PortableChair _portableChairExternalOwnerChair;
        private bool _portableChairHasExternalOwnerPair;
        private Vector2 _portableChairExternalOwnerPosition;
        private bool _portableChairExternalOwnerFacingRight;
        private bool _packetOwnedEmotionByItemOption;
        private ShadowPartnerState _activeShadowPartner;
        private MirrorImageState _activeMirrorImage;

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
            _graphicsDevice = device;
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
            ResetLandingTracking();
        }

        public void ApplyPacketOwnedPassiveMove(PassivePositionSnapshot snapshot, int currentTime, FootholdLine foothold = null)
        {
            Physics.X = snapshot.X;
            Physics.Y = snapshot.Y;
            Physics.VelocityX = snapshot.VelocityX;
            Physics.VelocityY = snapshot.VelocityY;
            Physics.CurrentAction = snapshot.Action;
            FacingRight = snapshot.FacingRight;
            Physics.FacingRight = snapshot.FacingRight;
            ApplyPacketOwnedPassiveMoveFootholdState(snapshot, foothold);
            ResetLandingTracking();
            ClearForcedActionName();
            CurrentSkillAnimationSkillId = 0;

            PlayerState newState = snapshot.Action switch
            {
                MoveAction.Walk => PlayerState.Walking,
                MoveAction.Jump => PlayerState.Jumping,
                MoveAction.Fall => PlayerState.Falling,
                MoveAction.Ladder => PlayerState.Ladder,
                MoveAction.Rope => PlayerState.Rope,
                MoveAction.Swim => PlayerState.Swimming,
                MoveAction.Fly => PlayerState.Flying,
                MoveAction.Hit => PlayerState.Hit,
                MoveAction.Die => PlayerState.Dead,
                _ => PlayerState.Standing
            };

            string newActionName = ResolvePacketOwnedPassiveMoveActionName(newState, snapshot.Action);
            CharacterAction newAction = newState switch
            {
                PlayerState.Walking => ResolveClientWalkAction(),
                PlayerState.Jumping or PlayerState.Falling => CharacterAction.Jump,
                PlayerState.Ladder => CharacterAction.Ladder,
                PlayerState.Rope => CharacterAction.Rope,
                PlayerState.Swimming => CharacterAction.Swim,
                PlayerState.Flying => CharacterAction.Fly,
                PlayerState.Dead => CharacterAction.Dead,
                _ => ResolveClientStandAction()
            };
            if (State != newState
                || CurrentAction != newAction
                || !string.Equals(CurrentActionName, newActionName, StringComparison.Ordinal))
            {
                _animationStartTime = currentTime;
            }

            State = newState;
            CurrentAction = newAction;
            CurrentActionName = newActionName;
        }

        private string ResolvePacketOwnedPassiveMoveActionName(PlayerState newState, MoveAction moveAction)
        {
            if (Build?.ActivePortableChair != null)
            {
                return CharacterPart.GetActionString(CharacterAction.Sit);
            }

            return moveAction switch
            {
                MoveAction.Walk => CharacterPart.GetActionString(ResolveClientWalkAction()),
                MoveAction.Jump or MoveAction.Fall => CharacterPart.GetActionString(CharacterAction.Jump),
                MoveAction.Ladder => CharacterPart.GetActionString(CharacterAction.Ladder),
                MoveAction.Rope => CharacterPart.GetActionString(CharacterAction.Rope),
                MoveAction.Swim => CharacterPart.GetActionString(CharacterAction.Swim),
                MoveAction.Fly => CharacterPart.GetActionString(CharacterAction.Fly),
                MoveAction.Attack or MoveAction.Hit => CharacterPart.GetActionString(CharacterAction.Alert),
                MoveAction.Die => CharacterPart.GetActionString(CharacterAction.Dead),
                _ => CharacterPart.GetActionString(newState == PlayerState.Walking
                    ? ResolveClientWalkAction()
                    : ResolveClientStandAction())
            };
        }

        private void ApplyPacketOwnedPassiveMoveFootholdState(PassivePositionSnapshot snapshot, FootholdLine foothold)
        {
            if (foothold != null)
            {
                Physics.CurrentFoothold = foothold;
                Physics.FallStartFoothold = null;
                Physics.IsOnLadderOrRope = false;
                return;
            }

            if (snapshot.FootholdId != 0)
            {
                return;
            }

            Physics.CurrentFoothold = null;
            Physics.FallStartFoothold = null;
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

        public void SetWeaponSfxSoundCallback(Action<string> onWeaponSfx)
        {
            _onWeaponSfxSound = onWeaponSfx;
        }

        public void SetLandingHandler(Action<PlayerCharacter, PlayerLandingInfo> onLanded)
        {
            _onLanded = onLanded;
        }

        public void SetJumpRestrictionHandler(
            Func<string> getRestrictionMessage,
            Func<string> getJumpDownRestrictionMessage,
            Action<string> onJumpRestricted)
        {
            _jumpRestrictionMessageProvider = getRestrictionMessage;
            _jumpDownRestrictionMessageProvider = getJumpDownRestrictionMessage;
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
            if (GetPortableChairActivationRestrictionMessage(chair) != null)
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
            CurrentActionName = ResolvePortableChairActionName(chair);
            _animationStartTime = Environment.TickCount;
            _nextPortableChairRecoveryTime = Environment.TickCount + PORTABLE_CHAIR_RECOVERY_INTERVAL_MS;
            _packetOwnedChairSitConfirmed = false;
            ConfigurePortableChairPairPreview(chair);
            return true;
        }

        internal string GetPortableChairActivationRestrictionMessage(PortableChair chair)
        {
            return ResolvePortableChairActivationRestrictionMessage(
                chair,
                Build != null,
                IsAlive,
                State,
                Physics?.IsOnFoothold() == true,
                HasActiveMorphTransform,
                HasPortableChairBlockedMountState());
        }

        public bool PacketOwnedChairSitConfirmed => _packetOwnedChairSitConfirmed;

        public void ApplyPacketOwnedSitPlacement(float x, float y)
        {
            Physics.X = x;
            Physics.Y = y;
            Physics.VelocityX = 0;
            Physics.VelocityY = 0;
            Physics.CurrentAction = MoveAction.Stand;
            ClearForcedActionName();
            State = PlayerState.Sitting;
            CurrentAction = CharacterAction.Sit;
            CurrentActionName = ResolvePortableChairActionName(Build?.ActivePortableChair);
            _animationStartTime = Environment.TickCount;
            _packetOwnedChairSitConfirmed = true;
        }

        public void ApplyPacketOwnedChairStandCorrection()
        {
            _packetOwnedChairSitConfirmed = false;
            if (Build?.ActivePortableChair != null)
            {
                ClearPortableChair();
                return;
            }

            SetPortableChairPairRequestActive(false);
            ClearPortableChairExternalOwnerPair();
            if (State == PlayerState.Sitting)
            {
                State = Physics.IsOnFoothold() ? PlayerState.Standing : PlayerState.Falling;
                CurrentAction = CharacterAction.Stand1;
                CurrentActionName = CharacterPart.GetActionString(CharacterAction.Stand1);
                _animationStartTime = Environment.TickCount;
            }
        }

        public void ClearPortableChair(bool standUp = true)
        {
            if (Build?.ActivePortableChair == null)
            {
                return;
            }

            Build.ActivePortableChair = null;
            _packetOwnedChairSitConfirmed = false;
            ClearPortableChairPairPreview();
            SetPortableChairPairRequestActive(false);
            ClearPortableChairExternalOwnerPair();
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

            if (_activeSkillBlockingStatuses.ContainsKey(PlayerSkillBlockingStatus.StopMotion))
            {
                return PlayerSkillBlockingStatusMapper.GetRestrictionMessage(PlayerSkillBlockingStatus.StopMotion);
            }

            if (_activeSkillBlockingStatuses.ContainsKey(PlayerSkillBlockingStatus.Attract))
            {
                return PlayerSkillBlockingStatusMapper.GetRestrictionMessage(PlayerSkillBlockingStatus.Attract);
            }

            if (_activeSkillBlockingStatuses.ContainsKey(PlayerSkillBlockingStatus.Polymorph))
            {
                return PlayerSkillBlockingStatusMapper.GetRestrictionMessage(PlayerSkillBlockingStatus.Polymorph);
            }

            return null;
        }

        public void ClearSkillBlockingStatuses()
        {
            _activeSkillBlockingStatuses.Clear();
        }

        public bool ClearSkillBlockingStatus(PlayerSkillBlockingStatus status)
        {
            return _activeSkillBlockingStatuses.Remove(status);
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
            bool wasSurfaceAttached = Physics.IsOnFoothold() || Physics.IsOnLadderOrRope;
            float surfaceYBeforeUpdate = Y;

            // Handle GM fly mode toggle
            if (_inputGmFlyToggle)
            {
                ToggleGmFlyMode();
                _inputGmFlyToggle = false;
            }

            // GM Fly Mode - free movement ignoring physics
            if (GmFlyMode)
            {
                ResetLandingTracking();
                UpdateGmFlyMode(deltaTime);
                UpdateAnimation(currentTime);
                UpdateOwnedTamingMobRenderState();
                UpdateShadowPartnerRenderState(currentTime);
                UpdateMirrorImageRenderState(currentTime);
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

            if (wasSurfaceAttached
                && !_landingTrackingActive
                && !Physics.IsOnFoothold()
                && !Physics.IsOnLadderOrRope
                && !Physics.IsInSwimArea
                && !Physics.IsUserFlying())
            {
                _landingTrackingActive = true;
                _landingTrackingStartY = surfaceYBeforeUpdate;
            }

            RefreshSwimAreaState();

            // Update state machine
            UpdateStateMachine(currentTime);

            // Update animation
            UpdateAnimation(currentTime);
            UpdateOwnedTamingMobRenderState();
            UpdateShadowPartnerRenderState(currentTime);
            UpdateMirrorImageRenderState(currentTime);
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
                ResetLandingTracking();
                // Swimming/Flying movement - when not on foothold
                ProcessFloatMovement(tSec);
            }
            else if ((Physics.IsInSwimArea || Physics.IsUserFlying()) && Physics.IsOnFoothold() && (_inputUp || _inputJump))
            {
                ResetLandingTracking();
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
                    System.Diagnostics.Debug.WriteLine($"[PlayerCharacter Physics] accel={walkForce/entityMass:F1} px/s・ゑｽｲ, decel={walkDrag/entityMass:F1} px/s・ゑｽｲ");
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
                string jumpDownRestrictionMessage = _jumpDownRestrictionMessageProvider?.Invoke();
                if (!string.IsNullOrWhiteSpace(jumpDownRestrictionMessage))
                {
                    _onJumpRestricted?.Invoke(jumpDownRestrictionMessage);
                    return;
                }

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
            const float horizontalSearchRange = 50f;
            const float bottomGrabTolerance = 10f;

            bool foundLadder = Physics.TryGetLadderOrRope(X, Y, horizontalSearchRange, out LadderOrRopeInfo ladder);
            if (!foundLadder)
            {
                foundLadder = Physics.TryGetLadderOrRope(X, Y - bottomGrabTolerance, horizontalSearchRange, out ladder)
                              && Y >= ladder.Top
                              && Y <= ladder.Bottom + bottomGrabTolerance;
            }

            if (foundLadder)
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

                void CompleteLanding(FootholdLine footholdToLandOn)
                {
                    float landingY = (float)CalculateYOnFoothold(footholdToLandOn, X);
                    float impactVelocityY = (float)Math.Max(0d, Physics.VelocityY);
                    Physics.LandOnFoothold(footholdToLandOn);
                    State = PlayerState.Standing;
                    NotifyLanding(landingY, impactVelocityY);
                }

                // Check if we're falling through this foothold
                if (Physics.FallStartFoothold != fh)
                {
                    // Landing on a different foothold - always allowed
                    CompleteLanding(fh);
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
                        CompleteLanding(fh);
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
                        CompleteLanding(fh);
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
                if (currentTime - _hitStateStartTime >= Math.Max(1, _hitStateDurationMs))
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
                PlayerState.Standing => ResolveClientStandAction(),
                PlayerState.Walking => ResolveClientWalkAction(),
                PlayerState.Jumping or PlayerState.Falling => CharacterAction.Jump,
                PlayerState.Ladder => CharacterAction.Ladder,
                PlayerState.Rope => CharacterAction.Rope,
                PlayerState.Sitting => CharacterAction.Sit,
                PlayerState.Prone => CharacterAction.Prone,
                PlayerState.Swimming => CharacterAction.Swim,
                PlayerState.Flying => CharacterAction.Fly,
                PlayerState.Attacking => GetAttackAction(),
                PlayerState.Hit => ResolveClientStandAction(),
                PlayerState.Dead => CharacterAction.Dead,
                _ => ResolveClientStandAction()
            };

            string newActionName;
            if (State == PlayerState.Attacking && !string.IsNullOrWhiteSpace(_forcedActionName))
            {
                newActionName = ResolveAvatarActionLayerCoordinatorActionName(_forcedActionName) ?? _forcedActionName;
            }
            else if (State == PlayerState.Sitting)
            {
                newActionName = ResolvePortableChairActionName(Build?.ActivePortableChair);
            }
            else
            {
                newActionName = ResolveAvatarActionLayerCoordinatorActionName(
                    GetSkillTransformActionName(State) ?? CharacterPart.GetActionString(newAction))
                    ?? CharacterPart.GetActionString(newAction);
            }

            bool isFloatAction = IsFloatAnimationAction(newAction);
            bool isFloatMoving = isFloatAction && ShouldAnimateFloatAction();

            if (newAction != CurrentAction || !string.Equals(newActionName, CurrentActionName, StringComparison.Ordinal) || (isFloatAction && isFloatMoving != _isFloatAnimationMoving))
            {
                _animationStartTime = currentTime;
            }

            CurrentAction = newAction;
            CurrentActionName = newActionName;
            if (State != PlayerState.Attacking)
            {
                CurrentSkillAnimationSkillId = 0;
                CurrentSkillAnimationStartTime = int.MinValue;
            }
            _isFloatAnimationMoving = isFloatMoving;
            SyncAssemblerActionLayerContext();
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

            bool packetOwnedEmotionActive = TryGetActivePacketOwnedEmotionName(currentTime, out string packetOwnedEmotionName);
            bool hitExpressionActive = State == PlayerState.Hit || currentTime < _hitExpressionEndTime;
            if (!hitExpressionActive)
            {
                _hitExpressionSuppressedByPacketOwnedItemOption = false;
            }

            if (ShouldUsePacketOwnedHitFaceExpression(
                    hitExpressionActive,
                    _hitExpressionSuppressedByPacketOwnedItemOption,
                    packetOwnedEmotionActive,
                    _hitStateStartTime,
                    _packetOwnedEmotionAppliedAt))
            {
                expressionName = "hit";
            }
            else if (packetOwnedEmotionActive)
            {
                expressionName = packetOwnedEmotionName;
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

        public bool TryApplyPacketOwnedEmotion(int emotionId, int durationMs, bool byItemOption, int currentTime, out string message)
        {
            message = null;

            if (!PacketOwnedAvatarEmotionResolver.TryResolveEmotionName(emotionId, out string emotionName))
            {
                message = $"Packet-owned emotion id {emotionId} is not supported by the simulator emotion catalog.";
                return false;
            }

            SkillAnimation emotionEffectAnimation = LoadPacketOwnedEmotionEffectAnimation(emotionName);
            int faceLookDuration = 0;
            if (Assembler?.TryGetFaceLookDuration(emotionName, out int resolvedFaceLookDuration) == true)
            {
                faceLookDuration = resolvedFaceLookDuration;
            }

            int resolvedDurationMs = ResolvePacketOwnedEmotionDuration(
                durationMs,
                byItemOption,
                emotionEffectAnimation?.TotalDuration ?? 0,
                faceLookDuration,
                out bool usedFaceLookDurationFallback);
            _packetOwnedEmotionByItemOption = byItemOption;

            if (string.Equals(emotionName, "default", StringComparison.OrdinalIgnoreCase))
            {
                _lastPacketOwnedEmotionState = new PacketOwnedEmotionState(
                    emotionId,
                    emotionName,
                    0,
                    currentTime,
                    0,
                    byItemOption);
                ResetPacketOwnedEmotionState(clearVisualEffect: true);
                message = "Cleared packet-owned avatar emotion state.";
                return true;
            }

            if (ShouldSuppressPacketOwnedEmotionApplication(emotionId))
            {
                _lastPacketOwnedEmotionState = new PacketOwnedEmotionState(
                    emotionId,
                    emotionName,
                    resolvedDurationMs,
                    currentTime,
                    resolvedDurationMs > 0 ? currentTime + resolvedDurationMs : 0,
                    byItemOption);
                message = HasActiveMorphTransform
                    ? $"Ignored packet-owned avatar emotion '{emotionName}' ({emotionId}) because a morph transform is active."
                    : $"Ignored packet-owned avatar emotion '{emotionName}' ({emotionId}) because the current client raw action suppresses blink emotion while the one-time action is active.";
                return true;
            }

            _packetOwnedEmotionId = emotionId;
            _packetOwnedEmotionDurationMs = resolvedDurationMs;
            _packetOwnedEmotionAppliedAt = currentTime;
            _packetOwnedEmotionName = emotionName;
            _packetOwnedEmotionEndTime = resolvedDurationMs > 0 ? currentTime + resolvedDurationMs : 0;
            _lastPacketOwnedEmotionState = new PacketOwnedEmotionState(
                emotionId,
                emotionName,
                resolvedDurationMs,
                currentTime,
                _packetOwnedEmotionEndTime,
                byItemOption);

            if (Assembler != null)
            {
                Assembler.FaceExpressionName = emotionName;
            }

            if (emotionEffectAnimation?.Frames.Count > 0 == true)
            {
                ApplyTransientSkillAvatarEffect(PacketOwnedEmotionEffectSkillId, emotionEffectAnimation, null, currentTime);
            }
            else
            {
                ClearTransientSkillAvatarEffect(PacketOwnedEmotionEffectSkillId);
            }

            string durationText = resolvedDurationMs > 0
                ? $"{resolvedDurationMs} ms"
                : "until cleared";
            if (usedFaceLookDurationFallback)
            {
                durationText += " (face-look fallback)";
            }
            string sourceText = byItemOption ? "item-option" : "packet";
            message = $"Applied packet-owned avatar emotion '{emotionName}' ({emotionId}) for {durationText} via {sourceText}.";
            return true;
        }

        internal static int ResolvePacketOwnedEmotionDuration(
            int durationMs,
            bool byItemOption,
            int emotionEffectDurationMs,
            int faceLookDurationMs,
            out bool usedFaceLookDurationFallback)
        {
            usedFaceLookDurationFallback = false;
            _ = byItemOption;

            int resolvedDurationMs = Math.Max(0, durationMs);
            if (resolvedDurationMs > 0)
            {
                return resolvedDurationMs;
            }

            if (emotionEffectDurationMs > 0)
            {
                return emotionEffectDurationMs;
            }

            if (faceLookDurationMs > 0)
            {
                usedFaceLookDurationFallback = true;
                return faceLookDurationMs;
            }

            return 0;
        }

        internal static bool ShouldUsePacketOwnedHitFaceExpression(
            bool hitExpressionActive,
            bool hitExpressionSuppressedByItemOption,
            bool packetOwnedEmotionActive,
            int hitExpressionStartedAt,
            int packetOwnedEmotionAppliedAt)
        {
            if (!hitExpressionActive || hitExpressionSuppressedByItemOption)
            {
                return false;
            }

            return !packetOwnedEmotionActive || hitExpressionStartedAt >= packetOwnedEmotionAppliedAt;
        }

        private bool ShouldSuppressPacketOwnedEmotionApplication(int emotionId)
        {
            int? rawActionCode = TryGetCurrentClientRawActionCode(out int resolvedRawActionCode)
                ? resolvedRawActionCode
                : null;
            return ShouldSuppressPacketOwnedEmotionApplication(
                HasActiveMorphTransform,
                emotionId,
                rawActionCode,
                _forcedActionName,
                CurrentActionName,
                CharacterPart.GetActionString(CurrentAction));
        }

        internal static bool ShouldSuppressPacketOwnedEmotionApplication(
            bool hasActiveMorphTransform,
            int emotionId,
            int? rawActionCode,
            params string[] actionNames)
        {
            if (hasActiveMorphTransform)
            {
                return true;
            }

            if (emotionId != BlinkEmotionId)
            {
                return false;
            }

            if (rawActionCode.HasValue && PacketOwnedEmotionSuppressedRawActionCodes.Contains(rawActionCode.Value))
            {
                return true;
            }

            if (actionNames == null || actionNames.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < actionNames.Length; i++)
            {
                string actionName = actionNames[i];
                if (!string.IsNullOrWhiteSpace(actionName)
                    && PacketOwnedEmotionSuppressedActionNames.Contains(actionName))
                {
                    return true;
                }
            }

            return false;
        }

        private static HashSet<string> BuildPacketOwnedEmotionSuppressedActionNames()
        {
            var actionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (int rawActionCode in PacketOwnedEmotionSuppressedRawActionCodes)
            {
                if (CharacterPart.TryGetActionStringFromCode(rawActionCode, out string actionName)
                    && !string.IsNullOrWhiteSpace(actionName))
                {
                    actionNames.Add(actionName);
                }
            }

            return actionNames;
        }

        internal bool TryGetPacketOwnedEmotionState(int currentTime, out PacketOwnedEmotionState state)
        {
            state = default;
            if (!TryGetActivePacketOwnedEmotionName(currentTime, out _))
            {
                return false;
            }

            state = new PacketOwnedEmotionState(
                _packetOwnedEmotionId,
                _packetOwnedEmotionName,
                _packetOwnedEmotionDurationMs,
                _packetOwnedEmotionAppliedAt,
                _packetOwnedEmotionEndTime,
                _packetOwnedEmotionByItemOption);
            return true;
        }

        internal bool TryGetLastPacketOwnedEmotionState(out PacketOwnedEmotionState state)
        {
            if (_lastPacketOwnedEmotionState.HasValue)
            {
                state = _lastPacketOwnedEmotionState.Value;
                return true;
            }

            state = default;
            return false;
        }

        public bool TryApplyPacketOwnedRandomEmotion(int areaBuffItemId, int currentTime, out string message)
        {
            message = null;
            if (!PacketOwnedAvatarEmotionResolver.TryResolveRandomEmotion(
                    areaBuffItemId,
                    _packetOwnedEmotionRandom.Next(),
                    out PacketOwnedAvatarEmotionSelection selection,
                    out string error))
            {
                message = error ?? $"Area-buff item {areaBuffItemId} did not resolve a packet-owned random emotion.";
                return false;
            }

            if (!TryApplyPacketOwnedEmotion(selection.EmotionId, durationMs: 0, byItemOption: false, currentTime, out string applyMessage))
            {
                message = applyMessage;
                return false;
            }

            message = $"Resolved area-buff item {areaBuffItemId} to packet-owned emotion '{selection.EmotionName}' ({selection.EmotionId}) with roll {selection.RandomRoll}/{selection.TotalWeight}. {applyMessage}";
            return true;
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
            PlayEffectiveWeaponSfx();

            // Trigger hitbox callback on attack frame
            var hitbox = GetAttackHitbox();
            OnAttackHitbox?.Invoke(this, hitbox);
        }

        private CharacterAction GetAttackAction()
        {
            if (_currentAttackType != AttackType.None
                && Build?.GetEffectiveAttackActionWeapon() is WeaponPart weapon)
            {
                return CharacterPart.ParseActionString(weapon.ResolveClientBasicAttackActionName(_currentAttackType));
            }

            return _currentAttackType switch
            {
                AttackType.Stab => CharacterAction.StabO1,
                AttackType.Swing => CharacterAction.SwingO1,
                AttackType.Shoot => CharacterAction.Shoot1,
                AttackType.ProneStab => CharacterAction.ProneStab,
                _ => CharacterAction.SwingO1
            };
        }

        private CharacterAction ResolveClientWalkAction()
        {
            return Build?.GetWeapon()?.ResolveClientWalkAction() ?? CharacterAction.Walk1;
        }

        private CharacterAction ResolveClientStandAction()
        {
            return Build?.GetWeapon()?.ResolveClientStandAction() ?? CharacterAction.Stand1;
        }

        private int GetAttackCooldown()
        {
            if (Build == null)
            {
                return 500;
            }

            // Attack speed: 2 (fast) to 9 (slow)
            int speed = Build.GetEffectiveWeaponAttackSpeed();
            return MIN_ATTACK_DELAY + (speed - 2) * 50;
        }

        private Rectangle GetAttackHitbox()
        {
            if (Assembler == null)
                return new Rectangle((int)X - 50, (int)Y - 50, 100, 50);
            SyncAssemblerActionLayerContext();
            return Assembler.GetAttackHitbox(CurrentAction, _attackFrame, FacingRight);
        }

        /// <summary>
        /// Trigger a specific skill animation (called by SkillManager)
        /// </summary>
        public void TriggerSkillAnimation(string actionName, int skillId = 0, int currentTime = int.MinValue)
        {
            // Map action name to CharacterAction
            ClearPortableChair(standUp: false);
            State = PlayerState.Attacking;
            _sustainedSkillAnimation = false;

            actionName = ResolveActiveSkillSpecificActionName(actionName);
            actionName = ResolveAvatarActionLayerCoordinatorActionName(actionName) ?? actionName;

            if (string.IsNullOrEmpty(actionName))
                actionName = "attack1";

            _forcedActionName = actionName;
            CurrentAction = GetCharacterActionForActionName(actionName);
            CurrentActionName = actionName;
            CurrentSkillAnimationSkillId = skillId;
            CurrentSkillAnimationStartTime = currentTime;
            _activeMeleeAfterImage = null;

            _attackFrame = 0;
            _animationStartTime = Environment.TickCount; // Set animation start time for completion check
            SyncAssemblerActionLayerContext();

            System.Diagnostics.Debug.WriteLine($"[TriggerSkillAnimation] actionName={actionName}, CurrentAction={CurrentActionName}, State={State}");
        }

        public void BeginSustainedSkillAnimation(string actionName, int skillId = 0, int currentTime = int.MinValue)
        {
            if (string.IsNullOrEmpty(actionName))
                actionName = "attack1";

            ClearPortableChair(standUp: false);
            actionName = ResolveAvatarActionLayerCoordinatorActionName(actionName) ?? actionName;

            bool isSameAction = _sustainedSkillAnimation
                && State == PlayerState.Attacking
                && string.Equals(_forcedActionName, actionName, StringComparison.OrdinalIgnoreCase);

            State = PlayerState.Attacking;
            _sustainedSkillAnimation = true;
            _forcedActionName = actionName;
            CurrentAction = GetCharacterActionForActionName(actionName);
            CurrentActionName = actionName;
            CurrentSkillAnimationSkillId = skillId;
            CurrentSkillAnimationStartTime = currentTime;
            _activeMeleeAfterImage = null;

            if (!isSameAction)
            {
                _attackFrame = 0;
                _animationStartTime = Environment.TickCount;
            }

            SyncAssemblerActionLayerContext();
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
                ActivationStartTime = currentTime + ClientMeleeAfterimageRangeResolver.ResolveActivationDelayMs(
                    afterImageAction,
                    Assembler?.GetAnimation(actionName)),
                FacingRight = FacingRight,
                ActionDuration = GetMeleeAfterImageActionDuration(actionName)
            };

            PlayEffectiveWeaponSfx();
        }

        public void PlayEffectiveWeaponSfx()
        {
            string sfx = Build?.GetEffectiveWeaponSfx();
            if (!string.IsNullOrWhiteSpace(sfx))
            {
                _onWeaponSfxSound?.Invoke(sfx);
            }
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
            int lastFrameIndex = _activeMeleeAfterImage.LastFrameIndex;
            int lastFrameElapsedMs = _activeMeleeAfterImage.LastFrameElapsedMs;
            IReadOnlyList<AfterimageRenderableLayer> lastResolvedLayers = _activeMeleeAfterImage.LastResolvedLayers;
            MeleeAfterimagePlaybackResolver.CaptureFadeSnapshotOrClearCache(
                Assembler,
                _activeMeleeAfterImage.ActionName,
                _activeMeleeAfterImage.AfterImageAction,
                animationTime,
                ref lastFrameIndex,
                ref lastFrameElapsedMs,
                ref lastResolvedLayers);
            _activeMeleeAfterImage.LastFrameIndex = lastFrameIndex;
            _activeMeleeAfterImage.LastFrameElapsedMs = lastFrameElapsedMs;
            _activeMeleeAfterImage.LastResolvedLayers = lastResolvedLayers;

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

        public bool CanApplyExternalAvatarTransform(int sourceId, string actionName, int morphTemplateId = 0)
        {
            return TryCreateExternalAvatarTransform(sourceId, actionName, morphTemplateId, out _);
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
                SupportedRawActionNames = skill.ShadowPartnerSupportedRawActionNames,
                HorizontalOffsetPx = skill.ShadowPartnerHorizontalOffsetPx,
                CurrentClientOffsetPx = ResolveShadowPartnerClientOffset(CurrentActionName, State, FacingRight),
                ClientOffsetStartPx = ResolveShadowPartnerClientOffset(CurrentActionName, State, FacingRight),
                ClientOffsetTargetPx = ResolveShadowPartnerClientOffset(CurrentActionName, State, FacingRight),
                ClientOffsetTransitionStartTime = currentTime,
                CurrentActionName = useSpawnAction ? spawnActionName : resolvedActionName,
                CurrentPlaybackAnimation = useSpawnAction
                    ? ResolveShadowPartnerPlaybackAnimation(skill.ShadowPartnerActionAnimations, spawnActionName, null)
                    : ResolveShadowPartnerPlaybackAnimation(skill.ShadowPartnerActionAnimations, resolvedActionName, CurrentActionName),
                CurrentActionStartTime = currentTime,
                CurrentFacingRight = FacingRight,
                ObservedPlayerActionName = CurrentActionName,
                QueuedActionName = useSpawnAction ? resolvedActionName : null,
                QueuedPlaybackAnimation = useSpawnAction
                    ? ResolveShadowPartnerPlaybackAnimation(skill.ShadowPartnerActionAnimations, resolvedActionName, CurrentActionName)
                    : null,
                QueuedFacingRight = FacingRight,
                QueuedForceReplay = useSpawnAction && State == PlayerState.Attacking && GetShadowPartnerObservedActionTriggerTime() != int.MinValue,
                ObservedPlayerFacingRight = FacingRight,
                ObservedPlayerFloatingState = State is PlayerState.Swimming or PlayerState.Flying,
                ObservedPlayerActionTriggerTime = GetShadowPartnerObservedActionTriggerTime()
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

        public bool ApplyMirrorImage(int skillId, int currentTime)
        {
            if (skillId != SkillData.MirrorImageSkillId)
            {
                return false;
            }

            _activeMirrorImage = new MirrorImageState
            {
                SkillId = skillId,
                StartTime = currentTime,
                ObservedPlayerActionName = CurrentActionName,
                ObservedPlayerFacingRight = FacingRight,
                ObservedPlayerState = State,
                CurrentOffsetPx = Point.Zero,
                Visible = false
            };

            UpdateMirrorImageRenderState(currentTime);
            return _activeMirrorImage.Visible;
        }

        public void ClearMirrorImage(int skillId)
        {
            if (_activeMirrorImage != null && _activeMirrorImage.SkillId == skillId)
            {
                DisposeMirrorImagePreparedSourceLayers(_activeMirrorImage.PreparedSourceLayers);
                _activeMirrorImage = null;
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

        public bool ApplyTransientSkillAvatarEffect(
            int skillId,
            SkillAnimation animation,
            SkillAnimation secondaryAnimation,
            int currentTime,
            SkillAnimation finishAnimation = null,
            SkillAnimation finishSecondaryAnimation = null,
            bool isClientMovementOwner = false)
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
                FinishAnimation = finishAnimation,
                FinishSecondaryAnimation = finishSecondaryAnimation,
                AnimationStartTime = currentTime,
                Plane = ResolveTransientSkillAvatarEffectPlane(animation, isClientMovementOwner),
                SecondaryPlane = ResolveTransientSkillAvatarEffectPlane(secondaryAnimation, isClientMovementOwner),
                FinishPlane = ResolveTransientSkillAvatarEffectPlane(finishAnimation, isClientMovementOwner),
                FinishSecondaryPlane = ResolveTransientSkillAvatarEffectPlane(finishSecondaryAnimation, isClientMovementOwner)
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

        internal int GetActiveAvatarTransformSkillId()
        {
            return GetActiveAvatarTransform()?.SkillId ?? 0;
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
                SkillAnimation primaryAnimation = transientEffect.IsFinishing
                    ? transientEffect.FinishAnimation
                    : transientEffect.Animation;
                SkillAnimation secondaryAnimation = transientEffect.IsFinishing
                    ? transientEffect.FinishSecondaryAnimation
                    : transientEffect.SecondaryAnimation;
                bool primaryComplete = primaryAnimation == null || primaryAnimation.IsComplete(elapsedTime);
                bool secondaryComplete = secondaryAnimation == null || secondaryAnimation.IsComplete(elapsedTime);
                if (primaryComplete && secondaryComplete)
                {
                    if (!transientEffect.IsFinishing && transientEffect.HasFinishAnimation)
                    {
                        transientEffect.IsFinishing = true;
                        transientEffect.AnimationStartTime = currentTime;
                        continue;
                    }

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

        private bool ShouldHideRotateSensitiveAvatarEffect()
        {
            return ClientOwnedAvatarEffectParity.ShouldHideDuringPlayerAction(
                TryGetCurrentClientRawActionCode(out int rawActionCode) ? rawActionCode : null,
                _forcedActionName,
                CurrentActionName,
                CharacterPart.GetActionString(CurrentAction));
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
                HideOnLadderOrRope = skill.HideAvatarEffectOnLadderOrRope,
                HideOnRotateAction = skill.HideAvatarEffectOnRotateAction
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

            PersistentSkillAvatarEffectRenderSelection selection =
                ResolvePersistentSkillAvatarEffectRenderSelectionForParity(
                    effectState.IsFinishing,
                    effectState.Mode == SkillAvatarEffectMode.LadderOrRope,
                    effectState.HideOnLadderOrRope,
                    effectState.LadderOverlayAnimation,
                    effectState.GroundOverlayAnimation,
                    effectState.GroundOverlaySecondaryAnimation,
                    effectState.GroundUnderFaceAnimation,
                    effectState.GroundUnderFaceSecondaryAnimation,
                    effectState.LadderOverlayFinishAnimation,
                    effectState.GroundOverlayFinishAnimation,
                    effectState.GroundUnderFaceFinishAnimation);

            if (selection.OverlayAnimation != null)
            {
                yield return selection.OverlayAnimation;
            }

            if (selection.OverlaySecondaryAnimation != null)
            {
                yield return selection.OverlaySecondaryAnimation;
            }

            if (selection.UnderFaceAnimation != null)
            {
                yield return selection.UnderFaceAnimation;
            }

            if (selection.UnderFaceSecondaryAnimation != null)
            {
                yield return selection.UnderFaceSecondaryAnimation;
            }
        }

        internal static PersistentSkillAvatarEffectRenderSelection ResolvePersistentSkillAvatarEffectRenderSelectionForParity(
            bool isFinishing,
            bool onLadderOrRope,
            bool hideOnLadderOrRope,
            SkillAnimation ladderOverlayAnimation,
            SkillAnimation groundOverlayAnimation,
            SkillAnimation groundOverlaySecondaryAnimation,
            SkillAnimation groundUnderFaceAnimation,
            SkillAnimation groundUnderFaceSecondaryAnimation,
            SkillAnimation ladderOverlayFinishAnimation,
            SkillAnimation groundOverlayFinishAnimation,
            SkillAnimation groundUnderFaceFinishAnimation)
        {
            if (onLadderOrRope)
            {
                if (hideOnLadderOrRope)
                {
                    return default;
                }

                if (isFinishing)
                {
                    if (ladderOverlayFinishAnimation != null)
                    {
                        return new PersistentSkillAvatarEffectRenderSelection(
                            ladderOverlayFinishAnimation,
                            null,
                            null,
                            null,
                            OverlayUsesBehindCharacterPlane: true);
                    }

                    return new PersistentSkillAvatarEffectRenderSelection(
                        groundOverlayFinishAnimation,
                        null,
                        groundUnderFaceFinishAnimation,
                        null,
                        OverlayUsesBehindCharacterPlane: false);
                }

                if (ladderOverlayAnimation != null)
                {
                    return new PersistentSkillAvatarEffectRenderSelection(
                        ladderOverlayAnimation,
                        null,
                        null,
                        null,
                        OverlayUsesBehindCharacterPlane: true);
                }

                return new PersistentSkillAvatarEffectRenderSelection(
                    groundOverlayAnimation,
                    groundOverlaySecondaryAnimation,
                    groundUnderFaceAnimation,
                    groundUnderFaceSecondaryAnimation,
                    OverlayUsesBehindCharacterPlane: false);
            }

            if (isFinishing)
            {
                return new PersistentSkillAvatarEffectRenderSelection(
                    groundOverlayFinishAnimation,
                    null,
                    groundUnderFaceFinishAnimation,
                    null,
                    OverlayUsesBehindCharacterPlane: false);
            }

            return new PersistentSkillAvatarEffectRenderSelection(
                groundOverlayAnimation,
                groundOverlaySecondaryAnimation,
                groundUnderFaceAnimation,
                groundUnderFaceSecondaryAnimation,
                OverlayUsesBehindCharacterPlane: false);
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

            int currentTime = Environment.TickCount;
            EnterHitState(currentTime, HIT_STUN_DURATION);

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

            EnterHitState(Environment.TickCount, HIT_STUN_DURATION);

            // Use Impact for immediate knockback
            Physics.Impact(knockbackX, knockbackY);
        }

        public void ApplyPacketDamageReaction(
            int damage,
            int hitDurationMs,
            float knockbackX = 0,
            float knockbackY = 0,
            bool useQueuedImpact = false)
        {
            if (!IsAlive || GodMode)
                return;

            if (damage > 0)
            {
                HP -= damage;
                OnDamaged?.Invoke(this, damage);
                if (HP <= 0)
                {
                    Die();
                    return;
                }
            }

            EnterHitState(Environment.TickCount, hitDurationMs);
            if (knockbackX != 0 || knockbackY != 0)
            {
                if (useQueuedImpact)
                {
                    Physics.SetImpactNext(knockbackX, knockbackY);
                }
                else
                {
                    Physics.Impact(knockbackX, knockbackY);
                }
            }
        }

        private void EnterHitState(int currentTime, int hitDurationMs)
        {
            ClearPortableChair(standUp: false);
            State = PlayerState.Hit;
            _hitStateStartTime = currentTime;
            _hitStateDurationMs = Math.Max(1, hitDurationMs);
            _hitExpressionEndTime = currentTime + Math.Max(FACE_HIT_EXPRESSION_DURATION_MS, _hitStateDurationMs);
            _hitExpressionSuppressedByPacketOwnedItemOption = _packetOwnedEmotionByItemOption;
            Physics.CurrentAction = MoveAction.Hit;
            CacheLadderStateForRegrab();
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
            _hitExpressionSuppressedByPacketOwnedItemOption = false;
            ResetPacketOwnedEmotionState(clearVisualEffect: true, clearDecodedItemOption: true);
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
            _hitExpressionSuppressedByPacketOwnedItemOption = false;
            ResetPacketOwnedEmotionState(clearVisualEffect: true, clearDecodedItemOption: true);
            CurrentFaceExpressionName = "default";
            ScheduleNextBlink(Environment.TickCount);
            Physics.Reset();
            Physics.SetPosition(x, y);
            ResetLandingTracking();
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
            ResetLandingTracking();
        }

        public void ResetLandingTracking()
        {
            _landingTrackingActive = false;
            _landingTrackingStartY = 0f;
        }

        private void NotifyLanding(float landingY, float impactVelocityY)
        {
            if (!_landingTrackingActive)
            {
                return;
            }

            PlayerLandingInfo landingInfo = new(_landingTrackingStartY, landingY, impactVelocityY);
            ResetLandingTracking();
            _onLanded?.Invoke(this, landingInfo);
        }

        public void PrepareForForcedHorizontalControl()
        {
            if (State == PlayerState.Dead)
            {
                return;
            }

            if (Physics.IsOnLadderOrRope)
            {
                ClearPortableChair(standUp: false);
                ClearForcedActionName();
                Physics.ReleaseLadder(yOverride: Physics.Y);
                State = PlayerState.Falling;
                return;
            }

            if (State == PlayerState.Sitting || State == PlayerState.Prone)
            {
                ForceStand();
            }
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

            SyncAssemblerActionLayerContext();

            // Get current frame
            int animTime = GetRenderAnimationTime(currentTime);

            var frame = Assembler.GetFrameAtTime(CurrentActionName, animTime);
            int currentFrameIndex = Assembler?.GetFrameIndexAtTime(CurrentActionName, animTime) ?? -1;

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

                DrawPortableChairCoupleSharedLayers(
                    spriteBatch,
                    skeletonRenderer,
                    screenX,
                    screenY,
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
                    currentFrameIndex,
                    screenX,
                    screenY,
                    tint,
                    currentTime,
                    drawUnderFaceOverlay);

                DrawPortableChairCoupleSharedLayers(
                    spriteBatch,
                    skeletonRenderer,
                    screenX,
                    screenY,
                    currentTime,
                    drawFrontLayers: true);

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
                || ShouldUsePortableChairExternalPair(_portableChairExternalPairRequested, _portableChairHasExternalPair)
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
            DrawPortableChairPairPreviewSharedLayers(
                spriteBatch,
                skeletonRenderer,
                partnerScreenX,
                partnerScreenY,
                currentTime,
                drawFrontLayers: false);
            for (int i = 0; i < partnerFrame.Parts.Count; i++)
            {
                DrawAssembledPart(spriteBatch, skeletonRenderer, partnerFrame.Parts[i], partnerScreenX, adjustedY, _portableChairPairFacingRight, Color.White);
            }

            DrawPortableChairPairPreviewSharedLayers(
                spriteBatch,
                skeletonRenderer,
                partnerScreenX,
                partnerScreenY,
                currentTime,
                drawFrontLayers: true);
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

        internal const int PortableChairCoupleMidpointScreenYOffset = -20;

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
            if (!TryResolvePortableChairMidpointLayerState(
                    out PortableChair chair,
                    out float partnerX,
                    out float partnerY,
                    out bool midpointFacingRight))
            {
                return;
            }

            int midpointScreenX = (int)Math.Round((X + partnerX) * 0.5f) - mapShiftX + centerX;
            int midpointScreenY = (int)Math.Round((Y + partnerY) * 0.5f) - mapShiftY + centerY + PortableChairCoupleMidpointScreenYOffset;
            int animationTime = GetRenderAnimationTime(currentTime);

            for (int i = 0; i < chair.CoupleMidpointLayers.Count; i++)
            {
                PortableChairLayer layer = chair.CoupleMidpointLayers[i];
                if ((layer.RelativeZ > 0) != drawFrontLayers)
                {
                    continue;
                }

                CharacterFrame frame = GetPortableChairLayerFrameAtTime(layer, animationTime);
                DrawPortableChairLayerFrame(spriteBatch, skeletonRenderer, frame, midpointScreenX, midpointScreenY, midpointFacingRight);
            }
        }

        private void DrawPortableChairCoupleSharedLayers(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            int screenX,
            int screenY,
            int currentTime,
            bool drawFrontLayers)
        {
            if (!TryResolvePortableChairSharedLayerState(out PortableChair chair))
            {
                return;
            }

            DrawPortableChairLayers(
                spriteBatch,
                skeletonRenderer,
                chair.CoupleSharedLayers,
                screenX,
                screenY,
                FacingRight,
                GetRenderAnimationTime(currentTime),
                drawFrontLayers);
        }

        private void DrawPortableChairPairPreviewSharedLayers(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            int partnerScreenX,
            int partnerScreenY,
            int currentTime,
            bool drawFrontLayers)
        {
            if (!TryResolvePortableChairCoupleSharedLayerState(out PortableChair chair, out bool previewPairActive)
                || !previewPairActive)
            {
                return;
            }

            DrawPortableChairLayers(
                spriteBatch,
                skeletonRenderer,
                chair.CoupleSharedLayers,
                partnerScreenX,
                partnerScreenY,
                _portableChairPairFacingRight,
                GetRenderAnimationTime(currentTime),
                drawFrontLayers);
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
            int currentFrameIndex,
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

            DrawAvatarEffectPlane(spriteBatch, skeletonRenderer, avatarEffects, SkillAvatarEffectPlane.BehindCharacter, frame, screenX, screenY, tint);
            DrawMeleeAfterImage(spriteBatch, skeletonRenderer, screenX, screenY, tint, currentTime);

            int[] avatarLayerInsertionIndices = GetAvatarRenderLayerInsertionIndices(frame.Parts);
            int mirrorOverlayInsertionIndex = ResolveMirrorImageOverlayInsertionIndex(
                avatarLayerInsertionIndices,
                frame.Parts.Count);
            bool underFaceDrawn = avatarEffects.Count == 0;
            bool shadowPartnerDrawn = false;
            bool mirrorImageDrawn = false;

            for (int i = 0; i < frame.Parts.Count; i++)
            {
                DrawMirrorImageAtInsertionIndex(
                    spriteBatch,
                    skeletonRenderer,
                    frame,
                    currentFrameIndex,
                    screenX,
                    screenY,
                    currentTime,
                    i,
                    mirrorOverlayInsertionIndex,
                    ref mirrorImageDrawn,
                    ref underFaceDrawn,
                    ref shadowPartnerDrawn,
                    avatarEffects,
                    tint,
                    drawUnderFaceOverlay);

                DrawAssembledPart(spriteBatch, skeletonRenderer, frame.Parts[i], screenX, adjustedY, FacingRight, tint);
            }

            DrawMirrorImageAtInsertionIndex(
                spriteBatch,
                skeletonRenderer,
                frame,
                currentFrameIndex,
                screenX,
                screenY,
                currentTime,
                frame.Parts.Count,
                mirrorOverlayInsertionIndex,
                ref mirrorImageDrawn,
                ref underFaceDrawn,
                ref shadowPartnerDrawn,
                avatarEffects,
                tint,
                drawUnderFaceOverlay);

            DrawAvatarEffectPlane(spriteBatch, skeletonRenderer, avatarEffects, SkillAvatarEffectPlane.OverCharacter, frame, screenX, screenY, tint);
        }

        private void DrawMirrorImageAtInsertionIndex(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            AssembledFrame frame,
            int currentFrameIndex,
            int screenX,
            int screenY,
            int currentTime,
            int insertionIndex,
            int mirrorOverlayInsertionIndex,
            ref bool mirrorImageDrawn,
            ref bool underFaceDrawn,
            ref bool shadowPartnerDrawn,
            List<AvatarEffectRenderable> avatarEffects,
            Color tint,
            Action drawUnderFaceOverlay)
        {
            if (mirrorImageDrawn || insertionIndex != mirrorOverlayInsertionIndex)
            {
                return;
            }

            if (!underFaceDrawn)
            {
                drawUnderFaceOverlay?.Invoke();
                DrawAvatarEffectPlane(spriteBatch, skeletonRenderer, avatarEffects, SkillAvatarEffectPlane.UnderFace, frame, screenX, screenY, tint);
                underFaceDrawn = true;
            }

            if (!shadowPartnerDrawn)
            {
                DrawShadowPartner(spriteBatch, skeletonRenderer, screenX, screenY, currentTime);
                shadowPartnerDrawn = true;
            }

            DrawMirrorImage(spriteBatch, skeletonRenderer, frame, currentFrameIndex, screenX, screenY, currentTime, AvatarRenderLayer.UnderFace);
            mirrorImageDrawn = true;
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

            if (currentTime < _activeMeleeAfterImage.ActivationStartTime)
            {
                bool sameAction = State == PlayerState.Attacking
                    && string.Equals(CurrentActionName, _activeMeleeAfterImage.ActionName, StringComparison.OrdinalIgnoreCase);
                if (!sameAction
                    || (_activeMeleeAfterImage.ActionDuration > 0
                        && currentTime - _activeMeleeAfterImage.AnimationStartTime >= _activeMeleeAfterImage.ActionDuration))
                {
                    _activeMeleeAfterImage = null;
                }

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
            IReadOnlyList<AfterimageRenderableLayer> layers = _activeMeleeAfterImage.LastResolvedLayers;
            if (activeAction)
            {
                int animationTime = Math.Max(0, currentTime - _activeMeleeAfterImage.AnimationStartTime);
                int lastFrameIndex = _activeMeleeAfterImage.LastFrameIndex;
                int lastFrameElapsedMs = _activeMeleeAfterImage.LastFrameElapsedMs;
                IReadOnlyList<AfterimageRenderableLayer> lastResolvedLayers = _activeMeleeAfterImage.LastResolvedLayers;
                MeleeAfterimagePlaybackResolver.RefreshSnapshotCache(
                    Assembler,
                    _activeMeleeAfterImage.ActionName,
                    _activeMeleeAfterImage.AfterImageAction,
                    animationTime,
                    ref lastFrameIndex,
                    ref lastFrameElapsedMs,
                    ref lastResolvedLayers);
                _activeMeleeAfterImage.LastFrameIndex = lastFrameIndex;
                _activeMeleeAfterImage.LastFrameElapsedMs = lastFrameElapsedMs;
                _activeMeleeAfterImage.LastResolvedLayers = lastResolvedLayers;
                frameIndex = _activeMeleeAfterImage.LastFrameIndex;
                layers = _activeMeleeAfterImage.LastResolvedLayers;
            }
            else if (_activeMeleeAfterImage.FadeStartTime >= 0)
            {
                int fadeElapsed = Math.Max(0, currentTime - _activeMeleeAfterImage.FadeStartTime);
                layers = MeleeAfterimagePlaybackResolver.ResolveFadingRenderableLayers(
                    _activeMeleeAfterImage.AfterImageAction,
                    frameIndex,
                    _activeMeleeAfterImage.LastFrameElapsedMs,
                    fadeElapsed);
                if (layers.Count == 0)
                {
                    _activeMeleeAfterImage = null;
                    return;
                }
            }

            if (layers == null || layers.Count == 0)
            {
                return;
            }

            foreach (AfterimageRenderableLayer layer in layers)
            {
                SkillFrame frame = layer.Frame;
                if (frame?.Texture == null)
                {
                    continue;
                }

                Color frameTint = tint * MathHelper.Clamp(layer.Alpha, 0f, 1f);
                bool shouldFlip = _activeMeleeAfterImage.FacingRight ^ frame.Flip;
                float zoom = MathHelper.Clamp(layer.Zoom, 0.01f, 10f);
                int drawWidth = Math.Max(1, (int)Math.Round(frame.Texture.Width * zoom));
                int drawHeight = Math.Max(1, (int)Math.Round(frame.Texture.Height * zoom));
                int drawX = shouldFlip
                    ? screenX - (int)Math.Round((frame.Texture.Width - frame.Origin.X) * zoom)
                    : screenX - (int)Math.Round(frame.Origin.X * zoom);
                int drawY = screenY - (int)Math.Round(frame.Origin.Y * zoom);
                spriteBatch.Draw(
                    frame.Texture.Texture,
                    new Rectangle(drawX, drawY, drawWidth, drawHeight),
                    null,
                    frameTint,
                    0f,
                    Vector2.Zero,
                    shouldFlip ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                    0f);
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

            int? rawActionCode = TryGetCurrentClientRawActionCode(out int resolvedRawActionCode)
                ? resolvedRawActionCode
                : null;
            if (!ShadowPartnerClientActionResolver.ShouldRenderClientShadowPartner(_activeShadowPartner.SkillId, rawActionCode))
            {
                return;
            }

            if (!TryGetShadowPartnerAnimation(currentTime, out SkillAnimation animation, out SkillFrame frame, out int frameElapsedMs, out bool facingRight))
            {
                return;
            }

            if (frame?.Texture == null)
            {
                return;
            }

            bool flip = facingRight ^ frame.Flip;
            Point clientOffset = _activeShadowPartner?.CurrentClientOffsetPx ?? Point.Zero;
            int horizontalOffsetPx = ResolveShadowPartnerHorizontalOffsetPx(animation);
            int drawX = screenX + clientOffset.X + (facingRight ? -horizontalOffsetPx : horizontalOffsetPx);
            drawX = flip
                ? drawX - (frame.Texture.Width - frame.Origin.X)
                : drawX - frame.Origin.X;

            int drawY = screenY + clientOffset.Y - frame.Origin.Y;
            Color frameTint = ShadowPartnerTint * ResolveShadowPartnerFrameAlpha(frame, frameElapsedMs);
            frame.Texture.DrawBackground(spriteBatch, skeletonRenderer, null, drawX, drawY, frameTint, flip, null);
        }

        private void DrawMirrorImage(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            AssembledFrame frame,
            int currentFrameIndex,
            int screenX,
            int screenY,
            int currentTime,
            AvatarRenderLayer? overlayTargetLayer = null)
        {
            if (frame == null || _activeMirrorImage == null)
            {
                return;
            }

            UpdateMirrorImageRenderState(currentTime);
            if (_activeMirrorImage == null || !_activeMirrorImage.Visible)
            {
                return;
            }

            float alpha = ResolveMirrorImageAlpha(currentTime);
            if (alpha <= 0f)
            {
                return;
            }

            DrawMirrorImageSourceLayers(
                spriteBatch,
                skeletonRenderer,
                frame,
                currentFrameIndex,
                screenX,
                screenY,
                currentTime,
                overlayTargetLayer);
        }

        private void DrawMirrorImageSourceLayers(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            AssembledFrame frame,
            int currentFrameIndex,
            int screenX,
            int screenY,
            int currentTime,
            AvatarRenderLayer? overlayTargetLayer)
        {
            if (_activeMirrorImage?.PreparedSourceLayers == null)
            {
                return;
            }

            MirrorImagePreparedSourceLayer[] layeredParts = _activeMirrorImage.PreparedSourceLayers;
            if (layeredParts.Length == 0)
            {
                return;
            }

            for (int layerIndex = 0; layerIndex < layeredParts.Length; layerIndex++)
            {
                MirrorImagePreparedSourceLayer layer = layeredParts[layerIndex];
                if (layer == null)
                {
                    continue;
                }

                if (overlayTargetLayer.HasValue && layer.OverlayTargetLayer != overlayTargetLayer.Value)
                {
                    continue;
                }

                MirrorImageRenderableSourceLayer? renderableLayer = TryResolveMirrorImageRenderableSourceLayer(
                    frame,
                    currentFrameIndex,
                    layer);
                if (!renderableLayer.HasValue)
                {
                    continue;
                }

                int transitionStartTime = renderableLayer.Value.TransitionStartTime;
                float alpha = ResolveMirrorImageAlpha(currentTime, transitionStartTime);
                if (alpha <= 0f)
                {
                    continue;
                }

                Point layerOffset = ResolveMirrorImageCurrentOffset(currentTime, transitionStartTime);
                int adjustedY = screenY + layerOffset.Y - _activeMirrorImage.PreparedFeetOffset;
                int adjustedX = screenX + layerOffset.X;
                Color tint = MirrorImageTint * alpha;
                if (renderableLayer.Value.PreparedSnapshotTexture == null)
                {
                    continue;
                }

                DrawMirrorImagePreparedSnapshot(
                    spriteBatch,
                    renderableLayer.Value.PreparedSnapshotTexture,
                    renderableLayer.Value.PreparedSnapshotSourceBounds,
                    renderableLayer.Value.PositionBounds,
                    renderableLayer.Value.Origin,
                    adjustedX,
                    adjustedY,
                    renderableLayer.Value.FacingRight,
                    tint);
            }
        }

        private static void DrawMirrorImagePreparedSnapshot(
            SpriteBatch spriteBatch,
            Texture2D snapshotTexture,
            Rectangle snapshotSourceBounds,
            Rectangle snapshotBounds,
            Point snapshotOrigin,
            int screenX,
            int adjustedY,
            bool flip,
            Color mirrorTint)
        {
            if (spriteBatch == null
                || snapshotTexture == null
                || snapshotBounds.Width <= 0
                || snapshotBounds.Height <= 0)
            {
                return;
            }

            Point drawPosition = ResolveMirrorImagePreparedSnapshotDrawPosition(screenX, adjustedY, snapshotBounds, snapshotOrigin, flip);
            spriteBatch.Draw(
                snapshotTexture,
                new Vector2(drawPosition.X, drawPosition.Y),
                snapshotSourceBounds.IsEmpty ? null : snapshotSourceBounds,
                mirrorTint,
                0f,
                Vector2.Zero,
                1f,
                flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                0f);
        }

        private void PrepareMirrorImageSourceLayers(AssembledFrame frame, int currentFrameIndex, int currentTime)
        {
            if (_activeMirrorImage == null)
            {
                return;
            }

            if (frame?.AvatarRenderLayers == null || frame.AvatarRenderLayers.Length == 0)
            {
                ResetMirrorImagePreparedSourceLayers();
                return;
            }

            string actionName = CurrentActionName ?? string.Empty;
            int sourceLayerCurrentTime = GetRenderAnimationTime(currentTime);
            bool preservesPreparedFeetOffset = CanPreserveMirrorImagePreparedFeetOffset(
                _activeMirrorImage.PreparedSourceLayers,
                FacingRight);
            _activeMirrorImage.PreparedActionName = actionName;
            _activeMirrorImage.PreparedFrameIndex = currentFrameIndex;
            _activeMirrorImage.PreparedFeetOffset = ResolveMirrorImagePreparedFeetOffset(
                _activeMirrorImage.PreparedFeetOffset,
                frame.FeetOffset,
                preservesPreparedFeetOffset);
            _activeMirrorImage.PreparedSourceLayers = BuildPreparedMirrorImageSourceLayers(
                frame.AvatarRenderLayers,
                _activeMirrorImage.PreparedSourceLayers,
                FacingRight,
                sourceLayerCurrentTime,
                currentTime);
        }

        private void ResetMirrorImagePreparedSourceLayers()
        {
            if (_activeMirrorImage == null)
            {
                return;
            }

            _activeMirrorImage.PreparedActionName = null;
            _activeMirrorImage.PreparedFrameIndex = -1;
            _activeMirrorImage.PreparedFeetOffset = 0;
            DisposeMirrorImagePreparedSourceLayers(_activeMirrorImage.PreparedSourceLayers);
            _activeMirrorImage.PreparedSourceLayers = CreateEmptyMirrorImagePreparedSourceLayers();
        }

        private static MirrorImagePreparedSourceLayer[] CreateEmptyMirrorImagePreparedSourceLayers()
        {
            var layers = new MirrorImagePreparedSourceLayer[5];
            for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++)
            {
                AvatarRenderLayer renderLayer = (AvatarRenderLayer)layerIndex;
                layers[layerIndex] = new MirrorImagePreparedSourceLayer
                {
                    RenderLayer = renderLayer,
                    SourceSignature = 0,
                    Bounds = Rectangle.Empty,
                    TextureSourceBounds = Rectangle.Empty,
                    Origin = Point.Zero,
                    PreparedCurrentTime = int.MinValue,
                    PreparedSourceLayerCurrentTime = int.MinValue,
                    PreparedFacingRight = false,
                    OverlayTargetLayer = ResolveMirrorImageOverlayTargetLayer(renderLayer),
                    ComposedTexture = null,
                    Parts = Array.Empty<AssembledPart>()
                };
            }

            return layers;
        }

        private MirrorImagePreparedSourceLayer[] BuildPreparedMirrorImageSourceLayers(
            IReadOnlyList<AssembledPart>[] sourceLayers,
            MirrorImagePreparedSourceLayer[] existingLayers,
            bool facingRight,
            int sourceLayerCurrentTime,
            int currentTime)
        {
            if (sourceLayers == null || sourceLayers.Length == 0)
            {
                DisposeMirrorImagePreparedSourceLayers(existingLayers);
                return CreateEmptyMirrorImagePreparedSourceLayers();
            }

            const int MirrorLayerCount = 5;
            var preparedLayers = new MirrorImagePreparedSourceLayer[MirrorLayerCount];
            int populatedLayerCount = Math.Min(sourceLayers.Length, MirrorLayerCount);
            for (int layerIndex = 0; layerIndex < populatedLayerCount; layerIndex++)
            {
                IReadOnlyList<AssembledPart> sourceParts = sourceLayers[layerIndex];
                int sourceSignature = ComputeMirrorImageSourceLayerSignature(sourceParts);
                MirrorImagePreparedSourceLayer existingLayer = existingLayers != null && layerIndex < existingLayers.Length
                    ? existingLayers[layerIndex]
                    : null;
                preparedLayers[layerIndex] = CreatePreparedMirrorImageSourceLayer(
                    existingLayer,
                    (AvatarRenderLayer)layerIndex,
                    sourceParts,
                    sourceSignature,
                    facingRight,
                    CanPreserveMirrorImagePreparedSourceLayerObject(
                        existingLayer?.PreparedFacingRight ?? false,
                        facingRight,
                        existingLayer?.Parts?.Count ?? 0),
                    sourceLayerCurrentTime,
                    currentTime);
            }

            for (int layerIndex = sourceLayers.Length; layerIndex < preparedLayers.Length; layerIndex++)
            {
                MirrorImagePreparedSourceLayer existingLayer = existingLayers != null && layerIndex < existingLayers.Length
                    ? existingLayers[layerIndex]
                    : null;
                preparedLayers[layerIndex] = ResetPreparedMirrorImageSourceLayer(existingLayer, (AvatarRenderLayer)layerIndex);
            }

            if (existingLayers != null)
            {
                for (int layerIndex = MirrorLayerCount; layerIndex < existingLayers.Length; layerIndex++)
                {
                    DisposeMirrorImagePreparedSourceLayerTexture(existingLayers[layerIndex]);
                }
            }

            return preparedLayers;
        }

        private MirrorImageRenderableSourceLayer? TryResolveMirrorImageRenderableSourceLayer(
            AssembledFrame frame,
            int currentFrameIndex,
            MirrorImagePreparedSourceLayer preparedLayer)
        {
            if (_activeMirrorImage == null || preparedLayer == null)
            {
                return null;
            }

            if (TryResolveLiveMirrorImageSourceLayer(
                    frame,
                    currentFrameIndex,
                    preparedLayer,
                    out IReadOnlyList<AssembledPart> liveSourceParts))
            {
                return new MirrorImageRenderableSourceLayer(
                    preparedLayer.ComposedTexture,
                    ResolveMirrorImagePreparedSnapshotSourceBounds(
                        preparedLayer.TextureSourceBounds,
                        preparedLayer.Bounds),
                    ResolveMirrorImageRenderablePositionBounds(
                        preparedLayer.Bounds,
                        CalculateMirrorImageSourceLayerBounds(liveSourceParts)),
                    preparedLayer.Origin,
                    FacingRight,
                    ResolveMirrorImageLayerTransitionStartTime(_activeMirrorImage.StartTime, preparedLayer.PreparedCurrentTime));
            }

            if (!CanRenderPreparedMirrorImageSourceLayer(
                    preparedLayer.ComposedTexture != null
                    && preparedLayer.Bounds.Width > 0
                    && preparedLayer.Bounds.Height > 0,
                    preparedLayer.Parts))
            {
                return null;
            }

            return new MirrorImageRenderableSourceLayer(
                preparedLayer.ComposedTexture,
                ResolveMirrorImagePreparedSnapshotSourceBounds(
                    preparedLayer.TextureSourceBounds,
                    preparedLayer.Bounds),
                ResolveMirrorImageRenderablePositionBounds(
                    preparedLayer.Bounds,
                    ResolveMirrorImageLiveRenderBounds(frame, preparedLayer.RenderLayer)),
                preparedLayer.Origin,
                ResolveMirrorImagePreparedFallbackFacing(preparedLayer.PreparedFacingRight, FacingRight),
                ResolveMirrorImageLayerTransitionStartTime(_activeMirrorImage.StartTime, preparedLayer.PreparedCurrentTime));
        }

        private bool TryResolveLiveMirrorImageSourceLayer(
            AssembledFrame frame,
            int currentFrameIndex,
            MirrorImagePreparedSourceLayer preparedLayer,
            out IReadOnlyList<AssembledPart> liveSourceParts)
        {
            liveSourceParts = null;
            if (_activeMirrorImage == null
                || frame?.AvatarRenderLayers == null
                || preparedLayer == null)
            {
                return false;
            }

            int renderLayerIndex = (int)preparedLayer.RenderLayer;
            if ((uint)renderLayerIndex >= (uint)frame.AvatarRenderLayers.Length)
            {
                return false;
            }

            liveSourceParts = frame.AvatarRenderLayers[renderLayerIndex];
            return CanUseLiveMirrorImageSourceLayer(
                _activeMirrorImage.PreparedActionName,
                CurrentActionName,
                _activeMirrorImage.PreparedFrameIndex,
                currentFrameIndex,
                preparedLayer.PreparedFacingRight,
                FacingRight,
                preparedLayer.SourceSignature,
                liveSourceParts);
        }

        private MirrorImagePreparedSourceLayer CreatePreparedMirrorImageSourceLayer(
            MirrorImagePreparedSourceLayer existingLayer,
            AvatarRenderLayer renderLayer,
            IReadOnlyList<AssembledPart> sourceParts,
            int sourceSignature,
            bool facingRight,
            bool preservesExistingLayerObject,
            int sourceLayerCurrentTime,
            int currentTime)
        {
            MirrorImagePreparedSourceLayer preparedLayer = existingLayer ?? new MirrorImagePreparedSourceLayer();
            if (sourceParts == null || sourceParts.Count == 0)
            {
                if (CanPreserveMirrorImagePreparedSourceLayerWhenSourceMissing(
                    preparedLayer.PreparedFacingRight,
                    facingRight,
                    preparedLayer.Parts?.Count ?? 0))
                {
                    preparedLayer.RenderLayer = renderLayer;
                    preparedLayer.OverlayTargetLayer = ResolveMirrorImageOverlayTargetLayer(renderLayer);
                    return preparedLayer;
                }

                return ResetPreparedMirrorImageSourceLayer(preparedLayer, renderLayer, sourceSignature);
            }

            if (CanReuseMirrorImagePreparedSourceLayer(
                preparedLayer.SourceSignature,
                preparedLayer.Parts?.Count ?? 0,
                preparedLayer.PreparedFacingRight,
                sourceSignature,
                facingRight))
            {
                int refreshedSourceLayerCurrentTime = ResolveMirrorImagePreparedSourceLayerCurrentTime(
                    preparedLayer.PreparedSourceLayerCurrentTime,
                    reusesExistingSourceCanvas: true,
                    sourceLayerCurrentTime);
                preparedLayer.RenderLayer = renderLayer;
                preparedLayer.SourceSignature = sourceSignature;
                preparedLayer.PreparedCurrentTime = ResolveMirrorImagePreparedSourceLayerUpdateTime(
                    preparedLayer.PreparedCurrentTime,
                    reusesExistingLayer: true,
                    preservesExistingLayerObject: true,
                    currentTime);
                preparedLayer.PreparedSourceLayerCurrentTime = refreshedSourceLayerCurrentTime;
                preparedLayer.PreparedFacingRight = facingRight;
                preparedLayer.OverlayTargetLayer = ResolveMirrorImageOverlayTargetLayer(renderLayer);
                return preparedLayer;
            }

            var clonedParts = new AssembledPart[sourceParts.Count];
            for (int partIndex = 0; partIndex < sourceParts.Count; partIndex++)
            {
                clonedParts[partIndex] = CloneMirrorImageSourcePart(sourceParts[partIndex]);
            }

            Rectangle bounds = CalculateMirrorImageSourceLayerBounds(clonedParts);
            bool preservesPreparedPlacement = CanPreserveMirrorImagePreparedSourceLayerPlacement(
                preservesExistingLayerObject,
                preparedLayer.Bounds,
                preparedLayer.Parts?.Count ?? 0);
            Texture2D composedTexture = CreateMirrorImageLayerTexture(
                clonedParts,
                bounds,
                preparedLayer.ComposedTexture,
                preservesExistingLayerObject);
            ReplacePreparedMirrorImageSourceLayerTexture(preparedLayer, composedTexture);
            preparedLayer.RenderLayer = renderLayer;
            preparedLayer.SourceSignature = sourceSignature;
            preparedLayer.Bounds = ResolveMirrorImagePreparedSourceLayerPlacementBounds(
                preparedLayer.Bounds,
                bounds,
                preservesPreparedPlacement);
            preparedLayer.TextureSourceBounds = ResolveMirrorImagePreparedSnapshotSourceBounds(Rectangle.Empty, bounds);
            preparedLayer.Origin = ResolveMirrorImagePreparedSourceLayerOrigin(
                preparedLayer.Origin,
                bounds,
                preservesPreparedPlacement);
            preparedLayer.PreparedCurrentTime = ResolveMirrorImagePreparedSourceLayerUpdateTime(
                preparedLayer.PreparedCurrentTime,
                reusesExistingLayer: false,
                preservesExistingLayerObject,
                currentTime);
            preparedLayer.PreparedSourceLayerCurrentTime = sourceLayerCurrentTime;
            preparedLayer.PreparedFacingRight = facingRight;
            preparedLayer.OverlayTargetLayer = ResolveMirrorImageOverlayTargetLayer(renderLayer);
            preparedLayer.Parts = clonedParts;
            return preparedLayer;
        }

        private MirrorImagePreparedSourceLayer ResetPreparedMirrorImageSourceLayer(
            MirrorImagePreparedSourceLayer existingLayer,
            AvatarRenderLayer renderLayer,
            int sourceSignature = 0)
        {
            MirrorImagePreparedSourceLayer preparedLayer = existingLayer ?? new MirrorImagePreparedSourceLayer();
            DisposeMirrorImagePreparedSourceLayerTexture(preparedLayer);
            preparedLayer.RenderLayer = renderLayer;
            preparedLayer.SourceSignature = sourceSignature;
            preparedLayer.Bounds = Rectangle.Empty;
            preparedLayer.TextureSourceBounds = Rectangle.Empty;
            preparedLayer.Origin = Point.Zero;
            preparedLayer.PreparedCurrentTime = int.MinValue;
            preparedLayer.PreparedSourceLayerCurrentTime = int.MinValue;
            preparedLayer.PreparedFacingRight = false;
            preparedLayer.OverlayTargetLayer = ResolveMirrorImageOverlayTargetLayer(renderLayer);
            preparedLayer.Parts = Array.Empty<AssembledPart>();
            return preparedLayer;
        }

        internal static AvatarRenderLayer ResolveMirrorImageOverlayTargetLayer(AvatarRenderLayer renderLayer)
        {
            return AvatarRenderLayer.UnderFace;
        }

        internal static int ResolveMirrorImageOverlayInsertionIndex(
            IReadOnlyList<int> avatarLayerInsertionIndices,
            int fallbackInsertionIndex)
        {
            if (avatarLayerInsertionIndices == null)
            {
                return fallbackInsertionIndex;
            }

            int underFaceLayerIndex = (int)AvatarRenderLayer.UnderFace;
            if ((uint)underFaceLayerIndex >= (uint)avatarLayerInsertionIndices.Count)
            {
                return fallbackInsertionIndex;
            }

            return avatarLayerInsertionIndices[underFaceLayerIndex];
        }

        internal static bool CanReuseMirrorImagePreparedSourceLayer(
            int existingSourceSignature,
            int existingPartCount,
            bool existingFacingRight,
            int incomingSourceSignature,
            bool facingRight)
        {
            return existingSourceSignature == incomingSourceSignature
                   && existingPartCount > 0
                   && existingFacingRight == facingRight;
        }

        internal static bool CanPreserveMirrorImagePreparedSourceLayerObject(
            bool existingFacingRight,
            bool currentFacingRight,
            int existingPartCount)
        {
            return existingPartCount > 0
                   && existingFacingRight == currentFacingRight;
        }

        internal static bool CanPreserveMirrorImagePreparedSourceLayerWhenSourceMissing(
            bool existingFacingRight,
            bool currentFacingRight,
            int existingPartCount)
        {
            return CanPreserveMirrorImagePreparedSourceLayerObject(
                existingFacingRight,
                currentFacingRight,
                existingPartCount);
        }

        internal static int ResolveMirrorImagePreparedSourceLayerUpdateTime(
            int existingPreparedCurrentTime,
            bool reusesExistingLayer,
            bool preservesExistingLayerObject,
            int currentTime)
        {
            if ((reusesExistingLayer || preservesExistingLayerObject) && existingPreparedCurrentTime != int.MinValue)
            {
                return existingPreparedCurrentTime;
            }

            return currentTime;
        }

        internal static int ResolveMirrorImagePreparedSourceLayerCurrentTime(
            int existingSourceLayerCurrentTime,
            bool reusesExistingSourceCanvas,
            int sourceLayerCurrentTime)
        {
            if (reusesExistingSourceCanvas && existingSourceLayerCurrentTime != int.MinValue)
            {
                return existingSourceLayerCurrentTime;
            }

            return sourceLayerCurrentTime;
        }

        internal static bool CanUseLiveMirrorImageSourceLayer(
            string preparedActionName,
            string currentActionName,
            int preparedFrameIndex,
            int currentFrameIndex,
            bool preparedFacingRight,
            bool currentFacingRight,
            int preparedSourceSignature,
            IReadOnlyList<AssembledPart> liveSourceParts)
        {
            if (string.IsNullOrWhiteSpace(currentActionName)
                || currentFrameIndex < 0
                || liveSourceParts == null
                || liveSourceParts.Count == 0)
            {
                return false;
            }

            if (!string.Equals(
                    preparedActionName ?? string.Empty,
                    currentActionName ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (preparedFrameIndex != currentFrameIndex
                || preparedFacingRight != currentFacingRight)
            {
                return false;
            }

            return preparedSourceSignature == ComputeMirrorImageSourceLayerSignature(liveSourceParts);
        }

        private static AssembledPart CloneMirrorImageSourcePart(AssembledPart part)
        {
            return part == null
                ? null
                : new AssembledPart
                {
                    Texture = part.Texture,
                    OffsetX = part.OffsetX,
                    OffsetY = part.OffsetY,
                    ZLayer = part.ZLayer,
                    ZIndex = part.ZIndex,
                    VisibilityTokens = part.VisibilityTokens,
                    VisibilityPriority = part.VisibilityPriority,
                    IsVisible = part.IsVisible,
                    SourcePart = part.SourcePart,
                    Tint = part.Tint,
                    PartType = part.PartType,
                    SourcePortableChairLayer = part.SourcePortableChairLayer,
                    RenderLayer = part.RenderLayer
                };
        }

        internal static int ComputeMirrorImageSourceLayerSignature(IReadOnlyList<AssembledPart> sourceParts)
        {
            if (sourceParts == null || sourceParts.Count == 0)
            {
                return 0;
            }

            var signature = new HashCode();
            signature.Add(sourceParts.Count);
            for (int partIndex = 0; partIndex < sourceParts.Count; partIndex++)
            {
                AssembledPart part = sourceParts[partIndex];
                if (part == null)
                {
                    signature.Add(0);
                    continue;
                }

                signature.Add(RuntimeHelpers.GetHashCode(part.Texture));
                signature.Add(part.OffsetX);
                signature.Add(part.OffsetY);
                signature.Add(part.ZIndex);
                signature.Add(part.IsVisible);
                signature.Add(part.Tint.PackedValue);
                signature.Add((int)part.PartType);
                signature.Add((int)part.RenderLayer);
                signature.Add(part.ZLayer, StringComparer.Ordinal);
                signature.Add(RuntimeHelpers.GetHashCode(part.SourcePortableChairLayer));
            }

            return signature.ToHashCode();
        }

        internal static Point ResolveMirrorImageSourceLayerOrigin(Rectangle bounds)
        {
            return bounds.IsEmpty
                ? Point.Zero
                : new Point(-bounds.X, -bounds.Y);
        }

        private static Rectangle CalculateMirrorImageSourceLayerBounds(IReadOnlyList<AssembledPart> sourceParts)
        {
            if (sourceParts == null || sourceParts.Count == 0)
            {
                return Rectangle.Empty;
            }

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            for (int partIndex = 0; partIndex < sourceParts.Count; partIndex++)
            {
                AssembledPart part = sourceParts[partIndex];
                if (part?.Texture == null || !part.IsVisible)
                {
                    continue;
                }

                minX = Math.Min(minX, part.OffsetX);
                minY = Math.Min(minY, part.OffsetY);
                maxX = Math.Max(maxX, part.OffsetX + part.Texture.Width);
                maxY = Math.Max(maxY, part.OffsetY + part.Texture.Height);
            }

            if (minX == int.MaxValue || minY == int.MaxValue)
            {
                return Rectangle.Empty;
            }

            return new Rectangle(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY));
        }

        internal static Point ResolveMirrorImagePreparedSnapshotDrawPosition(
            int screenX,
            int adjustedY,
            Rectangle snapshotBounds,
            bool flip)
        {
            return ResolveMirrorImagePreparedSnapshotDrawPosition(
                screenX,
                adjustedY,
                snapshotBounds,
                ResolveMirrorImageSourceLayerOrigin(snapshotBounds),
                flip);
        }

        internal static Point ResolveMirrorImagePreparedSnapshotDrawPosition(
            int screenX,
            int adjustedY,
            Rectangle snapshotBounds,
            Point snapshotOrigin,
            bool flip)
        {
            if (snapshotBounds.Width <= 0 || snapshotBounds.Height <= 0)
            {
                return new Point(screenX, adjustedY);
            }

            int drawX = flip
                ? screenX + snapshotOrigin.X - snapshotBounds.Width
                : screenX - snapshotOrigin.X;
            int drawY = adjustedY - snapshotOrigin.Y;
            return new Point(drawX, drawY);
        }

        internal static Rectangle ResolveMirrorImagePreparedSnapshotSourceBounds(
            Rectangle preparedSourceBounds,
            Rectangle snapshotBounds)
        {
            if (!preparedSourceBounds.IsEmpty)
            {
                return preparedSourceBounds;
            }

            if (snapshotBounds.Width <= 0 || snapshotBounds.Height <= 0)
            {
                return Rectangle.Empty;
            }

            return new Rectangle(0, 0, snapshotBounds.Width, snapshotBounds.Height);
        }

        internal static bool CanRenderPreparedMirrorImageSourceLayer(
            bool hasPreparedSnapshot,
            IReadOnlyList<AssembledPart> preparedParts)
        {
            return hasPreparedSnapshot || (preparedParts != null && preparedParts.Count > 0);
        }

        internal static bool ResolveMirrorImagePreparedFallbackFacing(bool preparedFacingRight, bool currentFacingRight)
        {
            return currentFacingRight;
        }

        private static Rectangle ResolveMirrorImageLiveRenderBounds(
            AssembledFrame frame,
            AvatarRenderLayer renderLayer)
        {
            if (frame?.AvatarRenderLayers != null)
            {
                int renderLayerIndex = (int)renderLayer;
                if ((uint)renderLayerIndex < (uint)frame.AvatarRenderLayers.Length)
                {
                    return CalculateMirrorImageSourceLayerBounds(frame.AvatarRenderLayers[renderLayerIndex]);
                }
            }

            return Rectangle.Empty;
        }

        internal static Rectangle ResolveMirrorImageRenderablePositionBounds(
            Rectangle preparedBounds,
            Rectangle liveBounds)
        {
            if (!preparedBounds.IsEmpty)
            {
                return preparedBounds;
            }

            if (!liveBounds.IsEmpty)
            {
                return liveBounds;
            }

            return preparedBounds;
        }

        private Texture2D CreateMirrorImageLayerTexture(
            IReadOnlyList<AssembledPart> sourceParts,
            Rectangle bounds,
            Texture2D existingTexture = null,
            bool preservesExistingLayerObject = false)
        {
            if (_graphicsDevice == null
                || sourceParts == null
                || sourceParts.Count == 0
                || bounds.Width <= 0
                || bounds.Height <= 0)
            {
                return null;
            }

            RenderTargetBinding[] previousTargets = _graphicsDevice.GetRenderTargets();
            Viewport previousViewport = _graphicsDevice.Viewport;
            RenderTarget2D renderTarget = TryReuseMirrorImageLayerTexture(
                existingTexture,
                bounds,
                preservesExistingLayerObject)
                ?? new RenderTarget2D(
                    _graphicsDevice,
                    bounds.Width,
                    bounds.Height,
                    false,
                    SurfaceFormat.Color,
                    DepthFormat.None);
            bool reusesExistingTexture = ReferenceEquals(renderTarget, existingTexture);

            try
            {
                _graphicsDevice.SetRenderTarget(renderTarget);
                _graphicsDevice.Clear(Color.Transparent);

                using var spriteBatch = new SpriteBatch(_graphicsDevice);
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                for (int partIndex = 0; partIndex < sourceParts.Count; partIndex++)
                {
                    DrawMirrorImageSourcePartToTexture(spriteBatch, sourceParts[partIndex], bounds);
                }

                spriteBatch.End();
                return renderTarget;
            }
            catch
            {
                if (!reusesExistingTexture)
                {
                    renderTarget.Dispose();
                }

                throw;
            }
            finally
            {
                if (previousTargets.Length > 0)
                {
                    _graphicsDevice.SetRenderTargets(previousTargets);
                }
                else
                {
                    _graphicsDevice.SetRenderTarget(null);
                }

                _graphicsDevice.Viewport = previousViewport;
            }
        }

        private static RenderTarget2D TryReuseMirrorImageLayerTexture(
            Texture2D existingTexture,
            Rectangle bounds,
            bool preservesExistingLayerObject)
        {
            if (existingTexture is not RenderTarget2D renderTarget)
            {
                return null;
            }

            return CanRefreshMirrorImagePreparedSourceLayerTextureInPlace(
                preservesExistingLayerObject,
                renderTarget.Width,
                renderTarget.Height,
                bounds)
                ? renderTarget
                : null;
        }

        internal static bool CanRefreshMirrorImagePreparedSourceLayerTextureInPlace(
            bool preservesExistingLayerObject,
            int existingTextureWidth,
            int existingTextureHeight,
            Rectangle incomingBounds)
        {
            if (!preservesExistingLayerObject
                || existingTextureWidth <= 0
                || existingTextureHeight <= 0
                || incomingBounds.Width <= 0
                || incomingBounds.Height <= 0)
            {
                return false;
            }

            return existingTextureWidth >= incomingBounds.Width
                   && existingTextureHeight >= incomingBounds.Height;
        }

        internal static bool CanPreserveMirrorImagePreparedSourceLayerPlacement(
            bool preservesExistingLayerObject,
            Rectangle existingBounds,
            int existingPartCount)
        {
            return preservesExistingLayerObject
                   && existingPartCount > 0
                   && !existingBounds.IsEmpty;
        }

        internal static Rectangle ResolveMirrorImagePreparedSourceLayerPlacementBounds(
            Rectangle existingBounds,
            Rectangle incomingBounds,
            bool preservesExistingLayerPlacement)
        {
            if (!preservesExistingLayerPlacement || existingBounds.IsEmpty)
            {
                return incomingBounds;
            }

            if (incomingBounds.Width <= 0 || incomingBounds.Height <= 0)
            {
                return existingBounds;
            }

            return new Rectangle(
                existingBounds.X,
                existingBounds.Y,
                incomingBounds.Width,
                incomingBounds.Height);
        }

        internal static Point ResolveMirrorImagePreparedSourceLayerOrigin(
            Point existingOrigin,
            Rectangle incomingBounds,
            bool preservesExistingLayerPlacement)
        {
            if (preservesExistingLayerPlacement)
            {
                return existingOrigin;
            }

            return incomingBounds.IsEmpty
                ? Point.Zero
                : ResolveMirrorImageSourceLayerOrigin(incomingBounds);
        }

        private static bool CanPreserveMirrorImagePreparedFeetOffset(
            IReadOnlyList<MirrorImagePreparedSourceLayer> existingLayers,
            bool currentFacingRight)
        {
            if (existingLayers == null || existingLayers.Count == 0)
            {
                return false;
            }

            for (int layerIndex = 0; layerIndex < existingLayers.Count; layerIndex++)
            {
                MirrorImagePreparedSourceLayer layer = existingLayers[layerIndex];
                if (layer?.Parts == null || layer.Parts.Count == 0)
                {
                    continue;
                }

                if (layer.PreparedFacingRight == currentFacingRight)
                {
                    return true;
                }
            }

            return false;
        }

        internal static int ResolveMirrorImagePreparedFeetOffset(
            int existingFeetOffset,
            int incomingFeetOffset,
            bool preservesExistingLayerObject)
        {
            return preservesExistingLayerObject
                ? existingFeetOffset
                : incomingFeetOffset;
        }

        private static void DrawMirrorImageSourcePartToTexture(SpriteBatch spriteBatch, AssembledPart part, Rectangle bounds)
        {
            if (part?.Texture == null || !part.IsVisible)
            {
                return;
            }

            int drawX = part.OffsetX - bounds.X;
            int drawY = part.OffsetY - bounds.Y;
            Color tint = part.Tint != Color.White ? part.Tint : Color.White;
            part.Texture.DrawBackground(spriteBatch, null, null, drawX, drawY, tint, false, null);
        }

        private static void ReplacePreparedMirrorImageSourceLayerTexture(MirrorImagePreparedSourceLayer layer, Texture2D texture)
        {
            if (ReferenceEquals(layer?.ComposedTexture, texture))
            {
                return;
            }

            DisposeMirrorImagePreparedSourceLayerTexture(layer);
            if (layer != null)
            {
                layer.ComposedTexture = texture;
            }
        }

        private static void DisposeMirrorImagePreparedSourceLayers(MirrorImagePreparedSourceLayer[] layers)
        {
            if (layers == null)
            {
                return;
            }

            for (int i = 0; i < layers.Length; i++)
            {
                DisposeMirrorImagePreparedSourceLayerTexture(layers[i]);
            }
        }

        private static void DisposeMirrorImagePreparedSourceLayerTexture(MirrorImagePreparedSourceLayer layer)
        {
            if (layer?.ComposedTexture == null)
            {
                return;
            }

            layer.ComposedTexture.Dispose();
            layer.ComposedTexture = null;
        }

        private bool TryGetShadowPartnerAnimation(
            int currentTime,
            out SkillAnimation animation,
            out SkillFrame frame,
            out int frameElapsedMs,
            out bool facingRight)
        {
            animation = null;
            frame = null;
            frameElapsedMs = 0;
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

            animation = _activeShadowPartner.CurrentPlaybackAnimation ?? animation;
            int animationTime = Math.Max(0, currentTime - _activeShadowPartner.CurrentActionStartTime);
            if (!animation.TryGetFrameAtTime(animationTime, out frame, out frameElapsedMs))
            {
                return false;
            }

            facingRight = _activeShadowPartner.CurrentFacingRight;
            return true;
        }

        private static float ResolveShadowPartnerFrameAlpha(SkillFrame frame, int frameElapsedMs)
        {
            if (frame == null)
            {
                return 1f;
            }

            int startAlpha = Math.Clamp(frame.AlphaStart, 0, 255);
            int endAlpha = Math.Clamp(frame.AlphaEnd, 0, 255);
            if (startAlpha == endAlpha)
            {
                return startAlpha / 255f;
            }

            float progress = frame.Delay <= 0
                ? 1f
                : MathHelper.Clamp(frameElapsedMs / (float)Math.Max(1, frame.Delay), 0f, 1f);

            return MathHelper.Lerp(startAlpha, endAlpha, progress) / 255f;
        }

        private int ResolveShadowPartnerHorizontalOffsetPx(SkillAnimation currentAnimation)
        {
            return ShadowPartnerClientActionResolver.ResolveHorizontalOffsetPx(
                currentAnimation,
                _activeShadowPartner?.HorizontalOffsetPx ?? 26);
        }

        private void DrawAvatarEffectPlane(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            List<AvatarEffectRenderable> avatarEffects,
            SkillAvatarEffectPlane plane,
            AssembledFrame assembledFrame,
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

                ResolveAvatarEffectAnchorPosition(assembledFrame, screenX, screenY, effect.PositionCode, out int anchorX, out int anchorY);
                bool shouldFlip = FacingRight ^ effect.Frame.Flip;
                int drawX = shouldFlip
                    ? anchorX - (effect.Frame.Texture.Width - effect.Frame.Origin.X)
                    : anchorX - effect.Frame.Origin.X;
                int drawY = anchorY - effect.Frame.Origin.Y;

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
                if (effectState.HideOnRotateAction
                    && ShouldHideRotateSensitiveAvatarEffect())
                {
                    continue;
                }

                elapsedTime = Math.Max(0, currentTime - effectState.AnimationStartTime);

                PersistentSkillAvatarEffectRenderSelection selection =
                    ResolvePersistentSkillAvatarEffectRenderSelectionForParity(
                        effectState.IsFinishing,
                        effectState.Mode == SkillAvatarEffectMode.LadderOrRope,
                        effectState.HideOnLadderOrRope,
                        effectState.LadderOverlayAnimation,
                        effectState.GroundOverlayAnimation,
                        effectState.GroundOverlaySecondaryAnimation,
                        effectState.GroundUnderFaceAnimation,
                        effectState.GroundUnderFaceSecondaryAnimation,
                        effectState.LadderOverlayFinishAnimation,
                        effectState.GroundOverlayFinishAnimation,
                        effectState.GroundUnderFaceFinishAnimation);
                SkillAvatarEffectPlane overlayPlane = selection.OverlayUsesBehindCharacterPlane
                    ? SkillAvatarEffectPlane.BehindCharacter
                    : SkillAvatarEffectPlane.OverCharacter;

                AddAvatarEffectRenderable(renderables, selection.OverlayAnimation, overlayPlane, elapsedTime);
                AddAvatarEffectRenderable(renderables, selection.OverlaySecondaryAnimation, overlayPlane, elapsedTime);
                AddAvatarEffectRenderable(renderables, selection.UnderFaceAnimation, SkillAvatarEffectPlane.UnderFace, elapsedTime);
                AddAvatarEffectRenderable(renderables, selection.UnderFaceSecondaryAnimation, SkillAvatarEffectPlane.UnderFace, elapsedTime);
            }

            for (int i = 0; i < _transientSkillAvatarEffects.Count; i++)
            {
                TransientSkillAvatarEffectState effectState = _transientSkillAvatarEffects[i];
                if (effectState == null)
                {
                    continue;
                }

                elapsedTime = Math.Max(0, currentTime - effectState.AnimationStartTime);
                SkillAnimation primaryAnimation = effectState.IsFinishing
                    ? effectState.FinishAnimation
                    : effectState.Animation;
                SkillAnimation secondaryAnimation = effectState.IsFinishing
                    ? effectState.FinishSecondaryAnimation
                    : effectState.SecondaryAnimation;
                SkillAvatarEffectPlane primaryPlane = effectState.IsFinishing
                    ? effectState.FinishPlane
                    : effectState.Plane;
                SkillAvatarEffectPlane secondaryPlane = effectState.IsFinishing
                    ? effectState.FinishSecondaryPlane
                    : effectState.SecondaryPlane;

                AddAvatarEffectRenderable(renderables, primaryAnimation, primaryPlane, elapsedTime);
                AddAvatarEffectRenderable(renderables, secondaryAnimation, secondaryPlane, elapsedTime);
            }

            return renderables;
        }

        private void UpdateShadowPartnerRenderState(int currentTime)
        {
            if (_activeShadowPartner?.ActionAnimations == null || _activeShadowPartner.ActionAnimations.Count == 0)
            {
                return;
            }

            UpdateShadowPartnerClientOffset(currentTime);

            if (TryAdvanceShadowPartnerQueuedAction(currentTime))
            {
                return;
            }

            string playerActionName = GetShadowPartnerObservedPlayerActionName();
            bool isFloatingState = State is PlayerState.Swimming or PlayerState.Flying;
            int actionTriggerTime = GetShadowPartnerObservedActionTriggerTime();
            if (!string.Equals(playerActionName, _activeShadowPartner.ObservedPlayerActionName, StringComparison.OrdinalIgnoreCase)
                || isFloatingState != _activeShadowPartner.ObservedPlayerFloatingState
                || FacingRight != _activeShadowPartner.ObservedPlayerFacingRight
                || actionTriggerTime != _activeShadowPartner.ObservedPlayerActionTriggerTime)
            {
                _activeShadowPartner.ObservedPlayerActionName = playerActionName;
                _activeShadowPartner.ObservedPlayerFloatingState = isFloatingState;
                _activeShadowPartner.ObservedPlayerFacingRight = FacingRight;
                _activeShadowPartner.ObservedPlayerActionTriggerTime = actionTriggerTime;
                RefreshShadowPartnerClientOffsetTarget(currentTime, FacingRight);
                if (IsShadowPartnerAttackAction(playerActionName))
                {
                    string delayedAttackAction = ResolveShadowPartnerActionName(playerActionName, _activeShadowPartner.CurrentActionName);
                    if (!string.IsNullOrWhiteSpace(delayedAttackAction))
                    {
                        _activeShadowPartner.PendingActionName = delayedAttackAction;
                        _activeShadowPartner.PendingPlaybackAnimation = ResolveShadowPartnerPlaybackAnimation(
                            _activeShadowPartner.ActionAnimations,
                            delayedAttackAction,
                            playerActionName);
                        _activeShadowPartner.PendingActionReadyTime = currentTime + ResolveShadowPartnerAttackDelayMs(delayedAttackAction);
                        _activeShadowPartner.PendingFacingRight = FacingRight;
                        _activeShadowPartner.PendingForceReplay = true;
                    }
                }
                else
                {
                    string resolvedAction = ResolveShadowPartnerActionName(playerActionName, _activeShadowPartner.CurrentActionName);
                    if (ShouldHoldShadowPartnerCurrentAction(currentTime))
                    {
                        _activeShadowPartner.QueuedActionName = resolvedAction;
                        _activeShadowPartner.QueuedPlaybackAnimation = ResolveShadowPartnerPlaybackAnimation(
                            _activeShadowPartner.ActionAnimations,
                            resolvedAction,
                            playerActionName);
                        _activeShadowPartner.QueuedFacingRight = FacingRight;
                        _activeShadowPartner.QueuedForceReplay = false;
                    }
                    else
                    {
                        SetShadowPartnerAction(
                            resolvedAction,
                            currentTime,
                            FacingRight,
                            preserveTimingWhenOnlyFacingChanges: true,
                            playbackAnimation: ResolveShadowPartnerPlaybackAnimation(
                                _activeShadowPartner.ActionAnimations,
                                resolvedAction,
                                playerActionName));
                    }

                    _activeShadowPartner.PendingActionName = null;
                    _activeShadowPartner.PendingPlaybackAnimation = null;
                    _activeShadowPartner.PendingForceReplay = false;
                }
            }

            if (!string.IsNullOrWhiteSpace(_activeShadowPartner.PendingActionName)
                && currentTime >= _activeShadowPartner.PendingActionReadyTime)
            {
                string pendingActionName = _activeShadowPartner.PendingActionName;
                SkillAnimation pendingPlaybackAnimation = _activeShadowPartner.PendingPlaybackAnimation;
                bool pendingFacingRight = _activeShadowPartner.PendingFacingRight;
                bool pendingForceReplay = _activeShadowPartner.PendingForceReplay;
                _activeShadowPartner.PendingActionName = null;
                _activeShadowPartner.PendingPlaybackAnimation = null;
                _activeShadowPartner.PendingForceReplay = false;

                if (ShouldHoldShadowPartnerCurrentAction(currentTime))
                {
                    _activeShadowPartner.QueuedActionName = pendingActionName;
                    _activeShadowPartner.QueuedPlaybackAnimation = pendingPlaybackAnimation;
                    _activeShadowPartner.QueuedFacingRight = pendingFacingRight;
                    _activeShadowPartner.QueuedForceReplay = pendingForceReplay;
                }
                else
                {
                    SetShadowPartnerAction(
                        pendingActionName,
                        currentTime,
                        pendingFacingRight,
                        playbackAnimation: pendingPlaybackAnimation,
                        forceRestartWhenSameAction: pendingForceReplay);
                }
            }

            if (string.IsNullOrWhiteSpace(_activeShadowPartner.CurrentActionName))
            {
                SetShadowPartnerAction(ResolveShadowPartnerFallbackAction(), currentTime, FacingRight);
            }
        }

        private void UpdateMirrorImageRenderState(int currentTime)
        {
            if (_activeMirrorImage == null)
            {
                return;
            }

            if (!ShouldRenderMirrorImageForCurrentAction())
            {
                _activeMirrorImage.Visible = false;
                _activeMirrorImage.CurrentOffsetPx = Point.Zero;
                ResetMirrorImagePreparedSourceLayers();
                return;
            }

            string observedActionName = CurrentActionName;
            if (ShouldRestartMirrorImageStartTime(
                    _activeMirrorImage.Visible,
                    _activeMirrorImage.ObservedPlayerActionName,
                    observedActionName))
            {
                _activeMirrorImage.StartTime = currentTime;
            }

            _activeMirrorImage.ObservedPlayerActionName = observedActionName;
            _activeMirrorImage.ObservedPlayerFacingRight = FacingRight;
            _activeMirrorImage.ObservedPlayerState = State;
            _activeMirrorImage.Visible = true;
            _activeMirrorImage.CurrentOffsetPx = ResolveMirrorImageCurrentOffset(currentTime);

            int animationTime = GetRenderAnimationTime(currentTime);
            AssembledFrame currentFrame = Assembler?.GetFrameAtTime(CurrentActionName, animationTime);
            int currentFrameIndex = Assembler?.GetFrameIndexAtTime(CurrentActionName, animationTime) ?? -1;
            PrepareMirrorImageSourceLayers(currentFrame, currentFrameIndex, currentTime);
        }

        internal static bool ShouldRestartMirrorImageStartTime(
            bool mirrorVisible,
            string observedActionName,
            string currentActionName)
        {
            if (!mirrorVisible)
            {
                return true;
            }

            return !string.Equals(
                observedActionName ?? string.Empty,
                currentActionName ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }

        private int ResolveShadowPartnerAttackDelayMs(string actionName)
        {
            return ShadowPartnerClientActionResolver.ResolveAttackDelayMs(
                _activeShadowPartner?.ActionAnimations,
                actionName,
                ShadowPartnerAttackDelayMs);
        }

        private void SetShadowPartnerAction(
            string actionName,
            int currentTime,
            bool facingRight,
            SkillAnimation playbackAnimation = null,
            bool forceRestartWhenSameAction = false)
        {
            SetShadowPartnerAction(
                actionName,
                currentTime,
                facingRight,
                preserveTimingWhenOnlyFacingChanges: false,
                playbackAnimation: playbackAnimation,
                forceRestartWhenSameAction: forceRestartWhenSameAction);
        }

        private void SetShadowPartnerAction(
            string actionName,
            int currentTime,
            bool facingRight,
            bool preserveTimingWhenOnlyFacingChanges,
            SkillAnimation playbackAnimation = null,
            bool forceRestartWhenSameAction = false)
        {
            if (_activeShadowPartner == null || string.IsNullOrWhiteSpace(actionName))
            {
                return;
            }

            if (!_activeShadowPartner.ActionAnimations.ContainsKey(actionName))
            {
                return;
            }

            if (string.Equals(_activeShadowPartner.CurrentActionName, actionName, StringComparison.OrdinalIgnoreCase))
            {
                RefreshShadowPartnerClientOffsetTarget(currentTime, facingRight);
                _activeShadowPartner.CurrentPlaybackAnimation = playbackAnimation
                    ?? ResolveShadowPartnerPlaybackAnimation(_activeShadowPartner.ActionAnimations, actionName, _activeShadowPartner.ObservedPlayerActionName);

                if (forceRestartWhenSameAction)
                {
                    _activeShadowPartner.CurrentActionStartTime = currentTime;
                    _activeShadowPartner.CurrentFacingRight = facingRight;
                    return;
                }

                if (_activeShadowPartner.CurrentFacingRight == facingRight)
                {
                    return;
                }

                _activeShadowPartner.CurrentFacingRight = facingRight;
                if (preserveTimingWhenOnlyFacingChanges)
                {
                    return;
                }
            }

            _activeShadowPartner.CurrentActionName = actionName;
            _activeShadowPartner.CurrentPlaybackAnimation = playbackAnimation
                ?? ResolveShadowPartnerPlaybackAnimation(_activeShadowPartner.ActionAnimations, actionName, _activeShadowPartner.ObservedPlayerActionName);
            _activeShadowPartner.CurrentActionStartTime = currentTime;
            _activeShadowPartner.CurrentFacingRight = facingRight;
            RefreshShadowPartnerClientOffsetTarget(currentTime, facingRight);
        }

        private string GetShadowPartnerObservedPlayerActionName()
        {
            return State switch
            {
                PlayerState.Hit => "hit",
                PlayerState.Dead => "dead",
                _ => CurrentActionName
            };
        }

        private int GetShadowPartnerObservedActionTriggerTime()
        {
            if (State != PlayerState.Attacking)
            {
                return int.MinValue;
            }

            if (CurrentSkillAnimationStartTime != int.MinValue)
            {
                return CurrentSkillAnimationStartTime;
            }

            return _lastAttackTime;
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
            string normalizedWeaponType = Build?.GetWeapon()?.WeaponType;
            int? rawActionCode = TryGetCurrentClientRawActionCode(out int currentRawActionCode)
                ? currentRawActionCode
                : null;

            foreach (string candidate in ShadowPartnerClientActionResolver.EnumerateClientMappedCandidates(
                         playerActionName,
                         State,
                         fallbackActionName,
                         normalizedWeaponType,
                         rawActionCode,
                         _activeShadowPartner?.SupportedRawActionNames))
            {
                if (!string.IsNullOrWhiteSpace(candidate) && yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        private IEnumerable<string> GetShadowPartnerClientMappedCandidates(string playerActionName, string fallbackActionName)
        {
            int? rawActionCode = TryGetCurrentClientRawActionCode(out int currentRawActionCode)
                ? currentRawActionCode
                : null;

            foreach (string candidate in ShadowPartnerClientActionResolver.EnumerateClientMappedCandidates(
                         playerActionName,
                         State,
                         fallbackActionName,
                         Build?.GetWeapon()?.WeaponType,
                         rawActionCode,
                         _activeShadowPartner?.SupportedRawActionNames))
            {
                yield return candidate;
            }
        }

        private string ResolveShadowPartnerCreateActionName(IReadOnlyDictionary<string, SkillAnimation> actionAnimations)
        {
            return ShadowPartnerClientActionResolver.ResolveCreateActionName(actionAnimations, State);
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

                foreach (string candidate in EnumerateShadowPartnerWeaponAwareAttackCandidates(playerActionName))
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }

                if (string.Equals(playerActionName, "hit", StringComparison.OrdinalIgnoreCase))
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

        private IEnumerable<string> EnumerateShadowPartnerWeaponAwareAttackCandidates(string playerActionName)
        {
            if (string.IsNullOrWhiteSpace(playerActionName)
                || !playerActionName.StartsWith("attack", StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            bool floating = State is PlayerState.Jumping or PlayerState.Falling or PlayerState.Swimming or PlayerState.Flying;
            string normalizedWeaponType = Build?.GetWeapon()?.WeaponType?.ToLowerInvariant();
            bool useRangedShootFamily = normalizedWeaponType is "bow" or "crossbow" or "claw" or "gun" or "double bowgun" or "cannon";
            bool usePolearmSwingFamily = normalizedWeaponType is "spear" or "polearm";
            bool useTwoHandedMeleeFamily = normalizedWeaponType is "2h sword" or "2h axe" or "2h blunt";

            if (useRangedShootFamily)
            {
                foreach (string candidate in EnumerateShadowPartnerRangedAttackCandidates(playerActionName, floating))
                {
                    yield return candidate;
                }

                yield break;
            }

            if (string.Equals(playerActionName, "attack1", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string candidate in EnumerateShadowPartnerStabCandidates(useTwoHandedMeleeFamily, floating))
                {
                    yield return candidate;
                }

                yield break;
            }

            if (usePolearmSwingFamily)
            {
                foreach (string candidate in EnumerateShadowPartnerSwingCandidates("swingP", "swingT", floating))
                {
                    yield return candidate;
                }

                yield break;
            }

            foreach (string candidate in EnumerateShadowPartnerSwingCandidates(
                         useTwoHandedMeleeFamily ? "swingT" : "swingO",
                         useTwoHandedMeleeFamily ? "swingO" : "swingT",
                         floating))
            {
                yield return candidate;
            }
        }

        private static IEnumerable<string> EnumerateShadowPartnerRangedAttackCandidates(string playerActionName, bool floating)
        {
            if (floating)
            {
                yield return "shootF";
            }

            if (string.Equals(playerActionName, "attack2", StringComparison.OrdinalIgnoreCase))
            {
                yield return "shoot2";
                yield return "shoot1";
            }
            else
            {
                yield return "shoot1";
                yield return "shoot2";
            }

            if (!floating)
            {
                yield return "shootF";
            }
        }

        private static IEnumerable<string> EnumerateShadowPartnerStabCandidates(bool preferTwoHandedFamily, bool floating)
        {
            foreach (string candidate in EnumerateShadowPartnerAttackFamilyCandidates(
                         preferTwoHandedFamily ? "stabT" : "stabO",
                         preferTwoHandedFamily ? "stabO" : "stabT",
                         floating,
                         includeThirdGroundFrame: false))
            {
                yield return candidate;
            }
        }

        private static IEnumerable<string> EnumerateShadowPartnerSwingCandidates(
            string primaryPrefix,
            string secondaryPrefix,
            bool floating)
        {
            foreach (string candidate in EnumerateShadowPartnerAttackFamilyCandidates(
                         primaryPrefix,
                         secondaryPrefix,
                         floating,
                         includeThirdGroundFrame: true))
            {
                yield return candidate;
            }
        }

        private static IEnumerable<string> EnumerateShadowPartnerAttackFamilyCandidates(
            string primaryPrefix,
            string secondaryPrefix,
            bool floating,
            bool includeThirdGroundFrame)
        {
            if (floating)
            {
                yield return primaryPrefix + "F";
            }

            yield return primaryPrefix + "1";
            yield return primaryPrefix + "2";

            if (includeThirdGroundFrame)
            {
                yield return primaryPrefix + "3";
            }

            if (!floating)
            {
                yield return primaryPrefix + "F";
            }

            if (floating)
            {
                yield return secondaryPrefix + "F";
            }

            yield return secondaryPrefix + "1";
            yield return secondaryPrefix + "2";

            if (includeThirdGroundFrame)
            {
                yield return secondaryPrefix + "3";
            }

            if (!floating)
            {
                yield return secondaryPrefix + "F";
            }
        }

        private IEnumerable<string> EnumerateShadowPartnerClientActionAliases(string playerActionName)
        {
            foreach (string candidate in ShadowPartnerClientActionResolver.EnumerateClientActionAliases(
                         playerActionName,
                         _activeShadowPartner?.SupportedRawActionNames))
            {
                yield return candidate;
            }
        }

        private Point ResolveShadowPartnerCurrentClientOffset(bool facingRight)
        {
            if (_activeShadowPartner == null)
            {
                return Point.Zero;
            }

            int? rawActionCode = TryGetCurrentClientRawActionCode(out int resolvedRawActionCode)
                ? resolvedRawActionCode
                : null;
            return ResolveShadowPartnerClientOffset(
                _activeShadowPartner.ObservedPlayerActionName,
                State,
                facingRight,
                rawActionCode,
                HasActiveMorphTransform);
        }

        private void RefreshShadowPartnerClientOffsetTarget(int currentTime, bool facingRight)
        {
            if (_activeShadowPartner == null)
            {
                return;
            }

            Point targetOffset = ResolveShadowPartnerCurrentClientOffset(facingRight);
            if (targetOffset == _activeShadowPartner.ClientOffsetTargetPx
                && _activeShadowPartner.CurrentClientOffsetPx == targetOffset)
            {
                return;
            }

            _activeShadowPartner.ClientOffsetStartPx = _activeShadowPartner.CurrentClientOffsetPx;
            _activeShadowPartner.ClientOffsetTargetPx = targetOffset;
            _activeShadowPartner.ClientOffsetTransitionStartTime = currentTime;
        }

        private void UpdateShadowPartnerClientOffset(int currentTime)
        {
            if (_activeShadowPartner == null)
            {
                return;
            }

            Point targetOffset = ResolveShadowPartnerCurrentClientOffset(_activeShadowPartner.CurrentFacingRight);
            if (targetOffset != _activeShadowPartner.ClientOffsetTargetPx)
            {
                _activeShadowPartner.ClientOffsetStartPx = _activeShadowPartner.CurrentClientOffsetPx;
                _activeShadowPartner.ClientOffsetTargetPx = targetOffset;
                _activeShadowPartner.ClientOffsetTransitionStartTime = currentTime;
            }

            _activeShadowPartner.CurrentClientOffsetPx = ShadowPartnerClientActionResolver.InterpolateClientOffset(
                _activeShadowPartner.ClientOffsetStartPx,
                _activeShadowPartner.ClientOffsetTargetPx,
                _activeShadowPartner.ClientOffsetTransitionStartTime,
                currentTime,
                ShadowPartnerTransitionDurationMs);
        }

        private static Point ResolveShadowPartnerClientOffset(
            string observedPlayerActionName,
            PlayerState state,
            bool facingRight,
            int? rawActionCode = null,
            bool hasMorphTransform = false)
        {
            return ShadowPartnerClientActionResolver.ResolveClientTargetOffset(
                observedPlayerActionName,
                state,
                facingRight,
                ShadowPartnerClientSideOffsetPx,
                ShadowPartnerClientBackActionOffsetYPx,
                rawActionCode,
                hasMorphTransform);
        }

        private static bool IsShadowPartnerBackAction(string observedPlayerActionName, PlayerState state)
        {
            return ShadowPartnerClientActionResolver.IsClientBackAction(observedPlayerActionName, state);
        }

        private bool ShouldRenderMirrorImageForCurrentAction()
        {
            if (_activeMirrorImage == null
                || _activeMirrorImage.SkillId != SkillData.MirrorImageSkillId
                || Build?.ActivePortableChair != null
                || IsMechanicTamingMobStateActive()
                || HasActiveMorphTransform
                || State is PlayerState.Ladder or PlayerState.Rope or PlayerState.Hit or PlayerState.Dead)
            {
                return false;
            }

            if (TryGetCurrentClientRawActionCode(out int rawActionCode)
                && ShouldSuppressMirrorImageForClientAction(rawActionCode, hasMorphTransform: false))
            {
                return false;
            }

            return !IsMirrorImageSuppressedAction(CurrentActionName);
        }

        internal bool TryGetCurrentClientRawActionCode(out int rawActionCode)
        {
            rawActionCode = default;

            if (!string.IsNullOrWhiteSpace(_forcedActionName)
                && CharacterPart.TryGetClientRawActionCode(_forcedActionName, out rawActionCode))
            {
                return true;
            }

            if (CharacterPart.TryGetClientRawActionCode(CharacterPart.GetActionString(CurrentAction), out rawActionCode))
            {
                return true;
            }

            return CharacterPart.TryGetClientRawActionCode(CurrentActionName, out rawActionCode);
        }

        private bool IsMechanicTamingMobStateActive()
        {
            CharacterPart activeTamingMobPart = GetEquippedTamingMobPart();
            return activeTamingMobPart != null && IsClientOwnedVehicleTamingMobStateActive(activeTamingMobPart);
        }

        private static bool IsMirrorImageSuppressedAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            string normalized = actionName.ToLowerInvariant();
            return normalized.Contains("ladder")
                   || normalized.Contains("rope")
                   || normalized.Contains("back")
                   || normalized.StartsWith("alert", StringComparison.Ordinal)
                   || string.Equals(normalized, "hit", StringComparison.Ordinal);
        }

        internal static bool IsMirrorImageBackAction(int rawActionCode, bool hasMorphTransform)
        {
            if (hasMorphTransform)
            {
                return rawActionCode is 9 or 10;
            }

            return rawActionCode is 45 or 46 or 129 or 130;
        }

        internal static bool IsMirrorImageAlertBackAction(int rawActionCode)
        {
            return rawActionCode is 64 or 65;
        }

        internal static bool ShouldSuppressMirrorImageForClientAction(int rawActionCode, bool hasMorphTransform)
        {
            return rawActionCode == 48
                   || IsMirrorImageBackAction(rawActionCode, hasMorphTransform)
                   || IsMirrorImageAlertBackAction(rawActionCode);
        }

        private Point ResolveMirrorImageCurrentOffset(int currentTime)
        {
            return ResolveMirrorImageCurrentOffset(currentTime, _activeMirrorImage?.StartTime ?? currentTime);
        }

        private Point ResolveMirrorImageCurrentOffset(int currentTime, int transitionStartTime)
        {
            int? rawActionCode = TryGetCurrentClientRawActionCode(out int resolvedRawActionCode)
                ? resolvedRawActionCode
                : null;
            Point targetOffset = ResolveMirrorImageTargetOffset(
                FacingRight,
                CurrentActionName,
                State,
                rawActionCode,
                HasActiveMorphTransform);
            int elapsedTime = Math.Max(0, currentTime - transitionStartTime);
            float progress = MathHelper.Clamp(elapsedTime / (float)MirrorImageTransitionDurationMs, 0f, 1f);
            return new Point(
                (int)Math.Round(targetOffset.X * progress),
                (int)Math.Round(targetOffset.Y * progress));
        }

        private static Point ResolveMirrorImageTargetOffset(bool facingRight, string actionName, PlayerState state)
        {
            return ResolveMirrorImageTargetOffset(
                facingRight,
                actionName,
                state,
                rawActionCode: null,
                hasMorphTransform: false);
        }

        internal static Point ResolveMirrorImageTargetOffset(
            bool facingRight,
            string actionName,
            PlayerState state,
            int? rawActionCode,
            bool hasMorphTransform)
        {
            if ((rawActionCode.HasValue
                    && (IsMirrorImageBackAction(rawActionCode.Value, hasMorphTransform)
                        || IsMirrorImageAlertBackAction(rawActionCode.Value)))
                || IsShadowPartnerBackAction(actionName, state)
                || IsMirrorImageSuppressedAction(actionName))
            {
                return new Point(0, MirrorImageClientBackActionOffsetYPx);
            }

            return new Point(facingRight ? -MirrorImageClientSideOffsetPx : MirrorImageClientSideOffsetPx, 0);
        }

        private float ResolveMirrorImageAlpha(int currentTime)
        {
            return ResolveMirrorImageAlpha(currentTime, _activeMirrorImage?.StartTime ?? currentTime);
        }

        private static float ResolveMirrorImageAlpha(int currentTime, int transitionStartTime)
        {
            int elapsedTime = Math.Max(0, currentTime - transitionStartTime);
            float progress = MathHelper.Clamp(elapsedTime / (float)MirrorImageTransitionDurationMs, 0f, 1f);
            return MathHelper.Lerp(0.35f, 1f, progress);
        }

        internal static int ResolveMirrorImageLayerTransitionStartTime(int mirrorStartTime, int layerPreparedCurrentTime)
        {
            if (layerPreparedCurrentTime == int.MinValue)
            {
                return mirrorStartTime;
            }

            return Math.Max(mirrorStartTime, layerPreparedCurrentTime);
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
            SkillAnimation queuedPlaybackAnimation = _activeShadowPartner.QueuedPlaybackAnimation;
            bool queuedFacingRight = _activeShadowPartner.QueuedFacingRight;
            bool queuedForceReplay = _activeShadowPartner.QueuedForceReplay;
            _activeShadowPartner.QueuedActionName = null;
            _activeShadowPartner.QueuedPlaybackAnimation = null;
            _activeShadowPartner.QueuedForceReplay = false;
            SetShadowPartnerAction(
                queuedActionName,
                currentTime,
                queuedFacingRight,
                playbackAnimation: queuedPlaybackAnimation,
                forceRestartWhenSameAction: queuedForceReplay);
            return true;
        }

        private SkillAnimation ResolveShadowPartnerPlaybackAnimation(
            IReadOnlyDictionary<string, SkillAnimation> actionAnimations,
            string resolvedActionName,
            string playerActionName)
        {
            string rawActionName = null;
            if (!string.IsNullOrWhiteSpace(playerActionName)
                && TryGetCurrentClientRawActionCode(out int rawActionCode))
            {
                CharacterPart.TryGetActionStringFromCode(rawActionCode, out rawActionName);
            }

            return ShadowPartnerClientActionResolver.ResolvePlaybackAnimation(
                actionAnimations,
                resolvedActionName,
                playerActionName,
                rawActionName,
                _activeShadowPartner?.SupportedRawActionNames);
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
            return ShadowPartnerClientActionResolver.IsBlockingAction(actionName);
        }

        private static bool IsShadowPartnerAttackAction(string actionName)
        {
            return ShadowPartnerClientActionResolver.IsAttackAction(actionName);
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
            _portableChairPairActionName = ResolvePortableChairActionName(chair);
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

        public void SetPortableChairExternalOwnerPair(PortableChair chair, Vector2 position, bool facingRight)
        {
            _portableChairExternalOwnerChair = chair;
            _portableChairHasExternalOwnerPair = chair?.IsCoupleChair == true;
            _portableChairExternalOwnerPosition = position;
            _portableChairExternalOwnerFacingRight = facingRight;
        }

        public void ClearPortableChairExternalOwnerPair()
        {
            _portableChairExternalOwnerChair = null;
            _portableChairHasExternalOwnerPair = false;
            _portableChairExternalOwnerPosition = Vector2.Zero;
            _portableChairExternalOwnerFacingRight = false;
        }

        private bool TryResolvePortableChairCoupleSharedLayerState(out PortableChair chair, out bool previewPairActive)
        {
            previewPairActive = false;
            chair = Build?.ActivePortableChair;
            if (chair?.IsCoupleChair != true
                || chair.CoupleSharedLayers == null
                || chair.CoupleSharedLayers.Count == 0)
            {
                chair = null;
                return false;
            }

            if (ShouldUsePortableChairExternalPair(_portableChairExternalPairRequested, _portableChairHasExternalPair))
            {
                return IsPortableChairActualPairActive(
                    chair,
                    FacingRight,
                    X,
                    Y,
                    _portableChairExternalPairFacingRight,
                    _portableChairExternalPairPosition.X,
                    _portableChairExternalPairPosition.Y);
            }

            previewPairActive = _portableChairPairAssembler != null && ShouldDrawPortableChairPairPreview();
            if (!previewPairActive)
            {
                chair = null;
            }

            return previewPairActive;
        }

        private bool TryResolvePortableChairSharedLayerState(out PortableChair chair)
        {
            if (TryResolvePortableChairCoupleSharedLayerState(out chair, out _))
            {
                return true;
            }

            chair = _portableChairExternalOwnerChair;
            return _portableChairHasExternalOwnerPair
                   && chair?.IsCoupleChair == true
                   && chair.CoupleSharedLayers != null
                   && chair.CoupleSharedLayers.Count > 0;
        }

        private bool TryResolvePortableChairMidpointLayerState(
            out PortableChair chair,
            out float partnerX,
            out float partnerY,
            out bool midpointFacingRight)
        {
            chair = Build?.ActivePortableChair;
            partnerX = 0f;
            partnerY = 0f;
            midpointFacingRight = FacingRight;
            if (chair?.IsCoupleChair == true
                && chair.CoupleMidpointLayers != null
                && chair.CoupleMidpointLayers.Count > 0)
            {
                if (ShouldUsePortableChairExternalPair(_portableChairExternalPairRequested, _portableChairHasExternalPair))
                {
                    partnerX = _portableChairExternalPairPosition.X;
                    partnerY = _portableChairExternalPairPosition.Y;
                    return true;
                }

                if (_portableChairPairAssembler != null && ShouldDrawPortableChairPairPreview())
                {
                    partnerX = X + _portableChairPairOffset.X;
                    partnerY = Y + _portableChairPairOffset.Y;
                    return true;
                }
            }

            chair = _portableChairExternalOwnerChair;
            if (!_portableChairHasExternalOwnerPair
                || chair?.IsCoupleChair != true
                || chair.CoupleMidpointLayers == null
                || chair.CoupleMidpointLayers.Count == 0)
            {
                chair = null;
                return false;
            }

            partnerX = _portableChairExternalOwnerPosition.X;
            partnerY = _portableChairExternalOwnerPosition.Y;
            midpointFacingRight = _portableChairExternalOwnerFacingRight;
            return true;
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

        internal static bool ShouldUsePortableChairExternalPair(bool externalPairRequested, bool hasExternalPair)
        {
            return externalPairRequested && hasExternalPair;
        }

        internal static bool IsPortableChairRidingChairMountItemId(int itemId)
        {
            return itemId > 0 && itemId / 1000 == 1983;
        }

        internal static string ResolvePortableChairActivationRestrictionMessage(
            PortableChair chair,
            bool hasBuild,
            bool isAlive,
            PlayerState state,
            bool isOnFoothold,
            bool hasActiveMorphTransform,
            bool hasBlockedMountState)
        {
            if (chair == null || !hasBuild)
            {
                return "Player runtime is not available.";
            }

            if (!isAlive)
            {
                return "Portable chairs cannot be activated while dead.";
            }

            if (state != PlayerState.Standing)
            {
                return "Portable chairs can only be activated while standing still.";
            }

            if (!isOnFoothold)
            {
                return "Portable chairs can only be activated while standing on a foothold.";
            }

            if (hasActiveMorphTransform)
            {
                return "Portable chairs cannot be activated while morphed.";
            }

            if (hasBlockedMountState)
            {
                return "Portable chairs cannot be activated while mounted.";
            }

            return null;
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

        internal static string ResolvePortableChairActionName(PortableChair chair)
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
                || !IsPortableChairRidingChairMountItemId(tamingMobItemId)
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

        private bool TryGetActivePacketOwnedEmotionName(int currentTime, out string emotionName)
        {
            emotionName = null;
            if (State == PlayerState.Dead
                || string.IsNullOrWhiteSpace(_packetOwnedEmotionName)
                || string.Equals(_packetOwnedEmotionName, "default", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (_packetOwnedEmotionEndTime > 0 && currentTime >= _packetOwnedEmotionEndTime)
            {
                // CAvatar::PrepareFaceLayer also gates Etc/EmotionEffect overlays on the resolved emotion lifetime.
                ResetPacketOwnedEmotionState(clearVisualEffect: true);
                return false;
            }

            emotionName = _packetOwnedEmotionName;
            return true;
        }

        private void ResetPacketOwnedEmotionState(bool clearVisualEffect, bool clearDecodedItemOption = false)
        {
            _packetOwnedEmotionId = 0;
            _packetOwnedEmotionDurationMs = 0;
            _packetOwnedEmotionAppliedAt = 0;
            _packetOwnedEmotionName = "default";
            _packetOwnedEmotionEndTime = 0;
            if (clearDecodedItemOption)
            {
                _packetOwnedEmotionByItemOption = false;
            }

            if (clearVisualEffect)
            {
                ClearTransientSkillAvatarEffect(PacketOwnedEmotionEffectSkillId);
            }
        }

        private SkillAnimation LoadPacketOwnedEmotionEffectAnimation(string emotionName)
        {
            if (_graphicsDevice == null
                || string.IsNullOrWhiteSpace(emotionName)
                || string.Equals(emotionName, "default", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (_packetOwnedEmotionEffectCache.TryGetValue(emotionName, out SkillAnimation cachedAnimation))
            {
                return cachedAnimation;
            }

            SkillAnimation animation = LoadPacketOwnedEmotionEffectAnimationCore($"Etc/EmotionEffect.img/{emotionName}");
            _packetOwnedEmotionEffectCache[emotionName] = animation;
            return animation;
        }

        private SkillAnimation LoadPacketOwnedEmotionEffectAnimationCore(string effectUol)
        {
            if (!TryResolveEffectAssetUol(effectUol, out string category, out string imageName, out string propertyPath))
            {
                return null;
            }

            WzImage image = global::HaCreator.Program.FindImage(category, imageName);
            if (image == null)
            {
                return null;
            }

            image.ParseImage();
            WzImageProperty property = ResolveWzProperty(image, propertyPath);
            if (property is not WzSubProperty subProperty)
            {
                return null;
            }

            SkillAnimation animation = CreatePacketOwnedEmotionEffectAnimation(subProperty);
            if (animation == null || animation.Frames.Count == 0)
            {
                return null;
            }

            animation.Name = effectUol;
            animation.Loop = false;
            animation.CalculateDuration();
            return animation;
        }

        private SkillAnimation CreatePacketOwnedEmotionEffectAnimation(WzSubProperty effectProperty)
        {
            if (effectProperty == null)
            {
                return null;
            }

            SkillAnimation animation = new SkillAnimation
            {
                Name = effectProperty.Name,
                Loop = false
            };

            foreach (WzCanvasProperty canvas in effectProperty.WzProperties.OfType<WzCanvasProperty>().OrderBy(static frame => ParsePacketOwnedEmotionFrameIndex(frame.Name)))
            {
                try
                {
                    if (canvas.GetLinkedWzCanvasBitmap() is not { } bitmap)
                    {
                        continue;
                    }

                    Texture2D texture = bitmap.ToTexture2DAndDispose(_graphicsDevice);
                    if (texture == null)
                    {
                        continue;
                    }

                    WzCanvasProperty metadataCanvas = canvas;
                    WzVectorProperty origin = metadataCanvas?["origin"] as WzVectorProperty;
                    int delay = Math.Max(1, GetPacketOwnedEmotionIntValue(metadataCanvas?["delay"], defaultValue: 100));
                    DXObject textureObject = new(0, 0, texture, delay)
                    {
                        Tag = canvas
                    };

                    animation.Frames.Add(new SkillFrame
                    {
                        Texture = textureObject,
                        Origin = new Point(origin?.X.Value ?? 0, origin?.Y.Value ?? 0),
                        Delay = delay,
                        Bounds = new Rectangle(0, 0, texture.Width, texture.Height),
                        AlphaStart = Math.Clamp(GetPacketOwnedEmotionIntValue(metadataCanvas?["a0"], defaultValue: 255), 0, 255),
                        AlphaEnd = Math.Clamp(GetPacketOwnedEmotionIntValue(metadataCanvas?["a1"], defaultValue: 255), 0, 255)
                    });
                }
                catch
                {
                    // Ignore malformed emotion-effect frames and keep the render-safe subset.
                }
            }

            return animation;
        }

        private static bool TryResolveEffectAssetUol(string uol, out string category, out string imageName, out string propertyPath)
        {
            category = "Etc";
            imageName = null;
            propertyPath = null;

            if (string.IsNullOrWhiteSpace(uol))
            {
                return false;
            }

            string[] segments = uol.Replace('\\', '/').Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 3)
            {
                return false;
            }

            category = segments[0];
            int imageSegmentIndex = Array.FindIndex(segments, segment => segment.EndsWith(".img", StringComparison.OrdinalIgnoreCase));
            if (imageSegmentIndex < 1 || imageSegmentIndex >= segments.Length - 1)
            {
                return false;
            }

            imageName = string.Join("/", segments, 1, imageSegmentIndex);
            propertyPath = string.Join("/", segments, imageSegmentIndex + 1, segments.Length - imageSegmentIndex - 1);
            return !string.IsNullOrWhiteSpace(imageName) && !string.IsNullOrWhiteSpace(propertyPath);
        }

        private static WzImageProperty ResolveWzProperty(WzImage image, string propertyPath)
        {
            if (image == null || string.IsNullOrWhiteSpace(propertyPath))
            {
                return null;
            }

            string[] segments = propertyPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return null;
            }

            WzImageProperty current = image[segments[0]];
            for (int i = 1; i < segments.Length && current != null; i++)
            {
                current = current[segments[i]];
            }

            return current;
        }

        private static int ParsePacketOwnedEmotionFrameIndex(string frameName)
        {
            return int.TryParse(frameName, out int frameIndex) ? frameIndex : int.MaxValue;
        }

        private static int GetPacketOwnedEmotionIntValue(WzImageProperty property, int defaultValue)
        {
            return property switch
            {
                WzIntProperty intProperty => intProperty.Value,
                WzShortProperty shortProperty => shortProperty.Value,
                WzLongProperty longProperty => (int)Math.Clamp(longProperty.Value, int.MinValue, int.MaxValue),
                WzStringProperty stringProperty when int.TryParse(stringProperty.Value, out int parsedValue) => parsedValue,
                _ => defaultValue
            };
        }

        private static SkillAvatarEffectPlane ResolveTransientSkillAvatarEffectPlane(SkillAnimation animation, bool isClientMovementOwner = false)
        {
            return ClientOwnedAvatarEffectParity.PrefersUnderFaceAvatarEffectPlane(animation, isClientMovementOwner)
                ? SkillAvatarEffectPlane.UnderFace
                : SkillAvatarEffectPlane.OverCharacter;
        }

        internal static bool PrefersUnderFaceTransientSkillAvatarEffectPlaneForTesting(
            SkillAnimation animation,
            bool isClientMovementOwner = false)
        {
            return ResolveTransientSkillAvatarEffectPlane(animation, isClientMovementOwner) == SkillAvatarEffectPlane.UnderFace;
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

            renderables.Add(new AvatarEffectRenderable(frame, plane, animation.PositionCode));
        }

        private void ResolveAvatarEffectAnchorPosition(
            AssembledFrame assembledFrame,
            int screenX,
            int screenY,
            int? positionCode,
            out int anchorX,
            out int anchorY)
        {
            ClientOwnedAvatarEffectParity.TryResolveFaceOwnedAvatarEffectAnchor(
                assembledFrame,
                FacingRight,
                screenX,
                screenY,
                positionCode,
                out anchorX,
                out anchorY);
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

        internal static int[] GetAvatarRenderLayerInsertionIndices(IReadOnlyList<AssembledPart> parts)
        {
            int layerCount = Enum.GetValues<AvatarRenderLayer>().Length;
            var insertionIndices = new int[layerCount];
            int defaultIndex = parts?.Count ?? 0;
            Array.Fill(insertionIndices, defaultIndex);

            if (parts == null || parts.Count == 0)
            {
                return insertionIndices;
            }

            for (int i = 0; i < parts.Count; i++)
            {
                AssembledPart part = parts[i];
                if (part?.Texture == null || !part.IsVisible)
                {
                    continue;
                }

                int layerIndex = (int)part.RenderLayer;
                if ((uint)layerIndex >= (uint)insertionIndices.Length)
                {
                    continue;
                }

                if (insertionIndices[layerIndex] == defaultIndex)
                {
                    insertionIndices[layerIndex] = i;
                }
            }

            int nextInsertionIndex = defaultIndex;
            for (int layerIndex = insertionIndices.Length - 1; layerIndex >= 0; layerIndex--)
            {
                if (insertionIndices[layerIndex] == defaultIndex)
                {
                    insertionIndices[layerIndex] = nextInsertionIndex;
                }
                else
                {
                    nextInsertionIndex = insertionIndices[layerIndex];
                }
            }

            return insertionIndices;
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
            AssembledFrame frame = TryGetCurrentFrame(currentTime);
            return frame == null
                ? null
                : new Point((int)X, (int)Y - frame.FeetOffset);
        }

        internal int TryGetCurrentBodyRelMoveY(int currentTime)
        {
            string actionName = CurrentActionName;
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return 0;
            }

            int animationTime = GetRenderAnimationTime(currentTime);

            CharacterPart mountedPart = ResolveMountedStateTamingMobPart();
            if (mountedPart?.Slot == EquipSlot.TamingMob)
            {
                if (TryResolveCurrentMountedClientBodyRelMoveY(actionName, animationTime, out int mountedBodyRelMoveY))
                {
                    return mountedBodyRelMoveY;
                }

                CharacterAnimation mountedAnimation = CharacterAssembler.GetPartAnimation(mountedPart, actionName);
                if (mountedAnimation?.Frames?.Count > 0)
                {
                    int mountedFrameIndex;
                    mountedAnimation.GetFrameAtTime(animationTime, out mountedFrameIndex);
                    return ResolveClientBodyRelMoveY(mountedAnimation.Frames, mountedFrameIndex);
                }
            }

            if (Assembler == null)
            {
                return 0;
            }

            AssembledFrame[] frames = Assembler.GetAnimation(actionName);
            if (frames == null || frames.Length == 0)
            {
                return 0;
            }

            int frameIndex = Assembler.GetFrameIndexAtTime(actionName, animationTime);
            return ResolveClientBodyRelMoveY(frames, frameIndex);
        }

        internal bool TryResolveCurrentMountedClientBodyRelMoveY(
            string actionName,
            int animationTime,
            out int bodyRelMoveY)
        {
            bodyRelMoveY = 0;
            if (Assembler == null || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            SyncAssemblerActionLayerContext();
            AssembledFrame frame = Assembler.GetFrameAtTime(actionName, animationTime);
            return TryResolveMountedClientBodyRelMoveY(frame, out bodyRelMoveY);
        }

        internal static bool TryResolveMountedClientBodyRelMoveY(
            AssembledFrame frame,
            out int bodyRelMoveY)
        {
            bodyRelMoveY = 0;
            if (frame?.MapPoints == null
                || !frame.MapPoints.TryGetValue(AvatarActionLayerCoordinator.ClientBodyOriginMapPoint, out Point bodyRelMove))
            {
                return false;
            }

            bodyRelMoveY = bodyRelMove.Y;
            return true;
        }

        internal static int ResolveClientBodyRelMoveY(IReadOnlyList<CharacterFrame> frames, int frameIndex)
        {
            if (frames == null || frames.Count == 0)
            {
                return 0;
            }

            if ((uint)frameIndex >= (uint)frames.Count)
            {
                frameIndex = 0;
            }

            CharacterFrame baseFrame = frames[0];
            CharacterFrame currentFrame = frames[frameIndex];
            if (baseFrame == null || currentFrame == null)
            {
                return 0;
            }

            if (baseFrame.Map?.TryGetValue("navel", out Point baseNavel) == true
                && currentFrame.Map?.TryGetValue("navel", out Point currentNavel) == true)
            {
                return currentNavel.Y - baseNavel.Y;
            }

            return currentFrame.Origin.Y - baseFrame.Origin.Y;
        }

        internal static int ResolveClientBodyRelMoveY(IReadOnlyList<AssembledFrame> frames, int frameIndex)
        {
            if (frames == null || frames.Count == 0)
            {
                return 0;
            }

            if ((uint)frameIndex >= (uint)frames.Count)
            {
                frameIndex = 0;
            }

            AssembledFrame baseFrame = frames[0];
            AssembledFrame currentFrame = frames[frameIndex];
            if (baseFrame == null || currentFrame == null)
            {
                return 0;
            }

            if (baseFrame.MapPoints?.TryGetValue("navel", out Point baseNavel) == true
                && currentFrame.MapPoints?.TryGetValue("navel", out Point currentNavel) == true)
            {
                return currentNavel.Y - baseNavel.Y;
            }

            return currentFrame.Origin.Y - baseFrame.Origin.Y;
        }

        public AssembledFrame TryGetCurrentFrame(int currentTime)
        {
            if (Assembler == null)
            {
                return null;
            }

            SyncAssemblerActionLayerContext();
            return Assembler.GetFrameAtTime(CurrentActionName, GetRenderAnimationTime(currentTime));
        }

        public Point? TryGetCurrentBodyMapPoint(string mapPointName, int currentTime)
        {
            if (Assembler == null || string.IsNullOrWhiteSpace(mapPointName))
            {
                return null;
            }

            AssembledFrame frame = TryGetCurrentFrame(currentTime);
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
            AssembledFrame frame = TryGetCurrentFrame(currentTime);
            return frame == null || frame.Bounds.IsEmpty
                ? null
                : frame.Bounds;
        }

        public int GetCurrentUnderFaceLayerZ(int currentTime)
        {
            AssembledFrame frame = TryGetCurrentFrame(currentTime);
            if (frame?.Parts == null || frame.Parts.Count == 0)
            {
                return 0;
            }

            int layerZ = 0;
            for (int i = 0; i < frame.Parts.Count; i++)
            {
                AssembledPart part = frame.Parts[i];
                if (part.RenderLayer == AvatarRenderLayer.UnderFace)
                {
                    layerZ = Math.Max(layerZ, part.ZIndex);
                }
            }

            return layerZ;
        }

        public int GetCurrentLayerZ(int currentTime)
        {
            AssembledFrame frame = TryGetCurrentFrame(currentTime);
            if (frame?.Parts == null || frame.Parts.Count == 0)
            {
                return 0;
            }

            int layerZ = 0;
            for (int i = 0; i < frame.Parts.Count; i++)
            {
                layerZ = Math.Max(layerZ, frame.Parts[i].ZIndex);
            }

            return layerZ;
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

        internal CharacterPart ResolveMountedStateTamingMobPart()
        {
            if (_transitionTamingMobOverridePart?.Slot == EquipSlot.TamingMob)
            {
                return _transitionTamingMobOverridePart;
            }

            if (_stateDrivenTamingMobOverridePart?.Slot == EquipSlot.TamingMob)
            {
                return _stateDrivenTamingMobOverridePart;
            }

            CharacterPart clientOwnedVehicleMount = GetClientOwnedVehicleTamingMobPart();
            if (clientOwnedVehicleMount?.Slot == EquipSlot.TamingMob)
            {
                return clientOwnedVehicleMount;
            }

            CharacterPart transformOwnedVehicleMount = ResolveClientOwnedVehicleAvatarTransformMountPart();
            if (transformOwnedVehicleMount?.Slot == EquipSlot.TamingMob)
            {
                return transformOwnedVehicleMount;
            }

            return GetEquippedTamingMobPart();
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
            if (ShouldKeepClientOwnedVehicleRenderOwner(clientOwnedVehicleMount, CurrentActionName))
            {
                SetStateDrivenTamingMobOverride(clientOwnedVehicleMount);
                return;
            }

            CharacterPart transformOwnedVehicleMount = ResolveClientOwnedVehicleAvatarTransformMountPart();
            if (ShouldKeepClientOwnedVehicleRenderOwner(transformOwnedVehicleMount, CurrentActionName))
            {
                SetStateDrivenTamingMobOverride(transformOwnedVehicleMount);
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

        private CharacterPart ResolveClientOwnedVehicleAvatarTransformMountPart()
        {
            int transformMountItemId = SkillManager.ResolveClientOwnedVehicleAvatarTransformMountItemId(
                GetActiveAvatarTransformSkillId());
            return transformMountItemId == MechanicTamingMobItemId
                ? ResolveMechanicVehicleTamingMobPart()
                : null;
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
            if (mountPart?.Slot != EquipSlot.TamingMob)
            {
                return false;
            }

            if (mountPart.TamingMobActionFrameOwner?.SupportsAction(mountPart, actionName) == true)
            {
                return true;
            }

            return mountPart.GetAnimation(actionName) != null;
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

        private bool HasPortableChairBlockedMountState()
        {
            return _clientOwnedVehicleTamingMobActive || IsMechanicTamingMobStateActive();
        }

        internal static bool ShouldKeepClientOwnedVehicleRenderOwner(CharacterPart mountPart, string actionName)
        {
            if (mountPart?.Slot != EquipSlot.TamingMob)
            {
                return false;
            }

            return SkillManager.SupportsClientOwnedVehicleMountedStateForCurrentAction(
                mountPart,
                actionName);
        }

        private static bool IsStateDrivenMechanicVehicleAction(string actionName)
        {
            return ClientOwnedVehicleSkillClassifier.IsMechanicVehicleActionName(actionName, includeTransformStates: true);
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

            if (ShouldUseSuperManMorphAirborneAttack(activeTransform, state))
            {
                return ResolveSkillTransformActionName(
                    activeTransform.AirborneAttackActionNames,
                    activeTransform.AttackActionNames,
                    activeTransform.StandActionNames);
            }

            return state switch
            {
                PlayerState.Walking => ResolveSkillTransformActionName(activeTransform.WalkActionNames, activeTransform.StandActionNames),
                PlayerState.Jumping or PlayerState.Falling => ResolveSkillTransformActionName(activeTransform.JumpActionNames, activeTransform.StandActionNames),
                PlayerState.Sitting => ResolveSkillTransformActionName(activeTransform.SitActionNames, activeTransform.StandActionNames),
                PlayerState.Prone => ResolveSkillTransformActionName(activeTransform.ProneActionNames, activeTransform.StandActionNames),
                PlayerState.Ladder => ResolveSkillTransformActionName(activeTransform.LadderActionNames, activeTransform.StandActionNames),
                PlayerState.Rope => ResolveSkillTransformActionName(activeTransform.RopeActionNames, activeTransform.StandActionNames),
                PlayerState.Swimming => ResolveSkillTransformActionName(activeTransform.SwimActionNames, activeTransform.StandActionNames),
                PlayerState.Flying when ShouldUseSuperManMorphAirborneMove(activeTransform) => ResolveSkillTransformActionName(
                    activeTransform.AirborneMoveActionNames,
                    activeTransform.FlyActionNames,
                    activeTransform.StandActionNames),
                PlayerState.Flying => ResolveSkillTransformActionName(activeTransform.FlyActionNames, activeTransform.StandActionNames),
                PlayerState.Attacking => ResolveSkillTransformActionName(activeTransform.AttackActionNames, activeTransform.StandActionNames),
                PlayerState.Hit => ResolveSkillTransformActionName(activeTransform.HitActionNames, activeTransform.StandActionNames),
                PlayerState.Dead => ResolveSkillTransformActionName(activeTransform.DeadActionNames, activeTransform.HitActionNames, activeTransform.StandActionNames)
                                    ?? CharacterPart.GetActionString(CharacterAction.Dead),
                _ => ResolveSkillTransformActionName(activeTransform.StandActionNames)
            };
        }

        private bool ShouldUseSuperManMorphAirborneMove(SkillAvatarTransformState activeTransform)
        {
            if (activeTransform?.AirborneMoveActionNames == null
                || Physics == null
                || !Physics.IsUserFlying()
                || Physics.IsOnFoothold())
            {
                return false;
            }

            const float movementThreshold = 5f;
            return _inputLeft
                   || _inputRight
                   || _inputUp
                   || _inputDown
                   || Math.Abs(Physics.VelocityX) > movementThreshold
                   || Math.Abs(Physics.VelocityY) > movementThreshold;
        }

        private bool ShouldUseSuperManMorphAirborneAttack(SkillAvatarTransformState activeTransform, PlayerState state)
        {
            return state == PlayerState.Attacking
                   && activeTransform?.AirborneAttackActionNames != null
                   && Physics != null
                   && Physics.IsUserFlying()
                   && !Physics.IsOnFoothold();
        }

        private string ResolveSkillTransformActionName(params IReadOnlyList<string>[] actionGroups)
        {
            foreach (string actionName in EnumerateTransformActionNames(actionGroups))
            {
                if (HasAvatarAction(actionName))
                {
                    return actionName;
                }
            }

            foreach (string actionName in EnumerateTransformActionNames(actionGroups))
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
                case 2111002:
                    transform = CreateSingleActionTransform(skillId, "explosion", "magic6");
                    return true;
                case 22121000:
                    transform = CreateSingleActionTransform(skillId, "icebreathe_prepare", "dragonIceBreathe");
                    return true;
                case 22151001:
                    transform = CreateSingleActionTransform(skillId, "breathe_prepare", "dragonBreathe");
                    return true;
                case 31101002:
                    transform = CreateSingleActionTransform(skillId, "demonTrace", "demonTrace");
                    return true;
                case 32121003:
                    transform = CreatePreparedSingleActionTransform(skillId, normalizedAction, "cyclone_pre", "cyclone", "cyclone_after");
                    return true;
                case 4211001:
                    transform = CreateSingleActionTransform(skillId, "alert3", "alert");
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
                    transform = CreatePreparedSingleActionTransform(skillId, normalizedAction, "darkTornado_pre", "darkTornado", "darkTornado_after", "dash");
                    return true;
                case 5311002:
                    transform = CreatePreparedSingleActionTransform(skillId, normalizedAction, "noiseWave_pre", "noiseWave_ing", "noiseWave");
                    return true;
                case 5221004:
                case 5721001:
                    transform = CreateSingleActionTransform(skillId, "rapidfire", exitActionName: null);
                    return true;
                case 35001001:
                    transform = IsRocketBoosterTransformAction(normalizedAction)
                        ? CreateRocketBoosterTransform(skillId, normalizedAction)
                        : CreatePreparedMechanicTransform(skillId, normalizedAction, "flamethrower_pre", "flamethrower", "flamethrower_after");
                    return true;
                case 35101009:
                    transform = IsMechanicTankOwnedRushAction(normalizedAction)
                        ? CreateSingleActionTransform(
                            skillId,
                            ResolveMechanicTankOwnedTransformActionName(normalizedAction, "mRush", "tank_mRush"),
                            exitActionName: null)
                        : CreatePreparedMechanicTransform(skillId, normalizedAction, "flamethrower_pre2", "flamethrower2", "flamethrower_after2");
                    return true;
                case 35121003:
                    transform = CreateSingleActionTransform(
                        skillId,
                        ResolveMechanicSummonTransformActionName(normalizedAction, "msummon2", "tank_msummon2"),
                        exitActionName: null);
                    return true;
                case 35121009:
                case 35121010:
                    transform = CreateSingleActionTransform(
                        skillId,
                        ResolveMechanicSummonTransformActionName(normalizedAction, "msummon", "tank_msummon"),
                        exitActionName: null);
                    return true;
                case 35121005:
                    transform = CreatePreparedMechanicStateTransform(skillId, normalizedAction, "tank_pre", "tank_stand", "tank_walk", "tank", "tank_prone", "tank_after", attackActionAliases: new[] { "tank_laser" });
                    return true;
                case 35111004:
                    transform = CreatePreparedMechanicStateTransform(skillId, normalizedAction, "siege_pre", "siege_stand", "siege_stand", "siege", "siege_stand", "siege_after", locksMovement: true, attackActionAliases: new[] { "lasergun" });
                    return true;
                case 35121013:
                    transform = CreatePreparedMechanicStateTransform(skillId, normalizedAction, "tank_siegepre", "tank_siegestand", "tank_siegestand", "tank_siegeattack", "tank_siegestand", "tank_siegeafter", locksMovement: true);
                    return true;
                case 35101004:
                    transform = CreateRocketBoosterTransform(skillId, normalizedAction);
                    return true;
            }

            if (string.Equals(normalizedAction, "flamethrower_pre", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "flamethrower", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "flamethrower_after", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreatePreparedMechanicTransform(skillId, normalizedAction, "flamethrower_pre", "flamethrower", "flamethrower_after");
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

            if (string.Equals(normalizedAction, "cyclone_pre", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "cyclone", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "cyclone_after", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreatePreparedSingleActionTransform(skillId, normalizedAction, "cyclone_pre", "cyclone", "cyclone_after");
                return true;
            }

            if (string.Equals(normalizedAction, "swallow_pre", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "swallow_loop", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "swallow", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreatePreparedSingleActionTransform(skillId, normalizedAction, "swallow_pre", "swallow_loop", "swallow");
                return true;
            }

            if (string.Equals(normalizedAction, "wildbeast", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreateSingleActionTransform(skillId, "wildbeast", exitActionName: null);
                return true;
            }

            if (string.Equals(normalizedAction, "bluntSmash", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "bluntSmashEnd", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "soulEater_end", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "soulEater", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreateSingleActionTransform(skillId, "bluntSmash", exitActionName: null);
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
                || string.Equals(normalizedAction, "dualVulcanLoop", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "dualVulcanEnd", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreatePreparedSingleActionTransform(skillId, normalizedAction, "dualVulcanPrep", "dualVulcanLoop", "dualVulcanEnd");
                return true;
            }

            if (string.Equals(normalizedAction, "darkTornado_pre", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "darkTornado", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "darkTornado_after", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "dash", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreatePreparedSingleActionTransform(skillId, normalizedAction, "darkTornado_pre", "darkTornado", "darkTornado_after", "dash");
                return true;
            }

            if (string.Equals(normalizedAction, "noiseWave_pre", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "noiseWave_ing", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "noiseWave", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreatePreparedSingleActionTransform(skillId, normalizedAction, "noiseWave_pre", "noiseWave_ing", "noiseWave");
                return true;
            }

            if (string.Equals(normalizedAction, "rapidfire", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreateSingleActionTransform(skillId, "rapidfire", exitActionName: null);
                return true;
            }

            if (string.Equals(normalizedAction, "flamethrower_pre2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "flamethrower2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "flamethrower_after2", StringComparison.OrdinalIgnoreCase)
                || IsMechanicTankOwnedRushAction(normalizedAction))
            {
                transform = IsMechanicTankOwnedRushAction(normalizedAction)
                    ? CreateSingleActionTransform(
                        skillId,
                        ResolveMechanicTankOwnedTransformActionName(normalizedAction, "mRush", "tank_mRush"),
                        exitActionName: null)
                    : CreatePreparedMechanicTransform(skillId, normalizedAction, "flamethrower_pre2", "flamethrower2", "flamethrower_after2");
                return true;
            }

            if (string.Equals(normalizedAction, "msummon", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "tank_msummon", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreateSingleActionTransform(
                    skillId,
                    ResolveMechanicSummonTransformActionName(normalizedAction, "msummon", "tank_msummon"),
                    exitActionName: null);
                return true;
            }

            if (string.Equals(normalizedAction, "msummon2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "tank_msummon2", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreateSingleActionTransform(
                    skillId,
                    ResolveMechanicSummonTransformActionName(normalizedAction, "msummon2", "tank_msummon2"),
                    exitActionName: null);
                return true;
            }

            if (string.Equals(normalizedAction, "tank_pre", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "tank_stand", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "tank_walk", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "tank", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "tank_laser", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "tank_prone", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "tank_after", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreatePreparedMechanicStateTransform(skillId, normalizedAction, "tank_pre", "tank_stand", "tank_walk", "tank", "tank_prone", "tank_after", attackActionAliases: new[] { "tank_laser" });
                return true;
            }

            if (string.Equals(normalizedAction, "siege_pre", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "siege_stand", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "siege", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "lasergun", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "siege_after", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreatePreparedMechanicStateTransform(skillId, normalizedAction, "siege_pre", "siege_stand", "siege_stand", "siege", "siege_stand", "siege_after", locksMovement: true, attackActionAliases: new[] { "lasergun" });
                return true;
            }

            if (string.Equals(normalizedAction, "tank_siegepre", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "tank_siegestand", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "tank_siegeattack", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedAction, "tank_siegeafter", StringComparison.OrdinalIgnoreCase))
            {
                transform = CreatePreparedMechanicStateTransform(skillId, normalizedAction, "tank_siegepre", "tank_siegestand", "tank_siegestand", "tank_siegeattack", "tank_siegestand", "tank_siegeafter", locksMovement: true);
                return true;
            }

            if (IsRocketBoosterTransformAction(normalizedAction))
            {
                transform = CreateRocketBoosterTransform(skillId, normalizedAction);
                return true;
            }

            return false;
        }

        internal static bool TryResolveBuiltInSkillAvatarTransformForTesting(
            int skillId,
            string actionName,
            out SkillAvatarTransformResolutionForTesting resolution)
        {
            resolution = default;
            if (!TryCreateBuiltInSkillAvatarTransform(skillId, actionName, out SkillAvatarTransformState transform))
            {
                return false;
            }

            resolution = new SkillAvatarTransformResolutionForTesting(
                transform.StandActionNames ?? Array.Empty<string>(),
                transform.WalkActionNames ?? Array.Empty<string>(),
                transform.JumpActionNames ?? Array.Empty<string>(),
                transform.AttackActionNames ?? Array.Empty<string>(),
                transform.LadderActionNames ?? Array.Empty<string>(),
                transform.RopeActionNames ?? Array.Empty<string>(),
                transform.FlyActionNames ?? Array.Empty<string>(),
                transform.SwimActionNames ?? Array.Empty<string>(),
                transform.HitActionNames ?? Array.Empty<string>(),
                transform.ProneActionNames ?? Array.Empty<string>(),
                transform.ExitActionName,
                transform.LocksMovement);
            return true;
        }

        internal static bool TryResolveMorphAvatarTransformForTesting(
            CharacterPart morphPart,
            string actionName,
            out MorphAvatarTransformResolutionForTesting resolution)
        {
            resolution = default;
            if (morphPart?.Type != CharacterPartType.Morph)
            {
                return false;
            }

            SkillAvatarTransformState transform = CreateMorphTransform(skillId: 0, morphPart, actionName);
            resolution = new MorphAvatarTransformResolutionForTesting(
                transform.StandActionNames ?? Array.Empty<string>(),
                transform.WalkActionNames ?? Array.Empty<string>(),
                transform.JumpActionNames ?? Array.Empty<string>(),
                transform.FlyActionNames ?? Array.Empty<string>(),
                transform.AirborneMoveActionNames ?? Array.Empty<string>(),
                transform.AirborneAttackActionNames ?? Array.Empty<string>(),
                transform.LadderActionNames ?? Array.Empty<string>(),
                transform.RopeActionNames ?? Array.Empty<string>(),
                transform.SwimActionNames ?? Array.Empty<string>(),
                transform.AttackActionNames ?? Array.Empty<string>(),
                transform.HitActionNames ?? Array.Empty<string>(),
                transform.DeadActionNames ?? Array.Empty<string>());
            return true;
        }

        private static SkillAvatarTransformState CreateMorphTransform(int skillId, CharacterPart morphPart, string actionName)
        {
            string normalizedAction = actionName?.Trim();
            bool isSuperManMorph = morphPart?.IsSuperManMorph == true;
            bool hasPublishedFly2Action = HasMorphPublishedAction(morphPart, "fly2");
            bool hasPublishedAirborneMoveAction = HasMorphPublishedAction(morphPart, "fly2Move");
            bool hasPublishedAirborneAttackAction = HasMorphPublishedAction(morphPart, "fly2Skill");
            bool shouldUseSuperManLadderRopeActions = isSuperManMorph;
            bool shouldUseFly2Family = isSuperManMorph || hasPublishedFly2Action;
            string normalizedJumpAction = MorphClientActionResolver.IsJumpActionName(normalizedAction)
                ? normalizedAction
                : null;
            return new SkillAvatarTransformState
            {
                SkillId = skillId,
                SourceId = skillId,
                AvatarPart = morphPart,
                StandActionNames = CreateMorphActionVariants(morphPart, normalizedAction, "stand", "stand1", "stand2"),
                WalkActionNames = CreateMorphActionVariants(morphPart, "walk", "move", "walk1", "walk2", "stand"),
                JumpActionNames = CreateMorphActionVariants(morphPart, normalizedJumpAction, "jump", "fly", "stand"),
                SitActionNames = CreateMorphActionVariants(morphPart, "sit", "stand"),
                ProneActionNames = CreateMorphActionVariants(morphPart, "prone", "stand"),
                AttackActionNames = CreateMorphAttackActionVariants(morphPart, normalizedAction),
                LadderActionNames = shouldUseSuperManLadderRopeActions
                    ? CreateMorphActionVariants(morphPart, "ladder2", "ladder", "rope2", "rope", "stand")
                    : CreateMorphActionVariants(morphPart, "ladder", "rope", "stand"),
                RopeActionNames = shouldUseSuperManLadderRopeActions
                    ? CreateMorphActionVariants(morphPart, "rope2", "rope", "ladder2", "ladder", "stand")
                    : CreateMorphActionVariants(morphPart, "rope", "ladder", "stand"),
                FlyActionNames = shouldUseFly2Family
                    ? CreateMorphActionVariants(morphPart, "fly2", "fly", "jump", "stand")
                    : CreateMorphActionVariants(morphPart, "fly", "swim", "jump", "stand"),
                AirborneMoveActionNames = hasPublishedAirborneMoveAction
                    ? CreateMorphActionVariants(morphPart, "fly2Move", "fly2", "fly", "jump", "stand")
                    : null,
                AirborneAttackActionNames = hasPublishedAirborneAttackAction
                    ? CreateMorphActionVariants(morphPart, "fly2Skill", normalizedAction, "attack", "attack1", "fly2", "fly", "jump", "stand")
                    : null,
                SwimActionNames = CreateMorphActionVariants(morphPart, "swim", "fly", "jump", "stand"),
                HitActionNames = CreateMorphHitActionVariants(morphPart),
                DeadActionNames = CreateMorphDeadActionVariants(morphPart),
                ExitActionName = null
            };
        }

        private static bool HasMorphPublishedAction(CharacterPart morphPart, string actionName)
        {
            if (morphPart == null || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return morphPart.Animations?.ContainsKey(actionName) == true
                   || morphPart.AvailableAnimations?.Contains(actionName) == true;
        }

        private static SkillAvatarTransformState CreateMechanicTransform(int skillId, string standActionName, string walkActionName, string attackActionName, string proneActionName, string exitActionName, bool locksMovement = false)
        {
            return new SkillAvatarTransformState
            {
                SkillId = skillId,
                SourceId = skillId,
                StandActionNames = CreateActionVariants(standActionName),
                WalkActionNames = CreateActionVariants(walkActionName, standActionName),
                JumpActionNames = CreateMechanicPublishedActionVariants(standActionName),
                SitActionNames = CreateActionVariants(standActionName),
                ProneActionNames = CreateActionVariants(proneActionName, standActionName),
                AttackActionNames = CreateActionVariants(attackActionName, standActionName),
                LadderActionNames = CreateMechanicPublishedClimbActionVariants(standActionName, preferRope: false),
                RopeActionNames = CreateMechanicPublishedClimbActionVariants(standActionName, preferRope: true),
                FlyActionNames = CreateMechanicPublishedActionVariants(standActionName),
                SwimActionNames = CreateMechanicPublishedActionVariants(standActionName),
                HitActionNames = CreateMechanicPublishedActionVariants(standActionName),
                ExitActionName = exitActionName,
                LocksMovement = locksMovement
            };
        }

        private static IReadOnlyList<string> CreateMechanicPublishedActionVariants(string standActionName)
        {
            return CreateActionVariants(standActionName);
        }

        private static IReadOnlyList<string> CreateMechanicPublishedClimbActionVariants(string standActionName, bool preferRope)
        {
            return preferRope
                ? CreateActionVariants("rope2", "ladder2", standActionName)
                : CreateActionVariants("ladder2", "rope2", standActionName);
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
                SitActionNames = CreateActionVariants(actionName),
                ProneActionNames = CreateActionVariants(actionName),
                AttackActionNames = CreateActionVariants(actionName),
                LadderActionNames = CreateActionVariants(actionName),
                RopeActionNames = CreateActionVariants(actionName),
                FlyActionNames = CreateActionVariants(actionName),
                SwimActionNames = CreateActionVariants(actionName),
                HitActionNames = CreateActionVariants(actionName),
                ExitActionName = exitActionName
            };
        }

        private static SkillAvatarTransformState CreatePreparedSingleActionTransform(
            int skillId,
            string currentActionName,
            string prepareActionName,
            string holdActionName,
            string exitActionName,
            params string[] prepareActionAliases)
        {
            PreparedAvatarActionStage stage = ResolvePreparedActionStage(
                currentActionName,
                prepareActionName,
                holdActionName,
                exitActionName,
                prepareActionAliases);

            return stage switch
            {
                PreparedAvatarActionStage.Prepare => CreateSingleActionTransform(skillId, prepareActionName, exitActionName: null),
                PreparedAvatarActionStage.Exit => CreateSingleActionTransform(skillId, exitActionName, exitActionName: null),
                _ => CreateSingleActionTransform(skillId, holdActionName, exitActionName)
            };
        }

        private enum PreparedAvatarActionStage
        {
            Hold,
            Prepare,
            Exit
        }

        private static PreparedAvatarActionStage ResolvePreparedActionStage(
            string currentActionName,
            string prepareActionName,
            string holdActionName,
            string exitActionName,
            params string[] prepareActionAliases)
        {
            if (string.Equals(currentActionName, prepareActionName, StringComparison.OrdinalIgnoreCase))
            {
                return PreparedAvatarActionStage.Prepare;
            }

            if (!string.IsNullOrWhiteSpace(exitActionName)
                && string.Equals(currentActionName, exitActionName, StringComparison.OrdinalIgnoreCase))
            {
                return PreparedAvatarActionStage.Exit;
            }

            if (!string.IsNullOrWhiteSpace(holdActionName)
                && string.Equals(currentActionName, holdActionName, StringComparison.OrdinalIgnoreCase))
            {
                return PreparedAvatarActionStage.Hold;
            }

            if (prepareActionAliases != null)
            {
                foreach (string alias in prepareActionAliases)
                {
                    if (string.Equals(currentActionName, alias, StringComparison.OrdinalIgnoreCase))
                    {
                        return PreparedAvatarActionStage.Prepare;
                    }
                }
            }

            return PreparedAvatarActionStage.Hold;
        }

        private static SkillAvatarTransformState CreatePreparedMechanicTransform(
            int skillId,
            string currentActionName,
            string prepareActionName,
            string holdActionName,
            string exitActionName)
        {
            PreparedAvatarActionStage stage = ResolvePreparedActionStage(
                currentActionName,
                prepareActionName,
                holdActionName,
                exitActionName);
            if (stage == PreparedAvatarActionStage.Exit)
            {
                return CreateSingleActionTransform(skillId, exitActionName, exitActionName: null);
            }

            string activeActionName = stage == PreparedAvatarActionStage.Prepare ? prepareActionName : holdActionName;
            return CreateMechanicTransform(
                skillId,
                activeActionName,
                activeActionName,
                activeActionName,
                activeActionName,
                stage == PreparedAvatarActionStage.Prepare ? null : exitActionName);
        }

        private static string ResolveMechanicTankOwnedTransformActionName(
            string currentActionName,
            string baseActionName,
            string tankActionName)
        {
            return string.Equals(currentActionName, tankActionName, StringComparison.OrdinalIgnoreCase)
                ? tankActionName
                : baseActionName;
        }

        private static string ResolveMechanicSummonTransformActionName(
            string currentActionName,
            string baseActionName,
            string tankActionName)
        {
            return ResolveMechanicTankOwnedTransformActionName(currentActionName, baseActionName, tankActionName);
        }

        private static bool IsMechanicTankOwnedRushAction(string actionName)
        {
            return string.Equals(actionName, "mRush", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "tank_mRush", StringComparison.OrdinalIgnoreCase);
        }

        private static SkillAvatarTransformState CreatePreparedMechanicStateTransform(
            int skillId,
            string currentActionName,
            string prepareActionName,
            string standActionName,
            string walkActionName,
            string attackActionName,
            string proneActionName,
            string exitActionName,
            bool locksMovement = false,
            IReadOnlyList<string> attackActionAliases = null)
        {
            PreparedAvatarActionStage stage = ResolvePreparedActionStage(
                currentActionName,
                prepareActionName,
                standActionName,
                exitActionName);
            if (stage == PreparedAvatarActionStage.Prepare)
            {
                return CreateSingleActionTransform(skillId, prepareActionName, exitActionName: null);
            }

            if (stage == PreparedAvatarActionStage.Exit)
            {
                return CreateSingleActionTransform(skillId, exitActionName, exitActionName: null);
            }

            string resolvedAttackActionName = ResolveMechanicAttackActionName(
                currentActionName,
                attackActionName,
                attackActionAliases);

            return CreateMechanicTransform(
                skillId,
                standActionName,
                walkActionName,
                resolvedAttackActionName,
                proneActionName,
                exitActionName,
                locksMovement);
        }

        private static string ResolveMechanicAttackActionName(
            string currentActionName,
            string defaultAttackActionName,
            IReadOnlyList<string> attackActionAliases)
        {
            if (!string.IsNullOrWhiteSpace(currentActionName) && attackActionAliases != null)
            {
                foreach (string attackActionAlias in attackActionAliases)
                {
                    if (string.Equals(currentActionName, attackActionAlias, StringComparison.OrdinalIgnoreCase))
                    {
                        return attackActionAlias;
                    }
                }
            }

            return defaultAttackActionName;
        }

        private static SkillAvatarTransformState CreateRocketBoosterTransform(int skillId, string actionName)
        {
            string normalizedActionName = actionName?.Trim();
            if (string.Equals(normalizedActionName, "rbooster_after", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedActionName, "tank_rbooster_after", StringComparison.OrdinalIgnoreCase))
            {
                return CreateSingleActionTransform(skillId, normalizedActionName, exitActionName: null);
            }

            string startupActionName = string.Equals(normalizedActionName, "tank_rbooster_pre", StringComparison.OrdinalIgnoreCase)
                ? "tank_rbooster_pre"
                : string.Equals(normalizedActionName, "rbooster_pre", StringComparison.OrdinalIgnoreCase)
                    ? "rbooster_pre"
                    : "rbooster";
            string exitActionName = string.Equals(startupActionName, "tank_rbooster_pre", StringComparison.OrdinalIgnoreCase)
                ? "tank_rbooster_after"
                : "rbooster_after";
            bool usesStartupPose = !string.Equals(startupActionName, "rbooster", StringComparison.OrdinalIgnoreCase);

            return new SkillAvatarTransformState
            {
                SkillId = skillId,
                SourceId = skillId,
                StandActionNames = CreateActionVariants(usesStartupPose ? startupActionName : "rbooster"),
                WalkActionNames = CreateActionVariants(usesStartupPose ? startupActionName : "rbooster"),
                JumpActionNames = CreateActionVariants("rbooster", startupActionName),
                SitActionNames = CreateActionVariants(usesStartupPose ? startupActionName : "rbooster"),
                ProneActionNames = CreateActionVariants(usesStartupPose ? startupActionName : "rbooster"),
                AttackActionNames = CreateActionVariants("rbooster", startupActionName),
                LadderActionNames = CreateActionVariants("rbooster", startupActionName),
                RopeActionNames = CreateActionVariants("rbooster", startupActionName),
                FlyActionNames = CreateActionVariants("rbooster", startupActionName),
                SwimActionNames = CreateActionVariants("rbooster", startupActionName),
                HitActionNames = CreateActionVariants("rbooster", startupActionName),
                ExitActionName = exitActionName
            };
        }

        private static bool IsRocketBoosterTransformAction(string actionName)
        {
            return string.Equals(actionName, "rbooster", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "rbooster_pre", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "rbooster_after", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "tank_rbooster_pre", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "tank_rbooster_after", StringComparison.OrdinalIgnoreCase);
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
                SitActionNames = CreateActionVariants("ghoststand", "darksight"),
                ProneActionNames = CreateActionVariants("ghostproneStab", "ghoststand", "darksight"),
                AttackActionNames = CreateActionVariants("ghoststand", "darksight"),
                LadderActionNames = CreateActionVariants("ghostladder", "ghostrope", "ghoststand", "darksight"),
                RopeActionNames = CreateActionVariants("ghostrope", "ghostladder", "ghoststand", "darksight"),
                FlyActionNames = CreateActionVariants("ghostfly", "ghostjump", "ghoststand", "darksight"),
                SwimActionNames = CreateActionVariants("ghostfly", "ghostjump", "ghoststand", "darksight"),
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
                SitActionNames = transform.SitActionNames,
                ProneActionNames = transform.ProneActionNames,
                AttackActionNames = transform.AttackActionNames,
                LadderActionNames = transform.LadderActionNames,
                RopeActionNames = transform.RopeActionNames,
                FlyActionNames = transform.FlyActionNames,
                AirborneMoveActionNames = transform.AirborneMoveActionNames,
                AirborneAttackActionNames = transform.AirborneAttackActionNames,
                SwimActionNames = transform.SwimActionNames,
                HitActionNames = transform.HitActionNames,
                DeadActionNames = transform.DeadActionNames,
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

                foreach (string candidate in MorphClientActionResolver.EnumerateClientActionAliases(morphPart, actionName))
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

        private static IReadOnlyList<string> CreateMorphAttackActionVariants(CharacterPart morphPart, params string[] preferredActions)
        {
            var actions = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(string actionName)
            {
                if (string.IsNullOrWhiteSpace(actionName))
                {
                    return;
                }

                foreach (string candidate in MorphClientActionResolver.EnumerateClientActionAliases(morphPart, actionName))
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

            string[] genericCombatActions =
            {
                "attack",
                "attack1",
                "attack2",
                "stabO1",
                "stabO2",
                "stabOF",
                "stabT1",
                "stabT2",
                "stabTF",
                "swingO1",
                "swingO2",
                "swingO3",
                "swingOF",
                "swingT1",
                "swingT2",
                "swingT3",
                "swingTF",
                "swingP1",
                "swingP2",
                "swingPF",
                "shoot1",
                "shoot2",
                "shootF",
                "shotC1",
                "proneStab"
            };

            foreach (string actionName in genericCombatActions)
            {
                Add(actionName);
            }

            if (morphPart?.Animations != null)
            {
                foreach (string actionName in morphPart.Animations.Keys
                             .Where(IsMorphAttackActionName)
                             .OrderBy(GetMorphAttackActionPriority, StringComparer.OrdinalIgnoreCase))
                {
                    Add(actionName);
                }

                foreach (string actionName in morphPart.Animations.Keys)
                {
                    Add(actionName);
                }
            }

            Add("walk");
            Add("stand");
            return actions;
        }

        private static IReadOnlyList<string> CreateMorphHitActionVariants(CharacterPart morphPart)
        {
            return CreateMorphActionVariants(morphPart, "hit", "recovery", "alert", "alert2", "alert3", "alert4", "alert5", "stand");
        }

        private static IReadOnlyList<string> CreateMorphDeadActionVariants(CharacterPart morphPart)
        {
            return CreateMorphActionVariants(morphPart, "dead", "pvpko", "alert", "stand");
        }

        private static bool IsMorphAttackActionName(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return actionName.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("stab", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("swing", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("shoot", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("shot", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("spear", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("rain", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("break", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("leap", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("smash", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("panic", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("chop", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("tempest", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("strike", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("burst", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("drain", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("fire", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("orb", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("wave", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("upper", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("spin", StringComparison.OrdinalIgnoreCase) >= 0
                   || string.Equals(actionName, "fist", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "screw", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "straight", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "somersault", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetMorphAttackActionPriority(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return "9_";
            }

            if (string.Equals(actionName, "attack", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "attack1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "attack2", StringComparison.OrdinalIgnoreCase)
                || actionName.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "0_" + actionName;
            }

            if (string.Equals(actionName, "fist", StringComparison.OrdinalIgnoreCase)
                || actionName.IndexOf("stab", StringComparison.OrdinalIgnoreCase) >= 0
                || actionName.IndexOf("swing", StringComparison.OrdinalIgnoreCase) >= 0
                || actionName.IndexOf("shoot", StringComparison.OrdinalIgnoreCase) >= 0
                || actionName.IndexOf("shot", StringComparison.OrdinalIgnoreCase) >= 0
                || actionName.IndexOf("spear", StringComparison.OrdinalIgnoreCase) >= 0
                || actionName.IndexOf("rain", StringComparison.OrdinalIgnoreCase) >= 0
                || actionName.IndexOf("break", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "1_" + actionName;
            }

            return "2_" + actionName;
        }

        private void UpdateAssemblerAvatarOverride()
        {
            if (Assembler != null)
            {
                Assembler.OverrideAvatarPart = GetActiveAvatarTransform()?.AvatarPart;
            }

            SyncAssemblerActionLayerContext();
        }

        private void SyncAssemblerActionLayerContext()
        {
            if (Assembler == null)
            {
                return;
            }

            Assembler.PreparedActionSpeedDegree = Build?.GetEffectiveWeaponAttackSpeed() ?? 6;
            Assembler.PreparedWalkSpeed = (int)Math.Round(Build?.Speed ?? 100f);
            Assembler.HeldActionFrameDelay = _sustainedSkillAnimation;
            Assembler.CurrentFacingRight = FacingRight;
        }

        private string ResolveAvatarActionLayerCoordinatorActionName(string actionName)
        {
            string resolvedActionName = actionName;
            int activeTransformSkillId = GetActiveAvatarTransform()?.SkillId ?? 0;
            string mechanicResolvedActionName = AvatarActionLayerCoordinator.ResolveMechanicTankOneTimeActionName(
                resolvedActionName,
                activeTransformSkillId);
            if (mechanicResolvedActionName != null)
            {
                resolvedActionName = mechanicResolvedActionName;
            }

            return AvatarActionLayerCoordinator.ResolvePreparedActionName(
                resolvedActionName,
                HasActiveMorphTransform);
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
