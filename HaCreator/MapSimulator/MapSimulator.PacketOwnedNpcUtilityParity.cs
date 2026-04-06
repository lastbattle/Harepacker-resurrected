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
                    return ChatCommandHandler.CommandResult.Error("Usage: /npcutility [status|packet <364|365|369|370|420|421|422|423> [payloadhex=..|payloadb64=..]|packetraw <364|365|369|370|420|421|422|423> <hex>|shop [status|show|buy <itemId> [quantity]|sell <itemId> [quantity]|recharge <itemId> [targetQuantity]|close]|storebank [status|show|getall|close]|battlerecord [status|show|page <summary|dot|packets>|close]]");
            }
        }

        private string DescribePacketOwnedNpcUtilityStatus()
        {
            return string.Join(
                Environment.NewLine,
                new[]
                {
                    "Packet-owned NPC utility owner family:",
                    $"Shop: {_packetOwnedNpcShopRuntime.BuildFooter()}",
                    $"StoreBank: {_packetOwnedStoreBankRuntime.BuildFooter()}",
                    $"BattleRecord: {_packetOwnedBattleRecordRuntime.BuildFooter()}"
                });
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedNpcUtilityPacketCommand(string[] args)
        {
            bool rawHex = string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase);
            if (args.Length < 2
                || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int packetType)
                || packetType is not (364 or 365 or 369 or 370 or 420 or 421 or 422 or 423))
            {
                return ChatCommandHandler.CommandResult.Error(rawHex
                    ? "Usage: /npcutility packetraw <364|365|369|370|420|421|422|423> <hex>"
                    : "Usage: /npcutility packet <364|365|369|370|420|421|422|423> [payloadhex=..|payloadb64=..]");
            }

            byte[] payload = Array.Empty<byte>();
            if (rawHex)
            {
                if (args.Length < 3 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(2)), out payload))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /npcutility packetraw <364|365|369|370|420|421|422|423> <hex>");
                }
            }
            else if (args.Length >= 3 && !TryParseBinaryPayloadArgument(args[2], out payload, out string payloadError))
            {
                return ChatCommandHandler.CommandResult.Error(payloadError ?? "Usage: /npcutility packet <364|365|369|370|420|421|422|423> [payloadhex=..|payloadb64=..]");
            }

            return TryApplyPacketOwnedNpcUtilityPacket(packetType, payload, out string message)
                ? ChatCommandHandler.CommandResult.Ok(message)
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedNpcShopCommand(string[] args)
        {
            if (args == null || args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(_packetOwnedNpcShopRuntime.BuildFooter());
            }

            switch (args[0].ToLowerInvariant())
            {
                case "show":
                case "open":
                    return ChatCommandHandler.CommandResult.Ok(
                        ShowPacketOwnedUniqueUtilityWindow(
                            MapSimulatorWindowNames.NpcShop,
                            "NPC Shop",
                            _packetOwnedNpcShopRuntime.BuildFooter()));

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

                    return _packetOwnedNpcShopRuntime.TryApplyLocalBuy(buyInventory, buyItemId, buyQuantity, out string buyMessage)
                        ? ChatCommandHandler.CommandResult.Ok(buyMessage)
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

                    return _packetOwnedNpcShopRuntime.TryApplyLocalSell(sellInventory, sellItemId, sellQuantity, out string sellMessage)
                        ? ChatCommandHandler.CommandResult.Ok(sellMessage)
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

                    return _packetOwnedNpcShopRuntime.TryApplyLocalRecharge(rechargeInventory, rechargeItemId, targetQuantity, out string rechargeMessage)
                        ? ChatCommandHandler.CommandResult.Ok(rechargeMessage)
                        : ChatCommandHandler.CommandResult.Error(rechargeMessage);

                case "close":
                    _packetOwnedNpcShopRuntime.Close();
                    uiWindowManager?.HideWindow(MapSimulatorWindowNames.NpcShop);
                    return ChatCommandHandler.CommandResult.Ok(_packetOwnedNpcShopRuntime.BuildFooter());

                default:
                    return ChatCommandHandler.CommandResult.Error("Usage: /npcutility shop [status|show|buy <itemId> [quantity]|sell <itemId> [quantity]|recharge <itemId> [targetQuantity]|close]");
            }
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedStoreBankCommand(string[] args)
        {
            if (args == null || args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(_packetOwnedStoreBankRuntime.BuildFooter());
            }

            switch (args[0].ToLowerInvariant())
            {
                case "show":
                case "open":
                    return ChatCommandHandler.CommandResult.Ok(
                        ShowPacketOwnedUniqueUtilityWindow(
                            MapSimulatorWindowNames.StoreBank,
                            "Store Bank",
                            _packetOwnedStoreBankRuntime.BuildFooter()));

                case "getall":
                case "accept":
                    return ChatCommandHandler.CommandResult.Ok(_packetOwnedStoreBankRuntime.ConsumePendingGetAllRequest());

                case "close":
                    _packetOwnedStoreBankRuntime.Close();
                    uiWindowManager?.HideWindow(MapSimulatorWindowNames.StoreBank);
                    return ChatCommandHandler.CommandResult.Ok(_packetOwnedStoreBankRuntime.BuildFooter());

                default:
                    return ChatCommandHandler.CommandResult.Error("Usage: /npcutility storebank [status|show|getall|close]");
            }
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
