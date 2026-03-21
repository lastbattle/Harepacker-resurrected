using HaCreator.MapSimulator.Effects;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;

namespace UnitTest_MapSimulator;

public class MassacreFieldTests
{
    [Fact]
    public void ConfigureReadsMobMassacreGaugeSettingsFromMapInfo()
    {
        var field = new SpecialEffectFields.MassacreField();
        field.Enable(926021200);

        field.Configure(CreateMapInfo(
            disableSkill: false,
            totalGauge: 200,
            decrease: 3,
            hitAdd: 5,
            coolAdd: 9,
            missSub: 12));

        Assert.Equal(200, field.MaxGauge);
        Assert.Equal(3, field.GaugeDecreasePerSecond);
        Assert.Equal(5, field.DefaultGaugeIncrease);
        Assert.False(field.IsSkillDisabled);
        Assert.Contains("nextCountEffect=250", field.DescribeStatus());
    }

    [Fact]
    public void DisableSkillMapsSuppressKeyAnimationButKeepGaugeUpdates()
    {
        var field = new SpecialEffectFields.MassacreField();
        field.Enable(926021200);
        field.Configure(CreateMapInfo(
            disableSkill: true,
            totalGauge: 200,
            decrease: 3,
            hitAdd: 7,
            coolAdd: 0,
            missSub: 0));

        field.AddKill(field.DefaultGaugeIncrease, currentTimeMs: 1000);
        field.Update(currentTimeMs: 1000, deltaSeconds: 0.016f);

        Assert.True(field.IsSkillDisabled);
        Assert.False(field.HasKeyAnimation);
        Assert.Equal(7, field.CurrentGauge);
    }

    private static MapInfo CreateMapInfo(bool disableSkill, int totalGauge, int decrease, int hitAdd, int coolAdd, int missSub)
    {
        var mobMassacre = new WzSubProperty("mobMassacre");
        mobMassacre.AddProperty(new WzIntProperty("mapDistance", 100));
        mobMassacre.AddProperty(new WzIntProperty("disableSkill", disableSkill ? 1 : 0));

        var gauge = new WzSubProperty("gauge");
        gauge.AddProperty(new WzIntProperty("total", totalGauge));
        gauge.AddProperty(new WzIntProperty("decrease", decrease));
        gauge.AddProperty(new WzIntProperty("hitAdd", hitAdd));
        gauge.AddProperty(new WzIntProperty("coolAdd", coolAdd));
        gauge.AddProperty(new WzIntProperty("missSub", missSub));
        mobMassacre.AddProperty(gauge);

        var countEffect = new WzSubProperty("countEffect");
        var threshold250 = new WzSubProperty("250");
        threshold250.AddProperty(new WzIntProperty("buff", 2022585));
        countEffect.AddProperty(threshold250);

        var threshold500 = new WzSubProperty("500");
        threshold500.AddProperty(new WzIntProperty("buff", 2022586));
        threshold500.AddProperty(new WzIntProperty("skillUse", 1));
        countEffect.AddProperty(threshold500);

        mobMassacre.AddProperty(countEffect);

        var mapInfo = new MapInfo();
        mapInfo.additionalNonInfoProps.Add(mobMassacre);
        return mapInfo;
    }
}
