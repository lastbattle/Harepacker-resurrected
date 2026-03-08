using System;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Physics;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data;
using Moq;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class PlayerMovementAndBackgroundTests
    {
        [Fact]
        public void JumpOffLadder_ClearsLadderStateAndSetsJumpMotion()
        {
            var physics = new CVecCtrl();
            physics.GrabLadder(120, 80, 240, true);
            physics.IsJumpingDown = true;

            physics.JumpOffLadder(130, -180);

            Assert.False(physics.IsOnLadderOrRope);
            Assert.Null(physics.CurrentFoothold);
            Assert.False(physics.IsJumpingDown);
            Assert.Equal(130, physics.VelocityX);
            Assert.Equal(-180, physics.VelocityY);
            Assert.Equal(JumpState.Jumping, physics.CurrentJumpState);
            Assert.Equal(MoveAction.Jump, physics.CurrentAction);
        }

        [Fact]
        public void VerticalMovingHVTiling_AppliesVerticalShiftBeforeDrawing()
        {
            var firstDrawY = int.MinValue;
            var capturedFirstDraw = false;

            var frame = new Mock<IDXObject>();
            frame.SetupGet(x => x.Delay).Returns(100);
            frame.SetupGet(x => x.X).Returns(0);
            frame.SetupGet(x => x.Y).Returns(0);
            frame.SetupGet(x => x.Width).Returns(32);
            frame.SetupGet(x => x.Height).Returns(32);
            frame.SetupProperty(x => x.Tag);
            frame.Setup(x => x.DrawBackground(null, null, null, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Microsoft.Xna.Framework.Color>(), false, null))
                .Callback<object, object, object, int, int, Microsoft.Xna.Framework.Color, bool, object>((_, _, _, _, y, _, _, _) =>
                {
                    if (!capturedFirstDraw)
                    {
                        firstDrawY = y;
                        capturedFirstDraw = true;
                    }
                });

            var background = new BackgroundItem(
                _cx: 300,
                _cy: 300,
                _rx: 0,
                _ry: 100,
                _type: BackgroundType.VerticalMovingHVTiling,
                a: 255,
                front: false,
                frame0: frame.Object,
                flip: false,
                screenMode: (int)RenderResolution.Res_All);

            var renderParameters = new RenderParameters(100, 100, 1f, RenderResolution.Res_All);
            int baseY = background.CalculateBackgroundPosY(frame.Object, 0, 0, renderParameters.RenderHeight, renderParameters.RenderObjectScaling);
            int tickCount = Environment.TickCount + 200;

            background.Draw(null, null, null, 0, 0, 0, 0, null, renderParameters, tickCount);

            Assert.True(capturedFirstDraw);
            Assert.True(firstDrawY > baseY + 30, $"Expected moving background to advance before draw. BaseY={baseY}, DrawY={firstDrawY}");
        }
    }
}
