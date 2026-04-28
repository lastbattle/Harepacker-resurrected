using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using CashServiceOwnerStageKind = HaCreator.MapSimulator.UI.CashServiceStageKind;

using BinaryWriter = MapleLib.PacketLib.PacketWriter;
namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private static readonly string[] CashShopChildOwnerWindowNames =
        {
            MapSimulatorWindowNames.CashShopLocker,
            MapSimulatorWindowNames.CashShopInventory,
            MapSimulatorWindowNames.CashShopList,
            MapSimulatorWindowNames.CashShopStatus,
            MapSimulatorWindowNames.CashShopOneADay
        };

        private static readonly string[] CashShopModalOwnerWindowNames =
        {
            MapSimulatorWindowNames.CashCouponDialog,
            MapSimulatorWindowNames.CashPurchaseConfirmDialog,
            MapSimulatorWindowNames.CashReceiveGiftDialog,
            MapSimulatorWindowNames.CashNameChangeLicenseDialog,
            MapSimulatorWindowNames.CashTransferWorldLicenseDialog
        };

        private static readonly string[] ItcChildOwnerWindowNames =
        {
            MapSimulatorWindowNames.ItcCharacter,
            MapSimulatorWindowNames.ItcSale,
            MapSimulatorWindowNames.ItcPurchase,
            MapSimulatorWindowNames.ItcInventory,
            MapSimulatorWindowNames.ItcTab,
            MapSimulatorWindowNames.ItcSubTab,
            MapSimulatorWindowNames.ItcList,
            MapSimulatorWindowNames.ItcStatus
        };

        private readonly CashServicePacketInboxManager _cashServicePacketInbox = new();
        private bool? _cashServicePacketInboxCommandOverrideEnabled;
        private int _cashServicePacketInboxConfiguredPort = CashServicePacketInboxManager.DefaultPort;
        private const string CashServiceStageBgmPath = "BgmUI/ShopBgm";
        private const int CashShopOneADayHistorySlotCount = 12;
        private const int CashShopOneADaySelectorInitArg = 4;
        private const int CashShopLockerScrollBarControlId = 1001;
        private const int CashShopInventoryTabControlId = 1000;
        private const int CashShopInventoryScrollBarControlId = 1001;
        private int _lastPlayedCashGachaponAnimationSequence;
        private bool _cashReceiveGiftFollowUpNoticePending;
        private int _cashReceiveGiftFollowUpNoticeNextIndex = -1;

        private sealed class CashInventoryPacketFocusSnapshot
        {
            public string ActiveTabName { get; init; } = string.Empty;
            public string FocusTitle { get; init; } = string.Empty;
            public string FocusMessage { get; init; } = string.Empty;
            public string FocusSignature { get; init; } = string.Empty;
            public int FirstPosition { get; init; } = 1;
            public int ScrollOffset { get; init; }
            public int RowFocusIndex { get; init; }
        }

        private sealed class CashShopOneADayArtSnapshot
        {
            public bool HasNoItemCanvas { get; init; }
            public bool HasKeyFocusCanvas { get; init; }
            public bool HasPlateCanvas { get; init; }
            public bool HasPlateBigCanvas { get; init; }
            public bool HasShortcutHelpCanvas { get; init; }
            public bool HasBuyButton { get; init; }
            public bool HasGiftButton { get; init; }
            public bool HasItemBoxButton { get; init; }
            public int BuyButtonWidth { get; init; }
            public int BuyButtonHeight { get; init; }
            public int GiftButtonWidth { get; init; }
            public int GiftButtonHeight { get; init; }
            public int ItemBoxButtonWidth { get; init; }
            public int ItemBoxButtonHeight { get; init; }
            public int JoinButtonWidth { get; init; }
            public int JoinButtonHeight { get; init; }
            public int NumberCanvasCount { get; init; }
            public int NumberCanvasReadyMask { get; init; }
            public int PlateCount { get; init; }
            public bool ResolvedFromClientStringPool { get; init; }
        }

        private sealed class CashShopListArtSnapshot
        {
            public bool HasBuyKeyFocusCanvas { get; init; }
            public bool HasGiftKeyFocusCanvas { get; init; }
            public bool HasReserveKeyFocusCanvas { get; init; }
            public bool HasRemoveKeyFocusCanvas { get; init; }
            public bool HasAnyKeyFocusCanvas =>
                HasBuyKeyFocusCanvas
                || HasGiftKeyFocusCanvas
                || HasReserveKeyFocusCanvas
                || HasRemoveKeyFocusCanvas;
        }

        private string PreviewCashAvatarWeatherAction()
        {
            int currTickCount = Environment.TickCount;
            ReleaseActiveKeydownSkillForClientCancelIngress(currTickCount);
            string restrictionMessage = FieldInteractionRestrictionEvaluator.GetCashWeatherRestrictionMessage(_mapBoard?.MapInfo?.fieldLimit ?? 0);
            if (!string.IsNullOrWhiteSpace(restrictionMessage))
            {
                PushFieldRuleMessage(restrictionMessage, currTickCount, showOverlay: false);
                return restrictionMessage;
            }

            _fieldEffects?.AddWeatherMessage("Cash Shop weather preview staged.", WeatherEffectType.None, currTickCount);
            return "CCSWnd_Char::BlowWeather staged the selected cash-weather preview action.";
        }

        private string ShowCashAvatarPersonalShopAction()
        {
            ReleaseActiveKeydownSkillForClientCancelIngress(Environment.TickCount);
            return ShowSocialRoomWindowForCallback(
                SocialRoomKind.PersonalShop,
                "CCSWnd_Char::ShowPersonalShop opened the dedicated personal-shop owner.");
        }

        private string ShowCashAvatarEntrustedShopAction()
        {
            ReleaseActiveKeydownSkillForClientCancelIngress(Environment.TickCount);
            return ShowSocialRoomWindowForCallback(
                SocialRoomKind.EntrustedShop,
                "CCSWnd_Char::ShowEntrustedShop opened the dedicated entrusted-shop owner.");
        }

        private string ShowCashAvatarTradingRoomAction()
        {
            ReleaseActiveKeydownSkillForClientCancelIngress(Environment.TickCount);
            ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.CashTradingRoom);
            return "CCSWnd_Char handed the selected listing to CCashTradingRoomDlg.";
        }

        private string ExecuteCashServiceClientCancelIngress(Func<string> action)
        {
            return ExecuteCashServiceClientCancelIngressForTests(
                Environment.TickCount,
                ReleaseActiveKeydownSkillForClientCancelIngress,
                action);
        }

        internal static string ExecuteCashServiceClientCancelIngressForTests(
            int currentTime,
            Action<int> releaseActiveKeydownSkillForClientCancelIngress,
            Func<string> action)
        {
            releaseActiveKeydownSkillForClientCancelIngress?.Invoke(currentTime);
            return action?.Invoke() ?? string.Empty;
        }

        private void WireCashServiceOwnerWindows()
        {
            IInventoryRuntime inventoryRuntime = uiWindowManager?.InventoryWindow as IInventoryRuntime;
            IStorageRuntime storageRuntime = uiWindowManager?.GetWindow(MapSimulatorWindowNames.Trunk) as IStorageRuntime;

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShop) is AdminShopDialogUI cashShopWindow)
            {
                cashShopWindow.SetInventory(inventoryRuntime);
                cashShopWindow.SetStorageRuntime(storageRuntime);
                cashShopWindow.SetCashBalances(_loginAccountCashShopNxCredit);
                cashShopWindow.TryConsumeCashBalance = TryConsumeLoginAccountCashShopNxCredit;
                cashShopWindow.ResolveStorageExpansionCommoditySerialNumber = ResolveStorageExpansionCommoditySerialNumber;
                cashShopWindow.GetStorageExpansionStatusSummary = GetStorageExpansionStatusSummary;
                cashShopWindow.StorageExpansionResolved = HandleStorageExpansionResolved;
                cashShopWindow.WindowHidden = _ => HideCashShopOwnerFamilyWindows();
                WireCashShopChildOwnerWindows(cashShopWindow);
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) is CashServiceStageWindow cashShopStageWindow)
            {
                cashShopStageWindow.SetFont(_fontChat);
                cashShopStageWindow.SetCharacterBuild(_playerManager?.Player?.Build);
                cashShopStageWindow.SetInventory(inventoryRuntime);
                cashShopStageWindow.SetStorageRuntime(storageRuntime);
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashAvatarPreview) is CashAvatarPreviewWindow cashAvatarPreviewWindow
                && _playerManager?.Player?.Build != null)
            {
                cashAvatarPreviewWindow.CharacterBuild = _playerManager.Player.Build;
                cashAvatarPreviewWindow.SetFont(_fontChat);
                cashAvatarPreviewWindow.EquipmentLoader = _playerManager.Loader != null ? _playerManager.Loader.LoadEquipment : null;
                cashAvatarPreviewWindow.ClientCancelIngressRequested =
                    () => ReleaseActiveKeydownSkillForClientCancelIngress(currTickCount);
                cashAvatarPreviewWindow.PersonalShopRequested = ShowCashAvatarPersonalShopAction;
                cashAvatarPreviewWindow.EntrustedShopRequested = ShowCashAvatarEntrustedShopAction;
                cashAvatarPreviewWindow.TradingRoomRequested = ShowCashAvatarTradingRoomAction;
                cashAvatarPreviewWindow.WeatherRequested = PreviewCashAvatarWeatherAction;
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashTradingRoom) is CashTradingRoomWindow cashTradingRoomWindow)
            {
                cashTradingRoomWindow.SetFont(_fontChat);
                cashTradingRoomWindow.SetWalletProvider(() => (int)Math.Clamp(ResolveCurrentCashServiceMesoBalance(), 0L, int.MaxValue));
                cashTradingRoomWindow.SetTraderNames(_playerManager?.Player?.Build?.Name, "CashTrader");
                cashTradingRoomWindow.SetPacketSessionProvider(BuildCashTradingRoomPacketSessionSnapshot);
            }

            WireCashShopModalOwnerWindows();

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.Mts) is AdminShopDialogUI mtsWindow)
            {
                mtsWindow.SetInventory(inventoryRuntime);
                mtsWindow.SetStorageRuntime(storageRuntime);
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.MtsStatus) is CashServiceStageWindow mtsStageWindow)
            {
                mtsStageWindow.SetFont(_fontChat);
                mtsStageWindow.SetCharacterBuild(_playerManager?.Player?.Build);
                mtsStageWindow.SetInventory(inventoryRuntime);
                mtsStageWindow.SetStorageRuntime(storageRuntime);
            }

            WireItcChildOwnerWindows();
        }

        private void WireCashShopChildOwnerWindows(AdminShopDialogUI cashShopWindow)
        {
            if (cashShopWindow == null)
            {
                return;
            }

            IInventoryRuntime inventoryRuntime = uiWindowManager?.InventoryWindow as IInventoryRuntime;

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopLocker) is CashShopStageChildWindow lockerWindow)
            {
                lockerWindow.SetFont(_fontChat);
                lockerWindow.SetContentProvider(() => BuildCashShopLockerOwnerLines(cashShopWindow));
                lockerWindow.SetLockerStateProvider(() => BuildCashShopLockerOwnerState(cashShopWindow));
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopInventory) is CashShopStageChildWindow inventoryWindow)
            {
                inventoryWindow.SetFont(_fontChat);
                inventoryWindow.SetContentProvider(() => BuildCashShopInventoryOwnerLines(cashShopWindow));
                inventoryWindow.SetInventoryVisibleRowProvider((tabName, scrollOffset, maxRows) =>
                    BuildCashServiceInventoryOwnerRows(inventoryRuntime, tabName, scrollOffset, maxRows));
                inventoryWindow.SetInventoryStateProvider(() => BuildCashShopInventoryOwnerState(cashShopWindow));
                inventoryWindow.SetExternalAction(
                    "BtExTrunk",
                    () => ExecuteCashServiceClientCancelIngress(
                        () => cashShopWindow.ExecuteCashStageInventoryAction("BtExTrunk")));
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopList) is CashShopStageChildWindow listWindow)
            {
                listWindow.SetFont(_fontChat);
                listWindow.SetContentProvider(() => BuildCashShopListOwnerLines(cashShopWindow));
                listWindow.SetListRowSelectionAction(cashShopWindow.SelectListOwnerVisibleRow);
                listWindow.SetListScrollAction(cashShopWindow.MoveListOwnerSelection);
                listWindow.SetExternalAction(
                    "BtBuy",
                    () => ExecuteCashServiceClientCancelIngress(
                        () => ShowCashPurchaseConfirmDialog(cashShopWindow)));
                listWindow.SetExternalAction(
                    "BtGift",
                    () => ExecuteCashServiceClientCancelIngress(
                        () => cashShopWindow.ExecuteCashStageListAction("BtGift")));
                listWindow.SetExternalAction(
                    "BtReserve",
                    () => ExecuteCashServiceClientCancelIngress(
                        () => cashShopWindow.ExecuteCashStageListAction("BtReserve")));
                listWindow.SetExternalAction(
                    "BtRemove",
                    () => ExecuteCashServiceClientCancelIngress(
                        () => cashShopWindow.ExecuteCashStageListAction("BtRemove")));
                listWindow.SetExternalAction("TogglePane", cashShopWindow.ToggleListOwnerPane);
                listWindow.SetExternalAction("PageUp", () => cashShopWindow.MoveListOwnerSelectionByPage(-1));
                listWindow.SetExternalAction("PageDown", () => cashShopWindow.MoveListOwnerSelectionByPage(1));
                listWindow.SetExternalAction("Home", () => cashShopWindow.SelectListOwnerBoundary(false));
                listWindow.SetExternalAction("End", () => cashShopWindow.SelectListOwnerBoundary(true));
                listWindow.SetListScrollOffsetAction(cashShopWindow.ScrollListOwnerToOffset);
                listWindow.SetListStateProvider(() => BuildCashShopListOwnerState(cashShopWindow));
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStatus) is CashShopStageChildWindow statusWindow)
            {
                statusWindow.SetFont(_fontChat);
                statusWindow.SetContentProvider(BuildCashShopStatusOwnerLines);
                statusWindow.SetStatusStateProvider(() =>
                {
                    if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) is not CashServiceStageWindow stageWindow)
                    {
                        return null;
                    }

                    return new CashShopStageChildWindow.StatusOwnerState
                    {
                        NexonCashBalance = stageWindow.NexonCashBalance,
                        MaplePointBalance = stageWindow.MaplePointBalance,
                        PrepaidCashBalance = stageWindow.PrepaidCashBalance,
                        ChargeParam = stageWindow.ChargeParam,
                        StatusMessage = stageWindow.StatusMessage,
                        DetailSummaries = stageWindow.GetStatusOwnerDetailLines()
                    };
                });
                statusWindow.SetExternalAction("BtCharge", () => "CCSWnd_Status kept the dedicated charge button armed; live billing flow remains outside the simulator.");
                statusWindow.SetExternalAction("BtCheck", () => BuildCashShopStatusOwnerLines()[0]);
                statusWindow.SetExternalAction(
                    "BtCoupon",
                    () => ExecuteCashServiceClientCancelIngress(
                        ShowCashCouponDialog));
                statusWindow.SetExternalAction(
                    "BtExit",
                    () => ExecuteCashServiceClientCancelIngress(
                        () =>
                        {
                            HideCashShopOwnerFamilyWindows();
                            return "CCSWnd_Status closed the parent CCashShop owner family.";
                        }));
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopOneADay) is CashShopStageChildWindow oneADayWindow)
            {
                oneADayWindow.SetFont(_fontChat);
                oneADayWindow.SetContentProvider(BuildCashShopOneADayOwnerLines);
                oneADayWindow.SetOneADayStateProvider(() =>
                {
                    if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) is not CashServiceStageWindow stageWindow)
                    {
                        return null;
                    }

                    CashShopOneADayArtSnapshot artSnapshot = ResolveCashShopOneADayArtSnapshot();
                    IReadOnlyList<CashShopStageChildWindow.OneADayOwnerState.HistoryEntryState> historyEntries =
                        BuildCashShopOneADayHistoryEntryStates(stageWindow);
                    bool packetOwnedRewardSessionByte = stageWindow.CashOneADayHasPacketRewardSessionByte;
                    int packetRewardSessionByte = stageWindow.CashOneADayPacketRewardSessionByte & 0xFF;
                    bool packetOwnedPending = ResolveCashShopOneADayPendingState(
                        packetOwnedRewardSessionByte,
                        packetRewardSessionByte,
                        stageWindow.IsOneADayPending);
                    bool previousLaneEnabled = IsCashShopOneADayPreviousLaneEnabled(
                        packetOwnedRewardSessionByte,
                        packetRewardSessionByte,
                        historyEntries);
                    int selectorIndex = Math.Clamp(oneADayWindow.GetOneADaySelectorIndex(), 0, previousLaneEnabled ? 1 : 0);
                    if (packetOwnedRewardSessionByte)
                    {
                        selectorIndex = previousLaneEnabled && (packetRewardSessionByte & 2) != 0 ? 1 : 0;
                    }

                    int currentCommoditySerialNumber = Math.Max(0, stageWindow.CashOneADayItemSerialNumber);
                    TryResolveCashShopOneADayRemainingTime(
                        stageWindow.CashOneADayItemDate,
                        DateTime.Now,
                        out int remainingHour,
                        out int remainingMinute,
                        out int remainingSecond);
                    return new CashShopStageChildWindow.OneADayOwnerState
                    {
                        IsPending = packetOwnedPending,
                        NoticeState = stageWindow.NoticeState,
                        SelectorIndex = selectorIndex,
                        SelectorControlId = 2001,
                        SelectorInitArg = CashShopOneADaySelectorInitArg,
                        SelectorStartX = 2,
                        SelectorStartY = 2,
                        SelectorStartWidth = 1,
                        SelectorStartHeight = 1,
                        SelectorPosition = new Microsoft.Xna.Framework.Point(412, 406),
                        TodaySelectorLabel = MapleStoryStringPool.GetOrFallback(0x16A1, "Today"),
                        PreviousSelectorLabel = MapleStoryStringPool.GetOrFallback(0x16A2, "Previous"),
                        SelectorCount = previousLaneEnabled ? 2 : 1,
                        SelectorEntries = BuildCashShopOneADaySelectorEntryStates(
                            selectorIndex,
                            MapleStoryStringPool.GetOrFallback(0x16A1, "Today"),
                            MapleStoryStringPool.GetOrFallback(0x16A2, "Previous"),
                            previousLaneEnabled),
                        HasKeyFocusCanvas = artSnapshot.HasKeyFocusCanvas,
                        HasPlateCanvas = artSnapshot.HasPlateCanvas,
                        HasPlateBigCanvas = artSnapshot.HasPlateBigCanvas,
                        NumberCanvasCount = artSnapshot.NumberCanvasCount,
                        NumberCanvasReadyMask = artSnapshot.NumberCanvasReadyMask,
                        ExpectedNumberCanvasCount = 10,
                        PlateCount = Math.Max(1, artSnapshot.PlateCount),
                        PlateButtonCount = previousLaneEnabled ? CashShopOneADayHistorySlotCount : 2,
                        PreviousOfferCount = previousLaneEnabled ? ResolveCashShopOneADayHistorySlotCount() : 0,
                        PlateCanvasBaseName = "NoItem",
                        ShortcutHelpCanvasName = artSnapshot.HasShortcutHelpCanvas ? "ShortcutHelp" : string.Empty,
                        CurrentCommoditySerialNumber = currentCommoditySerialNumber,
                        CurrentItemLabel = ResolveCashShopOneADayCommodityLabel(currentCommoditySerialNumber),
                        CurrentDateRaw = stageWindow.CashOneADayItemDate,
                        CurrentDateLabel = FormatCashShopOneADayDate(stageWindow.CashOneADayItemDate),
                        Hour = remainingHour,
                        Minute = remainingMinute,
                        Second = remainingSecond,
                        CounterSlots = BuildCashShopOneADayCounterSlotStates(
                            remainingHour,
                            remainingMinute,
                            remainingSecond,
                            artSnapshot.NumberCanvasReadyMask),
                        PlateButtons = BuildCashShopOneADayPlateButtonStates(
                            artSnapshot,
                            selectorIndex,
                            previousLaneEnabled,
                            historyEntries,
                            currentCommoditySerialNumber),
                        RewardSessionSummary = BuildCashShopOneADayRewardSessionSummary(
                            stageWindow,
                            historyEntries,
                            artSnapshot,
                            currentCommoditySerialNumber,
                            remainingHour,
                            remainingMinute,
                            remainingSecond),
                        HasPacketRewardSessionByte = packetOwnedRewardSessionByte,
                        PacketRewardSessionByte = packetRewardSessionByte,
                        PacketPayloadLength = stageWindow.CashOneADayPayloadLength,
                        PacketDecodedByteLength = stageWindow.CashOneADayDecodedByteLength,
                        PacketTrailingByteCount = stageWindow.CashOneADayTrailingByteCount,
                        PacketTrailingPayloadHex = stageWindow.CashOneADayTrailingPayloadHex,
                        PacketStateSignature = BuildCashShopOneADayPacketStateSignature(stageWindow, historyEntries),
                        HistoryEntries = historyEntries,
                        RecentPackets = stageWindow.GetRecentPacketSummaries()
                    };
                });
                oneADayWindow.SetExternalAction(
                    "BtBuy",
                    () => ExecuteCashServiceClientCancelIngress(
                        () =>
                        {
                            string summary = BuildCashShopOneADayCurrentPurchaseSummary();
                            return string.IsNullOrWhiteSpace(summary)
                                ? "CCSWnd_OneADay routed the dedicated today-item buy button through the packet-owned reward lane."
                                : summary;
                        }));
                oneADayWindow.SetExternalAction(
                    "BtItemBox",
                    () => ExecuteCashServiceClientCancelIngress(
                        () =>
                        {
                            string summary = BuildCashShopOneADayItemBoxSummary();
                            return string.IsNullOrWhiteSpace(summary)
                                ? "CCSWnd_OneADay moved owner focus through the dedicated item-box lane."
                                : summary;
                        }));
                oneADayWindow.SetExternalAction(
                    "BtGift",
                    () => ExecuteCashServiceClientCancelIngress(
                        () =>
                        {
                            string summary = BuildCashShopOneADayGiftSummary();
                            return string.IsNullOrWhiteSpace(summary)
                                ? "CCSWnd_OneADay routed the dedicated gift button through the packet-owned reward lane."
                                : summary;
                        }));
                oneADayWindow.SetExternalAction(
                    "BtJoin",
                    () => ExecuteCashServiceClientCancelIngress(
                        () => string.IsNullOrWhiteSpace(oneADayWindow.CurrentOwnerStatusMessage)
                            ? "CCSWnd_OneADay joined the packet-armed reward session preview."
                            : oneADayWindow.CurrentOwnerStatusMessage));
                oneADayWindow.SetExternalAction(
                    "BtShortcut",
                    () => ExecuteCashServiceClientCancelIngress(
                        () => string.IsNullOrWhiteSpace(oneADayWindow.CurrentOwnerStatusMessage)
                            ? "CCSWnd_OneADay switched focus to the shortcut-help plate owner."
                            : oneADayWindow.CurrentOwnerStatusMessage));
                oneADayWindow.SetExternalAction(
                    "BtClose",
                    () => ExecuteCashServiceClientCancelIngress(
                        () => string.IsNullOrWhiteSpace(oneADayWindow.CurrentOwnerStatusMessage)
                            ? "CCSWnd_OneADay dismissed the current reward plate preview."
                            : oneADayWindow.CurrentOwnerStatusMessage));
            }
        }

        private void WireCashShopModalOwnerWindows()
        {
            WireCashShopModalOwnerWindow(MapSimulatorWindowNames.CashCouponDialog, HandleCashCouponDialogButton);
            WireCashShopModalOwnerWindow(MapSimulatorWindowNames.CashPurchaseConfirmDialog, HandleCashPurchaseConfirmDialogButton);
            WireCashShopModalOwnerWindow(MapSimulatorWindowNames.CashReceiveGiftDialog, HandleCashReceiveGiftDialogButton);
            WireCashShopModalOwnerWindow(MapSimulatorWindowNames.CashNameChangeLicenseDialog, HandleCashNameChangeLicenseDialogButton);
            WireCashShopModalOwnerWindow(MapSimulatorWindowNames.CashTransferWorldLicenseDialog, HandleCashTransferWorldLicenseDialogButton);
        }

        private void WireCashShopModalOwnerWindow(string windowName, Action<int> buttonHandler)
        {
            if (uiWindowManager?.GetWindow(windowName) is CashServiceModalOwnerWindow modalWindow)
            {
                modalWindow.SetFont(_fontChat);
                modalWindow.SetButtonHandler(buttonIndex =>
                {
                    ReleaseActiveKeydownSkillForClientCancelIngress(currTickCount);
                    buttonHandler?.Invoke(buttonIndex);
                });
            }
        }

        private static IReadOnlyList<CashShopStageChildWindow.OneADayOwnerState.HistoryEntryState> BuildCashShopOneADayHistoryEntryStates(CashServiceStageWindow stageWindow)
        {
            if (stageWindow?.CashOneADayHistoryEntries == null || stageWindow.CashOneADayHistoryEntries.Count == 0)
            {
                return Array.Empty<CashShopStageChildWindow.OneADayOwnerState.HistoryEntryState>();
            }

            List<CashShopStageChildWindow.OneADayOwnerState.HistoryEntryState> entries = new(stageWindow.CashOneADayHistoryEntries.Count);
            foreach (CashServiceStageWindow.OneADayHistoryEntry entry in stageWindow.CashOneADayHistoryEntries)
            {
                int commoditySerialNumber = Math.Max(0, entry.CommoditySerialNumber);
                entries.Add(new CashShopStageChildWindow.OneADayOwnerState.HistoryEntryState
                {
                    CommoditySerialNumber = commoditySerialNumber,
                    OriginalCommoditySerialNumber = Math.Max(0, entry.OriginalCommoditySerialNumber),
                    ItemLabel = ResolveCashShopOneADayCommodityLabel(commoditySerialNumber),
                    DateLabel = FormatCashShopOneADayDate(entry.RawDate)
                });
            }

            return entries;
        }

        private static IReadOnlyList<CashShopStageChildWindow.OneADayOwnerState.PlateButtonState> BuildCashShopOneADayPlateButtonStates(
            CashShopOneADayArtSnapshot artSnapshot,
            int selectorIndex,
            bool previousLaneEnabled,
            IReadOnlyList<CashShopStageChildWindow.OneADayOwnerState.HistoryEntryState> historyEntries,
            int currentCommoditySerialNumber)
        {
            int clampedSelectorIndex = Math.Clamp(selectorIndex, 0, previousLaneEnabled ? 1 : 0);
            bool usePreviousLane = clampedSelectorIndex == 1 && previousLaneEnabled;
            if (!usePreviousLane)
            {
                bool hasBuyCanvas = artSnapshot?.HasBuyButton == true;
                bool hasGiftCanvas = artSnapshot?.HasGiftButton == true;
                bool hasCurrentReward = currentCommoditySerialNumber > 0;
                return new[]
                {
                    new CashShopStageChildWindow.OneADayOwnerState.PlateButtonState
                    {
                        ButtonId = 0,
                        SlotIndex = 0,
                        CommandKey = "BtBuy",
                        Position = new Microsoft.Xna.Framework.Point(165, 202),
                        Width = artSnapshot?.BuyButtonWidth ?? 0,
                        Height = artSnapshot?.BuyButtonHeight ?? 0,
                        HasCanvas = hasBuyCanvas,
                        IsLoaded = hasBuyCanvas,
                        IsEnabled = hasCurrentReward,
                        IsFocused = true,
                        Label = "Buy"
                    },
                    new CashShopStageChildWindow.OneADayOwnerState.PlateButtonState
                    {
                        ButtonId = 1,
                        SlotIndex = 1,
                        CommandKey = "BtGift",
                        Position = new Microsoft.Xna.Framework.Point(246, 202),
                        Width = artSnapshot?.GiftButtonWidth ?? 0,
                        Height = artSnapshot?.GiftButtonHeight ?? 0,
                        HasCanvas = hasGiftCanvas,
                        IsLoaded = hasGiftCanvas,
                        IsEnabled = hasCurrentReward,
                        IsFocused = false,
                        Label = "Gift"
                    }
                };
            }

            int historyCount = Math.Max(0, historyEntries?.Count ?? 0);
            List<CashShopStageChildWindow.OneADayOwnerState.PlateButtonState> buttons = new(CashShopOneADayHistorySlotCount);
            for (int i = 0; i < CashShopOneADayHistorySlotCount; i++)
            {
                bool hasHistoryEntry = i < historyCount;
                string label = hasHistoryEntry && !string.IsNullOrWhiteSpace(historyEntries[i].ItemLabel)
                    ? historyEntries[i].ItemLabel.Trim()
                    : $"History {i + 1}";
                buttons.Add(new CashShopStageChildWindow.OneADayOwnerState.PlateButtonState
                {
                    ButtonId = 2 + i,
                    SlotIndex = i,
                    CommandKey = "BtItemBox",
                    Position = new Microsoft.Xna.Framework.Point(16 + ((i % 4) * 92), 252 + ((i / 4) * 44)),
                    Width = artSnapshot?.JoinButtonWidth ?? 0,
                    Height = artSnapshot?.JoinButtonHeight ?? 0,
                    HasCanvas = artSnapshot?.HasPlateBigCanvas == true,
                    IsLoaded = hasHistoryEntry,
                    IsEnabled = hasHistoryEntry,
                    IsFocused = i == 0,
                    Label = label
                });
            }

            return buttons;
        }

        private static IReadOnlyList<CashShopStageChildWindow.OneADayOwnerState.SelectorEntryState> BuildCashShopOneADaySelectorEntryStates(
            int activeSelectorIndex,
            string todayLabel,
            string previousLabel,
            bool includePreviousLane)
        {
            int selectorCount = includePreviousLane ? 2 : 1;
            int clampedSelectorIndex = Math.Clamp(activeSelectorIndex, 0, selectorCount - 1);
            if (!includePreviousLane)
            {
                return new[]
                {
                    new CashShopStageChildWindow.OneADayOwnerState.SelectorEntryState
                    {
                        Index = 0,
                        Label = string.IsNullOrWhiteSpace(todayLabel) ? "Today" : todayLabel.Trim(),
                        IsActive = true
                    }
                };
            }

            return new[]
            {
                new CashShopStageChildWindow.OneADayOwnerState.SelectorEntryState
                {
                    Index = 0,
                    Label = string.IsNullOrWhiteSpace(todayLabel) ? "Today" : todayLabel.Trim(),
                    IsActive = clampedSelectorIndex == 0
                },
                new CashShopStageChildWindow.OneADayOwnerState.SelectorEntryState
                {
                    Index = 1,
                    Label = string.IsNullOrWhiteSpace(previousLabel) ? "Previous" : previousLabel.Trim(),
                    IsActive = clampedSelectorIndex == 1
                }
            };
        }

        private static bool IsCashShopOneADayPreviousLaneEnabled(
            bool hasPacketRewardSessionByte,
            int packetRewardSessionByte,
            IReadOnlyList<CashShopStageChildWindow.OneADayOwnerState.HistoryEntryState> historyEntries)
        {
            if (hasPacketRewardSessionByte)
            {
                return (packetRewardSessionByte & 8) != 0;
            }

            return (historyEntries?.Count ?? 0) > 0;
        }

        private static bool ResolveCashShopOneADayPendingState(
            bool hasPacketRewardSessionByte,
            int packetRewardSessionByte,
            bool stagePendingFallback)
        {
            return hasPacketRewardSessionByte
                ? (packetRewardSessionByte & 1) != 0
                : stagePendingFallback;
        }

        private CashTradingRoomWindow.PacketTradeSessionSnapshot BuildCashTradingRoomPacketSessionSnapshot()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShop) is not AdminShopDialogUI cashShopWindow)
            {
                return null;
            }

            AdminShopDialogUI.ListOwnerSnapshot listSnapshot = cashShopWindow.GetListOwnerSnapshot();
            if (listSnapshot == null || listSnapshot.VisibleEntries == null || listSnapshot.VisibleEntries.Count == 0)
            {
                return null;
            }

            AdminShopDialogUI.OwnerEntrySnapshot selectedEntry = listSnapshot.VisibleEntries.FirstOrDefault(entry => entry.IsSelected);
            if (selectedEntry == null)
            {
                int selectedVisibleIndex = listSnapshot.SelectedIndex - listSnapshot.ScrollOffset;
                if (selectedVisibleIndex >= 0 && selectedVisibleIndex < listSnapshot.VisibleEntries.Count)
                {
                    selectedEntry = listSnapshot.VisibleEntries[selectedVisibleIndex];
                }
            }

            selectedEntry ??= listSnapshot.VisibleEntries[0];

            int commoditySerialNumber = Math.Max(0, selectedEntry.CommoditySerialNumber);
            int itemId = Math.Max(0, selectedEntry.RewardItemId);
            int quantity = Math.Max(1, selectedEntry.RewardQuantity);
            bool commodityOnSale = selectedEntry.CommodityOnSale;
            int price = ParseCashServicePriceLabel(selectedEntry.PriceLabel);
            if (price <= 0
                && commoditySerialNumber > 0
                && AdminShopDialogUI.TryResolveCommodityBySerialNumber(commoditySerialNumber, out int resolvedItemId, out long resolvedPrice, out _, out _))
            {
                itemId = resolvedItemId > 0 ? resolvedItemId : itemId;
                price = resolvedPrice > 0 ? (int)Math.Clamp(resolvedPrice, 0L, int.MaxValue) : price;
            }

            string listingTitle = string.IsNullOrWhiteSpace(selectedEntry.Title)
                ? "Selected listing"
                : selectedEntry.Title.Trim();
            string seller = string.IsNullOrWhiteSpace(selectedEntry.Seller)
                ? "CashTrader"
                : selectedEntry.Seller.Trim();
            string paneLabel = string.IsNullOrWhiteSpace(listSnapshot.PaneLabel) ? "Unknown" : listSnapshot.PaneLabel.Trim();
            string listingSignature = $"{commoditySerialNumber}:{itemId}:{quantity}:{price}:{seller}:{selectedEntry.StateLabel}";
            string packetSummary = BuildCashTradingRoomPacketSummary(
                paneLabel,
                listingTitle,
                commoditySerialNumber,
                itemId,
                quantity,
                price,
                seller,
                listingSignature);
            string selectionSignature = BuildCashTradingRoomPacketSelectionSignature(
                paneLabel,
                listSnapshot.BrowseModeLabel,
                listSnapshot.CategoryLabel,
                listSnapshot.SelectedIndex,
                listSnapshot.ScrollOffset,
                commoditySerialNumber,
                itemId,
                quantity,
                price,
                commodityOnSale,
                listingTitle,
                seller,
                selectedEntry.StateLabel,
                selectedEntry.Detail);

            return new CashTradingRoomWindow.PacketTradeSessionSnapshot
            {
                Signature = selectionSignature,
                CommoditySerialNumber = commoditySerialNumber,
                ItemId = itemId,
                Price = price,
                Quantity = quantity,
                CommodityOnSale = commodityOnSale,
                ListingTitle = listingTitle,
                Seller = seller,
                ListingSignature = listingSignature,
                PacketSummary = packetSummary
            };
        }

        internal static string BuildCashTradingRoomPacketSelectionSignature(
            string paneLabel,
            string browseModeLabel,
            string categoryLabel,
            int selectedIndex,
            int scrollOffset,
            int commoditySerialNumber,
            int itemId,
            int quantity,
            int price,
            bool commodityOnSale,
            string listingTitle,
            string seller,
            string stateLabel,
            string detail)
        {
            return string.Join(
                "|",
                paneLabel ?? string.Empty,
                browseModeLabel ?? string.Empty,
                categoryLabel ?? string.Empty,
                selectedIndex.ToString(CultureInfo.InvariantCulture),
                scrollOffset.ToString(CultureInfo.InvariantCulture),
                commoditySerialNumber.ToString(CultureInfo.InvariantCulture),
                itemId.ToString(CultureInfo.InvariantCulture),
                quantity.ToString(CultureInfo.InvariantCulture),
                price.ToString(CultureInfo.InvariantCulture),
                commodityOnSale ? "sale" : "off-sale",
                stateLabel ?? string.Empty,
                listingTitle ?? string.Empty,
                seller ?? string.Empty,
                detail ?? string.Empty);
        }

        internal static string BuildCashTradingRoomPacketSummary(
            string paneLabel,
            string listingTitle,
            int commoditySerialNumber,
            int itemId,
            int quantity,
            int price,
            string seller,
            string listingSignature)
        {
            string quantitySuffix = quantity > 1
                ? $" x{quantity.ToString(CultureInfo.InvariantCulture)}"
                : string.Empty;
            string sellerLabel = string.IsNullOrWhiteSpace(seller) ? "Unknown seller" : seller.Trim();
            string signatureLabel = string.IsNullOrWhiteSpace(listingSignature) ? "none" : listingSignature.Trim();
            return
                $"CCSWnd_List handed {paneLabel} selection {listingTitle} to CCashTradingRoomDlg " +
                $"(SN {commoditySerialNumber.ToString(CultureInfo.InvariantCulture)}, item {itemId.ToString(CultureInfo.InvariantCulture)}{quantitySuffix}, " +
                $"price {price.ToString("N0", CultureInfo.InvariantCulture)} NX, seller {sellerLabel}, sig {signatureLabel}).";
        }

        private static int ParseCashServicePriceLabel(string priceLabel)
        {
            if (string.IsNullOrWhiteSpace(priceLabel))
            {
                return 0;
            }

            StringBuilder digits = new(priceLabel.Length);
            foreach (char ch in priceLabel)
            {
                if (char.IsDigit(ch))
                {
                    digits.Append(ch);
                }
            }

            return int.TryParse(digits.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedPrice)
                ? Math.Max(0, parsedPrice)
                : 0;
        }

        private static IReadOnlyList<CashShopStageChildWindow.OneADayOwnerState.CounterSlotState> BuildCashShopOneADayCounterSlotStates(
            int hour,
            int minute,
            int second,
            int numberCanvasReadyMask)
        {
            string digits = string.Create(
                8,
                (Hour: Math.Max(0, hour), Minute: Math.Max(0, minute), Second: Math.Max(0, second)),
                static (span, state) =>
                {
                    state.Hour.TryFormat(span[..2], out _, "00", CultureInfo.InvariantCulture);
                    span[2] = ':';
                    state.Minute.TryFormat(span.Slice(3, 2), out _, "00", CultureInfo.InvariantCulture);
                    span[5] = ':';
                    state.Second.TryFormat(span.Slice(6, 2), out _, "00", CultureInfo.InvariantCulture);
                });

            List<CashShopStageChildWindow.OneADayOwnerState.CounterSlotState> slots = new(digits.Length);
            for (int i = 0; i < digits.Length; i++)
            {
                char character = digits[i];
                bool isDigit = char.IsDigit(character);
                int digit = isDigit ? character - '0' : -1;
                slots.Add(new CashShopStageChildWindow.OneADayOwnerState.CounterSlotState
                {
                    SlotIndex = i,
                    Digit = character,
                    IsSeparator = !isDigit,
                    HasDigitCanvas = isDigit && digit >= 0 && ((_ = 1 << digit) & numberCanvasReadyMask) != 0
                });
            }

            return slots;
        }

        private static string BuildCashShopOneADayRewardSessionSummary(
            CashServiceStageWindow stageWindow,
            IReadOnlyList<CashShopStageChildWindow.OneADayOwnerState.HistoryEntryState> historyEntries,
            CashShopOneADayArtSnapshot artSnapshot,
            int currentCommoditySerialNumber,
            int remainingHour,
            int remainingMinute,
            int remainingSecond)
        {
            if (stageWindow == null)
            {
                return string.Empty;
            }

            bool pending = ResolveCashShopOneADayPendingState(
                stageWindow.CashOneADayHasPacketRewardSessionByte,
                stageWindow.CashOneADayPacketRewardSessionByte & 0xFF,
                stageWindow.IsOneADayPending);
            int approximatedRewardSessionByte = 0;
            if (pending)
            {
                approximatedRewardSessionByte |= 1;
            }

            if ((historyEntries?.Count ?? 0) > 0)
            {
                approximatedRewardSessionByte |= 8;
            }

            int rewardSessionByte = stageWindow.CashOneADayHasPacketRewardSessionByte
                ? stageWindow.CashOneADayPacketRewardSessionByte & 0xFF
                : approximatedRewardSessionByte;
            string sessionByteSource = stageWindow.CashOneADayHasPacketRewardSessionByte
                ? "packet-owned"
                : "owner-approx";

            string todayState = pending
                ? $"today armed for SN {currentCommoditySerialNumber.ToString(CultureInfo.InvariantCulture)}"
                : "today idle";
            bool previousLaneEnabled = IsCashShopOneADayPreviousLaneEnabled(
                stageWindow.CashOneADayHasPacketRewardSessionByte,
                stageWindow.CashOneADayPacketRewardSessionByte & 0xFF,
                historyEntries);
            string historyState = previousLaneEnabled
                ? $"history {(historyEntries?.Count ?? 0).ToString(CultureInfo.InvariantCulture)}/{CashShopOneADayHistorySlotCount.ToString(CultureInfo.InvariantCulture)} loaded"
                : "previous lane disabled";
            string counterState = $"{remainingHour.ToString("00", CultureInfo.InvariantCulture)}:{remainingMinute.ToString("00", CultureInfo.InvariantCulture)}:{remainingSecond.ToString("00", CultureInfo.InvariantCulture)}";
            string mismatchSuffix = stageWindow.CashOneADayHasPacketRewardSessionByte && rewardSessionByte != approximatedRewardSessionByte
                ? $" (owner-approx 0x{approximatedRewardSessionByte:X2})"
                : string.Empty;
            string trailingState = stageWindow.CashOneADayTrailingByteCount > 0
                ? $"trailing {stageWindow.CashOneADayTrailingByteCount.ToString(CultureInfo.InvariantCulture)}B ({stageWindow.CashOneADayTrailingPayloadHex})"
                : "trailing none";
            return
                $"Reward session {sessionByteSource} 0x{rewardSessionByte:X2}{mismatchSuffix}: {todayState}, {historyState}, counter {counterState}, selector 2 lanes, number canvases {artSnapshot.NumberCanvasCount.ToString(CultureInfo.InvariantCulture)}/10 (mask 0x{artSnapshot.NumberCanvasReadyMask:X3}), payload {stageWindow.CashOneADayPayloadLength.ToString(CultureInfo.InvariantCulture)}B decoded {stageWindow.CashOneADayDecodedByteLength.ToString(CultureInfo.InvariantCulture)}B, {trailingState}.";
        }

        internal static int ResolveCashShopOneADayHistorySlotCount()
        {
            return CashShopOneADayHistorySlotCount;
        }

        private static string BuildCashShopOneADayPacketStateSignature(
            CashServiceStageWindow stageWindow,
            IReadOnlyList<CashShopStageChildWindow.OneADayOwnerState.HistoryEntryState> historyEntries)
        {
            if (stageWindow == null)
            {
                return string.Empty;
            }

            List<string> parts = new()
            {
                ResolveCashShopOneADayPendingState(
                    stageWindow.CashOneADayHasPacketRewardSessionByte,
                    stageWindow.CashOneADayPacketRewardSessionByte & 0xFF,
                    stageWindow.IsOneADayPending) ? "1" : "0",
                stageWindow.CashOneADayItemSerialNumber.ToString(CultureInfo.InvariantCulture),
                stageWindow.CashOneADayItemDate.ToString(CultureInfo.InvariantCulture),
                stageWindow.CashOneADayHasPacketRewardSessionByte ? "packet-byte" : "no-packet-byte",
                stageWindow.CashOneADayPacketRewardSessionByte.ToString(CultureInfo.InvariantCulture),
                stageWindow.CashOneADayPayloadLength.ToString(CultureInfo.InvariantCulture),
                stageWindow.CashOneADayDecodedByteLength.ToString(CultureInfo.InvariantCulture),
                stageWindow.CashOneADayTrailingByteCount.ToString(CultureInfo.InvariantCulture),
                stageWindow.CashOneADayTrailingPayloadHex ?? string.Empty,
                stageWindow.NoticeState ?? string.Empty
            };

            if (historyEntries != null)
            {
                foreach (CashShopStageChildWindow.OneADayOwnerState.HistoryEntryState entry in historyEntries)
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    parts.Add($"{entry.CommoditySerialNumber.ToString(CultureInfo.InvariantCulture)}:{entry.OriginalCommoditySerialNumber.ToString(CultureInfo.InvariantCulture)}:{entry.DateLabel}");
                }
            }

            return string.Join("|", parts);
        }

        private static string ResolveCashShopOneADayCommodityLabel(int commoditySerialNumber)
        {
            if (commoditySerialNumber <= 0)
            {
                return string.Empty;
            }

            if (AdminShopDialogUI.TryResolveCommodityBySerialNumber(commoditySerialNumber, out int itemId, out long price, out int count, out bool onSale)
                && itemId > 0)
            {
                string itemName = InventoryItemMetadataResolver.TryResolveItemName(itemId, out string resolvedName)
                    ? resolvedName
                    : $"Item {itemId.ToString(CultureInfo.InvariantCulture)}";
                string countSuffix = count > 1 ? $" x{count.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
                string saleSuffix = onSale ? string.Empty : " off-sale";
                return $"{itemName}{countSuffix} / SN {commoditySerialNumber.ToString(CultureInfo.InvariantCulture)} / {price.ToString("N0", CultureInfo.InvariantCulture)} NX{saleSuffix}";
            }

            return $"SN {commoditySerialNumber.ToString(CultureInfo.InvariantCulture)}";
        }

        private static string FormatCashShopOneADayDate(int rawDate)
        {
            if (rawDate <= 0)
            {
                return string.Empty;
            }

            string rawText = rawDate.ToString(CultureInfo.InvariantCulture);
            if (rawText.Length == 8
                && DateTime.TryParseExact(rawText, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
            {
                return parsedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            return rawText;
        }

        internal static bool TryResolveCashShopOneADayRemainingTime(
            int rawDate,
            DateTime now,
            out int hour,
            out int minute,
            out int second)
        {
            hour = 0;
            minute = 0;
            second = 0;

            if (rawDate <= 0)
            {
                return false;
            }

            string rawText = rawDate.ToString(CultureInfo.InvariantCulture);
            if (rawText.Length != 8
                || !DateTime.TryParseExact(rawText, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime packetDate))
            {
                return false;
            }

            DateTime resetTime = packetDate.Date.AddDays(1);
            TimeSpan remaining = resetTime - now;
            if (remaining <= TimeSpan.Zero)
            {
                return true;
            }

            int totalSeconds = Math.Min((int)Math.Ceiling(remaining.TotalSeconds), 24 * 60 * 60 - 1);
            hour = totalSeconds / 3600;
            minute = (totalSeconds / 60) % 60;
            second = totalSeconds % 60;
            return true;
        }

        private IReadOnlyList<string> BuildCashShopLockerOwnerLines(AdminShopDialogUI cashShopWindow)
        {
            List<string> lines = new(cashShopWindow?.DescribeLockerOwnerState() ?? Array.Empty<string>());
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) is CashServiceStageWindow stageWindow)
            {
                foreach (CashServiceStageWindow.PacketCatalogEntry entry in stageWindow.CashLockerPacketEntries.Take(2))
                {
                    AppendUniqueLine(lines, entry.Detail);
                    AppendUniqueLine(lines, entry.PacketFieldSummary);
                }

                AppendCashShopStatusLine(lines, stageWindow.CashGiftLastSummary);
                AppendCashShopStatusLine(lines, stageWindow.CashPurchaseRecordSummary);
                AppendCashShopStatusLine(lines, stageWindow.CashItemLastSummary);
                foreach (string recentPacket in stageWindow.GetRecentPacketSummaries(2))
                {
                    AppendUniqueLine(lines, recentPacket);
                }
            }

            return lines;
        }

        private CashShopStageChildWindow.LockerOwnerState BuildCashShopLockerOwnerState(AdminShopDialogUI cashShopWindow)
        {
            AdminShopDialogUI.LockerOwnerSnapshot snapshot = cashShopWindow?.GetLockerOwnerSnapshot() ?? new();
            CashServiceStageWindow stageWindow = uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) as CashServiceStageWindow;
            int scrollBarDownButtonId = ResolveCashLockerScrollBarDownButtonId(_playerManager?.Player?.Build?.Job ?? 0);
            int usedSlotCount = snapshot.UsedSlotCount;
            int slotLimit = snapshot.SlotLimit;
            if (stageWindow != null)
            {
                if (stageWindow.CashLockerItemCount > 0)
                {
                    usedSlotCount = Math.Max(usedSlotCount, stageWindow.CashLockerItemCount);
                }

                if (stageWindow.CashLockerSlotLimit > 0)
                {
                    slotLimit = Math.Max(slotLimit, stageWindow.CashLockerSlotLimit);
                }
            }

            return new CashShopStageChildWindow.LockerOwnerState
            {
                AccountLabel = snapshot.AccountLabel,
                UsedSlotCount = usedSlotCount,
                SlotLimit = slotLimit,
                CanExpand = snapshot.CanExpand || (slotLimit > 0 && usedSlotCount < slotLimit),
                ScrollOffset = 0,
                WheelRange = 208,
                HasNumberFont = true,
                ScrollBarControlId = CashShopLockerScrollBarControlId,
                ScrollBarUpButtonId = 1,
                ScrollBarDownButtonId = scrollBarDownButtonId,
                ScrollBarX = 229,
                ScrollBarY = 29,
                ScrollBarHeight = 67,
                SharedCharacterNames = snapshot.SharedCharacterNames
            };
        }

        private static int ResolveCashLockerScrollBarDownButtonId(int jobId)
        {
            if (jobId / 1000 == 1)
            {
                return 5;
            }

            if (jobId / 100 == 21 || jobId == 2000)
            {
                return 6;
            }

            return jobId / 1000 == 3 ? 9 : 0;
        }

        private IReadOnlyList<string> BuildCashShopInventoryOwnerLines(AdminShopDialogUI cashShopWindow)
        {
            List<string> lines = new(cashShopWindow?.DescribeInventoryOwnerState() ?? Array.Empty<string>());
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) is CashServiceStageWindow stageWindow)
            {
                if (stageWindow.CashInventoryPacketEntries.Count > 0)
                {
                    foreach (CashServiceStageWindow.PacketCatalogEntry entry in stageWindow.CashInventoryPacketEntries.Take(2))
                    {
                        AppendUniqueLine(lines, entry.Detail);
                        AppendUniqueLine(lines, entry.PacketFieldSummary);
                    }
                }
                else
                {
                    AppendCashShopStatusLine(lines, stageWindow.CashItemLastSummary);
                    AppendCashShopStatusLine(lines, stageWindow.CashCouponLastSummary);
                    AppendCashShopStatusLine(lines, stageWindow.CashGiftLastSummary);
                }
            }

            return lines;
        }

        private CashShopStageChildWindow.InventoryOwnerState BuildCashShopInventoryOwnerState(AdminShopDialogUI cashShopWindow)
        {
            AdminShopDialogUI.InventoryOwnerSnapshot snapshot = cashShopWindow?.GetInventoryOwnerSnapshot() ?? new();
            CashServiceStageWindow stageWindow = uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) as CashServiceStageWindow;
            CashInventoryPacketFocusSnapshot packetFocus = ResolveCashInventoryPacketFocus(stageWindow);
            string selectedEntryTitle = snapshot.SelectedEntryTitle;
            if (string.IsNullOrWhiteSpace(selectedEntryTitle))
            {
                selectedEntryTitle = packetFocus?.FocusTitle ?? string.Empty;
            }

            return new CashShopStageChildWindow.InventoryOwnerState
            {
                EquipCount = snapshot.EquipCount,
                UseCount = snapshot.UseCount,
                SetupCount = snapshot.SetupCount,
                EtcCount = snapshot.EtcCount,
                CashCount = snapshot.CashCount,
                FirstPosition = packetFocus?.FirstPosition ?? 1,
                ScrollOffset = packetFocus?.ScrollOffset ?? 0,
                RowFocusIndex = packetFocus?.RowFocusIndex ?? 0,
                WheelRange = 140,
                HasNumberFont = true,
                TabControlId = CashShopInventoryTabControlId,
                ScrollBarControlId = CashShopInventoryScrollBarControlId,
                ScrollBarUpButtonId = 1,
                ScrollBarDownButtonId = 0,
                ScrollBarX = 160,
                ScrollBarY = 54,
                ScrollBarHeight = 102,
                ActiveTabName = packetFocus?.ActiveTabName ?? string.Empty,
                SelectedEntryTitle = selectedEntryTitle,
                PacketFocusSignature = packetFocus?.FocusSignature ?? string.Empty,
                PacketFocusMessage = packetFocus?.FocusMessage ?? string.Empty,
                ButtonControls = BuildCashShopInventoryButtonControlStates()
            };
        }

        internal static IReadOnlyList<CashShopStageChildWindow.InventoryOwnerState.ButtonControlState> BuildCashShopInventoryButtonControlStates()
        {
            return new[]
            {
                new CashShopStageChildWindow.InventoryOwnerState.ButtonControlState
                {
                    ActionKey = "BtExEquip",
                    ControlId = 0,
                    StringPoolUolId = 0xC94,
                    Position = new Microsoft.Xna.Framework.Point(176, 27)
                },
                new CashShopStageChildWindow.InventoryOwnerState.ButtonControlState
                {
                    ActionKey = "BtExConsume",
                    ControlId = 1,
                    StringPoolUolId = 0xC94,
                    Position = new Microsoft.Xna.Framework.Point(176, 54)
                },
                new CashShopStageChildWindow.InventoryOwnerState.ButtonControlState
                {
                    ActionKey = "BtExInstall",
                    ControlId = 2,
                    StringPoolUolId = 0xC94,
                    Position = new Microsoft.Xna.Framework.Point(176, 81)
                },
                new CashShopStageChildWindow.InventoryOwnerState.ButtonControlState
                {
                    ActionKey = "BtExEtc",
                    ControlId = 3,
                    StringPoolUolId = 0xC94,
                    Position = new Microsoft.Xna.Framework.Point(176, 108)
                },
                new CashShopStageChildWindow.InventoryOwnerState.ButtonControlState
                {
                    ActionKey = "BtExTrunk",
                    ControlId = 4,
                    StringPoolUolId = 0,
                    Position = new Microsoft.Xna.Framework.Point(176, 135)
                }
            };
        }

        private static IReadOnlyList<string> BuildCashServiceInventoryOwnerRows(
            IInventoryRuntime inventoryRuntime,
            string tabName,
            int scrollOffset,
            int maxRows)
        {
            if (inventoryRuntime == null || maxRows <= 0)
            {
                return Array.Empty<string>();
            }

            InventoryType inventoryType = tabName switch
            {
                "Use" => InventoryType.USE,
                "Setup" => InventoryType.SETUP,
                "Etc" => InventoryType.ETC,
                _ => InventoryType.EQUIP
            };

            IReadOnlyList<UI.InventorySlotData> slots = inventoryRuntime.GetSlots(inventoryType) ?? Array.Empty<UI.InventorySlotData>();
            if (slots.Count == 0)
            {
                return new[] { $"{tabName} tab has no live inventory rows." };
            }

            List<string> rows = new();
            int clampedOffset = Math.Clamp(scrollOffset, 0, Math.Max(0, slots.Count - 1));
            for (int i = 0; i < maxRows; i++)
            {
                int slotIndex = clampedOffset + i;
                if (slotIndex >= slots.Count)
                {
                    break;
                }

                UI.InventorySlotData slot = slots[slotIndex];
                if (slot == null)
                {
                    rows.Add($"#{slotIndex + 1} Empty row");
                    continue;
                }

                string itemLabel = string.IsNullOrWhiteSpace(slot.ItemName)
                    ? $"Item {slot.ItemId.ToString(CultureInfo.InvariantCulture)}"
                    : slot.ItemName.Trim();
                string quantityLabel = slot.Quantity > 1
                    ? $" x{slot.Quantity.ToString(CultureInfo.InvariantCulture)}"
                    : string.Empty;
                string stateLabel = slot.IsDisabled
                    ? " disabled"
                    : slot.IsCashOwnershipLocked
                        ? " locked"
                        : slot.IsEquipped
                            ? " equipped"
                            : string.Empty;
                rows.Add($"#{slotIndex + 1} {itemLabel}{quantityLabel}{stateLabel}");
            }

            return rows;
        }

        private static CashInventoryPacketFocusSnapshot ResolveCashInventoryPacketFocus(CashServiceStageWindow stageWindow)
        {
            CashServiceStageWindow.PacketCatalogEntry packetEntry = stageWindow?.CashInventoryPacketEntries.FirstOrDefault();
            if (packetEntry == null)
            {
                return null;
            }

            int slotIndex = Math.Max(1, packetEntry.ListingId);
            int zeroBasedSlotIndex = slotIndex - 1;
            int rowFocusIndex = Math.Clamp(zeroBasedSlotIndex % 4, 0, 3);
            int scrollOffset = Math.Max(0, zeroBasedSlotIndex - rowFocusIndex);
            string activeTabName = ResolveCashInventoryTabName(packetEntry.ItemId);
            return new CashInventoryPacketFocusSnapshot
            {
                ActiveTabName = activeTabName,
                FocusTitle = packetEntry.Title ?? string.Empty,
                FocusMessage = packetEntry.Detail ?? string.Empty,
                FocusSignature = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{activeTabName}|{slotIndex}|{packetEntry.ItemId}|{stageWindow.CashInventoryPacketEntries.Count}"),
                FirstPosition = scrollOffset + 1,
                ScrollOffset = scrollOffset,
                RowFocusIndex = rowFocusIndex
            };
        }

        private static string ResolveCashInventoryTabName(int itemId)
        {
            int inventoryTab = Math.Max(0, (itemId / 1_000_000) - 1);
            return inventoryTab switch
            {
                1 => "Use",
                2 => "Setup",
                3 => "Etc",
                _ => "Equip"
            };
        }

        private IReadOnlyList<string> BuildCashShopListOwnerLines(AdminShopDialogUI cashShopWindow)
        {
            List<string> lines = new(cashShopWindow?.DescribeListOwnerState() ?? Array.Empty<string>());
            CashShopListArtSnapshot artSnapshot = ResolveCashShopListArtSnapshot();
            lines.Add(
                $"WZ key-focus canvases: Buy={artSnapshot.HasBuyKeyFocusCanvas}, Gift={artSnapshot.HasGiftKeyFocusCanvas}, Reserve={artSnapshot.HasReserveKeyFocusCanvas}, Remove={artSnapshot.HasRemoveKeyFocusCanvas}.");
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) is CashServiceStageWindow stageWindow)
            {
                if (!string.IsNullOrWhiteSpace(stageWindow.CashPurchaseRecordSummary))
                {
                    AppendUniqueLine(lines, stageWindow.CashPurchaseRecordSummary);
                }

                IReadOnlyList<CashServiceStageWindow.PacketCatalogEntry> stagedEntries =
                    ResolveCashShopListFallbackEntries(stageWindow, out string fallbackPaneLabel, out string _);
                if (stagedEntries.Count > 0)
                {
                    AppendUniqueLine(
                        lines,
                        $"{fallbackPaneLabel} rows {stagedEntries.Count.ToString(CultureInfo.InvariantCulture)} remain owned by the Cash Shop stage.");
                    foreach (CashServiceStageWindow.PacketCatalogEntry entry in stagedEntries.Take(2))
                    {
                        AppendUniqueLine(lines, entry.Detail);
                        AppendUniqueLine(lines, entry.PacketFieldSummary);
                    }
                }

                foreach (string recentPacket in stageWindow.GetRecentPacketSummaries())
                {
                    AppendUniqueLine(lines, recentPacket);
                }
            }

            return lines;
        }

        private CashShopStageChildWindow.ListOwnerState BuildCashShopListOwnerState(AdminShopDialogUI cashShopWindow)
        {
            AdminShopDialogUI.ListOwnerSnapshot snapshot = cashShopWindow?.GetListOwnerSnapshot();
            CashServiceStageWindow stageWindow = uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) as CashServiceStageWindow;
            CashShopListArtSnapshot artSnapshot = ResolveCashShopListArtSnapshot();
            IReadOnlyList<string> recentPackets = stageWindow?.GetRecentPacketSummaries() ?? Array.Empty<string>();
            bool preferPacketOwnedList = ShouldPreferStagePacketOwnedCashList(snapshot, stageWindow);
            if (stageWindow != null
                && (snapshot == null || snapshot.TotalCount <= 0 || preferPacketOwnedList))
            {
                IReadOnlyList<CashServiceStageWindow.PacketCatalogEntry> packetSourceEntries =
                    ResolveCashShopListFallbackEntries(stageWindow, out string packetPaneLabel, out string packetBrowseModeLabel);
                List<CashShopStageChildWindow.ListOwnerEntryState> packetEntries = BuildPacketEntryStates(packetSourceEntries);
                return new CashShopStageChildWindow.ListOwnerState
                {
                    PaneLabel = packetPaneLabel,
                    BrowseModeLabel = packetBrowseModeLabel,
                    CategoryLabel = "CCashShop",
                    FooterMessage = stageWindow.StatusMessage,
                    SelectedEntryDetail = packetEntries.FirstOrDefault()?.Detail ?? string.Empty,
                    SelectedIndex = packetEntries.Count > 0 ? 0 : -1,
                    ScrollOffset = 0,
                    TotalCount = Math.Max(
                        packetEntries.Count,
                        string.Equals(packetBrowseModeLabel, "Wish", StringComparison.OrdinalIgnoreCase)
                            ? stageWindow.WishlistCount
                            : 0),
                    PlateFocusIndex = packetEntries.Count > 0 ? 0 : -1,
                    HasKeyFocusCanvas = artSnapshot.HasAnyKeyFocusCanvas,
                    VisibleEntries = packetEntries,
                    RecentPackets = recentPackets
                };
            }

            if (snapshot == null)
            {
                return new CashShopStageChildWindow.ListOwnerState
                {
                    PaneLabel = "Cash Shop list",
                    BrowseModeLabel = "Unavailable",
                    CategoryLabel = "CCashShop",
                    FooterMessage = "Cash Shop list owner snapshot is unavailable.",
                    SelectedIndex = -1,
                    PlateFocusIndex = -1,
                    HasKeyFocusCanvas = artSnapshot.HasAnyKeyFocusCanvas,
                    RecentPackets = recentPackets
                };
            }

            List<CashShopStageChildWindow.ListOwnerEntryState> entries = new();
            for (int i = 0; i < snapshot.VisibleEntries.Count; i++)
            {
                AdminShopDialogUI.OwnerEntrySnapshot entry = snapshot.VisibleEntries[i];
                entries.Add(new CashShopStageChildWindow.ListOwnerEntryState
                {
                    Title = entry.Title,
                    Detail = entry.Detail,
                    Seller = entry.Seller,
                    PriceLabel = entry.PriceLabel,
                    StateLabel = entry.StateLabel,
                    IsSelected = entry.IsSelected
                });
            }

            return new CashShopStageChildWindow.ListOwnerState
            {
                PaneLabel = snapshot.PaneLabel,
                BrowseModeLabel = snapshot.BrowseModeLabel,
                CategoryLabel = snapshot.CategoryLabel,
                FooterMessage = snapshot.FooterMessage,
                SelectedEntryDetail = snapshot.VisibleEntries.FirstOrDefault(entry => entry.IsSelected)?.Detail ?? string.Empty,
                SelectedIndex = snapshot.SelectedIndex,
                ScrollOffset = snapshot.ScrollOffset,
                TotalCount = snapshot.TotalCount,
                PlateFocusIndex = snapshot.SelectedIndex >= 0 ? snapshot.SelectedIndex - snapshot.ScrollOffset : -1,
                HasKeyFocusCanvas = artSnapshot.HasAnyKeyFocusCanvas,
                VisibleEntries = entries,
                RecentPackets = recentPackets
            };
        }

        private static bool ShouldPreferStagePacketOwnedCashList(AdminShopDialogUI.ListOwnerSnapshot snapshot, CashServiceStageWindow stageWindow)
        {
            if (stageWindow == null)
            {
                return false;
            }

            IReadOnlyList<CashServiceStageWindow.PacketCatalogEntry> packetSourceEntries =
                ResolveCashShopListFallbackEntries(stageWindow, out string packetPaneLabel, out string packetBrowseMode);
            if (packetSourceEntries.Count <= 0)
            {
                return false;
            }

            if (snapshot == null || snapshot.TotalCount <= 0)
            {
                return true;
            }

            if (string.Equals(packetPaneLabel, "Packet wishlist", StringComparison.OrdinalIgnoreCase)
                || string.Equals(packetBrowseMode, "Wish", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static IReadOnlyList<CashServiceStageWindow.PacketCatalogEntry> ResolveCashShopListFallbackEntries(
            CashServiceStageWindow stageWindow,
            out string paneLabel,
            out string browseModeLabel)
        {
            paneLabel = stageWindow?.CashPacketPaneLabel ?? "Packet wishlist";
            browseModeLabel = stageWindow?.CashPacketBrowseModeLabel ?? "Wish";
            if (stageWindow == null)
            {
                return Array.Empty<CashServiceStageWindow.PacketCatalogEntry>();
            }

            if (stageWindow.CashPacketCatalogEntries.Count > 0)
            {
                return stageWindow.CashPacketCatalogEntries;
            }

            if (stageWindow.CashGiftPacketEntries.Count > 0)
            {
                paneLabel = "Packet gifts";
                browseModeLabel = "Gift";
                return stageWindow.CashGiftPacketEntries;
            }

            if (stageWindow.CashInventoryPacketEntries.Count > 0)
            {
                paneLabel = "Packet inventory";
                browseModeLabel = "Inventory";
                return stageWindow.CashInventoryPacketEntries;
            }

            if (stageWindow.CashLockerPacketEntries.Count > 0)
            {
                paneLabel = "Packet locker";
                browseModeLabel = "Locker";
                return stageWindow.CashLockerPacketEntries;
            }

            return Array.Empty<CashServiceStageWindow.PacketCatalogEntry>();
        }

        private static CashShopListArtSnapshot ResolveCashShopListArtSnapshot()
        {
            WzSubProperty listProperty = global::HaCreator.Program.FindImage("ui", "CashShop.img")?["CSList"] as WzSubProperty;
            return new CashShopListArtSnapshot
            {
                HasBuyKeyFocusCanvas = HasCashShopListButtonKeyFocusCanvas(listProperty, "BtBuy"),
                HasGiftKeyFocusCanvas = HasCashShopListButtonKeyFocusCanvas(listProperty, "BtGift"),
                HasReserveKeyFocusCanvas = HasCashShopListButtonKeyFocusCanvas(listProperty, "BtReserve"),
                HasRemoveKeyFocusCanvas = HasCashShopListButtonKeyFocusCanvas(listProperty, "BtRemove")
            };
        }

        private static bool HasCashShopListButtonKeyFocusCanvas(WzSubProperty listProperty, string buttonName)
        {
            WzSubProperty buttonProperty = listProperty?[buttonName] as WzSubProperty;
            WzSubProperty keyFocusedProperty = buttonProperty?["keyFocused"] as WzSubProperty;
            return keyFocusedProperty?["0"] is WzCanvasProperty;
        }

        private IReadOnlyList<string> BuildCashShopStatusOwnerLines()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) is not CashServiceStageWindow stageWindow)
            {
                return new[]
                {
                    "Cash Shop stage owner unavailable.",
                    "CCSWnd_Status is waiting for the parent CCashShop stage."
                };
            }

            string balanceLine =
                $"NX {stageWindow.NexonCashBalance.ToString("N0", CultureInfo.InvariantCulture)}  " +
                $"MP {stageWindow.MaplePointBalance.ToString("N0", CultureInfo.InvariantCulture)}  " +
                $"Prepaid {stageWindow.PrepaidCashBalance.ToString("N0", CultureInfo.InvariantCulture)}";
            if (stageWindow.ChargeParam != 0)
            {
                balanceLine += $"  Charge {stageWindow.ChargeParam.ToString(CultureInfo.InvariantCulture)}";
            }

            List<string> lines = new()
            {
                balanceLine,
                stageWindow.StatusMessage
            };

            AppendCashShopStatusLine(lines, stageWindow.CashCouponLastSummary);
            AppendCashShopStatusLine(lines, stageWindow.CashNameChangeLastSummary);
            AppendCashShopStatusLine(lines, stageWindow.CashTransferWorldLastSummary);
            AppendCashShopStatusLine(lines, stageWindow.CashGachaponLastSummary);
            AppendCashShopStatusLine(lines, stageWindow.CashGiftLastSummary);
            AppendCashShopStatusLine(lines, stageWindow.CashPurchaseRecordSummary);
            AppendCashShopStatusLine(lines, stageWindow.CashItemLastSummary);
            return lines;
        }

        private static void AppendCashShopStatusLine(List<string> lines, string summary)
        {
            if (lines == null || string.IsNullOrWhiteSpace(summary) || lines.Contains(summary, StringComparer.Ordinal))
            {
                return;
            }

            if (summary.StartsWith("No packet-authored", StringComparison.Ordinal))
            {
                return;
            }

            if (summary.StartsWith("No cash-item", StringComparison.Ordinal))
            {
                return;
            }

            lines.Add(summary);
        }

        private static void AppendUniqueLine(List<string> lines, string summary)
        {
            if (lines == null || string.IsNullOrWhiteSpace(summary) || lines.Contains(summary, StringComparer.Ordinal))
            {
                return;
            }

            lines.Add(summary);
        }

        private string BuildCashShopOneADayCurrentPurchaseSummary()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) is not CashServiceStageWindow stageWindow)
            {
                return "CCSWnd_OneADay is waiting for the parent Cash Shop stage.";
            }

            bool pending = ResolveCashShopOneADayPendingState(
                stageWindow.CashOneADayHasPacketRewardSessionByte,
                stageWindow.CashOneADayPacketRewardSessionByte & 0xFF,
                stageWindow.IsOneADayPending);
            int commoditySerialNumber = Math.Max(0, stageWindow.CashOneADayItemSerialNumber);
            string itemLabel = ResolveCashShopOneADayCommodityLabel(commoditySerialNumber);
            if (!pending)
            {
                return $"CCSWnd_OneADay kept the dedicated buy lane idle because no packet-authored today reward is pending for {itemLabel}.";
            }

            return $"CCSWnd_OneADay routed the dedicated buy lane through packet 395 for {itemLabel} (SN {commoditySerialNumber.ToString(CultureInfo.InvariantCulture)}).";
        }

        private string BuildCashShopOneADayItemBoxSummary()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) is not CashServiceStageWindow stageWindow)
            {
                return "CCSWnd_OneADay is waiting for the parent Cash Shop stage.";
            }

            IReadOnlyList<CashShopStageChildWindow.OneADayOwnerState.HistoryEntryState> historyEntries =
                BuildCashShopOneADayHistoryEntryStates(stageWindow);
            bool previousLaneEnabled = IsCashShopOneADayPreviousLaneEnabled(
                stageWindow.CashOneADayHasPacketRewardSessionByte,
                stageWindow.CashOneADayPacketRewardSessionByte & 0xFF,
                historyEntries);
            return previousLaneEnabled
                ? $"CCSWnd_OneADay switched the dedicated item-box lane to the packet-authored previous-reward history ({historyEntries.Count.ToString(CultureInfo.InvariantCulture)} row(s))."
                : "CCSWnd_OneADay kept the item-box lane closed because packet reward-session state disabled previous history.";
        }

        private string BuildCashShopOneADayGiftSummary()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) is not CashServiceStageWindow stageWindow)
            {
                return "CCSWnd_OneADay is waiting for the parent Cash Shop stage.";
            }

            bool pending = ResolveCashShopOneADayPendingState(
                stageWindow.CashOneADayHasPacketRewardSessionByte,
                stageWindow.CashOneADayPacketRewardSessionByte & 0xFF,
                stageWindow.IsOneADayPending);
            int commoditySerialNumber = Math.Max(0, stageWindow.CashOneADayItemSerialNumber);
            string itemLabel = ResolveCashShopOneADayCommodityLabel(commoditySerialNumber);
            if (!pending)
            {
                return $"CCSWnd_OneADay kept the dedicated gift lane idle because no packet-authored today reward is pending for {itemLabel}.";
            }

            return $"CCSWnd_OneADay routed client button 1 through the gift/package lane for {itemLabel} (SN {commoditySerialNumber.ToString(CultureInfo.InvariantCulture)}).";
        }

        private IReadOnlyList<string> BuildCashShopOneADayOwnerLines()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) is not CashServiceStageWindow stageWindow)
            {
                return new[]
                {
                    "One-a-day owner unavailable.",
                    "CCSWnd_OneADay is waiting for the parent Cash Shop stage."
                };
            }

            CashShopOneADayArtSnapshot artSnapshot = ResolveCashShopOneADayArtSnapshot();
            bool pending = ResolveCashShopOneADayPendingState(
                stageWindow.CashOneADayHasPacketRewardSessionByte,
                stageWindow.CashOneADayPacketRewardSessionByte & 0xFF,
                stageWindow.IsOneADayPending);
            List<string> lines = new()
            {
                pending
                    ? "Packet 395 has armed the dedicated one-a-day owner."
                    : "No one-a-day packet is currently pending.",
                stageWindow.NoticeState
            };
            lines.Add(artSnapshot.HasKeyFocusCanvas || artSnapshot.HasPlateBigCanvas || artSnapshot.NumberCanvasCount > 0
                ? $"WZ-backed OneADay art exposes Base01={artSnapshot.HasKeyFocusCanvas}, ItemBox={artSnapshot.HasPlateCanvas}, ItemBoxBig={artSnapshot.HasPlateBigCanvas}, Counter digits={artSnapshot.NumberCanvasCount}, buttons Buy={artSnapshot.HasBuyButton}/Gift={artSnapshot.HasGiftButton}/ItemBox={artSnapshot.HasItemBoxButton}{(artSnapshot.ResolvedFromClientStringPool ? " via client StringPool paths" : string.Empty)}."
                : "The active UI export does not expose OneADay.img/CSOneADay Base01/ItemBox/ItemBoxBig/Counter, so CCSWnd_OneADay keeps those seams explicit in owner state only while PicturePlate remains the visible fallback.");

            foreach (string recentPacket in stageWindow.GetRecentPacketSummaries())
            {
                lines.Add(recentPacket);
            }

            return lines;
        }

        private static CashShopOneADayArtSnapshot ResolveCashShopOneADayArtSnapshot()
        {
            var cashShopImage = global::HaCreator.Program.FindImage("ui", "CashShop.img");
            WzSubProperty picturePlateProperty = cashShopImage?["PicturePlate"] as WzSubProperty;
            WzSubProperty noItemProperty = TryResolveCashShopOneADayUiSubProperty(0x4ED, "OneADay.img", "CSOneADay", "NoItem");
            WzSubProperty keyFocusProperty = TryResolveCashShopOneADayUiSubProperty(0x4EA, "OneADay.img", "CSOneADay", "Base01");
            WzSubProperty plateProperty = TryResolveCashShopOneADayUiSubProperty(0x4E9, "OneADay.img", "CSOneADay", "ItemBox");
            WzSubProperty plateBigProperty = TryResolveCashShopOneADayUiSubProperty(0x16A5, "OneADay.img", "CSOneADay", "ItemBoxBig");
            WzSubProperty counterProperty = TryResolveCashShopOneADayUiSubProperty(0x16A7, "OneADay.img", "CSOneADay", "Counter");
            WzSubProperty buyButtonProperty = TryResolveCashShopOneADayUiSubProperty(0x16A8, "OneADay.img", "CSOneADay", "BtBuy");
            WzSubProperty itemBoxButtonProperty = TryResolveCashShopOneADayUiSubProperty(0x16A9, "OneADay.img", "CSOneADay", "BtItemBox");
            WzSubProperty giftButtonProperty = TryResolveCashShopOneADayUiSubProperty(0x1A75, "OneADay.img", "CSOneADay", "BtGift");
            WzSubProperty joinButtonProperty = picturePlateProperty?["BtJoin"] as WzSubProperty;
            WzSubProperty shortcutButtonProperty = picturePlateProperty?["BtShortcut"] as WzSubProperty;
            int plateCount = 0;
            if (picturePlateProperty?["NoItem"] != null)
            {
                plateCount++;
            }

            if (picturePlateProperty?["NoItem0"] != null)
            {
                plateCount++;
            }

            if (picturePlateProperty?["NoItem1"] != null)
            {
                plateCount++;
            }

            int numberCanvasCount = 0;
            int numberCanvasReadyMask = 0;
            for (int i = 0; i < 10; i++)
            {
                if (counterProperty?[i.ToString(CultureInfo.InvariantCulture)] != null)
                {
                    numberCanvasCount++;
                    numberCanvasReadyMask |= 1 << i;
                }
            }

            return new CashShopOneADayArtSnapshot
            {
                HasNoItemCanvas = noItemProperty != null || picturePlateProperty?["NoItem"] != null,
                HasKeyFocusCanvas = keyFocusProperty != null,
                HasPlateCanvas = plateProperty != null || picturePlateProperty?["NoItem"] != null,
                HasPlateBigCanvas = plateBigProperty != null,
                HasShortcutHelpCanvas = picturePlateProperty?["ShortcutHelp"] != null,
                HasBuyButton = buyButtonProperty != null || picturePlateProperty?["BtJoin"] != null,
                HasGiftButton = giftButtonProperty != null || itemBoxButtonProperty != null || picturePlateProperty?["BtShortcut"] != null,
                HasItemBoxButton = itemBoxButtonProperty != null || picturePlateProperty?["BtShortcut"] != null,
                BuyButtonWidth = ResolveCashShopOneADayButtonWidth(buyButtonProperty, joinButtonProperty),
                BuyButtonHeight = ResolveCashShopOneADayButtonHeight(buyButtonProperty, joinButtonProperty),
                GiftButtonWidth = ResolveCashShopOneADayButtonWidth(giftButtonProperty, itemBoxButtonProperty, shortcutButtonProperty),
                GiftButtonHeight = ResolveCashShopOneADayButtonHeight(giftButtonProperty, itemBoxButtonProperty, shortcutButtonProperty),
                ItemBoxButtonWidth = ResolveCashShopOneADayButtonWidth(itemBoxButtonProperty, shortcutButtonProperty),
                ItemBoxButtonHeight = ResolveCashShopOneADayButtonHeight(itemBoxButtonProperty, shortcutButtonProperty),
                JoinButtonWidth = ResolveCashShopOneADayButtonWidth(joinButtonProperty, buyButtonProperty),
                JoinButtonHeight = ResolveCashShopOneADayButtonHeight(joinButtonProperty, buyButtonProperty),
                NumberCanvasCount = numberCanvasCount,
                NumberCanvasReadyMask = numberCanvasReadyMask,
                PlateCount = plateCount,
                ResolvedFromClientStringPool =
                    noItemProperty != null
                    || keyFocusProperty != null
                    || plateProperty != null
                    || plateBigProperty != null
                    || counterProperty != null
                    || buyButtonProperty != null
                    || giftButtonProperty != null
                    || itemBoxButtonProperty != null
            };
        }

        private static int ResolveCashShopOneADayButtonWidth(params WzSubProperty[] buttonProperties)
        {
            return ResolveCashShopOneADayButtonDimension(buttonProperties, canvas => canvas.PngProperty?.Width ?? 0);
        }

        private static int ResolveCashShopOneADayButtonHeight(params WzSubProperty[] buttonProperties)
        {
            return ResolveCashShopOneADayButtonDimension(buttonProperties, canvas => canvas.PngProperty?.Height ?? 0);
        }

        private static int ResolveCashShopOneADayButtonDimension(
            IEnumerable<WzSubProperty> buttonProperties,
            Func<WzCanvasProperty, int> dimensionSelector)
        {
            if (buttonProperties == null || dimensionSelector == null)
            {
                return 0;
            }

            foreach (WzSubProperty buttonProperty in buttonProperties)
            {
                if (buttonProperty == null)
                {
                    continue;
                }

                if (TryResolveCashShopOneADayButtonCanvas(buttonProperty, out WzCanvasProperty canvas))
                {
                    return Math.Max(0, dimensionSelector(canvas));
                }
            }

            return 0;
        }

        private static bool TryResolveCashShopOneADayButtonCanvas(WzSubProperty buttonProperty, out WzCanvasProperty canvas)
        {
            canvas = null;
            if (buttonProperty == null)
            {
                return false;
            }

            string[] states = { "normal", "mouseOver", "pressed", "disabled", "keyFocused" };
            foreach (string state in states)
            {
                if (buttonProperty[state] is WzSubProperty stateProperty
                    && stateProperty["0"] is WzCanvasProperty stateCanvas)
                {
                    canvas = stateCanvas;
                    return true;
                }
            }

            return false;
        }

        internal static WzSubProperty TryResolveCashShopOneADayUiSubProperty(int stringPoolId, params string[] fallbackPathSegments)
        {
            string path = MapleStoryStringPool.GetOrNull(stringPoolId);
            if (TryResolveUiSubPropertyFromStringPath(path, out WzSubProperty resolvedFromStringPool))
            {
                return resolvedFromStringPool;
            }

            return TryResolveUiSubPropertyFromSegments(fallbackPathSegments);
        }

        internal static bool TryResolveUiSubPropertyFromStringPath(string path, out WzSubProperty property)
        {
            property = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string[] segments = path
                .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(segment => !string.Equals(segment, "UI", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (segments.Length == 0)
            {
                return false;
            }

            property = TryResolveUiSubPropertyFromSegments(segments);
            return property != null;
        }

        private static WzSubProperty TryResolveUiSubPropertyFromSegments(params string[] segments)
        {
            if (segments == null || segments.Length == 0)
            {
                return null;
            }

            string imageName = segments[0];
            var image = global::HaCreator.Program.FindImage("ui", imageName);
            if (image == null)
            {
                return null;
            }

            object current = image;
            for (int i = 1; i < segments.Length; i++)
            {
                string segment = segments[i];
                if (current is WzImage wzImage)
                {
                    current = wzImage[segment];
                }
                else if (current is WzSubProperty wzSubProperty)
                {
                    current = wzSubProperty[segment];
                }
                else
                {
                    return null;
                }
            }

            return current as WzSubProperty;
        }

        private void WireItcChildOwnerWindows()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.MtsStatus) is not CashServiceStageWindow mtsStageWindow)
            {
                return;
            }

            AdminShopDialogUI mtsWindow = uiWindowManager.GetWindow(MapSimulatorWindowNames.Mts) as AdminShopDialogUI;
            IInventoryRuntime inventoryRuntime = uiWindowManager.InventoryWindow as IInventoryRuntime;

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.ItcCharacter) is CashShopStageChildWindow characterWindow)
            {
                characterWindow.SetFont(_fontChat);
                characterWindow.SetContentProvider(mtsStageWindow.DescribeCharacterOwnerState);
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.ItcSale) is CashShopStageChildWindow saleWindow)
            {
                saleWindow.SetFont(_fontChat);
                saleWindow.SetContentProvider(mtsStageWindow.DescribeSaleOwnerState);
                saleWindow.SetExternalAction(
                    "BtShoppingBasket",
                    () => ExecuteCashServiceClientCancelIngress(
                        () => $"CITCWnd_Sale kept shopping-basket ownership in the ITC stage (normal-item mutations: {mtsStageWindow.ItcNormalItemMutationCount.ToString(CultureInfo.InvariantCulture)})."));
                saleWindow.SetExternalAction(
                    "BtBuy",
                    () => ExecuteCashServiceClientCancelIngress(
                        () => $"CITCWnd_Sale staged listing {mtsStageWindow.ItcNormalItemSelectedListingId.ToString(CultureInfo.InvariantCulture)} at {mtsStageWindow.ItcNormalItemSelectedPrice.ToString("N0", CultureInfo.InvariantCulture)} mesos."));
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.ItcPurchase) is CashShopStageChildWindow purchaseWindow)
            {
                purchaseWindow.SetFont(_fontChat);
                purchaseWindow.SetContentProvider(mtsStageWindow.DescribePurchaseOwnerState);
                purchaseWindow.SetExternalAction(
                    "BtRegistration",
                    () => ExecuteCashServiceClientCancelIngress(
                        () => $"CITCWnd_Purchase armed listing registration on category {mtsStageWindow.ItcNormalItemCategory.ToString(CultureInfo.InvariantCulture)}, page {mtsStageWindow.ItcNormalItemPage.ToString(CultureInfo.InvariantCulture)}."));
                purchaseWindow.SetExternalAction(
                    "BtSell",
                    () => ExecuteCashServiceClientCancelIngress(
                        () => "CITCWnd_Purchase switched focus back to the dedicated sale owner."));
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.ItcInventory) is CashShopStageChildWindow inventoryWindow)
            {
                inventoryWindow.SetFont(_fontChat);
                inventoryWindow.SetContentProvider(mtsWindow != null
                    ? mtsWindow.DescribeInventoryOwnerState
                    : mtsStageWindow.DescribeInventoryOwnerState);
                inventoryWindow.SetInventoryVisibleRowProvider((tabName, scrollOffset, maxRows) =>
                    BuildCashServiceInventoryOwnerRows(inventoryRuntime, tabName, scrollOffset, maxRows));
                inventoryWindow.SetInventoryStateProvider(() =>
                {
                    if (mtsWindow == null)
                    {
                        return new CashShopStageChildWindow.InventoryOwnerState
                        {
                            EquipCount = inventoryRuntime?.GetSlots(InventoryType.EQUIP).Count ?? 0,
                            UseCount = inventoryRuntime?.GetSlots(InventoryType.USE).Count ?? 0,
                            SetupCount = inventoryRuntime?.GetSlots(InventoryType.SETUP).Count ?? 0,
                            EtcCount = inventoryRuntime?.GetSlots(InventoryType.ETC).Count ?? 0,
                            CashCount = inventoryRuntime?.GetSlots(InventoryType.CASH).Count ?? 0,
                            ScrollOffset = 0,
                            WheelRange = 158,
                            HasNumberFont = true,
                            SelectedEntryTitle = mtsStageWindow.ItcNormalItemMutationCount > 0
                                ? $"Listing {mtsStageWindow.ItcNormalItemSelectedListingId.ToString(CultureInfo.InvariantCulture)}"
                                : "No staged ITC listing."
                        };
                    }

                    AdminShopDialogUI.InventoryOwnerSnapshot snapshot = mtsWindow.GetInventoryOwnerSnapshot();
                    return new CashShopStageChildWindow.InventoryOwnerState
                    {
                        EquipCount = snapshot.EquipCount,
                        UseCount = snapshot.UseCount,
                        SetupCount = snapshot.SetupCount,
                        EtcCount = snapshot.EtcCount,
                        CashCount = snapshot.CashCount,
                        ScrollOffset = 0,
                        WheelRange = 158,
                        HasNumberFont = true,
                        SelectedEntryTitle = snapshot.SelectedEntryTitle
                    };
                });
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.ItcTab) is CashShopStageChildWindow tabWindow)
            {
                tabWindow.SetFont(_fontChat);
                tabWindow.SetContentProvider(mtsStageWindow.DescribeTabOwnerState);
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.ItcSubTab) is CashShopStageChildWindow subTabWindow)
            {
                subTabWindow.SetFont(_fontChat);
                subTabWindow.SetContentProvider(mtsStageWindow.DescribeSubTabOwnerState);
                subTabWindow.SetExternalAction(
                    "BtSearch",
                    () => ExecuteCashServiceClientCancelIngress(
                        () => $"CITCWnd_SubTab search stayed on category {mtsStageWindow.ItcNormalItemCategory.ToString(CultureInfo.InvariantCulture)}, page {mtsStageWindow.ItcNormalItemPage.ToString(CultureInfo.InvariantCulture)}."));
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.ItcList) is CashShopStageChildWindow listWindow)
            {
                listWindow.SetFont(_fontChat);
                listWindow.SetContentProvider(() => BuildItcListOwnerLines(mtsWindow, mtsStageWindow));
                listWindow.SetExternalAction(
                    "BtBuy",
                    () => ExecuteCashServiceClientCancelIngress(
                        () => $"CITCWnd_List buy staged listing {mtsStageWindow.ItcNormalItemSelectedListingId.ToString(CultureInfo.InvariantCulture)}."));
                listWindow.SetExternalAction(
                    "BtDelete",
                    () => ExecuteCashServiceClientCancelIngress(
                        () => $"CITCWnd_List delete staged listing {mtsStageWindow.ItcNormalItemSelectedListingId.ToString(CultureInfo.InvariantCulture)}."));
                listWindow.SetExternalAction(
                    "BtCancel",
                    () => ExecuteCashServiceClientCancelIngress(
                        () => "CITCWnd_List cancelled the currently staged action."));
                listWindow.SetExternalAction(
                    "BtBuy1",
                    () => ExecuteCashServiceClientCancelIngress(
                        () => $"CITCWnd_List opened alternate buy confirmation for listing {mtsStageWindow.ItcNormalItemSelectedListingId.ToString(CultureInfo.InvariantCulture)}."));
                listWindow.SetListStateProvider(() => BuildItcListOwnerState(mtsWindow, mtsStageWindow));
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.ItcStatus) is CashShopStageChildWindow statusWindow)
            {
                statusWindow.SetFont(_fontChat);
                statusWindow.SetContentProvider(BuildItcStatusOwnerLines);
                statusWindow.SetStatusStateProvider(() => new CashShopStageChildWindow.StatusOwnerState
                {
                    NexonCashBalance = mtsStageWindow.NexonCashBalance,
                    MaplePointBalance = mtsStageWindow.MaplePointBalance,
                    PrepaidCashBalance = mtsStageWindow.PrepaidCashBalance,
                    ChargeParam = mtsStageWindow.ChargeParam,
                    StatusMessage = mtsStageWindow.StatusMessage,
                    DetailSummaries = mtsStageWindow.GetStatusOwnerDetailLines()
                });
                statusWindow.SetExternalAction(
                    "BtCharge",
                    () => ExecuteCashServiceClientCancelIngress(
                        () => "CITCWnd_Status kept charge ownership on the ITC stage."));
                statusWindow.SetExternalAction(
                    "BtCheck",
                    () => ExecuteCashServiceClientCancelIngress(
                        () => $"CITCWnd_Status queried balances after {mtsStageWindow.ItcNormalItemMutationCount.ToString(CultureInfo.InvariantCulture)} normal-item mutation(s)."));
                statusWindow.SetExternalAction(
                    "BtExit",
                    () => ExecuteCashServiceClientCancelIngress(
                        () =>
                        {
                            HideItcOwnerFamilyWindows();
                            return "CITCWnd_Status closed the parent CITC owner family.";
                        }));
            }
        }

        private IReadOnlyList<string> BuildItcListOwnerLines(AdminShopDialogUI mtsWindow, CashServiceStageWindow mtsStageWindow)
        {
            List<string> lines = new(mtsWindow?.DescribeListOwnerState() ?? Array.Empty<string>());
            IReadOnlyList<CashServiceStageWindow.PacketCatalogEntry> stagedEntries =
                (mtsStageWindow?.ItcPacketCatalogEntries.Count ?? 0) > 0
                    ? mtsStageWindow.ItcPacketCatalogEntries
                    : (mtsStageWindow?.ItcWishPacketEntries.Count ?? 0) > 0
                        ? mtsStageWindow.ItcWishPacketEntries
                        : mtsStageWindow?.ItcResultPacketEntries;
            if (mtsStageWindow?.ItcWishPacketEntries.Count > 0)
            {
                lines.Add($"Wish-sale rows {mtsStageWindow.ItcWishPacketEntries.Count.ToString(CultureInfo.InvariantCulture)} remain owned by the ITC stage.");
            }
            else if ((mtsStageWindow?.ItcPacketCatalogEntries.Count ?? 0) > 0)
            {
                lines.Add($"Main-list rows {mtsStageWindow.ItcPacketCatalogEntries.Count.ToString(CultureInfo.InvariantCulture)} remain owned by the ITC stage.");
            }
            else if ((mtsStageWindow?.ItcResultPacketEntries.Count ?? 0) > 0)
            {
                lines.Add($"Fallback result rows {mtsStageWindow.ItcResultPacketEntries.Count.ToString(CultureInfo.InvariantCulture)} remain owned by the ITC stage.");
            }

            if (stagedEntries != null)
            {
                foreach (CashServiceStageWindow.PacketCatalogEntry entry in stagedEntries.Take(2))
                {
                    if (!string.IsNullOrWhiteSpace(entry.Detail))
                    {
                        lines.Add(entry.Detail);
                    }

                    if (!string.IsNullOrWhiteSpace(entry.PacketFieldSummary))
                    {
                        lines.Add(entry.PacketFieldSummary);
                    }
                }
            }

            foreach (string recentPacket in mtsStageWindow?.GetRecentPacketSummaries() ?? Array.Empty<string>())
            {
                lines.Add(recentPacket);
            }

            return lines;
        }

        private CashShopStageChildWindow.ListOwnerState BuildItcListOwnerState(AdminShopDialogUI mtsWindow, CashServiceStageWindow mtsStageWindow)
        {
            AdminShopDialogUI.ListOwnerSnapshot snapshot = mtsWindow?.GetListOwnerSnapshot();
            IReadOnlyList<string> recentPackets = mtsStageWindow?.GetRecentPacketSummaries() ?? Array.Empty<string>();
            bool shouldUsePacketFallback = snapshot == null
                || (snapshot.TotalCount <= 0
                    && ((mtsStageWindow?.ItcPacketCatalogEntries.Count ?? 0) > 0
                        || (mtsStageWindow?.ItcWishPacketEntries.Count ?? 0) > 0
                        || (mtsStageWindow?.ItcResultPacketEntries.Count ?? 0) > 0));
            if (shouldUsePacketFallback)
            {
                IReadOnlyList<CashServiceStageWindow.PacketCatalogEntry> sourceEntries =
                    (mtsStageWindow?.ItcPacketCatalogEntries.Count ?? 0) > 0
                        ? mtsStageWindow.ItcPacketCatalogEntries
                        : (mtsStageWindow?.ItcWishPacketEntries.Count ?? 0) > 0
                            ? mtsStageWindow.ItcWishPacketEntries
                            : mtsStageWindow?.ItcResultPacketEntries;
                List<CashShopStageChildWindow.ListOwnerEntryState> packetEntries = BuildPacketEntryStates(sourceEntries);
                int sortType = mtsStageWindow?.ItcNormalItemSortType ?? 0;
                int category = mtsStageWindow?.ItcNormalItemCategory ?? 0;
                int subCategory = mtsStageWindow?.ItcNormalItemSubCategory ?? 0;
                int totalCount = mtsStageWindow?.ItcCurrentCategoryItemCount ?? 0;
                bool usingWishEntries = (mtsStageWindow?.ItcPacketCatalogEntries.Count ?? 0) <= 0 && (mtsStageWindow?.ItcWishPacketEntries.Count ?? 0) > 0;
                bool usingResultEntries = (mtsStageWindow?.ItcPacketCatalogEntries.Count ?? 0) <= 0
                    && (mtsStageWindow?.ItcWishPacketEntries.Count ?? 0) <= 0
                    && (mtsStageWindow?.ItcResultPacketEntries.Count ?? 0) > 0;
                return new CashShopStageChildWindow.ListOwnerState
                {
                    PaneLabel = usingWishEntries
                        ? "CITC wish-sale list"
                        : usingResultEntries
                            ? "CITC result fallback"
                            : "CITC packet list",
                    BrowseModeLabel = $"Sort {sortType.ToString(CultureInfo.InvariantCulture)}",
                    CategoryLabel = $"Category {category.ToString(CultureInfo.InvariantCulture)}/{subCategory.ToString(CultureInfo.InvariantCulture)}",
                    FooterMessage = mtsStageWindow?.ItcNormalItemLastSummary ?? "CITC packet list unavailable.",
                    SelectedEntryDetail = packetEntries.FirstOrDefault()?.Detail ?? string.Empty,
                    SelectedIndex = packetEntries.Count > 0 ? 0 : -1,
                    ScrollOffset = 0,
                    TotalCount = Math.Max(
                        usingWishEntries
                            ? (mtsStageWindow?.ItcWishPacketEntries.Count ?? 0)
                            : usingResultEntries
                                ? (mtsStageWindow?.ItcResultPacketEntries.Count ?? 0)
                                : totalCount,
                        packetEntries.Count),
                    PlateFocusIndex = packetEntries.Count > 0 ? 0 : -1,
                    HasKeyFocusCanvas = true,
                    VisibleEntries = packetEntries,
                    RecentPackets = recentPackets
                };
            }

            List<CashShopStageChildWindow.ListOwnerEntryState> entries = new();
            for (int i = 0; i < snapshot.VisibleEntries.Count; i++)
            {
                AdminShopDialogUI.OwnerEntrySnapshot entry = snapshot.VisibleEntries[i];
                entries.Add(new CashShopStageChildWindow.ListOwnerEntryState
                {
                    Title = entry.Title,
                    Detail = entry.Detail,
                    Seller = entry.Seller,
                    PriceLabel = entry.PriceLabel,
                    StateLabel = entry.StateLabel,
                    IsSelected = entry.IsSelected
                });
            }

            return new CashShopStageChildWindow.ListOwnerState
            {
                PaneLabel = snapshot.PaneLabel,
                BrowseModeLabel = $"{snapshot.BrowseModeLabel} / Sort {mtsStageWindow.ItcNormalItemSortType.ToString(CultureInfo.InvariantCulture)}",
                CategoryLabel = $"{snapshot.CategoryLabel} / Cat {mtsStageWindow.ItcNormalItemCategory.ToString(CultureInfo.InvariantCulture)}",
                FooterMessage = string.IsNullOrWhiteSpace(snapshot.FooterMessage)
                    ? mtsStageWindow.ItcNormalItemLastSummary
                    : $"{snapshot.FooterMessage} {mtsStageWindow.ItcNormalItemLastSummary}",
                SelectedEntryDetail = snapshot.VisibleEntries.FirstOrDefault(entry => entry.IsSelected)?.Detail ?? string.Empty,
                SelectedIndex = snapshot.SelectedIndex,
                ScrollOffset = snapshot.ScrollOffset,
                TotalCount = snapshot.TotalCount,
                PlateFocusIndex = snapshot.SelectedIndex >= 0 ? snapshot.SelectedIndex - snapshot.ScrollOffset : -1,
                HasKeyFocusCanvas = true,
                VisibleEntries = entries,
                RecentPackets = recentPackets
            };
        }

        private static List<CashShopStageChildWindow.ListOwnerEntryState> BuildPacketEntryStates(IReadOnlyList<CashServiceStageWindow.PacketCatalogEntry> entries)
        {
            List<CashShopStageChildWindow.ListOwnerEntryState> result = new();
            if (entries == null)
            {
                return result;
            }

            for (int i = 0; i < entries.Count && i < 5; i++)
            {
                CashServiceStageWindow.PacketCatalogEntry entry = entries[i];
                result.Add(new CashShopStageChildWindow.ListOwnerEntryState
                {
                    Title = entry.Title,
                    Detail = string.IsNullOrWhiteSpace(entry.PacketFieldSummary)
                        ? entry.Detail
                        : $"{entry.Detail} {entry.PacketFieldSummary}",
                    Seller = entry.Seller,
                    PriceLabel = entry.PriceLabel,
                    StateLabel = entry.StateLabel,
                    IsSelected = i == 0
                });
            }

            return result;
        }

        private IReadOnlyList<string> BuildItcStatusOwnerLines()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.MtsStatus) is not CashServiceStageWindow stageWindow)
            {
                return new[]
                {
                    "ITC stage owner unavailable.",
                    "CITCWnd_Status is waiting for the parent CITC stage."
                };
            }

            string balanceLine =
                $"NX {stageWindow.NexonCashBalance.ToString("N0", CultureInfo.InvariantCulture)}  " +
                $"MP {stageWindow.MaplePointBalance.ToString("N0", CultureInfo.InvariantCulture)}";
            if (stageWindow.ChargeParam != 0)
            {
                balanceLine += $"  Charge {stageWindow.ChargeParam.ToString(CultureInfo.InvariantCulture)}";
            }

            List<string> lines = new()
            {
                balanceLine,
                stageWindow.StatusMessage
            };

            AppendUniqueLine(lines, stageWindow.ItcNormalItemLastSummary);
            if (stageWindow.ItcPacketCatalogEntries.Count > 0)
            {
                AppendUniqueLine(
                    lines,
                    $"Main-list rows {stageWindow.ItcPacketCatalogEntries.Count.ToString(CultureInfo.InvariantCulture)} remain owned by the ITC stage.");
                AppendUniqueLine(lines, stageWindow.ItcPacketCatalogEntries[0].Detail);
            }

            if (stageWindow.ItcWishPacketEntries.Count > 0)
            {
                AppendUniqueLine(
                    lines,
                    $"Wish-sale rows {stageWindow.ItcWishPacketEntries.Count.ToString(CultureInfo.InvariantCulture)} remain owned by the ITC stage.");
                AppendUniqueLine(lines, stageWindow.ItcWishPacketEntries[0].Detail);
            }

            if (stageWindow.ItcPurchasePacketEntries.Count > 0)
            {
                AppendUniqueLine(
                    lines,
                    $"Purchase rows {stageWindow.ItcPurchasePacketEntries.Count.ToString(CultureInfo.InvariantCulture)} remain owned by the ITC stage.");
                AppendUniqueLine(lines, stageWindow.ItcPurchasePacketEntries[0].Detail);
            }

            if (stageWindow.ItcResultPacketEntries.Count > 0)
            {
                AppendUniqueLine(lines, stageWindow.ItcResultPacketEntries[0].Detail);
            }

            foreach (string recentPacket in stageWindow.GetRecentPacketSummaries(2))
            {
                AppendUniqueLine(lines, recentPacket);
            }

            return lines;
        }

        private void HideCashShopOwnerFamilyWindows()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShop) is AdminShopDialogUI cashShopWindow)
            {
                if (cashShopWindow.ShouldRecordPacketOwnedAdminShopFamilyHide)
                {
                    cashShopWindow.RecordPacketOwnedAdminShopOwnerSurfaceHidden(
                        "CAdminShopDlg owner surface is hidden because the Cash Shop owner family is not visible.",
                        AdminShopPacketOwnedOwnerVisibilityState.HiddenByCashShopFamily);
                }
            }

            uiWindowManager?.HideWindow(MapSimulatorWindowNames.CashShop);
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.CashAvatarPreview);
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.CashShopStage);
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.CashTradingRoom);
            for (int i = 0; i < CashShopChildOwnerWindowNames.Length; i++)
            {
                uiWindowManager?.HideWindow(CashShopChildOwnerWindowNames[i]);
            }

            HideCashShopModalOwnerWindows();
            RefreshCashServiceStageBgmOverride();
        }

        private void HideCashShopModalOwnerWindows()
        {
            for (int i = 0; i < CashShopModalOwnerWindowNames.Length; i++)
            {
                uiWindowManager?.HideWindow(CashShopModalOwnerWindowNames[i]);
            }

            ClearCashReceiveGiftFollowUpNoticeState();
        }

        private void ShowCashShopChildOwnerWindows()
        {
            for (int i = 0; i < CashShopChildOwnerWindowNames.Length; i++)
            {
                ShowDirectionModeOwnedWindow(CashShopChildOwnerWindowNames[i]);
            }
        }

        private void HideItcOwnerFamilyWindows()
        {
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.Mts);
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.MtsStatus);
            for (int i = 0; i < ItcChildOwnerWindowNames.Length; i++)
            {
                uiWindowManager?.HideWindow(ItcChildOwnerWindowNames[i]);
            }

            RefreshCashServiceStageBgmOverride();
        }

        private void ShowItcChildOwnerWindows()
        {
            for (int i = 0; i < ItcChildOwnerWindowNames.Length; i++)
            {
                ShowDirectionModeOwnedWindow(ItcChildOwnerWindowNames[i]);
            }
        }

        private void ShowItcWindow()
        {
            OpenCashServiceOwnerFamily(CashServiceOwnerStageKind.ItemTradingCenter, resetStageSession: true);
        }

        private void OpenCashServiceOwnerFamily(CashServiceOwnerStageKind stageKind, bool resetStageSession)
        {
            if (stageKind == CashServiceOwnerStageKind.CashShop)
            {
                string shopRestrictionMessage = GetFieldWindowRestrictionMessage(MapSimulatorWindowNames.CashShop);
                if (!string.IsNullOrWhiteSpace(shopRestrictionMessage))
                {
                    ShowFieldRestrictionMessage(shopRestrictionMessage);
                    return;
                }
            }

            SyncCashShopAccountCredit();
            WireCashServiceOwnerWindows();
            if (stageKind == CashServiceOwnerStageKind.CashShop)
            {
                HideItcOwnerFamilyWindows();
                ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.CashShopStage);
                ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.CashShop);
                ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.CashAvatarPreview);
                ShowCashShopChildOwnerWindows();
                ApplyCashServiceStageBgmOverride();
                SyncCashServiceStageWindowState(MapSimulatorWindowNames.CashShopStage, stageKind, resetStageSession);
                return;
            }

            HideCashShopOwnerFamilyWindows();
            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.MtsStatus);
            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.Mts);
            ShowItcChildOwnerWindows();
            ApplyCashServiceStageBgmOverride();
            SyncCashServiceStageWindowState(MapSimulatorWindowNames.MtsStatus, stageKind, resetStageSession);
        }

        private void ApplyCashServiceStageBgmOverride()
        {
            RequestSpecialFieldBgmOverride(CashServiceStageBgmPath);
        }

        private void RefreshCashServiceStageBgmOverride()
        {
            if (uiWindowManager == null)
            {
                ClearSpecialFieldBgmOverride();
                return;
            }

            bool cashServiceVisible =
                uiWindowManager.GetWindow(MapSimulatorWindowNames.CashShopStage)?.IsVisible == true
                || uiWindowManager.GetWindow(MapSimulatorWindowNames.CashShop)?.IsVisible == true
                || uiWindowManager.GetWindow(MapSimulatorWindowNames.CashAvatarPreview)?.IsVisible == true
                || uiWindowManager.GetWindow(MapSimulatorWindowNames.MtsStatus)?.IsVisible == true
                || uiWindowManager.GetWindow(MapSimulatorWindowNames.Mts)?.IsVisible == true;
            if (!cashServiceVisible)
            {
                for (int i = 0; i < CashShopChildOwnerWindowNames.Length && !cashServiceVisible; i++)
                {
                    cashServiceVisible = uiWindowManager.GetWindow(CashShopChildOwnerWindowNames[i])?.IsVisible == true;
                }
            }

            if (!cashServiceVisible)
            {
                for (int i = 0; i < CashShopModalOwnerWindowNames.Length && !cashServiceVisible; i++)
                {
                    cashServiceVisible = uiWindowManager.GetWindow(CashShopModalOwnerWindowNames[i])?.IsVisible == true;
                }
            }

            if (!cashServiceVisible)
            {
                for (int i = 0; i < ItcChildOwnerWindowNames.Length && !cashServiceVisible; i++)
                {
                    cashServiceVisible = uiWindowManager.GetWindow(ItcChildOwnerWindowNames[i])?.IsVisible == true;
                }
            }

            if (cashServiceVisible)
            {
                ApplyCashServiceStageBgmOverride();
            }
            else
            {
                ClearSpecialFieldBgmOverride();
            }
        }

        private void SyncCashServiceStageWindowState(string windowName, CashServiceOwnerStageKind stageKind, bool resetStageSession)
        {
            if (uiWindowManager?.GetWindow(windowName) is not CashServiceStageWindow stageWindow)
            {
                return;
            }

            stageWindow.SetFont(_fontChat);
            stageWindow.SetCharacterBuild(_playerManager?.Player?.Build);
            stageWindow.SetInventory(uiWindowManager.InventoryWindow as IInventoryRuntime);
            stageWindow.SetStorageRuntime(uiWindowManager.GetWindow(MapSimulatorWindowNames.Trunk) as IStorageRuntime);

            if (resetStageSession)
            {
                int pendingCommoditySerialNumber = stageKind == CashServiceOwnerStageKind.CashShop
                    ? ResolveStorageExpansionCommoditySerialNumber()
                    : 0;
                stageWindow.BeginStageSession(
                    _playerManager?.Player?.Build,
                    ResolveCurrentCashServiceMesoBalance(),
                    currTickCount,
                    pendingCommoditySerialNumber);
                return;
            }

            stageWindow.PrepareStageOpen(currTickCount);
        }

        private long ResolveCurrentCashServiceMesoBalance()
        {
            return (uiWindowManager?.InventoryWindow as IInventoryRuntime)?.GetMesoCount() ?? 0L;
        }

        private bool TryFocusCashServiceCommodity(int commoditySerialNumber)
        {
            if (commoditySerialNumber <= 0)
            {
                return false;
            }

            bool focused = false;
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) is CashServiceStageWindow cashShopStageWindow)
            {
                focused |= cashShopStageWindow.TryFocusCommoditySerialNumber(commoditySerialNumber);
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShop) is AdminShopDialogUI cashShopWindow)
            {
                focused |= cashShopWindow.TryFocusCommoditySerialNumber(commoditySerialNumber);
            }

            return focused;
        }

        private void EnsureCashServicePacketInboxState(bool shouldRun)
        {
            if (!shouldRun)
            {
                if (_cashServicePacketInbox.IsRunning)
                {
                    _cashServicePacketInbox.Stop();
                }

                return;
            }

            if (_cashServicePacketInbox.IsRunning
                && _cashServicePacketInbox.Port == _cashServicePacketInboxConfiguredPort)
            {
                return;
            }

            if (_cashServicePacketInbox.IsRunning)
            {
                _cashServicePacketInbox.Stop();
            }

            try
            {
                _cashServicePacketInbox.Start(_cashServicePacketInboxConfiguredPort);
            }
            catch (Exception ex)
            {
                _cashServicePacketInbox.Stop();
                _chat?.AddErrorMessage($"Cash-service packet inbox failed to start: {ex.Message}", currTickCount);
            }
        }

        private string DescribeCashServiceStatus()
        {
            return $"{DescribeCashServicePacketInboxStatus()}{Environment.NewLine}{DescribeCashServiceOfficialSessionBridgeStatus()}";
        }

        private string DescribeCashServicePacketInboxStatus()
        {
            string ingressEnabledText = _cashServicePacketInbox.IsRunning ? "enabled" : "disabled";
            string listeningText = _cashServicePacketInbox.IsRunning
                ? $"listening on 127.0.0.1:{_cashServicePacketInbox.Port}"
                : $"configured for 127.0.0.1:{_cashServicePacketInboxConfiguredPort}";
            string modeText = _cashServicePacketInboxCommandOverrideEnabled.HasValue
                ? (_cashServicePacketInboxCommandOverrideEnabled.Value ? "command-forced on" : "command-forced off")
                : "auto";
            return $"Cash-service packet inbox {ingressEnabledText}, {listeningText}, mode={modeText}, received {_cashServicePacketInbox.ReceivedCount} packet(s) [proxy={_cashServicePacketInbox.ProxyIngressReceivedCount}, local={_cashServicePacketInbox.LocalIngressReceivedCount}], last ingress={_cashServicePacketInbox.LastIngressMode}. {_cashServicePacketInbox.LastStatus}";
        }

        private string DescribeCashServiceOfficialSessionBridgeStatus()
        {
            string cashShopEnabledText = _cashShopOfficialSessionBridgeEnabled ? "enabled" : "disabled";
            string cashShopModeText = _cashShopOfficialSessionBridgeUseDiscovery ? "auto-discovery" : "direct proxy";
            string cashShopTargetText = _cashShopOfficialSessionBridgeUseDiscovery
                ? _cashShopOfficialSessionBridgeConfiguredLocalPort.HasValue
                    ? $"discover remote port {_cashShopOfficialSessionBridgeConfiguredRemotePort} with local port {_cashShopOfficialSessionBridgeConfiguredLocalPort.Value}"
                    : $"discover remote port {_cashShopOfficialSessionBridgeConfiguredRemotePort}"
                : $"{_cashShopOfficialSessionBridgeConfiguredRemoteHost}:{_cashShopOfficialSessionBridgeConfiguredRemotePort}";
            string cashShopListeningText = _cashShopOfficialSessionBridge.IsRunning
                ? $"listening on 127.0.0.1:{_cashShopOfficialSessionBridge.ListenPort}"
                : $"configured for 127.0.0.1:{_cashShopOfficialSessionBridgeConfiguredListenPort}";
            string cashShopProcessText = string.IsNullOrWhiteSpace(_cashShopOfficialSessionBridgeConfiguredProcessSelector)
                ? string.Empty
                : $" for {_cashShopOfficialSessionBridgeConfiguredProcessSelector}";

            string mtsEnabledText = _mtsOfficialSessionBridgeEnabled ? "enabled" : "disabled";
            string mtsModeText = _mtsOfficialSessionBridgeUseDiscovery ? "auto-discovery" : "direct proxy";
            string mtsTargetText = _mtsOfficialSessionBridgeUseDiscovery
                ? _mtsOfficialSessionBridgeConfiguredLocalPort.HasValue
                    ? $"discover remote port {_mtsOfficialSessionBridgeConfiguredRemotePort} with local port {_mtsOfficialSessionBridgeConfiguredLocalPort.Value}"
                    : $"discover remote port {_mtsOfficialSessionBridgeConfiguredRemotePort}"
                : $"{_mtsOfficialSessionBridgeConfiguredRemoteHost}:{_mtsOfficialSessionBridgeConfiguredRemotePort}";
            string mtsListeningText = _mtsOfficialSessionBridge.IsRunning
                ? $"listening on 127.0.0.1:{_mtsOfficialSessionBridge.ListenPort}"
                : $"configured for 127.0.0.1:{_mtsOfficialSessionBridgeConfiguredListenPort}";
            string mtsProcessText = string.IsNullOrWhiteSpace(_mtsOfficialSessionBridgeConfiguredProcessSelector)
                ? string.Empty
                : $" for {_mtsOfficialSessionBridgeConfiguredProcessSelector}";

            return $"CashShop bridge {cashShopEnabledText}, {cashShopModeText}, {cashShopListeningText}, target {cashShopTargetText}{cashShopProcessText}. {_cashShopOfficialSessionBridge.DescribeStatus()}{Environment.NewLine}MTS bridge {mtsEnabledText}, {mtsModeText}, {mtsListeningText}, target {mtsTargetText}{mtsProcessText}. {_mtsOfficialSessionBridge.DescribeStatus()}";
        }

        private void EnsureCashServiceOfficialSessionBridgeState(bool shouldRun)
        {
            EnsureCashServiceOfficialSessionBridgeState(
                _cashShopOfficialSessionBridge,
                shouldRun,
                ref _cashShopOfficialSessionBridgeEnabled,
                _cashShopOfficialSessionBridgeUseDiscovery,
                ref _cashShopOfficialSessionBridgeConfiguredListenPort,
                CashServiceOfficialSessionBridgeManager.CashShopDefaultListenPort,
                _cashShopOfficialSessionBridgeConfiguredRemoteHost,
                _cashShopOfficialSessionBridgeConfiguredRemotePort,
                _cashShopOfficialSessionBridgeConfiguredProcessSelector,
                _cashShopOfficialSessionBridgeConfiguredLocalPort);

            EnsureCashServiceOfficialSessionBridgeState(
                _mtsOfficialSessionBridge,
                shouldRun,
                ref _mtsOfficialSessionBridgeEnabled,
                _mtsOfficialSessionBridgeUseDiscovery,
                ref _mtsOfficialSessionBridgeConfiguredListenPort,
                CashServiceOfficialSessionBridgeManager.MtsDefaultListenPort,
                _mtsOfficialSessionBridgeConfiguredRemoteHost,
                _mtsOfficialSessionBridgeConfiguredRemotePort,
                _mtsOfficialSessionBridgeConfiguredProcessSelector,
                _mtsOfficialSessionBridgeConfiguredLocalPort);
        }

        private static void EnsureCashServiceOfficialSessionBridgeState(
            CashServiceOfficialSessionBridgeManager manager,
            bool shouldRun,
            ref bool enabled,
            bool useDiscovery,
            ref int configuredListenPort,
            int defaultListenPort,
            string configuredRemoteHost,
            int configuredRemotePort,
            string configuredProcessSelector,
            int? configuredLocalPort)
        {
            if (!shouldRun || !enabled)
            {
                if (manager.IsRunning)
                {
                    manager.Stop();
                }

                return;
            }

            if (configuredListenPort <= 0 || configuredListenPort > ushort.MaxValue)
            {
                if (manager.IsRunning)
                {
                    manager.Stop();
                }

                enabled = false;
                configuredListenPort = defaultListenPort;
                return;
            }

            if (useDiscovery)
            {
                if (configuredRemotePort <= 0 || configuredRemotePort > ushort.MaxValue)
                {
                    if (manager.IsRunning)
                    {
                        manager.Stop();
                    }

                    return;
                }

                manager.TryStartFromDiscovery(
                    configuredListenPort,
                    configuredRemotePort,
                    configuredProcessSelector,
                    configuredLocalPort,
                    out _);
                return;
            }

            if (configuredRemotePort <= 0
                || configuredRemotePort > ushort.MaxValue
                || string.IsNullOrWhiteSpace(configuredRemoteHost))
            {
                if (manager.IsRunning)
                {
                    manager.Stop();
                }

                return;
            }

            if (manager.IsRunning
                && manager.ListenPort == configuredListenPort
                && string.Equals(manager.RemoteHost, configuredRemoteHost, StringComparison.OrdinalIgnoreCase)
                && manager.RemotePort == configuredRemotePort)
            {
                return;
            }

            if (manager.IsRunning)
            {
                manager.Stop();
            }

            manager.Start(configuredListenPort, configuredRemoteHost, configuredRemotePort);
        }

        private void RefreshCashServiceOfficialSessionBridgeDiscovery(int currentTickCount)
        {
            if (currentTickCount < _nextCashServiceOfficialSessionBridgeDiscoveryRefreshAt)
            {
                return;
            }

            _nextCashServiceOfficialSessionBridgeDiscoveryRefreshAt =
                currentTickCount + CashServiceOfficialSessionBridgeDiscoveryRefreshIntervalMs;

            RefreshCashServiceOfficialSessionBridgeDiscovery(
                _cashShopOfficialSessionBridge,
                _cashShopOfficialSessionBridgeEnabled,
                _cashShopOfficialSessionBridgeUseDiscovery,
                _cashShopOfficialSessionBridgeConfiguredListenPort,
                _cashShopOfficialSessionBridgeConfiguredRemotePort,
                _cashShopOfficialSessionBridgeConfiguredProcessSelector,
                _cashShopOfficialSessionBridgeConfiguredLocalPort);

            RefreshCashServiceOfficialSessionBridgeDiscovery(
                _mtsOfficialSessionBridge,
                _mtsOfficialSessionBridgeEnabled,
                _mtsOfficialSessionBridgeUseDiscovery,
                _mtsOfficialSessionBridgeConfiguredListenPort,
                _mtsOfficialSessionBridgeConfiguredRemotePort,
                _mtsOfficialSessionBridgeConfiguredProcessSelector,
                _mtsOfficialSessionBridgeConfiguredLocalPort);
        }

        private static void RefreshCashServiceOfficialSessionBridgeDiscovery(
            CashServiceOfficialSessionBridgeManager manager,
            bool enabled,
            bool useDiscovery,
            int configuredListenPort,
            int configuredRemotePort,
            string configuredProcessSelector,
            int? configuredLocalPort)
        {
            if (!enabled
                || !useDiscovery
                || configuredRemotePort <= 0
                || configuredRemotePort > ushort.MaxValue
                || manager.HasAttachedClient)
            {
                return;
            }

            manager.TryStartFromDiscovery(
                configuredListenPort,
                configuredRemotePort,
                configuredProcessSelector,
                configuredLocalPort,
                out _);
        }

        private ChatCommandHandler.CommandResult HandleCashServiceCommand(string[] args)
        {
            if (args == null || args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribeCashServiceStatus());
            }

            switch (args[0].ToLowerInvariant())
            {
                case "open":
                    if (args.Length < 2)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /cashservice open <cashshop|mts>");
                    }

                    if (string.Equals(args[1], "cashshop", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(args[1], "cs", StringComparison.OrdinalIgnoreCase))
                    {
                        OpenCashServiceOwnerFamily(CashServiceOwnerStageKind.CashShop, resetStageSession: true);
                        return ChatCommandHandler.CommandResult.Ok(DescribeCashServiceStatus());
                    }

                    if (string.Equals(args[1], "mts", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(args[1], "itc", StringComparison.OrdinalIgnoreCase))
                    {
                        OpenCashServiceOwnerFamily(CashServiceOwnerStageKind.ItemTradingCenter, resetStageSession: true);
                        return ChatCommandHandler.CommandResult.Ok(DescribeCashServiceStatus());
                    }

                    return ChatCommandHandler.CommandResult.Error("Usage: /cashservice open <cashshop|mts>");

                case "inbox":
                    return HandleCashServiceInboxCommand(args.Skip(1).ToArray());

                case "packet":
                case "packetraw":
                    return HandleCashServiceInboxPacketCommand(args);

                case "packetclientraw":
                    return HandleCashServiceInboxClientPacketRawCommand(args);

                case "bridge":
                case "session":
                    return HandleCashServiceBridgeCommand(args.Skip(1).ToArray());

                default:
                    return ChatCommandHandler.CommandResult.Error("Usage: /cashservice [status|open <cashshop|mts>|inbox [status|start [port]|stop|auto|packet <type> [payloadhex=..|payloadb64=..|hex|codec-text]|packetraw <type> <hex>|packetclientraw <hex>]|packet <type> [payloadhex=..|payloadb64=..|hex|codec-text]|packetraw <type> <hex>|packetclientraw <hex>|bridge [status|cashshop|mts <status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop>]]");
            }
        }

        private ChatCommandHandler.CommandResult HandleCashServiceInboxCommand(string[] args)
        {
            if (args == null || args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribeCashServicePacketInboxStatus());
            }

            if (string.Equals(args[0], "start", StringComparison.OrdinalIgnoreCase))
            {
                int port = CashServicePacketInboxManager.DefaultPort;
                if (args.Length > 1
                    && (!int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out port)
                        || port <= 0
                        || port > ushort.MaxValue))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /cashservice inbox start [port]");
                }

                _cashServicePacketInboxConfiguredPort = port;
                _cashServicePacketInboxCommandOverrideEnabled = true;
                EnsureCashServicePacketInboxState(shouldRun: true);
                return ChatCommandHandler.CommandResult.Ok(DescribeCashServicePacketInboxStatus());
            }

            if (string.Equals(args[0], "stop", StringComparison.OrdinalIgnoreCase))
            {
                _cashServicePacketInboxCommandOverrideEnabled = false;
                EnsureCashServicePacketInboxState(shouldRun: false);
                return ChatCommandHandler.CommandResult.Ok(DescribeCashServicePacketInboxStatus());
            }

            if (string.Equals(args[0], "auto", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "reset", StringComparison.OrdinalIgnoreCase))
            {
                _cashServicePacketInboxCommandOverrideEnabled = null;
                return ChatCommandHandler.CommandResult.Ok(DescribeCashServicePacketInboxStatus());
            }

            if (string.Equals(args[0], "packet", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase))
            {
                return HandleCashServiceInboxPacketCommand(args);
            }

            if (string.Equals(args[0], "packetclientraw", StringComparison.OrdinalIgnoreCase))
            {
                return HandleCashServiceInboxClientPacketRawCommand(args);
            }

            return ChatCommandHandler.CommandResult.Error("Usage: /cashservice inbox [status|start [port]|stop|auto|packet <type> [payloadhex=..|payloadb64=..|hex|codec-text]|packetraw <type> <hex>|packetclientraw <hex>]");
        }

        private ChatCommandHandler.CommandResult HandleCashServiceBridgeCommand(string[] args)
        {
            if (args == null || args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribeCashServiceOfficialSessionBridgeStatus());
            }

            bool isCashShopBridge;
            if (string.Equals(args[0], "cashshop", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "cs", StringComparison.OrdinalIgnoreCase))
            {
                isCashShopBridge = true;
            }
            else if (string.Equals(args[0], "mts", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "itc", StringComparison.OrdinalIgnoreCase))
            {
                isCashShopBridge = false;
            }
            else
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /cashservice bridge [status|cashshop|mts <status|discover <remotePort> [processName|pid] [localPort]|history [count]|clearhistory|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop>]");
            }

            return HandleCashServiceBridgeRoleCommand(isCashShopBridge, args.Skip(1).ToArray());
        }

        private ChatCommandHandler.CommandResult HandleCashServiceBridgeRoleCommand(bool isCashShopBridge, string[] args)
        {
            if (args == null || args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribeCashServiceOfficialSessionBridgeStatus());
            }

            CashServiceOfficialSessionBridgeManager manager = isCashShopBridge ? _cashShopOfficialSessionBridge : _mtsOfficialSessionBridge;
            string usagePrefix = isCashShopBridge
                ? "/cashservice bridge cashshop"
                : "/cashservice bridge mts";

            if (string.Equals(args[0], "discover", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2
                    || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int discoverRemotePort)
                    || discoverRemotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error($"Usage: {usagePrefix} discover <remotePort> [processName|pid] [localPort]");
                }

                string processSelector = args.Length >= 3 ? args[2] : null;
                int? localPortFilter = null;
                if (args.Length >= 4)
                {
                    if (!int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedLocalPort)
                        || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error($"Usage: {usagePrefix} discover <remotePort> [processName|pid] [localPort]");
                    }

                    localPortFilter = parsedLocalPort;
                }

                return ChatCommandHandler.CommandResult.Info(
                    manager.DescribeDiscoveredSessions(discoverRemotePort, processSelector, localPortFilter));
            }

            if (string.Equals(args[0], "history", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "recent", StringComparison.OrdinalIgnoreCase))
            {
                int maxCount = 10;
                if (args.Length >= 2
                    && (!int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out maxCount)
                        || maxCount <= 0))
                {
                    return ChatCommandHandler.CommandResult.Error($"Usage: {usagePrefix} history [count]");
                }

                return ChatCommandHandler.CommandResult.Info(manager.DescribeRecentPackets(maxCount));
            }

            if (string.Equals(args[0], "clearhistory", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "clearrecent", StringComparison.OrdinalIgnoreCase))
            {
                manager.ClearRecentPackets();
                return ChatCommandHandler.CommandResult.Ok(manager.DescribeStatus());
            }

            if (string.Equals(args[0], "start", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 4
                    || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int listenPort)
                    || listenPort <= 0
                    || !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int remotePort)
                    || remotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error($"Usage: {usagePrefix} start <listenPort> <serverHost> <serverPort>");
                }

                if (isCashShopBridge)
                {
                    _cashShopOfficialSessionBridgeEnabled = true;
                    _cashShopOfficialSessionBridgeUseDiscovery = false;
                    _cashShopOfficialSessionBridgeConfiguredListenPort = listenPort;
                    _cashShopOfficialSessionBridgeConfiguredRemoteHost = args[2];
                    _cashShopOfficialSessionBridgeConfiguredRemotePort = remotePort;
                    _cashShopOfficialSessionBridgeConfiguredProcessSelector = null;
                    _cashShopOfficialSessionBridgeConfiguredLocalPort = null;
                }
                else
                {
                    _mtsOfficialSessionBridgeEnabled = true;
                    _mtsOfficialSessionBridgeUseDiscovery = false;
                    _mtsOfficialSessionBridgeConfiguredListenPort = listenPort;
                    _mtsOfficialSessionBridgeConfiguredRemoteHost = args[2];
                    _mtsOfficialSessionBridgeConfiguredRemotePort = remotePort;
                    _mtsOfficialSessionBridgeConfiguredProcessSelector = null;
                    _mtsOfficialSessionBridgeConfiguredLocalPort = null;
                }

                EnsureCashServiceOfficialSessionBridgeState(shouldRun: true);
                return ChatCommandHandler.CommandResult.Ok(DescribeCashServiceOfficialSessionBridgeStatus());
            }

            if (string.Equals(args[0], "startauto", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "auto", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3
                    || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int autoListenPort)
                    || autoListenPort <= 0
                    || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int autoRemotePort)
                    || autoRemotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error($"Usage: {usagePrefix} startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                }

                string processSelector = args.Length >= 4 ? args[3] : null;
                int? localPortFilter = null;
                if (args.Length >= 5)
                {
                    if (!int.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedLocalPort)
                        || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error($"Usage: {usagePrefix} startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                    }

                    localPortFilter = parsedLocalPort;
                }

                if (isCashShopBridge)
                {
                    _cashShopOfficialSessionBridgeEnabled = true;
                    _cashShopOfficialSessionBridgeUseDiscovery = true;
                    _cashShopOfficialSessionBridgeConfiguredListenPort = autoListenPort;
                    _cashShopOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
                    _cashShopOfficialSessionBridgeConfiguredRemotePort = autoRemotePort;
                    _cashShopOfficialSessionBridgeConfiguredProcessSelector = processSelector;
                    _cashShopOfficialSessionBridgeConfiguredLocalPort = localPortFilter;
                }
                else
                {
                    _mtsOfficialSessionBridgeEnabled = true;
                    _mtsOfficialSessionBridgeUseDiscovery = true;
                    _mtsOfficialSessionBridgeConfiguredListenPort = autoListenPort;
                    _mtsOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
                    _mtsOfficialSessionBridgeConfiguredRemotePort = autoRemotePort;
                    _mtsOfficialSessionBridgeConfiguredProcessSelector = processSelector;
                    _mtsOfficialSessionBridgeConfiguredLocalPort = localPortFilter;
                }

                EnsureCashServiceOfficialSessionBridgeState(shouldRun: true);
                return manager.TryStartFromDiscovery(autoListenPort, autoRemotePort, processSelector, localPortFilter, out string startStatus)
                    ? ChatCommandHandler.CommandResult.Ok($"{startStatus}{Environment.NewLine}{DescribeCashServiceOfficialSessionBridgeStatus()}")
                    : ChatCommandHandler.CommandResult.Error(startStatus);
            }

            if (string.Equals(args[0], "stop", StringComparison.OrdinalIgnoreCase))
            {
                if (isCashShopBridge)
                {
                    _cashShopOfficialSessionBridgeEnabled = false;
                    _cashShopOfficialSessionBridgeUseDiscovery = false;
                    _cashShopOfficialSessionBridgeConfiguredRemotePort = 0;
                    _cashShopOfficialSessionBridgeConfiguredProcessSelector = null;
                    _cashShopOfficialSessionBridgeConfiguredLocalPort = null;
                    _cashShopOfficialSessionBridge.Stop();
                }
                else
                {
                    _mtsOfficialSessionBridgeEnabled = false;
                    _mtsOfficialSessionBridgeUseDiscovery = false;
                    _mtsOfficialSessionBridgeConfiguredRemotePort = 0;
                    _mtsOfficialSessionBridgeConfiguredProcessSelector = null;
                    _mtsOfficialSessionBridgeConfiguredLocalPort = null;
                    _mtsOfficialSessionBridge.Stop();
                }

                return ChatCommandHandler.CommandResult.Ok(DescribeCashServiceOfficialSessionBridgeStatus());
            }

            return ChatCommandHandler.CommandResult.Error($"{usagePrefix} <status|discover <remotePort> [processName|pid] [localPort]|history [count]|clearhistory|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop>");
        }

        private ChatCommandHandler.CommandResult HandleCashServiceInboxPacketCommand(string[] args)
        {
            bool rawHex = string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase);
            if (rawHex)
            {
                if (args.Length < 3
                    || !CashServicePacketInboxManager.TryParsePacketType(args[1], out int rawPacketType)
                    || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(2)), out byte[] rawPayload))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /cashservice packetraw <type> <hex>");
                }

                return TryApplyCashServiceInboxPacket(rawPacketType, rawPayload, "cashservice-packetraw", out string rawMessage)
                    ? ChatCommandHandler.CommandResult.Ok(rawMessage)
                    : ChatCommandHandler.CommandResult.Error(rawMessage);
            }

            string parseError = null;
            if (args.Length < 2
                || !CashServicePacketInboxManager.TryParseLine(string.Join(" ", args.Skip(1)), out CashServicePacketInboxMessage parsedMessage, out parseError))
            {
                return ChatCommandHandler.CommandResult.Error(parseError ?? "Usage: /cashservice packet <type> [payloadhex=..|payloadb64=..|hex|codec-text]");
            }

            return TryApplyCashServiceInboxPacket(parsedMessage.PacketType, parsedMessage.Payload, "cashservice-packet", out string message)
                ? ChatCommandHandler.CommandResult.Ok(message)
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private ChatCommandHandler.CommandResult HandleCashServiceInboxClientPacketRawCommand(string[] args)
        {
            if (args.Length < 2 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(1)), out byte[] rawPacket))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /cashservice packetclientraw <hex>");
            }

            if (!TryDecodeCashServiceClientOpcodePacket(rawPacket, out int packetType, out byte[] payload, out string decodeError))
            {
                return ChatCommandHandler.CommandResult.Error(decodeError ?? "Usage: /cashservice packetclientraw <hex>");
            }

            return TryApplyCashServiceInboxPacket(packetType, payload, "cashservice-clientraw", out string message)
                ? ChatCommandHandler.CommandResult.Ok($"Applied cash-service client opcode {packetType}. {message}")
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private bool TryApplyCashServiceInboxPacket(int packetType, byte[] payload, string source, out string message)
        {
            payload ??= Array.Empty<byte>();
            _cashServicePacketInbox.EnqueueLocal(packetType, payload, source);
            if (!_cashServicePacketInbox.TryDequeue(out CashServicePacketInboxMessage queuedMessage) || queuedMessage == null)
            {
                message = "Cash-service packet inbox did not retain the injected packet.";
                return false;
            }

            bool applied = TryApplyCashServiceStagePacket(queuedMessage.PacketType, queuedMessage.Payload, out message);
            _cashServicePacketInbox.RecordDispatchResult(queuedMessage, applied, message);
            return applied;
        }

        private static bool TryDecodeCashServiceClientOpcodePacket(byte[] rawPacket, out int packetType, out byte[] payload, out string error)
        {
            packetType = 0;
            payload = Array.Empty<byte>();
            error = null;

            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                error = "Cash-service client packet must include a 2-byte opcode.";
                return false;
            }

            packetType = BitConverter.ToUInt16(rawPacket, 0);
            if (!CashServicePacketInboxManager.TryParsePacketType(packetType.ToString(CultureInfo.InvariantCulture), out _))
            {
                error = $"Unsupported cash-service client opcode {packetType}.";
                return false;
            }

            payload = rawPacket.Length == sizeof(ushort)
                ? Array.Empty<byte>()
                : rawPacket[sizeof(ushort)..];
            return true;
        }

        private void DrainCashServicePacketInbox()
        {
            DrainCashServiceOfficialSessionBridges();

            while (_cashServicePacketInbox.TryDequeue(out CashServicePacketInboxMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                bool applied = TryApplyCashServiceStagePacket(message.PacketType, message.Payload, out string detail);
                _cashServicePacketInbox.RecordDispatchResult(message, applied, detail);
                if (string.IsNullOrWhiteSpace(detail))
                {
                    continue;
                }

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

        private void DrainCashServiceOfficialSessionBridges()
        {
            while (_cashShopOfficialSessionBridge.TryDequeue(out CashServicePacketInboxMessage cashShopMessage))
            {
                _cashServicePacketInbox.EnqueueProxy(
                    cashShopMessage.PacketType,
                    cashShopMessage.Payload,
                    cashShopMessage.Source);
                _cashShopOfficialSessionBridge.RecordDispatchResult(cashShopMessage, success: true, detail: "forwarded to cash-service inbox");
            }

            while (_mtsOfficialSessionBridge.TryDequeue(out CashServicePacketInboxMessage mtsMessage))
            {
                _cashServicePacketInbox.EnqueueProxy(
                    mtsMessage.PacketType,
                    mtsMessage.Payload,
                    mtsMessage.Source);
                _mtsOfficialSessionBridge.RecordDispatchResult(mtsMessage, success: true, detail: "forwarded to cash-service inbox");
            }
        }

        private bool TryApplyCashServiceStagePacket(int packetType, byte[] payload, out string message)
        {
            Managers.CashServiceStageKind stageKind = Managers.CashServiceStageRuntime.GetStageKindForPacket(packetType);
            if (stageKind == Managers.CashServiceStageKind.None)
            {
                message = $"Unsupported cash-service packet type {packetType}.";
                return false;
            }

            if (stageKind == Managers.CashServiceStageKind.CashShop)
            {
                OpenCashServiceOwnerFamily(CashServiceOwnerStageKind.CashShop, resetStageSession: false);
                if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) is not CashServiceStageWindow cashShopStageWindow)
                {
                    message = "Cash Shop stage owner is not available in this UI build.";
                    return false;
                }

                bool applied = cashShopStageWindow.TryApplyPacket(packetType, payload, currTickCount, out message);
                bool storageApplied = TryApplyCashShopStorageExpansionPacketResult(packetType, payload, out string storageMessage);
                bool balanceApplied = TryApplyCashShopBalancePacket(packetType, payload, out string balanceMessage);
                if (packetType == 384)
                {
                    TryFocusCashServiceCommodity(_lastPacketOwnedCommoditySerialNumber);
                }

                if (applied)
                {
                    TryOpenCashShopModalOwnerForPacket(cashShopStageWindow, packetType);
                    TryPlayCashGachaponOwnerAnimation(cashShopStageWindow, currTickCount);
                }

                message = CombineCashServicePacketMessages(message, storageMessage, balanceMessage);
                return applied || storageApplied || balanceApplied;
            }

            OpenCashServiceOwnerFamily(CashServiceOwnerStageKind.ItemTradingCenter, resetStageSession: false);
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.MtsStatus) is not CashServiceStageWindow mtsStageWindow)
            {
                message = "ITC stage owner is not available in this UI build.";
                return false;
            }

            return mtsStageWindow.TryApplyPacket(packetType, payload, currTickCount, out message);
        }

        private void TryOpenCashShopModalOwnerForPacket(CashServiceStageWindow stageWindow, int packetType)
        {
            if (stageWindow == null)
            {
                return;
            }

            if (packetType == 388 && stageWindow.CashNameChangePossibleResult.OpensLicenseDialog)
            {
                ShowCashNameChangeLicenseDialog(stageWindow);
                return;
            }

            if (packetType == 390 && stageWindow.CashTransferWorldPossibleResult.OpensLicenseDialog)
            {
                ShowCashTransferWorldLicenseDialog(stageWindow);
                return;
            }

            if (packetType != 384)
            {
                return;
            }

            if (stageWindow.TryFinalizeReceiveGiftAcceptResult(
                    stageWindow.CashItemResultSubtype,
                    out string receiveGiftSummary,
                    out string receiveGiftOwnerNotice,
                    out bool receiveGiftAccepted,
                    out int receiveGiftNextIndex))
            {
                if (receiveGiftAccepted)
                {
                    ShowCashReceiveGiftFollowUpNoticeDialog(
                        stageWindow,
                        receiveGiftNextIndex,
                        receiveGiftOwnerNotice,
                        receiveGiftSummary);
                }
                else
                {
                    string message = string.Join(
                        " ",
                        new[] { receiveGiftSummary, receiveGiftOwnerNotice }
                            .Where(part => !string.IsNullOrWhiteSpace(part)));
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        _chat?.AddErrorMessage(message, currTickCount);
                    }
                }
            }

            if (stageWindow.CashItemResultSubtype == 90 && stageWindow.CashGiftPacketEntries.Count > 0)
            {
                ShowCashReceiveGiftDialog(stageWindow, 0);
            }
        }

        private void TryPlayCashGachaponOwnerAnimation(CashServiceStageWindow stageWindow, int currentTimeMs)
        {
            if (stageWindow == null || uiWindowManager == null)
            {
                return;
            }

            int animationSequence = stageWindow.CashGachaponAnimationSequence;
            if (animationSequence < _lastPlayedCashGachaponAnimationSequence)
            {
                _lastPlayedCashGachaponAnimationSequence = 0;
            }

            if (animationSequence <= 0 || animationSequence == _lastPlayedCashGachaponAnimationSequence)
            {
                return;
            }

            _lastPlayedCashGachaponAnimationSequence = animationSequence;
            bool animationRegistered = uiWindowManager.ProductionEnhancementAnimationDisplayer.PlayCashGachaponResult(
                stageWindow.CashGachaponAnimationIsCopyResult,
                stageWindow.CashGachaponAnimationIsJackpot,
                currentTimeMs);
            stageWindow.SetCashGachaponAnimationOwnerPlaybackSummary(
                stageWindow.CashGachaponAnimationIsCopyResult,
                stageWindow.CashGachaponAnimationIsJackpot,
                animationRegistered);
        }

        private string ShowCashCouponDialog()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) is not CashServiceStageWindow stageWindow)
            {
                return "CCouponUseSelectDlg is waiting for the parent CCashShop stage.";
            }

            if (!TryGetCashServiceModalOwnerWindow(MapSimulatorWindowNames.CashCouponDialog, out CashServiceModalOwnerWindow modalWindow))
            {
                return "CCouponUseSelectDlg owner is not registered in this UI build.";
            }

            modalWindow.Configure(
                "CCouponUseSelectDlg",
                "Enter the coupon code for the Cash Shop status owner.",
                new[]
                {
                    stageWindow.CashCouponLastSummary,
                    "WZ: UIWindow2.img/Coupon/backgrnd (218x158).",
                    "Client evidence: control 1001 at (12,53,200,15) with focus on create and a 32-character limit."
                },
                new[]
                {
                    new CashServiceModalOwnerWindow.ActionButtonState { Label = "OK", IsPrimary = true },
                    new CashServiceModalOwnerWindow.ActionButtonState { Label = "Cancel" }
                },
                inputPlaceholder: "Coupon code",
                inputActive: true,
                inputMaxLength: 32);
            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.CashCouponDialog);
            return "CCouponUseSelectDlg opened from CCSWnd_Status::BtCoupon.";
        }

        private string ShowCashPurchaseConfirmDialog(AdminShopDialogUI cashShopWindow)
        {
            AdminShopDialogUI.ListOwnerSnapshot listSnapshot = cashShopWindow?.GetListOwnerSnapshot();
            AdminShopDialogUI.OwnerEntrySnapshot selectedEntry = listSnapshot?.VisibleEntries?.FirstOrDefault(entry => entry.IsSelected);
            if (selectedEntry == null)
            {
                string fallbackMessage = cashShopWindow?.ExecuteCashStageListAction("BtBuy")
                    ?? "CConfirmPurchaseDlg is waiting for the Cash Shop list owner.";
                return fallbackMessage;
            }

            if (!TryGetCashServiceModalOwnerWindow(MapSimulatorWindowNames.CashPurchaseConfirmDialog, out CashServiceModalOwnerWindow modalWindow))
            {
                return cashShopWindow.ExecuteCashStageListAction("BtBuy");
            }

            CashServiceStageWindow stageWindow = uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) as CashServiceStageWindow;
            IReadOnlyList<AdminShopDialogUI.CommodityPurchaseVariantSnapshot> purchaseVariants =
                selectedEntry.CommoditySerialNumber > 0
                    ? AdminShopDialogUI.GetCommodityPurchaseVariants(selectedEntry.CommoditySerialNumber)
                    : Array.Empty<AdminShopDialogUI.CommodityPurchaseVariantSnapshot>();
            int preferredVariantSerialNumber = ResolveCashPurchasePreferredVariantSerialNumber(
                selectedEntry,
                purchaseVariants,
                stageWindow?.CashPurchaseDialogSelectedVariantSerialNumber ?? 0);
            modalWindow.Configure(
                "CConfirmPurchaseDlg",
                $"Confirm purchase for {selectedEntry.Title}.",
                BuildCashPurchaseConfirmDialogLines(listSnapshot, selectedEntry, stageWindow, purchaseVariants),
                new[]
                {
                    new CashServiceModalOwnerWindow.ActionButtonState
                    {
                        Label = "OK",
                        IsPrimary = true,
                        ClientX = 157,
                        ClientYFromBottom = 37,
                        ClientWidth = 48,
                        ClientHeight = 20
                    },
                    new CashServiceModalOwnerWindow.ActionButtonState
                    {
                        Label = "Cancel",
                        ClientX = 207,
                        ClientYFromBottom = 37,
                        ClientWidth = 48,
                        ClientHeight = 20
                    }
                },
                footer: "Client evidence: CConfirmPurchaseDlg::OnCreate creates Maple Point (1000) at height-95, Prepaid Cash (1001) at height-80, Nexon Cash (1002) at height-65, combo 1003 at (62,42,150,18) for multi-packed commodities, OK at (157,height-37), and Cancel at (207,height-37).",
                checkBoxes: BuildCashPurchasePaymentSelectorStates(stageWindow, selectedEntry, purchaseVariants, preferredVariantSerialNumber),
                comboBox: BuildCashPurchaseVariantComboBoxState(selectedEntry, purchaseVariants, preferredVariantSerialNumber));
            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.CashPurchaseConfirmDialog);
            return $"CConfirmPurchaseDlg opened for {selectedEntry.Title}.";
        }

        private void ShowCashReceiveGiftDialog(CashServiceStageWindow stageWindow, int giftIndex)
        {
            if (stageWindow == null
                || giftIndex < 0
                || giftIndex >= stageWindow.CashGiftPacketEntries.Count
                || !TryGetCashServiceModalOwnerWindow(MapSimulatorWindowNames.CashReceiveGiftDialog, out CashServiceModalOwnerWindow modalWindow))
            {
                return;
            }

            ClearCashReceiveGiftFollowUpNoticeState();
            CashServiceStageWindow.PacketCatalogEntry entry = stageWindow.CashGiftPacketEntries[giftIndex];
            int totalGiftCount = stageWindow.CashGiftPacketEntries.Count;
            string sender = string.IsNullOrWhiteSpace(entry.Seller) ? "Unknown sender" : entry.Seller;
            string rawSenderLine = !string.IsNullOrWhiteSpace(entry.PacketSenderRaw)
                && !string.Equals(entry.PacketSenderRaw.Trim(), sender, StringComparison.Ordinal)
                ? $"GW_GiftList sFrom raw: {entry.PacketSenderRaw}"
                : string.Empty;
            string rawMessageLine = !string.IsNullOrWhiteSpace(entry.PacketMessageRaw)
                && !string.Equals(entry.PacketMessageRaw.Trim(), entry.PacketMessage, StringComparison.Ordinal)
                ? $"GW_GiftList sText raw: {entry.PacketMessageRaw}"
                : string.Empty;
            string senderShapeLine = entry.PacketSenderByteLength > 0 && !string.IsNullOrWhiteSpace(entry.PacketSenderRawHex)
                ? $"GW_GiftList sFrom[{entry.PacketSenderByteLength.ToString(CultureInfo.InvariantCulture)}] raw hex: {entry.PacketSenderRawHex}"
                : string.Empty;
            string messageShapeLine = entry.PacketMessageByteLength > 0 && !string.IsNullOrWhiteSpace(entry.PacketMessageRawHex)
                ? $"GW_GiftList sText[{entry.PacketMessageByteLength.ToString(CultureInfo.InvariantCulture)}] raw hex: {entry.PacketMessageRawHex}"
                : string.Empty;
            modalWindow.Configure(
                "CUIReceiveGift",
                $"Review gift row {(giftIndex + 1).ToString(CultureInfo.InvariantCulture)} of {totalGiftCount.ToString(CultureInfo.InvariantCulture)} before the next CDialog::DoModal pass.",
                new[]
                {
                    $"{entry.Title} | {sender}",
                    entry.Detail,
                    entry.PacketFieldSummary,
                    rawSenderLine,
                    rawMessageLine,
                    senderShapeLine,
                    messageShapeLine,
                    string.IsNullOrWhiteSpace(entry.PriceLabel) ? string.Empty : entry.PriceLabel,
                    string.IsNullOrWhiteSpace(entry.StateLabel) ? string.Empty : entry.StateLabel,
                    "Client evidence: OnCashItemResLoadGiftDone allocates one CUIReceiveGift per decoded GW_GiftList row and advances after each modal closes."
                },
                new[]
                {
                    new CashServiceModalOwnerWindow.ActionButtonState
                    {
                        Label = "OK",
                        IsPrimary = true,
                        ClientX = 203,
                        ClientY = 220
                    }
                },
                footer: totalGiftCount > 1
                    ? $"{Math.Max(0, totalGiftCount - giftIndex - 1).ToString(CultureInfo.InvariantCulture)} later gift row(s) remain in this packet-owned modal sequence after this CDialog::DoModal return."
                    : "This is the last staged gift row in the current packet-owned modal sequence.",
                inputPlaceholder: "Reply message",
                inputActive: true,
                inputMaxLength: 200,
                inputClientX: 25,
                inputClientY: 174,
                inputClientWidth: 210,
                inputClientHeight: 13,
                giftRows: BuildCashReceiveGiftQueueLabels(stageWindow.CashGiftPacketEntries),
                selectedGiftIndex: giftIndex,
                giftRowsSelectable: true);
            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.CashReceiveGiftDialog);
        }

        private void ShowCashReceiveGiftFollowUpNoticeDialog(
            CashServiceStageWindow stageWindow,
            int nextGiftIndex,
            string ownerNotice,
            string acceptanceSummary)
        {
            if (!TryGetCashServiceModalOwnerWindow(MapSimulatorWindowNames.CashReceiveGiftDialog, out CashServiceModalOwnerWindow modalWindow))
            {
                return;
            }

            int remainingRows = Math.Max(0, stageWindow?.CashGiftPacketEntries.Count ?? 0);
            bool hasNextGiftRow = stageWindow != null
                && nextGiftIndex >= 0
                && nextGiftIndex < stageWindow.CashGiftPacketEntries.Count;
            modalWindow.Configure(
                "CUIReceiveGift",
                "The current GW_GiftList row returned from CDialog::DoModal and staged its follow-up owner notice.",
                new[]
                {
                    ownerNotice,
                    acceptanceSummary,
                    "Client evidence: OnCashItemResLoadGiftDone sends opcode 154 and shows StringPool[0xAC0] notice immediately after each CDialog::DoModal return value 1.",
                    $"Decoded queue still has {remainingRows.ToString(CultureInfo.InvariantCulture)} row(s) after this accept branch."
                },
                new[]
                {
                    new CashServiceModalOwnerWindow.ActionButtonState
                    {
                        Label = "OK",
                        IsPrimary = true,
                        ClientX = 203,
                        ClientY = 220
                    }
                },
                footer: hasNextGiftRow
                    ? "Follow-up notice acknowledged: the next packet-owned gift row will open on OK."
                    : "Follow-up notice acknowledged: no additional packet-owned gift rows remain.");
            _cashReceiveGiftFollowUpNoticePending = true;
            _cashReceiveGiftFollowUpNoticeNextIndex = nextGiftIndex;
            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.CashReceiveGiftDialog);
        }

        private void ClearCashReceiveGiftFollowUpNoticeState()
        {
            _cashReceiveGiftFollowUpNoticePending = false;
            _cashReceiveGiftFollowUpNoticeNextIndex = -1;
        }

        private void ShowCashNameChangeLicenseDialog(CashServiceStageWindow stageWindow)
        {
            if (!TryGetCashServiceModalOwnerWindow(MapSimulatorWindowNames.CashNameChangeLicenseDialog, out CashServiceModalOwnerWindow modalWindow))
            {
                return;
            }

            modalWindow.Configure(
                "CUIChangingLicenseNotice",
                "Confirm the name-change license notice before returning to the Cash Shop stage.",
                new[]
                {
                    $"Request id: {stageWindow.CashNameChangePossibleResult.RequestId.ToString(CultureInfo.InvariantCulture)}.",
                    $"Birth date payload: {stageWindow.CashNameChangePossibleResult.BirthDate.ToString(CultureInfo.InvariantCulture)}.",
                    stageWindow.CashNameChangeLastSummary,
                    "WZ: CashShop.img/CSChangeName/Base/backgrndnotice."
                },
                new[]
                {
                    new CashServiceModalOwnerWindow.ActionButtonState { Label = "OK", IsPrimary = true }
                });
            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.CashNameChangeLicenseDialog);
        }

        private void ShowCashTransferWorldLicenseDialog(CashServiceStageWindow stageWindow)
        {
            if (!TryGetCashServiceModalOwnerWindow(MapSimulatorWindowNames.CashTransferWorldLicenseDialog, out CashServiceModalOwnerWindow modalWindow))
            {
                return;
            }

            modalWindow.Configure(
                "CUITransferWorldLicenseNotice",
                "Confirm the transfer-world license notice before returning to the Cash Shop stage.",
                new[]
                {
                    $"Request id: {stageWindow.CashTransferWorldPossibleResult.RequestId.ToString(CultureInfo.InvariantCulture)}.",
                    $"Birth date payload: {stageWindow.CashTransferWorldPossibleResult.BirthDate.ToString(CultureInfo.InvariantCulture)}.",
                    $"Decoded world list: {stageWindow.CashTransferWorldPossibleResult.WorldNames.Count.ToString(CultureInfo.InvariantCulture)} world(s).",
                    stageWindow.CashTransferWorldLastSummary,
                    "WZ: CashShop.img/CSTransferWorld/Base/backgrndnotice."
                },
                new[]
                {
                    new CashServiceModalOwnerWindow.ActionButtonState { Label = "OK", IsPrimary = true }
                },
                worldNames: stageWindow.CashTransferWorldPossibleResult.WorldNames);
            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.CashTransferWorldLicenseDialog);
        }

        private static IReadOnlyList<string> BuildCashPurchaseConfirmDialogLines(
            AdminShopDialogUI.ListOwnerSnapshot listSnapshot,
            AdminShopDialogUI.OwnerEntrySnapshot selectedEntry,
            CashServiceStageWindow stageWindow,
            IReadOnlyList<AdminShopDialogUI.CommodityPurchaseVariantSnapshot> purchaseVariants)
        {
            List<string> lines = new()
            {
                $"{listSnapshot?.PaneLabel} | {listSnapshot?.BrowseModeLabel} | {listSnapshot?.CategoryLabel}",
                $"{selectedEntry.Seller} | {selectedEntry.PriceLabel}",
                selectedEntry.Detail,
                selectedEntry.StateLabel
            };

            if (purchaseVariants?.Count > 1)
            {
                lines.Add($"Combo 1003 owns {purchaseVariants.Count.ToString(CultureInfo.InvariantCulture)} package/period option(s) for this item.");
            }

            if (selectedEntry.CommoditySerialNumber > 0
                && AdminShopDialogUI.TryResolveCommodityBySerialNumber(
                    selectedEntry.CommoditySerialNumber,
                    out int itemId,
                    out long price,
                    out int count,
                    out bool onSale))
            {
                string itemLabel = InventoryItemMetadataResolver.TryResolveItemName(itemId, out string resolvedName)
                    ? resolvedName
                    : $"Item {itemId.ToString(CultureInfo.InvariantCulture)}";
                lines.Add($"SN {selectedEntry.CommoditySerialNumber.ToString(CultureInfo.InvariantCulture)} -> {itemLabel} x{count.ToString(CultureInfo.InvariantCulture)} / {price.ToString("N0", CultureInfo.InvariantCulture)} NX{(onSale ? string.Empty : " (off-sale)")}");
            }
            else if (selectedEntry.RewardItemId > 0)
            {
                string itemLabel = InventoryItemMetadataResolver.TryResolveItemName(selectedEntry.RewardItemId, out string resolvedName)
                    ? resolvedName
                    : $"Item {selectedEntry.RewardItemId.ToString(CultureInfo.InvariantCulture)}";
                lines.Add($"{itemLabel} x{Math.Max(1, selectedEntry.RewardQuantity).ToString(CultureInfo.InvariantCulture)} is staged in the selected purchase row.");
            }

            return lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        }

        private static IReadOnlyList<CashServiceModalOwnerWindow.CheckBoxState> BuildCashPurchasePaymentSelectorStates(
            CashServiceStageWindow stageWindow,
            AdminShopDialogUI.OwnerEntrySnapshot selectedEntry,
            IReadOnlyList<AdminShopDialogUI.CommodityPurchaseVariantSnapshot> purchaseVariants,
            int preferredVariantSerialNumber)
        {
            if (stageWindow == null)
            {
                return Array.Empty<CashServiceModalOwnerWindow.CheckBoxState>();
            }

            long requiredPrice = ResolveCashPurchaseRequiredPrice(
                selectedEntry,
                purchaseVariants,
                preferredVariantSerialNumber);
            int preferredPaymentControlId = stageWindow.CashPurchaseDialogSelectedPaymentControlId;
            bool maplePointEnabled = stageWindow.MaplePointBalance >= requiredPrice;
            bool prepaidEnabled = stageWindow.PrepaidCashBalance >= requiredPrice;
            bool nexonEnabled = stageWindow.NexonCashBalance >= requiredPrice;
            bool preferredEnabled = preferredPaymentControlId switch
            {
                1000 => maplePointEnabled,
                1001 => prepaidEnabled,
                1002 => nexonEnabled,
                _ => false
            };
            int selectedPaymentControlId = preferredEnabled
                ? preferredPaymentControlId
                : maplePointEnabled
                    ? 1000
                    : prepaidEnabled
                        ? 1001
                        : nexonEnabled
                            ? 1002
                            : 0;
            return new[]
            {
                new CashServiceModalOwnerWindow.CheckBoxState
                {
                    ControlId = 1000,
                    Label = "Maple Point",
                    Detail = FormatCashPurchasePaymentDetail(stageWindow.MaplePointBalance, requiredPrice),
                    IsChecked = selectedPaymentControlId == 1000,
                    IsEnabled = maplePointEnabled,
                    ClientX = 25,
                    ClientYFromBottom = 95,
                    ClientWidth = 160,
                    ClientHeight = 14
                },
                new CashServiceModalOwnerWindow.CheckBoxState
                {
                    ControlId = 1001,
                    Label = "Prepaid Cash",
                    Detail = FormatCashPurchasePaymentDetail(stageWindow.PrepaidCashBalance, requiredPrice),
                    IsChecked = selectedPaymentControlId == 1001,
                    IsEnabled = prepaidEnabled,
                    ClientX = 25,
                    ClientYFromBottom = 80,
                    ClientWidth = 230,
                    ClientHeight = 14
                },
                new CashServiceModalOwnerWindow.CheckBoxState
                {
                    ControlId = 1002,
                    Label = "Nexon Cash",
                    Detail = FormatCashPurchasePaymentDetail(stageWindow.NexonCashBalance, requiredPrice),
                    IsChecked = selectedPaymentControlId == 1002,
                    IsEnabled = nexonEnabled,
                    ClientX = 25,
                    ClientYFromBottom = 65,
                    ClientWidth = 230,
                    ClientHeight = 14
                }
            };
        }

        private static int ResolveCashPurchasePreferredVariantSerialNumber(
            AdminShopDialogUI.OwnerEntrySnapshot selectedEntry,
            IReadOnlyList<AdminShopDialogUI.CommodityPurchaseVariantSnapshot> purchaseVariants,
            int preferredVariantSerialNumber)
        {
            if (purchaseVariants == null || purchaseVariants.Count == 0)
            {
                return preferredVariantSerialNumber > 0
                    ? preferredVariantSerialNumber
                    : Math.Max(0, selectedEntry?.CommoditySerialNumber ?? 0);
            }

            if (preferredVariantSerialNumber > 0
                && purchaseVariants.Any(variant => variant.SerialNumber == preferredVariantSerialNumber))
            {
                return preferredVariantSerialNumber;
            }

            int selectedCommoditySerial = Math.Max(0, selectedEntry?.CommoditySerialNumber ?? 0);
            if (selectedCommoditySerial > 0
                && purchaseVariants.Any(variant => variant.SerialNumber == selectedCommoditySerial))
            {
                return selectedCommoditySerial;
            }

            return Math.Max(0, purchaseVariants[0].SerialNumber);
        }

        private static long ResolveCashPurchaseRequiredPrice(
            AdminShopDialogUI.OwnerEntrySnapshot selectedEntry,
            IReadOnlyList<AdminShopDialogUI.CommodityPurchaseVariantSnapshot> purchaseVariants,
            int preferredVariantSerialNumber)
        {
            if (purchaseVariants != null && purchaseVariants.Count > 0)
            {
                AdminShopDialogUI.CommodityPurchaseVariantSnapshot variant = purchaseVariants
                    .FirstOrDefault(candidate => candidate.SerialNumber == preferredVariantSerialNumber)
                    ?? purchaseVariants[0];
                return Math.Max(0L, variant?.Price ?? 0L);
            }

            if (selectedEntry != null
                && selectedEntry.CommoditySerialNumber > 0
                && AdminShopDialogUI.TryResolveCommodityBySerialNumber(
                    selectedEntry.CommoditySerialNumber,
                    out _,
                    out long resolvedPrice,
                    out _,
                    out _))
            {
                return Math.Max(0L, resolvedPrice);
            }

            return 0L;
        }

        private static string FormatCashPurchasePaymentDetail(long availableBalance, long requiredPrice)
        {
            long normalizedBalance = Math.Max(0L, availableBalance);
            long normalizedRequired = Math.Max(0L, requiredPrice);
            if (normalizedRequired <= 0L)
            {
                return normalizedBalance.ToString("N0", CultureInfo.InvariantCulture);
            }

            return $"{normalizedBalance.ToString("N0", CultureInfo.InvariantCulture)} / need {normalizedRequired.ToString("N0", CultureInfo.InvariantCulture)}";
        }

        private static CashServiceModalOwnerWindow.ComboBoxState BuildCashPurchaseVariantComboBoxState(
            AdminShopDialogUI.OwnerEntrySnapshot selectedEntry,
            IReadOnlyList<AdminShopDialogUI.CommodityPurchaseVariantSnapshot> purchaseVariants,
            int preferredVariantSerialNumber)
        {
            if (selectedEntry == null || purchaseVariants == null || purchaseVariants.Count <= 1)
            {
                return null;
            }

            IReadOnlyList<CashServiceModalOwnerWindow.ComboBoxItemState> items = purchaseVariants
                .Select(variant => new CashServiceModalOwnerWindow.ComboBoxItemState
                {
                    Value = variant.SerialNumber,
                    Label = string.IsNullOrWhiteSpace(variant.Label)
                        ? $"SN {variant.SerialNumber.ToString(CultureInfo.InvariantCulture)}"
                        : $"SN {variant.SerialNumber.ToString(CultureInfo.InvariantCulture)} | {variant.Label}"
                })
                .ToArray();
            int selectedIndex = purchaseVariants.ToList().FindIndex(variant => variant.SerialNumber == preferredVariantSerialNumber);
            if (selectedIndex < 0)
            {
                selectedIndex = purchaseVariants.ToList().FindIndex(variant => variant.SerialNumber == selectedEntry.CommoditySerialNumber);
            }

            selectedIndex = Math.Max(0, selectedIndex);
            return new CashServiceModalOwnerWindow.ComboBoxState
            {
                ControlId = 1003,
                Label = "Package / period",
                Items = items,
                SelectedIndex = selectedIndex,
                ClientX = 62,
                ClientY = 42,
                ClientWidth = 150,
                ClientHeight = 18
            };
        }

        private static IReadOnlyList<string> BuildCashReceiveGiftQueueLabels(IReadOnlyList<CashServiceStageWindow.PacketCatalogEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return Array.Empty<string>();
            }

            List<string> queueLabels = new(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                CashServiceStageWindow.PacketCatalogEntry entry = entries[i];
                string sender = string.IsNullOrWhiteSpace(entry?.Seller) ? "Unknown sender" : entry.Seller;
                string title = string.IsNullOrWhiteSpace(entry?.Title) ? "Gift" : entry.Title;
                string rowLabel = entry?.PacketRowIndex > 0
                    ? $"row {entry.PacketRowIndex.ToString(CultureInfo.InvariantCulture)}"
                    : $"queue {(i + 1).ToString(CultureInfo.InvariantCulture)}";
                string serialLabel = entry?.SerialNumber > 0
                    ? $" / SN {entry.SerialNumber.ToString(CultureInfo.InvariantCulture)}"
                    : string.Empty;
                string messagePreview = !string.IsNullOrWhiteSpace(entry?.PacketMessageRaw)
                    ? entry.PacketMessageRaw.Trim()
                    : entry?.PacketMessage ?? string.Empty;
                if (messagePreview.Length > 32)
                {
                    messagePreview = $"{messagePreview.Substring(0, 32)}...";
                }

                string messageLabel = string.IsNullOrWhiteSpace(messagePreview)
                    ? string.Empty
                    : $" / msg \"{messagePreview}\"";
                queueLabels.Add($"{(i + 1).ToString(CultureInfo.InvariantCulture)}. {rowLabel} / {title} / {sender}{serialLabel}{messageLabel}");
            }

            return queueLabels;
        }

        private bool TryGetCashServiceModalOwnerWindow(string windowName, out CashServiceModalOwnerWindow modalWindow)
        {
            modalWindow = uiWindowManager?.GetWindow(windowName) as CashServiceModalOwnerWindow;
            if (modalWindow != null)
            {
                modalWindow.SetFont(_fontChat);
                return true;
            }

            return false;
        }

        private void HandleCashCouponDialogButton(int buttonIndex)
        {
            if (!TryGetCashServiceModalOwnerWindow(MapSimulatorWindowNames.CashCouponDialog, out CashServiceModalOwnerWindow modalWindow)
                || uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) is not CashServiceStageWindow stageWindow)
            {
                uiWindowManager?.HideWindow(MapSimulatorWindowNames.CashCouponDialog);
                return;
            }

            string message = buttonIndex == 0
                ? stageWindow.RecordCouponDialogSubmission(modalWindow.InputValue)
                : "CCouponUseSelectDlg was cancelled before a coupon request was submitted.";
            uiWindowManager.HideWindow(MapSimulatorWindowNames.CashCouponDialog);
            _chat?.AddSystemMessage(message, currTickCount);
        }

        private void HandleCashPurchaseConfirmDialogButton(int buttonIndex)
        {
            string message;
            if (buttonIndex == 0
                && TryGetCashServiceModalOwnerWindow(MapSimulatorWindowNames.CashPurchaseConfirmDialog, out CashServiceModalOwnerWindow modalWindow)
                && uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShop) is AdminShopDialogUI cashShopWindow)
            {
                CashServiceStageWindow stageWindow = uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) as CashServiceStageWindow;
                int selectedPaymentControlId = modalWindow.SelectedCheckBoxControlId;
                if (!IsCashPurchasePaymentSelectionAccepted(selectedPaymentControlId))
                {
                    message = BuildCashPurchasePaymentSelectionRequiredNotice(selectedPaymentControlId);
                    stageWindow?.RecordPurchaseDialogSelection(
                        selectedPaymentControlId,
                        ResolveCashPurchasePaymentLabel(selectedPaymentControlId),
                        modalWindow.SelectedComboValue,
                        modalWindow.SelectedComboLabel);
                    _chat?.AddErrorMessage(message, currTickCount);
                    return;
                }

                string selectedPaymentLabel = ResolveCashPurchasePaymentLabel(selectedPaymentControlId);
                string selectorSummary = stageWindow?.RecordPurchaseDialogSelection(
                        selectedPaymentControlId,
                        selectedPaymentLabel,
                        modalWindow.SelectedComboValue,
                        modalWindow.SelectedComboLabel)
                    ?? BuildCashPurchaseConfirmSelectionSummary(modalWindow);
                string comboFocusSummary = TryApplyCashPurchaseVariantSelection(cashShopWindow, modalWindow.SelectedComboValue);
                string purchaseMessage = cashShopWindow.ExecuteCashStageListAction("BtBuy");
                message = string.Join(
                    " ",
                    new[] { selectorSummary, comboFocusSummary, purchaseMessage }
                        .Where(part => !string.IsNullOrWhiteSpace(part)));
            }
            else
            {
                message = "CConfirmPurchaseDlg cancelled the selected Cash Shop purchase.";
            }

            uiWindowManager?.HideWindow(MapSimulatorWindowNames.CashPurchaseConfirmDialog);
            _chat?.AddSystemMessage(message, currTickCount);
        }

        private void HandleCashReceiveGiftDialogButton(int buttonIndex)
        {
            if (!TryGetCashServiceModalOwnerWindow(MapSimulatorWindowNames.CashReceiveGiftDialog, out CashServiceModalOwnerWindow modalWindow)
                || uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) is not CashServiceStageWindow stageWindow)
            {
                uiWindowManager?.HideWindow(MapSimulatorWindowNames.CashReceiveGiftDialog);
                ClearCashReceiveGiftFollowUpNoticeState();
                return;
            }

            if (_cashReceiveGiftFollowUpNoticePending)
            {
                int nextGiftIndexAfterNotice = _cashReceiveGiftFollowUpNoticeNextIndex;
                uiWindowManager.HideWindow(MapSimulatorWindowNames.CashReceiveGiftDialog);
                ClearCashReceiveGiftFollowUpNoticeState();
                if (nextGiftIndexAfterNotice >= 0 && nextGiftIndexAfterNotice < stageWindow.CashGiftPacketEntries.Count)
                {
                    ShowCashReceiveGiftDialog(stageWindow, nextGiftIndexAfterNotice);
                }

                return;
            }

            if (buttonIndex != 0)
            {
                uiWindowManager.HideWindow(MapSimulatorWindowNames.CashReceiveGiftDialog);
                return;
            }

            int selectedGiftIndex = Math.Clamp(modalWindow.SelectedGiftIndex, 0, Math.Max(0, stageWindow.CashGiftPacketEntries.Count - 1));
            CashServiceStageWindow.PacketCatalogEntry selectedGift = stageWindow.CashGiftPacketEntries.Count > 0
                ? stageWindow.CashGiftPacketEntries[selectedGiftIndex]
                : null;
            string normalizedReplyText = NormalizeCashReceiveGiftReplyText(modalWindow.InputValue);
            if (!TryValidateCashReceiveGiftReplyText(normalizedReplyText, out string replyNotice))
            {
                _chat?.AddErrorMessage(replyNotice, currTickCount);
                return;
            }

            string dispatchSummary = DispatchCashReceiveGiftAcceptRequest(selectedGift, selectedGiftIndex, normalizedReplyText);
            string message = stageWindow.StageReceiveGiftAcceptRequest(selectedGiftIndex, normalizedReplyText, dispatchSummary);
            uiWindowManager.HideWindow(MapSimulatorWindowNames.CashReceiveGiftDialog);
            if (stageWindow.TryCompletePendingReceiveGiftAcceptFromDialogReturn(
                out string completionSummary,
                out string ownerNotice,
                out int nextGiftIndex))
            {
                ShowCashReceiveGiftFollowUpNoticeDialog(
                    stageWindow,
                    nextGiftIndex,
                    ownerNotice,
                    completionSummary);
                _chat?.AddSystemMessage(completionSummary, currTickCount);
                return;
            }

            _chat?.AddSystemMessage(message, currTickCount);
        }

        private static IEnumerable<string> BuildCashReceiveGiftSpecialistMessages(
            CashServiceStageWindow.PacketCatalogEntry selectedGift,
            string replyText)
        {
            if (selectedGift != null)
            {
                yield return selectedGift.PacketMessage;
                yield return selectedGift.Detail;
            }

            yield return replyText;
        }

        private static string BuildCashPurchaseConfirmSelectionSummary(CashServiceModalOwnerWindow modalWindow)
        {
            if (modalWindow == null)
            {
                return string.Empty;
            }

            string payment = ResolveCashPurchasePaymentLabel(modalWindow.SelectedCheckBoxControlId);
            string combo = modalWindow.SelectedComboValue > 0
                ? $"combo 1003 selected {modalWindow.SelectedComboLabel}"
                : string.Empty;
            List<string> parts = new();
            if (!string.IsNullOrWhiteSpace(payment))
            {
                parts.Add($"payment selector {modalWindow.SelectedCheckBoxControlId.ToString(CultureInfo.InvariantCulture)} ({payment})");
            }

            if (!string.IsNullOrWhiteSpace(combo))
            {
                parts.Add(combo);
            }

            return parts.Count == 0
                ? string.Empty
                : $"CConfirmPurchaseDlg confirmed with {string.Join(" and ", parts)}.";
        }

        internal static bool IsCashPurchasePaymentSelectionAccepted(int selectedCheckBoxControlId)
        {
            return selectedCheckBoxControlId is 1000 or 1001 or 1002;
        }

        internal static string BuildCashPurchasePaymentSelectionRequiredNotice(int selectedCheckBoxControlId)
        {
            string selectorLabel = ResolveCashPurchasePaymentLabel(selectedCheckBoxControlId);
            string selectedLabel = string.IsNullOrWhiteSpace(selectorLabel)
                ? "no accepted payment checkbox"
                : $"{selectedCheckBoxControlId.ToString(CultureInfo.InvariantCulture)} ({selectorLabel})";
            return $"CConfirmPurchaseDlg kept the modal open because SetRet(1) requires exactly one checked cash-payment option; selected {selectedLabel}.";
        }

        private static string ResolveCashPurchasePaymentLabel(int selectedCheckBoxControlId)
        {
            return selectedCheckBoxControlId switch
            {
                1000 => "Maple Point",
                1001 => "Prepaid Cash",
                1002 => "Nexon Cash",
                _ => string.Empty
            };
        }

        private string TryApplyCashPurchaseVariantSelection(AdminShopDialogUI cashShopWindow, int selectedComboValue)
        {
            if (cashShopWindow == null || selectedComboValue <= 0)
            {
                return string.Empty;
            }

            bool focused = TryFocusCashServiceCommodity(selectedComboValue) || cashShopWindow.TryFocusCommoditySerialNumber(selectedComboValue);
            return focused
                ? $"Combo 1003 focused SN {selectedComboValue.ToString(CultureInfo.InvariantCulture)} before BtBuy."
                : $"Combo 1003 could not focus SN {selectedComboValue.ToString(CultureInfo.InvariantCulture)}, so BtBuy stayed on the currently selected catalog row.";
        }

        private static bool TryValidateCashReceiveGiftReplyText(string normalizedReplyText, out string notice)
        {
            string reply = normalizedReplyText ?? string.Empty;
            if (reply.Length > 200)
            {
                notice = "CUIReceiveGift rejected the reply because CCtrlEdit::SetTextLimit still caps the memo at 200 characters.";
                return false;
            }

            if (!ClientCurseProcessParity.TryValidateText(reply, out string contentNotice))
            {
                notice = $"CUIReceiveGift rejected the reply text through CCurseProcess::ProcessString: {contentNotice}";
                return false;
            }

            notice = string.Empty;
            return true;
        }

        private static string NormalizeCashReceiveGiftReplyText(string replyText)
        {
            return (replyText ?? string.Empty).Trim();
        }

        private string DispatchCashReceiveGiftAcceptRequest(
            CashServiceStageWindow.PacketCatalogEntry selectedGift,
            int selectedGiftIndex,
            string replyText)
        {
            if (selectedGift == null)
            {
                return "CUIReceiveGift did not dispatch an accept packet because the selected GW_GiftList row was unavailable.";
            }

            int opcode = selectedGift.RequestOpcode > 0 ? selectedGift.RequestOpcode : 154;
            byte[] payload = BuildCashReceiveGiftAcceptRequestPayload(selectedGift, selectedGiftIndex, replyText);
            string payloadHex = payload.Length > 0 ? Convert.ToHexString(payload) : "<empty>";
            string source = "CUIReceiveGift::OnCashItemResLoadGiftDone accept branch";
            string bridgeStatus = "official-session bridge unavailable";
            if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(opcode, payload, out bridgeStatus))
            {
                return $"{source} emitted opcode {opcode.ToString(CultureInfo.InvariantCulture)} [{payloadHex}] through the live local-utility bridge. {bridgeStatus}";
            }

            string outboxStatus = "packet outbox unavailable";
            if (_localUtilityPacketOutbox.TrySendOutboundPacket(opcode, payload, out outboxStatus))
            {
                return $"{source} emitted opcode {opcode.ToString(CultureInfo.InvariantCulture)} [{payloadHex}] through the generic local-utility outbox after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus}";
            }

            string bridgeDeferredStatus = "Official-session bridge deferred delivery is disabled.";
            if (_localUtilityOfficialSessionBridgeEnabled
                && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(opcode, payload, out bridgeDeferredStatus))
            {
                return $"{source} queued opcode {opcode.ToString(CultureInfo.InvariantCulture)} [{payloadHex}] for deferred official-session injection after immediate delivery was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred bridge: {bridgeDeferredStatus}";
            }

            if (_localUtilityPacketOutbox.TryQueueOutboundPacket(opcode, payload, out string queuedOutboxStatus))
            {
                return $"{source} queued opcode {opcode.ToString(CultureInfo.InvariantCulture)} [{payloadHex}] for deferred generic local-utility outbox delivery after immediate delivery was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred bridge: {bridgeDeferredStatus} Deferred outbox: {queuedOutboxStatus}";
            }

            return $"{source} kept opcode {opcode.ToString(CultureInfo.InvariantCulture)} [{payloadHex}] simulator-local because neither the live bridge nor the generic outbox nor either deferred queue accepted it. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred bridge: {bridgeDeferredStatus} Deferred outbox: {queuedOutboxStatus}";
        }

        private static byte[] BuildCashReceiveGiftAcceptRequestPayload(
            CashServiceStageWindow.PacketCatalogEntry selectedGift,
            int selectedGiftIndex,
            string replyText)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)0);
            string giftSender = !string.IsNullOrWhiteSpace(selectedGift?.PacketSenderRaw)
                ? selectedGift.PacketSenderRaw
                : selectedGift?.Seller ?? string.Empty;
            WriteCashReceiveGiftMapleString(writer, giftSender);
            WriteCashReceiveGiftMapleString(writer, replyText ?? string.Empty);
            writer.Write((byte)1);
            int zeroBasedGiftIndex = selectedGift?.PacketRowIndex > 0
                ? selectedGift.PacketRowIndex - 1
                : Math.Max(0, selectedGiftIndex);
            writer.Write(Math.Max(0, zeroBasedGiftIndex));
            writer.Write(selectedGift?.SerialNumber ?? 0L);
            writer.Flush();
            return stream.ToArray();
        }

        private static void WriteCashReceiveGiftMapleString(BinaryWriter writer, string text)
        {
            byte[] bytes = Encoding.Default.GetBytes(text ?? string.Empty);
            writer.Write((ushort)Math.Min(ushort.MaxValue, bytes.Length));
            writer.Write(bytes, 0, Math.Min(ushort.MaxValue, bytes.Length));
        }

        private void HandleCashNameChangeLicenseDialogButton(int buttonIndex)
        {
            string message = uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) is CashServiceStageWindow stageWindow
                ? stageWindow.AcknowledgeNameChangeLicenseDialog()
                : "CUIChangingLicenseNotice closed without an available CCashShop stage owner.";
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.CashNameChangeLicenseDialog);
            _chat?.AddSystemMessage(message, currTickCount);
        }

        private void HandleCashTransferWorldLicenseDialogButton(int buttonIndex)
        {
            string message = uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) is CashServiceStageWindow stageWindow
                ? stageWindow.AcknowledgeTransferWorldLicenseDialog()
                : "CUITransferWorldLicenseNotice closed without an available CCashShop stage owner.";
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.CashTransferWorldLicenseDialog);
            _chat?.AddSystemMessage(message, currTickCount);
        }

        private bool TryApplyCashShopStorageExpansionPacketResult(int packetType, byte[] payload, out string message)
        {
            message = null;
            if (packetType != 384
                || uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShop) is not AdminShopDialogUI cashShopWindow
                || !CashShopStorageExpansionPacketCodec.TryDecodePayload(payload, out CashShopStorageExpansionPacketResult packetResult)
                || packetResult == null)
            {
                return false;
            }

            bool applied;
            string storageMessage;
            if (cashShopWindow.HasPendingStorageExpansionRequest)
            {
                applied = cashShopWindow.TryApplyPacketOwnedStorageExpansionResult(
                    new AdminShopDialogUI.PacketOwnedStorageExpansionResult
                    {
                        PacketType = packetType,
                        CashItemResultSubtype = packetResult.CashItemResultSubtype,
                        CommoditySerialNumber = packetResult.CommoditySerialNumber,
                        ResultSubtype = packetResult.ResultSubtype,
                        FailureReason = packetResult.FailureReason,
                        NxPrice = packetResult.NxPrice,
                        SlotLimitAfterResult = packetResult.SlotLimitAfterResult,
                        ConsumeCash = packetResult.ConsumeCash,
                        Message = packetResult.Message
                    },
                    out storageMessage);
            }
            else
            {
                applied = TryApplyPassiveCashShopStorageExpansionPacketResult(packetType, packetResult, out storageMessage);
            }

            message = string.IsNullOrWhiteSpace(storageMessage)
                ? CashShopStorageExpansionPacketCodec.BuildSummary(packetResult)
                : storageMessage;
            if (ShouldExitCashShopAfterStorageExpansionFailure(packetResult))
            {
                HideCashShopOwnerFamilyWindows();
                message = $"{message} Client subtype 112 failure reason {packetResult.FailureReason.ToString(CultureInfo.InvariantCulture)} forced the simulator out of the Cash Shop owner family.";
            }

            return applied;
        }

        private bool TryApplyPassiveCashShopStorageExpansionPacketResult(
            int packetType,
            CashShopStorageExpansionPacketResult packetResult,
            out string message)
        {
            message = "Packet-owned storage-expansion result could not be applied outside an active request.";
            if (packetResult == null)
            {
                return false;
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.Trunk) is not IStorageRuntime storageRuntime)
            {
                message = "Storage runtime is unavailable for packet-owned trunk entitlement sync.";
                return false;
            }

            if (packetResult.ResultSubtype == 1)
            {
                int slotLimitBeforeResult = storageRuntime.GetSlotLimit();
                if (packetResult.SlotLimitAfterResult > 0)
                {
                    storageRuntime.SetSlotLimit(packetResult.SlotLimitAfterResult);
                }
                else if (storageRuntime.CanExpandSlotLimit())
                {
                    storageRuntime.TryExpandSlotLimit();
                }

                int slotLimitAfterResult = storageRuntime.GetSlotLimit();
                if (slotLimitAfterResult <= slotLimitBeforeResult)
                {
                    message = "Packet-owned trunk entitlement sync did not advance the storage slot limit.";
                    return false;
                }
            }

            HandleStorageExpansionResolved(new AdminShopDialogUI.StorageExpansionResolution
            {
                CashItemResultSubtype = Math.Max(0, packetResult.CashItemResultSubtype),
                CommoditySerialNumber = Math.Max(0, packetResult.CommoditySerialNumber),
                ResultSubtype = packetResult.ResultSubtype,
                FailureReason = Math.Max(0, packetResult.FailureReason),
                NxPrice = Math.Max(0L, packetResult.NxPrice),
                SlotLimitAfterResult = Math.Max(0, storageRuntime.GetSlotLimit()),
                IsPacketOwned = true,
                PacketType = Math.Max(0, packetType),
                CashAlreadySettled = true,
                Message = string.IsNullOrWhiteSpace(packetResult.Message)
                    ? $"{CashShopStorageExpansionPacketCodec.BuildSummary(packetResult)} Passive account-owned trunk entitlement sync updated the simulator storage snapshot."
                    : $"{packetResult.Message} Passive account-owned trunk entitlement sync updated the simulator storage snapshot."
            });

            message = packetResult.ResultSubtype == 1
                ? $"Applied packet-owned trunk entitlement sync; storage now has {storageRuntime.GetSlotLimit().ToString(CultureInfo.InvariantCulture)} slots."
                : CashShopStorageExpansionPacketCodec.BuildSummary(packetResult);
            return true;
        }

        private static bool ShouldExitCashShopAfterStorageExpansionFailure(CashShopStorageExpansionPacketResult packetResult)
        {
            return packetResult?.CashItemResultSubtype == CashShopStorageExpansionPacketCodec.CashItemResIncTrunkCountFailedSubtype
                && packetResult.ResultSubtype != 1
                && packetResult.FailureReason is 0 or 1 or 2;
        }

        private bool TryApplyCashShopBalancePacket(int packetType, byte[] payload, out string message)
        {
            message = null;
            if (packetType != 383
                || !TryReadCashShopBalancePayload(payload, out long nexonCash, out _, out _)
                || nexonCash < 0)
            {
                return false;
            }

            _loginAccountCashShopNxCredit = nexonCash;
            PersistLoginCharacterRosterToAccountStore(
                _loginCharacterRoster.Entries,
                _loginCharacterRoster.SlotCount,
                _loginCharacterRoster.BuyCharacterCount);
            SyncCashShopAccountCredit();
            message = $"Account NX synced from packet-owned QueryCash to {_loginAccountCashShopNxCredit.ToString("N0", CultureInfo.InvariantCulture)}.";
            return true;
        }

        private static bool TryReadCashShopBalancePayload(byte[] payload, out long nexonCash, out long maplePoint, out long prepaidCash)
        {
            nexonCash = 0;
            maplePoint = 0;
            prepaidCash = 0;

            if (payload == null || payload.Length < sizeof(int) * 3)
            {
                return false;
            }

            try
            {
                nexonCash = Math.Max(0L, BitConverter.ToInt32(payload, 0));
                maplePoint = Math.Max(0L, BitConverter.ToInt32(payload, sizeof(int)));
                prepaidCash = Math.Max(0L, BitConverter.ToInt32(payload, sizeof(int) * 2));
                return true;
            }
            catch
            {
                nexonCash = 0;
                maplePoint = 0;
                prepaidCash = 0;
                return false;
            }
        }

        private static string CombineCashServicePacketMessages(params string[] messages)
        {
            List<string> combined = new();
            foreach (string part in messages)
            {
                if (!string.IsNullOrWhiteSpace(part))
                {
                    combined.Add(part.Trim());
                }
            }

            return combined.Count == 0
                ? string.Empty
                : string.Join(" ", combined.Distinct(StringComparer.Ordinal));
        }

        private bool ShouldRunCashServicePacketInbox()
        {
            if (_cashServicePacketInboxCommandOverrideEnabled.HasValue)
            {
                return _cashServicePacketInboxCommandOverrideEnabled.Value;
            }

            bool anyCashShopChildVisible = false;
            for (int i = 0; i < CashShopChildOwnerWindowNames.Length && !anyCashShopChildVisible; i++)
            {
                anyCashShopChildVisible = uiWindowManager?.GetWindow(CashShopChildOwnerWindowNames[i])?.IsVisible == true;
            }

            bool anyItcChildVisible = false;
            for (int i = 0; i < ItcChildOwnerWindowNames.Length && !anyItcChildVisible; i++)
            {
                anyItcChildVisible = uiWindowManager?.GetWindow(ItcChildOwnerWindowNames[i])?.IsVisible == true;
            }

            return uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShop)?.IsVisible == true
                || uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage)?.IsVisible == true
                || uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashAvatarPreview)?.IsVisible == true
                || anyCashShopChildVisible
                || uiWindowManager?.GetWindow(MapSimulatorWindowNames.Mts)?.IsVisible == true
                || uiWindowManager?.GetWindow(MapSimulatorWindowNames.MtsStatus)?.IsVisible == true
                || anyItcChildVisible;
        }
    }
}
