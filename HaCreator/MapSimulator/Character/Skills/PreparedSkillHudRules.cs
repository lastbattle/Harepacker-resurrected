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
        private const int FireArrowIceArrowSkillId = 2111002;
        private const int AssaulterShadowChargeSkillId = 4211001;
        private const int PirateScrewPunchSkillId = 5101004;
        private const int MonkeyWaveSkillId = 5311002;
        private const int ThunderBreakerSharkWaveSkillId = 15101003;
        private const int WildHunterSwallowSkillId = 33101005;
        private const int WildHunterSwallowAttackSkillId = 33101007;
        private static readonly HashSet<int> ReleaseTriggeredSkillIds = new()
        {
            FireArrowIceArrowSkillId,
            2121001,
            2221001,
            2321001,
            3221001,
            AssaulterShadowChargeSkillId,
            PirateScrewPunchSkillId,
            ThunderBreakerSharkWaveSkillId,
            22121000,
            22151001,
            4341002,
            4341003,
            MonkeyWaveSkillId,
            WildHunterSwallowSkillId
        };
        private static readonly HashSet<int> NonKeyDownPreparedReleaseSkillIds = new()
        {
            FireArrowIceArrowSkillId,
            AssaulterShadowChargeSkillId
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
            23121000,
            24121000,
            24121005,
            31001000,
            31101000,
            31111005,
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
            5721001,
            SG88SkillId
        };
        private static readonly HashSet<int> ReleaseArmedTextSkillIds = new()
        {
            2121001,
            2221001,
            2321001,
            3221001,
            PirateScrewPunchSkillId,
            4341002,
            4341003,
            MonkeyWaveSkillId,
            ThunderBreakerSharkWaveSkillId,
            WildHunterSwallowSkillId
        };
        private static readonly HashSet<int> RemoteReleaseFollowUpPayloadSkillIds = new()
        {
            WildHunterSwallowAttackSkillId
        };

        private const int SG88SkillId = 35121003;
        private const int MonkeyWaveFallbackGaugeDurationMs = 1080;
        private static int? _monkeyWaveGaugeDurationMs;
        private static readonly object AuthoredKeydownSkillCacheLock = new();
        private static readonly Dictionary<int, bool> AuthoredKeydownSkillCache = new();
        private static readonly object SkillNameCacheLock = new();
        private static readonly Dictionary<int, string> SkillNameCache = new();

        public static PreparedSkillHudProfile ResolveProfile(int skillId)
        {
            return skillId switch
            {
                SG88SkillId => new PreparedSkillHudProfile(true, "KeyDownBar4", 2000),
                4341002 => new PreparedSkillHudProfile(true, "KeyDownBar1", 600),
                PirateScrewPunchSkillId => new PreparedSkillHudProfile(true, "KeyDownBar1", 1000),
                ThunderBreakerSharkWaveSkillId => new PreparedSkillHudProfile(true, "KeyDownBar1", 1000),
                14111006 => new PreparedSkillHudProfile(true, "KeyDownBar", 1000),
                2121001 => new PreparedSkillHudProfile(true, "KeyDownBar", 1000),
                2221001 => new PreparedSkillHudProfile(true, "KeyDownBar", 1000),
                2321001 => new PreparedSkillHudProfile(true, "KeyDownBar", 1000),
                3121004 => new PreparedSkillHudProfile(false, "KeyDownBar", 2000),
                3221001 => new PreparedSkillHudProfile(true, "KeyDownBar", 900),
                4341003 => new PreparedSkillHudProfile(true, "KeyDownBar1", 1200),
                // Release-owned branches such as Monkey Wave should use the caller's
                // authored charge window when available, so the profile itself stays open.
                MonkeyWaveSkillId => new PreparedSkillHudProfile(true, "KeyDownBar", 0),
                5221004 => new PreparedSkillHudProfile(false, "KeyDownBar", 2000),
                5201002 => new PreparedSkillHudProfile(true, "KeyDownBar", 1000),
                13111002 => new PreparedSkillHudProfile(false, "KeyDownBar", 1000),
                22121000 => new PreparedSkillHudProfile(true, "KeyDownBar3", 500, PreparedSkillHudSurface.World, showText: false),
                22151001 => new PreparedSkillHudProfile(true, "KeyDownBar2", 500, PreparedSkillHudSurface.World, showText: false),
                WildHunterSwallowSkillId => new PreparedSkillHudProfile(true, "KeyDownBar", 900),
                33121009 => new PreparedSkillHudProfile(false, "KeyDownBar", 2000),
                35001001 => new PreparedSkillHudProfile(false, "KeyDownBar", 2000),
                35101009 => new PreparedSkillHudProfile(false, "KeyDownBar", 2000),
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

        public static bool UsesReleaseTriggeredKeydownExecution(int skillId)
        {
            return UsesReleaseTriggeredExecution(skillId)
                && !NonKeyDownPreparedReleaseSkillIds.Contains(skillId);
        }

        public static bool IsSupportedKeyDownSkill(int skillId)
        {
            return SupportedKeyDownSkillIds.Contains(skillId)
                || HasAuthoredKeydownNode(skillId);
        }

        public static bool ResolveKeyDownSkillState(int skillId, bool isKeydownSkill)
        {
            if (NonKeyDownPreparedReleaseSkillIds.Contains(skillId))
            {
                return false;
            }

            return isKeydownSkill
                || UsesReleaseTriggeredKeydownExecution(skillId)
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
            if (followUpValue.HasValue)
            {
                int followUpSkillId = followUpValue.Value;
                if (UsesReleaseTriggeredExecution(followUpSkillId))
                {
                    preparedSkillId = followUpSkillId;
                    return true;
                }
            }

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

            if (UsesReleaseTriggeredKeydownExecution(skillId) && preparedDurationMs > 0)
            {
                return preparedDurationMs;
            }

            return ResolveGaugeDuration(skillId);
        }

        public static int ResolveReleaseChargeElapsedMs(int skillId, int elapsedMs, int gaugeDurationMs = 0)
        {
            int normalizedElapsedMs = Math.Max(0, elapsedMs);
            if (!UsesReleaseTriggeredKeydownExecution(skillId))
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
            bool hasHoldWindow = normalizedMaxHoldDurationMs > 0;
            bool hasDistinctHoldWindow = hasHoldWindow
                && normalizedMaxHoldDurationMs != normalizedDurationMs;
            bool supportsAutoHold = explicitAutoEnterHold || hasDistinctHoldWindow;

            autoEnterHold = !isHolding
                && normalizedIsKeydownSkill
                && supportsAutoHold
                && (normalizedDurationMs > 0
                    || (hasHoldWindow && UsesReleaseTriggeredKeydownExecution(skillId))
                    || (hasHoldWindow && explicitAutoEnterHold));

            if (autoEnterHold)
            {
                prepareDurationMs = normalizedDurationMs;
                activeDurationMs = hasHoldWindow
                    ? normalizedMaxHoldDurationMs
                    : normalizedDurationMs;
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
            or PirateScrewPunchSkillId
            or 22121000
            or 22151001
            or 4341002
            or 4341003
            or MonkeyWaveSkillId
            or ThunderBreakerSharkWaveSkillId;

        public static bool ArmsAtFullStrengthOnCriticalHit(int skillId) => skillId == MonkeyWaveSkillId;

        public static bool IsDragonOverlaySkill(int skillId) => skillId is 22121000 or 22151001;

        private static bool HasAuthoredKeydownNode(int skillId)
        {
            if (skillId <= 0)
            {
                return false;
            }

            lock (AuthoredKeydownSkillCacheLock)
            {
                if (AuthoredKeydownSkillCache.TryGetValue(skillId, out bool cachedResult))
                {
                    return cachedResult;
                }
            }

            bool result = false;
            bool shouldCache = false;
            try
            {
                int jobId = skillId / 10000;
                if (jobId > 0)
                {
                    WzImage image = global::HaCreator.Program.FindImage("Skill", $"{jobId}.img");
                    if (image != null)
                    {
                        if (!image.Parsed)
                        {
                            image.ParseImage();
                        }

                        result = image["skill"]?[skillId.ToString()]?["keydown"] != null;
                        shouldCache = true;
                    }
                }
            }
            catch (Exception)
            {
                result = false;
                shouldCache = true;
            }

            if (shouldCache)
            {
                lock (AuthoredKeydownSkillCacheLock)
                {
                    AuthoredKeydownSkillCache[skillId] = result;
                }
            }

            return result;
        }

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
