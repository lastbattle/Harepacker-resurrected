using HaCreator.MapSimulator.UI;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly Dictionary<int, HashSet<int>> _packetQuestGuideTargetsByMobId = new();
        private int _packetQuestGuideQuestId;
        private bool _packetOwnedUtilityRequestSent;
        private int _packetOwnedUtilityRequestTick = int.MinValue;
        private int _lastDeliveryQuestId;
        private int _lastDeliveryItemId;
        private readonly List<int> _lastDeliveryDisallowedQuestIds = new();
        private int _lastClassCompetitionOpenTick = int.MinValue;

        private static readonly string[] UniqueModelessUtilityWindowNames =
        {
            MapSimulatorWindowNames.CashShop,
            MapSimulatorWindowNames.Mts,
            MapSimulatorWindowNames.MapTransfer,
            MapSimulatorWindowNames.ItemMaker,
            MapSimulatorWindowNames.ItemUpgrade,
            MapSimulatorWindowNames.VegaSpell,
            MapSimulatorWindowNames.Trunk,
            MapSimulatorWindowNames.CharacterInfo,
            MapSimulatorWindowNames.SocialList,
            MapSimulatorWindowNames.GuildSearch,
            MapSimulatorWindowNames.GuildSkill,
            MapSimulatorWindowNames.GuildBbs,
            MapSimulatorWindowNames.Messenger,
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

            questDeliveryWindow.Configure(_lastDeliveryQuestId, _lastDeliveryItemId, _lastDeliveryDisallowedQuestIds, _packetOwnedUtilityRequestTick);
            questDeliveryWindow.Show();
            uiWindowManager.BringToFront(questDeliveryWindow);
            return $"Opened packet-authored quest delivery for {itemName}.";
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

            window.Show();
            uiWindowManager.BringToFront(window);
            return "Opened packet-authored Class Competition page placeholder.";
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
                MapSimulatorWindowNames.VegaSpell => "Vega's Spell",
                MapSimulatorWindowNames.CharacterInfo => "Character Info",
                MapSimulatorWindowNames.SocialList => "Social List",
                MapSimulatorWindowNames.GuildSearch => "Guild Search",
                MapSimulatorWindowNames.GuildSkill => "Guild Skill",
                MapSimulatorWindowNames.GuildBbs => "Guild BBS",
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
