using HaCreator.MapSimulator.UI;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class PreparedSkillHudRules
    {
        private const int DefaultKeyDownGaugeDurationMs = 2000;
        private const int MinimumReleaseChargeDurationMs = 30;
        private const int MonkeyWaveSkillId = 5311002;
        private const int WildHunterSwallowSkillId = 33101005;
        private const int WildHunterSwallowAttackSkillId = 33101007;
        private static readonly HashSet<int> ReleaseTriggeredSkillIds = new()
        {
            2121001,
            2221001,
            2321001,
            3221001,
            22121000,
            22151001,
            4341002,
            4341003,
            MonkeyWaveSkillId,
            WildHunterSwallowSkillId
        };
        private static readonly HashSet<int> SupportedKeyDownSkillIds = new()
        {
            4341002,
            5101004,
            5221004,
            15101003,
            14111006,
            2121001,
            2221001,
            2321001,
            3121004,
            3221001,
            4341003,
            MonkeyWaveSkillId,
            5201002,
            13111002,
            22121000,
            22151001,
            WildHunterSwallowSkillId,
            33121009,
            35001001,
            35101009,
            SG88SkillId
        };
        private static readonly HashSet<int> ReleaseArmedTextSkillIds = new()
        {
            2121001,
            2221001,
            2321001,
            3221001,
            4341002,
            4341003,
            MonkeyWaveSkillId,
            WildHunterSwallowSkillId
        };
        private static readonly HashSet<int> RemoteReleaseFollowUpPayloadSkillIds = new()
        {
            WildHunterSwallowAttackSkillId
        };

        private const int SG88SkillId = 35121003;
        private const int MonkeyWaveFallbackGaugeDurationMs = 1080;
        private static int? _monkeyWaveGaugeDurationMs;
        private static readonly object SkillNameCacheLock = new();
        private static readonly Dictionary<int, string> SkillNameCache = new();

        public static PreparedSkillHudProfile ResolveProfile(int skillId)
        {
            return skillId switch
            {
                SG88SkillId => new PreparedSkillHudProfile(true, "KeyDownBar4", 2000),
                4341002 => new PreparedSkillHudProfile(true, "KeyDownBar1", 600),
                5101004 => new PreparedSkillHudProfile(true, "KeyDownBar1", 1000),
                15101003 => new PreparedSkillHudProfile(true, "KeyDownBar1", 1000),
                14111006 => new PreparedSkillHudProfile(true, "KeyDownBar", 1000),
                2121001 => new PreparedSkillHudProfile(true, "KeyDownBar", 1000),
                2221001 => new PreparedSkillHudProfile(true, "KeyDownBar", 1000),
                2321001 => new PreparedSkillHudProfile(true, "KeyDownBar", 1000),
                3121004 => new PreparedSkillHudProfile(true, "KeyDownBar", 2000),
                3221001 => new PreparedSkillHudProfile(true, "KeyDownBar", 900),
                4341003 => new PreparedSkillHudProfile(true, "KeyDownBar1", 1200),
                // Release-owned branches such as Monkey Wave should use the caller's
                // authored charge window when available, so the profile itself stays open.
                MonkeyWaveSkillId => new PreparedSkillHudProfile(true, "KeyDownBar", 0),
                5201002 => new PreparedSkillHudProfile(true, "KeyDownBar", 1000),
                13111002 => new PreparedSkillHudProfile(true, "KeyDownBar", 1000),
                22121000 => new PreparedSkillHudProfile(true, "KeyDownBar3", 500, PreparedSkillHudSurface.World, showText: false),
                22151001 => new PreparedSkillHudProfile(true, "KeyDownBar2", 500, PreparedSkillHudSurface.World, showText: false),
                WildHunterSwallowSkillId => new PreparedSkillHudProfile(true, "KeyDownBar", 900),
                33121009 => new PreparedSkillHudProfile(true, "KeyDownBar", 2000),
                35001001 => new PreparedSkillHudProfile(true, "KeyDownBar", 2000),
                35101009 => new PreparedSkillHudProfile(true, "KeyDownBar", 2000),
                // The client `get_max_gauge_time` helper falls back to a 2s bar for
                // keydown skills that do not have one of the explicit branch overrides.
                _ => new PreparedSkillHudProfile(true, "KeyDownBar", DefaultKeyDownGaugeDurationMs)
            };
        }

        public static PreparedSkillHudTextVariant ResolveTextVariant(int skillId)
        {
            if (skillId == SG88SkillId)
            {
                return PreparedSkillHudTextVariant.Amplify;
            }

            if (ReleaseArmedTextSkillIds.Contains(skillId))
            {
                return PreparedSkillHudTextVariant.ReleaseArmed;
            }

            return PreparedSkillHudTextVariant.Default;
        }

        public static bool UsesReleaseTriggeredExecution(int skillId) => ReleaseTriggeredSkillIds.Contains(skillId);

        public static bool IsSupportedKeyDownSkill(int skillId) => SupportedKeyDownSkillIds.Contains(skillId);

        public static bool ResolveKeyDownSkillState(int skillId, bool isKeydownSkill)
        {
            return isKeydownSkill
                || UsesReleaseTriggeredExecution(skillId)
                || IsSupportedKeyDownSkill(skillId);
        }

        public static bool UsesRemoteReleaseFollowUpPayload(int skillId)
        {
            return UsesReleaseTriggeredExecution(skillId)
                || RemoteReleaseFollowUpPayloadSkillIds.Contains(skillId);
        }

        public static bool TryResolveRemotePreparedSkillReleaseOwner(int skillId, int? followUpValue, out int preparedSkillId)
        {
            preparedSkillId = 0;
            if (UsesReleaseTriggeredExecution(skillId))
            {
                preparedSkillId = skillId;
                return true;
            }

            if (skillId == WildHunterSwallowAttackSkillId)
            {
                preparedSkillId = WildHunterSwallowSkillId;
                return true;
            }

            return false;
        }

        public static int ResolveGaugeDuration(int skillId, int authoredDurationMs = 0)
        {
            if (authoredDurationMs > 0)
            {
                return authoredDurationMs;
            }

            PreparedSkillHudProfile profile = ResolveProfile(skillId);
            if (profile.GaugeDurationMs > 0)
            {
                return profile.GaugeDurationMs;
            }

            return skillId == MonkeyWaveSkillId
                ? ResolveMonkeyWaveGaugeDuration()
                : 0;
        }

        public static int ResolvePreparedGaugeDuration(int skillId, int explicitGaugeDurationMs = 0, int preparedDurationMs = 0)
        {
            if (explicitGaugeDurationMs > 0)
            {
                return explicitGaugeDurationMs;
            }

            PreparedSkillHudProfile profile = ResolveProfile(skillId);
            if (profile.GaugeDurationMs > 0)
            {
                return profile.GaugeDurationMs;
            }

            if (skillId == MonkeyWaveSkillId)
            {
                return ResolveMonkeyWaveGaugeDuration();
            }

            if (UsesReleaseTriggeredExecution(skillId) && preparedDurationMs > 0)
            {
                return preparedDurationMs;
            }

            return ResolveGaugeDuration(skillId);
        }

        public static int ResolveReleaseChargeElapsedMs(int skillId, int elapsedMs, int gaugeDurationMs = 0)
        {
            int normalizedElapsedMs = Math.Max(0, elapsedMs);
            if (!UsesReleaseTriggeredExecution(skillId))
            {
                return normalizedElapsedMs;
            }

            int normalizedGaugeDurationMs = gaugeDurationMs > 0
                ? gaugeDurationMs
                : ResolveGaugeDuration(skillId);
            int clampedElapsedMs = Math.Max(MinimumReleaseChargeDurationMs, normalizedElapsedMs);
            if (normalizedGaugeDurationMs > 0)
            {
                clampedElapsedMs = Math.Min(clampedElapsedMs, normalizedGaugeDurationMs);
            }

            return clampedElapsedMs;
        }

        public static void ResolveRemotePreparedSkillPhases(
            int skillId,
            bool isKeydownSkill,
            bool isHolding,
            int durationMs,
            int maxHoldDurationMs,
            bool explicitAutoEnterHold,
            out int activeDurationMs,
            out int prepareDurationMs,
            out bool autoEnterHold)
        {
            int normalizedDurationMs = Math.Max(0, durationMs);
            int normalizedMaxHoldDurationMs = Math.Max(0, maxHoldDurationMs);
            bool normalizedIsKeydownSkill = ResolveKeyDownSkillState(skillId, isKeydownSkill);
            bool usesReleaseTriggeredExecution = UsesReleaseTriggeredExecution(skillId);

            autoEnterHold = !isHolding
                && normalizedIsKeydownSkill
                && normalizedDurationMs > 0
                && (explicitAutoEnterHold
                    || usesReleaseTriggeredExecution
                    || (normalizedMaxHoldDurationMs > 0 && normalizedMaxHoldDurationMs != normalizedDurationMs));

            if (autoEnterHold)
            {
                prepareDurationMs = normalizedDurationMs;
                activeDurationMs = normalizedMaxHoldDurationMs;
                return;
            }

            prepareDurationMs = 0;
            activeDurationMs = normalizedDurationMs;
        }

        public static bool UsesChargeDamageScaling(int skillId) => skillId is
            2121001
            or 2221001
            or 2321001
            or 3221001
            or 22121000
            or 22151001
            or 4341002
            or 4341003
            or MonkeyWaveSkillId;

        public static bool ArmsAtFullStrengthOnCriticalHit(int skillId) => skillId == MonkeyWaveSkillId;

        public static bool IsDragonOverlaySkill(int skillId) => skillId is 22121000 or 22151001;

        public static string ResolveDisplayName(int skillId, string explicitName = null)
        {
            if (!string.IsNullOrWhiteSpace(explicitName))
            {
                return explicitName.Trim();
            }

            if (skillId <= 0)
            {
                return "Skill";
            }

            lock (SkillNameCacheLock)
            {
                if (SkillNameCache.TryGetValue(skillId, out string cachedName))
                {
                    return cachedName;
                }
            }

            string resolvedName = $"Skill {skillId}";
            try
            {
                WzImage stringImage = global::HaCreator.Program.FindImage("String", "Skill.img");
                if (stringImage?[skillId.ToString()]?["name"] is WzStringProperty nameProperty
                    && !string.IsNullOrWhiteSpace(nameProperty.Value))
                {
                    resolvedName = nameProperty.Value.Trim();
                }
            }
            catch (Exception)
            {
                resolvedName = $"Skill {skillId}";
            }

            lock (SkillNameCacheLock)
            {
                SkillNameCache[skillId] = resolvedName;
            }

            return resolvedName;
        }

        private static int ResolveMonkeyWaveGaugeDuration()
        {
            if (_monkeyWaveGaugeDurationMs.HasValue)
            {
                return _monkeyWaveGaugeDurationMs.Value;
            }

            int resolvedDuration = MonkeyWaveFallbackGaugeDurationMs;
            try
            {
                WzImage image = global::HaCreator.Program.FindImage("Skill", "531.img");
                if (image?["skill"] is WzSubProperty skillRoot
                    && skillRoot[MonkeyWaveSkillId.ToString()] is WzSubProperty monkeyWave
                    && monkeyWave["prepare"] is WzSubProperty prepare
                    && prepare["time"] is WzIntProperty prepareTime
                    && prepareTime.Value > 0)
                {
                    resolvedDuration = prepareTime.Value;
                }
            }
            catch (Exception)
            {
                resolvedDuration = MonkeyWaveFallbackGaugeDurationMs;
            }

            _monkeyWaveGaugeDurationMs = resolvedDuration;
            return resolvedDuration;
        }

        internal readonly struct PreparedSkillHudProfile
        {
            public PreparedSkillHudProfile(
                bool visible,
                string skinKey,
                int gaugeDurationMs,
                PreparedSkillHudSurface surface = PreparedSkillHudSurface.StatusBar,
                bool showText = true)
            {
                Visible = visible;
                SkinKey = string.IsNullOrWhiteSpace(skinKey) ? "KeyDownBar" : skinKey;
                GaugeDurationMs = gaugeDurationMs;
                Surface = surface;
                ShowText = showText;
            }

            public bool Visible { get; }
            public string SkinKey { get; }
            public int GaugeDurationMs { get; }
            public PreparedSkillHudSurface Surface { get; }
            public bool ShowText { get; }
        }
    }
}
