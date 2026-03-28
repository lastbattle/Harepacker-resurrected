using System.Reflection;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Fields;

namespace UnitTest_MapSimulator
{
    public sealed class EscortFollowControllerTests
    {
        [Fact]
        public void UpdateEscortFollow_BlocksInitialAttachWhileMounted()
        {
            EscortFollowController controller = new EscortFollowController();
            PlayerCharacter player = CreatePlayerCharacter();
            FootholdLine foothold = CreateFoothold(0, 0, 200, 0);
            MobMovementInfo movement = CreateMovement(40f, 0f, foothold);

            SetBuild(player, new CharacterBuild { HasMonsterRiding = true });
            SetPlayerGrounded(player, foothold, 0f, 0f);

            bool attached = controller.UpdateEscortFollow(player, movement);

            Assert.False(attached);
        }

        [Fact]
        public void UpdateEscortFollow_KeepsAttachedEscortWhileMovementLockStarts()
        {
            EscortFollowController controller = new EscortFollowController();
            PlayerCharacter player = CreatePlayerCharacter();
            FootholdLine foothold = CreateFoothold(0, 0, 200, 0);
            MobMovementInfo movement = CreateMovement(40f, 0f, foothold);

            SetPlayerGrounded(player, foothold, 0f, 0f);

            Assert.True(controller.UpdateEscortFollow(player, movement));

            bool attached = controller.UpdateEscortFollow(player, movement, movementLocked: true);

            Assert.True(attached);
        }

        [Fact]
        public void UpdateEscortFollow_KeepsAttachedEscortWhilePlayerSits()
        {
            EscortFollowController controller = new EscortFollowController();
            PlayerCharacter player = CreatePlayerCharacter();
            FootholdLine foothold = CreateFoothold(0, 0, 200, 0);
            MobMovementInfo movement = CreateMovement(40f, 0f, foothold);

            SetPlayerGrounded(player, foothold, 0f, 0f);

            Assert.True(controller.UpdateEscortFollow(player, movement));

            SetPlayerState(player, PlayerState.Sitting);
            bool attached = controller.UpdateEscortFollow(player, movement);

            Assert.True(attached);
        }

        [Fact]
        public void UpdateEscortFollow_KeepsAttachedEscortWhileMountedAfterAttach()
        {
            EscortFollowController controller = new EscortFollowController();
            PlayerCharacter player = CreatePlayerCharacter();
            FootholdLine foothold = CreateFoothold(0, 0, 200, 0);
            MobMovementInfo movement = CreateMovement(40f, 0f, foothold);

            SetPlayerGrounded(player, foothold, 0f, 0f);

            Assert.True(controller.UpdateEscortFollow(player, movement));

            SetBuild(player, new CharacterBuild { HasMonsterRiding = true });
            bool attached = controller.UpdateEscortFollow(player, movement);

            Assert.True(attached);
        }

        private static PlayerCharacter CreatePlayerCharacter()
        {
            return new PlayerCharacter(device: null, texturePool: null, build: null);
        }

        private static MobMovementInfo CreateMovement(float x, float y, FootholdLine foothold)
        {
            return new MobMovementInfo
            {
                X = x,
                Y = y,
                CurrentFoothold = foothold,
                MoveType = MobMoveType.Move
            };
        }

        private static FootholdLine CreateFoothold(int x1, int y1, int x2, int y2)
        {
            FootholdAnchor first = new FootholdAnchor(board: null, x1, y1, layer: 0, zm: 0, user: true);
            FootholdAnchor second = new FootholdAnchor(board: null, x2, y2, layer: 0, zm: 0, user: true);
            return new FootholdLine(board: null, first, second);
        }

        private static void SetPlayerGrounded(PlayerCharacter player, FootholdLine foothold, float x, float y)
        {
            player.SetPosition(x, y);
            player.Physics.CurrentFoothold = foothold;
            player.Physics.FallStartFoothold = null;
            player.Physics.IsOnLadderOrRope = false;
            player.Physics.IsFlyingMap = false;
            player.Physics.WingsActive = false;
            player.Physics.IsInSwimArea = false;
        }

        private static void SetPlayerState(PlayerCharacter player, PlayerState state)
        {
            typeof(PlayerCharacter)
                .GetProperty(nameof(PlayerCharacter.State), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .SetValue(player, state);
        }

        private static void SetBuild(PlayerCharacter player, CharacterBuild build)
        {
            typeof(PlayerCharacter)
                .GetProperty(nameof(PlayerCharacter.Build), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .SetValue(player, build);
        }
    }
}
