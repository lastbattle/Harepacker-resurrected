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

        private readonly Dictionary<SocialListTab, List<SocialEntryState>> _entriesByTab = new();
        private readonly Dictionary<SocialListTab, int> _selectedIndexByTab = new();
        private readonly Dictionary<SocialListTab, int> _pageByTab = new();
        private readonly Queue<SocialEntryState> _friendInviteSeeds = new();
        private readonly Queue<SocialEntryState> _guildInviteSeeds = new();
        private readonly Queue<SocialEntryState> _allianceInviteSeeds = new();
        private readonly Queue<SocialEntryState> _blacklistSeeds = new();
        private string _playerName = "Player";
        private string _locationSummary = "Maple Island";
        private string _guildName = "Maple GM";
        private string _allianceName = "Maple Union";
        private int _channel = 1;
        private bool _friendOnlineOnly;
        private SocialListTab _currentTab = SocialListTab.Friend;

        internal SocialListRuntime()
        {
            SeedDefaultData();
        }

        internal void UpdateLocalContext(CharacterBuild build, string locationSummary, int channel)
        {
            _playerName = string.IsNullOrWhiteSpace(build?.Name) ? "Player" : build.Name.Trim();
            _locationSummary = string.IsNullOrWhiteSpace(locationSummary) ? "Field" : locationSummary.Trim();
            _guildName = string.IsNullOrWhiteSpace(build?.GuildDisplayText) ? "Maple GM" : build.GuildDisplayText.Trim();
            _allianceName = string.IsNullOrWhiteSpace(build?.AllianceDisplayText) ? "Maple Union" : build.AllianceDisplayText.Trim();
            _channel = Math.Max(1, channel);

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
            UpdateOrInsertLocalEntry(
                SocialListTab.Guild,
                new SocialEntryState(_playerName, "Master", _guildName, _locationSummary, _channel, true, true, false)
                {
                    IsLocalPlayer = true
                });
            UpdateOrInsertLocalEntry(
                SocialListTab.Alliance,
                new SocialEntryState(_playerName, "Representative", _allianceName, _guildName, _channel, true, true, false)
                {
                    IsLocalPlayer = true
                });
        }

        internal SocialListSnapshot BuildSnapshot()
        {
            IReadOnlyList<SocialEntryState> tabEntries = GetFilteredEntries(_currentTab);
            int selectedIndex = GetSelectedIndex(_currentTab, tabEntries.Count);
            int totalPages = Math.Max(1, (int)Math.Ceiling(tabEntries.Count / (float)PageSize));
            int page = Math.Clamp(_pageByTab.TryGetValue(_currentTab, out int currentPage) ? currentPage : 0, 0, totalPages - 1);
            _pageByTab[_currentTab] = page;

            IReadOnlyList<SocialEntryState> pageEntries = tabEntries
                .Skip(page * PageSize)
                .Take(PageSize)
                .ToArray();
            int pageSelectedIndex = selectedIndex >= page * PageSize && selectedIndex < ((page + 1) * PageSize)
                ? selectedIndex - (page * PageSize)
                : -1;
            SocialEntryState selectedEntry = selectedIndex >= 0 && selectedIndex < tabEntries.Count ? tabEntries[selectedIndex] : null;

            return new SocialListSnapshot
            {
                CurrentTab = _currentTab,
                Entries = pageEntries.Select(CreateEntrySnapshot).ToArray(),
                SelectedVisibleIndex = pageSelectedIndex,
                Page = page + 1,
                TotalPages = totalPages,
                TotalEntries = tabEntries.Count,
                HeaderTitle = GetHeaderTitle(),
                SummaryLines = BuildSummaryLines(selectedEntry),
                EnabledActionKeys = GetEnabledActions(selectedEntry).ToArray(),
                FriendOnlineOnly = _friendOnlineOnly,
                CanPageBackward = page > 0,
                CanPageForward = page < totalPages - 1
            };
        }

        internal IReadOnlyList<SocialTrackedEntrySnapshot> BuildTrackedEntriesSnapshot()
        {
            List<SocialTrackedEntrySnapshot> entries = new();
            foreach (SocialListTab tab in new[] { SocialListTab.Friend, SocialListTab.Party, SocialListTab.Guild })
            {
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

                    entries.Add(new SocialTrackedEntrySnapshot
                    {
                        Tab = tab,
                        Name = entry.Name,
                        LocationSummary = entry.LocationSummary,
                        Channel = entry.Channel,
                        IsOnline = entry.IsOnline,
                        IsLeader = entry.IsLeader,
                        IsLocalPlayer = entry.IsLocalPlayer
                    });
                }
            }

            return entries;
        }

        internal void SelectTab(SocialListTab tab)
        {
            _currentTab = tab;
            int count = GetFilteredEntries(tab).Count;
            _selectedIndexByTab[tab] = count > 0 ? Math.Clamp(GetSelectedIndex(tab, count), 0, count - 1) : -1;
            ClampPage(tab, count);
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

            int page = _pageByTab.TryGetValue(_currentTab, out int currentPage) ? currentPage : 0;
            int absoluteIndex = (page * PageSize) + visibleIndex;
            if (absoluteIndex >= 0 && absoluteIndex < entries.Count)
            {
                _selectedIndexByTab[_currentTab] = absoluteIndex;
            }
        }

        internal void MovePage(int delta)
        {
            IReadOnlyList<SocialEntryState> entries = GetFilteredEntries(_currentTab);
            if (entries.Count <= PageSize)
            {
                _pageByTab[_currentTab] = 0;
                return;
            }

            int totalPages = Math.Max(1, (int)Math.Ceiling(entries.Count / (float)PageSize));
            int page = _pageByTab.TryGetValue(_currentTab, out int currentPage) ? currentPage : 0;
            _pageByTab[_currentTab] = Math.Clamp(page + delta, 0, totalPages - 1);
        }

        internal void SetFriendOnlineOnly(bool onlineOnly)
        {
            _friendOnlineOnly = onlineOnly;
            int count = GetFilteredEntries(SocialListTab.Friend).Count;
            _selectedIndexByTab[SocialListTab.Friend] = count > 0 ? 0 : -1;
            _pageByTab[SocialListTab.Friend] = 0;
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
                "Friend.AddGroup" => "Friend-group folders are visible in the WZ shell, but packet-backed group editing still remains out of scope.",
                "Friend.Party" => InviteFriendToParty(),
                "Friend.Chat" => SendFriendChat(),
                "Friend.Whisper" => WhisperSelected("friend"),
                "Friend.GroupWhisper" => GroupWhisperFriend(),
                "Friend.Mate" => "Couple and mate flows still need their own dedicated relationship runtime.",
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
                "Guild.PartyInvite" => "Guild party invite forwarded from the member list shell.",
                "Guild.GradeUp" => AdjustGuildGrade(1),
                "Guild.GradeDown" => AdjustGuildGrade(-1),
                "Guild.Kick" => RemoveGuildMember("guild"),
                "Guild.Where" => LocateSelected("guild"),
                "Guild.Whisper" => WhisperSelected("guild"),
                "Guild.Info" => ShowSelectedInfo("guild"),
                "Guild.Skill" => "Guild skill management remains a separate `GuildSkill` surface, but the button now resolves as a dedicated social action.",
                "Guild.Search" => null,
                "Guild.Manage" => "Guild management uses a separate `GuildManage` window family that still remains unmodeled.",
                "Guild.Change" => "Guild notice and emblem change tools still need their dedicated edit flow.",
                "Alliance.Invite" => AddAllianceMember(),
                "Alliance.Withdraw" => RemoveGuildMember("alliance"),
                "Alliance.PartyInvite" => "Alliance party invite forwarded from the union tab.",
                "Alliance.GradeUp" => "Alliance grade promotion still needs the packet-authored union rank model.",
                "Alliance.GradeDown" => "Alliance grade demotion still needs the packet-authored union rank model.",
                "Alliance.Kick" => RemoveGuildMember("alliance"),
                "Alliance.Change" => "Alliance notice and grade tools still need their dedicated edit flow.",
                "Alliance.Chat" => SendAllianceChat(),
                "Alliance.Whisper" => WhisperSelected("alliance"),
                "Alliance.Info" => ShowSelectedInfo("alliance"),
                "Alliance.Notice" => "Alliance notice editing still needs the dedicated notice editor behind the union tab.",
                "Blacklist.Add" => AddBlacklistEntry(),
                "Blacklist.Delete" => DeleteBlacklistEntry(),
                _ => "That social action is not modeled yet."
            };
        }

        internal string GetLocalGuildRoleLabel()
        {
            SocialEntryState localGuildEntry = _entriesByTab.TryGetValue(SocialListTab.Guild, out List<SocialEntryState> entries)
                ? entries.FirstOrDefault(entry => entry.IsLocalPlayer)
                : null;
            return string.IsNullOrWhiteSpace(localGuildEntry?.PrimaryText)
                ? "Member"
                : localGuildEntry.PrimaryText;
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
                _pageByTab[tab] = 0;
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

            _selectedIndexByTab[SocialListTab.Friend] = 0;
            _selectedIndexByTab[SocialListTab.Party] = 0;
            _selectedIndexByTab[SocialListTab.Guild] = 0;
            _selectedIndexByTab[SocialListTab.Alliance] = 0;
            _selectedIndexByTab[SocialListTab.Blacklist] = 0;
        }

        private void UpdateOrInsertLocalEntry(SocialListTab tab, SocialEntryState localEntry)
        {
            List<SocialEntryState> entries = _entriesByTab[tab];
            int existingIndex = entries.FindIndex(entry => entry.IsLocalPlayer);
            if (existingIndex >= 0)
            {
                entries[existingIndex] = localEntry;
            }
            else
            {
                entries.Insert(0, localEntry);
            }
        }

        private IReadOnlyList<SocialEntryState> GetFilteredEntries(SocialListTab tab)
        {
            List<SocialEntryState> entries = _entriesByTab[tab];
            if (tab == SocialListTab.Friend && _friendOnlineOnly)
            {
                return entries.Where(entry => entry.IsOnline).ToArray();
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

        private void ClampPage(SocialListTab tab, int entryCount)
        {
            int totalPages = Math.Max(1, (int)Math.Ceiling(entryCount / (float)PageSize));
            int page = _pageByTab.TryGetValue(tab, out int currentPage) ? currentPage : 0;
            _pageByTab[tab] = Math.Clamp(page, 0, totalPages - 1);
        }

        private SocialListEntrySnapshot CreateEntrySnapshot(SocialEntryState entry)
        {
            return new SocialListEntrySnapshot
            {
                Name = entry.Name,
                PrimaryText = entry.PrimaryText,
                SecondaryText = entry.SecondaryText,
                LocationSummary = entry.LocationSummary,
                Channel = entry.Channel,
                IsOnline = entry.IsOnline,
                IsLeader = entry.IsLeader,
                IsLocalPlayer = entry.IsLocalPlayer
            };
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

        private IReadOnlyList<string> BuildSummaryLines(SocialEntryState selectedEntry)
        {
            if (selectedEntry == null)
            {
                return _currentTab switch
                {
                    SocialListTab.Friend => new[] { "Friend tab mirrors the client's list shell.", "Use Show Online or page buttons to inspect roster slices.", $"{GetFilteredEntries(SocialListTab.Friend).Count} visible friend entries." },
                    SocialListTab.Party => new[] { "Party tab owns leader, member, and location summaries.", "Create, invite, and boss-change actions mutate the simulator roster.", $"{GetFilteredEntries(SocialListTab.Party).Count} visible party entries." },
                    SocialListTab.Guild => new[] { $"Guild: {_guildName}", "Member management is simulated locally from the UserList shell.", $"{GetFilteredEntries(SocialListTab.Guild).Count} guild members listed." },
                    SocialListTab.Alliance => new[] { $"Alliance: {_allianceName}", "Union/alliance data stays separate from the guild member list.", $"{GetFilteredEntries(SocialListTab.Alliance).Count} alliance entries listed." },
                    SocialListTab.Blacklist => new[] { "Blacklist entries stay isolated from the friend roster.", "Block and delete flows mutate the local simulator-owned list.", $"{GetFilteredEntries(SocialListTab.Blacklist).Count} blocked entries." },
                    _ => Array.Empty<string>()
                };
            }

            return new[]
            {
                selectedEntry.IsLocalPlayer ? $"{selectedEntry.Name} (You)" : selectedEntry.Name,
                $"{selectedEntry.PrimaryText}  {selectedEntry.SecondaryText}".Trim(),
                $"{selectedEntry.LocationSummary}  CH {selectedEntry.Channel}"
            };
        }

        private IEnumerable<string> GetEnabledActions(SocialEntryState selectedEntry)
        {
            switch (_currentTab)
            {
                case SocialListTab.Friend:
                    yield return "Friend.AddFriend";
                    if (selectedEntry != null)
                    {
                        yield return "Friend.Chat";
                        yield return "Friend.Whisper";
                        yield return "Friend.GroupWhisper";
                        yield return "Friend.Message";
                        yield return "Friend.Mod";
                        yield return _entriesByTab[SocialListTab.Blacklist].Any(entry => string.Equals(entry.Name, selectedEntry.Name, StringComparison.OrdinalIgnoreCase))
                            ? "Friend.UnBlock"
                            : "Friend.Block";
                        if (!selectedEntry.IsLocalPlayer)
                        {
                            yield return "Friend.AddGroup";
                            yield return "Friend.Mate";
                            yield return "Friend.Party";
                            yield return "Friend.Delete";
                        }
                    }
                    break;
                case SocialListTab.Party:
                    yield return "Party.Create";
                    yield return "Party.Invite";
                    yield return "Party.Chat";
                    if (selectedEntry != null)
                    {
                        yield return "Party.Whisper";
                        yield return "Party.ChangeBoss";
                        if (!selectedEntry.IsLocalPlayer)
                        {
                            yield return "Party.Kick";
                        }
                        else
                        {
                            yield return "Party.Withdraw";
                        }
                    }
                    yield return "Party.Search";
                    break;
                case SocialListTab.Guild:
                    yield return "Guild.Board";
                    yield return "Guild.Invite";
                    yield return "Guild.Search";
                    if (selectedEntry != null)
                    {
                        yield return "Guild.GradeUp";
                        yield return "Guild.GradeDown";
                        yield return "Guild.Where";
                        yield return "Guild.Whisper";
                        yield return "Guild.Info";
                        yield return "Guild.Skill";
                        yield return "Guild.Manage";
                        yield return "Guild.Change";
                        if (!selectedEntry.IsLocalPlayer)
                        {
                            yield return "Guild.PartyInvite";
                            yield return "Guild.Kick";
                        }
                        else
                        {
                            yield return "Guild.Withdraw";
                        }
                    }
                    break;
                case SocialListTab.Alliance:
                    yield return "Alliance.Invite";
                    if (selectedEntry != null)
                    {
                        yield return "Alliance.Chat";
                        yield return "Alliance.Whisper";
                        yield return "Alliance.Info";
                        yield return "Alliance.Change";
                        yield return "Alliance.Notice";
                        yield return "Alliance.GradeUp";
                        yield return "Alliance.GradeDown";
                        if (!selectedEntry.IsLocalPlayer)
                        {
                            yield return "Alliance.PartyInvite";
                            yield return "Alliance.Kick";
                        }
                        else
                        {
                            yield return "Alliance.Withdraw";
                        }
                    }
                    break;
                case SocialListTab.Blacklist:
                    yield return "Blacklist.Add";
                    if (selectedEntry != null)
                    {
                        yield return "Blacklist.Delete";
                    }
                    break;
            }
        }

        private string AddFriend()
        {
            SocialEntryState nextFriend = DequeueNextUnique(_friendInviteSeeds, SocialListTab.Friend);
            if (nextFriend == null)
            {
                return "No additional simulator friends are waiting for invitation.";
            }

            _entriesByTab[SocialListTab.Friend].Add(nextFriend);
            SelectTab(SocialListTab.Friend);
            _selectedIndexByTab[SocialListTab.Friend] = GetFilteredEntries(SocialListTab.Friend).Count - 1;
            ClampPage(SocialListTab.Friend, GetFilteredEntries(SocialListTab.Friend).Count);
            return $"{nextFriend.Name} was added to the friend roster.";
        }

        private string SendFriendChat()
        {
            SocialEntryState selectedFriend = GetSelectedEntry(SocialListTab.Friend);
            if (selectedFriend == null)
            {
                return "Select a friend entry before opening friend chat.";
            }

            return $"[Friend] {_playerName} -> {selectedFriend.Name}: Checking in from {_locationSummary}.";
        }

        private string GroupWhisperFriend()
        {
            SocialEntryState selectedFriend = GetSelectedEntry(SocialListTab.Friend);
            if (selectedFriend == null)
            {
                return "Select a friend entry before using group whisper.";
            }

            return $"Group whisper queued for {selectedFriend.Name}. Dedicated friend-group membership still remains packet-driven.";
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

        private string InviteFriendToParty()
        {
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

        private string WhisperSelected(string owner)
        {
            SocialEntryState selectedEntry = GetSelectedEntry(_currentTab);
            if (selectedEntry == null)
            {
                return $"Select a {owner} entry before whispering.";
            }

            return $"[Whisper] {_playerName} -> {selectedEntry.Name}: Meet in {selectedEntry.LocationSummary}.";
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
            return $"[Party] {_playerName}: Ready check sent to {onlineCount} visible party member(s).";
        }

        private string ChangePartyLeader()
        {
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
            SocialEntryState selectedEntry = GetSelectedEntry(tab);
            if (selectedEntry == null)
            {
                return $"Select a {owner} entry before removing it.";
            }

            if (selectedEntry.IsLocalPlayer)
            {
                return $"{owner} withdrawal remains a placeholder until the surrounding server-authored flow exists.";
            }

            _entriesByTab[tab].RemoveAll(entry => string.Equals(entry.Name, selectedEntry.Name, StringComparison.OrdinalIgnoreCase));
            ResetSelectionAfterMutation(tab);
            return $"{selectedEntry.Name} was removed from the {owner} roster.";
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
            SocialEntryState selectedEntry = GetSelectedEntry(SocialListTab.Guild);
            if (selectedEntry == null)
            {
                return "Select a guild member before changing its rank.";
            }

            string[] grades =
            {
                "Member",
                "Jr. Master",
                "Master"
            };

            int currentIndex = Array.FindIndex(grades, grade => string.Equals(grade, selectedEntry.PrimaryText, StringComparison.OrdinalIgnoreCase));
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int nextIndex = Math.Clamp(currentIndex + delta, 0, grades.Length - 1);
            if (nextIndex == currentIndex)
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
                    grades[nextIndex],
                    selectedEntry.SecondaryText,
                    selectedEntry.LocationSummary,
                    selectedEntry.Channel,
                    selectedEntry.IsOnline,
                    selectedEntry.IsLeader,
                    selectedEntry.IsBlocked)
                {
                    IsLocalPlayer = selectedEntry.IsLocalPlayer
                });
            return $"{selectedEntry.Name} now has simulated guild grade {grades[nextIndex]}.";
        }

        private string AddAllianceMember()
        {
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
            return $"[Alliance] {_playerName}: Union notice check sent across {onlineCount} visible alliance entries.";
        }

        private string AddBlacklistEntry()
        {
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

        private void ResetSelectionAfterMutation(SocialListTab tab)
        {
            IReadOnlyList<SocialEntryState> entries = GetFilteredEntries(tab);
            _selectedIndexByTab[tab] = entries.Count > 0 ? Math.Clamp(GetSelectedIndex(tab, entries.Count), 0, entries.Count - 1) : -1;
            ClampPage(tab, entries.Count);
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
            public bool IsLocalPlayer { get; init; }
        }
    }

    internal sealed class SocialListSnapshot
    {
        public SocialListTab CurrentTab { get; init; }
        public IReadOnlyList<SocialListEntrySnapshot> Entries { get; init; } = Array.Empty<SocialListEntrySnapshot>();
        public int SelectedVisibleIndex { get; init; } = -1;
        public int Page { get; init; } = 1;
        public int TotalPages { get; init; } = 1;
        public int TotalEntries { get; init; }
        public string HeaderTitle { get; init; } = string.Empty;
        public IReadOnlyList<string> SummaryLines { get; init; } = Array.Empty<string>();
        public IReadOnlyCollection<string> EnabledActionKeys { get; init; } = Array.Empty<string>();
        public bool FriendOnlineOnly { get; init; }
        public bool CanPageBackward { get; init; }
        public bool CanPageForward { get; init; }
    }

    internal sealed class SocialListEntrySnapshot
    {
        public string Name { get; init; } = string.Empty;
        public string PrimaryText { get; init; } = string.Empty;
        public string SecondaryText { get; init; } = string.Empty;
        public string LocationSummary { get; init; } = string.Empty;
        public int Channel { get; init; }
        public bool IsOnline { get; init; }
        public bool IsLeader { get; init; }
        public bool IsLocalPlayer { get; init; }
    }

    internal sealed class SocialTrackedEntrySnapshot
    {
        public SocialListTab Tab { get; init; }
        public string Name { get; init; } = string.Empty;
        public string LocationSummary { get; init; } = string.Empty;
        public int Channel { get; init; }
        public bool IsOnline { get; init; }
        public bool IsLeader { get; init; }
        public bool IsLocalPlayer { get; init; }
    }
}
