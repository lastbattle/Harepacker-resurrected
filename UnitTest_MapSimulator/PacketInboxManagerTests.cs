using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Fields;
using System.Net;
using System.Net.Sockets;

namespace UnitTest_MapSimulator
{
    public class PacketInboxManagerTests
    {
        [Fact]
        public void AdminShopInbox_EnqueueLocal_QueuesMessage()
        {
            using AdminShopPacketInboxManager manager = new AdminShopPacketInboxManager();

            manager.EnqueueLocal(AdminShopPacketInboxManager.ResultClientPacketType, new byte[] { 0x01 }, "unit-test");

            Assert.True(manager.TryDequeue(out AdminShopPacketInboxMessage message));
            Assert.Equal(AdminShopPacketInboxManager.ResultClientPacketType, message.PacketType);
            Assert.Equal(new byte[] { 0x01 }, message.Payload);
            Assert.Equal("unit-test", message.Source);
        }

        [Fact]
        public void AdminShopInbox_TryParseLine_ResultAlias_Succeeds()
        {
            bool parsed = AdminShopPacketInboxManager.TryParseLine(
                "result",
                out AdminShopPacketInboxMessage message,
                out string error);

            Assert.True(parsed, error);
            Assert.NotNull(message);
            Assert.Equal(AdminShopPacketInboxManager.ResultClientPacketType, message.PacketType);
        }

        [Fact]
        public void AriantInbox_EnqueueLocal_QueuesMessage()
        {
            using AriantArenaPacketInboxManager manager = new AriantArenaPacketInboxManager();

            manager.EnqueueLocal(179, new byte[] { 0xA1, 0xB2 }, "ariant-command");

            Assert.True(manager.TryDequeue(out AriantArenaPacketInboxMessage message));
            Assert.Equal(179, message.PacketType);
            Assert.Equal(new byte[] { 0xA1, 0xB2 }, message.Payload);
            Assert.Equal("ariant-command", message.Source);
            Assert.Contains("Queued userenter (179) from ariant-command", manager.LastStatus);
        }

        [Fact]
        public void AriantInbox_TryParsePacketLine_DirectPacket_Succeeds()
        {
            bool parsed = AriantArenaPacketInboxManager.TryParsePacketLine(
                "179 A1B2",
                out int packetType,
                out byte[] payload,
                out string error);

            Assert.True(parsed, error);
            Assert.Equal(179, packetType);
            Assert.Equal(new byte[] { 0xA1, 0xB2 }, payload);
        }

        [Fact]
        public void DojoInbox_EnqueueProxy_QueuesMessage()
        {
            using DojoPacketInboxManager manager = new DojoPacketInboxManager();
            DojoPacketInboxMessage message = new DojoPacketInboxMessage(
                DojoPacketMessageKind.Clock,
                60,
                string.Empty,
                "dojo-session-proxy",
                "clock 60");

            manager.EnqueueProxy(message);

            Assert.True(manager.TryDequeue(out DojoPacketInboxMessage queuedMessage));
            Assert.Same(message, queuedMessage);
            Assert.Contains("Queued Dojo clock 60s from dojo-session-proxy", manager.LastStatus);
        }

        [Fact]
        public void TransportationInbox_EnqueueLocal_QueuesMessage()
        {
            using TransportationPacketInboxManager manager = new TransportationPacketInboxManager();
            TransportationPacketInboxMessage message = new TransportationPacketInboxMessage(
                TransportationPacketInboxManager.PacketTypeContiMove,
                new[] { TransportationPacketInboxManager.ContiMoveStartShip, (byte)1 },
                "transport-command",
                "start 1");

            manager.EnqueueLocal(message);

            Assert.True(manager.TryDequeue(out TransportationPacketInboxMessage queuedMessage));
            Assert.Same(message, queuedMessage);
            Assert.Contains("Queued Transport OnContiMove start", manager.LastStatus);
            Assert.Contains("from transport-command", manager.LastStatus);
        }

        [Fact]
        public void TransportationField_VoyageBalrogCommand_RecordsTransportOwner()
        {
            TransportationField field = new TransportationField();
            field.Initialize(
                shipKind: 0,
                x: 1545,
                y: -195,
                x0: 2100,
                f: 0,
                tMove: 15,
                shipPath: "Map/Obj/vehicle.img/ship/ossyria/99");
            field.LeaveShipMove();

            bool applied = field.TryStartVoyageBalrogAttack(5000, out string message);

            Assert.True(applied, message);
            Assert.Equal("transport-voyagebalrog-command", field.LastVoyageBalrogEventOwner);
            Assert.True(field.HasActiveVoyageBalrogAttack);
            Assert.Contains("voyageBalrogOwner=transport-voyagebalrog-command", field.DescribeStatus());
        }

        [Fact]
        public void TransportationField_VoyageBalrogAuto_RecordsWzRouteOwner()
        {
            TransportationField field = new TransportationField();
            field.Initialize(
                shipKind: 0,
                x: 1545,
                y: -195,
                x0: 2100,
                f: 0,
                tMove: 15,
                shipPath: "Map/Obj/vehicle/ship/ossyria/99");
            field.LeaveShipMove();

            int triggerTime = Environment.TickCount + field.VoyageBalrogAutoTriggerOffsetMs + 1;
            field.Update(triggerTime, 0f);

            Assert.True(field.VoyageBalrogAutoTriggered);
            Assert.Equal("wz-route-auto", field.LastVoyageBalrogEventOwner);
            Assert.True(field.HasActiveVoyageBalrogAttack);
            Assert.Contains("voyageBalrogOwner=wz-route-auto", field.DescribeStatus());
        }

        [Fact]
        public void MemoryGameInbox_EnqueueLocal_QueuesMessage()
        {
            using MemoryGamePacketInboxManager manager = new MemoryGamePacketInboxManager();

            manager.EnqueueLocal(new byte[] { 0x01, 0x02 }, "memorygame-command");

            Assert.True(manager.TryDequeue(out MemoryGamePacketInboxMessage queuedMessage));
            Assert.Equal(new byte[] { 0x01, 0x02 }, queuedMessage.Payload);
            Assert.Equal("memorygame-command", queuedMessage.Source);
            Assert.Contains("Queued MiniRoom payload from memorygame-command", manager.LastStatus);
        }

        [Fact]
        public void MassacreInbox_EnqueueProxy_QueuesMessage()
        {
            using MassacrePacketInboxManager manager = new MassacrePacketInboxManager();
            MassacrePacketInboxMessage message = new MassacrePacketInboxMessage(
                MassacrePacketInboxMessageKind.Stage,
                "massacre-session-proxy",
                "stage 2",
                value1: 2);

            manager.EnqueueProxy(message);

            Assert.True(manager.TryDequeue(out MassacrePacketInboxMessage queuedMessage));
            Assert.Same(message, queuedMessage);
            Assert.Contains("Queued Massacre stage 2 from massacre-session-proxy", manager.LastStatus);
        }

        [Fact]
        public void StageTransitionInbox_EnqueueLocal_QueuesMessage()
        {
            using StageTransitionPacketInboxManager manager = new StageTransitionPacketInboxManager();

            manager.EnqueueLocal(143, Array.Empty<byte>(), "unit-test");

            Assert.True(manager.TryDequeue(out StageTransitionPacketInboxMessage message));
            Assert.Equal(143, message.PacketType);
            Assert.Empty(message.Payload);
            Assert.Equal("unit-test", message.Source);
        }

        [Fact]
        public void StageTransitionInbox_TryParsePacketType_CashShopAlias_Succeeds()
        {
            bool parsed = StageTransitionPacketInboxManager.TryParsePacketType("cashshop", out int packetType);

            Assert.True(parsed);
            Assert.Equal(143, packetType);
        }

        private static int ReserveLoopbackPort()
        {
            using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
    }
}
