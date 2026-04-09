using HaCreator.MapSimulator.Interaction;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public sealed class PacketScriptQuizOwnerLayoutTests
{
    [Fact]
    public void ResolvePreviewBounds_AnchorsOwnerPreviewToTopRightWithinViewport()
    {
        Rectangle result = PacketScriptQuizOwnerLayout.ResolvePreviewBounds(
            renderWidth: 1366,
            renderHeight: 768,
            panelWidth: 265,
            panelHeight: 422,
            overlayTop: 86);

        Assert.Equal(1110, result.X);
        Assert.Equal(86, result.Y);
        Assert.Equal(238, result.Width);
        Assert.Equal(380, result.Height);
    }

    [Fact]
    public void ResolveStackedPreviewBounds_PlacesSecondOwnerBelowFirstWhenSpaceAllows()
    {
        Rectangle upper = new(1110, 86, 238, 380);

        Rectangle result = PacketScriptQuizOwnerLayout.ResolveStackedPreviewBounds(
            upper,
            panelWidth: 266,
            panelHeight: 224,
            renderWidth: 1366,
            renderHeight: 768);

        Assert.True(result.Y >= upper.Bottom + 14);
        Assert.Equal(1110, result.X);
    }

    [Fact]
    public void AnchorRect_ScalesSourceCoordinatesIntoPreviewBounds()
    {
        Rectangle preview = new(1110, 86, 238, 380);

        Rectangle result = PacketScriptQuizOwnerLayout.AnchorRect(
            preview,
            sourceX: 18,
            sourceY: 122,
            sourceWidth: 76,
            sourceHeight: 18,
            sourcePanelWidth: 265,
            sourcePanelHeight: 422);

        Assert.Equal(new Rectangle(1126, 196, 68, 16), result);
    }

    [Fact]
    public void TryBuildOwnerSnapshot_UsesRoundedUpRemainingSecondsForInitialQuiz()
    {
        InitialQuizTimerRuntime runtime = new();
        runtime.ApplyClientOwnerState("Quiz", "Problem", "Hint", 3, 7, 5, currentTickCount: 1000);

        bool built = runtime.TryBuildOwnerSnapshot(currentTickCount: 5101, out InitialQuizOwnerSnapshot snapshot);

        Assert.True(built);
        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot.RemainingSeconds);
        Assert.Equal(899, snapshot.RemainingMs);
        Assert.Equal(7, snapshot.QuestionNumber);
    }

    [Fact]
    public void TryBuildOwnerSnapshot_UsesRoundedUpRemainingSecondsForSpeedQuiz()
    {
        SpeedQuizOwnerRuntime runtime = new();
        runtime.ApplyClientOwnerState(2, 10, 4, 6, 3, currentTickCount: 2000);

        bool built = runtime.TryBuildOwnerSnapshot(currentTickCount: 4701, out SpeedQuizOwnerSnapshot snapshot);

        Assert.True(built);
        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot.RemainingSeconds);
        Assert.Equal(299, snapshot.RemainingMs);
        Assert.Equal(4, snapshot.CorrectAnswers);
    }
}
