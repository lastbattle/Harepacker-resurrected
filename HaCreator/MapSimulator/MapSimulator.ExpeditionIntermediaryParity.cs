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
        // IDA v95: CWvsContext::OnPacket case 64 dispatches OnExpedtionResult.
        private ushort _expeditionIntermediaryOfficialSessionBridgeConfiguredOpcode = ExpeditionIntermediaryOfficialSessionBridgeManager.DefaultInboundResultOpcode;
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
            string opcodeText = $"{_expeditionIntermediaryOfficialSessionBridgeConfiguredOpcode} inbound / {ExpeditionIntermediaryPacketTable.OutboundRequestOpcode} outbound";
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

        private bool CanAutoSendExpeditionOutboundRequest()
        {
            return _expeditionIntermediaryOfficialSessionBridge.HasConnectedSession;
        }

        private bool TrySendExpeditionOutboundRequest(
            ExpeditionIntermediaryOutboundRequest request,
            out string status)
        {
            status = null;
            if (!ExpeditionIntermediaryPacketCodec.TryEncodeOutboundRequest(request, out ExpeditionIntermediaryEncodedOutboundPacket packet, out string encodeError))
            {
                status = encodeError ?? $"Expedition request '{request.Kind}' could not be encoded.";
                return false;
            }

            if (!_expeditionIntermediaryOfficialSessionBridge.TrySendRawPacket(packet.RawPacket, out string sendStatus))
            {
                status = sendStatus;
                return false;
            }

            status = $"{packet.Detail} {sendStatus}";
            return true;
        }

        private string ExecuteSocialSearchActionWithParityBridge(string actionKey)
        {
            ExpeditionIntermediaryOutboundRequest outboundRequest = default;
            bool shouldMirrorExpeditionRequest = CanAutoSendExpeditionOutboundRequest()
                && _socialListRuntime.TryBuildExpeditionSearchOutboundRequest(actionKey, out outboundRequest, out _);
            string result = _socialListRuntime.ExecuteSearchAction(actionKey);
            if (!shouldMirrorExpeditionRequest)
            {
                return result;
            }

            if (TrySendExpeditionOutboundRequest(outboundRequest, out string sendStatus))
            {
                return string.IsNullOrWhiteSpace(result) ? sendStatus : $"{result} {sendStatus}";
            }

            return string.IsNullOrWhiteSpace(result) ? sendStatus : $"{result} Live expedition send failed: {sendStatus}";
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

                case "opcodes":
                    return ChatCommandHandler.CommandResult.Info(ExpeditionIntermediaryPacketTable.DescribeOpcodeMap());

                case "history":
                    return HandleExpeditionBridgeHistoryCommand(args, actionIndex + 1);

                case "clearhistory":
                    return ChatCommandHandler.CommandResult.Ok(_expeditionIntermediaryOfficialSessionBridge.ClearRecentOutboundPackets());

                case "send":
                    return HandleExpeditionBridgeSendCommand(args, actionIndex + 1);

                case "replay":
                    return HandleExpeditionBridgeReplayCommand(args, actionIndex + 1);

                case "sendraw":
                    return HandleExpeditionBridgeSendRawCommand(args, actionIndex + 1);

                case "discoverstatus":
                    return HandleExpeditionBridgeDiscoverStatusCommand(args, actionIndex + 1);

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
                    return ChatCommandHandler.CommandResult.Error("Usage: /expedition bridge [status|opcodes|history [count]|clearhistory|send <create|register|quickjoin|request|response|leave|disband|remove|master|changeboss|relocate> ...|replay <historyIndex>|sendraw <hex>|discoverstatus <remotePort> [process=selector] [localPort=n]|start <listenPort> <remoteHost> <remotePort> [opcode]|discover <remotePort> [opcode] [listenPort] [process=selector] [localPort=n]|stop]");
            }
        }

        private ChatCommandHandler.CommandResult HandleExpeditionBridgeSendCommand(string[] args, int actionIndex)
        {
            if (args.Length <= actionIndex)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /expedition bridge send <create|register|quickjoin|request|response|leave|disband|remove|master|changeboss|relocate> ...");
            }

            return TryBuildExpeditionOutboundRequestFromCommand(args, actionIndex, out ExpeditionIntermediaryOutboundRequest request, out string error)
                ? TrySendExpeditionOutboundRequest(request, out string status)
                    ? ChatCommandHandler.CommandResult.Ok(status)
                    : ChatCommandHandler.CommandResult.Error(status)
                : ChatCommandHandler.CommandResult.Error(error);
        }

        private static bool TryBuildExpeditionOutboundRequestFromCommand(
            string[] args,
            int actionIndex,
            out ExpeditionIntermediaryOutboundRequest request,
            out string error)
        {
            request = default;
            error = null;
            if (args == null || args.Length <= actionIndex)
            {
                error = "Usage: /expedition bridge send <create|register|quickjoin|request|response|leave|disband|remove|master|changeboss|relocate> ...";
                return false;
            }

            string action = args[actionIndex].ToLowerInvariant();
            string title = TryGetExpeditionCommandValue(args, actionIndex + 1, "title", out string namedTitle)
                ? NormalizeExpeditionCommandText(namedTitle)
                : string.Empty;
            string ownerName = TryGetExpeditionCommandValue(args, actionIndex + 1, "owner", out string namedOwner)
                ? NormalizeExpeditionCommandText(namedOwner)
                : string.Empty;
            string characterName = TryGetExpeditionCommandValue(args, actionIndex + 1, "name", out string namedCharacter)
                ? NormalizeExpeditionCommandText(namedCharacter)
                : string.Empty;
            int partyIndex = TryGetExpeditionCommandInt(args, actionIndex + 1, "party", out int parsedPartyIndex) ? parsedPartyIndex : 0;
            int partyQuestId = TryGetExpeditionCommandInt(args, actionIndex + 1, "pq", out int parsedPartyQuestId) ? parsedPartyQuestId : 0;
            int characterId = TryGetExpeditionCommandInt(args, actionIndex + 1, "charid", out int parsedCharacterId) ? parsedCharacterId : 0;

            switch (action)
            {
                case "create":
                case "register":
                    request = new ExpeditionIntermediaryOutboundRequest(
                        string.Equals(action, "register", StringComparison.OrdinalIgnoreCase)
                            ? ExpeditionIntermediaryOutboundRequestKind.Register
                            : ExpeditionIntermediaryOutboundRequestKind.Start,
                        title,
                        ownerName,
                        characterName,
                        partyIndex,
                        ExpeditionNoticeKind.Joined,
                        ExpeditionRemovalKind.Leave,
                        PartyQuestId: partyQuestId,
                        CharacterId: characterId);
                    return true;

                case "quickjoin":
                case "request":
                    if (string.IsNullOrWhiteSpace(ownerName) && string.IsNullOrWhiteSpace(characterName))
                    {
                        error = "Usage: /expedition bridge send quickjoin|request owner=<leaderName> [title=Expedition]";
                        return false;
                    }

                    request = new ExpeditionIntermediaryOutboundRequest(
                        string.Equals(action, "quickjoin", StringComparison.OrdinalIgnoreCase)
                            ? ExpeditionIntermediaryOutboundRequestKind.QuickJoin
                            : ExpeditionIntermediaryOutboundRequestKind.Request,
                        title,
                        ownerName,
                        string.IsNullOrWhiteSpace(characterName) ? ownerName : characterName,
                        partyIndex,
                        ExpeditionNoticeKind.Joined,
                        ExpeditionRemovalKind.Leave,
                        PartyQuestId: partyQuestId,
                        CharacterId: characterId);
                    return true;

                case "response":
                {
                    bool accepted = true;
                    if (TryGetExpeditionCommandValue(args, actionIndex + 1, "result", out string responseToken))
                    {
                        accepted = string.Equals(responseToken, "accept", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(responseToken, "accepted", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(responseToken, "yes", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(responseToken, "1", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(responseToken, "true", StringComparison.OrdinalIgnoreCase);
                    }

                    request = new ExpeditionIntermediaryOutboundRequest(
                        ExpeditionIntermediaryOutboundRequestKind.Response,
                        title,
                        ownerName,
                        characterName,
                        partyIndex,
                        ExpeditionNoticeKind.Joined,
                        ExpeditionRemovalKind.Leave,
                        PartyQuestId: partyQuestId,
                        ResponseAccepted: accepted,
                        CharacterId: characterId);
                    return true;
                }

                case "leave":
                case "disband":
                    request = new ExpeditionIntermediaryOutboundRequest(
                        string.Equals(action, "leave", StringComparison.OrdinalIgnoreCase)
                            ? ExpeditionIntermediaryOutboundRequestKind.Leave
                            : ExpeditionIntermediaryOutboundRequestKind.Disband,
                        title,
                        ownerName,
                        characterName,
                        partyIndex,
                        ExpeditionNoticeKind.Joined,
                        string.Equals(action, "leave", StringComparison.OrdinalIgnoreCase)
                            ? ExpeditionRemovalKind.Leave
                            : ExpeditionRemovalKind.Disband,
                        PartyQuestId: partyQuestId,
                        CharacterId: characterId);
                    return true;

                case "remove":
                case "kick":
                    request = new ExpeditionIntermediaryOutboundRequest(
                        ExpeditionIntermediaryOutboundRequestKind.Remove,
                        title,
                        ownerName,
                        characterName,
                        partyIndex,
                        ExpeditionNoticeKind.Removed,
                        ExpeditionRemovalKind.Removed,
                        PartyQuestId: partyQuestId,
                        CharacterId: characterId);
                    return true;

                case "master":
                case "changemaster":
                    request = new ExpeditionIntermediaryOutboundRequest(
                        ExpeditionIntermediaryOutboundRequestKind.Master,
                        title,
                        ownerName,
                        characterName,
                        partyIndex,
                        ExpeditionNoticeKind.Joined,
                        ExpeditionRemovalKind.Leave,
                        PartyQuestId: partyQuestId,
                        CharacterId: characterId);
                    return true;

                case "changeboss":
                case "changepartyboss":
                    request = new ExpeditionIntermediaryOutboundRequest(
                        ExpeditionIntermediaryOutboundRequestKind.ChangePartyBoss,
                        title,
                        ownerName,
                        characterName,
                        partyIndex,
                        ExpeditionNoticeKind.Joined,
                        ExpeditionRemovalKind.Leave,
                        PartyQuestId: partyQuestId,
                        CharacterId: characterId);
                    return true;

                case "relocate":
                case "relocateparty":
                    request = new ExpeditionIntermediaryOutboundRequest(
                        ExpeditionIntermediaryOutboundRequestKind.RelocateParty,
                        title,
                        ownerName,
                        characterName,
                        partyIndex,
                        ExpeditionNoticeKind.Joined,
                        ExpeditionRemovalKind.Leave,
                        PartyQuestId: partyQuestId,
                        CharacterId: characterId);
                    return true;

                default:
                    error = $"Unsupported expedition bridge send action '{action}'.";
                    return false;
            }
        }

        private ChatCommandHandler.CommandResult HandleExpeditionBridgeHistoryCommand(string[] args, int countIndex)
        {
            int historyCount = 10;
            if (args.Length > countIndex
                && (!int.TryParse(args[countIndex], out historyCount) || historyCount <= 0))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /expedition bridge history [count]");
            }

            return ChatCommandHandler.CommandResult.Info(_expeditionIntermediaryOfficialSessionBridge.DescribeRecentOutboundPackets(historyCount));
        }

        private ChatCommandHandler.CommandResult HandleExpeditionBridgeReplayCommand(string[] args, int historyIndex)
        {
            if (args.Length <= historyIndex
                || !int.TryParse(args[historyIndex], out int replayIndex)
                || replayIndex <= 0)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /expedition bridge replay <historyIndex>");
            }

            return _expeditionIntermediaryOfficialSessionBridge.TryReplayRecentOutboundPacket(replayIndex, out string replayStatus)
                ? ChatCommandHandler.CommandResult.Ok(replayStatus)
                : ChatCommandHandler.CommandResult.Error(replayStatus);
        }

        private ChatCommandHandler.CommandResult HandleExpeditionBridgeSendRawCommand(string[] args, int payloadIndex)
        {
            if (args.Length <= payloadIndex || !TryDecodeHexBytes(string.Concat(args[payloadIndex..]), out byte[] rawPacket))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /expedition bridge sendraw <hex>");
            }

            return _expeditionIntermediaryOfficialSessionBridge.TrySendRawPacket(rawPacket, out string sendStatus)
                ? ChatCommandHandler.CommandResult.Ok(sendStatus)
                : ChatCommandHandler.CommandResult.Error(sendStatus);
        }

        private ChatCommandHandler.CommandResult HandleExpeditionBridgeDiscoverStatusCommand(string[] args, int startIndex)
        {
            if (args.Length <= startIndex
                || !int.TryParse(args[startIndex], out int remotePort)
                || remotePort <= 0
                || remotePort > ushort.MaxValue)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /expedition bridge discoverstatus <remotePort> [process=selector] [localPort=n]");
            }

            string processSelector = null;
            int? localPort = null;
            for (int i = startIndex + 1; i < args.Length; i++)
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

            return ChatCommandHandler.CommandResult.Info(
                _expeditionIntermediaryOfficialSessionBridge.DescribeDiscoveredSessions(remotePort, processSelector, localPort));
        }

        private ChatCommandHandler.CommandResult HandleExpeditionBridgeStartCommand(string[] args, int startIndex)
        {
            if (args.Length <= startIndex + 2
                || !int.TryParse(args[startIndex], out int listenPort)
                || listenPort <= 0
                || listenPort > ushort.MaxValue
                || !int.TryParse(args[startIndex + 2], out int remotePort)
                || remotePort <= 0
                || remotePort > ushort.MaxValue)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /expedition bridge start <listenPort> <remoteHost> <remotePort> [opcode]");
            }

            ushort opcode = _expeditionIntermediaryOfficialSessionBridgeConfiguredOpcode;
            if (args.Length > startIndex + 3
                && !TryParseExpeditionOpcode(args[startIndex + 3], out opcode))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /expedition bridge start <listenPort> <remoteHost> <remotePort> [opcode]");
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
            if (args.Length <= startIndex
                || !int.TryParse(args[startIndex], out int remotePort)
                || remotePort <= 0
                || remotePort > ushort.MaxValue)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /expedition bridge discover <remotePort> [opcode] [listenPort] [process=selector] [localPort=n]");
            }

            ushort opcode = _expeditionIntermediaryOfficialSessionBridgeConfiguredOpcode;
            int listenPort = ExpeditionIntermediaryOfficialSessionBridgeManager.DefaultListenPort;
            int optionIndex = startIndex + 1;
            if (args.Length > optionIndex
                && TryParseExpeditionOpcode(args[optionIndex], out ushort parsedOpcode))
            {
                opcode = parsedOpcode;
                optionIndex++;
            }

            if (args.Length > optionIndex && int.TryParse(args[optionIndex], out int parsedListenPort))
            {
                listenPort = parsedListenPort;
                optionIndex++;
            }

            string processSelector = null;
            int? localPort = null;
            for (int i = optionIndex; i < args.Length; i++)
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
