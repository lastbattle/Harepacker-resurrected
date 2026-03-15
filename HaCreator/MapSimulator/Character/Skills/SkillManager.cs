using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Instance.Shapes;
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
        private sealed class SkillMountState
        {
            public int SkillId { get; init; }
            public int MountItemId { get; init; }
            public CharacterPart PreviousMount { get; init; }
        }

        private enum AttackResolutionMode
        {
            Melee,
            Magic,
            Projectile
        }

        private sealed class QueuedFollowUpAttack
        {
            public int SkillId { get; init; }
            public int Level { get; init; }
            public int ExecuteTime { get; init; }
            public int? TargetMobId { get; init; }
            public bool FacingRight { get; init; }
            public int RequiredWeaponCode { get; init; }
        }

        private sealed class DeferredSkillPayload
        {
            public SkillData Skill { get; init; }
            public int Level { get; init; }
            public int ExecuteTime { get; init; }
            public bool QueueFollowUps { get; init; }
            public int? PreferredTargetMobId { get; init; }
            public bool FacingRight { get; init; }
        }

        private sealed class RocketBoosterState
        {
            public SkillData Skill { get; init; }
            public int Level { get; init; }
            public bool HasLeftGround { get; set; }
            public int LandingAttackTime { get; set; } = int.MinValue;
        }

        private sealed class CycloneState
        {
            public SkillData Skill { get; init; }
            public int Level { get; init; }
            public int ExpireTime { get; init; }
            public int NextAttackTime { get; set; }
        }

        private sealed class SwallowState
        {
            public int SkillId { get; init; }
            public int Level { get; init; }
            public int TargetMobId { get; init; }
            public int DigestStartTime { get; init; }
            public int DigestCompleteTime { get; init; }
            public int NextWriggleTime { get; set; }
        }

        private sealed class RepeatSkillSustainState
        {
            public int SkillId { get; init; }
            public int ReturnSkillId { get; init; }
            public bool RestrictToNormalAttack { get; init; }
        }

        private sealed class ClientSkillTimer
        {
            public int SkillId { get; init; }
            public string Source { get; init; }
            public int ExpireTime { get; init; }
            public Action<int> OnExpired { get; init; }
        }

        private sealed class BuffTemporaryStatPresentation
        {
            public string Label { get; init; }
            public string DisplayName { get; init; }
            public string IconKey { get; init; }
        }

        private const string GenericBuffIconKey = "united/buff";
        private const float GroundedSummonVisualYOffset = 25f;
        private const int WildHunterSwallowSkillId = 33101005;
        private const int WildHunterSwallowBuffSkillId = 33101006;
        private const int SwallowDigestDurationMs = 3000;
        private const int SwallowWriggleIntervalMs = 500;

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
        private Func<float, float, float, FootholdLine> _footholdLookup;
        private Func<SkillData, bool> _fieldSkillRestrictionEvaluator;
        private Func<SkillData, string> _fieldSkillRestrictionMessageProvider;
        private Func<int, CharacterPart> _tamingMobLoader;

        // Active state
        private readonly List<ActiveProjectile> _projectiles = new();
        private readonly List<ActiveBuff> _buffs = new();
        private readonly List<ActiveSkillZone> _skillZones = new();
        private readonly List<ActiveSummon> _summons = new();
        private readonly List<ActiveHitEffect> _hitEffects = new();
        private readonly Dictionary<int, int> _cooldowns = new(); // skillId -> lastCastTime
        private PreparedSkill _preparedSkill;
        private SkillCastInfo _currentCast;
        private SkillMountState _activeSkillMount;
        private bool _buffControlledFlyingAbility;

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
        public Action<SkillData, string> OnFieldSkillCastRejected;
        public Action<ActiveProjectile, MobItem> OnProjectileHit;
        public Action<ActiveBuff> OnBuffApplied;
        public Action<ActiveBuff> OnBuffExpired;
        public Action<PreparedSkill> OnPreparedSkillStarted;
        public Action<PreparedSkill> OnPreparedSkillReleased;
        public Action<int, string> OnClientSkillTimerExpired;

        // References
        private MobPool _mobPool;
        private DropPool _dropPool;
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
        public void SetDropPool(DropPool dropPool) => _dropPool = dropPool;
        public void SetCombatEffects(CombatEffects effects) => _combatEffects = effects;
        public void SetSoundManager(SoundManager soundManager) => _soundManager = soundManager;
        public void SetFootholdLookup(Func<float, float, float, FootholdLine> footholdLookup) => _footholdLookup = footholdLookup;
        public void SetFieldSkillRestrictionEvaluator(Func<SkillData, bool> evaluator) => _fieldSkillRestrictionEvaluator = evaluator;
        public void SetFieldSkillRestrictionMessageProvider(Func<SkillData, string> provider) => _fieldSkillRestrictionMessageProvider = provider;
        public void SetTamingMobLoader(Func<int, CharacterPart> loader) => _tamingMobLoader = loader;

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
            if (level <= 0)
            {
                _skillLevels.Remove(skillId);
            }
            else
            {
                _skillLevels[skillId] = level;
            }

            RevalidateHotkeys();
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
            TrySetHotkey(slotIndex, skillId);
        }

        /// <summary>
        /// Try to set a skill hotkey by absolute slot index (0-27).
        /// Returns false when the requested skill is not a learned, visible active skill.
        /// </summary>
        public bool TrySetHotkey(int slotIndex, int skillId)
        {
            if (slotIndex < 0 || slotIndex >= TOTAL_SLOT_COUNT)
                return false;

            if (skillId <= 0)
            {
                _skillHotkeys.Remove(slotIndex);
                return true;
            }

            if (!CanAssignHotkeySkill(skillId))
                return false;

            _skillHotkeys[slotIndex] = skillId;
            return true;
        }

        /// <summary>
        /// Get skill on hotkey by absolute slot index (0-27)
        /// </summary>
        public int GetHotkeySkill(int slotIndex)
        {
            if (!_skillHotkeys.TryGetValue(slotIndex, out int skillId))
                return 0;

            if (CanAssignHotkeySkill(skillId))
                return skillId;

            _skillHotkeys.Remove(slotIndex);
            return 0;
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
            RevalidateHotkeys();

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
            RevalidateHotkeys();
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
                TrySetHotkey(kv.Key, kv.Value);
            }
        }

        /// <summary>
        /// Whether the given skill can appear in a quick slot.
        /// Mirrors the client skill-side validation by rejecting unlearned, passive, and hidden skills.
        /// </summary>
        public bool CanAssignHotkeySkill(int skillId)
        {
            if (skillId <= 0)
                return false;

            SkillData skill = FindKnownSkillData(skillId);
            if (skill == null || skill.IsPassive || skill.Invisible)
                return false;

            return GetSkillLevel(skillId) > 0;
        }

        /// <summary>
        /// Revalidates quick-slot assignments against the current learned skill state.
        /// Returns the number of removed stale assignments.
        /// </summary>
        public int RevalidateHotkeys()
        {
            int removed = 0;

            foreach (int slotIndex in _skillHotkeys.Keys.ToList())
            {
                if (CanAssignHotkeySkill(_skillHotkeys[slotIndex]))
                    continue;

                _skillHotkeys.Remove(slotIndex);
                removed++;
            }

            return removed;
        }

        /// <summary>
        /// Clear all hotkeys
        /// </summary>
        public void ClearAllHotkeys()
        {
            _skillHotkeys.Clear();
        }

        private SkillData FindKnownSkillData(int skillId)
        {
            foreach (var skill in _availableSkills)
            {
                if (skill?.SkillId == skillId)
                    return skill;
            }

            return _loader?.LoadSkill(skillId);
        }

        #endregion

        #region Skill Casting

        // Skill queue for macro execution
        private readonly Queue<int> _skillQueue = new();
        private readonly Queue<QueuedFollowUpAttack> _queuedFollowUpAttacks = new();
        private readonly Queue<DeferredSkillPayload> _deferredSkillPayloads = new();
        private readonly List<ClientSkillTimer> _clientSkillTimers = new();
        private int _lastQueuedSkillTime = 0;
        private const int SKILL_QUEUE_DELAY = 100; // ms between queued skill attempts
        private const int FOLLOW_UP_ATTACK_DELAY = 90;
        private const int ROCKET_BOOSTER_SKILL_ID = 35101004;
        private const int ROCKET_BOOSTER_LANDING_RECOVERY_MS = 500;
        private const int MECHANIC_TAMING_MOB_ID = 1932016;
        private const int MECHANIC_KEYDOWN_MAX_DURATION_MS = 8000;
        private const int CYCLONE_SKILL_ID = 32121003;
        private const int MINE_SKILL_ID = 33101008;
        private const int SG88_SKILL_ID = 35111002;
        private const int HEALING_ROBOT_SKILL_ID = 35111011;
        private const int MINE_DEPLOY_INTERVAL_MS = 1500;
        private const int CYCLONE_ATTACK_INTERVAL_MS = 1000;
        private const int SMOKE_BOMB_SKILL_ID = 4221006;

        private RocketBoosterState _rocketBoosterState;
        private CycloneState _cycloneState;
        private SwallowState _swallowState;
        private int _mineMovementDirection;
        private int _mineMovementStartTime;
        private RepeatSkillSustainState _activeRepeatSkillSustain;

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

            if (TryToggleRepeatSkillManualAssist(skillId, currentTime))
            {
                return true;
            }

            if (_fieldSkillRestrictionEvaluator != null && !_fieldSkillRestrictionEvaluator(skill))
            {
                string message = _fieldSkillRestrictionMessageProvider?.Invoke(skill);
                if (!string.IsNullOrWhiteSpace(message))
                {
                    OnFieldSkillCastRejected?.Invoke(skill, message);
                }
                return false;
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

        public void ReleaseHotkeyIfActive(int keyIndex, int currentTime)
        {
            if (_preparedSkill?.IsKeydownSkill != true)
                return;

            int skillId = GetHotkeySkill(keyIndex);
            if (skillId > 0 && skillId == _preparedSkill.SkillId)
            {
                ReleasePreparedSkill(currentTime);
            }
        }

        public void ReleaseActiveKeydownSkill(int currentTime)
        {
            if (_preparedSkill?.IsKeydownSkill == true)
            {
                ReleasePreparedSkill(currentTime);
            }
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

            if (!IsSkillAllowedForCurrentJob(skill))
                return false;

            if (_fieldSkillRestrictionEvaluator != null && !_fieldSkillRestrictionEvaluator(skill))
                return false;

            if (CanToggleRepeatSkillManualAssist(skillId))
                return true;

            if (IsSkillCastBlockedByRepeatSkillSustain())
                return false;

            // Check cooldown
            if (IsOnCooldown(skillId, currentTime))
                return false;

            // Check MP
            var levelData = skill.GetLevel(level);
            if (levelData == null)
                return false;

            if (!CanAffordSkillCost(levelData))
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

        /// <summary>
        /// Get the tick count when the current cooldown started.
        /// </summary>
        public bool TryGetCooldownStartTime(int skillId, out int startTime)
        {
            return _cooldowns.TryGetValue(skillId, out startTime);
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
                EffectAnimation = GetInitialCastEffect(skill),
                CastTime = currentTime,
                CasterId = 0, // Player ID
                CasterX = _player.X,
                CasterY = _player.Y,
                FacingRight = _player.FacingRight
            };

            string prepareActionName = skill.IsKeydownSkill ? GetPrepareActionName(skill) : null;
            int repeatReturnSkillId = ResolveRepeatSkillReturnSkillId(skill.SkillId);

            ApplySkillCooldown(skill, levelData, currentTime);
            ApplySkillMount(skill, levelData);
            TriggerSkillAnimation(skill, prepareActionName);
            BeginRepeatSkillSustain(skill, levelData, currentTime, repeatReturnSkillId);
            if (skill.IsKeydownSkill)
            {
                _player.BeginSustainedSkillAnimation(prepareActionName);
            }

            if (_currentCast != null && TryApplyClientOwnedAvatarEffect(skill, currentTime))
            {
                _currentCast.SuppressEffectAnimation = true;
            }
            PlayCastSound(skill);
            OnSkillCast?.Invoke(_currentCast);

            if (skill.IsKeydownSkill)
            {
                BeginPreparedSkill(skill, level, currentTime);
                return;
            }

            if (!TryConsumeSkillResources(levelData))
                return;

            if (skill.IsPrepareSkill)
            {
                BeginPreparedSkill(skill, level, currentTime);
                return;
            }

            ExecuteSkillPayload(skill, level, currentTime);
        }

        private void ApplySkillCooldown(SkillData skill, SkillLevelData levelData, int currentTime)
        {
            if (levelData.Cooldown > 0)
            {
                _cooldowns[skill.SkillId] = currentTime;
            }
        }

        private bool TryConsumeSkillResources(SkillLevelData levelData)
        {
            if (levelData == null || !CanAffordSkillCost(levelData))
                return false;

            _player.MP = Math.Max(0, _player.MP - levelData.MpCon);

            if (levelData.HpCon > 0)
            {
                _player.HP = Math.Max(1, _player.HP - levelData.HpCon);
            }

            return true;
        }

        private bool CanAffordSkillCost(SkillLevelData levelData)
        {
            if (levelData == null)
                return false;

            if (_player.MP < levelData.MpCon)
                return false;

            return _player.HP > levelData.HpCon;
        }

        private void TriggerSkillAnimation(SkillData skill, string actionNameOverride = null)
        {
            string actionName = ResolveSkillActionName(skill, actionNameOverride);

            _player.ApplySkillAvatarTransform(skill.SkillId, actionName);
            _player.TriggerSkillAnimation(actionName);
        }

        private static string ResolveSkillActionName(SkillData skill, string actionNameOverride = null)
        {
            if (!string.IsNullOrWhiteSpace(actionNameOverride))
                return actionNameOverride;

            if (!string.IsNullOrWhiteSpace(skill?.ActionName))
                return skill.ActionName;

            return skill?.AttackType switch
            {
                SkillAttackType.Ranged => "shoot1",
                SkillAttackType.Magic => "swingO1",
                _ => "attack1"
            };
        }

        private void ApplySkillMount(SkillData skill, SkillLevelData levelData)
        {
            int mountItemId = ResolveSkillMountItemId(skill, levelData);
            if (_player.Build == null || _tamingMobLoader == null || mountItemId <= 0)
            {
                return;
            }

            CharacterPart mountPart = _tamingMobLoader(mountItemId);
            if (mountPart?.Slot != EquipSlot.TamingMob)
            {
                return;
            }

            if (_activeSkillMount != null && _activeSkillMount.MountItemId == mountItemId)
            {
                _player.Build.Equip(mountPart);
                _activeSkillMount = new SkillMountState
                {
                    SkillId = skill.SkillId,
                    MountItemId = mountItemId,
                    PreviousMount = _activeSkillMount.PreviousMount
                };
                return;
            }

            CharacterPart previousMount = _activeSkillMount?.PreviousMount;
            if (previousMount == null)
            {
                _player.Build.Equipment.TryGetValue(EquipSlot.TamingMob, out previousMount);
            }

            _player.Build.Equip(mountPart);
            _activeSkillMount = new SkillMountState
            {
                SkillId = skill.SkillId,
                MountItemId = mountItemId,
                PreviousMount = previousMount
            };
        }

        private static int ResolveSkillMountItemId(SkillData skill, SkillLevelData levelData)
        {
            if (levelData?.ItemConNo > 0)
            {
                return levelData.ItemConNo;
            }

            return IsMechanicVehicleSkill(skill?.SkillId ?? 0)
                ? MECHANIC_TAMING_MOB_ID
                : 0;
        }

        private static bool IsMechanicVehicleSkill(int skillId)
        {
            return skillId == 35001001
                   || skillId == 35101009
                   || skillId == 35111004
                   || skillId == 35121005
                   || skillId == 35121013;
        }

        private void TransferSkillMountOwnership(int previousSkillId, int nextSkillId)
        {
            if (_activeSkillMount == null || _activeSkillMount.SkillId != previousSkillId)
            {
                return;
            }

            _activeSkillMount = new SkillMountState
            {
                SkillId = nextSkillId,
                MountItemId = _activeSkillMount.MountItemId,
                PreviousMount = _activeSkillMount.PreviousMount
            };
        }

        private void ClearSkillMount()
        {
            if (_activeSkillMount == null || _player.Build == null)
            {
                return;
            }

            if (_activeSkillMount.PreviousMount != null)
            {
                _player.Build.Equip(_activeSkillMount.PreviousMount);
            }
            else
            {
                _player.Build.Unequip(EquipSlot.TamingMob);
            }

            _activeSkillMount = null;
        }

        private void ClearSkillMount(int skillId)
        {
            if (_activeSkillMount != null && _activeSkillMount.SkillId == skillId)
            {
                ClearSkillMount();
            }
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

        private void PlayRepeatSound(SkillData skill)
        {
            if (skill == null || _soundManager == null)
                return;

            string soundKey = _loader.EnsureRepeatSoundRegistered(skill, _soundManager);
            if (!string.IsNullOrEmpty(soundKey))
            {
                _soundManager.PlaySound(soundKey);
            }
        }

        private static bool IsRepeatSkillSustainFamily(int skillId)
        {
            return skillId == 35111004 || skillId == 35121013;
        }

        private bool CanToggleRepeatSkillManualAssist(int skillId)
        {
            return skillId == SG88_SKILL_ID
                   && _currentCast?.IsComplete != false
                   && _preparedSkill == null
                   && FindActiveSummon(skillId) != null;
        }

        private bool TryToggleRepeatSkillManualAssist(int skillId, int currentTime)
        {
            if (!CanToggleRepeatSkillManualAssist(skillId))
            {
                return false;
            }

            ActiveSummon summon = FindActiveSummon(skillId);
            if (summon == null)
            {
                return false;
            }

            summon.ManualAssistEnabled = !summon.ManualAssistEnabled;
            summon.LastAttackTime = currentTime;
            summon.LastAttackAnimationStartTime = int.MinValue;
            return true;
        }

        private ActiveSummon FindActiveSummon(int skillId)
        {
            return _summons.FirstOrDefault(summon => summon?.SkillId == skillId);
        }

        private int ResolveRepeatSkillReturnSkillId(int skillId)
        {
            return skillId == 35121013 && _player.HasSkillAvatarTransform(35121005)
                ? 35121005
                : 0;
        }

        private void BeginRepeatSkillSustain(SkillData skill, SkillLevelData levelData, int currentTime, int returnSkillId)
        {
            if (!IsRepeatSkillSustainFamily(skill?.SkillId ?? 0) || levelData == null)
            {
                ClearRepeatSkillSustain();
                return;
            }

            int durationMs = levelData.Time > 0 ? levelData.Time * 1000 : 0;
            if (durationMs <= 0)
            {
                ClearRepeatSkillSustain();
                return;
            }

            _activeRepeatSkillSustain = new RepeatSkillSustainState
            {
                SkillId = skill.SkillId,
                ReturnSkillId = returnSkillId,
                RestrictToNormalAttack = skill.OnlyNormalAttackInState
            };

            RegisterClientSkillTimer(
                skill.SkillId,
                "repeat-sustain-end",
                currentTime + durationMs,
                ExpireRepeatSkillSustain);
        }

        private void ClearRepeatSkillSustain()
        {
            if (_activeRepeatSkillSustain == null)
                return;

            CancelClientSkillTimers(_activeRepeatSkillSustain.SkillId, "repeat-sustain-end");
            _activeRepeatSkillSustain = null;
        }

        private void ExpireRepeatSkillSustain(int currentTime)
        {
            if (_activeRepeatSkillSustain == null)
                return;

            RepeatSkillSustainState sustain = _activeRepeatSkillSustain;
            _activeRepeatSkillSustain = null;

            if (!_player.HasSkillAvatarTransform(sustain.SkillId))
                return;

            _player.ClearSkillAvatarTransform(sustain.SkillId);
            TransferSkillMountOwnership(sustain.SkillId, sustain.ReturnSkillId);

            if (sustain.ReturnSkillId > 0 && _player.IsAlive)
            {
                _player.ApplySkillAvatarTransform(sustain.ReturnSkillId, actionName: null);
                _player.ForceStand();
            }
        }

        private bool IsSkillCastBlockedByRepeatSkillSustain()
        {
            if (_activeRepeatSkillSustain == null || !_activeRepeatSkillSustain.RestrictToNormalAttack)
            {
                return false;
            }

            return _player.HasSkillAvatarTransform(_activeRepeatSkillSustain.SkillId);
        }

        private void ExecuteSkillPayload(
            SkillData skill,
            int level,
            int currentTime,
            bool queueFollowUps = true,
            int? preferredTargetMobId = null,
            bool allowDeferredExecution = true,
            bool? facingRightOverride = null)
        {
            bool facingRight = facingRightOverride ?? _player.FacingRight;

            if (allowDeferredExecution && TryScheduleDeferredSkillPayload(skill, level, currentTime, queueFollowUps, preferredTargetMobId))
            {
                return;
            }

            if (TryExecuteClientSkillBranch(skill, level, currentTime))
            {
                return;
            }

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

            if (TryExecuteMesoExplosionAttack(skill, level, currentTime, facingRight))
                return;

            if (skill.Projectile != null)
            {
                SpawnProjectile(skill, level, currentTime, facingRight, queueFollowUps, preferredTargetMobId);
                return;
            }

            if (skill.AttackType == SkillAttackType.Magic)
            {
                ProcessMagicAttack(skill, level, currentTime, facingRight, queueFollowUps, preferredTargetMobId);
                return;
            }

            ProcessMeleeAttack(skill, level, currentTime, facingRight, queueFollowUps, preferredTargetMobId);
        }

        private bool TryExecuteClientSkillBranch(SkillData skill, int level, int currentTime)
        {
            if (skill == null)
            {
                return false;
            }

            if (IsInvincibleZoneSkill(skill))
            {
                return TryExecuteInvincibleZoneBranch(skill, level, currentTime);
            }

            if (IsSwallowSkill(skill))
            {
                return TryExecuteSwallowBranch(skill, level, currentTime);
            }

            if (skill.SkillId == CYCLONE_SKILL_ID)
            {
                StartCyclone(skill, level, currentTime);
                return true;
            }

            return false;
        }

        private bool TryExecuteInvincibleZoneBranch(SkillData skill, int level, int currentTime)
        {
            SkillLevelData levelData = skill?.GetLevel(level);
            if (skill == null || levelData == null)
            {
                return true;
            }

            Rectangle worldBounds = GetWorldAttackHitbox(skill, level, levelData, AttackResolutionMode.Melee, _player.FacingRight);
            if (worldBounds.Width <= 0 || worldBounds.Height <= 0)
            {
                return true;
            }

            StartSkillZone(skill, level, levelData, currentTime, worldBounds);
            return true;
        }

        private bool TryExecuteSwallowBranch(SkillData skill, int level, int currentTime)
        {
            SkillLevelData levelData = skill?.GetLevel(level);
            if (skill == null || levelData == null)
            {
                return true;
            }

            if (skill.SkillId == WildHunterSwallowSkillId)
            {
                return TryExecuteWildHunterSwallow(skill, level, levelData, currentTime);
            }

            Rectangle worldHitbox = GetWorldAttackHitbox(skill, level, levelData, AttackResolutionMode.Melee, _player.FacingRight);
            if (worldHitbox.Width <= 0 || worldHitbox.Height <= 0)
            {
                return true;
            }

            MobItem target = ResolveTargetsInHitbox(
                    worldHitbox,
                    currentTime,
                    maxTargets: 1,
                    AttackResolutionMode.Melee,
                    preferredTargetMobId: null)
                .FirstOrDefault();

            if (target == null)
            {
                ClearSwallowState();
                return true;
            }

            SpawnHitEffect(skill, target.MovementInfo?.X ?? _player.X, (target.MovementInfo?.Y ?? _player.Y) - 20f, currentTime);
            HandleMobDeath(target, currentTime, MobDeathType.Swallowed);

            if (ShouldApplySwallowBuff(skill, levelData))
            {
                ApplyBuff(skill, level, currentTime);
                ActiveBuff activeBuff = _buffs.LastOrDefault(buff => buff.SkillId == skill.SkillId);
                _swallowState = activeBuff != null
                    ? new SwallowState
                    {
                        SkillId = skill.SkillId
                    }
                    : null;
            }
            else
            {
                ClearSwallowState();
            }

            return true;
        }

        private bool TryExecuteWildHunterSwallow(SkillData skill, int level, SkillLevelData levelData, int currentTime)
        {
            if (_swallowState != null)
            {
                UpdateSwallowState(currentTime);
                return true;
            }

            Rectangle worldHitbox = GetWorldAttackHitbox(skill, level, levelData, AttackResolutionMode.Melee, _player.FacingRight);
            if (worldHitbox.Width <= 0 || worldHitbox.Height <= 0)
            {
                return true;
            }

            MobItem target = ResolveTargetsInHitbox(
                    worldHitbox,
                    currentTime,
                    maxTargets: 1,
                    AttackResolutionMode.Melee,
                    preferredTargetMobId: null)
                .FirstOrDefault();

            if (target == null)
            {
                ClearSwallowState();
                return true;
            }

            target.AI?.ApplyStatusEffect(
                MobStatusEffect.Stun,
                SwallowDigestDurationMs + SwallowWriggleIntervalMs,
                currentTime);

            SpawnHitEffect(skill, target.MovementInfo?.X ?? _player.X, (target.MovementInfo?.Y ?? _player.Y) - 20f, currentTime);
            _swallowState = new SwallowState
            {
                SkillId = skill.SkillId,
                Level = level,
                TargetMobId = target.PoolId,
                DigestStartTime = currentTime,
                DigestCompleteTime = currentTime + SwallowDigestDurationMs,
                NextWriggleTime = currentTime + SwallowWriggleIntervalMs
            };

            return true;
        }

        private static bool ShouldApplySwallowBuff(SkillData skill, SkillLevelData levelData)
        {
            return levelData?.Time > 0
                   && (skill?.IsBuff == true
                       || skill?.Type == SkillType.Buff
                       || skill?.Type == SkillType.PartyBuff);
        }

        private bool TryExecuteMesoExplosionAttack(SkillData skill, int level, int currentTime, bool facingRight)
        {
            if (skill?.IsMesoExplosion != true || _dropPool == null)
                return false;

            SkillLevelData levelData = skill.GetLevel(level);
            if (levelData == null)
                return true;

            Rectangle worldHitbox = GetWorldAttackHitbox(skill, level, levelData, AttackResolutionMode.Melee, facingRight);
            if (worldHitbox.Width <= 0 || worldHitbox.Height <= 0)
                return true;

            List<DropItem> explosiveDrops = _dropPool.GetExplosiveDropInRect(
                worldHitbox.Left + (worldHitbox.Width * 0.5f),
                worldHitbox.Top + (worldHitbox.Height * 0.5f),
                worldHitbox.Width,
                worldHitbox.Height,
                playerId: 0,
                currentTime,
                maxCount: Math.Max(1, levelData.AttackCount));

            if (explosiveDrops.Count == 0)
                return true;

            _dropPool.ConsumeMesosForExplosion(explosiveDrops, currentTime);

            foreach (DropItem explosiveDrop in explosiveDrops)
            {
                SpawnHitEffect(skill, explosiveDrop.X, explosiveDrop.Y, currentTime, facingRight);
            }

            if (_mobPool == null)
                return true;

            List<MobItem> targets = ResolveTargetsInHitbox(
                worldHitbox,
                currentTime,
                Math.Max(1, levelData.MobCount),
                AttackResolutionMode.Melee,
                preferredTargetMobId: null);

            if (targets.Count == 0)
                return true;

            int attackCount = Math.Min(Math.Max(1, explosiveDrops.Count), Math.Max(1, levelData.AttackCount));
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
                    AttackResolutionMode.Melee,
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

            return true;
        }

        private bool TryScheduleDeferredSkillPayload(
            SkillData skill,
            int level,
            int currentTime,
            bool queueFollowUps,
            int? preferredTargetMobId)
        {
            if (skill == null)
            {
                return false;
            }

            if (IsRocketBoosterSkill(skill))
            {
                _rocketBoosterState = null;
                _deferredSkillPayloads.Enqueue(new DeferredSkillPayload
                {
                    Skill = skill,
                    Level = level,
                    ExecuteTime = currentTime + GetRocketBoosterLaunchDelayMs(skill),
                    QueueFollowUps = false,
                    PreferredTargetMobId = preferredTargetMobId,
                    FacingRight = _player.FacingRight
                });
                return true;
            }

            if (!ShouldUseSmoothingMovingShoot(skill))
            {
                return false;
            }

            preferredTargetMobId ??= ResolveDeferredPreferredTargetMobId(skill, level, currentTime);

            _deferredSkillPayloads.Enqueue(new DeferredSkillPayload
            {
                Skill = skill,
                Level = level,
                ExecuteTime = currentTime + GetMovingShootDelayMs(skill),
                QueueFollowUps = queueFollowUps,
                PreferredTargetMobId = preferredTargetMobId,
                FacingRight = _player.FacingRight
            });
            return true;
        }

        private int? ResolveDeferredPreferredTargetMobId(SkillData skill, int level, int currentTime)
        {
            if (_mobPool == null || skill?.IsAttack != true)
            {
                return null;
            }

            SkillLevelData levelData = skill.GetLevel(level);
            if (levelData == null)
            {
                return null;
            }

            AttackResolutionMode mode = ResolveDeferredTargetingMode(skill);
            Rectangle worldHitbox = GetWorldAttackHitbox(skill, level, levelData, mode, _player.FacingRight);
            if (worldHitbox.Width <= 0 || worldHitbox.Height <= 0)
            {
                return null;
            }

            return ResolveTargetsInHitbox(worldHitbox, currentTime, 1, mode, preferredTargetMobId: null)
                .FirstOrDefault()
                ?.PoolId;
        }

        private static AttackResolutionMode ResolveDeferredTargetingMode(SkillData skill)
        {
            if (skill?.AttackType == SkillAttackType.Magic)
            {
                return AttackResolutionMode.Magic;
            }

            return skill?.Projectile != null || skill?.AttackType == SkillAttackType.Ranged
                ? AttackResolutionMode.Projectile
                : AttackResolutionMode.Melee;
        }

        private void BeginPreparedSkill(SkillData skill, int level, int currentTime)
        {
            var levelData = skill.GetLevel(level);
            int durationMs = GetPrepareDuration(skill, levelData);
            KeyDownHudProfile hudProfile = ResolveKeyDownHudProfile(skill?.SkillId ?? 0);

            _preparedSkill = new PreparedSkill
            {
                SkillId = skill.SkillId,
                Level = level,
                StartTime = currentTime,
                Duration = durationMs,
                MaxHoldDurationMs = ResolveKeyDownMaxHoldDuration(skill?.SkillId ?? 0),
                HudGaugeDurationMs = hudProfile.GaugeDurationMs,
                HudSkinKey = hudProfile.SkinKey,
                ShowHudBar = hudProfile.Visible,
                SkillData = skill,
                LevelData = levelData,
                IsKeydownSkill = skill.IsKeydownSkill
            };

            if (!_preparedSkill.IsKeydownSkill && _preparedSkill.Duration > 0)
            {
                RegisterClientSkillTimer(
                    _preparedSkill.SkillId,
                    "prepared-release",
                    currentTime + _preparedSkill.Duration,
                    ReleasePreparedSkill);
            }

            OnPreparedSkillStarted?.Invoke(_preparedSkill);
        }

        private int GetPrepareDuration(SkillData skill, SkillLevelData levelData)
        {
            if (skill?.PrepareDurationMs > 0)
                return skill.PrepareDurationMs;

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
            CancelClientSkillTimers(prepared.SkillId, "prepared-release");

            if (prepared.IsKeydownSkill)
            {
                _player.EndSustainedSkillAnimation();
                UpdateCurrentCastEffect(prepared.SkillData?.KeydownEndEffect, currentTime);
                bool hadTransform = _player.HasSkillAvatarTransform(prepared.SkillId);
                _player.ClearSkillAvatarTransform(prepared.SkillId);

                string endActionName = GetKeydownEndActionName(prepared.SkillData);
                if (!hadTransform
                    && !string.IsNullOrWhiteSpace(endActionName)
                    && _player.IsAlive)
                {
                    _player.TriggerSkillAnimation(endActionName);
                }

                ClearSkillMount(prepared.SkillId);
                OnPreparedSkillReleased?.Invoke(prepared);
                return;
            }

            ExecuteSkillPayload(prepared.SkillData, prepared.Level, currentTime);
            _player.ClearSkillAvatarTransform(prepared.SkillId);
            ClearSkillMount(prepared.SkillId);
            OnPreparedSkillReleased?.Invoke(prepared);
        }

        private void UpdateKeydownSkill(int currentTime)
        {
            if (_preparedSkill?.IsKeydownSkill != true)
                return;

            PreparedSkill prepared = _preparedSkill;
            if (prepared.MaxHoldDurationMs > 0
                && prepared.IsHolding
                && prepared.HoldElapsed(currentTime) >= prepared.MaxHoldDurationMs)
            {
                ReleasePreparedSkill(currentTime);
                return;
            }

            if (!prepared.IsHolding)
            {
                if (prepared.Duration > 0 && prepared.Elapsed(currentTime) < prepared.Duration)
                    return;

                prepared.IsHolding = true;
                prepared.HoldStartTime = currentTime;
                prepared.LastRepeatTime = currentTime - GetKeydownRepeatInterval(prepared.SkillData);
                UpdateCurrentCastEffect(prepared.SkillData?.KeydownEffect, currentTime);
                _player.BeginSustainedSkillAnimation(GetKeydownActionName(prepared.SkillData));
            }

            if (!_player.IsAlive || !_player.CanAttack)
            {
                ReleasePreparedSkill(currentTime);
                return;
            }

            if (_fieldSkillRestrictionEvaluator != null && !_fieldSkillRestrictionEvaluator(prepared.SkillData))
            {
                ReleasePreparedSkill(currentTime);
                return;
            }

            int repeatInterval = GetKeydownRepeatInterval(prepared.SkillData);
            while (_preparedSkill != null && currentTime - prepared.LastRepeatTime >= repeatInterval)
            {
                if (!TryConsumeSkillResources(prepared.LevelData))
                {
                    ReleasePreparedSkill(currentTime);
                    return;
                }

                PlayRepeatSound(prepared.SkillData);
                ExecuteSkillPayload(prepared.SkillData, prepared.Level, currentTime);
                prepared.LastRepeatTime += repeatInterval;
            }
        }

        private static string GetPrepareActionName(SkillData skill)
        {
            if (!string.IsNullOrWhiteSpace(skill?.PrepareActionName))
                return skill.PrepareActionName;

            return GetKeydownActionName(skill);
        }

        private static string GetKeydownActionName(SkillData skill)
        {
            if (!string.IsNullOrWhiteSpace(skill?.KeydownActionName))
                return skill.KeydownActionName;

            return ResolveSkillActionName(skill);
        }

        private static string GetKeydownEndActionName(SkillData skill)
        {
            if (!string.IsNullOrWhiteSpace(skill?.KeydownEndActionName))
                return skill.KeydownEndActionName;

            return null;
        }

        private static KeyDownHudProfile ResolveKeyDownHudProfile(int skillId)
        {
            return skillId switch
            {
                35121003 => new KeyDownHudProfile(true, "KeyDownBar4", 2000),
                4341002 => new KeyDownHudProfile(true, "KeyDownBar1", 600),
                5101004 => new KeyDownHudProfile(true, "KeyDownBar1", 1000),
                15101003 => new KeyDownHudProfile(true, "KeyDownBar1", 1000),
                14111006 => new KeyDownHudProfile(true, "KeyDownBar", 1000),
                2121001 => new KeyDownHudProfile(true, "KeyDownBar", 1000),
                2221001 => new KeyDownHudProfile(true, "KeyDownBar", 1000),
                2321001 => new KeyDownHudProfile(true, "KeyDownBar", 1000),
                3121004 => new KeyDownHudProfile(false, "KeyDownBar", 2000),
                3221001 => new KeyDownHudProfile(true, "KeyDownBar", 900),
                4341003 => new KeyDownHudProfile(true, "KeyDownBar", 1200),
                5201002 => new KeyDownHudProfile(false, "KeyDownBar", 1000),
                13111002 => new KeyDownHudProfile(false, "KeyDownBar", 1000),
                22121000 => new KeyDownHudProfile(false, "KeyDownBar", 500),
                22151001 => new KeyDownHudProfile(false, "KeyDownBar", 500),
                33101005 => new KeyDownHudProfile(true, "KeyDownBar", 900),
                33121009 => new KeyDownHudProfile(false, "KeyDownBar", 2000),
                35001001 => new KeyDownHudProfile(false, "KeyDownBar", 2000),
                35101009 => new KeyDownHudProfile(false, "KeyDownBar", 2000),
                _ => new KeyDownHudProfile(true, "KeyDownBar", 0)
            };
        }

        private static int ResolveKeyDownMaxHoldDuration(int skillId)
        {
            return skillId switch
            {
                35001001 => MECHANIC_KEYDOWN_MAX_DURATION_MS,
                35101009 => MECHANIC_KEYDOWN_MAX_DURATION_MS,
                _ => 0
            };
        }

        private readonly struct KeyDownHudProfile
        {
            public KeyDownHudProfile(bool visible, string skinKey, int gaugeDurationMs)
            {
                Visible = visible;
                SkinKey = string.IsNullOrWhiteSpace(skinKey) ? "KeyDownBar" : skinKey;
                GaugeDurationMs = gaugeDurationMs;
            }

            public bool Visible { get; }
            public string SkinKey { get; }
            public int GaugeDurationMs { get; }
        }

        private static SkillAnimation GetInitialCastEffect(SkillData skill)
        {
            if (skill == null)
                return null;

            return skill.IsKeydownSkill
                ? skill.PrepareEffect ?? skill.Effect
                : skill.Effect;
        }

        private void UpdateCurrentCastEffect(SkillAnimation animation, int currentTime)
        {
            if (_currentCast == null || animation == null)
                return;

            _currentCast.EffectAnimation = animation;
            _currentCast.CastTime = currentTime;
            _currentCast.IsComplete = false;
        }

        private static int GetKeydownRepeatInterval(SkillData skill)
        {
            if (skill?.KeydownRepeatIntervalMs > 0)
                return skill.KeydownRepeatIntervalMs;

            if (skill?.KeydownDurationMs > 0)
                return skill.KeydownDurationMs;

            return 90;
        }

        private bool TryApplyClientOwnedAvatarEffect(SkillData skill, int currentTime)
        {
            if (_player == null || skill == null)
            {
                return false;
            }

            if (skill.Effect != null && IsDoubleJumpAction(skill.ActionName))
            {
                return _player.ApplyTransientUnderFaceSkillAvatarEffect(skill.SkillId, skill.Effect, currentTime);
            }

            return UsesFlightBuffAvatarEffectFallback(skill);
        }

        private static bool IsDoubleJumpAction(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                   && actionName.IndexOf("doublejump", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool UsesFlightBuffAvatarEffectFallback(SkillData skill)
        {
            return skill?.IsBuff == true
                   && skill.Effect != null
                   && !skill.HasPersistentAvatarEffect
                   && SkillGrantsFlyingAbility(skill);
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

        private void ExecuteRocketBoosterLaunch(SkillData skill, int level)
        {
            SkillLevelData levelData = skill?.GetLevel(level);
            if (levelData == null)
            {
                return;
            }

            int horizontalRange = Math.Max(
                Math.Max(levelData.RangeR, levelData.RangeL),
                Math.Max(levelData.Range, Math.Max(levelData.X, levelData.Y)));

            if (horizontalRange <= 0)
            {
                horizontalRange = 160;
            }

            float direction = _player.FacingRight ? 1f : -1f;
            float rushSpeed = Math.Max(320f, horizontalRange * 2.4f);
            float verticalSpeed = Math.Max(260f, Math.Abs(levelData.RangeTop) * 3f);

            string rocketBoosterActionName = string.Equals(_player.CurrentActionName, "tank_rbooster_pre", StringComparison.OrdinalIgnoreCase)
                ? "tank_rbooster_pre"
                : "rbooster";

            _player.ApplySkillAvatarTransform(skill.SkillId, rocketBoosterActionName);
            _player.Physics.Jump();
            _player.Physics.SetVelocity(rushSpeed * direction, -verticalSpeed);
            _rocketBoosterState = new RocketBoosterState
            {
                Skill = skill,
                Level = level
            };
        }

        private void ProcessDeferredSkillPayloads(int currentTime)
        {
            while (_deferredSkillPayloads.Count > 0)
            {
                DeferredSkillPayload pending = _deferredSkillPayloads.Peek();
                if (pending.ExecuteTime > currentTime)
                {
                    return;
                }

                _deferredSkillPayloads.Dequeue();
                if (pending.Skill == null)
                {
                    continue;
                }

                if (IsRocketBoosterSkill(pending.Skill))
                {
                    ExecuteRocketBoosterLaunch(pending.Skill, pending.Level);
                    continue;
                }

                ExecuteSkillPayload(
                    pending.Skill,
                    pending.Level,
                    currentTime,
                    pending.QueueFollowUps,
                    pending.PreferredTargetMobId,
                    allowDeferredExecution: false,
                    facingRightOverride: pending.FacingRight);
            }
        }

        private void UpdateRocketBooster(int currentTime)
        {
            if (_rocketBoosterState == null)
            {
                return;
            }

            bool onFoothold = _player.Physics?.IsOnFoothold() == true;
            if (!onFoothold)
            {
                _rocketBoosterState.HasLeftGround = true;
                return;
            }

            if (!_rocketBoosterState.HasLeftGround)
            {
                return;
            }

            if (_rocketBoosterState.LandingAttackTime == int.MinValue)
            {
                SkillLevelData levelData = _rocketBoosterState.Skill.GetLevel(_rocketBoosterState.Level);
                bool canLandAttack = levelData == null || levelData.MpCon <= 0 || TryConsumeSkillResources(levelData);

                SpawnHitEffect(_rocketBoosterState.Skill, _player.X, _player.Y, currentTime);
                if (canLandAttack)
                {
                    ProcessMeleeAttack(
                        _rocketBoosterState.Skill,
                        _rocketBoosterState.Level,
                        currentTime,
                        _player.FacingRight,
                        queueFollowUps: false,
                        preferredTargetMobId: null);
                }

                _rocketBoosterState.LandingAttackTime = currentTime;
                return;
            }

            if (currentTime - _rocketBoosterState.LandingAttackTime >= ROCKET_BOOSTER_LANDING_RECOVERY_MS)
            {
                _player.ClearSkillAvatarTransform(_rocketBoosterState.Skill.SkillId);
                ClearSkillMount(_rocketBoosterState.Skill.SkillId);
                _rocketBoosterState = null;
            }
        }

        private void StartCyclone(SkillData skill, int level, int currentTime)
        {
            SkillLevelData levelData = skill?.GetLevel(level);
            if (levelData == null)
            {
                return;
            }

            int durationMs = Math.Max(CYCLONE_ATTACK_INTERVAL_MS, levelData.Time > 0 ? levelData.Time * 1000 : 0);
            _cycloneState = new CycloneState
            {
                Skill = skill,
                Level = level,
                ExpireTime = currentTime + durationMs,
                NextAttackTime = currentTime + CYCLONE_ATTACK_INTERVAL_MS
            };

            _player.BeginSustainedSkillAnimation(ResolveSkillActionName(skill));
        }

        private void UpdateCyclone(int currentTime)
        {
            if (_cycloneState == null)
            {
                return;
            }

            if (!_player.IsAlive
                || (_fieldSkillRestrictionEvaluator != null && !_fieldSkillRestrictionEvaluator(_cycloneState.Skill)))
            {
                StopCyclone();
                return;
            }

            if (currentTime >= _cycloneState.ExpireTime)
            {
                StopCyclone();
                return;
            }

            while (_cycloneState != null && currentTime >= _cycloneState.NextAttackTime)
            {
                _player.BeginSustainedSkillAnimation(ResolveSkillActionName(_cycloneState.Skill));
                ProcessMeleeAttack(_cycloneState.Skill, _cycloneState.Level, currentTime, _player.FacingRight, queueFollowUps: false);
                _cycloneState.NextAttackTime += CYCLONE_ATTACK_INTERVAL_MS;
            }
        }

        private void StopCyclone()
        {
            if (_cycloneState == null)
            {
                return;
            }

            _cycloneState = null;
            _player.EndSustainedSkillAnimation();
        }

        private int GetRocketBoosterLaunchDelayMs(SkillData skill)
        {
            return Math.Max(120, GetActionAnimationDurationMs(skill));
        }

        private int GetMovingShootDelayMs(SkillData skill)
        {
            int delay = skill?.Effect?.Frames?.Count > 0
                ? skill.Effect.Frames[0].Delay
                : GetActionAnimationLeadDelayMs(skill);

            return Math.Clamp(delay <= 0 ? 90 : delay, 60, 180);
        }

        private int GetActionAnimationLeadDelayMs(SkillData skill)
        {
            if (_player.Assembler == null)
            {
                return 0;
            }

            string actionName = ResolveSkillActionName(skill);
            var animation = _player.Assembler.GetAnimation(actionName);
            if (animation == null || animation.Length == 0)
            {
                return 0;
            }

            return animation[0].Duration;
        }

        private int GetActionAnimationDurationMs(SkillData skill)
        {
            if (_player.Assembler == null)
            {
                return 0;
            }

            string actionName = ResolveSkillActionName(skill);
            var animation = _player.Assembler.GetAnimation(actionName);
            if (animation == null || animation.Length == 0)
            {
                return 0;
            }

            int duration = 0;
            foreach (var frame in animation)
            {
                duration += frame.Duration;
            }

            return duration;
        }

        private static bool IsRocketBoosterSkill(SkillData skill)
        {
            return skill?.SkillId == ROCKET_BOOSTER_SKILL_ID;
        }

        private static bool ShouldUseSmoothingMovingShoot(SkillData skill)
        {
            return skill?.CasterMove == true
                   && skill.IsAttack
                   && !skill.IsMovement
                   && !IsRocketBoosterSkill(skill)
                   && (skill.Projectile != null || skill.AttackType == SkillAttackType.Ranged);
        }

        private static bool SkillTextContains(SkillData skill, string value)
        {
            return skill?.ActionName?.Contains(value, StringComparison.OrdinalIgnoreCase) == true
                   || skill?.Name?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;
        }

        private static bool IsSwallowSkill(SkillData skill)
        {
            return SkillTextContains(skill, "swallow");
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
            HandleMobDeath(mob, currentTime, MobDeathType.Killed);
        }

        private void HandleMobDeath(MobItem mob, int currentTime, MobDeathType deathType)
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
            _mobPool?.KillMob(mob, deathType);
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

        private void ProcessMeleeAttack(
            SkillData skill,
            int level,
            int currentTime,
            bool facingRight,
            bool queueFollowUps = true,
            int? preferredTargetMobId = null)
        {
            ProcessDirectionalAttack(skill, level, currentTime, AttackResolutionMode.Melee, facingRight, queueFollowUps, preferredTargetMobId);
        }

        private void ProcessMagicAttack(
            SkillData skill,
            int level,
            int currentTime,
            bool facingRight,
            bool queueFollowUps = true,
            int? preferredTargetMobId = null)
        {
            ProcessDirectionalAttack(skill, level, currentTime, AttackResolutionMode.Magic, facingRight, queueFollowUps, preferredTargetMobId);
        }

        private void ProcessDirectionalAttack(
            SkillData skill,
            int level,
            int currentTime,
            AttackResolutionMode mode,
            bool facingRight,
            bool queueFollowUps,
            int? preferredTargetMobId)
        {
            if (_mobPool == null)
                return;

            var levelData = skill.GetLevel(level);
            if (levelData == null)
                return;

            Rectangle worldHitbox = GetWorldAttackHitbox(skill, level, levelData, mode, facingRight);
            int maxTargets = Math.Max(1, levelData.MobCount);
            int attackCount = Math.Max(1, levelData.AttackCount);
            List<MobItem> targets = ResolveTargetsInHitbox(worldHitbox, currentTime, maxTargets, mode, preferredTargetMobId);
            if (targets.Count == 0)
                return;

            if (queueFollowUps)
            {
                TryQueueFollowUpAttack(skill, currentTime, targets[0].PoolId, facingRight);
            }

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

        private Rectangle GetWorldAttackHitbox(SkillData skill, int level, SkillLevelData levelData, AttackResolutionMode mode, bool facingRight)
        {
            Rectangle hitbox = skill.GetAttackRange(level, facingRight);
            if (hitbox.Width <= 0 || hitbox.Height <= 0)
            {
                hitbox = mode == AttackResolutionMode.Magic
                    ? GetDefaultMagicHitbox(skill, levelData, facingRight)
                    : GetDefaultMeleeHitbox(skill, levelData, facingRight);
            }

            return new Rectangle(
                (int)_player.X + hitbox.X,
                (int)_player.Y + hitbox.Y,
                Math.Max(1, hitbox.Width),
                Math.Max(1, hitbox.Height));
        }

        private List<MobItem> ResolveTargetsInHitbox(
            Rectangle worldHitbox,
            int currentTime,
            int maxTargets,
            AttackResolutionMode mode,
            int? preferredTargetMobId)
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
                        Preferred = preferredTargetMobId.HasValue && entry.Mob.PoolId == preferredTargetMobId.Value ? 0 : 1,
                        Primary = mode == AttackResolutionMode.Magic ? areaDistance : forwardPenalty,
                        Secondary = mode == AttackResolutionMode.Magic ? forwardPenalty : areaDistance,
                        Tertiary = verticalDistance
                    };
                })
                .OrderBy(entry => entry.Preferred)
                .ThenBy(entry => entry.Primary)
                .ThenBy(entry => entry.Secondary)
                .ThenBy(entry => entry.Tertiary)
                .Take(maxTargets)
                .Select(entry => entry.Mob)
                .ToList();
        }

        private static bool IsMobAttackable(MobItem mob)
        {
            return mob?.AI != null
                && !mob.IsProtectedFromPlayerDamage
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

            ApplyMobStatusFromSkill(skill, levelData, mob, currentTime);
            return mob.AI?.State == MobAIState.Death;
        }

        private void ApplyMobStatusFromSkill(SkillData skill, SkillLevelData levelData, MobItem mob, int currentTime)
        {
            if (skill == null || levelData == null || mob?.AI == null || !IsMobAttackable(mob))
                return;

            string searchText = BuildSkillSearchText(skill);
            int durationMs = Math.Max(1, ResolveStatusDurationMs(levelData));
            int propPercent = levelData.Prop > 0 ? Math.Min(100, levelData.Prop) : 100;

            if (MatchesMobStatusKeywords(searchText, "poison", "venom") || skill.Element == SkillElement.Poison)
            {
                MobStatusEffect poisonEffect = searchText.Contains("venom", StringComparison.OrdinalIgnoreCase)
                    ? MobStatusEffect.Venom
                    : MobStatusEffect.Poison;
                int dotValue = ResolveStatusMagnitude(levelData, fallback: Math.Max(1, CalculateSkillDamage(skill, levelData.Level) / 4));
                TryApplyInferredMobStatus(mob.AI, poisonEffect, durationMs, currentTime, dotValue, tickIntervalMs: 1000, propPercent);
            }

            if (MatchesMobStatusKeywords(searchText, "burn", "flame"))
            {
                int burnValue = ResolveStatusMagnitude(levelData, fallback: Math.Max(1, CalculateSkillDamage(skill, levelData.Level) / 5));
                TryApplyInferredMobStatus(mob.AI, MobStatusEffect.Burned, durationMs, currentTime, burnValue, tickIntervalMs: 1000, propPercent);
            }

            if (MatchesMobStatusKeywords(searchText, "freeze", "ice", "blizzard", "frost") || skill.Element == SkillElement.Ice)
            {
                TryApplyInferredMobStatus(mob.AI, MobStatusEffect.Freeze, durationMs, currentTime, 0, tickIntervalMs: 1000, propPercent);
            }

            if (MatchesMobStatusKeywords(searchText, "stun", "paraly", "shock"))
            {
                TryApplyInferredMobStatus(mob.AI, MobStatusEffect.Stun, durationMs, currentTime, 0, tickIntervalMs: 1000, propPercent);
            }

            if (MatchesMobStatusKeywords(searchText, "seal"))
            {
                TryApplyInferredMobStatus(mob.AI, MobStatusEffect.Seal, durationMs, currentTime, 0, tickIntervalMs: 1000, propPercent);
            }

            if (MatchesMobStatusKeywords(searchText, "dark", "blind"))
            {
                int magnitude = ResolveStatusMagnitude(levelData, fallback: 20);
                TryApplyInferredMobStatus(mob.AI, MobStatusEffect.Darkness, durationMs, currentTime, magnitude, tickIntervalMs: 1000, propPercent);
            }

            if (MatchesMobStatusKeywords(searchText, "slow", "web"))
            {
                int slowPercent = ResolveSlowStatusMagnitude(levelData);
                TryApplyInferredMobStatus(mob.AI, MobStatusEffect.Web, durationMs, currentTime, slowPercent, tickIntervalMs: 1000, propPercent);
            }

            if (MatchesMobStatusKeywords(searchText, "weak"))
            {
                int weaknessPercent = ResolveStatusMagnitude(levelData, fallback: 20);
                TryApplyInferredMobStatus(mob.AI, MobStatusEffect.Weakness, durationMs, currentTime, weaknessPercent, tickIntervalMs: 1000, propPercent);
            }
        }

        private void TryApplyInferredMobStatus(
            MobAI mobAI,
            MobStatusEffect effect,
            int durationMs,
            int currentTime,
            int value,
            int tickIntervalMs,
            int propPercent)
        {
            if (mobAI == null || propPercent <= 0)
                return;

            if (propPercent < 100 && Random.Next(100) >= propPercent)
                return;

            mobAI.ApplyStatusEffect(effect, durationMs, currentTime, Math.Max(0, value), tickIntervalMs);
        }

        private static string BuildSkillSearchText(SkillData skill)
        {
            return string.Join(" ",
                skill?.Name ?? string.Empty,
                skill?.Description ?? string.Empty,
                skill?.ActionName ?? string.Empty);
        }

        private static bool MatchesMobStatusKeywords(string searchText, params string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return false;

            foreach (string keyword in keywords)
            {
                if (!string.IsNullOrWhiteSpace(keyword)
                    && searchText.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static int ResolveStatusDurationMs(SkillLevelData levelData)
        {
            if (levelData == null)
                return 1000;

            if (levelData.Time > 0)
                return levelData.Time * 1000;

            return 4000;
        }

        private static int ResolveStatusMagnitude(SkillLevelData levelData, int fallback)
        {
            if (levelData == null)
                return Math.Max(1, fallback);

            int[] candidates =
            {
                Math.Abs(levelData.X),
                Math.Abs(levelData.Y),
                Math.Abs(levelData.Z),
                Math.Abs(levelData.PAD),
                Math.Abs(levelData.MAD),
                Math.Abs(levelData.PDD),
                Math.Abs(levelData.MDD),
                Math.Abs(levelData.ACC),
                Math.Abs(levelData.EVA)
            };

            foreach (int candidate in candidates)
            {
                if (candidate > 0)
                    return candidate;
            }

            return Math.Max(1, fallback);
        }

        private static int ResolveSlowStatusMagnitude(SkillLevelData levelData)
        {
            if (levelData == null)
                return 40;

            if (levelData.Speed != 0)
                return Math.Abs(levelData.Speed);

            return ResolveStatusMagnitude(levelData, fallback: 40);
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
        private void SpawnHitEffect(SkillData skill, float x, float y, int currentTime, bool? facingRightOverride = null)
        {
            if (skill == null)
                return;

            SpawnHitEffect(skill.SkillId, skill.HitEffect, x, y, facingRightOverride ?? _player.FacingRight, currentTime);
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

        private void SpawnProjectile(
            SkillData skill,
            int level,
            int currentTime,
            bool facingRight,
            bool queueFollowUps = true,
            int? preferredTargetMobId = null)
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
                    FacingRight = facingRight,
                    SpawnTime = currentTime,
                    OwnerId = 0,
                    OwnerX = _player.X,
                    OwnerY = _player.Y,
                    PreferredTargetMobId = preferredTargetMobId,
                    AllowFollowUpQueue = queueFollowUps
                };

                // Set velocity
                proj.VelocityX = facingRight ? speed : -speed;
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

            if (!HasHomingBehavior(proj))
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
            if (proj?.PreferredTargetMobId is int preferredMobId)
            {
                MobItem preferredTarget = FindAttackableMobByPoolId(preferredMobId, currentTime);
                if (preferredTarget != null && proj.CanHitMob(preferredTarget.PoolId, maxHits))
                {
                    return preferredTarget;
                }
            }

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

                if (proj.AllowFollowUpQueue)
                {
                    TryQueueFollowUpAttack(skill, currentTime, mob.PoolId, proj.FacingRight);
                    proj.AllowFollowUpQueue = false;
                }

                if (skill.RectBasedOnTarget)
                {
                    ApplyProjectileTargetRectSplash(
                        proj,
                        skill,
                        mob,
                        currentTime,
                        attackCount,
                        maxTargets,
                        mobsToKill);
                }

                if (ShouldDetonateProjectileOnImpact(proj))
                {
                    ApplyProjectileExplosionSplash(
                        proj,
                        skill,
                        currentTime,
                        attackCount,
                        maxTargets,
                        mobsToKill);
                    break;
                }

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

        private void ApplyProjectileTargetRectSplash(
            ActiveProjectile proj,
            SkillData skill,
            MobItem anchorMob,
            int currentTime,
            int attackCount,
            int maxTargets,
            List<MobItem> mobsToKill)
        {
            if (_mobPool == null || proj?.LevelData == null || skill == null || anchorMob == null)
                return;

            int remainingHits = maxTargets - proj.HitCount;
            if (remainingHits <= 0)
                return;

            foreach (MobItem mob in ResolveProjectileTargetRectTargets(proj, skill, anchorMob, currentTime, remainingHits, maxTargets))
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

                if (proj.IsExpired || proj.HitCount >= maxTargets)
                    break;
            }
        }

        private void ApplyProjectileExplosionSplash(
            ActiveProjectile proj,
            SkillData skill,
            int currentTime,
            int attackCount,
            int maxTargets,
            List<MobItem> mobsToKill)
        {
            if (proj?.Data == null)
                return;

            if (!proj.IsExploding && !proj.IsExpired)
            {
                proj.Explode(currentTime);
            }

            int remainingHits = maxTargets - proj.HitCount;
            if (remainingHits <= 0 || _mobPool == null)
                return;

            foreach (MobItem mob in ResolveProjectileExplosionTargets(proj, currentTime, remainingHits, maxTargets))
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

                if (proj.IsExpired || proj.HitCount >= maxTargets)
                    break;
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
                        Preferred = proj.PreferredTargetMobId.HasValue && entry.Mob.PoolId == proj.PreferredTargetMobId.Value ? 0 : 1,
                        ProgressPenalty = progressPenalty,
                        LateralOffset = lateralOffset,
                        DistanceSq = distanceSq
                    };
                })
                .OrderBy(entry => entry.Preferred)
                .ThenBy(entry => entry.ProgressPenalty)
                .ThenBy(entry => entry.LateralOffset)
                .ThenBy(entry => entry.DistanceSq)
                .Take(maxTargets)
                .Select(entry => entry.Mob)
                .ToList();
        }

        private List<MobItem> ResolveProjectileExplosionTargets(ActiveProjectile proj, int currentTime, int maxTargets, int hitLimit)
        {
            if (_mobPool == null || proj?.Data == null || maxTargets <= 0)
                return new List<MobItem>();

            float radius = proj.Data.ExplosionRadius;
            if (radius <= 0f)
                return new List<MobItem>();

            float radiusSq = radius * radius;
            return _mobPool.ActiveMobs
                .Where(IsMobAttackable)
                .Where(mob => proj.CanHitMob(mob.PoolId, hitLimit))
                .Select(mob => new { Mob = mob, Hitbox = GetMobHitbox(mob, currentTime) })
                .Where(entry => !entry.Hitbox.IsEmpty)
                .Select(entry =>
                {
                    float centerX = entry.Hitbox.Left + entry.Hitbox.Width * 0.5f;
                    float centerY = entry.Hitbox.Top + entry.Hitbox.Height * 0.5f;
                    float deltaX = centerX - proj.X;
                    float deltaY = centerY - proj.Y;
                    float distanceSq = (deltaX * deltaX) + (deltaY * deltaY);
                    return new { entry.Mob, DistanceSq = distanceSq };
                })
                .Where(entry => entry.DistanceSq <= radiusSq)
                .OrderBy(entry => entry.DistanceSq)
                .Take(maxTargets)
                .Select(entry => entry.Mob)
                .ToList();
        }

        private List<MobItem> ResolveProjectileTargetRectTargets(
            ActiveProjectile proj,
            SkillData skill,
            MobItem anchorMob,
            int currentTime,
            int maxTargets,
            int hitLimit)
        {
            if (_mobPool == null || proj?.LevelData == null || skill == null || anchorMob == null || maxTargets <= 0)
                return new List<MobItem>();

            Rectangle anchorHitbox = GetMobHitbox(anchorMob, currentTime);
            if (anchorHitbox.IsEmpty)
                return new List<MobItem>();

            Rectangle targetRect = skill.GetAttackRange(proj.SkillLevel, proj.FacingRight);
            if (targetRect.Width <= 0 || targetRect.Height <= 0)
            {
                targetRect = GetDefaultMagicHitbox(skill, proj.LevelData, proj.FacingRight);
            }

            float anchorX = anchorHitbox.Left + anchorHitbox.Width * 0.5f;
            float anchorY = anchorHitbox.Top + anchorHitbox.Height * 0.5f;
            Rectangle worldRect = new Rectangle(
                (int)MathF.Round(anchorX + targetRect.X),
                (int)MathF.Round(anchorY + targetRect.Y),
                Math.Max(1, targetRect.Width),
                Math.Max(1, targetRect.Height));

            return _mobPool.ActiveMobs
                .Where(IsMobAttackable)
                .Where(mob => mob.PoolId != anchorMob.PoolId && proj.CanHitMob(mob.PoolId, hitLimit))
                .Select(mob => new { Mob = mob, Hitbox = GetMobHitbox(mob, currentTime) })
                .Where(entry => !entry.Hitbox.IsEmpty && worldRect.Intersects(entry.Hitbox))
                .Select(entry =>
                {
                    float centerX = entry.Hitbox.Left + entry.Hitbox.Width * 0.5f;
                    float centerY = entry.Hitbox.Top + entry.Hitbox.Height * 0.5f;
                    float deltaX = centerX - anchorX;
                    float deltaY = centerY - anchorY;
                    float forward = proj.FacingRight ? deltaX : -deltaX;
                    float forwardPenalty = forward < 0f ? 100000f + MathF.Abs(forward) : forward;
                    float distanceSq = (deltaX * deltaX) + (deltaY * deltaY);

                    return new
                    {
                        entry.Mob,
                        ForwardPenalty = forwardPenalty,
                        DistanceSq = distanceSq,
                        VerticalDistance = MathF.Abs(deltaY)
                    };
                })
                .OrderBy(entry => entry.ForwardPenalty)
                .ThenBy(entry => entry.DistanceSq)
                .ThenBy(entry => entry.VerticalDistance)
                .Take(maxTargets)
                .Select(entry => entry.Mob)
                .ToList();
        }

        private static int GetEffectiveProjectileHitLimit(ActiveProjectile proj)
        {
            return Math.Max(1, Math.Max(proj?.LevelData?.MobCount ?? 0, proj?.Data?.MaxHits ?? 0));
        }

        private static bool HasHomingBehavior(ActiveProjectile proj)
        {
            return proj?.Data != null
                   && (proj.Data.Homing || proj.Data.Behavior == ProjectileBehavior.Homing);
        }

        private static bool ShouldDetonateProjectileOnImpact(ActiveProjectile proj)
        {
            return proj?.Data != null
                   && (proj.Data.Behavior == ProjectileBehavior.Exploding
                       || proj.Data.ExplosionRadius > 0f
                       || proj.Data.ExplosionAnimation != null);
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
            Vector2 spawnPosition = SummonMovementResolver.ResolveSpawnPosition(
                skill.SummonMovementStyle,
                skill.SummonSpawnDistanceX,
                _player.Position,
                _player.FacingRight);
            spawnPosition = SettleSummonOnFoothold(skill.SummonMovementStyle, spawnPosition);

            var summon = new ActiveSummon
            {
                ObjectId = _nextSummonId++,
                SkillId = skill.SkillId,
                Level = level,
                StartTime = currentTime,
                Duration = durationMs,
                LastAttackTime = currentTime,
                MoveAbility = skill.SummonMoveAbility,
                MovementStyle = skill.SummonMovementStyle,
                SpawnDistanceX = skill.SummonSpawnDistanceX,
                AnchorX = spawnPosition.X,
                AnchorY = spawnPosition.Y,
                PositionX = spawnPosition.X,
                PositionY = spawnPosition.Y,
                SkillData = skill,
                LevelData = levelData,
                FacingRight = _player.FacingRight
            };

            UpdateSummonPosition(summon, currentTime, 0f);
            _summons.Add(summon);
            SyncSummonPuppet(summon, currentTime);
        }

        private void UpdateSummons(int currentTime, float deltaTime)
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

                UpdateSummonPosition(summon, currentTime, deltaTime);
                SyncSummonPuppet(summon, currentTime);

                if (currentTime - summon.LastAttackTime < 1000)
                    continue;

                if (IsSummonAttackBlockedByOwnerState(summon))
                    continue;

                summon.LastAttackTime = currentTime;
                if (IsHealingSupportSummon(summon.SkillData))
                {
                    ProcessSummonSupport(summon, currentTime);
                }
                else
                {
                ProcessSummonAttack(summon, currentTime);
            }
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

            if (hitCount > 0)
            {
                summon.LastAttackAnimationStartTime = currentTime;
            }

            foreach (var mob in mobsToKill)
            {
                HandleMobDeath(mob, currentTime);
            }
        }

        private bool IsSummonAttackBlockedByOwnerState(ActiveSummon summon)
        {
            if (summon?.SkillData == null)
                return false;

            if (summon.SkillId == SG88_SKILL_ID && !summon.ManualAssistEnabled)
                return true;

            return (_player.State == PlayerState.Ladder || _player.State == PlayerState.Rope)
                && !SummonMovementResolver.CanAttackWhileOwnerIsOnLadderOrRope(summon.SkillData.SkillId);
        }

        private bool IsHealingSupportSummon(SkillData skill)
        {
            return skill != null
                   && skill.SkillId == HEALING_ROBOT_SKILL_ID
                   && string.Equals(skill.MinionAbility, "heal", StringComparison.OrdinalIgnoreCase);
        }

        private void ProcessSummonSupport(ActiveSummon summon, int currentTime)
        {
            if (summon?.LevelData == null)
            {
                return;
            }

            if (string.Equals(summon.SkillData?.SummonCondition, "whenUserLieDown", StringComparison.OrdinalIgnoreCase)
                && _player.State != PlayerState.Sitting)
            {
                return;
            }

            int hpHeal = summon.LevelData.HP;
            int mpHeal = summon.LevelData.MP;

            if (summon.LevelData.X > 0 && hpHeal <= 0)
            {
                hpHeal = summon.LevelData.X;
            }

            if (hpHeal > 0)
            {
                _player.HP = Math.Min(_player.MaxHP, _player.HP + hpHeal);
            }

            if (mpHeal > 0)
            {
                _player.MP = Math.Min(_player.MaxMP, _player.MP + mpHeal);
            }

            if (summon.SkillData.HitEffect != null)
            {
                SpawnHitEffect(
                    summon.SkillData.SkillId,
                    summon.SkillData.HitEffect,
                    _player.X,
                    _player.Y - 35f,
                    _player.FacingRight,
                    currentTime);
            }
        }

        private Vector2 GetSummonPosition(ActiveSummon summon)
        {
            return new Vector2(summon.PositionX, summon.PositionY);
        }

        private void UpdateMine(int currentTime)
        {
            SkillData mineSkill = GetSkillData(MINE_SKILL_ID);
            int mineLevel = GetSkillLevel(MINE_SKILL_ID);
            if (mineSkill == null
                || mineLevel <= 0
                || !_player.IsAlive
                || _player.Build == null
                || !_player.Build.Equipment.ContainsKey(EquipSlot.TamingMob)
                || (_fieldSkillRestrictionEvaluator != null && !_fieldSkillRestrictionEvaluator(mineSkill)))
            {
                ResetMineMovementState();
                return;
            }

            int moveDirection = GetMineMovementDirection();
            if (moveDirection != _mineMovementDirection)
            {
                _mineMovementDirection = moveDirection;
                _mineMovementStartTime = currentTime;
            }

            if (moveDirection == 0 || currentTime - _mineMovementStartTime < MINE_DEPLOY_INTERVAL_MS)
            {
                return;
            }

            SkillLevelData levelData = mineSkill.GetLevel(mineLevel);
            if (levelData == null || !TryConsumeSkillResources(levelData))
            {
                return;
            }

            SpawnSummon(mineSkill, mineLevel, currentTime);
            _mineMovementStartTime = currentTime;
        }

        private int GetMineMovementDirection()
        {
            if (!_player.Physics.IsOnFoothold())
            {
                return 0;
            }

            float velocityX = (float)_player.Physics.VelocityX;
            if (Math.Abs(velocityX) <= 5f)
            {
                return 0;
            }

            return velocityX > 0f ? 1 : -1;
        }

        private void ResetMineMovementState()
        {
            _mineMovementDirection = 0;
            _mineMovementStartTime = 0;
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

            float aggroRange = Math.Max(220f, Math.Abs(summon.PositionX - _player.X) + 170f);
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

        private void UpdateSummonPosition(ActiveSummon summon, int currentTime, float deltaTime)
        {
            if (summon == null)
                return;

            if (summon.NeedsAnchorReset && SummonMovementResolver.IsAnchorBound(summon.MovementStyle))
            {
                Vector2 respawnPosition = SummonMovementResolver.ResolveSpawnPosition(
                    summon.MovementStyle,
                    summon.SpawnDistanceX,
                    _player.Position,
                    _player.FacingRight);
                respawnPosition = SettleSummonOnFoothold(summon.MovementStyle, respawnPosition);

                summon.AnchorX = respawnPosition.X;
                summon.AnchorY = respawnPosition.Y;
                summon.PositionX = respawnPosition.X;
                summon.PositionY = respawnPosition.Y;
                summon.NeedsAnchorReset = false;
            }

            float elapsedSeconds = Math.Max(0f, (currentTime - summon.StartTime) / 1000f);
            Vector2 playerPosition = _player.Position;
            Vector2 targetPosition = summon.MovementStyle switch
            {
                SummonMovementStyle.GroundFollow => new Vector2(
                    playerPosition.X + (_player.FacingRight ? 70f : -70f),
                    playerPosition.Y - 25f),
                SummonMovementStyle.HoverFollow => new Vector2(
                    playerPosition.X + (_player.FacingRight ? 60f : -60f) + MathF.Sin(elapsedSeconds * 2.1f + summon.ObjectId) * 14f,
                    playerPosition.Y - 65f + MathF.Cos(elapsedSeconds * 3.3f + summon.ObjectId * 0.5f) * 8f),
                SummonMovementStyle.DriftAroundOwner => new Vector2(
                    playerPosition.X + MathF.Cos(elapsedSeconds * 1.6f + summon.ObjectId) * 65f,
                    playerPosition.Y - 52f + MathF.Sin(elapsedSeconds * 2.8f + summon.ObjectId * 0.75f) * 18f),
                SummonMovementStyle.HoverAroundAnchor => new Vector2(
                    summon.AnchorX + MathF.Sin(elapsedSeconds * 1.3f + summon.ObjectId) * 80f,
                    summon.AnchorY - 35f + MathF.Cos(elapsedSeconds * 2.0f + summon.ObjectId * 0.35f) * 16f),
                _ => new Vector2(summon.AnchorX, summon.AnchorY)
            };
            targetPosition = SettleSummonOnFoothold(summon.MovementStyle, targetPosition);

            if (deltaTime <= 0f)
            {
                summon.PositionX = targetPosition.X;
                summon.PositionY = targetPosition.Y;
            }
            else if (summon.MovementStyle == SummonMovementStyle.GroundFollow
                     || summon.MovementStyle == SummonMovementStyle.HoverFollow)
            {
                float followSpeed = summon.MovementStyle == SummonMovementStyle.GroundFollow ? 220f : 260f;
                summon.PositionX = MoveTowards(summon.PositionX, targetPosition.X, followSpeed * deltaTime);
                summon.PositionY = MoveTowards(summon.PositionY, targetPosition.Y, (followSpeed + 40f) * deltaTime);
            }
            else
            {
                summon.PositionX = targetPosition.X;
                summon.PositionY = targetPosition.Y;
            }

            if (summon.MovementStyle == SummonMovementStyle.GroundFollow
                || summon.MovementStyle == SummonMovementStyle.HoverFollow
                || summon.MovementStyle == SummonMovementStyle.DriftAroundOwner)
            {
                summon.FacingRight = _player.FacingRight;
            }
        }

        private Vector2 SettleSummonOnFoothold(SummonMovementStyle movementStyle, Vector2 targetPosition)
        {
            if (_footholdLookup == null)
            {
                return targetPosition;
            }

            bool needsGrounding = movementStyle == SummonMovementStyle.Stationary
                || movementStyle == SummonMovementStyle.GroundFollow
                || movementStyle == SummonMovementStyle.HoverAroundAnchor;
            if (!needsGrounding)
            {
                return targetPosition;
            }

            float searchRange = movementStyle == SummonMovementStyle.GroundFollow ? 80f : 140f;
            FootholdLine foothold = _footholdLookup(targetPosition.X, targetPosition.Y + GroundedSummonVisualYOffset, searchRange);
            if (foothold == null)
            {
                return targetPosition;
            }

            float minX = Math.Min(foothold.FirstDot.X, foothold.SecondDot.X);
            float maxX = Math.Max(foothold.FirstDot.X, foothold.SecondDot.X);
            float groundedX = Math.Clamp(targetPosition.X, minX, maxX);
            float groundedY = Board.CalculateYOnFoothold(foothold, groundedX) - GroundedSummonVisualYOffset;
            return new Vector2(groundedX, groundedY);
        }

        #region Buff System

        private void ApplyBuff(SkillData skill, int level, int currentTime)
        {
            var levelData = skill.GetLevel(level);
            if (levelData == null || levelData.Time <= 0)
                return;

            CancelActiveBuff(skill.SkillId, currentTime, playFinish: false);

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
            _player.ApplySkillAvatarEffect(skill.SkillId, ResolveBuffAvatarEffectSkill(skill), currentTime);
            OnBuffApplied?.Invoke(buff);
            RegisterClientSkillTimer(skill.SkillId, "buff-expire", currentTime + buff.Duration, ExpireBuffFromClientTimer);

            // Apply buff effects to player stats
            ApplyBuffStats(buff, true);
            RefreshBuffControlledFlyingAbility();
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

        public bool CancelActiveBuff(int skillId)
        {
            return CancelActiveBuff(skillId, Environment.TickCount, playFinish: true);
        }

        private bool CancelActiveBuff(int skillId, int currentTime, bool playFinish)
        {
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                var buff = _buffs[i];
                if (buff.SkillId != skillId)
                {
                    continue;
                }

                RemoveBuffAt(i, currentTime, playFinish);
                return true;
            }

            return false;
        }

        private void RemoveBuffAt(int index, int currentTime, bool playFinish)
        {
            var buff = _buffs[index];
            ApplyBuffStats(buff, false);
            _player.ClearSkillAvatarTransform(buff.SkillId);
            _player.ClearSkillAvatarEffect(buff.SkillId, currentTime, playFinish);
            ClearSkillMount(buff.SkillId);
            if (_swallowState?.SkillId == buff.SkillId)
            {
                ClearSwallowState();
            }

            _buffs.RemoveAt(index);
            CancelClientSkillTimers(buff.SkillId, "buff-expire");
            RefreshBuffControlledFlyingAbility();
            OnBuffExpired?.Invoke(buff);
        }

        private void ExpireBuffFromClientTimer(int currentTime)
        {
            bool removedBuff = false;
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                if (_buffs[i].IsExpired(currentTime))
                {
                    RemoveBuffAt(i, currentTime, playFinish: true);
                    removedBuff = true;
                }
            }

            if (removedBuff)
            {
                RefreshBuffControlledFlyingAbility();
            }
        }

        private void UpdateBuffs(int currentTime)
        {
            UpdateClientSkillTimers(currentTime);
        }

        private void RefreshBuffControlledFlyingAbility()
        {
            if (_player?.Physics == null)
                return;

            bool hasFlyingBuff = _buffs.Any(buff => SkillGrantsFlyingAbility(buff.SkillData));
            if (hasFlyingBuff)
            {
                _player.Physics.HasFlyingAbility = true;
            }
            else if (_buffControlledFlyingAbility)
            {
                _player.Physics.HasFlyingAbility = false;
            }

            _buffControlledFlyingAbility = hasFlyingBuff;
        }

        private static bool SkillGrantsFlyingAbility(SkillData skill)
        {
            return ActionGrantsFlyingAbility(skill?.ActionName)
                || ActionGrantsFlyingAbility(skill?.PrepareActionName)
                || ActionGrantsFlyingAbility(skill?.KeydownActionName);
        }

        private static SkillData ResolveBuffAvatarEffectSkill(SkillData skill)
        {
            if (!UsesEffectToAvatarLayerBuffFallback(skill))
            {
                return skill;
            }

            return new SkillData
            {
                SkillId = skill.SkillId,
                AvatarOverlayEffect = CreateLoopingAvatarEffect(skill.Effect)
            };
        }

        private static bool UsesEffectToAvatarLayerBuffFallback(SkillData skill)
        {
            return UsesFlightBuffAvatarEffectFallback(skill)
                   || UsesSwallowBuffAvatarEffectFallback(skill);
        }

        private static bool UsesSwallowBuffAvatarEffectFallback(SkillData skill)
        {
            return skill?.IsBuff == true
                   && skill.Invisible
                   && skill.Effect != null
                   && !skill.HasPersistentAvatarEffect
                   && IsSwallowSkill(skill);
        }

        private static SkillAnimation CreateLoopingAvatarEffect(SkillAnimation animation)
        {
            if (animation == null)
            {
                return null;
            }

            return new SkillAnimation
            {
                Name = animation.Name,
                Frames = new List<SkillFrame>(animation.Frames),
                Loop = true,
                Origin = animation.Origin,
                ZOrder = animation.ZOrder
            };
        }

        private static bool ActionGrantsFlyingAbility(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                && (actionName.IndexOf("fly", StringComparison.OrdinalIgnoreCase) >= 0
                    || actionName.IndexOf("flying", StringComparison.OrdinalIgnoreCase) >= 0);
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
                IReadOnlyList<BuffTemporaryStatPresentation> temporaryStats = GetBuffTemporaryStatPresentation(buff);
                entries.Add(new StatusBarBuffEntry
                {
                    SkillId = buff.SkillId,
                    SkillName = buff.SkillData?.Name ?? buff.SkillId.ToString(),
                    Description = buff.SkillData?.Description ?? string.Empty,
                    IconKey = ResolveBuffIconKey(temporaryStats),
                    IconTexture = buff.SkillData?.IconTexture,
                    StartTime = buff.StartTime,
                    DurationMs = buff.Duration,
                    RemainingMs = buff.GetRemainingTime(currentTime),
                    TemporaryStatLabels = temporaryStats.Select(stat => stat.Label).ToArray(),
                    TemporaryStatDisplayNames = temporaryStats.Select(stat => stat.DisplayName).ToArray()
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

        private static IReadOnlyList<BuffTemporaryStatPresentation> GetBuffTemporaryStatPresentation(ActiveBuff buff)
        {
            var levelData = buff?.LevelData;
            if (levelData == null)
            {
                return Array.Empty<BuffTemporaryStatPresentation>();
            }

            var temporaryStats = new List<BuffTemporaryStatPresentation>(8);

            void Track(int value, string label, string displayName, string key)
            {
                if (value <= 0)
                {
                    return;
                }

                temporaryStats.Add(new BuffTemporaryStatPresentation
                {
                    Label = label,
                    DisplayName = displayName,
                    IconKey = key
                });
            }

            Track(levelData.PAD, "PAD", "Physical Attack", "buff/incPAD");
            Track(levelData.PDD, "PDD", "Physical Defense", "buff/incPDD");
            Track(levelData.MAD, "MAD", "Magic Attack", "buff/incMAD");
            Track(levelData.MDD, "MDD", "Magic Defense", "buff/incMDD");
            Track(levelData.ACC, "ACC", "Accuracy", "buff/incACC");
            Track(levelData.EVA, "EVA", "Avoidability", "buff/incEVA");
            Track(levelData.Speed, "Speed", "Speed", "buff/incSpeed");
            Track(levelData.Jump, "Jump", "Jump", "buff/incJump");

            return temporaryStats;
        }

        private static string ResolveBuffIconKey(IReadOnlyList<BuffTemporaryStatPresentation> temporaryStats)
        {
            if (temporaryStats == null || temporaryStats.Count == 0)
            {
                return GenericBuffIconKey;
            }

            return string.IsNullOrWhiteSpace(temporaryStats[0].IconKey)
                ? GenericBuffIconKey
                : temporaryStats[0].IconKey;
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
            UpdateClientSkillTimers(currentTime);
            UpdateSwallowState(currentTime);
            UpdateSkillZones(currentTime);

            // Update current cast
            if (_currentCast != null)
            {
                // Check if cast animation is complete
                var effectAnimation = _currentCast.SuppressEffectAnimation
                    ? null
                    : _currentCast.EffectAnimation ?? _currentCast.SkillData?.Effect;
                if (effectAnimation != null)
                {
                    if (effectAnimation.IsComplete(_currentCast.AnimationTime))
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

            if (_preparedSkill?.IsKeydownSkill == true)
            {
                UpdateKeydownSkill(currentTime);
            }
            else if (_preparedSkill != null && _preparedSkill.Progress(currentTime) >= 1f)
            {
                ReleasePreparedSkill(currentTime);
            }

            ProcessDeferredSkillPayloads(currentTime);
            UpdateRocketBooster(currentTime);
            UpdateCyclone(currentTime);
            ProcessQueuedFollowUpAttacks(currentTime);

            // Process skill queue (for macros)
            ProcessSkillQueue(currentTime);

            // Update projectiles
            UpdateProjectiles(currentTime, deltaTime);

            // Update summons
            UpdateSummons(currentTime, deltaTime);
            UpdateMine(currentTime);

            // Update hit effects (remove expired)
            UpdateHitEffects(currentTime);
        }

        private void ClearSwallowState()
        {
            MobItem target = GetSwallowTarget();
            target?.AI?.RemoveStatusEffect(MobStatusEffect.Stun);
            _swallowState = null;
        }

        private void UpdateSwallowState(int currentTime)
        {
            if (_swallowState == null)
            {
                return;
            }

            MobItem target = GetSwallowTarget();
            if (target?.AI == null || target.AI.IsDead)
            {
                ClearSwallowState();
                return;
            }

            while (currentTime >= _swallowState.NextWriggleTime && currentTime < _swallowState.DigestCompleteTime)
            {
                SpawnHitEffect(
                    GetSkillData(_swallowState.SkillId),
                    target.MovementInfo?.X ?? _player.X,
                    (target.MovementInfo?.Y ?? _player.Y) - 20f,
                    _swallowState.NextWriggleTime);
                _swallowState.NextWriggleTime += SwallowWriggleIntervalMs;
            }

            if (currentTime < _swallowState.DigestCompleteTime)
            {
                return;
            }

            SkillData swallowSkill = GetSkillData(_swallowState.SkillId);
            int swallowLevel = _swallowState.Level;
            SkillData digestBuffSkill = GetSkillData(WildHunterSwallowBuffSkillId);
            int digestBuffLevel = GetSkillLevel(WildHunterSwallowBuffSkillId);
            if (digestBuffLevel <= 0)
            {
                digestBuffLevel = swallowLevel;
            }

            HandleMobDeath(target, currentTime, MobDeathType.Swallowed);
            _swallowState = null;

            if (digestBuffSkill?.GetLevel(digestBuffLevel) != null)
            {
                ApplyBuff(digestBuffSkill, digestBuffLevel, currentTime);
            }
            else if (ShouldApplySwallowBuff(swallowSkill, swallowSkill?.GetLevel(swallowLevel)))
            {
                ApplyBuff(swallowSkill, swallowLevel, currentTime);
            }
        }

        private MobItem GetSwallowTarget()
        {
            if (_swallowState == null || _mobPool == null)
            {
                return null;
            }

            return _mobPool.ActiveMobs.FirstOrDefault(mob => mob.PoolId == _swallowState.TargetMobId);
        }

        private void UpdateSkillZones(int currentTime)
        {
            for (int i = _skillZones.Count - 1; i >= 0; i--)
            {
                if (_skillZones[i].IsExpired(currentTime))
                {
                    _skillZones.RemoveAt(i);
                }
            }
        }

        private void StartSkillZone(
            SkillData skill,
            int level,
            SkillLevelData levelData,
            int currentTime,
            Rectangle worldBounds)
        {
            if (skill == null || levelData == null)
            {
                return;
            }

            _skillZones.RemoveAll(zone => zone.SkillId == skill.SkillId);

            int durationMs = levelData.Time > 0 ? levelData.Time * 1000 : 0;
            if (durationMs <= 0)
            {
                return;
            }

            _skillZones.Add(new ActiveSkillZone
            {
                SkillId = skill.SkillId,
                Level = level,
                StartTime = currentTime,
                Duration = durationMs,
                X = _player.X,
                Y = _player.Y,
                SkillData = skill,
                LevelData = levelData,
                Animation = skill.ZoneAnimation,
                WorldBounds = worldBounds
            });
        }

        public bool IsPlayerProtectedByClientSkillZone(int currentTime)
        {
            return IsPointInsideActiveZone(_player.X, _player.Y, currentTime, "invincible");
        }

        private bool IsPointInsideActiveZone(float worldX, float worldY, int currentTime, params string[] zoneTypes)
        {
            for (int i = 0; i < _skillZones.Count; i++)
            {
                ActiveSkillZone zone = _skillZones[i];
                if (zone.IsExpired(currentTime))
                {
                    continue;
                }

                if (zone.SkillData == null
                    || !zone.Contains(worldX, worldY)
                    || !MatchesZoneType(zone.SkillData.ZoneType, zoneTypes))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool MatchesZoneType(string zoneType, params string[] expectedZoneTypes)
        {
            if (string.IsNullOrWhiteSpace(zoneType) || expectedZoneTypes == null)
            {
                return false;
            }

            foreach (string expectedZoneType in expectedZoneTypes)
            {
                if (string.Equals(zoneType, expectedZoneType, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void RegisterClientSkillTimer(int skillId, string source, int expireTime, Action<int> onExpired, bool replaceExisting = true)
        {
            if (skillId <= 0 || string.IsNullOrWhiteSpace(source) || onExpired == null)
                return;

            if (replaceExisting)
            {
                CancelClientSkillTimers(skillId, source);
            }

            _clientSkillTimers.Add(new ClientSkillTimer
            {
                SkillId = skillId,
                Source = source,
                ExpireTime = expireTime,
                OnExpired = onExpired
            });
        }

        private void CancelClientSkillTimers(int skillId, string source = null)
        {
            for (int i = _clientSkillTimers.Count - 1; i >= 0; i--)
            {
                ClientSkillTimer timer = _clientSkillTimers[i];
                if (timer.SkillId != skillId)
                    continue;

                if (!string.IsNullOrWhiteSpace(source)
                    && !string.Equals(timer.Source, source, StringComparison.Ordinal))
                {
                    continue;
                }

                _clientSkillTimers.RemoveAt(i);
            }
        }

        private void UpdateClientSkillTimers(int currentTime)
        {
            if (_clientSkillTimers.Count == 0)
                return;

            List<ClientSkillTimer> expiredTimers = null;
            for (int i = _clientSkillTimers.Count - 1; i >= 0; i--)
            {
                ClientSkillTimer timer = _clientSkillTimers[i];
                if (timer.ExpireTime > currentTime)
                    continue;

                expiredTimers ??= new List<ClientSkillTimer>();
                expiredTimers.Add(timer);
                _clientSkillTimers.RemoveAt(i);
            }

            if (expiredTimers == null)
                return;

            foreach (ClientSkillTimer timer in expiredTimers
                         .OrderBy(timer => timer.ExpireTime)
                         .ThenBy(GetClientSkillTimerPriority)
                         .ThenBy(timer => timer.SkillId))
            {
                OnClientSkillTimerExpired?.Invoke(timer.SkillId, timer.Source);
                timer.OnExpired(currentTime);
            }
        }

        private static int GetClientSkillTimerPriority(ClientSkillTimer timer)
        {
            if (timer == null)
                return 0;

            return string.Equals(timer.Source, "repeat-sustain-end", StringComparison.Ordinal)
                ? 0
                : 1;
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

        private void ProcessQueuedFollowUpAttacks(int currentTime)
        {
            while (_queuedFollowUpAttacks.Count > 0)
            {
                if (_queuedFollowUpAttacks.Peek().ExecuteTime > currentTime)
                    return;

                if ((_currentCast != null && !_currentCast.IsComplete) || _preparedSkill != null)
                    return;

                ExecuteQueuedFollowUpAttack(_queuedFollowUpAttacks.Dequeue(), currentTime);
            }
        }

        private void ExecuteQueuedFollowUpAttack(QueuedFollowUpAttack queuedAttack, int currentTime)
        {
            if (queuedAttack == null)
                return;

            SkillData skill = GetSkillData(queuedAttack.SkillId);
            int level = queuedAttack.Level > 0 ? queuedAttack.Level : GetSkillLevel(queuedAttack.SkillId);
            SkillLevelData levelData = skill?.GetLevel(level);
            if (skill == null || levelData == null)
                return;

            if (queuedAttack.RequiredWeaponCode > 0 && GetEquippedWeaponCode() != queuedAttack.RequiredWeaponCode)
                return;

            _currentCast = new SkillCastInfo
            {
                SkillId = skill.SkillId,
                Level = level,
                SkillData = skill,
                LevelData = levelData,
                EffectAnimation = skill.Effect,
                CastTime = currentTime,
                CasterId = 0,
                CasterX = _player.X,
                CasterY = _player.Y,
                FacingRight = queuedAttack.FacingRight
            };

            TriggerSkillAnimation(skill);
            PlayCastSound(skill);
            OnSkillCast?.Invoke(_currentCast);
            ExecuteSkillPayload(
                skill,
                level,
                currentTime,
                queueFollowUps: false,
                preferredTargetMobId: queuedAttack.TargetMobId,
                facingRightOverride: queuedAttack.FacingRight);
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

            foreach (var zone in _skillZones)
            {
                DrawSkillZoneEffect(spriteBatch, zone, mapShiftX, mapShiftY, centerX, centerY, currentTime);
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

        private void DrawSkillZoneEffect(SpriteBatch spriteBatch, ActiveSkillZone zone,
            int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime)
        {
            SkillFrame frame = zone?.Animation?.GetFrameAtTime(zone.AnimationTime(currentTime));
            if (frame?.Texture == null)
            {
                return;
            }

            int tileWidth = Math.Max(1, frame.Bounds.Width);
            int tileHeight = Math.Max(1, frame.Bounds.Height);
            int columns = Math.Max(1, (int)Math.Ceiling(zone.WorldBounds.Width / (float)tileWidth));
            int rows = Math.Max(1, (int)Math.Ceiling(zone.WorldBounds.Height / (float)tileHeight));
            int startX = zone.WorldBounds.Left + tileWidth / 2;
            int startY = zone.WorldBounds.Top + tileHeight / 2;

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int worldX = startX + (column * tileWidth);
                    int worldY = startY + (row * tileHeight);
                    int screenX = worldX - mapShiftX + centerX;
                    int screenY = worldY - mapShiftY + centerY;
                    bool shouldFlip = frame.Flip;

                    frame.Texture.DrawBackground(spriteBatch, null, null,
                        GetFrameDrawX(screenX, frame, shouldFlip), screenY - frame.Origin.Y,
                        Color.White, shouldFlip, null);
                }
            }
        }

        private void DrawCastEffect(SpriteBatch spriteBatch, SkillCastInfo cast,
            int mapShiftX, int mapShiftY, int centerX, int centerY)
        {
            if (cast?.SuppressEffectAnimation == true)
                return;

            var effect = cast.EffectAnimation ?? cast.SkillData?.Effect;
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
            var animation = ResolveSummonAnimation(summon, currentTime, elapsed, out int animationTime)
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

        private static SkillAnimation ResolveSummonAnimation(ActiveSummon summon, int currentTime, int elapsedTime, out int animationTime)
        {
            animationTime = Math.Max(0, elapsedTime);
            var skill = summon?.SkillData;
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

            var attackAnimation = skill.SummonAttackAnimation;
            if (attackAnimation?.Frames.Count > 0 && summon != null)
            {
                int attackElapsed = currentTime - summon.LastAttackAnimationStartTime;
                int attackDuration = attackAnimation.TotalDuration > 0
                    ? attackAnimation.TotalDuration
                    : attackAnimation.Frames.Sum(frame => frame.Delay);
                if (attackElapsed >= 0 && attackDuration > 0 && attackElapsed < attackDuration)
                {
                    animationTime = attackElapsed;
                    return attackAnimation;
                }
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

        public SkillData GetSkillData(int skillId)
        {
            return FindKnownSkillData(skillId);
        }

        private static bool IsInvincibleZoneSkill(SkillData skill)
        {
            return skill != null
                   && (skill.SkillId == SMOKE_BOMB_SKILL_ID
                       || string.Equals(skill.ZoneType, "invincible", StringComparison.OrdinalIgnoreCase));
        }

        private bool IsSkillAllowedForCurrentJob(SkillData skill)
        {
            if (skill == null)
                return false;

            if (!IsAdminSkillJob(skill.Job))
                return true;

            int currentJob = _player.Build?.Job ?? 0;
            return currentJob switch
            {
                910 => skill.Job == 900 || skill.Job == 910,
                900 => skill.Job == 900,
                _ => false
            };
        }

        private void TryQueueFollowUpAttack(SkillData triggerSkill, int currentTime, int? targetMobId, bool facingRight)
        {
            if (triggerSkill?.FinalAttackTriggers == null || triggerSkill.FinalAttackTriggers.Count == 0)
                return;

            int equippedWeaponCode = GetEquippedWeaponCode();
            if (equippedWeaponCode <= 0)
                return;

            foreach ((int followUpSkillId, HashSet<int> allowedWeaponCodes) in triggerSkill.FinalAttackTriggers)
            {
                if (allowedWeaponCodes == null || !allowedWeaponCodes.Contains(equippedWeaponCode))
                    continue;

                int followUpLevel = GetSkillLevel(followUpSkillId);
                if (followUpLevel <= 0)
                    continue;

                SkillData followUpSkill = GetSkillData(followUpSkillId);
                SkillLevelData followUpLevelData = followUpSkill?.GetLevel(followUpLevel);
                if (followUpLevelData == null || followUpLevelData.Prop <= 0)
                    continue;

                if (Random.Next(100) >= Math.Clamp(followUpLevelData.Prop, 0, 100))
                    continue;

                QueueOrReplaceFollowUpAttack(new QueuedFollowUpAttack
                {
                    SkillId = followUpSkillId,
                    Level = followUpLevel,
                    ExecuteTime = currentTime + FOLLOW_UP_ATTACK_DELAY,
                    TargetMobId = targetMobId,
                    FacingRight = facingRight,
                    RequiredWeaponCode = equippedWeaponCode
                });
            }
        }

        private void QueueOrReplaceFollowUpAttack(QueuedFollowUpAttack queuedAttack)
        {
            if (queuedAttack == null)
                return;

            // The client keeps one pending final-attack slot and overwrites it when a newer proc wins.
            _queuedFollowUpAttacks.Clear();
            _queuedFollowUpAttacks.Enqueue(queuedAttack);
        }

        private int GetEquippedWeaponCode()
        {
            int itemId = _player.Build?.GetWeapon()?.ItemId ?? 0;
            return itemId > 0 ? Math.Abs(itemId / 10000) % 100 : 0;
        }

        private MobItem FindAttackableMobByPoolId(int poolId, int currentTime)
        {
            if (_mobPool == null || poolId <= 0)
                return null;

            MobItem mob = _mobPool.ActiveMobs.FirstOrDefault(candidate => candidate?.PoolId == poolId && IsMobAttackable(candidate));
            if (mob == null)
                return null;

            return GetMobHitbox(mob, currentTime).IsEmpty ? null : mob;
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
            _queuedFollowUpAttacks.Clear();
            _deferredSkillPayloads.Clear();
            _clientSkillTimers.Clear();
            _skillZones.Clear();
            _rocketBoosterState = null;
            _activeRepeatSkillSustain = null;
            _cycloneState = null;
            _swallowState = null;
            _mineMovementDirection = 0;
            _mineMovementStartTime = 0;
            _player.EndSustainedSkillAnimation();
            ClearSummonPuppets();
            _summons.Clear();
            _hitEffects.Clear();
            if (_preparedSkill != null)
            {
                _player.ClearSkillAvatarTransform(_preparedSkill.SkillId);
                _preparedSkill = null;
            }

            // Remove all buff effects
            foreach (var buff in _buffs)
            {
                ApplyBuffStats(buff, false);
                _player.ClearSkillAvatarTransform(buff.SkillId);
                _player.ClearSkillAvatarEffect(buff.SkillId, Environment.TickCount, playFinish: false);
                ClearSkillMount(buff.SkillId);
            }
            _buffs.Clear();
            RefreshBuffControlledFlyingAbility();

            _cooldowns.Clear();
            _currentCast = null;
            _skillLevels.Clear();
            _skillHotkeys.Clear();
            _availableSkills.Clear();
            _player.ClearSkillAvatarTransformAndPlayExitAction();
            _player.ClearAllSkillAvatarEffects(playFinish: false, Environment.TickCount);
            _player.ClearAllTransientSkillAvatarEffects();
            ClearSkillMount();
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
            _queuedFollowUpAttacks.Clear();
            _deferredSkillPayloads.Clear();
            _clientSkillTimers.Clear();
            _skillZones.Clear();
            _rocketBoosterState = null;
            _activeRepeatSkillSustain = null;
            _cycloneState = null;
            _swallowState = null;
            _mineMovementDirection = 0;
            _mineMovementStartTime = 0;
            _player.EndSustainedSkillAnimation();
            ClearSummonPuppets();
            _hitEffects.Clear();
            _currentCast = null;
            if (_preparedSkill != null)
            {
                _player.ClearSkillAvatarTransform(_preparedSkill.SkillId);
                _preparedSkill = null;
            }

            foreach (var buff in _buffs)
            {
                ApplyBuffStats(buff, false);
                _player.ClearSkillAvatarTransform(buff.SkillId);
                _player.ClearSkillAvatarEffect(buff.SkillId, Environment.TickCount, playFinish: false);
                ClearSkillMount(buff.SkillId);
            }
            _buffs.Clear();
            RefreshBuffControlledFlyingAbility();
            _player.ClearSkillAvatarTransformAndPlayExitAction();
            _player.ClearAllSkillAvatarEffects(playFinish: false, Environment.TickCount);
            _player.ClearAllTransientSkillAvatarEffects();
            ClearSkillMount();

            foreach (var summon in _summons)
            {
                summon.NeedsAnchorReset = SummonMovementResolver.IsAnchorBound(summon.MovementStyle);
            }

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
            _queuedFollowUpAttacks.Clear();
            _deferredSkillPayloads.Clear();
            _clientSkillTimers.Clear();
            _skillZones.Clear();
            _rocketBoosterState = null;
            _activeRepeatSkillSustain = null;
            _cycloneState = null;
            _swallowState = null;
            _mineMovementDirection = 0;
            _mineMovementStartTime = 0;
            _player.EndSustainedSkillAnimation();
            ClearSummonPuppets();
            _summons.Clear();
            _hitEffects.Clear();
            _currentCast = null;
            if (_preparedSkill != null)
            {
                _player.ClearSkillAvatarTransform(_preparedSkill.SkillId);
                _preparedSkill = null;
            }

            if (!clearBuffs)
                return;

            foreach (var buff in _buffs)
            {
                ApplyBuffStats(buff, false);
                _player.ClearSkillAvatarTransform(buff.SkillId);
                _player.ClearSkillAvatarEffect(buff.SkillId, Environment.TickCount, playFinish: false);
                ClearSkillMount(buff.SkillId);
            }
            _buffs.Clear();
            RefreshBuffControlledFlyingAbility();
            _player.ClearSkillAvatarTransformAndPlayExitAction();
            _player.ClearAllSkillAvatarEffects(playFinish: false, Environment.TickCount);
            _player.ClearAllTransientSkillAvatarEffects();
            ClearSkillMount();
        }

        private static bool IsFocusedSingleBookJob(int jobId)
        {
            return jobId >= 800 && jobId < 1000;
        }

        private static bool IsAdminSkillJob(int jobId)
        {
            return jobId == 900 || jobId == 910;
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (maxDelta <= 0f || Math.Abs(target - current) <= maxDelta)
                return target;

            return current + MathF.Sign(target - current) * maxDelta;
        }

        #endregion
    }
}
