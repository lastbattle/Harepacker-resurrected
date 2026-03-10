using System.Collections.Generic;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.Entities;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class MobMovementKnockbackTests
    {
        private static FootholdLine CreateFoothold(int x1, int y1, int x2, int y2)
        {
            var first = new FootholdAnchor(board: null, x: x1, y: y1, layer: 0, zm: 0, user: true);
            var second = new FootholdAnchor(board: null, x: x2, y: y2, layer: 0, zm: 0, user: true);
            return new FootholdLine(board: null, first, second);
        }

        [Fact]
        public void GroundKnockback_StaysOnCurrentPlatform()
        {
            var upperPlatform = CreateFoothold(0, 100, 120, 100);
            var lowerPlatform = CreateFoothold(60, 140, 260, 140);
            var footholds = new List<FootholdLine> { upperPlatform, lowerPlatform };

            var movement = new MobMovementInfo();
            movement.Initialize(
                x: 90,
                y: 100,
                rx0Shift: 90,
                rx1Shift: 170,
                yShift: 0,
                isFlyingMob: false,
                isJumpingMob: false);
            movement.FindCurrentFoothold(footholds);

            movement.ApplyKnockback(12f, knockbackRight: true);

            for (int i = 0; i < 40; i++)
            {
                movement.UpdateMovement(16);
            }

            Assert.Same(upperPlatform, movement.CurrentFoothold);
            Assert.InRange(movement.Y, 99f, 101f);
            Assert.True(movement.X <= 120f, $"Expected grounded knockback to stay on the original platform, but X={movement.X}");
        }
    }
}
