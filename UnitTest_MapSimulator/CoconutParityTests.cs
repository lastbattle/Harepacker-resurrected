using HaCreator.MapSimulator.Fields;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public sealed class CoconutParityTests
{
    [Fact]
    public void OnCoconutScore_UsesAuthoredMessageDurationForFinalResult()
    {
        var field = new CoconutField();
        field.Initialize(1, new Rectangle(0, 0, 32, 32), 0);
        field.ConfigureAuthoredPreviewForTesting(
            previewTreeHitCount: 5,
            defaultRoundDurationSeconds: 300,
            messageDurationMs: 6000,
            finalScoreMessageDurationMs: 12000,
            eventName: "Coconut Harvest");

        field.StartGame(300, currentTick: 1000);
        field.OnClock(0, 2000);
        field.OnCoconutScore(1, 0, 3000);

        Assert.Equal(CoconutField.RoundResult.Victory, field.LastRoundResult);
        Assert.Equal(9000, field.MessageExpiresAtTick);
    }

    [Fact]
    public void ConfigureAuthoredAssetsForTesting_PersistsWzBackedEffectAndSoundPaths()
    {
        var field = new CoconutField();

        field.ConfigureAuthoredAssetsForTesting(
            victoryEffectPath: "event/coconut/victory",
            loseEffectPath: "event/coconut/lose",
            victorySoundPath: "Coconut/Victory",
            loseSoundPath: "Coconut/Failed");

        Assert.Equal("event/coconut/victory", field.VictoryEffectPath);
        Assert.Equal("event/coconut/lose", field.LoseEffectPath);
        Assert.Equal("Coconut/Victory", field.VictorySoundPath);
        Assert.Equal("Coconut/Failed", field.LoseSoundPath);
    }
}
