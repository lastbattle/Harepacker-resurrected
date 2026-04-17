using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using System;
using System.Net;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private ChatCommandHandler.CommandResult HandleMessengerSessionCommand(string[] args)
        {
            const string usage = "Usage: /messenger session [status|table|discover <remotePort> [processName|pid] [localPort]|history [count]|historyin [count]|clearhistory|clearhistoryin|replay <historyIndex>|send <invite <name>|accept [name]|leave|room <message>|claim <target>|<type>|<context>[|<chatLog>]|blocked <inviter> [localName] [blocked]>|queue <invite <name>|accept [name]|leave|room <message>|claim <target>|<type>|<context>[|<chatLog>]|blocked <inviter> [localName] [blocked]>|sendraw <hex>|queueraw <hex>|sendpacketraw <opcode-framed-hex>|start <listenPort> <serverHost> <serverPort> [inboundOpcode]|startauto <listenPort> <remotePort> [inboundOpcode] [processName|pid] [localPort]|stop]";
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribeMessengerOfficialSessionBridgeStatus());
            }

            if (string.Equals(args[0], "table", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(_messengerOfficialSessionBridge.DescribeRecoveredPacketTable());
            }

            if (string.Equals(args[0], "discover", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2
                    || !int.TryParse(args[1], out int discoverRemotePort)
                    || discoverRemotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /messenger session discover <remotePort> [processName|pid] [localPort]");
                }

                string processSelector = args.Length >= 3 ? args[2] : null;
                int? localPortFilter = null;
                if (args.Length >= 4)
                {
                    if (!int.TryParse(args[3], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger session discover <remotePort> [processName|pid] [localPort]");
                    }

                    localPortFilter = parsedLocalPort;
                }

                return ChatCommandHandler.CommandResult.Info(
                    _messengerOfficialSessionBridge.DescribeDiscoveredSessions(discoverRemotePort, processSelector, localPortFilter));
            }

            if (string.Equals(args[0], "history", StringComparison.OrdinalIgnoreCase))
            {
                int count = 10;
                if (args.Length >= 2 && (!int.TryParse(args[1], out count) || count <= 0))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /messenger session history [count]");
                }

                return ChatCommandHandler.CommandResult.Info(_messengerOfficialSessionBridge.DescribeRecentOutboundPackets(count));
            }

            if (string.Equals(args[0], "historyin", StringComparison.OrdinalIgnoreCase))
            {
                int count = 10;
                if (args.Length >= 2 && (!int.TryParse(args[1], out count) || count <= 0))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /messenger session historyin [count]");
                }

                return ChatCommandHandler.CommandResult.Info(_messengerOfficialSessionBridge.DescribeRecentInboundPackets(count));
            }

            if (string.Equals(args[0], "clearhistory", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Ok(_messengerOfficialSessionBridge.ClearRecentOutboundPackets());
            }

            if (string.Equals(args[0], "clearhistoryin", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Ok(_messengerOfficialSessionBridge.ClearRecentInboundPackets());
            }

            if (string.Equals(args[0], "replay", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2 || !int.TryParse(args[1], out int historyIndex) || historyIndex <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /messenger session replay <historyIndex>");
                }

                return _messengerOfficialSessionBridge.TryReplayRecentOutboundPacket(historyIndex, out string replayStatus)
                    ? ChatCommandHandler.CommandResult.Ok(replayStatus)
                    : ChatCommandHandler.CommandResult.Error(replayStatus);
            }

            if (string.Equals(args[0], "send", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "queue", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /messenger session <send|queue> <invite <name>|accept [name]|leave|room <message>|claim <target>|<type>|<context>[|<chatLog>]|blocked <inviter> [localName] [blocked]>");
                }

                bool queueOnly = string.Equals(args[0], "queue", StringComparison.OrdinalIgnoreCase);
                switch (args[1].ToLowerInvariant())
                {
                    case "invite":
                        if (args.Length < 3)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /messenger session <send|queue> invite <name>");
                        }

                        return TryMirrorMessengerInviteClientRequest(string.Join(" ", args, 2, args.Length - 2), out string inviteStatus, queueOnly)
                            ? ChatCommandHandler.CommandResult.Ok(inviteStatus)
                            : ChatCommandHandler.CommandResult.Error(inviteStatus);
                    case "accept":
                    case "join":
                        return TryMirrorMessengerIncomingInviteAcceptClientRequest(args.Length >= 3 ? string.Join(" ", args, 2, args.Length - 2) : string.Empty, out string acceptStatus, queueOnly)
                            ? ChatCommandHandler.CommandResult.Ok(acceptStatus)
                            : ChatCommandHandler.CommandResult.Error(acceptStatus);
                    case "leave":
                    case "destroy":
                        return TryMirrorMessengerLeaveClientRequest(out string leaveStatus, queueOnly)
                            ? ChatCommandHandler.CommandResult.Ok(leaveStatus)
                            : ChatCommandHandler.CommandResult.Error(leaveStatus);
                    case "room":
                        return TryMirrorMessengerProcessChatClientRequest(string.Join(" ", args, 2, args.Length - 2), out string roomStatus, queueOnly)
                            ? ChatCommandHandler.CommandResult.Ok(roomStatus)
                            : ChatCommandHandler.CommandResult.Error(roomStatus);
                    case "claim":
                        return TryMirrorMessengerClaimClientRequest(string.Join(" ", args, 2, args.Length - 2), out string claimStatus, queueOnly)
                            ? ChatCommandHandler.CommandResult.Ok(claimStatus)
                            : ChatCommandHandler.CommandResult.Error(claimStatus);
                    case "blocked":
                    case "autoreject":
                        return TryMirrorMessengerBlockedAutoRejectClientRequest(
                                string.Join(" ", args, 2, args.Length - 2),
                                out string blockedStatus,
                                queueOnly)
                            ? ChatCommandHandler.CommandResult.Ok(blockedStatus)
                            : ChatCommandHandler.CommandResult.Error(blockedStatus);
                    default:
                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger session <send|queue> <invite <name>|accept [name]|leave|room <message>|claim <target>|<type>|<context>[|<chatLog>]|blocked <inviter> [localName] [blocked]>");
                }
            }

            if (string.Equals(args[0], "sendraw", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2 || !TryDecodeHexBytes(string.Join(string.Empty, args, 1, args.Length - 1), out byte[] payload))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /messenger session sendraw <hex>");
                }

                return _messengerOfficialSessionBridge.TrySendOutboundPacket(
                        MessengerPacketCodec.ClientMessengerRequestOpcode,
                        payload,
                        out string sendStatus)
                    ? ChatCommandHandler.CommandResult.Ok(sendStatus)
                    : ChatCommandHandler.CommandResult.Error(sendStatus);
            }

            if (string.Equals(args[0], "queueraw", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2 || !TryDecodeHexBytes(string.Join(string.Empty, args, 1, args.Length - 1), out byte[] payload))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /messenger session queueraw <hex>");
                }

                return _messengerOfficialSessionBridge.TryQueueOutboundPacket(
                        MessengerPacketCodec.ClientMessengerRequestOpcode,
                        payload,
                        out string queueStatus)
                    ? ChatCommandHandler.CommandResult.Ok(queueStatus)
                    : ChatCommandHandler.CommandResult.Error(queueStatus);
            }

            if (string.Equals(args[0], "sendpacketraw", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2 || !TryDecodeHexBytes(string.Join(string.Empty, args, 1, args.Length - 1), out byte[] rawPacket))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /messenger session sendpacketraw <opcode-framed-hex>");
                }

                return _messengerOfficialSessionBridge.TrySendOutboundRawPacket(rawPacket, out string rawStatus)
                    ? ChatCommandHandler.CommandResult.Ok(rawStatus)
                    : ChatCommandHandler.CommandResult.Error(rawStatus);
            }

            if (string.Equals(args[0], "start", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 4
                    || !int.TryParse(args[1], out int listenPort)
                    || listenPort <= 0
                    || !int.TryParse(args[3], out int remotePort)
                    || remotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /messenger session start <listenPort> <serverHost> <serverPort> [inboundOpcode]");
                }

                ushort inboundOpcode = PacketOwnedSocialUtilityPacketTable.ResolveRecoveredInboundOpcode("Messenger", 0);
                if (args.Length >= 5)
                {
                    if (!ushort.TryParse(args[4], out ushort requestedInboundOpcode) || requestedInboundOpcode == 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger session start <listenPort> <serverHost> <serverPort> [inboundOpcode]");
                    }

                    ushort resolvedInboundOpcode = PacketOwnedSocialUtilityPacketTable.ResolveRecoveredInboundOpcode("Messenger", requestedInboundOpcode);
                    if (resolvedInboundOpcode != requestedInboundOpcode)
                    {
                        return ChatCommandHandler.CommandResult.Error(
                            $"Messenger inbound opcode {requestedInboundOpcode} is outside the recovered table ({string.Join("/", PacketOwnedSocialUtilityPacketTable.GetRecoveredInboundOpcodes("Messenger"))}).");
                    }

                    inboundOpcode = resolvedInboundOpcode;
                }

                _messengerOfficialSessionBridgeEnabled = true;
                _messengerOfficialSessionBridgeUseDiscovery = false;
                _messengerOfficialSessionBridgeConfiguredListenPort = listenPort;
                _messengerOfficialSessionBridgeConfiguredRemoteHost = args[2];
                _messengerOfficialSessionBridgeConfiguredRemotePort = remotePort;
                _messengerOfficialSessionBridgeConfiguredInboundOpcode = inboundOpcode;
                _messengerOfficialSessionBridgeConfiguredProcessSelector = null;
                _messengerOfficialSessionBridgeConfiguredLocalPort = null;
                EnsureMessengerOfficialSessionBridgeState(shouldRun: true);
                return ChatCommandHandler.CommandResult.Ok(DescribeMessengerOfficialSessionBridgeStatus());
            }

            if (string.Equals(args[0], "startauto", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3
                    || !int.TryParse(args[1], out int autoListenPort)
                    || autoListenPort <= 0
                    || !int.TryParse(args[2], out int autoRemotePort)
                    || autoRemotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /messenger session startauto <listenPort> <remotePort> [inboundOpcode] [processName|pid] [localPort]");
                }

                int argumentIndex = 3;
                ushort autoInboundOpcode = PacketOwnedSocialUtilityPacketTable.ResolveRecoveredInboundOpcode("Messenger", 0);
                if (args.Length >= 4 && ushort.TryParse(args[3], out ushort parsedInboundOpcode) && parsedInboundOpcode != 0)
                {
                    ushort resolvedInboundOpcode = PacketOwnedSocialUtilityPacketTable.ResolveRecoveredInboundOpcode("Messenger", parsedInboundOpcode);
                    if (resolvedInboundOpcode != parsedInboundOpcode)
                    {
                        return ChatCommandHandler.CommandResult.Error(
                            $"Messenger inbound opcode {parsedInboundOpcode} is outside the recovered table ({string.Join("/", PacketOwnedSocialUtilityPacketTable.GetRecoveredInboundOpcodes("Messenger"))}).");
                    }

                    autoInboundOpcode = resolvedInboundOpcode;
                    argumentIndex = 4;
                }

                string processSelector = args.Length >= argumentIndex + 1 ? args[argumentIndex] : null;
                int? localPortFilter = null;
                if (args.Length >= argumentIndex + 2)
                {
                    if (!int.TryParse(args[argumentIndex + 1], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger session startauto <listenPort> <remotePort> [inboundOpcode] [processName|pid] [localPort]");
                    }

                    localPortFilter = parsedLocalPort;
                }

                _messengerOfficialSessionBridgeEnabled = true;
                _messengerOfficialSessionBridgeUseDiscovery = true;
                _messengerOfficialSessionBridgeConfiguredListenPort = autoListenPort;
                _messengerOfficialSessionBridgeConfiguredRemotePort = autoRemotePort;
                _messengerOfficialSessionBridgeConfiguredInboundOpcode = autoInboundOpcode;
                _messengerOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
                _messengerOfficialSessionBridgeConfiguredProcessSelector = processSelector;
                _messengerOfficialSessionBridgeConfiguredLocalPort = localPortFilter;
                _nextMessengerOfficialSessionBridgeDiscoveryRefreshAt = 0;

                return _messengerOfficialSessionBridge.TryRefreshFromDiscovery(
                        autoListenPort,
                        autoRemotePort,
                        autoInboundOpcode,
                        processSelector,
                        localPortFilter,
                        out string startStatus)
                    ? ChatCommandHandler.CommandResult.Ok($"{startStatus} {DescribeMessengerOfficialSessionBridgeStatus()}")
                    : ChatCommandHandler.CommandResult.Error(startStatus);
            }

            if (string.Equals(args[0], "stop", StringComparison.OrdinalIgnoreCase))
            {
                _messengerOfficialSessionBridgeEnabled = false;
                _messengerOfficialSessionBridgeUseDiscovery = false;
                _messengerOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
                _messengerOfficialSessionBridgeConfiguredRemotePort = 0;
                _messengerOfficialSessionBridgeConfiguredInboundOpcode = PacketOwnedSocialUtilityPacketTable.ResolveRecoveredInboundOpcode("Messenger", 0);
                _messengerOfficialSessionBridgeConfiguredProcessSelector = null;
                _messengerOfficialSessionBridgeConfiguredLocalPort = null;
                _messengerOfficialSessionBridge.Stop();
                return ChatCommandHandler.CommandResult.Ok(DescribeMessengerOfficialSessionBridgeStatus());
            }

            return ChatCommandHandler.CommandResult.Error(usage);
        }
    }
}
