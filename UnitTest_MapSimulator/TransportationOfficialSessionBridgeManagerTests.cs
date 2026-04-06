using HaCreator.MapSimulator.Managers;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace UnitTest_MapSimulator
{
    public sealed class TransportationOfficialSessionBridgeManagerTests
    {
        [Fact]
        public void TryDecodeInboundTransportPacket_DecodesOnContiMovePayload()
        {
            byte[] rawPacket =
            {
                0xA4, 0x00,
                TransportationPacketInboxManager.ContiMoveStartShip,
                0x02
            };

            bool decoded = TransportationOfficialSessionBridgeManager.TryDecodeInboundTransportPacket(
                rawPacket,
                "test-source",
                out TransportationPacketInboxMessage? message);

            Assert.True(decoded);
            Assert.NotNull(message);
            Assert.Equal(TransportationPacketInboxManager.PacketTypeContiMove, message!.PacketType);
            Assert.Equal(new byte[] { TransportationPacketInboxManager.ContiMoveStartShip, 0x02 }, message.Payload);
            Assert.Equal("test-source", message.Source);
        }

        [Fact]
        public void TryDecodeInboundTransportPacket_IgnoresNonTransportOpcode()
        {
            byte[] rawPacket = { 0x01, 0x00, 0x02, 0x03 };

            bool decoded = TransportationOfficialSessionBridgeManager.TryDecodeInboundTransportPacket(
                rawPacket,
                "test-source",
                out TransportationPacketInboxMessage? message);

            Assert.False(decoded);
            Assert.Null(message);
        }

        [Fact]
        public void TryResolveDiscoveryCandidate_SelectsMatchingLocalPort()
        {
            var candidates = new[]
            {
                new TransportationOfficialSessionBridgeManager.SessionDiscoveryCandidate(
                    100,
                    "MapleStory",
                    new IPEndPoint(IPAddress.Loopback, 51234),
                    new IPEndPoint(IPAddress.Parse("203.0.113.10"), 8484)),
                new TransportationOfficialSessionBridgeManager.SessionDiscoveryCandidate(
                    100,
                    "MapleStory",
                    new IPEndPoint(IPAddress.Loopback, 51235),
                    new IPEndPoint(IPAddress.Parse("203.0.113.10"), 8484))
            };

            bool resolved = TransportationOfficialSessionBridgeManager.TryResolveDiscoveryCandidate(
                candidates,
                8484,
                owningProcessId: null,
                owningProcessName: null,
                localPort: 51235,
                out TransportationOfficialSessionBridgeManager.SessionDiscoveryCandidate candidate,
                out string? status);

            Assert.True(resolved);
            Assert.Null(status);
            Assert.Equal(51235, candidate.LocalEndpoint.Port);
        }

        [Fact]
        public void TryResolveDiscoveryCandidate_ReturnsAmbiguousStatusWithoutLocalPortFilter()
        {
            var candidates = new[]
            {
                new TransportationOfficialSessionBridgeManager.SessionDiscoveryCandidate(
                    100,
                    "MapleStory",
                    new IPEndPoint(IPAddress.Loopback, 51234),
                    new IPEndPoint(IPAddress.Parse("203.0.113.10"), 8484)),
                new TransportationOfficialSessionBridgeManager.SessionDiscoveryCandidate(
                    101,
                    "MapleStory",
                    new IPEndPoint(IPAddress.Loopback, 51235),
                    new IPEndPoint(IPAddress.Parse("203.0.113.11"), 8484))
            };

            bool resolved = TransportationOfficialSessionBridgeManager.TryResolveDiscoveryCandidate(
                candidates,
                8484,
                owningProcessId: null,
                owningProcessName: null,
                localPort: null,
                out _,
                out string? status);

            Assert.False(resolved);
            Assert.NotNull(status);
            Assert.Contains("multiple candidates", status, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Specify a local port filter", status, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void DescribeRecentOutboundPackets_ListsNewestEntriesFirst_AndCanClearHistory()
        {
            using var manager = new TransportationOfficialSessionBridgeManager();

            RecordOutboundPacket(manager, new TransportationOfficialSessionBridgeManager.OutboundPacketTrace(
                TransportationPacketInboxManager.PacketTypeContiMove,
                2,
                "0802",
                "client-a"));
            RecordOutboundPacket(manager, new TransportationOfficialSessionBridgeManager.OutboundPacketTrace(
                TransportationPacketInboxManager.PacketTypeContiState,
                2,
                "0301",
                "client-b"));

            string history = manager.DescribeRecentOutboundPackets();

            string[] lines = history.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("Transport official-session bridge outbound history:", lines[0]);
            Assert.Contains("opcode=165", lines[1], StringComparison.Ordinal);
            Assert.Contains("source=client-b", lines[1], StringComparison.Ordinal);
            Assert.Contains("opcode=164", lines[2], StringComparison.Ordinal);
            Assert.Contains("source=client-a", lines[2], StringComparison.Ordinal);

            string clearStatus = manager.ClearRecentOutboundPackets();

            Assert.Contains("cleared", clearStatus, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Transport official-session bridge outbound history is empty.", manager.DescribeRecentOutboundPackets());
        }

        [Fact]
        public void TryStart_ReturnsFailure_WhenListenPortIsAlreadyOccupied()
        {
            TcpListener occupiedListener = new(IPAddress.Loopback, 0);
            occupiedListener.Start();

            try
            {
                int occupiedPort = ((IPEndPoint)occupiedListener.LocalEndpoint).Port;
                using var manager = new TransportationOfficialSessionBridgeManager();

                bool started = manager.TryStart(occupiedPort, "127.0.0.1", 8484, out string status);

                Assert.False(started);
                Assert.Contains("failed to start", status, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                occupiedListener.Stop();
            }
        }

        private static void RecordOutboundPacket(
            TransportationOfficialSessionBridgeManager manager,
            TransportationOfficialSessionBridgeManager.OutboundPacketTrace trace)
        {
            MethodInfo? recordMethod = typeof(TransportationOfficialSessionBridgeManager).GetMethod(
                "RecordOutboundPacket",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(recordMethod);
            recordMethod!.Invoke(manager, new object[] { trace });
        }
    }
}
