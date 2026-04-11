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
    /// Built-in Massacre transport bridge that proxies a live Maple session
    /// and feeds inbound Massacre packets into the existing packet-owned seam.
    /// </summary>
    public sealed class MassacreOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18489;
        private const string DefaultProcessName = "MapleStory";
        private const int AddressFamilyInet = 2;
        private const int ErrorInsufficientBuffer = 122;
        private const int CurrentWrapperRelayOpcode = SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode;
        private const int PacketTypeIncGauge = 173;
        private const int PacketTypeResult = 174;
        private const string DiscoverCommandUsage = "/massacre session discover <remotePort> [processName|pid] [localPort]";
        private const string AttachCommandUsage = "/massacre session attach <remotePort> [processName|pid] [localPort]";
        private const string StartAutoCommandUsage = "/massacre session startauto <listenPort|0> <remotePort> [processName|pid] [localPort]";

        private readonly ConcurrentQueue<MassacrePacketInboxMessage> _pendingMessages = new();
        private readonly Dictionary<int, MassacrePacketInboxMessageKind> _mappedInboundOpcodes = new();
        private readonly object _sync = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;
        private BridgePair _activePair;
        private SessionDiscoveryCandidate? _passiveEstablishedSession;

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
        public int ReceivedCount { get; private set; }
        public string LastStatus { get; private set; } = "Massacre official-session bridge inactive.";

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
            return $"Massacre official-session bridge {lifecycle}; {session}; received={ReceivedCount}; mapped inbound opcodes={DescribeMappedInboundOpcodes()}. {LastStatus}";
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
                string resolvedRemoteHost = string.IsNullOrWhiteSpace(remoteHost) ? IPAddress.Loopback.ToString() : remoteHost.Trim();

                StopInternal(clearPending: true);

                try
                {
                    ListenPort = requestedListenPort;
                    RemoteHost = resolvedRemoteHost;
                    RemotePort = remotePort;
                    _listenerCancellation = new CancellationTokenSource();
                    _listener = new TcpListener(IPAddress.Loopback, autoSelectListenPort ? 0 : requestedListenPort);
                    _listener.Start();
                    ListenPort = (_listener.LocalEndpoint as IPEndPoint)?.Port ?? requestedListenPort;
                    _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                    LastStatus = $"Massacre official-session bridge listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                    status = LastStatus;
                    return true;
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    LastStatus = $"Massacre official-session bridge failed to start: {ex.Message}";
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

            if (!TryStart(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, out string startStatus))
            {
                status = $"Massacre official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}, but startup failed. {startStatus}";
                LastStatus = status;
                return false;
            }

            status = $"Massacre official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {startStatus}";
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
                status = "Massacre official-session attach requires an established Maple client socket pair.";
                LastStatus = status;
                return false;
            }

            lock (_sync)
            {
                if (HasAttachedClient)
                {
                    status = $"Massacre official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before observing an already-established socket pair.";
                    LastStatus = status;
                    return false;
                }

                StopInternal(clearPending: true);
                _passiveEstablishedSession = candidate;
                RemoteHost = candidate.RemoteEndpoint.Address.ToString();
                RemotePort = candidate.RemoteEndpoint.Port;
                LastStatus = $"Observed already-established Massacre Maple socket pair {candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}. This path cannot decrypt post-handshake Massacre clock/context/result traffic or inject wrapper packets after the Maple handshake; reconnect through the localhost proxy for live packet ownership.";
                status = LastStatus;
                return true;
            }
        }

        public bool TryAttachEstablishedSessionAndStartProxy(int listenPort, int remotePort, string processSelector, int? localPort, out string status)
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

            return TryAttachEstablishedSessionAndStartProxy(listenPort, candidate, out status);
        }

        public bool TryAttachEstablishedSessionAndStartProxy(int listenPort, SessionDiscoveryCandidate candidate, out string status)
        {
            if (candidate.LocalEndpoint == null || candidate.RemoteEndpoint == null || candidate.RemoteEndpoint.Port <= 0)
            {
                status = "Massacre official-session proxy attach requires an established Maple client socket pair.";
                LastStatus = status;
                return false;
            }

            if (listenPort < 0 || listenPort > ushort.MaxValue)
            {
                status = "Massacre official-session proxy attach listen port must be 0 or a valid TCP port.";
                LastStatus = status;
                return false;
            }

            lock (_sync)
            {
                if (HasAttachedClient)
                {
                    status = $"Massacre official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before preparing an already-established socket pair for reconnect.";
                    LastStatus = status;
                    return false;
                }

                StopInternal(clearPending: true);
                _passiveEstablishedSession = candidate;

                if (!TryStartProxyListener(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, out string startStatus))
                {
                    LastStatus = $"Observed already-established Massacre Maple socket pair {DescribeEstablishedSession(candidate)}, but reconnect proxy startup failed. {startStatus}";
                    status = LastStatus;
                    return false;
                }

                LastStatus =
                    $"Observed already-established Massacre Maple socket pair {DescribeEstablishedSession(candidate)}. " +
                    $"Armed localhost proxy on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}; reconnect Maple through this proxy to recover Massacre decrypt/inject ownership for clock/context/result traffic through the existing bridge seam.";
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

        public void Stop()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
                LastStatus = "Massacre official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out MassacrePacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public bool TryMapInboundOpcode(int opcode, MassacrePacketInboxMessageKind kind, out string status)
        {
            if (opcode < 0 || opcode > ushort.MaxValue)
            {
                status = "Massacre inbound opcode must fit in an unsigned 16-bit packet id.";
                return false;
            }

            if (!IsSupportedMappedKind(kind))
            {
                status = $"Massacre official-session bridge cannot map inbound opcode {opcode} to {kind}.";
                return false;
            }

            lock (_sync)
            {
                _mappedInboundOpcodes[opcode] = kind;
                status = $"Massacre official-session bridge maps inbound opcode 0x{opcode:X4} to {DescribeMappedKind(kind)}.";
                LastStatus = status;
                return true;
            }
        }

        public bool TryRemoveMappedInboundOpcode(int opcode, out string status)
        {
            lock (_sync)
            {
                if (_mappedInboundOpcodes.Remove(opcode))
                {
                    status = $"Massacre official-session bridge removed inbound opcode 0x{opcode:X4}.";
                    LastStatus = status;
                    return true;
                }
            }

            status = $"Massacre inbound opcode 0x{opcode:X4} is not currently mapped.";
            return false;
        }

        public void ClearMappedInboundOpcodes()
        {
            lock (_sync)
            {
                _mappedInboundOpcodes.Clear();
                LastStatus = "Massacre official-session bridge cleared mapped inbound opcodes.";
            }
        }

        public string DescribeMappedInboundOpcodes()
        {
            lock (_sync)
            {
                if (_mappedInboundOpcodes.Count == 0)
                {
                    return "none";
                }

                return string.Join(
                    ", ",
                    _mappedInboundOpcodes
                        .OrderBy(pair => pair.Key)
                        .Select(pair => $"0x{pair.Key:X4}->{DescribeMappedKind(pair.Value)}"));
            }
        }

        public void RecordDispatchResult(string source, int packetType, bool success, string message)
        {
            string packetLabel = packetType switch
            {
                CurrentWrapperRelayOpcode => $"CField::OnPacket relay {CurrentWrapperRelayOpcode}",
                PacketTypeIncGauge => "Massacre inc gauge",
                PacketTypeResult => "Massacre result",
                _ => $"Massacre packet {packetType}"
            };
            string summary = string.IsNullOrWhiteSpace(message) ? packetLabel : $"{packetLabel}: {message}";
            LastStatus = success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.";
        }

        public void RecordDispatchResult(string source, MassacrePacketInboxMessage message, bool success, string result)
        {
            string packetLabel = message?.Kind switch
            {
                MassacrePacketInboxMessageKind.ClockPayload => "Massacre clock payload",
                MassacrePacketInboxMessageKind.InfoPayload => "Massacre context payload",
                MassacrePacketInboxMessageKind.Packet => message.PacketType switch
                {
                    CurrentWrapperRelayOpcode => $"CField::OnPacket relay {CurrentWrapperRelayOpcode}",
                    PacketTypeIncGauge => "Massacre inc gauge",
                    PacketTypeResult => "Massacre result",
                    _ => $"Massacre packet {message.PacketType}"
                },
                _ => "Massacre official-session payload"
            };
            string summary = string.IsNullOrWhiteSpace(result) ? packetLabel : $"{packetLabel}: {result}";
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
                LastStatus = $"Massacre official-session bridge error: {ex.Message}";
            }
        }

        private bool TryStartProxyListener(int listenPort, string remoteHost, int remotePort, out string status)
        {
            bool autoSelectListenPort = listenPort <= 0;
            int requestedListenPort = autoSelectListenPort ? DefaultListenPort : listenPort;
            string resolvedRemoteHost = string.IsNullOrWhiteSpace(remoteHost) ? IPAddress.Loopback.ToString() : remoteHost.Trim();

            try
            {
                ListenPort = requestedListenPort;
                RemoteHost = resolvedRemoteHost;
                RemotePort = remotePort;
                _listenerCancellation = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Loopback, autoSelectListenPort ? 0 : requestedListenPort);
                _listener.Start();
                ListenPort = (_listener.LocalEndpoint as IPEndPoint)?.Port ?? requestedListenPort;
                _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                status = $"Massacre official-session bridge listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                LastStatus = status;
                return true;
            }
            catch (Exception ex)
            {
                StopInternal(clearPending: false);
                status = $"Massacre official-session bridge failed to start: {ex.Message}";
                LastStatus = status;
                return false;
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
                        LastStatus = "Rejected Massacre official-session client because a live Maple session is already attached.";
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
                clientSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Massacre official-session client disconnected: {pair.ClientEndpoint}.");
                serverSession.OnPacketReceived += (packet, isInit) => HandleServerPacket(pair, packet, isInit);
                serverSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Massacre official-session server disconnected: {pair.RemoteEndpoint}.");

                lock (_sync)
                {
                    _passiveEstablishedSession = null;
                    _activePair = pair;
                }

                LastStatus = $"Massacre official-session bridge connected {pair.ClientEndpoint} -> {pair.RemoteEndpoint}. Waiting for Maple init packet.";
                serverSession.WaitForDataNoEncryption();
            }
            catch (Exception ex)
            {
                client.Close();
                pair?.Close();
                LastStatus = $"Massacre official-session bridge connect failed: {ex.Message}";
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
                    LastStatus = $"Massacre official-session bridge initialized Maple crypto for {pair.ClientEndpoint} <-> {pair.RemoteEndpoint}.";
                    pair.ClientSession.WaitForData();
                    return;
                }

                pair.ClientSession.SendPacket((byte[])raw.Clone());

                if (!TryDecodeInboundMassacrePacket(raw, $"official-session:{pair.RemoteEndpoint}", GetMappedInboundOpcodesSnapshot(), out MassacrePacketInboxMessage message))
                {
                    return;
                }

                _pendingMessages.Enqueue(message);
                ReceivedCount++;
                LastStatus = $"Queued {DescribeMessage(message)} from live session {pair.RemoteEndpoint}.";
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Massacre official-session server handling failed: {ex.Message}");
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
                pair.ServerSession.SendPacket(packet.ToArray());
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Massacre official-session client handling failed: {ex.Message}");
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
            _passiveEstablishedSession = null;
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

        private static MapleCrypto CreateCrypto(byte[] iv, short version)
        {
            return new MapleCrypto((byte[])iv.Clone(), version);
        }

        internal static bool TryDecodeInboundMassacrePacket(byte[] rawPacket, string source, out MassacrePacketInboxMessage message)
        {
            return TryDecodeInboundMassacrePacket(rawPacket, source, null, out message);
        }

        internal static bool TryDecodeInboundMassacrePacket(
            byte[] rawPacket,
            string source,
            IReadOnlyDictionary<int, MassacrePacketInboxMessageKind> mappedInboundOpcodes,
            out MassacrePacketInboxMessage message)
        {
            message = null;
            if (rawPacket == null || rawPacket.Length < sizeof(short))
            {
                return false;
            }

            int opcode = BitConverter.ToUInt16(rawPacket, 0);
            if (opcode != CurrentWrapperRelayOpcode
                && opcode != PacketTypeIncGauge
                && opcode != PacketTypeResult
                && (mappedInboundOpcodes == null || !mappedInboundOpcodes.TryGetValue(opcode, out _)))
            {
                return false;
            }

            byte[] payload = rawPacket.Skip(sizeof(short)).ToArray();
            if (mappedInboundOpcodes != null
                && mappedInboundOpcodes.TryGetValue(opcode, out MassacrePacketInboxMessageKind mappedKind))
            {
                return TryBuildMappedInboundMessage(opcode, mappedKind, payload, source, rawPacket, out message);
            }

            if (opcode != CurrentWrapperRelayOpcode)
            {
                payload = SpecialFieldRuntimeCoordinator.BuildCurrentWrapperRelayPayload(opcode, payload);
                opcode = CurrentWrapperRelayOpcode;
            }

            message = new MassacrePacketInboxMessage(
                MassacrePacketInboxMessageKind.Packet,
                source,
                $"packetraw {Convert.ToHexString(rawPacket)}",
                packetType: opcode,
                payload: payload);
            return true;
        }

        private IReadOnlyDictionary<int, MassacrePacketInboxMessageKind> GetMappedInboundOpcodesSnapshot()
        {
            lock (_sync)
            {
                return _mappedInboundOpcodes.Count == 0
                    ? null
                    : new Dictionary<int, MassacrePacketInboxMessageKind>(_mappedInboundOpcodes);
            }
        }

        private static bool TryBuildMappedInboundMessage(
            int mappedOpcode,
            MassacrePacketInboxMessageKind mappedKind,
            byte[] payload,
            string source,
            byte[] rawPacket,
            out MassacrePacketInboxMessage message)
        {
            message = null;
            if (!IsSupportedMappedKind(mappedKind) || !HasExpectedMappedPayloadLength(mappedKind, payload))
            {
                return false;
            }

            int packetType = mappedKind switch
            {
                MassacrePacketInboxMessageKind.IncGauge => PacketTypeIncGauge,
                MassacrePacketInboxMessageKind.Result => PacketTypeResult,
                _ => -1
            };
            MassacrePacketInboxMessageKind messageKind = mappedKind switch
            {
                MassacrePacketInboxMessageKind.IncGauge or MassacrePacketInboxMessageKind.Result => MassacrePacketInboxMessageKind.Packet,
                _ => mappedKind
            };

            message = new MassacrePacketInboxMessage(
                messageKind,
                source,
                $"packetraw {Convert.ToHexString(rawPacket)}",
                packetType: packetType,
                payload: payload);
            return true;
        }

        private static bool HasExpectedMappedPayloadLength(MassacrePacketInboxMessageKind kind, byte[] payload)
        {
            int length = payload?.Length ?? 0;
            return kind switch
            {
                MassacrePacketInboxMessageKind.ClockPayload => length >= sizeof(byte) + sizeof(int),
                MassacrePacketInboxMessageKind.InfoPayload => length >= sizeof(int) * 4,
                MassacrePacketInboxMessageKind.IncGauge => length >= sizeof(int),
                MassacrePacketInboxMessageKind.Result => length >= sizeof(byte) + sizeof(int),
                _ => false
            };
        }

        private static bool IsSupportedMappedKind(MassacrePacketInboxMessageKind kind)
        {
            return kind is MassacrePacketInboxMessageKind.ClockPayload
                or MassacrePacketInboxMessageKind.InfoPayload
                or MassacrePacketInboxMessageKind.IncGauge
                or MassacrePacketInboxMessageKind.Result;
        }

        private static string DescribeMappedKind(MassacrePacketInboxMessageKind kind)
        {
            return kind switch
            {
                MassacrePacketInboxMessageKind.ClockPayload => "clock",
                MassacrePacketInboxMessageKind.InfoPayload => "context",
                MassacrePacketInboxMessageKind.IncGauge => "inc-gauge",
                MassacrePacketInboxMessageKind.Result => "result",
                _ => kind.ToString()
            };
        }

        private static string DescribeMessage(MassacrePacketInboxMessage message)
        {
            if (message == null)
            {
                return "Massacre official-session payload";
            }

            return message.Kind switch
            {
                MassacrePacketInboxMessageKind.ClockPayload => "Massacre clock payload",
                MassacrePacketInboxMessageKind.InfoPayload => "Massacre context payload",
                MassacrePacketInboxMessageKind.Packet => $"Massacre opcode {message.PacketType}",
                _ => $"Massacre {message.Kind}"
            };
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
                error = "Massacre official-session discovery requires a process name or pid when a selector is provided.";
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

        private static string DescribeSelector(int? owningProcessId, string owningProcessName)
        {
            return owningProcessId.HasValue
                ? $"pid {owningProcessId.Value}"
                : string.IsNullOrWhiteSpace(owningProcessName)
                    ? "the selected process"
                    : $"process '{owningProcessName}'";
        }

        private static string DescribeEstablishedSession(SessionDiscoveryCandidate candidate)
        {
            return $"{candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}";
        }

        private static string DescribePassiveEstablishedSession(SessionDiscoveryCandidate candidate)
        {
            return $"observing established socket pair {candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}; proxy reconnect required for decrypt/inject";
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
                status = $"Massacre official-session discovery found no established TCP session for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
                candidate = default;
                return false;
            }

            if (filteredCandidates.Count > 1)
            {
                string matches = string.Join(", ", filteredCandidates.Select(candidate =>
                    $"{candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} via {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}"));
                status = $"Massacre official-session discovery found multiple candidates for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}: {matches}. Use {DiscoverCommandUsage} to inspect them, then narrow with a localPort filter before using {AttachCommandUsage} for passive observation or {StartAutoCommandUsage} for reconnect proxy ownership.";
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
                filteredCandidates
                    .Select(candidate =>
                        $"{candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}")
                    .Concat(new[]
                    {
                        $"Use {AttachCommandUsage} for passive observation of an already-established Maple socket pair.",
                        $"Use {StartAutoCommandUsage} when you need a reconnect through the localhost proxy for decryptable traffic and outbound injection."
                    }));
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
