using HaCreator.MapSimulator.UI;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class PreparedSkillHudRules
    {
        private static readonly HashSet<int> ReleaseTriggeredSkillIds = new()
        {
            2121001,
            2221001,
            2321001,
            22121000,
            22151001,
            4341002,
            4341003,
            5311002,
            WildHunterSwallowSkillId
        };

        private const int SG88SkillId = 35121003;
        private const int WildHunterSwallowSkillId = 33101005;

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
                5311002 => new PreparedSkillHudProfile(true, "KeyDownBar", 1080),
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

            if (skillId == 4341002
                || skillId == 4341003
                || skillId == 5311002
                || skillId == 2121001
                || skillId == 2221001
                || skillId == 2321001
                || skillId == WildHunterSwallowSkillId)
            {
                return PreparedSkillHudTextVariant.ReleaseArmed;
            }

            return PreparedSkillHudTextVariant.Default;
        }

        public static bool UsesReleaseTriggeredExecution(int skillId) => ReleaseTriggeredSkillIds.Contains(skillId);

        public static bool IsDragonOverlaySkill(int skillId) => skillId is 22121000 or 22151001;

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
