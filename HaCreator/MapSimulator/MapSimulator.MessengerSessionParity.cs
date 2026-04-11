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
            const string usage = "Usage: /messenger session [status|discover <remotePort> [processName|pid] [localPort]|send <invite <name>|accept [name]|leave|room <message>|claim <target>|<type>|<context>[|<chatLog>]>|queue <invite <name>|accept [name]|leave|room <message>|claim <target>|<type>|<context>[|<chatLog>]>|sendraw <hex>|queueraw <hex>|start <listenPort> <serverHost> <serverPort> <inboundOpcode>|startauto <listenPort> <remotePort> <inboundOpcode> [processName|pid] [localPort]|stop]";
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribeMessengerOfficialSessionBridgeStatus());
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

            if (string.Equals(args[0], "send", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "queue", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /messenger session <send|queue> <invite <name>|accept [name]|leave|room <message>|claim <target>|<type>|<context>[|<chatLog>]>");
                }

                bool queueOnly = string.Equals(args[0], "queue", StringComparison.OrdinalIgnoreCase);
                switch (args[1].ToLowerInvariant())
                {
                    case "invite":
                        if (args.Length < 4)
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
                    default:
                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger session <send|queue> <invite <name>|accept [name]|leave|room <message>|claim <target>|<type>|<context>[|<chatLog>]>");
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

            if (string.Equals(args[0], "start", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 5
                    || !int.TryParse(args[1], out int listenPort)
                    || listenPort <= 0
                    || !int.TryParse(args[3], out int remotePort)
                    || remotePort <= 0
                    || !ushort.TryParse(args[4], out ushort inboundOpcode)
                    || inboundOpcode == 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /messenger session start <listenPort> <serverHost> <serverPort> <inboundOpcode>");
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
                if (args.Length < 4
                    || !int.TryParse(args[1], out int autoListenPort)
                    || autoListenPort <= 0
                    || !int.TryParse(args[2], out int autoRemotePort)
                    || autoRemotePort <= 0
                    || !ushort.TryParse(args[3], out ushort autoInboundOpcode)
                    || autoInboundOpcode == 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /messenger session startauto <listenPort> <remotePort> <inboundOpcode> [processName|pid] [localPort]");
                }

                string processSelector = args.Length >= 5 ? args[4] : null;
                int? localPortFilter = null;
                if (args.Length >= 6)
                {
                    if (!int.TryParse(args[5], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger session startauto <listenPort> <remotePort> <inboundOpcode> [processName|pid] [localPort]");
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
                _messengerOfficialSessionBridgeConfiguredInboundOpcode = MessengerOfficialSessionBridgeManager.DefaultInboundResultOpcode;
                _messengerOfficialSessionBridgeConfiguredProcessSelector = null;
                _messengerOfficialSessionBridgeConfiguredLocalPort = null;
                _messengerOfficialSessionBridge.Stop();
                return ChatCommandHandler.CommandResult.Ok(DescribeMessengerOfficialSessionBridgeStatus());
            }

            return ChatCommandHandler.CommandResult.Error(usage);
        }
    }
}
