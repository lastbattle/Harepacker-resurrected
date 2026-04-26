using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator
{
    public class PacketTransportManagerTests
    {
        [Fact]
        public void LocalUtilityOutbox_StartStop_PreservesRetiredListenerState()
        {
            using LocalUtilityPacketTransportManager manager = new LocalUtilityPacketTransportManager();
            const int port = 18701;

            manager.Start(port);
            Assert.False(manager.IsRunning);
            Assert.Equal(port, manager.Port);
            Assert.Contains("loopback listener is retired", manager.LastStatus);

            manager.Stop();
            Assert.False(manager.IsRunning);
            Assert.Equal(port, manager.Port);
            Assert.Contains("already retired", manager.LastStatus);
        }

        [Fact]
        public void LocalUtilityOutbox_QueueOutboundPacket_TracksDeferredPacket()
        {
            using LocalUtilityPacketTransportManager manager = new LocalUtilityPacketTransportManager();

            bool queued = manager.TryQueueOutboundPacket(0x1234, new byte[] { 0xAB, 0xCD }, out string status);

            Assert.True(queued, status);
            Assert.Equal(1, manager.QueuedCount);
            Assert.Equal(1, manager.PendingPacketCount);
            Assert.Equal(0x1234, manager.LastQueuedOpcode);
            Assert.Equal(new byte[] { 0x34, 0x12, 0xAB, 0xCD }, manager.LastQueuedRawPacket);
            Assert.True(manager.HasQueuedOutboundPacket(0x1234, new byte[] { 0x34, 0x12, 0xAB, 0xCD }));
        }

        [Fact]
        public void LocalUtilityOutbox_TrySendOutboundPacket_WithoutClient_Fails()
        {
            using LocalUtilityPacketTransportManager manager = new LocalUtilityPacketTransportManager();

            bool sent = manager.TrySendOutboundPacket(0x1234, new byte[] { 0xAA }, out string status);

            Assert.False(sent);
            Assert.Equal(0, manager.SentCount);
            Assert.Contains("role-session bridge or local packet command path", status);
        }

        [Fact]
        public void GuildBossTransport_StartStop_PreservesRetiredListenerState()
        {
            using GuildBossPacketTransportManager manager = new GuildBossPacketTransportManager();
            const int port = 18702;

            manager.Start(port);
            Assert.False(manager.IsRunning);
            Assert.Equal(port, manager.Port);
            Assert.Contains("loopback listener is retired", manager.LastStatus);

            manager.Stop();
            Assert.False(manager.IsRunning);
            Assert.Equal(port, manager.Port);
            Assert.Contains("already retired", manager.LastStatus);
        }

        [Fact]
        public void GuildBossTransport_TryParsePacketLine_DirectOpcodePayload_Succeeds()
        {
            bool parsed = GuildBossPacketTransportManager.TryParsePacketLine(
                "healer 257",
                out int packetType,
                out byte[] payload,
                out string error);

            Assert.True(parsed, error);
            Assert.Equal(344, packetType);
            Assert.Equal(new byte[] { 0x01, 0x01 }, payload);
        }

        [Fact]
        public void GuildBossTransport_TrySendPulleyRequest_WithoutClient_Fails()
        {
            using GuildBossPacketTransportManager manager = new GuildBossPacketTransportManager();
            GuildBossField.PulleyPacketRequest request = new GuildBossField.PulleyPacketRequest(1000, 3);

            bool sent = manager.TrySendPulleyRequest(request, out string status);

            Assert.False(sent);
            Assert.Equal(0, manager.SentCount);
            Assert.Contains("role-session bridge or local packet command path", status);
        }
    }
}
