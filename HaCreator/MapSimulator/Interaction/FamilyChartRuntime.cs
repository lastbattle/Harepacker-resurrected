using HaCreator.MapSimulator.Character;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class FamilyChartRuntime
    {
        private const int FamilyEntitlementCount = 5;
        private const int LocalPlayerId = 120;
        private const int DefaultFamilyHeadId = 100;
        private const string SeedFamilyName = "Starfall";
        private const int RemotePreviewMemberId = 900000;
        private const int DirectJuniorSlotLeft = 5;
        private const int DirectJuniorSlotRight = 6;
        private const int GrandchildSlotStart = 7;
        private const int CenterFocusSlotIndex = 3;
        private const int EntitlementDurationMs = 15 * 60 * 1000;
        // CUIFamily::Draw.
        private const int ClientFamilyNoSelectionStringId = 0x1200;
        private const int ClientFamilyTitleStringId = 0x1202;
        // CUIFamilyChart::Draw / _DrawChartItem.
        private const int ClientFamilyTreeNoSelectionStringId = 0x1200;
        private const int ClientFamilyTreeTitleStringId = 0x1202;
        private const int ClientFamilyTreeJuniorCountStringId = 0x1203;
        private const int ClientFamilyTreeGrandchildCountStringId = 0x1204;
        private const int ClientFamilyTreeEmptyBranchStringId = 0x11FD;
        private const int ClientFamilyTreeJuniorEntryStringId = 0x1201;

        private readonly Dictionary<int, FamilyMemberState> _members = new();
        private readonly Queue<FamilyRecruitSeed> _juniorSeeds = new();
        private readonly FamilyChartTextResources _textResources = FamilyChartTextResources.CreateDefault();
        private readonly List<FamilyTrackedMemberSnapshot> _trackedMembersBuffer = new();
        private int _trackedMembersCount;
        private readonly List<string> _preceptSuggestions = new()
        {
            "Travel as one family and always answer a summon.",
            "Train juniors before chasing new privileges.",
            "Keep one safe town registered before field testing.",
            "Donate reputation before claiming the weekly special."
        };

        private int _selectedMemberId = LocalPlayerId;
        private int _selectedEmptyTreeSlot = -1;
        private int _familyHeadId = DefaultFamilyHeadId;
        private int _preceptIndex;
        private FamilyEntitlementType _entitlementType = FamilyEntitlementType.DropAndExpBuff;
        private readonly Dictionary<FamilyEntitlementType, int> _entitlementUseCounts = new();
        private readonly Dictionary<FamilyEntitlementType, FamilyPrivilegeMetadata> _packetPrivilegeMetadata = new();
        private readonly Dictionary<int, int> _packetChartStatistics = new();
        private string _familyName = string.Empty;
        private string _familyPrecept = string.Empty;
        private string _locationSummary = "Maple Island";
        private string _remotePreviewRequestSummary;
        private FamilyPrivilegeState _activePrivilege;
        private FamilyAuthorityState _authorityState = FamilyAuthorityState.CreateSimulatorLocal();
        private FamilyInfoPacketSnapshot _lastInfoPacketSnapshot;
        private int? _packetChartJuniorLimit;
        private int? _packetChartLocalMemberId;
        private int? _packetChartHeaderMemberId;
        private bool? _packetChartIsMine;
        private bool _packetChartSuppressRootSlot;

        private int FamilyHeadId => _familyHeadId;

        internal FamilyChartRuntime()
        {
            SeedDefaultFamily();
        }

        internal int TrackedMembersCount => _trackedMembersCount;
        internal Action<string, int> SocialChatObserved { get; set; }

        internal void UpdateLocalContext(CharacterBuild build, string locationSummary, int channel)
        {
            _locationSummary = string.IsNullOrWhiteSpace(locationSummary) ? "Field" : locationSummary.Trim();
            FamilyMemberState localPlayer = GetMember(LocalPlayerId);
            if (localPlayer == null)
            {
                return;
            }

            localPlayer.Name = string.IsNullOrWhiteSpace(build?.Name) ? "Player" : build.Name.Trim();
            localPlayer.JobName = string.IsNullOrWhiteSpace(build?.JobName) ? "Beginner" : build.JobName.Trim();
            localPlayer.Level = Math.Max(1, build?.Level ?? 1);
            localPlayer.LocationSummary = $"{_locationSummary}  CH {Math.Max(1, channel)}";
            localPlayer.CurrentReputation = Math.Max(120, (localPlayer.Level * 18) + (CountDescendants(localPlayer.Id) * 25));
            localPlayer.TodayReputation = Math.Max(25, localPlayer.Level / 2);
            localPlayer.IsOnline = true;

            if (!_members.ContainsKey(_selectedMemberId))
            {
                _selectedMemberId = LocalPlayerId;
            }

            ValidateEmptyTreeSelection();
        }

        internal string PreviewRemoteFamilyRequest(string targetName, CharacterBuild build, string locationSummary, int channel)
        {
            string resolvedName = string.IsNullOrWhiteSpace(targetName) ? "Remote Character" : targetName.Trim();
            FamilyMemberState previewMember = new(
                RemotePreviewMemberId,
                resolvedName,
                string.IsNullOrWhiteSpace(build?.JobName) ? "Adventurer" : build.JobName.Trim(),
                Math.Max(1, build?.Level ?? 1),
                $"{(string.IsNullOrWhiteSpace(locationSummary) ? "Current map" : locationSummary.Trim())}  CH {Math.Max(1, channel)}",
                null,
                Math.Max(60, (build?.Level ?? 1) * 12),
                Math.Max(5, (build?.Level ?? 1) / 2),
                true,
                Vector2.Zero);
            _members[RemotePreviewMemberId] = previewMember;
            _selectedMemberId = RemotePreviewMemberId;
            _selectedEmptyTreeSlot = -1;
            _remotePreviewRequestSummary = $"Viewing a simulated family-chart request target for {resolvedName}. Server-owned roster sync still remains outside this seam.";
            return _remotePreviewRequestSummary;
        }

        internal void ClearRemotePreviewRequest()
        {
            _members.Remove(RemotePreviewMemberId);
            _remotePreviewRequestSummary = null;
            if (_selectedMemberId == RemotePreviewMemberId)
            {
                _selectedMemberId = LocalPlayerId;
            }

            _selectedEmptyTreeSlot = -1;
        }

        internal FamilyChartSnapshot BuildChartSnapshot()
        {
            FamilyMemberState selectedMember = GetSelectedMember();
            FamilyMemberState localPlayer = GetMember(LocalPlayerId) ?? selectedMember;
            FamilyPrivilegeState activePrivilege = GetActivePrivilege(Environment.TickCount);
            int entitlementPage = Math.Clamp((int)_entitlementType, 0, FamilyEntitlementCount - 1) + 1;
            int specialCost = GetSpecialReputationCost(localPlayer);
            int specialUseCount = GetEntitlementUseCount(_entitlementType);
            int specialUseLimit = GetEntitlementDailyLimit(_entitlementType);
            int currentReputation = _lastInfoPacketSnapshot?.CurrentReputation ?? localPlayer?.CurrentReputation ?? 0;
            int totalReputation = _lastInfoPacketSnapshot?.TotalReputation ?? GetTotalFamilyReputation();
            int todayReputation = _lastInfoPacketSnapshot?.TodayReputation ?? localPlayer?.TodayReputation ?? 0;
            int juniorCount = _lastInfoPacketSnapshot?.ChildCount ?? Math.Max(0, localPlayer?.Children.Count ?? 0);
            int juniorLimit = Math.Max(0, _lastInfoPacketSnapshot?.ChildLimit ?? 2);

            return new FamilyChartSnapshot
            {
                TitleText = BuildCompactTitle(),
                SelectedMemberId = localPlayer?.Id ?? LocalPlayerId,
                SelectedMemberName = localPlayer?.Name ?? "Player",
                SelectedRank = GetRankLabel(localPlayer),
                LocationSummary = localPlayer?.LocationSummary ?? _locationSummary,
                TotalMembers = _members.Count,
                JuniorCount = juniorCount,
                JuniorLimit = juniorLimit,
                CurrentReputation = currentReputation,
                TotalReputation = totalReputation,
                TodayReputation = todayReputation,
                SpecialReputationCost = specialCost,
                SpecialUseCount = specialUseCount,
                SpecialUseLimit = specialUseLimit,
                Precept = _familyPrecept,
                EntitlementLabel = GetEntitlementLabel(_entitlementType),
                EntitlementIndex = (int)_entitlementType,
                EntitlementDescription = BuildEntitlementDescription(localPlayer, activePrivilege),
                DetailLines = BuildDetailLines(localPlayer, activePrivilege),
                CanEditPrecept = CanEditPrecept(),
                CanPageBackward = FamilyEntitlementCount > 1,
                CanPageForward = FamilyEntitlementCount > 1,
                CanAddJunior = CanAddJunior(localPlayer) && juniorCount < juniorLimit,
                CanUseSpecial = CanUseCompactEntitlement(localPlayer, specialCost, specialUseCount, specialUseLimit),
                Page = entitlementPage,
                TotalPages = FamilyEntitlementCount
            };
        }

        internal FamilyTreeSnapshot BuildTreeSnapshot()
        {
            FamilyMemberState selectedMember = GetSelectedMember();
            IReadOnlyList<int> focusOrder = BuildFocusOrder();
            int focusIndex = GetFocusIndex(focusOrder, selectedMember?.Id ?? LocalPlayerId);
            Dictionary<int, int> slotMembers = BuildTreeLayout(selectedMember);
            ValidateEmptyTreeSelection(slotMembers);
            FamilyMemberState treeTitleMember = TryGetTreeMember(slotMembers, 0);

            FamilyTreeNodeSnapshot[] nodes = Enumerable.Range(0, 11)
                .Select(slotIndex => CreateNodeSnapshot(slotIndex, slotMembers))
                .ToArray();

            return new FamilyTreeSnapshot
            {
                Nodes = nodes,
                TotalMembers = _members.Count,
                FocusName = selectedMember?.Name ?? "Player",
                TitleText = BuildTreeTitle(treeTitleMember),
                JuniorCountText = BuildJuniorCountText(slotMembers),
                SummaryLines = BuildTreeSummaryLines(selectedMember),
                CanPageBackward = focusIndex > 0,
                CanPageForward = focusIndex < focusOrder.Count - 1,
                CanAddJunior = CanAddJunior(selectedMember),
                CanRemoveSelected = CanRemoveSelected(selectedMember),
                Page = focusIndex + 1,
                TotalPages = Math.Max(1, focusOrder.Count)
            };
        }

        internal IReadOnlyList<FamilyTrackedMemberSnapshot> BuildTrackedMembersSnapshot()
        {
            _trackedMembersCount = 0;
            foreach (FamilyMemberState member in _members.Values)
            {
                FamilyTrackedMemberSnapshot snapshot = GetOrCreateTrackedMemberSnapshot(_trackedMembersCount++);
                snapshot.Name = member.Name;
                snapshot.LocationSummary = member.LocationSummary;
                snapshot.IsOnline = member.IsOnline;
                snapshot.SimulatedPosition = member.SimulatedPosition;
                snapshot.IsLocalPlayer = member.Id == LocalPlayerId;
            }

            return _trackedMembersBuffer;
        }

        private FamilyTrackedMemberSnapshot GetOrCreateTrackedMemberSnapshot(int index)
        {
            while (_trackedMembersBuffer.Count <= index)
            {
                _trackedMembersBuffer.Add(new FamilyTrackedMemberSnapshot());
            }

            return _trackedMembersBuffer[index];
        }

        internal string MoveFocus(int delta)
        {
            IReadOnlyList<int> focusOrder = BuildFocusOrder();
            if (focusOrder.Count == 0)
            {
                return "Family chart has no selectable members.";
            }

            int currentIndex = GetFocusIndex(focusOrder, _selectedMemberId);
            int nextIndex = Math.Clamp(currentIndex + delta, 0, focusOrder.Count - 1);
            _selectedMemberId = focusOrder[nextIndex];
            _selectedEmptyTreeSlot = -1;

            FamilyMemberState selectedMember = GetSelectedMember();
            return $"Focused family branch on {selectedMember?.Name ?? "Player"}.";
        }

        internal string SelectMemberById(int memberId)
        {
            if (!_members.ContainsKey(memberId))
            {
                return $"Family member id {memberId} is not present in the current simulator roster.";
            }

            _selectedMemberId = memberId;
            _selectedEmptyTreeSlot = -1;
            FamilyMemberState selectedMember = GetSelectedMember();
            return $"Selected {selectedMember?.Name ?? "family member"} in the family tree.";
        }

        internal string SelectNode(int slotIndex)
        {
            FamilyMemberState selectedMember = GetSelectedMember();
            Dictionary<int, int> layout = BuildTreeLayout(selectedMember);
            if (!layout.TryGetValue(slotIndex, out int memberId) || !_members.ContainsKey(memberId))
            {
                _selectedEmptyTreeSlot = slotIndex;
                return slotIndex is DirectJuniorSlotLeft or DirectJuniorSlotRight
                    ? "Selected an empty junior-entry slot."
                    : "Selected an empty family-tree branch slot.";
            }

            _selectedMemberId = memberId;
            _selectedEmptyTreeSlot = -1;
            FamilyMemberState member = GetSelectedMember();
            return $"Selected {member?.Name ?? "family member"} in the family tree.";
        }

        internal string AddJunior()
        {
            FamilyMemberState selectedMember = GetSelectedMember();
            if (!CanRegisterJunior())
            {
                return BuildAuthorityBlockedMessage("register another junior");
            }

            if (!CanAddJunior(selectedMember))
            {
                return selectedMember == null
                    ? "Family chart has no selected member."
                    : $"{selectedMember.Name} cannot register another junior in the simulator roster.";
            }

            FamilyRecruitSeed recruit = _juniorSeeds.Count > 0
                ? _juniorSeeds.Dequeue()
                : new FamilyRecruitSeed($"Junior {_members.Count + 1}", "Adventurer", 18 + _members.Count, "Family Hall", true, new Vector2(180f + (_members.Count * 12f), -30f));
            int nextId = _members.Keys.Max() + 1;

            FamilyMemberState newJunior = new(
                nextId,
                recruit.Name,
                recruit.JobName,
                recruit.Level,
                recruit.LocationSummary,
                selectedMember.Id,
                65 + (selectedMember.Children.Count * 15),
                12 + selectedMember.Children.Count,
                recruit.IsOnline,
                recruit.SimulatedPosition);
            _members[newJunior.Id] = newJunior;
            selectedMember.Children.Add(newJunior.Id);
            selectedMember.CurrentReputation += 30;
            selectedMember.TodayReputation += 8;
            _selectedMemberId = newJunior.Id;
            _selectedEmptyTreeSlot = -1;

            string message = $"{newJunior.Name} was registered under {selectedMember.Name}.";
            NotifySocialChatObserved(message);
            return message;
        }

        internal string RemoveSelectedMember()
        {
            FamilyMemberState selectedMember = GetSelectedMember();
            if (!CanRemoveMembers())
            {
                return BuildAuthorityBlockedMessage("remove family members");
            }

            if (!CanRemoveSelected(selectedMember))
            {
                return selectedMember == null
                    ? "Family chart has no selected member."
                    : $"{selectedMember.Name} cannot be removed from the simulator family tree.";
            }

            List<int> branchMembers = new();
            CollectBranchMemberIds(selectedMember.Id, branchMembers);
            foreach (int memberId in branchMembers.OrderByDescending(GetDepth))
            {
                if (_members.TryGetValue(memberId, out FamilyMemberState member))
                {
                    if (member.ParentId.HasValue && _members.TryGetValue(member.ParentId.Value, out FamilyMemberState parent))
                    {
                        parent.Children.Remove(memberId);
                    }

                    _members.Remove(memberId);
                }
            }

            _selectedMemberId = LocalPlayerId;
            _selectedEmptyTreeSlot = -1;
            string message = $"Removed {selectedMember.Name}'s simulator family branch.";
            NotifySocialChatObserved(message);
            return message;
        }

        internal string CyclePrecept()
        {
            _preceptIndex = (_preceptIndex + 1) % _preceptSuggestions.Count;
            _familyPrecept = _preceptSuggestions[_preceptIndex];
            return $"Family precept updated to: {_familyPrecept}";
        }

        internal string SetPrecept(string precept)
        {
            if (!CanEditPrecept())
            {
                return BuildAuthorityBlockedMessage("edit the family precept");
            }

            return SetPreceptCore(precept, packetAuthored: false);
        }

        internal string SetPreceptFromPacket(string precept)
        {
            return SetPreceptCore(precept, packetAuthored: true);
        }

        internal string SetAuthorityProfileFromPacket(string profile)
        {
            FamilyAuthorityState authorityState = ResolveAuthorityProfile(profile, out string resolvedProfile);
            _authorityState = authorityState;
            return $"Family authority now follows the {resolvedProfile} profile ({authorityState.SourceLabel}).";
        }

        internal string SetAuthorityProfileFromPacket(IReadOnlyList<string> tokens)
        {
            if (tokens == null || tokens.Count == 0)
            {
                return SetAuthorityProfileFromPacket("local");
            }

            string profile = tokens[0];
            if (!IsCustomAuthorityProfile(profile))
            {
                return SetAuthorityProfileFromPacket(profile);
            }

            if (!TryParseAuthorityFlags(tokens.Skip(1), out FamilyAuthorityState authorityState, out string error))
            {
                return error;
            }

            _authorityState = authorityState;
            return $"Family authority now follows custom packet flags ({authorityState.SourceLabel}).";
        }

        internal bool TryApplyClientPacketPayload(int opcode, byte[] payload, out string message)
        {
            message = string.Empty;
            switch (opcode)
            {
                case 99:
                    message = ApplyInfoPacketPayload(payload);
                    return true;
                case 100:
                    return TryApplyResultPacketPayload(payload, out message);
                case 104:
                    message = ApplyPrivilegeListPacketPayload(payload);
                    return true;
                case 107:
                    message = ApplySetPrivilegePacketPayload(payload);
                    return true;
                case 98:
                    message = ApplyLocalChartPacketPayload(payload);
                    return true;
                default:
                    message = $"Family client packet opcode {opcode} is not modeled by this runtime.";
                    return false;
            }
        }

        internal string ApplyInfoPacketPayload(byte[] payload)
        {
            if (!FamilyPacketCodec.TryDecodeInfoPayload(payload, out FamilyInfoPacketSnapshot snapshot, out string error))
            {
                return error;
            }

            return ApplyInfoPacketSnapshot(snapshot);
        }

        internal string ApplyInfoPacketSnapshot(FamilyInfoPacketSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "Family info packet snapshot is empty.";
            }

            _lastInfoPacketSnapshot = snapshot;
            _familyName = string.IsNullOrWhiteSpace(snapshot.FamilyName) ? string.Empty : snapshot.FamilyName.Trim();
            _familyPrecept = string.IsNullOrWhiteSpace(snapshot.Precept) ? string.Empty : snapshot.Precept.Trim();
            _entitlementUseCounts.Clear();
            foreach (KeyValuePair<int, int> pair in snapshot.PrivilegeUses)
            {
                if (Enum.IsDefined(typeof(FamilyEntitlementType), pair.Key))
                {
                    _entitlementUseCounts[(FamilyEntitlementType)pair.Key] = Math.Max(0, pair.Value);
                }
            }

            FamilyMemberState localPlayer = GetMember(LocalPlayerId);
            if (localPlayer != null)
            {
                localPlayer.CurrentReputation = snapshot.CurrentReputation;
                localPlayer.TodayReputation = snapshot.TodayReputation;
            }

            if (snapshot.BossId > 0 && _members.ContainsKey(snapshot.BossId))
            {
                _familyHeadId = snapshot.BossId;
            }

            _authorityState = FamilyAuthorityState.CreatePacketInfo();
            NormalizeRosterState();

            string familyName = string.IsNullOrWhiteSpace(_familyName) ? "(unnamed)" : _familyName;
            NotifySocialMessagesObserved(_familyName, _familyPrecept);
            return $"Applied packet-authored family info for {familyName}: current/total reputation {snapshot.CurrentReputation:N0}/{snapshot.TotalReputation:N0}, today {snapshot.TodayReputation:+#,#;-#,#;0}, juniors {snapshot.ChildCount}/{snapshot.ChildLimit}, total juniors {snapshot.TotalChildCount}.";
        }

        internal string ApplyPrivilegeListPacketPayload(byte[] payload)
        {
            if (!FamilyPacketCodec.TryDecodePrivilegeListPayload(payload, out IReadOnlyList<FamilyPrivilegePacketSnapshot> privileges, out string error))
            {
                return error;
            }

            _packetPrivilegeMetadata.Clear();
            foreach (FamilyPrivilegePacketSnapshot privilege in privileges)
            {
                if (!TryResolveEntitlementType(privilege.Type, out FamilyEntitlementType entitlementType))
                {
                    continue;
                }

                _packetPrivilegeMetadata[entitlementType] = new FamilyPrivilegeMetadata(
                    entitlementType,
                    privilege.FameCost,
                    privilege.DayLimit,
                    privilege.Name,
                    privilege.Description);
            }

            return $"Applied packet-authored family privilege metadata for {_packetPrivilegeMetadata.Count} entitlement(s) through `CWvsContext::OnFamilyPrivilegeList` (opcode 104).";
        }

        internal string ApplyLocalChartPacketPayload(byte[] payload)
        {
            if (!FamilyPacketCodec.TryDecodeLocalChartPayload(payload, out FamilyLocalChartPacketSnapshot snapshot, out string error))
            {
                return error;
            }

            return ApplyLocalChartPacketSnapshot(snapshot);
        }

        internal string ApplySetPrivilegePacketPayload(byte[] payload)
        {
            if (!FamilyPacketCodec.TryDecodeSetPrivilegePayload(payload, out FamilyPrivilegeStatePacketSnapshot snapshot, out string error))
            {
                return error;
            }

            if (snapshot.Type == 0)
            {
                _activePrivilege = null;
                return "Cleared the packet-authored family privilege state through `CWvsContext::OnFamilySetPrivilege` (opcode 107).";
            }

            if (!TryResolveEntitlementType(snapshot.Type, out FamilyEntitlementType entitlementType))
            {
                return $"Family set-privilege packet type {snapshot.Type} is not mapped to a simulator entitlement.";
            }

            _activePrivilege = new FamilyPrivilegeState(
                entitlementType,
                ResolvePrivilegeExpiryTick(snapshot.EndTimeFileTimeUtc),
                snapshot.Index,
                snapshot.IncrementExpRate,
                snapshot.IncrementDropRate,
                "packet privilege state");
            return $"Applied packet-authored family privilege state for {GetEntitlementLabel(entitlementType)} (+{snapshot.IncrementExpRate}% EXP, +{snapshot.IncrementDropRate}% drop, index {snapshot.Index}) through `CWvsContext::OnFamilySetPrivilege` (opcode 107).";
        }

        internal string ApplyLocalChartPacketSnapshot(FamilyLocalChartPacketSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "Family local-chart packet snapshot is empty.";
            }

            FamilyMemberState previousLocalPlayer = GetMember(LocalPlayerId);
            _members.Clear();
            _packetChartStatistics.Clear();
            _entitlementUseCounts.Clear();
            _packetChartLocalMemberId = snapshot.FocusMemberId > 0 ? snapshot.FocusMemberId : null;
            _packetChartJuniorLimit = Math.Max(0, snapshot.JuniorLimit);
            _packetChartHeaderMemberId = snapshot.Members.Count > 0 && snapshot.Members[0].CharacterId > 0
                ? snapshot.Members[0].CharacterId
                : null;
            _packetChartIsMine = _packetChartLocalMemberId == LocalPlayerId;
            // CUIFamilyChart::DecodeLocalChart clears m_apItem[0] after decode when the packet has two or fewer entries.
            _packetChartSuppressRootSlot = snapshot.Members.Count <= 2;

            foreach (KeyValuePair<int, int> pair in snapshot.Statistics)
            {
                _packetChartStatistics[pair.Key] = Math.Max(0, pair.Value);
            }

            foreach (KeyValuePair<int, int> pair in snapshot.PrivilegeUses)
            {
                if (Enum.IsDefined(typeof(FamilyEntitlementType), pair.Key))
                {
                    _entitlementUseCounts[(FamilyEntitlementType)pair.Key] = Math.Max(0, pair.Value);
                }
            }

            foreach (FamilyLocalChartMemberPacketSnapshot packetMember in snapshot.Members)
            {
                if (packetMember.CharacterId <= 0)
                {
                    continue;
                }

                int? parentId = packetMember.ParentId > 0 ? packetMember.ParentId : null;
                if (parentId == packetMember.CharacterId)
                {
                    parentId = null;
                }

                _members[packetMember.CharacterId] = new FamilyMemberState(
                    packetMember.CharacterId,
                    string.IsNullOrWhiteSpace(packetMember.Name) ? $"Member {packetMember.CharacterId}" : packetMember.Name.Trim(),
                    ResolvePacketJobName(packetMember.JobId),
                    Math.Max(1, (int)packetMember.Level),
                    FormatPacketLocation(packetMember.ChannelId, packetMember.LoginMinutes),
                    parentId,
                    Math.Max(0, packetMember.FamousPoint),
                    Math.Max(0, packetMember.TodayParentPoint),
                    _packetChartLocalMemberId == packetMember.CharacterId
                        ? _packetChartIsMine == true
                        : packetMember.IsOnline,
                    Vector2.Zero);
            }

            if (_packetChartIsMine == true
                && !_members.ContainsKey(LocalPlayerId))
            {
                _members[LocalPlayerId] = CreatePacketLocalPlayerFallback(previousLocalPlayer);
            }

            foreach (FamilyMemberState member in _members.Values)
            {
                if (member.ParentId.HasValue
                    && _members.TryGetValue(member.ParentId.Value, out FamilyMemberState parent)
                    && !parent.Children.Contains(member.Id))
                {
                    parent.Children.Add(member.Id);
                }
            }

            if (_packetChartLocalMemberId.HasValue && _members.ContainsKey(_packetChartLocalMemberId.Value))
            {
                _selectedMemberId = _packetChartLocalMemberId.Value;
            }

            NormalizeRosterState();
            if (_packetChartLocalMemberId.HasValue && _members.TryGetValue(_packetChartLocalMemberId.Value, out FamilyMemberState localChartMember))
            {
                FamilyMemberState root = localChartMember;
                while (root.ParentId.HasValue && _members.TryGetValue(root.ParentId.Value, out FamilyMemberState parent))
                {
                    root = parent;
                }

                _familyHeadId = root.Id;
                if (_members.TryGetValue(_packetChartLocalMemberId.Value, out FamilyMemberState selected))
                {
                    _selectedMemberId = selected.Id;
                }
            }

            ValidateEmptyTreeSelection();
            return $"Applied packet-authored family local chart for member #{_packetChartLocalMemberId ?? 0}: {snapshot.Members.Count} member(s), {snapshot.Statistics.Count} statistic entrie(s), {snapshot.PrivilegeUses.Count} privilege-use entrie(s), junior limit {_packetChartJuniorLimit.GetValueOrDefault()} through `CUIFamilyChart::DecodeLocalChart` (opcode 98).";
        }

        private string SetPreceptCore(string precept, bool packetAuthored)
        {
            string resolvedPrecept = string.IsNullOrWhiteSpace(precept)
                ? string.Empty
                : precept.Trim();
            if (resolvedPrecept.Length > 200)
            {
                resolvedPrecept = resolvedPrecept[..200].TrimEnd();
            }

            _familyPrecept = resolvedPrecept;
            if (string.IsNullOrWhiteSpace(_familyPrecept))
            {
                return packetAuthored
                    ? "Cleared the packet-authored family precept."
                    : "Cleared the simulator family precept.";
            }

            NotifySocialChatObserved(_familyPrecept);
            return packetAuthored
                ? $"Set the packet-authored family precept to: {_familyPrecept}"
                : $"Set the simulator family precept to: {_familyPrecept}";
        }

        internal string CycleEntitlement()
        {
            _entitlementType = GetWrappedEntitlement(1);
            return $"Family entitlement switched to {GetEntitlementLabel(_entitlementType)}.";
        }

        internal string MoveEntitlementSelection(int delta)
        {
            _entitlementType = GetWrappedEntitlement(delta);
            return $"Family entitlement switched to {GetEntitlementLabel(_entitlementType)}.";
        }

        internal FamilyEntitlementUseResult ExecuteSelectedEntitlement(int currentTick, Vector2 localPlayerPosition)
        {
            return ExecuteSelectedEntitlement(currentTick, localPlayerPosition, null);
        }

        internal FamilyEntitlementUseResult ExecuteSelectedEntitlement(int currentTick, Vector2 localPlayerPosition, string targetName)
        {
            FamilyMemberState selectedMember = ResolveEntitlementTarget(targetName, out FamilyEntitlementUseResult? resolutionFailure);
            if (resolutionFailure.HasValue)
            {
                return resolutionFailure.Value;
            }

            FamilyMemberState localPlayer = GetMember(LocalPlayerId) ?? selectedMember;
            int specialCost = GetSpecialReputationCost(localPlayer);
            int specialUseCount = GetEntitlementUseCount(_entitlementType);
            int specialUseLimit = GetEntitlementDailyLimit(_entitlementType);
            if (!CanUsePrivileges())
            {
                return new FamilyEntitlementUseResult(BuildAuthorityBlockedMessage("use family privileges"));
            }

            if (!CanUseCompactEntitlement(localPlayer, specialCost, specialUseCount, specialUseLimit))
            {
                if (string.IsNullOrWhiteSpace(_familyName))
                {
                    return new FamilyEntitlementUseResult("Register a family name before using a family entitlement.");
                }

                if (specialUseCount >= specialUseLimit)
                {
                    return new FamilyEntitlementUseResult("That family entitlement has reached its daily limit for this simulator session.");
                }

                if ((localPlayer?.CurrentReputation ?? 0) < specialCost)
                {
                    return new FamilyEntitlementUseResult("There is not enough family reputation to use that entitlement.");
                }

                return new FamilyEntitlementUseResult("That family entitlement cannot be used right now.");
            }

            string localLocation = localPlayer?.LocationSummary ?? _locationSummary;
            string selectedLocation = selectedMember?.LocationSummary ?? localLocation;

            switch (_entitlementType)
            {
                case FamilyEntitlementType.MoveToMember:
                {
                    if (selectedMember == null || selectedMember.Id == LocalPlayerId)
                    {
                        return new FamilyEntitlementUseResult("Select another online family member before moving.");
                    }

                    if (!selectedMember.IsOnline)
                    {
                        return new FamilyEntitlementUseResult($"{selectedMember.Name} is offline, so the move entitlement cannot resolve.");
                    }

                    bool sameField = IsSameField(localLocation, selectedLocation);
                    if (!sameField && !CanResolveCrossMapPrivileges())
                    {
                        return new FamilyEntitlementUseResult($"Prepared a move request to {selectedMember.Name}, but cross-map transfer still remains outside the simulator field seam.");
                    }

                    if (!sameField && localPlayer != null)
                    {
                        localPlayer.LocationSummary = selectedLocation;
                    }

                    ConsumeEntitlementUse(localPlayer, specialCost);
                    NotifySocialChatObserved(selectedMember.Name);
                    return new FamilyEntitlementUseResult(
                        $"Moved to {selectedMember.Name}'s family-chart branch in {selectedLocation}.",
                        RequestTeleport: true,
                        TeleportPosition: selectedMember.SimulatedPosition);
                }
                case FamilyEntitlementType.SummonMember:
                {
                    if (selectedMember == null || selectedMember.Id == LocalPlayerId)
                    {
                        return new FamilyEntitlementUseResult("Select another family member before using the summon entitlement.");
                    }

                    selectedMember.LocationSummary = localLocation;
                    selectedMember.IsOnline = true;
                    selectedMember.SimulatedPosition = new Vector2(localPlayerPosition.X + 64f, localPlayerPosition.Y);
                    ConsumeEntitlementUse(localPlayer, specialCost);
                    string message = $"Summoned {selectedMember.Name} to {localLocation}.";
                    NotifySocialChatObserved(message);
                    return new FamilyEntitlementUseResult(message);
                }
                case FamilyEntitlementType.DropBuff:
                case FamilyEntitlementType.ExpBuff:
                case FamilyEntitlementType.DropAndExpBuff:
                    ConsumeEntitlementUse(localPlayer, specialCost);
                    _activePrivilege = new FamilyPrivilegeState(_entitlementType, currentTick + EntitlementDurationMs);
                    string privilegeMessage = $"Applied {GetEntitlementLabel(_entitlementType)} for the current simulator family session.";
                    NotifySocialChatObserved(privilegeMessage);
                    return new FamilyEntitlementUseResult(privilegeMessage);
                default:
                    return new FamilyEntitlementUseResult("That family entitlement is not modeled yet.");
            }
        }

        internal bool SelectedEntitlementRequiresTargetInput()
        {
            return _entitlementType is FamilyEntitlementType.MoveToMember or FamilyEntitlementType.SummonMember;
        }

        internal bool TryApplyResultPacketPayload(byte[] payload, out string message)
        {
            message = string.Empty;
            if (!FamilyPacketCodec.TryDecodeResultPayload(payload, out FamilyResultPacket packet, out string error))
            {
                message = error;
                return false;
            }

            if (!TryApplyResultPacket(packet, out message))
            {
                return false;
            }

            return true;
        }

        internal void SetSelectedEntitlement(FamilyEntitlementType entitlementType)
        {
            _entitlementType = entitlementType;
        }

        internal string DescribeStatus()
        {
            FamilyMemberState selectedMember = GetSelectedMember();
            FamilyMemberState head = GetMember(_familyHeadId);
            string headName = head?.Name ?? "(missing)";
            string selectedName = selectedMember?.Name ?? "(none)";
            string familyName = string.IsNullOrWhiteSpace(_familyName) ? "(unset)" : _familyName;
            int useCount = GetEntitlementUseCount(_entitlementType);
            int useLimit = GetEntitlementDailyLimit(_entitlementType);
            string precept = string.IsNullOrWhiteSpace(_familyPrecept) ? "(unset)" : _familyPrecept;
            string crossMapResolution = CanResolveCrossMapPrivileges() ? "enabled" : "local-only";
            string activePrivilege = _activePrivilege == null
                ? "inactive"
                : $"{GetEntitlementLabel(_activePrivilege.Type)} ({_activePrivilege.SourceLabel})";
            return $"Family roster: {_members.Count} members, family {familyName}, precept {precept}, head {headName} (#{_familyHeadId}), selected {selectedName} (#{selectedMember?.Id ?? 0}), entitlement {useCount}/{useLimit} uses on {GetEntitlementLabel(_entitlementType)}, packet privilege entries {_packetPrivilegeMetadata.Count}, active privilege {activePrivilege}, authority {_authorityState.SourceLabel}, cross-map privilege resolution {crossMapResolution}.";
        }

        internal string ResetToSeedFamily()
        {
            ResetRuntimeState();
            SeedDefaultFamily();
            return "Restored the seeded simulator family roster.";
        }

        internal string ClearRosterFromPacket()
        {
            ResetRuntimeState();
            return "Cleared the simulator family roster. Sync packet members to rebuild it.";
        }

        internal string SetFamilyNameFromPacket(string familyName)
        {
            _familyName = string.IsNullOrWhiteSpace(familyName) ? string.Empty : familyName.Trim();
            NotifySocialChatObserved(_familyName);
            return string.IsNullOrWhiteSpace(_familyName)
                ? "Cleared the packet-authored family name."
                : $"Set the packet-authored family name to {_familyName}.";
        }

        internal string RemoveMemberFromPacket(int memberId)
        {
            if (!_members.TryGetValue(memberId, out FamilyMemberState selectedMember))
            {
                return $"Family member id {memberId} is not present in the current simulator roster.";
            }

            List<int> branchMembers = new();
            CollectBranchMemberIds(memberId, branchMembers);
            foreach (int branchMemberId in branchMembers.OrderByDescending(GetDepth))
            {
                if (!_members.TryGetValue(branchMemberId, out FamilyMemberState member))
                {
                    continue;
                }

                DetachFromParent(member);
                _members.Remove(branchMemberId);
            }

            NormalizeRosterState();
            string message = $"Removed synced family branch rooted at {selectedMember.Name} (#{memberId}).";
            NotifySocialChatObserved(message);
            return message;
        }

        internal string UpsertMemberFromPacket(
            int memberId,
            int? parentId,
            string name,
            string jobName,
            int level,
            string locationSummary,
            bool isOnline,
            int currentReputation,
            int todayReputation)
        {
            if (memberId <= 0)
            {
                return "Packet family sync requires a positive member id.";
            }

            if (parentId == memberId)
            {
                return $"Family member #{memberId} cannot parent itself.";
            }

            if (parentId.HasValue && !_members.ContainsKey(parentId.Value))
            {
                return $"Add parent #{parentId.Value} before syncing child #{memberId}.";
            }

            bool isHead = !parentId.HasValue;
            FamilyMemberState member = GetMember(memberId);
            int? previousParentId = member?.ParentId;
            if (member == null)
            {
                member = new FamilyMemberState(
                    memberId,
                    string.IsNullOrWhiteSpace(name) ? $"Member {memberId}" : name.Trim(),
                    string.IsNullOrWhiteSpace(jobName) ? "Adventurer" : jobName.Trim(),
                    Math.Max(1, level),
                    string.IsNullOrWhiteSpace(locationSummary) ? "Family Hall" : locationSummary.Trim(),
                    parentId,
                    Math.Max(0, currentReputation),
                    Math.Max(0, todayReputation),
                    isOnline,
                    Vector2.Zero);
                _members[memberId] = member;
            }
            else
            {
                member.Name = string.IsNullOrWhiteSpace(name) ? member.Name : name.Trim();
                member.JobName = string.IsNullOrWhiteSpace(jobName) ? member.JobName : jobName.Trim();
                member.Level = Math.Max(1, level);
                member.LocationSummary = string.IsNullOrWhiteSpace(locationSummary) ? member.LocationSummary : locationSummary.Trim();
                member.ParentId = parentId;
                member.CurrentReputation = Math.Max(0, currentReputation);
                member.TodayReputation = Math.Max(0, todayReputation);
                member.IsOnline = isOnline;
            }

            if (previousParentId != parentId)
            {
                DetachFromParent(memberId, previousParentId);
            }

            if (parentId.HasValue && _members.TryGetValue(parentId.Value, out FamilyMemberState parent)
                && !parent.Children.Contains(memberId))
            {
                parent.Children.Add(memberId);
            }

            if (isHead)
            {
                _familyHeadId = memberId;
            }

            NormalizeRosterState();
            NotifySocialChatObserved(member.Name);
            return $"Synced family member #{memberId} {member.Name} under {(parentId.HasValue ? $"#{parentId.Value}" : "the family head root")}.";
        }

        private void NotifySocialChatObserved(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            SocialChatObserved?.Invoke(message.Trim(), Environment.TickCount);
        }

        private void NotifySocialMessagesObserved(params string[] messages)
        {
            if (messages == null)
            {
                return;
            }

            HashSet<string> observed = null;
            foreach (string message in messages)
            {
                string normalized = message?.Trim();
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                observed ??= new HashSet<string>(StringComparer.Ordinal);
                if (!observed.Add(normalized))
                {
                    continue;
                }

                SocialChatObserved?.Invoke(normalized, Environment.TickCount);
            }
        }

        private void ResetRuntimeState()
        {
            _members.Clear();
            _juniorSeeds.Clear();
            _selectedMemberId = LocalPlayerId;
            _familyHeadId = DefaultFamilyHeadId;
            _preceptIndex = 0;
            _entitlementType = FamilyEntitlementType.DropAndExpBuff;
            _entitlementUseCounts.Clear();
            _packetPrivilegeMetadata.Clear();
            _packetChartStatistics.Clear();
            _familyName = string.Empty;
            _familyPrecept = string.Empty;
            _activePrivilege = null;
            _selectedEmptyTreeSlot = -1;
            _authorityState = FamilyAuthorityState.CreateSimulatorLocal();
            _lastInfoPacketSnapshot = null;
            _packetChartJuniorLimit = null;
            _packetChartLocalMemberId = null;
            _packetChartHeaderMemberId = null;
            _packetChartIsMine = null;
            _packetChartSuppressRootSlot = false;
        }

        private void SeedDefaultFamily()
        {
            _familyName = SeedFamilyName;
            _familyPrecept = _preceptSuggestions[0];
            AddMember(new FamilyMemberState(100, "Ephenia", "Bishop", 126, "Orbis  CH 8", null, 540, 42, true, new Vector2(260f, -20f)));
            AddMember(new FamilyMemberState(110, "Cassia", "Paladin", 94, "Leafre  CH 6", 100, 360, 21, true, new Vector2(180f, 10f)));
            AddMember(new FamilyMemberState(111, "Rowan", "Ranger", 90, "Mu Lung  CH 4", 100, 288, 16, false, new Vector2(340f, 10f)));
            AddMember(new FamilyMemberState(LocalPlayerId, "Player", "Beginner", 30, "Maple Island  CH 1", 110, 198, 15, true, new Vector2(205f, 70f)));
            AddMember(new FamilyMemberState(121, "Targa", "F/P Mage", 52, "Ludibrium  CH 11", 110, 140, 7, true, new Vector2(348f, 70f)));
            AddMember(new FamilyMemberState(130, "Puck", "Assassin", 31, "Kerning City  CH 3", LocalPlayerId, 82, 6, true, new Vector2(92f, 128f)));
            AddMember(new FamilyMemberState(131, "Nina", "Cleric", 29, "Henesys  CH 2", LocalPlayerId, 78, 5, true, new Vector2(330f, 128f)));
            AddMember(new FamilyMemberState(150, "Basil", "Page", 18, "Ellinia  CH 5", 130, 40, 3, true, new Vector2(61f, 182f)));
            AddMember(new FamilyMemberState(151, "Rhea", "Hunter", 17, "Sleepywood  CH 9", 130, 36, 2, false, new Vector2(211f, 182f)));
            AddMember(new FamilyMemberState(152, "Dory", "Spearman", 19, "Perion  CH 10", 131, 42, 4, true, new Vector2(342f, 182f)));
            AddMember(new FamilyMemberState(153, "Seth", "Wizard", 16, "Kerning Square  CH 7", 131, 32, 2, true, new Vector2(490f, 182f)));

            _juniorSeeds.Enqueue(new FamilyRecruitSeed("Mira", "Bandit", 27, "Nautilus  CH 13", true, new Vector2(330f, 128f)));
            _juniorSeeds.Enqueue(new FamilyRecruitSeed("Vale", "Priest", 41, "Ariant  CH 2", true, new Vector2(92f, 128f)));
            _juniorSeeds.Enqueue(new FamilyRecruitSeed("Iris", "Brawler", 38, "Singapore  CH 1", false, new Vector2(211f, 182f)));
        }

        private void AddMember(FamilyMemberState member)
        {
            _members[member.Id] = member;
            if (!member.ParentId.HasValue)
            {
                _familyHeadId = member.Id;
            }

            if (member.ParentId.HasValue && _members.TryGetValue(member.ParentId.Value, out FamilyMemberState parent))
            {
                if (!parent.Children.Contains(member.Id))
                {
                    parent.Children.Add(member.Id);
                }
            }
        }

        private FamilyMemberState GetSelectedMember()
        {
            return GetMember(_selectedMemberId) ?? GetMember(LocalPlayerId) ?? GetMember(FamilyHeadId);
        }

        private FamilyMemberState GetMember(int id)
        {
            return _members.TryGetValue(id, out FamilyMemberState member) ? member : null;
        }

        private IReadOnlyList<int> BuildFocusOrder()
        {
            Queue<int> pending = new();
            List<int> ordered = new();
            if (_members.ContainsKey(_familyHeadId))
            {
                pending.Enqueue(_familyHeadId);
            }

            while (pending.Count > 0)
            {
                int memberId = pending.Dequeue();
                if (!_members.ContainsKey(memberId))
                {
                    continue;
                }

                ordered.Add(memberId);
                foreach (int childId in GetChildren(memberId))
                {
                    pending.Enqueue(childId);
                }
            }

            return ordered;
        }

        private static int GetFocusIndex(IReadOnlyList<int> focusOrder, int memberId)
        {
            for (int i = 0; i < focusOrder.Count; i++)
            {
                if (focusOrder[i] == memberId)
                {
                    return i;
                }
            }

            return 0;
        }

        private Dictionary<int, int> BuildTreeLayout(FamilyMemberState focus)
        {
            Dictionary<int, int> layout = new();
            FamilyMemberState head = GetMember(_familyHeadId);
            if (!_packetChartSuppressRootSlot)
            {
                FamilyMemberState headerMember = _packetChartHeaderMemberId.HasValue
                    ? GetMember(_packetChartHeaderMemberId.Value)
                    : head;
                if (headerMember != null)
                {
                    layout[0] = headerMember.Id;
                }
            }

            if (focus == null)
            {
                return layout;
            }

            IReadOnlyList<int> upperPath = BuildUpperPathMemberIds(focus);
            if (upperPath.Count > 0)
            {
                layout[1] = upperPath[0];
            }

            if (upperPath.Count > 1)
            {
                layout[2] = upperPath[1];
            }

            layout[3] = focus.Id;

            FamilyMemberState parent = focus.ParentId.HasValue ? GetMember(focus.ParentId.Value) : null;
            FamilyMemberState sibling = parent == null
                ? GetChildren(_familyHeadId).Select(GetMember).FirstOrDefault(member => member != null && member.Id != focus.Id)
                : GetChildren(parent.Id).Select(GetMember).FirstOrDefault(member => member != null && member.Id != focus.Id);
            if (sibling != null)
            {
                layout[4] = sibling.Id;
            }

            List<FamilyMemberState> children = GetChildren(focus.Id).Select(GetMember).Where(member => member != null).ToList();
            if (children.Count > 0)
            {
                layout[5] = children[0].Id;
            }

            if (children.Count > 1)
            {
                layout[6] = children[1].Id;
            }

            LayoutGrandchildren(layout, children.ElementAtOrDefault(0), 7, 8);
            LayoutGrandchildren(layout, children.ElementAtOrDefault(1), 9, 10);

            return layout;
        }

        private IReadOnlyList<int> BuildUpperPathMemberIds(FamilyMemberState focus)
        {
            if (focus == null)
            {
                return Array.Empty<int>();
            }

            List<int> path = new();
            FamilyMemberState current = focus.ParentId.HasValue ? GetMember(focus.ParentId.Value) : null;
            while (current != null && current.Id != _familyHeadId)
            {
                path.Add(current.Id);
                current = current.ParentId.HasValue ? GetMember(current.ParentId.Value) : null;
            }

            if (path.Count == 0)
            {
                return Array.Empty<int>();
            }

            path.Reverse();
            if (path.Count <= 2)
            {
                return path;
            }

            return path.Skip(path.Count - 2).ToArray();
        }

        private void LayoutGrandchildren(Dictionary<int, int> layout, FamilyMemberState member, int leftSlot, int rightSlot)
        {
            if (member == null)
            {
                return;
            }

            List<FamilyMemberState> grandchildren = GetChildren(member.Id).Select(GetMember).Where(child => child != null).ToList();
            if (grandchildren.Count > 0)
            {
                layout[leftSlot] = grandchildren[0].Id;
            }

            if (grandchildren.Count > 1)
            {
                layout[rightSlot] = grandchildren[1].Id;
            }
        }

        private FamilyTreeNodeSnapshot CreateNodeSnapshot(int slotIndex, Dictionary<int, int> slotMembers)
        {
            if (!slotMembers.TryGetValue(slotIndex, out int memberId) || !_members.TryGetValue(memberId, out FamilyMemberState member))
            {
                return new FamilyTreeNodeSnapshot
                {
                    SlotIndex = slotIndex,
                    IsSelected = slotIndex == _selectedEmptyTreeSlot,
                    PlaceholderText = slotIndex is DirectJuniorSlotLeft or DirectJuniorSlotRight
                        ? GetClientPlaceholderText(slotIndex)
                        : slotIndex == 3
                            ? string.Empty
                            : GetClientPlaceholderText(slotIndex),
                    StatisticText = slotIndex >= GrandchildSlotStart
                        ? _textResources.FormatGrandchildCount(0)
                        : string.Empty
                };
            }

            return new FamilyTreeNodeSnapshot
            {
                SlotIndex = slotIndex,
                MemberId = member.Id,
                Name = member.Name,
                Rank = GetRankLabel(member),
                Detail = $"Lv.{member.Level} {member.JobName}",
                StatisticText = slotIndex >= GrandchildSlotStart
                    ? _textResources.FormatGrandchildCount(GetStatisticValue(member))
                    : string.Empty,
                IsLeader = member.Id == _familyHeadId,
                IsLocalPlayer = member.Id == LocalPlayerId,
                IsSelected = member.Id == _selectedMemberId,
                IsOnline = member.IsOnline,
                UseAlertNameColor = ShouldUseAlertNameColor(slotIndex, member)
            };
        }

        private IReadOnlyList<int> GetChildren(int memberId)
        {
            FamilyMemberState member = GetMember(memberId);
            return member != null ? member.Children : Array.Empty<int>();
        }

        private int GetTopPathChildId(int memberId)
        {
            FamilyMemberState current = GetMember(memberId);
            FamilyMemberState previous = current;
            while (current?.ParentId.HasValue == true)
            {
                previous = current;
                current = GetMember(current.ParentId.Value);
                if (current?.Id == _familyHeadId)
                {
                    return previous.Id;
                }
            }

            return memberId;
        }

        private IReadOnlyList<string> BuildDetailLines(FamilyMemberState selectedMember, FamilyPrivilegeState activePrivilege)
        {
            if (selectedMember == null)
            {
                return Array.Empty<string>();
            }

            List<string> lines = new()
            {
                $"Focus: {selectedMember.Name} ({GetRankLabel(selectedMember)}).",
                $"{selectedMember.Level} {selectedMember.JobName} at {selectedMember.LocationSummary}.",
                $"{GetStatisticValue(selectedMember)} direct juniors, {CountDescendants(selectedMember.Id)} total descendants."
            };

            if (!string.IsNullOrWhiteSpace(_familyPrecept))
            {
                lines.Add($"Precept: {_familyPrecept}");
            }

            lines.Add($"Authority: {_authorityState.SourceLabel}.");
            lines.Add(CanResolveCrossMapPrivileges()
                ? "Cross-map family privilege resolution is enabled in this runtime profile."
                : "Cross-map family privilege resolution remains local-field only.");

            if (activePrivilege != null)
            {
                TimeSpan remaining = TimeSpan.FromMilliseconds(Math.Max(0, activePrivilege.ExpiresAtTick - Environment.TickCount));
                string activeSummary = $"{GetEntitlementLabel(activePrivilege.Type)} active for {Math.Max(0, remaining.Minutes):00}:{Math.Max(0, remaining.Seconds):00}.";
                if (activePrivilege.IncrementExpRate > 0 || activePrivilege.IncrementDropRate > 0)
                {
                    activeSummary = $"{activeSummary} +{activePrivilege.IncrementExpRate}% EXP, +{activePrivilege.IncrementDropRate}% drop.";
                }

                lines.Add(activeSummary);
            }

            if (TryGetPrivilegeMetadata(_entitlementType, out FamilyPrivilegeMetadata metadata))
            {
                lines.Add($"Packet privilege metadata: fame {metadata.FameCost}, limit {metadata.DayLimit}.");
            }

            if (selectedMember.Id == RemotePreviewMemberId && !string.IsNullOrWhiteSpace(_remotePreviewRequestSummary))
            {
                lines.Add(_remotePreviewRequestSummary);
            }

            return lines;
        }

        private IReadOnlyList<string> BuildTreeSummaryLines(FamilyMemberState selectedMember)
        {
            if (selectedMember == null)
            {
                return Array.Empty<string>();
            }

            return new[]
            {
                $"{selectedMember.Name}  {GetRankLabel(selectedMember)}",
                $"{selectedMember.LocationSummary}",
                $"{GetStatisticValue(selectedMember)} direct juniors, {CountDescendants(selectedMember.Id)} total descendants"
            };
        }

        private bool CanAddJunior(FamilyMemberState selectedMember)
        {
            if (!CanRegisterJunior() || selectedMember == null || selectedMember.Id == RemotePreviewMemberId)
            {
                return false;
            }

            if (_packetChartLocalMemberId.HasValue)
            {
                return _packetChartIsMine == true
                    && selectedMember.Id == _packetChartLocalMemberId.Value
                    && selectedMember.Children.Count < Math.Max(0, _packetChartJuniorLimit ?? 0);
            }

            return selectedMember.Children.Count < 2;
        }

        private bool CanRemoveSelected(FamilyMemberState selectedMember)
        {
            if (!CanRemoveMembers() || selectedMember == null || selectedMember.Id == FamilyHeadId || selectedMember.Id == LocalPlayerId)
            {
                return false;
            }

            if (_packetChartLocalMemberId.HasValue && _packetChartIsMine != true)
            {
                return false;
            }

            List<int> branchMembers = new();
            CollectBranchMemberIds(selectedMember.Id, branchMembers);
            return !branchMembers.Contains(LocalPlayerId);
        }

        private bool CanExecuteEntitlement(FamilyMemberState selectedMember)
        {
            return selectedMember != null
                && selectedMember.Id != RemotePreviewMemberId
                && (selectedMember.Id == LocalPlayerId || selectedMember.IsOnline);
        }

        private FamilyMemberState ResolveEntitlementTarget(string targetName, out FamilyEntitlementUseResult? failureResult)
        {
            failureResult = null;
            if (!SelectedEntitlementRequiresTargetInput() || string.IsNullOrWhiteSpace(targetName))
            {
                return GetSelectedMember();
            }

            string normalizedTargetName = targetName.Trim();
            FamilyMemberState resolvedMember = _members.Values.FirstOrDefault(member =>
                !string.IsNullOrWhiteSpace(member.Name) &&
                string.Equals(member.Name.Trim(), normalizedTargetName, StringComparison.OrdinalIgnoreCase));
            if (resolvedMember == null)
            {
                failureResult = new FamilyEntitlementUseResult(
                    $"Family member `{normalizedTargetName}` is not present in the current simulator roster.");
                return null;
            }

            _selectedMemberId = resolvedMember.Id;
            _selectedEmptyTreeSlot = -1;
            return resolvedMember;
        }

        private bool CanUseCompactEntitlement(FamilyMemberState selectedMember, int specialCost, int specialUseCount, int specialUseLimit)
        {
            return CanUsePrivileges()
                && selectedMember != null
                && !string.IsNullOrWhiteSpace(_familyName)
                && specialUseCount < specialUseLimit
                && selectedMember.CurrentReputation >= specialCost;
        }

        private bool CanEditPrecept()
        {
            return _authorityState.CanEditPrecept;
        }

        private bool CanRegisterJunior()
        {
            return _authorityState.CanRegisterJunior;
        }

        private bool CanRemoveMembers()
        {
            return _authorityState.CanRemoveMembers;
        }

        private bool CanUsePrivileges()
        {
            return _authorityState.CanUsePrivileges;
        }

        private bool CanResolveCrossMapPrivileges()
        {
            return _authorityState.CanResolveCrossMapPrivileges;
        }

        private string BuildAuthorityBlockedMessage(string action)
        {
            return $"Family authority does not permit this client seam to {action}. Current profile: {_authorityState.SourceLabel}.";
        }

        private bool TryApplyResultPacket(FamilyResultPacket packet, out string message)
        {
            message = packet.Type switch
            {
                1 => FormatFamilyResultMessage(
                    0x121B,
                    "Family result {0} completed.",
                    GetSelectedMember()?.Name ?? "member"),
                64 => ResolveFamilyResultNotice(0x1206, packet.Type),
                65 => ResolveFamilyResultNotice(0x1205, packet.Type),
                66 => ResolveFamilyResultNotice(0x1207, packet.Type),
                67 => ResolveFamilyResultNotice(0x1208, packet.Type),
                69 => ResolveFamilyResultNotice(0x120A, packet.Type),
                70 => ResolveFamilyResultNotice(0x120B, packet.Type),
                71 => ResolveFamilyResultNotice(0x120C, packet.Type),
                72 => ResolveFamilyResultNotice(0x120D, packet.Type),
                73 => ResolveFamilyResultNotice(0x120F, packet.Type),
                74 => ResolveFamilyResultNotice(0x1210, packet.Type),
                75 => ResolveFamilyResultNotice(0x1212, packet.Type),
                76 => ResolveFamilyResultNotice(0x1213, packet.Type),
                77 => ResolveFamilyResultNotice(0x120E, packet.Type),
                78 => ResolveFamilyResultNotice(0x13AD, packet.Type),
                79 => ResolveFamilyResultNotice(0x13AC, packet.Type),
                80 => FormatFamilyResultMessage(0x1484, "Family result {0}: {1}", packet.Type, packet.Value),
                81 => FormatFamilyResultMessage(0x1485, "Family result {0}: {1}", packet.Type, packet.Value),
                82 => string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} {1}",
                    ResolveFamilyResultNotice(0x155A, packet.Type),
                    ResolveFamilyResultNotice(0x13AD, packet.Type)),
                _ => null
            };

            if (string.IsNullOrWhiteSpace(message))
            {
                message = $"Family result packet type {packet.Type} is not modeled yet.";
                return false;
            }

            return true;
        }

        private static string ResolveFamilyResultNotice(int stringPoolId, int resultType)
        {
            return MapleStoryStringPool.GetOrFallback(
                stringPoolId,
                $"Family result {resultType}",
                appendFallbackSuffix: true,
                minimumHexWidth: 4);
        }

        private static string FormatFamilyResultMessage(int stringPoolId, string fallbackFormat, params object[] arguments)
        {
            string compositeFormat = MapleStoryStringPool.GetCompositeFormatOrFallback(
                stringPoolId,
                fallbackFormat,
                arguments?.Length ?? 0,
                out _);
            return string.Format(CultureInfo.InvariantCulture, compositeFormat, arguments ?? Array.Empty<object>());
        }

        private void CollectBranchMemberIds(int memberId, List<int> results)
        {
            results.Add(memberId);
            foreach (int childId in GetChildren(memberId))
            {
                CollectBranchMemberIds(childId, results);
            }
        }

        private int GetSpecialReputationCost(FamilyMemberState selectedMember)
        {
            if (TryGetPrivilegeMetadata(_entitlementType, out FamilyPrivilegeMetadata metadata))
            {
                return Math.Max(0, metadata.FameCost);
            }

            int baseCost = _entitlementType switch
            {
                FamilyEntitlementType.MoveToMember => 300,
                FamilyEntitlementType.SummonMember => 500,
                FamilyEntitlementType.DropBuff => 700,
                FamilyEntitlementType.ExpBuff => 700,
                _ => 900
            };

            return Math.Max(baseCost, baseCost + (GetStatisticValue(selectedMember) * 25));
        }

        private int GetEntitlementUseCount(FamilyEntitlementType entitlementType)
        {
            return _entitlementUseCounts.TryGetValue(entitlementType, out int useCount)
                ? Math.Max(0, useCount)
                : 0;
        }

        private int GetEntitlementDailyLimit(FamilyEntitlementType entitlementType)
        {
            if (TryGetPrivilegeMetadata(entitlementType, out FamilyPrivilegeMetadata metadata))
            {
                return Math.Max(0, metadata.DayLimit);
            }

            return entitlementType switch
            {
                FamilyEntitlementType.MoveToMember => 3,
                FamilyEntitlementType.SummonMember => 3,
                FamilyEntitlementType.DropBuff => 2,
                FamilyEntitlementType.ExpBuff => 2,
                _ => 1
            };
        }

        private int GetTotalFamilyReputation()
        {
            return _members.Values.Sum(member => Math.Max(0, member.CurrentReputation));
        }

        private void ConsumeEntitlementUse(FamilyMemberState localPlayer, int specialCost)
        {
            if (localPlayer != null)
            {
                localPlayer.CurrentReputation = Math.Max(0, localPlayer.CurrentReputation - Math.Max(0, specialCost));
            }

            _entitlementUseCounts[_entitlementType] = GetEntitlementUseCount(_entitlementType) + 1;
        }

        private int CountDescendants(int memberId)
        {
            int count = 0;
            foreach (int childId in GetChildren(memberId))
            {
                count += 1 + CountDescendants(childId);
            }

            return count;
        }

        private int GetDepth(int memberId)
        {
            int depth = 0;
            FamilyMemberState member = GetMember(memberId);
            while (member?.ParentId.HasValue == true)
            {
                depth++;
                member = GetMember(member.ParentId.Value);
            }

            return depth;
        }

        private int GetStatisticValue(FamilyMemberState member)
        {
            if (member == null)
            {
                return TryGetPacketChartStatistic(-1, out int totalStatistic)
                    ? totalStatistic
                    : _members.Count;
            }

            if (TryGetPacketChartStatistic(member.Id, out int statistic))
            {
                return statistic;
            }

            return member.Id == _familyHeadId
                ? _members.Count - 1
                : member.Children.Count;
        }

        private bool ShouldUseAlertNameColor(int slotIndex, FamilyMemberState member)
        {
            if (slotIndex < DirectJuniorSlotLeft || member?.ParentId is null)
            {
                return false;
            }

            FamilyMemberState parent = GetMember(member.ParentId.Value);
            return parent != null && member.Level > parent.Level;
        }

        private FamilyPrivilegeState GetActivePrivilege(int currentTick)
        {
            if (_activePrivilege != null && currentTick >= _activePrivilege.ExpiresAtTick)
            {
                _activePrivilege = null;
            }

            return _activePrivilege;
        }

        private string GetRankLabel(FamilyMemberState member)
        {
            if (member == null)
            {
                return "Member";
            }

            if (member.Id == _familyHeadId)
            {
                return "Leader";
            }

            return member.Children.Count > 0 ? "Senior" : "Junior";
        }

        private string BuildTreeTitle(FamilyMemberState titleMember)
        {
            return titleMember == null
                ? _textResources.NoSelectionTitle
                : _textResources.FormatTitle(titleMember.Name);
        }

        private string BuildEntitlementDescription(FamilyMemberState localPlayer, FamilyPrivilegeState activePrivilege)
        {
            if (!CanUsePrivileges())
            {
                return $"Packet/session family authority has not enabled privilege use. Current profile: {_authorityState.SourceLabel}.";
            }

            string description = ResolvePacketPrivilegeDescription(_entitlementType);
            if (string.IsNullOrWhiteSpace(description))
            {
                description = _entitlementType switch
                {
                    FamilyEntitlementType.MoveToMember => "Move to the selected family member's location when both members share the current field seam.",
                    FamilyEntitlementType.SummonMember => "Summon the selected online family member into the current field seam beside the local player.",
                    FamilyEntitlementType.DropBuff => "Apply the simulator's family drop-rate support buff for the current family session.",
                    FamilyEntitlementType.ExpBuff => "Apply the simulator's family EXP support buff for the current family session.",
                    _ => "Apply the simulator's combined family drop-rate and EXP support buff for the current family session."
                };
            }

            if (activePrivilege?.Type == _entitlementType)
            {
                TimeSpan remaining = TimeSpan.FromMilliseconds(Math.Max(0, activePrivilege.ExpiresAtTick - Environment.TickCount));
                string modifiers = string.Empty;
                if (activePrivilege.IncrementExpRate > 0 || activePrivilege.IncrementDropRate > 0)
                {
                    modifiers = $" (+{activePrivilege.IncrementExpRate}% EXP, +{activePrivilege.IncrementDropRate}% drop)";
                }

                return $"{description} Active for {Math.Max(0, remaining.Minutes):00}:{Math.Max(0, remaining.Seconds):00}{modifiers}.";
            }

            int remainingUses = Math.Max(0, GetEntitlementDailyLimit(_entitlementType) - GetEntitlementUseCount(_entitlementType));
            return $"{description} {remainingUses} use(s) remain in this simulator session.";
        }

        private bool TryGetPrivilegeMetadata(FamilyEntitlementType entitlementType, out FamilyPrivilegeMetadata metadata)
        {
            return _packetPrivilegeMetadata.TryGetValue(entitlementType, out metadata);
        }

        private string ResolvePacketPrivilegeDescription(FamilyEntitlementType entitlementType)
        {
            return TryGetPrivilegeMetadata(entitlementType, out FamilyPrivilegeMetadata metadata)
                && !string.IsNullOrWhiteSpace(metadata.Description)
                ? metadata.Description
                : string.Empty;
        }

        private static bool TryResolveEntitlementType(byte rawType, out FamilyEntitlementType entitlementType)
        {
            if (Enum.IsDefined(typeof(FamilyEntitlementType), (int)rawType))
            {
                entitlementType = (FamilyEntitlementType)rawType;
                return true;
            }

            entitlementType = default;
            return false;
        }

        private static int ResolvePrivilegeExpiryTick(long endTimeFileTimeUtc)
        {
            if (endTimeFileTimeUtc <= 0)
            {
                return Environment.TickCount;
            }

            try
            {
                DateTime endUtc = DateTime.FromFileTimeUtc(endTimeFileTimeUtc);
                double remainingMilliseconds = Math.Max(0d, (endUtc - DateTime.UtcNow).TotalMilliseconds);
                return Environment.TickCount + (int)Math.Min(int.MaxValue, remainingMilliseconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                return Environment.TickCount;
            }
        }

        private string BuildCompactTitle()
        {
            return string.IsNullOrWhiteSpace(_familyName)
                ? _textResources.NoSelectionTitle
                : _textResources.FormatTitle(_familyName);
        }

        private string BuildJuniorCountText(Dictionary<int, int> slotMembers)
        {
            int juniorCount = TryGetPacketChartStatistic(0, out int packetJuniorCount)
                ? Math.Max(0, packetJuniorCount)
                : Math.Max(0, GetStatisticValue(TryGetTreeMember(slotMembers, 0)));
            if (slotMembers.ContainsKey(1))
            {
                juniorCount--;
            }

            if (slotMembers.ContainsKey(2))
            {
                juniorCount--;
            }

            return _textResources.FormatJuniorCount(Math.Max(0, juniorCount));
        }

        private string GetClientPlaceholderText(int slotIndex)
        {
            return slotIndex is DirectJuniorSlotLeft or DirectJuniorSlotRight
                ? _textResources.JuniorEntryPlaceholder
                : _textResources.EmptyBranchPlaceholder;
        }

        private FamilyMemberState TryGetTreeMember(Dictionary<int, int> slotMembers, int slotIndex)
        {
            return slotMembers.TryGetValue(slotIndex, out int memberId)
                ? GetMember(memberId)
                : null;
        }

        private bool TryGetPacketChartStatistic(int key, out int value)
        {
            if (_packetChartStatistics.TryGetValue(key, out value))
            {
                value = Math.Max(0, value);
                return true;
            }

            return false;
        }

        private static string ResolvePacketJobName(short jobId)
        {
            int normalizedJobId = Math.Abs(jobId);
            return normalizedJobId switch
            {
                0 => "Beginner",
                >= 100 and < 200 => "Warrior",
                >= 200 and < 300 => "Magician",
                >= 300 and < 400 => "Bowman",
                >= 400 and < 500 => "Thief",
                >= 500 and < 600 => "Pirate",
                >= 1000 and < 2000 => "Noblesse",
                >= 2000 and < 3000 => "Legend",
                >= 3000 and < 4000 => "Citizen",
                _ => $"Job {jobId}"
            };
        }

        private FamilyMemberState CreatePacketLocalPlayerFallback(FamilyMemberState previousLocalPlayer)
        {
            return new FamilyMemberState(
                LocalPlayerId,
                string.IsNullOrWhiteSpace(previousLocalPlayer?.Name) ? "Player" : previousLocalPlayer.Name,
                string.IsNullOrWhiteSpace(previousLocalPlayer?.JobName) ? "Beginner" : previousLocalPlayer.JobName,
                Math.Max(1, previousLocalPlayer?.Level ?? 1),
                string.IsNullOrWhiteSpace(previousLocalPlayer?.LocationSummary) ? _locationSummary : previousLocalPlayer.LocationSummary,
                null,
                Math.Max(0, previousLocalPlayer?.CurrentReputation ?? 0),
                Math.Max(0, previousLocalPlayer?.TodayReputation ?? 0),
                true,
                previousLocalPlayer?.SimulatedPosition ?? Vector2.Zero);
        }

        private static string FormatPacketLocation(int channelId, int loginMinutes)
        {
            string channel = channelId > 0
                ? $"CH {channelId}"
                : "offline";
            return loginMinutes > 0
                ? $"Family session  {channel}  {loginMinutes} min"
                : $"Family session  {channel}";
        }

        private void NormalizeRosterState()
        {
            if (_members.Count == 0)
            {
                _selectedMemberId = LocalPlayerId;
                _familyHeadId = DefaultFamilyHeadId;
                _entitlementUseCounts.Clear();
                _packetChartStatistics.Clear();
                _packetChartJuniorLimit = null;
                _packetChartLocalMemberId = null;
                _packetChartHeaderMemberId = null;
                _packetChartIsMine = null;
                return;
            }

            if (!_members.ContainsKey(_familyHeadId))
            {
                FamilyMemberState head = _members.Values
                    .FirstOrDefault(member => !member.ParentId.HasValue)
                    ?? _members.Values.OrderBy(member => member.Id).First();
                _familyHeadId = head.Id;
                head.ParentId = null;
            }

            foreach (FamilyMemberState member in _members.Values)
            {
                member.Children.RemoveAll(childId => !_members.ContainsKey(childId));
            }

            if (!_members.ContainsKey(_selectedMemberId))
            {
                _selectedMemberId = _members.ContainsKey(LocalPlayerId) ? LocalPlayerId : _familyHeadId;
            }

            ValidateEmptyTreeSelection();
        }

        private void ValidateEmptyTreeSelection(Dictionary<int, int> layout = null)
        {
            if (_selectedEmptyTreeSlot < 0)
            {
                return;
            }

            Dictionary<int, int> resolvedLayout = layout ?? BuildTreeLayout(GetSelectedMember());
            if (_selectedEmptyTreeSlot >= 11
                || _selectedEmptyTreeSlot == CenterFocusSlotIndex
                || resolvedLayout.ContainsKey(_selectedEmptyTreeSlot))
            {
                _selectedEmptyTreeSlot = -1;
            }
        }

        private void DetachFromParent(FamilyMemberState member)
        {
            DetachFromParent(member?.Id ?? 0, member?.ParentId);
        }

        private void DetachFromParent(int memberId, int? parentId)
        {
            if (!parentId.HasValue || !_members.TryGetValue(parentId.Value, out FamilyMemberState parent))
            {
                return;
            }

            parent.Children.Remove(memberId);
        }

        private static bool IsSameField(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            int leftChannelSeparator = left.IndexOf("  CH ", StringComparison.OrdinalIgnoreCase);
            int rightChannelSeparator = right.IndexOf("  CH ", StringComparison.OrdinalIgnoreCase);
            string leftField = leftChannelSeparator >= 0 ? left[..leftChannelSeparator] : left;
            string rightField = rightChannelSeparator >= 0 ? right[..rightChannelSeparator] : right;
            return string.Equals(leftField.Trim(), rightField.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string GetEntitlementLabel(FamilyEntitlementType entitlementType)
        {
            return entitlementType switch
            {
                FamilyEntitlementType.MoveToMember => "Move to the location of another character",
                FamilyEntitlementType.SummonMember => "Summon another character to my location",
                FamilyEntitlementType.DropBuff => "Drop Rate Buff",
                FamilyEntitlementType.ExpBuff => "EXP Buff",
                _ => "Drop Rate, EXP Buff"
            };
        }

        private FamilyEntitlementType GetWrappedEntitlement(int delta)
        {
            int nextIndex = ((int)_entitlementType + delta) % FamilyEntitlementCount;
            if (nextIndex < 0)
            {
                nextIndex += FamilyEntitlementCount;
            }

            return (FamilyEntitlementType)nextIndex;
        }

        private static FamilyAuthorityState ResolveAuthorityProfile(string profile, out string resolvedProfile)
        {
            switch ((profile ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "session":
                case "remote":
                    resolvedProfile = "session";
                    return FamilyAuthorityState.CreatePacketSession();
                case "readonly":
                case "read":
                    resolvedProfile = "readonly";
                    return FamilyAuthorityState.CreateReadOnlyPacket();
                case "privilege":
                case "privilegeonly":
                    resolvedProfile = "privilegeonly";
                    return FamilyAuthorityState.CreatePrivilegeOnlyPacket();
                case "manage":
                case "manageonly":
                    resolvedProfile = "manageonly";
                    return FamilyAuthorityState.CreateManageOnlyPacket();
                case "local":
                case "simulator":
                default:
                    resolvedProfile = "local";
                    return FamilyAuthorityState.CreateSimulatorLocal();
            }
        }

        private static bool IsCustomAuthorityProfile(string profile)
        {
            string normalized = (profile ?? string.Empty).Trim().ToLowerInvariant();
            return normalized is "flags" or "custom" or "mask";
        }

        private static bool TryParseAuthorityFlags(
            IEnumerable<string> tokens,
            out FamilyAuthorityState authorityState,
            out string error)
        {
            bool canEditPrecept = false;
            bool canRegisterJunior = false;
            bool canRemoveMembers = false;
            bool canUsePrivileges = false;
            bool canResolveCrossMapPrivileges = false;
            bool hasAnyFlag = false;

            foreach (string token in tokens ?? Array.Empty<string>())
            {
                string normalizedToken = (token ?? string.Empty).Trim();
                if (normalizedToken.Length == 0)
                {
                    continue;
                }

                int separator = normalizedToken.IndexOf('=');
                if (separator <= 0 || separator == normalizedToken.Length - 1)
                {
                    authorityState = null;
                    error = "Usage: /family packet authority flags precept=<0|1> junior=<0|1> remove=<0|1> privilege=<0|1> crossmap=<0|1>";
                    return false;
                }

                string key = normalizedToken[..separator].Trim().ToLowerInvariant();
                string valueText = normalizedToken[(separator + 1)..].Trim();
                if (!TryParseAuthorityFlagValue(valueText, out bool value))
                {
                    authorityState = null;
                    error = $"Family authority flag `{key}` must be 0/1, true/false, yes/no, or on/off.";
                    return false;
                }

                hasAnyFlag = true;
                switch (key)
                {
                    case "precept":
                    case "editprecept":
                    case "edit":
                        canEditPrecept = value;
                        break;
                    case "junior":
                    case "registerjunior":
                    case "register":
                        canRegisterJunior = value;
                        break;
                    case "remove":
                    case "removemembers":
                    case "bye":
                        canRemoveMembers = value;
                        break;
                    case "privilege":
                    case "privileges":
                    case "useprivileges":
                        canUsePrivileges = value;
                        break;
                    case "crossmap":
                    case "crossmapprivileges":
                    case "transfer":
                        canResolveCrossMapPrivileges = value;
                        break;
                    default:
                        authorityState = null;
                        error = $"Family authority flag `{key}` is not recognized.";
                        return false;
                }
            }

            if (!hasAnyFlag)
            {
                authorityState = null;
                error = "Usage: /family packet authority flags precept=<0|1> junior=<0|1> remove=<0|1> privilege=<0|1> crossmap=<0|1>";
                return false;
            }

            authorityState = FamilyAuthorityState.CreateCustomPacket(
                canEditPrecept,
                canRegisterJunior,
                canRemoveMembers,
                canUsePrivileges,
                canResolveCrossMapPrivileges);
            error = string.Empty;
            return true;
        }

        private static bool TryParseAuthorityFlagValue(string valueText, out bool value)
        {
            switch ((valueText ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "1":
                case "true":
                case "yes":
                case "on":
                case "enabled":
                    value = true;
                    return true;
                case "0":
                case "false":
                case "no":
                case "off":
                case "disabled":
                    value = false;
                    return true;
                default:
                    value = false;
                    return false;
            }
        }

        private sealed class FamilyMemberState
        {
            public FamilyMemberState(
                int id,
                string name,
                string jobName,
                int level,
                string locationSummary,
                int? parentId,
                int currentReputation,
                int todayReputation,
                bool isOnline,
                Vector2 simulatedPosition)
            {
                Id = id;
                Name = name;
                JobName = jobName;
                Level = level;
                LocationSummary = locationSummary;
                ParentId = parentId;
                CurrentReputation = currentReputation;
                TodayReputation = todayReputation;
                IsOnline = isOnline;
                SimulatedPosition = simulatedPosition;
            }

            public int Id { get; }
            public string Name { get; set; }
            public string JobName { get; set; }
            public int Level { get; set; }
            public string LocationSummary { get; set; }
            public int? ParentId { get; set; }
            public int CurrentReputation { get; set; }
            public int TodayReputation { get; set; }
            public bool IsOnline { get; set; }
            public Vector2 SimulatedPosition { get; set; }
            public List<int> Children { get; } = new();
        }

        private sealed class FamilyPrivilegeState
        {
            public FamilyPrivilegeState(
                FamilyEntitlementType type,
                int expiresAtTick,
                int index = 0,
                int incrementExpRate = 0,
                int incrementDropRate = 0,
                string sourceLabel = "local entitlement")
            {
                Type = type;
                ExpiresAtTick = expiresAtTick;
                Index = index;
                IncrementExpRate = incrementExpRate;
                IncrementDropRate = incrementDropRate;
                SourceLabel = string.IsNullOrWhiteSpace(sourceLabel) ? "local entitlement" : sourceLabel;
            }

            public FamilyEntitlementType Type { get; }
            public int ExpiresAtTick { get; }
            public int Index { get; }
            public int IncrementExpRate { get; }
            public int IncrementDropRate { get; }
            public string SourceLabel { get; }
        }

        private sealed class FamilyPrivilegeMetadata
        {
            public FamilyPrivilegeMetadata(
                FamilyEntitlementType type,
                int fameCost,
                int dayLimit,
                string name,
                string description)
            {
                Type = type;
                FameCost = Math.Max(0, fameCost);
                DayLimit = Math.Max(0, dayLimit);
                Name = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
                Description = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
            }

            public FamilyEntitlementType Type { get; }
            public int FameCost { get; }
            public int DayLimit { get; }
            public string Name { get; }
            public string Description { get; }
        }

        private sealed class FamilyAuthorityState
        {
            private FamilyAuthorityState(
                string sourceLabel,
                bool canEditPrecept,
                bool canRegisterJunior,
                bool canRemoveMembers,
                bool canUsePrivileges,
                bool canResolveCrossMapPrivileges)
            {
                SourceLabel = sourceLabel;
                CanEditPrecept = canEditPrecept;
                CanRegisterJunior = canRegisterJunior;
                CanRemoveMembers = canRemoveMembers;
                CanUsePrivileges = canUsePrivileges;
                CanResolveCrossMapPrivileges = canResolveCrossMapPrivileges;
            }

            public string SourceLabel { get; }
            public bool CanEditPrecept { get; }
            public bool CanRegisterJunior { get; }
            public bool CanRemoveMembers { get; }
            public bool CanUsePrivileges { get; }
            public bool CanResolveCrossMapPrivileges { get; }

            public static FamilyAuthorityState CreateSimulatorLocal() => new(
                "simulator-local",
                canEditPrecept: true,
                canRegisterJunior: true,
                canRemoveMembers: true,
                canUsePrivileges: true,
                canResolveCrossMapPrivileges: false);

            public static FamilyAuthorityState CreatePacketSession() => new(
                "packet/session-backed",
                canEditPrecept: true,
                canRegisterJunior: true,
                canRemoveMembers: true,
                canUsePrivileges: true,
                canResolveCrossMapPrivileges: true);

            public static FamilyAuthorityState CreatePacketInfo() => new(
                "packet family-info",
                canEditPrecept: true,
                canRegisterJunior: true,
                canRemoveMembers: false,
                canUsePrivileges: true,
                canResolveCrossMapPrivileges: false);

            public static FamilyAuthorityState CreateReadOnlyPacket() => new(
                "packet read-only",
                canEditPrecept: false,
                canRegisterJunior: false,
                canRemoveMembers: false,
                canUsePrivileges: false,
                canResolveCrossMapPrivileges: false);

            public static FamilyAuthorityState CreatePrivilegeOnlyPacket() => new(
                "packet privilege-only",
                canEditPrecept: false,
                canRegisterJunior: false,
                canRemoveMembers: false,
                canUsePrivileges: true,
                canResolveCrossMapPrivileges: true);

            public static FamilyAuthorityState CreateManageOnlyPacket() => new(
                "packet management-only",
                canEditPrecept: true,
                canRegisterJunior: true,
                canRemoveMembers: true,
                canUsePrivileges: false,
                canResolveCrossMapPrivileges: false);

            public static FamilyAuthorityState CreateCustomPacket(
                bool canEditPrecept,
                bool canRegisterJunior,
                bool canRemoveMembers,
                bool canUsePrivileges,
                bool canResolveCrossMapPrivileges) => new(
                    $"packet custom flags precept={(canEditPrecept ? 1 : 0)}, junior={(canRegisterJunior ? 1 : 0)}, remove={(canRemoveMembers ? 1 : 0)}, privilege={(canUsePrivileges ? 1 : 0)}, crossmap={(canResolveCrossMapPrivileges ? 1 : 0)}",
                    canEditPrecept,
                    canRegisterJunior,
                    canRemoveMembers,
                    canUsePrivileges,
                    canResolveCrossMapPrivileges);
        }

        private readonly record struct FamilyRecruitSeed(
            string Name,
            string JobName,
            int Level,
            string LocationSummary,
            bool IsOnline,
            Vector2 SimulatedPosition);
    }

    internal readonly record struct FamilyEntitlementUseResult(
        string Message,
        bool RequestTeleport = false,
        Vector2 TeleportPosition = default);

    internal sealed class FamilyChartSnapshot
    {
        public string TitleText { get; init; } = string.Empty;
        public int SelectedMemberId { get; init; }
        public string SelectedMemberName { get; init; } = "Player";
        public string SelectedRank { get; init; } = "Junior";
        public string LocationSummary { get; init; } = string.Empty;
        public int TotalMembers { get; init; }
        public int JuniorCount { get; init; }
        public int JuniorLimit { get; init; } = 2;
        public int CurrentReputation { get; init; }
        public int TotalReputation { get; init; }
        public int TodayReputation { get; init; }
        public int SpecialReputationCost { get; init; }
        public int SpecialUseCount { get; init; }
        public int SpecialUseLimit { get; init; }
        public string Precept { get; init; } = string.Empty;
        public string EntitlementLabel { get; init; } = string.Empty;
        public string EntitlementDescription { get; init; } = string.Empty;
        public int EntitlementIndex { get; init; }
        public IReadOnlyList<string> DetailLines { get; init; } = Array.Empty<string>();
        public bool CanEditPrecept { get; init; }
        public bool CanPageBackward { get; init; }
        public bool CanPageForward { get; init; }
        public bool CanAddJunior { get; init; }
        public bool CanUseSpecial { get; init; }
        public int Page { get; init; }
        public int TotalPages { get; init; } = 1;
    }

    internal sealed class FamilyTreeSnapshot
    {
        public IReadOnlyList<FamilyTreeNodeSnapshot> Nodes { get; init; } = Array.Empty<FamilyTreeNodeSnapshot>();
        public int TotalMembers { get; init; }
        public string FocusName { get; init; } = string.Empty;
        public string TitleText { get; init; } = string.Empty;
        public string JuniorCountText { get; init; } = string.Empty;
        public IReadOnlyList<string> SummaryLines { get; init; } = Array.Empty<string>();
        public bool CanPageBackward { get; init; }
        public bool CanPageForward { get; init; }
        public bool CanAddJunior { get; init; }
        public bool CanRemoveSelected { get; init; }
        public int Page { get; init; }
        public int TotalPages { get; init; } = 1;
    }

    internal sealed class FamilyTreeNodeSnapshot
    {
        public int SlotIndex { get; init; }
        public int MemberId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Rank { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
        public string StatisticText { get; init; } = string.Empty;
        public string PlaceholderText { get; init; } = string.Empty;
        public bool IsLeader { get; init; }
        public bool IsLocalPlayer { get; init; }
        public bool IsSelected { get; init; }
        public bool IsOnline { get; init; }
        public bool UseAlertNameColor { get; init; }
    }

    internal sealed class FamilyTrackedMemberSnapshot
    {
        public string Name { get; set; } = string.Empty;
        public string LocationSummary { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public Vector2 SimulatedPosition { get; set; }
        public bool IsLocalPlayer { get; set; }
    }

    internal sealed class FamilyChartTextResources
    {
        internal const int EmptyBranchPlaceholderStringPoolId = 0x11FD;
        internal const int NoSelectionTitleStringPoolId = 0x1200;
        internal const int JuniorEntryPlaceholderStringPoolId = 0x1201;
        internal const int SharedTitleFormatStringPoolId = 0x1202;
        internal const int SeniorCountFormatStringPoolId = 0x1203;
        internal const int JuniorCountFormatStringPoolId = 0x1204;

        public string NoSelectionTitle { get; init; } = string.Empty;
        public string SharedTitleFormat { get; init; } = "{0}";
        public string JuniorEntryPlaceholder { get; init; } = string.Empty;
        public string EmptyBranchPlaceholder { get; init; } = string.Empty;
        public string JuniorCountFormat { get; init; } = string.Empty;
        public string GrandchildCountFormat { get; init; } = string.Empty;

        public static FamilyChartTextResources CreateDefault()
        {
            return new FamilyChartTextResources
            {
                NoSelectionTitle = MapleStoryStringPool.GetOrFallback(
                    NoSelectionTitleStringPoolId,
                    "Family"),
                SharedTitleFormat = MapleStoryStringPool.GetCompositeFormatOrFallback(
                    SharedTitleFormatStringPoolId,
                    "{0}",
                    1,
                    out _),
                JuniorEntryPlaceholder = MapleStoryStringPool.GetOrFallback(
                    JuniorEntryPlaceholderStringPoolId,
                    "Junior Entry"),
                EmptyBranchPlaceholder = MapleStoryStringPool.GetOrFallback(
                    EmptyBranchPlaceholderStringPoolId,
                    "Empty Branch"),
                JuniorCountFormat = MapleStoryStringPool.GetCompositeFormatOrFallback(
                    SeniorCountFormatStringPoolId,
                    "{0} junior member(s)",
                    1,
                    out _),
                GrandchildCountFormat = MapleStoryStringPool.GetCompositeFormatOrFallback(
                    JuniorCountFormatStringPoolId,
                    "{0} grandchild member(s)",
                    1,
                    out _)
            };
        }

        public string FormatTitle(string titleValue)
        {
            return string.IsNullOrWhiteSpace(titleValue)
                ? NoSelectionTitle
                : string.Format(SharedTitleFormat, titleValue.Trim());
        }

        public string FormatJuniorCount(int juniorCount)
        {
            return string.Format(JuniorCountFormat, Math.Max(0, juniorCount));
        }

        public string FormatGrandchildCount(int grandchildCount)
        {
            return string.Format(GrandchildCountFormat, Math.Max(0, grandchildCount));
        }
    }

    internal enum FamilyEntitlementType
    {
        MoveToMember = 0,
        SummonMember = 1,
        DropBuff = 2,
        ExpBuff = 3,
        DropAndExpBuff = 4
    }
}
