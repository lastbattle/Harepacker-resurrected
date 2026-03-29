using HaCreator.MapSimulator.Fields;

namespace UnitTest_MapSimulator;

public sealed class TemporaryPortalFieldRemotePortalParityTests
{
    [Theory]
    [InlineData(0, 1959, TemporaryPortalField.RemoteOpenGateVisualPhase.Opening)]
    [InlineData(0, 1960, TemporaryPortalField.RemoteOpenGateVisualPhase.Stable)]
    public void RemoteOpenGateOpeningPhase_UsesWzDurationBeforePromoting(
        int phaseStartedAt,
        int currentTime,
        TemporaryPortalField.RemoteOpenGateVisualPhase expected)
    {
        TemporaryPortalField.RemoteOpenGateVisualPhase actual =
            TemporaryPortalField.AdvanceRemoteOpenGatePhaseForTesting(
                TemporaryPortalField.RemoteOpenGateVisualPhase.Opening,
                phaseStartedAt,
                currentTime);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(0, 1799, TemporaryPortalField.RemoteOpenGateVisualPhase.Removing)]
    [InlineData(0, 1800, TemporaryPortalField.RemoteOpenGateVisualPhase.Removing)]
    public void RemoteOpenGateRemovingPhase_RemainsRemovalUntilRuntimeDeletes(
        int phaseStartedAt,
        int currentTime,
        TemporaryPortalField.RemoteOpenGateVisualPhase expected)
    {
        TemporaryPortalField.RemoteOpenGateVisualPhase actual =
            TemporaryPortalField.AdvanceRemoteOpenGatePhaseForTesting(
                TemporaryPortalField.RemoteOpenGateVisualPhase.Removing,
                phaseStartedAt,
                currentTime);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(TemporaryPortalField.RemoteOpenGateVisualPhase.Opening, false, TemporaryPortalField.RemoteOpenGateVisualMode.Opening)]
    [InlineData(TemporaryPortalField.RemoteOpenGateVisualPhase.Stable, false, TemporaryPortalField.RemoteOpenGateVisualMode.Solo)]
    [InlineData(TemporaryPortalField.RemoteOpenGateVisualPhase.Stable, true, TemporaryPortalField.RemoteOpenGateVisualMode.Linked)]
    [InlineData(TemporaryPortalField.RemoteOpenGateVisualPhase.Removing, false, TemporaryPortalField.RemoteOpenGateVisualMode.Removing)]
    public void RemoteOpenGateVisualMode_TracksClientLikePartnerState(
        TemporaryPortalField.RemoteOpenGateVisualPhase phase,
        bool hasPartner,
        TemporaryPortalField.RemoteOpenGateVisualMode expected)
    {
        TemporaryPortalField.RemoteOpenGateVisualMode actual =
            TemporaryPortalField.ResolveRemoteOpenGateVisualModeForTesting(phase, hasPartner);

        Assert.Equal(expected, actual);
    }
}
