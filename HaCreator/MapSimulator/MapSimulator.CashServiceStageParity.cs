using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;
using System.Globalization;
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

        private readonly CashServicePacketInboxManager _cashServicePacketInbox = new();

        private void WireCashServiceOwnerWindows()
        {
            IInventoryRuntime inventoryRuntime = uiWindowManager?.InventoryWindow as IInventoryRuntime;

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShop) is AdminShopDialogUI cashShopWindow)
            {
                cashShopWindow.SetInventory(inventoryRuntime);
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
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashAvatarPreview) is CashAvatarPreviewWindow cashAvatarPreviewWindow
                && _playerManager?.Player?.Build != null)
            {
                cashAvatarPreviewWindow.CharacterBuild = _playerManager.Player.Build;
                cashAvatarPreviewWindow.SetFont(_fontChat);
                cashAvatarPreviewWindow.EquipmentLoader = _playerManager.Loader != null ? _playerManager.Loader.LoadEquipment : null;
                cashAvatarPreviewWindow.PersonalShopRequested = () =>
                {
                    ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.PersonalShop);
                    return "CCSWnd_Char::ShowPersonalShop opened the dedicated personal-shop owner.";
                };
                cashAvatarPreviewWindow.EntrustedShopRequested = () =>
                {
                    ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.EntrustedShop);
                    return "CCSWnd_Char::ShowEntrustedShop opened the dedicated entrusted-shop owner.";
                };
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
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.MtsStatus) is CashServiceStageWindow mtsStageWindow)
            {
                mtsStageWindow.SetFont(_fontChat);
                mtsStageWindow.SetCharacterBuild(_playerManager?.Player?.Build);
                mtsStageWindow.SetInventory(inventoryRuntime);
            }
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
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopInventory) is CashShopStageChildWindow inventoryWindow)
            {
                inventoryWindow.SetFont(_fontChat);
                inventoryWindow.SetContentProvider(cashShopWindow.DescribeInventoryOwnerState);
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopList) is CashShopStageChildWindow listWindow)
            {
                listWindow.SetFont(_fontChat);
                listWindow.SetContentProvider(() => BuildCashShopListOwnerLines(cashShopWindow));
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStatus) is CashShopStageChildWindow statusWindow)
            {
                statusWindow.SetFont(_fontChat);
                statusWindow.SetContentProvider(BuildCashShopStatusOwnerLines);
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopOneADay) is CashShopStageChildWindow oneADayWindow)
            {
                oneADayWindow.SetFont(_fontChat);
                oneADayWindow.SetContentProvider(BuildCashShopOneADayOwnerLines);
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

        private void HideCashShopOwnerFamilyWindows()
        {
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.CashShop);
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.CashAvatarPreview);
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.CashShopStage);
            for (int i = 0; i < CashShopChildOwnerWindowNames.Length; i++)
            {
                uiWindowManager?.HideWindow(CashShopChildOwnerWindowNames[i]);
            }
        }

        private void ShowCashShopChildOwnerWindows()
        {
            for (int i = 0; i < CashShopChildOwnerWindowNames.Length; i++)
            {
                ShowDirectionModeOwnedWindow(CashShopChildOwnerWindowNames[i]);
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

            string primaryWindowName = stageKind == CashServiceOwnerStageKind.CashShop
                ? MapSimulatorWindowNames.CashShop
                : MapSimulatorWindowNames.Mts;
            string stageWindowName = stageKind == CashServiceOwnerStageKind.CashShop
                ? MapSimulatorWindowNames.CashShopStage
                : MapSimulatorWindowNames.MtsStatus;
            string opposingPrimaryWindowName = stageKind == CashServiceOwnerStageKind.CashShop
                ? MapSimulatorWindowNames.Mts
                : MapSimulatorWindowNames.CashShop;
            string opposingStageWindowName = stageKind == CashServiceOwnerStageKind.CashShop
                ? MapSimulatorWindowNames.MtsStatus
                : MapSimulatorWindowNames.CashShopStage;

            uiWindowManager?.HideWindow(opposingPrimaryWindowName);
            uiWindowManager?.HideWindow(opposingStageWindowName);
            if (stageKind == CashServiceOwnerStageKind.CashShop)
            {
                ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.CashShopStage);
                ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.CashShop);
                ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.CashAvatarPreview);
                ShowCashShopChildOwnerWindows();
                SyncCashServiceStageWindowState(MapSimulatorWindowNames.CashShopStage, stageKind, resetStageSession);
                return;
            }

            HideCashShopOwnerFamilyWindows();
            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.MtsStatus);
            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.Mts);
            SyncCashServiceStageWindowState(MapSimulatorWindowNames.MtsStatus, stageKind, resetStageSession);
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
                if (packetType == 384)
                {
                    TryFocusCashServiceCommodity(_lastPacketOwnedCommoditySerialNumber);
                }

                return applied;
            }

            OpenCashServiceOwnerFamily(CashServiceOwnerStageKind.ItemTradingCenter, resetStageSession: false);
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.MtsStatus) is not CashServiceStageWindow mtsStageWindow)
            {
                message = "ITC stage owner is not available in this UI build.";
                return false;
            }

            return mtsStageWindow.TryApplyPacket(packetType, payload, currTickCount, out message);
        }

        private bool ShouldRunCashServicePacketInbox()
        {
            bool anyCashShopChildVisible = false;
            for (int i = 0; i < CashShopChildOwnerWindowNames.Length && !anyCashShopChildVisible; i++)
            {
                anyCashShopChildVisible = uiWindowManager?.GetWindow(CashShopChildOwnerWindowNames[i])?.IsVisible == true;
            }

            return uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShop)?.IsVisible == true
                || uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShopStage)?.IsVisible == true
                || uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashAvatarPreview)?.IsVisible == true
                || anyCashShopChildVisible
                || uiWindowManager?.GetWindow(MapSimulatorWindowNames.Mts)?.IsVisible == true
                || uiWindowManager?.GetWindow(MapSimulatorWindowNames.MtsStatus)?.IsVisible == true;
        }
    }
}
