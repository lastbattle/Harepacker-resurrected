using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Interaction;
using System;
using System.Net;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int FamilyOfficialSessionBridgeDefaultListenPort = 18506;
        private const ushort FamilyOfficialSessionBridgeDefaultChartOpcode = 98;
        // IDA evidence: CWvsContext::SendUseFamilyPrivilege (0xA09EE0) builds COutPacket(175).
        private const ushort FamilyUsePrivilegeRequestOpcode = 175;
        private const int FamilyOfficialSessionBridgeDiscoveryRefreshIntervalMs = 2000;

        private readonly MessengerOfficialSessionBridgeManager _familyOfficialSessionBridge =
            new("Family", FamilyOfficialSessionBridgeDefaultListenPort, FamilyOfficialSessionBridgeDefaultChartOpcode, 0, 99, 100, 104, 107);

        private bool _familyOfficialSessionBridgeEnabled;
        private bool _familyOfficialSessionBridgeUseDiscovery;
        private int _familyOfficialSessionBridgeConfiguredListenPort = FamilyOfficialSessionBridgeDefaultListenPort;
        private string _familyOfficialSessionBridgeConfiguredRemoteHost = "127.0.0.1";
        private int _familyOfficialSessionBridgeConfiguredRemotePort;
        private ushort _familyOfficialSessionBridgeConfiguredInboundOpcode = FamilyOfficialSessionBridgeDefaultChartOpcode;
        private string _familyOfficialSessionBridgeConfiguredProcessSelector;
        private int? _familyOfficialSessionBridgeConfiguredLocalPort;
        private int _nextFamilyOfficialSessionBridgeDiscoveryRefreshAt;

        private string DescribeFamilyOfficialSessionBridgeStatus()
        {
            string enabledText = _familyOfficialSessionBridgeEnabled ? "enabled" : "disabled";
            string modeText = _familyOfficialSessionBridgeUseDiscovery ? "auto-discovery" : "direct proxy";
            string configuredTarget = _familyOfficialSessionBridgeUseDiscovery
                ? _familyOfficialSessionBridgeConfiguredLocalPort.HasValue
                    ? $"discover remote port {_familyOfficialSessionBridgeConfiguredRemotePort} with local port {_familyOfficialSessionBridgeConfiguredLocalPort.Value}"
                    : $"discover remote port {_familyOfficialSessionBridgeConfiguredRemotePort}"
                : $"{_familyOfficialSessionBridgeConfiguredRemoteHost}:{_familyOfficialSessionBridgeConfiguredRemotePort}";
            string processText = string.IsNullOrWhiteSpace(_familyOfficialSessionBridgeConfiguredProcessSelector)
                ? string.Empty
                : $" for {_familyOfficialSessionBridgeConfiguredProcessSelector}";
            string listeningText = _familyOfficialSessionBridge.IsRunning
                ? $"listening on 127.0.0.1:{_familyOfficialSessionBridge.ListenPort}"
                : $"configured for 127.0.0.1:{_familyOfficialSessionBridgeConfiguredListenPort}";
            return $"Family session bridge {enabledText}, {modeText}, {listeningText}, target {configuredTarget}{processText}, primary inbound opcode {_familyOfficialSessionBridgeConfiguredInboundOpcode}, additional inbound opcodes 99/100/104/107. {_familyOfficialSessionBridge.DescribeStatus()}";
        }

        private void EnsureFamilyOfficialSessionBridgeState(bool shouldRun)
        {
            if (!shouldRun || !_familyOfficialSessionBridgeEnabled)
            {
                if (_familyOfficialSessionBridge.IsRunning)
                {
                    _familyOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_familyOfficialSessionBridgeConfiguredListenPort <= 0
                || _familyOfficialSessionBridgeConfiguredListenPort > ushort.MaxValue
                || _familyOfficialSessionBridgeConfiguredInboundOpcode == 0)
            {
                if (_familyOfficialSessionBridge.IsRunning)
                {
                    _familyOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_familyOfficialSessionBridgeUseDiscovery)
            {
                if (_familyOfficialSessionBridgeConfiguredRemotePort <= 0
                    || _familyOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue)
                {
                    if (_familyOfficialSessionBridge.IsRunning)
                    {
                        _familyOfficialSessionBridge.Stop();
                    }

                    return;
                }

                _familyOfficialSessionBridge.TryRefreshFromDiscovery(
                    _familyOfficialSessionBridgeConfiguredListenPort,
                    _familyOfficialSessionBridgeConfiguredRemotePort,
                    _familyOfficialSessionBridgeConfiguredInboundOpcode,
                    _familyOfficialSessionBridgeConfiguredProcessSelector,
                    _familyOfficialSessionBridgeConfiguredLocalPort,
                    out _);
                return;
            }

            if (_familyOfficialSessionBridgeConfiguredRemotePort <= 0
                || _familyOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue
                || string.IsNullOrWhiteSpace(_familyOfficialSessionBridgeConfiguredRemoteHost))
            {
                if (_familyOfficialSessionBridge.IsRunning)
                {
                    _familyOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_familyOfficialSessionBridge.IsRunning
                && _familyOfficialSessionBridge.ListenPort == _familyOfficialSessionBridgeConfiguredListenPort
                && _familyOfficialSessionBridge.RemotePort == _familyOfficialSessionBridgeConfiguredRemotePort
                && _familyOfficialSessionBridge.MessengerOpcode == _familyOfficialSessionBridgeConfiguredInboundOpcode
                && string.Equals(_familyOfficialSessionBridge.RemoteHost, _familyOfficialSessionBridgeConfiguredRemoteHost, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (_familyOfficialSessionBridge.IsRunning)
            {
                _familyOfficialSessionBridge.Stop();
            }

            _familyOfficialSessionBridge.Start(
                _familyOfficialSessionBridgeConfiguredListenPort,
                _familyOfficialSessionBridgeConfiguredRemoteHost,
                _familyOfficialSessionBridgeConfiguredRemotePort,
                _familyOfficialSessionBridgeConfiguredInboundOpcode);
        }

        private void RefreshFamilyOfficialSessionBridgeDiscovery(int currentTickCount)
        {
            if (!_familyOfficialSessionBridgeEnabled
                || !_familyOfficialSessionBridgeUseDiscovery
                || _familyOfficialSessionBridgeConfiguredRemotePort <= 0
                || _familyOfficialSessionBridgeConfiguredInboundOpcode == 0
                || _familyOfficialSessionBridge.HasAttachedClient
                || currentTickCount < _nextFamilyOfficialSessionBridgeDiscoveryRefreshAt)
            {
                return;
            }

            _nextFamilyOfficialSessionBridgeDiscoveryRefreshAt =
                currentTickCount + FamilyOfficialSessionBridgeDiscoveryRefreshIntervalMs;
            _familyOfficialSessionBridge.TryRefreshFromDiscovery(
                _familyOfficialSessionBridgeConfiguredListenPort,
                _familyOfficialSessionBridgeConfiguredRemotePort,
                _familyOfficialSessionBridgeConfiguredInboundOpcode,
                _familyOfficialSessionBridgeConfiguredProcessSelector,
                _familyOfficialSessionBridgeConfiguredLocalPort,
                out _);
        }

        private void DrainFamilyOfficialSessionBridge()
        {
            while (_familyOfficialSessionBridge.TryDequeue(out MessengerOfficialSessionBridgeMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                bool applied = _familyChartRuntime.TryApplyClientPacketPayload(message.Opcode, message.Payload, out string detail);
                if (applied)
                {
                    detail = AppendFamilyPrivilegeTransferCompletion(detail);
                }

                _familyOfficialSessionBridge.RecordDispatchResult(message.Source, applied, $"CWvsContext::OnPacket family opcode {message.Opcode}: {detail}");
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

        private string HandleFamilyEntitlementUseResult(FamilyEntitlementUseResult result)
        {
            string detail = result.Message ?? string.Empty;
            if (result.DispatchClientPrivilegeRequest && result.PrivilegeRequestIndex >= 0)
            {
                if (TryMirrorFamilyUsePrivilegeClientRequest(
                        result.PrivilegeRequestIndex,
                        result.IncludePrivilegeTargetName,
                        result.PrivilegeTargetName,
                        out string dispatchStatus))
                {
                    detail = string.IsNullOrWhiteSpace(detail)
                        ? dispatchStatus
                        : $"{detail} {dispatchStatus}";
                }
                else
                {
                    detail = string.IsNullOrWhiteSpace(detail)
                        ? dispatchStatus
                        : $"{detail} {dispatchStatus}";
                }
            }

            if (result.RequestTeleport)
            {
                _playerManager?.TeleportTo(result.TeleportPosition.X, result.TeleportPosition.Y);
            }

            return AppendFamilyPrivilegeTransferCompletion(detail);
        }

        private string AppendFamilyPrivilegeTransferCompletion(string detail)
        {
            if (!_familyChartRuntime.TryConsumeQueuedPrivilegeTeleport(out var teleportPosition, out string teleportMessage))
            {
                return detail ?? string.Empty;
            }

            _playerManager?.TeleportTo(teleportPosition.X, teleportPosition.Y);
            if (string.IsNullOrWhiteSpace(teleportMessage))
            {
                teleportMessage = "Completed packet-owned family privilege transfer.";
            }

            if (string.IsNullOrWhiteSpace(detail))
            {
                return teleportMessage;
            }

            return $"{detail} {teleportMessage}";
        }

        private bool TryMirrorFamilyUsePrivilegeClientRequest(
            int privilegeIndex,
            bool includeTargetName,
            string targetName,
            out string status,
            bool queueOnly = false)
        {
            if (privilegeIndex < 0)
            {
                status = "Family privilege request index must be non-negative.";
                return false;
            }

            byte[] payload = FamilyPacketCodec.BuildUsePrivilegeRequestPayload(
                privilegeIndex,
                includeTargetName,
                targetName);
            string payloadHex = Convert.ToHexString(payload);
            if (queueOnly)
            {
                if (_familyOfficialSessionBridge.TryQueueOutboundPacket(FamilyUsePrivilegeRequestOpcode, payload, out string queueStatus))
                {
                    status = $"Mirrored CWvsContext::SendUseFamilyPrivilege index {privilegeIndex} as queued opcode {FamilyUsePrivilegeRequestOpcode} [{payloadHex}]. {queueStatus}";
                    return true;
                }

                status = $"Failed to queue CWvsContext::SendUseFamilyPrivilege index {privilegeIndex} as opcode {FamilyUsePrivilegeRequestOpcode} [{payloadHex}]. {queueStatus}";
                return false;
            }

            if (_familyOfficialSessionBridge.TrySendOutboundPacket(FamilyUsePrivilegeRequestOpcode, payload, out string sendStatus))
            {
                status = $"Mirrored CWvsContext::SendUseFamilyPrivilege index {privilegeIndex} as opcode {FamilyUsePrivilegeRequestOpcode} [{payloadHex}] through the live family session bridge. {sendStatus}";
                return true;
            }

            if (_familyOfficialSessionBridge.TryQueueOutboundPacket(FamilyUsePrivilegeRequestOpcode, payload, out string deferredQueueStatus))
            {
                status = $"Queued CWvsContext::SendUseFamilyPrivilege index {privilegeIndex} as opcode {FamilyUsePrivilegeRequestOpcode} [{payloadHex}] after immediate live injection was unavailable. Bridge: {sendStatus} Deferred queue: {deferredQueueStatus}";
                return true;
            }

            status = $"Failed to mirror CWvsContext::SendUseFamilyPrivilege index {privilegeIndex} as opcode {FamilyUsePrivilegeRequestOpcode} [{payloadHex}]. Bridge: {sendStatus} Queue: {deferredQueueStatus}";
            return false;
        }

        private ChatCommandHandler.CommandResult HandleFamilySessionCommand(string[] args)
        {
            const string usage = "Usage: /family session [status|discover <remotePort> [processName|pid] [localPort]|history [count]|clearhistory|replay <historyIndex>|send <privilegeIndex> [targetName]|queue <privilegeIndex> [targetName]|sendraw <hex>|sendpacketraw <opcode-framed-hex>|start <listenPort> <serverHost> <serverPort> [chartOpcode]|startauto <listenPort> <remotePort> [chartOpcode] [processName|pid] [localPort]|stop]";
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribeFamilyOfficialSessionBridgeStatus());
            }

            if (string.Equals(args[0], "discover", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2
                    || !int.TryParse(args[1], out int discoverRemotePort)
                    || discoverRemotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /family session discover <remotePort> [processName|pid] [localPort]");
                }

                string processSelector = args.Length >= 3 ? args[2] : null;
                int? localPortFilter = null;
                if (args.Length >= 4)
                {
                    if (!int.TryParse(args[3], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /family session discover <remotePort> [processName|pid] [localPort]");
                    }

                    localPortFilter = parsedLocalPort;
                }

                return ChatCommandHandler.CommandResult.Info(
                    _familyOfficialSessionBridge.DescribeDiscoveredSessions(discoverRemotePort, processSelector, localPortFilter));
            }

            if (string.Equals(args[0], "history", StringComparison.OrdinalIgnoreCase))
            {
                int count = 10;
                if (args.Length >= 2 && (!int.TryParse(args[1], out count) || count <= 0))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /family session history [count]");
                }

                return ChatCommandHandler.CommandResult.Info(_familyOfficialSessionBridge.DescribeRecentOutboundPackets(count));
            }

            if (string.Equals(args[0], "clearhistory", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Ok(_familyOfficialSessionBridge.ClearRecentOutboundPackets());
            }

            if (string.Equals(args[0], "replay", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2 || !int.TryParse(args[1], out int historyIndex) || historyIndex <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /family session replay <historyIndex>");
                }

                return _familyOfficialSessionBridge.TryReplayRecentOutboundPacket(historyIndex, out string replayStatus)
                    ? ChatCommandHandler.CommandResult.Ok(replayStatus)
                    : ChatCommandHandler.CommandResult.Error(replayStatus);
            }

            if (string.Equals(args[0], "send", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "queue", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2 || !int.TryParse(args[1], out int privilegeIndex) || privilegeIndex < 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /family session <send|queue> <privilegeIndex> [targetName]");
                }

                bool queueOnly = string.Equals(args[0], "queue", StringComparison.OrdinalIgnoreCase);
                string targetName = args.Length >= 3
                    ? string.Join(" ", args, 2, args.Length - 2).Trim()
                    : string.Empty;
                bool includeTargetName = !string.IsNullOrWhiteSpace(targetName);
                return TryMirrorFamilyUsePrivilegeClientRequest(
                        privilegeIndex,
                        includeTargetName,
                        targetName,
                        out string sendFamilyStatus,
                        queueOnly)
                    ? ChatCommandHandler.CommandResult.Ok(sendFamilyStatus)
                    : ChatCommandHandler.CommandResult.Error(sendFamilyStatus);
            }

            if (string.Equals(args[0], "sendraw", StringComparison.OrdinalIgnoreCase))
            {
                string parseError = null;
                if (args.Length < 2 || !FamilyPacketCodec.TryParseHexBytes(string.Join(string.Empty, args, 1, args.Length - 1), out byte[] payload, out parseError))
                {
                    return ChatCommandHandler.CommandResult.Error(parseError ?? "Usage: /family session sendraw <hex>");
                }

                return _familyOfficialSessionBridge.TrySendOutboundPacket(
                        FamilyUsePrivilegeRequestOpcode,
                        payload,
                        out string sendRawStatus)
                    ? ChatCommandHandler.CommandResult.Ok(sendRawStatus)
                    : ChatCommandHandler.CommandResult.Error(sendRawStatus);
            }

            if (string.Equals(args[0], "sendpacketraw", StringComparison.OrdinalIgnoreCase))
            {
                string parseError = null;
                if (args.Length < 2 || !FamilyPacketCodec.TryParseHexBytes(string.Join(string.Empty, args, 1, args.Length - 1), out byte[] rawPacket, out parseError))
                {
                    return ChatCommandHandler.CommandResult.Error(parseError ?? "Usage: /family session sendpacketraw <opcode-framed-hex>");
                }

                return _familyOfficialSessionBridge.TrySendOutboundRawPacket(rawPacket, out string sendRawPacketStatus)
                    ? ChatCommandHandler.CommandResult.Ok(sendRawPacketStatus)
                    : ChatCommandHandler.CommandResult.Error(sendRawPacketStatus);
            }

            if (string.Equals(args[0], "start", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 4
                    || !int.TryParse(args[1], out int listenPort)
                    || listenPort <= 0
                    || !int.TryParse(args[3], out int remotePort)
                    || remotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /family session start <listenPort> <serverHost> <serverPort> [chartOpcode]");
                }

                ushort inboundOpcode = FamilyOfficialSessionBridgeDefaultChartOpcode;
                if (args.Length >= 5
                    && (!ushort.TryParse(args[4], out inboundOpcode) || inboundOpcode == 0))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /family session start <listenPort> <serverHost> <serverPort> [chartOpcode]");
                }

                _familyOfficialSessionBridgeEnabled = true;
                _familyOfficialSessionBridgeUseDiscovery = false;
                _familyOfficialSessionBridgeConfiguredListenPort = listenPort;
                _familyOfficialSessionBridgeConfiguredRemoteHost = args[2];
                _familyOfficialSessionBridgeConfiguredRemotePort = remotePort;
                _familyOfficialSessionBridgeConfiguredInboundOpcode = inboundOpcode;
                _familyOfficialSessionBridgeConfiguredProcessSelector = null;
                _familyOfficialSessionBridgeConfiguredLocalPort = null;
                EnsureFamilyOfficialSessionBridgeState(shouldRun: true);
                return ChatCommandHandler.CommandResult.Ok(DescribeFamilyOfficialSessionBridgeStatus());
            }

            if (string.Equals(args[0], "startauto", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3
                    || !int.TryParse(args[1], out int autoListenPort)
                    || autoListenPort <= 0
                    || !int.TryParse(args[2], out int autoRemotePort)
                    || autoRemotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /family session startauto <listenPort> <remotePort> [chartOpcode] [processName|pid] [localPort]");
                }

                int nextArgumentIndex = 3;
                ushort inboundOpcode = FamilyOfficialSessionBridgeDefaultChartOpcode;
                if (args.Length > nextArgumentIndex
                    && ushort.TryParse(args[nextArgumentIndex], out ushort parsedOpcode)
                    && parsedOpcode > 0)
                {
                    inboundOpcode = parsedOpcode;
                    nextArgumentIndex++;
                }

                string processSelector = args.Length > nextArgumentIndex ? args[nextArgumentIndex] : null;
                int? localPortFilter = null;
                if (args.Length > nextArgumentIndex + 1)
                {
                    if (!int.TryParse(args[nextArgumentIndex + 1], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /family session startauto <listenPort> <remotePort> [chartOpcode] [processName|pid] [localPort]");
                    }

                    localPortFilter = parsedLocalPort;
                }

                _familyOfficialSessionBridgeEnabled = true;
                _familyOfficialSessionBridgeUseDiscovery = true;
                _familyOfficialSessionBridgeConfiguredListenPort = autoListenPort;
                _familyOfficialSessionBridgeConfiguredRemotePort = autoRemotePort;
                _familyOfficialSessionBridgeConfiguredInboundOpcode = inboundOpcode;
                _familyOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
                _familyOfficialSessionBridgeConfiguredProcessSelector = processSelector;
                _familyOfficialSessionBridgeConfiguredLocalPort = localPortFilter;
                _nextFamilyOfficialSessionBridgeDiscoveryRefreshAt = 0;

                return _familyOfficialSessionBridge.TryRefreshFromDiscovery(
                        autoListenPort,
                        autoRemotePort,
                        inboundOpcode,
                        processSelector,
                        localPortFilter,
                        out string startStatus)
                    ? ChatCommandHandler.CommandResult.Ok($"{startStatus} {DescribeFamilyOfficialSessionBridgeStatus()}")
                    : ChatCommandHandler.CommandResult.Error(startStatus);
            }

            if (string.Equals(args[0], "stop", StringComparison.OrdinalIgnoreCase))
            {
                _familyOfficialSessionBridgeEnabled = false;
                _familyOfficialSessionBridgeUseDiscovery = false;
                _familyOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
                _familyOfficialSessionBridgeConfiguredRemotePort = 0;
                _familyOfficialSessionBridgeConfiguredInboundOpcode = FamilyOfficialSessionBridgeDefaultChartOpcode;
                _familyOfficialSessionBridgeConfiguredProcessSelector = null;
                _familyOfficialSessionBridgeConfiguredLocalPort = null;
                _familyOfficialSessionBridge.Stop();
                return ChatCommandHandler.CommandResult.Ok(DescribeFamilyOfficialSessionBridgeStatus());
            }

            return ChatCommandHandler.CommandResult.Error(usage);
        }
    }
}
