using MapleLib.PacketLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class MapTransferPacketInboxMessage
    {
        public MapTransferPacketInboxMessage(byte[] payload, string source, string rawText)
        {
            Payload = payload ?? Array.Empty<byte>();
            Source = source ?? "map-transfer";
            RawText = rawText ?? string.Empty;
        }

        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    /// <summary>
    /// Proxies a live Maple session and forwards CWvsContext map-transfer result
    /// packets into the simulator's existing map-transfer runtime.
    /// </summary>
    public sealed class MapTransferOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18497;
        private const string DefaultProcessName = "MapleStory";

        private sealed class ObservedOutboundRequest
        {
            public MapTransferRuntimeRequest Request { get; init; }
            public string Source { get; init; }
            public string RawText { get; init; }
        }
        private readonly ConcurrentQueue<MapTransferPacketInboxMessage> _pendingMessages = new();
        private readonly List<ObservedOutboundRequest> _observedOutboundRequests = new();
        private readonly object _sync = new();
        private readonly MapleRoleSessionProxy _roleSessionProxy;

        public int ListenPort { get; private set; } = DefaultListenPort;
        public string RemoteHost { get; private set; } = IPAddress.Loopback.ToString();
        public int RemotePort { get; private set; }
        public bool IsRunning => _roleSessionProxy.IsRunning;
        public bool HasConnectedSession => _roleSessionProxy.HasConnectedSession;
        internal Func<int> ActiveFieldMapIdProvider { get; set; }
        internal int ObservedOutboundRequestCount
        {
            get
            {
                lock (_sync)
                {
                    return _observedOutboundRequests.Count;
                }
            }
        }
        public int ReceivedCount { get; private set; }
        public int SentCount { get; private set; }
        public string LastStatus { get; private set; } = "Map transfer official-session bridge inactive.";

        public MapTransferOfficialSessionBridgeManager(Func<MapleRoleSessionProxy> roleSessionProxyFactory = null)
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
            return $"Map transfer official-session bridge {lifecycle}; {session}; received={ReceivedCount}; sent={SentCount}; observed outbound={ObservedOutboundRequestCount}; inbound opcode={MapTransferPacketCodec.InboundResultOpcode}; outbound opcode={MapTransferPacketCodec.OutboundRequestOpcode}. {LastStatus}";
        }

        public bool TryStart(int listenPort, string remoteHost, int remotePort, out string status)
        {
            lock (_sync)
            {
                string normalizedRemoteHost = string.IsNullOrWhiteSpace(remoteHost)
                    ? IPAddress.Loopback.ToString()
                    : remoteHost.Trim();
                bool targetChanged = !string.Equals(RemoteHost, normalizedRemoteHost, StringComparison.OrdinalIgnoreCase)
                    || (remotePort > 0 && RemotePort != remotePort);
                if (HasConnectedSession && targetChanged)
                {
                    status = $"Map transfer official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                    LastStatus = status;
                    return false;
                }

                if (IsRunning &&
                    ListenPort == listenPort &&
                    string.Equals(RemoteHost, normalizedRemoteHost, StringComparison.OrdinalIgnoreCase) &&
                    RemotePort == remotePort)
                {
                    status = $"Map transfer official-session bridge is already listening on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}.";
                    LastStatus = status;
                    return true;
                }

                StopInternal(clearPending: true);

                try
                {
                    ListenPort = listenPort <= 0 ? DefaultListenPort : listenPort;
                    RemoteHost = normalizedRemoteHost;
                    RemotePort = remotePort;
                    if (!_roleSessionProxy.Start(ListenPort, RemoteHost, RemotePort, out string proxyStatus))
                    {
                        StopInternal(clearPending: true);
                        status = proxyStatus;
                        LastStatus = status;
                        return false;
                    }

                    status = $"Map transfer official-session bridge listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}. {proxyStatus}";
                    LastStatus = status;
                    return true;
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    status = $"Map transfer official-session bridge failed to start: {ex.Message}";
                    LastStatus = status;
                    return false;
                }
            }
        }

        public void Start(int listenPort, string remoteHost, int remotePort)
        {
            TryStart(listenPort, remoteHost, remotePort, out _);
        }

        public bool TryStartFromDiscovery(int listenPort, int remotePort, string processSelector, int? localPort, out string status)
        {
            if (!TryResolveProcessSelector(processSelector, out int? owningProcessId, out string owningProcessName, out string selectorError))
            {
                status = selectorError;
                LastStatus = status;
                return false;
            }

            var candidates = TransportationOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            if (!TryResolveDiscoveryCandidate(candidates, remotePort, owningProcessId, owningProcessName, localPort, out var candidate, out status))
            {
                LastStatus = status;
                return false;
            }

            if (!TryStart(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, out string startStatus))
            {
                status = $"Map transfer official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}, but startup failed. {startStatus}";
                LastStatus = status;
                return false;
            }

            status = $"Map transfer official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {startStatus}";
            LastStatus = status;
            return true;
        }

        public string DescribeDiscoveredSessions(int remotePort, string processSelector = null, int? localPort = null)
        {
            if (!TryResolveProcessSelector(processSelector, out int? owningProcessId, out string owningProcessName, out string selectorError))
            {
                return selectorError;
            }

            var candidates = TransportationOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            if (!TryResolveDiscoveryCandidate(candidates, remotePort, owningProcessId, owningProcessName, localPort, out _, out string status))
            {
                return status;
            }

            return string.Join(
                Environment.NewLine,
                FilterCandidatesByLocalPort(candidates, localPort).Select(candidate =>
                    $"{candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}"));
        }

        public void Stop()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
                LastStatus = "Map transfer official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out MapTransferPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        internal bool TryResolveObservedRequest(
            MapTransferRuntimeResponse authoritativeResponse,
            out MapTransferRuntimeRequest request)
        {
            request = null;
            if (authoritativeResponse == null)
            {
                return false;
            }

            lock (_sync)
            {
                if (_observedOutboundRequests.Count == 0)
                {
                    return false;
                }

                List<MapTransferRuntimeRequest> pendingRequests = _observedOutboundRequests.ConvertAll(observed => observed?.Request);
                int requestIndex = MapTransferOfficialSessionResultResolver.ResolvePendingRequestIndex(pendingRequests, authoritativeResponse);
                if (requestIndex < 0 || requestIndex >= _observedOutboundRequests.Count)
                {
                    return false;
                }

                request = _observedOutboundRequests[requestIndex].Request;
                _observedOutboundRequests.RemoveAt(requestIndex);
                return request != null;
            }
        }

        internal bool TryObserveOutboundRequestPacket(byte[] rawPacket, string source, out string status)
        {
            if (!MapTransferPacketCodec.TryDecodeOutboundRequestPacket(rawPacket, out MapTransferRuntimeRequest request, out string errorMessage))
            {
                status = errorMessage;
                return false;
            }

            ObserveOutboundRequest(
                request,
                string.IsNullOrWhiteSpace(source) ? "official-session" : source,
                rawPacket == null ? string.Empty : Convert.ToHexString(rawPacket));

            status = $"Observed map transfer opcode {MapTransferPacketCodec.OutboundRequestOpcode} from {source ?? "official-session"}.";
            LastStatus = status;
            return true;
        }

        internal bool TryObserveClientRawPacket(byte[] rawPacket, string source, out string status)
        {
            if (MapTransferPacketCodec.TryDecodeOutboundRequestPacket(rawPacket, out _, out _))
            {
                return TryObserveOutboundRequestPacket(rawPacket, source, out status);
            }

            if (MapTransferPacketCodec.TryDecodeInboundResultPacket(rawPacket, out byte[] payload, out _))
            {
                return TryQueueInjectedResultPayload(payload, source, out status);
            }

            status = $"Map transfer client raw packet must be opcode {MapTransferPacketCodec.OutboundRequestOpcode} or {MapTransferPacketCodec.InboundResultOpcode}.";
            LastStatus = status;
            return false;
        }

        private MapTransferRuntimeRequest SnapshotObservedOutboundRegisterMapId(MapTransferRuntimeRequest request)
        {
            if (request?.Type != MapTransferRuntimeRequestType.Register || request.MapId > 0)
            {
                return request;
            }

            int activeFieldMapId = ActiveFieldMapIdProvider?.Invoke() ?? 0;
            if (activeFieldMapId <= 0)
            {
                return request;
            }

            return new MapTransferRuntimeRequest
            {
                Type = request.Type,
                Book = request.Book,
                MapId = activeFieldMapId,
                SlotIndex = request.SlotIndex
            };
        }

        internal void ObserveInjectedRequestForParity(MapTransferRuntimeRequest request, string source)
        {
            ObserveOutboundRequest(
                request,
                string.IsNullOrWhiteSpace(source) ? "maptransfer:inject" : source,
                string.Empty);
        }

        internal void DropObservedRequest(MapTransferRuntimeRequest request)
        {
            if (request == null)
            {
                return;
            }

            lock (_sync)
            {
                for (int i = 0; i < _observedOutboundRequests.Count; i++)
                {
                    if (!AreEquivalentRequests(_observedOutboundRequests[i]?.Request, request))
                    {
                        continue;
                    }

                    _observedOutboundRequests.RemoveAt(i);
                    return;
                }
            }
        }

        private void ObserveOutboundRequest(MapTransferRuntimeRequest request, string source, string rawText)
        {
            MapTransferRuntimeRequest snapshot = SnapshotObservedOutboundRegisterMapId(request);
            if (snapshot == null)
            {
                return;
            }

            lock (_sync)
            {
                _observedOutboundRequests.Add(new ObservedOutboundRequest
                {
                    Request = CloneRequest(snapshot),
                    Source = source ?? "official-session",
                    RawText = rawText ?? string.Empty
                });
            }
        }

        private static MapTransferRuntimeRequest CloneRequest(MapTransferRuntimeRequest request)
        {
            return request == null
                ? null
                : new MapTransferRuntimeRequest
                {
                    Type = request.Type,
                    Book = request.Book,
                    MapId = request.MapId,
                    SlotIndex = request.SlotIndex
                };
        }

        private static bool AreEquivalentRequests(MapTransferRuntimeRequest left, MapTransferRuntimeRequest right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            return left.Type == right.Type &&
                   left.Book == right.Book &&
                   left.MapId == right.MapId &&
                   left.SlotIndex == right.SlotIndex;
        }

        public void RecordDispatchResult(string source, bool success, string detail)
        {
            string summary = string.IsNullOrWhiteSpace(detail) ? "map transfer result" : detail;
            LastStatus = success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.";
        }

        public bool TrySendRequest(MapTransferRuntimeRequest request, out string status)
        {
            if (!_roleSessionProxy.HasConnectedSession)
            {
                status = "Map transfer official-session bridge has no connected Maple session for outbound injection.";
                LastStatus = status;
                return false;
            }

            byte[] rawPacket = MapTransferPacketCodec.BuildRawRequestPacket(request);
            try
            {
                if (!_roleSessionProxy.TrySendToServer(rawPacket, out string proxyStatus))
                {
                    status = proxyStatus;
                    LastStatus = status;
                    return false;
                }

                ObserveInjectedRequestForParity(request, "maptransfer:inject");
                SentCount++;
                status = "Injected map transfer request into live session.";
                LastStatus = status;
                return true;
            }
            catch (Exception ex)
            {
                status = $"Map transfer official-session outbound injection failed: {ex.Message}";
                LastStatus = status;
                return false;
            }
        }

        public bool TryQueueInjectedResultPayload(byte[] payload, string source, out string status)
        {
            if (payload == null || payload.Length < 2)
            {
                status = "Injected map transfer result payload must contain at least a result code and continent flag.";
                LastStatus = status;
                return false;
            }

            _pendingMessages.Enqueue(new MapTransferPacketInboxMessage(
                (byte[])payload.Clone(),
                string.IsNullOrWhiteSpace(source) ? "maptransfer:inject" : source,
                $"maptransfer result {Convert.ToHexString(payload)}"));
            ReceivedCount++;
            status = $"Queued injected map transfer result payload from {source ?? "maptransfer:inject"}.";
            LastStatus = status;
            return true;
        }

        public void Dispose()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
            }
        }
        private void OnRoleSessionServerPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            if (e == null || e.IsInit)
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            if (!MapTransferPacketCodec.TryDecodeInboundResultPacket(e.RawPacket, out byte[] payload, out _))
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            _pendingMessages.Enqueue(new MapTransferPacketInboxMessage(
                payload,
                $"official-session:{e.SourceEndpoint}",
                $"packetraw {Convert.ToHexString(e.RawPacket)}"));
            ReceivedCount++;
            LastStatus = $"Queued map transfer result opcode {MapTransferPacketCodec.InboundResultOpcode} from live session {e.SourceEndpoint}.";
        }

        private void OnRoleSessionClientPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            if (e == null || e.IsInit)
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            TryObserveOutboundRequestPacket(e.RawPacket, $"official-session:{e.SourceEndpoint}", out _);
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

                lock (_sync)
                {
                    _observedOutboundRequests.Clear();
                }

                ReceivedCount = 0;
                SentCount = 0;
            }
        }
        private static bool TryResolveProcessSelector(string selector, out int? owningProcessId, out string owningProcessName, out string error)
        {
            owningProcessId = null;
            owningProcessName = null;
            error = null;
            if (string.IsNullOrWhiteSpace(selector))
            {
                owningProcessName = DefaultProcessName;
                return true;
            }

            if (int.TryParse(selector, out int pid) && pid > 0)
            {
                owningProcessId = pid;
                return true;
            }

            string normalized = NormalizeProcessSelector(selector);
            if (normalized.Length == 0)
            {
                error = "Map transfer official-session discovery requires a process name or pid when a selector is provided.";
                return false;
            }

            owningProcessName = normalized;
            return true;
        }

        private static string NormalizeProcessSelector(string selector)
        {
            string trimmed = selector?.Trim() ?? string.Empty;
            return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? trimmed[..^4]
                : trimmed;
        }

        private static bool TryResolveDiscoveryCandidate(
            System.Collections.Generic.IReadOnlyList<TransportationOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates,
            int remotePort,
            int? owningProcessId,
            string owningProcessName,
            int? localPort,
            out TransportationOfficialSessionBridgeManager.SessionDiscoveryCandidate candidate,
            out string status)
        {
            var filteredCandidates = FilterCandidatesByLocalPort(candidates, localPort);
            if (filteredCandidates.Count == 0)
            {
                status = $"Map transfer official-session discovery found no established TCP session for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
                candidate = default;
                return false;
            }

            candidate = filteredCandidates[0];
            status = null;
            return true;
        }

        private static System.Collections.Generic.IReadOnlyList<TransportationOfficialSessionBridgeManager.SessionDiscoveryCandidate> FilterCandidatesByLocalPort(
            System.Collections.Generic.IReadOnlyList<TransportationOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates,
            int? localPort)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return Array.Empty<TransportationOfficialSessionBridgeManager.SessionDiscoveryCandidate>();
            }

            if (!localPort.HasValue)
            {
                return candidates;
            }

            return candidates.Where(candidate => candidate.LocalEndpoint.Port == localPort.Value).ToArray();
        }

        private static string DescribeDiscoveryScope(int? owningProcessId, string owningProcessName, int remotePort, int? localPort)
        {
            string processScope = owningProcessId.HasValue
                ? $"pid {owningProcessId.Value}"
                : !string.IsNullOrWhiteSpace(owningProcessName)
                    ? owningProcessName
                    : DefaultProcessName;
            string localScope = localPort.HasValue ? $" and local port {localPort.Value}" : string.Empty;
            return $"{processScope} on remote port {remotePort}{localScope}";
        }
    }
}
