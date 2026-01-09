using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Core;
using HaSharedLibrary;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data.MobStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Entities
{
    public class MobItem : BaseDXDrawableItem, ICombatEntity
    {
        private readonly MobInstance _mobInstance;
        private NameTooltipItem _nameTooltip = null;

        // Animation system - using AnimationController for unified frame management
        private readonly MobAnimationSet _animationSet;
        private readonly AnimationController _animationController;
        private bool _isPlayingOneShot = false; // Track if playing a one-shot animation (jump, death, hit)

        // Cached mirror boundary (optimization - avoid recalculating every frame)
        private readonly CachedBoundaryChecker _boundaryChecker = new CachedBoundaryChecker();

        /// <summary>
        /// AI controller for combat and behavior
        /// </summary>
        public MobAI AI { get; private set; }

        /// <summary>
        /// Movement information for this mob (position, direction, speed, foothold)
        /// </summary>
        public MobMovementInfo MovementInfo { get; private set; }

        /// <summary>
        /// Whether movement is enabled for this mob in the simulator
        /// </summary>
        public bool MovementEnabled { get; set; } = true;

        /// <summary>
        /// Whether AI is enabled for this mob
        /// </summary>
        public bool AIEnabled { get; set; } = true;

        /// <summary>
        /// Unique ID assigned by MobPool for efficient lookup
        /// </summary>
        public int PoolId { get; set; } = 0;

        /// <summary>
        /// Current animation action being played
        /// </summary>
        public string CurrentAction => _animationController?.CurrentAction ?? "stand";

        /// <summary>
        /// Whether the death animation has completed (all frames played)
        /// </summary>
        public bool IsDeathAnimationComplete => _animationController?.IsAnimationComplete == true &&
            (CurrentAction == "die1" || CurrentAction == "die2" || CurrentAction == "die");

        /// <summary>
        /// Sound effect for when mob takes damage
        /// </summary>
        public WzSoundResourceStreamer DamageSE { get; private set; }

        /// <summary>
        /// Sound effect for when mob dies
        /// </summary>
        public WzSoundResourceStreamer DieSE { get; private set; }

        /// <summary>
        /// Sound effect for mob attack 1
        /// </summary>
        public WzSoundResourceStreamer Attack1SE { get; private set; }

        /// <summary>
        /// Sound effect for mob attack 2
        /// </summary>
        public WzSoundResourceStreamer Attack2SE { get; private set; }

        /// <summary>
        /// Sound effect for when mob hits player (character damage 1)
        /// </summary>
        public WzSoundResourceStreamer CharDam1SE { get; private set; }

        /// <summary>
        /// Sound effect for when mob hits player (character damage 2)
        /// </summary>
        public WzSoundResourceStreamer CharDam2SE { get; private set; }

        /// <summary>
        /// Sets the mob's sound effects (damage and die)
        /// </summary>
        public void SetSounds(WzSoundResourceStreamer damageSE, WzSoundResourceStreamer dieSE)
        {
            DamageSE = damageSE;
            DieSE = dieSE;
        }

        /// <summary>
        /// Sets the mob's attack sound effects
        /// </summary>
        public void SetAttackSounds(WzSoundResourceStreamer attack1SE, WzSoundResourceStreamer attack2SE)
        {
            Attack1SE = attack1SE;
            Attack2SE = attack2SE;
        }

        /// <summary>
        /// Sets the mob's character damage sound effects (when mob hits player)
        /// </summary>
        public void SetCharDamSounds(WzSoundResourceStreamer charDam1SE, WzSoundResourceStreamer charDam2SE)
        {
            CharDam1SE = charDam1SE;
            CharDam2SE = charDam2SE;
        }

        /// <summary>
        /// Play damage sound effect
        /// </summary>
        public void PlayDamageSound()
        {
            Debug.WriteLine(DamageSE != null ? "[MobItem] DamageSE not null - playing" : "[MobItem] DamageSE is null");
            DamageSE?.Play();
        }

        /// <summary>
        /// Play die sound effect
        /// </summary>
        public void PlayDieSound()
        {
            Debug.WriteLine(DieSE != null ? "[MobItem] DieSE not null - playing" : "[MobItem] DieSE is null");
            DieSE?.Play();
        }

        /// <summary>
        /// Play attack sound effect based on attack number
        /// </summary>
        /// <param name="attackNum">1 for Attack1, 2 for Attack2</param>
        public void PlayAttackSound(int attackNum = 1)
        {
            if (attackNum == 2 && Attack2SE != null)
            {
                Attack2SE.Play();
            }
            else if (Attack1SE != null)
            {
                Attack1SE.Play();
            }
        }

        /// <summary>
        /// Play character damage sound effect (when mob hits player)
        /// </summary>
        /// <param name="damNum">1 for CharDam1, 2 for CharDam2</param>
        public void PlayCharDamSound(int damNum = 1)
        {
            if (damNum == 2 && CharDam2SE != null)
            {
                CharDam2SE.Play();
            }
            else if (CharDam1SE != null)
            {
                CharDam1SE.Play();
            }
        }

        /// <summary>
        /// Apply damage to this mob with sound effects.
        /// Use this instead of calling AI.TakeDamage directly.
        /// </summary>
        /// <param name="damage">Damage amount</param>
        /// <param name="currentTick">Current tick count</param>
        /// <param name="isCritical">Whether this is a critical hit</param>
        /// <returns>True if mob died from this damage</returns>
        public bool ApplyDamage(int damage, int currentTick, bool isCritical = false)
        {
            return ApplyDamage(damage, currentTick, isCritical, null, null);
        }

        /// <summary>
        /// Apply damage to this mob with sound effects and aggro.
        /// Use this instead of calling AI.TakeDamage directly.
        /// </summary>
        /// <param name="damage">Damage amount</param>
        /// <param name="currentTick">Current tick count</param>
        /// <param name="isCritical">Whether this is a critical hit</param>
        /// <param name="attackerX">Attacker X position for aggro</param>
        /// <param name="attackerY">Attacker Y position for aggro</param>
        /// <returns>True if mob died from this damage</returns>
        public bool ApplyDamage(int damage, int currentTick, bool isCritical, float? attackerX, float? attackerY)
        {
            if (AI == null)
                return false;

            bool died = AI.TakeDamage(damage, currentTick, isCritical, attackerX, attackerY);

            // Play damage sound on hit
            PlayDamageSound();

            // Play die sound if mob died
            if (died)
            {
                PlayDieSound();
            }

            return died;
        }

        /// <summary>
        /// Gets the current animation frame for size calculations
        /// </summary>
        public IDXObject GetCurrentFrame()
        {
            return _animationController?.GetCurrentFrame();
        }

        /// <summary>
        /// Gets the hit effect frames for a specific attack action.
        /// These frames are displayed on the player when hit by this mob's attack.
        /// </summary>
        /// <param name="attackAction">Attack action name (e.g., "attack1", "attack2")</param>
        /// <returns>Hit effect frames, or null if not available</returns>
        public List<IDXObject> GetAttackHitFrames(string attackAction)
        {
            return _animationSet?.GetAttackHitEffect(attackAction);
        }

        /// <summary>
        /// Check if this mob has hit effect frames for a specific attack
        /// </summary>
        public bool HasAttackHitEffect(string attackAction)
        {
            return _animationSet?.HasAttackHitEffect(attackAction) ?? false;
        }

        /// <summary>
        /// Constructor with animation set
        /// </summary>
        /// <param name="_mobInstance"></param>
        /// <param name="animationSet"></param>
        /// <param name="_nameTooltip"></param>
        public MobItem(MobInstance _mobInstance, MobAnimationSet animationSet, NameTooltipItem _nameTooltip)
            : base(animationSet.GetFrames(AnimationKeys.Stand) ?? animationSet.GetFrames(null), _mobInstance.Flip)
        {
            this._mobInstance = _mobInstance;
            this._nameTooltip = _nameTooltip;
            this._animationSet = animationSet;

            // Initialize animation controller
            _animationController = new AnimationController(animationSet, AnimationKeys.Stand);

            InitializeMovement();
        }

        /// <summary>
        /// Constructor for a multi frame mob image (legacy support)
        /// </summary>
        /// <param name="_mobInstance"></param>
        /// <param name="frames"></param>
        /// <param name="_nameTooltip"></param>
        public MobItem(MobInstance _mobInstance, List<IDXObject> frames, NameTooltipItem _nameTooltip)
            : base(frames, _mobInstance.Flip)
        {
            this._mobInstance = _mobInstance;
            this._nameTooltip = _nameTooltip;

            // Create a simple animation set with all frames as "stand"
            _animationSet = new MobAnimationSet();
            _animationSet.AddAnimation(AnimationKeys.Stand, frames);

            // Initialize animation controller
            _animationController = new AnimationController(_animationSet, AnimationKeys.Stand);

            InitializeMovement();
        }

        /// <summary>
        /// Constructor for a single frame mob (legacy support)
        /// </summary>
        /// <param name="_mobInstance"></param>
        /// <param name="frame0"></param>
        /// <param name="_nameTooltip"></param>
        public MobItem(MobInstance _mobInstance, IDXObject frame0, NameTooltipItem _nameTooltip)
            : base(frame0, _mobInstance.Flip)
        {
            this._mobInstance = _mobInstance;
            this._nameTooltip = _nameTooltip;

            // Create a simple animation set
            _animationSet = new MobAnimationSet();
            _animationSet.AddAnimation(AnimationKeys.Stand, new List<IDXObject> { frame0 });

            // Initialize animation controller
            _animationController = new AnimationController(_animationSet, AnimationKeys.Stand);

            InitializeMovement();
        }

        /// <summary>
        /// Initialize movement info from mob instance data
        /// </summary>
        private void InitializeMovement()
        {
            MovementInfo = new MobMovementInfo();

            // Get parsed mob data (cached per mob ID)
            var mobData = _mobInstance.MobInfo?.MobData;

            // Use MobData if available, fallback to animation set
            bool isFlyingMob = mobData?.CanFly ?? _animationSet?.CanFly ?? false;
            bool isMobile = mobData?.IsMobile ?? _animationSet?.CanMove ?? false;
            bool isJumpingMob = mobData?.CanJump ?? _animationSet?.CanJump ?? false;
            bool noFlip = mobData?.NoFlip ?? false;

            // Also check animation set for movement capabilities (fallback)
            if (!isFlyingMob && _animationSet?.CanFly == true)
                isFlyingMob = true;
            if (!isJumpingMob && _animationSet?.CanJump == true)
                isJumpingMob = true;
            if (!isMobile && _animationSet?.CanMove == true)
                isMobile = true;

            MovementInfo.Initialize(
                _mobInstance.X,
                _mobInstance.Y,
                _mobInstance.rx0Shift,
                _mobInstance.rx1Shift,
                _mobInstance.yShift,  // Pass yShift for correct foothold positioning
                isFlyingMob,
                isJumpingMob,
                noFlip
            );

            // For noFlip mobs, set initial flip based on mob instance flip property
            if (noFlip)
            {
                MovementInfo.FlipX = _mobInstance.Flip;
            }

            // If mob is not mobile (no fly/move/jump animations), set to Stand type
            if (!isMobile && !isFlyingMob && !isJumpingMob)
            {
                MovementInfo.MoveType = MobMoveType.Stand;
            }

            // Set default action based on movement type
            if (isFlyingMob)
            {
                MovementInfo.CurrentAction = MobAction.Fly;
                SetAction("fly");
            }
            else if (isJumpingMob)
            {
                MovementInfo.CurrentAction = MobAction.Stand;
                SetAction("stand");
            }
            else
            {
                MovementInfo.CurrentAction = MobAction.Stand;
                SetAction("stand");
            }

            // Get movement speed from mob data (formula from MapleNecrocer)
            // MoveSpeed = (1 + speed/100) * 2, default 2
            if (mobData != null)
            {
                if (mobData.Speed != 0)
                {
                    MovementInfo.MoveSpeed = (1 + (float)mobData.Speed / 100) * 2;
                }

                if (mobData.FlySpeed != 0)
                {
                    MovementInfo.FlySpeed = (1 + (float)mobData.FlySpeed / 100) * 2;
                }
            }

            // Initialize AI controller
            InitializeAI();
        }

        /// <summary>
        /// Initialize AI controller with mob stats
        /// </summary>
        private void InitializeAI()
        {
            AI = new MobAI();

            var mobData = _mobInstance.MobInfo?.MobData;
            if (mobData != null)
            {
                // Initialize with mob stats
                int maxHp = mobData.MaxHP > 0 ? mobData.MaxHP : 100;
                int level = mobData.Level > 0 ? mobData.Level : 1;
                int exp = mobData.Exp > 0 ? mobData.Exp : 0;
                bool isBoss = mobData.IsBoss;
                bool isUndead = mobData.Undead > 0;
                bool autoAggro = mobData.FirstAttack;  // FirstAttack = mob attacks first (auto-aggro)

                AI.Initialize(maxHp, level, exp, isBoss, isUndead, autoAggro);

                // Set aggro range based on mob level/boss status
                if (isBoss)
                {
                    AI.SetAggroRange(400);
                    AI.SetAttackRange(150);

                    // Boss monsters can move across the entire connected platform
                    // instead of being limited to their RX0/RX1 spawn boundaries
                    if (MovementInfo != null)
                    {
                        MovementInfo.UsePlatformBounds = true;
                    }
                }
                else
                {
                    AI.SetAggroRange(200);
                    AI.SetAttackRange(50);
                }

                // Add attacks based on available animations
                int attackDamage = mobData.PADamage > 0 ? mobData.PADamage : 10;

                if (_animationSet.HasAnimation("attack1"))
                {
                    AI.AddAttack(1, "attack1", attackDamage, 60, 1500);
                }
                if (_animationSet.HasAnimation("attack2"))
                {
                    AI.AddAttack(2, "attack2", (int)(attackDamage * 1.5f), 80, 2000);
                }
                if (_animationSet.HasAnimation("skill1"))
                {
                    AI.AddAttack(3, "skill1", attackDamage * 2, 150, 3000, isRanged: true);
                }
            }
            else
            {
                // Default initialization
                AI.Initialize(100, 1, 0, false, false);
                if (_animationSet.HasAnimation("attack1"))
                {
                    AI.AddAttack(1, "attack1", 10, 60, 1500);
                }
            }
        }

        /// <summary>
        /// Set the current animation action
        /// </summary>
        /// <param name="action">Action name (stand, move, fly, etc.)</param>
        public void SetAction(string action)
        {
            if (_animationController == null)
                return;

            if (action == _animationController.CurrentAction)
                return;

            // If currently playing a one-shot animation (jump/hit) and it hasn't completed, don't interrupt
            if (_isPlayingOneShot && !_animationController.IsAnimationComplete)
            {
                // Exception: allow interrupting for death animation (higher priority)
                if (action != AnimationKeys.Die1 && action != AnimationKeys.Die2 && action != AnimationKeys.Die)
                    return;
            }

            // Determine if this is a one-shot animation
            bool isOneShot = action == AnimationKeys.Jump ||
                             action == AnimationKeys.Die1 || action == AnimationKeys.Die2 || action == AnimationKeys.Die ||
                             action == AnimationKeys.Hit1 || action == AnimationKeys.Hit2 || action == AnimationKeys.Hit ||
                             action.StartsWith("attack");

            if (isOneShot)
            {
                _isPlayingOneShot = true;
                _animationController.PlayOnce(action);

                // Play attack sounds when attack animation starts
                if (action == "attack1")
                {
                    PlayAttackSound(1);
                }
                else if (action == "attack2")
                {
                    PlayAttackSound(2);
                }
            }
            else
            {
                _isPlayingOneShot = false;
                _animationController.SetAction(action);
            }
        }

        #region Custom Members
        public MobInstance MobInstance
        {
            get { return this._mobInstance; }
            private set { }
        }
        #endregion

        /// <summary>
        /// Set map boundaries for mob movement (VR rectangle)
        /// </summary>
        public void SetMapBoundaries(int left, int right, int top, int bottom)
        {
            if (MovementInfo != null)
            {
                MovementInfo.MapLeft = left;
                MovementInfo.MapRight = right;
                MovementInfo.MapTop = top;
                MovementInfo.MapBottom = bottom;
                // Flying mob Y adjustment is now handled in UpdateFlyingMovement
            }
        }

        /// <summary>
        /// Update mob movement and animation. Call this from MapSimulator.Update()
        /// </summary>
        /// <param name="deltaTimeMs">Time elapsed since last update in milliseconds</param>
        /// <param name="tickCount">Current tick count for AI timing</param>
        /// <param name="playerX">Player X position (null if no player)</param>
        /// <param name="playerY">Player Y position (null if no player)</param>
        public void UpdateMovement(int deltaTimeMs, int tickCount = 0, float? playerX = null, float? playerY = null)
        {
            if (!MovementEnabled || MovementInfo == null)
                return;

            // Get current animation frame info for smooth direction changes
            int currentFrameIndex = _animationController?.CurrentFrameIndex ?? 0;
            int frameCount = _animationController?.FrameCount ?? 1;

            // Update pending direction changes (waits for animation cycle to complete)
            MovementInfo.UpdatePendingDirection(currentFrameIndex, frameCount);

            // Update AI if enabled
            if (AIEnabled && AI != null)
            {
                AI.Update(tickCount, MovementInfo.X, MovementInfo.Y, playerX, playerY);

                // AI-driven chase behavior - override movement direction when chasing
                // Don't move while attacking
                if (AI.State == MobAIState.Attack)
                {
                    // Stop movement during attack animation
                    MovementInfo.Stop();
                    UpdateAnimationAction(); // Update animation to show attack

                    // Check if attack animation has completed - notify AI to transition out of Attack state
                    if (_animationController != null && _animationController.IsAnimationComplete)
                    {
                        AI.NotifyAttackAnimationComplete(tickCount);
                    }

                    return; // Skip all movement updates
                }
                else if (AI.State == MobAIState.Chase && playerX.HasValue)
                {
                    int chaseDir = AI.GetChaseDirection(MovementInfo.X);
                    if (chaseDir != 0)
                    {
                        // Pass frame info so direction change waits for animation cycle
                        MovementInfo.ForceDirection(
                            chaseDir > 0 ? MobMoveDirection.Right : MobMoveDirection.Left,
                            currentFrameIndex,
                            frameCount);
                        MovementInfo.SetSpeedMultiplier(AI.GetSpeedMultiplier());
                    }
                }
                else
                {
                    MovementInfo.SetSpeedMultiplier(1.0f);
                }

                // If AI is dead, stop movement and skip all movement updates
                if (AI.IsDead)
                {
                    MovementInfo.Stop();
                    UpdateAnimationAction(); // Still update animation to show death
                    return;
                }
            }

            MovementInfo.UpdateMovement(deltaTimeMs);

            // Update flip state based on movement direction
            this.flip = MovementInfo.FlipX;

            // Reset one-shot animation state when mob has landed from jump
            if (_isPlayingOneShot && CurrentAction == AnimationKeys.Jump && MovementInfo.JumpState == MobJumpState.None)
            {
                _isPlayingOneShot = false;
            }

            // Update animation action based on AI state or movement state
            UpdateAnimationAction();
        }

        /// <summary>
        /// Update the animation action based on AI state or movement state
        /// </summary>
        private void UpdateAnimationAction()
        {
            if (MovementInfo == null)
                return;

            string targetAction;

            // Check for death/hit states first - these always take priority
            if (AIEnabled && AI != null && (AI.State == MobAIState.Death || AI.State == MobAIState.Removed))
            {
                targetAction = _animationSet.HasAnimation("die1") ? "die1" : "stand";
                SetAction(targetAction);
                return;
            }

            if (AIEnabled && AI != null && AI.State == MobAIState.Hit)
            {
                targetAction = _animationSet.HasAnimation("hit1") ? "hit1" : "stand";
                SetAction(targetAction);
                return;
            }

            // AI aggressive state (chase/attack) takes priority over movement state
            if (AIEnabled && AI != null && AI.IsAggressive)
            {
                targetAction = AI.GetRecommendedAction();

                // Validate that the animation exists, fallback to stand if not
                if (!_animationSet.HasAnimation(targetAction))
                {
                    targetAction = AI.State == MobAIState.Attack ? "attack1" : "stand";

                    if (!_animationSet.HasAnimation(targetAction))
                        targetAction = "stand";
                }
            }
            else
            {
                // Use movement-based animation
                switch (MovementInfo.MoveType)
                {
                    case MobMoveType.Fly:
                        targetAction = "fly";
                        break;

                    case MobMoveType.Jump:
                        // Use current action from movement info (jump when in air, move/stand on ground)
                        if (MovementInfo.CurrentAction == MobAction.Jump)
                        {
                            targetAction = _animationSet.HasAnimation("jump") ? "jump" : "stand";
                        }
                        else if (MovementInfo.CurrentAction == MobAction.Move)
                        {
                            targetAction = _animationSet.HasAnimation("move") ? "move" :
                                           _animationSet.HasAnimation("walk") ? "walk" : "stand";
                        }
                        else
                        {
                            targetAction = "stand";
                        }
                        break;

                    case MobMoveType.Move:
                        // Check if mob is currently moving or standing
                        if (MovementInfo.CurrentAction == MobAction.Move)
                        {
                            // Use "move" or "walk" whichever is available
                            targetAction = _animationSet.HasAnimation("move") ? "move" :
                                           _animationSet.HasAnimation("walk") ? "walk" : "stand";
                        }
                        else
                        {
                            targetAction = "stand";
                        }
                        break;

                    case MobMoveType.Stand:
                    default:
                        targetAction = "stand";
                        break;
                }
            }

            SetAction(targetAction);
        }

        /// <summary>
        /// Get the current animation frame based on time
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IDXObject GetCurrentAnimationFrame(int tickCount)
        {
            if (_animationController == null)
                return null;

            // Update the animation controller's frame
            _animationController.UpdateFrame(tickCount);

            return _animationController.GetCurrentFrame();
        }

        public override void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            // Calculate position offset from movement
            int positionOffsetX = 0;
            int positionOffsetY = 0;

            if (MovementEnabled && MovementInfo != null)
            {
                positionOffsetX = (int)(MovementInfo.X - _mobInstance.X);
                positionOffsetY = (int)(MovementInfo.Y - _mobInstance.Y);
            }

            int adjustedMapShiftX = mapShiftX - positionOffsetX;
            int adjustedMapShiftY = mapShiftY - positionOffsetY;

            // Get current frame from animation
            IDXObject drawFrame = GetCurrentAnimationFrame(TickCount);

            if (drawFrame != null)
            {
                int shiftCenteredX = adjustedMapShiftX - centerX;
                int shiftCenteredY = adjustedMapShiftY - centerY;

                // Origin normalization to prevent horizontal displacement during animations.
                //
                // Problem: Each animation frame has a different origin baked into its X position
                // (calculated as: mobX - frameOriginX during WZ loading). When frames have varying
                // origins (common in attack animations where the mob "lunges"), the sprite visually
                // shifts between frames.
                //
                // Solution: Normalize all frames to use frame 0's origin as reference.
                //
                // Flip behavior (inherited from BaseDXDrawableItem):
                //   - flip = false: Sprite faces RIGHT (default WZ orientation) - no adjustment needed
                //   - flip = true:  Sprite faces LEFT (mirrored) - apply origin adjustment
                //
                // When flipped, the horizontal flip inverts how origin offsets affect visual position,
                // so we must compensate by subtracting the origin difference.
                int adjustedShiftX = shiftCenteredX;
                var currentFrames = _animationController?.CurrentFrames;
                if (currentFrames != null && currentFrames.Count > 0 && flip)
                {
                    IDXObject frame0 = currentFrames[0];
                    int originAdjustX = frame0.X - drawFrame.X;
                    adjustedShiftX = shiftCenteredX - originAdjustX;
                }

                if (IsFrameWithinView(drawFrame, adjustedShiftX, shiftCenteredY,
                    renderParameters.RenderWidth, renderParameters.RenderHeight))
                {
                    drawFrame.DrawObject(sprite, skeletonMeshRenderer, gameTime,
                        adjustedShiftX, shiftCenteredY,
                        flip,
                        drawReflectionInfo);
                }
            }

            // Draw name tooltip
            if (_nameTooltip != null)
            {
                _nameTooltip.Draw(sprite, skeletonMeshRenderer, gameTime,
                    adjustedMapShiftX, adjustedMapShiftY, centerX, centerY,
                    null,
                    renderParameters,
                    TickCount);
            }
        }

        /// <summary>
        /// Gets the current X position of the mob (considering movement)
        /// </summary>
        public int CurrentX => MovementEnabled && MovementInfo != null
            ? (int)MovementInfo.X
            : _mobInstance.X;

        /// <summary>
        /// Gets the current Y position of the mob (considering movement)
        /// </summary>
        public int CurrentY => MovementEnabled && MovementInfo != null
            ? (int)MovementInfo.Y
            : _mobInstance.Y;

        /// <summary>
        /// Gets the cached mirror boundary for this mob
        /// </summary>
        public ReflectionDrawableBoundary CachedMirrorBoundary => _boundaryChecker.CachedBoundary;

        /// <summary>
        /// Updates the cached mirror boundary if the mob has moved significantly.
        /// Call this once per frame to avoid redundant boundary checks.
        /// </summary>
        /// <param name="mirrorBottomRect">Mirror bottom rectangle</param>
        /// <param name="mirrorBottomReflection">Mirror bottom reflection info</param>
        /// <param name="checkMirrorFieldData">Function to check mirror field data boundaries</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateMirrorBoundary(Rectangle mirrorBottomRect, ReflectionDrawableBoundary mirrorBottomReflection,
            Func<int, int, ReflectionDrawableBoundary> checkMirrorFieldData)
        {
            _boundaryChecker.UpdateBoundary(CurrentX, CurrentY, mirrorBottomRect, mirrorBottomReflection, checkMirrorFieldData);
        }
    }
}
