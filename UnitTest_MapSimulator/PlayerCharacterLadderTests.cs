using HaCreator.MapSimulator.Character;

namespace UnitTest_MapSimulator;

public sealed class PlayerCharacterLadderTests
{
    [Fact]
    public void Update_GrabsLadderWhenStandingSlightlyBelowBottomWhileHoldingUp()
    {
        var player = new PlayerCharacter(device: null, texturePool: null, build: null);
        player.SetPosition(100f, 205f);
        player.SetLadderLookup((x, y, range) =>
            Math.Abs(x - 100f) <= range && y >= 100f && y <= 200f
                ? (100, 100, 200, true)
                : null);
        player.SetInput(left: false, right: false, up: true, down: false, jump: false, attack: false, pickup: false);

        player.Update(currentTime: 1000, deltaTime: 1f / 60f);

        Assert.Equal(PlayerState.Ladder, player.State);
        Assert.True(player.Physics.IsOnLadder());
        Assert.Equal(100, player.Physics.LadderX);
        Assert.Equal(100, player.Physics.LadderTop);
        Assert.Equal(200, player.Physics.LadderBottom);
    }
}
