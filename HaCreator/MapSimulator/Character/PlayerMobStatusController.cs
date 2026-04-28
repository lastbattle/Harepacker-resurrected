using System;
using System.Collections.Generic;
using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.Character.Skills;

namespace HaCreator.MapSimulator.Character
{
    internal enum PlayerMobStatusEffect
    {
        None,
        Seal,
        Darkness,
        Weakness,
        Stun,
        Poison,
        Slow,
        Freeze,
        StopMotion,
        Curse,
        PainMark,
        Banish,
        Attract,
        ReverseInput,
        Undead,
        Polymorph,
        StopPotion,
        Fear,
        Burn,
        Bomb,
        BattlefieldFlag
    }

    internal readonly struct PlayerMobStatusFrameState
    {
        public static readonly PlayerMobStatusFrameState Default = new(1f, 0f, false, false, false, false, 0, false, false, false, 100, 100, 100);

        public PlayerMobStatusFrameState(
            float moveSpeedMultiplier,
            float additionalMissChance,
            bool jumpBlocked,
            bool movementLocked,
            bool skillCastBlocked,
            bool inputReversed,
            int forcedHorizontalDirection,
            bool hpRecoveryReversed,
            bool pickupBlocked,
            bool recoveryBlocked,
            int maxHpPercentCap,
            int maxMpPercentCap,
            int hpRecoveryDamagePercent)
        {
            MoveSpeedMultiplier = moveSpeedMultiplier;
            AdditionalMissChance = additionalMissChance;
            JumpBlocked = jumpBlocked;
            MovementLocked = movementLocked;
            SkillCastBlocked = skillCastBlocked;
            InputReversed = inputReversed;
            ForcedHorizontalDirection = forcedHorizontalDirection;
            HpRecoveryReversed = hpRecoveryReversed;
            PickupBlocked = pickupBlocked;
            RecoveryBlocked = recoveryBlocked;
            MaxHpPercentCap = maxHpPercentCap;
            MaxMpPercentCap = maxMpPercentCap;
            HpRecoveryDamagePercent = hpRecoveryDamagePercent;
        }

        public float MoveSpeedMultiplier { get; }
        public float AdditionalMissChance { get; }
        public bool JumpBlocked { get; }
        public bool MovementLocked { get; }
        public bool SkillCastBlocked { get; }
        public bool InputReversed { get; }
        public int ForcedHorizontalDirection { get; }
        public bool HpRecoveryReversed { get; }
        public bool PickupBlocked { get; }
        public bool RecoveryBlocked { get; }
        public int MaxHpPercentCap { get; }
        public int MaxMpPercentCap { get; }
        public int HpRecoveryDamagePercent { get; }
    }

    internal sealed class PlayerMobStatusController
    {
        private sealed class PlayerMobStatusEntry
        {
            public PlayerMobStatusEffect Effect { get; init; }
            public int ExpirationTime { get; set; }
            public int Value { get; set; }
            public int TickIntervalMs { get; set; }
            public int NextTickTime { get; set; }
            public int RemainingCount { get; set; }
            public int AppliedCount { get; set; }
        }

        private readonly Dictionary<PlayerMobStatusEffect, PlayerMobStatusEntry> _entries = new();
        private readonly PlayerCharacter _player;
        private readonly SkillManager _skills;
        private readonly Action _teleportToSpawn;

        public event Action<PlayerMobStatusEffect, int, int> PeriodicDamageApplied;

        public PlayerMobStatusController(PlayerCharacter player, SkillManager skills, Action teleportToSpawn = null)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _skills = skills;
            _teleportToSpawn = teleportToSpawn;
        }

        public PlayerMobStatusFrameState Update(int currentTime)
        {
            RemoveExpiredEffects(currentTime);
            if (_entries.Count == 0)
            {
                return PlayerMobStatusFrameState.Default;
            }

            List<PlayerMobStatusEffect> expiredEffects = null;
            foreach (KeyValuePair<PlayerMobStatusEffect, PlayerMobStatusEntry> pair in _entries)
            {
                PlayerMobStatusEntry entry = pair.Value;
                bool shouldApplyPeriodicDamage =
                    IsPeriodicDamageStatus(entry.Effect)
                    && entry.Value > 0
                    && entry.TickIntervalMs > 0
                    && currentTime >= entry.NextTickTime;
                if (shouldApplyPeriodicDamage)
                {
                    _player.TakeStatusDamage(entry.Value);
                    PeriodicDamageApplied?.Invoke(entry.Effect, entry.Value, currentTime);
                    if (entry.RemainingCount > 0)
                    {
                        entry.RemainingCount--;
                        if (entry.RemainingCount <= 0)
                        {
                            expiredEffects ??= new List<PlayerMobStatusEffect>();
                            expiredEffects.Add(pair.Key);
                            continue;
                        }
                    }

                    entry.NextTickTime = currentTime + entry.TickIntervalMs;
                }
            }

            if (expiredEffects != null)
            {
                for (int i = 0; i < expiredEffects.Count; i++)
                {
                    _entries.Remove(expiredEffects[i]);
                }
            }

            bool movementLocked = HasStatus(PlayerMobStatusEffect.Stun) ||
                                  HasStatus(PlayerMobStatusEffect.Freeze) ||
                                  HasStatus(PlayerMobStatusEffect.StopMotion) ||
                                  HasStatus(PlayerMobStatusEffect.Banish);
            bool seduced = HasStatus(PlayerMobStatusEffect.Attract);
            bool banished = HasStatus(PlayerMobStatusEffect.Banish);
            bool jumpBlocked = movementLocked || seduced || HasStatus(PlayerMobStatusEffect.Weakness);
            bool polymorphed = HasStatus(PlayerMobStatusEffect.Polymorph);
            bool skillCastBlocked = movementLocked || seduced || banished || polymorphed || HasStatus(PlayerMobStatusEffect.Seal);
            bool pickupBlocked = movementLocked || seduced;
            float moveSpeedMultiplier = ResolveMoveSpeedMultiplier();
            float additionalMissChance = ResolveAdditionalMissChance();
            bool inputReversed = HasStatus(PlayerMobStatusEffect.ReverseInput);
            bool hpRecoveryReversed = HasStatus(PlayerMobStatusEffect.Undead);
            int forcedHorizontalDirection = ResolveForcedHorizontalDirection();
            bool recoveryBlocked = HasStatus(PlayerMobStatusEffect.StopPotion);
            int maxVitalPercentCap = ResolveCurseVitalCapPercent();
            int hpRecoveryDamagePercent = ResolveUndeadRecoveryDamagePercent();

            return new PlayerMobStatusFrameState(
                moveSpeedMultiplier,
                additionalMissChance,
                jumpBlocked,
                movementLocked,
                skillCastBlocked,
                inputReversed,
                forcedHorizontalDirection,
                hpRecoveryReversed,
                pickupBlocked,
                recoveryBlocked,
                maxVitalPercentCap,
                maxVitalPercentCap,
                hpRecoveryDamagePercent);
        }

        public string GetSkillCastRestrictionMessage(int currentTime)
        {
            RemoveExpiredEffects(currentTime);

            if (HasStatus(PlayerMobStatusEffect.Stun))
            {
                return "Skills cannot be used while stunned.";
            }

            if (HasStatus(PlayerMobStatusEffect.Freeze))
            {
                return "Skills cannot be used while frozen.";
            }

            if (HasStatus(PlayerMobStatusEffect.StopMotion))
            {
                return "Skills cannot be used while motion is locked.";
            }

            if (HasStatus(PlayerMobStatusEffect.Seal))
            {
                return "Skills cannot be used while sealed.";
            }

            if (HasStatus(PlayerMobStatusEffect.Attract))
            {
                return "Skills cannot be used while seduced.";
            }

            if (HasStatus(PlayerMobStatusEffect.Polymorph))
            {
                return "Skills cannot be used while polymorphed.";
            }

            if (HasStatus(PlayerMobStatusEffect.Banish))
            {
                return "Skills cannot be used while banished.";
            }

            return null;
        }

        public bool TryApplyMobSkill(int skillId, MobSkillRuntimeData runtimeData, int currentTime, float sourceX = 0f)
        {
            return TryApplyMobSkill(skillId, runtimeData, currentTime, sourceX, elementAttribute: 0);
        }

        public bool TryApplyMobSkill(int skillId, MobSkillRuntimeData runtimeData, int currentTime, float sourceX, int elementAttribute)
        {
            if (_player == null || runtimeData == null)
            {
                return false;
            }

            int propPercent = runtimeData.PropPercent > 0 ? runtimeData.PropPercent : 100;
            if (propPercent < 100 && Random.Shared.Next(100) >= Math.Clamp(propPercent, 0, 100))
            {
                return false;
            }

            if (ShouldResistMobSkillStatus(
                    skillId,
                    _skills?.GetActiveAbnormalStatusResistancePercent(currentTime) ?? 0,
                    _skills?.GetActiveElementalResistancePercent(currentTime) ?? 0,
                    elementAttribute,
                    Random.Shared.Next(100)))
            {
                return false;
            }

            switch (skillId)
            {
                case 170:
                    return ApplyStatus(
                        PlayerMobStatusEffect.StopMotion,
                        ResolveSkillStatusDurationMs(skillId, runtimeData),
                        currentTime,
                        1);
                case 120:
                    return ApplyStatus(PlayerMobStatusEffect.Seal, runtimeData.DurationMs, currentTime, 1);
                case 121:
                    return ApplyStatus(PlayerMobStatusEffect.Darkness, runtimeData.DurationMs, currentTime, ResolveValue(runtimeData, 20));
                case 122:
                    return ApplyStatus(PlayerMobStatusEffect.Weakness, runtimeData.DurationMs, currentTime, 1);
                case 123:
                    return ApplyStatus(PlayerMobStatusEffect.Stun, runtimeData.DurationMs, currentTime, 1);
                case 124:
                    return ApplyStatus(PlayerMobStatusEffect.Curse, runtimeData.DurationMs, currentTime, ResolveValue(runtimeData, 50));
                case 125:
                    return ApplyPeriodicDamageStatus(
                        PlayerMobStatusEffect.Poison,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 10),
                        ResolveTickInterval(runtimeData, 1000));
                case 126:
                    return ApplyStatus(PlayerMobStatusEffect.Slow, runtimeData.DurationMs, currentTime, ResolveValue(runtimeData, 20));
                case 127:
                    if (_skills?.ActiveBuffs == null || _skills.ActiveBuffs.Count == 0)
                    {
                        return false;
                    }

                    _skills.CancelAllActiveBuffs(currentTime);
                    return true;
                case 129:
                    bool teleported = _teleportToSpawn != null;
                    _teleportToSpawn?.Invoke();
                    return ApplyStatus(PlayerMobStatusEffect.Banish, runtimeData.DurationMs, currentTime, 1) || teleported;
                case 128:
                    return ApplyStatus(PlayerMobStatusEffect.Attract, runtimeData.DurationMs, currentTime, ResolveSeduceDirection(sourceX));
                case 131:
                    return ApplyStatus(PlayerMobStatusEffect.Freeze, runtimeData.DurationMs, currentTime, 1);
                case 132:
                    return HasAuthoredPeriodicDamage(runtimeData)
                        ? ApplyPeriodicDamageStatus(
                            PlayerMobStatusEffect.ReverseInput,
                            runtimeData.DurationMs,
                            currentTime,
                            ResolvePeriodicDamageValue(runtimeData, 1),
                            ResolveTickInterval(runtimeData, 1000),
                            runtimeData.Count)
                        : ApplyStatus(PlayerMobStatusEffect.ReverseInput, runtimeData.DurationMs, currentTime, 1);
                case 133:
                    return ApplyStatus(PlayerMobStatusEffect.Undead, runtimeData.DurationMs, currentTime, ResolveValue(runtimeData, 100));
                case 134:
                    return ApplyPeriodicDamageStatus(
                        PlayerMobStatusEffect.PainMark,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 1),
                        ResolveTickInterval(runtimeData, 1000),
                        runtimeData.Count);
                case 135:
                    return ApplyStatus(PlayerMobStatusEffect.StopPotion, runtimeData.DurationMs, currentTime, 1);
                case 136:
                    return ApplyStatus(PlayerMobStatusEffect.StopMotion, runtimeData.DurationMs, currentTime, 1);
                case 137:
                    return ApplyStatus(PlayerMobStatusEffect.Fear, runtimeData.DurationMs, currentTime, ResolveValue(runtimeData, 50));
                case 138:
                    return ApplyPeriodicDamageStatus(
                        PlayerMobStatusEffect.Burn,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 1),
                        ResolveTickInterval(runtimeData, 1000),
                        runtimeData.Count);
                case 171:
                    return ApplyPeriodicDamageStatus(
                        PlayerMobStatusEffect.Bomb,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 1),
                        ResolveBombTickInterval(runtimeData),
                        runtimeData.Count > 0 ? runtimeData.Count : 1);
                case 172:
                case 173:
                    return ApplyPolymorphStatus(runtimeData, currentTime);
                case 799:
                    return ApplyStatus(
                        PlayerMobStatusEffect.BattlefieldFlag,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 1));
                default:
                    return false;
            }
        }

        public bool TryApplyRemoteAffectedAreaPlayerSkill(
            SkillData skill,
            SkillLevelData levelData,
            int currentTime)
        {
            if (_player == null || skill == null || levelData == null)
            {
                return false;
            }

            IReadOnlyList<RemoteHostilePlayerAreaStatus> statuses =
                RemoteAffectedAreaSupportResolver.ResolveHostilePlayerAreaStatuses(skill, levelData);
            if (statuses.Count == 0)
            {
                return false;
            }

            int abnormalStatusResistancePercent = _skills?.GetActiveAbnormalStatusResistancePercent(currentTime) ?? 0;
            int elementalResistancePercent = _skills?.GetActiveElementalResistancePercent(currentTime) ?? 0;
            bool applied = false;
            for (int i = 0; i < statuses.Count; i++)
            {
                RemoteHostilePlayerAreaStatus status = statuses[i];
                if (!ShouldApplyRemoteAffectedAreaStatus(status.PropPercent, Random.Shared.Next(100))
                    || ShouldResistRemoteAffectedAreaStatus(
                        skill,
                        abnormalStatusResistancePercent,
                        elementalResistancePercent,
                        Random.Shared.Next(100)))
                {
                    continue;
                }

                bool statusApplied = status.TickIntervalMs > 0
                    ? ApplyPeriodicDamageStatus(
                        status.Effect,
                        status.DurationMs,
                        currentTime,
                        status.Value,
                        status.TickIntervalMs,
                        status.RemainingCount)
                    : ApplyStatus(status.Effect, status.DurationMs, currentTime, status.Value);

                applied |= statusApplied;
            }

            return applied;
        }

        internal static bool ShouldApplyRemoteAffectedAreaStatus(int propPercent, int rollPercent)
        {
            int clampedPropPercent = propPercent > 0
                ? Math.Clamp(propPercent, 0, 100)
                : 100;
            return clampedPropPercent > 0
                   && (clampedPropPercent >= 100 || Math.Clamp(rollPercent, 0, 99) < clampedPropPercent);
        }

        internal static bool ShouldApplyRemoteAffectedAreaStatus(
            SkillData skill,
            RemoteHostilePlayerAreaStatus status,
            int abnormalStatusResistancePercent,
            int elementalResistancePercent,
            int statusRollPercent,
            int resistanceRollPercent)
        {
            return ShouldApplyRemoteAffectedAreaStatus(status.PropPercent, statusRollPercent)
                   && !ShouldResistRemoteAffectedAreaStatus(
                       skill,
                       abnormalStatusResistancePercent,
                       elementalResistancePercent,
                       resistanceRollPercent);
        }

        internal static bool ShouldResistRemoteAffectedAreaStatus(
            SkillData skill,
            int abnormalStatusResistancePercent,
            int elementalResistancePercent,
            int rollPercent)
        {
            if (skill == null)
            {
                return false;
            }

            int resistancePercent = Math.Max(0, abnormalStatusResistancePercent);
            if (skill.Element != SkillElement.Physical)
            {
                resistancePercent += Math.Max(0, elementalResistancePercent);
            }

            resistancePercent = Math.Clamp(resistancePercent, 0, 100);
            return resistancePercent > 0 && rollPercent < resistancePercent;
        }

        internal static bool ShouldResistMobSkillStatus(
            int skillId,
            int abnormalStatusResistancePercent,
            int elementalResistancePercent,
            int elementAttribute,
            int rollPercent)
        {
            if (!IsResistibleMobSkillStatus(skillId))
            {
                return false;
            }

            int resistancePercent = Math.Max(0, abnormalStatusResistancePercent);
            if (elementAttribute > 0)
            {
                resistancePercent += Math.Max(0, elementalResistancePercent);
            }

            resistancePercent = Math.Clamp(resistancePercent, 0, 100);
            return resistancePercent > 0 && Math.Clamp(rollPercent, 0, 99) < resistancePercent;
        }

        private static bool IsResistibleMobSkillStatus(int skillId)
        {
            return skillId switch
            {
                120 or 121 or 122 or 123 or 124 or 125 or 126 or 127 or 128 or 129 or 131 or 132 or 133 or 134 or 135 or 136 or 137 or 138 or 170 or 171 or 172 or 173 => true,
                _ => false
            };
        }

        private bool HasStatus(PlayerMobStatusEffect effect)
        {
            return _entries.ContainsKey(effect);
        }

        private static bool IsPeriodicDamageStatus(PlayerMobStatusEffect effect)
        {
            return effect == PlayerMobStatusEffect.Poison
                   || effect == PlayerMobStatusEffect.Burn
                   || effect == PlayerMobStatusEffect.PainMark
                   || effect == PlayerMobStatusEffect.ReverseInput
                   || effect == PlayerMobStatusEffect.Bomb;
        }

        public bool ClearStatus(PlayerMobStatusEffect effect)
        {
            if (effect == PlayerMobStatusEffect.None)
            {
                return false;
            }

            bool removed = _entries.Remove(effect);
            if (!removed)
            {
                return false;
            }

            if (TryMapBlockingStatus(effect, out PlayerSkillBlockingStatus blockingStatus))
            {
                _player.ClearSkillBlockingStatus(blockingStatus);
            }

            if (effect == PlayerMobStatusEffect.Polymorph)
            {
                _player.ClearExternalAvatarTransform((int)PlayerMobStatusEffect.Polymorph);
            }

            return true;
        }

        public bool HasStatusEffect(PlayerMobStatusEffect effect)
        {
            return HasStatus(effect);
        }

        public bool HasAppliedMobSkillState(int skillId, int currentTime)
        {
            RemoveExpiredEffects(currentTime);
            if (!TryMapMobSkillStatusEffect(skillId, out PlayerMobStatusEffect effect))
            {
                return false;
            }

            return HasStatus(effect);
        }

        public bool CanAutoSelectMobSkill(
            int skillId,
            MobSkillRuntimeData runtimeData,
            int currentTime,
            float sourceX = 0f,
            int recastLeadTimeMs = 0)
        {
            RemoveExpiredEffects(currentTime);
            if (runtimeData == null)
            {
                return false;
            }

            int statusDurationMs = skillId == 170
                ? ResolveSkillStatusDurationMs(skillId, runtimeData)
                : runtimeData.DurationMs;
            int refreshLeadTimeMs = ResolveStatusRefreshLeadTimeMs(statusDurationMs, recastLeadTimeMs);

            switch (skillId)
            {
                case 170:
                    return WouldStatusApplicationChangeState(
                        PlayerMobStatusEffect.StopMotion,
                        ResolveSkillStatusDurationMs(skillId, runtimeData),
                        currentTime,
                        1,
                        refreshLeadTimeMs);
                case 120:
                    return WouldStatusApplicationChangeState(
                        PlayerMobStatusEffect.Seal,
                        runtimeData.DurationMs,
                        currentTime,
                        1,
                        refreshLeadTimeMs);
                case 121:
                    return WouldStatusApplicationChangeState(
                        PlayerMobStatusEffect.Darkness,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 20),
                        refreshLeadTimeMs);
                case 122:
                    return WouldStatusApplicationChangeState(
                        PlayerMobStatusEffect.Weakness,
                        runtimeData.DurationMs,
                        currentTime,
                        1,
                        refreshLeadTimeMs);
                case 123:
                    return WouldStatusApplicationChangeState(
                        PlayerMobStatusEffect.Stun,
                        runtimeData.DurationMs,
                        currentTime,
                        1,
                        refreshLeadTimeMs);
                case 124:
                    return WouldStatusApplicationChangeState(
                        PlayerMobStatusEffect.Curse,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 50),
                        refreshLeadTimeMs);
                case 125:
                    return WouldPeriodicStatusApplicationChangeState(
                        PlayerMobStatusEffect.Poison,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 10),
                        ResolveTickInterval(runtimeData, 1000),
                        0,
                        refreshLeadTimeMs);
                case 126:
                    return WouldStatusApplicationChangeState(
                        PlayerMobStatusEffect.Slow,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 20),
                        refreshLeadTimeMs);
                case 127:
                    return _skills?.ActiveBuffs?.Count > 0;
                case 128:
                    return WouldStatusApplicationChangeState(
                        PlayerMobStatusEffect.Attract,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveSeduceDirection(sourceX),
                        refreshLeadTimeMs);
                case 129:
                    return WouldStatusApplicationChangeState(
                        PlayerMobStatusEffect.Banish,
                        runtimeData.DurationMs,
                        currentTime,
                        1,
                        refreshLeadTimeMs);
                case 131:
                    return WouldStatusApplicationChangeState(
                        PlayerMobStatusEffect.Freeze,
                        runtimeData.DurationMs,
                        currentTime,
                        1,
                        refreshLeadTimeMs);
                case 132:
                    return HasAuthoredPeriodicDamage(runtimeData)
                        ? WouldPeriodicStatusApplicationChangeState(
                            PlayerMobStatusEffect.ReverseInput,
                            runtimeData.DurationMs,
                            currentTime,
                            ResolvePeriodicDamageValue(runtimeData, 1),
                            ResolveTickInterval(runtimeData, 1000),
                            runtimeData.Count,
                            refreshLeadTimeMs)
                        : WouldStatusApplicationChangeState(
                            PlayerMobStatusEffect.ReverseInput,
                            runtimeData.DurationMs,
                            currentTime,
                            1,
                            refreshLeadTimeMs);
                case 133:
                    return WouldStatusApplicationChangeState(
                        PlayerMobStatusEffect.Undead,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 100),
                        refreshLeadTimeMs);
                case 134:
                    return WouldPeriodicStatusApplicationChangeState(
                        PlayerMobStatusEffect.PainMark,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 1),
                        ResolveTickInterval(runtimeData, 1000),
                        runtimeData.Count,
                        refreshLeadTimeMs);
                case 135:
                    return WouldStatusApplicationChangeState(
                        PlayerMobStatusEffect.StopPotion,
                        runtimeData.DurationMs,
                        currentTime,
                        1,
                        refreshLeadTimeMs);
                case 136:
                    return WouldStatusApplicationChangeState(
                        PlayerMobStatusEffect.StopMotion,
                        runtimeData.DurationMs,
                        currentTime,
                        1,
                        refreshLeadTimeMs);
                case 137:
                    return WouldStatusApplicationChangeState(
                        PlayerMobStatusEffect.Fear,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 50),
                        refreshLeadTimeMs);
                case 138:
                    return WouldPeriodicStatusApplicationChangeState(
                        PlayerMobStatusEffect.Burn,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 1),
                        ResolveTickInterval(runtimeData, 1000),
                        runtimeData.Count,
                        refreshLeadTimeMs);
                case 171:
                    return WouldPeriodicStatusApplicationChangeState(
                        PlayerMobStatusEffect.Bomb,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 1),
                        ResolveBombTickInterval(runtimeData),
                        runtimeData.Count > 0 ? runtimeData.Count : 1,
                        refreshLeadTimeMs);
                case 172:
                case 173:
                    return WouldPolymorphApplicationChangeState(runtimeData, currentTime, refreshLeadTimeMs);
                case 799:
                    return WouldStatusApplicationChangeState(
                        PlayerMobStatusEffect.BattlefieldFlag,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 1),
                        refreshLeadTimeMs);
                default:
                    return false;
            }
        }

        public bool TryGetFearVisualState(int currentTime, out float intensity, out int remainingDurationMs)
        {
            RemoveExpiredEffects(currentTime);

            intensity = 0f;
            remainingDurationMs = 0;
            if (!_entries.TryGetValue(PlayerMobStatusEffect.Fear, out PlayerMobStatusEntry entry))
            {
                return false;
            }

            remainingDurationMs = Math.Max(0, entry.ExpirationTime - currentTime);
            if (remainingDurationMs <= 0)
            {
                return false;
            }

            int fearPercent = entry.Value > 0 ? entry.Value : 50;
            intensity = Math.Clamp(fearPercent / 100f, 0.1f, 0.95f);
            return true;
        }

        public int ClearStatuses(IEnumerable<PlayerMobStatusEffect> effects)
        {
            if (effects == null)
            {
                return 0;
            }

            int cleared = 0;
            foreach (PlayerMobStatusEffect effect in effects)
            {
                if (ClearStatus(effect))
                {
                    cleared++;
                }
            }

            return cleared;
        }

        public int AdjustExperienceReward(int baseAmount, int currentTime)
        {
            int amount = Math.Max(0, baseAmount);
            if (amount <= 0)
            {
                return 0;
            }

            RemoveExpiredEffects(currentTime);
            if (!_entries.TryGetValue(PlayerMobStatusEffect.Curse, out _))
            {
                return amount;
            }

            int experiencePercent = ResolveCurseVitalCapPercent();
            return Math.Max(1, (int)Math.Ceiling(amount * (experiencePercent / 100d)));
        }

        private bool ApplyStatus(PlayerMobStatusEffect effect, int durationMs, int currentTime, int value, int tickIntervalMs = 0)
        {
            return ApplyStatus(effect, durationMs, currentTime, value, tickIntervalMs, 0);
        }

        private bool ApplyPeriodicDamageStatus(PlayerMobStatusEffect effect, int durationMs, int currentTime, int value, int tickIntervalMs, int count = 0)
        {
            return ApplyStatus(effect, durationMs, currentTime, value, tickIntervalMs, count);
        }

        private bool ApplyPolymorphStatus(MobSkillRuntimeData runtimeData, int currentTime)
        {
            int morphTemplateId = runtimeData?.X ?? 0;
            if (morphTemplateId <= 0
                || !_player.ApplyExternalAvatarTransform((int)PlayerMobStatusEffect.Polymorph, actionName: null, morphTemplateId))
            {
                return false;
            }

            ApplyStatus(PlayerMobStatusEffect.Polymorph, runtimeData.DurationMs, currentTime, morphTemplateId);
            return true;
        }

        private bool ApplyStatus(PlayerMobStatusEffect effect, int durationMs, int currentTime, int value, int tickIntervalMs, int remainingCount)
        {
            if (effect == PlayerMobStatusEffect.None || durationMs <= 0)
            {
                return false;
            }

            if (_entries.TryGetValue(effect, out PlayerMobStatusEntry existingEntry))
            {
                existingEntry.ExpirationTime = currentTime + durationMs;
                existingEntry.Value = value;
                existingEntry.TickIntervalMs = tickIntervalMs;
                existingEntry.NextTickTime = tickIntervalMs > 0 ? currentTime + tickIntervalMs : 0;
                int clampedCount = Math.Max(0, remainingCount);
                existingEntry.RemainingCount = clampedCount;
                existingEntry.AppliedCount = clampedCount;
                return true;
            }

            int appliedCount = Math.Max(0, remainingCount);
            _entries[effect] = new PlayerMobStatusEntry
            {
                Effect = effect,
                ExpirationTime = currentTime + durationMs,
                Value = value,
                TickIntervalMs = tickIntervalMs,
                NextTickTime = tickIntervalMs > 0 ? currentTime + tickIntervalMs : 0,
                RemainingCount = appliedCount,
                AppliedCount = appliedCount
            };
            return true;
        }

        private float ResolveMoveSpeedMultiplier()
        {
            if (!_entries.TryGetValue(PlayerMobStatusEffect.Slow, out PlayerMobStatusEntry slowEntry))
            {
                return 1f;
            }

            float slowPercent = Math.Clamp(slowEntry.Value, 0, 85);
            return Math.Max(0.15f, 1f - slowPercent / 100f);
        }

        private float ResolveAdditionalMissChance()
        {
            if (!_entries.TryGetValue(PlayerMobStatusEffect.Darkness, out PlayerMobStatusEntry darknessEntry))
            {
                return 0f;
            }

            int darknessPercent = darknessEntry.Value > 0 ? darknessEntry.Value : 20;
            return Math.Clamp(darknessPercent / 100f, 0.05f, 0.85f);
        }

        private int ResolveForcedHorizontalDirection()
        {
            if (!_entries.TryGetValue(PlayerMobStatusEffect.Attract, out PlayerMobStatusEntry entry))
            {
                return 0;
            }

            return entry.Value switch
            {
                < 0 => -1,
                > 0 => 1,
                _ => 0
            };
        }

        private int ResolveCurseVitalCapPercent()
        {
            if (!_entries.TryGetValue(PlayerMobStatusEffect.Curse, out PlayerMobStatusEntry entry))
            {
                return 100;
            }

            int percent = entry.Value;
            if (percent < 10 || percent > 90)
            {
                percent = 50;
            }

            return percent;
        }

        private int ResolveUndeadRecoveryDamagePercent()
        {
            if (!_entries.TryGetValue(PlayerMobStatusEffect.Undead, out PlayerMobStatusEntry entry))
            {
                return 100;
            }

            int percent = entry.Value;
            if (percent <= 0)
            {
                return 100;
            }

            return Math.Clamp(percent, 1, 100);
        }

        private bool WouldStatusApplicationChangeState(
            PlayerMobStatusEffect effect,
            int durationMs,
            int currentTime,
            int value,
            int refreshLeadTimeMs = 0)
        {
            if (effect == PlayerMobStatusEffect.None || durationMs <= 0)
            {
                return false;
            }

            if (!_entries.TryGetValue(effect, out PlayerMobStatusEntry existingEntry))
            {
                return true;
            }

            if (existingEntry.Value != value)
            {
                return true;
            }

            int remainingDurationMs = existingEntry.ExpirationTime - currentTime;
            return ShouldAllowNoOpStatusRefreshRecast(remainingDurationMs, refreshLeadTimeMs);
        }

        private bool WouldPeriodicStatusApplicationChangeState(
            PlayerMobStatusEffect effect,
            int durationMs,
            int currentTime,
            int value,
            int tickIntervalMs,
            int remainingCount,
            int refreshLeadTimeMs = 0)
        {
            if (WouldStatusApplicationChangeState(effect, durationMs, currentTime, value, refreshLeadTimeMs))
            {
                return true;
            }

            if (!_entries.TryGetValue(effect, out PlayerMobStatusEntry existingEntry))
            {
                return false;
            }

            int expectedCount = Math.Max(0, remainingCount);
            return existingEntry.TickIntervalMs != tickIntervalMs
                   || existingEntry.AppliedCount != expectedCount;
        }

        private bool WouldPolymorphApplicationChangeState(
            MobSkillRuntimeData runtimeData,
            int currentTime,
            int refreshLeadTimeMs = 0)
        {
            if (runtimeData == null || runtimeData.DurationMs <= 0)
            {
                return false;
            }

            int morphTemplateId = runtimeData.X;
            if (morphTemplateId <= 0)
            {
                return false;
            }

            if (!_entries.TryGetValue(PlayerMobStatusEffect.Polymorph, out PlayerMobStatusEntry existingEntry))
            {
                return _player?.CanApplyExternalAvatarTransform((int)PlayerMobStatusEffect.Polymorph, actionName: null, morphTemplateId) == true;
            }

            return existingEntry.Value != morphTemplateId
                   || ShouldAllowNoOpStatusRefreshRecast(existingEntry.ExpirationTime - currentTime, refreshLeadTimeMs);
        }

        internal static int ResolveStatusRefreshLeadTimeMs(int durationMs, int recastLeadTimeMs)
        {
            int clampedDurationMs = Math.Max(0, durationMs);
            if (clampedDurationMs <= 0)
            {
                return 0;
            }

            return Math.Clamp(Math.Max(0, recastLeadTimeMs), 0, clampedDurationMs);
        }

        internal static bool ShouldAllowNoOpStatusRefreshRecast(int remainingDurationMs, int refreshLeadTimeMs)
        {
            if (refreshLeadTimeMs <= 0)
            {
                return false;
            }

            return remainingDurationMs <= refreshLeadTimeMs;
        }

        private int ResolveSeduceDirection(float sourceX)
        {
            if (sourceX < _player.X)
            {
                return -1;
            }

            if (sourceX > _player.X)
            {
                return 1;
            }

            return _player.FacingRight ? 1 : -1;
        }
        private void RemoveExpiredEffects(int currentTime)
        {
            if (_entries.Count == 0)
            {
                return;
            }

            List<PlayerMobStatusEffect> expiredEffects = null;
            foreach (KeyValuePair<PlayerMobStatusEffect, PlayerMobStatusEntry> pair in _entries)
            {
                if (currentTime < pair.Value.ExpirationTime)
                {
                    continue;
                }

                expiredEffects ??= new List<PlayerMobStatusEffect>();
                expiredEffects.Add(pair.Key);
            }

            if (expiredEffects == null)
            {
                return;
            }

            for (int i = 0; i < expiredEffects.Count; i++)
            {
                ClearStatus(expiredEffects[i]);
            }
        }

        private static int ResolveTickInterval(MobSkillRuntimeData runtimeData, int fallbackMs)
        {
            return runtimeData.IntervalMs > 0 ? runtimeData.IntervalMs : fallbackMs;
        }

        internal static int ResolveSkillStatusDurationMs(int skillId, MobSkillRuntimeData runtimeData)
        {
            if (runtimeData == null)
            {
                return 0;
            }

            if (runtimeData.DurationMs > 0)
            {
                return runtimeData.DurationMs;
            }

            // `MobSkill.img/170` publishes `x` + `interval` and commonly omits `time`.
            if (skillId == 170)
            {
                int xSeconds = Math.Max(0, runtimeData.X);
                return xSeconds > 0 ? xSeconds * 1000 : 1000;
            }

            return 0;
        }

        private static bool TryMapMobSkillStatusEffect(int skillId, out PlayerMobStatusEffect effect)
        {
            switch (skillId)
            {
                case 120:
                    effect = PlayerMobStatusEffect.Seal;
                    return true;
                case 121:
                    effect = PlayerMobStatusEffect.Darkness;
                    return true;
                case 122:
                    effect = PlayerMobStatusEffect.Weakness;
                    return true;
                case 123:
                    effect = PlayerMobStatusEffect.Stun;
                    return true;
                case 124:
                    effect = PlayerMobStatusEffect.Curse;
                    return true;
                case 125:
                    effect = PlayerMobStatusEffect.Poison;
                    return true;
                case 126:
                    effect = PlayerMobStatusEffect.Slow;
                    return true;
                case 128:
                    effect = PlayerMobStatusEffect.Attract;
                    return true;
                case 129:
                    effect = PlayerMobStatusEffect.Banish;
                    return true;
                case 131:
                    effect = PlayerMobStatusEffect.Freeze;
                    return true;
                case 132:
                    effect = PlayerMobStatusEffect.ReverseInput;
                    return true;
                case 133:
                    effect = PlayerMobStatusEffect.Undead;
                    return true;
                case 134:
                    effect = PlayerMobStatusEffect.PainMark;
                    return true;
                case 135:
                    effect = PlayerMobStatusEffect.StopPotion;
                    return true;
                case 136:
                case 170:
                    effect = PlayerMobStatusEffect.StopMotion;
                    return true;
                case 137:
                    effect = PlayerMobStatusEffect.Fear;
                    return true;
                case 138:
                    effect = PlayerMobStatusEffect.Burn;
                    return true;
                case 171:
                    effect = PlayerMobStatusEffect.Bomb;
                    return true;
                case 172:
                case 173:
                    effect = PlayerMobStatusEffect.Polymorph;
                    return true;
                case 799:
                    effect = PlayerMobStatusEffect.BattlefieldFlag;
                    return true;
                default:
                    effect = PlayerMobStatusEffect.None;
                    return false;
            }
        }

        private static int ResolveBombTickInterval(MobSkillRuntimeData runtimeData)
        {
            if (runtimeData == null)
            {
                return 1000;
            }

            if (runtimeData.BombDelayMs > 0)
            {
                return runtimeData.BombDelayMs;
            }

            return ResolveTickInterval(runtimeData, 1000);
        }

        private static int ResolveValue(MobSkillRuntimeData runtimeData, int fallbackValue)
        {
            if (runtimeData == null)
            {
                return fallbackValue;
            }

            if (runtimeData.X > 0)
            {
                return runtimeData.X;
            }

            if (runtimeData.Hp > 0)
            {
                return runtimeData.Hp;
            }

            return fallbackValue;
        }

        private static bool HasAuthoredPeriodicDamage(MobSkillRuntimeData runtimeData)
        {
            return runtimeData != null
                   && runtimeData.Hp > 0
                   && (runtimeData.IntervalMs > 0 || runtimeData.Count > 0);
        }

        private static int ResolvePeriodicDamageValue(MobSkillRuntimeData runtimeData, int fallbackValue)
        {
            if (runtimeData == null)
            {
                return fallbackValue;
            }

            if (runtimeData.Hp > 0)
            {
                return runtimeData.Hp;
            }

            if (runtimeData.X > 0)
            {
                return runtimeData.X;
            }

            return fallbackValue;
        }

        private static bool TryMapBlockingStatus(PlayerMobStatusEffect effect, out PlayerSkillBlockingStatus status)
        {
            switch (effect)
            {
                case PlayerMobStatusEffect.Seal:
                    status = PlayerSkillBlockingStatus.Seal;
                    return true;
                case PlayerMobStatusEffect.Stun:
                    status = PlayerSkillBlockingStatus.Stun;
                    return true;
                case PlayerMobStatusEffect.Freeze:
                    status = PlayerSkillBlockingStatus.Freeze;
                    return true;
                case PlayerMobStatusEffect.StopMotion:
                    status = PlayerSkillBlockingStatus.StopMotion;
                    return true;
                case PlayerMobStatusEffect.Attract:
                    status = PlayerSkillBlockingStatus.Attract;
                    return true;
                case PlayerMobStatusEffect.Polymorph:
                    status = PlayerSkillBlockingStatus.Polymorph;
                    return true;
                default:
                    status = default;
                    return false;
            }
        }
    }
}

