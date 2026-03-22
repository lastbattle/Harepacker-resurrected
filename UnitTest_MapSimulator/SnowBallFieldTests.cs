using HaCreator.MapSimulator.Fields;
using Microsoft.Xna.Framework;

public sealed class SnowBallFieldTests
{
    [Fact]
    public void OnSnowBallState_FirstSnapshotSnapsToPacketPosition()
    {
        var field = CreateField();

        field.OnSnowBallState(
            newState: (int)SnowBallField.GameState.Active,
            team0SnowManHp: 7500,
            team1SnowManHp: 7500,
            team0Pos: 200,
            team0SpeedDegree: 2,
            team1Pos: 800,
            team1SpeedDegree: 3);

        Assert.Equal(SnowBallField.GameState.Active, field.State);
        Assert.Equal(200, field.SnowBalls[0].PositionX);
        Assert.Equal(800, field.SnowBalls[1].PositionX);
        Assert.Equal(2, field.SnowBalls[0].SpeedDegree);
        Assert.Equal(3, field.SnowBalls[1].SpeedDegree);
        Assert.Equal("The snowball fight has begun!", field.CurrentMessage);
    }

    [Fact]
    public void OnSnowBallState_SubsequentSnapshotSmoothsMovementInsteadOfTeleporting()
    {
        var field = CreateField();
        field.OnSnowBallState(1, 7500, 7500, 200, 2, 800, 2);

        field.OnSnowBallState(1, 7500, 7500, 210, 2, 790, 2);

        Assert.Equal(200, field.SnowBalls[0].PositionX);
        Assert.Equal(800, field.SnowBalls[1].PositionX);

        field.Update(30);
        Assert.Equal(200, field.SnowBalls[0].PositionX);
        Assert.Equal(800, field.SnowBalls[1].PositionX);

        field.Update(60);
        Assert.True(field.SnowBalls[0].PositionX > 200);
        Assert.True(field.SnowBalls[1].PositionX < 800);
    }

    [Fact]
    public void OnSnowBallMsg_UsesClientShapedTeamOrderingForBrokenSnowman()
    {
        var field = CreateField();

        field.OnSnowBallMsg(team: 0, msgType: 4);

        Assert.True(field.TryConsumeChatMessage(out string message));
        Assert.Equal("Maple's snowman was broken by Story.", message);
    }

    [Fact]
    public void OnSnowBallMsg_UsesPacketFallbackTextWhenPresent()
    {
        var field = CreateField();

        field.OnSnowBallMsg(msgType: 1, message: "Packet-owned text");

        Assert.True(field.TryConsumeChatMessage(out string message));
        Assert.Equal("Packet-owned text", message);
    }

    [Fact]
    public void OnSnowBallTouch_ClearsMatchingPendingTouchRequest()
    {
        var field = CreateField();
        field.SetLocalPlayerPosition(new Vector2(250f, 920f));

        field.Update(100);

        Assert.True(field.TryConsumeTouchPacketRequest(out SnowBallField.TouchPacketRequest request));
        Assert.Equal(0, request.Team);

        field.SetLocalPlayerPosition(new Vector2(250f, 920f));
        field.Update(130);
        field.OnSnowBallTouch(0);

        Assert.False(field.TryConsumeTouchPacketRequest(out _));
    }

    private static SnowBallField CreateField()
    {
        var field = new SnowBallField();
        field.Initialize(leftGoalX: 0, rightGoalX: 1000, groundY: 1000, snowBallRadius: 80);
        return field;
    }
}
