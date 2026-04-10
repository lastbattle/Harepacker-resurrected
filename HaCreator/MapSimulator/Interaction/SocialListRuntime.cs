using HaCreator.MapSimulator.Character;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum SocialListTab
    {
        Friend = 0,
        Party = 1,
        Guild = 2,
        Alliance = 3,
        Blacklist = 4
    }

    internal sealed partial class SocialListRuntime
    {
        private const int PageSize = 8;
        private static readonly SocialListTab[] TrackedTabs =
        {
            SocialListTab.Friend,
            SocialListTab.Party,
            SocialListTab.Guild
        };

        private readonly Dictionary<SocialListTab, List<SocialEntryState>> _entriesByTab = new();
        private readonly Dictionary<SocialListTab, int> _selectedIndexByTab = new();
        private readonly Dictionary<SocialListTab, int> _firstVisibleIndexByTab = new();
        private readonly Queue<SocialEntryState> _friendInviteSeeds = new();
        private readonly Queue<SocialEntryState> _guildInviteSeeds = new();
        private readonly Queue<SocialEntryState> _allianceInviteSeeds = new();
        private readonly Queue<SocialEntryState> _blacklistSeeds = new();
        private readonly List<string> _friendGroups = new();
        private readonly Dictionary<string, string> _friendGroupByName = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<SocialListTab, bool> _packetOwnedRosterByTab = new();
        private readonly Dictionary<SocialListTab, string> _lastPacketSyncSummaryByTab = new();
        private readonly Dictionary<SocialListTab, string> _lastPendingRequestByTab = new();
        private readonly List<SocialTrackedEntrySnapshot> _trackedEntriesBuffer = new();
        private readonly List<SocialEntryState> _filteredFriendEntriesBuffer = new();
        private readonly SocialListSnapshot _snapshotBuffer = new();
        private readonly List<SocialListEntrySnapshot> _snapshotEntriesBuffer = new(PageSize);
        private readonly List<string> _snapshotSummaryLinesBuffer = new(3);
        private readonly List<string> _enabledActionKeysBuffer = new(12);
        private int _trackedEntriesCount;
        private string _playerName = "Player";
        private string _locationSummary = "Maple Island";
        private string _guildName = "Maple GM";
        private string _allianceName = "Maple Union";
        private int _channel = 1;
        private bool _friendOnlineOnly;
        private bool _hasGuildMembership;
        private bool _forceNoGuildMembership;
        private bool _forceNoAllianceMembership;
        private int _nextFriendGroupNumber = 1;
        private SocialListTab _currentTab = SocialListTab.Friend;

        internal SocialListRuntime()
        {
            SeedDefaultData();
        }

        internal SocialListTab CurrentTab => _currentTab;
        internal int TrackedEntriesCount => _trackedEntriesCount;
        internal Action<string, int> SocialChatObserved { get; set; }

        internal bool HasLocalPartyLeader()
        {
            return _entriesByTab.TryGetValue(SocialListTab.Party, out List<SocialEntryState> entries)
                && entries.Any(entry => entry.IsLocalPlayer && entry.IsLeader);
        }

        internal bool TryGetSelectedEntrySnapshot(SocialListTab tab, out SocialTrackedEntrySnapshot snapshot)
        {
            snapshot = null;
            SocialEntryState entry = GetSelectedEntry(tab);
            if (entry == null)
            {
                return false;
            }

            snapshot = new SocialTrackedEntrySnapshot
            {
                Tab = tab,
                Name = entry.Name,
                PrimaryText = entry.PrimaryText,
                SecondaryText = entry.SecondaryText,
                LocationSummary = entry.LocationSummary,
                Channel = entry.Channel,
                IsOnline = entry.IsOnline,
                IsLeader = entry.IsLeader,
                IsLocalPlayer = entry.IsLocalPlayer
            };
            return true;
        }

        internal bool TryFindTrackedEntry(string characterName, out SocialTrackedEntrySnapshot snapshot)
        {
            snapshot = null;
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return false;
            }

            foreach (SocialListTab tab in TrackedTabs)
            {
                if (!_entriesByTab.TryGetValue(tab, out List<SocialEntryState> entries))
                {
                    continue;
                }

                SocialEntryState entry = entries.FirstOrDefault(candidate =>
                    !candidate.IsLocalPlayer &&
                    string.Equals(candidate.Name, characterName, StringComparison.OrdinalIgnoreCase));
                if (entry == null)
                {
                    continue;
                }

                snapshot = new SocialTrackedEntrySnapshot
                {
                    Tab = tab,
                    Name = entry.Name,
                    PrimaryText = entry.PrimaryText,
                    SecondaryText = entry.SecondaryText,
                    LocationSummary = entry.LocationSummary,
                    Channel = entry.Channel,
                    IsOnline = entry.IsOnline,
                    IsLeader = entry.IsLeader,
                    IsLocalPlayer = entry.IsLocalPlayer
                };
                return true;
            }

            return false;
        }

        internal void UpdateLocalContext(CharacterBuild build, string locationSummary, int channel)
        {
            _playerName = string.IsNullOrWhiteSpace(build?.Name) ? "Player" : build.Name.Trim();
            _locationSummary = string.IsNullOrWhiteSpace(locationSummary) ? "Field" : locationSummary.Trim();
            _hasGuildMembership = ResolveEffectiveGuildMembership(build);
            _guildName = ResolveEffectiveGuildName(build, _hasGuildMembership);
            bool hasAllianceMembership = ResolveEffectiveAllianceMembership(build);
            _allianceName = ResolveEffectiveAllianceName(build, hasAllianceMembership);
            _channel = Math.Max(1, channel);
            string localGuildRoleLabel = ResolveUpdatedLocalGuildRoleLabel();

            UpdateOrInsertLocalEntry(
                SocialListTab.Friend,
                new SocialEntryState(_playerName, build?.JobName ?? "Beginner", $"Lv. {Math.Max(1, build?.Level ?? 1)}", _locationSummary, _channel, true, true, false)
                {
                    IsLocalPlayer = true
                });
            UpdateOrInsertLocalEntry(
                SocialListTab.Party,
                new SocialEntryState(_playerName, "Leader", $"Lv. {Math.Max(1, build?.Level ?? 1)}", _locationSummary, _channel, true, true, false)
                {
                    IsLocalPlayer = true
                });
            if (_hasGuildMembership)
            {
                UpdateOrInsertLocalEntry(
                    SocialListTab.Guild,
                    new SocialEntryState(_playerName, localGuildRoleLabel, _guildName, _locationSummary, _channel, true, IsGuildLeaderRole(localGuildRoleLabel), false)
                    {
                        IsLocalPlayer = true
                    });
            }
            else
            {
                RemoveLocalEntry(SocialListTab.Guild);
            }

            if (hasAllianceMembership)
            {
                UpdateOrInsertLocalEntry(
                    SocialListTab.Alliance,
                    new SocialEntryState(_playerName, "Representative", _allianceName, _guildName, _channel, true, true, false)
                    {
                        IsLocalPlayer = true
                    });
            }
            else
            {
                RemoveLocalEntry(SocialListTab.Alliance);
            }
        }

        internal GuildSkillUiContext BuildGuildSkillUiContext(CharacterBuild build)
        {
            bool hasGuildMembership = ResolveEffectiveGuildMembership(build);
            return new GuildSkillUiContext(
                hasGuildMembership,
                ResolveEffectiveGuildName(build, hasGuildMembership),
                ResolveEffectiveGuildLevel(),
                GetEffectiveGuildRoleLabelForUi(),
                CanManageGuildSkills(),
                TryGetEffectiveGuildPoints());
        }

        internal SocialListSnapshot BuildSnapshot()
        {
            IReadOnlyList<SocialEntryState> tabEntries = GetFilteredEntries(_currentTab);
            int selectedIndex = GetSelectedIndex(_currentTab, tabEntries.Count);
            int firstVisibleIndex = GetFirstVisibleIndex(_currentTab, tabEntries.Count);
            int totalPages = Math.Max(1, (int)Math.Ceiling(tabEntries.Count / (float)PageSize));
            int page = Math.Clamp((firstVisibleIndex / PageSize) + 1, 1, totalPages);
            int pageSelectedIndex = selectedIndex >= firstVisibleIndex && selectedIndex < (firstVisibleIndex + PageSize)
                ? selectedIndex - firstVisibleIndex
                : -1;
            SocialEntryState selectedEntry = selectedIndex >= 0 && selectedIndex < tabEntries.Count ? tabEntries[selectedIndex] : null;

            int visibleEntryCount = Math.Min(PageSize, Math.Max(0, tabEntries.Count - firstVisibleIndex));
            for (int i = 0; i < visibleEntryCount; i++)
            {
                SocialEntryState entry = tabEntries[firstVisibleIndex + i];
                SocialListEntrySnapshot snapshotEntry = GetOrCreateSnapshotEntry(i);
                snapshotEntry.Name = entry.Name;
                snapshotEntry.PrimaryText = entry.PrimaryText;
                snapshotEntry.SecondaryText = entry.SecondaryText;
                snapshotEntry.LocationSummary = entry.LocationSummary;
                snapshotEntry.Channel = entry.Channel;
                snapshotEntry.IsOnline = entry.IsOnline;
                snapshotEntry.IsLeader = entry.IsLeader;
                snapshotEntry.IsLocalPlayer = entry.IsLocalPlayer;
            }

            if (_snapshotEntriesBuffer.Count > visibleEntryCount)
            {
                _snapshotEntriesBuffer.RemoveRange(visibleEntryCount, _snapshotEntriesBuffer.Count - visibleEntryCount);
            }

            BuildSummaryLines(selectedEntry, _snapshotSummaryLinesBuffer, tabEntries.Count);
            PopulateEnabledActions(selectedEntry, _enabledActionKeysBuffer);

            _snapshotBuffer.CurrentTab = _currentTab;
            _snapshotBuffer.Entries = _snapshotEntriesBuffer;
            _snapshotBuffer.SelectedVisibleIndex = pageSelectedIndex;
            _snapshotBuffer.Page = page;
            _snapshotBuffer.TotalPages = totalPages;
            _snapshotBuffer.TotalEntries = tabEntries.Count;
            _snapshotBuffer.FirstVisibleIndex = firstVisibleIndex;
            _snapshotBuffer.MaxFirstVisibleIndex = Math.Max(0, tabEntries.Count - PageSize);
            _snapshotBuffer.VisibleCapacity = PageSize;
            _snapshotBuffer.HeaderTitle = GetHeaderTitle();
            _snapshotBuffer.SummaryLines = _snapshotSummaryLinesBuffer;
            _snapshotBuffer.EnabledActionKeys = _enabledActionKeysBuffer;
            _snapshotBuffer.FriendOnlineOnly = _friendOnlineOnly;
            _snapshotBuffer.CanPageBackward = firstVisibleIndex > 0;
            _snapshotBuffer.CanPageForward = firstVisibleIndex < Math.Max(0, tabEntries.Count - PageSize);
            return _snapshotBuffer;
        }

        internal IReadOnlyList<SocialTrackedEntrySnapshot> BuildTrackedEntriesSnapshot()
        {
            _trackedEntriesCount = 0;
            for (int tabIndex = 0; tabIndex < TrackedTabs.Length; tabIndex++)
            {
                SocialListTab tab = TrackedTabs[tabIndex];
                if (!_entriesByTab.TryGetValue(tab, out List<SocialEntryState> tabEntries) || tabEntries == null)
                {
                    continue;
                }

                for (int i = 0; i < tabEntries.Count; i++)
                {
                    SocialEntryState entry = tabEntries[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    SocialTrackedEntrySnapshot snapshot = GetOrCreateTrackedEntrySnapshot(_trackedEntriesCount++);
                    snapshot.Tab = tab;
                    snapshot.Name = entry.Name;
                    snapshot.LocationSummary = entry.LocationSummary;
                    snapshot.Channel = entry.Channel;
                    snapshot.IsOnline = entry.IsOnline;
                    snapshot.IsLeader = entry.IsLeader;
                    snapshot.IsLocalPlayer = entry.IsLocalPlayer;
                }
            }

            return _trackedEntriesBuffer;
        }

        private SocialTrackedEntrySnapshot GetOrCreateTrackedEntrySnapshot(int index)
        {
            while (_trackedEntriesBuffer.Count <= index)
            {
                _trackedEntriesBuffer.Add(new SocialTrackedEntrySnapshot());
            }

            return _trackedEntriesBuffer[index];
        }

        internal bool IsTrackedPartyMember(string entryName)
        {
            if (string.IsNullOrWhiteSpace(entryName)
                || !_entriesByTab.TryGetValue(SocialListTab.Party, out List<SocialEntryState> entries)
                || entries == null)
            {
                return false;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                SocialEntryState entry = entries[i];
                if (entry == null
                    || entry.IsLocalPlayer
                    || !string.Equals(entry.Name, entryName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        internal bool IsBlacklisted(string entryName)
        {
            if (string.IsNullOrWhiteSpace(entryName)
                || !_entriesByTab.TryGetValue(SocialListTab.Blacklist, out List<SocialEntryState> entries)
                || entries == null)
            {
                return false;
            }

            return entries.Any(entry => string.Equals(entry.Name, entryName, StringComparison.OrdinalIgnoreCase));
        }

        internal bool IsBlockedFriend(string entryName)
        {
            if (string.IsNullOrWhiteSpace(entryName)
                || !_entriesByTab.TryGetValue(SocialListTab.Friend, out List<SocialEntryState> entries)
                || entries == null)
            {
                return false;
            }

            return entries.Any(entry =>
                entry.IsBlocked
                && string.Equals(entry.Name, entryName, StringComparison.OrdinalIgnoreCase));
        }

        internal void SelectTab(SocialListTab tab)
        {
            _currentTab = tab;
            int count = GetFilteredEntries(tab).Count;
            _selectedIndexByTab[tab] = count > 0 ? Math.Clamp(GetSelectedIndex(tab, count), 0, count - 1) : -1;
            EnsureSelectionVisible(tab, count);
        }

        internal void SelectVisibleEntry(int visibleIndex)
        {
            if (visibleIndex < 0)
            {
                return;
            }

            IReadOnlyList<SocialEntryState> entries = GetFilteredEntries(_currentTab);
            if (entries.Count == 0)
            {
                _selectedIndexByTab[_currentTab] = -1;
                return;
            }

            int firstVisibleIndex = GetFirstVisibleIndex(_currentTab, entries.Count);
            int absoluteIndex = firstVisibleIndex + visibleIndex;
            if (absoluteIndex >= 0 && absoluteIndex < entries.Count)
            {
                _selectedIndexByTab[_currentTab] = absoluteIndex;
            }
        }

        internal void MovePage(int delta)
        {
            MoveScroll(delta * PageSize);
        }

        internal void MoveScroll(int delta)
        {
            IReadOnlyList<SocialEntryState> entries = GetFilteredEntries(_currentTab);
            if (entries.Count <= PageSize)
            {
                _firstVisibleIndexByTab[_currentTab] = 0;
                return;
            }

            int maxFirstVisibleIndex = Math.Max(0, entries.Count - PageSize);
            int firstVisibleIndex = GetFirstVisibleIndex(_currentTab, entries.Count);
            _firstVisibleIndexByTab[_currentTab] = Math.Clamp(firstVisibleIndex + delta, 0, maxFirstVisibleIndex);
        }

        internal void SetScrollPosition(float ratio)
        {
            IReadOnlyList<SocialEntryState> entries = GetFilteredEntries(_currentTab);
            int maxFirstVisibleIndex = Math.Max(0, entries.Count - PageSize);
            if (maxFirstVisibleIndex <= 0)
            {
                _firstVisibleIndexByTab[_currentTab] = 0;
                return;
            }

            _firstVisibleIndexByTab[_currentTab] = (int)Math.Round(Math.Clamp(ratio, 0f, 1f) * maxFirstVisibleIndex);
        }

        internal void SetFriendOnlineOnly(bool onlineOnly)
        {
            _friendOnlineOnly = onlineOnly;
            int count = GetFilteredEntries(SocialListTab.Friend).Count;
            _selectedIndexByTab[SocialListTab.Friend] = count > 0 ? 0 : -1;
            _firstVisibleIndexByTab[SocialListTab.Friend] = 0;
        }

        internal string InviteCharacterToParty(string characterName, string jobName, int level, string locationSummary, int channel)
        {
            if (TryStagePacketOwnedRequest(SocialListTab.Party, "Party invite", out string requestMessage))
            {
                return requestMessage;
            }

            string resolvedName = string.IsNullOrWhiteSpace(characterName) ? "Remote Character" : characterName.Trim();
            if (_entriesByTab[SocialListTab.Party].Any(entry => string.Equals(entry.Name, resolvedName, StringComparison.OrdinalIgnoreCase)))
            {
                return $"{resolvedName} is already in the simulated party roster.";
            }

            _entriesByTab[SocialListTab.Party].Add(new SocialEntryState(
                resolvedName,
                string.IsNullOrWhiteSpace(jobName) ? "Adventurer" : jobName.Trim(),
                $"Lv. {Math.Max(1, level)}",
                string.IsNullOrWhiteSpace(locationSummary) ? "Current map" : locationSummary.Trim(),
                Math.Max(1, channel),
                true,
                false,
                false));
            SelectTab(SocialListTab.Party);
            return $"Queued a simulated party invite for {resolvedName} from the profile window.";
        }

        internal string ExecuteAction(string actionKey)
        {
            if (string.IsNullOrWhiteSpace(actionKey))
            {
                return null;
            }

            return actionKey switch
            {
                "Friend.AddFriend" => AddFriend(),
                "Friend.AddGroup" => AddFriendGroup(),
                "Friend.Party" => InviteFriendToParty(),
                "Friend.Chat" => SendFriendChat(),
                "Friend.Whisper" => WhisperSelected("friend"),
                "Friend.GroupWhisper" => GroupWhisperFriend(),
                "Friend.Mate" => "Maple Mate proposals now open the dedicated MateMessage owner.",
                "Friend.Message" => MemoSelectedFriend(),
                "Friend.Mod" => ModifyFriendMemo(),
                "Friend.Delete" => DeleteFriend(),
                "Friend.Block" => BlockFriend(),
                "Friend.UnBlock" => UnblockFriend(),
                "Party.Create" => CreateParty(),
                "Party.Invite" => AddPartyMember(),
                "Party.Kick" => RemovePartyMember(false),
                "Party.Withdraw" => RemovePartyMember(true),
                "Party.Whisper" => WhisperSelected("party"),
                "Party.Chat" => SendPartyChat(),
                "Party.ChangeBoss" => ChangePartyLeader(),
                "Party.Search" => null,
                "Guild.Board" => "Guild BBS has its own backlog item and is not wired from the member list yet.",
                "Guild.Invite" => AddGuildMember(),
                "Guild.Withdraw" => RemoveGuildMember("guild"),
                "Guild.PartyInvite" => InviteSelectedEntryToPartyFromTab(SocialListTab.Guild, "guild"),
                "Guild.GradeUp" => AdjustGuildGrade(1),
                "Guild.GradeDown" => AdjustGuildGrade(-1),
                "Guild.Kick" => RemoveGuildMember("guild"),
                "Guild.Where" => LocateSelected("guild"),
                "Guild.Whisper" => WhisperSelected("guild"),
                "Guild.Info" => ShowSelectedInfo("guild"),
                "Guild.Skill" => "Guild skill management remains a separate `GuildSkill` surface, but the button now resolves as a dedicated social action.",
                "Guild.Search" => null,
                "Guild.Manage" => "Guild management now opens the dedicated three-tab editor window.",
                "Guild.Change" => "Guild notice editing now opens the dedicated change tab inside guild management.",
                "Alliance.Invite" => AddAllianceMember(),
                "Alliance.Withdraw" => RemoveGuildMember("alliance"),
                "Alliance.PartyInvite" => InviteSelectedEntryToPartyFromTab(SocialListTab.Alliance, "alliance"),
                "Alliance.GradeUp" => AdjustAllianceGrade(1),
                "Alliance.GradeDown" => AdjustAllianceGrade(-1),
                "Alliance.Kick" => RemoveGuildMember("alliance"),
                "Alliance.Change" => "Alliance rank titles now open in the dedicated union editor.",
                "Alliance.Chat" => SendAllianceChat(),
                "Alliance.Whisper" => WhisperSelected("alliance"),
                "Alliance.Info" => ShowSelectedInfo("alliance"),
                "Alliance.Notice" => "Alliance notice editing now opens in the dedicated union editor.",
                "Blacklist.Add" => AddBlacklistEntry(),
                "Blacklist.Delete" => DeleteBlacklistEntry(),
                _ => "That social action is not modeled yet."
            };
        }

        internal string GetLocalGuildRoleLabel()
        {
            if (!ResolveEffectiveGuildMembership(null))
            {
                return "Member";
            }

            SocialEntryState localGuildEntry = _entriesByTab.TryGetValue(SocialListTab.Guild, out List<SocialEntryState> entries)
                ? entries.FirstOrDefault(entry => entry.IsLocalPlayer)
                : null;
            return string.IsNullOrWhiteSpace(localGuildEntry?.PrimaryText)
                ? "Member"
                : localGuildEntry.PrimaryText;
        }

        private string ResolveUpdatedLocalGuildRoleLabel()
        {
            if (!ResolveEffectiveGuildMembership(null))
            {
                return "Member";
            }

            SocialEntryState localGuildEntry = _entriesByTab.TryGetValue(SocialListTab.Guild, out List<SocialEntryState> entries)
                ? entries.FirstOrDefault(entry => entry.IsLocalPlayer)
                : null;
            string currentRole = localGuildEntry?.PrimaryText?.Trim();
            return currentRole switch
            {
                "Master" => "Master",
                "Jr. Master" => "Jr. Master",
                "Jr Master" => "Jr. Master",
                "Junior Master" => "Jr. Master",
                "Member" => "Member",
                _ => "Member"
            };
        }

        private static bool IsGuildLeaderRole(string guildRoleLabel)
        {
            return string.Equals(guildRoleLabel, "Master", StringComparison.OrdinalIgnoreCase);
        }

        internal bool HasPartyAdmissionContext()
        {
            if (!_entriesByTab.TryGetValue(SocialListTab.Party, out List<SocialEntryState> entries) || entries == null)
            {
                return false;
            }

            return entries.Any(entry => entry != null && !entry.IsLocalPlayer);
        }

        private void SeedDefaultData()
        {
            foreach (SocialListTab tab in Enum.GetValues(typeof(SocialListTab)))
            {
                _entriesByTab[tab] = new List<SocialEntryState>();
                _selectedIndexByTab[tab] = -1;
                _firstVisibleIndexByTab[tab] = 0;
                _packetOwnedRosterByTab[tab] = false;
                _lastPacketSyncSummaryByTab[tab] = "No packet roster sync received.";
                _lastPendingRequestByTab[tab] = null;
            }

            _entriesByTab[SocialListTab.Friend].AddRange(new[]
            {
                new SocialEntryState("Rondo", "Fighter", "Lv. 32", "Lith Harbor", 4, true, false, false),
                new SocialEntryState("Rin", "Cleric", "Lv. 37", "Sleepywood", 7, true, false, false),
                new SocialEntryState("Targa", "Hunter", "Lv. 35", "Free Market", 1, true, false, false),
                new SocialEntryState("Aria", "Wizard", "Lv. 41", "Orbis", 12, true, false, false),
                new SocialEntryState("Pia", "Page", "Lv. 29", "Henesys", 2, true, false, false),
                new SocialEntryState("Sera", "Assassin", "Lv. 44", "Kerning City", 3, true, false, false),
                new SocialEntryState("Milo", "Spearman", "Lv. 31", "Perion", 5, false, false, false),
                new SocialEntryState("Eve", "Gunslinger", "Lv. 39", "Nautilus", 6, true, false, false),
                new SocialEntryState("Noel", "Priest", "Lv. 52", "Leafre", 14, false, false, false)
            });

            _entriesByTab[SocialListTab.Party].AddRange(new[]
            {
                new SocialEntryState("Rondo", "Fighter", "Leader", "Lith Harbor", 4, true, true, false),
                new SocialEntryState("Rin", "Cleric", "Support", "Sleepywood", 7, true, false, false),
                new SocialEntryState("Targa", "Hunter", "Scout", "Free Market", 1, true, false, false)
            });

            _entriesByTab[SocialListTab.Guild].AddRange(new[]
            {
                new SocialEntryState("Cody", "Jr. Master", "Maple GM", "Henesys", 1, true, false, false),
                new SocialEntryState("Pia", "Member", "Maple GM", "Henesys", 2, true, false, false),
                new SocialEntryState("Milo", "Member", "Maple GM", "Perion", 5, false, false, false),
                new SocialEntryState("Aria", "Member", "Maple GM", "Orbis", 12, true, false, false)
            });

            _entriesByTab[SocialListTab.Alliance].AddRange(new[]
            {
                new SocialEntryState("Lith Harbor", "Guild", "6 online", "Alliance Hall", 1, true, true, false),
                new SocialEntryState("Sleepywood", "Guild", "4 online", "Sleepywood", 7, true, false, false),
                new SocialEntryState("Orbis", "Guild", "3 online", "Orbis", 12, true, false, false)
            });

            _entriesByTab[SocialListTab.Blacklist].AddRange(new[]
            {
                new SocialEntryState("SpamBot01", "Blacklisted", "Trade spam", "Free Market", 1, true, false, true),
                new SocialEntryState("LureSeller", "Blacklisted", "Scam warning", "Henesys", 2, false, false, true)
            });

            foreach (SocialEntryState friend in new[]
            {
                new SocialEntryState("Clove", "Brawler", "Lv. 28", "Kerning Subway", 8, true, false, false),
                new SocialEntryState("Dawn", "Crossbowman", "Lv. 47", "Ludibrium", 11, true, false, false),
                new SocialEntryState("Vale", "Priest", "Lv. 61", "El Nath", 9, true, false, false)
            })
            {
                _friendInviteSeeds.Enqueue(friend);
            }

            _guildInviteSeeds.Enqueue(new SocialEntryState("Iris", "Member", "Maple GM", "Ludibrium", 9, true, false, false));
            _guildInviteSeeds.Enqueue(new SocialEntryState("Bran", "Member", "Maple GM", "Ariant", 6, true, false, false));

            _allianceInviteSeeds.Enqueue(new SocialEntryState("Aqua Road", "Guild", "5 online", "Aquarium", 8, true, false, false));
            _allianceInviteSeeds.Enqueue(new SocialEntryState("Mu Lung", "Guild", "2 online", "Mu Lung", 4, true, false, false));

            _blacklistSeeds.Enqueue(new SocialEntryState("MapleMegaphone", "Blacklisted", "Megaphone spam", "Channel 1", 1, true, false, true));
            _blacklistSeeds.Enqueue(new SocialEntryState("TradeFlood", "Blacklisted", "Bot advertising", "Channel 2", 2, true, false, true));

            _friendGroups.Add("Party Quest");
            _friendGroups.Add("Bossing");
            _nextFriendGroupNumber = 3;
            _friendGroupByName["Rondo"] = "Party Quest";
            _friendGroupByName["Rin"] = "Party Quest";
            _friendGroupByName["Aria"] = "Bossing";
            _friendGroupByName["Noel"] = "Bossing";

            _selectedIndexByTab[SocialListTab.Friend] = 0;
            _selectedIndexByTab[SocialListTab.Party] = 0;
            _selectedIndexByTab[SocialListTab.Guild] = 0;
            _selectedIndexByTab[SocialListTab.Alliance] = 0;
            _selectedIndexByTab[SocialListTab.Blacklist] = 0;
        }

        private void UpdateOrInsertLocalEntry(SocialListTab tab, SocialEntryState localEntry)
        {
            if (ShouldSuppressPacketOwnedLocalGuildEntry(tab))
            {
                RemoveLocalEntry(tab);
                return;
            }

            List<SocialEntryState> entries = _entriesByTab[tab];
            int existingIndex = entries.FindIndex(entry => entry.IsLocalPlayer);
            if (existingIndex >= 0)
            {
                SocialEntryState existingEntry = entries[existingIndex];
                entries[existingIndex] = IsPacketOwned(tab)
                    ? MergePacketOwnedLocalEntry(existingEntry, localEntry)
                    : localEntry;
            }
            else
            {
                entries.Insert(0, localEntry);
            }
        }

        private bool ShouldSuppressPacketOwnedLocalGuildEntry(SocialListTab tab)
        {
            return tab == SocialListTab.Guild &&
                   IsPacketOwned(tab) &&
                   _packetGuildUiState.HasValue &&
                   !_packetGuildUiState.Value.HasGuildMembership;
        }

        private void RemoveLocalEntry(SocialListTab tab)
        {
            if (!_entriesByTab.TryGetValue(tab, out List<SocialEntryState> entries))
            {
                return;
            }

            int existingIndex = entries.FindIndex(entry => entry.IsLocalPlayer);
            if (existingIndex >= 0)
            {
                entries.RemoveAt(existingIndex);
            }
        }

        private bool ResolveEffectiveGuildMembership(CharacterBuild build)
        {
            if (_forceNoGuildMembership)
            {
                return false;
            }

            return _packetGuildUiState?.HasGuildMembership ?? GuildSkillRuntime.HasGuildMembership(build);
        }

        private string ResolveEffectiveGuildName(CharacterBuild build, bool hasGuildMembership)
        {
            if (!hasGuildMembership)
            {
                return "No Guild";
            }

            if (_packetGuildUiState.HasValue)
            {
                string packetGuildName = _packetGuildUiState.Value.GuildName?.Trim();
                if (GuildSkillRuntime.HasGuildMembership(packetGuildName))
                {
                    return packetGuildName;
                }
            }

            return string.IsNullOrWhiteSpace(build?.GuildName) ? "No Guild" : build.GuildName.Trim();
        }

        private bool ResolveEffectiveAllianceMembership(CharacterBuild build)
        {
            if (_forceNoAllianceMembership || !ResolveEffectiveGuildMembership(build))
            {
                return false;
            }

            return true;
        }

        private static string ResolveEffectiveAllianceName(CharacterBuild build, bool hasAllianceMembership)
        {
            if (!hasAllianceMembership)
            {
                return "No Alliance";
            }

            return string.IsNullOrWhiteSpace(build?.AllianceDisplayText) ? "Maple Union" : build.AllianceDisplayText.Trim();
        }

        private int ResolveEffectiveGuildLevel()
        {
            return _packetGuildUiState?.GuildLevel ?? 0;
        }

        private IReadOnlyList<SocialEntryState> GetFilteredEntries(SocialListTab tab)
        {
            List<SocialEntryState> entries = _entriesByTab[tab];
            if (tab == SocialListTab.Friend && _friendOnlineOnly)
            {
                _filteredFriendEntriesBuffer.Clear();
                for (int i = 0; i < entries.Count; i++)
                {
                    SocialEntryState entry = entries[i];
                    if (entry.IsOnline)
                    {
                        _filteredFriendEntriesBuffer.Add(entry);
                    }
                }

                return _filteredFriendEntriesBuffer;
            }

            return entries;
        }

        private int GetSelectedIndex(SocialListTab tab, int entryCount)
        {
            if (entryCount <= 0)
            {
                return -1;
            }

            if (!_selectedIndexByTab.TryGetValue(tab, out int selectedIndex))
            {
                selectedIndex = 0;
            }

            return Math.Clamp(selectedIndex, 0, entryCount - 1);
        }

        private int GetFirstVisibleIndex(SocialListTab tab, int entryCount)
        {
            int maxFirstVisibleIndex = Math.Max(0, entryCount - PageSize);
            int firstVisibleIndex = _firstVisibleIndexByTab.TryGetValue(tab, out int currentFirstVisibleIndex)
                ? currentFirstVisibleIndex
                : 0;
            int clamped = Math.Clamp(firstVisibleIndex, 0, maxFirstVisibleIndex);
            _firstVisibleIndexByTab[tab] = clamped;
            return clamped;
        }

        private void EnsureSelectionVisible(SocialListTab tab, int entryCount)
        {
            if (entryCount <= 0)
            {
                _firstVisibleIndexByTab[tab] = 0;
                return;
            }

            int firstVisibleIndex = GetFirstVisibleIndex(tab, entryCount);
            int selectedIndex = GetSelectedIndex(tab, entryCount);
            if (selectedIndex < firstVisibleIndex)
            {
                _firstVisibleIndexByTab[tab] = selectedIndex;
                return;
            }

            int visibleEnd = firstVisibleIndex + PageSize - 1;
            if (selectedIndex > visibleEnd)
            {
                _firstVisibleIndexByTab[tab] = Math.Max(0, selectedIndex - PageSize + 1);
                GetFirstVisibleIndex(tab, entryCount);
            }
        }

        private SocialListEntrySnapshot GetOrCreateSnapshotEntry(int index)
        {
            while (_snapshotEntriesBuffer.Count <= index)
            {
                _snapshotEntriesBuffer.Add(new SocialListEntrySnapshot());
            }

            return _snapshotEntriesBuffer[index];
        }

        private string GetHeaderTitle()
        {
            return _currentTab switch
            {
                SocialListTab.Friend => _friendOnlineOnly ? "Friends (Online)" : "Friends",
                SocialListTab.Party => "Party",
                SocialListTab.Guild => "Guild",
                SocialListTab.Alliance => "Alliance",
                SocialListTab.Blacklist => "Blacklist",
                _ => "Social"
            };
        }

        private void BuildSummaryLines(SocialEntryState selectedEntry, List<string> destination, int visibleEntryCount)
        {
            destination.Clear();
            if (selectedEntry == null)
            {
                switch (_currentTab)
                {
                    case SocialListTab.Friend:
                        destination.Add("Friend tab mirrors the client's list shell.");
                        destination.Add(BuildOwnershipSummary(SocialListTab.Friend));
                        destination.Add($"{visibleEntryCount} visible friend entries across {_friendGroups.Count} local group folder{(_friendGroups.Count == 1 ? string.Empty : "s")}.");
                        return;
                    case SocialListTab.Party:
                        destination.Add("Party tab owns leader, member, and location summaries.");
                        destination.Add(BuildOwnershipSummary(SocialListTab.Party));
                        destination.Add($"{visibleEntryCount} visible party entries.");
                        return;
                    case SocialListTab.Guild:
                        destination.Add($"Guild: {_guildName}");
                        destination.Add(BuildOwnershipSummary(SocialListTab.Guild));
                        destination.Add($"{visibleEntryCount} guild members listed.");
                        return;
                    case SocialListTab.Alliance:
                        destination.Add($"Alliance: {_allianceName}");
                        destination.Add(BuildOwnershipSummary(SocialListTab.Alliance));
                        destination.Add($"{visibleEntryCount} alliance entries listed.");
                        return;
                    case SocialListTab.Blacklist:
                        destination.Add("Blacklist entries stay isolated from the friend roster.");
                        destination.Add(BuildOwnershipSummary(SocialListTab.Blacklist));
                        destination.Add($"{visibleEntryCount} blocked entries.");
                        return;
                    default:
                        return;
                }
            }

            destination.Add(selectedEntry.IsLocalPlayer ? $"{selectedEntry.Name} (You)" : selectedEntry.Name);
            destination.Add($"{selectedEntry.PrimaryText}  {selectedEntry.SecondaryText}".Trim());
            string thirdLine = $"{selectedEntry.LocationSummary}  CH {selectedEntry.Channel}  {GetOwnershipBadge(_currentTab)}".Trim();
            if (_currentTab == SocialListTab.Friend && TryGetFriendGroupLabel(selectedEntry.Name, out string friendGroupLabel))
            {
                thirdLine = $"{selectedEntry.LocationSummary}  Group {friendGroupLabel}  CH {selectedEntry.Channel}  {GetOwnershipBadge(_currentTab)}".Trim();
            }

            destination.Add(thirdLine);
        }

        private void PopulateEnabledActions(SocialEntryState selectedEntry, List<string> destination)
        {
            destination.Clear();
            switch (_currentTab)
            {
                case SocialListTab.Friend:
                    destination.Add("Friend.AddFriend");
                    if (selectedEntry != null)
                    {
                        destination.Add("Friend.Chat");
                        destination.Add("Friend.Whisper");
                        destination.Add("Friend.GroupWhisper");
                        destination.Add("Friend.Message");
                        destination.Add("Friend.Mod");
                        destination.Add(_entriesByTab[SocialListTab.Blacklist].Any(entry => string.Equals(entry.Name, selectedEntry.Name, StringComparison.OrdinalIgnoreCase))
                            ? "Friend.UnBlock"
                            : "Friend.Block");
                        if (!selectedEntry.IsLocalPlayer)
                        {
                            destination.Add("Friend.AddGroup");
                            destination.Add("Friend.Mate");
                            destination.Add("Friend.Party");
                            destination.Add("Friend.Delete");
                        }
                    }
                    break;
                case SocialListTab.Party:
                    destination.Add("Party.Create");
                    destination.Add("Party.Invite");
                    destination.Add("Party.Chat");
                    if (selectedEntry != null)
                    {
                        destination.Add("Party.Whisper");
                        destination.Add("Party.ChangeBoss");
                        if (!selectedEntry.IsLocalPlayer)
                        {
                            destination.Add("Party.Kick");
                        }
                        else
                        {
                            destination.Add("Party.Withdraw");
                        }
                    }
                    destination.Add("Party.Search");
                    break;
                case SocialListTab.Guild:
                    bool canManageGuildRoster = CanManageGuildRanks() || CanToggleGuildAdmission();
                    destination.Add("Guild.Search");
                    if (!ResolveEffectiveGuildMembership(null))
                    {
                        break;
                    }

                    destination.Add("Guild.Board");
                    if (canManageGuildRoster)
                    {
                        destination.Add("Guild.Invite");
                    }

                    if (selectedEntry != null)
                    {
                        destination.Add("Guild.Where");
                        destination.Add("Guild.Whisper");
                        destination.Add("Guild.Info");
                        destination.Add("Guild.Skill");
                        if (CanManageGuildRanks())
                        {
                            destination.Add("Guild.GradeUp");
                            destination.Add("Guild.GradeDown");
                        }

                        if (CanOpenGuildManageWindow())
                        {
                            destination.Add("Guild.Manage");
                        }

                        if (CanEditGuildNotice())
                        {
                            destination.Add("Guild.Change");
                        }

                        if (!selectedEntry.IsLocalPlayer)
                        {
                            if (HasLocalPartyLeader())
                            {
                                destination.Add("Guild.PartyInvite");
                            }

                            if (canManageGuildRoster)
                            {
                                destination.Add("Guild.Kick");
                            }
                        }
                        else if (selectedEntry.IsLocalPlayer)
                        {
                            destination.Add("Guild.Withdraw");
                        }
                    }
                    break;
                case SocialListTab.Alliance:
                    bool canManageAllianceRanks = CanEditAllianceRanks();
                    if (canManageAllianceRanks)
                    {
                        destination.Add("Alliance.Invite");
                    }

                    if (selectedEntry != null)
                    {
                        destination.Add("Alliance.Chat");
                        destination.Add("Alliance.Whisper");
                        destination.Add("Alliance.Info");
                        if (canManageAllianceRanks)
                        {
                            destination.Add("Alliance.Change");
                            destination.Add("Alliance.GradeUp");
                            destination.Add("Alliance.GradeDown");
                        }

                        if (CanEditAllianceNotice())
                        {
                            destination.Add("Alliance.Notice");
                        }

                        if (!selectedEntry.IsLocalPlayer)
                        {
                            if (HasLocalPartyLeader())
                            {
                                destination.Add("Alliance.PartyInvite");
                            }

                            if (canManageAllianceRanks)
                            {
                                destination.Add("Alliance.Kick");
                            }
                        }
                        else if (selectedEntry.IsLocalPlayer)
                        {
                            destination.Add("Alliance.Withdraw");
                        }
                    }
                    break;
                case SocialListTab.Blacklist:
                    destination.Add("Blacklist.Add");
                    if (selectedEntry != null)
                    {
                        destination.Add("Blacklist.Delete");
                    }
                    break;
            }
        }

        private string AddFriend()
        {
            if (TryStagePacketOwnedRequest(SocialListTab.Friend, "Friend add", out string requestMessage))
            {
                return requestMessage;
            }

            SocialEntryState nextFriend = DequeueNextUnique(_friendInviteSeeds, SocialListTab.Friend);
            if (nextFriend == null)
            {
                return "No additional simulator friends are waiting for invitation.";
            }

            _entriesByTab[SocialListTab.Friend].Add(nextFriend);
            SelectTab(SocialListTab.Friend);
            _selectedIndexByTab[SocialListTab.Friend] = GetFilteredEntries(SocialListTab.Friend).Count - 1;
            EnsureSelectionVisible(SocialListTab.Friend, GetFilteredEntries(SocialListTab.Friend).Count);
            return $"{nextFriend.Name} was added to the friend roster.";
        }

        private string SendFriendChat()
        {
            SocialEntryState selectedFriend = GetSelectedEntry(SocialListTab.Friend);
            if (selectedFriend == null)
            {
                return "Select a friend entry before opening friend chat.";
            }

            string message = $"[Friend] {_playerName} -> {selectedFriend.Name}: Checking in from {_locationSummary}.";
            NotifySocialChatObserved(message);
            return message;
        }

        private string GroupWhisperFriend()
        {
            SocialEntryState selectedFriend = GetSelectedEntry(SocialListTab.Friend);
            if (selectedFriend == null)
            {
                return "Select a friend entry before using group whisper.";
            }

            if (!TryGetFriendGroupLabel(selectedFriend.Name, out string friendGroupLabel))
            {
                return $"{selectedFriend.Name} is not in a local friend group yet. Use Add Group first.";
            }

            string message = $"[Friend Group:{friendGroupLabel}] {_playerName}: regroup around {selectedFriend.LocationSummary}.";
            NotifySocialChatObserved(message);
            return message;
        }

        private string ModifyFriendMemo()
        {
            SocialEntryState selectedFriend = GetSelectedEntry(SocialListTab.Friend);
            if (selectedFriend == null)
            {
                return "Select a friend entry before editing its note.";
            }

            return $"Friend note for {selectedFriend.Name} updated to \"Seen in {_locationSummary}\".";
        }

        private string AddFriendGroup()
        {
            SocialEntryState selectedFriend = GetSelectedEntry(SocialListTab.Friend);
            if (selectedFriend == null || selectedFriend.IsLocalPlayer)
            {
                return "Select a non-local friend entry before adding or assigning a friend group.";
            }

            if (TryGetFriendGroupLabel(selectedFriend.Name, out string existingGroupLabel))
            {
                return $"{selectedFriend.Name} already belongs to the local friend group \"{existingGroupLabel}\".";
            }

            string groupName = $"FriendGroup {_nextFriendGroupNumber++}";
            _friendGroups.Add(groupName);
            _friendGroupByName[selectedFriend.Name] = groupName;
            return $"{selectedFriend.Name} now belongs to the local friend group \"{groupName}\".";
        }

        private string InviteFriendToParty()
        {
            if (TryStagePacketOwnedRequest(SocialListTab.Party, "Party invite", out string requestMessage))
            {
                return requestMessage;
            }

            SocialEntryState selectedFriend = GetSelectedEntry(SocialListTab.Friend);
            if (selectedFriend == null || selectedFriend.IsLocalPlayer)
            {
                return "Select a friend entry before inviting it into the party list.";
            }

            if (_entriesByTab[SocialListTab.Party].Any(entry => string.Equals(entry.Name, selectedFriend.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return $"{selectedFriend.Name} is already in the party roster.";
            }

            _entriesByTab[SocialListTab.Party].Add(new SocialEntryState(
                selectedFriend.Name,
                selectedFriend.PrimaryText,
                "Joined from friend list",
                selectedFriend.LocationSummary,
                selectedFriend.Channel,
                selectedFriend.IsOnline,
                false,
                false));
            return $"{selectedFriend.Name} was invited from the friend tab into the party roster.";
        }

        private string InviteSelectedEntryToPartyFromTab(SocialListTab sourceTab, string owner)
        {
            if (TryStagePacketOwnedRequest(SocialListTab.Party, "Party invite", out string requestMessage))
            {
                return requestMessage;
            }

            if (!HasLocalPartyLeader())
            {
                return $"Party invite from the {owner} tab needs a local party leader entry first.";
            }

            SocialEntryState selectedEntry = GetSelectedEntry(sourceTab);
            if (selectedEntry == null || selectedEntry.IsLocalPlayer)
            {
                return $"Select a non-local {owner} entry before inviting it into the party roster.";
            }

            if (!selectedEntry.IsOnline)
            {
                return $"{selectedEntry.Name} is offline, so the {owner} tab cannot route a party invite right now.";
            }

            if (_entriesByTab[SocialListTab.Party].Any(entry => string.Equals(entry.Name, selectedEntry.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return $"{selectedEntry.Name} is already in the party roster.";
            }

            _entriesByTab[SocialListTab.Party].Add(new SocialEntryState(
                selectedEntry.Name,
                selectedEntry.PrimaryText,
                $"Invited from {owner} tab",
                selectedEntry.LocationSummary,
                selectedEntry.Channel,
                selectedEntry.IsOnline,
                false,
                false));
            _selectedIndexByTab[SocialListTab.Party] = _entriesByTab[SocialListTab.Party].Count - 1;
            EnsureSelectionVisible(SocialListTab.Party, _entriesByTab[SocialListTab.Party].Count);

            return $"{selectedEntry.Name} was invited from the {owner} tab into the party roster.";
        }

        private string WhisperSelected(string owner)
        {
            SocialEntryState selectedEntry = GetSelectedEntry(_currentTab);
            if (selectedEntry == null)
            {
                return $"Select a {owner} entry before whispering.";
            }

            string message = $"[Whisper] {_playerName} -> {selectedEntry.Name}: Meet in {selectedEntry.LocationSummary}.";
            NotifySocialChatObserved(message);
            return message;
        }

        private string MemoSelectedFriend()
        {
            SocialEntryState selectedFriend = GetSelectedEntry(SocialListTab.Friend);
            if (selectedFriend == null)
            {
                return "Select a friend entry before sending a memo.";
            }

            return $"Memo prepared for {selectedFriend.Name}. Compose/send parity still belongs to the mailbox follow-up work.";
        }

        private string DeleteFriend()
        {
            if (TryStagePacketOwnedRequest(SocialListTab.Friend, "Friend delete", out string requestMessage))
            {
                return requestMessage;
            }

            SocialEntryState selectedFriend = GetSelectedEntry(SocialListTab.Friend);
            if (selectedFriend == null || selectedFriend.IsLocalPlayer)
            {
                return "Select a removable friend entry before deleting it.";
            }

            _entriesByTab[SocialListTab.Friend].RemoveAll(entry => string.Equals(entry.Name, selectedFriend.Name, StringComparison.OrdinalIgnoreCase));
            ResetSelectionAfterMutation(SocialListTab.Friend);
            return $"{selectedFriend.Name} was removed from the friend roster.";
        }

        private string BlockFriend()
        {
            if (TryStagePacketOwnedRequest(SocialListTab.Blacklist, "Blacklist add", out string requestMessage))
            {
                return requestMessage;
            }

            SocialEntryState selectedFriend = GetSelectedEntry(SocialListTab.Friend);
            if (selectedFriend == null || selectedFriend.IsLocalPlayer)
            {
                return "Select a friend entry before blocking it.";
            }

            if (_entriesByTab[SocialListTab.Blacklist].Any(entry => string.Equals(entry.Name, selectedFriend.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return $"{selectedFriend.Name} is already in the blacklist.";
            }

            _entriesByTab[SocialListTab.Blacklist].Add(new SocialEntryState(
                selectedFriend.Name,
                "Blacklisted",
                "Blocked from friend contact",
                selectedFriend.LocationSummary,
                selectedFriend.Channel,
                selectedFriend.IsOnline,
                false,
                true));
            return $"{selectedFriend.Name} was copied into the blacklist.";
        }

        private string UnblockFriend()
        {
            if (TryStagePacketOwnedRequest(SocialListTab.Blacklist, "Blacklist remove", out string requestMessage))
            {
                return requestMessage;
            }

            SocialEntryState selectedFriend = GetSelectedEntry(SocialListTab.Friend);
            if (selectedFriend == null)
            {
                return "Select a friend entry before unblocking it.";
            }

            _entriesByTab[SocialListTab.Blacklist].RemoveAll(entry => string.Equals(entry.Name, selectedFriend.Name, StringComparison.OrdinalIgnoreCase));
            return $"{selectedFriend.Name} was removed from the blacklist.";
        }

        private string CreateParty()
        {
            if (TryStagePacketOwnedRequest(SocialListTab.Party, "Party create", out string requestMessage))
            {
                return requestMessage;
            }

            if (_entriesByTab[SocialListTab.Party].Any(entry => entry.IsLocalPlayer))
            {
                return "Party shell already owns a local leader entry.";
            }

            _entriesByTab[SocialListTab.Party].Insert(0, new SocialEntryState(
                _playerName,
                "Leader",
                "Created locally",
                _locationSummary,
                _channel,
                true,
                true,
                false)
            {
                IsLocalPlayer = true
            });
            ResetSelectionAfterMutation(SocialListTab.Party);
            return "Party roster seeded with the local player as leader.";
        }

        private string AddPartyMember()
        {
            if (TryStagePacketOwnedRequest(SocialListTab.Party, "Party invite", out string requestMessage))
            {
                return requestMessage;
            }

            SocialEntryState friendCandidate = _entriesByTab[SocialListTab.Friend]
                .FirstOrDefault(entry => !entry.IsLocalPlayer &&
                                         !_entriesByTab[SocialListTab.Party].Any(member => string.Equals(member.Name, entry.Name, StringComparison.OrdinalIgnoreCase)));
            if (friendCandidate == null)
            {
                return "No additional friend entries are available for the party roster.";
            }

            _entriesByTab[SocialListTab.Party].Add(new SocialEntryState(
                friendCandidate.Name,
                friendCandidate.PrimaryText,
                "Invited from party tab",
                friendCandidate.LocationSummary,
                friendCandidate.Channel,
                friendCandidate.IsOnline,
                false,
                false));
            return $"{friendCandidate.Name} joined the party roster.";
        }

        private string RemovePartyMember(bool localWithdraw)
        {
            if (TryStagePacketOwnedRequest(SocialListTab.Party, localWithdraw ? "Party withdraw" : "Party kick", out string requestMessage))
            {
                return requestMessage;
            }

            SocialEntryState selectedEntry = GetSelectedEntry(SocialListTab.Party);
            if (selectedEntry == null)
            {
                return "Select a party member before removing it.";
            }

            if (localWithdraw && !selectedEntry.IsLocalPlayer)
            {
                return "Select the local player row to test party withdrawal.";
            }

            if (!localWithdraw && selectedEntry.IsLocalPlayer)
            {
                return "Select a non-local party member to kick it from the roster.";
            }

            _entriesByTab[SocialListTab.Party].RemoveAll(entry => string.Equals(entry.Name, selectedEntry.Name, StringComparison.OrdinalIgnoreCase));
            ResetSelectionAfterMutation(SocialListTab.Party);
            return localWithdraw
                ? "Local player withdrew from the party roster."
                : $"{selectedEntry.Name} was removed from the party roster.";
        }

        private string SendPartyChat()
        {
            int onlineCount = _entriesByTab[SocialListTab.Party].Count(entry => entry.IsOnline);
            string message = $"[Party] {_playerName}: Ready check sent to {onlineCount} visible party member(s).";
            NotifySocialChatObserved(message);
            return message;
        }

        private string ChangePartyLeader()
        {
            if (TryStagePacketOwnedRequest(SocialListTab.Party, "Party leader change", out string requestMessage))
            {
                return requestMessage;
            }

            SocialEntryState selectedEntry = GetSelectedEntry(SocialListTab.Party);
            if (selectedEntry == null)
            {
                return "Select a party member before changing the leader.";
            }

            List<SocialEntryState> partyEntries = _entriesByTab[SocialListTab.Party];
            for (int i = 0; i < partyEntries.Count; i++)
            {
                SocialEntryState current = partyEntries[i];
                partyEntries[i] = new SocialEntryState(
                    current.Name,
                    current.PrimaryText,
                    current.SecondaryText,
                    current.LocationSummary,
                    current.Channel,
                    current.IsOnline,
                    string.Equals(current.Name, selectedEntry.Name, StringComparison.OrdinalIgnoreCase),
                    current.IsBlocked)
                {
                    IsLocalPlayer = current.IsLocalPlayer
                };
            }

            return $"{selectedEntry.Name} now owns the simulated party leader marker.";
        }

        private string AddGuildMember()
        {
            if (TryStagePacketOwnedRequest(SocialListTab.Guild, "Guild invite", out string requestMessage))
            {
                return requestMessage;
            }

            if (!(CanManageGuildRanks() || CanToggleGuildAdmission()))
            {
                return $"Guild invite is read-only while the active authority role is {GetEffectiveGuildRoleLabel()}.";
            }

            SocialEntryState recruit = DequeueNextUnique(_guildInviteSeeds, SocialListTab.Guild);
            if (recruit == null)
            {
                return "No additional simulator guild recruits are waiting.";
            }

            _entriesByTab[SocialListTab.Guild].Add(recruit);
            return $"{recruit.Name} joined guild {_guildName}.";
        }

        private string RemoveGuildMember(string owner)
        {
            SocialListTab tab = _currentTab;
            if (TryStagePacketOwnedRequest(tab, owner == "alliance" ? "Alliance remove" : "Guild remove", out string requestMessage))
            {
                return requestMessage;
            }

            SocialEntryState selectedEntry = GetSelectedEntry(tab);
            if (selectedEntry == null)
            {
                return $"Select a {owner} entry before removing it.";
            }

            if (!selectedEntry.IsLocalPlayer)
            {
                bool canRemoveRemoteEntry = tab == SocialListTab.Guild
                    ? CanManageGuildRanks()
                    : CanEditAllianceRanks();
                if (!canRemoveRemoteEntry)
                {
                    string roleLabel = tab == SocialListTab.Guild
                        ? GetEffectiveGuildRoleLabel()
                        : GetEffectiveAllianceRoleLabel();
                    return $"{owner} removal is read-only while the active authority role is {roleLabel}.";
                }
            }

            if (selectedEntry.IsLocalPlayer)
            {
                return tab == SocialListTab.Guild
                    ? LeaveLocalGuild()
                    : LeaveLocalAlliance();
            }

            _entriesByTab[tab].RemoveAll(entry => string.Equals(entry.Name, selectedEntry.Name, StringComparison.OrdinalIgnoreCase));
            ResetSelectionAfterMutation(tab);
            return $"{selectedEntry.Name} was removed from the {owner} roster.";
        }

        private string LeaveLocalGuild()
        {
            _forceNoGuildMembership = true;
            _forceNoAllianceMembership = true;
            _hasGuildMembership = false;
            _guildName = "No Guild";
            _allianceName = "No Alliance";
            _entriesByTab[SocialListTab.Guild].Clear();
            _entriesByTab[SocialListTab.Alliance].Clear();
            ResetSelectionAfterMutation(SocialListTab.Guild);
            ResetSelectionAfterMutation(SocialListTab.Alliance);
            return "Local guild membership was cleared, and the alliance roster was closed with it.";
        }

        private string LeaveLocalAlliance()
        {
            _forceNoAllianceMembership = true;
            _allianceName = "No Alliance";
            _entriesByTab[SocialListTab.Alliance].Clear();
            ResetSelectionAfterMutation(SocialListTab.Alliance);
            return "Local alliance membership was cleared from the union roster.";
        }

        private string LocateSelected(string owner)
        {
            SocialEntryState selectedEntry = GetSelectedEntry(_currentTab);
            if (selectedEntry == null)
            {
                return $"Select a {owner} entry before locating it.";
            }

            return $"{selectedEntry.Name} is listed in {selectedEntry.LocationSummary} on channel {selectedEntry.Channel}.";
        }

        private string ShowSelectedInfo(string owner)
        {
            SocialEntryState selectedEntry = GetSelectedEntry(_currentTab);
            if (selectedEntry == null)
            {
                return $"Select a {owner} entry before requesting info.";
            }

            return $"{owner} info: {selectedEntry.Name}  {selectedEntry.PrimaryText}  {selectedEntry.SecondaryText}.";
        }

        private string AdjustGuildGrade(int delta)
        {
            if (TryStagePacketOwnedRequest(SocialListTab.Guild, delta > 0 ? "Guild grade up" : "Guild grade down", out string requestMessage))
            {
                return requestMessage;
            }

            if (!CanManageGuildRanks())
            {
                return $"Guild grade changes are read-only while the active authority role is {GetEffectiveGuildRoleLabel()}.";
            }

            SocialEntryState selectedEntry = GetSelectedEntry(SocialListTab.Guild);
            if (selectedEntry == null)
            {
                return "Select a guild member before changing its rank.";
            }

            if (!TryResolveNextRankTitle(_guildRankTitles, selectedEntry, delta, out int currentIndex, out int nextIndex, out string nextTitle))
            {
                return delta > 0
                    ? $"{selectedEntry.Name} already has the highest simulated guild grade."
                    : $"{selectedEntry.Name} already has the lowest simulated guild grade.";
            }

            ReplaceEntry(
                SocialListTab.Guild,
                selectedEntry.Name,
                new SocialEntryState(
                    selectedEntry.Name,
                    nextTitle,
                    selectedEntry.SecondaryText,
                    selectedEntry.LocationSummary,
                    selectedEntry.Channel,
                    selectedEntry.IsOnline,
                    selectedEntry.IsLeader && nextIndex == 0,
                    selectedEntry.IsBlocked)
                {
                    IsLocalPlayer = selectedEntry.IsLocalPlayer
                });
            string message = delta > 0
                ? $"{selectedEntry.Name} advanced from guild rank title {currentIndex + 1} to {nextIndex + 1} ({nextTitle})."
                : $"{selectedEntry.Name} moved down from guild rank title {currentIndex + 1} to {nextIndex + 1} ({nextTitle}).";
            NotifySocialChatObserved(message);
            return message;
        }

        private string AddAllianceMember()
        {
            if (TryStagePacketOwnedRequest(SocialListTab.Alliance, "Alliance invite", out string requestMessage))
            {
                return requestMessage;
            }

            if (!CanEditAllianceRanks())
            {
                return $"Alliance invite is read-only while the active authority role is {GetEffectiveAllianceRoleLabel()}.";
            }

            SocialEntryState recruit = DequeueNextUnique(_allianceInviteSeeds, SocialListTab.Alliance);
            if (recruit == null)
            {
                return "No additional simulator alliance entries are waiting.";
            }

            _entriesByTab[SocialListTab.Alliance].Add(recruit);
            return $"{recruit.Name} joined alliance {_allianceName}.";
        }

        private string SendAllianceChat()
        {
            int onlineCount = _entriesByTab[SocialListTab.Alliance].Count(entry => entry.IsOnline);
            string message = $"[Alliance] {_playerName}: Union notice check sent across {onlineCount} visible alliance entries.";
            NotifySocialChatObserved(message);
            return message;
        }

        private string AdjustAllianceGrade(int delta)
        {
            if (TryStagePacketOwnedRequest(SocialListTab.Alliance, delta > 0 ? "Alliance grade up" : "Alliance grade down", out string requestMessage))
            {
                return requestMessage;
            }

            if (!CanEditAllianceRanks())
            {
                return $"Alliance grade changes are read-only while the active authority role is {GetEffectiveAllianceRoleLabel()}.";
            }

            SocialEntryState selectedEntry = GetSelectedEntry(SocialListTab.Alliance);
            if (selectedEntry == null)
            {
                return "Select an alliance entry before changing its rank.";
            }

            if (!TryResolveNextRankTitle(_allianceRankTitles, selectedEntry, delta, out int currentIndex, out int nextIndex, out string nextTitle))
            {
                return delta > 0
                    ? $"{selectedEntry.Name} already has the highest simulated alliance grade."
                    : $"{selectedEntry.Name} already has the lowest simulated alliance grade.";
            }

            ReplaceEntry(
                SocialListTab.Alliance,
                selectedEntry.Name,
                new SocialEntryState(
                    selectedEntry.Name,
                    nextTitle,
                    selectedEntry.SecondaryText,
                    selectedEntry.LocationSummary,
                    selectedEntry.Channel,
                    selectedEntry.IsOnline,
                    selectedEntry.IsLeader && nextIndex == 0,
                    selectedEntry.IsBlocked)
                {
                    IsLocalPlayer = selectedEntry.IsLocalPlayer
                });
            string message = delta > 0
                ? $"{selectedEntry.Name} advanced from alliance rank title {currentIndex + 1} to {nextIndex + 1} ({nextTitle})."
                : $"{selectedEntry.Name} moved down from alliance rank title {currentIndex + 1} to {nextIndex + 1} ({nextTitle}).";
            NotifySocialChatObserved(message);
            return message;
        }

        private void NotifySocialChatObserved(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            SocialChatObserved?.Invoke(message, Environment.TickCount);
        }

        private string AddBlacklistEntry()
        {
            if (TryStagePacketOwnedRequest(SocialListTab.Blacklist, "Blacklist add", out string requestMessage))
            {
                return requestMessage;
            }

            SocialEntryState selectedFriend = GetSelectedEntry(SocialListTab.Friend);
            if (selectedFriend != null && !selectedFriend.IsLocalPlayer)
            {
                if (_entriesByTab[SocialListTab.Blacklist].Any(entry => string.Equals(entry.Name, selectedFriend.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    return $"{selectedFriend.Name} is already blocked.";
                }

                _entriesByTab[SocialListTab.Blacklist].Add(new SocialEntryState(
                    selectedFriend.Name,
                    "Blacklisted",
                    "Blocked from friend tab",
                    selectedFriend.LocationSummary,
                    selectedFriend.Channel,
                    selectedFriend.IsOnline,
                    false,
                    true));
                return $"{selectedFriend.Name} was added to the blacklist.";
            }

            SocialEntryState seed = DequeueNextUnique(_blacklistSeeds, SocialListTab.Blacklist);
            if (seed == null)
            {
                return "No additional blacklist seeds are available.";
            }

            _entriesByTab[SocialListTab.Blacklist].Add(seed);
            return $"{seed.Name} was added to the blacklist.";
        }

        private string DeleteBlacklistEntry()
        {
            if (TryStagePacketOwnedRequest(SocialListTab.Blacklist, "Blacklist delete", out string requestMessage))
            {
                return requestMessage;
            }

            SocialEntryState selectedEntry = GetSelectedEntry(SocialListTab.Blacklist);
            if (selectedEntry == null)
            {
                return "Select a blacklist entry before deleting it.";
            }

            _entriesByTab[SocialListTab.Blacklist].RemoveAll(entry => string.Equals(entry.Name, selectedEntry.Name, StringComparison.OrdinalIgnoreCase));
            ResetSelectionAfterMutation(SocialListTab.Blacklist);
            return $"{selectedEntry.Name} was removed from the blacklist.";
        }

        private SocialEntryState GetSelectedEntry(SocialListTab tab)
        {
            IReadOnlyList<SocialEntryState> entries = GetFilteredEntries(tab);
            int selectedIndex = GetSelectedIndex(tab, entries.Count);
            return selectedIndex >= 0 && selectedIndex < entries.Count ? entries[selectedIndex] : null;
        }

        private void ReplaceEntry(SocialListTab tab, string entryName, SocialEntryState replacement)
        {
            if (string.IsNullOrWhiteSpace(entryName) || replacement == null)
            {
                return;
            }

            List<SocialEntryState> entries = _entriesByTab[tab];
            int index = entries.FindIndex(entry => string.Equals(entry.Name, entryName, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                entries[index] = replacement;
            }
        }

        private static bool TryResolveNextRankTitle(
            IReadOnlyList<string> rankTitles,
            SocialEntryState selectedEntry,
            int delta,
            out int currentIndex,
            out int nextIndex,
            out string nextTitle)
        {
            currentIndex = ResolveCurrentRankIndex(rankTitles, selectedEntry);
            nextIndex = currentIndex;
            nextTitle = selectedEntry?.PrimaryText ?? string.Empty;
            if (selectedEntry == null || rankTitles == null || rankTitles.Count == 0 || delta == 0)
            {
                return false;
            }

            nextIndex = Math.Clamp(currentIndex - delta, 0, rankTitles.Count - 1);
            nextTitle = rankTitles[nextIndex];
            return nextIndex != currentIndex;
        }

        private static int ResolveCurrentRankIndex(IReadOnlyList<string> rankTitles, SocialEntryState selectedEntry)
        {
            if (rankTitles == null || rankTitles.Count == 0 || selectedEntry == null)
            {
                return 0;
            }

            for (int i = 0; i < rankTitles.Count; i++)
            {
                if (string.Equals(rankTitles[i], selectedEntry.PrimaryText, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            if (selectedEntry.IsLeader)
            {
                return 0;
            }

            return Math.Max(0, rankTitles.Count - 1);
        }

        private void ResetSelectionAfterMutation(SocialListTab tab)
        {
            IReadOnlyList<SocialEntryState> entries = GetFilteredEntries(tab);
            _selectedIndexByTab[tab] = entries.Count > 0 ? Math.Clamp(GetSelectedIndex(tab, entries.Count), 0, entries.Count - 1) : -1;
            EnsureSelectionVisible(tab, entries.Count);
        }

        private bool TryGetFriendGroupLabel(string friendName, out string groupLabel)
        {
            groupLabel = null;
            if (string.IsNullOrWhiteSpace(friendName))
            {
                return false;
            }

            return _friendGroupByName.TryGetValue(friendName.Trim(), out groupLabel)
                   && !string.IsNullOrWhiteSpace(groupLabel);
        }

        private SocialEntryState DequeueNextUnique(Queue<SocialEntryState> queue, SocialListTab tab)
        {
            int count = queue.Count;
            while (count-- > 0)
            {
                SocialEntryState candidate = queue.Dequeue();
                queue.Enqueue(candidate);
                if (_entriesByTab[tab].Any(entry => string.Equals(entry.Name, candidate.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }

        private sealed class SocialEntryState
        {
            public SocialEntryState(string name, string primaryText, string secondaryText, string locationSummary, int channel, bool isOnline, bool isLeader, bool isBlocked)
            {
                Name = name;
                PrimaryText = primaryText;
                SecondaryText = secondaryText;
                LocationSummary = locationSummary;
                Channel = channel;
                IsOnline = isOnline;
                IsLeader = isLeader;
                IsBlocked = isBlocked;
            }

            public string Name { get; }
            public string PrimaryText { get; }
            public string SecondaryText { get; }
            public string LocationSummary { get; }
            public int Channel { get; }
            public bool IsOnline { get; }
            public bool IsLeader { get; }
            public bool IsBlocked { get; }
            public int? MemberId { get; init; }
            public bool IsLocalPlayer { get; init; }
        }
    }

    internal sealed class SocialListSnapshot
    {
        public SocialListTab CurrentTab { get; set; }
        public IReadOnlyList<SocialListEntrySnapshot> Entries { get; set; } = Array.Empty<SocialListEntrySnapshot>();
        public int SelectedVisibleIndex { get; set; } = -1;
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalEntries { get; set; }
        public string HeaderTitle { get; set; } = string.Empty;
        public IReadOnlyList<string> SummaryLines { get; set; } = Array.Empty<string>();
        public IReadOnlyCollection<string> EnabledActionKeys { get; set; } = Array.Empty<string>();
        public bool FriendOnlineOnly { get; set; }
        public bool CanPageBackward { get; set; }
        public bool CanPageForward { get; set; }
        public int FirstVisibleIndex { get; set; }
        public int MaxFirstVisibleIndex { get; set; }
        public int VisibleCapacity { get; set; }
    }

    internal sealed class SocialListEntrySnapshot
    {
        public string Name { get; set; } = string.Empty;
        public string PrimaryText { get; set; } = string.Empty;
        public string SecondaryText { get; set; } = string.Empty;
        public string LocationSummary { get; set; } = string.Empty;
        public int Channel { get; set; }
        public bool IsOnline { get; set; }
        public bool IsLeader { get; set; }
        public bool IsLocalPlayer { get; set; }
    }

    internal sealed class SocialTrackedEntrySnapshot
    {
        public SocialListTab Tab { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PrimaryText { get; set; } = string.Empty;
        public string SecondaryText { get; set; } = string.Empty;
        public string LocationSummary { get; set; } = string.Empty;
        public int Channel { get; set; }
        public bool IsOnline { get; set; }
        public bool IsLeader { get; set; }
        public bool IsLocalPlayer { get; set; }
    }
}
