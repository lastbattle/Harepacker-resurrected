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
        Bomb
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
        }

        private readonly Dictionary<PlayerMobStatusEffect, PlayerMobStatusEntry> _entries = new();
        private readonly PlayerCharacter _player;
        private readonly SkillManager _skills;
        private readonly Action _teleportToSpawn;

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
                case 120:
                    ApplyStatus(PlayerMobStatusEffect.Seal, runtimeData.DurationMs, currentTime, 1);
                    return true;
                case 121:
                    ApplyStatus(PlayerMobStatusEffect.Darkness, runtimeData.DurationMs, currentTime, ResolveValue(runtimeData, 20));
                    return true;
                case 122:
                    ApplyStatus(PlayerMobStatusEffect.Weakness, runtimeData.DurationMs, currentTime, 1);
                    return true;
                case 123:
                    ApplyStatus(PlayerMobStatusEffect.Stun, runtimeData.DurationMs, currentTime, 1);
                    return true;
                case 124:
                    ApplyStatus(PlayerMobStatusEffect.Curse, runtimeData.DurationMs, currentTime, ResolveValue(runtimeData, 50));
                    return true;
                case 125:
                    ApplyPeriodicDamageStatus(
                        PlayerMobStatusEffect.Poison,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 10),
                        ResolveTickInterval(runtimeData, 1000));
                    return true;
                case 126:
                    ApplyStatus(PlayerMobStatusEffect.Slow, runtimeData.DurationMs, currentTime, ResolveValue(runtimeData, 20));
                    return true;
                case 127:
                    _skills?.CancelAllActiveBuffs(currentTime);
                    return true;
                case 129:
                    _teleportToSpawn?.Invoke();
                    ApplyStatus(PlayerMobStatusEffect.Banish, runtimeData.DurationMs, currentTime, 1);
                    return true;
                case 128:
                    ApplyStatus(PlayerMobStatusEffect.Attract, runtimeData.DurationMs, currentTime, ResolveSeduceDirection(sourceX));
                    return true;
                case 131:
                    ApplyStatus(PlayerMobStatusEffect.Freeze, runtimeData.DurationMs, currentTime, 1);
                    return true;
                case 132:
                    ApplyStatus(PlayerMobStatusEffect.ReverseInput, runtimeData.DurationMs, currentTime, 1);
                    return true;
                case 133:
                    ApplyStatus(PlayerMobStatusEffect.Undead, runtimeData.DurationMs, currentTime, ResolveValue(runtimeData, 100));
                    return true;
                case 134:
                    ApplyPeriodicDamageStatus(
                        PlayerMobStatusEffect.PainMark,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 1),
                        ResolveTickInterval(runtimeData, 1000),
                        runtimeData.Count);
                    return true;
                case 135:
                    ApplyStatus(PlayerMobStatusEffect.StopPotion, runtimeData.DurationMs, currentTime, 1);
                    return true;
                case 136:
                    ApplyStatus(PlayerMobStatusEffect.StopMotion, runtimeData.DurationMs, currentTime, 1);
                    return true;
                case 137:
                    ApplyStatus(PlayerMobStatusEffect.Fear, runtimeData.DurationMs, currentTime, ResolveValue(runtimeData, 50));
                    return true;
                case 138:
                    ApplyPeriodicDamageStatus(
                        PlayerMobStatusEffect.Burn,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 1),
                        ResolveTickInterval(runtimeData, 1000),
                        runtimeData.Count);
                    return true;
                case 171:
                    ApplyPeriodicDamageStatus(
                        PlayerMobStatusEffect.Bomb,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 1),
                        ResolveBombTickInterval(runtimeData),
                        runtimeData.Count > 0 ? runtimeData.Count : 1);
                    return true;
                case 172:
                case 173:
                    return ApplyPolymorphStatus(runtimeData, currentTime);
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

            if (ShouldResistRemoteAffectedAreaStatus(
                    skill,
                    _skills?.GetActiveAbnormalStatusResistancePercent(currentTime) ?? 0,
                    _skills?.GetActiveElementalResistancePercent(currentTime) ?? 0,
                    Random.Shared.Next(100)))
            {
                return false;
            }

            bool applied = false;
            for (int i = 0; i < statuses.Count; i++)
            {
                RemoteHostilePlayerAreaStatus status = statuses[i];
                if (!ShouldApplyRemoteAffectedAreaStatus(status.PropPercent, Random.Shared.Next(100)))
                {
                    continue;
                }

                if (status.TickIntervalMs > 0)
                {
                    ApplyPeriodicDamageStatus(
                        status.Effect,
                        status.DurationMs,
                        currentTime,
                        status.Value,
                        status.TickIntervalMs,
                        status.RemainingCount);
                }
                else
                {
                    ApplyStatus(status.Effect, status.DurationMs, currentTime, status.Value);
                }

                applied = true;
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
                120 or 121 or 122 or 123 or 124 or 125 or 126 or 128 or 129 or 131 or 132 or 133 or 135 or 136 or 137 or 138 => true,
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

        public bool CanAutoSelectMobSkill(int skillId, MobSkillRuntimeData runtimeData, int currentTime, float sourceX = 0f)
        {
            RemoveExpiredEffects(currentTime);
            if (runtimeData == null)
            {
                return false;
            }

            switch (skillId)
            {
                case 120:
                    return WouldStatusApplicationChangeState(PlayerMobStatusEffect.Seal, runtimeData.DurationMs, currentTime, 1);
                case 121:
                    return WouldStatusApplicationChangeState(
                        PlayerMobStatusEffect.Darkness,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 20));
                case 122:
                    return WouldStatusApplicationChangeState(PlayerMobStatusEffect.Weakness, runtimeData.DurationMs, currentTime, 1);
                case 123:
                    return WouldStatusApplicationChangeState(PlayerMobStatusEffect.Stun, runtimeData.DurationMs, currentTime, 1);
                case 124:
                    return WouldStatusApplicationChangeState(
                        PlayerMobStatusEffect.Curse,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 50));
                case 125:
                    return WouldPeriodicStatusApplicationChangeState(
                        PlayerMobStatusEffect.Poison,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 10),
                        ResolveTickInterval(runtimeData, 1000),
                        0);
                case 126:
                    return WouldStatusApplicationChangeState(
                        PlayerMobStatusEffect.Slow,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 20));
                case 127:
                    return _skills?.ActiveBuffs?.Count > 0;
                case 128:
                    return WouldStatusApplicationChangeState(
                        PlayerMobStatusEffect.Attract,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveSeduceDirection(sourceX));
                case 129:
                    return WouldStatusApplicationChangeState(PlayerMobStatusEffect.Banish, runtimeData.DurationMs, currentTime, 1);
                case 131:
                    return WouldStatusApplicationChangeState(PlayerMobStatusEffect.Freeze, runtimeData.DurationMs, currentTime, 1);
                case 132:
                    return WouldStatusApplicationChangeState(PlayerMobStatusEffect.ReverseInput, runtimeData.DurationMs, currentTime, 1);
                case 133:
                    return WouldStatusApplicationChangeState(
                        PlayerMobStatusEffect.Undead,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 100));
                case 134:
                    return WouldPeriodicStatusApplicationChangeState(
                        PlayerMobStatusEffect.PainMark,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 1),
                        ResolveTickInterval(runtimeData, 1000),
                        runtimeData.Count);
                case 135:
                    return WouldStatusApplicationChangeState(PlayerMobStatusEffect.StopPotion, runtimeData.DurationMs, currentTime, 1);
                case 136:
                    return WouldStatusApplicationChangeState(PlayerMobStatusEffect.StopMotion, runtimeData.DurationMs, currentTime, 1);
                case 137:
                    return WouldStatusApplicationChangeState(
                        PlayerMobStatusEffect.Fear,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 50));
                case 138:
                    return WouldPeriodicStatusApplicationChangeState(
                        PlayerMobStatusEffect.Burn,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 1),
                        ResolveTickInterval(runtimeData, 1000),
                        runtimeData.Count);
                case 171:
                    return WouldPeriodicStatusApplicationChangeState(
                        PlayerMobStatusEffect.Bomb,
                        runtimeData.DurationMs,
                        currentTime,
                        ResolveValue(runtimeData, 1),
                        ResolveBombTickInterval(runtimeData),
                        runtimeData.Count > 0 ? runtimeData.Count : 1);
                case 172:
                case 173:
                    return WouldPolymorphApplicationChangeState(runtimeData, currentTime);
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

        private void ApplyStatus(PlayerMobStatusEffect effect, int durationMs, int currentTime, int value, int tickIntervalMs = 0)
        {
            ApplyStatus(effect, durationMs, currentTime, value, tickIntervalMs, 0);
        }

        private void ApplyPeriodicDamageStatus(PlayerMobStatusEffect effect, int durationMs, int currentTime, int value, int tickIntervalMs, int count = 0)
        {
            ApplyStatus(effect, durationMs, currentTime, value, tickIntervalMs, count);
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

        private void ApplyStatus(PlayerMobStatusEffect effect, int durationMs, int currentTime, int value, int tickIntervalMs, int remainingCount)
        {
            if (effect == PlayerMobStatusEffect.None || durationMs <= 0)
            {
                return;
            }

            if (_entries.TryGetValue(effect, out PlayerMobStatusEntry existingEntry))
            {
                existingEntry.ExpirationTime = currentTime + durationMs;
                existingEntry.Value = value;
                existingEntry.TickIntervalMs = tickIntervalMs;
                existingEntry.NextTickTime = tickIntervalMs > 0 ? currentTime + tickIntervalMs : 0;
                existingEntry.RemainingCount = Math.Max(0, remainingCount);
                return;
            }

            _entries[effect] = new PlayerMobStatusEntry
            {
                Effect = effect,
                ExpirationTime = currentTime + durationMs,
                Value = value,
                TickIntervalMs = tickIntervalMs,
                NextTickTime = tickIntervalMs > 0 ? currentTime + tickIntervalMs : 0,
                RemainingCount = Math.Max(0, remainingCount)
            };
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
            int value)
        {
            if (effect == PlayerMobStatusEffect.None || durationMs <= 0)
            {
                return false;
            }

            if (!_entries.TryGetValue(effect, out PlayerMobStatusEntry existingEntry))
            {
                return true;
            }

            int targetExpiration = currentTime + durationMs;
            return targetExpiration > existingEntry.ExpirationTime
                   || existingEntry.Value != value;
        }

        private bool WouldPeriodicStatusApplicationChangeState(
            PlayerMobStatusEffect effect,
            int durationMs,
            int currentTime,
            int value,
            int tickIntervalMs,
            int remainingCount)
        {
            if (WouldStatusApplicationChangeState(effect, durationMs, currentTime, value))
            {
                return true;
            }

            if (!_entries.TryGetValue(effect, out PlayerMobStatusEntry existingEntry))
            {
                return false;
            }

            return existingEntry.TickIntervalMs != tickIntervalMs
                   || existingEntry.RemainingCount != Math.Max(0, remainingCount);
        }

        private bool WouldPolymorphApplicationChangeState(MobSkillRuntimeData runtimeData, int currentTime)
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

            int targetExpiration = currentTime + runtimeData.DurationMs;
            return existingEntry.Value != morphTemplateId
                   || targetExpiration > existingEntry.ExpirationTime;
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

