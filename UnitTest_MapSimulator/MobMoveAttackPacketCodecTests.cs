using System.IO;
using HaCreator.MapSimulator.Interaction;
using MapleLib.PacketLib;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator
{
    public sealed class MobMoveAttackPacketCodecTests
    {
        [Fact]
        public void TryDecode_DecodesMoveHeaderFlagsAndAttackOverrides()
        {
            byte[] payload = BuildPayload(
                mobId: 321,
                notForceLandingWhenDiscard: true,
                notChangeAction: false,
                nextAttackPossible: true,
                moveActionByte: (byte)((14 << 1) | 1),
                targetInfoRaw: (777 << 2) | 3,
                multiTargetForBall: new[] { new Point(10, 20), new Point(30, 40) },
                randTimeForAreaAttack: new[] { 120, 240 });

            bool decoded = MobMoveAttackPacketCodec.TryDecode(287, payload, out var packet, out string error);

            Assert.True(decoded, error);
            Assert.NotNull(packet);
            Assert.Equal(321, packet.MobId);
            Assert.True(packet.NotForceLandingWhenDiscard);
            Assert.False(packet.NotChangeAction);
            Assert.True(packet.NextAttackPossible);
            Assert.False(packet.FacingLeft);
            Assert.Equal(14, packet.MoveAction);
            Assert.Equal(2, packet.AttackId);
            Assert.Equal(2, packet.MultiTargetForBall.Count);
            Assert.Equal(new Point(10, 20), packet.MultiTargetForBall[0]);
            Assert.Equal(new Point(30, 40), packet.MultiTargetForBall[1]);
            Assert.Equal(new[] { 120, 240 }, packet.RandTimeForAreaAttack);
            Assert.NotNull(packet.LockedTargetInfo);
            Assert.Equal(MobTargetType.Mob, packet.LockedTargetInfo.Value.TargetType);
            Assert.Equal(777, packet.LockedTargetInfo.Value.EncodedEntityId);
        }

        [Fact]
        public void ShouldQueueSimulatorAttackOverrides_FalseWhenNotChangeActionSuppressesAttack()
        {
            byte[] payload = BuildPayload(
                mobId: 55,
                notForceLandingWhenDiscard: false,
                notChangeAction: true,
                nextAttackPossible: true,
                moveActionByte: (byte)(13 << 1),
                targetInfoRaw: 0,
                multiTargetForBall: null,
                randTimeForAreaAttack: null);

            bool decoded = MobMoveAttackPacketCodec.TryDecode(287, payload, out var packet, out string error);

            Assert.True(decoded, error);
            Assert.NotNull(packet);
            Assert.False(MobMoveAttackPacketCodec.ShouldQueueSimulatorAttackOverrides(packet));
        }

        [Fact]
        public void ShouldQueueSimulatorAttackOverrides_TrueForExecutableAttackHeader()
        {
            byte[] payload = BuildPayload(
                mobId: 55,
                notForceLandingWhenDiscard: false,
                notChangeAction: false,
                nextAttackPossible: true,
                moveActionByte: (byte)(13 << 1),
                targetInfoRaw: (123 << 2),
                multiTargetForBall: null,
                randTimeForAreaAttack: null);

            bool decoded = MobMoveAttackPacketCodec.TryDecode(287, payload, out var packet, out string error);

            Assert.True(decoded, error);
            Assert.NotNull(packet);
            Assert.True(MobMoveAttackPacketCodec.ShouldQueueSimulatorAttackOverrides(packet));
        }

        private static byte[] BuildPayload(
            int mobId,
            bool notForceLandingWhenDiscard,
            bool notChangeAction,
            bool nextAttackPossible,
            byte moveActionByte,
            int targetInfoRaw,
            IReadOnlyList<Point>? multiTargetForBall,
            IReadOnlyList<int>? randTimeForAreaAttack)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(mobId);
            writer.Write((byte)(notForceLandingWhenDiscard ? 1 : 0));
            writer.Write((byte)(notChangeAction ? 1 : 0));
            writer.Write((byte)(nextAttackPossible ? 1 : 0));
            writer.Write(moveActionByte);
            writer.Write(targetInfoRaw);

            writer.Write(multiTargetForBall?.Count ?? 0);
            if (multiTargetForBall != null)
            {
                foreach (Point point in multiTargetForBall)
                {
                    writer.Write(point.X);
                    writer.Write(point.Y);
                }
            }

            writer.Write(randTimeForAreaAttack?.Count ?? 0);
            if (randTimeForAreaAttack != null)
            {
                foreach (int value in randTimeForAreaAttack)
                {
                    writer.Write(value);
                }
            }

            writer.Flush();
            return stream.ToArray();
        }
    }
}
