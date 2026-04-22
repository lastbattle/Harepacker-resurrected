using HaCreator.MapSimulator.Character;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum SocialSearchTab
    {
        Party = 0,
        PartyMember = 1,
        Expedition = 2
    }

    internal sealed partial class SocialListRuntime
    {
        private const int SearchPageSize = 6;
        private const int GuildSearchPageSize = 7;

        private readonly Dictionary<SocialSearchTab, List<SocialSearchEntryState>> _searchEntriesByTab = new();
        private readonly Dictionary<SocialSearchTab, int> _searchSelectedIndexByTab = new();
        private readonly Dictionary<SocialSearchTab, string> _searchSortByTab = new();
        private readonly Dictionary<int, SocialSearchEntryState> _characterInfoPartyEntryOverrides = new();
        private readonly List<GuildSearchEntryState> _guildSearchEntries = new();
        private readonly HashSet<string> _guildSearchWatchlist = new(StringComparer.OrdinalIgnoreCase);
        private CharacterInfoSearchLaunchState? _characterInfoSearchLaunch;
        private int _guildSearchSelectedIndex;
        private int _guildSearchPage;
        private bool _searchSeeded;
        private bool _guildSearchSeeded;
        private bool _searchSimilarLevelOnly = true;
        private bool _expeditionRegisterMode;
        private bool _expeditionAdmissionActive;
        private SocialSearchTab _searchCurrentTab = SocialSearchTab.Party;

        internal void OpenSearchWindow(SocialSearchTab initialTab)
        {
            EnsureSearchSeedData();
            ClearCharacterInfoSearchLaunch();
            _searchCurrentTab = initialTab;
            ClampSearchSelection(initialTab);
        }

        internal void OpenSearchWindowFromCharacterInfo(
            string characterName,
            CharacterBuild build,
            string locationSummary,
            int channel,
            bool isRemoteTarget,
            string handoffStatusText = null,
            int? launchOption = null)
        {
            EnsureSearchSeedData();
            ClearCharacterInfoSearchLaunch();

            if (isRemoteTarget && !string.IsNullOrWhiteSpace(characterName))
            {
                _characterInfoSearchLaunch = new CharacterInfoSearchLaunchState(
                    characterName.Trim(),
                    ResolveCharacterInfoSearchPrimaryText(build),
                    ResolveCharacterInfoSearchSecondaryText(build),
                    ResolveCharacterInfoSearchLocation(locationSummary),
                    NormalizeCharacterInfoSearchChannel(channel),
                    ResolveCharacterInfoSearchStatusText(handoffStatusText));
                ApplyCharacterInfoSearchLaunch();
                _searchSelectedIndexByTab[SocialSearchTab.PartyMember] = 0;
            }

            _searchCurrentTab = ResolveCharacterInfoSearchLaunchTab(isRemoteTarget, launchOption);
            ClampSearchSelection(_searchCurrentTab);
        }

        internal int? ResolveCharacterInfoSearchLaunchOption(int characterId, string characterName, bool isRemoteTarget)
        {
            if (!isRemoteTarget)
            {
                return null;
            }

            EnsureSearchSeedData();
            bool trackedPartyMember = characterId > 0 && IsTrackedPartyActor(characterId);
            if (!trackedPartyMember)
            {
                trackedPartyMember = IsTrackedPartyMember(characterName);
            }

            return trackedPartyMember || HasCharacterInfoPartyLaunchEntry(characterName)
                ? (int)SocialSearchTab.Party
                : (int)SocialSearchTab.PartyMember;
        }

        internal void OpenGuildSearchWindow()
        {
            EnsureGuildSearchSeedData();
            _guildSearchPage = Math.Clamp(_guildSearchPage, 0, Math.Max(0, ((int)Math.Ceiling(_guildSearchEntries.Count / (float)GuildSearchPageSize)) - 1));
            if (_guildSearchEntries.Count > 0)
            {
                _guildSearchSelectedIndex = Math.Clamp(_guildSearchSelectedIndex, 0, _guildSearchEntries.Count - 1);
            }
        }

        internal SocialSearchSnapshot BuildSearchSnapshot()
        {
            EnsureSearchSeedData();

            IReadOnlyList<SocialSearchEntryState> entries = GetFilteredSearchEntries(_searchCurrentTab);
            int selectedIndex = entries.Count > 0 ? Math.Clamp(GetSearchSelectedIndex(_searchCurrentTab), 0, entries.Count - 1) : -1;
            SocialSearchEntryState selectedEntry = selectedIndex >= 0 ? entries[selectedIndex] : null;
            string sortKey = _searchSortByTab.TryGetValue(_searchCurrentTab, out string currentSort) ? currentSort : string.Empty;

            return new SocialSearchSnapshot
            {
                CurrentTab = _searchCurrentTab,
                Entries = entries.Take(SearchPageSize).Select(CreateSearchEntrySnapshot).ToArray(),
                SelectedIndex = selectedIndex >= SearchPageSize ? -1 : selectedIndex,
                SimilarLevelOnly = _searchSimilarLevelOnly,
                SortKey = sortKey,
                SummaryLines = BuildSearchSummary(_searchCurrentTab, selectedEntry),
                EnabledActionKeys = GetEnabledSearchActions(_searchCurrentTab, selectedEntry).ToArray(),
                ExpeditionRegisterMode = _expeditionRegisterMode
            };
        }

        internal void SelectSearchTab(SocialSearchTab tab)
        {
            EnsureSearchSeedData();
            _searchCurrentTab = tab;
            ClampSearchSelection(tab);
        }

        internal void SelectSearchEntry(int visibleIndex)
        {
            EnsureSearchSeedData();
            if (visibleIndex < 0)
            {
                return;
            }

            IReadOnlyList<SocialSearchEntryState> entries = GetFilteredSearchEntries(_searchCurrentTab);
            if (visibleIndex < entries.Count && visibleIndex < SearchPageSize)
            {
                _searchSelectedIndexByTab[_searchCurrentTab] = visibleIndex;
            }
        }

        internal void SetSearchSimilarLevelOnly(bool enabled)
        {
            EnsureSearchSeedData();
            _searchSimilarLevelOnly = enabled;
            foreach (SocialSearchTab tab in Enum.GetValues(typeof(SocialSearchTab)))
            {
                ClampSearchSelection(tab);
            }
        }

        internal string ExecuteSearchAction(string actionKey)
        {
            EnsureSearchSeedData();
            if (string.IsNullOrWhiteSpace(actionKey))
            {
                return null;
            }

            string result = actionKey switch
            {
                "Search.Party.Request" => RequestSelectedParty(),
                "Search.Party.PartyLeader" => SortSearchEntries(SocialSearchTab.Party, "leader"),
                "Search.Party.PartyLevel" => SortSearchEntries(SocialSearchTab.Party, "level"),
                "Search.Party.Member" => SortSearchEntries(SocialSearchTab.Party, "member"),
                "Search.Party.Info" => ShowSelectedSearchInfo(),
                "Search.PartyMember.Invite" => InviteSelectedSearchMember(),
                "Search.PartyMember.Name" => SortSearchEntries(SocialSearchTab.PartyMember, "name"),
                "Search.PartyMember.Job" => SortSearchEntries(SocialSearchTab.PartyMember, "job"),
                "Search.PartyMember.Level" => SortSearchEntries(SocialSearchTab.PartyMember, "level"),
                "Search.Expedition.Start" => StartExpedition(),
                "Search.Expedition.Regist" => ToggleExpeditionRegistration(true),
                "Search.Expedition.Delete" => RemoveSelectedExpedition(),
                "Search.Expedition.QuickJoin" => QuickJoinExpedition(),
                "Search.Expedition.Request" => RequestSelectedExpedition(),
                "Search.Expedition.Whisper" => WhisperSelectedExpedition(),
                "Search.Expedition.Front" => RotateExpeditionResults(),
                "Search.Expedition.Regist2" => ConfirmExpeditionRegistration(),
                "Search.Expedition.Cancel" => ToggleExpeditionRegistration(false),
                _ => "That social search action is not modeled yet."
            };

            ObserveSearchSocialAction(actionKey, result);
            return result;
        }

        internal bool TryBuildExpeditionSearchOutboundRequest(
            string actionKey,
            out ExpeditionIntermediaryOutboundRequest request,
            out string reason)
        {
            request = default;
            reason = null;
            if (string.IsNullOrWhiteSpace(actionKey))
            {
                reason = "Expedition search action is empty.";
                return false;
            }

            SocialSearchEntryState selectedEntry = GetSelectedSearchEntry(SocialSearchTab.Expedition);
            if (selectedEntry == null)
            {
                reason = "No expedition search entry is selected.";
                return false;
            }

            switch (actionKey)
            {
                case "Search.Expedition.QuickJoin":
                    request = new ExpeditionIntermediaryOutboundRequest(
                        ExpeditionIntermediaryOutboundRequestKind.QuickJoin,
                        selectedEntry.Title,
                        selectedEntry.PrimaryText,
                        selectedEntry.PrimaryText,
                        PartyIndex: 0,
                        NoticeKind: ExpeditionNoticeKind.Joined,
                        RemovalKind: ExpeditionRemovalKind.Leave);
                    return true;

                case "Search.Expedition.Request":
                    request = new ExpeditionIntermediaryOutboundRequest(
                        ExpeditionIntermediaryOutboundRequestKind.Request,
                        selectedEntry.Title,
                        selectedEntry.PrimaryText,
                        selectedEntry.PrimaryText,
                        PartyIndex: 0,
                        NoticeKind: ExpeditionNoticeKind.Joined,
                        RemovalKind: ExpeditionRemovalKind.Leave);
                    return true;

                default:
                    reason = $"Expedition search action '{actionKey}' has no recovered outbound request shape.";
                    return false;
            }
        }

        internal GuildSearchSnapshot BuildGuildSearchSnapshot()
        {
            EnsureGuildSearchSeedData();

            int totalPages = Math.Max(1, (int)Math.Ceiling(_guildSearchEntries.Count / (float)GuildSearchPageSize));
            _guildSearchPage = Math.Clamp(_guildSearchPage, 0, totalPages - 1);
            int selectedIndex = _guildSearchEntries.Count > 0 ? Math.Clamp(_guildSearchSelectedIndex, 0, _guildSearchEntries.Count - 1) : -1;
            GuildSearchEntryState selectedEntry = selectedIndex >= 0 ? _guildSearchEntries[selectedIndex] : null;

            IReadOnlyList<GuildSearchEntryState> pageEntries = _guildSearchEntries
                .Skip(_guildSearchPage * GuildSearchPageSize)
                .Take(GuildSearchPageSize)
                .ToArray();
            int selectedVisibleIndex = selectedIndex >= 0 && selectedIndex / GuildSearchPageSize == _guildSearchPage
                ? selectedIndex % GuildSearchPageSize
                : -1;

            return new GuildSearchSnapshot
            {
                Entries = pageEntries.Select(entry => new GuildSearchEntrySnapshot
                {
                    GuildName = entry.GuildName,
                    MasterName = entry.MasterName,
                    LevelRange = entry.LevelRange,
                    MemberSummary = entry.MemberSummary,
                    Notice = entry.Notice,
                    IsWatched = _guildSearchWatchlist.Contains(entry.GuildName)
                }).ToArray(),
                SelectedVisibleIndex = selectedVisibleIndex,
                Page = _guildSearchPage + 1,
                TotalPages = totalPages,
                SummaryLines = BuildGuildSearchSummary(selectedEntry),
                EnabledActionKeys = GetEnabledGuildSearchActions(selectedEntry).ToArray(),
                CanPageBackward = _guildSearchPage > 0,
                CanPageForward = _guildSearchPage < totalPages - 1
            };
        }

        internal void SelectGuildSearchEntry(int visibleIndex)
        {
            EnsureGuildSearchSeedData();
            if (visibleIndex < 0)
            {
                return;
            }

            int absoluteIndex = (_guildSearchPage * GuildSearchPageSize) + visibleIndex;
            if (absoluteIndex >= 0 && absoluteIndex < _guildSearchEntries.Count)
            {
                _guildSearchSelectedIndex = absoluteIndex;
            }
        }

        internal void MoveGuildSearchPage(int delta)
        {
            EnsureGuildSearchSeedData();
            int totalPages = Math.Max(1, (int)Math.Ceiling(_guildSearchEntries.Count / (float)GuildSearchPageSize));
            _guildSearchPage = Math.Clamp(_guildSearchPage + delta, 0, totalPages - 1);
        }

        internal string ExecuteGuildSearchAction(string actionKey)
        {
            EnsureGuildSearchSeedData();
            GuildSearchEntryState selectedEntry = GetSelectedGuildSearchEntry();
            string result = actionKey switch
            {
                "GuildSearch.Add" => ToggleGuildWatch(selectedEntry, true),
                "GuildSearch.Delete" => ToggleGuildWatch(selectedEntry, false),
                "GuildSearch.Join" => JoinSelectedGuild(selectedEntry),
                "GuildSearch.Whisper" => selectedEntry == null ? "Select a guild entry before whispering." : $"[Whisper] {_playerName} -> {selectedEntry.MasterName}: Interested in {selectedEntry.GuildName}.",
                "GuildSearch.Renew" => RenewGuildSearch(),
                _ => "That guild-search action is not modeled yet."
            };

            ObserveGuildSearchSocialAction(actionKey, result);
            return result;
        }

        private void ObserveSearchSocialAction(string actionKey, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (actionKey is not ("Search.Party.Request"
                or "Search.PartyMember.Invite"
                or "Search.Expedition.Start"
                or "Search.Expedition.QuickJoin"
                or "Search.Expedition.Request"
                or "Search.Expedition.Whisper"
                or "Search.Expedition.Regist2"))
            {
                return;
            }

            NotifySocialChatObserved(message);
        }

        private void ObserveGuildSearchSocialAction(string actionKey, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (actionKey is not ("GuildSearch.Join" or "GuildSearch.Whisper"))
            {
                return;
            }

            NotifySocialChatObserved(message);
        }

        private void EnsureSearchSeedData()
        {
            if (_searchSeeded)
            {
                return;
            }

            foreach (SocialSearchTab tab in Enum.GetValues(typeof(SocialSearchTab)))
            {
                _searchEntriesByTab[tab] = new List<SocialSearchEntryState>();
                _searchSelectedIndexByTab[tab] = 0;
                _searchSortByTab[tab] = string.Empty;
            }

            _searchEntriesByTab[SocialSearchTab.Party].AddRange(new[]
            {
                new SocialSearchEntryState("Harbor Run", "Rondo", "Lv. 28-35", "3 / 6", "Lith Harbor", 4, "Need healer or ranged DPS"),
                new SocialSearchEntryState("Drake Hunt", "Aria", "Lv. 38-45", "4 / 6", "Sleepywood", 7, "Potion-light route, fast clears"),
                new SocialSearchEntryState("Ghost Ship Prep", "Eve", "Lv. 45-52", "2 / 6", "Singapore", 6, "Training party taking similar-level members"),
                new SocialSearchEntryState("Ariant Route", "Vale", "Lv. 55-62", "5 / 6", "Ariant", 9, "One slot open for a buffer")
            });

            _searchEntriesByTab[SocialSearchTab.PartyMember].AddRange(new[]
            {
                new SocialSearchEntryState("Clove", "Brawler", "Lv. 28", "Available", "Kerning Subway", 8, "Looking for subway or CDs"),
                new SocialSearchEntryState("Dawn", "Crossbowman", "Lv. 47", "Available", "Ludibrium", 11, "Prefers platform-heavy maps"),
                new SocialSearchEntryState("Vale", "Priest", "Lv. 61", "Available", "El Nath", 9, "Can support expedition prep"),
                new SocialSearchEntryState("Nina", "White Knight", "Lv. 52", "Available", "Omega Sector", 3, "Tank slot ready"),
                new SocialSearchEntryState("Jett", "Chief Bandit", "Lv. 44", "Available", "Singapore", 6, "Wants meso-up route")
            });

            _searchEntriesByTab[SocialSearchTab.Expedition].AddRange(new[]
            {
                new SocialSearchEntryState("Zakum Entry", "Cody", "Lv. 50+", "10 / 12", "El Nath", 1, "Preparation stage"),
                new SocialSearchEntryState("Scarga Check", "Pia", "Lv. 60+", "8 / 12", "Singapore", 2, "Need two ranged roles"),
                new SocialSearchEntryState("Horntail Scout", "Aria", "Lv. 80+", "6 / 12", "Leafre", 12, "Quick readiness check")
            });

            _searchCurrentTab = SocialSearchTab.Party;
            _searchSimilarLevelOnly = true;
            _searchSeeded = true;
        }

        private void EnsureGuildSearchSeedData()
        {
            if (_guildSearchSeeded)
            {
                return;
            }

            _guildSearchEntries.AddRange(new[]
            {
                new GuildSearchEntryState("Maple GM", "Cody", "Lv. 20+", "23 members", "Daily attendance checks and training nights."),
                new GuildSearchEntryState("Crimson Oak", "Rin", "Lv. 35+", "41 members", "Boss practice and LPQ rotations."),
                new GuildSearchEntryState("Blue Harbor", "Targa", "Lv. 15+", "18 members", "Casual leveling and quest help."),
                new GuildSearchEntryState("Skyline", "Aria", "Lv. 45+", "36 members", "Orbis and Leafre travel routes."),
                new GuildSearchEntryState("FreeMarket", "Milo", "Lv. 30+", "52 members", "Merchanting and bargain alerts."),
                new GuildSearchEntryState("Snowfall", "Vale", "Lv. 50+", "29 members", "Expedition prep and boss signups."),
                new GuildSearchEntryState("Ariant Sun", "Eve", "Lv. 25+", "20 members", "Desert routes and festival events."),
                new GuildSearchEntryState("Clocktower", "Noel", "Lv. 40+", "27 members", "Ludi and Omega grind roster.")
            });

            _guildSearchSelectedIndex = 0;
            _guildSearchPage = 0;
            _guildSearchSeeded = true;
        }

        private void ClampSearchSelection(SocialSearchTab tab)
        {
            IReadOnlyList<SocialSearchEntryState> entries = GetFilteredSearchEntries(tab);
            _searchSelectedIndexByTab[tab] = entries.Count > 0
                ? Math.Clamp(GetSearchSelectedIndex(tab), 0, Math.Min(SearchPageSize, entries.Count) - 1)
                : -1;
        }

        private int GetSearchSelectedIndex(SocialSearchTab tab)
        {
            return _searchSelectedIndexByTab.TryGetValue(tab, out int selectedIndex) ? selectedIndex : 0;
        }

        private IReadOnlyList<SocialSearchEntryState> GetFilteredSearchEntries(SocialSearchTab tab)
        {
            EnsureSearchSeedData();
            IEnumerable<SocialSearchEntryState> entries = _searchEntriesByTab[tab];
            if (!_searchSimilarLevelOnly)
            {
                return entries.ToArray();
            }

            return entries.Where(entry => entry.IsCharacterInfoOwned || IsEntryNearLocalLevel(entry.SecondaryText)).ToArray();
        }

        private void ApplyCharacterInfoSearchLaunch()
        {
            if (!_characterInfoSearchLaunch.HasValue)
            {
                return;
            }

            List<SocialSearchEntryState> entries = _searchEntriesByTab[SocialSearchTab.PartyMember];
            entries.RemoveAll(entry => entry.IsCharacterInfoOwned);

            CharacterInfoSearchLaunchState launch = _characterInfoSearchLaunch.Value;
            int existingIndex = entries.FindIndex(entry => string.Equals(entry.Title, launch.Name, StringComparison.OrdinalIgnoreCase));
            SocialSearchEntryState mergedEntry = existingIndex >= 0
                ? MergeCharacterInfoSearchEntry(entries[existingIndex], launch)
                : new SocialSearchEntryState(
                    launch.Name,
                    launch.PrimaryText,
                    launch.SecondaryText,
                    "Available",
                    launch.LocationSummary,
                    launch.Channel,
                    launch.StatusText)
                {
                    IsCharacterInfoOwned = true
                };

            if (existingIndex >= 0)
            {
                entries.RemoveAt(existingIndex);
            }

            entries.Insert(0, mergedEntry);

            if (TryFindCharacterInfoPartyLaunchEntryIndex(launch, out int partyIndex))
            {
                List<SocialSearchEntryState> partyEntries = _searchEntriesByTab[SocialSearchTab.Party];
                if (!_characterInfoPartyEntryOverrides.ContainsKey(partyIndex))
                {
                    _characterInfoPartyEntryOverrides[partyIndex] = partyEntries[partyIndex];
                }

                partyEntries[partyIndex] = MergeCharacterInfoPartyEntry(partyEntries[partyIndex], launch);
                _searchSelectedIndexByTab[SocialSearchTab.Party] = partyIndex;
            }
        }

        private void ClearCharacterInfoSearchLaunch()
        {
            _characterInfoSearchLaunch = null;
            if (_searchEntriesByTab.TryGetValue(SocialSearchTab.PartyMember, out List<SocialSearchEntryState> entries))
            {
                entries.RemoveAll(entry => entry.IsCharacterInfoOwned);
            }

            if (_characterInfoPartyEntryOverrides.Count <= 0
                || !_searchEntriesByTab.TryGetValue(SocialSearchTab.Party, out List<SocialSearchEntryState> partyEntries))
            {
                _characterInfoPartyEntryOverrides.Clear();
                return;
            }

            foreach (KeyValuePair<int, SocialSearchEntryState> entry in _characterInfoPartyEntryOverrides)
            {
                int index = entry.Key;
                if (index >= 0 && index < partyEntries.Count && entry.Value != null)
                {
                    partyEntries[index] = entry.Value;
                }
            }

            _characterInfoPartyEntryOverrides.Clear();
        }

        private static SocialSearchEntryState MergeCharacterInfoSearchEntry(
            SocialSearchEntryState existingEntry,
            CharacterInfoSearchLaunchState launch)
        {
            return new SocialSearchEntryState(
                launch.Name,
                string.IsNullOrWhiteSpace(existingEntry?.PrimaryText) ? launch.PrimaryText : existingEntry.PrimaryText,
                string.IsNullOrWhiteSpace(existingEntry?.SecondaryText) ? launch.SecondaryText : existingEntry.SecondaryText,
                string.IsNullOrWhiteSpace(existingEntry?.CapacityText) ? "Available" : existingEntry.CapacityText,
                string.IsNullOrWhiteSpace(launch.LocationSummary) ? existingEntry?.LocationSummary : launch.LocationSummary,
                launch.Channel > 0 ? launch.Channel : Math.Max(1, existingEntry?.Channel ?? 1),
                ResolveCharacterInfoSearchStatusText(launch.StatusText))
            {
                IsCharacterInfoOwned = true
            };
        }

        private static SocialSearchEntryState MergeCharacterInfoPartyEntry(
            SocialSearchEntryState existingEntry,
            CharacterInfoSearchLaunchState launch)
        {
            if (existingEntry == null)
            {
                return null;
            }

            return new SocialSearchEntryState(
                existingEntry.Title,
                existingEntry.PrimaryText,
                existingEntry.SecondaryText,
                existingEntry.CapacityText,
                string.IsNullOrWhiteSpace(launch.LocationSummary) ? existingEntry.LocationSummary : launch.LocationSummary,
                launch.Channel > 0 ? launch.Channel : Math.Max(1, existingEntry.Channel),
                ResolveCharacterInfoSearchStatusText(launch.StatusText));
        }

        private bool TryFindCharacterInfoPartyLaunchEntryIndex(CharacterInfoSearchLaunchState launch, out int index)
        {
            index = -1;
            if (!_searchEntriesByTab.TryGetValue(SocialSearchTab.Party, out List<SocialSearchEntryState> partyEntries)
                || partyEntries == null
                || partyEntries.Count == 0
                || string.IsNullOrWhiteSpace(launch.Name))
            {
                return false;
            }

            string name = launch.Name.Trim();
            return TryFindCharacterInfoPartyLaunchEntryIndexCore(partyEntries, name, out index);
        }

        private bool HasCharacterInfoPartyLaunchEntry(string characterName)
        {
            if (!_searchEntriesByTab.TryGetValue(SocialSearchTab.Party, out List<SocialSearchEntryState> partyEntries)
                || partyEntries == null
                || partyEntries.Count == 0
                || string.IsNullOrWhiteSpace(characterName))
            {
                return false;
            }

            return TryFindCharacterInfoPartyLaunchEntryIndexCore(partyEntries, characterName.Trim(), out _);
        }

        private static bool TryFindCharacterInfoPartyLaunchEntryIndexCore(
            IReadOnlyList<SocialSearchEntryState> partyEntries,
            string name,
            out int index)
        {
            index = -1;
            if (partyEntries == null
                || partyEntries.Count == 0
                || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            for (int i = 0; i < partyEntries.Count; i++)
            {
                SocialSearchEntryState entry = partyEntries[i];
                if (entry == null)
                {
                    continue;
                }

                bool titleMatch = string.Equals(entry.Title, name, StringComparison.OrdinalIgnoreCase);
                bool leaderMatch = string.Equals(entry.PrimaryText, name, StringComparison.OrdinalIgnoreCase);
                if (!titleMatch && !leaderMatch)
                {
                    continue;
                }

                index = i;
                return true;
            }

            return false;
        }

        private static string ResolveCharacterInfoSearchPrimaryText(CharacterBuild build)
        {
            if (!string.IsNullOrWhiteSpace(build?.JobName))
            {
                return build.JobName.Trim();
            }

            return "Job unavailable";
        }

        private static string ResolveCharacterInfoSearchSecondaryText(CharacterBuild build)
        {
            return build != null && build.Level > 0
                ? $"Lv. {build.Level}"
                : "Level unavailable";
        }

        private static string ResolveCharacterInfoSearchLocation(string locationSummary)
        {
            return string.IsNullOrWhiteSpace(locationSummary)
                ? "Location unknown"
                : locationSummary.Trim();
        }

        private static string ResolveCharacterInfoSearchStatusText(string handoffStatusText)
        {
            return string.IsNullOrWhiteSpace(handoffStatusText)
                ? "Character-info target handoff."
                : handoffStatusText.Trim();
        }

        private static int NormalizeCharacterInfoSearchChannel(int channel)
        {
            // Keep unknown remote channel context as CH ? instead of coercing it to CH 1.
            return channel > 0 ? channel : 0;
        }

        private static string FormatSearchChannelLabel(int channel)
        {
            return channel > 0 ? $"CH {channel}" : "CH ?";
        }

        private SocialSearchTab ResolveCharacterInfoSearchLaunchTab(bool isRemoteTarget, int? launchOption)
        {
            // CUIUserList::OnCreate seeds m_nCurTab from m_nOption when the owner is created.
            if (launchOption.HasValue && Enum.IsDefined(typeof(SocialSearchTab), launchOption.Value))
            {
                SocialSearchTab requestedTab = (SocialSearchTab)launchOption.Value;
                if (!isRemoteTarget || requestedTab != SocialSearchTab.Party || !_characterInfoSearchLaunch.HasValue)
                {
                    return requestedTab;
                }

                // When BtSearch launches with a Party-tab option but there is no matching
                // party listing for the inspected target, keep the target handoff visible on
                // the PartyMember tab instead of selecting an unrelated party row.
                return TryFindCharacterInfoPartyLaunchEntryIndex(_characterInfoSearchLaunch.Value, out _)
                    ? SocialSearchTab.Party
                    : SocialSearchTab.PartyMember;
            }

            if (isRemoteTarget)
            {
                return SocialSearchTab.PartyMember;
            }

            return Enum.IsDefined(typeof(SocialSearchTab), _searchCurrentTab)
                ? _searchCurrentTab
                : SocialSearchTab.Party;
        }

        private bool IsEntryNearLocalLevel(string levelText)
        {
            if (string.IsNullOrWhiteSpace(levelText))
            {
                return true;
            }

            int[] levels = levelText
                .Split(new[] { ' ', '-', '+', '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(chunk => int.TryParse(chunk, out int parsed) ? parsed : -1)
                .Where(level => level > 0)
                .ToArray();
            if (levels.Length == 0)
            {
                return true;
            }

            int localLevel = 1;
            SocialEntryState localPartyEntry = _entriesByTab[SocialListTab.Party].FirstOrDefault(entry => entry.IsLocalPlayer);
            if (localPartyEntry != null)
            {
                string[] localTokens = localPartyEntry.SecondaryText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                _ = int.TryParse(localTokens.LastOrDefault(), out localLevel);
                localLevel = Math.Max(1, localLevel);
            }

            int nearestLevel = levels.Aggregate((current, next) => Math.Abs(next - localLevel) < Math.Abs(current - localLevel) ? next : current);
            return Math.Abs(nearestLevel - localLevel) <= 20;
        }

        private SocialSearchEntrySnapshot CreateSearchEntrySnapshot(SocialSearchEntryState entry)
        {
            return new SocialSearchEntrySnapshot
            {
                Title = entry.Title,
                PrimaryText = entry.PrimaryText,
                SecondaryText = entry.SecondaryText,
                CapacityText = entry.CapacityText,
                LocationSummary = entry.LocationSummary,
                Channel = entry.Channel,
                StatusText = entry.StatusText
            };
        }

        private IReadOnlyList<string> BuildSearchSummary(SocialSearchTab tab, SocialSearchEntryState selectedEntry)
        {
            if (selectedEntry == null)
            {
                return tab switch
                {
                    SocialSearchTab.Party => new[] { "Party finder surface from `UserList/Search/Party`.", "Sort and request buttons are now simulator-owned.", $"{GetFilteredSearchEntries(tab).Count} visible party listings." },
                    SocialSearchTab.PartyMember => new[] { "Party-member finder surface from `UserList/Search/PartyMember`.", "Invite and sort buttons work against simulator-owned candidates.", $"{GetFilteredSearchEntries(tab).Count} visible member candidates." },
                    SocialSearchTab.Expedition => new[]
                    {
                        "Expedition finder surface from `UserList/Search/Expedition`.",
                        _expeditionRegisterMode
                            ? $"Registration strip is armed. {GetFilteredSearchEntries(tab).Count} visible expedition listings."
                            : $"Use REGIST to open the registration strip. {GetFilteredSearchEntries(tab).Count} visible expedition listings.",
                        DescribeExpeditionStatus()
                    },
                    _ => Array.Empty<string>()
                };
            }

            if (tab == SocialSearchTab.Expedition)
            {
                return new[]
                {
                    selectedEntry.Title,
                    $"{selectedEntry.PrimaryText}  {selectedEntry.SecondaryText}".Trim(),
                    selectedEntry.IsIntermediaryOwned
                        ? DescribeExpeditionStatus()
                        : $"{selectedEntry.LocationSummary}  CH {selectedEntry.Channel}  {selectedEntry.StatusText}".Trim()
                };
            }

            return new[]
            {
                selectedEntry.Title,
                $"{selectedEntry.PrimaryText}  {selectedEntry.SecondaryText}".Trim(),
                $"{selectedEntry.LocationSummary}  {FormatSearchChannelLabel(selectedEntry.Channel)}  {selectedEntry.StatusText}".Trim()
            };
        }

        private IEnumerable<string> GetEnabledSearchActions(SocialSearchTab tab, SocialSearchEntryState selectedEntry)
        {
            yield return tab switch
            {
                SocialSearchTab.Party => "Search.Party.PartyLeader",
                SocialSearchTab.PartyMember => "Search.PartyMember.Name",
                SocialSearchTab.Expedition => "Search.Expedition.Front",
                _ => string.Empty
            };

            switch (tab)
            {
                case SocialSearchTab.Party:
                    yield return "Search.Party.PartyLevel";
                    yield return "Search.Party.Member";
                    if (selectedEntry != null)
                    {
                        yield return "Search.Party.Request";
                        yield return "Search.Party.Info";
                    }
                    break;
                case SocialSearchTab.PartyMember:
                    yield return "Search.PartyMember.Job";
                    yield return "Search.PartyMember.Level";
                    if (selectedEntry != null)
                    {
                        yield return "Search.PartyMember.Invite";
                    }
                    break;
                case SocialSearchTab.Expedition:
                    yield return "Search.Expedition.Start";
                    yield return "Search.Expedition.Regist";
                    if (_expeditionRegisterMode)
                    {
                        yield return "Search.Expedition.Regist2";
                        yield return "Search.Expedition.Cancel";
                    }

                    if (selectedEntry != null)
                    {
                        yield return "Search.Expedition.Delete";
                        yield return "Search.Expedition.QuickJoin";
                        yield return "Search.Expedition.Request";
                        yield return "Search.Expedition.Whisper";
                    }
                    break;
            }
        }

        private IReadOnlyList<string> BuildGuildSearchSummary(GuildSearchEntryState selectedEntry)
        {
            if (selectedEntry == null)
            {
                return new[]
                {
                    "Guild discovery surface from `UserList/GuildSearch`.",
                    "Bookmark, join, whisper, renew, and page buttons are now locally owned.",
                    $"{_guildSearchEntries.Count} simulator guild entries listed."
                };
            }

            return new[]
            {
                selectedEntry.GuildName,
                $"{selectedEntry.MasterName}  {selectedEntry.LevelRange}  {selectedEntry.MemberSummary}",
                selectedEntry.Notice
            };
        }

        private IEnumerable<string> GetEnabledGuildSearchActions(GuildSearchEntryState selectedEntry)
        {
            yield return "GuildSearch.Renew";
            if (selectedEntry == null)
            {
                yield break;
            }

            yield return _guildSearchWatchlist.Contains(selectedEntry.GuildName)
                ? "GuildSearch.Delete"
                : "GuildSearch.Add";
            yield return "GuildSearch.Join";
            yield return "GuildSearch.Whisper";
        }

        private string SortSearchEntries(SocialSearchTab tab, string sortKey)
        {
            List<SocialSearchEntryState> entries = _searchEntriesByTab[tab];
            IEnumerable<SocialSearchEntryState> ordered = sortKey switch
            {
                "leader" => entries.OrderBy(entry => entry.PrimaryText, StringComparer.OrdinalIgnoreCase),
                "level" => entries.OrderBy(entry => entry.SecondaryText, StringComparer.OrdinalIgnoreCase),
                "member" => entries.OrderByDescending(entry => entry.CapacityText, StringComparer.OrdinalIgnoreCase),
                "name" => entries.OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase),
                "job" => entries.OrderBy(entry => entry.PrimaryText, StringComparer.OrdinalIgnoreCase),
                _ => entries
            };
            _searchEntriesByTab[tab] = RecomposeSortedSearchEntries(tab, ordered);
            _searchSortByTab[tab] = sortKey;
            ClampSearchSelection(tab);
            return $"{tab} search sorted by {sortKey}.";
        }

        private static List<SocialSearchEntryState> RecomposeSortedSearchEntries(
            SocialSearchTab tab,
            IEnumerable<SocialSearchEntryState> orderedEntries)
        {
            List<SocialSearchEntryState> sortedEntries = orderedEntries?.ToList() ?? new List<SocialSearchEntryState>();
            if (tab != SocialSearchTab.PartyMember || sortedEntries.Count == 0)
            {
                return sortedEntries;
            }

            List<SocialSearchEntryState> pinnedEntries = sortedEntries
                .Where(entry => entry.IsCharacterInfoOwned)
                .ToList();
            if (pinnedEntries.Count == 0)
            {
                return sortedEntries;
            }

            sortedEntries.RemoveAll(entry => entry.IsCharacterInfoOwned);
            pinnedEntries.AddRange(sortedEntries);
            return pinnedEntries;
        }

        private string RequestSelectedParty()
        {
            SocialSearchEntryState selectedEntry = GetSelectedSearchEntry(SocialSearchTab.Party);
            if (selectedEntry == null)
            {
                return "Select a party listing before requesting entry.";
            }

            return $"Party request sent to {selectedEntry.PrimaryText} for {selectedEntry.Title}.";
        }

        private string ShowSelectedSearchInfo()
        {
            SocialSearchEntryState selectedEntry = GetSelectedSearchEntry(_searchCurrentTab);
            if (selectedEntry == null)
            {
                return "Select a search entry before requesting details.";
            }

            return $"{selectedEntry.Title}: {selectedEntry.StatusText}.";
        }

        private string InviteSelectedSearchMember()
        {
            SocialSearchEntryState selectedEntry = GetSelectedSearchEntry(SocialSearchTab.PartyMember);
            if (selectedEntry == null)
            {
                return "Select a party-member entry before inviting it.";
            }

            if (_entriesByTab[SocialListTab.Party].Any(entry => string.Equals(entry.Name, selectedEntry.Title, StringComparison.OrdinalIgnoreCase)))
            {
                return $"{selectedEntry.Title} is already in the party roster.";
            }

            _entriesByTab[SocialListTab.Party].Add(new SocialEntryState(
                selectedEntry.Title,
                selectedEntry.PrimaryText,
                "Invited from search",
                selectedEntry.LocationSummary,
                selectedEntry.Channel,
                true,
                false,
                false));
            return $"{selectedEntry.Title} joined the party roster from search.";
        }

        private string StartExpedition()
        {
            return StartLocalExpeditionIntermediary();
        }

        private string ToggleExpeditionRegistration(bool enabled)
        {
            _expeditionRegisterMode = enabled;
            return enabled ? "Expedition registration strip opened." : "Expedition registration strip closed.";
        }

        private string ConfirmExpeditionRegistration()
        {
            _expeditionRegisterMode = false;
            return StartLocalExpeditionIntermediary($"{_playerName}'s Expedition", registrationDraft: true);
        }

        private string RemoveSelectedExpedition()
        {
            SocialSearchEntryState selectedEntry = GetSelectedSearchEntry(SocialSearchTab.Expedition);
            if (selectedEntry == null)
            {
                return "Select an expedition entry before deleting it.";
            }

            if (selectedEntry.IsIntermediaryOwned)
            {
                return ClearExpeditionIntermediary(_expeditionIntermediary.PacketOwned);
            }

            _searchEntriesByTab[SocialSearchTab.Expedition].RemoveAll(entry => string.Equals(entry.Title, selectedEntry.Title, StringComparison.OrdinalIgnoreCase));
            ClampSearchSelection(SocialSearchTab.Expedition);
            return $"{selectedEntry.Title} was removed from the expedition finder list.";
        }

        private string QuickJoinExpedition()
        {
            SocialSearchEntryState selectedEntry = GetSelectedSearchEntry(SocialSearchTab.Expedition);
            if (selectedEntry == null)
            {
                return "Select an expedition entry before quick-joining.";
            }

            return StageExpeditionRequest(selectedEntry.Title, selectedEntry.PrimaryText, quickJoin: true, packetOwned: false);
        }

        private string RequestSelectedExpedition()
        {
            SocialSearchEntryState selectedEntry = GetSelectedSearchEntry(SocialSearchTab.Expedition);
            if (selectedEntry == null)
            {
                return "Select an expedition entry before requesting entry.";
            }

            return StageExpeditionRequest(selectedEntry.Title, selectedEntry.PrimaryText, quickJoin: false, packetOwned: false);
        }

        private string WhisperSelectedExpedition()
        {
            SocialSearchEntryState selectedEntry = GetSelectedSearchEntry(SocialSearchTab.Expedition);
            if (selectedEntry == null)
            {
                return "Select an expedition entry before whispering.";
            }

            return $"[Whisper] {_playerName} -> {selectedEntry.PrimaryText}: Interested in {selectedEntry.Title}.";
        }

        private string RotateExpeditionResults()
        {
            List<SocialSearchEntryState> entries = _searchEntriesByTab[SocialSearchTab.Expedition];
            if (entries.Count <= 1)
            {
                return "No additional expedition listings are available.";
            }

            SocialSearchEntryState first = entries[0];
            entries.RemoveAt(0);
            entries.Add(first);
            ClampSearchSelection(SocialSearchTab.Expedition);
            return "Expedition finder rotated to the next listing set.";
        }

        internal bool HasExpeditionAdmissionContext()
        {
            return _expeditionAdmissionActive;
        }

        private SocialSearchEntryState GetSelectedSearchEntry(SocialSearchTab tab)
        {
            IReadOnlyList<SocialSearchEntryState> entries = GetFilteredSearchEntries(tab);
            int selectedIndex = GetSearchSelectedIndex(tab);
            return selectedIndex >= 0 && selectedIndex < entries.Count ? entries[selectedIndex] : null;
        }

        private GuildSearchEntryState GetSelectedGuildSearchEntry()
        {
            return _guildSearchSelectedIndex >= 0 && _guildSearchSelectedIndex < _guildSearchEntries.Count
                ? _guildSearchEntries[_guildSearchSelectedIndex]
                : null;
        }

        private string ToggleGuildWatch(GuildSearchEntryState selectedEntry, bool add)
        {
            if (selectedEntry == null)
            {
                return add
                    ? "Select a guild entry before bookmarking it."
                    : "Select a guild entry before deleting its bookmark.";
            }

            if (add)
            {
                _guildSearchWatchlist.Add(selectedEntry.GuildName);
                return $"{selectedEntry.GuildName} was added to the guild watchlist.";
            }

            _guildSearchWatchlist.Remove(selectedEntry.GuildName);
            return $"{selectedEntry.GuildName} was removed from the guild watchlist.";
        }

        private string JoinSelectedGuild(GuildSearchEntryState selectedEntry)
        {
            if (selectedEntry == null)
            {
                return "Select a guild entry before requesting to join it.";
            }

            return $"Join request prepared for guild {selectedEntry.GuildName}. Live admission approval still remains packet-driven.";
        }

        private string RenewGuildSearch()
        {
            if (_guildSearchEntries.Count > 1)
            {
                GuildSearchEntryState first = _guildSearchEntries[0];
                _guildSearchEntries.RemoveAt(0);
                _guildSearchEntries.Add(first);
            }

            _guildSearchSelectedIndex = Math.Clamp(_guildSearchSelectedIndex, 0, Math.Max(0, _guildSearchEntries.Count - 1));
            return "Guild search results renewed.";
        }

        private sealed class SocialSearchEntryState
        {
            public SocialSearchEntryState(string title, string primaryText, string secondaryText, string capacityText, string locationSummary, int channel, string statusText)
            {
                Title = title;
                PrimaryText = primaryText;
                SecondaryText = secondaryText;
                CapacityText = capacityText;
                LocationSummary = locationSummary;
                Channel = channel;
                StatusText = statusText;
            }

            public string Title { get; }
            public string PrimaryText { get; }
            public string SecondaryText { get; }
            public string CapacityText { get; }
            public string LocationSummary { get; }
            public int Channel { get; }
            public string StatusText { get; }
            public bool IsIntermediaryOwned { get; init; }
            public bool IsCharacterInfoOwned { get; init; }
        }

        private readonly struct CharacterInfoSearchLaunchState
        {
            public CharacterInfoSearchLaunchState(
                string name,
                string primaryText,
                string secondaryText,
                string locationSummary,
                int channel,
                string statusText)
            {
                Name = name ?? string.Empty;
                PrimaryText = primaryText ?? string.Empty;
                SecondaryText = secondaryText ?? string.Empty;
                LocationSummary = locationSummary ?? string.Empty;
                Channel = channel;
                StatusText = statusText ?? string.Empty;
            }

            public string Name { get; }
            public string PrimaryText { get; }
            public string SecondaryText { get; }
            public string LocationSummary { get; }
            public int Channel { get; }
            public string StatusText { get; }
        }

        private sealed class GuildSearchEntryState
        {
            public GuildSearchEntryState(string guildName, string masterName, string levelRange, string memberSummary, string notice)
            {
                GuildName = guildName;
                MasterName = masterName;
                LevelRange = levelRange;
                MemberSummary = memberSummary;
                Notice = notice;
            }

            public string GuildName { get; }
            public string MasterName { get; }
            public string LevelRange { get; }
            public string MemberSummary { get; }
            public string Notice { get; }
        }
    }

    internal sealed class SocialSearchSnapshot
    {
        public SocialSearchTab CurrentTab { get; init; }
        public IReadOnlyList<SocialSearchEntrySnapshot> Entries { get; init; } = Array.Empty<SocialSearchEntrySnapshot>();
        public int SelectedIndex { get; init; } = -1;
        public bool SimilarLevelOnly { get; init; }
        public string SortKey { get; init; } = string.Empty;
        public IReadOnlyList<string> SummaryLines { get; init; } = Array.Empty<string>();
        public IReadOnlyCollection<string> EnabledActionKeys { get; init; } = Array.Empty<string>();
        public bool ExpeditionRegisterMode { get; init; }
    }

    internal sealed class SocialSearchEntrySnapshot
    {
        public string Title { get; init; } = string.Empty;
        public string PrimaryText { get; init; } = string.Empty;
        public string SecondaryText { get; init; } = string.Empty;
        public string CapacityText { get; init; } = string.Empty;
        public string LocationSummary { get; init; } = string.Empty;
        public int Channel { get; init; }
        public string StatusText { get; init; } = string.Empty;
    }

    internal sealed class GuildSearchSnapshot
    {
        public IReadOnlyList<GuildSearchEntrySnapshot> Entries { get; init; } = Array.Empty<GuildSearchEntrySnapshot>();
        public int SelectedVisibleIndex { get; init; } = -1;
        public int Page { get; init; } = 1;
        public int TotalPages { get; init; } = 1;
        public IReadOnlyList<string> SummaryLines { get; init; } = Array.Empty<string>();
        public IReadOnlyCollection<string> EnabledActionKeys { get; init; } = Array.Empty<string>();
        public bool CanPageBackward { get; init; }
        public bool CanPageForward { get; init; }
    }

    internal sealed class GuildSearchEntrySnapshot
    {
        public string GuildName { get; init; } = string.Empty;
        public string MasterName { get; init; } = string.Empty;
        public string LevelRange { get; init; } = string.Empty;
        public string MemberSummary { get; init; } = string.Empty;
        public string Notice { get; init; } = string.Empty;
        public bool IsWatched { get; init; }
    }
}
