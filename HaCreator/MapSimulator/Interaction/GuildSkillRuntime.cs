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

    internal readonly record struct GuildSkillUiContext(
        bool HasGuildMembership,
        string GuildName,
        int GuildLevel,
        string GuildRoleLabel,
        bool CanManageSkills);

    internal sealed class GuildSkillRuntime
    {
        private const int DefaultGuildFundMeso = 25000000;
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
        private readonly Dictionary<int, DateTimeOffset> _activeGuildSkillExpirations = new();
        private readonly Dictionary<int, DateTimeOffset> _savedActiveGuildSkillExpirations = new();
        private int _selectedIndex;
        private int _recommendedSkillId;
        private int _availablePoints = 2;
        private int _guildFundMeso = DefaultGuildFundMeso;
        private int _savedAvailablePoints = 2;
        private int _savedGuildFundMeso = DefaultGuildFundMeso;
        private int _savedGuildLevel = 12;
        private int _guildLevel = 12;
        private string _guildName = "Maple GM";
        private string _guildRoleLabel = "Master";
        private bool _isInGuild = true;
        private bool _hasManagementAuthority = true;
        private GuildSkillPermissionLevel _permissionLevel = GuildSkillPermissionLevel.Master;

        internal static bool HasGuildMembership(CharacterBuild build)
        {
            return HasGuildMembership(build?.GuildName);
        }

        internal static bool HasGuildMembership(string guildName)
        {
            string normalizedGuildName = guildName?.Trim();
            return !string.IsNullOrWhiteSpace(normalizedGuildName) && !PlaceholderGuildNames.Contains(normalizedGuildName);
        }

        internal void UpdateContext(GuildSkillUiContext context)
        {
            bool inGuild = context.HasGuildMembership;
            _guildName = inGuild ? (context.GuildName?.Trim() ?? string.Empty) : "No Guild";
            _guildLevel = inGuild ? ResolveGuildLevel(context.GuildLevel) : 0;
            _guildRoleLabel = inGuild
                ? NormalizeGuildRoleLabel(context.GuildRoleLabel)
                : "No Guild";
            _permissionLevel = inGuild
                ? ResolvePermissionLevel(_guildRoleLabel)
                : GuildSkillPermissionLevel.None;
            _hasManagementAuthority = inGuild &&
                                      context.CanManageSkills &&
                                      _permissionLevel >= GuildSkillPermissionLevel.JrMaster;

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

        internal void UpdateLocalContext(CharacterBuild build, string guildRoleLabel, bool canManageSkills)
        {
            bool hasGuildMembership = HasGuildMembership(build);
            UpdateContext(new GuildSkillUiContext(
                hasGuildMembership,
                build?.GuildName,
                hasGuildMembership ? ResolveLocalFallbackGuildLevel() : 0,
                guildRoleLabel,
                canManageSkills));
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
                _guildFundMeso = Math.Max(0, _savedGuildFundMeso);
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
            int selectedRemainingMinutes = GetRemainingDurationMinutes(selectedSkill);

            return new GuildSkillSnapshot
            {
                InGuild = _isInGuild,
                GuildName = _guildName,
                GuildLevel = _guildLevel,
                GuildRoleLabel = _guildRoleLabel,
                CanManageSkills = CanManageSkills(),
                AvailablePoints = _availablePoints,
                GuildFundMeso = _guildFundMeso,
                SelectedIndex = _selectedIndex,
                RecommendedSkillId = _recommendedSkillId,
                CanRenew = CanRenew(selectedSkill),
                CanLevelUpSelected = CanManageSkills() && CanLevelUp(selectedSkill),
                SummaryLines = BuildSummaryLines(selectedSkill, selectedRequiredGuildLevel, selectedRemainingMinutes),
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
                    RenewalCost = skill.CurrentLevel > 0 ? skill.GetGuildRenewalCost(skill.CurrentLevel) : 0,
                    DurationMinutes = skill.GetGuildDurationMinutes(Math.Max(1, skill.CurrentLevel)),
                    RemainingDurationMinutes = GetRemainingDurationMinutes(skill),
                    GuildPriceUnit = Math.Max(1, skill.GuildPriceUnit),
                    GuildFundMeso = _guildFundMeso,
                    IconTexture = skill.IconTexture,
                    DisabledIconTexture = skill.IconDisabledTexture,
                    IsRecommended = _isInGuild && skill.SkillId == _recommendedSkillId,
                    CanManageSkills = CanManageSkills(),
                    CanLevelUp = CanLevelUp(skill),
                    CanRenew = CanRenew(skill)
                }).ToArray()
            };
        }

        internal void SetLocalGuildFundMeso(int guildFundMeso)
        {
            _guildFundMeso = Math.Max(0, guildFundMeso);
            if (_isInGuild)
            {
                SaveCurrentGuildState();
            }
        }

        internal void SelectEntry(int index)
        {
            if (index < 0 || index >= _skills.Count)
            {
                return;
            }

            _selectedIndex = index;
        }

        internal string TryRenewSelectedSkill()
        {
            if (!_isInGuild)
            {
                return "Join a guild to renew guild skills.";
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

            if (selectedSkill.CurrentLevel <= 0)
            {
                return $"{selectedSkill.SkillName} must be learned before it can be renewed.";
            }

            int durationMinutes = selectedSkill.GetGuildDurationMinutes(selectedSkill.CurrentLevel);
            int renewalCost = selectedSkill.GetGuildRenewalCost(selectedSkill.CurrentLevel);
            if (durationMinutes <= 0 || renewalCost <= 0)
            {
                return $"{selectedSkill.SkillName} does not expose a renewable timed effect in the loaded data.";
            }

            if (!HasEnoughGuildFund(renewalCost))
            {
                return BuildInsufficientGuildFundMessage("renew", selectedSkill.SkillName, renewalCost);
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTimeOffset baseTime = now;
            if (_activeGuildSkillExpirations.TryGetValue(selectedSkill.SkillId, out DateTimeOffset expiration) &&
                expiration > now)
            {
                baseTime = expiration;
            }

            DateTimeOffset renewedExpiration = baseTime.AddMinutes(durationMinutes);
            _activeGuildSkillExpirations[selectedSkill.SkillId] = renewedExpiration;
            _guildFundMeso = Math.Max(0, _guildFundMeso - renewalCost);
            SaveCurrentGuildState();

            int remainingMinutes = Math.Max(1, (int)Math.Ceiling((renewedExpiration - now).TotalMinutes));
            return $"{selectedSkill.SkillName} renewed for {FormatDuration(durationMinutes)} (cost {FormatMeso(renewalCost)}). Remaining: {FormatDuration(remainingMinutes)}. Guild fund: {FormatMeso(_guildFundMeso)}.";
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

                int requiredCost = ResolveLevelUpCost(selectedSkill);
                if (requiredCost > 0 && !HasEnoughGuildFund(requiredCost))
                {
                    return BuildInsufficientGuildFundMessage("learn", selectedSkill.SkillName, requiredCost);
                }

                return $"{selectedSkill.SkillName} is already at its current cap.";
            }

            int levelUpCost = ResolveLevelUpCost(selectedSkill);
            selectedSkill.CurrentLevel++;
            if (selectedSkill.CurrentLevel == 1)
            {
                _activeGuildSkillExpirations.Remove(selectedSkill.SkillId);
            }
            _availablePoints = Math.Max(0, _availablePoints - 1);
            _guildFundMeso = Math.Max(0, _guildFundMeso - levelUpCost);
            SaveCurrentGuildState();
            EnsureRecommendation();
            return $"{selectedSkill.SkillName} advanced to Lv. {selectedSkill.CurrentLevel}. Guild fund: {FormatMeso(_guildFundMeso)}.";
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

            return _guildLevel >= GetRequiredGuildLevel(skill, skill.CurrentLevel + 1) &&
                   HasEnoughGuildFund(ResolveLevelUpCost(skill));
        }

        private bool CanRenew(SkillDisplayData skill)
        {
            if (!CanManageSkills() || skill == null || skill.CurrentLevel <= 0)
            {
                return false;
            }

            return skill.GetGuildDurationMinutes(skill.CurrentLevel) > 0 &&
                   skill.GetGuildRenewalCost(skill.CurrentLevel) > 0 &&
                   HasEnoughGuildFund(ResolveRenewalCost(skill));
        }

        private bool CanManageSkills()
        {
            return _isInGuild && _hasManagementAuthority;
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

        private string[] BuildSummaryLines(SkillDisplayData selectedSkill, int selectedRequiredGuildLevel, int selectedRemainingMinutes)
        {
            if (selectedSkill == null)
            {
                return new[]
                {
                    _isInGuild ? $"Guild: {_guildName}" : "Current: Join a guild to use guild skills.",
                    _isInGuild ? $"Role: {_guildRoleLabel}  |  Guild Lv. {_guildLevel}" : "Next: Guild skills unlock with real guild membership.",
                    _isInGuild ? $"SP: {_availablePoints}  |  Fund: {FormatCompactMeso(_guildFundMeso)}" : "State: No guild"
                };
            }

            return new[]
            {
                BuildCurrentEffectSummary(selectedSkill),
                BuildNextEffectSummary(selectedSkill, selectedRequiredGuildLevel),
                BuildActivationDetailSummary(selectedSkill, selectedRequiredGuildLevel, selectedRemainingMinutes)
            };
        }

        private static string GetCurrentEffectDescription(SkillDisplayData skill)
        {
            if (skill == null)
                return string.Empty;

            if (skill.CurrentLevel > 0)
                return skill.GetLevelDescription(skill.CurrentLevel);

            return string.Empty;
        }

        private static string GetNextEffectDescription(SkillDisplayData skill)
        {
            if (skill == null || skill.CurrentLevel >= skill.MaxLevel)
                return string.Empty;

            int nextLevel = skill.CurrentLevel <= 0
                ? 1
                : skill.CurrentLevel + 1;
            if (nextLevel > skill.MaxLevel)
                return string.Empty;

            return skill.GetLevelDescription(nextLevel);
        }

        private string BuildCurrentEffectSummary(SkillDisplayData selectedSkill)
        {
            if (selectedSkill == null)
                return string.Empty;

            string effectText = GetCurrentEffectDescription(selectedSkill);
            if (!string.IsNullOrWhiteSpace(effectText))
                return $"Current: {effectText}";

            if (selectedSkill.CurrentLevel > 0)
                return $"Current: Lv. {selectedSkill.CurrentLevel}/{selectedSkill.MaxLevel}";

            return _isInGuild
                ? "Current: Not learned."
                : "Current: Join a guild.";
        }

        private string BuildNextEffectSummary(SkillDisplayData selectedSkill, int selectedRequiredGuildLevel)
        {
            if (selectedSkill == null)
                return string.Empty;

            if (selectedSkill.CurrentLevel >= selectedSkill.MaxLevel)
                return "Next: Max level reached.";

            string nextEffect = GetNextEffectDescription(selectedSkill);
            if (!string.IsNullOrWhiteSpace(nextEffect))
                return $"Next: {nextEffect}";

            if (!_isInGuild)
                return "Next: Requires guild membership.";

            if (selectedRequiredGuildLevel > _guildLevel)
                return $"Next: Requires Guild Lv. {selectedRequiredGuildLevel}.";

            return $"Next: Lv. {Math.Min(selectedSkill.MaxLevel, selectedSkill.CurrentLevel + 1)}/{selectedSkill.MaxLevel}";
        }

        private string BuildActivationDetailSummary(SkillDisplayData selectedSkill, int selectedRequiredGuildLevel, int remainingMinutes)
        {
            if (selectedSkill == null)
                return string.Empty;

            List<string> parts = new();

            if (selectedRequiredGuildLevel > 0 && selectedSkill.CurrentLevel < selectedSkill.MaxLevel)
            {
                parts.Add($"Req Lv. {selectedRequiredGuildLevel}");
            }

            int previewLevel = Math.Max(1, Math.Min(selectedSkill.MaxLevel, selectedSkill.CurrentLevel + 1));
            int activationCost = ResolveLevelUpCost(selectedSkill);
            if (activationCost > 0)
            {
                parts.Add($"Learn {FormatCompactMeso(activationCost)}");
            }

            int renewalReferenceLevel = Math.Max(1, selectedSkill.CurrentLevel);
            int renewalCost = ResolveRenewalCost(selectedSkill);
            if (selectedSkill.CurrentLevel > 0 && renewalCost > 0)
            {
                parts.Add($"Renew {FormatCompactMeso(renewalCost)}");
            }

            int durationMinutes = selectedSkill.GetGuildDurationMinutes(renewalReferenceLevel);
            if (durationMinutes > 0)
            {
                parts.Add($"Duration {FormatDuration(durationMinutes)}");
            }

            if (remainingMinutes > 0)
            {
                parts.Add($"Remain {FormatDuration(remainingMinutes)}");
            }
            else if (selectedSkill.CurrentLevel > 0 && durationMinutes > 0)
            {
                parts.Add(CanManageSkills() ? "Inactive" : "View only");
            }

            parts.Add($"Fund {FormatCompactMeso(_guildFundMeso)}");

            if (_isInGuild)
            {
                parts.Add(CanManageSkills() ? $"SP {_availablePoints}" : "View only");
            }
            else
            {
                parts.Add("No guild");
            }

            return parts.Count > 0
                ? string.Join("  |  ", parts)
                : (_isInGuild ? $"SP {_availablePoints}" : "No guild");
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

        private int GetRemainingDurationMinutes(SkillDisplayData skill)
        {
            if (skill == null || skill.CurrentLevel <= 0)
                return 0;

            if (!_activeGuildSkillExpirations.TryGetValue(skill.SkillId, out DateTimeOffset expiration))
                return 0;

            TimeSpan remaining = expiration - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                _activeGuildSkillExpirations.Remove(skill.SkillId);
                return 0;
            }

            return Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
        }

        private static string FormatMeso(int amount)
        {
            return $"{Math.Max(0, amount).ToString("N0", CultureInfo.InvariantCulture)} meso";
        }

        private static string FormatCompactMeso(int amount)
        {
            return Math.Max(0, amount).ToString("N0", CultureInfo.InvariantCulture);
        }

        private bool HasEnoughGuildFund(int cost)
        {
            return cost <= 0 || _guildFundMeso >= cost;
        }

        private string BuildInsufficientGuildFundMessage(string actionLabel, string skillName, int requiredCost)
        {
            int missingAmount = Math.Max(0, requiredCost - _guildFundMeso);
            return $"{skillName} cannot {actionLabel} because the guild fund is short by {FormatMeso(missingAmount)}.";
        }

        private static int ResolveLevelUpCost(SkillDisplayData skill)
        {
            if (skill == null || skill.CurrentLevel >= skill.MaxLevel)
            {
                return 0;
            }

            int nextLevel = Math.Max(1, Math.Min(skill.MaxLevel, skill.CurrentLevel + 1));
            return skill.GetGuildActivationCost(nextLevel);
        }

        private static int ResolveRenewalCost(SkillDisplayData skill)
        {
            if (skill == null || skill.CurrentLevel <= 0)
            {
                return 0;
            }

            return skill.GetGuildRenewalCost(skill.CurrentLevel);
        }

        private void ApplyNoGuildState()
        {
            _availablePoints = 0;
            _guildFundMeso = 0;
            _activeGuildSkillExpirations.Clear();
            foreach (SkillDisplayData skill in _skills)
            {
                skill.CurrentLevel = 0;
            }
        }

        private void RestoreSavedGuildState()
        {
            _guildLevel = Math.Max(1, _savedGuildLevel);
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
            _guildFundMeso = Math.Max(0, _savedGuildFundMeso);
            _activeGuildSkillExpirations.Clear();
            foreach (KeyValuePair<int, DateTimeOffset> entry in _savedActiveGuildSkillExpirations)
            {
                if (entry.Value > DateTimeOffset.UtcNow)
                {
                    _activeGuildSkillExpirations[entry.Key] = entry.Value;
                }
            }
        }

        private void SaveCurrentGuildState()
        {
            _savedGuildLevel = Math.Max(1, _guildLevel);
            _savedSkillLevels.Clear();
            foreach (SkillDisplayData skill in _skills)
            {
                _savedSkillLevels[skill.SkillId] = Math.Clamp(skill.CurrentLevel, 0, Math.Max(0, skill.MaxLevel));
            }

            _savedAvailablePoints = Math.Max(0, _availablePoints);
            _savedGuildFundMeso = Math.Max(0, _guildFundMeso);
            _savedActiveGuildSkillExpirations.Clear();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (KeyValuePair<int, DateTimeOffset> entry in _activeGuildSkillExpirations)
            {
                if (entry.Value > now)
                {
                    _savedActiveGuildSkillExpirations[entry.Key] = entry.Value;
                }
            }
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

        private int ResolveGuildLevel(int requestedGuildLevel)
        {
            if (requestedGuildLevel > 0)
            {
                return Math.Max(1, requestedGuildLevel);
            }

            return ResolveLocalFallbackGuildLevel();
        }

        private int ResolveLocalFallbackGuildLevel()
        {
            return Math.Max(1, Math.Max(_guildLevel, _savedGuildLevel));
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
        public int GuildFundMeso { get; init; }
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
        public int RemainingDurationMinutes { get; init; }
        public int GuildPriceUnit { get; init; } = 1;
        public int GuildFundMeso { get; init; }
        public Microsoft.Xna.Framework.Graphics.Texture2D IconTexture { get; init; }
        public Microsoft.Xna.Framework.Graphics.Texture2D DisabledIconTexture { get; init; }
        public bool IsRecommended { get; init; }
        public bool CanManageSkills { get; init; }
        public bool CanLevelUp { get; init; }
        public bool CanRenew { get; init; }
    }
}
