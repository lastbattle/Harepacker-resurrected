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
        private const string OfficialSessionSourcePrefix = "official-session:";
        private const string DefaultProcessName = "MapleStory";
        private const ushort V95HireTutorLocalOpcode = LocalUtilityPacketInboxManager.HireTutorClientPacketType;
        private const ushort V95TutorMsgLocalOpcode = LocalUtilityPacketInboxManager.TutorMsgClientPacketType;
        private const int MinOfficialSessionTutorInferenceProofCount = 2;

        private const string OfficialRemoteOwnerEvidence = "v95 CUserPool::OnPacket (0x94ddf0) routes 179 enter, 180 leave, common opcodes 181-209, remote-user opcodes 210-230, and local-user opcodes 231-276; CUserPool::OnUserRemotePacket (0x94b390) dispatches remote-user ownership on 210-230 with no tutor owner branch; CUserLocal::OnPacket (0x9340c0) resolves the full v95 local-user owner table in that 231-276 range with tutor on 255/256 only; CUserRemote::OnAvatarModified (0x954110) is the live relationship-record route for couple/friend/marriage add and remove before CUserPool::Update consumes the tables.";

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

        private static readonly IReadOnlyDictionary<ushort, string> KnownNonTutorLocalOwnerMapV95 = new Dictionary<ushort, string>
        {
            [231] = "CUserLocal::OnSitResult",
            [232] = "CUser::OnEmotion",
            [233] = "CUser::OnEffect",
            [234] = "CUserLocal::OnTeleport",
            [236] = "CUserLocal::OnMesoGive_Succeeded",
            [237] = "CUserLocal::OnMesoGive_Failed",
            [238] = "CUserLocal::OnRandomMesobag_Succeeded",
            [239] = "CUserLocal::OnRandomMesobag_Failed",
            [240] = "CUserLocal::OnFieldFadeInOut",
            [241] = "CUserLocal::OnFieldFadeOutForce",
            [242] = "CUserLocal::OnQuestResult",
            [243] = "CUserLocal::OnNotifyHPDecByField",
            [245] = "CUserLocal::OnBalloonMsg",
            [246] = "CUserLocal::OnPlayEventSound",
            [247] = "CUserLocal::OnPlayMinigameSound",
            [248] = "CUserLocal::OnMakerResult",
            [250] = "CUserLocal::OnOpenClassCompetitionPage",
            [251] = "CUserLocal::OnOpenUI",
            [252] = "CUserLocal::OnOpenUIWithOption",
            [253] = "CUserLocal::OnSetDirectionMode",
            [254] = "CUserLocal::OnSetStandAloneMode",
            [257] = "CUserLocal::OnIncComboResponse",
            [258] = "CUserLocal::OnRandomEmotion",
            [259] = "CUserLocal::OnResignQuestReturn",
            [260] = "CUserLocal::OnPassMateName",
            [261] = "CUserLocal::OnRadioSchedule",
            [262] = "CUserLocal::OnOpenSkillGuide",
            [263] = "CUserLocal::OnNoticeMsg",
            [264] = "CUserLocal::OnChatMsg",
            [265] = "CUserLocal::OnBuffzoneEffect",
            [266] = "CUserLocal::OnGoToCommoditySN",
            [267] = "CUserLocal::OnDamageMeter",
            [268] = "CUserLocal::OnTimeBombAttack",
            [269] = "CUserLocal::OnPassiveMove",
            [270] = "CUserLocal::OnFollowCharacterFailed",
            [271] = "CUserLocal::OnVengeanceSkillApply",
            [272] = "CUserLocal::OnExJablinApply",
            [273] = "CUserLocal::OnAskAPSPEvent",
            [274] = "CUserLocal::OnQuestGuideResult",
            [275] = "CUserLocal::OnDeliveryQuest",
            [276] = "CUserLocal::OnSkillCooltimeSet"
        };
        private static readonly IReadOnlySet<ushort> KnownNoHandlerLocalOwnerOpcodesV95 = new HashSet<ushort>
        {
            235,
            244,
            249
        };

        private readonly ConcurrentQueue<RemoteUserOfficialSessionBridgeMessage> _pendingMessages = new();
        private readonly Dictionary<ushort, int> _packetMap = new(DefaultPacketMap);
        private readonly Dictionary<ushort, LearnedOpcodeEntry> _learnedPacketMap = new();
        private readonly Dictionary<ushort, PendingTutorInferenceEvidence> _pendingTutorInferenceMap = new();
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
                OfficialSessionProofCount = IsOfficialSessionSource(source) ? 1 : 0;
                RememberOfficialSessionBuildProof(source);
            }

            public int PacketType { get; private set; }
            public string Evidence { get; private set; }
            public bool IsManual { get; }
            public string LastSource { get; private set; }
            public int LastPayloadLength { get; private set; }
            public string LastPayloadPreviewHex { get; private set; }
            public int Count { get; private set; }
            public int OfficialSessionProofCount { get; private set; }
            public string OfficialSessionBuildProofSummary => DescribeOfficialSessionBuildProofs();
            private readonly Dictionary<string, int> _officialSessionProofCountByBuild = new(StringComparer.OrdinalIgnoreCase);

            public void Update(int packetType, string evidence, string source, byte[] payload)
            {
                PacketType = packetType;
                Evidence = evidence ?? string.Empty;
                LastSource = string.IsNullOrWhiteSpace(source) ? LastSource : source;
                LastPayloadLength = payload?.Length ?? 0;
                LastPayloadPreviewHex = FormatPayloadPreviewHex(payload);
                Count++;
                if (IsOfficialSessionSource(source))
                {
                    OfficialSessionProofCount++;
                    RememberOfficialSessionBuildProof(source);
                }
            }

            public int ResolveOfficialSessionBuildProofCount(string buildTag)
            {
                string normalizedBuildTag = NormalizeOfficialSessionBuildTag(buildTag);
                if (_officialSessionProofCountByBuild.TryGetValue(normalizedBuildTag, out int proofCount))
                {
                    return proofCount;
                }

                return 0;
            }

            private void RememberOfficialSessionBuildProof(string source)
            {
                if (!TryResolveOfficialSessionBuildTag(source, out string buildTag))
                {
                    return;
                }

                if (_officialSessionProofCountByBuild.TryGetValue(buildTag, out int existingCount))
                {
                    _officialSessionProofCountByBuild[buildTag] = existingCount + 1;
                }
                else
                {
                    _officialSessionProofCountByBuild[buildTag] = 1;
                }
            }

            private string DescribeOfficialSessionBuildProofs()
            {
                if (_officialSessionProofCountByBuild.Count == 0)
                {
                    return "none";
                }

                return string.Join(
                    "|",
                    _officialSessionProofCountByBuild
                        .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(entry => $"{entry.Key}:{entry.Value}"));
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

        private sealed class PendingTutorInferenceEvidence
        {
            public PendingTutorInferenceEvidence(int packetType, string reason, string source, string payloadSignature)
            {
                PacketType = packetType;
                Reason = reason ?? string.Empty;
                LastSource = string.IsNullOrWhiteSpace(source) ? "unknown-source" : source;
                LastPayloadSignature = string.IsNullOrWhiteSpace(payloadSignature) ? "none" : payloadSignature;
                ObservationCount = 1;
                UniqueObservationCount = 1;
                OfficialSessionObservationCount = 0;
                OfficialSessionUniqueObservationCount = 0;
                RememberOfficialSessionBuildObservation(source, LastPayloadSignature);
            }

            public int PacketType { get; private set; }
            public string Reason { get; private set; }
            public string LastSource { get; private set; }
            public string LastPayloadSignature { get; private set; }
            public int ObservationCount { get; private set; }
            public int UniqueObservationCount { get; private set; }
            public int OfficialSessionObservationCount { get; private set; }
            public int OfficialSessionUniqueObservationCount { get; private set; }
            public string OfficialSessionBuildObservationSummary => DescribeOfficialSessionBuildObservations();
            public string OfficialSessionBuildUniqueObservationSummary => DescribeOfficialSessionBuildUniqueObservations();
            private readonly Dictionary<string, int> _officialSessionObservationCountByBuild = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, HashSet<string>> _officialSessionPayloadSignaturesByBuild = new(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> _uniqueObservationSignatures = new(StringComparer.Ordinal);

            public void Update(int packetType, string reason, string source, string payloadSignature)
            {
                PacketType = packetType;
                Reason = reason ?? string.Empty;
                LastSource = string.IsNullOrWhiteSpace(source) ? LastSource : source;
                LastPayloadSignature = string.IsNullOrWhiteSpace(payloadSignature) ? LastPayloadSignature : payloadSignature;
                ObservationCount++;
                if (_uniqueObservationSignatures.Add(LastPayloadSignature))
                {
                    UniqueObservationCount++;
                }
                if (IsOfficialSessionSource(source))
                {
                    OfficialSessionObservationCount++;
                    RememberOfficialSessionBuildObservation(source, LastPayloadSignature);
                }
            }

            public int ResolveOfficialSessionBuildObservationCount(string buildTag)
            {
                string normalizedBuildTag = NormalizeOfficialSessionBuildTag(buildTag);
                if (_officialSessionObservationCountByBuild.TryGetValue(normalizedBuildTag, out int proofCount))
                {
                    return proofCount;
                }

                return 0;
            }

            private void RememberOfficialSessionBuildObservation(string source)
            {
                if (!TryResolveOfficialSessionBuildTag(source, out string buildTag))
                {
                    return;
                }

                if (_officialSessionObservationCountByBuild.TryGetValue(buildTag, out int existingCount))
                {
                    _officialSessionObservationCountByBuild[buildTag] = existingCount + 1;
                }
                else
                {
                    _officialSessionObservationCountByBuild[buildTag] = 1;
                }
            }

            public int ResolveOfficialSessionBuildUniqueObservationCount(string buildTag)
            {
                string normalizedBuildTag = NormalizeOfficialSessionBuildTag(buildTag);
                if (_officialSessionPayloadSignaturesByBuild.TryGetValue(normalizedBuildTag, out HashSet<string> payloadSignatures))
                {
                    return payloadSignatures.Count;
                }

                return 0;
            }

            private void RememberOfficialSessionBuildObservation(string source, string payloadSignature)
            {
                if (!TryResolveOfficialSessionBuildTag(source, out string buildTag))
                {
                    return;
                }

                if (_officialSessionObservationCountByBuild.TryGetValue(buildTag, out int existingCount))
                {
                    _officialSessionObservationCountByBuild[buildTag] = existingCount + 1;
                }
                else
                {
                    _officialSessionObservationCountByBuild[buildTag] = 1;
                }

                if (!_officialSessionPayloadSignaturesByBuild.TryGetValue(buildTag, out HashSet<string> signatures))
                {
                    signatures = new HashSet<string>(StringComparer.Ordinal);
                    _officialSessionPayloadSignaturesByBuild[buildTag] = signatures;
                }

                if (signatures.Add(string.IsNullOrWhiteSpace(payloadSignature) ? "none" : payloadSignature))
                {
                    OfficialSessionUniqueObservationCount++;
                }
            }

            private string DescribeOfficialSessionBuildObservations()
            {
                if (_officialSessionObservationCountByBuild.Count == 0)
                {
                    return "none";
                }

                return string.Join(
                    "|",
                    _officialSessionObservationCountByBuild
                        .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(entry => $"{entry.Key}:{entry.Value}"));
            }

            private string DescribeOfficialSessionBuildUniqueObservations()
            {
                if (_officialSessionPayloadSignaturesByBuild.Count == 0)
                {
                    return "none";
                }

                return string.Join(
                    "|",
                    _officialSessionPayloadSignaturesByBuild
                        .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(entry => $"{entry.Key}:{entry.Value.Count}"));
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
            return $"Remote-user official-session bridge {lifecycle}; {session}; officialOwnerTable={OfficialRemoteOwnerEvidence}; received={ReceivedCount}; forwardedOutbound={ForwardedOutboundCount}; learned={DescribeLearnedPacketMappings()}; pendingTutorInference={DescribePendingTutorInferenceMappings()}. {LastStatus}";
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
                            $"{entry.Key}->{RemoteUserPacketInboxManager.DescribePacketType(entry.Value.PacketType)} ({entry.Value.Evidence}; count={entry.Value.Count}; officialSessionProof={entry.Value.OfficialSessionProofCount}; officialSessionBuildProof={entry.Value.OfficialSessionBuildProofSummary}; source={entry.Value.LastSource}; payloadBytes={entry.Value.LastPayloadLength}; sample={entry.Value.LastPayloadPreviewHex})"));
            }
        }

        public string DescribePendingTutorInferenceMappings()
        {
            lock (_sync)
            {
                return DescribePendingTutorInferenceMappingsNoLock();
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
                _pendingTutorInferenceMap.Remove(opcode);
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
                _pendingTutorInferenceMap.Remove(opcode);
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
                _pendingTutorInferenceMap.Clear();
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
                string officialSessionBuildTag = ResolveOfficialSessionBuildTag(source);
                bool trustV95LocalOwnerTable = !IsOfficialSessionSource(source)
                    || IsBuildTagKnownV95(officialSessionBuildTag);
                bool hasMappedPacketType = _packetMap.TryGetValue(opcode, out packetType);
                if (hasMappedPacketType
                    && ShouldRevalidateTutorOpcodeMappingForSourceNoLock(opcode, packetType, source, out string revalidationStatus))
                {
                    LastStatus = revalidationStatus;
                    hasMappedPacketType = false;
                }

                if (!hasMappedPacketType)
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
                    if (trustV95LocalOwnerTable
                        && TryResolveKnownTutorPacketTypeFromV95LocalOwnerTableNoLock(opcode, out packetType, out string knownOwnerReason))
                    {
                        if (!TryValidateKnownTutorOwnerPayloadNoLock(opcode, inferencePayload, packetType, out string knownPayloadReason))
                        {
                            LastStatus = $"Ignored CUserPool local-user opcode {opcode}: known tutor owner payload did not match exact remote wrapper ({knownPayloadReason}). {OfficialRemoteOwnerEvidence}";
                            return false;
                        }

                        _packetMap[opcode] = packetType;
                        string learnedEvidence = $"auto:{knownOwnerReason}; payloadProof={knownPayloadReason}; {OfficialRemoteOwnerEvidence}";
                        RememberLearnedOpcodeNoLock(opcode, packetType, learnedEvidence, isManual: false, source, inferencePayload);
                        LastStatus = $"Auto-mapped remote-user opcode {opcode} to {RemoteUserPacketInboxManager.DescribePacketType(packetType)} from recovered CUserLocal owner table ({knownOwnerReason}) with exact remote wrapper proof ({knownPayloadReason}); {OfficialRemoteOwnerEvidence}";
                    }
                    else if (trustV95LocalOwnerTable
                        && TryResolveKnownNonTutorLocalOwnerFromV95LocalOwnerTable(opcode, out string knownNonTutorReason))
                    {
                        LastStatus = $"Ignored CUserPool local-user opcode {opcode}: known recovered CUserLocal::OnPacket owner ({knownNonTutorReason}) is non-tutor. {OfficialRemoteOwnerEvidence}";
                        return false;
                    }
                    else if (!TryInferInboundRemoteTutorPacketTypeFromV95TutorOwnerTableNoLock(
                                 opcode,
                                 inferencePayload,
                                 trustV95LocalOwnerTable,
                                 out packetType,
                                 out string inferenceReason))
                    {
                        LastStatus = trustV95LocalOwnerTable
                            ? $"Ignored CUserPool local-user opcode {opcode}: payload did not match an exact remote tutor-owner wrapper. {OfficialRemoteOwnerEvidence}"
                            : $"Ignored CUserPool local-user opcode {opcode} on build {officialSessionBuildTag}: payload did not match an exact remote tutor-owner wrapper while v95 owner-table shortcuts were disabled. {OfficialRemoteOwnerEvidence}";
                        return false;
                    }
                    else
                    {
                        if (!TryObserveTutorInferenceNoLock(opcode, packetType, inferenceReason, source, inferencePayload, out PendingTutorInferenceEvidence pendingEvidence, out bool inferenceConfirmed))
                        {
                            LastStatus = $"Ignored CUserPool local-user opcode {opcode}: tutor payload inference conflict while collecting evidence for {RemoteUserPacketInboxManager.DescribePacketType(packetType)}.";
                            return false;
                        }

                        if (!inferenceConfirmed)
                        {
                            string buildTag = ResolveOfficialSessionBuildTag(source);
                            int buildProof = pendingEvidence.ResolveOfficialSessionBuildUniqueObservationCount(buildTag);
                            LastStatus = $"Observed potential tutor-owner mapping for opcode {opcode} -> {RemoteUserPacketInboxManager.DescribePacketType(packetType)} ({pendingEvidence.Reason}); awaiting official-session distinct wrapper proof {buildProof}/{MinOfficialSessionTutorInferenceProofCount} for build {buildTag} ({pendingEvidence.OfficialSessionBuildUniqueObservationSummary}). {OfficialRemoteOwnerEvidence}";
                            return false;
                        }

                        _pendingTutorInferenceMap.Remove(opcode);
                        _packetMap[opcode] = packetType;
                        string inferenceBuildTag = ResolveOfficialSessionBuildTag(source);
                        int inferenceBuildProof = pendingEvidence.ResolveOfficialSessionBuildUniqueObservationCount(inferenceBuildTag);
                        string learnedEvidence = $"auto:{inferenceReason}; inferenceDistinctWrapperProof={inferenceBuildProof}/{MinOfficialSessionTutorInferenceProofCount}@{inferenceBuildTag}; {OfficialRemoteOwnerEvidence}";
                        RememberLearnedOpcodeNoLock(opcode, packetType, learnedEvidence, isManual: false, source, inferencePayload);
                        LastStatus = $"Auto-mapped remote-user opcode {opcode} to {RemoteUserPacketInboxManager.DescribePacketType(packetType)} from tutor payload inference ({inferenceReason}) after official-session distinct wrapper proof {inferenceBuildProof}/{MinOfficialSessionTutorInferenceProofCount} for build {inferenceBuildTag}; {OfficialRemoteOwnerEvidence}";
                    }
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

        internal static bool IsOfficialTutorLocalOpcodeCoveredByV95OwnerTable(ushort opcode)
        {
            return opcode == V95HireTutorLocalOpcode
                || opcode == V95TutorMsgLocalOpcode;
        }

        private static bool TryResolveKnownTutorPacketTypeFromV95LocalOwnerTableNoLock(
            ushort opcode,
            out int packetType,
            out string reason)
        {
            packetType = 0;
            reason = string.Empty;
            if (opcode == V95HireTutorLocalOpcode)
            {
                packetType = (int)Pools.RemoteUserPacketType.UserTutorHire;
                reason = $"v95 CUserLocal::OnPacket case {V95HireTutorLocalOpcode} owns OnHireTutor";
                return true;
            }

            if (opcode == V95TutorMsgLocalOpcode)
            {
                packetType = (int)Pools.RemoteUserPacketType.UserTutorMessage;
                reason = $"v95 CUserLocal::OnPacket case {V95TutorMsgLocalOpcode} owns OnTutorMsg";
                return true;
            }

            return false;
        }

        private static bool TryResolveKnownNonTutorLocalOwnerFromV95LocalOwnerTable(ushort opcode, out string reason)
        {
            if (KnownNonTutorLocalOwnerMapV95.TryGetValue(opcode, out string owner))
            {
                reason = $"v95 CUserLocal::OnPacket case {opcode} -> {owner}";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        private static bool TryInferInboundRemoteTutorPacketTypeFromV95TutorOwnerTableNoLock(
            ushort opcode,
            byte[] payload,
            bool trustV95LocalOwnerTable,
            out int packetType,
            out string reason)
        {
            packetType = 0;
            reason = string.Empty;
            if (trustV95LocalOwnerTable
                && !IsOfficialTutorLocalOpcodeCoveredByV95OwnerTable(opcode)
                && !KnownNoHandlerLocalOwnerOpcodesV95.Contains(opcode))
            {
                return false;
            }

            if (payload == null || payload.Length < sizeof(int) + 1)
            {
                return false;
            }

            bool opcodeMatchesKnownHireCase = opcode == V95HireTutorLocalOpcode;
            bool opcodeMatchesKnownMessageCase = opcode == V95TutorMsgLocalOpcode;
            bool hirePayloadMatches = HaCreator.MapSimulator.MapSimulator.TryDecodeRemotePacketOwnedTutorHirePayload(
                payload,
                out int hireCharacterId,
                out bool enabled,
                out _);
            bool messagePayloadMatches = HaCreator.MapSimulator.MapSimulator.TryDecodeRemotePacketOwnedTutorMessagePayload(
                payload,
                out int messageCharacterId,
                out bool indexedPayload,
                out int messageIndex,
                out int durationMs,
                out string text,
                out int width,
                out _);

            if (hirePayloadMatches && messagePayloadMatches)
            {
                reason = $"opcode {opcode} payload matched both OnHireTutor and OnTutorMsg wrappers";
                return false;
            }

            if (opcodeMatchesKnownHireCase && hirePayloadMatches)
            {
                packetType = (int)Pools.RemoteUserPacketType.UserTutorHire;
                reason = $"v95 CUserLocal::OnPacket case {V95HireTutorLocalOpcode} -> OnHireTutor exact remote wrapper for character {hireCharacterId}, enabled={enabled}";
                return true;
            }

            if (opcodeMatchesKnownMessageCase && messagePayloadMatches)
            {
                packetType = (int)Pools.RemoteUserPacketType.UserTutorMessage;
                reason = indexedPayload
                    ? $"v95 CUserLocal::OnPacket case {V95TutorMsgLocalOpcode} -> OnTutorMsg exact remote indexed-wrapper for character {messageCharacterId}, index={messageIndex}, duration={durationMs}"
                    : $"v95 CUserLocal::OnPacket case {V95TutorMsgLocalOpcode} -> OnTutorMsg exact remote text-wrapper for character {messageCharacterId}, width={width}, duration={durationMs}, textLength={text?.Length ?? 0}";
                return true;
            }

            if (hirePayloadMatches)
            {
                packetType = (int)Pools.RemoteUserPacketType.UserTutorHire;
                reason = $"recovered CUserPool local-user opcode {opcode} inferred as OnHireTutor by exact remote wrapper shape for character {hireCharacterId}, enabled={enabled}";
                return true;
            }

            if (messagePayloadMatches)
            {
                packetType = (int)Pools.RemoteUserPacketType.UserTutorMessage;
                reason = indexedPayload
                    ? $"recovered CUserPool local-user opcode {opcode} inferred as OnTutorMsg indexed wrapper for character {messageCharacterId}, index={messageIndex}, duration={durationMs}"
                    : $"recovered CUserPool local-user opcode {opcode} inferred as OnTutorMsg text wrapper for character {messageCharacterId}, width={width}, duration={durationMs}, textLength={text?.Length ?? 0}";
                return true;
            }

            return false;
        }

        private static bool IsBuildTagKnownV95(string buildTag)
        {
            string normalizedBuildTag = NormalizeOfficialSessionBuildTag(buildTag);
            return string.Equals(normalizedBuildTag, "v95", StringComparison.OrdinalIgnoreCase);
        }

        private bool TryObserveTutorInferenceNoLock(
            ushort opcode,
            int packetType,
            string reason,
            string source,
            byte[] payload,
            out PendingTutorInferenceEvidence evidence,
            out bool inferenceConfirmed)
        {
            evidence = null;
            inferenceConfirmed = false;
            string payloadSignature = BuildTutorInferencePayloadSignature(packetType, payload);

            if (_pendingTutorInferenceMap.TryGetValue(opcode, out PendingTutorInferenceEvidence existing))
            {
                if (existing.PacketType != packetType)
                {
                    _pendingTutorInferenceMap[opcode] = new PendingTutorInferenceEvidence(packetType, reason, source, payloadSignature);
                }
                else
                {
                    existing.Update(packetType, reason, source, payloadSignature);
                }
            }
            else
            {
                _pendingTutorInferenceMap[opcode] = new PendingTutorInferenceEvidence(packetType, reason, source, payloadSignature);
            }

            evidence = _pendingTutorInferenceMap[opcode];
            if (!IsOfficialSessionSource(source))
            {
                inferenceConfirmed = false;
                return true;
            }

            string buildTag = ResolveOfficialSessionBuildTag(source);
            inferenceConfirmed = evidence.ResolveOfficialSessionBuildUniqueObservationCount(buildTag) >= MinOfficialSessionTutorInferenceProofCount;
            return true;
        }

        private static bool TryValidateKnownTutorOwnerPayloadNoLock(
            ushort opcode,
            byte[] payload,
            int expectedPacketType,
            out string reason)
        {
            reason = string.Empty;
            if (expectedPacketType == (int)Pools.RemoteUserPacketType.UserTutorHire)
            {
                bool decoded = HaCreator.MapSimulator.MapSimulator.TryDecodeRemotePacketOwnedTutorHirePayload(
                    payload,
                    out int characterId,
                    out bool enabled,
                    out string decodeMessage);
                if (!decoded)
                {
                    reason = decodeMessage;
                    return false;
                }

                reason = $"opcode {opcode} remote hire wrapper for character {characterId}, enabled={enabled}";
                return true;
            }

            if (expectedPacketType == (int)Pools.RemoteUserPacketType.UserTutorMessage)
            {
                bool decoded = HaCreator.MapSimulator.MapSimulator.TryDecodeRemotePacketOwnedTutorMessagePayload(
                    payload,
                    out int characterId,
                    out bool indexedPayload,
                    out int messageIndex,
                    out int durationMs,
                    out string text,
                    out int width,
                    out string decodeMessage);
                if (!decoded)
                {
                    reason = decodeMessage;
                    return false;
                }

                reason = indexedPayload
                    ? $"opcode {opcode} remote indexed-message wrapper for character {characterId}, index={messageIndex}, duration={durationMs}"
                    : $"opcode {opcode} remote text-message wrapper for character {characterId}, width={width}, duration={durationMs}, textLength={text?.Length ?? 0}";
                return true;
            }

            reason = $"unsupported expected tutor packet type {expectedPacketType}";
            return false;
        }

        private static string BuildTutorInferencePayloadSignature(int packetType, byte[] payload)
        {
            if (packetType == (int)Pools.RemoteUserPacketType.UserTutorHire)
            {
                bool decoded = HaCreator.MapSimulator.MapSimulator.TryDecodeRemotePacketOwnedTutorHirePayload(
                    payload,
                    out int characterId,
                    out bool enabled,
                    out _);
                if (!decoded)
                {
                    return $"invalid-hire:{payload?.Length ?? 0}";
                }

                return $"hire:{characterId}:{(enabled ? 1 : 0)}";
            }

            if (packetType == (int)Pools.RemoteUserPacketType.UserTutorMessage)
            {
                bool decoded = HaCreator.MapSimulator.MapSimulator.TryDecodeRemotePacketOwnedTutorMessagePayload(
                    payload,
                    out int characterId,
                    out bool indexedPayload,
                    out int messageIndex,
                    out int durationMs,
                    out string text,
                    out int width,
                    out _);
                if (!decoded)
                {
                    return $"invalid-message:{payload?.Length ?? 0}";
                }

                if (indexedPayload)
                {
                    return $"msg-indexed:{characterId}:{messageIndex}:{durationMs}";
                }

                int textHash = string.IsNullOrEmpty(text)
                    ? 0
                    : StringComparer.Ordinal.GetHashCode(text);
                return $"msg-text:{characterId}:{width}:{durationMs}:{text?.Length ?? 0}:{textHash}";
            }

            return $"packet:{packetType}:{payload?.Length ?? 0}";
        }

        private bool ShouldRevalidateTutorOpcodeMappingForSourceNoLock(
            ushort opcode,
            int packetType,
            string source,
            out string status)
        {
            status = null;
            if (!IsOfficialSessionSource(source))
            {
                return false;
            }

            if (packetType != (int)Pools.RemoteUserPacketType.UserTutorHire
                && packetType != (int)Pools.RemoteUserPacketType.UserTutorMessage)
            {
                return false;
            }

            if (!_learnedPacketMap.TryGetValue(opcode, out LearnedOpcodeEntry learnedEntry)
                || learnedEntry.IsManual)
            {
                return false;
            }

            string buildTag = ResolveOfficialSessionBuildTag(source);
            int buildProof = learnedEntry.ResolveOfficialSessionBuildProofCount(buildTag);
            if (buildProof >= MinOfficialSessionTutorInferenceProofCount)
            {
                return false;
            }

            _packetMap.Remove(opcode);
            _pendingTutorInferenceMap.Remove(opcode);
            status = $"Suspended learned tutor mapping for opcode {opcode} on build {buildTag}: official-session proof {buildProof}/{MinOfficialSessionTutorInferenceProofCount}. Awaiting build-specific tutor-owner evidence. {OfficialRemoteOwnerEvidence}";
            return true;
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

        private static bool IsOfficialSessionSource(string source)
        {
            return !string.IsNullOrWhiteSpace(source)
                && source.StartsWith(OfficialSessionSourcePrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveOfficialSessionBuildTag(string source)
        {
            return TryResolveOfficialSessionBuildTag(source, out string buildTag)
                ? buildTag
                : NormalizeOfficialSessionBuildTag(null);
        }

        private static bool TryResolveOfficialSessionBuildTag(string source, out string buildTag)
        {
            buildTag = NormalizeOfficialSessionBuildTag(null);
            if (!IsOfficialSessionSource(source))
            {
                return false;
            }

            string suffix = source.Substring(OfficialSessionSourcePrefix.Length);
            int delimiterIndex = suffix.IndexOf(':');
            if (delimiterIndex > 0)
            {
                string segment = suffix.Substring(0, delimiterIndex);
                buildTag = NormalizeOfficialSessionBuildTag(segment);
                return true;
            }

            buildTag = NormalizeOfficialSessionBuildTag(suffix);
            return true;
        }

        private static string NormalizeOfficialSessionBuildTag(string buildTag)
        {
            string trimmed = string.IsNullOrWhiteSpace(buildTag) ? string.Empty : buildTag.Trim();
            if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("build", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.ToLowerInvariant();
            }

            return "unknown";
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

                string buildTag = pair.Version > 0 ? $"v{pair.Version}" : "unknown";
                if (!TryDecodeInboundRemoteUserPacket(raw, $"{OfficialSessionSourcePrefix}{buildTag}:{pair.RemoteEndpoint}", out RemoteUserOfficialSessionBridgeMessage message))
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
                _pendingTutorInferenceMap.Remove(opcode);
                if (!DefaultPacketMap.ContainsKey(opcode))
                {
                    _packetMap.Remove(opcode);
                }
            }

            _pendingTutorInferenceMap.Clear();
        }

        private string DescribePendingTutorInferenceMappingsNoLock()
        {
            if (_pendingTutorInferenceMap.Count == 0)
            {
                return "none";
            }

            return string.Join(
                ", ",
                _pendingTutorInferenceMap
                    .OrderBy(entry => entry.Key)
                    .Select(entry =>
                        $"{entry.Key}->{RemoteUserPacketInboxManager.DescribePacketType(entry.Value.PacketType)} (observed={entry.Value.ObservationCount}; officialSessionObserved={entry.Value.OfficialSessionObservationCount}; officialSessionBuildObserved={entry.Value.OfficialSessionBuildObservationSummary}; reason={entry.Value.Reason}; source={entry.Value.LastSource})"));
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
