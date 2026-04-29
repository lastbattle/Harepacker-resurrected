using MapleLib.PacketLib;
using HaCreator.MapSimulator.Interaction;
using System;
using System.Buffers.Binary;
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
    /// Proxies a live Maple session and peels personal-shop / entrusted-shop
    /// receive opcodes into the existing merchant dialog packet seam.
    /// </summary>
    public sealed class SocialRoomMerchantOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18490;
        public const ushort DefaultInboundMiniRoomOpcode = 373;
        public const ushort OutboundMiniRoomOpcode = 144;
        public const byte RequestSubtypeCloseRoom = 10;
        public const byte RequestSubtypeOpenRoom = 11;
        public const byte RequestSubtypePutItem = 15;
        public const byte RequestSubtypePersonalShopBuy = 23;
        public const byte RequestSubtypeEntrustedShopBuy = 34;
        public const byte RequestSubtypeEntrustedShopGoOut = 39;
        public const byte RequestSubtypeEntrustedShopArrange = 40;
        public const byte RequestSubtypeEntrustedShopWithdrawAll = 41;
        public const byte RequestSubtypeEntrustedShopWithdrawMoney = 43;
        public const byte RequestSubtypeEntrustedShopVisitList = 46;
        public const byte RequestSubtypeEntrustedShopBlacklist = 47;
        public const byte EntrustedShopBlacklistRequestModeAdd = 0;
        public const byte EntrustedShopBlacklistRequestModeDelete = 1;
        private const int MaxRecentOutboundPackets = 32;
        private const int MaxPendingResultExpectations = 32;

        private readonly ConcurrentQueue<SocialRoomMerchantPacketInboxMessage> _pendingMessages = new();
        private readonly object _sync = new();
        private readonly Queue<OutboundPacketTrace> _recentOutboundPackets = new();
        private readonly List<PendingResultExpectation> _pendingResultExpectations = new();
        private readonly MapleRoleSessionProxy _roleSessionProxy;

        private readonly record struct PendingResultExpectation(
            int RequestOpcode,
            byte RequestSubtype,
            string Source,
            string PayloadHex,
            int[] ExpectedInboundOpcodes,
            byte[] ExpectedInboundSubtypes,
            string ExpectationSummary);

        public readonly record struct OutboundPacketTrace(
            int Opcode,
            byte PacketType,
            int PayloadLength,
            string PayloadHex,
            string RawPacketHex,
            string Source,
            string Summary);
        private int _expectedResultRequestCount;
        private int _expectedResultMatchCount;
        private int _expectedResultMismatchCount;
        private int _expectedResultUnexpectedCount;
        private int _expectedResultEvictedCount;
        public int ListenPort { get; private set; } = DefaultListenPort;
        public string RemoteHost { get; private set; } = IPAddress.Loopback.ToString();
        public int RemotePort { get; private set; }
        public ushort InboundOpcode { get; private set; }
        public ushort AutoDetectedInboundOpcode { get; private set; }
        public SocialRoomKind? PreferredKind { get; private set; }
        public bool IsRunning => _roleSessionProxy.IsRunning;
        public bool HasConnectedSession => _roleSessionProxy.HasConnectedSession;
        public int ReceivedCount { get; private set; }
        public int ForwardedOutboundCount { get; private set; }
        public int SentCount { get; private set; }
        public int LastSentOpcode { get; private set; } = -1;
        public string LastStatus { get; private set; } = "Merchant-room official-session bridge inactive.";

        public SocialRoomMerchantOfficialSessionBridgeManager(Func<MapleRoleSessionProxy> roleSessionProxyFactory = null)
        {
            _roleSessionProxy = (roleSessionProxyFactory ?? (() => MapleRoleSessionProxyFactory.GlobalV95.CreateChannel()))();
            _roleSessionProxy.ServerPacketReceived += OnRoleSessionServerPacketReceived;
            _roleSessionProxy.ClientPacketReceived += OnRoleSessionClientPacketReceived;
        }

        public static byte[] BuildMerchantOutboundPacket(byte requestSubtype, ReadOnlySpan<byte> requestBody = default)
        {
            using PacketWriter writer = new();
            writer.Write(OutboundMiniRoomOpcode);
            writer.WriteByte(requestSubtype);
            if (!requestBody.IsEmpty)
            {
                writer.WriteBytes(requestBody.ToArray());
            }

            return writer.ToArray();
        }

        public static byte[] BuildPersonalShopBuyOutboundPacket(bool buyFromEntrustedShop, byte itemIndex, short bundleCount, uint itemCrc)
        {
            byte requestSubtype = buyFromEntrustedShop ? RequestSubtypeEntrustedShopBuy : RequestSubtypePersonalShopBuy;
            using PacketWriter writer = new();
            writer.WriteByte(itemIndex);
            writer.Write(Math.Max((short)1, bundleCount));
            writer.Write(itemCrc);
            return BuildMerchantOutboundPacket(requestSubtype, writer.ToArray());
        }

        public static byte[] BuildMerchantOpenOutboundPacket(string title)
        {
            using PacketWriter writer = new();
            writer.WriteMapleString(string.IsNullOrWhiteSpace(title) ? "Shop" : title.Trim());
            return BuildMerchantOutboundPacket(RequestSubtypeOpenRoom, writer.ToArray());
        }

        public static byte[] BuildMerchantPutItemOutboundPacket(byte inventorySlot, short bundleCount, int bundlePrice, uint itemCrc)
        {
            using PacketWriter writer = new();
            writer.WriteByte(inventorySlot);
            writer.Write(Math.Max((short)1, bundleCount));
            writer.WriteInt(Math.Max(0, bundlePrice));
            writer.Write(itemCrc);
            return BuildMerchantOutboundPacket(RequestSubtypePutItem, writer.ToArray());
        }

        public static byte[] BuildEntrustedShopVisitListOutboundPacket()
        {
            return BuildMerchantOutboundPacket(RequestSubtypeEntrustedShopVisitList);
        }

        public static byte[] BuildEntrustedShopBlacklistOutboundPacket()
        {
            return BuildMerchantOutboundPacket(RequestSubtypeEntrustedShopBlacklist);
        }

        public static byte[] BuildEntrustedShopBlacklistAddOutboundPacket(string visitorName)
        {
            return BuildEntrustedShopBlacklistMutationOutboundPacket(EntrustedShopBlacklistRequestModeAdd, visitorName);
        }

        public static byte[] BuildEntrustedShopBlacklistDeleteOutboundPacket(string visitorName)
        {
            return BuildEntrustedShopBlacklistMutationOutboundPacket(EntrustedShopBlacklistRequestModeDelete, visitorName);
        }

        private static byte[] BuildEntrustedShopBlacklistMutationOutboundPacket(byte mode, string visitorName)
        {
            using PacketWriter writer = new();
            writer.WriteByte(mode);
            writer.WriteMapleString(NormalizeCharacterName(visitorName));
            return BuildMerchantOutboundPacket(RequestSubtypeEntrustedShopBlacklist, writer.ToArray());
        }

        private static string NormalizeCharacterName(string visitorName)
        {
            return string.IsNullOrWhiteSpace(visitorName)
                ? string.Empty
                : visitorName.Trim();
        }

        public string DescribeStatus()
        {
            string lifecycle = IsRunning
                ? $"listening on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}"
                : "inactive";
            string session = HasConnectedSession
                ? "connected Maple session"
                : "no active Maple session";
            string inboundOpcode = InboundOpcode > 0
                ? $"inbound opcode={InboundOpcode}"
                : AutoDetectedInboundOpcode > 0
                    ? $"auto-detected inbound opcode={AutoDetectedInboundOpcode}"
                : "inbound opcode unset";
            string preferredKind = PreferredKind.HasValue
                ? $"targeting {DescribeKind(PreferredKind.Value)}"
                : "merchant owner unset";
            int pendingExpectationCount;
            lock (_sync)
            {
                pendingExpectationCount = _pendingResultExpectations.Count;
            }

            return $"Merchant-room official-session bridge {lifecycle}; {session}; received={ReceivedCount}; forwardedOutbound={ForwardedOutboundCount}; injected={SentCount}; expectedRequests={_expectedResultRequestCount}; matched={_expectedResultMatchCount}; mismatched={_expectedResultMismatchCount}; unexpected={_expectedResultUnexpectedCount}; evicted={_expectedResultEvictedCount}; pending={pendingExpectationCount}; {inboundOpcode}; {preferredKind}. {LastStatus}";
        }

        public string DescribeMerchantOpcodeMap()
        {
            string inbound = InboundOpcode > 0
                ? $"inbound opcode {InboundOpcode} is configured"
                : AutoDetectedInboundOpcode > 0
                    ? $"inbound opcode {AutoDetectedInboundOpcode} was auto-detected from modeled merchant payloads"
                    : $"inbound opcode is not mapped yet; auto-detection shape-checks CPersonalShopDlg::OnPacket/CEntrustedShopDlg::OnPacket result payloads on opcode {DefaultInboundMiniRoomOpcode}";
            string outboundSubtypes =
                "10 CPersonalShopDlg::SetRet(nRet=2), 11 open/setup room, 15 put/list item, 23 CPersonalShopDlg::BuyItem(personal), 34 CPersonalShopDlg::BuyItem(entrusted), 39 CEntrustedShopDlg::OnGoOut, 40 CEntrustedShopDlg::OnArrange, 41 CEntrustedShopDlg::SetRet(nRet=8), 43 CEntrustedShopDlg::OnWithdrawMoney, 46 CEntrustedShopDlg::OnVisitList, 47 CEntrustedShopDlg::OnBlackList / child add-delete";
            return
                $"Merchant-room opcode map: outbound MiniRoom requests use opcode {OutboundMiniRoomOpcode} with recovered request subtypes {outboundSubtypes}; server-owned merchant updates currently model subtypes 24, 25, 26, 27, 40, 42, 44, 46, and 47 through CPersonalShopDlg::OnPacket/CEntrustedShopDlg::OnPacket with subtype 25 forwarding into CMiniRoomBaseDlg::OnPacketBase. {inbound}.";
        }

        public string DescribeRecentOutboundPackets(int maxCount = 10)
        {
            int normalizedCount = Math.Clamp(maxCount, 1, MaxRecentOutboundPackets);
            lock (_sync)
            {
                if (_recentOutboundPackets.Count == 0)
                {
                    return "Merchant-room official-session bridge outbound history is empty.";
                }

                OutboundPacketTrace[] entries = _recentOutboundPackets
                    .Reverse()
                    .Take(normalizedCount)
                    .ToArray();
                return "Merchant-room official-session bridge outbound history:"
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        entries.Select(entry =>
                            $"opcode={entry.Opcode} type={entry.PacketType} payloadLen={entry.PayloadLength} source={entry.Source} summary={entry.Summary} payloadHex={entry.PayloadHex} raw={entry.RawPacketHex}"));
            }
        }

        public string ClearRecentOutboundPackets()
        {
            lock (_sync)
            {
                _recentOutboundPackets.Clear();
                _pendingResultExpectations.Clear();
            }

            LastStatus = "Merchant-room official-session bridge outbound history cleared.";
            return LastStatus;
        }

        public bool TryReplayRecentOutboundPacket(int historyIndexFromNewest, out string status)
        {
            if (historyIndexFromNewest <= 0)
            {
                status = "Merchant-room replay index must be 1 or greater.";
                LastStatus = status;
                return false;
            }

            OutboundPacketTrace[] entries;
            lock (_sync)
            {
                if (_recentOutboundPackets.Count == 0)
                {
                    status = "No captured merchant-room outbound client packets are available to replay.";
                    LastStatus = status;
                    return false;
                }

                if (historyIndexFromNewest > _recentOutboundPackets.Count)
                {
                    status = $"Merchant-room replay index {historyIndexFromNewest} exceeds the {_recentOutboundPackets.Count} captured outbound packet(s).";
                    LastStatus = status;
                    return false;
                }

                entries = _recentOutboundPackets.ToArray();
            }

            OutboundPacketTrace trace = entries[^historyIndexFromNewest];
            if (string.IsNullOrWhiteSpace(trace.RawPacketHex))
            {
                status = $"Captured merchant-room outbound packet {historyIndexFromNewest} has no raw payload to replay.";
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
                status = $"Captured merchant-room outbound packet {historyIndexFromNewest} could not be replayed: {ex.Message}";
                LastStatus = status;
                return false;
            }
        }

        public void Start(SocialRoomKind preferredKind, int listenPort, string remoteHost, int remotePort, ushort inboundOpcode = 0)
        {
            if (!IsMerchantKind(preferredKind))
            {
                LastStatus = "Merchant-room official-session bridge only accepts personal-shop or entrusted-shop owners.";
                return;
            }

            lock (_sync)
            {
                StopInternal(clearPending: true);
                ResetInboundState();

                try
                {
                    ListenPort = listenPort <= 0 ? DefaultListenPort : listenPort;
                    RemoteHost = string.IsNullOrWhiteSpace(remoteHost) ? IPAddress.Loopback.ToString() : remoteHost.Trim();
                    RemotePort = remotePort;
                    InboundOpcode = inboundOpcode;
                    PreferredKind = preferredKind;
                    if (!_roleSessionProxy.Start(ListenPort, RemoteHost, RemotePort, out string proxyStatus))
                    {
                        StopInternal(clearPending: true);
                        LastStatus = proxyStatus;
                        return;
                    }

                    LastStatus = InboundOpcode > 0
                        ? $"Merchant-room official-session bridge listening on 127.0.0.1:{ListenPort}, proxying to {RemoteHost}:{RemotePort}, filtering inbound opcode {InboundOpcode}, and targeting {DescribeKind(preferredKind)}. {proxyStatus}"
                        : $"Merchant-room official-session bridge listening on 127.0.0.1:{ListenPort}, proxying to {RemoteHost}:{RemotePort}, and targeting {DescribeKind(preferredKind)}; inbound opcode is unset so packets are shape-checked for merchant result subtypes 24, 25, 26, 27, 40, 42, 44, 46, and 47. {proxyStatus}";
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    LastStatus = $"Merchant-room official-session bridge failed to start: {ex.Message}";
                }
            }
        }

        public bool TryStartFromDiscovery(
            SocialRoomKind preferredKind,
            int listenPort,
            int remotePort,
            ushort inboundOpcode,
            string processSelector,
            int? localPort,
            out string status)
        {
            if (!IsMerchantKind(preferredKind))
            {
                status = "Merchant-room official-session bridge only accepts personal-shop or entrusted-shop owners.";
                LastStatus = status;
                return false;
            }

            if (!TryResolveProcessSelector(processSelector, out int? owningProcessId, out string owningProcessName, out string selectorError))
            {
                status = selectorError;
                LastStatus = status;
                return false;
            }

            IReadOnlyList<MemoryGameOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates =
                MemoryGameOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            if (!MemoryGameOfficialSessionBridgeManager.TryResolveDiscoveryCandidate(
                    candidates,
                    remotePort,
                    owningProcessId,
                    owningProcessName,
                    localPort,
                    out MemoryGameOfficialSessionBridgeManager.SessionDiscoveryCandidate candidate,
                    out status))
            {
                LastStatus = status;
                return false;
            }

            Start(preferredKind, listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, inboundOpcode);
            status =
                $"Merchant-room official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} for {DescribeKind(preferredKind)}. {LastStatus}";
            LastStatus = status;
            return true;
        }

        public string DescribeDiscoveredSessions(int remotePort, string processSelector = null, int? localPort = null)
        {
            if (!TryResolveProcessSelector(processSelector, out int? owningProcessId, out string owningProcessName, out string selectorError))
            {
                return selectorError;
            }

            IReadOnlyList<MemoryGameOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates =
                MemoryGameOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            return MemoryGameOfficialSessionBridgeManager.DescribeDiscoveryCandidates(candidates, remotePort, owningProcessId, owningProcessName, localPort);
        }

        public void Stop()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
                LastStatus = "Merchant-room official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out SocialRoomMerchantPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(string source, bool success, string detail)
        {
            string summary = string.IsNullOrWhiteSpace(detail) ? "merchant-room payload" : detail;
            LastStatus = success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.";
        }

        public bool TrySendOutboundRawPacket(byte[] rawPacket, out string status)
        {
            if (!TryValidateOutboundRawPacket(rawPacket, out int opcode, out byte packetType, out string error))
            {
                status = error;
                LastStatus = status;
                return false;
            }

            if (!_roleSessionProxy.HasConnectedSession)
            {
                status = "Merchant-room official-session bridge has no connected Maple session for outbound injection.";
                LastStatus = status;
                return false;
            }

            byte[] clonedRawPacket = (byte[])rawPacket.Clone();
            if (!_roleSessionProxy.TrySendToServer(clonedRawPacket, out string proxyStatus))
            {
                status = proxyStatus;
                LastStatus = status;
                return false;
            }

            SentCount++;
            LastSentOpcode = opcode;
            RecordObservedOutboundPacket(clonedRawPacket, "simulator-send");
            status = $"Injected merchant-room outbound opcode {opcode} subtype {packetType} into live session.";
            LastStatus = status;
            return true;
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
            if (e == null || e.IsInit)
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            if (!TryDecodeOpcode(e.RawPacket, out int opcode, out byte[] payload)
                || !ShouldQueueInboundMerchantPacket(opcode, payload, out string autoMapDetail))
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            _pendingMessages.Enqueue(new SocialRoomMerchantPacketInboxMessage(
                PreferredKind ?? SocialRoomKind.PersonalShop,
                (byte[])e.RawPacket.Clone(),
                $"official-session:{e.SourceEndpoint}",
                $"packetrecv {opcode} {Convert.ToHexString(payload)}"));
            string expectationDetail = RecordRecoveredInboundExpectationResult(opcode, payload);
            ReceivedCount++;
            LastStatus = $"Queued merchant-room opcode {opcode} subtype {payload[0]} from live session {e.SourceEndpoint}. {autoMapDetail} {expectationDetail}";
        }

        private void OnRoleSessionClientPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            if (e == null || e.IsInit)
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            RecordObservedOutboundPacket(e.RawPacket, $"official-session-client:{e.SourceEndpoint}");
            LastStatus = _roleSessionProxy.LastStatus;
        }

        private void RecordObservedOutboundPacket(byte[] rawPacket, string source)
        {
            if (!TryValidateOutboundRawPacket(rawPacket, out int opcode, out byte packetType, out _))
            {
                return;
            }

            byte[] payload = rawPacket.Length > sizeof(ushort)
                ? rawPacket[sizeof(ushort)..]
                : Array.Empty<byte>();
            string summary = "observed";
            lock (_sync)
            {
                if (PacketOwnedSocialUtilityPacketTable.TryBuildRecoveredResultExpectation(
                        "Merchant",
                        opcode,
                        payload,
                        out int[] expectedInboundOpcodes,
                        out byte[] expectedInboundSubtypes,
                        out string expectationSummary))
                {
                    AddPendingResultExpectation(new PendingResultExpectation(
                        opcode,
                        packetType,
                        source,
                        Convert.ToHexString(payload),
                        expectedInboundOpcodes,
                        expectedInboundSubtypes,
                        expectationSummary));
                    summary = expectationSummary;
                }

                _recentOutboundPackets.Enqueue(new OutboundPacketTrace(
                    opcode,
                    packetType,
                    payload.Length,
                    Convert.ToHexString(payload),
                    Convert.ToHexString(rawPacket ?? Array.Empty<byte>()),
                    source,
                    summary));
                while (_recentOutboundPackets.Count > MaxRecentOutboundPackets)
                {
                    _recentOutboundPackets.Dequeue();
                }
            }
        }

        private void AddPendingResultExpectation(PendingResultExpectation expectation)
        {
            _pendingResultExpectations.Add(expectation);
            _expectedResultRequestCount++;
            while (_pendingResultExpectations.Count > MaxPendingResultExpectations)
            {
                _pendingResultExpectations.RemoveAt(0);
                _expectedResultEvictedCount++;
            }
        }

        private string RecordRecoveredInboundExpectationResult(int inboundOpcode, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return "No recovered-table expectation was evaluated for an empty merchant payload.";
            }

            if (!PacketOwnedSocialUtilityPacketTable.TryDecodeRecoveredInboundBranch(
                    "Merchant",
                    inboundOpcode,
                    payload,
                    out byte inboundSubtype,
                    out _,
                    out string branchSummary))
            {
                return "No recovered-table expectation was evaluated for an unknown merchant branch.";
            }

            lock (_sync)
            {
                for (int i = 0; i < _pendingResultExpectations.Count; i++)
                {
                    PendingResultExpectation expectation = _pendingResultExpectations[i];
                    if ((expectation.ExpectedInboundOpcodes?.Length ?? 0) > 0
                        && !expectation.ExpectedInboundOpcodes.Contains(inboundOpcode))
                    {
                        continue;
                    }

                    if ((expectation.ExpectedInboundSubtypes?.Length ?? 0) == 0
                        || expectation.ExpectedInboundSubtypes.Contains(inboundSubtype))
                    {
                        _pendingResultExpectations.RemoveAt(i);
                        _expectedResultMatchCount++;
                        return $"{branchSummary}; matched {expectation.ExpectationSummary} from {expectation.Source}.";
                    }
                }

                int mismatchIndex = _pendingResultExpectations.FindIndex(expectation =>
                    (expectation.ExpectedInboundOpcodes?.Length ?? 0) == 0
                    || expectation.ExpectedInboundOpcodes.Contains(inboundOpcode));
                if (mismatchIndex >= 0)
                {
                    PendingResultExpectation expectation = _pendingResultExpectations[mismatchIndex];
                    _pendingResultExpectations.RemoveAt(mismatchIndex);
                    _expectedResultMismatchCount++;
                    return $"{branchSummary}; mismatched pending {expectation.ExpectationSummary} from {expectation.Source}.";
                }

                _expectedResultUnexpectedCount++;
                return $"{branchSummary}; no pending recovered-table request expectation.";
            }
        }

        private void StopInternal(bool clearPending)
        {
            _roleSessionProxy.Stop(resetCounters: clearPending);

            if (clearPending)
            {
                while (_pendingMessages.TryDequeue(out _))
                {
                }
            }
        }

        private void ResetInboundState()
        {
            ReceivedCount = 0;
            ForwardedOutboundCount = 0;
            SentCount = 0;
            LastSentOpcode = -1;
            AutoDetectedInboundOpcode = 0;
            _expectedResultRequestCount = 0;
            _expectedResultMatchCount = 0;
            _expectedResultMismatchCount = 0;
            _expectedResultUnexpectedCount = 0;
            _expectedResultEvictedCount = 0;
            lock (_sync)
            {
                _recentOutboundPackets.Clear();
                _pendingResultExpectations.Clear();
            }

            while (_pendingMessages.TryDequeue(out _))
            {
            }
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

            string normalized = NormalizeProcessSelector(selector);
            if (normalized.Length == 0)
            {
                error = "Merchant-room official-session discovery requires a process name or pid when a selector is provided.";
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

        private static bool TryValidateOutboundRawPacket(byte[] rawPacket, out int opcode, out byte packetType, out string error)
        {
            opcode = 0;
            packetType = 0;
            error = null;
            if (!TryDecodeOpcode(rawPacket, out opcode, out byte[] payload))
            {
                error = "Merchant-room outbound packet requires an opcode-wrapped frame.";
                return false;
            }

            if (payload.Length == 0)
            {
                error = "Merchant-room outbound payload is empty.";
                return false;
            }

            if (opcode != OutboundMiniRoomOpcode)
            {
                error = $"Merchant-room outbound packet opcode must be {OutboundMiniRoomOpcode}, but was {opcode}.";
                return false;
            }

            packetType = payload[0];
            return true;
        }

        private static bool IsMerchantKind(SocialRoomKind kind)
        {
            return kind is SocialRoomKind.PersonalShop or SocialRoomKind.EntrustedShop;
        }

        private static bool IsModeledMerchantPacketType(byte packetType)
        {
            return packetType is 24 or 25 or 26 or 27 or 40 or 42 or 44 or 46 or 47;
        }

        private bool ShouldQueueInboundMerchantPacket(int opcode, byte[] payload, out string detail)
        {
            detail = string.Empty;
            if (payload == null || payload.Length == 0 || !IsModeledMerchantPacketType(payload[0]))
            {
                return false;
            }

            if (InboundOpcode > 0)
            {
                if (opcode != InboundOpcode)
                {
                    return false;
                }

                detail = $"Configured inbound opcode {InboundOpcode} matched the merchant OnPacket subtype table.";
                return true;
            }

            if (!TryIdentifyMerchantInboundPayloadForAutoMapping(payload, out detail))
            {
                return false;
            }

            if (AutoDetectedInboundOpcode == 0)
            {
                AutoDetectedInboundOpcode = (ushort)Math.Clamp(opcode, ushort.MinValue, ushort.MaxValue);
                detail = $"Auto-detected inbound opcode {AutoDetectedInboundOpcode} because {detail}";
                return true;
            }

            if (opcode != AutoDetectedInboundOpcode)
            {
                return false;
            }

            detail = $"Auto-detected inbound opcode {AutoDetectedInboundOpcode} matched again because {detail}";
            return true;
        }

        public static bool TryIdentifyMerchantInboundPayloadForAutoMapping(byte[] payload, out string detail)
        {
            detail = string.Empty;
            if (payload == null || payload.Length == 0)
            {
                detail = "the payload is empty";
                return false;
            }

            byte packetType = payload[0];
            switch (packetType)
            {
                case 24:
                    if (payload.Length >= 2)
                    {
                        detail = "subtype 24 has the personal-shop buy-result byte for CPersonalShopDlg::OnPacket.";
                        return true;
                    }

                    break;
                case 25:
                    if (payload.Length >= 2 && IsKnownMiniRoomBaseSubtype(payload[1]))
                    {
                        detail = $"subtype 25 wraps CMiniRoomBaseDlg::OnPacketBase subtype {payload[1]}.";
                        return true;
                    }

                    break;
                case 26:
                    if (payload.Length >= 2)
                    {
                        detail = "subtype 26 has the sold-item result shape for CPersonalShopDlg::OnSoldItemResult.";
                        return true;
                    }

                    break;
                case 27:
                    if (payload.Length >= 3)
                    {
                        detail = "subtype 27 has the move-to-inventory row shape for CPersonalShopDlg::OnMoveItemToInventoryResult.";
                        return true;
                    }

                    break;
                case 40:
                    if (payload.Length == 5)
                    {
                        detail = "subtype 40 has the exact arrange-result int shape for CEntrustedShopDlg::OnArrangeItemResult.";
                        return true;
                    }

                    break;
                case 42:
                    if (payload.Length == 2)
                    {
                        detail = "subtype 42 has the exact withdraw-all result-byte shape for CEntrustedShopDlg::OnPacket.";
                        return true;
                    }

                    break;
                case 44:
                    if (payload.Length == 1)
                    {
                        detail = "subtype 44 has the exact withdraw-money no-body shape for CEntrustedShopDlg::OnPacket.";
                        return true;
                    }

                    break;
                case 46:
                    if (payload.Length >= 2)
                    {
                        detail = "subtype 46 has the entrusted visit-list result shape for CEntrustedShopDlg::OnPacket.";
                        return true;
                    }

                    break;
                case 47:
                    if (payload.Length >= 2)
                    {
                        detail = "subtype 47 has the entrusted blacklist result shape for CEntrustedShopDlg::OnPacket.";
                        return true;
                    }

                    break;
            }

            detail = $"subtype {packetType} does not match a modeled merchant OnPacket payload shape.";
            return false;
        }

        private static bool IsKnownMiniRoomBaseSubtype(byte packetSubType)
        {
            return packetSubType is 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 14;
        }

        private static string DescribeKind(SocialRoomKind kind)
        {
            return kind switch
            {
                SocialRoomKind.PersonalShop => "personal-shop",
                SocialRoomKind.EntrustedShop => "entrusted-shop",
                _ => kind.ToString()
            };
        }
    }
}
