using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly PacketOwnedShopDialogRuntime _packetOwnedNpcShopRuntime = new();
        private readonly PacketOwnedStoreBankDialogRuntime _packetOwnedStoreBankRuntime = new();
        private readonly PacketOwnedBattleRecordRuntime _packetOwnedBattleRecordRuntime = new();

        private bool TryApplyPacketOwnedNpcUtilityPacket(int packetType, byte[] payload, out string message)
        {
            message = null;
            payload ??= Array.Empty<byte>();

            switch (packetType)
            {
                case 364:
                case 365:
                {
                    bool applied = _packetOwnedNpcShopRuntime.TryApplyPacket(packetType, payload, out message);
                    if (applied && packetType == 364)
                    {
                        message = ShowPacketOwnedUniqueUtilityWindow(MapSimulatorWindowNames.NpcShop, "NPC Shop", message);
                    }

                    return applied;
                }

                case 369:
                case 370:
                {
                    bool applied = _packetOwnedStoreBankRuntime.TryApplyPacket(packetType, payload, out message);
                    if (applied && packetType == 370 && payload.Length > 0 && payload[0] == 35)
                    {
                        message = ShowPacketOwnedUniqueUtilityWindow(MapSimulatorWindowNames.StoreBank, "Store Bank", message);
                    }

                    return applied;
                }

                case 420:
                case 421:
                case 422:
                case 423:
                {
                    bool applied = _packetOwnedBattleRecordRuntime.TryApplyPacket(packetType, payload, out message);
                    if (!applied)
                    {
                        return false;
                    }

                    if (packetType == 423 || (packetType == 422 && !_packetOwnedBattleRecordRuntime.IsOpen))
                    {
                        uiWindowManager?.HideWindow(MapSimulatorWindowNames.BattleRecord);
                        return true;
                    }

                    if (_packetOwnedBattleRecordRuntime.IsOpen)
                    {
                        message = ShowPacketOwnedUniqueUtilityWindow(MapSimulatorWindowNames.BattleRecord, "Battle Record", message);
                    }

                    return true;
                }

                default:
                    return false;
            }
        }

        private string ShowPacketOwnedUniqueUtilityWindow(string windowName, string displayName, string defaultMessage)
        {
            if (uiWindowManager == null)
            {
                return defaultMessage ?? $"{displayName} owner is not available because the UI window manager is missing.";
            }

            string blockingOwner = GetVisibleUniqueModelessOwner(windowName);
            if (!string.IsNullOrWhiteSpace(blockingOwner))
            {
                return $"{defaultMessage} {displayName} stayed in status-only mode because {blockingOwner} is already visible.";
            }

            ShowWindowWithInheritedDirectionModeOwner(windowName);
            if (uiWindowManager.GetWindow(windowName) is UIWindowBase window)
            {
                uiWindowManager.BringToFront(window);
            }

            return defaultMessage;
        }

        private IReadOnlyList<string> BuildPacketOwnedNpcShopLines()
        {
            return _packetOwnedNpcShopRuntime.BuildLines();
        }

        private string BuildPacketOwnedNpcShopFooter()
        {
            return _packetOwnedNpcShopRuntime.BuildFooter();
        }

        private IReadOnlyList<string> BuildPacketOwnedStoreBankLines()
        {
            return _packetOwnedStoreBankRuntime.BuildLines();
        }

        private string BuildPacketOwnedStoreBankFooter()
        {
            return _packetOwnedStoreBankRuntime.BuildFooter();
        }

        private IReadOnlyList<string> BuildPacketOwnedBattleRecordLines()
        {
            return _packetOwnedBattleRecordRuntime.BuildLines();
        }

        private string BuildPacketOwnedBattleRecordFooter()
        {
            return _packetOwnedBattleRecordRuntime.BuildFooter();
        }
    }
}
