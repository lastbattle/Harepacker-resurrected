using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class GuildSkillRuntime
    {
        private readonly List<SkillDisplayData> _skills = new();
        private int _selectedIndex;
        private int _recommendedSkillId;
        private int _availablePoints = 2;
        private int _guildLevel = 12;
        private string _guildName = "Maple GM";

        internal void UpdateLocalContext(CharacterBuild build)
        {
            bool inGuild = !string.IsNullOrWhiteSpace(build?.GuildDisplayText) &&
                           !string.Equals(build.GuildDisplayText, "-", StringComparison.Ordinal);
            _guildName = inGuild ? build.GuildDisplayText.Trim() : "No Guild";
            _guildLevel = inGuild ? Math.Clamp((build?.Level ?? 1) / 10 + 5, 1, 30) : 0;

            if (!inGuild)
            {
                _availablePoints = 0;
                foreach (SkillDisplayData skill in _skills)
                {
                    skill.CurrentLevel = 0;
                }
            }

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
            }
            else
            {
                _selectedIndex = -1;
            }

            EnsureRecommendation();
        }

        internal GuildSkillSnapshot BuildSnapshot()
        {
            SkillDisplayData selectedSkill = GetSelectedSkill();
            int selectedRequiredGuildLevel = GetRequiredGuildLevel(selectedSkill, selectedSkill?.CurrentLevel + 1 ?? 1);

            return new GuildSkillSnapshot
            {
                GuildName = _guildName,
                GuildLevel = _guildLevel,
                AvailablePoints = _availablePoints,
                SelectedIndex = _selectedIndex,
                RecommendedSkillId = _recommendedSkillId,
                CanRenew = _skills.Count > 1,
                CanLevelUpSelected = CanLevelUp(selectedSkill),
                SummaryLines = BuildSummaryLines(selectedSkill, selectedRequiredGuildLevel),
                Entries = _skills.Select(skill => new GuildSkillEntrySnapshot
                {
                    SkillId = skill.SkillId,
                    SkillName = skill.SkillName,
                    Description = skill.Description,
                    CurrentLevel = skill.CurrentLevel,
                    MaxLevel = skill.MaxLevel,
                    RequiredGuildLevel = GetRequiredGuildLevel(skill, skill.CurrentLevel + 1),
                    IconTexture = skill.IconTexture,
                    DisabledIconTexture = skill.IconDisabledTexture,
                    IsRecommended = skill.SkillId == _recommendedSkillId,
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
            EnsureRecommendation();
            return $"{selectedSkill.SkillName} advanced to Lv. {selectedSkill.CurrentLevel}.";
        }

        private void SeedInitialLevels()
        {
            for (int i = 0; i < _skills.Count; i++)
            {
                SkillDisplayData skill = _skills[i];
                skill.CurrentLevel = i switch
                {
                    0 => Math.Min(2, skill.MaxLevel),
                    1 => Math.Min(1, skill.MaxLevel),
                    _ => 0
                };
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
            if (skill == null || _availablePoints <= 0 || skill.CurrentLevel >= skill.MaxLevel)
            {
                return false;
            }

            return _guildLevel >= GetRequiredGuildLevel(skill, skill.CurrentLevel + 1);
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
            if (_skills.Count == 0)
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
            if (selectedSkill == null)
            {
                return new[]
                {
                    _guildName,
                    $"Guild Lv. {_guildLevel}",
                    $"SP: {_availablePoints}"
                };
            }

            return new[]
            {
                _guildName,
                $"Guild Lv. {_guildLevel}  |  SP: {_availablePoints}",
                selectedRequiredGuildLevel > _guildLevel && selectedSkill.CurrentLevel < selectedSkill.MaxLevel
                    ? $"Next rank requires Guild Lv. {selectedRequiredGuildLevel}."
                    : selectedSkill.Description
            };
        }
    }

    internal sealed class GuildSkillSnapshot
    {
        public string GuildName { get; init; } = string.Empty;
        public int GuildLevel { get; init; }
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
        public Microsoft.Xna.Framework.Graphics.Texture2D IconTexture { get; init; }
        public Microsoft.Xna.Framework.Graphics.Texture2D DisabledIconTexture { get; init; }
        public bool IsRecommended { get; init; }
        public bool CanLevelUp { get; init; }
    }
}
