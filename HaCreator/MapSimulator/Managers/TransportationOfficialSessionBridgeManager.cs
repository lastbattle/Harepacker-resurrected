using MapleLib.MapleCryptoLib;
using MapleLib.PacketLib;
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

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Built-in transport bridge that proxies a live Maple session and feeds
    /// decrypted CField_ContiMove packets into the existing transport dispatcher.
    /// </summary>
    public sealed class TransportationOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18491;
        private const string DefaultProcessName = "MapleStory";
        private const int AddressFamilyInet = 2;
        private const int ErrorInsufficientBuffer = 122;
        private const int MaxRecentOutboundPackets = 32;

        private readonly ConcurrentQueue<TransportationPacketInboxMessage> _pendingMessages = new();
        private readonly ConcurrentQueue<PendingOutboundPacket> _pendingOutboundPackets = new();
        private readonly object _sync = new();
        private readonly Queue<OutboundPacketTrace> _recentOutboundPackets = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;
        private BridgePair _activePair;

        public readonly record struct SessionDiscoveryCandidate(
            int ProcessId,
            string ProcessName,
            IPEndPoint LocalEndpoint,
            IPEndPoint RemoteEndpoint);
        public readonly record struct OutboundPacketTrace(
            int Opcode,
            int PayloadLength,
            string PayloadHex,
            string RawPacketHex,
            string Source);
        private sealed record PendingOutboundPacket(int Opcode, byte[] RawPacket);

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
        public bool HasConnectedSession => _activePair?.InitCompleted == true;
        public int ReceivedCount { get; private set; }
        public int SentCount { get; private set; }
        public int ForwardedOutboundCount { get; private set; }
        public int ForwardedOutboundTransportCount { get; private set; }
        public int QueuedCount { get; private set; }
        public int LastSentOpcode { get; private set; } = -1;
        public byte[] LastSentRawPacket { get; private set; } = Array.Empty<byte>();
        public int LastQueuedOpcode { get; private set; } = -1;
        public byte[] LastQueuedRawPacket { get; private set; } = Array.Empty<byte>();
        public int PendingPacketCount => _pendingOutboundPackets.Count;
        public string LastStatus { get; private set; } = "Transport official-session bridge inactive.";

        public string DescribeStatus()
        {
            string lifecycle = IsRunning
                ? $"listening on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}"
                : "inactive";
            string session = HasConnectedSession
                ? $"connected session {_activePair?.ClientEndpoint ?? "unknown-client"} -> {_activePair?.RemoteEndpoint ?? "unknown-remote"}"
                : "no active Maple session";
            string lastOutbound = LastSentOpcode >= 0
                ? $" lastOut={DescribeOutboundPacket(LastSentOpcode, LastSentRawPacket)}[{Convert.ToHexString(LastSentRawPacket)}]."
                : string.Empty;
            string lastQueued = LastQueuedOpcode >= 0
                ? $" lastQueued={DescribeOutboundPacket(LastQueuedOpcode, LastQueuedRawPacket)}[{Convert.ToHexString(LastQueuedRawPacket)}]."
                : string.Empty;
            return $"Transport official-session bridge {lifecycle}; {session}; attachMode=proxy-only; received={ReceivedCount}; sent={SentCount}; pending={PendingPacketCount}; queued={QueuedCount}; forwardedOutbound={ForwardedOutboundCount}; forwardedOutboundTransport={ForwardedOutboundTransportCount}; inbound opcodes=164,165; outbound opcode={TransportationFieldInitRequestCodec.OutboundFieldInitOpcode}.{lastOutbound}{lastQueued} {LastStatus}";
        }

        public string DescribeRecentOutboundPackets(int maxCount = 10)
        {
            int normalizedCount = Math.Clamp(maxCount, 1, MaxRecentOutboundPackets);
            lock (_sync)
            {
                if (_recentOutboundPackets.Count == 0)
                {
                    return "Transport official-session bridge outbound history is empty.";
                }

                OutboundPacketTrace[] entries = _recentOutboundPackets
                    .Reverse()
                    .Take(normalizedCount)
                    .ToArray();
                return "Transport official-session bridge outbound history:"
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        entries.Select(entry =>
                            $"{DescribeOutboundPacket(entry.Opcode, Convert.FromHexString(entry.RawPacketHex))} payloadLen={entry.PayloadLength} source={entry.Source} payloadHex={entry.PayloadHex} raw={entry.RawPacketHex}"));
            }
        }

        public string ClearRecentOutboundPackets()
        {
            lock (_sync)
            {
                _recentOutboundPackets.Clear();
            }

            LastStatus = "Transport official-session bridge outbound history cleared.";
            return LastStatus;
        }

        public bool TryReplayRecentOutboundPacket(int historyIndexFromNewest, out string status)
        {
            if (!TryResolveRecentOutboundPacket(historyIndexFromNewest, "replay", out byte[] rawPacket, out string resolveStatus))
            {
                status = resolveStatus;
                LastStatus = status;
                return false;
            }

            return TrySendRawPacket(rawPacket, out status);
        }

        public bool TryQueueRecentOutboundPacket(int historyIndexFromNewest, out string status)
        {
            if (!TryResolveRecentOutboundPacket(historyIndexFromNewest, "queue", out byte[] rawPacket, out string resolveStatus))
            {
                status = resolveStatus;
                LastStatus = status;
                return false;
            }

            return TryQueueRawPacket(rawPacket, out status);
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
                int resolvedListenPort = listenPort <= 0 ? DefaultListenPort : listenPort;
                string resolvedRemoteHost = NormalizeRemoteHost(remoteHost);
                if (HasAttachedClient)
                {
                    if (MatchesTargetConfiguration(ListenPort, RemoteHost, RemotePort, resolvedListenPort, resolvedRemoteHost, remotePort))
                    {
                        status = $"Transport official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                        LastStatus = status;
                        return true;
                    }

                    status = $"Transport official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                    LastStatus = status;
                    return false;
                }

                if (IsRunning
                    && MatchesTargetConfiguration(ListenPort, RemoteHost, RemotePort, resolvedListenPort, resolvedRemoteHost, remotePort))
                {
                    status = $"Transport official-session bridge already listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                    LastStatus = status;
                    return true;
                }

                StopInternal(clearPending: true);

                try
                {
                    ListenPort = resolvedListenPort;
                    RemoteHost = resolvedRemoteHost;
                    RemotePort = remotePort;
                    _listenerCancellation = new CancellationTokenSource();
                    _listener = new TcpListener(IPAddress.Loopback, ListenPort);
                    _listener.Start();
                    _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                    LastStatus = $"Transport official-session bridge listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                    status = LastStatus;
                    return true;
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    LastStatus = $"Transport official-session bridge failed to start: {ex.Message}";
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

            int resolvedListenPort = listenPort <= 0 ? DefaultListenPort : listenPort;
            if (HasAttachedClient)
            {
                if (MatchesDiscoveredTargetConfiguration(ListenPort, RemoteHost, RemotePort, resolvedListenPort, candidate.RemoteEndpoint))
                {
                    status = $"Transport official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                    LastStatus = status;
                    return true;
                }

                status = $"Transport official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                LastStatus = status;
                return false;
            }

            if (IsRunning
                && MatchesDiscoveredTargetConfiguration(ListenPort, RemoteHost, RemotePort, resolvedListenPort, candidate.RemoteEndpoint))
            {
                status = $"Transport official-session bridge remains armed for {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}.";
                LastStatus = status;
                return true;
            }

            if (!TryStart(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, out string startStatus))
            {
                status = $"Transport official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}, but startup failed. {startStatus}";
                LastStatus = status;
                return false;
            }

            status = $"Transport official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {startStatus} {BuildDiscoveryAttachmentRequirementMessage(resolvedListenPort)}";
            LastStatus = status;
            return true;
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

        public void Stop()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
                LastStatus = "Transport official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out TransportationPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public bool TrySendFieldInitRequest(int fieldId, int shipKind, out string status)
        {
            if (!TransportationFieldInitRequestCodec.IsSupportedShipKind(shipKind))
            {
                status = $"Transport field-init request only supports ship kinds 0 and 1, but received {shipKind}.";
                LastStatus = status;
                return false;
            }

            byte[] rawPacket = TransportationFieldInitRequestCodec.BuildRawFieldInitPacket(fieldId, shipKind);
            if (!TrySendRawPacket(rawPacket, out status, countAsTypedSend: true))
            {
                return false;
            }

            status = $"Injected {TransportationFieldInitRequestCodec.DescribeFieldInitRequest(fieldId, shipKind)} into live session {_activePair?.RemoteEndpoint ?? "unknown-remote"}.";
            LastStatus = status;
            return true;
        }

        public bool TryQueueFieldInitRequest(int fieldId, int shipKind, out string status)
        {
            if (!TransportationFieldInitRequestCodec.IsSupportedShipKind(shipKind))
            {
                status = $"Transport field-init request only supports ship kinds 0 and 1, but received {shipKind}.";
                LastStatus = status;
                return false;
            }

            byte[] rawPacket = TransportationFieldInitRequestCodec.BuildRawFieldInitPacket(fieldId, shipKind);
            return TryQueueRawPacket(rawPacket, out status);
        }

        public bool TrySendRawPacket(byte[] rawPacket, out string status)
        {
            return TrySendRawPacket(rawPacket, out status, countAsTypedSend: true);
        }

        public bool TryQueueRawPacket(byte[] rawPacket, out string status)
        {
            if (!TryDecodeOpcode(rawPacket, out int opcode, out _))
            {
                status = "Transport outbound packet must include a 2-byte opcode.";
                LastStatus = status;
                return false;
            }

            byte[] clonedPacket = (byte[])rawPacket.Clone();
            _pendingOutboundPackets.Enqueue(new PendingOutboundPacket(opcode, clonedPacket));
            QueuedCount++;
            LastQueuedOpcode = opcode;
            LastQueuedRawPacket = clonedPacket;
            status = $"Queued outbound {DescribeOutboundPacket(opcode, clonedPacket)} for deferred live-session injection.";
            LastStatus = status;
            return true;
        }

        public void RecordDispatchResult(string source, TransportationPacketInboxMessage message, bool success, string result)
        {
            string summary = string.IsNullOrWhiteSpace(result)
                ? TransportationPacketInboxManager.DescribePacket(message?.PacketType ?? 0, message?.Payload)
                : $"{TransportationPacketInboxManager.DescribePacket(message?.PacketType ?? 0, message?.Payload)}: {result}";
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
                LastStatus = $"Transport official-session bridge error: {ex.Message}";
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
                        LastStatus = "Rejected Transport official-session client because a live Maple session is already attached.";
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
                clientSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Transport official-session client disconnected: {pair.ClientEndpoint}.");
                serverSession.OnPacketReceived += (packet, isInit) => HandleServerPacket(pair, packet, isInit);
                serverSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Transport official-session server disconnected: {pair.RemoteEndpoint}.");

                lock (_sync)
                {
                    _activePair = pair;
                }

                LastStatus = $"Transport official-session bridge connected {pair.ClientEndpoint} -> {pair.RemoteEndpoint}. Waiting for Maple init packet.";
                serverSession.WaitForDataNoEncryption();
            }
            catch (Exception ex)
            {
                client.Close();
                pair?.Close();
                LastStatus = $"Transport official-session bridge connect failed: {ex.Message}";
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
                    int flushed = FlushQueuedOutboundPackets(pair);
                    LastStatus = flushed > 0
                        ? $"Transport official-session bridge initialized Maple crypto for {pair.ClientEndpoint} <-> {pair.RemoteEndpoint} and flushed {flushed} queued outbound packet(s)."
                        : $"Transport official-session bridge initialized Maple crypto for {pair.ClientEndpoint} <-> {pair.RemoteEndpoint}.";
                    pair.ClientSession.WaitForData();
                    return;
                }

                pair.ClientSession.SendPacket((byte[])raw.Clone());

                if (!TryDecodeInboundTransportPacket(raw, $"official-session:{pair.RemoteEndpoint}", out TransportationPacketInboxMessage message))
                {
                    return;
                }

                _pendingMessages.Enqueue(message);
                ReceivedCount++;
                LastStatus = $"Queued {TransportationPacketInboxManager.DescribePacket(message.PacketType, message.Payload)} from live session {pair.RemoteEndpoint}.";
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Transport official-session server handling failed: {ex.Message}");
            }
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
                ForwardedOutboundCount++;

                if (TryDecodeOpcode(raw, out int opcode, out byte[] payload))
                {
                    bool isTransportOpcode = opcode == TransportationPacketInboxManager.PacketTypeContiMove
                        || opcode == TransportationPacketInboxManager.PacketTypeContiState
                        || opcode == TransportationFieldInitRequestCodec.OutboundFieldInitOpcode;
                    if (isTransportOpcode)
                    {
                        ForwardedOutboundTransportCount++;
                    }

                    RecordOutboundPacket(new OutboundPacketTrace(
                        opcode,
                        payload?.Length ?? 0,
                        BuildPayloadPreview(payload),
                        Convert.ToHexString(raw),
                        pair.ClientEndpoint));

                    if (isTransportOpcode)
                    {
                        LastStatus = $"Forwarded outbound {TransportationPacketInboxManager.DescribePacket(opcode, payload)} from {pair.ClientEndpoint} to {pair.RemoteEndpoint}.";
                    }
                }
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Transport official-session client handling failed: {ex.Message}");
            }
        }

        private void RecordOutboundPacket(OutboundPacketTrace trace)
        {
            lock (_sync)
            {
                while (_recentOutboundPackets.Count >= MaxRecentOutboundPackets)
                {
                    _recentOutboundPackets.Dequeue();
                }

                _recentOutboundPackets.Enqueue(trace);
            }
        }

        private bool TrySendRawPacket(byte[] rawPacket, out string status, bool countAsTypedSend = false)
        {
            BridgePair pair = _activePair;
            if (pair == null || !pair.InitCompleted)
            {
                status = "Transport official-session bridge has no active Maple session.";
                LastStatus = status;
                return false;
            }

            if (rawPacket == null || rawPacket.Length < sizeof(short))
            {
                status = "Transport outbound packet must include a 2-byte opcode.";
                LastStatus = status;
                return false;
            }

            try
            {
                byte[] clonedPacket = (byte[])rawPacket.Clone();
                int opcode = BitConverter.ToUInt16(clonedPacket, 0);
                byte[] payload = clonedPacket.Length > sizeof(short)
                    ? clonedPacket.Skip(sizeof(short)).ToArray()
                    : Array.Empty<byte>();

                pair.ServerSession.SendPacket(clonedPacket);
                RecordSentOutboundPacket(
                    opcode,
                    clonedPacket,
                    payload,
                    "transport-replay",
                    countAsTypedSend);

                status = $"Replayed outbound {DescribeOutboundPacket(opcode, clonedPacket)} to live session {pair.RemoteEndpoint}.";
                LastStatus = status;
                return true;
            }
            catch (Exception ex)
            {
                status = $"Transport outbound replay failed: {ex.Message}";
                LastStatus = status;
                return false;
            }
        }

        private bool TryResolveRecentOutboundPacket(
            int historyIndexFromNewest,
            string actionName,
            out byte[] rawPacket,
            out string status)
        {
            rawPacket = Array.Empty<byte>();
            status = null;

            if (historyIndexFromNewest <= 0)
            {
                status = $"Transport {actionName} index must be 1 or greater.";
                return false;
            }

            OutboundPacketTrace[] entries;
            lock (_sync)
            {
                if (_recentOutboundPackets.Count == 0)
                {
                    status = $"No captured transport outbound client packets are available to {actionName}.";
                    return false;
                }

                if (historyIndexFromNewest > _recentOutboundPackets.Count)
                {
                    status = $"Transport {actionName} index {historyIndexFromNewest} exceeds the {_recentOutboundPackets.Count} captured outbound packet(s).";
                    return false;
                }

                entries = _recentOutboundPackets.ToArray();
            }

            OutboundPacketTrace trace = entries[^historyIndexFromNewest];
            if (string.IsNullOrWhiteSpace(trace.RawPacketHex))
            {
                status = $"Captured transport outbound packet {historyIndexFromNewest} has no raw payload to {actionName}.";
                return false;
            }

            try
            {
                rawPacket = Convert.FromHexString(trace.RawPacketHex);
                return true;
            }
            catch (FormatException ex)
            {
                status = $"Captured transport outbound packet {historyIndexFromNewest} could not be used for {actionName}: {ex.Message}";
                rawPacket = Array.Empty<byte>();
                return false;
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
            pair?.Close();

            if (clearPending)
            {
                while (_pendingMessages.TryDequeue(out _))
                {
                }

                while (_pendingOutboundPackets.TryDequeue(out _))
                {
                }

                ReceivedCount = 0;
                SentCount = 0;
                ForwardedOutboundCount = 0;
                ForwardedOutboundTransportCount = 0;
                QueuedCount = 0;
                LastSentOpcode = -1;
                LastSentRawPacket = Array.Empty<byte>();
                LastQueuedOpcode = -1;
                LastQueuedRawPacket = Array.Empty<byte>();
                _recentOutboundPackets.Clear();
            }
        }

        private int FlushQueuedOutboundPackets(BridgePair pair)
        {
            if (pair == null || !pair.InitCompleted)
            {
                return 0;
            }

            int flushed = 0;
            while (_pendingOutboundPackets.TryPeek(out PendingOutboundPacket pending))
            {
                byte[] clonedPacket = (byte[])pending.RawPacket.Clone();
                pair.ServerSession.SendPacket(clonedPacket);
                if (!_pendingOutboundPackets.TryDequeue(out PendingOutboundPacket dequeued))
                {
                    break;
                }

                byte[] payload = clonedPacket.Length > sizeof(short)
                    ? clonedPacket.Skip(sizeof(short)).ToArray()
                    : Array.Empty<byte>();
                RecordSentOutboundPacket(
                    dequeued.Opcode,
                    clonedPacket,
                    payload,
                    "transport-queued",
                    countAsTypedSend: true);
                flushed++;
            }

            return flushed;
        }

        private void RecordSentOutboundPacket(
            int opcode,
            byte[] rawPacket,
            byte[] payload,
            string source,
            bool countAsTypedSend)
        {
            if (countAsTypedSend)
            {
                SentCount++;
                LastSentOpcode = opcode;
                LastSentRawPacket = rawPacket;
            }

            ForwardedOutboundCount++;
            if (opcode == TransportationPacketInboxManager.PacketTypeContiMove
                || opcode == TransportationPacketInboxManager.PacketTypeContiState
                || opcode == TransportationFieldInitRequestCodec.OutboundFieldInitOpcode)
            {
                ForwardedOutboundTransportCount++;
            }

            RecordOutboundPacket(new OutboundPacketTrace(
                opcode,
                payload?.Length ?? 0,
                BuildPayloadPreview(payload),
                Convert.ToHexString(rawPacket),
                source));
        }

        private static MapleCrypto CreateCrypto(byte[] iv, short version)
        {
            return new MapleCrypto((byte[])iv.Clone(), version);
        }

        public static bool TryDecodeInboundTransportPacket(byte[] rawPacket, string source, out TransportationPacketInboxMessage message)
        {
            message = null;
            if (!TryDecodeOpcode(rawPacket, out int opcode, out byte[] payload))
            {
                return false;
            }

            if (opcode != TransportationPacketInboxManager.PacketTypeContiMove
                && opcode != TransportationPacketInboxManager.PacketTypeContiState)
            {
                return false;
            }

            message = new TransportationPacketInboxMessage(opcode, payload, source, BitConverter.ToString(rawPacket).Replace("-", string.Empty, StringComparison.Ordinal));
            return true;
        }

        private static bool TryDecodeOpcode(byte[] rawPacket, out int opcode, out byte[] payload)
        {
            opcode = 0;
            payload = Array.Empty<byte>();
            if (rawPacket == null || rawPacket.Length < sizeof(short))
            {
                return false;
            }

            opcode = BitConverter.ToUInt16(rawPacket, 0);
            payload = new byte[rawPacket.Length - sizeof(short)];
            if (payload.Length > 0)
            {
                Buffer.BlockCopy(rawPacket, sizeof(short), payload, 0, payload.Length);
            }

            return true;
        }

        private static string BuildPayloadPreview(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return "<none>";
            }

            const int maxPreviewBytes = 24;
            byte[] previewBytes = payload.Length <= maxPreviewBytes
                ? payload
                : payload.Take(maxPreviewBytes).ToArray();
            string previewHex = Convert.ToHexString(previewBytes);
            return payload.Length <= maxPreviewBytes
                ? previewHex
                : $"{previewHex}...";
        }

        private static string DescribeOutboundPacket(int opcode, byte[] rawPacket)
        {
            if (opcode == TransportationFieldInitRequestCodec.OutboundFieldInitOpcode)
            {
                return TransportationFieldInitRequestCodec.DescribeRawFieldInitPacket(rawPacket);
            }

            if (TryDecodeOpcode(rawPacket, out int decodedOpcode, out byte[] payload))
            {
                return TransportationPacketInboxManager.DescribePacket(decodedOpcode, payload);
            }

            return $"opcode={opcode}";
        }

        public static bool TryResolveDiscoveryCandidate(
            IReadOnlyList<SessionDiscoveryCandidate> candidates,
            int remotePort,
            int? owningProcessId,
            string owningProcessName,
            int? localPort,
            out SessionDiscoveryCandidate candidate,
            out string status)
        {
            candidate = default;
            status = null;

            if (candidates == null || candidates.Count == 0)
            {
                status = $"No established Maple session matched remote port {remotePort}."
                    + FormatOwnershipSuffix(owningProcessId, owningProcessName)
                    + FormatLocalPortSuffix(localPort);
                return false;
            }

            IEnumerable<SessionDiscoveryCandidate> filteredCandidates = candidates;
            if (localPort.HasValue)
            {
                filteredCandidates = filteredCandidates.Where(entry => entry.LocalEndpoint.Port == localPort.Value);
            }

            SessionDiscoveryCandidate[] resolvedCandidates = filteredCandidates.ToArray();
            if (resolvedCandidates.Length == 1)
            {
                candidate = resolvedCandidates[0];
                return true;
            }

            if (resolvedCandidates.Length == 0)
            {
                status = $"No established Maple session matched remote port {remotePort}."
                    + FormatOwnershipSuffix(owningProcessId, owningProcessName)
                    + FormatLocalPortSuffix(localPort);
                return false;
            }

            status = "Transport official-session bridge found multiple candidates for remote port "
                + remotePort
                + FormatOwnershipSuffix(owningProcessId, owningProcessName)
                + FormatLocalPortSuffix(localPort)
                + ": "
                + string.Join(", ", resolvedCandidates.Select(entry =>
                    $"{entry.ProcessName} ({entry.ProcessId}) local {entry.LocalEndpoint.Address}:{entry.LocalEndpoint.Port} -> remote {entry.RemoteEndpoint.Address}:{entry.RemoteEndpoint.Port}"))
                + ". Specify a local port filter.";
            return false;
        }

        public static string DescribeDiscoveryCandidates(
            IReadOnlyList<SessionDiscoveryCandidate> candidates,
            int remotePort,
            int? owningProcessId,
            string owningProcessName,
            int? localPort)
        {
            if (!TryResolveDiscoveryCandidate(candidates, remotePort, owningProcessId, owningProcessName, localPort, out SessionDiscoveryCandidate candidate, out string status))
            {
                if (candidates == null || candidates.Count == 0 || status != null)
                {
                    return status ?? $"No established Maple session matched remote port {remotePort}.";
                }
            }

            IEnumerable<SessionDiscoveryCandidate> filteredCandidates = candidates ?? Array.Empty<SessionDiscoveryCandidate>();
            if (localPort.HasValue)
            {
                filteredCandidates = filteredCandidates.Where(entry => entry.LocalEndpoint.Port == localPort.Value);
            }

            SessionDiscoveryCandidate[] entries = filteredCandidates.ToArray();
            if (entries.Length == 0)
            {
                return $"No established Maple session matched remote port {remotePort}."
                    + FormatOwnershipSuffix(owningProcessId, owningProcessName)
                    + FormatLocalPortSuffix(localPort);
            }

            return "Transport official-session bridge discovery candidates:"
                + Environment.NewLine
                + string.Join(
                    Environment.NewLine,
                    entries.Select(entry =>
                        $"{entry.ProcessName} ({entry.ProcessId}) local {entry.LocalEndpoint.Address}:{entry.LocalEndpoint.Port} -> remote {entry.RemoteEndpoint.Address}:{entry.RemoteEndpoint.Port}"))
                + Environment.NewLine
                + BuildDiscoveryAttachmentRequirementMessage();
        }

        private static string BuildDiscoveryAttachmentRequirementMessage(int? listenPort = null)
        {
            string reconnectTarget = listenPort.HasValue && listenPort.Value > 0
                ? $"127.0.0.1:{listenPort.Value}"
                : "the configured localhost listen port";
            return $"Discovery identifies established Maple sockets, but transport live-session attach is still proxy-only: Maple must reconnect through {reconnectTarget} so the bridge can recover the init packet and Maple crypto instead of attaching in place to the already-established socket.";
        }

        private static string FormatOwnershipSuffix(int? owningProcessId, string owningProcessName)
        {
            if (owningProcessId.HasValue)
            {
                return $" for pid {owningProcessId.Value}";
            }

            if (!string.IsNullOrWhiteSpace(owningProcessName))
            {
                return $" for process '{NormalizeProcessSelector(owningProcessName)}'";
            }

            return string.Empty;
        }

        private static string FormatLocalPortSuffix(int? localPort)
        {
            return localPort.HasValue ? $" on local port {localPort.Value}" : string.Empty;
        }

        private static bool TryResolveProcessSelector(string selector, out int? processId, out string processName, out string error)
        {
            processId = null;
            processName = null;
            error = null;

            if (string.IsNullOrWhiteSpace(selector))
            {
                processName = DefaultProcessName;
                return true;
            }

            string normalized = selector.Trim();
            if (int.TryParse(normalized, out int parsedPid))
            {
                processId = parsedPid;
                return true;
            }

            processName = NormalizeProcessSelector(normalized);
            return true;
        }

        private static string NormalizeProcessSelector(string processSelector)
        {
            if (string.IsNullOrWhiteSpace(processSelector))
            {
                return DefaultProcessName;
            }

            string normalized = processSelector.Trim();
            return normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? normalized[..^4]
                : normalized;
        }

        private static bool TryResolveProcess(int processId, out string processName)
        {
            processName = null;
            try
            {
                processName = Process.GetProcessById(processId).ProcessName;
                return !string.IsNullOrWhiteSpace(processName);
            }
            catch
            {
                return false;
            }
        }

        internal static bool MatchesDiscoveredTargetConfiguration(
            int currentListenPort,
            string currentRemoteHost,
            int currentRemotePort,
            int expectedListenPort,
            IPEndPoint discoveredRemoteEndpoint)
        {
            return discoveredRemoteEndpoint != null
                && MatchesTargetConfiguration(
                    currentListenPort,
                    currentRemoteHost,
                    currentRemotePort,
                    expectedListenPort,
                    discoveredRemoteEndpoint.Address.ToString(),
                    discoveredRemoteEndpoint.Port);
        }

        internal static bool MatchesTargetConfiguration(
            int currentListenPort,
            string currentRemoteHost,
            int currentRemotePort,
            int expectedListenPort,
            string expectedRemoteHost,
            int expectedRemotePort)
        {
            return currentListenPort == expectedListenPort
                && currentRemotePort == expectedRemotePort
                && string.Equals(
                    NormalizeRemoteHost(currentRemoteHost),
                    NormalizeRemoteHost(expectedRemoteHost),
                    StringComparison.OrdinalIgnoreCase);
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

        private static IEnumerable<TcpRowOwnerPid> EnumerateTcpRows()
        {
            int bufferSize = 0;
            uint result = GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, sort: true, AddressFamilyInet, TcpTableClass.TcpTableOwnerPidAll, 0);
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
                IntPtr rowPointer = IntPtr.Add(buffer, sizeof(int));
                int rowSize = Marshal.SizeOf<TcpRowOwnerPid>();
                for (int index = 0; index < rowCount; index++)
                {
                    yield return Marshal.PtrToStructure<TcpRowOwnerPid>(rowPointer);
                    rowPointer = IntPtr.Add(rowPointer, rowSize);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static int DecodePort(byte[] portBytes)
        {
            return portBytes == null || portBytes.Length < 2
                ? 0
                : (portBytes[0] << 8) | portBytes[1];
        }

        private static IPAddress DecodeAddress(uint address)
        {
            return new IPAddress(address);
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
            TcpTableOwnerPidAll = 5,
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(
            IntPtr pTcpTable,
            ref int dwOutBufLen,
            [MarshalAs(UnmanagedType.Bool)] bool sort,
            int ipVersion,
            TcpTableClass tableClass,
            uint reserved);
    }
}
