using System.Diagnostics;
using HaCreator.MapEditor;

namespace UnitTest_MapSimulator;

public class MultiBoardRenderPacingTests
{
    [Fact]
    public void StaticMapWaitsUntilItIsInvalidated()
    {
        int delayMilliseconds = MultiBoard.GetRenderWaitMilliseconds(
            deadlineTimestamp: long.MaxValue,
            currentTimestamp: 0);

        Assert.Equal(Timeout.Infinite, delayMilliseconds);
    }

    [Fact]
    public void AnimatedMapWaitsUntilItsDeadline()
    {
        long deadlineTimestamp = Stopwatch.Frequency / 30;

        int delayMilliseconds = MultiBoard.GetRenderWaitMilliseconds(
            deadlineTimestamp,
            currentTimestamp: 0);

        Assert.InRange(delayMilliseconds, 33, 34);
    }

    [Fact]
    public void MissedAnimationDeadlineStillYieldsToTheUi()
    {
        int delayMilliseconds = MultiBoard.GetRenderWaitMilliseconds(
            deadlineTimestamp: 0,
            currentTimestamp: Stopwatch.Frequency);

        Assert.Equal(1, delayMilliseconds);
    }
}
