using MapleLib.PacketLib;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace MapleLib.Tests.PacketLib
{
    public class MapleRoleSessionProxyTests
    {
        [Fact]
        public void Constructor_InitializesInactiveState()
        {
            using MapleRoleSessionProxy proxy = new MapleRoleSessionProxy(MapleServerRole.Login, MapleHandshakePolicy.GlobalV95);

            Assert.False(proxy.IsRunning);
            Assert.False(proxy.HasAttachedClient);
            Assert.False(proxy.HasConnectedSession);
            Assert.Equal(0, proxy.ReceivedCount);
            Assert.Equal(0, proxy.ClientReceivedCount);
            Assert.Equal(0, proxy.SentCount);
            Assert.Equal(0, proxy.ActiveSessionCount);
            Assert.Null(proxy.LastPacketUtc);
            Assert.Contains("inactive", proxy.LastStatus);
        }

        [Fact]
        public void StartAndStop_TogglesLifecycleState()
        {
            using MapleRoleSessionProxy proxy = new MapleRoleSessionProxy(MapleServerRole.Channel, MapleHandshakePolicy.GlobalV95);
            int listenPort = ReserveLoopbackPort();

            Assert.True(proxy.Start(listenPort, IPAddress.Loopback.ToString(), 8484, out string startStatus), startStatus);
            Assert.True(proxy.IsRunning);
            Assert.Contains("listening", startStatus);

            proxy.Stop(resetCounters: true);

            Assert.False(proxy.IsRunning);
            Assert.Contains("stopped", proxy.LastStatus);
        }

        [Fact]
        public void Start_WhenAlreadyRunningWithSameEndpoint_IsIdempotent()
        {
            using MapleRoleSessionProxy proxy = new MapleRoleSessionProxy(MapleServerRole.Channel, MapleHandshakePolicy.GlobalV95);
            int listenPort = ReserveLoopbackPort();

            Assert.True(proxy.Start(listenPort, IPAddress.Loopback.ToString(), 8484, out string startStatus), startStatus);

            bool startedAgain = proxy.Start(listenPort, IPAddress.Loopback.ToString(), 8484, out string secondStatus);

            Assert.True(startedAgain, secondStatus);
            Assert.Contains("already listening", secondStatus);
            Assert.Equal(listenPort, proxy.ListenPort);
            Assert.Equal(8484, proxy.RemotePort);
        }

        [Fact]
        public void Start_WithAutoSelectedListenPort_ReportsActualBoundPort()
        {
            using MapleRoleSessionProxy proxy = new MapleRoleSessionProxy(MapleServerRole.CashShop, MapleHandshakePolicy.GlobalV95);

            Assert.True(proxy.Start(0, IPAddress.Loopback.ToString(), 8484, out string startStatus), startStatus);

            Assert.True(proxy.ListenPort > 0);
            Assert.Contains($"127.0.0.1:{proxy.ListenPort}", startStatus);
        }

        [Fact]
        public void Start_WhenAlreadyRunningWithDifferentEndpoint_RejectsReconfiguration()
        {
            using MapleRoleSessionProxy proxy = new MapleRoleSessionProxy(MapleServerRole.Channel, MapleHandshakePolicy.GlobalV95);
            int firstListenPort = ReserveLoopbackPort();
            int secondListenPort = ReserveLoopbackPort();

            Assert.True(proxy.Start(firstListenPort, IPAddress.Loopback.ToString(), 8484, out string startStatus), startStatus);

            bool startedAgain = proxy.Start(secondListenPort, IPAddress.Loopback.ToString(), 8585, out string secondStatus);

            Assert.False(startedAgain);
            Assert.Contains("rejected incompatible start request", secondStatus);
            Assert.Equal(firstListenPort, proxy.ListenPort);
            Assert.Equal(8484, proxy.RemotePort);
        }

        [Fact]
        public void TrySendToServer_WithoutConnectedSession_ReturnsFalse()
        {
            using MapleRoleSessionProxy proxy = new MapleRoleSessionProxy(MapleServerRole.Mts, MapleHandshakePolicy.GlobalV95);

            bool sent = proxy.TrySendToServer(new byte[] { 0x01, 0x00 }, out string status);

            Assert.False(sent);
            Assert.Contains("no active Maple session", status);
            Assert.Equal(0, proxy.SentCount);
        }

        [Fact]
        public void RaiseServerPacket_NonInit_DecodesOpcodeAndRaisesEvent()
        {
            using MapleRoleSessionProxy proxy = new MapleRoleSessionProxy(MapleServerRole.CashShop, MapleHandshakePolicy.GlobalV95);
            MapleSessionPacketEventArgs? observed = null;
            proxy.ServerPacketReceived += (_, e) => observed = e;

            InvokePrivateRaise(
                proxy,
                "RaiseServerPacket",
                "127.0.0.1:8484",
                new byte[] { 0x34, 0x12, 0xAA },
                isInit: false);

            Assert.NotNull(observed);
            Assert.Equal(MapleServerRole.CashShop, observed.Role);
            Assert.False(observed.IsInit);
            Assert.Equal(0x1234, observed.Opcode);
            Assert.Equal("127.0.0.1:8484", observed.SourceEndpoint);
        }

        [Fact]
        public void CentralTelemetry_RemainsOnRoleProxySurface()
        {
            using MapleRoleSessionProxy proxy = new MapleRoleSessionProxy(MapleServerRole.Channel, MapleHandshakePolicy.GlobalV95);

            Assert.Equal(0, proxy.ActiveSessionCount);
            Assert.Equal(0, proxy.ReceivedCount);
            Assert.Equal(0, proxy.ClientReceivedCount);
            Assert.Null(proxy.LastPacketUtc);
        }

        [Fact]
        public void RaiseClientPacket_Init_UsesInitOpcodeSentinel()
        {
            using MapleRoleSessionProxy proxy = new MapleRoleSessionProxy(MapleServerRole.Login, MapleHandshakePolicy.GlobalV95);
            MapleSessionPacketEventArgs? observed = null;
            proxy.ClientPacketReceived += (_, e) => observed = e;

            InvokePrivateRaise(
                proxy,
                "RaiseClientPacket",
                "127.0.0.1:8485",
                new byte[] { 0x01, 0x02, 0x03 },
                isInit: true);

            Assert.NotNull(observed);
            Assert.Equal(MapleServerRole.Login, observed.Role);
            Assert.True(observed.IsInit);
            Assert.Equal(-1, observed.Opcode);
        }

        private static void InvokePrivateRaise(
            MapleRoleSessionProxy proxy,
            string methodName,
            string sourceEndpoint,
            byte[] rawPacket,
            bool isInit)
        {
            MethodInfo? method = typeof(MapleRoleSessionProxy).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            method!.Invoke(proxy, new object[] { sourceEndpoint, rawPacket, isInit });
        }

        private static int ReserveLoopbackPort()
        {
            using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
    }
}
