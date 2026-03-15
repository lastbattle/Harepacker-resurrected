using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Fields;

namespace UnitTest_MapSimulator;

public class EscortFollowControllerTests
{
    [Theory]
    [InlineData(80f, 30f, true)]
    [InlineData(81f, 30f, false)]
    [InlineData(80f, 31f, false)]
    public void UpdateEscortFollow_UsesClientAttachProximityWindow(float horizontalOffset, float verticalOffset, bool expected)
    {
        EscortFollowController controller = new EscortFollowController();
        FootholdLine sharedFoothold = CreateHorizontalFoothold(0, 100, 160);
        PlayerCharacter player = CreateGroundedPlayer(sharedFoothold, 100f, 100f);
        MobMovementInfo movement = CreateGroundMob(sharedFoothold, 100f + horizontalOffset, 100f + verticalOffset);

        bool attached = controller.UpdateEscortFollow(player, movement);

        Assert.Equal(expected, attached);
    }

    [Fact]
    public void UpdateEscortFollow_DoesNotAttachWhilePlayerIsAirborneBeforeFollowStarts()
    {
        EscortFollowController controller = new EscortFollowController();
        FootholdLine sharedFoothold = CreateHorizontalFoothold(0, 100, 160);
        PlayerCharacter player = CreateGroundedPlayer(sharedFoothold, 100f, 100f);
        player.Physics.CurrentFoothold = null;
        player.Physics.FallStartFoothold = sharedFoothold;

        MobMovementInfo movement = CreateGroundMob(sharedFoothold, 120f, 100f);

        bool attached = controller.UpdateEscortFollow(player, movement);

        Assert.False(attached);
    }

    [Fact]
    public void UpdateEscortFollow_KeepsAttachedEscortDuringShortHopWithinCarryWindow()
    {
        EscortFollowController controller = new EscortFollowController();
        FootholdLine sharedFoothold = CreateHorizontalFoothold(0, 100, 160);
        PlayerCharacter player = CreateGroundedPlayer(sharedFoothold, 100f, 100f);
        MobMovementInfo movement = CreateGroundMob(sharedFoothold, 120f, 100f);

        Assert.True(controller.UpdateEscortFollow(player, movement));

        player.SetPosition(118f, 84f);
        player.Physics.CurrentFoothold = null;
        player.Physics.FallStartFoothold = sharedFoothold;

        bool attached = controller.UpdateEscortFollow(player, movement);

        Assert.True(attached);
    }

    [Fact]
    public void UpdateEscortFollow_ReleasesAttachedEscortAfterCarryWindowExpires()
    {
        EscortFollowController controller = new EscortFollowController();
        FootholdLine sharedFoothold = CreateHorizontalFoothold(0, 100, 160);
        PlayerCharacter player = CreateGroundedPlayer(sharedFoothold, 100f, 100f);
        MobMovementInfo movement = CreateGroundMob(sharedFoothold, 120f, 100f);

        Assert.True(controller.UpdateEscortFollow(player, movement));

        player.SetPosition(190f, 84f);
        player.Physics.CurrentFoothold = null;
        player.Physics.FallStartFoothold = sharedFoothold;

        bool attached = controller.UpdateEscortFollow(player, movement);

        Assert.False(attached);
    }

    [Fact]
    public void UpdateEscortFollow_RejectsDisconnectedFootholdsEvenInsideProximityWindow()
    {
        EscortFollowController controller = new EscortFollowController();
        FootholdLine playerFoothold = CreateHorizontalFoothold(0, 100, 160);
        FootholdLine escortFoothold = CreateHorizontalFoothold(300, 100, 460);
        PlayerCharacter player = CreateGroundedPlayer(playerFoothold, 100f, 100f);
        MobMovementInfo movement = CreateGroundMob(escortFoothold, 120f, 100f);

        bool attached = controller.UpdateEscortFollow(player, movement);

        Assert.False(attached);
    }

    private static PlayerCharacter CreateGroundedPlayer(FootholdLine foothold, float x, float y)
    {
        PlayerCharacter player = new PlayerCharacter(null!, null!, null);
        player.SetPosition(x, y);
        player.Physics.CurrentFoothold = foothold;
        player.Physics.FallStartFoothold = null;
        return player;
    }

    private static MobMovementInfo CreateGroundMob(FootholdLine foothold, float x, float y)
    {
        return new MobMovementInfo
        {
            X = x,
            Y = y,
            CurrentFoothold = foothold,
            MoveType = MobMoveType.Move
        };
    }

    private static FootholdLine CreateHorizontalFoothold(int left, int y, int right)
    {
        FootholdAnchor first = new FootholdAnchor(null!, left, y, 0, 0, false);
        FootholdAnchor second = new FootholdAnchor(null!, right, y, 0, 0, false);
        return new FootholdLine(null!, first, second);
    }
}
