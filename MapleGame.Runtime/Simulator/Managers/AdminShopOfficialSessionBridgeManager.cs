using MapleLib.PacketLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Proxies a live channel session and forwards only CField::OnPacket
    /// CAdminShopDlg opcodes into the admin-shop packet-owned owner seam.
    /// </summary>
    public sealed class AdminShopOfficialSessionBridgeManager : IDisposable
    {
        private const string DefaultProcessName = "MapleStory";
        public const int DefaultListenPort = 18509;

        private readonly object _sync = new();
        private readonly object _historySync = new();
        private readonly MapleRoleSessionProxy _roleSessionProxy;
        private readonly ConcurrentQueue<AdminShopPacketInboxMessage> _pendingMessages = new();
        private readonly ConcurrentQueue<PendingOutboundPacket> _pendingOutboundPackets = new();
        private readonly Queue<RecentPacketSnapshot> _recentPackets = new();
        private const int MaxRecentPacketCount = 32;
        private const int AdminShopOutboundOpcode = 74;

        private sealed record PendingOutboundPacket(int Opcode, byte[] RawPacket);

        public int ListenPort { get; private set; }
        public string RemoteHost { get; private set; } = IPAddress.Loopback.ToString();
        public int RemotePort { get; private set; }
        public bool IsRunning => _roleSessionProxy.IsRunning;
        public bool HasAttachedClient => _roleSessionProxy.HasAttachedClient;
        public bool HasConnectedSession => _roleSessionProxy.HasConnectedSession;
        public int ReceivedCount { get; private set; }
        public int SentCount { get; private set; }
        public int QueuedCount { get; private set; }
        public int ForwardedOutboundCount { get; private set; }
        public int PendingOutboundCount => _pendingOutboundPackets.Count;
        public int LastReceivedPacketType { get; private set; } = -1;
        public byte[] LastReceivedPayload { get; private set; } = Array.Empty<byte>();
        public int LastSentOpcode { get; private set; } = -1;
        public byte[] LastSentRawPacket { get; private set; } = Array.Empty<byte>();
        public int LastQueuedOpcode { get; private set; } = -1;
        public byte[] LastQueuedRawPacket { get; private set; } = Array.Empty<byte>();
        public string LastStatus { get; private set; } = "Admin-shop official-session bridge inactive.";

        private sealed class RecentPacketSnapshot
        {
            public DateTimeOffset Timestamp { get; init; }
            public int PacketType { get; init; }
            public int PayloadLength { get; init; }
            public string SourceEndpoint { get; init; }
            public string RawPacketHex { get; init; }
        }

        public AdminShopOfficialSessionBridgeManager(Func<MapleRoleSessionProxy> roleSessionProxyFactory = null)
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
            string lastPacket = LastReceivedPacketType >= 0
                ? $" last={AdminShopPacketInboxManager.DescribePacketType(LastReceivedPacketType)}[{Convert.ToHexString(LastReceivedPayload)}]."
                : string.Empty;
            string lastOutbound = LastSentOpcode >= 0
                ? $" lastOut={LastSentOpcode}[{Convert.ToHexString(LastSentRawPacket)}]."
                : string.Empty;
            string lastQueued = LastQueuedOpcode >= 0
                ? $" lastQueued={LastQueuedOpcode}[{Convert.ToHexString(LastQueuedRawPacket)}]."
                : string.Empty;
            return $"Admin-shop official-session bridge {lifecycle}; {session}; received={ReceivedCount}; forwardedOutbound={ForwardedOutboundCount}; sent={SentCount}; pending={PendingOutboundCount}; queued={QueuedCount}; inbound opcodes=366,367; outbound opcode=74.{lastPacket}{lastOutbound}{lastQueued} {LastStatus}";
        }

        public bool TryStart(int listenPort, string remoteHost, int remotePort, out string status)
        {
            lock (_sync)
            {
                try
                {
                    bool autoSelectListenPort = listenPort <= 0;
                    int resolvedListenPort = autoSelectListenPort ? 0 : listenPort;
                    string resolvedRemoteHost = string.IsNullOrWhiteSpace(remoteHost) ? IPAddress.Loopback.ToString() : remoteHost.Trim();
                    if (remotePort <= 0 || remotePort > ushort.MaxValue)
                    {
                        status = $"Admin-shop official-session bridge requires a remote port between 1 and {ushort.MaxValue}.";
                        LastStatus = status;
                        return false;
                    }

                    if (IsRunning
                        && (autoSelectListenPort || ListenPort == resolvedListenPort)
                        && string.Equals(RemoteHost, resolvedRemoteHost, StringComparison.OrdinalIgnoreCase)
                        && RemotePort == remotePort)
                    {
                        status = $"Admin-shop official-session bridge already listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                        LastStatus = status;
                        return true;
                    }

                    StopInternal(clearPending: false);
                    RemoteHost = resolvedRemoteHost;
                    RemotePort = remotePort;
                    if (!_roleSessionProxy.Start(resolvedListenPort, RemoteHost, RemotePort, out string proxyStatus))
                    {
                        StopInternal(clearPending: false);
                        status = proxyStatus;
                        LastStatus = status;
                        return false;
                    }

                    ListenPort = _roleSessionProxy.ListenPort;
                    status = $"Admin-shop official-session bridge listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}. {proxyStatus}";
                    LastStatus = status;
                    return true;
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: false);
                    status = $"Admin-shop official-session bridge failed to start: {ex.Message}";
                    LastStatus = status;
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
            if (HasAttachedClient)
            {
                status = $"Admin-shop official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                LastStatus = status;
                return true;
            }

            bool autoSelectListenPort = listenPort <= 0;
            int resolvedListenPort = autoSelectListenPort ? 0 : listenPort;
            if (!TryResolveProcessSelector(processSelector, out int? owningProcessId, out string owningProcessName, out string selectorError))
            {
                status = selectorError;
                LastStatus = status;
                return false;
            }

            var candidates = CoconutOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            if (!CoconutOfficialSessionBridgeManager.TryResolveDiscoveryCandidate(
                    candidates,
                    remotePort,
                    owningProcessId,
                    owningProcessName,
                    localPort,
                    out CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate candidate,
                    out status))
            {
                LastStatus = status;
                return false;
            }

            if (IsRunning
                && (autoSelectListenPort || ListenPort == resolvedListenPort)
                && RemotePort == candidate.RemoteEndpoint.Port
                && IPAddress.TryParse(RemoteHost, out IPAddress currentRemoteAddress)
                && currentRemoteAddress.Equals(candidate.RemoteEndpoint.Address))
            {
                status = $"Admin-shop official-session bridge remains armed for {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}.";
                LastStatus = status;
                return true;
            }

            bool started = TryStart(resolvedListenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, out string startStatus);
            status = started
                ? $"Admin-shop official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {startStatus}"
                : startStatus;
            LastStatus = status;
            return started;
        }

        public string DescribeDiscoveredSessions(int remotePort, string processSelector = null, int? localPort = null)
        {
            if (!TryResolveProcessSelector(processSelector, out int? owningProcessId, out string owningProcessName, out string selectorError))
            {
                return selectorError;
            }

            var candidates = CoconutOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            return CoconutOfficialSessionBridgeManager.DescribeDiscoveryCandidates(
                candidates,
                remotePort,
                owningProcessId,
                owningProcessName,
                localPort);
        }

        public void Stop()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
                LastStatus = "Admin-shop official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out AdminShopPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public string DescribeRecentPackets(int maxCount = 10)
        {
            RecentPacketSnapshot[] snapshots;
            lock (_historySync)
            {
                snapshots = _recentPackets.ToArray();
            }

            if (snapshots.Length == 0)
            {
                return "Admin-shop official-session bridge has no recent mirrored packets.";
            }

            int takeCount = maxCount <= 0 ? 10 : Math.Min(maxCount, snapshots.Length);
            return string.Join(
                Environment.NewLine,
                snapshots
                    .Reverse()
                    .Take(takeCount)
                    .Select((packet, index) =>
                        $"{index + 1}. {packet.Timestamp:HH:mm:ss.fff} {AdminShopPacketInboxManager.DescribePacketType(packet.PacketType)} payload={packet.PayloadLength} byte(s) source={packet.SourceEndpoint ?? "unknown"} raw={packet.RawPacketHex}"));
        }

        public bool TrySendOutboundPacket(int opcode, IReadOnlyList<byte> payload, out string status)
        {
            if (opcode != AdminShopOutboundOpcode)
            {
                status = $"Admin-shop official-session bridge only accepts outbound opcode {AdminShopOutboundOpcode}.";
                LastStatus = status;
                return false;
            }

            if (!_roleSessionProxy.HasConnectedSession)
            {
                status = "Admin-shop official-session bridge has no connected Maple session for outbound opcode 74.";
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
            status = "Injected admin-shop outbound opcode 74 into live session.";
            LastStatus = status;
            return true;
        }

        public bool TryQueueOutboundPacket(int opcode, IReadOnlyList<byte> payload, out string status)
        {
            if (opcode != AdminShopOutboundOpcode)
            {
                status = $"Admin-shop official-session bridge only queues outbound opcode {AdminShopOutboundOpcode}.";
                LastStatus = status;
                return false;
            }

            byte[] rawPacket = BuildRawPacket((ushort)opcode, payload);
            _pendingOutboundPackets.Enqueue(new PendingOutboundPacket(opcode, rawPacket));
            QueuedCount++;
            LastQueuedOpcode = opcode;
            LastQueuedRawPacket = rawPacket;
            status = "Queued admin-shop outbound opcode 74 for deferred live-session injection.";
            LastStatus = status;
            return true;
        }

        public void ClearRecentPackets()
        {
            lock (_historySync)
            {
                _recentPackets.Clear();
            }

            LastStatus = "Admin-shop official-session bridge cleared recent packet history.";
        }

        public void RecordDispatchResult(AdminShopPacketInboxMessage message, bool success, string detail)
        {
            string packetLabel = AdminShopPacketInboxManager.DescribePacketType(message?.PacketType ?? 0);
            LastStatus = success
                ? $"Applied {packetLabel} from {message?.Source ?? "admin-shop-official-session"}."
                : $"Ignored {packetLabel} from {message?.Source ?? "admin-shop-official-session"}: {detail}";
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
                    ? $"Admin-shop official-session bridge initialized Maple crypto and flushed {flushed} queued opcode 74 packet(s)."
                    : _roleSessionProxy.LastStatus;
                return;
            }

            if (!AdminShopPacketInboxManager.TryDecodeOpcodeFramedPacket(
                    e.RawPacket,
                    out int packetType,
                    out byte[] payload,
                    out _))
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            _pendingMessages.Enqueue(new AdminShopPacketInboxMessage(
                packetType,
                payload,
                $"official-session:admin-shop:{e.SourceEndpoint}",
                $"packetclientraw {Convert.ToHexString(e.RawPacket ?? Array.Empty<byte>())}"));
            RecordRecentPacket(packetType, payload.Length, e.SourceEndpoint, e.RawPacket);
            ReceivedCount++;
            LastReceivedPacketType = packetType;
            LastReceivedPayload = payload ?? Array.Empty<byte>();
            LastStatus = $"Queued {AdminShopPacketInboxManager.DescribePacketType(packetType)} from live channel session {e.SourceEndpoint}.";
        }

        private void OnRoleSessionClientPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            if (e != null && !e.IsInit)
            {
                TryRecordForwardedAdminShopOutbound(e.RawPacket);
            }

            LastStatus = _roleSessionProxy.LastStatus;
        }

        private void StopInternal(bool clearPending)
        {
            _roleSessionProxy.Stop(resetCounters: clearPending);
            if (!clearPending)
            {
                return;
            }

            while (_pendingMessages.TryDequeue(out _))
            {
            }
            while (_pendingOutboundPackets.TryDequeue(out _))
            {
            }

            ReceivedCount = 0;
            SentCount = 0;
            QueuedCount = 0;
            ForwardedOutboundCount = 0;
            LastReceivedPacketType = -1;
            LastReceivedPayload = Array.Empty<byte>();
            LastSentOpcode = -1;
            LastSentRawPacket = Array.Empty<byte>();
            LastQueuedOpcode = -1;
            LastQueuedRawPacket = Array.Empty<byte>();
            ClearRecentPacketsNoStatus();
            ListenPort = 0;
            RemotePort = 0;
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

        private void TryRecordForwardedAdminShopOutbound(byte[] rawPacket)
        {
            if (!AdminShopPacketInboxManager.TryDecodeOpcodeFramedPacket(
                    rawPacket,
                    out int opcode,
                    out _,
                    out _)
                || opcode != AdminShopOutboundOpcode)
            {
                return;
            }

            ForwardedOutboundCount++;
        }

        private void RecordSentOutboundPacket(int opcode, byte[] rawPacket)
        {
            SentCount++;
            LastSentOpcode = opcode;
            LastSentRawPacket = rawPacket ?? Array.Empty<byte>();
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

        private void RecordRecentPacket(int packetType, int payloadLength, string sourceEndpoint, byte[] rawPacket)
        {
            lock (_historySync)
            {
                while (_recentPackets.Count >= MaxRecentPacketCount)
                {
                    _recentPackets.Dequeue();
                }

                _recentPackets.Enqueue(new RecentPacketSnapshot
                {
                    Timestamp = DateTimeOffset.Now,
                    PacketType = packetType,
                    PayloadLength = Math.Max(0, payloadLength),
                    SourceEndpoint = sourceEndpoint,
                    RawPacketHex = Convert.ToHexString(rawPacket ?? Array.Empty<byte>())
                });
            }
        }

        private void ClearRecentPacketsNoStatus()
        {
            lock (_historySync)
            {
                _recentPackets.Clear();
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
                error = "Admin-shop official-session discovery requires a process name or pid when a selector is provided.";
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
    }
}
