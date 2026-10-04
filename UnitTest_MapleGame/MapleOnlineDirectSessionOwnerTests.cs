using System.Net;
using HaCreator.MapSimulator.Managers;
using MapleLib.PacketLib;

namespace UnitTest_MapleGame
{
    /// <summary>
    /// Behavior tests for the single stage/opcode owner path (P03): role
    /// routing, game-thread queueing, and the native retire-before-dial
    /// migration contract (N04).
    /// </summary>
    public sealed class MapleOnlineDirectSessionOwnerTests
    {
        [Fact]
        public async Task Owner_RoutesDirectSessionPackets_ToRegisteredStageHandler()
        {
            using MapleTestFakeServer server = MapleTestFakeServer.Start(95);
            using var owner = new MapleOnlineDirectSessionOwner();
            var loginHandler = new RecordingStageHandler(MapleServerRole.Login);
            owner.RegisterStageHandler(loginHandler);

            await owner.ConnectAsync(MapleServerRole.Login, IPAddress.Loopback.ToString(), server.Port);
            await server.SendPacketAsync(new byte[] { 0x05, 0x00, 0x01 });

            MapleOnlineInboundPacket packet = await WaitUntilAsync(
                () =>
                {
                    owner.DrainPendingInbound();
                    return loginHandler.Received.FirstOrDefault();
                },
                received => received != null);

            Assert.Equal(MapleServerRole.Login, packet.Role);
            Assert.Equal(0x0005, packet.Opcode);
            Assert.Equal(owner.CurrentGeneration(MapleServerRole.Login), packet.Generation);
        }

        [Fact]
        public async Task Owner_DoesNotDeliverOtherRolePackets_ToLoginHandler()
        {
            using MapleTestFakeServer channelServer = MapleTestFakeServer.Start(95);
            using var owner = new MapleOnlineDirectSessionOwner();
            var loginHandler = new RecordingStageHandler(MapleServerRole.Login);
            var channelHandler = new RecordingStageHandler(MapleServerRole.Channel);
            owner.RegisterStageHandler(loginHandler);
            owner.RegisterStageHandler(channelHandler);

            await owner.ConnectAsync(MapleServerRole.Channel, IPAddress.Loopback.ToString(), channelServer.Port);
            await channelServer.SendPacketAsync(new byte[] { 0x64, 0x00 });

            await WaitUntilAsync(
                () =>
                {
                    owner.DrainPendingInbound();
                    return channelHandler.Received.Count;
                },
                count => count > 0);

            Assert.Empty(loginHandler.Received);
        }

        [Fact]
        public async Task Owner_Migration_RetiresLoginGeneration_BeforeChannelBecomesAuthoritative()
        {
            using MapleTestFakeServer loginServer = MapleTestFakeServer.Start(95);
            using MapleTestFakeServer channelServer = MapleTestFakeServer.Start(95);
            using var owner = new MapleOnlineDirectSessionOwner();
            owner.RegisterStageHandler(new RecordingStageHandler(MapleServerRole.Login));
            owner.RegisterStageHandler(new RecordingStageHandler(MapleServerRole.Channel));

            await owner.ConnectAsync(MapleServerRole.Login, IPAddress.Loopback.ToString(), loginServer.Port);
            long? loginGeneration = owner.CurrentGeneration(MapleServerRole.Login);
            Assert.NotNull(loginGeneration);

            long channelGeneration = await owner.MigrateAsync(
                MapleServerRole.Channel, IPAddress.Loopback, (ushort)channelServer.Port);

            Assert.True(channelGeneration > loginGeneration.Value);
            Assert.Null(owner.CurrentGeneration(MapleServerRole.Login));
            Assert.Equal(channelGeneration, owner.CurrentGeneration(MapleServerRole.Channel));
        }

        private static async Task<T> WaitUntilAsync<T>(Func<T> probe, Func<T, bool> done)
        {
            DateTime deadline = DateTime.UtcNow + MapleTestFakeServer.TestTimeout;
            while (DateTime.UtcNow < deadline)
            {
                T value = probe();
                if (done(value))
                    return value;
                await Task.Delay(25);
            }

            throw new TimeoutException("Condition was not met within the test timeout.");
        }

        private sealed class RecordingStageHandler : IMapleOnlineStageHandler
        {
            public RecordingStageHandler(MapleServerRole role)
            {
                Role = role;
            }

            public MapleServerRole Role { get; }
            public List<MapleOnlineInboundPacket> Received { get; } = new();

            public void HandleInboundPacket(MapleOnlineInboundPacket packet)
            {
                lock (Received)
                    Received.Add(packet);
            }

            public bool TrySendOutboundPacket(byte[] payload, out string status)
            {
                status = "not exercised by this test";
                return true;
            }
        }
    }
}
