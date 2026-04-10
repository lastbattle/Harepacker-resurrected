using HaCreator.MapSimulator.Managers;
using System;
using System.Net;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private bool _guildBossOfficialSessionBridgeEnabled;
        private bool _guildBossOfficialSessionBridgeUseDiscovery;
        private int _guildBossOfficialSessionBridgeConfiguredListenPort = GuildBossOfficialSessionBridgeManager.DefaultListenPort;
        private string _guildBossOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
        private int _guildBossOfficialSessionBridgeConfiguredRemotePort;
        private string _guildBossOfficialSessionBridgeConfiguredProcessSelector;
        private int? _guildBossOfficialSessionBridgeConfiguredLocalPort;
        private const int GuildBossOfficialSessionBridgeDiscoveryRefreshIntervalMs = 2000;
        private int _nextGuildBossOfficialSessionBridgeDiscoveryRefreshAt;

        internal static bool HasGuildBossOfficialSessionBridgeOwnership(
            bool isRunning,
            bool hasAttachedClient,
            bool hasPassiveEstablishedSocketPair)
        {
            return isRunning || hasAttachedClient || hasPassiveEstablishedSocketPair;
        }

        internal static bool ShouldAllowLocalGuildBossPulleyPreview(
            bool officialSessionBridgeHoldsOwnership,
            bool transportHasConnectedClients)
        {
            return !officialSessionBridgeHoldsOwnership && !transportHasConnectedClients;
        }

        private bool HoldsGuildBossOfficialSessionBridgeOwnership()
        {
            return HasGuildBossOfficialSessionBridgeOwnership(
                _guildBossOfficialSessionBridge.IsRunning,
                _guildBossOfficialSessionBridge.HasAttachedClient,
                _guildBossOfficialSessionBridge.HasPassiveEstablishedSocketPair);
        }

        private string DescribeGuildBossOfficialSessionBridgeStatus()
        {
            string enabledText = _guildBossOfficialSessionBridgeEnabled ? "enabled" : "disabled";
            string modeText = _guildBossOfficialSessionBridgeUseDiscovery ? "auto-discovery" : "direct proxy";
            string configuredTarget = _guildBossOfficialSessionBridgeUseDiscovery
                ? _guildBossOfficialSessionBridgeConfiguredLocalPort.HasValue
                    ? $"discover remote port {_guildBossOfficialSessionBridgeConfiguredRemotePort} with local port {_guildBossOfficialSessionBridgeConfiguredLocalPort.Value}"
                    : $"discover remote port {_guildBossOfficialSessionBridgeConfiguredRemotePort}"
                : $"{_guildBossOfficialSessionBridgeConfiguredRemoteHost}:{_guildBossOfficialSessionBridgeConfiguredRemotePort}";
            string processText = string.IsNullOrWhiteSpace(_guildBossOfficialSessionBridgeConfiguredProcessSelector)
                ? string.Empty
                : $" for {_guildBossOfficialSessionBridgeConfiguredProcessSelector}";
            string listeningText = _guildBossOfficialSessionBridge.IsRunning
                ? $"listening on 127.0.0.1:{_guildBossOfficialSessionBridge.ListenPort}"
                : _guildBossOfficialSessionBridgeConfiguredListenPort == 0
                    ? "configured for 127.0.0.1:auto"
                    : $"configured for 127.0.0.1:{_guildBossOfficialSessionBridgeConfiguredListenPort}";
            return $"Guild boss session bridge {enabledText}, {modeText}, {listeningText}, target {configuredTarget}{processText}. {_guildBossOfficialSessionBridge.DescribeStatus()}";
        }

        private void EnsureGuildBossOfficialSessionBridgeState(bool shouldRun)
        {
            if (!shouldRun || !_guildBossOfficialSessionBridgeEnabled)
            {
                if (_guildBossOfficialSessionBridge.IsRunning)
                {
                    _guildBossOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_guildBossOfficialSessionBridgeConfiguredListenPort < 0
                || _guildBossOfficialSessionBridgeConfiguredListenPort > ushort.MaxValue)
            {
                if (_guildBossOfficialSessionBridge.IsRunning)
                {
                    _guildBossOfficialSessionBridge.Stop();
                }

                _guildBossOfficialSessionBridgeEnabled = false;
                _guildBossOfficialSessionBridgeConfiguredListenPort = GuildBossOfficialSessionBridgeManager.DefaultListenPort;
                return;
            }

            if (_guildBossOfficialSessionBridgeUseDiscovery)
            {
                if (_guildBossOfficialSessionBridgeConfiguredRemotePort <= 0
                    || _guildBossOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue)
                {
                    if (_guildBossOfficialSessionBridge.IsRunning)
                    {
                        _guildBossOfficialSessionBridge.Stop();
                    }

                    return;
                }

                _guildBossOfficialSessionBridge.TryRefreshFromDiscovery(
                    _guildBossOfficialSessionBridgeConfiguredListenPort,
                    _guildBossOfficialSessionBridgeConfiguredRemotePort,
                    _guildBossOfficialSessionBridgeConfiguredProcessSelector,
                    _guildBossOfficialSessionBridgeConfiguredLocalPort,
                    out _);
                return;
            }

            if (_guildBossOfficialSessionBridgeConfiguredRemotePort <= 0
                || _guildBossOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue
                || string.IsNullOrWhiteSpace(_guildBossOfficialSessionBridgeConfiguredRemoteHost))
            {
                if (_guildBossOfficialSessionBridge.IsRunning)
                {
                    _guildBossOfficialSessionBridge.Stop();
                }

                return;
            }

            int expectedListenPort = _guildBossOfficialSessionBridgeConfiguredListenPort <= 0
                ? GuildBossOfficialSessionBridgeManager.DefaultListenPort
                : _guildBossOfficialSessionBridgeConfiguredListenPort;
            if (_guildBossOfficialSessionBridge.IsRunning
                && GuildBossOfficialSessionBridgeManager.MatchesTargetConfiguration(
                    _guildBossOfficialSessionBridge.ListenPort,
                    _guildBossOfficialSessionBridge.RemoteHost,
                    _guildBossOfficialSessionBridge.RemotePort,
                    expectedListenPort,
                    _guildBossOfficialSessionBridgeConfiguredRemoteHost,
                    _guildBossOfficialSessionBridgeConfiguredRemotePort))
            {
                return;
            }

            if (_guildBossOfficialSessionBridge.IsRunning)
            {
                _guildBossOfficialSessionBridge.Stop();
            }

            _guildBossOfficialSessionBridge.Start(
                _guildBossOfficialSessionBridgeConfiguredListenPort,
                _guildBossOfficialSessionBridgeConfiguredRemoteHost,
                _guildBossOfficialSessionBridgeConfiguredRemotePort);
        }

        private void RefreshGuildBossOfficialSessionBridgeDiscovery(int currentTickCount)
        {
            if (!_guildBossOfficialSessionBridgeEnabled
                || !_guildBossOfficialSessionBridgeUseDiscovery
                || _guildBossOfficialSessionBridgeConfiguredRemotePort <= 0
                || _guildBossOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue
                || _guildBossOfficialSessionBridge.HasAttachedClient
                || currentTickCount < _nextGuildBossOfficialSessionBridgeDiscoveryRefreshAt)
            {
                return;
            }

            _nextGuildBossOfficialSessionBridgeDiscoveryRefreshAt =
                currentTickCount + GuildBossOfficialSessionBridgeDiscoveryRefreshIntervalMs;
            _guildBossOfficialSessionBridge.TryRefreshFromDiscovery(
                _guildBossOfficialSessionBridgeConfiguredListenPort,
                _guildBossOfficialSessionBridgeConfiguredRemotePort,
                _guildBossOfficialSessionBridgeConfiguredProcessSelector,
                _guildBossOfficialSessionBridgeConfiguredLocalPort,
                out _);
        }
    }
}
