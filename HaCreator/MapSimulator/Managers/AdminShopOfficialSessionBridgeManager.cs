using MapleLib.PacketLib;
using System;
using System.Collections.Concurrent;
using System.Net;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Proxies a live channel session and forwards only CField::OnPacket
    /// CAdminShopDlg opcodes into the admin-shop packet-owned owner seam.
    /// </summary>
    public sealed class AdminShopOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18509;

        private readonly object _sync = new();
        private readonly MapleRoleSessionProxy _roleSessionProxy;
        private readonly ConcurrentQueue<AdminShopPacketInboxMessage> _pendingMessages = new();

        public int ListenPort { get; private set; } = DefaultListenPort;
        public string RemoteHost { get; private set; } = IPAddress.Loopback.ToString();
        public int RemotePort { get; private set; }
        public bool IsRunning => _roleSessionProxy.IsRunning;
        public bool HasAttachedClient => _roleSessionProxy.HasAttachedClient;
        public bool HasConnectedSession => _roleSessionProxy.HasConnectedSession;
        public int ReceivedCount { get; private set; }
        public int LastReceivedPacketType { get; private set; } = -1;
        public byte[] LastReceivedPayload { get; private set; } = Array.Empty<byte>();
        public string LastStatus { get; private set; } = "Admin-shop official-session bridge inactive.";

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
            return $"Admin-shop official-session bridge {lifecycle}; {session}; received={ReceivedCount}; inbound opcodes=366,367.{lastPacket} {LastStatus}";
        }

        public void Start(int listenPort, string remoteHost, int remotePort)
        {
            lock (_sync)
            {
                StopInternal(clearPending: false);
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

                    LastStatus = $"Admin-shop official-session bridge listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}. {proxyStatus}";
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: false);
                    LastStatus = $"Admin-shop official-session bridge failed to start: {ex.Message}";
                }
            }
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
                LastStatus = _roleSessionProxy.LastStatus;
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
                $"official-admin-shop-session:{e.SourceEndpoint}",
                $"packetclientraw {Convert.ToHexString(e.RawPacket ?? Array.Empty<byte>())}"));
            ReceivedCount++;
            LastReceivedPacketType = packetType;
            LastReceivedPayload = payload ?? Array.Empty<byte>();
            LastStatus = $"Queued {AdminShopPacketInboxManager.DescribePacketType(packetType)} from live channel session {e.SourceEndpoint}.";
        }

        private void OnRoleSessionClientPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
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

            ReceivedCount = 0;
            LastReceivedPacketType = -1;
            LastReceivedPayload = Array.Empty<byte>();
        }
    }
}
