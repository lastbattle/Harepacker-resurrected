using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Effects;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;

namespace UnitTest_MapSimulator;

public class BattlefieldFieldTests
{
    [Fact]
    public void ConfigureLoadsBattlefieldTeamLookPresetsFromMapUserData()
    {
        var mapInfo = new MapInfo
        {
            additionalNonInfoProps =
            {
                new WzSubProperty("battleField")
                {
                    ["timeDefault"] = new WzIntProperty("timeDefault", 300),
                    ["timeFinish"] = new WzIntProperty("timeFinish", 3),
                },
                CreateUserProperty(),
            }
        };

        var field = new BattlefieldField();
        field.Enable();
        field.Configure(mapInfo);

        Assert.True(field.TryGetTeamLookPreset(0, out BattlefieldField.BattlefieldTeamLookPreset wolvesPreset));
        Assert.Equal(1002919, wolvesPreset.EquipmentItemIds[EquipSlot.Cap]);
        Assert.Equal(1052168, wolvesPreset.EquipmentItemIds[EquipSlot.Longcoat]);
        Assert.Equal(1082247, wolvesPreset.EquipmentItemIds[EquipSlot.Glove]);
        Assert.Equal(1072367, wolvesPreset.EquipmentItemIds[EquipSlot.Shoes]);
        Assert.Equal(1102039, wolvesPreset.EquipmentItemIds[EquipSlot.Cape]);

        Assert.True(field.TryGetTeamLookPreset(1, out BattlefieldField.BattlefieldTeamLookPreset sheepPreset));
        Assert.Equal(1002923, sheepPreset.EquipmentItemIds[EquipSlot.Cap]);
        Assert.Equal(1052186, sheepPreset.EquipmentItemIds[EquipSlot.Longcoat]);

        Assert.True(field.TryGetTeamLookPreset(2, out BattlefieldField.BattlefieldTeamLookPreset teamTwoPreset));
        Assert.Equal(1002186, teamTwoPreset.EquipmentItemIds[EquipSlot.Cap]);
        Assert.Equal(1042162, teamTwoPreset.EquipmentItemIds[EquipSlot.Coat]);
        Assert.Equal(1062112, teamTwoPreset.EquipmentItemIds[EquipSlot.Pants]);
    }

    private static WzSubProperty CreateUserProperty()
    {
        var user = new WzSubProperty("user");
        user.AddProperty(CreateUserEntry("0", 0, 1002919, 1052168, null, 1082247, 1072367, 1102039));
        user.AddProperty(CreateUserEntry("1", 1, 1002923, 1052186, null, 1082250, 1072377, 1102039));
        user.AddProperty(CreateUserEntry("2", 2, 1002186, 1042162, 1062112, 1082102, 1072153, 1102039));
        return user;
    }

    private static WzSubProperty CreateUserEntry(
        string entryName,
        int teamId,
        int cap,
        int clothes,
        int? pants,
        int gloves,
        int shoes,
        int cape)
    {
        var entry = new WzSubProperty(entryName);

        var cond = new WzSubProperty("cond");
        cond.AddProperty(new WzIntProperty("battleFieldTeam", teamId));
        entry.AddProperty(cond);

        var look = new WzSubProperty("look");
        look.AddProperty(new WzIntProperty("cap", cap));
        look.AddProperty(new WzIntProperty("clothes", clothes));
        if (pants.HasValue)
        {
            look.AddProperty(new WzIntProperty("pants", pants.Value));
        }
        look.AddProperty(new WzIntProperty("gloves", gloves));
        look.AddProperty(new WzIntProperty("shoes", shoes));
        look.AddProperty(new WzIntProperty("cape", cape));
        entry.AddProperty(look);

        return entry;
    }
}
