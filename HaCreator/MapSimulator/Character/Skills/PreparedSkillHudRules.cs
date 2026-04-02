using HaCreator.MapSimulator.UI;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class PreparedSkillHudRules
    {
        private const int MonkeyWaveSkillId = 5311002;
        private const int WildHunterSwallowSkillId = 33101005;
        private const int WildHunterSwallowAttackSkillId = 33101007;
        private static readonly HashSet<int> ReleaseTriggeredSkillIds = new()
        {
            2121001,
            2221001,
            2321001,
            22121000,
            22151001,
            4341002,
            4341003,
            MonkeyWaveSkillId,
            WildHunterSwallowSkillId
        };
        private static readonly HashSet<int> ReleaseArmedTextSkillIds = new()
        {
            2121001,
            2221001,
            2321001,
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
                4341003 => new PreparedSkillHudProfile(true, "KeyDownBar", 1200),
                // `Skill/531.img/skill/5311002/prepare/time` owns the authored
                // Monkey Wave charge window, so the runtime should fall back to loaded WZ time.
                MonkeyWaveSkillId => new PreparedSkillHudProfile(true, "KeyDownBar", 0),
                5201002 => new PreparedSkillHudProfile(true, "KeyDownBar", 1000),
                13111002 => new PreparedSkillHudProfile(true, "KeyDownBar", 1000),
                22121000 => new PreparedSkillHudProfile(true, "KeyDownBar3", 500, PreparedSkillHudSurface.World, showText: false),
                22151001 => new PreparedSkillHudProfile(true, "KeyDownBar2", 500, PreparedSkillHudSurface.World, showText: false),
                WildHunterSwallowSkillId => new PreparedSkillHudProfile(true, "KeyDownBar", 900),
                33121009 => new PreparedSkillHudProfile(true, "KeyDownBar", 2000),
                35001001 => new PreparedSkillHudProfile(true, "KeyDownBar", 2000),
                35101009 => new PreparedSkillHudProfile(true, "KeyDownBar", 2000),
                _ => new PreparedSkillHudProfile(true, "KeyDownBar", 0)
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

        public static bool UsesRemoteReleaseFollowUpPayload(int skillId)
        {
            return UsesReleaseTriggeredExecution(skillId)
                || RemoteReleaseFollowUpPayloadSkillIds.Contains(skillId);
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

        public static bool UsesChargeDamageScaling(int skillId) => skillId is 22121000 or 22151001 or MonkeyWaveSkillId;

        public static bool ArmsAtFullStrengthOnCriticalHit(int skillId) => skillId == MonkeyWaveSkillId;

        public static bool IsDragonOverlaySkill(int skillId) => skillId is 22121000 or 22151001;

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
