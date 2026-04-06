using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using System;
using System.Globalization;
using System.Linq;
using System.Net;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly PacketReactorPoolRuntime _packetReactorPoolRuntime = new();
        private readonly ReactorPoolPacketInboxManager _reactorPoolPacketInbox = new();
        private readonly ReactorPoolOfficialSessionBridgeManager _reactorPoolOfficialSessionBridge = new();
        private bool _reactorPoolPacketInboxEnabled = EnablePacketConnectionsByDefault;
        private int _reactorPoolPacketInboxConfiguredPort = ReactorPoolPacketInboxManager.DefaultPort;
        private bool _reactorPoolOfficialSessionBridgeEnabled;
        private bool _reactorPoolOfficialSessionBridgeUseDiscovery;
        private int _reactorPoolOfficialSessionBridgeConfiguredListenPort = ReactorPoolOfficialSessionBridgeManager.DefaultListenPort;
        private string _reactorPoolOfficialSessionBridgeConfiguredRemoteHost = "127.0.0.1";
        private int _reactorPoolOfficialSessionBridgeConfiguredRemotePort;
        private string _reactorPoolOfficialSessionBridgeConfiguredProcessSelector;
        private int? _reactorPoolOfficialSessionBridgeConfiguredLocalPort;
        private const int ReactorPoolOfficialSessionBridgeDiscoveryRefreshIntervalMs = 2000;
        private int _nextReactorPoolOfficialSessionBridgeDiscoveryRefreshAt;

        private void RegisterReactorPoolPacketChatCommand()
        {
            _chat.CommandHandler.RegisterCommand(
                "reactorpacket",
                "Inspect or drive packet-owned CReactorPool lifecycle packets",
                "/reactorpacket [status|clear|enter <objectId> <templateId> <state> <x> <y> [flip] [name...]|changestate <objectId> <state> <x> <y> [hitStartDelayMs] [properEventIndex] [stateEndDelayTicks]|move <objectId> <x> <y>|leave <objectId> <state> <x> <y>|packet <changestate|move|enter|leave|334|335|336|337> [payloadhex=..|payloadb64=..]|packetraw <type> <hex>|packetclientraw <hex>|inbox [status|start [port]|stop]|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]]",
                HandlePacketOwnedReactorPoolCommand);
        }

        private bool TryApplyPacketOwnedReactorPoolPacket(int packetType, byte[] payload, out string message)
        {
            if (!TryParsePacketReactorPoolKind(packetType, out PacketReactorPoolPacketKind kind))
            {
                message = $"Unsupported reactor packet type {packetType}.";
                return false;
            }

            _packetReactorPoolRuntime.BindMap(_mapBoard?.MapInfo?.id ?? 0);
            return _packetReactorPoolRuntime.TryApplyPacket(
                kind,
                payload,
                currTickCount,
                BuildPacketReactorPoolCallbacks(),
                out message);
        }

        private PacketReactorPoolCallbacks BuildPacketReactorPoolCallbacks()
        {
            return new PacketReactorPoolCallbacks
            {
                EnterField = ApplyPacketOwnedReactorEnterField,
                ChangeState = ApplyPacketOwnedReactorChangeState,
                Move = ApplyPacketOwnedReactorMove,
                LeaveField = ApplyPacketOwnedReactorLeaveField
            };
        }

        private PacketReactorPoolApplyResult ApplyPacketOwnedReactorEnterField(PacketReactorEnterFieldPacket packet, int currentTick)
        {
            if (_reactorPool == null)
            {
                return new PacketReactorPoolApplyResult(false, "Packet-owned reactor enter ignored because ReactorPool is unavailable.");
            }

            bool entered = _reactorPool.TryEnterPacketOwnedReactor(
                packet.ObjectId,
                packet.ReactorTemplateId,
                packet.InitialState,
                packet.X,
                packet.Y,
                packet.Flip,
                packet.Name,
                currentTick,
                out int reactorIndex,
                out string detail);
            if (entered && reactorIndex >= 0)
            {
                RegisterDynamicReactorForRendering(reactorIndex);
            }

            return new PacketReactorPoolApplyResult(entered, detail);
        }

        private PacketReactorPoolApplyResult ApplyPacketOwnedReactorChangeState(PacketReactorChangeStatePacket packet, int currentTick)
        {
            if (_reactorPool == null)
            {
                return new PacketReactorPoolApplyResult(false, "Packet-owned reactor change-state ignored because ReactorPool is unavailable.");
            }

            bool changed = _reactorPool.TryChangePacketOwnedReactorState(
                packet.ObjectId,
                packet.State,
                packet.X,
                packet.Y,
                packet.HitStartDelayMs,
                packet.ProperEventIndex,
                packet.StateEndDelayTicks,
                currentTick,
                out string detail);
            return new PacketReactorPoolApplyResult(changed, detail);
        }

        private PacketReactorPoolApplyResult ApplyPacketOwnedReactorMove(PacketReactorMovePacket packet, int currentTick)
        {
            if (_reactorPool == null)
            {
                return new PacketReactorPoolApplyResult(false, "Packet-owned reactor move ignored because ReactorPool is unavailable.");
            }

            bool moved = _reactorPool.TryMovePacketOwnedReactor(
                packet.ObjectId,
                packet.X,
                packet.Y,
                currentTick,
                out string detail);
            return new PacketReactorPoolApplyResult(moved, detail);
        }

        private PacketReactorPoolApplyResult ApplyPacketOwnedReactorLeaveField(PacketReactorLeaveFieldPacket packet, int currentTick)
        {
            if (_reactorPool == null)
            {
                return new PacketReactorPoolApplyResult(false, "Packet-owned reactor leave-field ignored because ReactorPool is unavailable.");
            }

            bool left = _reactorPool.TryLeavePacketOwnedReactor(
                packet.ObjectId,
                packet.State,
                packet.X,
                packet.Y,
                currentTick,
                out string detail);
            return new PacketReactorPoolApplyResult(left, detail);
        }

        private void BindPacketOwnedReactorPoolMapState()
        {
            _packetReactorPoolRuntime.BindMap(_mapBoard?.MapInfo?.id ?? 0);
        }

        private void ClearPacketOwnedReactorPoolState()
        {
            _packetReactorPoolRuntime.Clear();
        }

        private void EnsureReactorPoolPacketInboxState(bool shouldRun)
        {
            if (!shouldRun || !_reactorPoolPacketInboxEnabled)
            {
                if (_reactorPoolPacketInbox.IsRunning)
                {
                    _reactorPoolPacketInbox.Stop();
                }

                return;
            }

            if (_reactorPoolPacketInbox.IsRunning && _reactorPoolPacketInbox.Port == _reactorPoolPacketInboxConfiguredPort)
            {
                return;
            }

            if (_reactorPoolPacketInbox.IsRunning)
            {
                _reactorPoolPacketInbox.Stop();
            }

            try
            {
                _reactorPoolPacketInbox.Start(_reactorPoolPacketInboxConfiguredPort);
            }
            catch (Exception ex)
            {
                _reactorPoolPacketInbox.Stop();
                _chat?.AddErrorMessage($"Reactor packet inbox failed to start: {ex.Message}", currTickCount);
            }
        }

        private void DrainReactorPoolPacketInbox()
        {
            while (_reactorPoolPacketInbox.TryDequeue(out ReactorPoolPacketInboxMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                bool applied = TryApplyPacketOwnedReactorPoolPacket(message.PacketType, message.Payload, out string detail);
                _reactorPoolPacketInbox.RecordDispatchResult(message, applied, detail);
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

        private string DescribeReactorPoolPacketInboxStatus()
        {
            string enabledText = _reactorPoolPacketInboxEnabled ? "enabled" : "disabled";
            string listeningText = _reactorPoolPacketInbox.IsRunning
                ? $"listening on 127.0.0.1:{_reactorPoolPacketInbox.Port}"
                : $"configured for 127.0.0.1:{_reactorPoolPacketInboxConfiguredPort}";
            return $"Reactor packet inbox {enabledText}, {listeningText}, received {_reactorPoolPacketInbox.ReceivedCount} packet(s).";
        }

        private void DrainReactorPoolOfficialSessionBridge()
        {
            while (_reactorPoolOfficialSessionBridge.TryDequeue(out ReactorPoolPacketInboxMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                bool applied = TryApplyPacketOwnedReactorPoolPacket(message.PacketType, message.Payload, out string detail);
                _reactorPoolOfficialSessionBridge.RecordDispatchResult(message.Source, applied, detail);
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

        private string DescribeReactorPoolOfficialSessionBridgeStatus()
        {
            string enabledText = _reactorPoolOfficialSessionBridgeEnabled ? "enabled" : "disabled";
            string modeText = _reactorPoolOfficialSessionBridgeUseDiscovery ? "auto-discovery" : "direct proxy";
            string configuredTarget = _reactorPoolOfficialSessionBridgeUseDiscovery
                ? _reactorPoolOfficialSessionBridgeConfiguredLocalPort.HasValue
                    ? $"discover remote port {_reactorPoolOfficialSessionBridgeConfiguredRemotePort} with local port {_reactorPoolOfficialSessionBridgeConfiguredLocalPort.Value}"
                    : $"discover remote port {_reactorPoolOfficialSessionBridgeConfiguredRemotePort}"
                : $"{_reactorPoolOfficialSessionBridgeConfiguredRemoteHost}:{_reactorPoolOfficialSessionBridgeConfiguredRemotePort}";
            string processText = string.IsNullOrWhiteSpace(_reactorPoolOfficialSessionBridgeConfiguredProcessSelector)
                ? string.Empty
                : $" for {_reactorPoolOfficialSessionBridgeConfiguredProcessSelector}";
            string listeningText = _reactorPoolOfficialSessionBridge.IsRunning
                ? $"listening on 127.0.0.1:{_reactorPoolOfficialSessionBridge.ListenPort}"
                : $"configured for 127.0.0.1:{_reactorPoolOfficialSessionBridgeConfiguredListenPort}";
            return $"Reactor packet session bridge {enabledText}, {modeText}, {listeningText}, target {configuredTarget}{processText}. {_reactorPoolOfficialSessionBridge.DescribeStatus()}";
        }

        private void EnsureReactorPoolOfficialSessionBridgeState(bool shouldRun)
        {
            if (!shouldRun || !_reactorPoolOfficialSessionBridgeEnabled)
            {
                if (_reactorPoolOfficialSessionBridge.IsRunning)
                {
                    _reactorPoolOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_reactorPoolOfficialSessionBridgeConfiguredListenPort <= 0
                || _reactorPoolOfficialSessionBridgeConfiguredListenPort > ushort.MaxValue)
            {
                if (_reactorPoolOfficialSessionBridge.IsRunning)
                {
                    _reactorPoolOfficialSessionBridge.Stop();
                }

                _reactorPoolOfficialSessionBridgeEnabled = false;
                _reactorPoolOfficialSessionBridgeConfiguredListenPort = ReactorPoolOfficialSessionBridgeManager.DefaultListenPort;
                return;
            }

            if (_reactorPoolOfficialSessionBridgeUseDiscovery)
            {
                if (_reactorPoolOfficialSessionBridgeConfiguredRemotePort <= 0
                    || _reactorPoolOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue)
                {
                    if (_reactorPoolOfficialSessionBridge.IsRunning)
                    {
                        _reactorPoolOfficialSessionBridge.Stop();
                    }

                    return;
                }

                _reactorPoolOfficialSessionBridge.TryRefreshFromDiscovery(
                    _reactorPoolOfficialSessionBridgeConfiguredListenPort,
                    _reactorPoolOfficialSessionBridgeConfiguredRemotePort,
                    _reactorPoolOfficialSessionBridgeConfiguredProcessSelector,
                    _reactorPoolOfficialSessionBridgeConfiguredLocalPort,
                    out _);
                return;
            }

            if (_reactorPoolOfficialSessionBridgeConfiguredRemotePort <= 0
                || _reactorPoolOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue
                || string.IsNullOrWhiteSpace(_reactorPoolOfficialSessionBridgeConfiguredRemoteHost))
            {
                if (_reactorPoolOfficialSessionBridge.IsRunning)
                {
                    _reactorPoolOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_reactorPoolOfficialSessionBridge.IsRunning
                && _reactorPoolOfficialSessionBridge.ListenPort == _reactorPoolOfficialSessionBridgeConfiguredListenPort
                && string.Equals(_reactorPoolOfficialSessionBridge.RemoteHost, _reactorPoolOfficialSessionBridgeConfiguredRemoteHost, StringComparison.OrdinalIgnoreCase)
                && _reactorPoolOfficialSessionBridge.RemotePort == _reactorPoolOfficialSessionBridgeConfiguredRemotePort)
            {
                return;
            }

            if (_reactorPoolOfficialSessionBridge.IsRunning)
            {
                _reactorPoolOfficialSessionBridge.Stop();
            }

            _reactorPoolOfficialSessionBridge.Start(
                _reactorPoolOfficialSessionBridgeConfiguredListenPort,
                _reactorPoolOfficialSessionBridgeConfiguredRemoteHost,
                _reactorPoolOfficialSessionBridgeConfiguredRemotePort);
        }

        private void RefreshReactorPoolOfficialSessionBridgeDiscovery(int currentTickCount)
        {
            if (!_reactorPoolOfficialSessionBridgeEnabled
                || !_reactorPoolOfficialSessionBridgeUseDiscovery
                || _reactorPoolOfficialSessionBridgeConfiguredRemotePort <= 0
                || _reactorPoolOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue
                || _reactorPoolOfficialSessionBridge.HasAttachedClient
                || currentTickCount < _nextReactorPoolOfficialSessionBridgeDiscoveryRefreshAt)
            {
                return;
            }

            _nextReactorPoolOfficialSessionBridgeDiscoveryRefreshAt =
                currentTickCount + ReactorPoolOfficialSessionBridgeDiscoveryRefreshIntervalMs;
            _reactorPoolOfficialSessionBridge.TryRefreshFromDiscovery(
                _reactorPoolOfficialSessionBridgeConfiguredListenPort,
                _reactorPoolOfficialSessionBridgeConfiguredRemotePort,
                _reactorPoolOfficialSessionBridgeConfiguredProcessSelector,
                _reactorPoolOfficialSessionBridgeConfiguredLocalPort,
                out _);
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedReactorPoolCommand(string[] args)
        {
            if (args == null || args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info($"{_packetReactorPoolRuntime.DescribeStatus()} {DescribeReactorPoolPacketInboxStatus()} {DescribeReactorPoolOfficialSessionBridgeStatus()} {_reactorPoolPacketInbox.LastStatus}");
            }

            if (string.Equals(args[0], "clear", StringComparison.OrdinalIgnoreCase))
            {
                _packetReactorPoolRuntime.Clear();
                return ChatCommandHandler.CommandResult.Ok($"{_packetReactorPoolRuntime.DescribeStatus()} {DescribeReactorPoolPacketInboxStatus()} {DescribeReactorPoolOfficialSessionBridgeStatus()}");
            }

            if (string.Equals(args[0], "inbox", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                {
                    return ChatCommandHandler.CommandResult.Info($"{DescribeReactorPoolPacketInboxStatus()} {_reactorPoolPacketInbox.LastStatus}");
                }

                if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                {
                    int port = ReactorPoolPacketInboxManager.DefaultPort;
                    if (args.Length >= 3 && (!int.TryParse(args[2], out port) || port <= 0))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /reactorpacket inbox start [port]");
                    }

                    _reactorPoolPacketInboxConfiguredPort = port;
                    _reactorPoolPacketInboxEnabled = true;
                    EnsureReactorPoolPacketInboxState(shouldRun: true);
                    return ChatCommandHandler.CommandResult.Ok(_reactorPoolPacketInbox.LastStatus);
                }

                if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                {
                    _reactorPoolPacketInboxEnabled = false;
                    EnsureReactorPoolPacketInboxState(shouldRun: false);
                    return ChatCommandHandler.CommandResult.Ok(_reactorPoolPacketInbox.LastStatus);
                }

                return ChatCommandHandler.CommandResult.Error("Usage: /reactorpacket inbox [status|start [port]|stop]");
            }

            if (string.Equals(args[0], "session", StringComparison.OrdinalIgnoreCase))
            {
                return HandlePacketOwnedReactorPoolSessionCommand(args.Skip(1).ToArray());
            }

            if (string.Equals(args[0], "enter", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 6
                    || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int objectId)
                    || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int templateId)
                    || !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int initialState)
                    || !int.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                    || !int.TryParse(args[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /reactorpacket enter <objectId> <templateId> <state> <x> <y> [flip] [name...]");
                }

                bool flip = false;
                int nameStartIndex = 6;
                if (args.Length >= 7 && TryParseBooleanToken(args[6], out bool parsedFlip))
                {
                    flip = parsedFlip;
                    nameStartIndex = 7;
                }

                string name = args.Length > nameStartIndex ? string.Join(" ", args.Skip(nameStartIndex)) : string.Empty;
                byte[] payload = PacketReactorPoolRuntime.BuildEnterFieldPayload(objectId, templateId, initialState, x, y, flip, name);
                return TryApplyPacketOwnedReactorPoolPacket((int)PacketReactorPoolPacketKind.EnterField, payload, out string message)
                    ? ChatCommandHandler.CommandResult.Ok(message)
                    : ChatCommandHandler.CommandResult.Error(message);
            }

            if (string.Equals(args[0], "changestate", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "change", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 5
                    || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int objectId)
                    || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int state)
                    || !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                    || !int.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /reactorpacket changestate <objectId> <state> <x> <y> [hitStartDelayMs] [properEventIndex] [stateEndDelayTicks]");
                }

                int hitStartDelayMs = 0;
                int properEventIndex = 0;
                int stateEndDelayTicks = 0;
                if (args.Length >= 6 && !int.TryParse(args[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out hitStartDelayMs))
                {
                    return ChatCommandHandler.CommandResult.Error("hitStartDelayMs must be an integer.");
                }

                if (args.Length >= 7 && !int.TryParse(args[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out properEventIndex))
                {
                    return ChatCommandHandler.CommandResult.Error("properEventIndex must be an integer.");
                }

                if (args.Length >= 8 && !int.TryParse(args[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out stateEndDelayTicks))
                {
                    return ChatCommandHandler.CommandResult.Error("stateEndDelayTicks must be an integer.");
                }

                byte[] payload = PacketReactorPoolRuntime.BuildChangeStatePayload(
                    objectId,
                    state,
                    x,
                    y,
                    hitStartDelayMs,
                    properEventIndex,
                    stateEndDelayTicks);
                return TryApplyPacketOwnedReactorPoolPacket((int)PacketReactorPoolPacketKind.ChangeState, payload, out string message)
                    ? ChatCommandHandler.CommandResult.Ok(message)
                    : ChatCommandHandler.CommandResult.Error(message);
            }

            if (string.Equals(args[0], "move", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 4
                    || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int objectId)
                    || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                    || !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /reactorpacket move <objectId> <x> <y>");
                }

                byte[] payload = PacketReactorPoolRuntime.BuildMovePayload(objectId, x, y);
                return TryApplyPacketOwnedReactorPoolPacket((int)PacketReactorPoolPacketKind.Move, payload, out string message)
                    ? ChatCommandHandler.CommandResult.Ok(message)
                    : ChatCommandHandler.CommandResult.Error(message);
            }

            if (string.Equals(args[0], "leave", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 5
                    || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int objectId)
                    || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int state)
                    || !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                    || !int.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /reactorpacket leave <objectId> <state> <x> <y>");
                }

                byte[] payload = PacketReactorPoolRuntime.BuildLeaveFieldPayload(objectId, state, x, y);
                return TryApplyPacketOwnedReactorPoolPacket((int)PacketReactorPoolPacketKind.LeaveField, payload, out string message)
                    ? ChatCommandHandler.CommandResult.Ok(message)
                    : ChatCommandHandler.CommandResult.Error(message);
            }

            bool rawHex = string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase);
            if (string.Equals(args[0], "packetclientraw", StringComparison.OrdinalIgnoreCase))
            {
                return HandlePacketOwnedReactorPoolClientPacketRawCommand(args);
            }

            if (!rawHex && !string.Equals(args[0], "packet", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /reactorpacket [status|clear|enter ...|changestate ...|move ...|leave ...|packet <type> [payloadhex=..|payloadb64=..]|packetraw <type> <hex>|packetclientraw <hex>|inbox [status|start [port]|stop]|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]]");
            }

            if (args.Length < 2 || !TryParsePacketReactorPoolKind(args[1], out PacketReactorPoolPacketKind kind))
            {
                return ChatCommandHandler.CommandResult.Error("Reactor packet type must be changestate, move, enter, leave, 334, 335, 336, or 337.");
            }

            byte[] packetPayload = Array.Empty<byte>();
            if (rawHex)
            {
                if (args.Length < 3 || !TryDecodeHexBytes(string.Concat(args.Skip(2)), out packetPayload))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /reactorpacket packetraw <type> <hex>");
                }
            }
            else if (args.Length >= 3 && !TryParseBinaryPayloadArgument(args[2], out packetPayload, out string payloadError))
            {
                return ChatCommandHandler.CommandResult.Error(payloadError ?? "Payload must use payloadhex=.. or payloadb64=..");
            }

            return TryApplyPacketOwnedReactorPoolPacket((int)kind, packetPayload, out string result)
                ? ChatCommandHandler.CommandResult.Ok(result)
                : ChatCommandHandler.CommandResult.Error(result);
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedReactorPoolClientPacketRawCommand(string[] args)
        {
            if (args.Length < 2 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(1)), out byte[] rawPacket))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /reactorpacket packetclientraw <hex>");
            }

            if (!ReactorPoolPacketInboxManager.TryDecodeOpcodeFramedPacket(rawPacket, out int packetType, out byte[] payload, out string decodeError))
            {
                return ChatCommandHandler.CommandResult.Error(decodeError ?? "Usage: /reactorpacket packetclientraw <hex>");
            }

            bool applied = TryApplyPacketOwnedReactorPoolPacket(packetType, payload, out string message);
            return applied
                ? ChatCommandHandler.CommandResult.Ok($"Applied reactor client opcode {packetType}. {message}")
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedReactorPoolSessionCommand(string[] args)
        {
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribeReactorPoolOfficialSessionBridgeStatus());
            }

            if (string.Equals(args[0], "discover", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2
                    || !int.TryParse(args[1], out int discoverRemotePort)
                    || discoverRemotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /reactorpacket session discover <remotePort> [processName|pid] [localPort]");
                }

                string processSelector = args.Length >= 3 ? args[2] : null;
                int? localPortFilter = null;
                if (args.Length >= 4)
                {
                    if (!int.TryParse(args[3], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /reactorpacket session discover <remotePort> [processName|pid] [localPort]");
                    }

                    localPortFilter = parsedLocalPort;
                }

                return ChatCommandHandler.CommandResult.Info(
                    _reactorPoolOfficialSessionBridge.DescribeDiscoveredSessions(discoverRemotePort, processSelector, localPortFilter));
            }

            if (string.Equals(args[0], "start", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 4
                    || !int.TryParse(args[1], out int listenPort)
                    || listenPort <= 0
                    || !int.TryParse(args[3], out int remotePort)
                    || remotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /reactorpacket session start <listenPort> <serverHost> <serverPort>");
                }

                _reactorPoolOfficialSessionBridgeEnabled = true;
                _reactorPoolOfficialSessionBridgeUseDiscovery = false;
                _reactorPoolOfficialSessionBridgeConfiguredListenPort = listenPort;
                _reactorPoolOfficialSessionBridgeConfiguredRemoteHost = args[2];
                _reactorPoolOfficialSessionBridgeConfiguredRemotePort = remotePort;
                _reactorPoolOfficialSessionBridgeConfiguredProcessSelector = null;
                _reactorPoolOfficialSessionBridgeConfiguredLocalPort = null;
                EnsureReactorPoolOfficialSessionBridgeState(shouldRun: true);
                return ChatCommandHandler.CommandResult.Ok(DescribeReactorPoolOfficialSessionBridgeStatus());
            }

            if (string.Equals(args[0], "startauto", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3
                    || !int.TryParse(args[1], out int autoListenPort)
                    || autoListenPort <= 0
                    || !int.TryParse(args[2], out int autoRemotePort)
                    || autoRemotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /reactorpacket session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                }

                string processSelector = args.Length >= 4 ? args[3] : null;
                int? localPortFilter = null;
                if (args.Length >= 5)
                {
                    if (!int.TryParse(args[4], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /reactorpacket session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                    }

                    localPortFilter = parsedLocalPort;
                }

                _reactorPoolOfficialSessionBridgeEnabled = true;
                _reactorPoolOfficialSessionBridgeUseDiscovery = true;
                _reactorPoolOfficialSessionBridgeConfiguredListenPort = autoListenPort;
                _reactorPoolOfficialSessionBridgeConfiguredRemotePort = autoRemotePort;
                _reactorPoolOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
                _reactorPoolOfficialSessionBridgeConfiguredProcessSelector = processSelector;
                _reactorPoolOfficialSessionBridgeConfiguredLocalPort = localPortFilter;
                _nextReactorPoolOfficialSessionBridgeDiscoveryRefreshAt = 0;

                return _reactorPoolOfficialSessionBridge.TryRefreshFromDiscovery(
                        autoListenPort,
                        autoRemotePort,
                        processSelector,
                        localPortFilter,
                        out string startStatus)
                    ? ChatCommandHandler.CommandResult.Ok($"{startStatus} {DescribeReactorPoolOfficialSessionBridgeStatus()}")
                    : ChatCommandHandler.CommandResult.Error(startStatus);
            }

            if (string.Equals(args[0], "stop", StringComparison.OrdinalIgnoreCase))
            {
                _reactorPoolOfficialSessionBridgeEnabled = false;
                _reactorPoolOfficialSessionBridgeUseDiscovery = false;
                _reactorPoolOfficialSessionBridgeConfiguredRemotePort = 0;
                _reactorPoolOfficialSessionBridgeConfiguredProcessSelector = null;
                _reactorPoolOfficialSessionBridgeConfiguredLocalPort = null;
                _reactorPoolOfficialSessionBridge.Stop();
                return ChatCommandHandler.CommandResult.Ok(DescribeReactorPoolOfficialSessionBridgeStatus());
            }

            return ChatCommandHandler.CommandResult.Error("Usage: /reactorpacket session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]");
        }

        private static bool TryParsePacketReactorPoolKind(string value, out PacketReactorPoolPacketKind kind)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int packetType))
            {
                return TryParsePacketReactorPoolKind(packetType, out kind);
            }

            return TryParsePacketReactorPoolKind(value?.Trim().ToLowerInvariant() switch
            {
                "changestate" or "change" => 334,
                "move" => 335,
                "enter" or "enterfield" => 336,
                "leave" or "leavefield" => 337,
                _ => -1
            }, out kind);
        }

        private static bool TryParsePacketReactorPoolKind(int packetType, out PacketReactorPoolPacketKind kind)
        {
            kind = packetType switch
            {
                334 => PacketReactorPoolPacketKind.ChangeState,
                335 => PacketReactorPoolPacketKind.Move,
                336 => PacketReactorPoolPacketKind.EnterField,
                337 => PacketReactorPoolPacketKind.LeaveField,
                _ => default
            };
            return Enum.IsDefined(typeof(PacketReactorPoolPacketKind), kind);
        }

        private static bool TryParseBooleanToken(string value, out bool result)
        {
            result = value?.Trim().ToLowerInvariant() switch
            {
                "1" or "true" or "on" or "yes" => true,
                "0" or "false" or "off" or "no" => false,
                _ => false
            };

            return value != null && (value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("0", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("false", StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", StringComparison.OrdinalIgnoreCase)
                || value.Equals("off", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("no", StringComparison.OrdinalIgnoreCase));
        }
    }
}
