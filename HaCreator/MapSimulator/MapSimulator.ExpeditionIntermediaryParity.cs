using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using MapleLib.Helpers;
using System;
using System.Net;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly ExpeditionIntermediaryPacketInboxManager _expeditionIntermediaryPacketInbox = new();
        private readonly ExpeditionIntermediaryOfficialSessionBridgeManager _expeditionIntermediaryOfficialSessionBridge = new();
        private bool _expeditionIntermediaryPacketInboxEnabled = EnablePacketConnectionsByDefault;
        private int _expeditionIntermediaryPacketInboxConfiguredPort = ExpeditionIntermediaryPacketInboxManager.DefaultPort;
        private bool _expeditionIntermediaryOfficialSessionBridgeEnabled;
        private bool _expeditionIntermediaryOfficialSessionBridgeUseDiscovery;
        private int _expeditionIntermediaryOfficialSessionBridgeConfiguredListenPort = ExpeditionIntermediaryOfficialSessionBridgeManager.DefaultListenPort;
        private string _expeditionIntermediaryOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
        private int _expeditionIntermediaryOfficialSessionBridgeConfiguredRemotePort;
        private string _expeditionIntermediaryOfficialSessionBridgeConfiguredProcessSelector;
        private int? _expeditionIntermediaryOfficialSessionBridgeConfiguredLocalPort;
        private ushort _expeditionIntermediaryOfficialSessionBridgeConfiguredOpcode;
        private const int ExpeditionIntermediaryOfficialSessionBridgeDiscoveryRefreshIntervalMs = 2000;
        private int _nextExpeditionIntermediaryOfficialSessionBridgeDiscoveryRefreshAt;

        private bool TryApplyExpeditionIntermediaryPayload(byte[] payload, bool packetOwned, out string detail)
        {
            _socialListRuntime.UpdateLocalContext(_playerManager?.Player?.Build, GetCurrentMapTransferDisplayName(), 1);
            if (!ExpeditionIntermediaryPacketCodec.TryDecodeResultPayload(
                    payload,
                    _playerManager?.Player?.Build?.Name,
                    GetCurrentMapTransferDisplayName(),
                    Math.Max(1, _simulatorChannelIndex + 1),
                    out ExpeditionIntermediaryPacket packet,
                    out string error))
            {
                detail = error ?? "Expedition intermediary payload could not be decoded.";
                return false;
            }

            detail = packet.Kind switch
            {
                ExpeditionIntermediaryPacketKind.Get => _socialListRuntime.ApplyExpeditionGet(packet.ExpeditionTitle, packet.MasterPartyIndex, packet.Parties, packetOwned, packet.RetCode),
                ExpeditionIntermediaryPacketKind.Modified => _socialListRuntime.ApplyExpeditionModified(packet.PartyIndex, packet.Members, packet.MasterPartyIndex, packetOwned, packet.RetCode),
                ExpeditionIntermediaryPacketKind.Invite => _socialListRuntime.ApplyExpeditionInvite(packet.CharacterName, packet.Level, packet.JobCode, packet.PartyQuestId, packetOwned, packet.RetCode),
                ExpeditionIntermediaryPacketKind.ResponseInvite => _socialListRuntime.ApplyExpeditionResponseInvite(packet.CharacterName, packet.ResponseCode, packetOwned, packet.RetCode),
                ExpeditionIntermediaryPacketKind.Notice => _socialListRuntime.ApplyExpeditionNotice(packet.NoticeKind, packet.CharacterName, packetOwned, packet.RetCode),
                ExpeditionIntermediaryPacketKind.MasterChanged => _socialListRuntime.ApplyExpeditionMasterChanged(packet.MasterPartyIndex, packetOwned, packet.RetCode),
                ExpeditionIntermediaryPacketKind.Removed => _socialListRuntime.ApplyExpeditionRemoved(packet.RemovalKind, packetOwned, packet.RetCode),
                _ => packet.Detail
            };
            return true;
        }

        private bool TryApplyExpeditionIntermediaryRawPacket(byte[] rawPacket, bool packetOwned, ushort expectedOpcode, out string detail)
        {
            if (!ExpeditionIntermediaryPacketInboxManager.TryDecodeOpcodeFramedPacket(rawPacket, out int opcode, out byte[] payload, out string error))
            {
                detail = error ?? "Expedition intermediary raw packet could not be decoded.";
                return false;
            }

            if (expectedOpcode > 0 && opcode != expectedOpcode)
            {
                detail = $"Expedition intermediary raw packet opcode {opcode} did not match expected opcode {expectedOpcode}.";
                return false;
            }

            return TryApplyExpeditionIntermediaryPayload(payload, packetOwned, out detail);
        }

        private void EnsureExpeditionIntermediaryPacketInboxState(bool shouldRun)
        {
            if (!shouldRun || !_expeditionIntermediaryPacketInboxEnabled)
            {
                if (_expeditionIntermediaryPacketInbox.IsRunning)
                {
                    _expeditionIntermediaryPacketInbox.Stop();
                }

                return;
            }

            if (_expeditionIntermediaryPacketInbox.IsRunning && _expeditionIntermediaryPacketInbox.Port == _expeditionIntermediaryPacketInboxConfiguredPort)
            {
                return;
            }

            if (_expeditionIntermediaryPacketInbox.IsRunning)
            {
                _expeditionIntermediaryPacketInbox.Stop();
            }

            try
            {
                _expeditionIntermediaryPacketInbox.Start(_expeditionIntermediaryPacketInboxConfiguredPort);
            }
            catch (Exception ex)
            {
                _expeditionIntermediaryPacketInbox.Stop();
                _chat?.AddErrorMessage($"Expedition intermediary packet inbox failed to start: {ex.Message}", currTickCount);
            }
        }

        private void DrainExpeditionIntermediaryPacketInbox()
        {
            while (_expeditionIntermediaryPacketInbox.TryDequeue(out ExpeditionIntermediaryPacketInboxMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                bool applied = TryApplyExpeditionIntermediaryPayload(message.Payload, packetOwned: true, out string detail);
                _expeditionIntermediaryPacketInbox.RecordDispatchResult(message, applied, detail);
                if (!string.IsNullOrWhiteSpace(detail))
                {
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
        }

        private void DrainExpeditionIntermediaryOfficialSessionBridge()
        {
            while (_expeditionIntermediaryOfficialSessionBridge.TryDequeue(out ExpeditionIntermediaryPacketInboxMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                bool applied = TryApplyExpeditionIntermediaryPayload(message.Payload, packetOwned: true, out string detail);
                _expeditionIntermediaryOfficialSessionBridge.RecordDispatchResult(message.Source, applied, detail);
                if (!string.IsNullOrWhiteSpace(detail))
                {
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
        }

        private string DescribeExpeditionIntermediaryPacketInboxStatus()
        {
            string enabledText = _expeditionIntermediaryPacketInboxEnabled ? "enabled" : "disabled";
            string listeningText = _expeditionIntermediaryPacketInbox.IsRunning
                ? $"listening on 127.0.0.1:{_expeditionIntermediaryPacketInbox.Port}"
                : $"configured for 127.0.0.1:{_expeditionIntermediaryPacketInboxConfiguredPort}";
            return $"Expedition intermediary packet inbox {enabledText}, {listeningText}, received {_expeditionIntermediaryPacketInbox.ReceivedCount} packet(s). {_expeditionIntermediaryPacketInbox.LastStatus}";
        }

        private string DescribeExpeditionIntermediaryOfficialSessionBridgeStatus()
        {
            string enabledText = _expeditionIntermediaryOfficialSessionBridgeEnabled ? "enabled" : "disabled";
            string modeText = _expeditionIntermediaryOfficialSessionBridgeUseDiscovery ? "auto-discovery" : "direct proxy";
            string opcodeText = _expeditionIntermediaryOfficialSessionBridgeConfiguredOpcode > 0
                ? _expeditionIntermediaryOfficialSessionBridgeConfiguredOpcode.ToString()
                : "unset";
            string configuredTarget = _expeditionIntermediaryOfficialSessionBridgeUseDiscovery
                ? _expeditionIntermediaryOfficialSessionBridgeConfiguredLocalPort.HasValue
                    ? $"discover remote port {_expeditionIntermediaryOfficialSessionBridgeConfiguredRemotePort} with local port {_expeditionIntermediaryOfficialSessionBridgeConfiguredLocalPort.Value}"
                    : $"discover remote port {_expeditionIntermediaryOfficialSessionBridgeConfiguredRemotePort}"
                : $"{_expeditionIntermediaryOfficialSessionBridgeConfiguredRemoteHost}:{_expeditionIntermediaryOfficialSessionBridgeConfiguredRemotePort}";
            string processText = string.IsNullOrWhiteSpace(_expeditionIntermediaryOfficialSessionBridgeConfiguredProcessSelector)
                ? string.Empty
                : $" for {_expeditionIntermediaryOfficialSessionBridgeConfiguredProcessSelector}";
            string listeningText = _expeditionIntermediaryOfficialSessionBridge.IsRunning
                ? $"listening on 127.0.0.1:{_expeditionIntermediaryOfficialSessionBridge.ListenPort}"
                : $"configured for 127.0.0.1:{_expeditionIntermediaryOfficialSessionBridgeConfiguredListenPort}";
            return $"Expedition intermediary session bridge {enabledText}, {modeText}, {listeningText}, target {configuredTarget}{processText}, opcode {opcodeText}. {_expeditionIntermediaryOfficialSessionBridge.DescribeStatus()}";
        }

        private void EnsureExpeditionIntermediaryOfficialSessionBridgeState(bool shouldRun)
        {
            if (!shouldRun || !_expeditionIntermediaryOfficialSessionBridgeEnabled)
            {
                if (_expeditionIntermediaryOfficialSessionBridge.IsRunning)
                {
                    _expeditionIntermediaryOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_expeditionIntermediaryOfficialSessionBridgeConfiguredListenPort <= 0
                || _expeditionIntermediaryOfficialSessionBridgeConfiguredListenPort > ushort.MaxValue
                || _expeditionIntermediaryOfficialSessionBridgeConfiguredOpcode == 0)
            {
                if (_expeditionIntermediaryOfficialSessionBridge.IsRunning)
                {
                    _expeditionIntermediaryOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_expeditionIntermediaryOfficialSessionBridgeUseDiscovery)
            {
                if (_expeditionIntermediaryOfficialSessionBridgeConfiguredRemotePort <= 0
                    || _expeditionIntermediaryOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue)
                {
                    if (_expeditionIntermediaryOfficialSessionBridge.IsRunning)
                    {
                        _expeditionIntermediaryOfficialSessionBridge.Stop();
                    }

                    return;
                }

                _expeditionIntermediaryOfficialSessionBridge.TryRefreshFromDiscovery(
                    _expeditionIntermediaryOfficialSessionBridgeConfiguredListenPort,
                    _expeditionIntermediaryOfficialSessionBridgeConfiguredRemotePort,
                    _expeditionIntermediaryOfficialSessionBridgeConfiguredOpcode,
                    _expeditionIntermediaryOfficialSessionBridgeConfiguredProcessSelector,
                    _expeditionIntermediaryOfficialSessionBridgeConfiguredLocalPort,
                    out _);
                return;
            }

            if (_expeditionIntermediaryOfficialSessionBridgeConfiguredRemotePort <= 0
                || _expeditionIntermediaryOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue
                || string.IsNullOrWhiteSpace(_expeditionIntermediaryOfficialSessionBridgeConfiguredRemoteHost))
            {
                if (_expeditionIntermediaryOfficialSessionBridge.IsRunning)
                {
                    _expeditionIntermediaryOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_expeditionIntermediaryOfficialSessionBridge.IsRunning
                && _expeditionIntermediaryOfficialSessionBridge.ListenPort == _expeditionIntermediaryOfficialSessionBridgeConfiguredListenPort
                && _expeditionIntermediaryOfficialSessionBridge.RemotePort == _expeditionIntermediaryOfficialSessionBridgeConfiguredRemotePort
                && _expeditionIntermediaryOfficialSessionBridge.ExpeditionOpcode == _expeditionIntermediaryOfficialSessionBridgeConfiguredOpcode
                && string.Equals(_expeditionIntermediaryOfficialSessionBridge.RemoteHost, _expeditionIntermediaryOfficialSessionBridgeConfiguredRemoteHost, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (_expeditionIntermediaryOfficialSessionBridge.IsRunning)
            {
                _expeditionIntermediaryOfficialSessionBridge.Stop();
            }

            _expeditionIntermediaryOfficialSessionBridge.Start(
                _expeditionIntermediaryOfficialSessionBridgeConfiguredListenPort,
                _expeditionIntermediaryOfficialSessionBridgeConfiguredRemoteHost,
                _expeditionIntermediaryOfficialSessionBridgeConfiguredRemotePort,
                _expeditionIntermediaryOfficialSessionBridgeConfiguredOpcode);
        }

        private void RefreshExpeditionIntermediaryOfficialSessionBridgeDiscovery(int currentTickCount)
        {
            if (!_expeditionIntermediaryOfficialSessionBridgeEnabled
                || !_expeditionIntermediaryOfficialSessionBridgeUseDiscovery
                || _expeditionIntermediaryOfficialSessionBridgeConfiguredRemotePort <= 0
                || _expeditionIntermediaryOfficialSessionBridgeConfiguredOpcode == 0
                || _expeditionIntermediaryOfficialSessionBridge.HasAttachedClient
                || currentTickCount < _nextExpeditionIntermediaryOfficialSessionBridgeDiscoveryRefreshAt)
            {
                return;
            }

            _nextExpeditionIntermediaryOfficialSessionBridgeDiscoveryRefreshAt =
                currentTickCount + ExpeditionIntermediaryOfficialSessionBridgeDiscoveryRefreshIntervalMs;
            _expeditionIntermediaryOfficialSessionBridge.TryRefreshFromDiscovery(
                _expeditionIntermediaryOfficialSessionBridgeConfiguredListenPort,
                _expeditionIntermediaryOfficialSessionBridgeConfiguredRemotePort,
                _expeditionIntermediaryOfficialSessionBridgeConfiguredOpcode,
                _expeditionIntermediaryOfficialSessionBridgeConfiguredProcessSelector,
                _expeditionIntermediaryOfficialSessionBridgeConfiguredLocalPort,
                out _);
        }

        private static bool TryParseExpeditionOpcode(string token, out ushort opcode)
        {
            opcode = 0;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string trimmed = token.Trim();
            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return ushort.TryParse(trimmed[2..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out opcode);
            }

            return ushort.TryParse(trimmed, out opcode);
        }

        private ChatCommandHandler.CommandResult HandleExpeditionInboxCommand(string[] args, int actionIndex)
        {
            if (args.Length <= actionIndex)
            {
                return ChatCommandHandler.CommandResult.Info(DescribeExpeditionIntermediaryPacketInboxStatus());
            }

            return args[actionIndex].ToLowerInvariant() switch
            {
                "status" => ChatCommandHandler.CommandResult.Info(DescribeExpeditionIntermediaryPacketInboxStatus()),
                "start" => HandleExpeditionInboxStartCommand(args, actionIndex + 1),
                "stop" => HandleExpeditionInboxStopCommand(),
                _ => ChatCommandHandler.CommandResult.Error("Usage: /expedition inbox [status|start [port]|stop]")
            };
        }

        private ChatCommandHandler.CommandResult HandleExpeditionInboxStartCommand(string[] args, int portIndex)
        {
            int port = ExpeditionIntermediaryPacketInboxManager.DefaultPort;
            if (args.Length > portIndex && (!int.TryParse(args[portIndex], out port) || port <= 0 || port > ushort.MaxValue))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /expedition inbox start [port]");
            }

            _expeditionIntermediaryPacketInboxConfiguredPort = port;
            _expeditionIntermediaryPacketInboxEnabled = true;
            EnsureExpeditionIntermediaryPacketInboxState(shouldRun: true);
            return ChatCommandHandler.CommandResult.Ok(DescribeExpeditionIntermediaryPacketInboxStatus());
        }

        private ChatCommandHandler.CommandResult HandleExpeditionInboxStopCommand()
        {
            _expeditionIntermediaryPacketInboxEnabled = false;
            EnsureExpeditionIntermediaryPacketInboxState(shouldRun: false);
            return ChatCommandHandler.CommandResult.Ok(DescribeExpeditionIntermediaryPacketInboxStatus());
        }

        private ChatCommandHandler.CommandResult HandleExpeditionBridgeCommand(string[] args, int actionIndex)
        {
            if (args.Length <= actionIndex)
            {
                return ChatCommandHandler.CommandResult.Info(DescribeExpeditionIntermediaryOfficialSessionBridgeStatus());
            }

            switch (args[actionIndex].ToLowerInvariant())
            {
                case "status":
                    return ChatCommandHandler.CommandResult.Info(DescribeExpeditionIntermediaryOfficialSessionBridgeStatus());

                case "discover":
                    return HandleExpeditionBridgeDiscoverCommand(args, actionIndex + 1);

                case "start":
                    return HandleExpeditionBridgeStartCommand(args, actionIndex + 1);

                case "stop":
                    _expeditionIntermediaryOfficialSessionBridgeEnabled = false;
                    _expeditionIntermediaryOfficialSessionBridgeUseDiscovery = false;
                    _expeditionIntermediaryOfficialSessionBridgeConfiguredRemotePort = 0;
                    _expeditionIntermediaryOfficialSessionBridgeConfiguredProcessSelector = null;
                    _expeditionIntermediaryOfficialSessionBridgeConfiguredLocalPort = null;
                    _expeditionIntermediaryOfficialSessionBridge.Stop();
                    return ChatCommandHandler.CommandResult.Ok(DescribeExpeditionIntermediaryOfficialSessionBridgeStatus());

                default:
                    return ChatCommandHandler.CommandResult.Error("Usage: /expedition bridge [status|start <listenPort> <remoteHost> <remotePort> <opcode>|discover <remotePort> <opcode> [listenPort] [process=selector] [localPort=n]|stop]");
            }
        }

        private ChatCommandHandler.CommandResult HandleExpeditionBridgeStartCommand(string[] args, int startIndex)
        {
            if (args.Length <= startIndex + 3
                || !int.TryParse(args[startIndex], out int listenPort)
                || listenPort <= 0
                || listenPort > ushort.MaxValue
                || !int.TryParse(args[startIndex + 2], out int remotePort)
                || remotePort <= 0
                || remotePort > ushort.MaxValue
                || !TryParseExpeditionOpcode(args[startIndex + 3], out ushort opcode))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /expedition bridge start <listenPort> <remoteHost> <remotePort> <opcode>");
            }

            _expeditionIntermediaryOfficialSessionBridgeEnabled = true;
            _expeditionIntermediaryOfficialSessionBridgeUseDiscovery = false;
            _expeditionIntermediaryOfficialSessionBridgeConfiguredListenPort = listenPort;
            _expeditionIntermediaryOfficialSessionBridgeConfiguredRemoteHost = args[startIndex + 1];
            _expeditionIntermediaryOfficialSessionBridgeConfiguredRemotePort = remotePort;
            _expeditionIntermediaryOfficialSessionBridgeConfiguredProcessSelector = null;
            _expeditionIntermediaryOfficialSessionBridgeConfiguredLocalPort = null;
            _expeditionIntermediaryOfficialSessionBridgeConfiguredOpcode = opcode;
            EnsureExpeditionIntermediaryOfficialSessionBridgeState(shouldRun: true);
            return ChatCommandHandler.CommandResult.Ok(DescribeExpeditionIntermediaryOfficialSessionBridgeStatus());
        }

        private ChatCommandHandler.CommandResult HandleExpeditionBridgeDiscoverCommand(string[] args, int startIndex)
        {
            if (args.Length <= startIndex + 1
                || !int.TryParse(args[startIndex], out int remotePort)
                || remotePort <= 0
                || remotePort > ushort.MaxValue
                || !TryParseExpeditionOpcode(args[startIndex + 1], out ushort opcode))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /expedition bridge discover <remotePort> <opcode> [listenPort] [process=selector] [localPort=n]");
            }

            int listenPort = ExpeditionIntermediaryOfficialSessionBridgeManager.DefaultListenPort;
            if (args.Length > startIndex + 2 && int.TryParse(args[startIndex + 2], out int parsedListenPort))
            {
                listenPort = parsedListenPort;
            }

            string processSelector = null;
            int? localPort = null;
            for (int i = startIndex + 2; i < args.Length; i++)
            {
                if (args[i].StartsWith("process=", StringComparison.OrdinalIgnoreCase))
                {
                    processSelector = args[i]["process=".Length..];
                }
                else if (args[i].StartsWith("localPort=", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(args[i]["localPort=".Length..], out int parsedLocalPort))
                {
                    localPort = parsedLocalPort;
                }
            }

            _expeditionIntermediaryOfficialSessionBridgeEnabled = true;
            _expeditionIntermediaryOfficialSessionBridgeUseDiscovery = true;
            _expeditionIntermediaryOfficialSessionBridgeConfiguredListenPort = listenPort;
            _expeditionIntermediaryOfficialSessionBridgeConfiguredRemotePort = remotePort;
            _expeditionIntermediaryOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
            _expeditionIntermediaryOfficialSessionBridgeConfiguredProcessSelector = processSelector;
            _expeditionIntermediaryOfficialSessionBridgeConfiguredLocalPort = localPort;
            _expeditionIntermediaryOfficialSessionBridgeConfiguredOpcode = opcode;
            _nextExpeditionIntermediaryOfficialSessionBridgeDiscoveryRefreshAt = 0;
            return _expeditionIntermediaryOfficialSessionBridge.TryRefreshFromDiscovery(
                listenPort,
                remotePort,
                opcode,
                processSelector,
                localPort,
                out string startStatus)
                ? ChatCommandHandler.CommandResult.Ok($"{startStatus} {DescribeExpeditionIntermediaryOfficialSessionBridgeStatus()}")
                : ChatCommandHandler.CommandResult.Error(startStatus);
        }

        private ChatCommandHandler.CommandResult HandleExpeditionPayloadCommand(string[] args, int actionIndex, bool packetOwned)
        {
            string payloadError = null;
            if (args.Length <= actionIndex || !TryParseBinaryPayloadArgument(args[actionIndex], out byte[] payload, out payloadError))
            {
                return ChatCommandHandler.CommandResult.Error(payloadError ?? "Usage: /expedition [packet|local] payload <payloadhex=..|payloadb64=..>");
            }

            return TryApplyExpeditionIntermediaryPayload(payload, packetOwned, out string detail)
                ? ChatCommandHandler.CommandResult.Ok(detail)
                : ChatCommandHandler.CommandResult.Error(detail);
        }

        private ChatCommandHandler.CommandResult HandleExpeditionRawPacketCommand(string[] args, int actionIndex, bool packetOwned)
        {
            if (args.Length <= actionIndex)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /expedition [packet|local] packetraw <hex> [opcode=n]");
            }

            byte[] rawPacket;
            try
            {
                rawPacket = ByteUtils.HexToBytes(args[actionIndex]);
            }
            catch (Exception ex)
            {
                return ChatCommandHandler.CommandResult.Error($"Invalid expedition raw packet hex payload: {ex.Message}");
            }

            ushort expectedOpcode = 0;
            for (int i = actionIndex + 1; i < args.Length; i++)
            {
                if (args[i].StartsWith("opcode=", StringComparison.OrdinalIgnoreCase)
                    && TryParseExpeditionOpcode(args[i]["opcode=".Length..], out ushort parsedOpcode))
                {
                    expectedOpcode = parsedOpcode;
                }
            }

            return TryApplyExpeditionIntermediaryRawPacket(rawPacket, packetOwned, expectedOpcode, out string detail)
                ? ChatCommandHandler.CommandResult.Ok(detail)
                : ChatCommandHandler.CommandResult.Error(detail);
        }
    }
}
