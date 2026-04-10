using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
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
        private int _lastPacketOwnedBattleRecordOutboundOpcode = -1;
        private byte[] _lastPacketOwnedBattleRecordOutboundPayload = Array.Empty<byte>();
        private string _lastPacketOwnedBattleRecordOutboundSummary;
        private bool _packetOwnedStoreBankGetAllPromptActive;

        private bool TryApplyPacketOwnedNpcUtilityPacket(int packetType, byte[] payload, out string message)
        {
            message = null;
            payload ??= Array.Empty<byte>();

            switch (packetType)
            {
                case LocalUtilityPacketInboxManager.AdminShopResultClientPacketType:
                case LocalUtilityPacketInboxManager.AdminShopOpenClientPacketType:
                    return TryApplyPacketOwnedAdminShopPacket(packetType, payload, out message);

                case 364:
                case 365:
                {
                    bool applied = _packetOwnedNpcShopRuntime.TryApplyPacket(packetType, payload, out message);
                    if (applied && packetType == 364)
                    {
                        PublishDynamicObjectTagStatesForNpc(_packetOwnedNpcShopRuntime.NpcTemplateId, currTickCount);
                        message = ShowPacketOwnedUniqueUtilityWindow(MapSimulatorWindowNames.NpcShop, "NPC Shop", message);
                    }

                    return applied;
                }

                case 369:
                case 370:
                {
                    bool applied = _packetOwnedStoreBankRuntime.TryApplyPacket(packetType, payload, out message);
                    if (applied && !_packetOwnedStoreBankRuntime.HasPendingGetAllRequest)
                    {
                        HidePacketOwnedStoreBankGetAllPrompt();
                    }

                    if (applied && packetType == 370 && payload.Length > 0 && payload[0] == 35)
                    {
                        PublishDynamicObjectTagStatesForNpc(_packetOwnedStoreBankRuntime.NpcTemplateId, currTickCount);
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

                default:
                    return false;
            }
        }

        internal static bool IsPacketOwnedNpcUtilityPacketType(int packetType)
        {
            return packetType is 364 or 365 or 369 or 370 or 420 or 421 or 422 or 423
                || packetType == LocalUtilityPacketInboxManager.AdminShopResultClientPacketType
                || packetType == LocalUtilityPacketInboxManager.AdminShopOpenClientPacketType;
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

        private string BuildPacketOwnedAdminShopFooter()
        {
            return uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShop) is AdminShopDialogUI adminShopWindow
                ? adminShopWindow.BuildPacketOwnedAdminShopOwnerFooter()
                : "admin-shop outbound=idle.";
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
            return $"{_packetOwnedBattleRecordRuntime.BuildFooter()} {DescribePacketOwnedNpcUtilityOutboundStatus("battle-record", _lastPacketOwnedBattleRecordOutboundOpcode, _lastPacketOwnedBattleRecordOutboundPayload, _lastPacketOwnedBattleRecordOutboundSummary)}";
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
                    $"AdminShop: {BuildPacketOwnedAdminShopFooter()}",
                    $"StoreBank: {BuildPacketOwnedStoreBankFooter()}",
                $"BattleRecord: {BuildPacketOwnedBattleRecordFooter()}"
                });
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedNpcUtilityPacketCommand(string[] args)
        {
            bool rawHex = string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase);
            if (args.Length < 2
                || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int packetType)
                || !IsPacketOwnedNpcUtilityPacketType(packetType))
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

        private ChatCommandHandler.CommandResult HandlePacketOwnedAdminShopCommand(string[] args)
        {
            if (args == null || args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info($"{DescribeAdminShopPacketInboxStatus()} {BuildPacketOwnedAdminShopFooter()} {_adminShopPacketInbox.LastStatus}");
            }

            switch (args[0].ToLowerInvariant())
            {
                case "inbox":
                    return HandlePacketOwnedAdminShopInboxCommand(args.Skip(1).ToArray());

                case "packet":
                case "packetraw":
                    return HandlePacketOwnedAdminShopPacketCommand(args);

                case "packetclientraw":
                    return HandlePacketOwnedAdminShopClientPacketRawCommand(args);

                case "show":
                case "open":
                    if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShop) is AdminShopDialogUI adminShopWindow)
                    {
                        return ChatCommandHandler.CommandResult.Ok(
                            ShowPacketOwnedAdminShopOwnerWindow(adminShopWindow, BuildPacketOwnedAdminShopFooter()));
                    }

                    return ChatCommandHandler.CommandResult.Error("Packet-owned admin-shop owner is unavailable.");

                default:
                    return ChatCommandHandler.CommandResult.Error("Usage: /adminshop [status|show|inbox [status|start [port]|stop|packet <366|367|result|open> [payloadhex=..|payloadb64=..]|packetraw <366|367|result|open> <hex>|packetclientraw <hex>]|packet <366|367|result|open> [payloadhex=..|payloadb64=..]|packetraw <366|367|result|open> <hex>|packetclientraw <hex>]");
            }
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedAdminShopInboxCommand(string[] args)
        {
            if (args == null || args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info($"{DescribeAdminShopPacketInboxStatus()} {BuildPacketOwnedAdminShopFooter()} {_adminShopPacketInbox.LastStatus}");
            }

            if (string.Equals(args[0], "start", StringComparison.OrdinalIgnoreCase))
            {
                int port = AdminShopPacketInboxManager.DefaultPort;
                if (args.Length > 1 && (!int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out port) || port <= 0 || port > ushort.MaxValue))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /adminshop inbox start [port]");
                }

                _adminShopPacketInboxConfiguredPort = port;
                _adminShopPacketInboxEnabled = true;
                EnsureAdminShopPacketInboxState(shouldRun: true);
                return ChatCommandHandler.CommandResult.Ok($"{DescribeAdminShopPacketInboxStatus()} {_adminShopPacketInbox.LastStatus}");
            }

            if (string.Equals(args[0], "stop", StringComparison.OrdinalIgnoreCase))
            {
                _adminShopPacketInboxEnabled = false;
                EnsureAdminShopPacketInboxState(shouldRun: false);
                return ChatCommandHandler.CommandResult.Ok($"{DescribeAdminShopPacketInboxStatus()} {_adminShopPacketInbox.LastStatus}");
            }

            if (string.Equals(args[0], "packet", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase))
            {
                return HandlePacketOwnedAdminShopPacketCommand(args);
            }

            if (string.Equals(args[0], "packetclientraw", StringComparison.OrdinalIgnoreCase))
            {
                return HandlePacketOwnedAdminShopClientPacketRawCommand(args);
            }

            return ChatCommandHandler.CommandResult.Error("Usage: /adminshop inbox [status|start [port]|stop|packet <366|367|result|open> [payloadhex=..|payloadb64=..]|packetraw <366|367|result|open> <hex>|packetclientraw <hex>]");
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedAdminShopPacketCommand(string[] args)
        {
            bool rawHex = string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase);
            if (args.Length < 2 || !AdminShopPacketInboxManager.TryParsePacketType(args[1], out int packetType))
            {
                return ChatCommandHandler.CommandResult.Error(rawHex
                    ? "Usage: /adminshop packetraw <366|367|result|open> <hex>"
                    : "Usage: /adminshop packet <366|367|result|open> [payloadhex=..|payloadb64=..]");
            }

            byte[] payload = Array.Empty<byte>();
            if (rawHex)
            {
                if (args.Length < 3 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(2)), out payload))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /adminshop packetraw <366|367|result|open> <hex>");
                }
            }
            else if (args.Length >= 3 && !TryParseBinaryPayloadArgument(args[2], out payload, out string payloadError))
            {
                return ChatCommandHandler.CommandResult.Error(payloadError ?? "Usage: /adminshop packet <366|367|result|open> [payloadhex=..|payloadb64=..]");
            }

            return TryApplyPacketOwnedAdminShopPacket(packetType, payload, out string message)
                ? ChatCommandHandler.CommandResult.Ok(message)
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedAdminShopClientPacketRawCommand(string[] args)
        {
            if (args.Length < 2 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(1)), out byte[] rawPacket))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /adminshop packetclientraw <hex>");
            }

            if (!AdminShopPacketInboxManager.TryDecodeOpcodeFramedPacket(rawPacket, out int packetType, out byte[] payload, out string decodeError))
            {
                return ChatCommandHandler.CommandResult.Error(decodeError ?? "Usage: /adminshop packetclientraw <hex>");
            }

            return TryApplyPacketOwnedAdminShopPacket(packetType, payload, out string message)
                ? ChatCommandHandler.CommandResult.Ok($"Applied admin-shop client opcode {packetType}. {message}")
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
                    if (!AdminShopPacketOwnedOpenCodec.TryDecode(payload, out AdminShopPacketOwnedOpenPayloadSnapshot snapshot))
                    {
                        message = "Admin-shop packet 367 payload could not be decoded with the recovered CAdminShopDlg::SetAdminShopDlg layout.";
                        return false;
                    }

                    string blockingOwner = GetVisibleUniqueModelessOwner(MapSimulatorWindowNames.CashShop);
                    if (!string.IsNullOrWhiteSpace(blockingOwner))
                    {
                        message = adminShopWindow.ApplyPacketOwnedAdminShopBlockedByUniqueModelessOwner(blockingOwner, snapshot);
                        return true;
                    }

                    if (!adminShopWindow.TryBeginPacketOwnedAdminShopSession(snapshot, out message))
                    {
                        return false;
                    }

                    PublishDynamicObjectTagStatesForNpc(npcTemplateId, currTickCount);
                    message = ShowPacketOwnedAdminShopOwnerWindow(adminShopWindow, message);
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
                PublishDynamicObjectTagStatesForNpc(adminShopWindow.PacketOwnedAdminShopNpcTemplateId, currTickCount);
                message = ShowPacketOwnedAdminShopOwnerWindow(adminShopWindow, message);
            }

            return true;
        }

        private string ShowPacketOwnedAdminShopOwnerWindow(AdminShopDialogUI adminShopWindow, string defaultMessage)
        {
            if (adminShopWindow == null)
            {
                return defaultMessage ?? "Admin Shop owner is unavailable.";
            }

            if (uiWindowManager == null)
            {
                return defaultMessage ?? "Admin Shop owner is unavailable because the UI window manager is missing.";
            }

            string blockingOwner = GetVisibleUniqueModelessOwner(MapSimulatorWindowNames.CashShop);
            if (!string.IsNullOrWhiteSpace(blockingOwner))
            {
                adminShopWindow.RecordPacketOwnedAdminShopOwnerSurfaceHidden(
                    $"CAdminShopDlg owner surface stayed hidden because {blockingOwner} already owned the unique-modeless slot.",
                    AdminShopPacketOwnedOwnerVisibilityState.StagedButHidden);
                return $"{defaultMessage} Admin Shop stayed in status-only mode because {blockingOwner} is already visible.";
            }

            ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.CashShop);
            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.CashShop) is UIWindowBase window)
            {
                uiWindowManager.BringToFront(window);
            }

            adminShopWindow.RecordPacketOwnedAdminShopOwnerSurfaceShown();
            return defaultMessage;
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
                    bool hasCloseOutbound = _packetOwnedStoreBankRuntime.TryBuildCloseOutboundRequest(
                        out PacketOwnedNpcUtilityOutboundRequest closeRequest,
                        out string closeLocalMessage);
                    if (hasCloseOutbound)
                    {
                        uiWindowManager?.HideWindow(MapSimulatorWindowNames.StoreBank);
                        HidePacketOwnedStoreBankGetAllPrompt();
                    }

                    return ChatCommandHandler.CommandResult.Ok(
                        DispatchPacketOwnedStoreBankOutboundRequest(
                            hasCloseOutbound,
                            closeRequest,
                            closeLocalMessage,
                            hasCloseOutbound ? null : closeLocalMessage));

                default:
                    return ChatCommandHandler.CommandResult.Error("Usage: /npcutility storebank [status|show|getall|close]");
            }
        }

        private void HandlePacketOwnedStoreBankGetButtonClick()
        {
            if (_packetOwnedStoreBankRuntime.HasPendingGetAllRequest)
            {
                OpenPacketOwnedStoreBankGetAllPrompt();
                return;
            }

            int selectedRowIndex = uiWindowManager?.GetWindow(MapSimulatorWindowNames.StoreBank) is StoreBankOwnerWindow storeBankWindow
                ? storeBankWindow.SelectedOwnerRowIndex
                : -1;
            bool hasRequest = _packetOwnedStoreBankRuntime.TryBuildSelectedGetOutboundRequest(
                selectedRowIndex,
                out PacketOwnedNpcUtilityOutboundRequest request,
                out string localMessage);
            DispatchPacketOwnedStoreBankOutboundRequest(
                hasRequest,
                request,
                localMessage,
                hasRequest ? null : _packetOwnedStoreBankRuntime.StatusMessage);
        }

        private bool HandlePacketOwnedStoreBankCloseButtonClick()
        {
            bool hasCloseOutbound = _packetOwnedStoreBankRuntime.TryBuildCloseOutboundRequest(
                out PacketOwnedNpcUtilityOutboundRequest request,
                out string localMessage);
            DispatchPacketOwnedStoreBankOutboundRequest(
                hasCloseOutbound,
                request,
                localMessage,
                hasCloseOutbound ? null : localMessage);

            if (hasCloseOutbound)
            {
                HidePacketOwnedStoreBankGetAllPrompt();
                return true;
            }

            return false;
        }

        private void OpenPacketOwnedStoreBankGetAllPrompt()
        {
            if (!_packetOwnedStoreBankRuntime.HasPendingGetAllRequest)
            {
                return;
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.InGameConfirmDialog) is not InGameConfirmDialogWindow confirmDialogWindow)
            {
                bool hasGetAllOutbound = _packetOwnedStoreBankRuntime.TryBuildPendingGetAllOutboundRequest(
                    out PacketOwnedNpcUtilityOutboundRequest getAllRequest,
                    out string getAllOutboundError);
                DispatchPacketOwnedStoreBankOutboundRequest(
                    hasGetAllOutbound,
                    getAllRequest,
                    _packetOwnedStoreBankRuntime.ConsumePendingGetAllRequest(),
                    getAllOutboundError);
                return;
            }

            _packetOwnedStoreBankGetAllPromptActive = true;
            ConfigureInGameConfirmDialog(
                "Store Bank",
                _packetOwnedStoreBankRuntime.BuildPendingGetAllPromptBody(),
                "Recovered CStoreBankDlg::SendGetAllRequest confirmation owner.",
                onConfirm: AcceptPacketOwnedStoreBankGetAllPrompt,
                onCancel: CancelPacketOwnedStoreBankGetAllPrompt);
            ShowWindow(
                MapSimulatorWindowNames.InGameConfirmDialog,
                confirmDialogWindow,
                trackDirectionModeOwner: true);
        }

        private void AcceptPacketOwnedStoreBankGetAllPrompt()
        {
            _packetOwnedStoreBankGetAllPromptActive = false;
            bool hasGetAllOutbound = _packetOwnedStoreBankRuntime.TryBuildPendingGetAllOutboundRequest(
                out PacketOwnedNpcUtilityOutboundRequest getAllRequest,
                out string getAllOutboundError);
            DispatchPacketOwnedStoreBankOutboundRequest(
                hasGetAllOutbound,
                getAllRequest,
                _packetOwnedStoreBankRuntime.ConsumePendingGetAllRequest(),
                getAllOutboundError);
        }

        private void CancelPacketOwnedStoreBankGetAllPrompt()
        {
            _packetOwnedStoreBankGetAllPromptActive = false;
            _packetOwnedStoreBankRuntime.CancelPendingGetAllRequest();
        }

        private void HidePacketOwnedStoreBankGetAllPrompt()
        {
            if (!_packetOwnedStoreBankGetAllPromptActive)
            {
                return;
            }

            _packetOwnedStoreBankGetAllPromptActive = false;
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.InGameConfirmDialog) is InGameConfirmDialogWindow confirmDialogWindow)
            {
                confirmDialogWindow.Hide();
            }

            ClearInGameConfirmDialogActions();
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

        private string DispatchPacketOwnedAdminShopOutboundRequest(PacketOwnedNpcUtilityOutboundRequest request)
        {
            int adminShopOpcode = -1;
            byte[] adminShopPayload = Array.Empty<byte>();
            string adminShopSummary = null;
            return DispatchPacketOwnedNpcUtilityOutboundRequest(
                true,
                request,
                string.Empty,
                null,
                ref adminShopOpcode,
                ref adminShopPayload,
                ref adminShopSummary).Trim();
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

        private string DispatchPacketOwnedBattleRecordOutboundRequest(
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
                ref _lastPacketOwnedBattleRecordOutboundOpcode,
                ref _lastPacketOwnedBattleRecordOutboundPayload,
                ref _lastPacketOwnedBattleRecordOutboundSummary);
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
                return ChatCommandHandler.CommandResult.Info(BuildPacketOwnedBattleRecordFooter());
            }

            switch (args[0].ToLowerInvariant())
            {
                case "show":
                case "open":
                    return ChatCommandHandler.CommandResult.Ok(
                        ShowPacketOwnedUniqueUtilityWindow(
                            MapSimulatorWindowNames.BattleRecord,
                            "Battle Record",
                            BuildPacketOwnedBattleRecordFooter()));

                case "on":
                case "start":
                    return HandlePacketOwnedBattleRecordOnCalcCommand(enabled: true);

                case "off":
                case "stop":
                    return HandlePacketOwnedBattleRecordOnCalcCommand(enabled: false);

                case "toggle":
                    bool hasToggleOutbound = _packetOwnedBattleRecordRuntime.TryBuildToggleOnCalcOutboundRequest(
                        out PacketOwnedNpcUtilityOutboundRequest toggleRequest,
                        out string toggleMessage);
                    return ChatCommandHandler.CommandResult.Ok(
                        DispatchPacketOwnedBattleRecordOutboundRequest(
                            hasToggleOutbound,
                            toggleRequest,
                            toggleMessage,
                            hasToggleOutbound ? null : toggleMessage));

                case "dot":
                    return HandlePacketOwnedBattleRecordIncludeCommand(args.Skip(1).ToArray(), option: 0);

                case "summon":
                    return HandlePacketOwnedBattleRecordIncludeCommand(args.Skip(1).ToArray(), option: 1);

                case "clear":
                    return HandlePacketOwnedBattleRecordClearCommand(args.Skip(1).ToArray());

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
                    return ChatCommandHandler.CommandResult.Ok(BuildPacketOwnedBattleRecordFooter());

                case "close":
                    bool hasCloseOutbound = _packetOwnedBattleRecordRuntime.TryBuildRequestOnCalcOutboundRequest(
                        enabled: false,
                        out PacketOwnedNpcUtilityOutboundRequest closeRequest,
                        out string closeOnCalcMessage);
                    _packetOwnedBattleRecordRuntime.Close();
                    uiWindowManager?.HideWindow(MapSimulatorWindowNames.BattleRecord);
                    return ChatCommandHandler.CommandResult.Ok(
                        DispatchPacketOwnedBattleRecordOutboundRequest(
                            hasCloseOutbound,
                            closeRequest,
                            _packetOwnedBattleRecordRuntime.BuildFooter(),
                            hasCloseOutbound ? null : closeOnCalcMessage));

                default:
                    return ChatCommandHandler.CommandResult.Error("Usage: /npcutility battlerecord [status|show|on|off|toggle|dot <on|off>|summon <on|off>|clear <damage|recovery|all>|page <summary|dot|packets>|close]");
            }
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedBattleRecordOnCalcCommand(bool enabled)
        {
            bool hasOutbound = _packetOwnedBattleRecordRuntime.TryBuildRequestOnCalcOutboundRequest(
                enabled,
                out PacketOwnedNpcUtilityOutboundRequest request,
                out string localMessage);
            return ChatCommandHandler.CommandResult.Ok(
                DispatchPacketOwnedBattleRecordOutboundRequest(
                    hasOutbound,
                    request,
                    localMessage,
                    hasOutbound ? null : localMessage));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedBattleRecordIncludeCommand(string[] args, int option)
        {
            if (args == null
                || args.Length < 1
                || !TryParseOnOffArgument(args[0], out bool enabled))
            {
                string owner = option == 0 ? "dot" : "summon";
                return ChatCommandHandler.CommandResult.Error($"Usage: /npcutility battlerecord {owner} <on|off>");
            }

            return ChatCommandHandler.CommandResult.Ok(_packetOwnedBattleRecordRuntime.SetAdditionDamageInclude(enabled, option));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedBattleRecordClearCommand(string[] args)
        {
            if (args == null || args.Length < 1)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /npcutility battlerecord clear <damage|recovery|all>");
            }

            int option = args[0].ToLowerInvariant() switch
            {
                "damage" => 0,
                "recovery" => 1,
                "all" => 3,
                _ => -1
            };
            if (option < 0)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /npcutility battlerecord clear <damage|recovery|all>");
            }

            return ChatCommandHandler.CommandResult.Ok(_packetOwnedBattleRecordRuntime.ClearInfo(option));
        }

        private static bool TryParseOnOffArgument(string value, out bool enabled)
        {
            enabled = false;
            if (string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "1", StringComparison.Ordinal))
            {
                enabled = true;
                return true;
            }

            if (string.Equals(value, "off", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "0", StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }
    }
}
