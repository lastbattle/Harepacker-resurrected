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
    /// Proxies a live Maple session and forwards CUserLocal::OnPacket utility
    /// opcodes into the existing local-utility packet seam.
    /// </summary>
    public sealed class LocalUtilityOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18496;
        private const string DefaultProcessName = "MapleStory";
        private sealed record PendingOutboundPacket(int Opcode, byte[] RawPacket);

        private readonly ConcurrentQueue<LocalUtilityPacketInboxMessage> _pendingMessages = new();
        private readonly ConcurrentQueue<PendingOutboundPacket> _pendingOutboundPackets = new();
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

        public int ListenPort { get; private set; } = DefaultListenPort;
        public string RemoteHost { get; private set; } = IPAddress.Loopback.ToString();
        public int RemotePort { get; private set; }
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public bool HasAttachedClient => _activePair != null;
        public bool HasConnectedSession => _activePair?.InitCompleted == true;
        public int ReceivedCount { get; private set; }
        public int SentCount { get; private set; }
        public int LastSentOpcode { get; private set; } = -1;
        public byte[] LastSentRawPacket { get; private set; } = Array.Empty<byte>();
        public int QueuedCount { get; private set; }
        public int LastQueuedOpcode { get; private set; } = -1;
        public byte[] LastQueuedRawPacket { get; private set; } = Array.Empty<byte>();
        public int PendingPacketCount => _pendingOutboundPackets.Count;
        public string LastStatus { get; private set; } = "Local utility official-session bridge inactive.";

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
            return $"Local utility official-session bridge {lifecycle}; {session}; received={ReceivedCount}; sent={SentCount}; pending={PendingPacketCount}; queued={QueuedCount}; inbound opcodes=193,253,254,269,270,1023; outbound opcodes=45,113,117,134,135,1023.{lastOutbound}{lastQueued} {LastStatus}";
        }

        public void Start(int listenPort, string remoteHost, int remotePort)
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
                    _listenerCancellation = new CancellationTokenSource();
                    _listener = new TcpListener(IPAddress.Loopback, ListenPort);
                    _listener.Start();
                    _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                    LastStatus = $"Local utility official-session bridge listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: false);
                    LastStatus = $"Local utility official-session bridge failed to start: {ex.Message}";
                }
            }
        }

        public bool TryStartFromDiscovery(int listenPort, int remotePort, string processSelector, int? localPort, out string status)
        {
            return TryRefreshFromDiscovery(listenPort, remotePort, processSelector, localPort, out status);
        }

        public bool TryRefreshFromDiscovery(int listenPort, int remotePort, string processSelector, int? localPort, out string status)
        {
            if (HasAttachedClient)
            {
                status = $"Local utility official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
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
                status = $"Local utility official-session bridge remains armed for {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}.";
                LastStatus = status;
                return true;
            }

            Start(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port);
            status = $"Local utility official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {LastStatus}";
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
                LastStatus = "Local utility official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out LocalUtilityPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(string source, bool success, string detail)
        {
            string summary = string.IsNullOrWhiteSpace(detail) ? "local utility opcode" : detail;
            LastStatus = success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.";
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
                status = "Local utility official-session bridge has no connected Maple session for outbound injection.";
                LastStatus = status;
                return false;
            }

            if (opcode < ushort.MinValue || opcode > ushort.MaxValue)
            {
                status = $"Local utility outbound opcode {opcode} is outside the 16-bit Maple packet range.";
                LastStatus = status;
                return false;
            }

            byte[] rawPacket = BuildRawPacket((ushort)opcode, payload);

            try
            {
                pair.ServerSession.SendPacket(rawPacket);
                SentCount++;
                LastSentOpcode = opcode;
                LastSentRawPacket = rawPacket;
                status = $"Injected local utility outbound opcode {opcode} into live session {pair.RemoteEndpoint}.";
                LastStatus = status;
                return true;
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Local utility official-session outbound injection failed: {ex.Message}");
                status = LastStatus;
                return false;
            }
        }

        public bool TryQueueOutboundPacket(int opcode, IReadOnlyList<byte> payload, out string status)
        {
            if (opcode < ushort.MinValue || opcode > ushort.MaxValue)
            {
                status = $"Local utility outbound opcode {opcode} is outside the 16-bit Maple packet range.";
                LastStatus = status;
                return false;
            }

            byte[] rawPacket = BuildRawPacket((ushort)opcode, payload);
            _pendingOutboundPackets.Enqueue(new PendingOutboundPacket(opcode, rawPacket));
            QueuedCount++;
            LastQueuedOpcode = opcode;
            LastQueuedRawPacket = rawPacket;
            status = $"Queued local utility outbound opcode {opcode} for deferred live-session injection.";
            LastStatus = status;
            return true;
        }

        public bool HasQueuedOutboundPacket(int opcode, IReadOnlyList<byte> rawPacket)
        {
            if (opcode < ushort.MinValue || opcode > ushort.MaxValue || rawPacket == null)
            {
                return false;
            }

            byte[] target = rawPacket as byte[] ?? rawPacket.ToArray();
            PendingOutboundPacket[] pending = _pendingOutboundPackets.ToArray();
            for (int i = 0; i < pending.Length; i++)
            {
                if (pending[i].Opcode == opcode && pending[i].RawPacket.AsSpan().SequenceEqual(target))
                {
                    return true;
                }
            }

            return false;
        }

        public bool WasLastSentOutboundPacket(int opcode, IReadOnlyList<byte> rawPacket)
        {
            if (opcode < ushort.MinValue || opcode > ushort.MaxValue || rawPacket == null || LastSentOpcode != opcode)
            {
                return false;
            }

            byte[] target = rawPacket as byte[] ?? rawPacket.ToArray();
            return LastSentRawPacket.AsSpan().SequenceEqual(target);
        }

        internal static byte[] BuildFollowCharacterRequestPayload(int driverId, bool autoRequest, bool keyInput)
        {
            byte[] payload = new byte[sizeof(int) + sizeof(byte) + sizeof(byte)];
            BitConverter.GetBytes(driverId).CopyTo(payload, 0);
            payload[sizeof(int)] = autoRequest ? (byte)1 : (byte)0;
            payload[sizeof(int) + sizeof(byte)] = keyInput ? (byte)1 : (byte)0;
            return payload;
        }

        internal static byte[] BuildFollowCharacterWithdrawPayload()
        {
            return Array.Empty<byte>();
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
                LastStatus = $"Local utility official-session bridge error: {ex.Message}";
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
                        LastStatus = "Rejected local utility official-session client because a live Maple session is already attached.";
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
                clientSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Local utility official-session client disconnected: {pair.ClientEndpoint}.");
                serverSession.OnPacketReceived += (packet, isInit) => HandleServerPacket(pair, packet, isInit);
                serverSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Local utility official-session server disconnected: {pair.RemoteEndpoint}.");

                lock (_sync)
                {
                    _activePair = pair;
                }

                LastStatus = $"Local utility official-session bridge connected {pair.ClientEndpoint} -> {pair.RemoteEndpoint}. Waiting for Maple init packet.";
                serverSession.WaitForDataNoEncryption();
            }
            catch (Exception ex)
            {
                client.Close();
                pair?.Close();
                LastStatus = $"Local utility official-session bridge connect failed: {ex.Message}";
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
                        ? $"Local utility official-session bridge initialized Maple crypto for {pair.ClientEndpoint} <-> {pair.RemoteEndpoint} and flushed {flushed} queued outbound packet(s)."
                        : $"Local utility official-session bridge initialized Maple crypto for {pair.ClientEndpoint} <-> {pair.RemoteEndpoint}.";
                    pair.ClientSession.WaitForData();
                    return;
                }

                pair.ClientSession.SendPacket((byte[])raw.Clone());

                if (!LocalUtilityPacketInboxManager.TryDecodeOpcodeFramedPacket(raw, out int packetType, out byte[] payload, out _)
                    || !IsBridgeOpcode(packetType))
                {
                    return;
                }

                _pendingMessages.Enqueue(new LocalUtilityPacketInboxMessage(
                    packetType,
                    payload,
                    $"official-session:{pair.RemoteEndpoint}",
                    $"packetclientraw {Convert.ToHexString(raw)}"));
                ReceivedCount++;
                LastStatus = $"Queued {DescribePacketType(packetType)} from live session {pair.RemoteEndpoint}.";
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Local utility official-session server handling failed: {ex.Message}");
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
                ClearActivePair(pair, $"Local utility official-session client handling failed: {ex.Message}");
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
                ResetInboundState();
                while (_pendingOutboundPackets.TryDequeue(out _))
                {
                }

                SentCount = 0;
                LastSentOpcode = -1;
                LastSentRawPacket = Array.Empty<byte>();
                QueuedCount = 0;
                LastQueuedOpcode = -1;
                LastQueuedRawPacket = Array.Empty<byte>();
            }
        }

        private int FlushQueuedOutboundPackets(BridgePair pair)
        {
            if (pair == null || !pair.InitCompleted)
            {
                return 0;
            }

            int flushed = 0;
            while (_pendingOutboundPackets.TryPeek(out PendingOutboundPacket packet))
            {
                pair.ServerSession.SendPacket(packet.RawPacket);
                if (!_pendingOutboundPackets.TryDequeue(out PendingOutboundPacket dequeuedPacket))
                {
                    break;
                }

                SentCount++;
                LastSentOpcode = dequeuedPacket.Opcode;
                LastSentRawPacket = dequeuedPacket.RawPacket;
                flushed++;
            }

            return flushed;
        }

        private void ResetInboundState()
        {
            while (_pendingMessages.TryDequeue(out _))
            {
            }

            ReceivedCount = 0;
        }

        private static byte[] BuildRawPacket(ushort opcode, IReadOnlyList<byte> payload)
        {
            int payloadLength = payload?.Count ?? 0;
            byte[] raw = new byte[sizeof(ushort) + payloadLength];
            BitConverter.GetBytes(opcode).CopyTo(raw, 0);
            for (int i = 0; i < payloadLength; i++)
            {
                raw[sizeof(ushort) + i] = payload[i];
            }

            return raw;
        }

        private static MapleCrypto CreateCrypto(byte[] iv, short version)
        {
            return new MapleCrypto((byte[])iv.Clone(), version);
        }

        private static bool IsBridgeOpcode(int packetType)
        {
            return packetType == LocalUtilityPacketInboxManager.FollowCharacterClientPacketType
                || packetType == LocalUtilityPacketInboxManager.SetDirectionModeClientPacketType
                || packetType == LocalUtilityPacketInboxManager.SetStandAloneModeClientPacketType
                || packetType == LocalUtilityPacketInboxManager.PassiveMoveClientPacketType
                || packetType == LocalUtilityPacketInboxManager.FollowCharacterFailedClientPacketType
                || packetType == LocalUtilityPacketInboxManager.MechanicEquipStatePacketType;
        }

        private static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
                LocalUtilityPacketInboxManager.FollowCharacterClientPacketType => "FollowCharacter(193)",
                LocalUtilityPacketInboxManager.SetDirectionModeClientPacketType => "SetDirectionMode(253)",
                LocalUtilityPacketInboxManager.SetStandAloneModeClientPacketType => "SetStandAloneMode(254)",
                LocalUtilityPacketInboxManager.PassiveMoveClientPacketType => "PassiveMove(269)",
                LocalUtilityPacketInboxManager.FollowCharacterFailedClientPacketType => "FollowCharacterFailed(270)",
                LocalUtilityPacketInboxManager.MechanicEquipStatePacketType => "MechanicEquipState(1023)",
                _ => $"packet {packetType}"
            };
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
                error = "Local utility official-session discovery requires a process name or pid when a selector is provided.";
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
                status = $"Local utility official-session discovery found no established TCP session for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
                candidate = default;
                return false;
            }

            if (filteredCandidates.Count > 1)
            {
                string matches = string.Join(", ", filteredCandidates.Select(match =>
                    $"{match.RemoteEndpoint.Address}:{match.RemoteEndpoint.Port} via {match.LocalEndpoint.Address}:{match.LocalEndpoint.Port}"));
                status = $"Local utility official-session discovery found multiple candidates for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}: {matches}. Use /localutility session discover to inspect them, or add a localPort filter.";
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
