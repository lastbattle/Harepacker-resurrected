using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using System;
using System.Net;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private ChatCommandHandler.CommandResult HandleMapleTvSessionCommand(string[] args)
        {
            const string usage = "Usage: /mapletv session [status|table|discover <remotePort> [processName|pid] [localPort]|history [count]|historyin [count]|clearhistory|clearhistoryin|replay <historyIndex>|send consume <inventoryPosition> [itemId]|queue consume <inventoryPosition> [itemId]|sendraw <hex>|queueraw <hex>|sendpacketraw <opcode-framed-hex>|start <listenPort> <serverHost> <serverPort> [405|406|407|table]|startauto <listenPort> <remotePort> [405|406|407|table] [processName|pid] [localPort]|stop]";
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribeMapleTvOfficialSessionBridgeStatus());
            }

            if (string.Equals(args[0], "table", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(_mapleTvOfficialSessionBridge.DescribeRecoveredPacketTable());
            }

            if (string.Equals(args[0], "discover", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2
                    || !int.TryParse(args[1], out int discoverRemotePort)
                    || discoverRemotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /mapletv session discover <remotePort> [processName|pid] [localPort]");
                }

                string processSelector = args.Length >= 3 ? args[2] : null;
                int? localPortFilter = null;
                if (args.Length >= 4)
                {
                    if (!int.TryParse(args[3], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /mapletv session discover <remotePort> [processName|pid] [localPort]");
                    }

                    localPortFilter = parsedLocalPort;
                }

                return ChatCommandHandler.CommandResult.Info(
                    _mapleTvOfficialSessionBridge.DescribeDiscoveredSessions(discoverRemotePort, processSelector, localPortFilter));
            }

            if (string.Equals(args[0], "history", StringComparison.OrdinalIgnoreCase))
            {
                int count = 10;
                if (args.Length >= 2 && (!int.TryParse(args[1], out count) || count <= 0))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /mapletv session history [count]");
                }

                return ChatCommandHandler.CommandResult.Info(_mapleTvOfficialSessionBridge.DescribeRecentOutboundPackets(count));
            }

            if (string.Equals(args[0], "historyin", StringComparison.OrdinalIgnoreCase))
            {
                int count = 10;
                if (args.Length >= 2 && (!int.TryParse(args[1], out count) || count <= 0))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /mapletv session historyin [count]");
                }

                return ChatCommandHandler.CommandResult.Info(_mapleTvOfficialSessionBridge.DescribeRecentInboundPackets(count));
            }

            if (string.Equals(args[0], "clearhistory", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Ok(_mapleTvOfficialSessionBridge.ClearRecentOutboundPackets());
            }

            if (string.Equals(args[0], "clearhistoryin", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Ok(_mapleTvOfficialSessionBridge.ClearRecentInboundPackets());
            }

            if (string.Equals(args[0], "replay", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2 || !int.TryParse(args[1], out int historyIndex) || historyIndex <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /mapletv session replay <historyIndex>");
                }

                return _mapleTvOfficialSessionBridge.TryReplayRecentOutboundPacket(historyIndex, out string replayStatus)
                    ? ChatCommandHandler.CommandResult.Ok(replayStatus)
                    : ChatCommandHandler.CommandResult.Error(replayStatus);
            }

            if (string.Equals(args[0], "send", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "queue", StringComparison.OrdinalIgnoreCase))
            {
                bool queueOnly = string.Equals(args[0], "queue", StringComparison.OrdinalIgnoreCase);
                if (args.Length < 3 || !string.Equals(args[1], "consume", StringComparison.OrdinalIgnoreCase))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /mapletv session <send|queue> consume <inventoryPosition> [itemId]");
                }

                if (!int.TryParse(args[2], out int inventoryPosition) || inventoryPosition <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /mapletv session <send|queue> consume <inventoryPosition> [itemId]");
                }

                int overrideItemId = 0;
                if (args.Length >= 4 && (!int.TryParse(args[3], out overrideItemId) || overrideItemId < 0))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /mapletv session <send|queue> consume <inventoryPosition> [itemId]");
                }

                return TryMirrorMapleTvConsumeCashItemUseClientRequest(inventoryPosition, overrideItemId, queueOnly, out string consumeStatus)
                    ? ChatCommandHandler.CommandResult.Ok(consumeStatus)
                    : ChatCommandHandler.CommandResult.Error(consumeStatus);
            }

            if (string.Equals(args[0], "sendraw", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2 || !TryDecodeHexBytes(string.Join(string.Empty, args, 1, args.Length - 1), out byte[] payload))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /mapletv session sendraw <hex>");
                }

                return _mapleTvOfficialSessionBridge.TrySendOutboundPacket(
                        MapleTvRuntime.ConsumeCashItemUseRequestOpcode,
                        payload,
                        out string sendStatus)
                    ? ChatCommandHandler.CommandResult.Ok(sendStatus)
                    : ChatCommandHandler.CommandResult.Error(sendStatus);
            }

            if (string.Equals(args[0], "queueraw", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2 || !TryDecodeHexBytes(string.Join(string.Empty, args, 1, args.Length - 1), out byte[] payload))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /mapletv session queueraw <hex>");
                }

                return _mapleTvOfficialSessionBridge.TryQueueOutboundPacket(
                        MapleTvRuntime.ConsumeCashItemUseRequestOpcode,
                        payload,
                        out string queueStatus)
                    ? ChatCommandHandler.CommandResult.Ok(queueStatus)
                    : ChatCommandHandler.CommandResult.Error(queueStatus);
            }

            if (string.Equals(args[0], "sendpacketraw", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2 || !TryDecodeHexBytes(string.Join(string.Empty, args, 1, args.Length - 1), out byte[] rawPacket))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /mapletv session sendpacketraw <opcode-framed-hex>");
                }

                return _mapleTvOfficialSessionBridge.TrySendOutboundRawPacket(rawPacket, out string rawStatus)
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
                    return ChatCommandHandler.CommandResult.Error("Usage: /mapletv session start <listenPort> <serverHost> <serverPort> [405|406|407|table]");
                }

                ushort inboundOpcode = MapleTvRuntime.PacketTypeSetMessage;
                if (args.Length >= 5
                    && !TryResolveMapleTvInboundOpcodeToken(args[4], out inboundOpcode))
                {
                    return ChatCommandHandler.CommandResult.Error(
                        $"Usage: /mapletv session start <listenPort> <serverHost> <serverPort> [405|406|407|table]. {DescribeMapleTvInboundOpcodeSet()}");
                }

                _mapleTvOfficialSessionBridgeEnabled = true;
                _mapleTvOfficialSessionBridgeUseDiscovery = false;
                _mapleTvOfficialSessionBridgeConfiguredListenPort = listenPort;
                _mapleTvOfficialSessionBridgeConfiguredRemoteHost = args[2];
                _mapleTvOfficialSessionBridgeConfiguredRemotePort = remotePort;
                _mapleTvOfficialSessionBridgeConfiguredInboundOpcode = inboundOpcode;
                _mapleTvOfficialSessionBridgeConfiguredProcessSelector = null;
                _mapleTvOfficialSessionBridgeConfiguredLocalPort = null;
                EnsureMapleTvOfficialSessionBridgeState(shouldRun: true);
                return ChatCommandHandler.CommandResult.Ok(DescribeMapleTvOfficialSessionBridgeStatus());
            }

            if (string.Equals(args[0], "startauto", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3
                    || !int.TryParse(args[1], out int autoListenPort)
                    || autoListenPort <= 0
                    || !int.TryParse(args[2], out int autoRemotePort)
                    || autoRemotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /mapletv session startauto <listenPort> <remotePort> [405|406|407|table] [processName|pid] [localPort]");
                }

                int argumentIndex = 3;
                ushort autoInboundOpcode = MapleTvRuntime.PacketTypeSetMessage;
                if (args.Length >= 4 && TryResolveMapleTvInboundOpcodeToken(args[3], out ushort parsedInboundOpcode))
                {
                    autoInboundOpcode = parsedInboundOpcode;
                    argumentIndex = 4;
                }
                else if (args.Length >= 4
                         && IsMapleTvInboundOpcodeToken(args[3])
                         && !TryResolveMapleTvInboundOpcodeToken(args[3], out _))
                {
                    return ChatCommandHandler.CommandResult.Error(
                        $"Usage: /mapletv session startauto <listenPort> <remotePort> [405|406|407|table] [processName|pid] [localPort]. {DescribeMapleTvInboundOpcodeSet()}");
                }

                string processSelector = args.Length >= argumentIndex + 1 ? args[argumentIndex] : null;
                int? localPortFilter = null;
                if (args.Length >= argumentIndex + 2)
                {
                    if (!int.TryParse(args[argumentIndex + 1], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /mapletv session startauto <listenPort> <remotePort> [405|406|407|table] [processName|pid] [localPort]");
                    }

                    localPortFilter = parsedLocalPort;
                }

                _mapleTvOfficialSessionBridgeEnabled = true;
                _mapleTvOfficialSessionBridgeUseDiscovery = true;
                _mapleTvOfficialSessionBridgeConfiguredListenPort = autoListenPort;
                _mapleTvOfficialSessionBridgeConfiguredRemotePort = autoRemotePort;
                _mapleTvOfficialSessionBridgeConfiguredInboundOpcode = autoInboundOpcode;
                _mapleTvOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
                _mapleTvOfficialSessionBridgeConfiguredProcessSelector = processSelector;
                _mapleTvOfficialSessionBridgeConfiguredLocalPort = localPortFilter;
                _nextMapleTvOfficialSessionBridgeDiscoveryRefreshAt = 0;

                return _mapleTvOfficialSessionBridge.TryRefreshFromDiscovery(
                        autoListenPort,
                        autoRemotePort,
                        autoInboundOpcode,
                        processSelector,
                        localPortFilter,
                        out string startStatus)
                    ? ChatCommandHandler.CommandResult.Ok($"{startStatus} {DescribeMapleTvOfficialSessionBridgeStatus()}")
                    : ChatCommandHandler.CommandResult.Error(startStatus);
            }

            if (string.Equals(args[0], "stop", StringComparison.OrdinalIgnoreCase))
            {
                _mapleTvOfficialSessionBridgeEnabled = false;
                _mapleTvOfficialSessionBridgeUseDiscovery = false;
                _mapleTvOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
                _mapleTvOfficialSessionBridgeConfiguredRemotePort = 0;
                _mapleTvOfficialSessionBridgeConfiguredInboundOpcode = MapleTvRuntime.PacketTypeSetMessage;
                _mapleTvOfficialSessionBridgeConfiguredProcessSelector = null;
                _mapleTvOfficialSessionBridgeConfiguredLocalPort = null;
                _mapleTvOfficialSessionBridge.Stop();
                return ChatCommandHandler.CommandResult.Ok(DescribeMapleTvOfficialSessionBridgeStatus());
            }

            return ChatCommandHandler.CommandResult.Error(usage);
        }

        private static string DescribeMapleTvInboundOpcodeSet()
        {
            return $"Recovered MapleTV inbound opcodes are {MapleTvRuntime.PacketTypeSetMessage}/{MapleTvRuntime.PacketTypeClearMessage}/{MapleTvRuntime.PacketTypeSendMessageResult}.";
        }

        private static bool IsMapleTvInboundOpcodeToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string normalizedToken = token.Trim();
            return string.Equals(normalizedToken, "table", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedToken, "default", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedToken, "auto", StringComparison.OrdinalIgnoreCase)
                || ushort.TryParse(normalizedToken, out _);
        }

        private static bool TryResolveMapleTvInboundOpcodeToken(string token, out ushort inboundOpcode)
        {
            inboundOpcode = MapleTvRuntime.PacketTypeSetMessage;
            if (string.IsNullOrWhiteSpace(token))
            {
                return true;
            }

            string normalizedToken = token.Trim();
            if (string.Equals(normalizedToken, "table", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedToken, "default", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedToken, "auto", StringComparison.OrdinalIgnoreCase))
            {
                inboundOpcode = MapleTvRuntime.PacketTypeSetMessage;
                return true;
            }

            if (!ushort.TryParse(normalizedToken, out ushort parsedOpcode))
            {
                return false;
            }

            if (parsedOpcode == MapleTvRuntime.PacketTypeSetMessage
                || parsedOpcode == MapleTvRuntime.PacketTypeClearMessage
                || parsedOpcode == MapleTvRuntime.PacketTypeSendMessageResult)
            {
                inboundOpcode = parsedOpcode;
                return true;
            }

            return false;
        }

        private bool TryMirrorMapleTvConsumeCashItemUseClientRequest(
            int inventoryPosition,
            int overrideItemId,
            bool queueOnly,
            out string message)
        {
            _mapleTvRuntime.UpdateLocalContext(_playerManager?.Player?.Build);
            if (!_mapleTvRuntime.TryBuildConsumeCashItemUseRequestPayload(
                    currTickCount,
                    inventoryPosition,
                    overrideItemId,
                    out byte[] payload,
                    out string error))
            {
                message = error;
                return false;
            }

            string payloadHex = Convert.ToHexString(payload);
            if (queueOnly)
            {
                if (_mapleTvOfficialSessionBridge.TryQueueOutboundPacket(MapleTvRuntime.ConsumeCashItemUseRequestOpcode, payload, out string queueStatus))
                {
                    message = $"Queued CUserLocal::ConsumeCashItem MapleTV request opcode {MapleTvRuntime.ConsumeCashItemUseRequestOpcode} [{payloadHex}]. {queueStatus}";
                    return true;
                }

                message = queueStatus;
                return false;
            }

            if (_mapleTvOfficialSessionBridge.TrySendOutboundPacket(MapleTvRuntime.ConsumeCashItemUseRequestOpcode, payload, out string sendStatus))
            {
                message = $"Dispatched CUserLocal::ConsumeCashItem MapleTV request opcode {MapleTvRuntime.ConsumeCashItemUseRequestOpcode} [{payloadHex}]. {sendStatus}";
                return true;
            }

            if ((_mapleTvOfficialSessionBridgeEnabled || _mapleTvOfficialSessionBridge.IsRunning)
                && _mapleTvOfficialSessionBridge.TryQueueOutboundPacket(MapleTvRuntime.ConsumeCashItemUseRequestOpcode, payload, out string deferredStatus))
            {
                message = $"Queued CUserLocal::ConsumeCashItem MapleTV request opcode {MapleTvRuntime.ConsumeCashItemUseRequestOpcode} [{payloadHex}] after live dispatch was unavailable. Bridge: {sendStatus} Deferred bridge: {deferredStatus}";
                return true;
            }

            message = sendStatus;
            return false;
        }

        private bool TryApplyMapleTvOpcodeFramedClientPacket(byte[] rawPacket, out string message)
        {
            message = string.Empty;
            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                message = "MapleTV opcode-framed packet must include a 2-byte opcode.";
                return false;
            }

            int opcode = BitConverter.ToUInt16(rawPacket, 0);
            byte[] payload = rawPacket.Length == sizeof(ushort)
                ? Array.Empty<byte>()
                : rawPacket[sizeof(ushort)..];
            switch (opcode)
            {
                case MapleTvRuntime.PacketTypeSetMessage:
                case MapleTvRuntime.PacketTypeClearMessage:
                case MapleTvRuntime.PacketTypeSendMessageResult:
                    return TryApplyMapleTvPacket(opcode, payload, out message);

                default:
                    message = $"Unsupported MapleTV opcode-framed packet {opcode}. Expected {MapleTvRuntime.PacketTypeSetMessage}, {MapleTvRuntime.PacketTypeClearMessage}, or {MapleTvRuntime.PacketTypeSendMessageResult}.";
                    return false;
            }
        }
    }
}
