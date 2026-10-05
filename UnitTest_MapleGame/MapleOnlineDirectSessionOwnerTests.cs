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
        public async Task Owner_SendsPostConnectHwidPacket_AfterHandshake()
        {
            using MapleTestFakeServer server = MapleTestFakeServer.Start(95);
            using var owner = new MapleOnlineDirectSessionOwner();

            await owner.ConnectAsync(MapleServerRole.Login, IPAddress.Loopback.ToString(), server.Port);
            byte[] body = await server.ReceivePacketAsync();

            // N05: opcode 0x1A + u16 length + blob; the default empty blob is
            // the native case for an absent hwid file.
            Assert.Equal(new byte[] { 0x1A, 0x00, 0x00, 0x00 }, body);
        }

        [Fact]
        public async Task Owner_DeliversOutboundPacketThroughActiveSession()
        {
            using MapleTestFakeServer server = MapleTestFakeServer.Start(95);
            using var owner = new MapleOnlineDirectSessionOwner();

            await owner.ConnectAsync(MapleServerRole.Login, IPAddress.Loopback.ToString(), server.Port);
            byte[] hwid = await server.ReceivePacketAsync();
            Assert.Equal(new byte[] { 0x1A, 0x00, 0x00, 0x00 }, hwid);

            byte[] payload = { 0x99, 0x12, 0x34 };
            Assert.True(owner.TrySendPacket(MapleServerRole.Login, payload, out string error));
            Assert.True(string.IsNullOrEmpty(error), error);

            byte[] delivered = await server.ReceivePacketAsync();
            Assert.Equal(payload, delivered);
        }

        [Fact]
        public void LoginBridge_AcceptsChannelRoleInbound_OnlyWhileDirectMigrationInFlight()
        {
            using var bridge = new LoginOfficialSessionBridgeManager();
            var handler = (IMapleOnlineStageHandler)bridge;
            bridge.TryConfigurePacketMapping(141, LoginPacketType.SetField, out _);
            var setFieldPacket = new MapleOnlineInboundPacket(
                MapleServerRole.Channel, 7, 141, new byte[] { 0x8D, 0x00, 0x01 }, "test", DateTime.UtcNow);

            handler.HandleInboundPacket(setFieldPacket);
            Assert.False(bridge.TryDequeue(out _));

            bridge.DirectChannelInboundEnabled = true;
            handler.HandleInboundPacket(setFieldPacket);
            Assert.True(bridge.TryDequeue(out LoginPacketInboxMessage message));
            Assert.Equal(LoginPacketType.SetField, message.PacketType);
        }

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
        public async Task Owner_FansOutChannelRolePackets_ToHandlersOptingIntoChannel()
        {
            using MapleTestFakeServer channelServer = MapleTestFakeServer.Start(95);
            using var owner = new MapleOnlineDirectSessionOwner();
            var loginHandler = new RecordingStageHandler(MapleServerRole.Login);
            loginHandler.AcceptAdditionalRole(MapleServerRole.Channel);
            owner.RegisterStageHandler(loginHandler);

            await owner.ConnectAsync(MapleServerRole.Channel, IPAddress.Loopback.ToString(), channelServer.Port);
            await channelServer.SendPacketAsync(new byte[] { 0x8D, 0x00, 0x02 });

            MapleOnlineInboundPacket packet = await WaitUntilAsync(
                () =>
                {
                    owner.DrainPendingInbound();
                    return loginHandler.Received.FirstOrDefault();
                },
                received => received != null);

            Assert.Equal(MapleServerRole.Channel, packet.Role);
            Assert.Equal(141, packet.Opcode);
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

        [Fact]
        public async Task Owner_TracesIngressAndEgressPacketDirection()
        {
            using MapleTestFakeServer server = MapleTestFakeServer.Start(95);
            using var owner = new MapleOnlineDirectSessionOwner();
            owner.RegisterStageHandler(new RecordingStageHandler(MapleServerRole.Login));

            await owner.ConnectAsync(MapleServerRole.Login, IPAddress.Loopback.ToString(), server.Port);
            await server.SendPacketAsync(new byte[] { 0x05, 0x00, 0x01 });
            await WaitUntilAsync(
                () => owner.DescribeStatus(),
                status => status.Contains("pending inbound=1"));
            owner.DrainPendingInbound();
            Assert.True(owner.TrySendPacket(MapleServerRole.Login, new byte[] { 0x42, 0x00, 0xAB }, out _));

            string trace = owner.DescribeTrace();
            Assert.Contains("inbound opcode 0x0005", trace);
            Assert.Contains("outbound opcode 0x0042", trace);
        }

        [Fact]
        public async Task Owner_Migration_DropsIngressQueuedForRetiredGeneration()
        {
            using MapleTestFakeServer loginServer = MapleTestFakeServer.Start(95);
            using MapleTestFakeServer channelServer = MapleTestFakeServer.Start(95);
            using var owner = new MapleOnlineDirectSessionOwner();
            var loginHandler = new RecordingStageHandler(MapleServerRole.Login);
            var channelHandler = new RecordingStageHandler(MapleServerRole.Channel);
            owner.RegisterStageHandler(loginHandler);
            owner.RegisterStageHandler(channelHandler);
            await owner.ConnectAsync(MapleServerRole.Login, IPAddress.Loopback.ToString(), loginServer.Port);
            await loginServer.SendPacketAsync(new byte[] { 0x05, 0x00, 0x01 });
            await WaitUntilAsync(
                () => owner.DescribeStatus(),
                status => status.Contains("pending inbound=1"));
            await owner.MigrateAsync(MapleServerRole.Channel, IPAddress.Loopback, (ushort)channelServer.Port);
            await channelServer.SendPacketAsync(new byte[] { 0x64, 0x00, 0x02 });
            MapleOnlineInboundPacket channelPacket = null;
            DateTime receivedDeadline = DateTime.UtcNow + MapleTestFakeServer.TestTimeout;
            while (channelPacket == null && DateTime.UtcNow < receivedDeadline)
            {
                owner.DrainPendingInbound();
                channelPacket = channelHandler.Received.FirstOrDefault();
                if (channelPacket == null)
                {
                    await Task.Delay(25);
                }
            }
            Assert.NotNull(channelPacket);
            Assert.Empty(loginHandler.Received);
            Assert.Contains("Rejected stale Login opcode 5 generation", owner.DescribeTrace(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task Owner_Dispose_ClosesActiveSessionAndReportsExpectedRetirement()
        {
            using MapleTestFakeServer server = MapleTestFakeServer.Start(95);
            var owner = new MapleOnlineDirectSessionOwner();
            TaskCompletionSource<bool> retired = new(TaskCreationOptions.RunContinuationsAsynchronously);
            owner.TraceRecorded += (_, entry) => OnOwnerDisposeTrace(entry, retired);
            await owner.ConnectAsync(MapleServerRole.Login, IPAddress.Loopback.ToString(), server.Port);
            owner.Dispose();
            bool retiredByOwner = await retired.Task.WaitAsync(MapleTestFakeServer.TestTimeout);
            Assert.True(retiredByOwner);
            Assert.False(owner.IsRoleConnected(MapleServerRole.Login));
            Assert.Null(owner.CurrentGeneration(MapleServerRole.Login));
        }

        private static void OnOwnerDisposeTrace(string entry, TaskCompletionSource<bool> retired)
        {
            if (entry.Contains("retired by owner dispose"))
                retired.TrySetResult(true);
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
            private readonly HashSet<MapleServerRole> _extraAcceptedRoles = new();

            public RecordingStageHandler(MapleServerRole role)
            {
                Role = role;
            }

            public MapleServerRole Role { get; }
            public List<MapleOnlineInboundPacket> Received { get; } = new();

            public void AcceptAdditionalRole(MapleServerRole role) => _extraAcceptedRoles.Add(role);

            public bool AcceptsPacketRole(MapleServerRole packetRole) =>
                packetRole == Role || _extraAcceptedRoles.Contains(packetRole);

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
