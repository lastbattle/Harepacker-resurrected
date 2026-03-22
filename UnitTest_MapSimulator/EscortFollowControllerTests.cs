using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Fields;

namespace UnitTest_MapSimulator;

public sealed class EscortFollowControllerTests
{
    [Fact]
    public void UpdateEscortFollow_AttachesWithinVerifiedClientWindow()
    {
        FootholdLine foothold = CreateFoothold(0, 100, 260, 100);
        PlayerCharacter player = CreatePlayer(100f, 100f, foothold);
        MobMovementInfo movement = CreateGroundMob(170f, 118f, foothold);

        EscortFollowController controller = new EscortFollowController();

        Assert.True(controller.UpdateEscortFollow(player, movement));
    }

    [Fact]
    public void UpdateEscortFollow_DoesNotAttachWhileMounted()
    {
        CharacterBuild build = new CharacterBuild
        {
            HasMonsterRiding = true
        };
        FootholdLine foothold = CreateFoothold(0, 100, 260, 100);
        PlayerCharacter player = CreatePlayer(100f, 100f, foothold, build);
        MobMovementInfo movement = CreateGroundMob(170f, 118f, foothold);

        EscortFollowController controller = new EscortFollowController();

        Assert.False(controller.UpdateEscortFollow(player, movement));
    }

    [Fact]
    public void UpdateEscortFollow_DoesNotAttachWhileSittingInChair()
    {
        FootholdLine foothold = CreateFoothold(0, 100, 260, 100);
        PlayerCharacter player = CreatePlayer(100f, 100f, foothold);
        Assert.True(player.TryActivatePortableChair(new PortableChair
        {
            ItemId = 3010000,
            Name = "Test Chair"
        }));

        MobMovementInfo movement = CreateGroundMob(170f, 118f, foothold);
        EscortFollowController controller = new EscortFollowController();

        Assert.False(controller.UpdateEscortFollow(player, movement));
    }

    [Fact]
    public void UpdateEscortFollow_KeepsAttachedThroughShortAirborneCarryWindow()
    {
        FootholdLine playerFoothold = CreateFoothold(0, 100, 260, 100);
        PlayerCharacter player = CreatePlayer(100f, 100f, playerFoothold);
        MobMovementInfo movement = CreateGroundMob(170f, 118f, playerFoothold);
        EscortFollowController controller = new EscortFollowController();

        Assert.True(controller.UpdateEscortFollow(player, movement));

        player.SetPosition(120f, 82f);
        player.Physics.FallStartFoothold = playerFoothold;
        player.Physics.CurrentFoothold = null;

        Assert.True(controller.UpdateEscortFollow(player, movement));
    }

    [Fact]
    public void UpdateEscortFollow_DoesNotAttachWhenMapDisablesFollowCharacter()
    {
        FootholdLine foothold = CreateFoothold(0, 100, 260, 100);
        PlayerCharacter player = CreatePlayer(100f, 100f, foothold);
        MobMovementInfo movement = CreateGroundMob(170f, 118f, foothold);

        EscortFollowController controller = new EscortFollowController();

        Assert.False(controller.UpdateEscortFollow(player, movement, followAllowed: false));
    }

    private static PlayerCharacter CreatePlayer(float x, float y, FootholdLine foothold, CharacterBuild build = null)
    {
        build ??= new CharacterBuild();
        PlayerCharacter player = new PlayerCharacter(build);
        player.SetPosition(x, y);
        player.Physics.CurrentFoothold = foothold;
        return player;
    }

    private static MobMovementInfo CreateGroundMob(float x, float y, FootholdLine foothold)
    {
        return new MobMovementInfo
        {
            X = x,
            Y = y,
            MoveType = MobMoveType.Move,
            CurrentFoothold = foothold
        };
    }

    private static FootholdLine CreateFoothold(int x1, int y1, int x2, int y2)
    {
        FootholdAnchor first = new FootholdAnchor(null, x1, y1, 0, 0, false);
        FootholdAnchor second = new FootholdAnchor(null, x2, y2, 0, 0, false);
        return new FootholdLine(null, first, second);
    }
}
