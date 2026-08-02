using HaCreator.MapSimulator.Managers;
using System.Net;
using System.Net.Sockets;

namespace UnitTest_MapSimulator
{
    public class OfficialSessionBridgeManagerTests
    {
        [Fact]
        public void Dojo_StartStop_TogglesRunningState()
        {
            using DojoOfficialSessionBridgeManager manager = new DojoOfficialSessionBridgeManager();
            int listenPort = ReserveLoopbackPort();

            bool started = manager.TryStart(listenPort, IPAddress.Loopback.ToString(), 8484, out string status);

            Assert.True(started, status);
            Assert.True(manager.IsRunning);
            Assert.Contains("listening", status);

            manager.Stop();

            Assert.False(manager.IsRunning);
            Assert.Contains("stopped", manager.LastStatus);
        }

        [Fact]
        public void Dojo_TryMapInboundPacket_UsesConfiguredMapping()
        {
            using DojoOfficialSessionBridgeManager manager = new DojoOfficialSessionBridgeManager();
            Assert.True(manager.TryConfigurePacketMapping(0x0201, 3, out string mappingStatus), mappingStatus);

            bool mapped = manager.TryMapInboundPacket(
                new byte[] { 0x01, 0x02, 0xAA, 0xBB },
                "unit-test",
                out DojoPacketInboxMessage message);

            Assert.True(mapped);
            Assert.NotNull(message);
            Assert.Equal(3, message.PacketType);
            Assert.Equal("unit-test", message.Source);
            Assert.Equal(new byte[] { 0xAA, 0xBB }, message.Payload);
        }

        [Fact]
        public void Transportation_StartStop_TogglesRunningState()
        {
            using TransportationOfficialSessionBridgeManager manager = new TransportationOfficialSessionBridgeManager();
            int listenPort = ReserveLoopbackPort();

            bool started = manager.TryStart(listenPort, IPAddress.Loopback.ToString(), 8484, out string status);

            Assert.True(started, status);
            Assert.True(manager.IsRunning);
            Assert.Contains("listening", status);

            manager.Stop();

            Assert.False(manager.IsRunning);
            Assert.Contains("stopped", manager.LastStatus);
        }

        [Fact]
        public void Transportation_QueueRawPacket_PersistsDeferredState()
        {
            using TransportationOfficialSessionBridgeManager manager = new TransportationOfficialSessionBridgeManager();
            byte[] rawPacket = new byte[] { 0x40, 0x00, 0x99 };

            bool queued = manager.TryQueueRawPacket(rawPacket, out string status);

            Assert.True(queued, status);
            Assert.Equal(1, manager.QueuedCount);
            Assert.Equal(1, manager.PendingPacketCount);
            Assert.Equal(64, manager.LastQueuedOpcode);
            Assert.Equal(rawPacket, manager.LastQueuedRawPacket);
            Assert.Contains("Queued outbound", status);
        }

        [Fact]
        public void Transportation_TrySendRawPacket_WithoutConnectedSession_Fails()
        {
            using TransportationOfficialSessionBridgeManager manager = new TransportationOfficialSessionBridgeManager();

            bool sent = manager.TrySendRawPacket(new byte[] { 0x41, 0x00 }, out string status);

            Assert.False(sent);
            Assert.Equal(0, manager.SentCount);
            Assert.Contains("no active Maple session", status);
        }

        [Fact]
        public void Summoned_StartStop_TogglesRunningState()
        {
            using SummonedOfficialSessionBridgeManager manager = new SummonedOfficialSessionBridgeManager();
            int listenPort = ReserveLoopbackPort();

            bool started = manager.TryStart(listenPort, IPAddress.Loopback.ToString(), 8484, out string status);

            Assert.True(started, status);
            Assert.True(manager.IsRunning);
            Assert.Contains("listening", status);

            manager.Stop();

            Assert.False(manager.IsRunning);
            Assert.Contains("stopped", manager.LastStatus);
        }

        [Fact]
        public void Summoned_QueueOutboundRawPacket_PersistsDeferredState()
        {
            using SummonedOfficialSessionBridgeManager manager = new SummonedOfficialSessionBridgeManager();
            byte[] rawPacket = new byte[] { 0x90, 0x00, 0x01 };

            bool queued = manager.TryQueueOutboundRawPacket(rawPacket, out string status);

            Assert.True(queued, status);
            Assert.Equal(1, manager.QueuedCount);
            Assert.Equal(1, manager.PendingPacketCount);
            Assert.Equal(144, manager.LastQueuedOpcode);
            Assert.Equal(rawPacket, manager.LastQueuedRawPacket);
            Assert.Contains("queued", status, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Summoned_TrySendOutboundRawPacket_WithoutConnectedSession_Fails()
        {
            using SummonedOfficialSessionBridgeManager manager = new SummonedOfficialSessionBridgeManager();

            bool sent = manager.TrySendOutboundRawPacket(new byte[] { 0x91, 0x00 }, out string status);

            Assert.False(sent);
            Assert.Equal(0, manager.SentCount);
            Assert.Contains("no connected Maple session", status);
        }

        private static int ReserveLoopbackPort()
        {
            using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
    }
}
