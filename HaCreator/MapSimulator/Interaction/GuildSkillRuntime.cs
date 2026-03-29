using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum GuildSkillPermissionLevel
    {
        None,
        Member,
        JrMaster,
        Master
    }

    internal sealed class GuildSkillRuntime
    {
        private static readonly HashSet<string> PlaceholderGuildNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "-",
            "No Guild",
            "Maple Guild",
            "Maple GM",
            "Lith Harbor",
            "Sleepywood"
        };

        private readonly List<SkillDisplayData> _skills = new();
        private readonly Dictionary<int, int> _savedSkillLevels = new();
        private int _selectedIndex;
        private int _recommendedSkillId;
        private int _availablePoints = 2;
        private int _savedAvailablePoints = 2;
        private int _guildLevel = 12;
        private string _guildName = "Maple GM";
        private string _guildRoleLabel = "Master";
        private bool _isInGuild = true;
        private GuildSkillPermissionLevel _permissionLevel = GuildSkillPermissionLevel.Master;

        internal static bool HasGuildMembership(CharacterBuild build)
        {
            string guildName = build?.GuildName?.Trim();
            return !string.IsNullOrWhiteSpace(guildName) && !PlaceholderGuildNames.Contains(guildName);
        }

        internal void UpdateLocalContext(CharacterBuild build, string guildRoleLabel)
        {
            bool inGuild = HasGuildMembership(build);
            _guildName = inGuild ? build.GuildName.Trim() : "No Guild";
            _guildLevel = inGuild ? Math.Clamp((build?.Level ?? 1) / 10 + 5, 1, 30) : 0;
            _guildRoleLabel = inGuild
                ? NormalizeGuildRoleLabel(guildRoleLabel)
                : "No Guild";
            _permissionLevel = inGuild
                ? ResolvePermissionLevel(_guildRoleLabel)
                : GuildSkillPermissionLevel.None;

            if (!inGuild)
            {
                if (_isInGuild)
                {
                    SaveCurrentGuildState();
                }

                ApplyNoGuildState();
            }
            else if (!_isInGuild)
            {
                RestoreSavedGuildState();
            }

            _isInGuild = inGuild;
            EnsureRecommendation();
        }

        internal void SetSkills(IEnumerable<SkillDisplayData> skills)
        {
            _skills.Clear();
            if (skills != null)
            {
                _skills.AddRange(skills
                    .Where(skill => skill != null)
                    .OrderBy(skill => skill.SkillId));
            }

            if (_skills.Count > 0)
            {
                _selectedIndex = Math.Clamp(_selectedIndex, 0, _skills.Count - 1);
                SeedInitialLevels();
                _availablePoints = 2;
                SaveCurrentGuildState();
                if (!_isInGuild)
                {
                    ApplyNoGuildState();
                }
            }
            else
            {
                _selectedIndex = -1;
                _savedSkillLevels.Clear();
                _savedAvailablePoints = 0;
            }

            EnsureRecommendation();
        }

        internal GuildSkillSnapshot BuildSnapshot()
        {
            SkillDisplayData selectedSkill = GetSelectedSkill();
            int selectedRequiredGuildLevel = GetRequiredGuildLevel(selectedSkill, selectedSkill?.CurrentLevel + 1 ?? 1);

            return new GuildSkillSnapshot
            {
                InGuild = _isInGuild,
                GuildName = _guildName,
                GuildLevel = _guildLevel,
                GuildRoleLabel = _guildRoleLabel,
                CanManageSkills = CanManageSkills(),
                AvailablePoints = _availablePoints,
                SelectedIndex = _selectedIndex,
                RecommendedSkillId = _recommendedSkillId,
                CanRenew = CanManageSkills() && _skills.Count > 1,
                CanLevelUpSelected = CanManageSkills() && CanLevelUp(selectedSkill),
                SummaryLines = BuildSummaryLines(selectedSkill, selectedRequiredGuildLevel),
                Entries = _skills.Select(skill => new GuildSkillEntrySnapshot
                {
                    SkillId = skill.SkillId,
                    SkillName = skill.SkillName,
                    Description = skill.Description,
                    CurrentLevel = skill.CurrentLevel,
                    MaxLevel = skill.MaxLevel,
                    RequiredGuildLevel = GetRequiredGuildLevel(skill, skill.CurrentLevel + 1),
                    CurrentEffectDescription = GetCurrentEffectDescription(skill),
                    NextEffectDescription = GetNextEffectDescription(skill),
                    ActivationCost = skill.GetGuildActivationCost(Math.Max(1, skill.CurrentLevel + 1)),
                    RenewalCost = skill.GetGuildRenewalCost(Math.Max(1, skill.CurrentLevel)),
                    DurationMinutes = skill.GetGuildDurationMinutes(Math.Max(1, skill.CurrentLevel)),
                    IconTexture = skill.IconTexture,
                    DisabledIconTexture = skill.IconDisabledTexture,
                    IsRecommended = _isInGuild && skill.SkillId == _recommendedSkillId,
                    CanLevelUp = CanLevelUp(skill)
                }).ToArray()
            };
        }

        internal void SelectEntry(int index)
        {
            if (index < 0 || index >= _skills.Count)
            {
                return;
            }

            _selectedIndex = index;
        }

        internal string RefreshRecommendation()
        {
            if (!_isInGuild)
            {
                _recommendedSkillId = 0;
                return "Join a guild to cycle guild skill recommendations.";
            }

            if (!CanManageSkills())
            {
                return "Guild skill management requires Jr. Master or Master authority.";
            }

            if (_skills.Count == 0)
            {
                _recommendedSkillId = 0;
                return "No guild skills are available in the loaded data set.";
            }

            List<SkillDisplayData> levelable = _skills.Where(CanLevelUp).ToList();
            if (levelable.Count == 0)
            {
                _recommendedSkillId = _skills[0].SkillId;
                return "No guild skill can level at the current guild state.";
            }

            int currentIndex = levelable.FindIndex(skill => skill.SkillId == _recommendedSkillId);
            int nextIndex = currentIndex >= 0 ? (currentIndex + 1) % levelable.Count : 0;
            _recommendedSkillId = levelable[nextIndex].SkillId;
            return $"Recommendation shifted to {levelable[nextIndex].SkillName}.";
        }

        internal string TryLevelSelectedSkill()
        {
            if (!_isInGuild)
            {
                return "Join a guild to level guild skills.";
            }

            if (!CanManageSkills())
            {
                return "Guild skill management requires Jr. Master or Master authority.";
            }

            SkillDisplayData selectedSkill = GetSelectedSkill();
            if (selectedSkill == null)
            {
                return "Select a guild skill first.";
            }

            if (!CanLevelUp(selectedSkill))
            {
                if (_availablePoints <= 0)
                {
                    return "No guild skill points remain for this session.";
                }

                int requiredGuildLevel = GetRequiredGuildLevel(selectedSkill, selectedSkill.CurrentLevel + 1);
                if (_guildLevel < requiredGuildLevel)
                {
                    return $"Guild Lv. {requiredGuildLevel} is required for {selectedSkill.SkillName}.";
                }

                return $"{selectedSkill.SkillName} is already at its current cap.";
            }

            selectedSkill.CurrentLevel++;
            _availablePoints = Math.Max(0, _availablePoints - 1);
            SaveCurrentGuildState();
            EnsureRecommendation();
            return $"{selectedSkill.SkillName} advanced to Lv. {selectedSkill.CurrentLevel}.";
        }

        private void SeedInitialLevels()
        {
            for (int i = 0; i < _skills.Count; i++)
            {
                SkillDisplayData skill = _skills[i];
                skill.CurrentLevel = GetSeededLevel(i, skill);
            }
        }

        private SkillDisplayData GetSelectedSkill()
        {
            return _selectedIndex >= 0 && _selectedIndex < _skills.Count
                ? _skills[_selectedIndex]
                : null;
        }

        private bool CanLevelUp(SkillDisplayData skill)
        {
            if (!_isInGuild || skill == null || _availablePoints <= 0 || skill.CurrentLevel >= skill.MaxLevel)
            {
                return false;
            }

            return _guildLevel >= GetRequiredGuildLevel(skill, skill.CurrentLevel + 1);
        }

        private bool CanManageSkills()
        {
            return _isInGuild && _permissionLevel >= GuildSkillPermissionLevel.JrMaster;
        }

        private static int GetRequiredGuildLevel(SkillDisplayData skill, int nextLevel)
        {
            if (skill == null)
            {
                return 0;
            }

            int resolvedLevel = Math.Clamp(nextLevel, 1, Math.Max(1, skill.MaxLevel));
            return skill.RequiredGuildLevels.TryGetValue(resolvedLevel, out int requiredGuildLevel)
                ? Math.Max(0, requiredGuildLevel)
                : 0;
        }

        private void EnsureRecommendation()
        {
            if (!_isInGuild || _skills.Count == 0)
            {
                _recommendedSkillId = 0;
                return;
            }

            if (_skills.Any(skill => skill.SkillId == _recommendedSkillId && CanLevelUp(skill)))
            {
                return;
            }

            SkillDisplayData nextRecommended = _skills.FirstOrDefault(CanLevelUp) ?? _skills[0];
            _recommendedSkillId = nextRecommended.SkillId;
        }

        private string[] BuildSummaryLines(SkillDisplayData selectedSkill, int selectedRequiredGuildLevel)
        {
            if (!_isInGuild)
            {
                return new[]
                {
                    _guildName,
                    "Guild Lv. 0  |  SP: 0",
                    "Join a guild to use guild skills."
                };
            }

            if (!CanManageSkills())
            {
                return new[]
                {
                    _guildName,
                    $"Guild Lv. {_guildLevel}  |  {_guildRoleLabel}",
                    "Guild skill management requires Jr. Master or Master."
                };
            }

            if (selectedSkill == null)
            {
                return new[]
                {
                    _guildName,
                    $"Guild Lv. {_guildLevel}  |  {_guildRoleLabel}",
                    $"SP: {_availablePoints}"
                };
            }

            return new[]
            {
                _guildName,
                $"Guild Lv. {_guildLevel}  |  SP: {_availablePoints}",
                BuildSelectedSkillSummary(selectedSkill, selectedRequiredGuildLevel)
            };
        }

        private static string GetCurrentEffectDescription(SkillDisplayData skill)
        {
            if (skill == null)
                return string.Empty;

            if (skill.CurrentLevel > 0)
                return skill.GetLevelDescription(skill.CurrentLevel);

            return skill.GetLevelDescription(1);
        }

        private static string GetNextEffectDescription(SkillDisplayData skill)
        {
            if (skill == null || skill.CurrentLevel >= skill.MaxLevel)
                return string.Empty;

            return skill.GetLevelDescription(skill.CurrentLevel + 1);
        }

        private string BuildSelectedSkillSummary(SkillDisplayData selectedSkill, int selectedRequiredGuildLevel)
        {
            if (selectedSkill == null)
                return $"SP: {_availablePoints}";

            if (selectedRequiredGuildLevel > _guildLevel && selectedSkill.CurrentLevel < selectedSkill.MaxLevel)
                return $"Next rank requires Guild Lv. {selectedRequiredGuildLevel}.";

            string effectText = GetCurrentEffectDescription(selectedSkill);
            if (!string.IsNullOrWhiteSpace(effectText))
                return effectText;

            int previewLevel = Math.Max(1, selectedSkill.CurrentLevel + 1);
            int activationCost = selectedSkill.GetGuildActivationCost(previewLevel);
            int renewalCost = selectedSkill.GetGuildRenewalCost(Math.Max(1, selectedSkill.CurrentLevel));
            int durationMinutes = selectedSkill.GetGuildDurationMinutes(Math.Max(1, selectedSkill.CurrentLevel));

            List<string> parts = new();
            if (durationMinutes > 0)
                parts.Add(FormatDuration(durationMinutes));
            if (activationCost > 0)
                parts.Add($"Learn {activationCost.ToString("N0", CultureInfo.InvariantCulture)}");
            if (renewalCost > 0)
                parts.Add($"Renew {renewalCost.ToString("N0", CultureInfo.InvariantCulture)}");

            return parts.Count > 0
                ? string.Join("  |  ", parts)
                : selectedSkill.Description;
        }

        private static string FormatDuration(int durationMinutes)
        {
            if (durationMinutes <= 0)
                return string.Empty;

            if (durationMinutes % (60 * 24) == 0)
                return $"{durationMinutes / (60 * 24)}d";

            if (durationMinutes % 60 == 0)
                return $"{durationMinutes / 60}h";

            return $"{durationMinutes}m";
        }

        private void ApplyNoGuildState()
        {
            _availablePoints = 0;
            foreach (SkillDisplayData skill in _skills)
            {
                skill.CurrentLevel = 0;
            }
        }

        private void RestoreSavedGuildState()
        {
            for (int i = 0; i < _skills.Count; i++)
            {
                SkillDisplayData skill = _skills[i];
                if (!_savedSkillLevels.TryGetValue(skill.SkillId, out int savedLevel))
                {
                    savedLevel = GetSeededLevel(i, skill);
                }

                skill.CurrentLevel = Math.Clamp(savedLevel, 0, Math.Max(0, skill.MaxLevel));
            }

            _availablePoints = Math.Max(0, _savedAvailablePoints);
        }

        private void SaveCurrentGuildState()
        {
            _savedSkillLevels.Clear();
            foreach (SkillDisplayData skill in _skills)
            {
                _savedSkillLevels[skill.SkillId] = Math.Clamp(skill.CurrentLevel, 0, Math.Max(0, skill.MaxLevel));
            }

            _savedAvailablePoints = Math.Max(0, _availablePoints);
        }

        private static int GetSeededLevel(int index, SkillDisplayData skill)
        {
            return index switch
            {
                0 => Math.Min(2, skill.MaxLevel),
                1 => Math.Min(1, skill.MaxLevel),
                _ => 0
            };
        }

        private static string NormalizeGuildRoleLabel(string guildRoleLabel)
        {
            return string.IsNullOrWhiteSpace(guildRoleLabel)
                ? "Member"
                : guildRoleLabel.Trim();
        }

        private static GuildSkillPermissionLevel ResolvePermissionLevel(string guildRoleLabel)
        {
            if (string.IsNullOrWhiteSpace(guildRoleLabel))
            {
                return GuildSkillPermissionLevel.Member;
            }

            string normalized = guildRoleLabel.Trim();
            if (normalized.Equals("Master", StringComparison.OrdinalIgnoreCase))
            {
                return GuildSkillPermissionLevel.Master;
            }

            if (normalized.Equals("Jr. Master", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Jr Master", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Junior Master", StringComparison.OrdinalIgnoreCase))
            {
                return GuildSkillPermissionLevel.JrMaster;
            }

            return GuildSkillPermissionLevel.Member;
        }
    }

    internal sealed class GuildSkillSnapshot
    {
        public bool InGuild { get; init; }
        public string GuildName { get; init; } = string.Empty;
        public int GuildLevel { get; init; }
        public string GuildRoleLabel { get; init; } = string.Empty;
        public bool CanManageSkills { get; init; }
        public int AvailablePoints { get; init; }
        public int SelectedIndex { get; init; } = -1;
        public int RecommendedSkillId { get; init; }
        public bool CanRenew { get; init; }
        public bool CanLevelUpSelected { get; init; }
        public IReadOnlyList<string> SummaryLines { get; init; } = Array.Empty<string>();
        public IReadOnlyList<GuildSkillEntrySnapshot> Entries { get; init; } = Array.Empty<GuildSkillEntrySnapshot>();
    }

    internal sealed class GuildSkillEntrySnapshot
    {
        public int SkillId { get; init; }
        public string SkillName { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public int CurrentLevel { get; init; }
        public int MaxLevel { get; init; }
        public int RequiredGuildLevel { get; init; }
        public string CurrentEffectDescription { get; init; } = string.Empty;
        public string NextEffectDescription { get; init; } = string.Empty;
        public int ActivationCost { get; init; }
        public int RenewalCost { get; init; }
        public int DurationMinutes { get; init; }
        public Microsoft.Xna.Framework.Graphics.Texture2D IconTexture { get; init; }
        public Microsoft.Xna.Framework.Graphics.Texture2D DisabledIconTexture { get; init; }
        public bool IsRecommended { get; init; }
        public bool CanLevelUp { get; init; }
    }
}
