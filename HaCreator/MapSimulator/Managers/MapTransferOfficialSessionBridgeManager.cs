using MapleLib.MapleCryptoLib;
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

        private sealed class BridgePair
        {
            public BridgePair(TcpClient clientTcpClient, TcpClient serverTcpClient, Session clientSession, Session serverSession)
            {
                ClientTcpClient = clientTcpClient;
                ServerTcpClient = serverTcpClient;
                ClientSession = clientSession;
                ServerSession = serverSession;
                RemoteEndpoint = serverTcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown-remote";
                ClientEndpoint = clientTcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown-client";
            }

            public TcpClient ClientTcpClient { get; }
            public TcpClient ServerTcpClient { get; }
            public Session ClientSession { get; }
            public Session ServerSession { get; }
            public string RemoteEndpoint { get; }
            public string ClientEndpoint { get; }
            public short Version { get; set; }
            public bool InitCompleted { get; set; }

            public void Close()
            {
                try
                {
                    ClientTcpClient.Close();
                }
                catch
                {
                }

                try
                {
                    ServerTcpClient.Close();
                }
                catch
                {
                }
            }
        }

        private readonly ConcurrentQueue<MapTransferPacketInboxMessage> _pendingMessages = new();
        private readonly List<ObservedOutboundRequest> _observedOutboundRequests = new();
        private readonly object _sync = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;
        private BridgePair _activePair;

        public int ListenPort { get; private set; } = DefaultListenPort;
        public string RemoteHost { get; private set; } = IPAddress.Loopback.ToString();
        public int RemotePort { get; private set; }
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public bool HasConnectedSession => _activePair?.InitCompleted == true;
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

        public string DescribeStatus()
        {
            string lifecycle = IsRunning
                ? $"listening on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}"
                : "inactive";
            string session = HasConnectedSession
                ? $"connected session {_activePair?.ClientEndpoint ?? "unknown-client"} -> {_activePair?.RemoteEndpoint ?? "unknown-remote"}"
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
                if (_activePair?.InitCompleted == true && targetChanged)
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
                    _listenerCancellation = new CancellationTokenSource();
                    _listener = new TcpListener(IPAddress.Loopback, ListenPort);
                    _listener.Start();
                    _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                    status = $"Map transfer official-session bridge listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
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

            request = SnapshotObservedOutboundRegisterMapId(request);

            lock (_sync)
            {
                _observedOutboundRequests.Add(new ObservedOutboundRequest
                {
                    Request = request,
                    Source = string.IsNullOrWhiteSpace(source) ? "official-session" : source,
                    RawText = rawPacket == null ? string.Empty : Convert.ToHexString(rawPacket)
                });
            }

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

        public void RecordDispatchResult(string source, bool success, string detail)
        {
            string summary = string.IsNullOrWhiteSpace(detail) ? "map transfer result" : detail;
            LastStatus = success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.";
        }

        public bool TrySendRequest(MapTransferRuntimeRequest request, out string status)
        {
            BridgePair pair;
            lock (_sync)
            {
                pair = _activePair;
            }

            if (pair == null || !pair.InitCompleted)
            {
                status = "Map transfer official-session bridge has no connected Maple session for outbound injection.";
                LastStatus = status;
                return false;
            }

            byte[] rawPacket = MapTransferPacketCodec.BuildRawRequestPacket(request);
            try
            {
                pair.ServerSession.SendPacket(rawPacket);
                SentCount++;
                status = $"Injected map transfer request into live session {pair.RemoteEndpoint}.";
                LastStatus = status;
                return true;
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Map transfer official-session outbound injection failed: {ex.Message}");
                status = LastStatus;
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

        private async Task ListenLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _listener != null)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                    _ = Task.Run(() => AcceptClientAsync(client, cancellationToken), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                LastStatus = $"Map transfer official-session bridge error: {ex.Message}";
            }
        }

        private async Task AcceptClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            BridgePair pair = null;
            try
            {
                lock (_sync)
                {
                    if (_activePair != null)
                    {
                        LastStatus = "Rejected map transfer official-session client because a live Maple session is already attached.";
                        client.Close();
                        return;
                    }
                }

                TcpClient server = new();
                await server.ConnectAsync(RemoteHost, RemotePort, cancellationToken).ConfigureAwait(false);

                Session clientSession = new(client.Client, SessionType.SERVER_TO_CLIENT);
                Session serverSession = new(server.Client, SessionType.CLIENT_TO_SERVER);
                pair = new BridgePair(client, server, clientSession, serverSession);

                clientSession.OnPacketReceived += (packet, isInit) => HandleClientPacket(pair, packet, isInit);
                clientSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Map transfer official-session client disconnected: {pair.ClientEndpoint}.");
                serverSession.OnPacketReceived += (packet, isInit) => HandleServerPacket(pair, packet, isInit);
                serverSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Map transfer official-session server disconnected: {pair.RemoteEndpoint}.");

                lock (_sync)
                {
                    _activePair = pair;
                }

                LastStatus = $"Map transfer official-session bridge connected {pair.ClientEndpoint} -> {pair.RemoteEndpoint}. Waiting for Maple init packet.";
                serverSession.WaitForDataNoEncryption();
            }
            catch (Exception ex)
            {
                client.Close();
                pair?.Close();
                LastStatus = $"Map transfer official-session bridge connect failed: {ex.Message}";
            }
        }

        private void HandleServerPacket(BridgePair pair, PacketReader packet, bool isInit)
        {
            try
            {
                byte[] raw = packet.ToArray();
                if (isInit)
                {
                    PacketReader initReader = new(raw);
                    initReader.ReadShort();
                    pair.Version = initReader.ReadShort();
                    string patchLocation = initReader.ReadMapleString();
                    byte[] clientSendIv = initReader.ReadBytes(4);
                    byte[] clientReceiveIv = initReader.ReadBytes(4);
                    byte serverType = initReader.ReadByte();

                    pair.ClientSession.SIV = CreateCrypto(clientReceiveIv, pair.Version);
                    pair.ClientSession.RIV = CreateCrypto(clientSendIv, pair.Version);
                    pair.ClientSession.SendInitialPacket(pair.Version, patchLocation, clientSendIv, clientReceiveIv, serverType);
                    pair.InitCompleted = true;
                    LastStatus = $"Map transfer official-session bridge initialized Maple crypto for {pair.ClientEndpoint} <-> {pair.RemoteEndpoint}.";
                    pair.ClientSession.WaitForData();
                    return;
                }

                pair.ClientSession.SendPacket((byte[])raw.Clone());

                if (!MapTransferPacketCodec.TryDecodeInboundResultPacket(raw, out byte[] payload, out _))
                {
                    return;
                }

                _pendingMessages.Enqueue(new MapTransferPacketInboxMessage(
                    payload,
                    $"official-session:{pair.RemoteEndpoint}",
                    $"packetclientraw {Convert.ToHexString(raw)}"));
                ReceivedCount++;
                LastStatus = $"Queued map transfer result from live session {pair.RemoteEndpoint}.";
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Map transfer official-session server handling failed: {ex.Message}");
            }
        }

        private void HandleClientPacket(BridgePair pair, PacketReader packet, bool isInit)
        {
            try
            {
                if (isInit)
                {
                    return;
                }

                byte[] rawPacket = packet.ToArray();
                pair.ServerSession.SendPacket(rawPacket);
                TryObserveOutboundRequestPacket(rawPacket, $"official-session:{pair.ClientEndpoint}", out _);
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Map transfer official-session client handling failed: {ex.Message}");
            }
        }

        private void ClearActivePair(BridgePair pair, string status)
        {
            lock (_sync)
            {
                if (_activePair != pair)
                {
                    return;
                }

                _activePair = null;
                LastStatus = status;
            }

            pair?.Close();
        }

        private void StopInternal(bool clearPending)
        {
            try
            {
                _listenerCancellation?.Cancel();
            }
            catch
            {
            }

            try
            {
                _listener?.Stop();
            }
            catch
            {
            }

            try
            {
                _listenerTask?.Wait(100);
            }
            catch
            {
            }

            BridgePair pair;
            lock (_sync)
            {
                pair = _activePair;
                _activePair = null;
            }

            pair?.Close();

            _listenerTask = null;
            _listener = null;
            _listenerCancellation?.Dispose();
            _listenerCancellation = null;

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

        private static MapleCrypto CreateCrypto(byte[] iv, short version)
        {
            return new MapleCrypto((byte[])iv.Clone(), version);
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
