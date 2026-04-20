using HaCreator.MapSimulator.Entities;

namespace UnitTest_MapSimulator
{
    public class MobMoveInterruptParityTests
    {
        [Fact]
        public void ApplyPacketMoveInterrupt_StoresLiveSnapshot_AndClearsPendingSteering()
        {
            var movementInfo = new MobMovementInfo
            {
                MoveType = MobMoveType.Move,
                MoveDirection = MobMoveDirection.Left,
                CurrentAction = MobAction.Move,
                JumpState = MobJumpState.Jumping,
                X = 120f,
                Y = 240f,
                VelocityX = 14f,
                VelocityY = -7f,
                FlipX = false
            };

            movementInfo.ForceDirection(MobMoveDirection.Right, currentFrameIndex: 0, frameCount: 4);
            movementInfo.ApplyPacketMoveInterrupt(notForceLandingWhenDiscard: true);

            MobPacketMoveInterruptSnapshot snapshot = Assert.IsType<MobPacketMoveInterruptSnapshot>(
                movementInfo.LastPacketMoveInterruptSnapshot);
            Assert.Equal(120f, snapshot.X);
            Assert.Equal(240f, snapshot.Y);
            Assert.Equal(14f, snapshot.VelocityX);
            Assert.Equal(-7f, snapshot.VelocityY);
            Assert.Equal(MobMoveType.Move, snapshot.MoveType);
            Assert.Equal(MobJumpState.Jumping, snapshot.JumpState);
            Assert.Equal(MobAction.Move, snapshot.CurrentAction);
            Assert.False(snapshot.FacingRight);

            for (int i = 0; i < 6; i++)
            {
                movementInfo.UpdatePendingDirection(currentFrameIndex: i, frameCount: 4);
            }

            Assert.Equal(MobMoveDirection.Left, movementInfo.MoveDirection);
            Assert.Equal(14f, movementInfo.VelocityX);
        }

        [Fact]
        public void ApplyPacketMoveInterrupt_WhenForcedLandingRequestedButNoFoothold_DoesNotZeroHorizontalVelocity()
        {
            var movementInfo = new MobMovementInfo
            {
                MoveType = MobMoveType.Move,
                MoveDirection = MobMoveDirection.Right,
                CurrentAction = MobAction.Jump,
                JumpState = MobJumpState.Falling,
                X = 300f,
                Y = 120f,
                VelocityX = 22f,
                VelocityY = 9f,
                FlipX = true
            };

            movementInfo.ApplyPacketMoveInterrupt(notForceLandingWhenDiscard: false);

            Assert.Equal(22f, movementInfo.VelocityX);
            Assert.Equal(9f, movementInfo.VelocityY);
            Assert.Equal(MobJumpState.Falling, movementInfo.JumpState);
            Assert.Equal(MobAction.Jump, movementInfo.CurrentAction);
            Assert.NotNull(movementInfo.LastPacketMoveInterruptSnapshot);
        }
    }
}
