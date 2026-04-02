using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.UI;
using HaCreator.Wz;
using HaSharedLibrary.Wz;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
        private const int PacketOwnedBattleshipCooldownSentinel = 0x004FAE6F;
        private const int PacketOwnedBattleshipSkillId = 5221006;
        private const int PacketOwnedBattleshipMountItemId = 1932000;
        private const int PacketOwnedApspPromptStringPoolId = 0x17BA;
        private const int PacketOwnedRadioStartStringPoolId = 0x14CF;
        private const int PacketOwnedRadioCompleteStringPoolId = 0x14D0;
        private const int PacketOwnedRadioTrackTemplateStringPoolId = 0x1501;
        private const int PacketOwnedRadioAudioTemplateStringPoolId = 0x1502;
        private const int PacketOwnedApspFollowUpOpcode = 195;
        private const int PacketOwnedApspFollowUpResponseCode = 6;
        private const int PacketOwnedApspMinEventType = 11;
        private const int PacketOwnedApspMaxEventType = 13;
        private const int PacketOwnedLegacyVengeanceSkillId = 3120010;
        private const int PacketOwnedCurrentVengeanceSkillId = 31101003;
        private const string PacketOwnedApspPromptPrimaryLabel = "OK";
        private const string PacketOwnedApspPromptSecondaryLabel = "Cancel";
        private const int PacketOwnedTutorBalloonHorizontalPadding = 10;
        private const int PacketOwnedTutorBalloonVerticalPadding = 10;
        private const int PacketOwnedTutorBalloonBodyExtraWidth = PacketOwnedTutorBalloonHorizontalPadding * 2;
        private const int PacketOwnedTutorBalloonScreenMargin = 6;
        private const int PacketOwnedTutorBalloonArrowOverlap = 6;
        private const int PacketOwnedTutorBalloonAnchorOffsetY = 8;
        private const string PacketOwnedApspPromptExactBody =
            "Congratulations! You have reached Lv.30/50/70 during the event period and have been selected as a winner of an AP/SP reset item!\r\n"
            + "Click the 'OK' button and the AP/SP reset item will be sent to your character's cash locker.\r\n"
            + "If you wish to receive it on another character, click'Cancel' and re-login with the character of your choice.";

        private sealed class PacketOwnedBattleshipDurabilityOverrideState
        {
            public CharacterPart MountPart { get; init; }
            public int? OriginalDurability { get; init; }
            public int? OriginalMaxDurability { get; init; }
        }

        private sealed class PacketOwnedRadioTrackResolution
        {
            public WzBinaryProperty AudioProperty { get; init; }
            public string ResolvedTrackDescriptor { get; init; }
            public string ResolvedAudioDescriptor { get; init; }
            public string DisplayName { get; init; }
        }

        private readonly Dictionary<int, HashSet<int>> _packetQuestGuideTargetsByMobId = new();
        private readonly LocalFollowCharacterRuntime _localFollowRuntime = new();
        private readonly TutorRuntime _packetOwnedTutorRuntime = new();
        private readonly LocalUtilityPacketInboxManager _localUtilityPacketInbox = new();
        private readonly LocalUtilityOfficialSessionBridgeManager _localUtilityOfficialSessionBridge = new();
        private LocalOverlayBalloonSkin _packetOwnedTutorBalloonSkin;
        private readonly Dictionary<int, List<IDXObject>> _packetOwnedTutorCueFramesByIndex = new();
        private PacketOwnedBattleshipDurabilityOverrideState _packetOwnedBattleshipDurabilityOverride;
        private int _packetQuestGuideQuestId;
        private int _packetOwnedUtilityRequestTick = int.MinValue;
        private int _lastDeliveryQuestId;
        private int _lastDeliveryItemId;
        private readonly List<int> _lastDeliveryDisallowedQuestIds = new();
        private int _lastQuestDemandItemQueryQuestId;
        private readonly List<int> _lastQuestDemandQueryVisibleItemIds = new();
        private int _lastQuestDemandQueryHiddenItemCount;
        private int _lastClassCompetitionOpenTick = int.MinValue;
        private int _lastClassCompetitionAuthRequestTick = int.MinValue;
        private int _lastClassCompetitionAuthIssuedTick = int.MinValue;
        private int _lastClassCompetitionNavigateTick = int.MinValue;
        private bool _lastClassCompetitionAuthPending = true;
        private bool _lastClassCompetitionLoggedIn;
        private string _lastClassCompetitionAuthKey = string.Empty;
        private string _lastClassCompetitionUrl = string.Empty;
        private int _lastPacketOwnedOpenUiType = -1;
        private int _lastPacketOwnedOpenUiOption = -1;
        private int _lastPacketOwnedCommoditySerialNumber;
        private int _lastPacketOwnedCommodityRequestTick = int.MinValue;
        private string _lastPacketOwnedNoticeMessage;
        private string _lastPacketOwnedChatMessage;
        private string _lastPacketOwnedBuffzoneMessage;
        private string _lastPacketOwnedAskApspMessage;
        private string _lastPacketOwnedSkillGuideMessage;
        private string _lastPacketOwnedFollowFailureMessage;
        private int? _lastPacketOwnedFollowFailureReason;
        private int _lastPacketOwnedFollowFailureDriverId;
        private bool _lastPacketOwnedFollowFailureClearedPending;
        private int _lastPacketOwnedDirectionModeTick = int.MinValue;
        private bool _lastPacketOwnedDirectionModeEnabled;
        private int _lastPacketOwnedDirectionModeDelayMs;
        private int _lastPacketOwnedStandAloneTick = int.MinValue;
        private bool _lastPacketOwnedStandAloneEnabled;
        private int _lastPacketOwnedSkillGuideGrade;
        private bool _packetOwnedApspPromptActive;
        private int _packetOwnedApspPromptContextToken;
        private int _packetOwnedApspPromptEventType;
        private readonly PacketOwnedLocalUtilityContextState _packetOwnedLocalUtilityContext = new();
        private int _lastPacketOwnedApspFollowUpContextToken;
        private int _lastPacketOwnedApspFollowUpResponseCode;
        private string _lastPacketOwnedEventSoundDescriptor;
        private string _lastPacketOwnedMinigameSoundDescriptor;
        private MonoGameBgmPlayer _packetOwnedRadioAudio;
        private string _lastPacketOwnedRadioTrackDescriptor;
        private string _lastPacketOwnedRadioResolvedTrackDescriptor;
        private string _lastPacketOwnedRadioResolvedDescriptor;
        private string _lastPacketOwnedRadioDisplayName;
        private string _lastPacketOwnedRadioStatusMessage = "Packet-owned radio idle.";
        private int _lastPacketOwnedRadioTimeValue;
        private int _lastPacketOwnedRadioStartOffsetMs;
        private int _lastPacketOwnedRadioAvailableDurationMs;
        private int _lastPacketOwnedRadioStartTick = int.MinValue;
        private int _lastPacketOwnedRadioLastPollTick = int.MinValue;
        private bool _localUtilityPacketInboxEnabled = EnablePacketConnectionsByDefault;
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
            MapSimulatorWindowNames.WeddingWishList,
            MapSimulatorWindowNames.MapleTv,
            MapSimulatorWindowNames.MemoMailbox,
            MapSimulatorWindowNames.MemoSend,
            MapSimulatorWindowNames.MemoGet,
            MapSimulatorWindowNames.QuestAlarm,
            MapSimulatorWindowNames.QuestDelivery,
            MapSimulatorWindowNames.QuestRewardRaise,
            MapSimulatorWindowNames.ClassCompetition,
            MapSimulatorWindowNames.NpcShop,
            MapSimulatorWindowNames.StoreBank,
            MapSimulatorWindowNames.BattleRecord,
            MapSimulatorWindowNames.MiniRoom,
            MapSimulatorWindowNames.PersonalShop,
            MapSimulatorWindowNames.EntrustedShop,
            MapSimulatorWindowNames.TradingRoom,
            MapSimulatorWindowNames.CashTradingRoom,
        };

        private void StampPacketOwnedUtilityRequestState()
        {
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
            StampPacketOwnedUtilityRequestState();
            _lastClassCompetitionOpenTick = Environment.TickCount;
            RefreshClassCompetitionRuntimeState(forceAuthRequest: true);

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ClassCompetition) is not UIWindowBase window)
            {
                const string unavailable = "Class Competition page owner is not available in this UI build.";
                ShowUtilityFeedbackMessage(unavailable);
                return unavailable;
            }

            ShowWindow(MapSimulatorWindowNames.ClassCompetition, window, trackDirectionModeOwner: true);
            return _lastClassCompetitionAuthPending
                ? "Opened packet-authored Class Competition page and seeded a local auth request."
                : "Opened packet-authored Class Competition page.";
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
                MapSimulatorWindowNames.WeddingWishList => "Wedding Wish List",
                MapSimulatorWindowNames.MapleTv => "MapleTV",
                MapSimulatorWindowNames.MemoMailbox => "Parcel Delivery",
                MapSimulatorWindowNames.MemoSend => "Parcel Send Info",
                MapSimulatorWindowNames.MemoGet => "Parcel Package",
                MapSimulatorWindowNames.QuestDelivery => "Quest Delivery",
                MapSimulatorWindowNames.ClassCompetition => "Class Competition",
                MapSimulatorWindowNames.NpcShop => "NPC Shop",
                MapSimulatorWindowNames.StoreBank => "Store Bank",
                MapSimulatorWindowNames.BattleRecord => "Battle Record",
                MapSimulatorWindowNames.MiniRoom => "Mini Room",
                MapSimulatorWindowNames.PersonalShop => "Personal Shop",
                MapSimulatorWindowNames.EntrustedShop => "Entrusted Shop",
                MapSimulatorWindowNames.TradingRoom => "Trading Room",
                MapSimulatorWindowNames.CashTradingRoom => "Cash Trading Room",
                _ => windowName
            };
        }

        private IReadOnlyList<QuestDeliveryWindow.DeliveryEntry> BuildQuestDeliveryEntries(int requestedQuestId, int itemId, IReadOnlyList<int> disallowedQuestIds)
        {
            var entries = new List<QuestDeliveryWindow.DeliveryEntry>();
            var appendedQuestIds = new HashSet<int>();
            var candidateQuestIds = new HashSet<int>();
            var blockedQuestIds = new HashSet<int>(disallowedQuestIds?.Where(id => id > 0) ?? Array.Empty<int>());
            IReadOnlyList<QuestDeliveryEntrySnapshot> deliverySnapshot = _questRuntime.BuildQuestDeliverySnapshot(
                requestedQuestId,
                itemId,
                disallowedQuestIds,
                _playerManager?.Player?.Build);

            for (int i = 0; i < deliverySnapshot.Count; i++)
            {
                QuestDeliveryEntrySnapshot snapshot = deliverySnapshot[i];
                if (snapshot == null || !appendedQuestIds.Add(snapshot.QuestId))
                {
                    continue;
                }

                entries.Add(new QuestDeliveryWindow.DeliveryEntry
                {
                    QuestId = snapshot.QuestId,
                    DisplayQuestId = snapshot.DisplayQuestId,
                    TargetNpcId = snapshot.TargetNpcId,
                    Title = snapshot.Title,
                    NpcName = snapshot.NpcName,
                    StatusText = snapshot.StatusText,
                    DetailText = snapshot.DetailText,
                    DeliveryCashItemName = snapshot.DeliveryCashItemName,
                    CompletionPhase = snapshot.CompletionPhase,
                    CanConfirm = snapshot.CanConfirm,
                    IsBlocked = snapshot.IsBlocked,
                    IsSeriesRepresentative = snapshot.IsSeriesRepresentative
                });
            }

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
                if (appendedQuestIds.Contains(questId))
                {
                    continue;
                }

                QuestWindowDetailState state = GetQuestWindowDetailStateWithPacketState(questId);
                bool blockedByPacket = blockedQuestIds.Contains(questId);
                bool matchingItem = state?.TargetItemId == itemId && itemId > 0;
                bool isDeliveryAction = state?.DeliveryType is QuestDetailDeliveryType.Accept or QuestDetailDeliveryType.Complete;
                if (!blockedByPacket && (!matchingItem || !isDeliveryAction))
                {
                    continue;
                }

                bool completionPhase = state?.DeliveryType == QuestDetailDeliveryType.Complete;
                bool canConfirm = state?.DeliveryActionEnabled == true && !blockedByPacket;
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
                    DisplayQuestId = questId,
                    TargetNpcId = state?.TargetNpcId ?? 0,
                    Title = state?.Title ?? $"Quest #{questId}",
                    NpcName = npcName,
                    StatusText = statusText,
                    DetailText = detailText,
                    DeliveryCashItemName = state?.DeliveryCashItemName ?? string.Empty,
                    CompletionPhase = completionPhase,
                    CanConfirm = canConfirm,
                    IsBlocked = blockedByPacket,
                    IsSeriesRepresentative = false
                });
            }

            return entries
                .OrderByDescending(entry => entry.QuestId == requestedQuestId)
                .ThenByDescending(entry => entry.CanConfirm)
                .ThenBy(entry => entry.CompletionPhase)
                .ThenBy(entry => entry.DisplayQuestId > 0 ? entry.DisplayQuestId : entry.QuestId)
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
                _chat?.AddErrorMessage(error, currTickCount);
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
            RefreshClassCompetitionRuntimeState();
            var lines = new List<string>();
            var build = _playerManager?.Player?.Build;
            lines.Add("Packet-authored web owner mirroring CUserLocal::OnOpenClassCompetitionPage and CClassCompetition.");
            lines.Add("Constructor shape: CWebWnd, 312x389 owner bounds, close/OK dismissal only, and a loading layer while auth is pending.");

            string authState = _lastClassCompetitionAuthIssuedTick == int.MinValue
                ? "No class-competition auth key has been seeded yet."
                : _lastClassCompetitionAuthPending
                    ? $"Auth key seeded at {_lastClassCompetitionAuthIssuedTick}, loading page is still pending."
                    : _lastClassCompetitionLoggedIn
                        ? $"Auth key seeded at {_lastClassCompetitionAuthIssuedTick}, navigation completed at {_lastClassCompetitionNavigateTick}."
                        : $"Auth key seeded at {_lastClassCompetitionAuthIssuedTick}, navigation has not completed yet.";
            lines.Add(authState);

            if (!string.IsNullOrWhiteSpace(_lastClassCompetitionUrl))
            {
                lines.Add($"Local web seed: {_lastClassCompetitionUrl}");
            }

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

            lines.Add("This owner still has no live server-fed ladder payload, so standings remain seeded from the active local build instead of a remote page response.");

            if (_lastClassCompetitionOpenTick != int.MinValue)
            {
                lines.Add($"Last packet launch tick: {_lastClassCompetitionOpenTick.ToString(CultureInfo.InvariantCulture)}");
            }

            return lines;
        }

        private string BuildClassCompetitionFooter()
        {
            RefreshClassCompetitionRuntimeState();
            if (_packetOwnedUtilityRequestTick == int.MinValue && _lastClassCompetitionAuthRequestTick == int.MinValue)
            {
                return "Utility request timing idle.";
            }

            string requestStamp = _packetOwnedUtilityRequestTick == int.MinValue
                ? "Shared request stamp: idle"
                : $"Shared request stamp: {_packetOwnedUtilityRequestTick} ({Math.Max(0, unchecked(currTickCount - _packetOwnedUtilityRequestTick))}ms ago)";
            string authStamp = _lastClassCompetitionAuthRequestTick == int.MinValue
                ? "auth request idle"
                : $"auth request: {_lastClassCompetitionAuthRequestTick} ({Math.Max(0, unchecked(currTickCount - _lastClassCompetitionAuthRequestTick))}ms ago)";
            return $"{requestStamp}, {authStamp}";
        }

        private void RefreshClassCompetitionRuntimeState(bool forceAuthRequest = false)
        {
            if (_lastClassCompetitionOpenTick == int.MinValue && !forceAuthRequest)
            {
                return;
            }

            int now = Environment.TickCount;
            bool authExpired = _lastClassCompetitionAuthIssuedTick == int.MinValue
                || Math.Max(0, unchecked(now - _lastClassCompetitionAuthIssuedTick)) >= 300000;
            bool shouldRequestAuth = forceAuthRequest
                || _lastClassCompetitionAuthRequestTick == int.MinValue
                || Math.Max(0, unchecked(now - _lastClassCompetitionAuthRequestTick)) >= 180000
                || authExpired;

            if (shouldRequestAuth)
            {
                _lastClassCompetitionAuthRequestTick = now;
                _lastClassCompetitionAuthIssuedTick = now;
                _lastClassCompetitionAuthPending = true;
                _lastClassCompetitionLoggedIn = false;
                _lastClassCompetitionAuthKey = BuildClassCompetitionAuthKey(now);
                _lastClassCompetitionUrl = BuildClassCompetitionUrl(_lastClassCompetitionAuthKey);
            }

            if (_lastClassCompetitionAuthPending
                && _lastClassCompetitionOpenTick != int.MinValue
                && Math.Max(0, unchecked(now - _lastClassCompetitionOpenTick)) >= 250)
            {
                _lastClassCompetitionAuthPending = false;
            }

            if (!_lastClassCompetitionAuthPending
                && !_lastClassCompetitionLoggedIn
                && !string.IsNullOrWhiteSpace(_lastClassCompetitionUrl))
            {
                _lastClassCompetitionLoggedIn = true;
                _lastClassCompetitionNavigateTick = now;
            }
        }

        private string BuildClassCompetitionAuthKey(int issuedAtTick)
        {
            int buildId = _playerManager?.Player?.Build?.Id ?? 0;
            int mapId = _mapBoard?.MapInfo?.id ?? 0;
            return $"msim-{buildId:x8}-{mapId:x8}-{issuedAtTick:x8}";
        }

        private string BuildClassCompetitionUrl(string authKey)
        {
            int worldId = Math.Max(0, _simulatorWorldId) + 1;
            int characterId = _playerManager?.Player?.Build?.Id ?? 0;
            return $"classcompetition://world/{worldId}/character/{characterId}?auth={authKey}";
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
                _chat?.AddErrorMessage($"Local utility packet inbox failed to start: {ex.Message}", currTickCount);
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
                    if (applied)
                    {
                        _chat?.AddSystemMessage(detail, currTickCount);
                    }
                    else
                    {
                        _chat?.AddErrorMessage(detail, currTickCount);
                    }
                }
            }
        }

        private void DrainLocalUtilityOfficialSessionBridge()
        {
            while (_localUtilityOfficialSessionBridge.TryDequeue(out LocalUtilityPacketInboxMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                bool applied = TryApplyPacketOwnedUtilityPacket(message.PacketType, message.Payload, out string detail);
                _localUtilityOfficialSessionBridge.RecordDispatchResult(message.Source, applied, detail);
                if (!string.IsNullOrWhiteSpace(detail))
                {
                    if (applied)
                    {
                        _chat?.AddSystemMessage(detail, currTickCount);
                    }
                    else
                    {
                        _chat?.AddErrorMessage(detail, currTickCount);
                    }
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

        private string DescribeLocalUtilityOfficialSessionBridgeStatus()
        {
            return _localUtilityOfficialSessionBridge.DescribeStatus();
        }

        private bool TryApplyPacketOwnedUtilityPacket(int packetType, byte[] payload, out string message)
        {
            message = null;
            switch (packetType)
            {
                case MapleTvRuntime.PacketTypeSetMessage:
                case MapleTvRuntime.PacketTypeClearMessage:
                case MapleTvRuntime.PacketTypeSendMessageResult:
                    return TryApplyMapleTvPacket(packetType, payload, out message);

                case LocalUtilityPacketInboxManager.OpenUiPacketType:
                case LocalUtilityPacketInboxManager.OpenUiClientPacketType:
                    return TryApplyPacketOwnedOpenUiPayload(payload, out message);

                case LocalUtilityPacketInboxManager.OpenUiWithOptionPacketType:
                case LocalUtilityPacketInboxManager.OpenUiWithOptionClientPacketType:
                    return TryApplyPacketOwnedOpenUiWithOptionPayload(payload, out message);

                case LocalUtilityPacketInboxManager.HireTutorClientPacketType:
                    return TryApplyPacketOwnedTutorHirePayload(payload, out message);

                case LocalUtilityPacketInboxManager.TutorMsgClientPacketType:
                    return TryApplyPacketOwnedTutorMessagePayload(payload, out message);

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

                case LocalUtilityPacketInboxManager.RadioSchedulePacketType:
                case LocalUtilityPacketInboxManager.RadioScheduleClientPacketType:
                    return TryApplyPacketOwnedRadioSchedulePayload(payload, out message);

                case PacketOwnedAntiMacroPacketType:
                    return TryApplyPacketOwnedAntiMacroPayload(payload, out message);

                case LocalUtilityPacketInboxManager.OpenSkillGuideClientPacketType:
                    message = ApplyPacketOwnedSkillGuideLaunch();
                    return true;

                case LocalUtilityPacketInboxManager.AskApspEventPacketType:
                case LocalUtilityPacketInboxManager.AskApspEventClientPacketType:
                    return TryApplyPacketOwnedAskApspEventPayload(payload, out message);

                case LocalUtilityPacketInboxManager.FollowCharacterPacketType:
                case LocalUtilityPacketInboxManager.FollowCharacterClientPacketType:
                    return TryApplyPacketOwnedFollowCharacterPayload(payload, out message);

                case LocalUtilityPacketInboxManager.SitResultPacketType:
                    return TryApplyPacketOwnedChairSitResultPayload(payload, out message);

                case LocalUtilityPacketInboxManager.QuestResultPacketType:
                    return TryApplyPacketOwnedQuestResultPayload(payload, out message);

                case LocalUtilityPacketInboxManager.SetDirectionModePacketType:
                    return TryApplyPacketOwnedDirectionModePayload(payload, out message);

                case LocalUtilityPacketInboxManager.SetStandAloneModePacketType:
                    return TryApplyPacketOwnedStandAloneModePayload(payload, out message);

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

                case LocalUtilityPacketInboxManager.TimeBombAttackPacketType:
                    return TryApplyPacketOwnedTimeBombAttackPayload(payload, out message);

                case LocalUtilityPacketInboxManager.VengeanceSkillApplyPacketType:
                    return TryApplyPacketOwnedVengeanceSkillApplyPayload(payload, out message);

                case LocalUtilityPacketInboxManager.ExJablinApplyPacketType:
                    return TryApplyPacketOwnedExJablinApplyPayload(payload, out message);

                case LocalUtilityPacketInboxManager.QuestGuideResultPacketType:
                    return TryApplyPacketOwnedQuestGuidePayload(payload, out message);

                case LocalUtilityPacketInboxManager.DeliveryQuestPacketType:
                    return TryApplyPacketOwnedDeliveryQuestPayload(payload, out message);

                case LocalUtilityPacketInboxManager.SkillCooltimeSetPacketType:
                    return TryApplyPacketOwnedSkillCooltimePayload(payload, out message);

                case LocalUtilityPacketInboxManager.FuncKeyMapInitPacketType:
                    return TryApplyPacketOwnedFuncKeyInitPayload(payload, out message);

                case LocalUtilityPacketInboxManager.PetConsumeItemInitPacketType:
                    return TryApplyPacketOwnedPetConsumeItemInitPayload(payload, mpItem: false, out message);

                case LocalUtilityPacketInboxManager.PetConsumeMpItemInitPacketType:
                    return TryApplyPacketOwnedPetConsumeItemInitPayload(payload, mpItem: true, out message);

                case 364:
                case 365:
                case 369:
                case 370:
                case 420:
                case 421:
                case 422:
                case 423:
                    return TryApplyPacketOwnedNpcUtilityPacket(packetType, payload, out message);

                default:
                    message = $"Unsupported local utility packet type {packetType}.";
                    return false;
            }
        }

        private bool TryApplyPacketOwnedChairSitResultPayload(byte[] payload, out string message)
        {
            message = null;
            PlayerCharacter player = _playerManager?.Player;
            if (player?.Build == null)
            {
                message = "Sit-result payload could not be applied because the local player is not initialized.";
                return false;
            }

            if (payload == null || payload.Length < 1)
            {
                message = "Sit-result payload must contain at least the success flag.";
                return false;
            }

            StampPacketOwnedUtilityRequestState();
            int currentTick = Environment.TickCount;
            _packetOwnedLocalUtilityContext.ObserveChairSitResult(currentTick);

            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: false);

            bool succeeded = reader.ReadByte() != 0;
            if (!succeeded)
            {
                player.ApplyPacketOwnedChairStandCorrection();
                message = "Packet-owned sit result rejected the chair request and forced a stand-up correction.";
                return true;
            }

            if (stream.Length - stream.Position < sizeof(ushort))
            {
                player.ApplyPacketOwnedChairStandCorrection();
                message = "Sit-result payload is missing the seat index.";
                return false;
            }

            int seatIndex = reader.ReadUInt16();
            if (!TryResolvePacketOwnedChairSeatPosition(_mapBoard?.MapInfo?.id ?? 0, seatIndex, out Vector2 seatPosition))
            {
                player.ApplyPacketOwnedChairStandCorrection();
                message = BuildPacketOwnedChairCorrectionMessage(
                    player,
                    seatIndex,
                    "the current field does not expose that seat index",
                    currentTick);
                return true;
            }

            if (!IsPacketOwnedChairSeatValidForPlayer(player, seatPosition))
            {
                player.ApplyPacketOwnedChairStandCorrection();
                message = BuildPacketOwnedChairCorrectionMessage(
                    player,
                    seatIndex,
                    "the local player is outside the client chair rectangle for that seat",
                    currentTick);
                return true;
            }

            string detachedPassengerMessage = ClearPacketOwnedChairPassengerLink(player);
            player.SetPortableChairPairRequestActive(false);
            player.ClearPortableChairExternalOwnerPair();
            player.ApplyPacketOwnedSitPlacement(seatPosition.X, seatPosition.Y);
            message = string.IsNullOrWhiteSpace(detachedPassengerMessage)
                ? $"Applied packet-owned sit result for seat {seatIndex} at ({seatPosition.X:0},{seatPosition.Y:0})."
                : $"Applied packet-owned sit result for seat {seatIndex} at ({seatPosition.X:0},{seatPosition.Y:0}). {detachedPassengerMessage}";
            return true;
        }

        private string BuildPacketOwnedChairCorrectionMessage(PlayerCharacter player, int seatIndex, string reason, int currentTick)
        {
            if (_packetOwnedLocalUtilityContext.TryEmitChairGetUpRequest(currentTick, player?.HP ?? 0, timeIntervalMs: 0, out PacketOwnedLocalUtilityOutboundRequest request))
            {
                return $"Packet-owned sit result returned seat {seatIndex}, but {reason}. Forced a stand-up correction and emitted GetUpFromChairRequest(0) as opcode {request.Opcode} [{BitConverter.ToString(request.Payload.ToArray()).Replace("-", string.Empty)}].";
            }

            return $"Packet-owned sit result returned seat {seatIndex}, but {reason}. Forced a stand-up correction, but the simulated CWvsContext gate suppressed GetUpFromChairRequest(0).";
        }

        private string ClearPacketOwnedChairPassengerLink(PlayerCharacter player)
        {
            if (player?.Build == null)
            {
                return null;
            }

            int passengerId = _localFollowRuntime.AttachedPassengerId;
            if (passengerId <= 0)
            {
                return null;
            }

            TryResolvePacketOwnedRemoteCharacterSnapshot(passengerId, out LocalFollowUserSnapshot passenger);
            string detachMessage = _localFollowRuntime.ClearAttachedPassenger(
                passenger.Exists
                    ? passenger
                    : LocalFollowUserSnapshot.Missing(passengerId, ResolvePacketOwnedRemoteCharacterName(passengerId)),
                transferField: false,
                transferPosition: null);
            _remoteUserPool?.TryApplyFollowCharacter(
                passengerId,
                driverId: 0,
                transferField: false,
                transferPosition: null,
                localCharacterId: player.Build.Id,
                localCharacterPosition: new Vector2(player.X, player.Y),
                out _);
            return detachMessage;
        }

        private static bool IsPacketOwnedChairSeatValidForPlayer(PlayerCharacter player, Vector2 seatPosition)
        {
            if (player == null)
            {
                return false;
            }

            return player.X >= seatPosition.X - 10f
                && player.X <= seatPosition.X + 10f
                && player.Y >= seatPosition.Y - 30f
                && player.Y <= seatPosition.Y + 30f;
        }

        private static bool TryResolvePacketOwnedChairSeatPosition(int mapId, int seatIndex, out Vector2 seatPosition)
        {
            seatPosition = Vector2.Zero;
            if (mapId <= 0 || seatIndex < 0)
            {
                return false;
            }

            WzImage mapImage = TryGetMapImageForMetadataLookup(mapId);
            if (mapImage == null)
            {
                return false;
            }

            bool shouldUnparse = !mapImage.Parsed;
            try
            {
                if (!mapImage.Parsed)
                {
                    mapImage.ParseImage();
                }

                if (mapImage["seat"] is not WzSubProperty seatParent
                    || seatParent[seatIndex.ToString(CultureInfo.InvariantCulture)] is not WzVectorProperty seatVector)
                {
                    return false;
                }

                seatPosition = new Vector2(seatVector.X.Value, seatVector.Y.Value);
                return true;
            }
            finally
            {
                if (shouldUnparse)
                {
                    mapImage.UnparseImage();
                }
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

        private bool TryApplyPacketOwnedDirectionModePayload(byte[] payload, out string message)
        {
            message = "Direction-mode payload is missing.";
            if (payload == null || payload.Length == 0)
            {
                return false;
            }

            if (payload.Length != 1 && payload.Length < 5)
            {
                message = "Direction-mode payload must contain an enable byte and an optional 32-bit delay.";
                return false;
            }

            try
            {
                using var stream = new MemoryStream(payload, writable: false);
                using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: false);
                bool enabled = reader.ReadByte() != 0;
                int delayMs = reader.BaseStream.Position <= reader.BaseStream.Length - sizeof(int)
                    ? reader.ReadInt32()
                    : 0;
                message = ApplyPacketOwnedDirectionMode(enabled, delayMs);
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                message = $"Direction-mode payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private bool TryApplyPacketOwnedStandAloneModePayload(byte[] payload, out string message)
        {
            message = "Stand-alone payload is missing.";
            if (payload == null || payload.Length < 1)
            {
                return false;
            }

            try
            {
                bool enabled = payload[0] != 0;
                message = ApplyPacketOwnedStandAloneMode(enabled);
                return true;
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException)
            {
                message = $"Stand-alone payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private string ApplyPacketOwnedDirectionMode(bool enabled, int delayMs)
        {
            StampPacketOwnedUtilityRequestState();
            int currentTickCount = Environment.TickCount;
            _lastPacketOwnedDirectionModeTick = currentTickCount;
            _lastPacketOwnedDirectionModeEnabled = enabled;
            _lastPacketOwnedDirectionModeDelayMs = Math.Max(0, delayMs);
            _gameState.SetPacketDirectionMode(enabled, currentTickCount, delayMs);

            string message = enabled
                ? "Applied packet-authored direction mode immediately."
                : delayMs > 0
                    ? $"Queued packet-authored direction-mode release in {Math.Max(0, delayMs)} ms."
                    : "Cleared packet-authored direction mode immediately.";
            ShowUtilityFeedbackMessage(message);
            return message;
        }

        private string ApplyPacketOwnedStandAloneMode(bool enabled)
        {
            StampPacketOwnedUtilityRequestState();
            _lastPacketOwnedStandAloneTick = Environment.TickCount;
            _lastPacketOwnedStandAloneEnabled = enabled;
            _gameState.SetStandAloneMode(enabled);
            string message = enabled
                ? "Enabled the packet-authored stand-alone control flag."
                : "Cleared the packet-authored stand-alone control flag.";
            ShowUtilityFeedbackMessage(message);
            return message;
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
            string skillGuideStatus = string.IsNullOrWhiteSpace(_lastPacketOwnedSkillGuideMessage)
                ? "Skill guide: none."
                : _lastPacketOwnedSkillGuideGrade > 0
                    ? $"Skill guide: grade {_lastPacketOwnedSkillGuideGrade}. {TruncatePacketOwnedUtilityText(_lastPacketOwnedSkillGuideMessage)}"
                    : $"Skill guide: {TruncatePacketOwnedUtilityText(_lastPacketOwnedSkillGuideMessage)}";
            string apspContextStatus = DescribePacketOwnedApspContextStatus();
            string apspStatus = string.IsNullOrWhiteSpace(_lastPacketOwnedAskApspMessage)
                ? "AP/SP event: none."
                : _packetOwnedApspPromptActive
                    ? $"AP/SP event: prompt active for context {_packetOwnedApspPromptContextToken}, event {_packetOwnedApspPromptEventType}. {TruncatePacketOwnedUtilityText(_lastPacketOwnedAskApspMessage)}"
                    : _lastPacketOwnedApspFollowUpContextToken > 0
                        ? $"AP/SP event: last follow-up {PacketOwnedApspFollowUpOpcode} ({_lastPacketOwnedApspFollowUpContextToken}, {_lastPacketOwnedApspFollowUpResponseCode}). {TruncatePacketOwnedUtilityText(_lastPacketOwnedAskApspMessage)}"
                        : $"AP/SP event: {TruncatePacketOwnedUtilityText(_lastPacketOwnedAskApspMessage)}";
            string tutorStatus = DescribePacketOwnedTutorStatus(currentTickCount);
            string localFollowStatus = TruncatePacketOwnedUtilityText(_localFollowRuntime.DescribeStatus(ResolvePacketOwnedRemoteCharacterName), 220);
            string followStatus = string.IsNullOrWhiteSpace(_lastPacketOwnedFollowFailureMessage)
                ? localFollowStatus
                : _lastPacketOwnedFollowFailureReason.HasValue
                    ? $"{localFollowStatus} Follow failure: reason {_lastPacketOwnedFollowFailureReason.Value}, driver {_lastPacketOwnedFollowFailureDriverId}, cleared={_lastPacketOwnedFollowFailureClearedPending}. {TruncatePacketOwnedUtilityText(_lastPacketOwnedFollowFailureMessage)}"
                    : $"{localFollowStatus} Follow failure: {TruncatePacketOwnedUtilityText(_lastPacketOwnedFollowFailureMessage)}";
            string localControlStatus = DescribePacketOwnedLocalControlStatus(currentTickCount);
            string soundStatus = string.IsNullOrWhiteSpace(_lastPacketOwnedEventSoundDescriptor) && string.IsNullOrWhiteSpace(_lastPacketOwnedMinigameSoundDescriptor)
                ? "Sound cues: none."
                : $"Sound cues: event={(_lastPacketOwnedEventSoundDescriptor ?? "none")}, minigame={(_lastPacketOwnedMinigameSoundDescriptor ?? "none")}.";
            string radioStatus = IsPacketOwnedRadioPlaying()
                ? $"Radio: \"{TruncatePacketOwnedUtilityText(_lastPacketOwnedRadioDisplayName ?? _lastPacketOwnedRadioTrackDescriptor)}\", authoredTime={_lastPacketOwnedRadioTimeValue}s, position={(_lastPacketOwnedRadioStartOffsetMs + Math.Max(0, unchecked(currentTickCount - _lastPacketOwnedRadioStartTick)))} ms, remaining={Math.Max(0, _lastPacketOwnedRadioAvailableDurationMs - Math.Max(0, unchecked(currentTickCount - _lastPacketOwnedRadioStartTick)))} ms."
                : $"Radio: {TruncatePacketOwnedUtilityText(_lastPacketOwnedRadioStatusMessage)}";
            string antiMacroStatus = $"Anti-macro: {TruncatePacketOwnedUtilityText(DescribePacketOwnedAntiMacroStatus(currentTickCount), 140)}";

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
                skillGuideStatus,
                apspContextStatus,
                apspStatus,
                tutorStatus,
                followStatus,
                localControlStatus,
                soundStatus,
                radioStatus,
                antiMacroStatus
            });
        }

        private string DescribePacketOwnedLocalControlStatus(int currentTickCount)
        {
            string directionStatus = _lastPacketOwnedDirectionModeTick == int.MinValue
                ? $"Local control: packet direction mode inactive, scripted direction mode={_gameState.ScriptedDirectionModeActive.ToString().ToLowerInvariant()}, packet direction mode={_gameState.PacketDirectionModeActive.ToString().ToLowerInvariant()}."
                : _gameState.PacketDirectionModeReleaseAt != int.MinValue
                    ? $"Local control: packet direction request enabled={_lastPacketOwnedDirectionModeEnabled.ToString().ToLowerInvariant()}, releaseDelay={_lastPacketOwnedDirectionModeDelayMs}, dueIn={Math.Max(0, unchecked(_gameState.PacketDirectionModeReleaseAt - currentTickCount))} ms, scripted direction mode={_gameState.ScriptedDirectionModeActive.ToString().ToLowerInvariant()}, packet direction mode={_gameState.PacketDirectionModeActive.ToString().ToLowerInvariant()}."
                    : $"Local control: packet direction request enabled={_lastPacketOwnedDirectionModeEnabled.ToString().ToLowerInvariant()}, delay={_lastPacketOwnedDirectionModeDelayMs}, scripted direction mode={_gameState.ScriptedDirectionModeActive.ToString().ToLowerInvariant()}, packet direction mode={_gameState.PacketDirectionModeActive.ToString().ToLowerInvariant()}.";
            string standAloneStatus = _lastPacketOwnedStandAloneTick == int.MinValue
                ? $"Stand-alone flag={_gameState.StandAloneModeActive.ToString().ToLowerInvariant()}."
                : $"Stand-alone request enabled={_lastPacketOwnedStandAloneEnabled.ToString().ToLowerInvariant()}, flag={_gameState.StandAloneModeActive.ToString().ToLowerInvariant()}, age={Math.Max(0, unchecked(currentTickCount - _lastPacketOwnedStandAloneTick))} ms.";
            bool chairSecureSit = _playerManager?.Player?.PacketOwnedChairSitConfirmed == true;
            string chairStatus = _packetOwnedLocalUtilityContext.DescribeChairContext(currentTickCount, chairSecureSit);
            string teleportPortalNames = !string.IsNullOrWhiteSpace(_lastPacketOwnedTeleportSourcePortalName)
                || !string.IsNullOrWhiteSpace(_lastPacketOwnedTeleportTargetPortalName)
                    ? $"{_lastPacketOwnedTeleportSourcePortalName ?? "?"}->{_lastPacketOwnedTeleportTargetPortalName ?? "?"}"
                    : "none";
            string teleportStatus = _lastPacketOwnedTeleportPortalRequestTick == int.MinValue
                ? $"Teleport request active={_packetOwnedTeleportRequestActive.ToString().ToLowerInvariant()}, last portal request=none, cooldown={IsPacketOwnedTeleportRegistrationCoolingDown(currentTickCount).ToString().ToLowerInvariant()}."
                : $"Teleport request active={_packetOwnedTeleportRequestActive.ToString().ToLowerInvariant()}, last portal request age={Math.Max(0, unchecked(currentTickCount - _lastPacketOwnedTeleportPortalRequestTick))} ms, handoff={teleportPortalNames}, portalIndex={_lastPacketOwnedTeleportPortalIndex}, cooldown={IsPacketOwnedTeleportRegistrationCoolingDown(currentTickCount).ToString().ToLowerInvariant()}.";
            return $"{directionStatus} {standAloneStatus} {chairStatus} {teleportStatus}";
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

        private static bool TryParsePacketOwnedBooleanToken(string token, out bool value)
        {
            value = false;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            switch (token.Trim().ToLowerInvariant())
            {
                case "1":
                case "on":
                case "true":
                case "enable":
                case "enabled":
                    value = true;
                    return true;

                case "0":
                case "off":
                case "false":
                case "disable":
                case "disabled":
                    value = false;
                    return true;

                default:
                    return false;
            }
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
                ? $"Stored packet-owned commodity SN {_lastPacketOwnedCommoditySerialNumber}, requested shop migration, and focused the matching Cash Shop row. {shopMessage}"
                : $"Stored packet-owned commodity SN {_lastPacketOwnedCommoditySerialNumber} and requested shop migration. {shopMessage}";
            ShowUtilityFeedbackMessage(message);
            return message;
        }

        private bool TryApplyPacketOwnedRadioSchedulePayload(byte[] payload, out string message)
        {
            message = "Radio-schedule payload is missing.";
            if (payload == null || payload.Length == 0)
            {
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
                string trackDescriptor = ReadPacketOwnedMapleString(reader);
                int timeValue = reader.BaseStream.Position <= reader.BaseStream.Length - sizeof(int)
                    ? reader.ReadInt32()
                    : 0;
                message = ApplyPacketOwnedRadioSchedule(trackDescriptor, timeValue);
                return true;
            }
            catch (EndOfStreamException)
            {
            }
            catch (IOException)
            {
            }

            if (TryDecodePacketOwnedStringPayload(payload, out string descriptor))
            {
                message = ApplyPacketOwnedRadioSchedule(descriptor, 0);
                return true;
            }

            message = "Radio-schedule payload could not be decoded.";
            return false;
        }

        private string ApplyPacketOwnedRadioSchedule(string trackDescriptor, int timeValue)
        {
            StampPacketOwnedUtilityRequestState();

            string normalizedTrackDescriptor = trackDescriptor?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedTrackDescriptor))
            {
                const string emptyMessage = "Packet-owned radio schedule did not include a track descriptor.";
                _lastPacketOwnedRadioStatusMessage = emptyMessage;
                ShowUtilityFeedbackMessage(emptyMessage);
                return emptyMessage;
            }

            if (IsPacketOwnedRadioPlaying())
            {
                string ignoreMessage = $"Ignored packet-owned radio schedule for {normalizedTrackDescriptor} because a radio session is already active.";
                _lastPacketOwnedRadioStatusMessage = ignoreMessage;
                ShowUtilityFeedbackMessage(ignoreMessage);
                return ignoreMessage;
            }

            if (!TryResolvePacketOwnedRadioTrack(
                normalizedTrackDescriptor,
                out PacketOwnedRadioTrackResolution trackResolution))
            {
                string missingMessage = $"Packet-owned radio track '{normalizedTrackDescriptor}' could not be resolved in the loaded Sound/*.img data.";
                _lastPacketOwnedRadioStatusMessage = missingMessage;
                ShowUtilityFeedbackMessage(missingMessage);
                return missingMessage;
            }

            try
            {
                _packetOwnedRadioAudio?.Dispose();
                int normalizedTimeValue = Math.Max(0, timeValue);
                int startOffsetMs = normalizedTimeValue * 1000;
                _packetOwnedRadioAudio = new MonoGameBgmPlayer(trackResolution.AudioProperty, looped: false, startOffsetMs);
                _lastPacketOwnedRadioTrackDescriptor = normalizedTrackDescriptor;
                _lastPacketOwnedRadioResolvedTrackDescriptor = trackResolution.ResolvedTrackDescriptor;
                _lastPacketOwnedRadioResolvedDescriptor = trackResolution.ResolvedAudioDescriptor;
                _lastPacketOwnedRadioDisplayName = trackResolution.DisplayName;
                _lastPacketOwnedRadioTimeValue = normalizedTimeValue;
                _lastPacketOwnedRadioStartOffsetMs = startOffsetMs;
                _lastPacketOwnedRadioAvailableDurationMs = (int)Math.Clamp(
                    Math.Round(_packetOwnedRadioAudio.Duration.TotalMilliseconds),
                    0d,
                    int.MaxValue);
                _lastPacketOwnedRadioStartTick = Environment.TickCount;
                _lastPacketOwnedRadioLastPollTick = int.MinValue;
                _lastPacketOwnedRadioStatusMessage =
                    $"Joined packet-owned radio playback for {trackResolution.DisplayName} " +
                    $"at {normalizedTimeValue}s (StringPool 0x{PacketOwnedRadioStartStringPoolId:X}).";

                _packetOwnedRadioAudio.Play();
                ApplyUtilityAudioSettings();
                ShowPacketOwnedRadioWindow();
                _chat?.AddClientChatMessage(
                    FormatPacketOwnedRadioChatMessage(PacketOwnedRadioStartStringPoolId, trackResolution.DisplayName),
                    Environment.TickCount,
                    12);

                string message = $"Started packet-owned radio playback for {trackResolution.DisplayName} ({trackResolution.ResolvedAudioDescriptor}).";
                ShowUtilityFeedbackMessage(message);
                return message;
            }
            catch (Exception ex)
            {
                _packetOwnedRadioAudio?.Dispose();
                _packetOwnedRadioAudio = null;
                string failedMessage = $"Packet-owned radio track '{normalizedTrackDescriptor}' could not start: {ex.Message}";
                _lastPacketOwnedRadioStatusMessage = failedMessage;
                ShowUtilityFeedbackMessage(failedMessage);
                return failedMessage;
            }
        }

        private void UpdatePacketOwnedRadioSchedule(int currentTickCount)
        {
            if (!IsPacketOwnedRadioPlaying())
            {
                return;
            }

            if (_lastPacketOwnedRadioLastPollTick != int.MinValue
                && unchecked(currentTickCount - _lastPacketOwnedRadioLastPollTick) < 2000)
            {
                return;
            }

            _lastPacketOwnedRadioLastPollTick = currentTickCount;
            if (_packetOwnedRadioAudio?.State == Microsoft.Xna.Framework.Audio.SoundState.Stopped)
            {
                StopPacketOwnedRadioSchedule(completed: true, emitChatNotice: true);
            }
        }

        private void UpdatePacketOwnedTutorRuntime(int currentTickCount)
        {
            _packetOwnedTutorRuntime.Update(currentTickCount);
            SyncPacketOwnedTutorSummonState(currentTickCount);
        }

        private bool IsPacketOwnedRadioPlaying()
        {
            return _packetOwnedRadioAudio != null && _lastPacketOwnedRadioStartTick != int.MinValue;
        }

        private string StopPacketOwnedRadioSchedule(bool completed, bool emitChatNotice)
        {
            string displayName = _lastPacketOwnedRadioDisplayName ?? _lastPacketOwnedRadioTrackDescriptor ?? "radio track";
            _packetOwnedRadioAudio?.Stop();
            _packetOwnedRadioAudio?.Dispose();
            _packetOwnedRadioAudio = null;
            _lastPacketOwnedRadioTrackDescriptor = null;
            _lastPacketOwnedRadioResolvedTrackDescriptor = null;
            _lastPacketOwnedRadioResolvedDescriptor = null;
            _lastPacketOwnedRadioDisplayName = null;
            _lastPacketOwnedRadioTimeValue = 0;
            _lastPacketOwnedRadioStartOffsetMs = 0;
            _lastPacketOwnedRadioAvailableDurationMs = 0;
            _lastPacketOwnedRadioStartTick = int.MinValue;
            _lastPacketOwnedRadioLastPollTick = int.MinValue;
            ApplyUtilityAudioSettings();

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.Radio) is UIWindowBase radioWindow && radioWindow.IsVisible)
            {
                uiWindowManager.HideWindow(MapSimulatorWindowNames.Radio);
            }

            _lastPacketOwnedRadioStatusMessage = completed
                ? $"Completed packet-owned radio playback for {displayName} (StringPool 0x{PacketOwnedRadioCompleteStringPoolId:X})."
                : $"Stopped packet-owned radio playback for {displayName}.";

            if (emitChatNotice)
            {
                string chatText = completed
                    ? FormatPacketOwnedRadioChatMessage(PacketOwnedRadioCompleteStringPoolId, displayName)
                    : $"[Radio] Stopped playing {displayName}.";
                _chat?.AddClientChatMessage(chatText, Environment.TickCount, 12);
            }

            return _lastPacketOwnedRadioStatusMessage;
        }

        private void ResetPacketOwnedRadioSchedule(bool emitChatNotice = false)
        {
            if (!IsPacketOwnedRadioPlaying())
            {
                return;
            }

            StopPacketOwnedRadioSchedule(completed: false, emitChatNotice: emitChatNotice);
        }

        private void ShowPacketOwnedRadioWindow()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.Radio) is not UIWindowBase radioWindow)
            {
                return;
            }

            ShowWindow(MapSimulatorWindowNames.Radio, radioWindow, trackDirectionModeOwner: ShouldTrackInheritedDirectionModeOwner());
        }

        private IReadOnlyList<string> BuildPacketOwnedRadioWindowLines()
        {
            List<string> lines = new();
            if (!IsPacketOwnedRadioPlaying())
            {
                lines.Add("Packet-authored radio playback is idle.");
                lines.Add(string.IsNullOrWhiteSpace(_lastPacketOwnedRadioStatusMessage)
                    ? "No packet-owned radio schedule has been applied yet."
                    : _lastPacketOwnedRadioStatusMessage);
                return lines;
            }

            int elapsedMs = Math.Max(0, unchecked(Environment.TickCount - _lastPacketOwnedRadioStartTick));
            int playheadMs = _lastPacketOwnedRadioStartOffsetMs + elapsedMs;
            lines.Add($"Track: {_lastPacketOwnedRadioDisplayName ?? _lastPacketOwnedRadioTrackDescriptor}");
            lines.Add($"Authored descriptor: {_lastPacketOwnedRadioTrackDescriptor}");
            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedRadioResolvedTrackDescriptor))
            {
                lines.Add($"Resolved track node: {_lastPacketOwnedRadioResolvedTrackDescriptor}");
            }

            lines.Add($"Resolved source: {_lastPacketOwnedRadioResolvedDescriptor}");
            lines.Add($"Session elapsed: {elapsedMs / 1000f:0.0}s");
            lines.Add($"Playback position: {playheadMs / 1000f:0.0}s");
            lines.Add(_lastPacketOwnedRadioTimeValue > 0
                ? $"Authored time value: {_lastPacketOwnedRadioTimeValue}s"
                : "Authored time value: 0");
            if (_lastPacketOwnedRadioAvailableDurationMs > 0)
            {
                lines.Add($"Remaining runtime: {Math.Max(0, _lastPacketOwnedRadioAvailableDurationMs - elapsedMs) / 1000f:0.0}s");
            }

            lines.Add(
                $"Client templates: StringPool 0x{PacketOwnedRadioTrackTemplateStringPoolId:X} track UOL, " +
                $"0x{PacketOwnedRadioAudioTemplateStringPoolId:X} audio UOL.");
            lines.Add("Field BGM is temporarily muted while the radio session owns playback.");
            return lines;
        }

        private string BuildPacketOwnedRadioWindowFooter()
        {
            return IsPacketOwnedRadioPlaying()
                ? "Client parity: packet-owned radio session active."
                : "Client parity: waiting for OnRadioSchedule.";
        }

        private string GetPacketOwnedRadioTrackName()
        {
            return _lastPacketOwnedRadioDisplayName ?? _lastPacketOwnedRadioTrackDescriptor;
        }

        private static string FormatPacketOwnedRadioChatMessage(int stringPoolId, string displayName)
        {
            string normalizedDisplayName = string.IsNullOrWhiteSpace(displayName)
                ? "radio track"
                : displayName.Trim();
            return stringPoolId switch
            {
                PacketOwnedRadioStartStringPoolId =>
                    $"[Radio] Joined {normalizedDisplayName}. [StringPool 0x{PacketOwnedRadioStartStringPoolId:X}]",
                PacketOwnedRadioCompleteStringPoolId =>
                    $"[Radio] Finished {normalizedDisplayName}. [StringPool 0x{PacketOwnedRadioCompleteStringPoolId:X}]",
                _ =>
                    $"[Radio] {normalizedDisplayName}. [StringPool 0x{stringPoolId:X}]",
            };
        }

        private static bool TryResolvePacketOwnedRadioTrack(
            string descriptor,
            out PacketOwnedRadioTrackResolution resolution)
        {
            resolution = null;

            if (string.IsNullOrWhiteSpace(descriptor))
            {
                return false;
            }

            string normalizedDescriptor = descriptor.Trim().Replace('\\', '/');
            if (TryResolvePacketOwnedRadioTrackViaClientStyleLookup(normalizedDescriptor, out resolution))
            {
                return true;
            }

            WzBinaryProperty soundProperty = Program.InfoManager.GetBgm(normalizedDescriptor);
            if (soundProperty != null)
            {
                resolution = CreatePacketOwnedRadioTrackResolution(
                    soundProperty,
                    normalizedDescriptor,
                    normalizedDescriptor,
                    ResolvePacketOwnedRadioDisplayName(soundProperty, normalizedDescriptor.Split('/').LastOrDefault() ?? normalizedDescriptor));
                return true;
            }

            if (!normalizedDescriptor.Contains("/", StringComparison.Ordinal))
            {
                string[] bgmPrefixes =
                {
                    "BgmUI",
                    "BgmEvent",
                    "BgmEvent2",
                };

                for (int i = 0; i < bgmPrefixes.Length; i++)
                {
                    string bgmCandidate = $"{bgmPrefixes[i]}/{normalizedDescriptor}";
                    soundProperty = Program.InfoManager.GetBgm(bgmCandidate);
                    if (soundProperty != null)
                    {
                        resolution = CreatePacketOwnedRadioTrackResolution(
                            soundProperty,
                            bgmCandidate,
                            bgmCandidate,
                            ResolvePacketOwnedRadioDisplayName(soundProperty, normalizedDescriptor));
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryResolvePacketOwnedRadioTrackViaClientStyleLookup(
            string descriptor,
            out PacketOwnedRadioTrackResolution resolution)
        {
            resolution = null;

            foreach ((string imageName, string propertyPath) in BuildPacketOwnedRadioTrackCandidates(descriptor))
            {
                WzImageProperty trackNode = ResolvePacketOwnedSoundProperty(imageName, propertyPath);
                if (trackNode == null)
                {
                    continue;
                }

                if (TryCreatePacketOwnedRadioTrackResolution(trackNode, $"{imageName[..^4]}/{propertyPath}", out resolution))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<(string ImageName, string PropertyPath)> BuildPacketOwnedRadioTrackCandidates(string descriptor)
        {
            if (string.IsNullOrWhiteSpace(descriptor))
            {
                yield break;
            }

            string normalized = descriptor.Trim().Replace('\\', '/');
            string[] segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                yield break;
            }

            yield return ("Radio.img", normalized);

            if (segments.Length >= 2)
            {
                yield return (NormalizePacketOwnedSoundImageName(segments[0]), string.Join("/", segments.Skip(1)));
            }
        }

        private static bool TryCreatePacketOwnedRadioTrackResolution(
            WzImageProperty trackNode,
            string resolvedTrackDescriptor,
            out PacketOwnedRadioTrackResolution resolution)
        {
            resolution = null;
            WzImageProperty resolvedTrackNode = WzInfoTools.GetRealProperty(trackNode);
            if (resolvedTrackNode == null)
            {
                return false;
            }

            WzBinaryProperty audioProperty = ResolvePacketOwnedRadioAudioProperty(resolvedTrackNode);
            if (audioProperty == null)
            {
                return false;
            }

            string displayName = ResolvePacketOwnedRadioDisplayName(
                resolvedTrackNode,
                resolvedTrackDescriptor.Split('/').LastOrDefault() ?? resolvedTrackDescriptor);
            resolution = CreatePacketOwnedRadioTrackResolution(
                audioProperty,
                resolvedTrackDescriptor,
                ResolvePacketOwnedDescriptor(audioProperty) ?? resolvedTrackDescriptor,
                displayName);
            return true;
        }

        private static PacketOwnedRadioTrackResolution CreatePacketOwnedRadioTrackResolution(
            WzBinaryProperty audioProperty,
            string resolvedTrackDescriptor,
            string resolvedAudioDescriptor,
            string displayName)
        {
            return new PacketOwnedRadioTrackResolution
            {
                AudioProperty = audioProperty,
                ResolvedTrackDescriptor = resolvedTrackDescriptor,
                ResolvedAudioDescriptor = resolvedAudioDescriptor,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? "radio track" : displayName.Trim(),
            };
        }

        private static WzBinaryProperty ResolvePacketOwnedRadioAudioProperty(WzImageProperty trackNode)
        {
            if (trackNode is WzBinaryProperty binaryProperty)
            {
                return binaryProperty;
            }

            string[] preferredChildNames =
            {
                "sound",
                "Sound",
                "bgm",
                "Bgm",
                "track",
                "Track",
                "music",
                "Music",
                "0",
            };

            for (int i = 0; i < preferredChildNames.Length; i++)
            {
                WzImageProperty candidate = WzInfoTools.GetRealProperty(trackNode[preferredChildNames[i]]);
                if (candidate is WzBinaryProperty binaryChild)
                {
                    return binaryChild;
                }
            }

            return null;
        }

        private static string ResolvePacketOwnedRadioDisplayName(WzObject source, string fallback)
        {
            string normalizedFallback = string.IsNullOrWhiteSpace(fallback) ? "radio track" : fallback.Trim();
            for (WzObject current = source; current != null; current = current.Parent)
            {
                if (current is not WzImageProperty propertyContainer)
                {
                    continue;
                }

                if (propertyContainer["name"] is WzStringProperty nameProperty
                    && !string.IsNullOrWhiteSpace(nameProperty.Value))
                {
                    return nameProperty.Value.Trim();
                }
            }

            return normalizedFallback;
        }

        private static string ResolvePacketOwnedDescriptor(WzImageProperty property)
        {
            if (property == null)
            {
                return null;
            }

            WzImage ownerImage = property.GetTopMostWzImage() as WzImage;
            string propertyPath = WzInformationManager.GetPropertyPathRelativeToImage(property);
            if (ownerImage == null || string.IsNullOrWhiteSpace(propertyPath))
            {
                return null;
            }

            return $"{ownerImage.Name[..^4]}/{propertyPath}";
        }

        private string ApplyPacketOwnedNoticeMessage(string message)
        {
            StampPacketOwnedUtilityRequestState();
            _lastPacketOwnedNoticeMessage = message?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedNoticeMessage))
            {
                TryPlayPacketOwnedNoticeSound();
                _chat?.AddClientChatMessage(
                    $"[Notice] {_lastPacketOwnedNoticeMessage}",
                    Environment.TickCount,
                    13);
                ShowPacketOwnedNoticeDialog(_lastPacketOwnedNoticeMessage);
                ShowUtilityFeedbackMessage(_lastPacketOwnedNoticeMessage);
            }

            return string.IsNullOrWhiteSpace(_lastPacketOwnedNoticeMessage)
                ? "Packet-owned notice was empty."
                : $"Queued packet-owned notice: {_lastPacketOwnedNoticeMessage}";
        }

        private string ApplyPacketOwnedChatMessage(string message, string channel = null)
        {
            return ApplyPacketOwnedChatMessage(message, null, channel, -1);
        }

        private string ApplyPacketOwnedChatMessage(string message, int? chatLogType, string channel = null, int channelId = -1)
        {
            StampPacketOwnedUtilityRequestState();
            _lastPacketOwnedChatMessage = message?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_lastPacketOwnedChatMessage))
            {
                return "Packet-owned chat line was empty.";
            }

            if (chatLogType.HasValue)
            {
                _chat?.AddClientChatMessage(_lastPacketOwnedChatMessage, Environment.TickCount, chatLogType.Value, null, channelId);
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
            _chat?.AddClientChatMessage(
                route.Line,
                Environment.TickCount,
                route.ChatLogType,
                route.WhisperTargetCandidate,
                route.ChannelId);
            string line = route.Line;
            return $"Queued packet-owned chat line: {line}";
        }

        private string ApplyPacketOwnedSkillCooltime(int skillId, int remainingSeconds)
        {
            StampPacketOwnedUtilityRequestState();
            ResetPacketOwnedBattleshipCooldownOverrideIfStale();
            int normalizedSkillId = NormalizePacketOwnedCooldownSkillId(skillId, out bool isVehicleSentinel);
            if (normalizedSkillId <= 0)
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
                _playerManager.Skills.SetServerCooldownRemaining(normalizedSkillId, remainingMs, currTickCount);
            }
            else
            {
                _playerManager.Skills.ClearServerCooldown(normalizedSkillId, currTickCount);
            }

            if (isVehicleSentinel)
            {
                ApplyPacketOwnedBattleshipCooldownSideEffects(remainingSeconds);
            }

            var skill = _playerManager.Skills.GetSkillData(normalizedSkillId) ?? _playerManager.SkillLoader?.LoadSkill(normalizedSkillId);
            string skillName = skill?.Name
                ?? (isVehicleSentinel ? "Battleship" : $"Skill {normalizedSkillId}");
            ShowPacketOwnedSkillCooldownNotification(skill, normalizedSkillId, skillName, remainingMs);
            string message = remainingMs > 0
                ? $"Applied packet-owned {(isVehicleSentinel ? "vehicle " : string.Empty)}skill cooldown for {skillName}: {FormatCooldownNotificationSeconds(remainingMs)} remaining."
                : $"Cleared packet-owned {(isVehicleSentinel ? "vehicle " : string.Empty)}skill cooldown for {skillName}.";
            ShowUtilityFeedbackMessage(message);
            return message;
        }

        private string ApplyPacketOwnedTimeBombAttack(int skillId, int action, int hitPeriodMs, int impactPercent, int damage)
        {
            StampPacketOwnedUtilityRequestState();
            if (_playerManager?.Skills == null || _playerManager.Player == null || _playerManager.Combat == null)
            {
                const string unavailable = "Time Bomb parity could not be applied because the local player skill runtime is not initialized.";
                ShowUtilityFeedbackMessage(unavailable);
                return unavailable;
            }

            int normalizedSkillId = NormalizePacketOwnedSkillId(skillId);
            if (!_playerManager.Skills.TryApplyPacketOwnedMeleeAttack(normalizedSkillId, currTickCount, out SkillData skill, out int level, out string errorMessage))
            {
                ShowUtilityFeedbackMessage(errorMessage);
                return errorMessage;
            }

            int resolvedHitPeriodMs = Math.Max(0, hitPeriodMs);
            if (resolvedHitPeriodMs > 0)
            {
                _playerManager.Combat.SetInvincible(currTickCount, resolvedHitPeriodMs);
            }

            float knockbackX = 0f;
            if (impactPercent > 0)
            {
                float impactMagnitude = Math.Max(390f, impactPercent * 4f);
                knockbackX = _playerManager.Player.FacingRight ? -impactMagnitude : impactMagnitude;
            }

            _playerManager.Player.ApplyPacketDamageReaction(
                Math.Max(0, damage),
                Math.Max(1, resolvedHitPeriodMs),
                knockbackX,
                0f);

            string skillName = skill?.Name ?? $"Skill {normalizedSkillId}";
            string message = $"Applied packet-owned Time Bomb attack for {skillName} Lv.{level} (action {Math.Max(0, action)}, hit {Math.Max(0, hitPeriodMs)} ms, impact {Math.Max(0, impactPercent)}%, damage {Math.Max(0, damage)}).";
            ShowUtilityFeedbackMessage(message);
            return message;
        }

        private string ApplyPacketOwnedVengeanceSkillApply(int skillId)
        {
            StampPacketOwnedUtilityRequestState();
            if (_playerManager?.Skills == null)
            {
                const string unavailable = "Vengeance parity could not be applied because the local player skill runtime is not initialized.";
                ShowUtilityFeedbackMessage(unavailable);
                return unavailable;
            }

            if (skillId != PacketOwnedLegacyVengeanceSkillId && skillId != PacketOwnedCurrentVengeanceSkillId)
            {
                string ignored = $"Ignored packet-owned Vengeance apply for unexpected skill id {skillId}.";
                ShowUtilityFeedbackMessage(ignored);
                return ignored;
            }

            int normalizedSkillId = NormalizePacketOwnedSkillId(skillId);
            if (!_playerManager.Skills.TryApplyPacketOwnedMeleeAttack(normalizedSkillId, currTickCount, out SkillData skill, out int level, out string errorMessage))
            {
                ShowUtilityFeedbackMessage(errorMessage);
                return errorMessage;
            }

            string skillName = skill?.Name ?? $"Skill {normalizedSkillId}";
            string message = $"Applied packet-owned Vengeance melee counter for {skillName} Lv.{level}.";
            ShowUtilityFeedbackMessage(message);
            return message;
        }

        private string ApplyPacketOwnedExJablinApply()
        {
            StampPacketOwnedUtilityRequestState();
            if (_playerManager?.Skills == null)
            {
                const string unavailable = "ExJablin parity could not be applied because the local player skill runtime is not initialized.";
                ShowUtilityFeedbackMessage(unavailable);
                return unavailable;
            }

            _playerManager.Skills.ArmPacketOwnedExJablin();
            const string message = "Armed the packet-owned ExJablin next-shot state for the next ranged attack.";
            ShowUtilityFeedbackMessage(message);
            return message;
        }

        private void ShowPacketOwnedNoticeDialog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            ShowLoginUtilityDialog(
                "Notice",
                message,
                LoginUtilityDialogButtonLayout.Ok,
                LoginUtilityDialogAction.DismissOnly);
        }

        private void ShowPacketOwnedSkillCooldownNotification(
            SkillData skill,
            int skillId,
            string skillName,
            int remainingMs)
        {
            if (!ShouldShowOffBarSkillCooldownNotification(skillId))
            {
                return;
            }

            string resolvedSkillName = string.IsNullOrWhiteSpace(skillName)
                ? $"Skill {skillId}"
                : skillName;
            string notification = remainingMs > 0
                ? $"{resolvedSkillName} is cooling down. {FormatCooldownNotificationSeconds(remainingMs)}."
                : $"{resolvedSkillName} is ready.";
            ShowSkillCooldownNotification(
                skill,
                notification,
                currTickCount,
                addChat: remainingMs <= 0,
                remainingMs > 0 ? SkillCooldownNoticeType.Started : SkillCooldownNoticeType.Ready);
        }

        private void ApplyPacketOwnedBattleshipCooldownSideEffects(int remainingUnits)
        {
            CharacterPart mountPart = ResolveActivePacketOwnedBattleshipMountPart();
            if (mountPart == null)
            {
                if (remainingUnits <= 0)
                {
                    RestorePacketOwnedBattleshipCooldownOverride();
                }

                return;
            }

            int skillLevel = Math.Max(0, _playerManager.Skills?.GetSkillLevel(PacketOwnedBattleshipSkillId) ?? 0);
            int characterLevel = Math.Max(0, _playerManager.Player.Build.Level);
            if (remainingUnits > 0)
            {
                EnsurePacketOwnedBattleshipCooldownOverrideCaptured(mountPart);

                int maxDurability = ResolvePacketOwnedBattleshipMaxDurability(skillLevel, characterLevel);
                if (maxDurability > 0)
                {
                    mountPart.MaxDurability = maxDurability;
                }

                int boundedMaxDurability = Math.Max(0, mountPart.MaxDurability ?? maxDurability);
                mountPart.Durability = boundedMaxDurability > 0
                    ? Math.Clamp(remainingUnits, 0, boundedMaxDurability)
                    : Math.Max(0, remainingUnits);
            }
            else
            {
                RestorePacketOwnedBattleshipCooldownOverride();
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.RepairDurability) is RepairDurabilityWindow repairWindow)
            {
                int npcTemplateId = repairWindow.NpcTemplateId;
                RefreshRepairDurabilityWindow(npcTemplateId, mountPart.ItemId);
            }
        }

        private CharacterPart ResolveActivePacketOwnedBattleshipMountPart()
        {
            if (_playerManager?.Player?.Build?.Equipment == null
                || !_playerManager.Player.Build.Equipment.TryGetValue(EquipSlot.TamingMob, out CharacterPart mountPart)
                || mountPart?.Slot != EquipSlot.TamingMob
                || mountPart.ItemId != PacketOwnedBattleshipMountItemId)
            {
                return null;
            }

            return mountPart;
        }

        private void EnsurePacketOwnedBattleshipCooldownOverrideCaptured(CharacterPart mountPart)
        {
            if (mountPart?.Slot != EquipSlot.TamingMob)
            {
                return;
            }

            if (ReferenceEquals(_packetOwnedBattleshipDurabilityOverride?.MountPart, mountPart))
            {
                return;
            }

            _packetOwnedBattleshipDurabilityOverride = new PacketOwnedBattleshipDurabilityOverrideState
            {
                MountPart = mountPart,
                OriginalDurability = mountPart.Durability,
                OriginalMaxDurability = mountPart.MaxDurability
            };
        }

        private void RestorePacketOwnedBattleshipCooldownOverride()
        {
            if (_packetOwnedBattleshipDurabilityOverride?.MountPart == null)
            {
                _packetOwnedBattleshipDurabilityOverride = null;
                return;
            }

            _packetOwnedBattleshipDurabilityOverride.MountPart.Durability = _packetOwnedBattleshipDurabilityOverride.OriginalDurability;
            _packetOwnedBattleshipDurabilityOverride.MountPart.MaxDurability = _packetOwnedBattleshipDurabilityOverride.OriginalMaxDurability;
            _packetOwnedBattleshipDurabilityOverride = null;
        }

        private void ResetPacketOwnedBattleshipCooldownOverrideIfStale()
        {
            if (_packetOwnedBattleshipDurabilityOverride?.MountPart == null)
            {
                _packetOwnedBattleshipDurabilityOverride = null;
                return;
            }

            CharacterPart activeMountPart = ResolveActivePacketOwnedBattleshipMountPart();
            if (!ReferenceEquals(activeMountPart, _packetOwnedBattleshipDurabilityOverride.MountPart))
            {
                RestorePacketOwnedBattleshipCooldownOverride();
            }
        }

        private static int ResolvePacketOwnedBattleshipMaxDurability(int skillLevel, int characterLevel)
        {
            if (skillLevel <= 0 || characterLevel <= 0)
            {
                return 0;
            }

            // Client evidence: get_max_durability_of_vehicle(5221006, slv, charLevel)
            // returns 300 * level + 500 * (slv - 72) for the Battleship sentinel branch.
            return Math.Max(0, (300 * characterLevel) + (500 * (skillLevel - 72)));
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
            _packetOwnedApspPromptActive = false;
            _lastPacketOwnedApspFollowUpContextToken = 0;
            _lastPacketOwnedApspFollowUpResponseCode = 0;
            _lastPacketOwnedAskApspMessage = string.IsNullOrWhiteSpace(message)
                ? "Packet-owned AP/SP event prompt triggered."
                : message.Trim();
            ShowUtilityFeedbackMessage(_lastPacketOwnedAskApspMessage);
            return _lastPacketOwnedAskApspMessage;
        }

        private string ApplyPacketOwnedTutorHire(bool enabled)
        {
            StampPacketOwnedUtilityRequestState();

            if (!enabled)
            {
                _packetOwnedTutorRuntime.ApplyRemoval("packet-owned tutor branch requested removal.");
                RemovePacketOwnedTutorSummon();
                ShowUtilityFeedbackMessage(_packetOwnedTutorRuntime.StatusMessage);
                return _packetOwnedTutorRuntime.StatusMessage;
            }

            // Client evidence: CUserLocal::OnHireTutor removes the prior tutor owner
            // before allocating and initializing the next one.
            RemovePacketOwnedTutorSummon();
            int skillId = ResolvePacketOwnedTutorSkillId();
            _packetOwnedTutorRuntime.ApplyHire(skillId, currTickCount);
            string summonDetail = EnsurePacketOwnedTutorSummon(currTickCount);
            string message = string.IsNullOrWhiteSpace(summonDetail)
                ? $"Activated packet-owned {DescribePacketOwnedTutorVariant(skillId)} tutor ownership."
                : $"Activated packet-owned {DescribePacketOwnedTutorVariant(skillId)} tutor ownership. {summonDetail}";
            ShowUtilityFeedbackMessage(message);
            return message;
        }

        private string ApplyPacketOwnedTutorIndexedMessage(int messageIndex, int durationMs)
        {
            StampPacketOwnedUtilityRequestState();
            if (!_packetOwnedTutorRuntime.IsActive)
            {
                const string inactiveMessage = "Ignored packet-owned tutor indexed message because no tutor actor is active.";
                ShowUtilityFeedbackMessage(inactiveMessage);
                return inactiveMessage;
            }

            _packetOwnedTutorRuntime.ApplyIndexedMessage(messageIndex, durationMs, currTickCount);
            string message = $"Applied packet-owned tutor cue #{Math.Max(0, messageIndex)} ({Math.Max(0, durationMs)}).";
            ShowUtilityFeedbackMessage(message);
            return message;
        }

        private string ApplyPacketOwnedTutorTextMessage(string text, int width, int durationMs)
        {
            StampPacketOwnedUtilityRequestState();
            if (!_packetOwnedTutorRuntime.IsActive)
            {
                const string inactiveMessage = "Ignored packet-owned tutor text message because no tutor actor is active.";
                ShowUtilityFeedbackMessage(inactiveMessage);
                return inactiveMessage;
            }

            _packetOwnedTutorRuntime.ApplyTextMessage(text, width, durationMs, currTickCount);
            if (!string.IsNullOrWhiteSpace(_packetOwnedTutorRuntime.ActiveMessageText))
            {
                ShowUtilityFeedbackMessage($"Tutor: {_packetOwnedTutorRuntime.ActiveMessageText}");
            }

            return $"Applied packet-owned tutor text message ({_packetOwnedTutorRuntime.ActiveMessageWidth}px, {_packetOwnedTutorRuntime.ActiveMessageDurationMs} ms).";
        }

        private string ApplyPacketOwnedSkillGuideLaunch()
        {
            string skillWindowMessage = ApplyPacketOwnedOpenUi(3, 1);
            int currentJobId = _playerManager?.Player?.Build?.Job ?? 0;
            int guideGrade = ResolvePacketOwnedAranGuideGrade(currentJobId);
            _lastPacketOwnedSkillGuideGrade = guideGrade;

            if (guideGrade <= 0)
            {
                _lastPacketOwnedSkillGuideMessage = $"{skillWindowMessage} The current job does not expose an Aran skill-guide page.";
                ShowUtilityFeedbackMessage(_lastPacketOwnedSkillGuideMessage);
                return _lastPacketOwnedSkillGuideMessage;
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.AranSkillGuide) is not AranSkillGuideUI aranSkillGuideWindow)
            {
                _lastPacketOwnedSkillGuideMessage = $"{skillWindowMessage} Aran skill-guide owner is not available in this UI build.";
                ShowUtilityFeedbackMessage(_lastPacketOwnedSkillGuideMessage);
                return _lastPacketOwnedSkillGuideMessage;
            }

            aranSkillGuideWindow.SetPage(guideGrade);
            ShowWindow(
                MapSimulatorWindowNames.AranSkillGuide,
                aranSkillGuideWindow,
                trackDirectionModeOwner: ShouldTrackInheritedDirectionModeOwner());
            _lastPacketOwnedSkillGuideMessage = $"{skillWindowMessage} Opened the packet-owned current skill guide at Aran grade {guideGrade}.";
            ShowUtilityFeedbackMessage(_lastPacketOwnedSkillGuideMessage);
            return _lastPacketOwnedSkillGuideMessage;
        }

        private bool TryApplyPacketOwnedAskApspEventPayload(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length == 0)
            {
                message = "AP/SP payload is missing.";
                return false;
            }

            if (payload.Length == sizeof(int) * 2)
            {
                try
                {
                    using MemoryStream stream = new(payload, writable: false);
                    using BinaryReader reader = new(stream);
                    int contextToken = reader.ReadInt32();
                    int eventType = reader.ReadInt32();
                    return TryApplyPacketOwnedAskApspEvent(contextToken, eventType, out message);
                }
                catch (Exception ex)
                {
                    message = $"AP/SP payload could not be decoded: {ex.Message}";
                    return false;
                }
            }

            return TryApplyPacketOwnedStringPayload(payload, ApplyPacketOwnedAskApspEvent, "AP/SP payload is missing.", out message);
        }

        private bool TryApplyPacketOwnedAskApspEvent(int contextToken, int eventType, out string message)
        {
            StampPacketOwnedUtilityRequestState();
            int expectedContextToken = ResolvePacketOwnedApspReceiveContextToken();
            if (contextToken != expectedContextToken)
            {
                _packetOwnedApspPromptActive = false;
                _lastPacketOwnedAskApspMessage = $"Suppressed packet-owned AP/SP helper prompt because context token {contextToken} did not match the active local context {expectedContextToken}.";
                message = _lastPacketOwnedAskApspMessage;
                ShowUtilityFeedbackMessage(message);
                return false;
            }

            if (eventType < PacketOwnedApspMinEventType || eventType > PacketOwnedApspMaxEventType)
            {
                _packetOwnedApspPromptActive = false;
                _lastPacketOwnedAskApspMessage = $"Packet-owned AP/SP helper prompt rejected unsupported event type {eventType}.";
                message = _lastPacketOwnedAskApspMessage;
                ShowUtilityFeedbackMessage(message);
                return false;
            }

            _packetOwnedApspPromptActive = true;
            _packetOwnedApspPromptContextToken = contextToken;
            _packetOwnedApspPromptEventType = eventType;
            _lastPacketOwnedApspFollowUpContextToken = 0;
            _lastPacketOwnedApspFollowUpResponseCode = 0;
            _lastPacketOwnedAskApspMessage = $"Opened packet-owned AP/SP helper prompt for context {contextToken}, event {eventType}, StringPool 0x{PacketOwnedApspPromptStringPoolId:X}.";
            ShowLoginUtilityDialog(
                "AP/SP Helper",
                BuildPacketOwnedApspPromptBody(contextToken, eventType),
                LoginUtilityDialogButtonLayout.YesNo,
                LoginUtilityDialogAction.ConfirmApspEvent,
                primaryLabel: PacketOwnedApspPromptPrimaryLabel,
                secondaryLabel: PacketOwnedApspPromptSecondaryLabel);
            message = _lastPacketOwnedAskApspMessage;
            return true;
        }

        private void AcceptPacketOwnedAskApspEventPrompt()
        {
            if (!_packetOwnedApspPromptActive)
            {
                HideLoginUtilityDialog();
                return;
            }

            _lastPacketOwnedApspFollowUpContextToken = ResolvePacketOwnedApspFollowUpContextToken(_packetOwnedApspPromptContextToken);
            _lastPacketOwnedApspFollowUpResponseCode = PacketOwnedApspFollowUpResponseCode;
            _lastPacketOwnedAskApspMessage =
                $"Accepted packet-owned AP/SP helper event {_packetOwnedApspPromptEventType}; simulated outpacket {PacketOwnedApspFollowUpOpcode} ({_lastPacketOwnedApspFollowUpContextToken}, {PacketOwnedApspFollowUpResponseCode}).";
            _packetOwnedApspPromptActive = false;
            HideLoginUtilityDialog();
            ShowUtilityFeedbackMessage(_lastPacketOwnedAskApspMessage);
        }

        private void DeclinePacketOwnedAskApspEventPrompt()
        {
            if (!_packetOwnedApspPromptActive)
            {
                HideLoginUtilityDialog();
                return;
            }

            _lastPacketOwnedAskApspMessage =
                $"Declined packet-owned AP/SP helper event {_packetOwnedApspPromptEventType}; outpacket {PacketOwnedApspFollowUpOpcode} was not emitted.";
            _packetOwnedApspPromptActive = false;
            HideLoginUtilityDialog();
            ShowUtilityFeedbackMessage(_lastPacketOwnedAskApspMessage);
        }

        private int ResolvePacketOwnedApspReceiveContextToken()
        {
            SyncPacketOwnedApspContextLifecycle();
            return _packetOwnedLocalUtilityContext.ResolveApspReceiveContextToken(ResolvePacketOwnedApspRuntimeCharacterId());
        }

        private int ResolvePacketOwnedApspFollowUpContextToken(int promptContextToken)
        {
            SyncPacketOwnedApspContextLifecycle();
            return ResolvePacketOwnedApspFollowUpContextToken(
                promptContextToken,
                _packetOwnedLocalUtilityContext.ResolveApspSendContextToken(promptContextToken, ResolvePacketOwnedApspRuntimeCharacterId()));
        }

        private void SyncPacketOwnedApspContextLifecycle()
        {
            int runtimeCharacterId = ResolvePacketOwnedApspRuntimeCharacterId();
            _packetOwnedLocalUtilityContext.ObserveRuntimeCharacterId(runtimeCharacterId);

            if (runtimeCharacterId <= 0)
            {
                return;
            }

            if (!_packetOwnedLocalUtilityContext.HasPersistedApspState)
            {
                _packetOwnedLocalUtilityContext.SeedFromCharacterId(runtimeCharacterId);
                return;
            }

            if (_packetOwnedLocalUtilityContext.BoundCharacterId > 0
                && _packetOwnedLocalUtilityContext.BoundCharacterId != runtimeCharacterId)
            {
                ResetPacketOwnedApspContextForCharacter(runtimeCharacterId);
            }
        }

        private void ResetPacketOwnedApspContextForCharacter(int runtimeCharacterId)
        {
            bool shouldHideMessageBox = _packetOwnedApspPromptActive;
            _packetOwnedLocalUtilityContext.SeedFromCharacterId(runtimeCharacterId);
            _packetOwnedApspPromptActive = false;
            _packetOwnedApspPromptContextToken = 0;
            _packetOwnedApspPromptEventType = 0;
            _lastPacketOwnedApspFollowUpContextToken = 0;
            _lastPacketOwnedApspFollowUpResponseCode = 0;

            if (shouldHideMessageBox)
            {
                HideLoginUtilityDialog();
            }

            _lastPacketOwnedAskApspMessage =
                $"Reset packet-owned local utility CWvsContext AP/SP tokens for runtime character {runtimeCharacterId} after a local character transition.";
        }

        private int ResolvePacketOwnedApspRuntimeCharacterId()
        {
            return Math.Max(0, _playerManager?.Player?.Build?.Id ?? 0);
        }

        private int ResolvePacketOwnedApspSeedCharacterId()
        {
            return Math.Max(1, ResolvePacketOwnedApspRuntimeCharacterId());
        }

        private string DescribePacketOwnedApspContextStatus()
        {
            SyncPacketOwnedApspContextLifecycle();
            return _packetOwnedLocalUtilityContext.DescribeApspContext();
        }

        private string SeedPacketOwnedApspContextTokens(int? characterId)
        {
            int resolvedCharacterId = Math.Max(1, characterId ?? ResolvePacketOwnedApspSeedCharacterId());
            _packetOwnedLocalUtilityContext.SeedFromCharacterId(resolvedCharacterId);
            return $"Seeded packet-owned local utility CWvsContext AP/SP tokens from character {resolvedCharacterId}.";
        }

        private string OverridePacketOwnedApspReceiveContextToken(int receiveContextToken)
        {
            _packetOwnedLocalUtilityContext.SetApspReceiveContextToken(receiveContextToken, ResolvePacketOwnedApspSeedCharacterId());
            return $"Set packet-owned local utility AP/SP receive token (+0x20B4) to {_packetOwnedLocalUtilityContext.ApspReceiveContextToken}. {_packetOwnedLocalUtilityContext.DescribeApspContext()}";
        }

        private string OverridePacketOwnedApspSendContextToken(int sendContextToken)
        {
            _packetOwnedLocalUtilityContext.SetApspSendContextToken(sendContextToken, ResolvePacketOwnedApspSeedCharacterId());
            return $"Set packet-owned local utility AP/SP send token (+0x2030) to {_packetOwnedLocalUtilityContext.ApspSendContextToken}. {_packetOwnedLocalUtilityContext.DescribeApspContext()}";
        }

        private string OverridePacketOwnedApspContextTokens(int receiveContextToken, int? sendContextToken)
        {
            int resolvedSendContextToken = sendContextToken ?? receiveContextToken;
            _packetOwnedLocalUtilityContext.SetApspContextTokens(receiveContextToken, resolvedSendContextToken, ResolvePacketOwnedApspSeedCharacterId());
            return $"Set packet-owned local utility CWvsContext AP/SP tokens to recv={_packetOwnedLocalUtilityContext.ApspReceiveContextToken}, send={_packetOwnedLocalUtilityContext.ApspSendContextToken}. {_packetOwnedLocalUtilityContext.DescribeApspContext()}";
        }

        private static int ResolvePacketOwnedApspFollowUpContextToken(int promptContextToken, int sendContextToken)
        {
            return sendContextToken > 0 ? sendContextToken : promptContextToken;
        }

        private static int ResolvePacketOwnedAranGuideGrade(int jobId)
        {
            return jobId switch
            {
                2000 => 1,
                2100 => 1,
                2110 => 2,
                2111 => 3,
                2112 => 4,
                _ => 0
            };
        }

        private int ResolvePacketOwnedTutorSkillId()
        {
            int jobId = _playerManager?.Player?.Build?.Job ?? 0;
            int jobFamily = jobId / 1000;
            return jobFamily switch
            {
                1 => TutorRuntime.CygnusTutorSkillId,
                2 => TutorRuntime.AranTutorSkillId,
                _ => _packetOwnedTutorRuntime.ActiveSkillId > 0
                    ? _packetOwnedTutorRuntime.ActiveSkillId
                    : TutorRuntime.AranTutorSkillId
            };
        }

        private string DescribePacketOwnedTutorStatus(int currentTickCount)
        {
            if (!_packetOwnedTutorRuntime.IsActive)
            {
                return "Tutor: idle.";
            }

            string variant = DescribePacketOwnedTutorVariant(_packetOwnedTutorRuntime.ActiveSkillId);
            if (_packetOwnedTutorRuntime.HasVisibleTextMessage(currentTickCount))
            {
                int remainingMs = Math.Max(0, unchecked(_packetOwnedTutorRuntime.ActiveMessageExpiresAt - currentTickCount));
                return $"Tutor: {variant}, {TruncatePacketOwnedUtilityText(_packetOwnedTutorRuntime.ActiveMessageText, 96)} ({remainingMs} ms left).";
            }

            if (_packetOwnedTutorRuntime.HasVisibleIndexedCue(currentTickCount))
            {
                int remainingMs = Math.Max(0, unchecked(_packetOwnedTutorRuntime.ActiveMessageExpiresAt - currentTickCount));
                return $"Tutor: {variant}, cue #{Math.Max(0, _packetOwnedTutorRuntime.LastIndexedMessage)} ({remainingMs} ms left).";
            }

            return $"Tutor: {variant}, waiting for message.";
        }

        private void LoadPacketOwnedTutorAssets()
        {
            WzImage chatBalloonImage = Program.FindImage("UI", "ChatBalloon.img");
            WzSubProperty tutorialBalloonSource = chatBalloonImage?["tutorial"] as WzSubProperty;
            if (tutorialBalloonSource == null)
            {
                return;
            }

            _packetOwnedTutorBalloonSkin = new LocalOverlayBalloonSkin
            {
                NorthWest = LoadUiCanvasTexture(tutorialBalloonSource["nw"] as WzCanvasProperty),
                NorthEast = LoadUiCanvasTexture(tutorialBalloonSource["ne"] as WzCanvasProperty),
                SouthWest = LoadUiCanvasTexture(tutorialBalloonSource["sw"] as WzCanvasProperty),
                SouthEast = LoadUiCanvasTexture(tutorialBalloonSource["se"] as WzCanvasProperty),
                North = LoadUiCanvasTexture(tutorialBalloonSource["n"] as WzCanvasProperty),
                South = LoadUiCanvasTexture(tutorialBalloonSource["s"] as WzCanvasProperty),
                West = LoadUiCanvasTexture(tutorialBalloonSource["w"] as WzCanvasProperty),
                East = LoadUiCanvasTexture(tutorialBalloonSource["e"] as WzCanvasProperty),
                Center = LoadUiCanvasTexture(tutorialBalloonSource["c"] as WzCanvasProperty),
                Arrow = LoadUiArrowSprite(tutorialBalloonSource["arrow"] as WzCanvasProperty),
                TextColor = Color.Black
            };
        }

        private string EnsurePacketOwnedTutorSummon(int currentTickCount)
        {
            if (!_packetOwnedTutorRuntime.IsActive)
            {
                RemovePacketOwnedTutorSummon();
                return null;
            }

            PlayerCharacter player = _playerManager?.Player;
            if (player?.Build == null)
            {
                return "Tutor summon is waiting for a local player.";
            }

            if (TryGetPacketOwnedTutorSummon(out ActiveSummon summon))
            {
                SyncPacketOwnedTutorSummonPose(summon, player, currentTickCount);
                return null;
            }

            int ownerCharacterId = player.Build.Id;
            if (ownerCharacterId <= 0)
            {
                return "Tutor summon could not resolve a local character id.";
            }

            int skillLevel = Math.Max(1, _playerManager?.Skills?.GetSkillLevel(_packetOwnedTutorRuntime.ActiveSkillId) ?? 0);
            byte moveAction = player.FacingRight ? (byte)0 : (byte)1;
            var packet = new SummonedCreatePacket(
                ownerCharacterId,
                _packetOwnedTutorRuntime.ActiveSummonObjectId,
                _packetOwnedTutorRuntime.ActiveSkillId,
                Math.Max(1, player.Level),
                skillLevel,
                player.Position,
                moveAction,
                0,
                0,
                0,
                2,
                null,
                0,
                Array.Empty<Point>());

            if (!_summonedPool.TryCreate(packet, currentTickCount, out string message))
            {
                return $"Tutor summon creation failed: {message}";
            }

            if (TryGetPacketOwnedTutorSummon(out summon))
            {
                SyncPacketOwnedTutorSummonPose(summon, player, currentTickCount);
            }

            return "Tutor summon owner created from the packet-owned summoned seam.";
        }

        private void SyncPacketOwnedTutorSummonState(int currentTickCount)
        {
            if (!_packetOwnedTutorRuntime.IsActive)
            {
                RemovePacketOwnedTutorSummon();
                return;
            }

            string summonDetail = EnsurePacketOwnedTutorSummon(currentTickCount);
            if (!string.IsNullOrWhiteSpace(summonDetail))
            {
                return;
            }

            if (!TryGetPacketOwnedTutorSummon(out ActiveSummon summon))
            {
                return;
            }

            PlayerCharacter player = _playerManager?.Player;
            if (player != null)
            {
                SyncPacketOwnedTutorSummonPose(summon, player, currentTickCount);
            }
        }

        private void SyncPacketOwnedTutorSummonPose(ActiveSummon summon, PlayerCharacter player, int currentTickCount)
        {
            if (summon == null || player == null)
            {
                return;
            }

            summon.PreviousPositionX = summon.PositionX;
            summon.PreviousPositionY = summon.PositionY;
            summon.AnchorX = player.X;
            summon.AnchorY = player.Y;
            summon.PositionX = player.X;
            summon.PositionY = player.Y;
            summon.FacingRight = player.FacingRight;
            summon.LastStateChangeTime = currentTickCount;
            summon.ActorState = _packetOwnedTutorRuntime.HasVisibleMessage(currentTickCount)
                ? SummonActorState.Prepare
                : SummonActorState.Idle;
            summon.CurrentAnimationBranchName = _packetOwnedTutorRuntime.HasVisibleMessage(currentTickCount)
                ? "say"
                : null;
        }

        private void RemovePacketOwnedTutorSummon()
        {
            _summonedPool.TryConsumeSummonByObjectId(_packetOwnedTutorRuntime.ActiveSummonObjectId);
            _summonedPool.TryConsumeSummonByObjectId(TutorRuntime.CygnusTutorObjectId);
            _summonedPool.TryConsumeSummonByObjectId(TutorRuntime.AranTutorObjectId);
        }

        private bool TryGetPacketOwnedTutorSummon(out ActiveSummon summon)
        {
            summon = null;
            if (!_packetOwnedTutorRuntime.IsActive || _packetOwnedTutorRuntime.ActiveSummonObjectId <= 0)
            {
                return false;
            }

            summon = _playerManager?.Player?.PacketOwnedSummons?.Summons?
                .FirstOrDefault(candidate => candidate?.ObjectId == _packetOwnedTutorRuntime.ActiveSummonObjectId && !candidate.IsPendingRemoval);
            return summon != null;
        }

        private void DrawPacketOwnedTutorState(int currentTickCount, int mapCenterX, int mapCenterY)
        {
            if (_spriteBatch == null)
            {
                return;
            }

            DrawPacketOwnedTutorIndexedCue(currentTickCount, mapCenterX, mapCenterY);

            if (!_packetOwnedTutorRuntime.HasVisibleTextMessage(currentTickCount)
                || _fontChat == null
                || _packetOwnedTutorBalloonSkin?.IsLoaded != true)
            {
                return;
            }

            if (!TryResolvePacketOwnedTutorBalloonAnchorScreenPoint(mapCenterX, mapCenterY, out Point anchor))
            {
                return;
            }

            DrawPacketOwnedTutorBalloon(anchor);
        }

        private void DrawPacketOwnedTutorIndexedCue(int currentTickCount, int mapCenterX, int mapCenterY)
        {
            if (!_packetOwnedTutorRuntime.HasVisibleIndexedCue(currentTickCount))
            {
                return;
            }

            List<IDXObject> frames = ResolvePacketOwnedTutorCueFrames(_packetOwnedTutorRuntime.LastIndexedMessage);
            if (frames == null || frames.Count == 0)
            {
                return;
            }

            if (!TryResolvePacketOwnedTutorActorScreenPoint(mapCenterX, mapCenterY, out Point anchor))
            {
                return;
            }

            IDXObject frame = ResolvePacketOwnedComboFrame(
                frames,
                currentTickCount,
                _packetOwnedTutorRuntime.ActiveMessageStartedAt == int.MinValue
                    ? currentTickCount
                    : _packetOwnedTutorRuntime.ActiveMessageStartedAt);
            if (frame?.Texture == null)
            {
                return;
            }

            Vector2 position = new(anchor.X - frame.X, anchor.Y - frame.Y);
            _spriteBatch.Draw(frame.Texture, position, Color.White);
        }

        private List<IDXObject> ResolvePacketOwnedTutorCueFrames(int cueIndex)
        {
            int normalizedIndex = Math.Max(0, cueIndex);
            if (_packetOwnedTutorCueFramesByIndex.TryGetValue(normalizedIndex, out List<IDXObject> cachedFrames))
            {
                return cachedFrames;
            }

            WzImage tutorialImage = Program.FindImage("UI", "tutorial.img");
            List<IDXObject> frames = LoadPacketOwnedAnimationFrames(
                ResolvePacketOwnedPropertyPath(tutorialImage, normalizedIndex.ToString(CultureInfo.InvariantCulture)),
                fallbackDelay: TutorRuntime.DefaultIndexedDurationMs);
            _packetOwnedTutorCueFramesByIndex[normalizedIndex] = frames;
            return frames;
        }

        private void DrawPacketOwnedTutorBalloon(Point anchor)
        {
            int requestedWidth = Math.Clamp(
                _packetOwnedTutorRuntime.ActiveMessageWidth <= 0 ? TutorRuntime.DefaultTextWidth : _packetOwnedTutorRuntime.ActiveMessageWidth,
                TutorRuntime.MinTextWidth,
                TutorRuntime.MaxTextWidth);
            PacketOwnedBalloonWrappedLine[] lines = WrapPacketOwnedBalloonText(_packetOwnedTutorRuntime.ActiveMessageText, requestedWidth);
            if (lines.Length == 0)
            {
                return;
            }

            Vector2 lineMeasure = MeasureChatTextWithFallback("Ay");
            int lineHeight = Math.Max(14, (int)Math.Ceiling(lineMeasure.Y));
            int bodyWidth = requestedWidth + PacketOwnedTutorBalloonBodyExtraWidth;
            int bodyHeight = (lines.Length * lineHeight) + (PacketOwnedTutorBalloonVerticalPadding * 2);
            Texture2D arrowTexture = _packetOwnedTutorBalloonSkin.Arrow?.Texture;
            int arrowWidth = arrowTexture?.Width ?? 0;
            int arrowHeight = arrowTexture?.Height ?? 0;

            Rectangle bodyBounds = new(
                anchor.X - (bodyWidth / 2),
                anchor.Y - bodyHeight - Math.Max(0, arrowHeight - PacketOwnedTutorBalloonArrowOverlap),
                bodyWidth,
                bodyHeight);
            Rectangle arrowBounds = arrowTexture == null
                ? Rectangle.Empty
                : new Rectangle(
                    anchor.X - (arrowWidth / 2),
                    bodyBounds.Bottom - PacketOwnedTutorBalloonArrowOverlap,
                    arrowWidth,
                    arrowHeight);
            Rectangle canvasBounds = Rectangle.Union(bodyBounds, arrowBounds == Rectangle.Empty ? bodyBounds : arrowBounds);
            Point canvasShift = ResolvePacketOwnedTutorCanvasShift(canvasBounds);
            if (canvasShift != Point.Zero)
            {
                bodyBounds.Offset(canvasShift);
                if (arrowBounds != Rectangle.Empty)
                {
                    arrowBounds.Offset(canvasShift);
                }
            }

            DrawPacketOwnedTutorBalloonNineSlice(bodyBounds);
            if (arrowTexture != null && arrowBounds != Rectangle.Empty)
            {
                _spriteBatch.Draw(arrowTexture, arrowBounds.Location.ToVector2(), Color.White);
            }

            float drawY = bodyBounds.Y + PacketOwnedTutorBalloonVerticalPadding;
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                PacketOwnedBalloonWrappedLine line = lines[lineIndex];
                if (!line.PreservesLineHeight && (line.Runs == null || line.Runs.Length == 0))
                {
                    drawY += lineHeight;
                    continue;
                }

                float drawX = bodyBounds.X + ((bodyBounds.Width - line.Width) / 2f);
                if (line.Runs != null)
                {
                    for (int runIndex = 0; runIndex < line.Runs.Length; runIndex++)
                    {
                        PacketOwnedBalloonTextRun run = line.Runs[runIndex];
                        DrawPacketOwnedBalloonRun(
                            run with { Style = new PacketOwnedBalloonTextStyle(run.Style.Color, run.Style.Emphasis) },
                            new Vector2(drawX, drawY),
                            1f);
                        drawX += MeasurePacketOwnedBalloonRun(run);
                    }
                }

                drawY += lineHeight;
            }
        }

        private bool TryResolvePacketOwnedTutorBalloonAnchorScreenPoint(int mapCenterX, int mapCenterY, out Point anchor)
        {
            if (!TryResolvePacketOwnedTutorActorScreenPoint(mapCenterX, mapCenterY, out anchor))
            {
                return false;
            }

            anchor = new Point(anchor.X, anchor.Y - _packetOwnedTutorRuntime.ResolveActorHeight() - PacketOwnedTutorBalloonAnchorOffsetY);
            return true;
        }

        private bool TryResolvePacketOwnedTutorActorScreenPoint(int mapCenterX, int mapCenterY, out Point anchor)
        {
            anchor = Point.Zero;

            if (!TryGetPacketOwnedTutorSummon(out ActiveSummon summon))
            {
                PlayerCharacter player = _playerManager?.Player;
                if (player == null)
                {
                    return false;
                }

                anchor = new Point(
                    (int)Math.Round(player.X - mapShiftX + mapCenterX),
                    (int)Math.Round(player.Y - mapShiftY + mapCenterY));
                return true;
            }

            anchor = new Point(
                (int)Math.Round(summon.PositionX - mapShiftX + mapCenterX),
                (int)Math.Round(summon.PositionY - mapShiftY + mapCenterY));
            return true;
        }

        private Point ResolvePacketOwnedTutorCanvasShift(Rectangle canvasBounds)
        {
            int minX = PacketOwnedTutorBalloonScreenMargin;
            int maxX = Math.Max(minX, Width - PacketOwnedTutorBalloonScreenMargin);
            int minY = PacketOwnedTutorBalloonScreenMargin;
            int maxY = Math.Max(minY, Height - PacketOwnedTutorBalloonScreenMargin);

            int shiftX = 0;
            if (canvasBounds.Left < minX)
            {
                shiftX = minX - canvasBounds.Left;
            }
            else if (canvasBounds.Right > maxX)
            {
                shiftX = maxX - canvasBounds.Right;
            }

            int shiftY = 0;
            if (canvasBounds.Top < minY)
            {
                shiftY = minY - canvasBounds.Top;
            }
            else if (canvasBounds.Bottom > maxY)
            {
                shiftY = maxY - canvasBounds.Bottom;
            }

            return shiftX == 0 && shiftY == 0
                ? Point.Zero
                : new Point(shiftX, shiftY);
        }

        private void DrawPacketOwnedTutorBalloonNineSlice(Rectangle bodyBounds)
        {
            Texture2D northWest = _packetOwnedTutorBalloonSkin.NorthWest;
            Texture2D northEast = _packetOwnedTutorBalloonSkin.NorthEast;
            Texture2D southWest = _packetOwnedTutorBalloonSkin.SouthWest;
            Texture2D southEast = _packetOwnedTutorBalloonSkin.SouthEast;
            Texture2D north = _packetOwnedTutorBalloonSkin.North;
            Texture2D south = _packetOwnedTutorBalloonSkin.South;
            Texture2D west = _packetOwnedTutorBalloonSkin.West;
            Texture2D east = _packetOwnedTutorBalloonSkin.East;
            Texture2D center = _packetOwnedTutorBalloonSkin.Center;
            if (northWest == null
                || northEast == null
                || southWest == null
                || southEast == null
                || north == null
                || south == null
                || west == null
                || east == null
                || center == null)
            {
                return;
            }

            Color tint = Color.White;
            int leftWidth = northWest.Width;
            int rightWidth = northEast.Width;
            int topHeight = northWest.Height;
            int bottomHeight = southWest.Height;
            int centerWidth = Math.Max(0, bodyBounds.Width - leftWidth - rightWidth);
            int centerHeight = Math.Max(0, bodyBounds.Height - topHeight - bottomHeight);

            _spriteBatch.Draw(center, new Rectangle(bodyBounds.X + leftWidth, bodyBounds.Y + topHeight, centerWidth, centerHeight), tint);
            _spriteBatch.Draw(northWest, new Vector2(bodyBounds.X, bodyBounds.Y), tint);
            _spriteBatch.Draw(northEast, new Vector2(bodyBounds.Right - rightWidth, bodyBounds.Y), tint);
            _spriteBatch.Draw(southWest, new Vector2(bodyBounds.X, bodyBounds.Bottom - bottomHeight), tint);
            _spriteBatch.Draw(southEast, new Vector2(bodyBounds.Right - rightWidth, bodyBounds.Bottom - bottomHeight), tint);

            if (centerWidth > 0)
            {
                _spriteBatch.Draw(north, new Rectangle(bodyBounds.X + leftWidth, bodyBounds.Y, centerWidth, north.Height), tint);
                _spriteBatch.Draw(south, new Rectangle(bodyBounds.X + leftWidth, bodyBounds.Bottom - south.Height, centerWidth, south.Height), tint);
            }

            if (centerHeight > 0)
            {
                _spriteBatch.Draw(west, new Rectangle(bodyBounds.X, bodyBounds.Y + topHeight, west.Width, centerHeight), tint);
                _spriteBatch.Draw(east, new Rectangle(bodyBounds.Right - east.Width, bodyBounds.Y + topHeight, east.Width, centerHeight), tint);
            }
        }

        private static string DescribePacketOwnedTutorVariant(int skillId)
        {
            return skillId switch
            {
                TutorRuntime.CygnusTutorSkillId => "Cygnus",
                TutorRuntime.AranTutorSkillId => "Aran",
                _ => $"skill {skillId}"
            };
        }

        private static string BuildPacketOwnedApspPromptBody(int contextToken, int eventType)
        {
            return PacketOwnedApspPromptExactBody;
        }

        private static string BuildPacketOwnedFollowPromptBody(string requesterName, int requesterId)
        {
            string displayName = string.IsNullOrWhiteSpace(requesterName)
                ? requesterId > 0 ? $"Character {requesterId}" : "Unknown character"
                : requesterName.Trim();
            return $"{displayName} asked to follow the local player. Press Yes to accept the request on the existing local follow seam or No to decline it.";
        }

        private void AcceptPacketOwnedFollowCharacterPrompt()
        {
            if (_localFollowRuntime.IncomingRequesterId <= 0)
            {
                HideLoginUtilityDialog();
                return;
            }

            if (!TryResolvePacketOwnedRemoteCharacterSnapshot(_localFollowRuntime.IncomingRequesterId, out LocalFollowUserSnapshot requester))
            {
                ShowUtilityFeedbackMessage("Incoming follow request could not be accepted because the requester is no longer available.");
                HideLoginUtilityDialog();
                return;
            }

            int localCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
            if (localCharacterId <= 0)
            {
                ShowUtilityFeedbackMessage("Incoming follow request could not be accepted because the local player is not fully initialized.");
                HideLoginUtilityDialog();
                return;
            }

            if (_remoteUserPool != null
                && !_remoteUserPool.TryApplyFollowCharacter(
                    requester.CharacterId,
                    localCharacterId,
                    transferField: false,
                    transferPosition: null,
                    localCharacterId,
                    _playerManager?.Player?.Position ?? Vector2.Zero,
                    out string followMessage))
            {
                ShowUtilityFeedbackMessage(followMessage);
                HideLoginUtilityDialog();
                return;
            }

            if (!_localFollowRuntime.TryAcceptIncomingRequest(requester, out string message))
            {
                if (_remoteUserPool != null)
                {
                    _remoteUserPool.TryApplyFollowCharacter(
                        requester.CharacterId,
                        0,
                        transferField: false,
                        transferPosition: null,
                        localCharacterId,
                        _playerManager?.Player?.Position ?? Vector2.Zero,
                        out _);
                }

                ShowUtilityFeedbackMessage(message);
                HideLoginUtilityDialog();
                return;
            }

            HideLoginUtilityDialog();
            ShowUtilityFeedbackMessage(message);
        }

        private void DeclinePacketOwnedFollowCharacterPrompt()
        {
            string message = TryResolvePacketOwnedRemoteCharacterSnapshot(_localFollowRuntime.IncomingRequesterId, out LocalFollowUserSnapshot requester)
                ? _localFollowRuntime.DeclineIncomingRequest(requester)
                : _localFollowRuntime.DeclineIncomingRequest(LocalFollowUserSnapshot.Missing(_localFollowRuntime.IncomingRequesterId));
            HideLoginUtilityDialog();
            ShowUtilityFeedbackMessage(message);
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
            _localFollowRuntime.ApplyFollowFailureText(_lastPacketOwnedFollowFailureMessage);
            _chat?.AddClientChatMessage($"[Error] {_lastPacketOwnedFollowFailureMessage}", Environment.TickCount, 15);
            return _lastPacketOwnedFollowFailureMessage;
        }

        private string ApplyPacketOwnedLocalFollowCharacter(RemoteUserFollowCharacterPacket packet)
        {
            int localCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
            if (localCharacterId <= 0)
            {
                return "Packet-owned follow-character payload could not be applied because the local player is not fully initialized.";
            }

            int previousDriverId = _localFollowRuntime.AttachedDriverId;
            if (packet.DriverId > 0)
            {
                TryResolvePacketOwnedRemoteCharacterSnapshot(packet.DriverId, out LocalFollowUserSnapshot driver);
                if (previousDriverId > 0 && previousDriverId != packet.DriverId)
                {
                    _remoteUserPool?.TryClearLocalPassengerFromDriver(previousDriverId, localCharacterId, out _);
                }

                string message = _localFollowRuntime.ApplyServerAttach(
                    driver.Exists
                        ? driver
                        : LocalFollowUserSnapshot.Missing(packet.DriverId, ResolvePacketOwnedRemoteCharacterName(packet.DriverId)),
                    currTickCount);
                _remoteUserPool?.TryAssignLocalPassengerToDriver(packet.DriverId, localCharacterId, out _);
                return message;
            }

            TryResolvePacketOwnedRemoteCharacterSnapshot(previousDriverId, out LocalFollowUserSnapshot previousDriver);
            LocalFollowApplyResult detachResult = _localFollowRuntime.ApplyServerDetach(
                previousDriver.Exists
                    ? previousDriver
                    : LocalFollowUserSnapshot.Missing(previousDriverId, ResolvePacketOwnedRemoteCharacterName(previousDriverId)),
                packet.TransferField,
                packet.TransferX.HasValue && packet.TransferY.HasValue
                    ? new Vector2(packet.TransferX.Value, packet.TransferY.Value)
                    : null);
            ApplyLocalFollowPlayerResult(detachResult);
            if (previousDriverId > 0)
            {
                _remoteUserPool?.TryClearLocalPassengerFromDriver(previousDriverId, localCharacterId, out _);
            }

            return _localFollowRuntime.LastStatusMessage;
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
            _localFollowRuntime.ApplyFollowFailure(info);

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

        private bool TryApplyPacketOwnedFollowCharacterPayload(byte[] payload, out string message)
        {
            int localCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
            if (localCharacterId <= 0)
            {
                message = "Packet-owned follow-character payload could not be applied because the local player is not fully initialized.";
                return false;
            }

            if (!RemoteUserPacketCodec.TryParseFollowCharacter(payload, out RemoteUserFollowCharacterPacket packet, out string error, localCharacterId))
            {
                message = error;
                return false;
            }

            message = ApplyPacketOwnedLocalFollowCharacter(packet);
            return true;
        }

        private readonly record struct PacketOwnedChatRoute(
            string Line,
            int ChatLogType,
            string WhisperTargetCandidate = null,
            int ChannelId = -1);

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
            int secondaryChannelId = -1;
            if ((mode == "channel" || mode == "type19" || mode == "ltype19" || mode == "19")
                && segments.Length >= 2
                && int.TryParse(segments[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedChannelId))
            {
                secondaryChannelId = parsedChannelId;
                primaryTarget = string.Empty;
            }

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

                case "gmwhisper":
                case "gmwhisperin":
                case "incominggmwhisper":
                    if (string.IsNullOrWhiteSpace(primaryTarget))
                    {
                        route = new PacketOwnedChatRoute($"[GM Whisper] {message}", 16);
                        return true;
                    }

                    route = new PacketOwnedChatRoute(
                        $"[GM Whisper] {primaryTarget}: {message}",
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
                    route = new PacketOwnedChatRoute(message, 19, null, secondaryChannelId);
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
            if (!TryPlayPacketOwnedWzSound(descriptor, minigame ? "MiniGame.img" : "Field.img", out string resolvedDescriptor, out string error))
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

                imageCandidates.Add("Field.img");
                imageCandidates.Add("UI.img");
                imageCandidates.Add("Game.img");
                imageCandidates.Add("MiniGame.img");
            }

            foreach (string imageName in imageCandidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                WzImageProperty resolved = ResolvePacketOwnedSoundProperty(imageName, propertyPath);
                if (resolved is WzBinaryProperty binaryProperty)
                {
                    soundProperty = binaryProperty;
                    resolvedDescriptor = $"{imageName[..^4]}/{propertyPath}";
                    return true;
                }
            }

            return false;
        }

        private static WzImageProperty ResolvePacketOwnedSoundProperty(string imageName, string propertyPath)
        {
            if (string.IsNullOrWhiteSpace(imageName) || string.IsNullOrWhiteSpace(propertyPath))
            {
                return null;
            }

            WzImage soundImage = Program.FindImage("Sound", imageName);
            if (soundImage == null)
            {
                return null;
            }

            string[] pathSegments = propertyPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (pathSegments.Length == 0)
            {
                return null;
            }

            WzImageProperty current = soundImage[pathSegments[0]];
            for (int i = 1; i < pathSegments.Length; i++)
            {
                current = current?[pathSegments[i]];
                if (current == null)
                {
                    return null;
                }
            }

            return WzInfoTools.GetRealProperty(current);
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

        private static int NormalizePacketOwnedCooldownSkillId(int skillId, out bool isVehicleSentinel)
        {
            isVehicleSentinel = skillId == PacketOwnedBattleshipCooldownSentinel;
            return isVehicleSentinel ? PacketOwnedBattleshipSkillId : skillId;
        }

        private static int NormalizePacketOwnedSkillId(int skillId)
        {
            return skillId == PacketOwnedLegacyVengeanceSkillId
                ? PacketOwnedCurrentVengeanceSkillId
                : skillId;
        }

        private void TryPlayPacketOwnedNoticeSound()
        {
            TryPlayPacketOwnedWzSound("UI/DlgNotice", "UI.img", out _, out _);
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

        private bool TryApplyPacketOwnedTutorHirePayload(byte[] payload, out string message)
        {
            message = "Hire-tutor payload is missing.";
            if (payload == null || payload.Length == 0)
            {
                return false;
            }

            message = ApplyPacketOwnedTutorHire(payload[0] != 0);
            return true;
        }

        private bool TryApplyPacketOwnedTutorMessagePayload(byte[] payload, out string message)
        {
            message = "Tutor message payload is missing.";
            if (payload == null || payload.Length < 1)
            {
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                bool indexedPayload = reader.ReadByte() != 0;
                if (indexedPayload)
                {
                    if (reader.BaseStream.Length - reader.BaseStream.Position < sizeof(int) * 2)
                    {
                        message = "Tutor indexed payload must contain two Int32 values.";
                        return false;
                    }

                    message = ApplyPacketOwnedTutorIndexedMessage(reader.ReadInt32(), reader.ReadInt32());
                    return true;
                }

                string text = ReadPacketOwnedMapleString(reader);
                int width = reader.ReadInt32();
                int durationMs = reader.ReadInt32();
                message = ApplyPacketOwnedTutorTextMessage(text, width, durationMs);
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                message = $"Tutor message payload could not be decoded: {ex.Message}";
                return false;
            }
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

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                ushort chatLogType = reader.ReadUInt16();
                int channelId = reader.ReadInt32();
                string chatText = ReadPacketOwnedMapleString(reader);
                if (reader.BaseStream.Position == reader.BaseStream.Length && !string.IsNullOrWhiteSpace(chatText))
                {
                    message = ApplyPacketOwnedChatMessage(chatText, chatLogType, channelId: channelId);
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

        private bool TryApplyPacketOwnedTimeBombAttackPayload(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length < (sizeof(int) * 5))
            {
                message = "Time Bomb payload must contain skillId, action, hitPeriodMs, impactPercent, and damage Int32 values.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                int skillId = reader.ReadInt32();
                int action = reader.ReadInt32();
                int hitPeriodMs = reader.ReadInt32();
                int impactPercent = reader.ReadInt32();
                int damage = reader.BaseStream.Position <= reader.BaseStream.Length - sizeof(int)
                    ? reader.ReadInt32()
                    : 0;
                message = ApplyPacketOwnedTimeBombAttack(skillId, action, hitPeriodMs, impactPercent, damage);
                return true;
            }
            catch (Exception ex)
            {
                message = $"Time Bomb payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private bool TryApplyPacketOwnedVengeanceSkillApplyPayload(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length < sizeof(int))
            {
                message = "Vengeance payload must contain the applied skill id Int32 value.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                message = ApplyPacketOwnedVengeanceSkillApply(reader.ReadInt32());
                return true;
            }
            catch (Exception ex)
            {
                message = $"Vengeance payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private bool TryApplyPacketOwnedExJablinApplyPayload(byte[] payload, out string message)
        {
            message = ApplyPacketOwnedExJablinApply();
            return true;
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
                return ChatCommandHandler.CommandResult.Info($"{DescribeLocalUtilityPacketInboxStatus()} {DescribePacketOwnedUtilityDispatchStatus(currentTickCount)} {_localUtilityPacketInbox.LastStatus} {DescribeLocalUtilityOfficialSessionBridgeStatus()}");
            }

            switch (args[0].ToLowerInvariant())
            {
                case "status":
                    return ChatCommandHandler.CommandResult.Info($"{DescribeLocalUtilityPacketInboxStatus()} {DescribePacketOwnedUtilityDispatchStatus(currentTickCount)} {_localUtilityPacketInbox.LastStatus} {DescribeLocalUtilityOfficialSessionBridgeStatus()}");

                case "inbox":
                    return HandlePacketOwnedUtilityInboxCommand(args);

                case "directionmode":
                    if (args.Length < 2 || !TryParsePacketOwnedBooleanToken(args[1], out bool directionModeEnabled))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility directionmode <on|off|1|0> [delayMs]");
                    }

                    int directionModeDelay = 0;
                    if (args.Length >= 3 && !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out directionModeDelay))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility directionmode <on|off|1|0> [delayMs]");
                    }

                    return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedDirectionMode(directionModeEnabled, directionModeDelay));

                case "standalone":
                case "standalonemode":
                    if (args.Length < 2 || !TryParsePacketOwnedBooleanToken(args[1], out bool standAloneEnabled))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility standalone <on|off|1|0>");
                    }

                    return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedStandAloneMode(standAloneEnabled));

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

                case "skillguide":
                    return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedSkillGuideLaunch());

                case "antimacro":
                    return HandlePacketOwnedAntiMacroCommand(args.Skip(1).ToArray());

                case "apsp":
                    if (args.Length >= 2)
                    {
                        switch (args[1].ToLowerInvariant())
                        {
                            case "status":
                                return ChatCommandHandler.CommandResult.Info(DescribePacketOwnedApspContextStatus());

                            case "seed":
                                int? apspSeedCharacterId = null;
                                int parsedApspSeedCharacterId = 0;
                                if (args.Length >= 3
                                    && !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedApspSeedCharacterId))
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /localutility apsp [status|seed [characterId]|receive <token>|send <token>|context <receiveToken> [sendToken]|<contextToken> <11|12|13>|text]");
                                }

                                if (args.Length >= 3)
                                {
                                    apspSeedCharacterId = parsedApspSeedCharacterId;
                                }

                                return ChatCommandHandler.CommandResult.Ok(
                                    SeedPacketOwnedApspContextTokens(apspSeedCharacterId));

                            case "receive":
                                if (args.Length < 3
                                    || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int apspReceiveToken)
                                    || apspReceiveToken <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /localutility apsp [status|seed [characterId]|receive <token>|send <token>|context <receiveToken> [sendToken]|<contextToken> <11|12|13>|text]");
                                }

                                return ChatCommandHandler.CommandResult.Ok(OverridePacketOwnedApspReceiveContextToken(apspReceiveToken));

                            case "send":
                                if (args.Length < 3
                                    || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int apspSendToken)
                                    || apspSendToken <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /localutility apsp [status|seed [characterId]|receive <token>|send <token>|context <receiveToken> [sendToken]|<contextToken> <11|12|13>|text]");
                                }

                                return ChatCommandHandler.CommandResult.Ok(OverridePacketOwnedApspSendContextToken(apspSendToken));

                            case "context":
                                if (args.Length < 3
                                    || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int apspReceiveContextToken)
                                    || apspReceiveContextToken <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /localutility apsp [status|seed [characterId]|receive <token>|send <token>|context <receiveToken> [sendToken]|<contextToken> <11|12|13>|text]");
                                }

                                int? apspSendContextToken = null;
                                int parsedApspSendContextToken = 0;
                                if (args.Length >= 4
                                    && (!int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedApspSendContextToken)
                                        || parsedApspSendContextToken <= 0))
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /localutility apsp [status|seed [characterId]|receive <token>|send <token>|context <receiveToken> [sendToken]|<contextToken> <11|12|13>|text]");
                                }

                                if (args.Length >= 4)
                                {
                                    apspSendContextToken = parsedApspSendContextToken;
                                }

                                return ChatCommandHandler.CommandResult.Ok(
                                    OverridePacketOwnedApspContextTokens(
                                        apspReceiveContextToken,
                                        apspSendContextToken));
                        }
                    }

                    if (args.Length >= 3
                        && int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int apspContextToken)
                        && int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int apspEventType))
                    {
                        return TryApplyPacketOwnedAskApspEvent(apspContextToken, apspEventType, out string structuredApspMessage)
                            ? ChatCommandHandler.CommandResult.Ok(structuredApspMessage)
                            : ChatCommandHandler.CommandResult.Error(structuredApspMessage);
                    }

                    return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedAskApspEvent(args.Length >= 2 ? string.Join(" ", args.Skip(1)) : null));

                case "follow":
                    return HandlePacketOwnedFollowCommand(args.Skip(1).ToArray());

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
                case "packetclientraw":
                    return HandlePacketOwnedUtilityClientPacketRawCommand(args);
                case "session":
                    return HandlePacketOwnedUtilitySessionCommand(args.Skip(1).ToArray());

                default:
                    return ChatCommandHandler.CommandResult.Error(
                        "Usage: /localutility [status|inbox [status|start [port]|stop|packet <sitresult|questresult|openui|openuiwithoption|commodity|notice|chat|buffzone|eventsound|minigamesound|skillguide|antimacro|apspevent|follow|followfail|directionmode|standalone|damagemeter|timebomb|vengeance|exjablin|hpdec|skillcooltime|193|231|242|243|246|247|250|251|252|262|263|264|265|266|267|268|270|271|272|273|274|275|276|1011|1012|1013|1014|classcompetition|questguide|deliveryquest> [payloadhex=..|payloadb64=..]|packetraw <type> [hex]|packetclientraw <hex>]|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]|directionmode <on|off|1|0> [delayMs]|standalone <on|off|1|0>|openui <uiType> [defaultTab]|openuiwithoption <uiType> <option>|commodity <serialNumber>|notice <text>|chat [channel|type19|whisper:name|whisperout:name] <text>|buffzone [text]|eventsound <image/path or path>|minigamesound <image/path or path>|questguide <questId> <mobId:mapId[,mapId...]>...|questguide clear|delivery <questId> <itemId> [blockedQuestIdsCsv]|classcompetition|skillguide|antimacro [status|launch <normal|admin> [first|retry]|notice <noticeType> [antiMacroType]|result <mode> [antiMacroType] [userName]|clear]|apsp [status|seed [characterId]|receive <token>|send <token>|context <receiveToken> [sendToken]|<contextToken> <11|12|13>|text]|follow <status|request <driverId|name> [auto|manual] [keyinput]|ask <requesterId|name>|accept|decline|attach <driverId|name>|detach [transferX transferY]|passengerdetach [requesterId|name] [transferX transferY]>|followfail [reasonCode [driverId]|text]|packet <sitresult|questresult|openui|openuiwithoption|commodity|fade|balloon|damagemeter|timebomb|vengeance|exjablin|hpdec|notice|chat|buffzone|eventsound|minigamesound|questguide|delivery|classcompetition|skillguide|hiretutor|tutormsg|antimacro|apspevent|directionmode|standalone|follow|followfail|193|231|242|243|250|251|252|255|256|262|263|264|265|266|267|268|270|271|272|273|274|275|276|1011|1012|1013|1014> [payloadhex=..|payloadb64=..]|packetraw <type> <hex>|packetclientraw <hex>]");
            }
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedUtilityInboxCommand(string[] args)
        {
            int offset = args.Length > 0 && string.Equals(args[0], "inbox", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            string usagePrefix = offset == 0 ? "/localutilitypacket" : "/localutility inbox";
            int currentTickCount = Environment.TickCount;

            if (args.Length <= offset || string.Equals(args[offset], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info($"{DescribeLocalUtilityPacketInboxStatus()} {DescribePacketOwnedUtilityDispatchStatus(currentTickCount)} {_localUtilityPacketInbox.LastStatus}");
            }

            if (string.Equals(args[offset], "start", StringComparison.OrdinalIgnoreCase))
            {
                int port = LocalUtilityPacketInboxManager.DefaultPort;
                if (args.Length > offset + 1 && (!int.TryParse(args[offset + 1], out port) || port <= 0 || port > ushort.MaxValue))
                {
                    return ChatCommandHandler.CommandResult.Error($"Usage: {usagePrefix} [status|start [port]|stop|packet <sitresult|questresult|openui|openuiwithoption|commodity|notice|chat|buffzone|eventsound|minigamesound|radio|skillguide|antimacro|apspevent|follow|followfail|directionmode|standalone|damagemeter|timebomb|vengeance|exjablin|hpdec|skillcooltime|193|231|242|243|246|247|250|251|252|261|262|263|264|265|266|267|268|270|271|272|273|274|275|276|1011|1012|1013|1014|classcompetition|questguide|deliveryquest> [payloadhex=..|payloadb64=..]|packetraw <type> [hex]|packetclientraw <hex>]");
                }

                _localUtilityPacketInboxConfiguredPort = port;
                _localUtilityPacketInboxEnabled = true;
                EnsureLocalUtilityPacketInboxState(shouldRun: true);
                return ChatCommandHandler.CommandResult.Ok($"{DescribeLocalUtilityPacketInboxStatus()} {DescribePacketOwnedUtilityDispatchStatus(currentTickCount)} {_localUtilityPacketInbox.LastStatus}");
            }

            if (string.Equals(args[offset], "stop", StringComparison.OrdinalIgnoreCase))
            {
                _localUtilityPacketInboxEnabled = false;
                EnsureLocalUtilityPacketInboxState(shouldRun: false);
                return ChatCommandHandler.CommandResult.Ok($"{DescribeLocalUtilityPacketInboxStatus()} {DescribePacketOwnedUtilityDispatchStatus(currentTickCount)} {_localUtilityPacketInbox.LastStatus}");
            }

            if (string.Equals(args[offset], "packet", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[offset], "packetraw", StringComparison.OrdinalIgnoreCase))
            {
                string[] packetArgs = args.Skip(offset).ToArray();
                return HandlePacketOwnedUtilityPacketCommand(
                    packetArgs,
                    rawHex: string.Equals(packetArgs[0], "packetraw", StringComparison.OrdinalIgnoreCase));
            }

            if (string.Equals(args[offset], "packetclientraw", StringComparison.OrdinalIgnoreCase))
            {
                return HandlePacketOwnedUtilityClientPacketRawCommand(args.Skip(offset).ToArray());
            }

            return ChatCommandHandler.CommandResult.Error(
                $"Usage: {usagePrefix} [status|start [port]|stop|packet <sitresult|questresult|openui|openuiwithoption|commodity|notice|chat|buffzone|eventsound|minigamesound|radio|skillguide|antimacro|apspevent|follow|followfail|directionmode|standalone|damagemeter|timebomb|vengeance|exjablin|hpdec|skillcooltime|193|231|242|243|246|247|250|251|252|261|262|263|264|265|266|267|268|270|271|272|273|274|275|276|1011|1012|1013|1014|classcompetition|questguide|deliveryquest> [payloadhex=..|payloadb64=..]|packetraw <type> [hex]|packetclientraw <hex>]");
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
                case "timebomb":
                    applied = TryApplyPacketOwnedTimeBombAttackPayload(payload, out message);
                    break;
                case "vengeance":
                    applied = TryApplyPacketOwnedVengeanceSkillApplyPayload(payload, out message);
                    break;
                case "exjablin":
                    applied = TryApplyPacketOwnedExJablinApplyPayload(payload, out message);
                    break;
                case "hpdec":
                    applied = TryApplyPacketOwnedFieldHazardPayload(payload, out message);
                    break;
                case "notice":
                    applied = TryApplyPacketOwnedNoticePayload(payload, out message);
                    break;
                case "chat":
                    applied = TryApplyPacketOwnedChatPayload(payload, out message);
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
                case "questresult":
                    applied = TryApplyPacketOwnedQuestResultPayload(payload, out message);
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
                case "skillguide":
                    message = ApplyPacketOwnedSkillGuideLaunch();
                    applied = true;
                    break;
                case "hiretutor":
                case "tutorhire":
                    applied = TryApplyPacketOwnedTutorHirePayload(payload, out message);
                    break;
                case "tutormsg":
                    applied = TryApplyPacketOwnedTutorMessagePayload(payload, out message);
                    break;
                case "antimacro":
                    applied = TryApplyPacketOwnedAntiMacroPayload(payload, out message);
                    break;
                case "apspevent":
                    applied = TryApplyPacketOwnedAskApspEventPayload(payload, out message);
                    break;
                case "directionmode":
                case "setdirectionmode":
                    applied = TryApplyPacketOwnedDirectionModePayload(payload, out message);
                    break;
                case "standalone":
                case "standalonemode":
                case "setstandalonemode":
                    applied = TryApplyPacketOwnedStandAloneModePayload(payload, out message);
                    break;
                case "follow":
                case "followcharacter":
                    applied = TryApplyPacketOwnedFollowCharacterPayload(payload, out message);
                    break;
                case "followfail":
                    applied = TryApplyPacketOwnedFollowCharacterFailedPayload(payload, out message);
                    break;
                default:
                    return ChatCommandHandler.CommandResult.Error(
                        rawHex
                        ? "Usage: /localutility packetraw <sitresult|questresult|openui|openuiwithoption|commodity|fade|balloon|damagemeter|timebomb|vengeance|exjablin|hpdec|notice|chat|buffzone|eventsound|minigamesound|radio|questguide|delivery|classcompetition|skillguide|hiretutor|tutormsg|antimacro|apspevent|directionmode|standalone|follow|followfail|skillcooltime|193|231|242|243|246|247|250|251|252|255|256|261|262|263|264|265|266|267|268|270|271|272|273|274|275|276|1011|1012|1013|1014> <hex>"
                        : "Usage: /localutility packet <sitresult|questresult|openui|openuiwithoption|commodity|fade|balloon|damagemeter|timebomb|vengeance|exjablin|hpdec|notice|chat|buffzone|eventsound|minigamesound|radio|questguide|delivery|classcompetition|skillguide|hiretutor|tutormsg|antimacro|apspevent|directionmode|standalone|follow|followfail|skillcooltime|193|231|242|243|246|247|250|251|252|255|256|261|262|263|264|265|266|267|268|270|271|272|273|274|275|276|1011|1012|1013|1014> [payloadhex=..|payloadb64=..]");
            }

            return applied
                ? ChatCommandHandler.CommandResult.Ok(message)
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedUtilityClientPacketRawCommand(string[] args)
        {
            if (args.Length < 2 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(1)), out byte[] rawPacket))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /localutility packetclientraw <hex>");
            }

            if (!LocalUtilityPacketInboxManager.TryDecodeOpcodeFramedPacket(rawPacket, out int packetType, out byte[] payload, out string decodeError))
            {
                return ChatCommandHandler.CommandResult.Error(decodeError ?? "Usage: /localutility packetclientraw <hex>");
            }

            bool applied = TryApplyPacketOwnedUtilityPacket(packetType, payload, out string message);
            return applied
                ? ChatCommandHandler.CommandResult.Ok($"Applied local utility client opcode {packetType}. {message}")
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedUtilitySessionCommand(string[] args)
        {
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribeLocalUtilityOfficialSessionBridgeStatus());
            }

            if (string.Equals(args[0], "discover", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2
                    || !int.TryParse(args[1], out int discoverRemotePort)
                    || discoverRemotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /localutility session discover <remotePort> [processName|pid] [localPort]");
                }

                string processSelector = args.Length >= 3 ? args[2] : null;
                int? localPortFilter = null;
                if (args.Length >= 4)
                {
                    if (!int.TryParse(args[3], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility session discover <remotePort> [processName|pid] [localPort]");
                    }

                    localPortFilter = parsedLocalPort;
                }

                return ChatCommandHandler.CommandResult.Info(
                    _localUtilityOfficialSessionBridge.DescribeDiscoveredSessions(discoverRemotePort, processSelector, localPortFilter));
            }

            if (string.Equals(args[0], "start", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 4
                    || !int.TryParse(args[1], out int listenPort)
                    || listenPort <= 0
                    || !int.TryParse(args[3], out int remotePort)
                    || remotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /localutility session start <listenPort> <serverHost> <serverPort>");
                }

                _localUtilityOfficialSessionBridge.Start(listenPort, args[2], remotePort);
                return ChatCommandHandler.CommandResult.Ok(_localUtilityOfficialSessionBridge.LastStatus);
            }

            if (string.Equals(args[0], "startauto", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3
                    || !int.TryParse(args[1], out int autoListenPort)
                    || autoListenPort <= 0
                    || !int.TryParse(args[2], out int autoRemotePort)
                    || autoRemotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /localutility session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                }

                string processSelector = args.Length >= 4 ? args[3] : null;
                int? localPortFilter = null;
                if (args.Length >= 5)
                {
                    if (!int.TryParse(args[4], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                    }

                    localPortFilter = parsedLocalPort;
                }

                return _localUtilityOfficialSessionBridge.TryStartFromDiscovery(autoListenPort, autoRemotePort, processSelector, localPortFilter, out string startStatus)
                    ? ChatCommandHandler.CommandResult.Ok(startStatus)
                    : ChatCommandHandler.CommandResult.Error(startStatus);
            }

            if (string.Equals(args[0], "stop", StringComparison.OrdinalIgnoreCase))
            {
                _localUtilityOfficialSessionBridge.Stop();
                return ChatCommandHandler.CommandResult.Ok(_localUtilityOfficialSessionBridge.LastStatus);
            }

            return ChatCommandHandler.CommandResult.Error("Usage: /localutility session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]");
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFollowCommand(string[] args)
        {
            if (args == null || args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(_localFollowRuntime.DescribeStatus(ResolvePacketOwnedRemoteCharacterName));
            }

            switch (args[0].ToLowerInvariant())
            {
                case "request":
                    if (args.Length < 2 || !TryResolvePacketOwnedRemoteCharacterToken(args[1], out int requestedDriverId, out _))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility follow request <driverId|name> [auto|manual] [keyinput]");
                    }

                    bool autoRequest = args.Skip(2).Any(token => string.Equals(token, "auto", StringComparison.OrdinalIgnoreCase));
                    bool keyInput = args.Skip(2).Any(token => string.Equals(token, "keyinput", StringComparison.OrdinalIgnoreCase) || string.Equals(token, "key", StringComparison.OrdinalIgnoreCase));
                    if (!TryResolvePacketOwnedLocalFollowSnapshot(out LocalFollowUserSnapshot localUser)
                        || !TryResolvePacketOwnedRemoteCharacterSnapshot(requestedDriverId, out LocalFollowUserSnapshot requestedDriver))
                    {
                        return ChatCommandHandler.CommandResult.Error("Follow request could not be issued because the local player or target driver is unavailable.");
                    }

                    StampPacketOwnedUtilityRequestState();
                    return _localFollowRuntime.TrySendOutgoingRequest(localUser, requestedDriver, currTickCount, autoRequest, keyInput, out string requestMessage)
                        ? ChatCommandHandler.CommandResult.Ok(requestMessage)
                        : ChatCommandHandler.CommandResult.Error(requestMessage);

                case "ask":
                    if (args.Length < 2 || !TryResolvePacketOwnedRemoteCharacterToken(args[1], out int requesterId, out _)
                        || !TryResolvePacketOwnedRemoteCharacterSnapshot(requesterId, out LocalFollowUserSnapshot requester))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility follow ask <requesterId|name>");
                    }

                    StampPacketOwnedUtilityRequestState();
                    if (!_localFollowRuntime.TryQueueIncomingRequest(requester, out string askMessage))
                    {
                        return ChatCommandHandler.CommandResult.Error(askMessage);
                    }

                    ShowLoginUtilityDialog(
                        "Follow Request",
                        BuildPacketOwnedFollowPromptBody(requester.Name, requester.CharacterId),
                        LoginUtilityDialogButtonLayout.YesNo,
                        LoginUtilityDialogAction.ConfirmFollowCharacterRequest);
                    return ChatCommandHandler.CommandResult.Ok(askMessage);

                case "accept":
                    AcceptPacketOwnedFollowCharacterPrompt();
                    return ChatCommandHandler.CommandResult.Ok(_localFollowRuntime.DescribeStatus(ResolvePacketOwnedRemoteCharacterName));

                case "decline":
                    DeclinePacketOwnedFollowCharacterPrompt();
                    return ChatCommandHandler.CommandResult.Ok(_localFollowRuntime.DescribeStatus(ResolvePacketOwnedRemoteCharacterName));

                case "attach":
                    if (args.Length < 2 || !TryResolvePacketOwnedRemoteCharacterToken(args[1], out int attachDriverId, out _)
                        || !TryResolvePacketOwnedRemoteCharacterSnapshot(attachDriverId, out LocalFollowUserSnapshot attachDriver))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility follow attach <driverId|name>");
                    }

                    int localAttachCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
                    if (localAttachCharacterId <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Local follow attach requires an initialized local player.");
                    }

                    StampPacketOwnedUtilityRequestState();
                    int previousAttachedDriverId = _localFollowRuntime.AttachedDriverId;
                    string attachMessage = _localFollowRuntime.ApplyServerAttach(attachDriver, currTickCount);
                    if (previousAttachedDriverId > 0 && previousAttachedDriverId != attachDriverId)
                    {
                        _remoteUserPool?.TryClearLocalPassengerFromDriver(previousAttachedDriverId, localAttachCharacterId, out _);
                    }

                    _remoteUserPool?.TryAssignLocalPassengerToDriver(attachDriverId, localAttachCharacterId, out _);
                    return ChatCommandHandler.CommandResult.Ok(attachMessage);

                case "detach":
                {
                    if (_localFollowRuntime.AttachedDriverId <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("No local follow driver is currently attached.");
                    }

                    Vector2? detachTransferPosition = null;
                    bool detachTransferField = false;
                    if (args.Length >= 3
                        && float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float transferX)
                        && float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float transferY))
                    {
                        detachTransferField = true;
                        detachTransferPosition = new Vector2(transferX, transferY);
                    }
                    else if (args.Length != 1)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility follow detach [transferX transferY]");
                    }

                    TryResolvePacketOwnedRemoteCharacterSnapshot(_localFollowRuntime.AttachedDriverId, out LocalFollowUserSnapshot previousDriver);
                    int localDetachCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
                    StampPacketOwnedUtilityRequestState();
                    LocalFollowApplyResult detachResult = _localFollowRuntime.ApplyServerDetach(previousDriver, detachTransferField, detachTransferPosition);
                    ApplyLocalFollowPlayerResult(detachResult);
                    if (localDetachCharacterId > 0 && previousDriver.CharacterId > 0)
                    {
                        _remoteUserPool?.TryClearLocalPassengerFromDriver(previousDriver.CharacterId, localDetachCharacterId, out _);
                    }

                    return ChatCommandHandler.CommandResult.Ok(_localFollowRuntime.LastStatusMessage);
                }

                case "passengerdetach":
                {
                    int passengerId = _localFollowRuntime.AttachedPassengerId;
                    int coordinateStartIndex = 1;
                    if (args.Length >= 2 && TryResolvePacketOwnedRemoteCharacterToken(args[1], out int explicitPassengerId, out _))
                    {
                        passengerId = explicitPassengerId;
                        coordinateStartIndex = 2;
                    }

                    if (passengerId <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility follow passengerdetach [requesterId|name] [transferX transferY]");
                    }

                    Vector2? passengerTransferPosition = null;
                    bool passengerTransferField = false;
                    if (args.Length >= coordinateStartIndex + 2
                        && float.TryParse(args[coordinateStartIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out float passengerTransferX)
                        && float.TryParse(args[coordinateStartIndex + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float passengerTransferY))
                    {
                        passengerTransferField = true;
                        passengerTransferPosition = new Vector2(passengerTransferX, passengerTransferY);
                    }
                    else if (args.Length != coordinateStartIndex)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility follow passengerdetach [requesterId|name] [transferX transferY]");
                    }

                    int localCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
                    if (localCharacterId <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Passenger detach requires an initialized local player.");
                    }

                    TryResolvePacketOwnedRemoteCharacterSnapshot(passengerId, out LocalFollowUserSnapshot passenger);
                    _remoteUserPool?.TryApplyFollowCharacter(
                        passengerId,
                        0,
                        passengerTransferField,
                        passengerTransferPosition,
                        localCharacterId,
                        _playerManager?.Player?.Position ?? Vector2.Zero,
                        out _);
                    StampPacketOwnedUtilityRequestState();
                    return ChatCommandHandler.CommandResult.Ok(_localFollowRuntime.ClearAttachedPassenger(passenger, passengerTransferField, passengerTransferPosition));
                }

                default:
                    return ChatCommandHandler.CommandResult.Error("Usage: /localutility follow <status|request <driverId|name> [auto|manual] [keyinput]|ask <requesterId|name>|accept|decline|attach <driverId|name>|detach [transferX transferY]|passengerdetach [requesterId|name] [transferX transferY]>");
            }
        }

        private bool TryResolvePacketOwnedLocalFollowSnapshot(out LocalFollowUserSnapshot snapshot)
        {
            snapshot = LocalFollowUserSnapshot.Missing(_playerManager?.Player?.Build?.Id ?? 0, _playerManager?.Player?.Build?.Name);
            PlayerCharacter player = _playerManager?.Player;
            if (player?.Physics == null)
            {
                return false;
            }

            bool isMounted = player.Build?.HasMonsterRiding == true
                || player.Build?.Equipment?.TryGetValue(EquipSlot.TamingMob, out CharacterPart mountPart) == true && mountPart != null;
            bool isImmovable = player.State == PlayerState.Sitting
                || player.IsMovementLockedBySkillTransform
                || player.GmFlyMode
                || player.Physics.IsOnLadderOrRope
                || player.Physics.IsUserFlying()
                || player.Physics.IsInSwimArea;
            bool isGhost = string.Equals(player.CurrentActionName, CharacterPart.GetActionString(CharacterAction.Ghost), StringComparison.OrdinalIgnoreCase);
            snapshot = new LocalFollowUserSnapshot(
                player.Build?.Id ?? 0,
                player.Build?.Name,
                Exists: true,
                IsAlive: player.IsAlive,
                IsImmovable: isImmovable,
                IsMounted: isMounted,
                HasMorphTemplate: player.HasActiveMorphTransform,
                IsGhostAction: isGhost,
                Position: player.Position,
                FacingRight: player.FacingRight);
            return true;
        }

        private bool TryResolvePacketOwnedRemoteCharacterToken(string token, out int characterId, out string resolvedName)
        {
            characterId = 0;
            resolvedName = null;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out characterId))
            {
                resolvedName = ResolvePacketOwnedRemoteCharacterName(characterId);
                return characterId > 0;
            }

            if (_remoteUserPool != null && _remoteUserPool.TryGetActorByName(token.Trim(), out var actor))
            {
                characterId = actor.CharacterId;
                resolvedName = actor.Name;
                return true;
            }

            return false;
        }

        private bool TryResolvePacketOwnedRemoteCharacterSnapshot(int characterId, out LocalFollowUserSnapshot snapshot)
        {
            snapshot = LocalFollowUserSnapshot.Missing(characterId, ResolvePacketOwnedRemoteCharacterName(characterId));
            if (characterId <= 0 || _remoteUserPool == null || !_remoteUserPool.TryGetActor(characterId, out var actor))
            {
                return false;
            }

            bool isMounted = actor.Build?.HasMonsterRiding == true
                || actor.Build?.Equipment?.TryGetValue(EquipSlot.TamingMob, out CharacterPart mountPart) == true && mountPart != null;
            bool hasMorphTemplate = actor.HasMorphTemplate
                || actor.Build?.Body?.Type == CharacterPartType.Morph;
            bool isGhost = string.Equals(actor.ActionName, CharacterPart.GetActionString(CharacterAction.Ghost), StringComparison.OrdinalIgnoreCase);
            snapshot = new LocalFollowUserSnapshot(
                actor.CharacterId,
                actor.Name,
                Exists: true,
                IsAlive: true,
                IsImmovable: false,
                IsMounted: isMounted,
                HasMorphTemplate: hasMorphTemplate,
                IsGhostAction: isGhost,
                Position: actor.Position,
                FacingRight: actor.FacingRight);
            return true;
        }

        private void HandlePacketOwnedRemoteActorRemoved(int characterId, string removedName)
        {
            if (characterId <= 0)
            {
                return;
            }

            bool shouldDismissFollowPrompt =
                _loginUtilityDialogAction == LoginUtilityDialogAction.ConfirmFollowCharacterRequest
                && _localFollowRuntime.IncomingRequesterId == characterId;
            Func<int, string> nameResolver = removedId =>
                removedId == characterId ? removedName : ResolvePacketOwnedRemoteCharacterName(removedId);
            if (!_localFollowRuntime.TryClearMissingRemoteCharacter(characterId, nameResolver, out string message))
            {
                return;
            }

            StampPacketOwnedUtilityRequestState();
            if (shouldDismissFollowPrompt)
            {
                HideLoginUtilityDialog();
            }

            ShowUtilityFeedbackMessage(message);
        }

        private void ApplyLocalFollowPlayerResult(LocalFollowApplyResult result)
        {
            PlayerCharacter player = _playerManager?.Player;
            if (player == null)
            {
                return;
            }

            if (result.PlayerPositionChanged)
            {
                player.SetPosition(result.PlayerPosition.X, result.PlayerPosition.Y);
            }

            if (result.PlayerFacingRightChanged)
            {
                player.FacingRight = result.PlayerFacingRight;
                player.Physics.FacingRight = result.PlayerFacingRight;
            }
        }

        private void SyncPacketOwnedLocalFollowCharacter()
        {
            if (_localFollowRuntime.AttachedDriverId <= 0
                || _playerManager?.Player == null
                || !TryResolvePacketOwnedRemoteCharacterSnapshot(_localFollowRuntime.AttachedDriverId, out LocalFollowUserSnapshot driver))
            {
                return;
            }

            ApplyLocalFollowPlayerResult(new LocalFollowApplyResult(
                PlayerPositionChanged: true,
                PlayerPosition: driver.Position,
                PlayerFacingRightChanged: true,
                PlayerFacingRight: driver.FacingRight));
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
