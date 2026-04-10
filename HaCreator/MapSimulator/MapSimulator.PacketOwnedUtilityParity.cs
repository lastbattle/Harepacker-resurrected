using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Companions;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Loaders;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Physics;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.UI;
using HaCreator.Wz;
using HaSharedLibrary.Wz;
using HaSharedLibrary.Render.DX;
using HaCreator.MapEditor.Instance.Shapes;
using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int FollowRequestClientOptionId = 1014;
        private const int FollowRequestPromptStringPoolId = 0x16E6;
        private const int PacketOwnedBattleshipCooldownSentinel = 0x004FAE6F;
        private const int PacketOwnedBattleshipSkillId = 5221006;
        private const int PacketOwnedBattleshipMountItemId = 1932000;
        private const int PacketOwnedApspPromptStringPoolId = 0x17BA;
        private const int PacketOwnedRadioStartStringPoolId = RadioOwnerStringPoolText.StartNoticeStringPoolId;
        private const int PacketOwnedRadioCompleteStringPoolId = RadioOwnerStringPoolText.CompleteNoticeStringPoolId;
        private const int PacketOwnedRadioTrackTemplateStringPoolId = RadioOwnerStringPoolText.TrackPathTemplateStringPoolId;
        private const int PacketOwnedRadioAudioTemplateStringPoolId = RadioOwnerStringPoolText.AudioPathTemplateStringPoolId;
        private const int PacketOwnedRadioCreateLayerContextSlot = 3562;
        private const int PacketOwnedRadioCreateLayerLeftMargin = 40;
        private const string PacketOwnedRadioUiCanvasPathOn = "UI/UIWindow.img/Radio/On";
        private const string PacketOwnedRadioUiCanvasPathOff = "UI/UIWindow.img/Radio/Off/0";
        private const int PacketOwnedRadioUiFrameDelayMs = 150;
        private const int PacketOwnedRadioUiFadeDurationMs = 100;
        private const int PacketOwnedRadioUpdatePollIntervalMs = 2000;
        private const int PacketOwnedClassCompetitionAuthRefreshIntervalMs = 180000;
        private const int PacketOwnedClassCompetitionAuthLifetimeMs = 300000;
        private const int PacketOwnedClassCompetitionSyntheticAuthResponseDelayMs = 250;
        private const int PacketOwnedClassCompetitionAuthRequestOpcode = 291;
        private const int PacketOwnedClassCompetitionUrlTemplateStringPoolId = 0x11DC;
        private const int PacketOwnedSkillLearnSuccessSoundStringPoolId = 0x0507;
        private const int PacketOwnedSkillLearnFailureSoundStringPoolId = 0x0508;
        private const int PacketOwnedSkillLearnMasteryBookLabelStringPoolId = 0x0F2F;
        private const int PacketOwnedSkillLearnSkillBookLabelStringPoolId = 0x0F30;
        private const int PacketOwnedSkillLearnCannotUseStringPoolId = 0x0F31;
        private const int PacketOwnedSkillLearnFailureNoticeStringPoolId = 0x0F32;
        private const int PacketOwnedSkillLearnMasterySuccessNoticeStringPoolId = 0x0F33;
        private const int PacketOwnedSkillLearnSkillSuccessNoticeStringPoolId = 0x0F34;
        private const string PacketOwnedSkillLearnSuccessSoundFallback = "Sound/Game.img/EnchantSuccess";
        private const string PacketOwnedSkillLearnFailureSoundFallback = "Sound/Game.img/EnchantFailure";
        private const string PacketOwnedClassCompetitionServerHost = "gamerank.maplestory";
        private const int PacketOwnedApspFollowUpOpcode = 195;
        private const int PacketOwnedApspFollowUpResponseCode = 6;
        private const int PacketOwnedApspMinEventType = 11;
        private const int PacketOwnedApspMaxEventType = 13;
        private const int PacketOwnedLegacyVengeanceSkillId = 3120010;
        private const int PacketOwnedCurrentVengeanceSkillId = 31101003;
        private const string PacketOwnedVengeanceSkillName = "Vengeance";
        private const int PacketOwnedCurrentTimeBombSkillId = 4341003;
        private const string PacketOwnedTimeBombSkillName = "Monster Bomb";
        private const string PacketOwnedTimeBombSkillDescriptionMarker = "explosion occurs 3 seconds after the charm is activated";
        private const int PacketOwnedCurrentExJablinSkillId = 4120010;
        private const string PacketOwnedExJablinSkillDescriptionMarker = "the next attack will always be a Critical Attack";
        private const int PacketOwnedBaseTimeBombHitPeriodMs = 1500;
        private const int PacketOwnedTimeBombInvincibilityOptionType = 52;
        private const string PacketOwnedTimeBombInvincibilityDurationTemplate = "Invincible for #time more seconds after getting attacked.";
        private const string PacketOwnedTimeBombInvincibilityChanceTemplate = "#prop% chance to become invincible for #time seconds when attacked.";
        private static readonly int[] PacketOwnedFallbackTimeBombInvincibilityOptionIds = { 20366, 30366, 30371, 40366, 40371 };
        private const string PacketOwnedApspPromptPrimaryLabel = "OK";
        private const string PacketOwnedApspPromptSecondaryLabel = "Cancel";
        private const int PacketOwnedTutorBalloonScreenMargin = 6;
        private const int PacketOwnedTutorBalloonClientLeftInset = 19;
        private const int PacketOwnedTutorBalloonClientRightInset = 19;
        private const int PacketOwnedTutorBalloonClientTopInset = 21;
        private const int PacketOwnedTutorBalloonClientBottomInset = 19;
        private const int PacketOwnedTutorBalloonClientArrowLeftOffset = 10;
        private const int PacketOwnedTutorBalloonClientLayerHeightPadding = 92;
        private const int PacketOwnedTutorBalloonClientArrowTopOffset = 35;
        private const int PacketOwnedTutorBalloonClientVerticalAnchorOffset = 90;
        private PacketOwnedSocialUtilityDialogDispatcher _packetOwnedSocialUtilityDialogDispatcher;
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

        private readonly record struct PacketOwnedSkillLearnItemResult(
            bool OnExclusiveRequest,
            int CharacterId,
            bool IsMasteryBook,
            int ItemId,
            int SkillId,
            bool Used,
            bool Succeeded);

        private readonly record struct PacketOwnedTutorDisplayOwner(
            int CharacterId,
            Vector2 Position,
            bool FacingRight,
            int Level,
            bool IsLocalOwner);

        private readonly record struct PacketOwnedTutorDisplayState(
            TutorVariantSnapshot Variant,
            PacketOwnedTutorDisplayOwner Owner);

        internal readonly record struct PacketOwnedTimeBombInvincibilityOptionLevelData(int DurationMs, int ProbabilityPercent);

        internal readonly record struct PacketOwnedTimeBombAttackPayload(
            int SkillId,
            int TimeBombX,
            int TimeBombY,
            int ImpactPercent,
            int Damage);

        internal enum PacketOwnedTimeBombHpEffectKind
        {
            None = 0,
            Heal,
            Miss
        }

        internal enum PacketOwnedFollowPromptOwnerKind
        {
            None = 0,
            InGameConfirmDialog,
            LoginUtilityDialog
        }

        internal sealed class PacketOwnedTimeBombInvincibilityOptionDefinition
        {
            public string DisplayTemplate { get; init; } = string.Empty;
            public PacketOwnedTimeBombInvincibilityOptionLevelData[] Levels { get; init; } = Array.Empty<PacketOwnedTimeBombInvincibilityOptionLevelData>();
        }

        private readonly Dictionary<int, HashSet<int>> _packetQuestGuideTargetsByMobId = new();
        private readonly Dictionary<int, PacketOwnedTimeBombInvincibilityOptionDefinition> _packetOwnedTimeBombInvincibilityOptions = new();
        private readonly LocalFollowCharacterRuntime _localFollowRuntime = new();
        private readonly TutorRuntime _packetOwnedTutorRuntime = new();
        private bool _packetOwnedMiniMapOnOffVisible = true;
        private readonly LocalUtilityPacketInboxManager _localUtilityPacketInbox = new();
        private readonly LocalUtilityOfficialSessionBridgeManager _localUtilityOfficialSessionBridge = new();
        private readonly LocalUtilityPacketTransportManager _localUtilityPacketOutbox = new();
        private static readonly Lazy<HashSet<int>> PacketOwnedTimeBombSkillIdCatalog = new(CreatePacketOwnedTimeBombSkillIdCatalog);
        private static readonly Lazy<HashSet<int>> PacketOwnedVengeanceSkillIdCatalog = new(CreatePacketOwnedVengeanceSkillIdCatalog);
        private static readonly Lazy<HashSet<int>> PacketOwnedExJablinSkillIdCatalog = new(CreatePacketOwnedExJablinSkillIdCatalog);
        private static readonly Lazy<int[]> PacketOwnedTimeBombInvincibilityOptionIds = new(CreatePacketOwnedTimeBombInvincibilityOptionIds);
        private static readonly Lazy<IReadOnlyDictionary<int, int[]>> PacketOwnedSkillIdAliasCandidates = new(CreatePacketOwnedSkillIdAliasCandidates);
        private LocalOverlayBalloonSkin _packetOwnedTutorBalloonSkin;
        private readonly Dictionary<int, List<IDXObject>> _packetOwnedTutorCueFramesByIndex = new();
        private readonly HashSet<int> _packetOwnedTutorTrackedSummonObjectIds = new();
        private readonly Dictionary<int, int> _packetOwnedTutorSummonMessageSequenceIdsByObjectId = new();
        private PacketOwnedBattleshipDurabilityOverrideState _packetOwnedBattleshipDurabilityOverride;
        private int _packetQuestGuideQuestId;
        private int _packetOwnedUtilityRequestTick = int.MinValue;
        private int _lastDeliveryQuestId;
        private int _lastDeliveryItemId;
        private QuestDetailDeliveryType _lastPacketOwnedDeliveryType;
        private readonly List<int> _lastDeliveryDisallowedQuestIds = new();
        private int _lastQuestDemandItemQueryQuestId;
        private readonly List<int> _lastQuestDemandQueryVisibleItemIds = new();
        private readonly Dictionary<int, List<int>> _lastQuestDemandQueryVisibleItemMapIds = new();
        private int _lastQuestDemandQueryHiddenItemCount;
        private bool _lastQuestDemandQueryHasPacketOwnedMapResults;
        private int _lastPacketOwnedResignQuestId;
        private int _lastPacketOwnedResignQuestTick = int.MinValue;
        private int _lastPacketOwnedMateNameQuestId;
        private string _lastPacketOwnedMateName = string.Empty;
        private int _lastPacketOwnedMateNameTick = int.MinValue;
        private int _lastClassCompetitionOpenTick = int.MinValue;
        private int _lastClassCompetitionAuthRequestTick = int.MinValue;
        private int _lastClassCompetitionAuthIssuedTick = int.MinValue;
        private int _lastClassCompetitionNavigateTick = int.MinValue;
        private bool _lastClassCompetitionAuthPending;
        private bool _lastClassCompetitionLoggedIn;
        private bool _lastClassCompetitionNavigatePending = true;
        private string _lastClassCompetitionAuthKey = string.Empty;
        private string _lastClassCompetitionUrl = string.Empty;
        private string _lastClassCompetitionAuthSource = "none";
        private string _lastClassCompetitionAuthDispatchStatus = "No class-competition auth request has been emitted yet.";
        private int _lastClassCompetitionAuthResponseTick = int.MinValue;
        private int _lastPacketOwnedOpenUiType = -1;
        private int _lastPacketOwnedOpenUiOption = -1;
        private int _lastPacketOwnedCommoditySerialNumber;
        private int _lastPacketOwnedCommodityRequestTick = int.MinValue;
        private string _lastPacketOwnedNoticeMessage;
        private int _lastPacketOwnedNoticeTick = int.MinValue;
        private string _lastPacketOwnedChatMessage;
        private int _lastPacketOwnedChatTick = int.MinValue;
        private readonly List<EventAlarmLineSnapshot> _packetOwnedEventAlarmLines = new();
        private string _lastPacketOwnedEventAlarmSummary = string.Empty;
        private int _lastPacketOwnedEventAlarmTick = int.MinValue;
        private readonly List<EventEntrySnapshot> _packetOwnedEventCalendarEntries = new();
        private string _lastPacketOwnedEventCalendarSummary = string.Empty;
        private int _lastPacketOwnedEventCalendarTick = int.MinValue;
        private string _lastPacketOwnedBuffzoneMessage;
        private int _lastPacketOwnedBuffzoneTick = int.MinValue;
        private string _lastPacketOwnedAskApspMessage;
        private int _lastPacketOwnedAskApspTick = int.MinValue;
        private string _lastPacketOwnedSkillGuideMessage;
        private int _lastPacketOwnedSkillGuideTick = int.MinValue;
        private string _lastPacketOwnedFollowFailureMessage;
        private int _lastPacketOwnedFollowFailureTick = int.MinValue;
        private int? _lastPacketOwnedFollowFailureReason;
        private int _lastPacketOwnedFollowFailureDriverId;
        private bool _lastPacketOwnedFollowFailureClearedPending;
        private PlayerMovementSyncSnapshot _lastPacketOwnedPassiveMoveSnapshot;
        private int _lastPacketOwnedPassiveMoveTick = int.MinValue;
        private int _lastPacketOwnedDirectionModeTick = int.MinValue;
        private bool _lastPacketOwnedDirectionModeEnabled;
        private int _lastPacketOwnedDirectionModeDelayMs;
        private int _lastPacketOwnedStandAloneTick = int.MinValue;
        private bool _lastPacketOwnedStandAloneEnabled;
        private int _lastPacketOwnedSkillGuideGrade;
        private bool _packetOwnedFollowPromptActive;
        private PacketOwnedFollowPromptOwnerKind _packetOwnedFollowPromptOwner;
        private bool _packetOwnedApspPromptActive;
        private int _packetOwnedApspPromptContextToken;
        private int _packetOwnedApspPromptEventType;
        private readonly PacketOwnedLocalUtilityContextState _packetOwnedLocalUtilityContext = new();
        private int _lastPacketOwnedApspFollowUpContextToken;
        private int _lastPacketOwnedApspFollowUpResponseCode;
        private string _lastPacketOwnedEventSoundDescriptor;
        private int _lastPacketOwnedEventSoundTick = int.MinValue;
        private string _lastPacketOwnedMinigameSoundDescriptor;
        private int _lastPacketOwnedMinigameSoundTick = int.MinValue;
        private MonoGameBgmPlayer _packetOwnedRadioAudio;
        private string _lastPacketOwnedRadioTrackDescriptor;
        private string _lastPacketOwnedRadioResolvedTrackDescriptor;
        private string _lastPacketOwnedRadioResolvedDescriptor;
        private string _lastPacketOwnedRadioDisplayName;
        private string _lastPacketOwnedRadioStatusMessage = "Packet-owned radio idle.";
        private int _lastPacketOwnedRadioTimeValue;
        private int _lastPacketOwnedRadioStartOffsetMs;
        private int _lastPacketOwnedRadioTrackDurationMs;
        private int _lastPacketOwnedRadioAvailableDurationMs;
        private int _lastPacketOwnedRadioStartTick = int.MinValue;
        private int _lastPacketOwnedRadioExpectedStopTick = int.MinValue;
        private int _lastPacketOwnedRadioLastPollTick = int.MinValue;
        private bool _packetOwnedRadioSessionCreateLayerLeft;
        private int _packetOwnedRadioSessionCreateLayerMutationSequence = -1;
        private string _packetOwnedRadioSessionCreateLayerSource = "uninitialized";
        private bool _localUtilityPacketInboxEnabled = EnablePacketConnectionsByDefault;
        private int _localUtilityPacketInboxConfiguredPort = LocalUtilityPacketInboxManager.DefaultPort;
        private bool _localUtilityOfficialSessionBridgeEnabled;
        private bool _localUtilityOfficialSessionBridgeUseDiscovery;
        private int _localUtilityOfficialSessionBridgeConfiguredListenPort = LocalUtilityOfficialSessionBridgeManager.DefaultListenPort;
        private string _localUtilityOfficialSessionBridgeConfiguredRemoteHost = "127.0.0.1";
        private int _localUtilityOfficialSessionBridgeConfiguredRemotePort;
        private string _localUtilityOfficialSessionBridgeConfiguredProcessSelector;
        private int? _localUtilityOfficialSessionBridgeConfiguredLocalPort;
        private const int LocalUtilityOfficialSessionBridgeDiscoveryRefreshIntervalMs = 2000;
        private int _nextLocalUtilityOfficialSessionBridgeDiscoveryRefreshAt;
        private bool _localUtilityPacketOutboxEnabled;
        private int _localUtilityPacketOutboxConfiguredPort = LocalUtilityPacketTransportManager.DefaultPort;

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
            MapSimulatorWindowNames.AccountMoreInfo,
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
                NotifyEventAlarmOwnerActivity("packet-owned quest guide");
                ShowUtilityFeedbackMessage(notice);
                return notice;
            }

            _packetQuestGuideQuestId = Math.Max(0, questId);
            RefreshWorldMapWindow(_mapBoard?.MapInfo?.id ?? 0);

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.WorldMap) is not WorldMapUI worldMapWindow)
            {
                ClearPacketQuestGuideTargets(refreshWorldMap: false);
                const string unavailable = "World map window is not available in this UI build.";
                NotifyEventAlarmOwnerActivity("packet-owned quest guide");
                ShowUtilityFeedbackMessage(unavailable);
                return unavailable;
            }

            if (!TryResolveFirstQuestGuideTarget(out int mobId, out int mapId))
            {
                ClearPacketQuestGuideTargets();
                const string notice = "Quest guide data did not contain any usable world-map mob targets.";
                NotifyEventAlarmOwnerActivity("packet-owned quest guide");
                ShowUtilityFeedbackMessage(notice);
                return notice;
            }

            string mobName = ResolvePacketGuideMobName(mobId);
            if (!worldMapWindow.FocusSearchResult(WorldMapUI.SearchResultKind.Mob, mobName, mapId))
            {
                ClearPacketQuestGuideTargets();
                string notice = $"Quest guide data for {mobName} could not be resolved in the simulator world map.";
                NotifyEventAlarmOwnerActivity("packet-owned quest guide");
                ShowUtilityFeedbackMessage(notice);
                return notice;
            }

            uiWindowManager.ShowWindow(MapSimulatorWindowNames.WorldMap);
            uiWindowManager.BringToFront(worldMapWindow);
            NotifyEventAlarmOwnerActivity("packet-owned quest guide");
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

        private string ApplyDeliveryQuestLaunch(
            int questId,
            int itemId,
            IReadOnlyList<int> disallowedQuestIds,
            QuestDetailDeliveryType packetOwnedDeliveryType = QuestDetailDeliveryType.None)
        {
            StampPacketOwnedUtilityRequestState();
            _lastDeliveryQuestId = Math.Max(0, questId);
            _lastDeliveryItemId = Math.Max(0, itemId);
            _lastPacketOwnedDeliveryType = packetOwnedDeliveryType;
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
                NotifyEventAlarmOwnerActivity("packet-owned delivery quest");
                ShowUtilityFeedbackMessage(message);
                return message;
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.QuestDelivery) is not QuestDeliveryWindow questDeliveryWindow)
            {
                const string unavailable = "Quest delivery window is not available in this UI build.";
                NotifyEventAlarmOwnerActivity("packet-owned delivery quest");
                ShowUtilityFeedbackMessage(unavailable);
                return unavailable;
            }

            IReadOnlyList<QuestDeliveryWindow.DeliveryEntry> entries = BuildQuestDeliveryEntries(
                _lastDeliveryQuestId,
                _lastDeliveryItemId,
                _lastDeliveryDisallowedQuestIds);
            if (entries.Count == 0)
            {
                string emptyMessage = _lastDeliveryQuestId > 0
                    ? $"Quest delivery suppressed quest #{_lastDeliveryQuestId} because no packet-worthy target with a usable NPC and live delivery-item slot survived the packet-owned filter."
                    : $"{itemName} delivery could not surface a packet-worthy quest target with a usable NPC and live delivery-item slot.";
                NotifyEventAlarmOwnerActivity("packet-owned delivery quest");
                ShowUtilityFeedbackMessage(emptyMessage);
                return emptyMessage;
            }

            questDeliveryWindow.Configure(_lastDeliveryQuestId, _lastDeliveryItemId, entries, _packetOwnedUtilityRequestTick);
            if (!TryShowFieldRestrictedWindow(MapSimulatorWindowNames.QuestDelivery))
            {
                return GetFieldWindowRestrictionMessage(MapSimulatorWindowNames.QuestDelivery)
                    ?? "Quest delivery cannot be opened in this map.";
            }

            ShowWindow(MapSimulatorWindowNames.QuestDelivery, questDeliveryWindow, trackDirectionModeOwner: true);
            NotifyEventAlarmOwnerActivity("packet-owned delivery quest");
            return $"Opened packet-authored quest delivery for {itemName}.";
        }

        private string ApplyQuestDemandItemQueryLaunch(QuestDemandItemQueryState queryState)
        {
            StampPacketOwnedUtilityRequestState();
            _lastQuestDemandItemQueryQuestId = Math.Max(0, queryState?.QuestId ?? 0);
            _lastQuestDemandQueryVisibleItemIds.Clear();
            _lastQuestDemandQueryVisibleItemMapIds.Clear();
            _lastQuestDemandQueryHiddenItemCount = Math.Max(0, queryState?.HiddenItemCount ?? 0);
            _lastQuestDemandQueryHasPacketOwnedMapResults = queryState?.HasPacketOwnedMapResults == true;

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

            if (queryState?.VisibleItemMapIds != null)
            {
                foreach ((int itemId, IReadOnlyList<int> mapIds) in queryState.VisibleItemMapIds)
                {
                    if (itemId <= 0 || mapIds == null)
                    {
                        continue;
                    }

                    List<int> normalizedMapIds = mapIds
                        .Where(mapId => mapId > 0)
                        .Distinct()
                        .OrderBy(mapId => mapId)
                        .ToList();
                    if (normalizedMapIds.Count > 0)
                    {
                        _lastQuestDemandQueryVisibleItemMapIds[itemId] = normalizedMapIds;
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
            int focusItemId = ResolveQuestDemandQueryFocusItemId(currentFieldId);
            IReadOnlyList<int> focusMapIds = ResolveQuestDemandQueryMapIds(focusItemId, currentFieldId);
            int focusMapId = focusMapIds.Count > 0 ? focusMapIds[0] : currentFieldId;
            string focusItemName = InventoryItemMetadataResolver.TryResolveItemName(focusItemId, out string resolvedItemName)
                ? resolvedItemName
                : $"Item {focusItemId}";
            bool focusedItem = demandWorldMapWindow.FocusSearchResult(WorldMapUI.SearchResultKind.Item, focusItemName, focusMapId);
            uiWindowManager.ShowWindow(MapSimulatorWindowNames.WorldMap);
            uiWindowManager.BringToFront(demandWorldMapWindow);

            string hiddenSuffix = _lastQuestDemandQueryHiddenItemCount > 0
                ? $" {_lastQuestDemandQueryHiddenItemCount} hidden demand item(s) remain packet-only."
                : string.Empty;
            string mapSuffix = _lastQuestDemandQueryHasPacketOwnedMapResults && focusMapIds.Count > 0
                ? focusMapIds.Count == 1
                    ? $" Routed to {ResolveMapTransferDisplayName(focusMapId, null)}."
                    : $" Routed across {focusMapIds.Count} packet-authored map result(s)."
                : string.Empty;
            return focusedItem
                ? $"Opened a packet-shaped quest demand item query for {focusItemName}.{hiddenSuffix}{mapSuffix}".TrimEnd()
                : $"Opened the world map, but the demand-item query for {focusItemName} could not be resolved.{hiddenSuffix}{mapSuffix}".TrimEnd();
        }

        private int ResolveQuestDemandQueryFocusItemId(int currentMapId)
        {
            if (_lastQuestDemandQueryVisibleItemIds.Count == 0)
            {
                return 0;
            }

            for (int i = 0; i < _lastQuestDemandQueryVisibleItemIds.Count; i++)
            {
                int itemId = _lastQuestDemandQueryVisibleItemIds[i];
                IReadOnlyList<int> mapIds = ResolveQuestDemandQueryMapIds(itemId, fallbackMapId: 0);
                if (mapIds.Count == 0)
                {
                    continue;
                }

                if (currentMapId > 0 && mapIds.Contains(currentMapId))
                {
                    return itemId;
                }

                return itemId;
            }

            return _lastQuestDemandQueryVisibleItemIds[0];
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
                IReadOnlyList<int> mapIds = ResolveQuestDemandQueryMapIds(itemId, currentMapId);
                if (mapIds.Count == 0)
                {
                    continue;
                }

                for (int mapIndex = 0; mapIndex < mapIds.Count; mapIndex++)
                {
                    int mapId = mapIds[mapIndex];
                    string dedupeKey = $"questitem:{_lastQuestDemandItemQueryQuestId}:{mapId}:{itemId}";
                    if (!seen.Add(dedupeKey))
                    {
                        continue;
                    }

                    string mapName = ResolveMapTransferDisplayName(mapId, null);
                    string sourceText = _lastQuestDemandQueryHasPacketOwnedMapResults
                        ? "Packet-authored quest demand item query"
                        : "Quest demand item query";
                    results.Add(new WorldMapUI.SearchResultEntry
                    {
                        Kind = WorldMapUI.SearchResultKind.Item,
                        MapId = mapId,
                        Label = itemName,
                        Description = $"{sourceText} for quest #{_lastQuestDemandItemQueryQuestId} in {mapName}"
                    });
                }
            }
        }

        private void ClearQuestDemandItemQueryState(bool refreshWorldMap = true)
        {
            _lastQuestDemandItemQueryQuestId = 0;
            _lastQuestDemandQueryVisibleItemIds.Clear();
            _lastQuestDemandQueryVisibleItemMapIds.Clear();
            _lastQuestDemandQueryHiddenItemCount = 0;
            _lastQuestDemandQueryHasPacketOwnedMapResults = false;

            if (refreshWorldMap)
            {
                RefreshWorldMapWindow(_mapBoard?.MapInfo?.id ?? 0);
            }
        }

        private IReadOnlyList<int> ResolveQuestDemandQueryMapIds(int itemId, int fallbackMapId)
        {
            if (itemId > 0 &&
                _lastQuestDemandQueryVisibleItemMapIds.TryGetValue(itemId, out List<int> storedMapIds) &&
                storedMapIds != null &&
                storedMapIds.Count > 0)
            {
                return storedMapIds;
            }

            return fallbackMapId > 0
                ? new[] { fallbackMapId }
                : Array.Empty<int>();
        }

        private static IReadOnlyList<int> ResolveRuntimeFallbackDemandItemMapIds(QuestDemandItemQueryState runtimeFallbackQuery, int itemId)
        {
            if (itemId > 0 &&
                runtimeFallbackQuery?.VisibleItemMapIds != null &&
                runtimeFallbackQuery.VisibleItemMapIds.TryGetValue(itemId, out IReadOnlyList<int> mapIds) &&
                mapIds != null &&
                mapIds.Count > 0)
            {
                return mapIds;
            }

            return Array.Empty<int>();
        }

        private string ApplyClassCompetitionPageLaunch()
        {
            StampPacketOwnedUtilityRequestState();
            _lastClassCompetitionOpenTick = Environment.TickCount;
            bool hadNavigatedPage = _lastClassCompetitionLoggedIn && !string.IsNullOrWhiteSpace(_lastClassCompetitionUrl);
            RefreshClassCompetitionRuntimeState(forceAuthRequest: true);

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ClassCompetition) is not UIWindowBase window)
            {
                const string unavailable = "Class Competition page owner is not available in this UI build.";
                NotifyEventAlarmOwnerActivity("packet-owned class competition");
                ShowUtilityFeedbackMessage(unavailable);
                return unavailable;
            }

            ShowWindow(MapSimulatorWindowNames.ClassCompetition, window, trackDirectionModeOwner: true);
            NotifyEventAlarmOwnerActivity("packet-owned class competition");
            if (_lastClassCompetitionAuthPending)
            {
                return hadNavigatedPage && _lastClassCompetitionLoggedIn
                    ? "Opened packet-authored Class Competition page, kept the cached NavigateUrl page live, and queued opcode 291 auth refresh in the background."
                    : "Opened packet-authored Class Competition page and seeded the initial opcode 291 auth request.";
            }

            return hadNavigatedPage && _lastClassCompetitionLoggedIn
                ? "Opened packet-authored Class Competition page using the cached NavigateUrl page."
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

                int deliveryCashItemId = snapshot.DeliveryCashItemId ?? 0;
                bool resolvedDeliverySlot = TryResolveQuestDeliveryCashItemSlot(
                    deliveryCashItemId,
                    out InventoryType deliveryInventoryType,
                    out int deliveryRuntimeSlotIndex,
                    out int deliveryClientSlotIndex);
                bool requiresDeliverySlot = deliveryCashItemId > 0;
                if (snapshot.IsBlocked || snapshot.TargetNpcId <= 0 || (requiresDeliverySlot && !resolvedDeliverySlot))
                {
                    appendedQuestIds.Remove(snapshot.QuestId);
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
                    CanConfirm = snapshot.CanConfirm && (!requiresDeliverySlot || resolvedDeliverySlot),
                    IsBlocked = snapshot.IsBlocked,
                    IsSeriesRepresentative = snapshot.IsSeriesRepresentative,
                    DeliveryCashInventoryType = resolvedDeliverySlot ? deliveryInventoryType : InventoryType.NONE,
                    DeliveryCashItemRuntimeSlotIndex = resolvedDeliverySlot ? deliveryRuntimeSlotIndex : -1,
                    DeliveryCashItemClientSlotIndex = resolvedDeliverySlot ? deliveryClientSlotIndex : 0
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

                if (blockedByPacket || (state?.TargetNpcId ?? 0) <= 0)
                {
                    continue;
                }

                bool completionPhase = state?.DeliveryType == QuestDetailDeliveryType.Complete;
                bool canConfirm = state?.DeliveryActionEnabled == true && !blockedByPacket;
                string npcName = string.IsNullOrWhiteSpace(state?.TargetNpcName)
                    ? "NPC unavailable"
                    : state.TargetNpcName;
                int deliveryCashItemId = state?.DeliveryCashItemId ?? 0;
                bool resolvedDeliverySlot = TryResolveQuestDeliveryCashItemSlot(
                    deliveryCashItemId,
                    out InventoryType deliveryInventoryType,
                    out int deliveryRuntimeSlotIndex,
                    out int deliveryClientSlotIndex);
                if (deliveryCashItemId > 0 && !resolvedDeliverySlot)
                {
                    continue;
                }

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
                    IsSeriesRepresentative = false,
                    DeliveryCashInventoryType = resolvedDeliverySlot ? deliveryInventoryType : InventoryType.NONE,
                    DeliveryCashItemRuntimeSlotIndex = resolvedDeliverySlot ? deliveryRuntimeSlotIndex : -1,
                    DeliveryCashItemClientSlotIndex = resolvedDeliverySlot ? deliveryClientSlotIndex : 0
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

        private bool TryResolveQuestDeliveryCashItemSlot(
            int itemId,
            out InventoryType inventoryType,
            out int runtimeSlotIndex,
            out int clientSlotIndex)
        {
            inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(itemId);
            runtimeSlotIndex = -1;
            clientSlotIndex = 0;
            if (itemId <= 0 || inventoryType == InventoryType.NONE || uiWindowManager?.InventoryWindow is not IInventoryRuntime inventoryWindow)
            {
                return false;
            }

            IReadOnlyList<InventorySlotData> slots = inventoryWindow.GetSlots(inventoryType);
            if (slots == null)
            {
                return false;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlotData slot = slots[i];
                if (slot == null
                    || slot.IsDisabled
                    || slot.ItemId != itemId
                    || Math.Max(0, slot.Quantity) <= 0)
                {
                    continue;
                }

                runtimeSlotIndex = i;
                clientSlotIndex = i + 1;
                return true;
            }

            return false;
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

            HandleQuestWindowActionResult(HandleQuestDeliveryLocalHandoff(
                entry.QuestId,
                entry.CompletionPhase,
                sourceContext: "packet-authored quest-delivery owner",
                localHandoff: () =>
                {
                    OpenPacketQuestDeliveryNpcInteraction(entry);
                    return $"Opened packet-authored quest delivery interaction for {entry.Title}.";
                },
                resolvedInventoryType: entry.DeliveryCashInventoryType,
                resolvedRuntimeSlotIndex: entry.DeliveryCashItemRuntimeSlotIndex,
                resolvedClientSlotIndex: entry.DeliveryCashItemClientSlotIndex));
        }

        private void OpenPacketQuestDeliveryNpcInteraction(QuestDeliveryWindow.DeliveryEntry entry)
        {
            if (entry == null || _npcInteractionOverlay == null)
            {
                return;
            }

            NpcItem targetNpc = FindNpcById(entry.TargetNpcId) ?? CreateNpcPreview(entry.TargetNpcId, includeTooltips: false);
            string npcName = targetNpc?.NpcInstance?.NpcInfo?.StringName;
            if (string.IsNullOrWhiteSpace(npcName))
            {
                npcName = string.IsNullOrWhiteSpace(entry.NpcName)
                    ? ResolveNpcDisplayName(entry.TargetNpcId)
                    : entry.NpcName;
            }

            NpcInteractionState interactionState = ApplyPacketOwnedQuestDeliveryPresentation(
                _questRuntime.BuildQuestDeliveryInteractionState(
                    entry.QuestId,
                    _playerManager?.Player?.Build,
                    _lastDeliveryItemId),
                npcName)
                ?? ApplyPacketOwnedQuestDeliveryPresentation(
                    _questRuntime.BuildSingleQuestInteractionState(
                        entry.TargetNpcId,
                        npcName,
                        entry.QuestId,
                        _playerManager?.Player?.Build,
                        includeDeliveryFallback: false),
                    npcName);
            if (interactionState == null)
            {
                return;
            }

            _gameState.EnterDirectionMode();
            _scriptedDirectionModeOwnerActive = true;
            _activeNpcInteractionNpc = targetNpc;
            _activeNpcInteractionNpcId = entry.TargetNpcId;
            TryRegisterAnimationDisplayerQuestDeliveryLocalUserState(_lastDeliveryItemId, out _);
            PublishDynamicObjectTagStatesForNpc(entry.TargetNpcId, currTickCount, targetNpc);
            _npcInteractionOverlay.Open(interactionState);

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.QuestDelivery) is QuestDeliveryWindow questDeliveryWindow)
            {
                questDeliveryWindow.Hide();
            }
        }

        private static NpcInteractionState ApplyPacketOwnedQuestDeliveryPresentation(NpcInteractionState interactionState, string npcName)
        {
            if (interactionState == null)
            {
                return null;
            }

            return new NpcInteractionState
            {
                NpcName = string.IsNullOrWhiteSpace(npcName)
                    ? interactionState.NpcName
                    : npcName,
                Entries = interactionState.Entries,
                SelectedEntryId = interactionState.SelectedEntryId,
                PresentationStyle = interactionState.PresentationStyle
            };
        }

        private IReadOnlyList<string> BuildClassCompetitionPageLines()
        {
            RefreshClassCompetitionRuntimeState();
            var lines = new List<string>();
            var build = _playerManager?.Player?.Build;
            string urlTemplate = ResolveClassCompetitionUrlTemplate(out bool usedResolvedTemplate);
            lines.Add("Packet-authored web owner mirroring CUserLocal::OnOpenClassCompetitionPage and CClassCompetition.");
            lines.Add("Constructor shape: CWebWnd, 312x389 owner bounds, close/OK dismissal only, opcode 291 auth requests, and a loading layer while initial auth is pending.");

            string authState;
            if (_lastClassCompetitionAuthPending)
            {
                authState = _lastClassCompetitionNavigatePending || !_lastClassCompetitionLoggedIn
                    ? $"Auth request {PacketOwnedClassCompetitionAuthRequestOpcode} was issued at {_lastClassCompetitionAuthRequestTick}, and the owner is waiting for its first navigable auth cache."
                    : $"Background auth refresh {PacketOwnedClassCompetitionAuthRequestOpcode} was issued at {_lastClassCompetitionAuthRequestTick} while the cached NavigateUrl page remains navigated.";
            }
            else if (_lastClassCompetitionAuthIssuedTick == int.MinValue)
            {
                authState = "No class-competition auth response has populated the local cache yet.";
            }
            else if (_lastClassCompetitionLoggedIn)
            {
                authState = $"Auth cache landed at {_lastClassCompetitionAuthIssuedTick} and the seeded page last navigated at {_lastClassCompetitionNavigateTick}.";
            }
            else
            {
                authState = $"Auth cache landed at {_lastClassCompetitionAuthIssuedTick}, but navigation has not completed yet.";
            }

            lines.Add(authState);
            lines.Add($"Auth cache source: {_lastClassCompetitionAuthSource}");
            lines.Add($"Auth request dispatch: {_lastClassCompetitionAuthDispatchStatus}");
            lines.Add($"NavigateUrl template: {(usedResolvedTemplate ? MapleStoryStringPool.FormatFallbackLabel(PacketOwnedClassCompetitionUrlTemplateStringPoolId, 4) : "local fallback")} -> {urlTemplate}");
            lines.Add($"Recovered server host: {PacketOwnedClassCompetitionServerHost}");
            if (_lastClassCompetitionAuthIssuedTick != int.MinValue)
            {
                lines.Add($"Cached auth token: {SummarizeClassCompetitionAuthKey(_lastClassCompetitionAuthKey)}");
            }

            if (!string.IsNullOrWhiteSpace(_lastClassCompetitionUrl))
            {
                lines.Add($"NavigateUrl target: {_lastClassCompetitionUrl}");
            }

            if (build != null)
            {
                lines.Add($"{build.Name}  Lv.{Math.Max(1, build.Level)}  {build.JobName}");
                lines.Add($"Map {(_mapBoard?.MapInfo?.id ?? 0)}  Fame {build.Fame}  HP {Math.Max(0, build.HP)}/{Math.Max(1, build.MaxHP)}");
                lines.Add($"Local ladder context: world {(build.WorldRank > 0 ? $"#{build.WorldRank}" : "local only")}  job {(build.JobRank > 0 ? $"#{build.JobRank}" : "local only")}");
                lines.Add($"Combat seed: PAD {build.TotalAttack}  MAD {build.TotalMagicAttack}  ACC {build.TotalAccuracy}  EVA {build.TotalAvoidability}");
                lines.AddRange(BuildClassCompetitionSeededStandingLines(build));
            }
            else
            {
                lines.Add("No active player build is bound to the simulator.");
            }

            lines.Add("This owner still has no live server-fed auth or ladder payload, so the first NavigateUrl and later auth refresh cadence match the client more closely while standings remain seeded from the active local build instead of a remote page response.");

            if (_lastClassCompetitionOpenTick != int.MinValue)
            {
                lines.Add($"Last packet launch tick: {_lastClassCompetitionOpenTick.ToString(CultureInfo.InvariantCulture)}");
            }

            return lines;
        }

        private IReadOnlyList<string> BuildClassCompetitionSeededStandingLines(CharacterBuild build)
        {
            if (build == null)
            {
                return Array.Empty<string>();
            }

            return new[]
            {
                "Synthetic ladder preview:",
                BuildClassCompetitionStandingLine("World", Math.Max(1, ResolveSeededWorldCompetitionRank(build.WorldRank) - 1), "Aldebaran", Math.Max(1, build.Level + 2), ResolveNeighborJobName(build.JobName, -1), Math.Max(0, build.Fame + 6), Math.Max(1, build.TotalAttack + 9)),
                BuildClassCompetitionStandingLine("World", ResolveSeededWorldCompetitionRank(build.WorldRank), build.Name, Math.Max(1, build.Level), build.JobName, build.Fame, Math.Max(1, build.TotalAttack)),
                BuildClassCompetitionStandingLine("World", ResolveSeededWorldCompetitionRank(build.WorldRank) + 1, "Bellflower", Math.Max(1, build.Level - 1), ResolveNeighborJobName(build.JobName, 1), Math.Max(0, build.Fame - 3), Math.Max(1, build.TotalAttack - 5)),
                BuildClassCompetitionStandingLine("Job", Math.Max(1, ResolveSeededJobCompetitionRank(build.JobRank) - 1), "Juniper", Math.Max(1, build.Level + 1), build.JobName, Math.Max(0, build.Fame + 4), Math.Max(1, build.TotalAttack + 6)),
                BuildClassCompetitionStandingLine("Job", ResolveSeededJobCompetitionRank(build.JobRank), build.Name, Math.Max(1, build.Level), build.JobName, build.Fame, Math.Max(1, build.TotalAttack)),
                BuildClassCompetitionStandingLine("Job", ResolveSeededJobCompetitionRank(build.JobRank) + 1, "Rowan", Math.Max(1, build.Level - 2), build.JobName, Math.Max(0, build.Fame - 2), Math.Max(1, build.TotalAttack - 4))
            };
        }

        private static int ResolveSeededWorldCompetitionRank(int rank)
        {
            return rank > 0 ? rank : 57;
        }

        private static int ResolveSeededJobCompetitionRank(int rank)
        {
            return rank > 0 ? rank : 12;
        }

        private static string ResolveNeighborJobName(string jobName, int direction)
        {
            if (string.IsNullOrWhiteSpace(jobName))
            {
                return "Adventurer";
            }

            return direction < 0
                ? $"{jobName} Veteran"
                : $"{jobName} Scout";
        }

        private static string BuildClassCompetitionStandingLine(string ladderName, int rank, string name, int level, string jobName, int fame, int attack)
        {
            return $"{ladderName} {FormatClassCompetitionRank(rank)}  {name}  Lv.{Math.Max(1, level)} {jobName}  Fame {Math.Max(0, fame)}  PAD {Math.Max(1, attack)}";
        }

        private static string FormatClassCompetitionRank(int rank)
        {
            return rank > 0
                ? $"#{rank}"
                : "local only";
        }

        private static string SummarizeClassCompetitionAuthKey(string authKey)
        {
            if (string.IsNullOrWhiteSpace(authKey))
            {
                return "empty";
            }

            return authKey.Length <= 20
                ? authKey
                : $"{authKey[..12]}...{authKey[^6..]}";
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
            string cacheStamp = _lastClassCompetitionAuthIssuedTick == int.MinValue
                ? "auth cache: empty"
                : $"auth cache: {_lastClassCompetitionAuthIssuedTick} ({Math.Max(0, unchecked(currTickCount - _lastClassCompetitionAuthIssuedTick))}ms old)";
            return $"{requestStamp}, {authStamp}, {cacheStamp}, source: {_lastClassCompetitionAuthSource}";
        }

        private void RefreshClassCompetitionRuntimeState(bool forceAuthRequest = false)
        {
            if (_lastClassCompetitionOpenTick == int.MinValue && !forceAuthRequest)
            {
                return;
            }

            int now = Environment.TickCount;
            bool authExpired = _lastClassCompetitionAuthIssuedTick == int.MinValue
                || Math.Max(0, unchecked(now - _lastClassCompetitionAuthIssuedTick)) >= PacketOwnedClassCompetitionAuthLifetimeMs;
            bool shouldRequestAuth = forceAuthRequest
                || _lastClassCompetitionAuthRequestTick == int.MinValue
                || Math.Max(0, unchecked(now - _lastClassCompetitionAuthRequestTick)) >= PacketOwnedClassCompetitionAuthRefreshIntervalMs
                || authExpired;

            if (shouldRequestAuth && !_lastClassCompetitionAuthPending)
            {
                bool requiresFreshNavigation = !_lastClassCompetitionLoggedIn
                    || string.IsNullOrWhiteSpace(_lastClassCompetitionUrl);
                _lastClassCompetitionAuthRequestTick = now;
                _lastClassCompetitionAuthResponseTick = Math.Max(0, unchecked(now + PacketOwnedClassCompetitionSyntheticAuthResponseDelayMs));
                _lastClassCompetitionAuthPending = true;
                _lastClassCompetitionNavigatePending = requiresFreshNavigation;
                _lastClassCompetitionAuthDispatchStatus = DispatchClassCompetitionAuthRequest();
                if (requiresFreshNavigation)
                {
                    _lastClassCompetitionLoggedIn = false;
                    _lastClassCompetitionNavigateTick = int.MinValue;
                    _lastClassCompetitionAuthKey = string.Empty;
                    _lastClassCompetitionUrl = string.Empty;
                    _lastClassCompetitionAuthIssuedTick = int.MinValue;
                    _lastClassCompetitionAuthSource = "pending opcode 291 auth cache";
                }
            }

            if (_lastClassCompetitionAuthPending
                && _lastClassCompetitionAuthResponseTick != int.MinValue
                && Math.Max(0, unchecked(now - _lastClassCompetitionAuthResponseTick)) >= 0)
            {
                ApplyClassCompetitionAuthCache(
                    BuildClassCompetitionAuthKey(now),
                    "synthetic opcode 291 fallback");
            }
        }

        private string DispatchClassCompetitionAuthRequest()
        {
            ReadOnlySpan<byte> emptyPayload = Array.Empty<byte>();
            if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(
                PacketOwnedClassCompetitionAuthRequestOpcode,
                emptyPayload.ToArray(),
                out string dispatchStatus))
            {
                return $"Mirrored opcode {PacketOwnedClassCompetitionAuthRequestOpcode} through the live official-session bridge. {dispatchStatus}";
            }

            if (_localUtilityPacketOutbox.TrySendOutboundPacket(
                PacketOwnedClassCompetitionAuthRequestOpcode,
                emptyPayload.ToArray(),
                out string outboxStatus))
            {
                return $"Mirrored opcode {PacketOwnedClassCompetitionAuthRequestOpcode} through the generic local-utility outbox after the live bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus}";
            }

            string deferredBridgeStatus = "Official-session bridge deferred delivery is disabled.";
            if (_localUtilityOfficialSessionBridgeEnabled
                && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(
                    PacketOwnedClassCompetitionAuthRequestOpcode,
                    emptyPayload.ToArray(),
                    out deferredBridgeStatus))
            {
                return $"Queued opcode {PacketOwnedClassCompetitionAuthRequestOpcode} for deferred live official-session injection after the immediate bridge and outbox paths were unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred official bridge: {deferredBridgeStatus}";
            }

            if (_localUtilityPacketOutbox.TryQueueOutboundPacket(
                PacketOwnedClassCompetitionAuthRequestOpcode,
                emptyPayload.ToArray(),
                out string queuedStatus))
            {
                return $"Queued opcode {PacketOwnedClassCompetitionAuthRequestOpcode} for deferred generic outbox delivery after the immediate bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred official bridge: {deferredBridgeStatus} Deferred outbox: {queuedStatus}";
            }

            return $"Auth stayed simulator-owned because opcode {PacketOwnedClassCompetitionAuthRequestOpcode} could not be dispatched through the live bridge, deferred bridge, outbox, or queued outbox paths. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred official bridge: {deferredBridgeStatus}";
        }

        private void ApplyClassCompetitionAuthCache(string authKey, string source)
        {
            int now = Environment.TickCount;
            string normalizedAuthKey = authKey?.Trim() ?? string.Empty;
            _lastClassCompetitionAuthIssuedTick = now;
            _lastClassCompetitionAuthPending = false;
            _lastClassCompetitionAuthResponseTick = int.MinValue;
            _lastClassCompetitionAuthKey = normalizedAuthKey;
            _lastClassCompetitionAuthSource = string.IsNullOrWhiteSpace(source)
                ? "class-competition auth cache"
                : source.Trim();

            if (_lastClassCompetitionNavigatePending
                || !_lastClassCompetitionLoggedIn
                || string.IsNullOrWhiteSpace(_lastClassCompetitionUrl))
            {
                _lastClassCompetitionUrl = BuildClassCompetitionUrl(normalizedAuthKey);
                _lastClassCompetitionLoggedIn = true;
                _lastClassCompetitionNavigateTick = now;
            }

            _lastClassCompetitionNavigatePending = false;
        }

        private bool TryApplyPacketOwnedClassCompetitionAuthPayload(byte[] payload, out string message)
        {
            message = null;
            if (!TryDecodeClassCompetitionAuthKeyPayload(payload, out string authKey, out string decodeDetail))
            {
                message = decodeDetail ?? "Class-competition auth payload could not be decoded.";
                return false;
            }

            StampPacketOwnedUtilityRequestState();
            ApplyClassCompetitionAuthCache(authKey, "packet opcode 291 auth cache");
            message = $"Applied packet-authored class-competition auth cache from opcode {PacketOwnedClassCompetitionAuthRequestOpcode} and {(string.IsNullOrWhiteSpace(_lastClassCompetitionUrl) ? "kept the owner waiting for navigation." : "updated the cached auth token without resetting an already navigated page.")}";
            return true;
        }

        private static bool TryDecodeClassCompetitionAuthKeyPayload(byte[] payload, out string authKey, out string detail)
        {
            authKey = string.Empty;
            detail = null;
            if (payload == null || payload.Length == 0)
            {
                detail = "Class-competition auth payload is missing.";
                return false;
            }

            if (TryExtractMapleStringClassCompetitionAuthKey(payload, out authKey)
                || TryExtractLikelyClassCompetitionAuthKey(payload, Encoding.ASCII, out authKey)
                || TryExtractLikelyClassCompetitionAuthKey(payload, Encoding.Unicode, out authKey)
                || TryExtractLengthPrefixedClassCompetitionAuthKey(payload, Encoding.ASCII, out authKey)
                || TryExtractLengthPrefixedClassCompetitionAuthKey(payload, Encoding.Unicode, out authKey))
            {
                detail = $"Recovered auth token {SummarizeClassCompetitionAuthKey(authKey)}.";
                return true;
            }

            detail = $"Class-competition auth payload did not contain a recognizable auth token. Raw={Convert.ToHexString(payload)}";
            return false;
        }

        internal static bool TryDecodeClassCompetitionAuthKeyPayloadForTests(byte[] payload, out string authKey, out string detail)
        {
            return TryDecodeClassCompetitionAuthKeyPayload(payload, out authKey, out detail);
        }

        internal static bool TryDecodePacketOwnedDeliveryQuestPayloadForTests(
            byte[] payload,
            out int questId,
            out int itemId,
            out int[] disallowedQuestIds,
            out QuestDetailDeliveryType deliveryType,
            out string error)
        {
            questId = 0;
            itemId = 0;
            disallowedQuestIds = Array.Empty<int>();
            deliveryType = QuestDetailDeliveryType.None;
            error = null;

            if (payload == null || payload.Length < 8)
            {
                error = "Delivery-quest payload must contain questId and itemId Int32 values.";
                return false;
            }

            try
            {
                using var stream = new MemoryStream(payload, writable: false);
                using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: false);
                questId = reader.ReadInt32();
                itemId = reader.ReadInt32();
                disallowedQuestIds = DecodePacketOwnedDeliveryQuestIdsWithOptionalCount(reader).ToArray();
                deliveryType = DecodePacketOwnedDeliveryType(reader);
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException or InvalidDataException)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryExtractMapleStringClassCompetitionAuthKey(byte[] payload, out string authKey)
        {
            authKey = string.Empty;
            if (payload == null || payload.Length < sizeof(ushort))
            {
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
                string mapleString = ReadPacketOwnedMapleString(reader);
                if (reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    return false;
                }

                return TryNormalizeClassCompetitionAuthKey(mapleString, out authKey);
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                return false;
            }
        }

        private static bool TryExtractLengthPrefixedClassCompetitionAuthKey(byte[] payload, Encoding encoding, out string authKey)
        {
            authKey = string.Empty;
            if (payload == null || payload.Length < sizeof(short))
            {
                return false;
            }

            int byteLength = BitConverter.ToInt16(payload, 0);
            if (byteLength <= 0 || byteLength > payload.Length - sizeof(short))
            {
                return false;
            }

            return TryNormalizeClassCompetitionAuthKey(
                encoding.GetString(payload, sizeof(short), byteLength),
                out authKey);
        }

        private static bool TryExtractLikelyClassCompetitionAuthKey(byte[] payload, Encoding encoding, out string authKey)
        {
            authKey = string.Empty;
            if (payload == null || payload.Length == 0)
            {
                return false;
            }

            return TryNormalizeClassCompetitionAuthKey(encoding.GetString(payload), out authKey);
        }

        private static bool TryNormalizeClassCompetitionAuthKey(string candidate, out string authKey)
        {
            authKey = string.Empty;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            string normalized = candidate
                .Replace("\0", string.Empty, StringComparison.Ordinal)
                .Trim();
            if (normalized.Length < 8)
            {
                return false;
            }

            int separatorIndex = normalized.IndexOfAny(new[] { '\r', '\n', '\t', ' ' });
            if (separatorIndex >= 0)
            {
                normalized = normalized[..separatorIndex].Trim();
            }

            if (normalized.Length < 8)
            {
                return false;
            }

            bool hasOnlySupportedCharacters = normalized.All(ch =>
                char.IsLetterOrDigit(ch)
                || ch is '-' or '_' or '=' or '+' or '/' or '.');
            if (!hasOnlySupportedCharacters)
            {
                return false;
            }

            authKey = normalized;
            return true;
        }

        private string BuildClassCompetitionAuthKey(int issuedAtTick)
        {
            int buildId = _playerManager?.Player?.Build?.Id ?? 0;
            int worldId = Math.Max(0, _simulatorWorldId) + 1;
            int channelId = Math.Max(0, _simulatorChannelIndex) + 1;
            return $"msim-cc-{worldId:x2}{channelId:x2}-{buildId:x8}-{issuedAtTick:x8}";
        }

        private string BuildClassCompetitionUrl(string authKey)
        {
            string urlTemplate = ResolveClassCompetitionUrlTemplate(out _);
            return string.Format(
                CultureInfo.InvariantCulture,
                urlTemplate,
                PacketOwnedClassCompetitionServerHost,
                authKey ?? string.Empty);
        }

        private static string ResolveClassCompetitionUrlTemplate(out bool usedResolvedTemplate)
        {
            return MapleStoryStringPool.GetCompositeFormatOrFallback(
                PacketOwnedClassCompetitionUrlTemplateStringPoolId,
                "http://{0}.nexon.com/maplestory/page/Gnxgame.aspx?URL=Event/classbattle/gameview&key={1}",
                2,
                out usedResolvedTemplate);
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
            string enabledText = _localUtilityOfficialSessionBridgeEnabled ? "enabled" : "disabled";
            string modeText = _localUtilityOfficialSessionBridgeUseDiscovery ? "auto-discovery" : "direct proxy";
            string configuredTarget = _localUtilityOfficialSessionBridgeUseDiscovery
                ? _localUtilityOfficialSessionBridgeConfiguredLocalPort.HasValue
                    ? $"discover remote port {_localUtilityOfficialSessionBridgeConfiguredRemotePort} with local port {_localUtilityOfficialSessionBridgeConfiguredLocalPort.Value}"
                    : $"discover remote port {_localUtilityOfficialSessionBridgeConfiguredRemotePort}"
                : $"{_localUtilityOfficialSessionBridgeConfiguredRemoteHost}:{_localUtilityOfficialSessionBridgeConfiguredRemotePort}";
            string processText = string.IsNullOrWhiteSpace(_localUtilityOfficialSessionBridgeConfiguredProcessSelector)
                ? string.Empty
                : $" for {_localUtilityOfficialSessionBridgeConfiguredProcessSelector}";
            string listeningText = _localUtilityOfficialSessionBridge.IsRunning
                ? $"listening on 127.0.0.1:{_localUtilityOfficialSessionBridge.ListenPort}"
                : $"configured for 127.0.0.1:{_localUtilityOfficialSessionBridgeConfiguredListenPort}";
            return $"Local utility packet session bridge {enabledText}, {modeText}, {listeningText}, target {configuredTarget}{processText}. {_localUtilityOfficialSessionBridge.DescribeStatus()}";
        }

        private void EnsureLocalUtilityOfficialSessionBridgeState(bool shouldRun)
        {
            if (!shouldRun || !_localUtilityOfficialSessionBridgeEnabled)
            {
                if (_localUtilityOfficialSessionBridge.IsRunning)
                {
                    _localUtilityOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_localUtilityOfficialSessionBridgeConfiguredListenPort <= 0
                || _localUtilityOfficialSessionBridgeConfiguredListenPort > ushort.MaxValue)
            {
                if (_localUtilityOfficialSessionBridge.IsRunning)
                {
                    _localUtilityOfficialSessionBridge.Stop();
                }

                _localUtilityOfficialSessionBridgeEnabled = false;
                _localUtilityOfficialSessionBridgeConfiguredListenPort = LocalUtilityOfficialSessionBridgeManager.DefaultListenPort;
                return;
            }

            if (_localUtilityOfficialSessionBridgeUseDiscovery)
            {
                if (_localUtilityOfficialSessionBridgeConfiguredRemotePort <= 0
                    || _localUtilityOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue)
                {
                    if (_localUtilityOfficialSessionBridge.IsRunning)
                    {
                        _localUtilityOfficialSessionBridge.Stop();
                    }

                    return;
                }

                _localUtilityOfficialSessionBridge.TryRefreshFromDiscovery(
                    _localUtilityOfficialSessionBridgeConfiguredListenPort,
                    _localUtilityOfficialSessionBridgeConfiguredRemotePort,
                    _localUtilityOfficialSessionBridgeConfiguredProcessSelector,
                    _localUtilityOfficialSessionBridgeConfiguredLocalPort,
                    out _);
                return;
            }

            if (_localUtilityOfficialSessionBridgeConfiguredRemotePort <= 0
                || _localUtilityOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue
                || string.IsNullOrWhiteSpace(_localUtilityOfficialSessionBridgeConfiguredRemoteHost))
            {
                if (_localUtilityOfficialSessionBridge.IsRunning)
                {
                    _localUtilityOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_localUtilityOfficialSessionBridge.IsRunning
                && _localUtilityOfficialSessionBridge.ListenPort == _localUtilityOfficialSessionBridgeConfiguredListenPort
                && string.Equals(_localUtilityOfficialSessionBridge.RemoteHost, _localUtilityOfficialSessionBridgeConfiguredRemoteHost, StringComparison.OrdinalIgnoreCase)
                && _localUtilityOfficialSessionBridge.RemotePort == _localUtilityOfficialSessionBridgeConfiguredRemotePort)
            {
                return;
            }

            if (_localUtilityOfficialSessionBridge.IsRunning)
            {
                _localUtilityOfficialSessionBridge.Stop();
            }

            _localUtilityOfficialSessionBridge.Start(
                _localUtilityOfficialSessionBridgeConfiguredListenPort,
                _localUtilityOfficialSessionBridgeConfiguredRemoteHost,
                _localUtilityOfficialSessionBridgeConfiguredRemotePort);
        }

        private void RefreshLocalUtilityOfficialSessionBridgeDiscovery(int currentTickCount)
        {
            if (!_localUtilityOfficialSessionBridgeEnabled
                || !_localUtilityOfficialSessionBridgeUseDiscovery
                || _localUtilityOfficialSessionBridgeConfiguredRemotePort <= 0
                || _localUtilityOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue
                || _localUtilityOfficialSessionBridge.HasAttachedClient
                || currentTickCount < _nextLocalUtilityOfficialSessionBridgeDiscoveryRefreshAt)
            {
                return;
            }

            _nextLocalUtilityOfficialSessionBridgeDiscoveryRefreshAt =
                currentTickCount + LocalUtilityOfficialSessionBridgeDiscoveryRefreshIntervalMs;
            _localUtilityOfficialSessionBridge.TryRefreshFromDiscovery(
                _localUtilityOfficialSessionBridgeConfiguredListenPort,
                _localUtilityOfficialSessionBridgeConfiguredRemotePort,
                _localUtilityOfficialSessionBridgeConfiguredProcessSelector,
                _localUtilityOfficialSessionBridgeConfiguredLocalPort,
                out _);
        }

        private void EnsureLocalUtilityPacketOutboxState(bool shouldRun)
        {
            if (!shouldRun || !_localUtilityPacketOutboxEnabled)
            {
                if (_localUtilityPacketOutbox.IsRunning)
                {
                    _localUtilityPacketOutbox.Stop();
                }

                return;
            }

            if (_localUtilityPacketOutbox.IsRunning && _localUtilityPacketOutbox.Port == _localUtilityPacketOutboxConfiguredPort)
            {
                return;
            }

            if (_localUtilityPacketOutbox.IsRunning)
            {
                _localUtilityPacketOutbox.Stop();
            }

            try
            {
                _localUtilityPacketOutbox.Start(_localUtilityPacketOutboxConfiguredPort);
            }
            catch
            {
                _localUtilityPacketOutbox.Stop();
                throw;
            }
        }

        private string DescribeLocalUtilityPacketOutboxStatus()
        {
            string enabledText = _localUtilityPacketOutboxEnabled ? "enabled" : "disabled";
            string listeningText = _localUtilityPacketOutbox.IsRunning
                ? $"listening on 127.0.0.1:{_localUtilityPacketOutbox.Port}"
                : $"configured for 127.0.0.1:{_localUtilityPacketOutboxConfiguredPort}";
            return $"Local utility packet outbox {enabledText}, {listeningText}, sent {_localUtilityPacketOutbox.SentCount} packet(s), pending {_localUtilityPacketOutbox.PendingPacketCount} packet(s).";
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

                case LocalUtilityPacketInboxManager.ParcelDialogPacketType:
                    return TryApplyPacketOwnedParcelDialogPayload(payload, out message);

                case LocalUtilityPacketInboxManager.TrunkDialogPacketType:
                    return TryApplyPacketOwnedTrunkDialogPayload(payload, out message);

                case LocalUtilityPacketInboxManager.MessengerDispatchPacketType:
                    return TryApplyPacketOwnedMessengerDispatchPayload(payload, out message);

                case LocalUtilityPacketInboxManager.OpenUiPacketType:
                    return TryApplyPacketOwnedOpenUiPayload(payload, requireExactClientPayload: false, out message);

                case LocalUtilityPacketInboxManager.OpenUiClientPacketType:
                    return TryApplyPacketOwnedOpenUiPayload(payload, requireExactClientPayload: true, out message);

                case LocalUtilityPacketInboxManager.OpenUiWithOptionPacketType:
                    return TryApplyPacketOwnedOpenUiWithOptionPayload(payload, requireExactClientPayload: false, out message);

                case LocalUtilityPacketInboxManager.OpenUiWithOptionClientPacketType:
                    return TryApplyPacketOwnedOpenUiWithOptionPayload(payload, requireExactClientPayload: true, out message);

                case LocalUtilityPacketInboxManager.HireTutorClientPacketType:
                    return TryApplyPacketOwnedTutorHirePayload(payload, requireExactClientPayload: true, out message);

                case LocalUtilityPacketInboxManager.TutorMsgClientPacketType:
                    return TryApplyPacketOwnedTutorMessagePayload(payload, requireExactClientPayload: true, out message);

                case LocalUtilityPacketInboxManager.GoToCommoditySnPacketType:
                    return TryApplyPacketOwnedCommodityPayload(payload, requireExactClientPayload: false, out message);

                case LocalUtilityPacketInboxManager.GoToCommoditySnClientPacketType:
                    return TryApplyPacketOwnedCommodityPayload(payload, requireExactClientPayload: true, out message);

                case LocalUtilityPacketInboxManager.NoticeMsgPacketType:
                case LocalUtilityPacketInboxManager.NoticeMsgClientPacketType:
                    return TryApplyPacketOwnedNoticePayload(payload, out message);

                case LocalUtilityPacketInboxManager.ChatMsgPacketType:
                case LocalUtilityPacketInboxManager.ChatMsgClientPacketType:
                    return TryApplyPacketOwnedChatPayload(payload, out message);

                case LocalUtilityPacketInboxManager.EventCalendarEntriesPacketType:
                    return TryApplyPacketOwnedEventCalendarEntriesPayload(payload, out message);

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
                    return TryApplyPacketOwnedRadioSchedulePayload(payload, requireExactClientPayload: false, out message);

                case LocalUtilityPacketInboxManager.RadioScheduleClientPacketType:
                    return TryApplyPacketOwnedRadioSchedulePayload(payload, requireExactClientPayload: true, out message);

                  case LocalUtilityPacketInboxManager.LogoutGiftClientPacketType:
                      return TryApplyPacketOwnedLogoutGiftPayload(payload, out message);

                  case MinimapOwnerContextRuntime.PacketType:
                      return TryApplyPacketOwnedMiniMapOnOffPayload(payload, out message);

                  case PacketOwnedAntiMacroPacketType:
                      return TryApplyPacketOwnedAntiMacroPayload(payload, out message);

                case InitialQuizTimerRuntime.PacketType:
                    return TryApplyPacketOwnedInitialQuizPayload(payload, out message);

                case LocalUtilityPacketInboxManager.OpenSkillGuideClientPacketType:
                    message = ApplyPacketOwnedSkillGuideLaunch();
                    return true;

                case LocalUtilityPacketInboxManager.AskApspEventPacketType:
                case LocalUtilityPacketInboxManager.AskApspEventClientPacketType:
                    return TryApplyPacketOwnedAskApspEventPayload(payload, out message);

                case LocalUtilityPacketInboxManager.FollowCharacterPacketType:
                    return TryApplyPacketOwnedFollowCharacterPayload(payload, clientOpcodePayload: false, out message);

                case LocalUtilityPacketInboxManager.FollowCharacterClientPacketType:
                    return TryApplyPacketOwnedFollowCharacterPayload(payload, clientOpcodePayload: true, out message);

                case LocalUtilityPacketInboxManager.FollowCharacterPromptPacketType:
                    return TryApplyPacketOwnedFollowCharacterPromptPayload(payload, out message);

                case LocalUtilityPacketInboxManager.SitResultPacketType:
                    return TryApplyPacketOwnedChairSitResultPayload(payload, out message);

                case LocalUtilityPacketInboxManager.EmotionPacketType:
                    return TryApplyPacketOwnedEmotionPayload(payload, out message);

                case LocalUtilityPacketInboxManager.MesoGiveSucceededPacketType:
                    return TryApplyPacketOwnedMesoGiveSucceededPayload(payload, out message);

                case LocalUtilityPacketInboxManager.MesoGiveFailedPacketType:
                    return TryApplyPacketOwnedMesoGiveFailedPayload(payload, out message);

                case LocalUtilityPacketInboxManager.RandomMesobagSucceededPacketType:
                    return TryApplyPacketOwnedRandomMesobagSucceededPayload(payload, out message);

                case LocalUtilityPacketInboxManager.RandomMesobagFailedPacketType:
                    return TryApplyPacketOwnedRandomMesobagFailedPayload(payload, out message);

                case LocalUtilityPacketInboxManager.RandomEmotionPacketType:
                    return TryApplyPacketOwnedRandomEmotionPayload(payload, out message);

                case LocalUtilityPacketInboxManager.DragonBoxClientPacketType:
                    return TryApplyPacketOwnedDragonBoxPayload(payload, out message);

                case LocalUtilityPacketInboxManager.AccountMoreInfoPacketType:
                    return TryApplyPacketOwnedAccountMoreInfoPayload(payload, out message);

                case LocalUtilityPacketInboxManager.SetGenderPacketType:
                    return TryApplyPacketOwnedSetGenderPayload(payload, out message);

                case LocalUtilityPacketInboxManager.QuestResultPacketType:
                    return TryApplyPacketOwnedQuestResultPayload(payload, out message);

                case LocalUtilityPacketInboxManager.ResignQuestReturnClientPacketType:
                    return TryApplyPacketOwnedResignQuestReturnPayload(payload, out message);

                case LocalUtilityPacketInboxManager.PassMateNameClientPacketType:
                    return TryApplyPacketOwnedPassMateNamePayload(payload, out message);

                case LocalUtilityPacketInboxManager.SetDirectionModePacketType:
                    return TryApplyPacketOwnedDirectionModePayload(payload, out message);

                case LocalUtilityPacketInboxManager.SetStandAloneModePacketType:
                    return TryApplyPacketOwnedStandAloneModePayload(payload, out message);

                case LocalUtilityPacketInboxManager.FollowCharacterFailedPacketType:
                case LocalUtilityPacketInboxManager.FollowCharacterFailedClientPacketType:
                    return TryApplyPacketOwnedFollowCharacterFailedPayload(payload, out message);

                case LocalUtilityPacketInboxManager.PassiveMoveClientPacketType:
                    return TryApplyPacketOwnedPassiveMovePayload(payload, out message);

                case LocalUtilityPacketInboxManager.NotifyHpDecByFieldPacketType:
                    return TryApplyPacketOwnedFieldHazardPayload(payload, out message);

                case LocalUtilityPacketInboxManager.OpenClassCompetitionPagePacketType:
                    message = ApplyClassCompetitionPageLaunch();
                    return true;

                case PacketOwnedClassCompetitionAuthRequestOpcode:
                    return TryApplyPacketOwnedClassCompetitionAuthPayload(payload, out message);

                case LocalUtilityPacketInboxManager.MakerResultClientPacketType:
                    return TryApplyPacketOwnedMakerResultPayload(payload, out message);

                case LocalUtilityPacketInboxManager.ItemMakerHiddenRecipeUnlockPacketType:
                    return TryApplyPacketOwnedMakerHiddenUnlockPayload(payload, out message);

                case LocalUtilityPacketInboxManager.ItemMakerSessionPacketType:
                    return TryApplyPacketOwnedMakerSessionPayload(payload, out message);

                case LocalUtilityPacketInboxManager.DamageMeterPacketType:
                    return TryApplyPacketOwnedDamageMeterPayload(payload, out message);

                case LocalUtilityPacketInboxManager.TimeBombAttackPacketType:
                    return TryApplyPacketOwnedTimeBombAttackPayload(payload, out message);

                case LocalUtilityPacketInboxManager.VengeanceSkillApplyPacketType:
                    return TryApplyPacketOwnedVengeanceSkillApplyPayload(payload, out message);

                case LocalUtilityPacketInboxManager.ExJablinApplyPacketType:
                    return TryApplyPacketOwnedExJablinApplyPayload(payload, out message);

                case LocalUtilityPacketInboxManager.RepeatSkillModeEndAckPacketType:
                    return TryApplyPacketOwnedRepeatSkillModeEndAckPayload(payload, out message);

                case LocalUtilityPacketInboxManager.Sg88ManualAttackConfirmPacketType:
                    return TryApplyPacketOwnedSg88ManualAttackConfirmPayload(payload, out message);

                case LocalUtilityPacketInboxManager.MechanicEquipStatePacketType:
                    return TryApplyPacketOwnedMechanicEquipPayload(payload, out message);

                case LocalUtilityPacketInboxManager.RepairDurabilityResultPacketType:
                    return TryApplyRepairDurabilityResultPayload(payload, out message);

                case LocalUtilityPacketInboxManager.VegaLaunchPacketType:
                    if (TryApplyPacketOwnedEventAlarmTextPayload(payload, out message))
                    {
                        return true;
                    }

                    return TryApplyPacketOwnedVegaLaunchPayload(payload, out message);

                case LocalUtilityPacketInboxManager.VegaResultClientPacketType:
                    return TryApplyPacketOwnedVegaResultPayload(payload, out message);

                case LocalUtilityPacketInboxManager.QuestRewardRaiseOwnerSyncPacketType:
                    if (ShouldHandlePacketOwned1026AsPetConsumeResult(payload))
                    {
                        return TryApplyPacketOwnedPetConsumeResultPayload(payload, out message);
                    }

                    return TryApplyPacketOwnedQuestRewardRaisePayload(QuestRewardRaiseInboundPacketKind.OwnerSync, payload, out message);

                case LocalUtilityPacketInboxManager.QuestRewardRaisePutItemAddResultPacketType:
                    return TryApplyPacketOwnedQuestRewardRaisePayload(QuestRewardRaiseInboundPacketKind.PutItemAddResult, payload, out message);

                case LocalUtilityPacketInboxManager.QuestRewardRaisePutItemReleaseResultPacketType:
                    return TryApplyPacketOwnedQuestRewardRaisePayload(QuestRewardRaiseInboundPacketKind.PutItemReleaseResult, payload, out message);

                case LocalUtilityPacketInboxManager.QuestRewardRaisePutItemConfirmResultPacketType:
                    return TryApplyPacketOwnedQuestRewardRaisePayload(QuestRewardRaiseInboundPacketKind.PutItemConfirmResult, payload, out message);

                case LocalUtilityPacketInboxManager.QuestRewardRaiseOwnerDestroyResultPacketType:
                    return TryApplyPacketOwnedQuestRewardRaisePayload(QuestRewardRaiseInboundPacketKind.OwnerDestroyResult, payload, out message);

                case LocalUtilityPacketInboxManager.QuestGuideResultPacketType:
                    return TryApplyPacketOwnedQuestGuidePayload(payload, out message);

                case LocalUtilityPacketInboxManager.DeliveryQuestPacketType:
                    return TryApplyPacketOwnedDeliveryQuestPayload(payload, out message);

                case LocalUtilityPacketInboxManager.SkillCooltimeSetPacketType:
                    return TryApplyPacketOwnedSkillCooltimePayload(payload, out message);

                case LocalUtilityPacketInboxManager.SkillLearnItemResultClientPacketType:
                    return TryApplyPacketOwnedSkillLearnItemResultPayload(payload, out message);

                case LocalUtilityPacketInboxManager.MarriageResultPacketType:
                    return TryApplyPacketOwnedMarriageResultPayload(payload, out message);

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

        private bool TryApplyPacketOwnedMesoGiveSucceededPayload(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length < sizeof(int))
            {
                message = "Meso-give success payload must contain the mesos amount.";
                return false;
            }

            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);
            int mesoAmount = reader.ReadInt32();
            if (stream.Position != stream.Length)
            {
                message = $"Meso-give success payload has {stream.Length - stream.Position} trailing byte(s).";
                return false;
            }

            StampPacketOwnedUtilityRequestState();
            string noticeText = PacketOwnedRewardResultRuntime.FormatMesoGiveSucceededText(mesoAmount);
            ShowPacketOwnedRewardResultNotice(noticeText);
            message = $"Applied packet-owned meso-give success for {mesoAmount.ToString("N0", CultureInfo.InvariantCulture)} mesos through the dedicated reward-result notice owner.";
            return true;
        }

        private bool TryApplyPacketOwnedMesoGiveFailedPayload(byte[] payload, out string message)
        {
            message = null;
            if (payload != null && payload.Length > 0)
            {
                message = $"Meso-give failure payload should be empty, but received {payload.Length} byte(s).";
                return false;
            }

            StampPacketOwnedUtilityRequestState();
            string noticeText = PacketOwnedRewardResultRuntime.GetMesoGiveFailedText();
            ShowPacketOwnedRewardResultNotice(noticeText);
            message = "Applied packet-owned meso-give failure through the dedicated reward-result notice owner.";
            return true;
        }

        private bool TryApplyPacketOwnedRandomMesobagSucceededPayload(byte[] payload, out string message)
        {
            message = null;
            if (!PacketOwnedRewardResultRuntime.TryDecodeRandomMesoBagSucceeded(payload, out PacketOwnedRandomMesoBagResult result, out string decodeError))
            {
                message = decodeError ?? "Random-mesobag success payload could not be decoded.";
                return false;
            }

            StampPacketOwnedUtilityRequestState();
            PacketOwnedRandomMesoBagPresentation presentation = PacketOwnedRewardResultRuntime.CreateRandomMesoBagPresentation(result.Rank, result.MesoAmount);
            _chat?.AddClientChatMessage(presentation.ChatLineText, currTickCount, 12);
            ShowPacketOwnedRandomMesoBagWindow(presentation);
            if (!string.IsNullOrWhiteSpace(presentation.SoundDescriptor)
                && !TryPlayPacketOwnedWzSound(presentation.SoundDescriptor, "Item.img", out _, out _))
            {
                ShowUtilityFeedbackMessage($"Random meso sack tried to play {presentation.SoundDescriptor}, but the sound asset was unavailable.");
            }

            message = $"Applied packet-owned random meso sack success for {result.MesoAmount.ToString("N0", CultureInfo.InvariantCulture)} mesos and opened the dedicated Random Meso Bag owner.";
            return true;
        }

        private bool TryApplyPacketOwnedRandomMesobagFailedPayload(byte[] payload, out string message)
        {
            message = null;
            if (payload != null && payload.Length > 0)
            {
                message = $"Random-mesobag failure payload should be empty, but received {payload.Length} byte(s).";
                return false;
            }

            StampPacketOwnedUtilityRequestState();
            string noticeText = PacketOwnedRewardResultRuntime.GetRandomMesoBagFailedText();
            ShowPacketOwnedRewardResultNotice(noticeText);
            message = "Applied packet-owned random meso sack failure through the dedicated reward-result notice owner.";
            return true;
        }

        private bool TryApplyPacketOwnedEmotionPayload(byte[] payload, out string message)
        {
            message = null;
            PlayerCharacter player = _playerManager?.Player;
            if (player == null)
            {
                message = "Packet-owned avatar emotion requires an initialized local player.";
                return false;
            }

            if (!TryDecodePacketOwnedEmotionPayload(
                    payload,
                    out int emotionId,
                    out int durationMs,
                    out bool byItemOption,
                    out message))
            {
                return false;
            }

            StampPacketOwnedUtilityRequestState();
            return player.TryApplyPacketOwnedEmotion(
                emotionId,
                durationMs,
                byItemOption,
                Environment.TickCount,
                out message);
        }

        private bool TryApplyPacketOwnedRandomEmotionPayload(byte[] payload, out string message)
        {
            message = null;
            int currentTick = Environment.TickCount;
            if (!TryResolvePacketOwnedRandomEmotionRequest(
                    payload,
                    _packetOwnedLocalUtilityContext,
                    currentTick,
                    out PacketOwnedAvatarEmotionSelection selection,
                    out PacketOwnedLocalUtilityOutboundRequest request,
                    out message))
            {
                return false;
            }

            StampPacketOwnedUtilityRequestState();
            if (request.Opcode < 0)
            {
                message = $"Resolved area-buff item {selection.AreaBuffItemId} to packet-owned emotion '{selection.EmotionName}' ({selection.EmotionId}) with roll {selection.RandomRoll}/{selection.TotalWeight}, but the simulated CWvsContext gate suppressed SendEmotionChange(56). {_packetOwnedLocalUtilityContext.DescribeEmotionContext(currentTick)}";
                return true;
            }

            string payloadHex = Convert.ToHexString(request.Payload?.ToArray() ?? Array.Empty<byte>());
            string dispatchSummary = DescribePacketOwnedEmotionOutboundDispatch(request, payloadHex, currentTick);
            message = $"Resolved area-buff item {selection.AreaBuffItemId} to packet-owned emotion '{selection.EmotionName}' ({selection.EmotionId}) with roll {selection.RandomRoll}/{selection.TotalWeight} and mirrored CWvsContext::SendEmotionChange(56); the local avatar now waits for the inbound OnEmotion(232) echo before changing expression. {dispatchSummary}";
            return true;
        }

        internal static bool TryResolvePacketOwnedRandomEmotionRequest(
            byte[] payload,
            PacketOwnedLocalUtilityContextState contextState,
            int currentTick,
            out PacketOwnedAvatarEmotionSelection selection,
            out PacketOwnedLocalUtilityOutboundRequest request,
            out string message)
        {
            selection = default;
            request = new PacketOwnedLocalUtilityOutboundRequest(-1, 0, Array.Empty<byte>());
            message = null;

            if (contextState == null)
            {
                message = "Random-emotion routing requires a local-utility context state.";
                return false;
            }

            if (!TryDecodePacketOwnedRandomEmotionPayload(payload, out int areaBuffItemId, out message))
            {
                return false;
            }

            if (!PacketOwnedAvatarEmotionResolver.TryResolveRandomEmotion(
                    areaBuffItemId,
                    currentTick,
                    out selection,
                    out string error))
            {
                message = error ?? $"Area-buff item {areaBuffItemId} did not resolve a packet-owned random emotion.";
                return false;
            }

            if (!contextState.TryEmitEmotionChangeRequest(
                    currentTick,
                    selection.EmotionId,
                    byItemOption: false,
                    durationMs: -1,
                    out request))
            {
                request = new PacketOwnedLocalUtilityOutboundRequest(-1, 0, Array.Empty<byte>());
            }

            return true;
        }

        internal static bool TryDecodePacketOwnedEmotionPayload(
            byte[] payload,
            out int emotionId,
            out int durationMs,
            out bool byItemOption,
            out string message)
        {
            emotionId = 0;
            durationMs = 0;
            byItemOption = false;
            message = null;

            if (payload == null || payload.Length < sizeof(int) + sizeof(int) + sizeof(byte))
            {
                message = "Emotion payload must contain emotionId Int32, durationMs Int32, and byItemOption Byte values.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                emotionId = reader.ReadInt32();
                durationMs = reader.ReadInt32();
                byItemOption = reader.ReadByte() != 0;

                if (stream.Position != stream.Length)
                {
                    message = $"Emotion payload has {stream.Length - stream.Position} trailing byte(s).";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                message = $"Emotion payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        internal static bool TryDecodePacketOwnedRandomEmotionPayload(
            byte[] payload,
            out int areaBuffItemId,
            out string message)
        {
            areaBuffItemId = 0;
            message = null;

            if (payload == null || payload.Length < sizeof(int))
            {
                message = "Random-emotion payload must contain an area-buff item id Int32 value.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                areaBuffItemId = reader.ReadInt32();

                if (stream.Position != stream.Length)
                {
                    message = $"Random-emotion payload has {stream.Length - stream.Position} trailing byte(s).";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                message = $"Random-emotion payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private string DescribePacketOwnedEmotionOutboundDispatch(
            PacketOwnedLocalUtilityOutboundRequest request,
            string payloadHex,
            int currentTick)
        {
            string dispatchStatus;
            if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(request.Opcode, request.Payload, out dispatchStatus))
            {
                return $"Mirrored CWvsContext::SendEmotionChange as opcode {request.Opcode} [{payloadHex}] through the live local-utility bridge. {dispatchStatus}";
            }

            string outboxStatus;
            if (_localUtilityPacketOutbox.TrySendOutboundPacket(request.Opcode, request.Payload, out outboxStatus))
            {
                return $"Mirrored CWvsContext::SendEmotionChange as opcode {request.Opcode} [{payloadHex}] through the generic local-utility outbox after the live bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus}";
            }

            string bridgeDeferredStatus = "Official-session bridge deferred delivery is disabled.";
            if (_localUtilityOfficialSessionBridgeEnabled
                && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(request.Opcode, request.Payload, out bridgeDeferredStatus))
            {
                return $"Mirrored CWvsContext::SendEmotionChange as opcode {request.Opcode} [{payloadHex}] and queued it for deferred live official-session injection after the live bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred official bridge: {bridgeDeferredStatus}";
            }

            string queuedStatus;
            if (_localUtilityPacketOutbox.TryQueueOutboundPacket(request.Opcode, request.Payload, out queuedStatus))
            {
                return $"Mirrored CWvsContext::SendEmotionChange as opcode {request.Opcode} [{payloadHex}] and queued it for deferred generic local-utility outbox delivery after the live bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred official bridge: {bridgeDeferredStatus} Deferred outbox: {queuedStatus}";
            }

            return $"Mirrored CWvsContext::SendEmotionChange as opcode {request.Opcode} [{payloadHex}], but it remained simulator-owned because neither the live local-utility bridge nor the deferred official-session bridge queue nor the generic outbox transport or deferred outbox queue accepted it. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred official bridge: {bridgeDeferredStatus} Deferred outbox: {queuedStatus} {_packetOwnedLocalUtilityContext.DescribeEmotionContext(currentTick)}";
        }

        private void ShowPacketOwnedRewardResultNotice(string body)
        {
            string noticeSoundDescriptor = PacketOwnedRewardResultRuntime.GetUtilDlgNoticeSoundDescriptor();
            if (!string.IsNullOrWhiteSpace(noticeSoundDescriptor))
            {
                TryPlayPacketOwnedWzSound(noticeSoundDescriptor, "UI.img", out _, out _);
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.PacketOwnedRewardResultNotice) is PacketOwnedRewardNoticeWindow noticeWindow)
            {
                noticeWindow.Configure(string.Empty, body);
                ShowWindow(MapSimulatorWindowNames.PacketOwnedRewardResultNotice, noticeWindow, trackDirectionModeOwner: true);
                return;
            }

            ShowUtilityFeedbackMessage(body);
        }

        private void ShowPacketOwnedRandomMesoBagWindow(PacketOwnedRandomMesoBagPresentation presentation)
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.RandomMesoBag) is RandomMesoBagWindow randomMesoBagWindow)
            {
                randomMesoBagWindow.Configure(presentation);
                ShowWindow(MapSimulatorWindowNames.RandomMesoBag, randomMesoBagWindow, trackDirectionModeOwner: true);
                return;
            }

            ShowUtilityFeedbackMessage($"{presentation.DescriptionText} {presentation.AmountText}".Trim());
        }

        private bool TryApplyPacketOwnedResignQuestReturnPayload(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length < sizeof(ushort))
            {
                message = "Resign-quest payload must contain a quest id.";
                return false;
            }

            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);

            int questId = reader.ReadUInt16();
            if (stream.Position != stream.Length)
            {
                message = $"Resign-quest payload has {stream.Length - stream.Position} trailing byte(s).";
                return false;
            }

            StampPacketOwnedUtilityRequestState();
            _questRuntime.SetPacketOwnedAutoStartQuestRegistration(questId, registered: true);
            string questName = _questRuntime.TryGetQuestName(questId, out string resolvedQuestName)
                ? resolvedQuestName
                : $"Quest #{questId}";
            bool removedFromAlarm = uiWindowManager?.GetWindow(MapSimulatorWindowNames.QuestAlarm) is QuestAlarmWindow questAlarmWindow
                && questAlarmWindow.RemoveQuestFromPacketAlarmList(questId);
            _lastPacketOwnedResignQuestId = questId;
            _lastPacketOwnedResignQuestTick = Environment.TickCount;

            RefreshQuestUiState();
            message = removedFromAlarm
                ? $"Registered packet-owned quest resign return for {questName}, re-armed auto-start registration, and removed it from the Quest Alarm list."
                : $"Registered packet-owned quest resign return for {questName}, re-armed auto-start registration, and found no Quest Alarm entry to clear.";
            return true;
        }

        private bool TryApplyPacketOwnedPassMateNamePayload(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length < sizeof(ushort) + sizeof(short))
            {
                message = "Pass-mate-name payload must contain a quest id and mate name.";
                return false;
            }

            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);

            int questId = reader.ReadUInt16();
            if (!TryReadPacketOwnedAsciiString(reader, out string mateName))
            {
                message = "Pass-mate-name payload is missing the mate name string.";
                return false;
            }

            if (stream.Position != stream.Length)
            {
                message = $"Pass-mate-name payload has {stream.Length - stream.Position} trailing byte(s).";
                return false;
            }

            StampPacketOwnedUtilityRequestState();
            _questRuntime.SetPacketOwnedQuestMateName(questId, mateName);
            _lastPacketOwnedMateNameQuestId = questId;
            _lastPacketOwnedMateName = mateName?.Trim() ?? string.Empty;
            _lastPacketOwnedMateNameTick = Environment.TickCount;
            RefreshQuestUiState();

            string questName = _questRuntime.TryGetQuestName(questId, out string resolvedQuestName)
                ? resolvedQuestName
                : $"Quest #{questId}";
            message = string.IsNullOrWhiteSpace(mateName)
                ? $"Cleared the packet-owned mate name for {questName}."
                : $"Stored the packet-owned mate name '{mateName}' for {questName}.";
            return true;
        }

        private bool TryApplyPacketOwnedMakerResultPayload(byte[] payload, out string message)
        {
            message = null;
            if (!PacketOwnedItemMakerResultRuntime.TryDecode(payload, out PacketOwnedItemMakerResult packetResult, out string decodeError))
            {
                message = decodeError ?? "Maker-result payload could not be decoded.";
                return false;
            }

            StampPacketOwnedUtilityRequestState();
            EmitPacketOwnedMakerResultFeedback(packetResult);

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemMaker) is not ItemMakerUI itemMakerWindow)
            {
                message = "Maker-result packet arrived and emitted its client-like feedback lines, but the Item Maker window is unavailable.";
                return false;
            }

            return itemMakerWindow.TryApplyPacketOwnedResult(packetResult, out message);
        }

        private void EmitPacketOwnedMakerResultFeedback(PacketOwnedItemMakerResult packetResult)
        {
            IReadOnlyList<string> feedbackLines = PacketOwnedItemMakerResultRuntime.BuildFeedbackLines(packetResult);
            if (feedbackLines == null || feedbackLines.Count == 0)
            {
                return;
            }

            for (int i = 0; i < feedbackLines.Count; i++)
            {
                string line = feedbackLines[i];
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _chat?.AddClientChatMessage(line, currTickCount, 7);
                }
            }
        }

        private bool TryApplyPacketOwnedSkillLearnItemResultPayload(byte[] payload, out string message)
        {
            message = null;
            if (!TryDecodePacketOwnedSkillLearnItemResult(payload, out PacketOwnedSkillLearnItemResult result, out string decodeError))
            {
                message = decodeError ?? "Skill-learn-item payload could not be decoded.";
                return false;
            }

            StampPacketOwnedUtilityRequestState();

            int localCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
            bool isLocalOwner = result.CharacterId > 0 && result.CharacterId == localCharacterId;
            string ownerName = isLocalOwner
                ? ResolvePacketOwnedLocalSkillLearnOwnerName()
                : ResolvePacketOwnedRemoteCharacterName(result.CharacterId);
            if (string.IsNullOrWhiteSpace(ownerName))
            {
                ownerName = result.CharacterId > 0
                    ? $"Character {result.CharacterId.ToString(CultureInfo.InvariantCulture)}"
                    : "Unknown character";
            }

            string itemCategoryLabel = result.IsMasteryBook ? "mastery book" : "skill book";
            string itemName = ResolvePacketOwnedSkillLearnItemName(result.ItemId, itemCategoryLabel);
            int? targetMasterLevel = null;

            if (result.Used)
            {
                if (isLocalOwner)
                {
                    TryConsumePacketOwnedSkillLearnItem(result.ItemId);

                    if (result.Succeeded
                        && result.IsMasteryBook
                        && TryResolvePacketOwnedSkillLearnMasterLevel(result.ItemId, result.SkillId, out int resolvedMasterLevel))
                    {
                        targetMasterLevel = resolvedMasterLevel;
                        ApplyQuestSkillMasterLevelReward(result.SkillId, resolvedMasterLevel);
                    }

                    uiWindowManager?.ProductionEnhancementAnimationDisplayer?.PlaySkillBookResult(result.Succeeded, currTickCount);
                }
                else if (!TryRegisterPacketOwnedRemoteSkillBookResultAvatarEffect(
                             result.CharacterId,
                             result.Succeeded,
                             currTickCount,
                             out string remoteEffectMessage)
                         && !string.IsNullOrWhiteSpace(remoteEffectMessage))
                {
                    ShowUtilityFeedbackMessage(remoteEffectMessage);
                }

                TryPlaySharedProductionEnhancementSound(
                    result.Succeeded ? PacketOwnedSkillLearnSuccessSoundStringPoolId : PacketOwnedSkillLearnFailureSoundStringPoolId,
                    result.Succeeded ? PacketOwnedSkillLearnSuccessSoundFallback : PacketOwnedSkillLearnFailureSoundFallback);
            }

            string chatLine = BuildPacketOwnedSkillLearnChatLine(
                ownerName,
                itemName,
                itemCategoryLabel,
                result,
                targetMasterLevel);
            _chat?.AddClientChatMessage(chatLine, currTickCount, 12);
            ShowUtilityFeedbackMessage(chatLine);

            string ownershipLabel = isLocalOwner ? "local" : "remote";
            string outcomeLabel = !result.Used
                ? "rejected"
                : result.Succeeded
                    ? "success"
                    : "failure";
            message = $"Applied packet-owned skill-learn-item {outcomeLabel} for {ownershipLabel} owner {ownerName} ({result.CharacterId}).";
            return true;
        }

        private bool TryApplyPacketOwnedMakerHiddenUnlockPayload(byte[] payload, out string message)
        {
            message = null;
            if (!PacketOwnedItemMakerHiddenRecipeUnlockRuntime.TryDecode(payload, out PacketOwnedItemMakerHiddenRecipeUnlock packetUnlock, out string decodeError))
            {
                message = decodeError ?? "Maker-hidden-unlock payload could not be decoded.";
                return false;
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemMaker) is not ItemMakerUI itemMakerWindow)
            {
                message = "Maker-hidden-unlock packet arrived, but the Item Maker window is unavailable.";
                return false;
            }

            if (!itemMakerWindow.TryResolvePacketOwnedHiddenUnlockEntries(packetUnlock, out IReadOnlyCollection<ItemMakerRecipeProgressionEntry> unlockedEntries, out message))
            {
                return false;
            }

            if (unlockedEntries.Count > 0)
            {
                CharacterBuild build = GetActiveItemMakerCharacterBuild();
                ItemMakerProgressionSnapshot updated = _itemMakerProgressionStore.RecordUnlockedHiddenRecipes(build, unlockedEntries);
                itemMakerWindow.UpdateProgression(updated, message);
            }
            else
            {
                itemMakerWindow.ApplyPacketOwnedResult(message, refreshSlotState: false);
            }

            StampPacketOwnedUtilityRequestState();
            return true;
        }

        private bool TryApplyPacketOwnedMakerSessionPayload(byte[] payload, out string message)
        {
            message = null;
            if (!PacketOwnedItemMakerSessionRuntime.TryDecode(payload, out PacketOwnedItemMakerSession packetSession, out string decodeError))
            {
                message = decodeError ?? "Maker-session payload could not be decoded.";
                return false;
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemMaker) is not ItemMakerUI itemMakerWindow)
            {
                message = "Maker-session packet arrived, but the Item Maker window is unavailable.";
                return false;
            }

            if (!itemMakerWindow.TryApplyPacketOwnedSession(packetSession, out message))
            {
                return false;
            }

            StampPacketOwnedUtilityRequestState();
            return true;
        }

        private static bool TryDecodePacketOwnedSkillLearnItemResult(
            byte[] payload,
            out PacketOwnedSkillLearnItemResult result,
            out string error)
        {
            result = default;
            error = null;

            if (payload == null || payload.Length < 16)
            {
                error = "Skill-learn-item payload must include the exclusive-request flag, owner id, item metadata, and result bytes.";
                return false;
            }

            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: false);
            result = new PacketOwnedSkillLearnItemResult(
                OnExclusiveRequest: reader.ReadByte() != 0,
                CharacterId: reader.ReadInt32(),
                IsMasteryBook: reader.ReadByte() != 0,
                ItemId: reader.ReadInt32(),
                SkillId: reader.ReadInt32(),
                Used: reader.ReadByte() != 0,
                Succeeded: reader.ReadByte() != 0);
            return true;
        }

        private string ResolvePacketOwnedLocalSkillLearnOwnerName()
        {
            string playerName = _playerManager?.Player?.Name;
            if (!string.IsNullOrWhiteSpace(playerName))
            {
                return playerName;
            }

            string buildName = _playerManager?.Player?.Build?.Name;
            return string.IsNullOrWhiteSpace(buildName) ? "You" : buildName;
        }

        private string ResolvePacketOwnedSkillLearnItemName(int itemId, string fallbackCategoryLabel)
        {
            if (itemId > 0
                && InventoryItemMetadataResolver.TryResolveItemName(itemId, out string itemName)
                && !string.IsNullOrWhiteSpace(itemName))
            {
                return itemName;
            }

            return string.IsNullOrWhiteSpace(fallbackCategoryLabel)
                ? $"item #{itemId.ToString(CultureInfo.InvariantCulture)}"
                : fallbackCategoryLabel;
        }

        private void TryConsumePacketOwnedSkillLearnItem(int itemId)
        {
            if (itemId <= 0)
            {
                return;
            }

            InventoryType inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(itemId);
            if (inventoryType == InventoryType.NONE)
            {
                return;
            }

            TryConsumeInventoryWindowItem(itemId, 1);
        }

        private static bool TryResolvePacketOwnedSkillLearnMasterLevel(int itemId, int skillId, out int masterLevel)
        {
            masterLevel = 0;
            if (!InventoryItemMetadataResolver.TryResolveSkillBookUseMetadata(itemId, out SkillBookUseMetadata metadata)
                || !metadata.IsValid)
            {
                return false;
            }

            if (skillId > 0 && metadata.SkillIds.Count > 0 && !metadata.SkillIds.Contains(skillId))
            {
                return false;
            }

            masterLevel = metadata.MasterLevel;
            return masterLevel > 0;
        }

        private string BuildPacketOwnedSkillLearnChatLine(
            string ownerName,
            string itemName,
            string itemCategoryLabel,
            PacketOwnedSkillLearnItemResult result,
            int? targetMasterLevel)
        {
            string stringPoolItemLabel = ResolvePacketOwnedSkillLearnItemLabel(result.IsMasteryBook, itemCategoryLabel);
            if (!result.Used)
            {
                return FormatPacketOwnedSkillLearnNotice(
                    PacketOwnedSkillLearnCannotUseStringPoolId,
                    "You cannot use {0}.",
                    stringPoolItemLabel);
            }

            if (result.Succeeded)
            {
                return MapleStoryStringPool.GetOrFallback(
                    result.IsMasteryBook
                        ? PacketOwnedSkillLearnMasterySuccessNoticeStringPoolId
                        : PacketOwnedSkillLearnSkillSuccessNoticeStringPoolId,
                    result.IsMasteryBook
                        ? "The Book of Mastery glows brightly, and the current skills have gone through an upgrade."
                        : "The Skill Book glows brightly, and new skills have now been added.");
            }

            return FormatPacketOwnedSkillLearnNotice(
                PacketOwnedSkillLearnFailureNoticeStringPoolId,
                "Despite using {0}, the effect was nowhere to be found.",
                stringPoolItemLabel);
        }

        private static string ResolvePacketOwnedSkillLearnItemLabel(bool isMasteryBook, string fallbackCategoryLabel)
        {
            int stringPoolId = isMasteryBook
                ? PacketOwnedSkillLearnMasteryBookLabelStringPoolId
                : PacketOwnedSkillLearnSkillBookLabelStringPoolId;
            return MapleStoryStringPool.GetOrFallback(
                stringPoolId,
                string.IsNullOrWhiteSpace(fallbackCategoryLabel)
                    ? isMasteryBook ? "Mastery Book" : "Skill Book"
                    : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(fallbackCategoryLabel));
        }

        private static string FormatPacketOwnedSkillLearnNotice(int stringPoolId, string fallbackFormat, string itemLabel)
        {
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                stringPoolId,
                fallbackFormat,
                maxPlaceholderCount: 1,
                out _);
            return string.Format(CultureInfo.InvariantCulture, format, itemLabel);
        }

        private static bool TryReadPacketOwnedAsciiString(BinaryReader reader, out string value)
        {
            value = string.Empty;
            Stream stream = reader?.BaseStream;
            if (stream == null || stream.Length - stream.Position < sizeof(short))
            {
                return false;
            }

            short length = reader.ReadInt16();
            if (length < 0 || stream.Length - stream.Position < length)
            {
                return false;
            }

            value = Encoding.ASCII.GetString(reader.ReadBytes(length)).Trim();
            return true;
        }

        private static void AppendQuestIds(HashSet<int> questIds, QuestLogSnapshot snapshot)
        {
            if (questIds == null || snapshot?.Entries == null)
            {
                return;
            }

            foreach (QuestLogEntrySnapshot entry in snapshot.Entries)
            {
                if (entry != null && entry.QuestId > 0)
                {
                    questIds.Add(entry.QuestId);
                }
            }
        }

        private string BuildPacketOwnedChairCorrectionMessage(PlayerCharacter player, int seatIndex, string reason, int currentTick)
        {
            if (_packetOwnedLocalUtilityContext.TryEmitChairGetUpRequest(currentTick, player?.HP ?? 0, timeIntervalMs: 0, out PacketOwnedLocalUtilityOutboundRequest request))
            {
                string payloadHex = BitConverter.ToString(request.Payload.ToArray()).Replace("-", string.Empty);
                if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(request.Opcode, request.Payload, out string dispatchStatus))
                {
                    return $"Packet-owned sit result returned seat {seatIndex}, but {reason}. Forced a stand-up correction, emitted GetUpFromChairRequest(0) as opcode {request.Opcode} [{payloadHex}], and dispatched it through the live local-utility bridge. {dispatchStatus}";
                }

                if (_localUtilityPacketOutbox.TrySendOutboundPacket(request.Opcode, request.Payload, out string outboxStatus))
                {
                    return $"Packet-owned sit result returned seat {seatIndex}, but {reason}. Forced a stand-up correction, emitted GetUpFromChairRequest(0) as opcode {request.Opcode} [{payloadHex}], and dispatched it through the generic local-utility outbox transport after the live bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus}";
                }

                string bridgeDeferredStatus = "Official-session bridge deferred delivery is disabled.";
                if (_localUtilityOfficialSessionBridgeEnabled
                    && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(request.Opcode, request.Payload, out bridgeDeferredStatus))
                {
                    return $"Packet-owned sit result returned seat {seatIndex}, but {reason}. Forced a stand-up correction, emitted GetUpFromChairRequest(0) as opcode {request.Opcode} [{payloadHex}], and queued it for deferred live official-session injection after the live bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred official bridge: {bridgeDeferredStatus}";
                }

                if (_localUtilityPacketOutbox.TryQueueOutboundPacket(request.Opcode, request.Payload, out string queuedStatus))
                {
                    return $"Packet-owned sit result returned seat {seatIndex}, but {reason}. Forced a stand-up correction, emitted GetUpFromChairRequest(0) as opcode {request.Opcode} [{payloadHex}], and queued it for deferred generic local-utility outbox delivery after the live bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred official bridge: {bridgeDeferredStatus} Deferred outbox: {queuedStatus}";
                }

                return $"Packet-owned sit result returned seat {seatIndex}, but {reason}. Forced a stand-up correction and emitted GetUpFromChairRequest(0) as opcode {request.Opcode} [{payloadHex}], but it remained simulator-owned because neither the live local-utility bridge nor the deferred official-session bridge queue nor the generic outbox transport or deferred outbox queue accepted the outbound packet. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred official bridge: {bridgeDeferredStatus} Deferred outbox: {queuedStatus}";
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
            if (payload == null || payload.Length < 8)
            {
                message = "Delivery-quest payload must contain questId and itemId Int32 values.";
                return false;
            }

            try
            {
                using var stream = new MemoryStream(payload, writable: false);
                using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: false);
                int questId = reader.ReadInt32();
                int itemId = reader.ReadInt32();
                List<int> disallowedQuestIds = DecodePacketOwnedDeliveryQuestIdsWithOptionalCount(reader);
                QuestDetailDeliveryType packetOwnedDeliveryType = DecodePacketOwnedDeliveryType(reader);
                message = ApplyDeliveryQuestLaunch(questId, itemId, disallowedQuestIds, packetOwnedDeliveryType);
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                message = $"Delivery-quest payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private bool TryApplyPacketOwnedMiniMapOnOffPayload(byte[] payload, out string message)
        {
            if (!MinimapOwnerContextRuntime.TryDecodePayload(payload, out PacketOwnedMiniMapOnOffResult result, out string error))
            {
                message = error ?? "MiniMapOnOff payload could not be decoded.";
                return false;
            }

            _packetOwnedMiniMapOnOffVisible = result.IsMiniMapVisible;
            miniMapUi?.ReloadMiniMap(result.IsMiniMapVisible);
            message = result.Summary;
            return true;
        }

        private static List<int> DecodePacketOwnedDeliveryQuestIdsWithOptionalCount(BinaryReader reader)
        {
            List<int> questIds = new();
            if (reader == null)
            {
                return questIds;
            }

            int remaining = (int)(reader.BaseStream.Length - reader.BaseStream.Position);
            if (remaining <= 0)
            {
                return questIds;
            }

            long listStart = reader.BaseStream.Position;
            if (remaining >= sizeof(int))
            {
                int count = reader.ReadInt32();
                int afterCount = (int)(reader.BaseStream.Length - reader.BaseStream.Position);
                if (count < 0)
                {
                    throw new InvalidDataException("Delivery-quest payload declared a negative disallowed-quest count.");
                }

                if (count == 0)
                {
                    return questIds;
                }

                if (afterCount == count * sizeof(int)
                    || afterCount == (count * sizeof(int)) + sizeof(byte)
                    || afterCount == (count * sizeof(int)) + sizeof(short)
                    || afterCount == (count * sizeof(int)) + sizeof(int))
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

                if (afterCount == count * sizeof(short)
                    || afterCount == (count * sizeof(short)) + sizeof(byte)
                    || afterCount == (count * sizeof(short)) + sizeof(short)
                    || afterCount == (count * sizeof(short)) + sizeof(int))
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

                reader.BaseStream.Position = listStart;
            }

            return DecodePacketOwnedDisallowedDeliveryQuestIds(reader);
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
            string socialUtilityStatus = GetPacketOwnedSocialUtilityDialogDispatcher().DescribeStatus(currentTickCount);
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
                ? $"Quest demand items: {_lastQuestDemandQueryVisibleItemIds.Count} visible for quest #{_lastQuestDemandItemQueryQuestId}, {_lastQuestDemandQueryHiddenItemCount} hidden, {_lastQuestDemandQueryVisibleItemMapIds.Sum(entry => entry.Value?.Count ?? 0)} map result(s)."
                : "Quest demand items: none.";
            string helperStatus = DescribePacketOwnedQuestHelperStatus(currentTickCount);
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
            string passiveMoveStatus = _lastPacketOwnedPassiveMoveTick == int.MinValue
                ? string.Empty
                : $" PassiveMove269@{_lastPacketOwnedPassiveMoveTick}.";
            string followPromptStatus = _packetOwnedFollowPromptActive
                ? $" Follow prompt: {_packetOwnedFollowPromptOwner} requester {_localFollowRuntime.IncomingRequesterId}."
                : string.Empty;
            string followStatus = string.IsNullOrWhiteSpace(_lastPacketOwnedFollowFailureMessage)
                ? $"{localFollowStatus}{followPromptStatus}"
                : _lastPacketOwnedFollowFailureReason.HasValue
                    ? $"{localFollowStatus}{followPromptStatus}{passiveMoveStatus} Follow failure: reason {_lastPacketOwnedFollowFailureReason.Value}, driver {_lastPacketOwnedFollowFailureDriverId}, cleared={_lastPacketOwnedFollowFailureClearedPending}. {TruncatePacketOwnedUtilityText(_lastPacketOwnedFollowFailureMessage)}"
                    : $"{localFollowStatus}{followPromptStatus}{passiveMoveStatus} Follow failure: {TruncatePacketOwnedUtilityText(_lastPacketOwnedFollowFailureMessage)}";
            if (string.IsNullOrWhiteSpace(_lastPacketOwnedFollowFailureMessage) && !string.IsNullOrWhiteSpace(passiveMoveStatus))
            {
                followStatus = $"{localFollowStatus}{followPromptStatus}{passiveMoveStatus}";
            }
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
                socialUtilityStatus,
                openUiStatus,
                commodityStatus,
                requestStampStatus,
                guideStatus,
                questDemandStatus,
                helperStatus,
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

        private PacketOwnedSocialUtilityDialogDispatcher GetPacketOwnedSocialUtilityDialogDispatcher()
        {
            return _packetOwnedSocialUtilityDialogDispatcher ??=
                new PacketOwnedSocialUtilityDialogDispatcher(
                    _mapleTvRuntime,
                    _memoMailbox,
                    ResolvePacketOwnedTrunkStorageRuntime,
                    _messengerRuntime);
        }

        private SimulatorStorageRuntime ResolvePacketOwnedTrunkStorageRuntime()
        {
            return uiWindowManager?.GetWindow(MapSimulatorWindowNames.Trunk) is TrunkUI trunkWindow
                ? trunkWindow.StorageRuntime as SimulatorStorageRuntime
                : null;
        }

        private bool TryApplyPacketOwnedParcelDialogPayload(byte[] payload, out string message)
        {
            bool parcelWindowWasVisible = uiWindowManager?.GetWindow(MapSimulatorWindowNames.MemoMailbox)?.IsVisible == true;
            bool applied = GetPacketOwnedSocialUtilityDialogDispatcher().TryApplyParcelPacket(payload, out message);
            if (applied
                && (GetPacketOwnedSocialUtilityDialogDispatcher().ShouldShowParcelOwnerAfterLastPacket || parcelWindowWasVisible))
            {
                TryOpenFieldRestrictedWindow(
                    MapSimulatorWindowNames.MemoMailbox,
                    inheritDirectionModeOwner: true);
            }

            return applied;
        }

        private bool TryApplyPacketOwnedTrunkDialogPayload(byte[] payload, out string message)
        {
            bool applied = GetPacketOwnedSocialUtilityDialogDispatcher().TryApplyTrunkPacket(payload, out message);
            if (applied)
            {
                ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.Trunk);
            }

            return applied;
        }

        private bool TryApplyPacketOwnedMessengerDispatchPayload(byte[] payload, out string message)
        {
            bool applied = GetPacketOwnedSocialUtilityDialogDispatcher().TryApplyMessengerDispatchPacket(payload, out message);
            if (applied)
            {
                ShowMessengerWindow();
            }

            return applied;
        }

        private bool TryApplyPacketOwnedMessengerPacket(MessengerPacketType packetType, byte[] payload, out string message)
        {
            bool applied = GetPacketOwnedSocialUtilityDialogDispatcher().TryApplyMessengerPacket(packetType, payload, out message);
            if (applied)
            {
                ShowMessengerWindow();
            }

            return applied;
        }

        private string DescribePacketOwnedQuestHelperStatus(int currentTickCount)
        {
            string resignStatus = _lastPacketOwnedResignQuestTick == int.MinValue
                ? "Quest resign return: none."
                : _questRuntime.TryGetQuestName(_lastPacketOwnedResignQuestId, out string resignQuestName)
                    ? $"Quest resign return: {resignQuestName} (#{_lastPacketOwnedResignQuestId}), age={Math.Max(0, unchecked(currentTickCount - _lastPacketOwnedResignQuestTick))} ms, auto-start registered={_questRuntime.IsPacketOwnedAutoStartQuestRegistered(_lastPacketOwnedResignQuestId).ToString().ToLowerInvariant()}."
                    : $"Quest resign return: quest #{_lastPacketOwnedResignQuestId}, age={Math.Max(0, unchecked(currentTickCount - _lastPacketOwnedResignQuestTick))} ms, auto-start registered={_questRuntime.IsPacketOwnedAutoStartQuestRegistered(_lastPacketOwnedResignQuestId).ToString().ToLowerInvariant()}.";
            string mateStatus = _lastPacketOwnedMateNameTick == int.MinValue
                ? "Pass mate-name: none."
                : _questRuntime.TryGetQuestName(_lastPacketOwnedMateNameQuestId, out string mateQuestName)
                    ? string.IsNullOrWhiteSpace(_lastPacketOwnedMateName)
                        ? $"Pass mate-name: cleared for {mateQuestName} (#{_lastPacketOwnedMateNameQuestId}), age={Math.Max(0, unchecked(currentTickCount - _lastPacketOwnedMateNameTick))} ms."
                        : $"Pass mate-name: '{TruncatePacketOwnedUtilityText(_lastPacketOwnedMateName, 40)}' for {mateQuestName} (#{_lastPacketOwnedMateNameQuestId}), age={Math.Max(0, unchecked(currentTickCount - _lastPacketOwnedMateNameTick))} ms."
                    : string.IsNullOrWhiteSpace(_lastPacketOwnedMateName)
                        ? $"Pass mate-name: cleared for quest #{_lastPacketOwnedMateNameQuestId}, age={Math.Max(0, unchecked(currentTickCount - _lastPacketOwnedMateNameTick))} ms."
                        : $"Pass mate-name: '{TruncatePacketOwnedUtilityText(_lastPacketOwnedMateName, 40)}' for quest #{_lastPacketOwnedMateNameQuestId}, age={Math.Max(0, unchecked(currentTickCount - _lastPacketOwnedMateNameTick))} ms.";
            return $"{resignStatus} {mateStatus}";
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
            string radioCreateLayerContextStatus = DescribePacketOwnedRadioCreateLayerContextStatus();
            string teleportPortalNames = !string.IsNullOrWhiteSpace(_lastPacketOwnedTeleportSourcePortalName)
                || !string.IsNullOrWhiteSpace(_lastPacketOwnedTeleportTargetPortalName)
                    ? $"{_lastPacketOwnedTeleportSourcePortalName ?? "?"}->{_lastPacketOwnedTeleportTargetPortalName ?? "?"}"
                    : "none";
            string teleportEffectStatus = _lastPacketOwnedTeleportEffectTick == int.MinValue
                ? "idle"
                : $"{_lastPacketOwnedTeleportEffectPath ?? "unresolved"} age={Math.Max(0, unchecked(currentTickCount - _lastPacketOwnedTeleportEffectTick))} ms";
            string teleportRegistrationStatus = _lastPacketOwnedTeleportRegistrationTick == int.MinValue
                ? "forced registration=idle"
                : $"forced registration age={Math.Max(0, unchecked(currentTickCount - _lastPacketOwnedTeleportRegistrationTick))} ms, movePathAttr={_lastPacketOwnedTeleportMovePathAttribute}, setItemBackground={(_lastPacketOwnedTeleportSetItemBackgroundActive ? "1/1" : "0/0")}, effect={teleportEffectStatus}";
            string teleportOutboundSummary = string.IsNullOrWhiteSpace(_lastPacketOwnedTeleportOutboundSummary)
                ? string.Empty
                : $" ({TruncatePacketOwnedUtilityText(_lastPacketOwnedTeleportOutboundSummary, 180)})";
            string teleportOutboundStatus = _lastPacketOwnedTeleportOutboundOpcode < 0
                ? "outbound portal request=unresolved"
                : $"outbound portal request={_lastPacketOwnedTeleportOutboundOpcode}[{Convert.ToHexString(_lastPacketOwnedTeleportOutboundPayload ?? Array.Empty<byte>())}]{teleportOutboundSummary}";
            string teleportStatus = _lastPacketOwnedTeleportPortalRequestTick == int.MinValue
                ? $"Teleport request active={_packetOwnedTeleportRequestActive.ToString().ToLowerInvariant()}, last portal request=none, cooldown={IsPacketOwnedTeleportRegistrationCoolingDown(currentTickCount).ToString().ToLowerInvariant()}."
                : $"Teleport request active={_packetOwnedTeleportRequestActive.ToString().ToLowerInvariant()}, last portal request age={Math.Max(0, unchecked(currentTickCount - _lastPacketOwnedTeleportPortalRequestTick))} ms, handoff={teleportPortalNames}, portalIndex={_lastPacketOwnedTeleportPortalIndex}, cooldown={IsPacketOwnedTeleportRegistrationCoolingDown(currentTickCount).ToString().ToLowerInvariant()}, {teleportRegistrationStatus}, {teleportOutboundStatus}.";
            return $"{directionStatus} {standAloneStatus} {chairStatus} {radioCreateLayerContextStatus} {teleportStatus}";
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

            if (string.Equals(windowName, MapSimulatorWindowNames.Ranking, StringComparison.Ordinal)
                || string.Equals(windowName, MapSimulatorWindowNames.Event, StringComparison.Ordinal))
            {
                ShowRecordedUtilityWindow(windowName, $"packet-owned {displayName}");
                return $"Opened packet-owned {displayName}.";
            }

            ShowWindow(windowName, window, trackDirectionModeOwner: true);
            return $"Opened packet-owned {displayName}.";
        }

        private string ApplyPacketOwnedGoToCommoditySn(int commoditySerialNumber)
        {
            StampPacketOwnedUtilityRequestState();
            _lastPacketOwnedCommoditySerialNumber = Math.Max(0, commoditySerialNumber);
            _lastPacketOwnedCommodityRequestTick = Environment.TickCount;

            OpenCashServiceOwnerFamily(UI.CashServiceStageKind.CashShop, resetStageSession: false);
            bool focusedCommodity = TryFocusCashServiceCommodity(_lastPacketOwnedCommoditySerialNumber);

            string message = focusedCommodity
                ? $"Stored packet-owned commodity SN {_lastPacketOwnedCommoditySerialNumber}, requested shop migration, opened the staged Cash Shop owner family, and focused the matching Cash Shop row."
                : $"Stored packet-owned commodity SN {_lastPacketOwnedCommoditySerialNumber}, requested shop migration, and opened the staged Cash Shop owner family.";
            ShowUtilityFeedbackMessage(message);
            return message;
        }

        private bool TryApplyPacketOwnedRadioSchedulePayload(byte[] payload, bool requireExactClientPayload, out string message)
        {
            if (TryDecodePacketOwnedRadioSchedulePayload(
                    payload,
                    requireExactClientPayload,
                    out string trackDescriptor,
                    out int timeValue,
                    out message))
            {
                message = ApplyPacketOwnedRadioSchedule(trackDescriptor, timeValue);
                return true;
            }

            if (!requireExactClientPayload && TryDecodePacketOwnedStringPayload(payload, out string descriptor))
            {
                message = ApplyPacketOwnedRadioSchedule(descriptor, 0);
                return true;
            }

            return false;
        }

        internal static bool TryDecodePacketOwnedRadioSchedulePayload(
            byte[] payload,
            bool requireExactClientPayload,
            out string trackDescriptor,
            out int timeValue,
            out string message)
        {
            trackDescriptor = null;
            timeValue = 0;
            message = "Radio-schedule payload is missing.";
            if (payload == null || payload.Length == 0)
            {
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
                trackDescriptor = ReadPacketOwnedMapleString(reader);
                if (requireExactClientPayload && reader.BaseStream.Length - reader.BaseStream.Position < sizeof(int))
                {
                    message = "Radio-schedule client payload must match CUserLocal::OnRadioSchedule: DecodeStr followed by Decode4 timeValue.";
                    return false;
                }

                timeValue = reader.BaseStream.Position <= reader.BaseStream.Length - sizeof(int)
                    ? reader.ReadInt32()
                    : 0;
                if (requireExactClientPayload && reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    message = "Radio-schedule client payload contained trailing bytes after DecodeStr and Decode4 timeValue.";
                    return false;
                }

                message = "Decoded packet-owned radio schedule payload.";
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                message = "Radio-schedule payload could not be decoded.";
                return false;
            }
        }

        private string ApplyPacketOwnedRadioSchedule(string trackDescriptor, int timeValue)
        {
            StampPacketOwnedUtilityRequestState();
            SyncPacketOwnedRadioCreateLayerContextLifecycle();

            string normalizedTrackDescriptor = trackDescriptor?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedTrackDescriptor))
            {
                const string emptyMessage = "Packet-owned radio schedule did not include a track descriptor.";
                _lastPacketOwnedRadioStatusMessage = emptyMessage;
                NotifyEventAlarmOwnerActivity("packet-owned radio schedule");
                ShowUtilityFeedbackMessage(emptyMessage);
                return emptyMessage;
            }

            if (IsPacketOwnedRadioPlaying())
            {
                string ignoreMessage = $"Ignored packet-owned radio schedule for {normalizedTrackDescriptor} because a radio session is already active.";
                _lastPacketOwnedRadioStatusMessage = ignoreMessage;
                return ignoreMessage;
            }

            if (!TryResolvePacketOwnedRadioTrack(
                normalizedTrackDescriptor,
                out PacketOwnedRadioTrackResolution trackResolution))
            {
                string missingMessage = $"Packet-owned radio track '{normalizedTrackDescriptor}' could not be resolved in the loaded Sound/*.img data.";
                _lastPacketOwnedRadioStatusMessage = missingMessage;
                NotifyEventAlarmOwnerActivity("packet-owned radio schedule");
                ShowUtilityFeedbackMessage(missingMessage);
                return missingMessage;
            }

            try
            {
                _packetOwnedRadioAudio?.Dispose();
                if (!TryResolvePacketOwnedRadioPlaybackOffset(
                        timeValue,
                        out int normalizedTimeValue,
                        out int startOffsetMs))
                {
                    const string invalidOffsetMessage = "Packet-owned radio schedule did not contain a usable playback offset.";
                    _lastPacketOwnedRadioStatusMessage = invalidOffsetMessage;
                    NotifyEventAlarmOwnerActivity("packet-owned radio schedule");
                    ShowUtilityFeedbackMessage(invalidOffsetMessage);
                    return invalidOffsetMessage;
                }

                _packetOwnedRadioAudio = new MonoGameBgmPlayer(trackResolution.AudioProperty, looped: false, startOffsetMs);
                int trackDurationMs = (int)Math.Clamp(
                    Math.Round(_packetOwnedRadioAudio.Duration.TotalMilliseconds),
                    0d,
                    int.MaxValue);
                if (!IsPacketOwnedRadioPlaybackOffsetUsable(startOffsetMs, trackDurationMs))
                {
                    _packetOwnedRadioAudio.Dispose();
                    _packetOwnedRadioAudio = null;
                    ResetPacketOwnedRadioCreateLayerSessionState();
                    string rejectedMessage = trackDurationMs > 0
                        ? $"Ignored packet-owned radio schedule for {trackResolution.DisplayName} because authored timeValue {normalizedTimeValue}s starts at {startOffsetMs} ms, past the {trackDurationMs} ms track length."
                        : $"Ignored packet-owned radio schedule for {trackResolution.DisplayName} because the resolved track length is unavailable.";
                    _lastPacketOwnedRadioStatusMessage = rejectedMessage;
                    NotifyEventAlarmOwnerActivity("packet-owned radio schedule");
                    ShowUtilityFeedbackMessage(rejectedMessage);
                    return rejectedMessage;
                }

                int remainingDurationMs = Math.Max(0, trackDurationMs - startOffsetMs);

                int startTick = Environment.TickCount;
                _lastPacketOwnedRadioTrackDescriptor = normalizedTrackDescriptor;
                _lastPacketOwnedRadioResolvedTrackDescriptor = trackResolution.ResolvedTrackDescriptor;
                _lastPacketOwnedRadioResolvedDescriptor = trackResolution.ResolvedAudioDescriptor;
                _lastPacketOwnedRadioDisplayName = trackResolution.DisplayName;
                _lastPacketOwnedRadioTimeValue = normalizedTimeValue;
                _lastPacketOwnedRadioStartOffsetMs = startOffsetMs;
                _lastPacketOwnedRadioTrackDurationMs = trackDurationMs;
                _lastPacketOwnedRadioAvailableDurationMs = remainingDurationMs;
                _lastPacketOwnedRadioStartTick = startTick;
                CapturePacketOwnedRadioCreateLayerSessionState();
                _lastPacketOwnedRadioExpectedStopTick = ResolvePacketOwnedRadioExpectedStopTick(
                    startTick,
                    _lastPacketOwnedRadioAvailableDurationMs);
                _lastPacketOwnedRadioLastPollTick = int.MinValue;
                _appliedPacketOwnedRadioVolume = 0f;
                _utilityAudioMixLastTick = startTick;
                _lastPacketOwnedRadioStatusMessage =
                    $"Radio play active at {normalizedTimeValue}s via " +
                    $"{RadioOwnerStringPoolText.FormatNotice(PacketOwnedRadioStartStringPoolId, trackResolution.DisplayName, appendFallbackSuffix: true)}.";
                NotifyEventAlarmOwnerActivity("packet-owned radio schedule");

                _packetOwnedRadioAudio.Play();
                ApplyUtilityAudioSettings();
                ShowPacketOwnedRadioWindow();
                _chat?.AddClientChatMessage(
                    FormatPacketOwnedRadioChatMessage(PacketOwnedRadioStartStringPoolId, trackResolution.DisplayName),
                    Environment.TickCount,
                    12);

                string message =
                    $"Started packet-owned radio playback for {trackResolution.DisplayName} " +
                    $"via {FormatPacketOwnedRadioTemplateResolution(PacketOwnedRadioAudioTemplateStringPoolId, normalizedTrackDescriptor, trackResolution.ResolvedAudioDescriptor)}.";
                ShowUtilityFeedbackMessage(message);
                return message;
            }
            catch (Exception ex)
            {
                _packetOwnedRadioAudio?.Dispose();
                _packetOwnedRadioAudio = null;
                ResetPacketOwnedRadioCreateLayerSessionState();
                string failedMessage = $"Packet-owned radio track '{normalizedTrackDescriptor}' could not start: {ex.Message}";
                _lastPacketOwnedRadioStatusMessage = failedMessage;
                NotifyEventAlarmOwnerActivity("packet-owned radio schedule");
                ShowUtilityFeedbackMessage(failedMessage);
                return failedMessage;
            }
        }

        internal static bool TryResolvePacketOwnedRadioPlaybackWindow(
            int timeValue,
            int trackDurationMs,
            out int normalizedTimeValue,
            out int startOffsetMs,
            out int remainingDurationMs)
        {
            if (!TryResolvePacketOwnedRadioPlaybackOffset(
                    timeValue,
                    out normalizedTimeValue,
                    out startOffsetMs))
            {
                remainingDurationMs = 0;
                return false;
            }

            remainingDurationMs = Math.Max(0, trackDurationMs - startOffsetMs);
            return true;
        }

        internal static bool TryResolvePacketOwnedRadioPlaybackOffset(int timeValue, out int normalizedTimeValue, out int startOffsetMs)
        {
            normalizedTimeValue = Math.Max(0, timeValue);
            if (normalizedTimeValue > int.MaxValue / 1000)
            {
                startOffsetMs = int.MaxValue;
                return false;
            }

            startOffsetMs = normalizedTimeValue * 1000;
            return true;
        }

        internal static bool IsPacketOwnedRadioPlaybackOffsetUsable(int startOffsetMs, int availableDurationMs)
        {
            return startOffsetMs >= 0
                && availableDurationMs >= 0
                && startOffsetMs <= availableDurationMs;
        }

        internal static int ResolvePacketOwnedRadioExpectedStopTick(int startTick, int remainingDurationMs)
        {
            long expectedStopTick = (long)startTick + Math.Max(0, remainingDurationMs);
            return expectedStopTick > int.MaxValue
                ? int.MaxValue
                : expectedStopTick < int.MinValue
                    ? int.MinValue
                    : (int)expectedStopTick;
        }

        private void UpdatePacketOwnedRadioSchedule(int currentTickCount)
        {
            SyncPacketOwnedRadioCreateLayerContextLifecycle();
            if (!IsPacketOwnedRadioPlaying())
            {
                return;
            }

            int pollBaseline = _lastPacketOwnedRadioLastPollTick != int.MinValue
                ? _lastPacketOwnedRadioLastPollTick
                : (_lastPacketOwnedRadioExpectedStopTick != int.MinValue
                    ? _lastPacketOwnedRadioExpectedStopTick
                    : _lastPacketOwnedRadioStartTick);
            if (pollBaseline != int.MinValue
                && unchecked(currentTickCount - pollBaseline) <= PacketOwnedRadioUpdatePollIntervalMs)
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
            SyncPacketOwnedTutorLifecycle(currentTickCount);
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
            _lastPacketOwnedRadioTrackDurationMs = 0;
            _lastPacketOwnedRadioAvailableDurationMs = 0;
            _lastPacketOwnedRadioStartTick = int.MinValue;
            _lastPacketOwnedRadioExpectedStopTick = int.MinValue;
            _lastPacketOwnedRadioLastPollTick = int.MinValue;
            ResetPacketOwnedRadioCreateLayerSessionState();
            ApplyUtilityAudioSettings();

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.Radio) is UIWindowBase radioWindow && radioWindow.IsVisible)
            {
                uiWindowManager.HideWindow(MapSimulatorWindowNames.Radio);
            }

            _lastPacketOwnedRadioStatusMessage = completed
                ? $"Radio completion notice: {RadioOwnerStringPoolText.FormatNotice(PacketOwnedRadioCompleteStringPoolId, displayName, appendFallbackSuffix: true)}."
                : $"Stopped packet-owned radio playback for {displayName}.";
            NotifyEventAlarmOwnerActivity("packet-owned radio schedule");

            if (emitChatNotice)
            {
                string chatText = completed
                    ? FormatPacketOwnedRadioChatMessage(PacketOwnedRadioCompleteStringPoolId, displayName)
                    : $"Radio stop requested for {FormatPacketOwnedRadioQuotedValue(displayName)}.";
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

        private bool ShouldUsePacketOwnedRadioLeftInset()
        {
            if (IsPacketOwnedRadioPlaying())
            {
                EnsurePacketOwnedRadioCreateLayerSessionState();
                return _packetOwnedRadioSessionCreateLayerLeft;
            }

            return ResolvePacketOwnedRadioCreateLayerLeftContext();
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
            lines.Add(RadioOwnerStringPoolText.DescribeNoticeInvocation(
                PacketOwnedRadioStartStringPoolId,
                _lastPacketOwnedRadioDisplayName,
                appendFallbackSuffix: true));
            lines.Add(RadioOwnerStringPoolText.DescribeNoticeInvocation(
                PacketOwnedRadioCompleteStringPoolId,
                _lastPacketOwnedRadioDisplayName,
                appendFallbackSuffix: true));
            lines.Add(DescribePacketOwnedRadioCreateLayerState());
            lines.Add(DescribePacketOwnedRadioCreateLayerLifecycleState());
            IReadOnlyList<string> recentMutations = _packetOwnedLocalUtilityContext.GetRecentRadioCreateLayerMutations();
            for (int i = 0; i < recentMutations.Count; i++)
            {
                lines.Add($"Context mutation[{i + 1}]: {recentMutations[i]}");
            }

            lines.Add(
                $"HUD art path: {PacketOwnedRadioUiCanvasPathOn}/0..4 ({PacketOwnedRadioUiFrameDelayMs} ms), {PacketOwnedRadioUiCanvasPathOff}");
            lines.Add(DescribePacketOwnedRadioUiAnimationState());
            lines.Add($"Session elapsed: {elapsedMs / 1000f:0.0}s");
            lines.Add($"Playback position: {playheadMs / 1000f:0.0}s");
            lines.Add(_lastPacketOwnedRadioTimeValue > 0
                ? $"Authored time value: {_lastPacketOwnedRadioTimeValue}s"
                : "Authored time value: 0");
            if (_lastPacketOwnedRadioAvailableDurationMs > 0)
            {
                lines.Add($"Remaining runtime: {Math.Max(0, _lastPacketOwnedRadioAvailableDurationMs - elapsedMs) / 1000f:0.0}s");
            }

            if (_lastPacketOwnedRadioTrackDurationMs > 0)
            {
                lines.Add($"Track length: {_lastPacketOwnedRadioTrackDurationMs / 1000f:0.0}s");
            }

            lines.Add(FormatPacketOwnedRadioTemplateResolution(
                PacketOwnedRadioTrackTemplateStringPoolId,
                _lastPacketOwnedRadioTrackDescriptor,
                _lastPacketOwnedRadioResolvedTrackDescriptor));
            lines.Add(FormatPacketOwnedRadioTemplateResolution(
                PacketOwnedRadioAudioTemplateStringPoolId,
                _lastPacketOwnedRadioTrackDescriptor,
                _lastPacketOwnedRadioResolvedDescriptor));
            lines.Add(DescribePacketOwnedRadioAudioPipeline());
            lines.Add(DescribePacketOwnedRadioUpdateScheduleState());
            lines.Add("Field BGM is temporarily muted while the radio session owns playback.");
            return lines;
        }

        private string BuildPacketOwnedRadioWindowFooter()
        {
            return IsPacketOwnedRadioPlaying()
                ? $"Client parity: CRadioManager owns playback, UI, and chat; CUIRadio::CreateLayer(int bLeft) anchors Off/0 to Origin_RT with x=-3-width-(bLeft?40:0), y=+3, and the simulator now keeps Off resident while the animated On overlay fades against _utilityBgmMuted after the session's CreateLayer bLeft is latched until CWvsContext[{PacketOwnedRadioCreateLayerContextSlot}] is explicitly mutated or the session ends; CRadioManager::MMS_Play still terminates through the MonoGame audio seam instead of the client's raw AIL handle."
                : $"Client parity: waiting for OnRadioSchedule; CreateLayer(int bLeft) prefers packet-owned CWvsContext[{PacketOwnedRadioCreateLayerContextSlot}] and otherwise falls back to the live minimap-expanded branch before applying the Origin_RT anchor, with Off/0 staying resident until the On overlay fades in.";
        }

        private string GetPacketOwnedRadioTrackName()
        {
            return _lastPacketOwnedRadioDisplayName ?? _lastPacketOwnedRadioTrackDescriptor;
        }

        private static string FormatPacketOwnedRadioChatMessage(int stringPoolId, string displayName)
        {
            return RadioOwnerStringPoolText.FormatNotice(stringPoolId, displayName, appendFallbackSuffix: true);
        }

        private static string FormatPacketOwnedRadioTemplateResolution(int stringPoolId, string trackDescriptor, string resolvedDescriptor)
        {
            return RadioOwnerStringPoolText.FormatPathTemplateResolution(
                stringPoolId,
                trackDescriptor,
                resolvedDescriptor,
                appendFallbackSuffix: true);
        }

        private static string FormatPacketOwnedRadioQuotedValue(string value)
        {
            string normalizedValue = string.IsNullOrWhiteSpace(value)
                ? "radio track"
                : value.Trim();
            return $"\"{normalizedValue.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
        }

        private string DescribePacketOwnedRadioCreateLayerState()
        {
            bool hasExplicitContextValue = _packetOwnedLocalUtilityContext.HasRadioCreateLayerLeftContextValue;
            bool explicitContextValue = _packetOwnedLocalUtilityContext.RadioCreateLayerLeftContextValue;
            bool minimapExpanded = miniMapUi?.IsExpandedOptionActive == true;
            bool bLeft = ShouldUsePacketOwnedRadioLeftInset();
            int nMargin = bLeft ? PacketOwnedRadioCreateLayerLeftMargin : 0;
            string liveSource = hasExplicitContextValue
                ? $"packet-owned CWvsContext[{PacketOwnedRadioCreateLayerContextSlot}]={(explicitContextValue ? 1 : 0)}"
                : $"minimap expanded fallback={(minimapExpanded ? 1 : 0)}";
            if (IsPacketOwnedRadioPlaying())
            {
                EnsurePacketOwnedRadioCreateLayerSessionState();
                bLeft = _packetOwnedRadioSessionCreateLayerLeft;
                nMargin = bLeft ? PacketOwnedRadioCreateLayerLeftMargin : 0;
                return
                    $"CreateLayer: bLeft={(_packetOwnedRadioSessionCreateLayerLeft ? 1 : 0)} via {_packetOwnedRadioSessionCreateLayerSource}, " +
                    $"nMargin={nMargin}, Origin_RT => x=-3-width-{nMargin}, y=+3 (live fallback now {liveSource}).";
            }

            return $"CreateLayer: bLeft={(bLeft ? 1 : 0)} via {liveSource}, nMargin={nMargin}, Origin_RT => x=-3-width-{nMargin}, y=+3";
        }

        private string DescribePacketOwnedRadioCreateLayerLifecycleState()
        {
            if (IsPacketOwnedRadioPlaying())
            {
                EnsurePacketOwnedRadioCreateLayerSessionState();
                return
                    $"Session CreateLayer owner: frozen bLeft={(_packetOwnedRadioSessionCreateLayerLeft ? 1 : 0)}, " +
                    $"capturedSeq={_packetOwnedRadioSessionCreateLayerMutationSequence}, contextSeq={_packetOwnedLocalUtilityContext.RadioCreateLayerMutationSequence}.";
            }

            return
                $"Context lifecycle: mutationSeq={_packetOwnedLocalUtilityContext.RadioCreateLayerMutationSequence}, " +
                $"lastMutation={_packetOwnedLocalUtilityContext.RadioCreateLayerLastMutationSource}.";
        }

        private string DescribePacketOwnedRadioUiAnimationState()
        {
            return $"Ctor fade: bMute=0 keeps the animated On overlay visible over Off/0 and fades the Off alpha to 0 over {PacketOwnedRadioUiFadeDurationMs} ms; bMute=1 performs the inverse fade before playback ownership flips, and the simulator now mirrors that state by fading the On overlay against _utilityBgmMuted while Off/0 stays resident.";
        }

        private string DescribePacketOwnedRadioAudioPipeline()
        {
            return $"MMS_Play: {FormatStringPoolId(PacketOwnedRadioAudioTemplateStringPoolId)} => IWzSound => buffer => AIL_quick_load_mem / ms_length / set_ms_position / play (simulator backend still uses MonoGameBgmPlayer).";
        }

        private string DescribePacketOwnedRadioUpdateScheduleState()
        {
            string expectedStop = _lastPacketOwnedRadioExpectedStopTick == int.MinValue
                ? "unset"
                : $"{Math.Max(0, unchecked(_lastPacketOwnedRadioExpectedStopTick - _lastPacketOwnedRadioStartTick)) / 1000f:0.0}s from session start";
            string firstPoll = _lastPacketOwnedRadioExpectedStopTick == int.MinValue
                ? "immediate fallback"
                : $"{Math.Max(0, unchecked(_lastPacketOwnedRadioExpectedStopTick + PacketOwnedRadioUpdatePollIntervalMs - _lastPacketOwnedRadioStartTick)) / 1000f:0.0}s from session start";
            return $"Update cadence: CRadioManager seeds m_tLastUpdate from get_update_time() - ms_position + ms_length, then checks status once tCur-m_tLastUpdate > {PacketOwnedRadioUpdatePollIntervalMs} ms; simulator now mirrors that remaining-runtime schedule ({expectedStop} stop, first poll at {firstPoll}).";
        }

        private static string FormatStringPoolId(int stringPoolId)
        {
            return $"StringPool 0x{stringPoolId:X}";
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

            string normalizedDescriptor = NormalizePacketOwnedClientSoundDescriptor(descriptor);
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

            string normalizedDescriptor = NormalizePacketOwnedClientSoundDescriptor(descriptor);
            if (string.IsNullOrWhiteSpace(normalizedDescriptor))
            {
                return false;
            }

            if (!TryResolvePacketOwnedRadioClientTrackNode(
                    normalizedDescriptor,
                    out WzImageProperty trackNode,
                    out string resolvedTrackDescriptor))
            {
                return false;
            }

            return TryCreatePacketOwnedRadioTrackResolution(
                trackNode,
                normalizedDescriptor,
                resolvedTrackDescriptor,
                out resolution);
        }

        internal static IEnumerable<(string ImageName, string PropertyPath)> BuildPacketOwnedRadioTrackCandidates(string descriptor)
        {
            if (string.IsNullOrWhiteSpace(descriptor))
            {
                yield break;
            }

            string normalized = NormalizePacketOwnedClientSoundDescriptor(descriptor);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                yield break;
            }

            HashSet<string> yieldedCandidates = new(StringComparer.OrdinalIgnoreCase);
            foreach (string descriptorCandidate in BuildPacketOwnedRadioDescriptorCandidates(normalized))
            {
                if (!TrySplitPacketOwnedClientSoundDescriptor(descriptorCandidate, out string imageName, out string propertyPath))
                {
                    continue;
                }

                string key = $"{imageName}/{propertyPath}";
                if (yieldedCandidates.Add(key))
                {
                    yield return (imageName, propertyPath);
                }
            }
        }

        internal static IEnumerable<string> BuildPacketOwnedRadioDescriptorCandidates(string descriptor)
        {
            string normalized = NormalizePacketOwnedClientSoundDescriptor(descriptor);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                yield break;
            }

            HashSet<string> yieldedDescriptors = new(StringComparer.OrdinalIgnoreCase);
            if (TryFormatPacketOwnedClientSoundDescriptorTemplate(
                    PacketOwnedRadioTrackTemplateStringPoolId,
                    normalized,
                    out string trackTemplateDescriptor)
                && yieldedDescriptors.Add(trackTemplateDescriptor))
            {
                yield return trackTemplateDescriptor;
            }

            if (TryFormatPacketOwnedClientSoundDescriptorTemplate(
                    PacketOwnedRadioAudioTemplateStringPoolId,
                    normalized,
                    out string audioTemplateDescriptor)
                && yieldedDescriptors.Add(audioTemplateDescriptor))
            {
                yield return audioTemplateDescriptor;
            }

            if (yieldedDescriptors.Add(normalized))
            {
                yield return normalized;
            }

            string[] segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2)
            {
                string fallbackDescriptor = $"{NormalizePacketOwnedSoundImageName(segments[0])}/{string.Join("/", segments.Skip(1))}";
                if (yieldedDescriptors.Add(fallbackDescriptor))
                {
                    yield return fallbackDescriptor;
                }
            }
        }

        internal static string NormalizePacketOwnedClientSoundDescriptor(string descriptor)
        {
            if (string.IsNullOrWhiteSpace(descriptor))
            {
                return string.Empty;
            }

            string normalized = descriptor.Trim().Replace('\\', '/');
            while (normalized.StartsWith("/", StringComparison.Ordinal))
            {
                normalized = normalized[1..];
            }

            const string soundRootPrefix = "Sound/";
            if (normalized.StartsWith(soundRootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[soundRootPrefix.Length..];
            }

            return normalized.Trim('/');
        }

        internal static bool TrySplitPacketOwnedClientSoundDescriptor(
            string descriptor,
            out string imageName,
            out string propertyPath)
        {
            imageName = null;
            propertyPath = null;

            string normalized = NormalizePacketOwnedClientSoundDescriptor(descriptor);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            string[] segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2)
            {
                return false;
            }

            imageName = NormalizePacketOwnedSoundImageName(segments[0]);
            propertyPath = string.Join("/", segments.Skip(1));
            return !string.IsNullOrWhiteSpace(propertyPath);
        }

        private static bool TryFormatPacketOwnedClientSoundDescriptorTemplate(
            int stringPoolId,
            string descriptor,
            out string formattedDescriptor)
        {
            formattedDescriptor = null;
            string normalized = NormalizePacketOwnedClientSoundDescriptor(descriptor);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(stringPoolId, null, 1, out bool hasResolvedText);
            if (!hasResolvedText || string.IsNullOrWhiteSpace(format))
            {
                return false;
            }

            formattedDescriptor = NormalizePacketOwnedClientSoundDescriptor(string.Format(format, normalized));
            return !string.IsNullOrWhiteSpace(formattedDescriptor);
        }

        private static IEnumerable<string> BuildPacketOwnedWzSoundDescriptorCandidates(string descriptor, string defaultImageName)
        {
            string normalized = NormalizePacketOwnedClientSoundDescriptor(descriptor);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                yield break;
            }

            HashSet<string> yieldedDescriptors = new(StringComparer.OrdinalIgnoreCase);
            if (TrySplitPacketOwnedClientSoundDescriptor(normalized, out string imageName, out string propertyPath))
            {
                string directDescriptor = $"{imageName}/{propertyPath}";
                if (yieldedDescriptors.Add(directDescriptor))
                {
                    yield return directDescriptor;
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(defaultImageName))
                {
                    string defaultDescriptor = $"{NormalizePacketOwnedSoundImageName(defaultImageName)}/{normalized}";
                    if (yieldedDescriptors.Add(defaultDescriptor))
                    {
                        yield return defaultDescriptor;
                    }
                }

                string[] fallbackImages =
                {
                    "Field.img",
                    "UI.img",
                    "Game.img",
                    "MiniGame.img",
                };

                for (int i = 0; i < fallbackImages.Length; i++)
                {
                    string fallbackDescriptor = $"{fallbackImages[i]}/{normalized}";
                    if (yieldedDescriptors.Add(fallbackDescriptor))
                    {
                        yield return fallbackDescriptor;
                    }
                }
            }
        }

        private static bool TryCreatePacketOwnedRadioTrackResolution(
            WzImageProperty trackNode,
            string authoredDescriptor,
            string resolvedTrackDescriptor,
            out PacketOwnedRadioTrackResolution resolution)
        {
            resolution = null;
            WzImageProperty resolvedTrackNode = WzInfoTools.GetRealProperty(trackNode);
            if (resolvedTrackNode == null)
            {
                return false;
            }

            if (!TryResolvePacketOwnedRadioAudioProperty(
                    authoredDescriptor,
                    resolvedTrackNode,
                    out WzBinaryProperty audioProperty,
                    out string resolvedAudioDescriptor))
            {
                return false;
            }

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
                resolvedAudioDescriptor,
                displayName);
            return true;
        }

        private static bool TryResolvePacketOwnedRadioClientTrackNode(
            string descriptor,
            out WzImageProperty trackNode,
            out string resolvedTrackDescriptor)
        {
            trackNode = null;
            resolvedTrackDescriptor = null;

            foreach (string descriptorCandidate in BuildPacketOwnedRadioClientTrackDescriptorCandidates(descriptor))
            {
                if (!TrySplitPacketOwnedClientSoundDescriptor(descriptorCandidate, out string imageName, out string propertyPath))
                {
                    continue;
                }

                WzImageProperty resolvedTrackNode = ResolvePacketOwnedSoundProperty(imageName, propertyPath);
                if (resolvedTrackNode == null)
                {
                    continue;
                }

                trackNode = resolvedTrackNode;
                resolvedTrackDescriptor = $"{imageName[..^4]}/{propertyPath}";
                return true;
            }

            return false;
        }

        internal static IEnumerable<string> BuildPacketOwnedRadioClientTrackDescriptorCandidates(string descriptor)
        {
            string normalized = NormalizePacketOwnedClientSoundDescriptor(descriptor);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                yield break;
            }

            HashSet<string> yieldedDescriptors = new(StringComparer.OrdinalIgnoreCase);
            if (TryFormatPacketOwnedClientSoundDescriptorTemplate(
                    PacketOwnedRadioTrackTemplateStringPoolId,
                    normalized,
                    out string trackTemplateDescriptor)
                && yieldedDescriptors.Add(trackTemplateDescriptor))
            {
                yield return trackTemplateDescriptor;
            }

            if (yieldedDescriptors.Add(normalized))
            {
                yield return normalized;
            }
        }

        internal static IEnumerable<string> BuildPacketOwnedRadioClientAudioDescriptorCandidates(string descriptor)
        {
            string normalized = NormalizePacketOwnedClientSoundDescriptor(descriptor);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                yield break;
            }

            HashSet<string> yieldedDescriptors = new(StringComparer.OrdinalIgnoreCase);
            if (TryFormatPacketOwnedClientSoundDescriptorTemplate(
                    PacketOwnedRadioAudioTemplateStringPoolId,
                    normalized,
                    out string audioTemplateDescriptor)
                && yieldedDescriptors.Add(audioTemplateDescriptor))
            {
                yield return audioTemplateDescriptor;
            }

            if (yieldedDescriptors.Add(normalized))
            {
                yield return normalized;
            }
        }

        private static bool TryResolvePacketOwnedRadioAudioProperty(
            string authoredDescriptor,
            WzImageProperty resolvedTrackNode,
            out WzBinaryProperty audioProperty,
            out string resolvedAudioDescriptor)
        {
            audioProperty = null;
            resolvedAudioDescriptor = null;

            foreach (string descriptorCandidate in BuildPacketOwnedRadioClientAudioDescriptorCandidates(authoredDescriptor))
            {
                if (!TrySplitPacketOwnedClientSoundDescriptor(descriptorCandidate, out string imageName, out string propertyPath))
                {
                    continue;
                }

                WzImageProperty resolvedAudioNode = ResolvePacketOwnedSoundProperty(imageName, propertyPath);
                if (WzInfoTools.GetRealProperty(resolvedAudioNode) is WzBinaryProperty binaryProperty)
                {
                    audioProperty = binaryProperty;
                    resolvedAudioDescriptor = $"{imageName[..^4]}/{propertyPath}";
                    return true;
                }
            }

            audioProperty = ResolvePacketOwnedRadioAudioProperty(resolvedTrackNode);
            if (audioProperty == null)
            {
                return false;
            }

            resolvedAudioDescriptor = ResolvePacketOwnedDescriptor(audioProperty)
                ?? ResolvePacketOwnedDescriptor(resolvedTrackNode);
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
            _lastPacketOwnedNoticeTick = Environment.TickCount;
            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedNoticeMessage))
            {
                NotifyEventAlarmOwnerActivity("packet-owned notice alarm");
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
            int currentTick = Environment.TickCount;
            _lastPacketOwnedChatMessage = message?.Trim() ?? string.Empty;
            _lastPacketOwnedChatTick = currentTick;
            if (string.IsNullOrWhiteSpace(_lastPacketOwnedChatMessage))
            {
                return "Packet-owned chat line was empty.";
            }

            NotifyEventAlarmOwnerActivity("packet-owned chat alarm");

            if (chatLogType.HasValue)
            {
                _chat?.AddClientChatMessage(_lastPacketOwnedChatMessage, currentTick, chatLogType.Value, null, channelId);
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
                currentTick,
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

            bool hadActiveCooldown = _playerManager.Skills.IsOnCooldown(normalizedSkillId, currTickCount);
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

            bool hasActiveCooldown = _playerManager.Skills.IsOnCooldown(normalizedSkillId, currTickCount);
            SkillCooldownNotificationTransition transition =
                SkillCooldownNotificationTransitionResolver.ResolvePacketOwnedTransition(
                    hadActiveCooldown,
                    hasActiveCooldown);
            var skill = _playerManager.Skills.GetSkillData(normalizedSkillId) ?? _playerManager.SkillLoader?.LoadSkill(normalizedSkillId);
            string skillName = skill?.Name
                ?? (isVehicleSentinel ? "Battleship" : $"Skill {normalizedSkillId}");
            ShowPacketOwnedSkillCooldownNotification(skill, normalizedSkillId, skillName, remainingMs, transition);
            string message = remainingMs > 0
                ? $"Applied packet-owned {(isVehicleSentinel ? "vehicle " : string.Empty)}skill cooldown for {skillName}: {FormatCooldownNotificationSeconds(remainingMs)} remaining."
                : $"Cleared packet-owned {(isVehicleSentinel ? "vehicle " : string.Empty)}skill cooldown for {skillName}.";
            ShowUtilityFeedbackMessage(message);
            return message;
        }

        private string ApplyPacketOwnedTimeBombAttack(int skillId, int timeBombX, int timeBombY, int impactPercent, int damage)
        {
            StampPacketOwnedUtilityRequestState();
            if (_playerManager?.Skills == null || _playerManager.Player == null || _playerManager.Combat == null)
            {
                const string unavailable = "Time Bomb parity could not be applied because the local player skill runtime is not initialized.";
                ShowUtilityFeedbackMessage(unavailable);
                return unavailable;
            }

            int normalizedSkillId = ResolvePacketOwnedLocalSkillId(skillId);
            Vector2 timeBombPosition = new(timeBombX, timeBombY);
            bool appliedPacketOwnedAttack = _playerManager.Skills.TryApplyPacketOwnedTimeBombAttack(
                normalizedSkillId,
                currTickCount,
                timeBombPosition,
                out SkillData skill,
                out int level,
                out string errorMessage);

            int appliedHitPeriodMs = 0;
            int appliedDamage = damage;
            if (ShouldApplyPacketOwnedTimeBombImpactReaction(impactPercent))
            {
                appliedHitPeriodMs = ResolvePacketOwnedTimeBombHitPeriodMs(PacketOwnedBaseTimeBombHitPeriodMs);
                if (appliedHitPeriodMs > 0)
                {
                    _playerManager.Combat.SetInvincible(currTickCount, appliedHitPeriodMs);
                }

                float impactMagnitude = Math.Max(390f, impactPercent * 4f);
                float knockbackX = _playerManager.Player.FacingRight ? -impactMagnitude : impactMagnitude;
                float knockbackY = -impactMagnitude;
                _playerManager.Player.ApplyPacketDamageReaction(
                    appliedDamage,
                    Math.Max(1, appliedHitPeriodMs),
                    knockbackX,
                    knockbackY,
                    useQueuedImpact: true);
                ApplyPacketOwnedTimeBombHpEffect(appliedDamage, currTickCount);
            }

            string skillName = skill?.Name ?? $"Skill {normalizedSkillId}";
            string message = appliedPacketOwnedAttack
                ? $"Applied packet-owned Time Bomb attack for {skillName} Lv.{level} (target {timeBombX}, {timeBombY}, hit {appliedHitPeriodMs} ms, impact {impactPercent}%, damage {appliedDamage})."
                : $"Applied packet-owned Time Bomb reaction for {skillName} without a resolved melee-attack branch ({errorMessage}) (target {timeBombX}, {timeBombY}, hit {appliedHitPeriodMs} ms, impact {impactPercent}%, damage {appliedDamage}).";
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

            if (!IsClientOwnedVengeancePacketSkillId(skillId))
            {
                string ignored = $"Ignored packet-owned Vengeance apply for unexpected skill id {skillId}; the client only branches on legacy packet skill id {PacketOwnedLegacyVengeanceSkillId}.";
                ShowUtilityFeedbackMessage(ignored);
                return ignored;
            }

            int normalizedSkillId = ResolvePacketOwnedLocalSkillId(skillId);
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
            int normalizedSkillId = ResolvePacketOwnedLocalSkillId(PacketOwnedCurrentExJablinSkillId);
            SkillData skill = _playerManager.Skills.GetSkillData(normalizedSkillId);
            int level = _playerManager.Skills.GetSkillLevel(normalizedSkillId);
            string skillName = skill?.Name;
            string message = !string.IsNullOrWhiteSpace(skillName) && level > 0
                ? $"Armed the packet-owned ExJablin next-shot state for {skillName} Lv.{level}; the next ranged attack will crit."
                : "Armed the packet-owned ExJablin next-shot state for the next ranged attack.";
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
                LoginUtilityDialogAction.DismissOnly,
                trackDirectionModeOwner: true);
        }

        private void ShowPacketOwnedSkillCooldownNotification(
            SkillData skill,
            int skillId,
            string skillName,
            int remainingMs,
            SkillCooldownNotificationTransition transition)
        {
            if (!ShouldShowOffBarSkillCooldownNotification(skillId)
                || transition == SkillCooldownNotificationTransition.None)
            {
                return;
            }

            string resolvedSkillName = string.IsNullOrWhiteSpace(skillName)
                ? $"Skill {skillId}"
                : skillName;
            string notification = transition == SkillCooldownNotificationTransition.Started
                ? $"{resolvedSkillName} is cooling down. {FormatCooldownNotificationSeconds(remainingMs)}."
                : $"{resolvedSkillName} is ready.";
            ShowSkillCooldownNotification(
                skill,
                notification,
                currTickCount,
                addChat: transition == SkillCooldownNotificationTransition.Ready,
                transition == SkillCooldownNotificationTransition.Started
                    ? SkillCooldownNoticeType.Started
                    : SkillCooldownNoticeType.Ready);
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
            _lastPacketOwnedBuffzoneTick = Environment.TickCount;
            NotifyEventAlarmOwnerActivity("packet-owned buffzone alarm");
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
            _lastPacketOwnedAskApspTick = Environment.TickCount;
            NotifyEventAlarmOwnerActivity("packet-owned AP/SP event");
            ShowUtilityFeedbackMessage(_lastPacketOwnedAskApspMessage);
            return _lastPacketOwnedAskApspMessage;
        }

        private string ApplyPacketOwnedTutorHire(bool enabled, int targetCharacterId = 0)
        {
            StampPacketOwnedUtilityRequestState();
            SyncPacketOwnedTutorLifecycle(currTickCount);
            int runtimeCharacterId = ResolvePacketOwnedTutorRuntimeCharacterId();
            int resolvedTargetCharacterId = Math.Max(0, targetCharacterId);
            bool targetsSharedOwner = resolvedTargetCharacterId > 0
                && resolvedTargetCharacterId != runtimeCharacterId;

            if (targetsSharedOwner)
            {
                int skillId = ResolvePacketOwnedTutorSkillIdForCharacter(resolvedTargetCharacterId);
                int actorHeight = ResolvePacketOwnedTutorActorHeight(skillId);
                if (!enabled)
                {
                    _packetOwnedTutorRuntime.ApplySharedRemovalRequestForCharacter(skillId, currTickCount, resolvedTargetCharacterId);
                    SyncPacketOwnedTutorSummonState(currTickCount);
                    string removalMessage = $"Removed packet-owned shared {DescribePacketOwnedTutorVariant(skillId)} tutor ownership for character {resolvedTargetCharacterId}.";
                    NotifyEventAlarmOwnerActivity("packet-owned tutor alarm");
                    ShowUtilityFeedbackMessage(removalMessage);
                    return removalMessage;
                }

                _packetOwnedTutorRuntime.ApplySharedHireRequestForCharacter(skillId, actorHeight, currTickCount, resolvedTargetCharacterId);
                SyncPacketOwnedTutorSummonState(currTickCount);
                string sharedMessage = $"Activated packet-owned shared {DescribePacketOwnedTutorVariant(skillId)} tutor ownership for character {resolvedTargetCharacterId} at height {actorHeight}.";
                NotifyEventAlarmOwnerActivity("packet-owned tutor alarm");
                ShowUtilityFeedbackMessage(sharedMessage);
                return sharedMessage;
            }

            if (!enabled)
            {
                int requestedSkillId = ResolvePacketOwnedTutorSkillId();
                _packetOwnedTutorRuntime.ApplyRemovalRequest(
                    requestedSkillId,
                    currTickCount,
                    "packet-owned tutor branch requested removal.");
                RemovePacketOwnedTutorSummon();
                NotifyEventAlarmOwnerActivity("packet-owned tutor alarm");
                ShowUtilityFeedbackMessage(_packetOwnedTutorRuntime.StatusMessage);
                return _packetOwnedTutorRuntime.StatusMessage;
            }

            // Client evidence: CUserLocal::OnHireTutor removes the prior tutor owner
            // before allocating and initializing the next one.
            RemovePacketOwnedTutorSummon();
            int tutorSkillId = ResolvePacketOwnedTutorSkillId();
            int tutorActorHeight = ResolvePacketOwnedTutorActorHeight(tutorSkillId);
            _packetOwnedTutorRuntime.ApplyHireRequest(tutorSkillId, tutorActorHeight, currTickCount, ResolvePacketOwnedTutorRuntimeCharacterId());
            string summonDetail = EnsurePacketOwnedTutorSummon(currTickCount);
            string message = string.IsNullOrWhiteSpace(summonDetail)
                ? $"Activated packet-owned {DescribePacketOwnedTutorVariant(tutorSkillId)} tutor ownership at height {tutorActorHeight}."
                : $"Activated packet-owned {DescribePacketOwnedTutorVariant(tutorSkillId)} tutor ownership at height {tutorActorHeight}. {summonDetail}";
            NotifyEventAlarmOwnerActivity("packet-owned tutor alarm");
            ShowUtilityFeedbackMessage(message);
            return message;
        }

        private string ApplyPacketOwnedTutorIndexedMessage(int messageIndex, int durationMs, int targetCharacterId = 0)
        {
            StampPacketOwnedUtilityRequestState();
            SyncPacketOwnedTutorLifecycle(currTickCount);
            int runtimeCharacterId = ResolvePacketOwnedTutorRuntimeCharacterId();
            int resolvedTargetCharacterId = Math.Max(0, targetCharacterId);
            bool targetsSharedOwner = resolvedTargetCharacterId > 0
                && resolvedTargetCharacterId != runtimeCharacterId;

            if (targetsSharedOwner)
            {
                if (!_packetOwnedTutorRuntime.TryGetSharedActiveVariantForCharacter(resolvedTargetCharacterId, out TutorVariantSnapshot sharedVariant))
                {
                    string inactiveSharedMessage = $"Ignored packet-owned tutor indexed message because no shared tutor actor is active for character {resolvedTargetCharacterId}.";
                    ShowUtilityFeedbackMessage(inactiveSharedMessage);
                    return inactiveSharedMessage;
                }

                _packetOwnedTutorRuntime.ApplySharedIndexedMessageForCharacter(
                    sharedVariant.SkillId,
                    messageIndex,
                    durationMs,
                    currTickCount,
                    resolvedTargetCharacterId);
                SyncPacketOwnedTutorSummonState(currTickCount);
                string sharedMessage = $"Applied packet-owned tutor cue #{Math.Max(0, messageIndex)} ({Math.Max(0, durationMs)}) for character {resolvedTargetCharacterId}.";
                NotifyEventAlarmOwnerActivity("packet-owned tutor alarm");
                ShowUtilityFeedbackMessage(sharedMessage);
                return sharedMessage;
            }

            if (!_packetOwnedTutorRuntime.IsActive)
            {
                const string inactiveMessage = "Ignored packet-owned tutor indexed message because no tutor actor is active.";
                ShowUtilityFeedbackMessage(inactiveMessage);
                return inactiveMessage;
            }

            _packetOwnedTutorRuntime.ApplyIndexedMessage(messageIndex, durationMs, currTickCount);
            string message = $"Applied packet-owned tutor cue #{Math.Max(0, messageIndex)} ({Math.Max(0, durationMs)}).";
            NotifyEventAlarmOwnerActivity("packet-owned tutor alarm");
            ShowUtilityFeedbackMessage(message);
            return message;
        }

        private string ApplyPacketOwnedTutorTextMessage(string text, int width, int durationMs, int targetCharacterId = 0)
        {
            StampPacketOwnedUtilityRequestState();
            SyncPacketOwnedTutorLifecycle(currTickCount);
            int runtimeCharacterId = ResolvePacketOwnedTutorRuntimeCharacterId();
            int resolvedTargetCharacterId = Math.Max(0, targetCharacterId);
            bool targetsSharedOwner = resolvedTargetCharacterId > 0
                && resolvedTargetCharacterId != runtimeCharacterId;

            if (targetsSharedOwner)
            {
                if (!_packetOwnedTutorRuntime.TryGetSharedActiveVariantForCharacter(resolvedTargetCharacterId, out TutorVariantSnapshot sharedVariant))
                {
                    string inactiveSharedMessage = $"Ignored packet-owned tutor text message because no shared tutor actor is active for character {resolvedTargetCharacterId}.";
                    ShowUtilityFeedbackMessage(inactiveSharedMessage);
                    return inactiveSharedMessage;
                }

                _packetOwnedTutorRuntime.ApplySharedTextMessageForCharacter(
                    sharedVariant.SkillId,
                    text,
                    width,
                    durationMs,
                    currTickCount,
                    resolvedTargetCharacterId);
                SyncPacketOwnedTutorSummonState(currTickCount);
                string sharedText = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
                if (!string.IsNullOrWhiteSpace(sharedText))
                {
                    NotifyEventAlarmOwnerActivity("packet-owned tutor alarm");
                    ShowUtilityFeedbackMessage($"Tutor[{resolvedTargetCharacterId}]: {sharedText}");
                }

                return $"Applied packet-owned tutor text message for character {resolvedTargetCharacterId} ({Math.Clamp(width <= 0 ? TutorRuntime.DefaultTextWidth : width, TutorRuntime.MinTextWidth, TutorRuntime.MaxTextWidth)}px, {Math.Clamp(durationMs <= 0 ? TutorRuntime.DefaultIndexedDurationMs : durationMs, TutorRuntime.MinMessageDurationMs, TutorRuntime.MaxMessageDurationMs)} ms).";
            }

            if (!_packetOwnedTutorRuntime.IsActive)
            {
                const string inactiveMessage = "Ignored packet-owned tutor text message because no tutor actor is active.";
                ShowUtilityFeedbackMessage(inactiveMessage);
                return inactiveMessage;
            }

            _packetOwnedTutorRuntime.ApplyTextMessage(text, width, durationMs, currTickCount);
            if (!string.IsNullOrWhiteSpace(_packetOwnedTutorRuntime.ActiveMessageText))
            {
                NotifyEventAlarmOwnerActivity("packet-owned tutor alarm");
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
                _lastPacketOwnedSkillGuideTick = Environment.TickCount;
                NotifyEventAlarmOwnerActivity("packet-owned skill guide");
                ShowUtilityFeedbackMessage(_lastPacketOwnedSkillGuideMessage);
                return _lastPacketOwnedSkillGuideMessage;
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.AranSkillGuide) is not AranSkillGuideUI aranSkillGuideWindow)
            {
                _lastPacketOwnedSkillGuideMessage = $"{skillWindowMessage} Aran skill-guide owner is not available in this UI build.";
                _lastPacketOwnedSkillGuideTick = Environment.TickCount;
                NotifyEventAlarmOwnerActivity("packet-owned skill guide");
                ShowUtilityFeedbackMessage(_lastPacketOwnedSkillGuideMessage);
                return _lastPacketOwnedSkillGuideMessage;
            }

            aranSkillGuideWindow.SetPage(guideGrade);
            ShowWindow(
                MapSimulatorWindowNames.AranSkillGuide,
                aranSkillGuideWindow,
                trackDirectionModeOwner: ShouldTrackInheritedDirectionModeOwner());
            _lastPacketOwnedSkillGuideMessage = $"{skillWindowMessage} Opened the packet-owned current skill guide at Aran grade {guideGrade}.";
            _lastPacketOwnedSkillGuideTick = Environment.TickCount;
            NotifyEventAlarmOwnerActivity("packet-owned skill guide");
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
                _lastPacketOwnedAskApspTick = Environment.TickCount;
                message = _lastPacketOwnedAskApspMessage;
                NotifyEventAlarmOwnerActivity("packet-owned AP/SP event");
                ShowUtilityFeedbackMessage(message);
                return false;
            }

            if (eventType < PacketOwnedApspMinEventType || eventType > PacketOwnedApspMaxEventType)
            {
                _packetOwnedApspPromptActive = false;
                _lastPacketOwnedAskApspMessage = $"Packet-owned AP/SP helper prompt rejected unsupported event type {eventType}.";
                _lastPacketOwnedAskApspTick = Environment.TickCount;
                message = _lastPacketOwnedAskApspMessage;
                NotifyEventAlarmOwnerActivity("packet-owned AP/SP event");
                ShowUtilityFeedbackMessage(message);
                return false;
            }

            _packetOwnedApspPromptActive = true;
            _packetOwnedApspPromptContextToken = contextToken;
            _packetOwnedApspPromptEventType = eventType;
            _lastPacketOwnedApspFollowUpContextToken = 0;
            _lastPacketOwnedApspFollowUpResponseCode = 0;
            _lastPacketOwnedAskApspMessage = $"Opened packet-owned AP/SP helper prompt for context {contextToken}, event {eventType}, StringPool 0x{PacketOwnedApspPromptStringPoolId:X}.";
            _lastPacketOwnedAskApspTick = Environment.TickCount;
            NotifyEventAlarmOwnerActivity("packet-owned AP/SP event");
            ShowLoginUtilityDialog(
                "AP/SP Helper",
                BuildPacketOwnedApspPromptBody(contextToken, eventType),
                LoginUtilityDialogButtonLayout.YesNo,
                LoginUtilityDialogAction.ConfirmApspEvent,
                primaryLabel: PacketOwnedApspPromptPrimaryLabel,
                secondaryLabel: PacketOwnedApspPromptSecondaryLabel,
                trackDirectionModeOwner: true);
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
            _lastPacketOwnedAskApspTick = Environment.TickCount;
            _packetOwnedApspPromptActive = false;
            HideLoginUtilityDialog();
            NotifyEventAlarmOwnerActivity("packet-owned AP/SP event");
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
            _lastPacketOwnedAskApspTick = Environment.TickCount;
            _packetOwnedApspPromptActive = false;
            HideLoginUtilityDialog();
            NotifyEventAlarmOwnerActivity("packet-owned AP/SP event");
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

        private int ResolvePacketOwnedTutorRuntimeCharacterId()
        {
            return Math.Max(0, _playerManager?.Player?.Build?.Id ?? 0);
        }

        private int ResolvePacketOwnedRadioRuntimeCharacterId()
        {
            return Math.Max(0, _playerManager?.Player?.Build?.Id ?? 0);
        }

        private void SyncPacketOwnedRadioCreateLayerContextLifecycle()
        {
            int runtimeCharacterId = ResolvePacketOwnedRadioRuntimeCharacterId();
            _packetOwnedLocalUtilityContext.ObserveRadioCreateLayerRuntimeCharacterId(runtimeCharacterId);
            if (runtimeCharacterId <= 0)
            {
                return;
            }

            int previousBoundCharacterId = _packetOwnedLocalUtilityContext.RadioCreateLayerBoundCharacterId;
            if (_packetOwnedLocalUtilityContext.RequiresRadioCreateLayerCharacterReset(runtimeCharacterId))
            {
                if (ShouldResetPacketOwnedRadioForRuntimeCharacterChange(
                        IsPacketOwnedRadioPlaying(),
                        previousBoundCharacterId,
                        runtimeCharacterId))
                {
                    string trackName = GetPacketOwnedRadioTrackName();
                    StopPacketOwnedRadioSchedule(completed: false, emitChatNotice: false);
                    _lastPacketOwnedRadioStatusMessage =
                        $"Reset packet-owned radio playback for {FormatPacketOwnedRadioQuotedValue(trackName)} after runtime character changed from {previousBoundCharacterId} to {runtimeCharacterId}.";
                }

                _packetOwnedLocalUtilityContext.ResetRadioCreateLayerForCharacter(runtimeCharacterId);
                ResetPacketOwnedRadioCreateLayerSessionState();
                return;
            }

            _packetOwnedLocalUtilityContext.EnsureRadioCreateLayerInitializedFromRuntime(runtimeCharacterId);
        }

        private void CapturePacketOwnedRadioCreateLayerSessionState()
        {
            SyncPacketOwnedRadioCreateLayerContextLifecycle();
            bool hasExplicitContextValue = _packetOwnedLocalUtilityContext.HasRadioCreateLayerLeftContextValue;
            bool explicitContextValue = _packetOwnedLocalUtilityContext.RadioCreateLayerLeftContextValue;
            bool minimapExpanded = miniMapUi?.IsExpandedOptionActive == true;
            _packetOwnedRadioSessionCreateLayerLeft = ResolvePacketOwnedRadioCreateLayerLeftState(
                hasExplicitContextValue,
                explicitContextValue,
                minimapExpanded);
            _packetOwnedRadioSessionCreateLayerMutationSequence = _packetOwnedLocalUtilityContext.RadioCreateLayerMutationSequence;
            _packetOwnedRadioSessionCreateLayerSource = hasExplicitContextValue
                ? $"packet-owned CWvsContext[{PacketOwnedRadioCreateLayerContextSlot}]={(explicitContextValue ? 1 : 0)}"
                : $"minimap expanded fallback={(minimapExpanded ? 1 : 0)}";
        }

        private void EnsurePacketOwnedRadioCreateLayerSessionState()
        {
            if (ShouldRefreshPacketOwnedRadioCreateLayerSessionState(
                    IsPacketOwnedRadioPlaying(),
                    _packetOwnedRadioSessionCreateLayerMutationSequence,
                    _packetOwnedLocalUtilityContext.RadioCreateLayerMutationSequence))
            {
                CapturePacketOwnedRadioCreateLayerSessionState();
            }
        }

        private void ResetPacketOwnedRadioCreateLayerSessionState()
        {
            _packetOwnedRadioSessionCreateLayerLeft = false;
            _packetOwnedRadioSessionCreateLayerMutationSequence = -1;
            _packetOwnedRadioSessionCreateLayerSource = "uninitialized";
        }

        private void SyncPacketOwnedTutorLifecycle(int currentTickCount)
        {
            int runtimeCharacterId = ResolvePacketOwnedTutorRuntimeCharacterId();
            if (runtimeCharacterId <= 0)
            {
                return;
            }

            if (_packetOwnedTutorRuntime.RequiresCharacterRebind(runtimeCharacterId))
            {
                _packetOwnedTutorRuntime.ResetActiveTutorForRuntimeCharacter(runtimeCharacterId, currentTickCount);
                RemovePacketOwnedTutorSummon();
                return;
            }

            _packetOwnedTutorRuntime.BindRuntimeCharacter(runtimeCharacterId);
            if (!_packetOwnedTutorRuntime.IsActive)
            {
                _packetOwnedTutorRuntime.TryRestoreVisibleActorFromSharedVariant(runtimeCharacterId, currentTickCount);
            }
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

        internal static bool ResolvePacketOwnedRadioCreateLayerLeftState(
            bool hasExplicitContextValue,
            bool explicitContextValue,
            bool minimapExpanded)
        {
            return hasExplicitContextValue
                ? explicitContextValue
                : minimapExpanded;
        }

        internal static bool ShouldRefreshPacketOwnedRadioCreateLayerSessionState(
            bool sessionActive,
            int capturedMutationSequence,
            int liveContextMutationSequence)
        {
            return !sessionActive && capturedMutationSequence != liveContextMutationSequence;
        }

        internal static bool ShouldResetPacketOwnedRadioForRuntimeCharacterChange(
            bool sessionActive,
            int boundCharacterId,
            int runtimeCharacterId)
        {
            return sessionActive
                && boundCharacterId > 0
                && runtimeCharacterId > 0
                && boundCharacterId != runtimeCharacterId;
        }

        private bool ResolvePacketOwnedRadioCreateLayerLeftContext()
        {
            SyncPacketOwnedRadioCreateLayerContextLifecycle();
            bool minimapExpanded = miniMapUi?.IsExpandedOptionActive == true;
            return ResolvePacketOwnedRadioCreateLayerLeftState(
                _packetOwnedLocalUtilityContext.HasRadioCreateLayerLeftContextValue,
                _packetOwnedLocalUtilityContext.RadioCreateLayerLeftContextValue,
                minimapExpanded);
        }

        private string DescribePacketOwnedRadioCreateLayerContextStatus()
        {
            SyncPacketOwnedRadioCreateLayerContextLifecycle();
            return _packetOwnedLocalUtilityContext.DescribeRadioCreateLayerContext(PacketOwnedRadioCreateLayerContextSlot);
        }

        private string SetPacketOwnedRadioCreateLayerContext(bool bLeft)
        {
            int runtimeCharacterId = ResolvePacketOwnedRadioRuntimeCharacterId();
            _packetOwnedLocalUtilityContext.SetRadioCreateLayerLeftContextValue(
                bLeft,
                source: "manual-radioctx",
                currentTick: Environment.TickCount,
                runtimeCharacterId);
            return $"Set packet-owned local utility CWvsContext[{PacketOwnedRadioCreateLayerContextSlot}] (radio bLeft) to {(bLeft ? 1 : 0)}.";
        }

        private string ClearPacketOwnedRadioCreateLayerContext()
        {
            int runtimeCharacterId = ResolvePacketOwnedRadioRuntimeCharacterId();
            _packetOwnedLocalUtilityContext.ClearRadioCreateLayerLeftContextValue(
                source: "manual-radioctx-clear",
                currentTick: Environment.TickCount,
                runtimeCharacterId);
            return $"Cleared packet-owned local utility CWvsContext[{PacketOwnedRadioCreateLayerContextSlot}] (radio bLeft) override.";
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
            return ResolvePacketOwnedTutorSkillIdFromJob(jobId, _packetOwnedTutorRuntime.ActiveSkillId);
        }

        private int ResolvePacketOwnedTutorSkillIdForCharacter(int characterId)
        {
            if (characterId > 0)
            {
                if (_playerManager?.Player?.Build?.Id == characterId)
                {
                    return ResolvePacketOwnedTutorSkillId();
                }

                if (_remoteUserPool != null && _remoteUserPool.TryGetActor(characterId, out RemoteUserActor actor))
                {
                    int remoteFallbackSkillId = _packetOwnedTutorRuntime.TryGetSharedActiveVariantForCharacter(characterId, out TutorVariantSnapshot activeVariant)
                        ? activeVariant.SkillId
                        : 0;
                    return ResolvePacketOwnedTutorSkillIdFromJob(actor.Build?.Job ?? 0, remoteFallbackSkillId);
                }

                if (_packetOwnedTutorRuntime.TryGetSharedActiveVariantForCharacter(characterId, out TutorVariantSnapshot variant))
                {
                    return variant.SkillId;
                }
            }

            return ResolvePacketOwnedTutorSkillId();
        }

        private static int ResolvePacketOwnedTutorSkillIdFromJob(int jobId, int fallbackSkillId)
        {
            int jobFamily = Math.Max(0, jobId) / 1000;
            return jobFamily switch
            {
                1 => TutorRuntime.CygnusTutorSkillId,
                2 => TutorRuntime.AranTutorSkillId,
                _ => fallbackSkillId > 0 ? fallbackSkillId : TutorRuntime.AranTutorSkillId
            };
        }

        private string DescribePacketOwnedTutorStatus(int currentTickCount)
        {
            SyncPacketOwnedTutorLifecycle(currentTickCount);
            if (!_packetOwnedTutorRuntime.IsActive)
            {
                if (_packetOwnedTutorRuntime.TryGetSharedActiveVariant(out TutorVariantSnapshot sharedActiveVariant))
                {
                    string sharedActiveDescription = DescribePacketOwnedTutorVariantSnapshot(sharedActiveVariant);
                    if (_packetOwnedTutorRuntime.TryResolveDisplayMessageSnapshot(sharedActiveVariant, currentTickCount, out TutorMessageSnapshot sharedMessage))
                    {
                        int remainingMs = Math.Max(0, unchecked(sharedMessage.MessageExpiresAt - currentTickCount));
                        if (sharedMessage.MessageKind == TutorMessageKind.Text && !string.IsNullOrWhiteSpace(sharedMessage.MessageText))
                        {
                            return BuildPacketOwnedTutorStatusWithRegisteredVariants(
                                $"Tutor: shared actor visible via {sharedActiveDescription}, {TruncatePacketOwnedUtilityText(sharedMessage.MessageText, 96)} ({remainingMs} ms left).");
                        }

                        if (sharedMessage.MessageKind == TutorMessageKind.Indexed && sharedMessage.LastIndexedMessage >= 0)
                        {
                            return BuildPacketOwnedTutorStatusWithRegisteredVariants(
                                $"Tutor: shared actor visible via {sharedActiveDescription}, cue #{sharedMessage.LastIndexedMessage} ({remainingMs} ms left).");
                        }
                    }

                    return BuildPacketOwnedTutorStatusWithRegisteredVariants(
                        $"Tutor: shared actor visible via {sharedActiveDescription} without a local packet-owned message lane.");
                }

                string clientSlots = DescribePacketOwnedTutorClientSlots();
                string knownVariants = DescribePacketOwnedTutorKnownVariants();
                if (string.IsNullOrWhiteSpace(clientSlots) && string.IsNullOrWhiteSpace(knownVariants))
                {
                    return "Tutor: idle.";
                }

                if (string.IsNullOrWhiteSpace(knownVariants))
                {
                    return $"Tutor: idle. Client tutor slots: {clientSlots}.";
                }

                return string.IsNullOrWhiteSpace(clientSlots)
                    ? $"Tutor: idle. Registered variants: {knownVariants}."
                    : $"Tutor: idle. Client tutor slots: {clientSlots}. Registered variants: {knownVariants}.";
            }

            string variant = DescribePacketOwnedTutorVariant(_packetOwnedTutorRuntime.ActiveSkillId);
            if (_packetOwnedTutorRuntime.HasVisibleTextMessage(currentTickCount))
            {
                int remainingMs = Math.Max(0, unchecked(_packetOwnedTutorRuntime.ActiveMessageExpiresAt - currentTickCount));
                return BuildPacketOwnedTutorStatusWithRegisteredVariants(
                    $"Tutor: {variant}, {TruncatePacketOwnedUtilityText(_packetOwnedTutorRuntime.ActiveMessageText, 96)} ({remainingMs} ms left).");
            }

            if (_packetOwnedTutorRuntime.HasVisibleIndexedCue(currentTickCount))
            {
                int remainingMs = Math.Max(0, unchecked(_packetOwnedTutorRuntime.ActiveMessageExpiresAt - currentTickCount));
                return BuildPacketOwnedTutorStatusWithRegisteredVariants(
                    $"Tutor: {variant}, cue #{Math.Max(0, _packetOwnedTutorRuntime.LastIndexedMessage)} ({remainingMs} ms left).");
            }

            return BuildPacketOwnedTutorStatusWithRegisteredVariants($"Tutor: {variant}, waiting for message.");
        }

        private string DescribePacketOwnedTutorKnownVariants()
        {
            IReadOnlyList<TutorVariantSnapshot> variants = _packetOwnedTutorRuntime.RegisteredTutorVariants;
            if (variants.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(
                ", ",
                variants.Select(DescribePacketOwnedTutorVariantSnapshot));
        }

        private string DescribePacketOwnedTutorClientSlots()
        {
            return _packetOwnedTutorRuntime.HasClientTutorSkillSlots
                ? _packetOwnedTutorRuntime.DescribeClientTutorSkillSlots()
                : string.Empty;
        }

        private string BuildPacketOwnedTutorStatusWithRegisteredVariants(string baseStatus)
        {
            string clientSlots = DescribePacketOwnedTutorClientSlots();
            string knownVariants = DescribePacketOwnedTutorKnownVariants();
            if (string.IsNullOrWhiteSpace(clientSlots) && string.IsNullOrWhiteSpace(knownVariants))
            {
                return baseStatus;
            }

            if (string.IsNullOrWhiteSpace(knownVariants))
            {
                return $"{baseStatus} Client tutor slots: {clientSlots}.";
            }

            return string.IsNullOrWhiteSpace(clientSlots)
                ? $"{baseStatus} Registered variants: {knownVariants}."
                : $"{baseStatus} Client tutor slots: {clientSlots}. Registered variants: {knownVariants}.";
        }

        private string DescribePacketOwnedTutorVariantSnapshot(TutorVariantSnapshot variant)
        {
            string variantName = DescribePacketOwnedTutorVariant(variant.SkillId);
            string state = variant.IsActive ? "active" : "listed";
            string boundCharacter = variant.BoundCharacterId > 0
                ? $"char {variant.BoundCharacterId.ToString(CultureInfo.InvariantCulture)}"
                : "char ?";
            string tickDescriptor = variant.IsActive
                ? variant.LastHireTick == int.MinValue
                    ? "hire tick unknown"
                    : $"hire {variant.LastHireTick.ToString(CultureInfo.InvariantCulture)}"
                : variant.LastRemovalTick == int.MinValue
                    ? variant.LastHireTick == int.MinValue
                        ? "never hired"
                        : $"last hire {variant.LastHireTick.ToString(CultureInfo.InvariantCulture)}"
                    : $"removed {variant.LastRemovalTick.ToString(CultureInfo.InvariantCulture)}";
            return $"{variantName} ({variant.SkillId.ToString(CultureInfo.InvariantCulture)}, {state}, {boundCharacter}, {tickDescriptor})";
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

        private static int ResolvePacketOwnedTutorActorHeight(int skillId)
        {
            int normalizedSkillId = Math.Max(0, skillId);
            if (normalizedSkillId <= 0)
            {
                return TutorRuntime.AranTutorHeight;
            }

            string skillImageName = ResolvePacketOwnedTutorSkillImageName(normalizedSkillId);
            WzImage skillImage = Program.FindImage("Skill", skillImageName);
            WzIntProperty heightProperty =
                skillImage?["skill"]?[normalizedSkillId.ToString(CultureInfo.InvariantCulture)]?["summon"]?["height"] as WzIntProperty;
            if (heightProperty?.Value is int height && height > 0)
            {
                return height;
            }

            return normalizedSkillId == TutorRuntime.CygnusTutorSkillId
                ? TutorRuntime.CygnusTutorHeight
                : TutorRuntime.AranTutorHeight;
        }

        internal static string ResolvePacketOwnedTutorSkillImageName(int skillId)
        {
            int normalizedSkillId = Math.Max(0, skillId);
            int imageId = normalizedSkillId / 10000;
            if (imageId <= 0)
            {
                imageId = normalizedSkillId;
            }

            return $"{imageId}.img";
        }

        private string EnsurePacketOwnedTutorSummon(int currentTickCount)
        {
            if (!TryResolvePacketOwnedTutorDisplayState(out TutorVariantSnapshot displayVariant, out PacketOwnedTutorDisplayOwner displayOwner))
            {
                RemovePacketOwnedTutorSummon();
                return _packetOwnedTutorRuntime.IsActive
                    ? "Tutor summon is waiting for a bound owner actor."
                    : null;
            }

            return EnsurePacketOwnedTutorSummon(displayVariant, displayOwner, currentTickCount);
        }

        private string EnsurePacketOwnedTutorSummon(
            TutorVariantSnapshot displayVariant,
            PacketOwnedTutorDisplayOwner displayOwner,
            int currentTickCount)
        {
            if (TryGetPacketOwnedTutorSummon(displayVariant, displayOwner.CharacterId, out ActiveSummon summon))
            {
                _packetOwnedTutorTrackedSummonObjectIds.Add(summon.ObjectId);
                SyncPacketOwnedTutorSummonPose(summon, displayVariant, displayOwner, currentTickCount);
                return null;
            }

            int ownerCharacterId = displayOwner.CharacterId;
            int skillLevel = ResolvePacketOwnedTutorDisplaySkillLevel(displayVariant, displayOwner);
            byte moveAction = displayOwner.FacingRight ? (byte)0 : (byte)1;
            var packet = new SummonedCreatePacket(
                ownerCharacterId,
                displayVariant.SummonObjectId,
                displayVariant.SkillId,
                Math.Max(1, displayOwner.Level),
                skillLevel,
                displayOwner.Position,
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

            if (TryGetPacketOwnedTutorSummon(displayVariant, displayOwner.CharacterId, out summon))
            {
                _packetOwnedTutorTrackedSummonObjectIds.Add(summon.ObjectId);
                SyncPacketOwnedTutorSummonPose(summon, displayVariant, displayOwner, currentTickCount);
            }

            return "Tutor summon owner created from the packet-owned summoned seam.";
        }

        private void SyncPacketOwnedTutorSummonState(int currentTickCount)
        {
            List<PacketOwnedTutorDisplayState> displayStates = ResolvePacketOwnedTutorDisplayStates();
            if (displayStates.Count == 0)
            {
                RemovePacketOwnedTutorSummon();
                return;
            }

            HashSet<int> activeSummonObjectIds = new();
            for (int i = 0; i < displayStates.Count; i++)
            {
                PacketOwnedTutorDisplayState displayState = displayStates[i];
                if (displayState.Variant.SummonObjectId > 0)
                {
                    activeSummonObjectIds.Add(displayState.Variant.SummonObjectId);
                }

                EnsurePacketOwnedTutorSummon(displayState.Variant, displayState.Owner, currentTickCount);
            }

            PrunePacketOwnedTutorSummons(activeSummonObjectIds);
        }

        private void SyncPacketOwnedTutorSummonPose(
            ActiveSummon summon,
            TutorVariantSnapshot displayVariant,
            PacketOwnedTutorDisplayOwner owner,
            int currentTickCount)
        {
            if (summon == null || owner.CharacterId <= 0)
            {
                return;
            }

            summon.PreviousPositionX = summon.PositionX;
            summon.PreviousPositionY = summon.PositionY;
            summon.AnchorX = owner.Position.X;
            summon.AnchorY = owner.Position.Y;
            summon.PositionX = owner.Position.X;
            summon.PositionY = owner.Position.Y;
            summon.FacingRight = owner.FacingRight;
            summon.LastStateChangeTime = currentTickCount;
            bool hasVisibleMessage = _packetOwnedTutorRuntime.TryResolveDisplayMessageSnapshot(
                displayVariant,
                currentTickCount,
                out TutorMessageSnapshot displayMessage);
            _packetOwnedTutorSummonMessageSequenceIdsByObjectId.TryGetValue(summon.ObjectId, out int lastMessageSequenceId);
            bool shouldTriggerSayPlayback = hasVisibleMessage
                && lastMessageSequenceId != displayMessage.MessageSequenceId
                && (displayMessage.MessageKind == TutorMessageKind.Text
                    || displayVariant.SkillId != TutorRuntime.AranTutorSkillId);

            if (shouldTriggerSayPlayback)
            {
                summon.LastAttackAnimationStartTime = displayMessage.MessageStartedAt == int.MinValue
                    ? currentTickCount
                    : displayMessage.MessageStartedAt;
                summon.CurrentAnimationBranchName = "say";
                _packetOwnedTutorSummonMessageSequenceIdsByObjectId[summon.ObjectId] = displayMessage.MessageSequenceId;
            }
            else if (!hasVisibleMessage)
            {
                summon.LastAttackAnimationStartTime = int.MinValue;
                summon.CurrentAnimationBranchName = null;
                _packetOwnedTutorSummonMessageSequenceIdsByObjectId.Remove(summon.ObjectId);
            }

            summon.ActorState = SummonActorState.Idle;
        }

        private void RemovePacketOwnedTutorSummon()
        {
            foreach (int objectId in _packetOwnedTutorTrackedSummonObjectIds.ToArray())
            {
                _summonedPool.TryConsumeSummonByObjectId(objectId);
            }

            _packetOwnedTutorTrackedSummonObjectIds.Clear();
            _packetOwnedTutorSummonMessageSequenceIdsByObjectId.Clear();
            IReadOnlyList<TutorVariantSnapshot> activeVariants = _packetOwnedTutorRuntime.SnapshotActiveDisplayTutorVariants();
            for (int i = 0; i < activeVariants.Count; i++)
            {
                if (activeVariants[i].SummonObjectId > 0)
                {
                    _summonedPool.TryConsumeSummonByObjectId(activeVariants[i].SummonObjectId);
                }
            }

            _summonedPool.TryConsumeSummonByObjectId(_packetOwnedTutorRuntime.ActiveSummonObjectId);
            _summonedPool.TryConsumeSummonByObjectId(TutorRuntime.CygnusTutorObjectId);
            _summonedPool.TryConsumeSummonByObjectId(TutorRuntime.AranTutorObjectId);
        }

        private bool TryGetPacketOwnedTutorSummon(out ActiveSummon summon)
        {
            summon = null;
            if (!TryResolvePacketOwnedTutorDisplayState(out TutorVariantSnapshot displayVariant, out PacketOwnedTutorDisplayOwner displayOwner))
            {
                return false;
            }

            return TryGetPacketOwnedTutorSummon(displayVariant, displayOwner.CharacterId, out summon);
        }

        private bool TryGetPacketOwnedTutorSummon(
            TutorVariantSnapshot displayVariant,
            int ownerCharacterId,
            out ActiveSummon summon)
        {
            summon = null;
            if (ownerCharacterId <= 0 || displayVariant.SummonObjectId <= 0)
            {
                return false;
            }

            summon = _summonedPool?.GetSummonsForOwner(ownerCharacterId)?
                .FirstOrDefault(candidate => candidate?.ObjectId == displayVariant.SummonObjectId && !candidate.IsPendingRemoval);
            return summon != null;
        }

        private void DrawPacketOwnedTutorState(int currentTickCount, int mapCenterX, int mapCenterY)
        {
            if (_spriteBatch == null)
            {
                return;
            }

            List<PacketOwnedTutorDisplayState> displayStates = ResolvePacketOwnedTutorDisplayStates();
            if (displayStates.Count == 0)
            {
                return;
            }

            for (int i = 0; i < displayStates.Count; i++)
            {
                PacketOwnedTutorDisplayState displayState = displayStates[i];
                if (!_packetOwnedTutorRuntime.TryResolveDisplayMessageSnapshot(
                    displayState.Variant,
                    currentTickCount,
                    out TutorMessageSnapshot displayMessage))
                {
                    continue;
                }

                DrawPacketOwnedTutorIndexedCue(
                    displayState.Variant,
                    displayState.Owner,
                    displayMessage,
                    currentTickCount,
                    mapCenterX,
                    mapCenterY);

                if (displayMessage.MessageKind != TutorMessageKind.Text
                    || string.IsNullOrWhiteSpace(displayMessage.MessageText)
                    || _fontChat == null
                    || _packetOwnedTutorBalloonSkin?.IsLoaded != true)
                {
                    continue;
                }

                if (!TryResolvePacketOwnedTutorBalloonAnchorScreenPoint(
                    displayState.Variant,
                    displayState.Owner,
                    mapCenterX,
                    mapCenterY,
                    out Point anchor))
                {
                    continue;
                }

                DrawPacketOwnedTutorBalloon(displayState.Variant, displayMessage, anchor);
            }
        }

        private void DrawPacketOwnedTutorIndexedCue(
            TutorVariantSnapshot displayVariant,
            PacketOwnedTutorDisplayOwner displayOwner,
            TutorMessageSnapshot displayMessage,
            int currentTickCount,
            int mapCenterX,
            int mapCenterY)
        {
            if (displayMessage.MessageKind != TutorMessageKind.Indexed
                || displayMessage.LastIndexedMessage < 0
                || currentTickCount >= displayMessage.MessageExpiresAt)
            {
                return;
            }

            List<IDXObject> frames = ResolvePacketOwnedTutorCueFrames(displayMessage.LastIndexedMessage);
            if (frames == null || frames.Count == 0)
            {
                return;
            }

            if (!TryResolvePacketOwnedTutorActorScreenPoint(displayVariant, displayOwner, mapCenterX, mapCenterY, out Point anchor))
            {
                return;
            }

            IDXObject frame = ResolvePacketOwnedComboFrame(
                frames,
                currentTickCount,
                displayMessage.MessageStartedAt == int.MinValue
                    ? currentTickCount
                    : displayMessage.MessageStartedAt);
            if (frame?.Texture == null)
            {
                return;
            }

            Vector2 position;
            int actorHeight = Math.Max(
                0,
                displayVariant.ActorHeight > 0
                    ? displayVariant.ActorHeight
                    : ResolvePacketOwnedTutorActorHeight(displayVariant.SkillId));
            if (frame.Texture.Width > 0 && frame.Texture.Height > 0 && actorHeight > 0)
            {
                // Client evidence: CTutor::OnMessage(long,long) positions numeric cues using
                // x = -(layerWidth / 2) and y = -(layerHeight + summonHeight).
                position = new Vector2(
                    anchor.X - (frame.Texture.Width / 2f),
                    anchor.Y - (frame.Texture.Height + actorHeight));
            }
            else
            {
                position = new Vector2(anchor.X - frame.X, anchor.Y - frame.Y);
            }

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

        private void DrawPacketOwnedTutorBalloon(
            TutorVariantSnapshot displayVariant,
            TutorMessageSnapshot displayMessage,
            Point anchor)
        {
            int requestedWidth = Math.Clamp(
                displayMessage.MessageWidth <= 0 ? TutorRuntime.DefaultTextWidth : displayMessage.MessageWidth,
                TutorRuntime.MinTextWidth,
                TutorRuntime.MaxTextWidth);
            PacketOwnedBalloonWrappedLine[] lines = WrapPacketOwnedBalloonText(displayMessage.MessageText, requestedWidth);
            if (lines.Length == 0)
            {
                return;
            }

            Vector2 lineMeasure = MeasureChatTextWithFallback("Ay");
            int lineHeight = Math.Max(14, (int)Math.Ceiling(lineMeasure.Y));
            Texture2D northWest = _packetOwnedTutorBalloonSkin.NorthWest;
            Texture2D northEast = _packetOwnedTutorBalloonSkin.NorthEast;
            Texture2D southWest = _packetOwnedTutorBalloonSkin.SouthWest;
            Texture2D southEast = _packetOwnedTutorBalloonSkin.SouthEast;
            int leftInset = Math.Max(
                PacketOwnedTutorBalloonClientLeftInset,
                Math.Max(
                    Math.Max(northWest?.Width ?? 0, southWest?.Width ?? 0),
                    _packetOwnedTutorBalloonSkin.West?.Width ?? 0));
            int rightInset = Math.Max(
                PacketOwnedTutorBalloonClientRightInset,
                Math.Max(
                    Math.Max(northEast?.Width ?? 0, southEast?.Width ?? 0),
                    _packetOwnedTutorBalloonSkin.East?.Width ?? 0));
            int topInset = Math.Max(
                PacketOwnedTutorBalloonClientTopInset,
                Math.Max(
                    Math.Max(northWest?.Height ?? 0, northEast?.Height ?? 0),
                    _packetOwnedTutorBalloonSkin.North?.Height ?? 0));
            int bottomInset = Math.Max(
                PacketOwnedTutorBalloonClientBottomInset,
                Math.Max(
                    Math.Max(southWest?.Height ?? 0, southEast?.Height ?? 0),
                    _packetOwnedTutorBalloonSkin.South?.Height ?? 0));
            int bodyWidth = requestedWidth + leftInset + rightInset;
            int bodyHeight = (lines.Length * lineHeight) + topInset + bottomInset;
            Texture2D arrowTexture = _packetOwnedTutorBalloonSkin.Arrow?.Texture;
            int arrowWidth = arrowTexture?.Width ?? 0;
            int arrowHeight = arrowTexture?.Height ?? 0;

            int actorHeight = Math.Max(
                0,
                displayVariant.ActorHeight > 0
                    ? displayVariant.ActorHeight
                    : ResolvePacketOwnedTutorActorHeight(displayVariant.SkillId));

            Point layerOrigin = ResolvePacketOwnedTutorBalloonLayerOrigin(
                anchor,
                requestedWidth,
                lineHeight * lines.Length,
                actorHeight);
            Rectangle bodyBounds = new(layerOrigin.X, layerOrigin.Y, bodyWidth, bodyHeight);
            Rectangle arrowBounds = ResolvePacketOwnedTutorBalloonArrowBounds(
                bodyBounds,
                lineHeight * lines.Length,
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

            // Client evidence: CTutor::OnMessage(ZXString&,long,long) uses the authored
            // width as the text analyzer box and offsets the wrapped text by the
            // tutorial frame extents instead of centering each line inside the bubble.
            float drawY = bodyBounds.Y + topInset;
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                PacketOwnedBalloonWrappedLine line = lines[lineIndex];
                if (!line.PreservesLineHeight && (line.Runs == null || line.Runs.Length == 0))
                {
                    drawY += lineHeight;
                    continue;
                }

                float drawX = bodyBounds.X + leftInset;
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

        internal static Point ResolvePacketOwnedTutorBalloonLayerOrigin(
            Point actorScreenPoint,
            int requestedWidth,
            int contentHeight,
            int actorHeight)
        {
            return new Point(
                actorScreenPoint.X - Math.Max(0, requestedWidth / 2),
                actorScreenPoint.Y - Math.Max(0, contentHeight) - Math.Max(0, actorHeight) - PacketOwnedTutorBalloonClientVerticalAnchorOffset);
        }

        internal static int ResolvePacketOwnedTutorBalloonArrowLeft(Rectangle bodyBounds, int requestedWidth)
        {
            return bodyBounds.X + Math.Max(0, requestedWidth / 2) + PacketOwnedTutorBalloonClientArrowLeftOffset;
        }

        internal static int ResolvePacketOwnedTutorBalloonArrowTop(Rectangle bodyBounds, int contentHeight)
        {
            return bodyBounds.Y + Math.Max(0, contentHeight) + PacketOwnedTutorBalloonClientArrowTopOffset;
        }

        internal static Rectangle ResolvePacketOwnedTutorBalloonArrowBounds(
            Rectangle bodyBounds,
            int contentHeight,
            int arrowWidth,
            int arrowHeight)
        {
            if (arrowWidth <= 0 || arrowHeight <= 0)
            {
                return Rectangle.Empty;
            }

            return new Rectangle(
                ResolvePacketOwnedTutorBalloonArrowLeft(bodyBounds, bodyBounds.Width - PacketOwnedTutorBalloonClientLeftInset - PacketOwnedTutorBalloonClientRightInset),
                ResolvePacketOwnedTutorBalloonArrowTop(bodyBounds, contentHeight),
                arrowWidth,
                arrowHeight);
        }

        private bool TryResolvePacketOwnedTutorBalloonAnchorScreenPoint(
            TutorVariantSnapshot displayVariant,
            PacketOwnedTutorDisplayOwner displayOwner,
            int mapCenterX,
            int mapCenterY,
            out Point anchor)
        {
            return TryResolvePacketOwnedTutorActorScreenPoint(displayVariant, displayOwner, mapCenterX, mapCenterY, out anchor);
        }

        private bool TryResolvePacketOwnedTutorActorScreenPoint(
            TutorVariantSnapshot displayVariant,
            PacketOwnedTutorDisplayOwner displayOwner,
            int mapCenterX,
            int mapCenterY,
            out Point anchor)
        {
            anchor = Point.Zero;

            if (!TryGetPacketOwnedTutorSummon(displayVariant, displayOwner.CharacterId, out ActiveSummon summon))
            {
                if (displayOwner.CharacterId <= 0)
                {
                    return false;
                }

                anchor = new Point(
                    (int)Math.Round(displayOwner.Position.X - mapShiftX + mapCenterX),
                    (int)Math.Round(displayOwner.Position.Y - mapShiftY + mapCenterY));
                return true;
            }

            anchor = new Point(
                (int)Math.Round(summon.PositionX - mapShiftX + mapCenterX),
                (int)Math.Round(summon.PositionY - mapShiftY + mapCenterY));
            return true;
        }

        private List<PacketOwnedTutorDisplayState> ResolvePacketOwnedTutorDisplayStates()
        {
            IReadOnlyList<TutorVariantSnapshot> variants = _packetOwnedTutorRuntime.SnapshotActiveDisplayTutorVariants();
            List<PacketOwnedTutorDisplayState> states = new(variants.Count);
            HashSet<long> emittedVariantKeys = new();
            for (int i = 0; i < variants.Count; i++)
            {
                TutorVariantSnapshot variant = variants[i];
                if (variant.SkillId <= 0
                    || !emittedVariantKeys.Add(BuildPacketOwnedTutorDisplayVariantKey(variant))
                    || !TryResolvePacketOwnedTutorDisplayOwner(variant, out PacketOwnedTutorDisplayOwner owner))
                {
                    continue;
                }

                states.Add(new PacketOwnedTutorDisplayState(variant, owner));
            }

            return states;
        }

        private void PrunePacketOwnedTutorSummons(HashSet<int> activeSummonObjectIds)
        {
            foreach (int objectId in _packetOwnedTutorTrackedSummonObjectIds.ToArray())
            {
                if (!activeSummonObjectIds.Contains(objectId))
                {
                    _summonedPool.TryConsumeSummonByObjectId(objectId);
                    _packetOwnedTutorTrackedSummonObjectIds.Remove(objectId);
                    _packetOwnedTutorSummonMessageSequenceIdsByObjectId.Remove(objectId);
                }
            }
        }

        private static long BuildPacketOwnedTutorDisplayVariantKey(TutorVariantSnapshot variant)
        {
            return ((long)Math.Max(0, variant.SkillId) << 32) | (uint)Math.Max(0, variant.BoundCharacterId);
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
            string promptText = MapleStoryStringPool.GetOrFallback(
                FollowRequestPromptStringPoolId,
                "%s has requested to follow you.\r\nWould you like to accept?");
            string displayName = string.IsNullOrWhiteSpace(requesterName)
                ? requesterId > 0 ? $"Character {requesterId}" : "Unknown character"
                : requesterName.Trim();
            return promptText.Replace("%s", displayName, StringComparison.Ordinal);
        }

        private static PacketOwnedFollowPromptOwnerKind ResolvePacketOwnedFollowPromptOwnerKind(bool hasInGameConfirmDialogWindow)
        {
            return hasInGameConfirmDialogWindow
                ? PacketOwnedFollowPromptOwnerKind.InGameConfirmDialog
                : PacketOwnedFollowPromptOwnerKind.LoginUtilityDialog;
        }

        internal static PacketOwnedFollowPromptOwnerKind ResolvePacketOwnedFollowPromptOwnerKindForTest(bool hasInGameConfirmDialogWindow)
        {
            return ResolvePacketOwnedFollowPromptOwnerKind(hasInGameConfirmDialogWindow);
        }

        internal static bool ShouldDismissPacketOwnedFollowPromptForRemovedCharacterForTest(
            bool promptActive,
            int incomingRequesterId,
            int removedCharacterId)
        {
            return promptActive
                && incomingRequesterId > 0
                && incomingRequesterId == removedCharacterId;
        }

        private void HidePacketOwnedFollowCharacterPrompt()
        {
            if (_packetOwnedFollowPromptOwner == PacketOwnedFollowPromptOwnerKind.InGameConfirmDialog
                && uiWindowManager?.GetWindow(MapSimulatorWindowNames.InGameConfirmDialog) is InGameConfirmDialogWindow confirmDialogWindow)
            {
                confirmDialogWindow.Hide();
                ClearInGameConfirmDialogActions();
            }

            if (_packetOwnedFollowPromptOwner == PacketOwnedFollowPromptOwnerKind.LoginUtilityDialog
                && _loginUtilityDialogAction == LoginUtilityDialogAction.ConfirmFollowCharacterRequest)
            {
                HideLoginUtilityDialog();
            }

            _packetOwnedFollowPromptActive = false;
            _packetOwnedFollowPromptOwner = PacketOwnedFollowPromptOwnerKind.None;
        }

        private bool TryMirrorPacketOwnedFollowRequestToOfficialSession(int driverId, bool autoRequest, bool keyInput, out string status)
        {
            status = null;
            if (!_localUtilityOfficialSessionBridge.HasConnectedSession)
            {
                return true;
            }

            return _localUtilityOfficialSessionBridge.TrySendOutboundPacket(
                LocalFollowCharacterRuntime.FollowRequestOpcode,
                LocalUtilityOfficialSessionBridgeManager.BuildFollowCharacterRequestPayload(driverId, autoRequest, keyInput),
                out status);
        }

        private bool TryMirrorPacketOwnedFollowWithdrawToOfficialSession(out string status)
        {
            status = null;
            if (!_localUtilityOfficialSessionBridge.HasConnectedSession)
            {
                return true;
            }

            return _localUtilityOfficialSessionBridge.TrySendOutboundPacket(
                LocalFollowCharacterRuntime.FollowWithdrawOpcode,
                LocalUtilityOfficialSessionBridgeManager.BuildFollowCharacterWithdrawPayload(),
                out status);
        }

        private void AcceptPacketOwnedFollowCharacterPrompt()
        {
            if (_localFollowRuntime.IncomingRequesterId <= 0)
            {
                HidePacketOwnedFollowCharacterPrompt();
                return;
            }

            if (!TryResolvePacketOwnedRemoteCharacterSnapshot(_localFollowRuntime.IncomingRequesterId, out LocalFollowUserSnapshot requester))
            {
                ShowUtilityFeedbackMessage("Incoming follow request could not be accepted because the requester is no longer available.");
                HidePacketOwnedFollowCharacterPrompt();
                return;
            }

            int localCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
            if (localCharacterId <= 0)
            {
                ShowUtilityFeedbackMessage("Incoming follow request could not be accepted because the local player is not fully initialized.");
                HidePacketOwnedFollowCharacterPrompt();
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
                HidePacketOwnedFollowCharacterPrompt();
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
                HidePacketOwnedFollowCharacterPrompt();
                return;
            }

            HidePacketOwnedFollowCharacterPrompt();
            if (!TryMirrorPacketOwnedFollowRequestToOfficialSession(0, autoRequest: false, keyInput: true, out string bridgeStatus))
            {
                message = $"{message} {bridgeStatus}".Trim();
                _chat?.AddErrorMessage(bridgeStatus, currTickCount);
            }
            else if (!string.IsNullOrWhiteSpace(bridgeStatus))
            {
                message = $"{message} {bridgeStatus}".Trim();
            }

            ShowUtilityFeedbackMessage(message);
        }

        private bool TryOpenPacketOwnedFollowCharacterPrompt(LocalFollowUserSnapshot requester, out string message)
        {
            if (!IsFollowRequestOptionEnabled())
            {
                message = "Incoming follow request could not be opened because follow requests are disabled in the client option owner.";
                return false;
            }

            StampPacketOwnedUtilityRequestState();
            if (!_localFollowRuntime.TryQueueIncomingRequest(requester, out message))
            {
                return false;
            }

            string promptBody = BuildPacketOwnedFollowPromptBody(requester.Name, requester.CharacterId);
            PacketOwnedFollowPromptOwnerKind promptOwner = ResolvePacketOwnedFollowPromptOwnerKind(
                uiWindowManager?.GetWindow(MapSimulatorWindowNames.InGameConfirmDialog) is InGameConfirmDialogWindow);
            _packetOwnedFollowPromptActive = true;
            _packetOwnedFollowPromptOwner = promptOwner;

            if (promptOwner == PacketOwnedFollowPromptOwnerKind.InGameConfirmDialog
                && uiWindowManager?.GetWindow(MapSimulatorWindowNames.InGameConfirmDialog) is InGameConfirmDialogWindow confirmDialogWindow)
            {
                ConfigureInGameConfirmDialog(
                    "Follow Request",
                    promptBody,
                    $"Recovered packet-owned in-field FadeYesNo follow prompt for requester {requester.CharacterId}.",
                    onConfirm: AcceptPacketOwnedFollowCharacterPrompt,
                    onCancel: DeclinePacketOwnedFollowCharacterPrompt);
                ShowWindow(
                    MapSimulatorWindowNames.InGameConfirmDialog,
                    confirmDialogWindow,
                    trackDirectionModeOwner: true);
                return true;
            }

            ShowLoginUtilityDialog(
                "Follow Request",
                promptBody,
                LoginUtilityDialogButtonLayout.YesNo,
                LoginUtilityDialogAction.ConfirmFollowCharacterRequest,
                frameVariant: LoginUtilityDialogFrameVariant.InGameFadeYesNo,
                trackDirectionModeOwner: true);
            return true;
        }

        private void TryHandlePacketOwnedLocalFollowReleaseInput(
            KeyboardState newKeyboardState,
            KeyboardState oldKeyboardState,
            bool isWindowActive,
            bool keyboardCaptured,
            int currentTime)
        {
            bool releaseKeyPressed = LocalFollowCharacterRuntime.IsAttachedReleaseKeyPressed(newKeyboardState, oldKeyboardState);
            if (!isWindowActive
                || keyboardCaptured
                || _gameState?.IsPlayerInputEnabled != true
                || !releaseKeyPressed)
            {
                return;
            }

            if (_passiveTransferRequestPending)
            {
                ClearPassiveTransferRequest();
            }

            if (_localFollowRuntime.AttachedDriverId <= 0)
            {
                return;
            }

            StampPacketOwnedUtilityRequestState();
            if (!_localFollowRuntime.TrySendAttachedReleaseRequest(currentTime, ResolvePacketOwnedRemoteCharacterName, out string message))
            {
                ShowUtilityFeedbackMessage(message);
                return;
            }

            if (!TryMirrorPacketOwnedFollowRequestToOfficialSession(0, autoRequest: false, keyInput: true, out string bridgeStatus))
            {
                message = $"{message} {bridgeStatus}".Trim();
                _chat?.AddErrorMessage(bridgeStatus, currentTime);
            }
            else if (!string.IsNullOrWhiteSpace(bridgeStatus))
            {
                message = $"{message} {bridgeStatus}".Trim();
            }

            ShowUtilityFeedbackMessage(message);
        }

        private void DeclinePacketOwnedFollowCharacterPrompt()
        {
            string message = TryResolvePacketOwnedRemoteCharacterSnapshot(_localFollowRuntime.IncomingRequesterId, out LocalFollowUserSnapshot requester)
                ? _localFollowRuntime.DeclineIncomingRequest(requester)
                : _localFollowRuntime.DeclineIncomingRequest(LocalFollowUserSnapshot.Missing(_localFollowRuntime.IncomingRequesterId));
            HidePacketOwnedFollowCharacterPrompt();
            if (!TryMirrorPacketOwnedFollowWithdrawToOfficialSession(out string bridgeStatus))
            {
                message = $"{message} {bridgeStatus}".Trim();
                _chat?.AddErrorMessage(bridgeStatus, currTickCount);
            }
            else if (!string.IsNullOrWhiteSpace(bridgeStatus))
            {
                message = $"{message} {bridgeStatus}".Trim();
            }

            ShowUtilityFeedbackMessage(message);
        }

        private string ApplyPacketOwnedFollowCharacterFailed(string message)
        {
            StampPacketOwnedUtilityRequestState();
            _lastPacketOwnedFollowFailureMessage = string.IsNullOrWhiteSpace(message)
                ? FollowCharacterFailureCodec.Resolve(0, 0, ResolvePacketOwnedRemoteCharacterName).Message
                : message.Trim();
            _lastPacketOwnedFollowFailureTick = Environment.TickCount;
            _lastPacketOwnedFollowFailureReason = null;
            _lastPacketOwnedFollowFailureDriverId = 0;
            _lastPacketOwnedFollowFailureClearedPending = false;
            _localFollowRuntime.ApplyFollowFailureText(_lastPacketOwnedFollowFailureMessage);
            _chat?.AddClientChatMessage($"[Error] {_lastPacketOwnedFollowFailureMessage}", Environment.TickCount, 15);
            NotifyEventAlarmOwnerActivity("packet-owned follow failure");
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
                ClearPacketOwnedPassiveMoveState();
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

            ClearPacketOwnedPassiveMoveState();
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
            _lastPacketOwnedFollowFailureTick = Environment.TickCount;
            _localFollowRuntime.ApplyFollowFailure(info);
            NotifyEventAlarmOwnerActivity("packet-owned follow failure");

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

        private bool TryApplyPacketOwnedPassiveMovePayload(byte[] payload, out string message)
        {
            if (_localFollowRuntime.AttachedDriverId <= 0)
            {
                message = "Ignored packet-owned passive-move payload because no local follow driver is attached.";
                return true;
            }

            PlayerCharacter player = _playerManager?.Player;
            if (player == null)
            {
                message = "Packet-owned passive-move payload could not be applied because the local player is not fully initialized.";
                return false;
            }

            if (!RemoteUserPacketCodec.TryParsePassiveMove(payload, currTickCount, out PlayerMovementSyncSnapshot snapshot, out _, out string error))
            {
                message = error;
                return false;
            }

            _lastPacketOwnedPassiveMoveSnapshot = snapshot;
            _lastPacketOwnedPassiveMoveTick = currTickCount;

            PassivePositionSnapshot passiveMove = snapshot.SampleAtTime(currTickCount);
            player.ApplyPacketOwnedPassiveMove(
                passiveMove,
                currTickCount,
                ResolvePacketOwnedLocalFollowFoothold(passiveMove.FootholdId));
            message =
                $"Applied packet-owned passive move for attached driver {_localFollowRuntime.AttachedDriverId}; move-path samples={snapshot.MovePath.Count}, final=({snapshot.PassivePosition.X},{snapshot.PassivePosition.Y}).";
            return true;
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

        private bool TryApplyPacketOwnedFollowCharacterPayload(byte[] payload, bool clientOpcodePayload, out string message)
        {
            int localCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
            if (localCharacterId <= 0)
            {
                message = "Packet-owned follow-character payload could not be applied because the local player is not fully initialized.";
                return false;
            }

            bool parsed = clientOpcodePayload
                ? RemoteUserPacketCodec.TryParseClientFollowCharacter(payload, localCharacterId, out RemoteUserFollowCharacterPacket packet, out string error)
                : RemoteUserPacketCodec.TryParseFollowCharacter(payload, out packet, out error, localCharacterId);
            if (!parsed)
            {
                message = error;
                return false;
            }

            message = ApplyPacketOwnedLocalFollowCharacter(packet);
            return true;
        }

        private bool TryApplyPacketOwnedFollowCharacterPromptPayload(byte[] payload, out string message)
        {
            message = "Follow-request prompt payload is missing.";
            if (payload == null || payload.Length == 0)
            {
                return false;
            }

            int requesterId;
            if (payload.Length == sizeof(int))
            {
                requesterId = BitConverter.ToInt32(payload, 0);
            }
            else if (TryDecodePacketOwnedStringPayload(payload, out string requesterToken)
                && TryResolvePacketOwnedRemoteCharacterToken(requesterToken, out int decodedRequesterId, out _))
            {
                requesterId = decodedRequesterId;
            }
            else
            {
                message = "Follow-request prompt payload must contain a requester Int32 or resolvable character token.";
                return false;
            }

            if (requesterId <= 0
                || !TryResolvePacketOwnedRemoteCharacterSnapshot(requesterId, out LocalFollowUserSnapshot requester))
            {
                message = "Follow-request prompt could not be opened because the requester is not available in the remote-user pool.";
                return false;
            }

            return TryOpenPacketOwnedFollowCharacterPrompt(requester, out message);
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
                _lastPacketOwnedMinigameSoundTick = Environment.TickCount;
            }
            else
            {
                _lastPacketOwnedEventSoundDescriptor = resolvedDescriptor;
                _lastPacketOwnedEventSoundTick = Environment.TickCount;
            }

            string message = $"Played packet-owned {(minigame ? "minigame" : "event")} sound {resolvedDescriptor}.";
            NotifyEventAlarmOwnerActivity(minigame ? "packet-owned minigame sound" : "packet-owned event sound");
            ShowUtilityFeedbackMessage(message);
            return message;
        }

        private bool TryPlaySharedProductionEnhancementSound(int stringPoolId, string fallbackDescriptor)
        {
            string descriptor = MapleStoryStringPool.GetOrFallback(stringPoolId, fallbackDescriptor);
            return TryPlayPacketOwnedWzSound(descriptor, "UI.img", out _, out _);
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

            foreach (string descriptorCandidate in BuildPacketOwnedWzSoundDescriptorCandidates(descriptor, defaultImageName))
            {
                if (!TrySplitPacketOwnedClientSoundDescriptor(descriptorCandidate, out string imageName, out string propertyPath))
                {
                    continue;
                }

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

        private int ResolvePacketOwnedTimeBombHitPeriodMs(int baseHitPeriodMs)
        {
            CharacterPart armorPart = ResolvePacketOwnedTimeBombArmorPart();
            IReadOnlyList<int> itemOptionIds = ResolvePacketOwnedTimeBombArmorItemOptionIds(armorPart);
            if (itemOptionIds.Count == 0)
            {
                return Math.Max(0, baseHitPeriodMs);
            }

            int equipLevel = ResolvePacketOwnedTimeBombItemOptionEquipLevel(armorPart.RequiredLevel);
            if (equipLevel <= 0)
            {
                return Math.Max(0, baseHitPeriodMs);
            }

            var optionLevels = new List<PacketOwnedTimeBombInvincibilityOptionLevelData>(itemOptionIds.Count);
            foreach (int itemOptionId in itemOptionIds)
            {
                PacketOwnedTimeBombInvincibilityOptionLevelData? optionLevel = ResolvePacketOwnedTimeBombInvincibilityOptionLevel(itemOptionId, equipLevel);
                if (optionLevel.HasValue)
                {
                    optionLevels.Add(optionLevel.Value);
                }
            }

            return ApplyPacketOwnedTimeBombInvincibilityOptions(
                baseHitPeriodMs,
                optionLevels,
                Random.Shared.Next(0, 101));
        }

        private IReadOnlyList<int> ResolvePacketOwnedTimeBombArmorItemOptionIds(CharacterPart armorPart)
        {
            if (armorPart?.ItemOptionIds != null && armorPart.ItemOptionIds.Count > 0)
            {
                return armorPart.ItemOptionIds;
            }

            if (armorPart?.PotentialLines == null || armorPart.PotentialLines.Count == 0)
            {
                return Array.Empty<int>();
            }

            Dictionary<int, PacketOwnedTimeBombInvincibilityOptionDefinition> candidateDefinitions = BuildPacketOwnedTimeBombInvincibilityOptionDefinitions();
            if (candidateDefinitions.Count == 0)
            {
                return Array.Empty<int>();
            }

            return InferPacketOwnedTimeBombInvincibilityOptionIds(armorPart.PotentialLines, candidateDefinitions);
        }

        private CharacterPart ResolvePacketOwnedTimeBombArmorPart()
        {
            CharacterBuild build = _playerManager?.Player?.Build;
            if (build == null)
            {
                return null;
            }

            foreach (EquipSlot slot in new[] { EquipSlot.Longcoat, EquipSlot.Coat })
            {
                CharacterPart displayed = EquipSlotStateResolver.ResolveDisplayedPart(build, slot);
                if (displayed?.IsCash == true)
                {
                    CharacterPart underlying = EquipSlotStateResolver.ResolveUnderlyingPart(build, slot);
                    if (underlying != null)
                    {
                        return underlying;
                    }
                }

                if (displayed != null)
                {
                    return displayed;
                }
            }

            return null;
        }

        internal static int ResolvePacketOwnedTimeBombItemOptionEquipLevel(int requiredLevel)
        {
            return requiredLevel <= 0
                ? 0
                : Math.Clamp((requiredLevel - 1) / 10, 1, 20);
        }

        internal static int ApplyPacketOwnedTimeBombInvincibilityOptions(
            int baseHitPeriodMs,
            IEnumerable<PacketOwnedTimeBombInvincibilityOptionLevelData> optionLevels,
            int probabilityRollInclusivePercent)
        {
            int resolvedHitPeriodMs = Math.Max(0, baseHitPeriodMs);
            int additionalDurationMs = 0;
            int probabilisticDurationMs = 0;
            int probabilisticChancePercent = 0;

            if (optionLevels != null)
            {
                foreach (PacketOwnedTimeBombInvincibilityOptionLevelData optionLevel in optionLevels)
                {
                    if (optionLevel.DurationMs <= 0)
                    {
                        continue;
                    }

                    if (optionLevel.ProbabilityPercent > 0)
                    {
                        probabilisticChancePercent = Math.Max(probabilisticChancePercent, optionLevel.ProbabilityPercent);
                        probabilisticDurationMs = Math.Max(probabilisticDurationMs, optionLevel.DurationMs);
                        continue;
                    }

                    additionalDurationMs += optionLevel.DurationMs;
                }
            }

            int clampedRoll = Math.Clamp(probabilityRollInclusivePercent, 0, 100);
            if (probabilisticChancePercent > 0 && clampedRoll <= probabilisticChancePercent)
            {
                resolvedHitPeriodMs = probabilisticDurationMs;
            }

            return resolvedHitPeriodMs + additionalDurationMs;
        }

        internal static IReadOnlyList<int> InferPacketOwnedTimeBombInvincibilityOptionIds(
            IEnumerable<string> potentialLines,
            IReadOnlyDictionary<int, PacketOwnedTimeBombInvincibilityOptionDefinition> definitions)
        {
            if (potentialLines == null || definitions == null || definitions.Count == 0)
            {
                return Array.Empty<int>();
            }

            Dictionary<string, int> normalizedLineLookup = BuildPacketOwnedTimeBombPotentialLineLookup(definitions);
            if (normalizedLineLookup.Count == 0)
            {
                return Array.Empty<int>();
            }

            var inferredIds = new List<int>();
            var seenIds = new HashSet<int>();
            foreach (string potentialLine in potentialLines)
            {
                string normalizedLine = NormalizePacketOwnedPotentialLineText(potentialLine);
                if (normalizedLine.Length == 0
                    || !normalizedLineLookup.TryGetValue(normalizedLine, out int optionId)
                    || !seenIds.Add(optionId))
                {
                    continue;
                }

                inferredIds.Add(optionId);
            }

            return inferredIds;
        }

        internal static string RenderPacketOwnedTimeBombInvincibilityOptionLine(
            string displayTemplate,
            PacketOwnedTimeBombInvincibilityOptionLevelData levelData)
        {
            if (string.IsNullOrWhiteSpace(displayTemplate))
            {
                return string.Empty;
            }

            return displayTemplate
                .Replace("#time", Math.Max(0, levelData.DurationMs / 1000).ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                .Replace("#prop", Math.Max(0, levelData.ProbabilityPercent).ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                .Trim();
        }

        private PacketOwnedTimeBombInvincibilityOptionLevelData? ResolvePacketOwnedTimeBombInvincibilityOptionLevel(int itemOptionId, int equipLevel)
        {
            PacketOwnedTimeBombInvincibilityOptionDefinition definition = ResolvePacketOwnedTimeBombInvincibilityOptionDefinition(itemOptionId);
            if (definition?.Levels == null || equipLevel <= 0 || equipLevel >= definition.Levels.Length)
            {
                return null;
            }

            PacketOwnedTimeBombInvincibilityOptionLevelData levelData = definition.Levels[equipLevel];
            return levelData.DurationMs > 0 || levelData.ProbabilityPercent > 0
                ? levelData
                : null;
        }

        private Dictionary<int, PacketOwnedTimeBombInvincibilityOptionDefinition> BuildPacketOwnedTimeBombInvincibilityOptionDefinitions()
        {
            var definitions = new Dictionary<int, PacketOwnedTimeBombInvincibilityOptionDefinition>();
            foreach (int itemOptionId in PacketOwnedTimeBombInvincibilityOptionIds.Value)
            {
                PacketOwnedTimeBombInvincibilityOptionDefinition definition = ResolvePacketOwnedTimeBombInvincibilityOptionDefinition(itemOptionId);
                if (definition != null)
                {
                    definitions[itemOptionId] = definition;
                }
            }

            return definitions;
        }

        private PacketOwnedTimeBombInvincibilityOptionDefinition ResolvePacketOwnedTimeBombInvincibilityOptionDefinition(int itemOptionId)
        {
            if (itemOptionId <= 0)
            {
                return null;
            }

            if (_packetOwnedTimeBombInvincibilityOptions.TryGetValue(itemOptionId, out PacketOwnedTimeBombInvincibilityOptionDefinition cached))
            {
                return cached;
            }

            WzImage itemOptionImage = Program.DataSource?.GetImage("Item", "ItemOption.img");
            if (itemOptionImage == null)
            {
                return null;
            }

            itemOptionImage.ParseImage();
            WzImageProperty optionRoot = itemOptionImage.GetFromPath($"{itemOptionId:D6}");
            if (optionRoot == null)
            {
                return null;
            }

            if (optionRoot["level"] is not WzSubProperty levelProperty)
            {
                return null;
            }

            string displayTemplate = optionRoot["info"]?["string"]?.GetString() ?? string.Empty;
            var levels = new PacketOwnedTimeBombInvincibilityOptionLevelData[21];
            foreach (WzSubProperty levelEntry in levelProperty.WzProperties.OfType<WzSubProperty>())
            {
                if (!int.TryParse(levelEntry.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level)
                    || level <= 0
                    || level >= levels.Length)
                {
                    continue;
                }

                int durationMs = Math.Max(0, (levelEntry["time"] as WzIntProperty)?.Value ?? 0) * 1000;
                int probabilityPercent = Math.Max(0, (levelEntry["prop"] as WzIntProperty)?.Value ?? 0);
                levels[level] = new PacketOwnedTimeBombInvincibilityOptionLevelData(durationMs, probabilityPercent);
            }

            var definition = new PacketOwnedTimeBombInvincibilityOptionDefinition
            {
                DisplayTemplate = displayTemplate,
                Levels = levels
            };
            _packetOwnedTimeBombInvincibilityOptions[itemOptionId] = definition;
            return definition;
        }

        internal static int[] CreatePacketOwnedTimeBombInvincibilityOptionIds()
        {
            WzImage itemOptionImage = Program.DataSource?.GetImage("Item", "ItemOption.img");
            if (itemOptionImage == null)
            {
                return PacketOwnedFallbackTimeBombInvincibilityOptionIds;
            }

            itemOptionImage.ParseImage();
            IReadOnlyList<int> discoveredIds = CollectPacketOwnedTimeBombInvincibilityOptionIds(
                itemOptionImage.WzProperties
                    .OfType<WzImageProperty>()
                    .Select(optionRoot =>
                    {
                        int optionId = int.TryParse(optionRoot.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedOptionId)
                            ? parsedOptionId
                            : 0;
                        int optionType = (optionRoot["info"]?["optionType"] as WzIntProperty)?.Value ?? 0;
                        string displayTemplate = optionRoot["info"]?["string"]?.GetString() ?? string.Empty;
                        return (OptionId: optionId, OptionType: optionType, DisplayTemplate: displayTemplate);
                    }));

            return discoveredIds.Count > 0
                ? discoveredIds.ToArray()
                : PacketOwnedFallbackTimeBombInvincibilityOptionIds;
        }

        internal static IReadOnlyList<int> CollectPacketOwnedTimeBombInvincibilityOptionIds(
            IEnumerable<(int OptionId, int OptionType, string DisplayTemplate)> candidates)
        {
            if (candidates == null)
            {
                return Array.Empty<int>();
            }

            return candidates
                .Where(candidate => candidate.OptionId > 0
                    && candidate.OptionType == PacketOwnedTimeBombInvincibilityOptionType
                    && IsPacketOwnedTimeBombInvincibilityDisplayTemplate(candidate.DisplayTemplate))
                .Select(candidate => candidate.OptionId)
                .Distinct()
                .OrderBy(optionId => optionId)
                .ToArray();
        }

        internal static bool IsPacketOwnedTimeBombInvincibilityDisplayTemplate(string displayTemplate)
        {
            if (string.IsNullOrWhiteSpace(displayTemplate))
            {
                return false;
            }

            return string.Equals(displayTemplate, PacketOwnedTimeBombInvincibilityDurationTemplate, StringComparison.Ordinal)
                || string.Equals(displayTemplate, PacketOwnedTimeBombInvincibilityChanceTemplate, StringComparison.Ordinal);
        }

        private static Dictionary<string, int> BuildPacketOwnedTimeBombPotentialLineLookup(
            IReadOnlyDictionary<int, PacketOwnedTimeBombInvincibilityOptionDefinition> definitions)
        {
            var lookup = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach ((int optionId, PacketOwnedTimeBombInvincibilityOptionDefinition definition) in definitions)
            {
                if (definition?.Levels == null)
                {
                    continue;
                }

                string templateKey = NormalizePacketOwnedPotentialLineText(definition.DisplayTemplate);
                if (templateKey.Length > 0)
                {
                    lookup.TryAdd(templateKey, optionId);
                }

                for (int level = 1; level < definition.Levels.Length; level++)
                {
                    string renderedLine = RenderPacketOwnedTimeBombInvincibilityOptionLine(definition.DisplayTemplate, definition.Levels[level]);
                    string renderedKey = NormalizePacketOwnedPotentialLineText(renderedLine);
                    if (renderedKey.Length > 0)
                    {
                        lookup.TryAdd(renderedKey, optionId);
                    }
                }
            }

            return lookup;
        }

        private static string NormalizePacketOwnedPotentialLineText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            StringBuilder builder = new(text.Length);
            bool previousWasSpace = false;
            foreach (char ch in text)
            {
                char normalized = char.ToLowerInvariant(ch);
                if (char.IsLetterOrDigit(normalized) || normalized == '%' || normalized == '#')
                {
                    builder.Append(normalized);
                    previousWasSpace = false;
                    continue;
                }

                if (!char.IsWhiteSpace(normalized) && normalized != '.' && normalized != ',' && normalized != ':' && normalized != ';')
                {
                    continue;
                }

                if (!previousWasSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }
            }

            return builder.ToString().Trim();
        }

        private static int NormalizePacketOwnedCooldownSkillId(int skillId, out bool isVehicleSentinel)
        {
            isVehicleSentinel = skillId == PacketOwnedBattleshipCooldownSentinel;
            return isVehicleSentinel ? PacketOwnedBattleshipSkillId : skillId;
        }

        private int ResolvePacketOwnedLocalSkillId(int skillId)
        {
            int[] candidateSkillIds = EnumeratePacketOwnedSkillIdCandidates(skillId)
                .Where(static candidateSkillId => candidateSkillId > 0)
                .Distinct()
                .ToArray();
            if (candidateSkillIds.Length == 0)
            {
                return skillId;
            }

            for (int i = 0; i < candidateSkillIds.Length; i++)
            {
                int candidateSkillId = candidateSkillIds[i];
                if (_playerManager?.Skills?.GetSkillLevel(candidateSkillId) > 0)
                {
                    return candidateSkillId;
                }
            }

            return candidateSkillIds[0];
        }

        internal static bool IsClientOwnedVengeancePacketSkillId(int skillId)
        {
            return skillId == PacketOwnedLegacyVengeanceSkillId;
        }

        internal static bool ShouldApplyPacketOwnedTimeBombImpactReaction(int impactPercent)
        {
            return impactPercent != 0;
        }

        internal static PacketOwnedTimeBombHpEffectKind ResolvePacketOwnedTimeBombHpEffectKind(int damage)
        {
            if (damage < 0)
            {
                return PacketOwnedTimeBombHpEffectKind.Heal;
            }

            return damage == 0
                ? PacketOwnedTimeBombHpEffectKind.Miss
                : PacketOwnedTimeBombHpEffectKind.None;
        }

        private static bool IsPacketOwnedVengeanceSkillId(int skillId)
        {
            return skillId > 0 && PacketOwnedVengeanceSkillIdCatalog.Value.Contains(skillId);
        }

        internal static HashSet<int> CreatePacketOwnedVengeanceSkillIdCatalog(
            IEnumerable<KeyValuePair<int, string>> skillNames)
        {
            return PacketOwnedSkillAliasCatalog.BuildVengeanceSkillIdCatalog(
                skillNames,
                PacketOwnedCurrentVengeanceSkillId,
                PacketOwnedLegacyVengeanceSkillId,
                PacketOwnedVengeanceSkillName);
        }

        private static HashSet<int> CreatePacketOwnedVengeanceSkillIdCatalog()
        {
            return CreatePacketOwnedVengeanceSkillIdCatalog(
                EnumeratePacketOwnedSkillNamesFromStringCatalog());
        }

        internal static HashSet<int> CreatePacketOwnedTimeBombSkillIdCatalog(
            IEnumerable<KeyValuePair<int, string>> skillNames,
            IEnumerable<KeyValuePair<int, string>> skillDescriptions)
        {
            return PacketOwnedSkillAliasCatalog.BuildSkillIdCatalog(
                skillNames: skillNames,
                skillDescriptions: skillDescriptions,
                preferredCurrentSkillId: PacketOwnedCurrentTimeBombSkillId,
                preferredLegacySkillId: 0,
                canonicalSkillName: PacketOwnedTimeBombSkillName,
                canonicalDescriptionFragment: PacketOwnedTimeBombSkillDescriptionMarker);
        }

        internal static HashSet<int> CreatePacketOwnedTimeBombSkillIdCatalog()
        {
            return CreatePacketOwnedTimeBombSkillIdCatalog(
                EnumeratePacketOwnedSkillNamesFromStringCatalog(),
                EnumeratePacketOwnedSkillDescriptionsFromStringCatalog());
        }

        internal static HashSet<int> CreatePacketOwnedExJablinSkillIdCatalog(
            IEnumerable<KeyValuePair<int, string>> skillNames,
            IEnumerable<KeyValuePair<int, string>> skillDescriptions)
        {
            return PacketOwnedSkillAliasCatalog.BuildSkillIdCatalog(
                skillNames: skillNames,
                skillDescriptions: skillDescriptions,
                preferredCurrentSkillId: PacketOwnedCurrentExJablinSkillId,
                preferredLegacySkillId: 0,
                canonicalDescriptionFragment: PacketOwnedExJablinSkillDescriptionMarker);
        }

        private static HashSet<int> CreatePacketOwnedExJablinSkillIdCatalog()
        {
            return CreatePacketOwnedExJablinSkillIdCatalog(
                EnumeratePacketOwnedSkillNamesFromStringCatalog(),
                EnumeratePacketOwnedSkillDescriptionsFromStringCatalog());
        }

        internal static IReadOnlyDictionary<int, int[]> CreatePacketOwnedSkillIdAliasCandidates(
            IEnumerable<int> timeBombSkillIds,
            IEnumerable<int> vengeanceSkillIds,
            IEnumerable<int> exJablinSkillIds)
        {
            int[] timeBombCandidates = PacketOwnedSkillAliasCatalog.BuildPreferredAliasCandidates(timeBombSkillIds, PacketOwnedCurrentTimeBombSkillId, 0);
            int[] vengeanceCandidates = PacketOwnedSkillAliasCatalog.BuildPreferredAliasCandidates(vengeanceSkillIds, PacketOwnedCurrentVengeanceSkillId, PacketOwnedLegacyVengeanceSkillId);
            int[] exJablinCandidates = PacketOwnedSkillAliasCatalog.BuildPreferredAliasCandidates(exJablinSkillIds, PacketOwnedCurrentExJablinSkillId, 0);
            var aliases = new Dictionary<int, int[]>(timeBombCandidates.Length + vengeanceCandidates.Length + exJablinCandidates.Length);
            AddFamilyAliases(timeBombCandidates);
            AddFamilyAliases(vengeanceCandidates);
            AddFamilyAliases(exJablinCandidates);

            return aliases;

            void AddFamilyAliases(int[] candidates)
            {
                for (int i = 0; i < candidates.Length; i++)
                {
                    aliases[candidates[i]] = candidates;
                }
            }
        }

        private static IReadOnlyDictionary<int, int[]> CreatePacketOwnedSkillIdAliasCandidates()
        {
            return CreatePacketOwnedSkillIdAliasCandidates(
                PacketOwnedTimeBombSkillIdCatalog.Value,
                PacketOwnedVengeanceSkillIdCatalog.Value,
                PacketOwnedExJablinSkillIdCatalog.Value);
        }

        private static IEnumerable<KeyValuePair<int, string>> EnumeratePacketOwnedSkillNamesFromStringCatalog()
        {
            return EnumeratePacketOwnedSkillStringsFromStringCatalog("name");
        }

        private static IEnumerable<KeyValuePair<int, string>> EnumeratePacketOwnedSkillDescriptionsFromStringCatalog()
        {
            return EnumeratePacketOwnedSkillStringsFromStringCatalog("desc", "pdesc");
        }

        private static IEnumerable<KeyValuePair<int, string>> EnumeratePacketOwnedSkillStringsFromStringCatalog(params string[] propertyNames)
        {
            WzImage skillStringImage = Program.FindImage("String", "Skill.img");
            if (skillStringImage == null || propertyNames == null || propertyNames.Length == 0)
            {
                yield break;
            }

            foreach (WzImageProperty property in skillStringImage.WzProperties)
            {
                if (!int.TryParse(property?.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int skillId) || skillId <= 0)
                {
                    continue;
                }

                if (property is not WzSubProperty skillEntry)
                {
                    continue;
                }

                for (int i = 0; i < propertyNames.Length; i++)
                {
                    string propertyName = propertyNames[i];
                    if (string.IsNullOrWhiteSpace(propertyName)
                        || skillEntry[propertyName] is not WzStringProperty stringProperty
                        || string.IsNullOrWhiteSpace(stringProperty.Value))
                    {
                        continue;
                    }

                    yield return new KeyValuePair<int, string>(skillId, stringProperty.Value);
                }
            }
        }

        private static IEnumerable<int> EnumeratePacketOwnedSkillIdCandidates(int skillId)
        {
            var yielded = new HashSet<int>();
            if (PacketOwnedSkillIdAliasCandidates.Value.TryGetValue(skillId, out int[] candidates))
            {
                for (int i = 0; i < candidates.Length; i++)
                {
                    int candidateSkillId = candidates[i];
                    if (candidateSkillId > 0 && yielded.Add(candidateSkillId))
                    {
                        yield return candidateSkillId;
                    }
                }
            }

            if (skillId > 0 && yielded.Add(skillId))
            {
                yield return skillId;
            }
        }

        private void ApplyPacketOwnedTimeBombHpEffect(int damage, int currentTime)
        {
            if (_combatEffects == null || _playerManager?.Player == null)
            {
                return;
            }

            float effectX = _playerManager.Player.X;
            float effectY = _playerManager.Player.Y - 50f;
            switch (ResolvePacketOwnedTimeBombHpEffectKind(damage))
            {
                case PacketOwnedTimeBombHpEffectKind.Heal:
                    _combatEffects.AddHealNumber(Math.Abs(damage), effectX, effectY, currentTime);
                    break;
                case PacketOwnedTimeBombHpEffectKind.Miss:
                    _combatEffects.AddMiss(effectX, effectY, currentTime, DamageColorType.Violet);
                    break;
            }
        }

        private void TryPlayPacketOwnedNoticeSound()
        {
            string noticeSoundDescriptor = PacketOwnedRewardResultRuntime.GetUtilDlgNoticeSoundDescriptor();
            if (!string.IsNullOrWhiteSpace(noticeSoundDescriptor))
            {
                TryPlayPacketOwnedWzSound(noticeSoundDescriptor, "UI.img", out _, out _);
            }
        }

        private bool TryApplyPacketOwnedOpenUiPayload(byte[] payload, bool requireExactClientPayload, out string message)
        {
            if (!TryDecodePacketOwnedOpenUiPayload(payload, requireExactClientPayload, out byte uiId, out message))
            {
                return false;
            }

            message = ApplyPacketOwnedOpenUi(uiId);
            return true;
        }

        internal static bool TryDecodePacketOwnedOpenUiPayload(
            byte[] payload,
            bool requireExactClientPayload,
            out byte uiId,
            out string message)
        {
            uiId = 0;
            message = "OpenUI payload must contain the raw UI id byte.";
            if (payload == null || payload.Length < 1)
            {
                return false;
            }

            uiId = payload[0];
            if (requireExactClientPayload && payload.Length != 1)
            {
                message = "OpenUI client payload must match CUserLocal::OnOpenUI: exactly one raw UI id byte.";
                return false;
            }

            message = "Decoded packet-owned OpenUI payload.";
            return true;
        }

        private bool TryApplyPacketOwnedNoticePayload(byte[] payload, out string message)
        {
            return TryApplyPacketOwnedStringPayload(payload, ApplyPacketOwnedNoticeMessage, "Notice payload is missing.", out message);
        }

        private bool TryApplyPacketOwnedTutorHirePayload(byte[] payload, out string message)
        {
            return TryApplyPacketOwnedTutorHirePayload(payload, requireExactClientPayload: false, out message);
        }

        private bool TryApplyPacketOwnedTutorHirePayload(byte[] payload, bool requireExactClientPayload, out string message)
        {
            if (!TryDecodePacketOwnedTutorHirePayload(payload, requireExactClientPayload, out bool enabled, out int targetCharacterId, out message))
            {
                return false;
            }

            message = ApplyPacketOwnedTutorHire(enabled, targetCharacterId);
            return true;
        }

        internal static bool TryDecodePacketOwnedTutorHirePayload(
            byte[] payload,
            bool requireExactClientPayload,
            out bool enabled,
            out int targetCharacterId,
            out string message)
        {
            enabled = false;
            targetCharacterId = 0;
            message = "Hire-tutor payload is missing.";
            if (payload == null || payload.Length == 0)
            {
                return false;
            }

            enabled = payload[0] != 0;
            if (requireExactClientPayload && payload.Length != 1)
            {
                message = "Hire-tutor client payload must match CUserLocal::OnHireTutor: exactly one enable byte.";
                return false;
            }

            if (!requireExactClientPayload)
            {
                if (payload.Length == 1)
                {
                    message = "Decoded packet-owned hire-tutor payload.";
                    return true;
                }

                if (payload.Length != 1 + sizeof(int))
                {
                    message = "Tutor pseudo-packet payload must contain one enable byte and an optional trailing Int32 owner character id.";
                    return false;
                }

                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                enabled = reader.ReadByte() != 0;
                targetCharacterId = Math.Max(0, reader.ReadInt32());
                message = targetCharacterId > 0
                    ? $"Decoded packet-owned hire-tutor payload for character {targetCharacterId}."
                    : "Decoded packet-owned hire-tutor payload.";
                return true;
            }

            message = "Decoded packet-owned hire-tutor payload.";
            return true;
        }

        private bool TryApplyPacketOwnedTutorMessagePayload(byte[] payload, out string message)
        {
            return TryApplyPacketOwnedTutorMessagePayload(payload, requireExactClientPayload: false, out message);
        }

        private bool TryApplyPacketOwnedTutorMessagePayload(byte[] payload, bool requireExactClientPayload, out string message)
        {
            if (!TryDecodePacketOwnedTutorMessagePayload(
                    payload,
                    requireExactClientPayload,
                    out bool indexedPayload,
                    out int messageIndex,
                    out int durationMs,
                    out string text,
                    out int width,
                    out int targetCharacterId,
                    out message))
            {
                return false;
            }

            message = indexedPayload
                ? ApplyPacketOwnedTutorIndexedMessage(messageIndex, durationMs, targetCharacterId)
                : ApplyPacketOwnedTutorTextMessage(text, width, durationMs, targetCharacterId);
            return true;
        }

        internal static bool TryDecodePacketOwnedTutorMessagePayload(
            byte[] payload,
            bool requireExactClientPayload,
            out bool indexedPayload,
            out int messageIndex,
            out int durationMs,
            out string text,
            out int width,
            out int targetCharacterId,
            out string message)
        {
            indexedPayload = false;
            messageIndex = 0;
            durationMs = 0;
            text = null;
            width = 0;
            targetCharacterId = 0;
            message = "Tutor message payload is missing.";
            if (payload == null || payload.Length < 1)
            {
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                indexedPayload = reader.ReadByte() != 0;
                if (indexedPayload)
                {
                    if (reader.BaseStream.Length - reader.BaseStream.Position < sizeof(int) * 2)
                    {
                        message = "Tutor indexed payload must contain two Int32 values.";
                        return false;
                    }

                    messageIndex = reader.ReadInt32();
                    durationMs = reader.ReadInt32();
                    if (requireExactClientPayload && reader.BaseStream.Position != reader.BaseStream.Length)
                    {
                        message = "Tutor indexed client payload contained trailing bytes after flag, message index, and duration.";
                        return false;
                    }

                    if (!requireExactClientPayload && reader.BaseStream.Position != reader.BaseStream.Length)
                    {
                        if (reader.BaseStream.Length - reader.BaseStream.Position != sizeof(int))
                        {
                            message = "Tutor indexed pseudo-packet payload may only append a trailing Int32 owner character id after the client fields.";
                            return false;
                        }

                        targetCharacterId = Math.Max(0, reader.ReadInt32());
                    }

                    message = targetCharacterId > 0
                        ? $"Decoded packet-owned tutor indexed payload for character {targetCharacterId}."
                        : "Decoded packet-owned tutor indexed payload.";
                    return true;
                }

                text = ReadPacketOwnedMapleString(reader);
                if (reader.BaseStream.Length - reader.BaseStream.Position < sizeof(int) * 2)
                {
                    message = "Tutor text payload must contain MapleString text followed by width and duration Int32 values.";
                    return false;
                }

                width = reader.ReadInt32();
                durationMs = reader.ReadInt32();
                if (requireExactClientPayload && reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    message = "Tutor text client payload contained trailing bytes after flag, text, width, and duration.";
                    return false;
                }

                if (!requireExactClientPayload && reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    if (reader.BaseStream.Length - reader.BaseStream.Position != sizeof(int))
                    {
                        message = "Tutor text pseudo-packet payload may only append a trailing Int32 owner character id after the client fields.";
                        return false;
                    }

                    targetCharacterId = Math.Max(0, reader.ReadInt32());
                }

                message = targetCharacterId > 0
                    ? $"Decoded packet-owned tutor text payload for character {targetCharacterId}."
                    : "Decoded packet-owned tutor text payload.";
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

        private bool TryApplyPacketOwnedOpenUiWithOptionPayload(byte[] payload, bool requireExactClientPayload, out string message)
        {
            if (!TryDecodePacketOwnedOpenUiWithOptionPayload(
                    payload,
                    requireExactClientPayload,
                    out int uiType,
                    out int option,
                    out message))
            {
                return false;
            }

            message = ApplyPacketOwnedOpenUiWithOption(uiType, option);
            return true;
        }

        internal static bool TryDecodePacketOwnedOpenUiWithOptionPayload(
            byte[] payload,
            bool requireExactClientPayload,
            out int uiType,
            out int option,
            out string message)
        {
            uiType = 0;
            option = 0;
            message = "OpenUIWithOption payload must contain uiType and option Int32 values.";
            if (payload == null || payload.Length < sizeof(int) * 2)
            {
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                uiType = reader.ReadInt32();
                option = reader.ReadInt32();
                if (requireExactClientPayload && reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    message = "OpenUIWithOption client payload contained trailing bytes after uiType and option Int32 values.";
                    return false;
                }

                message = "Decoded packet-owned OpenUIWithOption payload.";
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                message = $"OpenUIWithOption payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private bool TryApplyPacketOwnedCommodityPayload(byte[] payload, bool requireExactClientPayload, out string message)
        {
            if (!TryDecodePacketOwnedCommodityPayload(
                    payload,
                    requireExactClientPayload,
                    out int commoditySerialNumber,
                    out message))
            {
                return false;
            }

            message = ApplyPacketOwnedGoToCommoditySn(commoditySerialNumber);
            return true;
        }

        internal static bool TryDecodePacketOwnedCommodityPayload(
            byte[] payload,
            bool requireExactClientPayload,
            out int commoditySerialNumber,
            out string message)
        {
            commoditySerialNumber = 0;
            message = "Commodity payload must contain the commodity serial number Int32 value.";
            if (payload == null || payload.Length < sizeof(int))
            {
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                commoditySerialNumber = reader.ReadInt32();
                if (requireExactClientPayload && reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    message = "Commodity client payload contained trailing bytes after the commodity serial number Int32 value.";
                    return false;
                }

                message = "Decoded packet-owned commodity payload.";
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                message = $"Commodity payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private bool TryApplyPacketOwnedSkillCooltimePayload(byte[] payload, out string message)
        {
            if (!TryDecodePacketOwnedSkillCooltimePayload(payload, out int skillId, out int remainSeconds, out message))
            {
                return false;
            }

            message = ApplyPacketOwnedSkillCooltime(skillId, remainSeconds);
            return true;
        }

        internal static bool TryDecodePacketOwnedSkillCooltimePayload(
            byte[] payload,
            out int skillId,
            out int remainSeconds,
            out string message)
        {
            skillId = 0;
            remainSeconds = 0;
            message = "Skill-cooltime payload must contain skillId Int32 and remainSec UInt16 values.";
            if (payload == null || payload.Length < sizeof(int) + sizeof(ushort))
            {
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                skillId = reader.ReadInt32();
                remainSeconds = reader.ReadUInt16();
                if (reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    message = "Skill-cooltime payload contained trailing bytes after skillId Int32 and remainSec UInt16 values.";
                    return false;
                }

                message = "Decoded packet-owned skill-cooltime payload.";
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                message = $"Skill-cooltime payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        internal static bool TryDecodePacketOwnedTimeBombAttackPayload(
            byte[] payload,
            out PacketOwnedTimeBombAttackPayload decodedPayload,
            out string errorMessage)
        {
            decodedPayload = default;
            errorMessage = null;
            if (payload == null || payload.Length != (sizeof(int) * 5))
            {
                errorMessage = "Time Bomb payload must contain exactly skillId, timeBombX, timeBombY, impactPercent, and damage Int32 values.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                decodedPayload = new PacketOwnedTimeBombAttackPayload(
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32());
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Time Bomb payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        internal static bool TryDecodePacketOwnedVengeanceSkillApplyPayload(
            byte[] payload,
            out int skillId,
            out string errorMessage)
        {
            skillId = 0;
            errorMessage = null;
            if (payload == null || payload.Length != sizeof(int))
            {
                errorMessage = "Vengeance payload must contain exactly the applied skill id Int32 value.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                skillId = reader.ReadInt32();
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Vengeance payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        internal static bool TryDecodePacketOwnedExJablinApplyPayload(
            byte[] payload,
            out string errorMessage)
        {
            errorMessage = null;
            if (payload != null && payload.Length > 0)
            {
                errorMessage = "ExJablin payload should be empty because the client only arms the next-shot flag.";
                return false;
            }

            return true;
        }

        private bool TryApplyPacketOwnedTimeBombAttackPayload(byte[] payload, out string message)
        {
            message = null;
            if (!TryDecodePacketOwnedTimeBombAttackPayload(payload, out PacketOwnedTimeBombAttackPayload decodedPayload, out string errorMessage))
            {
                message = errorMessage;
                return false;
            }

            message = ApplyPacketOwnedTimeBombAttack(
                decodedPayload.SkillId,
                decodedPayload.TimeBombX,
                decodedPayload.TimeBombY,
                decodedPayload.ImpactPercent,
                decodedPayload.Damage);
            return true;
        }

        private bool TryApplyPacketOwnedVengeanceSkillApplyPayload(byte[] payload, out string message)
        {
            message = null;
            if (!TryDecodePacketOwnedVengeanceSkillApplyPayload(payload, out int skillId, out string errorMessage))
            {
                message = errorMessage;
                return false;
            }

            message = ApplyPacketOwnedVengeanceSkillApply(skillId);
            return true;
        }

        private bool TryApplyPacketOwnedExJablinApplyPayload(byte[] payload, out string message)
        {
            if (!TryDecodePacketOwnedExJablinApplyPayload(payload, out string errorMessage))
            {
                message = errorMessage;
                return false;
            }

            message = ApplyPacketOwnedExJablinApply();
            return true;
        }

        private bool TryApplyPacketOwnedRepeatSkillModeEndAckPayload(byte[] payload, out string message)
        {
            message = null;
            if (!PacketOwnedMechanicRepeatSkillRuntime.TryDecodeRepeatSkillModeEndAck(
                    payload,
                    out PacketOwnedRepeatSkillModeEndAck ack,
                    out string decodeError))
            {
                message = decodeError ?? "Repeat-skill mode-end ack payload could not be decoded.";
                return false;
            }

            if (_playerManager?.Player == null)
            {
                message = "Repeat-skill mode-end ack arrived before the local player initialized.";
                return false;
            }

            StampPacketOwnedUtilityRequestState();
            int currentTick = Environment.TickCount;
            if (!_playerManager.TryResolvePacketOwnedRepeatSkillModeEndRequest(
                    ack.SkillId,
                    ack.ReturnSkillId,
                    ack.RequestedAt,
                    currentTick))
            {
                message =
                    $"Repeat-skill mode-end ack did not match the active pending request for {ack.SkillId}->{ack.ReturnSkillId} at tick {ack.RequestedAt}.";
                return false;
            }

            message =
                $"Applied packet-owned repeat-skill mode-end ack for {ack.SkillId}->{ack.ReturnSkillId} at tick {ack.RequestedAt}.";
            return true;
        }

        private bool TryApplyPacketOwnedSg88ManualAttackConfirmPayload(byte[] payload, out string message)
        {
            message = null;
            if (!PacketOwnedMechanicRepeatSkillRuntime.TryDecodeSg88ManualAttackConfirm(
                    payload,
                    out PacketOwnedSg88ManualAttackConfirm confirm,
                    out string decodeError))
            {
                message = decodeError ?? "SG-88 manual-attack confirm payload could not be decoded.";
                return false;
            }

            if (_playerManager?.Player == null)
            {
                message = "SG-88 manual-attack confirm arrived before the local player initialized.";
                return false;
            }

            StampPacketOwnedUtilityRequestState();
            int currentTick = Environment.TickCount;
            if (!_playerManager.TryResolvePacketOwnedSg88ManualAttackRequest(
                    confirm.SummonObjectId,
                    confirm.RequestedAt,
                    currentTick))
            {
                message =
                    $"SG-88 manual-attack confirm did not match the active pending summon request for object {confirm.SummonObjectId} at tick {confirm.RequestedAt}.";
                return false;
            }

            _summonedOfficialSessionBridge.ResolveSg88ManualAttackRequest(
                confirm.SummonObjectId,
                confirm.RequestedAt,
                "1021-confirm");

            message =
                $"Applied packet-owned SG-88 manual-attack confirm for summon {confirm.SummonObjectId} at tick {confirm.RequestedAt}.";
            return true;
        }

        private bool TryApplyPacketOwnedMechanicEquipPayload(byte[] payload, out string message)
        {
            message = null;
            if (!MechanicEquipmentPacketParity.TryDecodePayload(payload, out MechanicEquipPacketPayload decodedPayload, out string errorMessage))
            {
                message = errorMessage;
                return false;
            }

            if (decodedPayload.Mode == MechanicEquipPacketPayloadMode.AuthorityRequest)
            {
                return TryResolvePacketOwnedMechanicAuthorityRequest(decodedPayload, out message);
            }

            if (decodedPayload.Mode == MechanicEquipPacketPayloadMode.AuthorityResult)
            {
                return TryQueuePacketOwnedMechanicAuthorityResult(decodedPayload, out message);
            }

            CharacterBuild build = _playerManager?.Player?.Build;
            MechanicEquipmentController controller = _playerManager?.CompanionEquipment?.Mechanic;
            if (build == null || controller == null)
            {
                message = "Mechanic equipment runtime is unavailable.";
                return false;
            }

            bool applied = decodedPayload.Mode switch
            {
                MechanicEquipPacketPayloadMode.Snapshot => controller.TryApplyExternalSnapshot(
                    build,
                    decodedPayload.SnapshotItems,
                    out errorMessage),
                MechanicEquipPacketPayloadMode.SlotMutation when decodedPayload.Slot.HasValue => controller.TryApplyExternalSlotMutation(
                    build,
                    decodedPayload.Slot.Value,
                    decodedPayload.ItemId,
                    out errorMessage),
                MechanicEquipPacketPayloadMode.ClearAll => controller.TryApplyExternalSnapshot(
                    build,
                    null,
                    out errorMessage),
                MechanicEquipPacketPayloadMode.ResetDefaults => ApplyPacketOwnedMechanicEquipDefaults(controller, build, out errorMessage),
                _ => false
            };
            if (!applied)
            {
                message = string.IsNullOrWhiteSpace(errorMessage)
                    ? "Packet-authored mechanic equipment payload could not be applied."
                    : errorMessage;
                return false;
            }

            StampPacketOwnedUtilityRequestState();
            message = decodedPayload.Mode switch
            {
                MechanicEquipPacketPayloadMode.Snapshot => "Applied packet-authored mechanic equipment snapshot.",
                MechanicEquipPacketPayloadMode.SlotMutation => decodedPayload.ItemId > 0
                    ? $"Applied packet-authored mechanic slot update for {decodedPayload.Slot} with item {decodedPayload.ItemId}."
                    : $"Applied packet-authored mechanic slot clear for {decodedPayload.Slot}.",
                MechanicEquipPacketPayloadMode.ClearAll => "Cleared packet-authored mechanic equipment state.",
                MechanicEquipPacketPayloadMode.ResetDefaults => "Reset mechanic equipment to the client default machine-part set.",
                _ => "Applied packet-authored mechanic equipment state."
            };
            return true;
        }

        private bool TryResolvePacketOwnedTutorDisplayState(
            out TutorVariantSnapshot displayVariant,
            out PacketOwnedTutorDisplayOwner displayOwner)
        {
            displayOwner = default;
            if (!TryResolvePacketOwnedTutorDisplayVariant(out displayVariant))
            {
                return false;
            }

            return TryResolvePacketOwnedTutorDisplayOwner(displayVariant, out displayOwner);
        }

        private bool TryResolvePacketOwnedTutorDisplayVariant(out TutorVariantSnapshot displayVariant)
        {
            if (_packetOwnedTutorRuntime.IsActive && _packetOwnedTutorRuntime.ActiveSkillId > 0)
            {
                displayVariant = new TutorVariantSnapshot(
                    _packetOwnedTutorRuntime.ActiveSkillId,
                    _packetOwnedTutorRuntime.ActiveSummonObjectId,
                    _packetOwnedTutorRuntime.ResolveActorHeight(),
                    _packetOwnedTutorRuntime.BoundCharacterId,
                    true,
                    _packetOwnedTutorRuntime.LastHireTick,
                    int.MinValue,
                    _packetOwnedTutorRuntime.LastRegistryMutationTick);
                return true;
            }

            return _packetOwnedTutorRuntime.TryGetSharedActiveVariant(out displayVariant);
        }

        private bool TryResolvePacketOwnedTutorDisplayOwner(
            TutorVariantSnapshot displayVariant,
            out PacketOwnedTutorDisplayOwner owner)
        {
            owner = default;
            int ownerCharacterId = Math.Max(0, displayVariant.BoundCharacterId);
            if (ownerCharacterId <= 0)
            {
                return false;
            }

            PlayerCharacter localPlayer = _playerManager?.Player;
            if (localPlayer?.Build?.Id == ownerCharacterId)
            {
                owner = new PacketOwnedTutorDisplayOwner(
                    ownerCharacterId,
                    localPlayer.Position,
                    localPlayer.FacingRight,
                    Math.Max(1, localPlayer.Level),
                    IsLocalOwner: true);
                return true;
            }

            if (_remoteUserPool != null && _remoteUserPool.TryGetActor(ownerCharacterId, out RemoteUserActor actor))
            {
                owner = new PacketOwnedTutorDisplayOwner(
                    ownerCharacterId,
                    actor.Position,
                    actor.FacingRight,
                    Math.Max(1, actor.Build?.Level ?? 1),
                    IsLocalOwner: false);
                return true;
            }

            return false;
        }

        private int ResolvePacketOwnedTutorDisplaySkillLevel(
            TutorVariantSnapshot displayVariant,
            PacketOwnedTutorDisplayOwner owner)
        {
            if (owner.IsLocalOwner)
            {
                return Math.Max(1, _playerManager?.Skills?.GetSkillLevel(displayVariant.SkillId) ?? 0);
            }

            return 1;
        }

        private static bool ApplyPacketOwnedMechanicEquipDefaults(
            MechanicEquipmentController controller,
            CharacterBuild build,
            out string errorMessage)
        {
            errorMessage = null;
            if (controller == null)
            {
                errorMessage = "Mechanic equipment runtime is unavailable.";
                return false;
            }

            if (!CompanionEquipmentController.HasMechanicOwnerState(build))
            {
                errorMessage = "Mechanic equipment is only available to Mechanic job paths.";
                return false;
            }

            controller.ResetToDefaults(build);
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
                if (targetCount < 0)
                {
                    message = "Quest-guide payload declared a negative target count.";
                    return false;
                }

                List<(int PrimaryId, List<int> ChildIds)> records = new(targetCount);
                for (int i = 0; i < targetCount; i++)
                {
                    int primaryId = reader.ReadInt32();
                    int childCount = reader.ReadUInt16();
                    List<int> childIds = new(Math.Max(0, childCount));
                    for (int childIndex = 0; childIndex < childCount; childIndex++)
                    {
                        childIds.Add(reader.ReadInt32());
                    }

                    records.Add((primaryId, childIds));
                }

                bool looksLikeLegacyMobGuide = records.Count > 0
                    && records.All(record => record.ChildIds.All(childId => childId <= 0 || childId >= 100000000));
                if (looksLikeLegacyMobGuide)
                {
                    Dictionary<int, IReadOnlyList<int>> targetsByMobId = new();
                    for (int i = 0; i < records.Count; i++)
                    {
                        int mobId = records[i].PrimaryId;
                        List<int> mapIds = records[i].ChildIds
                            .Where(mapId => mapId > 0)
                            .Distinct()
                            .ToList();
                        if (mobId > 0 && mapIds.Count > 0)
                        {
                            targetsByMobId[mobId] = mapIds;
                        }
                    }

                    message = ApplyPacketQuestGuideLaunch(questId, targetsByMobId);
                    return true;
                }

                QuestDemandItemQueryState runtimeFallbackQuery = null;
                _questRuntime.TryBuildQuestDemandItemQuery(questId, out runtimeFallbackQuery);

                List<int> visibleItemIds = new();
                Dictionary<int, IReadOnlyList<int>> visibleItemMapIds = new();
                int hiddenItemCount = 0;
                int currentMapId = _mapBoard?.MapInfo?.id ?? 0;
                bool hasPacketOwnedMapResults = false;
                for (int i = 0; i < records.Count; i++)
                {
                    int itemId = records[i].PrimaryId;
                    if (itemId <= 0)
                    {
                        hiddenItemCount++;
                        continue;
                    }

                    HashSet<int> mapIds = new();
                    for (int childIndex = 0; childIndex < records[i].ChildIds.Count; childIndex++)
                    {
                        int mobId = records[i].ChildIds[childIndex];
                        if (mobId <= 0)
                        {
                            continue;
                        }

                        if (TryGetPacketQuestGuideMapIds(mobId, out IReadOnlyList<int> packetMapIds) && packetMapIds.Count > 0)
                        {
                            for (int mapIndex = 0; mapIndex < packetMapIds.Count; mapIndex++)
                            {
                                if (packetMapIds[mapIndex] > 0)
                                {
                                    mapIds.Add(packetMapIds[mapIndex]);
                                    hasPacketOwnedMapResults = true;
                                }
                            }
                        }
                    }

                    if (!visibleItemIds.Contains(itemId))
                    {
                        visibleItemIds.Add(itemId);
                    }

                    IReadOnlyList<int> runtimeFallbackMapIds = ResolveRuntimeFallbackDemandItemMapIds(runtimeFallbackQuery, itemId);
                    for (int mapIndex = 0; mapIndex < runtimeFallbackMapIds.Count; mapIndex++)
                    {
                        if (runtimeFallbackMapIds[mapIndex] > 0)
                        {
                            mapIds.Add(runtimeFallbackMapIds[mapIndex]);
                        }
                    }

                    if (mapIds.Count == 0 && currentMapId > 0)
                    {
                        mapIds.Add(currentMapId);
                    }

                    if (mapIds.Count > 0)
                    {
                        visibleItemMapIds[itemId] = mapIds.OrderBy(mapId => mapId).ToArray();
                    }
                }

                string fallbackNpcName = runtimeFallbackQuery?.FallbackNpcName ?? string.Empty;
                if (visibleItemIds.Count == 0 && runtimeFallbackQuery?.VisibleItemIds != null)
                {
                    for (int i = 0; i < runtimeFallbackQuery.VisibleItemIds.Count; i++)
                    {
                        int itemId = runtimeFallbackQuery.VisibleItemIds[i];
                        if (itemId <= 0 || visibleItemIds.Contains(itemId))
                        {
                            continue;
                        }

                        visibleItemIds.Add(itemId);
                        IReadOnlyList<int> runtimeFallbackMapIds = ResolveRuntimeFallbackDemandItemMapIds(runtimeFallbackQuery, itemId);
                        if (runtimeFallbackMapIds.Count > 0)
                        {
                            visibleItemMapIds[itemId] = runtimeFallbackMapIds
                                .Where(mapId => mapId > 0)
                                .Distinct()
                                .OrderBy(mapId => mapId)
                                .ToArray();
                        }
                    }
                }

                if (hiddenItemCount == 0 && runtimeFallbackQuery?.HiddenItemCount > 0)
                {
                    hiddenItemCount = runtimeFallbackQuery.HiddenItemCount;
                }

                message = ApplyQuestDemandItemQueryLaunch(new QuestDemandItemQueryState
                {
                    QuestId = questId,
                    VisibleItemIds = visibleItemIds,
                    VisibleItemMapIds = visibleItemMapIds,
                    HiddenItemCount = hiddenItemCount,
                    FallbackNpcName = fallbackNpcName,
                    HasPacketOwnedMapResults = hasPacketOwnedMapResults
                });
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
                List<int> disallowedQuestIds = DecodePacketOwnedDeliveryQuestIdsWithOptionalCount(reader);
                QuestDetailDeliveryType packetOwnedDeliveryType = DecodePacketOwnedDeliveryType(reader);
                message = ApplyDeliveryQuestLaunch(questId, itemId, disallowedQuestIds, packetOwnedDeliveryType);
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

        private static QuestDetailDeliveryType DecodePacketOwnedDeliveryType(BinaryReader reader)
        {
            if (reader == null)
            {
                return QuestDetailDeliveryType.None;
            }

            int remaining = (int)(reader.BaseStream.Length - reader.BaseStream.Position);
            if (remaining <= 0)
            {
                return QuestDetailDeliveryType.None;
            }

            int rawType = remaining switch
            {
                >= 4 => reader.ReadInt32(),
                2 => reader.ReadUInt16(),
                _ => reader.ReadByte()
            };

            return QuestDetailDeliveryTypeCodec.FromClientRawValue(rawType);
        }

        private static bool TryParseQuestDeliveryTypeToken(string token, out QuestDetailDeliveryType deliveryType)
        {
            return QuestDetailDeliveryTypeCodec.TryParseToken(token, out deliveryType);
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

        private bool TryApplyPacketOwnedEventAlarmTextPayload(byte[] payload, out string message)
        {
            if (!TryDecodePacketOwnedEventAlarmTextPayload(
                    payload,
                    out bool clearRequested,
                    out EventAlarmLineSnapshot[] lines,
                    out string summary,
                    out string decodeMessage))
            {
                message = decodeMessage ?? "Event-alarm text payload could not be decoded.";
                return false;
            }

            StampPacketOwnedUtilityRequestState();
            _packetOwnedEventAlarmLines.Clear();
            if (!clearRequested)
            {
                _packetOwnedEventAlarmLines.AddRange(lines ?? Array.Empty<EventAlarmLineSnapshot>());
            }

            _lastPacketOwnedEventAlarmTick = Environment.TickCount;
            _lastPacketOwnedEventAlarmSummary = string.IsNullOrWhiteSpace(summary)
                ? clearRequested
                    ? "Packet-authored event-alarm CT lines cleared."
                    : $"Packet-authored event-alarm feed now carries {_packetOwnedEventAlarmLines.Count.ToString(CultureInfo.InvariantCulture)} CT line(s)."
                : summary;
            NotifyEventAlarmOwnerActivity("packet-owned event alarm");
            message = _lastPacketOwnedEventAlarmSummary;
            return true;
        }

        private bool TryApplyPacketOwnedEventCalendarEntriesPayload(byte[] payload, out string message)
        {
            if (!TryDecodePacketOwnedEventCalendarEntriesPayload(
                    payload,
                    out bool clearRequested,
                    out bool replaceExistingEntries,
                    out EventEntrySnapshot[] entries,
                    out string summary,
                    out string decodeMessage))
            {
                message = decodeMessage ?? "Event-calendar payload could not be decoded.";
                return false;
            }

            StampPacketOwnedUtilityRequestState();
            if (clearRequested || replaceExistingEntries)
            {
                _packetOwnedEventCalendarEntries.Clear();
            }

            int now = Environment.TickCount;
            int sortOrder = _packetOwnedEventCalendarEntries.Count;
            foreach (EventEntrySnapshot entry in entries ?? Array.Empty<EventEntrySnapshot>())
            {
                _packetOwnedEventCalendarEntries.Add(new EventEntrySnapshot
                {
                    Title = entry.Title,
                    Detail = entry.Detail,
                    StatusText = entry.StatusText,
                    Status = entry.Status,
                    ScheduledAt = entry.ScheduledAt.Date,
                    SourceTick = entry.SourceTick == int.MinValue ? now : entry.SourceTick,
                    SortPriority = entry.SortPriority,
                    SortOrder = sortOrder++
                });
            }

            _lastPacketOwnedEventCalendarTick = now;
            _lastPacketOwnedEventCalendarSummary = string.IsNullOrWhiteSpace(summary)
                ? clearRequested
                    ? "Packet-authored event-calendar entries cleared."
                    : $"Packet-authored event-calendar feed now carries {_packetOwnedEventCalendarEntries.Count.ToString(CultureInfo.InvariantCulture)} owner row(s)."
                : summary;
            NotifyEventAlarmOwnerActivity("packet-owned event calendar");
            message = _lastPacketOwnedEventCalendarSummary;
            return true;
        }

        private static bool TryDecodePacketOwnedEventAlarmTextPayload(
            byte[] payload,
            out bool clearRequested,
            out EventAlarmLineSnapshot[] lines,
            out string summary,
            out string message)
        {
            clearRequested = false;
            lines = Array.Empty<EventAlarmLineSnapshot>();
            summary = string.Empty;
            message = "Event-alarm text payload is missing.";
            if (payload == null || payload.Length == 0)
            {
                return false;
            }

            if (!TryDecodePacketOwnedStringPayload(payload, out string decodedText))
            {
                message = "Event-alarm text payload must decode to MapleString, UTF-8 text, or a JSON text body.";
                return false;
            }

            if (TryDecodePacketOwnedEventAlarmTextJsonPayload(decodedText, out clearRequested, out lines, out summary, out message))
            {
                return clearRequested || lines.Length > 0;
            }

            string normalizedText = decodedText.Trim();
            if (string.Equals(normalizedText, "clear", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedText, "reset", StringComparison.OrdinalIgnoreCase))
            {
                clearRequested = true;
                summary = "Cleared packet-authored event-alarm CT lines.";
                message = summary;
                return true;
            }

            string[] textLines = normalizedText.Split(
                new[] { "\r\n", "\n" },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (textLines.Length == 0)
            {
                message = "Event-alarm text payload did not contain any usable line text.";
                return false;
            }

            lines = textLines
                .Select((line, index) => CreateEventAlarmLine(line, index))
                .ToArray();
            summary = $"Applied packet-authored event-alarm text with {lines.Length.ToString(CultureInfo.InvariantCulture)} CT line(s).";
            message = summary;
            return true;
        }

        internal static bool TryDecodePacketOwnedEventAlarmTextPayloadForTests(
            byte[] payload,
            out bool clearRequested,
            out EventAlarmLineSnapshot[] lines,
            out string summary,
            out string message)
        {
            return TryDecodePacketOwnedEventAlarmTextPayload(payload, out clearRequested, out lines, out summary, out message);
        }

        private static bool TryDecodePacketOwnedEventAlarmTextJsonPayload(
            string payloadText,
            out bool clearRequested,
            out EventAlarmLineSnapshot[] lines,
            out string summary,
            out string message)
        {
            clearRequested = false;
            lines = Array.Empty<EventAlarmLineSnapshot>();
            summary = string.Empty;
            message = "Event-alarm JSON payload did not contain any usable CT line data.";
            if (string.IsNullOrWhiteSpace(payloadText))
            {
                return false;
            }

            string trimmed = payloadText.Trim();
            if (!trimmed.StartsWith("{", StringComparison.Ordinal)
                && !trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(trimmed);
                JsonElement root = document.RootElement;
                List<EventAlarmLineSnapshot> parsedLines = new();
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (TryGetJsonBoolean(root, "clear", out bool parsedClear))
                    {
                        clearRequested = parsedClear;
                    }

                    if (TryGetJsonString(root, "summary", out string parsedSummary))
                    {
                        summary = parsedSummary;
                    }

                    if (root.TryGetProperty("lines", out JsonElement lineArray))
                    {
                        AppendPacketOwnedEventAlarmJsonLines(lineArray, parsedLines);
                    }
                }
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    AppendPacketOwnedEventAlarmJsonLines(root, parsedLines);
                }

                lines = parsedLines.ToArray();
                if (string.IsNullOrWhiteSpace(summary))
                {
                    summary = clearRequested
                        ? "Cleared packet-authored event-alarm CT lines."
                        : lines.Length > 0
                            ? $"Applied packet-authored event-alarm JSON payload with {lines.Length.ToString(CultureInfo.InvariantCulture)} CT line(s)."
                            : string.Empty;
                }

                message = summary;
                return clearRequested || lines.Length > 0;
            }
            catch (JsonException ex)
            {
                message = $"Event-alarm JSON payload could not be parsed: {ex.Message}";
                return false;
            }
        }

        private static void AppendPacketOwnedEventAlarmJsonLines(JsonElement element, ICollection<EventAlarmLineSnapshot> destination)
        {
            if (destination == null || element.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            int index = destination.Count;
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    string text = item.GetString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        destination.Add(CreateEventAlarmLine(text, index++));
                    }

                    continue;
                }

                if (item.ValueKind != JsonValueKind.Object
                    || !TryGetJsonString(item, "text", out string lineText)
                    || string.IsNullOrWhiteSpace(lineText))
                {
                    continue;
                }

                destination.Add(new EventAlarmLineSnapshot
                {
                    Text = lineText.Trim(),
                    Left = TryGetJsonInt32(item, "left", out int left) ? Math.Max(0, left) : 0,
                    Top = TryGetJsonInt32(item, "top", out int top) ? Math.Max(0, top) : index * 13,
                    IsHighlighted = TryGetJsonBoolean(item, "highlight", out bool highlight) && highlight
                });
                index++;
            }
        }

        private static bool TryDecodePacketOwnedEventCalendarEntriesPayload(
            byte[] payload,
            out bool clearRequested,
            out bool replaceExistingEntries,
            out EventEntrySnapshot[] entries,
            out string summary,
            out string message)
        {
            clearRequested = false;
            replaceExistingEntries = true;
            entries = Array.Empty<EventEntrySnapshot>();
            summary = string.Empty;
            message = "Event-calendar payload is missing.";
            if (payload == null || payload.Length == 0)
            {
                return false;
            }

            if (!TryDecodePacketOwnedStringPayload(payload, out string decodedText))
            {
                message = "Event-calendar payload must decode to MapleString, UTF-8 text, or a JSON text body.";
                return false;
            }

            if (TryDecodePacketOwnedEventCalendarJsonPayload(decodedText, out clearRequested, out replaceExistingEntries, out entries, out summary, out message))
            {
                return clearRequested || entries.Length > 0;
            }

            string normalizedText = decodedText.Trim();
            if (string.Equals(normalizedText, "clear", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedText, "reset", StringComparison.OrdinalIgnoreCase))
            {
                clearRequested = true;
                summary = "Cleared packet-authored event-calendar entries.";
                message = summary;
                return true;
            }

            List<EventEntrySnapshot> parsedEntries = new();
            string[] entryLines = normalizedText.Split(
                new[] { "\r\n", "\n" },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (int i = 0; i < entryLines.Length; i++)
            {
                if (TryParsePacketOwnedEventCalendarTextEntry(entryLines[i], i, out EventEntrySnapshot parsedEntry))
                {
                    parsedEntries.Add(parsedEntry);
                }
            }

            if (parsedEntries.Count == 0)
            {
                message = "Event-calendar payload did not contain any usable event rows.";
                return false;
            }

            entries = parsedEntries.ToArray();
            summary = $"Applied packet-authored event-calendar payload with {entries.Length.ToString(CultureInfo.InvariantCulture)} owner row(s).";
            message = summary;
            return true;
        }

        internal static bool TryDecodePacketOwnedEventCalendarEntriesPayloadForTests(
            byte[] payload,
            out bool clearRequested,
            out bool replaceExistingEntries,
            out EventEntrySnapshot[] entries,
            out string summary,
            out string message)
        {
            return TryDecodePacketOwnedEventCalendarEntriesPayload(payload, out clearRequested, out replaceExistingEntries, out entries, out summary, out message);
        }

        private static bool TryDecodePacketOwnedEventCalendarJsonPayload(
            string payloadText,
            out bool clearRequested,
            out bool replaceExistingEntries,
            out EventEntrySnapshot[] entries,
            out string summary,
            out string message)
        {
            clearRequested = false;
            replaceExistingEntries = true;
            entries = Array.Empty<EventEntrySnapshot>();
            summary = string.Empty;
            message = "Event-calendar JSON payload did not contain any usable owner rows.";
            if (string.IsNullOrWhiteSpace(payloadText))
            {
                return false;
            }

            string trimmed = payloadText.Trim();
            if (!trimmed.StartsWith("{", StringComparison.Ordinal)
                && !trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(trimmed);
                JsonElement root = document.RootElement;
                List<EventEntrySnapshot> parsedEntries = new();
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (TryGetJsonBoolean(root, "clear", out bool parsedClear))
                    {
                        clearRequested = parsedClear;
                    }

                    if (TryGetJsonBoolean(root, "replace", out bool parsedReplace))
                    {
                        replaceExistingEntries = parsedReplace;
                    }

                    if (TryGetJsonString(root, "summary", out string parsedSummary))
                    {
                        summary = parsedSummary;
                    }

                    if (root.TryGetProperty("entries", out JsonElement entryArray))
                    {
                        AppendPacketOwnedEventCalendarJsonEntries(entryArray, parsedEntries);
                    }
                }
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    AppendPacketOwnedEventCalendarJsonEntries(root, parsedEntries);
                }

                entries = parsedEntries.ToArray();
                if (string.IsNullOrWhiteSpace(summary))
                {
                    summary = clearRequested
                        ? "Cleared packet-authored event-calendar entries."
                        : entries.Length > 0
                            ? $"Applied packet-authored event-calendar JSON payload with {entries.Length.ToString(CultureInfo.InvariantCulture)} owner row(s)."
                            : string.Empty;
                }

                message = summary;
                return clearRequested || entries.Length > 0;
            }
            catch (JsonException ex)
            {
                message = $"Event-calendar JSON payload could not be parsed: {ex.Message}";
                return false;
            }
        }

        private static void AppendPacketOwnedEventCalendarJsonEntries(JsonElement element, ICollection<EventEntrySnapshot> destination)
        {
            if (destination == null || element.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            int index = destination.Count;
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    string stringTitle = item.GetString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(stringTitle))
                    {
                        destination.Add(CreatePacketOwnedEventCalendarEntry(DateTime.Today, stringTitle, string.Empty, EventEntryStatus.Upcoming, "Will", int.MinValue, index++));
                    }

                    continue;
                }

                if (item.ValueKind != JsonValueKind.Object
                    || !TryGetJsonString(item, "title", out string title)
                    || string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                DateTime scheduledAt = TryGetJsonString(item, "scheduledAt", out string scheduledToken)
                    && TryParsePacketOwnedEventDate(scheduledToken, out DateTime parsedScheduledAt)
                    ? parsedScheduledAt
                    : TryGetJsonString(item, "date", out string dateToken)
                        && TryParsePacketOwnedEventDate(dateToken, out DateTime parsedDate)
                        ? parsedDate
                        : DateTime.Today;
                string detail = TryGetJsonString(item, "detail", out string parsedDetail) ? parsedDetail : string.Empty;
                EventEntryStatus status = TryGetJsonString(item, "status", out string parsedStatusToken)
                    && TryParsePacketOwnedEventEntryStatus(parsedStatusToken, out EventEntryStatus parsedStatus)
                    ? parsedStatus
                    : EventEntryStatus.Upcoming;
                string statusText = TryGetJsonString(item, "statusText", out string parsedStatusText)
                    ? parsedStatusText
                    : GetDefaultPacketOwnedEventStatusText(status);
                int sourceTick = TryGetJsonInt32(item, "sourceTick", out int parsedSourceTick)
                    ? parsedSourceTick
                    : int.MinValue;
                destination.Add(CreatePacketOwnedEventCalendarEntry(scheduledAt, title.Trim(), detail, status, statusText, sourceTick, index++));
            }
        }

        private static bool TryParsePacketOwnedEventCalendarTextEntry(string line, int sortOrder, out EventEntrySnapshot entry)
        {
            entry = new EventEntrySnapshot();
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            string[] tokens = line.Split('|');
            if (tokens.Length == 0)
            {
                return false;
            }

            int tokenIndex = 0;
            DateTime scheduledAt = DateTime.Today;
            if (TryParsePacketOwnedEventDate(tokens[0], out DateTime parsedDate))
            {
                scheduledAt = parsedDate;
                tokenIndex = 1;
            }

            if (tokenIndex >= tokens.Length)
            {
                return false;
            }

            string title = tokens[tokenIndex++].Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                return false;
            }

            string detail = tokenIndex < tokens.Length ? tokens[tokenIndex++].Trim() : string.Empty;
            EventEntryStatus status = tokenIndex < tokens.Length
                && TryParsePacketOwnedEventEntryStatus(tokens[tokenIndex], out EventEntryStatus parsedStatus)
                ? parsedStatus
                : EventEntryStatus.Upcoming;
            entry = CreatePacketOwnedEventCalendarEntry(
                scheduledAt,
                title,
                detail,
                status,
                GetDefaultPacketOwnedEventStatusText(status),
                int.MinValue,
                sortOrder);
            return true;
        }

        private static EventEntrySnapshot CreatePacketOwnedEventCalendarEntry(
            DateTime scheduledAt,
            string title,
            string detail,
            EventEntryStatus status,
            string statusText,
            int sourceTick,
            int sortOrder)
        {
            return new EventEntrySnapshot
            {
                Title = title ?? string.Empty,
                Detail = detail ?? string.Empty,
                StatusText = string.IsNullOrWhiteSpace(statusText) ? GetDefaultPacketOwnedEventStatusText(status) : statusText.Trim(),
                Status = status,
                ScheduledAt = scheduledAt.Date,
                SourceTick = sourceTick,
                SortPriority = ResolvePacketOwnedEventEntrySortPriority(status),
                SortOrder = Math.Max(0, sortOrder)
            };
        }

        internal static int ResolvePacketOwnedEventEntrySortPriority(EventEntryStatus status)
        {
            return status switch
            {
                EventEntryStatus.Start or EventEntryStatus.InProgress => EventEntrySortPriorityPrimary,
                EventEntryStatus.Clear => EventEntrySortPrioritySecondary,
                EventEntryStatus.Upcoming => EventEntrySortPriorityFallback,
                _ => EventEntrySortPriorityRuntime
            };
        }

        private static string GetDefaultPacketOwnedEventStatusText(EventEntryStatus status)
        {
            return status switch
            {
                EventEntryStatus.Start => "Start",
                EventEntryStatus.InProgress => "Running",
                EventEntryStatus.Clear => "Clear",
                EventEntryStatus.Upcoming => "Will",
                _ => string.Empty
            };
        }

        private static bool TryParsePacketOwnedEventEntryStatus(string token, out EventEntryStatus status)
        {
            status = EventEntryStatus.Upcoming;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            switch (token.Trim().ToLowerInvariant())
            {
                case "start":
                    status = EventEntryStatus.Start;
                    return true;
                case "running":
                case "inprogress":
                case "ing":
                    status = EventEntryStatus.InProgress;
                    return true;
                case "clear":
                case "complete":
                case "completed":
                    status = EventEntryStatus.Clear;
                    return true;
                case "will":
                case "upcoming":
                case "none":
                    status = EventEntryStatus.Upcoming;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParsePacketOwnedEventDate(string token, out DateTime date)
        {
            if (DateTime.TryParse(
                    token?.Trim(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                    out date))
            {
                date = date.Date;
                return true;
            }

            date = default;
            return false;
        }

        private static bool TryGetJsonString(JsonElement element, string propertyName, out string value)
        {
            value = string.Empty;
            if (!element.TryGetProperty(propertyName, out JsonElement property)
                || property.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            value = property.GetString() ?? string.Empty;
            return true;
        }

        private static bool TryGetJsonBoolean(JsonElement element, string propertyName, out bool value)
        {
            value = false;
            if (!element.TryGetProperty(propertyName, out JsonElement property))
            {
                return false;
            }

            if (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
            {
                value = property.GetBoolean();
                return true;
            }

            return false;
        }

        private static bool TryGetJsonInt32(JsonElement element, string propertyName, out int value)
        {
            value = 0;
            if (!element.TryGetProperty(propertyName, out JsonElement property))
            {
                return false;
            }

            return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value);
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedUtilityCommand(string[] args)
        {
            int currentTickCount = Environment.TickCount;
            if (args.Length == 0)
            {
                return ChatCommandHandler.CommandResult.Info($"{DescribeLocalUtilityPacketInboxStatus()} {DescribeLocalUtilityPacketOutboxStatus()} {DescribePacketOwnedUtilityDispatchStatus(currentTickCount)} {_localUtilityPacketInbox.LastStatus} {_localUtilityPacketOutbox.LastStatus} {DescribeLocalUtilityOfficialSessionBridgeStatus()}");
            }

            switch (args[0].ToLowerInvariant())
            {
                case "status":
                    return ChatCommandHandler.CommandResult.Info($"{DescribeLocalUtilityPacketInboxStatus()} {DescribeLocalUtilityPacketOutboxStatus()} {DescribePacketOwnedUtilityDispatchStatus(currentTickCount)} {_localUtilityPacketInbox.LastStatus} {_localUtilityPacketOutbox.LastStatus} {DescribeLocalUtilityOfficialSessionBridgeStatus()}");

                case "inbox":
                    return HandlePacketOwnedUtilityInboxCommand(args);

                case "outbox":
                    return HandlePacketOwnedUtilityOutboxCommand(args);

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
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility delivery <questId> <itemId> [blockedQuestIdsCsv] [accept|complete|none]");
                    }

                    List<int> blockedQuestIds = new();
                    QuestDetailDeliveryType packetOwnedDeliveryType = QuestDetailDeliveryType.None;
                    if (args.Length >= 4)
                    {
                        if (TryParseQuestDeliveryTypeToken(args[3], out packetOwnedDeliveryType))
                        {
                        }
                        else
                        {
                            blockedQuestIds.AddRange(
                                args[3].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(value => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int blockedQuestId) ? blockedQuestId : 0)
                                    .Where(blockedQuestId => blockedQuestId > 0));
                        }
                    }

                    if (args.Length >= 5)
                    {
                        if (!TryParseQuestDeliveryTypeToken(args[4], out packetOwnedDeliveryType))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /localutility delivery <questId> <itemId> [blockedQuestIdsCsv] [accept|complete|none]");
                        }
                    }

                    return ChatCommandHandler.CommandResult.Ok(ApplyDeliveryQuestLaunch(deliveryQuestId, deliveryItemId, blockedQuestIds, packetOwnedDeliveryType));

                case "classcompetition":
                    return ChatCommandHandler.CommandResult.Ok(ApplyClassCompetitionPageLaunch());

                case "skillguide":
                    return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedSkillGuideLaunch());

                case "radioctx":
                case "radiocontext":
                    if (args.Length < 2)
                    {
                        return ChatCommandHandler.CommandResult.Info($"Usage: /localutility radioctx <status|left|right|on|off|1|0|reset>. {DescribePacketOwnedRadioCreateLayerContextStatus()}");
                    }

                    switch (args[1].ToLowerInvariant())
                    {
                        case "status":
                            return ChatCommandHandler.CommandResult.Info(DescribePacketOwnedRadioCreateLayerContextStatus());

                        case "left":
                        case "on":
                        case "1":
                            return ChatCommandHandler.CommandResult.Ok($"{SetPacketOwnedRadioCreateLayerContext(true)} {DescribePacketOwnedRadioCreateLayerContextStatus()}");

                        case "right":
                        case "off":
                        case "0":
                            return ChatCommandHandler.CommandResult.Ok($"{SetPacketOwnedRadioCreateLayerContext(false)} {DescribePacketOwnedRadioCreateLayerContextStatus()}");

                        case "reset":
                        case "clear":
                            return ChatCommandHandler.CommandResult.Ok($"{ClearPacketOwnedRadioCreateLayerContext()} {DescribePacketOwnedRadioCreateLayerContextStatus()}");

                        default:
                            return ChatCommandHandler.CommandResult.Error("Usage: /localutility radioctx <status|left|right|on|off|1|0|reset>");
                    }

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
                    "Usage: /localutility [status|inbox [status|start [port]|stop|packet <sitresult|emotion|randomemotion|questresult|openui|openuiwithoption|commodity|notice|chat|buffzone|eventsound|minigamesound|skillguide|minimaponoff|antimacro|apspevent|follow|followfail|directionmode|standalone|damagemeter|passivemove|timebomb|vengeance|exjablin|repeatskillmodeend|tanksiegeend|sg88manual|summonattackconfirm|hpdec|skillcooltime|193|231|232|242|243|246|247|250|251|252|258|262|263|264|265|266|267|268|269|270|271|272|273|274|275|276|291|1011|1012|1013|1014|1020|1021|classcompetition|classcompetitionauth|questguide|deliveryquest> [payloadhex=..|payloadb64=..]|packetraw <type> [hex]|packetclientraw <hex>]|outbox [status|start [port]|stop]|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]|directionmode <on|off|1|0> [delayMs]|standalone <on|off|1|0>|openui <uiType> [defaultTab]|openuiwithoption <uiType> <option>|commodity <serialNumber>|notice <text>|chat [channel|type19|whisper:name|whisperout:name] <text>|buffzone [text]|eventsound <image/path or path>|minigamesound <image/path or path>|questguide <questId> <mobId:mapId[,mapId...]>...|questguide clear|delivery <questId> <itemId> [blockedQuestIdsCsv] [accept|complete|none]|classcompetition|skillguide|radioctx <status|left|right|on|off|1|0|reset>|antimacro [status|launch <normal|admin> [first|retry]|notice <noticeType> [antiMacroType]|result <mode> [antiMacroType] [userName]|clear]|apsp [status|seed [characterId]|receive <token>|send <token>|context <receiveToken> [sendToken]|<contextToken> <11|12|13>|text]|follow <status|request <driverId|name> [auto|manual] [keyinput]|withdraw|release|ask <requesterId|name>|accept|decline|attach <driverId|name>|detach [transferX transferY]|passengerdetach [requesterId|name] [transferX transferY]>|followfail [reasonCode [driverId]|text]|packet <sitresult|emotion|randomemotion|questresult|openui|openuiwithoption|commodity|fade|balloon|damagemeter|passivemove|timebomb|vengeance|exjablin|repeatskillmodeend|tanksiegeend|sg88manual|summonattackconfirm|hpdec|notice|chat|buffzone|eventsound|minigamesound|questguide|delivery|classcompetition|classcompetitionauth|skillguide|hiretutor|tutormsg|minimaponoff|antimacro|apspevent|directionmode|standalone|follow|followfail|193|231|232|242|243|250|251|252|255|256|258|262|263|264|265|266|267|268|269|270|271|272|273|274|275|276|291|1011|1012|1013|1014|1020|1021> [payloadhex=..|payloadb64=..]|packetraw <type> <hex>|packetclientraw <hex>]");
            }
        }

        private bool TryApplyPacketOwnedMarriageResultPayload(byte[] payload, out string message)
        {
            message = null;
            if (_weddingInvitationController.TryOpenFromMarriageResultPacket(
                payload,
                WeddingInvitationRuntime.DefaultPacketOpenStyle,
                "packet-owned marriage-result handoff",
                uiWindowManager,
                _playerManager?.Player?.Build,
                _fontChat,
                ShowUtilityFeedbackMessage,
                () => ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.WeddingInvitation),
                out string invitationMessage))
            {
                message = invitationMessage;
                return true;
            }

            if (WeddingInvitationRuntime.TryDecodeMarriageResultOpenPayload(payload, out _, out _, out _, out string decodeMessage))
            {
                message = $"{decodeMessage} The packet-owned owner is now wired, but the simulator still defaults the invitation skin to {WeddingInvitationRuntime.DefaultPacketOpenStyle} because subtype {WeddingInvitationRuntime.ClientOpenResultSubtype} only carries groom, bride, and dialog-type fields.";
                return false;
            }

            message = invitationMessage;
            return false;
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedUtilityOutboxCommand(string[] args)
        {
            int offset = args.Length > 0 && string.Equals(args[0], "outbox", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            int currentTickCount = Environment.TickCount;
            const string usage = "Usage: /localutility outbox [status|start [port]|stop]";

            if (args.Length <= offset || string.Equals(args[offset], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info($"{DescribeLocalUtilityPacketOutboxStatus()} {DescribePacketOwnedUtilityDispatchStatus(currentTickCount)} {_localUtilityPacketOutbox.LastStatus}");
            }

            if (string.Equals(args[offset], "start", StringComparison.OrdinalIgnoreCase))
            {
                int port = LocalUtilityPacketTransportManager.DefaultPort;
                if (args.Length > offset + 1 && (!int.TryParse(args[offset + 1], out port) || port <= 0 || port > ushort.MaxValue))
                {
                    return ChatCommandHandler.CommandResult.Error(usage);
                }

                _localUtilityPacketOutboxConfiguredPort = port;
                _localUtilityPacketOutboxEnabled = true;
                EnsureLocalUtilityPacketOutboxState(shouldRun: true);
                return ChatCommandHandler.CommandResult.Ok($"{DescribeLocalUtilityPacketOutboxStatus()} {DescribePacketOwnedUtilityDispatchStatus(currentTickCount)} {_localUtilityPacketOutbox.LastStatus}");
            }

            if (string.Equals(args[offset], "stop", StringComparison.OrdinalIgnoreCase))
            {
                _localUtilityPacketOutboxEnabled = false;
                EnsureLocalUtilityPacketOutboxState(shouldRun: false);
                return ChatCommandHandler.CommandResult.Ok($"{DescribeLocalUtilityPacketOutboxStatus()} {DescribePacketOwnedUtilityDispatchStatus(currentTickCount)} {_localUtilityPacketOutbox.LastStatus}");
            }

            return ChatCommandHandler.CommandResult.Error(usage);
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
                return ChatCommandHandler.CommandResult.Error($"Usage: {usagePrefix} [status|start [port]|stop|packet <sitresult|emotion|randomemotion|questresult|resignquestreturn|passmatename|openui|openuiwithoption|commodity|notice|chat|buffzone|eventsound|minigamesound|radio|skillguide|minimaponoff|antimacro|apspevent|follow|followfail|directionmode|standalone|damagemeter|passivemove|timebomb|vengeance|exjablin|mechanicequip|hpdec|skillcooltime|marriageresult|repairresult|repairdurabilityresult|repairreply|193|231|232|242|243|246|247|250|251|252|258|259|260|261|262|263|264|265|266|267|268|269|270|271|272|273|274|275|276|291|1011|1012|1013|1014|1018|1023|1025|classcompetition|classcompetitionauth|questguide|deliveryquest> [payloadhex=..|payloadb64=..]|packetraw <type> [hex]|packetclientraw <hex>]");
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
                $"Usage: {usagePrefix} [status|start [port]|stop|packet <sitresult|emotion|randomemotion|questresult|resignquestreturn|passmatename|openui|openuiwithoption|commodity|notice|chat|buffzone|eventsound|minigamesound|radio|skillguide|antimacro|apspevent|follow|followfail|directionmode|standalone|damagemeter|passivemove|timebomb|vengeance|exjablin|mechanicequip|repeatskillmodeend|tanksiegeend|sg88manual|summonattackconfirm|hpdec|skillcooltime|marriageresult|repairresult|repairdurabilityresult|repairreply|193|231|232|242|243|246|247|250|251|252|258|259|260|261|262|263|264|265|266|267|268|269|270|271|272|273|274|275|276|291|1011|1012|1013|1014|1018|1020|1021|1023|1025|classcompetition|classcompetitionauth|questguide|deliveryquest> [payloadhex=..|payloadb64=..]|packetraw <type> [hex]|packetclientraw <hex>]");
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
                    applied = TryApplyPacketOwnedOpenUiPayload(payload, requireExactClientPayload: false, out message);
                    break;
                case "openuiwithoption":
                    applied = TryApplyPacketOwnedOpenUiWithOptionPayload(payload, requireExactClientPayload: false, out message);
                    break;
                case "commodity":
                    applied = TryApplyPacketOwnedCommodityPayload(payload, requireExactClientPayload: false, out message);
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
                case "passivemove":
                    applied = TryApplyPacketOwnedPassiveMovePayload(payload, out message);
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
                case "mechanicequip":
                case "mechequip":
                case "mechanicpane":
                    applied = TryApplyPacketOwnedMechanicEquipPayload(payload, out message);
                    break;
                case "repeatskillmodeend":
                case "repeatskillmodeendack":
                case "tanksiegeend":
                case "tanksiegemodeend":
                    applied = TryApplyPacketOwnedRepeatSkillModeEndAckPayload(payload, out message);
                    break;
                case "sg88manual":
                case "sg88manualattack":
                case "sg88manualconfirm":
                case "summonattackconfirm":
                    applied = TryApplyPacketOwnedSg88ManualAttackConfirmPayload(payload, out message);
                    break;
                case "hpdec":
                    applied = TryApplyPacketOwnedFieldHazardPayload(payload, out message);
                    break;
                case "hazardresult":
                case "petconsumeresult":
                case "petitemuseresult":
                    applied = TryApplyPacketOwnedPetConsumeResultPayload(payload, out message);
                    break;
                case "notice":
                    applied = TryApplyPacketOwnedNoticePayload(payload, out message);
                    break;
                case "chat":
                    applied = TryApplyPacketOwnedChatPayload(payload, out message);
                    break;
                case "eventalarm":
                case "eventalarmtext":
                case "eventct":
                    applied = TryApplyPacketOwnedEventAlarmTextPayload(payload, out message);
                    break;
                case "eventcalendar":
                case "eventcalendarentries":
                case "attendancecalendar":
                case "eventlistentries":
                    applied = TryApplyPacketOwnedEventCalendarEntriesPayload(payload, out message);
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
                case "resignquest":
                case "resignquestreturn":
                    applied = TryApplyPacketOwnedResignQuestReturnPayload(payload, out message);
                    break;
                case "passmatename":
                case "matename":
                    applied = TryApplyPacketOwnedPassMateNamePayload(payload, out message);
                    break;
                case "questguide":
                    applied = TryApplyPacketOwnedQuestGuidePayload(payload, out message);
                    break;
                case "delivery":
                    applied = TryApplyPacketOwnedDeliveryPayload(payload, out message);
                    break;
                case "accountmoreinfo":
                case "moreinfo":
                    applied = TryApplyPacketOwnedAccountMoreInfoPayload(payload, out message);
                    break;
                case "setgender":
                    applied = TryApplyPacketOwnedSetGenderPayload(payload, out message);
                    break;
                case "classcompetition":
                    message = ApplyClassCompetitionPageLaunch();
                    applied = true;
                    break;
                case "classcompetitionauth":
                case "classcompetitionkey":
                case "auth291":
                    applied = TryApplyPacketOwnedClassCompetitionAuthPayload(payload, out message);
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
                case "minimaponoff":
                case "miniMapOnOff":
                    applied = TryApplyPacketOwnedMiniMapOnOffPayload(payload, out message);
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
                    applied = TryApplyPacketOwnedFollowCharacterPayload(payload, clientOpcodePayload: false, out message);
                    break;
                case "followask":
                case "followprompt":
                case "followrequestprompt":
                    applied = TryApplyPacketOwnedFollowCharacterPromptPayload(payload, out message);
                    break;
                case "followfail":
                    applied = TryApplyPacketOwnedFollowCharacterFailedPayload(payload, out message);
                    break;
                case "marriageresult":
                case "weddinginvitation":
                    applied = TryApplyPacketOwnedMarriageResultPayload(payload, out message);
                    break;
                default:
                    return ChatCommandHandler.CommandResult.Error(
                        rawHex
                ? "Usage: /localutility packetraw <sitresult|emotion|randomemotion|questresult|resignquestreturn|passmatename|openui|openuiwithoption|commodity|fade|balloon|damagemeter|passivemove|timebomb|vengeance|exjablin|mechanicequip|hpdec|notice|chat|eventalarm|eventcalendar|buffzone|eventsound|minigamesound|radio|questguide|delivery|classcompetition|skillguide|hiretutor|tutormsg|antimacro|apspevent|directionmode|standalone|follow|followask|followfail|skillcooltime|marriageresult|repairresult|repairdurabilityresult|repairreply|193|231|232|242|243|246|247|250|251|252|255|256|258|259|260|261|262|263|264|265|266|267|268|269|270|271|272|273|274|275|276|1011|1012|1013|1014|1018|1022|1023|1025|1031|1032> <hex>"
                : "Usage: /localutility packet <sitresult|emotion|randomemotion|questresult|resignquestreturn|passmatename|openui|openuiwithoption|commodity|fade|balloon|damagemeter|passivemove|timebomb|vengeance|exjablin|mechanicequip|hpdec|notice|chat|eventalarm|eventcalendar|buffzone|eventsound|minigamesound|radio|questguide|delivery|classcompetition|skillguide|hiretutor|tutormsg|antimacro|apspevent|directionmode|standalone|follow|followask|followfail|skillcooltime|marriageresult|repairresult|repairdurabilityresult|repairreply|193|231|232|242|243|246|247|250|251|252|255|256|258|259|260|261|262|263|264|265|266|267|268|269|270|271|272|273|274|275|276|1011|1012|1013|1014|1018|1022|1023|1025|1031|1032> [payloadhex=..|payloadb64=..]");
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

                _localUtilityOfficialSessionBridgeEnabled = true;
                _localUtilityOfficialSessionBridgeUseDiscovery = false;
                _localUtilityOfficialSessionBridgeConfiguredListenPort = listenPort;
                _localUtilityOfficialSessionBridgeConfiguredRemoteHost = args[2];
                _localUtilityOfficialSessionBridgeConfiguredRemotePort = remotePort;
                _localUtilityOfficialSessionBridgeConfiguredProcessSelector = null;
                _localUtilityOfficialSessionBridgeConfiguredLocalPort = null;
                EnsureLocalUtilityOfficialSessionBridgeState(shouldRun: true);
                return ChatCommandHandler.CommandResult.Ok(DescribeLocalUtilityOfficialSessionBridgeStatus());
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

                _localUtilityOfficialSessionBridgeEnabled = true;
                _localUtilityOfficialSessionBridgeUseDiscovery = true;
                _localUtilityOfficialSessionBridgeConfiguredListenPort = autoListenPort;
                _localUtilityOfficialSessionBridgeConfiguredRemotePort = autoRemotePort;
                _localUtilityOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
                _localUtilityOfficialSessionBridgeConfiguredProcessSelector = processSelector;
                _localUtilityOfficialSessionBridgeConfiguredLocalPort = localPortFilter;
                _nextLocalUtilityOfficialSessionBridgeDiscoveryRefreshAt = 0;

                return _localUtilityOfficialSessionBridge.TryRefreshFromDiscovery(
                        autoListenPort,
                        autoRemotePort,
                        processSelector,
                        localPortFilter,
                        out string startStatus)
                    ? ChatCommandHandler.CommandResult.Ok($"{startStatus} {DescribeLocalUtilityOfficialSessionBridgeStatus()}")
                    : ChatCommandHandler.CommandResult.Error(startStatus);
            }

            if (string.Equals(args[0], "stop", StringComparison.OrdinalIgnoreCase))
            {
                _localUtilityOfficialSessionBridgeEnabled = false;
                _localUtilityOfficialSessionBridgeUseDiscovery = false;
                _localUtilityOfficialSessionBridgeConfiguredRemotePort = 0;
                _localUtilityOfficialSessionBridgeConfiguredProcessSelector = null;
                _localUtilityOfficialSessionBridgeConfiguredLocalPort = null;
                _localUtilityOfficialSessionBridge.Stop();
                return ChatCommandHandler.CommandResult.Ok(DescribeLocalUtilityOfficialSessionBridgeStatus());
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
                    if (!_localFollowRuntime.TrySendOutgoingRequest(localUser, requestedDriver, currTickCount, autoRequest, keyInput, out string requestMessage))
                    {
                        return ChatCommandHandler.CommandResult.Error(requestMessage);
                    }

                    if (!TryMirrorPacketOwnedFollowRequestToOfficialSession(requestedDriver.CharacterId, autoRequest, keyInput, out string requestBridgeStatus))
                    {
                        return ChatCommandHandler.CommandResult.Error($"{requestMessage} {requestBridgeStatus}".Trim());
                    }

                    return ChatCommandHandler.CommandResult.Ok(string.IsNullOrWhiteSpace(requestBridgeStatus)
                        ? requestMessage
                        : $"{requestMessage} {requestBridgeStatus}".Trim());

                case "withdraw":
                    StampPacketOwnedUtilityRequestState();
                    if (!_localFollowRuntime.TryWithdrawOutgoingRequest(currTickCount, ResolvePacketOwnedRemoteCharacterName, out string withdrawMessage))
                    {
                        return ChatCommandHandler.CommandResult.Error(withdrawMessage);
                    }

                    if (!TryMirrorPacketOwnedFollowWithdrawToOfficialSession(out string withdrawBridgeStatus))
                    {
                        return ChatCommandHandler.CommandResult.Error($"{withdrawMessage} {withdrawBridgeStatus}".Trim());
                    }

                    return ChatCommandHandler.CommandResult.Ok(string.IsNullOrWhiteSpace(withdrawBridgeStatus)
                        ? withdrawMessage
                        : $"{withdrawMessage} {withdrawBridgeStatus}".Trim());

                case "release":
                    StampPacketOwnedUtilityRequestState();
                    if (!_localFollowRuntime.TrySendAttachedReleaseRequest(currTickCount, ResolvePacketOwnedRemoteCharacterName, out string releaseMessage))
                    {
                        return ChatCommandHandler.CommandResult.Error(releaseMessage);
                    }

                    if (!TryMirrorPacketOwnedFollowRequestToOfficialSession(0, autoRequest: false, keyInput: true, out string releaseBridgeStatus))
                    {
                        return ChatCommandHandler.CommandResult.Error($"{releaseMessage} {releaseBridgeStatus}".Trim());
                    }

                    return ChatCommandHandler.CommandResult.Ok(string.IsNullOrWhiteSpace(releaseBridgeStatus)
                        ? releaseMessage
                        : $"{releaseMessage} {releaseBridgeStatus}".Trim());

                case "ask":
                    if (args.Length < 2 || !TryResolvePacketOwnedRemoteCharacterToken(args[1], out int requesterId, out _)
                        || !TryResolvePacketOwnedRemoteCharacterSnapshot(requesterId, out LocalFollowUserSnapshot requester))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /localutility follow ask <requesterId|name>");
                    }

                    if (!TryOpenPacketOwnedFollowCharacterPrompt(requester, out string askMessage))
                    {
                        return ChatCommandHandler.CommandResult.Error(askMessage);
                    }
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
                    ClearPacketOwnedPassiveMoveState();
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
                    ClearPacketOwnedPassiveMoveState();
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
                    return ChatCommandHandler.CommandResult.Error("Usage: /localutility follow <status|request <driverId|name> [auto|manual] [keyinput]|withdraw|release|ask <requesterId|name>|accept|decline|attach <driverId|name>|detach [transferX transferY]|passengerdetach [requesterId|name] [transferX transferY]>");
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

            CharacterPart equippedMountPart = null;
            player.Build?.Equipment?.TryGetValue(EquipSlot.TamingMob, out equippedMountPart);
            CharacterPart mountedStateMountPart = player.ResolveMountedStateTamingMobPart();
            bool isMounted = FollowCharacterEligibilityResolver.ResolveMountedVehicleId(
                equippedMountPart,
                player.CurrentActionName,
                mechanicMode: null,
                activeMountedRenderOwner: mountedStateMountPart) > 0;
            bool isImmovable = player.State == PlayerState.Sitting
                || player.IsMovementLockedBySkillTransform
                || player.GmFlyMode
                || player.Physics.IsOnLadderOrRope
                || player.Physics.IsUserFlying()
                || player.Physics.IsInSwimArea;
            bool isGhost = FollowCharacterEligibilityResolver.IsGhostAction(player.CurrentActionName);
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

            bool isMounted = actor.RidingVehicleId > 0;
            bool hasMorphTemplate = actor.HasMorphTemplate
                || actor.Build?.Body?.Type == CharacterPartType.Morph;
            bool isGhost = FollowCharacterEligibilityResolver.IsGhostAction(actor.ActionName);
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

            bool shouldDismissFollowPrompt = ShouldDismissPacketOwnedFollowPromptForRemovedCharacterForTest(
                _packetOwnedFollowPromptActive,
                _localFollowRuntime.IncomingRequesterId,
                characterId);
            Func<int, string> nameResolver = removedId =>
                removedId == characterId ? removedName : ResolvePacketOwnedRemoteCharacterName(removedId);
            if (!_localFollowRuntime.TryClearMissingRemoteCharacter(characterId, nameResolver, out string message))
            {
                return;
            }

            StampPacketOwnedUtilityRequestState();
            if (shouldDismissFollowPrompt)
            {
                HidePacketOwnedFollowCharacterPrompt();
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
                || _playerManager?.Player == null)
            {
                return;
            }

            if (_lastPacketOwnedPassiveMoveSnapshot != null)
            {
                PassivePositionSnapshot passiveMove = _lastPacketOwnedPassiveMoveSnapshot.SampleAtTime(currTickCount);
                _playerManager.Player.ApplyPacketOwnedPassiveMove(
                    passiveMove,
                    currTickCount,
                    ResolvePacketOwnedLocalFollowFoothold(passiveMove.FootholdId));
                return;
            }

            if (!TryResolvePacketOwnedRemoteCharacterSnapshot(_localFollowRuntime.AttachedDriverId, out LocalFollowUserSnapshot driver))
            {
                return;
            }

            if (_remoteUserPool != null
                && _remoteUserPool.TryGetActor(_localFollowRuntime.AttachedDriverId, out RemoteUserActor driverActor))
            {
                PassivePositionSnapshot passiveMove = PacketOwnedLocalFollowPassiveMoveResolver.ResolveFollowerPassivePosition(
                    driverActor.MovementSnapshot,
                    driverActor.Position,
                    driverActor.FacingRight,
                    driverActor.ActionName,
                    driverActor.CurrentFootholdId,
                    currTickCount);
                _playerManager.Player.ApplyPacketOwnedPassiveMove(
                    passiveMove,
                    currTickCount,
                    ResolvePacketOwnedLocalFollowFoothold(passiveMove.FootholdId));
                return;
            }

            ApplyLocalFollowPlayerResult(new LocalFollowApplyResult(
                PlayerPositionChanged: true,
                PlayerPosition: driver.Position,
                PlayerFacingRightChanged: true,
                PlayerFacingRight: driver.FacingRight));
        }

        private void ClearPacketOwnedPassiveMoveState()
        {
            _lastPacketOwnedPassiveMoveSnapshot = null;
            _lastPacketOwnedPassiveMoveTick = int.MinValue;
        }

        private FootholdLine ResolvePacketOwnedLocalFollowFoothold(int footholdId)
        {
            if (footholdId == 0)
            {
                return null;
            }

            IReadOnlyList<FootholdLine> footholds = _mapBoard?.BoardItems?.FootholdLines;
            if (footholds == null)
            {
                return null;
            }

            for (int i = 0; i < footholds.Count; i++)
            {
                FootholdLine foothold = footholds[i];
                if (foothold?.num == footholdId)
                {
                    return foothold;
                }
            }

            return null;
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

        private bool IsFollowRequestOptionEnabled()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.OptionMenu) is OptionMenuWindow optionMenuWindow
                && optionMenuWindow.TryGetCommittedClientOptionValue(FollowRequestClientOptionId, out bool enabled))
            {
                return enabled;
            }

            return true;
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
