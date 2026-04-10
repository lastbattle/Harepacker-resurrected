using HaCreator.MapSimulator.Managers;
using System;
using System.Net;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int FieldMessageBoxOfficialSessionBridgeDiscoveryRefreshIntervalMs = 2000;
        private readonly FieldMessageBoxOfficialSessionBridgeManager _fieldMessageBoxOfficialSessionBridge = new();
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

            return ChatCommandHandler.CommandResult.Error("Usage: /messagebox session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]");
        }
    }
}
