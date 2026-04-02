using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum ExpeditionNoticeKind
    {
        Joined = 0,
        Left = 1,
        Removed = 2
    }

    internal enum ExpeditionRemovalKind
    {
        Leave = 0,
        Disband = 1,
        Removed = 2
    }

    internal readonly record struct ExpeditionMemberSeed(
        string Name,
        string RoleLabel,
        int Level,
        string LocationSummary,
        int Channel,
        bool IsOnline,
        bool IsLocalPlayer);

    internal readonly record struct ExpeditionPartySeed(
        int PartyIndex,
        IReadOnlyList<ExpeditionMemberSeed> Members);

    internal sealed partial class SocialListRuntime
    {
        private readonly ExpeditionIntermediaryState _expeditionIntermediary = new();

        internal string DescribeExpeditionStatus()
        {
            if (!_expeditionIntermediary.HasActiveExpedition && _expeditionIntermediary.PendingInvite == null)
            {
                return _expeditionIntermediary.LastStatusMessage;
            }

            string owner = _expeditionIntermediary.PacketOwned ? "packet-owned" : "local";
            string roster = _expeditionIntermediary.HasActiveExpedition
                ? $"{_expeditionIntermediary.ExpeditionTitle} with {_expeditionIntermediary.MemberCount} member(s) across {_expeditionIntermediary.ActivePartyCount} party slot(s); master={_expeditionIntermediary.MasterName} (party {_expeditionIntermediary.MasterPartyIndex + 1})."
                : "No expedition roster is active.";
            string invite = _expeditionIntermediary.PendingInvite == null
                ? "No invite is pending."
                : $"Pending invite from {_expeditionIntermediary.PendingInvite.InviterName} (Lv. {_expeditionIntermediary.PendingInvite.Level}, job {_expeditionIntermediary.PendingInvite.JobCode}).";
            string request = string.IsNullOrWhiteSpace(_expeditionIntermediary.PendingRequestSummary)
                ? "No expedition request is staged."
                : _expeditionIntermediary.PendingRequestSummary;
            return $"Expedition intermediary {owner}. {roster} {invite} {request} {_expeditionIntermediary.LastStatusMessage}".Trim();
        }

        internal string ClearExpeditionIntermediary(bool packetOwned)
        {
            return packetOwned
                ? ApplyExpeditionRemovedInternal(ExpeditionRemovalKind.Disband, packetOwned: true, "Packet clear removed the expedition intermediary roster.")
                : ResetLocalExpeditionIntermediary();
        }

        internal string ApplyExpeditionGet(
            string expeditionTitle,
            int masterPartyIndex,
            IReadOnlyList<ExpeditionPartySeed> parties,
            bool packetOwned,
            int retCode = 59)
        {
            string resolvedTitle = string.IsNullOrWhiteSpace(expeditionTitle) ? $"{_playerName}'s Expedition" : expeditionTitle.Trim();
            _expeditionIntermediary.PacketOwned = packetOwned;
            _expeditionIntermediary.ExpeditionTitle = resolvedTitle;
            _expeditionIntermediary.MasterPartyIndex = Math.Max(0, masterPartyIndex);
            _expeditionIntermediary.Parties.Clear();

            IReadOnlyList<ExpeditionPartySeed> resolvedParties = parties == null || parties.Count == 0
                ? CreateDefaultExpeditionSeedParties(resolvedTitle)
                : parties;
            foreach (ExpeditionPartySeed party in resolvedParties.OrderBy(entry => entry.PartyIndex))
            {
                ReplaceExpeditionParty(party.PartyIndex, party.Members);
            }

            _expeditionAdmissionActive = true;
            _expeditionIntermediary.PendingInvite = null;
            _expeditionIntermediary.PendingRequestSummary = string.Empty;
            _expeditionIntermediary.LastPacketRetCode = retCode;
            _expeditionIntermediary.LastStatusMessage = retCode switch
            {
                59 => "Expedition intermediary synchronized a packet-owned roster snapshot.",
                61 => "Expedition intermediary refreshed the roster after an acceptance branch.",
                _ => "Expedition intermediary synchronized the roster."
            };
            SyncExpeditionSearchEntryFromIntermediary();
            ClampSearchSelection(SocialSearchTab.Expedition);
            return _expeditionIntermediary.LastStatusMessage;
        }

        internal string ApplyExpeditionModified(
            int partyIndex,
            IReadOnlyList<ExpeditionMemberSeed> members,
            int? masterPartyIndex,
            bool packetOwned,
            int retCode = 70)
        {
            _expeditionIntermediary.PacketOwned = packetOwned;
            EnsureExpeditionIntermediarySeeded();

            if (masterPartyIndex.HasValue)
            {
                _expeditionIntermediary.MasterPartyIndex = Math.Max(0, masterPartyIndex.Value);
            }

            IReadOnlyList<ExpeditionMemberSeed> resolvedMembers = members ?? Array.Empty<ExpeditionMemberSeed>();
            if (resolvedMembers.Count == 0)
            {
                RemoveExpeditionPartyAt(partyIndex);
                ShiftExpeditionPartiesAfterRemoval(partyIndex);
                _expeditionIntermediary.LastStatusMessage = $"Expedition intermediary removed party {partyIndex + 1} from the active roster.";
            }
            else
            {
                ReplaceExpeditionParty(partyIndex, resolvedMembers);
                _expeditionIntermediary.LastStatusMessage = $"Expedition intermediary refreshed party {partyIndex + 1} with {resolvedMembers.Count} member(s).";
            }

            _expeditionAdmissionActive = _expeditionIntermediary.HasActiveExpedition || _expeditionAdmissionActive;
            _expeditionIntermediary.PendingRequestSummary = string.Empty;
            _expeditionIntermediary.LastPacketRetCode = retCode;
            SyncExpeditionSearchEntryFromIntermediary();
            ClampSearchSelection(SocialSearchTab.Expedition);
            return _expeditionIntermediary.LastStatusMessage;
        }

        internal string ApplyExpeditionInvite(string inviterName, int level, int jobCode, int partyQuestId, bool packetOwned, int retCode = 72)
        {
            string resolvedInviter = string.IsNullOrWhiteSpace(inviterName) ? "Expedition Leader" : inviterName.Trim();
            _expeditionIntermediary.PacketOwned = packetOwned;
            _expeditionIntermediary.PendingInvite = new ExpeditionInviteState(
                resolvedInviter,
                Math.Max(1, level),
                Math.Max(0, jobCode),
                Math.Max(0, partyQuestId));
            _expeditionIntermediary.PendingRequestSummary = string.Empty;
            _expeditionIntermediary.LastPacketRetCode = retCode;
            _expeditionIntermediary.LastStatusMessage = $"Expedition intermediary queued an invite from {resolvedInviter} (Lv. {Math.Max(1, level)}, job {Math.Max(0, jobCode)}).";
            _expeditionAdmissionActive = true;
            return _expeditionIntermediary.LastStatusMessage;
        }

        internal string ApplyExpeditionResponseInvite(string inviterName, int responseCode, bool packetOwned, int retCode = 73)
        {
            string resolvedInviter = string.IsNullOrWhiteSpace(inviterName)
                ? _expeditionIntermediary.PendingInvite?.InviterName ?? "Expedition Leader"
                : inviterName.Trim();
            _expeditionIntermediary.PacketOwned = packetOwned;
            _expeditionIntermediary.PendingRequestSummary = string.Empty;
            _expeditionIntermediary.LastPacketRetCode = retCode;

            if (responseCode == 1)
            {
                ExpeditionInviteState invite = _expeditionIntermediary.PendingInvite ?? new ExpeditionInviteState(resolvedInviter, 1, 0, 0);
                _expeditionIntermediary.PendingInvite = null;
                if (!_expeditionIntermediary.HasActiveExpedition)
                {
                    ApplyExpeditionGet(
                        $"{resolvedInviter}'s Expedition",
                        0,
                        new[]
                        {
                            new ExpeditionPartySeed(0, new[]
                            {
                                new ExpeditionMemberSeed(resolvedInviter, "Master", invite.Level, _locationSummary, _channel, true, false),
                                new ExpeditionMemberSeed(_playerName, "Member", 1, _locationSummary, _channel, true, true)
                            })
                        },
                        packetOwned,
                        retCode: 61);
                }
                else if (!ExpeditionContainsMember(_playerName))
                {
                    ExpeditionPartyState localParty = GetOrCreateExpeditionParty(0);
                    localParty.Members.Add(new ExpeditionMemberState(_playerName, "Member", 1, _locationSummary, _channel, true, true));
                    NormalizeExpeditionParty(localParty);
                    SyncExpeditionSearchEntryFromIntermediary();
                }

                _expeditionAdmissionActive = true;
                _expeditionIntermediary.LastStatusMessage = $"{resolvedInviter} accepted the expedition admission flow and the intermediary now owns the roster.";
                return _expeditionIntermediary.LastStatusMessage;
            }

            _expeditionIntermediary.PendingInvite = null;
            _expeditionIntermediary.LastStatusMessage = GetExpeditionResponseMessage(resolvedInviter, responseCode);
            return _expeditionIntermediary.LastStatusMessage;
        }

        internal string ApplyExpeditionNotice(ExpeditionNoticeKind noticeKind, string characterName, bool packetOwned, int retCode = 60)
        {
            string resolvedName = string.IsNullOrWhiteSpace(characterName) ? "Unknown member" : characterName.Trim();
            _expeditionIntermediary.PacketOwned = packetOwned;
            _expeditionIntermediary.LastPacketRetCode = retCode;
            _expeditionIntermediary.LastStatusMessage = noticeKind switch
            {
                ExpeditionNoticeKind.Joined => $"{resolvedName} joined the expedition roster.",
                ExpeditionNoticeKind.Left => $"{resolvedName} left the expedition roster.",
                _ => $"{resolvedName} was removed from the expedition roster."
            };

            if (noticeKind == ExpeditionNoticeKind.Joined && !ExpeditionContainsMember(resolvedName))
            {
                EnsureExpeditionIntermediarySeeded();
                ExpeditionPartyState localParty = GetOrCreateExpeditionParty(_expeditionIntermediary.MasterPartyIndex);
                localParty.Members.Add(new ExpeditionMemberState(
                    resolvedName,
                    "Member",
                    1,
                    _locationSummary,
                    _channel,
                    true,
                    false));
                NormalizeExpeditionParty(localParty);
            }
            else if (noticeKind != ExpeditionNoticeKind.Joined)
            {
                RemoveExpeditionMember(resolvedName);
            }

            SyncExpeditionSearchEntryFromIntermediary();
            return _expeditionIntermediary.LastStatusMessage;
        }

        internal string ApplyExpeditionMasterChanged(int masterPartyIndex, bool packetOwned, int retCode = 69)
        {
            EnsureExpeditionIntermediarySeeded();
            _expeditionIntermediary.PacketOwned = packetOwned;
            _expeditionIntermediary.MasterPartyIndex = Math.Max(0, masterPartyIndex);
            _expeditionIntermediary.LastPacketRetCode = retCode;
            _expeditionIntermediary.PendingRequestSummary = string.Empty;
            _expeditionIntermediary.LastStatusMessage = $"Expedition intermediary transferred master ownership to {_expeditionIntermediary.MasterName}.";
            SyncExpeditionSearchEntryFromIntermediary();
            return _expeditionIntermediary.LastStatusMessage;
        }

        internal string ApplyExpeditionRemoved(ExpeditionRemovalKind removalKind, bool packetOwned, int retCode = 67)
        {
            return ApplyExpeditionRemovedInternal(removalKind, packetOwned, null, retCode);
        }

        internal bool HasActiveExpedition()
        {
            return _expeditionIntermediary.HasActiveExpedition;
        }

        private string ResetLocalExpeditionIntermediary()
        {
            _expeditionIntermediary.PacketOwned = false;
            _expeditionIntermediary.ExpeditionTitle = string.Empty;
            _expeditionIntermediary.MasterPartyIndex = 0;
            _expeditionIntermediary.PendingInvite = null;
            _expeditionIntermediary.PendingRequestSummary = string.Empty;
            _expeditionIntermediary.Parties.Clear();
            _expeditionIntermediary.LastPacketRetCode = 0;
            _expeditionIntermediary.LastStatusMessage = "Cleared the local expedition intermediary state.";
            _expeditionAdmissionActive = false;
            RemoveIntermediaryOwnedSearchEntry();
            ClampSearchSelection(SocialSearchTab.Expedition);
            return _expeditionIntermediary.LastStatusMessage;
        }

        private string ApplyExpeditionRemovedInternal(ExpeditionRemovalKind removalKind, bool packetOwned, string overrideMessage, int retCode = 67)
        {
            _expeditionIntermediary.PacketOwned = packetOwned;
            _expeditionIntermediary.ExpeditionTitle = string.Empty;
            _expeditionIntermediary.MasterPartyIndex = 0;
            _expeditionIntermediary.PendingInvite = null;
            _expeditionIntermediary.PendingRequestSummary = string.Empty;
            _expeditionIntermediary.Parties.Clear();
            _expeditionIntermediary.LastPacketRetCode = retCode;
            _expeditionIntermediary.LastStatusMessage = overrideMessage ?? removalKind switch
            {
                ExpeditionRemovalKind.Disband => "Expedition intermediary disbanded the roster.",
                ExpeditionRemovalKind.Removed => "Expedition intermediary removed the local player from the roster.",
                _ => "Expedition intermediary cleared the roster after a withdrawal."
            };
            _expeditionAdmissionActive = false;
            RemoveIntermediaryOwnedSearchEntry();
            ClampSearchSelection(SocialSearchTab.Expedition);
            return _expeditionIntermediary.LastStatusMessage;
        }

        private void EnsureExpeditionIntermediarySeeded()
        {
            if (_expeditionIntermediary.HasActiveExpedition)
            {
                return;
            }

            ApplyExpeditionGet(
                $"{_playerName}'s Expedition",
                0,
                CreateDefaultExpeditionSeedParties($"{_playerName}'s Expedition"),
                _expeditionIntermediary.PacketOwned,
                retCode: 57);
        }

        private IReadOnlyList<ExpeditionPartySeed> CreateDefaultExpeditionSeedParties(string expeditionTitle)
        {
            string resolvedTitle = string.IsNullOrWhiteSpace(expeditionTitle) ? $"{_playerName}'s Expedition" : expeditionTitle.Trim();
            return new[]
            {
                new ExpeditionPartySeed(0, new[]
                {
                    new ExpeditionMemberSeed(_playerName, "Master", 1, _locationSummary, _channel, true, true)
                })
            };
        }

        private void SyncExpeditionSearchEntryFromIntermediary()
        {
            EnsureSearchSeedData();
            RemoveIntermediaryOwnedSearchEntry();
            if (!_expeditionIntermediary.HasActiveExpedition)
            {
                return;
            }

            ExpeditionPartyState masterParty = GetOrCreateExpeditionParty(_expeditionIntermediary.MasterPartyIndex);
            ExpeditionMemberState masterMember = masterParty.Members.FirstOrDefault() ?? new ExpeditionMemberState(_playerName, "Master", 1, _locationSummary, _channel, true, true);
            _searchEntriesByTab[SocialSearchTab.Expedition].Insert(0, new SocialSearchEntryState(
                _expeditionIntermediary.ExpeditionTitle,
                masterMember.Name,
                "Lv. expedition",
                $"{_expeditionIntermediary.MemberCount} / 12",
                string.IsNullOrWhiteSpace(masterMember.LocationSummary) ? _locationSummary : masterMember.LocationSummary,
                Math.Max(1, masterMember.Channel),
                _expeditionIntermediary.PacketOwned
                    ? $"Packet intermediary owns {_expeditionIntermediary.ActivePartyCount} party slot(s)"
                    : $"Local intermediary owns {_expeditionIntermediary.ActivePartyCount} party slot(s)")
            {
                IsIntermediaryOwned = true
            });
        }

        private void RemoveIntermediaryOwnedSearchEntry()
        {
            EnsureSearchSeedData();
            _searchEntriesByTab[SocialSearchTab.Expedition].RemoveAll(entry => entry.IsIntermediaryOwned);
        }

        private bool ExpeditionContainsMember(string memberName)
        {
            if (string.IsNullOrWhiteSpace(memberName))
            {
                return false;
            }

            return _expeditionIntermediary.Parties.Any(party => party.Members.Any(member => string.Equals(member.Name, memberName.Trim(), StringComparison.OrdinalIgnoreCase)));
        }

        private ExpeditionPartyState GetOrCreateExpeditionParty(int partyIndex)
        {
            int resolvedIndex = Math.Max(0, partyIndex);
            ExpeditionPartyState party = _expeditionIntermediary.Parties.FirstOrDefault(entry => entry.PartyIndex == resolvedIndex);
            if (party != null)
            {
                return party;
            }

            party = new ExpeditionPartyState(resolvedIndex);
            _expeditionIntermediary.Parties.Add(party);
            _expeditionIntermediary.Parties.Sort((left, right) => left.PartyIndex.CompareTo(right.PartyIndex));
            return party;
        }

        private void ReplaceExpeditionParty(int partyIndex, IReadOnlyList<ExpeditionMemberSeed> members)
        {
            ExpeditionPartyState party = GetOrCreateExpeditionParty(partyIndex);
            party.Members.Clear();
            if (members != null)
            {
                foreach (ExpeditionMemberSeed member in members)
                {
                    if (string.IsNullOrWhiteSpace(member.Name))
                    {
                        continue;
                    }

                    party.Members.Add(new ExpeditionMemberState(
                        member.Name.Trim(),
                        string.IsNullOrWhiteSpace(member.RoleLabel) ? "Member" : member.RoleLabel.Trim(),
                        Math.Max(1, member.Level),
                        string.IsNullOrWhiteSpace(member.LocationSummary) ? _locationSummary : member.LocationSummary.Trim(),
                        Math.Max(1, member.Channel),
                        member.IsOnline,
                        member.IsLocalPlayer));
                }
            }

            NormalizeExpeditionParty(party);
        }

        private void NormalizeExpeditionParty(ExpeditionPartyState party)
        {
            if (party == null)
            {
                return;
            }

            party.Members.Sort((left, right) =>
            {
                int localCompare = right.IsLocalPlayer.CompareTo(left.IsLocalPlayer);
                if (localCompare != 0)
                {
                    return localCompare;
                }

                int roleCompare = string.Equals(left.RoleLabel, "Master", StringComparison.OrdinalIgnoreCase)
                    ? -1
                    : string.Equals(right.RoleLabel, "Master", StringComparison.OrdinalIgnoreCase)
                        ? 1
                        : 0;
                if (roleCompare != 0)
                {
                    return roleCompare;
                }

                return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });
        }

        private void ShiftExpeditionPartiesAfterRemoval(int removedPartyIndex)
        {
            foreach (ExpeditionPartyState party in _expeditionIntermediary.Parties.Where(entry => entry.PartyIndex > removedPartyIndex).OrderBy(entry => entry.PartyIndex))
            {
                party.PartyIndex--;
            }

            if (_expeditionIntermediary.Parties.Count == 0)
            {
                _expeditionIntermediary.MasterPartyIndex = 0;
                return;
            }

            _expeditionIntermediary.MasterPartyIndex = Math.Clamp(
                _expeditionIntermediary.MasterPartyIndex,
                0,
                _expeditionIntermediary.Parties.Max(entry => entry.PartyIndex));
        }

        private void RemoveExpeditionPartyAt(int partyIndex)
        {
            _expeditionIntermediary.Parties.RemoveAll(entry => entry.PartyIndex == Math.Max(0, partyIndex));
        }

        private void RemoveExpeditionMember(string memberName)
        {
            if (string.IsNullOrWhiteSpace(memberName))
            {
                return;
            }

            string resolvedName = memberName.Trim();
            foreach (ExpeditionPartyState party in _expeditionIntermediary.Parties.ToArray())
            {
                party.Members.RemoveAll(member => string.Equals(member.Name, resolvedName, StringComparison.OrdinalIgnoreCase));
                if (party.Members.Count == 0)
                {
                    RemoveExpeditionPartyAt(party.PartyIndex);
                    ShiftExpeditionPartiesAfterRemoval(party.PartyIndex);
                }
            }

            if (_expeditionIntermediary.Parties.Count == 0)
            {
                _expeditionAdmissionActive = false;
            }
        }

        private string GetExpeditionResponseMessage(string inviterName, int responseCode)
        {
            return responseCode switch
            {
                0 => $"{inviterName} declined the expedition request.",
                2 => $"{inviterName} could not process the expedition request because the owner is busy.",
                3 => $"{inviterName} reported that the expedition state already changed.",
                4 => $"{inviterName} rejected the expedition request because the owner blocked the sender.",
                5 => $"{inviterName} is unavailable for expedition admission right now.",
                6 => $"{inviterName} returned an expedition response code 6 failure.",
                7 => $"{inviterName} already has a pending expedition prompt open.",
                _ => $"{inviterName} returned expedition response code {responseCode}."
            };
        }

        private sealed class ExpeditionIntermediaryState
        {
            public string ExpeditionTitle { get; set; } = string.Empty;
            public int MasterPartyIndex { get; set; }
            public List<ExpeditionPartyState> Parties { get; } = new();
            public ExpeditionInviteState PendingInvite { get; set; }
            public bool PacketOwned { get; set; }
            public int LastPacketRetCode { get; set; }
            public string PendingRequestSummary { get; set; } = string.Empty;
            public string LastStatusMessage { get; set; } = "Expedition intermediary idle.";

            public bool HasActiveExpedition => Parties.Count > 0 && Parties.Any(party => party.Members.Count > 0);

            public int MemberCount => Parties.Sum(party => party.Members.Count);

            public int ActivePartyCount => Parties.Count(party => party.Members.Count > 0);

            public string MasterName
            {
                get
                {
                    ExpeditionPartyState masterParty = Parties.FirstOrDefault(party => party.PartyIndex == MasterPartyIndex);
                    ExpeditionMemberState masterMember = masterParty?.Members.FirstOrDefault();
                    return string.IsNullOrWhiteSpace(masterMember?.Name)
                        ? "No master"
                        : masterMember.Name;
                }
            }
        }

        private sealed class ExpeditionPartyState
        {
            public ExpeditionPartyState(int partyIndex)
            {
                PartyIndex = Math.Max(0, partyIndex);
            }

            public int PartyIndex { get; set; }
            public List<ExpeditionMemberState> Members { get; } = new();
        }

        private sealed class ExpeditionMemberState
        {
            public ExpeditionMemberState(string name, string roleLabel, int level, string locationSummary, int channel, bool isOnline, bool isLocalPlayer)
            {
                Name = name;
                RoleLabel = roleLabel;
                Level = level;
                LocationSummary = locationSummary;
                Channel = channel;
                IsOnline = isOnline;
                IsLocalPlayer = isLocalPlayer;
            }

            public string Name { get; }
            public string RoleLabel { get; }
            public int Level { get; }
            public string LocationSummary { get; }
            public int Channel { get; }
            public bool IsOnline { get; }
            public bool IsLocalPlayer { get; }
        }

        private sealed class ExpeditionInviteState
        {
            public ExpeditionInviteState(string inviterName, int level, int jobCode, int partyQuestId)
            {
                InviterName = inviterName;
                Level = level;
                JobCode = jobCode;
                PartyQuestId = partyQuestId;
            }

            public string InviterName { get; }
            public int Level { get; }
            public int JobCode { get; }
            public int PartyQuestId { get; }
        }
    }
}
