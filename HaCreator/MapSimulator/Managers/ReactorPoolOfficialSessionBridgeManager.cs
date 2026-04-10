using MapleLib.MapleCryptoLib;
using MapleLib.PacketLib;
using HaCreator.MapSimulator.Interaction;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Proxies a live Maple session and forwards CReactorPool::OnPacket reactor
    /// opcodes into the existing packet-owned reactor runtime seam.
    /// </summary>
    public sealed class ReactorPoolOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18499;
        public const short OutboundTouchReactorOpcode = 250;
        private const string DefaultProcessName = "MapleStory";

        private readonly ConcurrentQueue<ReactorPoolPacketInboxMessage> _pendingMessages = new();
        private readonly Queue<PendingTouchRequest> _pendingTouchRequests = new();
        private readonly object _sync = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;
        private BridgePair _activePair;

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

        private sealed class PendingTouchRequest
        {
            public PendingTouchRequest(int objectId, bool isTouching, byte[] packet)
            {
                ObjectId = objectId;
                IsTouching = isTouching;
                Packet = packet ?? Array.Empty<byte>();
            }

            public int ObjectId { get; }
            public bool IsTouching { get; }
            public byte[] Packet { get; }
        }

        public int ListenPort { get; private set; } = DefaultListenPort;
        public string RemoteHost { get; private set; } = IPAddress.Loopback.ToString();
        public int RemotePort { get; private set; }
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public bool HasAttachedClient => _activePair != null;
        public bool HasConnectedSession => _activePair?.InitCompleted == true;
        public int ReceivedCount { get; private set; }
        public int InjectedTouchRequestCount { get; private set; }
        public int QueuedTouchRequestCount
        {
            get
            {
                lock (_sync)
                {
                    return _pendingTouchRequests.Count;
                }
            }
        }
        public int? LastInjectedTouchObjectId { get; private set; }
        public bool? LastInjectedTouchFlag { get; private set; }
        public byte[] LastInjectedTouchPacket { get; private set; } = Array.Empty<byte>();
        public int? LastQueuedTouchObjectId { get; private set; }
        public bool? LastQueuedTouchFlag { get; private set; }
        public byte[] LastQueuedTouchPacket { get; private set; } = Array.Empty<byte>();
        public string LastStatus { get; private set; } = "Reactor official-session bridge inactive.";

        public string DescribeStatus()
        {
            string lifecycle = IsRunning
                ? $"listening on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}"
                : "inactive";
            string session = HasConnectedSession
                ? $"connected session {_activePair?.ClientEndpoint ?? "unknown-client"} -> {_activePair?.RemoteEndpoint ?? "unknown-remote"}"
                : "no active Maple session";
            string lastInjected = LastInjectedTouchPacket.Length > 0 && LastInjectedTouchObjectId.HasValue && LastInjectedTouchFlag.HasValue
                ? $" Last injected={LastInjectedTouchObjectId.Value}:{(LastInjectedTouchFlag.Value ? "enter" : "leave")} [{Convert.ToHexString(LastInjectedTouchPacket)}]."
                : string.Empty;
            string lastQueued = LastQueuedTouchPacket.Length > 0 && LastQueuedTouchObjectId.HasValue && LastQueuedTouchFlag.HasValue
                ? $" Last queued={LastQueuedTouchObjectId.Value}:{(LastQueuedTouchFlag.Value ? "enter" : "leave")} [{Convert.ToHexString(LastQueuedTouchPacket)}]."
                : string.Empty;
            return $"Reactor official-session bridge {lifecycle}; {session}; received={ReceivedCount}; touch injected={InjectedTouchRequestCount}; touch queued={QueuedTouchRequestCount}; inbound opcodes=334,335,336,337.{lastInjected}{lastQueued} {LastStatus}";
        }

        public bool TrySendTouchRequest(int objectId, bool isTouching, out string status)
        {
            if (objectId <= 0)
            {
                status = "Reactor touch injection requires a positive reactor object id.";
                return false;
            }

            lock (_sync)
            {
                BridgePair pair = _activePair;
                if (pair?.InitCompleted != true)
                {
                    status = "Reactor official-session bridge has no connected Maple session for touch injection.";
                    LastStatus = status;
                    return false;
                }

                RemoveQueuedTouchRequestsUnsafe(objectId);
                FlushQueuedTouchRequestsUnsafe(pair);
                byte[] packet = BuildTouchRequestPacket(objectId, isTouching);
                pair.ServerSession.SendPacket(packet);
                InjectedTouchRequestCount++;
                LastInjectedTouchObjectId = objectId;
                LastInjectedTouchFlag = isTouching;
                LastInjectedTouchPacket = packet;
                LastStatus = $"Injected reactor touch opcode {OutboundTouchReactorOpcode} for object {objectId} ({(isTouching ? "enter" : "leave")}) into live session {pair.RemoteEndpoint}.";
                status = LastStatus;
                return true;
            }
        }

        public bool TryQueueTouchRequest(int objectId, bool isTouching, out string status)
        {
            if (objectId <= 0)
            {
                status = "Reactor touch queue requires a positive reactor object id.";
                LastStatus = status;
                return false;
            }

            byte[] packet = BuildTouchRequestPacket(objectId, isTouching);
            lock (_sync)
            {
                EnqueueOrReplaceTouchRequestUnsafe(new PendingTouchRequest(objectId, isTouching, packet));
            }

            LastQueuedTouchObjectId = objectId;
            LastQueuedTouchFlag = isTouching;
            LastQueuedTouchPacket = packet;
            status = $"Queued reactor touch opcode {OutboundTouchReactorOpcode} for object {objectId} ({(isTouching ? "enter" : "leave")}) until a live Maple session is attached.";
            LastStatus = status;
            return true;
        }

        public bool HasQueuedTouchRequest(int objectId, bool isTouching)
        {
            lock (_sync)
            {
                return _pendingTouchRequests.Any(packet => packet.ObjectId == objectId && packet.IsTouching == isTouching);
            }
        }

        public bool TryRemoveQueuedTouchRequests(int objectId, out string status)
        {
            if (objectId <= 0)
            {
                status = "Reactor touch queue removal requires a positive reactor object id.";
                LastStatus = status;
                return false;
            }

            int removedCount;
            lock (_sync)
            {
                removedCount = RemoveQueuedTouchRequestsUnsafe(objectId);
            }

            status = removedCount > 0
                ? $"Removed {removedCount} queued reactor touch opcode {OutboundTouchReactorOpcode} request(s) for object {objectId}."
                : $"No queued reactor touch opcode {OutboundTouchReactorOpcode} requests were pending for object {objectId}.";
            LastStatus = status;
            return removedCount > 0;
        }

        public int ClearQueuedTouchRequests()
        {
            lock (_sync)
            {
                int removedCount = _pendingTouchRequests.Count;
                _pendingTouchRequests.Clear();
                if (removedCount > 0)
                {
                    LastStatus = $"Cleared {removedCount} queued reactor touch opcode {OutboundTouchReactorOpcode} request(s).";
                }

                return removedCount;
            }
        }

        public bool WasLastInjectedTouchRequest(int objectId, bool isTouching)
        {
            return LastInjectedTouchObjectId == objectId && LastInjectedTouchFlag == isTouching;
        }

        internal static byte[] BuildTouchRequestPacket(int objectId, bool isTouching)
        {
            byte[] packet = new byte[7];
            BitConverter.GetBytes(OutboundTouchReactorOpcode).CopyTo(packet, 0);
            BitConverter.GetBytes(objectId).CopyTo(packet, 2);
            packet[6] = isTouching ? (byte)1 : (byte)0;
            return packet;
        }

        public void Start(int listenPort, string remoteHost, int remotePort)
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);

                try
                {
                    ListenPort = listenPort <= 0 ? DefaultListenPort : listenPort;
                    RemoteHost = string.IsNullOrWhiteSpace(remoteHost) ? IPAddress.Loopback.ToString() : remoteHost.Trim();
                    RemotePort = remotePort;
                    _listenerCancellation = new CancellationTokenSource();
                    _listener = new TcpListener(IPAddress.Loopback, ListenPort);
                    _listener.Start();
                    _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                    LastStatus = $"Reactor official-session bridge listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    LastStatus = $"Reactor official-session bridge failed to start: {ex.Message}";
                }
            }
        }

        public bool TryRefreshFromDiscovery(int listenPort, int remotePort, string processSelector, int? localPort, out string status)
        {
            if (HasAttachedClient)
            {
                status = $"Reactor official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
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

            var candidates = CoconutOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            if (!TryResolveDiscoveryCandidate(candidates, remotePort, owningProcessId, owningProcessName, localPort, out var candidate, out status))
            {
                LastStatus = status;
                return false;
            }

            if (MatchesDiscoveredTargetConfiguration(
                    IsRunning,
                    ListenPort,
                    RemoteHost,
                    RemotePort,
                    resolvedListenPort,
                    candidate.RemoteEndpoint))
            {
                status = $"Reactor official-session bridge remains armed for {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}.";
                LastStatus = status;
                return true;
            }

            Start(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port);
            status = $"Reactor official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {LastStatus}";
            LastStatus = status;
            return true;
        }

        public string DescribeDiscoveredSessions(int remotePort, string processSelector = null, int? localPort = null)
        {
            if (!TryResolveProcessSelector(processSelector, out int? owningProcessId, out string owningProcessName, out string selectorError))
            {
                return selectorError;
            }

            var candidates = CoconutOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            var filteredCandidates = FilterCandidatesByLocalPort(candidates, localPort);
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
                LastStatus = "Reactor official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out ReactorPoolPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(string source, bool success, string detail)
        {
            string summary = string.IsNullOrWhiteSpace(detail) ? "reactor opcode" : detail;
            LastStatus = success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.";
        }

        internal static bool TryCreateBridgeMessageFromRawPacket(byte[] rawPacket, string source, out ReactorPoolPacketInboxMessage message, out string error)
        {
            message = null;
            error = null;

            if (!ReactorPoolPacketInboxManager.TryDecodeOpcodeFramedPacket(rawPacket, out int packetType, out byte[] payload, out error))
            {
                return false;
            }

            if (!IsBridgeOpcode(packetType))
            {
                error = $"Unsupported reactor bridge opcode {packetType}.";
                return false;
            }

            message = new ReactorPoolPacketInboxMessage(
                packetType,
                payload,
                string.IsNullOrWhiteSpace(source) ? "official-session:unknown-remote" : source,
                $"packetclientraw {Convert.ToHexString(rawPacket)}");
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
                    _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
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
                LastStatus = $"Reactor official-session bridge error: {ex.Message}";
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            BridgePair pair = null;

            try
            {
                lock (_sync)
                {
                    if (_activePair != null)
                    {
                        LastStatus = "Rejected reactor official-session client because a live Maple session is already attached.";
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
                clientSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Reactor official-session client disconnected: {pair.ClientEndpoint}.");
                serverSession.OnPacketReceived += (packet, isInit) => HandleServerPacket(pair, packet, isInit);
                serverSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Reactor official-session server disconnected: {pair.RemoteEndpoint}.");

                lock (_sync)
                {
                    _activePair = pair;
                }

                LastStatus = $"Reactor official-session bridge connected {pair.ClientEndpoint} -> {pair.RemoteEndpoint}. Waiting for Maple init packet.";
                serverSession.WaitForDataNoEncryption();
            }
            catch (Exception ex)
            {
                client.Close();
                pair?.Close();
                LastStatus = $"Reactor official-session bridge connect failed: {ex.Message}";
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
                    LastStatus = $"Reactor official-session bridge initialized Maple crypto for {pair.ClientEndpoint} <-> {pair.RemoteEndpoint}.";
                    FlushQueuedTouchRequests(pair);
                    pair.ClientSession.WaitForData();
                    return;
                }

                pair.ClientSession.SendPacket((byte[])raw.Clone());

                if (!TryCreateBridgeMessageFromRawPacket(raw, $"official-session:{pair.RemoteEndpoint}", out ReactorPoolPacketInboxMessage message, out _))
                {
                    return;
                }

                _pendingMessages.Enqueue(message);
                ReceivedCount++;
                LastStatus = $"Queued {ReactorPoolPacketInboxManager.DescribePacketType(message.PacketType)} from live session {pair.RemoteEndpoint}.";
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Reactor official-session server handling failed: {ex.Message}");
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

                pair.ServerSession.SendPacket(packet.ToArray());
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Reactor official-session client handling failed: {ex.Message}");
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

                ReceivedCount = 0;
            }
        }

        private void FlushQueuedTouchRequests(BridgePair pair)
        {
            lock (_sync)
            {
                FlushQueuedTouchRequestsUnsafe(pair);
            }
        }

        private void FlushQueuedTouchRequestsUnsafe(BridgePair pair)
        {
            if (pair?.InitCompleted != true)
            {
                return;
            }

            int flushed = 0;
            while (_pendingTouchRequests.Count > 0)
            {
                try
                {
                    PendingTouchRequest pending = _pendingTouchRequests.Peek();
                    pair.ServerSession.SendPacket(pending.Packet);
                    PendingTouchRequest dequeued = _pendingTouchRequests.Dequeue();

                    InjectedTouchRequestCount++;
                    flushed++;
                    LastInjectedTouchObjectId = dequeued.ObjectId;
                    LastInjectedTouchFlag = dequeued.IsTouching;
                    LastInjectedTouchPacket = dequeued.Packet;
                }
                catch (Exception ex)
                {
                    ClearActivePair(pair, $"Reactor official-session touch flush failed: {ex.Message}");
                    break;
                }
            }

            if (flushed > 0)
            {
                LastStatus = $"Flushed {flushed} queued reactor touch request(s) into live session {pair.RemoteEndpoint}.";
            }
        }

        private int RemoveQueuedTouchRequestsUnsafe(int objectId)
        {
            if (objectId <= 0 || _pendingTouchRequests.Count == 0)
            {
                return 0;
            }

            int removedCount = 0;
            int pendingCount = _pendingTouchRequests.Count;
            for (int i = 0; i < pendingCount; i++)
            {
                PendingTouchRequest pending = _pendingTouchRequests.Dequeue();
                if (pending.ObjectId != objectId)
                {
                    _pendingTouchRequests.Enqueue(pending);
                }
                else
                {
                    removedCount++;
                }
            }

            return removedCount;
        }

        private void EnqueueOrReplaceTouchRequestUnsafe(PendingTouchRequest next)
        {
            RemoveQueuedTouchRequestsUnsafe(next.ObjectId);
            _pendingTouchRequests.Enqueue(next);
        }

        private static MapleCrypto CreateCrypto(byte[] iv, short version)
        {
            return new MapleCrypto((byte[])iv.Clone(), version);
        }

        private static bool IsBridgeOpcode(int packetType)
        {
            return packetType == (int)PacketReactorPoolPacketKind.ChangeState
                || packetType == (int)PacketReactorPoolPacketKind.Move
                || packetType == (int)PacketReactorPoolPacketKind.EnterField
                || packetType == (int)PacketReactorPoolPacketKind.LeaveField;
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
                error = "Reactor official-session discovery requires a process name or pid when a selector is provided.";
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

        private static bool MatchesDiscoveredTargetConfiguration(
            bool isRunning,
            int currentListenPort,
            string currentRemoteHost,
            int currentRemotePort,
            int expectedListenPort,
            IPEndPoint expectedRemoteEndpoint)
        {
            if (!isRunning || expectedRemoteEndpoint == null)
            {
                return false;
            }

            if (currentListenPort != expectedListenPort || currentRemotePort != expectedRemoteEndpoint.Port)
            {
                return false;
            }

            return IPAddress.TryParse(currentRemoteHost, out IPAddress currentRemoteAddress)
                && currentRemoteAddress.Equals(expectedRemoteEndpoint.Address);
        }

        private static bool TryResolveDiscoveryCandidate(
            System.Collections.Generic.IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates,
            int remotePort,
            int? owningProcessId,
            string owningProcessName,
            int? localPort,
            out CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate candidate,
            out string status)
        {
            var filteredCandidates = FilterCandidatesByLocalPort(candidates, localPort);
            if (filteredCandidates.Count == 0)
            {
                status = $"Reactor official-session discovery found no established TCP session for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
                candidate = default;
                return false;
            }

            if (filteredCandidates.Count > 1)
            {
                string matches = string.Join(", ", filteredCandidates.Select(match =>
                    $"{match.RemoteEndpoint.Address}:{match.RemoteEndpoint.Port} via {match.LocalEndpoint.Address}:{match.LocalEndpoint.Port}"));
                status = $"Reactor official-session discovery found multiple candidates for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}: {matches}. Use /reactorpacket session discover to inspect them, or add a localPort filter.";
                candidate = default;
                return false;
            }

            candidate = filteredCandidates[0];
            status = null;
            return true;
        }

        private static System.Collections.Generic.IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> FilterCandidatesByLocalPort(
            System.Collections.Generic.IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates,
            int? localPort)
        {
            if (!localPort.HasValue)
            {
                return candidates ?? Array.Empty<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate>();
            }

            return (candidates ?? Array.Empty<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate>())
                .Where(candidate => candidate.LocalEndpoint.Port == localPort.Value)
                .ToArray();
        }

        private static string DescribeDiscoveryScope(int? owningProcessId, string owningProcessName, int remotePort, int? localPort)
        {
            string selectorLabel = owningProcessId.HasValue
                ? $"pid {owningProcessId.Value}"
                : string.IsNullOrWhiteSpace(owningProcessName)
                    ? "the selected process"
                    : $"process '{owningProcessName}'";
            return localPort.HasValue
                ? $"{selectorLabel} on remote port {remotePort} and local port {localPort.Value}"
                : $"{selectorLabel} on remote port {remotePort}";
        }
    }
}
