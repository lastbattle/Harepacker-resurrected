using MapleLib.MapleCryptoLib;
using MapleLib.PacketLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class RemoteUserOfficialSessionBridgeMessage
    {
        public RemoteUserOfficialSessionBridgeMessage(int packetType, byte[] payload, string source, ushort opcode)
        {
            PacketType = packetType;
            Payload = payload ?? Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "remote-user-session" : source;
            Opcode = opcode;
        }

        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public ushort Opcode { get; }
    }

    /// <summary>
    /// Proxies a live Maple session and peels CUserPool::OnPacket remote-user opcodes
    /// into the existing remote-user packet seam.
    /// </summary>
    public sealed class RemoteUserOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18492;
        private const string DefaultProcessName = "MapleStory";

        private const string OfficialRemoteOwnerEvidence = "v95 CUserPool::OnPacket (0x94ddf0) routes 179 enter, 180 leave, common opcodes 181-209, remote-user opcodes 210-230, and local-user opcodes 231-276; CUserRemote::OnAvatarModified (0x954110) is the live relationship-record route for couple/friend/marriage add and remove before CUserPool::Update consumes the tables; tutor remains local under CUserLocal::OnPacket 255/256.";

        private static readonly IReadOnlyDictionary<ushort, int> DefaultPacketMap = new Dictionary<ushort, int>
        {
            [179] = (int)Pools.RemoteUserPacketType.UserEnterField,
            [180] = (int)Pools.RemoteUserPacketType.UserLeaveField,
            [181] = (int)Pools.RemoteUserPacketType.UserChat,
            [182] = (int)Pools.RemoteUserPacketType.UserChatFromOutsideMap,
            [210] = (int)Pools.RemoteUserPacketType.UserMoveOfficial,
            [211] = (int)Pools.RemoteUserPacketType.UserAttackOfficial1,
            [212] = (int)Pools.RemoteUserPacketType.UserAttackOfficial2,
            [213] = (int)Pools.RemoteUserPacketType.UserAttackOfficial3,
            [214] = (int)Pools.RemoteUserPacketType.UserAttackOfficial4,
            [215] = (int)Pools.RemoteUserPacketType.UserPreparedSkillOfficial,
            [216] = (int)Pools.RemoteUserPacketType.UserMovingShootAttackPrepareOfficial,
            [217] = (int)Pools.RemoteUserPacketType.UserPreparedSkillClearOfficial,
            [218] = (int)Pools.RemoteUserPacketType.UserHitOfficial,
            [219] = (int)Pools.RemoteUserPacketType.UserEmotionOfficial,
            [220] = (int)Pools.RemoteUserPacketType.UserActiveEffectItemOfficial,
            [221] = (int)Pools.RemoteUserPacketType.UserUpgradeTombOfficial,
            [222] = (int)Pools.RemoteUserPacketType.UserPortableChairOfficial,
            [223] = (int)Pools.RemoteUserPacketType.UserAvatarModified,
            [224] = (int)Pools.RemoteUserPacketType.UserEffectOfficial,
            [225] = (int)Pools.RemoteUserPacketType.UserTemporaryStatSet,
            [226] = (int)Pools.RemoteUserPacketType.UserTemporaryStatReset,
            [227] = (int)Pools.RemoteUserPacketType.UserReceiveHpOfficial,
            [228] = (int)Pools.RemoteUserPacketType.UserGuildNameChangedOfficial,
            [229] = (int)Pools.RemoteUserPacketType.UserGuildMarkChangedOfficial,
            [230] = (int)Pools.RemoteUserPacketType.UserThrowGrenadeOfficial
        };

        private readonly ConcurrentQueue<RemoteUserOfficialSessionBridgeMessage> _pendingMessages = new();
        private readonly Dictionary<ushort, int> _packetMap = new(DefaultPacketMap);
        private readonly Dictionary<ushort, LearnedOpcodeEntry> _learnedPacketMap = new();
        private readonly object _sync = new();

        private sealed class LearnedOpcodeEntry
        {
            public LearnedOpcodeEntry(int packetType, string evidence, bool isManual, string source, byte[] payload)
            {
                PacketType = packetType;
                Evidence = evidence ?? string.Empty;
                IsManual = isManual;
                LastSource = string.IsNullOrWhiteSpace(source) ? "unknown-source" : source;
                LastPayloadLength = payload?.Length ?? 0;
                LastPayloadPreviewHex = FormatPayloadPreviewHex(payload);
                Count = 1;
            }

            public int PacketType { get; private set; }
            public string Evidence { get; private set; }
            public bool IsManual { get; }
            public string LastSource { get; private set; }
            public int LastPayloadLength { get; private set; }
            public string LastPayloadPreviewHex { get; private set; }
            public int Count { get; private set; }

            public void Update(int packetType, string evidence, string source, byte[] payload)
            {
                PacketType = packetType;
                Evidence = evidence ?? string.Empty;
                LastSource = string.IsNullOrWhiteSpace(source) ? LastSource : source;
                LastPayloadLength = payload?.Length ?? 0;
                LastPayloadPreviewHex = FormatPayloadPreviewHex(payload);
                Count++;
            }

            private static string FormatPayloadPreviewHex(byte[] payload)
            {
                if (payload == null || payload.Length == 0)
                {
                    return "empty";
                }

                int previewLength = Math.Min(payload.Length, 24);
                string preview = BitConverter.ToString(payload, 0, previewLength);
                return previewLength < payload.Length
                    ? $"{preview}..."
                    : preview;
            }
        }

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
        public int ForwardedOutboundCount { get; private set; }
        public string LastStatus { get; private set; } = "Remote-user official-session bridge inactive.";

        public string DescribeStatus()
        {
            string lifecycle = IsRunning
                ? $"listening on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}"
                : "inactive";
            string session = HasConnectedSession
                ? $"connected session {_activePair?.ClientEndpoint ?? "unknown-client"} -> {_activePair?.RemoteEndpoint ?? "unknown-remote"}"
                : "no active Maple session";
            return $"Remote-user official-session bridge {lifecycle}; {session}; officialOwnerTable={OfficialRemoteOwnerEvidence}; received={ReceivedCount}; forwardedOutbound={ForwardedOutboundCount}; learned={DescribeLearnedPacketMappings()}. {LastStatus}";
        }

        public string DescribePacketMappings()
        {
            lock (_sync)
            {
                return string.Join(
                    Environment.NewLine,
                    _packetMap
                        .OrderBy(entry => entry.Key)
                        .Select(entry =>
                            $"{entry.Key} -> {RemoteUserPacketInboxManager.DescribePacketType(entry.Value)}"));
            }
        }

        public string DescribeLearnedPacketMappings()
        {
            lock (_sync)
            {
                if (_learnedPacketMap.Count == 0)
                {
                    return "none";
                }

                return string.Join(
                    ", ",
                    _learnedPacketMap
                        .OrderBy(entry => entry.Key)
                        .Select(entry =>
                            $"{entry.Key}->{RemoteUserPacketInboxManager.DescribePacketType(entry.Value.PacketType)} ({entry.Value.Evidence}; count={entry.Value.Count}; source={entry.Value.LastSource}; payloadBytes={entry.Value.LastPayloadLength}; sample={entry.Value.LastPayloadPreviewHex})"));
            }
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
                    LastStatus = $"Remote-user official-session bridge listening on 127.0.0.1:{ListenPort}, proxying to {RemoteHost}:{RemotePort}, and filtering CUserPool opcodes 179, 180, and 181-230.";
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: false);
                    LastStatus = $"Remote-user official-session bridge failed to start: {ex.Message}";
                }
            }
        }

        public bool TryRefreshFromDiscovery(int listenPort, int remotePort, string processSelector, int? localPort, out string status)
        {
            if (HasAttachedClient)
            {
                status = $"Remote-user official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
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

            IReadOnlyList<GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates =
                GuildBossOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            if (!TryResolveDiscoveryCandidate(candidates, remotePort, owningProcessId, owningProcessName, localPort, out GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate candidate, out status))
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
                status = $"Remote-user official-session bridge remains armed for {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}.";
                LastStatus = status;
                return true;
            }

            Start(resolvedListenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port);
            status = $"Remote-user official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {LastStatus}";
            LastStatus = status;
            return true;
        }

        public string DescribeDiscoveredSessions(int remotePort, string processSelector = null, int? localPort = null)
        {
            if (!TryResolveProcessSelector(processSelector, out int? owningProcessId, out string owningProcessName, out string selectorError))
            {
                return selectorError;
            }

            IReadOnlyList<GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates =
                GuildBossOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            IReadOnlyList<GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate> filteredCandidates = FilterCandidatesByLocalPort(candidates, localPort);
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
                LastStatus = "Remote-user official-session bridge stopped.";
            }
        }

        public bool TryConfigurePacketMapping(ushort opcode, int packetType, out string status)
        {
            if (!Enum.IsDefined(typeof(Pools.RemoteUserPacketType), packetType))
            {
                status = $"Remote-user official-session bridge packet type {packetType} is not supported.";
                LastStatus = status;
                return false;
            }

            lock (_sync)
            {
                _packetMap[opcode] = packetType;
                RememberLearnedOpcodeNoLock(opcode, packetType, "manual", isManual: true, source: "manual", payload: null);
            }

            status = $"Mapped remote-user opcode {opcode} to {RemoteUserPacketInboxManager.DescribePacketType(packetType)}.";
            LastStatus = status;
            return true;
        }

        public bool RemovePacketMapping(ushort opcode, out string status)
        {
            bool removed;
            lock (_sync)
            {
                removed = _packetMap.Remove(opcode);
                _learnedPacketMap.Remove(opcode);
            }

            status = removed
                ? $"Removed remote-user opcode mapping for {opcode}."
                : $"Remote-user opcode {opcode} was not mapped.";
            LastStatus = status;
            return removed;
        }

        public void ClearPacketMappings()
        {
            lock (_sync)
            {
                _packetMap.Clear();
                _learnedPacketMap.Clear();
                foreach (KeyValuePair<ushort, int> entry in DefaultPacketMap)
                {
                    _packetMap[entry.Key] = entry.Value;
                }
            }

            LastStatus = "Remote-user official-session bridge packet mappings restored to defaults.";
        }

        public bool TryDequeue(out RemoteUserOfficialSessionBridgeMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(RemoteUserOfficialSessionBridgeMessage message, bool success, string detail)
        {
            string summary = message == null
                ? "remote-user packet"
                : $"{RemoteUserPacketInboxManager.DescribePacketType(message.PacketType)} opcode {message.Opcode}";
            LastStatus = success
                ? $"Applied {summary} from {message?.Source ?? "remote-user-session"}."
                : $"Ignored {summary} from {message?.Source ?? "remote-user-session"}: {detail}";
        }

        public void Dispose()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
            }
        }

        internal bool TryDecodeInboundRemoteUserPacketForTesting(
            byte[] rawPacket,
            string source,
            out RemoteUserOfficialSessionBridgeMessage message)
        {
            return TryDecodeInboundRemoteUserPacket(rawPacket, source, out message);
        }

        private bool TryDecodeInboundRemoteUserPacket(byte[] rawPacket, string source, out RemoteUserOfficialSessionBridgeMessage message)
        {
            message = null;
            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                return false;
            }

            ushort opcode = BitConverter.ToUInt16(rawPacket, 0);
            int packetType;
            lock (_sync)
            {
                if (!_packetMap.TryGetValue(opcode, out packetType))
                {
                    if (IsOfficialRemoteOpcodeCoveredByV95OwnerTable(opcode))
                    {
                        LastStatus = $"Ignored remote-user opcode {opcode}: {OfficialRemoteOwnerEvidence}";
                        return false;
                    }

                    if (!IsOfficialLocalUserOpcodeCoveredByV95OwnerTable(opcode))
                    {
                        LastStatus = $"Ignored unmapped remote-user opcode {opcode}: it is outside the recovered CUserPool::OnPacket local-user tutor owner range. {OfficialRemoteOwnerEvidence}";
                        return false;
                    }

                    byte[] inferencePayload = rawPacket.Skip(sizeof(ushort)).ToArray();
                    if (!TryInferInboundRemoteTutorPacketTypeNoLock(opcode, inferencePayload, out packetType, out string inferenceReason))
                    {
                        LastStatus = $"Ignored CUserPool local-user opcode {opcode}: payload did not match the exact remote tutor wrapper. {OfficialRemoteOwnerEvidence}";
                        return false;
                    }

                    _packetMap[opcode] = packetType;
                    string learnedEvidence = $"auto:{inferenceReason}; {OfficialRemoteOwnerEvidence}";
                    RememberLearnedOpcodeNoLock(opcode, packetType, learnedEvidence, isManual: false, source, inferencePayload);
                    LastStatus = $"Auto-mapped remote-user opcode {opcode} to {RemoteUserPacketInboxManager.DescribePacketType(packetType)} from tutor payload inference ({inferenceReason}); {OfficialRemoteOwnerEvidence}";
                }
            }

            if (!Enum.IsDefined(typeof(Pools.RemoteUserPacketType), packetType))
            {
                return false;
            }

            byte[] payload = rawPacket.Skip(sizeof(ushort)).ToArray();
            message = new RemoteUserOfficialSessionBridgeMessage(packetType, payload, source, opcode);
            return true;
        }

        internal static bool IsOfficialRemoteOpcodeCoveredByV95OwnerTable(ushort opcode)
        {
            return opcode == 179
                || opcode == 180
                || (opcode >= 181 && opcode <= 209)
                || (opcode >= 210 && opcode <= 230);
        }

        internal static bool IsOfficialLocalUserOpcodeCoveredByV95OwnerTable(ushort opcode)
        {
            return opcode >= 231 && opcode <= 276;
        }

        private static bool TryInferInboundRemoteTutorPacketTypeNoLock(
            ushort opcode,
            byte[] payload,
            out int packetType,
            out string reason)
        {
            packetType = 0;
            reason = string.Empty;
            if (payload == null || payload.Length < sizeof(int) + 1)
            {
                return false;
            }

            if (HaCreator.MapSimulator.MapSimulator.TryDecodeRemotePacketOwnedTutorHirePayload(
                    payload,
                    out int hireCharacterId,
                    out bool enabled,
                    out _))
            {
                packetType = (int)Pools.RemoteUserPacketType.UserTutorHire;
                reason = $"CUserPool local-user opcode {opcode} exact remote tutor-hire wrapper for character {hireCharacterId}, enabled={enabled}";
                return true;
            }

            if (HaCreator.MapSimulator.MapSimulator.TryDecodeRemotePacketOwnedTutorMessagePayload(
                    payload,
                    out int messageCharacterId,
                    out bool indexedPayload,
                    out int messageIndex,
                    out int durationMs,
                    out string text,
                    out int width,
                    out _))
            {
                packetType = (int)Pools.RemoteUserPacketType.UserTutorMessage;
                reason = indexedPayload
                    ? $"CUserPool local-user opcode {opcode} exact remote tutor-indexed-message wrapper for character {messageCharacterId}, index={messageIndex}, duration={durationMs}"
                    : $"CUserPool local-user opcode {opcode} exact remote tutor-text-message wrapper for character {messageCharacterId}, width={width}, duration={durationMs}, textLength={text?.Length ?? 0}";
                return true;
            }

            return false;
        }

        private void RememberLearnedOpcodeNoLock(ushort opcode, int packetType, string evidence, bool isManual, string source, byte[] payload)
        {
            if (_learnedPacketMap.TryGetValue(opcode, out LearnedOpcodeEntry existing))
            {
                existing.Update(packetType, evidence, source, payload);
                return;
            }

            _learnedPacketMap[opcode] = new LearnedOpcodeEntry(packetType, evidence, isManual, source, payload);
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
                LastStatus = $"Remote-user official-session bridge error: {ex.Message}";
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
                        LastStatus = "Rejected remote-user official-session client because a live Maple session is already attached.";
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
                clientSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Remote-user official-session client disconnected: {pair.ClientEndpoint}.");
                serverSession.OnPacketReceived += (packet, isInit) => HandleServerPacket(pair, packet, isInit);
                serverSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Remote-user official-session server disconnected: {pair.RemoteEndpoint}.");

                lock (_sync)
                {
                    _activePair = pair;
                }

                LastStatus = $"Remote-user official-session bridge connected {pair.ClientEndpoint} -> {pair.RemoteEndpoint}. Waiting for Maple init packet.";
                serverSession.WaitForDataNoEncryption();
            }
            catch (Exception ex)
            {
                client.Close();
                pair?.Close();
                LastStatus = $"Remote-user official-session bridge connect failed: {ex.Message}";
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
                    LastStatus = $"Remote-user official-session bridge initialized Maple crypto for {pair.ClientEndpoint} <-> {pair.RemoteEndpoint}.";
                    pair.ClientSession.WaitForData();
                    return;
                }

                pair.ClientSession.SendPacket((byte[])raw.Clone());

                if (!TryDecodeInboundRemoteUserPacket(raw, $"official-session:{pair.RemoteEndpoint}", out RemoteUserOfficialSessionBridgeMessage message))
                {
                    return;
                }

                _pendingMessages.Enqueue(message);
                ReceivedCount++;
                LastStatus = $"Queued {RemoteUserPacketInboxManager.DescribePacketType(message.PacketType)} opcode {message.Opcode} ({message.Payload.Length} byte(s)) from live session {pair.RemoteEndpoint}.";
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Remote-user official-session server handling failed: {ex.Message}");
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

                byte[] rawPacket = packet.ToArray();
                pair.ServerSession.SendPacket((byte[])rawPacket.Clone());
                ForwardedOutboundCount++;
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Remote-user official-session client handling failed: {ex.Message}");
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

            _listenerTask = null;
            _listener = null;
            _listenerCancellation?.Dispose();
            _listenerCancellation = null;

            BridgePair pair = null;
            lock (_sync)
            {
                pair = _activePair;
                _activePair = null;
            }

            pair?.Close();

            if (clearPending)
            {
                while (_pendingMessages.TryDequeue(out _))
                {
                }

                ResetInboundState();
            }
        }

        private void ResetInboundState()
        {
            lock (_sync)
            {
                ReceivedCount = 0;
                ForwardedOutboundCount = 0;
                ClearLearnedTutorMappingsNoLock();
            }
        }

        private void ClearLearnedTutorMappingsNoLock()
        {
            ushort[] learnedTutorOpcodes = _learnedPacketMap
                .Where(entry =>
                    !entry.Value.IsManual
                    && (entry.Value.PacketType == (int)Pools.RemoteUserPacketType.UserTutorHire
                        || entry.Value.PacketType == (int)Pools.RemoteUserPacketType.UserTutorMessage))
                .Select(entry => entry.Key)
                .ToArray();

            for (int i = 0; i < learnedTutorOpcodes.Length; i++)
            {
                ushort opcode = learnedTutorOpcodes[i];
                _learnedPacketMap.Remove(opcode);
                if (!DefaultPacketMap.ContainsKey(opcode))
                {
                    _packetMap.Remove(opcode);
                }
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

            string trimmed = selector?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                owningProcessName = DefaultProcessName;
                return true;
            }

            if (int.TryParse(trimmed, out int processId) && processId > 0)
            {
                owningProcessId = processId;
                return true;
            }

            try
            {
                Process.GetProcessesByName(trimmed).FirstOrDefault();
                owningProcessName = trimmed;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Remote-user official-session discovery could not inspect process selector '{trimmed}': {ex.Message}";
                return false;
            }
        }

        private static bool MatchesDiscoveredTargetConfiguration(
            bool isRunning,
            int listenPort,
            string remoteHost,
            int remotePort,
            int desiredListenPort,
            IPEndPoint desiredRemoteEndpoint)
        {
            if (!isRunning || desiredRemoteEndpoint == null)
            {
                return false;
            }

            return listenPort == desiredListenPort
                && remotePort == desiredRemoteEndpoint.Port
                && string.Equals(remoteHost, desiredRemoteEndpoint.Address.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryResolveDiscoveryCandidate(
            IReadOnlyList<GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates,
            int remotePort,
            int? owningProcessId,
            string owningProcessName,
            int? localPort,
            out GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate candidate,
            out string status)
        {
            candidate = default;
            status = null;

            IReadOnlyList<GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate> filteredCandidates = FilterCandidatesByLocalPort(candidates, localPort);
            if (filteredCandidates.Count == 0)
            {
                status = $"Remote-user official-session discovery found no established TCP session for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
                return false;
            }

            if (filteredCandidates.Count > 1)
            {
                string matches = string.Join(
                    ", ",
                    filteredCandidates.Select(entry => $"{entry.ProcessName}({entry.ProcessId}) local {entry.LocalEndpoint.Port} -> remote {entry.RemoteEndpoint.Port}"));
                status = $"Remote-user official-session discovery found multiple candidates for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}: {matches}. Add a localPort filter.";
                return false;
            }

            candidate = filteredCandidates[0];
            return true;
        }

        private static IReadOnlyList<GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate> FilterCandidatesByLocalPort(
            IReadOnlyList<GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates,
            int? localPort)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return Array.Empty<GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate>();
            }

            return localPort.HasValue
                ? candidates.Where(candidate => candidate.LocalEndpoint.Port == localPort.Value).ToArray()
                : candidates;
        }

        private static string DescribeDiscoveryScope(int? owningProcessId, string owningProcessName, int remotePort, int? localPort)
        {
            string processScope = owningProcessId.HasValue
                ? $"process {owningProcessId.Value}"
                : string.IsNullOrWhiteSpace(owningProcessName)
                    ? DefaultProcessName
                    : owningProcessName;
            string remoteScope = remotePort > 0 ? $"remote port {remotePort}" : "any remote port";
            string localScope = localPort.HasValue ? $" and local port {localPort.Value}" : string.Empty;
            return $"{processScope} on {remoteScope}{localScope}";
        }
    }
}
