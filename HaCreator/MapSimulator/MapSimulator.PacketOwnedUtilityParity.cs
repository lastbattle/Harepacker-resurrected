using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly Dictionary<int, HashSet<int>> _packetQuestGuideTargetsByMobId = new();
        private readonly LocalUtilityPacketInboxManager _localUtilityPacketInbox = new();
        private int _packetQuestGuideQuestId;
        private bool _packetOwnedUtilityRequestSent;
        private int _packetOwnedUtilityRequestTick = int.MinValue;
        private int _lastDeliveryQuestId;
        private int _lastDeliveryItemId;
        private readonly List<int> _lastDeliveryDisallowedQuestIds = new();
        private int _lastQuestDemandItemQueryQuestId;
        private readonly List<int> _lastQuestDemandQueryVisibleItemIds = new();
        private int _lastQuestDemandQueryHiddenItemCount;
        private int _lastClassCompetitionOpenTick = int.MinValue;
        private int _lastPacketOwnedOpenUiType = -1;
        private int _lastPacketOwnedOpenUiOption = -1;
        private int _lastPacketOwnedCommoditySerialNumber;
        private int _lastPacketOwnedCommodityRequestTick = int.MinValue;
        private string _lastPacketOwnedNoticeMessage;
        private string _lastPacketOwnedChatMessage;
        private string _lastPacketOwnedBuffzoneMessage;
        private string _lastPacketOwnedAskApspMessage;
        private string _lastPacketOwnedFollowFailureMessage;
        private int? _lastPacketOwnedFollowFailureReason;
        private int _lastPacketOwnedFollowFailureDriverId;
        private bool _lastPacketOwnedFollowFailureClearedPending;
        private string _lastPacketOwnedEventSoundDescriptor;
        private string _lastPacketOwnedMinigameSoundDescriptor;
        private bool _localUtilityPacketInboxEnabled = true;
        private int _localUtilityPacketInboxConfiguredPort = LocalUtilityPacketInboxManager.DefaultPort;

        private static readonly string[] UniqueModelessUtilityWindowNames =
        {
            MapSimulatorWindowNames.CashShop,
            MapSimulatorWindowNames.Mts,
            MapSimulatorWindowNames.MapTransfer,
            MapSimulatorWindowNames.ItemMaker,
            MapSimulatorWindowNames.ItemUpgrade,
            MapSimulatorWindowNames.RepairDurability,
            MapSimulatorWindowNames.VegaSpell,
            MapSimulatorWindowNames.Trunk,
            MapSimulatorWindowNames.CharacterInfo,
            MapSimulatorWindowNames.SocialList,
            MapSimulatorWindowNames.GuildSearch,
            MapSimulatorWindowNames.GuildSkill,
            MapSimulatorWindowNames.GuildBbs,
            MapSimulatorWindowNames.Messenger,
            MapSimulatorWindowNames.EngagementProposal,
            MapSimulatorWindowNames.MapleTv,
            MapSimulatorWindowNames.MemoMailbox,
            MapSimulatorWindowNames.MemoSend,
            MapSimulatorWindowNames.MemoGet,
            MapSimulatorWindowNames.QuestAlarm,
            MapSimulatorWindowNames.QuestDelivery,
            MapSimulatorWindowNames.ClassCompetition,
            MapSimulatorWindowNames.MiniRoom,
            MapSimulatorWindowNames.PersonalShop,
            MapSimulatorWindowNames.EntrustedShop,
            MapSimulatorWindowNames.TradingRoom,
        };

        private void StampPacketOwnedUtilityRequestState()
        {
            _packetOwnedUtilityRequestSent = false;
            _packetOwnedUtilityRequestTick = Environment.TickCount;
        }

        private string ApplyPacketQuestGuideLaunch(int questId, IReadOnlyDictionary<int, IReadOnlyList<int>> targetsByMobId)
        {
            StampPacketOwnedUtilityRequestState();
            ClearPacketQuestGuideTargets(refreshWorldMap: false);

            if (targetsByMobId != null)
            {
                foreach ((int targetMobId, IReadOnlyList<int> mapIds) in targetsByMobId)
                {
                    if (targetMobId <= 0 || mapIds == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < mapIds.Count; i++)
                    {
                        AddPacketQuestGuideTarget(targetMobId, mapIds[i]);
                    }
                }
            }

            if (_packetQuestGuideTargetsByMobId.Count == 0)
            {
                ClearPacketQuestGuideTargets();
                const string notice = "Quest guide data did not contain any usable world-map mob targets.";
                ShowUtilityFeedbackMessage(notice);
                return notice;
            }

            _packetQuestGuideQuestId = Math.Max(0, questId);
            RefreshWorldMapWindow(_mapBoard?.MapInfo?.id ?? 0);

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.WorldMap) is not WorldMapUI worldMapWindow)
            {
                ClearPacketQuestGuideTargets(refreshWorldMap: false);
                const string unavailable = "World map window is not available in this UI build.";
                ShowUtilityFeedbackMessage(unavailable);
                return unavailable;
            }

            if (!TryResolveFirstQuestGuideTarget(out int mobId, out int mapId))
            {
                ClearPacketQuestGuideTargets();
                const string notice = "Quest guide data did not contain any usable world-map mob targets.";
                ShowUtilityFeedbackMessage(notice);
                return notice;
            }

            string mobName = ResolvePacketGuideMobName(mobId);
            if (!worldMapWindow.FocusSearchResult(WorldMapUI.SearchResultKind.Mob, mobName, mapId))
            {
                ClearPacketQuestGuideTargets();
                string notice = $"Quest guide data for {mobName} could not be resolved in the simulator world map.";
                ShowUtilityFeedbackMessage(notice);
                return notice;
            }

            uiWindowManager.ShowWindow(MapSimulatorWindowNames.WorldMap);
            uiWindowManager.BringToFront(worldMapWindow);
            return $"Opened packet-authored quest guide for quest #{_packetQuestGuideQuestId} targeting {mobName}.";
        }

        private string ResetPacketQuestGuideLaunch()
        {
            ClearPacketQuestGuideTargets();
            return "Cleared packet-authored quest guide demand state.";
        }

        private void AppendPacketQuestGuideSearchResults(List<WorldMapUI.SearchResultEntry> results, HashSet<string> seen)
        {
            if (results == null || _packetQuestGuideTargetsByMobId.Count == 0)
            {
                return;
            }

            foreach ((int mobId, HashSet<int> mapIds) in _packetQuestGuideTargetsByMobId.OrderBy(entry => entry.Key))
            {
                string mobName = ResolvePacketGuideMobName(mobId);
                foreach (int mapId in mapIds.OrderBy(value => value))
                {
                    string dedupeKey = $"packetmob:{mobId}:{mapId}";
                    if (!seen.Add(dedupeKey))
                    {
                        continue;
                    }

                    results.Add(new WorldMapUI.SearchResultEntry
                    {
                        Kind = WorldMapUI.SearchResultKind.Mob,
                        MapId = mapId,
                        Label = mobName,
                        Description = $"Packet-authored quest guide target in {ResolveMapTransferDisplayName(mapId, null)}"
                    });
                }
            }
        }

        private string ApplyDeliveryQuestLaunch(int questId, int itemId, IReadOnlyList<int> disallowedQuestIds)
        {
            StampPacketOwnedUtilityRequestState();
            _lastDeliveryQuestId = Math.Max(0, questId);
            _lastDeliveryItemId = Math.Max(0, itemId);
            _lastDeliveryDisallowedQuestIds.Clear();

            if (disallowedQuestIds != null)
            {
                for (int i = 0; i < disallowedQuestIds.Count; i++)
                {
                    int blockedQuestId = disallowedQuestIds[i];
                    if (blockedQuestId > 0 && !_lastDeliveryDisallowedQuestIds.Contains(blockedQuestId))
                    {
                        _lastDeliveryDisallowedQuestIds.Add(blockedQuestId);
                    }
                }
            }

            string itemName = InventoryItemMetadataResolver.TryResolveItemName(_lastDeliveryItemId, out string resolvedItemName)
                ? resolvedItemName
                : _lastDeliveryItemId > 0
                    ? $"Item {_lastDeliveryItemId}"
                    : "Unknown delivery item";

            string blockingOwner = GetVisibleUniqueModelessOwner(MapSimulatorWindowNames.QuestDelivery);
            if (!string.IsNullOrWhiteSpace(blockingOwner))
            {
                string message = $"{itemName} delivery was routed to the status-bar chat path because {blockingOwner} is already open.";
                ShowUtilityFeedbackMessage(message);
                return message;
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.QuestDelivery) is not QuestDeliveryWindow questDeliveryWindow)
            {
                const string unavailable = "Quest delivery window is not available in this UI build.";
                ShowUtilityFeedbackMessage(unavailable);
                return unavailable;
            }

            IReadOnlyList<QuestDeliveryWindow.DeliveryEntry> entries = BuildQuestDeliveryEntries(
                _lastDeliveryQuestId,
                _lastDeliveryItemId,
                _lastDeliveryDisallowedQuestIds);
            questDeliveryWindow.Configure(_lastDeliveryQuestId, _lastDeliveryItemId, entries, _packetOwnedUtilityRequestTick);
            ShowWindow(MapSimulatorWindowNames.QuestDelivery, questDeliveryWindow, trackDirectionModeOwner: true);
            return $"Opened packet-authored quest delivery for {itemName}.";
        }

        private string ApplyQuestDemandItemQueryLaunch(QuestDemandItemQueryState queryState)
        {
            StampPacketOwnedUtilityRequestState();
            _lastQuestDemandItemQueryQuestId = Math.Max(0, queryState?.QuestId ?? 0);
            _lastQuestDemandQueryVisibleItemIds.Clear();
            _lastQuestDemandQueryHiddenItemCount = Math.Max(0, queryState?.HiddenItemCount ?? 0);

            if (queryState?.VisibleItemIds != null)
            {
                for (int i = 0; i < queryState.VisibleItemIds.Count; i++)
                {
                    int itemId = queryState.VisibleItemIds[i];
                    if (itemId > 0 && !_lastQuestDemandQueryVisibleItemIds.Contains(itemId))
                    {
                        _lastQuestDemandQueryVisibleItemIds.Add(itemId);
                    }
                }
            }

            if (_lastQuestDemandQueryVisibleItemIds.Count == 0)
            {
                string fallbackNpc = queryState?.FallbackNpcName;
                if (!string.IsNullOrWhiteSpace(fallbackNpc) &&
                    uiWindowManager?.GetWindow(MapSimulatorWindowNames.WorldMap) is WorldMapUI worldMapWindow)
                {
                    RefreshWorldMapWindow(_mapBoard?.MapInfo?.id ?? 0);
                    int currentMapId = _mapBoard?.MapInfo?.id ?? 0;
                    bool focused = worldMapWindow.FocusSearchResult(WorldMapUI.SearchResultKind.Npc, fallbackNpc, currentMapId);
                    uiWindowManager.ShowWindow(MapSimulatorWindowNames.WorldMap);
                    uiWindowManager.BringToFront(worldMapWindow);
                    return focused
                        ? $"Quest demand query only exposed hidden items, so the world map fell back to {fallbackNpc}."
                        : $"Quest demand query only exposed hidden items, and {fallbackNpc} could not be resolved in the current world-map search results.";
                }

                return "Quest demand query only exposed hidden items, so no local item search target could be surfaced.";
            }

            RefreshWorldMapWindow(_mapBoard?.MapInfo?.id ?? 0);
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.WorldMap) is not WorldMapUI demandWorldMapWindow)
            {
                ClearQuestDemandItemQueryState(refreshWorldMap: false);
                const string unavailable = "World map window is not available in this UI build.";
                ShowUtilityFeedbackMessage(unavailable);
                return unavailable;
            }

            int currentFieldId = _mapBoard?.MapInfo?.id ?? 0;
            int focusItemId = _lastQuestDemandQueryVisibleItemIds[0];
            string focusItemName = InventoryItemMetadataResolver.TryResolveItemName(focusItemId, out string resolvedItemName)
                ? resolvedItemName
                : $"Item {focusItemId}";
            bool focusedItem = demandWorldMapWindow.FocusSearchResult(WorldMapUI.SearchResultKind.Item, focusItemName, currentFieldId);
            uiWindowManager.ShowWindow(MapSimulatorWindowNames.WorldMap);
            uiWindowManager.BringToFront(demandWorldMapWindow);

            string hiddenSuffix = _lastQuestDemandQueryHiddenItemCount > 0
                ? $" {_lastQuestDemandQueryHiddenItemCount} hidden demand item(s) remain packet-only."
                : string.Empty;
            return focusedItem
                ? $"Opened a packet-shaped quest demand item query for {focusItemName}.{hiddenSuffix}".TrimEnd()
                : $"Opened the world map, but the local demand-item query for {focusItemName} could not be resolved.{hiddenSuffix}".TrimEnd();
        }

        private void AppendQuestDemandItemSearchResults(List<WorldMapUI.SearchResultEntry> results, HashSet<string> seen)
        {
            if (results == null || _lastQuestDemandQueryVisibleItemIds.Count == 0)
            {
                return;
            }

            int currentMapId = _mapBoard?.MapInfo?.id ?? 0;
            for (int i = 0; i < _lastQuestDemandQueryVisibleItemIds.Count; i++)
            {
                int itemId = _lastQuestDemandQueryVisibleItemIds[i];
                if (itemId <= 0)
                {
                    continue;
                }

                string itemName = InventoryItemMetadataResolver.TryResolveItemName(itemId, out string resolvedItemName)
                    ? resolvedItemName
                    : $"Item {itemId}";
                string dedupeKey = $"questitem:{_lastQuestDemandItemQueryQuestId}:{currentMapId}:{itemId}";
                if (!seen.Add(dedupeKey))
                {
                    continue;
                }

                results.Add(new WorldMapUI.SearchResultEntry
                {
                    Kind = WorldMapUI.SearchResultKind.Item,
                    MapId = currentMapId,
                    Label = itemName,
                    Description = $"Quest demand item query for quest #{_lastQuestDemandItemQueryQuestId} in {ResolveMapTransferDisplayName(currentMapId, null)}"
                });
            }
        }

        private void ClearQuestDemandItemQueryState(bool refreshWorldMap = true)
        {
            _lastQuestDemandItemQueryQuestId = 0;
            _lastQuestDemandQueryVisibleItemIds.Clear();
            _lastQuestDemandQueryHiddenItemCount = 0;

            if (refreshWorldMap)
            {
                RefreshWorldMapWindow(_mapBoard?.MapInfo?.id ?? 0);
            }
        }

        private string ApplyClassCompetitionPageLaunch()
        {
            _lastClassCompetitionOpenTick = Environment.TickCount;

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ClassCompetition) is not UIWindowBase window)
            {
                const string unavailable = "Class Competition page owner is not available in this UI build.";
                ShowUtilityFeedbackMessage(unavailable);
                return unavailable;
            }

            ShowWindow(MapSimulatorWindowNames.ClassCompetition, window, trackDirectionModeOwner: true);
            return "Opened packet-authored Class Competition page.";
        }

        private void ClearPacketQuestGuideTargets(bool refreshWorldMap = true)
        {
            _packetQuestGuideTargetsByMobId.Clear();
            _packetQuestGuideQuestId = 0;

            if (refreshWorldMap)
            {
                RefreshWorldMapWindow(_mapBoard?.MapInfo?.id ?? 0);
            }
        }

        private void AddPacketQuestGuideTarget(int mobId, int mapId)
        {
            if (mobId <= 0 || mapId <= 0)
            {
                return;
            }

            if (!_packetQuestGuideTargetsByMobId.TryGetValue(mobId, out HashSet<int> mapIds))
            {
                mapIds = new HashSet<int>();
                _packetQuestGuideTargetsByMobId[mobId] = mapIds;
            }

            mapIds.Add(mapId);
        }

        private bool TryResolveFirstQuestGuideTarget(out int mobId, out int mapId)
        {
            foreach ((int currentMobId, HashSet<int> mapIds) in _packetQuestGuideTargetsByMobId.OrderBy(entry => entry.Key))
            {
                if (mapIds.Count == 0)
                {
                    continue;
                }

                mobId = currentMobId;
                mapId = mapIds.OrderBy(value => value).First();
                return true;
            }

            mobId = 0;
            mapId = 0;
            return false;
        }

        private string GetVisibleUniqueModelessOwner(string ignoredWindowName)
        {
            if (uiWindowManager == null)
            {
                return null;
            }

            for (int i = 0; i < UniqueModelessUtilityWindowNames.Length; i++)
            {
                string windowName = UniqueModelessUtilityWindowNames[i];
                if (string.Equals(windowName, ignoredWindowName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (uiWindowManager.GetWindow(windowName) is UIWindowBase window && window.IsVisible)
                {
                    return ResolveWindowDisplayName(windowName);
                }
            }

            return null;
        }

        private static string ResolveWindowDisplayName(string windowName)
        {
            return windowName switch
            {
                MapSimulatorWindowNames.CashShop => "Cash Shop",
                MapSimulatorWindowNames.Mts => "MTS",
                MapSimulatorWindowNames.MapTransfer => "Map Transfer",
                MapSimulatorWindowNames.ItemMaker => "Item Maker",
                MapSimulatorWindowNames.ItemUpgrade => "Item Upgrade",
                MapSimulatorWindowNames.RepairDurability => "Repair",
                MapSimulatorWindowNames.VegaSpell => "Vega's Spell",
                MapSimulatorWindowNames.CharacterInfo => "Character Info",
                MapSimulatorWindowNames.SocialList => "Social List",
                MapSimulatorWindowNames.GuildSearch => "Guild Search",
                MapSimulatorWindowNames.GuildSkill => "Guild Skill",
                MapSimulatorWindowNames.GuildBbs => "Guild BBS",
                MapSimulatorWindowNames.EngagementProposal => "Engagement Proposal",
                MapSimulatorWindowNames.MapleTv => "MapleTV",
                MapSimulatorWindowNames.MemoMailbox => "Memo Mailbox",
                MapSimulatorWindowNames.MemoSend => "Memo Send",
                MapSimulatorWindowNames.MemoGet => "Memo Package",
                MapSimulatorWindowNames.QuestDelivery => "Quest Delivery",
                MapSimulatorWindowNames.ClassCompetition => "Class Competition",
                MapSimulatorWindowNames.MiniRoom => "Mini Room",
                MapSimulatorWindowNames.PersonalShop => "Personal Shop",
                MapSimulatorWindowNames.EntrustedShop => "Entrusted Shop",
                MapSimulatorWindowNames.TradingRoom => "Trading Room",
                _ => windowName
            };
        }

        private IReadOnlyList<QuestDeliveryWindow.DeliveryEntry> BuildQuestDeliveryEntries(int requestedQuestId, int itemId, IReadOnlyList<int> disallowedQuestIds)
        {
            var entries = new List<QuestDeliveryWindow.DeliveryEntry>();
            var candidateQuestIds = new HashSet<int>();
            var blockedQuestIds = new HashSet<int>(disallowedQuestIds?.Where(id => id > 0) ?? Array.Empty<int>());

            if (requestedQuestId > 0)
            {
                candidateQuestIds.Add(requestedQuestId);
            }

            AppendQuestIds(candidateQuestIds, BuildQuestLogSnapshotWithPacketState(QuestLogTabType.Available, showAllLevels: true));
            AppendQuestIds(candidateQuestIds, BuildQuestLogSnapshotWithPacketState(QuestLogTabType.InProgress, showAllLevels: true));

            foreach (int questId in blockedQuestIds)
            {
                candidateQuestIds.Add(questId);
            }

            foreach (int questId in candidateQuestIds)
            {
                QuestWindowDetailState state = GetQuestWindowDetailStateWithPacketState(questId);
                bool blockedByPacket = blockedQuestIds.Contains(questId);
                bool matchingItem = state?.TargetItemId == itemId && itemId > 0;
                bool isDeliveryAction = state?.PrimaryAction is QuestWindowActionKind.QuestDeliveryAccept or QuestWindowActionKind.QuestDeliveryComplete;
                if (!blockedByPacket && (!matchingItem || !isDeliveryAction))
                {
                    continue;
                }

                bool completionPhase = state?.PrimaryAction == QuestWindowActionKind.QuestDeliveryComplete;
                bool canConfirm = state?.PrimaryActionEnabled == true && !blockedByPacket;
                string npcName = string.IsNullOrWhiteSpace(state?.TargetNpcName)
                    ? "NPC unavailable"
                    : state.TargetNpcName;
                string statusText = blockedByPacket
                    ? "Blocked by disallowed-delivery packet state"
                    : canConfirm
                        ? completionPhase
                            ? $"Complete delivery at {npcName}"
                            : $"Accept delivery at {npcName}"
                        : completionPhase
                            ? $"Complete delivery is not ready at {npcName}"
                            : $"Accept delivery is not ready at {npcName}";
                string detailText = !string.IsNullOrWhiteSpace(state?.HintText)
                    ? state.HintText.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal)
                    : !string.IsNullOrWhiteSpace(state?.RequirementText)
                        ? state.RequirementText
                        : "The packet-owned delivery owner mirrors the client's worthy-quest list over the simulator quest runtime.";

                entries.Add(new QuestDeliveryWindow.DeliveryEntry
                {
                    QuestId = questId,
                    TargetNpcId = state?.TargetNpcId ?? 0,
                    Title = state?.Title ?? $"Quest #{questId}",
                    NpcName = npcName,
                    StatusText = statusText,
                    DetailText = detailText,
                    CompletionPhase = completionPhase,
                    CanConfirm = canConfirm,
                    IsBlocked = blockedByPacket
                });
            }

            return entries
                .OrderByDescending(entry => entry.QuestId == requestedQuestId)
                .ThenByDescending(entry => entry.CanConfirm)
                .ThenByDescending(entry => entry.CompletionPhase)
                .ThenBy(entry => entry.QuestId)
                .ToArray();
        }

        private static void AppendQuestIds(HashSet<int> questIds, QuestLogSnapshot snapshot)
        {
            if (questIds == null || snapshot?.Entries == null)
            {
                return;
            }

            for (int i = 0; i < snapshot.Entries.Count; i++)
            {
                if (snapshot.Entries[i]?.QuestId > 0)
                {
                    questIds.Add(snapshot.Entries[i].QuestId);
                }
            }
        }

        private void HandleQuestDeliveryWindowRequest(QuestDeliveryWindow.DeliveryEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            int targetNpcId = entry.TargetNpcId;
            if (targetNpcId <= 0)
            {
                string error = $"{entry.Title} does not have a delivery NPC in the loaded quest data.";
                _chat?.AddMessage(error, Color.OrangeRed, currTickCount);
                return;
            }

            OpenPacketQuestDeliveryNpcInteraction(entry);
        }

        private void OpenPacketQuestDeliveryNpcInteraction(QuestDeliveryWindow.DeliveryEntry entry)
        {
            if (entry == null || _npcInteractionOverlay == null)
            {
                return;
            }

            NpcInteractionState interactionState = _questRuntime.BuildQuestDeliveryInteractionState(
                entry.QuestId,
                _playerManager?.Player?.Build,
                _lastDeliveryItemId);
            if (interactionState == null)
            {
                return;
            }

            _gameState.EnterDirectionMode();
            _scriptedDirectionModeOwnerActive = true;
            _activeNpcInteractionNpc = FindNpcById(entry.TargetNpcId);
            _activeNpcInteractionNpcId = entry.TargetNpcId;
            _npcInteractionOverlay.Open(interactionState);

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.QuestDelivery) is QuestDeliveryWindow questDeliveryWindow)
            {
                questDeliveryWindow.Hide();
            }
        }

        private IReadOnlyList<string> BuildClassCompetitionPageLines()
        {
            var lines = new List<string>();
            var build = _playerManager?.Player?.Build;
            if (build != null)
            {
                lines.Add($"{build.Name}  Lv.{Math.Max(1, build.Level)}  {build.JobName}");
                lines.Add($"Map {(_mapBoard?.MapInfo?.id ?? 0)}  Fame {build.Fame}  HP {Math.Max(0, build.HP)}/{Math.Max(1, build.MaxHP)}");
                lines.Add($"World rank seed: {(build.WorldRank > 0 ? $"#{build.WorldRank}" : "local only")}  Job rank seed: {(build.JobRank > 0 ? $"#{build.JobRank}" : "local only")}");
                lines.Add($"Combat seed: PAD {build.TotalAttack}  MAD {build.TotalMagicAttack}  ACC {build.TotalAccuracy}  EVA {build.TotalAvoidability}");
            }
            else
            {
                lines.Add("No active player build is bound to the simulator.");
            }

            lines.Add("This page is opened only from the packet-owned local-user branch.");
            lines.Add("The client constructor takes no packet payload, so this simulator page binds runtime status instead of a menu stub.");
            lines.Add("No server-fed class ladder payload is present, so standings stay seeded from the active local build only.");

            if (_lastClassCompetitionOpenTick != int.MinValue)
            {
                lines.Add($"Last packet launch tick: {_lastClassCompetitionOpenTick.ToString(CultureInfo.InvariantCulture)}");
            }

            return lines;
        }

        private string BuildClassCompetitionFooter()
        {
            if (_packetOwnedUtilityRequestTick == int.MinValue)
            {
                return "Utility request timing idle.";
            }

            int ageMs = Math.Max(0, unchecked(currTickCount - _packetOwnedUtilityRequestTick));
            return $"Shared request stamp: {_packetOwnedUtilityRequestTick} ({ageMs}ms ago)";
        }

        private void EnsureLocalUtilityPacketInboxState(bool shouldRun)
        {
            if (!shouldRun || !_localUtilityPacketInboxEnabled)
            {
                if (_localUtilityPacketInbox.IsRunning)
                {
                    _localUtilityPacketInbox.Stop();
                }

                return;
            }

            if (_localUtilityPacketInbox.IsRunning && _localUtilityPacketInbox.Port == _localUtilityPacketInboxConfiguredPort)
            {
                return;
            }

            if (_localUtilityPacketInbox.IsRunning)
            {
                _localUtilityPacketInbox.Stop();
            }

            try
            {
                _localUtilityPacketInbox.Start(_localUtilityPacketInboxConfiguredPort);
            }
            catch (Exception ex)
            {
                _localUtilityPacketInbox.Stop();
                _chat?.AddMessage($"Local utility packet inbox failed to start: {ex.Message}", Color.OrangeRed, currTickCount);
            }
        }

        private void DrainLocalUtilityPacketInbox()
        {
            while (_localUtilityPacketInbox.TryDequeue(out LocalUtilityPacketInboxMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                bool applied = TryApplyPacketOwnedUtilityPacket(message.PacketType, message.Payload, out string detail);
                _localUtilityPacketInbox.RecordDispatchResult(message, applied, detail);
                if (!string.IsNullOrWhiteSpace(detail))
                {
                    _chat?.AddMessage(detail, applied ? new Color(255, 228, 151) : Color.OrangeRed, currTickCount);
                }
            }
        }

        private string DescribeLocalUtilityPacketInboxStatus()
        {
            string enabledText = _localUtilityPacketInboxEnabled ? "enabled" : "disabled";
            string listeningText = _localUtilityPacketInbox.IsRunning
                ? $"listening on 127.0.0.1:{_localUtilityPacketInbox.Port}"
                : $"configured for 127.0.0.1:{_localUtilityPacketInboxConfiguredPort}";
            return $"Local utility packet inbox {enabledText}, {listeningText}, received {_localUtilityPacketInbox.ReceivedCount} packet(s).";
        }

        private bool TryApplyPacketOwnedUtilityPacket(int packetType, byte[] payload, out string message)
        {
            message = null;
            switch (packetType)
            {
                case LocalUtilityPacketInboxManager.OpenUiPacketType:
                case LocalUtilityPacketInboxManager.OpenUiClientPacketType:
                    return TryApplyPacketOwnedOpenUiPayload(payload, out message);

                case LocalUtilityPacketInboxManager.OpenUiWithOptionPacketType:
                case LocalUtilityPacketInboxManager.OpenUiWithOptionClientPacketType:
                    return TryApplyPacketOwnedOpenUiWithOptionPayload(payload, out message);

                case LocalUtilityPacketInboxManager.GoToCommoditySnPacketType:
                case LocalUtilityPacketInboxManager.GoToCommoditySnClientPacketType:
                    return TryApplyPacketOwnedCommodityPayload(payload, out message);

                case LocalUtilityPacketInboxManager.NoticeMsgPacketType:
                case LocalUtilityPacketInboxManager.NoticeMsgClientPacketType:
                    return TryApplyPacketOwnedNoticePayload(payload, out message);

                case LocalUtilityPacketInboxManager.ChatMsgPacketType:
                case LocalUtilityPacketInboxManager.ChatMsgClientPacketType:
                    return TryApplyPacketOwnedChatPayload(payload, out message);

                case LocalUtilityPacketInboxManager.BuffzoneEffectPacketType:
                case LocalUtilityPacketInboxManager.BuffzoneEffectClientPacketType:
                    return TryApplyPacketOwnedStringPayload(payload, ApplyPacketOwnedBuffzoneEffect, "Buff-zone payload is missing.", out message);

                case LocalUtilityPacketInboxManager.PlayEventSoundPacketType:
                case LocalUtilityPacketInboxManager.PlayEventSoundClientPacketType:
                    return TryApplyPacketOwnedStringPayload(payload, descriptor => ApplyPacketOwnedEventSound(descriptor, minigame: false), "Event-sound payload is missing.", out message);

                case LocalUtilityPacketInboxManager.PlayMinigameSoundPacketType:
                case LocalUtilityPacketInboxManager.PlayMinigameSoundClientPacketType:
                    return TryApplyPacketOwnedStringPayload(payload, descriptor => ApplyPacketOwnedEventSound(descriptor, minigame: true), "Minigame-sound payload is missing.", out message);

                case LocalUtilityPacketInboxManager.AskApspEventPacketType:
                case LocalUtilityPacketInboxManager.AskApspEventClientPacketType:
                    return TryApplyPacketOwnedStringPayload(payload, ApplyPacketOwnedAskApspEvent, "AP/SP payload is missing.", out message);

                case LocalUtilityPacketInboxManager.FollowCharacterFailedPacketType:
                case LocalUtilityPacketInboxManager.FollowCharacterFailedClientPacketType:
                    return TryApplyPacketOwnedFollowCharacterFailedPayload(payload, out message);

                case LocalUtilityPacketInboxManager.NotifyHpDecByFieldPacketType:
                    return TryApplyPacketOwnedFieldHazardPayload(payload, out message);

                case LocalUtilityPacketInboxManager.OpenClassCompetitionPagePacketType:
                    message = ApplyClassCompetitionPageLaunch();
                    return true;

                case LocalUtilityPacketInboxManager.DamageMeterPacketType:
                    return TryApplyPacketOwnedDamageMeterPayload(payload, out message);

                case LocalUtilityPacketInboxManager.QuestGuideResultPacketType:
                    return TryApplyPacketOwnedQuestGuidePayload(payload, out message);

                case LocalUtilityPacketInboxManager.DeliveryQuestPacketType:
                    return TryApplyPacketOwnedDeliveryQuestPayload(payload, out message);

                case LocalUtilityPacketInboxManager.SkillCooltimeSetPacketType:
                    return TryApplyPacketOwnedSkillCooltimePayload(payload, out message);

                default:
                    message = $"Unsupported local utility packet type {packetType}.";
                    return false;
            }
        }

        private bool TryApplyPacketOwnedDeliveryQuestPayload(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length < 12)
            {
                message = "Delivery-quest payload must contain questId, itemId, and the disallowed quest count.";
                return false;
            }

            try
            {
                using var stream = new MemoryStream(payload, writable: false);
                using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: false);
                int questId = reader.ReadInt32();
                int itemId = reader.ReadInt32();
                int disallowedCount = reader.ReadInt32();
                if (disallowedCount < 0)
                {
                    message = "Delivery-quest payload declared a negative disallowed-quest count.";
                    return false;
                }

                var disallowedQuestIds = new List<int>(disallowedCount);
                for (int i = 0; i < disallowedCount; i++)
                {
                    disallowedQuestIds.Add(reader.ReadInt32());
                }

                message = ApplyDeliveryQuestLaunch(questId, itemId, disallowedQuestIds);
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                message = $"Delivery-quest payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private string DescribePacketOwnedUtilityDispatchStatus(int currentTickCount)
        {
            string openUiStatus = _lastPacketOwnedOpenUiType >= 0
                ? _lastPacketOwnedOpenUiOption >= 0
                    ? $"Last UI open request: #{_lastPacketOwnedOpenUiType} (option {_lastPacketOwnedOpenUiOption})."
                    : $"Last UI open request: #{_lastPacketOwnedOpenUiType}."
                : "Last UI open request: none.";
            string commodityStatus = _lastPacketOwnedCommodityRequestTick != int.MinValue
                ? $"Commodity SN: {_lastPacketOwnedCommoditySerialNumber}."
                : "Commodity SN: none.";
            string requestStampStatus = _packetOwnedUtilityRequestTick != int.MinValue
                ? $"Shared request stamp age: {Math.Max(0, unchecked(currentTickCount - _packetOwnedUtilityRequestTick))} ms."
                : "Shared request stamp age: none.";
            string guideStatus = _packetQuestGuideTargetsByMobId.Count > 0
                ? $"Quest guide targets: {_packetQuestGuideTargetsByMobId.Count} mob family(s) for quest #{_packetQuestGuideQuestId}."
                : "Quest guide targets: none.";
            string questDemandStatus = _lastQuestDemandQueryVisibleItemIds.Count > 0
                ? $"Quest demand items: {_lastQuestDemandQueryVisibleItemIds.Count} visible for quest #{_lastQuestDemandItemQueryQuestId}, {_lastQuestDemandQueryHiddenItemCount} hidden."
                : "Quest demand items: none.";
            string deliveryStatus = _lastDeliveryQuestId > 0 || _lastDeliveryItemId > 0
                ? $"Delivery quest: quest #{_lastDeliveryQuestId}, item {_lastDeliveryItemId}, blocked {_lastDeliveryDisallowedQuestIds.Count}."
                : "Delivery quest: none.";
            string classCompetitionStatus = _lastClassCompetitionOpenTick != int.MinValue
                ? $"Class competition age: {Math.Max(0, unchecked(currentTickCount - _lastClassCompetitionOpenTick))} ms."
                : "Class competition age: none.";
            string noticeStatus = string.IsNullOrWhiteSpace(_lastPacketOwnedNoticeMessage)
                ? "Notice: none."
                : $"Notice: {TruncatePacketOwnedUtilityText(_lastPacketOwnedNoticeMessage)}";
            string chatStatus = string.IsNullOrWhiteSpace(_lastPacketOwnedChatMessage)
                ? "Chat: none."
                : $"Chat: {TruncatePacketOwnedUtilityText(_lastPacketOwnedChatMessage)}";
            string buffzoneStatus = string.IsNullOrWhiteSpace(_lastPacketOwnedBuffzoneMessage)
                ? "Buff zone: none."
                : $"Buff zone: {TruncatePacketOwnedUtilityText(_lastPacketOwnedBuffzoneMessage)}";
            string apspStatus = string.IsNullOrWhiteSpace(_lastPacketOwnedAskApspMessage)
                ? "AP/SP event: none."
                : $"AP/SP event: {TruncatePacketOwnedUtilityText(_lastPacketOwnedAskApspMessage)}";
            string followStatus = string.IsNullOrWhiteSpace(_lastPacketOwnedFollowFailureMessage)
                ? "Follow failure: none."
                : _lastPacketOwnedFollowFailureReason.HasValue
                    ? $"Follow failure: reason {_lastPacketOwnedFollowFailureReason.Value}, driver {_lastPacketOwnedFollowFailureDriverId}, cleared={_lastPacketOwnedFollowFailureClearedPending}. {TruncatePacketOwnedUtilityText(_lastPacketOwnedFollowFailureMessage)}"
                    : $"Follow failure: {TruncatePacketOwnedUtilityText(_lastPacketOwnedFollowFailureMessage)}";
            string soundStatus = string.IsNullOrWhiteSpace(_lastPacketOwnedEventSoundDescriptor) && string.IsNullOrWhiteSpace(_lastPacketOwnedMinigameSoundDescriptor)
                ? "Sound cues: none."
                : $"Sound cues: event={(_lastPacketOwnedEventSoundDescriptor ?? "none")}, minigame={(_lastPacketOwnedMinigameSoundDescriptor ?? "none")}.";

            return string.Join(" ", new[]
            {
                openUiStatus,
                commodityStatus,
                requestStampStatus,
                guideStatus,
                questDemandStatus,
                deliveryStatus,
                classCompetitionStatus,
                noticeStatus,
                chatStatus,
                buffzoneStatus,
                apspStatus,
                followStatus,
                soundStatus
            });
        }

        private static string TruncatePacketOwnedUtilityText(string value, int maxLength = 80)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim();
            return trimmed.Length <= maxLength
                ? trimmed
                : $"{trimmed[..Math.Max(0, maxLength - 3)]}...";
        }

        private string ApplyPacketOwnedOpenUi(int uiType, int defaultTab = -1)
        {
            StampPacketOwnedUtilityRequestState();
            _lastPacketOwnedOpenUiType = uiType;
            _lastPacketOwnedOpenUiOption = -1;

            return uiType switch
            {
                0 => ShowPacketOwnedWindow(MapSimulatorWindowNames.Inventory, "Inventory"),
                1 => ShowPacketOwnedWindow(MapSimulatorWindowNames.Equipment, "Equipment"),
                2 => ShowPacketOwnedWindow(MapSimulatorWindowNames.Ability, "Stat"),
                3 => ShowPacketOwnedWindow(MapSimulatorWindowNames.Skills, defaultTab >= 0 ? $"Skills (tab {defaultTab})" : "Skills"),
                5 => ShowPacketOwnedWindow(MapSimulatorWindowNames.KeyConfig, "Key Config"),
                6 => ShowPacketOwnedWindow(MapSimulatorWindowNames.Quest, defaultTab >= 0 ? $"Quest (tab {defaultTab})" : "Quest"),
                10 => ShowPacketOwnedWindow(MapSimulatorWindowNames.CharacterInfo, "Character Info"),
                21 => ApplyPacketOwnedPartySearchLaunch(defaultTab),
                22 => ShowPacketOwnedWindow(MapSimulatorWindowNames.ItemMaker, "Item Maker"),
                25 => ShowPacketOwnedWindow(MapSimulatorWindowNames.Ranking, "Ranking"),
                26 => ShowPacketOwnedWindow(MapSimulatorWindowNames.FamilyChart, "Family"),
                27 => ShowPacketOwnedWindow(MapSimulatorWindowNames.FamilyTree, "Family Tree"),
                39 => ShowPacketOwnedWindow(MapSimulatorWindowNames.GuildBbs, "Guild BBS"),
                41 => ShowPacketOwnedWindow(MapSimulatorWindowNames.SocialSearch, "Find Friend"),
                _ => ReportUnsupportedPacketOwnedOpenUi(uiType, defaultTab)
            };
        }

        private string ApplyPacketOwnedOpenUiWithOption(int uiType, int option)
        {
            StampPacketOwnedUtilityRequestState();
            _lastPacketOwnedOpenUiType = uiType;
            _lastPacketOwnedOpenUiOption = option;

            if (uiType == 21)
            {
                return ApplyPacketOwnedPartySearchLaunch(option);
            }

            if (uiType == 33)
            {
                string blockingOwner = GetVisibleUniqueModelessOwner(MapSimulatorWindowNames.RepairDurability);
                if (!string.IsNullOrWhiteSpace(blockingOwner))
                {
                    string blockedMessage = $"Packet-owned durability repair was suppressed because {blockingOwner} is already open.";
                    ShowUtilityFeedbackMessage(blockedMessage);
                    return blockedMessage;
                }

                if (!TryShowRepairDurabilityWindow(option, out RepairDurabilityWindow repairWindow, trackDirectionModeOwner: true))
                {
                    const string unavailable = "Repair durability owner is not available in this UI build.";
                    ShowUtilityFeedbackMessage(unavailable);
                    return unavailable;
                }

                string npcName = ResolveNpcDisplayName(option);
                string message = option > 0
                    ? $"Opened packet-owned durability repair through {npcName}."
                    : "Opened packet-owned durability repair.";
                repairWindow.SetStatusMessage(GetRepairDurabilityStatusMessage());
                ShowUtilityFeedbackMessage(message);
                return message;
            }

            if (uiType == 7)
            {
                return ApplyPacketOwnedSocialListToggle(option);
            }

            string baseMessage = ApplyPacketOwnedOpenUi(uiType);
            return string.IsNullOrWhiteSpace(baseMessage)
                ? $"Packet-owned UI open-with-option #{uiType} ({option}) did not resolve to a simulator owner."
                : $"{baseMessage} Option {option} was preserved on the packet-owned dispatch seam.";
        }

        private string ApplyPacketOwnedPartySearchLaunch(int option)
        {
            _socialListRuntime.OpenSearchWindow(SocialSearchTab.Party);
            WireSocialSearchWindowData();

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.SocialSearch) is not UIWindowBase window)
            {
                const string unavailable = "Party search owner is not available in this UI build.";
                ShowUtilityFeedbackMessage(unavailable);
                return unavailable;
            }

            ShowWindow(MapSimulatorWindowNames.SocialSearch, window, trackDirectionModeOwner: true);
            string message = option >= 0
                ? $"Opened packet-owned party search and preserved request option {option} for the simulator search owner."
                : "Opened packet-owned party search.";
            ShowUtilityFeedbackMessage(message);
            return message;
        }

        private string ApplyPacketOwnedSocialListToggle(int option)
        {
            WireSocialListWindowData();

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.SocialList) is not UIWindowBase socialListWindow)
            {
                const string unavailable = "Social List owner is not available in this UI build.";
                ShowUtilityFeedbackMessage(unavailable);
                return unavailable;
            }

            bool hasRequestedTab = TryResolvePacketOwnedSocialListTab(option, out SocialListTab requestedTab);
            SocialListTab targetTab = hasRequestedTab ? requestedTab : _socialListRuntime.CurrentTab;
            if (socialListWindow.IsVisible && hasRequestedTab && _socialListRuntime.CurrentTab == targetTab)
            {
                uiWindowManager.HideWindow(MapSimulatorWindowNames.SocialList);
                string closedMessage = $"Closed packet-owned Social List ({DescribePacketOwnedSocialListTab(targetTab)} tab).";
                ShowUtilityFeedbackMessage(closedMessage);
                return closedMessage;
            }

            _socialListRuntime.SelectTab(targetTab);
            ShowWindow(MapSimulatorWindowNames.SocialList, socialListWindow, trackDirectionModeOwner: true);

            string message = hasRequestedTab
                ? $"Opened packet-owned Social List on the {DescribePacketOwnedSocialListTab(targetTab)} tab."
                : $"Opened packet-owned Social List and preserved unmapped tab option {option} on the existing simulator tab seam.";
            ShowUtilityFeedbackMessage(message);
            return message;
        }

        private static bool TryResolvePacketOwnedSocialListTab(int option, out SocialListTab tab)
        {
            tab = SocialListTab.Friend;
            if (!Enum.IsDefined(typeof(SocialListTab), option))
            {
                return false;
            }

            tab = (SocialListTab)option;
            return true;
        }

        private static string DescribePacketOwnedSocialListTab(SocialListTab tab)
        {
            return tab switch
            {
                SocialListTab.Friend => "Friend",
                SocialListTab.Party => "Party",
                SocialListTab.Guild => "Guild",
                SocialListTab.Alliance => "Alliance",
                SocialListTab.Blacklist => "Blacklist",
                _ => "Social"
            };
        }

        private string ReportUnsupportedPacketOwnedOpenUi(int uiType, int defaultTab)
        {
            string message = defaultTab >= 0
                ? $"Packet-owned UI open #{uiType} (tab {defaultTab}) does not map to a simulator owner yet."
                : $"Packet-owned UI open #{uiType} does not map to a simulator owner yet.";
            ShowUtilityFeedbackMessage(message);
            return message;
        }

        private string ShowPacketOwnedWindow(string windowName, string displayName)
        {
            if (uiWindowManager?.GetWindow(windowName) is not UIWindowBase window)
            {
                string unavailable = $"{displayName} owner is not available in this UI build.";
                ShowUtilityFeedbackMessage(unavailable);
                return unavailable;
            }

            ShowWindow(windowName, window, trackDirectionModeOwner: true);
            return $"Opened packet-owned {displayName}.";
        }

        private string ApplyPacketOwnedGoToCommoditySn(int commoditySerialNumber)
        {
            StampPacketOwnedUtilityRequestState();
            _lastPacketOwnedCommoditySerialNumber = Math.Max(0, commoditySerialNumber);
            _lastPacketOwnedCommodityRequestTick = Environment.TickCount;

            string shopMessage = ShowPacketOwnedWindow(MapSimulatorWindowNames.CashShop, "Cash Shop");
            bool focusedCommodity = false;
            if (_lastPacketOwnedCommoditySerialNumber > 0
                && uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShop) is AdminShopDialogUI cashShopWindow)
            {
                focusedCommodity = cashShopWindow.TryFocusCommoditySerialNumber(_lastPacketOwnedCommoditySerialNumber);
            }

            string message = focusedCommodity
                ? $"Stored packet-owned commodity SN {_lastPacketOwnedCommoditySerialNumber}, requested shop migration, and focused the matching Cash Shop sample row. {shopMessage}"
                : $"Stored packet-owned commodity SN {_lastPacketOwnedCommoditySerialNumber} and requested shop migration. {shopMessage}";
            ShowUtilityFeedbackMessage(message);
            return message;
        }

        private string ApplyPacketOwnedNoticeMessage(string message)
        {
            StampPacketOwnedUtilityRequestState();
            _lastPacketOwnedNoticeMessage = message?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedNoticeMessage))
            {
                _chat?.AddClientChatMessage(
                    $"[Notice] {_lastPacketOwnedNoticeMessage}",
                    Environment.TickCount,
                    13);
            }

            return string.IsNullOrWhiteSpace(_lastPacketOwnedNoticeMessage)
                ? "Packet-owned notice was empty."
                : $"Queued packet-owned notice: {_lastPacketOwnedNoticeMessage}";
        }

        private string ApplyPacketOwnedChatMessage(string message, string channel = null)
        {
            return ApplyPacketOwnedChatMessage(message, null, channel);
        }

        private string ApplyPacketOwnedChatMessage(string message, int? chatLogType, string channel = null)
        {
            StampPacketOwnedUtilityRequestState();
            _lastPacketOwnedChatMessage = message?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_lastPacketOwnedChatMessage))
            {
                return "Packet-owned chat line was empty.";
            }

            if (chatLogType.HasValue)
            {
                _chat?.AddClientChatMessage(_lastPacketOwnedChatMessage, Environment.TickCount, chatLogType.Value);
                return $"Queued packet-owned chat line (type {chatLogType.Value}): {_lastPacketOwnedChatMessage}";
            }

            if (string.IsNullOrWhiteSpace(channel)
                && TryParsePacketOwnedStructuredChatPayload(
                    _lastPacketOwnedChatMessage,
                    out string payloadChannel,
                    out string payloadMessage))
            {
                channel = payloadChannel;
                _lastPacketOwnedChatMessage = payloadMessage;
            }

            PacketOwnedChatRoute route = ResolvePacketOwnedChatRoute(_lastPacketOwnedChatMessage, channel);
            _chat?.AddClientChatMessage(route.Line, Environment.TickCount, route.ChatLogType, route.WhisperTargetCandidate);
            string line = route.Line;
            return $"Queued packet-owned chat line: {line}";
        }

        private string ApplyPacketOwnedSkillCooltime(int skillId, int remainingSeconds)
        {
            StampPacketOwnedUtilityRequestState();
            if (skillId <= 0)
            {
                const string invalidSkill = "Packet-owned skill cooldown payload did not contain a valid skill id.";
                ShowUtilityFeedbackMessage(invalidSkill);
                return invalidSkill;
            }

            if (_playerManager?.Skills == null)
            {
                const string unavailable = "Skill cooldown runtime is not available in this simulator build.";
                ShowUtilityFeedbackMessage(unavailable);
                return unavailable;
            }

            int remainingMs = Math.Max(0, remainingSeconds) * 1000;
            if (remainingMs > 0)
            {
                _playerManager.Skills.SetServerCooldownRemaining(skillId, remainingMs, currTickCount);
            }
            else
            {
                _playerManager.Skills.ClearServerCooldown(skillId);
            }

            var skill = _playerManager.Skills.GetSkillData(skillId) ?? _playerManager.SkillLoader?.LoadSkill(skillId);
            string skillName = skill?.Name ?? $"Skill {skillId}";
            string message = remainingMs > 0
                ? $"Applied packet-owned skill cooldown for {skillName}: {FormatCooldownNotificationSeconds(remainingMs)} remaining."
                : $"Cleared packet-owned skill cooldown for {skillName}.";
            ShowUtilityFeedbackMessage(message);
            return message;
        }

        private string ApplyPacketOwnedBuffzoneEffect(string message)
        {
            StampPacketOwnedUtilityRequestState();
            _lastPacketOwnedBuffzoneMessage = string.IsNullOrWhiteSpace(message)
                ? "Buff-zone effect triggered."
                : message.Trim();
            ShowUtilityFeedbackMessage(_lastPacketOwnedBuffzoneMessage);
            return _lastPacketOwnedBuffzoneMessage;
        }

        private string ApplyPacketOwnedAskApspEvent(string message)
        {
            StampPacketOwnedUtilityRequestState();
            _lastPacketOwnedAskApspMessage = string.IsNullOrWhiteSpace(message)
                ? "Packet-owned AP/SP event prompt triggered."
                : message.Trim();
            ShowUtilityFeedbackMessage(_lastPacketOwnedAskApspMessage);
            return _lastPacketOwnedAskApspMessage;
        }

        private string ApplyPacketOwnedFollowCharacterFailed(string message)
        {
            StampPacketOwnedUtilityRequestState();
            _lastPacketOwnedFollowFailureMessage = string.IsNullOrWhiteSpace(message)
                ? "Packet-owned follow-character request failed."
                : message.Trim();
            _lastPacketOwnedFollowFailureReason = null;
            _lastPacketOwnedFollowFailureDriverId = 0;
            _lastPacketOwnedFollowFailureClearedPending = false;
            _chat?.AddClientChatMessage($"[Error] {_lastPacketOwnedFollowFailureMessage}", Environment.TickCount, 15);
            return _lastPacketOwnedFollowFailureMessage;
        }

        private string ApplyPacketOwnedFollowCharacterFailed(FollowCharacterFailureInfo info)
        {
            StampPacketOwnedUtilityRequestState();
            _lastPacketOwnedFollowFailureReason = info.ReasonCode;
            _lastPacketOwnedFollowFailureDriverId = info.DriverId;
            _lastPacketOwnedFollowFailureClearedPending = info.ClearsPendingRequest;
            _lastPacketOwnedFollowFailureMessage = string.IsNullOrWhiteSpace(info.Message)
                ? "Packet-owned follow-character request failed."
                : info.Message.Trim();

            if (info.ClearsPendingRequest)
            {
                ShowUtilityFeedbackMessage(_lastPacketOwnedFollowFailureMessage);
            }
            else
            {
                _chat?.AddClientChatMessage($"[Error] {_lastPacketOwnedFollowFailureMessage}", Environment.TickCount, 15);
            }

            return _lastPacketOwnedFollowFailureMessage;
        }

        private bool TryApplyPacketOwnedFollowCharacterFailedPayload(byte[] payload, out string message)
        {
            if (!FollowCharacterFailureCodec.TryDecodePayload(payload, ResolvePacketOwnedRemoteCharacterName, out FollowCharacterFailureInfo info))
            {
                return TryApplyPacketOwnedStringPayload(payload, ApplyPacketOwnedFollowCharacterFailed, "Follow-failed payload is missing.", out message);
            }

            message = ApplyPacketOwnedFollowCharacterFailed(info);
            return true;
        }

        private readonly record struct PacketOwnedChatRoute(
            string Line,
            int ChatLogType,
            string WhisperTargetCandidate = null);

        private static PacketOwnedChatRoute ResolvePacketOwnedChatRoute(string message, string channel)
        {
            string resolvedMessage = message?.Trim() ?? string.Empty;
            if (TryResolvePacketOwnedChatChannel(channel, resolvedMessage, out PacketOwnedChatRoute route))
            {
                return route;
            }

            return new PacketOwnedChatRoute(
                FormatPacketOwnedChatLine(resolvedMessage, channel),
                -1);
        }

        private static bool TryParsePacketOwnedStructuredChatPayload(
            string payload,
            out string channel,
            out string message)
        {
            channel = null;
            message = payload?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            int separatorIndex = message.IndexOf('|');
            if (separatorIndex <= 0 || separatorIndex >= message.Length - 1)
            {
                return false;
            }

            string candidateChannel = message[..separatorIndex].Trim();
            if (!IsSupportedPacketOwnedChatChannel(candidateChannel))
            {
                return false;
            }

            channel = candidateChannel;
            message = message[(separatorIndex + 1)..].Trim();
            return !string.IsNullOrWhiteSpace(message);
        }

        private static bool IsSupportedPacketOwnedChatChannel(string channel)
        {
            if (string.IsNullOrWhiteSpace(channel))
            {
                return false;
            }

            return TryResolvePacketOwnedChatChannel(channel, string.Empty, out _);
        }

        private static bool TryResolvePacketOwnedChatChannel(
            string channel,
            string message,
            out PacketOwnedChatRoute route)
        {
            route = default;
            if (string.IsNullOrWhiteSpace(channel))
            {
                return false;
            }

            string[] segments = channel.Split(':');
            string mode = segments[0].Trim().ToLowerInvariant();
            string primaryTarget = segments.Length >= 2 ? segments[1].Trim() : string.Empty;

            switch (mode)
            {
                case "all":
                case "say":
                case "default":
                    route = new PacketOwnedChatRoute(message, 0);
                    return true;

                case "notice":
                    route = new PacketOwnedChatRoute($"[Notice] {message}", 13);
                    return true;

                case "system":
                    route = new PacketOwnedChatRoute($"[System] {message}", 12);
                    return true;

                case "party":
                    route = new PacketOwnedChatRoute($"[Party] {message}", 2);
                    return true;

                case "friend":
                    route = new PacketOwnedChatRoute($"[Friend] {message}", 3);
                    return true;

                case "guild":
                    route = new PacketOwnedChatRoute($"[Guild] {message}", 4);
                    return true;

                case "alliance":
                    route = new PacketOwnedChatRoute($"[Alliance] {message}", 5);
                    return true;

                case "association":
                    route = new PacketOwnedChatRoute($"[Association] {message}", 5);
                    return true;

                case "expedition":
                    route = new PacketOwnedChatRoute($"[Expedition] {message}", 26);
                    return true;

                case "error":
                    route = new PacketOwnedChatRoute($"[Error] {message}", 15);
                    return true;

                case "whisper":
                case "whisperin":
                case "incomingwhisper":
                    if (string.IsNullOrWhiteSpace(primaryTarget))
                    {
                        route = new PacketOwnedChatRoute($"[Whisper] {message}", 16);
                        return true;
                    }

                    route = new PacketOwnedChatRoute(
                        $"[Whisper] {primaryTarget}: {message}",
                        16,
                        primaryTarget);
                    return true;

                case "whisperout":
                case "whisperto":
                case "outgoingwhisper":
                    if (string.IsNullOrWhiteSpace(primaryTarget))
                    {
                        return false;
                    }

                    route = new PacketOwnedChatRoute(
                        $"> {primaryTarget}: {message}",
                        14,
                        primaryTarget);
                    return true;

                case "channel":
                case "type19":
                case "ltype19":
                case "19":
                    route = new PacketOwnedChatRoute(message, 19);
                    return true;

                case "type11":
                case "ltype11":
                case "11":
                    route = new PacketOwnedChatRoute(message, 11);
                    return true;

                case "type18":
                case "ltype18":
                case "18":
                    route = new PacketOwnedChatRoute(message, 18);
                    return true;

                case "type20":
                case "ltype20":
                case "20":
                    route = new PacketOwnedChatRoute(message, 20);
                    return true;

                case "type21":
                case "ltype21":
                case "21":
                    route = new PacketOwnedChatRoute(message, 21);
                    return true;

                case "type22":
                case "ltype22":
                case "22":
                    route = new PacketOwnedChatRoute(message, 22);
                    return true;

                case "type23":
                case "ltype23":
                case "23":
                    route = new PacketOwnedChatRoute(message, 23);
                    return true;

                default:
                    return false;
            }
        }

        private static string FormatPacketOwnedChatLine(string message, string channel)
        {
            if (string.IsNullOrWhiteSpace(channel))
            {
                return message;
            }

            string normalized = channel.Trim().ToLowerInvariant();
            return normalized switch
            {
                "notice" => $"[Notice] {message}",
                "system" => $"[System] {message}",
                "party" => $"[Party] {message}",
                "friend" => $"[Friend] {message}",
                "guild" => $"[Guild] {message}",
                "alliance" or "association" => $"[Alliance] {message}",
                "expedition" => $"[Expedition] {message}",
                "error" => $"[Error] {message}",
                _ => message
            };
        }

        private string ApplyPacketOwnedEventSound(string descriptor, bool minigame)
        {
            StampPacketOwnedUtilityRequestState();
            if (!TryPlayPacketOwnedWzSound(descriptor, minigame ? "MiniGame.img" : null, out string resolvedDescriptor, out string error))
            {
                ShowUtilityFeedbackMessage(error);
                return error;
            }

            if (minigame)
            {
                _lastPacketOwnedMinigameSoundDescriptor = resolvedDescriptor;
            }
            else
            {
                _lastPacketOwnedEventSoundDescriptor = resolvedDescriptor;
            }

            string message = $"Played packet-owned {(minigame ? "minigame" : "event")} sound {resolvedDescriptor}.";
            ShowUtilityFeedbackMessage(message);
            return message;
        }

        private bool TryPlayPacketOwnedWzSound(string descriptor, string defaultImageName, out string resolvedDescriptor, out string error)
        {
            resolvedDescriptor = null;
            error = null;

            if (_soundManager == null)
            {
                error = "Sound manager is not available in this simulator build.";
                return false;
            }

            if (!TryResolvePacketOwnedWzSound(descriptor, defaultImageName, out WzBinaryProperty soundProperty, out resolvedDescriptor))
            {
                error = $"Packet-owned sound '{descriptor}' was not found in the loaded Sound/*.img data.";
                return false;
            }

            string soundKey = $"PacketOwnedSound:{resolvedDescriptor}";
            _soundManager.RegisterSound(soundKey, soundProperty);
            _soundManager.PlaySound(soundKey);
            return true;
        }

        private static bool TryResolvePacketOwnedWzSound(string descriptor, string defaultImageName, out WzBinaryProperty soundProperty, out string resolvedDescriptor)
        {
            soundProperty = null;
            resolvedDescriptor = null;

            if (string.IsNullOrWhiteSpace(descriptor))
            {
                return false;
            }

            string normalized = descriptor.Trim().Replace('\\', '/');
            string[] segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return false;
            }

            List<string> imageCandidates = new();
            string propertyPath;

            if (segments.Length >= 2)
            {
                imageCandidates.Add(NormalizePacketOwnedSoundImageName(segments[0]));
                propertyPath = string.Join("/", segments.Skip(1));
            }
            else
            {
                propertyPath = segments[0];
                if (!string.IsNullOrWhiteSpace(defaultImageName))
                {
                    imageCandidates.Add(NormalizePacketOwnedSoundImageName(defaultImageName));
                }

                imageCandidates.Add("UI.img");
                imageCandidates.Add("Game.img");
                imageCandidates.Add("MiniGame.img");
            }

            foreach (string imageName in imageCandidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                WzImage soundImage = Program.FindImage("Sound", imageName);
                if (soundImage == null)
                {
                    continue;
                }

                string[] pathSegments = propertyPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (pathSegments.Length == 0)
                {
                    continue;
                }

                WzImageProperty current = soundImage[pathSegments[0]];
                for (int i = 1; i < pathSegments.Length; i++)
                {
                    current = current?[pathSegments[i]];
                    if (current == null)
                    {
                        break;
                    }
                }

                WzImageProperty resolved = WzInfoTools.GetRealProperty(current);
                if (resolved is WzBinaryProperty binaryProperty)
                {
                    soundProperty = binaryProperty;
                    resolvedDescriptor = $"{imageName[..^4]}/{propertyPath}";
                    return true;
                }
            }

            return false;
        }

        private static string NormalizePacketOwnedSoundImageName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "UI.img";
            }

            return value.EndsWith(".img", StringComparison.OrdinalIgnoreCase)
                ? value
                : $"{value}.img";
        }

        private bool TryApplyPacketOwnedOpenUiPayload(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length < 1)
            {
                message = "OpenUI payload must contain the raw UI id byte.";
                return false;
            }

            message = ApplyPacketOwnedOpenUi(payload[0]);
            return true;
        }

        private bool TryApplyPacketOwnedNoticePayload(byte[] payload, out string message)
        {
            return TryApplyPacketOwnedStringPayload(payload, ApplyPacketOwnedNoticeMessage, "Notice payload is missing.", out message);
        }

        private bool TryApplyPacketOwnedChatPayload(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length == 0)
            {
                message = "Chat payload is missing.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                ushort chatLogType = reader.ReadUInt16();
                string chatText = ReadPacketOwnedMapleString(reader);
                if (reader.BaseStream.Position == reader.BaseStream.Length && !string.IsNullOrWhiteSpace(chatText))
                {
                    message = ApplyPacketOwnedChatMessage(chatText, chatLogType);
                    return true;
                }
            }
            catch
            {
            }

            return TryApplyPacketOwnedStringPayload(payload, value => ApplyPacketOwnedChatMessage(value), "Chat payload is missing.", out message);
        }

        private bool TryApplyPacketOwnedOpenUiWithOptionPayload(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length < 8)
            {
                message = "OpenUIWithOption payload must contain uiType and option Int32 values.";
                return false;
            }

            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream);
            message = ApplyPacketOwnedOpenUiWithOption(reader.ReadInt32(), reader.ReadInt32());
            return true;
        }

        private bool TryApplyPacketOwnedCommodityPayload(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length < 4)
            {
                message = "Commodity payload must contain the commodity serial number Int32 value.";
                return false;
            }

            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream);
            message = ApplyPacketOwnedGoToCommoditySn(reader.ReadInt32());
            return true;
        }

        private bool TryApplyPacketOwnedSkillCooltimePayload(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length < 6)
            {
                message = "Skill-cooltime payload must contain skillId Int32 and remainSec UInt16 values.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                int skillId = reader.ReadInt32();
                int remainSeconds = reader.ReadUInt16();
                message = ApplyPacketOwnedSkillCooltime(skillId, remainSeconds);
                return true;
            }
            catch (Exception ex)
            {
                message = $"Skill-cooltime payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private bool TryApplyPacketOwnedQuestGuidePayload(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length < 1)
            {
                message = "Quest-guide payload is missing.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                byte mode = reader.ReadByte();
                if (mode == 2)
                {
                    message = ResetPacketQuestGuideLaunch();
                    return true;
                }

                if (mode != 1)
                {
                    message = $"Quest-guide payload mode {mode} is not modeled by the simulator.";
                    return false;
                }

                int questId = reader.ReadUInt16();
                int targetCount = reader.ReadInt32();
                Dictionary<int, IReadOnlyList<int>> targetsByMobId = new();
                for (int i = 0; i < targetCount; i++)
                {
                    int mobId = reader.ReadInt32();
                    int mapCount = reader.ReadUInt16();
                    List<int> mapIds = new();
                    for (int mapIndex = 0; mapIndex < mapCount; mapIndex++)
                    {
                        int mapId = reader.ReadInt32();
                        if (mapId > 0)
                        {
                            mapIds.Add(mapId);
                        }
                    }

                    if (mobId > 0 && mapIds.Count > 0)
                    {
                        targetsByMobId[mobId] = mapIds.Distinct().ToList();
                    }
                }

                message = ApplyPacketQuestGuideLaunch(questId, targetsByMobId);
                return true;
            }
            catch (Exception ex)
            {
                message = $"Quest-guide payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private bool TryApplyPacketOwnedDeliveryPayload(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length < 8)
            {
                message = "Delivery payload must contain questId and itemId Int32 values.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                int questId = reader.ReadInt32();
                int itemId = reader.ReadInt32();
                List<int> disallowedQuestIds = DecodePacketOwnedDisallowedDeliveryQuestIds(reader);
                message = ApplyDeliveryQuestLaunch(questId, itemId, disallowedQuestIds);
                return true;
            }
            catch (Exception ex)
            {
                message = $"Delivery payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private static List<int> DecodePacketOwnedDisallowedDeliveryQuestIds(BinaryReader reader)
        {
            List<int> questIds = new();
            int remaining = (int)(reader.BaseStream.Length - reader.BaseStream.Position);
            if (remaining <= 0)
            {
                return questIds;
            }

            long listStart = reader.BaseStream.Position;
            if (remaining >= 2)
            {
                ushort count = reader.ReadUInt16();
                int afterCount = (int)(reader.BaseStream.Length - reader.BaseStream.Position);
                if (count == 0)
                {
                    return questIds;
                }

                if (afterCount == count * sizeof(short))
                {
                    for (int i = 0; i < count; i++)
                    {
                        int questId = reader.ReadUInt16();
                        if (questId > 0)
                        {
                            questIds.Add(questId);
                        }
                    }

                    return questIds;
                }

                if (afterCount == count * sizeof(int))
                {
                    for (int i = 0; i < count; i++)
                    {
                        int questId = reader.ReadInt32();
                        if (questId > 0)
                        {
                            questIds.Add(questId);
                        }
                    }

                    return questIds;
                }

                reader.BaseStream.Position = listStart;
            }

            remaining = (int)(reader.BaseStream.Length - reader.BaseStream.Position);
            if (remaining % sizeof(int) == 0)
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    int questId = reader.ReadInt32();
                    if (questId > 0)
                    {
                        questIds.Add(questId);
                    }
                }
            }
            else if (remaining % sizeof(short) == 0)
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    int questId = reader.ReadUInt16();
                    if (questId > 0)
                    {
                        questIds.Add(questId);
                    }
                }
            }

            return questIds;
        }

        private bool TryApplyPacketOwnedStringPayload(byte[] payload, Func<string, string> applier, string emptyMessage, out string message)
        {
            message = emptyMessage;
            if (payload == null || payload.Length == 0)
            {
                return false;
            }

            if (!TryDecodePacketOwnedStringPayload(payload, out string decodedText))
            {
                return false;
            }

            message = applier(decodedText);
            return true;
        }

        private static bool TryDecodePacketOwnedStringPayload(byte[] payload, out string text)
        {
            text = null;
            if (payload == null || payload.Length == 0)
            {
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                string mapleString = ReadPacketOwnedMapleString(reader);
                if (reader.BaseStream.Position == reader.BaseStream.Length && !string.IsNullOrWhiteSpace(mapleString))
                {
                    text = mapleString.Trim();
                    return true;
                }
            }
            catch
            {
            }

            string utf8 = Encoding.UTF8.GetString(payload).TrimEnd('\0', '\r', '\n', ' ');
            if (!string.IsNullOrWhiteSpace(utf8))
            {
                text = utf8;
                return true;
            }

            return false;
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedUtilityCommand(string[] args)
        {
            int currentTickCount = Environment.TickCount;
            if (args.Length == 0)
            {
                return ChatCommandHandler.CommandResult.Info($"{DescribeLocalUtilityPacketInboxStatus()} {DescribePacketOwnedUtilityDispatchStatus(currentTickCount)} {_localUtilityPacketInbox.LastStatus}");
            }

            switch (args[0].ToLowerInvariant())
            {
                case "status":
                    return ChatCommandHandler.CommandResult.Info($"{DescribeLocalUtilityPacketInboxStatus()} {DescribePacketOwnedUtilityDispatchStatus(currentTickCount)} {_localUtilityPacketInbox.LastStatus}");

                case "openui":
                    if (args.Length < 2 || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int uiType))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility openui <uiType> [defaultTab]");
                    }

                    int defaultTab = -1;
                    if (args.Length >= 3 && !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out defaultTab))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility openui <uiType> [defaultTab]");
                    }

                    return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedOpenUi(uiType, defaultTab));

                case "openuiwithoption":
                    if (args.Length < 3
                        || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int optionUiType)
                        || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int optionValue))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility openuiwithoption <uiType> <option>");
                    }

                    return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedOpenUiWithOption(optionUiType, optionValue));

                case "commodity":
                    if (args.Length < 2 || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int commoditySn))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility commodity <serialNumber>");
                    }

                    return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedGoToCommoditySn(commoditySn));

                case "notice":
                    if (args.Length < 2)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility notice <text>");
                    }

                    return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedNoticeMessage(string.Join(" ", args.Skip(1))));

                case "chat":
                    if (args.Length < 2)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility chat [channel|type19|whisper:name|whisperout:name] <text>");
                    }

                    if (args.Length >= 3)
                    {
                        return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedChatMessage(string.Join(" ", args.Skip(2)), args[1]));
                    }

                    return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedChatMessage(args[1]));

                case "buffzone":
                    return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedBuffzoneEffect(args.Length >= 2 ? string.Join(" ", args.Skip(1)) : null));

                case "eventsound":
                    if (args.Length < 2)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility eventsound <image/path or path>");
                    }

                    return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedEventSound(string.Join(" ", args.Skip(1)), minigame: false));

                case "minigamesound":
                    if (args.Length < 2)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility minigamesound <image/path or path>");
                    }

                    return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedEventSound(string.Join(" ", args.Skip(1)), minigame: true));

                case "questguide":
                    if (args.Length >= 2 && string.Equals(args[1], "clear", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Ok(ResetPacketQuestGuideLaunch());
                    }

                    if (args.Length < 3 || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int questId))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility questguide <questId> <mobId:mapId[,mapId...]>... | /localutility questguide clear");
                    }

                    Dictionary<int, IReadOnlyList<int>> targetsByMobId = new();
                    for (int i = 2; i < args.Length; i++)
                    {
                        string[] parts = args[i].Split(':');
                        if (parts.Length != 2
                            || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int mobId))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /localutility questguide <questId> <mobId:mapId[,mapId...]>... | /localutility questguide clear");
                        }

                        List<int> mapIds = parts[1]
                            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(value => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedMapId) ? parsedMapId : 0)
                            .Where(mapId => mapId > 0)
                            .Distinct()
                            .ToList();
                        if (mapIds.Count == 0)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /localutility questguide <questId> <mobId:mapId[,mapId...]>... | /localutility questguide clear");
                        }

                        targetsByMobId[mobId] = mapIds;
                    }

                    return ChatCommandHandler.CommandResult.Ok(ApplyPacketQuestGuideLaunch(questId, targetsByMobId));

                case "delivery":
                    if (args.Length < 3
                        || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int deliveryQuestId)
                        || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int deliveryItemId))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility delivery <questId> <itemId> [blockedQuestIdsCsv]");
                    }

                    List<int> blockedQuestIds = new();
                    if (args.Length >= 4)
                    {
                        blockedQuestIds.AddRange(
                            args[3].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(value => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int blockedQuestId) ? blockedQuestId : 0)
                                .Where(blockedQuestId => blockedQuestId > 0));
                    }

                    return ChatCommandHandler.CommandResult.Ok(ApplyDeliveryQuestLaunch(deliveryQuestId, deliveryItemId, blockedQuestIds));

                case "classcompetition":
                    return ChatCommandHandler.CommandResult.Ok(ApplyClassCompetitionPageLaunch());

                case "apsp":
                    return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedAskApspEvent(args.Length >= 2 ? string.Join(" ", args.Skip(1)) : null));

                case "followfail":
                    if (args.Length >= 2
                        && int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int followReasonCode))
                    {
                        int followDriverId = 0;
                        if (args.Length >= 3
                            && !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out followDriverId))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /localutility followfail [reasonCode [driverId]|text]");
                        }

                        FollowCharacterFailureInfo info = FollowCharacterFailureCodec.Resolve(
                            followReasonCode,
                            followDriverId,
                            ResolvePacketOwnedRemoteCharacterName);
                        return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedFollowCharacterFailed(info));
                    }

                    return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedFollowCharacterFailed(args.Length >= 2 ? string.Join(" ", args.Skip(1)) : null));

                case "packet":
                case "packetraw":
                    return HandlePacketOwnedUtilityPacketCommand(args, rawHex: string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase));

                default:
                    return ChatCommandHandler.CommandResult.Error(
                        "Usage: /localutility [status|openui <uiType> [defaultTab]|openuiwithoption <uiType> <option>|commodity <serialNumber>|notice <text>|chat [channel|type19|whisper:name|whisperout:name] <text>|buffzone [text]|eventsound <image/path or path>|minigamesound <image/path or path>|questguide <questId> <mobId:mapId[,mapId...]>...|questguide clear|delivery <questId> <itemId> [blockedQuestIdsCsv]|classcompetition|apsp [text]|followfail [reasonCode [driverId]|text]|packet <openui|openuiwithoption|commodity|fade|balloon|damagemeter|hpdec|notice|chat|buffzone|eventsound|minigamesound|questguide|delivery|classcompetition|apspevent|followfail|243|250|267|274|275> [payloadhex=..|payloadb64=..]|packetraw <type> <hex>]");
            }
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedUtilityPacketCommand(string[] args, bool rawHex)
        {
            if (args.Length < 2)
            {
                return ChatCommandHandler.CommandResult.Error(
                    rawHex
                        ? "Usage: /localutility packetraw <type> <hex>"
                        : "Usage: /localutility packet <type> [payloadhex=..|payloadb64=..]");
            }

            byte[] payload = Array.Empty<byte>();
            if (rawHex)
            {
                if (args.Length < 3 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(2)), out payload))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /localutility packetraw <type> <hex>");
                }
            }
            else if (args.Length >= 3 && !TryParseBinaryPayloadArgument(args[2], out payload, out string payloadError))
            {
                return ChatCommandHandler.CommandResult.Error(payloadError ?? "Usage: /localutility packet <type> [payloadhex=..|payloadb64=..]");
            }

            bool applied;
            string message;
            if (int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int numericPacketType))
            {
                applied = TryApplyPacketOwnedUtilityPacket(numericPacketType, payload, out message);
                return applied
                    ? ChatCommandHandler.CommandResult.Ok(message)
                    : ChatCommandHandler.CommandResult.Error(message);
            }

            switch (args[1].ToLowerInvariant())
            {
                case "openui":
                    applied = TryApplyPacketOwnedOpenUiPayload(payload, out message);
                    break;
                case "openuiwithoption":
                    applied = TryApplyPacketOwnedOpenUiWithOptionPayload(payload, out message);
                    break;
                case "commodity":
                    applied = TryApplyPacketOwnedCommodityPayload(payload, out message);
                    break;
                case "fade":
                    applied = TryApplyPacketOwnedFieldFadePayload(payload, out message);
                    break;
                case "balloon":
                    applied = TryApplyPacketOwnedBalloonPayload(payload, out message);
                    break;
                case "damagemeter":
                    applied = TryApplyPacketOwnedDamageMeterPayload(payload, out message);
                    break;
                case "hpdec":
                    applied = TryApplyPacketOwnedFieldHazardPayload(payload, out message);
                    break;
                case "notice":
                    applied = TryApplyPacketOwnedStringPayload(payload, ApplyPacketOwnedNoticeMessage, "Notice payload is missing.", out message);
                    break;
                case "chat":
                    applied = TryApplyPacketOwnedStringPayload(payload, value => ApplyPacketOwnedChatMessage(value), "Chat payload is missing.", out message);
                    break;
                case "buffzone":
                    applied = TryApplyPacketOwnedStringPayload(payload, ApplyPacketOwnedBuffzoneEffect, "Buff-zone payload is missing.", out message);
                    break;
                case "eventsound":
                    applied = TryApplyPacketOwnedStringPayload(payload, descriptor => ApplyPacketOwnedEventSound(descriptor, minigame: false), "Event-sound payload is missing.", out message);
                    break;
                case "minigamesound":
                    applied = TryApplyPacketOwnedStringPayload(payload, descriptor => ApplyPacketOwnedEventSound(descriptor, minigame: true), "Minigame-sound payload is missing.", out message);
                    break;
                case "questguide":
                    applied = TryApplyPacketOwnedQuestGuidePayload(payload, out message);
                    break;
                case "delivery":
                    applied = TryApplyPacketOwnedDeliveryPayload(payload, out message);
                    break;
                case "classcompetition":
                    message = ApplyClassCompetitionPageLaunch();
                    applied = true;
                    break;
                case "apspevent":
                    applied = TryApplyPacketOwnedStringPayload(payload, ApplyPacketOwnedAskApspEvent, "AP/SP payload is missing.", out message);
                    break;
                case "followfail":
                    applied = TryApplyPacketOwnedFollowCharacterFailedPayload(payload, out message);
                    break;
                default:
                    return ChatCommandHandler.CommandResult.Error(
                        rawHex
                        ? "Usage: /localutility packetraw <openui|openuiwithoption|commodity|fade|balloon|damagemeter|hpdec|notice|chat|buffzone|eventsound|minigamesound|questguide|delivery|classcompetition|apspevent|followfail|skillcooltime|243|246|247|250|251|252|263|264|265|266|267|270|273|274|275|276> <hex>"
                        : "Usage: /localutility packet <openui|openuiwithoption|commodity|fade|balloon|damagemeter|hpdec|notice|chat|buffzone|eventsound|minigamesound|questguide|delivery|classcompetition|apspevent|followfail|skillcooltime|243|246|247|250|251|252|263|264|265|266|267|270|273|274|275|276> [payloadhex=..|payloadb64=..]");
            }

            return applied
                ? ChatCommandHandler.CommandResult.Ok(message)
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private string ResolvePacketOwnedRemoteCharacterName(int characterId)
        {
            if (characterId <= 0)
            {
                return null;
            }

            return _remoteUserPool != null && _remoteUserPool.TryGetActor(characterId, out var actor)
                ? actor?.Name
                : null;
        }

        private static string ResolvePacketGuideMobName(int mobId)
        {
            if (mobId <= 0)
            {
                return "Unknown mob";
            }

            try
            {
                WzImage stringImage = Program.FindImage("String", "Mob.img");
                string mobName = ReadWzString(stringImage?[mobId.ToString(CultureInfo.InvariantCulture)]?["name"] as WzImageProperty);
                return string.IsNullOrWhiteSpace(mobName)
                    ? $"Mob {mobId}"
                    : mobName.Trim();
            }
            catch
            {
                return $"Mob {mobId}";
            }
        }

        private static string ReadWzString(WzImageProperty property)
        {
            if (property == null)
            {
                return null;
            }

            try
            {
                return InfoTool.GetString(property);
            }
            catch
            {
                return property.GetString();
            }
        }
    }
}
