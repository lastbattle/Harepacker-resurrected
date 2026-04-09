using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed partial class SocialListRuntime
    {
        private PacketGuildAuthorityState? _packetGuildAuthority;
        private PacketAllianceAuthorityState? _packetAllianceAuthority;
        private PacketGuildUiState? _packetGuildUiState;
        private GuildMarkSelection? _packetGuildMarkSelection;
        private int _packetGuildPoints;
        private readonly List<GuildRankingSeedEntry> _packetGuildRankingEntries = [];
        private GuildDialogPendingRequest? _pendingGuildDialogRequest;
        private int _guildDialogMesoBalance = 10_000_000;

        private const int DefaultGuildCreateCostMesos = 1_500_000;
        private const int DefaultGuildMarkCostMesos = 5_000_000;

        internal string DescribeStatus()
        {
            string[] tabLines = Enum.GetValues(typeof(SocialListTab))
                .Cast<SocialListTab>()
                .Select(DescribeTabStatusLine)
                .ToArray();

            string guildAuthority = _packetGuildAuthority.HasValue
                ? $"Guild authority packet-owned ({_packetGuildAuthority.Value.RoleLabel}: rank={FormatOnOff(_packetGuildAuthority.Value.CanManageRanks)}, admit={FormatOnOff(_packetGuildAuthority.Value.CanToggleAdmission)}, notice={FormatOnOff(_packetGuildAuthority.Value.CanEditNotice)})"
                : $"Guild authority local-role ({GetLocalGuildRoleLabel()})";
            string guildUiContext = _packetGuildUiState.HasValue
                ? $"Guild UI packet-owned (member={FormatOnOff(_packetGuildUiState.Value.HasGuildMembership)}, name={_packetGuildUiState.Value.GuildName}, level={_packetGuildUiState.Value.GuildLevel})"
                : "Guild UI local-build";
            string guildRankingContext = _packetGuildRankingEntries.Count > 0
                ? $"Guild ranking packet-owned rivals={_packetGuildRankingEntries.Count}"
                : "Guild ranking awaiting packet-owned rivals";
            string guildMarkContext = _packetGuildMarkSelection.HasValue
                ? $"Guild mark shared bg={_packetGuildMarkSelection.Value.MarkBackground}:{_packetGuildMarkSelection.Value.MarkBackgroundColor}, mark={_packetGuildMarkSelection.Value.Mark}:{_packetGuildMarkSelection.Value.MarkColor}, points={_packetGuildPoints}"
                : "Guild mark awaiting guild-result emblem sync";
            string guildDialogRequestContext = DescribeGuildDialogRequestStatus();
            string allianceAuthority = _packetAllianceAuthority.HasValue
                ? $"Alliance authority packet-owned ({_packetAllianceAuthority.Value.RoleLabel}: rank={FormatOnOff(_packetAllianceAuthority.Value.CanEditRanks)}, notice={FormatOnOff(_packetAllianceAuthority.Value.CanEditNotice)})"
                : $"Alliance authority local-role ({GetLocalAllianceRoleLabel()})";

            return string.Join(Environment.NewLine, tabLines.Concat(new[] { guildAuthority, guildUiContext, guildRankingContext, guildMarkContext, guildDialogRequestContext, allianceAuthority }));
        }

        internal string DescribeGuildDialogRequestStatus()
        {
            if (!_pendingGuildDialogRequest.HasValue)
            {
                return $"Guild dialog request idle: mesos={_guildDialogMesoBalance}.";
            }

            GuildDialogPendingRequest request = _pendingGuildDialogRequest.Value;
            return $"Guild dialog request pending: kind={request.RequestLabel}, guild={request.GuildName}, cost={request.RequiredMesos}, mesos={_guildDialogMesoBalance}, authority={(IsPacketOwned(SocialListTab.Guild) ? "packet" : "local")}.";
        }

        internal string SetGuildDialogMesoBalance(int mesos)
        {
            _guildDialogMesoBalance = Math.Max(0, mesos);
            return $"Guild dialog meso balance now tracks {_guildDialogMesoBalance} mesos.";
        }

        private string DescribeTabStatusLine(SocialListTab tab)
        {
            IReadOnlyList<SocialEntryState> visibleEntries = GetFilteredEntries(tab);
            int totalEntries = _entriesByTab.TryGetValue(tab, out List<SocialEntryState> entries) && entries != null
                ? entries.Count
                : visibleEntries.Count;
            SocialEntryState selectedEntry = GetSelectedEntry(tab);
            string selection = selectedEntry == null ? "none" : selectedEntry.Name;
            string syncSummary = _lastPacketSyncSummaryByTab.TryGetValue(tab, out string sync) && !string.IsNullOrWhiteSpace(sync)
                ? sync.Trim()
                : "none";
            string pendingSummary = _lastPendingRequestByTab.TryGetValue(tab, out string pending) && !string.IsNullOrWhiteSpace(pending)
                ? pending.Trim()
                : "none";

            return $"{GetHeaderTitle(tab)}: total={totalEntries}, visible={visibleEntries.Count}, owner={(IsPacketOwned(tab) ? "packet" : "local")}, selected={selection}, sync={syncSummary}, pending={pendingSummary}";
        }

        internal string SetPacketRosterOwnership(SocialListTab tab, bool packetOwned, string summary = null)
        {
            _packetOwnedRosterByTab[tab] = packetOwned;
            _lastPacketSyncSummaryByTab[tab] = string.IsNullOrWhiteSpace(summary)
                ? packetOwned
                    ? "Packet ownership armed without a concrete roster delta yet."
                    : "Returned to simulator-owned roster control."
                : summary.Trim();
            if (!packetOwned)
            {
                _lastPendingRequestByTab[tab] = null;
                if (tab == SocialListTab.Guild)
                {
                    _packetGuildUiState = null;
                    _packetGuildMarkSelection = null;
                    _packetGuildPoints = 0;
                }
            }

            ResetSelectionAfterMutation(tab);
            return packetOwned
                ? $"{GetHeaderTitle(tab)} now follows packet-owned roster authority."
                : $"{GetHeaderTitle(tab)} returned to simulator-owned roster authority.";
        }

        internal string SeedPacketRoster(SocialListTab tab)
        {
            _packetOwnedRosterByTab[tab] = true;
            _lastPacketSyncSummaryByTab[tab] = $"Packet seed captured {_entriesByTab[tab].Count} roster entr{(_entriesByTab[tab].Count == 1 ? "y" : "ies")}.";
            _lastPendingRequestByTab[tab] = null;
            SyncPacketGuildUiStateFromRoster(tab);
            ResetSelectionAfterMutation(tab);
            return $"{GetHeaderTitle(tab)} packet seed now owns {_entriesByTab[tab].Count} roster entr{(_entriesByTab[tab].Count == 1 ? "y" : "ies")}.";
        }

        internal string ClearPacketRoster(SocialListTab tab)
        {
            _entriesByTab[tab].Clear();
            _packetOwnedRosterByTab[tab] = true;
            _lastPacketSyncSummaryByTab[tab] = "Packet clear removed every roster entry.";
            _lastPendingRequestByTab[tab] = null;
            _selectedIndexByTab[tab] = -1;
            _firstVisibleIndexByTab[tab] = 0;
            SyncPacketGuildUiStateFromRoster(tab);
            return $"{GetHeaderTitle(tab)} packet clear removed every roster entry.";
        }

        internal string RemovePacketEntry(SocialListTab tab, string entryName)
        {
            if (string.IsNullOrWhiteSpace(entryName))
            {
                return $"Provide a {GetHeaderTitle(tab)} entry name to remove.";
            }

            int removed = _entriesByTab[tab].RemoveAll(entry => string.Equals(entry.Name, entryName.Trim(), StringComparison.OrdinalIgnoreCase));
            _packetOwnedRosterByTab[tab] = true;
            _lastPendingRequestByTab[tab] = null;
            _lastPacketSyncSummaryByTab[tab] = removed > 0
                ? $"Packet remove deleted {removed} entr{(removed == 1 ? "y" : "ies")} named {entryName.Trim()}."
                : $"Packet remove for {entryName.Trim()} did not match any current roster row.";
            SyncPacketGuildUiStateFromRoster(tab);
            ResetSelectionAfterMutation(tab);
            return removed > 0
                ? $"{entryName.Trim()} was removed from the packet-owned {GetHeaderTitle(tab)} roster."
                : $"{entryName.Trim()} was not present in the current {GetHeaderTitle(tab)} roster.";
        }

        internal string UpsertPacketEntry(
            SocialListTab tab,
            string name,
            string primaryText,
            string secondaryText,
            string locationSummary,
            int channel,
            bool isOnline,
            bool isLeader,
            bool isBlocked,
            bool isLocalPlayer)
        {
            string resolvedName = string.IsNullOrWhiteSpace(name) ? "Packet Entry" : name.Trim();
            SocialEntryState entry = new(
                resolvedName,
                string.IsNullOrWhiteSpace(primaryText) ? "-" : primaryText.Trim(),
                string.IsNullOrWhiteSpace(secondaryText) ? "-" : secondaryText.Trim(),
                string.IsNullOrWhiteSpace(locationSummary) ? "Unknown" : locationSummary.Trim(),
                Math.Max(1, channel),
                isOnline,
                isLeader,
                isBlocked)
            {
                IsLocalPlayer = isLocalPlayer
            };

            List<SocialEntryState> entries = _entriesByTab[tab];
            int existingIndex = entries.FindIndex(current => string.Equals(current.Name, resolvedName, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                entries[existingIndex] = entry;
            }
            else if (isLocalPlayer)
            {
                int localIndex = entries.FindIndex(current => current.IsLocalPlayer);
                if (localIndex >= 0)
                {
                    entries[localIndex] = entry;
                }
                else
                {
                    entries.Insert(0, entry);
                }
            }
            else
            {
                entries.Add(entry);
            }

            _packetOwnedRosterByTab[tab] = true;
            _lastPendingRequestByTab[tab] = null;
            _lastPacketSyncSummaryByTab[tab] = $"Packet upsert applied to {resolvedName} on the {GetHeaderTitle(tab)} roster.";
            if (isLocalPlayer)
            {
                if (tab == SocialListTab.Guild)
                {
                    _guildName = string.IsNullOrWhiteSpace(entry.SecondaryText) ? _guildName : entry.SecondaryText;
                }
                else if (tab == SocialListTab.Alliance)
                {
                    _allianceName = string.IsNullOrWhiteSpace(entry.SecondaryText) ? _allianceName : entry.SecondaryText;
                }
            }

            SyncPacketGuildUiStateFromRoster(tab);
            SelectEntryByName(tab, resolvedName);
            return $"{resolvedName} was upserted into the packet-owned {GetHeaderTitle(tab)} roster.";
        }

        internal string SetPacketSyncSummary(SocialListTab tab, string summary)
        {
            _packetOwnedRosterByTab[tab] = true;
            _lastPacketSyncSummaryByTab[tab] = string.IsNullOrWhiteSpace(summary)
                ? "Packet synchronization summary updated without a concrete delta."
                : summary.Trim();
            return $"{GetHeaderTitle(tab)} packet summary updated.";
        }

        internal string ResolvePacketOwnedRequest(SocialListTab tab, bool approved, string summary = null)
        {
            if (tab == SocialListTab.Guild && _pendingGuildDialogRequest.HasValue)
            {
                return ResolvePendingGuildDialogRequest(approved, summary);
            }

            string pendingRequest = _lastPendingRequestByTab.TryGetValue(tab, out string pending) && !string.IsNullOrWhiteSpace(pending)
                ? pending
                : null;
            if (string.IsNullOrWhiteSpace(pendingRequest))
            {
                return $"There is no staged packet-owned request on the {GetHeaderTitle(tab)} roster.";
            }

            _packetOwnedRosterByTab[tab] = true;
            _lastPendingRequestByTab[tab] = null;
            _lastPacketSyncSummaryByTab[tab] = string.IsNullOrWhiteSpace(summary)
                ? approved
                    ? $"Server approved the staged {pendingRequest.ToLowerInvariant()} request."
                    : $"Server rejected the staged {pendingRequest.ToLowerInvariant()} request."
                : summary.Trim();
            return approved
                ? $"Server approval cleared the staged {pendingRequest.ToLowerInvariant()} request."
                : $"Server rejection cleared the staged {pendingRequest.ToLowerInvariant()} request.";
        }

        internal string SelectEntryByName(SocialListTab tab, string entryName)
        {
            if (string.IsNullOrWhiteSpace(entryName))
            {
                return $"Provide a {GetHeaderTitle(tab)} entry name to select.";
            }

            IReadOnlyList<SocialEntryState> entries = GetFilteredEntries(tab);
            int index = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].Name, entryName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                return $"{entryName.Trim()} is not present in the current {GetHeaderTitle(tab)} roster.";
            }

            _selectedIndexByTab[tab] = index;
            EnsureSelectionVisible(tab, entries.Count);
            return $"{entryName.Trim()} is now selected on the {GetHeaderTitle(tab)} roster.";
        }

        internal string ClearPacketGuildAuthority()
        {
            _packetGuildAuthority = null;
            return $"Guild authority returned to the local-role seam ({GetLocalGuildRoleLabel()}).";
        }

        internal string SetPacketGuildUiContext(bool hasGuildMembership, string guildName, int guildLevel)
        {
            string normalizedGuildName = hasGuildMembership
                ? (string.IsNullOrWhiteSpace(guildName) ? _guildName : guildName.Trim())
                : "No Guild";
            _packetGuildUiState = new PacketGuildUiState(
                hasGuildMembership && GuildSkillRuntime.HasGuildMembership(normalizedGuildName),
                normalizedGuildName,
                Math.Max(0, guildLevel));
            TryFinalizePendingGuildDialogRequestFromPacket();
            return _packetGuildUiState.Value.HasGuildMembership
                ? $"Guild UI now follows packet-owned membership for {normalizedGuildName} (Guild Lv. {_packetGuildUiState.Value.GuildLevel})."
                : "Guild UI now follows packet-owned no-guild membership.";
        }

        internal string ClearPacketGuildUiContext()
        {
            _packetGuildUiState = null;
            return "Guild UI returned to the local build seam.";
        }

        internal GuildMarkSelection? GetEffectiveGuildMarkSelection()
        {
            return _packetGuildMarkSelection;
        }

        internal string ClearPacketGuildRankingEntries()
        {
            _packetGuildRankingEntries.Clear();
            return "Guild ranking packet-owned rival rows were cleared.";
        }

        internal string SetPacketGuildRankingEntries(IReadOnlyList<GuildRankingSeedEntry> rankingEntries, int guildId)
        {
            _packetGuildRankingEntries.Clear();
            if (rankingEntries != null)
            {
                foreach (GuildRankingSeedEntry entry in rankingEntries)
                {
                    if (string.IsNullOrWhiteSpace(entry.GuildName))
                    {
                        continue;
                    }

                    _packetGuildRankingEntries.Add(entry with
                    {
                        GuildName = entry.GuildName.Trim(),
                        MasterName = string.IsNullOrWhiteSpace(entry.MasterName) ? "Guild Master" : entry.MasterName.Trim(),
                        IsPacketOwned = true
                    });
                }
            }

            return _packetGuildRankingEntries.Count > 0
                ? $"Client OnGuildResult({(byte)SocialListClientGuildResultKind.Ranking}) refreshed {_packetGuildRankingEntries.Count} rival guild ranking row{(_packetGuildRankingEntries.Count == 1 ? string.Empty : "s")} for guild {guildId}."
                : $"Client OnGuildResult({(byte)SocialListClientGuildResultKind.Ranking}) cleared rival guild ranking rows for guild {guildId}.";
        }

        internal string UpsertPacketGuildRankingEntry(
            string guildName,
            string masterName,
            string levelRange,
            string memberSummary,
            string notice,
            int? markBackground,
            int? markBackgroundColor,
            int? mark,
            int? markColor)
        {
            string normalizedGuildName = string.IsNullOrWhiteSpace(guildName) ? "Packet Guild" : guildName.Trim();
            GuildRankingSeedEntry entry = new(
                normalizedGuildName,
                string.IsNullOrWhiteSpace(masterName) ? "Guild Master" : masterName.Trim(),
                null,
                string.IsNullOrWhiteSpace(levelRange) ? "Lv. 1" : levelRange.Trim(),
                string.IsNullOrWhiteSpace(memberSummary) ? "1/1 online" : memberSummary.Trim(),
                notice?.Trim() ?? string.Empty,
                markBackground,
                markBackgroundColor,
                mark,
                markColor,
                IsPacketOwned: true);

            int existingIndex = _packetGuildRankingEntries.FindIndex(candidate =>
                string.Equals(candidate.GuildName, normalizedGuildName, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                _packetGuildRankingEntries[existingIndex] = entry;
                return $"Guild ranking packet row for {normalizedGuildName} was updated.";
            }

            _packetGuildRankingEntries.Add(entry);
            return $"Guild ranking packet row for {normalizedGuildName} was added.";
        }

        internal string RemovePacketGuildRankingEntry(string guildName)
        {
            if (string.IsNullOrWhiteSpace(guildName))
            {
                return "Provide a guild name to remove from packet-owned ranking rows.";
            }

            int removed = _packetGuildRankingEntries.RemoveAll(entry =>
                string.Equals(entry.GuildName, guildName.Trim(), StringComparison.OrdinalIgnoreCase));
            return removed > 0
                ? $"{guildName.Trim()} was removed from packet-owned guild ranking rows."
                : $"{guildName.Trim()} was not present in packet-owned guild ranking rows.";
        }

        internal string SetPacketGuildAuthority(string roleLabel, bool canManageRanks, bool canToggleAdmission, bool canEditNotice)
        {
            string resolvedRole = string.IsNullOrWhiteSpace(roleLabel) ? GetLocalGuildRoleLabel() : roleLabel.Trim();
            _packetGuildAuthority = new PacketGuildAuthorityState(resolvedRole, canManageRanks, canToggleAdmission, canEditNotice);
            return $"Guild authority now follows packet-owned role {resolvedRole} (rank={FormatOnOff(canManageRanks)}, admission={FormatOnOff(canToggleAdmission)}, notice={FormatOnOff(canEditNotice)}).";
        }

        internal string SetPacketGuildMarkSelection(GuildMarkSelection selection, int guildId)
        {
            _packetGuildMarkSelection = selection with { ComboIndex = ResolveGuildMarkComboIndex(selection.Mark) };
            _lastPacketSyncSummaryByTab[SocialListTab.Guild] =
                $"Client OnGuildResult({(byte)SocialListClientGuildResultKind.Mark}) refreshed emblem bg={selection.MarkBackground}:{selection.MarkBackgroundColor}, mark={selection.Mark}:{selection.MarkColor}.";
            TryFinalizePendingGuildDialogRequestFromPacket();
            return $"Client OnGuildResult({(byte)SocialListClientGuildResultKind.Mark}) refreshed the shared guild emblem for guild {guildId}.";
        }

        internal string SetPacketGuildPointsAndLevel(int guildPoints, int guildLevel, int guildId)
        {
            _packetGuildPoints = Math.Max(0, guildPoints);
            if (_packetGuildUiState.HasValue)
            {
                PacketGuildUiState guildUi = _packetGuildUiState.Value;
                _packetGuildUiState = guildUi with { GuildLevel = Math.Max(0, guildLevel) };
            }

            _lastPacketSyncSummaryByTab[SocialListTab.Guild] =
                $"Client OnGuildResult({(byte)SocialListClientGuildResultKind.PointsAndLevel}) refreshed guild points={_packetGuildPoints}, level={Math.Max(0, guildLevel)}.";
            return $"Client OnGuildResult({(byte)SocialListClientGuildResultKind.PointsAndLevel}) refreshed guild {guildId} to Lv. {Math.Max(0, guildLevel)} with {_packetGuildPoints} point(s).";
        }

        internal string SubmitLocalGuildMarkSelection(GuildMarkSelection selection)
        {
            return SubmitPendingGuildDialogRequest(new GuildDialogPendingRequest(
                GuildDialogPendingRequestKind.SetMark,
                "Set guild mark",
                _playerName,
                ResolveEffectiveGuildName(null, ResolveEffectiveGuildMembership(null)),
                selection with { ComboIndex = ResolveGuildMarkComboIndex(selection.Mark) },
                DefaultGuildMarkCostMesos,
                DateTimeOffset.UtcNow));
        }

        private string ApplyLocalGuildMarkSelectionCore(GuildMarkSelection selection)
        {
            _packetGuildMarkSelection = selection with { ComboIndex = ResolveGuildMarkComboIndex(selection.Mark) };
            string guildName = ResolveEffectiveGuildName(null, ResolveEffectiveGuildMembership(null));
            _lastPacketSyncSummaryByTab[SocialListTab.Guild] =
                $"Local guild-mark commit mirrored the shared guild emblem for {guildName}.";
            return $"Local guild-mark commit now updates the shared guild seam for {guildName}: bg={selection.MarkBackground}:{selection.MarkBackgroundColor}, mark={selection.Mark}:{selection.MarkColor}.";
        }

        private string SubmitPendingGuildDialogRequest(GuildDialogPendingRequest request)
        {
            if (_pendingGuildDialogRequest.HasValue)
            {
                GuildDialogPendingRequest pending = _pendingGuildDialogRequest.Value;
                return $"{pending.RequestLabel} for {pending.GuildName} is already awaiting approval or rejection.";
            }

            if (_guildDialogMesoBalance < request.RequiredMesos)
            {
                return $"{request.RequestLabel} for {request.GuildName} needs {request.RequiredMesos} mesos, but the shared guild dialog seam only tracks {_guildDialogMesoBalance} mesos.";
            }

            _pendingGuildDialogRequest = request;
            _lastPendingRequestByTab[SocialListTab.Guild] = request.RequestLabel;
            _lastPacketSyncSummaryByTab[SocialListTab.Guild] = IsPacketOwned(SocialListTab.Guild)
                ? $"{request.RequestLabel} request for {request.GuildName} was sent through packet-owned guild authority and is awaiting approval."
                : $"{request.RequestLabel} request for {request.GuildName} was submitted through the simulator-owned guild seam.";

            if (IsPacketOwned(SocialListTab.Guild))
            {
                return $"{request.RequestLabel} request for {request.GuildName} is now pending packet-owned guild approval at {request.RequiredMesos} mesos.";
            }

            return ResolvePendingGuildDialogRequest(
                approved: true,
                $"{request.RequestLabel} auto-approved on the simulator-owned guild seam.");
        }

        internal string ResolvePendingGuildDialogRequest(bool approved, string summary = null)
        {
            if (!_pendingGuildDialogRequest.HasValue)
            {
                return "There is no pending guild creation or guild mark request.";
            }

            GuildDialogPendingRequest request = _pendingGuildDialogRequest.Value;
            _pendingGuildDialogRequest = null;
            _lastPendingRequestByTab[SocialListTab.Guild] = null;

            if (!approved)
            {
                _lastPacketSyncSummaryByTab[SocialListTab.Guild] = string.IsNullOrWhiteSpace(summary)
                    ? $"{request.RequestLabel} for {request.GuildName} was rejected before any guild state changed."
                    : summary.Trim();
                return $"{request.RequestLabel} for {request.GuildName} was rejected. Meso balance remains {_guildDialogMesoBalance}.";
            }

            _guildDialogMesoBalance = Math.Max(0, _guildDialogMesoBalance - request.RequiredMesos);
            string applyMessage = request.Kind switch
            {
                GuildDialogPendingRequestKind.CreateGuild => ApplyGuildCreateAgreementAcceptanceCore(request.MasterName, request.GuildName),
                GuildDialogPendingRequestKind.SetMark when request.MarkSelection.HasValue => ApplyLocalGuildMarkSelectionCore(request.MarkSelection.Value),
                _ => null
            };

            _lastPacketSyncSummaryByTab[SocialListTab.Guild] = string.IsNullOrWhiteSpace(summary)
                ? $"{request.RequestLabel} for {request.GuildName} was approved and consumed {request.RequiredMesos} mesos."
                : summary.Trim();
            return string.IsNullOrWhiteSpace(applyMessage)
                ? $"{request.RequestLabel} for {request.GuildName} was approved. Deducted {request.RequiredMesos} mesos; balance={_guildDialogMesoBalance}."
                : $"{request.RequestLabel} for {request.GuildName} was approved. Deducted {request.RequiredMesos} mesos; balance={_guildDialogMesoBalance}. {applyMessage}";
        }

        private void TryFinalizePendingGuildDialogRequestFromPacket()
        {
            if (!_pendingGuildDialogRequest.HasValue)
            {
                return;
            }

            GuildDialogPendingRequest request = _pendingGuildDialogRequest.Value;
            bool shouldFinalize = request.Kind switch
            {
                GuildDialogPendingRequestKind.CreateGuild => _packetGuildUiState.HasValue
                    && _packetGuildUiState.Value.HasGuildMembership
                    && string.Equals(_packetGuildUiState.Value.GuildName, request.GuildName, StringComparison.OrdinalIgnoreCase),
                GuildDialogPendingRequestKind.SetMark => request.MarkSelection.HasValue
                    && _packetGuildMarkSelection.HasValue
                    && _packetGuildMarkSelection.Value == request.MarkSelection.Value,
                _ => false
            };

            if (!shouldFinalize)
            {
                return;
            }

            _pendingGuildDialogRequest = null;
            _lastPendingRequestByTab[SocialListTab.Guild] = null;
            _guildDialogMesoBalance = Math.Max(0, _guildDialogMesoBalance - request.RequiredMesos);
            _lastPacketSyncSummaryByTab[SocialListTab.Guild] =
                $"Client guild-result synchronization finalized {request.RequestLabel.ToLowerInvariant()} for {request.GuildName} and consumed {request.RequiredMesos} mesos.";
        }

        private enum GuildDialogPendingRequestKind
        {
            CreateGuild,
            SetMark
        }

        private readonly record struct GuildDialogPendingRequest(
            GuildDialogPendingRequestKind Kind,
            string RequestLabel,
            string MasterName,
            string GuildName,
            GuildMarkSelection? MarkSelection,
            int RequiredMesos,
            DateTimeOffset RequestedAtUtc);

        private IReadOnlyList<GuildRankingSeedEntry> GetPacketGuildRankingEntries(string localGuildName)
        {
            if (_packetGuildRankingEntries.Count == 0)
            {
                return Array.Empty<GuildRankingSeedEntry>();
            }

            if (string.IsNullOrWhiteSpace(localGuildName))
            {
                return _packetGuildRankingEntries.ToArray();
            }

            return _packetGuildRankingEntries
                .Where(entry => !string.Equals(entry.GuildName, localGuildName.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        internal string ClearPacketAllianceAuthority()
        {
            _packetAllianceAuthority = null;
            return $"Alliance authority returned to the local-role seam ({GetLocalAllianceRoleLabel()}).";
        }

        internal string SetPacketAllianceAuthority(string roleLabel, bool canEditRanks, bool canEditNotice)
        {
            string resolvedRole = string.IsNullOrWhiteSpace(roleLabel) ? GetLocalAllianceRoleLabel() : roleLabel.Trim();
            _packetAllianceAuthority = new PacketAllianceAuthorityState(resolvedRole, canEditRanks, canEditNotice);
            return $"Alliance authority now follows packet-owned role {resolvedRole} (rank={FormatOnOff(canEditRanks)}, notice={FormatOnOff(canEditNotice)}).";
        }

        internal string GetEffectiveGuildRoleLabelForUi()
        {
            return GetEffectiveGuildRoleLabel();
        }

        internal bool CanManageGuildSkills()
        {
            if (!(ResolveEffectiveGuildMembership(null)))
            {
                return false;
            }

            string effectiveRole = GetEffectiveGuildRoleLabel();
            if (CanManageGuildByRole(effectiveRole))
            {
                return true;
            }

            return _packetGuildAuthority?.CanManageRanks == true ||
                   _packetGuildAuthority?.CanToggleAdmission == true ||
                   _packetGuildAuthority?.CanEditNotice == true;
        }

        private void SyncPacketGuildUiStateFromRoster(SocialListTab tab)
        {
            if (tab != SocialListTab.Guild || !IsPacketOwned(tab))
            {
                return;
            }

            SocialEntryState localGuildEntry = _entriesByTab[SocialListTab.Guild].FirstOrDefault(entry => entry.IsLocalPlayer);
            if (localGuildEntry == null)
            {
                _packetGuildUiState = new PacketGuildUiState(false, "No Guild", 0);
                return;
            }

            string guildName = string.IsNullOrWhiteSpace(localGuildEntry.SecondaryText)
                ? _guildName
                : localGuildEntry.SecondaryText.Trim();
            bool hasGuildMembership = GuildSkillRuntime.HasGuildMembership(guildName);
            _packetGuildUiState = new PacketGuildUiState(
                hasGuildMembership,
                hasGuildMembership ? guildName : "No Guild",
                _packetGuildUiState?.GuildLevel ?? 0);
        }

        private bool HasGuildAdministrativeAuthority()
        {
            return ResolveEffectiveGuildMembership(null)
                   && (CanManageGuildRanks()
                       || CanToggleGuildAdmission()
                       || CanEditGuildNotice());
        }

        private bool HasAllianceAdministrativeAuthority()
        {
            return CanEditAllianceRanks() || CanEditAllianceNotice();
        }

        private string GetHeaderTitle(SocialListTab tab)
        {
            return tab switch
            {
                SocialListTab.Friend => _friendOnlineOnly ? "Friends" : "Friend",
                SocialListTab.Party => "Party",
                SocialListTab.Guild => "Guild",
                SocialListTab.Alliance => "Alliance",
                SocialListTab.Blacklist => "Blacklist",
                _ => "Social"
            };
        }

        private string GetEffectiveGuildRoleLabel()
        {
            return _packetGuildAuthority?.RoleLabel ?? GetLocalGuildRoleLabel();
        }

        private bool CanManageGuildRanks()
        {
            return _packetGuildAuthority?.CanManageRanks ?? CanManageGuildByRole(GetLocalGuildRoleLabel());
        }

        private bool CanToggleGuildAdmission()
        {
            return _packetGuildAuthority?.CanToggleAdmission ?? CanManageGuildByRole(GetLocalGuildRoleLabel());
        }

        private bool CanEditGuildNotice()
        {
            return _packetGuildAuthority?.CanEditNotice ?? CanManageGuildByRole(GetLocalGuildRoleLabel());
        }

        private bool CanOpenGuildManageWindow()
        {
            return CanManageGuildRanks() || CanToggleGuildAdmission();
        }

        private string GetGuildAuthoritySummary()
        {
            return _packetGuildAuthority.HasValue
                ? $"Packet authority: role {GetEffectiveGuildRoleLabel()}, rank {FormatOnOff(CanManageGuildRanks())}, admission {FormatOnOff(CanToggleGuildAdmission())}, notice {FormatOnOff(CanEditGuildNotice())}"
                : $"Local authority: role {GetLocalGuildRoleLabel()}";
        }

        private bool CanEditAllianceRanks()
        {
            return _packetAllianceAuthority?.CanEditRanks ?? CanManageAllianceByRole(GetLocalAllianceRoleLabel());
        }

        private bool CanEditAllianceNotice()
        {
            return _packetAllianceAuthority?.CanEditNotice ?? CanManageAllianceByRole(GetLocalAllianceRoleLabel());
        }

        private string GetEffectiveAllianceRoleLabel()
        {
            return _packetAllianceAuthority?.RoleLabel ?? GetLocalAllianceRoleLabel();
        }

        private string GetAllianceAuthoritySummary()
        {
            return _packetAllianceAuthority.HasValue
                ? $"Packet authority: role {GetEffectiveAllianceRoleLabel()}, rank {FormatOnOff(CanEditAllianceRanks())}, notice {FormatOnOff(CanEditAllianceNotice())}"
                : $"Local authority: role {GetLocalAllianceRoleLabel()}";
        }

        private static bool CanManageGuildByRole(string role)
        {
            return string.Equals(role, "Master", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(role, "Jr. Master", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(role, "Jr Master", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(role, "Junior Master", StringComparison.OrdinalIgnoreCase);
        }

        private static bool CanManageAllianceByRole(string role)
        {
            return string.Equals(role, "Representative", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(role, "Leader", StringComparison.OrdinalIgnoreCase);
        }

        private static int ResolveGuildMarkComboIndex(int mark)
        {
            if (mark < 1000)
            {
                return 0;
            }

            return mark / 1000 switch
            {
                2 => 0,
                3 => 1,
                4 => 2,
                5 => 3,
                9 => 4,
                _ => 0
            };
        }

        private static string FormatOnOff(bool value)
        {
            return value ? "on" : "off";
        }

        private readonly record struct PacketGuildAuthorityState(
            string RoleLabel,
            bool CanManageRanks,
            bool CanToggleAdmission,
            bool CanEditNotice);

        private readonly record struct PacketGuildUiState(
            bool HasGuildMembership,
            string GuildName,
            int GuildLevel);

        private readonly record struct PacketAllianceAuthorityState(
            string RoleLabel,
            bool CanEditRanks,
            bool CanEditNotice);
    }
}
