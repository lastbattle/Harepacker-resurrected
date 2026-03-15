using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Fields;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class EscortFollowControllerTests
    {
        [Fact]
        public void UpdateEscortFollow_AttachesWhenPlayerIsWithinClientWindowOnConnectedFootholds()
        {
            FootholdLine foothold = CreateFoothold(0, 0, 120, 0);
            var player = new PlayerCharacter(device: null, texturePool: null, build: null);
            player.SetPosition(40, 0);
            player.Physics.LandOnFoothold(foothold);

            var movement = new MobMovementInfo();
            movement.Initialize(x: 100, y: 0, rx0Shift: 50, rx1Shift: 50, yShift: 0, isFlyingMob: false);
            movement.CurrentFoothold = foothold;

            var controller = new EscortFollowController();

            Assert.True(controller.UpdateEscortFollow(player, movement));
        }

        [Fact]
        public void UpdateEscortFollow_DoesNotAttachWhenVerticalAlignmentFails()
        {
            FootholdLine foothold = CreateFoothold(0, 0, 120, 0);
            var player = new PlayerCharacter(device: null, texturePool: null, build: null);
            player.SetPosition(40, 0);
            player.Physics.LandOnFoothold(foothold);

            var movement = new MobMovementInfo();
            movement.Initialize(x: 70, y: 40, rx0Shift: 50, rx1Shift: 50, yShift: 0, isFlyingMob: false);
            movement.CurrentFoothold = foothold;

            var controller = new EscortFollowController();

            Assert.False(controller.UpdateEscortFollow(player, movement));
        }

        [Fact]
        public void UpdateEscortFollow_ReleasesWhenTraversalBreaksAfterAttach()
        {
            FootholdLine left = CreateFoothold(0, 0, 100, 0);
            FootholdLine right = CreateFoothold(100, 0, 220, 0);
            Connect(left, right);

            var player = new PlayerCharacter(device: null, texturePool: null, build: null);
            player.SetPosition(90, 0);
            player.Physics.LandOnFoothold(left);

            var movement = new MobMovementInfo();
            movement.Initialize(x: 110, y: 0, rx0Shift: 50, rx1Shift: 50, yShift: 0, isFlyingMob: false);
            movement.CurrentFoothold = right;

            var controller = new EscortFollowController();

            Assert.True(controller.UpdateEscortFollow(player, movement));

            FootholdLine isolated = CreateFoothold(300, 0, 400, 0);
            movement.CurrentFoothold = isolated;
            player.SetPosition(310, 0);

            Assert.False(controller.UpdateEscortFollow(player, movement));
        }

        [Fact]
        public void UpdateEscortFollow_StaysAttachedWhilePlayerIsAirborneFromSameFoothold()
        {
            FootholdLine foothold = CreateFoothold(0, 0, 180, 0);
            var player = new PlayerCharacter(device: null, texturePool: null, build: null);
            player.SetPosition(70, 0);
            player.Physics.LandOnFoothold(foothold);

            var movement = new MobMovementInfo();
            movement.Initialize(x: 110, y: 0, rx0Shift: 50, rx1Shift: 50, yShift: 0, isFlyingMob: false);
            movement.CurrentFoothold = foothold;

            var controller = new EscortFollowController();

            Assert.True(controller.UpdateEscortFollow(player, movement));

            player.Physics.Jump();

            Assert.Null(player.Physics.CurrentFoothold);
            Assert.Same(foothold, player.Physics.FallStartFoothold);
            Assert.True(controller.UpdateEscortFollow(player, movement));
        }

        [Fact]
        public void CanTraverseBetween_RejectsDisconnectedFootholds()
        {
            FootholdLine first = CreateFoothold(0, 0, 100, 0);
            FootholdLine second = CreateFoothold(140, 0, 240, 0);

            Assert.False(EscortFollowController.CanTraverseBetween(first, second));
        }

        private static FootholdLine CreateFoothold(int x1, int y1, int x2, int y2)
        {
            var first = new FootholdAnchor(board: null, x: x1, y: y1, layer: 0, zm: 0, user: true);
            var second = new FootholdAnchor(board: null, x: x2, y: y2, layer: 0, zm: 0, user: true);
            return new FootholdLine(board: null, first, second);
        }

        private static void Connect(FootholdLine left, FootholdLine right)
        {
            FootholdAnchor leftAnchor = (FootholdAnchor)left.SecondDot;
            FootholdAnchor rightAnchor = (FootholdAnchor)right.FirstDot;
            leftAnchor.connectedLines.Add(right);
            rightAnchor.connectedLines.Add(left);
        }
    }
}
