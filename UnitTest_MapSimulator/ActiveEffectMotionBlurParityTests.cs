using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Physics;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class ActiveEffectMotionBlurParityTests
    {
        [Fact]
        public void ShouldUsePassiveSnapshotForMotionBlurOwnerCapture_ReturnsFalseWhenSnapshotMissing()
        {
            bool shouldUse = MapSimulator.ShouldUsePacketOwnedActiveEffectMotionBlurPassiveSnapshotForTesting(
                passiveMoveSnapshot: null,
                sampleTime: 250);

            Assert.False(shouldUse);
        }

        [Fact]
        public void ShouldUsePassiveSnapshotForMotionBlurOwnerCapture_RemainsTrueAfterPassiveTimestamp()
        {
            PlayerMovementSyncSnapshot snapshot = CreateSnapshot(
                pathTimestamp: 100,
                pathX: 90,
                pathY: 40,
                pathFacingRight: false,
                passiveTimestamp: 200,
                passiveX: 120,
                passiveY: 50,
                passiveFacingRight: true);

            bool shouldUse = MapSimulator.ShouldUsePacketOwnedActiveEffectMotionBlurPassiveSnapshotForTesting(
                snapshot,
                sampleTime: 260);

            Assert.True(shouldUse);
        }

        [Fact]
        public void ResolvePacketOwnedMotionBlurOwnerSample_UsesPassiveSnapshotHistoryForLateSampleTime()
        {
            PlayerMovementSyncSnapshot snapshot = CreateSnapshot(
                pathTimestamp: 100,
                pathX: 90,
                pathY: 40,
                pathFacingRight: false,
                passiveTimestamp: 200,
                passiveX: 120,
                passiveY: 50,
                passiveFacingRight: true);

            (Vector2 position, bool facingRight) sample =
                MapSimulator.ResolvePacketOwnedActiveEffectMotionBlurSnapshotOwnerStateForTesting(
                    snapshot,
                    fallbackPosition: new Vector2(5f, 6f),
                    fallbackFacingRight: false,
                    sampleTime: 260);

            Assert.Equal(new Vector2(120f, 50f), sample.position);
            Assert.True(sample.facingRight);
        }

        [Fact]
        public void ResolvePacketOwnedMotionBlurOwnerSample_FallsBackToActorStateWhenMovementSnapshotMissing()
        {
            (Vector2 position, bool facingRight) sample =
                MapSimulator.ResolvePacketOwnedActiveEffectMotionBlurSnapshotOwnerStateForTesting(
                    movementSnapshot: null,
                    fallbackPosition: new Vector2(33f, 77f),
                    fallbackFacingRight: true,
                    sampleTime: 500);

            Assert.Equal(new Vector2(33f, 77f), sample.position);
            Assert.True(sample.facingRight);
        }

        private static PlayerMovementSyncSnapshot CreateSnapshot(
            int pathTimestamp,
            int pathX,
            int pathY,
            bool pathFacingRight,
            int passiveTimestamp,
            int passiveX,
            int passiveY,
            bool passiveFacingRight)
        {
            var path = new List<MovePathElement>
            {
                new()
                {
                    TimeStamp = pathTimestamp,
                    X = pathX,
                    Y = pathY,
                    FacingRight = pathFacingRight,
                    Duration = 50
                }
            };

            var passive = new PassivePositionSnapshot
            {
                TimeStamp = passiveTimestamp,
                X = passiveX,
                Y = passiveY,
                FacingRight = passiveFacingRight
            };

            return new PlayerMovementSyncSnapshot(passive, path);
        }
    }
}
