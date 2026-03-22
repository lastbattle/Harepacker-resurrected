using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HaCreator.MapSimulator.Core;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.AI
{
    /// <summary>
    /// Mob AI state machine - controls mob behavior, combat, and interactions.
    /// Based on MapleStory client CMob structures.
    /// </summary>
    public enum MobAIState
    {
        Idle,           // Standing still, no target
        Patrol,         // Walking around patrol area (rx0-rx1)
        Alert,          // Detected player, waiting before aggro
        Chase,          // Actively pursuing target
        Attack,         // Executing attack animation/action
        Skill,          // Using a skill
        Hit,            // Being hit, stunned
        Death,          // Playing death animation
        Removed         // Mob has been removed from pool
    }

    /// <summary>
    /// Mob status effects - based on CMob::MobStat from client
    /// </summary>
    [Flags]
    public enum MobStatusEffect : long
    {
        None = 0,
        PADamage = 1L << 0,         // Physical Attack Damage up
        PDamage = 1L << 1,          // Physical Damage reduction
        MADamage = 1L << 2,         // Magic Attack Damage up
        MDamage = 1L << 3,          // Magic Damage reduction
        ACC = 1L << 4,              // Accuracy up
        EVA = 1L << 5,              // Evasion up
        Speed = 1L << 6,            // Movement speed
        Stun = 1L << 7,             // Stunned (cannot act)
        Freeze = 1L << 8,           // Frozen (cannot move or attack)
        Poison = 1L << 9,           // Poison (DoT)
        Seal = 1L << 10,            // Sealed (cannot use skills)
        Darkness = 1L << 11,        // Darkness (reduced accuracy)
        PowerUp = 1L << 12,         // Power up (attack boost)
        MagicUp = 1L << 13,         // Magic up (magic boost)
        PGuardUp = 1L << 14,        // Physical Guard up
        MGuardUp = 1L << 15,        // Magic Guard up
        Doom = 1L << 16,            // Doomed (transformed)
        Web = 1L << 17,             // Webbed (slowed)
        PImmune = 1L << 18,         // Physical Immune
        MImmune = 1L << 19,         // Magic Immune
        HardSkin = 1L << 20,        // Hard Skin (damage reduction)
        Ambush = 1L << 21,          // Ambush (increased damage)
        Dazzle = 1L << 22,          // Dazzled (confused, attacks own side)
        Venom = 1L << 23,           // Venom (stronger poison DoT)
        Blind = 1L << 24,           // Blind (cannot see target)
        SealSkill = 1L << 25,       // Skill Seal (cannot use specific skill)
        Burned = 1L << 26,          // Burning (fire DoT)
        Reflect = 1L << 27,         // Damage Reflect
        Showdown = 1L << 28,        // Showdown (marked for extra EXP)
        Weakness = 1L << 29,        // Weakness (reduced stats)
        Neutralise = 1L << 30,      // Neutralise (reduced magic defense)
        Hypnotize = 1L << 31,       // Hypnotized (attacks allies)
    }

    /// <summary>
    /// Mob controller type - determines who controls mob AI
    /// </summary>
    public enum MobControllerType
    {
        None,           // No controller (static)
        Local,          // Controlled locally (client-side)
        Remote,         // Controlled by another client
        Server          // Controlled by server
    }

    /// <summary>
    /// Puppet/Summon info for aggro targeting
    /// </summary>
    public class PuppetInfo
    {
        public int ObjectId { get; set; }
        public int SummonSlotIndex { get; set; } = -1;
        public float X { get; set; }
        public float Y { get; set; }
        public Rectangle Hitbox { get; set; }
        public bool IsGrounded { get; set; }
        public int OwnerId { get; set; }
        public int AggroValue { get; set; }
        public int ExpirationTime { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public enum MobTargetType
    {
        Player = 0,
        Summoned = 1,
        Mob = 3
    }

    /// <summary>
    /// Mob death type - from CMobPool::Update switch statement
    /// </summary>
    public enum MobDeathType
    {
        Normal = 0,     // OnDie - standard death
        Killed = 1,     // OnDie - killed by player
        Bomb = 2,       // OnBomb - explosion death
        Miss = 3,       // OnDestructByMiss - disappeared
        Swallowed = 4,  // OnSwallowed - eaten by another mob
        Timeout = 5     // OnDie - timed out
    }

    /// <summary>
    /// Attack entry structure - from CMob::ATTACKENTRY
    /// </summary>
    public class MobAttackEntry
    {
        public int AttackId { get; set; }           // Attack index (attack1, attack2, etc.)
        public int AttackType { get; set; } = -1;  // info/attack/N/type or attackN/info/type
        public int Damage { get; set; }             // Base damage
        public int Range { get; set; }              // Attack range in pixels
        public int Delay { get; set; }              // Delay before damage applies (ms)
        public int Cooldown { get; set; }           // Cooldown between uses (ms)
        public bool IsRanged { get; set; }          // True for projectile attacks
        public bool IsAreaOfEffect { get; set; }    // True for AoE attacks
        public int EffectAfter { get; set; }        // Effect delay (tEffectAfter)
        public int AttackAfter { get; set; }        // Attack delay (tAttackAfter)
        public string AnimationName { get; set; }   // Animation to play (e.g., "attack1")
        public int BulletSpeed { get; set; }        // Projectile speed from Mob.wz info/attack
        public int ProjectileCount { get; set; }    // Multi-ball count for boss/projectile attacks
        public int AreaWidth { get; set; }          // Ground/AoE attack width
        public int AreaHeight { get; set; }         // Ground/AoE attack height
        public int RandomDelayWindow { get; set; }  // Additional delay window for area attacks
        public bool HasRangeBounds { get; set; }    // True when attackN/info/range exists
        public int RangeLeft { get; set; }          // Relative left bound from attack info
        public int RangeTop { get; set; }           // Relative top bound from attack info
        public int RangeRight { get; set; }         // Relative right bound from attack info
        public int RangeBottom { get; set; }        // Relative bottom bound from attack info
        public int AreaCount { get; set; }          // Number of possible area slots from attackN/info/range/areaCount
        public int AttackCount { get; set; }        // Number of chosen area slots from attackN/info/range/attackCount
        public int StartOffset { get; set; }        // Starting slot offset from attackN/info/range/start
        public bool IsRushAttack { get; set; }      // attackN/info/rush or info/attack/N/rush
        public bool IsJumpAttack { get; set; }      // attackN/info/jumpAttack or info/attack/N/jumpAttack
        public bool Tremble { get; set; }           // attackN/info/tremble or info/attack/N/tremble
        public bool IsAngerAttack { get; set; }     // attackN/info/AngerAttack
        public int DiseaseSkillId { get; set; }     // info/attack/N/disease -> MobSkill.img id
        public int DiseaseLevel { get; set; }       // info/attack/N/level -> MobSkill.img level

        // Runtime state
        public int LastUseTime { get; set; }        // Tick when last used

        public bool IsOnCooldown(int currentTick)
        {
            return currentTick - LastUseTime < Cooldown;
        }
    }

    /// <summary>
    /// Mob skill entry structure - from Mob.wz info/skill entries
    /// Links to MobSkill.img for "affected" animations to play on the player when hit.
    /// </summary>
    public class MobSkillEntry
    {
        /// <summary>Mob skill ID (e.g., 126 for Slow, 200 for Summon)</summary>
        public int SkillId { get; set; }

        /// <summary>Skill level (affects duration, damage, etc.)</summary>
        public int Level { get; set; }

        /// <summary>Animation action index to use when casting this skill (e.g., 1 for skill1)</summary>
        public int ActionIndex { get; set; }

        /// <summary>Effect after time in ms</summary>
        public int EffectAfter { get; set; }

        /// <summary>Skill after (for multi-phase skills)</summary>
        public int SkillAfter { get; set; }

        /// <summary>Animation name (e.g., "skill1")</summary>
        public string AnimationName { get; set; }

        /// <summary>Range of the skill</summary>
        public int Range { get; set; }

        /// <summary>Cooldown between uses (ms)</summary>
        public int Cooldown { get; set; }

        /// <summary>Original WZ slot index from Mob.wz info/skill</summary>
        public int SourceIndex { get; set; } = -1;

        /// <summary>Priority hint from Mob.wz info/skill/priority</summary>
        public int Priority { get; set; }

        /// <summary>Prerequisite WZ slot index from preSkillIndex</summary>
        public int PreSkillIndex { get; set; } = -1;

        /// <summary>Required prerequisite use count from preSkillCount</summary>
        public int PreSkillCount { get; set; }

        /// <summary>True when the skill is marked onlyFsm and should not be chosen by generic AI</summary>
        public bool OnlyFsm { get; set; }

        /// <summary>Global skill lockout duration from skillForbid</summary>
        public int SkillForbid { get; set; }

        /// <summary>Runtime: Last time this skill was used</summary>
        public int LastUseTime { get; set; }

        public bool IsOnCooldown(int currentTick)
        {
            return currentTick - LastUseTime < Cooldown;
        }
    }

    /// <summary>
    /// Target info structure - from CMob::TARGETINFO
    /// </summary>
    public class MobTargetInfo
    {
        public int TargetId { get; set; }           // Target object ID
        public int TargetSlotIndex { get; set; } = -1; // Summon slot index for client-style summoned targeting
        public MobTargetType TargetType { get; set; } = MobTargetType.Player;
        public float TargetX { get; set; }          // Target X position
        public float TargetY { get; set; }          // Target Y position
        public float Distance { get; set; }         // Distance to target
        public bool IsValid { get; set; }           // Target still exists
        public int LastSeenTime { get; set; }       // Last time target was in range

        public MobTargetInfo Clone()
        {
            return new MobTargetInfo
            {
                TargetId = TargetId,
                TargetSlotIndex = TargetSlotIndex,
                TargetType = TargetType,
                TargetX = TargetX,
                TargetY = TargetY,
                Distance = Distance,
                IsValid = IsValid,
                LastSeenTime = LastSeenTime
            };
        }
    }

    /// <summary>
    /// Damage info structure - for displaying damage numbers
    /// </summary>
    public class MobDamageInfo
    {
        public int Damage { get; set; }             // Damage amount
        public bool IsCritical { get; set; }        // Critical hit
        public int DisplayTime { get; set; }        // When to start displaying
        public int Duration { get; set; } = 1000;   // How long to display (ms)
        public float OffsetX { get; set; }          // Random offset for stacking
        public float OffsetY { get; set; }          // Vertical offset (rises up)
        public Color Color { get; set; } = Color.White;

        public bool IsExpired(int currentTick) => currentTick - DisplayTime > Duration;
    }

    public class MobStatusEntry
    {
        public MobStatusEffect Effect { get; set; }
        public int ExpirationTime { get; set; }
        public int Value { get; set; }
        public int SecondaryValue { get; set; }
        public int TertiaryValue { get; set; }
        public int TickIntervalMs { get; set; }
        public int NextTickTime { get; set; }
        public int SourceSkillId { get; set; }
    }

    public enum MobDamageType
    {
        Physical,
        Magical
    }

    /// <summary>
    /// Mob AI controller - handles state machine, combat, and behavior
    /// </summary>
    public class MobAI
    {
        #region Constants
        private const int DEFAULT_AGGRO_RANGE = 200;        // Pixels to detect player
        private const int DEFAULT_ATTACK_RANGE = 50;        // Melee attack range
        private const int DEFAULT_CHASE_SPEED_MULT = 2;     // Speed multiplier when chasing
        private const int ALERT_DURATION = 500;             // Time in alert before chase (ms)
        private const int ATTACK_COOLDOWN = 1500;           // Default attack cooldown (ms)
        private const int HIT_STUN_DURATION = 300;          // Stun duration when hit (ms)
        private const int DEATH_DURATION = 1000;            // Death animation duration (ms)
        private const int LOSE_AGGRO_TIME = 5000;           // Time to lose aggro if no LOS (ms)
        private const int MAX_DAMAGE_DISPLAYS = 10;         // Max damage numbers shown
        #endregion

        #region State
        private MobAIState _state = MobAIState.Idle;
        private MobAIState _previousState = MobAIState.Idle;
        private int _stateStartTime = 0;
        private int _stateTimer = 0;

        // Combat
        private readonly List<MobAttackEntry> _attacks = new List<MobAttackEntry>();
        private readonly List<MobSkillEntry> _skills = new List<MobSkillEntry>();
        private int _currentAttackIndex = -1;
        private int _currentSkillIndex = -1;
        private MobTargetInfo _target = new MobTargetInfo();
        private readonly List<MobDamageInfo> _damageDisplays = new List<MobDamageInfo>();
        private readonly Dictionary<int, int> _skillUseCounts = new Dictionary<int, int>();
        private int _skillForbidUntil = 0;
        private bool _actionAnimationCompleted;
        private int _actionRecoveryUntil;

        // Stats
        private int _maxHp = 100;
        private int _currentHp = 100;
        private int _level = 1;
        private int _exp = 0;
        private bool _isBoss = false;
        private bool _isUndead = false;

        // Aggro
        private int _aggroRange = DEFAULT_AGGRO_RANGE;
        private int _attackRange = DEFAULT_ATTACK_RANGE;
        private float _chaseSpeedMultiplier = DEFAULT_CHASE_SPEED_MULT;
        private bool _isAggroed = false;  // True if mob has been hit and should chase
        private bool _autoAggro = false;  // True if mob auto-aggros when player enters range
        private bool _canTargetPlayer = true;
        private bool _isEscortMob = false;

        // Boss aggro timeout tracking
        private int _bossAggroStartTime = 0;      // When boss first aggroed (0 = never aggroed)
        private bool _bossAggroTimedOut = false;  // True if boss has timed out on aggro

        // Death
        private MobDeathType _deathType = MobDeathType.Normal;
        private int _deathTime = 0;

        // Special mob interactions
        private int _selfDestructHpThreshold = -1;
        private int _selfDestructAction = -1;
        private int _selfDestructRemoveAfterMs = -1;
        private int _removeAfterMs = -1;
        private int _specialSpawnTime = int.MinValue;
        private bool _selfDestructTriggered = false;
        private bool _selfDestructPending = false;
        private bool _hasAngerGauge = false;
        private int _angerChargeTarget = 0;
        private int _angerChargeCount = 0;
        private int _angerAttackIndex = -1;

        // Status effects
        private MobStatusEffect _statusEffects = MobStatusEffect.None;
        private readonly Dictionary<MobStatusEffect, MobStatusEntry> _statusEntries = new Dictionary<MobStatusEffect, MobStatusEntry>();

        // Controller
        private MobControllerType _controllerType = MobControllerType.Local;
        private int _controllerId = 0;

        // Guided arrow targeting (for skills like Guided Arrow)
        private int _guidedTargetId = 0;
        private bool _isGuidedTarget = false;
        #endregion

        #region Properties
        public MobAIState State => _state;
        public MobAIState PreviousState => _previousState;
        public int CurrentHp => _currentHp;
        public int MaxHp => _maxHp;
        public float HpPercent => _maxHp > 0 ? (float)_currentHp / _maxHp : 0;
        public bool IsDead => _state == MobAIState.Death || _state == MobAIState.Removed;
        public bool IsBoss => _isBoss;
        public bool IsUndead => _isUndead;
        public int Level => _level;
        public int Exp => _exp;
        public int AggroRange => _aggroRange;
        public int AttackRange => _attackRange;
        public MobTargetInfo Target => _target;
        public IReadOnlyList<MobDamageInfo> DamageDisplays => _damageDisplays;
        public MobDeathType DeathType => _deathType;
        public int StateElapsed(int currentTick) => currentTick - _stateStartTime;
        public IReadOnlyDictionary<MobStatusEffect, MobStatusEntry> StatusEntries => _statusEntries;
        public bool IsEscortMob => _isEscortMob;
        public bool CanTargetPlayer => CanTargetPlayerNow;
        public bool IsTargetingSummoned => _target.IsValid && _target.TargetType == MobTargetType.Summoned;
        public bool IsTargetingMob => _target.IsValid && _target.TargetType == MobTargetType.Mob;
        public bool HasAngerGauge => _hasAngerGauge && _angerChargeTarget > 0;
        public int AngerChargeTarget => _angerChargeTarget;
        public int AngerChargeCount => _angerChargeCount;
        public bool IsAngerCharged => HasAngerGauge && _angerChargeCount >= _angerChargeTarget;

        // Status effect properties
        public MobStatusEffect StatusEffects => _statusEffects;
        public bool IsDazzled => HasStatusEffect(MobStatusEffect.Dazzle);
        public bool IsStunned => HasStatusEffect(MobStatusEffect.Stun);
        public bool IsFrozen => HasStatusEffect(MobStatusEffect.Freeze);
        public bool IsPoisoned => HasStatusEffect(MobStatusEffect.Poison);
        public bool IsSealed => HasStatusEffect(MobStatusEffect.Seal);
        public bool IsDoomed => HasStatusEffect(MobStatusEffect.Doom);
        public bool IsHypnotized => HasStatusEffect(MobStatusEffect.Hypnotize);

        // Controller properties
        public MobControllerType ControllerType => _controllerType;
        public int ControllerId => _controllerId;
        public bool IsLocalController => _controllerType == MobControllerType.Local;
        public bool IsRemoteController => _controllerType == MobControllerType.Remote;

        // Guided arrow targeting
        public int GuidedTargetId => _guidedTargetId;
        public bool IsGuidedTarget => _isGuidedTarget;

        /// <summary>
        /// Whether this mob auto-aggros when player enters range (vs only chasing after being hit)
        /// </summary>
        public bool AutoAggro => _autoAggro;

        /// <summary>
        /// True if this boss has timed out on aggro and will no longer chase the player.
        /// Only applies to boss monsters after BOSS_AGGRO_TIMEOUT has elapsed.
        /// </summary>
        public bool BossAggroTimedOut => _bossAggroTimedOut;
        #endregion

        #region Initialization
        public void Initialize(int maxHp, int level = 1, int exp = 0, bool isBoss = false, bool isUndead = false, bool autoAggro = false)
        {
            _maxHp = maxHp;
            _currentHp = maxHp;
            _level = level;
            _exp = exp;
            _isBoss = isBoss;
            _isUndead = isUndead;
            _autoAggro = autoAggro;  // Determined by Mob.wz firstAttack property
            _canTargetPlayer = true;
            _isEscortMob = false;
            _selfDestructHpThreshold = -1;
            _selfDestructAction = -1;
            _selfDestructRemoveAfterMs = -1;
            _removeAfterMs = -1;
            _specialSpawnTime = int.MinValue;
            _selfDestructTriggered = false;
            _selfDestructPending = false;
            _hasAngerGauge = false;
            _angerChargeTarget = 0;
            _angerChargeCount = 0;
            _angerAttackIndex = -1;

            // Bosses have larger aggro range
            if (isBoss)
            {
                _aggroRange = DEFAULT_AGGRO_RANGE * 2;
                _attackRange = DEFAULT_ATTACK_RANGE * 2;
                // Note: autoAggro is determined by firstAttack from Mob.wz, not forced for bosses
            }
        }

        public void ConfigureSpecialBehavior(
            bool canTargetPlayer,
            bool isEscortMob,
            int selfDestructHpThreshold = -1,
            int selfDestructAction = -1,
            int selfDestructRemoveAfterMs = -1,
            int removeAfterMs = -1)
        {
            _canTargetPlayer = canTargetPlayer;
            _isEscortMob = isEscortMob;
            _selfDestructHpThreshold = selfDestructHpThreshold;
            _selfDestructAction = selfDestructAction;
            _selfDestructRemoveAfterMs = selfDestructRemoveAfterMs;
            _removeAfterMs = removeAfterMs;
            _specialSpawnTime = int.MinValue;
            _selfDestructTriggered = false;
            _selfDestructPending = false;

            if (!canTargetPlayer)
            {
                _target.IsValid = false;
                _target.Distance = float.MaxValue;
                _isAggroed = false;
            }
        }

        /// <summary>
        /// Set auto-aggro behavior
        /// </summary>
        public void SetAutoAggro(bool autoAggro)
        {
            _autoAggro = autoAggro;
        }

        public void AddAttack(MobAttackEntry attack)
        {
            if (attack?.IsAngerAttack == true)
            {
                _angerAttackIndex = _attacks.Count;
            }

            _attacks.Add(attack);
        }

        public void ConfigureAngerGauge(bool hasAngerGauge, int chargeTarget)
        {
            _hasAngerGauge = hasAngerGauge && chargeTarget > 0;
            _angerChargeTarget = _hasAngerGauge ? chargeTarget : 0;
            _angerChargeCount = 0;
        }

        public void AddAttack(int attackId, string animName, int damage, int range, int cooldown = 1500, bool isRanged = false)
        {
            _attacks.Add(new MobAttackEntry
            {
                AttackId = attackId,
                AnimationName = animName,
                Damage = damage,
                Range = range,
                Cooldown = cooldown,
                IsRanged = isRanged,
                Delay = 200,  // Default delay before damage
                AttackAfter = 100
            });
        }

        public void SetAggroRange(int range) => _aggroRange = range;
        public void SetAttackRange(int range) => _attackRange = range;
        public void SetChaseSpeedMultiplier(float mult) => _chaseSpeedMultiplier = mult;

        /// <summary>
        /// Add a mob skill entry from Mob.wz info/skill data.
        /// These link to MobSkill.img for "affected" animations.
        /// </summary>
        public void AddSkill(MobSkillEntry skill)
        {
            _skills.Add(skill);
        }

        /// <summary>
        /// Add a mob skill entry (simplified version)
        /// </summary>
        public void AddSkill(int skillId, int level, int actionIndex, int range = 300, int cooldown = 5000)
        {
            _skills.Add(new MobSkillEntry
            {
                SkillId = skillId,
                Level = level,
                ActionIndex = actionIndex,
                AnimationName = $"skill{actionIndex}",
                Range = range,
                Cooldown = cooldown
            });
        }
        #endregion

        #region State Management
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetState(MobAIState newState, int currentTick)
        {
            if (_state == newState)
                return;

            _previousState = _state;
            _state = newState;
            _stateStartTime = currentTick;
            _stateTimer = 0;

            // Reset attack when leaving attack state
            if (_previousState == MobAIState.Attack)
            {
                _currentAttackIndex = -1;
            }

            // Reset skill when leaving skill state
            if (_previousState == MobAIState.Skill)
            {
                _currentSkillIndex = -1;
            }

            if (newState != MobAIState.Attack && newState != MobAIState.Skill)
            {
                _actionAnimationCompleted = false;
                _actionRecoveryUntil = 0;
            }
        }

        public void Update(int currentTick, float mobX, float mobY, float? playerX, float? playerY)
        {
            // Update damage displays (remove expired)
            _damageDisplays.RemoveAll(d => d.IsExpired(currentTick));

            // Don't update AI if dead
            if (_state == MobAIState.Death || _state == MobAIState.Removed)
            {
                UpdateDeathState(currentTick);
                return;
            }

            UpdateStatusEffects(currentTick);
            if (IsDead)
            {
                return;
            }

            UpdateSpecialInteractions(currentTick);
            if (IsDead)
            {
                return;
            }

            // Boss aggro timeout check - bosses become passive after timeout
            // as long as player is still around (even if dead)
            if (_isBoss && !_bossAggroTimedOut && _bossAggroStartTime > 0 && playerX.HasValue)
            {
                if (currentTick - _bossAggroStartTime >= GameConstants.BOSS_AGGRO_TIMEOUT)
                {
                    _bossAggroTimedOut = true;
                    _isAggroed = false;
                    _autoAggro = false;  // Stop auto-aggroing

                    // Return to idle/patrol state
                    if (_state == MobAIState.Chase || _state == MobAIState.Alert || _state == MobAIState.Attack || _state == MobAIState.Skill)
                    {
                        SetState(MobAIState.Patrol, currentTick);
                    }
                }
            }

            // Update target info
            UpdateTarget(currentTick, mobX, mobY, playerX, playerY);

            // State machine
            switch (_state)
            {
                case MobAIState.Idle:
                    UpdateIdleState(currentTick);
                    break;

                case MobAIState.Patrol:
                    UpdatePatrolState(currentTick);
                    break;

                case MobAIState.Alert:
                    UpdateAlertState(currentTick);
                    break;

                case MobAIState.Chase:
                    UpdateChaseState(currentTick, mobX, mobY);
                    break;

                case MobAIState.Attack:
                    UpdateAttackState(currentTick);
                    break;

                case MobAIState.Skill:
                    UpdateSkillState(currentTick);
                    break;

                case MobAIState.Hit:
                    UpdateHitState(currentTick);
                    break;
            }
        }

        private void UpdateTarget(int currentTick, float mobX, float mobY, float? playerX, float? playerY)
        {
            if (_target.IsValid && _target.TargetType != MobTargetType.Player)
            {
                float externalDx = _target.TargetX - mobX;
                float externalDy = _target.TargetY - mobY;
                _target.Distance = MathF.Sqrt(externalDx * externalDx + externalDy * externalDy);
                _target.LastSeenTime = currentTick;
                return;
            }

            if (!CanTargetPlayerNow)
            {
                _target.IsValid = false;
                _target.Distance = float.MaxValue;
                return;
            }

            if (!playerX.HasValue || !playerY.HasValue)
            {
                // If aggroed, keep tracking the last known target position
                // Only invalidate if not aggroed
                if (!_isAggroed)
                {
                    _target.IsValid = false;
                }
                else
                {
                    // Recalculate distance to last known target position
                    float dx = _target.TargetX - mobX;
                    float dy = _target.TargetY - mobY;
                    _target.Distance = MathF.Sqrt(dx * dx + dy * dy);
                }
                return;
            }

            float dxNew = playerX.Value - mobX;
            float dyNew = playerY.Value - mobY;
            float distance = MathF.Sqrt(dxNew * dxNew + dyNew * dyNew);

            _target.TargetX = playerX.Value;
            _target.TargetY = playerY.Value;
            _target.Distance = distance;
            _target.IsValid = true;
            _target.TargetId = 0;
            _target.TargetType = MobTargetType.Player;
            _target.LastSeenTime = currentTick;
        }
        #endregion

        #region State Updates
        private void UpdateIdleState(int currentTick)
        {
            // Boss aggro timeout - don't aggro if timed out
            if (_bossAggroTimedOut)
            {
                // Transition to patrol after idle time
                if (StateElapsed(currentTick) > 2000)
                {
                    SetState(MobAIState.Patrol, currentTick);
                }
                return;
            }

            // Check for aggro - only if autoAggro is enabled OR mob was already hit (isAggroed)
            if (_target.IsValid && _target.Distance <= _aggroRange && (_autoAggro || _isAggroed))
            {
                // Track boss aggro start time
                if (_isBoss && _bossAggroStartTime == 0)
                {
                    _bossAggroStartTime = currentTick;
                }
                SetState(MobAIState.Alert, currentTick);
                return;
            }

            // Transition to patrol after idle time
            if (StateElapsed(currentTick) > 2000)
            {
                SetState(MobAIState.Patrol, currentTick);
            }
        }

        private void UpdatePatrolState(int currentTick)
        {
            // Boss aggro timeout - don't aggro if timed out
            if (_bossAggroTimedOut)
            {
                // Continue patrolling passively
                return;
            }

            // Check for aggro - only if autoAggro is enabled OR mob was already hit (isAggroed)
            if (_target.IsValid && _target.Distance <= _aggroRange && (_autoAggro || _isAggroed))
            {
                // Track boss aggro start time
                if (_isBoss && _bossAggroStartTime == 0)
                {
                    _bossAggroStartTime = currentTick;
                }
                SetState(MobAIState.Alert, currentTick);
                return;
            }

            // Continue patrolling (movement handled by MobMovementInfo)
        }

        private void UpdateAlertState(int currentTick)
        {
            // Lost target
            if (!_target.IsValid || _target.Distance > _aggroRange * 1.5f)
            {
                SetState(MobAIState.Patrol, currentTick);
                return;
            }

            // After alert duration, start chase
            if (StateElapsed(currentTick) > ALERT_DURATION)
            {
                SetState(MobAIState.Chase, currentTick);
            }
        }

        private void UpdateChaseState(int currentTick, float mobX, float mobY)
        {
            // Lost target for too long - aggroed mobs chase longer
            int loseAggroTime = _isAggroed ? LOSE_AGGRO_TIME * 2 : LOSE_AGGRO_TIME;
            if (!_target.IsValid || currentTick - _target.LastSeenTime > loseAggroTime)
            {
                _isAggroed = false;  // Clear aggro when giving up
                SetState(MobAIState.Patrol, currentTick);
                return;
            }

            int availableSkillIndex = FindAvailableSkillIndex(currentTick);
            int availableAttackIndex = FindAvailableAttackIndex(currentTick);

            if (availableSkillIndex >= 0 &&
                ShouldPreferSkill(_skills[availableSkillIndex], availableAttackIndex >= 0 ? _attacks[availableAttackIndex] : null))
            {
                StartSkill(availableSkillIndex, currentTick);
                return;
            }

            if (availableAttackIndex >= 0)
            {
                StartAttack(availableAttackIndex, currentTick);
            }
        }

        private void UpdateAttackState(int currentTick)
        {
            if (_currentAttackIndex < 0 || _currentAttackIndex >= _attacks.Count)
            {
                SetState(MobAIState.Chase, currentTick);
                return;
            }

            if (_actionAnimationCompleted && currentTick >= _actionRecoveryUntil)
            {
                CompleteCurrentCombatAction(currentTick);
            }
        }

        private void UpdateSkillState(int currentTick)
        {
            if (_currentSkillIndex < 0 || _currentSkillIndex >= _skills.Count)
            {
                SetState(MobAIState.Chase, currentTick);
                return;
            }

            if (_actionAnimationCompleted && currentTick >= _actionRecoveryUntil)
            {
                CompleteCurrentCombatAction(currentTick);
            }
        }

        /// <summary>
        /// Called by MobItem when the attack animation has completed.
        /// This allows the AI to transition out of Attack state only after
        /// the full animation has played.
        /// </summary>
        public void NotifyAttackAnimationComplete(int currentTick)
        {
            if (_state == MobAIState.Attack || _state == MobAIState.Skill)
            {
                _actionAnimationCompleted = true;
                if (currentTick >= _actionRecoveryUntil)
                {
                    CompleteCurrentCombatAction(currentTick);
                }
            }
        }

        private void UpdateHitState(int currentTick)
        {
            if (IsFrozen || IsStunned)
            {
                return;
            }

            // Stun duration over
            if (StateElapsed(currentTick) > HIT_STUN_DURATION)
            {
                // If aggroed (was hit by player), always chase
                if (_isAggroed && _target.IsValid)
                {
                    SetState(MobAIState.Chase, currentTick);
                }
                // If target still valid and in range, chase
                else if (_target.IsValid && _target.Distance <= _aggroRange)
                {
                    SetState(MobAIState.Chase, currentTick);
                }
                else
                {
                    SetState(MobAIState.Patrol, currentTick);
                }
            }
        }

        private void UpdateDeathState(int currentTick)
        {
            if (_state == MobAIState.Death && StateElapsed(currentTick) > DEATH_DURATION)
            {
                SetState(MobAIState.Removed, currentTick);
            }
        }
        #endregion

        #region Combat
        /// <summary>
        /// Apply damage to this mob (simple version without attacker info)
        /// </summary>
        /// <returns>True if mob died from this damage</returns>
        public bool TakeDamage(int damage, int currentTick, bool isCritical = false, MobDamageType damageType = MobDamageType.Physical)
        {
            return TakeDamage(damage, currentTick, isCritical, null, null, damageType);
        }

        /// <summary>
        /// Apply damage to this mob with attacker position for aggro
        /// </summary>
        /// <param name="damage">Damage amount</param>
        /// <param name="currentTick">Current game tick</param>
        /// <param name="isCritical">Was this a critical hit</param>
        /// <param name="attackerX">Attacker X position (for aggro)</param>
        /// <param name="attackerY">Attacker Y position (for aggro)</param>
        /// <returns>True if mob died from this damage</returns>
        public bool TakeDamage(int damage, int currentTick, bool isCritical, float? attackerX, float? attackerY, MobDamageType damageType = MobDamageType.Physical)
        {
            if (IsDead)
                return false;

            damage = CalculateIncomingDamage(damage, damageType);
            LastDamageTaken = damage;
            _currentHp -= damage;

            // Add damage display
            AddDamageDisplay(damage, currentTick, isCritical);

            // AGGRO: When hit, the mob should chase the attacker (unless boss aggro timed out)
            if (CanTargetPlayerNow && attackerX.HasValue && attackerY.HasValue && !_bossAggroTimedOut)
            {
                // Set/update target to attacker position - this triggers aggro
                _target.TargetX = attackerX.Value;
                _target.TargetY = attackerY.Value;
                _target.IsValid = true;
                _target.TargetId = 0;
                _target.TargetType = MobTargetType.Player;
                _target.LastSeenTime = currentTick;

                // Force aggro state - mob will chase after hit stun ends
                _isAggroed = true;

                // Track boss aggro start time
                if (_isBoss && _bossAggroStartTime == 0)
                {
                    _bossAggroStartTime = currentTick;
                }
            }

            // Enter hit state (stun) unless boss or already attacking
            if (!_isBoss && _state != MobAIState.Attack && _state != MobAIState.Skill)
            {
                SetState(MobAIState.Hit, currentTick);
            }
            else if (CanTargetPlayerNow && _isBoss && attackerX.HasValue && !_bossAggroTimedOut)
            {
                // Bosses don't stun but still aggro - go straight to chase
                if (_state == MobAIState.Idle || _state == MobAIState.Patrol)
                {
                    SetState(MobAIState.Chase, currentTick);
                }
            }

            // Check death
            if (_currentHp <= 0)
            {
                _currentHp = 0;
                _deathType = MobDeathType.Killed;
                _deathTime = currentTick;
                SetState(MobAIState.Death, currentTick);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Force aggro on this mob towards a position
        /// </summary>
        public void ForceAggro(
            float targetX,
            float targetY,
            int currentTick,
            int targetId = 0,
            MobTargetType targetType = MobTargetType.Player)
        {
            if (IsDead)
                return;

            if (!CanTargetPlayerNow && targetType == MobTargetType.Player)
                return;

            // Don't aggro if boss aggro has timed out
            if (_bossAggroTimedOut)
                return;

            _target.TargetX = targetX;
            _target.TargetY = targetY;
            _target.IsValid = true;
            _target.TargetId = targetId;
            _target.TargetType = targetType;
            _target.LastSeenTime = currentTick;
            _isAggroed = true;

            // Track boss aggro start time
            if (_isBoss && _bossAggroStartTime == 0)
            {
                _bossAggroStartTime = currentTick;
            }

            // If idle or patrolling, start chasing
            if (_state == MobAIState.Idle || _state == MobAIState.Patrol)
            {
                SetState(MobAIState.Chase, currentTick);
            }
        }

        /// <summary>
        /// Kill the mob immediately
        /// </summary>
        public void Kill(int currentTick, MobDeathType deathType = MobDeathType.Normal)
        {
            _currentHp = 0;
            _deathType = deathType;
            _deathTime = currentTick;
            _selfDestructPending = false;
            SetState(MobAIState.Death, currentTick);
        }

        /// <summary>
        /// Heal the mob
        /// </summary>
        public void Heal(int amount)
        {
            _currentHp = Math.Min(_currentHp + amount, _maxHp);
        }

        /// <summary>
        /// Restore HP to a specific value (used for map state restoration).
        /// Does not trigger any combat effects or state changes.
        /// </summary>
        public void RestoreHp(int hp)
        {
            _currentHp = Math.Clamp(hp, 0, _maxHp);
        }

        /// <summary>
        /// Get the current attack being executed (or null if not attacking)
        /// </summary>
        public MobAttackEntry GetCurrentAttack()
        {
            if (_state != MobAIState.Attack || _currentAttackIndex < 0 || _currentAttackIndex >= _attacks.Count)
                return null;
            return _attacks[_currentAttackIndex];
        }

        /// <summary>
        /// Get the current skill being executed (or null if not using a skill).
        /// The skill contains the SkillId and Level needed to look up the "affected"
        /// animation from MobSkill.img to play on the player when hit.
        /// </summary>
        public MobSkillEntry GetCurrentSkill()
        {
            if (_state != MobAIState.Skill || _currentSkillIndex < 0 || _currentSkillIndex >= _skills.Count)
                return null;
            return _skills[_currentSkillIndex];
        }

        /// <summary>
        /// Start using a skill (for external control or testing)
        /// </summary>
        public void UseSkill(int skillIndex, int currentTick)
        {
            if (skillIndex < 0 || skillIndex >= _skills.Count)
                return;

            StartSkill(skillIndex, currentTick);
        }

        /// <summary>
        /// Check if mob should deal damage this frame (based on attack timing)
        /// </summary>
        public bool ShouldDealDamage(int currentTick)
        {
            var attack = GetCurrentAttack();
            if (attack == null)
                return false;

            int elapsed = StateElapsed(currentTick);
            int triggerDelay = GetAttackTriggerDelay(attack);
            return elapsed >= triggerDelay && elapsed < triggerDelay + 50;
        }

        /// <summary>
        /// Check if mob skill effect should apply this frame (based on skill timing)
        /// </summary>
        public bool ShouldApplySkillEffect(int currentTick)
        {
            var skill = GetCurrentSkill();
            if (skill == null)
                return false;

            int elapsed = StateElapsed(currentTick);
            int triggerDelay = GetSkillTriggerDelay(skill);
            return elapsed >= triggerDelay && elapsed < triggerDelay + 50;
        }

        public int CalculateOutgoingDamage(int baseDamage, MobDamageType damageType)
        {
            int damage = Math.Max(1, baseDamage);
            int percentBonus = 0;

            if (damageType == MobDamageType.Magical)
            {
                percentBonus += GetStatusPercent(MobStatusEffect.MADamage);
                percentBonus += GetStatusPercent(MobStatusEffect.MagicUp);
            }
            else
            {
                percentBonus += GetStatusPercent(MobStatusEffect.PADamage);
                percentBonus += GetStatusPercent(MobStatusEffect.PowerUp);
            }

            return ApplyPercentModifier(damage, percentBonus);
        }

        public int CalculateIncomingDamage(int baseDamage, MobDamageType damageType)
        {
            int damage = Math.Max(1, baseDamage);

            if (damageType == MobDamageType.Magical)
            {
                if (HasStatusEffect(MobStatusEffect.MImmune))
                {
                    return 1;
                }

                int reductionPercent = GetStatusPercent(MobStatusEffect.MDamage) +
                                       GetStatusPercent(MobStatusEffect.MGuardUp) +
                                       GetStatusPercent(MobStatusEffect.HardSkin);
                int bonusPercent = GetStatusPercent(MobStatusEffect.Ambush) +
                                   GetStatusPercent(MobStatusEffect.Neutralise) +
                                   GetStatusPercent(MobStatusEffect.Weakness);
                damage = ApplyPercentModifier(damage, bonusPercent - reductionPercent);
            }
            else
            {
                if (HasStatusEffect(MobStatusEffect.PImmune))
                {
                    return 1;
                }

                int reductionPercent = GetStatusPercent(MobStatusEffect.PDamage) +
                                       GetStatusPercent(MobStatusEffect.PGuardUp) +
                                       GetStatusPercent(MobStatusEffect.HardSkin);
                int bonusPercent = GetStatusPercent(MobStatusEffect.Ambush) +
                                   GetStatusPercent(MobStatusEffect.Weakness);
                damage = ApplyPercentModifier(damage, bonusPercent - reductionPercent);
            }

            return Math.Max(1, damage);
        }

        private void AddDamageDisplay(int damage, int currentTick, bool isCritical)
        {
            // Limit max displays
            if (_damageDisplays.Count >= MAX_DAMAGE_DISPLAYS)
            {
                _damageDisplays.RemoveAt(0);
            }

            var display = new MobDamageInfo
            {
                Damage = damage,
                IsCritical = isCritical,
                DisplayTime = currentTick,
                OffsetX = (Random.Shared.NextSingle() - 0.5f) * 30f,
                OffsetY = 0,
                Color = isCritical ? Color.Yellow : Color.White
            };

            _damageDisplays.Add(display);
        }
        #endregion

        #region Queries
        /// <summary>
        /// Get recommended action string for animation system
        /// </summary>
        public string GetRecommendedAction()
        {
            return _state switch
            {
                MobAIState.Idle => "stand",
                MobAIState.Patrol => "move",
                MobAIState.Alert => "stand",
                MobAIState.Chase => "move",
                MobAIState.Attack => GetCurrentAttack()?.AnimationName ?? "attack1",
                MobAIState.Skill => GetCurrentSkill()?.AnimationName ?? "skill1",
                MobAIState.Hit => "hit1",
                MobAIState.Death => "die1",
                MobAIState.Removed => "die1",
                _ => "stand"
            };
        }

        /// <summary>
        /// Get chase direction towards target (-1 = left, 0 = none, 1 = right)
        /// </summary>
        public int GetChaseDirection(float mobX)
        {
            if (!_target.IsValid || _state != MobAIState.Chase)
                return 0;

            float dx = _target.TargetX - mobX;
            if (Math.Abs(dx) < 5)
                return 0;
            return dx > 0 ? 1 : -1;
        }

        /// <summary>
        /// Get chase speed multiplier (faster when chasing)
        /// </summary>
        public float GetSpeedMultiplier()
        {
            if (IsFrozen || IsStunned)
            {
                return 0f;
            }

            float baseMultiplier = _state == MobAIState.Chase ? _chaseSpeedMultiplier : 1.0f;
            int speedPercent = GetStatusPercent(MobStatusEffect.Speed);
            int slowPercent = 0;

            if (HasStatusEffect(MobStatusEffect.Web))
            {
                slowPercent += GetStatusPercentOrDefault(MobStatusEffect.Web, 50);
            }

            if (HasStatusEffect(MobStatusEffect.Weakness))
            {
                slowPercent += GetStatusPercentOrDefault(MobStatusEffect.Weakness, 20);
            }

            if (IsDoomed)
            {
                slowPercent += 65;
            }

            float statusMultiplier = 1f + (speedPercent - slowPercent) / 100f;
            return Math.Max(0f, baseMultiplier * Math.Max(0f, statusMultiplier));
        }

        /// <summary>
        /// Check if mob is aggressive (chasing or attacking)
        /// </summary>
        public bool IsAggressive =>
            _state == MobAIState.Chase ||
            _state == MobAIState.Attack ||
            _state == MobAIState.Skill ||
            _state == MobAIState.Alert;

        /// <summary>
        /// True if mob has been hit and is now aggroed
        /// </summary>
        public bool IsAggroed => _isAggroed;
        #endregion

        #region Reset
        public void Reset()
        {
            _state = MobAIState.Idle;
            _previousState = MobAIState.Idle;
            _currentHp = _maxHp;
            _target = new MobTargetInfo();
            _damageDisplays.Clear();
            _currentAttackIndex = -1;
            _currentSkillIndex = -1;
            _statusEffects = MobStatusEffect.None;
            _statusEntries.Clear();
            LastDamageTaken = 0;
            _guidedTargetId = 0;
            _isGuidedTarget = false;
            _isAggroed = false;
            _specialSpawnTime = int.MinValue;
            _selfDestructTriggered = false;
            _selfDestructPending = false;
            _angerChargeCount = 0;
            _skillUseCounts.Clear();
            _skillForbidUntil = 0;

            // Reset boss aggro timeout state
            _bossAggroStartTime = 0;
            _bossAggroTimedOut = false;

            foreach (var attack in _attacks)
            {
                attack.LastUseTime = 0;
            }

            foreach (var skill in _skills)
            {
                skill.LastUseTime = 0;
            }
        }

        /// <summary>
        /// Clear aggro state - mob will stop chasing
        /// </summary>
        public void ClearAggro()
        {
            _isAggroed = false;
            _target.IsValid = false;
        }

        /// <summary>
        /// Reset boss aggro timeout state.
        /// Call this when a boss respawns or when you want to reset the timeout timer.
        /// </summary>
        /// <param name="restoreAutoAggro">If true, restore the original autoAggro value (from firstAttack WZ property)</param>
        public void ResetBossAggroTimeout(bool restoreAutoAggro = false)
        {
            _bossAggroStartTime = 0;
            _bossAggroTimedOut = false;

            // Optionally restore auto-aggro (caller should pass the original firstAttack value)
            if (restoreAutoAggro)
            {
                // Note: The caller should set autoAggro based on Mob.wz firstAttack property
                // using SetAutoAggro() after calling this method
            }
        }
        #endregion

        #region Status Effects
        /// <summary>
        /// Check if mob has a specific status effect
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasStatusEffect(MobStatusEffect effect)
        {
            return (_statusEffects & effect) != 0;
        }

        /// <summary>
        /// Apply a status effect to this mob
        /// </summary>
        /// <param name="effect">Effect to apply</param>
        /// <param name="durationMs">Duration in milliseconds</param>
        /// <param name="currentTick">Current tick count</param>
        public void ApplyStatusEffect(
            MobStatusEffect effect,
            int durationMs,
            int currentTick,
            int value = 0,
            int tickIntervalMs = 1000,
            int secondaryValue = 0,
            int tertiaryValue = 0,
            int sourceSkillId = 0)
        {
            _statusEffects |= effect;
            bool wasActive = _statusEntries.ContainsKey(effect);
            _statusEntries[effect] = new MobStatusEntry
            {
                Effect = effect,
                ExpirationTime = currentTick + durationMs,
                Value = NormalizeStatusValue(effect, value),
                SecondaryValue = secondaryValue,
                TertiaryValue = tertiaryValue,
                TickIntervalMs = Math.Max(1, tickIntervalMs),
                NextTickTime = currentTick + Math.Max(1, tickIntervalMs),
                SourceSkillId = sourceSkillId
            };

            // Special handling for certain effects
            if (effect == MobStatusEffect.Stun || effect == MobStatusEffect.Freeze)
            {
                // Stun/freeze forces the mob into its incapacitated hit state.
                if (!IsDead)
                {
                    SetState(MobAIState.Hit, currentTick);
                }
            }

            if (!wasActive)
            {
                OnStatusSet?.Invoke(effect);
            }
        }

        public void UpdateExternalTargetPosition(int targetId, MobTargetType targetType, float targetX, float targetY, int currentTick)
        {
            if (IsDead || targetType == MobTargetType.Player)
            {
                return;
            }

            _target.TargetId = targetId;
            _target.TargetType = targetType;
            _target.TargetX = targetX;
            _target.TargetY = targetY;
            _target.IsValid = true;
            _target.LastSeenTime = currentTick;
        }

        public void ClearExternalTarget(int currentTick)
        {
            if (_target.TargetType == MobTargetType.Player)
            {
                return;
            }

            _target = new MobTargetInfo();
            _isAggroed = false;

            if (_state == MobAIState.Alert ||
                _state == MobAIState.Chase ||
                _state == MobAIState.Attack ||
                _state == MobAIState.Skill)
            {
                SetState(MobAIState.Patrol, currentTick);
            }
        }

        /// <summary>
        /// Remove a status effect from this mob
        /// </summary>
        public void RemoveStatusEffect(MobStatusEffect effect)
        {
            _statusEffects &= ~effect;
            if (_statusEntries.Remove(effect))
            {
                OnStatusReset?.Invoke(effect);
            }
        }

        /// <summary>
        /// Clear all status effects
        /// </summary>
        public void ClearStatusEffects()
        {
            if (_statusEntries.Count == 0)
            {
                _statusEffects = MobStatusEffect.None;
                return;
            }

            foreach (MobStatusEffect effect in new List<MobStatusEffect>(_statusEntries.Keys))
            {
                RemoveStatusEffect(effect);
            }
        }

        /// <summary>
        /// Update status effect durations (call from Update)
        /// </summary>
        public void UpdateStatusEffects(int currentTick)
        {
            if (_statusEffects == MobStatusEffect.None)
                return;

            // Check each active effect for expiration
            var expiredEffects = new List<MobStatusEffect>();
            foreach (var kvp in _statusEntries)
            {
                MobStatusEntry entry = kvp.Value;
                if (currentTick >= entry.ExpirationTime)
                {
                    expiredEffects.Add(kvp.Key);
                    continue;
                }

                if (IsDotEffect(entry.Effect) &&
                    entry.Value > 0 &&
                    currentTick >= entry.NextTickTime)
                {
                    ApplyStatusTickDamage(entry.Value, currentTick);
                    entry.NextTickTime = currentTick + entry.TickIntervalMs;
                }
            }

            // Remove expired effects
            foreach (var effect in expiredEffects)
            {
                RemoveStatusEffect(effect);
            }
        }

        /// <summary>
        /// Get remaining duration of a status effect in milliseconds
        /// </summary>
        public int GetStatusEffectRemaining(MobStatusEffect effect, int currentTick)
        {
            if (!HasStatusEffect(effect))
                return 0;

            if (_statusEntries.TryGetValue(effect, out MobStatusEntry entry))
            {
                return Math.Max(0, entry.ExpirationTime - currentTick);
            }
            return 0;
        }

        public int GetStatusEffectValue(MobStatusEffect effect)
        {
            return _statusEntries.TryGetValue(effect, out MobStatusEntry entry) ? entry.Value : 0;
        }
        #endregion

        #region Controller Management
        /// <summary>
        /// Set this mob as locally controlled
        /// </summary>
        public void SetLocalController(int controllerId)
        {
            _controllerType = MobControllerType.Local;
            _controllerId = controllerId;
        }

        /// <summary>
        /// Set this mob as remotely controlled
        /// </summary>
        public void SetRemoteController(int controllerId)
        {
            _controllerType = MobControllerType.Remote;
            _controllerId = controllerId;
        }

        /// <summary>
        /// Change the controller of this mob (OnMobChangeController)
        /// </summary>
        /// <param name="newControllerType">New controller type</param>
        /// <param name="newControllerId">New controller ID</param>
        /// <param name="currentTick">Current tick for state update</param>
        public void ChangeController(MobControllerType newControllerType, int newControllerId, int currentTick)
        {
            var oldType = _controllerType;
            var oldId = _controllerId;

            _controllerType = newControllerType;
            _controllerId = newControllerId;

            // If switching from remote to local, reset some state
            if (oldType == MobControllerType.Remote && newControllerType == MobControllerType.Local)
            {
                // Reset attack state - we now control this mob
                if (_state == MobAIState.Attack)
                {
                    _currentAttackIndex = -1;
                }
            }
        }

        /// <summary>
        /// Clear the controller (mob becomes uncontrolled)
        /// </summary>
        public void ClearController()
        {
            _controllerType = MobControllerType.None;
            _controllerId = 0;
        }
        #endregion

        #region Guided Arrow Targeting
        /// <summary>
        /// Set this mob as a guided arrow target
        /// </summary>
        /// <param name="targetId">The targeting skill/arrow ID</param>
        public void SetGuided(int targetId)
        {
            _guidedTargetId = targetId;
            _isGuidedTarget = true;
        }

        /// <summary>
        /// Reset guided targeting state (ResetGuidedMob)
        /// </summary>
        public void ResetGuided()
        {
            _guidedTargetId = 0;
            _isGuidedTarget = false;
        }

        /// <summary>
        /// Check if this mob is targeted by a specific guided arrow
        /// </summary>
        public bool IsGuidedBy(int targetId)
        {
            return _isGuidedTarget && _guidedTargetId == targetId;
        }
        #endregion

        #region Helpers
        private int FindAvailableAttackIndex(int currentTick)
        {
            if (IsDoomed || !_target.IsValid || (_target.TargetType == MobTargetType.Player && !CanTargetPlayerNow))
            {
                return -1;
            }

            if (IsAngerCharged &&
                _angerAttackIndex >= 0 &&
                _angerAttackIndex < _attacks.Count &&
                !_attacks[_angerAttackIndex].IsOnCooldown(currentTick) &&
                _target.Distance <= _attacks[_angerAttackIndex].Range)
            {
                return _angerAttackIndex;
            }

            for (int i = 0; i < _attacks.Count; i++)
            {
                if (IsReservedSelfDestructAttack(i))
                {
                    continue;
                }

                if (i == _angerAttackIndex)
                {
                    continue;
                }

                if (!_attacks[i].IsOnCooldown(currentTick) && _target.Distance <= _attacks[i].Range)
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindAvailableSkillIndex(int currentTick)
        {
            if (IsDoomed || IsSealed || HasStatusEffect(MobStatusEffect.SealSkill) || !_target.IsValid || (_target.TargetType == MobTargetType.Player && !CanTargetPlayerNow))
            {
                return -1;
            }

            if (currentTick < _skillForbidUntil)
            {
                return -1;
            }

            int bestSkillIndex = -1;
            int bestPriority = int.MinValue;
            bool bestHasPrerequisite = false;

            for (int i = 0; i < _skills.Count; i++)
            {
                MobSkillEntry skill = _skills[i];
                if (!CanAutoSelectSkill(skill, currentTick))
                {
                    continue;
                }

                bool hasPrerequisite = skill.PreSkillCount > 0;
                int priority = skill.Priority;
                if (hasPrerequisite)
                {
                    priority += 100;
                }

                if (bestSkillIndex < 0 ||
                    priority > bestPriority ||
                    (priority == bestPriority && hasPrerequisite && !bestHasPrerequisite))
                {
                    bestSkillIndex = i;
                    bestPriority = priority;
                    bestHasPrerequisite = hasPrerequisite;
                }
            }

            return bestSkillIndex;
        }

        private bool ShouldPreferSkill(MobSkillEntry skill, MobAttackEntry attack)
        {
            if (skill == null)
            {
                return false;
            }

            if (attack == null)
            {
                return true;
            }

            if (skill.PreSkillCount > 0 || skill.SkillForbid > 0)
            {
                return true;
            }

            if (skill.Priority > 1)
            {
                return true;
            }

            if (_isBoss)
            {
                return true;
            }

            if (_target.Distance > attack.Range * 0.75f)
            {
                return true;
            }

            return Random.Shared.Next(100) < 35;
        }

        private void StartAttack(int attackIndex, int currentTick)
        {
            if (attackIndex < 0 || attackIndex >= _attacks.Count)
            {
                return;
            }

            _currentAttackIndex = attackIndex;
            MobAttackEntry attack = _attacks[attackIndex];
            attack.LastUseTime = currentTick;
            _actionAnimationCompleted = false;
            _actionRecoveryUntil = currentTick + GetActionRecoveryDelay(attack);
            TransitionToActionState(MobAIState.Attack, currentTick);
        }

        private void StartSkill(int skillIndex, int currentTick)
        {
            if (skillIndex < 0 || skillIndex >= _skills.Count)
            {
                return;
            }

            _currentSkillIndex = skillIndex;
            MobSkillEntry skill = _skills[skillIndex];
            skill.LastUseTime = currentTick;
            RegisterSkillUsage(skill, currentTick);
            _actionAnimationCompleted = false;
            _actionRecoveryUntil = currentTick + GetActionRecoveryDelay(skill);
            TransitionToActionState(MobAIState.Skill, currentTick);
        }

        private void TransitionToActionState(MobAIState newState, int currentTick)
        {
            if (_state != newState)
            {
                SetState(newState, currentTick);
                return;
            }

            _stateStartTime = currentTick;
            _stateTimer = 0;
        }

        private bool TryChainNextCombatAction(int currentTick)
        {
            if (!_target.IsValid || (_target.TargetType == MobTargetType.Player && !CanTargetPlayerNow))
            {
                return false;
            }

            int availableAttackIndex = FindAvailableAttackIndex(currentTick);
            int availableSkillIndex = FindAvailableSkillIndex(currentTick);

            if (availableSkillIndex >= 0 &&
                ShouldPreferSkill(_skills[availableSkillIndex], availableAttackIndex >= 0 ? _attacks[availableAttackIndex] : null))
            {
                StartSkill(availableSkillIndex, currentTick);
                return true;
            }

            if (availableAttackIndex >= 0)
            {
                StartAttack(availableAttackIndex, currentTick);
                return true;
            }

            return false;
        }

        public int LastDamageTaken { get; private set; }

        public int CalculateReflectedDamageToAttacker(int inflictedDamage, MobDamageType damageType)
        {
            if (inflictedDamage <= 0 || !_statusEntries.TryGetValue(MobStatusEffect.Reflect, out MobStatusEntry entry))
            {
                return 0;
            }

            int reflectPercent = Math.Clamp(entry.Value, 0, 100);
            if (reflectPercent <= 0)
            {
                return 0;
            }

            int cap = entry.SourceSkillId switch
            {
                143 when damageType == MobDamageType.Physical => entry.SecondaryValue,
                144 when damageType == MobDamageType.Magical => entry.TertiaryValue != 0 ? entry.TertiaryValue : entry.SecondaryValue,
                145 when damageType == MobDamageType.Physical => entry.SecondaryValue,
                145 when damageType == MobDamageType.Magical => entry.TertiaryValue != 0 ? entry.TertiaryValue : entry.SecondaryValue,
                143 or 144 or 145 => 0,
                _ => Math.Max(entry.SecondaryValue, entry.TertiaryValue)
            };

            if (cap <= 0)
            {
                return 0;
            }

            int reflectedDamage = (int)MathF.Round(inflictedDamage * (reflectPercent / 100f));
            return Math.Max(1, Math.Min(cap, reflectedDamage));
        }

        private static int GetAttackTriggerDelay(MobAttackEntry attack)
        {
            if (attack == null)
            {
                return 0;
            }

            if (attack.Delay > 0)
            {
                return attack.Delay;
            }

            if (attack.EffectAfter > 0)
            {
                return attack.EffectAfter;
            }

            if (attack.AttackAfter > 0)
            {
                return attack.AttackAfter;
            }

            return 200;
        }

        private void CompleteCurrentCombatAction(int currentTick)
        {
            _actionAnimationCompleted = false;
            _actionRecoveryUntil = 0;

            if (_selfDestructPending)
            {
                _selfDestructPending = false;
                Kill(currentTick, MobDeathType.Bomb);
                return;
            }

            if (_state == MobAIState.Attack)
            {
                ResolveAngerGaugeAfterAttack(_currentAttackIndex);
            }

            if (TryChainNextCombatAction(currentTick))
            {
                return;
            }

            SetState(MobAIState.Chase, currentTick);
        }

        private static int GetActionRecoveryDelay(MobAttackEntry attack)
        {
            if (attack == null)
            {
                return 0;
            }

            return Math.Max(0, Math.Max(attack.AttackAfter, attack.EffectAfter));
        }

        private static int GetSkillTriggerDelay(MobSkillEntry skill)
        {
            if (skill == null)
            {
                return 0;
            }

            if (skill.EffectAfter > 0)
            {
                return skill.EffectAfter;
            }

            if (skill.SkillAfter > 0)
            {
                return skill.SkillAfter;
            }

            return 250;
        }

        private static int GetActionRecoveryDelay(MobSkillEntry skill)
        {
            if (skill == null)
            {
                return 0;
            }

            return Math.Max(0, Math.Max(skill.SkillAfter, skill.EffectAfter));
        }

        private bool CanAutoSelectSkill(MobSkillEntry skill, int currentTick)
        {
            if (skill == null ||
                skill.OnlyFsm ||
                skill.IsOnCooldown(currentTick) ||
                _target.Distance > skill.Range)
            {
                return false;
            }

            return IsSkillPrerequisiteSatisfied(skill);
        }

        private bool IsSkillPrerequisiteSatisfied(MobSkillEntry skill)
        {
            if (skill == null || skill.PreSkillCount <= 0)
            {
                return true;
            }

            if (skill.PreSkillIndex < 0)
            {
                return false;
            }

            return _skillUseCounts.TryGetValue(skill.PreSkillIndex, out int useCount) &&
                   useCount >= skill.PreSkillCount;
        }

        private void RegisterSkillUsage(MobSkillEntry skill, int currentTick)
        {
            if (skill == null)
            {
                return;
            }

            if (skill.SourceIndex >= 0)
            {
                _skillUseCounts[skill.SourceIndex] = _skillUseCounts.TryGetValue(skill.SourceIndex, out int useCount)
                    ? useCount + 1
                    : 1;
            }

            if (skill.PreSkillCount > 0 && skill.PreSkillIndex >= 0)
            {
                _skillUseCounts[skill.PreSkillIndex] = 0;
            }

            if (skill.SkillForbid > 0)
            {
                _skillForbidUntil = Math.Max(_skillForbidUntil, currentTick + skill.SkillForbid);
            }
        }

        private static bool IsDotEffect(MobStatusEffect effect)
        {
            return effect == MobStatusEffect.Poison ||
                   effect == MobStatusEffect.Venom ||
                   effect == MobStatusEffect.Burned;
        }

        private bool CanTargetPlayerNow => _canTargetPlayer && !IsHypnotized;

        private int GetStatusPercent(MobStatusEffect effect)
        {
            return _statusEntries.TryGetValue(effect, out MobStatusEntry entry) ? entry.Value : 0;
        }

        private int GetStatusPercentOrDefault(MobStatusEffect effect, int defaultValue)
        {
            int value = GetStatusPercent(effect);
            return value != 0 ? Math.Abs(value) : defaultValue;
        }

        private static int NormalizeStatusValue(MobStatusEffect effect, int value)
        {
            return IsDotEffect(effect) ? Math.Max(0, value) : value;
        }

        private static int ApplyPercentModifier(int baseValue, int percent)
        {
            if (percent == 0)
            {
                return Math.Max(1, baseValue);
            }

            float scaled = baseValue * (1f + percent / 100f);
            return Math.Max(1, (int)MathF.Round(scaled));
        }

        private void UpdateSpecialInteractions(int currentTick)
        {
            if (_specialSpawnTime == int.MinValue)
            {
                _specialSpawnTime = currentTick;
            }

            if (_selfDestructTriggered || _selfDestructPending)
            {
                return;
            }

            if (_selfDestructHpThreshold >= 0 && _currentHp > 0 && _currentHp <= _selfDestructHpThreshold)
            {
                TriggerSelfDestruction(currentTick);
                return;
            }

            if (_selfDestructRemoveAfterMs > 0 && currentTick - _specialSpawnTime >= _selfDestructRemoveAfterMs)
            {
                TriggerSelfDestruction(currentTick);
                return;
            }

            if (_removeAfterMs > 0 && currentTick - _specialSpawnTime >= _removeAfterMs)
            {
                Kill(currentTick, MobDeathType.Timeout);
            }
        }

        private void TriggerSelfDestruction(int currentTick)
        {
            _selfDestructTriggered = true;

            if (TryStartSelfDestructionAction(currentTick))
            {
                return;
            }

            Kill(currentTick, MobDeathType.Bomb);
        }

        private bool TryStartSelfDestructionAction(int currentTick)
        {
            if (_selfDestructAction <= 0)
            {
                return false;
            }

            for (int i = 0; i < _attacks.Count; i++)
            {
                MobAttackEntry attack = _attacks[i];
                if (attack.AttackId != _selfDestructAction &&
                    !string.Equals(attack.AnimationName, $"attack{_selfDestructAction}", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _currentAttackIndex = i;
                attack.LastUseTime = currentTick;
                _selfDestructPending = true;
                SetState(MobAIState.Attack, currentTick);
                return true;
            }

            for (int i = 0; i < _skills.Count; i++)
            {
                MobSkillEntry skill = _skills[i];
                if (skill.ActionIndex != _selfDestructAction &&
                    !string.Equals(skill.AnimationName, $"skill{_selfDestructAction}", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _currentSkillIndex = i;
                skill.LastUseTime = currentTick;
                _selfDestructPending = true;
                SetState(MobAIState.Skill, currentTick);
                return true;
            }

            return false;
        }

        private bool IsReservedSelfDestructAttack(int attackIndex)
        {
            if (_selfDestructTriggered || _selfDestructAction <= 0 || attackIndex < 0 || attackIndex >= _attacks.Count)
            {
                return false;
            }

            MobAttackEntry attack = _attacks[attackIndex];
            return attack.AttackId == _selfDestructAction ||
                   string.Equals(attack.AnimationName, $"attack{_selfDestructAction}", StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyStatusTickDamage(int damage, int currentTick)
        {
            if (damage <= 0 || IsDead)
            {
                return;
            }

            _currentHp -= damage;
            AddDamageDisplay(damage, currentTick, false);

            if (_currentHp <= 0)
            {
                _currentHp = 0;
                _deathType = MobDeathType.Normal;
                _deathTime = currentTick;
                SetState(MobAIState.Death, currentTick);
            }
        }

        private void ResolveAngerGaugeAfterAttack(int attackIndex)
        {
            if (!HasAngerGauge || attackIndex < 0 || attackIndex >= _attacks.Count)
            {
                return;
            }

            if (attackIndex == _angerAttackIndex)
            {
                _angerChargeCount = 0;
                return;
            }

            _angerChargeCount = Math.Min(_angerChargeTarget, _angerChargeCount + 1);
        }
        #endregion

        public event Action<MobStatusEffect> OnStatusSet;
        public event Action<MobStatusEffect> OnStatusReset;
    }
}
