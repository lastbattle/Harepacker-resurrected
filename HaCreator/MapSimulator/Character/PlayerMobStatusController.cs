using System;
using System.Collections.Generic;
using HaCreator.MapSimulator;
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
        Freeze
    }

    internal readonly struct PlayerMobStatusFrameState
    {
        public static readonly PlayerMobStatusFrameState Default = new(1f, 0f, false, false, false);

        public PlayerMobStatusFrameState(
            float moveSpeedMultiplier,
            float additionalMissChance,
            bool jumpBlocked,
            bool movementLocked,
            bool skillCastBlocked)
        {
            MoveSpeedMultiplier = moveSpeedMultiplier;
            AdditionalMissChance = additionalMissChance;
            JumpBlocked = jumpBlocked;
            MovementLocked = movementLocked;
            SkillCastBlocked = skillCastBlocked;
        }

        public float MoveSpeedMultiplier { get; }
        public float AdditionalMissChance { get; }
        public bool JumpBlocked { get; }
        public bool MovementLocked { get; }
        public bool SkillCastBlocked { get; }
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
        }

        private readonly Dictionary<PlayerMobStatusEffect, PlayerMobStatusEntry> _entries = new();
        private readonly PlayerCharacter _player;
        private readonly SkillManager _skills;

        public PlayerMobStatusController(PlayerCharacter player, SkillManager skills)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _skills = skills;
        }

        public PlayerMobStatusFrameState Update(int currentTime)
        {
            if (_entries.Count == 0)
            {
                return PlayerMobStatusFrameState.Default;
            }

            List<PlayerMobStatusEffect> expiredEffects = null;
            foreach (KeyValuePair<PlayerMobStatusEffect, PlayerMobStatusEntry> pair in _entries)
            {
                PlayerMobStatusEntry entry = pair.Value;
                if (currentTime >= entry.ExpirationTime)
                {
                    expiredEffects ??= new List<PlayerMobStatusEffect>();
                    expiredEffects.Add(pair.Key);
                    continue;
                }

                if (entry.Effect == PlayerMobStatusEffect.Poison &&
                    entry.Value > 0 &&
                    entry.TickIntervalMs > 0 &&
                    currentTime >= entry.NextTickTime)
                {
                    _player.TakeStatusDamage(entry.Value);
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

            bool movementLocked = HasStatus(PlayerMobStatusEffect.Stun) || HasStatus(PlayerMobStatusEffect.Freeze);
            bool jumpBlocked = movementLocked || HasStatus(PlayerMobStatusEffect.Weakness);
            bool skillCastBlocked = movementLocked || HasStatus(PlayerMobStatusEffect.Seal);
            float moveSpeedMultiplier = ResolveMoveSpeedMultiplier();
            float additionalMissChance = ResolveAdditionalMissChance();

            return new PlayerMobStatusFrameState(
                moveSpeedMultiplier,
                additionalMissChance,
                jumpBlocked,
                movementLocked,
                skillCastBlocked);
        }

        public bool TryApplyMobSkill(int skillId, MobSkillRuntimeData runtimeData, int currentTime)
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
                case 125:
                    ApplyStatus(
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
                case 131:
                    ApplyStatus(PlayerMobStatusEffect.Freeze, runtimeData.DurationMs, currentTime, 1);
                    return true;
                default:
                    return false;
            }
        }

        private bool HasStatus(PlayerMobStatusEffect effect)
        {
            return _entries.ContainsKey(effect);
        }

        private void ApplyStatus(PlayerMobStatusEffect effect, int durationMs, int currentTime, int value, int tickIntervalMs = 0)
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
                return;
            }

            _entries[effect] = new PlayerMobStatusEntry
            {
                Effect = effect,
                ExpirationTime = currentTime + durationMs,
                Value = value,
                TickIntervalMs = tickIntervalMs,
                NextTickTime = tickIntervalMs > 0 ? currentTime + tickIntervalMs : 0
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

        private static int ResolveTickInterval(MobSkillRuntimeData runtimeData, int fallbackMs)
        {
            return runtimeData.IntervalMs > 0 ? runtimeData.IntervalMs : fallbackMs;
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
    }
}
