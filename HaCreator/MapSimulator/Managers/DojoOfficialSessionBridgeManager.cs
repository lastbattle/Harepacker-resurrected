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
using HaCreator.MapSimulator.Effects;
using MapleLib.MapleCryptoLib;
using MapleLib.PacketLib;

using System.Buffers.Binary;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Built-in Mu Lung Dojo transport bridge that proxies a live Maple session
    /// and maps configured inbound opcodes into the existing Dojo raw-packet seam.
    /// </summary>
    public sealed class DojoOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18490;
        private const string DefaultProcessName = "MapleStory";
        private const int AddressFamilyInet = 2;
        private const int ErrorInsufficientBuffer = 122;
        private const int RecentPacketCapacity = 8;

        private readonly ConcurrentQueue<DojoPacketInboxMessage> _pendingMessages = new();
        private readonly ConcurrentDictionary<int, int> _opcodeMappings = new();
        private readonly ConcurrentDictionary<int, LearnedOpcodeEntry> _learnedOpcodeTable = new();
        private readonly List<DeferredInboundPacket> _deferredPackets = new();
        private readonly Queue<string> _recentPackets = new();
        private readonly object _sync = new();
        private int _inferenceClearMapId = -1;
        private string _inferenceClearPortalName = string.Empty;
        private int _inferenceExitMapId = -1;
        private bool _inferenceTimerRunning;
        private bool _inferenceTimerExpired;
        private bool _inferenceClearActive;
        private bool _inferenceTimeOverActive;

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;
        private BridgePair _activePair;

        public readonly record struct SessionDiscoveryCandidate(
            int ProcessId,
            string ProcessName,
            IPEndPoint LocalEndpoint,
            IPEndPoint RemoteEndpoint);

        private sealed class LearnedOpcodeEntry
        {
            public LearnedOpcodeEntry(int packetType, string evidence)
            {
                PacketType = packetType;
                Evidence = evidence ?? string.Empty;
                Count = 1;
            }

            public int PacketType { get; private set; }
            public string Evidence { get; private set; }
            public int Count { get; private set; }

            public void Update(int packetType, string evidence)
            {
                PacketType = packetType;
                Evidence = evidence ?? string.Empty;
                Count++;
            }
        }

        private sealed class DeferredInboundPacket
        {
            public DeferredInboundPacket(int opcode, byte[] rawPacket, byte[] payload, string source, int tentativePacketType, string evidence)
            {
                Opcode = opcode;
                RawPacket = rawPacket != null ? (byte[])rawPacket.Clone() : Array.Empty<byte>();
                Payload = payload != null ? (byte[])payload.Clone() : Array.Empty<byte>();
                Source = source ?? string.Empty;
                TentativePacketType = tentativePacketType;
                Evidence = evidence ?? string.Empty;
            }

            public int Opcode { get; }
            public byte[] RawPacket { get; }
            public byte[] Payload { get; }
            public string Source { get; }
            public int TentativePacketType { get; }
            public string Evidence { get; }
        }

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
        public string LastStatus { get; private set; } = "Dojo official-session bridge inactive.";

        public string DescribeStatus()
        {
            string lifecycle = IsRunning
                ? $"listening on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}"
                : "inactive";
            string session = HasConnectedSession
                ? $"connected session {_activePair?.ClientEndpoint ?? "unknown-client"} -> {_activePair?.RemoteEndpoint ?? "unknown-remote"}"
                : "no active Maple session";
            return $"Dojo official-session bridge {lifecycle}; {session}; received={ReceivedCount}; mappings={DescribePacketMappings()}; learned={DescribeLearnedPacketTable()}; deferred={DescribeDeferredPackets()}; inference={DescribeInferenceContext()}; recent={DescribeRecentPackets()}. {LastStatus}";
        }

        public void UpdateInferenceContext(DojoField field)
        {
            lock (_sync)
            {
                if (field == null || !field.IsActive)
                {
                    _inferenceClearMapId = -1;
                    _inferenceClearPortalName = string.Empty;
                    _inferenceExitMapId = -1;
                    _inferenceTimerRunning = false;
                    _inferenceTimerExpired = false;
                    _inferenceClearActive = false;
                    _inferenceTimeOverActive = false;
                    _deferredPackets.Clear();
                    return;
                }

                _inferenceClearMapId = field.NextFloorMapId;
                _inferenceClearPortalName = field.NextFloorPortalName ?? string.Empty;
                _inferenceExitMapId = field.ExitMapId;
                _inferenceTimerRunning = field.HasLiveTimer;
                _inferenceTimerExpired = field.IsTimerExpired;
                _inferenceClearActive = field.IsClearResultActive;
                _inferenceTimeOverActive = field.IsTimeOverResultActive;
                PromoteDeferredPacketsNoLock();
            }
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
                        status = $"Dojo official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                        LastStatus = status;
                        return true;
                    }

                    status = $"Dojo official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                    LastStatus = status;
                    return false;
                }

                if (IsRunning
                    && MatchesTargetConfiguration(ListenPort, RemoteHost, RemotePort, resolvedListenPort, resolvedRemoteHost, remotePort))
                {
                    status = $"Dojo official-session bridge already listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
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
                    LastStatus = $"Dojo official-session bridge listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                    status = LastStatus;
                    return true;
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    LastStatus = $"Dojo official-session bridge failed to start: {ex.Message}";
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
                    status = $"Dojo official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                    LastStatus = status;
                    return true;
                }

                status = $"Dojo official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                LastStatus = status;
                return false;
            }

            if (MatchesDiscoveredTargetConfiguration(ListenPort, RemoteHost, RemotePort, resolvedListenPort, candidate.RemoteEndpoint) && IsRunning)
            {
                status = $"Dojo official-session bridge remains armed for {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}.";
                LastStatus = status;
                return true;
            }

            if (!TryStart(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, out string startStatus))
            {
                status = $"Dojo official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}, but startup failed. {startStatus}";
                LastStatus = status;
                return false;
            }

            status = $"Dojo official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {startStatus}";
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

        public bool TryConfigurePacketMapping(int opcode, int packetType, out string status)
        {
            if (opcode <= 0)
            {
                status = "Dojo opcode mappings require a positive opcode.";
                return false;
            }

            if (packetType < DojoField.PacketTypeClock || packetType > DojoField.PacketTypeTimeOver)
            {
                status = $"Dojo packet mappings only accept internal packet types {DojoField.PacketTypeClock}-{DojoField.PacketTypeTimeOver}.";
                return false;
            }

            _opcodeMappings[opcode] = packetType;
            RememberLearnedOpcode(opcode, packetType, "manual");
            status = $"Mapped Dojo opcode {opcode} to {DescribePacketType(packetType)}.";
            LastStatus = status;
            return true;
        }

        public bool RemovePacketMapping(int opcode, out string status)
        {
            if (_opcodeMappings.TryRemove(opcode, out int packetType))
            {
                status = $"Removed Dojo opcode {opcode} mapping for {DescribePacketType(packetType)}.";
                LastStatus = status;
                return true;
            }

            status = $"Dojo opcode {opcode} is not currently mapped.";
            return false;
        }

        public void ClearPacketMappings()
        {
            _opcodeMappings.Clear();
            LastStatus = "Cleared Dojo official-session opcode mappings.";
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

        public string DescribeLearnedPacketTable()
        {
            if (_learnedOpcodeTable.IsEmpty)
            {
                return "none";
            }

            return string.Join(
                ", ",
                _learnedOpcodeTable
                    .OrderBy(entry => entry.Key)
                    .Select(entry => $"{entry.Key}->{DescribePacketType(entry.Value.PacketType)}[{entry.Value.Count}x:{entry.Value.Evidence}]"));
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

        public string DescribeDeferredPackets()
        {
            lock (_sync)
            {
                if (_deferredPackets.Count == 0)
                {
                    return "none";
                }

                return string.Join(
                    ", ",
                    _deferredPackets
                        .Select(packet => $"{packet.Opcode}->{DescribePacketType(packet.TentativePacketType)}[{packet.Evidence}]"));
            }
        }

        public bool TryMapInboundPacket(byte[] rawPacket, string source, out DojoPacketInboxMessage message)
        {
            message = null;
            if (rawPacket == null || rawPacket.Length < sizeof(short))
            {
                return false;
            }

            int opcode = BitConverter.ToUInt16(rawPacket, 0);
            byte[] payload = rawPacket.Skip(sizeof(short)).ToArray();
            string mappingReason = "configured";
            if (!_opcodeMappings.TryGetValue(opcode, out int packetType))
            {
                if (!TryInferInboundPacketType(opcode, payload, out packetType, out mappingReason))
                {
                    return false;
                }
            }

            message = new DojoPacketInboxMessage(
                DojoPacketMessageKind.RawPacket,
                value: 0,
                option: string.Empty,
                source: source,
                rawText: $"packetraw {Convert.ToHexString(rawPacket)}",
                packetType: packetType,
                payload: payload);
            RecordRecentPacket(opcode, rawPacket, packetType, mappingReason);
            return true;
        }

        public void Stop()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
                LastStatus = "Dojo official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out DojoPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(string source, DojoPacketInboxMessage message, bool success, string result)
        {
            string summary = string.IsNullOrWhiteSpace(result)
                ? DescribePacketType(message?.PacketType ?? -1)
                : $"{DescribePacketType(message?.PacketType ?? -1)}: {result}";
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
                LastStatus = $"Dojo official-session bridge error: {ex.Message}";
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
                        LastStatus = "Rejected Dojo official-session client because a live Maple session is already attached.";
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
                clientSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Dojo official-session client disconnected: {pair.ClientEndpoint}.");
                serverSession.OnPacketReceived += (packet, isInit) => HandleServerPacket(pair, packet, isInit);
                serverSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Dojo official-session server disconnected: {pair.RemoteEndpoint}.");

                lock (_sync)
                {
                    _activePair = pair;
                }

                LastStatus = $"Dojo official-session bridge connected {pair.ClientEndpoint} -> {pair.RemoteEndpoint}. Waiting for Maple init packet.";
                serverSession.WaitForDataNoEncryption();
            }
            catch (Exception ex)
            {
                client.Close();
                pair?.Close();
                LastStatus = $"Dojo official-session bridge connect failed: {ex.Message}";
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
                    LastStatus = $"Dojo official-session bridge initialized Maple crypto for {pair.ClientEndpoint} <-> {pair.RemoteEndpoint}.";
                    pair.ClientSession.WaitForData();
                    return;
                }

                pair.ClientSession.SendPacket((byte[])raw.Clone());

                if (!TryMapInboundPacket(raw, $"official-session:{pair.RemoteEndpoint}", out DojoPacketInboxMessage message))
                {
                    return;
                }

                _pendingMessages.Enqueue(message);
                ReceivedCount++;
                LastStatus = $"Queued Dojo opcode {BitConverter.ToUInt16(raw, 0)} as {DescribePacketType(message.PacketType)} from live session {pair.RemoteEndpoint}.";
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Dojo official-session server handling failed: {ex.Message}");
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
                ClearActivePair(pair, $"Dojo official-session client handling failed: {ex.Message}");
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
                lock (_sync)
                {
                    _recentPackets.Clear();
                    _deferredPackets.Clear();
                }
            }
        }

        private bool TryInferInboundPacketType(int opcode, byte[] payload, out int packetType, out string mappingReason)
        {
            packetType = -1;
            mappingReason = string.Empty;
            int clearMapIdHint;
            string clearPortalNameHint;
            int exitMapIdHint;
            bool timerRunning;
            bool timerExpired;
            bool clearActive;
            bool timeOverActive;
            lock (_sync)
            {
                clearMapIdHint = _inferenceClearMapId;
                clearPortalNameHint = _inferenceClearPortalName;
                exitMapIdHint = _inferenceExitMapId;
                timerRunning = _inferenceTimerRunning;
                timerExpired = _inferenceTimerExpired;
                clearActive = _inferenceClearActive;
                timeOverActive = _inferenceTimeOverActive;
            }

            bool inferredFromPayload = DojoField.TryInferPacketType(
                payload,
                clearMapIdHint,
                clearPortalNameHint,
                exitMapIdHint,
                out int inferredPacketType,
                out string inferredReason,
                out bool isStableInference);

            if (inferredFromPayload && isStableInference)
            {
                _opcodeMappings[opcode] = inferredPacketType;
                RememberLearnedOpcode(opcode, inferredPacketType, $"auto:{inferredReason}");
                packetType = inferredPacketType;
                mappingReason = $"auto:{inferredReason}";
                LastStatus = $"Auto-mapped Dojo opcode {opcode} to {DescribePacketType(packetType)} from payload inference ({inferredReason}).";
                return true;
            }

            if (inferredFromPayload
                && TryInferTransferPacketTypeFromFieldState(
                    payload,
                    clearMapIdHint,
                    clearPortalNameHint,
                    exitMapIdHint,
                    timerRunning,
                    timerExpired,
                    clearActive,
                    timeOverActive,
                    out packetType,
                    out string stateReason))
            {
                _opcodeMappings[opcode] = packetType;
                RememberLearnedOpcode(opcode, packetType, $"state:{stateReason}");
                mappingReason = $"state:{stateReason}";
                LastStatus = $"Auto-mapped Dojo opcode {opcode} to {DescribePacketType(packetType)} from field-state inference ({stateReason}).";
                return true;
            }

            if (inferredFromPayload)
            {
                if (ShouldDeferTentativeTransferPayload(payload, inferredReason))
                {
                    DeferPacket(opcode, payload, inferredPacketType, inferredReason);
                    packetType = -1;
                    mappingReason = $"deferred:{inferredReason}";
                    return false;
                }

                packetType = inferredPacketType;
                mappingReason = $"tentative:{inferredReason}";
                RememberLearnedOpcode(opcode, packetType, mappingReason);
                LastStatus = $"Tentatively identified Dojo opcode {opcode} as {DescribePacketType(packetType)} from payload inference ({inferredReason}); waiting for stronger evidence before persisting the mapping.";
                return true;
            }

            string candidateSummary = DojoField.DescribePacketPayloadCandidates(payload, clearMapIdHint, clearPortalNameHint, exitMapIdHint);
            mappingReason = candidateSummary;
            RecordRecentPacket(opcode, BuildRawPacket(opcode, payload), mappedPacketType: null, $"unmapped:{candidateSummary}");
            LastStatus = candidateSummary == "unknown"
                ? $"Ignored unmapped Dojo opcode {opcode}; payload did not match any known Dojo packet shape."
                : $"Ignored unmapped Dojo opcode {opcode}; payload matched multiple Dojo packet candidates ({candidateSummary}).";
            return false;
        }

        private void PromoteDeferredPacketsNoLock()
        {
            if (_deferredPackets.Count == 0)
            {
                return;
            }

            for (int i = _deferredPackets.Count - 1; i >= 0; i--)
            {
                DeferredInboundPacket packet = _deferredPackets[i];
                if (!TryResolveDeferredPacketNoLock(packet, out int packetType, out string evidence))
                {
                    continue;
                }

                _opcodeMappings[packet.Opcode] = packetType;
                RememberLearnedOpcode(packet.Opcode, packetType, $"deferred:{evidence}");
                _pendingMessages.Enqueue(CreateRawPacketMessage(packet.RawPacket, packet.Source, packetType, packet.Payload));
                RecordRecentPacket(packet.Opcode, packet.RawPacket, packetType, $"deferred:{evidence}");
                ReceivedCount++;
                LastStatus = $"Promoted deferred Dojo opcode {packet.Opcode} to {DescribePacketType(packetType)} after field-state evidence ({evidence}).";
                _deferredPackets.RemoveAt(i);
            }
        }

        private bool TryResolveDeferredPacketNoLock(DeferredInboundPacket packet, out int packetType, out string evidence)
        {
            packetType = -1;
            evidence = string.Empty;
            if (packet == null)
            {
                return false;
            }

            if (_opcodeMappings.TryGetValue(packet.Opcode, out packetType))
            {
                evidence = "configured mapping";
                return true;
            }

            bool inferredFromPayload = DojoField.TryInferPacketType(
                packet.Payload,
                _inferenceClearMapId,
                _inferenceClearPortalName,
                _inferenceExitMapId,
                out int inferredPacketType,
                out string inferredReason,
                out bool isStableInference);
            if (inferredFromPayload && isStableInference)
            {
                packetType = inferredPacketType;
                evidence = $"stable payload {inferredReason}";
                return true;
            }

            if (inferredFromPayload
                && TryInferTransferPacketTypeFromFieldState(
                    packet.Payload,
                    _inferenceClearMapId,
                    _inferenceClearPortalName,
                    _inferenceExitMapId,
                    _inferenceTimerRunning,
                    _inferenceTimerExpired,
                    _inferenceClearActive,
                    _inferenceTimeOverActive,
                    out packetType,
                    out string stateReason))
            {
                evidence = stateReason;
                return true;
            }

            return false;
        }

        private static bool ShouldDeferTentativeTransferPayload(byte[] payload, string inferredReason)
        {
            if (payload == null)
            {
                return false;
            }

            if (!inferredReason.Contains("default transfer tie-break", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string candidateSummary = DojoField.DescribeFieldSpecificPayloadCandidates(payload);
            return candidateSummary.Contains("clear(", StringComparison.OrdinalIgnoreCase)
                && candidateSummary.Contains("timeover(", StringComparison.OrdinalIgnoreCase);
        }

        private void DeferPacket(int opcode, byte[] payload, int tentativePacketType, string inferredReason)
        {
            byte[] rawPacket = BuildRawPacket(opcode, payload);
            lock (_sync)
            {
                _deferredPackets.Add(new DeferredInboundPacket(
                    opcode,
                    rawPacket,
                    payload,
                    $"official-session:opcode-{opcode}",
                    tentativePacketType,
                    inferredReason));
                RecordRecentPacket(opcode, rawPacket, null, $"deferred:{inferredReason}");
                LastStatus = $"Deferred tentative Dojo opcode {opcode} as {DescribePacketType(tentativePacketType)} from payload inference ({inferredReason}); waiting for clear or timeout field-state evidence before dispatch.";
            }
        }

        private static DojoPacketInboxMessage CreateRawPacketMessage(byte[] rawPacket, string source, int packetType, byte[] payload)
        {
            return new DojoPacketInboxMessage(
                DojoPacketMessageKind.RawPacket,
                value: 0,
                option: string.Empty,
                source: source,
                rawText: $"packetraw {Convert.ToHexString(rawPacket ?? Array.Empty<byte>())}",
                packetType: packetType,
                payload: payload);
        }

        private string DescribeInferenceContext()
        {
            lock (_sync)
            {
                string clearTarget = _inferenceClearMapId > 0
                    ? _inferenceClearMapId.ToString()
                    : "unknown";
                if (!string.IsNullOrWhiteSpace(_inferenceClearPortalName))
                {
                    clearTarget = $"{clearTarget}:{_inferenceClearPortalName}";
                }

                string exitTarget = _inferenceExitMapId > 0
                    ? _inferenceExitMapId.ToString()
                    : "unknown";
                string state = _inferenceClearActive
                    ? "clear-active"
                    : _inferenceTimeOverActive
                        ? "timeover-active"
                        : _inferenceTimerExpired
                            ? "timer-expired"
                            : _inferenceTimerRunning
                                ? "timer-running"
                                : "idle";
                return $"clear={clearTarget}, exit={exitTarget}, state={state}";
            }
        }

        private void RememberLearnedOpcode(int opcode, int packetType, string evidence)
        {
            _learnedOpcodeTable.AddOrUpdate(
                opcode,
                _ => new LearnedOpcodeEntry(packetType, evidence),
                (_, existing) =>
                {
                    existing.Update(packetType, evidence);
                    return existing;
                });
        }

        private static bool TryInferTransferPacketTypeFromFieldState(
            byte[] payload,
            int clearMapIdHint,
            string clearPortalNameHint,
            int exitMapIdHint,
            bool timerRunning,
            bool timerExpired,
            bool clearActive,
            bool timeOverActive,
            out int packetType,
            out string reason)
        {
            packetType = -1;
            reason = string.Empty;

            string candidateSummary = DojoField.DescribeFieldSpecificPayloadCandidates(payload);
            bool hasClearCandidate = candidateSummary.Contains("clear(", StringComparison.OrdinalIgnoreCase);
            bool hasTimeOverCandidate = candidateSummary.Contains("timeover(", StringComparison.OrdinalIgnoreCase);
            if (!hasClearCandidate || !hasTimeOverCandidate)
            {
                return false;
            }

            int transferMapId = 0;
            string portalName = null;
            if (!DojoField.TryInferFieldSpecificPacketType(payload, out _, out _)
                && !TryParseTransferPayloadCompat(payload, out transferMapId, out portalName))
            {
                return false;
            }

            bool matchesClear = clearMapIdHint > 0 && transferMapId == clearMapIdHint;
            bool matchesExit = exitMapIdHint > 0 && transferMapId == exitMapIdHint;
            bool matchesPortal = !string.IsNullOrWhiteSpace(portalName)
                && !string.IsNullOrWhiteSpace(clearPortalNameHint)
                && string.Equals(portalName, clearPortalNameHint, StringComparison.OrdinalIgnoreCase);
            if (matchesPortal || (matchesClear && !matchesExit))
            {
                packetType = DojoField.PacketTypeClear;
                reason = $"transfer target matched clear path ({transferMapId}{FormatPortalSuffix(portalName)})";
                return true;
            }

            if (matchesExit && !matchesClear)
            {
                packetType = DojoField.PacketTypeTimeOver;
                reason = $"transfer target matched exit path ({transferMapId})";
                return true;
            }

            if (clearActive)
            {
                packetType = DojoField.PacketTypeClear;
                reason = "clear presentation already active";
                return true;
            }

            if (timeOverActive || timerExpired)
            {
                packetType = DojoField.PacketTypeTimeOver;
                reason = timeOverActive ? "time-over presentation already active" : "live Dojo timer already expired";
                return true;
            }

            if (timerRunning)
            {
                packetType = DojoField.PacketTypeClear;
                reason = transferMapId > 0
                    ? $"live timer still running for transfer target {transferMapId}{FormatPortalSuffix(portalName)}"
                    : "live timer still running before timeout";
                return true;
            }

            return false;
        }

        private static bool TryParseTransferPayloadCompat(byte[] payload, out int mapId, out string portalName)
        {
            mapId = -1;
            portalName = string.Empty;
            if (payload == null || payload.Length == 0)
            {
                mapId = 0;
                return true;
            }

            if (payload.Length < sizeof(int))
            {
                return false;
            }

            mapId = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, sizeof(int)));
            if (payload.Length <= sizeof(int))
            {
                return true;
            }

            try
            {
                portalName = System.Text.Encoding.UTF8.GetString(payload, sizeof(int), payload.Length - sizeof(int)).TrimEnd('\0');
            }
            catch
            {
                portalName = string.Empty;
            }

            return true;
        }

        private static string FormatPortalSuffix(string portalName)
        {
            return string.IsNullOrWhiteSpace(portalName) ? string.Empty : $":{portalName}";
        }

        private static byte[] BuildRawPacket(int opcode, byte[] payload)
        {
            byte[] rawPacket = new byte[sizeof(short) + (payload?.Length ?? 0)];
            BinaryPrimitives.WriteUInt16LittleEndian(rawPacket.AsSpan(0, sizeof(short)), (ushort)opcode);
            if (payload != null && payload.Length > 0)
            {
                payload.CopyTo(rawPacket, sizeof(short));
            }

            return rawPacket;
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

        private static MapleCrypto CreateCrypto(byte[] iv, short version)
        {
            return new MapleCrypto((byte[])iv.Clone(), version);
        }

        private static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
                DojoField.PacketTypeClock => "clock",
                DojoField.PacketTypeStage => "stage",
                DojoField.PacketTypeClear => "clear",
                DojoField.PacketTypeTimeOver => "timeover",
                _ => $"packet {packetType}"
            };
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
            return string.IsNullOrWhiteSpace(remoteHost)
                ? IPAddress.Loopback.ToString()
                : remoteHost.Trim();
        }

        private static bool TryResolveProcessSelector(
            string processSelector,
            out int? owningProcessId,
            out string owningProcessName,
            out string error)
        {
            owningProcessId = null;
            owningProcessName = null;
            error = null;

            if (string.IsNullOrWhiteSpace(processSelector))
            {
                owningProcessName = DefaultProcessName;
                return true;
            }

            string trimmedSelector = processSelector.Trim();
            if (int.TryParse(trimmedSelector, out int parsedPid))
            {
                if (parsedPid <= 0)
                {
                    error = "Dojo official-session discovery requires a positive pid.";
                    return false;
                }

                owningProcessId = parsedPid;
                return true;
            }

            owningProcessName = NormalizeProcessSelector(trimmedSelector);
            if (string.IsNullOrWhiteSpace(owningProcessName))
            {
                error = "Dojo official-session discovery requires a process name or pid when a selector is provided.";
                return false;
            }

            return true;
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
                return $"Dojo official-session discovery found no established TCP session for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
            }

            string matches = string.Join(
                Environment.NewLine,
                filteredCandidates.Select(candidate => $"- {candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}"));
            return $"Dojo official-session discovery matches for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}:{Environment.NewLine}{matches}";
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
            if (filteredCandidates.Count == 0)
            {
                candidate = default;
                status = $"Dojo official-session discovery found no established TCP session for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
                return false;
            }

            if (filteredCandidates.Count > 1)
            {
                candidate = default;
                string matches = string.Join(
                    ", ",
                    filteredCandidates.Select(entry => $"{entry.ProcessName}({entry.ProcessId}) local {entry.LocalEndpoint.Port} remote {entry.RemoteEndpoint.Address}:{entry.RemoteEndpoint.Port}"));
                status = $"Dojo official-session discovery found multiple candidates for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}: {matches}. Use /dojo session discover to inspect them, or add a localPort filter.";
                return false;
            }

            candidate = filteredCandidates[0];
            status = null;
            return true;
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

        private static string DescribeDiscoveryScope(int? owningProcessId, string owningProcessName, int remotePort, int? localPort)
        {
            string processScope = owningProcessId.HasValue
                ? $"pid {owningProcessId.Value}"
                : string.IsNullOrWhiteSpace(owningProcessName)
                    ? DefaultProcessName
                    : owningProcessName;
            return localPort.HasValue && localPort.Value > 0
                ? $"{processScope} remotePort {remotePort} localPort {localPort.Value}"
                : $"{processScope} remotePort {remotePort}";
        }

        private static string NormalizeProcessSelector(string selector)
        {
            if (string.IsNullOrWhiteSpace(selector))
            {
                return null;
            }

            string normalized = selector.Trim();
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

        private static IEnumerable<TcpRowOwnerPid> EnumerateTcpRows()
        {
            int bufferSize = 0;
            int result = GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, sort: true, AddressFamilyInet, TcpTableClass.OwnerPidAll, 0);
            if (result != 0 && result != ErrorInsufficientBuffer)
            {
                yield break;
            }

            IntPtr tableBuffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                result = GetExtendedTcpTable(tableBuffer, ref bufferSize, sort: true, AddressFamilyInet, TcpTableClass.OwnerPidAll, 0);
                if (result != 0)
                {
                    yield break;
                }

                int rowCount = Marshal.ReadInt32(tableBuffer);
                IntPtr rowPtr = IntPtr.Add(tableBuffer, sizeof(int));
                int rowSize = Marshal.SizeOf<TcpRowOwnerPid>();
                for (int i = 0; i < rowCount; i++)
                {
                    yield return Marshal.PtrToStructure<TcpRowOwnerPid>(rowPtr);
                    rowPtr = IntPtr.Add(rowPtr, rowSize);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(tableBuffer);
            }
        }

        private static int DecodePort(byte[] encodedPort)
        {
            if (encodedPort == null || encodedPort.Length < 2)
            {
                return 0;
            }

            return (encodedPort[0] << 8) | encodedPort[1];
        }

        private static IPAddress DecodeAddress(uint encodedAddress)
        {
            return new IPAddress(encodedAddress);
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern int GetExtendedTcpTable(
            IntPtr pTcpTable,
            ref int dwOutBufLen,
            bool sort,
            int ipVersion,
            TcpTableClass tableClass,
            uint reserved);

        private enum TcpTableClass
        {
            OwnerPidAll = 5
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
    }
}
