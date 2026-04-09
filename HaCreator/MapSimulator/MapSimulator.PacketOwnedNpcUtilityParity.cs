using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly PacketOwnedShopDialogRuntime _packetOwnedNpcShopRuntime = new();
        private readonly PacketOwnedStoreBankDialogRuntime _packetOwnedStoreBankRuntime = new();
        private readonly PacketOwnedBattleRecordRuntime _packetOwnedBattleRecordRuntime = new();
        private int _lastPacketOwnedNpcShopOutboundOpcode = -1;
        private byte[] _lastPacketOwnedNpcShopOutboundPayload = Array.Empty<byte>();
        private string _lastPacketOwnedNpcShopOutboundSummary;
        private int _lastPacketOwnedStoreBankOutboundOpcode = -1;
        private byte[] _lastPacketOwnedStoreBankOutboundPayload = Array.Empty<byte>();
        private string _lastPacketOwnedStoreBankOutboundSummary;

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

                    if (packetType == 422 && !_packetOwnedBattleRecordRuntime.IsOpen)
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

                case 366:
                case 367:
                    return TryApplyPacketOwnedAdminShopPacket(packetType, payload, out message);

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
            return $"{_packetOwnedNpcShopRuntime.BuildFooter()} {DescribePacketOwnedNpcUtilityOutboundStatus("shop", _lastPacketOwnedNpcShopOutboundOpcode, _lastPacketOwnedNpcShopOutboundPayload, _lastPacketOwnedNpcShopOutboundSummary)}";
        }

        private IReadOnlyList<string> BuildPacketOwnedStoreBankLines()
        {
            return _packetOwnedStoreBankRuntime.BuildLines();
        }

        private string BuildPacketOwnedStoreBankFooter()
        {
            return $"{_packetOwnedStoreBankRuntime.BuildFooter()} {DescribePacketOwnedNpcUtilityOutboundStatus("store-bank", _lastPacketOwnedStoreBankOutboundOpcode, _lastPacketOwnedStoreBankOutboundPayload, _lastPacketOwnedStoreBankOutboundSummary)}";
        }

        private IReadOnlyList<string> BuildPacketOwnedBattleRecordLines()
        {
            return _packetOwnedBattleRecordRuntime.BuildLines();
        }

        private string BuildPacketOwnedBattleRecordFooter()
        {
            return _packetOwnedBattleRecordRuntime.BuildFooter();
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedNpcUtilityCommand(string[] args)
        {
            if (args == null || args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribePacketOwnedNpcUtilityStatus());
            }

            switch (args[0].ToLowerInvariant())
            {
                case "packet":
                case "packetraw":
                    return HandlePacketOwnedNpcUtilityPacketCommand(args);

                case "shop":
                case "npcshop":
                    return HandlePacketOwnedNpcShopCommand(args.Skip(1).ToArray());

                case "storebank":
                    return HandlePacketOwnedStoreBankCommand(args.Skip(1).ToArray());

                case "battlerecord":
                    return HandlePacketOwnedBattleRecordCommand(args.Skip(1).ToArray());

                default:
                    return ChatCommandHandler.CommandResult.Error("Usage: /npcutility [status|packet <364|365|366|367|369|370|420|421|422|423> [payloadhex=..|payloadb64=..]|packetraw <364|365|366|367|369|370|420|421|422|423> <hex>|shop [status|show|buy <itemId> [quantity]|sell <itemId> [quantity]|recharge <itemId> [targetQuantity]|close]|storebank [status|show|getall|close]|battlerecord [status|show|page <summary|dot|packets>|close]]");
            }
        }

        private string DescribePacketOwnedNpcUtilityStatus()
        {
            return string.Join(
                Environment.NewLine,
                new[]
                {
                    "Packet-owned NPC utility owner family:",
                    $"Shop: {BuildPacketOwnedNpcShopFooter()}",
                    $"StoreBank: {BuildPacketOwnedStoreBankFooter()}",
                    $"BattleRecord: {_packetOwnedBattleRecordRuntime.BuildFooter()}"
                });
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedNpcUtilityPacketCommand(string[] args)
        {
            bool rawHex = string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase);
            if (args.Length < 2
                || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int packetType)
                || packetType is not (364 or 365 or 366 or 367 or 369 or 370 or 420 or 421 or 422 or 423))
            {
                return ChatCommandHandler.CommandResult.Error(rawHex
                    ? "Usage: /npcutility packetraw <364|365|366|367|369|370|420|421|422|423> <hex>"
                    : "Usage: /npcutility packet <364|365|366|367|369|370|420|421|422|423> [payloadhex=..|payloadb64=..]");
            }

            byte[] payload = Array.Empty<byte>();
            if (rawHex)
            {
                if (args.Length < 3 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(2)), out payload))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /npcutility packetraw <364|365|366|367|369|370|420|421|422|423> <hex>");
                }
            }
            else if (args.Length >= 3 && !TryParseBinaryPayloadArgument(args[2], out payload, out string payloadError))
            {
                return ChatCommandHandler.CommandResult.Error(payloadError ?? "Usage: /npcutility packet <364|365|366|367|369|370|420|421|422|423> [payloadhex=..|payloadb64=..]");
            }

            return TryApplyPacketOwnedNpcUtilityPacket(packetType, payload, out string message)
                ? ChatCommandHandler.CommandResult.Ok(message)
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private bool TryApplyPacketOwnedAdminShopPacket(int packetType, byte[] payload, out string message)
        {
            message = "Packet-owned admin-shop owner is unavailable.";
            payload ??= Array.Empty<byte>();

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShop) is not AdminShopDialogUI adminShopWindow)
            {
                return false;
            }

            if (packetType == 367)
            {
                if (payload.Length < sizeof(int) + sizeof(ushort))
                {
                    message = "Admin-shop packet 367 requires NPC template id and item-count payload fields.";
                    return false;
                }

                int npcTemplateId = BitConverter.ToInt32(payload, 0);
                int itemCount = BitConverter.ToUInt16(payload, sizeof(int));
                if (itemCount > 0)
                {
                    if (!adminShopWindow.TryBeginPacketOwnedAdminShopSession(payload, out message))
                    {
                        return false;
                    }

                    message = ShowPacketOwnedUniqueUtilityWindow(MapSimulatorWindowNames.CashShop, "Admin Shop", message);
                    return true;
                }

                string rejectionNotice = AdminShopDialogClientParityText.GetOpenRejectedNotice();
                message = adminShopWindow.ApplyPacketOwnedAdminShopOpenRejected(rejectionNotice);
                HideCashShopOwnerFamilyWindows();
                ShowPacketOwnedNoticeDialog(rejectionNotice);
                return true;
            }

            if (payload.Length < 2)
            {
                message = "Admin-shop packet 366 requires subtype and result-code bytes.";
                return false;
            }

            byte subtype = payload[0];
            byte resultCode = payload[1];
            bool applied = adminShopWindow.TryApplyPacketOwnedAdminShopResult(
                subtype,
                resultCode,
                out message,
                out string notice,
                out bool reopenRequested);
            if (!applied)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(notice))
            {
                ShowPacketOwnedNoticeDialog(notice);
            }

            if (reopenRequested)
            {
                message = ShowPacketOwnedUniqueUtilityWindow(MapSimulatorWindowNames.CashShop, "Admin Shop", message);
            }

            return true;
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedNpcShopCommand(string[] args)
        {
            if (args == null || args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(BuildPacketOwnedNpcShopFooter());
            }

            switch (args[0].ToLowerInvariant())
            {
                case "show":
                case "open":
                    return ChatCommandHandler.CommandResult.Ok(
                        ShowPacketOwnedUniqueUtilityWindow(
                            MapSimulatorWindowNames.NpcShop,
                            "NPC Shop",
                            BuildPacketOwnedNpcShopFooter()));

                case "buy":
                    if (args.Length < 2
                        || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int buyItemId))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /npcutility shop buy <itemId> [quantity]");
                    }

                    int buyQuantity = 1;
                    if (args.Length >= 3
                        && (!int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out buyQuantity) || buyQuantity <= 0))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /npcutility shop buy <itemId> [quantity]");
                    }

                    if (uiWindowManager?.InventoryWindow is not IInventoryRuntime buyInventory)
                    {
                        return ChatCommandHandler.CommandResult.Error("NPC shop buy request requires the inventory window runtime.");
                    }

                    bool hasBuyOutbound = _packetOwnedNpcShopRuntime.TryBuildBuyOutboundRequest(
                        buyItemId,
                        buyQuantity,
                        out PacketOwnedNpcUtilityOutboundRequest buyRequest,
                        out string buyOutboundError);
                    return _packetOwnedNpcShopRuntime.TryApplyLocalBuy(buyInventory, buyItemId, buyQuantity, out string buyMessage)
                        ? ChatCommandHandler.CommandResult.Ok(DispatchPacketOwnedNpcShopOutboundRequest(hasBuyOutbound, buyRequest, buyMessage, buyOutboundError))
                        : ChatCommandHandler.CommandResult.Error(buyMessage);

                case "sell":
                    if (args.Length < 2
                        || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int sellItemId))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /npcutility shop sell <itemId> [quantity]");
                    }

                    int sellQuantity = 1;
                    if (args.Length >= 3
                        && (!int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out sellQuantity) || sellQuantity <= 0))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /npcutility shop sell <itemId> [quantity]");
                    }

                    if (uiWindowManager?.InventoryWindow is not IInventoryRuntime sellInventory)
                    {
                        return ChatCommandHandler.CommandResult.Error("NPC shop sell request requires the inventory window runtime.");
                    }

                    bool hasSellOutbound = _packetOwnedNpcShopRuntime.TryBuildSellOutboundRequest(
                        sellInventory,
                        sellItemId,
                        sellQuantity,
                        out PacketOwnedNpcUtilityOutboundRequest sellRequest,
                        out string sellOutboundError);
                    return _packetOwnedNpcShopRuntime.TryApplyLocalSell(sellInventory, sellItemId, sellQuantity, out string sellMessage)
                        ? ChatCommandHandler.CommandResult.Ok(DispatchPacketOwnedNpcShopOutboundRequest(hasSellOutbound, sellRequest, sellMessage, sellOutboundError))
                        : ChatCommandHandler.CommandResult.Error(sellMessage);

                case "recharge":
                    if (args.Length < 2
                        || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int rechargeItemId))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /npcutility shop recharge <itemId> [targetQuantity]");
                    }

                    int targetQuantity = 0;
                    if (args.Length >= 3
                        && !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out targetQuantity))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /npcutility shop recharge <itemId> [targetQuantity]");
                    }

                    if (uiWindowManager?.InventoryWindow is not IInventoryRuntime rechargeInventory)
                    {
                        return ChatCommandHandler.CommandResult.Error("NPC shop recharge request requires the inventory window runtime.");
                    }

                    bool hasRechargeOutbound = _packetOwnedNpcShopRuntime.TryBuildRechargeOutboundRequest(
                        rechargeInventory,
                        rechargeItemId,
                        out PacketOwnedNpcUtilityOutboundRequest rechargeRequest,
                        out string rechargeOutboundError);
                    return _packetOwnedNpcShopRuntime.TryApplyLocalRecharge(rechargeInventory, rechargeItemId, targetQuantity, out string rechargeMessage)
                        ? ChatCommandHandler.CommandResult.Ok(DispatchPacketOwnedNpcShopOutboundRequest(hasRechargeOutbound, rechargeRequest, rechargeMessage, rechargeOutboundError))
                        : ChatCommandHandler.CommandResult.Error(rechargeMessage);

                case "close":
                    _packetOwnedNpcShopRuntime.Close();
                    uiWindowManager?.HideWindow(MapSimulatorWindowNames.NpcShop);
                    return ChatCommandHandler.CommandResult.Ok(BuildPacketOwnedNpcShopFooter());

                default:
                    return ChatCommandHandler.CommandResult.Error("Usage: /npcutility shop [status|show|buy <itemId> [quantity]|sell <itemId> [quantity]|recharge <itemId> [targetQuantity]|close]");
            }
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedStoreBankCommand(string[] args)
        {
            if (args == null || args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(BuildPacketOwnedStoreBankFooter());
            }

            switch (args[0].ToLowerInvariant())
            {
                case "show":
                case "open":
                    return ChatCommandHandler.CommandResult.Ok(
                        ShowPacketOwnedUniqueUtilityWindow(
                            MapSimulatorWindowNames.StoreBank,
                            "Store Bank",
                            BuildPacketOwnedStoreBankFooter()));

                case "getall":
                case "accept":
                    bool hasGetAllOutbound = _packetOwnedStoreBankRuntime.TryBuildPendingGetAllOutboundRequest(
                        out PacketOwnedNpcUtilityOutboundRequest getAllRequest,
                        out string getAllOutboundError);
                    return ChatCommandHandler.CommandResult.Ok(
                        DispatchPacketOwnedStoreBankOutboundRequest(
                            hasGetAllOutbound,
                            getAllRequest,
                            _packetOwnedStoreBankRuntime.ConsumePendingGetAllRequest(),
                            getAllOutboundError));

                case "close":
                    _packetOwnedStoreBankRuntime.Close();
                    uiWindowManager?.HideWindow(MapSimulatorWindowNames.StoreBank);
                    return ChatCommandHandler.CommandResult.Ok(BuildPacketOwnedStoreBankFooter());

                default:
                    return ChatCommandHandler.CommandResult.Error("Usage: /npcutility storebank [status|show|getall|close]");
            }
        }

        private string DispatchPacketOwnedNpcShopOutboundRequest(
            bool hasRequest,
            PacketOwnedNpcUtilityOutboundRequest request,
            string localMessage,
            string requestError)
        {
            return DispatchPacketOwnedNpcUtilityOutboundRequest(
                hasRequest,
                request,
                localMessage,
                requestError,
                ref _lastPacketOwnedNpcShopOutboundOpcode,
                ref _lastPacketOwnedNpcShopOutboundPayload,
                ref _lastPacketOwnedNpcShopOutboundSummary);
        }

        private string DispatchPacketOwnedStoreBankOutboundRequest(
            bool hasRequest,
            PacketOwnedNpcUtilityOutboundRequest request,
            string localMessage,
            string requestError)
        {
            return DispatchPacketOwnedNpcUtilityOutboundRequest(
                hasRequest,
                request,
                localMessage,
                requestError,
                ref _lastPacketOwnedStoreBankOutboundOpcode,
                ref _lastPacketOwnedStoreBankOutboundPayload,
                ref _lastPacketOwnedStoreBankOutboundSummary);
        }

        private string DispatchPacketOwnedNpcUtilityOutboundRequest(
            bool hasRequest,
            PacketOwnedNpcUtilityOutboundRequest request,
            string localMessage,
            string requestError,
            ref int lastOpcode,
            ref byte[] lastPayload,
            ref string lastSummary)
        {
            if (!hasRequest)
            {
                lastOpcode = -1;
                lastPayload = Array.Empty<byte>();
                lastSummary = string.IsNullOrWhiteSpace(requestError)
                    ? "No outbound request was generated."
                    : requestError;
                return string.IsNullOrWhiteSpace(requestError)
                    ? localMessage
                    : $"{localMessage} {requestError}";
            }

            lastOpcode = request.Opcode;
            lastPayload = request.Payload?.ToArray() ?? Array.Empty<byte>();
            lastSummary = request.Summary;

            if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(request.Opcode, request.Payload, out string bridgeStatus))
            {
                lastSummary = $"{request.Summary} Dispatched through the live official-session bridge. {bridgeStatus}";
                return $"{localMessage} {lastSummary}";
            }

            if (_localUtilityPacketOutbox.TrySendOutboundPacket(request.Opcode, request.Payload, out string outboxStatus))
            {
                lastSummary = $"{request.Summary} Dispatched through the generic packet outbox after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus}";
                return $"{localMessage} {lastSummary}";
            }

            if (_localUtilityOfficialSessionBridge.IsRunning
                && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(request.Opcode, request.Payload, out string queuedBridgeStatus))
            {
                lastSummary = $"{request.Summary} Queued for deferred official-session injection after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred bridge: {queuedBridgeStatus}";
                return $"{localMessage} {lastSummary}";
            }

            if (_localUtilityPacketOutbox.TryQueueOutboundPacket(request.Opcode, request.Payload, out string queuedOutboxStatus))
            {
                lastSummary = $"{request.Summary} Queued for deferred generic packet outbox delivery after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred outbox: {queuedOutboxStatus}";
                return $"{localMessage} {lastSummary}";
            }

            lastSummary = $"{request.Summary} The request remained simulator-owned because neither the live bridge nor the packet outbox accepted opcode {request.Opcode}. Bridge: {bridgeStatus} Outbox: {outboxStatus}";
            return $"{localMessage} {lastSummary}";
        }

        private static string DescribePacketOwnedNpcUtilityOutboundStatus(string ownerLabel, int opcode, byte[] payload, string summary)
        {
            if (opcode < 0)
            {
                return string.IsNullOrWhiteSpace(summary)
                    ? $"{ownerLabel} outbound=idle."
                    : $"{ownerLabel} outbound=idle ({summary})";
            }

            string payloadHex = Convert.ToHexString(payload ?? Array.Empty<byte>());
            return string.IsNullOrWhiteSpace(summary)
                ? $"{ownerLabel} outbound={opcode}[{payloadHex}]."
                : $"{ownerLabel} outbound={opcode}[{payloadHex}] ({summary})";
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedBattleRecordCommand(string[] args)
        {
            if (args == null || args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(_packetOwnedBattleRecordRuntime.BuildFooter());
            }

            switch (args[0].ToLowerInvariant())
            {
                case "show":
                case "open":
                    return ChatCommandHandler.CommandResult.Ok(
                        ShowPacketOwnedUniqueUtilityWindow(
                            MapSimulatorWindowNames.BattleRecord,
                            "Battle Record",
                            _packetOwnedBattleRecordRuntime.BuildFooter()));

                case "page":
                    if (args.Length < 2)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /npcutility battlerecord page <summary|dot|packets>");
                    }

                    int pageIndex = args[1].ToLowerInvariant() switch
                    {
                        "summary" => 0,
                        "dot" => 1,
                        "packets" => 2,
                        _ => -1
                    };
                    if (pageIndex < 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /npcutility battlerecord page <summary|dot|packets>");
                    }

                    _packetOwnedBattleRecordRuntime.SelectPage(pageIndex);
                    return ChatCommandHandler.CommandResult.Ok(_packetOwnedBattleRecordRuntime.BuildFooter());

                case "close":
                    _packetOwnedBattleRecordRuntime.Close();
                    uiWindowManager?.HideWindow(MapSimulatorWindowNames.BattleRecord);
                    return ChatCommandHandler.CommandResult.Ok(_packetOwnedBattleRecordRuntime.BuildFooter());

                default:
                    return ChatCommandHandler.CommandResult.Error("Usage: /npcutility battlerecord [status|show|page <summary|dot|packets>|close]");
            }
        }
    }
}
