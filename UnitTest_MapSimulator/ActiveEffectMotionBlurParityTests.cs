using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Physics;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace UnitTest_MapSimulator
{
    public sealed class ActiveEffectMotionBlurParityTests
    {
        [Fact]
        public void PassiveSnapshotGate_UsesPassiveHistoryOnlyWithinTimestampWindow()
        {
            var passiveSnapshot = new PlayerMovementSyncSnapshot(
                passivePosition: new PassivePositionSnapshot
                {
                    X = 100,
                    Y = 200,
                    TimeStamp = 1000,
                    FacingRight = true,
                    Action = MoveAction.Stand
                },
                movePath: new List<MovePathElement>());

            Assert.True(MapSimulator.ShouldUsePacketOwnedActiveEffectMotionBlurPassiveSnapshotForTesting(
                passiveSnapshot,
                sampleTime: 1000));
            Assert.True(MapSimulator.ShouldUsePacketOwnedActiveEffectMotionBlurPassiveSnapshotForTesting(
                passiveSnapshot,
                sampleTime: 900));
            Assert.False(MapSimulator.ShouldUsePacketOwnedActiveEffectMotionBlurPassiveSnapshotForTesting(
                passiveSnapshot,
                sampleTime: 1001));
        }

        [Fact]
        public void SnapshotOwnerResolution_UsesMovementSnapshotSampleWhenAvailable()
        {
            var movementSnapshot = new PlayerMovementSyncSnapshot(
                passivePosition: new PassivePositionSnapshot
                {
                    X = 200,
                    Y = 300,
                    TimeStamp = 1000,
                    FacingRight = true,
                    Action = MoveAction.Stand
                },
                movePath: new List<MovePathElement>
                {
                    new()
                    {
                        X = 100,
                        Y = 200,
                        TimeStamp = 900,
                        Duration = 100,
                        FacingRight = false,
                        Action = MoveAction.Walk
                    },
                    new()
                    {
                        X = 200,
                        Y = 300,
                        TimeStamp = 1000,
                        Duration = 100,
                        FacingRight = true,
                        Action = MoveAction.Stand
                    }
                });

            (Vector2 position, bool facingRight) sample =
                MapSimulator.ResolvePacketOwnedActiveEffectMotionBlurSnapshotOwnerStateForTesting(
                    movementSnapshot,
                    fallbackPosition: new Vector2(999f, 999f),
                    fallbackFacingRight: true,
                    sampleTime: 950);

            Assert.Equal(new Vector2(150f, 250f), sample.position);
            Assert.False(sample.facingRight);
        }

        [Fact]
        public void SnapshotOwnerResolution_FallsBackToCurrentActorStateWhenNoSnapshotAvailable()
        {
            (Vector2 position, bool facingRight) sample =
                MapSimulator.ResolvePacketOwnedActiveEffectMotionBlurSnapshotOwnerStateForTesting(
                    movementSnapshot: null,
                    fallbackPosition: new Vector2(444f, 555f),
                    fallbackFacingRight: false,
                    sampleTime: 1234);

            Assert.Equal(new Vector2(444f, 555f), sample.position);
            Assert.False(sample.facingRight);
        }
    }
}
