using HaCreator.MapSimulator.Effects;

namespace UnitTest_MapSimulator;

public sealed class BattlefieldFieldTests
{
    [Fact]
    public void OnTeamChanged_UsesConfiguredLocalCharacterId_ForLocalTeam()
    {
        BattlefieldField field = new();
        field.Enable();
        field.SetLocalPlayerState(1337);

        field.OnTeamChanged(1337, 1, 1000);

        Assert.Equal(1, field.LocalTeamId);
        Assert.Empty(field.RemoteUserTeams);
    }

    [Fact]
    public void OnTeamChanged_TracksRemoteTeams_WithoutOverwritingLocalTeam()
    {
        BattlefieldField field = new();
        field.Enable();
        field.SetLocalPlayerState(1337);
        field.SetLocalTeam(0, 1000);

        field.OnTeamChanged(2001, 1, 1200);
        field.OnTeamChanged(2002, 2, 1300);

        Assert.Equal(0, field.LocalTeamId);
        Assert.Equal(1, field.RemoteUserTeams[2001]);
        Assert.Equal(2, field.RemoteUserTeams[2002]);
    }

    [Fact]
    public void SetLocalPlayerState_PromotesPreviouslyTrackedRemoteTeam()
    {
        BattlefieldField field = new();
        field.Enable();

        field.OnTeamChanged(3003, 2, 1000);
        field.SetLocalPlayerState(3003);

        Assert.Equal(2, field.LocalTeamId);
        Assert.Empty(field.RemoteUserTeams);
    }
}
