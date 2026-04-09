using HaCreator.MapSimulator.Managers;
using System;
using System.Net;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly PacketFieldOfficialSessionBridgeManager _packetFieldOfficialSessionBridge = new();
        private bool _packetFieldOfficialSessionBridgeEnabled;
        private bool _packetFieldOfficialSessionBridgeUseDiscovery;
        private int _packetFieldOfficialSessionBridgeConfiguredListenPort = PacketFieldOfficialSessionBridgeManager.DefaultListenPort;
        private string _packetFieldOfficialSessionBridgeConfiguredRemoteHost = "127.0.0.1";
        private int _packetFieldOfficialSessionBridgeConfiguredRemotePort;
        private string _packetFieldOfficialSessionBridgeConfiguredProcessSelector;
        private int? _packetFieldOfficialSessionBridgeConfiguredLocalPort;
        private const int PacketFieldOfficialSessionBridgeDiscoveryRefreshIntervalMs = 2000;
        private int _nextPacketFieldOfficialSessionBridgeDiscoveryRefreshAt;

        private void DrainPacketFieldOfficialSessionBridge()
        {
            while (_packetFieldOfficialSessionBridge.TryDequeue(out ReactorPoolPacketInboxMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                bool applied = TryApplyPacketOwnedFieldScopedPacket(message.PacketType, message.Payload, out string detail);
                _packetFieldOfficialSessionBridge.RecordDispatchResult(message.Source, applied, detail);
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

        private string DescribePacketFieldOfficialSessionBridgeStatus()
        {
            string enabledText = _packetFieldOfficialSessionBridgeEnabled ? "enabled" : "disabled";
            string modeText = _packetFieldOfficialSessionBridgeUseDiscovery ? "auto-discovery" : "direct proxy";
            string configuredTarget = _packetFieldOfficialSessionBridgeUseDiscovery
                ? _packetFieldOfficialSessionBridgeConfiguredLocalPort.HasValue
                    ? $"discover remote port {_packetFieldOfficialSessionBridgeConfiguredRemotePort} with local port {_packetFieldOfficialSessionBridgeConfiguredLocalPort.Value}"
                    : $"discover remote port {_packetFieldOfficialSessionBridgeConfiguredRemotePort}"
                : $"{_packetFieldOfficialSessionBridgeConfiguredRemoteHost}:{_packetFieldOfficialSessionBridgeConfiguredRemotePort}";
            string processText = string.IsNullOrWhiteSpace(_packetFieldOfficialSessionBridgeConfiguredProcessSelector)
                ? string.Empty
                : $" for {_packetFieldOfficialSessionBridgeConfiguredProcessSelector}";
            string listeningText = _packetFieldOfficialSessionBridge.IsRunning
                ? $"listening on 127.0.0.1:{_packetFieldOfficialSessionBridge.ListenPort}"
                : $"configured for 127.0.0.1:{_packetFieldOfficialSessionBridgeConfiguredListenPort}";
            return $"Field-scoped packet session bridge {enabledText}, {modeText}, {listeningText}, target {configuredTarget}{processText}. {_packetFieldOfficialSessionBridge.DescribeStatus()}";
        }

        private void EnsurePacketFieldOfficialSessionBridgeState(bool shouldRun)
        {
            if (!shouldRun || !_packetFieldOfficialSessionBridgeEnabled)
            {
                if (_packetFieldOfficialSessionBridge.IsRunning)
                {
                    _packetFieldOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_packetFieldOfficialSessionBridgeConfiguredListenPort <= 0
                || _packetFieldOfficialSessionBridgeConfiguredListenPort > ushort.MaxValue)
            {
                if (_packetFieldOfficialSessionBridge.IsRunning)
                {
                    _packetFieldOfficialSessionBridge.Stop();
                }

                _packetFieldOfficialSessionBridgeEnabled = false;
                _packetFieldOfficialSessionBridgeConfiguredListenPort = PacketFieldOfficialSessionBridgeManager.DefaultListenPort;
                return;
            }

            if (_packetFieldOfficialSessionBridgeUseDiscovery)
            {
                if (_packetFieldOfficialSessionBridgeConfiguredRemotePort <= 0
                    || _packetFieldOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue)
                {
                    if (_packetFieldOfficialSessionBridge.IsRunning)
                    {
                        _packetFieldOfficialSessionBridge.Stop();
                    }

                    return;
                }

                _packetFieldOfficialSessionBridge.TryRefreshFromDiscovery(
                    _packetFieldOfficialSessionBridgeConfiguredListenPort,
                    _packetFieldOfficialSessionBridgeConfiguredRemotePort,
                    _packetFieldOfficialSessionBridgeConfiguredProcessSelector,
                    _packetFieldOfficialSessionBridgeConfiguredLocalPort,
                    out _);
                return;
            }

            if (_packetFieldOfficialSessionBridgeConfiguredRemotePort <= 0
                || _packetFieldOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue)
            {
                if (_packetFieldOfficialSessionBridge.IsRunning)
                {
                    _packetFieldOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_packetFieldOfficialSessionBridge.IsRunning
                && string.Equals(_packetFieldOfficialSessionBridge.RemoteHost, _packetFieldOfficialSessionBridgeConfiguredRemoteHost, StringComparison.OrdinalIgnoreCase)
                && _packetFieldOfficialSessionBridge.RemotePort == _packetFieldOfficialSessionBridgeConfiguredRemotePort
                && _packetFieldOfficialSessionBridge.ListenPort == _packetFieldOfficialSessionBridgeConfiguredListenPort)
            {
                return;
            }

            _packetFieldOfficialSessionBridge.Start(
                _packetFieldOfficialSessionBridgeConfiguredListenPort,
                _packetFieldOfficialSessionBridgeConfiguredRemoteHost,
                _packetFieldOfficialSessionBridgeConfiguredRemotePort);
        }

        private void RefreshPacketFieldOfficialSessionBridgeDiscovery(int currentTickCount)
        {
            if (!_packetFieldOfficialSessionBridgeEnabled
                || !_packetFieldOfficialSessionBridgeUseDiscovery
                || _packetFieldOfficialSessionBridgeConfiguredRemotePort <= 0
                || currentTickCount < _nextPacketFieldOfficialSessionBridgeDiscoveryRefreshAt)
            {
                return;
            }

            _nextPacketFieldOfficialSessionBridgeDiscoveryRefreshAt = currentTickCount + PacketFieldOfficialSessionBridgeDiscoveryRefreshIntervalMs;
            _packetFieldOfficialSessionBridge.TryRefreshFromDiscovery(
                _packetFieldOfficialSessionBridgeConfiguredListenPort,
                _packetFieldOfficialSessionBridgeConfiguredRemotePort,
                _packetFieldOfficialSessionBridgeConfiguredProcessSelector,
                _packetFieldOfficialSessionBridgeConfiguredLocalPort,
                out _);
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldStateSessionCommand(string[] args)
        {
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribePacketFieldOfficialSessionBridgeStatus());
            }

            if (string.Equals(args[0], "discover", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2
                    || !int.TryParse(args[1], out int discoverRemotePort)
                    || discoverRemotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /fieldstate session discover <remotePort> [processName|pid] [localPort]");
                }

                string processSelector = args.Length >= 3 ? args[2] : null;
                int? localPortFilter = null;
                if (args.Length >= 4)
                {
                    if (!int.TryParse(args[3], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /fieldstate session discover <remotePort> [processName|pid] [localPort]");
                    }

                    localPortFilter = parsedLocalPort;
                }

                return ChatCommandHandler.CommandResult.Info(
                    _packetFieldOfficialSessionBridge.DescribeDiscoveredSessions(discoverRemotePort, processSelector, localPortFilter));
            }

            if (string.Equals(args[0], "start", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 4
                    || !int.TryParse(args[1], out int listenPort)
                    || listenPort <= 0
                    || !int.TryParse(args[3], out int remotePort)
                    || remotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /fieldstate session start <listenPort> <serverHost> <serverPort>");
                }

                _packetFieldOfficialSessionBridgeEnabled = true;
                _packetFieldOfficialSessionBridgeUseDiscovery = false;
                _packetFieldOfficialSessionBridgeConfiguredListenPort = listenPort;
                _packetFieldOfficialSessionBridgeConfiguredRemoteHost = args[2];
                _packetFieldOfficialSessionBridgeConfiguredRemotePort = remotePort;
                _packetFieldOfficialSessionBridgeConfiguredProcessSelector = null;
                _packetFieldOfficialSessionBridgeConfiguredLocalPort = null;
                EnsurePacketFieldOfficialSessionBridgeState(shouldRun: true);
                return ChatCommandHandler.CommandResult.Ok(DescribePacketFieldOfficialSessionBridgeStatus());
            }

            if (string.Equals(args[0], "startauto", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3
                    || !int.TryParse(args[1], out int autoListenPort)
                    || autoListenPort <= 0
                    || !int.TryParse(args[2], out int autoRemotePort)
                    || autoRemotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /fieldstate session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                }

                string processSelector = args.Length >= 4 ? args[3] : null;
                int? localPortFilter = null;
                if (args.Length >= 5)
                {
                    if (!int.TryParse(args[4], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /fieldstate session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                    }

                    localPortFilter = parsedLocalPort;
                }

                _packetFieldOfficialSessionBridgeEnabled = true;
                _packetFieldOfficialSessionBridgeUseDiscovery = true;
                _packetFieldOfficialSessionBridgeConfiguredListenPort = autoListenPort;
                _packetFieldOfficialSessionBridgeConfiguredRemotePort = autoRemotePort;
                _packetFieldOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
                _packetFieldOfficialSessionBridgeConfiguredProcessSelector = processSelector;
                _packetFieldOfficialSessionBridgeConfiguredLocalPort = localPortFilter;
                _nextPacketFieldOfficialSessionBridgeDiscoveryRefreshAt = 0;

                return _packetFieldOfficialSessionBridge.TryRefreshFromDiscovery(
                        autoListenPort,
                        autoRemotePort,
                        processSelector,
                        localPortFilter,
                        out string startStatus)
                    ? ChatCommandHandler.CommandResult.Ok($"{startStatus} {DescribePacketFieldOfficialSessionBridgeStatus()}")
                    : ChatCommandHandler.CommandResult.Error(startStatus);
            }

            if (string.Equals(args[0], "stop", StringComparison.OrdinalIgnoreCase))
            {
                _packetFieldOfficialSessionBridgeEnabled = false;
                _packetFieldOfficialSessionBridgeUseDiscovery = false;
                _packetFieldOfficialSessionBridgeConfiguredRemotePort = 0;
                _packetFieldOfficialSessionBridgeConfiguredProcessSelector = null;
                _packetFieldOfficialSessionBridgeConfiguredLocalPort = null;
                _packetFieldOfficialSessionBridge.Stop();
                return ChatCommandHandler.CommandResult.Ok(DescribePacketFieldOfficialSessionBridgeStatus());
            }

            return ChatCommandHandler.CommandResult.Error("Usage: /fieldstate session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]");
        }
    }
}
