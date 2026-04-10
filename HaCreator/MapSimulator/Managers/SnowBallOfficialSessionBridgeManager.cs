using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using HaCreator.MapSimulator.Fields;
using MapleLib.MapleCryptoLib;
using MapleLib.PacketLib;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Built-in SnowBall transport bridge that proxies a live Maple session,
    /// peels inbound SnowBall packets into the existing packet-owned runtime seam,
    /// and can inject outbound touch packets without an external bridge.
    /// </summary>
    public sealed class SnowBallOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18489;

        private readonly ConcurrentQueue<SnowBallPacketInboxMessage> _pendingMessages = new();
        private readonly object _sync = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;
        private BridgePair _activePair;

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
        public bool HasConnectedSession => _activePair?.InitCompleted == true;
        public int ReceivedCount { get; private set; }
        public int SentCount { get; private set; }
        public string LastStatus { get; private set; } = "SnowBall official-session bridge inactive.";

        public string DescribeStatus()
        {
            string lifecycle = IsRunning
                ? $"listening on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}"
                : "inactive";
            string session = HasConnectedSession
                ? $"connected session {_activePair?.ClientEndpoint ?? "unknown-client"} -> {_activePair?.RemoteEndpoint ?? "unknown-remote"}"
                : "no live Maple session";
            return $"SnowBall official-session bridge {lifecycle}; {session}; received={ReceivedCount}; sent={SentCount}. {LastStatus}";
        }

        public static IReadOnlyList<SessionDiscoveryCandidate> DiscoverEstablishedSessions(
            int remotePort,
            int? owningProcessId = null,
            string owningProcessName = null)
        {
            return CoconutOfficialSessionBridgeManager
                .DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName)
                .Select(candidate => new SessionDiscoveryCandidate(
                    candidate.ProcessId,
                    candidate.ProcessName,
                    candidate.LocalEndpoint,
                    candidate.RemoteEndpoint))
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
                        status = $"SnowBall official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                        LastStatus = status;
                        return true;
                    }

                    status = $"SnowBall official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                    LastStatus = status;
                    return false;
                }

                if (IsRunning
                    && MatchesTargetConfiguration(ListenPort, RemoteHost, RemotePort, resolvedListenPort, resolvedRemoteHost, remotePort))
                {
                    status = $"SnowBall official-session bridge already listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
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
                    LastStatus = $"SnowBall official-session bridge listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                    status = LastStatus;
                    return true;
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    LastStatus = $"SnowBall official-session bridge failed to start: {ex.Message}";
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
                    status = $"SnowBall official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                    LastStatus = status;
                    return true;
                }

                status = $"SnowBall official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                LastStatus = status;
                return false;
            }

            if (MatchesDiscoveredTargetConfiguration(ListenPort, RemoteHost, RemotePort, resolvedListenPort, candidate.RemoteEndpoint) && IsRunning)
            {
                status = $"SnowBall official-session bridge remains armed for {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}.";
                LastStatus = status;
                return true;
            }

            if (!TryStart(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, out string startStatus))
            {
                status = $"SnowBall official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}, but startup failed. {startStatus}";
                LastStatus = status;
                return false;
            }

            status = $"SnowBall official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {startStatus}";
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
                LastStatus = "SnowBall official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out SnowBallPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public bool TrySendTouchRequest(SnowBallField.TouchPacketRequest request, out string status)
        {
            BridgePair pair = _activePair;
            if (pair == null || !pair.InitCompleted)
            {
                status = "SnowBall official-session bridge has no active Maple session.";
                LastStatus = status;
                return false;
            }

            try
            {
                PacketWriter writer = new PacketWriter();
                writer.WriteShort(SnowBallField.OutboundTouchOpcode);
                pair.ServerSession.SendPacket(writer.ToArray());
                SentCount++;
                status = $"Injected SnowBall opcode {SnowBallField.OutboundTouchOpcode} for team {request.Team} into live session {pair.RemoteEndpoint}.";
                LastStatus = status;
                return true;
            }
            catch (Exception ex)
            {
                status = $"SnowBall official-session touch injection failed: {ex.Message}";
                LastStatus = status;
                ClearActivePair(pair, status);
                return false;
            }
        }

        public void RecordDispatchResult(string source, int packetType, bool success, string message)
        {
            string packetLabel = packetType switch
            {
                SnowBallField.PacketTypeState => "SnowBall state",
                SnowBallField.PacketTypeHit => "SnowBall hit",
                SnowBallField.PacketTypeMessage => "SnowBall message",
                SnowBallField.PacketTypeTouch => "SnowBall touch",
                _ => $"SnowBall packet {packetType}"
            };
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
                LastStatus = $"SnowBall official-session bridge error: {ex.Message}";
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
                        LastStatus = "Rejected SnowBall official-session client because a live Maple session is already attached.";
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
                clientSession.OnClientDisconnected += _ => ClearActivePair(pair, $"SnowBall official-session client disconnected: {pair.ClientEndpoint}.");
                serverSession.OnPacketReceived += (packet, isInit) => HandleServerPacket(pair, packet, isInit);
                serverSession.OnClientDisconnected += _ => ClearActivePair(pair, $"SnowBall official-session server disconnected: {pair.RemoteEndpoint}.");

                lock (_sync)
                {
                    _activePair = pair;
                }

                LastStatus = $"SnowBall official-session bridge connected {pair.ClientEndpoint} -> {pair.RemoteEndpoint}. Waiting for Maple init packet.";
                serverSession.WaitForDataNoEncryption();
            }
            catch (Exception ex)
            {
                client.Close();
                pair?.Close();
                LastStatus = $"SnowBall official-session bridge connect failed: {ex.Message}";
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
                    LastStatus = $"SnowBall official-session bridge initialized Maple crypto for {pair.ClientEndpoint} <-> {pair.RemoteEndpoint}.";
                    pair.ClientSession.WaitForData();
                    return;
                }

                pair.ClientSession.SendPacket((byte[])raw.Clone());

                if (!TryDecodeOpcode(raw, out int opcode, out byte[] payload))
                {
                    return;
                }

                if (opcode != SnowBallField.PacketTypeState
                    && opcode != SnowBallField.PacketTypeHit
                    && opcode != SnowBallField.PacketTypeMessage
                    && opcode != SnowBallField.PacketTypeTouch)
                {
                    return;
                }

                byte[] relayPayload = SpecialFieldRuntimeCoordinator.BuildCurrentWrapperRelayPayload(opcode, payload);
                _pendingMessages.Enqueue(new SnowBallPacketInboxMessage(
                    SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode,
                    relayPayload,
                    $"official-session:{pair.RemoteEndpoint}",
                    $"packetraw {Convert.ToHexString(raw)}"));
                ReceivedCount++;
                LastStatus =
                    $"Queued CField::OnPacket opcode {SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode} relay for SnowBall opcode {opcode} from live session {pair.RemoteEndpoint}.";
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"SnowBall official-session server handling failed: {ex.Message}");
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

                if (TryDecodeOpcode(raw, out int opcode, out _)
                    && opcode == SnowBallField.OutboundTouchOpcode)
                {
                    LastStatus = $"Forwarded live SnowBall opcode {SnowBallField.OutboundTouchOpcode} from {pair.ClientEndpoint} to {pair.RemoteEndpoint}.";
                }
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"SnowBall official-session client handling failed: {ex.Message}");
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
                owningProcessName = "MapleStory";
                return true;
            }

            if (int.TryParse(selector, out int pid) && pid > 0)
            {
                owningProcessId = pid;
                return true;
            }

            string normalized = selector.Trim();
            if (normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[..^4];
            }

            if (normalized.Length == 0)
            {
                error = "SnowBall official-session discovery requires a process name or pid when a selector is provided.";
                return false;
            }

            owningProcessName = normalized;
            return true;
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

        internal static bool TryResolveDiscoveryCandidate(
            IReadOnlyList<SessionDiscoveryCandidate> candidates,
            int remotePort,
            int? owningProcessId,
            string owningProcessName,
            int? localPort,
            out SessionDiscoveryCandidate candidate,
            out string status)
        {
            IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> convertedCandidates =
                ConvertCandidates(candidates);
            bool resolved = CoconutOfficialSessionBridgeManager.TryResolveDiscoveryCandidate(
                convertedCandidates,
                remotePort,
                owningProcessId,
                owningProcessName,
                localPort,
                out CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate coconutCandidate,
                out status);
            candidate = resolved
                ? new SessionDiscoveryCandidate(
                    coconutCandidate.ProcessId,
                    coconutCandidate.ProcessName,
                    coconutCandidate.LocalEndpoint,
                    coconutCandidate.RemoteEndpoint)
                : default;
            if (!resolved && !string.IsNullOrWhiteSpace(status))
            {
                status = status.Replace("Coconut", "SnowBall", StringComparison.Ordinal);
                status = status.Replace("/coconut", "/snowball", StringComparison.Ordinal);
            }

            return resolved;
        }

        internal static string DescribeDiscoveryCandidates(
            IReadOnlyList<SessionDiscoveryCandidate> candidates,
            int remotePort,
            int? owningProcessId,
            string owningProcessName,
            int? localPort)
        {
            string description = CoconutOfficialSessionBridgeManager.DescribeDiscoveryCandidates(
                ConvertCandidates(candidates),
                remotePort,
                owningProcessId,
                owningProcessName,
                localPort);
            return description.Replace("Coconut", "SnowBall", StringComparison.Ordinal);
        }

        private static IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> ConvertCandidates(
            IReadOnlyList<SessionDiscoveryCandidate> candidates)
        {
            return (candidates ?? Array.Empty<SessionDiscoveryCandidate>())
                .Select(candidate => new CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate(
                    candidate.ProcessId,
                    candidate.ProcessName,
                    candidate.LocalEndpoint,
                    candidate.RemoteEndpoint))
                .ToArray();
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
    }
}
