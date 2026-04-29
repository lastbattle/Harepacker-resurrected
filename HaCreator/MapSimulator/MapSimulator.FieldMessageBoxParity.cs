using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Character;
using Microsoft.Xna.Framework;
using MapleLib.PacketLib;
using System;
using System.Linq;
using System.Net;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int FieldMessageBoxOfficialSessionBridgeDiscoveryRefreshIntervalMs = 2000;
        private readonly FieldMessageBoxOfficialSessionBridgeManager _fieldMessageBoxOfficialSessionBridge;
        private const int DefaultMessageBoxConsumeRequestItemId = 5370000;
        private bool _fieldMessageBoxOfficialSessionBridgeEnabled;
        private bool _fieldMessageBoxOfficialSessionBridgeUseDiscovery;
        private int _fieldMessageBoxOfficialSessionBridgeConfiguredListenPort = FieldMessageBoxOfficialSessionBridgeManager.DefaultListenPort;
        private string _fieldMessageBoxOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
        private int _fieldMessageBoxOfficialSessionBridgeConfiguredRemotePort;
        private string _fieldMessageBoxOfficialSessionBridgeConfiguredProcessSelector;
        private int? _fieldMessageBoxOfficialSessionBridgeConfiguredLocalPort;
        private int _nextFieldMessageBoxOfficialSessionBridgeDiscoveryRefreshAt;

        private string DescribeFieldMessageBoxOfficialSessionBridgeStatus()
        {
            string enabledText = _fieldMessageBoxOfficialSessionBridgeEnabled ? "enabled" : "disabled";
            string modeText = _fieldMessageBoxOfficialSessionBridgeUseDiscovery ? "auto-discovery" : "direct proxy";
            string configuredTarget = _fieldMessageBoxOfficialSessionBridgeUseDiscovery
                ? _fieldMessageBoxOfficialSessionBridgeConfiguredLocalPort.HasValue
                    ? $"discover remote port {_fieldMessageBoxOfficialSessionBridgeConfiguredRemotePort} with local port {_fieldMessageBoxOfficialSessionBridgeConfiguredLocalPort.Value}"
                    : $"discover remote port {_fieldMessageBoxOfficialSessionBridgeConfiguredRemotePort}"
                : $"{_fieldMessageBoxOfficialSessionBridgeConfiguredRemoteHost}:{_fieldMessageBoxOfficialSessionBridgeConfiguredRemotePort}";
            string processText = string.IsNullOrWhiteSpace(_fieldMessageBoxOfficialSessionBridgeConfiguredProcessSelector)
                ? string.Empty
                : $" for {_fieldMessageBoxOfficialSessionBridgeConfiguredProcessSelector}";
            string listeningText = _fieldMessageBoxOfficialSessionBridge.IsRunning
                ? $"listening on 127.0.0.1:{_fieldMessageBoxOfficialSessionBridge.ListenPort}"
                : $"configured for 127.0.0.1:{_fieldMessageBoxOfficialSessionBridgeConfiguredListenPort}";
            return $"Field message-box session bridge {enabledText}, {modeText}, {listeningText}, target {configuredTarget}{processText}. {_fieldMessageBoxOfficialSessionBridge.LastStatus}";
        }

        private bool TrySyncFieldMessageBoxOfficialSessionBridgeFromLocalUtility(out string status)
        {
            int resolvedRemotePort = _localUtilityOfficialSessionBridgeConfiguredRemotePort > 0
                ? _localUtilityOfficialSessionBridgeConfiguredRemotePort
                : _localUtilityOfficialSessionBridge.RemotePort;
            if (resolvedRemotePort <= 0 || resolvedRemotePort > ushort.MaxValue)
            {
                status = "Message-box session bridge sync requires an active or configured local-utility official-session remote port.";
                return false;
            }

            _fieldMessageBoxOfficialSessionBridgeEnabled = true;
            _fieldMessageBoxOfficialSessionBridgeConfiguredListenPort = _fieldMessageBoxOfficialSessionBridgeConfiguredListenPort > 0
                ? _fieldMessageBoxOfficialSessionBridgeConfiguredListenPort
                : FieldMessageBoxOfficialSessionBridgeManager.DefaultListenPort;
            _fieldMessageBoxOfficialSessionBridgeConfiguredRemotePort = resolvedRemotePort;

            bool useDiscovery = _localUtilityOfficialSessionBridgeUseDiscovery
                || (_localUtilityOfficialSessionBridgeEnabled && string.IsNullOrWhiteSpace(_localUtilityOfficialSessionBridgeConfiguredRemoteHost));
            _fieldMessageBoxOfficialSessionBridgeUseDiscovery = useDiscovery;
            if (useDiscovery)
            {
                _fieldMessageBoxOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
                _fieldMessageBoxOfficialSessionBridgeConfiguredProcessSelector = _localUtilityOfficialSessionBridgeConfiguredProcessSelector;
                _fieldMessageBoxOfficialSessionBridgeConfiguredLocalPort = _localUtilityOfficialSessionBridgeConfiguredLocalPort;
                _nextFieldMessageBoxOfficialSessionBridgeDiscoveryRefreshAt = 0;
                if (_fieldMessageBoxOfficialSessionBridge.TryStartFromDiscovery(
                        _fieldMessageBoxOfficialSessionBridgeConfiguredListenPort,
                        _fieldMessageBoxOfficialSessionBridgeConfiguredRemotePort,
                        _fieldMessageBoxOfficialSessionBridgeConfiguredProcessSelector,
                        _fieldMessageBoxOfficialSessionBridgeConfiguredLocalPort,
                        out string discoverStatus))
                {
                    status = $"Synced message-box bridge from local-utility discovery settings. {discoverStatus}";
                    return true;
                }

                status = $"Message-box bridge sync from local-utility discovery settings failed. {discoverStatus}";
                return false;
            }

            string resolvedRemoteHost = !string.IsNullOrWhiteSpace(_localUtilityOfficialSessionBridgeConfiguredRemoteHost)
                ? _localUtilityOfficialSessionBridgeConfiguredRemoteHost
                : _localUtilityOfficialSessionBridge.RemoteHost;
            if (string.IsNullOrWhiteSpace(resolvedRemoteHost))
            {
                status = "Message-box session bridge sync requires a configured local-utility official-session remote host.";
                return false;
            }

            _fieldMessageBoxOfficialSessionBridgeConfiguredRemoteHost = resolvedRemoteHost;
            _fieldMessageBoxOfficialSessionBridgeConfiguredProcessSelector = null;
            _fieldMessageBoxOfficialSessionBridgeConfiguredLocalPort = null;
            EnsureFieldMessageBoxOfficialSessionBridgeState(shouldRun: true);
            status = $"Synced message-box bridge from local-utility direct-proxy settings. {DescribeFieldMessageBoxOfficialSessionBridgeStatus()}";
            return _fieldMessageBoxOfficialSessionBridge.IsRunning;
        }

        private void EnsureFieldMessageBoxOfficialSessionBridgeState(bool shouldRun)
        {
            if (!shouldRun || !_fieldMessageBoxOfficialSessionBridgeEnabled)
            {
                if (_fieldMessageBoxOfficialSessionBridge.IsRunning)
                {
                    _fieldMessageBoxOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_fieldMessageBoxOfficialSessionBridgeConfiguredListenPort <= 0
                || _fieldMessageBoxOfficialSessionBridgeConfiguredListenPort > ushort.MaxValue)
            {
                _fieldMessageBoxOfficialSessionBridge.Stop();
                _fieldMessageBoxOfficialSessionBridgeEnabled = false;
                _fieldMessageBoxOfficialSessionBridgeConfiguredListenPort = FieldMessageBoxOfficialSessionBridgeManager.DefaultListenPort;
                return;
            }

            if (_fieldMessageBoxOfficialSessionBridgeUseDiscovery)
            {
                if (_fieldMessageBoxOfficialSessionBridgeConfiguredRemotePort <= 0
                    || _fieldMessageBoxOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue)
                {
                    _fieldMessageBoxOfficialSessionBridge.Stop();
                    return;
                }

                _fieldMessageBoxOfficialSessionBridge.TryStartFromDiscovery(
                    _fieldMessageBoxOfficialSessionBridgeConfiguredListenPort,
                    _fieldMessageBoxOfficialSessionBridgeConfiguredRemotePort,
                    _fieldMessageBoxOfficialSessionBridgeConfiguredProcessSelector,
                    _fieldMessageBoxOfficialSessionBridgeConfiguredLocalPort,
                    out _);
                return;
            }

            if (_fieldMessageBoxOfficialSessionBridgeConfiguredRemotePort <= 0
                || _fieldMessageBoxOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue
                || string.IsNullOrWhiteSpace(_fieldMessageBoxOfficialSessionBridgeConfiguredRemoteHost))
            {
                _fieldMessageBoxOfficialSessionBridge.Stop();
                return;
            }

            if (_fieldMessageBoxOfficialSessionBridge.IsRunning
                && _fieldMessageBoxOfficialSessionBridge.ListenPort == _fieldMessageBoxOfficialSessionBridgeConfiguredListenPort
                && string.Equals(_fieldMessageBoxOfficialSessionBridge.RemoteHost, _fieldMessageBoxOfficialSessionBridgeConfiguredRemoteHost, StringComparison.OrdinalIgnoreCase)
                && _fieldMessageBoxOfficialSessionBridge.RemotePort == _fieldMessageBoxOfficialSessionBridgeConfiguredRemotePort)
            {
                return;
            }

            _fieldMessageBoxOfficialSessionBridge.Start(
                _fieldMessageBoxOfficialSessionBridgeConfiguredListenPort,
                _fieldMessageBoxOfficialSessionBridgeConfiguredRemoteHost,
                _fieldMessageBoxOfficialSessionBridgeConfiguredRemotePort);
        }

        private void RefreshFieldMessageBoxOfficialSessionBridgeDiscovery(int currentTickCount)
        {
            if (!_fieldMessageBoxOfficialSessionBridgeEnabled
                || !_fieldMessageBoxOfficialSessionBridgeUseDiscovery
                || _fieldMessageBoxOfficialSessionBridgeConfiguredRemotePort <= 0
                || _fieldMessageBoxOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue
                || _fieldMessageBoxOfficialSessionBridge.HasConnectedSession
                || currentTickCount < _nextFieldMessageBoxOfficialSessionBridgeDiscoveryRefreshAt)
            {
                return;
            }

            _nextFieldMessageBoxOfficialSessionBridgeDiscoveryRefreshAt =
                currentTickCount + FieldMessageBoxOfficialSessionBridgeDiscoveryRefreshIntervalMs;
            _fieldMessageBoxOfficialSessionBridge.TryStartFromDiscovery(
                _fieldMessageBoxOfficialSessionBridgeConfiguredListenPort,
                _fieldMessageBoxOfficialSessionBridgeConfiguredRemotePort,
                _fieldMessageBoxOfficialSessionBridgeConfiguredProcessSelector,
                _fieldMessageBoxOfficialSessionBridgeConfiguredLocalPort,
                out _);
        }

        private void DrainFieldMessageBoxOfficialSessionBridge()
        {
            while (_fieldMessageBoxOfficialSessionBridge.TryDequeue(out FieldMessageBoxPacketInboxMessage packet))
            {
                bool applied = TryApplyFieldMessageBoxPacket(packet.Opcode, packet.Payload, out string message);
                _fieldMessageBoxOfficialSessionBridge.RecordDispatchResult(packet.Source, applied, message);
                if (!string.IsNullOrWhiteSpace(message))
                {
                    _chat?.AddMessage(
                        $"Message-box session {packet.Opcode}: {message}",
                        applied ? Microsoft.Xna.Framework.Color.LightGreen : Microsoft.Xna.Framework.Color.OrangeRed,
                        currTickCount);
                }
            }
        }

        private ChatCommandHandler.CommandResult HandleFieldMessageBoxSessionCommand(string[] args)
        {
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribeFieldMessageBoxOfficialSessionBridgeStatus());
            }

            if (string.Equals(args[0], "history", StringComparison.OrdinalIgnoreCase))
            {
                int historyCount = 10;
                if (args.Length >= 2 && (!int.TryParse(args[1], out historyCount) || historyCount <= 0))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /messagebox session history [count]");
                }

                return ChatCommandHandler.CommandResult.Info(_fieldMessageBoxOfficialSessionBridge.DescribeRecentOutboundPackets(historyCount));
            }

            if (string.Equals(args[0], "clearhistory", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Ok(_fieldMessageBoxOfficialSessionBridge.ClearRecentOutboundPackets());
            }

            if (string.Equals(args[0], "replay", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2 || !int.TryParse(args[1], out int replayIndex) || replayIndex <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /messagebox session replay <historyIndex>");
                }

                return _fieldMessageBoxOfficialSessionBridge.TryReplayRecentOutboundPacket(replayIndex, out string replayStatus)
                    ? ChatCommandHandler.CommandResult.Ok(replayStatus)
                    : ChatCommandHandler.CommandResult.Error(replayStatus);
            }

            if (string.Equals(args[0], "syncutility", StringComparison.OrdinalIgnoreCase))
            {
                return TrySyncFieldMessageBoxOfficialSessionBridgeFromLocalUtility(out string syncStatus)
                    ? ChatCommandHandler.CommandResult.Ok(syncStatus)
                    : ChatCommandHandler.CommandResult.Error(syncStatus);
            }

            if (string.Equals(args[0], "send", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "queue", StringComparison.OrdinalIgnoreCase))
            {
                bool queueOnly = string.Equals(args[0], "queue", StringComparison.OrdinalIgnoreCase);
                if (args.Length >= 2 && string.Equals(args[1], "consume", StringComparison.OrdinalIgnoreCase))
                {
                    if (args.Length < 4
                        || !int.TryParse(args[2], out int inventoryPosition)
                        || inventoryPosition <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /messagebox session <send|queue> consume <inventoryPosition> [itemId] <message>");
                    }

                    int itemId = DefaultMessageBoxConsumeRequestItemId;
                    int messageTokenStart = 3;
                    if (args.Length >= 5 && int.TryParse(args[3], out int parsedItemId) && parsedItemId > 0)
                    {
                        itemId = parsedItemId;
                        messageTokenStart = 4;
                    }

                    if (args.Length <= messageTokenStart)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /messagebox session <send|queue> consume <inventoryPosition> [itemId] <message>");
                    }

                    string messageText = string.Join(" ", args.Skip(messageTokenStart));
                    return TryMirrorFieldMessageBoxConsumeCashItemUseClientRequest(
                            inventoryPosition,
                            itemId,
                            messageText,
                            queueOnly,
                            out string consumeStatus)
                        ? ChatCommandHandler.CommandResult.Ok(consumeStatus)
                        : ChatCommandHandler.CommandResult.Error(consumeStatus);
                }
            }

            if (string.Equals(args[0], "sendraw", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(1)), out byte[] rawPacket))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /messagebox session sendraw <opcode-prefixed-hex>");
                }

                return _fieldMessageBoxOfficialSessionBridge.TrySendOutboundRawPacket(rawPacket, out string sendStatus)
                    ? ChatCommandHandler.CommandResult.Ok(sendStatus)
                    : ChatCommandHandler.CommandResult.Error(sendStatus);
            }

            if (string.Equals(args[0], "send", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2
                    || !int.TryParse(args[1], out int opcode)
                    || opcode < 0
                    || opcode > ushort.MaxValue)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /messagebox session send <opcode> [payloadhex=..|payloadb64=..]");
                }

                byte[] payload = Array.Empty<byte>();
                if (args.Length >= 3 && !TryParseBinaryPayloadArgument(args[2], out payload, out string payloadError))
                {
                    return ChatCommandHandler.CommandResult.Error(payloadError ?? "Usage: /messagebox session send <opcode> [payloadhex=..|payloadb64=..]");
                }

                using PacketWriter writer = new();
                writer.Write((ushort)opcode);
                writer.WriteBytes(payload);
                byte[] rawPacket = writer.ToArray();
                return _fieldMessageBoxOfficialSessionBridge.TrySendOutboundRawPacket(rawPacket, out string sendStatus)
                    ? ChatCommandHandler.CommandResult.Ok(sendStatus)
                    : ChatCommandHandler.CommandResult.Error(sendStatus);
            }

            if (string.Equals(args[0], "discover", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2 || !int.TryParse(args[1], out int discoverRemotePort) || discoverRemotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /messagebox session discover <remotePort> [processName|pid] [localPort]");
                }

                string processSelector = args.Length >= 3 ? args[2] : null;
                int? localPortFilter = null;
                if (args.Length >= 4)
                {
                    if (!int.TryParse(args[3], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /messagebox session discover <remotePort> [processName|pid] [localPort]");
                    }

                    localPortFilter = parsedLocalPort;
                }

                return ChatCommandHandler.CommandResult.Info(
                    _fieldMessageBoxOfficialSessionBridge.DescribeDiscoveredSessions(discoverRemotePort, processSelector, localPortFilter));
            }

            if (string.Equals(args[0], "start", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 4
                    || !int.TryParse(args[1], out int listenPort)
                    || listenPort <= 0
                    || !int.TryParse(args[3], out int remotePort)
                    || remotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /messagebox session start <listenPort> <serverHost> <serverPort>");
                }

                _fieldMessageBoxOfficialSessionBridgeEnabled = true;
                _fieldMessageBoxOfficialSessionBridgeUseDiscovery = false;
                _fieldMessageBoxOfficialSessionBridgeConfiguredListenPort = listenPort;
                _fieldMessageBoxOfficialSessionBridgeConfiguredRemoteHost = args[2];
                _fieldMessageBoxOfficialSessionBridgeConfiguredRemotePort = remotePort;
                _fieldMessageBoxOfficialSessionBridgeConfiguredProcessSelector = null;
                _fieldMessageBoxOfficialSessionBridgeConfiguredLocalPort = null;
                EnsureFieldMessageBoxOfficialSessionBridgeState(shouldRun: true);
                return ChatCommandHandler.CommandResult.Ok(DescribeFieldMessageBoxOfficialSessionBridgeStatus());
            }

            if (string.Equals(args[0], "startauto", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3
                    || !int.TryParse(args[1], out int autoListenPort)
                    || autoListenPort <= 0
                    || !int.TryParse(args[2], out int autoRemotePort)
                    || autoRemotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /messagebox session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                }

                string processSelector = args.Length >= 4 ? args[3] : null;
                int? localPortFilter = null;
                if (args.Length >= 5)
                {
                    if (!int.TryParse(args[4], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /messagebox session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                    }

                    localPortFilter = parsedLocalPort;
                }

                _fieldMessageBoxOfficialSessionBridgeEnabled = true;
                _fieldMessageBoxOfficialSessionBridgeUseDiscovery = true;
                _fieldMessageBoxOfficialSessionBridgeConfiguredListenPort = autoListenPort;
                _fieldMessageBoxOfficialSessionBridgeConfiguredRemotePort = autoRemotePort;
                _fieldMessageBoxOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
                _fieldMessageBoxOfficialSessionBridgeConfiguredProcessSelector = processSelector;
                _fieldMessageBoxOfficialSessionBridgeConfiguredLocalPort = localPortFilter;
                _nextFieldMessageBoxOfficialSessionBridgeDiscoveryRefreshAt = 0;

                return _fieldMessageBoxOfficialSessionBridge.TryStartFromDiscovery(
                        autoListenPort,
                        autoRemotePort,
                        processSelector,
                        localPortFilter,
                        out string startStatus)
                    ? ChatCommandHandler.CommandResult.Ok($"{startStatus} {DescribeFieldMessageBoxOfficialSessionBridgeStatus()}")
                    : ChatCommandHandler.CommandResult.Error(startStatus);
            }

            if (string.Equals(args[0], "stop", StringComparison.OrdinalIgnoreCase))
            {
                _fieldMessageBoxOfficialSessionBridgeEnabled = false;
                _fieldMessageBoxOfficialSessionBridgeUseDiscovery = false;
                _fieldMessageBoxOfficialSessionBridgeConfiguredRemotePort = 0;
                _fieldMessageBoxOfficialSessionBridgeConfiguredProcessSelector = null;
                _fieldMessageBoxOfficialSessionBridgeConfiguredLocalPort = null;
                _fieldMessageBoxOfficialSessionBridge.Stop();
                return ChatCommandHandler.CommandResult.Ok(DescribeFieldMessageBoxOfficialSessionBridgeStatus());
            }

            return ChatCommandHandler.CommandResult.Error("Usage: /messagebox session [status|history [count]|clearhistory|replay <historyIndex>|syncutility|send <opcode> [payloadhex=..|payloadb64=..]|sendraw <opcode-prefixed-hex>|<send|queue> consume <inventoryPosition> [itemId] <message>|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]");
        }

        private bool TryMirrorFieldMessageBoxConsumeCashItemUseClientRequest(
            int inventoryPosition,
            int itemId,
            string messageText,
            bool queueOnly,
            out string message)
        {
            bool registeredPendingRequest = false;
            string pendingRequestNote = string.Empty;
            string bridgeSyncNote = string.Empty;
            if (!_fieldMessageBoxOfficialSessionBridgeEnabled)
            {
                bridgeSyncNote = TrySyncFieldMessageBoxOfficialSessionBridgeFromLocalUtility(out string bridgeSyncStatus)
                    ? $" Message-box session bridge auto-sync succeeded. {bridgeSyncStatus}"
                    : $" Message-box session bridge auto-sync unavailable. {bridgeSyncStatus}";
            }

            if (!FieldMessageBoxRuntime.TryBuildConsumeCashItemUseRequestPayload(
                    currTickCount,
                    inventoryPosition,
                    itemId,
                    messageText,
                    out byte[] payload,
                    out string payloadError))
            {
                message = payloadError;
                return false;
            }

            if (TryResolveFieldMessageBoxRequestHostPosition(out Point hostPosition, out string hostStatus))
            {
                _fieldMessageBoxRuntime.Initialize(GraphicsDevice);
                registeredPendingRequest = _fieldMessageBoxRuntime.TryRegisterPendingConsumeCashItemUseRequest(
                    payload,
                    hostPosition,
                    currTickCount,
                    out string pendingStatus);
                pendingRequestNote = registeredPendingRequest
                    ? $" {pendingStatus}"
                    : $" Pending request registration failed: {pendingStatus}";
            }
            else
            {
                pendingRequestNote = $" Pending request registration skipped: {hostStatus}";
            }

            string payloadHex = Convert.ToHexString(payload);
            if (queueOnly)
            {
                if (_localUtilityOfficialSessionBridge.TryQueueOutboundPacket(
                        FieldMessageBoxRuntime.ConsumeCashItemUseRequestOpcode,
                        payload,
                        out string queueStatus))
                {
                    message = $"Queued CUserLocal::ConsumeCashItem message-box request opcode {FieldMessageBoxRuntime.ConsumeCashItemUseRequestOpcode} [{payloadHex}] for deferred live delivery. {queueStatus}{pendingRequestNote}{bridgeSyncNote}";
                    return true;
                }

                if (_localUtilityPacketOutbox.TryQueueOutboundPacket(
                        FieldMessageBoxRuntime.ConsumeCashItemUseRequestOpcode,
                        payload,
                        out string outboxQueueStatus))
                {
                    message = $"Queued CUserLocal::ConsumeCashItem message-box request opcode {FieldMessageBoxRuntime.ConsumeCashItemUseRequestOpcode} [{payloadHex}] for deferred generic local-utility outbox delivery after live queueing was unavailable. Bridge queue: {queueStatus} Deferred outbox: {outboxQueueStatus}{pendingRequestNote}{bridgeSyncNote}";
                    return true;
                }

                message = $"Message-box consume request queueing failed for opcode {FieldMessageBoxRuntime.ConsumeCashItemUseRequestOpcode} [{payloadHex}]. Bridge queue: {queueStatus} Outbox queue: {outboxQueueStatus}{pendingRequestNote}{bridgeSyncNote}";
                return false;
            }

            string dispatchStatus = "live local-utility bridge unavailable";
            if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(
                    FieldMessageBoxRuntime.ConsumeCashItemUseRequestOpcode,
                    payload,
                    out dispatchStatus))
            {
                message = $"Dispatched CUserLocal::ConsumeCashItem message-box request opcode {FieldMessageBoxRuntime.ConsumeCashItemUseRequestOpcode} [{payloadHex}] through the live local-utility bridge. {dispatchStatus}{pendingRequestNote}{bridgeSyncNote}";
                return true;
            }

            string outboxStatus = "generic local-utility outbox unavailable";
            if (_localUtilityPacketOutbox.TrySendOutboundPacket(
                    FieldMessageBoxRuntime.ConsumeCashItemUseRequestOpcode,
                    payload,
                    out outboxStatus))
            {
                message = $"Dispatched CUserLocal::ConsumeCashItem message-box request opcode {FieldMessageBoxRuntime.ConsumeCashItemUseRequestOpcode} [{payloadHex}] through the generic local-utility outbox after the live bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus}{pendingRequestNote}{bridgeSyncNote}";
                return true;
            }

            string deferredBridgeStatus = "deferred official-session bridge queue unavailable";
            if (_localUtilityOfficialSessionBridge.IsRunning
                && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(
                    FieldMessageBoxRuntime.ConsumeCashItemUseRequestOpcode,
                    payload,
                    out deferredBridgeStatus))
            {
                message = $"Queued CUserLocal::ConsumeCashItem message-box request opcode {FieldMessageBoxRuntime.ConsumeCashItemUseRequestOpcode} [{payloadHex}] for deferred official-session bridge delivery after immediate dispatch was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred bridge: {deferredBridgeStatus}{pendingRequestNote}{bridgeSyncNote}";
                return true;
            }

            if (_localUtilityPacketOutbox.TryQueueOutboundPacket(
                    FieldMessageBoxRuntime.ConsumeCashItemUseRequestOpcode,
                    payload,
                    out string deferredOutboxStatus))
            {
                message = $"Queued CUserLocal::ConsumeCashItem message-box request opcode {FieldMessageBoxRuntime.ConsumeCashItemUseRequestOpcode} [{payloadHex}] for deferred generic local-utility outbox delivery after immediate dispatch was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred bridge: {deferredBridgeStatus} Deferred outbox: {deferredOutboxStatus}{pendingRequestNote}{bridgeSyncNote}";
                return true;
            }

            if (registeredPendingRequest)
            {
                message = $"Staged CUserLocal::ConsumeCashItem message-box request opcode {FieldMessageBoxRuntime.ConsumeCashItemUseRequestOpcode} [{payloadHex}] locally while waiting for packet 326/325 completion because neither the live bridge nor the generic outbox nor either deferred queue accepted it. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred bridge: {deferredBridgeStatus} Deferred outbox: {deferredOutboxStatus}{pendingRequestNote}{bridgeSyncNote}";
                return true;
            }

            message = $"Message-box consume request opcode {FieldMessageBoxRuntime.ConsumeCashItemUseRequestOpcode} [{payloadHex}] could not be staged because neither the live bridge nor the generic outbox nor either deferred queue accepted it. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred bridge: {deferredBridgeStatus} Deferred outbox: {deferredOutboxStatus}{pendingRequestNote}{bridgeSyncNote}";
            return false;
        }

        private bool TryResolveFieldMessageBoxRequestHostPosition(out Point hostPosition, out string status)
        {
            PlayerCharacter player = _playerManager?.Player;
            if (player == null)
            {
                hostPosition = Point.Zero;
                status = "a loaded local player host is required.";
                return false;
            }

            hostPosition = new Point((int)Math.Round(player.X), (int)Math.Round(player.Y));
            status = "resolved local player host position.";
            return true;
        }
    }
}

