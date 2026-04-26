using MapleLib.PacketLib;
using HaCreator.MapSimulator.Interaction;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Proxies a live Maple session and forwards opcode-wrapped trading-room traffic
    /// into the existing CTradingRoomDlg::OnPacket payload seam.
    /// </summary>
    public sealed class TradingRoomOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18492;
        public const ushort OutboundTradingRoomOpcode = 144;
        public const ushort AutoDetectInboundTradingRoomOpcode = ushort.MaxValue;
        private const int MaxRecentOutboundPackets = 32;

        private readonly ConcurrentQueue<TradingRoomPacketInboxMessage> _pendingMessages = new();
        private readonly object _sync = new();
        private readonly Queue<OutboundPacketTrace> _recentOutboundPackets = new();
        private readonly MapleRoleSessionProxy _roleSessionProxy;
        private bool _useRecoveredInboundOpcodeTable;

        public readonly record struct OutboundPacketTrace(
            int Opcode,
            byte PacketType,
            int PayloadLength,
            string PayloadHex,
            string RawPacketHex,
            string Source);

        public int ListenPort { get; private set; } = DefaultListenPort;
        public string RemoteHost { get; private set; } = IPAddress.Loopback.ToString();
        public int RemotePort { get; private set; }
        public ushort InboundTradingRoomOpcode { get; private set; }
        public ushort AutoDetectedInboundTradingRoomOpcode { get; private set; }
        public bool UsesRecoveredInboundOpcodeTable => _useRecoveredInboundOpcodeTable;
        public bool IsRunning => _roleSessionProxy.IsRunning;
        public bool HasAttachedClient => _roleSessionProxy.HasAttachedClient;
        public bool HasConnectedSession => _roleSessionProxy.HasConnectedSession;
        public int ReceivedCount => _roleSessionProxy.ReceivedCount;
        public int ForwardedOutboundCount { get; private set; }
        public int ForwardedOutboundCrcCount { get; private set; }
        public int SentCount { get; private set; }
        public int LastSentOpcode { get; private set; } = -1;
        public string LastStatus { get; private set; } = "Trading-room official-session bridge inactive.";

        public TradingRoomOfficialSessionBridgeManager(Func<MapleRoleSessionProxy> roleSessionProxyFactory = null)
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
            string inboundOpcode = InboundTradingRoomOpcode > 0
                ? $"inbound opcode={InboundTradingRoomOpcode}"
                : _useRecoveredInboundOpcodeTable
                    ? $"recovered inbound opcode set={string.Join("/", TradingRoomPacketTable.GetRecoveredInboundOpcodes())}"
                : AutoDetectedInboundTradingRoomOpcode > 0
                    ? $"auto-detected inbound opcode={AutoDetectedInboundTradingRoomOpcode}"
                : "inbound opcode unset";
            return $"Trading-room official-session bridge {lifecycle}; {session}; received={ReceivedCount}; forwardedOutbound={ForwardedOutboundCount}; forwardedOutboundCrc={ForwardedOutboundCrcCount}; injected={SentCount}; {inboundOpcode}. {LastStatus}";
        }

        public string DescribeRecentOutboundPackets(int maxCount = 10)
        {
            int normalizedCount = Math.Clamp(maxCount, 1, MaxRecentOutboundPackets);
            lock (_sync)
            {
                if (_recentOutboundPackets.Count == 0)
                {
                    return "Trading-room official-session bridge outbound history is empty.";
                }

                OutboundPacketTrace[] entries = _recentOutboundPackets
                    .Reverse()
                    .Take(normalizedCount)
                    .ToArray();
                return "Trading-room official-session bridge outbound history:"
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        entries.Select(entry =>
                            $"opcode={entry.Opcode} type={entry.PacketType} payloadLen={entry.PayloadLength} source={entry.Source} payloadHex={entry.PayloadHex} raw={entry.RawPacketHex}"));
            }
        }

        public string DescribeTradingRoomOpcodeMap()
        {
            string inbound = InboundTradingRoomOpcode > 0
                ? $"inbound opcode {InboundTradingRoomOpcode} is configured"
                : _useRecoveredInboundOpcodeTable
                    ? $"recovered inbound opcode set {string.Join("/", TradingRoomPacketTable.GetRecoveredInboundOpcodes())} is active from targeted IDA packet-table recovery"
                : AutoDetectedInboundTradingRoomOpcode > 0
                    ? $"inbound opcode {AutoDetectedInboundTradingRoomOpcode} was auto-detected from modeled CTradingRoomDlg::OnPacket payloads"
                : "inbound opcode is not mapped yet; auto-detection shape-checks modeled CTradingRoomDlg::OnPacket payloads";
            return
                $"Trading-room opcode map: outbound CTradingRoomDlg client requests use opcode {OutboundTradingRoomOpcode}; subtype 17 is the Trade request and subtype 20 is the CRC reply emitted by CTradingRoomDlg::OnTrade. Server-owned CTradingRoomDlg::OnPacket payloads currently model subtypes 15 put-item, 16 put-money, 17 trade handoff, 20 CRC follow-up, and 21 exceed-limit; {inbound}. {TradingRoomPacketTable.DescribeRecoveredPacketTable()}";
        }

        public string ClearRecentOutboundPackets()
        {
            lock (_sync)
            {
                _recentOutboundPackets.Clear();
            }

            LastStatus = "Trading-room official-session bridge outbound history cleared.";
            return LastStatus;
        }

        public bool TryReplayRecentOutboundPacket(int historyIndexFromNewest, out string status)
        {
            if (historyIndexFromNewest <= 0)
            {
                status = "Trading-room replay index must be 1 or greater.";
                LastStatus = status;
                return false;
            }

            OutboundPacketTrace[] entries;
            lock (_sync)
            {
                if (_recentOutboundPackets.Count == 0)
                {
                    status = "No captured trading-room outbound client packets are available to replay.";
                    LastStatus = status;
                    return false;
                }

                if (historyIndexFromNewest > _recentOutboundPackets.Count)
                {
                    status = $"Trading-room replay index {historyIndexFromNewest} exceeds the {_recentOutboundPackets.Count} captured outbound packet(s).";
                    LastStatus = status;
                    return false;
                }

                entries = _recentOutboundPackets.ToArray();
            }

            OutboundPacketTrace trace = entries[^historyIndexFromNewest];
            if (string.IsNullOrWhiteSpace(trace.RawPacketHex))
            {
                status = $"Captured trading-room outbound packet {historyIndexFromNewest} has no raw payload to replay.";
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
                status = $"Captured trading-room outbound packet {historyIndexFromNewest} could not be replayed: {ex.Message}";
                LastStatus = status;
                return false;
            }
        }

        public void Start(int listenPort, string remoteHost, int remotePort, ushort inboundTradingRoomOpcode = 0)
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
                    _useRecoveredInboundOpcodeTable =
                        inboundTradingRoomOpcode == 0
                        || (inboundTradingRoomOpcode != AutoDetectInboundTradingRoomOpcode
                            && TradingRoomPacketTable.IsRecoveredInboundOpcode(inboundTradingRoomOpcode));
                    InboundTradingRoomOpcode = inboundTradingRoomOpcode == AutoDetectInboundTradingRoomOpcode
                        ? (ushort)0
                        : _useRecoveredInboundOpcodeTable
                            ? (ushort)0
                            : inboundTradingRoomOpcode;
                    if (!_roleSessionProxy.Start(ListenPort, RemoteHost, RemotePort, out string proxyStatus))
                    {
                        StopInternal(clearPending: false);
                        LastStatus = proxyStatus;
                        return;
                    }

                    LastStatus = InboundTradingRoomOpcode > 0
                        ? $"Trading-room official-session bridge listening on 127.0.0.1:{ListenPort}, proxying to {RemoteHost}:{RemotePort}, and filtering inbound opcode {InboundTradingRoomOpcode}. {proxyStatus}"
                        : _useRecoveredInboundOpcodeTable
                            ? $"Trading-room official-session bridge listening on 127.0.0.1:{ListenPort}, proxying to {RemoteHost}:{RemotePort}, and filtering the recovered CTradingRoomDlg::OnPacket inbound opcode set {string.Join("/", TradingRoomPacketTable.GetRecoveredInboundOpcodes())}. {proxyStatus}"
                            : $"Trading-room official-session bridge listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}; inbound opcode is unset so server packets are shape-checked for CTradingRoomDlg::OnPacket subtypes 15, 16, 17, 20, and 21. {proxyStatus}";
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: false);
                    LastStatus = $"Trading-room official-session bridge failed to start: {ex.Message}";
                }
            }
        }

        public bool TryStartFromDiscovery(int listenPort, int remotePort, ushort inboundTradingRoomOpcode, string processSelector, int? localPort, out string status)
        {
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

            Start(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, inboundTradingRoomOpcode);
            status =
                $"Trading-room official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {LastStatus}";
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
                StopInternal(clearPending: false);
                LastStatus = "Trading-room official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out TradingRoomPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(string source, bool success, string detail)
        {
            string summary = string.IsNullOrWhiteSpace(detail) ? "trading-room payload" : detail;
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
                status = "Trading-room official-session bridge has no connected Maple session for outbound injection.";
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
            status = $"Injected trading-room outbound opcode {opcode} subtype {packetType} into live session.";
            LastStatus = status;
            return true;
        }

        public void Dispose()
        {
            lock (_sync)
            {
                StopInternal(clearPending: false);
            }
        }

        public bool MatchesInboundOpcodeConfiguration(ushort configuredInboundTradingRoomOpcode)
        {
            if (configuredInboundTradingRoomOpcode == AutoDetectInboundTradingRoomOpcode)
            {
                return !_useRecoveredInboundOpcodeTable && InboundTradingRoomOpcode == 0;
            }

            if (configuredInboundTradingRoomOpcode == 0
                || TradingRoomPacketTable.IsRecoveredInboundOpcode(configuredInboundTradingRoomOpcode))
            {
                return _useRecoveredInboundOpcodeTable && InboundTradingRoomOpcode == 0;
            }

            return !_useRecoveredInboundOpcodeTable && InboundTradingRoomOpcode == configuredInboundTradingRoomOpcode;
        }

        private void OnRoleSessionServerPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            if (e == null || e.IsInit)
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            if (!TryDecodeOpcode(e.RawPacket, out int opcode, out byte[] payload)
                || !ShouldQueueInboundTradingRoomPacket(opcode, payload, out string autoMapDetail))
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            _pendingMessages.Enqueue(new TradingRoomPacketInboxMessage(
                (byte[])e.RawPacket.Clone(),
                $"official-session:{e.SourceEndpoint}",
                $"packetrecv {opcode} {Convert.ToHexString(payload)}"));
            LastStatus = $"Queued trading-room opcode {opcode} subtype {payload[0]} from live session {e.SourceEndpoint}. {autoMapDetail}";
        }

        private void OnRoleSessionClientPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            if (e == null || e.IsInit)
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            RecordObservedOutboundPacket(e.RawPacket, $"official-session-client:{e.SourceEndpoint}");
        }

        private void RecordObservedOutboundPacket(byte[] rawPacket, string source)
        {
            if (!TryDecodeOpcode(rawPacket, out int opcode, out byte[] payload)
                || opcode != OutboundTradingRoomOpcode
                || payload.Length == 0)
            {
                return;
            }

            ForwardedOutboundCount++;
            if (payload[0] == 20)
            {
                ForwardedOutboundCrcCount++;
            }

            lock (_sync)
            {
                while (_recentOutboundPackets.Count >= MaxRecentOutboundPackets)
                {
                    _recentOutboundPackets.Dequeue();
                }

                _recentOutboundPackets.Enqueue(new OutboundPacketTrace(
                    opcode,
                    payload[0],
                    payload.Length,
                    Convert.ToHexString(payload),
                    Convert.ToHexString(rawPacket),
                    source));
            }

            LastStatus = $"Forwarded live trading-room outbound opcode {opcode} subtype {payload[0]} from {source}.";
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
            ForwardedOutboundCount = 0;
            ForwardedOutboundCrcCount = 0;
            SentCount = 0;
            LastSentOpcode = -1;
            AutoDetectedInboundTradingRoomOpcode = 0;
            _useRecoveredInboundOpcodeTable = false;
            lock (_sync)
            {
                _recentOutboundPackets.Clear();
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
                error = "Trading-room official-session discovery requires a process name or pid when a selector is provided.";
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
                error = "Trading-room outbound packet requires an opcode-wrapped frame.";
                return false;
            }

            if (opcode != OutboundTradingRoomOpcode)
            {
                error = $"Trading-room outbound packet opcode must be {OutboundTradingRoomOpcode}, but was {opcode}.";
                return false;
            }

            if (payload.Length == 0)
            {
                error = "Trading-room outbound payload is empty.";
                return false;
            }

            packetType = payload[0];
            return true;
        }

        private static bool IsTradingRoomInboundSubtype(byte packetType)
        {
            return packetType is 15 or 16 or 17 or 20 or 21;
        }

        private bool ShouldQueueInboundTradingRoomPacket(int opcode, byte[] payload, out string detail)
        {
            detail = string.Empty;
            if (payload == null || payload.Length == 0)
            {
                return false;
            }

            if (_useRecoveredInboundOpcodeTable)
            {
                if (!TradingRoomPacketTable.IsRecoveredInboundOpcode((ushort)opcode) || !IsTradingRoomInboundSubtype(payload[0]))
                {
                    return false;
                }

                detail = $"Recovered inbound opcode set {string.Join("/", TradingRoomPacketTable.GetRecoveredInboundOpcodes())} matched the CTradingRoomDlg::OnPacket subtype table.";
                return true;
            }

            if (InboundTradingRoomOpcode > 0)
            {
                if (opcode != InboundTradingRoomOpcode || !IsTradingRoomInboundSubtype(payload[0]))
                {
                    return false;
                }

                detail = $"Configured inbound opcode {InboundTradingRoomOpcode} matched the CTradingRoomDlg::OnPacket subtype table.";
                return true;
            }

            if (!TryIdentifyTradingRoomInboundPayloadForAutoMapping(payload, out detail))
            {
                return false;
            }

            if (AutoDetectedInboundTradingRoomOpcode == 0)
            {
                AutoDetectedInboundTradingRoomOpcode = (ushort)Math.Clamp(opcode, ushort.MinValue, ushort.MaxValue);
                detail = $"Auto-detected inbound opcode {AutoDetectedInboundTradingRoomOpcode} because {detail}";
                return true;
            }

            if (opcode != AutoDetectedInboundTradingRoomOpcode)
            {
                return false;
            }

            detail = $"Auto-detected inbound opcode {AutoDetectedInboundTradingRoomOpcode} matched again because {detail}";
            return true;
        }

        public static bool TryIdentifyTradingRoomInboundPayloadForAutoMapping(byte[] payload, out string detail)
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
                case 15:
                    if (payload.Length >= 5)
                    {
                        detail = "subtype 15 has trader, slot, and GW_ItemSlotBase body bytes for CTradingRoomDlg::OnPutItem.";
                        return true;
                    }

                    break;
                case 16:
                    if (payload.Length == 6)
                    {
                        detail = "subtype 16 has the exact trader plus 32-bit meso offer shape for CTradingRoomDlg::OnPutMoney.";
                        return true;
                    }

                    break;
                case 17:
                    detail = "subtype 17 is the CTradingRoomDlg::OnTrade CRC handoff.";
                    return true;
                case 20:
                    if (payload.Length >= 2
                        && ((payload.Length - 2) % 8) == 0
                        && payload[1] == (payload.Length - 2) / 8)
                    {
                        detail = "subtype 20 has the OnTrade checksum follow-up row-count shape.";
                        return true;
                    }

                    break;
                case 21:
                    detail = "subtype 21 is the CTradingRoomDlg::OnExceedLimit failure branch.";
                    return true;
            }

            detail = $"subtype {packetType} does not match a modeled CTradingRoomDlg::OnPacket payload shape.";
            return false;
        }

    }
}
