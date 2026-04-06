using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator
{
    public sealed class PacketReactorPoolRuntimeTests
    {
        [Fact]
        public void TryApplyPacket_DecodesEnterFieldPayload()
        {
            PacketReactorPoolRuntime runtime = new();
            PacketReactorEnterFieldPacket decoded = default;
            byte[] payload = PacketReactorPoolRuntime.BuildEnterFieldPayload(41, 1002009, 2, 610, 254, true, "door");

            bool applied = runtime.TryApplyPacket(
                PacketReactorPoolPacketKind.EnterField,
                payload,
                currentTick: 1234,
                new PacketReactorPoolCallbacks
                {
                    EnterField = (packet, _) =>
                    {
                        decoded = packet;
                        return new PacketReactorPoolApplyResult(true, "enter");
                    }
                },
                out string message);

            Assert.True(applied);
            Assert.Equal("enter", message);
            Assert.Equal(41, decoded.ObjectId);
            Assert.Equal("1002009", decoded.ReactorTemplateId);
            Assert.Equal(2, decoded.InitialState);
            Assert.Equal(610, decoded.X);
            Assert.Equal(254, decoded.Y);
            Assert.True(decoded.Flip);
            Assert.Equal("door", decoded.Name);
        }

        [Fact]
        public void TryApplyPacket_DecodesChangeStatePayload()
        {
            PacketReactorPoolRuntime runtime = new();
            PacketReactorChangeStatePacket decoded = default;
            byte[] payload = PacketReactorPoolRuntime.BuildChangeStatePayload(52, 3, 77, 88, 450, 6, 9);

            bool applied = runtime.TryApplyPacket(
                PacketReactorPoolPacketKind.ChangeState,
                payload,
                currentTick: 5678,
                new PacketReactorPoolCallbacks
                {
                    ChangeState = (packet, _) =>
                    {
                        decoded = packet;
                        return new PacketReactorPoolApplyResult(true, "change");
                    }
                },
                out string message);

            Assert.True(applied);
            Assert.Equal("change", message);
            Assert.Equal(52, decoded.ObjectId);
            Assert.Equal(3, decoded.State);
            Assert.Equal(77, decoded.X);
            Assert.Equal(88, decoded.Y);
            Assert.Equal(450, decoded.HitStartDelayMs);
            Assert.Equal(6, decoded.ProperEventIndex);
            Assert.Equal(9, decoded.StateEndDelayTicks);
        }

        [Fact]
        public void TryApplyPacket_RejectsTrailingBytesOnMovePayload()
        {
            PacketReactorPoolRuntime runtime = new();
            byte[] payload = PacketReactorPoolRuntime.BuildMovePayload(99, 10, 20)
                .Concat(new byte[] { 0xAB })
                .ToArray();

            bool applied = runtime.TryApplyPacket(
                PacketReactorPoolPacketKind.Move,
                payload,
                currentTick: 1,
                new PacketReactorPoolCallbacks
                {
                    Move = (packet, _) => new PacketReactorPoolApplyResult(true, packet.ToString())
                },
                out string message);

            Assert.False(applied);
            Assert.Contains("trailing byte", message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void TryDecodeOpcodeFramedPacket_DecodesSupportedClientOpcode()
        {
            byte[] payload = PacketReactorPoolRuntime.BuildEnterFieldPayload(5, 1002009, 1, 30, 40, false, string.Empty);
            byte[] raw = new byte[sizeof(ushort) + payload.Length];
            BitConverter.GetBytes((ushort)PacketReactorPoolPacketKind.EnterField).CopyTo(raw, 0);
            payload.CopyTo(raw, sizeof(ushort));

            bool decoded = ReactorPoolPacketInboxManager.TryDecodeOpcodeFramedPacket(raw, out int packetType, out byte[] framedPayload, out string error);

            Assert.True(decoded);
            Assert.Null(error);
            Assert.Equal((int)PacketReactorPoolPacketKind.EnterField, packetType);
            Assert.Equal(payload, framedPayload);
        }

        [Fact]
        public void TryCreateBridgeMessageFromRawPacket_RejectsUnsupportedOpcode()
        {
            byte[] raw = new byte[sizeof(ushort) + 1];
            BitConverter.GetBytes((ushort)333).CopyTo(raw, 0);
            raw[^1] = 0x01;

            bool decoded = ReactorPoolOfficialSessionBridgeManager.TryCreateBridgeMessageFromRawPacket(raw, "test", out ReactorPoolPacketInboxMessage message, out string error);

            Assert.False(decoded);
            Assert.Null(message);
            Assert.Contains("Unsupported reactor", error, StringComparison.OrdinalIgnoreCase);
        }
    }
}
