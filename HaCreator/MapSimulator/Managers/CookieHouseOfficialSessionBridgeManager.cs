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
    /// Built-in Cookie House transport bridge that proxies a live Maple session
    /// and feeds a configured inbound point opcode into the existing context-owned
    /// Cookie House score seam.
    /// </summary>
    public sealed class CookieHouseOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18496;
        private const string DefaultProcessName = "MapleStory";
        private const int RecentPacketCapacity = 8;

        private readonly ConcurrentQueue<CookieHousePointInboxMessage> _pendingMessages = new();
        private readonly Queue<string> _recentPackets = new();
        private readonly HashSet<int> _mappedInboundPointOpcodes = new();
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
        public string LastStatus { get; private set; } = "Cookie House official-session bridge inactive.";

        public string DescribeStatus()
        {
            string lifecycle = IsRunning
                ? $"listening on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}"
                : "inactive";
            string session = HasConnectedSession
                ? $"connected session {_activePair?.ClientEndpoint ?? "unknown-client"} -> {_activePair?.RemoteEndpoint ?? "unknown-remote"}"
                : "no live Maple session";
            return $"Cookie House official-session bridge {lifecycle}; {session}; received={ReceivedCount}; mapped point opcodes={DescribeMappedInboundPointOpcodes()}; recent={DescribeRecentPackets()}. {LastStatus}";
        }

        public string DescribeMappedInboundPointOpcodes()
        {
            lock (_sync)
            {
                return DescribeMappedInboundPointOpcodesNoLock();
            }
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

        public void SetMappedInboundPointOpcodes(IEnumerable<int> opcodes)
        {
            lock (_sync)
            {
                _mappedInboundPointOpcodes.Clear();
                foreach (int opcode in opcodes ?? Enumerable.Empty<int>())
                {
                    if (opcode > 0 && opcode <= ushort.MaxValue)
                    {
                        _mappedInboundPointOpcodes.Add(opcode);
                    }
                }

                LastStatus = $"Cookie House official-session bridge mapped point opcodes: {DescribeMappedInboundPointOpcodesNoLock()}.";
            }
        }

        public void ClearMappedInboundPointOpcodes()
        {
            lock (_sync)
            {
                _mappedInboundPointOpcodes.Clear();
                LastStatus = "Cookie House official-session bridge cleared mapped point opcodes.";
            }
        }

        public void ClearRecentPackets()
        {
            lock (_sync)
            {
                _recentPackets.Clear();
                LastStatus = "Cookie House official-session bridge cleared recent packet history.";
            }
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
                        status = $"Cookie House official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                        LastStatus = status;
                        return true;
                    }

                    status = $"Cookie House official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                    LastStatus = status;
                    return false;
                }

                if (IsRunning
                    && MatchesTargetConfiguration(ListenPort, RemoteHost, RemotePort, resolvedListenPort, resolvedRemoteHost, remotePort))
                {
                    status = $"Cookie House official-session bridge already listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
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
                    LastStatus = $"Cookie House official-session bridge listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                    status = LastStatus;
                    return true;
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    LastStatus = $"Cookie House official-session bridge failed to start: {ex.Message}";
                    status = LastStatus;
                    return false;
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

            IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates =
                CoconutOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            if (!CoconutOfficialSessionBridgeManager.TryResolveDiscoveryCandidate(candidates, remotePort, owningProcessId, owningProcessName, localPort, out CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate candidate, out status))
            {
                LastStatus = status;
                return false;
            }

            int resolvedListenPort = listenPort <= 0 ? DefaultListenPort : listenPort;
            if (HasAttachedClient)
            {
                if (MatchesDiscoveredTargetConfiguration(ListenPort, RemoteHost, RemotePort, resolvedListenPort, candidate.RemoteEndpoint))
                {
                    status = $"Cookie House official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                    LastStatus = status;
                    return true;
                }

                status = $"Cookie House official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                LastStatus = status;
                return false;
            }

            if (IsRunning
                && MatchesDiscoveredTargetConfiguration(ListenPort, RemoteHost, RemotePort, resolvedListenPort, candidate.RemoteEndpoint))
            {
                status = $"Cookie House official-session bridge remains armed for {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}.";
                LastStatus = status;
                return true;
            }

            if (!TryStart(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, out string startStatus))
            {
                status = $"Cookie House official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}, but startup failed. {startStatus}";
                LastStatus = status;
                return false;
            }

            status = $"Cookie House official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {startStatus}";
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

            IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates =
                CoconutOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            return CoconutOfficialSessionBridgeManager.DescribeDiscoveryCandidates(candidates, remotePort, owningProcessId, owningProcessName, localPort);
        }

        public void Stop()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
                LastStatus = "Cookie House official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out CookieHousePointInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(string source, bool success, string detail)
        {
            string summary = string.IsNullOrWhiteSpace(detail) ? "point update" : detail;
            LastStatus = success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.";
        }

        public static bool TryBuildInboundPointMessageFromRawPacket(
            byte[] rawPacket,
            int expectedOpcode,
            string source,
            out CookieHousePointInboxMessage message,
            out string error)
        {
            HashSet<int> mappedOpcodes = new() { expectedOpcode };
            return TryBuildInboundPointMessageFromRawPacket(rawPacket, mappedOpcodes, source, out message, out _, out error);
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
                LastStatus = $"Cookie House official-session bridge error: {ex.Message}";
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
                        LastStatus = "Rejected Cookie House official-session client because a live Maple session is already attached.";
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
                clientSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Cookie House official-session client disconnected: {pair.ClientEndpoint}.");
                serverSession.OnPacketReceived += (packet, isInit) => HandleServerPacket(pair, packet, isInit);
                serverSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Cookie House official-session server disconnected: {pair.RemoteEndpoint}.");

                lock (_sync)
                {
                    _activePair = pair;
                }

                LastStatus = $"Cookie House official-session bridge connected {pair.ClientEndpoint} -> {pair.RemoteEndpoint}. Waiting for Maple init packet.";
                serverSession.WaitForDataNoEncryption();
            }
            catch (Exception ex)
            {
                client.Close();
                pair?.Close();
                LastStatus = $"Cookie House official-session bridge connect failed: {ex.Message}";
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
                    LastStatus = $"Cookie House official-session bridge initialized Maple crypto for {pair.ClientEndpoint} <-> {pair.RemoteEndpoint}.";
                    pair.ClientSession.WaitForData();
                    return;
                }

                pair.ClientSession.SendPacket((byte[])raw.Clone());

                if (!TryDecodeOpcode(raw, out int opcode, out byte[] payload))
                {
                    return;
                }

                bool mapped;
                lock (_sync)
                {
                    mapped = _mappedInboundPointOpcodes.Contains(opcode);
                    RecordRecentPacketNoLock(opcode, payload.Length, mapped, raw);
                }

                if (!mapped)
                {
                    return;
                }

                if (!TryBuildInboundPointMessageFromRawPacket(raw, _mappedInboundPointOpcodes, $"official-session:{pair.RemoteEndpoint}", out CookieHousePointInboxMessage message, out int resolvedOpcode, out string error))
                {
                    LastStatus = $"Ignored Cookie House live packet opcode {opcode}: {error}";
                    return;
                }

                _pendingMessages.Enqueue(message);
                ReceivedCount++;
                LastStatus = $"Queued Cookie House point {message.Point} from live session {pair.RemoteEndpoint} via opcode {resolvedOpcode}.";
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Cookie House official-session server handling failed: {ex.Message}");
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
                ClearActivePair(pair, $"Cookie House official-session client handling failed: {ex.Message}");
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

        private static bool TryBuildInboundPointMessageFromRawPacket(
            byte[] rawPacket,
            ISet<int> mappedOpcodes,
            string source,
            out CookieHousePointInboxMessage message,
            out int opcode,
            out string error)
        {
            message = null;
            error = null;
            opcode = 0;

            if (!TryDecodeOpcode(rawPacket, out opcode, out byte[] payload))
            {
                error = "Cookie House live packet must contain a 2-byte opcode prefix.";
                return false;
            }

            if (mappedOpcodes == null || mappedOpcodes.Count == 0)
            {
                error = "Cookie House live packet bridge has no mapped inbound point opcode.";
                return false;
            }

            if (!mappedOpcodes.Contains(opcode))
            {
                error = $"opcode {opcode} is not mapped as a Cookie House point carrier.";
                return false;
            }

            if (payload.Length != CookieHousePointInboxManager.ClientContextPointByteLength)
            {
                error = $"Cookie House live opcode {opcode} payload must be exactly {CookieHousePointInboxManager.ClientContextPointByteLength} bytes for CWvsContext+0x{CookieHousePointInboxManager.ClientContextPointOffset:X}.";
                return false;
            }

            int point = Math.Max(0, BitConverter.ToInt32(payload, 0));
            message = new CookieHousePointInboxMessage(
                point,
                string.IsNullOrWhiteSpace(source) ? "official-session:unknown-remote" : source,
                $"packetclientraw {Convert.ToHexString(rawPacket)}",
                CookieHousePointInboxPayloadKind.OpcodeFramedRawContextPoint);
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
            payload = rawPacket.Skip(sizeof(short)).ToArray();
            return true;
        }

        private void RecordRecentPacketNoLock(int opcode, int payloadLength, bool mapped, byte[] rawPacket)
        {
            string rawHex = rawPacket == null || rawPacket.Length == 0 ? string.Empty : Convert.ToHexString(rawPacket);
            if (rawHex.Length > 48)
            {
                rawHex = rawHex[..48] + "...";
            }

            _recentPackets.Enqueue($"{opcode}/0x{opcode:X} len={payloadLength} {(mapped ? "mapped" : "seen")} raw={rawHex}");
            while (_recentPackets.Count > RecentPacketCapacity)
            {
                _recentPackets.Dequeue();
            }
        }

        private string DescribeMappedInboundPointOpcodesNoLock()
        {
            if (_mappedInboundPointOpcodes.Count == 0)
            {
                return "none";
            }

            return string.Join(", ", _mappedInboundPointOpcodes.OrderBy(opcode => opcode).Select(opcode => $"{opcode}/0x{opcode:X}"));
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
                error = "Cookie House official-session discovery requires a process name or pid when a selector is provided.";
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
            return string.IsNullOrWhiteSpace(remoteHost)
                ? IPAddress.Loopback.ToString()
                : remoteHost.Trim();
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

        private static bool MatchesDiscoveredTargetConfiguration(
            int currentListenPort,
            string currentRemoteHost,
            int currentRemotePort,
            int requestedListenPort,
            IPEndPoint discoveredRemoteEndpoint)
        {
            if (discoveredRemoteEndpoint == null)
            {
                return false;
            }

            return MatchesTargetConfiguration(
                currentListenPort,
                currentRemoteHost,
                currentRemotePort,
                requestedListenPort,
                discoveredRemoteEndpoint.Address.ToString(),
                discoveredRemoteEndpoint.Port);
        }
    }
}
