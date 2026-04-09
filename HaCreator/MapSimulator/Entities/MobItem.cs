using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Core;
using HaCreator.MapSimulator.Managers;
using HaSharedLibrary;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
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
        private const int SPAWN_FADE_DURATION_MS = 750;
        private const int DoomBodyOffsetLeft = -16;
        private const int DoomBodyOffsetTop = -34;
        private const int DoomBodyWidth = 35;
        private const int DoomBodyHeight = 34;

        private readonly MobInstance _mobInstance;
        private NameTooltipItem _nameTooltip = null;

        // Animation system - using AnimationController for unified frame management
        private readonly MobAnimationSet _animationSet;
        private readonly MobAnimationSet _doomAnimationSet;
        private readonly AnimationController _animationController;
        private bool _isPlayingOneShot = false; // Track if playing a one-shot animation (jump, death, hit)
        private bool _escortFollowActive;
        private bool _baseUsePlatformBounds;
        private int _lastAngerChargeCount = -1;
        private int _angerGaugeLoopStartTick;
        private int _angerGaugeEffectStartTick = int.MinValue;

        // Cached mirror boundary (optimization - avoid recalculating every frame)
        private readonly CachedBoundaryChecker _boundaryChecker = new CachedBoundaryChecker();
        private bool _isSpawnFading;
        private int _spawnFadeStartTick;

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
        /// Mobs with Mob.wz info/damagedByMob are encounter actors the client routes through mob-vs-mob damage.
        /// They should not be targetable by player attacks.
        /// </summary>
        public bool IsProtectedFromPlayerDamage => _mobInstance?.MobInfo?.MobData?.DamagedByMob == true;

        /// <summary>
        /// Escort and damagedByMob mobs participate in the encounter mob-vs-mob combat lane.
        /// </summary>
        public bool UsesMobCombatLane
        {
            get
            {
                MobData mobData = _mobInstance?.MobInfo?.MobData;
                return mobData?.DamagedByMob == true || (mobData?.Escort ?? 0) > 0;
            }
        }

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
        public string DamageSE { get; private set; }

        /// <summary>
        /// Sound effect for when mob dies
        /// </summary>
        public string DieSE { get; private set; }

        /// <summary>
        /// Sound effect for mob attack 1
        /// </summary>
        public string Attack1SE { get; private set; }

        /// <summary>
        /// Sound effect for mob attack 2
        /// </summary>
        public string Attack2SE { get; private set; }

        /// <summary>
        /// Sound effect for when mob hits player (character damage 1)
        /// </summary>
        public string CharDam1SE { get; private set; }

        /// <summary>
        /// Sound effect for when mob hits player (character damage 2)
        /// </summary>
        public string CharDam2SE { get; private set; }

        private SoundManager _soundManager;

        /// <summary>
        /// Sets the mob's sound effects (damage and die)
        /// </summary>
        public void SetSoundManager(SoundManager soundManager)
        {
            _soundManager = soundManager;
        }

        public void SetSounds(string damageSE, string dieSE)
        {
            DamageSE = damageSE;
            DieSE = dieSE;
        }

        /// <summary>
        /// Sets the mob's attack sound effects
        /// </summary>
        public void SetAttackSounds(string attack1SE, string attack2SE)
        {
            Attack1SE = attack1SE;
            Attack2SE = attack2SE;
        }

        public void StartSpawnFadeIn(int tickCount)
        {
            _spawnFadeStartTick = tickCount;
            _isSpawnFading = true;
        }

        /// <summary>
        /// Sets the mob's character damage sound effects (when mob hits player)
        /// </summary>
        public void SetCharDamSounds(string charDam1SE, string charDam2SE)
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
            PlaySound(DamageSE);
        }

        /// <summary>
        /// Play die sound effect
        /// </summary>
        public void PlayDieSound()
        {
            Debug.WriteLine(DieSE != null ? "[MobItem] DieSE not null - playing" : "[MobItem] DieSE is null");
            PlaySound(DieSE);
        }

        /// <summary>
        /// Play attack sound effect based on attack number
        /// </summary>
        /// <param name="attackNum">1 for Attack1, 2 for Attack2</param>
        public void PlayAttackSound(int attackNum = 1)
        {
            if (attackNum == 2 && Attack2SE != null)
            {
                PlaySound(Attack2SE);
            }
            else if (Attack1SE != null)
            {
                PlaySound(Attack1SE);
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
                PlaySound(CharDam2SE);
            }
            else if (CharDam1SE != null)
            {
                PlaySound(CharDam1SE);
            }
        }

        private void PlaySound(string soundName)
        {
            if (!string.IsNullOrEmpty(soundName))
            {
                _soundManager?.PlaySound(soundName);
            }
        }

        /// <summary>
        /// Apply damage to this mob with sound effects.
        /// Use this instead of calling AI.TakeDamage directly.
        /// </summary>
        /// <param name="damage">Damage amount</param>
        /// <param name="currentTick">Current tick count</param>
        /// <returns>True if mob died from this damage</returns>
        public bool ApplyDamage(int damage, int currentTick, bool isCritical = false)
        {
            return ApplyDamage(damage, currentTick, isCritical, null, null, true, MobDamageType.Physical);
        }

        /// <summary>
        /// Apply damage to this mob with sound effects.
        /// Use this instead of calling AI.TakeDamage directly.
        /// </summary>
        /// <param name="damage">Damage amount</param>
        /// <param name="currentTick">Current tick count</param>
        /// <param name="isCritical">Whether this is a critical hit</param>
        /// <returns>True if mob died from this damage</returns>
        public bool ApplyDamage(int damage, int currentTick, bool isCritical = false, MobDamageType damageType = MobDamageType.Physical)
        {
            return ApplyDamage(damage, currentTick, isCritical, null, null, true, damageType);
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
        public bool ApplyDamage(
            int damage,
            int currentTick,
            bool isCritical,
            float? attackerX,
            float? attackerY,
            bool originatedFromPlayer = true,
            MobDamageType damageType = MobDamageType.Physical,
            int attackerId = 0,
            MobTargetType attackerTargetType = MobTargetType.Player,
            MobExternalTargetSource attackerExternalTargetSource = MobExternalTargetSource.None)
        {
            if (AI == null)
                return false;

            if (IsProtectedFromPlayerDamage && originatedFromPlayer)
                return false;

            bool died = AI.TakeDamage(
                damage,
                currentTick,
                isCritical,
                attackerX,
                attackerY,
                damageType,
                attackerId,
                attackerTargetType,
                attackerExternalTargetSource);

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
        /// Gets an approximate visual height for the mob's current frame.
        /// This is used for effects that need to appear above the mob instead of at its feet.
        /// </summary>
        public int GetVisualHeight(int fallbackHeight = 60)
        {
            if (AI?.IsDoomed == true)
            {
                return DoomBodyHeight;
            }

            var currentFrame = GetCurrentFrame();
            return currentFrame != null && currentFrame.Height > 0
                ? currentFrame.Height
                : fallbackHeight;
        }

        /// <summary>
        /// Gets the mob's current body bounds in world space.
        /// Uses the same live animation frame and flipped-origin normalization as rendering.
        /// </summary>
        public Rectangle GetBodyHitbox(int tickCount)
        {
            IReadOnlyList<Rectangle> worldHitboxes = GetBodyHitboxes(tickCount);
            if (worldHitboxes.Count > 0)
            {
                Rectangle union = worldHitboxes[0];
                for (int i = 1; i < worldHitboxes.Count; i++)
                {
                    union = Rectangle.Union(union, worldHitboxes[i]);
                }

                return union;
            }

            IDXObject frame = GetCurrentAnimationFrame(tickCount) ?? GetCurrentFrame();
            if (frame == null)
            {
                return Rectangle.Empty;
            }

            int positionOffsetX = 0;
            int positionOffsetY = 0;
            if (MovementEnabled && MovementInfo != null)
            {
                positionOffsetX = (int)(MovementInfo.X - _mobInstance.X);
                positionOffsetY = (int)(MovementInfo.Y - _mobInstance.Y);
            }

            int worldX = frame.X + positionOffsetX;
            int worldY = frame.Y + positionOffsetY;
            // Match the draw-time normalization for flipped mobs so contact checks align with the sprite.
            var currentFrames = _animationController?.CurrentFrames;
            if (flip && currentFrames != null && currentFrames.Count > 0)
            {
                IDXObject frame0 = currentFrames[0];
                worldX += frame0.X - frame.X;
            }

            return new Rectangle(worldX, worldY, frame.Width, frame.Height);
        }

        public IReadOnlyList<Rectangle> GetBodyHitboxes(int tickCount)
        {
            if (AI?.IsDoomed == true)
            {
                return new[]
                {
                    new Rectangle(
                        CurrentX + DoomBodyOffsetLeft,
                        CurrentY + DoomBodyOffsetTop,
                        DoomBodyWidth,
                        DoomBodyHeight)
                };
            }

            IDXObject frame = GetCurrentAnimationFrame(tickCount) ?? GetCurrentFrame();
            if (frame == null)
            {
                return Array.Empty<Rectangle>();
            }

            MobAnimationSet.FrameMetadata frameMetadata = GetCurrentAnimationFrameMetadata();
            if (frameMetadata != null)
            {
                IReadOnlyList<Rectangle> localBounds = frameMetadata.MultiBodyBounds;
                if (localBounds != null && localBounds.Count > 0)
                {
                    Rectangle[] worldBounds = new Rectangle[localBounds.Count];
                    for (int i = 0; i < localBounds.Count; i++)
                    {
                        worldBounds[i] = TranslateMobBodyBoundsToWorld(localBounds[i]);
                    }

                    return worldBounds;
                }

                return new[] { TranslateMobBodyBoundsToWorld(frameMetadata.EffectiveBodyBounds) };
            }

            return Array.Empty<Rectangle>();
        }

        /// <summary>
        /// Gets the mob's current body bounds in world space using the current system tick.
        /// </summary>
        public Rectangle GetBodyHitbox()
        {
            return GetBodyHitbox(Environment.TickCount);
        }

        public IReadOnlyList<Rectangle> GetBodyHitboxes()
        {
            return GetBodyHitboxes(Environment.TickCount);
        }

        /// <summary>
        /// Gets a world-space anchor point for damage numbers above the mob.
        /// </summary>
        public Vector2 GetDamageNumberAnchor(int verticalPadding = 12)
        {
            MobAnimationSet.FrameMetadata frameMetadata = GetCurrentAnimationFrameMetadata();
            if (frameMetadata?.HasHeadAnchor == true)
            {
                int headOffsetX = flip ? -frameMetadata.HeadAnchor.X : frameMetadata.HeadAnchor.X;
                return new Vector2(
                    CurrentX + headOffsetX,
                    CurrentY + frameMetadata.HeadAnchor.Y - verticalPadding);
            }

            Rectangle bodyHitbox = GetBodyHitbox(Environment.TickCount);
            if (bodyHitbox != Rectangle.Empty)
            {
                return new Vector2(
                    bodyHitbox.Center.X,
                    bodyHitbox.Top - verticalPadding);
            }

            float x = CurrentX;
            float y = CurrentY - GetVisualHeight() - verticalPadding;
            return new Vector2(x, y);
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
        /// Gets the projectile frames for a specific attack action.
        /// These frames are displayed while a ranged mob attack is travelling.
        /// </summary>
        public List<IDXObject> GetAttackProjectileFrames(string attackAction)
        {
            return _animationSet?.GetAttackProjectileEffect(attackAction);
        }

        public List<IDXObject> GetAttackEffectFrames(string attackAction)
        {
            return _animationSet?.GetAttackEffect(attackAction);
        }

        public List<IDXObject> GetAttackWarningFrames(string attackAction)
        {
            return _animationSet?.GetAttackWarningEffect(attackAction);
        }

        public IReadOnlyList<MobAnimationSet.AttackEffectNode> GetAttackExtraEffects(string attackAction)
        {
            return _animationSet?.GetAttackExtraEffects(attackAction);
        }

        public MobAnimationSet.AttackInfoMetadata GetAttackInfo(string attackAction)
        {
            return _animationSet?.GetAttackInfoMetadata(attackAction);
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
        public MobItem(MobInstance _mobInstance, MobAnimationSet animationSet, NameTooltipItem _nameTooltip, MobAnimationSet doomAnimationSet = null)
            : base(animationSet.GetFrames(AnimationKeys.Stand) ?? animationSet.GetFrames(null), _mobInstance.Flip)
        {
            this._mobInstance = _mobInstance;
            this._nameTooltip = _nameTooltip;
            this._animationSet = animationSet;
            _doomAnimationSet = doomAnimationSet;

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
                bool isEscortMob = mobData.Escort > 0;
                bool canTargetPlayer = !isEscortMob && !mobData.DamagedByMob;
                bool autoAggro = canTargetPlayer && mobData.FirstAttack;  // FirstAttack = mob attacks first (auto-aggro)

                AI.Initialize(maxHp, level, exp, isBoss, isUndead, autoAggro);
                AI.ConfigureSpecialBehavior(
                    canTargetPlayer,
                    isEscortMob,
                    mobData.SelfDestruction?.Hp ?? -1,
                    mobData.SelfDestruction?.Action ?? -1,
                    mobData.SelfDestruction?.RemoveAfter > 0 ? mobData.SelfDestruction.RemoveAfter * 1000 : -1,
                    mobData.RemoveAfter > 0 ? mobData.RemoveAfter * 1000 : -1);
                AI.ConfigureAngerGauge(mobData.HasAngerGauge, mobData.ChargeCount);

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

                InitializeAttackEntries(mobData, isBoss);
                InitializeSkillEntries(mobData, isBoss);
                _baseUsePlatformBounds = MovementInfo?.UsePlatformBounds ?? false;
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

        public void SetEscortFollowActive(bool active)
        {
            _escortFollowActive = active;
            if (MovementInfo != null)
            {
                MovementInfo.UsePlatformBounds = _baseUsePlatformBounds || active;
            }
        }

        private void InitializeAttackEntries(MobData mobData, bool isBoss)
        {
            int basePhysicalDamage = mobData.PADamage > 0 ? mobData.PADamage : 10;
            var usedAttackMetadata = new HashSet<int>();

            for (int actionIndex = 1; actionIndex <= 9; actionIndex++)
            {
                string animationName = $"attack{actionIndex}";
                if (!_animationSet.HasAnimation(animationName))
                {
                    continue;
                }

                MobAttackData attackMeta = GetAttackMetadataForAction(mobData, actionIndex, usedAttackMetadata);
                var attackInfo = _animationSet.GetAttackInfoMetadata(animationName);
                bool isRanged = (attackMeta?.BulletSpeed ?? 0) > 0 || (isBoss && actionIndex >= 2);
                bool hasAreaWarning = attackInfo?.HasAreaWarning == true;
                bool hasAreaSlots = (attackInfo?.AreaCount ?? 0) > 1;
                bool hasPrimaryEffect = attackInfo?.HasPrimaryEffect == true;
                bool hasAreaLikeMeta = isBoss &&
                                       ((attackMeta?.DeadlyAttack ?? 0) > 0 ||
                                        (attackMeta?.Magic ?? 0) > 0 ||
                                        (attackMeta?.Disease ?? 0) > 0 ||
                                        (attackMeta?.MpBurn ?? 0) > 0) &&
                                       actionIndex >= 2;

                // Client data like Pianus attack3 uses a source-anchored beam effect with a magic flag,
                // but without areaWarning it should not be treated as a ground-targeted AoE.
                bool isArea = hasAreaWarning || hasAreaSlots || (hasAreaLikeMeta && !hasPrimaryEffect);

                int range = attackInfo?.HasRangeBounds == true
                    ? Math.Max(
                        Math.Max(System.Math.Abs(attackInfo.RangeBounds.Left), System.Math.Abs(attackInfo.RangeBounds.Right)),
                        1)
                    : (attackInfo?.RangeRadius ?? 0) > 0
                        ? attackInfo.RangeRadius
                    : DetermineAttackRange(attackMeta, actionIndex, isBoss, isRanged, isArea);
                int effectAfter = attackInfo?.EffectAfter > 0 ? attackInfo.EffectAfter : (isArea ? 380 : isRanged ? 300 : 220);
                int attackAfter = attackInfo?.AttackAfter > 0 ? attackInfo.AttackAfter : (isArea ? 520 : effectAfter + 80);
                int cooldown = ResolveAttackCooldown(attackMeta, actionIndex, isBoss, isRanged, isArea, attackAfter);
                int attackDelay = effectAfter > 0 ? effectAfter : (isArea ? 450 : isRanged ? 300 : 220);
                int damage = basePhysicalDamage;
                int areaWidth = attackInfo?.HasRangeBounds == true
                    ? System.Math.Max(attackInfo.RangeBounds.Width, 1)
                    : System.Math.Max(range, isBoss ? 180 : 120);
                int areaHeight = attackInfo?.HasRangeBounds == true
                    ? System.Math.Max(attackInfo.RangeBounds.Height, 1)
                    : (isArea ? (isBoss ? 90 : 70) : 60);
                bool isRushAttack = attackInfo?.IsRushAttack == true || attackMeta?.Rush == true;
                bool isJumpAttack = attackInfo?.IsJumpAttack == true || attackMeta?.JumpAttack == true;
                bool tremble = attackInfo?.Tremble == true || attackMeta?.Tremble == true;

                if ((attackMeta?.Magic ?? 0) > 0 && mobData.MADamage > 0)
                {
                    damage = Math.Max(damage, mobData.MADamage);
                }

                if ((attackMeta?.DeadlyAttack ?? 0) > 0)
                {
                    damage = (int)(damage * 1.4f);
                }
                else if (actionIndex >= 2)
                {
                    damage = (int)(damage * 1.2f);
                }

                AI.AddAttack(new MobAttackEntry
                {
                    AttackId = actionIndex,
                    AttackType = attackInfo?.AttackType >= 0 ? attackInfo.AttackType : attackMeta?.Type ?? -1,
                    AnimationName = animationName,
                    Damage = Math.Max(1, damage),
                    Range = range,
                    Delay = attackDelay,
                    Cooldown = cooldown,
                    IsRanged = isRanged,
                    IsAreaOfEffect = isArea,
                    EffectAfter = effectAfter,
                    AttackAfter = attackAfter,
                    BulletSpeed = (attackMeta?.BulletSpeed ?? 0) > 0 ? attackMeta.BulletSpeed : (isRanged ? 320 : 0),
                    ProjectileCount = ResolveProjectileCount(attackMeta, attackInfo, isBoss, isRanged, isArea, actionIndex),
                    AreaWidth = areaWidth,
                    AreaHeight = areaHeight,
                    RandomDelayWindow = isArea ? Math.Max(0, attackInfo?.RandDelayAttack ?? 0) : 0,
                    HasRangeBounds = attackInfo?.HasRangeBounds == true,
                    RangeLeft = attackInfo?.RangeBounds.Left ?? 0,
                    RangeTop = attackInfo?.RangeBounds.Top ?? 0,
                    RangeRight = attackInfo?.RangeBounds.Right ?? 0,
                    RangeBottom = attackInfo?.RangeBounds.Bottom ?? 0,
                    HasRangeOrigin = attackInfo?.HasRangeOrigin == true,
                    RangeOriginX = attackInfo?.RangeOrigin.X ?? 0,
                    RangeOriginY = attackInfo?.RangeOrigin.Y ?? 0,
                    RangeRadius = attackInfo?.RangeRadius ?? 0,
                    AreaCount = attackInfo?.AreaCount ?? 0,
                    AttackCount = attackInfo?.AttackCount ?? 0,
                    StartOffset = attackInfo?.StartOffset ?? 0,
                    IsRushAttack = isRushAttack,
                    IsJumpAttack = isJumpAttack,
                    EffectFacingAttach = attackInfo?.EffectFacingAttach == true,
                    Tremble = tremble,
                    IsAngerAttack = attackInfo?.IsAngerAttack == true,
                    DiseaseSkillId = attackMeta?.Disease ?? 0,
                    DiseaseLevel = Math.Max(1, attackMeta?.Level ?? 1)
                });
            }

            if (!_animationSet.HasAnimation("attack1") && _animationSet.HasAnimation("skill1") && (mobData.SkillData == null || mobData.SkillData.Count == 0))
            {
                AI.AddAttack(new MobAttackEntry
                {
                    AttackId = 1,
                    AttackType = -1,
                    AnimationName = "skill1",
                    Damage = Math.Max(1, basePhysicalDamage),
                    Range = isBoss ? 220 : 160,
                    Delay = 320,
                    Cooldown = 2400,
                    IsRanged = true,
                    EffectAfter = 280,
                    AttackAfter = 360,
                    BulletSpeed = 320,
                    ProjectileCount = isBoss ? 2 : 1,
                    AreaWidth = isBoss ? 180 : 120,
                    AreaHeight = 60
                });
            }
        }

        private void InitializeSkillEntries(MobData mobData, bool isBoss)
        {
            if (mobData.OnlyNormalAttack > 0 || mobData.SkillData == null || mobData.SkillData.Count == 0)
            {
                return;
            }

            for (int skillIndex = 0; skillIndex < mobData.SkillData.Count; skillIndex++)
            {
                var skillData = mobData.SkillData[skillIndex];
                string animationName = ResolveSkillAnimationName(skillData.Action);
                if (animationName == null)
                {
                    continue;
                }

                int actionIndex = GetActionIndex(animationName);
                AI.AddSkill(new MobSkillEntry
                {
                    SkillId = skillData.Skill,
                    Level = skillData.Level > 0 ? skillData.Level : 1,
                    ActionIndex = actionIndex,
                    EffectAfter = skillData.EffectAfter > 0 ? skillData.EffectAfter : 250,
                    SkillAfter = skillData.SkillAfter > 0 ? skillData.SkillAfter : 350,
                    AnimationName = animationName,
                    Range = DetermineSkillRange(skillData, isBoss),
                    Cooldown = DetermineSkillCooldown(skillData, isBoss),
                    SourceIndex = skillData.SourceIndex >= 0 ? skillData.SourceIndex : skillIndex,
                    Priority = skillData.Priority,
                    PreSkillIndex = skillData.PreSkillCount > 0 ? skillData.PreSkillIndex : -1,
                    PreSkillCount = skillData.PreSkillCount,
                    OnlyFsm = skillData.OnlyFsm,
                    SkillForbid = skillData.SkillForbid
                });
            }
        }

        private MobAttackData GetAttackMetadataForAction(MobData mobData, int actionIndex, HashSet<int> usedAttackMetadata)
        {
            if (mobData?.AttackData == null)
            {
                return null;
            }

            for (int i = 0; i < mobData.AttackData.Count; i++)
            {
                if (usedAttackMetadata.Contains(i))
                {
                    continue;
                }

                MobAttackData attackMeta = mobData.AttackData[i];
                int attackNum = attackMeta.AttackNum;
                if (attackMeta.Action == actionIndex || attackNum == actionIndex || attackNum + 1 == actionIndex)
                {
                    usedAttackMetadata.Add(i);
                    return attackMeta;
                }
            }

            for (int i = 0; i < mobData.AttackData.Count; i++)
            {
                if (usedAttackMetadata.Add(i))
                {
                    return mobData.AttackData[i];
                }
            }

            return null;
        }

        private static int ResolveProjectileCount(
            MobAttackData attackMeta,
            MobAnimationSet.AttackInfoMetadata attackInfo,
            bool isBoss,
            bool isRanged,
            bool isArea,
            int actionIndex)
        {
            if (!isRanged || isArea)
            {
                return 1;
            }

            int configuredCount = Math.Max(
                attackInfo?.AttackCount ?? 0,
                attackMeta?.AttackCount ?? 0);
            if (configuredCount > 0)
            {
                return configuredCount;
            }

            return isBoss ? Math.Max(1, Math.Min(3, actionIndex)) : 1;
        }

        private string ResolveSkillAnimationName(int preferredActionIndex)
        {
            if (preferredActionIndex > 0)
            {
                string preferredName = $"skill{preferredActionIndex}";
                if (_animationSet.HasAnimation(preferredName))
                {
                    return preferredName;
                }
            }

            for (int actionIndex = 1; actionIndex <= 9; actionIndex++)
            {
                string animationName = $"skill{actionIndex}";
                if (_animationSet.HasAnimation(animationName))
                {
                    return animationName;
                }
            }

            return null;
        }

        private static int DetermineAttackRange(MobAttackData attackMeta, int actionIndex, bool isBoss, bool isRanged, bool isArea)
        {
            if ((attackMeta?.BulletSpeed ?? 0) > 0)
            {
                return isBoss ? 260 : 180;
            }

            if (isArea)
            {
                return isBoss ? 220 : 140;
            }

            if (isRanged)
            {
                return isBoss ? 200 : 150;
            }

            return isBoss ? 120 + (actionIndex * 15) : 60 + (actionIndex * 20);
        }

        internal static int ResolveAttackCooldown(MobAttackData attackMeta, int actionIndex, bool isBoss, bool isRanged, bool isArea, int attackAfter)
        {
            int cooldown = DetermineAttackCooldown(attackMeta, actionIndex, isBoss, isRanged, isArea);
            if (attackAfter > 0)
            {
                cooldown = Math.Max(cooldown, attackAfter);
            }

            return cooldown;
        }

        private static int DetermineAttackCooldown(MobAttackData attackMeta, int actionIndex, bool isBoss, bool isRanged, bool isArea)
        {
            int cooldown = 1200 + (actionIndex - 1) * 250;
            if (isRanged)
            {
                cooldown += 400;
            }

            if (isArea)
            {
                cooldown += 500;
            }

            if (isBoss)
            {
                cooldown = Math.Max(1600, cooldown);
            }

            if ((attackMeta?.ConMP ?? 0) > 0)
            {
                cooldown += 200;
            }

            return cooldown;
        }

        private static int DetermineSkillRange(MobSkillData skillData, bool isBoss)
        {
            int authoredRange = ResolveAuthoredMobSkillRange(skillData?.Skill ?? 0, skillData?.Level ?? 1);
            if (authoredRange > 0)
            {
                return authoredRange;
            }

            if (skillData.OnlyFsm)
            {
                return isBoss ? 500 : 320;
            }

            return isBoss ? 360 : 260;
        }

        private static int ResolveAuthoredMobSkillRange(int skillId, int skillLevel)
        {
            if (skillId <= 0)
            {
                return 0;
            }

            WzImage mobSkillImage = Program.FindImage("Skill", "MobSkill");
            if (mobSkillImage == null)
            {
                return 0;
            }

            if (!mobSkillImage.Parsed)
            {
                mobSkillImage.ParseImage();
            }

            WzSubProperty skillNode = mobSkillImage[skillId.ToString()] as WzSubProperty;
            WzSubProperty levelNode = skillNode?["level"] as WzSubProperty;
            WzSubProperty selectedLevel =
                levelNode?[Math.Max(1, skillLevel).ToString()] as WzSubProperty ??
                levelNode?["1"] as WzSubProperty;
            if (selectedLevel == null)
            {
                return 0;
            }

            WzVectorProperty lt = selectedLevel["lt"] as WzVectorProperty;
            WzVectorProperty rb = selectedLevel["rb"] as WzVectorProperty;
            if (lt == null || rb == null)
            {
                return 0;
            }

            int horizontalRange = Math.Max(Math.Abs(lt.X.Value), Math.Abs(rb.X.Value));
            int verticalRange = Math.Max(Math.Abs(lt.Y.Value), Math.Abs(rb.Y.Value));
            return Math.Max(horizontalRange, verticalRange);
        }

        private static int DetermineSkillCooldown(MobSkillData skillData, bool isBoss)
        {
            int cooldown = skillData.SkillAfter > 0 ? skillData.SkillAfter + 1500 : 4000;
            if (skillData.PreSkillCount > 0)
            {
                cooldown += 300 * skillData.PreSkillCount;
            }

            if (isBoss)
            {
                cooldown = Math.Max(3500, cooldown);
            }

            return cooldown;
        }

        private static int GetActionIndex(string animationName)
        {
            if (string.IsNullOrEmpty(animationName))
            {
                return 1;
            }

            int suffixIndex = animationName.Length - 1;
            return suffixIndex >= 0 && int.TryParse(animationName[suffixIndex].ToString(), out int actionIndex)
                ? actionIndex
                : 1;
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
                             action.StartsWith("attack") ||
                             action.StartsWith("skill");

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
                else if (action.StartsWith("skill"))
                {
                    PlayAttackSound(1);
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

        public MobData MobData => _mobInstance.MobInfo?.MobData;
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
                UpdateAngerGaugeVisualState(tickCount);

                bool isPerformingAction = AI.State == MobAIState.Attack || AI.State == MobAIState.Skill;

                // AI-driven chase behavior - override movement direction when chasing
                // Don't move while attacking or casting
                if (isPerformingAction)
                {
                    MobAttackEntry currentAttack = AI.GetCurrentAttack();
                    bool canRushDuringAttack = currentAttack?.IsRushAttack == true &&
                                               MovementInfo.MoveType != MobMoveType.Stand;
                    bool canJumpDuringAttack = currentAttack?.IsJumpAttack == true &&
                                               MovementInfo.MoveType != MobMoveType.Stand &&
                                               MovementInfo.MoveType != MobMoveType.Fly;

                    if (canJumpDuringAttack && MovementInfo.JumpState == MobJumpState.None)
                    {
                        MovementInfo.TryTriggerAttackJump();
                    }

                    if (canRushDuringAttack)
                    {
                        int chaseDir = AI.GetChaseDirection(MovementInfo.X);
                        if (chaseDir != 0)
                        {
                            MovementInfo.ForceDirection(
                                chaseDir > 0 ? MobMoveDirection.Right : MobMoveDirection.Left,
                                currentFrameIndex,
                                frameCount);
                        }

                        MovementInfo.SetSpeedMultiplier(Math.Max(AI.GetSpeedMultiplier(), 2.4f));
                        MovementInfo.Resume();
                        MovementInfo.UpdateMovement(deltaTimeMs);
                        this.flip = MovementInfo.FlipX;
                    }
                    else if (canJumpDuringAttack && MovementInfo.JumpState != MobJumpState.None)
                    {
                        MovementInfo.SetSpeedMultiplier(Math.Max(AI.GetSpeedMultiplier(), 1.25f));
                        MovementInfo.Resume();
                        MovementInfo.UpdateMovement(deltaTimeMs);
                        this.flip = MovementInfo.FlipX;
                    }
                    else
                    {
                        // Stationary attacks still need immediate hit physics so knockback is not delayed
                        // until the attack state ends.
                        if (MovementInfo.IsInKnockback)
                        {
                            MovementInfo.Resume();
                            MovementInfo.UpdateMovement(deltaTimeMs);
                            this.flip = MovementInfo.FlipX;
                        }
                        else
                        {
                            // Most mob attacks are stationary until their queued attack entry fires.
                            MovementInfo.Stop();
                        }
                    }

                    UpdateAnimationAction(); // Update animation to show action

                    // Check if action animation has completed - notify AI to transition back to chase
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

            if (AIEnabled
                && AI?.IsEscortMob == true
                && _escortFollowActive
                && playerX.HasValue
                && playerY.HasValue
                && TryApplyEscortFollow(deltaTimeMs, currentFrameIndex, frameCount, playerX.Value, playerY.Value))
            {
                return;
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

        private bool TryApplyEscortFollow(int deltaTimeMs, int currentFrameIndex, int frameCount, float targetX, float targetY)
        {
            if (MovementInfo == null || MovementInfo.IsInKnockback || MovementInfo.MoveType == MobMoveType.Fly)
            {
                return false;
            }

            const float StopDistanceX = 24f;
            const float StopDistanceY = 45f;

            float dx = targetX - MovementInfo.X;
            float dy = Math.Abs(targetY - MovementInfo.Y);

            if (Math.Abs(dx) <= StopDistanceX && dy <= StopDistanceY)
            {
                MovementInfo.Stop();
                UpdateAnimationAction();
                return true;
            }

            MovementInfo.ForceDirection(
                dx >= 0f ? MobMoveDirection.Right : MobMoveDirection.Left,
                currentFrameIndex,
                frameCount);
            MovementInfo.SetSpeedMultiplier(1.1f);
            MovementInfo.Resume();
            MovementInfo.UpdateMovement(deltaTimeMs);
            this.flip = MovementInfo.FlipX;
            UpdateAnimationAction();
            return true;
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
                targetAction = AnimationKeys.ResolveMobDeathAction(_animationSet);
                SetAction(targetAction);
                return;
            }

            if (AIEnabled && AI != null && AI.State == MobAIState.Hit)
            {
                targetAction = AnimationKeys.ResolveMobHitAction(_animationSet);
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
                    targetAction = AI.State == MobAIState.Skill ? "skill1" :
                                   AI.State == MobAIState.Attack ? "attack1" : "stand";

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
            if (AI?.IsDoomed == true)
            {
                IDXObject doomFrame = GetCurrentDoomAnimationFrame(tickCount);
                if (doomFrame != null)
                {
                    return doomFrame;
                }
            }

            if (_animationController == null)
                return null;

            // Update the animation controller's frame
            _animationController.UpdateFrame(tickCount);

            return _animationController.GetCurrentFrame();
        }

        private IReadOnlyList<IDXObject> GetCurrentAnimationFrames()
        {
            if (AI?.IsDoomed == true)
            {
                IReadOnlyList<IDXObject> doomFrames = ResolveDoomAnimationFrames();
                if (doomFrames != null && doomFrames.Count > 0)
                {
                    return doomFrames;
                }
            }

            return _animationController?.CurrentFrames;
        }

        private MobAnimationSet.FrameMetadata GetCurrentAnimationFrameMetadata()
        {
            if (AI?.IsDoomed == true || _animationController == null)
            {
                return null;
            }

            return _animationSet?.GetFrameMetadata(
                _animationController.CurrentAction,
                _animationController.CurrentFrameIndex);
        }

        private Rectangle TranslateMobBodyBoundsToWorld(Rectangle bodyBounds)
        {
            if (flip)
            {
                bodyBounds = new Rectangle(
                    -(bodyBounds.X + bodyBounds.Width),
                    bodyBounds.Y,
                    bodyBounds.Width,
                    bodyBounds.Height);
            }

            return new Rectangle(
                CurrentX + bodyBounds.X,
                CurrentY + bodyBounds.Y,
                bodyBounds.Width,
                bodyBounds.Height);
        }

        private IReadOnlyList<IDXObject> ResolveDoomAnimationFrames()
        {
            if (_doomAnimationSet == null)
            {
                return null;
            }

            string currentAction = _animationController?.CurrentAction;
            string doomAction = currentAction?.ToLowerInvariant() switch
            {
                AnimationKeys.Die1 or AnimationKeys.Die2 or AnimationKeys.Die => AnimationKeys.Die1,
                AnimationKeys.Hit1 or AnimationKeys.Hit2 or AnimationKeys.Hit => AnimationKeys.Hit1,
                "move" or "walk" or "fly" or "jump" or "chase" => "move",
                _ => AnimationKeys.Stand
            };

            return _doomAnimationSet.GetFrames(doomAction);
        }

        private IDXObject GetCurrentDoomAnimationFrame(int tickCount)
        {
            IReadOnlyList<IDXObject> doomFrames = ResolveDoomAnimationFrames();
            if (doomFrames == null || doomFrames.Count == 0)
            {
                return null;
            }

            if (doomFrames.Count == 1)
            {
                return doomFrames[0];
            }

            if ((_animationController?.CurrentAction?.StartsWith("die", StringComparison.OrdinalIgnoreCase)).GetValueOrDefault())
            {
                return doomFrames[doomFrames.Count - 1];
            }

            return GetTimedAnimationFrame(doomFrames, tickCount, 0, loop: true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float GetSpawnAlpha(int tickCount)
        {
            if (!_isSpawnFading)
            {
                return 1f;
            }

            int elapsed = Math.Max(0, unchecked(tickCount - _spawnFadeStartTick));
            if (elapsed >= SPAWN_FADE_DURATION_MS)
            {
                _isSpawnFading = false;
                return 1f;
            }

            return Math.Clamp((float)elapsed / SPAWN_FADE_DURATION_MS, 0f, 1f);
        }

        private Color GetStatusTint(int tickCount)
        {
            if (AI == null || AI.StatusEffects == MobStatusEffect.None)
            {
                return Color.White;
            }

            float pulse = 0.78f + (float)((Math.Sin(tickCount / 140.0) + 1.0) * 0.09);
            if (AI.HasStatusEffect(MobStatusEffect.Freeze))
            {
                return new Color(170, 225, 255) * pulse;
            }

            if (AI.HasStatusEffect(MobStatusEffect.Poison) || AI.HasStatusEffect(MobStatusEffect.Venom))
            {
                return new Color(150, 255, 160) * pulse;
            }

            if (AI.HasStatusEffect(MobStatusEffect.Burned))
            {
                return new Color(255, 200, 120) * pulse;
            }

            if (AI.HasStatusEffect(MobStatusEffect.Stun) || AI.HasStatusEffect(MobStatusEffect.Seal))
            {
                return new Color(220, 180, 255) * pulse;
            }

            if (AI.HasStatusEffect(MobStatusEffect.Blind) || AI.HasStatusEffect(MobStatusEffect.Darkness))
            {
                return new Color(95, 95, 145) * pulse;
            }

            if (AI.HasStatusEffect(MobStatusEffect.Web) || AI.HasStatusEffect(MobStatusEffect.Weakness))
            {
                return new Color(220, 220, 220) * pulse;
            }

            if (AI.HasStatusEffect(MobStatusEffect.Doom))
            {
                return new Color(210, 190, 255) * pulse;
            }

            if (AI.HasStatusEffect(MobStatusEffect.Hypnotize))
            {
                return new Color(255, 170, 230) * pulse;
            }

            if (AI.HasStatusEffect(MobStatusEffect.Ambush) || AI.HasStatusEffect(MobStatusEffect.Neutralise))
            {
                return new Color(255, 205, 170) * pulse;
            }

            if (AI.HasStatusEffect(MobStatusEffect.PImmune) ||
                AI.HasStatusEffect(MobStatusEffect.MImmune) ||
                AI.HasStatusEffect(MobStatusEffect.Reflect))
            {
                return new Color(255, 245, 190) * pulse;
            }

            if (AI.HasStatusEffect(MobStatusEffect.Showdown) ||
                AI.HasStatusEffect(MobStatusEffect.SealSkill) ||
                AI.HasStatusEffect(MobStatusEffect.Rich))
            {
                return new Color(255, 235, 150) * pulse;
            }

            return Color.White;
        }

        private void UpdateAngerGaugeVisualState(int tickCount)
        {
            if (AI?.HasAngerGauge != true)
            {
                _lastAngerChargeCount = -1;
                _angerGaugeEffectStartTick = int.MinValue;
                return;
            }

            int currentChargeCount = AI.AngerChargeCount;
            if (currentChargeCount == _lastAngerChargeCount)
            {
                return;
            }

            _angerGaugeLoopStartTick = tickCount;
            if (currentChargeCount >= AI.AngerChargeTarget &&
                _lastAngerChargeCount >= 0 &&
                currentChargeCount > _lastAngerChargeCount)
            {
                _angerGaugeEffectStartTick = tickCount;
            }

            _lastAngerChargeCount = currentChargeCount;
        }

        private static IDXObject GetTimedAnimationFrame(IReadOnlyList<IDXObject> frames, int tickCount, int startTick, bool loop)
        {
            if (frames == null || frames.Count == 0)
            {
                return null;
            }

            if (frames.Count == 1)
            {
                return frames[0];
            }

            int elapsed = Math.Max(0, unchecked(tickCount - startTick));
            int totalDuration = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                totalDuration += Math.Max(10, frames[i]?.Delay ?? 100);
            }

            if (totalDuration <= 0)
            {
                return frames[0];
            }

            if (loop)
            {
                elapsed %= totalDuration;
            }
            else if (elapsed >= totalDuration)
            {
                return null;
            }

            int cursor = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                cursor += Math.Max(10, frames[i]?.Delay ?? 100);
                if (elapsed < cursor)
                {
                    return frames[i];
                }
            }

            return loop ? frames[frames.Count - 1] : null;
        }

        private IDXObject GetCurrentAngerGaugeAnimationFrame(int tickCount)
        {
            if (AI?.HasAngerGauge != true || AI.AngerChargeCount <= 0)
            {
                return null;
            }

            int stageIndex = Math.Clamp(AI.AngerChargeCount - 1, 0, Math.Max(0, AI.AngerChargeTarget - 1));
            List<IDXObject> frames = _animationSet.GetAngerGaugeAnimation(stageIndex);
            return GetTimedAnimationFrame(frames, tickCount, _angerGaugeLoopStartTick, loop: true);
        }

        private IDXObject GetCurrentAngerGaugeEffectFrame(int tickCount)
        {
            if (_angerGaugeEffectStartTick == int.MinValue)
            {
                return null;
            }

            IDXObject frame = GetTimedAnimationFrame(_animationSet.GetAngerGaugeEffect(), tickCount, _angerGaugeEffectStartTick, loop: false);
            if (frame == null)
            {
                _angerGaugeEffectStartTick = int.MinValue;
            }

            return frame;
        }

        private static void DrawOverlayFrame(
            IDXObject frame,
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int adjustedShiftX,
            int shiftCenteredY,
            bool flip,
            float alpha)
        {
            if (frame == null)
            {
                return;
            }

            if (alpha < 1f)
            {
                frame.DrawBackground(
                    sprite,
                    skeletonMeshRenderer,
                    gameTime,
                    frame.X - adjustedShiftX,
                    frame.Y - shiftCenteredY,
                    Color.White * alpha,
                    flip,
                    null);
                return;
            }

            frame.DrawObject(sprite, skeletonMeshRenderer, gameTime, adjustedShiftX, shiftCenteredY, flip, null);
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
                IReadOnlyList<IDXObject> currentFrames = GetCurrentAnimationFrames();
                if (currentFrames != null && currentFrames.Count > 0 && flip)
                {
                    IDXObject frame0 = currentFrames[0];
                    int originAdjustX = frame0.X - drawFrame.X;
                    adjustedShiftX = shiftCenteredX - originAdjustX;
                }

                if (IsFrameWithinView(drawFrame, adjustedShiftX, shiftCenteredY,
                    renderParameters.RenderWidth, renderParameters.RenderHeight))
                {
                    float spawnAlpha = GetSpawnAlpha(TickCount);
                    Color statusTint = GetStatusTint(TickCount);
                    if (spawnAlpha < 1f || statusTint != Color.White)
                    {
                        drawFrame.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                            drawFrame.X - adjustedShiftX,
                            drawFrame.Y - shiftCenteredY,
                            statusTint * spawnAlpha,
                            flip,
                            drawReflectionInfo);
                    }
                    else
                    {
                        drawFrame.DrawObject(sprite, skeletonMeshRenderer, gameTime,
                            adjustedShiftX, shiftCenteredY,
                            flip,
                            drawReflectionInfo);
                    }

                    IDXObject angerGaugeFrame = GetCurrentAngerGaugeAnimationFrame(TickCount);
                    DrawOverlayFrame(angerGaugeFrame, sprite, skeletonMeshRenderer, gameTime, adjustedShiftX, shiftCenteredY, flip, spawnAlpha);

                    IDXObject angerEffectFrame = GetCurrentAngerGaugeEffectFrame(TickCount);
                    DrawOverlayFrame(angerEffectFrame, sprite, skeletonMeshRenderer, gameTime, adjustedShiftX, shiftCenteredY, flip, spawnAlpha);
                }
            }

            // Draw name tooltip
            if (_nameTooltip != null && !_isSpawnFading)
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
