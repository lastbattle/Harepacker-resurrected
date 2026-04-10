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
    public sealed class FieldMessageBoxPacketInboxMessage
    {
        public FieldMessageBoxPacketInboxMessage(ushort opcode, byte[] payload, string source, string rawText)
        {
            Opcode = opcode;
            Payload = payload ?? Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "unknown" : source;
            RawText = rawText ?? string.Empty;
        }

        public ushort Opcode { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    /// <summary>
    /// Proxies a live Maple session and peels CMessageBoxPool::OnPacket opcodes
    /// into the existing field message-box runtime.
    /// </summary>
    public sealed class FieldMessageBoxOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18489;
        public const ushort CreateFailedOpcode = 325;
        public const ushort EnterFieldOpcode = 326;
        public const ushort LeaveFieldOpcode = 327;
        private const int MaxRecentOutboundPackets = 32;
        private const string DefaultProcessName = "MapleStory";
        private const int AddressFamilyInet = 2;
        private const int ErrorInsufficientBuffer = 122;

        private readonly ConcurrentQueue<FieldMessageBoxPacketInboxMessage> _pendingMessages = new();
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
        public bool HasConnectedSession => _activePair?.InitCompleted == true;
        public int ReceivedCount { get; private set; }
        public int ForwardedOutboundCount { get; private set; }
        public int InjectedOutboundCount { get; private set; }
        public string LastStatus { get; private set; } = "Field message-box official-session bridge inactive.";

        public string DescribeStatus()
        {
            string lifecycle = IsRunning
                ? $"listening on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}"
                : "inactive";
            string session = HasConnectedSession
                ? $"connected session {_activePair?.ClientEndpoint ?? "unknown-client"} -> {_activePair?.RemoteEndpoint ?? "unknown-remote"}"
                : "no active Maple session";
            return $"Field message-box official-session bridge {lifecycle}; {session}; received={ReceivedCount}; forwardedOutbound={ForwardedOutboundCount}; injectedOutbound={InjectedOutboundCount}. {LastStatus}";
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
                    LastStatus = $"Field message-box official-session bridge listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    LastStatus = $"Field message-box official-session bridge failed to start: {ex.Message}";
                }
            }
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

            Start(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port);
            status = $"Field message-box official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {LastStatus}";
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
                LastStatus = "Field message-box official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out FieldMessageBoxPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(string source, bool success, string message)
        {
            string summary = string.IsNullOrWhiteSpace(message) ? "message-box payload" : message;
            LastStatus = success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.";
        }

        public string DescribeRecentOutboundPackets(int maxCount = 10)
        {
            int normalizedCount = Math.Clamp(maxCount, 1, MaxRecentOutboundPackets);
            lock (_sync)
            {
                if (_recentOutboundPackets.Count == 0)
                {
                    return "Field message-box official-session bridge outbound history is empty.";
                }

                OutboundPacketTrace[] entries = _recentOutboundPackets
                    .Reverse()
                    .Take(normalizedCount)
                    .ToArray();
                return "Field message-box official-session bridge outbound history:"
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        entries.Select(entry =>
                            $"opcode={entry.Opcode} payloadLen={entry.PayloadLength} source={entry.Source} payloadHex={entry.PayloadHex} raw={entry.RawPacketHex}"));
            }
        }

        public string ClearRecentOutboundPackets()
        {
            lock (_sync)
            {
                _recentOutboundPackets.Clear();
            }

            LastStatus = "Field message-box official-session bridge outbound history cleared.";
            return LastStatus;
        }

        public bool TryReplayRecentOutboundPacket(int historyIndexFromNewest, out string status)
        {
            if (historyIndexFromNewest <= 0)
            {
                status = "Field message-box replay index must be 1 or greater.";
                LastStatus = status;
                return false;
            }

            OutboundPacketTrace[] entries;
            lock (_sync)
            {
                if (_recentOutboundPackets.Count == 0)
                {
                    status = "No captured field message-box outbound client packets are available to replay.";
                    LastStatus = status;
                    return false;
                }

                if (historyIndexFromNewest > _recentOutboundPackets.Count)
                {
                    status = $"Field message-box replay index {historyIndexFromNewest} exceeds the {_recentOutboundPackets.Count} captured outbound packet(s).";
                    LastStatus = status;
                    return false;
                }

                entries = _recentOutboundPackets.ToArray();
            }

            OutboundPacketTrace trace = entries[^historyIndexFromNewest];
            if (string.IsNullOrWhiteSpace(trace.RawPacketHex))
            {
                status = $"Captured field message-box outbound packet {historyIndexFromNewest} has no raw payload to replay.";
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
                status = $"Captured field message-box outbound packet {historyIndexFromNewest} could not be replayed: {ex.Message}";
                LastStatus = status;
                return false;
            }
        }

        public bool TrySendOutboundRawPacket(byte[] rawPacket, out string status)
        {
            if (!TryDecodeOpcode(rawPacket, out int opcode, out _))
            {
                status = "Field message-box outbound injection requires a raw packet with a 2-byte opcode header.";
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
                status = "Field message-box official-session bridge has no connected Maple session for outbound injection.";
                LastStatus = status;
                return false;
            }

            byte[] clonedRawPacket = (byte[])rawPacket.Clone();
            try
            {
                pair.ServerSession.SendPacket(clonedRawPacket);
                InjectedOutboundCount++;
                RecordObservedOutboundPacket(clonedRawPacket, "simulator-send");
                status = $"Injected field message-box outbound opcode {opcode} into live session {pair.RemoteEndpoint}.";
                LastStatus = status;
                return true;
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Field message-box official-session outbound injection failed: {ex.Message}");
                status = LastStatus;
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
                LastStatus = $"Field message-box official-session bridge error: {ex.Message}";
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
                        LastStatus = "Rejected field message-box official-session client because a live Maple session is already attached.";
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
                clientSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Field message-box official-session client disconnected: {pair.ClientEndpoint}.");
                serverSession.OnPacketReceived += (packet, isInit) => HandleServerPacket(pair, packet, isInit);
                serverSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Field message-box official-session server disconnected: {pair.RemoteEndpoint}.");

                lock (_sync)
                {
                    _activePair = pair;
                }

                LastStatus = $"Field message-box official-session bridge connected {pair.ClientEndpoint} -> {pair.RemoteEndpoint}. Waiting for Maple init packet.";
                serverSession.WaitForDataNoEncryption();
            }
            catch (Exception ex)
            {
                client.Close();
                pair?.Close();
                LastStatus = $"Field message-box official-session bridge connect failed: {ex.Message}";
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
                    LastStatus = $"Field message-box official-session bridge initialized Maple crypto for {pair.ClientEndpoint} <-> {pair.RemoteEndpoint}.";
                    pair.ClientSession.WaitForData();
                    return;
                }

                pair.ClientSession.SendPacket((byte[])raw.Clone());

                if (!TryDecodeOpcode(raw, out int opcode, out byte[] payload)
                    || !IsMessageBoxOpcode(opcode))
                {
                    return;
                }

                _pendingMessages.Enqueue(new FieldMessageBoxPacketInboxMessage(
                    (ushort)opcode,
                    payload,
                    $"official-session:{pair.RemoteEndpoint}",
                    $"packetrecv {opcode} {Convert.ToHexString(payload)}"));
                ReceivedCount++;
                LastStatus = $"Queued field message-box opcode {opcode} from live session {pair.RemoteEndpoint}.";
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Field message-box official-session server handling failed: {ex.Message}");
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
                ClearActivePair(pair, $"Field message-box official-session client handling failed: {ex.Message}");
            }
        }

        private void RecordObservedOutboundPacket(byte[] rawPacket, string source)
        {
            if (!TryDecodeOpcode(rawPacket, out int opcode, out byte[] payload))
            {
                return;
            }

            ForwardedOutboundCount++;

            lock (_sync)
            {
                while (_recentOutboundPackets.Count >= MaxRecentOutboundPackets)
                {
                    _recentOutboundPackets.Dequeue();
                }

                _recentOutboundPackets.Enqueue(new OutboundPacketTrace(
                    opcode,
                    payload.Length,
                    Convert.ToHexString(payload),
                    Convert.ToHexString(rawPacket),
                    source));
            }

            LastStatus = $"Forwarded live field message-box outbound opcode {opcode} from {source}.";
        }

        internal void RecordObservedOutboundPacketForTest(byte[] rawPacket, string source = "test")
        {
            RecordObservedOutboundPacket(rawPacket, source);
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

                ReceivedCount = 0;
            }
        }

        private static bool IsMessageBoxOpcode(int opcode)
        {
            return opcode == CreateFailedOpcode
                || opcode == EnterFieldOpcode
                || opcode == LeaveFieldOpcode;
        }

        private static MapleCrypto CreateCrypto(byte[] iv, short version)
        {
            return new MapleCrypto(iv, (short)(0xFFFF - version));
        }

        private static IReadOnlyList<TcpRowOwnerPid> EnumerateTcpRows()
        {
            int size = 0;
            int result = GetExtendedTcpTable(IntPtr.Zero, ref size, sort: true, AddressFamilyInet, TcpTableClass.TcpTableOwnerPidAll, 0);
            if (result != 0 && result != ErrorInsufficientBuffer)
            {
                return Array.Empty<TcpRowOwnerPid>();
            }

            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                result = GetExtendedTcpTable(buffer, ref size, sort: true, AddressFamilyInet, TcpTableClass.TcpTableOwnerPidAll, 0);
                if (result != 0)
                {
                    return Array.Empty<TcpRowOwnerPid>();
                }

                int rowCount = Marshal.ReadInt32(buffer);
                IntPtr rowPointer = IntPtr.Add(buffer, sizeof(int));
                int rowSize = Marshal.SizeOf<TcpRowOwnerPid>();
                List<TcpRowOwnerPid> rows = new(rowCount);
                for (int i = 0; i < rowCount; i++)
                {
                    rows.Add(Marshal.PtrToStructure<TcpRowOwnerPid>(rowPointer));
                    rowPointer = IntPtr.Add(rowPointer, rowSize);
                }

                return rows;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static int DecodePort(byte[] portBytes)
        {
            if (portBytes == null || portBytes.Length < 2)
            {
                return 0;
            }

            return (portBytes[0] << 8) | portBytes[1];
        }

        private static IPAddress DecodeAddress(uint address)
        {
            byte[] bytes = BitConverter.GetBytes(address);
            return new IPAddress(bytes);
        }

        private static bool TryResolveProcess(int processId, out string processName)
        {
            processName = null;
            try
            {
                Process process = Process.GetProcessById(processId);
                processName = process.ProcessName;
                return !string.IsNullOrWhiteSpace(processName);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryResolveProcessSelector(string processSelector, out int? processId, out string processName, out string error)
        {
            processId = null;
            processName = null;
            error = null;

            if (string.IsNullOrWhiteSpace(processSelector))
            {
                processName = DefaultProcessName;
                return true;
            }

            if (int.TryParse(processSelector, out int parsedProcessId) && parsedProcessId > 0)
            {
                processId = parsedProcessId;
                return true;
            }

            string normalized = NormalizeProcessSelector(processSelector);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                error = "Process selector must be a positive pid or process name.";
                return false;
            }

            processName = normalized;
            return true;
        }

        private static string NormalizeProcessSelector(string processSelector)
        {
            if (string.IsNullOrWhiteSpace(processSelector))
            {
                return null;
            }

            string trimmed = processSelector.Trim();
            return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? trimmed[..^4]
                : trimmed;
        }

        private static bool TryResolveDiscoveryCandidate(
            IReadOnlyList<SessionDiscoveryCandidate> candidates,
            int remotePort,
            int? owningProcessId,
            string owningProcessName,
            int? localPort,
            out SessionDiscoveryCandidate candidate,
            out string status)
        {
            IReadOnlyList<SessionDiscoveryCandidate> filteredCandidates = FilterCandidatesByLocalPort(candidates, localPort);
            if (filteredCandidates.Count == 1)
            {
                candidate = filteredCandidates[0];
                status = $"Resolved 1 established Maple session for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
                return true;
            }

            if (filteredCandidates.Count == 0)
            {
                candidate = default;
                status = $"No established Maple sessions found for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
                return false;
            }

            candidate = default;
            status = $"Found {filteredCandidates.Count} established Maple sessions for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}. Refine the process selector or local port.";
            return false;
        }

        private static IReadOnlyList<SessionDiscoveryCandidate> FilterCandidatesByLocalPort(
            IReadOnlyList<SessionDiscoveryCandidate> candidates,
            int? localPort)
        {
            if (!localPort.HasValue || localPort.Value <= 0)
            {
                return candidates ?? Array.Empty<SessionDiscoveryCandidate>();
            }

            return (candidates ?? Array.Empty<SessionDiscoveryCandidate>())
                .Where(candidate => candidate.LocalEndpoint.Port == localPort.Value)
                .ToArray();
        }

        private static string DescribeDiscoveryCandidates(
            IReadOnlyList<SessionDiscoveryCandidate> candidates,
            int remotePort,
            int? owningProcessId,
            string owningProcessName,
            int? localPort)
        {
            IReadOnlyList<SessionDiscoveryCandidate> filteredCandidates = FilterCandidatesByLocalPort(candidates, localPort);
            if (filteredCandidates.Count == 0)
            {
                return $"No established Maple sessions found for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
            }

            return string.Join(
                Environment.NewLine,
                filteredCandidates.Select(candidate =>
                    $"{candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}"));
        }

        private static string DescribeSelector(int? owningProcessId, string owningProcessName)
        {
            if (owningProcessId.HasValue)
            {
                return $"pid {owningProcessId.Value}";
            }

            return string.IsNullOrWhiteSpace(owningProcessName) ? "MapleStory" : owningProcessName;
        }

        private static string DescribeDiscoveryScope(int? owningProcessId, string owningProcessName, int remotePort, int? localPort)
        {
            string selectorLabel = DescribeSelector(owningProcessId, owningProcessName);
            return localPort.HasValue
                ? $"{selectorLabel} on remote port {remotePort} and local port {localPort.Value}"
                : $"{selectorLabel} on remote port {remotePort}";
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
