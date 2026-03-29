using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator
{
    public class TransportationPacketInboxManagerTests
    {
        [Fact]
        public void TryParsePacketLine_StartAlias_BuildsContiMovePayload()
        {
            bool parsed = TransportationPacketInboxManager.TryParsePacketLine("start 2", out int packetType, out byte[] payload, out string error);

            Assert.True(parsed, error);
            Assert.Equal(TransportationPacketInboxManager.PacketTypeContiMove, packetType);
            Assert.Equal(new byte[] { TransportationPacketInboxManager.ContiMoveStartShip, 2 }, payload);
        }

        [Fact]
        public void TryParsePacketLine_StateAlias_BuildsContiStatePayload()
        {
            bool parsed = TransportationPacketInboxManager.TryParsePacketLine("state 4 1", out int packetType, out byte[] payload, out string error);

            Assert.True(parsed, error);
            Assert.Equal(TransportationPacketInboxManager.PacketTypeContiState, packetType);
            Assert.Equal(new byte[] { 4, 1 }, payload);
        }

        [Fact]
        public void TryParsePacketLine_RawPacket_DecodesOpcodeAndPayload()
        {
            bool parsed = TransportationPacketInboxManager.TryParsePacketLine("packetraw a4000c06", out int packetType, out byte[] payload, out string error);

            Assert.True(parsed, error);
            Assert.Equal(TransportationPacketInboxManager.PacketTypeContiMove, packetType);
            Assert.Equal(new byte[] { TransportationPacketInboxManager.ContiMoveEndShip, 6 }, payload);
        }

        [Fact]
        public void TryParsePacketLine_UnsupportedOpcode_Fails()
        {
            bool parsed = TransportationPacketInboxManager.TryParsePacketLine("packetraw a6000102", out int packetType, out byte[] payload, out string error);

            Assert.False(parsed);
            Assert.Equal(0, packetType);
            Assert.Empty(payload);
            Assert.Contains("Unsupported transport raw packet opcode", error);
        }

        [Fact]
        public void TransportationField_TryApplyContiStateForBalrog_AppearsOnlyWhenSecondByteIsOne()
        {
            var field = new TransportationField();
            field.Initialize(shipKind: 1, x: 120, y: 45, x0: 20, f: 0, tMove: 5, shipPath: "Map/Obj/vehicle.img/ship");

            bool applied = field.TryApplyContiState(4, 1, out string message);

            Assert.True(applied, message);
            Assert.Equal(ShipState.Appearing, field.State);
            Assert.Equal(0f, field.ShipAlpha);
        }

        [Fact]
        public void TransportationField_TryApplyStartShipMovePacket_LeavesDockOnClientValue()
        {
            var field = new TransportationField();
            field.Initialize(shipKind: 0, x: 320, y: 90, x0: -480, f: 0, tMove: 8, shipPath: "Map/Obj/vehicle.img/ship");
            field.ApplyClientOwnedDefaultState(null);

            bool applied = field.TryApplyStartShipMovePacket(2, out string message);

            Assert.True(applied, message);
            Assert.Equal(ShipState.Moving, field.State);
            Assert.Equal(320f, field.ShipX);
        }
    }
}
