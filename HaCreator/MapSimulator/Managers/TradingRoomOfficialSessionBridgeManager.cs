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
    /// <summary>
    /// Proxies a live Maple session and forwards opcode-wrapped trading-room traffic
    /// into the existing CTradingRoomDlg::OnPacket payload seam.
    /// </summary>
    public sealed class TradingRoomOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18492;
        public const ushort OutboundTradingRoomOpcode = 144;
        private const int MaxRecentOutboundPackets = 32;

        private readonly ConcurrentQueue<TradingRoomPacketInboxMessage> _pendingMessages = new();
        private readonly object _sync = new();
        private readonly Queue<OutboundPacketTrace> _recentOutboundPackets = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;
        private BridgePair _activePair;

        public readonly record struct OutboundPacketTrace(
            int Opcode,
            byte PacketType,
            int PayloadLength,
            string PayloadHex,
            string RawPacketHex,
            string Source);

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
        public ushort InboundTradingRoomOpcode { get; private set; }
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public bool HasAttachedClient => _activePair != null;
        public bool HasConnectedSession => _activePair?.InitCompleted == true;
        public int ReceivedCount { get; private set; }
        public int ForwardedOutboundCount { get; private set; }
        public int ForwardedOutboundCrcCount { get; private set; }
        public int SentCount { get; private set; }
        public int LastSentOpcode { get; private set; } = -1;
        public string LastStatus { get; private set; } = "Trading-room official-session bridge inactive.";

        public string DescribeStatus()
        {
            string lifecycle = IsRunning
                ? $"listening on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}"
                : "inactive";
            string session = HasConnectedSession
                ? $"connected session {_activePair?.ClientEndpoint ?? "unknown-client"} -> {_activePair?.RemoteEndpoint ?? "unknown-remote"}"
                : "no active Maple session";
            string inboundOpcode = InboundTradingRoomOpcode > 0
                ? $"inbound opcode={InboundTradingRoomOpcode}"
                : "inbound opcode unset";
            return $"Trading-room official-session bridge {lifecycle}; {session}; received={ReceivedCount}; forwardedOutbound={ForwardedOutboundCount}; forwardedOutboundCrc={ForwardedOutboundCrcCount}; injected={SentCount}; {inboundOpcode}. {LastStatus}";
        }

        public string DescribeRecentOutboundPackets(int maxCount = 10)
        {
            int normalizedCount = Math.Clamp(maxCount, 1, MaxRecentOutboundPackets);
            lock (_sync)
            {
                if (_recentOutboundPackets.Count == 0)
                {
                    return "Trading-room official-session bridge outbound history is empty.";
                }

                OutboundPacketTrace[] entries = _recentOutboundPackets
                    .Reverse()
                    .Take(normalizedCount)
                    .ToArray();
                return "Trading-room official-session bridge outbound history:"
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        entries.Select(entry =>
                            $"opcode={entry.Opcode} type={entry.PacketType} payloadLen={entry.PayloadLength} source={entry.Source} payloadHex={entry.PayloadHex} raw={entry.RawPacketHex}"));
            }
        }

        public string ClearRecentOutboundPackets()
        {
            lock (_sync)
            {
                _recentOutboundPackets.Clear();
            }

            LastStatus = "Trading-room official-session bridge outbound history cleared.";
            return LastStatus;
        }

        public bool TryReplayRecentOutboundPacket(int historyIndexFromNewest, out string status)
        {
            if (historyIndexFromNewest <= 0)
            {
                status = "Trading-room replay index must be 1 or greater.";
                LastStatus = status;
                return false;
            }

            OutboundPacketTrace[] entries;
            lock (_sync)
            {
                if (_recentOutboundPackets.Count == 0)
                {
                    status = "No captured trading-room outbound client packets are available to replay.";
                    LastStatus = status;
                    return false;
                }

                if (historyIndexFromNewest > _recentOutboundPackets.Count)
                {
                    status = $"Trading-room replay index {historyIndexFromNewest} exceeds the {_recentOutboundPackets.Count} captured outbound packet(s).";
                    LastStatus = status;
                    return false;
                }

                entries = _recentOutboundPackets.ToArray();
            }

            OutboundPacketTrace trace = entries[^historyIndexFromNewest];
            if (string.IsNullOrWhiteSpace(trace.RawPacketHex))
            {
                status = $"Captured trading-room outbound packet {historyIndexFromNewest} has no raw payload to replay.";
                LastStatus = status;
                return false;
            }

            try
            {
                byte[] rawPacket = Convert.FromHexString(trace.RawPacketHex);
                return TrySendOutboundRawPacket(rawPacket, out status);
            }
            catch (FormatException ex)
            {
                status = $"Captured trading-room outbound packet {historyIndexFromNewest} could not be replayed: {ex.Message}";
                LastStatus = status;
                return false;
            }
        }

        public void Start(int listenPort, string remoteHost, int remotePort, ushort inboundTradingRoomOpcode = 0)
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
                    InboundTradingRoomOpcode = inboundTradingRoomOpcode;
                    _listenerCancellation = new CancellationTokenSource();
                    _listener = new TcpListener(IPAddress.Loopback, ListenPort);
                    _listener.Start();
                    _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                    LastStatus = InboundTradingRoomOpcode > 0
                        ? $"Trading-room official-session bridge listening on 127.0.0.1:{ListenPort}, proxying to {RemoteHost}:{RemotePort}, and filtering inbound opcode {InboundTradingRoomOpcode}."
                        : $"Trading-room official-session bridge listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}; inbound opcode is unset so server payload capture is idle until configured.";
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: false);
                    LastStatus = $"Trading-room official-session bridge failed to start: {ex.Message}";
                }
            }
        }

        public bool TryStartFromDiscovery(int listenPort, int remotePort, ushort inboundTradingRoomOpcode, string processSelector, int? localPort, out string status)
        {
            if (!TryResolveProcessSelector(processSelector, out int? owningProcessId, out string owningProcessName, out string selectorError))
            {
                status = selectorError;
                LastStatus = status;
                return false;
            }

            IReadOnlyList<MemoryGameOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates =
                MemoryGameOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            if (!MemoryGameOfficialSessionBridgeManager.TryResolveDiscoveryCandidate(
                    candidates,
                    remotePort,
                    owningProcessId,
                    owningProcessName,
                    localPort,
                    out MemoryGameOfficialSessionBridgeManager.SessionDiscoveryCandidate candidate,
                    out status))
            {
                LastStatus = status;
                return false;
            }

            Start(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, inboundTradingRoomOpcode);
            status =
                $"Trading-room official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {LastStatus}";
            LastStatus = status;
            return true;
        }

        public string DescribeDiscoveredSessions(int remotePort, string processSelector = null, int? localPort = null)
        {
            if (!TryResolveProcessSelector(processSelector, out int? owningProcessId, out string owningProcessName, out string selectorError))
            {
                return selectorError;
            }

            IReadOnlyList<MemoryGameOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates =
                MemoryGameOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            return MemoryGameOfficialSessionBridgeManager.DescribeDiscoveryCandidates(candidates, remotePort, owningProcessId, owningProcessName, localPort);
        }

        public void Stop()
        {
            lock (_sync)
            {
                StopInternal(clearPending: false);
                LastStatus = "Trading-room official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out TradingRoomPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(string source, bool success, string detail)
        {
            string summary = string.IsNullOrWhiteSpace(detail) ? "trading-room payload" : detail;
            LastStatus = success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.";
        }

        public bool TrySendOutboundRawPacket(byte[] rawPacket, out string status)
        {
            if (!TryValidateOutboundRawPacket(rawPacket, out int opcode, out byte packetType, out string error))
            {
                status = error;
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
                status = "Trading-room official-session bridge has no connected Maple session for outbound injection.";
                LastStatus = status;
                return false;
            }

            byte[] clonedRawPacket = (byte[])rawPacket.Clone();
            try
            {
                pair.ServerSession.SendPacket(clonedRawPacket);
                SentCount++;
                LastSentOpcode = opcode;
                RecordObservedOutboundPacket(clonedRawPacket, "simulator-send");
                status = $"Injected trading-room outbound opcode {opcode} subtype {packetType} into live session {pair.RemoteEndpoint}.";
                LastStatus = status;
                return true;
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Trading-room official-session outbound injection failed: {ex.Message}");
                status = LastStatus;
                return false;
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                StopInternal(clearPending: false);
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
                LastStatus = $"Trading-room official-session bridge error: {ex.Message}";
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
                        LastStatus = "Rejected trading-room official-session client because a live Maple session is already attached.";
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
                clientSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Trading-room official-session client disconnected: {pair.ClientEndpoint}.");
                serverSession.OnPacketReceived += (packet, isInit) => HandleServerPacket(pair, packet, isInit);
                serverSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Trading-room official-session server disconnected: {pair.RemoteEndpoint}.");

                lock (_sync)
                {
                    _activePair = pair;
                }

                LastStatus = $"Trading-room official-session bridge connected {pair.ClientEndpoint} -> {pair.RemoteEndpoint}. Waiting for Maple init packet.";
                serverSession.WaitForDataNoEncryption();
            }
            catch (Exception ex)
            {
                client.Close();
                pair?.Close();
                LastStatus = $"Trading-room official-session bridge connect failed: {ex.Message}";
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
                    LastStatus = $"Trading-room official-session bridge initialized Maple crypto for {pair.ClientEndpoint} <-> {pair.RemoteEndpoint}.";
                    pair.ClientSession.WaitForData();
                    return;
                }

                pair.ClientSession.SendPacket((byte[])raw.Clone());
                if (!TryDecodeOpcode(raw, out int opcode, out byte[] payload)
                    || InboundTradingRoomOpcode <= 0
                    || opcode != InboundTradingRoomOpcode
                    || payload.Length == 0
                    || !IsTradingRoomInboundSubtype(payload[0]))
                {
                    return;
                }

                _pendingMessages.Enqueue(new TradingRoomPacketInboxMessage(
                    (byte[])raw.Clone(),
                    $"official-session:{pair.RemoteEndpoint}",
                    $"packetrecv {opcode} {Convert.ToHexString(payload)}"));
                ReceivedCount++;
                LastStatus = $"Queued trading-room opcode {opcode} subtype {payload[0]} from live session {pair.RemoteEndpoint}.";
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Trading-room official-session server handling failed: {ex.Message}");
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
                RecordObservedOutboundPacket(raw, $"official-session-client:{pair.ClientEndpoint}");
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Trading-room official-session client handling failed: {ex.Message}");
            }
        }

        private void RecordObservedOutboundPacket(byte[] rawPacket, string source)
        {
            if (!TryDecodeOpcode(rawPacket, out int opcode, out byte[] payload)
                || opcode != OutboundTradingRoomOpcode
                || payload.Length == 0)
            {
                return;
            }

            ForwardedOutboundCount++;
            if (payload[0] == 20)
            {
                ForwardedOutboundCrcCount++;
            }

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
                    source));
            }

            LastStatus = $"Forwarded live trading-room outbound opcode {opcode} subtype {payload[0]} from {source}.";
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

            _activePair?.Close();
            _activePair = null;
            _listener = null;
            _listenerTask = null;
            _listenerCancellation?.Dispose();
            _listenerCancellation = null;

            if (clearPending)
            {
                while (_pendingMessages.TryDequeue(out _))
                {
                }
            }
        }

        private void ResetInboundState()
        {
            ReceivedCount = 0;
            ForwardedOutboundCount = 0;
            ForwardedOutboundCrcCount = 0;
            SentCount = 0;
            LastSentOpcode = -1;
            lock (_sync)
            {
                _recentOutboundPackets.Clear();
            }

            while (_pendingMessages.TryDequeue(out _))
            {
            }
        }

        private static bool TryResolveProcessSelector(string selector, out int? owningProcessId, out string owningProcessName, out string error)
        {
            owningProcessId = null;
            owningProcessName = null;
            error = null;
            if (string.IsNullOrWhiteSpace(selector))
            {
                owningProcessName = "MapleStory";
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
                error = "Trading-room official-session discovery requires a process name or pid when a selector is provided.";
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

        private static bool TryDecodeOpcode(byte[] rawPacket, out int opcode, out byte[] payload)
        {
            opcode = 0;
            payload = Array.Empty<byte>();
            if (rawPacket == null || rawPacket.Length < sizeof(short))
            {
                return false;
            }

            opcode = BitConverter.ToUInt16(rawPacket, 0);
            payload = rawPacket.Skip(sizeof(short)).ToArray();
            return true;
        }

        private static bool TryValidateOutboundRawPacket(byte[] rawPacket, out int opcode, out byte packetType, out string error)
        {
            opcode = 0;
            packetType = 0;
            error = null;
            if (!TryDecodeOpcode(rawPacket, out opcode, out byte[] payload))
            {
                error = "Trading-room outbound packet requires an opcode-wrapped frame.";
                return false;
            }

            if (opcode != OutboundTradingRoomOpcode)
            {
                error = $"Trading-room outbound packet opcode must be {OutboundTradingRoomOpcode}, but was {opcode}.";
                return false;
            }

            if (payload.Length == 0)
            {
                error = "Trading-room outbound payload is empty.";
                return false;
            }

            packetType = payload[0];
            return true;
        }

        private static bool IsTradingRoomInboundSubtype(byte packetType)
        {
            return packetType is 15 or 16 or 17 or 20 or 21;
        }

        private static MapleCrypto CreateCrypto(byte[] iv, short version)
        {
            return new MapleCrypto((byte[])iv.Clone(), version);
        }
    }
}
