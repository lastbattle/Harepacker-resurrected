using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Effects;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;

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

    [Fact]
    public void Configure_LoadsTeamPresetSpeedAndBlockedItems()
    {
        BattlefieldField field = new();
        field.Enable();

        MapInfo mapInfo = new();
        mapInfo.additionalNonInfoProps.Add(CreateBattlefieldUserProperty());

        field.Configure(mapInfo);

        Assert.True(field.TryGetTeamLookPreset(0, out BattlefieldField.BattlefieldTeamLookPreset preset));
        Assert.Equal(1002919, preset.EquipmentItemIds[EquipSlot.Cap]);
        Assert.Equal(140f, preset.MoveSpeed);
        Assert.Equal(190, preset.MoveSpeedCap);
        Assert.Equal(new[] { 2022543, 2022548 }, preset.BlockedItemIds);
    }

    [Fact]
    public void TryGetAssignedTeamLookPreset_UsesRemoteTeamAssignment()
    {
        BattlefieldField field = new();
        field.Enable();
        field.SetLocalPlayerState(1337);

        MapInfo mapInfo = new();
        mapInfo.additionalNonInfoProps.Add(CreateBattlefieldUserProperty());
        field.Configure(mapInfo);

        field.OnTeamChanged(2001, 0, 1000);

        Assert.True(field.TryGetAssignedTeamLookPreset(2001, out BattlefieldField.BattlefieldTeamLookPreset preset));
        Assert.Equal(0, preset.TeamId);
        Assert.Equal(140f, preset.MoveSpeed);
    }

    private static WzSubProperty CreateBattlefieldUserProperty()
    {
        WzSubProperty user = new("user");
        WzSubProperty team0 = new("0");
        WzSubProperty cond = new("cond");
        cond.AddProperty(new WzIntProperty("battleFieldTeam", 0));
        team0.AddProperty(cond);

        WzSubProperty look = new("look");
        look.AddProperty(new WzIntProperty("cap", 1002919));
        look.AddProperty(new WzIntProperty("clothes", 1052168));
        look.AddProperty(new WzIntProperty("gloves", 1082247));
        team0.AddProperty(look);

        WzSubProperty stat = new("stat");
        stat.AddProperty(new WzIntProperty("speed", 140));
        stat.AddProperty(new WzIntProperty("speedmax", 190));
        team0.AddProperty(stat);

        WzSubProperty noitem = new("noitem");
        noitem.AddProperty(new WzIntProperty("0", 2022543));
        noitem.AddProperty(new WzIntProperty("1", 2022548));
        team0.AddProperty(noitem);

        user.AddProperty(team0);
        return user;
    }
}
