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
using System.Linq;
using CashServiceOwnerStageKind = HaCreator.MapSimulator.UI.CashServiceStageKind;

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
        private const string CashServiceStageBgmPath = "BgmUI/ShopBgm";
        private const int CashShopOneADayHistorySlotCount = 12;
        private int _lastPlayedCashGachaponAnimationSequence;

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
            public bool HasItemBoxButton { get; init; }
            public int NumberCanvasCount { get; init; }
            public int PlateCount { get; init; }
            public bool ResolvedFromClientStringPool { get; init; }
        }

        private string PreviewCashAvatarWeatherAction()
        {
            int currTickCount = Environment.TickCount;
            string restrictionMessage = FieldInteractionRestrictionEvaluator.GetCashWeatherRestrictionMessage(_mapBoard?.MapInfo?.fieldLimit ?? 0);
            if (!string.IsNullOrWhiteSpace(restrictionMessage))
            {
                PushFieldRuleMessage(restrictionMessage, currTickCount, showOverlay: false);
                return restrictionMessage;
            }

            _fieldEffects?.AddWeatherMessage("Cash Shop weather preview staged.", WeatherEffectType.None, currTickCount);
            return "CCSWnd_Char::BlowWeather staged the selected cash-weather preview action.";
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
                cashAvatarPreviewWindow.PersonalShopRequested = () => ShowSocialRoomWindowForCallback(
                    SocialRoomKind.PersonalShop,
                    "CCSWnd_Char::ShowPersonalShop opened the dedicated personal-shop owner.");
                cashAvatarPreviewWindow.EntrustedShopRequested = () => ShowSocialRoomWindowForCallback(
                    SocialRoomKind.EntrustedShop,
                    "CCSWnd_Char::ShowEntrustedShop opened the dedicated entrusted-shop owner.");
                cashAvatarPreviewWindow.TradingRoomRequested = () =>
                {
                    ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.CashTradingRoom);
                    return "CCSWnd_Char handed the selected listing to CCashTradingRoomDlg.";
                };
                cashAvatarPreviewWindow.WeatherRequested = PreviewCashAvatarWeatherAction;
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashTradingRoom) is CashTradingRoomWindow cashTradingRoomWindow)
            {
                cashTradingRoomWindow.SetFont(_fontChat);
                cashTradingRoomWindow.SetWalletProvider(() => (int)Math.Clamp(ResolveCurrentCashServiceMesoBalance(), 0L, int.MaxValue));
                cashTradingRoomWindow.SetTraderNames(_playerManager?.Player?.Build?.Name, "CashTrader");
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
                inventoryWindow.SetExternalAction("BtExTrunk", () =>
                {
                    ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.CashShopLocker);
                    return "CCSWnd_Inventory routed trunk access back to CCSWnd_Locker.";
                });
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopList) is CashShopStageChildWindow listWindow)
            {
                listWindow.SetFont(_fontChat);
                listWindow.SetContentProvider(() => BuildCashShopListOwnerLines(cashShopWindow));
                listWindow.SetListRowSelectionAction(cashShopWindow.SelectListOwnerVisibleRow);
                listWindow.SetListScrollAction(cashShopWindow.MoveListOwnerSelection);
                listWindow.SetExternalAction("BtBuy", () => ShowCashPurchaseConfirmDialog(cashShopWindow));
                listWindow.SetExternalAction("BtGift", () => cashShopWindow.ExecuteCashStageListAction("BtGift"));
                listWindow.SetExternalAction("BtReserve", () => cashShopWindow.ExecuteCashStageListAction("BtReserve"));
                listWindow.SetExternalAction("BtRemove", () => cashShopWindow.ExecuteCashStageListAction("BtRemove"));
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
                statusWindow.SetExternalAction("BtCoupon", () =>
                    ShowCashCouponDialog());
                statusWindow.SetExternalAction("BtExit", () =>
                {
                    HideCashShopOwnerFamilyWindows();
                    return "CCSWnd_Status closed the parent CCashShop owner family.";
                });
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
                    int currentCommoditySerialNumber = Math.Max(0, stageWindow.CashOneADayItemSerialNumber);
                    TryResolveCashShopOneADayRemainingTime(
                        stageWindow.CashOneADayItemDate,
                        DateTime.Now,
                        out int remainingHour,
                        out int remainingMinute,
                        out int remainingSecond);
                    return new CashShopStageChildWindow.OneADayOwnerState
                    {
                        IsPending = stageWindow.IsOneADayPending,
                        NoticeState = stageWindow.NoticeState,
                        SelectorIndex = 0,
                        SelectorStartX = 2,
                        SelectorStartY = 2,
                        TodaySelectorLabel = MapleStoryStringPool.GetOrFallback(0x16A1, "Today"),
                        PreviousSelectorLabel = MapleStoryStringPool.GetOrFallback(0x16A2, "Previous"),
                        HasKeyFocusCanvas = artSnapshot.HasKeyFocusCanvas,
                        HasPlateCanvas = artSnapshot.HasPlateCanvas,
                        HasPlateBigCanvas = artSnapshot.HasPlateBigCanvas,
                        NumberCanvasCount = artSnapshot.NumberCanvasCount,
                        PlateCount = Math.Max(1, artSnapshot.PlateCount),
                        PreviousOfferCount = ResolveCashShopOneADayHistorySlotCount(),
                        PlateCanvasBaseName = "NoItem",
                        ShortcutHelpCanvasName = artSnapshot.HasShortcutHelpCanvas ? "ShortcutHelp" : string.Empty,
                        CurrentCommoditySerialNumber = currentCommoditySerialNumber,
                        CurrentItemLabel = ResolveCashShopOneADayCommodityLabel(currentCommoditySerialNumber),
                        CurrentDateRaw = stageWindow.CashOneADayItemDate,
                        CurrentDateLabel = FormatCashShopOneADayDate(stageWindow.CashOneADayItemDate),
                        Hour = remainingHour,
                        Minute = remainingMinute,
                        Second = remainingSecond,
                        PacketStateSignature = BuildCashShopOneADayPacketStateSignature(stageWindow, historyEntries),
                        HistoryEntries = historyEntries,
                        RecentPackets = stageWindow.GetRecentPacketSummaries()
                    };
                });
                oneADayWindow.SetExternalAction("BtBuy", () =>
                {
                    string summary = BuildCashShopOneADayCurrentPurchaseSummary();
                    return string.IsNullOrWhiteSpace(summary)
                        ? "CCSWnd_OneADay routed the dedicated today-item buy button through the packet-owned reward lane."
                        : summary;
                });
                oneADayWindow.SetExternalAction("BtItemBox", () =>
                {
                    string summary = BuildCashShopOneADayItemBoxSummary();
                    return string.IsNullOrWhiteSpace(summary)
                        ? "CCSWnd_OneADay moved owner focus through the dedicated item-box lane."
                        : summary;
                });
                oneADayWindow.SetExternalAction("BtJoin", () => "CCSWnd_OneADay joined the packet-armed reward session preview.");
                oneADayWindow.SetExternalAction("BtShortcut", () => "CCSWnd_OneADay switched focus to the shortcut-help plate owner.");
                oneADayWindow.SetExternalAction("BtClose", () => "CCSWnd_OneADay dismissed the current reward plate preview.");
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
                modalWindow.SetButtonHandler(buttonHandler);
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
                stageWindow.IsOneADayPending ? "1" : "0",
                stageWindow.CashOneADayItemSerialNumber.ToString(CultureInfo.InvariantCulture),
                stageWindow.CashOneADayItemDate.ToString(CultureInfo.InvariantCulture),
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
                lines.Add(stageWindow.CashGiftLastSummary);
                foreach (string recentPacket in stageWindow.GetRecentPacketSummaries(2))
                {
                    lines.Add(recentPacket);
                }
            }

            return lines;
        }

        private CashShopStageChildWindow.LockerOwnerState BuildCashShopLockerOwnerState(AdminShopDialogUI cashShopWindow)
        {
            AdminShopDialogUI.LockerOwnerSnapshot snapshot = cashShopWindow?.GetLockerOwnerSnapshot() ?? new();
            CashServiceStageWindow stageWindow = uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) as CashServiceStageWindow;
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
                ScrollOffset = ResolveCashLockerInitialScrollOffset(_playerManager?.Player?.Build?.Job ?? 0),
                WheelRange = 208,
                HasNumberFont = true,
                SharedCharacterNames = snapshot.SharedCharacterNames
            };
        }

        private static int ResolveCashLockerInitialScrollOffset(int jobId)
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
                        lines.Add(entry.Detail);
                    }
                }
                else
                {
                    lines.Add(stageWindow.CashGiftLastSummary);
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
                ActiveTabName = packetFocus?.ActiveTabName ?? string.Empty,
                SelectedEntryTitle = selectedEntryTitle,
                PacketFocusSignature = packetFocus?.FocusSignature ?? string.Empty,
                PacketFocusMessage = packetFocus?.FocusMessage ?? string.Empty
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
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) is CashServiceStageWindow stageWindow)
            {
                if (!string.IsNullOrWhiteSpace(stageWindow.CashPurchaseRecordSummary))
                {
                    lines.Add(stageWindow.CashPurchaseRecordSummary);
                }

                foreach (CashServiceStageWindow.PacketCatalogEntry entry in stageWindow.CashPacketCatalogEntries.Take(2))
                {
                    lines.Add(entry.Detail);
                }

                foreach (string recentPacket in stageWindow.GetRecentPacketSummaries())
                {
                    lines.Add(recentPacket);
                }
            }

            return lines;
        }

        private CashShopStageChildWindow.ListOwnerState BuildCashShopListOwnerState(AdminShopDialogUI cashShopWindow)
        {
            AdminShopDialogUI.ListOwnerSnapshot snapshot = cashShopWindow?.GetListOwnerSnapshot();
            CashServiceStageWindow stageWindow = uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) as CashServiceStageWindow;
            IReadOnlyList<string> recentPackets = stageWindow?.GetRecentPacketSummaries() ?? Array.Empty<string>();
            if (stageWindow != null
                && (snapshot == null || (snapshot.TotalCount <= 0 && stageWindow.CashPacketCatalogEntries.Count > 0)))
            {
                List<CashShopStageChildWindow.ListOwnerEntryState> packetEntries = BuildPacketEntryStates(stageWindow.CashPacketCatalogEntries);
                return new CashShopStageChildWindow.ListOwnerState
                {
                    PaneLabel = stageWindow.CashPacketPaneLabel,
                    BrowseModeLabel = stageWindow.CashPacketBrowseModeLabel,
                    CategoryLabel = "CCashShop",
                    FooterMessage = stageWindow.StatusMessage,
                    SelectedEntryDetail = packetEntries.FirstOrDefault()?.Detail ?? string.Empty,
                    SelectedIndex = packetEntries.Count > 0 ? 0 : -1,
                    ScrollOffset = 0,
                    TotalCount = Math.Max(packetEntries.Count, stageWindow.WishlistCount),
                    PlateFocusIndex = packetEntries.Count > 0 ? 0 : -1,
                    HasKeyFocusCanvas = true,
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
                    HasKeyFocusCanvas = true,
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
                HasKeyFocusCanvas = true,
                VisibleEntries = entries,
                RecentPackets = recentPackets
            };
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

            lines.Add(summary);
        }

        private string BuildCashShopOneADayCurrentPurchaseSummary()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) is not CashServiceStageWindow stageWindow)
            {
                return "CCSWnd_OneADay is waiting for the parent Cash Shop stage.";
            }

            int commoditySerialNumber = Math.Max(0, stageWindow.CashOneADayItemSerialNumber);
            string itemLabel = ResolveCashShopOneADayCommodityLabel(commoditySerialNumber);
            if (!stageWindow.IsOneADayPending)
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
            return historyEntries.Count > 0
                ? $"CCSWnd_OneADay switched the dedicated item-box lane to the packet-authored previous-reward history ({historyEntries.Count.ToString(CultureInfo.InvariantCulture)} row(s))."
                : "CCSWnd_OneADay kept the item-box lane on the recovered previous selector, but no packet-authored history rows are loaded.";
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
            List<string> lines = new()
            {
                stageWindow.IsOneADayPending
                    ? "Packet 395 has armed the dedicated one-a-day owner."
                    : "No one-a-day packet is currently pending.",
                stageWindow.NoticeState
            };
            lines.Add(artSnapshot.HasKeyFocusCanvas || artSnapshot.HasPlateBigCanvas || artSnapshot.NumberCanvasCount > 0
                ? $"WZ-backed OneADay art exposes Base01={artSnapshot.HasKeyFocusCanvas}, ItemBox={artSnapshot.HasPlateCanvas}, ItemBoxBig={artSnapshot.HasPlateBigCanvas}, Counter digits={artSnapshot.NumberCanvasCount}, buttons Buy={artSnapshot.HasBuyButton}/ItemBox={artSnapshot.HasItemBoxButton}{(artSnapshot.ResolvedFromClientStringPool ? " via client StringPool paths" : string.Empty)}."
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
            for (int i = 0; i < 10; i++)
            {
                if (counterProperty?[i.ToString(CultureInfo.InvariantCulture)] != null)
                {
                    numberCanvasCount++;
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
                HasItemBoxButton = itemBoxButtonProperty != null || picturePlateProperty?["BtShortcut"] != null,
                NumberCanvasCount = numberCanvasCount,
                PlateCount = plateCount,
                ResolvedFromClientStringPool =
                    noItemProperty != null
                    || keyFocusProperty != null
                    || plateProperty != null
                    || plateBigProperty != null
                    || counterProperty != null
                    || buyButtonProperty != null
                    || itemBoxButtonProperty != null
            };
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
                saleWindow.SetExternalAction("BtShoppingBasket", () =>
                    $"CITCWnd_Sale kept shopping-basket ownership in the ITC stage (normal-item mutations: {mtsStageWindow.ItcNormalItemMutationCount.ToString(CultureInfo.InvariantCulture)}).");
                saleWindow.SetExternalAction("BtBuy", () =>
                    $"CITCWnd_Sale staged listing {mtsStageWindow.ItcNormalItemSelectedListingId.ToString(CultureInfo.InvariantCulture)} at {mtsStageWindow.ItcNormalItemSelectedPrice.ToString("N0", CultureInfo.InvariantCulture)} mesos.");
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.ItcPurchase) is CashShopStageChildWindow purchaseWindow)
            {
                purchaseWindow.SetFont(_fontChat);
                purchaseWindow.SetContentProvider(mtsStageWindow.DescribePurchaseOwnerState);
                purchaseWindow.SetExternalAction("BtRegistration", () =>
                    $"CITCWnd_Purchase armed listing registration on category {mtsStageWindow.ItcNormalItemCategory.ToString(CultureInfo.InvariantCulture)}, page {mtsStageWindow.ItcNormalItemPage.ToString(CultureInfo.InvariantCulture)}.");
                purchaseWindow.SetExternalAction("BtSell", () =>
                    "CITCWnd_Purchase switched focus back to the dedicated sale owner.");
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
                subTabWindow.SetExternalAction("BtSearch", () =>
                    $"CITCWnd_SubTab search stayed on category {mtsStageWindow.ItcNormalItemCategory.ToString(CultureInfo.InvariantCulture)}, page {mtsStageWindow.ItcNormalItemPage.ToString(CultureInfo.InvariantCulture)}.");
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.ItcList) is CashShopStageChildWindow listWindow)
            {
                listWindow.SetFont(_fontChat);
                listWindow.SetContentProvider(() => BuildItcListOwnerLines(mtsWindow, mtsStageWindow));
                listWindow.SetExternalAction("BtBuy", () =>
                    $"CITCWnd_List buy staged listing {mtsStageWindow.ItcNormalItemSelectedListingId.ToString(CultureInfo.InvariantCulture)}.");
                listWindow.SetExternalAction("BtDelete", () =>
                    $"CITCWnd_List delete staged listing {mtsStageWindow.ItcNormalItemSelectedListingId.ToString(CultureInfo.InvariantCulture)}.");
                listWindow.SetExternalAction("BtCancel", () => "CITCWnd_List cancelled the currently staged action.");
                listWindow.SetExternalAction("BtBuy1", () =>
                    $"CITCWnd_List opened alternate buy confirmation for listing {mtsStageWindow.ItcNormalItemSelectedListingId.ToString(CultureInfo.InvariantCulture)}.");
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
                statusWindow.SetExternalAction("BtCharge", () => "CITCWnd_Status kept charge ownership on the ITC stage.");
                statusWindow.SetExternalAction("BtCheck", () =>
                    $"CITCWnd_Status queried balances after {mtsStageWindow.ItcNormalItemMutationCount.ToString(CultureInfo.InvariantCulture)} normal-item mutation(s).");
                statusWindow.SetExternalAction("BtExit", () =>
                {
                    HideItcOwnerFamilyWindows();
                    return "CITCWnd_Status closed the parent CITC owner family.";
                });
            }
        }

        private IReadOnlyList<string> BuildItcListOwnerLines(AdminShopDialogUI mtsWindow, CashServiceStageWindow mtsStageWindow)
        {
            List<string> lines = new(mtsWindow?.DescribeListOwnerState() ?? Array.Empty<string>());
            if (mtsStageWindow?.ItcWishPacketEntries.Count > 0)
            {
                lines.Add($"Wish-sale rows {mtsStageWindow.ItcWishPacketEntries.Count.ToString(CultureInfo.InvariantCulture)} remain owned by the ITC stage.");
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
                        || (mtsStageWindow?.ItcWishPacketEntries.Count ?? 0) > 0));
            if (shouldUsePacketFallback)
            {
                IReadOnlyList<CashServiceStageWindow.PacketCatalogEntry> sourceEntries =
                    (mtsStageWindow?.ItcPacketCatalogEntries.Count ?? 0) > 0
                        ? mtsStageWindow.ItcPacketCatalogEntries
                        : mtsStageWindow?.ItcWishPacketEntries;
                List<CashShopStageChildWindow.ListOwnerEntryState> packetEntries = BuildPacketEntryStates(sourceEntries);
                int sortType = mtsStageWindow?.ItcNormalItemSortType ?? 0;
                int category = mtsStageWindow?.ItcNormalItemCategory ?? 0;
                int subCategory = mtsStageWindow?.ItcNormalItemSubCategory ?? 0;
                int totalCount = mtsStageWindow?.ItcCurrentCategoryItemCount ?? 0;
                bool usingWishEntries = (mtsStageWindow?.ItcPacketCatalogEntries.Count ?? 0) <= 0 && (mtsStageWindow?.ItcWishPacketEntries.Count ?? 0) > 0;
                return new CashShopStageChildWindow.ListOwnerState
                {
                    PaneLabel = usingWishEntries ? "CITC wish-sale list" : "CITC packet list",
                    BrowseModeLabel = $"Sort {sortType.ToString(CultureInfo.InvariantCulture)}",
                    CategoryLabel = $"Category {category.ToString(CultureInfo.InvariantCulture)}/{subCategory.ToString(CultureInfo.InvariantCulture)}",
                    FooterMessage = mtsStageWindow?.ItcNormalItemLastSummary ?? "CITC packet list unavailable.",
                    SelectedEntryDetail = packetEntries.FirstOrDefault()?.Detail ?? string.Empty,
                    SelectedIndex = packetEntries.Count > 0 ? 0 : -1,
                    ScrollOffset = 0,
                    TotalCount = Math.Max(usingWishEntries ? (mtsStageWindow?.ItcWishPacketEntries.Count ?? 0) : totalCount, packetEntries.Count),
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
                    Detail = entry.Detail,
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

            return new[]
            {
                balanceLine,
                stageWindow.StatusMessage
            };
        }

        private void HideCashShopOwnerFamilyWindows()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShop) is AdminShopDialogUI cashShopWindow)
            {
                cashShopWindow.RecordPacketOwnedAdminShopOwnerSurfaceHidden(
                    "CAdminShopDlg owner surface is hidden because the Cash Shop owner family is not visible.",
                    AdminShopPacketOwnedOwnerVisibilityState.HiddenByCashShopFamily);
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

            if (_cashServicePacketInbox.IsRunning)
            {
                return;
            }

            try
            {
                _cashServicePacketInbox.Start();
            }
            catch (Exception ex)
            {
                _cashServicePacketInbox.Stop();
                _chat?.AddErrorMessage($"Cash-service packet inbox failed to start: {ex.Message}", currTickCount);
            }
        }

        private void DrainCashServicePacketInbox()
        {
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

            if (packetType == 384 && stageWindow.CashItemResultSubtype == 90 && stageWindow.CashGiftPacketEntries.Count > 0)
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
            modalWindow.Configure(
                "CConfirmPurchaseDlg",
                $"Confirm purchase for {selectedEntry.Title}.",
                BuildCashPurchaseConfirmDialogLines(listSnapshot, selectedEntry, stageWindow),
                new[]
                {
                    new CashServiceModalOwnerWindow.ActionButtonState { Label = "OK", IsPrimary = true },
                    new CashServiceModalOwnerWindow.ActionButtonState { Label = "Cancel" }
                },
                footer: "Client evidence: CConfirmPurchaseDlg::OnCreate creates Maple Point (1000), Prepaid Cash (1001), and Nexon Cash (1002) selectors plus a package/period combo box when the selected commodity exposes multiple packed entries.");
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

            CashServiceStageWindow.PacketCatalogEntry entry = stageWindow.CashGiftPacketEntries[giftIndex];
            int totalGiftCount = stageWindow.CashGiftPacketEntries.Count;
            string sender = string.IsNullOrWhiteSpace(entry.Seller) ? "Unknown sender" : entry.Seller;
            modalWindow.Configure(
                "CUIReceiveGift",
                $"Review gift row {(giftIndex + 1).ToString(CultureInfo.InvariantCulture)} of {totalGiftCount.ToString(CultureInfo.InvariantCulture)} before the next CDialog::DoModal pass.",
                new[]
                {
                    $"{entry.Title} | {sender}",
                    entry.Detail,
                    string.IsNullOrWhiteSpace(entry.PriceLabel) ? string.Empty : entry.PriceLabel,
                    string.IsNullOrWhiteSpace(entry.StateLabel) ? string.Empty : entry.StateLabel,
                    "Client evidence: OnCashItemResLoadGiftDone allocates one CUIReceiveGift per decoded GW_GiftList row and advances after each modal closes."
                },
                new[]
                {
                    new CashServiceModalOwnerWindow.ActionButtonState { Label = "Accept", IsPrimary = true },
                    new CashServiceModalOwnerWindow.ActionButtonState { Label = "Cancel" }
                },
                footer: totalGiftCount > 1
                    ? $"{Math.Max(0, totalGiftCount - giftIndex - 1).ToString(CultureInfo.InvariantCulture)} later gift row(s) remain in this packet-owned modal sequence."
                    : "This is the last staged gift row in the current packet-owned modal sequence.",
                inputPlaceholder: "Reply message",
                inputActive: true,
                inputMaxLength: 64,
                giftRows: BuildCashReceiveGiftQueueLabels(stageWindow.CashGiftPacketEntries),
                selectedGiftIndex: giftIndex);
            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.CashReceiveGiftDialog);
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
            CashServiceStageWindow stageWindow)
        {
            List<string> lines = new()
            {
                $"{listSnapshot?.PaneLabel} | {listSnapshot?.BrowseModeLabel} | {listSnapshot?.CategoryLabel}",
                $"{selectedEntry.Seller} | {selectedEntry.PriceLabel}",
                selectedEntry.Detail,
                selectedEntry.StateLabel
            };

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

            if (stageWindow != null)
            {
                lines.Add($"[ ] Maple Point: {stageWindow.MaplePointBalance.ToString("N0", CultureInfo.InvariantCulture)}");
                lines.Add($"[ ] Prepaid Cash: {stageWindow.PrepaidCashBalance.ToString("N0", CultureInfo.InvariantCulture)}");
                lines.Add($"[ ] Nexon Cash: {stageWindow.NexonCashBalance.ToString("N0", CultureInfo.InvariantCulture)}");
            }

            return lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
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
                queueLabels.Add($"{(i + 1).ToString(CultureInfo.InvariantCulture)}. {title} / {sender}");
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
            if (buttonIndex == 0 && uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShop) is AdminShopDialogUI cashShopWindow)
            {
                message = cashShopWindow.ExecuteCashStageListAction("BtBuy");
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
                return;
            }

            int selectedGiftIndex = Math.Clamp(modalWindow.SelectedGiftIndex, 0, Math.Max(0, stageWindow.CashGiftPacketEntries.Count - 1));
            string message = buttonIndex == 0
                ? stageWindow.CompleteReceiveGiftDialog(selectedGiftIndex, modalWindow.InputValue)
                : "CUIReceiveGift skipped the current decoded gift row and advanced to the next modal-owner pass without sending the accept packet.";
            uiWindowManager.HideWindow(MapSimulatorWindowNames.CashReceiveGiftDialog);
            _chat?.AddSystemMessage(message, currTickCount);

            int nextGiftIndex = buttonIndex == 0 ? selectedGiftIndex : selectedGiftIndex + 1;
            if (nextGiftIndex < stageWindow.CashGiftPacketEntries.Count)
            {
                ShowCashReceiveGiftDialog(stageWindow, nextGiftIndex);
            }
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
