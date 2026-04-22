using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using Microsoft.Xna.Framework;
using System;
using System.Globalization;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly RockPaperScissorsPacketInboxManager _rockPaperScissorsPacketInbox = new();
        private readonly RockPaperScissorsClientPacketTransportManager _rockPaperScissorsClientPacketOutbox = new();
        private readonly RockPaperScissorsOfficialSessionBridgeManager _rockPaperScissorsOfficialSessionBridge = new();
        private const int RockPaperScissorsOfficialSessionBridgeDiscoveryRefreshIntervalMs = 5000;
        private bool _rockPaperScissorsOfficialSessionBridgeEnabled;
        private bool _rockPaperScissorsOfficialSessionBridgeUseDiscovery;
        private int _rockPaperScissorsOfficialSessionBridgeConfiguredListenPort = RockPaperScissorsOfficialSessionBridgeManager.DefaultListenPort;
        private string _rockPaperScissorsOfficialSessionBridgeConfiguredRemoteHost = "127.0.0.1";
        private int _rockPaperScissorsOfficialSessionBridgeConfiguredRemotePort;
        private string _rockPaperScissorsOfficialSessionBridgeConfiguredProcessSelector;
        private int? _rockPaperScissorsOfficialSessionBridgeConfiguredLocalPort;
        private int _nextRockPaperScissorsOfficialSessionBridgeDiscoveryRefreshAt;

        private void RegisterRockPaperScissorsChatCommands()
        {
            _chat.CommandHandler.RegisterCommand(
                "rps",
                "Drive the CRPSGameDlg Rock-Paper-Scissors runtime",
                "/rps <open|close|choose|main|status|packet|packetraw|inbox|outbox|session> [...]",
                args =>
                {
                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /rps <open|close|choose|main|status|packet|packetraw|inbox|outbox|session> [...]");
                    }

                    RockPaperScissorsField field = _specialFieldRuntime.Minigames.RockPaperScissors;
                    string action = args[0].ToLowerInvariant();
                    int currTickCount = Environment.TickCount;

                    switch (action)
                    {
                        case "open":
                        {
                            uint entryValue = 0;
                            if (args.Length >= 2
                                && !uint.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out entryValue))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /rps open [entryValue]");
                            }

                            byte[] payload = BitConverter.GetBytes(entryValue);
                            return TryApplyRockPaperScissorsPacket(8, payload, currTickCount, out string openMessage)
                                ? ChatCommandHandler.CommandResult.Ok(field.DescribeStatus())
                                : ChatCommandHandler.CommandResult.Error(openMessage);
                        }

                        case "close":
                        case "destroy":
                            return TryApplyRockPaperScissorsPacket(13, Array.Empty<byte>(), currTickCount, out string destroyMessage)
                                ? ChatCommandHandler.CommandResult.Ok(field.DescribeStatus())
                                : ChatCommandHandler.CommandResult.Error(destroyMessage);

                        case "choose":
                        case "select":
                        {
                            if (args.Length < 2 || !TryParseRockPaperScissorsChoiceToken(args[1], out RockPaperScissorsChoice choice))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /rps choose <rock|paper|scissor>");
                            }

                            return field.TrySelectChoice(choice, out string choiceMessage)
                                ? ChatCommandHandler.CommandResult.Ok(choiceMessage)
                                : ChatCommandHandler.CommandResult.Error(choiceMessage);
                        }

                        case "main":
                        case "start":
                        case "continue":
                        case "retry":
                            return field.TryActivateMainButton(currTickCount, out string mainMessage)
                                ? ChatCommandHandler.CommandResult.Ok(mainMessage)
                                : ChatCommandHandler.CommandResult.Error(mainMessage);

                        case "status":
                            return ChatCommandHandler.CommandResult.Info($"{field.DescribeStatus()} | inbox={_rockPaperScissorsPacketInbox.LastStatus} | outbox={_rockPaperScissorsClientPacketOutbox.LastStatus} | {DescribeRockPaperScissorsOfficialSessionBridgeStatus()}");

                        case "packet":
                        {
                            if (args.Length < 2 || !RockPaperScissorsField.TryParsePacketType(args[1], out int packetType))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /rps packet <open|destroy|win|lose|start|forceresult|result|continue|reset> [...]");
                            }

                            byte[] payload = Array.Empty<byte>();
                            switch (packetType)
                            {
                                case 8:
                                {
                                    uint entryValue = 0;
                                    if (args.Length >= 3
                                        && !uint.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out entryValue))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /rps packet open [entryValue]");
                                    }

                                    payload = BitConverter.GetBytes(entryValue);
                                    break;
                                }

                                case 11:
                                {
                                    if (args.Length < 4
                                        || !TryParseRockPaperScissorsChoiceToken(args[2], out RockPaperScissorsChoice npcChoice)
                                        || !sbyte.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out sbyte streak))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /rps packet result <rock|paper|scissor> <straightVictoryCount>");
                                    }

                                    payload = new[] { (byte)npcChoice, unchecked((byte)streak) };
                                    break;
                                }
                            }

                            return TryApplyRockPaperScissorsPacket(packetType, payload, currTickCount, out string packetMessage)
                                ? ChatCommandHandler.CommandResult.Ok(field.DescribeStatus())
                                : ChatCommandHandler.CommandResult.Error(packetMessage);
                        }

                        case "packetraw":
                        {
                            if (args.Length < 2 || !TryDecodeHexBytes(string.Join(string.Empty, args, 1, args.Length - 1), out byte[] rawPacket))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /rps packetraw <opcode-wrapped hex bytes>");
                            }

                            if (!RockPaperScissorsPacketInboxManager.TryParsePacketLine($"packetraw {Convert.ToHexString(rawPacket)}", out int packetType, out byte[] payload, out string packetParseError))
                            {
                                return ChatCommandHandler.CommandResult.Error(packetParseError ?? "Usage: /rps packetraw <opcode-wrapped hex bytes>");
                            }

                            return TryApplyRockPaperScissorsPacket(packetType, payload, currTickCount, out string packetRawMessage)
                                ? ChatCommandHandler.CommandResult.Ok(field.DescribeStatus())
                                : ChatCommandHandler.CommandResult.Error(packetRawMessage);
                        }

                        case "inbox":
                        {
                            string inboxAction = args.Length >= 2 ? args[1].ToLowerInvariant() : "status";
                            switch (inboxAction)
                            {
                                case "status":
                                    return ChatCommandHandler.CommandResult.Info(_rockPaperScissorsPacketInbox.LastStatus);
                                case "start":
                                {
                                    int port = RockPaperScissorsPacketInboxManager.DefaultPort;
                                    if (args.Length >= 3 && (!int.TryParse(args[2], out port) || port <= 0))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /rps inbox start [port]");
                                    }

                                    _rockPaperScissorsPacketInbox.Start(port);
                                    return ChatCommandHandler.CommandResult.Ok(_rockPaperScissorsPacketInbox.LastStatus);
                                }
                                case "stop":
                                    _rockPaperScissorsPacketInbox.Stop();
                                    return ChatCommandHandler.CommandResult.Ok(_rockPaperScissorsPacketInbox.LastStatus);
                                default:
                                    return ChatCommandHandler.CommandResult.Error("Usage: /rps inbox [status|start [port]|stop]");
                            }
                        }

                        case "outbox":
                        {
                            string outboxAction = args.Length >= 2 ? args[1].ToLowerInvariant() : "status";
                            switch (outboxAction)
                            {
                                case "status":
                                    return ChatCommandHandler.CommandResult.Info(_rockPaperScissorsClientPacketOutbox.DescribeStatus());
                                case "start":
                                {
                                    int port = RockPaperScissorsClientPacketTransportManager.DefaultPort;
                                    if (args.Length >= 3 && (!int.TryParse(args[2], out port) || port <= 0))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /rps outbox start [port]");
                                    }

                                    _rockPaperScissorsClientPacketOutbox.Start(port);
                                    return ChatCommandHandler.CommandResult.Ok(_rockPaperScissorsClientPacketOutbox.LastStatus);
                                }
                                case "stop":
                                    _rockPaperScissorsClientPacketOutbox.Stop();
                                    return ChatCommandHandler.CommandResult.Ok(_rockPaperScissorsClientPacketOutbox.LastStatus);
                                default:
                                    return ChatCommandHandler.CommandResult.Error("Usage: /rps outbox [status|start [port]|stop]");
                            }
                        }

                        case "session":
                            return HandleRockPaperScissorsSessionCommand(args.Length > 1 ? args[1..] : Array.Empty<string>());

                        default:
                            return ChatCommandHandler.CommandResult.Error("Usage: /rps <open|close|choose|main|status|packet|packetraw|inbox|outbox|session> [...]");
                    }
                });
        }

        private void SyncRockPaperScissorsPacketInboxState()
        {
            _specialFieldRuntime.Minigames.RockPaperScissors.SetUniqueModelessOwnerConflictEvaluator(HasRockPaperScissorsUniqueModelessConflict);

            if (_gameState.IsLoginMap)
            {
                _rockPaperScissorsPacketInbox.Stop();
                _rockPaperScissorsClientPacketOutbox.Stop();
                _rockPaperScissorsOfficialSessionBridge.Stop();
                return;
            }

            _rockPaperScissorsPacketInbox.Start();
            _rockPaperScissorsClientPacketOutbox.Start();
        }

        private string DescribeRockPaperScissorsOfficialSessionBridgeStatus()
        {
            if (_rockPaperScissorsOfficialSessionBridge.HasPassiveEstablishedSocketPair && !_rockPaperScissorsOfficialSessionBridgeEnabled)
            {
                return $"RPS session bridge passive attach. {_rockPaperScissorsOfficialSessionBridge.DescribeStatus()}";
            }

            string enabledText = _rockPaperScissorsOfficialSessionBridgeEnabled ? "enabled" : "disabled";
            string modeText = _rockPaperScissorsOfficialSessionBridgeUseDiscovery ? "auto-discovery" : "direct proxy";
            string configuredTarget = _rockPaperScissorsOfficialSessionBridgeUseDiscovery
                ? _rockPaperScissorsOfficialSessionBridgeConfiguredLocalPort.HasValue
                    ? $"discover remote port {_rockPaperScissorsOfficialSessionBridgeConfiguredRemotePort} with local port {_rockPaperScissorsOfficialSessionBridgeConfiguredLocalPort.Value}"
                    : $"discover remote port {_rockPaperScissorsOfficialSessionBridgeConfiguredRemotePort}"
                : $"{_rockPaperScissorsOfficialSessionBridgeConfiguredRemoteHost}:{_rockPaperScissorsOfficialSessionBridgeConfiguredRemotePort}";
            string processText = string.IsNullOrWhiteSpace(_rockPaperScissorsOfficialSessionBridgeConfiguredProcessSelector)
                ? string.Empty
                : $" for {_rockPaperScissorsOfficialSessionBridgeConfiguredProcessSelector}";
            return $"RPS session bridge {enabledText}, {modeText}, target {configuredTarget}{processText}. {_rockPaperScissorsOfficialSessionBridge.DescribeStatus()}";
        }

        private void EnsureRockPaperScissorsOfficialSessionBridgeState(bool shouldRun)
        {
            if (!shouldRun || !_rockPaperScissorsOfficialSessionBridgeEnabled)
            {
                if (_rockPaperScissorsOfficialSessionBridge.IsRunning)
                {
                    _rockPaperScissorsOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_rockPaperScissorsOfficialSessionBridgeConfiguredListenPort < 0 ||
                _rockPaperScissorsOfficialSessionBridgeConfiguredListenPort > ushort.MaxValue)
            {
                _rockPaperScissorsOfficialSessionBridge.Stop();
                _rockPaperScissorsOfficialSessionBridgeEnabled = false;
                _rockPaperScissorsOfficialSessionBridgeConfiguredListenPort = RockPaperScissorsOfficialSessionBridgeManager.DefaultListenPort;
                return;
            }

            if (_rockPaperScissorsOfficialSessionBridgeUseDiscovery)
            {
                if (_rockPaperScissorsOfficialSessionBridgeConfiguredRemotePort <= 0 ||
                    _rockPaperScissorsOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue)
                {
                    _rockPaperScissorsOfficialSessionBridge.Stop();
                    return;
                }

                _rockPaperScissorsOfficialSessionBridge.TryStartFromDiscovery(
                    _rockPaperScissorsOfficialSessionBridgeConfiguredListenPort,
                    _rockPaperScissorsOfficialSessionBridgeConfiguredRemotePort,
                    _rockPaperScissorsOfficialSessionBridgeConfiguredProcessSelector,
                    _rockPaperScissorsOfficialSessionBridgeConfiguredLocalPort,
                    out _);
                return;
            }

            if (_rockPaperScissorsOfficialSessionBridgeConfiguredRemotePort <= 0 ||
                _rockPaperScissorsOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue ||
                string.IsNullOrWhiteSpace(_rockPaperScissorsOfficialSessionBridgeConfiguredRemoteHost))
            {
                _rockPaperScissorsOfficialSessionBridge.Stop();
                return;
            }

            _rockPaperScissorsOfficialSessionBridge.TryStart(
                _rockPaperScissorsOfficialSessionBridgeConfiguredListenPort,
                _rockPaperScissorsOfficialSessionBridgeConfiguredRemoteHost,
                _rockPaperScissorsOfficialSessionBridgeConfiguredRemotePort,
                out _);
        }

        private void RefreshRockPaperScissorsOfficialSessionBridgeDiscovery(int currentTickCount)
        {
            if (!_rockPaperScissorsOfficialSessionBridgeEnabled ||
                !_rockPaperScissorsOfficialSessionBridgeUseDiscovery ||
                _rockPaperScissorsOfficialSessionBridgeConfiguredRemotePort <= 0 ||
                _rockPaperScissorsOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue ||
                _rockPaperScissorsOfficialSessionBridge.HasConnectedSession ||
                currentTickCount < _nextRockPaperScissorsOfficialSessionBridgeDiscoveryRefreshAt)
            {
                return;
            }

            _nextRockPaperScissorsOfficialSessionBridgeDiscoveryRefreshAt = currentTickCount + RockPaperScissorsOfficialSessionBridgeDiscoveryRefreshIntervalMs;
            _rockPaperScissorsOfficialSessionBridge.TryStartFromDiscovery(
                _rockPaperScissorsOfficialSessionBridgeConfiguredListenPort,
                _rockPaperScissorsOfficialSessionBridgeConfiguredRemotePort,
                _rockPaperScissorsOfficialSessionBridgeConfiguredProcessSelector,
                _rockPaperScissorsOfficialSessionBridgeConfiguredLocalPort,
                out _);
        }

        private void DrainRockPaperScissorsPacketInbox(int currentTickCount)
        {
            RockPaperScissorsField field = _specialFieldRuntime.Minigames.RockPaperScissors;
            while (_rockPaperScissorsPacketInbox.TryDequeue(out RockPaperScissorsPacketInboxMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                bool applied = field.TryApplyRawPacket(message.PacketType, message.Payload, currentTickCount, out string resultMessage);
                if (applied)
                {
                    DrainRockPaperScissorsPendingNotice(field);
                }

                _rockPaperScissorsPacketInbox.RecordDispatchResult(
                    message.Source,
                    message.PacketType,
                    applied,
                    applied ? field.DescribeStatus() : resultMessage);
            }
        }

        private void DrainRockPaperScissorsOfficialSessionBridge(int currentTickCount)
        {
            RockPaperScissorsField field = _specialFieldRuntime.Minigames.RockPaperScissors;
            while (_rockPaperScissorsOfficialSessionBridge.TryDequeue(out RockPaperScissorsPacketInboxMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                bool applied = field.TryApplyRawPacket(message.PacketType, message.Payload, currentTickCount, out string resultMessage);
                if (applied)
                {
                    DrainRockPaperScissorsPendingNotice(field);
                }

                _rockPaperScissorsPacketInbox.RecordDispatchResult(
                    message.Source,
                    message.PacketType,
                    applied,
                    applied ? field.DescribeStatus() : resultMessage);
            }
        }

        private bool TryApplyRockPaperScissorsPacket(int packetType, byte[] payload, int currentTickCount, out string message)
        {
            RockPaperScissorsField field = _specialFieldRuntime.Minigames.RockPaperScissors;
            bool applied = field.TryApplyRawPacket(packetType, payload, currentTickCount, out message);
            if (!applied)
            {
                return false;
            }

            DrainRockPaperScissorsPendingNotice(field);
            message = field.DescribeStatus();
            return true;
        }

        private void DrainRockPaperScissorsPendingClientPackets()
        {
            RockPaperScissorsField field = _specialFieldRuntime.Minigames.RockPaperScissors;
            while (field.TryConsumePendingClientPacket(out RockPaperScissorsClientPacket packet))
            {
                if (packet == null)
                {
                    continue;
                }

                bool preferOfficialSessionBridge = ShouldPreferRockPaperScissorsOfficialSessionBridge(
                    _rockPaperScissorsOfficialSessionBridgeEnabled,
                    _rockPaperScissorsOfficialSessionBridge.IsRunning,
                    _rockPaperScissorsOfficialSessionBridge.HasConnectedSession,
                    _rockPaperScissorsOfficialSessionBridge.HasAttachedClient,
                    _rockPaperScissorsOfficialSessionBridge.HasPassiveEstablishedSocketPair);
                if (preferOfficialSessionBridge
                    && _rockPaperScissorsOfficialSessionBridge.TrySendOrQueueClientPacket(packet, out _, out _))
                {
                    continue;
                }

                if (_rockPaperScissorsClientPacketOutbox.TrySendClientPacket(packet, out _))
                {
                    continue;
                }

                string outboxStatus = "Rock-Paper-Scissors client outbox immediate delivery unavailable.";
                if (!preferOfficialSessionBridge
                    && _rockPaperScissorsOfficialSessionBridgeEnabled
                    && _rockPaperScissorsOfficialSessionBridge.TrySendOrQueueClientPacket(packet, out _, out _))
                {
                    continue;
                }

                _rockPaperScissorsClientPacketOutbox.TryQueueClientPacket(packet, out outboxStatus);
            }
        }

        private ChatCommandHandler.CommandResult HandleRockPaperScissorsSessionCommand(string[] args)
        {
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribeRockPaperScissorsOfficialSessionBridgeStatus());
            }

            if (string.Equals(args[0], "discover", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2 || !int.TryParse(args[1], out int remotePort) || remotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /rps session discover <remotePort> [processName|pid] [localPort]");
                }

                string processSelector = args.Length >= 3 ? args[2] : null;
                int? localPort = null;
                if (args.Length >= 4)
                {
                    if (!int.TryParse(args[3], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /rps session discover <remotePort> [processName|pid] [localPort]");
                    }

                    localPort = parsedLocalPort;
                }

                return ChatCommandHandler.CommandResult.Info(
                    _rockPaperScissorsOfficialSessionBridge.DescribeDiscoveredSessions(remotePort, processSelector, localPort));
            }

            if (string.Equals(args[0], "recent", StringComparison.OrdinalIgnoreCase))
            {
                int count = 10;
                if (args.Length > 2
                    || (args.Length == 2 && (!int.TryParse(args[1], out count) || count <= 0)))
                {
                    return ChatCommandHandler.CommandResult.Error(RockPaperScissorsSessionCommandParsing.RecentUsage);
                }

                return ChatCommandHandler.CommandResult.Info(_rockPaperScissorsOfficialSessionBridge.DescribeRecentOutboundPackets(count));
            }

            if (string.Equals(args[0], "clearrecent", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length != 1)
                {
                    return ChatCommandHandler.CommandResult.Error(RockPaperScissorsSessionCommandParsing.ClearRecentUsage);
                }

                return ChatCommandHandler.CommandResult.Ok(_rockPaperScissorsOfficialSessionBridge.ClearRecentOutboundPackets());
            }

            if (string.Equals(args[0], "recentin", StringComparison.OrdinalIgnoreCase))
            {
                int count = 10;
                if (args.Length > 2
                    || (args.Length == 2 && (!int.TryParse(args[1], out count) || count <= 0)))
                {
                    return ChatCommandHandler.CommandResult.Error(RockPaperScissorsSessionCommandParsing.RecentInboundUsage);
                }

                return ChatCommandHandler.CommandResult.Info(_rockPaperScissorsOfficialSessionBridge.DescribeRecentInboundPackets(count));
            }

            if (string.Equals(args[0], "clearrecentin", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length != 1)
                {
                    return ChatCommandHandler.CommandResult.Error(RockPaperScissorsSessionCommandParsing.ClearRecentInboundUsage);
                }

                return ChatCommandHandler.CommandResult.Ok(_rockPaperScissorsOfficialSessionBridge.ClearRecentInboundPackets());
            }

            if (string.Equals(args[0], "attach", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2 || !int.TryParse(args[1], out int remotePort) || remotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /rps session attach <remotePort> [processName|pid] [localPort]");
                }

                string processSelector = args.Length >= 3 ? args[2] : null;
                int? localPort = null;
                if (args.Length >= 4)
                {
                    if (!int.TryParse(args[3], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /rps session attach <remotePort> [processName|pid] [localPort]");
                    }

                    localPort = parsedLocalPort;
                }

                _rockPaperScissorsOfficialSessionBridgeEnabled = false;
                _rockPaperScissorsOfficialSessionBridgeUseDiscovery = false;
                _rockPaperScissorsOfficialSessionBridgeConfiguredProcessSelector = processSelector;
                _rockPaperScissorsOfficialSessionBridgeConfiguredLocalPort = localPort;
                _rockPaperScissorsOfficialSessionBridgeConfiguredRemotePort = remotePort;

                return _rockPaperScissorsOfficialSessionBridge.TryAttachEstablishedSession(remotePort, processSelector, localPort, out string status)
                    ? ChatCommandHandler.CommandResult.Ok($"{status} {DescribeRockPaperScissorsOfficialSessionBridgeStatus()}")
                    : ChatCommandHandler.CommandResult.Error(status);
            }

            if (string.Equals(args[0], "attachproxy", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3 ||
                    !RockPaperScissorsSessionCommandParsing.TryParseProxyListenPort(args[1], out int listenPort) ||
                    !int.TryParse(args[2], out int remotePort) ||
                    remotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error(RockPaperScissorsSessionCommandParsing.AttachProxyUsage);
                }

                string processSelector = args.Length >= 4 ? args[3] : null;
                int? localPort = null;
                if (args.Length >= 5)
                {
                    if (!int.TryParse(args[4], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error(RockPaperScissorsSessionCommandParsing.AttachProxyUsage);
                    }

                    localPort = parsedLocalPort;
                }

                _rockPaperScissorsOfficialSessionBridgeEnabled = true;
                _rockPaperScissorsOfficialSessionBridgeUseDiscovery = true;
                _rockPaperScissorsOfficialSessionBridgeConfiguredListenPort = listenPort;
                _rockPaperScissorsOfficialSessionBridgeConfiguredRemoteHost = "127.0.0.1";
                _rockPaperScissorsOfficialSessionBridgeConfiguredRemotePort = remotePort;
                _rockPaperScissorsOfficialSessionBridgeConfiguredProcessSelector = processSelector;
                _rockPaperScissorsOfficialSessionBridgeConfiguredLocalPort = localPort;
                _nextRockPaperScissorsOfficialSessionBridgeDiscoveryRefreshAt = 0;

                return _rockPaperScissorsOfficialSessionBridge.TryAttachEstablishedSessionAndStartProxy(listenPort, remotePort, processSelector, localPort, out string status)
                    ? ChatCommandHandler.CommandResult.Ok($"{status} {DescribeRockPaperScissorsOfficialSessionBridgeStatus()}")
                    : ChatCommandHandler.CommandResult.Error(status);
            }

            if (string.Equals(args[0], "start", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 4 ||
                    !RockPaperScissorsSessionCommandParsing.TryParseProxyListenPort(args[1], out int listenPort) ||
                    !int.TryParse(args[3], out int remotePort) ||
                    remotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error(RockPaperScissorsSessionCommandParsing.StartUsage);
                }

                _rockPaperScissorsOfficialSessionBridgeEnabled = true;
                _rockPaperScissorsOfficialSessionBridgeUseDiscovery = false;
                _rockPaperScissorsOfficialSessionBridgeConfiguredListenPort = listenPort;
                _rockPaperScissorsOfficialSessionBridgeConfiguredRemoteHost = args[2];
                _rockPaperScissorsOfficialSessionBridgeConfiguredRemotePort = remotePort;
                _rockPaperScissorsOfficialSessionBridgeConfiguredProcessSelector = null;
                _rockPaperScissorsOfficialSessionBridgeConfiguredLocalPort = null;

                return _rockPaperScissorsOfficialSessionBridge.TryStart(listenPort, args[2], remotePort, out string status)
                    ? ChatCommandHandler.CommandResult.Ok($"{status} {DescribeRockPaperScissorsOfficialSessionBridgeStatus()}")
                    : ChatCommandHandler.CommandResult.Error(status);
            }

            if (string.Equals(args[0], "startauto", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3 ||
                    !RockPaperScissorsSessionCommandParsing.TryParseProxyListenPort(args[1], out int listenPort) ||
                    !int.TryParse(args[2], out int remotePort) ||
                    remotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error(RockPaperScissorsSessionCommandParsing.StartAutoUsage);
                }

                string processSelector = args.Length >= 4 ? args[3] : null;
                int? localPort = null;
                if (args.Length >= 5)
                {
                    if (!int.TryParse(args[4], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error(RockPaperScissorsSessionCommandParsing.StartAutoUsage);
                    }

                    localPort = parsedLocalPort;
                }

                _rockPaperScissorsOfficialSessionBridgeEnabled = true;
                _rockPaperScissorsOfficialSessionBridgeUseDiscovery = true;
                _rockPaperScissorsOfficialSessionBridgeConfiguredListenPort = listenPort;
                _rockPaperScissorsOfficialSessionBridgeConfiguredRemoteHost = "127.0.0.1";
                _rockPaperScissorsOfficialSessionBridgeConfiguredRemotePort = remotePort;
                _rockPaperScissorsOfficialSessionBridgeConfiguredProcessSelector = processSelector;
                _rockPaperScissorsOfficialSessionBridgeConfiguredLocalPort = localPort;
                _nextRockPaperScissorsOfficialSessionBridgeDiscoveryRefreshAt = 0;

                return _rockPaperScissorsOfficialSessionBridge.TryStartFromDiscovery(listenPort, remotePort, processSelector, localPort, out string status)
                    ? ChatCommandHandler.CommandResult.Ok($"{status} {DescribeRockPaperScissorsOfficialSessionBridgeStatus()}")
                    : ChatCommandHandler.CommandResult.Error(status);
            }

            if (string.Equals(args[0], "stop", StringComparison.OrdinalIgnoreCase))
            {
                _rockPaperScissorsOfficialSessionBridgeEnabled = false;
                _rockPaperScissorsOfficialSessionBridgeUseDiscovery = false;
                _rockPaperScissorsOfficialSessionBridgeConfiguredProcessSelector = null;
                _rockPaperScissorsOfficialSessionBridgeConfiguredLocalPort = null;
                _rockPaperScissorsOfficialSessionBridge.Stop();
                return ChatCommandHandler.CommandResult.Ok(DescribeRockPaperScissorsOfficialSessionBridgeStatus());
            }

            return ChatCommandHandler.CommandResult.Error(RockPaperScissorsSessionCommandParsing.SessionUsage);
        }

        private void DrainRockPaperScissorsPendingNotice(RockPaperScissorsField field)
        {
            while (field.TryConsumePendingNotice(out _, out string notice))
            {
                ShowPacketOwnedNoticeDialog(notice);
            }
        }

        private bool HasRockPaperScissorsUniqueModelessConflict()
        {
            return !string.IsNullOrWhiteSpace(GetVisibleUniqueModelessOwner(null))
                || (_npcInteractionOverlay?.IsVisible == true)
                || _specialFieldRuntime.Minigames.MemoryGame.IsVisible
                || _specialFieldRuntime.Minigames.Tournament.MatchTableDialog.IsVisible
                || _specialFieldRuntime.Minigames.RockPaperScissors.IsVisible;
        }

        private static bool TryParseRockPaperScissorsChoiceToken(string token, out RockPaperScissorsChoice choice)
        {
            choice = RockPaperScissorsChoice.None;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            switch (token.Trim().ToLowerInvariant())
            {
                case "rock":
                case "r":
                case "0":
                    choice = RockPaperScissorsChoice.Rock;
                    return true;
                case "paper":
                case "p":
                case "1":
                    choice = RockPaperScissorsChoice.Paper;
                    return true;
                case "scissor":
                case "scissors":
                case "s":
                case "2":
                    choice = RockPaperScissorsChoice.Scissor;
                    return true;
                default:
                    return false;
            }
        }

        internal static bool ShouldPreferRockPaperScissorsOfficialSessionBridge(
            bool bridgeEnabled,
            bool isRunning,
            bool hasConnectedSession,
            bool hasAttachedClient,
            bool hasPassiveEstablishedSocketPair)
        {
            // Passive attach is a status-only ownership seam even when the reconnect proxy
            // is not armed, so keep bridge routing preferred to avoid outbox fallback.
            return hasPassiveEstablishedSocketPair
                || (bridgeEnabled && (isRunning || hasConnectedSession || hasAttachedClient));
        }
    }
}
