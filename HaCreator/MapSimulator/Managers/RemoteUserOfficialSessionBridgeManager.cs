using MapleLib.PacketLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class RemoteUserOfficialSessionBridgeMessage
    {
        public RemoteUserOfficialSessionBridgeMessage(
            int packetType,
            byte[] payload,
            string source,
            ushort opcode,
            int officialSessionSequence = 0)
        {
            PacketType = packetType;
            Payload = payload ?? Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "remote-user-session" : source;
            Opcode = opcode;
            OfficialSessionSequence = officialSessionSequence;
        }

        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public ushort Opcode { get; }
        public int OfficialSessionSequence { get; }
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

        private const string OfficialRemoteOwnerEvidence = "v95 CUserPool::OnPacket (0x94ddf0) routes 179 enter, 180 leave, common opcodes 181-209, remote-user opcodes 210-230, and local-user opcodes 231-276; CUserPool::OnUserRemotePacket (0x94b390) dispatches remote-user ownership on 210-230 with no tutor owner branch; CUserLocal::OnPacket (0x9340c0) resolves the full v95 local-user owner table in that 231-276 range with tutor on 255/256 only; CUserRemote::OnAvatarModified (0x954110) is the live relationship-record route for couple/friend/marriage add and remove before CUserPool::Update consumes the tables; CUserPool::Update couple-chair lock admission at 0x94c9f4/0x94ca01 consumes raw pair records only when dwPairCharacterID is 0 or the current owner.";
        private const string OfficialPortableChairRecordEvidence = "WZ Item/Install/0301.img/03012000/info authors distanceX=53, distanceY=0, maxDiff=6, direction=21 and Effect/ItemEff.img/3012000/0 provides seat-bound 300ms effect frames; compact 8-byte chair payloads remain ambiguous with seat-state traffic, so only the extended 16-byte packet-owned couple-chair record-add wrapper is promoted from live capture.";

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
        private readonly Dictionary<ushort, string> _portableChairRecordInferenceMap = new();
        private readonly Dictionary<string, Dictionary<int, ushort>> _portableChairRecordAddOpcodeByCharacterByBuild = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<ushort, LearnedOpcodeEntry>> _learnedTutorPacketMapByBuild = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<ushort, PendingTutorInferenceEvidence> _pendingTutorInferenceMap = new();
        private readonly Dictionary<ushort, string> _tutorInferenceConflictMap = new();
        private readonly object _sync = new();
        private readonly MapleRoleSessionProxy _roleSessionProxy;

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

            public void EnsureOfficialSessionBuildProofCount(string buildTag, int proofCount)
            {
                string normalizedBuildTag = NormalizeOfficialSessionBuildTag(buildTag);
                int normalizedProofCount = Math.Max(0, proofCount);
                if (normalizedProofCount <= 0)
                {
                    return;
                }

                _officialSessionProofCountByBuild.TryGetValue(normalizedBuildTag, out int existingProofCount);
                if (existingProofCount >= normalizedProofCount)
                {
                    return;
                }

                int addedProofCount = normalizedProofCount - existingProofCount;
                _officialSessionProofCountByBuild[normalizedBuildTag] = normalizedProofCount;
                OfficialSessionProofCount += addedProofCount;
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
                OfficialSessionObservationCount = IsOfficialSessionSource(source) ? 1 : 0;
                OfficialSessionUniqueObservationCount = 0;
                _uniqueObservationSignatures.Add(LastPayloadSignature);
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

        public RemoteUserOfficialSessionBridgeManager(Func<MapleRoleSessionProxy> roleSessionProxyFactory = null)
        {
            _roleSessionProxy = (roleSessionProxyFactory ?? (() => MapleRoleSessionProxyFactory.GlobalV95.CreateChannel()))();
            _roleSessionProxy.ServerPacketReceived += OnRoleSessionServerPacketReceived;
            _roleSessionProxy.ClientPacketReceived += OnRoleSessionClientPacketReceived;
        }

        public int ListenPort { get; private set; } = DefaultListenPort;
        public string RemoteHost { get; private set; } = IPAddress.Loopback.ToString();
        public int RemotePort { get; private set; }
        public bool IsRunning => _roleSessionProxy.IsRunning;
        public bool HasAttachedClient => _roleSessionProxy.HasAttachedClient;
        public bool HasConnectedSession => _roleSessionProxy.HasConnectedSession;
        public int ReceivedCount { get; private set; }
        public int ForwardedOutboundCount { get; private set; }
        public string LastStatus { get; private set; } = "Remote-user official-session bridge inactive.";

        public string DescribeStatus()
        {
            string lifecycle = IsRunning
                ? $"listening on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}"
                : "inactive";
            string session = HasConnectedSession
                ? $"connected Maple session {RemoteHost}:{RemotePort}"
                : "no active Maple session";
            return $"Remote-user official-session bridge {lifecycle}; {session}; officialOwnerTable={OfficialRemoteOwnerEvidence}; portableChairRecordInference={DescribePortableChairRecordInferences()}; received={ReceivedCount}; forwardedOutbound={ForwardedOutboundCount}; learned={DescribeLearnedPacketMappings()}; learnedTutorByBuild={DescribeLearnedTutorMappingsByBuild()}; pendingTutorInference={DescribePendingTutorInferenceMappings()}; tutorInferenceConflicts={DescribeTutorInferenceConflicts()}. {LastStatus}";
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

        private string DescribePortableChairRecordInferences()
        {
            lock (_sync)
            {
                if (_portableChairRecordInferenceMap.Count == 0)
                {
                    return "none";
                }

                return string.Join(
                    ", ",
                    _portableChairRecordInferenceMap
                        .OrderBy(entry => entry.Key)
                        .Select(entry => $"{entry.Key}:{entry.Value}"));
            }
        }

        public string DescribePendingTutorInferenceMappings()
        {
            lock (_sync)
            {
                return DescribePendingTutorInferenceMappingsNoLock();
            }
        }

        public string DescribeLearnedTutorMappingsByBuild()
        {
            lock (_sync)
            {
                return DescribeLearnedTutorMappingsByBuildNoLock();
            }
        }

        public string DescribeTutorInferenceConflicts()
        {
            lock (_sync)
            {
                return DescribeTutorInferenceConflictsNoLock();
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
                    if (!_roleSessionProxy.Start(ListenPort, RemoteHost, RemotePort, out string proxyStatus))
                    {
                        StopInternal(clearPending: false);
                        LastStatus = proxyStatus;
                        return;
                    }

                    LastStatus = $"Remote-user official-session bridge listening on 127.0.0.1:{ListenPort}, proxying to {RemoteHost}:{RemotePort}, and filtering CUserPool opcodes 179, 180, and 181-230. {proxyStatus}";
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
                _tutorInferenceConflictMap.Remove(opcode);
                RememberLearnedOpcodeNoLock(opcode, packetType, "manual", isManual: true, source: "manual", payload: null);
            }

            status = $"Mapped remote-user opcode {opcode} to {RemoteUserPacketInboxManager.DescribePacketType(packetType)}.";
            LastStatus = status;
            return true;
        }

        public bool TryConfigureBuildScopedTutorPacketMapping(string buildTag, ushort opcode, int packetType, out string status)
        {
            if (packetType != (int)Pools.RemoteUserPacketType.UserTutorHire
                && packetType != (int)Pools.RemoteUserPacketType.UserTutorMessage)
            {
                status = $"Build-scoped tutor mapping only supports {RemoteUserPacketInboxManager.DescribePacketType((int)Pools.RemoteUserPacketType.UserTutorHire)} and {RemoteUserPacketInboxManager.DescribePacketType((int)Pools.RemoteUserPacketType.UserTutorMessage)}.";
                LastStatus = status;
                return false;
            }

            string normalizedBuildTag = NormalizeOfficialSessionBuildTag(buildTag);
            if (string.Equals(normalizedBuildTag, NormalizeOfficialSessionBuildTag(null), StringComparison.OrdinalIgnoreCase))
            {
                status = "Build-scoped tutor mapping requires a versioned build tag such as v95, v999, or build123.";
                LastStatus = status;
                return false;
            }

            lock (_sync)
            {
                if (!_learnedTutorPacketMapByBuild.TryGetValue(normalizedBuildTag, out Dictionary<ushort, LearnedOpcodeEntry> learnedMap))
                {
                    learnedMap = new Dictionary<ushort, LearnedOpcodeEntry>();
                    _learnedTutorPacketMapByBuild[normalizedBuildTag] = learnedMap;
                }

                string source = $"{OfficialSessionSourcePrefix}{normalizedBuildTag}:manual";
                string evidence = $"manual-build-scoped:{normalizedBuildTag}; exact remote tutor wrapper is still validated before dispatch; {OfficialRemoteOwnerEvidence}";
                if (learnedMap.TryGetValue(opcode, out LearnedOpcodeEntry existing))
                {
                    existing.Update(packetType, evidence, source, payload: null);
                }
                else
                {
                    learnedMap[opcode] = new LearnedOpcodeEntry(packetType, evidence, isManual: true, source, payload: null);
                }

                _pendingTutorInferenceMap.Remove(opcode);
                _tutorInferenceConflictMap.Remove(opcode);
            }

            status = $"Mapped build {normalizedBuildTag} remote-user tutor opcode {opcode} to {RemoteUserPacketInboxManager.DescribePacketType(packetType)} with decode-time wrapper validation.";
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
                RemoveLearnedTutorMappingByBuildNoLock(opcode);
                _pendingTutorInferenceMap.Remove(opcode);
                _tutorInferenceConflictMap.Remove(opcode);
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
                _learnedTutorPacketMapByBuild.Clear();
                _pendingTutorInferenceMap.Clear();
                _tutorInferenceConflictMap.Clear();
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
                : $"{RemoteUserPacketInboxManager.DescribePacketType(message.PacketType)} opcode {message.Opcode}{FormatOfficialSessionSequence(message.OfficialSessionSequence)}";
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

                byte[] inferencePayload = null;
                if (hasMappedPacketType)
                {
                    inferencePayload = rawPacket.Skip(sizeof(ushort)).ToArray();
                    RememberMappedPortableChairRecordEvidenceNoLock(opcode, packetType, inferencePayload, source);
                }

                if (!hasMappedPacketType)
                {
                    if (!IsOfficialSessionSource(source))
                    {
                        LastStatus = $"Ignored unmapped remote-user opcode {opcode}: tutor-owner opcode inference requires official-session capture evidence. {OfficialRemoteOwnerEvidence}";
                        return false;
                    }

                    inferencePayload ??= rawPacket.Skip(sizeof(ushort)).ToArray();
                    if (TryResolveExtendedPortableChairRecordAddFromCaptureNoLock(
                            opcode,
                            inferencePayload,
                            source,
                            out packetType,
                            out string portableChairRecordReason,
                            out int portableChairRecordCharacterId))
                    {
                        _packetMap[opcode] = packetType;
                        string learnedEvidence = $"auto:{portableChairRecordReason}; {OfficialRemoteOwnerEvidence}; {OfficialPortableChairRecordEvidence}";
                        _portableChairRecordInferenceMap[opcode] = portableChairRecordReason;
                        RememberPortableChairRecordAddOwnerNoLock(source, portableChairRecordCharacterId, opcode);
                        RememberLearnedOpcodeNoLock(opcode, packetType, learnedEvidence, isManual: false, source, inferencePayload);
                        LastStatus = $"Auto-mapped remote-user opcode {opcode} to {RemoteUserPacketInboxManager.DescribePacketType(packetType)} from extended packet-owned couple-chair record-add capture ({portableChairRecordReason}). {OfficialPortableChairRecordEvidence}";
                        hasMappedPacketType = true;
                    }

                    if (!hasMappedPacketType
                        && TryResolvePortableChairRecordRemoveFromPriorAddCaptureNoLock(
                            opcode,
                            inferencePayload,
                            source,
                            out packetType,
                            out string portableChairRecordRemoveReason))
                    {
                        _packetMap[opcode] = packetType;
                        string learnedEvidence = $"auto:{portableChairRecordRemoveReason}; {OfficialRemoteOwnerEvidence}; {OfficialPortableChairRecordEvidence}";
                        _portableChairRecordInferenceMap[opcode] = portableChairRecordRemoveReason;
                        RememberLearnedOpcodeNoLock(opcode, packetType, learnedEvidence, isManual: false, source, inferencePayload);
                        LastStatus = $"Auto-mapped remote-user opcode {opcode} to {RemoteUserPacketInboxManager.DescribePacketType(packetType)} from packet-owned couple-chair record-remove capture paired with prior add evidence ({portableChairRecordRemoveReason}). {OfficialPortableChairRecordEvidence}";
                        hasMappedPacketType = true;
                    }

                    bool resolvedFromBuildScopedTutorMapping = false;
                    if (!hasMappedPacketType
                        && TryResolveBuildScopedLearnedTutorPacketTypeNoLock(
                            opcode,
                            source,
                            inferencePayload,
                            out packetType,
                            out string buildScopedReason))
                    {
                        _packetMap[opcode] = packetType;
                        string learnedEvidence = $"auto:{buildScopedReason}; {OfficialRemoteOwnerEvidence}";
                        RememberLearnedOpcodeNoLock(opcode, packetType, learnedEvidence, isManual: false, source, inferencePayload);
                        LastStatus = $"Restored remote-user opcode {opcode} to {RemoteUserPacketInboxManager.DescribePacketType(packetType)} from build-scoped tutor evidence ({buildScopedReason}); {OfficialRemoteOwnerEvidence}";
                        resolvedFromBuildScopedTutorMapping = true;
                    }
                    else if (_tutorInferenceConflictMap.TryGetValue(opcode, out string conflictReason))
                    {
                        LastStatus = $"Ignored CUserPool local-user opcode {opcode}: tutor payload inference is suspended due to conflicting tutor wrapper observations ({conflictReason}). Remove or manually remap this opcode to continue.";
                        return false;
                    }

                    if (!hasMappedPacketType
                        && !resolvedFromBuildScopedTutorMapping
                        && IsOfficialRemoteOpcodeCoveredByV95OwnerTable(opcode))
                    {
                        LastStatus = $"Ignored native non-local CUser opcode {opcode}: recovered v95 CUserPool::OnUserRemotePacket owner range has no CTutor/CSummoned tutor branch, so tutor-shaped payloads are not promoted from remote-user opcodes. {OfficialRemoteOwnerEvidence}";
                        return false;
                    }

                    if (!hasMappedPacketType
                        && !resolvedFromBuildScopedTutorMapping
                        && trustV95LocalOwnerTable
                        && !IsOfficialLocalUserOpcodeCoveredByV95OwnerTable(opcode))
                    {
                        LastStatus = $"Ignored unmapped remote-user opcode {opcode}: it is outside the recovered CUserPool::OnPacket local-user tutor owner range. {OfficialRemoteOwnerEvidence}";
                        return false;
                    }

                    string inferenceReason = string.Empty;
                    if (!hasMappedPacketType
                        && !resolvedFromBuildScopedTutorMapping
                        && trustV95LocalOwnerTable
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
                    else if (!hasMappedPacketType
                        && !resolvedFromBuildScopedTutorMapping
                        && trustV95LocalOwnerTable
                        && TryResolveKnownNonTutorLocalOwnerFromV95LocalOwnerTable(opcode, out string knownNonTutorReason))
                    {
                        LastStatus = $"Ignored CUserPool local-user opcode {opcode}: known recovered CUserLocal::OnPacket owner ({knownNonTutorReason}) is non-tutor. {OfficialRemoteOwnerEvidence}";
                        return false;
                    }
                    else if (!hasMappedPacketType
                        && !resolvedFromBuildScopedTutorMapping
                        && trustV95LocalOwnerTable
                        && KnownNoHandlerLocalOwnerOpcodesV95.Contains(opcode))
                    {
                        LastStatus = $"Ignored CUserPool local-user opcode {opcode}: recovered v95 CUserLocal::OnPacket has no handler for this local-owner case, so tutor inference is disabled for this opcode. {OfficialRemoteOwnerEvidence}";
                        return false;
                    }
                    else if (!hasMappedPacketType
                        && !resolvedFromBuildScopedTutorMapping
                        && !TryInferInboundRemoteTutorPacketTypeFromV95TutorOwnerTableNoLock(
                                 opcode,
                                 inferencePayload,
                                 trustV95LocalOwnerTable,
                                 out packetType,
                                 out inferenceReason))
                    {
                        LastStatus = trustV95LocalOwnerTable
                            ? $"Ignored CUserPool local-user opcode {opcode}: payload did not match an exact remote tutor-owner wrapper. {OfficialRemoteOwnerEvidence}"
                            : $"Ignored CUserPool local-user opcode {opcode} on build {officialSessionBuildTag}: payload did not match an exact remote tutor-owner wrapper while v95 owner-table shortcuts were disabled. {OfficialRemoteOwnerEvidence}";
                        return false;
                    }
                    else if (!hasMappedPacketType
                        && !resolvedFromBuildScopedTutorMapping)
                    {
                        if (!TryObserveTutorInferenceNoLock(opcode, packetType, inferenceReason, source, inferencePayload, out PendingTutorInferenceEvidence pendingEvidence, out bool inferenceConfirmed, out string inferenceConflictReason))
                        {
                            LastStatus = string.IsNullOrWhiteSpace(inferenceConflictReason)
                                ? $"Ignored CUserPool local-user opcode {opcode}: tutor payload inference conflict while collecting evidence for {RemoteUserPacketInboxManager.DescribePacketType(packetType)}."
                                : $"Ignored CUserPool local-user opcode {opcode}: tutor payload inference conflict while collecting evidence for {RemoteUserPacketInboxManager.DescribePacketType(packetType)} ({inferenceConflictReason}).";
                            return false;
                        }

                        if (!inferenceConfirmed)
                        {
                            string buildTag = ResolveOfficialSessionBuildTag(source);
                            int buildProof = pendingEvidence.ResolveOfficialSessionBuildUniqueObservationCount(buildTag);
                            LastStatus = $"Observed potential tutor-owner mapping for opcode {opcode} -> {RemoteUserPacketInboxManager.DescribePacketType(packetType)} ({pendingEvidence.Reason}); awaiting official-session distinct wrapper proof {buildProof}/{MinOfficialSessionTutorInferenceProofCount} for build {buildTag} ({pendingEvidence.OfficialSessionBuildUniqueObservationSummary}). {OfficialRemoteOwnerEvidence}";
                            return false;
                        }

                        string inferenceBuildTag = ResolveOfficialSessionBuildTag(source);
                        int inferenceBuildProof = pendingEvidence.ResolveOfficialSessionBuildUniqueObservationCount(inferenceBuildTag);
                        if (!trustV95LocalOwnerTable
                            && !HasBuildScopedCompanionTutorOwnerProofNoLock(opcode, packetType, inferenceBuildTag, out string companionReason))
                        {
                            LastStatus = $"Observed potential tutor-owner mapping for opcode {opcode} -> {RemoteUserPacketInboxManager.DescribePacketType(packetType)} ({pendingEvidence.Reason}); build {inferenceBuildTag} has distinct wrapper proof {inferenceBuildProof}/{MinOfficialSessionTutorInferenceProofCount}, but awaits companion tutor-owner opcode proof ({companionReason}) before mapping. {OfficialRemoteOwnerEvidence}";
                            return false;
                        }

                        if (!trustV95LocalOwnerTable
                            && TryResolveConflictingBuildScopedTutorOwnerOpcodeNoLock(
                                inferenceBuildTag,
                                opcode,
                                packetType,
                                out ushort conflictingOpcode,
                                out int conflictingProof))
                        {
                            string buildConflictReason =
                                $"build {inferenceBuildTag} already maps {RemoteUserPacketInboxManager.DescribePacketType(packetType)} to opcode {conflictingOpcode} with official-session proof {conflictingProof}/{MinOfficialSessionTutorInferenceProofCount}";
                            _pendingTutorInferenceMap.Remove(opcode);
                            _tutorInferenceConflictMap[opcode] = buildConflictReason;
                            LastStatus = $"Ignored CUserPool local-user opcode {opcode}: tutor payload inference conflict ({buildConflictReason}). Remove or manually remap this opcode to continue.";
                            return false;
                        }

                        _pendingTutorInferenceMap.Remove(opcode);
                        _packetMap[opcode] = packetType;
                        string learnedEvidence = $"auto:{inferenceReason}; inferenceDistinctWrapperProof={inferenceBuildProof}/{MinOfficialSessionTutorInferenceProofCount}@{inferenceBuildTag}; {OfficialRemoteOwnerEvidence}";
                        RememberLearnedOpcodeNoLock(opcode, packetType, learnedEvidence, isManual: false, source, inferencePayload);
                        EnsureLearnedTutorOpcodeBuildProofNoLock(opcode, packetType, inferenceBuildTag, inferenceBuildProof);
                        if (!trustV95LocalOwnerTable)
                        {
                            string promoted = PromoteBuildScopedTutorOwnerInferenceMappingsNoLock(inferenceBuildTag, source);
                            if (!string.IsNullOrWhiteSpace(promoted))
                            {
                                LastStatus = $"Auto-mapped remote-user opcode {opcode} to {RemoteUserPacketInboxManager.DescribePacketType(packetType)} from tutor payload inference ({inferenceReason}) after official-session distinct wrapper proof {inferenceBuildProof}/{MinOfficialSessionTutorInferenceProofCount} for build {inferenceBuildTag}; promoted pending build-scoped tutor owner mappings: {promoted}; {OfficialRemoteOwnerEvidence}";
                            }
                            else
                            {
                                LastStatus = $"Auto-mapped remote-user opcode {opcode} to {RemoteUserPacketInboxManager.DescribePacketType(packetType)} from tutor payload inference ({inferenceReason}) after official-session distinct wrapper proof {inferenceBuildProof}/{MinOfficialSessionTutorInferenceProofCount} for build {inferenceBuildTag}; {OfficialRemoteOwnerEvidence}";
                            }
                        }
                        else
                        {
                            LastStatus = $"Auto-mapped remote-user opcode {opcode} to {RemoteUserPacketInboxManager.DescribePacketType(packetType)} from tutor payload inference ({inferenceReason}) after official-session distinct wrapper proof {inferenceBuildProof}/{MinOfficialSessionTutorInferenceProofCount} for build {inferenceBuildTag}; {OfficialRemoteOwnerEvidence}";
                        }
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
                && !IsOfficialTutorLocalOpcodeCoveredByV95OwnerTable(opcode))
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

        private bool TryResolveConflictingBuildScopedTutorOwnerOpcodeNoLock(
            string buildTag,
            ushort pendingOpcode,
            int packetType,
            out ushort conflictingOpcode,
            out int conflictingProof)
        {
            conflictingOpcode = 0;
            conflictingProof = 0;
            if (string.IsNullOrWhiteSpace(buildTag)
                || !_learnedTutorPacketMapByBuild.TryGetValue(buildTag, out Dictionary<ushort, LearnedOpcodeEntry> learnedMap))
            {
                return false;
            }

            foreach ((ushort learnedOpcode, LearnedOpcodeEntry learnedEntry) in learnedMap)
            {
                if (learnedOpcode == pendingOpcode
                    || learnedEntry.PacketType != packetType)
                {
                    continue;
                }

                int proof = learnedEntry.ResolveOfficialSessionBuildProofCount(buildTag);
                if (proof < MinOfficialSessionTutorInferenceProofCount)
                {
                    continue;
                }

                conflictingOpcode = learnedOpcode;
                conflictingProof = proof;
                return true;
            }

            return false;
        }

        private static bool IsBuildTagKnownV95(string buildTag)
        {
            string normalizedBuildTag = NormalizeOfficialSessionBuildTag(buildTag);
            if (string.Equals(normalizedBuildTag, "v95", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedBuildTag, "build95", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return IsV95BuildAlias(normalizedBuildTag);
        }

        private static bool IsV95BuildAlias(string normalizedBuildTag)
        {
            if (string.IsNullOrWhiteSpace(normalizedBuildTag)
                || string.Equals(normalizedBuildTag, "unknown", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (normalizedBuildTag.StartsWith("v95", StringComparison.OrdinalIgnoreCase))
            {
                return HasVersionDelimiterSuffix(normalizedBuildTag, prefixLength: 3);
            }

            if (normalizedBuildTag.StartsWith("build95", StringComparison.OrdinalIgnoreCase))
            {
                return HasVersionDelimiterSuffix(normalizedBuildTag, prefixLength: 7);
            }

            return false;
        }

        private static bool HasVersionDelimiterSuffix(string normalizedBuildTag, int prefixLength)
        {
            if (string.IsNullOrWhiteSpace(normalizedBuildTag) || prefixLength < 0)
            {
                return false;
            }

            if (normalizedBuildTag.Length == prefixLength)
            {
                return true;
            }

            if (normalizedBuildTag.Length < prefixLength)
            {
                return false;
            }

            char delimiter = normalizedBuildTag[prefixLength];
            return delimiter == '.'
                || delimiter == '_'
                || delimiter == '-';
        }

        private bool TryObserveTutorInferenceNoLock(
            ushort opcode,
            int packetType,
            string reason,
            string source,
            byte[] payload,
            out PendingTutorInferenceEvidence evidence,
            out bool inferenceConfirmed,
            out string conflictReason)
        {
            evidence = null;
            inferenceConfirmed = false;
            conflictReason = null;
            string payloadSignature = BuildTutorInferencePayloadSignature(packetType, payload);

            if (_pendingTutorInferenceMap.TryGetValue(opcode, out PendingTutorInferenceEvidence existing))
            {
                if (existing.PacketType != packetType)
                {
                    string existingPacketTypeDescription = RemoteUserPacketInboxManager.DescribePacketType(existing.PacketType);
                    string incomingPacketTypeDescription = RemoteUserPacketInboxManager.DescribePacketType(packetType);
                    conflictReason =
                        $"existing={existingPacketTypeDescription} signature={existing.LastPayloadSignature}; incoming={incomingPacketTypeDescription} signature={payloadSignature}; source={source}";
                    _pendingTutorInferenceMap.Remove(opcode);
                    _tutorInferenceConflictMap[opcode] = conflictReason;
                    return false;
                }
                
                existing.Update(packetType, reason, source, payloadSignature);
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

        private bool HasBuildScopedCompanionTutorOwnerProofNoLock(
            ushort opcode,
            int packetType,
            string buildTag,
            out string reason)
        {
            reason = "missing opposite tutor packet type proof";
            int oppositePacketType = packetType == (int)Pools.RemoteUserPacketType.UserTutorHire
                ? (int)Pools.RemoteUserPacketType.UserTutorMessage
                : packetType == (int)Pools.RemoteUserPacketType.UserTutorMessage
                    ? (int)Pools.RemoteUserPacketType.UserTutorHire
                    : 0;
            if (oppositePacketType == 0)
            {
                reason = $"unsupported tutor packet type {packetType}";
                return false;
            }

            if (_learnedTutorPacketMapByBuild.TryGetValue(buildTag, out Dictionary<ushort, LearnedOpcodeEntry> learnedMap))
            {
                foreach ((ushort learnedOpcode, LearnedOpcodeEntry learnedEntry) in learnedMap)
                {
                    if (learnedOpcode == opcode
                        || learnedEntry.PacketType != oppositePacketType)
                    {
                        continue;
                    }

                    int proof = learnedEntry.ResolveOfficialSessionBuildProofCount(buildTag);
                    if (proof >= MinOfficialSessionTutorInferenceProofCount)
                    {
                        reason = $"learned opcode {learnedOpcode}->{RemoteUserPacketInboxManager.DescribePacketType(oppositePacketType)} with build proof {proof}/{MinOfficialSessionTutorInferenceProofCount}";
                        return true;
                    }
                }
            }

            foreach ((ushort pendingOpcode, PendingTutorInferenceEvidence pendingEvidence) in _pendingTutorInferenceMap)
            {
                if (pendingOpcode == opcode
                    || pendingEvidence.PacketType != oppositePacketType)
                {
                    continue;
                }

                int buildProof = pendingEvidence.ResolveOfficialSessionBuildUniqueObservationCount(buildTag);
                if (buildProof >= MinOfficialSessionTutorInferenceProofCount)
                {
                    reason = $"pending opcode {pendingOpcode}->{RemoteUserPacketInboxManager.DescribePacketType(oppositePacketType)} with distinct wrapper proof {buildProof}/{MinOfficialSessionTutorInferenceProofCount}";
                    return true;
                }
            }

            return false;
        }

        private string PromoteBuildScopedTutorOwnerInferenceMappingsNoLock(string buildTag, string source)
        {
            if (string.IsNullOrWhiteSpace(buildTag)
                || _pendingTutorInferenceMap.Count == 0)
            {
                return string.Empty;
            }

            List<string> promotedMappings = null;
            KeyValuePair<ushort, PendingTutorInferenceEvidence>[] pendingEntries = _pendingTutorInferenceMap.ToArray();
            for (int i = 0; i < pendingEntries.Length; i++)
            {
                ushort pendingOpcode = pendingEntries[i].Key;
                PendingTutorInferenceEvidence pendingEvidence = pendingEntries[i].Value;
                int buildProof = pendingEvidence.ResolveOfficialSessionBuildUniqueObservationCount(buildTag);
                if (buildProof < MinOfficialSessionTutorInferenceProofCount)
                {
                    continue;
                }

                int pendingPacketType = pendingEvidence.PacketType;
                if (pendingPacketType != (int)Pools.RemoteUserPacketType.UserTutorHire
                    && pendingPacketType != (int)Pools.RemoteUserPacketType.UserTutorMessage)
                {
                    continue;
                }

                if (!HasBuildScopedCompanionTutorOwnerProofNoLock(
                        pendingOpcode,
                        pendingPacketType,
                        buildTag,
                        out _))
                {
                    continue;
                }

                _pendingTutorInferenceMap.Remove(pendingOpcode);
                _packetMap[pendingOpcode] = pendingPacketType;
                string evidence = $"auto:{pendingEvidence.Reason}; inferenceDistinctWrapperProof={buildProof}/{MinOfficialSessionTutorInferenceProofCount}@{buildTag}; pairedBuildTutorOwnerTable=1; {OfficialRemoteOwnerEvidence}";
                RememberLearnedOpcodeNoLock(pendingOpcode, pendingPacketType, evidence, isManual: false, source, payload: null);
                EnsureLearnedTutorOpcodeBuildProofNoLock(pendingOpcode, pendingPacketType, buildTag, buildProof);
                (promotedMappings ??= new List<string>()).Add($"{pendingOpcode}->{RemoteUserPacketInboxManager.DescribePacketType(pendingPacketType)}");
            }

            return promotedMappings == null || promotedMappings.Count == 0
                ? string.Empty
                : string.Join(", ", promotedMappings);
        }

        private static string BuildTutorInferencePayloadSignature(int packetType, byte[] payload)
        {
            if (packetType == (int)Pools.RemoteUserPacketType.UserTutorHire)
            {
                bool decoded = HaCreator.MapSimulator.MapSimulator.TryDecodeRemotePacketOwnedTutorHirePayload(
                    payload,
                    out _,
                    out bool enabled,
                    out _);
                if (!decoded)
                {
                    return $"invalid-hire:{payload?.Length ?? 0}";
                }

                // Evidence should reflect wrapper shape, not owner id churn from traffic.
                return $"hire:enabled={(enabled ? 1 : 0)}";
            }

            if (packetType == (int)Pools.RemoteUserPacketType.UserTutorMessage)
            {
                bool decoded = HaCreator.MapSimulator.MapSimulator.TryDecodeRemotePacketOwnedTutorMessagePayload(
                    payload,
                    out _,
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
                    return $"msg-indexed:{messageIndex}:{durationMs}";
                }

                int textHash = string.IsNullOrEmpty(text)
                    ? 0
                    : StringComparer.Ordinal.GetHashCode(text);
                return $"msg-text:{width}:{durationMs}:{text?.Length ?? 0}:{textHash}";
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
            if (IsBuildTagKnownV95(buildTag)
                && IsOfficialTutorLocalOpcodeCoveredByV95OwnerTable(opcode))
            {
                // The v95 tutor opcodes are recovered CUserLocal owner-table entries, not
                // build-inferred remote wrappers, so do not churn them through unknown-build
                // proof suspension on later packets from the same recovered build.
                return false;
            }

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
                RememberLearnedTutorOpcodeByBuildNoLock(opcode, packetType, evidence, isManual, source, payload);
                return;
            }

            _learnedPacketMap[opcode] = new LearnedOpcodeEntry(packetType, evidence, isManual, source, payload);
            RememberLearnedTutorOpcodeByBuildNoLock(opcode, packetType, evidence, isManual, source, payload);
        }

        private void RememberMappedPortableChairRecordEvidenceNoLock(
            ushort opcode,
            int packetType,
            byte[] payload,
            string source)
        {
            if (packetType != (int)Pools.RemoteUserPacketType.UserCoupleChairRecordAdd)
            {
                return;
            }

            if (!TryResolveExtendedPortableChairRecordAddFromCaptureNoLock(
                    opcode,
                    payload,
                    source,
                    out int resolvedPacketType,
                    out string reason,
                    out int characterId)
                || resolvedPacketType != packetType)
            {
                return;
            }

            _portableChairRecordInferenceMap[opcode] = reason;
            RememberPortableChairRecordAddOwnerNoLock(source, characterId, opcode);
        }

        private void RememberLearnedTutorOpcodeByBuildNoLock(
            ushort opcode,
            int packetType,
            string evidence,
            bool isManual,
            string source,
            byte[] payload)
        {
            if (isManual
                || (packetType != (int)Pools.RemoteUserPacketType.UserTutorHire
                    && packetType != (int)Pools.RemoteUserPacketType.UserTutorMessage)
                || !TryResolveOfficialSessionBuildTag(source, out string buildTag))
            {
                return;
            }

            if (!_learnedTutorPacketMapByBuild.TryGetValue(buildTag, out Dictionary<ushort, LearnedOpcodeEntry> learnedMap))
            {
                learnedMap = new Dictionary<ushort, LearnedOpcodeEntry>();
                _learnedTutorPacketMapByBuild[buildTag] = learnedMap;
            }

            if (learnedMap.TryGetValue(opcode, out LearnedOpcodeEntry existing))
            {
                existing.Update(packetType, evidence, source, payload);
                return;
            }

            learnedMap[opcode] = new LearnedOpcodeEntry(packetType, evidence, isManual: false, source, payload);
        }

        private static bool TryResolveExtendedPortableChairRecordAddFromCaptureNoLock(
            ushort opcode,
            byte[] payload,
            string source,
            out int packetType,
            out string reason,
            out int characterId)
        {
            packetType = 0;
            reason = string.Empty;
            characterId = 0;
            if (!IsOfficialSessionSource(source)
                || payload == null
                || payload.Length != sizeof(int) * 4)
            {
                return false;
            }

            if (!Pools.RemoteUserPacketCodec.TryParsePortableChairRecordAdd(
                    payload,
                    out Pools.RemoteUserPortableChairRecordAddPacket packet,
                    out _))
            {
                return false;
            }

            if (packet.ChairItemId / 10000 != 301
                || packet.PairCharacterId.GetValueOrDefault() <= 0
                || packet.Status.GetValueOrDefault(-1) < 0)
            {
                return false;
            }

            packetType = (int)Pools.RemoteUserPacketType.UserCoupleChairRecordAdd;
            characterId = packet.CharacterId;
            reason = $"opcode {opcode} extended chair-record add for character {packet.CharacterId}, chair {packet.ChairItemId}, pair {packet.PairCharacterId.Value}, status {packet.Status.Value}";
            return true;
        }

        private bool TryResolvePortableChairRecordRemoveFromPriorAddCaptureNoLock(
            ushort opcode,
            byte[] payload,
            string source,
            out int packetType,
            out string reason)
        {
            packetType = 0;
            reason = string.Empty;
            if (!IsOfficialSessionSource(source)
                || payload == null
                || payload.Length != sizeof(int)
                || !Pools.RemoteUserPacketCodec.TryParsePortableChairRecordRemove(
                    payload,
                    out Pools.RemoteUserPortableChairRecordRemovePacket packet,
                    out _)
                || packet.CharacterId <= 0)
            {
                return false;
            }

            string buildTag = ResolveOfficialSessionBuildTag(source);
            if (!_portableChairRecordAddOpcodeByCharacterByBuild.TryGetValue(buildTag, out Dictionary<int, ushort> addOpcodeByCharacter)
                || !addOpcodeByCharacter.TryGetValue(packet.CharacterId, out ushort addOpcode)
                || addOpcode == opcode)
            {
                return false;
            }

            packetType = (int)Pools.RemoteUserPacketType.UserCoupleChairRecordRemove;
            reason = $"opcode {opcode} compact chair-record remove for character {packet.CharacterId}, paired with prior build {buildTag} add opcode {addOpcode}";
            return true;
        }

        private void RememberPortableChairRecordAddOwnerNoLock(string source, int characterId, ushort opcode)
        {
            if (characterId <= 0)
            {
                return;
            }

            string buildTag = ResolveOfficialSessionBuildTag(source);
            if (!_portableChairRecordAddOpcodeByCharacterByBuild.TryGetValue(buildTag, out Dictionary<int, ushort> addOpcodeByCharacter))
            {
                addOpcodeByCharacter = new Dictionary<int, ushort>();
                _portableChairRecordAddOpcodeByCharacterByBuild[buildTag] = addOpcodeByCharacter;
            }

            addOpcodeByCharacter[characterId] = opcode;
        }

        private void EnsureLearnedTutorOpcodeBuildProofNoLock(ushort opcode, int packetType, string buildTag, int proofCount)
        {
            if (packetType != (int)Pools.RemoteUserPacketType.UserTutorHire
                && packetType != (int)Pools.RemoteUserPacketType.UserTutorMessage)
            {
                return;
            }

            if (_learnedPacketMap.TryGetValue(opcode, out LearnedOpcodeEntry learnedEntry))
            {
                learnedEntry.EnsureOfficialSessionBuildProofCount(buildTag, proofCount);
            }

            string normalizedBuildTag = NormalizeOfficialSessionBuildTag(buildTag);
            if (_learnedTutorPacketMapByBuild.TryGetValue(normalizedBuildTag, out Dictionary<ushort, LearnedOpcodeEntry> learnedMap)
                && learnedMap.TryGetValue(opcode, out LearnedOpcodeEntry buildScopedEntry))
            {
                buildScopedEntry.EnsureOfficialSessionBuildProofCount(normalizedBuildTag, proofCount);
            }
        }

        private bool TryResolveBuildScopedLearnedTutorPacketTypeNoLock(
            ushort opcode,
            string source,
            byte[] payload,
            out int packetType,
            out string reason)
        {
            packetType = 0;
            reason = string.Empty;
            if (!TryResolveOfficialSessionBuildTag(source, out string buildTag)
                || !_learnedTutorPacketMapByBuild.TryGetValue(buildTag, out Dictionary<ushort, LearnedOpcodeEntry> learnedMap)
                || !learnedMap.TryGetValue(opcode, out LearnedOpcodeEntry learnedEntry))
            {
                return false;
            }

            if (learnedEntry.PacketType != (int)Pools.RemoteUserPacketType.UserTutorHire
                && learnedEntry.PacketType != (int)Pools.RemoteUserPacketType.UserTutorMessage)
            {
                return false;
            }

            if (!TryValidateKnownTutorOwnerPayloadNoLock(opcode, payload, learnedEntry.PacketType, out string payloadReason))
            {
                reason = $"build {buildTag} cached tutor owner payload mismatch ({payloadReason})";
                return false;
            }

            packetType = learnedEntry.PacketType;
            reason = $"build {buildTag} learned proof ({payloadReason}; cachedProof={learnedEntry.ResolveOfficialSessionBuildProofCount(buildTag)})";
            return true;
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

        private void StopInternal(bool clearPending)
        {
            _roleSessionProxy.Stop(resetCounters: clearPending);

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
                _tutorInferenceConflictMap.Remove(opcode);
                if (!DefaultPacketMap.ContainsKey(opcode))
                {
                    _packetMap.Remove(opcode);
                }
            }

            Dictionary<string, Dictionary<ushort, LearnedOpcodeEntry>> manualBuildScopedTutorMappings = _learnedTutorPacketMapByBuild
                .Where(buildEntry => buildEntry.Value.Any(mapEntry => mapEntry.Value.IsManual))
                .ToDictionary(
                    buildEntry => buildEntry.Key,
                    buildEntry => buildEntry.Value
                        .Where(mapEntry => mapEntry.Value.IsManual)
                        .ToDictionary(mapEntry => mapEntry.Key, mapEntry => mapEntry.Value),
                    StringComparer.OrdinalIgnoreCase);

            _learnedTutorPacketMapByBuild.Clear();
            foreach ((string buildTag, Dictionary<ushort, LearnedOpcodeEntry> mappings) in manualBuildScopedTutorMappings)
            {
                _learnedTutorPacketMapByBuild[buildTag] = mappings;
            }
            _pendingTutorInferenceMap.Clear();
            _tutorInferenceConflictMap.Clear();
        }

        private void RemoveLearnedTutorMappingByBuildNoLock(ushort opcode)
        {
            if (_learnedTutorPacketMapByBuild.Count == 0)
            {
                return;
            }

            string[] buildTags = _learnedTutorPacketMapByBuild.Keys.ToArray();
            for (int i = 0; i < buildTags.Length; i++)
            {
                string buildTag = buildTags[i];
                if (!_learnedTutorPacketMapByBuild.TryGetValue(buildTag, out Dictionary<ushort, LearnedOpcodeEntry> learnedMap))
                {
                    continue;
                }

                learnedMap.Remove(opcode);
                if (learnedMap.Count == 0)
                {
                    _learnedTutorPacketMapByBuild.Remove(buildTag);
                }
            }
        }

        private void OnRoleSessionServerPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (e.IsInit)
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            string evidenceSource = BuildOfficialSessionEvidenceSource(e);
            if (!TryDecodeInboundRemoteUserPacket(e.RawPacket, evidenceSource, out RemoteUserOfficialSessionBridgeMessage message))
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            ReceivedCount++;
            message = new RemoteUserOfficialSessionBridgeMessage(
                message.PacketType,
                message.Payload,
                message.Source,
                message.Opcode,
                ReceivedCount);
            _pendingMessages.Enqueue(message);
            LastStatus = $"Queued {RemoteUserPacketInboxManager.DescribePacketType(message.PacketType)} opcode {message.Opcode}{FormatOfficialSessionSequence(message.OfficialSessionSequence)} ({message.Payload.Length} byte(s)) from live session {e.SourceEndpoint} ({ResolveOfficialSessionBuildTag(evidenceSource)} evidence).";
        }

        private void OnRoleSessionClientPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            if (e == null || e.IsInit)
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            ForwardedOutboundCount++;
            LastStatus = _roleSessionProxy.LastStatus;
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

        internal static string BuildOfficialSessionEvidenceSource(MapleSessionPacketEventArgs e)
        {
            if (e == null)
            {
                return $"{OfficialSessionSourcePrefix}unknown:unknown-endpoint";
            }

            string buildTag = e.SessionVersion.HasValue && e.SessionVersion.Value > 0
                ? $"v{e.SessionVersion.Value}"
                : "unknown";
            string endpoint = string.IsNullOrWhiteSpace(e.SourceEndpoint)
                ? "unknown-endpoint"
                : e.SourceEndpoint;
            return $"{OfficialSessionSourcePrefix}{buildTag}:{endpoint}";
        }

        private static string FormatOfficialSessionSequence(int sequence)
        {
            return sequence > 0
                ? $" seq {sequence}"
                : string.Empty;
        }

        private string DescribeTutorInferenceConflictsNoLock()
        {
            if (_tutorInferenceConflictMap.Count == 0)
            {
                return "none";
            }

            return string.Join(
                ", ",
                _tutorInferenceConflictMap
                    .OrderBy(entry => entry.Key)
                    .Select(entry => $"{entry.Key} ({entry.Value})"));
        }

        private string DescribeLearnedTutorMappingsByBuildNoLock()
        {
            if (_learnedTutorPacketMapByBuild.Count == 0)
            {
                return "none";
            }

            return string.Join(
                ", ",
                _learnedTutorPacketMapByBuild
                    .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(entry =>
                    {
                        string buildTag = entry.Key;
                        string mappings = string.Join(
                            "|",
                            entry.Value
                                .OrderBy(mapEntry => mapEntry.Key)
                                .Select(mapEntry =>
                                {
                                    LearnedOpcodeEntry learnedEntry = mapEntry.Value;
                                    return $"{mapEntry.Key}->{RemoteUserPacketInboxManager.DescribePacketType(learnedEntry.PacketType)}(count={learnedEntry.Count};proof={learnedEntry.ResolveOfficialSessionBuildProofCount(buildTag)})";
                                }));
                        return $"{buildTag}[{mappings}]";
                    }));
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
