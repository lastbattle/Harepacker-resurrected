using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Managers;

namespace HaCreator.MapSimulator.Character.Skills
{
    /// <summary>
    /// Manages active skills, projectiles, buffs, and cooldowns
    /// </summary>
    public class SkillManager
    {
        private enum AttackResolutionMode
        {
            Melee,
            Magic,
            Projectile
        }

        private const string GenericBuffIconKey = "united/buff";

        #region Constants

        // Hotkey slot counts
        public const int PRIMARY_SLOT_COUNT = 8;       // Skill1-8 (indices 0-7)
        public const int FUNCTION_SLOT_COUNT = 12;     // F1-F12 (indices 8-19)
        public const int CTRL_SLOT_COUNT = 8;          // Ctrl+1-8 (indices 20-27)
        public const int TOTAL_SLOT_COUNT = PRIMARY_SLOT_COUNT + FUNCTION_SLOT_COUNT + CTRL_SLOT_COUNT;

        // Slot index offsets
        public const int FUNCTION_SLOT_OFFSET = PRIMARY_SLOT_COUNT;
        public const int CTRL_SLOT_OFFSET = PRIMARY_SLOT_COUNT + FUNCTION_SLOT_COUNT;

        #endregion

        #region Properties

        private readonly SkillLoader _loader;
        private readonly PlayerCharacter _player;
        private Func<SkillData, bool> _fieldSkillRestrictionEvaluator;

        // Active state
        private readonly List<ActiveProjectile> _projectiles = new();
        private readonly List<ActiveBuff> _buffs = new();
        private readonly List<ActiveSummon> _summons = new();
        private readonly List<ActiveHitEffect> _hitEffects = new();
        private readonly Dictionary<int, int> _cooldowns = new(); // skillId -> lastCastTime
        private PreparedSkill _preparedSkill;
        private SkillCastInfo _currentCast;

        // Skill book
        private readonly Dictionary<int, int> _skillLevels = new(); // skillId -> level
        private List<SkillData> _availableSkills = new();

        // Hotkeys - supports 28 total slots:
        // 0-7: Primary slots (Skill1-8)
        // 8-19: Function key slots (F1-F12)
        // 20-27: Ctrl+Number slots (Ctrl+1-8)
        private readonly Dictionary<int, int> _skillHotkeys = new(); // slotIndex -> skillId

        // Counters
        private int _nextProjectileId = 1;
        private int _nextSummonId = 1;

        private static readonly Random Random = new();

        // Callbacks
        public Action<SkillCastInfo> OnSkillCast;
        public Action<ActiveProjectile, MobItem> OnProjectileHit;
        public Action<ActiveBuff> OnBuffApplied;
        public Action<ActiveBuff> OnBuffExpired;
        public Action<PreparedSkill> OnPreparedSkillStarted;
        public Action<PreparedSkill> OnPreparedSkillReleased;

        // References
        private MobPool _mobPool;
        private CombatEffects _combatEffects;
        private SoundManager _soundManager;

        #endregion

        #region Initialization

        public SkillManager(SkillLoader loader, PlayerCharacter player)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        public void SetMobPool(MobPool mobPool) => _mobPool = mobPool;
        public void SetCombatEffects(CombatEffects effects) => _combatEffects = effects;
        public void SetSoundManager(SoundManager soundManager) => _soundManager = soundManager;
        public void SetFieldSkillRestrictionEvaluator(Func<SkillData, bool> evaluator) => _fieldSkillRestrictionEvaluator = evaluator;

        /// <summary>
        /// Load skills for player's job
        /// </summary>
        public void LoadSkillsForJob(int jobId)
        {
            ClearActiveSkillState(clearBuffs: true);

            // Standard jobs should keep their full advancement chain available (Beginner -> current job),
            // while special admin jobs stay on their focused single-book behavior.
            _availableSkills = IsFocusedSingleBookJob(jobId)
                ? _loader.LoadSkillsForJob(jobId)
                : _loader.LoadSkillsForJobPath(jobId);

            var validSkillIds = new HashSet<int>(_availableSkills.Select(skill => skill.SkillId));

            foreach (int obsoleteSkillId in _skillLevels.Keys.Where(skillId => !validSkillIds.Contains(skillId)).ToList())
            {
                _skillLevels.Remove(obsoleteSkillId);
            }

            foreach (int hotkeySlot in _skillHotkeys
                         .Where(entry => !validSkillIds.Contains(entry.Value))
                         .Select(entry => entry.Key)
                         .ToList())
            {
                _skillHotkeys.Remove(hotkeySlot);
            }

            foreach (int cooldownSkillId in _cooldowns.Keys.Where(skillId => !validSkillIds.Contains(skillId)).ToList())
            {
                _cooldowns.Remove(cooldownSkillId);
            }

            // Initialize skill levels to 0 (unlearned)
            foreach (var skill in _availableSkills)
            {
                if (!_skillLevels.ContainsKey(skill.SkillId))
                {
                    _skillLevels[skill.SkillId] = 0;
                }
            }
        }

        /// <summary>
        /// Load the full player skill catalog from Skill.wz.
        /// </summary>
        public void LoadAllSkills()
        {
            _availableSkills = _loader.LoadAllSkills();

            foreach (var skill in _availableSkills)
            {
                if (!_skillLevels.ContainsKey(skill.SkillId))
                {
                    _skillLevels[skill.SkillId] = 0;
                }
            }
        }

        /// <summary>
        /// Set skill level
        /// </summary>
        public void SetSkillLevel(int skillId, int level)
        {
            _skillLevels[skillId] = level;
        }

        /// <summary>
        /// Get skill level
        /// </summary>
        public int GetSkillLevel(int skillId)
        {
            return _skillLevels.TryGetValue(skillId, out int level) ? level : 0;
        }

        public void LearnAllActiveSkills()
        {
            foreach (var skill in _availableSkills)
            {
                if (skill == null || skill.IsPassive || skill.Invisible)
                    continue;

                SetSkillLevel(skill.SkillId, Math.Max(1, skill.MaxLevel));
            }
        }

        /// <summary>
        /// Set skill hotkey by absolute slot index (0-27)
        /// </summary>
        public void SetHotkey(int slotIndex, int skillId)
        {
            if (slotIndex < 0 || slotIndex >= TOTAL_SLOT_COUNT)
                return;

            if (skillId <= 0)
            {
                _skillHotkeys.Remove(slotIndex);
            }
            else
            {
                _skillHotkeys[slotIndex] = skillId;
            }
        }

        /// <summary>
        /// Get skill on hotkey by absolute slot index (0-27)
        /// </summary>
        public int GetHotkeySkill(int slotIndex)
        {
            return _skillHotkeys.TryGetValue(slotIndex, out int skillId) ? skillId : 0;
        }

        /// <summary>
        /// Set primary hotkey (slots 0-7, used by Skill1-8 keys)
        /// </summary>
        public void SetPrimaryHotkey(int index, int skillId)
        {
            if (index >= 0 && index < PRIMARY_SLOT_COUNT)
                SetHotkey(index, skillId);
        }

        /// <summary>
        /// Get primary hotkey skill (slots 0-7)
        /// </summary>
        public int GetPrimaryHotkey(int index)
        {
            return index >= 0 && index < PRIMARY_SLOT_COUNT ? GetHotkeySkill(index) : 0;
        }

        /// <summary>
        /// Set function key hotkey (F1-F12, slots 8-19)
        /// </summary>
        public void SetFunctionHotkey(int index, int skillId)
        {
            if (index >= 0 && index < FUNCTION_SLOT_COUNT)
                SetHotkey(FUNCTION_SLOT_OFFSET + index, skillId);
        }

        /// <summary>
        /// Get function key hotkey skill (F1-F12, slots 8-19)
        /// </summary>
        public int GetFunctionHotkey(int index)
        {
            return index >= 0 && index < FUNCTION_SLOT_COUNT ? GetHotkeySkill(FUNCTION_SLOT_OFFSET + index) : 0;
        }

        /// <summary>
        /// Set Ctrl+Number hotkey (Ctrl+1-8, slots 20-27)
        /// </summary>
        public void SetCtrlHotkey(int index, int skillId)
        {
            if (index >= 0 && index < CTRL_SLOT_COUNT)
                SetHotkey(CTRL_SLOT_OFFSET + index, skillId);
        }

        /// <summary>
        /// Get Ctrl+Number hotkey skill (Ctrl+1-8, slots 20-27)
        /// </summary>
        public int GetCtrlHotkey(int index)
        {
            return index >= 0 && index < CTRL_SLOT_COUNT ? GetHotkeySkill(CTRL_SLOT_OFFSET + index) : 0;
        }

        /// <summary>
        /// Clear a hotkey slot
        /// </summary>
        public void ClearHotkey(int slotIndex)
        {
            _skillHotkeys.Remove(slotIndex);
        }

        /// <summary>
        /// Get the slot index where a skill is assigned (or -1 if not found)
        /// </summary>
        public int FindSkillSlot(int skillId)
        {
            foreach (var kv in _skillHotkeys)
            {
                if (kv.Value == skillId)
                    return kv.Key;
            }
            return -1;
        }

        /// <summary>
        /// Get all hotkey configurations for saving
        /// Returns dictionary of slotIndex -> skillId
        /// </summary>
        public Dictionary<int, int> GetAllHotkeys()
        {
            return new Dictionary<int, int>(_skillHotkeys);
        }

        /// <summary>
        /// Load all hotkey configurations
        /// </summary>
        public void LoadHotkeys(Dictionary<int, int> hotkeys)
        {
            _skillHotkeys.Clear();
            if (hotkeys == null) return;

            foreach (var kv in hotkeys)
            {
                if (kv.Key >= 0 && kv.Key < TOTAL_SLOT_COUNT && kv.Value > 0)
                {
                    _skillHotkeys[kv.Key] = kv.Value;
                }
            }
        }

        /// <summary>
        /// Clear all hotkeys
        /// </summary>
        public void ClearAllHotkeys()
        {
            _skillHotkeys.Clear();
        }

        #endregion

        #region Skill Casting

        // Skill queue for macro execution
        private readonly Queue<int> _skillQueue = new();
        private int _lastQueuedSkillTime = 0;
        private const int SKILL_QUEUE_DELAY = 100; // ms between queued skill attempts

        /// <summary>
        /// Queue a skill for execution (used by skill macros)
        /// </summary>
        public void QueueSkill(int skillId)
        {
            if (skillId > 0)
            {
                _skillQueue.Enqueue(skillId);
            }
        }

        /// <summary>
        /// Clear the skill queue
        /// </summary>
        public void ClearSkillQueue()
        {
            _skillQueue.Clear();
        }

        /// <summary>
        /// Process queued skills (called from Update)
        /// </summary>
        private void ProcessSkillQueue(int currentTime)
        {
            if (_skillQueue.Count == 0)
                return;

            // Rate limit queue processing
            if (currentTime - _lastQueuedSkillTime < SKILL_QUEUE_DELAY)
                return;

            // Try to cast the next queued skill
            while (_skillQueue.Count > 0)
            {
                int skillId = _skillQueue.Peek();

                if (TryCastSkill(skillId, currentTime))
                {
                    _skillQueue.Dequeue();
                    _lastQueuedSkillTime = currentTime;
                    break; // Only cast one skill per frame
                }
                else if (!CanCastSkill(skillId, currentTime))
                {
                    // Can't cast this skill (on cooldown, no MP, etc.)
                    // Remove it from queue to avoid blocking
                    _skillQueue.Dequeue();
                }
                else
                {
                    // Skill might be castable later, keep it in queue
                    break;
                }
            }
        }

        /// <summary>
        /// Try to cast a skill
        /// </summary>
        public bool TryCastSkill(int skillId, int currentTime)
        {
            int level = GetSkillLevel(skillId);
            if (level <= 0)
                return false;

            var skill = GetSkillData(skillId);
            if (skill == null)
                return false;

            if (_preparedSkill != null && _preparedSkill.SkillId == skillId)
            {
                ReleasePreparedSkill(currentTime);
                return true;
            }

            // Check if can cast
            if (!CanCastSkill(skillId, currentTime))
                return false;

            // Start casting
            StartCast(skill, level, currentTime);
            return true;
        }

        /// <summary>
        /// Try to cast skill on hotkey
        /// </summary>
        public bool TryCastHotkey(int keyIndex, int currentTime)
        {
            int skillId = GetHotkeySkill(keyIndex);
            if (skillId <= 0)
                return false;

            return TryCastSkill(skillId, currentTime);
        }

        /// <summary>
        /// Check if skill can be cast
        /// </summary>
        public bool CanCastSkill(int skillId, int currentTime)
        {
            int level = GetSkillLevel(skillId);
            if (level <= 0)
                return false;

            var skill = GetSkillData(skillId);
            if (skill == null)
                return false;

            // Check passive
            if (skill.IsPassive)
                return false;

            if (_fieldSkillRestrictionEvaluator != null && !_fieldSkillRestrictionEvaluator(skill))
                return false;

            // Check cooldown
            if (IsOnCooldown(skillId, currentTime))
                return false;

            // Check MP
            var levelData = skill.GetLevel(level);
            if (levelData == null)
                return false;

            if (_player.MP < levelData.MpCon)
                return false;

            // Check HP (some skills consume HP)
            if (_player.HP <= levelData.HpCon)
                return false;

            // Check if already casting
            if (_currentCast != null && !_currentCast.IsComplete)
                return false;

            if (_preparedSkill != null)
                return false;

            // Attack skills cannot be cast while on ladder/rope/swimming (buffs and heals are allowed)
            if (skill.IsAttack && !_player.CanAttack)
                return false;

            return true;
        }

        /// <summary>
        /// Check cooldown
        /// </summary>
        public bool IsOnCooldown(int skillId, int currentTime)
        {
            if (!_cooldowns.TryGetValue(skillId, out int lastCast))
                return false;

            var skill = GetSkillData(skillId);
            if (skill == null)
                return false;

            int level = GetSkillLevel(skillId);
            var levelData = skill.GetLevel(level);
            if (levelData == null || levelData.Cooldown <= 0)
                return false;

            return currentTime - lastCast < levelData.Cooldown;
        }

        /// <summary>
        /// Get remaining cooldown
        /// </summary>
        public int GetCooldownRemaining(int skillId, int currentTime)
        {
            if (!_cooldowns.TryGetValue(skillId, out int lastCast))
                return 0;

            var skill = GetSkillData(skillId);
            int level = GetSkillLevel(skillId);
            var levelData = skill?.GetLevel(level);

            if (levelData == null || levelData.Cooldown <= 0)
                return 0;

            int remaining = levelData.Cooldown - (currentTime - lastCast);
            return Math.Max(0, remaining);
        }

        private void StartCast(SkillData skill, int level, int currentTime)
        {
            var levelData = skill.GetLevel(level);
            if (levelData == null)
                return;

            _currentCast = new SkillCastInfo
            {
                SkillId = skill.SkillId,
                Level = level,
                SkillData = skill,
                LevelData = levelData,
                CastTime = currentTime,
                CasterId = 0, // Player ID
                CasterX = _player.X,
                CasterY = _player.Y,
                FacingRight = _player.FacingRight
            };

            ConsumeSkillResources(skill, levelData, currentTime);
            TriggerSkillAnimation(skill);
            PlayCastSound(skill);
            OnSkillCast?.Invoke(_currentCast);

            if (skill.IsPrepareSkill)
            {
                BeginPreparedSkill(skill, level, currentTime);
                return;
            }

            ExecuteSkillPayload(skill, level, currentTime);
        }

        private void ConsumeSkillResources(SkillData skill, SkillLevelData levelData, int currentTime)
        {
            _player.MP = Math.Max(0, _player.MP - levelData.MpCon);

            if (levelData.HpCon > 0)
            {
                _player.HP = Math.Max(1, _player.HP - levelData.HpCon);
            }

            if (levelData.Cooldown > 0)
            {
                _cooldowns[skill.SkillId] = currentTime;
            }
        }

        private void TriggerSkillAnimation(SkillData skill)
        {
            string actionName = string.IsNullOrWhiteSpace(skill.ActionName)
                ? skill.AttackType switch
                {
                    SkillAttackType.Ranged => "shoot1",
                    SkillAttackType.Magic => "swingO1",
                    _ => "attack1"
                }
                : skill.ActionName;

            _player.TriggerSkillAnimation(actionName);
        }

        private void PlayCastSound(SkillData skill)
        {
            if (skill == null || _soundManager == null)
                return;

            string soundKey = _loader.EnsureCastSoundRegistered(skill, _soundManager);
            if (!string.IsNullOrEmpty(soundKey))
            {
                _soundManager.PlaySound(soundKey);
            }
        }

        private void ExecuteSkillPayload(SkillData skill, int level, int currentTime)
        {
            if (skill.IsMovement)
            {
                ExecuteMovementSkill(skill, level);
            }

            if (skill.IsBuff)
            {
                ApplyBuff(skill, level, currentTime);
            }

            if (skill.IsHeal)
            {
                ApplyHeal(skill, level);
            }

            if (skill.IsSummon)
            {
                SpawnSummon(skill, level, currentTime);
            }

            if (!skill.IsAttack)
                return;

            if (skill.Projectile != null)
            {
                SpawnProjectile(skill, level, currentTime);
                return;
            }

            if (skill.AttackType == SkillAttackType.Magic)
            {
                ProcessMagicAttack(skill, level, currentTime);
                return;
            }

            ProcessMeleeAttack(skill, level, currentTime);
        }

        private void BeginPreparedSkill(SkillData skill, int level, int currentTime)
        {
            var levelData = skill.GetLevel(level);
            int durationMs = GetPrepareDuration(skill, levelData);

            _preparedSkill = new PreparedSkill
            {
                SkillId = skill.SkillId,
                Level = level,
                StartTime = currentTime,
                Duration = durationMs,
                SkillData = skill,
                LevelData = levelData
            };

            OnPreparedSkillStarted?.Invoke(_preparedSkill);
        }

        private int GetPrepareDuration(SkillData skill, SkillLevelData levelData)
        {
            if (levelData == null)
                return 750;

            if (levelData.Time > 0 && levelData.Time <= 5)
                return levelData.Time * 1000;

            if (levelData.X > 0 && levelData.X <= 5000)
                return levelData.X;

            if (levelData.Y > 0 && levelData.Y <= 5000)
                return levelData.Y;

            return skill.Projectile != null ? 900 : 750;
        }

        private void ReleasePreparedSkill(int currentTime)
        {
            if (_preparedSkill == null)
                return;

            PreparedSkill prepared = _preparedSkill;
            _preparedSkill = null;

            ExecuteSkillPayload(prepared.SkillData, prepared.Level, currentTime);
            OnPreparedSkillReleased?.Invoke(prepared);
        }

        private void ExecuteMovementSkill(SkillData skill, int level)
        {
            var levelData = skill.GetLevel(level);
            if (levelData == null)
                return;

            int horizontalRange = Math.Max(
                Math.Max(levelData.RangeR, levelData.RangeL),
                Math.Max(levelData.Range, Math.Max(levelData.X, levelData.Y)));

            if (horizontalRange <= 0)
                horizontalRange = 120;

            bool isTeleport = SkillTextContains(skill, "teleport");
            bool isFlyingRush = SkillTextContains(skill, "fly")
                                || SkillTextContains(skill, "flying")
                                || SkillTextContains(skill, "rocket");
            bool isJumpRush = !isTeleport
                              && !isFlyingRush
                              && (SkillTextContains(skill, "flash jump")
                                  || SkillTextContains(skill, "jump")
                                  || SkillTextContains(skill, "hop"));

            float direction = _player.FacingRight ? 1f : -1f;
            float targetX = _player.X + (horizontalRange * direction);

            if (isTeleport)
            {
                _player.SetPosition(targetX, _player.Y);
                return;
            }

            float rushSpeed = Math.Max(250f, horizontalRange * 2.5f);
            if (isJumpRush)
            {
                float jumpPower = (_player.Build?.JumpPower ?? 100) / 100f;
                float jumpRushVerticalSpeed = Math.Max(
                    140f,
                    Math.Max(levelData.RangeY, Math.Max(levelData.Y, levelData.RangeBottom - levelData.RangeTop)));

                _player.Physics.Jump();
                _player.Physics.SetVelocity(
                    rushSpeed * direction,
                    -Math.Max(jumpRushVerticalSpeed, HaCreator.MapSimulator.Physics.CVecCtrl.JumpVelocity * jumpPower));
                return;
            }

            float verticalSpeed = isFlyingRush ? -60f : (float)_player.Physics.VelocityY;
            _player.Physics.SetVelocity(rushSpeed * direction, verticalSpeed);
        }

        private static bool SkillTextContains(SkillData skill, string value)
        {
            return skill?.ActionName?.Contains(value, StringComparison.OrdinalIgnoreCase) == true
                   || skill?.Name?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;
        }

        #endregion

        #region Basic Attack Methods (TryDoingMeleeAttack, TryDoingShoot, TryDoingMagicAttack)

        /// <summary>
        /// TryDoingMeleeAttack - Close range attack (matching CUserLocal::TryDoingMeleeAttack from client)
        /// </summary>
        public bool TryDoingMeleeAttack(int currentTime)
        {
            // Cannot attack while on ladder/rope/swimming
            if (!_player.CanAttack)
                return false;

            if (_mobPool == null)
                return false;

            // Trigger attack animation
            _player.TriggerSkillAnimation("swingO1");

            // Define melee hitbox (relative to player facing)
            int hitWidth = 80;
            int hitHeight = 60;
            int offsetX = _player.FacingRight ? 10 : -(hitWidth + 10);

            var worldHitbox = new Rectangle(
                (int)_player.X + offsetX,
                (int)_player.Y - hitHeight - 10,
                hitWidth,
                hitHeight);

            int hitCount = 0;
            int maxTargets = 3; // Can hit up to 3 mobs
            var mobsToKill = new List<MobItem>();

            foreach (var mob in _mobPool.ActiveMobs)
            {
                if (hitCount >= maxTargets)
                    break;

                // Skip dead or removed mobs
                if (mob?.AI == null || mob.AI.IsDead)
                    continue;

                var mobHitbox = GetMobHitbox(mob);
                if (!worldHitbox.Intersects(mobHitbox))
                    continue;

                // Calculate basic melee damage
                int damage = CalculateBasicDamage();

                bool died = mob.ApplyDamage(damage, currentTime, damage > 100, _player.X, _player.Y);

                // Apply knockback effect if mob didn't die
                if (!died && mob.MovementInfo != null)
                {
                    float knockbackForce = 6f + (damage / 50f); // Scale knockback with damage
                    knockbackForce = Math.Min(knockbackForce, 12f); // Cap at 12
                    bool knockRight = _player.FacingRight; // Knock away from player
                    mob.MovementInfo.ApplyKnockback(knockbackForce, knockRight);
                }

                if (_combatEffects != null)
                {
                    Vector2 damageAnchor = mob.GetDamageNumberAnchor();

                    // Notify HP bar system
                    _combatEffects.OnMobDamaged(mob, currentTime);

                    _combatEffects.AddDamageNumber(
                        damage,
                        damageAnchor.X,
                        damageAnchor.Y,
                        damage > 100,
                        false,
                        currentTime,
                        hitCount);
                }

                // Queue mob for death (can't modify collection during iteration)
                if (died)
                {
                    mobsToKill.Add(mob);
                }

                hitCount++;
            }

            // Kill mobs after iteration is complete
            foreach (var mob in mobsToKill)
            {
                HandleMobDeath(mob, currentTime);
            }

            return hitCount > 0;
        }

        /// <summary>
        /// TryDoingShoot - Ranged projectile attack (matching CUserLocal::TryDoingShoot from client)
        /// </summary>
        public bool TryDoingShoot(int currentTime)
        {
            // Cannot attack while on ladder/rope/swimming
            if (!_player.CanAttack)
                return false;

            // Trigger shooting animation
            _player.TriggerSkillAnimation("shoot1");

            // Create a basic projectile
            var proj = new ActiveProjectile
            {
                Id = _nextProjectileId++,
                SkillId = 0, // Basic attack
                SkillLevel = 1,
                Data = CreateBasicProjectileData(),
                LevelData = null,
                X = _player.X,
                Y = _player.Y - 25, // Hand height
                FacingRight = _player.FacingRight,
                SpawnTime = currentTime,
                OwnerId = 0,
                OwnerX = _player.X,
                OwnerY = _player.Y
            };

            // Set velocity
            float speed = 8.0f;
            proj.VelocityX = _player.FacingRight ? speed : -speed;
            proj.VelocityY = 0;

            _projectiles.Add(proj);

            return true;
        }

        /// <summary>
        /// TryDoingMagicAttack - Magic attack with effect (matching CUserLocal::TryDoingMagicAttack from client)
        /// </summary>
        public bool TryDoingMagicAttack(int currentTime)
        {
            // Cannot attack while on ladder/rope/swimming
            if (!_player.CanAttack)
                return false;

            if (_mobPool == null)
                return false;

            // Trigger magic attack animation
            _player.TriggerSkillAnimation("swingO1");

            // Consume MP for magic attack
            int mpCost = 10;
            if (_player.MP < mpCost)
                return false;

            _player.MP -= mpCost;

            // Magic attack has larger range but single target
            int hitWidth = 120;
            int hitHeight = 80;
            int offsetX = _player.FacingRight ? 20 : -(hitWidth + 20);

            var worldHitbox = new Rectangle(
                (int)_player.X + offsetX,
                (int)_player.Y - hitHeight - 10,
                hitWidth,
                hitHeight);

            MobItem closestMob = null;
            float closestDist = float.MaxValue;

            foreach (var mob in _mobPool.ActiveMobs)
            {
                // Skip dead or removed mobs
                if (mob?.AI == null || mob.AI.IsDead)
                    continue;

                var mobHitbox = GetMobHitbox(mob);
                if (!worldHitbox.Intersects(mobHitbox))
                    continue;

                float dist = Math.Abs((mob.MovementInfo?.X ?? 0) - _player.X);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestMob = mob;
                }
            }

            if (closestMob == null)
                return false;

            // Calculate magic damage (higher than melee but single target)
            int damage = CalculateBasicDamage() + 50;
            bool isCritical = Random.Next(100) < 20; // 20% crit chance
            if (isCritical)
                damage = (int)(damage * 1.5f);

            bool died = closestMob.ApplyDamage(damage, currentTime, isCritical, _player.X, _player.Y);

            // Apply knockback effect if mob didn't die
            if (!died && closestMob.MovementInfo != null)
            {
                float knockbackForce = 4f + (damage / 80f); // Magic has less knockback
                knockbackForce = Math.Min(knockbackForce, 8f); // Cap at 8
                bool knockRight = _player.FacingRight;
                closestMob.MovementInfo.ApplyKnockback(knockbackForce, knockRight);
            }

            if (_combatEffects != null)
            {
                Vector2 damageAnchor = closestMob.GetDamageNumberAnchor();

                // Notify HP bar system
                _combatEffects.OnMobDamaged(closestMob, currentTime);

                _combatEffects.AddDamageNumber(
                    damage,
                    damageAnchor.X,
                    damageAnchor.Y,
                    isCritical,
                    false,
                    currentTime,
                    0);
            }

            // If mob died, notify mob pool
            if (died && _mobPool != null)
            {
                HandleMobDeath(closestMob, currentTime);
            }

            return true;
        }

        /// <summary>
        /// Handles mob death effects, sounds, and pool removal
        /// </summary>
        private void HandleMobDeath(MobItem mob, int currentTime)
        {
            if (mob == null)
                return;

            // Play death sound FIRST, before any cleanup
            mob.PlayDieSound();

            // Trigger death effects
            if (_combatEffects != null)
            {
                _combatEffects.AddDeathEffectForMob(mob, currentTime);
                _combatEffects.RemoveMobHPBar(mob.PoolId);
            }

            // Remove from mob pool LAST
            _mobPool?.KillMob(mob, MobDeathType.Killed);
        }

        /// <summary>
        /// TryDoingRandomAttack - Randomly selects and performs one of the three attack types
        /// </summary>
        public bool TryDoingRandomAttack(int currentTime)
        {
            int attackType = Random.Next(3);

            return attackType switch
            {
                0 => TryDoingMeleeAttack(currentTime),
                1 => TryDoingShoot(currentTime),
                2 => TryDoingMagicAttack(currentTime),
                _ => TryDoingMeleeAttack(currentTime)
            };
        }

        /// <summary>
        /// Calculate basic attack damage without skill
        /// </summary>
        private int CalculateBasicDamage()
        {
            int baseAttack = _player.Build?.Attack ?? 10;
            var weapon = _player.Build?.GetWeapon();
            if (weapon != null)
            {
                baseAttack += weapon.Attack;
            }

            // Variance 0.9 - 1.1
            float variance = 0.9f + (float)Random.NextDouble() * 0.2f;

            return Math.Max(1, (int)(baseAttack * variance));
        }

        /// <summary>
        /// Create basic projectile data for shooting
        /// </summary>
        private ProjectileData CreateBasicProjectileData()
        {
            return new ProjectileData
            {
                Speed = 8.0f,
                Piercing = false,
                MaxHits = 1,
                LifeTime = 2000 // 2 seconds
            };
        }

        #endregion

        #region Attack Processing

        private void ProcessMeleeAttack(SkillData skill, int level, int currentTime)
        {
            ProcessDirectionalAttack(skill, level, currentTime, AttackResolutionMode.Melee);
        }

        private void ProcessMagicAttack(SkillData skill, int level, int currentTime)
        {
            ProcessDirectionalAttack(skill, level, currentTime, AttackResolutionMode.Magic);
        }

        private void ProcessDirectionalAttack(SkillData skill, int level, int currentTime, AttackResolutionMode mode)
        {
            if (_mobPool == null)
                return;

            var levelData = skill.GetLevel(level);
            if (levelData == null)
                return;

            Rectangle worldHitbox = GetWorldAttackHitbox(skill, level, levelData, mode);
            int maxTargets = Math.Max(1, levelData.MobCount);
            int attackCount = Math.Max(1, levelData.AttackCount);
            List<MobItem> targets = ResolveTargetsInHitbox(worldHitbox, currentTime, maxTargets, mode);
            if (targets.Count == 0)
                return;

            var mobsToKill = new List<MobItem>();
            foreach (MobItem mob in targets)
            {
                bool died = ApplySkillAttackToMob(
                    skill,
                    level,
                    levelData,
                    mob,
                    currentTime,
                    attackCount,
                    mode,
                    skill.HitEffect,
                    _player.FacingRight);

                if (died && !mobsToKill.Contains(mob))
                {
                    mobsToKill.Add(mob);
                }
            }

            foreach (MobItem mob in mobsToKill)
            {
                HandleMobDeath(mob, currentTime);
            }
        }

        private Rectangle GetWorldAttackHitbox(SkillData skill, int level, SkillLevelData levelData, AttackResolutionMode mode)
        {
            Rectangle hitbox = skill.GetAttackRange(level, _player.FacingRight);
            if (hitbox.Width <= 0 || hitbox.Height <= 0)
            {
                hitbox = mode == AttackResolutionMode.Magic
                    ? GetDefaultMagicHitbox(skill, levelData, _player.FacingRight)
                    : GetDefaultMeleeHitbox(skill, levelData, _player.FacingRight);
            }

            return new Rectangle(
                (int)_player.X + hitbox.X,
                (int)_player.Y + hitbox.Y,
                Math.Max(1, hitbox.Width),
                Math.Max(1, hitbox.Height));
        }

        private List<MobItem> ResolveTargetsInHitbox(Rectangle worldHitbox, int currentTime, int maxTargets, AttackResolutionMode mode)
        {
            if (_mobPool == null || maxTargets <= 0)
                return new List<MobItem>();

            float areaCenterX = worldHitbox.Left + worldHitbox.Width * 0.5f;
            float areaCenterY = worldHitbox.Top + worldHitbox.Height * 0.5f;

            return _mobPool.ActiveMobs
                .Where(IsMobAttackable)
                .Select(mob => new { Mob = mob, Hitbox = GetMobHitbox(mob, currentTime) })
                .Where(entry => !entry.Hitbox.IsEmpty && worldHitbox.Intersects(entry.Hitbox))
                .Select(entry =>
                {
                    float mobCenterX = entry.Hitbox.Left + entry.Hitbox.Width * 0.5f;
                    float mobCenterY = entry.Hitbox.Top + entry.Hitbox.Height * 0.5f;
                    float deltaX = mobCenterX - _player.X;
                    float forwardDistance = _player.FacingRight ? deltaX : -deltaX;
                    float forwardPenalty = forwardDistance < 0f ? 100000f + MathF.Abs(forwardDistance) : forwardDistance;
                    float verticalDistance = MathF.Abs(mobCenterY - _player.Y);
                    float areaDistance = Vector2.Distance(
                        new Vector2(mobCenterX, mobCenterY),
                        new Vector2(areaCenterX, areaCenterY));

                    return new
                    {
                        entry.Mob,
                        Primary = mode == AttackResolutionMode.Magic ? areaDistance : forwardPenalty,
                        Secondary = mode == AttackResolutionMode.Magic ? forwardPenalty : areaDistance,
                        Tertiary = verticalDistance
                    };
                })
                .OrderBy(entry => entry.Primary)
                .ThenBy(entry => entry.Secondary)
                .ThenBy(entry => entry.Tertiary)
                .Take(maxTargets)
                .Select(entry => entry.Mob)
                .ToList();
        }

        private static bool IsMobAttackable(MobItem mob)
        {
            return mob?.AI != null
                && mob.AI.State != MobAIState.Death
                && mob.AI.State != MobAIState.Removed;
        }

        private bool ApplySkillAttackToMob(
            SkillData skill,
            int level,
            SkillLevelData levelData,
            MobItem mob,
            int currentTime,
            int attackCount,
            AttackResolutionMode mode,
            SkillAnimation impactAnimation,
            bool impactFacingRight)
        {
            if (!IsMobAttackable(mob))
                return false;

            for (int i = 0; i < attackCount; i++)
            {
                int damage = CalculateSkillDamage(skill, level);
                bool isCritical = IsSkillCritical(levelData);
                if (isCritical)
                {
                    damage = (int)MathF.Round(damage * 1.5f);
                }

                bool died = mob.ApplyDamage(damage, currentTime, isCritical, _player.X, _player.Y);

                ShowSkillDamageNumber(mob, damage, isCritical, currentTime, i);
                SpawnHitEffect(
                    skill.SkillId,
                    impactAnimation ?? skill.HitEffect,
                    GetMobImpactX(mob),
                    GetMobImpactY(mob),
                    impactFacingRight,
                    currentTime);

                if (died)
                    return true;

                ApplySkillKnockback(mode, mob, damage, impactFacingRight);

                if (!IsMobAttackable(mob))
                    return true;
            }

            return mob.AI?.State == MobAIState.Death;
        }

        private static bool IsSkillCritical(SkillLevelData levelData)
        {
            return levelData?.CriticalRate > 0 && Random.Next(100) < levelData.CriticalRate;
        }

        private void ShowSkillDamageNumber(MobItem mob, int damage, bool isCritical, int currentTime, int hitIndex)
        {
            if (_combatEffects == null)
                return;

            Vector2 damageAnchor = mob.GetDamageNumberAnchor();
            _combatEffects.OnMobDamaged(mob, currentTime);
            _combatEffects.AddDamageNumber(
                damage,
                damageAnchor.X,
                damageAnchor.Y,
                isCritical,
                false,
                currentTime,
                hitIndex);
        }

        private void ApplySkillKnockback(AttackResolutionMode mode, MobItem mob, int damage, bool knockRight)
        {
            if (mob?.MovementInfo == null)
                return;

            float knockbackForce = mode switch
            {
                AttackResolutionMode.Magic => 4f + (damage / 80f),
                AttackResolutionMode.Projectile => 5f + (damage / 95f),
                _ => 6f + (damage / 60f)
            };

            float cap = mode switch
            {
                AttackResolutionMode.Magic => 8f,
                AttackResolutionMode.Projectile => 10f,
                _ => 12f
            };

            mob.MovementInfo.ApplyKnockback(Math.Min(knockbackForce, cap), knockRight);
        }

        private float GetMobImpactX(MobItem mob)
        {
            if (mob == null)
                return 0f;

            return mob.MovementInfo?.X ?? mob.CurrentX;
        }

        private float GetMobImpactY(MobItem mob)
        {
            if (mob == null)
                return 0f;

            float baseY = mob.MovementInfo?.Y ?? mob.CurrentY;
            return baseY - Math.Max(20f, mob.GetVisualHeight() * 0.5f);
        }

        private Rectangle GetMobHitbox(MobItem mob, int currentTime)
        {
            if (mob == null)
                return Rectangle.Empty;

            Rectangle bodyHitbox = mob.GetBodyHitbox(currentTime);
            if (!bodyHitbox.IsEmpty)
                return bodyHitbox;

            if (mob.MovementInfo == null)
                return Rectangle.Empty;

            return new Rectangle(
                (int)mob.MovementInfo.X - 20,
                (int)mob.MovementInfo.Y - 50,
                40,
                50);
        }

        private Rectangle GetMobHitbox(MobItem mob)
        {
            return GetMobHitbox(mob, Environment.TickCount);
        }

        private int CalculateSkillDamage(SkillData skill, int level)
        {
            if (skill == null)
                return 1;

            var levelData = skill.GetLevel(level);
            if (levelData == null)
                return 1;

            // Base attack calculation
            int baseAttack = skill.AttackType == SkillAttackType.Magic
                ? _player.Build?.MagicAttack ?? 10
                : _player.Build?.Attack ?? 10;
            var weapon = _player.Build?.GetWeapon();
            if (weapon != null && skill.AttackType != SkillAttackType.Magic)
            {
                baseAttack += weapon.Attack;
            }

            // Apply skill damage multiplier
            float multiplier = levelData.Damage / 100f;
            if (multiplier <= 0f)
                multiplier = 1f;

            // Variance
            float variance = 0.9f + (float)Random.NextDouble() * 0.2f;

            int damage = (int)(baseAttack * multiplier * variance);

            return Math.Max(1, damage);
        }

        /// <summary>
        /// Spawn a hit effect at the specified position
        /// </summary>
        private void SpawnHitEffect(SkillData skill, float x, float y, int currentTime)
        {
            if (skill == null)
                return;

            SpawnHitEffect(skill.SkillId, skill.HitEffect, x, y, _player.FacingRight, currentTime);
        }

        private void SpawnHitEffect(int skillId, SkillAnimation animation, float x, float y, bool facingRight, int currentTime)
        {
            if (animation == null)
                return;

            _hitEffects.Add(new ActiveHitEffect
            {
                SkillId = skillId,
                X = x,
                Y = y,
                StartTime = currentTime,
                Animation = animation,
                FacingRight = facingRight
            });
        }

        #endregion

        #region Projectile System

        private void SpawnProjectile(SkillData skill, int level, int currentTime)
        {
            var levelData = skill.GetLevel(level);
            int bulletCount = levelData?.BulletCount ?? 1;
            float speed = GetProjectileSpeed(skill.Projectile, levelData);

            for (int i = 0; i < bulletCount; i++)
            {
                var proj = new ActiveProjectile
                {
                    Id = _nextProjectileId++,
                    SkillId = skill.SkillId,
                    SkillLevel = level,
                    Data = skill.Projectile,
                    LevelData = levelData,
                    X = _player.X,
                    Y = _player.Y - 20, // Adjust to hand height
                    FacingRight = _player.FacingRight,
                    SpawnTime = currentTime,
                    OwnerId = 0,
                    OwnerX = _player.X,
                    OwnerY = _player.Y
                };

                // Set velocity
                proj.VelocityX = _player.FacingRight ? speed : -speed;
                proj.VelocityY = 0;

                // Spread for multiple projectiles
                if (bulletCount > 1)
                {
                    float spreadAngle = (i - (bulletCount - 1) / 2f) * 10f * MathF.PI / 180f;
                    proj.VelocityX = speed * MathF.Cos(spreadAngle) * (_player.FacingRight ? 1 : -1);
                    proj.VelocityY = speed * MathF.Sin(spreadAngle);
                }

                _projectiles.Add(proj);
            }
        }

        private static float GetProjectileSpeed(ProjectileData projectileData, SkillLevelData levelData)
        {
            float speed = levelData?.BulletSpeed > 0
                ? levelData.BulletSpeed
                : projectileData?.Speed ?? 0f;
            return speed > 0f ? speed : 400f;
        }

        private void UpdateProjectiles(int currentTime, float deltaTime)
        {
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                var proj = _projectiles[i];
                UpdateProjectileBehavior(proj, currentTime);
                proj.Update(deltaTime, currentTime);

                // Check for expired
                if (proj.IsExpired)
                {
                    _projectiles.RemoveAt(i);
                    continue;
                }

                // Skip if exploding (just playing animation)
                if (proj.IsExploding)
                    continue;

                // Check mob collisions
                if (_mobPool != null)
                {
                    CheckProjectileCollisions(proj, currentTime);
                }
            }
        }

        private void UpdateProjectileBehavior(ActiveProjectile proj, int currentTime)
        {
            if (proj?.Data == null || proj.IsExploding || _mobPool == null)
                return;

            if (proj.Data.Behavior != ProjectileBehavior.Homing)
                return;

            MobItem target = FindHomingProjectileTarget(proj, currentTime);
            if (target == null)
                return;

            Rectangle targetHitbox = GetMobHitbox(target, currentTime);
            float targetX = targetHitbox.Left + targetHitbox.Width * 0.5f;
            float targetY = targetHitbox.Top + targetHitbox.Height * 0.5f;
            float deltaX = targetX - proj.X;
            float deltaY = targetY - proj.Y;
            float distance = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
            if (distance <= 0.001f)
                return;

            float speed = GetProjectileSpeed(proj.Data, proj.LevelData);
            proj.VelocityX = deltaX / distance * speed;
            proj.VelocityY = deltaY / distance * speed;
            proj.FacingRight = proj.VelocityX >= 0f;
        }

        private MobItem FindHomingProjectileTarget(ActiveProjectile proj, int currentTime)
        {
            int maxHits = GetEffectiveProjectileHitLimit(proj);
            return _mobPool.ActiveMobs
                .Where(IsMobAttackable)
                .Where(mob => proj.CanHitMob(mob.PoolId, maxHits))
                .Select(mob => new { Mob = mob, Hitbox = GetMobHitbox(mob, currentTime) })
                .Where(entry => !entry.Hitbox.IsEmpty)
                .Select(entry =>
                {
                    float centerX = entry.Hitbox.Left + entry.Hitbox.Width * 0.5f;
                    float centerY = entry.Hitbox.Top + entry.Hitbox.Height * 0.5f;
                    float deltaX = centerX - proj.X;
                    float deltaY = centerY - proj.Y;
                    float distanceSq = deltaX * deltaX + deltaY * deltaY;
                    float forward = proj.VelocityX >= 0f ? deltaX : -deltaX;
                    float forwardPenalty = forward < 0f ? 100000f + MathF.Abs(forward) : forward;
                    return new { entry.Mob, DistanceSq = distanceSq, ForwardPenalty = forwardPenalty };
                })
                .OrderBy(entry => entry.ForwardPenalty)
                .ThenBy(entry => entry.DistanceSq)
                .Select(entry => entry.Mob)
                .FirstOrDefault();
        }

        private void CheckProjectileCollisions(ActiveProjectile proj, int currentTime)
        {
            if (_mobPool == null)
                return;

            SkillData skill = GetSkillData(proj.SkillId);
            if (skill == null)
            {
                proj.IsExpired = true;
                return;
            }

            Rectangle projHitbox = GetProjectileHitbox(proj, currentTime);
            int maxTargets = GetEffectiveProjectileHitLimit(proj);
            int attackCount = Math.Max(1, proj.LevelData?.AttackCount ?? 1);
            var mobsToKill = new List<MobItem>();

            foreach (MobItem mob in ResolveProjectileCollisionTargets(proj, projHitbox, currentTime, maxTargets))
            {
                if (!proj.CanHitMob(mob.PoolId, maxTargets))
                    continue;

                proj.RegisterHit(mob.PoolId, currentTime, maxTargets);

                bool died = ApplySkillAttackToMob(
                    skill,
                    proj.SkillLevel,
                    proj.LevelData,
                    mob,
                    currentTime,
                    attackCount,
                    AttackResolutionMode.Projectile,
                    proj.Data.HitAnimation ?? skill.HitEffect,
                    proj.FacingRight);

                if (died && !mobsToKill.Contains(mob))
                {
                    mobsToKill.Add(mob);
                }

                OnProjectileHit?.Invoke(proj, mob);

                if (proj.IsExploding || proj.IsExpired || proj.HitCount >= maxTargets)
                    break;
            }

            foreach (MobItem mob in mobsToKill)
            {
                HandleMobDeath(mob, currentTime);
            }
        }

        private List<MobItem> ResolveProjectileCollisionTargets(ActiveProjectile proj, Rectangle projHitbox, int currentTime, int maxTargets)
        {
            if (_mobPool == null || maxTargets <= 0)
                return new List<MobItem>();

            float speedSq = (proj.VelocityX * proj.VelocityX) + (proj.VelocityY * proj.VelocityY);
            float speed = speedSq > 0.001f ? MathF.Sqrt(speedSq) : 0f;

            return _mobPool.ActiveMobs
                .Where(IsMobAttackable)
                .Where(mob => proj.CanHitMob(mob.PoolId, maxTargets))
                .Select(mob => new { Mob = mob, Hitbox = GetMobHitbox(mob, currentTime) })
                .Where(entry => !entry.Hitbox.IsEmpty && projHitbox.Intersects(entry.Hitbox))
                .Select(entry =>
                {
                    float centerX = entry.Hitbox.Left + entry.Hitbox.Width * 0.5f;
                    float centerY = entry.Hitbox.Top + entry.Hitbox.Height * 0.5f;
                    float deltaX = centerX - proj.X;
                    float deltaY = centerY - proj.Y;
                    float progress = speedSq > 0.001f
                        ? ((deltaX * proj.VelocityX) + (deltaY * proj.VelocityY)) / speedSq
                        : MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
                    float progressPenalty = progress < 0f ? 100000f + MathF.Abs(progress) : progress;
                    float lateralOffset = speed > 0.001f
                        ? MathF.Abs((deltaX * proj.VelocityY) - (deltaY * proj.VelocityX)) / speed
                        : 0f;
                    float distanceSq = (deltaX * deltaX) + (deltaY * deltaY);

                    return new
                    {
                        entry.Mob,
                        ProgressPenalty = progressPenalty,
                        LateralOffset = lateralOffset,
                        DistanceSq = distanceSq
                    };
                })
                .OrderBy(entry => entry.ProgressPenalty)
                .ThenBy(entry => entry.LateralOffset)
                .ThenBy(entry => entry.DistanceSq)
                .Take(maxTargets)
                .Select(entry => entry.Mob)
                .ToList();
        }

        private static int GetEffectiveProjectileHitLimit(ActiveProjectile proj)
        {
            return Math.Max(1, Math.Max(proj?.LevelData?.MobCount ?? 0, proj?.Data?.MaxHits ?? 0));
        }

        private Rectangle GetProjectileHitbox(ActiveProjectile proj, int currentTime)
        {
            if (proj?.Data == null)
                return Rectangle.Empty;

            SkillAnimation animation = proj.IsExploding ? proj.Data.ExplosionAnimation : proj.Data.Animation;
            int animationTime = proj.IsExploding ? currentTime - proj.ExplodeTime : currentTime - proj.SpawnTime;
            SkillFrame frame = animation?.GetFrameAtTime(animationTime);
            int width = frame?.Bounds.Width ?? 0;
            int height = frame?.Bounds.Height ?? 0;
            if (width <= 0 || height <= 0)
                return proj.GetHitbox();

            width = Math.Max(12, width);
            height = Math.Max(12, height);

            return new Rectangle(
                (int)proj.X - width / 2,
                (int)proj.Y - height / 2,
                width,
                height);
        }

        public IReadOnlyList<ActiveProjectile> ActiveProjectiles => _projectiles;

        #endregion

        #region Summon System

        private void SpawnSummon(SkillData skill, int level, int currentTime)
        {
            var levelData = skill.GetLevel(level);
            if (levelData == null)
                return;

            for (int i = _summons.Count - 1; i >= 0; i--)
            {
                if (_summons[i].SkillId == skill.SkillId)
                {
                    RemoveSummonPuppet(_summons[i]);
                    _summons.RemoveAt(i);
                }
            }

            int durationMs = levelData.Time > 0 ? levelData.Time * 1000 : 30000;
            float offsetX = _player.FacingRight ? 50f : -50f;
            float offsetY = -25f;

            var summon = new ActiveSummon
            {
                ObjectId = _nextSummonId++,
                SkillId = skill.SkillId,
                Level = level,
                StartTime = currentTime,
                Duration = durationMs,
                LastAttackTime = currentTime,
                OffsetX = offsetX,
                OffsetY = offsetY,
                SkillData = skill,
                LevelData = levelData,
                FacingRight = _player.FacingRight
            };

            _summons.Add(summon);
            SyncSummonPuppet(summon, currentTime);
        }

        private void UpdateSummons(int currentTime)
        {
            _mobPool?.UpdatePuppets(currentTime);

            for (int i = _summons.Count - 1; i >= 0; i--)
            {
                var summon = _summons[i];
                if (summon.IsExpired(currentTime))
                {
                    RemoveSummonPuppet(summon);
                    _summons.RemoveAt(i);
                    continue;
                }

                SyncSummonPuppet(summon, currentTime);

                if (currentTime - summon.LastAttackTime < 1000)
                    continue;

                summon.LastAttackTime = currentTime;
                ProcessSummonAttack(summon, currentTime);
            }
        }

        private void ProcessSummonAttack(ActiveSummon summon, int currentTime)
        {
            if (_mobPool == null || summon?.SkillData == null)
                return;

            int maxTargets = summon.LevelData?.MobCount ?? 2;
            int attackCount = summon.LevelData?.AttackCount ?? 1;
            var summonCenter = GetSummonPosition(summon);
            var summonArea = new Rectangle((int)summonCenter.X - 90, (int)summonCenter.Y - 70, 180, 100);
            int hitCount = 0;
            var mobsToKill = new List<MobItem>();

            foreach (var mob in _mobPool.ActiveMobs)
            {
                if (hitCount >= maxTargets)
                    break;

                if (mob?.AI == null || mob.AI.State == MobAIState.Death)
                    continue;

                if (!summonArea.Intersects(GetMobHitbox(mob)))
                    continue;

                for (int i = 0; i < attackCount; i++)
                {
                    int damage = CalculateSkillDamage(summon.SkillData, summon.Level);
                    bool died = mob.ApplyDamage(damage, currentTime, false, _player.X, _player.Y);

                    if (_combatEffects != null)
                    {
                        Vector2 damageAnchor = mob.GetDamageNumberAnchor();
                        _combatEffects.OnMobDamaged(mob, currentTime);
                        _combatEffects.AddDamageNumber(
                            damage,
                            damageAnchor.X,
                            damageAnchor.Y,
                            false,
                            false,
                            currentTime,
                            i);
                    }

                    if (summon.SkillData.HitEffect != null)
                    {
                        SpawnHitEffect(summon.SkillData, mob.MovementInfo?.X ?? summonCenter.X, (mob.MovementInfo?.Y ?? summonCenter.Y) - 20, currentTime);
                    }

                    if (died)
                    {
                        mobsToKill.Add(mob);
                        break;
                    }
                }

                hitCount++;
            }

            foreach (var mob in mobsToKill)
            {
                HandleMobDeath(mob, currentTime);
            }
        }

        private Vector2 GetSummonPosition(ActiveSummon summon)
        {
            float facingOffsetX = summon.FacingRight ? Math.Abs(summon.OffsetX) : -Math.Abs(summon.OffsetX);
            return new Vector2(_player.X + facingOffsetX, _player.Y + summon.OffsetY);
        }

        private void SyncSummonPuppet(ActiveSummon summon, int currentTime)
        {
            if (_mobPool == null || summon == null)
                return;

            Vector2 summonPosition = GetSummonPosition(summon);
            int expirationTime = summon.Duration > 0 ? summon.StartTime + summon.Duration : 0;

            _mobPool.RegisterPuppet(new PuppetInfo
            {
                ObjectId = summon.ObjectId,
                X = summonPosition.X,
                Y = summonPosition.Y,
                OwnerId = 0,
                AggroValue = 1,
                ExpirationTime = expirationTime,
                IsActive = true
            });

            float aggroRange = Math.Max(220f, Math.Abs(summon.OffsetX) + 170f);
            _mobPool.LetMobChasePuppet(summonPosition.X, summonPosition.Y, aggroRange, summon.ObjectId);
        }

        private void RemoveSummonPuppet(ActiveSummon summon)
        {
            if (_mobPool == null || summon == null)
                return;

            _mobPool.RemovePuppet(summon.ObjectId);
        }

        private void ClearSummonPuppets()
        {
            if (_mobPool == null)
                return;

            foreach (ActiveSummon summon in _summons)
            {
                _mobPool.RemovePuppet(summon.ObjectId);
            }
        }

        public IReadOnlyList<ActiveSummon> ActiveSummons => _summons;

        #endregion

        #region Buff System

        private void ApplyBuff(SkillData skill, int level, int currentTime)
        {
            var levelData = skill.GetLevel(level);
            if (levelData == null || levelData.Time <= 0)
                return;

            // Check for existing buff of same skill
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                if (_buffs[i].SkillId == skill.SkillId)
                {
                    ApplyBuffStats(_buffs[i], false);
                    OnBuffExpired?.Invoke(_buffs[i]);
                    _buffs.RemoveAt(i);
                }
            }

            var buff = new ActiveBuff
            {
                SkillId = skill.SkillId,
                Level = level,
                StartTime = currentTime,
                Duration = levelData.Time * 1000, // Convert seconds to ms
                SkillData = skill,
                LevelData = levelData
            };

            _buffs.Add(buff);
            OnBuffApplied?.Invoke(buff);

            // Apply buff effects to player stats
            ApplyBuffStats(buff, true);
        }

        private void ApplyBuffStats(ActiveBuff buff, bool apply)
        {
            var levelData = buff.LevelData;
            if (levelData == null || _player.Build == null)
                return;

            int modifier = apply ? 1 : -1;

            _player.Build.Attack += levelData.PAD * modifier;
            _player.Build.Defense += levelData.PDD * modifier;
            _player.Build.MagicAttack += levelData.MAD * modifier;
            _player.Build.MagicDefense += levelData.MDD * modifier;
            _player.Build.Accuracy += levelData.ACC * modifier;
            _player.Build.Avoidability += levelData.EVA * modifier;
            _player.Build.Speed += levelData.Speed * modifier;
            _player.Build.JumpPower += levelData.Jump * modifier;
        }

        private void UpdateBuffs(int currentTime)
        {
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                var buff = _buffs[i];
                if (buff.IsExpired(currentTime))
                {
                    // Remove buff effects
                    ApplyBuffStats(buff, false);
                    _buffs.RemoveAt(i);
                    OnBuffExpired?.Invoke(buff);
                }
            }
        }

        public IReadOnlyList<ActiveBuff> ActiveBuffs => _buffs;

        public IReadOnlyList<StatusBarBuffEntry> GetStatusBarBuffEntries(int currentTime)
        {
            if (_buffs.Count == 0)
            {
                return Array.Empty<StatusBarBuffEntry>();
            }

            var entries = new List<StatusBarBuffEntry>(_buffs.Count);
            foreach (var buff in _buffs.OrderBy(activeBuff => activeBuff.StartTime).ThenBy(activeBuff => activeBuff.SkillId))
            {
                entries.Add(new StatusBarBuffEntry
                {
                    SkillId = buff.SkillId,
                    SkillName = buff.SkillData?.Name ?? buff.SkillId.ToString(),
                    IconKey = ResolveBuffIconKey(buff),
                    StartTime = buff.StartTime,
                    DurationMs = buff.Duration,
                    RemainingMs = buff.GetRemainingTime(currentTime)
                });
            }

            return entries;
        }

        /// <summary>
        /// Get total buff stat bonus
        /// </summary>
        public int GetBuffStat(BuffStatType stat)
        {
            int total = 0;
            foreach (var buff in _buffs)
            {
                var data = buff.LevelData;
                if (data == null) continue;

                total += stat switch
                {
                    BuffStatType.Attack => data.PAD,
                    BuffStatType.MagicAttack => data.MAD,
                    BuffStatType.Defense => data.PDD,
                    BuffStatType.MagicDefense => data.MDD,
                    BuffStatType.Accuracy => data.ACC,
                    BuffStatType.Avoidability => data.EVA,
                    BuffStatType.Speed => data.Speed,
                    BuffStatType.Jump => data.Jump,
                    _ => 0
                };
            }
            return total;
        }

        /// <summary>
        /// Check if buff type is active
        /// </summary>
        public bool HasBuff(int skillId)
        {
            foreach (var buff in _buffs)
            {
                if (buff.SkillId == skillId)
                    return true;
            }
            return false;
        }

        private static string ResolveBuffIconKey(ActiveBuff buff)
        {
            var levelData = buff?.LevelData;
            if (levelData == null)
            {
                return GenericBuffIconKey;
            }

            int mappedStatCount = 0;
            string iconKey = null;

            void Track(int value, string key)
            {
                if (value <= 0)
                {
                    return;
                }

                mappedStatCount++;
                iconKey = key;
            }

            Track(levelData.PAD, "buff/incPAD");
            Track(levelData.PDD, "buff/incPDD");
            Track(levelData.MAD, "buff/incMAD");
            Track(levelData.MDD, "buff/incMDD");
            Track(levelData.ACC, "buff/incACC");
            Track(levelData.EVA, "buff/incEVA");
            Track(levelData.Speed, "buff/incSpeed");
            Track(levelData.Jump, "buff/incJump");

            return mappedStatCount == 1 && !string.IsNullOrEmpty(iconKey)
                ? iconKey
                : GenericBuffIconKey;
        }

        #endregion

        #region Heal

        private void ApplyHeal(SkillData skill, int level)
        {
            var levelData = skill.GetLevel(level);
            if (levelData == null)
                return;

            // Calculate heal amount
            int hpHeal = levelData.HP;
            int mpHeal = levelData.MP;

            // Some heals are percentage based
            if (levelData.X > 0)
            {
                hpHeal = _player.MaxHP * levelData.X / 100;
            }

            // Apply heal
            if (hpHeal > 0)
            {
                _player.HP = Math.Min(_player.MaxHP, _player.HP + hpHeal);
            }

            if (mpHeal > 0)
            {
                _player.MP = Math.Min(_player.MaxMP, _player.MP + mpHeal);
            }
        }

        #endregion

        #region Passive Skills

        /// <summary>
        /// Get passive skill bonus
        /// </summary>
        public int GetPassiveBonus(BuffStatType stat)
        {
            int total = 0;

            foreach (var skill in _availableSkills)
            {
                if (!skill.IsPassive)
                    continue;

                int level = GetSkillLevel(skill.SkillId);
                if (level <= 0)
                    continue;

                var levelData = skill.GetLevel(level);
                if (levelData == null)
                    continue;

                total += stat switch
                {
                    BuffStatType.Attack => levelData.PAD,
                    BuffStatType.MagicAttack => levelData.MAD,
                    BuffStatType.Defense => levelData.PDD,
                    BuffStatType.MagicDefense => levelData.MDD,
                    BuffStatType.Accuracy => levelData.ACC,
                    BuffStatType.Avoidability => levelData.EVA,
                    BuffStatType.CriticalRate => levelData.CriticalRate,
                    BuffStatType.Booster => levelData.X, // Usually attack speed
                    _ => 0
                };
            }

            return total;
        }

        /// <summary>
        /// Get mastery from passive skills
        /// </summary>
        public int GetMastery()
        {
            int mastery = 10; // Base mastery

            foreach (var skill in _availableSkills)
            {
                if (!skill.IsPassive)
                    continue;

                int level = GetSkillLevel(skill.SkillId);
                if (level <= 0)
                    continue;

                var levelData = skill.GetLevel(level);
                if (levelData?.Mastery > mastery)
                {
                    mastery = levelData.Mastery;
                }
            }

            return mastery;
        }

        #endregion

        #region Update

        public void Update(int currentTime, float deltaTime)
        {
            // Update current cast
            if (_currentCast != null)
            {
                // Check if cast animation is complete
                var skill = _currentCast.SkillData;
                if (skill?.Effect != null)
                {
                    if (skill.Effect.IsComplete(_currentCast.AnimationTime))
                    {
                        _currentCast.IsComplete = true;
                    }
                }
                else
                {
                    // No effect animation, complete after delay
                    if (_currentCast.AnimationTime > 500)
                    {
                        _currentCast.IsComplete = true;
                    }
                }
            }

            if (_preparedSkill != null && _preparedSkill.Progress(currentTime) >= 1f)
            {
                ReleasePreparedSkill(currentTime);
            }

            // Process skill queue (for macros)
            ProcessSkillQueue(currentTime);

            // Update projectiles
            UpdateProjectiles(currentTime, deltaTime);

            // Update summons
            UpdateSummons(currentTime);

            // Update buffs
            UpdateBuffs(currentTime);

            // Update hit effects (remove expired)
            UpdateHitEffects(currentTime);
        }

        private void UpdateHitEffects(int currentTime)
        {
            for (int i = _hitEffects.Count - 1; i >= 0; i--)
            {
                if (_hitEffects[i].IsExpired(currentTime))
                {
                    _hitEffects.RemoveAt(i);
                }
            }
        }

        #endregion

        #region Draw

        public void DrawProjectiles(SpriteBatch spriteBatch, int mapShiftX, int mapShiftY,
            int centerX, int centerY, int currentTime)
        {
            foreach (var proj in _projectiles)
            {
                DrawProjectile(spriteBatch, proj, mapShiftX, mapShiftY, centerX, centerY, currentTime);
            }
        }

        private void DrawProjectile(SpriteBatch spriteBatch, ActiveProjectile proj,
            int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime)
        {
            SkillAnimation anim;
            int animTime;

            if (proj.IsExploding)
            {
                anim = proj.Data.ExplosionAnimation;
                animTime = currentTime - proj.ExplodeTime;
            }
            else
            {
                anim = proj.Data.Animation;
                animTime = currentTime - proj.SpawnTime;
            }

            if (anim == null)
                return;

            var frame = anim.GetFrameAtTime(animTime);
            if (frame?.Texture == null)
                return;

            int screenX = (int)proj.X - mapShiftX + centerX;
            int screenY = (int)proj.Y - mapShiftY + centerY;

            bool shouldFlip = proj.FacingRight ^ frame.Flip;

            frame.Texture.DrawBackground(spriteBatch, null, null,
                GetFrameDrawX(screenX, frame, shouldFlip), screenY - frame.Origin.Y,
                Color.White, shouldFlip, null);
        }

        public void DrawEffects(SpriteBatch spriteBatch, int mapShiftX, int mapShiftY,
            int centerX, int centerY, int currentTime)
        {
            // Draw current cast effect
            if (_currentCast != null && !_currentCast.IsComplete)
            {
                DrawCastEffect(spriteBatch, _currentCast, mapShiftX, mapShiftY, centerX, centerY);
            }

            // Draw affected effects for active buffs (looping on character)
            foreach (var buff in _buffs)
            {
                DrawAffectedEffect(spriteBatch, buff, mapShiftX, mapShiftY, centerX, centerY, currentTime);
            }

            foreach (var summon in _summons)
            {
                DrawSummonEffect(spriteBatch, summon, mapShiftX, mapShiftY, centerX, centerY, currentTime);
            }

            // Draw hit effects
            foreach (var hitEffect in _hitEffects)
            {
                DrawHitEffect(spriteBatch, hitEffect, mapShiftX, mapShiftY, centerX, centerY, currentTime);
            }
        }

        /// <summary>
        /// Draw a hit effect at its position
        /// </summary>
        private void DrawHitEffect(SpriteBatch spriteBatch, ActiveHitEffect hitEffect,
            int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime)
        {
            if (hitEffect.Animation == null)
                return;

            var frame = hitEffect.Animation.GetFrameAtTime(hitEffect.AnimationTime(currentTime));
            if (frame?.Texture == null)
                return;

            int screenX = (int)hitEffect.X - mapShiftX + centerX;
            int screenY = (int)hitEffect.Y - mapShiftY + centerY;

            bool shouldFlip = hitEffect.FacingRight ^ frame.Flip;

            frame.Texture.DrawBackground(spriteBatch, null, null,
                GetFrameDrawX(screenX, frame, shouldFlip), screenY - frame.Origin.Y,
                Color.White, shouldFlip, null);
        }

        private void DrawCastEffect(SpriteBatch spriteBatch, SkillCastInfo cast,
            int mapShiftX, int mapShiftY, int centerX, int centerY)
        {
            var effect = cast.SkillData?.Effect;
            if (effect == null)
                return;

            var frame = effect.GetFrameAtTime(cast.AnimationTime);
            if (frame?.Texture == null)
                return;

            // WZ effect origins are relative to the caster, so keep the cast visual attached
            // to the player's live position instead of the position captured when casting started.
            int screenX = (int)_player.X - mapShiftX + centerX;
            int screenY = (int)_player.Y - mapShiftY + centerY;

            bool shouldFlip = _player.FacingRight ^ frame.Flip;

            frame.Texture.DrawBackground(spriteBatch, null, null,
                GetFrameDrawX(screenX, frame, shouldFlip), screenY - frame.Origin.Y,
                Color.White, shouldFlip, null);
        }

        /// <summary>
        /// Draw affected effect for active buff (loops while buff is active)
        /// </summary>
        private void DrawAffectedEffect(SpriteBatch spriteBatch, ActiveBuff buff,
            int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime)
        {
            var affected = buff.SkillData?.AffectedEffect;
            if (affected == null)
                return;

            // Calculate animation time - loop continuously
            int animTime = currentTime - buff.StartTime;
            var frame = affected.GetFrameAtTime(animTime);
            if (frame?.Texture == null)
                return;

            // Position at player
            int screenX = (int)_player.X - mapShiftX + centerX;
            int screenY = (int)_player.Y - mapShiftY + centerY;

            bool shouldFlip = _player.FacingRight ^ frame.Flip;

            frame.Texture.DrawBackground(spriteBatch, null, null,
                GetFrameDrawX(screenX, frame, shouldFlip), screenY - frame.Origin.Y,
                Color.White, shouldFlip, null);
        }

        private void DrawSummonEffect(SpriteBatch spriteBatch, ActiveSummon summon,
            int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime)
        {
            int elapsed = currentTime - summon.StartTime;
            var animation = ResolveSummonAnimation(summon.SkillData, elapsed, out int animationTime)
                ?? summon.SkillData?.AffectedEffect
                ?? summon.SkillData?.Effect;
            if (animation == null)
                return;

            var frame = animation.GetFrameAtTime(animationTime);
            if (frame?.Texture == null)
                return;

            Vector2 summonPosition = GetSummonPosition(summon);
            int screenX = (int)summonPosition.X - mapShiftX + centerX;
            int screenY = (int)summonPosition.Y - mapShiftY + centerY;
            bool shouldFlip = summon.FacingRight ^ frame.Flip;

            frame.Texture.DrawBackground(spriteBatch, null, null,
                GetFrameDrawX(screenX, frame, shouldFlip), screenY - frame.Origin.Y,
                Color.White, shouldFlip, null);
        }

        private static SkillAnimation ResolveSummonAnimation(SkillData skill, int elapsedTime, out int animationTime)
        {
            animationTime = Math.Max(0, elapsedTime);
            if (skill == null)
                return null;

            var spawnAnimation = skill.SummonSpawnAnimation;
            if (spawnAnimation?.Frames.Count > 0)
            {
                int spawnDuration = spawnAnimation.TotalDuration > 0
                    ? spawnAnimation.TotalDuration
                    : spawnAnimation.Frames.Sum(frame => frame.Delay);
                if (spawnDuration > 0 && elapsedTime < spawnDuration)
                {
                    animationTime = elapsedTime;
                    return spawnAnimation;
                }

                animationTime = Math.Max(0, elapsedTime - spawnDuration);
            }

            if (skill.SummonAnimation?.Frames.Count > 0)
            {
                return skill.SummonAnimation;
            }

            animationTime = Math.Max(0, elapsedTime);
            return spawnAnimation;
        }

        #endregion

        #region Utility

        private SkillData GetSkillData(int skillId)
        {
            return _loader.LoadSkill(skillId);
        }

        private static int GetFrameDrawX(int anchorX, SkillFrame frame, bool shouldFlip)
        {
            if (frame?.Texture == null)
                return anchorX;

            return shouldFlip
                ? anchorX - (frame.Texture.Width - frame.Origin.X)
                : anchorX - frame.Origin.X;
        }

        private Rectangle GetDefaultMeleeHitbox(SkillData skill, SkillLevelData levelData, bool facingRight)
        {
            int width = Math.Max(80, Math.Max(levelData?.Range ?? 0, GetAnimationWidth(skill)));
            int height = Math.Max(60, Math.Max(levelData?.RangeY ?? 0, GetAnimationHeight(skill)));
            int offsetX = facingRight ? 10 : -(width + 10);

            return new Rectangle(offsetX, -height - 10, width, height);
        }

        private Rectangle GetDefaultMagicHitbox(SkillData skill, SkillLevelData levelData, bool facingRight)
        {
            int width = Math.Max(120, Math.Max(levelData?.Range ?? 0, GetAnimationWidth(skill)));
            int height = Math.Max(80, Math.Max(levelData?.RangeY ?? 0, GetAnimationHeight(skill)));
            int offsetX = facingRight ? 20 : -(width + 20);

            return new Rectangle(offsetX, -height - 20, width, height);
        }

        private static int GetAnimationWidth(SkillData skill)
        {
            return GetMaxFrameDimension(skill?.Effect, frame => frame.Bounds.Width);
        }

        private static int GetAnimationHeight(SkillData skill)
        {
            return GetMaxFrameDimension(skill?.Effect, frame => frame.Bounds.Height);
        }

        private static int GetMaxFrameDimension(SkillAnimation animation, Func<SkillFrame, int> selector)
        {
            if (animation?.Frames == null || animation.Frames.Count == 0)
                return 0;

            int max = 0;
            foreach (var frame in animation.Frames)
            {
                if (frame == null)
                    continue;

                max = Math.Max(max, selector(frame));
            }

            return max;
        }

        public IEnumerable<SkillData> GetLearnedSkills()
        {
            foreach (var skill in _availableSkills)
            {
                if (GetSkillLevel(skill.SkillId) > 0)
                {
                    yield return skill;
                }
            }
        }

        public IEnumerable<SkillData> GetActiveSkills()
        {
            foreach (var skill in _availableSkills)
            {
                if (!skill.IsPassive && !skill.Invisible && GetSkillLevel(skill.SkillId) > 0)
                {
                    yield return skill;
                }
            }
        }

        public IEnumerable<SkillData> GetAllSkills()
        {
            return _availableSkills;
        }

        public PreparedSkill GetPreparedSkill() => _preparedSkill;

        /// <summary>
        /// Full clear - clears everything including skill levels and hotkeys.
        /// Use when completely disposing the skill system.
        /// </summary>
        public void Clear()
        {
            _projectiles.Clear();
            ClearSummonPuppets();
            _summons.Clear();
            _hitEffects.Clear();
            _preparedSkill = null;

            // Remove all buff effects
            foreach (var buff in _buffs)
            {
                ApplyBuffStats(buff, false);
            }
            _buffs.Clear();

            _cooldowns.Clear();
            _currentCast = null;
            _skillLevels.Clear();
            _skillHotkeys.Clear();
            _availableSkills.Clear();
        }

        /// <summary>
        /// Clear map-specific state but preserve persistent data.
        /// Preserves: skill levels, hotkeys, available skills, cooldowns.
        /// Clears: active projectiles, hit effects, current cast, buffs.
        /// Preserves: active summons and remaining summon durations.
        /// </summary>
        public void ClearMapState()
        {
            _projectiles.Clear();
            ClearSummonPuppets();
            _hitEffects.Clear();
            _currentCast = null;
            _preparedSkill = null;

            foreach (var buff in _buffs)
            {
                ApplyBuffStats(buff, false);
            }
            _buffs.Clear();

            // Clear map-specific references
            _mobPool = null;
            _combatEffects = null;

            // Note: We intentionally do NOT clear:
            // - _skillLevels (learned skills persist)
            // - _skillHotkeys (hotkey bindings persist)
            // - _availableSkills (job skills persist)
            // - _cooldowns (debatable - could reset or persist)
        }

        private void ClearActiveSkillState(bool clearBuffs)
        {
            _projectiles.Clear();
            ClearSummonPuppets();
            _summons.Clear();
            _hitEffects.Clear();
            _currentCast = null;
            _preparedSkill = null;

            if (!clearBuffs)
                return;

            foreach (var buff in _buffs)
            {
                ApplyBuffStats(buff, false);
            }
            _buffs.Clear();
        }

        private static bool IsFocusedSingleBookJob(int jobId)
        {
            return jobId >= 800 && jobId < 1000;
        }

        #endregion
    }
}
