using MapleLib.PacketLib;
using HaCreator.MapSimulator.Interaction;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Proxies a live Maple session and forwards CUserLocal::OnPacket utility
    /// opcodes into the existing local-utility packet seam.
    /// </summary>
    public sealed class LocalUtilityOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18496;
        internal const int FollowCharacterRequestPayloadLength = sizeof(int) + sizeof(byte) + sizeof(byte);
        // CWvsContext::SendFollowCharacterRequest encodes withdraw as opcode 134 with (0, 0, 1).
        internal const int FollowCharacterWithdrawDriverId = 0;
        internal const byte FollowCharacterWithdrawAutoRequest = 0;
        internal const byte FollowCharacterWithdrawKeyInput = 1;
        private const string DefaultProcessName = "MapleStory";
        private const int SentOutboundHistoryCapacity = 64;
        private const int ReceivedInboundHistoryCapacity = 64;
        private sealed record PendingOutboundPacket(int Opcode, byte[] RawPacket, int SentOrdinal = 0);
        private sealed record ReceivedInboundPacket(int PacketType, byte[] Payload, int ReceivedOrdinal = 0);

        private readonly ConcurrentQueue<LocalUtilityPacketInboxMessage> _pendingMessages = new();
        private readonly ConcurrentQueue<PendingOutboundPacket> _pendingOutboundPackets = new();
        private readonly ConcurrentQueue<PendingOutboundPacket> _sentOutboundHistory = new();
        private readonly ConcurrentQueue<ReceivedInboundPacket> _receivedInboundHistory = new();
        private readonly object _sync = new();
        private readonly MapleRoleSessionProxy _roleSessionProxy;

        public int ListenPort { get; private set; } = DefaultListenPort;
        public string RemoteHost { get; private set; } = IPAddress.Loopback.ToString();
        public int RemotePort { get; private set; }
        public bool IsRunning => _roleSessionProxy.IsRunning;
        public bool HasAttachedClient => _roleSessionProxy.HasAttachedClient;
        public bool HasConnectedSession => _roleSessionProxy.HasConnectedSession;
        public string ActiveRemoteEndpoint => HasConnectedSession ? $"{RemoteHost}:{RemotePort}" : string.Empty;
        public int ReceivedCount { get; private set; }
        public int SentCount { get; private set; }
        public int LastSentOpcode { get; private set; } = -1;
        public byte[] LastSentRawPacket { get; private set; } = Array.Empty<byte>();
        public int QueuedCount { get; private set; }
        public int LastQueuedOpcode { get; private set; } = -1;
        public byte[] LastQueuedRawPacket { get; private set; } = Array.Empty<byte>();
        public int ForwardedOutboundCount { get; private set; }
        public int PendingPacketCount => _pendingOutboundPackets.Count;
        public string LastStatus { get; private set; } = "Local utility official-session bridge inactive.";
        public event EventHandler<LocalUtilityOutboundPacketObservedEventArgs> ClientOutboundPacketObserved;

        public LocalUtilityOfficialSessionBridgeManager(Func<MapleRoleSessionProxy> roleSessionProxyFactory = null)
        {
            _roleSessionProxy = (roleSessionProxyFactory ?? (() => MapleRoleSessionProxyFactory.GlobalV95.CreateChannel()))();
            _roleSessionProxy.ServerPacketReceived += OnRoleSessionServerPacketReceived;
            _roleSessionProxy.ClientPacketReceived += OnRoleSessionClientPacketReceived;
        }

        public string DescribeStatus()
        {
            string lifecycle = IsRunning
                ? $"listening on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}"
                : "inactive";
            string session = HasConnectedSession
                ? "connected Maple session"
                : "no active Maple session";
            string lastOutbound = LastSentOpcode >= 0
                ? $" lastOut={LastSentOpcode}[{Convert.ToHexString(LastSentRawPacket)}]."
                : string.Empty;
            string lastQueued = LastQueuedOpcode >= 0
                ? $" lastQueued={LastQueuedOpcode}[{Convert.ToHexString(LastQueuedRawPacket)}]."
                : string.Empty;
            return $"Local utility official-session bridge {lifecycle}; {session}; received={ReceivedCount}; forwardedOutbound={ForwardedOutboundCount}; sent={SentCount}; pending={PendingPacketCount}; queued={QueuedCount}; inbound opcodes=28,58,133,193,234,248,250,253,254,255,256,261,264,269,270,274,275,291,366,367,405,406,407,425,1011,1019,1023,1024,1025,1035,1047; outbound opcodes=45,74,77,113,117,130,131,134,135,191,193,214,1023.{lastOutbound}{lastQueued} {LastStatus}";
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

                    LastStatus = $"Local utility official-session bridge listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}. {proxyStatus}";
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
            if (!_roleSessionProxy.HasConnectedSession)
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

            if (!_roleSessionProxy.TrySendToServer(rawPacket, out string proxyStatus))
            {
                status = proxyStatus;
                LastStatus = status;
                return false;
            }

            RecordSentOutboundPacket(opcode, rawPacket);
            status = $"Injected local utility outbound opcode {opcode} into live session.";
            LastStatus = status;
            return true;
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

        public bool HasSentOutboundPacket(int opcode, IReadOnlyList<byte> rawPacket)
        {
            return HasSentOutboundPacketSince(opcode, rawPacket, minimumSentCountExclusive: 0);
        }

        public bool HasSentOutboundPacketSince(int opcode, IReadOnlyList<byte> rawPacket, int minimumSentCountExclusive)
        {
            if (opcode < ushort.MinValue || opcode > ushort.MaxValue || rawPacket == null)
            {
                return false;
            }

            byte[] target = rawPacket as byte[] ?? rawPacket.ToArray();
            if (LastSentOpcode == opcode
                && SentCount > minimumSentCountExclusive
                && LastSentRawPacket.AsSpan().SequenceEqual(target))
            {
                return true;
            }

            PendingOutboundPacket[] history = _sentOutboundHistory.ToArray();
            for (int i = history.Length - 1; i >= 0; i--)
            {
                PendingOutboundPacket sent = history[i];
                if (sent.Opcode == opcode
                    && sent.SentOrdinal > minimumSentCountExclusive
                    && sent.RawPacket.AsSpan().SequenceEqual(target))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasReceivedInboundPacketPayloadSince(int packetType, IReadOnlyList<byte> payload, int minimumReceivedCountExclusive)
        {
            if (packetType < 0 || payload == null)
            {
                return false;
            }

            byte[] target = payload as byte[] ?? payload.ToArray();
            ReceivedInboundPacket[] history = _receivedInboundHistory.ToArray();
            for (int i = history.Length - 1; i >= 0; i--)
            {
                ReceivedInboundPacket received = history[i];
                if (received.PacketType == packetType
                    && received.ReceivedOrdinal > minimumReceivedCountExclusive
                    && received.Payload.AsSpan().SequenceEqual(target))
                {
                    return true;
                }
            }

            return false;
        }

        internal static byte[] BuildFollowCharacterRequestPayload(int driverId, bool autoRequest, bool keyInput)
        {
            using PacketWriter writer = new();
            writer.WriteInt(driverId);
            writer.WriteByte(autoRequest ? 1 : 0);
            writer.WriteByte(keyInput ? 1 : 0);
            return writer.ToArray();
        }

        internal static byte[] BuildFollowCharacterWithdrawPayload()
        {
            return BuildFollowCharacterRequestPayload(
                FollowCharacterWithdrawDriverId,
                FollowCharacterWithdrawAutoRequest != 0,
                FollowCharacterWithdrawKeyInput != 0);
        }

        public void Dispose()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
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
                int flushed = FlushQueuedOutboundPacketsViaProxy();
                LastStatus = flushed > 0
                    ? $"Local utility official-session bridge initialized Maple crypto and flushed {flushed} queued outbound packet(s)."
                    : _roleSessionProxy.LastStatus;
                return;
            }

            if (!LocalUtilityPacketInboxManager.TryDecodeOpcodeFramedPacket(e.RawPacket, out int packetType, out byte[] payload, out _)
                || !ShouldMirrorInboundPacketType(packetType))
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            _pendingMessages.Enqueue(new LocalUtilityPacketInboxMessage(
                packetType,
                payload,
                $"official-session:{e.SourceEndpoint}",
                $"packetclientraw {Convert.ToHexString(e.RawPacket)}"));
            RecordReceivedInboundPacket(packetType, payload);
            LastStatus = $"Queued {DescribePacketType(packetType)} from live session {e.SourceEndpoint}.";
        }

        private void OnRoleSessionClientPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            if (e != null && !e.IsInit)
            {
                ForwardedOutboundCount++;
                TryPublishObservedClientOutboundPacket(e.RawPacket, $"official-session:{e.SourceEndpoint}");
            }

            LastStatus = _roleSessionProxy.LastStatus;
        }

        private void TryPublishObservedClientOutboundPacket(byte[] rawPacket, string source)
        {
            if (!LocalUtilityPacketInboxManager.TryDecodeOpcodeFramedPacket(rawPacket, out int opcode, out byte[] payload, out _))
            {
                return;
            }

            byte[] safeRawPacket = rawPacket?.ToArray() ?? Array.Empty<byte>();
            byte[] safePayload = payload?.ToArray() ?? Array.Empty<byte>();
            ClientOutboundPacketObserved?.Invoke(
                this,
                new LocalUtilityOutboundPacketObservedEventArgs(
                    opcode,
                    safePayload,
                    safeRawPacket,
                    string.IsNullOrWhiteSpace(source) ? "official-session:outbound" : source.Trim(),
                    ForwardedOutboundCount));
        }

        private void StopInternal(bool clearPending)
        {
            _roleSessionProxy.Stop(resetCounters: clearPending);

            if (clearPending)
            {
                ResetInboundState();
                while (_pendingOutboundPackets.TryDequeue(out _))
                {
                }
                while (_sentOutboundHistory.TryDequeue(out _))
                {
                }

                SentCount = 0;
                ForwardedOutboundCount = 0;
                LastSentOpcode = -1;
                LastSentRawPacket = Array.Empty<byte>();
                QueuedCount = 0;
                LastQueuedOpcode = -1;
                LastQueuedRawPacket = Array.Empty<byte>();
            }
        }

        private int FlushQueuedOutboundPacketsViaProxy()
        {
            if (!_roleSessionProxy.HasConnectedSession)
            {
                return 0;
            }

            int flushed = 0;
            while (_pendingOutboundPackets.TryPeek(out PendingOutboundPacket packet))
            {
                if (!_roleSessionProxy.TrySendToServer(packet.RawPacket, out _))
                {
                    break;
                }

                if (!_pendingOutboundPackets.TryDequeue(out PendingOutboundPacket dequeuedPacket))
                {
                    break;
                }

                RecordSentOutboundPacket(dequeuedPacket.Opcode, dequeuedPacket.RawPacket);
                flushed++;
            }

            return flushed;
        }

        private void ResetInboundState()
        {
            while (_pendingMessages.TryDequeue(out _))
            {
            }
            while (_receivedInboundHistory.TryDequeue(out _))
            {
            }

            ReceivedCount = 0;
        }

        private void RecordReceivedInboundPacket(int packetType, byte[] payload)
        {
            ReceivedCount++;
            _receivedInboundHistory.Enqueue(new ReceivedInboundPacket(
                packetType,
                payload ?? Array.Empty<byte>(),
                ReceivedCount));
            while (_receivedInboundHistory.Count > ReceivedInboundHistoryCapacity
                   && _receivedInboundHistory.TryDequeue(out _))
            {
            }
        }

        private void RecordSentOutboundPacket(int opcode, byte[] rawPacket)
        {
            SentCount++;
            LastSentOpcode = opcode;
            LastSentRawPacket = rawPacket ?? Array.Empty<byte>();
            _sentOutboundHistory.Enqueue(new PendingOutboundPacket(opcode, LastSentRawPacket, SentCount));
            while (_sentOutboundHistory.Count > SentOutboundHistoryCapacity
                   && _sentOutboundHistory.TryDequeue(out _))
            {
            }
        }

        private static byte[] BuildRawPacket(ushort opcode, IReadOnlyList<byte> payload)
        {
            using PacketWriter writer = new();
            writer.Write(opcode);
            if (payload is byte[] bytes)
            {
                writer.WriteBytes(bytes);
            }
            else if (payload != null)
            {
                for (int i = 0; i < payload.Count; i++)
                {
                    writer.WriteByte(payload[i]);
                }
            }

            return writer.ToArray();
        }

        internal static bool ShouldMirrorInboundPacketType(int packetType)
        {
            return packetType == LocalUtilityPacketInboxManager.InventoryOperationClientPacketType
                || packetType == LocalUtilityPacketInboxManager.FollowCharacterClientPacketType
                || packetType == LocalUtilityPacketInboxManager.SetGenderPacketType
                || packetType == LocalUtilityPacketInboxManager.AccountMoreInfoPacketType
                || packetType == LocalUtilityPacketInboxManager.TeleportClientPacketType
                || packetType == LocalUtilityPacketInboxManager.SetDirectionModeClientPacketType
                || packetType == LocalUtilityPacketInboxManager.SetStandAloneModeClientPacketType
                || packetType == LocalUtilityPacketInboxManager.HireTutorClientPacketType
                || packetType == LocalUtilityPacketInboxManager.TutorMsgClientPacketType
                || packetType == LocalUtilityPacketInboxManager.RadioScheduleClientPacketType
                || packetType == LocalUtilityPacketInboxManager.ChatMsgClientPacketType
                || packetType == LocalUtilityPacketInboxManager.RadioCreateLayerContextPacketType
                || packetType == LocalUtilityPacketInboxManager.NotifyHpDecByFieldPacketType
                || packetType == LocalUtilityPacketInboxManager.DamageMeterPacketType
                || packetType == LocalUtilityPacketInboxManager.PassiveMoveClientPacketType
                || packetType == LocalUtilityPacketInboxManager.FollowCharacterFailedClientPacketType
                || packetType == LocalUtilityPacketInboxManager.MakerResultClientPacketType
                || packetType == LocalUtilityPacketInboxManager.ItemMakerHiddenRecipeUnlockPacketType
                || packetType == LocalUtilityPacketInboxManager.ItemMakerSessionPacketType
                || packetType == LocalUtilityPacketInboxManager.OpenClassCompetitionPagePacketType
                || packetType == LocalUtilityPacketInboxManager.QuestGuideResultPacketType
                || packetType == LocalUtilityPacketInboxManager.DeliveryQuestPacketType
                || packetType == LocalUtilityPacketInboxManager.ClassCompetitionAuthCachePacketType
                || packetType == LocalUtilityPacketInboxManager.AdminShopResultClientPacketType
                || packetType == LocalUtilityPacketInboxManager.AdminShopOpenClientPacketType
                || packetType == LocalUtilityPacketInboxManager.ItemUpgradeResultClientPacketType
                || packetType == MapleTvRuntime.PacketTypeSetMessage
                || packetType == MapleTvRuntime.PacketTypeClearMessage
                || packetType == MapleTvRuntime.PacketTypeSendMessageResult
                || packetType == LocalUtilityPacketInboxManager.AntiMacroResultPacketType
                || packetType == LocalUtilityPacketInboxManager.PetConsumeResultPacketType
                || packetType == LocalUtilityPacketInboxManager.RepairDurabilityResultPacketType
                || packetType == LocalUtilityPacketInboxManager.MonsterBookRegistrationResultPacketType
                || packetType == LocalUtilityPacketInboxManager.MonsterBookOwnershipSyncPacketType
                || packetType == LocalUtilityPacketInboxManager.MechanicEquipStatePacketType
                || packetType == LocalUtilityPacketInboxManager.CharacterEquipStatePacketType;
        }

        internal static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
                LocalUtilityPacketInboxManager.InventoryOperationClientPacketType => "OnInventoryOperation(28)",
                LocalUtilityPacketInboxManager.SetGenderPacketType => "SetGender(58)",
                LocalUtilityPacketInboxManager.AccountMoreInfoPacketType => "AccountMoreInfo(133)",
                LocalUtilityPacketInboxManager.TeleportClientPacketType => "OnTeleport(234)",
                LocalUtilityPacketInboxManager.FollowCharacterClientPacketType => "FollowCharacter(193)",
                LocalUtilityPacketInboxManager.SetDirectionModeClientPacketType => "SetDirectionMode(253)",
                LocalUtilityPacketInboxManager.SetStandAloneModeClientPacketType => "SetStandAloneMode(254)",
                LocalUtilityPacketInboxManager.HireTutorClientPacketType => "HireTutor(255)",
                LocalUtilityPacketInboxManager.TutorMsgClientPacketType => "TutorMsg(256)",
                LocalUtilityPacketInboxManager.RadioScheduleClientPacketType => "RadioSchedule(261)",
                LocalUtilityPacketInboxManager.ChatMsgClientPacketType => "OnChatMsg(264)",
                LocalUtilityPacketInboxManager.RadioCreateLayerContextPacketType => "RadioCreateLayerContext(1035)",
                LocalUtilityPacketInboxManager.NotifyHpDecByFieldPacketType => "NotifyHPDecByField(243)",
                LocalUtilityPacketInboxManager.DamageMeterPacketType => "DamageMeter(267)",
                LocalUtilityPacketInboxManager.PassiveMoveClientPacketType => "PassiveMove(269)",
                LocalUtilityPacketInboxManager.FollowCharacterFailedClientPacketType => "FollowCharacterFailed(270)",
                LocalUtilityPacketInboxManager.MakerResultClientPacketType => "OnMakerResult(248)",
                LocalUtilityPacketInboxManager.ItemMakerHiddenRecipeUnlockPacketType => "ItemMakerHiddenRecipeUnlock(1019)",
                LocalUtilityPacketInboxManager.ItemMakerSessionPacketType => "ItemMakerSession(1024)",
                LocalUtilityPacketInboxManager.OpenClassCompetitionPagePacketType => "OpenClassCompetitionPage(250)",
                LocalUtilityPacketInboxManager.QuestGuideResultPacketType => "QuestGuideResult(274)",
                LocalUtilityPacketInboxManager.DeliveryQuestPacketType => "DeliveryQuest(275)",
                LocalUtilityPacketInboxManager.ClassCompetitionAuthCachePacketType => "ClassCompetitionAuthCache(291)",
                LocalUtilityPacketInboxManager.AdminShopResultClientPacketType => "CAdminShopDlg::OnPacket Result(366)",
                LocalUtilityPacketInboxManager.AdminShopOpenClientPacketType => "CAdminShopDlg::OnPacket Open(367)",
                LocalUtilityPacketInboxManager.ItemUpgradeResultClientPacketType => "OnItemUpgradeResult(425)",
                MapleTvRuntime.PacketTypeSetMessage => "CMapleTVMan::OnSetMessage(405)",
                MapleTvRuntime.PacketTypeClearMessage => "CMapleTVMan::OnClearMessage(406)",
                MapleTvRuntime.PacketTypeSendMessageResult => "CMapleTVMan::OnSendMessageResult(407)",
                LocalUtilityPacketInboxManager.AntiMacroResultPacketType => "AntiMacroResult(1011)",
                LocalUtilityPacketInboxManager.PetConsumeResultPacketType => "PetConsumeResult(1026) / RaiseOwnerSync(1026)",
                LocalUtilityPacketInboxManager.RepairDurabilityResultPacketType => "RepairDurabilityResult(1025)",
                LocalUtilityPacketInboxManager.MonsterBookRegistrationResultPacketType => "MonsterBookRegistrationResult(1047)",
                LocalUtilityPacketInboxManager.MonsterBookOwnershipSyncPacketType => "MonsterBookOwnershipSync(1048)",
                LocalUtilityPacketInboxManager.MechanicEquipStatePacketType => "MechanicEquipState(1023)",
                LocalUtilityPacketInboxManager.CharacterEquipStatePacketType => "CharacterEquipState(1034)",
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
