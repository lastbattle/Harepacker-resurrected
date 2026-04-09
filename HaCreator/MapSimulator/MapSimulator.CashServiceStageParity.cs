using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
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
                cashAvatarPreviewWindow.WeatherRequested = () =>
                {
                    _fieldEffects?.AddWeatherMessage("Cash Shop weather preview staged.", WeatherEffectType.None, currTickCount);
                    return "CCSWnd_Char::BlowWeather staged the selected cash-weather preview action.";
                };
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashTradingRoom) is CashTradingRoomWindow cashTradingRoomWindow)
            {
                cashTradingRoomWindow.SetFont(_fontChat);
                cashTradingRoomWindow.SetWalletProvider(() => (int)Math.Clamp(ResolveCurrentCashServiceMesoBalance(), 0L, int.MaxValue));
                cashTradingRoomWindow.SetTraderNames(_playerManager?.Player?.Build?.Name, "CashTrader");
            }

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

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopLocker) is CashShopStageChildWindow lockerWindow)
            {
                lockerWindow.SetFont(_fontChat);
                lockerWindow.SetContentProvider(cashShopWindow.DescribeLockerOwnerState);
                lockerWindow.SetLockerStateProvider(() =>
                {
                    AdminShopDialogUI.LockerOwnerSnapshot snapshot = cashShopWindow.GetLockerOwnerSnapshot();
                    return new CashShopStageChildWindow.LockerOwnerState
                    {
                        AccountLabel = snapshot.AccountLabel,
                        UsedSlotCount = snapshot.UsedSlotCount,
                        SlotLimit = snapshot.SlotLimit,
                        CanExpand = snapshot.CanExpand,
                        ScrollOffset = 0,
                        WheelRange = 208,
                        HasNumberFont = true,
                        SharedCharacterNames = snapshot.SharedCharacterNames
                    };
                });
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopInventory) is CashShopStageChildWindow inventoryWindow)
            {
                inventoryWindow.SetFont(_fontChat);
                inventoryWindow.SetContentProvider(cashShopWindow.DescribeInventoryOwnerState);
                inventoryWindow.SetInventoryStateProvider(() =>
                {
                    AdminShopDialogUI.InventoryOwnerSnapshot snapshot = cashShopWindow.GetInventoryOwnerSnapshot();
                    return new CashShopStageChildWindow.InventoryOwnerState
                    {
                        EquipCount = snapshot.EquipCount,
                        UseCount = snapshot.UseCount,
                        SetupCount = snapshot.SetupCount,
                        EtcCount = snapshot.EtcCount,
                        CashCount = snapshot.CashCount,
                        ScrollOffset = 0,
                        WheelRange = 140,
                        HasNumberFont = true,
                        SelectedEntryTitle = snapshot.SelectedEntryTitle
                    };
                });
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
                listWindow.SetExternalAction("BtBuy", () => cashShopWindow.ExecuteCashStageListAction("BtBuy"));
                listWindow.SetExternalAction("BtGift", () => cashShopWindow.ExecuteCashStageListAction("BtGift"));
                listWindow.SetExternalAction("BtReserve", () => cashShopWindow.ExecuteCashStageListAction("BtReserve"));
                listWindow.SetExternalAction("BtRemove", () => cashShopWindow.ExecuteCashStageListAction("BtRemove"));
                listWindow.SetExternalAction("TogglePane", cashShopWindow.ToggleListOwnerPane);
                listWindow.SetExternalAction("PageUp", () => cashShopWindow.MoveListOwnerSelectionByPage(-1));
                listWindow.SetExternalAction("PageDown", () => cashShopWindow.MoveListOwnerSelectionByPage(1));
                listWindow.SetExternalAction("Home", () => cashShopWindow.SelectListOwnerBoundary(false));
                listWindow.SetExternalAction("End", () => cashShopWindow.SelectListOwnerBoundary(true));
                listWindow.SetListStateProvider(() =>
                {
                    AdminShopDialogUI.ListOwnerSnapshot snapshot = cashShopWindow.GetListOwnerSnapshot();
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

                    IReadOnlyList<string> recentPackets = uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) is CashServiceStageWindow stageWindow
                        ? stageWindow.GetRecentPacketSummaries()
                        : Array.Empty<string>();
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
                });
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
                        StatusMessage = stageWindow.StatusMessage
                    };
                });
                statusWindow.SetExternalAction("BtCharge", () => "CCSWnd_Status kept the dedicated charge button armed; live billing flow remains outside the simulator.");
                statusWindow.SetExternalAction("BtCheck", () => BuildCashShopStatusOwnerLines()[0]);
                statusWindow.SetExternalAction("BtCoupon", () => "CCSWnd_Status routed into the coupon-registration seam; packet-backed coupon redemption still remains unimplemented.");
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

                    return new CashShopStageChildWindow.OneADayOwnerState
                    {
                        IsPending = stageWindow.IsOneADayPending,
                        NoticeState = stageWindow.NoticeState,
                        SelectorIndex = stageWindow.IsOneADayPending ? 0 : 1,
                        Hour = 0,
                        Minute = stageWindow.IsOneADayPending ? 0 : 59,
                        Second = stageWindow.IsOneADayPending ? 30 : 0,
                        RecentPackets = stageWindow.GetRecentPacketSummaries()
                    };
                });
                oneADayWindow.SetExternalAction("BtJoin", () => "CCSWnd_OneADay joined the packet-armed reward session preview.");
                oneADayWindow.SetExternalAction("BtShortcut", () => "CCSWnd_OneADay switched focus to the shortcut-help plate owner.");
                oneADayWindow.SetExternalAction("BtClose", () => "CCSWnd_OneADay dismissed the current reward plate preview.");
            }
        }

        private IReadOnlyList<string> BuildCashShopListOwnerLines(AdminShopDialogUI cashShopWindow)
        {
            List<string> lines = new(cashShopWindow?.DescribeListOwnerState() ?? Array.Empty<string>());
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage) is CashServiceStageWindow stageWindow)
            {
                foreach (string recentPacket in stageWindow.GetRecentPacketSummaries())
                {
                    lines.Add(recentPacket);
                }
            }

            return lines;
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

            return new[]
            {
                balanceLine,
                stageWindow.StatusMessage
            };
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

            List<string> lines = new()
            {
                stageWindow.IsOneADayPending
                    ? "Packet 395 has armed the dedicated one-a-day owner."
                    : "No one-a-day packet is currently pending.",
                stageWindow.NoticeState
            };

            foreach (string recentPacket in stageWindow.GetRecentPacketSummaries())
            {
                lines.Add(recentPacket);
            }

            return lines;
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
                listWindow.SetListStateProvider(() =>
                {
                    AdminShopDialogUI.ListOwnerSnapshot snapshot = mtsWindow?.GetListOwnerSnapshot();
                    if (snapshot == null)
                    {
                        List<CashShopStageChildWindow.ListOwnerEntryState> fallbackEntries = new();
                        if (mtsStageWindow.ItcNormalItemMutationCount > 0)
                        {
                            fallbackEntries.Add(new CashShopStageChildWindow.ListOwnerEntryState
                            {
                                Title = $"Listing {mtsStageWindow.ItcNormalItemSelectedListingId.ToString(CultureInfo.InvariantCulture)}",
                                Detail = mtsStageWindow.ItcNormalItemLastSummary,
                                Seller = "CITC packet owner",
                                PriceLabel = mtsStageWindow.ItcNormalItemSelectedPrice.ToString("N0", CultureInfo.InvariantCulture),
                                StateLabel = $"Subtype {mtsStageWindow.ItcNormalItemSubtype.ToString(CultureInfo.InvariantCulture)}",
                                IsSelected = true
                            });
                        }

                        return new CashShopStageChildWindow.ListOwnerState
                        {
                            PaneLabel = "CITC list",
                            BrowseModeLabel = $"Sort {mtsStageWindow.ItcNormalItemSortType.ToString(CultureInfo.InvariantCulture)}",
                            CategoryLabel = $"Category {mtsStageWindow.ItcNormalItemCategory.ToString(CultureInfo.InvariantCulture)}",
                            FooterMessage = mtsStageWindow.ItcNormalItemLastSummary,
                            SelectedEntryDetail = fallbackEntries.Count > 0 ? fallbackEntries[0].Detail : string.Empty,
                            SelectedIndex = fallbackEntries.Count > 0 ? 0 : -1,
                            ScrollOffset = 0,
                            TotalCount = Math.Max(mtsStageWindow.ItcNormalItemEntryCount, fallbackEntries.Count),
                            PlateFocusIndex = fallbackEntries.Count > 0 ? 0 : -1,
                            HasKeyFocusCanvas = true,
                            VisibleEntries = fallbackEntries,
                            RecentPackets = mtsStageWindow.GetRecentPacketSummaries()
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
                        RecentPackets = mtsStageWindow.GetRecentPacketSummaries()
                    };
                });
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
                    StatusMessage = mtsStageWindow.StatusMessage
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
            foreach (string recentPacket in mtsStageWindow?.GetRecentPacketSummaries() ?? Array.Empty<string>())
            {
                lines.Add(recentPacket);
            }

            return lines;
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
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.CashShop);
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.CashAvatarPreview);
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.CashShopStage);
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.CashTradingRoom);
            for (int i = 0; i < CashShopChildOwnerWindowNames.Length; i++)
            {
                uiWindowManager?.HideWindow(CashShopChildOwnerWindowNames[i]);
            }

            RefreshCashServiceStageBgmOverride();
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
