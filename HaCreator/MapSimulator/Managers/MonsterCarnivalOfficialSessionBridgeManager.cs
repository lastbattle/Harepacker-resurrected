using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using HaCreator.MapSimulator.Fields;
using MapleLib.MapleCryptoLib;
using MapleLib.PacketLib;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Built-in Monster Carnival transport bridge that proxies a live Maple
    /// session and feeds inbound Monster Carnival packets into the existing
    /// packet-owned runtime seam.
    /// </summary>
    public sealed class MonsterCarnivalOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18488;
        private const string DefaultProcessName = "MapleStory";
        private const int AddressFamilyInet = 2;
        private const int ErrorInsufficientBuffer = 122;
        private const int FirstCarnivalOpcode = 346;
        private const int LastCarnivalOpcode = 353;
        internal const int OutboundRequestOpcode = 262;
        private const int RecentPacketCapacity = 8;

        private readonly ConcurrentQueue<MonsterCarnivalPacketInboxMessage> _pendingMessages = new();
        private readonly ConcurrentQueue<PendingRequest> _pendingOutboundRequests = new();
        private readonly ConcurrentDictionary<int, int> _opcodeMappings = new();
        private readonly Queue<string> _recentPackets = new();
        private readonly object _sync = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;
        private BridgePair _activePair;
        private SessionDiscoveryCandidate? _passiveEstablishedSession;

        private sealed record PendingRequest(MonsterCarnivalTab Tab, int EntryIndex, byte[] RawPacket);

        public readonly record struct SessionDiscoveryCandidate(
            int ProcessId,
            string ProcessName,
            IPEndPoint LocalEndpoint,
            IPEndPoint RemoteEndpoint);

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

        public int ListenPort { get; private set; } = DefaultListenPort;
        public string RemoteHost { get; private set; } = IPAddress.Loopback.ToString();
        public int RemotePort { get; private set; }
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public bool HasAttachedClient => _activePair != null;
        public bool HasPassiveEstablishedSocketPair => _passiveEstablishedSession.HasValue && _activePair == null;
        public bool HasConnectedSession => _activePair?.InitCompleted == true;
        public int PendingPacketCount => _pendingOutboundRequests.Count;
        public int ReceivedCount { get; private set; }
        public int SentCount { get; private set; }
        public int QueuedCount { get; private set; }
        public string LastStatus { get; private set; } = "Monster Carnival official-session bridge inactive.";

        public string DescribeStatus()
        {
            string lifecycle = IsRunning
                ? $"listening on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}"
                : "inactive";
            string session = HasConnectedSession
                ? $"connected session {_activePair?.ClientEndpoint ?? "unknown-client"} -> {_activePair?.RemoteEndpoint ?? "unknown-remote"}"
                : HasPassiveEstablishedSocketPair
                    ? DescribePassiveEstablishedSession(_passiveEstablishedSession.Value)
                : "no active Maple session";
            return $"Monster Carnival official-session bridge {lifecycle}; {session}; attachMode=proxy+passive-observe; received={ReceivedCount}; sent={SentCount}; pending={PendingPacketCount}; queued={QueuedCount}; mappings={DescribePacketMappings()}; recent={DescribeRecentPackets()}. {LastStatus}";
        }

        public static IReadOnlyList<SessionDiscoveryCandidate> DiscoverEstablishedSessions(
            int remotePort,
            int? owningProcessId = null,
            string owningProcessName = null)
        {
            if (remotePort <= 0)
            {
                return Array.Empty<SessionDiscoveryCandidate>();
            }

            List<SessionDiscoveryCandidate> candidates = new();
            foreach (TcpRowOwnerPid row in EnumerateTcpRows())
            {
                if (row.state != (uint)TcpState.Established)
                {
                    continue;
                }

                int localPort = DecodePort(row.localPort);
                int resolvedRemotePort = DecodePort(row.remotePort);
                if (localPort <= 0 || resolvedRemotePort != remotePort)
                {
                    continue;
                }

                if (!TryResolveProcess(row.owningPid, out string processName))
                {
                    continue;
                }

                if (owningProcessId.HasValue && row.owningPid != owningProcessId.Value)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(owningProcessName)
                    && !string.Equals(processName, NormalizeProcessSelector(owningProcessName), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                IPAddress localAddress = DecodeAddress(row.localAddr);
                IPAddress remoteAddress = DecodeAddress(row.remoteAddr);
                if (IPAddress.Any.Equals(remoteAddress) || IPAddress.None.Equals(remoteAddress))
                {
                    continue;
                }

                candidates.Add(new SessionDiscoveryCandidate(
                    row.owningPid,
                    processName,
                    new IPEndPoint(localAddress, localPort),
                    new IPEndPoint(remoteAddress, resolvedRemotePort)));
            }

            return candidates
                .OrderBy(candidate => candidate.ProcessName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.ProcessId)
                .ThenBy(candidate => candidate.LocalEndpoint.Port)
                .ToArray();
        }

        public bool TryStart(int listenPort, string remoteHost, int remotePort, out string status)
        {
            lock (_sync)
            {
                bool autoSelectListenPort = listenPort <= 0;
                int requestedListenPort = autoSelectListenPort ? DefaultListenPort : listenPort;
                string resolvedRemoteHost = NormalizeRemoteHost(remoteHost);
                if (HasAttachedClient)
                {
                    if (MatchesRequestedTargetConfiguration(ListenPort, RemoteHost, RemotePort, requestedListenPort, resolvedRemoteHost, remotePort, autoSelectListenPort))
                    {
                        status = $"Monster Carnival official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                        LastStatus = status;
                        return true;
                    }

                    status = $"Monster Carnival official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                    LastStatus = status;
                    return false;
                }

                if (IsRunning
                    && MatchesRequestedTargetConfiguration(ListenPort, RemoteHost, RemotePort, requestedListenPort, resolvedRemoteHost, remotePort, autoSelectListenPort))
                {
                    status = $"Monster Carnival official-session bridge already listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                    LastStatus = status;
                    return true;
                }

                StopInternal(clearPending: true);

                try
                {
                    RemoteHost = resolvedRemoteHost;
                    RemotePort = remotePort;
                    _listenerCancellation = new CancellationTokenSource();
                    _listener = new TcpListener(IPAddress.Loopback, autoSelectListenPort ? 0 : requestedListenPort);
                    _listener.Start();
                    ListenPort = (_listener.LocalEndpoint as IPEndPoint)?.Port ?? requestedListenPort;
                    _passiveEstablishedSession = null;
                    _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                    LastStatus = $"Monster Carnival official-session bridge listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                    status = LastStatus;
                    return true;
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    LastStatus = $"Monster Carnival official-session bridge failed to start: {ex.Message}";
                    status = LastStatus;
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
            int? owningProcessId = null;
            string owningProcessName = null;
            if (!TryResolveProcessSelector(processSelector, out owningProcessId, out owningProcessName, out string selectorError))
            {
                status = selectorError;
                LastStatus = status;
                return false;
            }

            IReadOnlyList<SessionDiscoveryCandidate> candidates = DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            if (!TryResolveDiscoveryCandidate(candidates, remotePort, owningProcessId, owningProcessName, localPort, out SessionDiscoveryCandidate candidate, out status))
            {
                LastStatus = status;
                return false;
            }

            bool autoSelectListenPort = listenPort <= 0;
            int requestedListenPort = autoSelectListenPort ? DefaultListenPort : listenPort;
            if (HasAttachedClient)
            {
                if (MatchesDiscoveredTargetConfiguration(ListenPort, RemoteHost, RemotePort, requestedListenPort, candidate.RemoteEndpoint, autoSelectListenPort))
                {
                    status = $"Monster Carnival official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                    LastStatus = status;
                    return true;
                }

                status = $"Monster Carnival official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                LastStatus = status;
                return false;
            }

            if (IsRunning
                && MatchesDiscoveredTargetConfiguration(ListenPort, RemoteHost, RemotePort, requestedListenPort, candidate.RemoteEndpoint, autoSelectListenPort))
            {
                status = $"Monster Carnival official-session bridge already listens on 127.0.0.1:{ListenPort} and remains armed for discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}.";
                LastStatus = status;
                return true;
            }

            if (!TryStart(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, out string startStatus))
            {
                status = $"Monster Carnival official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}, but startup failed. {startStatus}";
                LastStatus = status;
                return false;
            }

            status = $"Monster Carnival official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {startStatus}";
            LastStatus = status;
            return true;
        }

        public bool TryAttachEstablishedSession(int remotePort, string processSelector, int? localPort, out string status)
        {
            int? owningProcessId = null;
            string owningProcessName = null;
            if (!TryResolveProcessSelector(processSelector, out owningProcessId, out owningProcessName, out string selectorError))
            {
                status = selectorError;
                LastStatus = status;
                return false;
            }

            IReadOnlyList<SessionDiscoveryCandidate> candidates = DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            if (!TryResolveDiscoveryCandidate(candidates, remotePort, owningProcessId, owningProcessName, localPort, out SessionDiscoveryCandidate candidate, out status))
            {
                LastStatus = status;
                return false;
            }

            return TryAttachEstablishedSession(candidate, out status);
        }

        public bool TryAttachEstablishedSession(SessionDiscoveryCandidate candidate, out string status)
        {
            if (candidate.LocalEndpoint == null || candidate.RemoteEndpoint == null || candidate.RemoteEndpoint.Port <= 0)
            {
                status = "Monster Carnival official-session attach requires an established Maple client socket pair.";
                LastStatus = status;
                return false;
            }

            lock (_sync)
            {
                if (HasAttachedClient)
                {
                    status = $"Monster Carnival official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before observing an already-established socket pair.";
                    LastStatus = status;
                    return false;
                }

                StopInternal(clearPending: true);
                _passiveEstablishedSession = candidate;
                RemoteHost = candidate.RemoteEndpoint.Address.ToString();
                RemotePort = candidate.RemoteEndpoint.Port;
                LastStatus = $"Observed already-established Monster Carnival Maple socket pair {candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}. This passive attach can keep the CField_MonsterCarnival session target visible, but it cannot decrypt inbound 346-353 traffic or inject outbound opcode {OutboundRequestOpcode} after the Maple handshake; reconnect through the localhost proxy for live Monster Carnival packet ownership.";
                status = LastStatus;
                return true;
            }
        }

        public string DescribeDiscoveredSessions(int remotePort, string processSelector = null, int? localPort = null)
        {
            int? owningProcessId = null;
            string owningProcessName = null;
            if (!TryResolveProcessSelector(processSelector, out owningProcessId, out owningProcessName, out string selectorError))
            {
                return selectorError;
            }

            IReadOnlyList<SessionDiscoveryCandidate> candidates = DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            return DescribeDiscoveryCandidates(candidates, remotePort, owningProcessId, owningProcessName, localPort);
        }

        public bool TryConfigurePacketMapping(int opcode, int packetType, out string status)
        {
            if (opcode <= 0)
            {
                status = "Monster Carnival opcode mappings require a positive opcode.";
                return false;
            }

            if (packetType < FirstCarnivalOpcode || packetType > LastCarnivalOpcode)
            {
                status = $"Monster Carnival packet mappings only accept raw packet types {FirstCarnivalOpcode}-{LastCarnivalOpcode}.";
                return false;
            }

            _opcodeMappings[opcode] = packetType;
            status = $"Mapped Monster Carnival opcode {opcode} to {DescribePacketType(packetType)}.";
            LastStatus = status;
            return true;
        }

        public bool RemovePacketMapping(int opcode, out string status)
        {
            if (_opcodeMappings.TryRemove(opcode, out int packetType))
            {
                status = $"Removed Monster Carnival opcode {opcode} mapping for {DescribePacketType(packetType)}.";
                LastStatus = status;
                return true;
            }

            status = $"Monster Carnival opcode {opcode} is not currently mapped.";
            return false;
        }

        public void ClearPacketMappings()
        {
            _opcodeMappings.Clear();
            LastStatus = "Cleared Monster Carnival official-session opcode mappings.";
        }

        public string DescribePacketMappings()
        {
            if (_opcodeMappings.IsEmpty)
            {
                return "none";
            }

            return string.Join(
                ", ",
                _opcodeMappings
                    .OrderBy(entry => entry.Key)
                    .Select(entry => $"{entry.Key}->{DescribePacketType(entry.Value)}"));
        }

        public string DescribeRecentPackets()
        {
            lock (_sync)
            {
                if (_recentPackets.Count == 0)
                {
                    return "none";
                }

                return string.Join(" | ", _recentPackets);
            }
        }

        public void Stop()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
                LastStatus = "Monster Carnival official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out MonsterCarnivalPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public bool TrySendRequest(MonsterCarnivalTab tab, int entryIndex, out string status)
        {
            if (entryIndex < 0)
            {
                status = $"Monster Carnival request index must be non-negative, got {entryIndex}.";
                LastStatus = status;
                return false;
            }

            BridgePair pair = _activePair;
            if (HasPassiveEstablishedSocketPair)
            {
                status = $"Monster Carnival official-session bridge is observing {DescribePassiveEstablishedSession(_passiveEstablishedSession.Value)}. It cannot inject opcode {OutboundRequestOpcode} into an already-established Maple socket pair after the handshake; reconnect through the localhost proxy first.";
                LastStatus = status;
                return false;
            }

            if (pair == null || !pair.InitCompleted)
            {
                status = "Monster Carnival official-session bridge has no active Maple session.";
                LastStatus = status;
                return false;
            }

            try
            {
                byte[] rawPacket = BuildRequestPacket(tab, entryIndex);
                pair.ServerSession.SendPacket((byte[])rawPacket.Clone());
                SentCount++;
                RecordRecentPacket(OutboundRequestOpcode, rawPacket, OutboundRequestOpcode, $"inject-request tab={(int)tab} index={entryIndex}");
                status = $"Injected Monster Carnival opcode {OutboundRequestOpcode} (tab={(int)tab}, index={entryIndex}) into live session {pair.RemoteEndpoint}.";
                LastStatus = status;
                return true;
            }
            catch (Exception ex)
            {
                status = $"Monster Carnival official-session injection failed: {ex.Message}";
                LastStatus = status;
                ClearActivePair(pair, status);
                return false;
            }
        }

        public bool TrySendOrQueueRequest(MonsterCarnivalTab tab, int entryIndex, out bool queued, out string status)
        {
            queued = false;
            if (HasConnectedSession)
            {
                return TrySendRequest(tab, entryIndex, out status);
            }

            if (!TryQueueRequest(tab, entryIndex, out status))
            {
                return false;
            }

            queued = true;
            return true;
        }

        public bool TryQueueRequest(MonsterCarnivalTab tab, int entryIndex, out string status)
        {
            if (entryIndex < 0)
            {
                status = $"Monster Carnival request index must be non-negative, got {entryIndex}.";
                LastStatus = status;
                return false;
            }

            if (HasPassiveEstablishedSocketPair && !IsRunning)
            {
                status = $"Monster Carnival official-session bridge is observing {DescribePassiveEstablishedSession(_passiveEstablishedSession.Value)}. Deferred opcode {OutboundRequestOpcode} queueing only applies to sessions that reconnect through the localhost proxy.";
                LastStatus = status;
                return false;
            }

            if (!IsRunning && !HasAttachedClient)
            {
                status = "Monster Carnival official-session bridge is not armed for deferred live-session injection.";
                LastStatus = status;
                return false;
            }

            byte[] rawPacket = BuildRequestPacket(tab, entryIndex);
            _pendingOutboundRequests.Enqueue(new PendingRequest(tab, entryIndex, rawPacket));
            QueuedCount++;
            RecordRecentPacket(OutboundRequestOpcode, rawPacket, OutboundRequestOpcode, $"queue-request tab={(int)tab} index={entryIndex}");
            status = HasPassiveEstablishedSocketPair
                ? $"Queued Monster Carnival opcode {OutboundRequestOpcode} (tab={(int)tab}, index={entryIndex}) for the proxied reconnect handshake."
                : $"Queued Monster Carnival opcode {OutboundRequestOpcode} (tab={(int)tab}, index={entryIndex}) for deferred live-session injection.";
            LastStatus = status;
            return true;
        }

        public void RecordDispatchResult(string source, int packetType, bool success, string message)
        {
            string packetLabel = packetType is >= FirstCarnivalOpcode and <= LastCarnivalOpcode
                ? $"Monster Carnival opcode {packetType}"
                : packetType == SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode
                    ? $"CField::OnPacket relay {packetType}"
                : $"Monster Carnival packet {packetType}";
            string summary = string.IsNullOrWhiteSpace(message) ? packetLabel : $"{packetLabel}: {message}";
            LastStatus = success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.";
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
                LastStatus = $"Monster Carnival official-session bridge error: {ex.Message}";
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
                        LastStatus = "Rejected Monster Carnival official-session client because a live Maple session is already attached.";
                        client.Close();
                        return;
                    }
                }

                TcpClient server = new TcpClient();
                await server.ConnectAsync(RemoteHost, RemotePort, cancellationToken).ConfigureAwait(false);

                Session clientSession = new Session(client.Client, SessionType.SERVER_TO_CLIENT);
                Session serverSession = new Session(server.Client, SessionType.CLIENT_TO_SERVER);
                pair = new BridgePair(client, server, clientSession, serverSession);

                clientSession.OnPacketReceived += (packet, isInit) => HandleClientPacket(pair, packet, isInit);
                clientSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Monster Carnival official-session client disconnected: {pair.ClientEndpoint}.");
                serverSession.OnPacketReceived += (packet, isInit) => HandleServerPacket(pair, packet, isInit);
                serverSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Monster Carnival official-session server disconnected: {pair.RemoteEndpoint}.");

                lock (_sync)
                {
                    _activePair = pair;
                    _passiveEstablishedSession = null;
                }

                LastStatus = $"Monster Carnival official-session bridge connected {pair.ClientEndpoint} -> {pair.RemoteEndpoint}. Waiting for Maple init packet.";
                serverSession.WaitForDataNoEncryption();
            }
            catch (Exception ex)
            {
                client.Close();
                pair?.Close();
                LastStatus = $"Monster Carnival official-session bridge connect failed: {ex.Message}";
            }
        }

        private void HandleServerPacket(BridgePair pair, PacketReader packet, bool isInit)
        {
            try
            {
                byte[] raw = packet.ToArray();
                if (isInit)
                {
                    PacketReader initReader = new PacketReader(raw);
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
                    int flushed = FlushQueuedRequests(pair);
                    LastStatus = flushed > 0
                        ? $"Monster Carnival official-session bridge initialized Maple crypto for {pair.ClientEndpoint} <-> {pair.RemoteEndpoint} and flushed {flushed} queued request(s)."
                        : $"Monster Carnival official-session bridge initialized Maple crypto for {pair.ClientEndpoint} <-> {pair.RemoteEndpoint}.";
                    pair.ClientSession.WaitForData();
                    return;
                }

                pair.ClientSession.SendPacket((byte[])raw.Clone());

                if (!TryMapInboundPacket(raw, $"official-session:{pair.RemoteEndpoint}", out MonsterCarnivalPacketInboxMessage message))
                {
                    return;
                }

                _pendingMessages.Enqueue(message);
                ReceivedCount++;
                LastStatus = $"Queued Monster Carnival opcode {message.PacketType} ({DescribePacketType(message.PacketType)}) from live session {pair.RemoteEndpoint}.";
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Monster Carnival official-session server handling failed: {ex.Message}");
            }
        }

        public bool TryMapInboundPacket(byte[] rawPacket, string source, out MonsterCarnivalPacketInboxMessage message)
        {
            message = null;
            if (rawPacket == null || rawPacket.Length < sizeof(short))
            {
                return false;
            }

            int opcode = BitConverter.ToUInt16(rawPacket, 0);
            byte[] payload = rawPacket.Skip(sizeof(short)).ToArray();
            if (opcode == SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode)
            {
                if (!SpecialFieldRuntimeCoordinator.TryDecodeCurrentWrapperRelayPayload(payload, out int relayedPacketType, out _, out string relayError)
                    || !IsSupportedCarnivalPacketType(relayedPacketType))
                {
                    RecordRecentPacket(opcode, rawPacket, mappedPacketType: null, "unsupported-relay");
                    LastStatus = string.IsNullOrWhiteSpace(relayError)
                        ? $"Ignored CField::OnPacket relay {opcode}; relayed packet is not in the recovered Monster Carnival 346-353 handler family."
                        : $"Ignored CField::OnPacket relay {opcode}; {relayError}";
                    return false;
                }

                message = new MonsterCarnivalPacketInboxMessage(
                    opcode,
                    payload,
                    source,
                    $"packetraw {Convert.ToHexString(rawPacket)}");
                RecordRecentPacket(opcode, rawPacket, relayedPacketType, "current-wrapper");
                return true;
            }

            if (opcode >= FirstCarnivalOpcode && opcode <= LastCarnivalOpcode)
            {
                int relayedPacketType = SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode;
                payload = SpecialFieldRuntimeCoordinator.BuildCurrentWrapperRelayPayload(opcode, payload);
                message = new MonsterCarnivalPacketInboxMessage(
                    relayedPacketType,
                    payload,
                    source,
                    $"packetraw {Convert.ToHexString(rawPacket)}");
                RecordRecentPacket(opcode, rawPacket, opcode, "native-relay");
                return true;
            }

            if (!_opcodeMappings.TryGetValue(opcode, out int mappedPacketType))
            {
                RecordRecentPacket(opcode, rawPacket, mappedPacketType: null, "unmapped");
                LastStatus = $"Ignored unmapped Monster Carnival opcode {opcode}; add /mcarnival session map <opcode> <rawPacketType> to route it into the recovered 346-353 seam.";
                return false;
            }

            int relayOpcode = SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode;
            payload = SpecialFieldRuntimeCoordinator.BuildCurrentWrapperRelayPayload(mappedPacketType, payload);
            message = new MonsterCarnivalPacketInboxMessage(
                relayOpcode,
                payload,
                source,
                $"packetraw {Convert.ToHexString(rawPacket)}");
            RecordRecentPacket(opcode, rawPacket, mappedPacketType, "configured-relay");
            return true;
        }

        private void HandleClientPacket(BridgePair pair, PacketReader packet, bool isInit)
        {
            if (isInit)
            {
                return;
            }

            try
            {
                byte[] raw = packet.ToArray();
                pair.ServerSession.SendPacket((byte[])raw.Clone());

                if (TryDecodeOutboundRequestPacket(raw, out int tab, out int entryIndex))
                {
                    RecordRecentPacket(OutboundRequestOpcode, raw, OutboundRequestOpcode, $"outbound-request tab={tab} index={entryIndex}");
                    LastStatus = $"Forwarded live Monster Carnival request opcode {OutboundRequestOpcode} (tab={tab}, index={entryIndex}) from {pair.ClientEndpoint} to {pair.RemoteEndpoint}.";
                }
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Monster Carnival official-session client handling failed: {ex.Message}");
            }
        }

        private void ClearActivePair(BridgePair pair, string status)
        {
            if (pair == null)
            {
                return;
            }

            lock (_sync)
            {
                if (!ReferenceEquals(_activePair, pair))
                {
                    return;
                }

                _activePair = null;
            }

            pair.Close();
            LastStatus = status;
        }

        private void StopInternal(bool clearPending)
        {
            _listenerCancellation?.Cancel();

            try
            {
                _listener?.Stop();
            }
            catch
            {
            }

            _listenerTask = null;
            _listener = null;
            _listenerCancellation?.Dispose();
            _listenerCancellation = null;

            BridgePair pair = _activePair;
            _activePair = null;
            _passiveEstablishedSession = null;
            pair?.Close();

            if (clearPending)
            {
                while (_pendingMessages.TryDequeue(out _))
                {
                }

                while (_pendingOutboundRequests.TryDequeue(out _))
                {
                }

                SentCount = 0;
                QueuedCount = 0;

                ReceivedCount = 0;
                lock (_sync)
                {
                    _recentPackets.Clear();
                }
            }
        }

        private int FlushQueuedRequests(BridgePair pair)
        {
            if (pair == null || !pair.InitCompleted)
            {
                return 0;
            }

            int flushed = 0;
            while (_pendingOutboundRequests.TryDequeue(out PendingRequest pending))
            {
                pair.ServerSession.SendPacket((byte[])pending.RawPacket.Clone());
                SentCount++;
                flushed++;
                RecordRecentPacket(OutboundRequestOpcode, pending.RawPacket, OutboundRequestOpcode, $"flush-request tab={(int)pending.Tab} index={pending.EntryIndex}");
            }

            return flushed;
        }

        private static byte[] BuildRequestPacket(MonsterCarnivalTab tab, int entryIndex)
        {
            byte[] rawPacket = new byte[sizeof(short) + sizeof(byte) + sizeof(int)];
            BitConverter.GetBytes((short)OutboundRequestOpcode).CopyTo(rawPacket, 0);
            rawPacket[sizeof(short)] = (byte)tab;
            BitConverter.GetBytes(entryIndex).CopyTo(rawPacket, sizeof(short) + sizeof(byte));
            return rawPacket;
        }

        private static MapleCrypto CreateCrypto(byte[] iv, short version)
        {
            return new MapleCrypto((byte[])iv.Clone(), version);
        }

        internal static bool TryDecodeInboundCarnivalPacket(byte[] rawPacket, string source, out MonsterCarnivalPacketInboxMessage message)
        {
            message = null;
            if (rawPacket == null || rawPacket.Length < sizeof(short))
            {
                return false;
            }

            int opcode = BitConverter.ToUInt16(rawPacket, 0);
            byte[] payload = rawPacket.Skip(sizeof(short)).ToArray();
            if (opcode == SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode)
            {
                if (!SpecialFieldRuntimeCoordinator.TryDecodeCurrentWrapperRelayPayload(payload, out int relayedPacketType, out _, out _)
                    || !IsSupportedCarnivalPacketType(relayedPacketType))
                {
                    return false;
                }

                message = new MonsterCarnivalPacketInboxMessage(
                    opcode,
                    payload,
                    source,
                    $"packetraw {Convert.ToHexString(rawPacket)}");
                return true;
            }

            if (!IsSupportedCarnivalPacketType(opcode))
            {
                return false;
            }

            byte[] relayPayload = SpecialFieldRuntimeCoordinator.BuildCurrentWrapperRelayPayload(opcode, payload);
            message = new MonsterCarnivalPacketInboxMessage(
                SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode,
                relayPayload,
                source,
                $"packetraw {Convert.ToHexString(rawPacket)}");
            return true;
        }

        internal static bool TryDecodeOutboundRequestPacket(byte[] rawPacket, out int tab, out int entryIndex)
        {
            tab = 0;
            entryIndex = 0;
            if (rawPacket == null || rawPacket.Length != sizeof(short) + sizeof(byte) + sizeof(int))
            {
                return false;
            }

            int opcode = BitConverter.ToUInt16(rawPacket, 0);
            if (opcode != OutboundRequestOpcode)
            {
                return false;
            }

            tab = rawPacket[sizeof(short)];
            entryIndex = BitConverter.ToInt32(rawPacket, sizeof(short) + sizeof(byte));
            return tab >= 0
                && tab <= (int)MonsterCarnivalTab.Guardian
                && entryIndex >= 0;
        }

        private static bool IsSupportedCarnivalPacketType(int packetType)
        {
            return packetType >= FirstCarnivalOpcode
                && packetType <= LastCarnivalOpcode;
        }

        private static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
                OutboundRequestOpcode => "requestsend",
                SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode => $"CField::OnPacket relay {SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode}",
                346 => "enter",
                347 => "personalcp",
                348 => "teamcp",
                349 => "requestresult",
                350 => "requestfailure",
                351 => "processfordeath",
                352 => "memberout",
                353 => "gameresult",
                _ => $"packet {packetType}"
            };
        }

        private void RecordRecentPacket(int opcode, byte[] rawPacket, int? mappedPacketType, string detail = null)
        {
            string summary = mappedPacketType.HasValue
                ? $"{opcode}->{DescribePacketType(mappedPacketType.Value)}[{detail ?? "configured"}]:{Convert.ToHexString(rawPacket)}"
                : $"{opcode}:{detail ?? "unmapped"}:{Convert.ToHexString(rawPacket)}";

            lock (_sync)
            {
                _recentPackets.Enqueue(summary);
                while (_recentPackets.Count > RecentPacketCapacity)
                {
                    _recentPackets.Dequeue();
                }
            }
        }

        private static IEnumerable<TcpRowOwnerPid> EnumerateTcpRows()
        {
            int bufferSize = 0;
            int result = GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, sort: true, AddressFamilyInet, TcpTableClass.TcpTableOwnerPidAll, 0);
            if (result != ErrorInsufficientBuffer || bufferSize <= 0)
            {
                yield break;
            }

            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                result = GetExtendedTcpTable(buffer, ref bufferSize, sort: true, AddressFamilyInet, TcpTableClass.TcpTableOwnerPidAll, 0);
                if (result != 0)
                {
                    yield break;
                }

                int rowCount = Marshal.ReadInt32(buffer);
                IntPtr rowPtr = IntPtr.Add(buffer, sizeof(int));
                int rowSize = Marshal.SizeOf<TcpRowOwnerPid>();
                for (int i = 0; i < rowCount; i++)
                {
                    yield return Marshal.PtrToStructure<TcpRowOwnerPid>(rowPtr);
                    rowPtr = IntPtr.Add(rowPtr, rowSize);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static IPAddress DecodeAddress(uint address)
        {
            return new IPAddress(BitConverter.GetBytes(address));
        }

        private static int DecodePort(byte[] portBytes)
        {
            return portBytes == null || portBytes.Length < 2
                ? 0
                : (portBytes[0] << 8) | portBytes[1];
        }

        private static bool TryResolveProcess(int pid, out string processName)
        {
            processName = null;
            try
            {
                processName = Process.GetProcessById(pid).ProcessName;
                return !string.IsNullOrWhiteSpace(processName);
            }
            catch
            {
                return false;
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
                error = "Monster Carnival official-session discovery requires a process name or pid when a selector is provided.";
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

        private static string NormalizeRemoteHost(string remoteHost)
        {
            string trimmed = string.IsNullOrWhiteSpace(remoteHost)
                ? IPAddress.Loopback.ToString()
                : remoteHost.Trim();
            return IPAddress.TryParse(trimmed, out IPAddress parsedAddress)
                ? parsedAddress.ToString()
                : trimmed;
        }

        private static bool MatchesTargetConfiguration(
            int currentListenPort,
            string currentRemoteHost,
            int currentRemotePort,
            int requestedListenPort,
            string requestedRemoteHost,
            int requestedRemotePort)
        {
            return currentListenPort == requestedListenPort
                && currentRemotePort == requestedRemotePort
                && string.Equals(
                    NormalizeRemoteHost(currentRemoteHost),
                    NormalizeRemoteHost(requestedRemoteHost),
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static bool MatchesRequestedTargetConfiguration(
            int currentListenPort,
            string currentRemoteHost,
            int currentRemotePort,
            int requestedListenPort,
            string requestedRemoteHost,
            int requestedRemotePort,
            bool ignoreListenPort)
        {
            return (ignoreListenPort || currentListenPort == requestedListenPort)
                && currentRemotePort == requestedRemotePort
                && string.Equals(
                    NormalizeRemoteHost(currentRemoteHost),
                    NormalizeRemoteHost(requestedRemoteHost),
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static bool MatchesDiscoveredTargetConfiguration(
            int currentListenPort,
            string currentRemoteHost,
            int currentRemotePort,
            int requestedListenPort,
            IPEndPoint candidateRemoteEndpoint,
            bool ignoreListenPort = false)
        {
            if (candidateRemoteEndpoint == null)
            {
                return false;
            }

            return MatchesRequestedTargetConfiguration(
                currentListenPort,
                currentRemoteHost,
                currentRemotePort,
                requestedListenPort,
                candidateRemoteEndpoint.Address.ToString(),
                candidateRemoteEndpoint.Port,
                ignoreListenPort);
        }

        private static string DescribeSelector(int? owningProcessId, string owningProcessName)
        {
            return owningProcessId.HasValue
                ? $"pid {owningProcessId.Value}"
                : string.IsNullOrWhiteSpace(owningProcessName)
                    ? "the selected process"
                    : $"process '{owningProcessName}'";
        }

        internal static bool TryResolveDiscoveryCandidate(
            IReadOnlyList<SessionDiscoveryCandidate> candidates,
            int remotePort,
            int? owningProcessId,
            string owningProcessName,
            int? localPort,
            out SessionDiscoveryCandidate candidate,
            out string status)
        {
            IReadOnlyList<SessionDiscoveryCandidate> filteredCandidates = FilterCandidatesByLocalPort(candidates, localPort);
            if (filteredCandidates.Count == 0)
            {
                status = $"Monster Carnival official-session discovery found no established TCP session for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
                candidate = default;
                return false;
            }

            if (filteredCandidates.Count > 1)
            {
                string matches = string.Join(", ", filteredCandidates.Select(candidate =>
                    $"{candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} via {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}"));
                status = $"Monster Carnival official-session discovery found multiple candidates for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}: {matches}. Use /mcarnival session discover to inspect them, or add a localPort filter.";
                candidate = default;
                return false;
            }

            candidate = filteredCandidates[0];
            status = null;
            return true;
        }

        internal static string DescribeDiscoveryCandidates(
            IReadOnlyList<SessionDiscoveryCandidate> candidates,
            int remotePort,
            int? owningProcessId,
            string owningProcessName,
            int? localPort)
        {
            IReadOnlyList<SessionDiscoveryCandidate> filteredCandidates = FilterCandidatesByLocalPort(candidates, localPort);
            if (filteredCandidates.Count == 0)
            {
                return $"No established TCP sessions matched {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
            }

            return string.Join(
                Environment.NewLine,
                filteredCandidates.Select(candidate =>
                    $"{candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}"));
        }

        private static string DescribePassiveEstablishedSession(SessionDiscoveryCandidate candidate)
        {
            return $"observing established socket pair {candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}; proxy reconnect required for decrypt/inject";
        }

        private static IReadOnlyList<SessionDiscoveryCandidate> FilterCandidatesByLocalPort(
            IReadOnlyList<SessionDiscoveryCandidate> candidates,
            int? localPort)
        {
            if (!localPort.HasValue)
            {
                return candidates ?? Array.Empty<SessionDiscoveryCandidate>();
            }

            return (candidates ?? Array.Empty<SessionDiscoveryCandidate>())
                .Where(candidate => candidate.LocalEndpoint.Port == localPort.Value)
                .ToArray();
        }

        private static string DescribeDiscoveryScope(int? owningProcessId, string owningProcessName, int remotePort, int? localPort)
        {
            string selectorLabel = DescribeSelector(owningProcessId, owningProcessName);
            return localPort.HasValue
                ? $"{selectorLabel} on remote port {remotePort} and local port {localPort.Value}"
                : $"{selectorLabel} on remote port {remotePort}";
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TcpRowOwnerPid
        {
            public uint state;
            public uint localAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] localPort;
            public uint remoteAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] remotePort;
            public int owningPid;
        }

        private enum TcpTableClass
        {
            TcpTableBasicListener,
            TcpTableBasicConnections,
            TcpTableBasicAll,
            TcpTableOwnerPidListener,
            TcpTableOwnerPidConnections,
            TcpTableOwnerPidAll,
            TcpTableOwnerModuleListener,
            TcpTableOwnerModuleConnections,
            TcpTableOwnerModuleAll
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern int GetExtendedTcpTable(
            IntPtr pTcpTable,
            ref int pdwSize,
            [MarshalAs(UnmanagedType.Bool)] bool sort,
            int ipVersion,
            TcpTableClass tableClass,
            uint reserved);
    }
}
