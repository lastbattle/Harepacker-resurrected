using HaCreator.MapSimulator.Fields;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public sealed class SnowBallFieldTests
{
    private static SnowBallField CreateField()
    {
        SnowBallField field = new();
        field.Initialize(leftGoalX: 0, rightGoalX: 400, groundY: 200, snowBallRadius: 40);
        field.StartGame();
        return field;
    }

    [Fact]
    public void Update_LocalPlayerInsideSnowBallArea_QueuesTouchPacketRequest()
    {
        SnowBallField field = CreateField();
        field.SetLocalPlayerPosition(new Vector2(field.SnowBalls[0].PositionX, field.SnowBalls[0].PositionY));

        field.Update(1000);

        Assert.True(field.TryConsumeTouchPacketRequest(out SnowBallField.TouchPacketRequest request));
        Assert.Equal(0, request.Team);
        Assert.Equal(1000, request.TickCount);
        Assert.Equal(1, request.Sequence);
    }

    [Fact]
    public void Update_RepeatedTouchingAdvancesTouchPacketSequence()
    {
        SnowBallField field = CreateField();
        field.SetLocalPlayerPosition(new Vector2(field.SnowBalls[1].PositionX, field.SnowBalls[1].PositionY));

        field.Update(1000);
        Assert.True(field.TryConsumeTouchPacketRequest(out SnowBallField.TouchPacketRequest first));

        field.SetLocalPlayerPosition(new Vector2(field.SnowBalls[1].PositionX, field.SnowBalls[1].PositionY));
        field.Update(1120);

        Assert.True(field.TryConsumeTouchPacketRequest(out SnowBallField.TouchPacketRequest second));
        Assert.Equal(1, first.Team);
        Assert.Equal(1, second.Team);
        Assert.Equal(1, first.Sequence);
        Assert.Equal(2, second.Sequence);
        Assert.Equal(1120, second.TickCount);
    }

    [Fact]
    public void Update_WinningLaneTouch_DoesNotQueueTouchPacketRequest()
    {
        SnowBallField field = CreateField();
        field.OnSnowBallState((int)SnowBallField.GameState.Team0Win, 120, 280);
        field.SetLocalPlayerPosition(new Vector2(field.SnowBalls[0].PositionX, field.SnowBalls[0].PositionY));

        field.Update(1400);

        Assert.False(field.TryConsumeTouchPacketRequest(out _));
    }
}
