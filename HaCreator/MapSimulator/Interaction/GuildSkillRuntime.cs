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
        bool CanManageSkills,
        int? GuildPoints);

    internal sealed class GuildSkillRuntime
    {
        internal enum GuildSkillPendingRequestKind
        {
            LevelUp,
            Renew
        }

        private const int DefaultAvailablePoints = 2;
        private const int DefaultGuildFundMeso = 25000000;
        private const int DefaultLocalGuildLevel = 12;
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
        private readonly Dictionary<int, DateTimeOffset> _activeGuildSkillExpirations = new();
        private readonly Dictionary<string, GuildSkillSavedState> _savedGuildStates = new(StringComparer.OrdinalIgnoreCase);
        private int _selectedIndex;
        private int _recommendedSkillId;
        private int _availablePoints = DefaultAvailablePoints;
        private int _guildFundMeso = DefaultGuildFundMeso;
        private int _guildLevel = DefaultLocalGuildLevel;
        private int _guildPoints;
        private string _guildName = "Maple GM";
        private string _guildRoleLabel = "Master";
        private bool _isInGuild = true;
        private bool _hasManagementAuthority = true;
        private GuildSkillPermissionLevel _permissionLevel = GuildSkillPermissionLevel.Master;
        private string _activeGuildStateKey = "Maple GM";
        private GuildSkillPendingRequest _pendingRequest;

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
            string newGuildStateKey = inGuild ? NormalizeGuildStateKey(context.GuildName) : string.Empty;
            bool changedGuildIdentity = !string.Equals(_activeGuildStateKey, newGuildStateKey, StringComparison.OrdinalIgnoreCase);
            if ((!inGuild || changedGuildIdentity) && _pendingRequest != null)
            {
                _pendingRequest = null;
            }

            if (changedGuildIdentity && !string.IsNullOrWhiteSpace(_activeGuildStateKey))
            {
                SaveCurrentGuildState(_activeGuildStateKey);
            }

            GuildSkillSavedState savedState = inGuild
                ? GetSavedGuildState(newGuildStateKey)
                : null;
            _guildName = inGuild ? (context.GuildName?.Trim() ?? string.Empty) : "No Guild";
            _guildLevel = inGuild ? ResolveGuildLevel(context.GuildLevel, savedState?.GuildLevel ?? 0) : 0;
            _guildPoints = inGuild ? ResolveGuildPoints(context.GuildPoints, savedState?.GuildPoints ?? 0) : 0;
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
                ApplyNoGuildState();
            }
            else if (!_isInGuild || changedGuildIdentity)
            {
                RestoreGuildState(savedState);
                SaveCurrentGuildState(newGuildStateKey);
            }

            _activeGuildStateKey = newGuildStateKey;
            _isInGuild = inGuild;
            if (inGuild)
            {
                // Persist packet-owned guild level/point echoes so later local-context refreshes
                // do not fall back to the simulator seed between owner updates.
                SaveCurrentGuildState(newGuildStateKey);
            }

            EnsureRecommendation();
        }

        internal void UpdateLocalContext(CharacterBuild build, string guildRoleLabel, bool canManageSkills)
        {
            bool hasGuildMembership = HasGuildMembership(build);
            UpdateContext(new GuildSkillUiContext(
                hasGuildMembership,
                build?.GuildName,
                hasGuildMembership ? ResolveLocalFallbackGuildLevel(GetSavedGuildState(NormalizeGuildStateKey(build?.GuildName))?.GuildLevel ?? 0) : 0,
                guildRoleLabel,
                canManageSkills,
                null));
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
                if (_isInGuild)
                {
                    RestoreGuildState(GetSavedGuildState(_activeGuildStateKey));
                    SaveCurrentGuildState(_activeGuildStateKey);
                }
                else
                {
                    ApplyNoGuildState();
                }
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
            int selectedRemainingMinutes = GetRemainingDurationMinutes(selectedSkill);
            bool hasPendingRequest = _pendingRequest != null;
            string pendingActionLabel = hasPendingRequest
                ? _pendingRequest.ActionLabel
                : string.Empty;

            return new GuildSkillSnapshot
            {
                InGuild = _isInGuild,
                GuildName = _guildName,
                GuildLevel = _guildLevel,
                GuildRoleLabel = _guildRoleLabel,
                CanManageSkills = CanManageSkills(),
                AvailablePoints = _availablePoints,
                GuildFundMeso = _guildFundMeso,
                GuildPoints = _guildPoints,
                SelectedIndex = _selectedIndex,
                RecommendedSkillId = _recommendedSkillId,
                PendingActionLabel = pendingActionLabel,
                HasPendingRequest = hasPendingRequest,
                CanRenew = CanRenew(selectedSkill),
                CanLevelUpSelected = CanManageSkills() && CanLevelUp(selectedSkill),
                SummaryLines = BuildSummaryLines(selectedSkill, selectedRequiredGuildLevel, selectedRemainingMinutes, pendingActionLabel),
                Entries = _skills.Select(skill => new GuildSkillEntrySnapshot
                {
                    InGuild = _isInGuild,
                    GuildRoleLabel = _guildRoleLabel,
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
                    GuildPoints = _guildPoints,
                    IconTexture = skill.IconTexture,
                    MouseOverIconTexture = skill.IconMouseOverTexture,
                    DisabledIconTexture = skill.IconDisabledTexture,
                    IsRecommended = _isInGuild && skill.SkillId == _recommendedSkillId,
                    PendingActionLabel = _pendingRequest?.SkillId == skill.SkillId ? _pendingRequest.ActionLabel : string.Empty,
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
                SaveCurrentGuildState(_activeGuildStateKey);
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

        internal bool HasPendingPacketRequest => _pendingRequest != null;

        internal string TryRenewSelectedSkill(bool packetOwned)
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

            if (packetOwned)
            {
                return StagePendingRequest(
                    GuildSkillPendingRequestKind.Renew,
                    selectedSkill,
                    renewalCost,
                    durationMinutes,
                    targetLevel: selectedSkill.CurrentLevel);
            }

            return ApplyRenewSelectedSkill(selectedSkill, renewalCost, durationMinutes);
        }

        internal string ResolvePendingPacketRequest(bool approved, string summary = null, GuildSkillPacketResolution? packetResolution = null)
        {
            if (_pendingRequest == null)
            {
                return null;
            }

            GuildSkillPendingRequest pendingRequest = _pendingRequest;
            _pendingRequest = null;

            if (!approved)
            {
                return string.IsNullOrWhiteSpace(summary)
                    ? $"{pendingRequest.ActionLabel} for {pendingRequest.SkillName} was rejected. Guild fund remains {FormatMeso(_guildFundMeso)}."
                    : summary.Trim();
            }

            if (!_isInGuild || !string.Equals(_activeGuildStateKey, pendingRequest.GuildStateKey, StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(summary)
                    ? $"Packet approval for {pendingRequest.ActionLabel.ToLowerInvariant()} on {pendingRequest.SkillName} arrived after the active guild context changed, so the request was discarded."
                    : summary.Trim();
            }

            SkillDisplayData selectedSkill = _skills.FirstOrDefault(skill => skill?.SkillId == pendingRequest.SkillId);
            if (selectedSkill == null)
            {
                return string.IsNullOrWhiteSpace(summary)
                    ? $"Packet approval for {pendingRequest.ActionLabel.ToLowerInvariant()} on skill {pendingRequest.SkillId} could not be applied because that skill is no longer loaded."
                    : summary.Trim();
            }

            string result = packetResolution.HasValue
                ? ApplyPacketResolution(pendingRequest, selectedSkill, packetResolution.Value)
                : pendingRequest.Kind switch
                {
                    GuildSkillPendingRequestKind.Renew => ApplyRenewSelectedSkill(selectedSkill, pendingRequest.Cost, pendingRequest.DurationMinutes),
                    GuildSkillPendingRequestKind.LevelUp => ApplyLevelUpSelectedSkill(selectedSkill, pendingRequest.Cost),
                    _ => $"Unsupported pending guild skill request for {selectedSkill.SkillName}."
                };

            return string.IsNullOrWhiteSpace(summary)
                ? result
                : $"{summary.Trim()} {result}";
        }

        internal string ApplyPacketOwnedResult(GuildSkillResultPacket packet)
        {
            if (!_isInGuild)
            {
                return "Ignored guild-skill packet result because no guild is currently active.";
            }

            SkillDisplayData selectedSkill = _skills.FirstOrDefault(skill => skill?.SkillId == packet.SkillId);
            if (selectedSkill == null)
            {
                return $"Guild-skill packet result for skill {packet.SkillId} could not be applied because that skill is not loaded.";
            }

            GuildSkillPendingRequest pendingRequest = ResolveMatchingPendingRequest(packet);
            if (!packet.Approved)
            {
                if (pendingRequest != null)
                {
                    _pendingRequest = null;
                }

                string rejectedMessage = pendingRequest != null
                    ? $"{ResolvePacketActionLabel(packet.Kind)} for {selectedSkill.SkillName} was rejected by the packet-owned guild authority."
                    : $"{ResolvePacketActionLabel(packet.Kind)} rejection for {selectedSkill.SkillName} arrived without a matching pending request.";
                return string.IsNullOrWhiteSpace(packet.Summary)
                    ? rejectedMessage
                    : $"{packet.Summary.Trim()} {rejectedMessage}";
            }

            GuildSkillPacketResolution packetResolution = new(
                packet.SkillLevel,
                packet.RemainingDurationMinutes,
                packet.GuildFundMeso);
            string result;
            if (pendingRequest != null)
            {
                _pendingRequest = null;
                result = ApplyPacketResolution(pendingRequest, selectedSkill, packetResolution);
            }
            else
            {
                result = ApplyStandalonePacketResult(packet, selectedSkill, packetResolution);
            }

            return string.IsNullOrWhiteSpace(packet.Summary)
                ? result
                : $"{packet.Summary.Trim()} {result}";
        }

        internal string ApplyPacketOwnedSkillRecord(SocialListGuildSkillRecordPacket packet, int guildId)
        {
            if (!_isInGuild)
            {
                return "Ignored client guild-skill record because no guild is currently active.";
            }

            SkillDisplayData selectedSkill = _skills.FirstOrDefault(skill => skill?.SkillId == packet.SkillId);
            if (selectedSkill == null)
            {
                return $"Client guild-skill record for skill {packet.SkillId} could not be applied because that skill is not loaded.";
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTimeOffset? previousExpiration = _activeGuildSkillExpirations.TryGetValue(selectedSkill.SkillId, out DateTimeOffset existingExpiration) &&
                                                 existingExpiration > now
                ? existingExpiration
                : null;
            int previousLevel = selectedSkill.CurrentLevel;
            int resolvedLevel = Math.Clamp(packet.SkillLevel, 0, Math.Max(0, selectedSkill.MaxLevel));
            selectedSkill.CurrentLevel = resolvedLevel;

            DateTimeOffset? resolvedExpiration = packet.Expiration.HasValue && packet.Expiration.Value > now
                ? packet.Expiration.Value
                : null;
            if (resolvedLevel <= 0 || !resolvedExpiration.HasValue)
            {
                _activeGuildSkillExpirations.Remove(selectedSkill.SkillId);
            }
            else
            {
                _activeGuildSkillExpirations[selectedSkill.SkillId] = resolvedExpiration.Value;
            }

            if (resolvedLevel > previousLevel)
            {
                _availablePoints = Math.Max(0, _availablePoints - (resolvedLevel - previousLevel));
            }

            GuildSkillPendingRequest pendingRequest = ResolveMatchingPendingSkillRecord(selectedSkill.SkillId, previousLevel, resolvedLevel, previousExpiration, resolvedExpiration);
            string pendingResolutionDetail = ApplyPendingSkillRecordResolution(pendingRequest, selectedSkill, previousLevel, resolvedLevel, previousExpiration, resolvedExpiration);

            SaveCurrentGuildState(_activeGuildStateKey);
            EnsureRecommendation();

            string buyerText = string.IsNullOrWhiteSpace(packet.BuyCharacterName)
                ? string.Empty
                : $" bought by {packet.BuyCharacterName.Trim()}";
            int remainingMinutes = GetRemainingDurationMinutes(selectedSkill);
            string expirationText = remainingMinutes > 0
                ? $" with {FormatDuration(remainingMinutes)} remaining"
                : string.Empty;
            string summary = $"Client OnGuildResult(81) synced {selectedSkill.SkillName} to Lv. {selectedSkill.CurrentLevel}{expirationText}{buyerText} for guild {guildId}.";
            return string.IsNullOrWhiteSpace(pendingResolutionDetail)
                ? summary
                : $"{summary} {pendingResolutionDetail}";
        }

        internal string TryLevelSelectedSkill(bool packetOwned)
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
            if (packetOwned)
            {
                return StagePendingRequest(
                    GuildSkillPendingRequestKind.LevelUp,
                    selectedSkill,
                    levelUpCost,
                    durationMinutes: 0,
                    targetLevel: selectedSkill.CurrentLevel + 1);
            }

            return ApplyLevelUpSelectedSkill(selectedSkill, levelUpCost);
        }

        private string StagePendingRequest(
            GuildSkillPendingRequestKind kind,
            SkillDisplayData selectedSkill,
            int cost,
            int durationMinutes,
            int targetLevel)
        {
            if (_pendingRequest != null)
            {
                return $"{_pendingRequest.ActionLabel} for {_pendingRequest.SkillName} is already awaiting packet-owned guild approval.";
            }

            _pendingRequest = new GuildSkillPendingRequest(
                kind,
                _activeGuildStateKey,
                selectedSkill.SkillId,
                selectedSkill.SkillName,
                cost,
                durationMinutes,
                targetLevel);

            return $"{_pendingRequest.ActionLabel} for {selectedSkill.SkillName} is now pending packet-owned guild approval.";
        }

        private string ApplyRenewSelectedSkill(SkillDisplayData selectedSkill, int renewalCost, int durationMinutes)
        {
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
            SaveCurrentGuildState(_activeGuildStateKey);

            int remainingMinutes = Math.Max(1, (int)Math.Ceiling((renewedExpiration - now).TotalMinutes));
            return $"{selectedSkill.SkillName} renewed for {FormatDuration(durationMinutes)} (cost {FormatMeso(renewalCost)}). Remaining: {FormatDuration(remainingMinutes)}. Guild fund: {FormatMeso(_guildFundMeso)}.";
        }

        private string ApplyLevelUpSelectedSkill(SkillDisplayData selectedSkill, int levelUpCost)
        {
            selectedSkill.CurrentLevel++;
            if (selectedSkill.CurrentLevel == 1)
            {
                _activeGuildSkillExpirations.Remove(selectedSkill.SkillId);
            }
            _availablePoints = Math.Max(0, _availablePoints - 1);
            _guildFundMeso = Math.Max(0, _guildFundMeso - levelUpCost);
            SaveCurrentGuildState(_activeGuildStateKey);
            EnsureRecommendation();
            return $"{selectedSkill.SkillName} advanced to Lv. {selectedSkill.CurrentLevel}. Guild fund: {FormatMeso(_guildFundMeso)}.";
        }

        private string ApplyPacketResolution(
            GuildSkillPendingRequest pendingRequest,
            SkillDisplayData selectedSkill,
            GuildSkillPacketResolution packetResolution)
        {
            return pendingRequest.Kind switch
            {
                GuildSkillPendingRequestKind.LevelUp => ApplyPacketOwnedLevelUpResolution(pendingRequest, selectedSkill, packetResolution),
                GuildSkillPendingRequestKind.Renew => ApplyPacketOwnedRenewalResolution(pendingRequest, selectedSkill, packetResolution),
                _ => $"Unsupported pending guild skill request for {selectedSkill.SkillName}."
            };
        }

        private string ApplyPacketOwnedLevelUpResolution(
            GuildSkillPendingRequest pendingRequest,
            SkillDisplayData selectedSkill,
            GuildSkillPacketResolution packetResolution)
        {
            int previousLevel = selectedSkill.CurrentLevel;
            int resolvedLevel = Math.Clamp(packetResolution.ResolvedSkillLevel ?? pendingRequest.TargetLevel, 0, Math.Max(0, selectedSkill.MaxLevel));
            bool gainedLevel = resolvedLevel > previousLevel;

            selectedSkill.CurrentLevel = resolvedLevel;
            SyncActiveExpirationForResolvedLevel(selectedSkill, previousLevel, resolvedLevel);

            if (packetResolution.GuildFundMeso.HasValue)
            {
                _guildFundMeso = Math.Max(0, packetResolution.GuildFundMeso.Value);
            }
            else if (gainedLevel)
            {
                _guildFundMeso = Math.Max(0, _guildFundMeso - pendingRequest.Cost);
            }

            if (gainedLevel)
            {
                int spentPoints = Math.Max(1, resolvedLevel - previousLevel);
                _availablePoints = Math.Max(0, _availablePoints - spentPoints);
            }

            SaveCurrentGuildState(_activeGuildStateKey);
            EnsureRecommendation();

            return gainedLevel
                ? $"{selectedSkill.SkillName} now matches the packet-owned Lv. {selectedSkill.CurrentLevel} result. Guild fund: {FormatMeso(_guildFundMeso)}."
                : $"{selectedSkill.SkillName} approval arrived without a visible level change. Guild fund: {FormatMeso(_guildFundMeso)}.";
        }

        private string ApplyPacketOwnedRenewalResolution(
            GuildSkillPendingRequest pendingRequest,
            SkillDisplayData selectedSkill,
            GuildSkillPacketResolution packetResolution)
        {
            int previousRemainingMinutes = GetRemainingDurationMinutes(selectedSkill);
            int resolvedRemainingMinutes = Math.Max(0, packetResolution.RemainingDurationMinutes ?? 0);
            bool hasExplicitRemaining = packetResolution.RemainingDurationMinutes.HasValue;

            if (hasExplicitRemaining)
            {
                if (resolvedRemainingMinutes > 0)
                {
                    _activeGuildSkillExpirations[selectedSkill.SkillId] = DateTimeOffset.UtcNow.AddMinutes(resolvedRemainingMinutes);
                }
                else
                {
                    _activeGuildSkillExpirations.Remove(selectedSkill.SkillId);
                }
            }
            else
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                DateTimeOffset baseTime = now;
                if (_activeGuildSkillExpirations.TryGetValue(selectedSkill.SkillId, out DateTimeOffset expiration) &&
                    expiration > now)
                {
                    baseTime = expiration;
                }

                DateTimeOffset renewedExpiration = baseTime.AddMinutes(pendingRequest.DurationMinutes);
                _activeGuildSkillExpirations[selectedSkill.SkillId] = renewedExpiration;
                resolvedRemainingMinutes = Math.Max(1, (int)Math.Ceiling((renewedExpiration - now).TotalMinutes));
            }

            if (packetResolution.GuildFundMeso.HasValue)
            {
                _guildFundMeso = Math.Max(0, packetResolution.GuildFundMeso.Value);
            }
            else if (!hasExplicitRemaining || resolvedRemainingMinutes > previousRemainingMinutes)
            {
                _guildFundMeso = Math.Max(0, _guildFundMeso - pendingRequest.Cost);
            }

            SaveCurrentGuildState(_activeGuildStateKey);

            return resolvedRemainingMinutes > 0
                ? $"{selectedSkill.SkillName} now mirrors the packet-owned renewal window with {FormatDuration(resolvedRemainingMinutes)} remaining. Guild fund: {FormatMeso(_guildFundMeso)}."
                : $"{selectedSkill.SkillName} approval arrived without an active renewal timer. Guild fund: {FormatMeso(_guildFundMeso)}.";
        }

        private GuildSkillPendingRequest ResolveMatchingPendingRequest(GuildSkillResultPacket packet)
        {
            if (_pendingRequest == null || _pendingRequest.SkillId != packet.SkillId)
            {
                return null;
            }

            if (!TryResolvePendingKind(packet.Kind, out GuildSkillPendingRequestKind expectedKind))
            {
                return null;
            }

            return _pendingRequest.Kind == expectedKind ? _pendingRequest : null;
        }

        private GuildSkillPendingRequest ResolveMatchingPendingSkillRecord(
            int skillId,
            int previousLevel,
            int resolvedLevel,
            DateTimeOffset? previousExpiration,
            DateTimeOffset? resolvedExpiration)
        {
            if (_pendingRequest == null || _pendingRequest.SkillId != skillId)
            {
                return null;
            }

            return _pendingRequest.Kind switch
            {
                GuildSkillPendingRequestKind.LevelUp when resolvedLevel != previousLevel => _pendingRequest,
                GuildSkillPendingRequestKind.Renew when !Nullable.Equals(previousExpiration, resolvedExpiration) => _pendingRequest,
                _ => null
            };
        }

        private string ApplyPendingSkillRecordResolution(
            GuildSkillPendingRequest pendingRequest,
            SkillDisplayData selectedSkill,
            int previousLevel,
            int resolvedLevel,
            DateTimeOffset? previousExpiration,
            DateTimeOffset? resolvedExpiration)
        {
            if (pendingRequest == null || selectedSkill == null)
            {
                return string.Empty;
            }

            _pendingRequest = null;
            switch (pendingRequest.Kind)
            {
                case GuildSkillPendingRequestKind.LevelUp:
                {
                    if (resolvedLevel > previousLevel)
                    {
                        _guildFundMeso = Math.Max(0, _guildFundMeso - pendingRequest.Cost);
                        return $"Pending packet-owned level-up approval resolved through OnGuildResult(81). Guild fund: {FormatMeso(_guildFundMeso)}.";
                    }

                    return $"Pending packet-owned level-up approval cleared by an authoritative OnGuildResult(81) skill-record echo.";
                }

                case GuildSkillPendingRequestKind.Renew:
                {
                    if (resolvedExpiration.HasValue && !Nullable.Equals(previousExpiration, resolvedExpiration))
                    {
                        _guildFundMeso = Math.Max(0, _guildFundMeso - pendingRequest.Cost);
                        int remainingMinutes = GetRemainingDurationMinutes(selectedSkill);
                        return $"Pending packet-owned renewal approval resolved through OnGuildResult(81) with {FormatDuration(remainingMinutes)} remaining. Guild fund: {FormatMeso(_guildFundMeso)}.";
                    }

                    return $"Pending packet-owned renewal approval cleared by an authoritative OnGuildResult(81) skill-record echo.";
                }

                default:
                    return string.Empty;
            }
        }

        private string ApplyStandalonePacketResult(
            GuildSkillResultPacket packet,
            SkillDisplayData selectedSkill,
            GuildSkillPacketResolution packetResolution)
        {
            return packet.Kind switch
            {
                GuildSkillResultPacketKind.LevelUp => ApplyStandalonePacketOwnedLevelSync(selectedSkill, packetResolution),
                GuildSkillResultPacketKind.Renew => ApplyStandalonePacketOwnedRenewalSync(selectedSkill, packetResolution),
                _ => $"Unsupported packet-owned guild skill result for {selectedSkill.SkillName}."
            };
        }

        private string ApplyStandalonePacketOwnedLevelSync(
            SkillDisplayData selectedSkill,
            GuildSkillPacketResolution packetResolution)
        {
            int previousLevel = selectedSkill.CurrentLevel;
            int resolvedLevel = Math.Clamp(packetResolution.ResolvedSkillLevel ?? previousLevel, 0, Math.Max(0, selectedSkill.MaxLevel));
            bool changedLevel = resolvedLevel != previousLevel;
            bool gainedLevel = resolvedLevel > previousLevel;

            selectedSkill.CurrentLevel = resolvedLevel;
            SyncActiveExpirationForResolvedLevel(selectedSkill, previousLevel, resolvedLevel);

            if (packetResolution.GuildFundMeso.HasValue)
            {
                _guildFundMeso = Math.Max(0, packetResolution.GuildFundMeso.Value);
            }

            if (gainedLevel)
            {
                _availablePoints = Math.Max(0, _availablePoints - (resolvedLevel - previousLevel));
            }

            SaveCurrentGuildState(_activeGuildStateKey);
            EnsureRecommendation();

            if (changedLevel)
            {
                return $"{selectedSkill.SkillName} now mirrors the packet-owned Lv. {selectedSkill.CurrentLevel} echo. Guild fund: {FormatMeso(_guildFundMeso)}.";
            }

            return packetResolution.GuildFundMeso.HasValue
                ? $"{selectedSkill.SkillName} kept Lv. {selectedSkill.CurrentLevel} while syncing the packet-owned guild fund to {FormatMeso(_guildFundMeso)}."
                : $"{selectedSkill.SkillName} packet-owned level echo matched the current local state.";
        }

        private string ApplyStandalonePacketOwnedRenewalSync(
            SkillDisplayData selectedSkill,
            GuildSkillPacketResolution packetResolution)
        {
            if (!packetResolution.RemainingDurationMinutes.HasValue && !packetResolution.GuildFundMeso.HasValue)
            {
                return $"{selectedSkill.SkillName} packet-owned renewal echo did not carry any timer or guild-fund update.";
            }

            int resolvedRemainingMinutes = Math.Max(0, packetResolution.RemainingDurationMinutes ?? GetRemainingDurationMinutes(selectedSkill));
            if (packetResolution.RemainingDurationMinutes.HasValue)
            {
                if (resolvedRemainingMinutes > 0)
                {
                    _activeGuildSkillExpirations[selectedSkill.SkillId] = DateTimeOffset.UtcNow.AddMinutes(resolvedRemainingMinutes);
                }
                else
                {
                    _activeGuildSkillExpirations.Remove(selectedSkill.SkillId);
                }
            }

            if (packetResolution.GuildFundMeso.HasValue)
            {
                _guildFundMeso = Math.Max(0, packetResolution.GuildFundMeso.Value);
            }

            SaveCurrentGuildState(_activeGuildStateKey);

            return resolvedRemainingMinutes > 0
                ? $"{selectedSkill.SkillName} now mirrors the packet-owned renewal echo with {FormatDuration(resolvedRemainingMinutes)} remaining. Guild fund: {FormatMeso(_guildFundMeso)}."
                : $"{selectedSkill.SkillName} packet-owned renewal echo cleared the active timer. Guild fund: {FormatMeso(_guildFundMeso)}.";
        }

        private static string ResolvePacketActionLabel(GuildSkillResultPacketKind kind)
        {
            return kind == GuildSkillResultPacketKind.Renew ? "Renewal" : "Level-up";
        }

        private void SyncActiveExpirationForResolvedLevel(SkillDisplayData selectedSkill, int previousLevel, int resolvedLevel)
        {
            if (selectedSkill == null)
            {
                return;
            }

            if (resolvedLevel <= 0 || (resolvedLevel > previousLevel && previousLevel == 0))
            {
                _activeGuildSkillExpirations.Remove(selectedSkill.SkillId);
            }
        }

        private static bool TryResolvePendingKind(GuildSkillResultPacketKind kind, out GuildSkillPendingRequestKind pendingKind)
        {
            switch (kind)
            {
                case GuildSkillResultPacketKind.LevelUp:
                    pendingKind = GuildSkillPendingRequestKind.LevelUp;
                    return true;

                case GuildSkillResultPacketKind.Renew:
                    pendingKind = GuildSkillPendingRequestKind.Renew;
                    return true;

                default:
                    pendingKind = default;
                    return false;
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
            if (_pendingRequest != null || !CanManageSkills() || skill == null || _availablePoints <= 0 || skill.CurrentLevel >= skill.MaxLevel)
            {
                return false;
            }

            return _guildLevel >= GetRequiredGuildLevel(skill, skill.CurrentLevel + 1) &&
                   HasEnoughGuildFund(ResolveLevelUpCost(skill));
        }

        private bool CanRenew(SkillDisplayData skill)
        {
            if (_pendingRequest != null || !CanManageSkills() || skill == null || skill.CurrentLevel <= 0)
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

            if (_skills.Any(skill => skill.SkillId == _recommendedSkillId))
            {
                return;
            }

            SkillDisplayData nextRecommended = _skills.FirstOrDefault(CanLevelUp) ?? _skills[0];
            _recommendedSkillId = nextRecommended.SkillId;
        }

        private string[] BuildSummaryLines(
            SkillDisplayData selectedSkill,
            int selectedRequiredGuildLevel,
            int selectedRemainingMinutes,
            string pendingActionLabel)
        {
            if (selectedSkill == null)
            {
                return new[]
                {
                    _isInGuild ? $"Guild: {_guildName}" : "Current: Join a guild to use guild skills.",
                    _isInGuild
                        ? string.IsNullOrWhiteSpace(pendingActionLabel)
                            ? $"Role: {_guildRoleLabel}  |  Guild Lv. {_guildLevel}"
                            : $"Pending: {pendingActionLabel} approval  |  Guild Lv. {_guildLevel}"
                        : "Next: Guild skills unlock with real guild membership.",
                    _isInGuild
                        ? $"SP: {_availablePoints}  |  GP: {FormatCompactGuildPoints(_guildPoints)}  |  Fund: {FormatCompactMeso(_guildFundMeso)}"
                        : "State: No guild"
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

            if (_pendingRequest != null && !string.IsNullOrWhiteSpace(_pendingRequest.ActionLabel))
            {
                parts.Add(_pendingRequest.SkillId == selectedSkill.SkillId
                    ? $"Pending {_pendingRequest.ActionLabel}"
                    : $"Pending {_pendingRequest.ActionLabel} for {_pendingRequest.SkillName}");
            }

            string stateLabel = ResolveStateLabel(_isInGuild, CanManageSkills(), selectedSkill.CurrentLevel, durationMinutes, remainingMinutes);
            if (!string.IsNullOrWhiteSpace(stateLabel))
            {
                parts.Add(stateLabel);
            }

            parts.Add($"Fund {FormatCompactMeso(_guildFundMeso)}");

            if (_isInGuild)
            {
                parts.Add($"GP {FormatCompactGuildPoints(_guildPoints)}");
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

        internal static string ResolveStateLabel(bool inGuild, bool canManageSkills, int currentLevel, int durationMinutes, int remainingMinutes)
        {
            if (!inGuild)
            {
                return "No guild";
            }

            if (remainingMinutes > 0)
            {
                return "Active";
            }

            if (currentLevel > 0 && durationMinutes > 0)
            {
                return "Inactive";
            }

            return string.Empty;
        }

        internal static string ResolveAuthorityLabel(bool inGuild, bool canManageSkills)
        {
            if (!inGuild || canManageSkills)
            {
                return string.Empty;
            }

            return "View only";
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

        private static string FormatCompactGuildPoints(int amount)
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

        private void RestoreGuildState(GuildSkillSavedState savedState)
        {
            for (int i = 0; i < _skills.Count; i++)
            {
                SkillDisplayData skill = _skills[i];
                if (savedState?.SkillLevels == null || !savedState.SkillLevels.TryGetValue(skill.SkillId, out int savedLevel))
                {
                    savedLevel = GetSeededLevel(i, skill);
                }

                skill.CurrentLevel = Math.Clamp(savedLevel, 0, Math.Max(0, skill.MaxLevel));
            }

            _availablePoints = Math.Max(0, savedState?.AvailablePoints ?? DefaultAvailablePoints);
            _guildFundMeso = Math.Max(0, savedState?.GuildFundMeso ?? DefaultGuildFundMeso);
            _activeGuildSkillExpirations.Clear();
            if (savedState?.ActiveExpirations == null)
            {
                return;
            }

            foreach (KeyValuePair<int, DateTimeOffset> entry in savedState.ActiveExpirations)
            {
                if (entry.Value > DateTimeOffset.UtcNow)
                {
                    _activeGuildSkillExpirations[entry.Key] = entry.Value;
                }
            }
        }

        private void SaveCurrentGuildState(string guildStateKey)
        {
            if (string.IsNullOrWhiteSpace(guildStateKey))
            {
                return;
            }

            Dictionary<int, int> savedSkillLevels = new();
            foreach (SkillDisplayData skill in _skills)
            {
                savedSkillLevels[skill.SkillId] = Math.Clamp(skill.CurrentLevel, 0, Math.Max(0, skill.MaxLevel));
            }

            Dictionary<int, DateTimeOffset> savedActiveGuildSkillExpirations = new();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (KeyValuePair<int, DateTimeOffset> entry in _activeGuildSkillExpirations)
            {
                if (entry.Value > now)
                {
                    savedActiveGuildSkillExpirations[entry.Key] = entry.Value;
                }
            }

            _savedGuildStates[guildStateKey] = new GuildSkillSavedState(
                Math.Max(1, _guildLevel),
                Math.Max(0, _availablePoints),
                Math.Max(0, _guildPoints),
                Math.Max(0, _guildFundMeso),
                savedSkillLevels,
                savedActiveGuildSkillExpirations);
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

        private int ResolveGuildLevel(int requestedGuildLevel, int savedGuildLevel)
        {
            if (requestedGuildLevel > 0)
            {
                return Math.Max(1, requestedGuildLevel);
            }

            return ResolveLocalFallbackGuildLevel(savedGuildLevel);
        }

        private int ResolveGuildPoints(int? requestedGuildPoints, int savedGuildPoints)
        {
            if (requestedGuildPoints.HasValue)
            {
                return Math.Max(0, requestedGuildPoints.Value);
            }

            return Math.Max(0, Math.Max(_guildPoints, savedGuildPoints));
        }

        private int ResolveLocalFallbackGuildLevel(int savedGuildLevel = 0)
        {
            return Math.Max(1, Math.Max(Math.Max(_guildLevel, savedGuildLevel), DefaultLocalGuildLevel));
        }

        private GuildSkillSavedState GetSavedGuildState(string guildStateKey)
        {
            if (string.IsNullOrWhiteSpace(guildStateKey))
            {
                return null;
            }

            _savedGuildStates.TryGetValue(guildStateKey, out GuildSkillSavedState savedState);
            return savedState;
        }

        private static string NormalizeGuildStateKey(string guildName)
        {
            return string.IsNullOrWhiteSpace(guildName)
                ? string.Empty
                : guildName.Trim();
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

    internal sealed class GuildSkillPendingRequest
    {
        internal GuildSkillPendingRequest(
            GuildSkillRuntime.GuildSkillPendingRequestKind kind,
            string guildStateKey,
            int skillId,
            string skillName,
            int cost,
            int durationMinutes,
            int targetLevel)
        {
            Kind = kind;
            GuildStateKey = guildStateKey ?? string.Empty;
            SkillId = skillId;
            SkillName = skillName ?? string.Empty;
            Cost = Math.Max(0, cost);
            DurationMinutes = Math.Max(0, durationMinutes);
            TargetLevel = Math.Max(0, targetLevel);
        }

        internal GuildSkillRuntime.GuildSkillPendingRequestKind Kind { get; }
        internal string GuildStateKey { get; }
        internal int SkillId { get; }
        internal string SkillName { get; }
        internal int Cost { get; }
        internal int DurationMinutes { get; }
        internal int TargetLevel { get; }
        internal string ActionLabel => Kind == GuildSkillRuntime.GuildSkillPendingRequestKind.LevelUp ? "Level-up" : "Renewal";
    }

    internal sealed class GuildSkillSavedState
    {
        internal GuildSkillSavedState(
            int guildLevel,
            int availablePoints,
            int guildPoints,
            int guildFundMeso,
            IReadOnlyDictionary<int, int> skillLevels,
            IReadOnlyDictionary<int, DateTimeOffset> activeExpirations)
        {
            GuildLevel = guildLevel;
            AvailablePoints = availablePoints;
            GuildPoints = guildPoints;
            GuildFundMeso = guildFundMeso;
            SkillLevels = skillLevels ?? new Dictionary<int, int>();
            ActiveExpirations = activeExpirations ?? new Dictionary<int, DateTimeOffset>();
        }

        internal int GuildLevel { get; }
        internal int AvailablePoints { get; }
        internal int GuildPoints { get; }
        internal int GuildFundMeso { get; }
        internal IReadOnlyDictionary<int, int> SkillLevels { get; }
        internal IReadOnlyDictionary<int, DateTimeOffset> ActiveExpirations { get; }
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
        public int GuildPoints { get; init; }
        public int SelectedIndex { get; init; } = -1;
        public int RecommendedSkillId { get; init; }
        public string PendingActionLabel { get; init; } = string.Empty;
        public bool HasPendingRequest { get; init; }
        public bool CanRenew { get; init; }
        public bool CanLevelUpSelected { get; init; }
        public IReadOnlyList<string> SummaryLines { get; init; } = Array.Empty<string>();
        public IReadOnlyList<GuildSkillEntrySnapshot> Entries { get; init; } = Array.Empty<GuildSkillEntrySnapshot>();
    }

    internal sealed class GuildSkillEntrySnapshot
    {
        public bool InGuild { get; init; }
        public string GuildRoleLabel { get; init; } = string.Empty;
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
        public int GuildPoints { get; init; }
        public Microsoft.Xna.Framework.Graphics.Texture2D IconTexture { get; init; }
        public Microsoft.Xna.Framework.Graphics.Texture2D MouseOverIconTexture { get; init; }
        public Microsoft.Xna.Framework.Graphics.Texture2D DisabledIconTexture { get; init; }
        public bool IsRecommended { get; init; }
        public string PendingActionLabel { get; init; } = string.Empty;
        public bool CanManageSkills { get; init; }
        public bool CanLevelUp { get; init; }
        public bool CanRenew { get; init; }
    }

    internal readonly record struct GuildSkillPacketResolution(
        int? ResolvedSkillLevel,
        int? RemainingDurationMinutes,
        int? GuildFundMeso);
}
