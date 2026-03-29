using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.MobStructure;

namespace MapleLib.Tests;

public class MobDataSpecialInteractionParseTests
{
    [Fact]
    public void Parse_ReadsEncounterAndRageFieldsFromInfo()
    {
        WzImage image = CreateMobImage(
            CreateInfoProperty(
                new WzIntProperty("damagedByMob", 1),
                new WzIntProperty("escort", 2),
                new WzStringProperty("removeAfter", "15"),
                new WzIntProperty("ChargeCount", 3),
                new WzIntProperty("AngerGauge", 1),
                CreateSelfDestructionProperty(hp: 25, action: 2, removeAfter: 4)));

        MobData data = MobData.Parse(image, 9400633);

        Assert.NotNull(data);
        Assert.True(data.DamagedByMob);
        Assert.True(data.Friendly);
        Assert.Equal((byte)2, data.Escort);
        Assert.Equal(15, data.RemoveAfter);
        Assert.Equal(3, data.ChargeCount);
        Assert.True(data.HasAngerGauge);
        Assert.NotNull(data.SelfDestruction);
        Assert.Equal(25, data.SelfDestruction.Hp);
        Assert.Equal(2, data.SelfDestruction.Action);
        Assert.Equal(4, data.SelfDestruction.RemoveAfter);
    }

    [Fact]
    public void Parse_UsesDamagedByMobForFriendlyHpDisplay()
    {
        WzImage image = CreateMobImage(
            CreateInfoProperty(
                new WzIntProperty("damagedByMob", 1),
                new WzIntProperty("maxHP", 1000)));

        MobData data = MobData.Parse(image, 9300061);

        Assert.NotNull(data);
        Assert.Equal(MobHpDisplayType.Friendly, data.HpDisplayType);
    }

    [Fact]
    public void Parse_ReadsIntegerRemoveAfterVariant()
    {
        WzImage image = CreateMobImage(
            CreateInfoProperty(
                new WzIntProperty("removeAfter", 9)));

        MobData data = MobData.Parse(image, 9300061);

        Assert.NotNull(data);
        Assert.Equal(9, data.RemoveAfter);
    }

    private static WzImage CreateMobImage(params WzImageProperty[] properties)
    {
        var image = new WzImage("9400633.img");
        foreach (WzImageProperty property in properties)
        {
            image.AddProperty(property);
        }

        return image;
    }

    private static WzSubProperty CreateInfoProperty(params WzImageProperty[] properties)
    {
        var info = new WzSubProperty("info");
        foreach (WzImageProperty property in properties)
        {
            info.AddProperty(property);
        }

        return info;
    }

    private static WzSubProperty CreateSelfDestructionProperty(int hp, int action, int removeAfter)
    {
        var selfDestruction = new WzSubProperty("selfDestruction");
        selfDestruction.AddProperty(new WzIntProperty("hp", hp));
        selfDestruction.AddProperty(new WzIntProperty("action", action));
        selfDestruction.AddProperty(new WzIntProperty("removeAfter", removeAfter));
        return selfDestruction;
    }
}
