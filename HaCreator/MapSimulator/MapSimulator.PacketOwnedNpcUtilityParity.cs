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

        private bool TryApplyPacketOwnedNpcUtilityPacket(int packetType, byte[] payload, out string message, string source = null)
        {
            message = null;
            payload ??= Array.Empty<byte>();

            switch (packetType)
            {
                case LocalUtilityPacketInboxManager.AdminShopResultClientPacketType:
                case LocalUtilityPacketInboxManager.AdminShopOpenClientPacketType:
                    return TryApplyPacketOwnedAdminShopPacket(packetType, payload, out message, source);

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

            string fieldRestrictionMessage = GetFieldWindowRestrictionMessage(windowName);
            if (!string.IsNullOrWhiteSpace(fieldRestrictionMessage))
            {
                ShowFieldRestrictionMessage(fieldRestrictionMessage);
                return $"{defaultMessage} {displayName} stayed in status-only mode because current field metadata blocks that owner.";
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

        private void UpdatePacketOwnedBattleRecordTimerLifecycle(int currentTickCount)
        {
            if (!_packetOwnedBattleRecordRuntime.TryBuildTimerExpiryOutboundRequest(
                    currentTickCount,
                    out PacketOwnedNpcUtilityOutboundRequest request,
                    out string localMessage))
            {
                return;
            }

            string dispatchMessage = DispatchPacketOwnedBattleRecordOutboundRequest(
                hasRequest: true,
                request,
                localMessage,
                requestError: null);
            if (!string.IsNullOrWhiteSpace(dispatchMessage))
            {
                ShowUtilityFeedbackMessage(dispatchMessage);
            }
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
                    return ChatCommandHandler.CommandResult.Error("Usage: /npcutility [status|packet <364|365|366|367|369|370|420|421|422|423> [payloadhex=..|payloadb64=..]|packetraw <364|365|366|367|369|370|420|421|422|423> <hex>|shop [status|show|buy <itemId> [quantity]|sell <itemId> [quantity]|recharge <itemId> [targetQuantity]|close]|storebank [status|show|getall|close]|battlerecord [status|show|on|off|toggle|timer <seconds>|timerstop|viewtoggle|dot <on|off>|summon <on|off>|damage <value> [critical=<on|off>] [summon=<on|off>] [attrRate=<value>]|recovery <hpRecovery> <mpRecovery> <beforeHp> <beforeMp> [currentHp=<value>] [currentMp=<value>] [wvsContext=<on|off>]|forceoff|clear <damage|recovery|all>|page <summary|dot|packets>|close]]");
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

                case "session":
                    return HandlePacketOwnedAdminShopSessionCommand(args.Skip(1).ToArray());

                case "packet":
                case "packetraw":
                    return HandlePacketOwnedAdminShopPacketCommand(args);

                case "packetclientraw":
                    return HandlePacketOwnedAdminShopClientPacketRawCommand(args);

                case "packetclientrawseq":
                    return HandlePacketOwnedAdminShopClientPacketRawSequenceCommand(args);

                case "show":
                case "open":
                    if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShop) is AdminShopDialogUI adminShopWindow)
                    {
                        return ChatCommandHandler.CommandResult.Ok(
                            ShowPacketOwnedAdminShopOwnerWindow(adminShopWindow, BuildPacketOwnedAdminShopFooter()));
                    }

                    return ChatCommandHandler.CommandResult.Error("Packet-owned admin-shop owner is unavailable.");

                default:
                    return ChatCommandHandler.CommandResult.Error("Usage: /adminshop [status|show|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort|0> <serverHost> <serverPort>|startauto <listenPort|0> <remotePort> [processName|pid] [localPort]|recent|clearrecent|stop]|inbox [status|start [port]|stop|packet <366|367|result|open> [payloadhex=..|payloadb64=..]|packetraw <366|367|result|open> <hex>|packetclientraw <hex>|packetclientrawseq <hex;hex;...>]|packet <366|367|result|open> [payloadhex=..|payloadb64=..]|packetraw <366|367|result|open> <hex>|packetclientraw <hex>|packetclientrawseq <hex;hex;...>]");
            }
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedAdminShopSessionCommand(string[] args)
        {
            if (args == null || args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(_adminShopOfficialSessionBridge.DescribeStatus());
            }

            switch (args[0].ToLowerInvariant())
            {
                case "start":
                    if (args.Length < 4
                        || !TryParseAdminShopProxyListenPort(args[1], out int listenPort)
                        || !TryParseAdminShopRemotePort(args[3], out int remotePort))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /adminshop session start <listenPort|0> <serverHost> <serverPort>");
                    }

                    return _adminShopOfficialSessionBridge.TryStart(listenPort, args[2], remotePort, out string startStatus)
                        ? ChatCommandHandler.CommandResult.Ok(startStatus)
                        : ChatCommandHandler.CommandResult.Error(startStatus);

                case "startauto":
                    if (args.Length < 3
                        || !TryParseAdminShopProxyListenPort(args[1], out int autoListenPort)
                        || !TryParseAdminShopRemotePort(args[2], out int autoRemotePort))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /adminshop session startauto <listenPort|0> <remotePort> [processName|pid] [localPort]");
                    }

                    int? autoLocalPort = null;
                    if (args.Length >= 5)
                    {
                        if (!TryParseAdminShopLocalPortFilter(args[4], out int parsedLocalPort))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /adminshop session startauto <listenPort|0> <remotePort> [processName|pid] [localPort]");
                        }

                        autoLocalPort = parsedLocalPort;
                    }

                    string autoProcessSelector = args.Length >= 4 ? args[3] : null;
                    return _adminShopOfficialSessionBridge.TryStartFromDiscovery(autoListenPort, autoRemotePort, autoProcessSelector, autoLocalPort, out string autoStatus)
                        ? ChatCommandHandler.CommandResult.Ok(autoStatus)
                        : ChatCommandHandler.CommandResult.Error(autoStatus);

                case "discover":
                    if (args.Length < 2 || !TryParseAdminShopRemotePort(args[1], out int discoverRemotePort))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /adminshop session discover <remotePort> [processName|pid] [localPort]");
                    }

                    int? discoverLocalPort = null;
                    if (args.Length >= 4)
                    {
                        if (!TryParseAdminShopLocalPortFilter(args[3], out int parsedDiscoverLocalPort))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /adminshop session discover <remotePort> [processName|pid] [localPort]");
                        }

                        discoverLocalPort = parsedDiscoverLocalPort;
                    }

                    string discoverProcessSelector = args.Length >= 3 ? args[2] : null;
                    return ChatCommandHandler.CommandResult.Info(
                        _adminShopOfficialSessionBridge.DescribeDiscoveredSessions(discoverRemotePort, discoverProcessSelector, discoverLocalPort));

                case "recent":
                    return ChatCommandHandler.CommandResult.Info(_adminShopOfficialSessionBridge.DescribeRecentPackets());

                case "clearrecent":
                    _adminShopOfficialSessionBridge.ClearRecentPackets();
                    return ChatCommandHandler.CommandResult.Ok(_adminShopOfficialSessionBridge.LastStatus);

                case "stop":
                    _adminShopOfficialSessionBridge.Stop();
                    return ChatCommandHandler.CommandResult.Ok(_adminShopOfficialSessionBridge.LastStatus);

                default:
                    return ChatCommandHandler.CommandResult.Error("Usage: /adminshop session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort|0> <serverHost> <serverPort>|startauto <listenPort|0> <remotePort> [processName|pid] [localPort]|recent|clearrecent|stop]");
            }
        }

        private static bool TryParseAdminShopProxyListenPort(string value, out int listenPort)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out listenPort)
                && listenPort >= 0
                && listenPort <= ushort.MaxValue;
        }

        private static bool TryParseAdminShopRemotePort(string value, out int remotePort)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out remotePort)
                && remotePort > 0
                && remotePort <= ushort.MaxValue;
        }

        private static bool TryParseAdminShopLocalPortFilter(string value, out int localPort)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out localPort)
                && localPort > 0
                && localPort <= ushort.MaxValue;
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedAdminShopInboxCommand(string[] args)
        {
            if (args == null || args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info($"{DescribeAdminShopPacketInboxStatus()} {BuildPacketOwnedAdminShopFooter()} {_adminShopPacketInbox.LastStatus}");
            }

            if (string.Equals(args[0], "start", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(
                    "Admin-shop packet inbox loopback listener is retired; use role-session ingress or packet commands for local injection.");
            }

            if (string.Equals(args[0], "stop", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info("Admin-shop packet inbox loopback listener is already retired.");
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

            if (string.Equals(args[0], "packetclientrawseq", StringComparison.OrdinalIgnoreCase))
            {
                return HandlePacketOwnedAdminShopClientPacketRawSequenceCommand(args);
            }

            return ChatCommandHandler.CommandResult.Error("Usage: /adminshop inbox [status|start|stop|packet <366|367|result|open> [payloadhex=..|payloadb64=..]|packetraw <366|367|result|open> <hex>|packetclientraw <hex>|packetclientrawseq <hex;hex;...>]");
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

            return TryApplyPacketOwnedAdminShopPacket(packetType, payload, out string message, "admin-shop-command")
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

            return TryApplyPacketOwnedAdminShopPacket(packetType, payload, out string message, "admin-shop-client-raw")
                ? ChatCommandHandler.CommandResult.Ok($"Applied admin-shop client opcode {packetType}. {message}")
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedAdminShopClientPacketRawSequenceCommand(string[] args)
        {
            if (args.Length < 2)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /adminshop packetclientrawseq <opcodeFramedHex1;opcodeFramedHex2;...>");
            }

            string sequenceText = string.Join(" ", args.Skip(1));
            if (!AdminShopPacketInboxManager.TryDecodeOpcodeFramedPacketSequence(
                    sequenceText,
                    out IReadOnlyList<AdminShopOpcodeFramedPacket> packets,
                    out string decodeError))
            {
                return ChatCommandHandler.CommandResult.Error(decodeError ?? "Usage: /adminshop packetclientrawseq <opcodeFramedHex1;opcodeFramedHex2;...>");
            }

            List<string> appliedMessages = new(packets.Count);
            for (int index = 0; index < packets.Count; index++)
            {
                AdminShopOpcodeFramedPacket packet = packets[index];
                string source = $"admin-shop-client-raw-seq:{index + 1}/{packets.Count}";
                if (!TryApplyPacketOwnedAdminShopPacket(packet.PacketType, packet.Payload, out string message, source))
                {
                    return ChatCommandHandler.CommandResult.Error(
                        $"Admin-shop client packet sequence stopped at {index + 1}/{packets.Count} opcode {packet.PacketType}: {message}");
                }

                appliedMessages.Add($"{index + 1}/{packets.Count} opcode {packet.PacketType}: {message}");
            }

            return ChatCommandHandler.CommandResult.Ok(string.Join(Environment.NewLine, appliedMessages));
        }

        private bool TryApplyPacketOwnedAdminShopPacket(int packetType, byte[] payload, out string message, string source = null)
        {
            message = "Packet-owned admin-shop owner is unavailable.";
            payload ??= Array.Empty<byte>();

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShop) is not AdminShopDialogUI adminShopWindow)
            {
                return false;
            }

            adminShopWindow.RecordPacketOwnedAdminShopInboundPacket(packetType, source);

            if (packetType == 367)
            {
                if (payload.Length < sizeof(int) + sizeof(ushort))
                {
                    message = "Admin-shop packet 367 requires NPC template id and item-count payload fields.";
                    return false;
                }

                int npcTemplateId = BitConverter.ToInt32(payload, 0);
                int itemCount = BitConverter.ToUInt16(payload, sizeof(int));
                if (!AdminShopPacketOwnedOpenCodec.TryDecode(payload, out AdminShopPacketOwnedOpenPayloadSnapshot snapshot))
                {
                    message = itemCount > 0
                        ? "Admin-shop packet 367 payload could not be decoded with the recovered CAdminShopDlg::SetAdminShopDlg layout."
                        : "Admin-shop packet 367 rejected-open payload could not be decoded with the recovered CAdminShopDlg::SetAdminShopDlg header layout.";
                    return false;
                }

                if (itemCount > 0)
                {
                    string blockingOwner = GetVisibleUniqueModelessOwner(MapSimulatorWindowNames.CashShop);
                    if (AdminShopPacketOwnedOwnerGateParity.ShouldIgnoreOpenAtOwnerGate(
                            hasBlockingUniqueModelessOwner: !string.IsNullOrWhiteSpace(blockingOwner),
                            commodityCount: itemCount))
                    {
                        message = adminShopWindow.ApplyPacketOwnedAdminShopBlockedByUniqueModelessOwner(
                            blockingOwner,
                            snapshot);
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
                message = adminShopWindow.ApplyPacketOwnedAdminShopOpenRejected(
                    rejectionNotice,
                    snapshot.NpcTemplateId,
                    snapshot.CommodityCount,
                    snapshot.TrailingByteCount,
                    snapshot.TrailingPayloadSignature);
                HideCashShopOwnerFamilyWindows();
                ShowPacketOwnedNoticeDialog(rejectionNotice);
                return true;
            }

            string resultBlockingOwner = GetVisibleUniqueModelessOwner(MapSimulatorWindowNames.CashShop);
            bool ignoreResultAtOwnerGate = AdminShopPacketOwnedOwnerGateParity.ShouldIgnoreResultAtOwnerGate(
                hasBlockingUniqueModelessOwner: !string.IsNullOrWhiteSpace(resultBlockingOwner),
                acceptsResultAtOwnerGate: adminShopWindow.ShouldAcceptPacketOwnedAdminShopResultAtOwnerGate);
            if (!AdminShopPacketOwnedResultCodec.TryDecode(payload, out AdminShopPacketOwnedResultPayloadSnapshot resultSnapshot))
            {
                if (ignoreResultAtOwnerGate)
                {
                    message = adminShopWindow.ApplyPacketOwnedAdminShopMalformedResultIgnoredByUniqueModelessOwner(
                        resultBlockingOwner);
                    return true;
                }

                message = "Admin-shop packet 366 requires the subtype byte.";
                return false;
            }

            if (ignoreResultAtOwnerGate)
            {
                message = adminShopWindow.ApplyPacketOwnedAdminShopResultIgnoredByUniqueModelessOwner(
                    resultSnapshot.Subtype,
                    resultSnapshot.ResultCode,
                    resultSnapshot.TrailingByteCount,
                    resultSnapshot.TrailingPayloadSignature,
                    resultSnapshot.TrailingPayload,
                    resultSnapshot.HasResultCode,
                    resultBlockingOwner);
                return true;
            }

            bool applied = adminShopWindow.TryApplyPacketOwnedAdminShopResult(
                resultSnapshot.Subtype,
                resultSnapshot.ResultCode,
                resultSnapshot.TrailingByteCount,
                resultSnapshot.TrailingPayloadSignature,
                resultSnapshot.TrailingPayload,
                resultSnapshot.HasResultCode,
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

            string fieldRestrictionMessage = GetFieldWindowRestrictionMessage(MapSimulatorWindowNames.CashShop);
            if (!string.IsNullOrWhiteSpace(fieldRestrictionMessage))
            {
                ShowFieldRestrictionMessage(fieldRestrictionMessage);
                adminShopWindow.RecordPacketOwnedAdminShopOwnerSurfaceHidden(
                    "CAdminShopDlg owner surface stayed hidden because current field metadata blocks shop owners.",
                    AdminShopPacketOwnedOwnerVisibilityState.HiddenByFieldRestriction);
                return $"{defaultMessage} Admin Shop stayed in status-only mode because current field metadata blocks shop owners.";
            }

            string blockingOwner = GetVisibleUniqueModelessOwner(MapSimulatorWindowNames.CashShop);
            if (!string.IsNullOrWhiteSpace(blockingOwner))
            {
                adminShopWindow.RecordPacketOwnedAdminShopOwnerSurfaceHidden(
                    $"CAdminShopDlg owner surface stayed hidden because {blockingOwner} already owned the unique-modeless slot.",
                    AdminShopPacketOwnedOwnerVisibilityState.HiddenByUniqueModelessOwner);
                return $"{defaultMessage} Admin Shop stayed in status-only mode because {blockingOwner} is already visible.";
            }

            ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.CashShop);
            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.CashShop) is UIWindowBase window)
            {
                uiWindowManager.BringToFront(window);
            }

            adminShopWindow.RecordPacketOwnedAdminShopOwnerSurfaceShown();
            if (adminShopWindow.TryApplyDeferredPacketOwnedAdminShopResultAfterOwnerVisible(
                    out string deferredSummary,
                    out string deferredNotice))
            {
                if (!string.IsNullOrWhiteSpace(deferredNotice))
                {
                    ShowPacketOwnedNoticeDialog(deferredNotice);
                }

                if (!string.IsNullOrWhiteSpace(deferredSummary))
                {
                    return string.IsNullOrWhiteSpace(defaultMessage)
                        ? deferredSummary
                        : $"{defaultMessage} {deferredSummary}".Trim();
                }
            }

            return defaultMessage;
        }

        private void RestorePacketOwnedAdminShopAfterOwnerVisibilityResumes()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShop) is not AdminShopDialogUI adminShopWindow
                || adminShopWindow.IsVisible)
            {
                return;
            }

            bool restoreAfterBlockerClears = adminShopWindow.ShouldRestorePacketOwnedAdminShopAfterUniqueModelessBlockerClears;
            bool restoreAfterCashShopFamilyVisible = adminShopWindow.ShouldRestorePacketOwnedAdminShopAfterCashShopFamilyVisible
                && IsCashShopOwnerFamilyVisibleWithoutAdminShopOwner();
            if (!restoreAfterBlockerClears && !restoreAfterCashShopFamilyVisible)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(GetVisibleUniqueModelessOwner(MapSimulatorWindowNames.CashShop)))
            {
                return;
            }

            string restoreReason = restoreAfterCashShopFamilyVisible
                ? "CAdminShopDlg resumed the staged packet-owned session after the Cash Shop owner family became visible again."
                : "CAdminShopDlg surfaced the staged packet 367 payload after the unique-modeless blocker cleared.";
            string restoreSummary = ShowPacketOwnedAdminShopOwnerWindow(
                adminShopWindow,
                restoreReason);
            if (!string.IsNullOrWhiteSpace(restoreSummary))
            {
                ShowUtilityFeedbackMessage(restoreSummary);
            }
        }

        private bool IsCashShopOwnerFamilyVisibleWithoutAdminShopOwner()
        {
            if (uiWindowManager == null)
            {
                return false;
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.CashShopStage)?.IsVisible == true
                || uiWindowManager.GetWindow(MapSimulatorWindowNames.CashAvatarPreview)?.IsVisible == true
                || uiWindowManager.GetWindow(MapSimulatorWindowNames.CashTradingRoom)?.IsVisible == true)
            {
                return true;
            }

            for (int i = 0; i < CashShopChildOwnerWindowNames.Length; i++)
            {
                if (uiWindowManager.GetWindow(CashShopChildOwnerWindowNames[i])?.IsVisible == true)
                {
                    return true;
                }
            }

            for (int i = 0; i < CashShopModalOwnerWindowNames.Length; i++)
            {
                if (uiWindowManager.GetWindow(CashShopModalOwnerWindowNames[i])?.IsVisible == true)
                {
                    return true;
                }
            }

            return false;
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
                    return HandlePacketOwnedBattleRecordShowCommand();

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

                case "timer":
                case "timerset":
                    if (args.Length < 2
                        || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int timerSeconds)
                        || timerSeconds <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /npcutility battlerecord timer <seconds>");
                    }

                    bool hasTimerOutbound = _packetOwnedBattleRecordRuntime.TryBuildTimerSetOutboundRequest(
                        timerSeconds,
                        currTickCount,
                        out PacketOwnedNpcUtilityOutboundRequest timerRequest,
                        out string timerMessage);
                    return ChatCommandHandler.CommandResult.Ok(
                        DispatchPacketOwnedBattleRecordOutboundRequest(
                            hasTimerOutbound,
                            timerRequest,
                            timerMessage,
                            hasTimerOutbound ? null : timerMessage));

                case "timerstop":
                case "timerpause":
                case "timerresume":
                case "timerstopresume":
                    bool hasTimerStopResumeOutbound = _packetOwnedBattleRecordRuntime.TryBuildTimerStopResumeOutboundRequest(
                        currTickCount,
                        out PacketOwnedNpcUtilityOutboundRequest timerStopResumeRequest,
                        out string timerStopResumeMessage);
                    return ChatCommandHandler.CommandResult.Ok(
                        DispatchPacketOwnedBattleRecordOutboundRequest(
                            hasTimerStopResumeOutbound,
                            timerStopResumeRequest,
                            timerStopResumeMessage,
                            hasTimerStopResumeOutbound ? null : timerStopResumeMessage));

                case "viewtoggle":
                case "shelltoggle":
                case "fold":
                case "expand":
                    return ChatCommandHandler.CommandResult.Ok(_packetOwnedBattleRecordRuntime.ToggleExtended());

                case "dot":
                    return HandlePacketOwnedBattleRecordIncludeCommand(args.Skip(1).ToArray(), option: 0);

                case "summon":
                    return HandlePacketOwnedBattleRecordIncludeCommand(args.Skip(1).ToArray(), option: 1);

                case "damage":
                    return HandlePacketOwnedBattleRecordDamageCommand(args.Skip(1).ToArray());

                case "recovery":
                    return HandlePacketOwnedBattleRecordRecoveryCommand(args.Skip(1).ToArray());

                case "forceoff":
                    _packetOwnedBattleRecordRuntime.ApplyForcedOffCalc();
                    uiWindowManager?.HideWindow(MapSimulatorWindowNames.BattleRecord);
                    return ChatCommandHandler.CommandResult.Ok(BuildPacketOwnedBattleRecordFooter());

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
                    return ChatCommandHandler.CommandResult.Error("Usage: /npcutility battlerecord [status|show|on|off|toggle|timer <seconds>|timerstop|viewtoggle|dot <on|off>|summon <on|off>|damage <value> [critical=<on|off>] [summon=<on|off>] [attrRate=<value>]|recovery <hpRecovery> <mpRecovery> <beforeHp> <beforeMp> [currentHp=<value>] [currentMp=<value>] [wvsContext=<on|off>]|forceoff|clear <damage|recovery|all>|page <summary|dot|packets>|close]");
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

        private ChatCommandHandler.CommandResult HandlePacketOwnedBattleRecordShowCommand()
        {
            bool hasOpenOutbound = _packetOwnedBattleRecordRuntime.TryBuildOwnerOpenOutboundRequest(
                out PacketOwnedNpcUtilityOutboundRequest openRequest,
                out string openMessage);
            string showMessage = ShowPacketOwnedUniqueUtilityWindow(
                MapSimulatorWindowNames.BattleRecord,
                "Battle Record",
                BuildPacketOwnedBattleRecordFooter());
            if (!hasOpenOutbound)
            {
                return ChatCommandHandler.CommandResult.Ok(string.IsNullOrWhiteSpace(openMessage)
                    ? showMessage
                    : $"{showMessage} {openMessage}");
            }

            return ChatCommandHandler.CommandResult.Ok(
                DispatchPacketOwnedBattleRecordOutboundRequest(
                    hasOpenOutbound,
                    openRequest,
                    showMessage,
                    null));
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

        private ChatCommandHandler.CommandResult HandlePacketOwnedBattleRecordDamageCommand(string[] args)
        {
            if (args == null
                || args.Length < 1
                || !int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int damage))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /npcutility battlerecord damage <value> [critical=<on|off>] [summon=<on|off>] [attrRate=<value>]");
            }

            bool critical = false;
            bool isSummon = false;
            int? attrRate = null;
            for (int i = 1; i < args.Length; i++)
            {
                string argument = args[i];
                if (argument.StartsWith("critical=", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryParseOnOffArgument(argument.Substring("critical=".Length), out critical))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /npcutility battlerecord damage <value> [critical=<on|off>] [summon=<on|off>] [attrRate=<value>]");
                    }

                    continue;
                }

                if (argument.StartsWith("summon=", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryParseOnOffArgument(argument.Substring("summon=".Length), out isSummon))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /npcutility battlerecord damage <value> [critical=<on|off>] [summon=<on|off>] [attrRate=<value>]");
                    }

                    continue;
                }

                if (argument.StartsWith("attrrate=", StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(argument.Substring("attrrate=".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedAttrRate))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /npcutility battlerecord damage <value> [critical=<on|off>] [summon=<on|off>] [attrRate=<value>]");
                    }

                    attrRate = parsedAttrRate;
                    continue;
                }

                return ChatCommandHandler.CommandResult.Error("Usage: /npcutility battlerecord damage <value> [critical=<on|off>] [summon=<on|off>] [attrRate=<value>]");
            }

            return ChatCommandHandler.CommandResult.Ok(
                _packetOwnedBattleRecordRuntime.ApplyBattleDamageInfo(
                    damage,
                    critical,
                    isSummon,
                    attrRate));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedBattleRecordRecoveryCommand(string[] args)
        {
            if (args == null
                || args.Length < 4
                || !int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int hpRecovery)
                || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int mpRecovery)
                || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int beforeHp)
                || !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int beforeMp))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /npcutility battlerecord recovery <hpRecovery> <mpRecovery> <beforeHp> <beforeMp> [currentHp=<value>] [currentMp=<value>] [wvsContext=<on|off>]");
            }

            int? currentHp = null;
            int? currentMp = null;
            bool hasWvsContext = true;
            for (int i = 4; i < args.Length; i++)
            {
                string argument = args[i];
                if (argument.StartsWith("currenthp=", StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(argument.Substring("currenthp=".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedCurrentHp))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /npcutility battlerecord recovery <hpRecovery> <mpRecovery> <beforeHp> <beforeMp> [currentHp=<value>] [currentMp=<value>] [wvsContext=<on|off>]");
                    }

                    currentHp = parsedCurrentHp;
                    continue;
                }

                if (argument.StartsWith("currentmp=", StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(argument.Substring("currentmp=".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedCurrentMp))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /npcutility battlerecord recovery <hpRecovery> <mpRecovery> <beforeHp> <beforeMp> [currentHp=<value>] [currentMp=<value>] [wvsContext=<on|off>]");
                    }

                    currentMp = parsedCurrentMp;
                    continue;
                }

                if (argument.StartsWith("wvscontext=", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryParseOnOffArgument(argument.Substring("wvscontext=".Length), out hasWvsContext))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /npcutility battlerecord recovery <hpRecovery> <mpRecovery> <beforeHp> <beforeMp> [currentHp=<value>] [currentMp=<value>] [wvsContext=<on|off>]");
                    }

                    continue;
                }

                return ChatCommandHandler.CommandResult.Error("Usage: /npcutility battlerecord recovery <hpRecovery> <mpRecovery> <beforeHp> <beforeMp> [currentHp=<value>] [currentMp=<value>] [wvsContext=<on|off>]");
            }

            return ChatCommandHandler.CommandResult.Ok(
                _packetOwnedBattleRecordRuntime.ApplyBattleRecoveryInfo(
                    hpRecovery,
                    mpRecovery,
                    beforeHp,
                    beforeMp,
                    currentHp,
                    currentMp,
                    currTickCount,
                    hasWvsContext));
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
