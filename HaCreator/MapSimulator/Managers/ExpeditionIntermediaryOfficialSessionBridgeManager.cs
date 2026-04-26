using MapleLib.PacketLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Proxies a live Maple session and forwards the configured inbound
    /// expedition result opcode into the expedition intermediary packet seam.
    /// </summary>
    public sealed class ExpeditionIntermediaryOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18503;
        public const ushort DefaultInboundResultOpcode = 64;
        private const string DefaultProcessName = "MapleStory";
        private const int MaxRecentOutboundPackets = 32;

        private readonly ConcurrentQueue<ExpeditionIntermediaryPacketInboxMessage> _pendingMessages = new();
        private readonly object _sync = new();
        private readonly List<RecentOutboundPacket> _recentOutboundPackets = new();
        private readonly MapleRoleSessionProxy _roleSessionProxy;

        public sealed class RecentOutboundPacket
        {
            public RecentOutboundPacket(int opcode, int payloadLength, string rawPacketHex)
            {
                Opcode = opcode;
                PayloadLength = payloadLength;
                RawPacketHex = rawPacketHex ?? string.Empty;
            }

            public int Opcode { get; }
            public int PayloadLength { get; }
            public string RawPacketHex { get; }
        }
        public int ListenPort { get; private set; } = DefaultListenPort;
        public string RemoteHost { get; private set; } = IPAddress.Loopback.ToString();
        public int RemotePort { get; private set; }
        public ushort ExpeditionOpcode { get; private set; }
        public bool IsRunning => _roleSessionProxy.IsRunning;
        public bool HasAttachedClient => _roleSessionProxy.HasAttachedClient;
        public bool HasConnectedSession => _roleSessionProxy.HasConnectedSession;
        public int ReceivedCount { get; private set; }
        public int SentCount { get; private set; }
        public int ForwardedOutboundCount { get; private set; }
        public int LastSentOpcode { get; private set; } = -1;
        public int LastSentPayloadLength { get; private set; }
        public string LastStatus { get; private set; } = "Expedition intermediary official-session bridge inactive.";

        public ExpeditionIntermediaryOfficialSessionBridgeManager(Func<MapleRoleSessionProxy> roleSessionProxyFactory = null)
        {
            _roleSessionProxy = (roleSessionProxyFactory ?? (() => MapleRoleSessionProxyFactory.GlobalV95.CreateChannel()))();
            _roleSessionProxy.ServerPacketReceived += OnRoleSessionServerPacketReceived;
            _roleSessionProxy.ClientPacketReceived += OnRoleSessionClientPacketReceived;
        }

        public string DescribeStatus()
        {
            string lifecycle = IsRunning
                ? $"listening on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}"
                : "inactive";
            string session = HasConnectedSession
                ? "connected Maple session"
                : "no active Maple session";
            string opcodeText = ExpeditionOpcode > 0
                ? $"opcode={ExpeditionOpcode}"
                : "opcode unset";
            string outboundObserved = _recentOutboundPackets.Count == 0
                ? "no outbound history"
                : $"{_recentOutboundPackets.Count} captured outbound packet(s)";
            string lastSent = LastSentOpcode >= 0
                ? $"; last outbound opcode={LastSentOpcode} payload={LastSentPayloadLength} byte(s)"
                : string.Empty;
            return $"Expedition intermediary official-session bridge {lifecycle}; {session}; received={ReceivedCount}; injected={SentCount}; forwarded={ForwardedOutboundCount}; {outboundObserved}; {opcodeText}{lastSent}. {LastStatus}";
        }

        public void Start(int listenPort, string remoteHost, int remotePort, ushort expeditionOpcode)
        {
            lock (_sync)
            {
                StopInternal(clearPending: false);
                ResetInboundState();

                try
                {
                    ListenPort = listenPort <= 0 ? DefaultListenPort : listenPort;
                    RemoteHost = string.IsNullOrWhiteSpace(remoteHost) ? IPAddress.Loopback.ToString() : remoteHost.Trim();
                    RemotePort = remotePort;
                    ExpeditionOpcode = expeditionOpcode;
                    if (!_roleSessionProxy.Start(ListenPort, RemoteHost, RemotePort, out string proxyStatus))
                    {
                        StopInternal(clearPending: false);
                        LastStatus = proxyStatus;
                        return;
                    }

                    LastStatus = $"Expedition intermediary official-session bridge listening on 127.0.0.1:{ListenPort}, proxying to {RemoteHost}:{RemotePort}, and filtering opcode {ExpeditionOpcode}. {proxyStatus}";
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: false);
                    LastStatus = $"Expedition intermediary official-session bridge failed to start: {ex.Message}";
                }
            }
        }

        public bool TryStartFromDiscovery(int listenPort, int remotePort, ushort expeditionOpcode, string processSelector, int? localPort, out string status)
        {
            return TryRefreshFromDiscovery(listenPort, remotePort, expeditionOpcode, processSelector, localPort, out status);
        }

        public bool TryRefreshFromDiscovery(int listenPort, int remotePort, ushort expeditionOpcode, string processSelector, int? localPort, out string status)
        {
            if (HasAttachedClient)
            {
                status = $"Expedition intermediary official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                LastStatus = status;
                return true;
            }

            int resolvedListenPort = listenPort <= 0 ? DefaultListenPort : listenPort;
            if (!TryResolveProcessSelector(processSelector, out int? owningProcessId, out string owningProcessName, out string selectorError))
            {
                status = selectorError;
                LastStatus = status;
                return false;
            }

            IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates =
                CoconutOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            if (!TryResolveDiscoveryCandidate(candidates, remotePort, owningProcessId, owningProcessName, localPort, out CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate candidate, out status))
            {
                LastStatus = status;
                return false;
            }

            if (MatchesDiscoveredTargetConfiguration(
                    IsRunning,
                    ListenPort,
                    RemoteHost,
                    RemotePort,
                    ExpeditionOpcode,
                    resolvedListenPort,
                    candidate.RemoteEndpoint,
                    expeditionOpcode))
            {
                status = $"Expedition intermediary official-session bridge remains armed for {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} using opcode {expeditionOpcode}.";
                LastStatus = status;
                return true;
            }

            Start(resolvedListenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, expeditionOpcode);
            status = $"Expedition intermediary official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {LastStatus}";
            LastStatus = status;
            return true;
        }

        public string DescribeDiscoveredSessions(int remotePort, string processSelector = null, int? localPort = null)
        {
            if (!TryResolveProcessSelector(processSelector, out int? owningProcessId, out string owningProcessName, out string selectorError))
            {
                return selectorError;
            }

            IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates =
                CoconutOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> filteredCandidates = FilterCandidatesByLocalPort(candidates, localPort);
            if (filteredCandidates.Count == 0)
            {
                return $"No established TCP sessions matched {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
            }

            return string.Join(
                Environment.NewLine,
                filteredCandidates.Select(candidate =>
                    $"{candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}"));
        }

        public void Stop()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
                LastStatus = "Expedition intermediary official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out ExpeditionIntermediaryPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(string source, bool success, string detail)
        {
            string summary = string.IsNullOrWhiteSpace(detail) ? "expedition intermediary payload" : detail;
            LastStatus = success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.";
        }

        public bool TrySendRawPacket(byte[] rawPacket, out string status)
        {
            if (!_roleSessionProxy.HasConnectedSession)
            {
                status = "Expedition intermediary official-session bridge has no active Maple session.";
                LastStatus = status;
                return false;
            }

            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                status = "Expedition intermediary outbound packet must include a 2-byte opcode.";
                LastStatus = status;
                return false;
            }

            try
            {
                byte[] clonedPacket = (byte[])rawPacket.Clone();
                int opcode = BitConverter.ToUInt16(clonedPacket, 0);
                int payloadLength = Math.Max(0, clonedPacket.Length - sizeof(ushort));
                if (!_roleSessionProxy.TrySendToServer(clonedPacket, out string proxyStatus))
                {
                    status = proxyStatus;
                    LastStatus = status;
                    return false;
                }

                SentCount++;
                LastSentOpcode = opcode;
                LastSentPayloadLength = payloadLength;
                status = $"Injected expedition opcode {opcode} ({payloadLength} byte(s)) into live session.";
                LastStatus = status;
                return true;
            }
            catch (Exception ex)
            {
                status = $"Expedition intermediary official-session injection failed: {ex.Message}";
                LastStatus = status;
                return false;
            }
        }

        public string DescribeRecentOutboundPackets(int count)
        {
            lock (_sync)
            {
                if (_recentOutboundPackets.Count == 0)
                {
                    return "No expedition outbound client packets have been captured from the live session.";
                }

                int takeCount = Math.Max(1, count);
                return string.Join(
                    Environment.NewLine,
                    _recentOutboundPackets
                        .TakeLast(takeCount)
                        .Select((packet, index) =>
                            $"[{index + 1}] opcode={packet.Opcode} payload={packet.PayloadLength} byte(s) raw={packet.RawPacketHex}"));
            }
        }

        public string ClearRecentOutboundPackets()
        {
            lock (_sync)
            {
                _recentOutboundPackets.Clear();
            }

            return "Cleared captured expedition outbound client packet history.";
        }

        public bool TryReplayRecentOutboundPacket(int historyIndexFromNewest, out string status)
        {
            if (historyIndexFromNewest <= 0)
            {
                status = "Expedition intermediary replay index must be 1 or greater.";
                LastStatus = status;
                return false;
            }

            RecentOutboundPacket packet;
            lock (_sync)
            {
                if (_recentOutboundPackets.Count == 0)
                {
                    status = "No captured expedition outbound client packets are available to replay.";
                    LastStatus = status;
                    return false;
                }

                if (historyIndexFromNewest > _recentOutboundPackets.Count)
                {
                    status = $"Expedition intermediary replay index {historyIndexFromNewest} exceeds the {_recentOutboundPackets.Count} captured outbound packet(s).";
                    LastStatus = status;
                    return false;
                }

                packet = _recentOutboundPackets[^historyIndexFromNewest];
            }

            if (string.IsNullOrWhiteSpace(packet.RawPacketHex))
            {
                status = $"Captured expedition outbound packet {historyIndexFromNewest} has no raw payload to replay.";
                LastStatus = status;
                return false;
            }

            try
            {
                byte[] rawPacket = Convert.FromHexString(packet.RawPacketHex);
                return TrySendRawPacket(rawPacket, out status);
            }
            catch (FormatException ex)
            {
                status = $"Captured expedition outbound packet {historyIndexFromNewest} could not be replayed: {ex.Message}";
                LastStatus = status;
                return false;
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
            }
        }

        internal static bool TryDecodeInboundExpeditionPacket(byte[] rawPacket, string source, ushort expeditionOpcode, out ExpeditionIntermediaryPacketInboxMessage message)
        {
            message = null;
            if (rawPacket == null || rawPacket.Length < sizeof(ushort) || expeditionOpcode == 0)
            {
                return false;
            }

            int opcode = BitConverter.ToUInt16(rawPacket, 0);
            if (opcode != expeditionOpcode)
            {
                return false;
            }

            byte[] payload = rawPacket.Skip(sizeof(ushort)).ToArray();
            message = new ExpeditionIntermediaryPacketInboxMessage(
                payload,
                source,
                $"packetclientraw {Convert.ToHexString(rawPacket)}",
                opcode);
            return true;
        }
        private void OnRoleSessionServerPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            if (e == null || e.IsInit)
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            if (!TryDecodeInboundExpeditionPacket(e.RawPacket, $"official-session:{e.SourceEndpoint}", ExpeditionOpcode, out ExpeditionIntermediaryPacketInboxMessage message))
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            _pendingMessages.Enqueue(message);
            ReceivedCount++;
            LastStatus = $"Queued expedition opcode {ExpeditionOpcode} from live session {e.SourceEndpoint}.";
        }

        private void OnRoleSessionClientPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            if (e == null || e.IsInit)
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            ForwardedOutboundCount++;
            lock (_sync)
            {
                _recentOutboundPackets.Add(new RecentOutboundPacket(
                    e.Opcode,
                    Math.Max(0, e.RawPacket.Length - sizeof(ushort)),
                    Convert.ToHexString(e.RawPacket)));
                if (_recentOutboundPackets.Count > MaxRecentOutboundPackets)
                {
                    _recentOutboundPackets.RemoveAt(0);
                }
            }

            LastStatus = _roleSessionProxy.LastStatus;
        }

        private void StopInternal(bool clearPending)
        {
            _roleSessionProxy.Stop(resetCounters: clearPending);

            if (clearPending)
            {
                while (_pendingMessages.TryDequeue(out _))
                {
                }

                ResetInboundState();
            }
        }

        private void ResetInboundState()
        {
            ReceivedCount = 0;
            SentCount = 0;
            ForwardedOutboundCount = 0;
            LastSentOpcode = -1;
            LastSentPayloadLength = 0;
            lock (_sync)
            {
                _recentOutboundPackets.Clear();
            }
        }

        private void RecordObservedOutboundPacket(byte[] rawPacket)
        {
            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                return;
            }

            int opcode = BitConverter.ToUInt16(rawPacket, 0);
            int payloadLength = Math.Max(0, rawPacket.Length - sizeof(ushort));
            string rawPacketHex = Convert.ToHexString(rawPacket);

            lock (_sync)
            {
                _recentOutboundPackets.Add(new RecentOutboundPacket(opcode, payloadLength, rawPacketHex));
                if (_recentOutboundPackets.Count > MaxRecentOutboundPackets)
                {
                    _recentOutboundPackets.RemoveAt(0);
                }
            }
        }
        private static bool TryResolveProcessSelector(string selector, out int? owningProcessId, out string owningProcessName, out string error)
        {
            owningProcessId = null;
            owningProcessName = null;
            error = null;

            string trimmed = selector?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                owningProcessName = DefaultProcessName;
                return true;
            }

            if (int.TryParse(trimmed, out int processId) && processId > 0)
            {
                owningProcessId = processId;
                return true;
            }

            try
            {
                Process.GetProcessesByName(trimmed).FirstOrDefault();
                owningProcessName = trimmed;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Expedition intermediary official-session discovery could not inspect process selector '{trimmed}': {ex.Message}";
                return false;
            }
        }

        private static bool MatchesDiscoveredTargetConfiguration(
            bool isRunning,
            int listenPort,
            string remoteHost,
            int remotePort,
            ushort opcode,
            int desiredListenPort,
            IPEndPoint desiredRemoteEndpoint,
            ushort desiredOpcode)
        {
            if (!isRunning || desiredRemoteEndpoint == null)
            {
                return false;
            }

            return listenPort == desiredListenPort
                && remotePort == desiredRemoteEndpoint.Port
                && opcode == desiredOpcode
                && string.Equals(remoteHost, desiredRemoteEndpoint.Address.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryResolveDiscoveryCandidate(
            IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates,
            int remotePort,
            int? owningProcessId,
            string owningProcessName,
            int? localPort,
            out CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate candidate,
            out string status)
        {
            candidate = default;
            status = null;

            IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> filteredCandidates = FilterCandidatesByLocalPort(candidates, localPort);
            if (filteredCandidates.Count == 0)
            {
                status = $"Expedition intermediary official-session discovery found no established TCP session for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
                return false;
            }

            if (filteredCandidates.Count > 1)
            {
                string matches = string.Join(
                    ", ",
                    filteredCandidates.Select(entry => $"{entry.ProcessName}({entry.ProcessId}) local {entry.LocalEndpoint.Port} -> remote {entry.RemoteEndpoint.Port}"));
                status = $"Expedition intermediary official-session discovery found multiple candidates for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}: {matches}. Add a localPort filter.";
                return false;
            }

            candidate = filteredCandidates[0];
            return true;
        }

        private static IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> FilterCandidatesByLocalPort(
            IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates,
            int? localPort)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return Array.Empty<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate>();
            }

            return localPort.HasValue
                ? candidates.Where(candidate => candidate.LocalEndpoint.Port == localPort.Value).ToArray()
                : candidates;
        }

        private static string DescribeDiscoveryScope(int? owningProcessId, string owningProcessName, int remotePort, int? localPort)
        {
            string processScope = owningProcessId.HasValue
                ? $"process {owningProcessId.Value}"
                : string.IsNullOrWhiteSpace(owningProcessName)
                    ? DefaultProcessName
                    : owningProcessName;
            string remoteScope = remotePort > 0 ? $"remote port {remotePort}" : "any remote port";
            string localScope = localPort.HasValue ? $" and local port {localPort.Value}" : string.Empty;
            return $"{processScope} on {remoteScope}{localScope}";
        }
    }
}
