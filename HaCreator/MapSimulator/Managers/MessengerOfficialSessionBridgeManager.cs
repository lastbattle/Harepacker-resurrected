using HaCreator.MapSimulator.Interaction;
using MapleLib.MapleCryptoLib;
using MapleLib.PacketLib;
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
    public sealed class MessengerOfficialSessionBridgeMessage
    {
        public MessengerOfficialSessionBridgeMessage(byte[] payload, string source, string rawText, int opcode)
        {
            Payload = payload ?? Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "messenger-session" : source;
            RawText = rawText ?? string.Empty;
            Opcode = opcode;
        }

        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
        public int Opcode { get; }
    }

    public sealed class MessengerOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18504;
        public const ushort DefaultInboundResultOpcode = 372;
        private const int MaxRecentOutboundPackets = 32;
        private const int MaxRecentInboundPackets = 32;
        private const string DefaultProcessName = "MapleStory";

        private readonly string _ownerName;
        private readonly int _defaultListenPort;
        private readonly ushort _defaultInboundOpcode;
        private readonly ushort _defaultOutboundOpcode;
        private readonly HashSet<ushort> _additionalInboundOpcodes;
        private readonly ConcurrentQueue<MessengerOfficialSessionBridgeMessage> _pendingMessages = new();
        private readonly ConcurrentQueue<PendingOutboundPacket> _pendingOutboundPackets = new();
        private readonly object _sync = new();
        private readonly Queue<OutboundPacketTrace> _recentOutboundPackets = new();
        private readonly Queue<InboundPacketTrace> _recentInboundPackets = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;
        private BridgePair _activePair;

        private readonly record struct PendingOutboundPacket(int Opcode, byte[] RawPacket);

        public readonly record struct OutboundPacketTrace(
            int Opcode,
            byte RequestType,
            int PayloadLength,
            string PayloadHex,
            string RawPacketHex,
            string Source,
            string Summary);

        public readonly record struct InboundPacketTrace(
            int Opcode,
            byte ResultType,
            int PayloadLength,
            string PayloadHex,
            string RawPacketHex,
            string Source,
            string Summary);

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
        public ushort MessengerOpcode { get; private set; } = DefaultInboundResultOpcode;
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public bool HasAttachedClient => _activePair != null;
        public bool HasConnectedSession => _activePair?.InitCompleted == true;
        public int ReceivedCount { get; private set; }
        public int SentCount { get; private set; }
        public int QueuedCount { get; private set; }
        public int PendingOutboundPacketCount => _pendingOutboundPackets.Count;
        public int LastSentOpcode { get; private set; } = -1;
        public byte[] LastSentRawPacket { get; private set; } = Array.Empty<byte>();
        public int LastQueuedOpcode { get; private set; } = -1;
        public byte[] LastQueuedRawPacket { get; private set; } = Array.Empty<byte>();
        public string LastStatus { get; private set; } = "Messenger official-session bridge inactive.";

        public MessengerOfficialSessionBridgeManager()
            : this("Messenger", DefaultListenPort, DefaultInboundResultOpcode, MessengerPacketCodec.ClientMessengerRequestOpcode)
        {
        }

        internal MessengerOfficialSessionBridgeManager(string ownerName, int defaultListenPort, ushort defaultInboundOpcode, ushort defaultOutboundOpcode = 0, params ushort[] additionalInboundOpcodes)
        {
            _ownerName = string.IsNullOrWhiteSpace(ownerName) ? "Messenger" : ownerName.Trim();
            _defaultListenPort = defaultListenPort <= 0 ? DefaultListenPort : defaultListenPort;
            _defaultInboundOpcode = defaultInboundOpcode == 0 ? DefaultInboundResultOpcode : defaultInboundOpcode;
            _defaultOutboundOpcode = defaultOutboundOpcode;
            _additionalInboundOpcodes = new HashSet<ushort>((additionalInboundOpcodes ?? Array.Empty<ushort>()).Where(opcode => opcode != 0));
            ListenPort = _defaultListenPort;
            MessengerOpcode = _defaultInboundOpcode;
            LastStatus = $"{_ownerName} official-session bridge inactive.";
        }

        public string DescribeStatus()
        {
            string lifecycle = IsRunning
                ? $"listening on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}"
                : "inactive";
            string session = HasConnectedSession
                ? $"connected session {_activePair?.ClientEndpoint ?? "unknown-client"} -> {_activePair?.RemoteEndpoint ?? "unknown-remote"}"
                : "no active Maple session";
            string lastOutbound = LastSentOpcode >= 0
                ? $" lastOut={LastSentOpcode}[{Convert.ToHexString(LastSentRawPacket)}]."
                : string.Empty;
            string lastQueued = LastQueuedOpcode >= 0
                ? $" lastQueued={LastQueuedOpcode}[{Convert.ToHexString(LastQueuedRawPacket)}]."
                : string.Empty;
            string outboundText = _defaultOutboundOpcode > 0
                ? $"; outbound opcode={_defaultOutboundOpcode}"
                : string.Empty;
            string inboundText = DescribeInboundOpcodeSet();
            return $"{_ownerName} official-session bridge {lifecycle}; {session}; received={ReceivedCount}; sent={SentCount}; pending={PendingOutboundPacketCount}; queued={QueuedCount}; inbound opcode {inboundText}{outboundText}.{lastOutbound}{lastQueued} {LastStatus}";
        }

        public string DescribeRecentOutboundPackets(int maxCount = 10)
        {
            int normalizedCount = Math.Clamp(maxCount, 1, MaxRecentOutboundPackets);
            lock (_sync)
            {
                if (_recentOutboundPackets.Count == 0)
                {
                    return $"{_ownerName} official-session bridge outbound history is empty.";
                }

                OutboundPacketTrace[] entries = _recentOutboundPackets
                    .Reverse()
                    .Take(normalizedCount)
                    .ToArray();
                return $"{_ownerName} official-session bridge outbound history:"
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        entries.Select(entry =>
                            $"opcode={entry.Opcode} type={entry.RequestType} payloadLen={entry.PayloadLength} source={entry.Source} summary={entry.Summary} payloadHex={entry.PayloadHex} raw={entry.RawPacketHex}"));
            }
        }

        public string DescribeRecentInboundPackets(int maxCount = 10)
        {
            int normalizedCount = Math.Clamp(maxCount, 1, MaxRecentInboundPackets);
            lock (_sync)
            {
                if (_recentInboundPackets.Count == 0)
                {
                    return $"{_ownerName} official-session bridge inbound history is empty.";
                }

                InboundPacketTrace[] entries = _recentInboundPackets
                    .Reverse()
                    .Take(normalizedCount)
                    .ToArray();
                return $"{_ownerName} official-session bridge inbound history:"
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        entries.Select(entry =>
                            $"opcode={entry.Opcode} type={entry.ResultType} payloadLen={entry.PayloadLength} source={entry.Source} summary={entry.Summary} payloadHex={entry.PayloadHex} raw={entry.RawPacketHex}"));
            }
        }

        public string ClearRecentOutboundPackets()
        {
            lock (_sync)
            {
                _recentOutboundPackets.Clear();
            }

            LastStatus = $"{_ownerName} official-session bridge outbound history cleared.";
            return LastStatus;
        }

        public string ClearRecentInboundPackets()
        {
            lock (_sync)
            {
                _recentInboundPackets.Clear();
            }

            LastStatus = $"{_ownerName} official-session bridge inbound history cleared.";
            return LastStatus;
        }

        public string DescribeRecoveredPacketTable()
        {
            if (string.Equals(_ownerName, "MapleTV", StringComparison.OrdinalIgnoreCase))
            {
                return "Recovered MapleTV packet table: inbound CMapleTVMan::OnSetMessage(405), CMapleTVMan::OnClearMessage(406), CMapleTVMan::OnSendMessageResult(407); outbound CUserLocal::ConsumeCashItem(85).";
            }

            return "Recovered Messenger packet table: inbound CUIMessenger::OnPacket(372) subtypes 0-8; outbound CUIMessenger::TryNew/OnDestroy/SendInviteMsg/OnInvite blacklist auto-reject/ProcessChat on opcode 143 subtypes 0/2/3/5/6, plus CWvsContext::SendClaimRequest on opcode 118.";
        }

        public bool TryReplayRecentOutboundPacket(int historyIndexFromNewest, out string status)
        {
            if (historyIndexFromNewest <= 0)
            {
                status = $"{_ownerName} replay index must be 1 or greater.";
                LastStatus = status;
                return false;
            }

            OutboundPacketTrace[] entries;
            lock (_sync)
            {
                if (_recentOutboundPackets.Count == 0)
                {
                    status = $"No captured {_ownerName} outbound packets are available to replay.";
                    LastStatus = status;
                    return false;
                }

                if (historyIndexFromNewest > _recentOutboundPackets.Count)
                {
                    status = $"{_ownerName} replay index {historyIndexFromNewest} exceeds the {_recentOutboundPackets.Count} captured outbound packet(s).";
                    LastStatus = status;
                    return false;
                }

                entries = _recentOutboundPackets.ToArray();
            }

            OutboundPacketTrace trace = entries[^historyIndexFromNewest];
            try
            {
                byte[] rawPacket = Convert.FromHexString(trace.RawPacketHex);
                return TrySendOutboundRawPacket(rawPacket, out status);
            }
            catch (FormatException ex)
            {
                status = $"Captured {_ownerName} outbound packet {historyIndexFromNewest} could not be replayed: {ex.Message}";
                LastStatus = status;
                return false;
            }
        }

        public void Start(int listenPort, string remoteHost, int remotePort, ushort messengerOpcode)
        {
            lock (_sync)
            {
                StopInternal(clearPending: false);
                ResetInboundState();

                try
                {
                    ListenPort = listenPort <= 0 ? _defaultListenPort : listenPort;
                    RemoteHost = string.IsNullOrWhiteSpace(remoteHost) ? IPAddress.Loopback.ToString() : remoteHost.Trim();
                    RemotePort = remotePort;
                    MessengerOpcode = messengerOpcode == 0 ? _defaultInboundOpcode : messengerOpcode;
                    _listenerCancellation = new CancellationTokenSource();
                    _listener = new TcpListener(IPAddress.Loopback, ListenPort);
                    _listener.Start();
                    _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                    LastStatus = $"{_ownerName} official-session bridge listening on 127.0.0.1:{ListenPort}, proxying to {RemoteHost}:{RemotePort}, and filtering opcode {DescribeInboundOpcodeSet()}.";
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: false);
                    LastStatus = $"{_ownerName} official-session bridge failed to start: {ex.Message}";
                }
            }
        }

        public bool TryRefreshFromDiscovery(int listenPort, int remotePort, ushort messengerOpcode, string processSelector, int? localPort, out string status)
        {
            if (HasAttachedClient)
            {
                status = $"{_ownerName} official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                LastStatus = status;
                return true;
            }

            int resolvedListenPort = listenPort <= 0 ? _defaultListenPort : listenPort;
            if (!TryResolveProcessSelector(processSelector, out int? owningProcessId, out string owningProcessName, out string selectorError))
            {
                status = selectorError;
                LastStatus = status;
                return false;
            }

            var candidates = CoconutOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            if (!TryResolveDiscoveryCandidate(candidates, remotePort, owningProcessId, owningProcessName, localPort, _ownerName, out var candidate, out status))
            {
                LastStatus = status;
                return false;
            }

            ushort resolvedOpcode = messengerOpcode == 0 ? _defaultInboundOpcode : messengerOpcode;
            if (IsRunning
                && ListenPort == resolvedListenPort
                && RemotePort == candidate.RemoteEndpoint.Port
                && MessengerOpcode == resolvedOpcode
                && string.Equals(RemoteHost, candidate.RemoteEndpoint.Address.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                status = $"{_ownerName} official-session bridge remains armed for {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} using opcode {DescribeInboundOpcodeSet(resolvedOpcode)}.";
                LastStatus = status;
                return true;
            }

            Start(resolvedListenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, resolvedOpcode);
            status = $"{_ownerName} official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {LastStatus}";
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
                LastStatus = $"{_ownerName} official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out MessengerOfficialSessionBridgeMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public bool TrySendOutboundPacket(int opcode, IReadOnlyList<byte> payload, out string status)
        {
            BridgePair pair;
            lock (_sync)
            {
                pair = _activePair;
            }

            if (pair == null || !pair.InitCompleted)
            {
                status = $"{_ownerName} official-session bridge has no connected Maple session for outbound injection.";
                LastStatus = status;
                return false;
            }

            if (!TryBuildRawPacket(opcode, payload, out byte[] rawPacket, out status))
            {
                LastStatus = status;
                return false;
            }

            try
            {
                pair.ServerSession.SendPacket(rawPacket);
                SentCount++;
                LastSentOpcode = opcode;
                LastSentRawPacket = rawPacket;
                RecordObservedOutboundPacket(rawPacket, "simulator-send");
                status = $"Injected {_ownerName} outbound opcode {opcode} into live session {pair.RemoteEndpoint}.";
                LastStatus = status;
                return true;
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"{_ownerName} official-session outbound injection failed: {ex.Message}");
                status = LastStatus;
                return false;
            }
        }

        public bool TryQueueOutboundPacket(int opcode, IReadOnlyList<byte> payload, out string status)
        {
            if (!TryBuildRawPacket(opcode, payload, out byte[] rawPacket, out status))
            {
                LastStatus = status;
                return false;
            }

            _pendingOutboundPackets.Enqueue(new PendingOutboundPacket(opcode, rawPacket));
            QueuedCount++;
            LastQueuedOpcode = opcode;
            LastQueuedRawPacket = rawPacket;
            RecordObservedOutboundPacket(rawPacket, "simulator-queue");
            status = $"Queued {_ownerName} outbound opcode {opcode} for deferred live-session injection.";
            LastStatus = status;
            return true;
        }

        public bool TrySendOutboundRawPacket(byte[] rawPacket, out string status)
        {
            if (!TryDecodeOpcode(rawPacket, out int opcode, out byte[] _))
            {
                status = $"{_ownerName} outbound raw packet must include a 2-byte opcode.";
                LastStatus = status;
                return false;
            }

            BridgePair pair;
            lock (_sync)
            {
                pair = _activePair;
            }

            if (pair == null || !pair.InitCompleted)
            {
                status = $"{_ownerName} official-session bridge has no connected Maple session for outbound injection.";
                LastStatus = status;
                return false;
            }

            byte[] clonedRawPacket = (byte[])rawPacket.Clone();
            try
            {
                pair.ServerSession.SendPacket(clonedRawPacket);
                SentCount++;
                LastSentOpcode = opcode;
                LastSentRawPacket = clonedRawPacket;
                RecordObservedOutboundPacket(clonedRawPacket, "simulator-replay");
                status = $"Injected {_ownerName} outbound opcode {opcode} raw packet into live session {pair.RemoteEndpoint}.";
                LastStatus = status;
                return true;
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"{_ownerName} official-session outbound injection failed: {ex.Message}");
                status = LastStatus;
                return false;
            }
        }

        public void RecordDispatchResult(string source, bool success, string detail)
        {
            string summary = string.IsNullOrWhiteSpace(detail) ? "Messenger OnPacket payload" : detail;
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
                LastStatus = $"{_ownerName} official-session bridge error: {ex.Message}";
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
                        LastStatus = $"Rejected {_ownerName} official-session client because a live Maple session is already attached.";
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
                clientSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Messenger official-session client disconnected: {pair.ClientEndpoint}.");
                serverSession.OnPacketReceived += (packet, isInit) => HandleServerPacket(pair, packet, isInit);
                serverSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Messenger official-session server disconnected: {pair.RemoteEndpoint}.");

                lock (_sync)
                {
                    _activePair = pair;
                }

                LastStatus = $"{_ownerName} official-session bridge connected {pair.ClientEndpoint} -> {pair.RemoteEndpoint}. Waiting for Maple init packet.";
                serverSession.WaitForDataNoEncryption();
            }
            catch (Exception ex)
            {
                client.Close();
                pair?.Close();
                LastStatus = $"{_ownerName} official-session bridge connect failed: {ex.Message}";
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
                    int flushed = FlushPendingOutboundPackets(pair);
                    LastStatus = flushed > 0
                        ? $"{_ownerName} official-session bridge initialized Maple crypto for {pair.ClientEndpoint} <-> {pair.RemoteEndpoint} and flushed {flushed} queued request(s)."
                        : $"{_ownerName} official-session bridge initialized Maple crypto for {pair.ClientEndpoint} <-> {pair.RemoteEndpoint}.";
                    pair.ClientSession.WaitForData();
                    return;
                }

                pair.ClientSession.SendPacket((byte[])raw.Clone());
                if (!TryDecodeInboundMessengerPacket(raw, $"official-session:{pair.RemoteEndpoint}", MessengerOpcode, _additionalInboundOpcodes, out MessengerOfficialSessionBridgeMessage message))
                {
                    return;
                }

                _pendingMessages.Enqueue(message);
                RecordObservedInboundPacket(raw, message.Source);
                ReceivedCount++;
                LastStatus = $"Queued {_ownerName} opcode {message.Opcode} ({message.Payload.Length} byte(s)) from live session {pair.RemoteEndpoint}.";
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"{_ownerName} official-session server handling failed: {ex.Message}");
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
                RecordObservedOutboundPacket(packet.ToArray(), $"official-session-client:{pair.ClientEndpoint}");
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"{_ownerName} official-session client handling failed: {ex.Message}");
            }
        }

        private static bool TryDecodeInboundMessengerPacket(byte[] rawPacket, string source, ushort messengerOpcode, IReadOnlySet<ushort> additionalOpcodes, out MessengerOfficialSessionBridgeMessage message)
        {
            message = null;
            if (rawPacket == null || rawPacket.Length < sizeof(ushort) || messengerOpcode == 0)
            {
                return false;
            }

            int opcode = BitConverter.ToUInt16(rawPacket, 0);
            if (opcode != messengerOpcode && additionalOpcodes?.Contains((ushort)opcode) != true)
            {
                return false;
            }

            byte[] payload = rawPacket.Length == sizeof(ushort)
                ? Array.Empty<byte>()
                : rawPacket[sizeof(ushort)..];
            message = new MessengerOfficialSessionBridgeMessage(payload, source, $"packetclientraw {Convert.ToHexString(rawPacket)}", opcode);
            return true;
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

        private void RecordObservedOutboundPacket(byte[] rawPacket, string source)
        {
            if (!TryDecodeOpcode(rawPacket, out int opcode, out byte[] payload)
                || _defaultOutboundOpcode > 0 && opcode != _defaultOutboundOpcode
                || payload.Length == 0)
            {
                return;
            }

            string summary = TryDescribeOwnerClientRequest(opcode, payload, out string described)
                ? described
                : $"{_ownerName} request opcode={opcode} type={payload[0]}";

            lock (_sync)
            {
                while (_recentOutboundPackets.Count >= MaxRecentOutboundPackets)
                {
                    _recentOutboundPackets.Dequeue();
                }

                _recentOutboundPackets.Enqueue(new OutboundPacketTrace(
                    opcode,
                    payload[0],
                    payload.Length,
                    Convert.ToHexString(payload),
                    Convert.ToHexString(rawPacket),
                    source,
                    summary));
            }

            LastStatus = $"Forwarded live {_ownerName} outbound opcode {opcode} type {payload[0]} from {source}.";
        }

        private void RecordObservedInboundPacket(byte[] rawPacket, string source)
        {
            if (!TryDecodeOpcode(rawPacket, out int opcode, out byte[] payload))
            {
                return;
            }

            byte resultType = payload.Length > 0 ? payload[0] : (byte)0;
            string summary = TryDescribeOwnerClientResult(opcode, payload, out string described)
                ? described
                : $"{_ownerName} result opcode={opcode} type={resultType}";

            lock (_sync)
            {
                while (_recentInboundPackets.Count >= MaxRecentInboundPackets)
                {
                    _recentInboundPackets.Dequeue();
                }

                _recentInboundPackets.Enqueue(new InboundPacketTrace(
                    opcode,
                    resultType,
                    payload.Length,
                    Convert.ToHexString(payload),
                    Convert.ToHexString(rawPacket),
                    source,
                    summary));
            }
        }

        private static bool TryDecodeOpcode(byte[] rawPacket, out int opcode, out byte[] payload)
        {
            opcode = 0;
            payload = Array.Empty<byte>();
            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                return false;
            }

            opcode = BitConverter.ToUInt16(rawPacket, 0);
            payload = rawPacket.Length == sizeof(ushort)
                ? Array.Empty<byte>()
                : rawPacket[sizeof(ushort)..];
            return true;
        }

        private bool TryDescribeOwnerClientRequest(int opcode, byte[] payload, out string summary)
        {
            summary = null;
            if (string.Equals(_ownerName, "MapleTV", StringComparison.OrdinalIgnoreCase)
                && opcode == MapleTvRuntime.ConsumeCashItemUseRequestOpcode)
            {
                if (MapleTvRuntime.TryDecodeConsumeCashItemUseRequestPayload(payload, out MapleTvConsumeCashItemUseRequest request, out _))
                {
                    string receiver = string.IsNullOrWhiteSpace(request.ReceiverName)
                        ? "self broadcast"
                        : $"receiver={request.ReceiverName}";
                    summary = $"MapleTV consume-cash request slot={request.InventoryPosition} item={request.ItemId} {receiver}";
                    return true;
                }

                summary = "MapleTV consume-cash request";
                return true;
            }

            return MessengerPacketCodec.TryDescribeClientRequest(opcode, payload, out summary);
        }

        private bool TryDescribeOwnerClientResult(int opcode, byte[] payload, out string summary)
        {
            summary = null;
            if (string.Equals(_ownerName, "MapleTV", StringComparison.OrdinalIgnoreCase))
            {
                switch (opcode)
                {
                    case MapleTvRuntime.PacketTypeSetMessage:
                        summary = "CMapleTVMan::OnSetMessage";
                        return true;
                    case MapleTvRuntime.PacketTypeClearMessage:
                        summary = "CMapleTVMan::OnClearMessage";
                        return true;
                    case MapleTvRuntime.PacketTypeSendMessageResult:
                        if (payload.Length >= 2)
                        {
                            bool showFeedback = payload[0] != 0;
                            summary = showFeedback
                                ? $"CMapleTVMan::OnSendMessageResult showFeedback=1 resultCode={payload[1]}"
                                : "CMapleTVMan::OnSendMessageResult showFeedback=0";
                            return true;
                        }

                        summary = "CMapleTVMan::OnSendMessageResult";
                        return true;
                }
            }

            return MessengerPacketCodec.TryDescribeClientResult(opcode, payload, out summary);
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

            _listenerTask = null;
            _listener = null;
            _listenerCancellation?.Dispose();
            _listenerCancellation = null;

            BridgePair pair;
            lock (_sync)
            {
                pair = _activePair;
                _activePair = null;
            }

            pair?.Close();
            if (clearPending)
            {
                ResetInboundState();
                while (_pendingOutboundPackets.TryDequeue(out _))
                {
                }
            }
        }

        private void ResetInboundState()
        {
            while (_pendingMessages.TryDequeue(out _))
            {
            }

            ReceivedCount = 0;
            SentCount = 0;
            QueuedCount = 0;
            LastSentOpcode = -1;
            LastSentRawPacket = Array.Empty<byte>();
            LastQueuedOpcode = -1;
            LastQueuedRawPacket = Array.Empty<byte>();
        }

        private int FlushPendingOutboundPackets(BridgePair pair)
        {
            int flushed = 0;
            while (_pendingOutboundPackets.TryDequeue(out PendingOutboundPacket pending))
            {
                pair.ServerSession.SendPacket(pending.RawPacket);
                SentCount++;
                LastSentOpcode = pending.Opcode;
                LastSentRawPacket = pending.RawPacket;
                flushed++;
            }

            return flushed;
        }

        private bool TryBuildRawPacket(int opcode, IReadOnlyList<byte> payload, out byte[] rawPacket, out string status)
        {
            rawPacket = Array.Empty<byte>();
            if (opcode < ushort.MinValue || opcode > ushort.MaxValue)
            {
                status = $"{_ownerName} outbound opcode {opcode} is outside the 16-bit Maple packet range.";
                return false;
            }

            byte[] safePayload = payload == null
                ? Array.Empty<byte>()
                : payload as byte[] ?? payload.ToArray();
            rawPacket = new byte[sizeof(ushort) + safePayload.Length];
            rawPacket[0] = (byte)(opcode & 0xFF);
            rawPacket[1] = (byte)((opcode >> 8) & 0xFF);
            if (safePayload.Length > 0)
            {
                Buffer.BlockCopy(safePayload, 0, rawPacket, sizeof(ushort), safePayload.Length);
            }

            status = null;
            return true;
        }

        private string DescribeInboundOpcodeSet()
        {
            return DescribeInboundOpcodeSet(MessengerOpcode);
        }

        private string DescribeInboundOpcodeSet(ushort primaryOpcode)
        {
            if (_additionalInboundOpcodes.Count == 0)
            {
                return primaryOpcode.ToString();
            }

            return string.Join(",", new[] { primaryOpcode }.Concat(_additionalInboundOpcodes).Distinct().OrderBy(opcode => opcode));
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

            string normalized = selector.Trim();
            owningProcessName = normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? normalized[..^4]
                : normalized;
            return owningProcessName.Length > 0;
        }

        private static bool TryResolveDiscoveryCandidate(
            System.Collections.Generic.IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates,
            int remotePort,
            int? owningProcessId,
            string owningProcessName,
            int? localPort,
            string ownerName,
            out CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate candidate,
            out string status)
        {
            var filteredCandidates = FilterCandidatesByLocalPort(candidates, localPort);
            if (filteredCandidates.Count == 0)
            {
                status = $"{DescribeDiscoveryOwner(ownerName)} official-session discovery found no established TCP session for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
                candidate = default;
                return false;
            }

            if (filteredCandidates.Count > 1)
            {
                string matches = string.Join(", ", filteredCandidates.Select(match =>
                    $"{match.ProcessName}({match.ProcessId}) local {match.LocalEndpoint.Port} -> remote {match.RemoteEndpoint.Port}"));
                status = $"{DescribeDiscoveryOwner(ownerName)} official-session discovery found multiple candidates for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}: {matches}. Add a localPort filter.";
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

        private static string DescribeDiscoveryOwner(string ownerName)
        {
            return string.IsNullOrWhiteSpace(ownerName) ? "Messenger" : ownerName.Trim();
        }

        private static string DescribeDiscoveryScope(int? owningProcessId, string owningProcessName, int remotePort, int? localPort)
        {
            string processScope = owningProcessId.HasValue
                ? $"process {owningProcessId.Value}"
                : string.IsNullOrWhiteSpace(owningProcessName)
                    ? DefaultProcessName
                    : owningProcessName;
            string localScope = localPort.HasValue ? $" and local port {localPort.Value}" : string.Empty;
            return $"{processScope} on remote port {remotePort}{localScope}";
        }
    }
}
