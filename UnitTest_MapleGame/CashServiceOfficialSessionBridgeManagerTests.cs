using HaCreator.MapSimulator.Managers;
using MapleLib.PacketLib;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace UnitTest_MapSimulator
{
    public class CashServiceOfficialSessionBridgeManagerTests
    {
        [Fact]
        public void CashShopBridge_StartStop_TogglesRunningState()
        {
            using CashServiceOfficialSessionBridgeManager manager = new CashServiceOfficialSessionBridgeManager(MapleServerRole.CashShop);
            int listenPort = ReserveLoopbackPort();

            bool started = manager.TryStart(listenPort, IPAddress.Loopback.ToString(), 8600, out string status);

            Assert.True(started, status);
            Assert.True(manager.IsRunning);
            Assert.Contains("Cash Shop official-session bridge listening", status);

            manager.Stop();

            Assert.False(manager.IsRunning);
            Assert.Contains("stopped", manager.LastStatus);
        }

        [Fact]
        public void MtsBridge_StartStop_TogglesRunningState()
        {
            using CashServiceOfficialSessionBridgeManager manager = new CashServiceOfficialSessionBridgeManager(MapleServerRole.Mts);
            int listenPort = ReserveLoopbackPort();

            bool started = manager.TryStart(listenPort, IPAddress.Loopback.ToString(), 8700, out string status);

            Assert.True(started, status);
            Assert.True(manager.IsRunning);
            Assert.Contains("MTS official-session bridge listening", status);

            manager.Stop();

            Assert.False(manager.IsRunning);
            Assert.Contains("stopped", manager.LastStatus);
        }

        [Fact]
        public void CashShopBridge_StartWithAutoSelectedListenPort_ReportsActualBoundPort()
        {
            using CashServiceOfficialSessionBridgeManager manager = new CashServiceOfficialSessionBridgeManager(MapleServerRole.CashShop);

            bool started = manager.TryStart(0, IPAddress.Loopback.ToString(), 8600, out string status);

            Assert.True(started, status);
            Assert.True(manager.IsRunning);
            Assert.True(manager.ListenPort > 0);
            Assert.Contains($"127.0.0.1:{manager.ListenPort}", status);
            Assert.Contains($"127.0.0.1:{manager.ListenPort}", manager.DescribeStatus());

            manager.Stop();
        }

        [Fact]
        public void CashShopBridge_ServerPacket_QueuesCashShopOpcode()
        {
            using CashServiceOfficialSessionBridgeManager manager = new CashServiceOfficialSessionBridgeManager(MapleServerRole.CashShop);

            InvokeServerPacket(manager, MapleServerRole.CashShop, 383, new byte[] { 0x34, 0x12 });

            Assert.True(manager.TryDequeue(out CashServicePacketInboxMessage message));
            Assert.NotNull(message);
            Assert.Equal(383, message.PacketType);
            Assert.Equal(new byte[] { 0x34, 0x12 }, message.Payload);
            Assert.Contains("official-session", message.Source);
        }

        [Fact]
        public void MtsBridge_ServerPacket_IgnoresCashShopOpcode()
        {
            using CashServiceOfficialSessionBridgeManager manager = new CashServiceOfficialSessionBridgeManager(MapleServerRole.Mts);

            InvokeServerPacket(manager, MapleServerRole.Mts, 383, new byte[] { 0x34, 0x12 });

            Assert.False(manager.TryDequeue(out _));
        }

        [Fact]
        public void MtsBridge_ServerPacket_QueuesItcOpcode()
        {
            using CashServiceOfficialSessionBridgeManager manager = new CashServiceOfficialSessionBridgeManager(MapleServerRole.Mts);

            InvokeServerPacket(manager, MapleServerRole.Mts, 411, new byte[] { 0x78, 0x56 });

            Assert.True(manager.TryDequeue(out CashServicePacketInboxMessage message));
            Assert.NotNull(message);
            Assert.Equal(411, message.PacketType);
            Assert.Equal(new byte[] { 0x78, 0x56 }, message.Payload);
            Assert.Contains("official-session", message.Source);
        }

        [Fact]
        public void CashShopBridge_ServerPacket_IgnoresItcOpcode()
        {
            using CashServiceOfficialSessionBridgeManager manager = new CashServiceOfficialSessionBridgeManager(MapleServerRole.CashShop);

            InvokeServerPacket(manager, MapleServerRole.CashShop, 411, new byte[] { 0x78, 0x56 });

            Assert.False(manager.TryDequeue(out _));
        }

        [Fact]
        public void CashShopBridge_ServerPacket_AddsRecentHistory()
        {
            using CashServiceOfficialSessionBridgeManager manager = new CashServiceOfficialSessionBridgeManager(MapleServerRole.CashShop);

            InvokeServerPacket(manager, MapleServerRole.CashShop, 383, new byte[] { 0x34, 0x12 });

            string history = manager.DescribeRecentPackets();
            Assert.Contains("QueryCash", history);
            Assert.Contains("payload=2 byte(s)", history);
            Assert.Contains("raw=7F013412", history);
        }

        [Fact]
        public void CashShopBridge_ClearRecentPackets_ResetsRecentHistory()
        {
            using CashServiceOfficialSessionBridgeManager manager = new CashServiceOfficialSessionBridgeManager(MapleServerRole.CashShop);

            InvokeServerPacket(manager, MapleServerRole.CashShop, 383, new byte[] { 0x34, 0x12 });
            manager.ClearRecentPackets();

            Assert.Contains("no recent mirrored packets", manager.DescribeRecentPackets());
            Assert.Contains("cleared recent packet history", manager.LastStatus);
        }

        private static void InvokeServerPacket(
            CashServiceOfficialSessionBridgeManager manager,
            MapleServerRole role,
            ushort opcode,
            byte[] payload)
        {
            MethodInfo? handler = typeof(CashServiceOfficialSessionBridgeManager).GetMethod(
                "OnRoleSessionServerPacketReceived",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(handler);

            byte[] rawPacket = new byte[sizeof(ushort) + (payload?.Length ?? 0)];
            BitConverter.GetBytes(opcode).CopyTo(rawPacket, 0);
            if (payload?.Length > 0)
            {
                payload.CopyTo(rawPacket, sizeof(ushort));
            }

            MapleSessionPacketEventArgs args = new MapleSessionPacketEventArgs(
                role,
                "unit-test:cash-service",
                rawPacket,
                isInit: false,
                opcode: opcode);
            handler!.Invoke(manager, new object[] { manager, args });
        }

        private static int ReserveLoopbackPort()
        {
            using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
    }
}
