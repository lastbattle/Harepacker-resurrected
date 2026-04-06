using HaCreator.MapSimulator.Managers;
using System.Net;
using System.Net.Sockets;

namespace UnitTest_MapSimulator
{
    public sealed class LocalUtilityOfficialSessionBridgeManagerTests
    {
        [Fact]
        public void TryQueueOutboundPacket_RecordsPendingBridgeInjectionState()
        {
            using var manager = new LocalUtilityOfficialSessionBridgeManager();

            bool queued = manager.TryQueueOutboundPacket(45, new byte[] { 0xFF, 0xFF }, out string status);

            Assert.True(queued);
            Assert.Equal(1, manager.PendingPacketCount);
            Assert.Equal(1, manager.QueuedCount);
            Assert.Equal(45, manager.LastQueuedOpcode);
            Assert.Equal("2D00FFFF", Convert.ToHexString(manager.LastQueuedRawPacket));
            Assert.Contains("Queued local utility outbound opcode 45", status);
            Assert.Contains("pending=1", manager.DescribeStatus());
            Assert.Contains("lastQueued=45[2D00FFFF]", manager.DescribeStatus());
        }

        [Fact]
        public void Start_PreservesQueuedOutboundPacketsUntilExplicitStop()
        {
            using var manager = new LocalUtilityOfficialSessionBridgeManager();
            manager.TryQueueOutboundPacket(45, new byte[] { 0xFF, 0xFF }, out _);

            int listenPort = ReserveFreeLoopbackPort();
            manager.Start(listenPort, "127.0.0.1", 8484);

            Assert.True(manager.IsRunning);
            Assert.Equal(1, manager.PendingPacketCount);

            manager.Stop();

            Assert.Equal(0, manager.PendingPacketCount);
            Assert.Equal(0, manager.QueuedCount);
            Assert.Equal(-1, manager.LastQueuedOpcode);
            Assert.Empty(manager.LastQueuedRawPacket);
        }

        private static int ReserveFreeLoopbackPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
